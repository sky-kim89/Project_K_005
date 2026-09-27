using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // ── 추모비: 쓰러진 이들의 이름을 새긴 비석, 함께한 모든 용병의 도감 ──────

    public sealed class SoulMemorialPopup : SoulBuildingPopup
    {
        protected override SoulBuildingKind Kind => SoulBuildingKind.Memorial;
        static readonly Color Stone = new Color(.42f, .41f, .46f), StoneDark = new Color(.3f, .29f, .34f), Carved = new Color(.9f, .88f, .8f);

        protected override void BuildBody(RectTransform body, int tab)
        {
            var main = SoulKit.Scroll(body);
            var fallen = Campaign.Records.FindAll(r => r.Status == SoulRecordStatus.Fallen);

            // the tablet: the fallen, engraved
            var holder = SoulUi.Rect("TabletRow", main);
            SoulUi.Layout(holder.gameObject, -1, 330);
            var tablet = SoulUi.Framed("Tablet", holder, Stone, StoneDark, 6);
            tablet.anchorMin = tablet.anchorMax = new Vector2(.5f, 1);
            tablet.pivot = new Vector2(.5f, 1);
            tablet.sizeDelta = new Vector2(1000, 320);
            tablet.anchoredPosition = Vector2.zero;
            var title = SoulUi.Text(tablet, "여기, 돌아오지 못한 이들을 기억한다", 26, Carved, TextAlignmentOptions.Top);
            title.fontStyle = FontStyles.Bold;
            SoulUi.Place(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -60), new Vector2(0, -20));
            var rule = SoulUi.Panel("Rule", tablet, StoneDark);
            SoulUi.Place(rule.rectTransform, new Vector2(.2f, 1), new Vector2(.8f, 1), new Vector2(0, -68), new Vector2(0, -64));
            var names = SoulUi.Rect("Names", tablet);
            SoulUi.Place(names, Vector2.zero, Vector2.one, new Vector2(40, 60), new Vector2(-40, -80));
            var grid = names.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(220, 52);
            grid.spacing = new Vector2(12, 8);
            grid.childAlignment = TextAnchor.UpperCenter;
            if (fallen.Count == 0)
            {
                var none = SoulUi.Text(names, "—", 30, Carved, TextAlignmentOptions.Center);
            }
            foreach (var record in fallen)
            {
                var plate = SoulUi.Text(names, $"<b>{record.Name}</b>\n<size=15>{record.Job} · Lv.{record.Level} · {record.FellOnFloor}층</size>", 20, Carved, TextAlignmentOptions.Center);
                plate.raycastTarget = true;
                var shown = record;
                SoulTooltip.Attach(plate, SoulIconSet.Ui("kills"), record.Name, () => RecordLines(shown));
            }
            foreach (float x in new[] { .06f, .94f }) Candle(tablet, x);

            // the codex: everyone who served
            SoulKit.Section(main, "용병 도감", $"{Campaign.Records.Count}명 · 전사 {fallen.Count}");
            var cards = SoulKit.Grid(main, new Vector2(150, 206), 12);
            var records = new List<SoulMercenaryRecord>(Campaign.Records);
            records.Sort((a, b) => a.Status == b.Status ? b.Level.CompareTo(a.Level) : a.Status.CompareTo(b.Status));
            foreach (var record in records) RecordCard(cards, record);
        }

        static void Candle(RectTransform tablet, float x)
        {
            var wax = SoulUi.Panel("Candle", tablet, new Color(.95f, .92f, .82f));
            SoulUi.Place(wax.rectTransform, new Vector2(x, 0), new Vector2(x, 0), new Vector2(-7, 20), new Vector2(7, 60));
            var flame = SoulUi.Panel("Flame", tablet, new Color(1f, .8f, .35f), true);
            SoulUi.Place(flame.rectTransform, new Vector2(x, 0), new Vector2(x, 0), new Vector2(-5, 62), new Vector2(5, 80));
        }

        void RecordCard(Transform parent, SoulMercenaryRecord record)
        {
            bool dead = record.Status == SoulRecordStatus.Fallen;
            var live = Campaign.FindHero(record.Id); // at home or away
            var card = SoulUi.Framed("Record", parent, SoulKit.CardBg, dead ? new Color(.5f, .2f, .22f) : record.Status == SoulRecordStatus.Serving ? SoulUi.PanelBorder : SoulKit.Empty, 2);
            var sprite = live != null ? SoulPortraits.For(live) : SoulPortraits.For(record.Id, record.Look);
            var face = SoulUi.Portrait(card, sprite, 118, SoulIconSet.Ui("race"), 2.4f, .34f);
            face.anchorMin = face.anchorMax = face.pivot = new Vector2(.5f, 1);
            face.anchoredPosition = new Vector2(0, -8);
            face.sizeDelta = new Vector2(118, 118);
            if (dead) foreach (var image in face.GetComponentsInChildren<Image>()) if (image.name == "Face") image.color = new Color(.55f, .55f, .6f);
            var name = SoulUi.Text(card, $"<b>{record.Name}</b>", 19, Color.white, TextAlignmentOptions.Top);
            SoulUi.Place(name.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(4, 44), new Vector2(-4, 70));
            var sub = SoulUi.Text(card, $"Lv.{record.Level} {record.Job}", 15, SoulUi.SubText, TextAlignmentOptions.Top);
            SoulUi.Place(sub.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(4, 24), new Vector2(-4, 46));
            string stamp = dead ? $"{record.FellOnFloor}층 전사" : record.Status == SoulRecordStatus.Serving ? "재직" : "해고";
            var pill = SoulUi.Panel("Stamp", card, dead ? SoulUi.Bad : record.Status == SoulRecordStatus.Serving ? SoulUi.Good : SoulUi.SubText, true);
            SoulUi.Place(pill.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-52, 3), new Vector2(52, 23));
            var text = SoulUi.Text(pill.transform, stamp, 14, Color.black, TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            SoulUi.Stretch(text.rectTransform);
            SoulTooltip.Attach(card, SoulIconSet.Ui(dead ? "kills" : "race"), record.Name, () => RecordLines(record));
        }

        static List<SoulLine> RecordLines(SoulMercenaryRecord record) => new List<SoulLine>
        {
            new SoulLine(SoulIconSet.Ui("race"), $"{record.Race} {record.Job} · Lv.{record.Level} · {record.Grade}등급"),
            new SoulLine(SoulIconSet.Ui("exit"), $"출정 {record.Expeditions}회"),
            new SoulLine(SoulIconSet.Ui("kills"), $"처치 {record.Kills}"),
            new SoulLine(SoulIconSet.Ui("party"), $"{record.JoinedAt + 1}번째 출정 무렵 합류"),
        };
    }
}
