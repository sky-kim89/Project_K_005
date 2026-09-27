using System;
using UnityEngine;

namespace SoulMercenaries
{
    public enum SoulBuildingKind { Guild, Shop, Church, Training, Blacksmith, Storage, Library, Memorial, SoulAltar, Merchant, Board }

    // A building on the village grid: fixed place and size, levels 0 (empty plot) to 3.
    [Serializable]
    public struct SoulBuildingDef
    {
        public SoulBuildingKind Kind;
        public string Name;
        [TextArea] public string Description;
        [Tooltip("Bottom-left cell on the village grid (row 0 is the top row of GroundRows).")]
        public Vector2Int Cell;
        public Vector2Int Size;
        [Range(0, 3)] public int StartLevel;
        [Tooltip("Gold to reach level 1, 2, 3 (index 0 builds it).")]
        public int[] UpgradeCosts;
        [Tooltip("Level 0 (plot) to 3.")]
        public Sprite[] Sprites;
        [Tooltip("What each level adds, shown before building / upgrading (index = level 1..3 → 0..2).")]
        public string[] LevelNotes;
        [Tooltip("Full-screen backdrop of its popup: the inside of the building.")]
        public Sprite Interior;
    }

    // Everything the village needs: the party it starts with, what can be hired, sold and trained, and the map.
    [CreateAssetMenu(menuName = "Soul Mercenaries/Village")]
    public sealed class SoulVillageData : ScriptableObject
    {
        public SoulStatRules StatRules;
        public SoulDungeonData Dungeon;
        public string DungeonScene = "SoulMercenaries";
        public string VillageScene = "SoulVillage";

        [Header("Start")]
        public SoulMercenaryData[] StartingRoster = Array.Empty<SoulMercenaryData>();
        [Min(0)] public int StartingGold = 300, StartingPotions = 2, StartingStones = 1;

        [Header("Guild")]
        [Tooltip("The jobs (base data: stats, kit, look, tendencies and 비성향 of each).")]
        public SoulMercenaryData[] HireTemplates = Array.Empty<SoulMercenaryData>();
        [Tooltip("Every mercenary there is (용병 명단, one asset each under Recruits/): the founders the company starts with, " +
                 "and the people the guild can offer — each always the same person. One who is in the company or has fallen is not offered again.")]
        public SoulRecruitData[] Recruits = Array.Empty<SoulRecruitData>();
        [Tooltip("Priests come from the church: offered at the guild once the church stands.")]
        public SoulMercenaryData[] PriestTemplates = Array.Empty<SoulMercenaryData>();
        [Min(0)] public int HirePrice = 150;

        [Header("Shop")]
        public SoulEquipmentData[] ShopEquipment = Array.Empty<SoulEquipmentData>();
        [Min(0)] public int PotionPrice = 40, StonePrice = 60;

        [Header("Training")]
        public SoulPatternData[] TrainingPatterns = Array.Empty<SoulPatternData>();

        [Header("Content index (save / load looks things up here by id)")]
        public SoulEquipmentData[] AllEquipment = Array.Empty<SoulEquipmentData>();
        public SoulActiveSkillData[] AllSkills = Array.Empty<SoulActiveSkillData>();
        public SoulPatternData[] AllPatterns = Array.Empty<SoulPatternData>();
        public SoulPassiveSkillData[] AllPassives = Array.Empty<SoulPassiveSkillData>();
        public SoulData[] AllSouls = Array.Empty<SoulData>();
        public SoulRaceData[] AllRaces = Array.Empty<SoulRaceData>();
        public SoulMercenaryData[] AllMercenaries = Array.Empty<SoulMercenaryData>();
        public SoulMonsterData[] AllMonsters = Array.Empty<SoulMonsterData>();

        [Header("Map")]
        public SoulBuildingDef[] Buildings = Array.Empty<SoulBuildingDef>();
        [Tooltip("One line per row, one char per cell. The key of each char is in TileKeys.")]
        [TextArea(6, 20)] public string GroundRows = "";
        public string TileKeys = "";
        public Sprite[] TileSprites = Array.Empty<Sprite>();
        public Sprite DoorLeft, DoorRight;

        public Sprite Tile(char key)
        {
            int index = TileKeys.IndexOf(key);
            return index >= 0 && index < TileSprites.Length ? TileSprites[index] : null;
        }
    }
}
