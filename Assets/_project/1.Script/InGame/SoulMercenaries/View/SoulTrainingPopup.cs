using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // ── 훈련소: 훈련 카드, 대련장 ────────────────────────────────

    public sealed class SoulTrainingPopup : SoulBuildingPopup
    {
        protected override SoulBuildingKind Kind => SoulBuildingKind.Training;
        protected override string[] Tabs => new[] { "훈련", "대련장" };
        int selected, sparFloor = 1;
        SoulSparResult spar;

        protected override void BuildBody(RectTransform body, int tab)
        {
            if (tab == 1) { Sparring(body); return; }
            var (mainArea, sideArea) = SoulKit.Split(body, 520);
            var main = SoulKit.Scroll(mainArea);
            if (Campaign.Roster.Count == 0) return;
            selected = Mathf.Clamp(selected, 0, Campaign.Roster.Count - 1);
            var hero = Campaign.Roster[selected];

            SoulKit.Section(main, "용병");
            var cards = SoulKit.Grid(main, new Vector2(150, 196), 12);
            for (int i = 0; i < Campaign.Roster.Count; i++)
            {
                int index = i;
                var who = Campaign.Roster[i];
                bool training = Campaign.IsTraining(who);
                var card = HeroCard(cards, who, i == selected, () => { selected = index; Refresh(); }, training ? "훈련" : Campaign.AutoTraining(who) ? "자동" : null, SoulUi.Accent);
                if (training) SoulAttackLoop.Attach(card, who);
            }

            SoulKit.Section(main, "훈련");
            var drills = SoulKit.Grid(main, new Vector2(250, 150), 14);
            foreach (var training in SoulCampaign.Trainings) TrainingCard(drills, hero, training);

            var side = SoulKit.Side(sideArea);
            var header = HeroHeader(side, hero);
            if (Campaign.IsTraining(hero)) SoulAttackLoop.Attach(header, hero);
            // 자동 훈련 at the right end of the header
            bool auto = Campaign.AutoTraining(hero);
            var autoButton = SoulUi.Button(header, "자동 훈련", auto ? new Color(.2f, .5f, .28f) : SoulUi.TabInactive, () => Campaign.ToggleAutoTrain(hero), 18, SoulIconSet.Ui("follow"));
            SoulUi.Layout(autoButton.gameObject, 140, 44);
            SoulTooltip.Attach(autoButton, SoulIconSet.Ui("follow"), auto ? "자동 훈련: 켜짐" : "자동 훈련: 꺼짐", () =>
            {
                var next = Campaign.AutoPick(hero);
                return new List<SoulLine>
                {
                    new SoulLine(null, "마을에서 쉬는 동안 성장 성향에 맞는 능력치 훈련을 알아서 합니다. 오늘 출정 전인 파티원, 자정을 넘기는 훈련, 패턴 수련은 하지 않으며 금화 " + SoulCampaign.AutoTrainReserve + "은 남겨 둡니다."),
                    new SoulLine(SoulIconSet.Ui("level"), next != null ? $"다음: {next.Name} — {Campaign.TrainingCost(hero, next)} 금화" : "할 수 있는 훈련이 없습니다"),
                };
            });
            var offer = Campaign.PatternOffer(hero);
            if (offer != null)
            {
                SoulKit.Section(side, "패턴 수련");
                foreach (var pattern in offer)
                {
                    var chosen = pattern;
                    var entry = Entry(side, SoulDescribe.PatternIcon(pattern), $"<b>{pattern.Id}</b>  <size=16><color=#{SoulUi.Hex(SoulUi.SubText)}>{SoulDescribe.Category(pattern.Category)}</color></size>",
                        "습득", true, () => Campaign.ChoosePattern(hero, chosen));
                    SoulTooltip.Attach(entry, SoulDescribe.PatternIcon(pattern), SoulDescribe.PatternTitle(pattern), () => SoulMercenaryPopup.PatternTooltip(hero, chosen, Campaign.Rules));
                }
            }
            HeroStats(side, hero);
            HeroKit(side, hero);
        }

        void TrainingCard(Transform parent, SoulMercenary hero, SoulTraining training)
        {
            string block = Campaign.TrainBlock(hero, training);
            bool locked = Campaign.Level(SoulBuildingKind.Training) < training.Level;
            int times = Campaign.TimesTrained(hero, training), cost = Campaign.TrainingCost(hero, training);
            var card = SoulUi.Framed("Drill", parent, SoulKit.CardBg, locked ? SoulKit.Empty : SoulUi.PanelBorder, 2);
            var icon = SoulUi.Icon(card, TrainingIcon(training), 48);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 1);
            icon.rectTransform.anchoredPosition = new Vector2(36, -36);
            icon.color = new Color(1, 1, 1, locked ? .3f : 1);
            string count = training.LearnPattern ? (times > 0 ? $"×{times}" : "") : $"{times}/{Campaign.TrainingCap}";
            var name = SoulUi.Text(card, training.Name + (count.Length > 0 ? $"  <size=15><color=#{SoulUi.Hex(SoulUi.Accent)}>{count}</color></size>" : ""), 19, locked ? SoulUi.SubText : Color.white, TextAlignmentOptions.MidlineLeft);
            name.fontStyle = FontStyles.Bold;
            SoulUi.Place(name.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(70, -54), new Vector2(-8, -14));
            var effect = SoulUi.Text(card, $"{training.Description}\n{Campaign.TrainingMinutes(hero, training):0}분", 16, SoulUi.SubText, TextAlignmentOptions.TopLeft);
            SoulUi.Place(effect.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 56), new Vector2(-8, -62));
            var button = SoulUi.Button(card, locked ? $"훈련소 {training.Level}단계" : $"{cost} 금화", new Color(.2f, .42f, .7f), () => { Campaign.Train(hero, training, out _); Message = null; }, 17);
            button.interactable = block == null;
            SoulUi.Place((RectTransform)button.transform, Vector2.zero, new Vector2(1, 0), new Vector2(10, 10), new Vector2(-10, 48));
            var drill = Campaign.DrillOf(hero);
            if (drill != null && drill.Training == training)
                SoulLiveText.Bind(button.GetComponentInChildren<TMP_Text>(), () => Remaining(drill.Left(Campaign.Clock)));
            if (block != null && !locked) SoulTooltip.Attach(button, SoulIconSet.Ui("lock"), training.Name, () => new List<SoulLine> { new SoulLine(null, block) });
        }

        // The first party at home (none: nobody to spar with); with the guild's 합동 대련, the first two together.
        List<SoulMercenary> SparParty
        {
            get
            {
                var list = new List<SoulMercenary>();
                int parties = Campaign.PerkLevel(SoulPerk.JointSparring) > 0 ? 2 : 1;
                foreach (var party in Campaign.Parties)
                {
                    if (parties == 0) break;
                    if (party.Members.Count == 0 || Campaign.IsAway(party)) continue;
                    list.AddRange(party.Members); parties--;
                }
                return list;
            }
        }

        // 대련장 (3단계): the party fights a chosen opponent on copies of itself — no wounds, no deaths, no pay.
        void Sparring(RectTransform body)
        {
            var (mainArea, sideArea) = SoulKit.Split(body, 720);
            var main = SoulKit.Scroll(mainArea);
            if (Campaign.Level(SoulBuildingKind.Training) < 3) { SoulKit.Section(main, "대련장", "훈련소 3단계"); return; }
            int maxFloor = 1;
            foreach (var hero in SparParty) maxFloor = Mathf.Max(maxFloor, hero.HighestFloorCleared + 1);
            maxFloor = Mathf.Min(maxFloor, SoulDungeonSession.FinalFloor);
            sparFloor = Mathf.Clamp(sparFloor, 1, maxFloor);

            SoulKit.Section(main, "층", $"적 ×{SoulDungeonSession.FloorPower(sparFloor):0.#}");
            var floors = SoulKit.Grid(main, new Vector2(64, 52), 8);
            for (int floor = 1; floor <= maxFloor; floor++)
            {
                int f = floor;
                var button = SoulUi.Button(floors, $"{f}층", f == sparFloor ? SoulUi.TabActive : SoulUi.TabInactive, () => { sparFloor = f; Refresh(); }, 18);
            }
            SoulKit.Section(main, "상대");
            var opponents = SoulKit.Grid(main, new Vector2(260, 180), 16);
            foreach (SoulSparOpponent kind in System.Enum.GetValues(typeof(SoulSparOpponent)))
            {
                var opponent = kind;
                var card = SoulUi.Framed("Opponent", opponents, SoulKit.CardBg, kind == SoulSparOpponent.Boss ? SoulUi.Soul : kind == SoulSparOpponent.Elite ? SoulUi.Bad : SoulUi.PanelBorder, 2);
                var icon = SoulUi.Icon(card, SoulIconSet.Ui(kind == SoulSparOpponent.Boss ? "vault" : kind == SoulSparOpponent.Elite ? "kills" : "party"), 64);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f, 1);
                icon.rectTransform.anchoredPosition = new Vector2(0, -48);
                var name = SoulUi.Text(card, SoulSparring.OpponentName(Campaign.Data.Dungeon, kind, sparFloor), 22, Color.white, TextAlignmentOptions.Center);
                name.fontStyle = FontStyles.Bold;
                SoulUi.Place(name.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 58), new Vector2(0, 92));
                var start = SoulUi.Button(card, "대련", new Color(.2f, .42f, .7f),
                    () => spar = SoulSparring.Run(Campaign, SparParty, opponent, sparFloor, System.Environment.TickCount & int.MaxValue), 18, SoulIconSet.Ui("kills"));
                start.interactable = SparParty.Count > 0;
                SoulUi.Place((RectTransform)start.transform, Vector2.zero, new Vector2(1, 0), new Vector2(14, 10), new Vector2(-14, 50));
            }

            var side = SoulKit.Side(sideArea);
            if (spar == null) { SoulKit.Section(side, "결과"); return; }
            SoulKit.Section(side, spar.Won ? "승리" : spar.Seconds >= SoulSparring.MaxSeconds ? "시간 초과" : "패배", $"{spar.Opponent} · {spar.Seconds:0.0}초");
            float most = 1;
            foreach (var line in spar.Lines) most = Mathf.Max(most, Mathf.Max(line.Dealt, line.Taken));
            foreach (var line in spar.Lines) ResultRow(side, line, most);
        }

        // One mercenary's fight: portrait, dealt / taken as bars, evasion and guard counts, what it used most.
        void ResultRow(Transform side, SoulSparLine line, float most)
        {
            var who = Campaign.Roster.Find(h => h.Name == line.Name);
            var row = SoulUi.Panel("Result", side, line.Downed > 0 ? new Color(.2f, .08f, .1f) : SoulKit.CardBg, true).rectTransform;
            SoulUi.Layout(row.gameObject, -1, 104);
            var face = SoulUi.Portrait(row, who != null ? SoulPortraits.For(who) : null, 84, SoulIconSet.Ui("race"));
            face.anchorMin = face.anchorMax = new Vector2(0, .5f);
            face.anchoredPosition = new Vector2(52, 0);
            var name = SoulUi.Text(row, line.Name, 18, Color.white, TextAlignmentOptions.TopLeft);
            name.fontStyle = FontStyles.Bold;
            SoulUi.Place(name.rectTransform, new Vector2(0, 1), new Vector2(.4f, 1), new Vector2(104, -30), new Vector2(0, -6));
            BarLine(row, SoulIconSet.Stat(StatType.Attack), line.Dealt / most, SoulUi.Accent, $"{line.Dealt:0}", -38);
            BarLine(row, SoulIconSet.Stat(StatType.MaxHp), line.Taken / most, SoulUi.HpColor, $"{line.Taken:0}", -62);
            var counts = SoulUi.Text(row, $"회피 {line.Evasions} · 막기 {line.Guards}", 15, SoulUi.SubText, TextAlignmentOptions.TopLeft);
            SoulUi.Place(counts.rectTransform, new Vector2(0, 1), new Vector2(.6f, 1), new Vector2(104, -100), new Vector2(0, -80));
            // most used moves as icons
            var used = new List<KeyValuePair<string, int>>(line.Used);
            used.Sort((a, b) => b.Value.CompareTo(a.Value));
            var moves = SoulUi.Rect("Moves", row);
            SoulUi.Place(moves, new Vector2(.62f, 0), Vector2.one, new Vector2(0, 8), new Vector2(-8, -8));
            var grid = SoulKit.Grid(moves, new Vector2(40, 40), 4);
            SoulUi.Stretch(grid);
            for (int i = 0; i < Mathf.Min(6, used.Count); i++)
            {
                string key = used[i].Key;
                Sprite icon = SoulIconSet.Ui("skill");
                var pattern = who?.OwnedPatterns().Find(p => p.Id == key);
                if (pattern != null) icon = SoulDescribe.PatternIcon(pattern);
                var skill = who?.AllActiveSkills().Find(s => s.SkillName == key);
                if (skill != null && skill.Icon != null) icon = skill.Icon;
                var tile = SoulKit.Tile(grid, icon, 40, SoulUi.PanelBorder, $"{used[i].Value}");
                SoulTooltip.Attach(tile, icon, key, () => new List<SoulLine> { new SoulLine(null, $"{used.Find(u => u.Key == key).Value}회 사용") });
            }
        }

        static void BarLine(RectTransform row, Sprite icon, float ratio, Color color, string value, float y)
        {
            var image = SoulUi.Icon(row, icon, 20);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0, 1);
            image.rectTransform.anchoredPosition = new Vector2(116, y);
            var fill = SoulUi.Bar(row, color, 10);
            var back = (RectTransform)fill.parent;
            SoulUi.Place(back, new Vector2(0, 1), new Vector2(.5f, 1), new Vector2(132, y - 5), new Vector2(0, y + 5));
            SoulUi.SetRatio(fill, Mathf.Clamp01(ratio));
            var text = SoulUi.Text(row, value, 15, Color.white, TextAlignmentOptions.MidlineLeft);
            SoulUi.Place(text.rectTransform, new Vector2(.5f, 1), new Vector2(.62f, 1), new Vector2(6, y - 10), new Vector2(0, y + 10));
        }

        // Game minutes left as h:mm.
        static string Remaining(float minutes)
        {
            int left = Mathf.CeilToInt(minutes);
            return $"{left / 60}:{left % 60:00}";
        }

        static Sprite TrainingIcon(SoulTraining training)
            => training.LearnPattern ? SoulIconSet.Ui("skill") : SoulIconSet.Stat(training.Stat);
    }
}
