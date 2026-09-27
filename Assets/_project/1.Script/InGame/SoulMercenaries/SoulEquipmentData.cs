using System;
using UnityEngine;

namespace SoulMercenaries
{
    // An equipment template. What a mercenary wears is a SoulItem made from it (grade, enchants, special options).
    [CreateAssetMenu(menuName = "Soul Mercenaries/Equipment")]
    public sealed class SoulEquipmentData : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public string Name => string.IsNullOrEmpty(DisplayName) ? Id : DisplayName;
        [Tooltip("Kind shown in the UI (한손검, 판금 갑옷, 반지 …); accessories use it to lean their enchants.")]
        public string Kind;
        public SoulEquipSlot Slot;
        [Tooltip("Main hand only: takes the off hand too.")]
        public bool TwoHanded;
        public string WeaponTag;
        [Tooltip("Kind weight in kg; the material multiplies it.")]
        [Min(0)] public float BaseWeight = 1f;
        public SoulMaterial Material;
        [Tooltip("Named item: options as written (not scaled by grade), grade fixed.")]
        public bool Unique;
        [Range(1, 6)] public int FixedGrade = 3;
        [Tooltip("Price at grade 평범한 (uniques: as written); it sells back for half.")]
        [Min(0)] public int Price = 100;
        [Tooltip("Shop level needed to stock it (1–3). 0: never in the shop.")]
        [Range(0, 3)] public int ShopTier = 1;
        [Tooltip("Shallowest floor it can drop on (dungeon loot table).")]
        [Range(1, 8)] public int DropFloor = 1;
        public SoulAppearancePatch Look;
        [Tooltip("Inventory icon (cut from the part sheet's icon frame; drawn for items without part art).")]
        public Sprite Icon;
        [Tooltip("Used at reduced power when the wearer owns no attack pattern at all. Leave empty for staves: mages get no free physical attack.")]
        public SoulPatternData BasicAttack;
        [Tooltip("Base options at grade 평범한 (the grade scales them).")]
        public SoulStatBonus[] Bonuses = Array.Empty<SoulStatBonus>();
        public SoulPatternData[] Patterns = Array.Empty<SoulPatternData>();
        public SoulActiveSkillData[] ActiveSkills = Array.Empty<SoulActiveSkillData>();
        public SoulPassiveSkillData[] Passives = Array.Empty<SoulPassiveSkillData>();
    }
}
