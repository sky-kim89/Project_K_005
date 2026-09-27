using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    public enum SoulPartyPlan { Explore, Recover, Escape, Stranded, Retreat }

    // What the party decides by itself under automatic control (always when nobody watches): it goes on while it
    // can still fight — wounds and all. Worn out, it rests where it stands until rested; too wounded to fight
    // (rest does not heal wounds), it reads a mending scroll if it has one, otherwise leaves the dungeon. Only a
    // party fit to fight walks through a portal, so this also decides whether it goes a floor deeper — never
    // deeper than its target floor, where it hunts or farms instead. It also shares out what it finds: souls to
    // whoever has room (the rest into soul stones), gear to whoever it is an upgrade for.
    public sealed partial class SoulDungeonSession
    {
        // Nobody is watching (the expedition runs in the background): choices meant for the player are skipped
        // and the party decides for itself.
        public bool Unattended;
        public bool AutoControl => AutoExplore || Unattended;
        public SoulPartyPlan Plan { get; private set; }

        // The party's order from the village: down to this floor, then hunt or farm there (Explore: no target).
        public int TargetFloor = FinalFloor;
        public SoulExploreMode TargetMode = SoulExploreMode.Explore;
        public bool AtTarget => TargetMode != SoulExploreMode.Explore && Floor >= TargetFloor;

        // Fit to fight: fewer than three wounds and not exhausted.
        public const int WoundLimit = 3;
        // Resting in place takes fatigue away, slowly (lying on the dungeon floor is no bed): half the old rate.
        // The party goes on once nobody is tired any more.
        public const float SleepRelief = .12f;
        // Slept on the floor this often this trip (all floors): the next time the party is worn out it goes home.
        public const int MaxFloorSleeps = 2;
        public int FloorSleeps;

        public static bool Wounded(SoulMercenary hero) => hero.Wounds >= WoundLimit;

        // A small party turns back sooner: two alive, at two wounds on anyone (and its crisis comes a little earlier);
        // one alone, at the first wound, at once.
        public int Living { get { int count = 0; foreach (var hero in Mercenaries) if (hero.Alive) count++; return count; } }
        public int RetreatWounds => Mathf.Max(1, (Living >= 3 ? WoundLimit : Living == 2 ? PairRetreatWounds : 1) - (PotionCaution >= 2 ? 1 : 0));
        public const int PairRetreatWounds = 2;
        public const float PairCrisisMargin = .05f, SoloCrisisMargin = .1f, PairLosingOdds = 1f;

        // What the potions left say about the next fight: 0 — one or more a head, 1 — fewer than heads, 2 — none at all.
        // The shorter, the sooner the party gives up a fight (CrisisPerCaution, OddsPerCaution) and, with none, a
        // wound less sends it home (RetreatWounds).
        public int PotionCaution
        {
            get
            {
                int potions = HealPotions, living = Living;
                return potions <= 0 ? 2 : potions < living ? 1 : 0;
            }
        }
        public const float CrisisPerCaution = .04f, OddsPerCaution = .15f;
        public static bool Exhausted(SoulMercenary hero) => SoulFatigue.Tier(hero.Fatigue) >= 3;
        public static bool CanFight(SoulMercenary hero) => hero.Alive && !Wounded(hero) && !Exhausted(hero);

        // More than half the living can fight. `rested`: as if the fatigue had been slept off.
        public bool PartyCanFight(bool rested = false)
        {
            int alive = 0, fit = 0;
            foreach (var hero in Mercenaries)
            {
                if (!hero.Alive) continue;
                alive++;
                if (!Wounded(hero) && (rested || !Exhausted(hero))) fit++;
            }
            return fit > 0 && fit * 2 > alive; // more than half of the living
        }

        // Rested enough to go on: everyone at RestedFatigue or less — back under 지침 (70) with a margin, or the
        // party would stop again a few steps later (fatigue comes back at a point every 8 minutes). 피곤 is walked on.
        public const float RestedFatigue = 60f;
        bool Rested => Mercenaries.TrueForAll(hero => !hero.Alive || hero.Fatigue <= RestedFatigue);
        bool Sleeping => Plan == SoulPartyPlan.Recover && !partyFighting && Camping <= 0;

        int HealPotions => Count(Belt, SoulSupplies.HealPotion) + Count(Belt, "potion_greater");

        // Auto exploration, out of combat. True: the plan holds the party this step.
        // A fallen ally or wounds too deep to fight: mend what can be mended (a mending scroll, a camp — it heals a
        // wound —, a potion); with nothing left to recover with the party stops where it stands (sits or lies down)
        // and goes on only under the player's orders. Worn out: it sleeps it off.
        bool FollowPlan()
        {
            if (Camping > 0 || RecallLeft > 0 || GoingToCamp) return true;
            var leader = Leader;
            if (leader == null) return true;
            if (Plan == SoulPartyPlan.Escape && !Recalled) Plan = SoulPartyPlan.Retreat; // the scroll was broken off
            if (Plan == SoulPartyPlan.Retreat) return true; // TickRetreat walks it out
            if (Plan == SoulPartyPlan.Stranded)
            {
                if (!Arrived(leader)) SetDestination(leader, leader.Position, out Vector2 _, playerOrder: false);
                return true;
            }
            if (Plan == SoulPartyPlan.Recover)
            {
                if (!Rested)
                {
                    if (!Arrived(leader)) SetDestination(leader, leader.Position, out Vector2 _, playerOrder: false);
                    return true;
                }
                Plan = SoulPartyPlan.Explore;
                Log("휴식을 마쳤습니다 — 탐험을 이어갑니다");
            }
            // someone 탈진: no sleeping it off on the dungeon floor — home
            if (Mercenaries.Exists(hero => hero.Alive && Exhausted(hero))) { BeginRetreat("탈진한 용병이 있습니다", recoverable: false); return true; }
            bool fit = PartyCanFight(), lost = Mercenaries.Exists(hero => !hero.Alive);
            // one comrade wounded too deep to fight (and nothing mends it): the company turns back together,
            // while the way out is still walkable — sooner the fewer they are (RetreatWounds)
            bool hurt = Mercenaries.Exists(hero => hero.Alive && hero.Wounds >= RetreatWounds);
            // alone and hurt: no patching up down here, straight home
            if (Living == 1 && hurt) { BeginRetreat("홀로 부상을 입었습니다"); return true; }
            bool supplies = Count(Pouch, SoulSupplies.CampKit) > 0 || HealPotions > 0;
            // worn before it gets worse: two wounds on someone, or someone 피곤 (fatigue tier 1) — a camp kit mends and
            // rests (a wound, 70 fatigue); without one the party goes on, and only someone 지침 (tier 2) makes it
            // sleep where it stands — until back under 지침 (RestedFatigue), not until 피곤 is gone
            if (fit && !hurt)
            {
                bool weary = Mercenaries.Exists(hero => hero.Alive && SoulFatigue.Tier(hero.Fatigue) >= WornFatigue);
                bool tired = Mercenaries.Exists(hero => hero.Alive && SoulFatigue.Tier(hero.Fatigue) >= RestFatigue);
                bool scarred = Mercenaries.Exists(hero => hero.Alive && hero.Wounds >= WornWounds);
                if ((weary || scarred) && SupplyBlock(SoulSupplies.CampKit) == null)
                {
                    Log(weary ? "지친 용병이 있습니다 — 야영으로 쉬어 갑니다" : "상처가 쌓였습니다 — 야영으로 돌봅니다");
                    UseSupply(SoulSupplies.CampKit);
                    return true;
                }
                if (tired)
                {
                    if (FloorSleeps >= MaxFloorSleeps) { BeginRetreat("여러 번 쉬었지만 피로가 쌓였습니다", recoverable: false); return true; }
                    FloorSleeps++;
                    Plan = SoulPartyPlan.Recover;
                    Log("지친 용병이 있습니다 — 그 자리에서 쉬어 갑니다");
                    return true;
                }
            }
            if (fit && !hurt && (!lost || supplies)) return false;
            if (!fit && PartyCanFight(rested: true) && FloorSleeps < MaxFloorSleeps)
            {
                FloorSleeps++;
                Plan = SoulPartyPlan.Recover;
                Log("지친 용병이 많아 싸우기 어렵습니다 — 그 자리에서 쉬어 갑니다");
                if (SupplyBlock(SoulSupplies.CampKit) == null) UseSupply(SoulSupplies.CampKit);
                return true;
            }
            if (!fit || hurt)
            {
                if (Count(Pouch, SoulSupplies.MendScroll) > 0)
                {
                    if (SupplyBlock(SoulSupplies.MendScroll) == null) UseSupply(SoulSupplies.MendScroll);
                    return true; // waiting out the scroll cooldown
                }
                if (SupplyBlock(SoulSupplies.CampKit) == null) { UseSupply(SoulSupplies.CampKit); return true; }
                if (DrinkForWounds()) return true;
            }
            BeginRetreat(lost ? "동료를 잃고 회복할 방법도 없습니다"
                : !fit ? "부상이 깊어 더 싸울 수 없습니다"
                : PotionCaution >= 2 ? "포션이 떨어졌고 부상이 쌓였습니다"
                : Living == 2 ? "둘뿐인데 부상이 깊습니다" : "부상이 깊은 동료가 있습니다");
            return true;
        }

        // ── picking fights ─────────────────────────────────────
        // A group heavier than the party takes on (OddsLimit) that has not noticed the party is left alone under
        // automatic control: not hunted, not engaged, and — once seen — explored around (DangerRadius cells kept
        // clear of it). Unwounded the party counts heads (a hall's horde: more than three a living hero). With a
        // wound anywhere it weighs them — an elite as 4, a guardian as 8 — against what it has left (Condition), so
        // a hurt party walks around a pack or a lone elite it would take on fresh.
        public const float DangerRadius = 7f;
        readonly HashSet<SoulMonster> avoided = new HashSet<SoulMonster>();
        int[] dangerStamp;
        int dangerMark;
        float avoidTimer;

        bool Avoided(SoulMonster monster) => avoided.Contains(monster) && !(monster.CurrentTarget is SoulMercenary);
        bool Dangerous(int x, int y) => dangerStamp != null && x >= 0 && y >= 0 && x < Map.Width && y < Map.Height && dangerStamp[y * Map.Width + x] == dangerMark;

        // Worn: a wound anywhere, or anyone tired (피곤 and worse) — the party weighs its fights.
        public bool Hurt => Mercenaries.Exists(hero => hero.Alive && (hero.Wounds > 0 || SoulFatigue.Tier(hero.Fatigue) >= 1));
        // What the party has left for a fight: the mean over the living of health × (1 − 20% a wound). 1 = fresh.
        public float Condition
        {
            get
            {
                float sum = 0; int count = 0;
                // health x (1 - 20% a wound) x (1 - 10% a fatigue tier)
                foreach (var hero in Mercenaries)
                    if (hero.Alive) { sum += Health(hero) * Mathf.Max(0, 1 - .2f * hero.Wounds) * (1 - .1f * SoulFatigue.Tier(hero.Fatigue)); count++; }
                return count > 0 ? sum / count : 0;
            }
        }
        public static float Weight(SoulMonster monster) => monster.Data.Guardian ? 8 : monster.Data.Elite ? 4 : 1;
        float Heft(SoulMonster monster) => Hurt ? Weight(monster) : 1;
        public float OddsLimit => HuntGroupPerHero * Mathf.Max(1, Mercenaries.FindAll(hero => hero.Alive).Count) * (Hurt ? Condition : 1);

        void TickAvoidance(float dt)
        {
            avoidTimer -= dt;
            if (avoidTimer > 0) return;
            avoidTimer = .5f;
            avoided.Clear();
            dangerMark++;
            if (!AutoControl) return;
            // a group's weight (a monster without a group is a group of its own)
            int Key(int index) => Monsters[index].Group != 0 ? Monsters[index].Group : -(index + 1);
            var size = new Dictionary<int, float>();
            var engaged = new HashSet<int>();
            for (int i = 0; i < Monsters.Count; i++)
            {
                var monster = Monsters[i];
                if (!monster.Alive) continue;
                int key = Key(i);
                size[key] = (size.TryGetValue(key, out float n) ? n : 0) + Heft(monster);
                if (monster.CurrentTarget is SoulMercenary) engaged.Add(key);
            }
            float limit = OddsLimit;
            var heavy = new HashSet<int>();
            foreach (var entry in size) if (entry.Value > limit && !engaged.Contains(entry.Key)) heavy.Add(entry.Key);
            if (heavy.Count == 0) return;
            for (int i = 0; i < Monsters.Count; i++) if (Monsters[i].Alive && heavy.Contains(Key(i))) avoided.Add(Monsters[i]);
            if (dangerStamp == null) dangerStamp = new int[Map.Width * Map.Height];
            int r = Mathf.CeilToInt(DangerRadius);
            foreach (var monster in avoided)
            {
                var cell = Map.Cell(monster.Position);
                if (!IsExplored(cell)) continue; // only what the party knows about
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = cell.x + dx, y = cell.y + dy;
                        if (dx * dx + dy * dy > DangerRadius * DangerRadius || x < 0 || y < 0 || x >= Map.Width || y >= Map.Height) continue;
                        dangerStamp[y * Map.Width + x] = dangerMark;
                    }
            }
        }

        // ── retreat ──────────────────────────────────────────────
        // Leaving takes a return scroll — or the way out on foot: the escape portal at the entrance, or a known
        // portal to the next floor (taken home instead of down). The party runs there under move orders, fights
        // or not: it only hits what is already in reach on the way, and wounds can pile up on the road.
        // Nobody is left behind: the ones who must get out (too wounded, out of breath, near death) run first, and the
        // fit ones keep the fight going until they are clear of it, then follow (Hold). Who covers is each one's
        // resolve plus a roll when the retreat is called; the steadiest fit mercenary always does. With nobody fit
        // (or nobody in need) everyone just runs.
        float retreatTimer;
        readonly HashSet<SoulMercenary> covering = new HashSet<SoulMercenary>();
        readonly Dictionary<SoulMercenary, float> coverRoll = new Dictionary<SoulMercenary, float>();
        public const float CoverLeash = 3.5f, ChaseRadius = 7f, QuietRadius = 10f;
        bool breather;
        bool Retreating => (AutoControl || EscapeOrdered) && Plan == SoulPartyPlan.Retreat;

        // The player's call (the HUD's 탈출): the party heads out as in a retreat — automatic control or not — until
        // called back (CancelRetreat).
        public bool EscapeOrdered { get; private set; }

        public void OrderEscape()
        {
            EscapeOrdered = true;
            BeginRetreat("탈출 명령");
        }

        public void CancelRetreat()
        {
            EscapeOrdered = false;
            StopRetreat("탈출을 멈추고 탐험을 이어갑니다");
        }

        void StopRetreat(string message)
        {
            if (Plan != SoulPartyPlan.Retreat || Recalled) return;
            Plan = SoulPartyPlan.Explore;
            covering.Clear(); pacing.Clear(); retreatSteps.Clear();
            standing = false;
            if (breather) breather = Resting = false;
            foreach (var hero in Mercenaries)
                if (hero.Alive) { ordered.Remove(hero); SetDestination(hero, hero.Position, out Vector2 _, playerOrder: false); }
            Log(message);
        }

        // A retreat called only because the fight was being lost (oddsLost) is taken up again, under automatic
        // control, once the monsters on the party weigh ResumeOdds × the losing line or less, nobody is near its
        // crisis and no wound sends the party home anyway: the fight is winnable now, running only drags it out.
        public const float ResumeOdds = .7f;
        bool oddsRetreat;

        public void BeginRetreat(string why, bool oddsLost = false, bool recoverable = true)
        {
            if (Plan == SoulPartyPlan.Retreat || Plan == SoulPartyPlan.Escape || Recalled) return;
            CancelCampCall(); // going home comes before a camp
            Plan = SoulPartyPlan.Retreat;
            oddsRetreat = oddsLost;
            recoverableRetreat = recoverable;
            standing = false;
            retreatSteps.Clear();
            tollTimer = 0;
            coverRoll.Clear();
            foreach (var hero in Mercenaries) coverRoll[hero] = (float)random.NextDouble() * .3f;
            Log(why + " — 출구로 향합니다");
        }

        bool CanCover(SoulMercenary hero)
            => CanFight(hero) && hero.DownTime <= 0 && Health(hero) > CrisisFor(hero) + .15f && Breath(hero) > BreathBelow;

        void TickRetreat(float dt)
        {
            if (!(AutoControl || EscapeOrdered) || Plan != SoulPartyPlan.Retreat || Recalled || RecallLeft > 0 || GoingToCamp)
            {
                covering.Clear();
                pacing.Clear();
                if (breather) breather = Resting = false;
                return;
            }
            retreatTimer -= dt;
            if (retreatTimer > 0) return;
            retreatTimer = .5f;
            var leader = Leader;
            if (leader == null) return;
            // mended on the way (a heal, a breather, a potion): fit to go on again — the retreat is called off
            if (recoverableRetreat && AutoControl && !EscapeOrdered && !UnderAttack() && GoodToGoOn())
            {
                StopRetreat("회복했습니다 — 다시 탐험을 이어갑니다");
                return;
            }
            if (oddsRetreat && AutoControl && !EscapeOrdered)
            {
                float weight = EngagedWeight();
                bool steady = !Mercenaries.Exists(hero => hero.Alive && (Health(hero) <= CrisisFor(hero) + .1f || hero.Wounds >= RetreatWounds));
                if (steady && weight > 0 && weight <= LosingLimit * ResumeOdds) { StopRetreat("적이 줄었습니다 — 돌아서서 싸웁니다"); return; }
            }
            if (SupplyBlock(SoulSupplies.ReturnScroll) == null)
            {
                Plan = SoulPartyPlan.Escape;
                Log("귀환의 두루마리를 읽습니다");
                UseSupply(SoulSupplies.ReturnScroll);
                return;
            }
            // the nearest way out: the entrance's escape portal or a portal to the next floor the party knows
            SoulInteractable portal = null;
            foreach (var thing in Interactables)
                if (thing.Kind == SoulObjectKind.Escape && (portal == null || Vector2.Distance(leader.Position, thing.Position) < Vector2.Distance(leader.Position, portal.Position))) portal = thing;
            Vector2 goal = portal != null ? portal.Position : Map.Center(PartyStart);
            bool toExit = false;
            float best = Vector2.Distance(leader.Position, goal);
            // a portal down is a way out only on the way to the target floor: going home never goes deeper than that
            if (ExitOpen && Floor < TargetFloor)
                foreach (var cell in exits)
                {
                    if (!IsExitKnown(cell) || !SafeExit(cell)) continue;
                    float distance = Vector2.Distance(leader.Position, Map.Center(cell));
                    if (distance < best) { best = distance; goal = Map.Center(cell); toExit = true; }
                }
            if (TryShortcut(leader, goal)) return;
            // Thrown somewhere (Warped) with someone near death or no safe way back: the party stays where it is and
            // lets the dungeon's time run out (it is thrown out then) — fighting only what comes to it.
            bool hide = Warped && AutoControl && !EscapeOrdered
                && (Mercenaries.Exists(hero => hero.Alive && Health(hero) <= CrisisHealth) || !SafeWayHome(leader, goal));
            if (hide)
            {
                if (!Hiding) { Hiding = true; Log("돌아갈 안전한 길이 없습니다 — 그 자리에서 버티며 던전 시간이 끝나기를 기다립니다"); }
                covering.Clear(); pacing.Clear();
                foreach (var hero in Mercenaries)
                    if (hero.Alive && HasMoveOrder(hero)) { ordered.Remove(hero); SetDestination(hero, hero.Position, out Vector2 _, playerOrder: false); }
                return;
            }
            if (Hiding) { Hiding = false; Log("길이 트였습니다 — 다시 출구로 향합니다"); }
            if (!toExit && portal != null && EscapeBlock(portal) == null)
            {
                var first = Mercenaries.Find(hero => hero.Alive && Vector2.Distance(hero.Position, portal.Position) <= 1.5f);
                if (first != null) { Use(portal, first); return; } // at the entrance, everyone together: out
            }
            // Stalled on the last stretch: whoever has not come closer for StallRepath seconds aims at a free spot beside
            // the way out instead (its first aim may be a spot its body cannot reach); and when someone stands at the
            // portal, nothing is on the party and the rest have been stuck within GatherReach for StallEscape seconds,
            // they go out together from where they are.
            TrackStall(goal);
            if (!toExit && portal != null && !UnderAttack() && stallTime >= StallEscape
                && Mercenaries.Exists(hero => hero.Alive && Vector2.Distance(hero.Position, portal.Position) <= 1.5f))
            {
                gatherGrace = true;
                var first = Mercenaries.Find(hero => hero.Alive && Vector2.Distance(hero.Position, portal.Position) <= 1.5f);
                bool out_ = EscapeBlock(portal) == null;
                if (out_) { Log("길이 막혀 모이지 못했습니다 — 가까이 있는 동료와 함께 탈출합니다"); Use(portal, first); }
                gatherGrace = false;
                if (out_) return;
            }
            // Out of breath with nothing near (no monster within QuietRadius of anyone): everyone stops together and
            // catches their breath (resting regeneration) until all have BreathUntil × .75 back — a half-speed
            // crawl home only runs into the next fight.
            bool quiet = true;
            foreach (var monster in nearby)
                foreach (var hero in Mercenaries) quiet &= !(monster.Alive && hero.Alive && Vector2.Distance(monster.Position, hero.Position) < QuietRadius);
            if (breather) breatherTime += .5f;
            if (breather && (!quiet || EscapeOrdered || breatherTime >= BreatherMax || Recovered()))
            {
                breather = Resting = false;
                breatherAgain = clock + BreatherCooldown; // what did not come back in BreatherMax will not by waiting on
            }
            else if (!breather && clock >= breatherAgain && quiet && !EscapeOrdered && NeedsBreather())
            {
                breather = Resting = true;
                breatherTime = 0;
                Log(Mending() && Mercenaries.Exists(hero => hero.Alive && hero.Wounds > 0) ? "안전한 곳입니다 — 숨을 고르고 상처를 돌봅니다" : "안전한 곳입니다 — 숨을 고릅니다");
            }
            if (breather)
            {
                foreach (var hero in Mercenaries)
                    if (hero.Alive && HasMoveOrder(hero)) { ordered.Remove(hero); SetDestination(hero, hero.Position, out Vector2 _, playerOrder: false); }
                // the healer goes over to whoever still has a wound to mend (its heal reaches only so far)
                foreach (var healer in Mercenaries)
                {
                    var heal = healer.Alive ? WoundHeal(healer) : null;
                    if (heal == null) continue;
                    float reach = SoulSkillUsePolicy.Range(healer, heal) - .5f;
                    SoulMercenary patient = null;
                    foreach (var hero in Mercenaries)
                        if (hero.Alive && hero.Wounds > 0 && (patient == null || Vector2.Distance(healer.Position, hero.Position) < Vector2.Distance(healer.Position, patient.Position))) patient = hero;
                    if (patient != null && Vector2.Distance(healer.Position, patient.Position) > reach)
                        SetDestination(healer, patient.Position + (healer.Position - patient.Position).normalized * 1.2f, out Vector2 _, playerOrder: false);
                }
                return;
            }
            // Caught on the way: something after the party is on one of it (within CaughtReach) and the fight is not
            // a losing one — walking on only takes the blows in the back, so everyone turns and fights it together
            // until nothing is on anyone, then the walk goes on.
            var caught = EscapeOrdered ? null : Caught(); // the player's 탈출 is not argued with: everyone walks on
            if (caught != null && EngagedWeight() <= LosingLimit)
            {
                if (!standing) { standing = true; Log(caught.Name + ": 추격에 따라잡혔습니다 — 모두 돌아서서 싸웁니다"); }
                Stand(caught);
                return;
            }
            if (standing) { standing = false; Log("추격을 떨쳐냈습니다 — 다시 출구로 향합니다"); }
            // who runs, who covers
            covering.Clear();
            SoulMercenary steadiest = null;
            foreach (var hero in Mercenaries) if (hero.Alive && CanCover(hero) && (steadiest == null || hero.Resolve > steadiest.Resolve)) steadiest = hero;
            var runners = new List<SoulMercenary>();
            foreach (var hero in Mercenaries)
            {
                if (!hero.Alive) continue;
                coverRoll.TryGetValue(hero, out float roll);
                if (hero == steadiest || CanCover(hero) && hero.Resolve + roll >= .5f) covering.Add(hero);
                else runners.Add(hero);
            }
            if (runners.Count == 0 || EscapeOrdered) { runners.AddRange(covering); covering.Clear(); }
            // The ones in need get out of the fight; the others hold it until nothing is on a runner's heels any more
            // (no monster within ChaseRadius in sight of one), then everyone heads out together — and turn to hold
            // it again whenever something catches up with a runner on the way.
            var chased = new Dictionary<SoulMercenary, SoulMonster>();
            if (covering.Count > 0)
                foreach (var hero in runners)
                {
                    float near = ChaseRadius;
                    foreach (var monster in nearby)
                    {
                        // only one that is after the party: a monster that has not noticed it (or stands off) is no chase
                        if (!monster.Alive || !(monster.CurrentTarget is SoulMercenary prey && Mercenaries.Contains(prey))) continue;
                        float d = Vector2.Distance(monster.Position, hero.Position);
                        if (d < near && Map.Sight(monster.Position, hero.Position)) { near = d; chased[hero] = monster; }
                    }
                }
            bool replan = (tollTimer -= .5f) <= 0 || retreatGoal != goal;
            if (replan) { tollTimer = RetreatReplan; retreatGoal = goal; RetreatToll(); }
            // together: everyone on the way out walks at the slowest one's pace (a tired or breathless one included),
            // and whoever gets GroupGap tiles ahead of the last waits for it
            pacing.Clear();
            paceSpeed = float.MaxValue; rearDistance = 0; paceGoal = goal;
            foreach (var hero in Mercenaries)
            {
                if (!hero.Alive || covering.Contains(hero) && chased.Count > 0) continue;
                pacing.Add(hero);
                paceSpeed = Mathf.Min(paceSpeed, WalkSpeed(hero));
                rearDistance = Mathf.Max(rearDistance, Vector2.Distance(hero.Position, goal));
            }
            foreach (var hero in Mercenaries)
            {
                if (!hero.Alive) continue;
                if (covering.Contains(hero) && chased.Count > 0) { Hold(hero, chased); continue; }
                if (Vector2.Distance(hero.Position, goal) <= EscapeRadius * .5f) continue;
                var step = RetreatStep(hero, goal, replan);
                if (stalled.TryGetValue(hero, out float still) && still >= StallRepath) step = Beside(goal, Mercenaries.IndexOf(hero), hero.Stats.Radius);
                bool heading = HasMoveOrder(hero) && Vector2.Distance(DestinationPoint(hero), step) < 1.5f;
                if (heading) continue;
                if (toExit && step == goal) SetDestination(hero, Map.Cell(goal), out Vector2Int _); // standing on the portal ends the floor
                else SetDestination(hero, step, out Vector2 _);
            }
        }

        // ── the way home ─────────────────────────────────────────
        // A retreating party picks its way around what it knows is out there: a tile route over the map where every
        // tile near a known monster (seen, not already on the party) costs more — HordeToll within DangerRadius of a
        // group it would not take on (Avoided), MonsterToll within TollRadius of any other — and unexplored tiles
        // UnknownToll. Each mercenary walks it RetreatStride tiles at a time; the route is planned again every
        // RetreatReplan seconds (monsters move) and whenever a stretch is walked.
        public const float HordeToll = 8f, MonsterToll = 3f, UnknownToll = 2f, TollRadius = 4f, RetreatReplan = 2f;
        public const int RetreatStride = 10;
        float[] toll;
        float tollTimer;
        Vector2 retreatGoal = new Vector2(-1, -1);
        readonly Dictionary<SoulMercenary, Vector2> retreatSteps = new Dictionary<SoulMercenary, Vector2>();
        readonly List<Vector2Int> retreatRoute = new List<Vector2Int>();

        void RetreatToll()
        {
            int width = Map.Width, height = Map.Height;
            if (toll == null) toll = new float[width * height];
            for (int i = 0; i < toll.Length; i++) toll[i] = Explored[i] ? 0 : UnknownToll;
            foreach (var monster in Monsters)
            {
                if (!monster.Alive || monster.CurrentTarget is SoulMercenary target && Mercenaries.Contains(target)) continue;
                var cell = Map.Cell(monster.Position);
                if (!IsExplored(cell)) continue; // only what the party knows about
                bool horde = Avoided(monster);
                float radius = horde ? DangerRadius : TollRadius, add = horde ? HordeToll : MonsterToll;
                int r = Mathf.CeilToInt(radius);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = cell.x + dx, y = cell.y + dy;
                        if (dx * dx + dy * dy > radius * radius || x < 0 || y < 0 || x >= width || y >= height) continue;
                        int index = y * width + x;
                        toll[index] = Mathf.Max(toll[index], add + (Explored[index] ? 0 : UnknownToll));
                    }
            }
        }

        // The next stretch of this mercenary's way home: RetreatStride tiles along the route, or the goal itself.
        Vector2 RetreatStep(SoulMercenary hero, Vector2 goal, bool replan)
        {
            if (!replan && retreatSteps.TryGetValue(hero, out var kept) && Vector2.Distance(hero.Position, kept) > 2f) return kept;
            if (toll == null) RetreatToll();
            var step = goal;
            if (Map.TileRoute(Map.Cell(hero.Position), Map.Cell(goal), toll, retreatRoute) && retreatRoute.Count > RetreatStride + 1)
                step = Map.Center(retreatRoute[RetreatStride]);
            retreatSteps[hero] = step;
            return step;
        }

        readonly HashSet<SoulMercenary> pacing = new HashSet<SoulMercenary>();
        float paceSpeed = float.MaxValue, rearDistance;
        Vector2 paceGoal;
        public const float GroupGap = 3f;

        // ── a retreat that stops short ──
        public const float StallRepath = 3f, StallEscape = 8f, GatherReach = 10f;
        readonly Dictionary<SoulMercenary, float> stalled = new Dictionary<SoulMercenary, float>();
        readonly Dictionary<SoulMercenary, float> closest = new Dictionary<SoulMercenary, float>();
        float stallTime;
        bool gatherGrace;

        // Called every retreat step (0.5 s): how long each one still on its way has come no closer to the goal, and
        // how long the whole party (those not yet there, all within GatherReach) has been that way.
        void TrackStall(Vector2 goal)
        {
            bool anyMoving = false, anyWaiting = false, allNear = true;
            foreach (var hero in Mercenaries)
            {
                if (!hero.Alive) continue;
                float distance = Vector2.Distance(hero.Position, goal);
                if (distance > GatherReach) allNear = false;
                if (distance <= EscapeRadius) { stalled.Remove(hero); closest.Remove(hero); continue; }
                anyWaiting = true;
                if (!closest.TryGetValue(hero, out float best) || distance < best - .3f) { closest[hero] = distance; stalled[hero] = 0; anyMoving = true; }
                else stalled[hero] = (stalled.TryGetValue(hero, out float still) ? still : 0) + .5f;
            }
            stallTime = anyWaiting && allNear && !anyMoving ? stallTime + .5f : 0;
        }

        // Walking speed now: fatigue is in MoveSpeed; out of stamina it is the half-speed walk.
        float WalkSpeed(SoulMercenary hero)
            => SoulCombat.MoveSpeed(hero) * (tired.Contains(hero) ? .5f + Mathf.Clamp(hero.Stats.Total(StatType.TirelessWalk), 0, .5f) : 1f);

        // The share of its own speed a retreating mercenary walks at: the slowest one's pace; 0 while it waits for the last.
        float Pace(SoulMercenary hero)
        {
            if (!Retreating || !pacing.Contains(hero)) return 1f;
            if (Vector2.Distance(hero.Position, paceGoal) < rearDistance - GroupGap) return 0f;
            return Mathf.Clamp01(paceSpeed / Mathf.Max(.1f, WalkSpeed(hero)));
        }

        // ── the ways out, judged ──
        public bool Hiding { get; private set; }
        readonly List<Vector2Int> judgedRoute = new List<Vector2Int>();

        // The next floor's portal is a way out only while someone has it in sight (within its vision, in a line) and
        // the whole way there is known and clear: explored tiles, no monster the party knows of near them.
        bool SafeExit(Vector2Int cell)
        {
            var goal = Map.Center(cell);
            float vision = VisionRadius * Theme.Vision;
            bool sighted = false;
            foreach (var hero in Mercenaries)
                sighted |= hero.Alive && Vector2.Distance(hero.Position, goal) <= vision && LineOfSight(Map.Cell(hero.Position), cell);
            var leader = Leader;
            if (!sighted || leader == null) return false;
            RetreatToll();
            if (!Map.TileRoute(Map.Cell(leader.Position), cell, toll, judgedRoute)) return false;
            foreach (var step in judgedRoute) if (toll[step.y * Map.Width + step.x] > 0) return false;
            return true;
        }

        // A way home a thrown party can walk: its route passes no crowd it would not take on (HordeToll).
        bool SafeWayHome(SoulMercenary leader, Vector2 goal)
        {
            if (toll == null) RetreatToll();
            if (!Map.TileRoute(Map.Cell(leader.Position), Map.Cell(goal), toll, judgedRoute)) return false;
            foreach (var step in judgedRoute) if (toll[step.y * Map.Width + step.x] >= HordeToll) return false;
            return true;
        }

        // ── a breather on the way out ──
        // Clear of pursuers (no monster within QuietRadius): the party stops and recovers when someone is out of
        // breath, below RestBelow HP, or wounded while someone can mend wounds (a heal that closes them, the MP
        // for it) — until breath (BreathUntil × .75) and health (RestUntil) are back and no wound is left to mend,
        // BreatherMax seconds at most.
        public const float BreatherMax = 45f, BreatherCooldown = 40f, ResumeHealth = .7f;
        float breatherTime, breatherAgain;
        bool recoverableRetreat;

        // Fit to explore again: what would not have sent the party home (FollowPlan) — able to fight, nobody at its
        // wound limit, a fallen comrade only with supplies left — and everyone at ResumeHealth or more, not 지침.
        bool GoodToGoOn()
        {
            if (!PartyCanFight() || Mercenaries.Exists(hero => hero.Alive && hero.Wounds >= RetreatWounds)) return false;
            bool supplies = Count(Pouch, SoulSupplies.CampKit) > 0 || HealPotions > 0;
            if (Mercenaries.Exists(hero => !hero.Alive) && !supplies) return false;
            return Mercenaries.TrueForAll(hero => !hero.Alive
                || Health(hero) >= ResumeHealth && Health(hero) > CrisisFor(hero) + .15f && SoulFatigue.Tier(hero.Fatigue) < RestFatigue);
        }

        // A heal of this one's that closes wounds and that it has the MP for (null: none).
        static SoulActiveSkillData WoundHeal(SoulMercenary hero)
        {
            foreach (var skill in hero.ActiveSkills())
            {
                if (skill == null || skill.Trigger != SoulTrigger.AllyHurt || !skill.HealsWounds) continue;
                SoulSkillUsePolicy.Costs(hero, skill, out _, out float mana);
                if (hero.Mp >= mana) return skill;
            }
            return null;
        }

        bool Mending() => Mercenaries.Exists(hero => hero.Alive && WoundHeal(hero) != null);

        bool NeedsBreather()
        {
            bool mend = Mending();
            return Mercenaries.Exists(hero => hero.Alive && (Breath(hero) < BreathBelow || hero.Regenerates && Health(hero) < RestBelow || mend && hero.Wounds > 0));
        }

        bool Recovered()
        {
            bool mend = Mending();
            return Mercenaries.TrueForAll(hero => !hero.Alive
                || Breath(hero) >= BreathUntil * .75f && (Health(hero) >= ResumeHealth + .05f || !hero.Regenerates) && !(mend && hero.Wounds > 0));
        }

        // ── caught on the way ──
        public const float CaughtReach = 2.5f, StandLeash = 4f;
        bool standing;
        public bool Standing => standing;

        // The monsters after the party, by weight (as in Crisis), and the line past which a fight is being lost.
        float EngagedWeight()
        {
            float weight = 0;
            foreach (var monster in nearby)
                if (monster.Alive && monster.CurrentTarget is SoulMercenary target && Mercenaries.Contains(target)) weight += Weight(monster);
            return weight;
        }
        float LosingLimit => OddsLimit * (Living <= 2 ? PairLosingOdds : LosingOdds) * (1 - OddsPerCaution * PotionCaution);

        // The one a pursuer has caught up with: a monster after the party within CaughtReach of it, in sight.
        SoulMercenary Caught()
        {
            SoulMercenary caught = null;
            float best = CaughtReach;
            foreach (var monster in nearby)
            {
                if (!monster.Alive || !(monster.CurrentTarget is SoulMercenary prey && Mercenaries.Contains(prey))) continue;
                foreach (var hero in Mercenaries)
                {
                    if (!hero.Alive) continue;
                    float d = Vector2.Distance(monster.Position, hero.Position);
                    if (d <= best && Map.Sight(monster.Position, hero.Position)) { best = d; caught = hero; }
                }
            }
            return caught;
        }

        // Everyone turns: those near the fight (StandLeash, further for those who shoot) drop their walk and fight
        // whoever comes; the rest come back to the one caught.
        void Stand(SoulMercenary caught)
        {
            covering.Clear(); pacing.Clear();
            foreach (var hero in Mercenaries)
            {
                if (!hero.Alive) continue;
                float leash = hero.Role == SoulRole.Ranged || hero.Role == SoulRole.Support ? StandLeash * 1.7f : StandLeash;
                if (hero == caught || Vector2.Distance(hero.Position, caught.Position) <= leash)
                {
                    if (HasMoveOrder(hero)) { ordered.Remove(hero); SetDestination(hero, hero.Position, out Vector2 _, playerOrder: false); }
                    continue;
                }
                if (!HasMoveOrder(hero) || Vector2.Distance(DestinationPoint(hero), caught.Position) > 1.5f) SetDestination(hero, caught.Position, out Vector2 _);
            }
        }

        // Holding the fight for a runner: close to the fight (a chaser or the runner within the leash) no order at
        // all, so it fights whoever comes (Act skips the leader tether while retreating); further off it moves in
        // between the nearest chased runner and its chaser.
        void Hold(SoulMercenary hero, Dictionary<SoulMercenary, SoulMonster> chased)
        {
            SoulMercenary runner = null;
            foreach (var entry in chased)
                if (runner == null || Vector2.Distance(hero.Position, entry.Key.Position) < Vector2.Distance(hero.Position, runner.Position)) runner = entry.Key;
            var chaser = chased[runner];
            float leash = hero.Role == SoulRole.Ranged || hero.Role == SoulRole.Support ? CoverLeash * 1.7f : CoverLeash; // shooting from behind
            bool inFight = Vector2.Distance(hero.Position, runner.Position) <= leash;
            foreach (var entry in chased) inFight |= Vector2.Distance(hero.Position, entry.Value.Position) <= leash;
            if (inFight)
            {
                if (HasMoveOrder(hero)) { ordered.Remove(hero); SetDestination(hero, hero.Position, out Vector2 _, playerOrder: false); }
                return;
            }
            var toward = chaser.Position - runner.Position;
            var spot = runner.Position + (toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector2.zero) * 1.5f;
            var aim = HasMoveOrder(hero) ? DestinationPoint(hero) : hero.Position;
            if (Vector2.Distance(aim, spot) > 1f) SetDestination(hero, spot, out Vector2 _);
        }

        // A healing potion for the most wounded (potions mend wounds).
        bool DrinkForWounds()
        {
            SoulMercenary worst = null;
            foreach (var hero in Mercenaries) if (hero.Alive && hero.Wounds > 0 && (worst == null || hero.Wounds > worst.Wounds)) worst = hero;
            if (worst == null) return false;
            string id = Count(Belt, "potion_greater") > 0 ? "potion_greater" : Count(Belt, SoulSupplies.HealPotion) > 0 ? SoulSupplies.HealPotion : null;
            if (id == null) return false;
            Belt[id]--;
            SoulSupplies.Drink(id, worst, Perks.PotionPower, Perks.AntidoteWard);
            Log($"{worst.Name}: {SoulSupplies.Get(id).Name} — 부상 치료 (남은 {Belt[id]}개)");
            return true;
        }

        // A fight about to cost a life: someone at CrisisHealth or less (a potion was already tried at 30%), someone
        // on the ground at DownCrisisHealth or less, or someone already fallen — the party gets out and goes home
        // (the living, with what they carry). Only under automatic control; with manual control the player decides.
        public float CrisisHealth => Perks.CrisisHealth;       // 25% (35% at most with the guild's 신중한 후퇴)
        public float DownCrisisHealth => Perks.CrisisHealth + .1f;
        // Each mercenary calls it a little differently: up to 6 points earlier with little resolve, later with much.
        // On top: CrisisMargin, and CrisisPerWound for every wound it carries (a wounded body gives out sooner).
        public const float CrisisSpread = .12f, CrisisMargin = .05f, CrisisPerWound = .03f;
        float CrisisFor(SoulMercenary hero) => CrisisHealth + CrisisMargin + CrisisPerWound * hero.Wounds + CrisisPerFatigue * SoulFatigue.Tier(hero.Fatigue)
            + (.5f - hero.Resolve) * CrisisSpread + (Living == 1 ? SoloCrisisMargin : Living == 2 ? PairCrisisMargin : 0) + CrisisPerCaution * PotionCaution;
        public const float CrisisPerFatigue = .03f; // a tired body gives out sooner too

        // Why the party must get out of this fight, or null. A comrade fallen or about to; or, with wounds, the
        // monsters on it weigh more than LosingOdds × what it takes on (OddsLimit) — a fight it is losing.
        public const float LosingOdds = 1.2f;
        public const int WornFatigue = 1, WornWounds = 2; // 피곤 (fatigue tier 1): camp at once (with a kit)
        public const int RestFatigue = 2;                  // 지침: without a kit, sleep where it stands

        bool crisisOdds; // the last Crisis() was the losing-fight one

        string Crisis()
        {
            crisisOdds = false;
            if (!partyFighting || Mercenaries.TrueForAll(hero => !hero.Alive)) return null;
            if (Mercenaries.Exists(hero => !hero.Alive)) return "위기 — 동료가 쓰러졌습니다";
            if (Mercenaries.Exists(hero => hero.Alive && (Health(hero) <= CrisisFor(hero) || hero.DownTime > 0 && Health(hero) <= CrisisFor(hero) + .1f)))
                return "위기 — 더 싸우면 죽습니다";
            // a small party does not fight on with its wounds
            if (Living == 1 && Mercenaries.Exists(hero => hero.Alive && hero.Wounds >= 1)) return "홀로 부상을 입었습니다 — 물러납니다";
            if (Living == 2 && Mercenaries.Exists(hero => hero.Alive && hero.Wounds >= RetreatWounds))
                return PotionCaution >= 2 ? "둘뿐인데 포션도 없이 다쳤습니다 — 물러납니다" : "둘뿐인데 부상이 깊습니다 — 물러납니다";
            if (!Hurt || EngagedWeight() <= LosingLimit) return null;
            crisisOdds = true;
            return PotionCaution >= 2 ? "포션도 없이 불리한 싸움입니다" : "부상을 안고 불리한 싸움입니다";
        }

        // The party's mood: the mean resolve of the living. A steadier party rests a little later (RestBelow).
        float PartyResolve
        {
            get
            {
                float sum = 0; int count = 0;
                foreach (var hero in Mercenaries) if (hero.Alive) { sum += hero.Resolve; count++; }
                return count > 0 ? sum / count : .5f;
            }
        }
        float RestBelowNow => RestBelow + (.5f - PartyResolve) * .2f;

        void TickPlan(float dt)
        {
            if (!Sleeping) return;
            foreach (var hero in Mercenaries) if (hero.Alive) SoulFatigue.Add(hero, -SleepRelief * dt);
        }

        // Out of the dungeon with what the party carries (time is up, or it chose to leave).
        public void LeaveDungeon(string message)
        {
            if (Defeated || Recalled) return;
            RecallLeft = 0;
            Recalled = true;
            foreach (var drop in Stash) if (drop.Preserved) Vault.Add(drop.Soul);
            Stash.Clear();
            Log(message);
        }

        // Soul stones packed in the pouch become the party's stones (each keeps one unabsorbed soul).
        public void StowStones()
        {
            int stones = Count(Pouch, SoulSupplies.SoulStone);
            Pouch.Remove(SoulSupplies.SoulStone);
            PreservationItems += stones;
        }

        // ── spoils: shared out by the party itself ───────────────

        // How much a mercenary needs a soul: the soul's stats it grows in (value x its race and job tendency for that
        // stat — a stat outside its tendencies counts nothing), +2 for each pattern, skill or passive it does not have
        // yet, less what goes against its place (a melee attack from the back -4, -8 without a weapon: a mage's staff;
        // a magic attack at the front -2; rushing or flanking from the back, keeping away at the front -6). A second
        // copy of a soul it already has brings nothing new. A pattern its race or weapon cannot use: -inf.
        // At SoulNeed or above it is worth a slot; a soul that is worth one to nobody is let go.
        public const float SoulNeed = 4f; // at least this: a new pattern (+2) or passive (+3) alone is not enough, the stats have to suit it too
        public const float SuitedAbility = 4f; // a skill that suits the mercenary is reason enough for the soul
        public const float NewPassive = 3f; // a passive that suits it helps, but alone it does not make the soul worth a slot
        public const float SideBenefit = 1f; // gold, loot, souls, the map: nice to have, no help in a fight
        public const float StatShareWorth = 3f, NotItsSoul = 20f;

        // A skill suits: a spell someone with a leaning to magic, a close-in blow someone at the front, one of its job, one
        // whose pattern it has (or the soul brings), and a blow whose power comes from a stat it grows (a strength trap
        // is little use to a summoner); buffs, fields, ranged and reactive skills suit anyone otherwise.
        static bool Suits(SoulMercenary hero, SoulData soul, SoulActiveSkillData skill, bool back, bool magical, System.Func<StatType, float> growth)
        {
            if (SoulSkillUsePolicy.Blocked(hero, skill) != null) return false;
            if (skill.RequiredPattern != null && !hero.AllPatterns().Contains(skill.RequiredPattern) && System.Array.IndexOf(soul.Patterns, skill.RequiredPattern) < 0) return false;
            if (SoulSkillUsePolicy.Magical(skill) && !magical) return false;
            bool blow = skill.Damage != null && (skill.Damage.Flat > 0 || (skill.Damage.Terms != null && skill.Damage.Terms.Length > 0));
            bool close = !skill.AtTarget && !skill.Line && skill.Field == SoulFieldKind.None && SoulSkillUsePolicy.Range(hero, skill) <= 3f;
            if (back && blow && close) return false;
            if (blow && !Grows(skill.Damage, growth) && growth(KindPower(skill.DamageKind)) <= 0) return false;
            return true;
        }

        // The upper stat that powers a kind of blow (절삭력 a slash, 타격력 an impact, 관통력 a thrust).
        static StatType KindPower(SoulDamageKind kind)
            => kind == SoulDamageKind.Slash ? StatType.SlashPower : kind == SoulDamageKind.Impact ? StatType.ImpactPower
             : kind == SoulDamageKind.Pierce ? StatType.PiercePower : StatType.Magic;

        // Its power comes from a stat the mercenary grows (or from nothing in particular).
        static bool Grows(SoulValue value, System.Func<StatType, float> growth)
        {
            if (value?.Terms == null) return true;
            bool scaled = false;
            foreach (var term in value.Terms)
            {
                if (!HeroStatPipeline.IsUpper(term.Stat)) continue;
                scaled = true;
                if (growth(term.Stat) > 0) return true;
            }
            return !scaled;
        }

        // What a new passive is worth to it: one that helps it fight NewPassive, one that only brings gold, loot, souls or
        // a better map SideBenefit, one it has no use for (mana to one who casts nothing, a stat it does not grow) nothing.
        static float PassiveWorth(SoulPassiveSkillData passive, bool magical, System.Func<StatType, float> growth)
        {
            bool fights = SoulDescribe.Has(passive.Damage) || SoulDescribe.Has(passive.Heal) || passive.Stamina > 0;
            bool side = passive.MapTraits != SoulMapTrait.None;
            foreach (var bonus in passive.AlwaysBonuses)
            {
                if (bonus.Value <= 0 || bonus.Stat == StatType.StaffBound || bonus.Stat == StatType.CursedHp) continue;
                if (SideStat(bonus.Stat)) side = true;
                else if (ManaStat(bonus.Stat)) fights |= magical;
                else if (HeroStatPipeline.IsUpper(bonus.Stat)) fights |= growth(bonus.Stat) > 0;
                else fights = true;
            }
            return fights ? NewPassive : side ? SideBenefit : 0;
        }

        static bool SideStat(StatType stat)
            => stat == StatType.GoldFind || stat == StatType.LootFind || stat == StatType.SoulFind || stat == StatType.BookDiscount;

        static bool ManaStat(StatType stat)
            => stat == StatType.MaxMp || stat == StatType.MpRegen || stat == StatType.ManaRegenRate || stat == StatType.KillMana || stat == StatType.SpellPower
            || stat == StatType.ManaShield || stat == StatType.FireFocus || stat == StatType.BattleCaster || stat == StatType.Magic;

        public static float SoulFit(SoulMercenary hero, SoulData soul)
        {
            float Growth(StatType stat)
            {
                float weight = 0;
                foreach (var g in hero.Race.GrowthWeights) if (g.Stat == stat) weight += g.Value;
                foreach (var g in hero.GrowthWeights) if (g.Stat == stat) weight += g.Value;
                return weight;
            }
            bool back = hero.Role == SoulRole.Ranged || hero.Role == SoulRole.Support;
            bool armed = hero.Equipment.Exists(item => item.BasicAttack != null);
            bool again = hero.Souls.Contains(soul);
            float fit = 0;
            // the stats, as a share (a higher grade's bigger numbers do not make a stray match count more): the
            // tendency-weighted part of the soul's stats, StatShareWorth for a soul that is all its tendencies
            float total = 0, weighted = 0, top = 0;
            foreach (var bonus in soul.CharacteristicStats) { total += Mathf.Abs(bonus.Value); weighted += bonus.Value * Growth(bonus.Stat); top = Mathf.Max(top, bonus.Value); }
            if (total > 0) fit += weighted / total * StatShareWorth;
            // …and a soul is its stats first: its main one (the largest) must be one this mercenary's job grows (its
            // race's leanings count in the share above, not here — an elf summoner is still a summoner), or no
            // passive, skill or pattern it brings makes it this one's soul
            bool JobGrows(StatType stat) => System.Array.Exists(hero.GrowthWeights, g => g.Stat == stat && g.Value > 0);
            if (total > 0 && !System.Array.Exists(soul.CharacteristicStats, b => b.Value >= top - .001f && JobGrows(b.Stat))) fit -= NotItsSoul;
            if (!again)
            {
                // a new skill that suits it is worth the soul on its own, whatever the stats (SuitedAbility is
                // SoulNeed); a passive adds less, and only if it is of use to it; a skill that does not suit counts against it
                foreach (var skill in soul.ActiveSkills)
                {
                    if (skill == null || hero.AllActiveSkills().Contains(skill)) continue;
                    fit += Suits(hero, soul, skill, back, Growth(StatType.Magic) > 0, Growth) ? SuitedAbility : -1;
                }
                foreach (var passive in soul.Passives)
                    if (passive != null && !hero.Passives().Contains(passive)) fit += PassiveWorth(passive, Growth(StatType.Magic) > 0, Growth);
            }
            foreach (var pattern in soul.Patterns)
            {
                if (pattern == null) continue;
                if (!pattern.Compatible(hero)) return float.NegativeInfinity;
                if (!again && !hero.AllPatterns().Contains(pattern)) fit += 2;
                if (pattern.Category == SoulPatternCategory.Movement)
                {
                    if (back && (pattern.MoveStyle == SoulMoveStyle.Rush || pattern.MoveStyle == SoulMoveStyle.Flank)) fit -= 6;
                    if (!back && pattern.MoveStyle == SoulMoveStyle.KeepDistance) fit -= 6;
                }
                if (pattern.Category != SoulPatternCategory.Attack) continue;
                if (back && pattern.DamageSchool == SoulDamageSchool.Physical && pattern.Range <= 2f) fit -= armed ? 4 : 8;
                if (!back && pattern.DamageSchool == SoulDamageSchool.Magic) fit -= 2;
            }
            return fit;
        }

        int lootSeen;

        void ShareSpoils()
        {
            // souls: the one who needs it most (SoulFit above SoulNeed; fewer souls, then the higher level on a tie)
            // takes it if it has room. Soul stones are kept for the target floor (its souls are what the party came
            // for): there the best souls go into them — a better one takes the stone of the worst kept so far. What
            // is neither taken nor kept is let go (one someone wants stays in the bag until the floor ends). Without a
            // target (free exploring) a stone keeps a soul someone wants but has no room for, as it comes.
            bool targeted = TargetMode != SoulExploreMode.Explore, stoneFloor = targeted && Floor >= TargetFloor;
            foreach (var drop in Stash.ToArray())
            {
                if (drop.Preserved) continue;
                SoulMercenary taker = null;
                float best = SoulNeed;
                bool wanted = false;
                foreach (var hero in Mercenaries)
                {
                    if (!hero.Alive || hero.CoreBlocked(drop.Soul)) continue;
                    float fit = SoulFit(hero, drop.Soul);
                    if (fit < SoulNeed) continue;
                    wanted = true;
                    if (!hero.HasFreeSoulSlot) continue;
                    if (taker == null || fit > best + .01f || Mathf.Abs(fit - best) <= .01f
                        && (hero.Souls.Count < taker.Souls.Count || hero.Souls.Count == taker.Souls.Count && hero.Level > taker.Level)) { taker = hero; best = fit; }
                }
                if (taker != null && Absorb(taker, drop)) continue;
                if (stoneFloor && KeepBest(drop)) continue;
                if (!wanted)
                {
                    Stash.Remove(drop);
                    Log($"{drop.Soul.OriginMonster}의 영혼 — 맞는 용병이 없어 흘려보냈습니다");
                    continue;
                }
                if (!targeted && Preserve(drop)) Log($"영혼석에 {drop.Soul.OriginMonster}의 영혼을 담았습니다 (남은 영혼석 {PreservationItems})");
            }
        }

        // How good a soul is to bring home: its grade first, then how well it suits the best of the party.
        float SoulValue(SoulDrop drop)
        {
            float fit = -50;
            foreach (var hero in Mercenaries) if (hero.Alive) fit = Mathf.Max(fit, SoulFit(hero, drop.Soul));
            return drop.Soul.Grade * 100 + Mathf.Clamp(fit, -50, 50);
        }

        // Into a soul stone: a free one, or the stone of the worst soul kept on this floor if this one is better
        // (that one goes back into the bag).
        bool KeepBest(SoulDrop drop)
        {
            if (PreservationItems > 0)
            {
                if (!Preserve(drop)) return false;
                Log($"영혼석에 {drop.Soul.OriginMonster}의 영혼을 담았습니다 (남은 영혼석 {PreservationItems})");
                return true;
            }
            SoulDrop worst = null;
            float worstValue = float.MaxValue, value = SoulValue(drop);
            foreach (var kept in Stash)
            {
                if (!kept.Preserved) continue;
                float v = SoulValue(kept);
                if (v < worstValue) { worstValue = v; worst = kept; }
            }
            if (worst == null || worstValue >= value) return false;
            worst.Preserved = false;
            PreservationItems++;
            if (!Preserve(drop)) { worst.Preserved = true; PreservationItems--; return false; }
            Log($"더 좋은 {drop.Soul.OriginMonster}의 영혼을 영혼석에 담았습니다 ({worst.Soul.OriginMonster}의 영혼 대신)");
            return true;
        }

        // Gear: every new find goes on whoever needs it (SoulCampaign.UpgradeGain) — automatic control or not; the
        // rest is carried home to the storehouse.
        void ShareGear()
        {
            while (lootSeen < Inventory.Count)
            {
                var item = Inventory[lootSeen];
                var taker = SoulCampaign.BestTaker(Mercenaries.FindAll(hero => hero.Alive), item, out var replace);
                if (taker == null) { lootSeen++; continue; }
                Inventory.RemoveAt(lootSeen);
                var lost = SoulCampaign.Wear(taker, item, replace, rules);
                Inventory.AddRange(lost); // what it replaced goes in the bag (someone else may want it; else it goes home)
                Log($"{taker.Name}: {item.Name} 장착" + (lost.Count > 0 ? $" ({string.Join(", ", lost.ConvertAll(e => e.Name))} 가방으로)" : ""));
            }
        }
    }
}
