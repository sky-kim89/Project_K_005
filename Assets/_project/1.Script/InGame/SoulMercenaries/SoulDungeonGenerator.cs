using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SoulMercenaries
{
    //  Single — one weak monster near the start, or a lone elite further out
    //  Pack   — 3–8 monsters, melee front line + ranged back line, waiting in formation at a way in
    //  Horde  — an elite leading packs, 10+ monsters, in a large hall
    //  Boss   — the boss alone in its arena
    public enum SoulEncounterKind { Single, Elite, Pack, Horde, Boss }

    //  Spring      — heals the party fully (once)
    //  Campfire    — a short camp: the party recovers everything (again after a cooldown; later: consumables)
    //  Blessing    — the dungeon's blessing: attack and armor up for a while (once)
    //  Altar       — experience ×1.5 for a while (once)
    //  Warp        — sends the party into a horde, to a hidden treasure, or to the boss (once)
    //  Watchtower  — reveals the map around it
    //  Suspicious  — a chest that is either treasure or a mimic calling monsters
    //  Fortune     — pay gold for a random blessing, or a curse
    public enum SoulObjectKind { Spring, Campfire, Blessing, Altar, Warp, Watchtower, Suspicious, Fortune, Escape }
    public enum SoulWarpKind { Horde, Treasure, Boss }
    //  Vault — the treasure right away · Traps — traps and monsters guard it · Boss — a boss waits inside
    public enum SoulHiddenStageKind { Vault, Traps, Boss }
    public enum SoulTrapKind { Spikes, Poison, Warp }

    public struct SoulChestSpawn
    {
        public Vector2Int Cell;
        public bool Hidden;
        public Vector2Int Door; // the secret door in front of it (-1, -1: none)
        public int Items;
    }

    public struct SoulObjectSpawn
    {
        public SoulObjectKind Kind;
        public Vector2Int Cell;
        public SoulWarpKind WarpKind;
        public Vector2Int WarpTarget, WarpDoor; // WarpDoor: a secret door opened on arrival (-1, -1: none)
        public bool Mimic;
    }

    public struct SoulHiddenStageSpawn
    {
        public SoulHiddenStageKind Kind;
        public RectInt Rect;
        public Vector2Int Door;
    }

    public struct SoulEncounter
    {
        public SoulEncounterKind Kind;
        public string Name;
        public Vector2Int Center;
        public int Count, Group;
        public bool Formation;
    }

    public sealed class SoulLayout
    {
        public string Rows;
        public Vector2Int Start;
        public Vector2Int[] Exits;
        public readonly List<SoulMonsterSpawn> Spawns = new List<SoulMonsterSpawn>();
        public readonly List<SoulEncounter> Encounters = new List<SoulEncounter>();
        public readonly List<RectInt> Rooms = new List<RectInt>();
        public readonly List<RectInt> Halls = new List<RectInt>();
        public RectInt BossArena;
        public readonly List<SoulChestSpawn> Chests = new List<SoulChestSpawn>();
        public readonly List<(Vector2Int cell, SoulTrapKind kind)> Traps = new List<(Vector2Int, SoulTrapKind)>();
        public readonly Dictionary<Vector2Int, Vector2Int> TrapTargets = new Dictionary<Vector2Int, Vector2Int>(); // Warp traps: where they send
        public readonly List<SoulHiddenStageSpawn> HiddenStages = new List<SoulHiddenStageSpawn>();
        public readonly List<SoulObjectSpawn> Objects = new List<SoulObjectSpawn>();
    }

    // Rooms, halls and 2-wide corridors. The party starts in the room nearest the map center; one portal sits at
    // each of the north/east/south/west edges; the boss arena is the hall farthest from the start and every
    // portal; the other halls hold hordes. Monsters get harder with the distance from the start.
    public static class SoulDungeonGenerator
    {
        public static SoulLayout FromData(SoulDungeonData data)
        {
            var layout = new SoulLayout
            {
                Rows = data.MapRows, Start = data.PartyStart,
                Exits = data.Exits != null && data.Exits.Length > 0 ? data.Exits : new[] { data.Exit }
            };
            if (data.Monsters != null) layout.Spawns.AddRange(data.Monsters);
            return layout;
        }

        public static SoulLayout Generate(SoulDungeonData data, int seed, int floor = 1)
        {
            var roster = data.Roster(floor);
            var random = new System.Random(seed);
            int width = data.Width, height = data.Height;
            var grid = new bool[width, height];
            var used = new bool[width, height];
            var layout = new SoulLayout();
            var middle = new Vector2(width / 2f, height / 2f);

            bool Place(RectInt r, int margin)
            {
                if (r.xMin < 2 || r.yMin < 2 || r.xMax > width - 2 || r.yMax > height - 2) return false;
                for (int y = Mathf.Max(0, r.yMin - margin); y < Mathf.Min(height, r.yMax + margin); y++)
                    for (int x = Mathf.Max(0, r.xMin - margin); x < Mathf.Min(width, r.xMax + margin); x++)
                        if (used[x, y]) return false;
                for (int y = r.yMin; y < r.yMax; y++) for (int x = r.xMin; x < r.xMax; x++) used[x, y] = true;
                return true;
            }

            // Halls first (they need the room), away from the quiet middle.
            var rooms = new List<(RectInt rect, bool hall)>();
            int halls = data.Halls + 1;
            float calm = Mathf.Min(width, height) * .3f;
            for (int attempt = 0; attempt < 4000 && rooms.Count < halls; attempt++)
            {
                int w = random.Next(24, 35), h = random.Next(16, 23);
                if (w >= width - 6 || h >= height - 6) break;
                var r = new RectInt(random.Next(3, width - w - 3), random.Next(3, height - h - 3), w, h);
                if (Vector2.Distance(r.center, middle) < calm) continue;
                if (Place(r, 3)) rooms.Add((r, true));
            }
            int target = Mathf.Max(16, width * height / 280);
            for (int attempt = 0; attempt < target * 30 && rooms.Count < target; attempt++)
            {
                int w = random.Next(7, 13), h = random.Next(6, 10);
                var r = new RectInt(random.Next(2, Mathf.Max(3, width - w - 2)), random.Next(2, Mathf.Max(3, height - h - 2)), w, h);
                if (Place(r, 2)) rooms.Add((r, false));
            }
            rooms.Sort((a, b) => (a.rect.center.x + a.rect.center.y * .35f).CompareTo(b.rect.center.x + b.rect.center.y * .35f));
            foreach (var room in rooms) Carve(grid, room.rect);
            // Every room joins the nearest room before it (a tree: everything connects); every third room also
            // its second nearest (loops, more than one way around).
            for (int i = 1; i < rooms.Count; i++)
            {
                int nearest = -1, second = -1;
                float best = float.MaxValue, next = float.MaxValue;
                for (int j = 0; j < i; j++)
                {
                    float d = Vector2Int.Distance(Center(rooms[i].rect), Center(rooms[j].rect));
                    if (d < best) { next = best; second = nearest; best = d; nearest = j; }
                    else if (d < next) { next = d; second = j; }
                }
                Corridor(grid, Center(rooms[i].rect), Center(rooms[nearest].rect), random.Next(2) == 0);
                if (i % 3 == 0 && second >= 0) Corridor(grid, Center(rooms[i].rect), Center(rooms[second].rect), random.Next(2) == 0);
            }
            // Cover: pillars in rooms; in halls only around the edge so the middle stays a wide battlefield.
            foreach (var (room, hall) in rooms)
            {
                if (room.width * room.height < 60) continue;
                var c = Center(room);
                int pillars = hall ? random.Next(3, 7) : random.Next(1, 3);
                for (int p = 0; p < pillars; p++)
                {
                    int x = random.Next(room.xMin + 1, room.xMax - 2), y = random.Next(room.yMin + 1, room.yMax - 2);
                    bool crossesCenter = (x <= c.x && c.x <= x + 1) || (y <= c.y && c.y <= y + 1);
                    if (crossesCenter || (hall && Mathf.Abs(x - c.x) < 8 && Mathf.Abs(y - c.y) < 5)) continue;
                    for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) grid[x + dx, y + dy] = false;
                }
            }

            int startRoom = -1;
            for (int i = 0; i < rooms.Count; i++)
                if (!rooms[i].hall && (startRoom < 0 || Vector2.Distance(Center(rooms[i].rect), middle) < Vector2.Distance(Center(rooms[startRoom].rect), middle))) startRoom = i;
            layout.Start = Center(rooms[startRoom].rect);
            if (floor == 1) FourWays(grid, rooms[startRoom].rect);

            var exits = new Vector2Int[4];
            var taken = new HashSet<int> { startRoom };
            for (int d = 0; d < 4; d++) // north, east, south, west
            {
                int best = -1;
                float bestEdge = float.MaxValue;
                for (int i = 0; i < rooms.Count; i++)
                {
                    if (taken.Contains(i) || rooms[i].hall) continue;
                    var r = rooms[i].rect;
                    float edge = d == 0 ? r.yMin : d == 1 ? width - r.xMax : d == 2 ? height - r.yMax : r.xMin;
                    if (edge < bestEdge) { bestEdge = edge; best = i; }
                }
                taken.Add(best);
                var c = Center(rooms[best].rect);
                var portal = d == 0 ? new Vector2Int(c.x, 1) : d == 1 ? new Vector2Int(width - 2, c.y) : d == 2 ? new Vector2Int(c.x, height - 2) : new Vector2Int(1, c.y);
                Line(grid, c, portal);
                exits[d] = portal;
            }
            layout.Exits = exits;

            int arena = -1;
            float farthest = -1;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (!rooms[i].hall) continue;
                var c = Center(rooms[i].rect);
                float nearest = Vector2Int.Distance(c, layout.Start);
                foreach (var portal in exits) nearest = Mathf.Min(nearest, Vector2Int.Distance(c, portal));
                if (nearest > farthest) { farthest = nearest; arena = i; }
            }

            var doors = new HashSet<Vector2Int>();
            var stageRooms = HiddenStages(data, layout, grid, doors, rooms, startRoom, arena, random);
            HiddenAlcoves(data, layout, grid, doors, rooms, startRoom, arena, random);

            var rows = new StringBuilder();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) rows.Append(grid[x, y] ? '.' : doors.Contains(new Vector2Int(x, y)) ? '+' : '#');
                if (y < height - 1) rows.Append('\n');
            }
            layout.Rows = rows.ToString();
            foreach (var (room, hall) in rooms) (hall ? layout.Halls : layout.Rooms).Add(room);
            if (arena >= 0) layout.BossArena = rooms[arena].rect;

            Populate(data, roster, layout, grid, rooms, startRoom, arena, random, floor);
            StageContents(data, roster, layout, grid, stageRooms, random);
            Objects(data, layout, grid, rooms, startRoom, arena, random);
            WarpTraps(layout, grid, rooms, startRoom, arena, random, floor);
            return layout;
        }

        // From WarpTrapFloor on: hidden plates in the rooms that throw the party into an elite, a horde or the boss's
        // arena — two on the first such floor, one more every second floor after (WarpTrapMax at most).
        public const int WarpTrapFloor = 3, WarpTrapMax = 4;
        static void WarpTraps(SoulLayout layout, bool[,] grid, List<(RectInt rect, bool hall)> rooms, int startRoom, int arena, System.Random random, int floor)
        {
            if (floor < WarpTrapFloor) return;
            var targets = new List<Vector2Int>();
            foreach (var encounter in layout.Encounters)
                if ((encounter.Kind == SoulEncounterKind.Elite || encounter.Kind == SoulEncounterKind.Horde || encounter.Kind == SoulEncounterKind.Boss)
                    && (encounter.Name == null || !encounter.Name.EndsWith("(히든)"))) targets.Add(encounter.Center);
            if (targets.Count == 0) return;
            int wanted = Mathf.Min(WarpTrapMax, 2 + (floor - WarpTrapFloor) / 2), placed = 0;
            for (int attempt = 0; attempt < 400 && placed < wanted; attempt++)
            {
                int index = random.Next(rooms.Count);
                if (index == startRoom || index == arena || rooms[index].hall) continue;
                var cell = Inside(rooms[index].rect, random);
                if (!grid[cell.x, cell.y] || Vector2Int.Distance(cell, layout.Start) < 15 || layout.TrapTargets.ContainsKey(cell)) continue;
                bool crowded = false;
                foreach (var thing in layout.Objects) crowded |= Vector2Int.Distance(thing.Cell, cell) < 4;
                if (crowded) continue;
                layout.Traps.Add((cell, SoulTrapKind.Warp));
                layout.TrapTargets[cell] = targets[random.Next(targets.Count)];
                placed++;
            }
        }

        static readonly Vector2Int[] Sides = { Vector2Int.down, Vector2Int.right, Vector2Int.up, Vector2Int.left }; // map y down: "down" = north

        // A pocket carved in solid rock next to a room: a secret door in the room's wall, `gap` tiles of corridor,
        // then `depth` × (2·half + 1) open tiles. Null when the rock there is not solid enough.
        static (Vector2Int door, Vector2Int outward, RectInt rect)? Pocket(bool[,] grid, HashSet<Vector2Int> doors, RectInt room, System.Random random, int depth, int half, int gap)
        {
            int width = grid.GetLength(0), height = grid.GetLength(1);
            var outward = Sides[random.Next(4)];
            var lateral = new Vector2Int(Mathf.Abs(outward.y), Mathf.Abs(outward.x));
            Vector2Int edge = outward == Vector2Int.down ? new Vector2Int(random.Next(room.xMin + 1, room.xMax - 1), room.yMin)
                : outward == Vector2Int.up ? new Vector2Int(random.Next(room.xMin + 1, room.xMax - 1), room.yMax - 1)
                : outward == Vector2Int.left ? new Vector2Int(room.xMin, random.Next(room.yMin + 1, room.yMax - 1))
                : new Vector2Int(room.xMax - 1, random.Next(room.yMin + 1, room.yMax - 1));
            if (!grid[edge.x, edge.y]) return null;
            var door = edge + outward;
            for (int i = 0; i <= gap + depth + 1; i++)
                for (int j = -(half + 1); j <= half + 1; j++)
                {
                    var cell = door + outward * i + lateral * j;
                    if (cell.x < 2 || cell.y < 2 || cell.x >= width - 2 || cell.y >= height - 2 || grid[cell.x, cell.y] || doors.Contains(cell)) return null;
                }
            for (int i = 1; i <= gap; i++) { var cell = door + outward * i; grid[cell.x, cell.y] = true; }
            int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;
            for (int i = gap + 1; i <= gap + depth; i++)
                for (int j = -half; j <= half; j++)
                {
                    var cell = door + outward * i + lateral * j;
                    grid[cell.x, cell.y] = true;
                    xMin = Mathf.Min(xMin, cell.x); yMin = Mathf.Min(yMin, cell.y); xMax = Mathf.Max(xMax, cell.x); yMax = Mathf.Max(yMax, cell.y);
                }
            doors.Add(door);
            return (door, outward, new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1));
        }

        static bool Usable(List<(RectInt rect, bool hall)> rooms, int index, int startRoom, int arena)
            => index != startRoom && index != arena && !rooms[index].hall && rooms[index].rect.width >= 5 && rooms[index].rect.height >= 5;

        // Treasure alcoves: a 3×3 pocket behind a secret door (found by a pathfinder, or by being knocked through it).
        static void HiddenAlcoves(SoulDungeonData data, SoulLayout layout, bool[,] grid, HashSet<Vector2Int> doors, List<(RectInt rect, bool hall)> rooms, int startRoom, int arena, System.Random random)
        {
            int placed = 0;
            for (int attempt = 0; attempt < data.HiddenChests * 60 && placed < data.HiddenChests; attempt++)
            {
                int index = random.Next(rooms.Count);
                if (!Usable(rooms, index, startRoom, arena)) continue;
                var pocket = Pocket(grid, doors, rooms[index].rect, random, 3, 1, 0);
                if (pocket == null) continue;
                layout.Chests.Add(new SoulChestSpawn { Cell = pocket.Value.door + pocket.Value.outward * 2, Hidden = true, Door = pocket.Value.door, Items = 1 });
                placed++;
            }
        }

        // Hidden stages: a room-sized pocket behind a secret wall, one corridor tile past the door.
        static List<(SoulHiddenStageSpawn stage, Vector2Int outward)> HiddenStages(SoulDungeonData data, SoulLayout layout, bool[,] grid, HashSet<Vector2Int> doors, List<(RectInt rect, bool hall)> rooms, int startRoom, int arena, System.Random random)
        {
            var stages = new List<(SoulHiddenStageSpawn, Vector2Int)>();
            for (int attempt = 0; attempt < data.HiddenStages * 200 && stages.Count < data.HiddenStages; attempt++)
            {
                int index = random.Next(rooms.Count);
                if (!Usable(rooms, index, startRoom, arena)) continue;
                var pocket = Pocket(grid, doors, rooms[index].rect, random, random.Next(7, 10), random.Next(4, 6), 1);
                if (pocket == null) continue;
                double roll = random.NextDouble();
                var kind = roll < .35 ? SoulHiddenStageKind.Vault : roll < .75 ? SoulHiddenStageKind.Traps : SoulHiddenStageKind.Boss;
                var stage = new SoulHiddenStageSpawn { Kind = kind, Rect = pocket.Value.rect, Door = pocket.Value.door };
                layout.HiddenStages.Add(stage);
                stages.Add((stage, pocket.Value.outward));
            }
            return stages;
        }

        // What waits inside: the treasure at the far end; traps and a pack, or a boss, between it and the door.
        static void StageContents(SoulDungeonData data, SoulFloorRoster roster, SoulLayout layout, bool[,] grid, List<(SoulHiddenStageSpawn stage, Vector2Int outward)> stages, System.Random random)
        {
            int group = 1000;
            foreach (var (stage, outward) in stages)
            {
                var rect = stage.Rect;
                var center = Center(rect);
                int reach = (outward.x != 0 ? rect.width : rect.height) / 2;
                var far = center + outward * Mathf.Max(0, reach - 1);
                layout.Chests.Add(new SoulChestSpawn { Cell = stage.Kind == SoulHiddenStageKind.Vault ? center : far, Hidden = true, Door = stage.Door, Items = 2 });
                Vector2 facing = -(Vector2)outward;
                if (stage.Kind == SoulHiddenStageKind.Boss && roster.Boss != null)
                {
                    Add(layout, roster.Boss, center, 0, facing, rect);
                    layout.Encounters.Add(new SoulEncounter { Kind = SoulEncounterKind.Boss, Name = roster.Boss.Name + " (히든)", Center = center, Count = 1 });
                    continue;
                }
                if (stage.Kind != SoulHiddenStageKind.Traps) continue;
                for (int n = 4 + random.Next(3); n > 0; n--)
                {
                    var cell = new Vector2Int(random.Next(rect.xMin, rect.xMax), random.Next(rect.yMin, rect.yMax));
                    if (cell == far || !grid[cell.x, cell.y]) continue;
                    layout.Traps.Add((cell, random.Next(2) == 0 ? SoulTrapKind.Spikes : SoulTrapKind.Poison));
                }
                if (!PickPack(data, roster, .7f, random, out var pack)) continue;
                Split(pack, .7f, random, out var front, out var back);
                int id = ++group;
                foreach (var monster in Concat(front, back)) Add(layout, monster, new Vector2Int(random.Next(rect.xMin, rect.xMax), random.Next(rect.yMin, rect.yMax)), id, facing, rect);
                if (roster.Elites.Length > 0) Add(layout, roster.Elites[random.Next(roster.Elites.Length)], center, id, facing, rect);
                layout.Encounters.Add(new SoulEncounter { Kind = SoulEncounterKind.Pack, Name = pack.Name + " (히든)", Center = center, Count = front.Count + back.Count + 1, Group = id });
            }
        }

        // Usable objects, spread over the rooms (one per room, 20+ tiles apart, none in the start room).
        static void Objects(SoulDungeonData data, SoulLayout layout, bool[,] grid, List<(RectInt rect, bool hall)> rooms, int startRoom, int arena, System.Random random)
        {
            var wanted = new List<SoulObjectKind>();
            void Want(SoulObjectKind kind, int count) { for (int i = 0; i < count; i++) wanted.Add(kind); }
            Want(SoulObjectKind.Spring, data.Springs); Want(SoulObjectKind.Campfire, data.Campfires); Want(SoulObjectKind.Blessing, data.Blessings);
            Want(SoulObjectKind.Altar, data.Altars); Want(SoulObjectKind.Warp, data.Warps); Want(SoulObjectKind.Watchtower, data.Watchtowers);
            Want(SoulObjectKind.Suspicious, data.SuspiciousChests);
            // the fortune statue is retired: its places go to the other objects (a spring, a blessing, an altar, a
            // watchtower, a suspicious chest), at random
            var stead = new[] { SoulObjectKind.Spring, SoulObjectKind.Blessing, SoulObjectKind.Altar, SoulObjectKind.Watchtower, SoulObjectKind.Suspicious };
            for (int i = 0; i < data.FortuneIdols; i++) wanted.Add(stead[random.Next(stead.Length)]);
            var hordes = new List<RectInt>();
            foreach (var hall in layout.Halls) if (!hall.Equals(layout.BossArena)) hordes.Add(hall);
            var treasures = layout.Chests.FindAll(chest => chest.Hidden && chest.Items == 1);
            var usedRooms = new HashSet<int>();
            foreach (var kind in wanted)
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    int index = random.Next(rooms.Count);
                    if (usedRooms.Contains(index) || !Usable(rooms, index, startRoom, arena)) continue;
                    var cell = Inside(rooms[index].rect, random);
                    if (!grid[cell.x, cell.y]) continue;
                    bool crowded = false;
                    foreach (var other in layout.Objects) crowded |= Vector2Int.Distance(other.Cell, cell) < 20;
                    if (crowded) continue;
                    var spawn = new SoulObjectSpawn { Kind = kind, Cell = cell, WarpDoor = new Vector2Int(-1, -1), Mimic = random.NextDouble() < .5 };
                    if (kind == SoulObjectKind.Warp)
                    {
                        double roll = random.NextDouble();
                        if (roll < .4 && hordes.Count > 0) { spawn.WarpKind = SoulWarpKind.Horde; spawn.WarpTarget = Center(hordes[random.Next(hordes.Count)]); }
                        else if (roll < .75 && treasures.Count > 0)
                        {
                            var chest = treasures[random.Next(treasures.Count)];
                            spawn.WarpKind = SoulWarpKind.Treasure; spawn.WarpTarget = chest.Cell; spawn.WarpDoor = chest.Door;
                        }
                        else { spawn.WarpKind = SoulWarpKind.Boss; spawn.WarpTarget = Center(layout.BossArena) + new Vector2Int(-6, 0); }
                    }
                    layout.Objects.Add(spawn);
                    usedRooms.Add(index);
                    break;
                }
        }

        // ── encounters ───────────────────────────────────────────

        // How sharply the danger climbs from the start to the rim: in full on floor 1 (many lone monsters around the
        // start, crowds and elites at the edges and by the portals), a little softer each floor below, 40% at most.
        public static float Gradient(int floor) => floor <= 1 ? 1f : Mathf.Max(.4f, 1f - .15f * (floor - 1));

        static void Populate(SoulDungeonData data, SoulFloorRoster roster, SoulLayout layout, bool[,] grid, List<(RectInt rect, bool hall)> rooms, int startRoom, int arena, System.Random random, int floor)
        {
            float maxDepth = 1;
            foreach (var room in rooms) maxDepth = Mathf.Max(maxDepth, Vector2Int.Distance(Center(room.rect), layout.Start));
            float gradient = Gradient(floor);
            int group = 0;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (i == startRoom) continue;
                var (room, hall) = rooms[i];
                float depth = Vector2Int.Distance(Center(room), layout.Start) / maxDepth;
                // Out at the rim or by a portal (0 up to halfway out, 1 at the far edge or right at a portal).
                float nearPortal = 0;
                foreach (var portal in layout.Exits) nearPortal = Mathf.Max(nearPortal, 1 - Vector2Int.Distance(Center(room), portal) / (maxDepth * .4f));
                float outer = Mathf.Clamp01(Mathf.Max((depth - .5f) / .5f, nearPortal)) * gradient;
                var door = Door(grid, room, layout.Start);
                // Waiting monsters look toward the way the party most likely comes in.
                Vector2 facing = door.HasValue ? -(Vector2)door.Value.inward : RandomFacing(random);
                if (i == arena)
                {
                    if (roster.Boss == null) continue;
                    Add(layout, roster.Boss, Center(room), 0, facing, room);
                    layout.Encounters.Add(new SoulEncounter { Kind = SoulEncounterKind.Boss, Name = roster.Boss.Name, Center = Center(room), Count = 1 });
                    continue;
                }
                if (hall) { Horde(data, roster, layout, room, facing, depth, ++group, random); continue; }
                if (depth < .2f + .05f * gradient)
                {
                    // Around the start every room holds lone monsters (each fights on its own): two or three, one
                    // more on floor 1, and a little further out too.
                    if (roster.Singles.Length == 0) continue;
                    for (int n = 2 + random.Next(2) + Mathf.RoundToInt(gradient); n > 0; n--)
                    {
                        var single = roster.Singles[random.Next(roster.Singles.Length)];
                        var cell = Inside(room, random);
                        Add(layout, single, cell, 0, RandomFacing(random), room);
                        layout.Encounters.Add(new SoulEncounter { Kind = SoulEncounterKind.Single, Name = single.Name, Center = cell, Count = 1 });
                    }
                    continue;
                }
                // The rim is never empty for long, and its packs come from further down the depth table.
                if (random.NextDouble() < data.EmptyRooms * (1 - .8f * outer)) continue;
                float threat = Mathf.Max(depth, Mathf.Lerp(depth, 1, outer));
                if (depth >= .3f && roster.Elites.Length > 0 && random.NextDouble() < .1 + .25 * outer)
                {
                    var elite = roster.Elites[random.Next(roster.Elites.Length)];
                    Add(layout, elite, Center(room), 0, facing, room);
                    layout.Encounters.Add(new SoulEncounter { Kind = SoulEncounterKind.Elite, Name = elite.Name, Center = Center(room), Count = 1 });
                    continue;
                }
                if (!PickPack(data, roster, threat, random, out var pack)) continue;
                Split(pack, threat, random, out var front, out var back);
                string name = pack.Name;
                // Out at the rim a second pack often shares the room, and an elite may lead them.
                if (random.NextDouble() < .5 * outer && PickPack(data, roster, threat, random, out var more))
                {
                    Split(more, threat, random, out var f, out var b);
                    front.AddRange(f); back.AddRange(b);
                    if (more.Name != name) name += ", " + more.Name;
                }
                if (roster.Elites.Length > 0 && random.NextDouble() < .35 * outer)
                {
                    var leader = roster.Elites[random.Next(roster.Elites.Length)];
                    front.Insert(0, leader);
                    name = leader.Name + " + " + name;
                }
                int id = ++group;
                bool formation = door.HasValue && random.NextDouble() < .7;
                if (formation) Formation(layout, room, door.Value.cell, door.Value.inward, front, back, id);
                else foreach (var monster in Concat(front, back)) Add(layout, monster, Inside(room, random), id, facing, room);
                layout.Encounters.Add(new SoulEncounter { Kind = SoulEncounterKind.Pack, Name = name, Center = Center(room), Count = front.Count + back.Count, Group = id, Formation = formation });
            }
            Stragglers(data, roster, layout, grid, rooms, maxDepth, random, gradient);
        }

        public const int StragglerLimit = 40, StragglerSpacing = 7;

        // Lone monsters wandering the corridors around the start (up to 30% of the way out), at least 6 tiles from
        // the start and 7 from each other, denser the closer: the first fights come within a few steps. With the
        // floor's full gradient (floor 1) they reach 35% of the way out, 5 tiles apart, up to 60 of them.
        static void Stragglers(SoulDungeonData data, SoulFloorRoster roster, SoulLayout layout, bool[,] grid, List<(RectInt rect, bool hall)> rooms, float maxDepth, System.Random random, float gradient)
        {
            if (roster.Singles.Length == 0) return;
            int width = grid.GetLength(0), height = grid.GetLength(1);
            var inRoom = new bool[width, height];
            foreach (var (room, _) in rooms) for (int y = room.yMin; y < room.yMax; y++) for (int x = room.xMin; x < room.xMax; x++) inRoom[x, y] = true;
            float reach = maxDepth * (.3f + .05f * gradient);
            float spacing = StragglerSpacing - 2 * gradient;
            int limit = StragglerLimit + Mathf.RoundToInt(20 * gradient);
            var candidates = new List<Vector2Int>();
            int x0 = Mathf.Max(1, Mathf.FloorToInt(layout.Start.x - reach)), x1 = Mathf.Min(width - 2, Mathf.CeilToInt(layout.Start.x + reach));
            int y0 = Mathf.Max(1, Mathf.FloorToInt(layout.Start.y - reach)), y1 = Mathf.Min(height - 2, Mathf.CeilToInt(layout.Start.y + reach));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!grid[x, y] || inRoom[x, y]) continue;
                    float d = Vector2Int.Distance(new Vector2Int(x, y), layout.Start);
                    if (d >= 6 && d <= reach) candidates.Add(new Vector2Int(x, y));
                }
            // Nearer cells first, loosely (distance × 0.6–1.4): dense around the start, thinning out further away.
            var keys = new Dictionary<Vector2Int, float>();
            foreach (var cell in candidates) keys[cell] = Vector2Int.Distance(cell, layout.Start) * (.6f + (float)random.NextDouble() * .8f);
            candidates.Sort((a, b) => keys[a].CompareTo(keys[b]));
            var placed = new List<Vector2Int>();
            foreach (var cell in candidates)
            {
                if (placed.Count >= limit) break;
                bool crowded = false;
                foreach (var other in placed) crowded |= Vector2Int.Distance(other, cell) < spacing;
                if (crowded) continue;
                placed.Add(cell);
                var single = roster.Singles[random.Next(roster.Singles.Length)];
                Add(layout, single, cell, 0, RandomFacing(random), new RectInt(cell.x, cell.y, 1, 1));
                layout.Encounters.Add(new SoulEncounter { Kind = SoulEncounterKind.Single, Name = single.Name, Center = cell, Count = 1 });
            }
        }

        // An elite in the middle of the hall, its packs' melee in rows ahead of it, their archers in rows behind.
        static void Horde(SoulDungeonData data, SoulFloorRoster roster, SoulLayout layout, RectInt hall, Vector2 facing, float depth, int id, System.Random random)
        {
            var front = new List<SoulMonsterData>();
            var back = new List<SoulMonsterData>();
            string names = "";
            int wanted = 10 + random.Next(7);
            for (int guard = 0; guard < 8 && front.Count + back.Count < wanted - 1; guard++)
            {
                if (!PickPack(data, roster, depth, random, out var pack)) break;
                Split(pack, depth, random, out var f, out var b);
                front.AddRange(f); back.AddRange(b);
                if (!names.Contains(pack.Name)) names += (names.Length > 0 ? ", " : "") + pack.Name;
            }
            var center = Center(hall);
            int count = front.Count + back.Count;
            if (roster.Elites.Length > 0)
            {
                var elite = roster.Elites[random.Next(roster.Elites.Length)];
                Add(layout, elite, center, id, facing, hall);
                names = elite.Name + " + " + names;
                count++;
            }
            var ahead = Snap(facing);
            Rows(layout, hall, center + ahead * 2, ahead, front, id, 7);
            Rows(layout, hall, center - ahead * 2, -ahead, back, id, 7, facing);
            layout.Encounters.Add(new SoulEncounter { Kind = SoulEncounterKind.Horde, Name = names, Center = center, Count = count, Group = id, Formation = true });
        }

        // Pack waiting at a way in: melee rows two tiles inside the door, archers two rows further back.
        static void Formation(SoulLayout layout, RectInt room, Vector2Int door, Vector2Int inward, List<SoulMonsterData> front, List<SoulMonsterData> back, int id)
        {
            Rows(layout, room, door + inward * 2, inward, front, id, 4, -(Vector2)inward);
            Rows(layout, room, door + inward * (front.Count > 4 ? 5 : 4), inward, back, id, 4, -(Vector2)inward);
        }

        // Lines of up to `perRow` monsters across `direction`, starting at `start` and stacking along it.
        static void Rows(SoulLayout layout, RectInt room, Vector2Int start, Vector2Int direction, List<SoulMonsterData> monsters, int id, int perRow, Vector2? facing = null)
        {
            var side = new Vector2Int(-direction.y, direction.x);
            for (int i = 0; i < monsters.Count; i++)
            {
                int row = i / perRow, slot = i % perRow;
                int offset = (slot + 1) / 2 * (slot % 2 == 0 ? 1 : -1); // 0, -1, 1, -2, 2 …
                var cell = start + direction * row + side * offset;
                Add(layout, monsters[i], cell, id, facing ?? (Vector2)direction, room);
            }
        }

        static void Add(SoulLayout layout, SoulMonsterData monster, Vector2Int cell, int group, Vector2 facing, RectInt room)
        {
            if (monster == null) return;
            cell.x = Mathf.Clamp(cell.x, room.xMin, room.xMax - 1);
            cell.y = Mathf.Clamp(cell.y, room.yMin, room.yMax - 1);
            layout.Spawns.Add(new SoulMonsterSpawn { Monster = monster, Cell = cell, Group = group, Facing = facing });
        }

        static bool PickPack(SoulDungeonData data, SoulFloorRoster roster, float depth, System.Random random, out SoulPackTemplate pack)
        {
            pack = default;
            var fitting = new List<SoulPackTemplate>();
            foreach (var p in roster.Packs) if (depth >= p.MinDepth && depth <= p.MaxDepth && (p.Front?.Length ?? 0) + (p.Back?.Length ?? 0) > 0) fitting.Add(p);
            if (fitting.Count == 0) foreach (var p in roster.Packs) if ((p.Front?.Length ?? 0) + (p.Back?.Length ?? 0) > 0) fitting.Add(p);
            if (fitting.Count == 0) return false;
            pack = fitting[random.Next(fitting.Count)];
            return true;
        }

        // Bigger the deeper inside the template's range; archers get BackShare of the places.
        static void Split(SoulPackTemplate pack, float depth, System.Random random, out List<SoulMonsterData> front, out List<SoulMonsterData> back)
        {
            front = new List<SoulMonsterData>(); back = new List<SoulMonsterData>();
            int min = Mathf.Max(1, pack.Min), max = Mathf.Max(min, pack.Max);
            float t = pack.MaxDepth > pack.MinDepth ? Mathf.InverseLerp(pack.MinDepth, pack.MaxDepth, depth) : .5f;
            int size = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(min, max, t)) + random.Next(-1, 2), min, max);
            bool hasFront = pack.Front != null && pack.Front.Length > 0, hasBack = pack.Back != null && pack.Back.Length > 0;
            int backCount = !hasBack ? 0 : !hasFront ? size : Mathf.Clamp(Mathf.RoundToInt(size * pack.BackShare), 1, size - 1);
            for (int i = 0; i < size - backCount; i++) front.Add(pack.Front[random.Next(pack.Front.Length)]);
            for (int i = 0; i < backCount; i++) back.Add(pack.Back[random.Next(pack.Back.Length)]);
        }

        static IEnumerable<SoulMonsterData> Concat(List<SoulMonsterData> a, List<SoulMonsterData> b) { foreach (var m in a) yield return m; foreach (var m in b) yield return m; }

        // The opening in the room's wall nearest the start: the outside corridor cell and the inward direction.
        static (Vector2Int cell, Vector2Int inward)? Door(bool[,] grid, RectInt room, Vector2Int start)
        {
            int width = grid.GetLength(0), height = grid.GetLength(1);
            (Vector2Int cell, Vector2Int inward)? best = null;
            float bestDistance = float.MaxValue;
            void Edge(Vector2Int from, Vector2Int step, int length, Vector2Int inward)
            {
                int run = -1;
                for (int i = 0; i <= length; i++)
                {
                    var cell = from + step * i;
                    bool open = i < length && cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height && grid[cell.x, cell.y];
                    if (open && run < 0) run = i;
                    if (!open && run >= 0)
                    {
                        var mid = from + step * ((run + i - 1) / 2);
                        float d = Vector2Int.Distance(mid, start);
                        if (d < bestDistance) { bestDistance = d; best = (mid, inward); }
                        run = -1;
                    }
                }
            }
            Edge(new Vector2Int(room.xMin, room.yMin - 1), Vector2Int.right, room.width, Vector2Int.up);
            Edge(new Vector2Int(room.xMin, room.yMax), Vector2Int.right, room.width, Vector2Int.down);
            Edge(new Vector2Int(room.xMin - 1, room.yMin), Vector2Int.up, room.height, Vector2Int.right);
            Edge(new Vector2Int(room.xMax, room.yMin), Vector2Int.up, room.height, Vector2Int.left);
            return best;
        }

        static Vector2Int Snap(Vector2 direction)
            => Mathf.Abs(direction.x) >= Mathf.Abs(direction.y) ? new Vector2Int(direction.x >= 0 ? 1 : -1, 0) : new Vector2Int(0, direction.y >= 0 ? 1 : -1);

        static Vector2 RandomFacing(System.Random random) => random.Next(2) == 0 ? Vector2.left : Vector2.right;

        static Vector2Int Inside(RectInt room, System.Random random)
            => new Vector2Int(random.Next(room.xMin + 1, Mathf.Max(room.xMin + 2, room.xMax - 1)), random.Next(room.yMin + 1, Mathf.Max(room.yMin + 2, room.yMax - 1)));

        // ── carving ──────────────────────────────────────────────

        public static Vector2Int Center(RectInt room) => new Vector2Int(room.xMin + room.width / 2, room.yMin + room.height / 2);

        static void Carve(bool[,] grid, RectInt room)
        {
            for (int y = room.yMin; y < room.yMax; y++) for (int x = room.xMin; x < room.xMax; x++) grid[x, y] = true;
        }

        // A way out of the room to the north, east, south and west: straight on from the center to the first open
        // ground at least 3 tiles past the wall (a corridor hugging the wall only leads sideways); when nothing lies
        // straight ahead, to the nearest such ground on that side.
        static void FourWays(bool[,] grid, RectInt room)
        {
            int width = grid.GetLength(0), height = grid.GetLength(1);
            var from = Center(room);
            int Out(Vector2Int c) => Mathf.Max(Mathf.Max(room.xMin - c.x, c.x - room.xMax + 1), Mathf.Max(room.yMin - c.y, c.y - room.yMax + 1));
            foreach (var side in Sides)
            {
                Vector2Int? target = null;
                for (var cell = from + side; cell.x >= 2 && cell.y >= 2 && cell.x < width - 2 && cell.y < height - 2; cell += side)
                    if (Out(cell) >= 3 && grid[cell.x, cell.y]) { target = cell; break; }
                if (target.HasValue) { Line(grid, from, target.Value); continue; }
                var lateral = new Vector2Int(Mathf.Abs(side.y), Mathf.Abs(side.x));
                float best = float.MaxValue;
                for (int y = 2; y < height - 2; y++)
                    for (int x = 2; x < width - 2; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        var offset = cell - from;
                        int ahead = offset.x * side.x + offset.y * side.y, across = Mathf.Abs(offset.x * lateral.x + offset.y * lateral.y);
                        if (!grid[x, y] || Out(cell) < 3 || ahead <= across) continue;
                        float d = offset.magnitude;
                        if (d < best) { best = d; target = cell; }
                    }
                if (target.HasValue) Corridor(grid, from, target.Value, side.x != 0);
            }
        }

        static void Corridor(bool[,] grid, Vector2Int from, Vector2Int to, bool horizontalFirst)
        {
            var corner = horizontalFirst ? new Vector2Int(to.x, from.y) : new Vector2Int(from.x, to.y);
            Line(grid, from, corner);
            Line(grid, corner, to);
        }

        static void Line(bool[,] grid, Vector2Int a, Vector2Int b)
        {
            int w = grid.GetLength(0), h = grid.GetLength(1);
            for (int x = Mathf.Min(a.x, b.x); x <= Mathf.Max(a.x, b.x); x++)
                for (int y = Mathf.Min(a.y, b.y); y <= Mathf.Max(a.y, b.y); y++)
                    for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
                        grid[Mathf.Clamp(x + dx, 1, w - 2), Mathf.Clamp(y + dy, 1, h - 2)] = true;
        }
    }
}
