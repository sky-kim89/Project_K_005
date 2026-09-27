using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // The storehouse (창고) and what goes down with each party. A party has one list of provisions (준비물): up to
    // CarryKinds kinds (2, up to 4 with the guild's 준비물 칸), 1–CarryLimit (3) of each — potions drink themselves, the camp
    // kit, the return and mending scrolls and the soul stones are used by the party itself. The enhancement
    // scrolls are not packed: whoever watches reads them straight from the store (SoulGameplayController).
    // Provisions leave the store the moment they are given to a party (the store shows what is left); they go
    // down with it, and whatever comes back up goes back on the shelf — the party's list is empty again.
    public sealed partial class SoulCampaign
    {
        static readonly (string id, int count)[] StartingSupplies = { ("potion_stamina", 2), ("scroll_heal", 1), (SoulSupplies.CampKit, 1) };


        public int Potions
        {
            get => Supply(SoulSupplies.HealPotion);
            set => SetAmount(eItem.PotionHeal, Mathf.Max(0, value));
        }

        public int Supply(string id) { var def = SoulSupplies.Get(id); return def != null ? Wallet.Get(def.Item) : 0; }

        // Every kind in stock.
        public Dictionary<string, int> Supplies()
        {
            var stock = new Dictionary<string, int>();
            foreach (var supply in SoulSupplies.All) { int count = Wallet.Get(supply.Item); if (count > 0) stock[supply.Id] = count; }
            return stock;
        }

        public void AddSupply(string id, int count)
        {
            var def = SoulSupplies.Get(id);
            if (count == 0 || def == null) return;
            if (count > 0) Wallet.Add(def.Item, count);
            else Wallet.Spend(def.Item, Mathf.Min(-count, Supply(id)));
            Changed();
        }

        // Storehouse level: 1 at the start (a shed), up to 3.
        public int StorageLevel => Mathf.Max(1, Level(SoulBuildingKind.Storage));
        public const int BaseCarryKinds = 2, CarryPerKind = 3;
        public int CarryKinds => BaseCarryKinds + PerkLevel(SoulPerk.CarryKinds); // 2, up to 4 with the guild's 준비물 칸
        public int StorageCapacity => 10 + 15 * StorageLevel; // equipment kept unworn: 25 / 40 / 55
        public bool StorageFull => Inventory.Count >= StorageCapacity;

        public bool Buyable(string id)
        {
            var def = SoulSupplies.Get(id);
            return def != null && def.ShopLevel > 0 && def.ShopLevel <= Level(SoulBuildingKind.Shop);
        }

        public int Carried(SoulParty party, string id) => party?.Carry.Find(c => c.Id == id)?.Count ?? 0;

        // Most of a kind a party can hold: what it has and what the store has, up to CarryLimit.
        public int CarryMax(SoulParty party, string id) => Mathf.Min(CarryLimit, Supply(id) + Carried(party, id));

        // The party's provisions, checked: packable kinds, at most CarryKinds of them, 1..CarryLimit each, no more
        // than the store has. What it held goes back on the shelf first; the new list comes out of the store.
        public bool SetCarry(SoulParty party, IEnumerable<SoulCarry> carry)
        {
            if (IsAway(party)) return false;
            var wanted = new List<SoulCarry>();
            foreach (var entry in carry) wanted.Add(new SoulCarry { Id = entry.Id, Count = entry.Count });
            foreach (var entry in party.Carry) { var def = SoulSupplies.Get(entry.Id); if (def != null) Wallet.Add(def.Item, entry.Count); }
            party.Carry.Clear();
            foreach (var entry in wanted)
            {
                var def = SoulSupplies.Get(entry.Id);
                if (def == null || !SoulSupplies.Packed(entry.Id) || party.Carry.Exists(c => c.Id == entry.Id)) continue;
                if (party.Carry.Count >= CarryKinds) break;
                int count = Mathf.Min(entry.Count, CarryLimit, Supply(entry.Id));
                if (count <= 0) continue;
                Wallet.Spend(def.Item, count);
                party.Carry.Add(new SoulCarry { Id = entry.Id, Count = count });
            }
            Changed();
            return true;
        }

        // One kind set to `count` (0 takes it off the list; a new kind goes at the end).
        public bool SetCarryCount(SoulParty party, string id, int count)
        {
            var list = party.Carry.ConvertAll(c => new SoulCarry { Id = c.Id, Count = c.Count });
            var entry = list.Find(c => c.Id == id);
            if (entry == null)
            {
                if (count <= 0 || list.Count >= CarryKinds) return false;
                list.Add(new SoulCarry { Id = id, Count = count });
            }
            else if (count <= 0) list.Remove(entry);
            else entry.Count = count;
            return SetCarry(party, list);
        }

        // A first list from the store: healing first, then the way out and rest (by priority), three of each.
        public void AutoFill(SoulParty party)
        {
            var order = new List<SoulSupply>(SoulSupplies.All);
            order.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            var carry = new List<SoulCarry>();
            foreach (var supply in order)
                if (carry.Count < CarryKinds && SoulSupplies.Packed(supply.Id) && CarryMax(party, supply.Id) > 0)
                    carry.Add(new SoulCarry { Id = supply.Id, Count = Mathf.Min(3, CarryMax(party, supply.Id)) });
            SetCarry(party, carry);
        }

        // The expedition takes its provisions down (already out of the store): potions onto the belt, tools and
        // the party's own scrolls into the pouch, soul stones to the party (PreservationItems).
        public void Pack(SoulParty party, SoulDungeonSession session)
        {
            foreach (var entry in party.Carry)
            {
                if (entry.Count <= 0) continue;
                var bag = ForBelt(entry.Id) ? session.Belt : session.Pouch;
                bag[entry.Id] = SoulDungeonSession.Count(bag, entry.Id) + entry.Count;
            }
            party.Carry.Clear();
            session.StowStones();
            Changed();
        }

        public static bool ForBelt(string id) => SoulSupplies.Get(id)?.Kind == SoulSupplyKind.Potion;

        // Whatever came back goes back on the shelf.
        void Unpack(SoulDungeonSession session)
        {
            foreach (var entry in session.Belt) AddSupply(entry.Key, entry.Value);
            foreach (var entry in session.Pouch) AddSupply(entry.Key, entry.Value);
        }

        // ── shop ─────────────────────────────────────────────────

        public List<SoulSupply> SupplyStock()
        {
            var list = new List<SoulSupply>();
            foreach (var supply in SoulSupplies.All)
                if (supply.ShopLevel > 0 && supply.ShopLevel <= Level(SoulBuildingKind.Shop)
                    || supply.Id == SoulSupplies.ReturnScroll && PerkLevel(SoulPerk.ReturnScrollShop) > 0) list.Add(supply); // 귀환 두루마리 조달
            return list;
        }

        public int SupplyPrice(SoulSupply supply)
            => Mathf.RoundToInt((supply.Id == SoulSupplies.ReturnScroll ? ReturnScrollPrice : supply.Price) * PriceScale * SupplyDiscount);

        public bool BuySupply(SoulSupply supply)
        {
            if (!SupplyStock().Contains(supply) || !Spend(SupplyPrice(supply))) return false;
            Wallet.Add(supply.Item, 1);
            Changed($"{supply.Name} 구매 (보유 {Supply(supply.Id)})");
            return true;
        }

        public bool BuyPotion()
        {
            var potion = SoulSupplies.Get(SoulSupplies.HealPotion);
            return BuySupply(potion);
        }
    }
}
