using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // Potion: on the belt, drunk by itself when its condition is met. Scroll: in the pouch, read when the player
    // taps it — a party-wide effect at the moment that matters. Tool: in the pouch, used by the player (camp).
    public enum SoulSupplyKind { Potion, Scroll, Tool }

    public sealed class SoulSupply
    {
        public string Id, Name, Description, Icon;
        public eItem Item;         // the stock is counted in ItemData
        public SoulSupplyKind Kind;
        public int Price;
        public int ShopLevel;      // 0: never in the shop
        public int MaxStack = 5;   // per belt / pouch slot
        public bool Rare;          // the return scroll: drops only (and the odd wandering merchant)
        public int Priority;       // auto belt / pouch order (low first)
    }

    public static class SoulSupplies
    {
        public const string HealPotion = "potion_heal", ReturnScroll = "scroll_return", CampKit = "camp_kit", SoulStone = "soul_stone", MendScroll = "scroll_mend";

        public static readonly SoulSupply[] All =
        {
            // potions (automatic)
            new SoulSupply { Id = HealPotion, Item = eItem.PotionHeal, Name = "회복 포션", Icon = "potion", Kind = SoulSupplyKind.Potion, Price = 40, ShopLevel = 1, Priority = 0,
                Description = "HP 30% 아래에서 자동 (부상 1단계마다 4%씩 일찍): HP 40% 회복 · 부상 1단계 치료" },
            new SoulSupply { Id = "potion_greater", Item = eItem.PotionGreater, Name = "상급 회복 포션", Icon = "potion", Kind = SoulSupplyKind.Potion, Price = 150, ShopLevel = 3, MaxStack = 3, Priority = 1,
                Description = "HP 25% 아래에서 자동 (부상 1단계마다 4%씩 일찍, 회복 포션보다 먼저): HP 70% 회복 · 부상 2단계 치료" },
            new SoulSupply { Id = "potion_stamina", Item = eItem.PotionStamina, Name = "스태미나 물약", Icon = "potion_stamina", Kind = SoulSupplyKind.Potion, Price = 30, ShopLevel = 1, Priority = 2,
                Description = "전투 중 스태미나 20% 아래에서 자동: 스태미나 50% 회복" },
            new SoulSupply { Id = "potion_mana", Item = eItem.PotionMana, Name = "마나 물약", Icon = "potion_mana", Kind = SoulSupplyKind.Potion, Price = 50, ShopLevel = 2, Priority = 3,
                Description = "MP를 쓰는 용병의 MP가 20% 아래일 때 자동: MP 40% 회복" },
            new SoulSupply { Id = "antidote", Item = eItem.Antidote, Name = "해독제", Icon = "antidote", Kind = SoulSupplyKind.Potion, Price = 25, ShopLevel = 1, Priority = 4,
                Description = "중독·출혈·화상이 2등급 이상이면 자동: 셋 모두 해제, 10초 면역" },
            new SoulSupply { Id = "panacea", Item = eItem.Panacea, Name = "만능약", Icon = "antidote", Kind = SoulSupplyKind.Potion, Price = 120, ShopLevel = 3, MaxStack = 3, Priority = 5,
                Description = "상태 이상 2개 이상 또는 행동 불가(스턴·빙결·공포·석화)일 때 자동: 모두 해제, 6초 면역" },
            // scrolls (the player reads them) — one idea each: heal, strike, guard, speed, breath, cleanse
            new SoulSupply { Id = "scroll_heal", Item = eItem.ScrollHeal, Name = "치유의 두루마리", Icon = "scroll_heal", Kind = SoulSupplyKind.Scroll, Price = 90, ShopLevel = 1, MaxStack = 3, Priority = 10,
                Description = "회복 — 파티 전원 HP 25% 즉시 회복, 10초 동안 초당 최대 HP의 3% 재생" },
            new SoulSupply { Id = "scroll_fury", Item = eItem.ScrollFury, Name = "투지의 두루마리", Icon = "scroll_fury", Kind = SoulSupplyKind.Scroll, Price = 80, ShopLevel = 1, MaxStack = 3, Priority = 11,
                Description = "공격 — 30초 동안 파티 전원 공격력 +25% · 마력 +20%" },
            new SoulSupply { Id = "scroll_guard", Item = eItem.ScrollGuard, Name = "수호의 두루마리", Icon = "scroll_guard", Kind = SoulSupplyKind.Scroll, Price = 80, ShopLevel = 1, MaxStack = 3, Priority = 12,
                Description = "방어 — 30초 동안 파티 전원 방어력 +40% · 물리·마법 내성 +10%" },
            new SoulSupply { Id = "scroll_gale", Item = eItem.ScrollGale, Name = "질풍의 두루마리", Icon = "scroll_gale", Kind = SoulSupplyKind.Scroll, Price = 100, ShopLevel = 2, MaxStack = 3, Priority = 13,
                Description = "속도 — 20초 동안 파티 전원 행동 속도 +15% · 이동 속도 +25% · 회피 +10%" },
            new SoulSupply { Id = "scroll_breath", Item = eItem.ScrollBreath, Name = "숨결의 두루마리", Icon = "scroll_breath", Kind = SoulSupplyKind.Scroll, Price = 90, ShopLevel = 2, MaxStack = 3, Priority = 14,
                Description = "자원 — 파티 전원 스태미나 50% · MP 30% 즉시 회복, 20초 동안 스태미나 재생 2배" },
            new SoulSupply { Id = "scroll_purify", Item = eItem.ScrollPurify, Name = "정화의 두루마리", Icon = "scroll_purify", Kind = SoulSupplyKind.Scroll, Price = 110, ShopLevel = 3, MaxStack = 3, Priority = 15,
                Description = "정화 — 파티 전원의 상태 이상을 모두 풀고 10초 동안 막음" },
            new SoulSupply { Id = MendScroll, Item = eItem.ScrollMend, Name = "봉합의 두루마리", Icon = "scroll_heal", Kind = SoulSupplyKind.Scroll, Price = 160, ShopLevel = 2, MaxStack = 3, Priority = 6,
                Description = "부상 — 파티 전원의 부상 1단계 치료. 부상으로 탈출하기 직전이면 자동 탐험도 먼저 읽습니다" },
            new SoulSupply { Id = ReturnScroll, Item = eItem.ScrollReturn, Name = "귀환의 두루마리", Icon = "scroll_return", Kind = SoulSupplyKind.Scroll, Price = 1500, ShopLevel = 0, MaxStack = 1, Rare = true, Priority = 8,
                Description = "전투 중이 아닐 때만: 10초 동안 주문을 외워 파티 전원이 전리품을 들고 마을로 돌아갑니다. 전투가 시작되면 끊깁니다. 매우 귀합니다" },
            // tools
            new SoulSupply { Id = CampKit, Item = eItem.CampKit, Name = "야영 도구", Icon = "camp", Kind = SoulSupplyKind.Tool, Price = 70, ShopLevel = 1, MaxStack = 3, Priority = 9,
                Description = "전투 중이 아닐 때: 그 자리에서 야영 (4초 동안 HP·MP·스태미나 회복), 끝까지 쉬면 피로도 −70 · 부상 1단계 치료" },
            // the soul stone: a supply, not a currency — each one carries an unabsorbed soul out of the dungeon
            new SoulSupply { Id = SoulStone, Item = eItem.SoulStone, Name = "영혼석", Icon = "preserve", Kind = SoulSupplyKind.Tool, Price = 60, ShopLevel = 1, MaxStack = 5, Priority = 7,
                Description = "흡수하지 않은 몬스터 영혼 하나를 담아 던전 밖으로 가져옵니다 (파티원이 흡수하고 남은 영혼에 자동으로 씁니다)" },
        };

        public static SoulSupply Get(string id)
        {
            foreach (var supply in All) if (supply.Id == id) return supply;
            return null;
        }

        // Automatic pouch order: the return scroll and the camping kit first (the way out and the way to rest),
        // then the scrolls. Scrolls share one cooldown across the party; potions have one per mercenary and kind.
        public const float ScrollCooldown = 12f, PotionCooldown = 6f, RecallTime = 10f;

        // ── effects ──────────────────────────────────────────────

        static SoulStatBonus Share(SoulCombatant unit, StatType stat, float share)
            => new SoulStatBonus { Stat = stat, Value = unit.Stats.Total(stat) * share };

        static SoulStatBonus Flat(StatType stat, float value) => new SoulStatBonus { Stat = stat, Value = value };

        // A scroll on one living ally (the session applies it to the whole party).
        public static void ApplyScroll(string id, SoulMercenary hero, float duration = 1)
        {
            float maxHp = hero.Stats.Total(StatType.MaxHp);
            float d = Mathf.Max(0, duration); // 두루마리 필사: longer buffs
            switch (id)
            {
                case "scroll_heal":
                    hero.Hp = Mathf.Min(maxHp, hero.Hp + maxHp * .25f);
                    hero.AddTimedBuff("scroll_heal", new[] { Flat(StatType.HpRegen, maxHp * .03f) }, 10 * d);
                    break;
                case "scroll_fury":
                    hero.AddTimedBuff("scroll_fury", new[] { Share(hero, StatType.Attack, .25f), Share(hero, StatType.Magic, .2f) }, 30 * d);
                    break;
                case "scroll_guard":
                    hero.AddTimedBuff("scroll_guard", new[] { Share(hero, StatType.Armor, .4f), Flat(StatType.PhysicalResist, .1f), Flat(StatType.MagicResist, .1f) }, 30 * d);
                    break;
                case "scroll_gale":
                    hero.AddTimedBuff("scroll_gale", new[] { Flat(StatType.ActionSpeed, .15f), Share(hero, StatType.MoveSpeed, .25f), Flat(StatType.Evasion, .1f) }, 20 * d);
                    break;
                case "scroll_breath":
                    hero.Stamina = Mathf.Min(hero.Stats.Total(StatType.MaxStamina), hero.Stamina + hero.Stats.Total(StatType.MaxStamina) * .5f);
                    hero.Mp = Mathf.Min(hero.Stats.Total(StatType.MaxMp), hero.Mp + hero.Stats.Total(StatType.MaxMp) * .3f);
                    hero.AddTimedBuff("scroll_breath", new[] { Share(hero, StatType.StaminaRegen, 1f) }, 20 * d);
                    break;
                case "scroll_purify":
                    Cleanse(hero, null, 10);
                    break;
                case MendScroll:
                    hero.HealWounds(1);
                    break;
            }
        }

        static readonly SoulStatus[] Toxins = { SoulStatus.Poison, SoulStatus.Bleed, SoulStatus.Burn };
        static readonly SoulStatus[] Controls = { SoulStatus.Stun, SoulStatus.Freeze, SoulStatus.Fear, SoulStatus.Petrify, SoulStatus.Confuse };

        // Lifts the given statuses (all when null) and keeps them off for a while.
        public static void Cleanse(SoulCombatant unit, SoulStatus[] kinds, float immunity)
        {
            var lifted = new List<SoulStatus>();
            unit.Statuses.RemoveAll(status => { bool hit = kinds == null || Array.IndexOf(kinds, status.Kind) >= 0; if (hit) lifted.Add(status.Kind); return hit; });
            foreach (SoulStatus kind in kinds ?? (SoulStatus[])Enum.GetValues(typeof(SoulStatus)))
                if (kind != SoulStatus.None) unit.StatusImmunity[kind] = Mathf.Max(unit.StatusImmunity.TryGetValue(kind, out float left) ? left : 0, immunity);
            SoulCombat.RefreshStatusEffects(unit);
        }

        // Does this potion's condition hold for the mercenary right now?
        // The deeper the wounds, the sooner a healing potion goes down: PotionPerWound a wound, MaxPotionLead at most
        // (three wounds: the plain one at 42% instead of 30%).
        public const float PotionPerWound = .04f, MaxPotionLead = .2f;
        public static float PotionLead(SoulMercenary hero) => Mathf.Min(MaxPotionLead, PotionPerWound * hero.Wounds);

        public static bool Wants(string id, SoulMercenary hero, bool fighting)
        {
            float hp = hero.Hp / Mathf.Max(1, hero.Stats.Total(StatType.MaxHp));
            switch (id)
            {
                case "potion_greater": return hp < .25f + PotionLead(hero);
                case HealPotion: return hp < .3f + PotionLead(hero);
                case "potion_stamina": return fighting && hero.Stamina < hero.Stats.Total(StatType.MaxStamina) * .2f;
                case "potion_mana": return UsesMana(hero) && hero.Mp < hero.Stats.Total(StatType.MaxMp) * .2f;
                case "antidote": return hero.Statuses.Exists(s => Array.IndexOf(Toxins, s.Kind) >= 0 && s.Grade >= 2);
                case "panacea": return hero.Statuses.Count >= 2 || hero.Statuses.Exists(s => Array.IndexOf(Controls, s.Kind) >= 0);
                default: return false;
            }
        }

        static bool UsesMana(SoulMercenary hero)
        {
            foreach (var skill in hero.AllActiveSkills())
                foreach (var cost in skill.Costs) if (cost.Resource == SoulResource.Mana && cost.Amount > 0) return true;
            return false;
        }

        // power: 물약 효율 (the guild's); ward: 해독 전문 — the antidotes keep statuses off 10 s longer.
        public static void Drink(string id, SoulMercenary hero, float power = 1, bool ward = false)
        {
            float maxHp = hero.Stats.Total(StatType.MaxHp);
            switch (id)
            {
                case "potion_greater": hero.Hp = Mathf.Min(maxHp, hero.Hp + maxHp * .7f * power); hero.HealWounds(2); break;
                case HealPotion: hero.Hp = Mathf.Min(maxHp, hero.Hp + maxHp * .4f * power); hero.HealWounds(1); break;
                case "potion_stamina": hero.Stamina = Mathf.Min(hero.Stats.Total(StatType.MaxStamina), hero.Stamina + hero.Stats.Total(StatType.MaxStamina) * .5f * power); break;
                case "potion_mana": hero.Mp = Mathf.Min(hero.Stats.Total(StatType.MaxMp), hero.Mp + hero.Stats.Total(StatType.MaxMp) * .4f * power); break;
                case "antidote": Cleanse(hero, Toxins, ward ? 20 : 10); break;
                case "panacea": Cleanse(hero, null, ward ? 16 : 6); break;
            }
        }

        // Fills belt / pouch slots from what is in store: the given plan first (a chosen kind per slot), empty
        // slots get the next kind in priority order that is still in stock.
        // What the sockets take from the store. autoEmpty: an empty socket takes the next kind by priority (the
        // auto-fill button); otherwise an empty socket stays empty — the player set exactly what goes.
        public static List<(string id, int count)> Pack(IDictionary<string, int> stock, IList<string> plan, int slots, SoulSupplyKind[] kinds, bool autoEmpty = true)
        {
            var left = new Dictionary<string, int>(stock);
            var packed = new List<(string, int)>();
            var used = new HashSet<string>();
            var order = new List<SoulSupply>(All);
            order.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            for (int slot = 0; slot < slots; slot++)
            {
                string id = plan != null && slot < plan.Count ? plan[slot] : null;
                if (id == null && autoEmpty)
                    foreach (var supply in order)
                        if (Array.IndexOf(kinds, supply.Kind) >= 0 && !used.Contains(supply.Id) && left.TryGetValue(supply.Id, out int n) && n > 0) { id = supply.Id; break; }
                if (id == null) continue;
                var def = Get(id);
                if (def == null || Array.IndexOf(kinds, def.Kind) < 0) continue;
                int have = left.TryGetValue(id, out int count) ? count : 0;
                int take = Mathf.Min(have, def.MaxStack);
                used.Add(id);
                if (take <= 0) continue;
                left[id] = have - take;
                packed.Add((id, take));
            }
            return packed;
        }

        // Enhancement scrolls (fury, guard, …): read by whoever watches, straight from the store — never packed.
        public static bool PlayerScroll(string id) { var def = Get(id); return def != null && def.Kind == SoulSupplyKind.Scroll && id != ReturnScroll && id != MendScroll; }
        // What a party takes down as provisions: potions, tools, and the scrolls it reads itself (return, mending).
        public static bool Packed(string id) => Get(id) != null && !PlayerScroll(id);

        public static readonly SoulSupplyKind[] BeltKinds = { SoulSupplyKind.Potion };
        public static readonly SoulSupplyKind[] PouchKinds = { SoulSupplyKind.Scroll, SoulSupplyKind.Tool };
    }
}
