using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    [Serializable]
    public struct SoulMonsterSpawn
    {
        public SoulMonsterData Monster;
        public Vector2Int Cell;
        [Tooltip("Monsters with the same group (not 0) fight together: one engaged, all engage.")]
        public int Group;
        [Tooltip("Facing while waiting (a formation looks toward the way in). Zero: right.")]
        public Vector2 Facing;
    }

    // Monsters that travel together. Front: melee line, Back: ranged line behind it (goblin warriors + archers).
    [Serializable]
    public struct SoulPackTemplate
    {
        public string Name;
        public SoulMonsterData[] Front;
        public SoulMonsterData[] Back;
        [Min(1)] public int Min, Max;
        [Range(0, 1)] public float BackShare;
        [Tooltip("Where it appears: 0 = the start (map center), 1 = the farthest room.")]
        [Range(0, 1)] public float MinDepth, MaxDepth;
    }

    // One floor's own monsters: who wanders alone, who leads, which packs, the guardian of its farthest hall.
    [Serializable]
    public sealed class SoulFloorRoster
    {
        public string Name;
        public SoulMonsterData[] Singles = Array.Empty<SoulMonsterData>();
        public SoulMonsterData[] Elites = Array.Empty<SoulMonsterData>();
        public SoulPackTemplate[] Packs = Array.Empty<SoulPackTemplate>();
        public SoulMonsterData Boss;

        public bool IsEmpty => Singles.Length == 0 && Packs.Length == 0 && Boss == null;

        // Every monster in it (the codex, the quest board).
        public List<SoulMonsterData> All()
        {
            var list = new List<SoulMonsterData>();
            void Add(SoulMonsterData monster) { if (monster != null && !list.Contains(monster)) list.Add(monster); }
            foreach (var monster in Singles) Add(monster);
            foreach (var pack in Packs) { foreach (var monster in pack.Front ?? Array.Empty<SoulMonsterData>()) Add(monster); foreach (var monster in pack.Back ?? Array.Empty<SoulMonsterData>()) Add(monster); }
            foreach (var monster in Elites) Add(monster);
            Add(Boss);
            return list;
        }
    }

    [CreateAssetMenu(menuName = "Soul Mercenaries/Dungeon")]
    public sealed class SoulDungeonData : ScriptableObject
    {
        [Header("Fixed layout (ignored when Procedural)")]
        [TextArea(8, 40)] public string MapRows;
        public Vector2Int PartyStart, Exit;
        [Tooltip("Portals to the next stage. Empty: the single Exit cell.")]
        public Vector2Int[] Exits = Array.Empty<Vector2Int>();
        public SoulMonsterSpawn[] Monsters = Array.Empty<SoulMonsterSpawn>();

        [Header("Procedural: a new map every play")]
        public bool Procedural;
        [Min(40)] public int Width = 370, Height = 238;
        [Tooltip("Large halls for hordes; one more hall becomes the boss arena.")]
        [Min(0)] public int Halls = 6;
        [Range(0, 1)] public float EmptyRooms = .25f;
        [Tooltip("Treasure chests in small alcoves behind secret doors (one item from the loot list each).")]
        [Min(0)] public int HiddenChests = 6;
        [Tooltip("Hidden stages behind secret walls: a vault, a trap room with monsters, or a boss room.")]
        [Min(0)] public int HiddenStages = 2;
        [Header("Objects the player can use (click)")]
        [Min(0)] public int Springs = 4;
        [Min(0)] public int Campfires = 4;
        [Min(0)] public int Blessings = 3;
        [Min(0)] public int Altars = 3;
        [Min(0)] public int Warps = 5;
        [Min(0)] public int Watchtowers = 3;
        [Min(0)] public int SuspiciousChests = 4;
        [Min(0)] public int FortuneIdols = 3;
        [Tooltip("Lone weak monsters near the start.")]
        public SoulMonsterData[] Singles = Array.Empty<SoulMonsterData>();
        [Tooltip("Lone elites in the middle and far rooms; they also lead hordes.")]
        public SoulMonsterData[] Elites = Array.Empty<SoulMonsterData>();
        public SoulPackTemplate[] Packs = Array.Empty<SoulPackTemplate>();
        [Tooltip("Alone in its arena, the hall farthest from the start and the portals.")]
        public SoulMonsterData Boss;
        [Tooltip("Each floor's own monsters (element 0 = floor 1). A floor past the list, or an empty one, uses the lists above.")]
        public SoulFloorRoster[] Floors = Array.Empty<SoulFloorRoster>();

        public SoulFloorRoster Roster(int floor)
        {
            var own = Floors != null && floor >= 1 && floor <= Floors.Length ? Floors[floor - 1] : null;
            return own != null && !own.IsEmpty ? own : new SoulFloorRoster { Name = name, Singles = Singles, Elites = Elites, Packs = Packs, Boss = Boss };
        }

        [Header("Rules")]
        public SoulPatternData[] LevelPatternPool = Array.Empty<SoulPatternData>();
        [Tooltip("Prototype onboarding: the first monster with a soul always drops it.")]
        public bool GuaranteeFirstSoul = true;
        [Tooltip("On: the portals open only once every guardian (boss) is down. Off: the boss is optional (loot only).")]
        public bool BossGatesExit = true;
        [Tooltip("What drops from monsters and chests (the grade is rolled by floor).")]
        public SoulEquipmentData[] LootTable = Array.Empty<SoulEquipmentData>();
        [Tooltip("Named items a defeated guardian's chest holds (BossLootCount - 1 of them, plus one rolled item).")]
        public SoulEquipmentData[] BossLoot = Array.Empty<SoulEquipmentData>();
        public int BossLootCount = 2;
        [Tooltip("Skill books that can drop (elites, bosses, hidden chests); SoulActiveSkillData.BookTier gates the floor.")]
        public SoulActiveSkillData[] SkillBooks = Array.Empty<SoulActiveSkillData>();
    }
}
