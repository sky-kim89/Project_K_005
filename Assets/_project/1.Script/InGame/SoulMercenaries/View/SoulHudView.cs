using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // Always-on HUD (runtime uGUI). Details live in PopupManager popups (SoulMercenaryPopup, SoulMapPopup).
    //
    //  ┌── top bar: floor · time │ status · goal │ gold · stones · souls │ auto · aim · move │ souls · ⚙ · village ──┐
    //  │                                                              ┌ minimap ┐     │
    //  │                         map (camera)                         └─────────┘     │
    //  ├──── supply bar (pouch · belt)                                        keys ────┤
    //  ├──────────── party cards, centred (portrait · bars · status · level) ────────────┤
    public sealed class SoulHudView : MonoBehaviour
    {
        const float TopHeight = 76, BottomHeight = 206, MinimapWidth = 400, MinimapHeight = 270;

        sealed class PartyCard
        {
            public Image Background, Portrait;
            public TextMeshProUGUI Name, Action, HpText;
            public RectTransform Hp, Stamina, Mana, Statuses;
            public Button LevelUp;
            public Image Crown;
            public string StatusKey;
        }

        public SoulGameplayController Controller;
        public SoulWorldView World;

        RectTransform mapArea, minimapPanel, minimapBody;
        TextMeshProUGUI where, when, gold, preserve, vault, toastText, partyLabel;
        Button soulButton, toast, partyButton, foldButton, autoButton, aimButton, nextFloorButton;
        Button villageButton, againButton, homeButton, speedButton;
        RectTransform supplyBar;
        string supplyKey;
        readonly List<PartyCard> cards = new List<PartyCard>();
        GameObject result;
        TextMeshProUGUI resultTitle, resultBody;
        float toastTime;
        bool minimapFolded, resultShown;
        float cardWidth = 326;
        int knownStash, knownLoot;
        System.Action toastAction;

        SoulDungeonSession Session => Controller.Session;

        // Screen-pixel rect the world camera draws into (between the bars).
        public Rect MapScreenRect
        {
            get
            {
                if (mapArea == null) return new Rect(0, 0, Screen.width, Screen.height);
                var corners = new Vector3[4];
                mapArea.GetWorldCorners(corners);
                return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            }
        }

        public void Toast(string message, System.Action onClick = null)
        {
            if (toast == null) return;
            toastText.text = message;
            toastAction = onClick;
            toastTime = onClick != null ? 4f : 2f;
        }

        // Start, not Awake: the controller creates the session in its Awake.
        void Start()
        {
            var canvas = SoulUi.OverlayCanvas("HUD Canvas", 10, transform);
            var root = canvas.transform;
            mapArea = SoulUi.Rect("MapArea", root);
            SoulUi.Place(mapArea, Vector2.zero, Vector2.one, new Vector2(0, BottomHeight), new Vector2(0, -TopHeight));
            BuildTopBar(root);
            BuildSupplyBar(root);
            BuildMinimap(root);
            BuildBottomBar(root);
            BuildToast(root);
            BuildResult(root);
            BuildTripTabs(root);
            knownStash = Session.Stash.Count;
        }

        // ── construction ─────────────────────────────────────────

        void BuildTopBar(Transform root)
        {
            var bar = SoulUi.Panel("TopBar", root, SoulUi.HeaderBg).rectTransform;
            SoulUi.Place(bar, new Vector2(0, 1), Vector2.one, new Vector2(0, -TopHeight), Vector2.zero);
            var line = SoulUi.Panel("Line", bar, SoulUi.PanelBorder).rectTransform;
            SoulUi.Place(line, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 2));

            // left: where (and what the party is doing), when · right: what the party has, how it is steered (auto,
            // aim, who moves), and the menus (souls, settings, back to the village)
            var mark = SoulUi.Icon(bar, SoulIconSet.Ui("exit"), 52);
            SoulUi.Place(mark.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(16, -26), new Vector2(68, 26));
            where = SoulUi.Text(bar, "", 30, SoulUi.Accent, TextAlignmentOptions.BottomLeft);
            where.fontStyle = FontStyles.Bold;
            where.textWrappingMode = TextWrappingModes.NoWrap;
            where.overflowMode = TextOverflowModes.Overflow;
            SoulUi.Place(where.rectTransform, new Vector2(0, .5f), new Vector2(0, 1), new Vector2(80, -4), new Vector2(LeftWidth, 0));
            when = SoulUi.Text(bar, "", 22, new Color(.82f, .86f, .96f), TextAlignmentOptions.TopLeft);
            when.textWrappingMode = TextWrappingModes.NoWrap;
            when.overflowMode = TextOverflowModes.Overflow;
            SoulUi.Place(when.rectTransform, Vector2.zero, new Vector2(0, .5f), new Vector2(80, 2), new Vector2(LeftWidth, 2));

            var right = SoulUi.Row(bar, TopHeight - 20, 8);
            right.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            SoulUi.Place(right, new Vector2(1, 0), Vector2.one, new Vector2(-RightWidth - 16, 10), new Vector2(-16, -10));
            gold = Stat(right, SoulIconSet.Ui("gold"), "금화");
            preserve = Stat(right, SoulIconSet.Ui("preserve"), "영혼석");
            vault = Stat(right, SoulIconSet.Ui("vault"), "보관한 영혼");
            Divider(right);
            speedButton = SoulUi.Button(right, "×1", SoulUi.TabInactive, SoulExpeditionRunner.NextGameSpeed, 22, SoulIconSet.Ui("cooldown"));
            SoulUi.Layout(speedButton.gameObject, 92, -1);
            SoulTooltip.Attach(speedButton, SoulIconSet.Ui("cooldown"), "배속", () => new List<SoulLine> { new SoulLine(null, "누를 때마다 ×1 → ×2 → ×4 → ×8") });
            autoButton = SoulUi.Button(right, "자동 ON", SoulUi.TabActive, () => Session.AutoExplore = !Session.AutoExplore, 19, SoulIconSet.Ui("follow"));
            SoulUi.Layout(autoButton.gameObject, 124, -1);
            // The aim: 기본 ↔ 탈출 for anyone; a pathfinder also picks 사냥 / 탐험 / 돌파 on the way (then 탈출).
            aimButton = SoulUi.Button(right, "기본", SoulUi.TabInactive, NextAim, 19, SoulIconSet.Ui("map"));
            SoulUi.Layout(aimButton.gameObject, 124, -1);
            partyButton = SoulUi.Button(right, "개별 이동", SoulUi.TabInactive, () => Controller.MoveParty = !Controller.MoveParty, 19, SoulIconSet.Ui("party"));
            partyLabel = partyButton.GetComponentInChildren<TextMeshProUGUI>();
            SoulUi.Layout(partyButton.gameObject, 124, -1);
            Divider(right);
            soulButton = SoulUi.Button(right, "영혼", SoulUi.TabInactive, () => OpenPopup(SoulMercenaryPopup.Tab.Souls), 19, SoulIconSet.Ui("soul"));
            SoulUi.Layout(soulButton.gameObject, 108, -1);
            var settings = SoulUi.Button(right, "", SoulUi.TabInactive, () => PopupManager.Instance?.Open(PopupType.Settings), 22, SoulIconSet.Ui("gear"));
            SoulUi.Layout(settings.gameObject, 56, -1);
            // watching an expedition: back to the village (the party stays down)
            homeButton = SoulUi.Button(right, "마을", new Color(.2f, .5f, .28f), () => Controller.ReturnToVillage(), 19, SoulIconSet.Ui("exit"));
            SoulUi.Layout(homeButton.gameObject, 100, -1);
            homeButton.gameObject.SetActive(Controller.FromVillage);
        }

        const float LeftWidth = 780, RightWidth = 1110;

        static void Divider(Transform parent)
        {
            var line = SoulUi.Panel("Divider", parent, new Color(.35f, .4f, .6f, .6f));
            line.raycastTarget = false;
            SoulUi.Layout(line.gameObject, 2, 40);
        }

        // Pouch (tap: read a scroll / camp) and belt (potions drink themselves) under the top bar, left.
        void BuildSupplyBar(Transform root)
        {
            supplyBar = SoulUi.Row(root, 64, 8);
            supplyBar.anchorMin = supplyBar.anchorMax = supplyBar.pivot = Vector2.zero;
            supplyBar.anchoredPosition = new Vector2(14, BottomHeight + 10);
            supplyBar.sizeDelta = new Vector2(1400, 64);
            supplyBar.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
        }

        void RefreshSupplies()
        {
            var key = new StringBuilder();
            foreach (var entry in Session.Pouch) key.Append(entry.Key).Append(entry.Value).Append(Session.SupplyBlock(entry.Key) == null);
            foreach (var entry in Session.Belt) key.Append(entry.Key).Append(entry.Value);
            key.Append(Session.ScrollWait > 0).Append(Session.RecallLeft > 0);
            foreach (var supply in SoulSupplies.All) key.Append(Controller.StoreScrolls(supply.Id));
            if (key.ToString() == supplyKey) return;
            supplyKey = key.ToString();
            SoulUi.Clear(supplyBar);
            // enhancement scrolls from the village store (watching an expedition)
            foreach (var supply in SoulSupplies.All)
            {
                int stock = Controller.StoreScrolls(supply.Id);
                if (stock <= 0) continue;
                var def = supply;
                bool ready = Controller.ScrollBlock(def.Id) == null;
                var button = SoulUi.Button(supplyBar, $"{supply.Name} ×{stock}", ready ? new Color(.35f, .25f, .5f) : SoulUi.TabInactive, () =>
                {
                    string block = Controller.ScrollBlock(def.Id);
                    if (block != null) { Toast($"{def.Name}: {block}"); return; }
                    Controller.ReadScroll(def.Id);
                    Toast($"{def.Name} 사용");
                }, 17, SoulIconSet.Ui(supply.Icon));
                SoulUi.Layout(button.gameObject, 190, 56);
                SoulTooltip.Attach(button, SoulIconSet.Ui(def.Icon), def.Name, () => new List<SoulLine> { new SoulLine(null, def.Description), new SoulLine(null, Controller.ScrollBlock(def.Id) ?? "사용 가능") });
            }
            foreach (var supply in SoulSupplies.All)
            {
                int count = SoulDungeonSession.Count(Session.Pouch, supply.Id);
                if (count <= 0) continue;
                var def = supply;
                bool ready = Session.SupplyBlock(supply.Id) == null;
                var button = SoulUi.Button(supplyBar, $"{supply.Name} ×{count}", ready ? new Color(.22f, .3f, .5f) : SoulUi.TabInactive, () =>
                {
                    string block = Session.SupplyBlock(def.Id);
                    if (block != null) { Toast($"{def.Name}: {block}"); return; }
                    Session.UseSupply(def.Id, Controller.SelectedMercenary);
                    Toast($"{def.Name} 사용");
                }, 17, SoulIconSet.Ui(supply.Icon));
                SoulUi.Layout(button.gameObject, 190, 56);
                SoulTooltip.Attach(button, SoulIconSet.Ui(def.Icon), def.Name, () => new List<SoulLine> { new SoulLine(null, def.Description), new SoulLine(null, Session.SupplyBlock(def.Id) ?? "지금 쓸 수 있습니다") });
            }
            foreach (var supply in SoulSupplies.All)
            {
                int count = SoulDungeonSession.Count(Session.Belt, supply.Id);
                if (count <= 0) continue;
                var def = supply;
                var chip = SoulUi.Chip(supplyBar, SoulIconSet.Ui(supply.Icon), $"{count}", Color.white, 44);
                SoulUi.Layout(chip.gameObject, 84, 44);
                SoulTooltip.Attach(chip, SoulIconSet.Ui(def.Icon), $"{def.Name} ×{count} (자동)", () => new List<SoulLine> { new SoulLine(null, def.Description) });
            }
        }

        static TextMeshProUGUI Stat(Transform parent, Sprite icon, string name)
        {
            var chip = SoulUi.Chip(parent, icon, "", Color.white, 48);
            SoulUi.Layout(chip.gameObject, 96, 48);
            chip.GetComponent<Image>().raycastTarget = true;
            SoulTooltip.Attach(chip, icon, name, () => new List<SoulLine>());
            return chip.GetComponentInChildren<TextMeshProUGUI>();
        }

        // Minimap: always on, folds to its header; the expand button opens the full map popup.
        void BuildMinimap(Transform root)
        {
            minimapPanel = SoulUi.Framed("Minimap", root, new Color(.04f, .045f, .08f, .92f), SoulUi.PanelBorder, 2);
            minimapPanel.anchorMin = minimapPanel.anchorMax = minimapPanel.pivot = Vector2.one;
            minimapPanel.anchoredPosition = new Vector2(-14, -TopHeight - 12);
            minimapPanel.sizeDelta = new Vector2(MinimapWidth, MinimapHeight);

            var header = SoulUi.Row(minimapPanel, 40, 6);
            SoulUi.Place(header, new Vector2(0, 1), Vector2.one, new Vector2(10, -44), new Vector2(-8, -4));
            SoulUi.Icon(header, SoulIconSet.Ui("map"), 30);
            var title = SoulUi.Text(header, "지도", 22, Color.white);
            SoulUi.Layout(title.gameObject, -1, -1, 1);
            var follow = SoulUi.Button(header, "", SoulUi.TabInactive, () => World.Follow(), 18, SoulIconSet.Ui("follow"));
            SoulUi.Layout(follow.gameObject, 40, 36);
            var expand = SoulUi.Button(header, "", SoulUi.TabInactive, () => SoulMapPopup.Open(Controller, World), 18, SoulIconSet.Ui("expand"));
            SoulUi.Layout(expand.gameObject, 40, 36);
            foldButton = SoulUi.Button(header, "", SoulUi.TabInactive, ToggleMinimap, 18, SoulIconSet.Ui("fold"));
            SoulUi.Layout(foldButton.gameObject, 40, 36);

            minimapBody = SoulUi.Rect("Body", minimapPanel);
            SoulUi.Place(minimapBody, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -46));
            var minimap = SoulMinimap.Create(minimapBody, Controller, World);
            minimap.OnCellClicked = cell => World.FocusOn(Session.Map.Center(cell));
        }

        void ToggleMinimap()
        {
            minimapFolded = !minimapFolded;
            minimapBody.gameObject.SetActive(!minimapFolded);
            minimapPanel.sizeDelta = new Vector2(MinimapWidth, minimapFolded ? 48 : MinimapHeight);
            foreach (var image in foldButton.GetComponentsInChildren<Image>(true))
                if (image.gameObject != foldButton.gameObject) image.sprite = SoulIconSet.Ui(minimapFolded ? "unfold" : "fold");
        }

        void BuildBottomBar(Transform root)
        {
            var bar = SoulUi.Panel("BottomBar", root, SoulUi.PanelBg).rectTransform;
            SoulUi.Place(bar, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, BottomHeight));
            var line = SoulUi.Panel("Line", bar, SoulUi.PanelBorder).rectTransform;
            SoulUi.Place(line, new Vector2(0, 1), Vector2.one, new Vector2(0, -2), Vector2.zero);

            // The whole bar belongs to the party: cards share the width (wider when fewer go down), from the left.
            int count = Mathf.Max(1, Session.Mercenaries.Count);
            cardWidth = Mathf.Min(380, (1920 - 24 - 10 * (count - 1)) / (float)count);
            float partyWidth = count * cardWidth + (count - 1) * 10;
            var party = SoulUi.Rect("Party", bar);
            party.anchorMin = new Vector2(0, 0); party.anchorMax = new Vector2(0, 1);
            party.offsetMin = new Vector2(12, 12); party.offsetMax = new Vector2(12 + partyWidth, -12);
            var layout = SoulUi.Horizontal(party.gameObject, 10);
            layout.childAlignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < Session.Mercenaries.Count; i++) cards.Add(BuildCard(party, i));

            // controls hint: a thin line above the bar, right side (the supply bar holds the left)
            var keys = SoulUi.Text(root, "좌클릭 이동 · 드래그 화면 이동 · 휠 확대 · Space 따라가기 · 선택된 카드 클릭 = 상세", 15, new Color(.5f, .54f, .68f), TextAlignmentOptions.BottomRight);
            SoulUi.Place(keys.rectTransform, new Vector2(.5f, 0), new Vector2(1, 0), new Vector2(0, BottomHeight + 4), new Vector2(-16, BottomHeight + 26));
        }

        // Card: live portrait · name/level · HP/stamina/MP · status icons · level-up badge.
        // Click selects and follows; clicking the already selected card opens the detail popup.
        PartyCard BuildCard(Transform parent, int index)
        {
            var card = new PartyCard();
            var button = SoulUi.Button(parent, "", SoulUi.SlotBg, () =>
            {
                if (Controller.SelectedHero == index) OpenPopup(SoulMercenaryPopup.Tab.Stats);
                Controller.SelectedHero = index;
                World.Follow();
            });
            Object.Destroy(button.transform.GetChild(0).gameObject); // unused label row
            card.Background = button.GetComponent<Image>();
            SoulUi.Layout(button.gameObject, cardWidth, -1);
            var rect = (RectTransform)button.transform;

            var face = SoulUi.Panel("Face", rect, new Color(0, 0, 0, .35f), true).rectTransform;
            SoulUi.Place(face, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -98), new Vector2(98, -10));
            face.gameObject.AddComponent<RectMask2D>();
            card.Portrait = SoulUi.Icon(face, null, 10, "Sprite");
            card.Portrait.rectTransform.anchorMin = card.Portrait.rectTransform.anchorMax = new Vector2(.5f, 0);
            card.Portrait.rectTransform.pivot = new Vector2(.5f, 0);
            card.Portrait.rectTransform.sizeDelta = new Vector2(277, 277); // 64px frame, the body sits low
            card.Portrait.rectTransform.anchoredPosition = new Vector2(0, -55);
            SoulKit.JobBadge(face, index < Session.Mercenaries.Count ? Session.Mercenaries[index] : null, 26, new Vector2(1, 0));

            card.Name = SoulUi.Text(rect, "", 24, Color.white);
            card.Name.fontStyle = FontStyles.Bold;
            card.Name.textWrappingMode = TextWrappingModes.NoWrap;
            SoulUi.Place(card.Name.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(108, -42), new Vector2(-60, -8)); // clear of the level badge
            card.Action = SoulUi.Text(rect, "", 17, SoulUi.SubText);
            card.Action.textWrappingMode = TextWrappingModes.NoWrap;
            card.Action.overflowMode = TextOverflowModes.Ellipsis;
            SoulUi.Place(card.Action.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(108, -66), new Vector2(-10, -42));
            card.Crown = SoulUi.Icon(rect, SoulIconSet.Ui("leader"), 34, "Leader");
            card.Crown.rectTransform.anchorMin = card.Crown.rectTransform.anchorMax = new Vector2(0, 1);
            card.Crown.rectTransform.anchoredPosition = new Vector2(22, -20); // on the portrait's corner
            card.Statuses = SoulUi.Row(rect, 30, 4);
            SoulUi.Place(card.Statuses, new Vector2(0, 1), Vector2.one, new Vector2(108, -96), new Vector2(-10, -68));

            // HP / stamina / MP, each behind its own symbol (heart, bolt, drop) at a readable size
            var bars = SoulUi.Rect("Bars", rect);
            SoulUi.Place(bars, Vector2.zero, new Vector2(1, 0), new Vector2(10, 8), new Vector2(-10, 84));
            SoulUi.Vertical(bars.gameObject, 2);
            card.Hp = SoulUi.IconBar(bars, SoulIconSet.Stat(StatType.MaxHp), SoulUi.HpColor, 16, out card.HpText, 24);
            card.Stamina = SoulUi.IconBar(bars, SoulIconSet.Stat(StatType.MaxStamina), SoulUi.StaminaColor, 10, out _, 24);
            card.Mana = SoulUi.IconBar(bars, SoulIconSet.Stat(StatType.MaxMp), SoulUi.ManaColor, 10, out _, 24);

            card.LevelUp = SoulUi.Button(rect, "", SoulUi.Accent, () =>
            {
                Controller.SelectedHero = index;
                SoulMercenaryPopup.Open(Controller, World, index, SoulMercenaryPopup.Tab.Stats, pickPattern: true);
            }, 20, SoulIconSet.Ui("level"));
            SoulUi.Place((RectTransform)card.LevelUp.transform, Vector2.one, Vector2.one, new Vector2(-54, -54), new Vector2(-8, -8));
            if (SoulIconSet.Ui("level") == null) SoulUi.SetLabel(card.LevelUp, "UP");
            return card;
        }

        void BuildToast(Transform root)
        {
            toast = SoulUi.Button(root, " ", new Color(.05f, .06f, .1f, .92f), () => { toastAction?.Invoke(); toastTime = 0; }, 23);
            var rect = (RectTransform)toast.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -TopHeight - 16);
            rect.sizeDelta = new Vector2(720, 56);
            toastText = toast.GetComponentInChildren<TextMeshProUGUI>();
            toast.gameObject.SetActive(false);
        }

        void BuildResult(Transform root)
        {
            result = SoulUi.Panel("Result", root, new Color(0, 0, 0, .72f)).gameObject;
            SoulUi.Stretch((RectTransform)result.transform);
            var box = SoulUi.Framed("Box", result.transform, SoulUi.PanelBg, SoulUi.Accent, 3);
            SoulUi.Place(box, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-420, -270), new Vector2(420, 270));
            SoulUi.Vertical(box.gameObject, 14, 36);
            SoulUi.Layout(box.GetChild(0).gameObject, -1, -1).ignoreLayout = true;
            resultTitle = SoulUi.Text(box, "", 48, SoulUi.Accent, TextAlignmentOptions.Center);
            resultTitle.fontStyle = FontStyles.Bold;
            SoulUi.Layout(resultTitle.gameObject, -1, 64);
            resultBody = SoulUi.Text(box, "", 23, Color.white, TextAlignmentOptions.Center);
            SoulUi.Layout(resultBody.gameObject, -1, 270);
            nextFloorButton = SoulUi.Button(box, "다음 층으로", SoulUi.TabActive, () => Controller.NextFloor(), 28, SoulIconSet.Ui("follow"));
            SoulUi.Layout(nextFloorButton.gameObject, -1, 70);
            villageButton = SoulUi.Button(box, "마을로 귀환", new Color(.2f, .5f, .28f), () => Controller.ReturnToVillage(escape: Session.HasNextFloor), 26, SoulIconSet.Ui("exit"));
            SoulUi.Layout(villageButton.gameObject, -1, 64);
            againButton = SoulUi.Button(box, "처음부터 다시", SoulUi.TabInactive, () => Controller.Restart(), 24, SoulIconSet.Ui("follow"));
            SoulUi.Layout(againButton.gameObject, -1, 60);
            result.SetActive(false);
        }

        // The parties down there (watching one): a tab each, the watched one lit.
        RectTransform tripTabs;
        string tripKey;

        void BuildTripTabs(Transform root)
        {
            tripTabs = SoulUi.Row(root, 48, 6);
            tripTabs.anchorMin = tripTabs.anchorMax = tripTabs.pivot = new Vector2(0, 1);
            tripTabs.anchoredPosition = new Vector2(14, -TopHeight - 8);
            tripTabs.sizeDelta = new Vector2(400, 48);
            tripTabs.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;

            questPanel = SoulUi.Panel("Quests", root, new Color(.04f, .045f, .08f, .78f), true).rectTransform;
            questPanel.anchorMin = questPanel.anchorMax = questPanel.pivot = new Vector2(0, 1);
            questText = SoulUi.Text(questPanel, "", 19, Color.white, TextAlignmentOptions.TopLeft);
            questText.raycastTarget = false;
            SoulUi.Stretch(questText.rectTransform, 10);
            questPanel.gameObject.SetActive(false);
        }

        // The quests taken (top left, under the party tabs): grade, title, where it stands with this trip.
        RectTransform questPanel;
        TextMeshProUGUI questText;
        bool tripsShown;
        const float QuestWidth = 380;

        void RefreshQuests()
        {
            var campaign = SoulCampaign.Current;
            var quests = campaign != null && Controller.FromVillage ? campaign.Active : null;
            questPanel.gameObject.SetActive(quests != null && quests.Count > 0);
            if (quests == null || quests.Count == 0) return;
            var lines = new StringBuilder($"<size=17><color=#{SoulUi.Hex(SoulUi.Accent)}><b>의뢰</b></color></size>");
            foreach (var quest in quests)
            {
                int progress = SoulCampaign.ProgressWith(quest, Session);
                string state = progress >= quest.Goal ? SoulUi.Colored("완료", SoulUi.Good) : $"<color=#{SoulUi.Hex(SoulUi.SubText)}>{progress}/{quest.Goal}</color>";
                lines.Append($"\n[{quest.GradeName}] {quest.Title}  {state}");
            }
            string text = lines.ToString();
            if (questText.text != text) questText.text = text;
            float height = questText.GetPreferredValues(text, QuestWidth - 20, 0).y + 20;
            questPanel.sizeDelta = new Vector2(QuestWidth, height);
            questPanel.anchoredPosition = new Vector2(14, -TopHeight - 8 - (tripsShown ? 56 : 0));
        }

        // A small face per party down there (its first living member); the watched one framed in gold.
        void RefreshTripTabs()
        {
            var campaign = SoulCampaign.Current;
            var trips = campaign != null && Controller.FromVillage ? campaign.Away : new List<SoulExpedition>();
            string key = string.Join(",", trips.ConvertAll(t => t.Number + ":" + t.Alive));
            if (key == tripKey) return;
            tripKey = key;
            SoulUi.Clear(tripTabs);
            tripsShown = trips.Count >= 2;
            if (!tripsShown) return;
            foreach (var trip in trips)
            {
                var shown = trip;
                var face = trip.Party.Find(h => h.Alive) ?? trip.Party[0];
                var icon = SoulUi.Portrait(tripTabs, SoulPortraits.For(face), 48, SoulIconSet.Ui("race"));
                icon.GetComponent<Image>().color = trip == Controller.Expedition ? SoulUi.Accent : SoulUi.PanelBorder;
                icon.GetComponent<Image>().raycastTarget = true;
                SoulUi.Clickable(icon.gameObject).onClick.AddListener(() => Controller.Watch(shown));
                var number = SoulUi.Text(icon, $"{trip.Number}", 16, Color.white, TextAlignmentOptions.BottomRight);
                number.fontStyle = FontStyles.Bold;
                SoulUi.Stretch(number.rectTransform, 3);
            }
        }

        static string AimLabel(SoulExploreMode mode)
            => mode == SoulExploreMode.Hunt ? "사냥" : mode == SoulExploreMode.Farm ? "탐험" : mode == SoulExploreMode.Advance ? "돌파" : "기본";

        bool Escaping => Session.Plan == SoulPartyPlan.Retreat || Session.Plan == SoulPartyPlan.Escape;

        // 기본 → (사냥 → 탐험 → 돌파, with a pathfinder) → 탈출 → 기본
        void NextAim()
        {
            if (Escaping) { Session.ExploreMode = SoulExploreMode.Explore; Session.CancelRetreat(); return; }
            if (Session.CanChooseRoute && (int)Session.ExploreMode < 3) { Session.ExploreMode = (SoulExploreMode)((int)Session.ExploreMode + 1); return; }
            Session.OrderEscape();
        }

        void OpenPopup(SoulMercenaryPopup.Tab tab) => SoulMercenaryPopup.Open(Controller, World, Controller.SelectedHero, tab);

        // ── per frame ────────────────────────────────────────────

        void Update()
        {
            if (Session == null || cards.Count == 0) return;
            // what the party is doing, only when it is something (camping, a scroll being read, stranded …)
            string doing = Session.RecallLeft > 0 ? SoulUi.Colored($"귀환 주문 {Session.RecallLeft:0.0}초", SoulUi.Good)
                : Session.Camping > 0 ? "야영 중" : Session.Plan == SoulPartyPlan.Recover ? "휴식 중"
                : Session.Plan == SoulPartyPlan.Stranded ? SoulUi.Colored("수동 조작", SoulUi.Bad) : Session.Resting ? "휴식 중" : null;
            if (Session.ExperienceBoost > 0) doing = (doing != null ? doing + "  " : "") + $"경험치 ×{SoulDungeonSession.AltarBoost} {Session.ExperienceBoost:0}초";
            where.text = $"{Session.Floor}층 {Session.Theme.Name}" + (doing != null ? $"   <size=24><color=#FFFFFF>{doing}</color></size>" : "");
            when.text = Session.ClockText + TimeLeft();
            gold.text = Session.Gold.ToString();
            preserve.text = Session.PreservationItems.ToString();
            vault.text = Session.Vault.Count.ToString();
            SoulUi.SetLabel(soulButton, Session.Stash.Count > 0 ? $"영혼 {Session.Stash.Count}" : "영혼");
            soulButton.GetComponent<Image>().color = Session.Stash.Count > 0
                ? Color.Lerp(SoulUi.TabInactive, new Color(.45f, .28f, .7f), .5f + .5f * Mathf.Sin(Time.time * 4)) : SoulUi.TabInactive;
            partyLabel.text = Controller.MoveParty ? "파티 이동" : "개별 이동";
            partyButton.GetComponent<Image>().color = Controller.MoveParty ? SoulUi.TabActive : SoulUi.TabInactive;
            SoulUi.SetLabel(autoButton, Session.AutoExplore ? "자동 ON" : "자동 OFF");
            SoulUi.SetLabel(speedButton, $"×{SoulExpeditionRunner.GameSpeed:0}");
            speedButton.GetComponent<Image>().color = SoulExpeditionRunner.GameSpeed > 1 ? new Color(.55f, .38f, .12f) : SoulUi.TabInactive;
            SoulUi.SetLabel(aimButton, Escaping ? "탈출" : AimLabel(Session.EffectiveMode));
            aimButton.GetComponent<Image>().color = Escaping ? new Color(.6f, .22f, .2f) : Session.CanChooseRoute ? new Color(.2f, .42f, .3f) : SoulUi.TabInactive;
            autoButton.GetComponent<Image>().color = Session.AutoExplore ? SoulUi.TabActive : SoulUi.TabInactive;

            if (Session.Stash.Count > knownStash)
                Toast($"{Session.Stash[Session.Stash.Count - 1].Soul.OriginMonster}의 영혼 획득 — 눌러서 보관함 열기", () => OpenPopup(SoulMercenaryPopup.Tab.Souls));
            knownStash = Session.Stash.Count;
            if (Session.Inventory.Count > knownLoot)
            {
                var names = new List<string>();
                for (int i = knownLoot; i < Session.Inventory.Count; i++) names.Add(Session.Inventory[i].Name);
                Toast("장비 획득: " + string.Join(", ", names));
            }
            knownLoot = Session.Inventory.Count;

            RefreshCards();
            RefreshSupplies();
            RefreshTripTabs();
            RefreshQuests();

            toastTime -= Time.unscaledDeltaTime;
            toast.gameObject.SetActive(toastTime > 0);
            toastText.alpha = Mathf.Clamp01(toastTime * 2);

            // An expedition under automatic control goes a floor down by itself: the choice is for manual play.
            bool ended = Session.Finished || Session.Defeated || Session.Recalled;
            if (ended && !resultShown && !(Controller.FromVillage && Session.HasNextFloor && Session.AutoControl)) ShowResult();
        }

        // Village clock and the dungeon time the expedition has left (watching one).
        string TimeLeft()
        {
            var trip = Controller.Expedition;
            var campaign = SoulCampaign.Current;
            if (trip == null || campaign == null) return "";
            int minutes = Mathf.FloorToInt(SoulClock.DungeonHoursLeft(campaign.Clock, trip.ReturnAt) * 60);
            return $"  ·  마을 {SoulClock.HourMinute(campaign.Clock)}  ·  남은 시간 {minutes / 60}시간 {minutes % 60}분";
        }

        void RefreshCards()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var hero = Session.Mercenaries[i];
                var card = cards[i];
                card.Background.color = i == Controller.SelectedHero ? new Color(.17f, .25f, .45f) : SoulUi.SlotBg;
                card.Crown.enabled = hero == Session.Leader && card.Crown.sprite != null;
                card.Portrait.sprite = World != null ? World.Portrait(hero) : null;
                SoulPortraits.Fit(card.Portrait, hero, -55, 88);
                card.Portrait.enabled = card.Portrait.sprite != null;
                card.Portrait.color = hero.Alive ? Color.white : new Color(.4f, .4f, .4f, .8f);
                int tired = SoulFatigue.Tier(hero.Fatigue);
                var tiredColor = tired >= 3 ? SoulUi.Bad : tired == 2 ? new Color(1f, .6f, .3f) : tired == 1 ? new Color(.95f, .85f, .4f) : SoulUi.SubText;
                card.Name.text = $"{(hero.Rarity != SoulStyleRarity.Normal ? SoulUi.Colored(hero.Name, SoulHireStyles.RarityColor(hero.Rarity)) : hero.Name)} <size=18><color=#{SoulUi.Hex(SoulUi.Accent)}>Lv.{hero.Level}</color> <color=#{SoulUi.Hex(SoulUi.SubText)}>{hero.Grade}등급</color></size>";
                string who = $"<color=#{SoulUi.Hex(new Color(.55f, .6f, .75f))}>{hero.Race.Id} {hero.Job}</color>";
                card.Action.text = who + "  " + (hero.Alive ? hero.ShownAction ?? hero.Action : SoulUi.Colored("쓰러짐", SoulUi.Bad));
                SoulUi.SetHp(card.Hp, hero);
                card.HpText.text = $"{hero.Hp:0}";
                SoulUi.SetRatio(card.Stamina, hero.Stamina / Mathf.Max(1, hero.Stats.Total(StatType.MaxStamina)));
                SoulUi.SetRatio(card.Mana, hero.Mp / Mathf.Max(1, hero.Stats.Total(StatType.MaxMp)));
                bool canLevel = hero.CanPickPattern && hero.Alive;
                card.LevelUp.gameObject.SetActive(canLevel);
                if (canLevel) card.LevelUp.transform.localScale = Vector3.one * (1 + .08f * Mathf.Sin(Time.time * 6));

                // status and buff icons, rebuilt only when they change
                var key = new StringBuilder();
                key.Append(tired).Append('|').Append(hero.Wounds).Append('|');
                foreach (var status in hero.Statuses) key.Append(status.Kind);
                foreach (var buff in hero.TimedBuffs) key.Append(buff.Id);
                if (key.ToString() == card.StatusKey) continue;
                card.StatusKey = key.ToString();
                SoulUi.Clear(card.Statuses);
                // fatigue and wounds as small labelled chips, then statuses and real buffs (not the internal layers)
                if (tired > 0) SoulUi.Chip(card.Statuses, SoulIconSet.Ui("camp"), SoulFatigue.Name(hero.Fatigue), tiredColor, 28);
                if (hero.Wounds > 0) SoulUi.Chip(card.Statuses, SoulIconSet.Status(SoulStatus.Bleed), $"부상 {hero.Wounds}", SoulUi.Bad, 28);
                foreach (var status in hero.Statuses) SoulUi.Icon(card.Statuses, SoulIconSet.Status(status.Kind), 28);
                foreach (var buff in hero.TimedBuffs)
                    if (buff.Id != SoulCombat.StatusBuffId && buff.Id != SoulFatigue.BuffId) SoulUi.Icon(card.Statuses, SoulDescribe.BuffIcon(buff), 28);
            }
        }

        void ShowResult()
        {
            resultShown = true;
            result.SetActive(true);
            PopupManager.Instance?.CloseAll();
            resultTitle.text = Session.Recalled ? "귀환" : !Session.Finished ? "용병단 전멸" : Session.HasNextFloor ? $"{Session.Floor}층 돌파" : "최종 8층 돌파!";
            nextFloorButton.gameObject.SetActive(Session.HasNextFloor);
            villageButton.gameObject.SetActive(Controller.FromVillage);
            againButton.gameObject.SetActive(!Controller.FromVillage);
            var trip = Controller.Expedition;
            if (trip != null)
                SoulUi.SetLabel(villageButton, Session.HasNextFloor ? "탈출 — 지금 마을로 귀환"
                    : trip.Waiting && !trip.Settled ? $"마을로 (원정대는 {SoulClock.HourMinute(trip.ReturnAt)}에 귀환)" : "마을로 돌아가기");
            if (Session.HasNextFloor) SoulUi.SetLabel(nextFloorButton, $"{Session.Floor + 1}층으로 (적 ×{SoulDungeonSession.FloorPower(Session.Floor + 1):0.#})");
            var body = new StringBuilder();
            if (Session.Recalled)
            {
                body.AppendLine(Session.Events.Count > 0 ? Session.Events[0] : "전리품을 들고 마을로 돌아갑니다.");
                body.AppendLine($"획득 금화 {Session.Gold} · 장비 {Session.Inventory.Count}개 · 가져간 영혼 {Session.Vault.Count}개");
                foreach (var hero in Session.Mercenaries) if (!hero.Alive) body.AppendLine(SoulUi.Colored(SoulCampaign.FallenText(hero.Name), SoulUi.Bad));
            }
            else if (Session.Finished)
            {
                body.AppendLine($"획득 금화 {Session.Gold}");
                body.AppendLine($"던전 밖으로 가져간 영혼 {Session.Vault.Count}개 (흡수하지 않은 영혼은 사라졌습니다)");
                foreach (var hero in Session.Mercenaries) if (hero.Alive) body.AppendLine($"{hero.Name}  Lv.{hero.Level}  {hero.Grade}등급  영혼 {hero.Souls.Count}");
                foreach (var hero in Session.Mercenaries) if (!hero.Alive) body.AppendLine(SoulUi.Colored(SoulCampaign.FallenText(hero.Name), SoulUi.Bad));
                foreach (var line in Session.MentalReport) body.AppendLine(SoulUi.Colored(line, SoulUi.Good));
                if (trip != null && trip.Waiting) body.AppendLine(SoulUi.Colored($"더 내려갈 층이 없습니다 — 원정대는 {SoulClock.HourMinute(trip.ReturnAt)}에 마을로 돌아옵니다", SoulUi.Accent));
            }
            else body.AppendLine("모든 용병이 쓰러졌습니다.\n임시 보관함의 영혼은 사라졌습니다." + (Controller.FromVillage ? (SoulCampaign.DeathIsFinal ? "\n쓰러진 용병은 모두 사망했습니다." : $"\n쓰러진 용병은 부상 {SoulCampaign.FallenWounds}로 마을에 실려 갑니다.") : ""));
            resultBody.text = body.ToString();
        }
    }
}
