using TMPro;
using UnityEngine;

namespace SoulMercenaries
{
    // ── 게시판: notices pinned to the village board — grade, target, reward ──

    public sealed class SoulBoardPopup : SoulBuildingPopup
    {
        protected override SoulBuildingKind Kind => SoulBuildingKind.Board;
        protected override string[] Tabs => new[] { "의뢰" };

        static readonly Color Paper = new Color(.91f, .86f, .75f), Ink = new Color(.22f, .16f, .1f), Faded = new Color(.42f, .34f, .24f);
        static readonly Color[] GradeColors =
        {
            new Color(.55f, .55f, .55f), new Color(.3f, .62f, .32f), new Color(.25f, .45f, .8f), new Color(.6f, .32f, .78f), new Color(.9f, .66f, .12f),
        };

        protected override void BuildBody(RectTransform body, int tab)
        {
            var main = SoulKit.Scroll(body);
            SoulKit.Section(main, "게시판", $"진행 {Campaign.Active.Count}/{Campaign.QuestSlots}");
            var board = SoulKit.Grid(main, new Vector2(340, 196), 16);
            foreach (var quest in Campaign.Board.ToArray())
            {
                var taken = quest;
                Note(board, quest, "수락", Campaign.Active.Count < Campaign.QuestSlots, () => Campaign.Accept(taken));
            }
            if (Campaign.Active.Count == 0) return;
            SoulKit.Section(main, "진행 중");
            var active = SoulKit.Grid(main, new Vector2(340, 196), 16);
            foreach (var quest in Campaign.Active.ToArray())
            {
                var done = quest;
                Note(active, quest, quest.Done ? "보상 받기" : null, quest.Done, () => Campaign.Claim(done));
            }
        }

        void Note(Transform parent, SoulQuest quest, string action, bool enabled, UnityEngine.Events.UnityAction onClick)
        {
            var note = SoulUi.Panel("Note", parent, Paper, true).rectTransform;
            var pin = SoulUi.Panel("Pin", note, SoulUi.Bad, true);
            SoulUi.Place(pin.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-6, -14), new Vector2(6, -2));

            // grade: a coloured stamp in the corner
            var stamp = SoulUi.Panel("Grade", note, GradeColors[Mathf.Clamp(quest.Grade, 1, 5) - 1], true);
            SoulUi.Place(stamp.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -58), new Vector2(54, -14));
            var letter = SoulUi.Text(stamp.transform, quest.GradeName, 28, Color.white, TextAlignmentOptions.Center);
            letter.fontStyle = FontStyles.Bold;
            SoulUi.Stretch(letter.rectTransform);

            var title = SoulUi.Text(note, quest.Title, 19, Ink, TextAlignmentOptions.TopLeft);
            title.fontStyle = FontStyles.Bold;
            SoulUi.Place(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(64, -64), new Vector2(-10, -16));
            var target = SoulUi.Text(note, $"{quest.Floor}층 {SoulFloorTheme.For(quest.Floor).Name}", 16, Faded, TextAlignmentOptions.TopLeft);
            SoulUi.Place(target.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(64, -90), new Vector2(-10, -66));

            // reward: gold, then the extra with its icon
            var gold = SoulUi.Text(note, $"{Campaign.QuestReward(quest)} 금화", 18, new Color(.55f, .38f, .05f), TextAlignmentOptions.MidlineLeft);
            SoulUi.Place(gold.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -124), new Vector2(-10, -98));
            string bonus = Campaign.BonusName(quest);
            if (bonus != null)
            {
                var icon = SoulUi.Icon(note, Campaign.BonusIcon(quest), 24);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 1);
                icon.rectTransform.anchoredPosition = new Vector2(26, -136);
                var extra = SoulUi.Text(note, "+ " + bonus, 16, Ink, TextAlignmentOptions.MidlineLeft);
                SoulUi.Place(extra.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(42, -148), new Vector2(-10, -124));
            }

            if (Campaign.Active.Contains(quest))
            {
                var bar = SoulUi.Bar(note, SoulUi.Good, 10);
                SoulUi.Place(bar.parent as RectTransform, new Vector2(0, 0), new Vector2(.55f, 0), new Vector2(14, 22), new Vector2(0, 32));
                SoulUi.SetRatio(bar, quest.Progress / (float)Mathf.Max(1, quest.Goal));
                var progress = SoulUi.Text(note, $"{quest.Progress}/{quest.Goal}", 15, Ink, TextAlignmentOptions.MidlineLeft);
                SoulUi.Place(progress.rectTransform, new Vector2(0, 0), new Vector2(.55f, 0), new Vector2(14, 34), new Vector2(0, 52));
            }
            if (action != null)
            {
                var button = SoulUi.Button(note, action, enabled ? new Color(.2f, .5f, .28f) : new Color(.5f, .45f, .38f), onClick, 17);
                button.interactable = enabled;
                SoulUi.Place((RectTransform)button.transform, new Vector2(.58f, 0), new Vector2(1, 0), new Vector2(0, 10), new Vector2(-10, 48));
            }
        }
    }
}
