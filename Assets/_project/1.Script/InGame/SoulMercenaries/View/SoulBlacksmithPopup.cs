using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // ── 대장간: 용병의 장비를 골라 모루에 올린다 ─────────────────────
    // Left: the company. Right: the chosen mercenary's paper doll (as in the guild's 장비); a piece picked on it goes on
    // the anvil — its enchant sockets (click one to keep it through re-rolls, level 3) and the hammer; an empty slot
    // opens the guild's swap screen to put something on. Only worn gear is enchanted: the storehouse's pieces are not.

    public sealed class SoulBlacksmithPopup : SoulBuildingPopup
    {
        protected override SoulBuildingKind Kind => SoulBuildingKind.Blacksmith;
        SoulMercenary forHero;  // whose doll is shown
        SoulItem selected;      // the piece on the anvil (null: the doll)

        protected override void BuildBody(RectTransform body, int tab)
        {
            if (forHero != null && !Campaign.Roster.Contains(forHero)) forHero = null;
            if (forHero == null && Campaign.Roster.Count > 0) forHero = Campaign.Roster[0];
            if (selected != null && (forHero == null || !forHero.Equipment.Contains(selected))) selected = null;
            var (mainArea, sideArea) = SoulKit.Split(body, 560);
            var main = SoulKit.Scroll(mainArea);

            SoulKit.Section(main, "용병", $"{Campaign.Roster.Count}");
            var cards = SoulKit.Grid(main, new Vector2(150, 196), 12);
            foreach (var hero in Campaign.Roster)
            {
                var one = hero;
                HeroCard(cards, hero, hero == forHero, () => { forHero = one; selected = null; slotOpen = false; pending = pendingReplace = null; Refresh(); }, Campaign.PartyOf(hero)?.Name, SoulUi.SubText);
            }

            var side = SoulKit.Side(sideArea);
            if (forHero == null) return;
            if (slotOpen) { SlotSwap(side, forHero); return; }
            if (selected == null)
            {
                HeroHeader(side, forHero);
                Paperdoll(side, forHero, (slot, index) => false, (slot, index, shown) =>
                {
                    if (shown != null) selected = shown; // a piece: onto the anvil
                    else { gearSlot = slot; gearAccessory = index; pending = pendingReplace = null; slotOpen = true; } // empty: put one on
                    Refresh();
                });
                return;
            }
            Anvil(side, selected, forHero);
        }

        // The piece on the anvil: its sockets, 부여 / 재부여, the odds of each tier.
        void Anvil(Transform side, SoulItem item, SoulMercenary owner)
        {
            var back = SoulKit.Action(side, $"{owner.Name}의 장비", SoulUi.TabInactive, true, () => { selected = null; Refresh(); }, 40, SoulIconSet.Ui("fold"));
            SoulUi.Layout(back.gameObject, -1, 40);
            var tile = ItemTile(side, item, 96, false, null, owner);
            SoulKit.Header(side, tile, SoulDescribe.ItemName(item), SoulDescribe.ItemSummary(item, Campaign.Rules));

            SoulKit.Section(side, "부여", $"{item.Enchants.Count}/{item.EnchantSlots}");
            var sockets = SoulKit.Grid(side, new Vector2(118, 118), 10);
            for (int i = 0; i < item.EnchantSlots; i++)
            {
                int index = i;
                if (i >= item.Enchants.Count) { EmptySeat(sockets, SoulIconSet.Ui("skill")); continue; }
                var enchant = item.Enchants[i];
                var icon = EnchantIcon(enchant);
                var socket = SoulKit.Tile(sockets, icon, 118, TierColor(enchant.Tier), SoulItemRules.TierNames[enchant.Tier], TierColor(enchant.Tier), false, false,
                    Campaign.SmithLevel >= 3 ? (UnityEngine.Events.UnityAction)(() => Campaign.ToggleEnchantLock(item, index)) : null);
                if (enchant.Locked)
                {
                    var lockIcon = SoulUi.Icon(socket, SoulIconSet.Ui("lock"), 30);
                    lockIcon.rectTransform.anchorMin = lockIcon.rectTransform.anchorMax = new Vector2(0, 1);
                    lockIcon.rectTransform.anchoredPosition = new Vector2(20, -20);
                }
                var text = SoulUi.Text(socket, SoulItemRules.EnchantText(enchant), 13, Color.white, TextAlignmentOptions.Top);
                SoulUi.Place(text.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(4, -26), new Vector2(-4, -4));
                SoulTooltip.Attach(socket, icon, SoulItemRules.EnchantText(enchant), () => new List<SoulLine>
                {
                    new SoulLine(null, $"{SoulItemRules.TierNames[enchant.Tier]}" + (enchant.Locked ? " · 고정" : "")),
                    new SoulLine(SoulIconSet.Ui("lock"), Campaign.SmithLevel >= 3 ? "누르면 고정 / 해제 (재부여 때 남음)" : "대장간 3단계: 옵션 고정"),
                });
            }

            string enchantBlock = Campaign.EnchantBlock(item);
            SoulKit.Action(side, enchantBlock ?? $"부여 — {Campaign.EnchantCost(item)} 금화", new Color(.75f, .45f, .15f), enchantBlock == null,
                () => { Campaign.Enchant(item, out _); Message = null; }, 56, SoulIconSet.Ui("skill"));
            if (Campaign.SmithLevel >= 2)
            {
                string rerollBlock = Campaign.RerollBlock(item);
                SoulKit.Action(side, rerollBlock ?? $"재부여 — {Campaign.RerollCost(item)} 금화", new Color(.5f, .3f, .12f), rerollBlock == null,
                    () => { Campaign.Reroll(item, out _); Message = null; }, 48, SoulIconSet.Ui("follow"));
            }
            // the odds of each value tier at this smithy, as a coloured strip
            var chances = SoulItemRules.TierChances(Campaign.SmithLevel, item.Rerolls);
            var odds = System.Array.ConvertAll(chances, c => Mathf.RoundToInt(c * 100));
            var strip = SoulUi.Row(side, 26, 2);
            for (int t = 0; t < odds.Length; t++)
            {
                var part = SoulUi.Panel("Odds", strip, TierColor(t), false);
                SoulUi.Layout(part.gameObject, odds[t] * 4.6f, 26);
                var label = SoulUi.Text(part.transform, $"{odds[t]}%", 14, Color.black, TextAlignmentOptions.Center);
                SoulUi.Stretch(label.rectTransform);
                int tier = t;
                SoulTooltip.Attach(part, SoulIconSet.Ui("skill"), SoulItemRules.TierNames[tier], () => new List<SoulLine> { new SoulLine(null, $"{odds[tier]}%") });
            }
        }
    }
}
