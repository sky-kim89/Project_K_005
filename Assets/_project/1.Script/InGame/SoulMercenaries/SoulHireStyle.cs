using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // A mercenary's grade (saved as a number: keep the order). 일반 < 정예 < 전설.
    public enum SoulStyleRarity { Normal, Special, Rare }

    // Grades and what a recruit (SoulRecruitData) puts on a freshly made mercenary.
    public static class SoulHireStyles
    {
        static readonly StatType[] MainStats = { StatType.Strength, StatType.Vitality, StatType.Agility, StatType.Magic, StatType.Will, StatType.Luck };

        public static string RarityName(SoulStyleRarity rarity) => rarity == SoulStyleRarity.Rare ? "전설" : rarity == SoulStyleRarity.Special ? "정예" : "일반";

        public static Color RarityColor(SoulStyleRarity rarity)
            => rarity == SoulStyleRarity.Rare ? new Color(1f, .72f, .22f) : rarity == SoulStyleRarity.Special ? new Color(.5f, .68f, 1f) : new Color(.75f, .78f, .88f);

        // Extra gold a hire of this grade asks.
        public static int Premium(SoulStyleRarity rarity) => rarity == SoulStyleRarity.Rare ? 450 : rarity == SoulStyleRarity.Special ? 150 : 0;

        // A 전설 brings better gear than the guild's level gives.
        public static int GearBonus(SoulStyleRarity rarity) => rarity == SoulStyleRarity.Rare ? 1 : 0;

        // A freshly made mercenary becomes this person: its difference (+1 `Up`, −1 `Down`), stats and growth, what it
        // knows (a pattern its race or weapon cannot use is left out) and its trait.
        public static void Apply(SoulMercenary hero, SoulRecruitData recruit, SoulStatRules rules)
        {
            if (recruit != null)
            {
                hero.Style = recruit.Id;
                hero.Recruit = recruit;
                if (recruit.Up != recruit.Down) { Raise(hero, recruit.Up, 1); Raise(hero, recruit.Down, -1); }
                foreach (var bonus in recruit.Stats) Raise(hero, bonus.Stat, bonus.Value);
                var growth = new List<SoulStatBonus>(hero.GrowthWeights);
                foreach (var bonus in recruit.Growth)
                {
                    int at = growth.FindIndex(g => g.Stat == bonus.Stat);
                    if (at >= 0) growth[at] = new SoulStatBonus { Stat = bonus.Stat, Value = growth[at].Value + bonus.Value };
                    else growth.Add(bonus);
                }
                hero.GrowthWeights = growth.ToArray();
                if (recruit.Weapon != null) Arm(hero, recruit.Weapon);
                // what it can do is its own (the grade decides how much: 일반 at most one skill), not the job's whole kit
                hero.StartingActives.Clear();
                foreach (var pattern in recruit.Patterns)
                    if (pattern != null && !hero.StartingPatterns.Contains(pattern) && !hero.AllPatterns().Contains(pattern) && pattern.Compatible(hero)) hero.StartingPatterns.Add(pattern);
                foreach (var skill in recruit.Actives)
                    if (skill != null && !hero.StartingActives.Contains(skill)) hero.StartingActives.Add(skill);
                foreach (var passive in recruit.Passives)
                    if (passive != null && !hero.StartingPassives.Contains(passive)) hero.StartingPassives.Add(passive);
            }
            hero.Rebuild(rules);
            hero.Hp = hero.Stats.Total(StatType.MaxHp);
            hero.Mp = hero.Stats.Total(StatType.MaxMp);
            hero.Stamina = hero.Stats.Total(StatType.MaxStamina);
        }

        // Someone made before a trait was added to its recruit gets it now (a save from before).
        public static bool CatchUp(SoulMercenary hero)
        {
            if (hero.Recruit == null) return false;
            bool added = false;
            foreach (var passive in hero.Recruit.Passives)
                if (passive != null && !hero.StartingPassives.Contains(passive)) { hero.StartingPassives.Add(passive); added = true; }
            // the skills it was born with are its recruit's kit (a skill the library made better stays better); what it
            // learned at the library is kept apart (LearnedActives) and untouched
            var kit = new List<SoulActiveSkillData>();
            foreach (var skill in hero.Recruit.Actives)
            {
                if (skill == null) continue;
                var better = hero.StartingActives.Find(s => s != null && s.SoulId == skill.SoulId + "_plus");
                kit.Add(better ?? skill);
            }
            bool same = kit.Count == hero.StartingActives.Count && kit.TrueForAll(hero.StartingActives.Contains);
            if (!same) { hero.StartingActives.Clear(); hero.StartingActives.AddRange(kit); added = true; }
            // still carrying the job's starting weapon it was never meant to (a save from before): its own instead
            var weapon = hero.Recruit.Weapon;
            var main = hero.Equipment.Find(item => item.Slot == SoulEquipSlot.MainHand);
            if (weapon != null && main != null && main.Data != weapon && hero.Recruit.Template != null
                && System.Array.IndexOf(hero.Recruit.Template.StartingEquipment, main.Data) >= 0 && !main.Dropped && main.Enchants.Count == 0)
            { Arm(hero, weapon); added = true; }
            return added;
        }

        // Its own weapon in the main hand, as good as the one it replaces (a two-handed one leaves no room for a shield).
        static void Arm(SoulMercenary hero, SoulEquipmentData weapon)
        {
            int at = hero.Equipment.FindIndex(item => item.Slot == SoulEquipSlot.MainHand);
            var armed = new SoulItem(weapon, at >= 0 ? hero.Equipment[at].Grade : 1);
            if (at >= 0) hero.Equipment[at] = armed; else hero.Equipment.Add(armed);
            if (weapon.TwoHanded) hero.Equipment.RemoveAll(item => item.Slot == SoulEquipSlot.OffHand);
        }

        // A cut never takes a main stat below 1 (nor any below 0) — and never raises one that was already lower.
        static void Raise(SoulMercenary hero, StatType stat, float value)
        {
            hero.BaseStats.TryGetValue(stat, out float now);
            float floor = System.Array.IndexOf(MainStats, stat) >= 0 ? 1 : 0;
            hero.BaseStats[stat] = value >= 0 ? now + value : Mathf.Max(now + value, Mathf.Min(now, floor));
        }
    }
}
