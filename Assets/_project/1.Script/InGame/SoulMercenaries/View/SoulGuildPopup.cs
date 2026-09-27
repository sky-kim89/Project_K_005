using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // ── 길드: 파티, 용병단, 고용, 길드 능력 ──────────────────────
    // 파티: the open presets as cards in a strip that slides sideways (members, 전투력, target floor and mode, the
    // provisions it holds — set at the portal); a click picks one. Below, always, the company as portrait cards (drop
    // one on a preset, drag a face out), the selected mercenary in the side panel.
    // 용병단: the whole company and the selected mercenary — stats as icons, or the paper doll with its six slots;
    // 해고 at the right end of the header, across from the portrait.

    public sealed class SoulGuildPopup : SoulBuildingPopup
    {
        protected override SoulBuildingKind Kind => SoulBuildingKind.Guild;
        protected override string[] Tabs => new[] { "파티", "용병단", "고용", "길드 능력" };
        const float SideWidth = 600;
        int selected, selectedOffer;
        bool gearView;                    // side panel: stats or equipment

        protected override void BuildBody(RectTransform body, int tab)
        {
            if (tab == 0) Parties(body);
            else if (tab == 1) Company(body);
            else if (tab == 2) Hiring(body);
            else Abilities(body);
        }

        // ── the company ──────────────────────────────────────────

        const float PresetWidth = 470, PresetHeight = 214, PresetTitle = 40;
        static float presetScroll; // where the preset strip was slid to (kept across rebuilds)

        // The presets side by side in a strip that slides; a click picks one. The company is always below it (drag a
        // card onto a preset, or use the side panel's button), the selected mercenary in the side panel.
        void Parties(RectTransform body)
        {
            var (mainArea, sideArea) = SoulKit.Split(body, SideWidth);
            var party = EditedParty;
            var top = SoulUi.Rect("Presets", mainArea);
            SoulUi.Place(top, new Vector2(0, 1), Vector2.one, new Vector2(0, -PresetTitle - PresetHeight), Vector2.zero);
            var heading = SoulUi.Rect("Heading", top);
            SoulUi.Place(heading, new Vector2(0, 1), Vector2.one, new Vector2(4, -PresetTitle), Vector2.zero);
            SoulUi.Vertical(heading.gameObject, 0);
            SoulKit.Section(heading, "파티", $"{Campaign.Parties.Count}/{SoulCampaign.MaxParties}");
            var stripArea = SoulUi.Rect("Strip", top);
            SoulUi.Place(stripArea, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -PresetTitle));
            var strip = SoulKit.Slider(stripArea, 12, out var slider);
            foreach (var each in Campaign.Parties) SoulUi.Layout(PresetCard(strip, each).gameObject, PresetWidth, PresetHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(strip);
            slider.horizontalNormalizedPosition = presetScroll;
            slider.onValueChanged.AddListener(value => presetScroll = value.x);

            var listArea = SoulUi.Rect("Company", mainArea);
            SoulUi.Place(listArea, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -PresetTitle - PresetHeight - 12));
            var main = SoulKit.Scroll(listArea);
            SoulKit.Section(main, "용병", $"{Campaign.HeadCount}/{Campaign.RosterLimit}");
            if (Campaign.Roster.Count == 0) return;
            selected = Mathf.Clamp(selected, 0, Campaign.Roster.Count - 1);
            var cards = SoulKit.Grid(main, new Vector2(150, 196), 12);
            for (int i = 0; i < Campaign.Roster.Count; i++)
            {
                int index = i;
                var hero = Campaign.Roster[i];
                var card = HeroCard(cards, hero, i == selected, () => { selected = index; pending = null; slotOpen = false; Refresh(); }, Campaign.PartyOf(hero)?.Name, SoulUi.SubText);
                SoulKit.Draggable(card, "hero:" + hero.Id, SoulPortraits.For(hero) ?? SoulIconSet.Ui("race"));
            }
            HeroPanel(sideArea, Campaign.Roster[selected], party);
        }

        void Company(RectTransform body)
        {
            var (mainArea, sideArea) = SoulKit.Split(body, SideWidth);
            var main = SoulKit.Scroll(mainArea);
            SoulKit.Section(main, "용병단", $"{Campaign.HeadCount}/{Campaign.RosterLimit}");
            if (Campaign.Roster.Count == 0) return;
            selected = Mathf.Clamp(selected, 0, Campaign.Roster.Count - 1);
            var cards = SoulKit.Grid(main, new Vector2(150, 196), 12);
            for (int i = 0; i < Campaign.Roster.Count; i++)
            {
                int index = i;
                var hero = Campaign.Roster[i];
                HeroCard(cards, hero, i == selected, () => { selected = index; pending = null; slotOpen = false; Refresh(); }, Campaign.PartyOf(hero)?.Name, SoulUi.SubText);
            }
            HeroPanel(sideArea, Campaign.Roster[selected], null);
        }

        // The selected mercenary: header (해고 at its right end), the party toggle when a preset is open, stats or
        // the paper doll.
        void HeroPanel(RectTransform sideArea, SoulMercenary hero0, SoulParty party)
        {
            var side = SoulKit.Side(sideArea);
            if (gearView && slotOpen) { SlotSwap(side, hero0); return; } // only the swap: no stats, no doll
            var header = HeroHeader(side, hero0);
            if (Campaign.Roster.Count > 1)
            {
                var dismiss = SoulUi.Button(header, "해고", new Color(.45f, .16f, .16f),
                    () => Confirm("dismiss", $"정말 해고하시겠습니까?\n<size=22><color=#{SoulUi.Hex(SoulUi.Accent)}>{hero0.Name}</color> · 장착한 장비도 함께 떠납니다</size>", () => { Campaign.Dismiss(hero0); selected = 0; }),
                    18, SoulIconSet.Ui("exit"));
                SoulUi.Layout(dismiss.gameObject, 110, 44);
            }
            var actions = SoulUi.Row(side, 48, 8);
            if (party != null)
            {
                bool away = Campaign.IsAway(party), inParty = party.Members.Contains(hero0);
                var toggle = SoulKit.Action(actions, inParty ? $"{party.Name} 제외" : $"{party.Name} 편성", inParty ? SoulUi.TabInactive : new Color(.2f, .5f, .28f),
                    !away && (inParty || party.Members.Count < Campaign.PartySize), () => Campaign.TogglePartyMember(hero0, party), 48, SoulIconSet.Ui("party"));
                SoulUi.Layout(toggle.gameObject, -1, 48, 1);
            }
            foreach (var (label, gear) in new[] { ("능력치", false), ("장비", true) })
            {
                bool target = gear;
                var button = SoulKit.Action(actions, label, gearView == gear ? SoulUi.TabActive : SoulUi.TabInactive, true, () => { gearView = target; pending = null; slotOpen = false; Refresh(); }, 48);
                SoulUi.Layout(button.gameObject, party != null ? 120 : -1, 48, party != null ? -1 : 1);
            }
            if (hero0.Wounds > 0)
            {
                var wounds = SoulUi.Row(side, 32, 8);
                SoulUi.Icon(wounds, SoulIconSet.Status(SoulStatus.Bleed), 28);
                SoulKit.Pips(wounds, hero0.Wounds, SoulMercenary.MaxWounds, SoulUi.Bad, new Color(.25f, .2f, .22f), 14);
                if (Campaign.Potions > 0)
                {
                    var potion = SoulKit.Action(wounds, $"포션 ({Campaign.Potions})", SoulUi.TabInactive, true, () => Campaign.UsePotion(hero0), 32, SoulIconSet.Ui("potion"));
                    SoulUi.Layout(potion.gameObject, 150, 32);
                }
                SoulTooltip.Attach(wounds, SoulIconSet.Status(SoulStatus.Bleed), "부상", () => WoundLines(hero0));
            }
            if (gearView || pending != null) { Paperdoll(side, hero0); return; }
            HeroStats(side, hero0);
            HeroKit(side, hero0);
        }

        bool JoinParty(SoulParty party, string payload)
        {
            if (!payload.StartsWith("hero:")) return false;
            var hero = Campaign.Roster.Find(h => h.Id == payload.Substring(5));
            if (hero == null || party.Members.Contains(hero)) return false;
            if (!Campaign.Assign(hero, party)) { Message = $"{party.Name} 인원 초과"; Refresh(); return false; }
            return true;
        }

        static readonly string[] FloorOptions = { "1층", "2층", "3층", "4층", "5층", "6층", "7층", "8층" };
        static readonly string[] ModeOptions = { "사냥", "탐험" };

        // A preset: name and 전투력, five member slots, target floor and mode, provisions (icon + count).
        RectTransform PresetCard(Transform parent, SoulParty party)
        {
            bool chosen = party == EditedParty, away = Campaign.IsAway(party);
            var card = SoulUi.Framed("Preset", parent, SoulKit.CardBg, chosen ? SoulUi.Accent : SoulUi.PanelBorder, chosen ? 4 : 2);
            card.GetComponent<Image>().raycastTarget = true;
            int index = party.Number - 1;
            SoulUi.Clickable(card.gameObject).onClick.AddListener(() => { partyIndex = index; Refresh(); });
            if (!away) SoulKit.DropZone(card, payload => JoinParty(party, payload));

            var title = SoulUi.Text(card, $"<b>{party.Name}</b>", 21, Color.white, TextAlignmentOptions.TopLeft);
            SoulUi.Place(title.rectTransform, new Vector2(0, 1), new Vector2(.5f, 1), new Vector2(14, -38), new Vector2(0, -8));
            var power = SoulUi.Text(card, away ? "원정 중" : $"전투력 {party.Power}", 19, away ? SoulUi.Accent : SoulUi.Good, TextAlignmentOptions.TopRight);
            SoulUi.Place(power.rectTransform, new Vector2(.5f, 1), Vector2.one, new Vector2(0, -38), new Vector2(-14, -8));

            var slots = SoulUi.Row(card, 62, 6);
            SoulUi.Place(slots, new Vector2(0, 1), Vector2.one, new Vector2(12, -106), new Vector2(-12, -44));
            for (int i = 0; i < Mathf.Max(Campaign.PartySize, party.Members.Count); i++)
            {
                if (i >= party.Members.Count) { SoulUi.Layout(EmptySeat(slots, SoulIconSet.Ui("party")).gameObject, 62, 62); continue; }
                var member = party.Members[i];
                var face = SoulUi.Portrait(slots, SoulPortraits.For(member), 62, SoulIconSet.Ui("race"));
                SoulKit.JobBadge(face, member, 22, new Vector2(1, 0));
                if (Campaign.EnteredToday(member)) foreach (var image in face.GetComponentsInChildren<Image>()) image.color *= new Color(.45f, .45f, .45f, 1); // went down today
                int heroIndex = Campaign.Roster.IndexOf(member);
                if (heroIndex < 0) continue; // down in the dungeon
                face.GetComponent<Image>().raycastTarget = true;
                SoulUi.Clickable(face.gameObject).onClick.AddListener(() => { selected = heroIndex; partyIndex = index; pending = null; slotOpen = false; Refresh(); });
                SoulKit.Draggable(face, "hero:" + member.Id, SoulPortraits.For(member) ?? SoulIconSet.Ui("race"), () => Campaign.Assign(member, null));
            }

            var orders = SoulUi.Row(card, 38, 8);
            SoulUi.Place(orders, new Vector2(0, 1), Vector2.one, new Vector2(12, -152), new Vector2(-12, -114));
            var floor = SoulUi.Dropdown(orders, FloorOptions, party.TargetFloor - 1, value => Campaign.SetTarget(party, value + 1, party.TargetMode));
            SoulUi.Layout(floor.gameObject, 110, 38);
            floor.interactable = !away;
            var mode = SoulUi.Dropdown(orders, ModeOptions, party.TargetMode == SoulExploreMode.Farm ? 1 : 0,
                value => Campaign.SetTarget(party, party.TargetFloor, value == 1 ? SoulExploreMode.Farm : SoulExploreMode.Hunt));
            SoulUi.Layout(mode.gameObject, 110, 38);
            mode.interactable = !away;

            var kit = SoulUi.Row(card, 34, 6);
            SoulUi.Place(kit, Vector2.zero, new Vector2(1, 0), new Vector2(12, 10), new Vector2(-12, 44));
            foreach (var entry in party.Carry)
            {
                var def = SoulSupplies.Get(entry.Id);
                if (def == null) continue;
                var chip = SoulUi.Chip(kit, SoulIconSet.Ui(def.Icon), $"{entry.Count}", Color.white, 34);
                SoulUi.Layout(chip.gameObject, 72, 34);
                foreach (var graphic in chip.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            }
            return card;
        }

        void Paperdoll(Transform side, SoulMercenary hero)
        {
            var doll = Paperdoll(side, hero, (slot, index) => gearSlot == slot && (slot != SoulEquipSlot.Accessory || gearAccessory == index),
                (slot, index, shown) => { gearSlot = slot; gearAccessory = index; pending = null; slotOpen = true; Refresh(); });
            // 자동 장착 — beside the head, above the off hand: the storehouse's best for this one (what it replaces is destroyed)
            bool better = Campaign.Roster.Contains(hero) && Campaign.NextUpgrade(hero, out _) != null;
            var auto = SoulUi.Button(doll, "자동 장착", better ? new Color(.2f, .45f, .6f) : SoulUi.TabInactive,
                () => Confirm("autoequip", $"창고에서 더 나은 장비를 장착할까요?\n<size=22><color=#{SoulUi.Hex(SoulUi.Accent)}>{hero.Name}</color> · 교체되는 장비는 창고로 돌아갑니다</size>",
                    () => { var worn = Campaign.AutoEquip(hero); Message = worn.Count > 0 ? $"{hero.Name}: {string.Join(", ", worn.ConvertAll(i => i.Name))} 장착" : "더 나은 장비가 없습니다"; }), 18);
            auto.interactable = better;
            var autoRect = (RectTransform)auto.transform;
            autoRect.anchorMin = autoRect.anchorMax = new Vector2(.85f, .87f);
            autoRect.sizeDelta = new Vector2(120, 44);
            autoRect.anchoredPosition = Vector2.zero;
            var load = SoulUi.Text(doll, LoadText(hero), 16, Color.white, TextAlignmentOptions.TopLeft);
            SoulUi.Place(load.rectTransform, new Vector2(0, 1), new Vector2(.4f, 1), new Vector2(12, -40), new Vector2(0, -8));

        }

        // ── hiring ───────────────────────────────────────────────

        void Hiring(RectTransform body)
        {
            var (mainArea, sideArea) = SoulKit.Split(body, SideWidth);
            var main = SoulKit.Scroll(mainArea);
            SoulKit.Section(main, "고용 후보", $"용병단 {Campaign.HeadCount}/{Campaign.RosterLimit}");
            if (Campaign.Offers.Count == 0) return;
            selectedOffer = Mathf.Clamp(selectedOffer, 0, Campaign.Offers.Count - 1);
            var cards = SoulKit.Grid(main, new Vector2(150, 196), 12);
            for (int i = 0; i < Campaign.Offers.Count; i++)
            {
                int index = i;
                var offer = Campaign.Offers[i];
                HeroCard(cards, offer.Preview(Campaign.Rules), i == selectedOffer, () => { selectedOffer = index; Refresh(); }, $"{offer.Price}", SoulUi.Accent);
            }

            var chosen = Campaign.Offers[selectedOffer];
            var hero = chosen.Preview(Campaign.Rules);
            var side = SoulKit.Side(sideArea);
            var header = HeroHeader(side, hero);
            // 고용 at the right end of the header — where 해고 is for the company's own
            bool full = Campaign.HeadCount >= Campaign.RosterLimit, poor = Campaign.Gold < chosen.Price;
            var hire = SoulUi.Button(header, full ? "정원 초과" : poor ? "금화 부족" : $"고용 {chosen.Price}", full || poor ? SoulUi.TabInactive : new Color(.2f, .5f, .28f),
                () => Confirm("hire", $"정말 고용하시겠습니까?\n<size=22><color=#{SoulUi.Hex(chosen.Rarity != SoulStyleRarity.Normal ? SoulHireStyles.RarityColor(chosen.Rarity) : SoulUi.Accent)}>{(chosen.Rarity != SoulStyleRarity.Normal ? SoulHireStyles.RarityName(chosen.Rarity) + " " : "")}{hero.Name}</color> · {chosen.Price} 금화</size>", () => { Message = Campaign.Hire(chosen) ? null : "고용할 수 없습니다"; selectedOffer = 0; }),
                18, SoulIconSet.Ui("gold"));
            hire.interactable = !full && !poor;
            SoulUi.Layout(hire.gameObject, 130, 44);
            HeroStats(side, hero);
            HeroKit(side, hero);
            if (hero.Equipment.Count > 0)
            {
                var gear = SoulKit.Grid(side, new Vector2(72, 72), 8);
                foreach (var item in Sorted(hero.Equipment)) ItemTile(gear, item, 72, false, null, hero);
            }
        }

        // ── guild abilities (길드 능력): gold for company-wide upgrades, by group ──

        void Abilities(RectTransform body)
        {
            var main = SoulKit.Scroll(body);
            int guild = Campaign.Level(SoulBuildingKind.Guild);
            // an ability not started whose first level wants a bigger guild is not shown yet
            bool Shown(SoulPerkDef def) => Campaign.PerkLevel(def.Perk) > 0 || guild >= def.GuildLevels[0];
            for (int group = 0; group < SoulCampaign.GroupNames.Length; group++)
            {
                var defs = System.Array.FindAll(SoulCampaign.PerkDefs, def => (int)def.Group == group && Shown(def));
                if (defs.Length == 0) continue;
                SoulKit.Section(main, SoulCampaign.GroupNames[group]);
                bool stats = group == (int)SoulPerkGroup.Stats;
                var grid = SoulKit.Grid(main, stats ? new Vector2(250, 132) : new Vector2(330, 132), 12);
                foreach (var def in defs) AbilityCard(grid, def);
            }
        }

        void AbilityCard(Transform parent, SoulPerkDef def)
        {
            int level = Campaign.PerkLevel(def.Perk);
            bool maxed = level >= def.MaxLevel;
            string block = Campaign.PerkBlock(def.Perk);
            var card = SoulUi.Framed("Ability", parent, SoulKit.CardBg, level > 0 ? SoulUi.Accent : SoulUi.PanelBorder, 2);
            var icon = SoulUi.Icon(card, SoulIconSet.Get(def.Icon), 44);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 1);
            icon.rectTransform.anchoredPosition = new Vector2(32, -32);
            var name = SoulUi.Text(card, $"<b>{def.Name}</b>  <size=15><color=#{SoulUi.Hex(SoulUi.Accent)}>{level}/{def.MaxLevel}</color></size>", 19, Color.white, TextAlignmentOptions.MidlineLeft);
            SoulUi.Place(name.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(62, -32), new Vector2(-8, -8));
            string now = level > 0 ? def.Effects[level - 1] : "—";
            string next = maxed ? "" : $"  →  <color=#{SoulUi.Hex(SoulUi.Good)}>{def.Effects[level]}</color>";
            var effect = SoulUi.Text(card, now + next, 16, SoulUi.SubText, TextAlignmentOptions.TopLeft);
            SoulUi.Place(effect.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(62, -80), new Vector2(-8, -36));
            var perk = def.Perk;
            // the next level wants a bigger guild: said on the button, in red
            bool needsGuild = !maxed && Campaign.Level(SoulBuildingKind.Guild) < def.GuildLevels[level];
            var button = SoulUi.Button(card, maxed ? "최고 단계" : needsGuild ? $"길드 {def.GuildLevels[level]}단계 필요" : $"{def.Costs[level]} 금화", new Color(.2f, .42f, .7f), () => { Campaign.BuyPerk(perk); Message = null; }, 17);
            button.interactable = block == null;
            if (needsGuild) foreach (var label in button.GetComponentsInChildren<TMPro.TMP_Text>()) label.color = SoulUi.Bad;
            SoulUi.Place((RectTransform)button.transform, Vector2.zero, new Vector2(1, 0), new Vector2(10, 10), new Vector2(-10, 46));
            SoulTooltip.Attach(card, SoulIconSet.Get(def.Icon), def.Name, () => AbilityLines(def));
        }

        List<SoulLine> AbilityLines(SoulPerkDef def)
        {
            var lines = new List<SoulLine>();
            int level = Campaign.PerkLevel(def.Perk);
            for (int i = 0; i < def.MaxLevel; i++)
            {
                string need = (def.GuildLevels[i] > 1 ? $" · 길드 {def.GuildLevels[i]}" : "") + (def.RenownTiers[i] > 0 ? $" · 명성 {SoulCampaign.RenownNameOf(def.RenownTiers[i])}" : "");
                string text = $"{i + 1}단계  {def.Effects[i]}  <size=15><color=#{SoulUi.Hex(SoulUi.SubText)}>{def.Costs[i]} 금화{need}</color></size>";
                lines.Add(new SoulLine(null, i < level ? SoulUi.Colored(text, SoulUi.Good) : text));
            }
            string block = Campaign.PerkBlock(def.Perk);
            if (block != null && level < def.MaxLevel) lines.Add(new SoulLine(SoulIconSet.Ui("lock"), block));
            return lines;
        }
    }
}
