using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // ── 영혼 제단: 보관한 영혼(구슬)과 용병, 흡수·분리·합성 ─────────────
    // Pick a kept soul and a mercenary: absorb. Pick a soul the mercenary carries: take it back out.
    // Three of the same kept soul: fuse into its refined form.

    public sealed class SoulAltarPopup : SoulBuildingPopup
    {
        protected override SoulBuildingKind Kind => SoulBuildingKind.SoulAltar;
        int selectedHero;
        SoulData kept, carried; // a soul from the vault, or one the selected mercenary carries

        protected override void BuildBody(RectTransform body, int tab)
        {
            if (kept != null && !Campaign.Vault.Contains(kept)) kept = null;
            if (Campaign.Roster.Count == 0) return;
            selectedHero = Mathf.Clamp(selectedHero, 0, Campaign.Roster.Count - 1);
            var hero = Campaign.Roster[selectedHero];
            if (carried != null && !hero.Souls.Contains(carried)) carried = null;
            var (mainArea, sideArea) = SoulKit.Split(body, 560);
            var main = SoulKit.Scroll(mainArea);

            SoulKit.Section(main, "보관한 영혼", $"{Campaign.Vault.Count}");
            var orbs = SoulKit.Grid(main, new Vector2(96, 96), 10);
            var kinds = new List<SoulData>();
            foreach (var soul in Campaign.Vault) if (!kinds.Contains(soul)) kinds.Add(soul);
            foreach (var soul in kinds)
            {
                var one = soul;
                int count = Campaign.Vault.FindAll(s => s == soul).Count;
                var tile = SoulKit.SoulTile(orbs, soul, 96, SoulUi.Soul, count > 1 ? $"×{count}" : $"{soul.Grade}", null, kept == soul, false,
                    () => { kept = one; carried = null; Refresh(); });
                SoulTooltip.Attach(tile, SoulKit.SoulIcon(one), $"{one.OriginMonster}의 영혼", () => SoulLinesFor(one));
            }

            SoulKit.Section(main, "용병");
            var cards = SoulKit.Grid(main, new Vector2(150, 196), 12);
            for (int i = 0; i < Campaign.Roster.Count; i++)
            {
                int index = i;
                var who = Campaign.Roster[i];
                HeroCard(cards, who, i == selectedHero, () => { selectedHero = index; carried = null; Refresh(); }, $"{who.Souls.Count}/{who.SoulSlots}", SoulUi.Soul);
            }

            var side = SoulKit.Side(sideArea);
            HeroHeader(side, hero);
            SoulKit.Section(side, "영혼 칸", $"{hero.Souls.Count}/{hero.SoulSlots}");
            var slots = SoulKit.Grid(side, new Vector2(84, 84), 8);
            for (int i = 0; i < hero.SoulSlots; i++)
            {
                if (i >= hero.Souls.Count) { EmptySeat(slots, SoulIconSet.Ui("soul")); continue; }
                var soul = hero.Souls[i];
                var tile = SoulKit.SoulTile(slots, soul, 84, SoulUi.Soul, $"{soul.Grade}", null, carried == soul, false, () => { carried = soul; kept = null; Refresh(); });
                SoulTooltip.Attach(tile, SoulKit.SoulIcon(soul), $"{soul.OriginMonster}의 영혼", () => SoulLinesFor(soul));
            }

            if (carried != null)
            {
                SoulKit.Header(side, SoulKit.SoulTile(side, carried, 96, SoulUi.Soul), $"{carried.OriginMonster}의 영혼", $"{carried.Grade}등급");
                SoulKit.Lines(side, SoulLinesFor(carried));
                var soul = carried;
                int cost = SoulCampaign.ReleaseCost(soul);
                SoulKit.Action(side, $"분리 — {cost} 금화", new Color(.45f, .3f, .6f), Campaign.Gold >= cost, () => { Campaign.Release(hero, soul); carried = null; }, 50);
                return;
            }
            if (kept == null) return;
            var chosen = kept;
            SoulKit.Header(side, SoulKit.SoulTile(side, chosen, 96, SoulUi.Soul), $"{chosen.OriginMonster}의 영혼", $"{chosen.Grade}등급");
            SoulKit.Lines(side, SoulLinesFor(chosen));
            string absorbBlock = Campaign.AbsorbBlock(hero, chosen);
            SoulKit.Action(side, absorbBlock ?? $"{hero.Name}에게 흡수", new Color(.45f, .3f, .6f), absorbBlock == null, () => { Campaign.AbsorbAtAltar(hero, chosen); kept = null; }, 52, SoulIconSet.Ui("soul"));

            // fusion: three of these → one refined
            if (chosen.Refined == null) return;
            SoulKit.Section(side, "합성");
            var row = SoulUi.Row(side, 84, 8);
            int have = Campaign.Vault.FindAll(s => s == chosen).Count;
            for (int i = 0; i < SoulCampaign.FuseCount; i++)
                SoulKit.SoulTile(row, chosen, 72, i < have ? SoulUi.Soul : SoulKit.Empty, null, null, false, i >= have);
            var arrow = SoulUi.Text(row, "→", 34, SoulUi.Accent, TextAlignmentOptions.Center);
            SoulUi.Layout(arrow.gameObject, 40, 72);
            var result = SoulKit.SoulTile(row, chosen.Refined, 84, SoulUi.Accent, $"{chosen.Refined.Grade}");
            SoulTooltip.Attach(result, SoulKit.SoulIcon(chosen.Refined), $"{chosen.Refined.OriginMonster}의 영혼", () => SoulLinesFor(chosen.Refined));
            string fuseBlock = Campaign.FuseBlock(chosen);
            SoulKit.Action(side, fuseBlock ?? $"합성 — {Campaign.FuseCost(chosen)} 금화", new Color(.6f, .45f, .2f), fuseBlock == null, () => Campaign.Fuse(chosen), 48);
        }
    }
}
