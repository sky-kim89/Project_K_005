using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // ── 창고: 보관품과 출정 준비를 한 화면에 ─────────────────────────
    // What is in store: supplies, gear, kept souls. What each party takes down is set in the guild (준비물).

    public sealed class SoulStoragePopup : SoulBuildingPopup
    {
        protected override SoulBuildingKind Kind => SoulBuildingKind.Storage;
        readonly HashSet<SoulItem> marked = new HashSet<SoulItem>(); // picked for 일괄 판매

        protected override void BuildBody(RectTransform body, int tab)
        {
            var (mainArea, sideArea) = SoulKit.Split(body, 600);
            var main = SoulKit.Scroll(mainArea);

            SoulKit.Section(main, "소모품");
            var supplies = SoulKit.Grid(main, new Vector2(92, 92), 10);
            foreach (var supply in SoulSupplies.All)
            {
                int count = Campaign.Supply(supply.Id);
                if (count <= 0) continue;
                var def = supply;
                var tile = SoulKit.Tile(supplies, SoulIconSet.Ui(supply.Icon), 92, supply.Rare ? SoulUi.Accent : SoulUi.PanelBorder, $"{count}", null, false, false);
                SoulTooltip.Attach(tile, SoulIconSet.Ui(def.Icon), def.Name, () => new List<SoulLine> { new SoulLine(null, def.Description), new SoulLine(null, $"보유 {Campaign.Supply(def.Id)}") });
            }

            marked.RemoveWhere(item => !Campaign.Inventory.Contains(item));
            SoulKit.Section(main, "장비", $"{Campaign.Inventory.Count}/{Campaign.StorageCapacity}");
            var gear = SoulKit.Grid(main, new Vector2(92, 92), 10);
            foreach (var item in Sorted(Campaign.Inventory))
            {
                var one = item;
                ItemTile(gear, item, 92, marked.Contains(item), () => { if (!marked.Remove(one)) marked.Add(one); Refresh(); });
            }

            if (Campaign.Vault.Count > 0)
            {
                SoulKit.Section(main, "영혼", $"{Campaign.Vault.Count}");
                var souls = SoulKit.Grid(main, new Vector2(92, 92), 10);
                foreach (var soul in Campaign.Vault)
                {
                    var kept = soul;
                    var tile = SoulKit.SoulTile(souls, soul, 92, SoulUi.Soul, $"{soul.Grade}");
                    SoulTooltip.Attach(tile, SoulKit.SoulIcon(kept), $"{kept.OriginMonster}의 영혼", () => SoulLinesFor(kept));
                }
            }

            var side = SoulKit.Side(sideArea);
            SellPanel(side);
        }

        // A grade button in the grade's colour: outlined when there is something to pick, filled when all of it is picked,
        // faded when the storehouse has none of that grade.
        void GradeChip(Transform row, int grade, bool any, bool all, List<SoulItem> plain)
        {
            var shade = SoulItemRules.GradeColor(grade);
            var fill = all ? shade * .85f : any ? new Color(shade.r * .22f, shade.g * .22f, shade.b * .22f, 1) : new Color(.06f, .065f, .1f, 1);
            fill.a = 1;
            var frame = any ? shade : new Color(shade.r, shade.g, shade.b, .3f);
            var chip = SoulUi.Framed("Grade", row, fill, frame, all ? 3 : 2);
            SoulUi.Layout(chip.gameObject, -1, 40, 1);
            var label = SoulUi.Text(chip, SoulItemRules.GradeName(grade), 18,
                all ? new Color(.06f, .06f, .08f) : any ? shade : new Color(shade.r, shade.g, shade.b, .35f), TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            SoulUi.Stretch(label.rectTransform);
            if (!any) return;
            chip.GetComponent<Image>().raycastTarget = true;
            SoulUi.Clickable(chip.gameObject).onClick.AddListener(() =>
            {
                if (all) foreach (var item in plain) marked.Remove(item); else foreach (var item in plain) marked.Add(item);
                Refresh();
            });
        }

        // 일괄 판매: pick by hand (a tap on a piece), or at once — whatever nobody would wear, or a whole grade — then sell
        // the lot. Unique pieces and those with enchants or special options are never picked at once (a tap still does).
        void SellPanel(Transform side)
        {
            SoulKit.Section(side, "일괄 판매", marked.Count > 0 ? $"{marked.Count}개" : null);
            bool shop = Campaign.Level(SoulBuildingKind.Shop) > 0;
            var quick = SoulUi.Row(side, 44, 8);
            var surplus = SoulKit.Action(quick, "안 쓰는 장비", SoulUi.TabInactive, Campaign.Inventory.Exists(Campaign.Surplus),
                () => { foreach (var item in Campaign.Inventory) if (Campaign.Surplus(item)) marked.Add(item); Refresh(); }, 44);
            SoulUi.Layout(surplus.gameObject, -1, 44, 1);
            SoulTooltip.Attach(surplus, SoulIconSet.Ui("gold"), "안 쓰는 장비", () => new List<SoulLine> { new SoulLine(null, "용병단 누구에게도 더 나은 장비가 아닌 것 (전용·부여·특수 옵션 장비 제외)") });
            var clear = SoulKit.Action(quick, "선택 해제", SoulUi.TabInactive, marked.Count > 0, () => { marked.Clear(); Refresh(); }, 44);
            SoulUi.Layout(clear.gameObject, 130, 44);
            // by grade: every grade shown in its colour; a tap picks every plain piece of that grade (again: lets them go)
            var row = SoulUi.Row(side, 40, 6);
            for (int grade = 1; grade <= SoulItemRules.MaxGrade; grade++)
            {
                int g = grade;
                var plain = Campaign.Inventory.FindAll(item => item.Grade == g && !item.Data.Unique && item.Enchants.Count == 0 && item.Specials.Count == 0);
                GradeChip(row, g, plain.Count > 0, plain.Count > 0 && plain.TrueForAll(marked.Contains), plain);
            }
            // the lot and its price
            int gold = 0; foreach (var item in marked) gold += SoulCampaign.SellPrice(item);
            if (marked.Count > 0)
            {
                var picked = SoulKit.Grid(side, new Vector2(56, 56), 6);
                foreach (var item in Sorted(new List<SoulItem>(marked))) { var one = item; ItemTile(picked, item, 56, false, () => { marked.Remove(one); Refresh(); }); }
            }
            var total = SoulUi.Text(side, SoulUi.Colored($"{gold} 금화", SoulUi.Accent) + SoulDescribe.Small($"  {marked.Count}개"), 26, Color.white, TextAlignmentOptions.MidlineLeft);
            SoulUi.Layout(total.gameObject, -1, 40);
            SoulKit.Action(side, !shop ? "상점이 있어야 팔 수 있습니다" : marked.Count > 0 ? $"일괄 판매 — {gold} 금화" : "일괄 판매",
                new Color(.55f, .38f, .1f), shop && marked.Count > 0,
                () => Confirm("bulksell", $"장비 {marked.Count}개를 판매할까요?\n<size=22>{gold} 금화</size>", () => { Campaign.SellMany(marked); marked.Clear(); }), 52, SoulIconSet.Ui("gold"));
        }
    }
}

