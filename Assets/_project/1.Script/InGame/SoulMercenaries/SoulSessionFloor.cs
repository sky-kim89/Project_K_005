using System.Collections.Generic;

namespace SoulMercenaries
{
    // One day's dungeon: every party that goes down that day walks the same floors (midnight starts a new one).
    public sealed class SoulDungeonDay
    {
        readonly Dictionary<int, SoulDungeonSession.SharedFloor> floors = new Dictionary<int, SoulDungeonSession.SharedFloor>();

        public SoulDungeonSession.SharedFloor FloorAt(int floor)
        {
            if (!floors.TryGetValue(floor, out var shared)) floors[floor] = shared = new SoulDungeonSession.SharedFloor();
            return shared;
        }
    }

    // A floor is one place shared by the parties on it: the map, the monsters, chests, traps and objects. Each
    // party keeps its own session (its heroes, loot, supplies, what it has seen). The first party still inside
    // moves the monsters (once a tick), and they go for whoever of any party is nearest — so parties can fight
    // the same monster. Whoever lands the last hit takes the kill: its party gets the loot and the experience.
    public sealed partial class SoulDungeonSession
    {
        public SoulDungeonDay Day { get; }
        public SharedFloor Shared { get; }
        public bool Active => !Finished && !Defeated && !Recalled;
        public IReadOnlyList<SoulDungeonSession> FloorParties => Shared.Parties;
        bool Hosting => Shared.Host == this;

        public sealed class SharedFloor
        {
            internal SoulLayout Layout;
            internal SoulMap Map;
            internal List<SoulMonster> Monsters;
            internal List<SoulChest> Chests;
            internal List<SoulTrap> Traps;
            internal List<SoulHiddenStage> HiddenStages;
            internal List<SoulInteractable> Interactables;
            internal Dictionary<SoulCombatant, RouteState> Routes;
            internal HashSet<string> Rewarded;
            internal Dictionary<SoulCombatant, SoulMercenary> Killers;
            internal Dictionary<SoulCombatant, (SoulCombatant attacker, float at)> Provoked;
            internal Dictionary<SoulCombatant, int> TelegraphTurn;
            internal Dictionary<SoulCombatant, float> TelegraphRest;
            public float Clock;
            public readonly List<SoulDungeonSession> Parties = new List<SoulDungeonSession>();
            // Everyone on the floor still in the dungeon (the host refreshes it every tick).
            public readonly List<SoulMercenary> Heroes = new List<SoulMercenary>();

            public SoulDungeonSession Host
            {
                get
                {
                    foreach (var party in Parties) if (party.Active) return party;
                    return null;
                }
            }

            public bool Holds(SoulMercenary hero)
            {
                foreach (var party in Parties) if (party.Active && party.Mercenaries.Contains(hero)) return true;
                return false;
            }

            internal void RefreshHeroes()
            {
                Heroes.Clear();
                foreach (var party in Parties) if (party.Active) Heroes.AddRange(party.Mercenaries);
            }

            // The first party builds the floor; the others use what it built.
            internal void Adopt(SoulDungeonSession first)
            {
                Layout = first.Layout; Map = first.Map; Monsters = first.Monsters; Chests = first.Chests; Traps = first.Traps;
                HiddenStages = first.HiddenStages; Interactables = first.Interactables; Routes = first.routes; Rewarded = first.rewarded;
                Killers = first.killers; Provoked = first.provoked; TelegraphTurn = first.telegraphTurn; TelegraphRest = first.telegraphRest;
            }
        }

        // A dead monster is this party's to reward: its hero landed the last hit — or nobody on the floor did.
        bool OwnsKill(SoulMonster monster)
            => !killers.TryGetValue(monster, out var killer) || Mercenaries.Contains(killer) || !Shared.Holds(killer) && Hosting;
    }
}
