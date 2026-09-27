using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // 원정 기록: the reports of finished expeditions, newest first — filtered by party along the top of the list. The
    // one picked: how it ended, the deepest floor, the gold; each mercenary's fight (kills, damage dealt and taken,
    // evasions, guards) and growth (levels, stats); everything found on the way.
    public sealed class SoulReportPanel
    {
        const float Width = 1480, Height = 860, ListWidth = 340, HeaderHeight = 96;

        readonly RectTransform shade, filters, list, detail;
        int filter; // 0: every party, n: party n
        SoulTripReport shown;

        SoulCampaign Campaign => SoulCampaign.Current;

        // A thin gold rule across the box, `y` from its top.
        static void Line(RectTransform box, float y)
        {
            var line = SoulUi.Panel("Rule", box, new Color(1f, .78f, .3f, .35f));
            line.raycastTarget = false;
            SoulUi.Place(line.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, y - 1), new Vector2(-24, y + 1));
        }
        public bool Visible => shade.gameObject.activeSelf;

        public SoulReportPanel(Transform root)
        {
            var dim = SoulUi.Panel("Reports", root, new Color(0, 0, 0, .72f));
            dim.raycastTarget = true;
            shade = dim.rectTransform;
            SoulUi.Stretch(shade);
            var box = SoulUi.Framed("Box", shade, SoulUi.PanelBg, SoulUi.Accent, 3);
            SoulUi.Place(box, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-Width / 2, -Height / 2), new Vector2(Width / 2, Height / 2));
            var body = SoulUi.Panel("Body", box, new Color(.045f, .05f, .09f, 1f), true); // solid: the frame's gold does not show through
            body.raycastTarget = false;
            SoulUi.Stretch(body.rectTransform, 3);
            Line(box, -HeaderHeight - 7);

            var header = SoulUi.Panel("Header", box, SoulUi.HeaderBg, true).rectTransform;
            SoulUi.Place(header, new Vector2(0, 1), Vector2.one, new Vector2(6, -HeaderHeight), new Vector2(-6, -6));
            var mark = SoulUi.Icon(header, SoulIconSet.Ui("map"), 60);
            SoulUi.Place(mark.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(22, -30), new Vector2(82, 30));
            var title = SoulUi.Text(header, "원정 기록", 36, SoulUi.Accent);
            title.fontStyle = FontStyles.Bold;
            SoulUi.Place(title.rectTransform, Vector2.zero, new Vector2(.6f, 1), new Vector2(100, 0), Vector2.zero);
            var close = SoulUi.Button(header, "닫기", SoulUi.TabInactive, Hide, 22);
            SoulUi.Place((RectTransform)close.transform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-160, -28), new Vector2(-18, 28));

            filters = SoulUi.Rect("Filters", box);
            SoulUi.Place(filters, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -HeaderHeight - 64), new Vector2(20 + ListWidth, -HeaderHeight - 16));
            SoulUi.Grid(filters.gameObject, new Vector2(46, 46), new Vector2(6, 6));
            var left = SoulUi.Rect("List", box);
            SoulUi.Place(left, Vector2.zero, new Vector2(0, 1), new Vector2(20, 20), new Vector2(20 + ListWidth, -HeaderHeight - 72));
            list = SoulKit.Scroll(left);
            list.GetComponent<VerticalLayoutGroup>().spacing = 8;
            var right = SoulUi.Rect("Detail", box);
            SoulUi.Place(right, Vector2.zero, Vector2.one, new Vector2(ListWidth + 40, 20), new Vector2(-20, -HeaderHeight - 16));
            detail = SoulKit.Side(right);
            shade.gameObject.SetActive(false);
        }

        // Opens on `focus` (or the newest); everything waiting to be read counts as read.
        public void Show(SoulTripReport focus = null)
        {
            if (Campaign == null) return;
            shown = focus ?? (Campaign.Reports.Count > 0 ? Campaign.Reports[0] : null);
            filter = 0;
            bool unread = false;
            foreach (var report in Campaign.Reports) if (!report.Seen) { report.Seen = true; unread = true; }
            shade.gameObject.SetActive(true);
            shade.SetAsLastSibling();
            if (unread) Campaign.Touch(); // saved as read
            Refresh();
        }

        public void Hide() => shade.gameObject.SetActive(false);

        public void Refresh()
        {
            if (!Visible || Campaign == null) return;
            SoulUi.Clear(filters);
            Filter(0, "전체");
            var numbers = new SortedSet<int>();
            foreach (var report in Campaign.Reports) numbers.Add(report.Party);
            foreach (int number in numbers) Filter(number, $"{number}");
            SoulUi.Clear(list);
            var reports = Campaign.Reports.FindAll(r => filter == 0 || r.Party == filter);
            if (shown == null || !reports.Contains(shown)) shown = reports.Count > 0 ? reports[0] : null;
            foreach (var report in reports) Entry(report);
            SoulUi.Clear(detail);
            if (shown != null) Detail(shown);
        }

        void Filter(int number, string label)
        {
            var button = SoulUi.Button(filters, label, filter == number ? SoulUi.TabActive : SoulUi.TabInactive, () => { filter = number; Refresh(); }, number == 0 ? 16 : 20);
            if (number == 0) SoulUi.Layout(button.gameObject, 46, 46);
        }

        static Color OutcomeColor(SoulTripReport report)
            => report.Outcome == "전멸" ? SoulUi.Bad : report.Heroes.Exists(h => !h.Alive) ? SoulUi.Accent : SoulUi.Good;

        void Entry(SoulTripReport report)
        {
            bool picked = report == shown;
            var card = SoulUi.Framed("Report", list, SoulKit.CardBg, picked ? SoulUi.Accent : SoulUi.PanelBorder, picked ? 3 : 2);
            SoulUi.Layout(card.gameObject, -1, 78);
            card.GetComponent<Image>().raycastTarget = true;
            SoulUi.Clickable(card.gameObject).onClick.AddListener(() => { shown = report; Refresh(); });
            var name = SoulUi.Text(card, $"<b>{report.Party}파티</b>  <size=17><color=#{SoulUi.Hex(SoulUi.SubText)}>{report.Day}일차 · {report.Floor}층</color></size>", 22, Color.white, TextAlignmentOptions.TopLeft);
            SoulUi.Place(name.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(14, -38), new Vector2(-10, -8));
            var outcome = SoulUi.Text(card, report.Outcome, 17, OutcomeColor(report), TextAlignmentOptions.BottomLeft);
            SoulUi.Place(outcome.rectTransform, Vector2.zero, new Vector2(.6f, 0), new Vector2(14, 8), new Vector2(0, 34));
            var gold = SoulUi.Text(card, $"+{report.Gold}", 17, SoulUi.Accent, TextAlignmentOptions.BottomRight);
            SoulUi.Place(gold.rectTransform, new Vector2(.6f, 0), new Vector2(1, 0), new Vector2(0, 8), new Vector2(-12, 34));
        }

        void Detail(SoulTripReport report)
        {
            var title = SoulUi.Text(detail, $"<b>{report.Party}파티</b>   {SoulUi.Colored(report.Outcome, OutcomeColor(report))}", 30, Color.white);
            SoulUi.Layout(title.gameObject, -1, 44);
            int kills = 0, levels = 0;
            foreach (var hero in report.Heroes) { kills += hero.Kills; levels += Mathf.Max(0, hero.LevelTo - hero.LevelFrom); }
            var totals = SoulUi.Row(detail, 44, 10);
            Stat(totals, SoulIconSet.Ui("map"), $"{report.Floor}층", "도달한 층", Color.white);
            Stat(totals, SoulIconSet.Ui("gold"), $"+{report.Gold}", "획득 금화", SoulUi.Accent);
            Stat(totals, SoulIconSet.Ui("kills"), $"{kills}", "처치", Color.white);
            Stat(totals, SoulIconSet.Ui("level"), $"+{levels}", "레벨업", SoulUi.Good);
            Stat(totals, SoulIconSet.Ui("equipment"), $"{report.Found.Count}", "전리품", Color.white);

            SoulKit.Section(detail, "용병", $"{report.Heroes.FindAll(h => h.Alive).Count}/{report.Heroes.Count}");
            foreach (var hero in report.Heroes) Hero(hero);

            SoulKit.Section(detail, "전리품", $"{report.Found.Count}");
            var found = SoulUi.Flow(detail, new Vector2(300, 42), 8);
            foreach (var find in report.Found) Find(found, find);
        }

        static void Stat(Transform parent, Sprite icon, string value, string name, Color color)
        {
            var chip = SoulKit.IconValue(parent, icon, value, color, 44);
            SoulTooltip.Attach(chip, icon, name, () => new List<SoulLine>());
        }

        // A mercenary: portrait, name and level, its fight in chips, the stats it gained.
        void Hero(SoulTripHero hero)
        {
            var card = SoulUi.Framed("Hero", detail, SoulKit.CardBg, hero.Alive ? SoulUi.PanelBorder : SoulUi.Bad, 2);
            SoulUi.Layout(card.gameObject, -1, 132);
            // the mercenary at home or away again; gone, the look its record kept
            var member = Campaign.FindHero(hero.Id);
            var record = member == null ? Campaign.Records.Find(r => r.Id == hero.Id) : null;
            var sprite = member != null ? SoulPortraits.For(member) : record != null ? SoulPortraits.For(record.Id, record.Look) : null;
            var face = SoulUi.Portrait(card, sprite, 104, SoulIconSet.Ui("race"));
            SoulUi.Place(face, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(14, -52), new Vector2(118, 52));
            if (!hero.Alive) face.gameObject.AddComponent<CanvasGroup>().alpha = .45f;

            string level = hero.LevelTo > hero.LevelFrom ? $"Lv.{hero.LevelFrom} → {SoulUi.Colored($"Lv.{hero.LevelTo}", SoulUi.Good)}" : $"Lv.{hero.LevelTo}";
            var name = SoulUi.Text(card, $"<b>{hero.Name}</b>   <size=19>{level}</size>" + (hero.Alive ? "" : "   " + SoulUi.Colored(Campaign.Permadeath ? "사망" : "쓰러짐", SoulUi.Bad)), 23, Color.white, TextAlignmentOptions.TopLeft);
            SoulUi.Place(name.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(134, -40), new Vector2(-12, -10));

            var fight = SoulUi.Row(card, 36, 8);
            SoulUi.Place(fight, new Vector2(0, 1), Vector2.one, new Vector2(134, -82), new Vector2(-12, -46));
            Chip(fight, SoulIconSet.Ui("kills"), $"{hero.Kills}", "처치", Color.white);
            Chip(fight, SoulIconSet.Stat(StatType.Attack), $"{hero.Dealt:0}", "준 피해", SoulUi.Accent);
            Chip(fight, SoulIconSet.Stat(StatType.MaxHp), $"{hero.Taken:0}", "받은 피해", SoulUi.Bad);
            Chip(fight, SoulIconSet.Stat(StatType.Evasion), $"{hero.Evasions}", "회피", Color.white);
            Chip(fight, SoulIconSet.Stat(StatType.Armor), $"{hero.Guards}", "막기", Color.white);

            var gains = SoulUi.Row(card, 32, 6);
            SoulUi.Place(gains, Vector2.zero, new Vector2(1, 0), new Vector2(134, 10), new Vector2(-12, 42));
            foreach (var gain in hero.Gains)
                Chip(gains, SoulIconSet.Stat(gain.Stat), $"+{gain.Amount}", SoulDescribe.StatName(Campaign.Rules, gain.Stat), SoulUi.Good, 32);
        }

        static void Chip(Transform parent, Sprite icon, string value, string name, Color color, float height = 36)
        {
            var chip = SoulKit.IconValue(parent, icon, value, color, height);
            SoulTooltip.Attach(chip, icon, name, () => new List<SoulLine>());
        }

        static readonly string[] FindIcons = { "equipment", "skill", "soul", "potion" };

        static void Find(Transform parent, SoulTripFind find)
        {
            var color = find.Kind == SoulFindKind.Item ? SoulItemRules.GradeColor(find.Grade) : find.Kind == SoulFindKind.Soul ? SoulUi.Soul : Color.white;
            string label = find.Kind == SoulFindKind.Book ? $"스킬북 '{find.Name}'" : find.Name;
            if (find.Worn) label += SoulUi.Colored(" (장착)", SoulUi.SubText);
            var chip = SoulUi.Chip(parent, SoulIconSet.Ui(FindIcons[(int)find.Kind]), label, color, 42);
            var text = chip.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) { text.fontSize = 17; text.overflowMode = TextOverflowModes.Ellipsis; }
        }
    }
}
