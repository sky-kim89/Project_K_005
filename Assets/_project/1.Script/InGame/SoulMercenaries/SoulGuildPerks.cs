using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // New kinds go at the end: levels are saved by name, but keep the order stable anyway.
    public enum SoulPerk
    {
        PartySize, CarryStack, CarryKinds, SoulStones, CautiousRetreat, ExitMap, CampMastery, ReturnScrollShop,
        PotionPower, ShopDiscount, AntidoteWard, ScrollScribe,
        TrainingCost, TrainingTime, FocusedTraining, PatternTutor, JointSparring,
        Strength, Vitality, Agility, Magic, Will, Luck,
        PartyCount,
        TrainingCap,
    }

    public enum SoulPerkGroup { Expedition, Supplies, Training, Stats }

    // One guild ability (길드 능력): bought level by level with gold; each level wants a guild level and a renown tier.
    public sealed class SoulPerkDef
    {
        public SoulPerk Perk;
        public SoulPerkGroup Group;
        public string Name, Icon;
        public string[] Effects;     // what each level gives (short)
        public int[] Costs, GuildLevels, RenownTiers;
        public int MaxLevel => Costs.Length;
    }

    // What an expedition takes along from the guild's abilities (set when the party leaves, kept down every floor).
    public sealed class SoulExpeditionPerks
    {
        public float PotionPower = 1, ScrollDuration = 1, CrisisHealth = .25f, CampRelief = 1;
        public int CampWounds = 1, FreeStones;
        public bool AntidoteWard, RevealExits;
    }

    public sealed partial class SoulCampaign
    {
        public static readonly string[] GroupNames = { "원정", "물약 · 보급", "훈련", "능력치" };
        public const string GuildStatsKey = "guild";

        static SoulPerkDef Perk(SoulPerk perk, SoulPerkGroup group, string name, string icon, string[] effects, int[] costs, int[] guild = null, int[] renown = null)
            => new SoulPerkDef { Perk = perk, Group = group, Name = name, Icon = icon, Effects = effects, Costs = costs,
                GuildLevels = guild ?? new int[costs.Length], RenownTiers = renown ?? new int[costs.Length] };

        // Every mercenary gets it (home or away): well above what one drill costs one mercenary.
        static readonly int[] StatCosts = { 1000, 2000, 3500, 5500, 8000, 11000, 15000, 20000, 26000, 33000 };
        static readonly int[] StatRenown = { 0, 0, 0, 1, 1, 1, 2, 2, 2, 3 };
        static readonly int[] StatGuild = { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 };
        static SoulPerkDef StatPerk(SoulPerk perk, string name, StatType stat)
        {
            var effects = new string[StatCosts.Length];
            for (int i = 0; i < effects.Length; i++) effects[i] = $"{name} +{i + 1}";
            return Perk(perk, SoulPerkGroup.Stats, name, "stat:" + stat, effects, StatCosts, StatGuild, StatRenown);
        }

        public static readonly SoulPerkDef[] PerkDefs =
        {
            // the guild's level opens them step by step: first a full party of five, then more parties (up to six)
            Perk(SoulPerk.PartySize, SoulPerkGroup.Expedition, "파티 정원", "ui:party", new[] { "파티 4명", "파티 5명" }, new[] { 800, 2500 }, new[] { 1, 2 }),
            Perk(SoulPerk.PartyCount, SoulPerkGroup.Expedition, "파티 편성", "ui:party", new[] { "파티 2개", "파티 3개", "파티 4개", "파티 5개", "파티 6개" }, new[] { 1500, 3500, 7000, 12000, 20000 }, new[] { 2, 3, 4, 4, 5 }, new[] { 0, 1, 1, 2, 2 }),
            Perk(SoulPerk.CarryKinds, SoulPerkGroup.Expedition, "준비물 칸", "ui:gold", new[] { "준비물 종류 +1", "준비물 종류 +2" }, new[] { 600, 1800 }, new[] { 2, 3 }, new[] { 0, 2 }),
            Perk(SoulPerk.SoulStones, SoulPerkGroup.Expedition, "영혼석 주머니", "ui:preserve", new[] { "출정 때 영혼석 +1", "출정 때 영혼석 +2", "출정 때 영혼석 +3" }, new[] { 400, 1000, 2500 }, new[] { 1, 2, 3 }, new[] { 0, 0, 1 }),
            Perk(SoulPerk.CautiousRetreat, SoulPerkGroup.Expedition, "신중한 후퇴", "ui:exit", new[] { "위기 판정 HP 30%", "위기 판정 HP 35%" }, new[] { 400, 1200 }, new[] { 1, 2 }, new[] { 0, 1 }),
            Perk(SoulPerk.ExitMap, SoulPerkGroup.Expedition, "출구 지도", "ui:map", new[] { "포탈 위치 표시" }, new[] { 1500 }, new[] { 3 }, new[] { 1 }),
            Perk(SoulPerk.CampMastery, SoulPerkGroup.Expedition, "야영 숙련", "ui:camp", new[] { "야영 피로 회복 +25%", "야영 부상 회복 2단계" }, new[] { 500, 1500 }, new[] { 1, 3 }),
            Perk(SoulPerk.ReturnScrollShop, SoulPerkGroup.Expedition, "귀환 두루마리 조달", "ui:scroll_return", new[] { "상점 판매 · 1200 금화" }, new[] { 2000 }, new[] { 4 }, new[] { 2 }),
            Perk(SoulPerk.PotionPower, SoulPerkGroup.Supplies, "물약 효율", "ui:potion", new[] { "회복량 +10%", "회복량 +20%", "회복량 +30%" }, new[] { 300, 900, 2200 }, new[] { 1, 2, 4 }, new[] { 0, 0, 2 }),
            Perk(SoulPerk.ShopDiscount, SoulPerkGroup.Supplies, "상점 할인", "ui:gold", new[] { "소모품 −5%", "소모품 −10%", "소모품 −15%" }, new[] { 400, 1000, 2500 }, new[] { 1, 3, 4 }, new[] { 0, 1, 2 }),
            Perk(SoulPerk.AntidoteWard, SoulPerkGroup.Supplies, "해독 전문", "ui:antidote", new[] { "해독제·만능약: 상태이상 면역 +10초" }, new[] { 800 }, new[] { 2 }, new[] { 1 }),
            Perk(SoulPerk.ScrollScribe, SoulPerkGroup.Supplies, "두루마리 필사", "ui:scroll_heal", new[] { "두루마리 지속 +20%", "두루마리 지속 +40%" }, new[] { 500, 1500 }, new[] { 2, 3 }, new[] { 0, 2 }),
            Perk(SoulPerk.TrainingCost, SoulPerkGroup.Training, "훈련 비용 절감", "ui:gold", new[] { "훈련비 −10%", "훈련비 −20%", "훈련비 −30%" }, new[] { 400, 1000, 2500 }, new[] { 1, 3, 4 }, new[] { 0, 1, 2 }),
            Perk(SoulPerk.TrainingTime, SoulPerkGroup.Training, "훈련 시간 단축", "ui:cooldown", new[] { "훈련 시간 −15%", "훈련 시간 −30%", "훈련 시간 −45%" }, new[] { 300, 900, 2200 }, new[] { 1, 2, 4 }, new[] { 0, 0, 1 }),
            Perk(SoulPerk.FocusedTraining, SoulPerkGroup.Training, "집중 훈련", "ui:skill", new[] { "15% 확률로 두 배", "30% 확률로 두 배" }, new[] { 800, 2400 }, new[] { 2, 4 }, new[] { 0, 2 }),
            Perk(SoulPerk.PatternTutor, SoulPerkGroup.Training, "패턴 교관", "ui:skill", new[] { "패턴 수련 선택지 4개" }, new[] { 1500 }, new[] { 3 }, new[] { 1 }),
            Perk(SoulPerk.JointSparring, SoulPerkGroup.Training, "합동 대련", "ui:party", new[] { "대련에 두 파티 함께 출전" }, new[] { 1000 }, new[] { 3 }),
            Perk(SoulPerk.TrainingCap, SoulPerkGroup.Training, "훈련 한계", "ui:level", new[] { "훈련마다 10회까지", "훈련마다 15회까지", "훈련마다 20회까지" }, new[] { 1500, 4000, 9000 }, new[] { 1, 3, 5 }, new[] { 0, 1, 2 }),
            StatPerk(SoulPerk.Strength, "근력", StatType.Strength),
            StatPerk(SoulPerk.Vitality, "체력", StatType.Vitality),
            StatPerk(SoulPerk.Agility, "민첩", StatType.Agility),
            StatPerk(SoulPerk.Magic, "마력", StatType.Magic),
            StatPerk(SoulPerk.Will, "정신력", StatType.Will),
            StatPerk(SoulPerk.Luck, "운", StatType.Luck),
        };

        public static SoulPerkDef PerkDef(SoulPerk perk) => System.Array.Find(PerkDefs, def => def.Perk == perk);

        public readonly Dictionary<SoulPerk, int> Perks = new Dictionary<SoulPerk, int>();
        public int PerkLevel(SoulPerk perk) => Perks.TryGetValue(perk, out int level) ? level : 0;

        // Why the next level cannot be bought (null: it can).
        public string PerkBlock(SoulPerk perk)
        {
            var def = PerkDef(perk);
            int level = PerkLevel(perk);
            if (def == null || level >= def.MaxLevel) return "최고 단계";
            if (Level(SoulBuildingKind.Guild) < def.GuildLevels[level]) return $"길드 {def.GuildLevels[level]}단계 필요";
            if (RenownTier < def.RenownTiers[level]) return $"명성 '{renownNames[def.RenownTiers[level]]}' 필요";
            if (Gold < def.Costs[level]) return "금화 부족";
            return null;
        }

        public bool BuyPerk(SoulPerk perk)
        {
            var def = PerkDef(perk);
            if (PerkBlock(perk) != null || !Spend(def.Costs[PerkLevel(perk)])) return false;
            Perks[perk] = PerkLevel(perk) + 1;
            if (def.Group == SoulPerkGroup.Stats) ApplyGuildStats();
            if (perk == SoulPerk.ReturnScrollShop) RefreshStock();
            if (perk == SoulPerk.PartyCount) SyncParties();
            Changed($"길드 능력: {def.Name} {PerkLevel(perk)}단계 — {def.Effects[PerkLevel(perk) - 1]}");
            return true;
        }

        // ── effects ──────────────────────────────────────────────

        public int PartySize => 3 + PerkLevel(SoulPerk.PartySize);
        public int PartyLimit => Mathf.Min(MaxParties, StartingParties + PerkLevel(SoulPerk.PartyCount));
        public int CarryLimit => CarryPerKind; // 3 of a kind (the 준비물 가방 is gone)
        public float SupplyDiscount => 1 - .05f * PerkLevel(SoulPerk.ShopDiscount);
        public float TrainingCostScale => 1 - .1f * PerkLevel(SoulPerk.TrainingCost);
        public float TrainingMinutes(SoulTraining training) => training.Minutes * (1 - .15f * PerkLevel(SoulPerk.TrainingTime));
        public float DoubleTrainingChance => .15f * PerkLevel(SoulPerk.FocusedTraining);
        public int PatternChoices => 3 + PerkLevel(SoulPerk.PatternTutor);
        public const int ReturnScrollPrice = 1200;

        public SoulExpeditionPerks ExpeditionPerks() => new SoulExpeditionPerks
        {
            PotionPower = 1 + .1f * PerkLevel(SoulPerk.PotionPower),
            ScrollDuration = 1 + .2f * PerkLevel(SoulPerk.ScrollScribe),
            CrisisHealth = .25f + .05f * PerkLevel(SoulPerk.CautiousRetreat),
            CampRelief = PerkLevel(SoulPerk.CampMastery) >= 1 ? 1.25f : 1,
            CampWounds = PerkLevel(SoulPerk.CampMastery) >= 2 ? 2 : 1,
            FreeStones = PerkLevel(SoulPerk.SoulStones),
            AntidoteWard = PerkLevel(SoulPerk.AntidoteWard) > 0,
            RevealExits = PerkLevel(SoulPerk.ExitMap) > 0,
        };

        // The six main stats from the guild, on every mercenary (home or away).
        public SoulStatBonus[] GuildStats()
        {
            var list = new List<SoulStatBonus>();
            void Add(SoulPerk perk, StatType stat) { int level = PerkLevel(perk); if (level > 0) list.Add(new SoulStatBonus { Stat = stat, Value = level }); }
            Add(SoulPerk.Strength, StatType.Strength); Add(SoulPerk.Vitality, StatType.Vitality); Add(SoulPerk.Agility, StatType.Agility);
            Add(SoulPerk.Magic, StatType.Magic); Add(SoulPerk.Will, StatType.Will); Add(SoulPerk.Luck, StatType.Luck);
            return list.ToArray();
        }

        public void ApplyGuildStats()
        {
            var bonuses = GuildStats();
            void Apply(SoulMercenary hero)
            {
                if (bonuses.Length > 0) hero.Buffs[GuildStatsKey] = bonuses; else hero.Buffs.Remove(GuildStatsKey);
                hero.Rebuild(Rules);
            }
            foreach (var hero in Roster) Apply(hero);
            foreach (var trip in Away) foreach (var hero in trip.Party) Apply(hero);
        }
    }
}
