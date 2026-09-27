using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    public struct SoulHit
    {
        public SoulDamageSchool School;
        public SoulDamageKind Kind;
        public float Raw, Pierce, Knockback;
        public float ArmorIgnore; // share of the target's armor ignored (막고 찌르기)
        public bool Melee;
        public SoulStatus Status, Extra;
        public float StatusChance, StatusDuration, StatusDps, ExtraChance, ExtraDuration, ExtraDps;
        public int StatusGrade, ExtraGrade;

        // Status values are evaluated once from the attacker's stats when the hit is created. The first call
        // fills the pattern's own slot, the second the extra slot (a status coated on by 독 바르기).
        public void SetStatus(SoulStatusApply apply, UnitStat stats)
        {
            if (apply == null || apply.Kind == SoulStatus.None) return;
            SetStatus(apply.Kind, apply.Chance.Evaluate(stats), apply.Duration.Evaluate(stats), apply.DamagePerSecond.Evaluate(stats), apply.GradeFor(stats));
        }

        public void SetStatus(SoulStatus kind, float chance, float duration, float dps, int grade)
        {
            if (kind == SoulStatus.None) return;
            if (Status == SoulStatus.None) { Status = kind; StatusChance = chance; StatusDuration = duration; StatusDps = dps; StatusGrade = grade; }
            else { Extra = kind; ExtraChance = chance; ExtraDuration = duration; ExtraDps = dps; ExtraGrade = grade; }
        }

        // A spell's status (curse, burn): longer, harder and a grade higher with the caster's spell power.
        public void Empower(float power)
        {
            if (Status == SoulStatus.None || power <= 1) return;
            StatusDuration *= SoulSkillUsePolicy.DurationScale(power);
            StatusDps *= power;
            StatusGrade = Mathf.Min(3, StatusGrade + SoulSkillUsePolicy.GradeBonus(power));
        }
    }

    public struct SoulHitResult
    {
        public float Damage, Knockback, CounterDamage;
        public bool Dodged, Guarded, Killed, Countered, Evaded; // Evaded: a dodge pattern sidestepped in place
        public bool Critical; // 치명타: CritChance rolled, ×(CritMultiplier + CritDamage)
        public float RollDistance; // > 0: a roll pattern avoided the hit and the body rolls this far away
        public SoulStatus Applied;
    }

    public sealed class SoulStatusState
    {
        public SoulStatus Kind;
        public float Remaining, DamagePerSecond;
        public int Stacks = 1;
        public int Grade = 1;
        public float Elapsed; // time under this status in one go (poison worsens the longer it lasts)
    }

    // Both AI selection and execution use this policy. A pattern used by a skill is charged once.
    public static class SoulSkillUsePolicy
    {
        // Job requirement (healing is for priests). Skills never unlock by level: they are learned somewhere.
        public static string Blocked(SoulCombatant owner, SoulActiveSkillData skill)
        {
            if (!string.IsNullOrEmpty(skill.RequiredJob) && !(owner is SoulMercenary job && job.Job == skill.RequiredJob)) return $"{skill.RequiredJob}만 사용";
            return null;
        }

        public static bool CanUse(SoulCombatant owner, SoulActiveSkillData skill, SoulCombatant target)
        {
            if (owner == null || !owner.Alive || owner.Disabled || skill == null) return false;
            if (Blocked(owner, skill) != null) return false;
            if (owner.Grade(SoulStatus.Fear) >= 2) return false; // fear grade 2+: too shaken for any skill
            if (owner.Has(SoulStatus.Silence)) return false; // 봉인: no skill at all
            if (skill.Cleanse && skill.Trigger != SoulTrigger.AllyHurt && owner.Statuses.Count == 0) return false; // 탈피: only with something to shed
            if (skill.HpCost > 0 && owner.Hp <= 1) return false;
            if (owner.SkillCooldowns.TryGetValue(Id(skill), out float cooldown) && cooldown > 0) return false;
            if (skill.RequiredPattern != null && !owner.Patterns().Contains(skill.RequiredPattern)) return false;
            if (skill.Trigger == SoulTrigger.LowHealth && owner.Hp > owner.Stats.Total(StatType.MaxHp) * .35f) return false;
            if ((skill.Trigger == SoulTrigger.InRange || skill.Trigger == SoulTrigger.Cast) && (target == null || !target.Alive || Vector2.Distance(owner.Position, target.Position) > Range(owner, skill))) return false;
            if (NeedsTarget(skill) && (target == null || !target.Alive)) return false;
            if (skill.ExecuteBelow > 0 && (target == null || target.Hp > target.Stats.Total(StatType.MaxHp) * skill.ExecuteBelow)) return false; // 처형
            Costs(owner, skill, out float stamina, out float mana);
            return owner.Stamina >= stamina && owner.Mp >= mana;
        }

        static readonly System.Random echoRoll = new System.Random(11);

        public static bool TryCommit(SoulCombatant owner, SoulActiveSkillData skill, SoulCombatant target)
        {
            if (!CanUse(owner, skill, target)) return false;
            Costs(owner, skill, out float stamina, out float mana);
            // 메아리의 (special option): now and then a skill costs nothing
            float echo = owner.Stats.Total(StatType.FreeSkillChance);
            if (echo <= 0 || echoRoll.NextDouble() >= echo)
            {
                owner.Stamina -= stamina;
                owner.Mp -= mana;
            }
            // 주문 집중: an attack spell takes every stack gathered while it could not be cast
            if (Magical(skill) && skill.Damage != null && skill.Damage.Evaluate(owner.Stats.Combat) > 0)
            {
                owner.FocusPower = 1 + SoulDungeonSession.FocusPerStack * owner.Focus;
                owner.Focus = 0;
            }
            owner.SkillCooldowns[Id(skill)] = skill.Cooldown * (1 - Mathf.Clamp(owner.Stats.Total(StatType.SkillCooldownReduce), 0, .6f));
            owner.Cooldown = ActionTime(skill) / SoulCombat.ActionSpeed(owner);
            if (skill.HpCost > 0) owner.Hp = Mathf.Max(1, owner.Hp * (1 - skill.HpCost)); // 희생, 악마의 계약: paid in blood
            return true;
        }

        public static float ActionTime(SoulActiveSkillData skill)
            => skill.CastTime > 0 ? skill.CastTime : skill.RequiredPattern != null ? skill.RequiredPattern.ActionTime : 1f;

        // Pattern part gets pattern cost modifiers (e.g. human adaptability); the skill's own cost does not.
        public static void Costs(SoulCombatant owner, SoulActiveSkillData skill, out float stamina, out float mana)
        {
            stamina = mana = 0;
            if (skill.RequiredPattern != null) SoulCombat.PatternCost(owner, skill.RequiredPattern, out stamina, out mana);
            float scale = SoulCombat.WoundCostScale(owner) * CostScale(owner, skill);
            foreach (var cost in skill.Costs)
                if (cost.Resource == SoulResource.Stamina) stamina += cost.Amount * scale;
                else mana += cost.Amount * scale;
        }

        // Attack magic lands as a circle on the target (1.2 cells when the skill sets no radius).
        public const float DefaultSpellRadius = 1.2f;
        public static float SpellRadius(SoulCombatant owner, SoulActiveSkillData skill)
        {
            float radius = skill.Radius != null ? skill.Radius.Evaluate(owner.Stats.Combat) : 0;
            return radius > 0 ? radius : DefaultSpellRadius;
        }

        public static float Range(SoulCombatant owner, SoulActiveSkillData skill)
            => skill.Range > 0 ? skill.Range : skill.RequiredPattern != null ? skill.RequiredPattern.Range : 2.5f;

        public static string Id(SoulActiveSkillData skill) => string.IsNullOrEmpty(skill.SoulId) ? skill.name : skill.SoulId;

        // Skill level (library): +20% effect and −10% cost a level above 1. Monsters stay at 1.
        public const float PowerPerLevel = .2f, CostCutPerLevel = .1f;
        public static int Level(SoulCombatant owner, SoulActiveSkillData skill) => owner is SoulMercenary hero ? hero.SkillLevel(skill) : 1;
        public static float Power(SoulCombatant owner, SoulActiveSkillData skill)
            => (1 + PowerPerLevel * (Level(owner, skill) - 1)) * (Magical(skill) ? SpellPower(owner) : 1)
               * (Magical(skill) && owner.FocusPower > 1 && skill.Damage != null && skill.Damage.Evaluate(owner.Stats.Combat) > 0 ? owner.FocusPower : 1)
               * (skill.DamageKind == SoulDamageKind.Fire || skill.DamageKind == SoulDamageKind.Burn ? 1 + Mathf.Max(0, owner.Stats.Total(StatType.FireFocus)) : 1);

        // ── magic ──
        // A skill paid with MP is magic: its damage, healing, buffs and statuses scale with the caster's spell power -
        // 1 + SpellPower (+4% a point of magic) - times its focus: a staff, a wand or a holy symbol in hand is full power,
        // empty hands 80%, any other weapon (a bow, a sword...) half.
        public const float BareHandsFocus = .8f, OtherWeaponFocus = .5f;
        static readonly string[] FocusTags = { "staff", "wand", "holy" };

        public static bool Magical(SoulActiveSkillData skill)
        {
            if (skill == null || skill.Technique) return false; // a technique pays MP but is no spell
            foreach (var cost in skill.Costs) if (cost.Resource == SoulResource.Mana && cost.Amount > 0) return true;
            return false;
        }

        public static float Focus(SoulCombatant owner)
        {
            if (!(owner is SoulMercenary hero)) return 1f;
            var weapon = hero.Equipped(SoulEquipSlot.MainHand);
            float battle = Mathf.Max(0, owner.Stats.Total(StatType.BattleCaster)); // 검과 주문
            if (weapon == null) return Mathf.Min(1f, BareHandsFocus + battle);
            if (System.Array.IndexOf(FocusTags, weapon.Data.WeaponTag) >= 0) return 1f;
            // 마법사·성직자·소환사 (지팡이의 마력): StaffBound off with any other weapon; anyone else the old half
            float bound = Mathf.Clamp01(owner.Stats.Total(StatType.StaffBound));
            return Mathf.Min(1f, (bound > 0 ? 1 - bound : OtherWeaponFocus) + battle);
        }

        public static float SpellPower(SoulCombatant owner) => Mathf.Max(.1f, 1 + owner.Stats.Total(StatType.SpellPower)) * Focus(owner);

        // Durations grow half as fast as the power; a status gains a grade at x1.6 and another at x2.2.
        public static float DurationScale(float power) => 1 + .5f * (power - 1);
        public static int GradeBonus(float power) => power >= 2.2f ? 2 : power >= 1.6f ? 1 : 0;
        public static float CostScale(SoulCombatant owner, SoulActiveSkillData skill) => 1 - CostCutPerLevel * (Level(owner, skill) - 1);

        static bool NeedsTarget(SoulActiveSkillData skill)
            => skill.Damage != null && (skill.Damage.Flat > 0 || skill.Damage.Terms.Length > 0);
    }

    public static class SoulCombat
    {
        public const float MaxPatternCostReduce = .3f, MaxKnockbackResist = .9f, ControlImmunity = 2f, ChillSlow = .7f;
        public const int MaxBleedStacks = 5, MaxGrade = 3;
        // Poison eats a share of max HP each second by grade; the longer it lasts the more it takes away.
        public const float PoisonWeakenAfter = 4f, PoisonWitherAfter = 8f;
        public static float PoisonShare(int grade) => .005f + .005f * grade; // 1% / 1.5% / 2% of max HP a second
        // Bleed and burn: flat damage per second scaled by grade.
        public static float DotScale(int grade) => 1f + .3f * (grade - 1);
        public const string StatusBuffId = "status";

        public const int MaxRepeatStacks = 3;
        public const float LowHealth = .35f, MaxTotalCostReduce = .8f;

        public static void PatternCost(SoulCombatant actor, SoulPatternData pattern, out float stamina, out float mana)
        {
            float reduce = Mathf.Clamp(actor.Stats.Total(StatType.PatternCostReduce), 0, MaxPatternCostReduce);
            // 몸에 익은 무기: the same attack again, cheaper each time (up to 3 stacks)
            if (pattern.Category == SoulPatternCategory.Attack && pattern == actor.LastAttack)
                reduce += Mathf.Min(actor.AttackStreak, MaxRepeatStacks) * actor.Stats.Total(StatType.RepeatCostReduce);
            // 아드레날린: cheaper when close to falling
            if (actor.Hp < actor.Stats.Total(StatType.MaxHp) * LowHealth) reduce += actor.Stats.Total(StatType.LowHpCostReduce);
            stamina = pattern.StaminaCost * (1 - Mathf.Clamp(reduce, 0, MaxTotalCostReduce)) * WoundCostScale(actor)
                * SoulItemRules.LoadCostScale(actor.Stats.Total(StatType.LoadRatio)) * FatigueCostScale(actor);
            mana = pattern.ManaCost * WoundCostScale(actor);
        }

        // Wounds: +15% stamina and MP per wound; from the fourth wound a pattern fails 7% a wound (4: 7%, 5: 14%).
        public const float WoundCostStep = .15f, WoundFailStep = .07f;
        public const int WoundFailFrom = 4;
        public static int Wounds(SoulCombatant unit) => unit is SoulMercenary hero ? hero.Wounds : 0;
        public static float WoundCostScale(SoulCombatant unit) => 1 + WoundCostStep * Wounds(unit);
        public static float WoundFailChance(SoulCombatant unit) => Mathf.Max(0, Wounds(unit) - (WoundFailFrom - 1)) * WoundFailStep;

        public static bool CanPay(SoulCombatant actor, SoulPatternData pattern)
        {
            PatternCost(actor, pattern, out float stamina, out float mana);
            return actor.Stamina >= stamina && actor.Mp >= mana;
        }

        // 무념 동작: a pattern now and then costs nothing (FreePatternChance). A fixed seed keeps runs repeatable.
        static readonly System.Random freeRoll = new System.Random(7);

        public static void Pay(SoulCombatant actor, SoulPatternData pattern)
        {
            float free = actor.Stats.Total(StatType.FreePatternChance);
            if (free > 0 && freeRoll.NextDouble() < Mathf.Clamp01(free)) return;
            PatternCost(actor, pattern, out float stamina, out float mana);
            actor.Stamina -= stamina;
            actor.Mp -= mana;
        }

        public static float ActionSpeed(SoulCombatant actor)
            => Mathf.Clamp(actor.Stats.Total(StatType.ActionSpeed), .5f, 2f) * (actor.Has(SoulStatus.Chill) ? ChillSlow : 1f);

        public static float MoveSpeed(SoulCombatant actor)
            => actor.Stats.Total(StatType.MoveSpeed) * (actor.Has(SoulStatus.Chill) ? ChillSlow : 1f);

        // Down: a mercenary with this many wounds, hit while its stamina is at this share or less, lies there a while —
        // unless its will holds it up (다운 저항: that share of the falls it stands through, the rest shorter by half of it).
        public const int DownWounds = 2;
        public const float DownStamina = .1f, DownDuration = 2.5f, MaxDownResist = .75f;
        public static float DownResist(SoulCombatant unit) => Mathf.Clamp(unit.Stats.Total(StatType.DownResist), 0, MaxDownResist);

        public static void TickResources(SoulCombatant actor, float dt)
        {
            if (!actor.Alive) return;
            if (actor.DownTime > 0) actor.DownTime = Mathf.Max(0, actor.DownTime - dt);
            actor.Cooldown = Mathf.Max(0, actor.Cooldown - dt);
            actor.MoveTime = Mathf.Max(0, actor.MoveTime - dt);
            if (!(actor is SoulMercenary torn) || torn.Regenerates)
                actor.Hp = Mathf.Min(actor.Stats.Total(StatType.MaxHp), actor.Hp + actor.Stats.Total(StatType.HpRegen) * dt);
            else if (torn.Wounds >= SoulMercenary.BleedingWounds && actor.Hp > 1)
                actor.Hp = Mathf.Max(1, actor.Hp - actor.Stats.Total(StatType.MaxHp) * SoulMercenary.WoundBleed * dt);
            actor.Mp = Mathf.Min(actor.Stats.Total(StatType.MaxMp), actor.Mp + actor.Stats.Total(StatType.MpRegen) * actor.ManaRegenScale
                * (1 + Mathf.Max(0, actor.Stats.Total(StatType.ManaRegenRate))) * dt); // 명상: faster, always
            actor.Stamina = Mathf.Min(actor.Stats.Total(StatType.MaxStamina), actor.Stamina + actor.Stats.Total(StatType.StaminaRegen) * actor.StaminaRegenScale * dt);
            actor.TickTimedBuffs(dt);
            var ids = new List<string>(actor.SkillCooldowns.Keys);
            foreach (string id in ids) actor.SkillCooldowns[id] = Mathf.Max(0, actor.SkillCooldowns[id] - dt);

            if (actor.ImbueRemaining > 0) actor.ImbueRemaining = Mathf.Max(0, actor.ImbueRemaining - dt);
            if (actor.FollowupRemaining > 0) actor.FollowupRemaining = Mathf.Max(0, actor.FollowupRemaining - dt);
            for (int i = actor.Statuses.Count - 1; i >= 0; i--)
            {
                var status = actor.Statuses[i];
                float dps = status.DamagePerSecond * status.Stacks;
                if (status.Kind == SoulStatus.Poison) dps += actor.Stats.Total(StatType.MaxHp) * PoisonShare(status.Grade);
                else if (IsDamageOverTime(status.Kind)) dps *= DotScale(status.Grade);
                if (dps > 0)
                {
                    actor.Hp = Mathf.Max(0, actor.Hp - dps * dt);
                    if (actor is SoulMercenary wounded) wounded.TakeWoundDamage(dps * dt);
                    Defy(actor);
                }
                status.Elapsed += dt;
                status.Remaining -= dt;
                if (status.Remaining > 0) continue;
                actor.Statuses.RemoveAt(i);
                if (IsControl(status.Kind)) actor.StatusImmunity[status.Kind] = ControlImmunity;
            }
            var immune = new List<SoulStatus>(actor.StatusImmunity.Keys);
            foreach (var kind in immune) actor.StatusImmunity[kind] = Mathf.Max(0, actor.StatusImmunity[kind] - dt);
            RefreshStatusEffects(actor);
        }

        // ── status grades ────────────────────────────────────────

        static readonly StatType[] FearStats = { StatType.Attack, StatType.Armor, StatType.Accuracy, StatType.Evasion, StatType.MoveSpeed, StatType.ActionSpeed };

        // What a status does to the numbers at a grade, as shares of the current value (-.2 = 20% lower).
        public static List<(StatType stat, float share)> StatusShares(SoulStatus kind, int grade, float elapsed)
        {
            var shares = new List<(StatType, float)>();
            switch (kind)
            {
                case SoulStatus.Slow:
                    shares.Add((StatType.MoveSpeed, -(.05f + .1f * grade)));          // 15 / 25 / 35%
                    if (grade >= 3) shares.Add((StatType.ActionSpeed, -.1f));
                    break;
                case SoulStatus.Fear:
                    foreach (var stat in FearStats) shares.Add((stat, -.1f * grade)); // 10 / 20 / 30%, every stat
                    break;
                case SoulStatus.Confuse:
                    shares.Add((StatType.Accuracy, -(.05f + .05f * grade)));         // 10 / 15 / 20%
                    break;
                case SoulStatus.Weaken:
                    shares.Add((StatType.Attack, -(.05f + .05f * grade)));
                    shares.Add((StatType.Armor, -(.05f + .05f * grade)));
                    break;
                case SoulStatus.Poison:
                    if (elapsed >= PoisonWeakenAfter) { shares.Add((StatType.Attack, -(.05f + .05f * grade))); shares.Add((StatType.Armor, -(.05f + .05f * grade))); }
                    if (elapsed >= PoisonWitherAfter) { shares.Add((StatType.HpRegen, -1f)); shares.Add((StatType.StaminaRegen, -.5f)); }
                    break;
            }
            return shares;
        }

        // Confusion: chance per action to stumble instead of acting.
        public static float StumbleChance(int grade) => grade <= 0 ? 0 : .15f * grade;

        // Status stat changes live on one timed-buff layer ("status"), recomputed only when a status, its grade
        // or its poison stage changes — so they survive stat rebuilds like any timed buff.
        public static void RefreshStatusEffects(SoulCombatant unit)
        {
            var key = new System.Text.StringBuilder();
            foreach (var status in unit.Statuses)
            {
                key.Append((int)status.Kind).Append(':').Append(status.Grade);
                if (status.Kind == SoulStatus.Poison) key.Append(status.Elapsed >= PoisonWitherAfter ? 'w' : status.Elapsed >= PoisonWeakenAfter ? 'k' : '-');
                key.Append('|');
            }
            string signature = key.ToString();
            if (signature == unit.StatusSignature) return;
            unit.StatusSignature = signature;
            var totals = new Dictionary<StatType, float>();
            foreach (var status in unit.Statuses)
                foreach (var (stat, share) in StatusShares(status.Kind, status.Grade, status.Elapsed))
                    totals[stat] = (totals.TryGetValue(stat, out float sum) ? sum : 0) + share;
            var bonuses = new List<SoulStatBonus>();
            foreach (var entry in totals)
            {
                float current = unit.Stats.Combat.Get(entry.Key) - unit.Stats.Combat.GetLayer("timed:" + StatusBuffId, entry.Key);
                float value = current * Mathf.Max(-1f, entry.Value);
                if (value != 0) bonuses.Add(new SoulStatBonus { Stat = entry.Key, Value = value });
            }
            unit.AddTimedBuff(StatusBuffId, bonuses.ToArray(), bonuses.Count > 0 ? float.MaxValue : 0);
        }

        // Starts an attack: the cost and the action time are paid now, the hit lands when the telegraphed area
        // resolves (SoulDungeonSession). basicAttack: weapon fallback — scaled damage, no pierce/status/knockback.
        public static bool TryBeginAttack(SoulCombatant attacker, SoulCombatant target, SoulPatternData pattern, out SoulHit hit,
            float damageScale = 1f, bool basicAttack = false, bool free = false)
        {
            hit = default;
            if (!attacker.Alive || target == null || !target.Alive || attacker.Cooldown > 0 || attacker.Disabled) return false;
            if (pattern == null || pattern.Category != SoulPatternCategory.Attack) return false;
            if (!free && !CanPay(attacker, pattern)) return false;
            if (Vector2.Distance(attacker.Position, target.Position) > pattern.Range + target.Stats.Radius) return false;
            if (!free) Pay(attacker, pattern);
            if (pattern == attacker.LastAttack) attacker.AttackStreak++;
            else { attacker.LastAttack = pattern; attacker.AttackStreak = 1; }
            attacker.Cooldown = pattern.ActionTime / ActionSpeed(attacker);
            hit = new SoulHit
            {
                School = pattern.DamageSchool,
                Kind = pattern.DamageKind,
                Raw = pattern.Damage.Evaluate(attacker.Stats.Combat) * damageScale,
                Melee = pattern.Range < 2.5f,
                Pierce = basicAttack ? 0 : pattern.Pierce.Evaluate(attacker.Stats.Combat),
                Knockback = basicAttack ? 0 : pattern.Knockback
            };
            if (!basicAttack) hit.SetStatus(pattern.Status, attacker.Stats.Combat);
            if (attacker.ImbueRemaining > 0 && attacker.Imbue != null) hit.SetStatus(attacker.Imbue.Kind, attacker.ImbueChance, attacker.ImbueDuration, attacker.ImbueDps, attacker.ImbueGrade);
            FirePassives(attacker, SoulTrigger.OnAttack, target);
            return true;
        }

        public static bool TrySkill(SoulCombatant caster, SoulCombatant target, SoulActiveSkillData skill, System.Random random, out SoulHitResult result)
        {
            result = default;
            if (caster.Cooldown > 0 || !SoulSkillUsePolicy.TryCommit(caster, skill, target)) return false;
            if (skill.Damage != null && target != null && target.Alive && skill.Damage.Evaluate(caster.Stats.Combat) > 0)
            {
                var hit = new SoulHit
                {
                    School = skill.DamageSchool, Kind = skill.DamageKind,
                    Raw = skill.Damage.Evaluate(caster.Stats.Combat) * SoulSkillUsePolicy.Power(caster, skill),
                    Melee = SoulSkillUsePolicy.Range(caster, skill) < 2.5f,
                    Knockback = skill.Knockback
                };
                hit.SetStatus(skill.Status, caster.Stats.Combat);
                hit.SetStatus(skill.Extra, caster.Stats.Combat);
                if (SoulSkillUsePolicy.Magical(skill)) hit.Empower(SoulSkillUsePolicy.SpellPower(caster));
                result = Resolve(caster, target, hit, random);
                if (!result.Dodged) FirePassives(caster, SoulTrigger.OnAttackLanded, target);
                if (result.Killed) FirePassives(caster, SoulTrigger.OnKill, target);
            }
            else if (skill.Status != null && skill.Status.Kind != SoulStatus.None && target != null && target.Alive)
            {
                // a mark or a trick with no blow (사냥감 표식, 모래 뿌리기): only the status
                var stats = caster.Stats.Combat;
                if (ApplyStatus(target, skill.Status.Kind, skill.Status.Chance.Evaluate(stats), skill.Status.Duration.Evaluate(stats),
                    skill.Status.DamagePerSecond.Evaluate(stats), random, Mathf.Min(MaxGrade, skill.Status.GradeFor(stats) + Mathf.FloorToInt(caster.Stats.Total(StatType.StatusGrade)))))
                    result.Applied = skill.Status.Kind;
            }
            if (skill.Cleanse && skill.Trigger != SoulTrigger.AllyHurt) caster.Statuses.Clear(); // 탈피
            ApplySelfBuffs(caster, skill);
            if (skill.Heal != null)
                caster.Hp = Mathf.Min(caster.Stats.Total(StatType.MaxHp), caster.Hp + Mathf.Max(0, skill.Heal.Evaluate(caster.Stats.Combat) * SoulSkillUsePolicy.Power(caster, skill)));
            return true;
        }

        // Dodging and rolling are no longer a die roll inside the hit: a unit that owns one of those patterns
        // physically leaves the telegraphed area before it resolves (SoulDungeonSession.TryEvade). What is left
        // here is what still applies once the area does catch the body — blocking and countering.
        public static bool Evasive(SoulPatternData pattern)
            => pattern != null && (pattern.DefenseMode == SoulDefenseMode.Dodge || pattern.DefenseMode == SoulDefenseMode.Roll) && pattern.Category == SoulPatternCategory.Defense;

        // 치명타: a blow or a spell lands for CritMultiplier (+ 치명타 피해) × at 치명타 확률 (none without it).
        public const float CritMultiplier = 1.5f;
        public static float CritScale(SoulCombatant attacker) => CritMultiplier + Mathf.Max(0, attacker.Stats.Total(StatType.CritDamage));

        public static SoulHitResult Resolve(SoulCombatant attacker, SoulCombatant target, SoulHit hit, System.Random random)
        {
            var result = new SoulHitResult();
            float evasion = SoulDungeonSession.CanEvade(target) ? target.Stats.Total(StatType.Evasion) : 0; // 회피 본능 없는 몬스터
            float hitChance = Mathf.Clamp(attacker.Stats.Total(StatType.Accuracy) - evasion + target.Stats.Radius * .05f, .05f, 1f);
            if (random.NextDouble() > hitChance) { result.Dodged = true; return result; }

            // A disabled target (stun/freeze/petrify) cannot react with a defense pattern.
            float guard = 1f;
            var defenses = new List<SoulPatternData>();
            if (!target.Disabled)
                foreach (var pattern in target.Patterns()) if (!Evasive(pattern)) defenses.Add(pattern);
            if (!target.Disabled)
                foreach (var pattern in defenses)
                {
                    if (pattern == null || pattern.Category != SoulPatternCategory.Defense || !CanPay(target, pattern)) continue;
                    if (pattern.DefenseMode == SoulDefenseMode.Guard)
                    {
                        Pay(target, pattern);
                        guard = Mathf.Clamp01(pattern.GuardMultiplier);
                        result.Guarded = true;
                        var riposte = ChainPattern(target, SoulChainTrigger.Guard); // 막고 찌르기
                        if (riposte != null) OpenFollowup(target, riposte);
                    }
                    else if (pattern.DefenseMode == SoulDefenseMode.Counter)
                    {
                        // 받아치기: only against melee; partial block plus an immediate strike back.
                        if (!hit.Melee) continue;
                        Pay(target, pattern);
                        guard = Mathf.Clamp01(pattern.GuardMultiplier);
                        float counter = Mathf.Max(1, pattern.Damage.Evaluate(target.Stats.Combat) - attacker.Stats.Total(StatType.Armor) * .5f);
                        attacker.Hp = Mathf.Max(0, attacker.Hp - counter);
                        result.Countered = true;
                        result.CounterDamage = counter;
                    }
                    else continue; // Cover works through interception (session), not here
                    break;
                }

            float raw = Mathf.Max(0, hit.Raw) * ExecuteScale(attacker, target, hit.Melee);
            // 사냥꾼의: more against elites and bosses
            if (target is SoulMonster big && (big.Data.Elite || big.Data.Guardian)) raw *= 1 + attacker.Stats.Total(StatType.EliteDamage);
            // 퇴마: the dead go back harder
            if (target is SoulMonster dead && dead.Data.Undead) raw *= 1 + Mathf.Max(0, attacker.Stats.Total(StatType.HolyBane));
            // 위기의 일격: the lower the attacker's HP, the harder it hits
            float desperate = attacker.Stats.Total(StatType.LowHpDamage);
            if (desperate > 0) raw *= 1 + desperate * (1 - Mathf.Clamp01(attacker.Hp / Mathf.Max(1, attacker.Stats.Total(StatType.MaxHp))));
            // 치명타 (blows and spells, not a status's ticking)
            float crit = Mathf.Clamp01(attacker.Stats.Total(StatType.CritChance));
            if (crit > 0 && hit.School != SoulDamageSchool.Other && random.NextDouble() < crit) { raw *= CritScale(attacker); result.Critical = true; }
            float resist;
            if (hit.School == SoulDamageSchool.Physical)
            {
                float ignore = Mathf.Clamp01(Mathf.Max(hit.ArmorIgnore, attacker.Stats.Total(StatType.ArmorShred))); // 파쇄의
                raw = Mathf.Max(1, raw - Mathf.Max(0, target.Stats.Total(StatType.Armor) * (1 - ignore) - hit.Pierce));
                resist = target.Stats.Total(StatType.PhysicalResist);
            }
            else if (hit.School == SoulDamageSchool.Magic) resist = target.Stats.Total(StatType.MagicResist);
            else resist = StatusResist(target, hit.Kind);
            result.Damage = DamageMath.AfterDefense(raw * guard,
                Mathf.Clamp(resist, 0, .85f), 0, .65f, .25f, .85f) * (1 - ElementResist(target, hit.Kind));
            // 연격의: the blow lands twice
            float twice = attacker.Stats.Total(StatType.DoubleHit);
            if (twice > 0 && hit.School == SoulDamageSchool.Physical && random.NextDouble() < twice) result.Damage *= 2;
            // weapon elements (equipment): extra damage of each element the attacker's weapon carries
            float knock = hit.Knockback;
            if (hit.School == SoulDamageSchool.Physical) result.Damage += ElementDamage(attacker, target, random, ref knock);
            result.Damage = Absorb(target, result.Damage);
            target.Hp = Mathf.Max(0, target.Hp - result.Damage);
            if (target is SoulMercenary wounded)
            {
                wounded.TakeWoundDamage(result.Damage);
                // wounded (2+) and out of breath (stamina 10% or less): the blow puts it on the ground
                if (wounded.Alive && result.Damage > 0 && wounded.DownTime <= 0 && wounded.Wounds >= DownWounds
                    && wounded.Stamina <= wounded.Stats.Total(StatType.MaxStamina) * DownStamina)
                {
                    float steady = DownResist(wounded);
                    if (random.NextDouble() >= steady) wounded.DownTime = DownDuration * (1 - steady * .5f);
                }
            }
            Defy(target);
            // 흡혈의
            float steal = attacker.Stats.Total(StatType.LifeSteal);
            if (steal > 0 && attacker.Alive) attacker.Hp = Mathf.Min(attacker.Stats.Total(StatType.MaxHp), attacker.Hp + result.Damage * steal);
            // 가시의: a melee blow bites back
            float thorns = target.Stats.Total(StatType.Thorns);
            if (thorns > 0 && hit.Melee && attacker.Alive) { attacker.Hp = Mathf.Max(0, attacker.Hp - result.Damage * thorns); Defy(attacker); }
            // 결정 반사: magic bounces back
            float reflect = Mathf.Clamp(target.Stats.Total(StatType.SpellReflect), 0, MaxSpellReflect);
            if (reflect > 0 && hit.School == SoulDamageSchool.Magic && attacker.Alive && attacker != target) { attacker.Hp = Mathf.Max(0, attacker.Hp - result.Damage * reflect); Defy(attacker); }
            result.Killed = !target.Alive;
            if (result.Killed)
            {
                // 숨결의: a kill gives breath back
                float breath = attacker.Stats.Total(StatType.KillStamina);
                if (breath > 0) attacker.Stamina = Mathf.Min(attacker.Stats.Total(StatType.MaxStamina), attacker.Stamina + attacker.Stats.Total(StatType.MaxStamina) * breath);
                float harvest = attacker.Stats.Total(StatType.KillMana); // 죽음의 수확
                if (harvest > 0) attacker.Mp = Mathf.Min(attacker.Stats.Total(StatType.MaxMp), attacker.Mp + attacker.Stats.Total(StatType.MaxMp) * harvest);
            }
            else
            {
                int deeper = Mathf.FloorToInt(attacker.Stats.Total(StatType.StatusGrade)); // 깊은 저주, 독손톱 …
                if (ApplyStatus(target, hit.Status, hit.StatusChance, hit.StatusDuration, hit.StatusDps, random, Mathf.Min(MaxGrade, hit.StatusGrade + deeper))) result.Applied = hit.Status;
                if (ApplyStatus(target, hit.Extra, hit.ExtraChance, hit.ExtraDuration, hit.ExtraDps, random, Mathf.Min(MaxGrade, hit.ExtraGrade + deeper)) && result.Applied == SoulStatus.None) result.Applied = hit.Extra;
                if (knock > 0) result.Knockback = KnockbackDistance(attacker, target, knock);
            }
            FirePassives(target, SoulTrigger.OnHit, attacker);
            return result;
        }

        // 마나 보호막: MP pays for part of the blow · 대신 맞기: a guardian close by takes its share.
        public const float MaxManaShield = .8f, GuardReach = 6f, MaxSpellReflect = .6f;
        public const string BarrierKey = "holy_shield";
        static float Absorb(SoulCombatant target, float damage)
        {
            // 신성한 방패 first
            if (target.Barrier > 0 && target.TimedBuffs.Exists(b => b.Id == BarrierKey))
            {
                float taken = Mathf.Min(damage, target.Barrier);
                target.Barrier -= taken;
                damage -= taken;
            }
            float shield = Mathf.Clamp(target.Stats.Total(StatType.ManaShield), 0, MaxManaShield);
            if (shield > 0 && target.Mp > 0)
            {
                float paid = Mathf.Min(damage * shield, target.Mp);
                target.Mp -= paid;
                damage -= paid;
            }
            var guardian = target.Guardian;
            if (guardian != null && guardian != target && guardian.Alive && target.GuardShare > 0 && target.TimedBuffs.Exists(b => b.Id == target.GuardBuff)
                && Vector2.Distance(guardian.Position, target.Position) <= GuardReach)
            {
                float taken = damage * target.GuardShare;
                guardian.Hp = Mathf.Max(0, guardian.Hp - taken);
                Defy(guardian);
                damage -= taken;
            }
            return damage;
        }

        // ── elements ─────────────────────────────────────────────

        static readonly (StatType attack, SoulDamageKind kind)[] WeaponElements =
        {
            (StatType.FireAttack, SoulDamageKind.Fire), (StatType.ColdAttack, SoulDamageKind.Cold),
            (StatType.LightningAttack, SoulDamageKind.Lightning), (StatType.EarthAttack, SoulDamageKind.Stone),
            (StatType.WindAttack, SoulDamageKind.Wind), (StatType.ToxicAttack, SoulDamageKind.Poison),
        };

        public const float MaxElementResist = .75f;

        // The share of an element's damage the target shrugs off (0 for the plain physical kinds).
        public static float ElementResist(SoulCombatant target, SoulDamageKind kind)
        {
            StatType stat;
            switch (kind)
            {
                case SoulDamageKind.Fire: case SoulDamageKind.Burn: stat = StatType.FireResist; break;
                case SoulDamageKind.Cold: case SoulDamageKind.Frostbite: stat = StatType.ColdResist; break;
                case SoulDamageKind.Lightning: stat = StatType.LightningResist; break;
                case SoulDamageKind.Stone: stat = StatType.EarthResist; break;
                case SoulDamageKind.Wind: stat = StatType.WindResist; break;
                case SoulDamageKind.Poison: stat = StatType.ToxicResist; break;
                default: return 0;
            }
            return Mathf.Clamp(target.Stats.Total(stat), 0, MaxElementResist);
        }

        // Each element on the weapon adds its value as damage (less the target's resist to it) and may bring its
        // status: fire burns, cold chills, lightning stuns briefly, earth slows, poison poisons, wind pushes.
        // The status chance is 2% a point (a 최상급 +12 enchant: 24%).
        static float ElementDamage(SoulCombatant attacker, SoulCombatant target, System.Random random, ref float knockback)
        {
            float total = 0;
            foreach (var (stat, kind) in WeaponElements)
            {
                float value = attacker.Stats.Total(stat);
                if (value <= 0) continue;
                total += value * (1 - ElementResist(target, kind));
                float chance = value * .02f;
                switch (kind)
                {
                    case SoulDamageKind.Fire: ApplyStatus(target, SoulStatus.Burn, chance, 4, value * .5f, random); break;
                    case SoulDamageKind.Cold: ApplyStatus(target, SoulStatus.Chill, chance, 3, 0, random); break;
                    case SoulDamageKind.Lightning: ApplyStatus(target, SoulStatus.Stun, chance, .6f, 0, random); break;
                    case SoulDamageKind.Stone: ApplyStatus(target, SoulStatus.Slow, chance, 3, 0, random); break;
                    case SoulDamageKind.Poison: ApplyStatus(target, SoulStatus.Poison, chance, 5, 0, random); break;
                    case SoulDamageKind.Wind: if (random.NextDouble() < chance * 2) knockback += .3f; break;
                }
            }
            return total;
        }

        // 거부의 (special option): once a floor, a mercenary felled by a blow keeps 1 HP instead.
        public static void Defy(SoulCombatant unit)
        {
            if (unit.Alive || !(unit is SoulMercenary hero) || hero.DefyUsed || hero.Stats.Total(StatType.DeathDefy) <= 0) return;
            hero.DefyUsed = true;
            hero.Hp = 1;
            hero.Action = "거부 — 버텨냈다";
            float rage = hero.Stats.Total(StatType.DefyRage); // 불사: back up, and furious
            if (rage > 0) hero.AddTimedBuff("trait_defy_rage", new[] { new SoulStatBonus { Stat = StatType.ActionSpeed, Value = .3f }, new SoulStatBonus { Stat = StatType.LifeSteal, Value = .15f } }, rage);
        }

        // Tired mercenaries pay more for every pattern (SoulFatigue).
        public static float FatigueCostScale(SoulCombatant unit) => unit is SoulMercenary hero ? SoulFatigue.CostScale(hero.Fatigue) : 1f;

        public const float ExecuteThreshold = .35f;

        // 사냥 본능 (beastkin): melee hits on targets under 35% HP gain ExecuteBonus.
        public static float ExecuteScale(SoulCombatant attacker, SoulCombatant target, bool melee)
        {
            if (target == null || !melee) return 1f;
            float bonus = attacker.Stats.Total(StatType.ExecuteBonus);
            return bonus > 0 && target.Hp < target.Stats.Total(StatType.MaxHp) * ExecuteThreshold ? 1f + bonus : 1f;
        }

        public static void ApplySelfBuffs(SoulCombatant caster, SoulActiveSkillData skill)
        {
            if (skill.SelfBuffs == null || skill.SelfBuffs.Length == 0) return;
            var bonuses = new SoulStatBonus[skill.SelfBuffs.Length];
            for (int i = 0; i < bonuses.Length; i++)
                bonuses[i] = new SoulStatBonus { Stat = skill.SelfBuffs[i].Stat, Value = skill.SelfBuffs[i].Amount.Evaluate(caster.Stats.Combat) * SoulSkillUsePolicy.Power(caster, skill) };
            float power = SoulSkillUsePolicy.Magical(skill) ? SoulSkillUsePolicy.SpellPower(caster) : 1f;
            caster.AddTimedBuff(string.IsNullOrEmpty(skill.SoulId) ? skill.name : skill.SoulId, bonuses, skill.Duration.Evaluate(caster.Stats.Combat) * SoulSkillUsePolicy.DurationScale(power));
        }

        // Heavier attackers push lighter targets further; the target's knockback resist cuts the distance.
        public static float KnockbackDistance(SoulCombatant attacker, SoulCombatant target, float force)
            => force * Mathf.Clamp(attacker.Stats.Weight / Mathf.Max(1f, target.Stats.Weight), .5f, 2f)
                     * (1 - Mathf.Clamp(target.Stats.Total(StatType.KnockbackResist), 0, MaxKnockbackResist));

        // One resist value lowers the apply chance fully, the duration by half and the grade in proportion
        // (grade × (1 - resist), rounded, at least 1). DoT damage itself is not reduced again. Control states
        // cannot be extended while active (a higher grade still replaces a lower one) and grant a short
        // immunity when they end, so repeated stuns cannot lock a unit forever.
        public static bool ApplyStatus(SoulCombatant target, SoulStatus kind, float chance, float duration, float dps, System.Random random, int grade = 1)
        {
            if (kind == SoulStatus.None || !target.Alive || duration <= 0) return false;
            if (target.StatusImmunity.TryGetValue(kind, out float immune) && immune > 0) return false;
            float resist = Mathf.Clamp(StatusResist(target, kind), 0, .9f);
            if (random.NextDouble() >= Mathf.Clamp01(chance) * (1 - resist)) return false;
            duration *= 1 - resist * .5f;
            dps = Mathf.Max(0, dps);
            grade = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(1, grade) * (1 - resist)), 1, MaxGrade);

            var current = target.Statuses.Find(status => status.Kind == kind);
            if (current == null)
            {
                target.Statuses.Add(new SoulStatusState { Kind = kind, Remaining = duration, Grade = grade, DamagePerSecond = IsDamageOverTime(kind) ? dps : 0 });
                RefreshStatusEffects(target);
                return true;
            }
            if (IsControl(kind))
            {
                if (grade <= current.Grade) return false;
                current.Grade = grade;
                RefreshStatusEffects(target);
                return true;
            }
            current.Grade = Mathf.Max(current.Grade, grade);
            if (kind == SoulStatus.Bleed) current.Stacks = Mathf.Min(MaxBleedStacks, current.Stacks + 1);
            current.Remaining = Mathf.Max(current.Remaining, duration);
            if (IsDamageOverTime(kind)) current.DamagePerSecond = Mathf.Max(current.DamagePerSecond, dps);
            RefreshStatusEffects(target);
            return true;
        }

        // ── chains (연계) ──────────────────────────────────────────

        public static SoulPatternData ChainPattern(SoulCombatant unit, SoulChainTrigger trigger)
        {
            foreach (var pattern in unit.Patterns())
                if (pattern != null && pattern.Category == SoulPatternCategory.Chain && pattern.ChainTrigger == trigger) return pattern;
            return null;
        }

        // The next attack within the window is free and stronger; an instant chain cuts the running action short.
        public static void OpenFollowup(SoulCombatant unit, SoulPatternData chain)
        {
            unit.FollowupRemaining = chain.FollowupWindow;
            unit.FollowupMultiplier = chain.FollowupMultiplier;
            unit.FollowupArmorIgnore = chain.FollowupArmorIgnore;
            if (chain.FollowupInstant) unit.Cooldown = 0;
        }

        public static bool IsControl(SoulStatus kind)
            => kind == SoulStatus.Freeze || kind == SoulStatus.Stun || kind == SoulStatus.Fear || kind == SoulStatus.Petrify;

        public static bool IsDamageOverTime(SoulStatus kind)
            => kind == SoulStatus.Bleed || kind == SoulStatus.Poison || kind == SoulStatus.Burn;

        public static string StatusName(SoulStatus kind)
        {
            switch (kind)
            {
                case SoulStatus.Bleed: return "출혈";
                case SoulStatus.Poison: return "중독";
                case SoulStatus.Burn: return "화상";
                case SoulStatus.Chill: return "냉기";
                case SoulStatus.Freeze: return "빙결";
                case SoulStatus.Stun: return "스턴";
                case SoulStatus.Fear: return "공포";
                case SoulStatus.Petrify: return "석화";
                case SoulStatus.Slow: return "둔화";
                case SoulStatus.Confuse: return "혼란";
                case SoulStatus.Weaken: return "쇠약";
                case SoulStatus.Silence: return "침묵";
                default: return "";
            }
        }

        static float StatusResist(SoulCombatant target, SoulStatus kind)
        {
            switch (kind)
            {
                case SoulStatus.Bleed: return target.Stats.Total(StatType.BleedResist);
                case SoulStatus.Poison: return target.Stats.Total(StatType.PoisonResist);
                case SoulStatus.Burn: return target.Stats.Total(StatType.BurnResist);
                case SoulStatus.Chill: return target.Stats.Total(StatType.ChillResist);
                // Freeze is a control state: will (via stun resist) assists the chill resist.
                case SoulStatus.Freeze: return target.Stats.Total(StatType.ChillResist) + target.Stats.Total(StatType.StunResist) * .5f;
                case SoulStatus.Stun: return target.Stats.Total(StatType.StunResist);
                case SoulStatus.Fear: return target.Stats.Total(StatType.FearResist);
                case SoulStatus.Petrify: return target.Stats.Total(StatType.PetrifyResist);
                // new kinds read the stat that fights them: legs (agility), nerve (will), body (poison resist)
                case SoulStatus.Slow: return target.Stats.Total(StatType.Agility) * .01f;
                case SoulStatus.Confuse: return target.Stats.Total(StatType.FearResist);
                case SoulStatus.Weaken: return target.Stats.Total(StatType.PoisonResist);
                case SoulStatus.Silence: return target.Stats.Total(StatType.StunResist);
                default: return 0;
            }
        }

        static float StatusResist(SoulCombatant target, SoulDamageKind kind)
        {
            switch (kind)
            {
                case SoulDamageKind.Bleed: return target.Stats.Total(StatType.BleedResist);
                case SoulDamageKind.Poison: return target.Stats.Total(StatType.PoisonResist);
                case SoulDamageKind.Burn: return target.Stats.Total(StatType.BurnResist);
                case SoulDamageKind.Frostbite: return target.Stats.Total(StatType.ChillResist);
                default: return 0;
            }
        }

        public static void FirePassives(SoulCombatant owner, SoulTrigger trigger, SoulCombatant target)
        {
            foreach (var passive in owner.Passives())
            {
                if (passive == null || passive.SoulEvent != trigger) continue;
                string key = passive.SoulId + ":" + (target == null ? "none" : target.CombatId);
                int count = owner.PassiveCounters.TryGetValue(key, out int previous) ? previous + 1 : 1;
                owner.PassiveCounters[key] = count;
                if (count % Mathf.Max(1, passive.Every) != 0) continue;
                float heal = passive.Heal.Evaluate(owner.Stats.Combat);
                if (heal > 0) owner.Hp = Mathf.Min(owner.Stats.Total(StatType.MaxHp), owner.Hp + heal);
                if (passive.Stamina > 0) owner.Stamina = Mathf.Min(owner.Stats.Total(StatType.MaxStamina), owner.Stamina + passive.Stamina);
                float damage = passive.Damage.Evaluate(owner.Stats.Combat);
                if (damage > 0 && target != null && target.Alive)
                    target.Hp = Mathf.Max(0, target.Hp - damage);
            }
        }
    }
}
