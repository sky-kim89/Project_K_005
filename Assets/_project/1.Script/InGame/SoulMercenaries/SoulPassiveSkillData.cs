using System;
using UnityEngine;

namespace SoulMercenaries
{
    [CreateAssetMenu(menuName = "Soul Mercenaries/Passive Skill")]
    public sealed class SoulPassiveSkillData : ScriptableObject
    {
        public string SkillName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;
        public string SoulId;
        public SoulTrigger SoulEvent = SoulTrigger.Always;
        [Min(1)] public int Every = 1;
        public SoulStatBonus[] AlwaysBonuses = Array.Empty<SoulStatBonus>();
        public SoulValue Damage = new SoulValue();
        public SoulValue Heal = new SoulValue();
        [Tooltip("Stamina restored when the passive fires (e.g. on a kill).")]
        [Min(0)] public float Stamina;
        public SoulMapTrait MapTraits;
    }
}
