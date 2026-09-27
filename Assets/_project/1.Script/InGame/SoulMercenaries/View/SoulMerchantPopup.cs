using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // ── 떠돌이 상인: 수레 위의 물건 ──────────────────────────────

    public sealed class SoulMerchantPopup : SoulBuildingPopup
    {
        protected override SoulBuildingKind Kind => SoulBuildingKind.Merchant;
        SoulMerchantOffer selected;
        static readonly Color Parchment = new Color(.85f, .75f, .5f);

        protected override void BuildBody(RectTransform body, int tab)
        {
            if (selected != null && !Campaign.MerchantStock.Contains(selected)) selected = null;
            if (selected == null && Campaign.MerchantStock.Count > 0) selected = Campaign.MerchantStock[0];
            var (mainArea, sideArea) = SoulKit.Split(body, 520);
            var main = SoulKit.Scroll(mainArea);
            SoulKit.Section(main, "수레", Campaign.MerchantHere ? $"{Campaign.MerchantStock.Count}" : "떠났습니다");
            var goods = SoulKit.Grid(main, new Vector2(110, 110), 12);
            foreach (var offer in Campaign.MerchantStock)
            {
                var chosen = offer;
                UnityEngine.Events.UnityAction pick = () => { selected = chosen; Refresh(); };
                if (offer.Item != null) ItemTile(goods, offer.Item, 110, selected == offer, pick, null, $"{offer.Price}", SoulUi.Accent);
                else if (offer.Book != null)
                {
                    var tile = SoulKit.Tile(goods, offer.Book.Icon != null ? offer.Book.Icon : SoulIconSet.Ui("skill"), 110, Parchment, $"{offer.Price}", SoulUi.Accent, selected == offer, false, pick);
                    SoulTooltip.Attach(tile, offer.Book.Icon, $"스킬북 '{offer.Book.SkillName}'", () => new List<SoulLine> { new SoulLine(null, offer.Book.Description ?? "") });
                }
                else
                {
                    var supply = SoulSupplies.Get(offer.Supply);
                    var tile = SoulKit.Tile(goods, SoulIconSet.Ui(supply.Icon), 110, supply.Rare ? SoulUi.Accent : SoulUi.PanelBorder, $"{offer.Price}", SoulUi.Accent, selected == offer, false, pick);
                    SoulTooltip.Attach(tile, SoulIconSet.Ui(supply.Icon), supply.Name, () => new List<SoulLine> { new SoulLine(null, supply.Description) });
                }
            }

            var side = SoulKit.Side(sideArea);
            if (selected == null) return;
            if (selected.Item != null) ItemDetail(side, selected.Item);
            else if (selected.Book != null)
            {
                var tile = SoulKit.Tile(side, selected.Book.Icon != null ? selected.Book.Icon : SoulIconSet.Ui("skill"), 88, Parchment);
                SoulKit.Header(side, tile, $"스킬북 '{selected.Book.SkillName}'", $"{selected.Book.BookTier}급 스킬북");
                if (!string.IsNullOrEmpty(selected.Book.Description)) SoulKit.Lines(side, new[] { new SoulLine(null, selected.Book.Description) });
            }
            else
            {
                var supply = SoulSupplies.Get(selected.Supply);
                var tile = SoulKit.Tile(side, SoulIconSet.Ui(supply.Icon), 88, supply.Rare ? SoulUi.Accent : SoulUi.PanelBorder);
                SoulKit.Header(side, tile, supply.Name, $"보유 {Campaign.Supply(supply.Id)}");
                SoulKit.Lines(side, new[] { new SoulLine(null, supply.Description) });
            }
            var bought = selected;
            SoulKit.Action(side, $"구매 — {bought.Price} 금화", new Color(.2f, .5f, .28f), Campaign.Gold >= bought.Price, () => Confirm("buy", BuyQuestion(bought.Item != null ? SoulDescribe.ItemName(bought.Item) : bought.Book != null ? $"스킬북 '{bought.Book.SkillName}'" : SoulSupplies.Get(bought.Supply).Name, bought.Price), () => { Campaign.BuyFromMerchant(bought); selected = null; }), 52, SoulIconSet.Ui("gold"));
        }
    }
}
