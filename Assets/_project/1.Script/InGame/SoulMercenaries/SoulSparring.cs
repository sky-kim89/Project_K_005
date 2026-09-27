using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SoulMercenaries
{
    public enum SoulSparOpponent { Pack, Elite, Boss }

    // One mercenary's numbers from a mock fight.
    public sealed class SoulSparLine
    {
        public string Name;
        public float Dealt, Taken, Stamina, Mana;
        public int Evasions, Guards, Downed;
        public readonly Dictionary<string, int> Used = new Dictionary<string, int>();
    }

    public sealed class SoulSparResult
    {
        public bool Won;
        public float Seconds;
        public string Opponent;
        public readonly List<SoulSparLine> Lines = new List<SoulSparLine>();
    }

    // The training ground's sparring yard (대련장): the party fights a chosen opponent in a closed arena, on copies
    // of the mercenaries — nobody is hurt, wounded, tired or paid in experience. Meant for trying pattern locks,
    // gear and belts before the dungeon. The fight is simulated at once and reported.
    public static class SoulSparring
    {
        public const float Step = .05f, MaxSeconds = 180f;

        public static SoulMonsterData[] Opponents(SoulDungeonData dungeon, SoulSparOpponent kind, int floor = 1)
        {
            if (dungeon == null) return new SoulMonsterData[0];
            var roster = dungeon.Roster(floor);
            switch (kind)
            {
                case SoulSparOpponent.Elite:
                    return roster.Elites.Length > 0 ? new[] { roster.Elites[0] } : new SoulMonsterData[0];
                case SoulSparOpponent.Boss:
                    return roster.Boss != null ? new[] { roster.Boss } : new SoulMonsterData[0];
                default:
                    var list = new List<SoulMonsterData>();
                    if (roster.Packs.Length > 0)
                    {
                        var pack = roster.Packs[0];
                        for (int i = 0; i < 3 && pack.Front != null && pack.Front.Length > 0; i++) list.Add(pack.Front[i % pack.Front.Length]);
                        for (int i = 0; i < 2 && pack.Back != null && pack.Back.Length > 0; i++) list.Add(pack.Back[i % pack.Back.Length]);
                    }
                    return list.ToArray();
            }
        }

        // The floor's first pack by name, its elite, its guardian.
        public static string OpponentName(SoulDungeonData dungeon, SoulSparOpponent kind, int floor)
        {
            var monsters = Opponents(dungeon, kind, floor);
            if (kind == SoulSparOpponent.Pack) { var roster = dungeon?.Roster(floor); return roster != null && roster.Packs.Length > 0 && !string.IsNullOrEmpty(roster.Packs[0].Name) ? roster.Packs[0].Name : "무리"; }
            return monsters.Length > 0 ? monsters[0].Name : kind == SoulSparOpponent.Boss ? "보스" : "엘리트";
        }

        public static SoulSparResult Run(SoulCampaign campaign, IList<SoulMercenary> party, SoulSparOpponent kind, int floor, int seed)
        {
            var result = new SoulSparResult { Opponent = $"{floor}층 {OpponentName(campaign.Data.Dungeon, kind, floor)}" };
            var monsters = Opponents(campaign.Data.Dungeon, kind, floor);
            if (monsters.Length == 0 || party.Count == 0) return result;
            var arena = ScriptableObject.CreateInstance<SoulDungeonData>();
            var rows = new StringBuilder();
            for (int y = 0; y < 18; y++) rows.Append(y == 0 || y == 17 ? new string('#', 30) : "#" + new string('.', 28) + "#").Append(y < 17 ? "\n" : "");
            arena.MapRows = rows.ToString();
            arena.PartyStart = new Vector2Int(6, 9); arena.Exit = new Vector2Int(28, 1);
            arena.GuaranteeFirstSoul = false;
            arena.Monsters = new SoulMonsterSpawn[monsters.Length];
            for (int i = 0; i < monsters.Length; i++)
                arena.Monsters[i] = new SoulMonsterSpawn { Monster = monsters[i], Cell = new Vector2Int(17 + i % 2 * 2, 6 + i / 2 * 2 + i % 2), Group = 1 };
            var copies = new List<SoulMercenary>();
            foreach (var hero in party)
            {
                var copy = SoulSave.Clone(hero, campaign.Data);
                if (copy == null) continue;
                foreach (var buff in hero.Buffs) copy.Buffs[buff.Key] = buff.Value; // the guild's stats too
                copy.Rebuild(campaign.Rules);
                copies.Add(copy);
            }
            var fight = new SoulDungeonSession(arena, campaign.Rules, copies, seed, Mathf.Clamp(floor, 1, SoulDungeonSession.FinalFloor)) { AutoExplore = false };
            for (int i = 0; i < fight.Mercenaries.Count; i++)
            {
                fight.Mercenaries[i].Position = fight.Map.Center(new Vector2Int(12, 5 + i * 2));
                // walk up to the opponents: a melee-only party does not see them from the line-up
                fight.SetDestination(fight.Mercenaries[i], fight.Map.Center(new Vector2Int(16, 5 + i * 2)), out Vector2 _, playerOrder: false);
            }
            foreach (var hero in fight.Mercenaries) result.Lines.Add(new SoulSparLine { Name = hero.Name });

            var lastStamina = new float[fight.Mercenaries.Count];
            var lastMana = new float[fight.Mercenaries.Count];
            for (int i = 0; i < lastStamina.Length; i++) { lastStamina[i] = fight.Mercenaries[i].Stamina; lastMana[i] = fight.Mercenaries[i].Mp; }
            float time = 0;
            while (time < MaxSeconds && fight.Monsters.Exists(m => m.Alive) && fight.Mercenaries.Exists(h => h.Alive))
            {
                fight.Tick(Step);
                time += Step;
                for (int i = 0; i < fight.Mercenaries.Count; i++)
                {
                    var hero = fight.Mercenaries[i];
                    var line = result.Lines[i];
                    line.Stamina += Mathf.Max(0, lastStamina[i] - hero.Stamina);
                    line.Mana += Mathf.Max(0, lastMana[i] - hero.Mp);
                    lastStamina[i] = hero.Stamina; lastMana[i] = hero.Mp;
                }
                foreach (var evt in fight.CombatEvents) Count(result, fight, evt);
                fight.CombatEvents.Clear();
            }
            for (int i = 0; i < fight.Mercenaries.Count; i++) if (!fight.Mercenaries[i].Alive) result.Lines[i].Downed = 1;
            result.Won = !fight.Monsters.Exists(m => m.Alive);
            result.Seconds = time;
            Object.DestroyImmediate(arena);
            return result;
        }

        static void Count(SoulSparResult result, SoulDungeonSession fight, SoulCombatEvent evt)
        {
            int actor = evt.Actor is SoulMercenary a ? fight.Mercenaries.IndexOf(a) : -1;
            int target = evt.Target is SoulMercenary t ? fight.Mercenaries.IndexOf(t) : -1;
            switch (evt.Kind)
            {
                case SoulEventKind.Hit:
                    if (actor >= 0) result.Lines[actor].Dealt += evt.Amount;
                    if (target >= 0) result.Lines[target].Taken += evt.Amount;
                    break;
                case SoulEventKind.Attack:
                case SoulEventKind.Skill:
                    if (actor < 0) break;
                    string name = evt.Kind == SoulEventKind.Skill ? evt.Label : evt.Pattern != null ? evt.Pattern.Id : evt.Label;
                    if (string.IsNullOrEmpty(name)) break;
                    var used = result.Lines[actor].Used;
                    used[name] = used.TryGetValue(name, out int n) ? n + 1 : 1;
                    break;
                case SoulEventKind.Evade:
                case SoulEventKind.Roll:
                    if (target >= 0) result.Lines[target].Evasions++;
                    break;
                case SoulEventKind.Guard:
                    if (target >= 0) result.Lines[target].Guards++;
                    break;
            }
        }
    }
}
