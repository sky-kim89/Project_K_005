using System;
using UnityEngine;

namespace SoulMercenaries
{
    // Something a skill leaves on the ground (SoulActiveSkillData.Field). New kinds go at the end.
    // Spirit: a summoned spirit at its caller's side (소환사) — every SpiritInterval seconds it strikes the nearest foe in
    // reach (Radius), or, a healing spirit (Heal, no Damage), mends the most hurt ally in reach.
    public enum SoulFieldKind { None, Trap, Sanctuary, Aura, Hazard, Spirit }

    [CreateAssetMenu(menuName = "Soul Mercenaries/Active Skill")]
    public sealed class SoulActiveSkillData : ScriptableObject
    {
        public string SkillName;
        [TextArea(2, 4)] public string Description;
        [Min(0f)] public float Cooldown = 15f;
        public string SoulId;
        public SoulTrigger Trigger = SoulTrigger.InRange;
        public SoulPatternData RequiredPattern;
        public SoulResourceCost[] Costs = Array.Empty<SoulResourceCost>();
        public SoulDamageSchool DamageSchool;
        public SoulDamageKind DamageKind;
        public SoulValue Damage = new SoulValue();
        public SoulValue Heal = new SoulValue();
        [Tooltip("The heal also closes one wound (AllyHurt heals the ally, otherwise the caster).")]
        public bool HealsWounds;
        [Tooltip("AllyHurt: a shield on one ally (신성한 방패) that takes this much damage before its HP does, for Duration.")]
        public SoulValue Barrier = new SoulValue();
        [Tooltip("Spirit: the spirit's picture (Resources/SoulSpirits: fire, frost, wind, light) and how often it acts.")]
        public string SpiritLook;
        public float SpiritInterval = 1.2f;
        [Tooltip("HealsWounds as a roll: this chance (0–1, from the caster's stats — 치유의 빛: 마력) closes the wounds. Empty: always.")]
        public SoulValue WoundChance = new SoulValue();
        public bool WoundRoll => WoundChance != null && (WoundChance.Flat > 0 || WoundChance.Terms != null && WoundChance.Terms.Length > 0);
        [Tooltip("Only a mercenary with this job can use it (e.g. 성직자 for healing). Empty = anyone.")]
        public string RequiredJob;
        [Tooltip("AllySupport: SelfBuffs and Imbue go to every ally within Radius (the caster included) for Duration.")]
        public SoulStatusApply Imbue = new SoulStatusApply();
        [Tooltip("AllyFallen: share of max HP the revived ally comes back with.")]
        [Range(0, 1)] public float ReviveRatio;
        [Header("Library")]
        [Tooltip("Skill book tier: 1 anywhere, 2 from floor 3, 3 (the highest skills) only from bosses on floor 7 and deeper.")]
        [Range(1, 3)] public int BookTier = 1;
        [Tooltip("A library skill at level 3 plus one more book of it becomes this.")]
        public SoulActiveSkillData UpgradeTo;
        public SoulValue Chance = new SoulValue();
        public SoulValue Radius = new SoulValue();
        public SoulValue Duration = new SoulValue();
        [Min(0)] public float Knockback;
        [Tooltip("0 = the required pattern's range (or 2.5 without one).")]
        [Min(0)] public float Range;
        [Tooltip("Seconds the caster is busy (before action speed). 0 = the required pattern's action time, or 1.")]
        [Min(0)] public float CastTime;
        public SoulStatusApply Status = new SoulStatusApply();
        public Sprite Icon;
        [Tooltip("Buffs on the caster for Duration seconds (e.g. taunt: threat and armor).")]
        public SoulBuffEffect[] SelfBuffs = Array.Empty<SoulBuffEffect>();
        [Header("Technique")]
        [Tooltip("기술: paid with MP but not magic — no spell power, no weapon focus (일섬, 천검 …).")]
        public bool Technique;
        [Tooltip("Only on a target at this share of its HP or less (처형). 0: any.")]
        [Range(0, 1)] public float ExecuteBelow;
        [Tooltip("Spends every MP left: the damage grows by the MP spent over the listed cost (비전 폭발).")]
        public bool DrainMana;
        [Header("Special")]
        [Tooltip("Hits everyone on the line from the caster through the target, up to Range (관통 사격).")]
        public bool Line;
        [Tooltip("After the target the hit jumps to this many more foes nearby, a quarter weaker each time (연쇄 번개).")]
        [Min(0)] public int Chain;
        [Tooltip("Left on the ground for Duration within Radius: a trap (springs on the first foe), a sanctuary (heals allies every second) or an aura that follows the caster (hits foes every second).")]
        public SoulFieldKind Field;
        [Tooltip("AllySupport: while the buff lasts, this share of each ally's damage taken goes to the caster instead (대신 맞기).")]
        [Range(0, 1)] public float GuardShare;
        [Tooltip("AllyHurt: lifts every status from the most afflicted ally instead of healing the most hurt (정화).")]
        public bool Cleanse;
        [Tooltip("Costs this share of the caster's current HP (희생); never on the caster.")]
        [Range(0, 1)] public float HpCost;
        [Tooltip("While retreating, once a floor: the party steps out next to the way out (지름길).")]
        public bool Shortcut;
        [Tooltip("A mercenary uses it at once, with no warning circle. A monster always winds up (예고) whatever this says.")]
        public bool Instant;
        [Header("Monster-like (souls)")]
        [Tooltip("The burst or the field is centred on the target, not on the caster (화살비, 용암 웅덩이).")]
        public bool AtTarget;
        [Tooltip("Width of a Line (0: 0.5). A wide one is a breath (용의 숨결).")]
        [Min(0)] public float Width;
        [Tooltip("Pulls each foe hit this far toward the centre (혀 휘감기, 소용돌이).")]
        [Min(0)] public float Pull;
        [Tooltip("The caster heals this share of the damage it deals (체액 흡수, 생명 흡수).")]
        [Range(0, 2)] public float Drain;
        [Tooltip("Takes this share of each foe's MP and stamina for the caster (영혼 착취).")]
        [Range(0, 1)] public float Sap;
        [Tooltip("Gold the party gets for each blow that lands (소매치기).")]
        [Min(0)] public int Steal;
        [Tooltip("Strips every buff from each foe hit (공허).")]
        public bool Dispel;
        [Tooltip("A leap lands behind the target instead of before it (그림자 습격).")]
        public bool Behind;
        [Tooltip("After the skill the caster steps this far away from the target (박쥐 변신).")]
        [Min(0)] public float Retreat;
        [Tooltip("A second status on the same blow (삼키기: stun and poison).")]
        public SoulStatusApply Extra = new SoulStatusApply();
        [Header("Area")]
        [Tooltip("A burst around the caster: every foe within Radius is hit (damage, status, knockback) — 칼날 폭풍, 포효 …")]
        public bool Burst;
        [Tooltip("AllyFallen: every fallen ally within Range comes back, and every living one there is healed by Heal.")]
        public bool Mass;
        [Tooltip("Once a floor, whatever the cooldown (기적).")]
        public bool OncePerFloor;
        [Header("Leap")]
        [Tooltip("Jump next to the target first. The damage (area = Radius) only happens when BodyWeight >= WeightThreshold.")]
        public bool LeapToTarget;
        [Min(0)] public float WeightThreshold;

        // A skill that does something: a blow or a heal (flat or from a stat), a status, an enchant, a revival, buffs
        // (fixed or scaled — 전장의 집중 is a flat +10%), or one of the special effects (a field, a pull, a theft,
        // a cleansing, the way out …). Only one that does none of it is a mistake in the data.
        static bool Has(SoulValue value) => value != null && (value.Flat != 0 || (value.Terms != null && value.Terms.Length > 0));
        public bool HasEffect()
            => Has(Damage) || Has(Heal) || Has(Barrier) || ReviveRatio > 0
               || (Status != null && Status.Kind != SoulStatus.None) || (Extra != null && Extra.Kind != SoulStatus.None) || (Imbue != null && Imbue.Kind != SoulStatus.None)
               || (SelfBuffs != null && SelfBuffs.Length > 0)
               || Field != SoulFieldKind.None || Shortcut || Cleanse || GuardShare > 0 || Pull > 0 || Dispel || Sap > 0 || Steal > 0 || Retreat > 0 || LeapToTarget;

        void OnValidate()
        {
            if (!HasEffect()) Debug.LogError($"Active skill {name} does nothing: give it a blow, a heal, a status, a buff or a special effect.", this);
        }
    }
}
