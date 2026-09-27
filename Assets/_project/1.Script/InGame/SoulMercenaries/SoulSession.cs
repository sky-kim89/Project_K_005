using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    public sealed class SoulDrop
    {
        public readonly SoulData Soul;
        public bool Preserved;
        public SoulDrop(SoulData soul) { Soul = soul; }
    }

    public sealed class SoulLevelChoice
    {
        public readonly SoulMercenary Mercenary;
        public readonly SoulPatternData[] Patterns;
        public readonly int Level;
        // Rolled when the choice opens; reopening the window shows the same result (no reroll).
        public readonly IReadOnlyDictionary<StatType, int> Points;
        public SoulLevelChoice(SoulMercenary mercenary, SoulPatternData[] patterns, IReadOnlyDictionary<StatType, int> points)
        { Mercenary = mercenary; Patterns = patterns; Points = points; Level = mercenary.Level; }
    }

    public enum SoulEventKind { Attack, Skill, Hit, Miss, Guard, Status, Knockback, Death, Absorb, LevelUp, Cover, Counter, Shockwave, Leap, Roll, Flank, Chest, Loot, Evade, Telegraph, Impact, Trap, TrapFound, Interact, SoulDrop, Revive, SkillFx }
    // A skill's picture (SkillFx): where it lands, each one it touches, and a link from one to the next (a chain).
    public enum SoulFxPhase { Land, Victim, Link }

    public sealed class SoulTrap
    {
        public Vector2 Position;
        public SoulTrapKind Kind;
        public Vector2 Target; // Warp: where it throws the party
        public bool Found;  // spotted by a pathfinder: disarmed
        public bool Sprung;
    }

    public sealed class SoulHiddenStage
    {
        public SoulHiddenStageKind Kind;
        public RectInt Rect;
        public Vector2Int Door;
        public bool Revealed; // marked on the map by a pathfinder's luck
        public bool Found;    // its secret wall is open
    }

    // Something the player can click: a mercenary walks up to it and uses it.
    public sealed class SoulInteractable
    {
        public SoulObjectKind Kind;
        public Vector2 Position;
        public bool Used;
        public float Cooldown;
        public SoulWarpKind WarpKind;
        public Vector2 WarpTarget;
        public Vector2Int WarpDoor;
        public bool Mimic;
        public bool Insight; // a pathfinder's luck told what it holds (warp destination, mimic or not)

        public string Name
        {
            get
            {
                switch (Kind)
                {
                    case SoulObjectKind.Spring: return "회복의 샘";
                    case SoulObjectKind.Campfire: return "야영지";
                    case SoulObjectKind.Blessing: return "던전의 가호";
                    case SoulObjectKind.Altar: return "경험의 제단";
                    case SoulObjectKind.Warp: return "워프석" + (!Insight ? "" : WarpKind == SoulWarpKind.Horde ? " (대군집)" : WarpKind == SoulWarpKind.Treasure ? " (보물)" : " (보스)");
                    case SoulObjectKind.Watchtower: return "전망대";
                    case SoulObjectKind.Suspicious: return "수상한 상자" + (!Insight ? "" : Mimic ? " (미믹!)" : " (보물)");
                    case SoulObjectKind.Escape: return "탈출 포탈";
                    default: return "행운의 석상";
                }
            }
        }
    }

    // An attack in progress: the area is fixed when the cast starts; after WindUp everything inside is hit.
    // Every attack goes through here — a boss slam, a mercenary's thrust, a wolf's bite. The shape is what
    // makes a "single target" attack single target: a lane one body wide.
    public sealed class SoulTelegraph
    {
        public SoulCombatant Caster, Target;
        public SoulTelegraphData Data;      // boss / elite pattern, null for ordinary attacks
        public SoulPatternData Pattern;     // the attack pattern behind it, null for boss patterns
        public string Name;
        public SoulAreaShape Shape;
        public SoulAreaAnchor Anchor;
        public float Size, Width, WindUp, Rest;
        public SoulHit Hit;                 // evaluated from the caster's stats when the cast starts
        public int Hits = 1;                // multi-hit patterns land this many times
        public bool Hostile;                // cast by a monster: the area is drawn as a threat
        public bool Basic;                  // weapon fallback, not a learned pattern
        public bool Magic;                  // a spell's circle (views: magic bolt)
        public SoulActiveSkillData Skill;   // a skill's warning: when it lands, the skill's own effects follow (SoulDungeonSession.StrikeSkill)
        public SoulActiveSkillData FxSkill; // a spell's circle: the spell drawn when it lands
        public bool Signature => Data != null;   // named boss pattern: big label, longer read
        public Vector2 Origin, Direction;
        public float Elapsed;
        public bool Struck;
        public float Progress => Mathf.Clamp01(Elapsed / Mathf.Max(.01f, WindUp));
        public Vector2 CircleCenter => Anchor == SoulAreaAnchor.Forward ? Origin + Direction * Size : Origin;

        // pad: the body radius (a body touching the area is hit).
        public bool Contains(Vector2 point, float pad)
        {
            Vector2 v = point - Origin;
            switch (Shape)
            {
                case SoulAreaShape.Circle:
                    return Vector2.Distance(point, CircleCenter) <= Size + pad;
                case SoulAreaShape.Cone:
                    float distance = v.magnitude;
                    if (distance > Size + pad) return false;
                    if (distance <= pad) return true;
                    return Vector2.Angle(Direction, v) <= Width / 2 + Mathf.Asin(Mathf.Clamp01(pad / distance)) * Mathf.Rad2Deg;
                default:
                    float along = Vector2.Dot(v, Direction), side = Mathf.Abs(Direction.x * v.y - Direction.y * v.x);
                    bool length = Anchor == SoulAreaAnchor.Self ? Mathf.Abs(along) <= Size / 2 + pad : along >= -pad && along <= Size + pad;
                    return length && side <= Width / 2 + pad;
            }
        }
    }

    // Dropped by a defeated boss; a mercenary walking up to it opens it and the equipment goes to the inventory.
    public sealed class SoulChest
    {
        public Vector2 Position;
        public SoulItem[] Contents;
        public SoulActiveSkillData Book; // a skill book inside, if any
        public string Supply;            // a scroll inside, if any
        public bool Opened;
        public bool Hidden;   // in an alcove or hidden stage behind a secret door
        public bool Revealed; // marked on the map by a pathfinder's luck
        public bool InStage;  // inside a hidden stage (traps, monsters or a boss on the way)
    }

    // Presentation feed: the session records what happened, views drain it (animation, damage numbers).
    public struct SoulCombatEvent
    {
        public SoulEventKind Kind;
        public SoulCombatant Actor, Target;
        public SoulPatternData Pattern;
        public SoulStatus Status;
        public float Amount;
        public string Label;
        public bool Critical; // Hit: a 치명타
        public bool Magic; // a spell: the views play the casting motion and a magic bolt
        public bool Support; // a heal, buff, revive or potion: the views play the blessing motion, no swing
        public SoulActiveSkillData Skill; // SkillFx: the skill drawn
        public Vector2 Point, Direction;  // SkillFx: where (map), which way
        public SoulFxPhase Phase;
    }

    public sealed partial class SoulDungeonSession
    {
        internal sealed class RouteState
        {
            public bool Valid;   // false: plan again on the next step
            public Vector2 Goal;
            public float Radius;
            public readonly SoulPath Path = new SoulPath();
            public int Step;
            public bool Careful; // after a blocked step: follow waypoints one by one, no shortcuts
        }

        struct TriggerEvent
        {
            public SoulCombatant Owner, Target;
            public SoulTrigger Trigger;
        }

        readonly System.Random random;
        readonly SoulDungeonData dungeon;
        readonly SoulStatRules rules;
        readonly Dictionary<SoulCombatant, RouteState> routes = new Dictionary<SoulCombatant, RouteState>();
        readonly Dictionary<SoulMercenary, Vector2> destinations = new Dictionary<SoulMercenary, Vector2>();
        readonly Queue<TriggerEvent> triggers = new Queue<TriggerEvent>();
        readonly HashSet<string> rewarded = new HashSet<string>();
        readonly HashSet<string> encounters = new HashSet<string>();
        int soulsDropped;
        readonly Dictionary<SoulMercenary, Vector2Int> lastReveal = new Dictionary<SoulMercenary, Vector2Int>();
        readonly HashSet<SoulMercenary> witnessedDeath = new HashSet<SoulMercenary>();
        public readonly Dictionary<SoulMercenary, int> Kills = new Dictionary<SoulMercenary, int>();
        public readonly List<string> MentalReport = new List<string>();
        // The whole expedition's numbers and finds (handed on floor to floor): the report back in the village.
        public SoulTripTally Tally = new SoulTripTally();

        // Fog of war: cells any mercenary has seen (radius + line of sight). The player commands on what is seen.
        public const float VisionRadius = 6.5f, DoorDetectRadius = 3.5f;
        public bool AutoExplore = true;

        public bool PartyHas(SoulMapTrait trait) => Mercenaries.Exists(hero => hero.Alive && (hero.MapTraits & trait) != 0);
        // What the party knows beyond its own sight (pathfinder options).
        // Portals to the next stage (the sample ruins: one at each of the north/east/south/west edges).
        readonly List<Vector2Int> exits = new List<Vector2Int>();
        public IReadOnlyList<Vector2Int> Exits => exits;
        public bool IsExit(Vector2Int cell) => exits.Contains(cell);
        public bool IsExitKnown(Vector2Int exit) => IsExplored(exit) || PartyHas(SoulMapTrait.RevealExit) || Perks.RevealExits || marks.Contains(exit);
        public bool ExitKnown => exits.Exists(IsExitKnown);
        public readonly List<SoulChest> Chests = new List<SoulChest>();
        public readonly List<SoulItem> Inventory = new List<SoulItem>();
        public bool BossAlive => Monsters.Exists(m => m.Alive && m.Data.Guardian);
        public bool Knows(SoulMonster monster) => IsExplored(Map.Cell(monster.Position)) || (monster.Data.Guardian && (PartyHas(SoulMapTrait.RevealGuardian) || marks.Contains(monster)));
        public bool Knows(SoulChest chest) => IsExplored(Map.Cell(chest.Position)) || marks.Contains(chest);
        public bool Knows(SoulInteractable thing) => IsExplored(Map.Cell(thing.Position)) || thing.Insight || marks.Contains(thing); // Insight: known from the start (the escape portal)
        public bool Knows(SoulHiddenStage stage) => stage.Found || marks.Contains(stage);
        // Landmarks this party marked on its map by luck (portals as cells, the boss, chests, stages, objects).
        readonly HashSet<object> marks = new HashSet<object>();
        public readonly List<SoulTrap> Traps = new List<SoulTrap>();
        public readonly List<SoulHiddenStage> HiddenStages = new List<SoulHiddenStage>();
        public readonly List<SoulInteractable> Interactables = new List<SoulInteractable>();

        // ── luck ─────────────────────────────────────────────────

        // The best Luck among the party's pathfinders (-1: none).
        public float PathfinderLuck
        {
            get
            {
                float best = -1;
                foreach (var hero in Mercenaries)
                    if (hero.Alive && (hero.MapTraits & SoulMapTrait.RouteSense) != 0) best = Mathf.Max(best, hero.Stats.Total(StatType.Luck));
                return best;
            }
        }

        // Chance that a pathfinder already knows a hidden thing when a floor starts: 6% a point of Luck (max 95%).
        public static float LuckChance(float luck) => luck < 0 ? 0 : Mathf.Clamp(luck * .06f, 0, .95f);
        // How far a secret-finder spots hidden doors and traps: 2.5 tiles + 0.15 a point of Luck.
        public static float DetectRadius(SoulCombatant unit) => 2.5f + .15f * unit.Stats.Total(StatType.Luck);

        // Chance that the party's luck marks a landmark (a portal, the boss, a treasure chest) when it arrives:
        // 0.8% a point of the best Luck in the party — a pathfinder's route sense makes it LuckChance (6% a point).
        public float LandmarkChance
        {
            get
            {
                float luck = PartyBest(StatType.Luck);
                return CanChooseRoute ? LuckChance(luck) : Mathf.Clamp(luck * .008f, 0, .5f);
            }
        }

        void RevealByLuck()
        {
            float chance = LandmarkChance;
            int landmarks = 0;
            foreach (var exit in exits) if (random.NextDouble() < chance && marks.Add(exit)) landmarks++;
            foreach (var monster in Monsters) if (monster.Data.Guardian && random.NextDouble() < chance && marks.Add(monster)) landmarks++;
            foreach (var chest in Chests) if (!chest.Hidden && random.NextDouble() < chance && marks.Add(chest)) landmarks++;
            if (landmarks > 0) Log($"운이 좋았습니다 — 특수 지형 {landmarks}곳을 지도에 표시");
            // a pathfinder also knows hidden things now and then (6% a point of its Luck)
            float hidden = LuckChance(PathfinderLuck);
            if (hidden <= 0) return;
            int marked = 0;
            foreach (var chest in Chests) if (chest.Hidden && random.NextDouble() < hidden && marks.Add(chest)) marked++;
            foreach (var stage in HiddenStages) if (random.NextDouble() < hidden && marks.Add(stage)) marked++;
            foreach (var trap in Traps) if (random.NextDouble() < hidden) { trap.Found = true; marked++; }
            foreach (var thing in Interactables) if (random.NextDouble() < hidden && marks.Add(thing)) marked++;
            Log($"길잡이의 감(운 {PathfinderLuck:0}, {hidden * 100:0}%): 숨겨진 것 {marked}곳을 지도에 표시");
        }

        // ── floors ───────────────────────────────────────────────

        // Eight floors; each one multiplies monster health / attack by 1.6 (floor 8: ×27) and experience by 1.9.
        public const int FinalFloor = 8, FirstKillMultiplier = 10;
        public int Floor { get; }
        public SoulFloorTheme Theme => SoulFloorTheme.For(Floor);
        public static float FloorPower(int floor) => Mathf.Pow(1.6f, Mathf.Clamp(floor, 1, FinalFloor) - 1);
        public static float FloorExperience(int floor) => Mathf.Pow(1.9f, Mathf.Clamp(floor, 1, FinalFloor) - 1);
        public bool HasNextFloor => Finished && Floor < FinalFloor;

        // The party (and its gold, kept souls and equipment) goes down to a new, freshly generated floor.
        public SoulDungeonSession NextFloor(int seed)
        {
            if (!HasNextFloor) return null;
            // the fallen are dead: only the living go down
            var next = new SoulDungeonSession(dungeon, rules, Mercenaries.FindAll(hero => hero.Alive), seed, Floor + 1, PreservationItems, Day);
            next.Gold = Gold;
            foreach (var entry in Belt) next.Belt[entry.Key] = entry.Value;
            foreach (var entry in Pouch) next.Pouch[entry.Key] = entry.Value;
            next.FoundBooks.AddRange(FoundBooks);
            next.DungeonMinutes = DungeonMinutes;
            next.TargetFloor = TargetFloor; next.TargetMode = TargetMode; next.ExploreMode = ExploreMode;
            next.Perks = Perks; next.FreeStones = FreeStones; next.FloorSleeps = FloorSleeps;
            next.AutoExplore = AutoExplore; next.Unattended = Unattended;
            foreach (var entry in DefeatedCounts) next.DefeatedCounts[entry.Key] = entry.Value;
            foreach (var entry in SoulsByMonster) next.SoulsByMonster[entry.Key] = entry.Value;
            next.ChestsOpened = ChestsOpened; next.HiddenFound = HiddenFound;
            next.ElitesDefeated = ElitesDefeated; next.BossesDefeated = BossesDefeated;
            next.Vault.AddRange(Vault);
            next.Inventory.AddRange(Inventory);
            next.Tally = Tally;
            return next;
        }

        // ── exploration aim (pathfinder) ─────────────────────────

        public SoulExploreMode ExploreMode = SoulExploreMode.Explore;
        public bool CanChooseRoute => PartyHas(SoulMapTrait.RouteSense);
        // At the target floor the party's order (hunt / farm) holds, pathfinder or not.
        public SoulExploreMode EffectiveMode => AtTarget ? TargetMode : CanChooseRoute ? ExploreMode : SoulExploreMode.Explore;
        public bool[] Explored { get; private set; }
        public int ExploreRevision { get; private set; }
        // Tile indices in the order they became explored (or a hidden door there was found): views repaint
        // only the new entries instead of the whole map.
        public readonly List<int> ExploreLog = new List<int>();

        // Seen with one's own eyes (line of sight) or from a watchtower. Knowing a tile from a map sense — seeing
        // through walls — fills Explored but not Seen: the dungeon view stays black there, only the minimap knows.
        public bool[] Seen { get; private set; }
        public readonly List<int> SeenLog = new List<int>();
        public bool IsSeen(Vector2Int cell)
            => cell.x >= 0 && cell.y >= 0 && cell.x < Map.Width && cell.y < Map.Height && Seen[cell.y * Map.Width + cell.x];
        public bool IsExplored(Vector2Int cell)
            => cell.x >= 0 && cell.y >= 0 && cell.x < Map.Width && cell.y < Map.Height && Explored[cell.y * Map.Width + cell.x];

        // Guardians gate the exit; without any guardian every monster must fall.
        // An optional boss (BossGatesExit off) leaves the portals open from the start.
        public bool ExitOpen => Monsters.Exists(m => m.Data.Guardian)
            ? !dungeon.BossGatesExit || !BossAlive
            : Monsters.TrueForAll(m => !m.Alive);
        public Vector2Int Exit => exits[0];
        readonly Dictionary<SoulMercenary, SoulLevelChoice> pendingLevels = new Dictionary<SoulMercenary, SoulLevelChoice>();
        public readonly List<SoulMercenary> Mercenaries = new List<SoulMercenary>();
        public readonly List<SoulMonster> Monsters = new List<SoulMonster>();
        public readonly List<SoulDrop> Stash = new List<SoulDrop>();
        public readonly List<SoulData> Vault = new List<SoulData>();
        public readonly List<string> Events = new List<string>();
        public readonly List<SoulCombatEvent> CombatEvents = new List<SoulCombatEvent>();
        readonly HashSet<SoulMercenary> fallen = new HashSet<SoulMercenary>();
        // once-a-floor skills already used on this floor (기적)
        readonly HashSet<(SoulCombatant, SoulActiveSkillData)> usedThisFloor = new HashSet<(SoulCombatant, SoulActiveSkillData)>();
        public SoulMap Map { get; }
        public SoulLayout Layout { get; }
        public Vector2Int PartyStart { get; }  // the entrance (the escape portal stands there)
        public Vector2Int Arrival { get; }     // where this party was sent to (a random room)
        public int Gold { get; set; }
        // Tally for guild quests: kills by monster id, elites and bosses defeated.
        public readonly Dictionary<string, int> DefeatedCounts = new Dictionary<string, int>();
        public int ElitesDefeated { get; private set; }
        // For the board's quests: souls released per monster kind, chests opened, hidden stages found.
        public readonly Dictionary<string, int> SoulsByMonster = new Dictionary<string, int>();
        public int ChestsOpened { get; private set; }
        public int HiddenFound { get; private set; }
        public int BossesDefeated { get; private set; }
        public int PreservationItems { get; private set; }
        // The guild's abilities the party took along (set when it leaves; every floor keeps them).
        public SoulExpeditionPerks Perks = new SoulExpeditionPerks();
        // Soul stones the guild gave (영혼석 주머니): used first, never carried home.
        public int FreeStones { get; private set; }
        public void AddFreeStones(int count) { if (count <= 0) return; FreeStones += count; PreservationItems += count; }
        // Stones that go back to the store: what is left, less the guild's.
        public int StonesLeft => Mathf.Max(0, PreservationItems - FreeStones);
        public bool Finished { get; private set; }
        public bool Defeated { get; private set; }

        public SoulDungeonSession(SoulDungeonData dungeon, SoulStatRules rules, IEnumerable<SoulMercenaryData> party, int seed, int preservationItems = 1)
            : this(dungeon, rules, Recruit(party, rules), seed, 1, preservationItems) { }

        public static List<SoulMercenary> Recruit(IEnumerable<SoulMercenaryData> party, SoulStatRules rules)
        {
            var heroes = new List<SoulMercenary>();
            foreach (var definition in party) heroes.Add(RecruitOne(definition, rules));
            return heroes;
        }

        // One mercenary from its data; id and name can be overridden (guild hires share a template).
        public static SoulMercenary RecruitOne(SoulMercenaryData definition, SoulStatRules rules, string id = null, string name = null, int gearGrade = 2, SoulRaceData race = null)
        {
            {
                var stats = new Dictionary<StatType, float>();
                foreach (var bonus in definition.BaseStats)
                    stats[bonus.Stat] = stats.TryGetValue(bonus.Stat, out float value) ? value + bonus.Value : bonus.Value;
                var hero = new SoulMercenary(id ?? definition.Id, name ?? definition.DisplayName, race ?? definition.Race, stats, rules);
                foreach (var gear in definition.StartingEquipment) if (gear != null) hero.Equipment.Add(new SoulItem(gear, gearGrade));
                hero.Look = definition.Look;
                if (definition.GrowthWeights != null) hero.GrowthWeights = definition.GrowthWeights;
                if (definition.WeakGrowth != null) hero.WeakGrowth = (StatType[])definition.WeakGrowth.Clone();
                hero.Job = definition.Job;
                hero.Role = definition.Role;
                if (definition.Passives != null) hero.StartingPassives.AddRange(definition.Passives);
                hero.Rebuild(rules);
                if (definition.Patterns != null) hero.StartingPatterns.AddRange(definition.Patterns);
                if (definition.ActiveSkills != null) hero.StartingActives.AddRange(definition.ActiveSkills);
                hero.Rebuild(rules);
                return hero;
            }
        }

        // A floor with an existing party (recruited, or carried down from the floor above): everyone starts rested.
        public SoulDungeonSession(SoulDungeonData dungeon, SoulStatRules rules, IEnumerable<SoulMercenary> party, int seed, int floor, int preservationItems = 1, SoulDungeonDay day = null)
        {
            if (dungeon == null) throw new ArgumentNullException(nameof(dungeon));
            this.dungeon = dungeon; this.rules = rules; random = new System.Random(seed);
            // A side of the map of its own (one of the four ways out of the start, give or take 25°), from its own
            // roll so the session's other draws stay as they were.
            var lean = new System.Random(seed ^ 0x5EA7);
            float angle = (lean.Next(4) * 90 + (float)lean.NextDouble() * 50 - 25) * Mathf.Deg2Rad;
            bearing = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Floor = Mathf.Clamp(floor, 1, FinalFloor);
            // The floor is shared with every party on it that day (SoulSessionFloor): the first one builds it
            // (a procedural dungeon: a new map, the seed decides it), the others join it.
            Day = day;
            Shared = day != null ? day.FloorAt(Floor) : new SharedFloor();
            bool fresh = Shared.Layout == null;
            Layout = !fresh ? Shared.Layout : dungeon.Procedural ? SoulDungeonGenerator.Generate(dungeon, seed, Floor) : SoulDungeonGenerator.FromData(dungeon);
            Map = fresh ? new SoulMap(Layout.Rows) : Shared.Map;
            if (!fresh)
            {
                Monsters = Shared.Monsters; Chests = Shared.Chests; Traps = Shared.Traps; HiddenStages = Shared.HiddenStages; Interactables = Shared.Interactables;
                routes = Shared.Routes; rewarded = Shared.Rewarded; killers = Shared.Killers; provoked = Shared.Provoked;
                telegraphTurn = Shared.TelegraphTurn; telegraphRest = Shared.TelegraphRest;
            }
            else Shared.Adopt(this);
            exits.AddRange(Layout.Exits);
            PartyStart = Layout.Start;
            Arrival = PartyStart; // every party starts at the entrance, by the escape portal
            PreservationItems = preservationItems;
            var taken = new HashSet<Vector2Int>();
            foreach (var hero in party)
            {
                hero.TickTimedBuffs(float.MaxValue);
                hero.Statuses.Clear();
                hero.SkillCooldowns.Clear();
                hero.Cooldown = hero.MoveTime = 0;
                hero.CurrentTarget = null;
                hero.Action = hero.ShownAction = null;
                hero.Rebuild(rules);
                hero.DefyUsed = false;
                SoulFatigue.Refresh(hero);
                hero.Hp = hero.Stats.Total(StatType.MaxHp); // rested for the new floor (the dead do not come this far)
                hero.Mp = hero.Stats.Total(StatType.MaxMp);
                hero.Stamina = hero.Stats.Total(StatType.MaxStamina);
                hero.Position = Map.Center(SpawnCell(Arrival, hero.Stats.Radius, taken));
                Mercenaries.Add(hero);
                destinations[hero] = hero.Position;
                routes[hero] = new RouteState();
            }
            for (int i = 0; fresh && i < Layout.Spawns.Count; i++)
            {
                var spawn = Layout.Spawns[i];
                if (spawn.Monster == null) continue;
                var monster = new SoulMonster(spawn.Monster, Map.Center(spawn.Cell), rules, spawn.Monster.Id + ":" + i, FloorPower(Floor));
                monster.Position = Map.Center(SpawnCell(spawn.Cell, monster.Stats.Radius, taken));
                monster.Group = spawn.Group;
                if (spawn.Facing.sqrMagnitude > 1e-4f) monster.Facing = spawn.Facing.normalized;
                Monsters.Add(monster);
                routes[monster] = new RouteState();
            }
            if (fresh)
                foreach (var chest in Layout.Chests)
                Chests.Add(new SoulChest
                {
                    Position = Map.Center(chest.Cell), Contents = RollLoot(Mathf.Max(1, chest.Items), chest.Hidden ? SoulDropSource.HiddenChest : SoulDropSource.Chest), Hidden = chest.Hidden,
                    Book = chest.Hidden ? RollBook(SoulDropSource.HiddenChest) : null, Supply = chest.Hidden ? RollScroll(SoulDropSource.HiddenChest) : null,
                    InStage = Layout.HiddenStages.Exists(stage => stage.Rect.Contains(chest.Cell))
                });
            if (fresh)
                foreach (var (cell, kind) in Layout.Traps)
                    Traps.Add(new SoulTrap { Position = Map.Center(cell), Kind = kind, Target = Layout.TrapTargets.TryGetValue(cell, out var to) ? Map.Center(to) : Map.Center(cell) });
            if (fresh) foreach (var stage in Layout.HiddenStages) HiddenStages.Add(new SoulHiddenStage { Kind = stage.Kind, Rect = stage.Rect, Door = stage.Door });
            if (fresh)
                foreach (var thing in Layout.Objects)
                Interactables.Add(new SoulInteractable
                {
                    Kind = thing.Kind, Position = Map.Center(thing.Cell), WarpKind = thing.WarpKind, WarpTarget = Map.Center(thing.WarpTarget),
                    WarpDoor = thing.WarpDoor, Mimic = thing.Mimic
                });
            if (fresh) Interactables.Add(new SoulInteractable { Kind = SoulObjectKind.Escape, Position = Map.Center(PartyStart) + new Vector2(0, -1.2f), Insight = true });
            RevealByLuck();
            Explored = new bool[Map.Width * Map.Height];
            Seen = new bool[Map.Width * Map.Height];
            foreach (var hero in Mercenaries) Reveal(hero);
            foreach (var hero in Mercenaries) Raise(hero, SoulTrigger.BattleStart, null);
            if (fresh) foreach (var monster in Monsters) Raise(monster, SoulTrigger.BattleStart, null);
            Shared.Parties.Add(this);
            // Every party of the day rolls the same seed, so they all rolled the same way: each one on this floor is
            // turned from it by the order it came in (the first as rolled, the second the opposite way, then the sides).
            int order = Shared.Parties.Count - 1;
            if (order > 0)
            {
                float turn = PartyTurns[order % PartyTurns.Length] * Mathf.Deg2Rad;
                bearing = new Vector2(bearing.x * Mathf.Cos(turn) - bearing.y * Mathf.Sin(turn), bearing.x * Mathf.Sin(turn) + bearing.y * Mathf.Cos(turn));
            }
        }

        // Nearest open, unoccupied cell the unit fits in and can walk from the origin, searched ring by ring.
        // Party members spread around the start instead of being dropped into walls.
        Vector2Int SpawnCell(Vector2Int origin, float radius, HashSet<Vector2Int> taken)
        {
            for (int ring = 0; ring < Mathf.Max(Map.Width, Map.Height); ring++)
                for (int dy = -ring; dy <= ring; dy++) for (int dx = -ring; dx <= ring; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue;
                    var cell = origin + new Vector2Int(dx, dy);
                    if (taken.Contains(cell) || !Map.Open(cell) || !Map.Clear(Map.Center(cell), radius)) continue;
                    if (Map.Open(origin) && cell != origin && !Map.Reachable(Map.Center(origin), Map.Center(cell), radius)) continue;
                    taken.Add(cell);
                    return cell;
                }
            return origin;
        }

        // Tile order (minimap clicks, auto exploration): the tile center, or the nearest reachable point.
        public bool SetDestination(SoulMercenary hero, Vector2Int requested, out Vector2Int actual, bool playerOrder = true)
        {
            bool set = SetDestination(hero, Map.Center(requested), out Vector2 point, playerOrder);
            actual = set ? Map.Cell(point) : new Vector2Int(-1, -1);
            return set;
        }

        // Point order on the fine navigation grid.
        public bool SetDestination(SoulMercenary hero, Vector2 requested, out Vector2 actual, bool playerOrder = true)
        {
            actual = requested;
            if (hero == null || !hero.Alive || !destinations.ContainsKey(hero)) return false;
            if (!Map.ClosestReachable(requested, hero.Position, hero.Stats.Radius, out actual)) return false;
            destinations[hero] = actual;
            routes[hero].Valid = false;
            if (playerOrder) campCall = null; // the player sends it elsewhere: no camp
            if (playerOrder && !Arrived(hero)) ordered.Add(hero);
            return true;
        }

        public const float ArriveDistance = .3f;
        bool Arrived(SoulMercenary hero) => Vector2.Distance(hero.Position, destinations[hero]) <= ArriveDistance;

        // A player move order wins over auto-engagement until the mercenary arrives (design 10.1/10.2:
        // the player only gives destinations; fighting resumes by itself once there).
        readonly HashSet<SoulMercenary> ordered = new HashSet<SoulMercenary>();
        readonly HashSet<SoulMercenary> stepNext = new HashSet<SoulMercenary>();
        readonly HashSet<SoulCombatant> retreatNext = new HashSet<SoulCombatant>();

        // ── party assist ─────────────────────────────────────────

        public const float AssistRadius = 10f;

        // Enemy the party is already fighting (or that is attacking the party) nearest to this hero.
        // Melee sight is short (3.5): without this, a ranged ally fights alone while the others wait.
        // ── a fight that goes nowhere ──
        // Mercenaries "fighting" what none of them can reach (across a gap, stuck in a wall pocket — and each counting
        // the fight because another aims at it) used to stand there until they were worn out: no blow lands on either
        // side for StallTime seconds, and what they were after is left alone for IgnoreTime.
        public const float StallTime = 15f, IgnoreTime = 60f;
        readonly Dictionary<SoulMonster, float> ignoredUntil = new Dictionary<SoulMonster, float>();
        float stalledFight, lastFoeHp = -1, lastPartyHp = -1;

        // left alone — unless it comes within reach of someone it is after (then it is a fight again)
        bool Ignored(SoulMonster monster) => ignoredUntil.TryGetValue(monster, out float until) && clock < until
            && !(monster.CurrentTarget is SoulMercenary prey && Vector2.Distance(monster.Position, prey.Position) <= AttackReach(monster) + 1f);

        void WatchStalledFight(float dt)
        {
            float foes = 0, party = 0;
            foreach (var monster in nearby) if (monster.Alive) foes += monster.Hp;
            foreach (var hero in Mercenaries) if (hero.Alive) party += hero.Hp;
            bool progress = foes < lastFoeHp - .01f || party < lastPartyHp - .01f; // a blow landed somewhere
            lastFoeHp = foes; lastPartyHp = party;
            // (on the way home the mercenaries hold their blows on purpose: that is no stalled fight)
            if (!partyFighting || progress || Retreating) { stalledFight = 0; return; }
            stalledFight += dt;
            if (stalledFight < StallTime) return;
            stalledFight = 0;
            int left = 0;
            foreach (var hero in Mercenaries)
            {
                if (!hero.Alive) continue;
                var foe = Nearest(hero, Perception(hero)) ?? AssistTarget(hero);
                if (foe == null || Ignored(foe)) continue;
                ignoredUntil[foe] = clock + IgnoreTime;
                left++;
            }
            if (left > 0) Log("닿지 않는 적은 두고 움직입니다");
            partyFighting = PartyInCombat();
        }

        SoulMonster AssistTarget(SoulMercenary hero)
        {
            SoulMonster best = null;
            float bestDistance = AssistRadius;
            foreach (var monster in nearby)
            {
                if (!monster.Alive || Ignored(monster)) continue;
                bool fought = monster.CurrentTarget is SoulMercenary;
                if (!fought) foreach (var ally in Mercenaries) fought |= ally.Alive && ally != hero && ally.CurrentTarget == monster;
                if (!fought) continue;
                float distance = Vector2.Distance(hero.Position, monster.Position);
                if (distance < bestDistance) { bestDistance = distance; best = monster; }
            }
            return best;
        }

        bool PartyInCombat()
        {
            foreach (var hero in Mercenaries)
                if (hero.Alive && (Nearest(hero, Perception(hero)) != null || AssistTarget(hero) != null)) return true;
            return false;
        }

        // ── cell claims ──────────────────────────────────────────

        // Each tick every mercenary claims the spot it heads for, so two never aim at the same place
        // (two melee units squeezing into one spot used to push each other forever). Spots are free points;
        // a claim keeps other bodies about a body-width away.
        readonly List<(Vector2 point, SoulCombatant unit)> claims = new List<(Vector2, SoulCombatant)>();
        static readonly Vector2[] ClaimOffsets = BuildClaimOffsets();

        static Vector2[] BuildClaimOffsets()
        {
            var offsets = new List<Vector2> { Vector2.zero };
            foreach (float ring in new[] { .45f, .9f })
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4 + (ring > .5f ? Mathf.PI / 8 : 0);
                    offsets.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring);
                }
            return offsets.ToArray();
        }

        Vector2 Claim(SoulCombatant unit, Vector2 preferred)
        {
            if (!(unit is SoulMercenary)) return preferred;
            Vector2 best = preferred;
            float bestScore = float.MaxValue;
            foreach (var offset in ClaimOffsets)
            {
                var point = preferred + offset;
                if (!Map.Clear(point, unit.Stats.Radius)) continue;
                bool taken = false;
                foreach (var claim in claims)
                    taken |= claim.unit != unit && Vector2.Distance(claim.point, point) < (claim.unit.Stats.Radius + unit.Stats.Radius) * .9f;
                if (taken) continue;
                float score = Vector2.Distance(point, unit.Position) + (offset == Vector2.zero ? -.3f : 0);
                if (score < bestScore) { bestScore = score; best = point; }
            }
            claims.Add((best, unit));
            return best;
        }

        // ── leader ───────────────────────────────────────────────

        public const float TetherRadius = 5f, EngageRadius = 7f;
        SoulMercenary leader;

        // The party leader walks in front; the others keep to slots behind it. Default: the tank.
        public SoulMercenary Leader
        {
            get
            {
                if (leader == null || !leader.Alive)
                    leader = Mercenaries.Find(h => h.Alive && h.Role == SoulRole.Tank) ?? Mercenaries.Find(h => h.Alive);
                return leader;
            }
        }

        public void SetLeader(SoulMercenary hero)
        {
            if (hero == null || !hero.Alive || !Mercenaries.Contains(hero)) return;
            leader = hero;
            Log(hero.Name + " 리더 지정");
        }

        // The one furthest behind the leader beyond PaceRadius (null: the party is together).
        public const float PaceRadius = 4.5f, PaceWaitLimit = 12f, PaceWalkOn = 8f;
        float paceWait;
        SoulMercenary Straggler(SoulMercenary lead)
        {
            SoulMercenary worst = null;
            float far = PaceRadius;
            foreach (var hero in Mercenaries)
            {
                if (hero == lead || !hero.Alive || ordered.Contains(hero)) continue;
                float d = Vector2.Distance(hero.Position, lead.Position);
                if (d > far) { far = d; worst = hero; }
            }
            return worst;
        }

        // Leader on its way somewhere and not stopping to fight. A melee/tank leader under an order walks
        // past enemies; a ranged/support leader stops to fight first (see Act).
        bool LeaderTravelling()
        {
            var lead = Leader;
            if (lead == null || Arrived(lead)) return false;
            if (ordered.Contains(lead))
                return !((lead.Role == SoulRole.Ranged || lead.Role == SoulRole.Support) && lead.CurrentTarget != null);
            return lead.CurrentTarget == null;
        }

        // Slot behind the leader (opposite its heading): front liners close, ranged/support further back.
        Vector2 FollowPoint(SoulMercenary hero)
        {
            var lead = Leader;
            Vector2 heading = destinations[lead] - lead.Position;
            heading = heading.sqrMagnitude < .01f ? Vector2.down : heading.normalized;
            Vector2 side = new Vector2(-heading.y, heading.x);
            int slot = 0;
            foreach (var other in Mercenaries) { if (other == hero) break; if (other != lead && other.Alive) slot++; }
            float back = hero.Role == SoulRole.Ranged || hero.Role == SoulRole.Support ? 2.2f : 1.2f;
            float lateral = (slot % 2 == 0 ? 1 : -1) * (.6f + .5f * (slot / 2));
            var spot = lead.Position - heading * back + side * lateral;
            return Claim(hero, Map.Clear(spot, hero.Stats.Radius) && Map.Sight(lead.Position, spot) ? spot : lead.Position);
        }

        // ── attack modifiers ─────────────────────────────────────

        // Flank: an enemy busy with someone else takes +25% from a unit that moves in flanks.
        float FlankBonus(SoulCombatant attacker, SoulCombatant target)
            => target != null && MovementPattern(attacker)?.MoveStyle == SoulMoveStyle.Flank && target.CurrentTarget != null && target.CurrentTarget != attacker ? 1.25f : 1f;

        float AttackScale(SoulCombatant attacker, SoulCombatant target, AttackPlan plan)
            => (plan.Basic ? BasicAttackScale : 1f) * FlankBonus(attacker, target) * (plan.Pattern.Ambush ? plan.Pattern.BackstabMultiplier : 1f);

        public bool HasMoveOrder(SoulMercenary hero) => ordered.Contains(hero);

        public Vector2Int Destination(SoulMercenary hero) => Map.Cell(destinations[hero]);
        public Vector2 DestinationPoint(SoulMercenary hero) => destinations[hero];
        public bool HasArrived(SoulMercenary hero) => Arrived(hero);

        public IReadOnlyList<Vector2> Route(SoulMercenary hero) => routes[hero].Path.Points;

        public void Tick(float deltaTime)
        {
            if (Finished || Defeated || Recalled) return;
            float dt = Mathf.Clamp(deltaTime, 0, .05f);
            // Without a view draining the feed (batch validation) keep it bounded.
            if (CombatEvents.Count > 256) CombatEvents.RemoveRange(0, CombatEvents.Count - 256);
            int feed = CombatEvents.Count; // this tick's events are counted into the tally at the end
            bool host = Hosting;
            if (host) { Shared.RefreshHeroes(); clock += dt; }
            GatherNearby(host);
            partyFighting = PartyInCombat();
            WatchStalledFight(dt);
            // Stamina: rest > out of combat > fight. Camping restores on top of this (Rest).
            float staminaScale = partyFighting ? CombatStaminaRegen : Resting || Camping > 0 ? RestStaminaRegen : 1f;
            foreach (var hero in Mercenaries) hero.StaminaRegenScale = staminaScale;
            // MP trickles back in a fight; once it is quiet (RestDelay) the mage gathers it CalmManaRegen times as fast
            foreach (var hero in Mercenaries) hero.ManaRegenScale = !partyFighting && calm >= RestDelay ? CalmManaRegen : 1f;
            if (!partyFighting) foreach (var hero in Mercenaries) hero.Focus = 0; // 주문 집중 does not outlast the fight
            foreach (var hero in Mercenaries) SoulCombat.TickResources(hero, dt);
            UseBelt();
            if (host) foreach (var monster in nearby) SoulCombat.TickResources(monster, dt); // far away nothing happens to them
            int pending = triggers.Count;
            for (int i = 0; i < pending; i++)
            {
                var evt = triggers.Dequeue();
                if (!TryTriggeredSkill(evt)) triggers.Enqueue(evt);
            }
            claims.Clear();
            foreach (var hero in Mercenaries) if (hero.Alive) Act(hero, Nearest(hero, Perception(hero)) ?? AssistTarget(hero), dt);
            if (host)
                foreach (var monster in nearby)
                {
                    if (!monster.Alive || GoingBack(monster, dt)) continue;
                    var was = monster.Position;
                    Act(monster, Leash(monster, Threatened(monster, Perception(monster)) ?? PackTarget(monster) ?? Provoked(monster)), dt);
                    CountGround(monster, was);
                }
            TickTelegraphs(dt);
            TickFields(dt);
            TickAuras(dt);
            Rest(dt);
            TickPlan(dt);
            TickSupplies(dt);
            TickHidden();
            TickInteraction(dt);
            foreach (var hero in Mercenaries) if (hero.Alive) Reveal(hero);
            Separate();
            ShareGear();
            AutoExploreStep(dt);
            foreach (var monster in Monsters) if (!monster.Alive && OwnsKill(monster) && rewarded.Add(monster.CombatId)) Reward(monster);
            OpenChests();
            foreach (var hero in Mercenaries)
                if (!hero.Alive && fallen.Add(hero))
                {
                    Emit(SoulEventKind.Death, null, hero);
                    Log(hero.Name + " 쓰러짐");
                    foreach (var other in Mercenaries) if (other.Alive) witnessedDeath.Add(other);
                }
            if (AutoControl && Crisis() is string crisis) BeginRetreat(crisis, crisisOdds);
            TickRetreat(dt);
            TickAvoidance(dt);
            if (Mercenaries.TrueForAll(hero => !hero.Alive)) { Defeated = true; Log("용병단이 전멸했습니다."); }
            else if (ExitOpen && Mercenaries.Exists(Leaving)) Finish();
            SettleActions(dt);
            SettlePoses();
            for (int i = feed; i < CombatEvents.Count; i++) SoulTripTally.Count(CombatEvents[i], TallyOf);
        }

        // The tally of the party a mercenary on this floor belongs to.
        SoulTripTally TallyOf(SoulMercenary hero)
        {
            if (Mercenaries.Contains(hero)) return Tally;
            foreach (var party in Shared.Parties) if (party.Mercenaries.Contains(hero)) return party.Tally;
            return null;
        }

        // Large maps hold hundreds of monsters; only those near a living mercenary act, collide or get
        // considered as targets. The rest wait where they are (nothing out there can reach them anyway).
        public const float ActiveRadius = 18f;
        readonly List<SoulMonster> nearby = new List<SoulMonster>();

        // The host gathers around every party on the floor (it moves those monsters), the others around their own.
        void GatherNearby(bool host)
        {
            nearby.Clear();
            var around = host ? Shared.Heroes : Mercenaries;
            foreach (var monster in Monsters)
            {
                if (!monster.Alive) continue;
                foreach (var hero in around)
                    if (hero.Alive && (hero.Position - monster.Position).sqrMagnitude < ActiveRadius * ActiveRadius) { nearby.Add(monster); break; }
            }
        }

        // ── rest ─────────────────────────────────────────────────

        // Out of combat for a few seconds the party catches its breath: 4% of max HP a second. Auto exploration
        // waits for it when someone is below half health, until everyone is back to 90%.
        public const float RestDelay = 3f, RestRate = .04f, RestBelow = .5f, RestUntil = .9f, CalmManaRegen = 3f;
        float calm;
        public bool Resting { get; private set; }

        void Rest(float dt)
        {
            bool fighting = PartyInCombat();
            if (Camping > 0)
            {
                if (fighting) { Camping = 0; Log("야영이 중단되었습니다"); }
                else
                {
                    Camping -= dt;
                    if (Camping <= 0) EndCamp();
                    foreach (var hero in Mercenaries)
                    {
                        if (!hero.Alive) continue;
                        hero.Hp = Mathf.Min(hero.Stats.Total(StatType.MaxHp), hero.Hp + hero.Stats.Total(StatType.MaxHp) * CampRate * dt);
                        hero.Mp = Mathf.Min(hero.Stats.Total(StatType.MaxMp), hero.Mp + hero.Stats.Total(StatType.MaxMp) * CampRate * dt);
                        hero.Stamina = Mathf.Min(hero.Stats.Total(StatType.MaxStamina), hero.Stamina + hero.Stats.Total(StatType.MaxStamina) * CampRate * dt);
                        hero.Action = "야영";
                    }
                }
            }
            calm = fighting ? 0 : calm + dt;
            if (calm < RestDelay) return;
            foreach (var hero in Mercenaries)
                if (hero.Alive && hero.Regenerates) hero.Hp = Mathf.Min(hero.Stats.Total(StatType.MaxHp), hero.Hp + hero.Stats.Total(StatType.MaxHp) * RestRate * dt);
        }

        static float Health(SoulCombatant unit) => unit.Hp / Mathf.Max(1, unit.Stats.Total(StatType.MaxHp));
        static float Breath(SoulCombatant unit) => unit.Stamina / Mathf.Max(1, unit.Stats.Total(StatType.MaxStamina));
        public const float BreathBelow = .25f, BreathUntil = .8f;

        // ── hidden stages and traps ──────────────────────────────

        void TickHidden()
        {
            foreach (var stage in HiddenStages)
            {
                if (stage.Found || !Map.IsDiscovered(stage.Door)) continue;
                stage.Found = stage.Revealed = true;
                HiddenFound++;
                Log("히든 스테이지 발견! — " + (stage.Kind == SoulHiddenStageKind.Vault ? "보물 창고" : stage.Kind == SoulHiddenStageKind.Traps ? "함정과 몬스터" : "보스가 기다립니다"));
            }
            foreach (var trap in Traps)
            {
                if (trap.Found || trap.Sprung) continue;
                foreach (var hero in Mercenaries)
                {
                    if (!hero.Alive) continue;
                    float distance = Vector2.Distance(hero.Position, trap.Position);
                    // A secret-finder (pathfinder) spots it in reach and disarms it; anyone else just steps on it.
                    if ((hero.MapTraits & SoulMapTrait.DetectHiddenDoors) != 0 && distance <= DetectRadius(hero) && Map.Sight(hero.Position, trap.Position))
                    {
                        trap.Found = true;
                        Log(hero.Name + ": 함정 발견 — 해제했습니다");
                        Emit(SoulEventKind.TrapFound, hero, null);
                        break;
                    }
                    if (distance <= .45f + hero.Stats.Radius * .5f) { SpringTrap(trap, hero); break; }
                }
                if (trap.Sprung && trap.Kind == SoulTrapKind.Warp) return; // the party is elsewhere now
            }
        }

        void SpringTrap(SoulTrap trap, SoulMercenary hero)
        {
            trap.Sprung = true;
            if (trap.Kind == SoulTrapKind.Warp)
            {
                // 강제 이동: the whole party is thrown somewhere it would not have gone (an elite, a horde, the boss)
                Teleport(trap.Target);
                Warped = true;
                Emit(SoulEventKind.Trap, null, hero);
                Log(hero.Name + ": 강제 이동 함정! — 파티가 어딘가로 끌려갔습니다");
                return;
            }
            float max = hero.Stats.Total(StatType.MaxHp);
            float damage = max * (trap.Kind == SoulTrapKind.Spikes ? .25f : .1f);
            hero.Hp = Mathf.Max(0, hero.Hp - damage);
            CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Hit, Target = hero, Amount = damage });
            SoulCombat.ApplyStatus(hero, trap.Kind == SoulTrapKind.Spikes ? SoulStatus.Bleed : SoulStatus.Poison, 1, 5, max * .02f, random);
            Emit(SoulEventKind.Trap, null, hero);
            Log(hero.Name + ": 함정! (" + (trap.Kind == SoulTrapKind.Spikes ? "가시" : "독침") + ")");
        }

        // ── usable objects ───────────────────────────────────────

        public const float AltarBoost = 1.5f, AltarTime = 180, BlessingTime = 120, CampTime = 4, CampCooldown = 90,
            CampRate = .25f, WatchtowerRadius = 28, FortuneCooldown = 45;
        public float ExperienceBoost { get; private set; } // seconds left of the altar's bonus
        public float Camping { get; private set; }
        public int FortuneCost => 25 * Floor;
        public bool CanUse(SoulInteractable thing) => thing != null && !thing.Used && thing.Cooldown <= 0;

        // A click on an object works when a mercenary is near it (InteractReach): most are used at once; a campsite
        // calls the party over — they walk there, sit down around it, and the camp begins once they are gathered.
        public const float InteractReach = 12f, CampGatherTime = 30f, CampSeatReach = 2.2f;
        SoulInteractable campCall;
        float campCallTime;
        bool campAfterFight; // clicked during a fight: the party gathers once it is over
        public bool GoingToCamp => campCall != null;

        void CancelCampCall() { campCall = null; campAfterFight = false; }

        public bool AllyNear(SoulInteractable thing) => Mercenaries.Exists(h => h.Alive && Vector2.Distance(h.Position, thing.Position) <= InteractReach);

        // Why a click on it does nothing now (null: it works).
        public string InteractBlock(SoulInteractable thing)
        {
            if (thing == null || !Knows(thing)) return "지금은 사용할 수 없습니다";
            if (!CanUse(thing)) return thing.Used ? "이미 사용했습니다" : $"{thing.Cooldown:0}초 뒤 다시 쓸 수 있습니다";
            if (!AllyNear(thing)) return "근처에 아군이 1명 이상 있어야 합니다";
            return null;
        }

        public bool Interact(SoulInteractable thing, SoulMercenary hero)
        {
            if (InteractBlock(thing) != null || hero == null || !hero.Alive) return false;
            if (thing.Kind == SoulObjectKind.Campfire)
            {
                campCall = thing;
                campCallTime = 0;
                campAfterFight = PartyInCombat();
                if (campAfterFight) Log("전투를 끝내고 야영지로 모입니다");
                else { GatherAround(thing.Position, false); Log("야영지로 모입니다"); }
                return true;
            }
            Use(thing, hero);
            return true;
        }

        // The party on its way to a campsite: the camp begins when everyone sits around it (or, after CampGatherTime,
        // with whoever made it). A fight comes first — the party finishes it, then gathers; a player's order calls it off.
        void TickCampCall(float dt)
        {
            if (campCall == null) return;
            if (!CanUse(campCall)) { campCall = null; return; }
            if (PartyInCombat()) { campAfterFight = true; return; }
            if (campAfterFight)
            {
                campAfterFight = false;
                campCallTime = 0;
                GatherAround(campCall.Position, false);
                Log("전투가 끝났습니다 — 야영지로 모입니다");
            }
            campCallTime += dt;
            var fire = campCall;
            bool gathered = Mercenaries.TrueForAll(h => !h.Alive || Vector2.Distance(h.Position, fire.Position) <= CampSeatReach || Arrived(h));
            if (!gathered && campCallTime < CampGatherTime) return;
            campCall = null;
            if (!AllyNear(fire)) { Log("야영지에 모이지 못했습니다"); return; }
            Use(fire, Leader);
        }

        void TickInteraction(float dt)
        {
            TickCampCall(dt);
            foreach (var each in Interactables) if (each.Cooldown > 0) each.Cooldown -= dt;
            if (ExperienceBoost > 0) ExperienceBoost = Mathf.Max(0, ExperienceBoost - dt);
        }

        public void Use(SoulInteractable thing, SoulMercenary hero)
        {
            if (!CanUse(thing)) return;
            string text;
            float power = FloorPower(Floor);
            switch (thing.Kind)
            {
                case SoulObjectKind.Spring:
                    foreach (var h in Mercenaries) if (h.Alive) { h.Hp = h.Stats.Total(StatType.MaxHp); h.Mp = h.Stats.Total(StatType.MaxMp); h.Stamina = h.Stats.Total(StatType.MaxStamina); h.Statuses.Clear(); }
                    thing.Used = true;
                    text = "회복의 샘: 파티 전원 회복";
                    break;
                case SoulObjectKind.Campfire:
                    Camping = CampTime;
                    campRelief = SoulFatigue.CampfireRelief;
                    thing.Cooldown = CampCooldown;
                    GatherAround(thing.Position, false);
                    text = "야영: 잠시 쉬며 모두 회복합니다";
                    break;
                case SoulObjectKind.Blessing:
                    foreach (var h in Mercenaries)
                        if (h.Alive) h.AddTimedBuff("dungeon_blessing", new[] { new SoulStatBonus { Stat = StatType.Attack, Value = 3 * power }, new SoulStatBonus { Stat = StatType.Armor, Value = 5 * power } }, BlessingTime);
                    thing.Used = true;
                    text = $"던전의 가호: {BlessingTime:0}초 동안 공격력·방어력 증가";
                    break;
                case SoulObjectKind.Altar:
                    ExperienceBoost = AltarTime;
                    thing.Used = true;
                    text = $"경험의 제단: {AltarTime:0}초 동안 경험치 ×{AltarBoost}";
                    break;
                case SoulObjectKind.Warp:
                    thing.Used = true;
                    text = Warp(thing);
                    break;
                case SoulObjectKind.Watchtower:
                    thing.Used = true;
                    RevealArea(thing.Position, WatchtowerRadius);
                    text = "전망대: 주변 지형이 드러났습니다";
                    break;
                case SoulObjectKind.Suspicious:
                    thing.Used = true;
                    if (thing.Mimic) { SpawnMimic(thing); text = "미믹이었다! 몬스터가 튀어나왔습니다"; }
                    else
                    {
                        var loot = RollLoot(1, SoulDropSource.Chest);
                        Inventory.AddRange(loot);
                        Tally.Items.AddRange(loot);
                        int purse = Mathf.RoundToInt(20 * FloorGold);
                        Gold += purse;
                        text = "수상한 상자: " + (loot.Length > 0 ? loot[0].Name + " + " : "") + $"금화 {purse}";
                    }
                    break;
                case SoulObjectKind.Escape:
                    string block = EscapeBlock(thing);
                    if (block != null) { Log("탈출 포탈: " + block); CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Interact, Actor = hero, Label = block }); return; }
                    Recall(scroll: false);
                    text = "탈출 포탈: 전리품을 들고 마을로 돌아갑니다";
                    break;
                default:
                    if (Gold < FortuneCost) { Log($"행운의 석상: 금화가 부족합니다 (필요 {FortuneCost})"); return; }
                    Gold -= FortuneCost;
                    thing.Cooldown = FortuneCooldown;
                    text = Fortune(thing);
                    break;
            }
            Log(text);
            CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Interact, Actor = hero, Label = text });
        }

        // The escape portal at the entrance takes the party home — but only everyone together, and not mid-fight.
        public const float EscapeRadius = 4f;

        public string EscapeBlock(SoulInteractable portal)
        {
            if (UnderAttack() && !EscapeOrdered) return "전투 중에는 쓸 수 없습니다"; // the player's 탈출: out, fight or not
            float reach = gatherGrace ? GatherReach : EscapeRadius; // a party stalled on its way in: those close by come too
            foreach (var hero in Mercenaries)
                if (hero.Alive && Vector2.Distance(hero.Position, portal.Position) > reach) return "파티 전원이 포탈 가까이 모여야 합니다";
            return null;
        }

        // Something is actually on the party: a monster after one of it within ChaseRadius, or any right beside one
        // (EngagedReach). A monster merely in sight — asleep, standing off — does not keep it from the way out.
        public const float EngagedReach = 2f;
        public bool UnderAttack()
        {
            foreach (var monster in nearby)
            {
                if (!monster.Alive) continue;
                bool after = monster.CurrentTarget is SoulMercenary prey && Mercenaries.Contains(prey);
                foreach (var hero in Mercenaries)
                {
                    if (!hero.Alive) continue;
                    float distance = Vector2.Distance(monster.Position, hero.Position);
                    if (distance <= EngagedReach || after && distance <= ChaseRadius) return true;
                }
            }
            return false;
        }

        // Pay gold, roll: heal (30%), experience (25%), blessing (25%) — or a curse (20%).
        // The fortune statue: something that shows at once — the party healed (the bars), gold (the purse), a piece
        // of gear (the bag), or monsters out of the ground (the curse).
        public const int FortunePayout = 3; // gold back: three times what was paid
        string Fortune(SoulInteractable thing)
        {
            double roll = random.NextDouble();
            if (roll < .3)
            {
                foreach (var h in Mercenaries) if (h.Alive) { h.Hp = h.Stats.Total(StatType.MaxHp); h.Stamina = h.Stats.Total(StatType.MaxStamina); }
                return "행운의 석상: 축복 — 파티 전원 회복";
            }
            if (roll < .55)
            {
                int gold = FortuneCost * FortunePayout;
                Gold += gold;
                return $"행운의 석상: 축복 — 금화 {gold}";
            }
            if (roll < .8)
            {
                var loot = RollLoot(1, SoulDropSource.Chest);
                if (loot.Length > 0)
                {
                    Inventory.AddRange(loot);
                    Tally.Items.AddRange(loot);
                    return $"행운의 석상: 축복 — {loot[0].Name} 획득";
                }
                int gold = FortuneCost * FortunePayout;
                Gold += gold;
                return $"행운의 석상: 축복 — 금화 {gold}";
            }
            SpawnMimic(thing);
            return "행운의 석상: 저주 — 몬스터가 튀어나왔습니다";
        }

        string Warp(SoulInteractable thing)
        {
            if (thing.WarpDoor.x >= 0 && Map.Discover(thing.WarpDoor))
            {
                int door = thing.WarpDoor.y * Map.Width + thing.WarpDoor.x;
                Explored[door] = true;
                ExploreLog.Add(door);
                Seen[door] = true;
                SeenLog.Add(door);
                ExploreRevision++;
            }
            Teleport(thing.WarpTarget);
            if (thing.WarpKind != SoulWarpKind.Treasure) Warped = true;
            return thing.WarpKind == SoulWarpKind.Horde ? "워프! — 대군집 한가운데로 떨어졌습니다"
                : thing.WarpKind == SoulWarpKind.Treasure ? "워프! — 숨겨진 보물 앞입니다" : "워프! — 보스의 경기장입니다";
        }

        // Thrown somewhere this floor (a warp stone into danger, a warp trap): a retreat from there that cannot be
        // walked safely waits it out instead (SoulSessionPlan, Hiding).
        public bool Warped { get; private set; }

        // The whole party lands around a point (in a little grid, each on a free spot).
        void Teleport(Vector2 target)
        {
            int slot = 0;
            foreach (var hero in Mercenaries)
            {
                if (!hero.Alive) continue;
                var wanted = target + new Vector2((slot % 3 - 1) * .8f, slot / 3 * .8f);
                slot++;
                if (!Map.ClosestReachable(wanted, target, hero.Stats.Radius, out var spot)) spot = target;
                hero.Position = spot;
                destinations[hero] = spot;
                routes[hero].Valid = false;
                ordered.Remove(hero);
            }
            autoGoal = new Vector2Int(-1, -1);
            retreatSteps.Clear();
        }

        void RevealArea(Vector2 center, float radius)
        {
            var origin = Map.Cell(center);
            int r = Mathf.CeilToInt(radius);
            for (int y = origin.y - r; y <= origin.y + r; y++)
                for (int x = origin.x - r; x <= origin.x + r; x++)
                {
                    if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height) continue;
                    if ((x - origin.x) * (x - origin.x) + (y - origin.y) * (y - origin.y) > radius * radius) continue;
                    int index = y * Map.Width + x;
                    if (!Seen[index]) { Seen[index] = true; SeenLog.Add(index); } // the dungeon view shows it too, not only the minimap
                    if (Explored[index]) continue;
                    Explored[index] = true;
                    ExploreLog.Add(index);
                }
            ExploreRevision++;
        }

        // A mimic: an elite and two lone monsters burst out around the chest, a little stronger than the floor.
        void SpawnMimic(SoulInteractable thing)
        {
            var kinds = new List<SoulMonsterData>();
            var roster = dungeon.Roster(Floor);
            if (roster.Elites.Length > 0) kinds.Add(roster.Elites[random.Next(roster.Elites.Length)]);
            for (int i = 0; i < 2 && roster.Singles.Length > 0; i++) kinds.Add(roster.Singles[random.Next(roster.Singles.Length)]);
            // A dungeon without those lists: whatever lives on this floor.
            if (kinds.Count == 0 && Monsters.Count > 0) for (int i = 0; i < 3; i++) kinds.Add(Monsters[random.Next(Monsters.Count)].Data);
            int group = 9000 + Monsters.Count;
            for (int i = 0; i < kinds.Count; i++)
            {
                var wanted = thing.Position + new Vector2(Mathf.Cos(i * 2.1f), Mathf.Sin(i * 2.1f)) * 1.2f;
                var monster = new SoulMonster(kinds[i], wanted, rules, kinds[i].Id + ":mimic" + Monsters.Count, FloorPower(Floor) * 1.2f) { Group = group };
                if (Map.ClosestReachable(wanted, thing.Position, monster.Stats.Radius, out var spot)) monster.Position = spot;
                Monsters.Add(monster);
                routes[monster] = new RouteState();
            }
        }

        // ── the chase has a limit ──
        // A monster follows its prey MonsterLeash cells from where the fight began; further, with nobody in reach,
        // it gives up and walks back there (GiveUpTime at most), paying no heed to anyone on the way.
        public const float MonsterLeash = 36f, GiveUpTime = 10f;
        readonly Dictionary<SoulMonster, Vector2> chaseAnchor = new Dictionary<SoulMonster, Vector2>();
        readonly Dictionary<SoulMonster, (Vector2 home, float until)> givingUp = new Dictionary<SoulMonster, (Vector2, float)>();
        public bool GivingUp(SoulMonster monster) => givingUp.ContainsKey(monster);

        SoulCombatant Leash(SoulMonster monster, SoulCombatant target)
        {
            if (target == null) { chaseAnchor.Remove(monster); return null; }
            if (!chaseAnchor.TryGetValue(monster, out var anchor)) { chaseAnchor[monster] = monster.Position; return target; }
            if (Vector2.Distance(monster.Position, anchor) <= MonsterLeash) return target;
            if (Vector2.Distance(monster.Position, target.Position) <= AttackReach(monster) + target.Stats.Radius + 1f) return target; // still in the fight
            givingUp[monster] = (anchor, clock + GiveUpTime);
            chaseAnchor.Remove(monster);
            provoked.Remove(monster);
            monster.CurrentTarget = null;
            return null;
        }

        bool GoingBack(SoulMonster monster, float dt)
        {
            if (!givingUp.TryGetValue(monster, out var back)) return false;
            if (clock >= back.until || Vector2.Distance(monster.Position, back.home) < .6f) { givingUp.Remove(monster); return false; }
            monster.CurrentTarget = null;
            if (!monster.Disabled) Navigate(monster, back.home, dt, 1f, "복귀", true);
            return true;
        }

        // One more monster of this floor's strength at a spot (tests, set pieces).
        public SoulMonster AddMonster(SoulMonsterData data, Vector2 at)
        {
            var monster = new SoulMonster(data, at, rules, data.Id + ":added" + Monsters.Count, FloorPower(Floor));
            Monsters.Add(monster);
            routes[monster] = new RouteState();
            return monster;
        }

        // ── packs ────────────────────────────────────────────────

        public const float PackAlertRadius = 16f;

        // Packs and hordes fight as one: a member with nothing in its own sight joins its group's fight.
        // A monster hit from beyond its own perception (a mage at range) still turns on its attacker.
        public const float ProvokeTime = 12f;
        readonly Dictionary<SoulCombatant, (SoulCombatant attacker, float at)> provoked = new Dictionary<SoulCombatant, (SoulCombatant, float)>();
        float clock { get => Shared.Clock; set => Shared.Clock = value; } // the floor's (the host keeps it)

        SoulCombatant Provoked(SoulMonster monster)
        {
            if (!provoked.TryGetValue(monster, out var entry)) return null;
            if (!entry.attacker.Alive || clock - entry.at > ProvokeTime || Vector2.Distance(monster.Position, entry.attacker.Position) > ActiveRadius)
            {
                provoked.Remove(monster);
                return null;
            }
            return entry.attacker;
        }

        SoulMercenary PackTarget(SoulMonster monster)
        {
            if (monster.Group == 0) return null;
            foreach (var mate in nearby)
                if (mate != monster && mate.Group == monster.Group && mate.Alive && mate.CurrentTarget is SoulMercenary hero && hero.Alive
                    && Vector2.Distance(monster.Position, hero.Position) < PackAlertRadius) return hero;
            return null;
        }

        // ── telegraphed patterns ─────────────────────────────────

        // The struck area stays a moment for the flash.
        public const float TelegraphLinger = .45f;

        // How long a unit needs to read an attack and start moving. This is the whole of evasion: an attack can
        // be evaded only when the unit reacts inside its wind-up, so Agility decides WHICH attacks are evadable
        // and the pattern decides how far the escape carries.
        //   Agility 2 (토르)  → 0.63s: only spins and boss patterns
        //   Agility 4 (리아)  → 0.46s: swung arcs (휘두르기 0.49s), not thrusts
        //   Agility 7 (카론)  → 0.21s: thrusts and arrows too — a fast bruiser slips basic attacks
        //   Agility 9+        → 0.10s: reacts to anything that has a wind-up at all
        public static float ReactionTime(SoulCombatant unit) => Mathf.Clamp(.8f - .085f * unit.Stats.Total(StatType.Agility), .1f, .8f);
        public readonly List<SoulTelegraph> Telegraphs = new List<SoulTelegraph>();
        readonly Dictionary<SoulCombatant, int> telegraphTurn = new Dictionary<SoulCombatant, int>();
        readonly Dictionary<SoulCombatant, float> telegraphRest = new Dictionary<SoulCombatant, float>();
        readonly Dictionary<SoulCombatant, Vector2> evadeSpot = new Dictionary<SoulCombatant, Vector2>();

        SoulTelegraph Casting(SoulCombatant unit)
        {
            foreach (var t in Telegraphs) if (t.Caster == unit && !t.Struck) return t;
            return null;
        }

        // Patterns go strictly in order (a boss is readable): the next one starts once its target is in range
        // and the rest after the previous hit is over; in between the caster fights normally.
        bool StartTelegraph(SoulMonster caster, SoulCombatant target)
        {
            var list = caster.Data.Telegraphs;
            if (list == null || list.Length == 0 || caster.Cooldown > 0) return false;
            if (telegraphRest.TryGetValue(caster, out float rest) && rest > 0) return false;
            telegraphTurn.TryGetValue(caster, out int turn);
            var data = list[turn % list.Length];
            if (data == null) { telegraphTurn[caster] = turn + 1; return false; }
            if (Vector2.Distance(caster.Position, target.Position) > data.TriggerRange + target.Stats.Radius) return false;
            var hit = new SoulHit
            {
                School = data.DamageSchool, Kind = data.DamageKind, Raw = data.Damage.Evaluate(caster.Stats.Combat),
                Knockback = data.Knockback, Melee = true
            };
            hit.SetStatus(data.Status, caster.Stats.Combat);
            Cast(caster, target, new SoulTelegraph
            {
                Data = data, Name = data.SkillName, Shape = data.Shape, Anchor = data.Anchor,
                Size = data.Size, Width = data.Width, WindUp = data.WindUp, Rest = data.Rest, Hit = hit
            });
            telegraphTurn[caster] = turn + 1;
            return true;
        }

        // Ordinary attacks are telegraphed too: the pattern's shape shows for its wind-up, then everything
        // inside is hit. A 찌르기 lane is one body wide, so it still reads as a single-target attack.
        void CastPattern(SoulCombatant caster, SoulCombatant target, SoulPatternData pattern, SoulHit hit, bool basic)
        {
            pattern.Area(out var shape, out var anchor, out float size, out float width);
            Cast(caster, target, new SoulTelegraph
            {
                Pattern = pattern, Name = basic ? "기본 공격" : pattern.Id, Shape = shape, Anchor = anchor,
                Size = size, Width = width, WindUp = pattern.WindUp(pattern.ActionTime / SoulCombat.ActionSpeed(caster)),
                Hit = hit, Hits = Mathf.Max(1, pattern.Hits), Basic = basic
            });
        }

        // Shared start: the area is pinned to where the caster and its target stand right now.
        void Cast(SoulCombatant caster, SoulCombatant target, SoulTelegraph telegraph)
        {
            Vector2 direction = target != null ? target.Position - caster.Position : caster.Facing;
            direction = direction.sqrMagnitude < 1e-4f ? caster.Facing : direction.normalized;
            caster.Facing = direction;
            telegraph.Caster = caster;
            telegraph.Target = target;
            telegraph.Direction = direction;
            telegraph.Hostile = caster is SoulMonster;
            telegraph.Origin = telegraph.Anchor == SoulAreaAnchor.Target && telegraph.Shape == SoulAreaShape.Circle && target != null
                ? target.Position : caster.Position;
            Telegraphs.Add(telegraph);
            caster.MoveTime = 0;
            caster.Action = telegraph.Name;
            Emit(SoulEventKind.Telegraph, caster, target, telegraph.Pattern, telegraph.Signature ? telegraph.Name : null);
        }

        void TickTelegraphs(float dt)
        {
            if (telegraphRest.Count > 0)
                foreach (var unit in new List<SoulCombatant>(telegraphRest.Keys)) telegraphRest[unit] = Mathf.Max(0, telegraphRest[unit] - dt);
            if (evadeRest.Count > 0)
                foreach (var unit in new List<SoulCombatant>(evadeRest.Keys)) evadeRest[unit] = Mathf.Max(0, evadeRest[unit] - dt);
            for (int i = Telegraphs.Count - 1; i >= 0; i--)
            {
                var t = Telegraphs[i];
                t.Elapsed += dt;
                if (!t.Struck && !t.Caster.Alive) { Telegraphs.RemoveAt(i); continue; }
                if (!t.Struck && t.Elapsed >= t.WindUp) Strike(t);
                if (t.Struck && t.Elapsed >= t.WindUp + TelegraphLinger) Telegraphs.RemoveAt(i);
            }
        }

        void Strike(SoulTelegraph t)
        {
            t.Struck = true;
            if (t.Skill != null) { StrikeSkill(t); return; }
            if (t.Signature)
            {
                telegraphRest[t.Caster] = t.Rest;
                t.Caster.Cooldown = Mathf.Max(t.Caster.Cooldown, .4f);
                CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Impact, Actor = t.Caster, Label = t.Name });
            }
            else CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Attack, Actor = t.Caster, Target = t.Target, Pattern = t.Pattern, Magic = t.Magic });
            if (t.FxSkill != null) Fx(t.Caster, t.Target, t.FxSkill, SoulFxPhase.Land, t.CircleCenter, t.Size, t.Direction);
            var victims = new List<SoulCombatant>();
            if (t.Caster is SoulMercenary) victims.AddRange(nearby); else victims.AddRange(Shared.Heroes);
            // A lane one body wide is stopped by the first body standing in it: the arrow lands in whoever is
            // in front and whoever stands behind them is covered by that body. Cones, circles and the wide box
            // cleaves of a boss pattern sweep through everyone they cover.
            if (t.Shape == SoulAreaShape.Box && t.Width <= SoulPatternData.SingleWidth * 1.3f)
            {
                SoulCombatant nearest = null;
                float best = float.MaxValue;
                foreach (var victim in victims)
                {
                    if (!victim.Alive || !t.Contains(victim.Position, victim.Stats.Radius)) continue;
                    float along = Vector2.Distance(victim.Position, t.Origin);
                    if (along < best) { best = along; nearest = victim; }
                }
                victims.Clear();
                if (nearest != null) victims.Add(nearest);
            }
            bool first = true;
            foreach (var victim in victims)
            {
                if (!victim.Alive || !t.Contains(victim.Position, victim.Stats.Radius)) continue;
                if (first && !t.Signature && FlankBonus(t.Caster, victim) > 1) Emit(SoulEventKind.Flank, t.Caster, victim);
                first = false;
                for (int strike = 0; strike < t.Hits && victim.Alive; strike++)
                {
                    var hit = t.Hit;
                    if (t.Hits > 1) hit.Raw *= .6f;
                    var outcome = SoulCombat.Resolve(t.Caster, victim, hit, random);
                    if (!outcome.Dodged && t.FxSkill != null && strike == 0) Fx(t.Caster, victim, t.FxSkill, SoulFxPhase.Victim, victim.Position);
                    if (!outcome.Dodged) SoulCombat.FirePassives(t.Caster, SoulTrigger.OnAttackLanded, victim);
                    if (!outcome.Dodged && !t.Basic && t.Pattern != null && !t.Signature) CountCombo(t.Caster, victim);
                    if (outcome.Killed) SoulCombat.FirePassives(t.Caster, SoulTrigger.OnKill, victim);
                    AfterHit(t.Caster, victim, outcome);
                }
            }
        }

        // Any area aimed at this unit's side that has been up at least `noticed` seconds (0: every area).
        // minWindUp filters to the slow, readable attacks — a quick swipe cannot be walked out of.
        bool Danger(SoulCombatant unit, Vector2 point, float pad, float noticed, float minWindUp = 0)
        {
            bool hero = unit is SoulMercenary;
            foreach (var t in Telegraphs)
                if (!t.Struck && t.Caster is SoulMercenary != hero && t.Elapsed >= noticed && t.WindUp >= minWindUp && t.Contains(point, pad))
                    return true;
            return false;
        }

        // Evasion is no longer a die roll. A unit that owns 구르기 or 회피 physically leaves the area that is
        // about to land on it: roll first (it covers more ground), dodge second. If neither can reach a spot
        // outside the area, the unit stands there and takes the hit.
        // Escaping on foot (no evasion pattern) needs a big slow area to walk out of; an evasion pattern only
        // needs the unit to have read the attack in time. A movement pattern built on disengaging (치고 빠지기,
        // 거리 유지) doubles as a short evasive step — less ground than a roll, but it is real evasion.
        public const float EvadeRest = .3f, ReadableWindUp = .6f, MoveEvadeStep = 1.1f;
        readonly Dictionary<SoulCombatant, float> evadeRest = new Dictionary<SoulCombatant, float>();

        // An enemy area this unit can still get out of. Two conditions, both from Agility:
        //   readable — the wind-up is at least as long as the unit's reaction time, or it never sees it coming;
        //   started  — the escape must begin a reaction time before impact to land clear of the area.
        // So a slow unit reacts late to a long boss wind-up and not at all to a thrust, while a very quick one
        // reads everything and leaves at the last moment.
        bool Incoming(SoulCombatant unit, Vector2 point, float pad)
        {
            bool hero = unit is SoulMercenary;
            float reaction = ReactionTime(unit);
            foreach (var t in Telegraphs)
                if (!t.Struck && t.Caster is SoulMercenary != hero && t.WindUp >= reaction
                    && t.Elapsed >= t.WindUp - reaction && t.Contains(point, pad)) return true;
            return false;
        }

        public static bool CanEvade(SoulCombatant unit) => !(unit is SoulMonster monster) || monster.Data.Evasive;

        bool TryEvade(SoulCombatant unit, float dt)
        {
            if (!CanEvade(unit)) return false; // 회피 본능 없는 몬스터: 맞고 버틴다
            float radius = unit.Stats.Radius;
            if (unit.Disabled) { evadeSpot.Remove(unit); return false; }
            if (!Incoming(unit, unit.Position, radius + .1f)) { evadeSpot.Remove(unit); return false; }
            if (!evadeRest.TryGetValue(unit, out float rest) || rest <= 0)
                // Roll first (it covers the most ground), then the sidestep, then a disengaging movement
                // pattern used as a short evasive step.
                foreach (var pattern in new[] { DefensePattern(unit, SoulDefenseMode.Roll), DefensePattern(unit, SoulDefenseMode.Dodge), EvasiveMovement(unit) })
                {
                    if (pattern == null) continue;
                    // overloaded (load over 160%): too heavy to dodge or roll
                    if (pattern.Category == SoulPatternCategory.Defense && unit.Stats.Total(StatType.LoadRatio) > SoulItemRules.OverLoad) continue;
                    bool rolling = pattern.DefenseMode == SoulDefenseMode.Roll && pattern.Category == SoulPatternCategory.Defense;
                    float cost = EvadeCost(unit, pattern, out float mana);
                    if (unit.Stamina < cost || unit.Mp < mana || !WorthEvading(unit, cost)) continue;
                    if (random.NextDouble() < SoulCombat.WoundFailChance(unit))
                    {
                        unit.Stamina -= cost;
                        evadeRest[unit] = Mathf.Max(EvadeRest, pattern.ActionTime / SoulCombat.ActionSpeed(unit));
                        unit.Action = "부상으로 회피 실패";
                        return false;
                    }
                    // Each pattern's own distance decides what it can clear: a sidestep leaves a narrow lane,
                    // a roll leaves a swung arc. Neither is scaled down here — the data says how far it goes.
                    float step = pattern.Category == SoulPatternCategory.Movement ? MoveEvadeStep : Mathf.Max(.5f, pattern.RollDistance);
                    if (!EscapeStep(unit, step, out Vector2 landing)) continue;
                    unit.Stamina -= cost;
                    unit.Mp -= mana;
                    evadeChain[unit] = (Chain(unit) + 1, clock);
                    // 회피 반격: out of the evasion straight into the next attack
                    var followup = SoulCombat.ChainPattern(unit, SoulChainTrigger.Evade);
                    if (followup != null) SoulCombat.OpenFollowup(unit, followup);
                    // Dodge-cancel: the swing this unit was winding up is dropped (its cost stays spent).
                    for (int i = Telegraphs.Count - 1; i >= 0; i--)
                        if (Telegraphs[i].Caster == unit && !Telegraphs[i].Struck) Telegraphs.RemoveAt(i);
                    unit.Position = landing;
                    unit.MoveTime = 0;
                    routes[unit].Valid = false;
                    // still rolling / stepping: no second evasion until the motion is over
                    evadeRest[unit] = Mathf.Max(EvadeRest, pattern.ActionTime / SoulCombat.ActionSpeed(unit));
                    evadeSpot.Remove(unit);
                    unit.Action = pattern.Id;
                    Emit(rolling ? SoulEventKind.Roll : SoulEventKind.Evade, null, unit);
                    var rollStrike = rolling ? SoulCombat.ChainPattern(unit, SoulChainTrigger.Roll) : null;
                    if (rollStrike != null) RollStrike(unit, rollStrike);
                    return true;
                }
            // Without an evasion pattern only the slow, well-signalled attacks can be walked out of.
            if (!Danger(unit, unit.Position, radius + .1f, ReactionTime(unit), ReadableWindUp)) { evadeSpot.Remove(unit); return false; }
            if (!evadeSpot.TryGetValue(unit, out var spot) || Danger(unit, spot, radius + .3f, 0))
            {
                if (!SafeSpot(unit, out spot)) { evadeSpot.Remove(unit); return false; }
                evadeSpot[unit] = spot;
            }
            Navigate(unit, spot, dt, 1.15f, "장판 회피", true);
            return true;
        }

        // 치고 빠지기 / 거리 유지: patterns whose whole job is breaking contact. Used well that is evasion, so
        // they buy a short step out of an incoming area — shorter than a roll, and only for a unit quick enough
        // to have read the attack in the first place.
        // 구르며 베기: the roll ends in a cut at everything within reach of where it lands.
        void RollStrike(SoulCombatant unit, SoulPatternData chain)
        {
            var stats = unit.Stats.Combat;
            foreach (SoulCombatant foe in unit is SoulMercenary ? (IEnumerable<SoulCombatant>)new List<SoulMonster>(nearby) : new List<SoulMercenary>(Shared.Heroes))
            {
                if (!foe.Alive || Vector2.Distance(foe.Position, unit.Position) > chain.StrikeRadius + foe.Stats.Radius) continue;
                var hit = new SoulHit { School = chain.DamageSchool, Kind = chain.DamageKind, Raw = chain.Damage.Evaluate(stats), Melee = true };
                var outcome = SoulCombat.Resolve(unit, foe, hit, random);
                Emit(SoulEventKind.Attack, unit, foe, chain);
                ReportHit(unit, foe, outcome);
            }
            unit.Action = chain.Id;
        }

        // 연타 마무리: landed hits on one target in a row; the Nth opens the follow-up.
        void CountCombo(SoulCombatant attacker, SoulCombatant victim)
        {
            var chain = SoulCombat.ChainPattern(attacker, SoulChainTrigger.Combo);
            if (chain == null) return;
            if (attacker.ComboTarget != victim) { attacker.ComboTarget = victim; attacker.ComboCount = 0; }
            if (++attacker.ComboCount < chain.ComboHits) return;
            attacker.ComboCount = 0;
            SoulCombat.OpenFollowup(attacker, chain);
        }

        // Evading again soon after the last one costs more each time (+50% a step within 3 s).
        public const float EvadeChainWindow = 3f, EvadeChainCost = .5f;
        // With plenty of HP and little stamina, a unit takes the hit rather than spend its last stamina;
        // below this much HP it evades whenever it can still pay.
        public const float EvadeStaminaReserve = .4f, DesperateHealth = .35f;
        readonly Dictionary<SoulCombatant, (int count, float at)> evadeChain = new Dictionary<SoulCombatant, (int, float)>();

        int Chain(SoulCombatant unit)
            => evadeChain.TryGetValue(unit, out var entry) && clock - entry.at <= EvadeChainWindow ? entry.count : 0;

        float EvadeCost(SoulCombatant unit, SoulPatternData pattern, out float mana)
        {
            SoulCombat.PatternCost(unit, pattern, out float stamina, out mana);
            // light gear (load up to 60%): evading is 10% cheaper
            if (unit is SoulMercenary && unit.Stats.Total(StatType.LoadRatio) <= SoulItemRules.LightLoad) stamina *= .9f;
            return stamina * (1 + EvadeChainCost * Chain(unit));
        }

        static bool WorthEvading(SoulCombatant unit, float cost)
            => Health(unit) < DesperateHealth || unit.Stamina - cost >= unit.Stats.Total(StatType.MaxStamina) * EvadeStaminaReserve;

        // Stamina regeneration by situation (share of the StaminaRegen stat).
        public const float CombatStaminaRegen = .35f, RestStaminaRegen = 2.5f;

        static SoulPatternData EvasiveMovement(SoulCombatant unit)
        {
            SoulPatternData best = null;
            foreach (var pattern in unit.Patterns())
                if (pattern != null && pattern.Category == SoulPatternCategory.Movement
                    && (pattern.MoveStyle == SoulMoveStyle.Kite || pattern.MoveStyle == SoulMoveStyle.KeepDistance)
                    && SoulCombat.CanPay(unit, pattern) && (best == null || pattern.Priority > best.Priority)) best = pattern;
            return best;
        }

        static SoulPatternData DefensePattern(SoulCombatant unit, SoulDefenseMode mode)
        {
            SoulPatternData best = null;
            foreach (var pattern in unit.Patterns())
                if (pattern != null && pattern.Category == SoulPatternCategory.Defense && pattern.DefenseMode == mode
                    && SoulCombat.CanPay(unit, pattern) && (best == null || pattern.Priority > best.Priority)) best = pattern;
            return best;
        }

        // One roll/sidestep: the reachable landing spot outside every incoming area that stays nearest the fight.
        bool EscapeStep(SoulCombatant unit, float distance, out Vector2 landing)
        {
            landing = unit.Position;
            float radius = unit.Stats.Radius, best = float.MaxValue;
            // a monster out of ground to give (GroundLeft) rolls aside or in, never further away
            float farthest = !MayGiveGround(unit) && unit.CurrentTarget != null ? Vector2.Distance(unit.Position, unit.CurrentTarget.Position) + .1f : float.MaxValue;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8;
                var point = unit.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if (Danger(unit, point, radius + .25f, 0) || !Map.Clear(point, radius) || !Map.Straight(unit.Position, point, radius)) continue;
                if (unit.CurrentTarget != null && Vector2.Distance(point, unit.CurrentTarget.Position) > farthest) continue;
                float score = unit.CurrentTarget != null ? Vector2.Distance(point, unit.CurrentTarget.Position) : 0;
                if (score < best) { best = score; landing = point; }
            }
            return best < float.MaxValue;
        }

        bool SafeSpot(SoulCombatant unit, out Vector2 spot)
        {
            spot = unit.Position;
            float radius = unit.Stats.Radius;
            for (float distance = .5f; distance <= 7f; distance += .5f)
            {
                float bestScore = float.MaxValue;
                bool found = false;
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI / 8;
                    var point = unit.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    if (Danger(unit, point, radius + .3f, 0) || !Map.Clear(point, radius) || !Map.Straight(unit.Position, point, radius)) continue;
                    // Equally near ways out: the one that keeps the unit closest to its fight.
                    float score = unit.CurrentTarget != null ? Vector2.Distance(point, unit.CurrentTarget.Position) : 0;
                    if (score < bestScore) { bestScore = score; spot = point; found = true; }
                }
                if (found) return true;
            }
            return false;
        }

        // On a portal that is its destination: walking across a portal while exploring does not end the stage.
        bool Leaving(SoulMercenary hero)
        {
            var cell = Map.Cell(hero.Position);
            return hero.Alive && IsExit(cell) && Map.Cell(destinations[hero]) == cell;
        }

        // UI labels: a new action shows only after the current one was up for a moment.
        public const float LabelHold = .6f;

        void SettleActions(float dt)
        {
            foreach (var hero in Mercenaries) Settle(hero, dt);
            foreach (var monster in nearby) Settle(monster, dt);
        }

        static void Settle(SoulCombatant unit, float dt)
        {
            unit.ShownFor += dt;
            if (unit.Action == unit.ShownAction) return;
            if (unit.ShownAction != null && unit.ShownFor < LabelHold) return;
            unit.ShownAction = unit.Action;
            unit.ShownFor = 0;
        }

        // ── treasure ─────────────────────────────────────────────

        public const float ChestReach = 1f;

        // A guardian leaves a chest: named boss loot plus one item rolled a floor deeper.
        void DropChest(SoulMonster boss)
        {
            var list = new List<SoulItem>();
            var uniques = new List<SoulEquipmentData>();
            if (dungeon.BossLoot != null) foreach (var item in dungeon.BossLoot) if (item != null) uniques.Add(item);
            for (int i = 0; i < Mathf.Max(1, dungeon.BossLootCount - 1) && uniques.Count > 0; i++)
            {
                int pick = random.Next(uniques.Count);
                list.Add(SoulItemRules.Drop(uniques[pick], Floor, SoulDropSource.Boss, random));
                uniques.RemoveAt(pick);
            }
            list.AddRange(RollLoot(1, SoulDropSource.Boss));
            var contents = list.ToArray();
            if (contents.Length == 0) return;
            var chest = new SoulChest { Position = boss.Position, Contents = contents, Book = RollBook(SoulDropSource.Boss), Supply = RollScroll(SoulDropSource.Boss) };
            if (!Map.Clear(chest.Position, .3f) && Map.ClosestReachable(boss.Position, boss.Position, .3f, out var spot)) chest.Position = spot;
            Chests.Add(chest);
            Log("보물 상자가 나타났습니다");
            CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Chest, Target = boss });
        }

        // Items from the dungeon's loot table: the grade follows the floor (deeper is better, floor 1 almost only
        // 조잡한/평범한), the source can lift it a floor, and only these carry special options (§9).
        SoulItem[] RollLoot(int count, SoulDropSource source)
        {
            var contents = new List<SoulItem>();
            var pool = dungeon.LootTable != null && dungeon.LootTable.Length > 0 ? dungeon.LootTable : dungeon.BossLoot;
            if (pool == null) return contents.ToArray();
            for (int i = 0; i < count; i++)
            {
                var item = SoulItemRules.Roll(pool, Floor, source, PartyBest(StatType.Luck), random);
                if (item != null) contents.Add(item);
            }
            return contents.ToArray();
        }

        // The best value among the living (luck, and the special options that only count once a party).
        public float PartyBest(StatType stat)
        {
            float best = 0;
            foreach (var hero in Mercenaries) if (hero.Alive) best = Mathf.Max(best, hero.Stats.Total(stat));
            return best;
        }

        // A defeated monster may drop an item: 3% a plain one, 35% an elite (행운의 raises it).
        void DropItem(SoulMonster monster)
        {
            if (monster.Data.Guardian) return; // the chest covers it
            if (monster.Data.Elite)
            {
                var book = RollBook(SoulDropSource.Elite);
                if (book != null)
                {
                    FoundBooks.Add(book);
                    Log($"{monster.Data.Name}: 스킬북 '{book.SkillName}' 획득 — 마을 서고에 등록할 수 있습니다");
                    CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Loot, Target = monster, Label = "스킬북: " + book.SkillName });
                }
            }
            var source = monster.Data.Elite ? SoulDropSource.Elite : SoulDropSource.Monster;
            if (random.NextDouble() >= SoulItemRules.DropChance(source) * (1 + PartyBest(StatType.LootFind))) return;
            var loot = RollLoot(1, source);
            if (loot.Length == 0) return;
            Inventory.AddRange(loot);
            Tally.Items.AddRange(loot);
            Log($"{monster.Data.Name}: {loot[0].Name} 획득");
            CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Loot, Target = monster, Label = loot[0].Name });
        }

        void OpenChests()
        {
            foreach (var chest in Chests)
            {
                if (chest.Opened) continue;
                var opener = Mercenaries.Find(hero => hero.Alive && Vector2.Distance(hero.Position, chest.Position) <= ChestReach);
                if (opener == null) continue;
                chest.Opened = true;
                ChestsOpened++;
                Inventory.AddRange(chest.Contents);
                Tally.Items.AddRange(chest.Contents);
                var names = new List<string>();
                foreach (var item in chest.Contents) names.Add(item.Name);
                if (chest.Book != null) { FoundBooks.Add(chest.Book); names.Add($"스킬북 '{chest.Book.SkillName}'"); }
                if (chest.Supply != null) { Pouch[chest.Supply] = Count(Pouch, chest.Supply) + 1; Tally.Supplies.Add(chest.Supply); names.Add(SoulSupplies.Get(chest.Supply).Name); }
                Log("보물 상자: " + string.Join(", ", names) + " 획득");
                CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Loot, Actor = opener, Label = string.Join(", ", names) });
            }
        }

        void Act(SoulCombatant actor, SoulCombatant target, float dt)
        {
            // An assist target may be out of sight: walk to it, but attack only what is actually seen.
            bool seen = target != null && Map.Sight(actor.Position, target.Position);
            if (seen && encounters.Add(actor.CombatId + ":" + target.CombatId))
            {
                Raise(actor, SoulTrigger.Encounter, target);
                if (actor is SoulMercenary scared && target is SoulMonster presence) Intimidate(scared, presence);
            }
            if (actor.Disabled)
            {
                actor.MoveTime = 0;
                var control = actor.Statuses.Find(s => SoulCombat.IsControl(s.Kind) && s.Kind != SoulStatus.Fear);
                actor.Action = actor.DownTime > 0 || control == null ? "다운" : SoulCombat.StatusName(control.Kind);
                return;
            }
            // Telegraphed patterns: a caster winding one up stands still; a mercenary inside a red area gets out
            // first (ahead of orders and attacks); a monster with patterns starts its next one when in range.
            if (TryEvade(actor, dt)) return;
            var winding = Casting(actor);
            if (winding != null) { actor.Action = winding.Name; return; }
            if (actor is SoulMonster caster && target != null && seen && StartTelegraph(caster, target)) return;
            // Under a move order the mercenary hits whatever is already in reach on the way,
            // but does not chase, keep distance or leap away from its route.
            var mover = actor as SoulMercenary;
            // Walking home in a retreat: no blows on the way (a blow costs stamina and time — the one who turned to
            // shoot fell behind and died); only a hero caught (Stand) or holding the fight (no order) fights.
            if (mover != null && ordered.Contains(mover) && Retreating && !standing)
            {
                actor.CurrentTarget = null;
                // …but a heal, a regeneration or a speed spell still goes up on the way (FleeingAid)
                if (actor.Cooldown <= 0 && TryAutomaticSkill(actor, null, true, fleeing: true)) return;
                Navigate(actor, destinations[mover], dt);
                return;
            }
            actor.CurrentTarget = target;
            if (target != null && (target.Position - actor.Position).sqrMagnitude > 1e-4f) actor.Facing = (target.Position - actor.Position).normalized;
            bool marching = mover != null && ordered.Contains(mover);
            // A ranged/support leader fights first and moves after (its safety comes first); the order waits.
            if (marching && mover == Leader && (mover.Role == SoulRole.Ranged || mover.Role == SoulRole.Support) && target != null) marching = false;
            // The leader waits for whoever fell behind (a slow walker, one still finishing a fight) before it walks on:
            // a mercenary left alone runs into a pack by itself.
            if (mover != null && mover == Leader && !ordered.Contains(mover) && !Retreating && target == null)
            {
                if (Straggler(mover) == null) paceWait = 0;
                else if ((paceWait += dt) < PaceWaitLimit || paceWait > PaceWaitLimit + PaceWalkOn)
                {
                    if (paceWait > PaceWaitLimit + PaceWalkOn) paceWait = 0; // walked on a while: wait again
                    actor.Action = "동료 기다리는 중";
                    return;
                }
                // (waited PaceWaitLimit: one that cannot catch up does not hold the party for good — it walks on a while)
            }
            // Followers stay with the leader: while it travels (or when too far / chasing something far from
            // it) they walk to their slot behind it and only hit what is already in reach. Not in a retreat: there
            // each one runs or covers on its own (TickRetreat).
            if (mover != null && !ordered.Contains(mover) && !Retreating && Leader != null && mover != Leader && Leader.Alive)
            {
                bool stray = target != null && Vector2.Distance(target.Position, Leader.Position) > EngageRadius;
                if (Vector2.Distance(mover.Position, Leader.Position) > TetherRadius || LeaderTravelling() || stray)
                {
                    marching = true;
                    destinations[mover] = FollowPoint(mover);
                    if (stray) target = null;
                }
            }
            if (actor.Cooldown > 0 && actor.MoveTime <= 0) return;
            // Marching alternates: after a strike the next action is a step, so a chasing enemy
            // cannot pin the unit in place — it fights on the way and still gets there.
            if (marching && stepNext.Contains(mover))
            {
                Navigate(actor, destinations[mover], dt);
                if (actor.MoveTime > 0 || Arrived(mover)) stepNext.Remove(mover);
                return;
            }
            // Kite: right after a shot, step back while the enemy is still close.
            if (!marching && target != null && retreatNext.Contains(actor))
            {
                if (Vector2.Distance(actor.Position, target.Position) < 2.2f && MayGiveGround(actor))
                {
                    if (MovementPattern(actor)?.FreeRetreat == true && actor.MoveTime <= 0) actor.FreeSteps = 1; // 치고 빠지기 강화
                    Navigate(actor, RetreatPoint(actor, target), dt, 1f, "치고 빠지기");
                    if (actor.MoveTime > 0) retreatNext.Remove(actor);
                    return;
                }
                retreatNext.Remove(actor);
            }
            var sighted = seen ? target : null; // no attacks through walls
            int confused = actor.Grade(SoulStatus.Confuse);
            if (confused > 0 && sighted != null && actor.Cooldown <= 0 && random.NextDouble() < SoulCombat.StumbleChance(confused))
            {
                actor.Cooldown = .8f / SoulCombat.ActionSpeed(actor);
                actor.Action = "혼란: 헛손질";
                return;
            }
            if (actor.Cooldown <= 0 && TryAutomaticSkill(actor, sighted, marching))
            {
                if (marching) stepNext.Add(mover);
                return;
            }
            // Support patterns (buffs, debuffs, heals) come after skills but before a plain attack or a step.
            if (actor.Cooldown <= 0 && TrySupport(actor, sighted)) return;
            if (!marching && target != null && Backliner(actor) && Mercenaries.Exists(ally => ally != actor && ally.Alive && !Backliner(ally) && !CasterBehind(ally))) { StayBehind(actor, target, dt); return; }
            var plan = PickAttack(actor, sighted);
            if (!marching && target != null && CasterBehind(actor) && plan.Spell == null && plan.Pattern == null
                && Mercenaries.Exists(ally => ally != actor && ally.Alive && !Backliner(ally) && !CasterBehind(ally))) { StayBehind(actor, target, dt); return; }
            if ((plan.Pattern != null || plan.Spell != null) && actor.Cooldown <= 0)
            {
                float reach = plan.Spell != null ? SoulSkillUsePolicy.Range(actor, plan.Spell) : plan.Pattern.Range;
                if (reach >= 2.5f && !(plan.Spell != null && plan.Spell.LeapToTarget)) target = Intercept(actor, target);
                if (plan.Spell != null)
                {
                    var cast = CastSkill(actor, target, plan.Spell, out bool casted);
                    if (casted)
                    {
                        spelled.Add(actor); // next time in reach: a blow of its own (PickAttack)
                        actor.Action = plan.Spell.SkillName;
                        CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Skill, Actor = actor, Target = target, Label = plan.Spell.SkillName, Magic = true });
                        QueueActive(actor, SoulTrigger.OnAttack, target);
                        AfterHit(actor, target, cast);
                        if (marching) stepNext.Add(mover);
                        if (MovementPattern(actor)?.MoveStyle == SoulMoveStyle.Kite) retreatNext.Add(actor);
                        return;
                    }
                }
                else if (random.NextDouble() < SoulCombat.WoundFailChance(actor) && SoulCombat.CanPay(actor, plan.Pattern))
                {
                    // the wound gives way mid-swing: the stamina and the time are spent, nothing lands
                    SoulCombat.Pay(actor, plan.Pattern);
                    actor.Cooldown = plan.Pattern.ActionTime / SoulCombat.ActionSpeed(actor);
                    actor.Action = "부상으로 실패: " + plan.Pattern.Id;
                    return;
                }
                // An ambush teleports first, so it may only go when the strike can follow: ready and affordable.
                // (Blinking and then failing to pay left the unit teleporting — "도약" — every frame.)
                else if ((!plan.Pattern.Ambush || (actor.Cooldown <= 0 && !actor.Disabled
                        && (actor.FollowupRemaining > 0 || SoulCombat.CanPay(actor, plan.Pattern)) && Blink(actor, target)))
                    && SoulCombat.TryBeginAttack(actor, target, plan.Pattern, out SoulHit hit,
                        AttackScale(actor, target, plan) * (actor.FollowupRemaining > 0 ? actor.FollowupMultiplier : 1f), plan.Basic, actor.FollowupRemaining > 0))
                {
                    bool followed = actor.FollowupRemaining > 0;
                    if (followed) hit.ArmorIgnore = actor.FollowupArmorIgnore;
                    actor.FollowupRemaining = 0;
                    if (plan.Pattern.Cooldown > 0) actor.SkillCooldowns[PatternKey(plan.Pattern)] = plan.Pattern.Cooldown;
                    CastPattern(actor, target, plan.Pattern, hit, plan.Basic);
                    spelled.Remove(actor);
                    if (followed) actor.Action = "회피 반격: " + plan.Pattern.Id;
                    QueueActive(actor, SoulTrigger.OnAttack, target);
                    if (marching) stepNext.Add(mover);
                    // a caster that had to hit steps back after it (치고 빠지기)
                    if (MovementPattern(actor)?.MoveStyle == SoulMoveStyle.Kite || CasterBehind(actor)) retreatNext.Add(actor);
                    return;
                }
                if (!marching)
                {
                    actor.Action = "자원 회복";
                    return;
                }
            }
            if (marching)
            {
                Navigate(actor, destinations[mover], dt);
                return;
            }
            if (target == null)
            {
                Navigate(actor, actor is SoulMercenary hero ? destinations[hero] : actor.Position, dt);
                return;
            }
            if (!seen)
            {
                // Joining a fight it cannot see yet: walk up to it (ranged units too — no standing behind a wall).
                Navigate(actor, Claim(actor, target.Position), dt);
                actor.Action = "합류";
                return;
            }
            var goal = EngageGoal(actor, target, out bool hold);
            if (hold)
            {
                if (actor.Action != "탱커 뒤 대기" && actor.Action != "호위") actor.Action = plan.OutOfMana ? "MP 회복 대기" : plan.SavingMana ? "MP 아끼는 중" : "거리 유지";
                return;
            }
            // Rush: a burst of speed while still more than two cells away.
            bool rush = MovementPattern(actor)?.MoveStyle == SoulMoveStyle.Rush && Vector2.Distance(actor.Position, target.Position) > 2f;
            Navigate(actor, goal, dt, rush ? 1.45f : 1f, engageLabel);
            if (rush) actor.Action = "돌입";
            if (plan.OutOfMana) actor.Action = "MP 회복 대기";
            else if (plan.SavingMana) actor.Action = "MP 아끼는 중";
        }

        // Standing still: what the party is doing then (resting where it stands, a breather, holding out) or 대기.
        string IdleLabel(SoulCombatant actor)
        {
            if (!(actor is SoulMercenary)) return "대기";
            if (Plan == SoulPartyPlan.Recover && !partyFighting) return "쉬는 중";
            if (Retreating && breather) return "숨 고르는 중";
            if (Retreating && Hiding) return "버티는 중";
            return "대기";
        }

        // ── the healer's place ───────────────────────────────────
        // A 성직자 (not a 전투 사제 — SoulRecruitData.Frontline) lands no blows of its own: it heals, blesses and
        // casts from behind the front, BacklineGap cells back from the ally nearest the enemy, away from it.
        public const float BacklineGap = 2.5f, BacklineSafe = 3f;
        public static bool Backliner(SoulCombatant unit) => unit is SoulMercenary hero && hero.Job == "성직자" && !(hero.Recruit != null && hero.Recruit.Frontline);
        // A mage or a summoner: its spells from where it stands; out of them (no MP) it waits behind, never a blow.
        public static bool CasterBehind(SoulCombatant unit) => unit is SoulMercenary hero && (hero.Job == "마법사" || hero.Job == "소환사") && !(hero.Recruit != null && hero.Recruit.Frontline);

        void StayBehind(SoulCombatant actor, SoulCombatant target, float dt)
        {
            SoulMercenary front = null;
            foreach (var ally in Mercenaries)
                if (ally != actor && ally.Alive && !Backliner(ally) && !CasterBehind(ally)
                    && (front == null || Vector2.Distance(ally.Position, target.Position) < Vector2.Distance(front.Position, target.Position))) front = ally;
            Vector2 spot;
            if (front != null)
            {
                var away = front.Position - target.Position;
                spot = front.Position + (away.sqrMagnitude > 1e-4f ? away.normalized : (actor.Position - target.Position).normalized) * BacklineGap;
                if (!Map.Clear(spot, actor.Stats.Radius) && !Map.ClosestReachable(spot, actor.Position, actor.Stats.Radius, out spot)) spot = actor.Position;
            }
            else spot = Vector2.Distance(actor.Position, target.Position) < BacklineSafe ? RetreatPoint(actor, target) : actor.Position;
            if (Vector2.Distance(actor.Position, spot) < .8f) { actor.Action = "후방 대기"; return; }
            Navigate(actor, spot, dt, 1f, "후방으로");
        }

        // ── attack choice ────────────────────────────────────────

        // Design proposal: a weapon without a matching pattern hits at half power, no special effects.
        public const float BasicAttackScale = .5f, LuckDropBonus = .01f;

        struct AttackPlan
        {
            public SoulPatternData Pattern;
            public SoulActiveSkillData Spell;  // set when Pattern casts skills (mage)
            public bool Basic, OutOfMana, SavingMana;
        }

        AttackPlan PickAttack(SoulCombatant actor, SoulCombatant target)
        {
            var plan = new AttackPlan();
            if (target == null) return plan;
            float distance = Vector2.Distance(actor.Position, target.Position);
            bool ownsAttack = false, caster = CasterBehind(actor) || Backliner(actor);
            foreach (var pattern in actor.Patterns())
            {
                if (pattern == null || pattern.Category != SoulPatternCategory.Attack || pattern.CastsSkills) continue;
                ownsAttack = true;
                if (distance > pattern.Range + target.Stats.Radius) continue;
                if (OnCooldown(actor, pattern) || (pattern.Ambush && !BlinkSpot(actor, target, out _))) continue;
                if (plan.Pattern == null || AttackScore(pattern, target) > AttackScore(plan.Pattern, target)) plan.Pattern = pattern;
            }
            // Magic is not a pattern: attack spells are skills, and one worth its MP goes before a plain attack —
            // except that someone with both, the enemy in reach of a pattern of its own (a mage with a boar's charge,
            // caught up close), alternates: after a spell, the blow (a charge shoves them back), then a spell again.
            var spell = BestSpell(actor, target, out bool starved, out bool saving);
            bool blowTurn = plan.Pattern != null && spelled.Contains(actor) && SoulCombat.CanPay(actor, plan.Pattern);
            if (spell != null && !blowTurn) { plan.Spell = spell; plan.Pattern = null; }
            else if (plan.Pattern == null) { plan.OutOfMana = starved; plan.SavingMana = saving && !starved; }
            ownsAttack |= HasSpells(actor);
            // a caster caught up close hits with what it holds (a staff: 휘두르기)
            if (caster && plan.Spell == null && plan.Pattern == null) ownsAttack = false;
            if (!ownsAttack && actor is SoulMercenary hero)
                foreach (var equipment in hero.Equipment)
                    if (equipment.BasicAttack != null && distance <= equipment.BasicAttack.Range + target.Stats.Radius)
                    {
                        plan.Pattern = equipment.BasicAttack;
                        plan.Basic = true;
                        break;
                    }
            return plan;
        }

        // Priority first; an ambush opens whenever it is ready, and a debuff goes on before plain damage.
        readonly HashSet<SoulCombatant> spelled = new HashSet<SoulCombatant>(); // last attack was a spell

        static float AttackScore(SoulPatternData pattern, SoulCombatant target)
        {
            float score = pattern.Priority;
            if (pattern.Ambush) score += 3;
            if (pattern.Status != null && pattern.Status.Kind != SoulStatus.None && !target.Has(pattern.Status.Kind)) score += 2;
            return score;
        }

        static string PatternKey(SoulPatternData pattern) => "pattern:" + pattern.Id;

        public static bool OnCooldown(SoulCombatant actor, SoulPatternData pattern)
            => pattern.Cooldown > 0 && actor.SkillCooldowns.TryGetValue(PatternKey(pattern), out float left) && left > 0;

        static string SkillKey(SoulActiveSkillData skill) => string.IsNullOrEmpty(skill.SoulId) ? skill.name : skill.SoulId;

        // Allies within the skill's radius (the caster too) who do not carry its buff / enchant yet.
        List<SoulCombatant> SupportedAllies(SoulCombatant actor, SoulActiveSkillData skill)
        {
            float radius = skill.Radius != null && skill.Radius.Evaluate(actor.Stats.Combat) > 0 ? skill.Radius.Evaluate(actor.Stats.Combat) : 5f;
            var result = new List<SoulCombatant>();
            string key = SkillKey(skill);
            bool imbue = skill.Imbue != null && skill.Imbue.Kind != SoulStatus.None;
            foreach (SoulCombatant ally in actor is SoulMercenary ? (IEnumerable<SoulCombatant>)Mercenaries : nearby)
            {
                if (!ally.Alive || Vector2.Distance(ally.Position, actor.Position) > radius) continue;
                // an element enchant rides on physical attacks: pointless for someone who has none (a mage)
                if (imbue && !ally.Patterns().Exists(p => p != null && p.Category == SoulPatternCategory.Attack)) continue;
                bool lacks = (skill.SelfBuffs.Length > 0 && !ally.TimedBuffs.Exists(buff => buff.Id == key)) || (imbue && (ally.ImbueRemaining <= 0 || ally.ImbueName != skill.SkillName));
                if (lacks) result.Add(ally);
            }
            return result;
        }

        // A healing skill's patient: the most hurt ally within reach, under half HP.
        SoulCombatant AfflictedAlly(SoulCombatant actor, float range)
        {
            SoulCombatant worst = null;
            foreach (var ally in AlliesOf(actor))
                if (ally.Alive && ally.Statuses.Count > 0 && Vector2.Distance(ally.Position, actor.Position) <= range && (worst == null || ally.Statuses.Count > worst.Statuses.Count))
                    worst = ally;
            return worst;
        }

        // The most hurt ally in range that needs the heal: below half HP, missing at least HealWorth of what the heal
        // gives (mend) — none of it wasted — or, for a heal that closes wounds, carrying one.
        public const float HealWorth = .7f;
        SoulCombatant WeakestAlly(SoulCombatant actor, float range, SoulCombatant besides = null, float mend = 0, bool wounds = false)
        {
            SoulCombatant worst = null;
            foreach (SoulCombatant ally in actor is SoulMercenary ? (IEnumerable<SoulCombatant>)Mercenaries : nearby)
            {
                if (ally == besides || !ally.Alive || Vector2.Distance(ally.Position, actor.Position) > range) continue;
                bool needs = Health(ally) < .5f || mend > 0 && ally.Stats.Total(StatType.MaxHp) - ally.Hp >= mend * HealWorth
                    || wounds && ally is SoulMercenary hurt && hurt.Wounds > 0;
                if (needs && (worst == null || Health(ally) < Health(worst))) worst = ally;
            }
            return worst;
        }

        // ── ambush ───────────────────────────────────────────────

        // 암습: the spot right behind the target — the side it is not facing.
        bool BlinkSpot(SoulCombatant actor, SoulCombatant target, out Vector2 spot)
        {
            Vector2 facing = target.Facing.sqrMagnitude > 1e-4f ? target.Facing.normalized : (target.Position - actor.Position).normalized;
            spot = target.Position - facing * (actor.Stats.Radius + target.Stats.Radius + .1f);
            // never into a red area that is about to land
            return Map.Clear(spot, actor.Stats.Radius) && !Danger(actor, spot, actor.Stats.Radius + .2f, 0);
        }

        bool Blink(SoulCombatant actor, SoulCombatant target)
        {
            if (!BlinkSpot(actor, target, out var spot)) return false;
            actor.Position = spot;
            actor.Facing = (target.Position - spot).normalized;
            if (routes.TryGetValue(actor, out var route)) route.Valid = false;
            Emit(SoulEventKind.Leap, actor, target);
            return true;
        }

        // ── support patterns ─────────────────────────────────────

        bool partyFighting;

        // Buffs, debuffs and heals: used when their moment comes (in or out of a fight, someone who gains from
        // it in reach), before any attack. Costs stamina and the pattern's action time like any pattern.
        bool TrySupport(SoulCombatant actor, SoulCombatant target)
        {
            bool fighting = actor is SoulMercenary ? partyFighting : target != null;
            foreach (var pattern in actor.Patterns())
            {
                if (pattern == null || pattern.Category != SoulPatternCategory.Support) continue;
                if ((pattern.UseWhen == SoulUseWhen.InCombat && !fighting) || (pattern.UseWhen == SoulUseWhen.OutOfCombat && fighting)) continue;
                if (actor is SoulMercenary && Retreating && !breather && pattern.UseWhen == SoulUseWhen.OutOfCombat) continue; // no 명상 on the way out
                if (OnCooldown(actor, pattern) || !SoulCombat.CanPay(actor, pattern)) continue;
                if (pattern.SpellFocus && !FocusWanted(actor)) continue;
                var targets = SupportTargets(actor, target, pattern);
                if (targets.Count == 0) continue;
                SoulCombat.Pay(actor, pattern);
                actor.Cooldown = pattern.ActionTime / SoulCombat.ActionSpeed(actor);
                if (pattern.Cooldown > 0) actor.SkillCooldowns[PatternKey(pattern)] = pattern.Cooldown;
                foreach (var unit in targets) ApplySupport(actor, unit, pattern);
                actor.Action = pattern.SpellFocus ? $"{pattern.Id} ({actor.Focus})" : pattern.Id;
                CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Skill, Actor = actor, Target = targets[0] == actor ? null : targets[0], Pattern = pattern, Label = pattern.Id, Support = true });
                return true;
            }
            return false;
        }

        // Who would gain from the pattern right now; empty = not worth an action.
        List<SoulCombatant> SupportTargets(SoulCombatant actor, SoulCombatant target, SoulPatternData pattern)
        {
            var result = new List<SoulCombatant>();
            bool hero = actor is SoulMercenary;
            if (pattern.SupportTarget == SoulSupportTarget.Self)
            {
                if (Gains(actor, pattern)) result.Add(actor);
            }
            else if (pattern.SupportTarget == SoulSupportTarget.Allies)
            {
                SoulCombatant worst = null;
                foreach (SoulCombatant ally in hero ? (IEnumerable<SoulCombatant>)Mercenaries : nearby)
                {
                    if (!ally.Alive || Vector2.Distance(ally.Position, actor.Position) > pattern.SupportRadius || !Gains(ally, pattern)) continue;
                    if (!pattern.Heals) result.Add(ally);
                    else if (worst == null || Health(ally) < Health(worst)) worst = ally; // a heal goes where it is needed most
                }
                if (worst != null) result.Add(worst);
            }
            else if (target != null && target.Alive)
            {
                if (pattern.Range > 0)
                {
                    if (Vector2.Distance(actor.Position, target.Position) <= pattern.Range + target.Stats.Radius && Gains(target, pattern)) result.Add(target);
                }
                else
                    foreach (SoulCombatant foe in hero ? (IEnumerable<SoulCombatant>)nearby : Shared.Heroes)
                        if (foe.Alive && Vector2.Distance(foe.Position, actor.Position) <= pattern.SupportRadius + foe.Stats.Radius && Gains(foe, pattern)) result.Add(foe);
            }
            return result;
        }

        // 주문 집중: gathered while no attack spell can go (no MP, all cooling down) — MaxFocus stacks, each FocusPerStack
        // more power for the spell that takes them.
        public const int MaxFocus = 3;
        public const float FocusPerStack = .25f;
        bool FocusWanted(SoulCombatant actor)
        {
            if (actor.Focus >= MaxFocus) return false;
            bool casts = false;
            foreach (var skill in actor.ActiveSkills())
            {
                if (skill == null || !SoulSkillUsePolicy.Magical(skill) || skill.Damage == null || skill.Damage.Evaluate(actor.Stats.Combat) <= 0) continue;
                casts = true;
                if (SoulSkillUsePolicy.CanUse(actor, skill, null)) return false; // a spell is ready: cast it instead
            }
            return casts;
        }

        static bool Gains(SoulCombatant unit, SoulPatternData pattern)
        {
            if (pattern.SpellFocus) return true;
            if (pattern.MaxStacks > 1 && pattern.Buffs.Length > 0)
            {
                // a stacking blessing: while it can stack higher, or when it is about to run out
                var held = unit.TimedBuffs.Find(buff => buff.Id == pattern.Id);
                return held == null || held.Stacks < pattern.MaxStacks || held.Remaining < 4f;
            }
            if (pattern.Heals) return Health(unit) < pattern.UseBelow;
            if (pattern.ManaRatio > 0) return unit.Mp < unit.Stats.Total(StatType.MaxMp) * pattern.UseBelow;
            if (pattern.StaminaRatio > 0) return unit.Stamina < unit.Stats.Total(StatType.MaxStamina) * pattern.UseBelow;
            if (pattern.Imbue != null && pattern.Imbue.Kind != SoulStatus.None) return unit.ImbueRemaining <= 0;
            if (pattern.SupportTarget == SoulSupportTarget.Enemies) return pattern.Status != null && pattern.Status.Kind != SoulStatus.None && !unit.Has(pattern.Status.Kind);
            if (pattern.UseBelow < .9f && Health(unit) >= pattern.UseBelow) return false; // a buff kept for when it goes badly (질주)
            return pattern.Buffs.Length > 0 && !unit.TimedBuffs.Exists(buff => buff.Id == pattern.Id);
        }

        void ApplySupport(SoulCombatant user, SoulCombatant unit, SoulPatternData pattern)
        {
            var stats = user.Stats.Combat;
            if (pattern.Heals)
                unit.Hp = Mathf.Min(unit.Stats.Total(StatType.MaxHp), unit.Hp + unit.Stats.Total(StatType.MaxHp) * pattern.HealRatio + Mathf.Max(0, pattern.Heal.Evaluate(stats)));
            if (pattern.ManaRatio > 0)
                unit.Mp = Mathf.Min(unit.Stats.Total(StatType.MaxMp), unit.Mp + unit.Stats.Total(StatType.MaxMp) * pattern.ManaRatio);
            if (pattern.StaminaRatio > 0)
                unit.Stamina = Mathf.Min(unit.Stats.Total(StatType.MaxStamina), unit.Stamina + unit.Stats.Total(StatType.MaxStamina) * pattern.StaminaRatio);
            if (pattern.SpellFocus) unit.Focus = Mathf.Min(MaxFocus, unit.Focus + 1);
            if (pattern.Buffs.Length > 0)
            {
                // a stacking one (자연 교감) adds a stack (MaxStacks at most) and renews the time
                int stacks = 1;
                if (pattern.MaxStacks > 1) { var held = unit.TimedBuffs.Find(buff => buff.Id == pattern.Id); stacks = Mathf.Min(pattern.MaxStacks, (held != null ? held.Stacks : 0) + 1); }
                var bonuses = new SoulStatBonus[pattern.Buffs.Length];
                for (int i = 0; i < bonuses.Length; i++)
                    bonuses[i] = new SoulStatBonus { Stat = pattern.Buffs[i].Stat, Value = pattern.Buffs[i].Amount.Evaluate(stats) * stacks };
                unit.AddTimedBuff(pattern.Id, bonuses, pattern.BuffDuration.Evaluate(stats));
                var laid = unit.TimedBuffs.Find(buff => buff.Id == pattern.Id);
                if (laid != null) laid.Stacks = stacks;
            }
            if (pattern.Imbue != null && pattern.Imbue.Kind != SoulStatus.None)
            {
                unit.Imbue = pattern.Imbue;
                unit.ImbueRemaining = pattern.ImbueDuration.Evaluate(stats);
                unit.ImbueName = pattern.Id;
                unit.ImbueChance = pattern.Imbue.Chance.Evaluate(stats);
                unit.ImbueDuration = pattern.Imbue.Duration.Evaluate(stats);
                unit.ImbueDps = pattern.Imbue.DamagePerSecond.Evaluate(stats);
                unit.ImbueGrade = pattern.Imbue.GradeFor(stats);
            }
            if (pattern.SupportTarget == SoulSupportTarget.Enemies && pattern.Status != null && pattern.Status.Kind != SoulStatus.None)
                SoulCombat.ApplyStatus(unit, pattern.Status.Kind, pattern.Status.Chance.Evaluate(stats), pattern.Status.Duration.Evaluate(stats),
                    pattern.Status.DamagePerSecond.Evaluate(stats), random, pattern.Status.GradeFor(stats));
        }

        // ── intimidation ─────────────────────────────────────────

        // A monster whose presence (threat) is far above a mercenary's nerve frightens it at first sight.
        // Nerve = 1 + will × 10%; the gap sets the fear grade (fear resist, also from will, can still lower it).
        public const float NerveBase = 1f, NervePerWill = .1f;
        public static float Nerve(SoulCombatant unit) => NerveBase + unit.Stats.Total(StatType.Will) * NervePerWill;

        void Intimidate(SoulMercenary hero, SoulMonster monster)
        {
            float gap = monster.Stats.Total(StatType.Threat) - Nerve(hero);
            if (gap <= 0) return;
            int grade = gap < 1 ? 1 : gap < 2 ? 2 : 3;
            if (SoulCombat.ApplyStatus(hero, SoulStatus.Fear, 1f, Mathf.Min(8f, 3f + gap * 2f), 0, random, grade))
                Log($"{hero.Name}: {monster.Data.Name}의 위압감에 공포 {hero.Grade(SoulStatus.Fear)}등급");
        }

        // Highest-damage castable skill in range. starved = something was in range but MP/stamina was short.
        // MP is spent where it counts. A spell is cast when enough MP stays in reserve afterwards, or when the
        // moment is worth it anyway: two or more enemies inside its circle, an elite or boss, or a finishing blow.
        // Among those the one with the most damage over everyone it catches wins.
        public const float ManaReserve = .35f;

        static bool HasSpells(SoulCombatant actor)
            => actor.ActiveSkills().Exists(skill => skill != null && skill.Trigger == SoulTrigger.Cast);

        SoulActiveSkillData BestSpell(SoulCombatant actor, SoulCombatant target, out bool starved, out bool saving)
        {
            starved = saving = false;
            SoulActiveSkillData best = null;
            float bestDamage = float.MinValue;
            float distance = Vector2.Distance(actor.Position, target.Position);
            foreach (var skill in actor.ActiveSkills())
            {
                if (skill == null || skill.Trigger != SoulTrigger.Cast) continue;
                if (distance > SoulSkillUsePolicy.Range(actor, skill)) continue;
                if (!SoulSkillUsePolicy.CanUse(actor, skill, target))
                {
                    SoulSkillUsePolicy.Costs(actor, skill, out float stamina, out float mana);
                    starved |= actor.Mp < mana || actor.Stamina < stamina;
                    continue;
                }
                float damage = skill.Damage.Evaluate(actor.Stats.Combat);
                int crowd = Crowd(actor, target, SoulSkillUsePolicy.SpellRadius(actor, skill));
                bool curse = skill.Status != null && skill.Status.Kind != SoulStatus.None;
                if (damage <= 0 && curse)
                {
                    // a debuff spell: worth it only while someone in the circle is still free of it
                    int fresh = Crowd(actor, target, SoulSkillUsePolicy.SpellRadius(actor, skill), skill.Status.Kind);
                    if (fresh == 0) continue;
                    damage = 5f * fresh * skill.Status.GradeFor(actor.Stats.Combat); // ranking value, not damage
                }
                SoulSkillUsePolicy.Costs(actor, skill, out _, out float cost);
                bool worth = actor.Mp - cost >= actor.Stats.Total(StatType.MaxMp) * ManaReserve || crowd >= 2
                    || (target is SoulMonster big && (big.Data.Elite || big.Data.Guardian)) || target.Hp <= damage;
                if (!worth) { saving = true; continue; }
                if (damage * crowd > bestDamage) { best = skill; bestDamage = damage * crowd; }
            }
            return best;
        }

        // Enemies of the actor standing inside a circle around the target (the target included).
        int Crowd(SoulCombatant actor, SoulCombatant target, float radius, SoulStatus without = SoulStatus.None)
        {
            int count = 0;
            foreach (SoulCombatant foe in actor is SoulMercenary ? (IEnumerable<SoulCombatant>)nearby : Shared.Heroes)
                if (foe.Alive && Vector2.Distance(foe.Position, target.Position) <= radius + foe.Stats.Radius && (without == SoulStatus.None || !foe.Has(without))) count++;
            return without == SoulStatus.None ? Mathf.Max(1, count) : count;
        }

        // Longest distance this unit can attack from (patterns, castable skills, weapon fallback).
        public static float AttackReach(SoulCombatant actor)
        {
            float reach = 0;
            var patterns = actor.Patterns();
            foreach (var pattern in patterns)
                if (pattern != null && pattern.Category == SoulPatternCategory.Attack && !pattern.CastsSkills) reach = Mathf.Max(reach, pattern.Range);
            foreach (var skill in actor.ActiveSkills())
                if (skill != null && skill.Trigger == SoulTrigger.Cast && (skill.RequiredPattern == null || patterns.Contains(skill.RequiredPattern)))
                    reach = Mathf.Max(reach, SoulSkillUsePolicy.Range(actor, skill));
            if (reach == 0 && actor is SoulMercenary hero)
                foreach (var equipment in hero.Equipment)
                    if (equipment.BasicAttack != null) reach = Mathf.Max(reach, equipment.BasicAttack.Range);
            return reach;
        }

        static float Perception(SoulCombatant actor) => Mathf.Max(3.5f, AttackReach(actor) + .5f);

        // ── positioning ──────────────────────────────────────────

        // Ranged units prefer keeping distance; everyone else takes their highest-priority movement pattern.
        static SoulPatternData MovementPattern(SoulCombatant actor)
        {
            SoulPatternData best = null, keep = null;
            foreach (var pattern in actor.Patterns())
            {
                if (pattern == null || pattern.Category != SoulPatternCategory.Movement) continue;
                if (pattern.MoveStyle == SoulMoveStyle.KeepDistance || pattern.MoveStyle == SoulMoveStyle.Kite) { if (keep == null || pattern.Priority > keep.Priority) keep = pattern; continue; }
                if (best == null || pattern.Priority > best.Priority) best = pattern;
            }
            bool frontLine = actor is SoulMercenary hero && (hero.Role == SoulRole.Melee || hero.Role == SoulRole.Tank);
            if (frontLine) return best ?? keep; // front liners close in even if they learned a ranged pattern
            if (keep != null && (best == null || AttackReach(actor) >= 3f)) return keep;
            return best;
        }

        // Units holding their keep-distance spot, and the side each flanker chose (hysteresis: no flipping).
        readonly HashSet<SoulCombatant> keeping = new HashSet<SoulCombatant>();

        // A monster that backs away from its fight (고블린 궁수: 거리 유지, 치고 빠지기, 구르기) gives up MonsterBackoff
        // cells at most, won back at MonsterBackoffRefill cells a second: pressed longer, it stands and fights where
        // it is (it still rolls, just not away) instead of drawing the party across the floor.
        public static float MonsterBackoff = 2f;
        public const float MonsterBackoffRefill = .1f;
        readonly Dictionary<SoulCombatant, (float left, float at)> ground = new Dictionary<SoulCombatant, (float, float)>();

        public float GroundLeft(SoulCombatant unit)
            => ground.TryGetValue(unit, out var last) ? Mathf.Min(MonsterBackoff, last.left + Mathf.Max(0, clock - last.at) * MonsterBackoffRefill) : MonsterBackoff;

        bool MayGiveGround(SoulCombatant unit) => !(unit is SoulMonster) || GroundLeft(unit) > 0;

        // After a monster's turn: whatever it moved away from its target is ground given up.
        void CountGround(SoulMonster monster, Vector2 was)
        {
            var foe = monster.CurrentTarget;
            if (foe == null) return;
            float gain = Vector2.Distance(monster.Position, foe.Position) - Vector2.Distance(was, foe.Position);
            if (gain > .001f) ground[monster] = (GroundLeft(monster) - gain, clock);
        }
        readonly Dictionary<SoulCombatant, float> flankSide = new Dictionary<SoulCombatant, float>();

        // How far around the target one strafing step aims (radians): a visible curve, not a jitter.
        public const float StrafeStep = .55f;
        string engageLabel;

        Vector2 EngageGoal(SoulCombatant actor, SoulCombatant target, out bool hold)
        {
            hold = false;
            engageLabel = "접근";
            var movement = MovementPattern(actor);
            var style = movement != null ? movement.MoveStyle : SoulMoveStyle.Approach;
            float distance = Vector2.Distance(actor.Position, target.Position);
            if (actor is SoulMercenary hero && (hero.Role == SoulRole.Melee || hero.Role == SoulRole.Tank))
            {
                // Tank goes first. Melee lets a living tank take the front (closing up behind it) and
                // acts like a tank itself when there is none.
                var tank = hero.Role == SoulRole.Melee ? Mercenaries.Find(m => m.Alive && m.Role == SoulRole.Tank) : null;
                // Wait only while the tank is still closing in; once it is within 2.5 cells of the target
                // (even a kiting archer), melee goes in — never stands back for good.
                float tankGap = tank != null ? Vector2.Distance(tank.Position, target.Position) : 0;
                if (tank != null && tankGap > 2.5f && distance < tankGap - .5f)
                {
                    if (Vector2.Distance(hero.Position, tank.Position) <= 1.4f) { hold = true; hero.Action = "탱커 뒤 대기"; return hero.Position; }
                    return Claim(hero, tank.Position);
                }
                // A front liner without a tank to hide behind goes straight in (escort/rush keep their meaning).
                if ((hero.Role == SoulRole.Tank || tank == null) && style == SoulMoveStyle.Flank) style = SoulMoveStyle.Approach;
            }
            if (style == SoulMoveStyle.Escort && actor is SoulMercenary guard)
            {
                // Stand between the most hurt backliner and the enemy coming at it.
                SoulMercenary ward = null;
                foreach (var ally in Mercenaries)
                    if (ally != guard && ally.Alive && (ally.Role == SoulRole.Ranged || ally.Role == SoulRole.Support)
                        && Vector2.Distance(ally.Position, guard.Position) < EngageRadius
                        && (ward == null || ally.Hp / ally.Stats.Total(StatType.MaxHp) < ward.Hp / ward.Stats.Total(StatType.MaxHp))) ward = ally;
                if (ward != null && Vector2.Distance(target.Position, ward.Position) < 4f)
                {
                    var post = ward.Position + (target.Position - ward.Position).normalized * 1.1f;
                    if (Vector2.Distance(guard.Position, post) < ArriveDistance) { hold = true; guard.Action = "호위"; return post; }
                    if (Map.Clear(post, guard.Stats.Radius)) { engageLabel = "호위"; return post; }
                }
                style = SoulMoveStyle.Approach;
            }
            if (style == SoulMoveStyle.Kite) style = SoulMoveStyle.KeepDistance; // positioning like keep-distance, plus the post-shot step
            if (style == SoulMoveStyle.KeepDistance)
            {
                // Keep distance only from inside the real attack range (spell ranges do not count the
                // target's body), so a ranged unit never stands out of reach just watching.
                float reach = AttackReach(actor);
                float ideal = Mathf.Max(1.5f, reach * .75f);
                // Hysteresis: once settled, stay until the target really leaves the band (a target shuffling
                // back and forth used to flip hold / approach / retreat several times a second).
                bool settled = keeping.Contains(actor);
                bool far = distance > (settled ? reach - .05f : reach - .4f), inBand = !far && distance >= ideal - (settled ? 1.1f : .6f);
                // a monster gives ground only so far (GiveGround); then it stands and shoots, however close the enemy
                bool yield = !far && !inBand && MayGiveGround(actor);
                if (far) { keeping.Remove(actor); engageLabel = "사거리 진입"; return target.Position; }
                if (!yield && !inBand) { hold = true; actor.Action = "버티기"; return actor.Position; }
                if (inBand) { keeping.Add(actor); hold = true; return actor.Position; }
                keeping.Remove(actor);
                var away = RetreatPoint(actor, target);
                hold = (away - actor.Position).sqrMagnitude < 1e-4f;
                engageLabel = "거리 벌리기";
                return away;
            }
            if (style == SoulMoveStyle.Flank)
            {
                // 측면 이동: instead of walking straight at the enemy the unit circles it — eyes on the target
                // (Act keeps the facing) while the feet slide along an arc around it. That arc is what puts it
                // on the enemy's flank, and the flank bonus follows from standing there.
                float ring = Mathf.Clamp(AttackReach(actor) * .85f, 1f, 3.2f);
                if (distance <= ring + 1.5f)
                {
                    Vector2 offset = actor.Position - target.Position;
                    float here = offset.sqrMagnitude < 1e-4f ? 0 : Mathf.Atan2(offset.y, offset.x);
                    if (!flankSide.TryGetValue(actor, out float sign)) sign = random.Next(2) == 0 ? 1 : -1;
                    for (int attempt = 0; attempt < 2; attempt++)
                    {
                        float angle = here + sign * StrafeStep;
                        var point = target.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                        if (Map.Clear(point, actor.Stats.Radius) && Map.Straight(actor.Position, point, actor.Stats.Radius))
                        {
                            flankSide[actor] = sign;
                            engageLabel = movement != null ? movement.Id : "측면 이동";
                            return point;
                        }
                        sign = -sign;
                    }
                    flankSide[actor] = sign; // boxed in on both sides: close in this step, try the arc again next
                }
            }
            return Claim(actor, target.Position); // a free spot next to the target, not the same one as an ally
        }

        // Some monster is inside this unit's attack reach.
        bool Engaged(SoulCombatant unit)
        {
            float reach = AttackReach(unit) + .3f;
            foreach (var monster in nearby)
                if (monster.Alive && Vector2.Distance(unit.Position, monster.Position) <= reach + monster.Stats.Radius) return true;
            return false;
        }

        // ── automatic exploration ────────────────────────────────

        float autoTimer;

        // With no player order and nobody fighting, the party walks to the nearest edge of the known map;
        // once the exit is open and known it heads out. A player order always takes precedence.
        void AutoExploreStep(float dt)
        {
            if (!AutoControl || Mercenaries.Exists(hero => hero.Alive && ordered.Contains(hero))) return;
            autoTimer -= dt;
            if (autoTimer > 0) return;
            autoTimer = .5f;
            ShareSpoils(); // souls and gear go to whoever needs them (SoulSessionPlan)
            if (PartyInCombat()) return; // fight first — including fights only a ranged ally can see
            if (FollowPlan()) return;     // resting it off, or on the way out (SoulSessionPlan)
            // out of breath counts too: below a quarter of stamina the party waits until everyone has 80% back
            if (!Resting && Mercenaries.Exists(hero => hero.Alive && (Health(hero) < RestBelowNow || Breath(hero) < BreathBelow))) Resting = true;
            if (Resting && Mercenaries.TrueForAll(hero => !hero.Alive || Health(hero) >= RestUntil && Breath(hero) >= BreathUntil)) Resting = false;
            if (Camping > 0) return;
            if (Resting)
            {
                // Stop where the party stands (no order, so a fight still breaks the rest).
                var resting = Leader;
                if (resting != null && !Arrived(resting)) SetDestination(resting, resting.Position, out Vector2 _, playerOrder: false);
                autoGoal = new Vector2Int(-1, -1);
                return;
            }

            var leader = Leader;
            if (leader == null) return;
            var goal = AutoGoal(leader);
            // nothing left but a guardian too strong for the party as it is: home, rather than waiting in the dark
            if (goal == null && Monsters.Exists(m => m.Alive && m.Data.Guardian && Avoided(m))) { BeginRetreat("부상을 안고는 수호자를 이길 수 없습니다", recoverable: false); return; }
            // at the target floor no goal is a portal (not even the edge of the known map lying on one)
            if (goal == null || AtTarget && IsExit(goal.Value))
            {
                // nothing left it can do here (what is left is avoided, out of reach or explored): home, rather than
                // standing there until worn out
                idleNoGoal += .5f;
                if (idleNoGoal >= NoGoalTime) { idleNoGoal = 0; BeginRetreat(AtTarget ? "더 사냥할 수 있는 적이 없습니다" : "더 갈 수 있는 곳이 없습니다", recoverable: false); }
                return;
            }
            idleNoGoal = 0;
            if (goal.Value == autoGoal)
            {
                if (!Arrived(leader)) return; // still on the way
                // Arrived next to a goal it could not reach (a chest behind a closed secret door): try again —
                // a pathfinder may have found the door on the way — and give up after a few tries.
                if (Map.Cell(leader.Position) != goal.Value && ++goalTries >= 3) { skippedGoals.Add(goal.Value); goalTries = 0; }
            }
            else goalTries = 0;
            autoGoal = goal.Value;
            SetDestination(leader, goal.Value, out _, playerOrder: false); // followers keep their slots behind the leader
        }

        Vector2Int autoGoal = new Vector2Int(-1, -1);
        public const float NoGoalTime = 20f;
        float idleNoGoal;
        int goalTries;
        readonly HashSet<Vector2Int> skippedGoals = new HashSet<Vector2Int>();

        Vector2Int? AutoGoal(SoulMercenary leader)
        {
            var mode = EffectiveMode;
            if (mode == SoulExploreMode.Advance) return NearestKnownExit(leader) ?? Exit;
            // On the way to its target floor (the party's order: hunt or farm down there): up the first portal it knows —
            // no waiting for the guardian, no clearing the floor; it fights what it meets on the way.
            if (TargetMode != SoulExploreMode.Explore && Floor < TargetFloor && ExitOpen && ExitKnown) return NearestKnownExit(leader);
            // Hidden stages are dangerous: only the farming aim (the player's call) goes for what is inside them.
            var chest = NearestChest(leader, mode == SoulExploreMode.Farm);
            if (mode == SoulExploreMode.Farm)
            {
                var boss = Monsters.Find(m => m.Alive && m.Data.Guardian && Knows(m) && !Avoided(m));
                if (boss != null) return Map.Cell(boss.Position);
                if (chest != null) return Map.Cell(chest.Position);
                var stage = HiddenStages.Find(s => Knows(s) && !s.Found && !skippedGoals.Contains(SoulDungeonGenerator.Center(s.Rect)));
                if (stage != null) return SoulDungeonGenerator.Center(stage.Rect);
            }
            if (mode == SoulExploreMode.Hunt)
            {
                var prey = HuntTarget(leader);
                if (prey != null) return Map.Cell(prey.Position);
            }
            if (chest != null) return Map.Cell(chest.Position);
            // Without a pathfinder the party leaves on its own once the boss is down; with one the player decides.
            // At the target floor it stays: no portal, whatever else there is to do.
            if (!AtTarget && !CanChooseRoute && ExitOpen && ExitKnown && !BossAlive) return NearestKnownExit(leader);
            var next = (CanChooseRoute ? NextRoom(leader) : null) ?? Frontier(Map.Cell(leader.Position));
            if (next != null) return next;
            var guardian = Monsters.Find(m => m.Alive && m.Data.Guardian && !Avoided(m)); // not while too hurt for it
            if (guardian != null) return Map.Cell(guardian.Position);
            return ExitOpen && !AtTarget ? NearestKnownExit(leader) ?? Exit : (Vector2Int?)null;
        }

        // A goal once chosen is kept: another takes over only when it is clearly nearer (SwitchMargin of the distance).
        // Two at about the same distance used to swap every few steps — each step toward one brought the other nearer —
        // and the party turned round and round on the spot.
        public const float SwitchMargin = .6f, OtherPartyReach = 8f;
        SoulChest chestAim;
        SoulMonster preyAim;

        SoulChest NearestChest(SoulMercenary from, bool intoStages)
        {
            SoulChest best = null;
            float bestDistance = float.MaxValue, aimDistance = float.MaxValue;
            foreach (var chest in Chests)
            {
                if (chest.Opened || !Knows(chest) || (chest.InStage && !intoStages) || skippedGoals.Contains(Map.Cell(chest.Position))) continue;
                float d = Vector2.Distance(from.Position, chest.Position);
                if (chest == chestAim) aimDistance = d;
                if (d < bestDistance) { bestDistance = d; best = chest; }
            }
            if (aimDistance < float.MaxValue && bestDistance > aimDistance * SwitchMargin) return chestAim;
            return chestAim = best;
        }

        // Hunting: the nearest monster, with species nobody in the party has beaten yet counting as twice as close.
        // A horde more than three times the party's living number is left alone (a small party picks its fights).
        public const int HuntGroupPerHero = 3;

        SoulMonster HuntTarget(SoulMercenary from)
        {
            SoulMonster best = null;
            float bestScore = float.MaxValue, aimScore = float.MaxValue;
            foreach (var monster in Monsters)
            {
                if (!monster.Alive || monster.Data.Guardian || skippedGoals.Contains(Map.Cell(monster.Position))) continue;
                if (Avoided(monster) || Ignored(monster)) continue;
                bool known = Mercenaries.Exists(hero => hero.Alive && hero.Codex.ContainsKey(monster.Data.Id));
                var offset = monster.Position - from.Position;
                float score = offset.magnitude * (known ? 1f : .5f) - Lean(offset);
                // another party of the day is right by it: leave it to them
                foreach (var other in FloorParties)
                    if (other != this && other.Leader != null && Vector2.Distance(other.Leader.Position, monster.Position) < OtherPartyReach) { score += OtherPartyReach * 2; break; }
                if (monster == preyAim) aimScore = score;
                if (score < bestScore) { bestScore = score; best = monster; }
            }
            if (aimScore < float.MaxValue && bestScore > aimScore * SwitchMargin) return preyAim; // the one it is after
            return preyAim = best;
        }

        // Each party leans its own way (bearing, set once): among goals at about the same distance it takes the one
        // on its side — so parties leaving the same start split up instead of walking as one. BearingPull tiles
        // of distance are worth a goal straight ahead over one straight behind (half that each way).
        Vector2 bearing;
        static readonly float[] PartyTurns = { 0, 180, 90, 270, 45, 225 };
        public const float BearingPull = 6f;
        float Lean(Vector2 offset) => offset.sqrMagnitude < 1e-4f ? 0 : Vector2.Dot(offset.normalized, bearing) * BearingPull * .5f;

        // Route sense: the nearest room not seen yet (rooms, not corridor ends — no dead ends). The boss arena is
        // left to the farming aim.
        Vector2Int? NextRoom(SoulMercenary from)
        {
            Vector2Int? best = null;
            float bestDistance = float.MaxValue;
            void Consider(RectInt room)
            {
                var center = SoulDungeonGenerator.Center(room);
                if (room.Equals(Layout.BossArena) || IsExplored(center) || skippedGoals.Contains(center) || Dangerous(center.x, center.y)) return;
                var offset = Map.Center(center) - from.Position;
                float d = offset.magnitude - Lean(offset);
                if (d < bestDistance) { bestDistance = d; best = center; }
            }
            foreach (var room in Layout.Rooms) Consider(room);
            foreach (var hall in Layout.Halls) Consider(hall);
            return best;
        }

        Vector2Int? NearestKnownExit(SoulMercenary from)
        {
            Vector2Int? best = null;
            float bestDistance = float.MaxValue;
            foreach (var exit in exits)
            {
                if (!IsExitKnown(exit)) continue;
                float d = Vector2.Distance(from.Position, Map.Center(exit));
                if (d < bestDistance) { bestDistance = d; best = exit; }
            }
            return best;
        }

        // Explored walkable cell that touches unexplored space (BFS over the known map, reused buffers): the nearest
        // by walking distance, leaning toward the party's bearing (edges up to BearingPull steps further count).
        int[] frontierSeen, frontierQueue, frontierDepth;
        int frontierStamp;

        Vector2Int? Frontier(Vector2Int start)
        {
            if (!Map.Open(start)) return null;
            int width = Map.Width, height = Map.Height;
            if (frontierSeen == null) { frontierSeen = new int[width * height]; frontierQueue = new int[width * height]; frontierDepth = new int[width * height]; }
            int stamp = ++frontierStamp, head = 0, tail = 0, origin = start.y * width + start.x;
            frontierSeen[origin] = stamp;
            frontierDepth[origin] = 0;
            frontierQueue[tail++] = origin;
            Vector2Int? best = null;
            float bestScore = float.MaxValue;
            int horizon = int.MaxValue;
            while (head < tail)
            {
                int index = frontierQueue[head++], x = index % width, y = index / width, depth = frontierDepth[index];
                if (depth > horizon) break;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int next = ny * width + nx;
                    if (!Explored[next])
                    {
                        if (index == origin || Dangerous(x, y)) continue;
                        if (horizon == int.MaxValue) horizon = depth + Mathf.CeilToInt(BearingPull);
                        float score = depth - Lean(new Vector2(x - start.x, y - start.y));
                        if (score < bestScore) { bestScore = score; best = new Vector2Int(x, y); }
                        continue;
                    }
                    if (frontierSeen[next] == stamp || !Map.Open(nx, ny) || Dangerous(nx, ny)) continue;
                    frontierSeen[next] = stamp;
                    frontierDepth[next] = depth + 1;
                    frontierQueue[tail++] = next;
                }
            }
            return best;
        }

        // A step of about one tile that gains distance from the threat (8 directions, straight line clear).
        Vector2 RetreatPoint(SoulCombatant actor, SoulCombatant threat)
        {
            var best = actor.Position;
            float bestDistance = Vector2.Distance(actor.Position, threat.Position);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                var point = actor.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (!Map.Straight(actor.Position, point, actor.Stats.Radius)) continue;
                float distance = Vector2.Distance(point, threat.Position);
                if (distance > bestDistance + .2f) { best = point; bestDistance = distance; }
            }
            return best;
        }

        // Soft body collision: overlapping units drift apart each tick, the lighter one moves more.
        void Separate()
        {
            var bodies = new List<SoulCombatant>();
            foreach (var hero in Mercenaries) if (hero.Alive) bodies.Add(hero);
            foreach (var monster in nearby) if (monster.Alive) bodies.Add(monster);
            for (int i = 0; i < bodies.Count; i++)
                for (int j = i + 1; j < bodies.Count; j++)
                {
                    var a = bodies[i];
                    var b = bodies[j];
                    Vector2 delta = b.Position - a.Position;
                    float distance = delta.magnitude, minimum = a.Stats.Radius + b.Stats.Radius;
                    if (distance >= minimum) continue;
                    Vector2 direction = distance < 1e-4f ? Vector2.right : delta / distance;
                    float overlap = (minimum - distance) * (a is SoulMercenary && b is SoulMercenary ? .15f : .5f);
                    float wa = Mathf.Max(1f, a.Stats.Weight), wb = Mathf.Max(1f, b.Stats.Weight);
                    Shift(a, -direction * overlap * wb / (wa + wb));
                    Shift(b, direction * overlap * wa / (wa + wb));
                }
        }

        void Shift(SoulCombatant unit, Vector2 delta)
        {
            var next = unit.Position + delta;
            if (Map.Clear(next, unit.Stats.Radius)) unit.Position = next;
        }

        // Shared follow-up of every landed pattern/skill hit: active triggers, knockback and status log.
        void AfterHit(SoulCombatant actor, SoulCombatant target, SoulHitResult hit)
        {
            if (target == null) return;
            if (!hit.Dodged && hit.Damage > 0)
            {
                QueueActive(actor, SoulTrigger.OnAttackLanded, target);
                QueueActive(target, SoulTrigger.OnHit, actor);
            }
            if (hit.Killed) QueueActive(actor, SoulTrigger.OnKill, target);
            ReportHit(actor, target, hit);
        }

        // ── special actions ──────────────────────────────────────

        // Every active skill goes through here. What the views draw: a spell or a warned skill when it lands (Strike),
        // anything else the moment it goes off.
        public SoulHitResult CastSkill(SoulCombatant actor, SoulCombatant target, SoulActiveSkillData skill, out bool cast)
        {
            var result = CastSkillCore(actor, target, skill, out cast);
            bool later = skill.Trigger == SoulTrigger.Cast || (target != null && Warned(actor, skill));
            if (cast && !later) Fx(actor, target, skill, SoulFxPhase.Land);
            return result;
        }

        // A SkillFx event: where the skill shows (the target for a single blow, the caster for what spreads from it, the
        // target's spot for what lands there), which way, how big (radius, or the reach of a line).
        void Fx(SoulCombatant actor, SoulCombatant target, SoulActiveSkillData skill, SoulFxPhase phase, Vector2? at = null, float size = -1, Vector2? way = null)
        {
            var stats = actor.Stats.Combat;
            Vector2 direction = way ?? (target != null && target != actor ? target.Position - actor.Position : actor.Facing);
            direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector2.right;
            bool spreads = skill.Burst || skill.Line || skill.Field != SoulFieldKind.None || skill.LeapToTarget || target == null || target == actor;
            Vector2 point = at ?? (skill.AtTarget && target != null ? target.Position : spreads ? actor.Position : target.Position);
            float amount = size >= 0 ? size : skill.Line ? SoulSkillUsePolicy.Range(actor, skill) : skill.Radius != null ? skill.Radius.Evaluate(stats) : 0;
            CombatEvents.Add(new SoulCombatEvent
            {
                Kind = SoulEventKind.SkillFx, Actor = actor, Target = target, Skill = skill, Point = point, Direction = direction, Amount = amount, Phase = phase,
                Magic = SoulSkillUsePolicy.Magical(skill) || skill.DamageSchool == SoulDamageSchool.Magic,
            });
        }

        SoulHitResult CastSkillCore(SoulCombatant actor, SoulCombatant target, SoulActiveSkillData skill, out bool cast)
        {
            // Attack magic is an area like every other attack: a circle on the target, read during the chant,
            // then everyone inside is hit (SoulSkillUsePolicy.SpellRadius).
            bool curse = skill.Status != null && skill.Status.Kind != SoulStatus.None;
            if (skill.Trigger == SoulTrigger.Cast && !skill.LeapToTarget && ((skill.Damage != null && skill.Damage.Evaluate(actor.Stats.Combat) > 0) || curse))
            {
                cast = target != null && target.Alive && actor.Cooldown <= 0 && SoulSkillUsePolicy.TryCommit(actor, skill, target);
                if (!cast) return default;
                var stats = actor.Stats.Combat;
                float drain = 1;
                if (skill.DrainMana)
                {
                    // 비전 폭발: all the MP left goes into it
                    SoulSkillUsePolicy.Costs(actor, skill, out _, out float listed);
                    drain += actor.Mp / Mathf.Max(1, listed);
                    actor.Mp = 0;
                }
                var hit = new SoulHit { School = skill.DamageSchool, Kind = skill.DamageKind, Raw = skill.Damage.Evaluate(stats) * SoulSkillUsePolicy.Power(actor, skill) * drain, Knockback = skill.Knockback };
                hit.SetStatus(skill.Status, stats);
                hit.SetStatus(skill.Extra, stats);
                if (SoulSkillUsePolicy.Magical(skill)) hit.Empower(SoulSkillUsePolicy.SpellPower(actor)); // curses and burns: longer, a grade higher
                float circle = SoulSkillUsePolicy.SpellRadius(actor, skill);
                float action = SoulSkillUsePolicy.ActionTime(skill) / SoulCombat.ActionSpeed(actor);
                Cast(actor, target, new SoulTelegraph
                {
                    FxSkill = skill,
                    Pattern = skill.RequiredPattern, Name = skill.SkillName, Shape = SoulAreaShape.Circle, Anchor = SoulAreaAnchor.Target,
                    Size = circle, Width = circle, WindUp = Mathf.Max(.35f, action * .45f), Hit = hit, Hits = 1, Magic = true
                });
                SoulCombat.ApplySelfBuffs(actor, skill);
                if (skill.Heal != null && skill.Heal.Evaluate(stats) > 0)
                    actor.Hp = Mathf.Min(actor.Stats.Total(StatType.MaxHp), actor.Hp + skill.Heal.Evaluate(stats) * SoulSkillUsePolicy.Power(actor, skill));
                return default; // the hits are reported when the circle lands (Strike)
            }
            if (target != null && Warned(actor, skill))
            {
                cast = actor.Cooldown <= 0 && SoulSkillUsePolicy.TryCommit(actor, skill, target);
                if (!cast) return default;
                if (skill.Cleanse) actor.Statuses.Clear();
                SoulCombat.ApplySelfBuffs(actor, skill);
                if (skill.Heal != null && skill.Heal.Evaluate(actor.Stats.Combat) > 0)
                    actor.Hp = Mathf.Min(actor.Stats.Total(StatType.MaxHp), actor.Hp + skill.Heal.Evaluate(actor.Stats.Combat) * SoulSkillUsePolicy.Power(actor, skill));
                Cast(actor, target, SkillTelegraph(actor, target, skill));
                return default; // the hits come when the warning lands (StrikeSkill)
            }
            if (!skill.LeapToTarget && Special(skill))
            {
                cast = actor.Cooldown <= 0 && SoulSkillUsePolicy.TryCommit(actor, skill, target);
                if (!cast) return default;
                if (skill.Cleanse) actor.Statuses.Clear(); // 탈피
                SoulCombat.ApplySelfBuffs(actor, skill);
                if (skill.Heal != null && skill.Heal.Evaluate(actor.Stats.Combat) > 0)
                    actor.Hp = Mathf.Min(actor.Stats.Total(StatType.MaxHp), actor.Hp + skill.Heal.Evaluate(actor.Stats.Combat) * SoulSkillUsePolicy.Power(actor, skill));
                Vector2 centre = skill.AtTarget && target != null ? target.Position : actor.Position;
                if (skill.Field != SoulFieldKind.None) PlaceFieldFor(actor, skill, centre, target);
                else if (skill.Burst) Burst(actor, skill, centre);
                else if (skill.Line) { if (target != null) LineShot(actor, target, skill); }
                else if (target != null && target.Alive && (SkillRaw(actor, skill) > 0 || (skill.Status != null && skill.Status.Kind != SoulStatus.None) || (skill.Extra != null && skill.Extra.Kind != SoulStatus.None) || skill.Pull > 0 || skill.Dispel || skill.Sap > 0))
                    ChainHit(actor, target, skill);
                if (skill.Retreat > 0 && target != null) StepAway(actor, target.Position, skill.Retreat);
                return default; // hits are reported inside; callers must not report the target twice
            }
            if (!skill.LeapToTarget)
            {
                cast = SoulCombat.TrySkill(actor, target, skill, random, out SoulHitResult result);
                return result;
            }
            cast = actor.Cooldown <= 0 && SoulSkillUsePolicy.TryCommit(actor, skill, target);
            if (!cast) return default;
            SoulCombat.ApplySelfBuffs(actor, skill);
            Leap(actor, target, skill);
            return default; // hits are reported inside; callers must not report the target twice
        }

        // A leap next to the target (behind it for 그림자 습격) and, heavy enough, the shock of the landing.
        void Leap(SoulCombatant actor, SoulCombatant target, SoulActiveSkillData skill)
        {
            if (target != null)
            {
                Vector2 direction = target.Position - actor.Position;
                float gap = actor.Stats.Radius + target.Stats.Radius + .05f;
                if (direction.magnitude > gap)
                {
                    Vector2 landing = skill.Behind ? target.Position + direction.normalized * gap : target.Position - direction.normalized * gap; // 그림자 습격: behind it
                    Vector2 step = (landing - actor.Position) / 12f;
                    for (int i = 0; i < 12 && Map.Clear(actor.Position + step, actor.Stats.Radius); i++) actor.Position += step;
                    routes[actor].Valid = false;
                    Emit(SoulEventKind.Leap, actor, target);
                }
            }
            // Design 5 "도약 충격파": the landing only shakes the ground when the body is heavy enough.
            if (actor.Stats.Total(StatType.BodyWeight) < skill.WeightThreshold) return;
            float radius = Mathf.Max(.5f, skill.Radius.Evaluate(actor.Stats.Combat));
            float raw = skill.Damage.Evaluate(actor.Stats.Combat) * SoulSkillUsePolicy.Power(actor, skill);
            CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Shockwave, Actor = actor, Amount = radius });
            var foes = new List<SoulCombatant>();
            if (actor is SoulMercenary) foes.AddRange(nearby); else foes.AddRange(Shared.Heroes);
            foreach (var foe in foes)
            {
                if (!foe.Alive || Vector2.Distance(foe.Position, actor.Position) > radius + foe.Stats.Radius) continue;
                var hit = SoulCombat.Resolve(actor, foe, new SoulHit
                { School = skill.DamageSchool, Kind = skill.DamageKind, Raw = raw, Knockback = skill.Knockback, Melee = true }, random);
                ReportHit(actor, foe, hit); // credits the kill too
            }
        }

        // ── traits that read the fight around them (용병 특징) ──
        // 결투 (one foe near: stronger) · 검투장 (every foe near: stronger) · 월광 (a dark floor: sharper) · 왕의 위엄 (fear
        // on those close) · 숲의 숨결 (regeneration round the one who has it) · 성벽의 맹세 (a share of the others' blows).
        public const float DuelReach = 4f, CrowdReach = 3f, AuraReach = 4f, AegisReach = 5f, DarkFloor = .95f;
        const float AuraHold = .45f;
        float auraClock;

        void TickAuras(float dt)
        {
            auraClock -= dt;
            if (auraClock > 0) return;
            auraClock = .25f;
            foreach (var hero in Mercenaries)
            {
                if (!hero.Alive) continue;
                var stats = hero.Stats;
                float duel = stats.Total(StatType.DuelFocus), crowd = stats.Total(StatType.CrowdFury), fear = stats.Total(StatType.FearAura);
                if (duel > 0 || crowd > 0 || fear > 0)
                {
                    int near = 0, close = 0;
                    foreach (var foe in nearby)
                    {
                        if (!foe.Alive) continue;
                        float distance = Vector2.Distance(foe.Position, hero.Position);
                        if (distance <= DuelReach) near++;
                        if (distance > CrowdReach) continue;
                        close++;
                        if (fear > 0 && random.NextDouble() < fear * .25f) SoulCombat.ApplyStatus(foe, SoulStatus.Fear, 1, 2f, 0, random);
                    }
                    float share = (duel > 0 && near == 1 ? duel : 0) + crowd * Mathf.Min(5, close);
                    hero.AddTimedBuff("trait_fury", null, 0); // off first: the bonus is of the attack without it
                    if (share > 0) hero.AddTimedBuff("trait_fury", new[] { new SoulStatBonus { Stat = StatType.Attack, Value = stats.Total(StatType.Attack) * share } }, AuraHold);
                }
                if (stats.Total(StatType.DarkSight) > 0 && Theme.Vision < DarkFloor)
                    hero.AddTimedBuff("trait_dark", new[] { new SoulStatBonus { Stat = StatType.CritChance, Value = .15f }, new SoulStatBonus { Stat = StatType.Accuracy, Value = .1f } }, AuraHold);
                float regen = stats.Total(StatType.RegenAura), guard = stats.Total(StatType.GuardAura);
                if (regen <= 0 && guard <= 0) continue;
                foreach (var ally in Mercenaries)
                {
                    if (!ally.Alive) continue;
                    float distance = Vector2.Distance(ally.Position, hero.Position);
                    if (regen > 0 && distance <= AuraReach)
                        ally.AddTimedBuff("trait_regen:" + hero.CombatId, new[] { new SoulStatBonus { Stat = StatType.HpRegen, Value = regen } }, AuraHold);
                    if (guard > 0 && ally != hero && distance <= AegisReach)
                    {
                        ally.Guardian = hero; ally.GuardShare = Mathf.Min(.6f, guard); ally.GuardBuff = "trait_aegis:" + hero.CombatId;
                        ally.AddTimedBuff(ally.GuardBuff, new[] { new SoulStatBonus { Stat = StatType.Armor, Value = 0 } }, AuraHold);
                    }
                }
            }
        }

        // ── skills that warn (예고) ──
        // Something that harms the other side: a blow, a status, a pull, a theft, a field of theirs.
        public static bool Harmful(SoulActiveSkillData skill)
            => (skill.Damage != null && (skill.Damage.Flat > 0 || HasTerms(skill.Damage))) || (skill.Status != null && skill.Status.Kind != SoulStatus.None)
               || (skill.Extra != null && skill.Extra.Kind != SoulStatus.None) || skill.Pull > 0 || skill.Dispel || skill.Sap > 0
               || skill.Field == SoulFieldKind.Trap || skill.Field == SoulFieldKind.Hazard || skill.Field == SoulFieldKind.Aura;
        static bool HasTerms(SoulValue value) => value != null && value.Terms != null && value.Terms.Length > 0;

        // A monster always winds up; a mercenary's skill only when it is not marked instant (a leap is its own tell).
        // Attack magic (Trigger Cast) has its own warning circle already.
        public static bool Warned(SoulCombatant actor, SoulActiveSkillData skill)
        {
            if (skill == null || skill.Trigger == SoulTrigger.Cast || !Harmful(skill)) return false;
            if (actor is SoulMonster) return true;
            return !skill.Instant && !skill.LeapToTarget;
        }

        // How long the warning stands: the skill's cast time (action speed applied) — a monster's at least MonsterWindUp.
        public const float MonsterWindUp = .7f, HeroWindUp = .4f;
        public static float SkillWindUp(SoulCombatant actor, SoulActiveSkillData skill)
            => Mathf.Max(actor is SoulMonster ? MonsterWindUp : HeroWindUp, SoulSkillUsePolicy.ActionTime(skill) / SoulCombat.ActionSpeed(actor));

        // A breath (a wide line) fans out as a cone.
        public static bool Breath(SoulActiveSkillData skill) => skill.Line && skill.Width >= 1f;
        public static float BreathAngle(SoulActiveSkillData skill) => Mathf.Clamp(skill.Width * 40f, 40f, 120f);

        SoulTelegraph SkillTelegraph(SoulCombatant actor, SoulCombatant target, SoulActiveSkillData skill)
        {
            var stats = actor.Stats.Combat;
            float radius = Mathf.Max(.5f, skill.Radius != null ? skill.Radius.Evaluate(stats) : 0);
            var t = new SoulTelegraph { Skill = skill, Name = skill.SkillName, WindUp = SkillWindUp(actor, skill), Hits = 1,
                Magic = SoulSkillUsePolicy.Magical(skill) || skill.DamageSchool == SoulDamageSchool.Magic };
            if (skill.Line)
            {
                float range = SoulSkillUsePolicy.Range(actor, skill);
                if (Breath(skill)) { t.Shape = SoulAreaShape.Cone; t.Anchor = SoulAreaAnchor.Forward; t.Size = range; t.Width = BreathAngle(skill); }
                else { t.Shape = SoulAreaShape.Box; t.Anchor = SoulAreaAnchor.Forward; t.Size = range; t.Width = 2 * (skill.Width > 0 ? skill.Width : LineWidth); }
            }
            else if (skill.Burst || skill.Field != SoulFieldKind.None)
            {
                t.Shape = SoulAreaShape.Circle; t.Anchor = skill.AtTarget ? SoulAreaAnchor.Target : SoulAreaAnchor.Self; t.Size = radius;
            }
            else
            {
                // one target (a chain starts there; a leap lands there): a small circle on it
                t.Shape = SoulAreaShape.Circle; t.Anchor = SoulAreaAnchor.Target; t.Size = skill.LeapToTarget ? radius : .8f;
            }
            return t;
        }

        // The warning lands: the skill's own effect on whoever is still inside.
        void StrikeSkill(SoulTelegraph t)
        {
            var skill = t.Skill;
            var actor = t.Caster;
            CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Skill, Actor = actor, Target = t.Target, Label = skill.SkillName, Magic = t.Magic });
            if (skill.LeapToTarget) { Leap(actor, t.Target, skill); Fx(actor, t.Target, skill, SoulFxPhase.Land, actor.Position); return; }
            if (skill.Field != SoulFieldKind.None)
            {
                var spot = skill.AtTarget ? t.Origin : actor.Position;
                PlaceFieldFor(actor, skill, spot, t.Target);
                Fx(actor, t.Target, skill, SoulFxPhase.Land, spot);
                return;
            }
            Fx(actor, t.Target, skill, SoulFxPhase.Land, t.Shape == SoulAreaShape.Circle ? t.CircleCenter : actor.Position,
                skill.Line ? t.Size : skill.Burst || skill.AtTarget ? t.Size : 0, t.Direction);
            var inside = FoesOf(actor).FindAll(foe => foe.Alive && t.Contains(foe.Position, foe.Stats.Radius));
            float raw = SkillRaw(actor, skill);
            Vector2 centre = t.Shape == SoulAreaShape.Circle ? t.CircleCenter : actor.Position;
            if (skill.Burst || skill.Line)
            {
                if (skill.Burst) CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Shockwave, Actor = actor, Amount = t.Size });
                foreach (var foe in inside) SkillHit(actor, foe, skill, raw, skill.Burst && !skill.AtTarget, centre);
            }
            else
            {
                SoulCombatant first = t.Target != null && inside.Contains(t.Target) ? t.Target : null;
                if (first == null) foreach (var foe in inside) if (first == null || Vector2.Distance(foe.Position, t.Origin) < Vector2.Distance(first.Position, t.Origin)) first = foe;
                if (first != null)
                {
                    if (skill.Chain > 0) ChainHit(actor, first, skill);
                    else SkillHit(actor, first, skill, raw, SoulSkillUsePolicy.Range(actor, skill) < 2.5f);
                }
            }
            if (skill.Retreat > 0 && t.Target != null) StepAway(actor, t.Target.Position, skill.Retreat);
        }

        // A burst around the caster (칼날 폭풍, 포효, 연막탄 …): every foe within the radius takes the hit and its status;
        // a burst with no damage only tries the status (fear, confusion).
        // A skill that goes its own way (areas, lines, chains, fields, and whatever a monster's soul does to a foe).
        static bool Special(SoulActiveSkillData skill)
            => skill.Burst || skill.Line || skill.Chain > 0 || skill.Field != SoulFieldKind.None || skill.AtTarget || skill.Pull > 0 || skill.Drain > 0
               || skill.Sap > 0 || skill.Steal > 0 || skill.Dispel || skill.Retreat > 0 || (skill.Extra != null && skill.Extra.Kind != SoulStatus.None)
               || (skill.Cleanse && skill.Trigger != SoulTrigger.AllyHurt);

        void Burst(SoulCombatant actor, SoulActiveSkillData skill, Vector2 centre)
        {
            float radius = Mathf.Max(.5f, skill.Radius.Evaluate(actor.Stats.Combat));
            CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Shockwave, Actor = actor, Amount = radius });
            float raw = SkillRaw(actor, skill);
            foreach (var foe in FoesOf(actor))
                if (foe.Alive && Vector2.Distance(foe.Position, centre) <= radius + foe.Stats.Radius) SkillHit(actor, foe, skill, raw, !skill.AtTarget, centre);
        }

        // Steps a unit away from a point (박쥐 변신), stopping at a wall.
        void StepAway(SoulCombatant unit, Vector2 from, float distance)
        {
            Vector2 direction = unit.Position - from;
            direction = direction.sqrMagnitude > .0001f ? direction.normalized : -unit.Facing;
            Vector2 step = direction * (distance / 12f);
            for (int i = 0; i < 12 && Map.Clear(unit.Position + step, unit.Stats.Radius); i++) unit.Position += step;
            if (routes.TryGetValue(unit, out var route)) route.Valid = false;
        }

        // Drags a unit toward a point (혀 휘감기, 소용돌이), stopping at a wall or at the point.
        void DragToward(SoulCombatant unit, Vector2 point, float distance)
        {
            Vector2 gap = point - unit.Position;
            float room = gap.magnitude - unit.Stats.Radius - .3f;
            if (room <= 0) return;
            Vector2 step = gap.normalized * (Mathf.Min(distance, room) / 12f);
            for (int i = 0; i < 12 && Map.Clear(unit.Position + step, unit.Stats.Radius); i++) unit.Position += step;
            if (routes.TryGetValue(unit, out var route)) route.Valid = false;
        }

        List<SoulCombatant> FoesOf(SoulCombatant actor)
        {
            var foes = new List<SoulCombatant>();
            if (actor is SoulMercenary) foes.AddRange(nearby); else foes.AddRange(Shared.Heroes);
            return foes;
        }

        List<SoulCombatant> AlliesOf(SoulCombatant actor)
        {
            var allies = new List<SoulCombatant>();
            if (actor is SoulMercenary) allies.AddRange(Mercenaries); else allies.AddRange(nearby);
            return allies;
        }

        static float SkillRaw(SoulCombatant actor, SoulActiveSkillData skill)
            => skill.Damage != null ? skill.Damage.Evaluate(actor.Stats.Combat) * SoulSkillUsePolicy.Power(actor, skill) : 0;

        // One foe caught by a skill's area, line, chain or field: the blow and its status — or, with no blow, the status.
        void SkillHit(SoulCombatant actor, SoulCombatant foe, SoulActiveSkillData skill, float raw, bool melee, Vector2? centre = null)
        {
            var stats = actor.Stats.Combat;
            bool landed = true;
            if (raw > 0)
            {
                var hit = new SoulHit { School = skill.DamageSchool, Kind = skill.DamageKind, Raw = raw, Knockback = skill.Knockback, Melee = melee };
                hit.SetStatus(skill.Status, stats);
                hit.SetStatus(skill.Extra, stats);
                if (SoulSkillUsePolicy.Magical(skill)) hit.Empower(SoulSkillUsePolicy.SpellPower(actor));
                var result = SoulCombat.Resolve(actor, foe, hit, random);
                landed = !result.Dodged;
                if (landed) Fx(actor, foe, skill, SoulFxPhase.Victim, foe.Position);
                // 흡수: the blow feeds the one who struck it
                if (landed && skill.Drain > 0 && actor.Alive) actor.Hp = Mathf.Min(actor.Stats.Total(StatType.MaxHp), actor.Hp + result.Damage * skill.Drain);
                ReportHit(actor, foe, result); // credits the kill too
            }
            else
            {
                Fx(actor, foe, skill, SoulFxPhase.Victim, foe.Position);
                foreach (var apply in new[] { skill.Status, skill.Extra })
                    if (apply != null && apply.Kind != SoulStatus.None)
                        SoulCombat.ApplyStatus(foe, apply.Kind, apply.Chance.Evaluate(stats), apply.Duration.Evaluate(stats), apply.DamagePerSecond.Evaluate(stats), random,
                            Mathf.Min(SoulCombat.MaxGrade, apply.GradeFor(stats) + Mathf.FloorToInt(actor.Stats.Total(StatType.StatusGrade))));
            }
            if (!landed) return;
            if (skill.Sap > 0)
            {
                // 영혼 착취: its breath and its magic, taken
                float mp = foe.Mp * skill.Sap, stamina = foe.Stamina * skill.Sap;
                foe.Mp -= mp; foe.Stamina -= stamina;
                actor.Mp = Mathf.Min(actor.Stats.Total(StatType.MaxMp), actor.Mp + mp);
                actor.Stamina = Mathf.Min(actor.Stats.Total(StatType.MaxStamina), actor.Stamina + stamina);
            }
            if (skill.Dispel && foe.TimedBuffs.Count > 0) foreach (var buff in foe.TimedBuffs.ToArray()) foe.AddTimedBuff(buff.Id, null, 0); // 공허
            if (skill.Steal > 0 && actor is SoulMercenary) Gold += skill.Steal; // 소매치기
            if (skill.Pull > 0 && foe.Alive) DragToward(foe, centre ?? actor.Position, skill.Pull);
        }

        // 관통 사격: everyone on the line from the caster through the target, as far as the skill reaches.
        public const float LineWidth = .5f;
        void LineShot(SoulCombatant actor, SoulCombatant target, SoulActiveSkillData skill)
        {
            Vector2 from = actor.Position, direction = (target.Position - from).normalized;
            if (direction == Vector2.zero) direction = actor.Facing;
            actor.Facing = direction;
            float length = SoulSkillUsePolicy.Range(actor, skill), raw = SkillRaw(actor, skill), width = skill.Width > 0 ? skill.Width : LineWidth;
            foreach (var foe in FoesOf(actor))
            {
                if (!foe.Alive) continue;
                Vector2 offset = foe.Position - from;
                float along = Vector2.Dot(offset, direction);
                if (along < 0 || along > length + foe.Stats.Radius) continue;
                if ((offset - direction * along).magnitude > width + foe.Stats.Radius) continue;
                SkillHit(actor, foe, skill, raw, false);
            }
        }

        // 연쇄 번개: the target, then the nearest foe not yet struck (within ChainReach), a quarter weaker each jump.
        public const float ChainReach = 3.5f, ChainFalloff = .75f;
        void ChainHit(SoulCombatant actor, SoulCombatant target, SoulActiveSkillData skill)
        {
            var struck = new HashSet<SoulCombatant>();
            var current = target;
            float raw = SkillRaw(actor, skill);
            var foes = FoesOf(actor);
            SoulCombatant previous = actor;
            int jumps = skill.Chain + Mathf.FloorToInt(actor.Stats.Total(StatType.ChainBonus)); // 뇌운
            for (int jump = 0; jump <= jumps && current != null; jump++)
            {
                CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.SkillFx, Actor = previous, Target = current, Skill = skill, Phase = SoulFxPhase.Link,
                    Point = current.Position, Magic = true });
                previous = current;
                SkillHit(actor, current, skill, raw, false);
                struck.Add(current);
                raw *= ChainFalloff;
                SoulCombatant next = null;
                float best = ChainReach;
                foreach (var foe in foes)
                {
                    if (!foe.Alive || struck.Contains(foe)) continue;
                    float distance = Vector2.Distance(foe.Position, current.Position);
                    if (distance < best) { best = distance; next = foe; }
                }
                current = next;
            }
        }

        // ── fields: traps, sanctuaries, auras ──

        public sealed class SoulField
        {
            public SoulCombatant Owner;
            public SoulActiveSkillData Skill;
            public Vector2 Position;
            public float Radius, Remaining, Pulse;
            public int Seat; // Spirit: which side of its caller
        }
        readonly List<SoulField> fields = new List<SoulField>();
        public int FieldCount => fields.Count;
        public IReadOnlyList<SoulField> Fields => fields;

        // 소환: at most SpiritCap spirits at a caller's side (a new one sends the oldest away; the same one is renewed).
        public const int SpiritCap = 2;
        public int SpiritsOf(SoulCombatant owner) => fields.FindAll(f => f.Owner == owner && f.Skill.Field == SoulFieldKind.Spirit).Count;
        bool SpiritWanted(SoulCombatant owner, SoulActiveSkillData skill)
            => !fields.Exists(f => f.Owner == owner && f.Skill == skill) && SpiritsOf(owner) < SpiritCap;
        static Vector2 SpiritSpot(SoulCombatant owner, int seat) => owner.Position + new Vector2(seat == 0 ? -.8f : .8f, 0);

        // A lined field (화염 벽): patches from in front of the caster toward the target, Range long, stopping at a wall.
        void PlaceFieldFor(SoulCombatant actor, SoulActiveSkillData skill, Vector2 position, SoulCombatant target)
        {
            if (!(skill.Line && skill.Field == SoulFieldKind.Hazard && target != null)) { PlaceField(actor, skill, position); return; }
            Vector2 way = target.Position - actor.Position;
            way = way.sqrMagnitude > 1e-4f ? way.normalized : actor.Facing;
            float patch = Mathf.Max(.5f, skill.Radius.Evaluate(actor.Stats.Combat)), length = SoulSkillUsePolicy.Range(actor, skill);
            for (float along = 1f; along <= length; along += patch * 1.5f)
            {
                var spot = actor.Position + way * along;
                if (!Map.Clear(spot, .2f)) break;
                PlaceField(actor, skill, spot);
            }
        }

        void PlaceField(SoulCombatant actor, SoulActiveSkillData skill, Vector2 position)
        {
            var stats = actor.Stats.Combat;
            float power = SoulSkillUsePolicy.Magical(skill) ? SoulSkillUsePolicy.SpellPower(actor) : 1f;
            float radius = Mathf.Max(.5f, skill.Radius.Evaluate(stats));
            int seat = 0;
            if (skill.Field == SoulFieldKind.Spirit)
            {
                fields.RemoveAll(f => f.Owner == actor && f.Skill == skill);
                var mine = fields.FindAll(f => f.Owner == actor && f.Skill.Field == SoulFieldKind.Spirit);
                if (mine.Count >= SpiritCap) { mine.Sort((a, b) => a.Remaining.CompareTo(b.Remaining)); fields.Remove(mine[0]); mine.RemoveAt(0); }
                while (mine.Exists(f => f.Seat == seat)) seat++;
                position = SpiritSpot(actor, seat);
            }
            fields.Add(new SoulField { Owner = actor, Skill = skill, Position = position, Radius = radius, Seat = seat, Pulse = skill.Field == SoulFieldKind.Spirit ? .5f : 0,
                Remaining = Mathf.Max(1, skill.Duration.Evaluate(stats)) * SoulSkillUsePolicy.DurationScale(power) });
            CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Shockwave, Actor = actor, Amount = radius });
        }

        void TickFields(float dt)
        {
            for (int i = fields.Count - 1; i >= 0; i--)
            {
                var field = fields[i];
                field.Remaining -= dt;
                var kind = field.Skill.Field;
                if (field.Remaining <= 0 || ((kind == SoulFieldKind.Aura || kind == SoulFieldKind.Spirit) && !field.Owner.Alive)) { fields.RemoveAt(i); continue; }
                if (kind == SoulFieldKind.Aura) field.Position = field.Owner.Position;
                if (kind == SoulFieldKind.Spirit) { TickSpirit(field, dt); continue; }
                if (kind == SoulFieldKind.Trap)
                {
                    // springs on the first foe that steps in: everyone within it is caught
                    var caught = FoesOf(field.Owner).FindAll(foe => foe.Alive && Vector2.Distance(foe.Position, field.Position) <= field.Radius + foe.Stats.Radius);
                    if (caught.Count == 0) continue;
                    float raw = SkillRaw(field.Owner, field.Skill);
                    foreach (var foe in caught) SkillHit(field.Owner, foe, field.Skill, raw, true);
                    CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Shockwave, Actor = caught[0], Amount = field.Radius });
                    fields.RemoveAt(i);
                    continue;
                }
                field.Pulse -= dt;
                if (field.Pulse > 0) continue;
                field.Pulse = 1f;
                if (kind == SoulFieldKind.Sanctuary)
                {
                    float mend = field.Skill.Heal != null ? field.Skill.Heal.Evaluate(field.Owner.Stats.Combat) * SoulSkillUsePolicy.Power(field.Owner, field.Skill) : 0;
                    foreach (var ally in AlliesOf(field.Owner))
                        if (ally.Alive && Vector2.Distance(ally.Position, field.Position) <= field.Radius + ally.Stats.Radius)
                            ally.Hp = Mathf.Min(ally.Stats.Total(StatType.MaxHp), ally.Hp + mend);
                }
                else if (kind == SoulFieldKind.Aura || kind == SoulFieldKind.Hazard) // a hazard stays where it was laid (독액 웅덩이, 늪)
                {
                    float raw = SkillRaw(field.Owner, field.Skill);
                    foreach (var foe in FoesOf(field.Owner))
                        if (foe.Alive && Vector2.Distance(foe.Position, field.Position) <= field.Radius + foe.Stats.Radius) SkillHit(field.Owner, foe, field.Skill, raw, true);
                }
            }
        }

        // A spirit by its caller: now and then the nearest foe in reach and in sight takes its bolt — or, a healing
        // spirit, the most hurt ally in reach is mended.
        void TickSpirit(SoulField field, float dt)
        {
            field.Position = SpiritSpot(field.Owner, field.Seat);
            field.Pulse -= dt;
            if (field.Pulse > 0) return;
            field.Pulse = Mathf.Max(.3f, field.Skill.SpiritInterval / SoulCombat.ActionSpeed(field.Owner));
            var owner = field.Owner;
            var skill = field.Skill;
            bool heals = skill.Heal != null && skill.Heal.Evaluate(owner.Stats.Combat) > 0 && SkillRaw(owner, skill) <= 0;
            if (heals)
            {
                SoulCombatant worst = null;
                foreach (var ally in AlliesOf(owner))
                    if (ally.Alive && Health(ally) < .98f && Vector2.Distance(ally.Position, field.Position) <= field.Radius && (worst == null || Health(ally) < Health(worst))) worst = ally;
                if (worst == null) return;
                worst.Hp = Mathf.Min(worst.Stats.Total(StatType.MaxHp), worst.Hp + skill.Heal.Evaluate(owner.Stats.Combat) * SoulSkillUsePolicy.Power(owner, skill));
                CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.SkillFx, Actor = owner, Target = worst, Skill = skill, Phase = SoulFxPhase.Link, Point = field.Position, Magic = true, Support = true });
                return;
            }
            SoulCombatant prey = null;
            foreach (var foe in FoesOf(owner))
                if (foe.Alive && Vector2.Distance(foe.Position, field.Position) <= field.Radius && Map.Sight(field.Position, foe.Position)
                    && (prey == null || Vector2.Distance(foe.Position, field.Position) < Vector2.Distance(prey.Position, field.Position))) prey = foe;
            if (prey == null) return;
            CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.SkillFx, Actor = owner, Target = prey, Skill = skill, Phase = SoulFxPhase.Link, Point = field.Position, Magic = true });
            SkillHit(owner, prey, skill, SkillRaw(owner, skill), false);
        }

        // 지름길: retreating, far from the way out — once a floor a pathfinder leads the party out beside it.
        public const float ShortcutFrom = 10f;
        bool TryShortcut(SoulMercenary leader, Vector2 goal)
        {
            if (Vector2.Distance(leader.Position, goal) < ShortcutFrom) return false;
            foreach (var guide in Mercenaries)
            {
                if (!guide.Alive) continue;
                var skill = guide.ActiveSkills().Find(s => s != null && s.Shortcut);
                if (skill == null || usedThisFloor.Contains((guide, skill)) || !SoulSkillUsePolicy.TryCommit(guide, skill, null)) continue;
                usedThisFloor.Add((guide, skill));
                int n = 0;
                foreach (var hero in Mercenaries)
                {
                    if (!hero.Alive) continue;
                    hero.Position = Beside(goal, n++, hero.Stats.Radius);
                    if (routes.TryGetValue(hero, out var route)) route.Valid = false;
                }
                guide.Action = skill.SkillName;
                CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Skill, Actor = guide, Label = skill.SkillName, Support = true });
                foreach (var hero in Mercenaries) if (hero.Alive) Fx(guide, hero, skill, SoulFxPhase.Victim, hero.Position);
                Log($"{guide.Name}의 지름길 — 출구 근처로 빠져나왔습니다");
                return true;
            }
            return false;
        }

        // A free spot around a point (the n-th around it, then any), for someone to stand.
        Vector2 Beside(Vector2 point, int n, float radius)
        {
            for (int ring = 1; ring <= 3; ring++)
                for (int k = 0; k < 8; k++)
                {
                    float angle = (n * 2.4f + k * 45f * Mathf.Deg2Rad);
                    var spot = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (.9f * ring);
                    if (Map.Clear(spot, radius)) return spot;
                }
            return point;
        }

        // Design 5: a body between a ranged attacker and its target may take the shot instead.
        // Chance grows with how centered the blocker is on the line of fire; a Cover pattern boosts it.
        public SoulCombatant InterceptFor(SoulCombatant attacker, SoulCombatant target) => Intercept(attacker, target);

        SoulCombatant Intercept(SoulCombatant attacker, SoulCombatant target)
        {
            Vector2 from = attacker.Position, line = target.Position - from;
            float length = line.magnitude;
            if (length < .01f) return target;
            Vector2 direction = line / length;
            SoulCombatant best = null;
            float bestAlong = float.MaxValue, bestChance = 0;
            var allies = new List<SoulCombatant>();
            if (target is SoulMercenary) allies.AddRange(Mercenaries); else allies.AddRange(nearby);
            foreach (var ally in allies)
            {
                if (ally == target || !ally.Alive || ally.Disabled) continue;
                Vector2 offset = ally.Position - from;
                float along = Vector2.Dot(offset, direction);
                if (along <= .2f || along >= length - .2f) continue;
                float side = (offset - direction * along).magnitude, reach = ally.Stats.Radius * 1.25f;
                if (side >= reach || along >= bestAlong) continue;
                best = ally; bestAlong = along; bestChance = (1 - side / reach) * .75f;
            }
            if (best == null) return target;
            var cover = CoverPattern(best);
            if (cover != null) bestChance *= 1.6f;
            if (random.NextDouble() >= Mathf.Min(.95f, bestChance)) return target;
            if (cover != null) SoulCombat.Pay(best, cover);
            CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Cover, Actor = best, Target = target });
            return best;
        }

        static SoulPatternData CoverPattern(SoulCombatant unit)
        {
            foreach (var pattern in unit.Patterns())
                if (pattern != null && pattern.Category == SoulPatternCategory.Defense && pattern.DefenseMode == SoulDefenseMode.Cover && SoulCombat.CanPay(unit, pattern))
                    return pattern;
            return null;
        }

        // Monsters pick by distance divided by threat, so a taunting mercenary pulls attention.
        public const float DownThreatScale = 20f;
        SoulMercenary Threatened(SoulCombatant actor, float perception)
        {
            SoulMercenary best = null;
            float bestScore = float.MaxValue;
            foreach (var hero in Shared.Heroes)
            {
                if (!hero.Alive) continue;
                float distance = Vector2.Distance(actor.Position, hero.Position);
                if (distance >= perception || !Map.Sight(actor.Position, hero.Position)) continue;
                float score = distance / Mathf.Max(.2f, hero.Stats.Total(StatType.Threat));
                if (hero.DownTime > 0) score *= DownThreatScale; // on the ground: the last one a monster goes for
                if (score < bestScore) { bestScore = score; best = hero; }
            }
            return best;
        }

        readonly Dictionary<SoulCombatant, SoulMercenary> killers = new Dictionary<SoulCombatant, SoulMercenary>();

        void CreditKill(SoulCombatant actor, SoulCombatant target)
        {
            if (!(actor is SoulMercenary hero)) return;
            Kills[hero] = Kills.TryGetValue(hero, out int count) ? count + 1 : 1;
            var killTally = TallyOf(hero);
            if (killTally != null) killTally.For(hero).Kills++;
            if (target != null) killers[target] = hero;
        }

        void Reveal(SoulMercenary hero)
        {
            var origin = Map.Cell(hero.Position);
            if (lastReveal.TryGetValue(hero, out var last) && last == origin) return;
            lastReveal[hero] = origin;
            var traits = hero.MapTraits;
            // The floor decides how far anyone sees: a dark mine shortens it, a lit lake lengthens it.
            float vision = (VisionRadius + ((traits & SoulMapTrait.WideVision) != 0 ? 2.5f : 0)) * Theme.Vision;
            bool xray = (traits & SoulMapTrait.SeeThroughWalls) != 0, detect = (traits & SoulMapTrait.DetectHiddenDoors) != 0;
            int r = Mathf.CeilToInt(vision);
            bool changed = false;
            for (int y = origin.y - r; y <= origin.y + r; y++)
                for (int x = origin.x - r; x <= origin.x + r; x++)
                {
                    if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height) continue;
                    int index = y * Map.Width + x, d2 = (x - origin.x) * (x - origin.x) + (y - origin.y) * (y - origin.y);
                    if (d2 > vision * vision) continue;
                    var cell = new Vector2Int(x, y);
                    if (detect && d2 <= DetectRadius(hero) * DetectRadius(hero) && Map.Discover(cell))
                    {
                        Explored[index] = true;
                        ExploreLog.Add(index);
                        Seen[index] = true;
                        SeenLog.Add(index);
                        changed = true;
                        Log(hero.Name + ": 숨겨진 문 발견");
                        continue;
                    }
                    bool line = LineOfSight(origin, cell);
                    if (line && !Seen[index]) { Seen[index] = true; SeenLog.Add(index); changed = true; }
                    if (Explored[index] || (!xray && !line)) continue;
                    Explored[index] = true;
                    ExploreLog.Add(index);
                    changed = true;
                }
            if (changed) ExploreRevision++;
        }

        // Grid ray: walls block sight but are themselves seen.
        bool LineOfSight(Vector2Int from, Vector2Int to)
        {
            int dx = Mathf.Abs(to.x - from.x), dy = Mathf.Abs(to.y - from.y);
            int sx = from.x < to.x ? 1 : -1, sy = from.y < to.y ? 1 : -1, error = dx - dy;
            int x = from.x, y = from.y;
            while (x != to.x || y != to.y)
            {
                if ((x != from.x || y != from.y) && !Map.Transparent(x, y)) return false;
                int twice = 2 * error;
                if (twice > -dy) { error -= dy; x += sx; }
                if (twice < dx) { error += dx; y += sy; }
            }
            return true;
        }

        // Design 7.1: experiences become Will after the battle, with first-time and per-dungeon caps.
        void ApplyMentalGrowth()
        {
            foreach (var hero in Mercenaries)
            {
                if (!hero.Alive) continue;
                float gain = 0;
                var reasons = new List<string>();
                if (witnessedDeath.Contains(hero))
                {
                    float value = hero.Experiences.Add("ally_death") ? 2 : 1;
                    gain += value;
                    reasons.Add($"동료의 죽음 +{value}");
                }
                Kills.TryGetValue(hero, out int kills);
                int fromKills = Mathf.Min(2, kills / 5);
                if (fromKills > 0) { gain += fromKills; reasons.Add($"적 {kills}체 처치 +{fromKills}"); }
                hero.MentalStats.TryGetValue(StatType.Will, out float current);
                gain = Mathf.Min(gain, MaxMentalWill - current);
                if (gain <= 0) continue;
                hero.MentalStats[StatType.Will] = current + gain;
                hero.Rebuild(rules);
                MentalReport.Add($"{hero.Name} 정신력 +{gain:0} ({string.Join(", ", reasons)})");
            }
        }

        public const float MaxMentalWill = 10f;

        // Null when the soul can be absorbed, otherwise why not (for the UI).
        public string AbsorbBlockReason(SoulMercenary hero, SoulDrop drop)
        {
            if (hero == null || !hero.Alive) return "쓰러진 용병은 흡수할 수 없습니다.";
            if (drop.Soul.CorePattern != null && hero.CoreBlocked(drop.Soul))
                return $"핵심 패턴 '{drop.Soul.CorePattern.Id}'을(를) {hero.Race.Id}이(가) 쓸 수 없어 흡수 불가";
            if (!hero.HasFreeSoulSlot) return $"영혼 슬롯 부족 ({hero.Souls.Count}/{hero.SoulSlots}) — 레벨 {(hero.Level / SoulMercenary.MilestoneLevels + 1) * SoulMercenary.MilestoneLevels}에 열립니다.";
            return null;
        }

        // Knockback, status log and presentation events of a resolved hit (no trigger queueing).
        void ReportHit(SoulCombatant actor, SoulCombatant target, SoulHitResult hit)
        {
            if (target == null) return;
            // hit from beyond its own sight range (a caster, an archer): it goes after the attacker
            if (target is SoulMonster && actor is SoulMercenary && (provoked.ContainsKey(target) || Vector2.Distance(actor.Position, target.Position) > Perception(target)))
                provoked[target] = (actor, clock);
            if (hit.Dodged)
            {
                Emit(SoulEventKind.Miss, actor, target);
                return;
            }
            if (hit.Guarded) Emit(SoulEventKind.Guard, actor, target);
            if (hit.Countered) CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Counter, Actor = target, Target = actor, Amount = hit.CounterDamage });
            if (hit.Killed) CreditKill(actor, target);
            if (hit.Damage > 0) CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Hit, Actor = actor, Target = target, Amount = hit.Damage, Critical = hit.Critical });
            if (hit.Knockback > 0) { Push(actor, target, hit.Knockback); Emit(SoulEventKind.Knockback, actor, target); }
            if (hit.Applied != SoulStatus.None)
            {
                CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Status, Actor = actor, Target = target, Status = hit.Applied });
                Log(Name(target) + " " + SoulCombat.StatusName(hit.Applied));
            }
        }

        void Emit(SoulEventKind kind, SoulCombatant actor, SoulCombatant target, SoulPatternData pattern = null, string label = null)
            => CombatEvents.Add(new SoulCombatEvent { Kind = kind, Actor = actor, Target = target, Pattern = pattern, Label = label });

        // Knockback moves in small steps and stops at walls; the pushed unit re-plans its route.
        void Push(SoulCombatant from, SoulCombatant target, float distance)
        {
            Vector2 direction = target.Position - from.Position;
            direction = direction.sqrMagnitude < .0001f ? Vector2.right : direction.normalized;
            for (float moved = 0; moved < distance; moved += .1f)
            {
                var next = target.Position + direction * Mathf.Min(.1f, distance - moved);
                if (!Map.Clear(next, target.Stats.Radius, forced: true)) break;
                target.Position = next;
            }
            // Knocked through a hidden door: the door is found (and now walkable for everyone).
            if (Map.Tile(Map.Cell(target.Position)) == SoulTile.HiddenDoor && Map.Discover(Map.Cell(target.Position)))
            {
                int door = Map.Cell(target.Position).y * Map.Width + Map.Cell(target.Position).x;
                Explored[door] = true;
                ExploreLog.Add(door);
                Seen[door] = true;
                SeenLog.Add(door);
                ExploreRevision++;
                Log("숨겨진 문이 열렸습니다");
            }
            target.MoveTime = 0;
            routes[target].Valid = false;
        }

        // 구르기: the body rolls away from the attacker — straight back or diagonally back, whichever has room
        // (walls stop it like knockback, but it never passes hidden doors).
        void RollAway(SoulCombatant unit, SoulCombatant from, float distance)
        {
            Vector2 back = unit.Position - from.Position;
            back = back.sqrMagnitude < 1e-4f ? Vector2.right : back.normalized;
            Vector2 best = unit.Position;
            float bestGain = 0;
            foreach (float angle in new[] { 0f, 45f, -45f, 90f, -90f })
            {
                Vector2 direction = Quaternion.Euler(0, 0, angle) * back;
                Vector2 at = unit.Position;
                for (float moved = 0; moved < distance; moved += .1f)
                {
                    var next = at + direction * Mathf.Min(.1f, distance - moved);
                    if (!Map.Clear(next, unit.Stats.Radius)) break;
                    at = next;
                }
                float gain = Vector2.Distance(at, unit.Position) * (1 - Mathf.Abs(angle) / 360f);
                if (gain > bestGain + .05f) { bestGain = gain; best = at; }
            }
            unit.Position = best;
            unit.MoveTime = 0;
            routes[unit].Valid = false;
        }

        static string Name(SoulCombatant unit) => unit is SoulMercenary hero ? hero.Name : unit is SoulMonster monster ? monster.Data.Name : unit.CombatId;

        static bool HealingBlessing(SoulActiveSkillData skill)
        {
            if (skill.Heal != null && (skill.Heal.Flat > 0 || skill.Heal.Terms != null && skill.Heal.Terms.Length > 0)) return true;
            if (skill.SelfBuffs != null) foreach (var buff in skill.SelfBuffs) if (buff.Stat == StatType.HpRegen) return true;
            return false;
        }

        // Who a 신성한 방패 goes on: in a fight, within reach, without one already — whoever something is attacking
        // (the most hurt of them), else the most hurt ally below 70%.
        SoulCombatant ShieldTarget(SoulCombatant actor, float reach)
        {
            if (actor is SoulMercenary && !partyFighting) return null;
            SoulCombatant best = null; bool bestHunted = false;
            foreach (SoulCombatant ally in actor is SoulMercenary ? (IEnumerable<SoulCombatant>)Mercenaries : nearby)
            {
                if (!ally.Alive || Vector2.Distance(ally.Position, actor.Position) > reach) continue;
                if (ally.Barrier > 0 && ally.TimedBuffs.Exists(b => b.Id == SoulCombat.BarrierKey)) continue;
                bool hunted = false;
                foreach (var monster in nearby) hunted |= monster.Alive && monster.CurrentTarget == ally;
                if (!hunted && Health(ally) >= .7f) continue;
                if (best == null || hunted && !bestHunted || hunted == bestHunted && Health(ally) < Health(best)) { best = ally; bestHunted = hunted; }
            }
            return best;
        }

        // What is still worth casting while running: a heal, or a blessing that heals, regenerates or speeds up.
        static bool FleeingAid(SoulActiveSkillData skill)
        {
            if (skill.Trigger == SoulTrigger.AllyHurt) return true;
            if (skill.Trigger != SoulTrigger.AllySupport) return false;
            if (skill.Heal != null && (skill.Heal.Flat > 0 || skill.Heal.Terms != null && skill.Heal.Terms.Length > 0)) return true;
            if (skill.SelfBuffs != null)
                foreach (var buff in skill.SelfBuffs)
                    if (buff.Stat == StatType.MoveSpeed || buff.Stat == StatType.HpRegen || buff.Stat == StatType.StaminaRegen) return true;
            return false;
        }

        bool TryAutomaticSkill(SoulCombatant actor, SoulCombatant target, bool marching = false, bool fleeing = false)
        {
            foreach (var skill in actor.ActiveSkills())
            {
                if (fleeing && (skill == null || !FleeingAid(skill))) continue;
                if (skill != null && skill.Field == SoulFieldKind.Spirit && !SpiritWanted(actor, skill)) continue;
                if (skill != null && skill.Trigger == SoulTrigger.AllyHurt)
                {
                    float reach = SoulSkillUsePolicy.Range(actor, skill);
                    var casterStats = actor.Stats.Combat;
                    if (skill.Barrier != null && skill.Barrier.Evaluate(casterStats) > 0)
                    {
                        // 신성한 방패: on the ally in the thick of it (something on it, else the most hurt), none of whom has one
                        var guarded = ShieldTarget(actor, reach);
                        if (guarded == null || !SoulSkillUsePolicy.TryCommit(actor, skill, guarded)) continue;
                        guarded.Barrier = skill.Barrier.Evaluate(casterStats) * SoulSkillUsePolicy.Power(actor, skill);
                        guarded.AddTimedBuff(SoulCombat.BarrierKey, new[] { new SoulStatBonus { Stat = StatType.Armor, Value = 2 } }, Mathf.Max(1, skill.Duration.Evaluate(casterStats)));
                        Fx(actor, guarded, skill, SoulFxPhase.Victim, guarded.Position);
                        actor.Action = skill.SkillName;
                        CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Skill, Actor = actor, Target = guarded, Label = skill.SkillName, Magic = true, Support = true });
                        return true;
                    }
                    // 정화: whoever carries the most statuses · 희생: never the caster itself
                    float mend = skill.Heal != null ? skill.Heal.Evaluate(actor.Stats.Combat) * SoulSkillUsePolicy.Power(actor, skill) : 0;
                    float lasting = Mathf.Max(1, skill.Duration != null ? skill.Duration.Evaluate(casterStats) : 0);
                    if (skill.SelfBuffs != null) foreach (var buff in skill.SelfBuffs) if (buff.Stat == StatType.HpRegen) mend += buff.Amount.Evaluate(casterStats) * lasting; // 재생의 서약: what it heals over time
                    var patient = skill.Cleanse ? AfflictedAlly(actor, reach) : WeakestAlly(actor, reach, skill.HpCost > 0 ? actor : null, mend, skill.HealsWounds);
                    if (patient == null || !SoulSkillUsePolicy.TryCommit(actor, skill, patient)) continue;
                    if (skill.Cleanse) patient.Statuses.Clear(); // (희생's blood is paid in TryCommit)
                    Fx(actor, patient, skill, SoulFxPhase.Victim, patient.Position);
                    patient.Hp = Mathf.Min(patient.Stats.Total(StatType.MaxHp), patient.Hp + Mathf.Max(0, skill.Heal.Evaluate(actor.Stats.Combat) * SoulSkillUsePolicy.Power(actor, skill)));
                    // a level-3 heal closes two wounds
                    // (치유의 빛: a roll on the caster's 마력)
                    if (skill.HealsWounds && patient is SoulMercenary mended && (!skill.WoundRoll || random.NextDouble() < skill.WoundChance.Evaluate(actor.Stats.Combat)))
                        mended.HealWounds((SoulSkillUsePolicy.Level(actor, skill) >= 3 ? 2 : 1) + Mathf.FloorToInt(actor.Stats.Total(StatType.WoundMend)));
                    // a blessing laid on the one healed (재생의 서약: regeneration for a while)
                    if (skill.SelfBuffs != null && skill.SelfBuffs.Length > 0)
                    {
                        var laid = new SoulStatBonus[skill.SelfBuffs.Length];
                        for (int b = 0; b < laid.Length; b++) laid[b] = new SoulStatBonus { Stat = skill.SelfBuffs[b].Stat, Value = skill.SelfBuffs[b].Amount.Evaluate(casterStats) * SoulSkillUsePolicy.Power(actor, skill) };
                        patient.AddTimedBuff(SkillKey(skill), laid, lasting * (1 + Mathf.Max(0, actor.Stats.Total(StatType.BuffDuration))));
                    }
                    actor.Action = skill.SkillName;
                    CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Skill, Actor = actor, Target = patient, Label = skill.SkillName, Magic = true, Support = true });
                    return true;
                }
                if (skill != null && skill.Trigger == SoulTrigger.AllySupport)
                {
                    bool fighting = fleeing || (actor is SoulMercenary ? partyFighting : target != null);
                    var allies = SupportedAllies(actor, skill);
                    // a healing blessing (축복의 비, 축복의 기도) waits for someone around who needs it
                    if (HealingBlessing(skill) && !allies.Exists(ally => Health(ally) < .8f)) continue;
                    SoulSkillUsePolicy.Costs(actor, skill, out _, out float supportMana);
                    bool affordable = actor.Mp - supportMana >= actor.Stats.Total(StatType.MaxMp) * ManaReserve; // support never eats the reserve
                    if (!fighting || !affordable || allies.Count == 0 || !SoulSkillUsePolicy.TryCommit(actor, skill, null)) continue;
                    var stats = actor.Stats.Combat;
                    float power = SoulSkillUsePolicy.Magical(skill) ? SoulSkillUsePolicy.SpellPower(actor) : 1f;
                    float duration = skill.Duration.Evaluate(stats) * SoulSkillUsePolicy.DurationScale(power) * (1 + Mathf.Max(0, actor.Stats.Total(StatType.BuffDuration)));
                    var bonuses = new SoulStatBonus[skill.SelfBuffs.Length];
                    for (int i = 0; i < bonuses.Length; i++) bonuses[i] = new SoulStatBonus { Stat = skill.SelfBuffs[i].Stat, Value = skill.SelfBuffs[i].Amount.Evaluate(stats) * SoulSkillUsePolicy.Power(actor, skill) };
                    float mend = skill.Heal != null ? Mathf.Max(0, skill.Heal.Evaluate(stats) * SoulSkillUsePolicy.Power(actor, skill)) : 0;
                    Fx(actor, null, skill, SoulFxPhase.Land, actor.Position, skill.Radius != null ? skill.Radius.Evaluate(stats) : 5f);
                    foreach (var ally in allies)
                    {
                        Fx(actor, ally, skill, SoulFxPhase.Victim, ally.Position);
                        if (bonuses.Length > 0) ally.AddTimedBuff(SkillKey(skill), bonuses, duration);
                        if (skill.GuardShare > 0 && ally != actor) { ally.Guardian = actor; ally.GuardShare = skill.GuardShare; ally.GuardBuff = SkillKey(skill); } // 대신 맞기
                        if (mend > 0) ally.Hp = Mathf.Min(ally.Stats.Total(StatType.MaxHp), ally.Hp + mend); // 축복의 기도
                        if (skill.Imbue != null && skill.Imbue.Kind != SoulStatus.None)
                        {
                            ally.Imbue = skill.Imbue; ally.ImbueRemaining = duration; ally.ImbueName = skill.SkillName;
                            // the enchant burns / chills as hard as its caster's magic makes it
                            ally.ImbueChance = skill.Imbue.Chance.Evaluate(stats);
                            ally.ImbueDuration = skill.Imbue.Duration.Evaluate(stats) * SoulSkillUsePolicy.DurationScale(power);
                            ally.ImbueDps = skill.Imbue.DamagePerSecond.Evaluate(stats) * power;
                            ally.ImbueGrade = Mathf.Min(3, skill.Imbue.GradeFor(stats) + SoulSkillUsePolicy.GradeBonus(power));
                        }
                    }
                    actor.Action = skill.SkillName;
                    CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Skill, Actor = actor, Label = skill.SkillName, Magic = true, Support = true });
                    return true;
                }
                if (skill != null && skill.Trigger == SoulTrigger.AllyFallen)
                {
                    if (skill.OncePerFloor && usedThisFloor.Contains((actor, skill))) continue;
                    float reach = SoulSkillUsePolicy.Range(actor, skill);
                    var dead = new List<SoulMercenary>();
                    if (actor is SoulMercenary)
                        foreach (var ally in Mercenaries)
                            if (!ally.Alive && Vector2.Distance(ally.Position, actor.Position) <= reach) { dead.Add(ally); if (!skill.Mass) break; }
                    if (dead.Count == 0 || !SoulSkillUsePolicy.TryCommit(actor, skill, null)) continue;
                    if (skill.OncePerFloor) usedThisFloor.Add((actor, skill));
                    actor.Action = skill.SkillName;
                    CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Skill, Actor = actor, Target = dead[0], Label = skill.SkillName, Magic = true, Support = true });
                    Fx(actor, null, skill, SoulFxPhase.Land, actor.Position, reach);
                    foreach (var one in dead)
                    {
                        Fx(actor, one, skill, SoulFxPhase.Victim, one.Position);
                        one.Hp = Mathf.Max(1, one.Stats.Total(StatType.MaxHp) * Mathf.Min(1, skill.ReviveRatio * SoulSkillUsePolicy.Power(actor, skill)));
                        one.Statuses.Clear();
                        fallen.Remove(one);
                        CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Revive, Target = one });
                        Log($"{one.Name} 소생!");
                    }
                    // 기적: the living around are healed as well
                    float mend = skill.Mass && skill.Heal != null ? skill.Heal.Evaluate(actor.Stats.Combat) * SoulSkillUsePolicy.Power(actor, skill) : 0;
                    if (mend > 0)
                        foreach (var ally in Mercenaries)
                            if (ally.Alive && !dead.Contains(ally) && Vector2.Distance(ally.Position, actor.Position) <= reach)
                                ally.Hp = Mathf.Min(ally.Stats.Total(StatType.MaxHp), ally.Hp + mend);
                    return true;
                }
                if (skill == null || (skill.Trigger != SoulTrigger.InRange && skill.Trigger != SoulTrigger.LowHealth)) continue;
                if (marching && skill.LeapToTarget) continue; // a leap would pull the unit off its ordered route
                if (!SoulSkillUsePolicy.CanUse(actor, skill, target)) continue;
                if (skill.Trigger == SoulTrigger.InRange && SoulSkillUsePolicy.Magical(skill))
                {
                    // a spell outside the chant (연쇄 번개, 마나 보호막) keeps the MP reserve like any other
                    SoulSkillUsePolicy.Costs(actor, skill, out _, out float spellMana);
                    if (actor.Mp - spellMana < actor.Stats.Total(StatType.MaxMp) * ManaReserve) continue;
                }
                var hit = CastSkill(actor, target, skill, out _);
                actor.Action = skill.SkillName;
                Emit(SoulEventKind.Skill, actor, target, skill.RequiredPattern, skill.SkillName);
                QueueActive(actor, SoulTrigger.OnAttack, target);
                AfterHit(actor, target, hit);
                return true;
            }
            return false;
        }

        bool TryTriggeredSkill(TriggerEvent evt)
        {
            if (!evt.Owner.Alive) return true;
            foreach (var skill in evt.Owner.ActiveSkills())
            {
                if (skill == null || skill.Trigger != evt.Trigger) continue;
                if (evt.Owner.Cooldown > 0) return false;
                if (!SoulSkillUsePolicy.CanUse(evt.Owner, skill, evt.Target)) continue;
                var hit = CastSkill(evt.Owner, evt.Target, skill, out _);
                evt.Owner.Action = skill.SkillName;
                Emit(SoulEventKind.Skill, evt.Owner, evt.Target, skill.RequiredPattern, skill.SkillName);
                if (hit.Killed) QueueActive(evt.Owner, SoulTrigger.OnKill, evt.Target);
                ReportHit(evt.Owner, evt.Target, hit);
                return true;
            }
            return true;
        }

        // A goal that moved less than this keeps the current route (a chased target shifting a little).
        const float RetargetDistance = .25f;

        void Navigate(SoulCombatant actor, Vector2 goal, float dt, float speedScale = 1f, string label = "이동", bool free = false)
        {
            var route = routes[actor];
            float radius = actor.Stats.Radius;
            if ((goal - actor.Position).sqrMagnitude < .0004f)
            {
                actor.Action = IdleLabel(actor);
                if (actor is SoulMercenary there) ordered.Remove(there);
                return;
            }
            var points = route.Path.Points;
            bool stale = !route.Valid || points.Count == 0 || Mathf.Abs(route.Radius - radius) > .001f
                || (route.Goal - goal).sqrMagnitude > RetargetDistance * RetargetDistance
                || (route.Path.Partial && route.Step >= points.Count);
            if (stale)
            {
                Map.Plan(actor.Position, goal, radius, route.Path);
                route.Goal = goal; route.Radius = radius; route.Valid = true; route.Step = 0;
                // Careful: first back onto the grid cell the unit stands in, then waypoint by waypoint.
                var here = Map.NavCell(actor.Position);
                if (route.Careful && points.Count > 0 && Map.NavWalkable(here, radius) && (Map.NavCenter(here) - actor.Position).sqrMagnitude > .0004f)
                    points.Insert(0, Map.NavCenter(here));
            }
            // String pulling: skip waypoints while the next one is reachable in a straight line. Replanning
            // every time a chased target moves therefore never walks the unit back to a grid cell center;
            // that only happens when the straight line is blocked (off-grid next to a corner).
            for (int look = 0; look < 6 && !route.Careful && route.Step + 1 < points.Count; look++)
            {
                if (!Map.Straight(actor.Position, points[route.Step + 1], radius)) break;
                route.Step++;
            }
            if (route.Step >= points.Count)
            {
                actor.Action = points.Count == 0 ? "경로 없음" : IdleLabel(actor);
                if (actor is SoulMercenary arrived) ordered.Remove(arrived); // arrived (or unreachable): auto combat again
                return;
            }
            var movement = MovementPattern(actor);
            if (movement == null) { actor.Action = "이동 패턴 없음"; return; }
            float pace = actor is SoulMercenary together ? Pace(together) : 1f; // retreating: the slowest one's pace
            if (pace <= 0) { actor.Action = "동료 기다림"; return; }
            if (actor.MoveTime <= 0)
            {
                bool orderedWalk = free || (actor is SoulMercenary walker && ordered.Contains(walker));
                if ((actor.Cooldown > 0 && !free) || (!SoulCombat.CanPay(actor, movement) && !orderedWalk && actor.FreeSteps <= 0)) { actor.Action = "자원 회복"; return; }
                // the walk home costs no stamina: a party that spent it all on the way would meet the next monster empty
                bool homeward = actor is SoulMercenary home && Retreating && !standing && ordered.Contains(home);
                if (homeward) exhausted = false;
                else if (actor.FreeSteps > 0) { actor.FreeSteps--; exhausted = false; }
                else
                {
                    exhausted = !SoulCombat.CanPay(actor, movement);
                    if (!exhausted) SoulCombat.Pay(actor, movement);
                }
                actor.MoveTime = movement.ActionTime / SoulCombat.ActionSpeed(actor);
                actor.Cooldown = actor.MoveTime;
                if (exhausted) tired.Add(actor); else tired.Remove(actor);
            }
            // Out of stamina under a move order: a free, half-speed walk instead of standing still.
            bool slow = tired.Contains(actor);
            var next = points[route.Step];
            // out of stamina: half speed, more with 피로 무시
            float tiredShare = .5f + Mathf.Clamp(actor.Stats.Total(StatType.TirelessWalk), 0, .5f);
            var moved = Vector2.MoveTowards(actor.Position, next, SoulCombat.MoveSpeed(actor) * (slow ? tiredShare : 1f) * speedScale * pace * dt);
            // A unit outside an incoming area waits at its edge instead of walking into the hit.
            if (!free && Danger(actor, moved, radius + .1f, ReactionTime(actor)) && !Danger(actor, actor.Position, radius + .1f, ReactionTime(actor)))
            {
                actor.Action = "장판 대기";
                return;
            }
            if (!Map.Clear(moved, radius))
            {
                // Grazing a wall corner (pushed off the route by a body, a roll, a knockback): slide along the
                // wall with whatever part of the step is free, and plan again from there without a stop.
                Vector2 step = moved - actor.Position;
                Vector2 slideX = actor.Position + new Vector2(step.x, 0), slideY = actor.Position + new Vector2(0, step.y);
                bool okX = Mathf.Abs(step.x) > 1e-5f && Map.Clear(slideX, radius), okY = Mathf.Abs(step.y) > 1e-5f && Map.Clear(slideY, radius);
                if (okX && (!okY || Vector2.Distance(slideX, next) <= Vector2.Distance(slideY, next))) moved = slideX;
                else if (okY) moved = slideY;
                else
                {
                    // Really stuck: back onto the grid, then waypoint by waypoint. Only a repeated block shows.
                    blocked[actor] = blocked.TryGetValue(actor, out int count) ? count + 1 : 1;
                    route.Valid = false;
                    route.Careful = true;
                    actor.Action = blocked[actor] >= 3 ? "길 막힘" : label;
                    return;
                }
                route.Valid = false;
            }
            blocked.Remove(actor);
            if ((moved - actor.Position).sqrMagnitude > 1e-6f) actor.Facing = (moved - actor.Position).normalized;
            actor.Position = moved;
            // No progress toward the waypoint for a second (bodies in the way): sidestep and replan.
            float remaining = Vector2.Distance(moved, next);
            bool tracked = progress.TryGetValue(actor, out var track) && track.step == route.Step;
            if (tracked && remaining > track.best - .05f)
            {
                track.stuck += dt;
                if (track.stuck > 1f)
                {
                    Vector2 away = next - actor.Position;
                    Vector2 side = away.sqrMagnitude > 1e-4f ? new Vector2(-away.y, away.x).normalized : Vector2.right;
                    if (random.NextDouble() < .5) side = -side;
                    for (float step = .45f; step > .05f; step *= .5f)
                        if (Map.Clear(actor.Position + side * step, radius)) { actor.Position += side * step; break; }
                    route.Valid = false;
                    route.Careful = true;
                    progress.Remove(actor);
                }
                else progress[actor] = track;
            }
            else progress[actor] = (tracked ? Mathf.Min(remaining, track.best) : remaining, 0, route.Step);
            if (Vector2.Distance(moved, next) < .02f)
            {
                route.Step++;
                route.Careful = false; // reached a waypoint cleanly: shortcuts are safe again
                progress.Remove(actor);
            }
            actor.Action = slow ? "지친 걸음" : actor is SoulMercenary hero && ordered.Contains(hero) ? "이동 명령" : label;
        }

        readonly HashSet<SoulCombatant> tired = new HashSet<SoulCombatant>();
        readonly Dictionary<SoulCombatant, int> blocked = new Dictionary<SoulCombatant, int>();
        readonly Dictionary<SoulCombatant, (float best, float stuck, int step)> progress = new Dictionary<SoulCombatant, (float best, float stuck, int step)>();
        bool exhausted;

        // Nearest monster the mercenary can actually see (walls block sight).
        SoulMonster Nearest(SoulCombatant actor, float perception)
        {
            SoulMonster best = null;
            float distance = perception;
            foreach (var candidate in nearby)
            {
                if (!candidate.Alive || actor is SoulMercenary && (Avoided(candidate) || Ignored(candidate))) continue;
                float current = Vector2.Distance(actor.Position, candidate.Position);
                if (current < distance && Map.Sight(actor.Position, candidate.Position)) { distance = current; best = candidate; }
            }
            return best;
        }

        // Gold grows +20% a floor (1.2× on the second, 2.4× on the eighth).
        public float FloorGold => 1 + .2f * (Floor - 1);

        // Gold from a kill (what the party carries back and sells in the village): the monster's gold × FloorGold,
        // 0.8–1.2 at random, +1% a point of luck, 탐욕의; a guardian also leaves a purse of 50 × FloorGold.
        public int KillGold(SoulMonster monster, double roll)
        {
            float gold = monster.Data.Gold * FloorGold * (float)(.8 + .4 * roll) * (1 + PartyBest(StatType.Luck) * .01f + PartyBest(StatType.GoldFind));
            if (monster.Data.Guardian) gold += 50 * FloorGold;
            return Mathf.RoundToInt(gold);
        }

        // The one who lands the killing blow gets half again the experience.
        public const float LastHitBonus = 1.5f;
        public const string MilestoneLabel = "milestone", FloorBonusLabel = "floor";

        void Reward(SoulMonster monster)
        {
            Gold += KillGold(monster, random.NextDouble());
            DropItem(monster);
            DefeatedCounts[monster.Data.Id] = DefeatedCounts.TryGetValue(monster.Data.Id, out int count) ? count + 1 : 1;
            if (monster.Data.Elite) ElitesDefeated++;
            if (monster.Data.Guardian) BossesDefeated++;
            // Experience: the first of a species each mercenary helps defeat pays ten times; repeats pay once.
            int experience = Mathf.RoundToInt(monster.Data.Experience * FloorExperience(Floor) * (ExperienceBoost > 0 ? AltarBoost : 1));
            killers.TryGetValue(monster, out var killer);
            bool anyFirst = false;
            foreach (var hero in Mercenaries)
            {
                if (!hero.Alive) continue;
                bool first = hero.RecordDefeat(monster.Data, hero == killer, Floor);
                anyFirst |= first;
                int levels = hero.AddExperience(Mathf.RoundToInt(experience * (first ? FirstKillMultiplier : 1) * (hero == killer ? LastHitBonus : 1f)
                    * (1 + Mathf.Max(0, hero.Stats.Total(StatType.ExpGainBonus)))), rules, random);
                for (int i = levels; i > 0; i--)
                {
                    int reached = hero.Level - i + 1;
                    Emit(SoulEventKind.LevelUp, hero, null, null, reached % SoulMercenary.MilestoneLevels == 0 ? MilestoneLabel : null);
                    Log(hero.Name + " 레벨 " + reached);
                    if (reached % SoulMercenary.MilestoneLevels == 0) Log(hero.Name + ": 새 패턴을 배울 수 있습니다 · 영혼 슬롯 +1");
                }
            }
            Log(monster.Data.Name + " 처치" + (anyFirst ? " — 첫 처치 경험치 ×" + FirstKillMultiplier : ""));
            Emit(SoulEventKind.Death, null, monster);
            if (monster.Data.Guardian) DropChest(monster);
            if (monster.Data.DroppedSoul == null) return;
            bool guaranteed = dungeon.GuaranteeFirstSoul && soulsDropped == 0;
            // Luck (the pathfinder's stat): the party's best Luck adds 1%p soul drop chance per point.
            float luck = 0;
            foreach (var hero in Mercenaries) if (hero.Alive) luck = Mathf.Max(luck, hero.Stats.Total(StatType.Luck));
            if (guaranteed || random.NextDouble() < (monster.Data.SoulDropChance + luck * LuckDropBonus) * (1 + PartyBest(StatType.SoulFind)))
            {
                soulsDropped++;
                SoulsByMonster[monster.Data.Id] = SoulsByMonster.TryGetValue(monster.Data.Id, out int released) ? released + 1 : 1;
                var soul = monster.Data.DroppedSoul;
                Stash.Add(new SoulDrop(soul));
                Log(soul.OriginMonster + "의 영혼이 풀려났습니다!");
                // Not a counter ticking up: the view stops on this, shakes the camera and names what came out.
                CombatEvents.Add(new SoulCombatEvent
                {
                    Kind = SoulEventKind.SoulDrop, Target = monster, Amount = soul.Grade,
                    Label = soul.OriginMonster + "의 영혼"
                });
            }
        }

        public bool Absorb(SoulMercenary hero, SoulDrop drop)
        {
            if (!Stash.Contains(drop) || !Mercenaries.Contains(hero) || !hero.Absorb(drop.Soul, rules)) return false;
            Stash.Remove(drop); Log(hero.Name + " 영혼 흡수: " + drop.Soul.OriginMonster);
            Tally.Absorbed.Add($"{hero.Name}: {drop.Soul.OriginMonster}");
            Emit(SoulEventKind.Absorb, hero, null, null, drop.Soul.OriginMonster);
            return true;
        }

        public bool Preserve(SoulDrop drop)
        {
            if (PreservationItems <= 0 || !Stash.Contains(drop) || drop.Preserved) return false;
            PreservationItems--; if (FreeStones > 0) FreeStones--; drop.Preserved = true; return true;
        }

        public SoulLevelChoice OpenLevel(SoulMercenary hero)
        {
            if (hero == null || !hero.CanPickPattern) return null;
            if (pendingLevels.TryGetValue(hero, out var pending)) return pending;
            var available = new List<SoulPatternData>();
            var owned = hero.AllPatterns();
            foreach (var pattern in dungeon.LevelPatternPool)
                if (pattern != null && pattern.Compatible(hero) && !owned.Contains(pattern) && !available.Contains(pattern)) available.Add(pattern);
            for (int i = available.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = available[i]; available[i] = available[j]; available[j] = temp;
            }
            var choice = new SoulLevelChoice(hero, available.GetRange(0, Mathf.Min(3, available.Count)).ToArray(), null);
            pendingLevels[hero] = choice;
            return choice;
        }

        public int StatPointsPerLevel => SoulStatRules.PointsPerLevel(rules);

        public bool ConfirmLevel(SoulLevelChoice choice, SoulPatternData pattern)
        {
            if (choice == null || !Mercenaries.Contains(choice.Mercenary) || Array.IndexOf(choice.Patterns, pattern) < 0) return false;
            if (!choice.Mercenary.LearnPattern(pattern, rules)) return false;
            pendingLevels.Remove(choice.Mercenary);
            Log(choice.Mercenary.Name + " 패턴 습득: " + pattern.Id);
            return true;
        }

        // Camp: everyone around the fire (a kit lights one where the leader stands; the views draw it).
        public Vector2 CampSpot { get; private set; }
        public bool CampFire { get; private set; }

        void GatherAround(Vector2 spot, bool fire)
        {
            CampSpot = spot;
            CampFire = fire;
            int alive = Mercenaries.FindAll(h => h.Alive).Count, i = 0;
            foreach (var hero in Mercenaries)
            {
                if (!hero.Alive) continue;
                float angle = i++ * Mathf.PI * 2 / Mathf.Max(1, alive);
                var seat = spot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.3f;
                destinations[hero] = Map.ClosestReachable(seat, hero.Position, hero.Stats.Radius, out var reachable) ? reachable : hero.Position;
                routes[hero].Valid = false;
                ordered.Remove(hero);
            }
        }

        // Resting (a camp, sleeping it off, or stuck): those standing still sit or lie down (views: SoulMercenary.RestPose).
        void SettlePoses()
        {
            bool resting = Camping > 0 || Plan == SoulPartyPlan.Stranded || Sleeping;
            for (int i = 0; i < Mercenaries.Count; i++)
            {
                var hero = Mercenaries[i];
                hero.RestPose = resting && hero.Alive && Arrived(hero) && hero.CurrentTarget == null ? (i % 2 == 0 ? 1 : 2) : 0;
            }
        }

        static readonly StatType[] FloorBonusStats = { StatType.Strength, StatType.Vitality, StatType.Agility, StatType.Magic, StatType.Will, StatType.Luck };

        void Finish()
        {
            Finished = true;
            // Grade: 9 at the start, one up per floor cleared (only the mercenaries still standing cleared it).
            foreach (var hero in Mercenaries)
            {
                if (!hero.Alive || Floor <= hero.HighestFloorCleared) continue;
                hero.HighestFloorCleared = Floor;
                // a new deepest floor: every main stat +1 (kept with the trained stats)
                foreach (var stat in FloorBonusStats) hero.TrainedStats[stat] = (hero.TrainedStats.TryGetValue(stat, out float value) ? value : 0) + 1;
                hero.Rebuild(rules);
                Log($"{hero.Name}: {Floor}층 첫 돌파 — 모든 능력치 +1");
                Emit(SoulEventKind.LevelUp, hero, null, null, FloorBonusLabel);
            }
            ApplyMentalGrowth();
            foreach (var drop in Stash) if (drop.Preserved) Vault.Add(drop.Soul);
            Stash.Clear();
            Log(Floor >= FinalFloor ? "최종 8층 돌파!" : Floor + "층 돌파");
        }

        public void Log(string message)
        {
            Events.Insert(0, message);
            if (Events.Count > 20) Events.RemoveAt(20);
        }

        void Raise(SoulCombatant owner, SoulTrigger trigger, SoulCombatant target)
        {
            SoulCombat.FirePassives(owner, trigger, target);
            QueueActive(owner, trigger, target);
        }

        void QueueActive(SoulCombatant owner, SoulTrigger trigger, SoulCombatant target)
            => triggers.Enqueue(new TriggerEvent { Owner = owner, Trigger = trigger, Target = target });
    }
}
