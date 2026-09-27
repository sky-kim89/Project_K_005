using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // The village scene: a tiled grid with buildings on fixed plots. Clicking a building swings the doors shut and
    // open onto its popup (SoulDoorTransition). The top bar shows what the company owns and sends the party out
    // through the dungeon portal in the middle of the village (open 13:00–14:00, SoulClock); the parties away are
    // listed bottom left, each one can be watched.
    public sealed class SoulVillageView : MonoBehaviour
    {
        public SoulVillageData Data;
        const float Cell = 64, TopBar = 92;

        RectTransform map;
        TextMeshProUGUI goldText, partyText, eventText, clockText, portalLabel;
        Button departButton, newGameButton;
        RectTransform portal, tripPanel, tripRows;
        Image portalGate;
        string shownTrips;
        float newGameArmed; // a second press within this time starts over
        RectTransform uiRoot;
        readonly Dictionary<SoulBuildingKind, (Image picture, TextMeshProUGUI label)> buildings = new Dictionary<SoulBuildingKind, (Image, TextMeshProUGUI)>();
        readonly Dictionary<SoulBuildingKind, (GameObject root, TextMeshProUGUI text)> badges = new Dictionary<SoulBuildingKind, (GameObject, TextMeshProUGUI)>();
        int shownRevision = -1, shownPortraits = -1;

        SoulCampaign Campaign => SoulCampaign.Current;

        void Awake()
        {
            if (SoulCampaign.Current == null || SoulCampaign.Current.Data != Data)
                SoulCampaign.Current = SoulSave.Load(Data) ?? new SoulCampaign(Data, System.Environment.TickCount & int.MaxValue, SoulSave.Wallet);
            Campaign.Watching = null; // back in the village: nobody is watched, time runs at the village's pace
            SoulExpeditionRunner.Ensure();
            Build();
            foreach (var hero in Campaign.Roster) SoulPortraits.For(hero); // start drawing before any popup opens
        }

        void Start() => AudioManager.Instance?.PlayBgm(BgmKey.Lobby);

        void Build()
        {
            var canvas = SoulUi.OverlayCanvas("Village", 10, transform);
            var root = (RectTransform)canvas.transform;
            uiRoot = root;
            SoulUi.Stretch(SoulUi.Panel("Backdrop", root, new Color(.16f, .22f, .13f)).rectTransform);

            // ── top bar ──
            var bar = SoulUi.Panel("TopBar", root, SoulUi.HeaderBg).rectTransform;
            SoulUi.Place(bar, new Vector2(0, 1), Vector2.one, new Vector2(0, -TopBar), Vector2.zero);
            var title = SoulUi.Text(bar, "용병단의 마을", 34, SoulUi.Accent);
            title.fontStyle = FontStyles.Bold;
            SoulUi.Place(title.rectTransform, Vector2.zero, new Vector2(.3f, 1), new Vector2(28, 0), Vector2.zero);
            goldText = SoulUi.Text(bar, "", 24, Color.white);
            SoulUi.Place(goldText.rectTransform, new Vector2(.24f, 0), new Vector2(.62f, 1), Vector2.zero, Vector2.zero);
            partyText = SoulUi.Text(bar, "", 20, SoulUi.SubText);
            SoulUi.Place(partyText.rectTransform, new Vector2(.62f, 0), new Vector2(.705f, 1), Vector2.zero, Vector2.zero);
            var settings = SoulUi.Button(bar, "설정", SoulUi.TabInactive, () => PopupManager.Instance?.Open(PopupType.Settings), 18, SoulIconSet.Ui("gear"));
            SoulUi.Place((RectTransform)settings.transform, new Vector2(.705f, 0), new Vector2(.77f, 1), new Vector2(0, 18), new Vector2(-8, -18));
            newGameButton = SoulUi.Button(bar, "새로 시작", SoulUi.TabInactive, NewGame, 18);
            SoulUi.Place((RectTransform)newGameButton.transform, new Vector2(.775f, 0), new Vector2(.86f, 1), new Vector2(0, 18), new Vector2(-8, -18));
            departButton = SoulUi.Button(bar, "출정", new Color(.2f, .5f, .28f), PortalClicked, 28, SoulIconSet.Ui("exit"));
            SoulUi.Place((RectTransform)departButton.transform, new Vector2(.865f, 0), Vector2.one, new Vector2(0, 12), new Vector2(-20, -12));
            eventText = SoulUi.Text(root, "", 20, SoulUi.Accent, TextAlignmentOptions.TopLeft);
            SoulUi.Place(eventText.rectTransform, new Vector2(0, 1), new Vector2(.6f, 1), new Vector2(28, -TopBar - 40), new Vector2(0, -TopBar - 6));
            clockText = SoulUi.Text(root, "", 22, Color.white, TextAlignmentOptions.TopRight);
            SoulUi.Place(clockText.rectTransform, new Vector2(.6f, 1), Vector2.one, new Vector2(0, -TopBar - 40), new Vector2(-28, -TopBar - 6));

            // ── map ──
            var rows = (Data.GroundRows ?? "").Replace("\r", "").Split('\n');
            int height = rows.Length, width = 0;
            foreach (var row in rows) width = Mathf.Max(width, row.Length);
            map = SoulUi.Rect("Map", root);
            map.anchorMin = map.anchorMax = new Vector2(.5f, .5f);
            map.sizeDelta = new Vector2(width * Cell, height * Cell);
            map.anchoredPosition = new Vector2(0, -TopBar / 2 + 6);
            var ground = SoulUi.Rect("Ground", map);
            SoulUi.Stretch(ground);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    char key = x < rows[y].Length ? rows[y][x] : '.';
                    var tile = SoulUi.Icon(ground, Data.Tile(key) ?? Data.Tile('.'), Cell, "Tile");
                    tile.preserveAspect = false;
                    Place(tile.rectTransform, x, y, 1, 1);
                }
            walkerRoot = SoulUi.Rect("Walkers", map);
            SoulUi.Stretch(walkerRoot);
            walkSpots = WalkSpots(rows, width, height);
            foreach (var def in Data.Buildings) if (Campaign.HasBuilding(def.Kind)) BuildBuilding(def);
            BuildPortal(width, height);
            BuildTrips(root);
            dispatch = new SoulDispatchPanel(root, Sent, OpenReports);
            reports = new SoulReportPanel(root);
        }

        // The dungeon portal on the village square, in the middle of the map: a click sends the party down.
        void BuildPortal(int width, int height)
        {
            portal = SoulUi.Rect("Portal", map);
            Place(portal, width / 2 - 1, height / 2, 2, 2);
            portalGate = SoulUi.Icon(portal, SoulSprites.Object(SoulObjectKind.Escape), 10, "Gate");
            SoulUi.Stretch(portalGate.rectTransform);
            portalGate.raycastTarget = true;
            SoulUi.Clickable(portalGate.gameObject).onClick.AddListener(PortalClicked);
            var plate = SoulUi.Panel("Plate", portal, new Color(.08f, .07f, .1f, .82f), true);
            plate.raycastTarget = false;
            SoulUi.Place(plate.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-110, -34), new Vector2(110, -4));
            portalLabel = SoulUi.Text(plate.transform, "", 18, Color.white, TextAlignmentOptions.Center);
            SoulUi.Stretch(portalLabel.rectTransform);
        }

        // Parties in the dungeon, bottom left, a row each: where it is and how it goes (time left, kills, gold), its
        // members' faces with their health and wounds under them, and a button to watch.
        const float TripWidth = 780, TripRow = 104, FaceSize = 58;

        sealed class TripMember
        {
            public SoulMercenary Hero;
            public RectTransform Hp;
            public Image[] Wounds;
            public CanvasGroup Fade;
        }

        sealed class TripLine
        {
            public SoulExpedition Trip;
            public TextMeshProUGUI Status, Detail;
            public readonly List<TripMember> Members = new List<TripMember>();
        }

        readonly List<TripLine> tripLines = new List<TripLine>();

        void BuildTrips(Transform root)
        {
            tripPanel = SoulUi.Framed("Expeditions", root, SoulUi.PanelBg, SoulUi.PanelBorder);
            tripPanel.anchorMin = tripPanel.anchorMax = tripPanel.pivot = Vector2.zero;
            tripPanel.anchoredPosition = new Vector2(20, 20);
            tripPanel.sizeDelta = new Vector2(TripWidth, 60);
            var title = SoulUi.Text(tripPanel, "원정 중", 20, SoulUi.Accent, TextAlignmentOptions.TopLeft);
            title.fontStyle = FontStyles.Bold;
            SoulUi.Place(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(16, -38), new Vector2(-16, -8));
            tripRows = SoulUi.Rect("Rows", tripPanel);
            SoulUi.Place(tripRows, Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -42));
            SoulUi.Vertical(tripRows.gameObject, 6);
            tripPanel.gameObject.SetActive(false);
        }

        void RefreshTrips()
        {
            string key = string.Join(",", Campaign.Away.ConvertAll(trip => trip.Number + ":" + trip.Party.Count));
            if (key != shownTrips)
            {
                shownTrips = key;
                SoulUi.Clear(tripRows);
                tripLines.Clear();
                foreach (var trip in Campaign.Away) tripLines.Add(TripCard(trip));
                tripPanel.sizeDelta = new Vector2(TripWidth, 54 + (TripRow + 6) * Campaign.Away.Count);
                tripPanel.gameObject.SetActive(Campaign.Away.Count > 0);
            }
            foreach (var line in tripLines)
            {
                var trip = line.Trip;
                var session = trip.Session;
                int minutes = Mathf.FloorToInt(SoulClock.DungeonHoursLeft(Campaign.Clock, trip.ReturnAt) * 60), kills = 0;
                foreach (var hero in trip.Party) kills += session.Tally.For(hero).Kills;
                bool trouble = session.Plan == SoulPartyPlan.Retreat || session.Plan == SoulPartyPlan.Escape || session.Plan == SoulPartyPlan.Stranded;
                line.Status.text = SoulUi.Colored(trip.Status, trouble ? SoulUi.Bad : session.InCombat ? SoulUi.Accent : SoulUi.Good);
                line.Detail.text = $"남은 {minutes / 60}시간 {minutes % 60:00}분\n처치 {kills} · 금화 +{session.Gold}";
                foreach (var member in line.Members)
                {
                    var hero = member.Hero;
                    bool alive = hero.Alive && session.Mercenaries.Contains(hero);
                    member.Fade.alpha = alive ? 1f : .35f;
                    if (alive) SoulUi.SetHp(member.Hp, hero); else SoulUi.SetRatio(member.Hp, 0);
                    for (int i = 0; i < member.Wounds.Length; i++)
                        member.Wounds[i].color = i < hero.Wounds ? (hero.Wounds >= SoulDungeonSession.WoundLimit ? SoulUi.Bad : new Color(1f, .62f, .3f)) : new Color(.2f, .21f, .28f);
                }
            }
        }

        TripLine TripCard(SoulExpedition trip)
        {
            var line = new TripLine { Trip = trip };
            var card = SoulUi.Framed("Trip", tripRows, SoulKit.CardBg, SoulUi.PanelBorder, 2);
            SoulUi.Layout(card.gameObject, -1, TripRow);
            var name = SoulUi.Text(card, $"<b>{trip.Name}</b>", 22, Color.white, TextAlignmentOptions.TopLeft);
            SoulUi.Place(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -36), new Vector2(210, -8));
            line.Status = SoulUi.Text(card, "", 16, Color.white, TextAlignmentOptions.TopLeft);
            line.Status.textWrappingMode = TextWrappingModes.NoWrap;
            SoulUi.Place(line.Status.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -58), new Vector2(210, -36));
            line.Detail = SoulUi.Text(card, "", 15, SoulUi.SubText, TextAlignmentOptions.TopLeft);
            SoulUi.Place(line.Detail.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -100), new Vector2(210, -58));

            var faces = SoulUi.Row(card, TripRow - 12, 6);
            SoulUi.Place(faces, Vector2.zero, Vector2.one, new Vector2(216, 6), new Vector2(-112, -6));
            foreach (var hero in trip.Party)
            {
                var column = SoulUi.Rect("Member", faces);
                SoulUi.Layout(column.gameObject, FaceSize, TripRow - 12);
                var fade = column.gameObject.AddComponent<CanvasGroup>();
                var face = SoulUi.Portrait(column, SoulPortraits.For(hero), FaceSize, SoulIconSet.Ui("race"));
                SoulUi.Place(face, new Vector2(0, 1), Vector2.one, new Vector2(0, -FaceSize), Vector2.zero);
                var bar = SoulUi.Rect("Hp", column);
                SoulUi.Place(bar, new Vector2(0, 1), Vector2.one, new Vector2(0, -FaceSize - 12), new Vector2(0, -FaceSize - 3));
                SoulUi.Vertical(bar.gameObject, 0);
                var hp = SoulUi.Bar(bar, SoulUi.HpColor, 9);
                var pips = SoulUi.Rect("Wounds", column);
                SoulUi.Place(pips, new Vector2(0, 1), Vector2.one, new Vector2(0, -FaceSize - 24), new Vector2(0, -FaceSize - 15));
                var layout = SoulUi.Horizontal(pips.gameObject, 2);
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childForceExpandHeight = false;
                var wounds = new Image[SoulMercenary.MaxWounds];
                for (int i = 0; i < wounds.Length; i++)
                {
                    wounds[i] = SoulUi.Panel("Wound", pips, new Color(.2f, .21f, .28f), true);
                    wounds[i].raycastTarget = false;
                    SoulUi.Layout(wounds[i].gameObject, 9, 9);
                }
                line.Members.Add(new TripMember { Hero = hero, Hp = hp, Wounds = wounds, Fade = fade });
            }

            var watched = trip;
            var watch = SoulUi.Button(card, "관전", SoulUi.TabActive, () => Watch(watched), 18, SoulIconSet.Ui("follow"));
            SoulUi.Place((RectTransform)watch.transform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-104, -24), new Vector2(-10, 24));
            return line;
        }

        // Clock, portal and depart button.
        void RefreshClock()
        {
            clockText.text = $"<b>{SoulClock.Text(Campaign.Clock)}</b>";
            portalLabel.text = "던전 포탈";
            bool ready = Campaign.CanDepart;
            portalGate.color = ready ? Color.white : new Color(.55f, .55f, .65f, .7f);
            portal.localScale = Vector3.one * (ready ? 1 + .05f * Mathf.Sin(Time.unscaledTime * 3) : 1);
            departButton.GetComponent<Image>().color = ready ? new Color(.2f, .5f, .28f) : SoulUi.TabInactive; // still clickable: the next morning
        }

        // ── those at home walk around ──

        RectTransform walkerRoot;
        List<Vector2> walkSpots;
        readonly List<SoulVillageWalker> walkers = new List<SoulVillageWalker>();

        // Cell centres not under a building or the portal.
        List<Vector2> WalkSpots(string[] rows, int width, int height)
        {
            var spots = new List<Vector2>();
            int portalX = width / 2 - 1, portalY = height / 2;
            for (int y = 1; y < height - 1; y++)
                for (int x = 1; x < width - 1; x++)
                {
                    bool blocked = x >= portalX && x < portalX + 2 && y >= portalY && y < portalY + 2;
                    foreach (var def in Data.Buildings)
                        blocked |= Campaign.HasBuilding(def.Kind) && x >= def.Cell.x && x < def.Cell.x + def.Size.x && y >= def.Cell.y && y < def.Cell.y + def.Size.y;
                    if (!blocked) spots.Add(new Vector2((x + .5f) * Cell, -(y + .7f) * Cell));
                }
            return spots;
        }

        void RefreshWalkers()
        {
            if (walkSpots == null || walkSpots.Count == 0) return;
            for (int i = walkers.Count - 1; i >= 0; i--)
                if (walkers[i] == null || !Campaign.Roster.Contains(walkers[i].Hero)) { if (walkers[i] != null) Destroy(walkers[i].gameObject); walkers.RemoveAt(i); }
            foreach (var hero in Campaign.Roster)
                if (!walkers.Exists(w => w.Hero == hero)) walkers.Add(SoulVillageWalker.Create(walkerRoot, hero, walkSpots, Cell));
            foreach (var walker in walkers) walker.Post = TrainingPost(walker.Hero);
            // nearer the bottom is in front
            walkers.Sort((a, b) => ((RectTransform)b.transform).anchoredPosition.y.CompareTo(((RectTransform)a.transform).anchoredPosition.y));
            for (int i = 0; i < walkers.Count; i++) walkers[i].transform.SetSiblingIndex(i);
        }

        // Where a mercenary trains: the free spot nearest the front of the training ground (side by side).
        Vector2? TrainingPost(SoulMercenary hero)
        {
            int at = System.Array.FindIndex(Data.Buildings, def => def.Kind == SoulBuildingKind.Training);
            if (at < 0) return null;
            var def = Data.Buildings[at];
            int slot = (hero.Id.GetHashCode() & int.MaxValue) % 5 - 2;
            var wanted = new Vector2((def.Cell.x + def.Size.x / 2f + slot * .7f) * Cell, -(def.Cell.y + def.Size.y + .7f) * Cell);
            var best = walkSpots[0];
            foreach (var spot in walkSpots) if ((spot - wanted).sqrMagnitude < (best - wanted).sqrMagnitude) best = spot;
            return best;
        }

        // Grid cell (x, y) counted from the top-left, spanning w × h cells.
        void Place(RectTransform rect, int x, int y, int w, int h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(w * Cell, h * Cell);
            rect.anchoredPosition = new Vector2(x * Cell, -y * Cell);
        }

        void BuildBuilding(SoulBuildingDef def)
        {
            var holder = SoulUi.Rect(def.Name, map);
            Place(holder, def.Cell.x, def.Cell.y, def.Size.x, def.Size.y);
            var picture = SoulUi.Icon(holder, null, 10, "Building");
            picture.preserveAspect = false;
            SoulUi.Stretch(picture.rectTransform);
            picture.raycastTarget = true;
            var button = SoulUi.Clickable(picture.gameObject);
            var colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.05f);
            colors.pressedColor = new Color(.85f, .85f, .85f);
            button.colors = colors;
            var kind = def.Kind;
            button.onClick.AddListener(() => Enter(kind, holder));

            var plate = SoulUi.Panel("Plate", holder, new Color(.08f, .07f, .1f, .82f), true);
            plate.raycastTarget = false;
            SoulUi.Place(plate.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-96, -34), new Vector2(96, -4));
            var label = SoulUi.Text(plate.transform, "", 19, Color.white, TextAlignmentOptions.Center);
            SoulUi.Stretch(label.rectTransform);
            buildings[kind] = (picture, label);
            // something waiting inside (books to register, wounds to heal, a reward to claim …)
            var badge = SoulUi.Panel("Badge", holder, new Color(.85f, .25f, .22f, .95f), true);
            badge.raycastTarget = false;
            SoulUi.Place(badge.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-80, -2), new Vector2(80, 26));
            var badgeText = SoulUi.Text(badge.transform, "", 16, Color.white, TextAlignmentOptions.Center);
            badgeText.fontStyle = FontStyles.Bold;
            SoulUi.Stretch(badgeText.rectTransform);
            badges[kind] = (badge.gameObject, badgeText);
        }

        void Enter(SoulBuildingKind kind, RectTransform holder)
        {
            if (entering || SoulDoorTransition.Busy || (PopupManager.Instance != null && PopupManager.Instance.IsOpen(SoulBuildingPopup.PopupFor(kind)))) return;
            StartCoroutine(EnterRoutine(kind, holder));
        }

        bool entering;

        // A small push toward the building, then the doors.
        IEnumerator EnterRoutine(SoulBuildingKind kind, RectTransform holder)
        {
            holder.SetAsLastSibling();
            if (kind == SoulBuildingKind.Board)
            {
                // a notice board in the open: lean in to read it (no doors)
                entering = true;
                holder.pivot = new Vector2(.5f, .5f);
                holder.anchoredPosition += new Vector2(holder.sizeDelta.x * .5f, -holder.sizeDelta.y * .5f);
                for (float t = 0; t < .22f; t += Time.unscaledDeltaTime)
                {
                    float k = Mathf.SmoothStep(0, 1, t / .22f);
                    holder.localScale = Vector3.one * (1 + .35f * k);
                    yield return null;
                }
                SoulBuildingPopup.Open(kind);
                yield return null;
                holder.localScale = Vector3.one;
                holder.anchoredPosition -= new Vector2(holder.sizeDelta.x * .5f, -holder.sizeDelta.y * .5f);
                holder.pivot = new Vector2(0, 1);
                entering = false;
                yield break;
            }
            for (float t = 0; t < .16f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Sin(Mathf.Clamp01(t / .16f) * Mathf.PI);
                holder.localScale = Vector3.one * (1 + .06f * k);
                yield return null;
            }
            holder.localScale = Vector3.one;
            SoulDoorTransition.Play(Data.DoorLeft, Data.DoorRight, () => SoulBuildingPopup.Open(kind));
        }

        // Starting over wipes the save: the first press arms it, a second within 3 seconds does it.
        void NewGame()
        {
            if (Time.unscaledTime > newGameArmed)
            {
                newGameArmed = Time.unscaledTime + 3;
                SoulUi.SetLabel(newGameButton, "한 번 더 누르면 삭제");
                newGameButton.GetComponent<Image>().color = SoulUi.Bad;
                return;
            }
            newGameArmed = 0;
            SoulUi.SetLabel(newGameButton, "새로 시작");
            newGameButton.GetComponent<Image>().color = SoulUi.TabInactive;
            PickDifficulty();
        }

        // The new company's difficulty: 보통 · 어려움 · 최상 (only there does the dungeon kill for good).
        void PickDifficulty()
        {
            var shade = SoulUi.Panel("DifficultyPicker", uiRoot, new Color(0, 0, 0, .7f));
            SoulUi.Stretch(shade.rectTransform);
            shade.raycastTarget = true;
            shade.gameObject.AddComponent<Button>().onClick.AddListener(() => Destroy(shade.gameObject));
            var window = SoulUi.Framed("Window", shade.transform, SoulUi.PanelBg, SoulUi.Accent, 3);
            window.anchorMin = window.anchorMax = new Vector2(.5f, .5f);
            window.sizeDelta = new Vector2(640, 230);
            window.GetComponent<Image>().raycastTarget = true;
            var head = SoulUi.Text(window, "난이도", 30, SoulUi.Accent, TextAlignmentOptions.Center);
            head.fontStyle = FontStyles.Bold;
            SoulUi.Place(head.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -64), new Vector2(0, -14));
            var choices = new[] { SoulDifficulty.Normal, SoulDifficulty.Hard, SoulDifficulty.Extreme };
            for (int i = 0; i < choices.Length; i++)
            {
                var difficulty = choices[i];
                string label = SoulCampaign.DifficultyName(difficulty) + (difficulty == SoulDifficulty.Extreme ? "\n<size=16>영구 사망</size>" : "");
                var pick = SoulUi.Button(window, label, difficulty == SoulDifficulty.Extreme ? SoulUi.Bad : SoulUi.TabInactive, () => StartOver(difficulty), 24);
                float from = i / 3f, to = (i + 1) / 3f;
                SoulUi.Place((RectTransform)pick.transform, new Vector2(from, 0), new Vector2(to, 0), new Vector2(i == 0 ? 24 : 8, 24), new Vector2(i == 2 ? -24 : -8, 124));
            }
        }

        void StartOver(SoulDifficulty difficulty)
        {
            PopupManager.Instance?.CloseAll();
            SoulTooltip.Hide();
            SoulSave.Delete();
            SoulCampaign.Current = new SoulCampaign(Data, System.Environment.TickCount & int.MaxValue, SoulSave.Wallet) { Difficulty = difficulty };
            SoulSave.Save(SoulCampaign.Current, true);
            SceneManager.LoadScene(Data.VillageScene);
        }

        // What a building has waiting (null: nothing).
        string Badge(SoulBuildingKind kind)
        {
            switch (kind)
            {
                case SoulBuildingKind.Guild:
                    return Campaign.Roster.Exists(h => h.CanPickPattern) ? "새 패턴 선택" : null;
                case SoulBuildingKind.Board:
                    int done = Campaign.Active.FindAll(q => q.Done).Count;
                    return done > 0 ? $"의뢰 보상 {done}" : null;
                case SoulBuildingKind.Church:
                    int hurt = Campaign.Roster.FindAll(h => h.Wounds > 0).Count;
                    return hurt > 0 ? $"부상당한 용병 {hurt}" : null;
                case SoulBuildingKind.Library:
                    return Campaign.Books.Count > 0 ? $"새 스킬북 {Campaign.Books.Count}" : null;
                case SoulBuildingKind.SoulAltar:
                    return Campaign.Vault.Count > 0 ? $"보관 영혼 {Campaign.Vault.Count}" : null;
                case SoulBuildingKind.Storage:
                    return Campaign.StorageFull ? "창고 가득" : null;
                case SoulBuildingKind.Shop:
                    return Campaign.Event == SoulVillageEvent.Festival ? "축제 할인" : null;
                default: return null;
            }
        }

        // ── the portal: the dispatch (SoulDispatchPanel) — or, nobody able to go down, the next morning ──

        SoulDispatchPanel dispatch;
        SoulReportPanel reports;

        void PortalClicked()
        {
            if (Campaign == null || SoulDoorTransition.Busy) return;
            PopupManager.Instance?.CloseAll();
            SoulTooltip.Hide();
            // a party can go, or some are still down there (hunting): the dispatch as usual
            if (Campaign.CanDepart || Campaign.Away.Count > 0) { dispatch.Show(); return; }
            ConfirmPopup.Ask("next_day", $"더 이상 탐험을 진행할 수 있는 용병이 없습니다.\n다음 날로 이동하시겠습니까?\n<size=22>{SoulClock.Day(Campaign.Clock) + 1}일차 {SoulClock.HourMinute(SoulClock.Start)}</size>",
                () => Campaign.SkipToMorning());
        }

        // The chosen parties went down together; the village goes with them (the first one on screen).
        void Sent(List<SoulExpedition> sent)
        {
            if (sent.Count > 0) Watch(sent[0]);
        }

        void OpenReports()
        {
            dispatch.Hide();
            reports.Show();
        }

        void Watch(SoulExpedition trip)
        {
            if (trip == null || trip.Settled || SoulDoorTransition.Busy) return;
            PopupManager.Instance?.CloseAll();
            SoulTooltip.Hide();
            Campaign.Watching = trip;
            SceneManager.LoadScene(Data.DungeonScene);
        }

        void Update()
        {
            if (newGameArmed > 0 && Time.unscaledTime > newGameArmed)
            {
                newGameArmed = 0;
                SoulUi.SetLabel(newGameButton, "새로 시작");
                newGameButton.GetComponent<Image>().color = SoulUi.TabInactive;
            }
            if (Campaign == null) return;
            RefreshClock();
            RefreshTrips();
            RefreshWalkers();
            // a party came back: its report, once nothing else is open
            if (!reports.Visible && !dispatch.Visible && !SoulDoorTransition.Busy && (PopupManager.Instance == null || !PopupManager.Instance.HasAnyOpen))
            {
                var unread = Campaign.Reports.Find(r => !r.Seen);
                if (unread != null) reports.Show(unread);
            }
            // a portrait finished drawing: the open panels show it
            if (shownPortraits != SoulPortraits.Version)
            {
                shownPortraits = SoulPortraits.Version;
                if (dispatch.Visible) dispatch.Refresh();
                if (reports.Visible) reports.Refresh();
            }
            if (shownRevision == Campaign.Revision) return;
            shownRevision = Campaign.Revision;
            SoulSave.Save(Campaign); // every change in the village is kept (parties away count as home: SoulSave)
            if (dispatch.Visible) dispatch.Refresh();
            if (reports.Visible) reports.Refresh();
            goldText.text = $"<color=#{SoulUi.Hex(SoulUi.Accent)}>금화 {Campaign.Gold}</color>   명성 {Campaign.Renown} <size=18>({Campaign.RenownName})</size>   창고 {Campaign.Inventory.Count}/{Campaign.StorageCapacity}"
                + (Campaign.Blessing != null ? $"   {SoulUi.Colored(Campaign.Blessing.Name, SoulUi.Good)}" : "");
            var news = new List<string>();
            if (Campaign.EventText != null && Campaign.Event != SoulVillageEvent.Thief) news.Add(Campaign.EventText);
            if (Campaign.MerchantHere) news.Add("떠돌이 상인이 광장에 와 있습니다");
            if (Campaign.Books.Count > 0) news.Add($"서고에 등록할 스킬북 {Campaign.Books.Count}권");
            eventText.text = string.Join("   ·   ", news);
            int ready = Campaign.Parties.FindAll(party => party.Members.Count > 0 && !Campaign.IsAway(party)).Count;
            int wounded = Campaign.Roster.FindAll(hero => hero.Wounds > 0 && Campaign.PartyOf(hero) != null).Count;
            partyText.text = $"출정 준비 {ready}파티" + (wounded > 0 ? "  " + SoulUi.Colored($"부상 {wounded}", SoulUi.Bad) : "");
            foreach (var def in Data.Buildings)
            {
                if (!buildings.TryGetValue(def.Kind, out var view)) continue;
                // the merchant's wagon is only there while the merchant is
                if (def.Kind == SoulBuildingKind.Merchant) view.picture.transform.parent.gameObject.SetActive(Campaign.MerchantHere);
                int level = Campaign.Level(def.Kind);
                view.picture.sprite = def.Sprites != null && level < def.Sprites.Length ? def.Sprites[level] : null;
                view.picture.enabled = true; // created without a sprite (SoulUi.Icon disables it then)
                view.picture.color = view.picture.sprite != null ? Color.white : new Color(1, 1, 1, .15f);
                bool fixedLevel = def.UpgradeCosts == null || def.UpgradeCosts.Length <= 1;
                view.label.text = level == 0 ? $"{def.Name}  <size=15>(건설 가능)</size>" : fixedLevel ? def.Name : $"{def.Name}  <size=16><color=#{SoulUi.Hex(SoulUi.Accent)}>Lv.{level}</color></size>";
            }
            foreach (var entry in badges)
            {
                string note = Campaign.Level(entry.Key) > 0 ? Badge(entry.Key) : null;
                entry.Value.root.SetActive(note != null);
                if (note != null) entry.Value.text.text = note;
            }
        }
    }
}
