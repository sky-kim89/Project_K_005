using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // ── 상점: 진열대 (구매), 창고 (판매) ───────────────────────────
    // Goods as tiles with their price in the corner; the selected one is shown in the side panel with the button.

    public sealed class SoulShopPopup : SoulBuildingPopup
    {
        protected override SoulBuildingKind Kind => SoulBuildingKind.Shop;
        protected override string[] Tabs => new[] { "구매", "판매" };
        SoulItem selectedItem;
        SoulSupply selectedSupply;

        protected override void BuildBody(RectTransform body, int tab)
        {
            var (mainArea, sideArea) = SoulKit.Split(body, 520);
            var main = SoulKit.Scroll(mainArea);
            var side = SoulKit.Side(sideArea);
            if (tab == 0) Buy(main, side);
            else Sell(main, side);
        }

        void Buy(Transform main, Transform side)
        {
            var stock = Campaign.Stock();
            if (selectedItem != null && !stock.Contains(selectedItem)) selectedItem = null;
            if (selectedItem == null && selectedSupply == null ) selectedSupply = Campaign.SupplyStock().Count > 0 ? Campaign.SupplyStock()[0] : null;

            SoulKit.Section(main, "소모품", Campaign.Event == SoulVillageEvent.Festival ? SoulUi.Colored("축제 20% 할인", SoulUi.Good) : null);
            var supplies = SoulKit.Grid(main, new Vector2(92, 92), 10);
            foreach (var supply in Campaign.SupplyStock())
            {
                var def = supply;
                var tile = SoulKit.Tile(supplies, SoulIconSet.Ui(supply.Icon), 92, SoulUi.PanelBorder, $"{Campaign.SupplyPrice(supply)}", SoulUi.Accent, selectedSupply == supply, false,
                    () => { selectedSupply = def; selectedItem = null; Refresh(); });
                SoulTooltip.Attach(tile, SoulIconSet.Ui(def.Icon), def.Name, () => new List<SoulLine> { new SoulLine(null, def.Description), new SoulLine(null, $"보유 {Campaign.Supply(def.Id)}") });
            }
            SoulKit.Section(main, "장비", Campaign.StorageFull ? SoulUi.Colored("창고 가득", SoulUi.Bad) : null);
            var gear = SoulKit.Grid(main, new Vector2(92, 92), 10);
            foreach (var item in Sorted(stock))
            {
                var chosen = item;
                ItemTile(gear, item, 92, selectedItem == item, () => { selectedItem = chosen; selectedSupply = null; Refresh(); }, null, $"{Campaign.ItemPrice(item)}", SoulUi.Accent);
            }

            if (selectedItem != null)
            {
                ItemDetail(side, selectedItem);
                int price = Campaign.ItemPrice(selectedItem);
                var item = selectedItem;
                SoulKit.Action(side, Campaign.StorageFull ? "창고가 가득 찼습니다" : $"구매 — {price} 금화", new Color(.2f, .5f, .28f), Campaign.Gold >= price && !Campaign.StorageFull,
                    () => Confirm("buy", BuyQuestion(SoulDescribe.ItemName(item), price), () => { Campaign.Buy(item); selectedItem = null; }), 52, SoulIconSet.Ui("gold"));
            }
            else if (selectedSupply != null)
            {
                var def = selectedSupply;
                var tile = SoulKit.Tile(side, SoulIconSet.Ui(def.Icon), 88, SoulUi.PanelBorder);
                SoulKit.Header(side, tile, def.Name, $"보유 {Campaign.Supply(def.Id)}");
                SoulKit.Lines(side, new[] { new SoulLine(null, def.Description) });
                int price = Campaign.SupplyPrice(def);
                foreach (int amount in new[] { 1, 5 })
                {
                    int n = amount;
                    SoulKit.Action(side, $"{n}개 구매 — {price * n} 금화", new Color(.2f, .5f, .28f), Campaign.Gold >= price * n,
                        () => Confirm("buy", BuyQuestion($"{def.Name} ×{n}", price * n), () => { for (int i = 0; i < n; i++) Campaign.BuySupply(def); }), 48, SoulIconSet.Ui("gold"));
                }
            }
        }

        void Sell(Transform main, Transform side)
        {
            if (selectedItem != null && !Campaign.Inventory.Contains(selectedItem)) selectedItem = null;
            SoulKit.Section(main, "창고", $"{Campaign.Inventory.Count}/{Campaign.StorageCapacity}");
            var gear = SoulKit.Grid(main, new Vector2(92, 92), 10);
            foreach (var item in Sorted(Campaign.Inventory))
            {
                var chosen = item;
                ItemTile(gear, item, 92, selectedItem == item, () => { selectedItem = chosen; Refresh(); }, null, $"{SoulCampaign.SellPrice(item)}", SoulUi.Good);
            }
            if (selectedItem == null) return;
            ItemDetail(side, selectedItem);
            var sold = selectedItem;
            SoulKit.Action(side, $"판매 — {SoulCampaign.SellPrice(sold)} 금화", new Color(.55f, .38f, .1f), true, () => Confirm("sell", $"정말 판매하시겠습니까?\n<size=22>{SoulDescribe.ItemName(sold)} · {SoulCampaign.SellPrice(sold)} 금화</size>", () => { Campaign.Sell(sold); selectedItem = null; }), 52, SoulIconSet.Ui("gold"));
        }
    }
}
