using System;
using UnityEngine;

namespace SoulMercenaries
{
    [CreateAssetMenu(menuName = "Soul Mercenaries/Soul")]
    public sealed class SoulData : ScriptableObject
    {
        public string Id;
        public string OriginMonster;
        public SoulMonsterData Source;
        public int Grade = 1;
        public SoulStatBonus[] CharacteristicStats = Array.Empty<SoulStatBonus>();
        public SoulPatternData[] Patterns = Array.Empty<SoulPatternData>();
        public SoulActiveSkillData[] ActiveSkills = Array.Empty<SoulActiveSkillData>();
        public SoulPassiveSkillData[] Passives = Array.Empty<SoulPassiveSkillData>();
        public SoulAppearancePatch Appearance;
        public float HeightMultiplier = 1f, WeightMultiplier = 1f;
        [Tooltip("The soul's core pattern. If the mercenary's race cannot use it, the whole soul cannot be absorbed (흡수 불가).")]
        public SoulPatternData CorePattern;
        [Tooltip("Three of this soul fuse into this one at the soul altar.")]
        public SoulData Refined;

        void OnValidate()
        {
            if (CharacteristicStats == null || CharacteristicStats.Length == 0)
                Debug.LogError($"Soul {name} must provide at least one characteristic upper stat.", this);
        }
    }
}
