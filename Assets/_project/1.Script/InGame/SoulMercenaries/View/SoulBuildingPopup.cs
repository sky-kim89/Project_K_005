using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // Shared frame of the village building popups (PopupManager popups, built at runtime like SoulMercenaryPopup):
    // full screen, as if walking in — the building's interior behind, a header banner with the building at its
    // level, build / upgrade, tabs, and a body rebuilt whenever the campaign changes.
    public abstract class SoulBuildingPopup : PopupBase
    {
        protected abstract SoulBuildingKind Kind { get; }
        protected virtual string[] Tabs => null;

        const float Margin = 48;
        protected SoulCampaign Campaign => SoulCampaign.Current;
        protected string Message;

        bool built, dirty = true;
        int shownRevision = -1, shownPortraits = -1, tab;
        RectTransform body, tabRow;
        Image picture, interior;
        RectTransform overlayRoot, foot; // windows over the building (stat sheet, confirmations); the refusal line
        TextMeshProUGUI title, notes, message, goldText;
        Button upgrade;
        readonly List<Button> tabButtons = new List<Button>();

        public static void Open(SoulBuildingKind kind)
        {
            var manager = PopupManager.Instance;
            if (manager == null || SoulCampaign.Current == null) return;
            manager.Open<SoulBuildingPopup>(PopupFor(kind));
        }

        public static PopupType PopupFor(SoulBuildingKind kind)
        {
            switch (kind)
            {
                case SoulBuildingKind.Guild: return PopupType.SoulGuild;
                case SoulBuildingKind.Shop: return PopupType.SoulShop;
                case SoulBuildingKind.Church: return PopupType.SoulChurch;
                case SoulBuildingKind.Training: return PopupType.SoulTraining;
                case SoulBuildingKind.Blacksmith: return PopupType.SoulBlacksmith;
                case SoulBuildingKind.Storage: return PopupType.SoulStorage;
                case SoulBuildingKind.Library: return PopupType.SoulLibrary;
                case SoulBuildingKind.Memorial: return PopupType.SoulMemorial;
                case SoulBuildingKind.SoulAltar: return PopupType.SoulAltar;
                case SoulBuildingKind.Board: return PopupType.SoulBoard;
                default: return PopupType.SoulMerchant;
            }
        }

        protected override void OnAfterClose() => SoulTooltip.Hide();

        protected void Refresh() => dirty = true;

        // ── parties (the guild forms them, the storehouse equips them) ──

        // The party being edited: shared, so the storehouse opens on the guild's choice.
        protected static int partyIndex;
        protected SoulParty EditedParty => Campaign.Parties[Mathf.Clamp(partyIndex, 0, Campaign.Parties.Count - 1)];

        void Update()
        {
            if (Campaign == null) return;
            if (!built) Build();
            if (!dirty && shownRevision == Campaign.Revision && shownPortraits == SoulPortraits.Version) return;
            dirty = false;
            shownRevision = Campaign.Revision;
            shownPortraits = SoulPortraits.Version;
            Show();
        }

        void Build()
        {
            built = true;
            var root = (RectTransform)transform;
            SoulUi.Stretch(root);
            var frame = SoulUi.Panel("Frame", transform, Color.black).rectTransform; // blocks the village behind
            overlayRoot = frame;
            SoulUi.Stretch(frame);
            interior = SoulUi.Icon(frame, null, 10, "Interior");
            interior.preserveAspect = false;
            interior.raycastTarget = false;
            SoulUi.Stretch(interior.rectTransform);
            var shade = SoulUi.Panel("Shade", frame, new Color(0, 0, 0, .25f));
            shade.raycastTarget = false;
            SoulUi.Stretch(shade.rectTransform);

            var header = SoulUi.Panel("Header", frame, new Color(SoulUi.HeaderBg.r, SoulUi.HeaderBg.g, SoulUi.HeaderBg.b, .9f), true).rectTransform;
            SoulUi.Place(header, new Vector2(0, 1), Vector2.one, new Vector2(Margin, -166), new Vector2(-Margin, -Margin * .35f));
            var box = SoulUi.Panel("Picture", header, new Color(0, 0, 0, .3f), true).rectTransform;
            SoulUi.Place(box, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(12, -60), new Vector2(172, 60));
            picture = SoulUi.Icon(box, null, 10);
            SoulUi.Stretch(picture.rectTransform, 6);
            title = SoulUi.Text(header, "", 36, Color.white, TextAlignmentOptions.TopLeft);
            title.fontStyle = FontStyles.Bold;
            SoulUi.Place(title.rectTransform, new Vector2(0, 1), new Vector2(.6f, 1), new Vector2(190, -62), new Vector2(0, -12));
            notes = SoulUi.Text(header, "", 18, SoulUi.SubText, TextAlignmentOptions.TopLeft);
            SoulUi.Place(notes.rectTransform, Vector2.zero, new Vector2(.62f, 1), new Vector2(190, 8), new Vector2(0, -66));
            goldText = SoulUi.Text(header, "", 24, SoulUi.Accent, TextAlignmentOptions.MidlineRight);
            SoulUi.Place(goldText.rectTransform, new Vector2(.5f, .55f), new Vector2(1, 1), new Vector2(0, 0), new Vector2(-190, -12));
            upgrade = SoulUi.Button(header, "강화", SoulUi.TabActive, () =>
            {
                Message = Campaign.UpgradeBlock(Kind) ?? (Campaign.Upgrade(Kind) ? null : "금화가 부족합니다");
                Refresh();
            }, 22, SoulIconSet.Ui("level"));
            SoulUi.Place((RectTransform)upgrade.transform, new Vector2(.64f, 0), new Vector2(1, .5f), new Vector2(0, 12), new Vector2(-16, -6));

            var close = SoulUi.Button(frame, "나가기", SoulUi.Bad, () => Close(), 22, SoulIconSet.Ui("close"));
            SoulUi.Place((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-Margin - 150, -72), new Vector2(-Margin - 12, -Margin * .35f - 12));

            tabRow = SoulUi.Rect("Tabs", frame);
            SoulUi.Place(tabRow, new Vector2(0, 1), Vector2.one, new Vector2(Margin, -230), new Vector2(-Margin, -176));
            var tabLayout = SoulUi.Horizontal(tabRow.gameObject, 8);
            tabLayout.childAlignment = TextAnchor.MiddleLeft;
            var names = Tabs ?? new string[0];
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var button = SoulUi.Button(tabRow, names[i], SoulUi.TabInactive, () => { tab = index; Message = null; Refresh(); }, 22);
                SoulUi.Layout(button.gameObject, 220, -1);
                tabButtons.Add(button);
            }

            // the work area: a dark glass pane over the interior, so the room shows around it
            var pane = SoulUi.Panel("Pane", frame, new Color(.035f, .04f, .075f, .88f), true).rectTransform;
            bool tabbed = Tabs != null && Tabs.Length > 1;   // one view only: no tab row, the pane takes its place
            SoulUi.Place(pane, Vector2.zero, Vector2.one, new Vector2(Margin, 64), new Vector2(-Margin, tabbed ? -240 : -178));
            body = SoulUi.Rect("Body", pane);
            SoulUi.Place(body, Vector2.zero, Vector2.one, new Vector2(16, 12), new Vector2(-16, -12));
            foot = SoulUi.Panel("Message", frame, new Color(0, 0, 0, .6f), true).rectTransform;
            SoulUi.Place(foot, Vector2.zero, new Vector2(1, 0), new Vector2(Margin, 12), new Vector2(-Margin, 54));
            message = SoulUi.Text(foot, "", 20, SoulUi.Good, TextAlignmentOptions.MidlineLeft);
            SoulUi.Place(message.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-16, 0));
        }

        void Show()
        {
            var def = Campaign.Building(Kind);
            int level = Campaign.Level(Kind);
            picture.sprite = def.Sprites != null && level < def.Sprites.Length ? def.Sprites[level] : null;
            picture.enabled = picture.sprite != null;
            interior.sprite = def.Interior;
            interior.enabled = def.Interior != null;
            interior.color = level == 0 ? new Color(.35f, .35f, .4f) : Color.white; // an empty plot: the room is still dark
            bool fixedLevel = def.UpgradeCosts == null || def.UpgradeCosts.Length <= 1; // the memorial, the merchant
            title.text = level == 0 ? $"{def.Name}  " + SoulDescribe.Small("건설 전") : fixedLevel ? def.Name : $"{def.Name}  <size=24><color=#{SoulUi.Hex(SoulUi.Accent)}>{level}단계</color></size>";
            // only what the next level brings (the room speaks for itself)
            notes.text = def.LevelNotes != null && level < def.LevelNotes.Length && level < Campaign.MaxLevelOf(Kind)
                ? SoulUi.Colored($"{(level == 0 ? "건설" : $"{level + 1}단계")}: {def.LevelNotes[level]}", SoulUi.Accent) : "";
            goldText.text = $"금화 {Campaign.Gold}";
            int cost = Campaign.UpgradeCost(Kind);
            upgrade.gameObject.SetActive(cost >= 0);
            string block = Campaign.UpgradeBlock(Kind);
            if (cost >= 0) SoulUi.SetLabel(upgrade, block ?? (level == 0 ? $"건설하기 ({cost} 금화)" : $"{level + 1}단계로 강화 ({cost} 금화)"));
            upgrade.interactable = cost >= 0 && Campaign.Gold >= cost && block == null;
            tabRow.gameObject.SetActive(level > 0 && Tabs != null && Tabs.Length > 1);
            for (int i = 0; i < tabButtons.Count; i++) tabButtons[i].GetComponent<Image>().color = i == tab ? SoulUi.TabActive : SoulUi.TabInactive;
            message.text = Message ?? "";
            message.color = SoulUi.Bad;
            foot.gameObject.SetActive(!string.IsNullOrEmpty(Message));
            // the scroll positions of the screen as it was: a rebuild (buying a guild ability, a tap anywhere) keeps them
            var kept = new List<Vector2>();
            foreach (var scroll in body.GetComponentsInChildren<ScrollRect>()) kept.Add(scroll.content != null ? scroll.content.anchoredPosition : Vector2.zero);
            bool sameView = keptTab == tab;
            keptTab = tab;
            SoulUi.Clear(body);
            if (level == 0)
            {
                var hint = SoulUi.Text(body, "아직 빈 터입니다. 위의 '건설하기'로 세울 수 있습니다.", 26, SoulUi.SubText, TextAlignmentOptions.Center);
                SoulUi.Stretch(hint.rectTransform);
                return;
            }
            BuildBody(body, tab);
            if (!sameView || kept.Count == 0) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(body);
            var fresh = body.GetComponentsInChildren<ScrollRect>();
            for (int i = 0; i < fresh.Length && i < kept.Count; i++)
            {
                var content = fresh[i].content;
                if (content == null || fresh[i].viewport == null) continue;
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                var room = content.rect.size - fresh[i].viewport.rect.size;
                content.anchoredPosition = new Vector2(Mathf.Clamp(kept[i].x, -Mathf.Max(0, room.x), 0), Mathf.Clamp(kept[i].y, 0, Mathf.Max(0, room.y)));
            }
        }

        int keptTab = -1;

        protected abstract void BuildBody(RectTransform body, int tab);

        // ── helpers for the bodies ───────────────────────────────

        protected static RectTransform Column(RectTransform body, float left, float width)
        {
            var column = SoulUi.Rect("Column", body);
            SoulUi.Place(column, Vector2.zero, width > 0 ? new Vector2(0, 1) : Vector2.one, new Vector2(left, 0), new Vector2(width > 0 ? left + width : 0, 0));
            return column;
        }

        protected static RectTransform List(RectTransform column)
        {
            var list = SoulUi.Scroll(column, out var scroll);
            SoulUi.Stretch((RectTransform)scroll.transform);
            return list;
        }

        protected static void Heading(Transform parent, string text, string note = null)
        {
            var row = SoulUi.Row(parent, 40, 10);
            var label = SoulUi.Text(row, text, 24, SoulUi.SectionLbl, TextAlignmentOptions.BottomLeft);
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (note != null)
            {
                var hint = SoulUi.Text(row, note, 17, SoulUi.SubText, TextAlignmentOptions.BottomLeft);
                SoulUi.Layout(hint.gameObject, -1, -1, 1);
            }
        }

        // A smaller heading inside a list (the shop's slot groups).
        protected static void SubHeading(Transform parent, string text)
        {
            var label = SoulUi.Text(parent, text, 19, SoulUi.Accent, TextAlignmentOptions.BottomLeft);
            label.fontStyle = FontStyles.Bold;
            SoulUi.Layout(label.gameObject, -1, 30);
        }

        // One line: icon · text (flexible) · optional button on the right. A frame colour puts the icon in a tile
        // (items: their grade), a mercenary puts its portrait there.
        protected Image Entry(Transform parent, Sprite icon, string text, string action, bool enabled, UnityAction onClick, float height = 64, Color? background = null,
            Color? frame = null, SoulMercenary portrait = null)
        {
            var row = SoulUi.Panel("Entry", parent, background ?? SoulUi.SlotBg, true);
            SoulUi.Layout(row.gameObject, -1, height);
            var layout = SoulUi.Horizontal(row.gameObject, 12);
            layout.padding = new RectOffset(10, 10, 6, 6);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandHeight = false;
            if (portrait != null) SoulUi.Portrait(row.transform, SoulPortraits.For(portrait), height - 12, icon);
            else if (frame.HasValue) SoulUi.Tile(row.transform, icon, height - 12, frame.Value);
            else if (icon != null) SoulUi.Icon(row.transform, icon, height - 16);
            var label = SoulUi.Text(row.transform, text, 20, Color.white);
            SoulUi.Layout(label.gameObject, -1, -1, 1);
            if (action != null)
            {
                var button = SoulUi.Button(row.transform, action, enabled ? SoulUi.TabActive : SoulUi.TabInactive, () => { onClick?.Invoke(); Refresh(); }, 19);
                button.interactable = enabled;
                SoulUi.Layout(button.gameObject, 190, height - 16);
            }
            return row;
        }

        protected static string ItemText(SoulItem item, SoulStatRules rules)
            => $"{SoulDescribe.ItemName(item)}\n<size=16><color=#{SoulUi.Hex(SoulUi.SubText)}>{SoulDescribe.ItemSummary(item, rules)}</color></size>";

        // Load line for a mercenary: worn weight against the carry limit (10 + strength × 4).
        protected static string LoadText(SoulMercenary hero)
        {
            float load = hero.Stats.Total(StatType.LoadRatio);
            var color = load > 1 ? SoulUi.Bad : load > SoulItemRules.LightLoad ? SoulUi.SubText : SoulUi.Good;
            return SoulUi.Colored($"무게 {hero.GearWeight:0.#}/{SoulItemRules.CarryLimit(hero.Stats.Total(StatType.Strength)):0}kg · {SoulItemRules.LoadName(load)}", color);
        }

        protected static string Brief(SoulMercenary hero)
            => $"<b>{hero.Name}</b>  <size=17><color=#{SoulUi.Hex(SoulUi.SubText)}>{hero.Race.Id} {hero.Job} · Lv.{hero.Level}</color></size>"
               + (hero.Wounds > 0 ? "  " + SoulUi.Colored($"부상 {hero.Wounds}", SoulUi.Bad) : "");

        public static Sprite ItemIcon(SoulItem item) => SoulDescribe.ItemIcon(item);

        // An item line: its icon framed in the grade colour, name and summary, the tooltip on hover.
        protected Image ItemEntry(Transform parent, SoulItem item, string action, bool enabled, UnityAction onClick, SoulMercenary wearer = null,
            string prefix = null, Color? background = null)
        {
            var row = Entry(parent, ItemIcon(item), (prefix ?? "") + ItemText(item, Campaign.Rules), action, enabled, onClick, 72, background, item.GradeColor);
            AttachItem(row, item, wearer);
            return row;
        }

        // A mercenary line with its portrait.
        protected Image HeroEntry(Transform parent, SoulMercenary hero, string text, string action, bool enabled, UnityAction onClick, float height = 72, Color? background = null)
            => Entry(parent, SoulIconSet.Ui("race"), text, action, enabled, onClick, height, background, null, hero);

        // ── picture-first pieces (SoulKit) ───────────────────────

        // An item as a tile: its picture framed in the grade colour; enchant count in the corner, a purple mark
        // for special options, the wearer's face when worn. Hover: everything about it.
        protected RectTransform ItemTile(Transform parent, SoulItem item, float size, bool selected, UnityAction onClick, SoulMercenary wearer = null,
            string badge = null, Color? badgeColor = null)
        {
            var tile = SoulKit.Tile(parent, ItemIcon(item), size, item.GradeColor, badge, badgeColor, selected, false, onClick);
            EnchantMarks(tile, item, size);
            if (item.Specials.Count > 0)
            {
                var mark = SoulUi.Panel("Special", tile, SoulItemRules.SpecialColor, true);
                mark.raycastTarget = false;
                SoulUi.Place(mark.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(-5, -5));
            }
            if (wearer != null)
            {
                var face = SoulUi.Portrait(tile, SoulPortraits.For(wearer), 30, SoulIconSet.Ui("race"));
                face.anchorMin = face.anchorMax = face.pivot = new Vector2(0, 1);
                face.anchoredPosition = new Vector2(3, -3);
                face.sizeDelta = new Vector2(30, 30);
            }
            AttachItem(tile, item, wearer);
            return tile;
        }

        // What a piece is enchanted with, at a glance: each option's stat icon along the bottom edge, framed in its tier colour.
        protected static void EnchantMarks(RectTransform tile, SoulItem item, float size)
        {
            if (item.Enchants.Count == 0) return;
            float mark = Mathf.Clamp(size * .27f, 15, 30);
            for (int i = 0; i < item.Enchants.Count; i++)
            {
                var enchant = item.Enchants[i];
                var box = SoulUi.Framed("Enchant", tile, new Color(.03f, .035f, .06f, .92f), TierColor(enchant.Tier), 1);
                box.GetComponent<Image>().raycastTarget = false;
                box.anchorMin = box.anchorMax = box.pivot = Vector2.zero;
                box.sizeDelta = new Vector2(mark, mark);
                box.anchoredPosition = new Vector2(3 + i * (mark + 1), 3);
                var icon = SoulUi.Icon(box, EnchantIcon(enchant), mark - 2);
                SoulUi.Stretch(icon.rectTransform, 1);
            }
        }

        protected static Sprite EnchantIcon(SoulEnchant enchant)
        {
            var option = SoulItemRules.Enchant(enchant.Option);
            return option != null && !option.Lightness ? SoulIconSet.Stat(option.Stat) : SoulIconSet.Stat(StatType.BodyWeight);
        }

        // An enchant's value tier: grey, blue, purple, gold.
        protected static Color TierColor(int tier) => tier >= 3 ? new Color(1f, .7f, .25f) : tier == 2 ? new Color(.78f, .5f, 1f) : tier == 1 ? new Color(.4f, .66f, 1f) : new Color(.6f, .62f, .7f);

        // ── the paper doll: the mercenary in the middle, six slots around it (the guild's 장비, the smithy) ──

        protected SoulItem pending, pendingReplace; // equip waiting for confirmation
        protected SoulEquipSlot gearSlot = SoulEquipSlot.MainHand;
        protected int gearAccessory;                // which of the two accessory slots
        protected bool slotOpen;                    // a slot clicked: the swap screen instead of the mercenary

        // The swap screen for one slot: [what it wears → the one picked], what changes (up / down), 교체 · 취소, then
        // the storehouse's pieces for that slot. What it wears goes back to the storehouse.
        protected void SlotSwap(Transform side, SoulMercenary hero)
        {
            if (pending != null && (!Campaign.Inventory.Contains(pending) || pending.Slot != gearSlot)) pending = null;
            var worn = SlotItem(hero, gearSlot, gearAccessory);
            SoulKit.Section(side, $"{SoulItemRules.SlotName(gearSlot)} 교체", hero.Name);
            // [worn → picked]
            var pair = SoulUi.Row(side, 96, 14);
            if (worn != null) ItemTile(pair, worn, 88, false, null, hero);
            else SoulKit.Tile(pair, SlotIcon(gearSlot), 88, SoulKit.Empty, null, null, false, true, null);
            var arrow = SoulUi.Text(pair, "→", 36, SoulUi.Accent, TextAlignmentOptions.Center);
            SoulUi.Layout(arrow.gameObject, 40, 88);
            if (pending != null) ItemTile(pair, pending, 88, true, null); // not worn yet: no wearer badge
            else SoulKit.Tile(pair, SlotIcon(gearSlot), 88, SoulKit.Empty, "?", null, false, true, null);
            // what changes: the mercenary as it is and as it would be (grade, material, enchants, a shield a two-hander drops)
            if (pending != null)
            {
                var lost = SoulCampaign.Replaced(hero, pending, pendingReplace);
                bool any = false;
                foreach (var entry in Campaign.SwapChanges(hero, pending, pendingReplace))
                {
                    var stat = entry.Key;
                    float change = entry.Value, was = hero.Stats.Total(stat), now = was + change;
                    bool worse = stat == StatType.LoadRatio || stat == StatType.BodyWeight ? change > 0 : change < 0;
                    string line = $"{SoulDescribe.StatName(Campaign.Rules, stat)}  {SoulDescribe.Value(stat, was)} → {SoulDescribe.Value(stat, now)}  "
                        + SoulUi.Colored(SoulDescribe.Signed(stat, change), worse ? SoulUi.Bad : SoulUi.Good);
                    var row = SoulUi.Text(side, line, 19, Color.white, TextAlignmentOptions.MidlineLeft);
                    SoulUi.Layout(row.gameObject, -1, 26);
                    any = true;
                }
                if (!any) { var same = SoulUi.Text(side, "능력치 변화 없음", 19, SoulUi.SubText, TextAlignmentOptions.MidlineLeft); SoulUi.Layout(same.gameObject, -1, 26); }
                if (lost.Count > 1)
                {
                    var also = SoulUi.Text(side, $"함께 빠짐: {string.Join(", ", lost.FindAll(l => l != worn).ConvertAll(l => l.Name))} (창고로)", 18, SoulUi.SubText, TextAlignmentOptions.MidlineLeft);
                    SoulUi.Layout(also.gameObject, -1, 24);
                }
            }
            // 교체 · 취소
            var buttons = SoulUi.Row(side, 50, 10);
            var swap = SoulKit.Action(buttons, "교체", new Color(.2f, .5f, .28f), pending != null, () =>
            {
                Message = Campaign.Equip(hero, pending, pendingReplace) ? null : "장착할 수 없습니다";
                pending = pendingReplace = null;
                slotOpen = false;
                Refresh();
            }, 50);
            SoulUi.Layout(swap.gameObject, -1, 50, 1);
            SoulTooltip.Attach(swap, SoulIconSet.Ui("lock"), "교체", () => new List<SoulLine> { new SoulLine(null, "끼고 있던 장비는 창고로 돌아갑니다.") });
            var cancel = SoulKit.Action(buttons, "취소", SoulUi.TabInactive, true, () => { pending = pendingReplace = null; slotOpen = false; Refresh(); }, 50);
            SoulUi.Layout(cancel.gameObject, -1, 50, 1);
            // the storehouse's pieces for this slot
            var fitting = Sorted(Campaign.Inventory).FindAll(item => item.Slot == gearSlot);
            SoulKit.Section(side, "보유 장비", $"{fitting.Count}");
            var grid = SoulKit.Grid(side, new Vector2(84, 84), 8);
            foreach (var item in fitting)
            {
                var chosen = item;
                string block = SoulCampaign.EquipBlock(hero, item);
                var tile = ItemTile(grid, item, 84, chosen == pending, block != null ? (UnityEngine.Events.UnityAction)(() => { Message = block; Refresh(); })
                    : () => { pending = chosen; pendingReplace = chosen.Slot == SoulEquipSlot.Accessory ? worn : null; Refresh(); }, null, null);
                if (block != null) tile.Find("Icon").GetComponent<Image>().color = new Color(1, 1, 1, .3f);
            }
        }


        protected static readonly (SoulEquipSlot slot, int index, Vector2 at)[] DollSlots =
        {
            (SoulEquipSlot.Head, 0, new Vector2(.5f, .87f)),
            (SoulEquipSlot.MainHand, 0, new Vector2(.15f, .55f)),
            (SoulEquipSlot.OffHand, 0, new Vector2(.85f, .55f)),
            (SoulEquipSlot.Body, 0, new Vector2(.5f, .15f)),
            (SoulEquipSlot.Accessory, 0, new Vector2(.15f, .17f)),
            (SoulEquipSlot.Accessory, 1, new Vector2(.85f, .17f)),
        };

        protected static SoulItem SlotItem(SoulMercenary hero, SoulEquipSlot slot, int index)
        {
            var worn = hero.Equipment.FindAll(e => e.Slot == slot);
            return index < worn.Count ? worn[index] : null;
        }

        protected static Sprite SlotIcon(SoulEquipSlot slot)
            => slot == SoulEquipSlot.MainHand ? SoulIconSet.Stat(StatType.Attack) : slot == SoulEquipSlot.OffHand || slot == SoulEquipSlot.Body ? SoulIconSet.Stat(StatType.Armor)
             : slot == SoulEquipSlot.Head ? SoulIconSet.Ui("look") : SoulIconSet.Ui("gold");

        // The doll; chosen tells which slot is picked, pick gets the slot, its index and what shows in it (a two-hander
        // shows in the off hand too).
        protected RectTransform Paperdoll(Transform side, SoulMercenary hero, System.Func<SoulEquipSlot, int, bool> chosen, System.Action<SoulEquipSlot, int, SoulItem> pick)
        {
            var doll = SoulUi.Panel("Doll", side, new Color(.04f, .045f, .08f, 1f), true).rectTransform;
            SoulUi.Layout(doll.gameObject, -1, 420);
            var figure = SoulUi.Portrait(doll, SoulPortraits.For(hero), 190, SoulIconSet.Ui("race"), 2.6f, .24f);
            figure.anchorMin = figure.anchorMax = new Vector2(.5f, .53f);
            figure.sizeDelta = new Vector2(190, 190);
            figure.anchoredPosition = Vector2.zero;
            foreach (var (slot, index, at) in DollSlots) SlotTile(doll, hero, slot, index, at, chosen(slot, index), pick);
            return doll;
        }

        void SlotTile(RectTransform doll, SoulMercenary hero, SoulEquipSlot slot, int index, Vector2 at, bool chosen, System.Action<SoulEquipSlot, int, SoulItem> pick)
        {
            var item = SlotItem(hero, slot, index);
            var main = hero.Equipped(SoulEquipSlot.MainHand);
            bool twoHands = slot == SoulEquipSlot.OffHand && item == null && main != null && main.TwoHanded && main.WeaponTag != "bow"; // (a bow leaves room for a quiver)
            var shown = twoHands ? main : item;
            UnityAction click = () => pick(slot, index, shown);
            RectTransform tile;
            if (twoHands)
            {
                // the two-hander fills the off hand too (faded)
                tile = ItemTile(doll, main, 80, chosen, click, null, "양손");
                var icon = tile.Find("Icon");
                if (icon != null) icon.GetComponent<Image>().color = new Color(1, 1, 1, .45f);
            }
            else tile = item != null ? ItemTile(doll, item, 80, chosen, click, null)
                : SoulKit.Tile(doll, SlotIcon(slot), 80, chosen ? SoulUi.Accent : SoulKit.Empty, null, null, chosen, true, click);
            tile.anchorMin = tile.anchorMax = at;
            tile.sizeDelta = new Vector2(80, 80);
            tile.anchoredPosition = Vector2.zero;
            if (slot == SoulEquipSlot.Head)
            {
                // the eye: the helmet's look shown or hidden (hidden by default)
                var eye = SoulUi.Framed("Eye", tile, new Color(.05f, .055f, .09f, .92f), hero.ShowHelmet ? SoulUi.Accent : SoulUi.PanelBorder, 2);
                eye.anchorMin = eye.anchorMax = eye.pivot = new Vector2(1, 1);
                eye.sizeDelta = new Vector2(28, 28);
                eye.anchoredPosition = new Vector2(8, 8);
                var mark = SoulUi.Icon(eye, SoulIconSet.Ui("look"), 22);
                SoulUi.Stretch(mark.rectTransform, 3);
                mark.color = new Color(1, 1, 1, hero.ShowHelmet ? 1f : .35f);
                eye.GetComponent<Image>().raycastTarget = true;
                SoulUi.Clickable(eye.gameObject).onClick.AddListener(() => { Campaign.ToggleHelmet(hero); Refresh(); });
                SoulTooltip.Attach(eye, SoulIconSet.Ui("look"), hero.ShowHelmet ? "투구 외형: 보임" : "투구 외형: 숨김",
                    () => new List<SoulLine> { new SoulLine(null, "누르면 투구 외형을 보이거나 숨깁니다. 능력치는 그대로입니다.") });
            }
        }

        // A mercenary as a card: portrait, name, level and title; wounds as red dots; a tag in the corner. 정예 and
        // 전설 stand out: their colour on the frame, a glow behind the face, the name and a ribbon.
        protected RectTransform HeroCard(Transform parent, SoulMercenary hero, bool selected, UnityAction onClick, string tag = null, Color? tagColor = null)
        {
            var rarity = hero.Rarity;
            bool graded = rarity != SoulStyleRarity.Normal;
            var shade = SoulHireStyles.RarityColor(rarity);
            var card = SoulUi.Framed("Card", parent, selected ? new Color(.12f, .14f, .23f, 1f) : SoulKit.CardBg,
                selected ? SoulUi.Accent : graded ? shade : SoulUi.PanelBorder, selected ? 4 : graded ? 3 : 2);
            if (graded)
            {
                var glow = SoulUi.Panel("Glow", card, new Color(shade.r, shade.g, shade.b, rarity == SoulStyleRarity.Rare ? .42f : .26f), true);
                glow.raycastTarget = false;
                SoulUi.Place(glow.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-66, -130), new Vector2(66, -4));
            }
            var face = SoulUi.Portrait(card, SoulPortraits.For(hero), 118, SoulIconSet.Ui("race"), 2.4f, .34f);
            face.anchorMin = face.anchorMax = face.pivot = new Vector2(.5f, 1);
            face.anchoredPosition = new Vector2(0, -8);
            face.sizeDelta = new Vector2(118, 118);
            if (graded) face.GetComponent<Image>().color = shade;
            SoulKit.JobBadge(face, hero, 32, Vector2.zero);
            var name = SoulUi.Text(card, $"<b>{hero.Name}</b>", 19, graded ? shade : Color.white, TextAlignmentOptions.Top);
            SoulUi.Place(name.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(4, 36), new Vector2(-4, 62));
            var sub = SoulUi.Text(card, $"Lv.{hero.Level} {(hero.Recruit != null ? hero.Recruit.ShownTitle : hero.Job)}", 15, SoulUi.SubText, TextAlignmentOptions.Top);
            sub.textWrappingMode = TextWrappingModes.NoWrap;
            sub.overflowMode = TextOverflowModes.Ellipsis;
            SoulUi.Place(sub.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(4, 16), new Vector2(-4, 38));
            if (hero.Wounds > 0)
            {
                var pips = SoulUi.Rect("Wounds", card);
                SoulUi.Place(pips, new Vector2(0, 0), new Vector2(1, 0), new Vector2(8, 2), new Vector2(-8, 14));
                var row = SoulUi.Horizontal(pips.gameObject, 3);
                row.childAlignment = TextAnchor.MiddleCenter;
                row.childForceExpandWidth = false;
                for (int i = 0; i < hero.Wounds; i++) { var dot = SoulUi.Panel("Wound", pips, SoulUi.Bad, true); dot.raycastTarget = false; SoulUi.Layout(dot.gameObject, 10, 10); }
            }
            // the grade as a ribbon (top right)
            if (graded)
            {
                var badge = SoulUi.Panel("Grade", card, shade, true);
                badge.raycastTarget = false;
                SoulUi.Place(badge.rectTransform, Vector2.one, Vector2.one, new Vector2(-58, -32), new Vector2(-4, -4));
                var label = SoulUi.Text(badge.transform, SoulHireStyles.RarityName(rarity), 16, new Color(.08f, .06f, .04f), TextAlignmentOptions.Center);
                label.fontStyle = FontStyles.Bold;
                SoulUi.Stretch(label.rectTransform);
            }
            if (tag != null)
            {
                var pill = SoulUi.Panel("Tag", card, tagColor ?? SoulUi.Good, true);
                pill.raycastTarget = false;
                SoulUi.Place(pill.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(6, -30), new Vector2(6 + 12 + tag.Length * 16, -6));
                var text = SoulUi.Text(pill.transform, tag, 14, Color.black, TextAlignmentOptions.Center);
                text.fontStyle = FontStyles.Bold;
                SoulUi.Stretch(text.rectTransform);
            }
            if (onClick != null)
            {
                card.GetComponent<Image>().raycastTarget = true;
                SoulUi.Clickable(card.gameObject).onClick.AddListener(onClick);
            }
            return card;
        }

        // An empty seat (party slot, socket): a faint frame with an icon.
        protected static RectTransform EmptySeat(Transform parent, Sprite icon)
        {
            var seat = SoulUi.Framed("Seat", parent, new Color(.04f, .045f, .07f, .9f), SoulKit.Empty, 2);
            var image = SoulUi.Icon(seat, icon, 40);
            image.color = new Color(1, 1, 1, .2f);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            return seat;
        }

        static readonly StatType[] MainStats = { StatType.Strength, StatType.Vitality, StatType.Agility, StatType.Magic, StatType.Will, StatType.Luck };
        static readonly (string, StatType[])[] CombatGroups =
        {
            ("방어", new[] { StatType.Armor, StatType.Evasion }),
            ("내성", new[] { StatType.PhysicalResist, StatType.MagicResist, StatType.BleedResist, StatType.PoisonResist, StatType.BurnResist,
                             StatType.ChillResist, StatType.FearResist, StatType.StunResist, StatType.PetrifyResist }),
            ("공격 · 이동", new[] { StatType.Attack, StatType.Accuracy, StatType.ActionSpeed, StatType.MoveSpeed }),
            ("HP · 스태미나 · MP", new[] { StatType.MaxHp, StatType.MaxStamina, StatType.MaxMp, StatType.HpRegen, StatType.StaminaRegen, StatType.MpRegen }),
        };

        // The six main stats, named, three to a line; the rest is one button away (자세히 보기).
        protected void HeroStats(Transform parent, SoulMercenary hero)
        {
            var rules = Campaign.Rules;
            var grid = SoulKit.Grid(parent, new Vector2(176, 40), 8);
            foreach (var stat in MainStats)
            {
                var id = stat;
                var chip = SoulKit.IconValue(grid, SoulIconSet.Stat(stat), $"{SoulDescribe.StatName(rules, stat)} <b>{hero.Stats.Total(stat):0.#}</b>", SoulDescribe.StatColor(rules, stat), 40);
                SoulTooltip.Attach(chip, SoulIconSet.Stat(id), SoulDescribe.StatName(rules, id), () => SoulDescribe.UpperTooltip(hero, id, rules));
            }
            SoulKit.Action(parent, "자세히 보기", SoulUi.TabInactive, true, () => ShowStatSheet(hero), 40, SoulIconSet.Ui("expand"));
        }

        // A window over the building: every stat with its name and value (hover: where it comes from).
        protected void ShowStatSheet(SoulMercenary hero)
        {
            var rules = Campaign.Rules;
            var shade = SoulUi.Panel("StatSheet", overlayRoot, new Color(0, 0, 0, .7f));
            SoulUi.Stretch(shade.rectTransform);
            SoulUi.Clickable(shade.gameObject, true).onClick.AddListener(() => Destroy(shade.gameObject));
            var window = SoulUi.Framed("Window", shade.transform, SoulUi.PanelBg, SoulUi.Accent, 3);
            window.anchorMin = window.anchorMax = new Vector2(.5f, .5f);
            window.sizeDelta = new Vector2(1200, 800);
            window.GetComponent<Image>().raycastTarget = true;
            window.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // clicks inside do not close it
            var close = SoulUi.Button(window, "닫기", SoulUi.Bad, () => Destroy(shade.gameObject), 20, SoulIconSet.Ui("close"));
            SoulUi.Place((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-150, -62), new Vector2(-16, -16));
            var area = SoulUi.Rect("Area", window);
            SoulUi.Place(area, Vector2.zero, Vector2.one, new Vector2(20, 16), new Vector2(-20, -16));
            var content = SoulUi.Rect("Content", area);
            SoulUi.Stretch(content);
            var column = SoulUi.Vertical(content.gameObject, 8);
            column.childControlHeight = true;
            column.childForceExpandHeight = false;
            var face = SoulUi.Portrait(content, SoulPortraits.For(hero), 96, SoulIconSet.Ui("race"), 2.6f, .38f);
            SoulKit.JobBadge(face, hero, 28, new Vector2(1, 0));
            SoulKit.Header(content, face, hero.Name, $"{hero.Race.Id} {hero.Job} · Lv.{hero.Level} · {hero.Grade}등급");
            var cell = new Vector2(222, 40);
            SoulKit.Section(content, "주 스탯");
            var upper = SoulKit.Grid(content, cell, 8);
            for (var stat = StatType.Strength; stat <= StatType.PiercePower; stat++)
            {
                var id = stat;
                var chip = SoulKit.IconValue(upper, SoulIconSet.Stat(stat), $"{SoulDescribe.StatName(rules, stat)} <b>{hero.Stats.Total(stat):0.#}</b>", SoulDescribe.StatColor(rules, stat), 40);
                SoulTooltip.Attach(chip, SoulIconSet.Stat(id), SoulDescribe.StatName(rules, id), () => SoulDescribe.UpperTooltip(hero, id, rules));
            }
            foreach (var (title, stats) in CombatGroups)
            {
                SoulKit.Section(content, title);
                var grid = SoulKit.Grid(content, cell, 8);
                foreach (var stat in stats)
                {
                    var id = stat;
                    var chip = SoulKit.IconValue(grid, SoulIconSet.Stat(stat), $"{SoulStatRules.CombatName(stat)} <b>{SoulDescribe.Value(stat, hero.Stats.Total(stat))}</b>", Color.white, 40);
                    SoulTooltip.Attach(chip, SoulIconSet.Stat(id), SoulStatRules.CombatName(id), () => SoulDescribe.CombatTooltip(hero, id, rules));
                }
            }
        }

        protected static string BuyQuestion(string what, int price) => $"정말 구매하시겠습니까?\n<size=22>{what} · {price} 금화</size>";

        // One question before anything costly or final (ConfirmPopup; "다시 묻지 않기" is a setting per kind).
        protected void Confirm(string kind, string question, UnityAction yes) => ConfirmPopup.Ask(kind, question, () => { yes(); Refresh(); });

        // Patterns, skills, passives and souls as small tiles under their own labels (hover to read).
        protected void HeroKit(Transform parent, SoulMercenary hero)
        {
            var rules = Campaign.Rules;
            var patterns = hero.OwnedPatterns();
            if (patterns.Count > 0)
            {
                SoulKit.Section(parent, "패턴", $"{patterns.Count}");
                var tiles = SoulKit.Grid(parent, new Vector2(56, 56), 6);
                foreach (var pattern in patterns)
                {
                    var owned = pattern;
                    string block = hero.BlockReason(pattern);
                    var tile = SoulKit.Tile(tiles, SoulDescribe.PatternIcon(pattern), 56, block != null ? SoulKit.Empty : CategoryColor(pattern.Category), null, null, false, block != null);
                    SoulTooltip.Attach(tile, () => SoulDescribe.PatternIcon(owned), () => SoulDescribe.PatternTitle(owned), () =>
                    {
                        var lines = SoulMercenaryPopup.PatternTooltip(hero, owned, rules);
                        string why = hero.BlockReason(owned);
                        if (why != null) lines.Insert(0, new SoulLine(SoulIconSet.Ui("lock"), SoulUi.Colored(why, SoulUi.Bad)));
                        return lines;
                    });
                }
            }
            var skills = hero.AllActiveSkills();
            SoulKit.Section(parent, "스킬", $"{skills.Count}");
            if (skills.Count > 0)
            {
                var tiles = SoulKit.Grid(parent, new Vector2(56, 56), 6);
                foreach (var skill in skills)
                {
                    var known = skill;
                    var tile = SoulKit.Tile(tiles, skill.Icon != null ? skill.Icon : SoulIconSet.Ui("skill"), 56, SoulUi.Accent, hero.SkillLevel(skill) > 1 ? $"{hero.SkillLevel(skill)}" : null);
                    SoulTooltip.Attach(tile, known.Icon != null ? known.Icon : SoulIconSet.Ui("skill"), SoulDescribe.SkillTitle(hero, known), () => SoulDescribe.SkillLines(hero, known, rules));
                }
            }
            var passives = hero.Passives();
            if (passives.Count > 0)
            {
                SoulKit.Section(parent, "패시브", $"{passives.Count}");
                var tiles = SoulKit.Grid(parent, new Vector2(56, 56), 6);
                foreach (var passive in passives)
                {
                    var held = passive;
                    var tile = SoulKit.Tile(tiles, passive.Icon != null ? passive.Icon : SoulIconSet.Ui("passive"), 56, SoulUi.Good);
                    SoulTooltip.Attach(tile, held.Icon != null ? held.Icon : SoulIconSet.Ui("passive"), held.SkillName, () => SoulDescribe.PassiveLines(hero, held, rules));
                }
            }
            SoulKit.Section(parent, "영혼", $"{hero.Souls.Count}/{hero.SoulSlots}칸");
            if (hero.Souls.Count > 0)
            {
                var tiles = SoulKit.Grid(parent, new Vector2(56, 56), 6);
                foreach (var soul in hero.Souls)
                {
                    var kept = soul;
                    var tile = SoulKit.SoulTile(tiles, soul, 56, SoulUi.Soul, $"{soul.Grade}");
                    SoulTooltip.Attach(tile, SoulKit.SoulIcon(kept), $"{kept.OriginMonster}의 영혼", () => SoulLinesFor(kept));
                }
            }
        }

        protected List<SoulLine> SoulLinesFor(SoulData soul)
        {
            var lines = new List<SoulLine> { new SoulLine(SoulIconSet.Ui("soul"), $"{soul.Grade}등급") };
            foreach (var bonus in soul.CharacteristicStats)
                lines.Add(new SoulLine(SoulIconSet.Stat(bonus.Stat), $"{SoulDescribe.StatName(Campaign.Rules, bonus.Stat)} {SoulUi.Colored($"+{bonus.Value * HeroStatPipeline.SoulPower:0.#}", SoulUi.Good)}"));
            foreach (var pattern in soul.Patterns) if (pattern != null) lines.Add(new SoulLine(SoulDescribe.PatternIcon(pattern), $"패턴 {pattern.Id}"));
            foreach (var skill in soul.ActiveSkills) if (skill != null) lines.Add(new SoulLine(SoulIconSet.Ui("skill"), $"스킬 {skill.SkillName}"));
            return lines;
        }

        // The selected item in the side panel.
        protected void ItemDetail(Transform side, SoulItem item, SoulMercenary wearer = null)
        {
            var tile = SoulKit.Tile(side, ItemIcon(item), 88, item.GradeColor);
            SoulKit.Header(side, tile, SoulDescribe.ItemName(item), SoulDescribe.ItemSummary(item, Campaign.Rules));
            SoulKit.Lines(side, SoulDescribe.EquipmentLines(item, Campaign.Rules, wearer));
        }

        // A mercenary's face and name at the top of the side panel.
        protected RectTransform HeroHeader(Transform side, SoulMercenary hero)
        {
            var face = SoulUi.Portrait(side, SoulPortraits.For(hero), 96, SoulIconSet.Ui("race"), 2.6f, .38f);
            SoulKit.JobBadge(face, hero, 28, new Vector2(1, 0));
            var recruit = hero.Recruit;
            var shade = SoulHireStyles.RarityColor(hero.Rarity);
            bool graded = hero.Rarity != SoulStyleRarity.Normal;
            if (graded) face.GetComponent<Image>().color = shade;
            string kind = recruit != null && !string.IsNullOrEmpty(recruit.Title) ? SoulUi.Colored(recruit.Title, shade) + " · " : "";
            string name = graded ? $"{SoulUi.Colored(hero.Name, shade)}  <size=18>{SoulUi.Colored(SoulHireStyles.RarityName(hero.Rarity), shade)}</size>" : hero.Name;
            var header = SoulKit.Header(side, face, name, $"{kind}{hero.Race.Id} {hero.Job} · Lv.{hero.Level} · {hero.Grade}등급");
            int at = header.GetSiblingIndex();
            if (recruit != null && !string.IsNullOrEmpty(recruit.Concept))
            {
                var concept = SoulUi.Text(side, recruit.Concept, 17, SoulUi.SubText);
                SoulUi.Layout(concept.gameObject, -1, 26);
                concept.transform.SetSiblingIndex(++at);
            }
            // 고유 특성: what only this one has
            var trait = recruit != null ? recruit.Signature : null;
            if (trait != null)
            {
                var box = SoulUi.Framed("Trait", side, new Color(shade.r * .18f, shade.g * .18f, shade.b * .18f, .95f), shade, 2);
                SoulUi.Layout(box.gameObject, -1, 80);
                box.SetSiblingIndex(++at);
                var icon = SoulUi.Icon(box, trait.Icon, 44);
                SoulUi.Place(icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(10, -22), new Vector2(54, 22));
                var text = SoulUi.Text(box, $"<b>{SoulUi.Colored("고유 특성 · " + trait.SkillName, shade)}</b>\n<size=15>{trait.Description}</size>", 18, Color.white, TextAlignmentOptions.MidlineLeft);
                SoulUi.Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(64, 4), new Vector2(-8, -4));
            }
            return header;
        }

        // Everything about a mercenary at a glance: the six main stats and the combat numbers (hover for where
        // each comes from), then patterns, skills, passives and souls as icon tiles with their tooltips.
        protected void HeroDetails(Transform parent, SoulMercenary hero)
        {
            var rules = Campaign.Rules;
            SubHeading(parent, "능력치");
            var main = SoulUi.Flow(parent, new Vector2(196, 40), 6);
            foreach (var stat in new[] { StatType.Strength, StatType.Vitality, StatType.Agility, StatType.Magic, StatType.Will, StatType.Luck })
            {
                var id = stat;
                var chip = SoulUi.Chip(main, SoulIconSet.Stat(stat), $"{SoulDescribe.StatName(rules, stat)} {hero.Stats.Total(stat):0.#}", SoulDescribe.StatColor(rules, stat), 40);
                SoulTooltip.Attach(chip, SoulIconSet.Stat(id), SoulDescribe.StatName(rules, id), () => SoulDescribe.UpperTooltip(hero, id, rules));
            }
            var combat = SoulUi.Flow(parent, new Vector2(196, 40), 6);
            var shown = new[] { StatType.MaxHp, StatType.MaxStamina, StatType.MaxMp, StatType.Attack, StatType.Armor, StatType.Evasion,
                                StatType.Accuracy, StatType.ActionSpeed, StatType.MoveSpeed, StatType.SpellPower };
            foreach (var stat in shown)
            {
                var id = stat;
                var chip = SoulUi.Chip(combat, SoulIconSet.Stat(stat), $"{SoulStatRules.CombatName(stat)} {SoulDescribe.Value(stat, SoulDescribe.CombatValue(hero, stat))}", Color.white, 40);
                SoulTooltip.Attach(chip, SoulIconSet.Stat(id), SoulStatRules.CombatName(id), () => SoulDescribe.CombatTooltip(hero, id, rules));
            }

            var patterns = hero.OwnedPatterns();
            SubHeading(parent, $"패턴 {patterns.Count}");
            var patternRow = SoulUi.Flow(parent, new Vector2(64, 64), 6);
            foreach (var pattern in patterns)
            {
                var owned = pattern;
                string block = hero.BlockReason(pattern);
                var tile = SoulUi.Tile(patternRow, SoulDescribe.PatternIcon(pattern), 64, block != null ? new Color(.35f, .35f, .4f) : CategoryColor(pattern.Category));
                var icon = tile.Find("Icon")?.GetComponent<Image>();
                if (block != null && icon != null) icon.color = new Color(1, 1, 1, .35f); // owned but unusable now
                SoulTooltip.Attach(tile, () => SoulDescribe.PatternIcon(owned), () => SoulDescribe.PatternTitle(owned), () =>
                {
                    var lines = SoulMercenaryPopup.PatternTooltip(hero, owned, rules);
                    string why = hero.BlockReason(owned);
                    if (why != null) lines.Insert(0, new SoulLine(SoulIconSet.Ui("lock"), SoulUi.Colored(why, SoulUi.Bad)));
                    return lines;
                });
            }

            var skills = hero.AllActiveSkills();
            SubHeading(parent, $"스킬 {skills.Count}");
            if (skills.Count == 0) { var none = SoulUi.Text(parent, "배운 스킬이 없습니다 — 서고에서 배웁니다", 17, SoulUi.SubText); SoulUi.Layout(none.gameObject, -1, 28); }
            var skillRow = SoulUi.Flow(parent, new Vector2(64, 64), 6);
            foreach (var skill in skills)
            {
                var known = skill;
                var tile = SoulUi.Tile(skillRow, skill.Icon != null ? skill.Icon : SoulIconSet.Ui("skill"), 64, SoulUi.Accent);
                SoulTooltip.Attach(tile, known.Icon != null ? known.Icon : SoulIconSet.Ui("skill"), SoulDescribe.SkillTitle(hero, known),
                    () => SoulDescribe.SkillLines(hero, known, rules));
            }

            var passives = hero.Passives();
            SubHeading(parent, $"패시브 {passives.Count}");
            var passiveRow = SoulUi.Flow(parent, new Vector2(64, 64), 6);
            foreach (var passive in passives)
            {
                var held = passive;
                var tile = SoulUi.Tile(passiveRow, passive.Icon != null ? passive.Icon : SoulIconSet.Ui("passive"), 64, SoulUi.Good);
                SoulTooltip.Attach(tile, held.Icon != null ? held.Icon : SoulIconSet.Ui("passive"), held.SkillName, () => SoulDescribe.PassiveLines(hero, held, rules));
            }

            if (hero.Souls.Count > 0 || hero.SoulSlots > 0)
            {
                SubHeading(parent, $"영혼 {hero.Souls.Count}/{hero.SoulSlots}");
                foreach (var soul in hero.Souls)
                    Entry(parent, SoulKit.SoulIcon(soul), $"{soul.OriginMonster}의 영혼  <size=16>{soul.Grade}등급</size>", null, false, null, 48);
            }
        }

        static Color CategoryColor(SoulPatternCategory category)
        {
            switch (category)
            {
                case SoulPatternCategory.Attack: return new Color(.9f, .45f, .35f);
                case SoulPatternCategory.Defense: return new Color(.4f, .6f, .95f);
                case SoulPatternCategory.Movement: return new Color(.45f, .85f, .6f);
                case SoulPatternCategory.Support: return new Color(.95f, .8f, .4f);
                case SoulPatternCategory.Chain: return new Color(.8f, .55f, 1f);
                default: return SoulUi.SubText;
            }
        }

        // Items in a stable order: by slot, then grade (best first), then name.
        protected static List<SoulItem> Sorted(IEnumerable<SoulItem> items)
        {
            var list = new List<SoulItem>(items);
            list.Sort((a, b) => a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : a.Grade != b.Grade ? b.Grade.CompareTo(a.Grade) : string.CompareOrdinal(a.Data.Name, b.Data.Name));
            return list;
        }

        protected void AttachItem(Component row, SoulItem item, SoulMercenary wearer = null)
            => SoulTooltip.Attach(row, SoulIconSet.Ui("equipment"), item.Name, () => SoulDescribe.EquipmentLines(item, Campaign.Rules, wearer));

        protected static List<SoulLine> WoundLines(SoulMercenary hero)
            => new List<SoulLine>
            {
                new SoulLine(SoulIconSet.Status(SoulStatus.Bleed), $"부상 {hero.Wounds}/{SoulMercenary.MaxWounds}단계"),
                new SoulLine(SoulIconSet.Stat(StatType.MaxStamina), $"스태미나·MP 소모 +{SoulCombat.WoundCostStep * 100 * hero.Wounds:0}%"),
                new SoulLine(SoulIconSet.Ui("lock"), $"패턴 실패 확률 {SoulCombat.WoundFailChance(hero) * 100:0}% ({SoulCombat.WoundFailFrom}단계부터)"),
                new SoulLine(null, "휴식으로는 낫지 않습니다. 치유 마법·회복 포션·성당에서만 회복됩니다."),
            };
    }
}
