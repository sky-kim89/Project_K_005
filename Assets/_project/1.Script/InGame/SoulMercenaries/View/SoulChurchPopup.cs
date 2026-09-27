using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // ── 성당: 축복 세 가지(위), 부상당한 용병(아래) ──────────────────

    public sealed class SoulChurchPopup : SoulBuildingPopup
    {
        protected override SoulBuildingKind Kind => SoulBuildingKind.Church;
        int selected = -1;

        protected override void BuildBody(RectTransform body, int tab)
        {
            var (mainArea, sideArea) = SoulKit.Split(body, 520);
            var main = SoulKit.Scroll(mainArea);

            SoulKit.Section(main, "축복", Campaign.Blessing != null ? SoulUi.Colored(Campaign.Blessing.Name, SoulUi.Good) : null);
            var blessings = SoulKit.Grid(main, new Vector2(300, 190), 16);
            foreach (var blessing in SoulCampaign.Blessings) BlessingCard(blessings, blessing);

            SoulKit.Section(main, "용병", $"부상 {Campaign.Roster.FindAll(h => h.Wounds > 0).Count}");
            var hurt = Campaign.Roster.FindAll(h => h.Wounds > 0);
            if (hurt.Count > 0)
            {
                int all = Campaign.HealAllCost();
                var row = SoulUi.Row(main, 52, 8);
                var healAll = SoulKit.Action(row, $"모두 치료 ({hurt.Count}명) — {all} 금화", new Color(.75f, .65f, .3f), Campaign.Gold >= all,
                    () => Confirm("heal_all", $"부상당한 용병 {hurt.Count}명을 모두 치료하시겠습니까?\n<size=22>{all} 금화</size>", () => Campaign.HealAllAtChurch()), 52, SoulIconSet.Stat(StatType.MaxHp));
                SoulUi.Layout(healAll.gameObject, 360, 52);
            }
            var cards = SoulKit.Grid(main, new Vector2(150, 196), 12);
            if (selected >= Campaign.Roster.Count || (selected < 0 && hurt.Count > 0)) selected = hurt.Count > 0 ? Campaign.Roster.IndexOf(hurt[0]) : -1;
            for (int i = 0; i < Campaign.Roster.Count; i++)
            {
                int index = i;
                var hero = Campaign.Roster[i];
                HeroCard(cards, hero, i == selected, () => { selected = index; Refresh(); }, hero.Wounds > 0 ? $"부상 {hero.Wounds}" : null, SoulUi.Bad);
            }

            var side = SoulKit.Side(sideArea);
            if (selected < 0) return;
            var patient = Campaign.Roster[selected];
            HeroHeader(side, patient);
            var wounds = SoulUi.Row(side, 32, 8);
            SoulUi.Icon(wounds, SoulIconSet.Status(SoulStatus.Bleed), 28);
            SoulKit.Pips(wounds, patient.Wounds, SoulMercenary.MaxWounds, SoulUi.Bad, new Color(.25f, .2f, .22f), 16);
            SoulKit.Lines(side, WoundLines(patient));
            int cost = Campaign.HealCost(patient);
            SoulKit.Action(side, patient.Wounds > 0 ? $"치료 — {cost} 금화" : "부상 없음", new Color(.75f, .65f, .3f), patient.Wounds > 0 && Campaign.Gold >= cost,
                () => Campaign.HealAtChurch(patient), 52, SoulIconSet.Stat(StatType.MaxHp));
        }

        void BlessingCard(Transform parent, SoulBlessing blessing)
        {
            bool open = Campaign.Level(SoulBuildingKind.Church) >= blessing.ChurchLevel;
            bool mine = Campaign.Blessing == blessing;
            var card = SoulUi.Framed("Blessing", parent, SoulKit.CardBg, mine ? SoulUi.Good : open ? new Color(.85f, .75f, .4f) : SoulKit.Empty, mine ? 4 : 2);
            var icon = SoulUi.Icon(card, SoulIconSet.Stat(blessing.Bonuses[0].Stat), 56);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 1);
            icon.rectTransform.anchoredPosition = new Vector2(44, -44);
            icon.color = new Color(1, 1, 1, open ? 1 : .3f);
            var name = SoulUi.Text(card, blessing.Name, 22, open ? Color.white : SoulUi.SubText, TextAlignmentOptions.MidlineLeft);
            name.fontStyle = FontStyles.Bold;
            SoulUi.Place(name.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(84, -64), new Vector2(-10, -20));
            var text = SoulUi.Text(card, blessing.Description.Replace("다음 출정 동안 ", ""), 17, SoulUi.SubText, TextAlignmentOptions.TopLeft);
            SoulUi.Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 60), new Vector2(-12, -80));
            string label = mine ? "받음" : !open ? $"성당 {blessing.ChurchLevel}단계" : $"{blessing.Cost} 금화";
            var button = SoulUi.Button(card, label, mine ? SoulUi.Good : new Color(.75f, .65f, .3f), () => Campaign.Bless(blessing), 18);
            button.interactable = open && !mine && Campaign.Blessing == null && Campaign.Gold >= blessing.Cost;
            SoulUi.Place((RectTransform)button.transform, Vector2.zero, new Vector2(1, 0), new Vector2(14, 12), new Vector2(-14, 52));
        }
    }
}
