using System;
using UnityEngine;

namespace SoulMercenaries
{
    [CreateAssetMenu(menuName = "Soul Mercenaries/Monster")]
    public sealed class SoulMonsterData : ScriptableObject
    {
        public string Id;
        public SoulStatBonus[] Stats = Array.Empty<SoulStatBonus>();
        public SoulPatternData[] Patterns = Array.Empty<SoulPatternData>();
        public SoulActiveSkillData[] ActiveSkills = Array.Empty<SoulActiveSkillData>();
        public SoulPassiveSkillData[] Passives = Array.Empty<SoulPassiveSkillData>();
        public float Height = 1.7f, Weight = 75f;
        public int Experience = 8, Gold = 5;
        [Range(0, 1)] public float SoulDropChance = 0.1f;
        public SoulData DroppedSoul;
        [Tooltip("Dungeon guardian: the exit opens once every guardian is down.")]
        public bool Guardian;
        [Tooltip("Elite: stands alone or leads a horde; shown larger.")]
        public bool Elite;
        [Tooltip("회피 본능: only such a monster evades at all — dodge and roll patterns, disengaging steps, walking out of a warned area, and its evasion rating.")]
        public bool Evasive;
        [Tooltip("언데드: 퇴마 (HolyBane) hits it harder.")]
        public bool Undead;
        [Tooltip("Telegraphed patterns (red area, then the hit), used in this order, one after another.")]
        public SoulTelegraphData[] Telegraphs = Array.Empty<SoulTelegraphData>();
        public string DisplayName;
        public string Name => string.IsNullOrEmpty(DisplayName) ? Id : DisplayName;

        [Header("Look")]
        [Tooltip("Use a complete monster sprite library (EnemyMonsterCatalog) instead of CharacterBuilder parts.")]
        public bool UseMonsterSprite = true;
        public EnemyRace MonsterRace = EnemyRace.Hog;
        public UnitAppearanceData Appearance = new UnitAppearanceData();
        public Color Tint = Color.white;
        [Tooltip("Baked idle frame (a CharacterBuilder monster), for its picture where none is on screen — a soul's card.")]
        public Sprite Portrait;
    }
}
