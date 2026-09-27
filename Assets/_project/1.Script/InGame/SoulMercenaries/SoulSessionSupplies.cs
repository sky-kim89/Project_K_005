using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // What the party carries into the dungeon and how it is used: potions on the belt go down by themselves,
    // scrolls and tools in the pouch wait for the player. Also the dungeon clock and fatigue.
    public sealed partial class SoulDungeonSession
    {
        public readonly Dictionary<string, int> Belt = new Dictionary<string, int>();   // potions (automatic)
        public readonly Dictionary<string, int> Pouch = new Dictionary<string, int>();  // scrolls and tools (manual)

        // The plain recovery potion (older code and tests count only these).
        public int Potions
        {
            get => Belt.TryGetValue(SoulSupplies.HealPotion, out int count) ? count : 0;
            set => Belt[SoulSupplies.HealPotion] = Mathf.Max(0, value);
        }

        public float ScrollWait { get; private set; }  // shared scroll cooldown
        public float RecallLeft { get; private set; }  // seconds of the return chant left (0: none)
        public bool Recalled { get; private set; }     // the return scroll took the party home
        // Dungeon clock: one real second is one minute down here (drives fatigue).
        public float DungeonMinutes;
        float campRelief;
        readonly Dictionary<string, float> drunkAt = new Dictionary<string, float>();

        public static int Count(Dictionary<string, int> bag, string id) => bag.TryGetValue(id, out int count) ? count : 0;

        // Skill books found down here (registered at the library back home).
        public readonly List<SoulActiveSkillData> FoundBooks = new List<SoulActiveSkillData>();

        // Books: elites 5%, bosses 30%, hidden chests 8%. Tier 2 from floor 3; tier 3 (the highest skills) only
        // from bosses on floor 7 and deeper.
        SoulActiveSkillData RollBook(SoulDropSource source)
        {
            float chance = source == SoulDropSource.Boss ? .3f : source == SoulDropSource.Elite ? .05f : source == SoulDropSource.HiddenChest ? .08f : 0;
            if (dungeon.SkillBooks == null || random.NextDouble() >= chance) return null;
            var pool = new List<SoulActiveSkillData>();
            foreach (var skill in dungeon.SkillBooks)
            {
                if (skill == null) continue;
                if (skill.BookTier >= 2 && Floor < 3) continue;
                if (skill.BookTier >= 3 && (Floor < 7 || source != SoulDropSource.Boss)) continue;
                pool.Add(skill);
            }
            return pool.Count > 0 ? pool[random.Next(pool.Count)] : null;
        }

        // Scrolls in treasure: the return scroll is very rare (boss 4%, hidden chest 2%); others a bit likelier.
        string RollScroll(SoulDropSource source)
        {
            double roll = random.NextDouble();
            float recall = source == SoulDropSource.Boss ? .04f : .02f, other = source == SoulDropSource.Boss ? .35f : .15f;
            if (roll < recall) return SoulSupplies.ReturnScroll;
            if (roll >= recall + other) return null;
            var scrolls = new List<SoulSupply>();
            foreach (var supply in SoulSupplies.All) if (supply.Kind == SoulSupplyKind.Scroll && !supply.Rare) scrolls.Add(supply);
            return scrolls[random.Next(scrolls.Count)].Id;
        }

        public string ClockText
        {
            get
            {
                int minutes = Mathf.FloorToInt(DungeonMinutes);
                return minutes >= 60 ? $"{minutes / 60}시간 {minutes % 60}분" : $"{minutes}분";
            }
        }

        // ── belt: potions drink themselves ───────────────────────

        void UseBelt()
        {
            if (Belt.Count == 0) return;
            var order = new List<SoulSupply>();
            foreach (var entry in Belt) { var def = SoulSupplies.Get(entry.Key); if (def != null && entry.Value > 0) order.Add(def); }
            if (order.Count == 0) return;
            order.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            foreach (var hero in Mercenaries)
            {
                if (!hero.Alive) continue;
                foreach (var potion in order)
                {
                    if (Count(Belt, potion.Id) <= 0 || !SoulSupplies.Wants(potion.Id, hero, partyFighting)) continue;
                    string key = hero.CombatId + ":" + potion.Id;
                    if (drunkAt.TryGetValue(key, out float last) && clock - last < SoulSupplies.PotionCooldown) continue;
                    drunkAt[key] = clock;
                    Belt[potion.Id]--;
                    SoulSupplies.Drink(potion.Id, hero, Perks.PotionPower * (1 + Mathf.Max(0, hero.Stats.Total(StatType.PotionPower))), Perks.AntidoteWard);
                    CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Skill, Actor = hero, Label = potion.Name, Support = true });
                    Log($"{hero.Name}: {potion.Name} (남은 {Belt[potion.Id]}개)");
                    break; // one potion a mercenary a moment
                }
            }
        }

        // ── pouch: the player reads scrolls, uses tools ──────────

        public bool InCombat => partyFighting;

        // Why this cannot be used now (null: it can).
        public string SupplyBlock(string id)
        {
            var def = SoulSupplies.Get(id);
            if (def == null || Count(Pouch, id) <= 0) return "없음";
            if (Finished || Defeated || Recalled) return "끝난 던전";
            if (id == SoulSupplies.ReturnScroll || id == SoulSupplies.CampKit)
            {
                if (partyFighting) return "전투 중에는 쓸 수 없습니다";
                if (RecallLeft > 0) return "귀환 주문을 외우는 중";
                if (id == SoulSupplies.CampKit && Camping > 0) return "야영 중";
                return null;
            }
            if (def.Kind == SoulSupplyKind.Scroll && ScrollWait > 0) return $"두루마리 대기 {ScrollWait:0.0}초";
            return null;
        }

        public bool UseSupply(string id, SoulMercenary reader = null)
        {
            if (SupplyBlock(id) != null) return false;
            var def = SoulSupplies.Get(id);
            reader = reader != null && reader.Alive ? reader : Leader;
            Pouch[id]--;
            string text;
            if (id == SoulSupplies.ReturnScroll)
            {
                RecallLeft = SoulSupplies.RecallTime;
                text = "귀환의 두루마리: 주문을 외웁니다 — 10초 동안 전투가 없으면 마을로 돌아갑니다";
            }
            else if (id == SoulSupplies.CampKit)
            {
                Camping = CampTime;
                campRelief = SoulFatigue.KitRelief;
                var leader = Leader;
                GatherAround(leader != null ? leader.Position : Mercenaries[0].Position, true);
                text = "야영 도구: 그 자리에서 야영합니다";
            }
            else
            {
                foreach (var hero in Mercenaries) if (hero.Alive) SoulSupplies.ApplyScroll(id, hero, Perks.ScrollDuration);
                ScrollWait = SoulSupplies.ScrollCooldown;
                text = $"{def.Name}: {def.Description}";
            }
            Log(text);
            CombatEvents.Add(new SoulCombatEvent { Kind = SoulEventKind.Interact, Actor = reader, Label = def.Name });
            return true;
        }

        // ── clock, fatigue, the return chant ─────────────────────

        void TickSupplies(float dt)
        {
            if (ScrollWait > 0) ScrollWait = Mathf.Max(0, ScrollWait - dt);
            DungeonMinutes += dt;
            if (Camping <= 0 && !Sleeping)
                foreach (var hero in Mercenaries)
                    if (hero.Alive) SoulFatigue.Add(hero, SoulFatigue.Gain(hero, dt, partyFighting));
            if (RecallLeft > 0)
            {
                if (partyFighting) { RecallLeft = 0; Log("전투가 시작되어 귀환 주문이 끊겼습니다 (두루마리는 사라졌습니다)"); }
                else
                {
                    RecallLeft -= dt;
                    foreach (var hero in Mercenaries) if (hero.Alive) hero.Action = "귀환 주문";
                    if (RecallLeft <= 0) Recall();
                }
            }
        }

        // Sleep takes fatigue away and mends a wound — but only a camp that was not broken off.
        void EndCamp()
        {
            float relief = campRelief * Perks.CampRelief;
            foreach (var hero in Mercenaries) if (hero.Alive) { SoulFatigue.Add(hero, -relief); hero.HealWounds(Perks.CampWounds); }
            Log($"야영을 마쳤습니다 — 피로도 −{relief:0} · 부상 {Perks.CampWounds}단계 회복");
            campRelief = 0;
        }

        void Recall(bool scroll = true)
        {
            RecallLeft = 0;
            Recalled = true;
            foreach (var drop in Stash) if (drop.Preserved) Vault.Add(drop.Soul);
            Stash.Clear();
            if (scroll) Log("귀환의 두루마리: 용병단이 마을로 돌아갑니다");
        }
    }
}
