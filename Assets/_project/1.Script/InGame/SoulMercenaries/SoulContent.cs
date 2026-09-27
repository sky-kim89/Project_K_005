using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    public enum SoulDamageSchool { Physical, Magic, Other }
    public enum SoulDamageKind { Slash, Impact, Pierce, Wind, Stone, Fire, Lightning, Cold, Arcane, Bleed, Poison, Burn, Frostbite }
    // Support: buffs, debuffs and heals instead of a hit (SoulDungeonSession.TrySupport).
    // Chain: a follow-up that fires off another action (evasion, guard, a run of hits, a roll) — 연계.
    public enum SoulPatternCategory { Attack, Defense, Movement, Encounter, Support, Chain }
    public enum SoulChainTrigger { Evade, Guard, Combo, Roll }
    // Who a support pattern works on; Enemies = the current target, or everyone around when it has no range.
    public enum SoulSupportTarget { Self, Allies, Enemies }
    public enum SoulUseWhen { InCombat, OutOfCombat, Always }
    // Dodge: sidestep in place (회피). Roll: a dodge that also moves the body away (구르기).
    // Followup: not a defense of its own — after a successful evasion the next attack comes at once (회피 반격).
    public enum SoulDefenseMode { Guard, Dodge, Counter, Cover, Roll, Followup }
    // Cast: attack magic. Not a pattern — the caster picks the best affordable spell as its attack (MP, cast time,
    // a circle on the target). A skill with a RequiredPattern still needs that pattern (legacy CastsSkills data).
    // AllyHurt: a healing skill used on the most hurt ally in range that needs it (below half HP, missing most of
    // what the heal gives, or wounded for a heal that closes wounds).
    // AllySupport: buffs / an element enchant laid on every ally around the caster during a fight.
    // AllyFallen: brings a fallen ally back (소생).
    public enum SoulTrigger { Always, BattleStart, Encounter, InRange, LowHealth, OnHit, OnAttack, OnAttackLanded, OnKill, Cast, AllyHurt, AllySupport, AllyFallen }
    // Approach: shortest way in · KeepDistance: stay inside own range · Flank: circle to the side of an enemy busy
    // with someone else (side hits +25%) · Kite: step back right after each shot · Rush: close in fast ·
    // Escort: stand between a threatened backliner and its attacker.
    public enum SoulMoveStyle { Approach, KeepDistance, Flank, Kite, Rush, Escort }

    // Party role: how a mercenary takes its place in a fight (tank first, melee next, ranged/support behind).
    public enum SoulRole { Melee, Tank, Ranged, Support }

    // Map-related special options (pathfinder job). Carried by passives, summed over living mercenaries.
    [Flags]
    public enum SoulMapTrait
    {
        None = 0,
        SeeThroughWalls = 1,    // own vision ignores walls (exploration only — never attacks)
        RevealExit = 2,         // exit (portal) shown on the minimap from the start
        RevealGuardian = 4,     // guardian positions shown through the fog
        DetectHiddenDoors = 8,  // hidden doors nearby become passable
        WideVision = 16,        // larger vision radius
        RouteSense = 32,        // knows the map's rooms: explores room to room (no dead ends) and lets the player
                                // pick the party's aim — hunt, farm or break through
        RevealTreasure = 64,    // treasure chests (hidden ones too) shown on the minimap
    }

    //  Explore — uncover the map (without a pathfinder the only mode)
    //  Hunt    — seek monsters, species the party has not beaten yet first (x10 experience, new souls)
    //  Farm    — the boss, then every treasure chest (hidden ones behind secret doors too)
    //  Advance — straight to the nearest portal
    public enum SoulExploreMode { Explore, Hunt, Farm, Advance }
    public enum SoulResource { Stamina, Mana }
    // New kinds go at the end: the values are serialized in assets.
    public enum SoulStatus { None, Bleed, Poison, Burn, Chill, Freeze, Stun, Fear, Petrify, Slow, Confuse, Weaken, Silence }

    // A status a pattern or skill tries to apply on a landed hit. Values are evaluated with the attacker's stats.
    [Serializable]
    public class SoulStatusApply
    {
        public SoulStatus Kind;
        public SoulValue Chance = new SoulValue();          // 0~1 before the target's resist
        public SoulValue Duration = new SoulValue();        // seconds before the target's resist
        public SoulValue DamagePerSecond = new SoulValue(); // bleed/poison/burn only
        [Tooltip("Debuff grade 1–3 before the target's resist: higher grades hit harder (SoulCombat.StatusEffects).")]
        [Range(1, 3)] public int Grade = 1;
        [Tooltip("Added to Grade from the attacker's stats (e.g. will ×5% for fear); the total is rounded down.")]
        public SoulValue GradeBonus = new SoulValue();

        public int GradeFor(UnitStat stats) => Mathf.Clamp(Grade + Mathf.FloorToInt(GradeBonus != null ? GradeBonus.Evaluate(stats) : 0), 1, SoulCombat.MaxGrade);
    }

    [Serializable]
    public struct SoulStatBonus
    {
        public StatType Stat;
        public float Value;
    }

    [Serializable]
    public struct SoulStatTerm
    {
        public StatType Stat;
        public float Factor;
    }

    [Serializable]
    public class SoulValue
    {
        public float Flat;
        public SoulStatTerm[] Terms = Array.Empty<SoulStatTerm>();
        public float Minimum = -1000000000f;
        public float Maximum = 1000000000f;

        public float Evaluate(UnitStat stats)
        {
            float value = Flat;
            foreach (var term in Terms) value += stats.Get(term.Stat) * term.Factor;
            return Mathf.Clamp(value, Minimum, Maximum);
        }
    }

    // Timed buff an active skill puts on its caster; applied on the combat (lower) stat layer.
    [Serializable]
    public struct SoulBuffEffect
    {
        public StatType Stat;
        public SoulValue Amount;
    }

    [Serializable]
    public struct SoulResourceCost
    {
        public SoulResource Resource;
        [Min(0)] public float Amount;
    }

    // Partial CharacterBuilder look. Empty fields keep the lower layer (race → mercenary → equipment → soul).
    [Serializable]
    public struct SoulAppearancePatch
    {
        public string Body, Head, Ears, Eyes, Hair, Horns;
        public string Armor, Helmet, Mask, Cape, Weapon, Shield;

        public void Apply(UnitAppearanceData appearance)
        {
            if (!string.IsNullOrEmpty(Body)) appearance.Body = Body;
            if (!string.IsNullOrEmpty(Head)) appearance.Head = Head;
            if (!string.IsNullOrEmpty(Ears)) appearance.Ears = Ears;
            if (!string.IsNullOrEmpty(Eyes)) appearance.Eyes = Eyes;
            if (!string.IsNullOrEmpty(Hair)) appearance.Hair = Hair;
            if (!string.IsNullOrEmpty(Horns)) appearance.Horns = Horns;
            if (!string.IsNullOrEmpty(Armor)) appearance.Armor = Armor;
            if (!string.IsNullOrEmpty(Helmet)) appearance.Helmet = Helmet;
            if (!string.IsNullOrEmpty(Mask)) appearance.Mask = Mask;
            if (!string.IsNullOrEmpty(Cape)) appearance.Cape = Cape;
            if (!string.IsNullOrEmpty(Weapon)) appearance.Weapon = Weapon;
            if (!string.IsNullOrEmpty(Shield)) appearance.Shield = Shield;
        }
    }
}
