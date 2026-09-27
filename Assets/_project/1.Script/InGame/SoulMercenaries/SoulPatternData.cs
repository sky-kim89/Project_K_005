using System;
using UnityEngine;

namespace SoulMercenaries
{
    [CreateAssetMenu(menuName = "Soul Mercenaries/Pattern")]
    public sealed class SoulPatternData : ScriptableObject
    {
        public string Id;
        [Tooltip("No longer handed out: it became this passive (명상). A save that has the pattern gets the passive instead.")]
        public SoulPassiveSkillData NowPassive;
        public SoulPatternCategory Category;
        public string WeaponTag, RequiredRaceId;
        public string[] Tags = Array.Empty<string>();
        public int Priority;
        [Min(0.05f)] public float ActionTime = 1f;
        [Min(0)] public float StaminaCost = 1f;
        [Min(0)] public float ManaCost;
        [Min(0)] public float Range = 1.2f;
        public SoulDamageSchool DamageSchool;
        public SoulDamageKind DamageKind;
        public SoulValue Damage = new SoulValue();
        public SoulValue Pierce = new SoulValue();
        public SoulDefenseMode DefenseMode;
        [Range(0, 1)] public float GuardMultiplier = .6f;
        public SoulValue DodgeChance = new SoulValue();
        [Tooltip("Roll defense: how far the body rolls away from the attacker.")]
        [Min(0)] public float RollDistance = 1.4f;
        [Min(0)] public float Knockback;
        [Tooltip("Movement patterns: how the unit positions itself against its current target.")]
        public SoulMoveStyle MoveStyle;
        [Tooltip("Attack patterns: instead of hitting, cast the best affordable active skill with Trigger = Cast that requires this pattern.")]
        public bool CastsSkills;
        [Tooltip("Attack patterns: strikes per action; with more than one each strike deals 60%.")]
        [Min(1)] public int Hits = 1;
        [Tooltip("Attack patterns: spin attacks — a circle of this radius around the attacker instead of the shape below.")]
        [Min(0)] public float AreaRadius;
        public SoulStatusApply Status = new SoulStatusApply();
        public Sprite Icon;
        [Tooltip("Seconds before this pattern can be used again (support patterns, ambush). 0 = whenever affordable.")]
        [Min(0)] public float Cooldown;

        // ── support: buffs, debuffs, heals ───────────────────────
        [Header("Support")]
        public SoulSupportTarget SupportTarget;
        public SoulUseWhen UseWhen;
        [Tooltip("Allies / enemies within this many cells of the user.")]
        [Min(0)] public float SupportRadius = 4f;
        public SoulBuffEffect[] Buffs = Array.Empty<SoulBuffEffect>();
        public SoulValue BuffDuration = new SoulValue();
        [Tooltip("Heal this share of the target's max HP.")]
        [Range(0, 1)] public float HealRatio;
        [Tooltip("Heal this much HP (from the user's stats — 기도: 4 + 마력×20%), on top of HealRatio.")]
        public SoulValue Heal = new SoulValue();
        [Tooltip("주문 집중: one more focus stack (SoulDungeonSession.MaxFocus at most) while no attack spell can go; the next attack spell spends them all.")]
        public bool SpellFocus;
        [Tooltip("A buff that stacks (자연 교감): each use adds its Buffs once more, up to this many, and renews the time. 0/1: no stacking.")]
        public int MaxStacks;
        public bool Heals => HealRatio > 0 || Heal != null && (Heal.Flat > 0 || Heal.Terms != null && Heal.Terms.Length > 0);
        [Tooltip("Restore this share of the target's max MP.")]
        [Range(0, 1)] public float ManaRatio;
        [Tooltip("Restore this share of the target's max stamina.")]
        [Range(0, 1)] public float StaminaRatio;
        [Tooltip("Heal / mana patterns: used only when the resource is below this share.")]
        [Range(0, 1)] public float UseBelow = .95f;
        [Tooltip("Self: coats the user's attacks with this status for ImbueDuration seconds (독 바르기).")]
        public SoulStatusApply Imbue = new SoulStatusApply();
        public SoulValue ImbueDuration = new SoulValue();

        // ── ambush ───────────────────────────────────────────────
        [Header("Ambush")]
        [Tooltip("Attack: blink behind the target first (up to Range away), then strike from behind.")]
        public bool Ambush;
        [Tooltip("Damage multiplier for the strike from behind.")]
        [Min(1)] public float BackstabMultiplier = 1.5f;

        // ── follow-up (DefenseMode.Followup) ─────────────────────
        [Header("Follow-up")]
        [Tooltip("After an evasion: seconds in which the next attack comes at once and costs no stamina.")]
        [Min(0)] public float FollowupWindow = 1.5f;
        [Min(1)] public float FollowupMultiplier = 1.3f;

        // ── chain (Category = Chain) ─────────────────────────────
        [Header("Chain")]
        public SoulChainTrigger ChainTrigger;
        [Tooltip("The follow-up attack comes at once (the running action is cut short).")]
        public bool FollowupInstant = true;
        [Tooltip("Share of the target's armor the follow-up ignores.")]
        [Range(0, 1)] public float FollowupArmorIgnore;
        [Tooltip("Combo: landed hits on one target that open the follow-up.")]
        [Min(1)] public int ComboHits = 3;
        [Tooltip("Roll: radius of the strike at the end of the roll (Damage is the strike's damage).")]
        [Min(0)] public float StrikeRadius = 1f;
        [Tooltip("Movement (kite): the step back right after an attack costs no stamina.")]
        public bool FreeRetreat;

        // ── area and wind-up ─────────────────────────────────────
        // Every attack is telegraphed: the area shows for a moment, then everything inside is hit. Single-target
        // patterns are not an exception — they get a shape just wide enough for one body (찌르기: a narrow lane).
        [Header("Area")]
        [Tooltip("Leave Auto to derive the shape from Range, AreaRadius and the damage kind.")]
        public SoulAreaSizing Sizing = SoulAreaSizing.Auto;
        public SoulAreaShape Shape = SoulAreaShape.Cone;
        public SoulAreaAnchor Anchor = SoulAreaAnchor.Forward;
        [Tooltip("Circle: radius. Cone / box: length. 0 = the pattern's Range.")]
        [Min(0)] public float AreaSize;
        [Tooltip("Cone: angle in degrees. Box: width in cells.")]
        [Min(0)] public float AreaWidth;
        [Tooltip("Share of the action time spent winding up before the hit lands. 0 = derive it from the area.")]
        [Range(0, .9f)] public float WindUpShare;

        // One body is about .8 cells across, so that is the width of a single-target lane.
        public const float SingleWidth = .85f;

        public void Area(out SoulAreaShape shape, out SoulAreaAnchor anchor, out float size, out float width)
        {
            if (Sizing == SoulAreaSizing.Explicit)
            {
                shape = Shape; anchor = Anchor;
                size = AreaSize > 0 ? AreaSize : Mathf.Max(.4f, Range);
                width = AreaWidth > 0 ? AreaWidth : shape == SoulAreaShape.Cone ? 60 : SingleWidth;
                return;
            }
            if (AreaRadius > 0) { shape = SoulAreaShape.Circle; anchor = SoulAreaAnchor.Self; size = AreaRadius; width = AreaRadius; return; }
            anchor = SoulAreaAnchor.Forward;
            size = Mathf.Max(.4f, Range);
            // Ranged shots and thrusts travel down a lane one body wide; swung weapons sweep a short arc.
            if (Range >= 2.5f || DamageKind == SoulDamageKind.Pierce)
            {
                shape = SoulAreaShape.Box;
                width = SingleWidth;
                return;
            }
            shape = SoulAreaShape.Cone;
            width = DamageKind == SoulDamageKind.Slash ? 90 : 70;
        }

        // The wider the attack, the longer it has to be wound up. A thrust down a one-body lane comes out
        // almost at once — too fast to walk out of, so only a roll or a sidestep beats it. A sweep or a spin
        // covers ground, so it announces itself long enough to be read and left.
        public float WindUp(float actionTime)
        {
            float share = WindUpShare;
            if (share <= 0)
            {
                Area(out var shape, out _, out _, out float width);
                share = shape == SoulAreaShape.Box ? (width <= SingleWidth * 1.3f ? .2f : .38f)
                    : shape == SoulAreaShape.Cone ? Mathf.Lerp(.3f, .55f, Mathf.InverseLerp(45f, 180f, width))
                    : .55f;
            }
            return Mathf.Max(.1f, actionTime * Mathf.Clamp(share, .05f, .9f));
        }

        public bool Compatible(SoulMercenary owner) => RaceAllows(owner) && WeaponAllows(owner);

        public bool RaceAllows(SoulMercenary owner)
        {
            if (!string.IsNullOrEmpty(RequiredRaceId) && owner.Race.Id != RequiredRaceId) return false;
            foreach (string tag in Tags)
                foreach (string forbidden in owner.Race.ForbiddenPatternTags)
                    if (tag == forbidden) return false;
            return true;
        }

        public bool WeaponAllows(SoulMercenary owner)
        {
            if (string.IsNullOrEmpty(WeaponTag)) return true;
            foreach (var item in owner.Equipment) if (item.WeaponTag == WeaponTag) return true;
            return false;
        }
    }
}
