using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // One of the company's parties: who goes, how deep, what it does there, what it takes along. Members stay
    // in their party while it is away (the roster lends them to the expedition) and come back to it.
    public sealed class SoulParty
    {
        public int Number;                      // 1..MaxParties
        public string Name => $"{Number}파티";
        public readonly List<SoulMercenary> Members = new List<SoulMercenary>();
        // Down to this floor, then no deeper: hunt (monsters) or farm (boss, chests, hidden stages) there.
        public int TargetFloor = 2;
        public SoulExploreMode TargetMode = SoulExploreMode.Hunt;
        // Provisions (준비물): kind and count, in the order chosen.
        public readonly List<SoulCarry> Carry = new List<SoulCarry>();

        public static string ModeName(SoulExploreMode mode) => mode == SoulExploreMode.Farm ? "탐험" : "사냥";

        // A rough 전투력 for the guild's cards: health, damage, defence, level and souls.
        public static int PowerOf(SoulMercenary hero)
            => Mathf.RoundToInt(hero.Stats.Total(StatType.MaxHp) * .5f + (hero.Stats.Total(StatType.Attack) + hero.Stats.Total(StatType.Magic)) * 4
                + hero.Stats.Total(StatType.Armor) * 3 + hero.Level * 10 + hero.Souls.Count * 25);

        public int Power { get { int power = 0; foreach (var hero in Members) power += PowerOf(hero); return power; } }
    }

    [System.Serializable]
    public sealed class SoulCarry
    {
        public string Id;
        public int Count;
    }

    // The parties: formed in the guild, equipped in the storehouse, sent down together through the portal. A new
    // company has StartingParties; the guild's 파티 편성 opens one more a level, MaxParties in all (PartyLimit).
    // Only the open ones are in Parties.
    public sealed partial class SoulCampaign
    {
        public const int MaxParties = MaxExpeditions, StartingParties = 1;
        public readonly List<SoulParty> Parties = new List<SoulParty>();

        // Opens parties up to PartyLimit (a new company, a bought level, a loaded save).
        public void SyncParties()
        {
            while (Parties.Count < PartyLimit) Parties.Add(new SoulParty { Number = Parties.Count + 1 });
        }

        public SoulParty PartyOf(SoulMercenary hero) => Parties.Find(party => party.Members.Contains(hero));

        // A mercenary of the company by id, at home or away on an expedition (null: gone).
        public SoulMercenary FindHero(string id)
        {
            var hero = Roster.Find(h => h.Id == id);
            if (hero != null) return hero;
            foreach (var trip in Away) { hero = trip.Party.Find(h => h.Id == id); if (hero != null) return hero; }
            return null;
        }
        public SoulExpedition TripOf(SoulParty party) => Away.Find(trip => trip.Squad == party);
        public bool IsAway(SoulParty party) => TripOf(party) != null;
        public bool IsAway(SoulMercenary hero) => Away.Exists(trip => trip.Party.Contains(hero));
        // Mercenaries at home in no party.
        public List<SoulMercenary> Unassigned => Roster.FindAll(hero => PartyOf(hero) == null);
        // A party at home with people in it (sparring at the training ground uses the first one).
        public SoulParty HomeParty => Parties.Find(party => party.Members.Count > 0 && !IsAway(party));

        // Into a party (out of any other); null takes the mercenary out of every party.
        public bool Assign(SoulMercenary hero, SoulParty party)
        {
            if (!Roster.Contains(hero)) return false;
            var from = PartyOf(hero);
            if (from == party) return true;
            if (party != null && (IsAway(party) || party.Members.Count >= PartySize)) return false;
            from?.Members.Remove(hero);
            party?.Members.Add(hero);
            Changed();
            return true;
        }

        public bool TogglePartyMember(SoulMercenary hero, SoulParty party)
            => party != null && party.Members.Contains(hero) ? Assign(hero, null) : Assign(hero, party);

        public void SetTarget(SoulParty party, int floor, SoulExploreMode mode)
        {
            if (IsAway(party)) return;
            party.TargetFloor = UnityEngine.Mathf.Clamp(floor, 1, SoulDungeonSession.FinalFloor);
            party.TargetMode = mode == SoulExploreMode.Farm ? SoulExploreMode.Farm : SoulExploreMode.Hunt;
            Changed();
        }

        // A new hire joins the first party at home with a free seat.
        SoulParty SeatFor() => Parties.Find(party => !IsAway(party) && party.Members.Count > 0 && party.Members.Count < PartySize)
            ?? Parties.Find(party => !IsAway(party) && party.Members.Count < PartySize);
    }
}
