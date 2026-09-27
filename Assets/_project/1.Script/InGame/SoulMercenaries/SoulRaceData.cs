using System;
using UnityEngine;

namespace SoulMercenaries
{
    [CreateAssetMenu(menuName = "Soul Mercenaries/Race")]
    public sealed class SoulRaceData : ScriptableObject
    {
        public string Id;
        public SoulStatBonus[] Bonuses = Array.Empty<SoulStatBonus>();
        public SoulPatternData[] StartingPatterns = Array.Empty<SoulPatternData>();
        public SoulPassiveSkillData[] InnatePassives = Array.Empty<SoulPassiveSkillData>();
        public string[] ForbiddenPatternTags = Array.Empty<string>();
        [Tooltip("Level-up stat tendency: relative weight per upper stat (added to the mercenary's own weights).")]
        public SoulStatBonus[] GrowthWeights = Array.Empty<SoulStatBonus>();
        public float Height = 1.7f, Weight = 70f;
        public UnitAppearanceData Appearance = new UnitAppearanceData();
    }
}
