using System;
using System.Collections.Generic;
using UnityEngine;
using SoulMercenaries;

namespace SoulMercenaries
{
    public sealed class SoulStatBuild
    {
        public readonly UnitStat Upper = new UnitStat();
        public readonly UnitStat Combat = new UnitStat();
        public UnitAppearanceData Appearance;
        public float Height, Weight, Radius;

        public float Total(StatType id) => Combat.Get(id);
        public float From(string key, StatType id) => Combat.GetLayer(key, id);
    }

    public abstract class SoulCombatant
    {
        public string CombatId;
        public float DownTime; // knocked down (on the ground, cannot act) for this many seconds
        public Vector2 Position;
        public float Hp, Mp, Stamina, Cooldown;
        public float MoveTime;
        public string Action;
        // What the UI shows: Action, but held for a moment so a label flipping every tick stays readable.
        public string ShownAction;
        public float ShownFor;
        public SoulCombatant CurrentTarget; // who this unit is fighting right now (flank bonus, escort)
        public Vector2 Facing = Vector2.right; // last direction of movement or attack (forward patterns)
        public SoulStatBuild Stats { get; protected set; }
        public bool Alive => Hp > 0;
        public readonly Dictionary<string, float> SkillCooldowns = new Dictionary<string, float>();
        public readonly Dictionary<string, int> PassiveCounters = new Dictionary<string, int>();
        public readonly List<SoulStatusState> Statuses = new List<SoulStatusState>();
        public readonly Dictionary<SoulStatus, float> StatusImmunity = new Dictionary<SoulStatus, float>();
        public bool Has(SoulStatus kind) => Statuses.Exists(status => status.Kind == kind);
        public bool Disabled => DownTime > 0 || Has(SoulStatus.Stun) || Has(SoulStatus.Freeze) || Has(SoulStatus.Petrify);
        public readonly List<SoulTimedBuff> TimedBuffs = new List<SoulTimedBuff>();
        // Current grade of a status (0 = not affected).
        public int Grade(SoulStatus kind)
        {
            var status = Statuses.Find(s => s.Kind == kind);
            return status == null ? 0 : status.Grade;
        }
        // 대신 맞기: while the buff GuardBuff lasts, Guardian takes GuardShare of this unit's damage.
        public SoulCombatant Guardian;
        public float Barrier; // 신성한 방패: damage it takes before HP, while its buff (SoulCombat.BarrierKey) lasts
        public int Focus;             // 주문 집중 stacks, waiting for the next attack spell
        public float FocusPower = 1f; // what the last attack spell took from them (× its power)
        public float GuardShare;
        public string GuardBuff;
        // 독 바르기: attacks carry this status while ImbueRemaining > 0.
        public SoulStatusApply Imbue;
        public float ImbueRemaining;
        public string ImbueName;
        // The imbue's numbers, fixed when it was put on (an enchant: from its caster's magic and spell power).
        public float ImbueChance, ImbueDuration, ImbueDps;
        public int ImbueGrade = 1;
        public string StatusSignature; // what the "status" stat layer was last built from (SoulCombat.RefreshStatusEffects)
        // Share of StaminaRegen that applies right now: low in a fight, high while resting (set by the session).
        public float StaminaRegenScale = 1f;
        // Share of MpRegen that applies right now: 1 in a fight, more once things are quiet (set by the session).
        public float ManaRegenScale = 1f;
        // 회피 반격: after an evasion the next attack is free and stronger while this runs.
        public float FollowupRemaining, FollowupMultiplier = 1f, FollowupArmorIgnore;
        // 몸에 익은 무기: the attack pattern used last and how many times in a row.
        public SoulPatternData LastAttack;
        public int AttackStreak;
        // 연타 마무리: landed hits on one target in a row.
        public SoulCombatant ComboTarget;
        public int ComboCount;
        // 치고 빠지기 강화: movement steps that cost nothing.
        public int FreeSteps;

        // Timed buffs live on the combat layer "timed:<id>" and survive stat rebuilds (ReapplyTimedBuffs).
        public void AddTimedBuff(string id, SoulStatBonus[] bonuses, float duration)
        {
            TimedBuffs.RemoveAll(buff => buff.Id == id);
            Stats.Combat.RemoveKey("timed:" + id);
            if (duration <= 0 || bonuses == null || bonuses.Length == 0) return;
            TimedBuffs.Add(new SoulTimedBuff { Id = id, Bonuses = bonuses, Remaining = duration });
            foreach (var bonus in bonuses) Stats.Combat.Add(bonus.Stat, bonus.Value, "timed:" + id);
        }

        public void TickTimedBuffs(float dt)
        {
            for (int i = TimedBuffs.Count - 1; i >= 0; i--)
            {
                TimedBuffs[i].Remaining -= dt;
                if (TimedBuffs[i].Remaining > 0) continue;
                Stats.Combat.RemoveKey("timed:" + TimedBuffs[i].Id);
                TimedBuffs.RemoveAt(i);
            }
        }

        protected void ReapplyTimedBuffs()
        {
            StatusSignature = null; // status shares are rebuilt from the new totals on the next tick
            foreach (var buff in TimedBuffs)
                foreach (var bonus in buff.Bonuses) Stats.Combat.Add(bonus.Stat, bonus.Value, "timed:" + buff.Id);
        }
        public abstract List<SoulPatternData> Patterns();
        public abstract List<SoulActiveSkillData> ActiveSkills();
        public abstract List<SoulPassiveSkillData> Passives();
    }

    public enum SoulPatternFit { New, Owned, RaceBlocked, NeedsWeapon }

    public sealed class SoulTimedBuff
    {
        public string Id;
        public SoulStatBonus[] Bonuses;
        public float Remaining;
        public int Stacks = 1; // a stacking pattern buff (자연 교감)
    }

    public sealed class SoulMercenary : SoulCombatant
    {
        public readonly string Id, Name;
        public int RestPose; // resting in the dungeon: 0 standing, 1 sitting, 2 lying (views)
        public readonly SoulRaceData Race;
        public SoulAppearancePatch Look;
        public string Job;
        public SoulRole Role;
        public readonly List<SoulPassiveSkillData> StartingPassives = new List<SoulPassiveSkillData>();
        public SoulMapTrait MapTraits
        {
            get
            {
                var traits = SoulMapTrait.None;
                foreach (var passive in Passives()) traits |= passive.MapTraits;
                return traits;
            }
        }
        public SoulStatBonus[] GrowthWeights = Array.Empty<SoulStatBonus>();
        // 비성향 (the job's): no level-up point ever goes there — unless the mercenary's own tendency (a style) asks for it.
        public StatType[] WeakGrowth = Array.Empty<StatType>();

        // Every upper stat keeps this weight so growth stays close to random; race and job weights tilt it.
        // 비성향 stats get none (only the mercenary's own tendency counts there).
        public const float BaseGrowthWeight = 1f;

        // Level-up stats are rolled, not chosen: weighted by race + job tendency.
        public Dictionary<StatType, int> RollGrowth(int points, System.Random random)
        {
            var weights = new Dictionary<StatType, float>();
            for (int value = (int)StatType.Strength; value <= (int)StatType.PiercePower; value++) weights[(StatType)value] = BaseGrowthWeight;
            foreach (var bonus in Race.GrowthWeights) if (weights.ContainsKey(bonus.Stat)) weights[bonus.Stat] += Mathf.Max(0, bonus.Value);
            foreach (var bonus in GrowthWeights) if (weights.ContainsKey(bonus.Stat)) weights[bonus.Stat] += Mathf.Max(0, bonus.Value);
            foreach (var stat in WeakGrowth)
            {
                if (!weights.ContainsKey(stat)) continue;
                float own = 0;
                foreach (var bonus in GrowthWeights) if (bonus.Stat == stat) own += Mathf.Max(0, bonus.Value);
                weights[stat] = own;
            }
            float total = 0;
            foreach (var weight in weights.Values) total += weight;
            var result = new Dictionary<StatType, int>();
            for (int i = 0; i < points; i++)
            {
                double roll = random.NextDouble() * total;
                foreach (var entry in weights)
                {
                    roll -= entry.Value;
                    if (roll > 0) continue;
                    result[entry.Key] = result.TryGetValue(entry.Key, out int count) ? count + 1 : 1;
                    break;
                }
            }
            return result;
        }
        public readonly List<SoulItem> Equipment = new List<SoulItem>();
        public SoulItem Equipped(SoulEquipSlot slot) => Equipment.Find(item => item.Slot == slot);
        public float GearWeight { get { float total = 0; foreach (var item in Equipment) total += item.Weight; return total; } }
        // 거부의 (special option): once a floor a blow that would fell this mercenary leaves 1 HP.
        public bool DefyUsed;
        // Dungeon time spent tired (피로도 0~100): rises with time in the dungeon, falls when camping.
        public float Fatigue;
        public int FatigueTier;
        public readonly List<SoulData> Souls = new List<SoulData>();
        public readonly List<SoulPatternData> LearnedPatterns = new List<SoulPatternData>();
        public readonly List<SoulPatternData> StartingPatterns = new List<SoulPatternData>();
        public readonly List<SoulActiveSkillData> StartingActives = new List<SoulActiveSkillData>();
        // Skills are never learned by levelling: every one comes from somewhere (a teacher, a book, a quest...).
        // Whatever the route, it ends here.
        public readonly List<SoulActiveSkillData> LearnedActives = new List<SoulActiveSkillData>();
        // Library level of a known skill (1–3, from duplicate books). Set by the campaign; 1 when unknown.
        public readonly Dictionary<string, int> SkillLevels = new Dictionary<string, int>();
        public int SkillLevel(SoulActiveSkillData skill)
            => skill != null && SkillLevels.TryGetValue(SoulSkillUsePolicy.Id(skill), out int level) ? Mathf.Max(1, level) : 1;

        public bool LearnSkill(SoulActiveSkillData skill)
        {
            if (skill == null || AllActiveSkills().Contains(skill)) return false;
            LearnedActives.Add(skill);
            return true;
        }
        public readonly Dictionary<string, SoulStatBonus[]> Buffs = new Dictionary<string, SoulStatBonus[]>();
        public readonly Dictionary<StatType, float> BaseStats = new Dictionary<StatType, float>();
        public readonly Dictionary<StatType, float> LevelStats = new Dictionary<StatType, float>();
        // Bought at the training ground (layer "mercenary:training").
        public readonly Dictionary<StatType, float> TrainedStats = new Dictionary<StatType, float>();

        // ── wounds ───────────────────────────────────────────────
        // Damage taken piles up; every WoundThreshold of max HP leaves a wound. Rest and regeneration do not close
        // wounds — only a healing skill, a potion, a camp or the church. Each wound makes patterns dearer, from the
        // second one they can fail (SoulCombat.WoundCostScale / WoundFailChance). From the fourth, HP no longer
        // comes back by itself; the fifth bleeds it away (down to 1 HP, SoulCombat.TickResources).
        public const int MaxWounds = 5;
        public const float WoundThreshold = .6f;
        public const int NoRegenWounds = 4, BleedingWounds = 5;
        public const float WoundBleed = .004f; // share of max HP a second at the fifth wound
        public bool Regenerates => Wounds < NoRegenWounds;
        public int Wounds { get; private set; }
        public float WoundDamage { get; private set; }
        // Each wound locks 10% of max HP (shown grey at the end of the HP bar) until the wound is healed.
        public const float WoundHpLock = .1f;
        const string WoundLockKey = "wounds";
        public float LockedHp { get; private set; }
        public float FullMaxHp => Stats.Total(StatType.MaxHp) + LockedHp;

        public void TakeWoundDamage(float damage)
        {
            if (damage <= 0 || Wounds >= MaxWounds) return;
            WoundDamage += damage * (1 - Mathf.Clamp(Stats.Total(StatType.WoundResist), 0, .8f)); // 봉합의
            float step = Mathf.Max(1, FullMaxHp * WoundThreshold);
            int before = Wounds;
            while (WoundDamage >= step && Wounds < MaxWounds) { WoundDamage -= step; Wounds++; }
            if (Wounds != before) ApplyWoundLock();
        }

        public void HealWounds(int count)
        {
            Wounds = Mathf.Max(0, Wounds - Mathf.Max(0, count));
            if (Wounds == 0) WoundDamage = 0;
            ApplyWoundLock();
        }

        public void AddWounds(int count) { Wounds = Mathf.Clamp(Wounds + count, 0, MaxWounds); ApplyWoundLock(); }

        void ApplyWoundLock()
        {
            if (Stats == null) return;
            Stats.Combat.RemoveKey(WoundLockKey);
            LockedHp = Stats.Total(StatType.MaxHp) * WoundHpLock * Wounds;
            if (LockedHp > 0) Stats.Combat.Add(StatType.MaxHp, -LockedHp, WoundLockKey);
            Hp = Mathf.Min(Hp, Stats.Total(StatType.MaxHp));
        }
        // Who it is on the guild's roster (SoulRecruitData.Id, saved; null: nobody's — made from a job alone) and that
        // recruit, found again on load.
        public string Style;
        public bool ShowHelmet; // the helmet's look (the guild's eye on the head slot); hidden by default — its stats count either way
        public SoulRecruitData Recruit;
        public SoulStyleRarity Rarity => Recruit != null ? Recruit.Rarity : SoulStyleRarity.Normal;
        // Will gained from experiences (ally deaths, many kills), applied after a battle — design 7.1.
        public readonly Dictionary<StatType, float> MentalStats = new Dictionary<StatType, float>();

        // Hidden resolve (0–1, never shown): how long this mercenary keeps its nerve — when it calls a crisis, whether
        // it stays behind to cover a wounded comrade (SoulSessionPlan). A personal share fixed by the id (two
        // mercenaries with the same stats still differ), plus 정신력 and a little 마력 (a clearer head).
        public float Resolve => Mathf.Clamp01(.2f + .35f * Temperament + .03f * Stats.Total(StatType.Will) + .01f * Stats.Total(StatType.Magic));
        public float Temperament
        {
            get
            {
                uint hash = 2166136261; // FNV-1a: stable across runs and platforms (string.GetHashCode is not)
                foreach (char c in Id ?? "") hash = (hash ^ c) * 16777619;
                return hash % 1000 / 999f;
            }
        }
        public readonly HashSet<string> Experiences = new HashSet<string>();
        public int Level { get; private set; } = 1;
        public int Experience { get; private set; }
        public const int MaxLevel = 50, MilestoneLevels = 10;
        public int ExperienceToNext => Level >= MaxLevel ? 0 : ExperienceFor(Level);

        // Experience from level L to L+1: steep up to 10 (level 5 comes after about 335, level 10 — the level for
        // clearing floor 1 — after about 1,900), then +12% a level up to 50 (deeper floors pay much more).
        public static int ExperienceFor(int level)
        {
            if (level < MilestoneLevels) return Mathf.RoundToInt(52 * Mathf.Pow(1.33f, level - 1));
            return Mathf.RoundToInt(52 * Mathf.Pow(1.33f, MilestoneLevels - 1) * Mathf.Pow(1.12f, level - MilestoneLevels + 1));
        }

        // Every 10 levels: one more soul slot and one new pattern to pick (the only choice in levelling).
        public int SoulSlots => 1 + Level / MilestoneLevels;
        public bool HasFreeSoulSlot => Souls.Count < SoulSlots;
        public int PatternPicks { get; private set; }
        public bool CanPickPattern => PatternPicks > 0;

        // Dungeon floors cleared: grade 9 at the start, one grade up per floor, grade 1 after the last floor.
        public int HighestFloorCleared;
        public int Grade => Mathf.Clamp(9 - HighestFloorCleared, 1, 9);

        // Bestiary: every monster species this mercenary has fought down.
        public readonly Dictionary<string, SoulCodexEntry> Codex = new Dictionary<string, SoulCodexEntry>();

        public SoulMercenary(string id, string name, SoulRaceData race, IDictionary<StatType, float> initial, SoulStatRules rules)
        {
            if (race == null) throw new ArgumentNullException(nameof(race));
            Id = id; Name = name; Race = race;
            CombatId = id;
            foreach (var entry in initial) BaseStats.Add(entry.Key, entry.Value);
            Rebuild(rules);
            Hp = Stats.Total(StatType.MaxHp);
            Mp = Stats.Total(StatType.MaxMp);
            Stamina = Stats.Total(StatType.MaxStamina);
        }

        public void Rebuild(SoulStatRules rules)
        {
            Stats = HeroStatPipeline.Build(this, rules);
            ReapplyTimedBuffs();
            ApplyWoundLock();
            Hp = Mathf.Min(Hp, Stats.Total(StatType.MaxHp));
            Mp = Mathf.Min(Mp, Stats.Total(StatType.MaxMp));
            Stamina = Mathf.Min(Stamina, Stats.Total(StatType.MaxStamina));
        }

        public bool Absorb(SoulData soul, SoulStatRules rules)
        {
            if (soul == null || soul.CharacteristicStats == null || soul.CharacteristicStats.Length == 0 || !HasFreeSoulSlot || CoreBlocked(soul)) return false;
            Souls.Add(soul);
            Rebuild(rules);
            return true;
        }

        // Soul removal is deliberately restricted to the future church service.
        public bool ReleaseAtChurch(SoulData soul, SoulStatRules rules)
        {
            if (!Souls.Remove(soul)) return false;
            Rebuild(rules);
            return true;
        }

        // Levels come by themselves: the stat points are rolled (race + job tendency) and applied at once.
        // Returns how many levels were gained.
        public int AddExperience(int amount, SoulStatRules rules, System.Random random)
        {
            if (Level >= MaxLevel) return 0;
            Experience += Math.Max(0, amount);
            int gained = 0;
            while (Level < MaxLevel && Experience >= ExperienceToNext)
            {
                Experience -= ExperienceToNext;
                Level++;
                gained++;
                foreach (var entry in RollGrowth(SoulStatRules.PointsPerLevel(rules), random))
                    LevelStats[entry.Key] = LevelStats.TryGetValue(entry.Key, out float value) ? value + entry.Value : entry.Value;
                if (Level % MilestoneLevels == 0) PatternPicks++;
            }
            if (Level >= MaxLevel) Experience = 0;
            if (gained > 0)
            {
                float hp = Hp / Mathf.Max(1, Stats.Total(StatType.MaxHp));
                Rebuild(rules);
                Hp = Mathf.Max(Hp, hp * Stats.Total(StatType.MaxHp)); // a level keeps the health ratio
            }
            return gained;
        }

        // Loading a saved game puts the progress back as it was (no level-up rolls).
        public void RestoreProgress(int level, int experience, int picks, int wounds, float woundDamage)
        {
            Level = Mathf.Clamp(level, 1, MaxLevel);
            Experience = Mathf.Max(0, experience);
            PatternPicks = Mathf.Max(0, picks);
            Wounds = Mathf.Clamp(wounds, 0, MaxWounds);
            WoundDamage = Mathf.Max(0, woundDamage);
            ApplyWoundLock();
        }

        // The one manual step of growth: learning a pattern offered at a milestone level.
        public bool LearnPattern(SoulPatternData pattern, SoulStatRules rules)
        {
            if (!CanPickPattern || pattern == null || !pattern.Compatible(this) || AllPatterns().Contains(pattern)) return false;
            PatternPicks--;
            LearnedPatterns.Add(pattern);
            Rebuild(rules);
            return true;
        }

        // Records a defeated monster; true when it is the first of its kind for this mercenary.
        public bool RecordDefeat(SoulMonsterData monster, bool ownKill, int floor)
        {
            bool first = !Codex.TryGetValue(monster.Id, out var entry);
            if (first) Codex[monster.Id] = entry = new SoulCodexEntry { Monster = monster, FirstFloor = floor };
            entry.Defeated++;
            if (ownKill) entry.Kills++;
            return first;
        }

        public bool CoreBlocked(SoulData soul) => soul.CorePattern != null && !soul.CorePattern.RaceAllows(this);

        public SoulPatternFit Fit(SoulPatternData pattern)
        {
            if (!pattern.RaceAllows(this)) return SoulPatternFit.RaceBlocked;
            if (AllPatterns().Contains(pattern)) return SoulPatternFit.Owned;
            return pattern.WeaponAllows(this) ? SoulPatternFit.New : SoulPatternFit.NeedsWeapon;
        }

        // Locked patterns and skills stay owned but the mercenary never uses them (set in the detail popup).
        public readonly HashSet<UnityEngine.Object> Locked = new HashSet<UnityEngine.Object>();
        public int LockVersion { get; private set; }
        public bool IsLocked(UnityEngine.Object item) => item != null && Locked.Contains(item);
        public void ToggleLock(UnityEngine.Object item)
        {
            if (item == null) return;
            if (!Locked.Remove(item)) Locked.Add(item);
            LockVersion++;
        }

        // What the mercenary uses in a fight: everything owned minus what is locked.
        public override List<SoulPatternData> Patterns()
        {
            var result = AllPatterns();
            if (Locked.Count > 0) result.RemoveAll(pattern => Locked.Contains(pattern));
            return result;
        }

        public override List<SoulActiveSkillData> ActiveSkills()
        {
            var result = AllActiveSkills();
            if (Locked.Count > 0) result.RemoveAll(skill => Locked.Contains(skill));
            return result;
        }

        // Everything the mercenary carries, usable or not (a soul's bow pattern without a bow): for the UI.
        public List<SoulPatternData> OwnedPatterns()
        {
            var result = new List<SoulPatternData>();
            AddUnique(result, Race.StartingPatterns);
            AddUnique(result, StartingPatterns);
            foreach (var equipment in Equipment) AddUnique(result, equipment.Patterns);
            foreach (var soul in Souls) AddUnique(result, soul.Patterns);
            AddUnique(result, LearnedPatterns);
            return result;
        }

        // Why an owned pattern cannot be used right now (null = usable).
        public string BlockReason(SoulPatternData pattern)
        {
            if (!pattern.RaceAllows(this)) return "종족 제약 — 쓸 수 없음";
            if (!pattern.WeaponAllows(this)) return $"무기 필요 ({pattern.WeaponTag}) — 맞는 무기를 들면 사용";
            return null;
        }

        public List<SoulPatternData> AllPatterns()
        {
            var result = new List<SoulPatternData>();
            AddCompatible(result, Race.StartingPatterns);
            AddCompatible(result, StartingPatterns);
            foreach (var equipment in Equipment) AddCompatible(result, equipment.Patterns);
            foreach (var soul in Souls) AddCompatible(result, soul.Patterns);
            AddCompatible(result, LearnedPatterns);
            return result;
        }

        void AddCompatible(List<SoulPatternData> result, IEnumerable<SoulPatternData> candidates)
        {
            if (candidates == null) return;
            foreach (var pattern in candidates)
                if (pattern != null && pattern.Compatible(this) && !result.Contains(pattern)) result.Add(pattern);
        }

        public List<SoulActiveSkillData> AllActiveSkills()
        {
            var result = new List<SoulActiveSkillData>();
            AddUnique(result, StartingActives);
            AddUnique(result, LearnedActives);
            foreach (var equipment in Equipment) AddUnique(result, equipment.ActiveSkills);
            foreach (var soul in Souls) AddUnique(result, soul.ActiveSkills);
            return result;
        }

        public override List<SoulPassiveSkillData> Passives()
        {
            var result = new List<SoulPassiveSkillData>();
            AddUnique(result, Race.InnatePassives);
            AddUnique(result, StartingPassives);
            foreach (var equipment in Equipment) AddUnique(result, equipment.Passives);
            foreach (var soul in Souls) AddUnique(result, soul.Passives);
            return result;
        }

        static void AddUnique<T>(List<T> result, IEnumerable<T> values) where T : UnityEngine.Object
        {
            if (values == null) return;
            foreach (var value in values) if (value != null && !result.Contains(value)) result.Add(value);
        }
    }

    public sealed class SoulCodexEntry
    {
        public SoulMonsterData Monster;
        public int Defeated;   // with this mercenary in the party
        public int Kills;      // its own finishing blows
        public int FirstFloor;
    }

    public sealed class SoulMonster : SoulCombatant
    {
        public readonly SoulMonsterData Data;
        public int Group; // pack / horde: one engaged, all engage (0 = alone)
        public SoulMonster(SoulMonsterData data, Vector2 position, SoulStatRules rules, string id, float power = 1)
        {
            Data = data; CombatId = id; Position = position;
            Stats = HeroStatPipeline.Build(data, rules);
            // Deeper floors: health and attack multiply, armor grows with the square root.
            if (power > 1)
            {
                Stats.Combat.Add(StatType.MaxHp, Stats.Total(StatType.MaxHp) * (power - 1), "floor");
                Stats.Combat.Add(StatType.Attack, Stats.Total(StatType.Attack) * (power - 1), "floor");
                Stats.Combat.Add(StatType.Armor, Stats.Total(StatType.Armor) * (Mathf.Sqrt(power) - 1), "floor");
            }
            // Monsters fight on a deeper breath than mercenaries: one whole encounter without running dry.
            Stats.Combat.Add(StatType.MaxStamina, Stats.Total(StatType.MaxStamina) * (MonsterStaminaScale - 1), "monster:stamina");
            Stats.Combat.Add(StatType.StaminaRegen, Stats.Total(StatType.StaminaRegen) * (MonsterStaminaScale - 1), "monster:stamina");
            Hp = Stats.Total(StatType.MaxHp);
            Mp = Stats.Total(StatType.MaxMp);
            Stamina = Stats.Total(StatType.MaxStamina);
        }
        public const float MonsterStaminaScale = 1.6f;
        public override List<SoulPatternData> Patterns() => new List<SoulPatternData>(Data.Patterns);
        public override List<SoulActiveSkillData> ActiveSkills() => new List<SoulActiveSkillData>(Data.ActiveSkills);
        public override List<SoulPassiveSkillData> Passives() => new List<SoulPassiveSkillData>(Data.Passives);
    }
}

public static partial class HeroStatPipeline
{
    public static bool IsUpper(StatType id) => id >= StatType.Strength && id <= StatType.PiercePower;

    public static SoulStatBuild Build(SoulMercenary hero, SoulStatRules rules)
    {
        var build = new SoulStatBuild();
        foreach (var entry in hero.BaseStats) build.Upper.Add(entry.Key, entry.Value, "mercenary:base");
        foreach (var entry in hero.LevelStats) build.Upper.Add(entry.Key, entry.Value, "mercenary:level");
        foreach (var entry in hero.MentalStats) build.Upper.Add(entry.Key, entry.Value, "mercenary:mental");
        foreach (var entry in hero.TrainedStats) build.Upper.Add(entry.Key, entry.Value, "mercenary:training");
        Add(build.Upper, hero.Race.Bonuses, "race:" + hero.Race.Id);
        for (int i = 0; i < hero.Equipment.Count; i++) Add(build.Upper, hero.Equipment[i].Bonuses, "equipment:" + i);
        // A soul is meant to be felt the moment it goes in, so what it carries lands at full weight — the
        // characteristic stats of the monster it came from, multiplied, not sprinkled on. Rarity pays for it.
        for (int i = 0; i < hero.Souls.Count; i++) Add(build.Upper, hero.Souls[i].CharacteristicStats, "soul:" + i, SoulPower);
        foreach (var passive in hero.Passives()) Add(build.Upper, passive.AlwaysBonuses, "passive:" + passive.SoulId);
        foreach (var entry in hero.Buffs) Add(build.Upper, entry.Value, "buff:" + entry.Key);

        var keys = new List<string>(build.Upper.GetKeys());
        foreach (string key in keys)
            foreach (StatType id in Enum.GetValues(typeof(StatType)))
            {
                float value = build.Upper.GetLayer(key, id);
                if (value != 0) build.Combat.Add(id, value, key);
            }

        Convert(build, rules, keys);

        build.Height = hero.Race.Height;
        build.Weight = hero.Race.Weight;
        build.Appearance = JsonUtility.FromJson<UnitAppearanceData>(JsonUtility.ToJson(hero.Race.Appearance ?? new UnitAppearanceData()));
        SoulLooks.Dress(hero, build.Appearance); // own features and a little variation, the plain outfit
        foreach (var equipment in hero.Equipment)
        {
            var look = equipment.Look;
            if (!hero.ShowHelmet) look.Helmet = null; // a helmet's stats, not its look, unless shown: the face and hair stay its own
            look.Apply(build.Appearance);
        }
        foreach (var soul in hero.Souls)
        {
            build.Height *= soul.HeightMultiplier;
            build.Weight *= soul.WeightMultiplier;
            soul.Appearance.Apply(build.Appearance);
        }
        // Gear adds to the body weight but not to its size (the radius is taken before it).
        build.Radius = BodyRadius(build.Height, build.Weight);
        float gear = hero.GearWeight;
        build.Weight += gear;
        build.Combat.Set(StatType.BodyWeight, build.Weight, "body");
        float load = gear / SoulItemRules.CarryLimit(build.Upper.Get(StatType.Strength));
        build.Combat.Set(StatType.LoadRatio, load, "load");
        float moveScale = SoulItemRules.LoadMoveScale(load);
        if (moveScale < 1) build.Combat.Add(StatType.MoveSpeed, build.Combat.Get(StatType.MoveSpeed) * (moveScale - 1), "load");
        float cursed = build.Combat.Get(StatType.CursedHp);
        if (cursed > 0) build.Combat.Add(StatType.MaxHp, -build.Combat.Get(StatType.MaxHp) * Mathf.Clamp01(cursed), "cursed");
        return build;
    }

    public static SoulStatBuild Build(SoulMonsterData monster, SoulStatRules rules)
    {
        var build = new SoulStatBuild();
        Add(build.Upper, monster.Stats, "monster:" + monster.Id);
        var keys = new List<string>(build.Upper.GetKeys());
        foreach (string key in keys)
            foreach (StatType id in Enum.GetValues(typeof(StatType)))
            {
                float value = build.Upper.GetLayer(key, id);
                if (value != 0) build.Combat.Add(id, value, key);
            }
        Convert(build, rules, keys);
        build.Height = monster.Height; build.Weight = monster.Weight;
        build.Radius = BodyRadius(build.Height, build.Weight);
        build.Combat.Set(StatType.BodyWeight, build.Weight, "body");
        return build;
    }

    // Body half-width in map cells. Cross-section grows with sqrt(weight / height), so a stocky dwarf is wider
    // than a human without scaling linearly with weight. Capped below half a cell so every unit fits one open
    // cell; giant-size rules (narrow passages, wall overlap) will lift this cap explicitly later.
    public static float BodyRadius(float height, float weight)
        => Mathf.Clamp(.045f * Mathf.Sqrt(Mathf.Max(weight, 1f) / Mathf.Max(height, .5f)), .15f, .45f);

    static void Convert(SoulStatBuild build, SoulStatRules rules, List<string> keys)
    {
        foreach (var conversion in SoulStatRules.Effective(rules))
        {
            if (conversion.Formula == null) continue;
            build.Combat.Add(conversion.Output, conversion.Formula.Flat, "conversion:base");
            foreach (string key in keys)
                foreach (var term in conversion.Formula.Terms)
                {
                    float value = build.Upper.GetLayer(key, term.Stat) * term.Factor;
                    if (value != 0) build.Combat.Add(conversion.Output, value, key);
                }
        }
    }

    // How hard an absorbed soul hits the stat sheet. Souls are rare and permanent (only the church can take
    // one back out), so each one is a build-defining jump rather than a small increment.
    public const float SoulPower = 2.5f;

    static void Add(UnitStat stats, SoulStatBonus[] values, string key, float scale = 1f)
    {
        if (values == null) return;
        foreach (var value in values) stats.Add(value.Stat, value.Value * scale, key);
    }
}

public static partial class HeroStatResolver
{
    public static SoulStatBuild Resolve(SoulMercenary hero, SoulStatRules rules)
        => HeroStatPipeline.Build(hero, rules);
}
