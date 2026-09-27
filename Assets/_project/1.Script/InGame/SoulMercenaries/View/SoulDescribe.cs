using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    public struct SoulLine
    {
        public Sprite Icon;
        public string Text;
        public bool Header; // a small section title inside a tooltip ("구성", "효과")
        public bool Cost;   // a price (icon + number) the tooltip puts at the right end of its title
        public SoulLine(Sprite icon, string text) { Icon = icon; Text = text; Header = false; Cost = false; }
        public static SoulLine Title(string text) => new SoulLine(null, text) { Header = true };
        public static SoulLine Price(Sprite icon, string text) => new SoulLine(icon, text) { Cost = true };
    }

    // Names, colors, descriptions and formula chips shared by the HUD, popups and tooltips.
    // Numbers always come from the same SoulValue/UnitStat the combat uses (design 11.2: one definition).
    public static class SoulDescribe
    {
        public static string StatName(SoulStatRules rules, StatType stat)
            => HeroStatPipeline.IsUpper(stat) && rules != null ? rules.Info(stat).DisplayName : SoulStatRules.CombatName(stat);

        public static Color StatColor(SoulStatRules rules, StatType stat)
            => HeroStatPipeline.IsUpper(stat) && rules != null ? rules.Info(stat).Color : SoulUi.SubText;

        public static string Colored(SoulStatRules rules, StatType stat) => SoulUi.Colored(StatName(rules, stat), StatColor(rules, stat));

        public static bool IsRatio(StatType id)
        {
            switch (id)
            {
                case StatType.PhysicalResist: case StatType.MagicResist: case StatType.Accuracy: case StatType.Evasion:
                case StatType.CritChance: case StatType.CritDamage:
                case StatType.BleedResist: case StatType.PoisonResist: case StatType.BurnResist: case StatType.ChillResist:
                case StatType.FearResist: case StatType.StunResist: case StatType.PetrifyResist:
                case StatType.KnockbackResist: case StatType.PatternCostReduce: case StatType.ExecuteBonus: case StatType.FreePatternChance:
                case StatType.RepeatCostReduce: case StatType.TirelessWalk: case StatType.LowHpCostReduce:
                case StatType.SpellPower: case StatType.DownResist: case StatType.ManaShield: case StatType.SpellReflect:
                case StatType.DuelFocus: case StatType.CrowdFury: case StatType.FearAura: case StatType.GuardAura: case StatType.BookDiscount: case StatType.FireFocus: case StatType.BattleCaster: case StatType.PotionPower: case StatType.HolyBane: case StatType.KillMana: case StatType.LowHpDamage: case StatType.BuffDuration: case StatType.ManaRegenRate: case StatType.StaffBound:
                    return true;
                default:
                    return SoulItemRules.IsRatio(id);
            }
        }

        // Stats kept as a positive cut (0.05 = 5% less) but read as a minus: 패턴 비용 -5%, 마법 효과 -40%.
        public static bool IsCut(StatType id)
            => id == StatType.PatternCostReduce || id == StatType.RepeatCostReduce || id == StatType.LowHpCostReduce
            || id == StatType.StaffBound || id == StatType.CursedHp;

        // A timed effect that only takes away (a curse): shown as a debuff, in red.
        public static bool IsDebuff(SoulTimedBuff buff)
        {
            bool harm = false, help = false;
            foreach (var bonus in buff.Bonuses)
            {
                bool worse = IsPenalty(bonus.Stat) ? bonus.Value > 0 : bonus.Value < 0;
                if (worse) harm = true; else help = true;
            }
            return harm && !help;
        }

        public static Sprite BuffIcon(SoulTimedBuff buff) => IsDebuff(buff) ? SoulIconSet.Status(SoulStatus.Weaken) : SoulIconSet.Ui("buff");

        // More of it is worse (마법 효과 -40%, 저주, 무게 부하).
        public static bool IsPenalty(StatType id) => id == StatType.StaffBound || id == StatType.CursedHp || id == StatType.LoadRatio;

        public static string Value(StatType id, float value)
        {
            if (IsCut(id) && value > 0) return Signed(id, value);
            return IsRatio(id) ? $"{value * 100:0.#}%" : id == StatType.BodyWeight ? $"{value:0.#}kg" : $"{value:0.##}";
        }

        public static string Signed(StatType id, float value)
        {
            if (IsCut(id)) value = -value;
            return IsRatio(id) ? $"{value * 100:+0.#;-0.#}%" : $"{value:+0.##;-0.##}";
        }

        public static string Category(SoulPatternCategory category)
        {
            switch (category)
            {
                case SoulPatternCategory.Attack: return "공격";
                case SoulPatternCategory.Defense: return "방어";
                case SoulPatternCategory.Movement: return "이동";
                case SoulPatternCategory.Support: return "보조";
                case SoulPatternCategory.Chain: return "연계";
                default: return "조우";
            }
        }

        public static string Trigger(SoulTrigger trigger)
        {
            switch (trigger)
            {
                case SoulTrigger.Always: return "상시";
                case SoulTrigger.BattleStart: return "전투 시작";
                case SoulTrigger.Encounter: return "적 발견 시";
                case SoulTrigger.InRange: return "사거리 안";
                case SoulTrigger.LowHealth: return "HP 35% 이하";
                case SoulTrigger.OnHit: return "피격 시";
                case SoulTrigger.OnAttack: return "공격 시";
                case SoulTrigger.OnAttackLanded: return "명중 시";
                case SoulTrigger.OnKill: return "처치 시";
                case SoulTrigger.AllyHurt: return "아군 위기";
                case SoulTrigger.AllySupport: return "전투 중 아군 지원";
                case SoulTrigger.AllyFallen: return "아군 사망";
                default: return "마법 시전";
            }
        }

        public static string UpperDescription(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength: return "물리 공격력과 밀치기 강도, 무거운 무기의 효율을 올립니다.";
                case StatType.Vitality: return "최대 HP, 최대 스태미나, 스태미나 재생, 신체 상태이상 저항을 올립니다.";
                case StatType.Agility: return "행동 속도, 이동 속도, 명중과 회피를 올립니다. 둔화에 버팁니다.";
                case StatType.Magic: return "마법 피해와 최대 MP, 마법 효과량을 올립니다.";
                case StatType.Will: return "공포·혼란·스턴·석화에 버티고 그 등급을 낮춥니다. 지쳐 쓰러지는 다운에도 버팁니다. 높을수록 강한 적의 위압감에 겁먹지 않습니다. 동료의 죽음이나 많은 처치를 겪으면 전투 후 성장합니다.";
                case StatType.Luck: return "확률형 스킬과 드문 발동을 보정합니다.";
                case StatType.Regeneration: return "초당 HP를 재생합니다.";
                case StatType.Durability: return "방어력과 물리 내성, 위협도를 올립니다.";
                case StatType.AntiMagic: return "마법 내성을 올려 마법 피해를 줄입니다.";
                case StatType.Recovery: return "MP 회복 속도를 크게, 스태미나 회복 속도를 조금 올립니다.";
                case StatType.SlashPower: return "베기·검 계열 피해와 그 공격의 방어 관통을 올립니다.";
                case StatType.ImpactPower: return "휘두르기·둔기·충격파 피해와 경직(스턴) 확률을 올립니다.";
                case StatType.PiercePower: return "찌르기·활 계열 피해와 방어 관통, 출혈 피해를 올립니다.";
                default: return "";
            }
        }

        public static string LowerDescription(StatType stat)
        {
            switch (stat)
            {
                case StatType.DownResist: return "부상 2단계 이상에서 스태미나가 바닥났을 때 맞아도 넘어지지 않을 확률입니다. 넘어져도 그만큼 빨리 일어납니다. 정신력 1당 +1.5% (최대 75%).";
                case StatType.SpellPower: return "MP를 쓰는 스킬(공격 마법, 저주, 부여, 치유)의 효과에 곱해집니다. 마력 1당 +4%. 지팡이·완드·성구는 그대로, 맨손은 80%, 다른 무기는 50%.";
                case StatType.MaxHp: return "0이 되면 쓰러집니다.";
                case StatType.MaxStamina: return "모든 패턴(공격·방어·이동)이 소모합니다. 부족하면 회복을 기다립니다. HP가 넉넉한데 스태미나가 적으면 회피하지 않고 맞습니다.";
                case StatType.StaminaRegen: return $"초당 회복량입니다. 전투 중에는 {SoulDungeonSession.CombatStaminaRegen * 100:0}%, 휴식·야영 중에는 {SoulDungeonSession.RestStaminaRegen * 100:0}%만큼 회복됩니다.";
                case StatType.MpRegen: return "초당 MP 회복량입니다.";
                case StatType.HpRegen: return "초당 HP 회복량입니다.";
                case StatType.MaxMp: return "마법 시전과 일부 스킬이 소모합니다.";
                case StatType.Attack: return "물리 패턴의 기본 피해에 더해집니다.";
                case StatType.Armor: return "물리 피해에서 빼집니다. 관통이 높은 공격에는 덜 막힙니다.";
                case StatType.ActionSpeed: return "공격·이동 패턴의 행동 시간을 나눕니다 (0.5~2배).";
                case StatType.MoveSpeed: return "초당 이동 칸 수입니다.";
                case StatType.Accuracy: return "공격이 빗나가지 않을 확률입니다. 큰 몸집의 적일수록 맞히기 쉽습니다.";
                case StatType.Evasion: return "상대 명중에서 빠집니다.";
                case StatType.PhysicalResist: return "물리 피해를 비율로 줄입니다.";
                case StatType.MagicResist: return "마법 피해를 비율로 줄입니다.";
                case StatType.Threat: return "몬스터는 거리 ÷ 위협도가 가장 작은 용병을 노립니다. 위협·도발로 올릴 수 있습니다. 위협도가 높은 몬스터를 처음 보면 정신력이 모자란 용병은 공포에 빠집니다.";
                case StatType.BodyWeight: return "넉백 거리와 도약 충격파 발동 조건에 쓰입니다.";
                case StatType.KnockbackResist: return "밀려나는 거리를 줄입니다.";
                case StatType.PatternCostReduce: return "모든 패턴의 스태미나 비용을 줄입니다 (최대 30%).";
                case StatType.ExecuteBonus: return "HP 35% 미만 적에게 근접 피해가 늘어납니다.";
                case StatType.FreePatternChance: return "패턴을 쓸 때 이 확률로 스태미나를 쓰지 않습니다.";
                case StatType.RepeatCostReduce: return $"같은 공격 패턴을 연속으로 쓸 때마다 스태미나 비용이 이만큼 줄어듭니다 (최대 {SoulCombat.MaxRepeatStacks}회 누적).";
                case StatType.TirelessWalk: return "스태미나가 바닥났을 때 걷는 속도가 기본 50%에서 이만큼 더해집니다.";
                case StatType.LowHpCostReduce: return $"HP가 {SoulCombat.LowHealth * 100:0}% 아래일 때 스태미나 비용이 이만큼 줄어듭니다.";
                default: return "";
            }
        }

        public static string SourceName(SoulMercenary hero, string key)
        {
            if (key == "mercenary:base") return "기본";
            if (key == "mercenary:level") return "레벨업";
            if (key == "mercenary:mental") return "경험(정신력)";
            if (key == "timed:" + SoulCombat.StatusBuffId) return "상태이상";
            if (key == "mercenary:training") return "훈련";
            if (key == "buff:" + SoulCampaign.BlessingKey) return "성당의 축복";
            if (key.StartsWith("race:")) return "종족";
            if (key.StartsWith("equipment:") && int.TryParse(key.Substring(10), out int e) && e < hero.Equipment.Count) return hero.Equipment[e].Name;
            if (key == "load") return "무게 부하";
            if (key == "cursed") return "저주";
            if (key == "timed:" + SoulFatigue.BuffId) return "피로";
            if (key.StartsWith("soul:") && int.TryParse(key.Substring(5), out int s) && s < hero.Souls.Count) return hero.Souls[s].OriginMonster + " 영혼";
            if (key.StartsWith("passive:"))
            {
                string id = key.Substring(8);
                foreach (var passive in hero.Passives()) if (passive.SoulId == id) return passive.SkillName;
                return id;
            }
            if (key.StartsWith("timed:")) return "버프 " + key.Substring(6);
            if (key.StartsWith("buff:")) return "버프 " + key.Substring(5);
            return key;
        }

        static Sprite SourceIcon(string key)
        {
            if (key.StartsWith("soul:")) return SoulIconSet.Ui("soul");
            if (key.StartsWith("equipment:")) return SoulIconSet.Ui("equipment");
            if (key.StartsWith("race:")) return SoulIconSet.Ui("race");
            if (key == "mercenary:level") return SoulIconSet.Ui("level");
            if (key == "mercenary:mental") return SoulIconSet.Stat(StatType.Will);
            if (key.StartsWith("passive:") || key.StartsWith("timed:")) return SoulIconSet.Ui("passive");
            return SoulIconSet.Ui("base");
        }

        // Upper stat tooltip in three separate blocks so "where the number comes from" never mixes with
        // "what the number does": 구성 (sources that add up to the total) · 오르는 상세 스탯 · 쓰는 기술.
        public static List<SoulLine> UpperTooltip(SoulMercenary hero, StatType stat, SoulStatRules rules)
        {
            string name = StatName(rules, stat);
            float total = hero.Stats.Total(stat);
            var lines = new List<SoulLine> { new SoulLine(null, UpperDescription(stat)) };

            lines.Add(SoulLine.Title($"{name} 구성"));
            bool first = true;
            foreach (string key in hero.Stats.Upper.GetKeys())
            {
                float amount = hero.Stats.Upper.GetLayer(key, stat);
                if (amount == 0) continue;
                lines.Add(new SoulLine(SourceIcon(key), $"{SourceName(hero, key)}  {SoulUi.Colored(first ? $"{amount:0.#}" : $"{amount:+0.#;-0.#}", Color.white)}"));
                first = false;
            }
            lines.Add(new SoulLine(null, $"합계  {SoulUi.Colored($"{total:0.#}", SoulUi.Accent)}"));

            lines.Add(SoulLine.Title($"{name} {total:0.#}로 오르는 상세 스탯"));
            int effects = 0;
            foreach (var conversion in SoulStatRules.Effective(rules))
            {
                if (conversion.Formula == null) continue;
                foreach (var term in conversion.Formula.Terms)
                {
                    if (term.Stat != stat || term.Factor == 0) continue;
                    effects++;
                    lines.Add(new SoulLine(SoulIconSet.Stat(conversion.Output),
                        $"{SoulStatRules.CombatName(conversion.Output)} {SoulUi.Colored(Signed(conversion.Output, total * term.Factor), SoulUi.Good)}" +
                        Small($"  (1당 {Signed(conversion.Output, term.Factor)})")));
                }
            }
            if (effects == 0) lines.Add(new SoulLine(null, "직접 올리는 상세 스탯은 없습니다. 아래 기술의 계산식에 쓰입니다."));

            var users = SkillsUsing(hero, stat);
            if (users.Count > 0)
            {
                lines.Add(SoulLine.Title($"{name}을(를) 쓰는 기술"));
                lines.Add(new SoulLine(null, string.Join(", ", users)));
            }
            return lines;
        }

        // Patterns and skills whose formulas read this stat — the reason to raise it beyond the derived stats.
        static List<string> SkillsUsing(SoulMercenary hero, StatType stat)
        {
            var names = new List<string>();
            foreach (var pattern in hero.Patterns())
                if (Uses(pattern.Damage, stat) || Uses(pattern.Pierce, stat) || (pattern.Status != null && (Uses(pattern.Status.Chance, stat) || Uses(pattern.Status.Duration, stat))))
                    names.Add(pattern.Id);
            foreach (var skill in hero.ActiveSkills())
                if (Uses(skill.Damage, stat) || Uses(skill.Heal, stat) || Uses(skill.Radius, stat) || Uses(skill.Duration, stat) || System.Array.Exists(skill.SelfBuffs, buff => Uses(buff.Amount, stat)))
                    names.Add(skill.SkillName);
            foreach (var passive in hero.Passives())
                if (Uses(passive.Damage, stat) || Uses(passive.Heal, stat)) names.Add(passive.SkillName);
            return names;
        }

        static bool Uses(SoulValue value, StatType stat)
            => value != null && value.Terms != null && System.Array.Exists(value.Terms, term => term.Stat == stat && term.Factor != 0);

        // The number the combat actually uses (speeds are clamped and slowed by chill).
        // 마법 위력 shows as the whole multiplier with the weapon in hand (136% · with a bow 68%).
        public static float CombatValue(SoulCombatant unit, StatType stat)
            => stat == StatType.ActionSpeed ? SoulCombat.ActionSpeed(unit) : stat == StatType.MoveSpeed ? SoulCombat.MoveSpeed(unit)
                : stat == StatType.SpellPower ? SoulSkillUsePolicy.SpellPower(unit) : unit.Stats.Total(stat);

        // Detail stat tooltip: description, then the calculation line by line, then the result.
        public static List<SoulLine> CombatTooltip(SoulCombatant unit, StatType stat, SoulStatRules rules)
        {
            var lines = new List<SoulLine>();
            string description = LowerDescription(stat);
            if (!string.IsNullOrEmpty(description)) lines.Add(new SoulLine(null, description));
            lines.Add(SoulLine.Title("계산"));
            foreach (var conversion in SoulStatRules.Effective(rules))
            {
                if (conversion.Output != stat || conversion.Formula == null) continue;
                if (conversion.Formula.Flat != 0) lines.Add(new SoulLine(SoulIconSet.Ui("base"), $"기본  {Signed(stat, conversion.Formula.Flat)}"));
                foreach (var term in conversion.Formula.Terms)
                {
                    if (term.Factor == 0) continue;
                    float upper = unit.Stats.Total(term.Stat);
                    lines.Add(new SoulLine(SoulIconSet.Stat(term.Stat), $"{Colored(rules, term.Stat)} {upper:0.#} × {Value(stat, term.Factor)}  → {SoulUi.Colored(Signed(stat, upper * term.Factor), SoulUi.Good)}"));
                }
            }
            // bonuses that name this stat directly (race, equipment, souls, passives)
            foreach (string key in unit.Stats.Upper.GetKeys())
            {
                float amount = unit.Stats.Upper.GetLayer(key, stat);
                if (amount == 0) continue;
                string source = unit is SoulMercenary hero ? SourceName(hero, key) : key;
                lines.Add(new SoulLine(SourceIcon(key), $"{source}  {SoulUi.Colored(Signed(stat, amount), SoulUi.Good)}"));
            }
            if (stat == StatType.BodyWeight) lines.Add(new SoulLine(SoulIconSet.Ui("race"), "종족 몸무게 × 영혼의 몸무게 배율"));
            if (stat == StatType.SpellPower)
            {
                float focus = SoulSkillUsePolicy.Focus(unit);
                string hand = focus >= 1 ? "마법 도구" : focus >= SoulSkillUsePolicy.BareHandsFocus ? "맨손" : "다른 무기";
                lines.Add(new SoulLine(SoulIconSet.Ui("equipment"), $"손에 든 것: {hand}  ×{focus * 100:0}%" + (focus < 1 ? "  " + SoulUi.Colored("(지팡이·완드·성구가 아니면 마법이 약해집니다)", SoulUi.Bad) : "")));
            }
            foreach (var buff in unit.TimedBuffs)
                foreach (var bonus in buff.Bonuses)
                    if (bonus.Stat == stat)
                        lines.Add(buff.Id == SoulCombat.StatusBuffId
                            ? new SoulLine(SoulIconSet.Ui("buff"), $"상태이상  {SoulUi.Colored(Signed(stat, bonus.Value), SoulUi.Bad)}")
                            : IsDebuff(buff) ? new SoulLine(BuffIcon(buff), $"약화  {SoulUi.Colored(Signed(stat, bonus.Value), SoulUi.Bad)} ({buff.Remaining:0.0}초)")
                            : new SoulLine(SoulIconSet.Ui("buff"), $"버프  {SoulUi.Colored(Signed(stat, bonus.Value), SoulUi.Good)} ({buff.Remaining:0.0}초)"));
            if ((stat == StatType.ActionSpeed || stat == StatType.MoveSpeed) && unit.Has(SoulStatus.Chill))
                lines.Add(new SoulLine(SoulIconSet.Status(SoulStatus.Chill), $"냉기  ×{SoulCombat.ChillSlow:0.##}"));
            if (stat == StatType.ActionSpeed) lines.Add(new SoulLine(null, "0.5 ~ 2 사이로 제한됩니다."));
            lines.Add(new SoulLine(null, $"최종  {SoulUi.Colored(Value(stat, CombatValue(unit, stat)), SoulUi.Accent)}"));
            return lines;
        }

        // ── skills ───────────────────────────────────────────────

        public static string School(SoulDamageSchool school)
            => school == SoulDamageSchool.Physical ? "물리" : school == SoulDamageSchool.Magic ? "마법" : "고정";

        public static string Kind(SoulDamageKind kind)
        {
            switch (kind)
            {
                case SoulDamageKind.Slash: return "베기";
                case SoulDamageKind.Impact: return "충격";
                case SoulDamageKind.Pierce: return "찌르기";
                case SoulDamageKind.Wind: return "바람";
                case SoulDamageKind.Stone: return "대지";
                case SoulDamageKind.Fire: return "화염";
                case SoulDamageKind.Lightning: return "번개";
                case SoulDamageKind.Cold: return "냉기";
                case SoulDamageKind.Arcane: return "비전";
                case SoulDamageKind.Bleed: return "출혈";
                case SoulDamageKind.Poison: return "독";
                case SoulDamageKind.Burn: return "화상";
                default: return "동상";
            }
        }

        public static bool Has(SoulValue value) => value != null && (value.Flat != 0 || (value.Terms != null && value.Terms.Length > 0));

        // "15.2 (3 + 근력×160%)": the evaluated number first, the formula after it in small print.
        public static string Formula(SoulValue value, SoulStatRules rules, UnitStat stats, string suffix = "")
        {
            var parts = new List<string>();
            if (value.Flat != 0 || value.Terms.Length == 0) parts.Add($"{value.Flat:0.##}");
            foreach (var term in value.Terms)
                parts.Add(SoulUi.Colored($"{StatName(rules, term.Stat)}×{term.Factor * 100:0.#}%", StatColor(rules, term.Stat)));
            string result = SoulUi.Colored($"{value.Evaluate(stats):0.#}{suffix}", Color.white);
            return value.Terms.Length == 0 ? result : result + Small($"  ({string.Join(" + ", parts)})");
        }

        // A 0–1 chance and how it is made: "37%  (25% + 마력×1%)".
        public static string PercentFormula(SoulValue value, SoulStatRules rules, UnitStat stats)
        {
            var parts = new List<string>();
            if (value.Flat != 0 || value.Terms.Length == 0) parts.Add($"{value.Flat * 100:0.#}%");
            foreach (var term in value.Terms)
                parts.Add(SoulUi.Colored($"{StatName(rules, term.Stat)}×{term.Factor * 100:0.#}%", StatColor(rules, term.Stat)));
            string result = SoulUi.Colored(stats != null ? $"{Mathf.Clamp01(value.Evaluate(stats)) * 100:0.#}%" : "", Color.white);
            return value.Terms.Length == 0 ? result : result + Small($"  ({string.Join(" + ", parts)})");
        }

        public static string Small(string text) => $"<size=16><color=#{SoulUi.Hex(SoulUi.SubText)}>{text}</color></size>";

        static string SkillTriggerText(SoulCombatant unit, SoulActiveSkillData skill)
        {
            float range = SoulSkillUsePolicy.Range(unit, skill);
            if (skill.Shortcut) return $"후퇴·탈출 중 나가는 곳이 {SoulDungeonSession.ShortcutFrom:0}칸보다 멀면 사용합니다 (층마다 한 번).";
            if (skill.Cleanse && skill.Trigger == SoulTrigger.AllyHurt) return $"{range:0.#}칸 안에 상태 이상에 걸린 아군이 있으면 가장 많이 걸린 아군에게 사용합니다.";
            if (skill.Cleanse) return "상태 이상에 걸리면 사용합니다.";
            switch (skill.Trigger)
            {
                case SoulTrigger.Always: return "상시 적용됩니다.";
                case SoulTrigger.BattleStart: return "전투가 시작되면 사용합니다.";
                case SoulTrigger.Encounter: return "적을 처음 발견하면 사용합니다.";
                case SoulTrigger.InRange: return $"대상이 {range:0.#}칸 안에 있으면 사용합니다.";
                case SoulTrigger.LowHealth: return "HP가 35% 이하로 떨어지면 사용합니다.";
                case SoulTrigger.OnHit: return "공격을 받으면 사용합니다.";
                case SoulTrigger.OnAttack: return "공격할 때 사용합니다.";
                case SoulTrigger.OnAttackLanded: return "공격이 명중하면 사용합니다.";
                case SoulTrigger.OnKill: return "적을 처치하면 사용합니다.";
                case SoulTrigger.AllyHurt: return skill.HealsWounds
                    ? $"{range:0.#}칸 안에 치유량만큼 HP가 빠졌거나 부상을 입은 아군이 있으면 가장 다친 아군에게 사용합니다."
                    : $"{range:0.#}칸 안에 치유량만큼 HP가 빠진 아군이 있으면 가장 다친 아군에게 사용합니다.";
                case SoulTrigger.AllySupport: return "전투 중, 주변 아군 중 이 효과가 없는 사람이 있으면 사용합니다 (자신 포함, 주변 아군 모두에게).";
                case SoulTrigger.AllyFallen: return $"{range:0.#}칸 안에 쓰러진 아군이 있으면 되살립니다.";
                default:
                    return $"공격 마법: 사거리 {range:0.#}칸 안의 적에게 {SoulSkillUsePolicy.ActionTime(skill):0.#}초 동안 시전합니다. 쓸 수 있는 것 중 범위 안 적 모두에게 가장 큰 피해를 주는 마법을 고릅니다. 시전 뒤 MP가 {SoulDungeonSession.ManaReserve * 100:0}% 넘게 남을 때, 또는 적 2명 이상이 범위에 있거나 엘리트·보스이거나 마무리 일격일 때만 씁니다.";
            }
        }

        // 시전 시간: at once, or a warning (예고) that stands this long — a monster always warns.
        public static string CastTimeText(SoulCombatant unit, SoulActiveSkillData skill)
        {
            float action = SoulSkillUsePolicy.ActionTime(skill) / SoulCombat.ActionSpeed(unit);
            if (skill.Trigger == SoulTrigger.Cast) return $"시전 시간 {action:0.#}초 · 대상 지점에 예고 원";
            if (SoulDungeonSession.Warned(unit, skill)) return $"시전 시간 {SoulDungeonSession.SkillWindUp(unit, skill):0.#}초 · 예고 범위 표시 (그동안 피할 수 있음)";
            if (!SoulDungeonSession.Harmful(skill)) return "시전 시간: 즉시";
            float monster = Mathf.Max(SoulDungeonSession.MonsterWindUp, SoulSkillUsePolicy.ActionTime(skill));
            return skill.LeapToTarget ? $"시전 시간: 즉시 (도약) · 몬스터가 쓰면 {monster:0.#}초 예고"
                : $"시전 시간: 즉시 (예고 없음) · 몬스터가 쓰면 {monster:0.#}초 예고";
        }

        // Tooltip titles: [name Lv ········ price] (the price comes from the lines, SoulLine.Price).
        public static string SkillTitle(SoulCombatant unit, SoulActiveSkillData skill) => $"{skill.SkillName}  <size=18>Lv.{SoulSkillUsePolicy.Level(unit, skill)}</size>";
        public static string PatternTitle(SoulPatternData pattern) => $"{pattern.Id}  <size=18>{Category(pattern.Category)}</size>";

        // Every line a player needs to understand an active skill: its price (title), what it does, when.
        public static List<SoulLine> SkillLines(SoulCombatant unit, SoulActiveSkillData skill, SoulStatRules rules)
        {
            var stats = unit.Stats.Combat;
            var lines = new List<SoulLine>();
            // the price goes to the title's right end: stamina, MP, HP
            SoulSkillUsePolicy.Costs(unit, skill, out float stamina, out float mana);
            if (stamina > 0) lines.Add(SoulLine.Price(SoulIconSet.Stat(StatType.MaxStamina), $"{stamina:0.#}"));
            if (mana > 0) lines.Add(SoulLine.Price(SoulIconSet.Stat(StatType.MaxMp), $"{mana:0.#}"));
            if (skill.HpCost > 0) lines.Add(SoulLine.Price(SoulIconSet.Stat(StatType.MaxHp), $"{skill.HpCost * 100:0}%"));
            if (!string.IsNullOrEmpty(skill.Description)) lines.Add(new SoulLine(null, skill.Description));

            lines.Add(SoulLine.Title("효과"));
            if (skill.LeapToTarget)
            {
                bool heavy = unit.Stats.Weight >= skill.WeightThreshold;
                lines.Add(new SoulLine(SoulIconSet.Ui("leap"), "대상 바로 옆으로 도약합니다."));
                lines.Add(new SoulLine(SoulIconSet.Stat(StatType.BodyWeight),
                    $"몸무게 {skill.WeightThreshold:0}kg 이상이면 착지 충격파 — 현재 {unit.Stats.Weight:0.#}kg " + (heavy ? SoulUi.Colored("발동", SoulUi.Good) : SoulUi.Colored("부족", SoulUi.Bad))));
                if (Has(skill.Damage))
                    lines.Add(new SoulLine(SoulIconSet.Stat(StatType.Attack), $"반경 {Mathf.Max(.5f, skill.Radius.Evaluate(stats)):0.#}칸 안 모든 적에게 {School(skill.DamageSchool)}·{Kind(skill.DamageKind)} 피해 {Formula(skill.Damage, rules, stats)}"));
            }
            else if (skill.Line)
            {
                lines.Add(new SoulLine(SoulIconSet.Ui("range"), $"대상을 꿰뚫고 {SoulSkillUsePolicy.Range(unit, skill):0.#}칸까지 일직선 위 모든 적"));
                if (Has(skill.Damage)) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.Attack), $"{School(skill.DamageSchool)}·{Kind(skill.DamageKind)} 피해 {Formula(skill.Damage, rules, stats)}"));
            }
            else if (skill.Chain > 0)
            {
                lines.Add(new SoulLine(SoulIconSet.Ui("range"), $"대상에게 맞은 뒤 {SoulDungeonSession.ChainReach:0.#}칸 안 다른 적에게 {skill.Chain}번 튕깁니다 (튕길 때마다 {(1 - SoulDungeonSession.ChainFalloff) * 100:0}% 약해짐)"));
                if (Has(skill.Damage)) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.Attack), $"{School(skill.DamageSchool)}·{Kind(skill.DamageKind)} 피해 {Formula(skill.Damage, rules, stats)}"));
            }
            else if (skill.Field != SoulFieldKind.None)
            {
                float radius = Mathf.Max(.5f, skill.Radius.Evaluate(stats));
                string heal = skill.Heal != null && Has(skill.Heal) && !Has(skill.Damage) ? "가장 다친 아군을 회복" : "가장 가까운 적을 공격";
                string what = skill.Field == SoulFieldKind.Hazard && skill.Line ? $"앞으로 {SoulSkillUsePolicy.Range(unit, skill):0.#}칸 일직선으로 장판 — 1초마다 밟은 모든 적"
                    : skill.Field == SoulFieldKind.Spirit ? $"곁에 정령 소환 (최대 {SoulDungeonSession.SpiritCap}마리) — {skill.SpiritInterval:0.#}초마다 {radius:0.#}칸 안 {heal}"
                    : skill.Field == SoulFieldKind.Trap ? $"발밑에 반경 {radius:0.#}칸 덫 — 적이 밟으면 터져 범위 안 모든 적"
                    : skill.Field == SoulFieldKind.Sanctuary ? $"발밑에 반경 {radius:0.#}칸 성역 — 안에 있는 아군을 1초마다 회복"
                    : skill.Field == SoulFieldKind.Hazard ? $"{(skill.AtTarget ? "대상 지점" : "발밑")}에 반경 {radius:0.#}칸 장판 — 1초마다 안에 있는 모든 적"
                    : $"자신 주변 반경 {radius:0.#}칸 — 1초마다 범위 안 모든 적";
                lines.Add(new SoulLine(SoulIconSet.Ui("range"), what + $" ({Formula(skill.Duration, rules, stats, "초")} 유지)"));
                if (Has(skill.Damage)) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.Attack), $"{School(skill.DamageSchool)}·{Kind(skill.DamageKind)} 피해 {Formula(skill.Damage, rules, stats)}"));
            }
            else if (skill.Burst)
            {
                float radius = Mathf.Max(.5f, skill.Radius.Evaluate(stats));
                lines.Add(new SoulLine(SoulIconSet.Ui("range"), $"{(skill.AtTarget ? "대상 지점" : "자신 주변")} 반경 {radius:0.#}칸 안 모든 적" + (Has(skill.Damage) ? "" : "에게 걸림")));
                if (Has(skill.Damage))
                    lines.Add(new SoulLine(SoulIconSet.Stat(StatType.Attack), $"{School(skill.DamageSchool)}·{Kind(skill.DamageKind)} 피해 {Formula(skill.Damage, rules, stats)}"));
            }
            else if (Has(skill.Damage) && skill.Trigger == SoulTrigger.Cast)
            {
                lines.Add(new SoulLine(SoulIconSet.Ui("range"), $"대상 지점 반경 {SoulSkillUsePolicy.SpellRadius(unit, skill):0.#}칸 원 · 예고 뒤 범위 안 모든 적"));
                lines.Add(new SoulLine(SoulIconSet.Stat(StatType.Attack), $"{School(skill.DamageSchool)}·{Kind(skill.DamageKind)} 피해 {Formula(skill.Damage, rules, stats)}"));
            }
            else if (Has(skill.Damage))
                lines.Add(new SoulLine(SoulIconSet.Stat(StatType.Attack), $"대상에게 {School(skill.DamageSchool)}·{Kind(skill.DamageKind)} 피해 {Formula(skill.Damage, rules, stats)}"));
            else if (skill.Trigger == SoulTrigger.Cast && skill.Status != null && skill.Status.Kind != SoulStatus.None)
                lines.Add(new SoulLine(SoulIconSet.Ui("range"), $"대상 지점 반경 {SoulSkillUsePolicy.SpellRadius(unit, skill):0.#}칸 원 · 예고 뒤 범위 안 모든 적에게 걸림"));
            if (skill.Knockback > 0) lines.Add(new SoulLine(SoulIconSet.Ui("knockback"), $"넉백 {skill.Knockback:0.#}칸 (대상 몸무게·넉백 저항 적용)"));
            if (skill.Trigger == SoulTrigger.AllySupport && skill.Imbue != null && skill.Imbue.Kind != SoulStatus.None)
            {
                lines.Add(new SoulLine(SoulIconSet.Ui("buff"), $"주변 아군의 공격에 {Formula(skill.Duration, rules, stats, "초")} 동안 속성 부여"));
                lines.Add(StatusLine(skill.Imbue, stats));
            }
            if (skill.ReviveRatio > 0) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.MaxHp), $"{(skill.Mass ? "쓰러진 아군 모두를" : "쓰러진 아군을")} 최대 HP {skill.ReviveRatio * 100:0}%로 되살립니다"));
            if (skill.OncePerFloor) lines.Add(new SoulLine(SoulIconSet.Ui("cooldown"), "한 층에 한 번만 쓸 수 있습니다"));
            if (skill.DrainMana) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.MaxMp), "남은 MP를 모두 쏟아붓습니다 — 더 쓴 MP만큼 피해가 커집니다"));
            if (skill.GuardShare > 0) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.Armor), $"효과가 있는 동안 주변 아군이 받는 피해의 {skill.GuardShare * 100:0}%를 대신 받습니다"));
            if (skill.Cleanse) lines.Add(new SoulLine(SoulIconSet.Status(SoulStatus.Poison), "대상의 상태 이상을 모두 없앱니다"));
            if (skill.Shortcut) lines.Add(new SoulLine(SoulIconSet.Ui("map"), "살아 있는 파티 전원이 가장 가까운 나가는 곳 옆으로 이동합니다"));
            if (skill.Extra != null && skill.Extra.Kind != SoulStatus.None) lines.Add(StatusLine(skill.Extra, stats));
            if (skill.Pull > 0) lines.Add(new SoulLine(SoulIconSet.Ui("knockback"), $"맞은 적을 {skill.Pull:0.#}칸 끌어당깁니다"));
            if (skill.Drain > 0) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.LifeSteal), $"준 피해의 {skill.Drain * 100:0}%만큼 HP 회복"));
            if (skill.Sap > 0) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.MaxMp), $"맞은 적의 MP·스태미나 {skill.Sap * 100:0}%를 빼앗습니다"));
            if (skill.Steal > 0) lines.Add(new SoulLine(SoulIconSet.Ui("gold"), $"맞힐 때마다 금화 +{skill.Steal}"));
            if (skill.Dispel) lines.Add(new SoulLine(SoulIconSet.Ui("buff"), "맞은 적의 강화 효과를 모두 지웁니다"));
            if (skill.Behind) lines.Add(new SoulLine(SoulIconSet.Ui("leap"), "대상의 등 뒤로 뛰어듭니다"));
            if (skill.Retreat > 0) lines.Add(new SoulLine(SoulIconSet.Ui("leap"), $"쓰고 나서 {skill.Retreat:0.#}칸 물러납니다"));
            if (skill.Cleanse && skill.Trigger != SoulTrigger.AllyHurt) lines.Add(new SoulLine(SoulIconSet.Status(SoulStatus.Poison), "자신의 상태 이상을 모두 없앱니다"));
            if (skill.Status != null && skill.Status.Kind != SoulStatus.None) lines.Add(StatusLine(skill.Status, stats));
            if (Has(skill.Heal))
            {
                string who = skill.Field == SoulFieldKind.Sanctuary ? "성역 안 아군 (1초마다)" : skill.Trigger == SoulTrigger.AllyHurt ? "대상 아군" : skill.Trigger == SoulTrigger.AllySupport ? "주변 아군" : skill.Mass ? "주변의 살아 있는 아군" : "자신";
                lines.Add(new SoulLine(SoulIconSet.Stat(StatType.MaxHp), $"{who}의 HP 회복 {Formula(skill.Heal, rules, stats)}"));
            }
            if (Has(skill.Barrier))
                lines.Add(new SoulLine(SoulIconSet.Stat(StatType.Armor), $"대상 아군에게 피해 흡수 보호막 {Formula(skill.Barrier, rules, stats)} ({Formula(skill.Duration, rules, stats, "초")} 유지)"));
            if (skill.HealsWounds)
                lines.Add(new SoulLine(SoulIconSet.Status(SoulStatus.Bleed), !skill.WoundRoll ? "부상 1단계 치료" : $"부상 1단계 치료 확률 {PercentFormula(skill.WoundChance, rules, stats)}"));
            if (skill.SelfBuffs.Length > 0)
            {
                foreach (var buff in skill.SelfBuffs)
                    lines.Add(new SoulLine(SoulIconSet.Stat(buff.Stat), $"{(skill.Trigger == SoulTrigger.AllySupport ? "주변 아군" : skill.Trigger == SoulTrigger.AllyHurt ? "대상 아군" : "자신")}의 {SoulStatRules.CombatName(buff.Stat)} {SoulUi.Colored(Signed(buff.Stat, buff.Amount.Evaluate(stats)), SoulUi.Good)}"));
                lines.Add(new SoulLine(SoulIconSet.Ui("buff"), "지속 " + (Has(skill.Duration) ? Formula(skill.Duration, rules, stats, "초") : "0초")));
            }

            lines.Add(SoulLine.Title("발동"));
            lines.Add(new SoulLine(SoulIconSet.Ui("trigger"), SkillTriggerText(unit, skill)));
            if (!string.IsNullOrEmpty(skill.RequiredJob))
            {
                string blocked = SoulSkillUsePolicy.Blocked(unit, skill);
                lines.Add(new SoulLine(SoulIconSet.Ui("lock"), $"{skill.RequiredJob} 전용  " + (blocked == null ? SoulUi.Colored("사용 가능", SoulUi.Good) : SoulUi.Colored(blocked, SoulUi.Bad))));
            }
            if (skill.RequiredPattern != null && skill.Trigger != SoulTrigger.Cast)
            {
                bool owned = unit.Patterns().Contains(skill.RequiredPattern);
                bool locked = unit is SoulMercenary owner && owner.IsLocked(skill.RequiredPattern);
                lines.Add(new SoulLine(PatternIcon(skill.RequiredPattern),
                    $"'{skill.RequiredPattern.Id}' 패턴이 있어야 합니다  " + (owned ? SoulUi.Colored("보유", SoulUi.Good)
                        : locked ? SoulUi.Colored("잠김 — 사용 불가", SoulUi.Bad) : SoulUi.Colored("미보유 — 사용 불가", SoulUi.Bad))));
            }
            if (skill.Cooldown > 0) lines.Add(new SoulLine(SoulIconSet.Ui("cooldown"), $"재사용 대기 {skill.Cooldown:0.#}초"));
            lines.Add(new SoulLine(SoulIconSet.Ui("cooldown"), CastTimeText(unit, skill)));
            if (skill.Technique) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.MaxMp), "기술: MP를 쓰지만 마법이 아닙니다 (주문력·무기 영향 없음)"));
            if (skill.ExecuteBelow > 0) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.ExecuteBonus), $"HP {skill.ExecuteBelow * 100:0}% 이하인 적에게만 씁니다"));
            return lines;
        }

        public static SoulLine StatusLine(SoulStatusApply status, UnitStat stats)
        {
            int grade = status.GradeFor(stats);
            string text = $"{SoulCombat.StatusName(status.Kind)} {grade}등급 · 확률 {status.Chance.Evaluate(stats) * 100:0}% · {status.Duration.Evaluate(stats):0.#}초";
            if (Has(status.DamagePerSecond)) text += $" · 초당 피해 {status.DamagePerSecond.Evaluate(stats):0.#}";
            return new SoulLine(SoulIconSet.Status(status.Kind), text + Small($"  {StatusEffect(status.Kind, grade)} · 대상의 {ResistStat(status.Kind)}이 높으면 확률·시간·등급이 줄어듦"));
        }

        public static Sprite PatternIcon(SoulPatternData pattern) => pattern.Icon != null ? pattern.Icon : SoulIconSet.Category(pattern.Category);

        // The upper stat that fights a status (through its resist).
        public static string ResistStat(SoulStatus kind)
        {
            switch (kind)
            {
                case SoulStatus.Fear: case SoulStatus.Confuse: case SoulStatus.Stun: case SoulStatus.Petrify: case SoulStatus.Silence: return "정신력";
                case SoulStatus.Slow: return "민첩";
                default: return "체력";
            }
        }

        // What a status does at a grade, in words (numbers from SoulCombat, the same ones the fight uses).
        public static string StatusEffect(SoulStatus kind, int grade)
        {
            var parts = new List<string>();
            if (kind == SoulStatus.Fear) parts.Add($"모든 능력치 -{grade * 10}%");
            else if (kind != SoulStatus.Poison)
                foreach (var (stat, share) in SoulCombat.StatusShares(kind, grade, 0)) parts.Add($"{SoulStatRules.CombatName(stat)} {share * 100:0}%");
            switch (kind)
            {
                case SoulStatus.Fear:
                    if (grade >= 2) parts.Add("스킬 사용 불가");
                    break;
                case SoulStatus.Confuse: parts.Add($"행동의 {SoulCombat.StumbleChance(grade) * 100:0}%가 헛손질"); break;
                case SoulStatus.Silence: parts.Add("스킬 사용 불가"); break;
                case SoulStatus.Poison:
                    parts.Add($"초당 최대 HP의 {SoulCombat.PoisonShare(grade) * 100:0.#}%");
                    parts.Add($"{SoulCombat.PoisonWeakenAfter:0}초 넘으면 공격력·방어력 -{(5 + 5 * grade)}%");
                    parts.Add($"{SoulCombat.PoisonWitherAfter:0}초 넘으면 HP 재생 정지·스태미나 재생 -50%");
                    break;
                case SoulStatus.Bleed: parts.Add($"지속 피해 ×{SoulCombat.DotScale(grade):0.#} · 최대 {SoulCombat.MaxBleedStacks}중첩"); break;
                case SoulStatus.Burn: parts.Add($"지속 피해 ×{SoulCombat.DotScale(grade):0.#}"); break;
                case SoulStatus.Chill: parts.Add($"행동·이동 속도 ×{SoulCombat.ChillSlow:0.#}"); break;
                case SoulStatus.Stun: case SoulStatus.Freeze: case SoulStatus.Petrify: parts.Add("행동 불가"); break;
            }
            return string.Join(" · ", parts);
        }

        // Tooltip for a status the unit is under right now.
        public static List<SoulLine> StatusLines(SoulStatusState status)
        {
            var lines = new List<SoulLine> { new SoulLine(SoulIconSet.Status(status.Kind), StatusEffect(status.Kind, status.Grade)) };
            if (status.Kind == SoulStatus.Poison) lines.Add(new SoulLine(SoulIconSet.Ui("cooldown"), $"중독된 지 {status.Elapsed:0.#}초"));
            if (status.Stacks > 1) lines.Add(new SoulLine(null, $"{status.Stacks}중첩"));
            lines.Add(new SoulLine(null, $"{ResistStat(status.Kind)}이 높을수록 확률·시간·등급이 줄어듭니다."));
            return lines;
        }

        // One line for a skill card: what it mainly does, at a glance.
        public static string SkillSummary(SoulCombatant unit, SoulActiveSkillData skill)
        {
            var stats = unit.Stats.Combat;
            var parts = new List<string>();
            if (skill.LeapToTarget) parts.Add("도약");
            if (skill.Technique) parts.Add("기술");
            if (skill.Line) parts.Add("관통");
            if (skill.Chain > 0) parts.Add($"연쇄 {skill.Chain}");
            if (skill.Field == SoulFieldKind.Trap) parts.Add("덫");
            if (skill.Field == SoulFieldKind.Sanctuary) parts.Add("성역");
            if (skill.Field == SoulFieldKind.Aura) parts.Add("오라");
            if (skill.Field == SoulFieldKind.Spirit) parts.Add("소환");
            if (skill.Cleanse) parts.Add("정화");
            if (skill.Shortcut) parts.Add("지름길");
            if (Has(skill.Damage)) parts.Add($"{Kind(skill.DamageKind)} 피해 {skill.Damage.Evaluate(stats):0.#}" + (skill.LeapToTarget || skill.Burst || skill.Trigger == SoulTrigger.Cast ? " (범위)" : ""));
            if (skill.Status != null && skill.Status.Kind != SoulStatus.None) parts.Add($"{SoulCombat.StatusName(skill.Status.Kind)} {skill.Status.Chance.Evaluate(stats) * 100:0}%");
            if (skill.Knockback > 0) parts.Add("넉백");
            if (Has(skill.Heal)) parts.Add($"HP 회복 {skill.Heal.Evaluate(stats):0.#}");
            foreach (var buff in skill.SelfBuffs) parts.Add($"{SoulStatRules.CombatName(buff.Stat)} {Signed(buff.Stat, buff.Amount.Evaluate(stats))}");
            if (skill.SelfBuffs.Length > 0 && Has(skill.Duration)) parts.Add($"{skill.Duration.Evaluate(stats):0.#}초");
            return string.Join(" · ", parts);
        }

        static string PassiveTriggerText(SoulPassiveSkillData passive)
        {
            string every = passive.Every > 1 ? $" 같은 대상에게 {passive.Every}번째마다 발동합니다." : "";
            switch (passive.SoulEvent)
            {
                case SoulTrigger.Always:
                    if (System.Array.Exists(passive.AlwaysBonuses, b => b.Stat == StatType.StaffBound)) return "지팡이 외 무기 장착 시";
                    return "항상 적용됩니다.";
                case SoulTrigger.OnAttackLanded: return "공격이 명중하면 발동합니다." + every;
                case SoulTrigger.OnAttack: return "공격하면 발동합니다." + every;
                case SoulTrigger.OnHit: return "공격을 받으면 발동합니다." + every;
                case SoulTrigger.OnKill: return "적을 처치하면 발동합니다." + every;
                default: return Trigger(passive.SoulEvent) + " 발동합니다." + every;
            }
        }

        static string MapTraitText(SoulMapTrait traits)
        {
            var parts = new List<string>();
            if ((traits & SoulMapTrait.SeeThroughWalls) != 0) parts.Add("벽 너머 시야 (탐색만)");
            if ((traits & SoulMapTrait.RevealExit) != 0) parts.Add("출구 위치 표시");
            if ((traits & SoulMapTrait.RevealGuardian) != 0) parts.Add("수호자 위치 표시");
            if ((traits & SoulMapTrait.DetectHiddenDoors) != 0) parts.Add("숨은 문 발견");
            if ((traits & SoulMapTrait.WideVision) != 0) parts.Add("넓은 시야");
            if ((traits & SoulMapTrait.RouteSense) != 0) parts.Add("방 구조 파악 · 탐색 목표 선택");
            if ((traits & SoulMapTrait.RevealTreasure) != 0) parts.Add("보물 상자 위치 표시");
            return string.Join(", ", parts);
        }

        public static List<SoulLine> PassiveLines(SoulMercenary hero, SoulPassiveSkillData passive, SoulStatRules rules)
        {
            var stats = hero.Stats.Combat;
            var lines = new List<SoulLine>();
            if (!string.IsNullOrEmpty(passive.Description)) lines.Add(new SoulLine(null, passive.Description));
            if (System.Array.IndexOf(hero.Race.InnatePassives, passive) >= 0) lines.Add(new SoulLine(SoulIconSet.Ui("race"), "종족 고유 능력"));
            lines.Add(SoulLine.Title("효과"));
            foreach (var bonus in passive.AlwaysBonuses)
                lines.Add(new SoulLine(SoulIconSet.Stat(bonus.Stat), $"{StatName(rules, bonus.Stat)} {SoulUi.Colored(Signed(bonus.Stat, bonus.Value), IsPenalty(bonus.Stat) == bonus.Value > 0 ? SoulUi.Bad : SoulUi.Good)}"));
            if (Has(passive.Damage)) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.Attack), $"대상에게 추가 피해 {Formula(passive.Damage, rules, stats)}" + Small("  (방어·내성 무시)")));
            if (Has(passive.Heal)) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.MaxHp), $"자신의 HP 회복 {Formula(passive.Heal, rules, stats)}"));
            if (passive.Stamina > 0) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.MaxStamina), $"스태미나 {passive.Stamina:0.#} 회복"));
            if (passive.MapTraits != SoulMapTrait.None) lines.Add(new SoulLine(SoulIconSet.Ui("map"), "탐색: " + MapTraitText(passive.MapTraits)));
            lines.Add(SoulLine.Title("발동"));
            lines.Add(new SoulLine(SoulIconSet.Ui("trigger"), PassiveTriggerText(passive)));
            return lines;
        }

        public static string PassiveSummary(SoulMercenary hero, SoulPassiveSkillData passive, SoulStatRules rules)
        {
            var stats = hero.Stats.Combat;
            var parts = new List<string>();
            if (passive.SoulEvent != SoulTrigger.Always) parts.Add(Trigger(passive.SoulEvent) + (passive.Every > 1 ? $" {passive.Every}회마다" : ""));
            foreach (var bonus in passive.AlwaysBonuses) parts.Add($"{StatName(rules, bonus.Stat)} {Signed(bonus.Stat, bonus.Value)}");
            if (Has(passive.Damage)) parts.Add($"추가 피해 {passive.Damage.Evaluate(stats):0.#}");
            if (Has(passive.Heal)) parts.Add($"HP 회복 {passive.Heal.Evaluate(stats):0.#}");
            if (passive.Stamina > 0) parts.Add($"스태미나 +{passive.Stamina:0.#}");
            if (passive.MapTraits != SoulMapTrait.None) parts.Add(MapTraitText(passive.MapTraits));
            return string.Join(" · ", parts);
        }

        public static Sprite ItemIcon(SoulItem item) => item != null && item.Data.Icon != null ? item.Data.Icon : SoulIconSet.Ui("equipment");

        // An item's name in its grade colour (special options make it purple-tinted by their prefix).
        public static string ItemName(SoulItem item)
        {
            string name = item.Data.Unique ? item.Data.Name : item.GradeName + " " + item.Data.Name;
            string prefix = "";
            foreach (string id in item.Specials) { var special = SoulItemRules.Special(id); if (special != null) prefix += special.Prefix + " "; }
            return (prefix.Length > 0 ? SoulUi.Colored(prefix, SoulItemRules.SpecialColor) : "") + SoulUi.Colored(name, item.GradeColor);
        }

        // One line for lists: slot · kind · weight · the base options · enchant slots · special options.
        public static string ItemSummary(SoulItem item, SoulStatRules rules)
        {
            var parts = new List<string> { SoulItemRules.SlotName(item.Slot) + (string.IsNullOrEmpty(item.Data.Kind) ? "" : " " + item.Data.Kind) + (item.TwoHanded ? "(양손)" : ""), $"{item.Weight:0.#}kg" };
            var material = SoulItemRules.Material(item.Data.Material);
            float scale = item.Data.Unique ? 1 : SoulItemRules.GradeScale(item.Grade) * material.Base;
            foreach (string id in item.Specials) { var special = SoulItemRules.Special(id); if (special != null) scale *= special.BaseScale; }
            foreach (var bonus in item.Data.Bonuses) parts.Add($"{StatName(rules, bonus.Stat)} {Signed(bonus.Stat, bonus.Value * scale)}");
            if (item.EnchantSlots > 0) parts.Add($"부여 {item.Enchants.Count}/{item.EnchantSlots}");
            foreach (string id in item.Specials) { var special = SoulItemRules.Special(id); if (special != null) parts.Add(SoulUi.Colored("특수 " + special.Prefix, SoulItemRules.SpecialColor)); }
            return string.Join(" · ", parts);
        }

        public static List<SoulLine> EquipmentLines(SoulItem item, SoulStatRules rules, SoulMercenary wearer = null)
        {
            var lines = new List<SoulLine>();
            var material = SoulItemRules.Material(item.Data.Material);
            lines.Add(new SoulLine(SoulIconSet.Ui("equipment"), $"{SoulUi.Colored(item.GradeName, item.GradeColor)} · {SoulItemRules.SlotName(item.Slot)} {item.Data.Kind}{(item.TwoHanded ? " (양손)" : "")}"
                + (material.Material != SoulMaterial.None ? $" · {material.Name}" : "") + $" · {item.Weight:0.#}kg"));
            if (!string.IsNullOrEmpty(item.WeaponTag)) lines.Add(new SoulLine(null, Small($"무기 종류: {item.WeaponTag} (이 종류를 쓰는 패턴이 사용 가능해집니다)")));
            float scale = item.Data.Unique ? 1 : SoulItemRules.GradeScale(item.Grade) * material.Base;
            foreach (string id in item.Specials) { var special = SoulItemRules.Special(id); if (special != null) scale *= special.BaseScale; }
            if (item.Data.Bonuses.Length > 0)
            {
                lines.Add(SoulLine.Title(item.Data.Unique ? "고유 옵션" : $"기본 옵션 (등급 ×{SoulItemRules.GradeScale(item.Grade):0.##}{(material.Base != 1 ? $", {material.Name} ×{material.Base:0.##}" : "")})"));
                foreach (var bonus in item.Data.Bonuses)
                    lines.Add(new SoulLine(SoulIconSet.Stat(bonus.Stat), $"{StatName(rules, bonus.Stat)} {SoulUi.Colored(Signed(bonus.Stat, bonus.Value * scale), SoulUi.Good)}"));
            }
            if (material.Traits.Length > 0)
                foreach (var bonus in material.Traits)
                    lines.Add(new SoulLine(SoulIconSet.Stat(bonus.Stat), $"{material.Name}: {StatName(rules, bonus.Stat)} {SoulUi.Colored(Signed(bonus.Stat, bonus.Value), SoulUi.Good)}"));
            if (item.EnchantSlots > 0)
            {
                lines.Add(SoulLine.Title($"마법 부여 {item.Enchants.Count}/{item.EnchantSlots}"));
                foreach (var enchant in item.Enchants)
                    lines.Add(new SoulLine(SoulIconSet.Ui("skill"), $"{SoulItemRules.EnchantText(enchant)}  {Small(SoulItemRules.TierNames[enchant.Tier] + (enchant.Locked ? " · 고정" : ""))}"));
                for (int i = item.Enchants.Count; i < item.EnchantSlots; i++) lines.Add(new SoulLine(null, Small("빈 칸 — 대장간에서 부여할 수 있습니다")));
            }
            if (item.Specials.Count > 0)
            {
                lines.Add(SoulLine.Title("특수 옵션 (던전 드롭)"));
                foreach (string id in item.Specials)
                {
                    var special = SoulItemRules.Special(id);
                    if (special != null) lines.Add(new SoulLine(SoulIconSet.Ui("passive"), $"{SoulUi.Colored(special.Prefix, SoulItemRules.SpecialColor)} — {special.Description}"));
                }
            }
            if (wearer != null)
            {
                float load = wearer.Stats.Total(StatType.LoadRatio);
                lines.Add(new SoulLine(SoulIconSet.Stat(StatType.BodyWeight), Small($"착용 무게 {wearer.GearWeight:0.#}/{SoulItemRules.CarryLimit(wearer.Stats.Total(StatType.Strength)):0}kg ({load * 100:0}% {SoulItemRules.LoadName(load)})")));
            }
            lines.Add(new SoulLine(null, Small($"가격 {item.Price} · 판매 {item.SellPrice}")));
            if (item.BasicAttack != null || item.Patterns.Length > 0 || item.ActiveSkills.Length > 0 || item.Passives.Length > 0)
            {
                lines.Add(SoulLine.Title("부여"));
                if (item.BasicAttack != null) lines.Add(new SoulLine(PatternIcon(item.BasicAttack), $"기본 공격 '{item.BasicAttack.Id}' — 공격 패턴이 하나도 없을 때 약하게 사용"));
                foreach (var pattern in item.Patterns) if (pattern != null) lines.Add(new SoulLine(PatternIcon(pattern), $"패턴 {pattern.Id}"));
                foreach (var skill in item.ActiveSkills) if (skill != null) lines.Add(new SoulLine(skill.Icon != null ? skill.Icon : SoulIconSet.Ui("skill"), $"액티브 {skill.SkillName}"));
                foreach (var passive in item.Passives) if (passive != null) lines.Add(new SoulLine(passive.Icon != null ? passive.Icon : SoulIconSet.Ui("passive"), $"패시브 {passive.SkillName}"));
            }
            return lines;
        }
    }
}
