using System;
using UnityEngine;

namespace SoulMercenaries
{
    [CreateAssetMenu(menuName = "Soul Mercenaries/Mercenary")]
    public sealed class SoulMercenaryData : ScriptableObject
    {
        public string Id, DisplayName;
        public SoulRaceData Race;
        public SoulStatBonus[] BaseStats = Array.Empty<SoulStatBonus>();
        public SoulEquipmentData[] StartingEquipment = Array.Empty<SoulEquipmentData>();
        public SoulAppearancePatch Look;
        [Tooltip("Job shown in the UI (검사, 수호자, 마법사, 길잡이 …).")]
        public string Job;
        public SoulRole Role;
        [Tooltip("Job passives on top of race passives (e.g. the pathfinder's map options).")]
        public SoulPassiveSkillData[] Passives = Array.Empty<SoulPassiveSkillData>();
        [Tooltip("Job tendency for automatic level-up stats, on top of the race weights.")]
        public SoulStatBonus[] GrowthWeights = Array.Empty<SoulStatBonus>();
        [Tooltip("비성향: stats this job never grows in by level (whatever the race leans to).")]
        public StatType[] WeakGrowth = Array.Empty<StatType>();
        [Tooltip("Personal starting kit on top of the race patterns (e.g. a mage's casting pattern).")]
        public SoulPatternData[] Patterns = Array.Empty<SoulPatternData>();
        public SoulActiveSkillData[] ActiveSkills = Array.Empty<SoulActiveSkillData>();
    }
}
