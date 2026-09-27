using System;
using UnityEngine;

namespace SoulMercenaries
{
    public enum SoulAreaShape { Circle, Cone, Box }

    //  Self    — around the caster (radial): circle / box centered on it, cone from it
    //  Forward — in front of the caster, the way it faces when the cast starts
    //  Target  — circle on the target's spot at cast start; cone / box aimed from the caster at it
    public enum SoulAreaAnchor { Self, Forward, Target }

    // Auto: the shape comes from the pattern's range and damage kind (see SoulPatternData.Area).
    public enum SoulAreaSizing { Auto, Explicit }

    // A telegraphed attack (boss / elite pattern): a red area shows for WindUp seconds, then everything
    // inside is hit. The area is fixed when the cast starts, so stepping out of it avoids the hit.
    [CreateAssetMenu(menuName = "Soul Mercenaries/Telegraph")]
    public sealed class SoulTelegraphData : ScriptableObject
    {
        public string Id;
        public string SkillName;
        public SoulAreaShape Shape;
        public SoulAreaAnchor Anchor;
        [Tooltip("Circle: radius. Cone / box: length.")]
        [Min(.1f)] public float Size = 3;
        [Tooltip("Cone: angle in degrees. Box: width.")]
        [Min(.1f)] public float Width = 2;
        [Tooltip("Seconds the red area shows before the hit.")]
        [Min(.1f)] public float WindUp = 1.2f;
        [Tooltip("After the hit: seconds before the next pattern may start (basic attacks in between).")]
        [Min(0)] public float Rest = 2;
        [Tooltip("The pattern starts once the target is this close.")]
        [Min(0)] public float TriggerRange = 4;
        public SoulDamageSchool DamageSchool = SoulDamageSchool.Physical;
        public SoulDamageKind DamageKind = SoulDamageKind.Impact;
        public SoulValue Damage = new SoulValue();
        [Min(0)] public float Knockback;
        public SoulStatusApply Status = new SoulStatusApply();
    }
}
