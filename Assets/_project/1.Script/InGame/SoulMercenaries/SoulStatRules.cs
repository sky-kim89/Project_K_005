using System;
using System.Collections.Generic;
using UnityEngine;
using SoulMercenaries;

namespace SoulMercenaries
{
    [Serializable]
    public struct SoulStatConversion
    {
        public StatType Output;
        public SoulValue Formula;
    }

    [Serializable]
    public struct SoulStatInfo
    {
        public StatType Id;
        public string DisplayName, Description;
        public Sprite Icon;
        public Color Color;
    }

    [CreateAssetMenu(menuName = "Soul Mercenaries/Stat Rules")]
    public sealed class SoulStatRules : ScriptableObject
    {
        public SoulStatConversion[] Conversions = Array.Empty<SoulStatConversion>();
        public SoulStatInfo[] UpperStatInfo = Array.Empty<SoulStatInfo>();
        [Min(1)] public int StatPointsPerLevel = 3;

        public static int PointsPerLevel(SoulStatRules rules) => rules != null ? Mathf.Max(1, rules.StatPointsPerLevel) : 3;

        public static SoulStatConversion[] Effective(SoulStatRules rules)
            => rules != null && rules.Conversions != null && rules.Conversions.Length > 0 ? rules.Conversions : Defaults;

        public static string CombatName(StatType id)
        {
            switch (id)
            {
                case StatType.MaxHp: return "최대 HP";
                case StatType.MaxStamina: return "최대 스태미나";
                case StatType.MaxMp: return "최대 MP";
                case StatType.Attack: return "공격력";
                case StatType.Armor: return "방어력";
                case StatType.PhysicalResist: return "물리 내성";
                case StatType.MagicResist: return "마법 내성";
                case StatType.SpellPower: return "마법 위력";
                case StatType.HpRegen: return "HP 재생";
                case StatType.MpRegen: return "MP 재생";
                case StatType.StaminaRegen: return "스태미나 재생";
                case StatType.ActionSpeed: return "행동 속도";
                case StatType.MoveSpeed: return "이동 속도";
                case StatType.Accuracy: return "명중";
                case StatType.CritChance: return "치명타 확률";
                case StatType.CritDamage: return "치명타 피해";
                case StatType.Evasion: return "회피";
                case StatType.BleedResist: return "출혈 내성";
                case StatType.PoisonResist: return "중독 내성";
                case StatType.BurnResist: return "화상 내성";
                case StatType.ChillResist: return "냉기 내성";
                case StatType.FearResist: return "공포 내성";
                case StatType.StunResist: return "스턴 내성";
                case StatType.DownResist: return "다운 저항";
                case StatType.ManaShield: return "마나 보호막";
                case StatType.SpellReflect: return "마법 반사";
                case StatType.DuelFocus: return "결투 집중";
                case StatType.CrowdFury: return "난전의 흥분";
                case StatType.DarkSight: return "어둠 눈";
                case StatType.FearAura: return "공포 오라";
                case StatType.RegenAura: return "재생 오라";
                case StatType.GuardAura: return "수호 오라";
                case StatType.DefyRage: return "불사의 분노";
                case StatType.BookDiscount: return "스킬북 보존";
                case StatType.FireFocus: return "화염 집중";
                case StatType.ChainBonus: return "연쇄 추가";
                case StatType.BattleCaster: return "전투 시전";
                case StatType.PotionPower: return "포션 효과";
                case StatType.HolyBane: return "퇴마";
                case StatType.StatusGrade: return "상태 이상 강화";
                case StatType.KillMana: return "처치 시 MP";
                case StatType.LowHpDamage: return "위기의 일격";
                case StatType.WoundMend: return "추가 부상 치료";
                case StatType.BuffDuration: return "강화 지속";
                case StatType.ManaRegenRate: return "MP 회복 속도";
                case StatType.StaffBound: return "마법 효과";
                case StatType.PetrifyResist: return "석화 내성";
                case StatType.KnockbackResist: return "넉백 저항";
                case StatType.PatternCostReduce: return "패턴 비용";
                case StatType.Threat: return "위협도";
                case StatType.BodyWeight: return "몸무게";
                case StatType.ExecuteBonus: return "저체력 적 추가 피해";
                case StatType.FreePatternChance: return "무소모 확률";
                case StatType.RepeatCostReduce: return "연속 사용 비용";
                case StatType.TirelessWalk: return "지친 걸음 속도";
                case StatType.LowHpCostReduce: return "위기 시 비용";
                case StatType.FireAttack: return "화염 공격";
                case StatType.ColdAttack: return "냉기 공격";
                case StatType.LightningAttack: return "번개 공격";
                case StatType.EarthAttack: return "대지 공격";
                case StatType.WindAttack: return "바람 공격";
                case StatType.ToxicAttack: return "독 공격";
                case StatType.FireResist: return "화염 저항";
                case StatType.ColdResist: return "냉기 저항";
                case StatType.LightningResist: return "번개 저항";
                case StatType.EarthResist: return "대지 저항";
                case StatType.WindResist: return "바람 저항";
                case StatType.ToxicResist: return "독 저항";
                case StatType.LoadRatio: return "무게 부하";
                case StatType.LifeSteal: return "흡혈";
                case StatType.KillStamina: return "처치 시 스태미나";
                case StatType.EliteDamage: return "엘리트·보스 피해";
                case StatType.DoubleHit: return "연격 확률";
                case StatType.ArmorShred: return "방어 무시";
                case StatType.FreeSkillChance: return "스킬 무소모 확률";
                case StatType.Thorns: return "가시 반사";
                case StatType.DeathDefy: return "거부 (층마다 1회)";
                case StatType.WoundResist: return "부상 저항";
                case StatType.GoldFind: return "금화 획득";
                case StatType.LootFind: return "장비 드롭";
                case StatType.SoulFind: return "영혼 드롭";
                case StatType.CursedHp: return "저주 (최대 HP)";
                default: return id.ToString();
            }
        }

        public static readonly SoulStatConversion[] Defaults =
        {
            Rule(StatType.MaxHp, 38, StatType.Vitality, 7),
            // A deep pool: a fight is fought mostly on what was brought into it (regen in combat is only 20%).
            // Little flat, much from vitality: short of breath at the start, grows with the body (and gear).
            Rule(StatType.MaxStamina, 35, StatType.Vitality, 9),
            // Magic is scarce: a deep pool from magic, a slow trickle back (0.1 a second + a little from recovery and
            // magic; five times that out of combat) — a mage picks its moments. Magic also powers every spell (+4% a point).
            Rule(StatType.MaxMp, 10, StatType.Magic, 6),
            Rule(StatType.SpellPower, 0, StatType.Magic, .04f),
            Rule(StatType.Attack, 3, StatType.Strength, 1.6f),
            Rule(StatType.Armor, 1, StatType.Durability, 1.3f),
            Rule(StatType.PhysicalResist, 0, StatType.Durability, .005f),
            Rule(StatType.MagicResist, 0, StatType.AntiMagic, .007f),
            Rule(StatType.HpRegen, 0, StatType.Regeneration, .2f),
            // Recovery leans to MP; stamina comes back mostly with the body (vitality).
            Rule(StatType.MpRegen, .1f, StatType.Recovery, .03f, StatType.Magic, .01f),
            Rule(StatType.StaminaRegen, 2f, StatType.Vitality, .45f, StatType.Recovery, .15f),
            Rule(StatType.ActionSpeed, 1, StatType.Agility, .045f),
            Rule(StatType.MoveSpeed, 1.9f, StatType.Agility, .1f),
            Rule(StatType.Accuracy, .75f, StatType.Agility, .015f),
            Rule(StatType.Evasion, 0, StatType.Agility, .01f),
            Rule(StatType.BleedResist, 0, StatType.Vitality, .005f),
            Rule(StatType.PoisonResist, 0, StatType.Vitality, .005f),
            Rule(StatType.BurnResist, 0, StatType.Vitality, .005f),
            Rule(StatType.ChillResist, 0, StatType.Vitality, .005f),
            Rule(StatType.FearResist, 0, StatType.Will, .012f),
            Rule(StatType.StunResist, 0, StatType.Will, .01f),
            Rule(StatType.PetrifyResist, 0, StatType.Will, .012f),
            Rule(StatType.DownResist, 0, StatType.Will, .015f),
            Rule(StatType.Threat, 1, StatType.Durability, .02f),
        };

        static SoulStatConversion Rule(StatType output, float flat, StatType input, float factor)
            => new SoulStatConversion { Output = output, Formula = new SoulValue
            { Flat = flat, Terms = new[] { new SoulStatTerm { Stat = input, Factor = factor } } } };

        static SoulStatConversion Rule(StatType output, float flat, StatType input, float factor, StatType second, float secondFactor)
            => new SoulStatConversion { Output = output, Formula = new SoulValue
            { Flat = flat, Terms = new[] { new SoulStatTerm { Stat = input, Factor = factor }, new SoulStatTerm { Stat = second, Factor = secondFactor } } } };

        public SoulStatInfo Info(StatType id)
        {
            foreach (var info in UpperStatInfo) if (info.Id == id) return info;
            return new SoulStatInfo { Id = id, DisplayName = id.ToString(), Description = id.ToString(), Color = Color.white };
        }
    }
}
