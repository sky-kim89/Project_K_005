using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // Fatigue (피로도 0~100): time in the dungeon wears the party down. One real second is one dungeon minute.
    // It rises by the minute (twice as fast in a fight), slower for the hardy (vitality), and only sleep brings it
    // down: a camp (campfire or camping kit). Back in the village everyone is rested.
    public static class SoulFatigue
    {
        public const float Max = 100;
        // Dungeon minutes per fatigue point out of combat (1 point / 8 minutes: about 7.5 an hour).
        public const float MinutesPerPoint = 8f, CombatScale = 2f;
        public const float CampfireRelief = 40, KitRelief = 70;

        public static readonly int[] TierFrom = { 0, 40, 70, 90 };
        static readonly string[] names = { "상쾌", "피곤", "지침", "탈진" };

        public static int Tier(float fatigue)
        {
            for (int i = TierFrom.Length - 1; i > 0; i--) if (fatigue >= TierFrom[i]) return i;
            return 0;
        }

        public static string Name(float fatigue) => names[Tier(fatigue)];

        // Pattern stamina cost multiplier.
        public static float CostScale(float fatigue)
        {
            switch (Tier(fatigue)) { case 1: return 1.1f; case 2: return 1.25f; case 3: return 1.4f; default: return 1f; }
        }

        // Shares taken off the current value at each tier (like status grades).
        public static List<(StatType stat, float share)> Shares(int tier)
        {
            var shares = new List<(StatType, float)>();
            if (tier >= 1) shares.Add((StatType.StaminaRegen, -.2f * tier));
            if (tier >= 2) { shares.Add((StatType.Accuracy, -.05f * (tier - 1))); shares.Add((StatType.MoveSpeed, -.1f * (tier - 1))); }
            if (tier >= 3) shares.Add((StatType.ActionSpeed, -.1f));
            return shares;
        }

        public static string Effect(int tier)
        {
            switch (tier)
            {
                case 1: return "스태미나 재생 −20% · 패턴 비용 +10%";
                case 2: return "스태미나 재생 −40% · 패턴 비용 +25% · 명중 −5% · 이동 −10%";
                case 3: return "스태미나 재생 −60% · 패턴 비용 +40% · 명중 −10% · 이동 −20% · 행동 속도 −10%";
                default: return "피로 없음";
            }
        }

        public const string BuffId = "fatigue";

        // Rate for one mercenary: hardier bodies tire slower (2% a point of vitality, at most half).
        public static float Gain(SoulMercenary hero, float minutes, bool fighting)
            => minutes / MinutesPerPoint * (fighting ? CombatScale : 1f) * (1 - Mathf.Min(.5f, hero.Stats.Total(StatType.Vitality) * .02f));

        public static void Add(SoulMercenary hero, float amount)
        {
            hero.Fatigue = Mathf.Clamp(hero.Fatigue + amount, 0, Max);
            Refresh(hero);
        }

        // The stat part lives on a timed-buff layer, rebuilt only when the tier changes.
        public static void Refresh(SoulMercenary hero)
        {
            int tier = Tier(hero.Fatigue);
            if (tier == hero.FatigueTier && hero.TimedBuffs.Exists(b => b.Id == BuffId) == tier > 0) return;
            hero.FatigueTier = tier;
            var bonuses = new List<SoulStatBonus>();
            foreach (var (stat, share) in Shares(tier))
            {
                float current = hero.Stats.Combat.Get(stat) - hero.Stats.Combat.GetLayer("timed:" + BuffId, stat);
                bonuses.Add(new SoulStatBonus { Stat = stat, Value = current * share });
            }
            hero.AddTimedBuff(BuffId, bonuses.ToArray(), bonuses.Count > 0 ? float.MaxValue : 0);
        }
    }
}
