using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // A party down in the dungeon. Its session runs whether anyone watches it or not (SoulExpeditionRunner):
    // unwatched it goes on at eight times the pace of the village, deciding everything by itself.
    public sealed class SoulExpedition
    {
        public int Number;                  // its party's number
        public string Name => $"{Number}파티";
        public SoulParty Squad;             // the party at home it belongs to
        public readonly List<SoulMercenary> Party = new List<SoulMercenary>();
        public readonly Dictionary<SoulMercenary, SoulTripStart> Start = new Dictionary<SoulMercenary, SoulTripStart>(); // as they left (the report)
        public SoulDungeonSession Session;
        public float ReturnAt;              // village clock: out of the dungeon then, whatever happens
        public float Backlog;               // dungeon seconds not simulated yet
        public int Seed;                    // the day's dungeon it went into
        public bool Settled;                // back in the village (reported)

        public bool Over => Session.Finished || Session.Defeated || Session.Recalled;
        // The last floor is cleared: the party waits below until it is thrown out.
        public bool Waiting => Session.Finished && !Session.HasNextFloor;
        public int Alive => Session.Mercenaries.FindAll(hero => hero.Alive).Count;

        public string Status
        {
            get
            {
                var session = Session;
                if (session.Defeated) return "전멸";
                if (session.Recalled) return "귀환";
                if (Waiting) return "최종층 돌파 — 귀환 대기";
                if (session.Finished) return $"{session.Floor}층 돌파";
                if (session.Plan == SoulPartyPlan.Stranded) return $"{session.Floor}층 · 탐색 불가";
                if (session.RecallLeft > 0 || session.Plan == SoulPartyPlan.Escape || session.Plan == SoulPartyPlan.Retreat) return $"{session.Floor}층 · 탈출 중";
                if (session.Plan == SoulPartyPlan.Recover || session.Camping > 0) return $"{session.Floor}층 · 휴식 중";
                return session.InCombat ? $"{session.Floor}층 · 전투 중" : $"{session.Floor}층 · 탐험 중";
            }
        }
    }

    // Expeditions and the village clock (SoulClock): every open party can be down at once (six at most). A party leaves the
    // roster while it is away (nothing in the village can touch it) and comes back at 19:00 — earlier if it
    // escapes or falls.
    public sealed partial class SoulCampaign
    {
        public const int MaxExpeditions = 6;
        // Fixed step for parties nobody watches; how far one may fall behind the clock (dungeon seconds).
        public const float Step = .05f, MaxBacklog = 10f;

        public float Clock = SoulClock.Start;
        public readonly List<SoulExpedition> Away = new List<SoulExpedition>();
        // The party on screen: the village slows to its pace while it is watched.
        public SoulExpedition Watching;
        public bool InExpedition => Away.Count > 0;
        // Mercenary id → the day it last went down (once a day).
        public readonly Dictionary<string, int> EnteredOn = new Dictionary<string, int>();
        public bool EnteredToday(SoulMercenary hero) => EnteredOn.TryGetValue(hero.Id, out int day) && day == SoulClock.Day(Clock);
        // The day's dungeon: every party that goes down the same day finds the same floors.
        public int WorldSeed;
        public int DaySeed => WorldSeed + SoulClock.Day(Clock) * 7919;
        // Today's dungeon: every party going down today shares its floors (a new one after midnight).
        public SoulDungeonDay Today { get; private set; }
        // Everyone the company has, home or away (the guild's roster limit counts both).
        public int HeadCount { get { int count = Roster.Count; foreach (var trip in Away) count += trip.Party.Count; return count; } }
        int turn; // who ticks first when the frame budget runs out

        // Why this party cannot go down now (null: it can).
        public string DepartBlock(SoulParty party)
        {
            if (party.Members.Count == 0) return "파티원이 없습니다";
            if (IsAway(party)) return "원정 중입니다";
            if (party.Members.Exists(EnteredToday)) return "오늘 이미 다녀온 용병";
            if (Data.Dungeon == null) return "던전이 없습니다";
            return null;
        }

        public bool CanDepart => Parties.Exists(party => DepartBlock(party) == null);

        // Every party ready to go, through the portal together.
        public List<SoulExpedition> DepartAll()
        {
            var sent = new List<SoulExpedition>();
            foreach (var party in Parties)
                if (DepartBlock(party) == null) { var trip = Depart(party, DaySeed); if (trip != null) sent.Add(trip); }
            return sent;
        }

        // The party goes down with its belt and pouch (bought first with auto-buy) and its soul stones. The
        // church's blessing goes with the first party that leaves.
        public SoulExpedition Depart(SoulParty party, int seed, SoulDungeonData dungeon = null)
        {
            dungeon = dungeon != null ? dungeon : Data.Dungeon;
            if (party == null || IsAway(party) || party.Members.Count == 0 || party.Members.Exists(EnteredToday) || dungeon == null) return null;
            foreach (var hero in party.Members)
            {
                if (Blessing != null) hero.Buffs[BlessingKey] = Blessing.Bonuses;
                hero.Rebuild(Rules);
            }
            Blessing = null; // spent on this party
            var expedition = new SoulExpedition { Number = party.Number, Squad = party, ReturnAt = SoulClock.ReturnTime(Clock), Seed = seed };
            foreach (var hero in party.Members) EnteredOn[hero.Id] = SoulClock.Day(Clock);
            expedition.Party.AddRange(party.Members);
            foreach (var hero in party.Members) expedition.Start[hero] = SoulTripStart.Of(hero);
            var session = new SoulDungeonSession(dungeon, Rules, expedition.Party, seed + 101, 1, 0, Today ?? (Today = new SoulDungeonDay()))
            {
                TargetFloor = party.TargetFloor, TargetMode = party.TargetMode,
                ExploreMode = SoulExploreMode.Advance, // on the way down: straight for the portal (a pathfinder knows it)
            };
            expedition.Session = session;
            session.Perks = ExpeditionPerks();
            Pack(party, session);
            session.AddFreeStones(session.Perks.FreeStones); // 영혼석 주머니
            foreach (var hero in party.Members) { PauseDrill(hero); Roster.Remove(hero); } // a drill waits until it is back
            Away.Add(expedition);
            Changed($"{expedition.Name} 출정! — 목표 {party.TargetFloor}층 {SoulParty.ModeName(party.TargetMode)} · 던전 시간 {SoulClock.DungeonHoursLeft(Clock, expedition.ReturnAt):0}시간, {SoulClock.HourMinute(expedition.ReturnAt)} 귀환");
            return expedition;
        }

        // The clock and every expedition, for `realSeconds` of play. Unwatched parties tick in fixed steps while
        // the frame budget lasts; the watched one every frame (smooth), at the village's slowed pace.
        public void Advance(float realSeconds, float budgetMs = 8f)
        {
            bool watching = Watching != null && !Watching.Settled;
            float minutes = realSeconds * (watching ? SoulClock.WatchRate : 1f);
            int day = SoulClock.Day(Clock);
            Clock += minutes;
            if (SoulClock.Day(Clock) != day) NewDay();
            FinishDrills();
            AutoDrills();
            if (Away.Count == 0) return;
            var budget = System.Diagnostics.Stopwatch.StartNew();
            var parties = Away.ToArray();
            turn = (turn + 1) % parties.Length;
            for (int i = 0; i < parties.Length; i++)
            {
                var trip = parties[(turn + i) % parties.Length];
                Run(trip, minutes * SoulClock.DungeonRate, trip == Watching, budget, budgetMs);
            }
        }

        void Run(SoulExpedition trip, float seconds, bool watched, System.Diagnostics.Stopwatch budget, float budgetMs)
        {
            var session = trip.Session;
            session.Unattended = !watched;
            trip.Backlog = trip.Over ? 0 : Mathf.Min(trip.Backlog + seconds, MaxBacklog);
            while (!trip.Over && trip.Backlog > (watched ? 0 : Step - 1e-4f) && (watched || budget.Elapsed.TotalMilliseconds < budgetMs))
            {
                float dt = Mathf.Min(trip.Backlog, Step);
                session.Tick(dt);
                trip.Backlog -= dt;
            }
            // Under automatic control the party goes down by itself (it only reaches the exit when fit to fight).
            // A retreating party goes home through the portal it reached instead of down.
            if (session.Finished && session.Plan == SoulPartyPlan.Retreat) Escape(trip);
            else if (session.HasNextFloor && session.AutoControl) NextFloor(trip);
            else if (session.Defeated || session.Recalled) Settle(trip);
            if (!trip.Settled && Clock >= trip.ReturnAt)
            {
                trip.Session.LeaveDungeon("던전의 시간이 다 되었습니다 — 던전 밖으로 튕겨 나갑니다");
                Settle(trip);
            }
        }

        // The time until 08:00 the next day passed at once (asked for when nobody can go down): midnight comes on
        // the way — the dungeon starts over and whoever is still below comes out — and training finishes.
        public float NextMorning => SoulClock.Day(Clock) * SoulClock.DayMinutes + SoulClock.Start;
        public void SkipToMorning()
        {
            Watching = null;
            Advance(NextMorning - Clock);
        }

        // Midnight: everyone out, the dungeon starts over.
        void ClearDungeon()
        {
            foreach (var trip in Away.ToArray())
            {
                trip.Session.LeaveDungeon("자정 — 던전이 초기화되어 밖으로 나왔습니다");
                Settle(trip);
            }
            Today = null;
        }

        public void NextFloor(SoulExpedition trip)
        {
            var old = trip.Session;
            if (trip.Settled || !old.HasNextFloor) return;
            trip.Session = old.NextFloor(trip.Seed + (old.Floor + 1) * 101); // the day's floor: the same for every party
        }

        // Out now with what the party carries (the player chose to stop after a floor).
        public void Escape(SoulExpedition trip)
        {
            if (trip.Settled) return;
            trip.Session.LeaveDungeon("탈출: 전리품을 들고 마을로 돌아갑니다");
            Settle(trip);
        }

        void Settle(SoulExpedition trip)
        {
            if (trip.Settled) return;
            Return(trip);
        }

        // The party comes back up: loot, gold, stones and potions left, quest progress. Whoever fell down there is
        // carried home with FallenWounds wounds (everything it had kept) — at 최상 it is dead, gone for good with
        // everything it carried (equipment, absorbed souls).
        public List<string> Return(SoulExpedition trip)
        {
            var session = trip.Session;
            var report = new List<string>();
            Away.Remove(trip);
            trip.Settled = true;
            Expeditions++;
            MakeReport(trip);
            Wallet.Add(eItem.Gold, session.Gold);
            // what the party found and nobody wore yet: one last look for who needs it (those who fell too, unless at
            // 최상); the rest goes to the storehouse
            var takers = trip.Party.FindAll(hero => session.Mercenaries.Contains(hero) && hero.Alive || !Permadeath);
            for (int i = session.Inventory.Count - 1; i >= 0; i--)
            {
                var item = session.Inventory[i];
                var taker = BestTaker(takers, item, out var replace);
                if (taker == null) continue;
                session.Inventory.RemoveAt(i);
                Inventory.AddRange(Wear(taker, item, replace, Rules)); // what it replaced: to the storehouse
                report.Add($"{taker.Name}: {item.Name} 장착");
            }
            Inventory.AddRange(session.Inventory);
            Vault.AddRange(session.Vault);
            Wallet.Add(eItem.SoulStone, session.StonesLeft);
            Unpack(session);
            report.Add($"금화 +{session.Gold} · 장비 {session.Inventory.Count}개 · 영혼 {session.Vault.Count}개");
            CountQuests(session, report);
            foreach (var hero in trip.Party)
            {
                if (session.Mercenaries.Contains(hero) && hero.Alive)
                {
                    Roster.Add(hero); // still in its party: ready to go again together
                    ResumeDrill(hero);
                    hero.Buffs.Remove(BlessingKey);
                    hero.Fatigue = 0; // a night in the village
                    hero.TickTimedBuffs(float.MaxValue);
                    hero.Rebuild(Rules);
                    RestoreAll(hero);
                    continue;
                }
                if (!Permadeath)
                {
                    Roster.Add(hero); // carried out: back in the village, badly hurt
                    ResumeDrill(hero);
                    hero.Buffs.Remove(BlessingKey);
                    hero.Statuses.Clear();
                    hero.DownTime = 0;
                    hero.Fatigue = 0;
                    hero.TickTimedBuffs(float.MaxValue);
                    hero.AddWounds(FallenWounds);
                    hero.Rebuild(Rules);
                    RestoreAll(hero);
                    report.Add($"{hero.Name} 쓰러짐 — 부상 {hero.Wounds}로 실려 왔습니다");
                    continue;
                }
                trip.Squad?.Members.Remove(hero);
                Fallen.Add(hero.Name);
                Record(hero).Status = SoulRecordStatus.Fallen; // also those who fell on a floor above the last
                report.Add($"{hero.Name} 사망 — 동료를 잃었습니다");
            }
            // back in their old places: the roster keeps the order people joined in (the codex's order)
            Roster.Sort((a, b) => Records.FindIndex(r => r.Id == a.Id).CompareTo(Records.FindIndex(r => r.Id == b.Id)));
            AfterReturn(session, report);
            Note(report);
            Changed(report.Exists(line => line.Contains("사망")) ? $"{trip.Name} 귀환 — 잃은 동료가 있습니다" : $"{trip.Name} 귀환");
            return report;
        }

        // Log lines in reading order (the newest entry is on top).
        void Note(List<string> lines)
        {
            for (int i = lines.Count - 1; i >= 0; i--) Log.Insert(0, lines[i]);
            while (Log.Count > 12) Log.RemoveAt(Log.Count - 1);
        }
    }
}
