using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // What one expedition did, floor after floor (every floor's session shares it): each mercenary's fighting and
    // what the party found on the way.
    public sealed class SoulTripTally
    {
        public sealed class Line { public float Dealt, Taken; public int Kills, Evasions, Guards; }
        public readonly Dictionary<SoulMercenary, Line> Heroes = new Dictionary<SoulMercenary, Line>();
        public readonly List<SoulItem> Items = new List<SoulItem>();
        public readonly List<string> Supplies = new List<string>();  // supply ids found in chests
        public readonly List<string> Absorbed = new List<string>();  // "who: whose soul"

        public Line For(SoulMercenary hero)
        {
            if (!Heroes.TryGetValue(hero, out var line)) Heroes[hero] = line = new Line();
            return line;
        }

        // One combat event into the tally of the party each mercenary in it belongs to (`owner`: on a shared floor
        // the monsters' blows land in the host party's feed).
        public static void Count(SoulCombatEvent evt, Func<SoulMercenary, SoulTripTally> owner)
        {
            switch (evt.Kind)
            {
                case SoulEventKind.Hit:
                    if (evt.Actor is SoulMercenary dealer) { var tally = owner(dealer); if (tally != null) tally.For(dealer).Dealt += evt.Amount; }
                    if (evt.Target is SoulMercenary hurt) { var tally = owner(hurt); if (tally != null) tally.For(hurt).Taken += evt.Amount; }
                    break;
                case SoulEventKind.Evade:
                case SoulEventKind.Roll:
                    if (evt.Target is SoulMercenary dodger) { var tally = owner(dodger); if (tally != null) tally.For(dodger).Evasions++; }
                    break;
                case SoulEventKind.Guard:
                    if (evt.Target is SoulMercenary guard) { var tally = owner(guard); if (tally != null) tally.For(guard).Guards++; }
                    break;
            }
        }
    }

    public enum SoulFindKind { Item, Book, Soul, Supply }

    // The report of a finished expedition (saved): kept in the village for the party to be looked at.
    [Serializable]
    public sealed class SoulTripReport
    {
        public int Party, Day, Floor, Gold;
        public string Outcome;
        public bool Seen;
        public List<SoulTripHero> Heroes = new List<SoulTripHero>();
        public List<SoulTripFind> Found = new List<SoulTripFind>();

        public string Title => $"{Party}파티 · {Day}일차";
    }

    [Serializable]
    public sealed class SoulTripHero
    {
        public string Id, Name;
        public bool Alive;
        public int LevelFrom, LevelTo, Kills, Evasions, Guards;
        public float Dealt, Taken;
        public List<SoulTripGain> Gains = new List<SoulTripGain>();
    }

    [Serializable]
    public sealed class SoulTripGain
    {
        public StatType Stat;
        public int Amount;
    }

    [Serializable]
    public sealed class SoulTripFind
    {
        public SoulFindKind Kind;
        public string Name;
        public int Grade;   // items: the grade colour
        public bool Worn;   // items: put on down there
    }

    // Who went down, as they were when they left (levels and the stats that grow in the dungeon).
    public sealed class SoulTripStart
    {
        public int Level;
        public readonly Dictionary<StatType, float> Stats = new Dictionary<StatType, float>();

        // every upper stat a level-up point can go to (the main six and the rest: durability, the powers, recovery …)
        public static readonly StatType[] Grown =
        {
            StatType.Strength, StatType.Vitality, StatType.Agility, StatType.Magic, StatType.Will, StatType.Luck,
            StatType.Regeneration, StatType.Durability, StatType.AntiMagic, StatType.Recovery, StatType.SlashPower, StatType.ImpactPower, StatType.PiercePower,
        };

        // Level points and experience-born 정신력 (the stats a trip can raise).
        public static float Grew(SoulMercenary hero, StatType stat)
        {
            hero.LevelStats.TryGetValue(stat, out float level);
            hero.MentalStats.TryGetValue(stat, out float mental);
            return level + mental;
        }

        public static SoulTripStart Of(SoulMercenary hero)
        {
            var start = new SoulTripStart { Level = hero.Level };
            foreach (var stat in Grown) start.Stats[stat] = Grew(hero, stat);
            return start;
        }
    }

    public sealed partial class SoulCampaign
    {
        public const int KeptReports = 30;
        // Newest first.
        public readonly List<SoulTripReport> Reports = new List<SoulTripReport>();
        public SoulTripReport LastReport(int party) => Reports.Find(r => r.Party == party);

        SoulTripReport MakeReport(SoulExpedition trip)
        {
            var session = trip.Session;
            var report = new SoulTripReport { Party = trip.Number, Day = SoulClock.Day(Clock), Floor = session.Floor, Gold = session.Gold, Outcome = trip.Status };
            foreach (var hero in trip.Party)
            {
                var line = session.Tally.For(hero);
                var entry = new SoulTripHero
                {
                    Id = hero.Id, Name = hero.Name, Alive = session.Mercenaries.Contains(hero) && hero.Alive, LevelTo = hero.Level,
                    Kills = line.Kills, Evasions = line.Evasions, Guards = line.Guards, Dealt = line.Dealt, Taken = line.Taken,
                };
                if (trip.Start.TryGetValue(hero, out var start))
                {
                    entry.LevelFrom = start.Level;
                    foreach (var stat in SoulTripStart.Grown)
                    {
                        int gain = Mathf.RoundToInt(SoulTripStart.Grew(hero, stat) - start.Stats[stat]);
                        if (gain > 0) entry.Gains.Add(new SoulTripGain { Stat = stat, Amount = gain });
                    }
                }
                else entry.LevelFrom = hero.Level;
                report.Heroes.Add(entry);
            }
            foreach (var item in session.Tally.Items)
                report.Found.Add(new SoulTripFind { Kind = SoulFindKind.Item, Name = item.Name, Grade = item.Grade, Worn = !session.Inventory.Contains(item) });
            foreach (var book in session.FoundBooks) report.Found.Add(new SoulTripFind { Kind = SoulFindKind.Book, Name = book.SkillName });
            foreach (var soul in session.Tally.Absorbed) report.Found.Add(new SoulTripFind { Kind = SoulFindKind.Soul, Name = soul });
            foreach (var soul in session.Vault) report.Found.Add(new SoulTripFind { Kind = SoulFindKind.Soul, Name = soul.OriginMonster + " (영혼석)" });
            foreach (var id in session.Tally.Supplies) report.Found.Add(new SoulTripFind { Kind = SoulFindKind.Supply, Name = SoulSupplies.Get(id)?.Name ?? id });
            Reports.Insert(0, report);
            while (Reports.Count > KeptReports) Reports.RemoveAt(Reports.Count - 1);
            return report;
        }
    }
}
