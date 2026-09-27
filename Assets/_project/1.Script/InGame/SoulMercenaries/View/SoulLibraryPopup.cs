using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // ── 서고: 용병을 고르고, 그 용병의 스킬을 보며 스킬북을 쓴다 ─────────
    // Left: the company. Right: the chosen mercenary's skills (each at its own level), then the books on hand — a book
    // shows what it would do for this one (learn, a level up, the stronger form); picked, it is spent or sold.

    public sealed class SoulLibraryPopup : SoulBuildingPopup
    {
        protected override SoulBuildingKind Kind => SoulBuildingKind.Library;
        SoulMercenary reader;          // whose skills are shown
        SoulActiveSkillData book;      // the book picked

        static readonly Color Parchment = new Color(.85f, .75f, .5f);
        static Sprite Icon(SoulActiveSkillData skill) => skill.Icon != null ? skill.Icon : SoulIconSet.Ui("skill");

        protected override void BuildBody(RectTransform body, int tab)
        {
            if (reader != null && !Campaign.Roster.Contains(reader)) reader = null;
            if (reader == null && Campaign.Roster.Count > 0) reader = Campaign.Roster[0];
            if (book != null && !Campaign.Books.Contains(book)) book = null;
            var (mainArea, sideArea) = SoulKit.Split(body, 600);
            var main = SoulKit.Scroll(mainArea);

            SoulKit.Section(main, "용병", $"{Campaign.Roster.Count}");
            var cards = SoulKit.Grid(main, new Vector2(150, 196), 12);
            foreach (var hero in Campaign.Roster)
            {
                var one = hero;
                // how many of the books on hand this one could use now
                int usable = 0;
                foreach (var b in DistinctBooks()) if (Campaign.BookBlock(hero, b, out _) == null) usable++;
                HeroCard(cards, hero, hero == reader, () => { reader = one; Refresh(); }, usable > 0 ? $"책 {usable}" : null, SoulUi.Good);
            }

            var side = SoulKit.Side(sideArea);
            if (reader == null) return;
            HeroHeader(side, reader);

            var skills = SoulCampaign.KnownSkills(reader);
            SoulKit.Section(side, "보유 스킬", $"{skills.Count}");
            var grid = SoulKit.Grid(side, new Vector2(84, 84), 8);
            foreach (var skill in skills)
            {
                var known = skill;
                int level = SoulCampaign.HeroSkillLevel(reader, skill);
                var tile = SoulKit.Tile(grid, Icon(skill), 84, SoulUi.Accent, $"Lv{level}", level >= SoulCampaign.MaxSkillLevel ? SoulUi.Accent : Color.white, false, false,
                    () => { book = Campaign.Books.Contains(known) ? known : book; Refresh(); });
                SoulTooltip.Attach(tile, Icon(known), SoulDescribe.SkillTitle(reader, known), () => SoulDescribe.SkillLines(reader, known, Campaign.Rules));
            }

            var books = DistinctBooks();
            SoulKit.Section(side, "스킬북", $"{Campaign.Books.Count}");
            var shelf = SoulKit.Grid(side, new Vector2(84, 84), 8);
            foreach (var b in books)
            {
                var one = b;
                int count = Campaign.Books.FindAll(x => x == b).Count;
                string block = Campaign.BookBlock(reader, b, out var use);
                string badge = block != null ? null : use == SoulCampaign.BookUse.Learn ? "배움" : use == SoulCampaign.BookUse.LevelUp ? "Lv+1" : "강화";
                var tile = SoulKit.Tile(shelf, Icon(b), 84, Parchment, badge ?? (count > 1 ? $"×{count}" : null), block == null ? SoulUi.Good : Color.white, book == b, block != null,
                    () => { book = one; Refresh(); });
                SoulTooltip.Attach(tile, Icon(one), $"스킬북 '{one.SkillName}'" + (count > 1 ? $"  <size=18>×{count}</size>" : ""), () =>
                {
                    var lines = new List<SoulLine>();
                    string why = Campaign.BookBlock(reader, one, out var how);
                    lines.Add(new SoulLine(SoulIconSet.Ui(why == null ? "level" : "lock"), why == null ? UseText(one, how) : SoulUi.Colored(why, SoulUi.Bad)));
                    lines.AddRange(SoulDescribe.SkillLines(reader, one, Campaign.Rules));
                    return lines;
                });
            }

            if (book == null) return;
            // the book picked: what it does for this one, spend it or sell it
            string reason = Campaign.BookBlock(reader, book, out var action);
            var picked = book;
            var row = SoulUi.Row(side, 52, 10);
            var spend = SoulKit.Action(row, reason ?? UseText(picked, action), new Color(.2f, .42f, .7f), reason == null,
                () => Confirm("usebook", $"스킬북을 사용할까요?\n<size=22><color=#{SoulUi.Hex(SoulUi.Accent)}>{reader.Name}</color> · {UseText(picked, action)}</size>",
                    () => { Campaign.UseBook(reader, picked, out _); }), 52, SoulIconSet.Ui("level"));
            SoulUi.Layout(spend.gameObject, -1, 52, 1);
            var sell = SoulKit.Action(row, $"팔기 {SoulCampaign.BookPrice(picked)}", SoulUi.TabInactive, true, () => { Campaign.SellBook(picked); }, 52, SoulIconSet.Ui("gold"));
            SoulUi.Layout(sell.gameObject, 160, 52);
        }

        // Each kind of book once, in the order found.
        List<SoulActiveSkillData> DistinctBooks()
        {
            var list = new List<SoulActiveSkillData>();
            foreach (var b in Campaign.Books) if (b != null && !list.Contains(b)) list.Add(b);
            return list;
        }

        string UseText(SoulActiveSkillData skill, SoulCampaign.BookUse use)
        {
            int level = SoulCampaign.HeroSkillLevel(reader, skill);
            return use == SoulCampaign.BookUse.Learn ? $"'{skill.SkillName}' 배우기"
                : use == SoulCampaign.BookUse.LevelUp ? $"'{skill.SkillName}' Lv.{level} → {level + 1}"
                : $"'{skill.SkillName}' → '{skill.UpgradeTo?.SkillName}' 강화";
        }
    }
}
