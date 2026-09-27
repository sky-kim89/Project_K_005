using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

namespace SoulMercenaries
{
    // Map, units, fog and camera in world space. Input only gives orders; rules stay in the session.
    // Map cell (x, y) with y growing downward maps to world (x, -y): row 0 is the top of the screen.
    //
    // Controls: left click = move order · left drag / right drag = pan · wheel = zoom · arrows/WASD = pan · Space = follow again.
    public sealed class SoulWorldView : MonoBehaviour
    {
        const float DragThreshold = 12f, MinZoom = 3.5f, DefaultZoom = 6.5f, MaxZoom = 30f;

        public SoulGameplayController Controller;
        public SoulHudView Hud;
        public Camera Camera;
        [Tooltip("Stripped PixelHeroes character (SoulUnit.prefab).")]
        public GameObject CharacterPrefab;
        public Sprite Ground;
        public Sprite MagicBolt, Arrow;

        readonly Dictionary<SoulCombatant, SoulUnitView> units = new Dictionary<SoulCombatant, SoulUnitView>();
        // Hidden doors not found yet (they look like rock until then).
        readonly List<Vector2Int> hiddenDoors = new List<Vector2Int>();
        // Monster views are created the first time the party sees the monster's tile (big maps hold hundreds).
        readonly List<SoulMonster> unseenMonsters = new List<SoulMonster>();
        Transform unitRoot;
        Tilemap rocks;
        Tile doorTile;
        SpriteRenderer destination;
        readonly List<(Vector2Int cell, SpriteRenderer marker)> exitMarkers = new List<(Vector2Int, SpriteRenderer)>();
        readonly Dictionary<SoulChest, SpriteRenderer> chests = new Dictionary<SoulChest, SpriteRenderer>();
        readonly Dictionary<SoulInteractable, (SpriteRenderer body, TMPro.TextMeshPro label)> objects = new Dictionary<SoulInteractable, (SpriteRenderer, TMPro.TextMeshPro)>();
        readonly Dictionary<SoulTrap, SpriteRenderer> traps = new Dictionary<SoulTrap, SpriteRenderer>();
        int knownMonsters;
        // Telegraphed attacks: the whole area faint red, a stronger fill growing from the caster until the hit.
        readonly Dictionary<SoulTelegraph, (SpriteRenderer zone, SpriteRenderer fill)> telegraphs = new Dictionary<SoulTelegraph, (SpriteRenderer, SpriteRenderer)>();
        Transform effects;
        SoulFieldFx fieldFx;
        Texture2D fogTexture;
        Color32[] fogPixels;
        int fogRevision = -1, fogLogged;
        float zoom = DefaultZoom;
        bool following = true, dragging, pressed;
        Vector3 pressPosition, lastMouse, focus;

        public static Vector3 ToWorld(Vector2 position) => new Vector3(position.x, -position.y, 0);
        public static Vector2 ToMap(Vector3 world) => new Vector2(world.x, -world.y);

        // Visible map area in map coordinates (y down) — the minimap draws it.
        public Rect ViewRect
        {
            get
            {
                float height = Camera.orthographicSize * 2, width = height * Camera.aspect;
                var center = ToMap(Camera.transform.position);
                return new Rect(center.x - width / 2, center.y - height / 2, width, height);
            }
        }

        public bool Following => following;

        public Sprite Portrait(SoulCombatant unit) => unit != null && units.TryGetValue(unit, out var view) ? view.BodySprite : null;


        public void FocusOn(Vector2 mapPosition)
        {
            following = false;
            focus = ToWorld(mapPosition);
        }

        public void Follow() => following = true;

        // Move order on a tile (minimap / map popup): its center.
        public void Command(Vector2Int cell) => Command(Controller.Session.Map.Center(cell));

        // Move order for the selected mercenary, or the whole party in a loose formation. Orders land on the
        // exact clicked point (fine navigation grid), not on the tile center.
        public void Command(Vector2 point)
        {
            var session = Controller.Session;
            var hero = Controller.SelectedMercenary;
            if (hero == null || !hero.Alive) return;
            var cell = session.Map.Cell(point);
            bool knownExit = session.IsExit(cell) && session.IsExitKnown(cell);
            if (!session.IsExplored(cell) && !knownExit) { Hud?.Toast("탐험하지 않은 지역입니다."); return; }
            if (!Controller.MoveParty)
            {
                if (!session.SetDestination(hero, point, out Vector2 actual)) Hud?.Toast("도달할 수 없는 위치입니다.");
                else if (Vector2.Distance(actual, point) > .5f) Hud?.Toast("가장 가까운 이동 가능 위치로 보정했습니다.");
                return;
            }
            var offsets = new[] { Vector2.zero, new Vector2(-.8f, 0), new Vector2(.8f, 0), new Vector2(0, -.8f), new Vector2(0, .8f), new Vector2(-.8f, .8f), new Vector2(.8f, .8f) };
            int slot = 0;
            bool any = false;
            foreach (var member in session.Mercenaries)
                if (member.Alive) any |= session.SetDestination(member, point + offsets[slot++ % offsets.Length], out Vector2 _);
            if (!any) Hud?.Toast("도달할 수 없는 위치입니다.");
        }

        void Start()
        {
            AudioManager.Instance?.PlayBgm(BgmKey.InGame);
            var session = Controller.Session;
            var map = session.Map;
            BuildGround(map);
            BuildFog(map);
            BuildFilter(session.Theme);
            effects = new GameObject("Effects").transform;
            effects.SetParent(transform, false);
            fieldFx = effects.gameObject.AddComponent<SoulFieldFx>();

            foreach (var cell in session.Exits)
            {
                // Above the fog: a pathfinder knows where the portals are before the party has been there.
                var marker = SoulSprites.Renderer("Exit", transform, SoulSprites.Diamond, Color.white, 720);
                marker.transform.position = ToWorld(map.Center(cell));
                var exitLabel = SoulSprites.WorldText("ExitLabel", marker.transform, session.Exits.Count > 1 ? "포탈" : "출구", 2.4f, SoulUi.Accent, 721);
                exitLabel.transform.localPosition = new Vector3(0, -.62f, 0);
                exitMarkers.Add((cell, marker));
            }
            destination = SoulSprites.Renderer("Destination", transform, SoulSprites.Ring, new Color(.3f, 1f, 1f, .8f), 6);

            unitRoot = new GameObject("Units").transform;
            unitRoot.SetParent(transform, false);
            foreach (var hero in session.Mercenaries) units[hero] = SoulUnitView.Create(hero, CharacterPrefab, unitRoot);
            unseenMonsters.AddRange(session.Monsters);
            knownMonsters = session.Monsters.Count;
            SpawnSeenMonsters(session);

            var selected = Controller.SelectedMercenary;
            if (selected != null)
            {
                focus = ToWorld(selected.Position);
                Camera.transform.position = focus + Vector3.back * 10;
            }
            Camera.orthographicSize = zoom;
        }

        void BuildGround(SoulMap map)
        {
            var theme = Controller.Session.Theme;
            var root = new GameObject("Ground").transform;
            root.SetParent(transform, false);
            var floorGround = SoulSprites.FloorGround(Controller.Session.Floor);
            if (floorGround != null)
            {
                // the floor's own ground, tiled over the whole map in one renderer
                var tiled = SoulSprites.Renderer("Backdrop", root, floorGround, Color.white, -100);
                tiled.drawMode = SpriteDrawMode.Tiled;
                tiled.size = new Vector2(map.Width + 2, map.Height + 2);
                tiled.transform.position = new Vector3(map.Width / 2f, -map.Height / 2f, 0);
            }
            else if (Ground != null)
            {
                // The original battle backdrop tiled over the dungeon (one stretch would blur a large map).
                var size = Ground.bounds.size;
                float tileW = 16, tileH = 16 * size.y / size.x;
                for (float y = -1; y < map.Height + 1; y += tileH)
                    for (float x = -1; x < map.Width + 1; x += tileW)
                    {
                        var tile = SoulSprites.Renderer("Backdrop", root, Ground, theme.Tint, -100);
                        tile.transform.position = new Vector3(x + tileW / 2, -(y + tileH / 2), 0);
                        tile.transform.localScale = new Vector3(tileW / size.x, tileH / size.y, 1);
                    }
            }
            else
            {
                var fill = SoulSprites.Renderer("Backdrop", root, SoulSprites.Pixel, new Color(.34f, .24f, .15f) * theme.Tint, -100);
                fill.transform.position = new Vector3(map.Width / 2f, -map.Height / 2f, 0);
                fill.transform.localScale = new Vector3(map.Width + 2, map.Height + 2, 1);
            }
            // Walls, lava and deep rock go into tilemaps: one renderer per layer instead of a GameObject per
            // cell (a 100× map has hundreds of thousands of wall cells).
            var grid = new GameObject("Grid", typeof(Grid)).transform;
            grid.SetParent(root, false);
            var deep = Layer(grid, "Deep", -90, TilemapRenderer.Mode.Chunk);
            var lava = Layer(grid, "Lava", -95, TilemapRenderer.Mode.Chunk);
            // Boulders sort one by one with the units (custom Y axis), so characters walk in front of/behind them.
            rocks = Layer(grid, "Rocks", 100, TilemapRenderer.Mode.Individual);
            Tile deepTile = MakeTile(SoulSprites.Pixel, new Color(.09f, .08f, .08f), 1.02f);
            var lavaTile = MakeTile(SoulSprites.Lava, Color.white, 1.01f);
            doorTile = MakeTile(SoulSprites.Door, Color.white, 1f);
            int floor = Controller.Session.Floor;
            bool floorArt = SoulSprites.FloorWall(floor, 255, 0) != null;
            lava.color = theme.Tint; // the floor's own colour on the dungeon itself
            deep.color = rocks.color = floorArt ? Color.white : theme.Tint; // floor wall art is coloured already
            if (floorArt) deepTile = MakeTile(SoulSprites.FloorWall(floor, 255, 0), Color.white, 1f); // the rock mass itself
            var rockTiles = new Dictionary<Sprite, Tile>();
            var deepCells = new List<Vector3Int>(); var lavaCells = new List<Vector3Int>(); var rockCells = new List<Vector3Int>();
            var rockList = new List<TileBase>();
            for (int y = 0; y < map.Height; y++) for (int x = 0; x < map.Width; x++)
            {
                var tile = map.Tile(x, y);
                if (tile == SoulTile.Floor) continue;
                if (tile == SoulTile.Lava) { lavaCells.Add(TileCell(x, y)); continue; }
                // Deep rock (no floor around it) is a dark block; the boulder sprite only where walls face a floor.
                if (!TouchesFloor(map, x, y)) { deepCells.Add(TileCell(x, y)); continue; }
                // 8-direction wall piece from the rock around it (the old boulders on floors without wall art)
                var sprite = floorArt ? SoulSprites.FloorWall(floor, WallMask(map, x, y), x * 31 + y * 17) : SoulSprites.Rock(x * 31 + y * 17);
                if (!rockTiles.TryGetValue(sprite, out var rockTile)) rockTiles[sprite] = rockTile = MakeTile(sprite, Color.white, floorArt ? 1f : 1.12f);
                rockCells.Add(TileCell(x, y));
                rockList.Add(rockTile);
                if (tile == SoulTile.HiddenDoor) hiddenDoors.Add(new Vector2Int(x, y)); // looks like rock until found
            }
            deep.SetTiles(deepCells.ToArray(), Fill(deepTile, deepCells.Count));
            lava.SetTiles(lavaCells.ToArray(), Fill(lavaTile, lavaCells.Count));
            rocks.SetTiles(rockCells.ToArray(), rockList.ToArray());
        }

        // Map cell (x, y) → tilemap cell whose square is centered on ToWorld(Center(x, y)).
        // Which of the 8 neighbours are rock too (outside the map counts as rock; lava and floor are open).
        static int WallMask(SoulMap map, int x, int y)
        {
            int mask = 0;
            void Check(int dx, int dy, int bit)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= map.Width || ny >= map.Height) { mask |= bit; return; }
                var tile = map.Tile(nx, ny);
                if (tile != SoulTile.Floor && tile != SoulTile.Lava) mask |= bit;
            }
            Check(0, -1, SoulSprites.WallN); Check(1, -1, SoulSprites.WallNE); Check(1, 0, SoulSprites.WallE); Check(1, 1, SoulSprites.WallSE);
            Check(0, 1, SoulSprites.WallS); Check(-1, 1, SoulSprites.WallSW); Check(-1, 0, SoulSprites.WallW); Check(-1, -1, SoulSprites.WallNW);
            return mask;
        }

        static Vector3Int TileCell(int x, int y) => new Vector3Int(x, -y - 1, 0);

        static Tilemap Layer(Transform grid, string name, int order, TilemapRenderer.Mode mode)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid, false);
            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            renderer.mode = mode;
            return go.GetComponent<Tilemap>();
        }

        static Tile MakeTile(Sprite sprite, Color color, float scale)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = color;
            tile.transform = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            tile.flags = TileFlags.LockAll;
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        static TileBase[] Fill(TileBase tile, int count)
        {
            var tiles = new TileBase[count];
            for (int i = 0; i < count; i++) tiles[i] = tile;
            return tiles;
        }

        void SpawnSeenMonsters(SoulDungeonSession session)
        {
            for (; knownMonsters < session.Monsters.Count; knownMonsters++)
                if (!units.ContainsKey(session.Monsters[knownMonsters]) && !unseenMonsters.Contains(session.Monsters[knownMonsters])) unseenMonsters.Add(session.Monsters[knownMonsters]);
            for (int i = unseenMonsters.Count - 1; i >= 0; i--)
            {
                var monster = unseenMonsters[i];
                if (!monster.Alive) { unseenMonsters.RemoveAt(i); continue; } // died out of sight: never shown
                if (!session.IsSeen(session.Map.Cell(monster.Position))) continue;
                units[monster] = SoulUnitView.Create(monster, CharacterPrefab, unitRoot);
                unseenMonsters.RemoveAt(i);
            }
        }

        static bool TouchesFloor(SoulMap map, int x, int y)
        {
            if (map.Tile(x, y) == SoulTile.HiddenDoor) return true;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) if (map.Tile(x + dx, y + dy) != SoulTile.Wall) return true;
            return false;
        }

        // One pixel per cell, bilinear so the edge of the known world is soft.
        void BuildFog(SoulMap map)
        {
            fogTexture = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            fogPixels = new Color32[map.Width * map.Height];
            for (int i = 0; i < fogPixels.Length; i++) fogPixels[i] = FogDark;
            var sprite = Sprite.Create(fogTexture, new Rect(0, 0, map.Width, map.Height), Vector2.zero, 1, 0, SpriteMeshType.FullRect);
            var fog = SoulSprites.Renderer("Fog", transform, sprite, Color.white, 700);
            fog.transform.position = new Vector3(0, -map.Height, 0);
        }

        // What the party has not seen is solid dark — nothing of the ground, the walls or the rooms beyond a wall
        // shows through. The soft edge of the fog sits on the last explored tiles instead, inside known ground.
        static readonly Color32 FogDark = new Color32(6, 7, 11, 255), FogEdge = new Color32(6, 7, 11, 150), FogClear = new Color32(0, 0, 0, 0);

        void PaintFog(SoulDungeonSession session, int x, int y)
        {
            var map = session.Map;
            if (x < 0 || y < 0 || x >= map.Width || y >= map.Height) return;
            bool Unseen(int nx, int ny) => nx >= 0 && ny >= 0 && nx < map.Width && ny < map.Height && !session.IsSeen(new Vector2Int(nx, ny));
            var color = Unseen(x, y) ? FogDark
                : Unseen(x + 1, y) || Unseen(x - 1, y) || Unseen(x, y + 1) || Unseen(x, y - 1) ? FogEdge
                : FogClear;
            fogPixels[(map.Height - 1 - y) * map.Width + x] = color;
        }

        // The floor's signature filter: a translucent sheet of colour over the dungeon and its units, but *under*
        // the fog (order 700), so places the party has not seen stay black instead of taking the floor's colour.
        void BuildFilter(SoulFloorTheme theme)
        {
            var filter = SoulSprites.Renderer("FloorFilter", Camera.transform, SoulSprites.Pixel, theme.Filter, 690);
            filter.transform.localPosition = new Vector3(0, 0, 2);
            filter.transform.localScale = new Vector3(600, 600, 1); // larger than any zoom level
        }

        void OnDestroy() { if (fogTexture != null) Destroy(fogTexture); }

        void UpdateFog(SoulDungeonSession session)
        {
            if (fogRevision == session.ExploreRevision) return;
            fogRevision = session.ExploreRevision;
            var map = session.Map;
            // Only the tiles explored since the last update (the log is append-only).
            var log = session.SeenLog;
            for (; fogLogged < log.Count; fogLogged++)
            {
                int index = log[fogLogged], x = index % map.Width, y = index / map.Width;
                // The new tile, and its neighbours: they may have stopped being the edge of the fog.
                PaintFog(session, x, y);
                PaintFog(session, x + 1, y); PaintFog(session, x - 1, y); PaintFog(session, x, y + 1); PaintFog(session, x, y - 1);
            }
            fogTexture.SetPixels32(fogPixels);
            fogTexture.Apply();
            for (int i = hiddenDoors.Count - 1; i >= 0; i--)
                if (map.IsDiscovered(hiddenDoors[i]))
                {
                    rocks.SetTile(TileCell(hiddenDoors[i].x, hiddenDoors[i].y), doorTile);
                    hiddenDoors.RemoveAt(i);
                }
            SpawnSeenMonsters(session);
        }

        void Update()
        {
            var session = Controller.Session;
            if (session == null) return;
            UpdateFog(session);
            DrawTelegraphs(session);
            DrawObjects(session);
            SpawnSeenMonsters(session);
            foreach (var other in session.Shared.Heroes) if (!units.ContainsKey(other)) units[other] = SoulUnitView.Create(other, CharacterPrefab, unitRoot);
            HandleInput(session);
            MoveCamera(session.Map);
            DrawMarkers(session);
            PlayEvents(session);
            fieldFx.Sync(session.FloorParties, point => session.IsSeen(session.Map.Cell(point)));
            var selected = Controller.SelectedMercenary;
            foreach (var entry in units)
            {
                entry.Value.SetSelected(entry.Key == selected);
                // Monsters show only where the party has looked.
                // other parties on the floor: where this party has looked, while they are still down here
                if (entry.Key is SoulMercenary other && !session.Mercenaries.Contains(other))
                    entry.Value.SetHidden(!session.Shared.Holds(other) || !session.IsSeen(session.Map.Cell(other.Position)));
                if (entry.Key is SoulMonster monster)
                {
                    entry.Value.SetHidden(!session.IsSeen(session.Map.Cell(monster.Position)));
                }
            }
        }

        void MoveCamera(SoulMap map)
        {
            Rect area = Hud != null ? Hud.MapScreenRect : new Rect(0, 0, Screen.width, Screen.height);
            if (area.width < 10 || area.height < 10) return;
            Camera.pixelRect = area;
            // Whole map on small dungeons; on large ones a regional view (the map popup shows the whole thing).
            float maxZoom = Mathf.Min(MaxZoom, Mathf.Max(map.Height, map.Width / Camera.aspect) / 2f + 1);
            zoom = Mathf.Clamp(zoom, MinZoom, maxZoom);
            Camera.orthographicSize = Mathf.Lerp(Camera.orthographicSize, zoom, 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime));
            var selected = Controller.SelectedMercenary;
            if (following && selected != null && selected.Alive) focus = ToWorld(selected.Position);
            float halfH = Camera.orthographicSize, halfW = halfH * Camera.aspect;
            focus.x = map.Width <= halfW * 2 ? map.Width / 2f : Mathf.Clamp(focus.x, halfW - .5f, map.Width - halfW + .5f);
            focus.y = map.Height <= halfH * 2 ? -map.Height / 2f : Mathf.Clamp(focus.y, -map.Height + halfH - .5f, -halfH + .5f);
            var target = new Vector3(focus.x, focus.y, -10);
            Camera.transform.position = Vector3.Lerp(Camera.transform.position, target, 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
        }

        void HandleInput(SoulDungeonSession session)
        {
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool inside = Camera.pixelRect.Contains(Input.mousePosition);
            if (inside && !overUi && Mathf.Abs(Input.mouseScrollDelta.y) > .01f) zoom *= Input.mouseScrollDelta.y > 0 ? .85f : 1.18f;
            if (Input.GetKeyDown(KeyCode.Space)) Follow();
            Vector2 keys = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (keys.sqrMagnitude > .01f)
            {
                following = false;
                focus += (Vector3)(keys.normalized * zoom * 1.5f * Time.unscaledDeltaTime);
            }

            // Pan with right drag, or left drag past a small threshold; a short left click is a move order.
            if (Input.GetMouseButton(1) && inside && !overUi && !Input.GetMouseButtonDown(1)) PanBy(Input.mousePosition - lastMouse);
            if (Input.GetMouseButtonDown(0) && inside && !overUi) { pressed = true; dragging = false; pressPosition = Input.mousePosition; }
            if (pressed && Input.GetMouseButton(0))
            {
                if (!dragging && (Input.mousePosition - pressPosition).magnitude > DragThreshold) dragging = true;
                if (dragging) PanBy(Input.mousePosition - lastMouse);
            }
            if (pressed && Input.GetMouseButtonUp(0))
            {
                pressed = false;
                if (!dragging) Click(session, Input.mousePosition);
            }
            lastMouse = Input.mousePosition;
        }

        void PanBy(Vector3 screenDelta)
        {
            following = false;
            float unitsPerPixel = Camera.orthographicSize * 2 / Mathf.Max(1, Camera.pixelHeight);
            focus -= screenDelta * unitsPerPixel;
        }

        void Click(SoulDungeonSession session, Vector3 screen)
        {
            Vector2 point = ToMap(Camera.ScreenToWorldPoint(screen));
            SoulInteractable clicked = null;
            float nearest = 1.8f; // generous: at high speed the party walks past quickly
            foreach (var thing in session.Interactables)
            {
                float d = Vector2.Distance(thing.Position, point);
                if (d < nearest && session.IsSeen(session.Map.Cell(thing.Position))) { nearest = d; clicked = thing; }
            }
            if (clicked != null)
            {
                var user = Controller.SelectedMercenary != null && Controller.SelectedMercenary.Alive ? Controller.SelectedMercenary : session.Leader;
                string block = session.InteractBlock(clicked);
                if (block != null) Hud?.Toast(clicked.Name + ": " + block);
                else if (clicked.Kind == SoulObjectKind.Warp)
                    ConfirmPopup.Ask("warp", "워프석을 사용할까요?\n<size=22>어디로 이동할지 알 수 없습니다. 초보 파티는 전멸할 수도 있습니다.</size>",
                        () => { if (!session.Interact(clicked, user)) Hud?.Toast("지금은 사용할 수 없습니다"); });
                else if (!session.Interact(clicked, user)) Hud?.Toast("지금은 사용할 수 없습니다");
                return;
            }
            // Clicking a mercenary selects it instead of moving the current one.
            for (int i = 0; i < session.Mercenaries.Count; i++)
                if (session.Mercenaries[i].Alive && Vector2.Distance(session.Mercenaries[i].Position, point) < .5f)
                {
                    Controller.SelectedHero = i;
                    Follow();
                    return;
                }
            Command(point);
        }

        void DrawMarkers(SoulDungeonSession session)
        {
            float pulse = .5f + .5f * Mathf.Sin(Time.time * 4f);
            foreach (var (cell, marker) in exitMarkers)
            {
                marker.transform.localScale = Vector3.one * (.9f + .15f * pulse);
                marker.color = session.ExitOpen ? Color.white : new Color(.6f, .6f, .6f, .6f);
                marker.enabled = session.IsExitKnown(cell); // a pathfinder knows where the portals are before seeing them
            }
            foreach (var chest in session.Chests)
            {
                if (!chests.TryGetValue(chest, out var box))
                {
                    chests[chest] = box = SoulSprites.Renderer("Chest", transform, SoulSprites.Chest, Color.white, 100);
                    box.spriteSortPoint = SpriteSortPoint.Pivot;
                    box.transform.position = ToWorld(chest.Position);
                }
                box.sprite = chest.Opened ? SoulSprites.ChestOpen : SoulSprites.Chest;
                box.transform.localScale = Vector3.one * (chest.Opened ? 1f : 1f + .08f * pulse);
                box.enabled = session.IsSeen(session.Map.Cell(chest.Position));
            }

            var hero = Controller.SelectedMercenary;
            bool show = hero != null && hero.Alive && !session.HasArrived(hero);
            destination.enabled = show;
            if (!show) return;
            destination.transform.position = ToWorld(session.DestinationPoint(hero));
            destination.transform.localScale = new Vector3(.9f, .45f, 1) * (1 + .1f * pulse);
        }

        readonly List<SoulTelegraph> floorTelegraphs = new List<SoulTelegraph>();

        // Every party's attacks on this floor (the monsters' live with the party that moves them).
        void DrawTelegraphs(SoulDungeonSession session)
        {
            floorTelegraphs.Clear();
            foreach (var party in session.FloorParties) floorTelegraphs.AddRange(party.Telegraphs);
            foreach (var t in floorTelegraphs)
            {
                if (!telegraphs.TryGetValue(t, out var pair))
                {
                    var sprite = t.Shape == SoulAreaShape.Circle ? SoulSprites.Disc : t.Shape == SoulAreaShape.Cone ? SoulSprites.Cone(t.Width) : SoulSprites.Pixel;
                    pair = (SoulSprites.Renderer("Telegraph", transform, sprite, Color.clear, -40), SoulSprites.Renderer("TelegraphFill", transform, sprite, Color.clear, -39));
                    telegraphs[t] = pair;
                }
                Shape(t, pair.zone, 1);
                Shape(t, pair.fill, t.Progress);
                // Enemy areas are the loud red warning; the party's own swings are a quiet blue sweep, so a
                // screen full of allied attacks never drowns out the one thing the player must step out of.
                bool threat = t.Hostile;
                float weight = t.Signature ? 1f : .55f; // a named boss pattern shouts, an ordinary swing does not
                if (!t.Struck)
                {
                    pair.zone.color = threat
                        ? new Color(1f, .12f, .08f, (.2f + .08f * Mathf.Sin(Time.time * 14)) * weight)
                        : new Color(.45f, .75f, 1f, .12f * weight);
                    pair.fill.color = threat ? new Color(1f, .15f, .1f, .38f * weight) : new Color(.55f, .85f, 1f, .26f * weight);
                }
                else
                {
                    // Impact: a hot flash fading over the linger time.
                    float fade = 1 - Mathf.Clamp01((t.Elapsed - t.WindUp) / SoulDungeonSession.TelegraphLinger);
                    pair.zone.color = threat ? new Color(1f, .8f, .35f, .55f * fade) : new Color(.8f, .95f, 1f, .4f * fade * weight);
                    pair.fill.color = threat ? new Color(1f, 1f, .85f, .5f * fade) : new Color(1f, 1f, 1f, .35f * fade * weight);
                }
            }
            if (telegraphs.Count == floorTelegraphs.Count) return;
            var gone = new List<SoulTelegraph>();
            foreach (var entry in telegraphs) if (!floorTelegraphs.Contains(entry.Key)) gone.Add(entry.Key);
            foreach (var t in gone)
            {
                Destroy(telegraphs[t].zone.gameObject);
                Destroy(telegraphs[t].fill.gameObject);
                telegraphs.Remove(t);
            }
        }

        SpriteRenderer campfire;

        void DrawObjects(SoulDungeonSession session)
        {
            // a camp from a kit: a fire in the middle of the party
            bool fire = session.Camping > 0 && session.CampFire;
            if (fire && campfire == null) campfire = SoulSprites.Renderer("Campfire", transform, SoulSprites.Object(SoulObjectKind.Campfire), Color.white, 100);
            if (campfire != null)
            {
                campfire.gameObject.SetActive(fire);
                if (fire) campfire.transform.position = ToWorld(session.CampSpot);
            }
            foreach (var thing in session.Interactables)
            {
                if (!objects.TryGetValue(thing, out var view))
                {
                    var body = SoulSprites.Renderer(thing.Name, transform, SoulSprites.Object(thing.Kind), Color.white, 100);
                    body.spriteSortPoint = SpriteSortPoint.Pivot;
                    body.transform.position = ToWorld(thing.Position) + Vector3.down * .25f;
                    body.transform.localScale = Vector3.one * 1.3f;
                    var label = SoulSprites.WorldText("Label", body.transform, thing.Name, 2f, SoulUi.Accent, 101);
                    label.transform.localPosition = new Vector3(0, -.35f, 0);
                    objects[thing] = view = (body, label);
                }
                bool seen = session.IsSeen(session.Map.Cell(thing.Position));
                view.body.enabled = view.label.enabled = seen;
                bool ready = session.CanUse(thing);
                view.body.color = ready ? Color.white : new Color(.5f, .5f, .5f, .6f);
                view.label.text = thing.Name + (thing.Used ? " (사용함)" : thing.Cooldown > 0 ? $" ({thing.Cooldown:0}초)" : thing.Kind == SoulObjectKind.Fortune ? $" · 금화 {session.FortuneCost}" : "");
                if (thing.Kind == SoulObjectKind.Warp && ready) view.body.transform.localRotation = Quaternion.Euler(0, 0, Time.time * 40);
                if (thing.Kind == SoulObjectKind.Escape) view.body.transform.localScale = Vector3.one * (1 + .06f * Mathf.Sin(Time.time * 3));
            }
            foreach (var trap in session.Traps)
            {
                if (!trap.Found && !trap.Sprung) continue; // hidden until spotted or stepped on
                if (!traps.TryGetValue(trap, out var plate))
                {
                    traps[trap] = plate = SoulSprites.Renderer("Trap", transform, SoulSprites.Trap(false), Color.white, -38);
                    plate.transform.position = ToWorld(trap.Position);
                }
                plate.sprite = SoulSprites.Trap(trap.Sprung);
                var tint = trap.Kind == SoulTrapKind.Warp ? new Color(.45f, .95f, 1f) : Color.white; // a warp plate glows cyan
                plate.color = trap.Sprung ? tint : new Color(tint.r, tint.g, tint.b, .55f); // a disarmed one fades
                plate.enabled = session.IsSeen(session.Map.Cell(trap.Position));
            }
        }

        // Places a renderer over the telegraph's area; `grow` (0–1) scales it out from the caster / center.
        static void Shape(SoulTelegraph t, SpriteRenderer renderer, float grow)
        {
            Vector2 d = t.Direction;
            float angle = Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg; // map y points down
            var transform = renderer.transform;
            switch (t.Shape)
            {
                case SoulAreaShape.Circle:
                    transform.position = ToWorld(t.CircleCenter);
                    transform.rotation = Quaternion.identity;
                    transform.localScale = Vector3.one * t.Size * 2 * grow;
                    break;
                case SoulAreaShape.Cone:
                    transform.position = ToWorld(t.Origin);
                    transform.rotation = Quaternion.Euler(0, 0, angle);
                    transform.localScale = Vector3.one * t.Size * grow;
                    break;
                default:
                    float length = t.Size * grow;
                    Vector2 center = t.Anchor == SoulAreaAnchor.Self ? t.Origin : t.Origin + d * length / 2;
                    transform.position = ToWorld(center);
                    transform.rotation = Quaternion.Euler(0, 0, angle);
                    transform.localScale = new Vector3(Mathf.Max(.01f, length), t.Width, 1);
                    break;
            }
        }

        // Ranged patterns (bows, magic) show a projectile from the attacker to the target.
        // The game's sounds (AudioManager keeps each within its budget, so a big fight does not roar).
        static void Sound(SfxKey key, float volume = 1f) => AudioManager.Instance?.Play(key, volume);

        // A swing sounds like its weapon: bow, spell, shove (knockback), or blade.
        static SfxKey AttackSound(SoulCombatEvent evt)
        {
            var pattern = evt.Pattern;
            if (pattern != null && pattern.WeaponTag == "bow") return SfxKey.ATK_Archer;
            if (evt.Magic || (pattern != null && pattern.DamageSchool == SoulDamageSchool.Magic)) return SfxKey.ATK_Mage;
            if (pattern != null && pattern.Knockback > 0) return SfxKey.ATK_Shield;
            return SfxKey.ATK_Knight;
        }

        // Heals (a target, or a potion) sound like healing, blessings like a war cry; spells crackle, blows strike.
        static SfxKey SkillSound(SoulCombatEvent evt)
        {
            if (!evt.Support) return evt.Magic ? SfxKey.SKILL_ChainLightning : SfxKey.SKILL_HeavyStrike;
            return evt.Target != null || (evt.Pattern == null && !evt.Magic) ? SfxKey.SKILL_TargetHeal : SfxKey.SKILL_BattleCry;
        }

        void Projectile(SoulPatternData pattern, SoulUnitView from, SoulUnitView to, bool spell = false)
        {
            if ((pattern == null && !spell) || from == null || to == null) return;
            bool magic = spell || pattern.CastsSkills || pattern.DamageSchool == SoulDamageSchool.Magic;
            if (!magic && pattern.Range < 2.5f) return;
            SoulProjectileFx.Spawn(effects, magic ? MagicBolt : Arrow, from.BodyPosition, to.BodyPosition);
        }

        // What happened on the floor (every party's feed: a monster's blows are in the feed of the party moving it).
        void PlayEvents(SoulDungeonSession session)
        {
            foreach (var party in session.FloorParties) PlayFeed(party);
        }

        void PlayFeed(SoulDungeonSession session)
        {
            foreach (var evt in session.CombatEvents)
            {
                SoulUnitView actor = null;
                var actorKey = evt.Actor ?? evt.Target;
                if (actorKey != null) units.TryGetValue(actorKey, out actor);
                SoulUnitView target = null;
                if (evt.Target != null) units.TryGetValue(evt.Target, out target);
                switch (evt.Kind)
                {
                    case SoulEventKind.Attack:
                        Sound(AttackSound(evt), evt.Actor is SoulMercenary ? 1f : .6f);
                        actor?.PlayAttack(evt.Pattern, evt.Target, evt.Magic);
                        Projectile(evt.Pattern, actor, target, evt.Magic);
                        break;
                    case SoulEventKind.Skill:
                        Sound(SkillSound(evt), evt.Support && evt.Pattern == null && !evt.Magic ? .6f : 1f); // a potion: softer
                        if (evt.Support) actor?.PlaySupport(evt.Target);
                        else
                        {
                            actor?.PlayAttack(evt.Pattern, evt.Target, evt.Magic);
                            if (!evt.Magic) Projectile(evt.Pattern, actor, target); // a spell's bolt flies when its circle lands
                        }
                        if (actor != null) SoulFloatingText.Spawn(effects, actor.HeadPosition + Vector3.up * .3f, evt.Label, SoulUi.Accent, 3.2f);
                        break;
                    case SoulEventKind.Hit:
                        if (target == null) break;
                        target.PlayHit(actor != null && actor != target ? actor.transform.position : (Vector3?)null);
                        // A hit that takes a real bite out of the bar is felt, not just counted.
                        float bite = evt.Amount / Mathf.Max(1, evt.Target.Stats.Total(StatType.MaxHp));
                        if (bite > .08f) CameraShaker.Impulse(Mathf.Min(.5f, bite * 2.5f), target.transform.position);
                        if (evt.Critical) SoulFloatingText.Spawn(effects, target.HeadPosition, Mathf.CeilToInt(evt.Amount) + "!", new Color(1f, .82f, .2f), 5.5f); // 치명타: bigger, gold
                        else SoulFloatingText.Spawn(effects, target.HeadPosition, Mathf.CeilToInt(evt.Amount).ToString(), evt.Target is SoulMercenary ? SoulUi.Bad : Color.white);
                        break;
                    case SoulEventKind.Roll:
                        if (target == null) break;
                        target.PlayRoll();
                        SoulFloatingText.Spawn(effects, target.HeadPosition, "구르기", SoulUi.SubText, 3f);
                        break;
                    case SoulEventKind.Flank:
                        if (target != null) SoulFloatingText.Spawn(effects, target.HeadPosition + Vector3.up * .45f, "측면!", SoulUi.Accent, 3f);
                        break;
                    case SoulEventKind.Miss:
                        if (target != null) SoulFloatingText.Spawn(effects, target.HeadPosition, "빗나감", SoulUi.SubText, 3f);
                        break;
                    case SoulEventKind.Evade:
                        if (target != null) SoulFloatingText.Spawn(effects, target.HeadPosition, "회피", SoulUi.ManaColor, 3f);
                        break;
                    case SoulEventKind.Guard:
                        if (target != null) SoulFloatingText.Spawn(effects, target.HeadPosition + Vector3.up * .25f, "막기", SoulUi.ManaColor, 3f);
                        break;
                    case SoulEventKind.Status:
                        if (target != null) SoulFloatingText.Spawn(effects, target.HeadPosition + Vector3.up * .45f, SoulCombat.StatusName(evt.Status), SoulUi.Accent, 3f);
                        break;
                    case SoulEventKind.Cover:
                        if (actor == null) break;
                        actor.Flash(new Color(.5f, .7f, 1f));
                        SoulFloatingText.Spawn(effects, actor.HeadPosition + Vector3.up * .3f, "가림", SoulUi.ManaColor, 3.2f);
                        break;
                    case SoulEventKind.Counter:
                        if (actor != null) SoulFloatingText.Spawn(effects, actor.HeadPosition + Vector3.up * .3f, "받아치기", SoulUi.Accent, 3.2f);
                        if (target != null) { target.PlayHit(); SoulFloatingText.Spawn(effects, target.HeadPosition, Mathf.CeilToInt(evt.Amount).ToString(), Color.white); }
                        break;
                    case SoulEventKind.SkillFx:
                        SoulSkillFx.Play(effects, evt, actor, target);
                        break;
                    case SoulEventKind.Shockwave:
                        Sound(SfxKey.SKILL_Shockwave); // the picture comes with the skill's SkillFx
                        break;
                    case SoulEventKind.Telegraph:
                        // Named boss patterns announce themselves; an ordinary swing is told by its area alone.
                        if (actor != null && !string.IsNullOrEmpty(evt.Label))
                            SoulFloatingText.Spawn(effects, actor.HeadPosition + Vector3.up * .4f, evt.Label, SoulUi.Bad, 3.8f);
                        break;
                    case SoulEventKind.Impact:
                        Sound(SfxKey.SKILL_BossSlam);
                        actor?.PlayAttack(null, null);
                        if (actor != null) CameraShaker.Impulse(.4f, actor.transform.position);
                        break;
                    case SoulEventKind.Interact:
                        if (actor != null) SoulFloatingText.Spawn(effects, actor.HeadPosition + Vector3.up * .4f, evt.Label, SoulUi.Accent, 3.4f);
                        Hud?.Toast(evt.Label);
                        break;
                    case SoulEventKind.Trap:
                        if (target != null) SoulFloatingText.Spawn(effects, target.HeadPosition + Vector3.up * .3f, "함정!", SoulUi.Bad, 3.6f);
                        break;
                    case SoulEventKind.TrapFound:
                        if (actor != null) SoulFloatingText.Spawn(effects, actor.HeadPosition + Vector3.up * .3f, "함정 해제", SoulUi.Good, 3.2f);
                        break;
                    case SoulEventKind.Chest:
                        if (target != null) SoulFloatingText.Spawn(effects, target.HeadPosition + Vector3.up * .3f, "보물 상자!", SoulUi.Accent, 3.6f);
                        break;
                    case SoulEventKind.Loot:
                        if (actor != null) SoulFloatingText.Spawn(effects, actor.HeadPosition + Vector3.up * .3f, evt.Label, SoulUi.Accent, 3.4f);
                        break;
                    case SoulEventKind.Leap:
                        Sound(SfxKey.SKILL_LeapStrike);
                        if (actor != null) SoulFloatingText.Spawn(effects, actor.HeadPosition + Vector3.up * .3f, "도약", SoulUi.Accent, 3f);
                        break;
                    case SoulEventKind.Death:
                        target?.PlayDeath();
                        break;
                    case SoulEventKind.Revive:
                        Sound(SfxKey.SKILL_HealAura);
                        target?.PlayRevive();
                        if (target != null) SoulFloatingText.Spawn(effects, target.HeadPosition + Vector3.up * .3f, "소생", SoulUi.Good, 3.4f);
                        break;
                    case SoulEventKind.SoulDrop:
                    {
                        // A soul is rare enough that it gets the whole screen for a moment: three rings pulsing
                        // out of the body, a shake, and the name of what was just torn loose.
                        var where = target != null ? target.HeadPosition : transform.position;
                        for (int ring = 1; ring <= 3; ring++) SoulRingFx.Spawn(effects, where, ring * 1.1f);
                        CameraShaker.Impulse(.55f + .1f * evt.Amount, where);
                        SoulFloatingText.Spawn(effects, where + Vector3.up * .5f, evt.Label, SoulUi.Soul, 6f);
                        Hud?.Toast(SoulUi.Colored(evt.Label + " 획득!", SoulUi.Soul));
                        break;
                    }
                    case SoulEventKind.Absorb:
                        if (actor == null) break;
                        actor.ApplyLook();
                        actor.Flash(new Color(.75f, .45f, 1f));
                        SoulFloatingText.Spawn(effects, actor.HeadPosition + Vector3.up * .3f, "영혼 흡수", SoulUi.Soul, 3.4f);
                        break;
                    case SoulEventKind.LevelUp:
                    {
                        if (actor == null) break;
                        bool milestone = evt.Label == SoulDungeonSession.MilestoneLabel, floorBonus = evt.Label == SoulDungeonSession.FloorBonusLabel;
                        Sound(SfxKey.SKILL_WarBanner, milestone ? 1f : .8f);
                        actor.ApplyLook();
                        actor.Flash(new Color(1f, .9f, .4f));
                        var at = actor.transform.position;
                        SoulRingFx.Spawn(effects, at, milestone ? 3.2f : 1.6f);
                        if (milestone)
                        {
                            SoulRingFx.Spawn(effects, at, 2f);
                            SoulRingFx.Spawn(effects, at, 4.4f);
                            CameraShaker.Impulse(.35f, at);
                            Sound(SfxKey.SKILL_BossEnrage, .6f);
                        }
                        int level = evt.Actor is SoulMercenary grown ? grown.Level : 0;
                        string text = floorBonus ? "모든 능력치 +1" : milestone ? $"LV {level}!" : "LEVEL UP";
                        SoulFloatingText.Spawn(effects, actor.HeadPosition + Vector3.up * .3f, text, SoulUi.Accent, milestone ? 6f : 4.4f);
                        if (milestone && Hud != null && evt.Actor is SoulMercenary hero) Hud.Toast($"{hero.Name} Lv.{hero.Level} — 새 패턴 · 영혼 슬롯 +1");
                        break;
                    }
                }
            }
            session.CombatEvents.Clear();
        }
    }
}
