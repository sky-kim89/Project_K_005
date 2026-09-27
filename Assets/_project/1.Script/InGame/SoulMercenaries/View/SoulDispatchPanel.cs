using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // The portal's dispatch (출정). Left: the open parties as cards — a click looks at one, its 출정 button picks it
    // to go. Right: the provisions (준비물) of the one looked at, kind by kind: what is given leaves the store at
    // once, and whatever the party brings back up goes back on the shelf. 보내기 sends the picked parties down.
    public sealed class SoulDispatchPanel
    {
        const float Width = 1480, Height = 860, SideWidth = 560, HeaderHeight = 104, FooterHeight = 92;
        static readonly Color Go = new Color(.2f, .5f, .28f);

        readonly RectTransform shade, parties, provisions;
        readonly TextMeshProUGUI subtitle, summary;
        readonly Button sendButton;
        readonly HashSet<SoulParty> chosen = new HashSet<SoulParty>();
        readonly Action<List<SoulExpedition>> onSent;
        SoulParty focus;

        SoulCampaign Campaign => SoulCampaign.Current;

        // A thin gold rule across the box, `y` from its top.
        static void Line(RectTransform box, float y)
        {
            var line = SoulUi.Panel("Rule", box, new Color(1f, .78f, .3f, .35f));
            line.raycastTarget = false;
            SoulUi.Place(line.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, y - 1), new Vector2(-24, y + 1));
        }
        public bool Visible => shade.gameObject.activeSelf;

        public SoulDispatchPanel(Transform root, Action<List<SoulExpedition>> onSent, Action openReports)
        {
            this.onSent = onSent;
            var dim = SoulUi.Panel("Dispatch", root, new Color(0, 0, 0, .72f));
            dim.raycastTarget = true;
            shade = dim.rectTransform;
            SoulUi.Stretch(shade);
            var box = SoulUi.Framed("Box", shade, SoulUi.PanelBg, SoulUi.Accent, 3);
            SoulUi.Place(box, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-Width / 2, -Height / 2), new Vector2(Width / 2, Height / 2));
            var body = SoulUi.Panel("Body", box, new Color(.045f, .05f, .09f, 1f), true); // solid: the frame's gold does not show through
            body.raycastTarget = false;
            SoulUi.Stretch(body.rectTransform, 3);
            Line(box, -HeaderHeight - 7);
            Line(box, FooterHeight - Height + 2);

            // header: the gate, the title, when a party sent now comes back; the records on the right
            var header = SoulUi.Panel("Header", box, SoulUi.HeaderBg, true).rectTransform;
            SoulUi.Place(header, new Vector2(0, 1), Vector2.one, new Vector2(6, -HeaderHeight), new Vector2(-6, -6));
            var glow = SoulUi.Panel("Glow", header, new Color(.55f, .35f, .95f, .18f), true);
            glow.raycastTarget = false;
            SoulUi.Place(glow.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(14, -42), new Vector2(98, 42));
            var gate = SoulUi.Icon(header, SoulSprites.Object(SoulObjectKind.Escape), 80, "Gate");
            SoulUi.Place(gate.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(16, -40), new Vector2(96, 40));
            var title = SoulUi.Text(header, "출정", 38, SoulUi.Accent, TextAlignmentOptions.BottomLeft);
            title.fontStyle = FontStyles.Bold;
            SoulUi.Place(title.rectTransform, new Vector2(0, .5f), new Vector2(.6f, 1), new Vector2(114, -2), new Vector2(0, -8));
            subtitle = SoulUi.Text(header, "", 19, SoulUi.SubText, TextAlignmentOptions.TopLeft);
            SoulUi.Place(subtitle.rectTransform, Vector2.zero, new Vector2(.75f, .5f), new Vector2(116, 8), new Vector2(0, -4));
            var records = SoulUi.Button(header, "원정 기록", SoulUi.TabInactive, () => openReports(), 20, SoulIconSet.Ui("map"));
            SoulUi.Place((RectTransform)records.transform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-226, -28), new Vector2(-18, 28));

            // body: the parties | the provisions of the one looked at
            var left = SoulUi.Rect("Parties", box);
            SoulUi.Place(left, Vector2.zero, Vector2.one, new Vector2(20, FooterHeight), new Vector2(-SideWidth - 36, -HeaderHeight - 14));
            parties = SoulKit.Scroll(left);
            parties.GetComponent<VerticalLayoutGroup>().spacing = 10;
            var right = SoulUi.Rect("Provisions", box);
            SoulUi.Place(right, new Vector2(1, 0), Vector2.one, new Vector2(-SideWidth - 20, FooterHeight), new Vector2(-20, -HeaderHeight - 14));
            provisions = SoulKit.Side(right);

            // footer: who goes, and the buttons
            var footer = SoulUi.Row(box, 62, 12);
            SoulUi.Place(footer, Vector2.zero, new Vector2(1, 0), new Vector2(24, 18), new Vector2(-24, 80));
            summary = SoulUi.Text(footer, "", 22, Color.white);
            SoulUi.Layout(summary.gameObject, -1, -1, 1);
            sendButton = SoulUi.Button(footer, "보내기", Go, Send, 24, SoulIconSet.Ui("exit"));
            SoulUi.Layout(sendButton.gameObject, 220, 62);
            var close = SoulUi.Button(footer, "닫기", SoulUi.TabInactive, Hide, 22);
            SoulUi.Layout(close.gameObject, 140, 62);
            shade.gameObject.SetActive(false);
        }

        public void Show()
        {
            chosen.Clear();
            focus = null;
            shade.gameObject.SetActive(true);
            shade.SetAsLastSibling();
            Refresh();
        }

        public void Hide() => shade.gameObject.SetActive(false);

        public void Refresh()
        {
            if (!Visible || Campaign == null) return;
            chosen.RemoveWhere(party => !Campaign.Parties.Contains(party) || Campaign.DepartBlock(party) != null);
            if (focus == null || !Campaign.Parties.Contains(focus))
                focus = Campaign.Parties.Find(party => Campaign.DepartBlock(party) == null) ?? Campaign.Parties[0];
            float clock = Campaign.Clock, back = SoulClock.ReturnTime(clock);
            subtitle.text = $"{SoulClock.Text(clock)}   ·   지금 출정하면 {SoulClock.HourMinute(back)} 귀환 (던전 {SoulClock.DungeonHoursLeft(clock, back):0}시간)";
            SoulUi.Clear(parties);
            foreach (var party in Campaign.Parties) Card(party);
            SoulUi.Clear(provisions);
            Provisions(focus);
            int people = 0;
            foreach (var party in chosen) people += party.Members.Count;
            summary.text = chosen.Count > 0 ? $"<b>{chosen.Count}파티</b> · {people}명 출정" : "";
            sendButton.interactable = chosen.Count > 0;
        }

        // A party: name, aim, 전투력 or why it cannot go; faces; its provisions; the 출정 toggle on the right.
        void Card(SoulParty party)
        {
            string block = Campaign.DepartBlock(party);
            bool ready = block == null, pick = chosen.Contains(party), looked = party == focus;
            var card = SoulUi.Framed("Party", parties, SoulKit.CardBg, looked ? SoulUi.Accent : SoulUi.PanelBorder, looked ? 4 : 2);
            SoulUi.Layout(card.gameObject, -1, 176);
            card.GetComponent<Image>().raycastTarget = true;
            SoulUi.Clickable(card.gameObject).onClick.AddListener(() => { focus = party; Refresh(); });
            if (pick)
            {
                var band = SoulUi.Panel("Picked", card, new Color(.25f, .6f, .32f, .9f), true);
                band.raycastTarget = false;
                SoulUi.Place(band.rectTransform, Vector2.zero, new Vector2(0, 1), new Vector2(4, 6), new Vector2(12, -6));
            }
            var content = SoulUi.Rect("Content", card);
            SoulUi.Place(content, Vector2.zero, Vector2.one, new Vector2(20, 0), new Vector2(-150, 0));
            if (!ready) content.gameObject.AddComponent<CanvasGroup>().alpha = .5f;

            var name = SoulUi.Text(content, $"<b>{party.Name}</b>   <size=18><color=#{SoulUi.Hex(SoulUi.SubText)}>{party.TargetFloor}층 {SoulParty.ModeName(party.TargetMode)}</color></size>",
                24, Color.white, TextAlignmentOptions.TopLeft);
            SoulUi.Place(name.rectTransform, new Vector2(0, 1), new Vector2(.65f, 1), new Vector2(0, -42), new Vector2(0, -10));
            string state = ready ? $"전투력 {party.Power}" : Campaign.IsAway(party) ? "원정 중" : party.Members.Count == 0 ? "빈 파티" : block;
            var note = SoulUi.Text(content, state, 19, ready ? SoulUi.Good : Campaign.IsAway(party) ? SoulUi.Accent : SoulUi.SubText, TextAlignmentOptions.TopRight);
            SoulUi.Place(note.rectTransform, new Vector2(.5f, 1), Vector2.one, new Vector2(0, -42), new Vector2(0, -12));

            var faces = SoulUi.Row(content, 72, 8);
            SoulUi.Place(faces, new Vector2(0, 1), Vector2.one, new Vector2(0, -122), new Vector2(0, -50));
            foreach (var member in party.Members) SoulUi.Portrait(faces, SoulPortraits.For(member), 72, SoulIconSet.Ui("race"));

            var kit = SoulUi.Row(content, 34, 6);
            SoulUi.Place(kit, Vector2.zero, new Vector2(1, 0), new Vector2(0, 12), new Vector2(0, 46));
            foreach (var entry in party.Carry)
            {
                var def = SoulSupplies.Get(entry.Id);
                if (def == null) continue;
                var chip = SoulUi.Chip(kit, SoulIconSet.Ui(def.Icon), $"{entry.Count}", Color.white, 34);
                SoulUi.Layout(chip.gameObject, 70, 34);
            }

            var toggle = SoulUi.Button(card, pick ? "출정" : "대기", pick ? Go : SoulUi.TabInactive, () =>
            {
                if (!chosen.Remove(party)) chosen.Add(party);
                focus = party;
                Refresh();
            }, 22, pick ? SoulIconSet.Ui("exit") : null);
            SoulUi.Place((RectTransform)toggle.transform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-134, -32), new Vector2(-16, 32));
            toggle.interactable = ready;
        }

        // Potions first, then the scrolls and tools; each with what is in the store and what the party holds.
        void Provisions(SoulParty party)
        {
            bool away = Campaign.IsAway(party);
            SoulKit.Section(provisions, $"준비물 · {party.Name}", $"{party.Carry.Count}/{Campaign.CarryKinds}종 · 종류당 {Campaign.CarryLimit}개");
            var kinds = new List<SoulSupply>();
            foreach (var def in SoulSupplies.All)
                if (SoulSupplies.Packed(def.Id) && (Campaign.Supply(def.Id) > 0 || Campaign.Carried(party, def.Id) > 0)) kinds.Add(def);
            kinds.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : a.Priority.CompareTo(b.Priority));
            var grid = SoulKit.Grid(provisions, new Vector2(160, 186), 10);
            foreach (var def in kinds) Tile(grid, party, def, away);
        }

        void Tile(Transform parent, SoulParty party, SoulSupply def, bool away)
        {
            int held = Campaign.Carried(party, def.Id), stock = Campaign.Supply(def.Id);
            var cell = SoulUi.Framed("Provision", parent, SoulKit.CardBg, held > 0 ? SoulUi.Accent : SoulUi.PanelBorder, held > 0 ? 3 : 2);
            cell.GetComponent<Image>().raycastTarget = true;
            var icon = SoulUi.Icon(cell, SoulIconSet.Ui(def.Icon), 64);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f, 1);
            icon.rectTransform.anchoredPosition = new Vector2(0, -48);
            var store = SoulUi.Text(cell, $"창고 {stock}", 15, stock > 0 ? SoulUi.SubText : SoulUi.Bad, TextAlignmentOptions.TopLeft);
            SoulUi.Place(store.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(8, -26), new Vector2(-4, -4));
            var name = SoulUi.Text(cell, def.Name, 15, Color.white, TextAlignmentOptions.Center);
            SoulUi.Place(name.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(4, 46), new Vector2(-4, 72));
            SoulTooltip.Attach(cell, SoulIconSet.Ui(def.Icon), def.Name, () => new List<SoulLine> { new SoulLine(null, def.Description) });

            var counter = SoulUi.Row(cell, 36, 4);
            SoulUi.Place(counter, Vector2.zero, new Vector2(1, 0), new Vector2(8, 8), new Vector2(-8, 44));
            var minus = SoulUi.Button(counter, "−", SoulUi.TabInactive, () => Campaign.SetCarryCount(party, def.Id, held - 1), 22);
            SoulUi.Layout(minus.gameObject, 40, 36);
            minus.interactable = !away && held > 0;
            var count = SoulUi.Text(counter, $"{held}", 22, held > 0 ? SoulUi.Accent : SoulUi.SubText, TextAlignmentOptions.Center);
            count.fontStyle = FontStyles.Bold;
            SoulUi.Layout(count.gameObject, -1, 36, 1);
            bool room = held > 0 || party.Carry.Count < Campaign.CarryKinds;
            var plus = SoulUi.Button(counter, "+", SoulUi.TabInactive, () => Campaign.SetCarryCount(party, def.Id, held + 1), 22);
            SoulUi.Layout(plus.gameObject, 40, 36);
            plus.interactable = !away && room && held < Campaign.CarryMax(party, def.Id);
        }

        void Send()
        {
            var sent = new List<SoulExpedition>();
            foreach (var party in Campaign.Parties)
                if (chosen.Contains(party) && Campaign.DepartBlock(party) == null)
                {
                    var trip = Campaign.Depart(party, Campaign.DaySeed);
                    if (trip != null) sent.Add(trip);
                }
            chosen.Clear();
            Hide();
            onSent(sent);
        }
    }
}
