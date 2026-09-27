using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // Mercenary details as a PopupManager popup (PopupType.SoulMercenary). The prefab only carries the
    // PopupBase root; the content is built here and refreshed while the battle keeps running behind it.
    // Every command goes through the session (Absorb, Preserve, OpenLevel, ConfirmLevel).
    //
    // Layout: the character sheet (portrait, level/EXP, HP/stamina/MP, equipment, soul slots) stays on the
    // left for every tab; the right side shows the tab. Each tab fits the screen — only lists that can grow
    // (skills, stash, bestiary) scroll inside their own column.
    public sealed class SoulMercenaryPopup : PopupBase
    {
        public enum Tab { Stats, Souls, Codex }

        const float Width = 1560, Height = 900, SideWidth = 420, Gap = 16;

        static readonly StatType[] MainStats = { StatType.Strength, StatType.Vitality, StatType.Agility, StatType.Magic, StatType.Will, StatType.Luck };
        static readonly StatType[] OtherUpperStats = { StatType.Durability, StatType.AntiMagic, StatType.Regeneration, StatType.Recovery, StatType.SlashPower, StatType.ImpactPower, StatType.PiercePower };
        static readonly StatType[] KeyDetailStats = { StatType.Attack, StatType.Armor, StatType.ActionSpeed, StatType.MoveSpeed, StatType.Accuracy, StatType.Evasion, StatType.PhysicalResist, StatType.MagicResist, StatType.SpellPower, StatType.MpRegen };
        static readonly StatType[] ResourceStats = { StatType.MaxHp, StatType.MaxStamina, StatType.MaxMp, StatType.HpRegen, StatType.StaminaRegen, StatType.MpRegen, StatType.ManaRegenRate };
        static readonly StatType[] OffenseStats = { StatType.Attack, StatType.SpellPower, StatType.Accuracy, StatType.ActionSpeed, StatType.CritChance, StatType.CritDamage, StatType.PatternCostReduce, StatType.FreePatternChance, StatType.ExecuteBonus };
        static readonly StatType[] DefenseStats = { StatType.Armor, StatType.PhysicalResist, StatType.MagicResist, StatType.Evasion, StatType.KnockbackResist, StatType.Threat };
        static readonly StatType[] BodyStats = { StatType.MoveSpeed, StatType.BodyWeight };
        static readonly StatType[] ResistStats = { StatType.BleedResist, StatType.PoisonResist, StatType.BurnResist, StatType.ChillResist, StatType.FearResist, StatType.StunResist, StatType.PetrifyResist, StatType.DownResist };

        SoulGameplayController controller;
        SoulWorldView world;
        int heroIndex;
        Tab tab;
        string signature, view;
        int selectedDrop, selectedPattern;
        bool details;
        SoulLevelChoice levelChoice;

        bool built;
        RectTransform body, equipmentRow, soulSlots, buffRow;
        ScrollRect scroll;
        Image portrait, jobBadge;
        TextMeshProUGUI nameText, infoText, bodyText, actionText, levelText, expText, hpText, staminaText, manaText, equipmentHint, memoryText;
        RectTransform expBar, hpBar, staminaBar, manaBar;
        readonly Dictionary<Tab, Button> tabs = new Dictionary<Tab, Button>();
        readonly List<Image> switcherPortraits = new List<Image>();
        Button leaderButton;

        SoulDungeonSession Session => controller.Session;
        SoulStatRules Rules => controller.StatRules;
        SoulMercenary Hero => Session.Mercenaries[Mathf.Clamp(heroIndex, 0, Session.Mercenaries.Count - 1)];

        // pickPattern: open straight into the new-pattern choice (HUD level-up badge).
        public static void Open(SoulGameplayController controller, SoulWorldView world, int hero, Tab tab, bool pickPattern = false)
        {
            var manager = PopupManager.Instance;
            if (manager == null) { Debug.LogWarning("[SoulMercenaryPopup] PopupManager 가 씬에 없습니다."); return; }
            var popup = manager.IsOpen(PopupType.SoulMercenary)
                ? manager.Get<SoulMercenaryPopup>(PopupType.SoulMercenary)
                : manager.Open<SoulMercenaryPopup>(PopupType.SoulMercenary);
            popup?.Bind(controller, world, hero, tab, pickPattern);
        }

        public void Bind(SoulGameplayController controller, SoulWorldView world, int hero, Tab tab, bool pickPattern = false)
        {
            this.controller = controller;
            this.world = world;
            heroIndex = hero;
            this.tab = tab;
            signature = null;
            details = false;
            levelChoice = pickPattern && tab == Tab.Stats ? Session.OpenLevel(Hero) : null;
            selectedPattern = 0;
            if (!built) Build();
        }

        protected override void OnAfterClose() => SoulTooltip.Hide();

        // ── construction ─────────────────────────────────────────

        void Build()
        {
            built = true;
            var root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
            root.sizeDelta = new Vector2(Width, Height);
            var frame = SoulUi.Framed("Frame", transform, SoulUi.PanelBg, SoulUi.PanelBorder, 3);
            SoulUi.Stretch(frame);

            BuildSheet(frame);

            float right = 14 + SideWidth + Gap;
            var tabRow = SoulUi.Rect("Tabs", frame);
            SoulUi.Place(tabRow, new Vector2(0, 1), Vector2.one, new Vector2(right, -70), new Vector2(-84, -14));
            var tabLayout = SoulUi.Horizontal(tabRow.gameObject, 8);
            tabLayout.childAlignment = TextAnchor.MiddleLeft;
            AddTab(tabRow, Tab.Stats, "능력치", SoulIconSet.Stat(StatType.Strength));
            AddTab(tabRow, Tab.Souls, "영혼", SoulIconSet.Ui("soul"));
            AddTab(tabRow, Tab.Codex, "도감", SoulIconSet.Ui("map"));

            var close = SoulUi.Button(frame, "", SoulUi.Bad, () => Close(), 26, SoulIconSet.Ui("close"));
            SoulUi.Place((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-70, -68), new Vector2(-18, -16));
            if (SoulIconSet.Ui("close") == null) SoulUi.SetLabel(close, "X");

            body = SoulUi.Rect("Body", frame);
            SoulUi.Place(body, Vector2.zero, Vector2.one, new Vector2(right, 14), new Vector2(-14, -84));
        }

        // Left column: who this is and what state it is in. Visible on every tab so equipment and souls
        // are always judged against the character they go on.
        void BuildSheet(Transform frame)
        {
            var sheet = SoulUi.Panel("Character", frame, SoulUi.HeaderBg, true).rectTransform;
            SoulUi.Place(sheet, Vector2.zero, new Vector2(0, 1), new Vector2(14, 14), new Vector2(14 + SideWidth, -14));
            SoulUi.Vertical(sheet.gameObject, 10, 14);

            var switcher = SoulUi.Row(sheet, 52, 8);
            for (int i = 0; i < Session.Mercenaries.Count; i++)
            {
                int index = i;
                var button = SoulUi.Button(switcher, "", SoulUi.SlotBg, () => { heroIndex = index; levelChoice = null; signature = null; });
                SoulUi.Layout(button.gameObject, 52, 52);
                var mask = SoulUi.Rect("Mask", button.transform);
                SoulUi.Stretch(mask, 3);
                mask.gameObject.AddComponent<RectMask2D>();
                var face = SoulUi.Icon(mask, null, 10, "Face");
                face.rectTransform.anchorMin = face.rectTransform.anchorMax = new Vector2(.5f, 0);
                face.rectTransform.pivot = new Vector2(.5f, 0);
                face.rectTransform.sizeDelta = new Vector2(170, 170);
                face.rectTransform.anchoredPosition = new Vector2(0, -34);
                switcherPortraits.Add(face);
            }

            var identity = SoulUi.Row(sheet, 196, 14);
            var portraitBox = SoulUi.Panel("Portrait", identity, new Color(0, 0, 0, .35f), true).rectTransform;
            SoulUi.Layout(portraitBox.gameObject, 150, 196);
            portraitBox.gameObject.AddComponent<RectMask2D>();
            portrait = SoulUi.Icon(portraitBox, null, 10, "Sprite");
            jobBadge = SoulUi.Icon(portraitBox, null, 34, "Job"); // the job's badge (set per mercenary in Update)
            jobBadge.rectTransform.anchorMin = jobBadge.rectTransform.anchorMax = jobBadge.rectTransform.pivot = new Vector2(1, 0);
            jobBadge.rectTransform.anchoredPosition = new Vector2(-4, 4);
            jobBadge.GetComponent<LayoutElement>().ignoreLayout = true;
            portrait.rectTransform.anchorMin = portrait.rectTransform.anchorMax = new Vector2(.5f, 0);
            portrait.rectTransform.pivot = new Vector2(.5f, 0);
            portrait.rectTransform.sizeDelta = new Vector2(470, 470); // 64px frame: the body sits low in the frame
            portrait.rectTransform.anchoredPosition = new Vector2(0, -92);
            var info = SoulUi.Rect("Info", identity);
            SoulUi.Layout(info.gameObject, -1, -1, 1);
            SoulUi.Vertical(info.gameObject, 4);
            nameText = SoulUi.Text(info, "", 30, Color.white);
            nameText.fontStyle = FontStyles.Bold;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            SoulUi.Layout(nameText.gameObject, -1, 40);
            infoText = SoulUi.Text(info, "", 19, SoulUi.SubText);
            SoulUi.Layout(infoText.gameObject, -1, 26);
            bodyText = SoulUi.Text(info, "", 18, SoulUi.SubText);
            SoulUi.Layout(bodyText.gameObject, -1, 24);
            actionText = SoulUi.Text(info, "", 18, SoulUi.Accent, TextAlignmentOptions.TopLeft);
            SoulUi.Layout(actionText.gameObject, -1, 46);
            leaderButton = SoulUi.Button(info, "리더 지정", SoulUi.TabInactive, () => { Session.SetLeader(Hero); signature = null; }, 19, SoulIconSet.Ui("leader"));
            SoulUi.Layout(leaderButton.gameObject, -1, 42);

            // level + EXP sit with the resource bars: everything that fills up over time in one block
            var levelRow = SoulUi.Row(sheet, 28, 8);
            levelRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            levelText = SoulUi.Text(levelRow, "", 22, SoulUi.Accent);
            levelText.fontStyle = FontStyles.Bold;
            levelText.textWrappingMode = TextWrappingModes.NoWrap;
            SoulUi.Layout(levelText.gameObject, 96, 28);
            expBar = SoulUi.Bar(levelRow, SoulUi.Accent, 14);
            SoulUi.Layout(expBar.parent.gameObject, -1, 14, 1);
            expText = SoulUi.Text(levelRow, "", 17, SoulUi.SubText, TextAlignmentOptions.MidlineRight);
            expText.textWrappingMode = TextWrappingModes.NoWrap;
            SoulUi.Layout(expText.gameObject, 118, 28);
            SoulTooltip.Attach(levelRow, SoulIconSet.Ui("level"), "레벨과 경험치", () => LevelLines(Hero));

            var resources = SoulUi.Rect("Resources", sheet);
            SoulUi.Vertical(resources.gameObject, 6);
            hpBar = SoulUi.IconBar(resources, SoulIconSet.Stat(StatType.MaxHp), SoulUi.HpColor, 20, out hpText);
            staminaBar = SoulUi.IconBar(resources, SoulIconSet.Stat(StatType.MaxStamina), SoulUi.StaminaColor, 16, out staminaText);
            manaBar = SoulUi.IconBar(resources, SoulIconSet.Stat(StatType.MaxMp), SoulUi.ManaColor, 16, out manaText);
            AttachStat(hpBar.parent.parent, StatType.MaxHp);
            AttachStat(staminaBar.parent.parent, StatType.MaxStamina);
            AttachStat(manaBar.parent.parent, StatType.MaxMp);

            buffRow = SoulUi.Flow(sheet, new Vector2(192, 34), 6);

            equipmentHint = SheetTitle(sheet, "장비");
            equipmentRow = SoulUi.Row(sheet, 80, 8);
            SheetTitle(sheet, "영혼 슬롯").text = "10레벨마다 +1칸";
            soulSlots = SoulUi.Row(sheet, 56, 8);
            SheetTitle(sheet, "경험");
            memoryText = SoulUi.Text(sheet, "", 18, SoulUi.SubText, TextAlignmentOptions.TopLeft);
            SoulUi.Layout(memoryText.gameObject, -1, 52);
        }

        // Title with a right-aligned hint; returns the hint so callers can fill it in.
        static TextMeshProUGUI SheetTitle(Transform parent, string title)
        {
            var row = SoulUi.Row(parent, 26, 8);
            var label = SoulUi.Text(row, title, 21, SoulUi.SectionLbl, TextAlignmentOptions.BottomLeft);
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var hint = SoulUi.Text(row, "", 16, new Color(.55f, .58f, .7f), TextAlignmentOptions.BottomRight);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            SoulUi.Layout(hint.gameObject, -1, -1, 1);
            return hint;
        }

        void AttachStat(Component target, StatType stat)
            => SoulTooltip.Attach(target, () => StatIcon(stat),
                () => $"{SoulDescribe.StatName(Rules, stat)} {SoulDescribe.Value(stat, SoulDescribe.CombatValue(Hero, stat))}",
                () => HeroStatPipeline.IsUpper(stat) ? SoulDescribe.UpperTooltip(Hero, stat, Rules) : SoulDescribe.CombatTooltip(Hero, stat, Rules));

        void AddTab(Transform parent, Tab value, string label, Sprite icon)
        {
            tabs[value] = SoulUi.Button(parent, label, SoulUi.TabInactive, () =>
            {
                tab = value;
                details = false;
                signature = null;
                SoulTooltip.Hide();
            }, 24, icon);
            SoulUi.Layout(tabs[value].gameObject, 210, -1);
        }

        // ── per frame ────────────────────────────────────────────

        void Update()
        {
            if (!built || controller == null || Session == null) return;
            var hero = Hero;
            if (levelChoice != null && levelChoice.Mercenary != hero) levelChoice = null;

            portrait.sprite = world != null ? world.Portrait(hero) : null;
            jobBadge.sprite = SoulIconSet.Job(hero.Job);
            jobBadge.enabled = jobBadge.sprite != null;
            portrait.enabled = portrait.sprite != null;
            SoulPortraits.Fit(portrait, hero, -92, 196);
            for (int i = 0; i < switcherPortraits.Count && i < Session.Mercenaries.Count; i++)
            {
                switcherPortraits[i].sprite = world != null ? world.Portrait(Session.Mercenaries[i]) : null;
                SoulPortraits.Fit(switcherPortraits[i], Session.Mercenaries[i], -34, 46);
                switcherPortraits[i].enabled = switcherPortraits[i].sprite != null;
                switcherPortraits[i].transform.parent.parent.GetComponent<Image>().color = i == heroIndex ? SoulUi.TabActive : SoulUi.SlotBg;
            }
            nameText.text = hero.Name + (hero.Alive ? "" : "  " + SoulUi.Colored("쓰러짐", SoulUi.Bad));
            infoText.text = $"{hero.Race.Id} {hero.Job} · {hero.Grade}등급";
            bodyText.text = $"키 {hero.Stats.Height:0.00}m · 몸무게 {hero.Stats.Weight:0.#}kg";
            actionText.text = hero.Alive ? "행동: " + (hero.ShownAction ?? hero.Action) : "";
            bool isLeader = hero == Session.Leader;
            SoulUi.SetLabel(leaderButton, isLeader ? "현재 리더" : "리더 지정");
            leaderButton.interactable = !isLeader && hero.Alive;
            leaderButton.GetComponent<Image>().color = isLeader ? new Color(.55f, .42f, .12f) : SoulUi.TabInactive;

            bool maxed = hero.Level >= SoulMercenary.MaxLevel;
            levelText.text = $"Lv.{hero.Level}";
            SoulUi.SetRatio(expBar, maxed ? 1 : hero.Experience / (float)Mathf.Max(1, hero.ExperienceToNext));
            expText.text = maxed ? "MAX" : $"{hero.Experience}/{hero.ExperienceToNext}";
            float maxHp = hero.Stats.Total(StatType.MaxHp), maxSt = hero.Stats.Total(StatType.MaxStamina), maxMp = hero.Stats.Total(StatType.MaxMp);
            SoulUi.SetHp(hpBar, hero); hpText.text = $"{hero.Hp:0}/{maxHp:0}";
            SoulUi.SetRatio(staminaBar, hero.Stamina / Mathf.Max(1, maxSt)); staminaText.text = $"{hero.Stamina:0}/{maxSt:0}";
            SoulUi.SetRatio(manaBar, hero.Mp / Mathf.Max(1, maxMp)); manaText.text = $"{hero.Mp:0}/{maxMp:0}";

            foreach (var entry in tabs) entry.Value.GetComponent<Image>().color = entry.Key == tab ? SoulUi.TabActive : SoulUi.TabInactive;
            SoulUi.SetLabel(tabs[Tab.Stats], hero.CanPickPattern ? "능력치 " + SoulUi.Colored("●", SoulUi.Accent) : "능력치");
            SoulUi.SetLabel(tabs[Tab.Souls], Session.Stash.Count > 0 ? $"영혼 ({Session.Stash.Count})" : "영혼");

            string next = Signature(hero);
            if (next == signature) return;
            // What the fight changes on its own (statuses, buffs, enchants, wounds) waits while a tooltip is being
            // read — rebuilding would pull the hovered tile out from under the mouse. What the player did (tab,
            // mercenary, lock …) rebuilds at once.
            if (signature != null && SoulTooltip.IsOpen && Structure(next) == Structure(signature)) return;
            signature = next;
            BuildSheetContent(hero);

            // a rebuild caused by the battle (buff, kill) keeps the list where the player left it
            string nextView = $"{tab}|{heroIndex}|{details}|{levelChoice != null}";
            float keep = scroll != null && nextView == view ? scroll.content.anchoredPosition.y : 0;
            view = nextView;
            scroll = null;
            SoulUi.Clear(body);
            if (tab == Tab.Stats)
            {
                if (levelChoice != null) BuildPatternChoice(hero);
                else if (details) BuildDetails(hero);
                else BuildStats(hero);
            }
            else if (tab == Tab.Souls) BuildSouls(hero);
            else BuildCodex(hero);
            if (scroll != null) scroll.content.anchoredPosition = new Vector2(0, keep);
        }

        // Everything after this mark in a signature is what the battle changes by itself.
        const string LiveMark = "|~";
        static string Structure(string signature) { int cut = signature.IndexOf(LiveMark, System.StringComparison.Ordinal); return cut < 0 ? signature : signature.Substring(0, cut); }

        // Experience is left out on purpose: it changes every kill and only moves the EXP bar (per frame).
        string Signature(SoulMercenary hero)
        {
            var sb = new StringBuilder();
            sb.Append(tab).Append('|').Append(heroIndex).Append('|').Append(hero.Level);
            foreach (var soul in hero.Souls) sb.Append('|').Append(soul.Id).Append(soul.Grade);
            sb
              .Append('|').Append(hero.Equipment.Count).Append('|').Append(Session.Inventory.Count)
              .Append('|').Append(hero.PatternPicks).Append('|').Append(hero.Alive).Append('|').Append(Session.PreservationItems).Append('|').Append(hero.Codex.Count)
              .Append('|').Append(selectedDrop).Append('|').Append(levelChoice != null).Append('|').Append(selectedPattern).Append('|').Append(details)
              .Append('|').Append(hero.LockVersion).Append(LiveMark).Append(hero.ImbueRemaining > 0).Append('|').Append(hero.Wounds);
            foreach (var status in hero.Statuses) sb.Append('|').Append((int)status.Kind).Append(status.Grade);
            foreach (var buff in hero.TimedBuffs) sb.Append('|').Append(buff.Id);
            foreach (var drop in Session.Stash) sb.Append('|').Append(drop.Soul.Id).Append(drop.Preserved);
            return sb.ToString();
        }

        // ── character sheet (dynamic parts) ──────────────────────

        void BuildSheetContent(SoulMercenary hero)
        {
            SoulUi.Clear(buffRow);
            if (hero.Wounds > 0)
            {
                var chip = SoulUi.Chip(buffRow, SoulIconSet.Status(SoulStatus.Bleed), $"부상 {hero.Wounds}단계", SoulUi.Bad, 34);
                SoulTooltip.Attach(chip, SoulIconSet.Status(SoulStatus.Bleed), $"부상 {hero.Wounds}/{SoulMercenary.MaxWounds}단계", () => new List<SoulLine>
                {
                    new SoulLine(SoulIconSet.Stat(StatType.MaxStamina), $"스태미나·MP 소모 +{SoulCombat.WoundCostStep * 100 * hero.Wounds:0}%"),
                    new SoulLine(SoulIconSet.Ui("lock"), $"패턴 실패 확률 {SoulCombat.WoundFailChance(hero) * 100:0}% ({SoulCombat.WoundFailFrom}단계부터)"),
                    new SoulLine(null, $"받은 피해가 최대 HP의 {SoulMercenary.WoundThreshold * 100:0}%만큼 쌓일 때마다 부상이 1단계 늘어납니다. 치유 마법·회복 포션·야영·성당에서만 낫습니다."),
                    new SoulLine(null, $"{SoulMercenary.NoRegenWounds}단계부터 HP가 저절로 회복되지 않고, {SoulMercenary.BleedingWounds}단계는 HP가 계속 줄어듭니다."),
                });
            }
            foreach (var status in hero.Statuses)
            {
                var state = status;
                string name = SoulCombat.StatusName(state.Kind);
                var chip = SoulUi.Chip(buffRow, SoulIconSet.Status(state.Kind), $"{name} {state.Grade}등급", SoulUi.Bad, 34);
                SoulTooltip.Attach(chip, () => SoulIconSet.Status(state.Kind), () => $"{name} {state.Grade}등급 · {state.Remaining:0.0}초 남음", () => SoulDescribe.StatusLines(state));
            }
            if (hero.ImbueRemaining > 0 && hero.Imbue != null)
            {
                var chip = SoulUi.Chip(buffRow, SoulIconSet.Status(hero.Imbue.Kind), hero.ImbueName, SoulUi.Good, 34);
                SoulTooltip.Attach(chip, () => SoulIconSet.Status(hero.Imbue.Kind), () => $"{hero.ImbueName} · {hero.ImbueRemaining:0.0}초 남음",
                    () => new[] { new SoulLine(null, "공격에 상태이상이 묻어 있습니다."), SoulDescribe.StatusLine(hero.Imbue, hero.Stats.Combat) });
            }
            foreach (var buff in hero.TimedBuffs)
            {
                if (buff.Id == SoulCombat.StatusBuffId) continue;
                var current = buff;
                var parts = new List<string>();
                foreach (var bonus in buff.Bonuses) parts.Add($"{SoulStatRules.CombatName(bonus.Stat)} {SoulDescribe.Signed(bonus.Stat, bonus.Value)}");
                bool debuff = SoulDescribe.IsDebuff(buff);
                var chip = SoulUi.Chip(buffRow, SoulDescribe.BuffIcon(buff), string.Join(", ", parts), debuff ? SoulUi.Bad : SoulUi.Good, 34);
                SoulTooltip.Attach(chip, () => SoulDescribe.BuffIcon(current), () => $"{(debuff ? "약화" : "버프")} · {current.Remaining:0.0}초 남음", () => BuffLines(current));
            }

            // equipment: fixed slots so the sheet keeps its shape; what the party carries unequipped is listed
            // six fixed slots (main, off hand, body, head, two accessories), framed in the item's grade colour
            SoulUi.Clear(equipmentRow);
            var slots = new List<(SoulEquipSlot slot, SoulItem item)>();
            foreach (SoulEquipSlot kind in System.Enum.GetValues(typeof(SoulEquipSlot)))
            {
                var worn = hero.Equipment.FindAll(e => e.Slot == kind);
                int count = kind == SoulEquipSlot.Accessory ? SoulCampaign.AccessorySlots : 1;
                for (int i = 0; i < count; i++) slots.Add((kind, i < worn.Count ? worn[i] : null));
            }
            foreach (var (kind, item) in slots)
            {
                bool filled = item != null;
                var slot = SoulUi.Framed("Equip", equipmentRow, filled ? SoulUi.TabInactive : new Color(.07f, .07f, .11f), filled ? item.GradeColor : new Color(.2f, .2f, .26f), 2);
                SoulUi.Layout(slot.gameObject, 58, 80);
                var icon = SoulUi.Icon(slot, filled ? SoulDescribe.ItemIcon(item) : SoulIconSet.Ui("equipment"), 32);
                icon.color = new Color(1, 1, 1, filled ? 1 : .25f);
                SoulUi.Place(icon.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-16, -40), new Vector2(16, -8));
                var label = SoulUi.Text(slot, SoulItemRules.SlotName(kind), 13, filled ? Color.white : new Color(.45f, .47f, .58f), TextAlignmentOptions.Center);
                label.overflowMode = TextOverflowModes.Ellipsis;
                SoulUi.Place(label.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(2, 4), new Vector2(-2, 36));
                if (!filled) continue;
                var shown = item;
                SoulTooltip.Attach(slot, SoulIconSet.Ui("equipment"), shown.Name, () => SoulDescribe.EquipmentLines(shown, Rules, hero));
            }
            float load = hero.Stats.Total(StatType.LoadRatio);
            equipmentHint.text = $"{hero.GearWeight:0.#}/{SoulItemRules.CarryLimit(hero.Stats.Total(StatType.Strength)):0}kg {SoulItemRules.LoadName(load)}"
                + (Session.Inventory.Count > 0 ? $" · 전리품 {Session.Inventory.Count}개" : "");

            BuildSlots(hero);

            Session.Kills.TryGetValue(hero, out int kills);
            hero.MentalStats.TryGetValue(StatType.Will, out float mental);
            memoryText.text = $"이번 던전 처치 {kills} · 도감 {hero.Codex.Count}종 · 회복 포션 {Session.Potions}\n경험으로 얻은 정신력 {mental:0}/{SoulDungeonSession.MaxMentalWill:0}"
                + $"\n피로 {hero.Fatigue:0} ({SoulFatigue.Name(hero.Fatigue)}) — {SoulFatigue.Effect(SoulFatigue.Tier(hero.Fatigue))}";
        }

        List<SoulLine> BuffLines(SoulTimedBuff buff)
        {
            var lines = new List<SoulLine>();
            foreach (var bonus in buff.Bonuses)
                lines.Add(new SoulLine(SoulIconSet.Stat(bonus.Stat), $"{SoulStatRules.CombatName(bonus.Stat)} {SoulUi.Colored(SoulDescribe.Signed(bonus.Stat, bonus.Value), SoulUi.Good)}"));
            return lines;
        }

        List<SoulLine> LevelLines(SoulMercenary hero)
        {
            var lines = new List<SoulLine>
            {
                new SoulLine(null, "경험치가 차면 자동으로 레벨이 오르고, 스탯이 종족·직업 성향에 따라 자동으로 분배됩니다."),
                new SoulLine(SoulIconSet.Ui("level"), hero.Level >= SoulMercenary.MaxLevel ? "최대 레벨" : $"다음 레벨까지 {hero.ExperienceToNext - hero.Experience}"),
            };
            int next = (hero.Level / SoulMercenary.MilestoneLevels + 1) * SoulMercenary.MilestoneLevels;
            if (next <= SoulMercenary.MaxLevel) lines.Add(new SoulLine(SoulIconSet.Ui("soul"), $"레벨 {next}: 새 패턴 1개 + 영혼 슬롯 1칸"));
            if (hero.CanPickPattern) lines.Add(new SoulLine(SoulIconSet.Ui("level"), SoulUi.Colored($"배울 수 있는 패턴 {hero.PatternPicks}개 — 능력치 탭에서 선택", SoulUi.Accent)));
            if (hero.LevelStats.Count > 0)
            {
                lines.Add(SoulLine.Title("레벨로 얻은 스탯"));
                var parts = new List<string>();
                foreach (var entry in hero.LevelStats) parts.Add($"{SoulDescribe.Colored(Rules, entry.Key)} +{entry.Value:0}");
                lines.Add(new SoulLine(null, string.Join("  ", parts)));
            }
            return lines;
        }

        // Soul slots: absorbed souls show their monster, open slots glow, locked slots (above level) stay dark.
        void BuildSlots(SoulMercenary hero)
        {
            SoulUi.Clear(soulSlots);
            for (int i = 0; i < 1 + SoulMercenary.MaxLevel / SoulMercenary.MilestoneLevels; i++)
            {
                bool absorbed = i < hero.Souls.Count, open = i < hero.SoulSlots;
                var slot = SoulUi.Panel("Slot", soulSlots, absorbed ? new Color(.25f, .16f, .38f) : open ? new Color(.16f, .14f, .26f) : new Color(.06f, .06f, .09f), true);
                SoulUi.Layout(slot.gameObject, 56, 56);
                if (absorbed)
                {
                    var soul = hero.Souls[i];
                    SoulKit.MonsterPicture((RectTransform)slot.transform, soul.Source, 56);
                    SoulTooltip.Attach(slot, SoulIconSet.Ui("soul"), soul.OriginMonster + "의 영혼", () => SoulLines(soul, hero));
                }
                else
                {
                    var icon = SoulUi.Icon(slot.transform, SoulIconSet.Ui(open ? "soul" : "lock"), 28);
                    icon.color = new Color(1, 1, 1, open ? .35f : .5f);
                    icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f, .5f);
                    int level = i * SoulMercenary.MilestoneLevels;
                    SoulTooltip.Attach(slot, SoulIconSet.Ui(open ? "soul" : "lock"), open ? "빈 영혼 슬롯" : "잠긴 영혼 슬롯",
                        () => new[] { new SoulLine(null, open ? "영혼 탭에서 영혼을 흡수할 수 있습니다." : $"레벨 {level}에 열립니다.") });
                }
            }
        }

        // ── tab: stats ───────────────────────────────────────────

        RectTransform Column(float left, float width)
        {
            var column = SoulUi.Rect("Column", body);
            SoulUi.Place(column, Vector2.zero, width > 0 ? new Vector2(0, 1) : Vector2.one, new Vector2(left, 0), new Vector2(width > 0 ? left + width : 0, 0));
            return column;
        }

        // A header strip at the top of a column and a scroll list filling the rest of it.
        RectTransform ScrollBelow(RectTransform column, float header)
        {
            var list = SoulUi.Scroll(column, out scroll);
            SoulUi.Place((RectTransform)scroll.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -header));
            return list;
        }

        RectTransform Header(RectTransform column, float height)
        {
            var header = SoulUi.Row(column, height, 10);
            SoulUi.Place(header, new Vector2(0, 1), Vector2.one, new Vector2(0, -height), Vector2.zero);
            return header;
        }

        void BuildStats(SoulMercenary hero)
        {
            // left: the six main stats, then the detail stats that matter most in a fight
            var left = Column(0, 440);
            SoulUi.Vertical(left.gameObject, 10);
            var head = SoulUi.Row(left, 44, 10);
            HeaderText(head, "기본 스탯", null);
            var more = SoulUi.Button(head, "상세 스탯", SoulUi.TabInactive, () => { details = true; signature = null; }, 20, SoulIconSet.Ui("expand"));
            SoulUi.Layout(more.gameObject, 150, 44);
            var grid = SoulUi.Flow(left, new Vector2(214, 84), 12);
            foreach (var stat in MainStats) StatTile(grid, hero, stat);

            Section(left, "주요 상세 스탯", null);
            var keys = SoulUi.Flow(left, new Vector2(214, 42), 8);
            foreach (var stat in KeyDetailStats) StatRow(keys, hero, stat);

            // right: everything the mercenary can do; hovering explains each one in full
            var right = Column(460, 0);
            var top = Header(right, 48);
            HeaderText(top, "기술", null);
            if (hero.CanPickPattern)
            {
                var pick = SoulUi.Button(top, $"새 패턴 배우기 ({hero.PatternPicks})", SoulUi.Accent, () =>
                {
                    levelChoice = Session.OpenLevel(hero);
                    selectedPattern = 0;
                }, 20, SoulIconSet.Ui("level"));
                SoulUi.Layout(pick.gameObject, 250, 44);
            }
            var list = ScrollBelow(right, 56);

            var patterns = hero.OwnedPatterns();
            var usable = hero.Patterns();
            Section(list, $"행동 패턴 {patterns.Count}", null);
            var patternGrid = SoulUi.Flow(list, new Vector2(84, 84), 10);
            foreach (var pattern in patterns)
            {
                string blocked = hero.BlockReason(pattern);
                IconTile(patternGrid, hero, "Pattern", SoulDescribe.PatternIcon(pattern), SoulDescribe.PatternTitle(pattern), blocked != null, () =>
                {
                    var shown = PatternLines(hero, pattern);
                    if (blocked != null) shown.Insert(0, new SoulLine(SoulIconSet.Ui("lock"), SoulUi.Colored(blocked, SoulUi.Bad)));
                    return shown;
                }, pattern);
            }

            var actives = hero.AllActiveSkills();
            Section(list, $"액티브 스킬 {actives.Count}", actives.Count == 0 ? "없음" : null);
            var activeGrid = SoulUi.Flow(list, new Vector2(84, 84), 10);
            foreach (var skill in actives)
            {
                bool ready = (skill.RequiredPattern == null || usable.Contains(skill.RequiredPattern)) && SoulSkillUsePolicy.Blocked(hero, skill) == null;
                IconTile(activeGrid, hero, "Ability", skill.Icon != null ? skill.Icon : SoulIconSet.Ui("skill"), SoulDescribe.SkillTitle(hero, skill), !ready, () => SoulDescribe.SkillLines(hero, skill, Rules), skill);
            }

            var passives = hero.Passives();
            Section(list, $"패시브 {passives.Count}", passives.Count == 0 ? "없음" : null);
            var passiveGrid = SoulUi.Flow(list, new Vector2(84, 84), 10);
            foreach (var passive in passives)
                IconTile(passiveGrid, hero, "Ability", passive.Icon != null ? passive.Icon : SoulIconSet.Ui("passive"), passive.SkillName, false, () => SoulDescribe.PassiveLines(hero, passive, Rules));
        }

        // Icon only; name and full description come up on hover (mouse) or tap (touch). Unusable ones are dimmed.
        // Patterns and actives carry a lock in the corner: locked, the mercenary keeps them but never uses them.
        void IconTile(Transform parent, SoulMercenary hero, string name, Sprite icon, string title, bool dim, System.Func<IEnumerable<SoulLine>> lines, Object lockable = null)
        {
            bool locked = hero.IsLocked(lockable);
            var tile = SoulUi.Panel(name, parent, SoulUi.SlotBg, true);
            var image = SoulUi.Icon(tile.transform, icon, 10);
            SoulUi.Stretch(image.rectTransform, 8);
            if (dim || locked) image.color = new Color(1, 1, 1, .3f);
            SoulTooltip.Attach(tile, icon, title, () =>
            {
                var shown = new List<SoulLine>(lines());
                if (locked) shown.Insert(0, new SoulLine(SoulIconSet.Ui("lock"), SoulUi.Colored("잠김 — 전투에서 사용하지 않습니다", SoulUi.Bad)));
                return shown;
            });
            if (lockable == null) return;
            var button = SoulUi.Panel("Lock", tile.transform, locked ? SoulUi.Bad : new Color(0, 0, 0, .55f), true);
            SoulUi.Place(button.rectTransform, Vector2.one, Vector2.one, new Vector2(-32, -32), new Vector2(-2, -2));
            var mark = SoulUi.Icon(button.transform, SoulIconSet.Ui("lock"), 10);
            SoulUi.Stretch(mark.rectTransform, 3);
            mark.color = locked ? Color.white : new Color(1, 1, 1, .45f);
            SoulUi.Clickable(button.gameObject).onClick.AddListener(() =>
            {
                hero.ToggleLock(lockable);
                signature = null;
                SoulTooltip.Hide();
            });
        }

        static void HeaderText(Transform row, string title, string hint)
        {
            var label = SoulUi.Text(row, title, 26, SoulUi.SectionLbl, TextAlignmentOptions.MidlineLeft);
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var note = SoulUi.Text(row, hint ?? "", 17, new Color(.55f, .58f, .7f), TextAlignmentOptions.MidlineLeft);
            note.textWrappingMode = TextWrappingModes.NoWrap;
            SoulUi.Layout(note.gameObject, -1, -1, 1);
        }

        void StatTile(Transform parent, SoulMercenary hero, StatType stat)
        {
            var tile = SoulUi.Panel("Stat", parent, SoulUi.SlotBg, true);
            var icon = SoulUi.Icon(tile.transform, SoulIconSet.Stat(stat), 56);
            SoulUi.Place(icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(12, -28), new Vector2(68, 28));
            var label = SoulUi.Text(tile.transform, SoulDescribe.StatName(Rules, stat), 20, SoulDescribe.StatColor(Rules, stat), TextAlignmentOptions.BottomLeft);
            SoulUi.Place(label.rectTransform, new Vector2(0, .5f), Vector2.one, new Vector2(80, 0), new Vector2(-8, -8));
            var number = SoulUi.Text(tile.transform, $"{hero.Stats.Total(stat):0.#}", 32, Color.white, TextAlignmentOptions.TopLeft);
            number.fontStyle = FontStyles.Bold;
            SoulUi.Place(number.rectTransform, Vector2.zero, new Vector2(1, .5f), new Vector2(80, 4), new Vector2(-8, 0));
            AttachStat(tile, stat);
        }

        // icon · name · value in one row, tooltip with the calculation
        void StatRow(Transform parent, SoulMercenary hero, StatType stat)
        {
            float amount = SoulDescribe.CombatValue(hero, stat);
            var row = SoulUi.Panel("Row", parent, SoulUi.SlotBg, true);
            var layout = SoulUi.Horizontal(row.gameObject, 8);
            layout.padding = new RectOffset(6, 12, 3, 3);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandHeight = false;
            SoulUi.Icon(row.transform, StatIcon(stat), 28);
            var name = SoulUi.Text(row.transform, SoulDescribe.StatName(Rules, stat), 18, HeroStatPipeline.IsUpper(stat) ? SoulDescribe.StatColor(Rules, stat) : SoulUi.SubText);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            SoulUi.Layout(name.gameObject, -1, -1, 1);
            var value = SoulUi.Text(row.transform, SoulDescribe.Value(stat, amount), 20, Mathf.Approximately(amount, 0) ? new Color(.45f, .47f, .58f) : Color.white, TextAlignmentOptions.MidlineRight);
            value.fontStyle = FontStyles.Bold;
            value.textWrappingMode = TextWrappingModes.NoWrap;
            AttachStat(row, stat);
        }

        // Not every detail stat has its own icon; borrow the closest one so rows stay aligned and readable.
        static Sprite StatIcon(StatType stat)
        {
            var icon = SoulIconSet.Stat(stat);
            if (icon != null) return icon;
            switch (stat)
            {
                case StatType.HpRegen: return SoulIconSet.Stat(StatType.MaxHp);
                case StatType.StaminaRegen: return SoulIconSet.Stat(StatType.MaxStamina);
                case StatType.MpRegen: return SoulIconSet.Stat(StatType.MaxMp);
                case StatType.BleedResist: return SoulIconSet.Status(SoulStatus.Bleed);
                case StatType.PoisonResist: return SoulIconSet.Status(SoulStatus.Poison);
                case StatType.BurnResist: return SoulIconSet.Status(SoulStatus.Burn);
                case StatType.ChillResist: return SoulIconSet.Status(SoulStatus.Chill);
                case StatType.FearResist: return SoulIconSet.Status(SoulStatus.Fear);
                case StatType.StunResist: return SoulIconSet.Status(SoulStatus.Stun);
                case StatType.PetrifyResist: return SoulIconSet.Status(SoulStatus.Petrify);
                default: return SoulIconSet.Ui("base");
            }
        }

        // ── stats: details ───────────────────────────────────────

        // Every stat on one screen, grouped by what it is for — no scrolling, four columns.
        void BuildDetails(SoulMercenary hero)
        {
            var whole = Column(0, 0);
            var top = Header(whole, 48);
            var back = SoulUi.Button(top, "돌아가기", SoulUi.TabInactive, () => { details = false; signature = null; }, 20, SoulIconSet.Ui("fold"));
            SoulUi.Layout(back.gameObject, 160, 44);
            HeaderText(top, "상세 스탯", null);

            float width = (body.rect.width - 3 * Gap) / 4;
            if (width <= 0) width = (Width - SideWidth - 44 - 3 * Gap) / 4; // before the first layout pass
            var columns = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                columns[i] = SoulUi.Rect("Group", whole);
                SoulUi.Place(columns[i], new Vector2(0, 0), new Vector2(0, 1), new Vector2(i * (width + Gap), 0), new Vector2(i * (width + Gap) + width, -60));
                SoulUi.Vertical(columns[i].gameObject, 5);
            }
            Group(columns[0], hero, "기본 스탯", MainStats);
            Group(columns[0], hero, "기타 상위 스탯", OtherUpperStats);
            Group(columns[1], hero, "자원", ResourceStats);
            Group(columns[1], hero, "이동 · 신체", BodyStats);
            Group(columns[2], hero, "공격", OffenseStats);
            Group(columns[2], hero, "방어", DefenseStats);
            Group(columns[3], hero, "상태이상 내성", ResistStats);
            // everything else it has (traits, passives, gear options, elements): only what is not zero
            var special = new List<StatType>();
            foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
            {
                if ((int)stat < (int)StatType.MaxMp || stat == StatType.LoadRatio || Listed(stat)) continue;
                if (stat == StatType.StaffBound && SoulSkillUsePolicy.Focus(hero) >= 1) continue; // holding a staff: no cut
                if (Mathf.Abs(SoulDescribe.CombatValue(hero, stat)) > .0005f) special.Add(stat);
            }
            if (special.Count > 0) Group(columns[3], hero, "특수 효과", special.ToArray());

            var growth = columns[1];
            GroupTitle(growth, "성장 성향");
            var tendency = new Dictionary<StatType, float>();
            foreach (var bonus in hero.Race.GrowthWeights) tendency[bonus.Stat] = (tendency.TryGetValue(bonus.Stat, out float a) ? a : 0) + bonus.Value;
            foreach (var bonus in hero.GrowthWeights) tendency[bonus.Stat] = (tendency.TryGetValue(bonus.Stat, out float b) ? b : 0) + bonus.Value;
            if (tendency.Count == 0) Line(growth, SoulIconSet.Ui("race"), "고른 성장", SoulUi.SubText);
            // 비성향 (the job's): never grows — unless its own tendency asks (then it shows above instead)
            foreach (var stat in hero.WeakGrowth) if (!System.Array.Exists(hero.GrowthWeights, g => g.Stat == stat && g.Value > 0)) tendency.Remove(stat);
            foreach (var entry in tendency)
                Line(growth, SoulIconSet.Stat(entry.Key), $"{SoulDescribe.Colored(Rules, entry.Key)}  {SoulUi.Colored(new string('▲', Mathf.Clamp(Mathf.RoundToInt(entry.Value), 1, 4)), SoulUi.Good)}", Color.white);
            foreach (var stat in hero.WeakGrowth)
                if (!tendency.ContainsKey(stat))
                    Line(growth, SoulIconSet.Stat(stat), $"{SoulDescribe.Colored(Rules, stat)}  {SoulUi.Colored("▼ 비성향", SoulUi.Bad)}", Color.white);
        }

        static bool Listed(StatType stat)
            => System.Array.IndexOf(ResourceStats, stat) >= 0 || System.Array.IndexOf(OffenseStats, stat) >= 0 || System.Array.IndexOf(DefenseStats, stat) >= 0
            || System.Array.IndexOf(BodyStats, stat) >= 0 || System.Array.IndexOf(ResistStats, stat) >= 0;

        void Group(Transform column, SoulMercenary hero, string title, StatType[] stats)
        {
            GroupTitle(column, title);
            foreach (var stat in stats)
            {
                StatRow(column, hero, stat);
                SoulUi.Layout(column.GetChild(column.childCount - 1).gameObject, -1, 32);
            }
        }

        static void GroupTitle(Transform column, string title)
        {
            var label = SoulUi.Text(column, title, 21, SoulUi.Accent, TextAlignmentOptions.BottomLeft);
            label.fontStyle = FontStyles.Bold;
            SoulUi.Layout(label.gameObject, -1, 34);
        }

        // ── stats: skills ────────────────────────────────────────

        List<SoulLine> PatternLines(SoulMercenary hero, SoulPatternData pattern) => PatternTooltip(hero, pattern, Rules);

        // A pattern's tooltip for a mercenary (the numbers come from its stats). Shared with the village.
        public static List<SoulLine> PatternTooltip(SoulMercenary hero, SoulPatternData pattern, SoulStatRules rules)
        {
            var stats = hero.Stats.Combat;
            // the price at the title's right end: stamina always, MP when it takes some
            SoulCombat.PatternCost(hero, pattern, out float stamina, out float mana);
            var lines = new List<SoulLine> { SoulLine.Price(SoulIconSet.Stat(StatType.MaxStamina), $"{stamina:0.#}") };
            if (mana > 0) lines.Add(SoulLine.Price(SoulIconSet.Stat(StatType.MaxMp), $"{mana:0.#}"));
            lines.Add(SoulLine.Title("효과"));
            if (pattern.Category == SoulPatternCategory.Attack && !pattern.CastsSkills)
            {
                lines.Add(new SoulLine(SoulIconSet.Stat(StatType.Attack), $"{SoulDescribe.School(pattern.DamageSchool)}·{SoulDescribe.Kind(pattern.DamageKind)} 피해 {SoulDescribe.Formula(pattern.Damage, rules, stats)}"));
                if (pattern.Pierce.Terms.Length > 0 || pattern.Pierce.Flat > 0)
                    lines.Add(new SoulLine(SoulIconSet.Ui("pierce"), $"방어 관통 {SoulDescribe.Formula(pattern.Pierce, rules, stats)}"));
                lines.Add(new SoulLine(SoulIconSet.Ui("range"), $"사거리 {pattern.Range:0.#}칸"));
            }
            if (pattern.Category == SoulPatternCategory.Attack)
                lines.Add(new SoulLine(SoulIconSet.Category(SoulPatternCategory.Attack), AreaText(pattern)));
            if (pattern.CastsSkills) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.MaxMp), "쓸 수 있는 공격 마법 중 가장 강한 것을 MP로 시전합니다. MP가 부족하면 거리를 두고 회복을 기다립니다."));
            if (pattern.Category == SoulPatternCategory.Defense)
            {
                string mode = pattern.DefenseMode == SoulDefenseMode.Guard ? $"피해를 {pattern.GuardMultiplier * 100:0}%로 줄입니다."
                    : pattern.DefenseMode == SoulDefenseMode.Dodge ? $"공격을 읽어내면 {pattern.RollDistance:0.#}칸 옆으로 비켜섭니다. 확률이 아니라, 범위 밖으로 나가면 완전히 피합니다. {ReactionText(hero)}"
                    : pattern.DefenseMode == SoulDefenseMode.Roll ? $"공격을 읽어내면 {pattern.RollDistance:0.#}칸 굴러 빠져나갑니다 (회피보다 먼저 시도). 확률이 아니라, 범위 밖으로 나가면 완전히 피합니다. {ReactionText(hero)}"
                    : pattern.DefenseMode == SoulDefenseMode.Followup ? $"회피·구르기·거리 벌리기로 공격을 피하면 {pattern.FollowupWindow:0.#}초 안의 다음 공격이 곧바로 나갑니다. 그 공격은 스태미나를 쓰지 않고 피해 ×{pattern.FollowupMultiplier:0.##}."
                    : pattern.DefenseMode == SoulDefenseMode.Counter ? $"근접 공격을 {pattern.GuardMultiplier * 100:0}%로 받고 {pattern.Damage.Evaluate(stats):0} 피해로 받아칩니다."
                    : "뒤쪽 아군을 노린 원거리 공격을 더 자주 대신 맞습니다 (가림 확률 ×1.6).";
                lines.Add(new SoulLine(SoulIconSet.Category(SoulPatternCategory.Defense), mode));
            }
            if (pattern.Category == SoulPatternCategory.Movement)
                lines.Add(new SoulLine(SoulIconSet.Category(SoulPatternCategory.Movement), MoveStyleText(pattern.MoveStyle)));
            if (pattern.FreeRetreat) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.MaxStamina), "공격 직후 물러나는 걸음은 스태미나를 쓰지 않습니다."));
            if (pattern.Category == SoulPatternCategory.Chain) lines.Add(new SoulLine(SoulDescribe.PatternIcon(pattern), ChainText(pattern, stats, rules)));
            if (pattern.Category == SoulPatternCategory.Encounter)
                lines.Add(new SoulLine(SoulIconSet.Category(SoulPatternCategory.Encounter), "적을 처음 발견했을 때 이 패턴을 요구하는 스킬이 발동할 수 있습니다."));
            if (pattern.Ambush)
                lines.Add(new SoulLine(SoulIconSet.Ui("leap"), $"{pattern.Range:0.#}칸 안의 대상 등 뒤로 순간이동해 공격 · 배후 피해 ×{pattern.BackstabMultiplier:0.##}"));
            if (pattern.Category == SoulPatternCategory.Support) SupportLines(lines, hero, pattern);
            else if (pattern.Status != null && pattern.Status.Kind != SoulStatus.None) lines.Add(SoulDescribe.StatusLine(pattern.Status, stats));
            if (pattern.Knockback > 0) lines.Add(new SoulLine(SoulIconSet.Ui("knockback"), $"넉백 {pattern.Knockback:0.#}칸 (몸무게 비율 적용)"));
            if (pattern.AreaRadius > 0) lines.Add(new SoulLine(SoulIconSet.Ui("range"), $"주변 {pattern.AreaRadius:0.#}칸 안의 다른 적도 75% 피해"));

            var users = new List<string>();
            foreach (var skill in hero.ActiveSkills()) if (skill.RequiredPattern == pattern) users.Add(skill.SkillName);
            if (users.Count > 0) lines.Add(new SoulLine(SoulIconSet.Ui("skill"), "이 패턴을 쓰는 스킬: " + string.Join(", ", users)));

            lines.Add(SoulLine.Title("발동"));
            lines.Add(new SoulLine(SoulIconSet.Stat(StatType.ActionSpeed), $"행동 {pattern.ActionTime:0.##}초"));
            if (pattern.Cooldown > 0) lines.Add(new SoulLine(SoulIconSet.Ui("cooldown"), $"재사용 대기 {pattern.Cooldown:0.#}초"));
            return lines;
        }

        static void SupportLines(List<SoulLine> lines, SoulMercenary hero, SoulPatternData pattern)
        {
            var stats = hero.Stats.Combat;
            string who = pattern.SupportTarget == SoulSupportTarget.Self ? "자신"
                : pattern.SupportTarget == SoulSupportTarget.Allies ? (pattern.Heals ? $"{pattern.SupportRadius:0.#}칸 안에서 가장 다친 아군" : $"{pattern.SupportRadius:0.#}칸 안의 아군")
                : pattern.Range > 0 ? "대상" : $"{pattern.SupportRadius:0.#}칸 안의 적";
            string when = pattern.UseWhen == SoulUseWhen.InCombat ? "전투 중" : pattern.UseWhen == SoulUseWhen.OutOfCombat ? "전투가 없을 때" : "언제든";
            lines.Add(new SoulLine(SoulIconSet.Ui("trigger"), $"{when} 사용 · 대상: {who}"));
            if (pattern.HealRatio > 0) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.MaxHp), $"최대 HP의 {pattern.HealRatio * 100:0.#}% 회복" + (pattern.UseBelow < .95f ? $" · HP {pattern.UseBelow * 100:0}% 아래일 때" : "")));
            if (pattern.Heal != null && pattern.Heals && pattern.HealRatio <= 0)
                lines.Add(new SoulLine(SoulIconSet.Stat(StatType.MaxHp), $"HP 회복 {SoulDescribe.Formula(pattern.Heal, hero.Stats != null ? SoulCampaign.Current?.Rules : null, stats)}" + (pattern.UseBelow < .95f ? $" · HP {pattern.UseBelow * 100:0}% 아래일 때" : "")));
            if (pattern.SpellFocus)
                lines.Add(new SoulLine(SoulIconSet.Stat(StatType.SpellPower), $"공격 주문을 쓸 수 없을 때(MP 부족·재사용 대기) 집중 1 — 최대 {SoulDungeonSession.MaxFocus} · 다음 공격 주문이 모두 써서 집중 1당 위력 +{SoulDungeonSession.FocusPerStack * 100:0}%"));
            if (pattern.MaxStacks > 1)
                lines.Add(new SoulLine(SoulIconSet.Ui("buff"), $"쓸 때마다 한 번 더 겹칩니다 (최대 {pattern.MaxStacks}중첩) · 시간 갱신"));
            if (pattern.StaminaRatio > 0) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.MaxStamina), $"최대 스태미나의 {pattern.StaminaRatio * 100:0.#}% 회복" + (pattern.UseBelow < .95f ? $" · 스태미나 {pattern.UseBelow * 100:0}% 아래일 때" : "")));
            if (pattern.ManaRatio > 0) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.MaxMp), $"최대 MP의 {pattern.ManaRatio * 100:0.#}% 회복" + (pattern.UseBelow < .95f ? $" · MP {pattern.UseBelow * 100:0}% 아래일 때" : "")));
            foreach (var buff in pattern.Buffs)
                lines.Add(new SoulLine(SoulIconSet.Stat(buff.Stat), $"{SoulStatRules.CombatName(buff.Stat)} {SoulUi.Colored(SoulDescribe.Signed(buff.Stat, buff.Amount.Evaluate(stats)), SoulUi.Good)}"
                    + $" · {pattern.BuffDuration.Evaluate(stats):0.#}초"));
            if (pattern.Imbue != null && pattern.Imbue.Kind != SoulStatus.None)
            {
                lines.Add(new SoulLine(SoulIconSet.Ui("buff"), $"{pattern.ImbueDuration.Evaluate(stats):0.#}초 동안 공격에 묻어 나감"));
                lines.Add(SoulDescribe.StatusLine(pattern.Imbue, stats));
            }
            if (pattern.SupportTarget == SoulSupportTarget.Enemies && pattern.Status != null && pattern.Status.Kind != SoulStatus.None)
                lines.Add(SoulDescribe.StatusLine(pattern.Status, stats));
        }

        static string ChainText(SoulPatternData chain, UnitStat stats, SoulStatRules rules)
        {
            string follow = $"{chain.FollowupWindow:0.#}초 안의 다음 공격이 " + (chain.FollowupInstant ? "곧바로 나가고 " : "")
                + "스태미나를 쓰지 않음" + (chain.FollowupMultiplier > 1 ? $" · 피해 ×{chain.FollowupMultiplier:0.##}" : "")
                + (chain.FollowupArmorIgnore > 0 ? $" · 방어력 {chain.FollowupArmorIgnore * 100:0}% 무시" : "");
            switch (chain.ChainTrigger)
            {
                case SoulChainTrigger.Evade: return "회피·구르기·거리 벌리기로 피하면 " + follow;
                case SoulChainTrigger.Guard: return "방패 막기로 막으면 " + follow;
                case SoulChainTrigger.Combo: return $"같은 대상을 {chain.ComboHits}번 맞히면 " + follow;
                default: return $"구르기가 끝날 때 주변 {chain.StrikeRadius:0.#}칸 안의 적을 벱니다 · 피해 {SoulDescribe.Formula(chain.Damage, rules, stats)}";
            }
        }

        // Every attack is a telegraphed area now, so what that area looks like is the first thing to know
        // about a pattern — it is what decides how many bodies it catches and how easily it is stepped out of.
        static string AreaText(SoulPatternData pattern)
        {
            pattern.Area(out var shape, out var anchor, out float size, out float width);
            string where = anchor == SoulAreaAnchor.Self ? "자신 주위" : "전방";
            string area = shape == SoulAreaShape.Circle ? $"{where} 반경 {size:0.#}칸 원"
                : shape == SoulAreaShape.Cone ? $"{where} {width:0}° 부채꼴 {size:0.#}칸"
                : $"{where} 폭 {width:0.#}칸 · 길이 {size:0.#}칸 직선";
            string tell = $"예고 {pattern.WindUp(pattern.ActionTime):0.0#}초 뒤 범위 안 전원 타격";
            return area + " · " + tell + (pattern.Hits > 1 ? $" · {pattern.Hits}회 연타 (회당 60%)" : "");
        }

        // Agility is the gate on evasion: an attack can only be evaded if it is read inside its wind-up.
        static string ReactionText(SoulMercenary hero)
            => $"민첩 {hero.Stats.Total(StatType.Agility):0} → 반응 {SoulDungeonSession.ReactionTime(hero):0.0#}초: 예고가 이보다 긴 공격만 피할 수 있습니다.";

        static string MoveStyleText(SoulMoveStyle style)
        {
            switch (style)
            {
                case SoulMoveStyle.KeepDistance: return "공격 사거리 안에서 거리를 두고, 너무 가까우면 물러납니다. 읽어낸 공격 범위에서 한 걸음 빠져나가는 회피로도 씁니다.";
                case SoulMoveStyle.Flank: return "적을 계속 응시한 채 좌우로 곡선을 그리며 돕니다. 적의 측면에 서면 피해 +25%.";
                case SoulMoveStyle.Kite: return "치고 빠지기: 공격 직후 적이 가까우면 한 걸음 물러납니다. 읽어낸 공격 범위에서 빠져나가는 회피로도 씁니다.";
                case SoulMoveStyle.Rush: return "돌입: 2칸 넘게 떨어진 적에게 45% 빠르게 달려듭니다.";
                case SoulMoveStyle.Escort: return "호위: 공격받는 후열 아군과 적 사이를 막아섭니다.";
                default: return "가장 짧은 길로 접근합니다.";
            }
        }

        // ── stats: new pattern choice (was the growth tab) ───────

        void BuildPatternChoice(SoulMercenary hero)
        {
            var whole = Column(0, 0);
            var top = Header(whole, 48);
            var back = SoulUi.Button(top, "나중에", SoulUi.TabInactive, () => { levelChoice = null; signature = null; }, 20, SoulIconSet.Ui("fold"));
            SoulUi.Layout(back.gameObject, 160, 44);
            HeaderText(top, $"새 패턴 배우기 · 남은 선택 {hero.PatternPicks}", "3개 중 1개 · 10레벨마다 1번");

            if (levelChoice.Patterns.Length == 0)
            {
                var empty = SoulUi.Rect("Empty", whole);
                SoulUi.Place(empty, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -60));
                SoulUi.Vertical(empty.gameObject, 8);
                Line(empty, SoulIconSet.Ui("lock"), "선택 가능한 새 패턴이 없습니다.", SoulUi.SubText);
                return;
            }
            selectedPattern = Mathf.Clamp(selectedPattern, 0, levelChoice.Patterns.Length - 1);
            int count = levelChoice.Patterns.Length;
            float width = (body.rect.width > 0 ? body.rect.width : Width - SideWidth - 44) - (count - 1) * Gap;
            width /= count;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var pattern = levelChoice.Patterns[i];
                var card = SoulUi.Panel("Choice", whole, i == selectedPattern ? SoulUi.TabActive : SoulUi.SlotBg, true);
                SoulUi.Place(card.rectTransform, Vector2.zero, new Vector2(0, 1), new Vector2(i * (width + Gap), 92), new Vector2(i * (width + Gap) + width, -60));
                SoulUi.Clickable(card.gameObject).onClick.AddListener(() => { selectedPattern = index; signature = null; });
                SoulUi.Vertical(card.gameObject, 6, 14);
                var head = SoulUi.Row(card.transform, 56, 10);
                SoulUi.Icon(head, SoulDescribe.PatternIcon(pattern), 52);
                var name = SoulUi.Text(head, $"<b>{pattern.Id}</b>\n<size=18><color=#{SoulUi.Hex(SoulUi.SubText)}>{SoulDescribe.Category(pattern.Category)} 패턴</color></size>", 26, Color.white);
                SoulUi.Layout(name.gameObject, -1, -1, 1);
                foreach (var line in PatternLines(hero, pattern))
                {
                    if (line.Cost)
                    {
                        SoulUi.Icon(head, line.Icon, 28);
                        var price = SoulUi.Text(head, line.Text, 24, Color.white, TextAlignmentOptions.MidlineRight);
                        price.fontStyle = FontStyles.Bold;
                        price.textWrappingMode = TextWrappingModes.NoWrap;
                        continue;
                    }
                    if (line.Header) { GroupTitle(card.transform, line.Text); continue; }
                    Line(card.transform, line.Icon, line.Text, Color.white);
                }
            }

            var actions = SoulUi.Row(whole, 76, 12);
            SoulUi.Place(actions, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 76));
            var confirm = SoulUi.Button(actions, $"'{levelChoice.Patterns[selectedPattern].Id}' 배우기", SoulUi.TabActive, () =>
            {
                if (Session.ConfirmLevel(levelChoice, levelChoice.Patterns[selectedPattern])) { levelChoice = null; signature = null; }
            }, 26, SoulIconSet.Ui("level"));
            SoulUi.Layout(confirm.gameObject, -1, -1, 1);
        }

        // ── tab: souls ───────────────────────────────────────────

        void BuildSouls(SoulMercenary hero)
        {
            var left = Column(0, 540);
            var top = Header(left, 48);
            HeaderText(top, "영혼 임시 보관함", $"보존석 {Session.PreservationItems}개");
            var note = SoulUi.Text(left, "흡수하지 않은 영혼은 던전을 나가면 사라집니다. 보존석을 쓰면 남습니다.", 17, SoulUi.SubText, TextAlignmentOptions.TopLeft);
            SoulUi.Place(note.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -80), new Vector2(0, -50));
            var list = ScrollBelow(left, 88);

            var right = Column(560, 0);
            SoulUi.Vertical(right.gameObject, 10);

            if (Session.Stash.Count == 0)
            {
                var empty = SoulUi.Row(list, 120, 16);
                SoulUi.Icon(empty, SoulIconSet.Ui("soul"), 96).color = new Color(1, 1, 1, .4f);
                SoulUi.Text(empty, "아직 얻은 영혼이 없습니다.\n몬스터를 처치하면 낮은 확률로 영혼이 떨어집니다.", 22, SoulUi.SubText);
                return;
            }
            selectedDrop = Mathf.Clamp(selectedDrop, 0, Session.Stash.Count - 1);
            var grid = SoulUi.Flow(list, new Vector2(124, 164), 10);
            for (int i = 0; i < Session.Stash.Count; i++)
            {
                int index = i;
                var drop = Session.Stash[i];
                var card = SoulUi.Panel("Soul", grid, i == selectedDrop ? SoulUi.TabActive : SoulUi.SlotBg, true);
                var picture = SoulUi.Rect("Picture", card.transform);
                SoulUi.Place(picture, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-50, -104), new Vector2(50, -4));
                SoulKit.MonsterPicture(picture, drop.Soul.Source, 100);
                var label = SoulUi.Text(card.transform, drop.Soul.OriginMonster, 18, Color.white, TextAlignmentOptions.Center);
                label.overflowMode = TextOverflowModes.Ellipsis;
                SoulUi.Place(label.rectTransform, Vector2.zero, new Vector2(1, .32f), new Vector2(4, 22), new Vector2(-4, 0));
                // grade as small icons (the UI font has no star glyph), preserved souls get the chain icon
                var grade = SoulUi.Row(card.transform, 20, 2);
                grade.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                SoulUi.Place(grade, Vector2.zero, new Vector2(1, 0), new Vector2(4, 2), new Vector2(-4, 22));
                for (int g = 0; g < Mathf.Clamp(drop.Soul.Grade, 1, 5); g++) SoulUi.Icon(grade, SoulIconSet.Ui("level"), 18);
                if (drop.Preserved) SoulUi.Icon(grade, SoulIconSet.Ui("preserve"), 18);
                SoulUi.Clickable(card.gameObject).onClick.AddListener(() => selectedDrop = index);
            }

            var chosen = Session.Stash[selectedDrop];
            var head = SoulUi.Row(right, 96, 14);
            var face = SoulUi.Panel("Face", head, SoulUi.SlotBg, true);
            SoulUi.Layout(face.gameObject, 96, 96);
            SoulKit.MonsterPicture(face.rectTransform, chosen.Soul.Source, 96);
            var title = SoulUi.Text(head, $"<b>{chosen.Soul.OriginMonster}의 영혼</b>\n<size=19><color=#{SoulUi.Hex(SoulUi.SubText)}>{chosen.Soul.Grade}등급" + (chosen.Preserved ? " · 보존됨" : "") + "</color></size>", 30, Color.white);
            SoulUi.Layout(title.gameObject, -1, -1, 1);

            Section(right, $"{hero.Name}에게 흡수하면", null);
            foreach (var line in SoulLines(chosen.Soul, hero)) Line(right, line.Icon, line.Text, Color.white);

            string blocked = Session.AbsorbBlockReason(hero, chosen);
            if (blocked != null) Line(right, SoulIconSet.Ui("lock"), blocked, SoulUi.Bad);
            var actions = SoulUi.Row(right, 70, 12);
            var absorb = SoulUi.Button(actions, $"{hero.Name}에게 흡수", SoulUi.TabActive, () => Session.Absorb(hero, chosen), 24, SoulIconSet.Ui("soul"));
            absorb.interactable = blocked == null;
            SoulUi.Layout(absorb.gameObject, -1, -1, 1);
            var preserve = SoulUi.Button(actions, $"보존석 사용 ({Session.PreservationItems})", SoulUi.TabInactive, () => Session.Preserve(chosen), 24, SoulIconSet.Ui("preserve"));
            preserve.interactable = !chosen.Preserved && Session.PreservationItems > 0;
            SoulUi.Layout(preserve.gameObject, -1, -1, 1);
            Line(right, SoulIconSet.Ui("lock"), "흡수한 영혼은 되돌릴 수 없습니다.", SoulUi.SubText);
        }

        // What a soul gives, as icon lines, judged against a specific mercenary.
        List<SoulLine> SoulLines(SoulData soul, SoulMercenary hero)
        {
            var lines = new List<SoulLine>();
            foreach (var bonus in soul.CharacteristicStats)
                lines.Add(new SoulLine(SoulIconSet.Stat(bonus.Stat), $"{SoulDescribe.Colored(Rules, bonus.Stat)} {SoulUi.Colored($"+{bonus.Value * HeroStatPipeline.SoulPower:0.#}", SoulUi.Good)}"));
            foreach (var pattern in soul.Patterns)
            {
                if (pattern == null) continue;
                var fit = hero.Fit(pattern);
                string state = fit == SoulPatternFit.New ? SoulUi.Colored("새로 획득", SoulUi.Good)
                    : fit == SoulPatternFit.Owned ? SoulUi.Colored("이미 보유 — 중복 없음", SoulUi.SubText)
                    : fit == SoulPatternFit.RaceBlocked ? SoulUi.Colored("종족 제약 — 장착 불가 (다른 효과는 적용)", SoulUi.Bad)
                    : SoulUi.Colored("무기 필요 — 맞는 무기 장착 시 활성", SoulUi.SubText);
                string core = soul.CorePattern == pattern ? " [핵심]" : "";
                lines.Add(new SoulLine(SoulDescribe.PatternIcon(pattern), $"패턴 {pattern.Id}{core}  {state}"));
            }
            foreach (var skill in soul.ActiveSkills)
                if (skill != null) lines.Add(new SoulLine(skill.Icon != null ? skill.Icon : SoulIconSet.Ui("skill"), $"액티브 {skill.SkillName}  " + SoulDescribe.Small(SoulDescribe.SkillSummary(hero, skill))));
            foreach (var passive in soul.Passives)
                if (passive != null) lines.Add(new SoulLine(passive.Icon != null ? passive.Icon : SoulIconSet.Ui("passive"), $"패시브 {passive.SkillName}  " + SoulDescribe.Small(SoulDescribe.PassiveSummary(hero, passive, Rules))));
            if (!Mathf.Approximately(soul.HeightMultiplier, 1)) lines.Add(new SoulLine(SoulIconSet.Ui("height"), $"키 ×{soul.HeightMultiplier:0.##}"));
            if (!Mathf.Approximately(soul.WeightMultiplier, 1)) lines.Add(new SoulLine(SoulIconSet.Stat(StatType.BodyWeight), $"몸무게 ×{soul.WeightMultiplier:0.##} → {hero.Stats.Weight * soul.WeightMultiplier:0.#}kg"));
            if (!string.IsNullOrEmpty(soul.Appearance.Body) || !string.IsNullOrEmpty(soul.Appearance.Head)) lines.Add(new SoulLine(SoulIconSet.Ui("look"), "외형 변화"));
            return lines;
        }

        // ── tab: bestiary ────────────────────────────────────────

        // Every species this mercenary has helped defeat: its first defeat paid ten times the experience.
        void BuildCodex(SoulMercenary hero)
        {
            var whole = Column(0, 0);
            var top = Header(whole, 48);
            HeaderText(top, $"몬스터 도감 {hero.Codex.Count}종", "처음 쓰러뜨린 종류는 경험치 ×10, 그 뒤로는 ×1");
            var list = ScrollBelow(whole, 56);
            if (hero.Codex.Count == 0) { Line(list, SoulIconSet.Ui("map"), "아직 쓰러뜨린 몬스터가 없습니다.", SoulUi.SubText); return; }
            var entries = new List<SoulCodexEntry>(hero.Codex.Values);
            entries.Sort((a, b) => b.Defeated.CompareTo(a.Defeated));
            var grid = SoulUi.Flow(list, new Vector2(535, 112), 10);
            foreach (var entry in entries)
            {
                var monster = entry.Monster;
                var card = SoulUi.Panel("Monster", grid, SoulUi.SlotBg, true);
                var picture = SoulUi.Rect("Picture", card.transform);
                SoulUi.Place(picture, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(10, -44), new Vector2(98, 44));
                SoulKit.MonsterPicture(picture, monster, 88);
                string kind = monster.Guardian ? SoulUi.Colored("  보스", SoulUi.Bad) : monster.Elite ? SoulUi.Colored("  엘리트", SoulUi.Accent) : "";
                var patterns = new List<string>();
                foreach (var pattern in monster.Patterns) if (pattern != null) patterns.Add(pattern.Id);
                foreach (var telegraph in monster.Telegraphs) if (telegraph != null) patterns.Add(telegraph.SkillName);
                string soul = monster.DroppedSoul != null ? $"영혼 {monster.SoulDropChance * 100:0}%" : "영혼 없음";
                var text = SoulUi.Text(card.transform,
                    $"<b>{monster.Name}</b>{kind}\n<size=17><color=#{SoulUi.Hex(SoulUi.SubText)}>함께 처치 {entry.Defeated} · 직접 {entry.Kills} · {entry.FirstFloor}층에서 처음\n{soul} · 경험치 {monster.Experience}\n{string.Join(", ", patterns)}</color></size>",
                    23, Color.white, TextAlignmentOptions.TopLeft);
                text.overflowMode = TextOverflowModes.Ellipsis;
                SoulUi.Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(108, 8), new Vector2(-10, -8));
            }
        }

        // ── helpers ──────────────────────────────────────────────

        static void Section(Transform parent, string title, string note)
        {
            var row = SoulUi.Row(parent, 40, 10);
            var label = SoulUi.Text(row, title, 23, SoulUi.SectionLbl, TextAlignmentOptions.BottomLeft);
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (!string.IsNullOrEmpty(note))
            {
                var hint = SoulUi.Text(row, note, 17, new Color(.55f, .58f, .7f), TextAlignmentOptions.BottomLeft);
                hint.textWrappingMode = TextWrappingModes.NoWrap;
                SoulUi.Layout(hint.gameObject, -1, -1, 1);
            }
        }

        // Icon + text that wraps: the row grows with the text instead of clipping it.
        static void Line(Transform parent, Sprite icon, string text, Color color)
        {
            var row = SoulUi.Rect("Line", parent);
            var layout = SoulUi.Horizontal(row.gameObject, 10);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandHeight = false;
            SoulUi.Layout(row.gameObject, -1, -1).minHeight = 34;
            if (icon != null) SoulUi.Icon(row, icon, 32);
            var label = SoulUi.Text(row, text, 20, color, TextAlignmentOptions.TopLeft);
            SoulUi.Layout(label.gameObject, -1, -1, 1);
        }
    }
}
