using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    public sealed class SoulHireOffer
    {
        public SoulRecruitData Recruit;  // who (null: a plain one of `Template`, named `Name`)
        public SoulMercenaryData Template;
        public string Name;
        public int Price;
        public int GearGrade = 2;    // the gear the hire brings (rolled with the offer, so the preview is the hire)
        SoulMercenary preview;

        public SoulStyleRarity Rarity => Recruit != null ? Recruit.Rarity : SoulStyleRarity.Normal;

        public static SoulHireOffer Of(SoulRecruitData recruit, int price, int gearGrade)
            => new SoulHireOffer { Recruit = recruit, Template = recruit.Template, Name = recruit.DisplayName, Price = price, GearGrade = gearGrade };

        // The mercenary this offer would be, for the guild's detail view (not on the roster).
        public SoulMercenary Preview(SoulStatRules rules) => preview ?? (preview = Make(rules, Template.Id + "_offer_" + Name));

        public SoulMercenary Make(SoulStatRules rules, string id)
        {
            int top = Recruit != null && Recruit.Rarity == SoulStyleRarity.Rare ? 4 : 3;
            var hero = SoulDungeonSession.RecruitOne(Template, rules, id, Name, Mathf.Clamp(GearGrade, 1, top), Recruit != null ? Recruit.Race : null);
            SoulHireStyles.Apply(hero, Recruit, rules);
            return hero;
        }
    }

    public enum SoulDifficulty { Normal, Hard, Extreme }

    public sealed class SoulBlessing
    {
        public string Id, Name, Description;
        public int Cost, ChurchLevel;
        public SoulStatBonus[] Bonuses;
    }

    public sealed class SoulTraining
    {
        public string Id, Name, Description;
        public int BaseCost, Level;          // training ground level that unlocks it
        public StatType Stat; public float Amount;
        public bool LearnPattern;
        public float Minutes;                // village minutes it takes (1 real second = 1 minute)
    }

    // A mercenary at the training ground: paid up front, the effect lands when the time is up. Going down to the
    // dungeon pauses it (Paused: the minutes left) and it goes on when the mercenary is back.
    public sealed class SoulDrill
    {
        public SoulTraining Training;
        public float EndsAt;
        public float Paused = -1;
        public bool IsPaused => Paused >= 0;
        public float Left(float clock) => IsPaused ? Paused : UnityEngine.Mathf.Max(0, EndsAt - clock);
    }

    // The campaign between dungeons: gold, the roster and who goes down, what the party owns, the village buildings.
    // Lives across scene loads (Current); the dungeon takes the party from here and hands it back (Depart / Return).
    public sealed partial class SoulCampaign
    {
        public static SoulCampaign Current;

        // Seats in a party: PartySize (3, more with the guild's 파티 정원); MaxParty is the most it can ever be.
        public const int MaxParty = 5;
        // A new company has the first two of the village's starting roster; everyone else is hired at the guild.
        public const int StartingMercenaries = 2;
        // Mercenaries lost in the dungeon (for the village log) — only at 최상; below it the fallen come home hurt.
        public readonly List<string> Fallen = new List<string>();

        // Chosen when the company starts (새로 시작), kept for the whole game. Only 최상 lets the dungeon kill for good:
        // below it one who falls down there is carried home with FallenWounds wounds.
        public SoulDifficulty Difficulty = SoulDifficulty.Normal;
        public bool Permadeath => Difficulty == SoulDifficulty.Extreme;
        // For views: does falling in the dungeon kill for good in the game being played (a sandbox run: yes)?
        public static bool DeathIsFinal => Current == null || Current.Permadeath;
        // A building this game has: the 추모비 stands only where the dungeon kills for good (최상).
        public bool HasBuilding(SoulBuildingKind kind) => kind != SoulBuildingKind.Memorial || Permadeath;
        public static string FallenText(string name)
            => DeathIsFinal ? $"{name} 사망 — 다시 돌아오지 않습니다" : $"{name} 쓰러짐 — 부상 {FallenWounds}로 마을에 실려 갑니다";
        public const int FallenWounds = 5;
        public static string DifficultyName(SoulDifficulty difficulty)
            => difficulty == SoulDifficulty.Extreme ? "최상" : difficulty == SoulDifficulty.Hard ? "어려움" : "보통";
        public readonly SoulVillageData Data;
        public SoulStatRules Rules => Data.StatRules;
        // Gold, soul stones and supplies are counted in ItemData (the game's currency store): the live campaign
        // uses the saved one, a test or sparring campaign its own.
        public readonly ItemData Wallet;
        public int Gold => Wallet.Get(eItem.Gold);
        public int Stones => Wallet.Get(eItem.SoulStone);

        public void SetAmount(eItem item, int value)
        {
            int now = Wallet.Get(item);
            if (value > now) Wallet.Add(item, value - now);
            else if (value < now) Wallet.Spend(item, now - value);
        }
        public readonly List<SoulMercenary> Roster = new List<SoulMercenary>();
        public readonly List<SoulItem> Inventory = new List<SoulItem>();
        public readonly List<SoulItem> ShopStock = new List<SoulItem>();
        public readonly List<SoulData> Vault = new List<SoulData>();
        public readonly Dictionary<SoulBuildingKind, int> Levels = new Dictionary<SoulBuildingKind, int>();
        public readonly List<SoulHireOffer> Offers = new List<SoulHireOffer>();
        public readonly Dictionary<string, int> TrainingCounts = new Dictionary<string, int>();
        public readonly List<string> Log = new List<string>();
        public SoulBlessing Blessing;
        public int Expeditions;
        public int Revision { get; private set; } // bumps on every change: views rebuild on it
        readonly System.Random random;
        int hired;
        public int HiredCount { get => hired; set => hired = value; }

        // A campaign with nothing in it yet (SoulSave fills it from a save).
        SoulCampaign(SoulVillageData data, int seed, ItemData wallet, bool empty)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            random = new System.Random(seed);
            Wallet = wallet ?? new ItemData();
            SyncParties();
        }

        public static SoulCampaign Empty(SoulVillageData data, ItemData wallet = null) => new SoulCampaign(data, System.Environment.TickCount & int.MaxValue, wallet, true);

        public SoulCampaign(SoulVillageData data, int seed, ItemData wallet = null)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            random = new System.Random(seed);
            WorldSeed = seed;
            Wallet = wallet ?? new ItemData();
            SyncParties();
            SetAmount(eItem.Gold, data.StartingGold);
            SetAmount(eItem.SoulStone, data.StartingStones);
            foreach (var supply in SoulSupplies.All) SetAmount(supply.Item, 0);
            Potions = data.StartingPotions;
            foreach (var start in StartingSupplies) SetAmount(SoulSupplies.Get(start.id).Item, start.count);
            // the founders (창단 멤버) on the roster, else the first of the starting jobs
            foreach (var founder in data.Recruits)
                if (founder != null && founder.Founder && founder.Template != null && Roster.Count < StartingMercenaries)
                    Roster.Add(SoulHireOffer.Of(founder, 0, 2).Make(Rules, founder.Id));
            for (int i = 0; i < data.StartingRoster.Length && Roster.Count < StartingMercenaries; i++)
                if (data.StartingRoster[i] != null) Roster.Add(SoulDungeonSession.RecruitOne(data.StartingRoster[i], Rules));
            for (int i = 0; i < Roster.Count && i / PartySize < Parties.Count; i++) Parties[i / PartySize].Members.Add(Roster[i]);
            foreach (var hero in Roster) Record(hero);
            foreach (var building in data.Buildings) Levels[building.Kind] = building.StartLevel;
            AutoFill(Parties[0]);
            RefreshOffers();
            RefreshBoard();
            Active.Add(StarterQuest());
            RefreshStock();
        }

        void Changed(string message = null)
        {
            Revision++;
            if (string.IsNullOrEmpty(message)) return;
            Log.Insert(0, message);
            if (Log.Count > 12) Log.RemoveAt(12);
        }

        // A change made from outside (the cheat window): views rebuild, the message goes to the log.
        public void Touch(string message = null) => Changed(message);

        bool Spend(int cost)
        {
            return cost >= 0 && Wallet.Spend(eItem.Gold, cost);
        }

        // ── buildings ────────────────────────────────────────────

        public int Level(SoulBuildingKind kind) => Levels.TryGetValue(kind, out int level) ? level : 0;

        public SoulBuildingDef Building(SoulBuildingKind kind)
        {
            foreach (var building in Data.Buildings) if (building.Kind == kind) return building;
            return default;
        }

        // The top level of a building: one per entry of its cost list (the guild 5, most others 3).
        public int MaxLevelOf(SoulBuildingKind kind) => Building(kind).UpgradeCosts?.Length ?? 0;

        // Gold for the next level, or -1 when already at the top.
        public int UpgradeCost(SoulBuildingKind kind)
        {
            int level = Level(kind);
            var costs = Building(kind).UpgradeCosts;
            return costs == null || level >= costs.Length ? -1 : costs[level];
        }

        public bool Upgrade(SoulBuildingKind kind)
        {
            int cost = UpgradeCost(kind);
            if (cost < 0 || UpgradeBlock(kind) != null || !Spend(cost)) return false;
            Levels[kind] = Level(kind) + 1;
            if (kind == SoulBuildingKind.Guild) RefreshOffers();
            if (kind == SoulBuildingKind.Board) RefreshBoard();
            if (kind == SoulBuildingKind.Shop) RefreshStock();
            if (kind == SoulBuildingKind.Church && Level(kind) == 1) RefreshOffers();
            Changed(Level(kind) == 1 ? $"{Building(kind).Name} 건설 완료" : $"{Building(kind).Name} {Level(kind)}단계로 강화");
            return true;
        }

        // ── guild: roster, hiring, quests ────────────────────────

        // The company's size by guild level (8 → 50), never below every party the guild can field plus two on the bench.
        public const int MaxRoster = 50;
        static readonly int[] RosterByGuild = { 6, 8, 14, 22, 34, 50 };
        public int RosterLimit => Mathf.Min(MaxRoster, Mathf.Max(RosterByGuild[Mathf.Clamp(Level(SoulBuildingKind.Guild), 0, RosterByGuild.Length - 1)], PartyLimit * PartySize + 2));

        // The day's candidates from the roster (Data.Recruits, founders aside): 2 + the guild's level of them, never one
        // who is in the company (home or away) or has fallen, 정예 and 전설 only once the guild is big enough; priests
        // only once the church stands, and then one of them for sure. Each is always the same person and asks the same
        // price (more for a higher grade).
        public void RefreshOffers()
        {
            Offers.Clear();
            var taken = new HashSet<string>(Fallen);
            foreach (var hero in Roster) taken.Add(hero.Name);
            foreach (var trip in Away) foreach (var hero in trip.Party) taken.Add(hero.Name);
            bool church = Level(SoulBuildingKind.Church) > 0;
            var pool = new List<SoulRecruitData>();
            foreach (var entry in Data.Recruits)
                if (entry != null && entry.Template != null && !entry.Founder && !taken.Contains(entry.DisplayName) && (church || !IsPriest(entry)) && HireOpen(entry)) pool.Add(entry);
            for (int i = pool.Count - 1; i > 0; i--) { int j = random.Next(i + 1); var t = pool[i]; pool[i] = pool[j]; pool[j] = t; }
            int count = Mathf.Min(pool.Count, 2 + Level(SoulBuildingKind.Guild));
            // a mix of jobs: one of each (in the shuffled order) comes first, then whoever else
            var mixed = new List<SoulRecruitData>();
            var jobs = new HashSet<string>();
            foreach (var entry in pool) if (jobs.Add(entry.Job)) mixed.Add(entry);
            foreach (var entry in pool) if (!mixed.Contains(entry)) mixed.Add(entry);
            var chosen = mixed.GetRange(0, count);
            // healers are church-trained: once the church stands, one priest is always among them
            if (church && !chosen.Exists(IsPriest))
            {
                var priest = pool.Find(IsPriest);
                if (priest != null) { if (chosen.Count >= count && chosen.Count > 0) chosen[chosen.Count - 1] = priest; else chosen.Add(priest); }
            }
            foreach (var entry in chosen) Offers.Add(OfferFor(entry));
            Changed();
        }

        // What the guild asks for someone: the base, a priest's church fee, the grade; 전설 bring a grade better gear.
        public SoulHireOffer OfferFor(SoulRecruitData entry)
            => SoulHireOffer.Of(entry, Mathf.Max(50, Data.HirePrice + (IsPriest(entry) ? 100 : 0) + SoulHireStyles.Premium(entry.Rarity) + entry.PriceShift),
                Mathf.Clamp(Level(SoulBuildingKind.Guild), 1, 3) + SoulHireStyles.GearBonus(entry.Rarity));

        bool IsPriest(SoulRecruitData entry) => System.Array.IndexOf(Data.PriestTemplates, entry.Template) >= 0;

        // Who comes to the guild grows with it: 정예 from guild level 2, 전설 from level 3.
        public const int SpecialGuildLevel = 2, RareGuildLevel = 4; // 정예 from guild level 2, 전설 from 4
        public bool HireOpen(SoulRecruitData entry)
            => entry.Rarity == SoulStyleRarity.Normal || Level(SoulBuildingKind.Guild) >= (entry.Rarity == SoulStyleRarity.Rare ? RareGuildLevel : SpecialGuildLevel);

        public bool Hire(SoulHireOffer offer)
        {
            if (!Offers.Contains(offer) || HeadCount >= RosterLimit || Level(SoulBuildingKind.Guild) <= 0 || !Spend(offer.Price)) return false;
            // a hire brings the gear the template lists, worn and bound: 조잡한–평범한 early, better with the guild
            var hero = offer.Make(Rules, offer.Template.Id + "_hire" + (++hired));
            Roster.Add(hero);
            SeatFor()?.Members.Add(hero);
            ApplyGuildStats();
            Offers.Remove(offer);
            Record(hero);
            Changed(offer.Recruit != null && offer.Rarity != SoulStyleRarity.Normal
                ? $"{SoulHireStyles.RarityName(offer.Rarity)} 용병 {offer.Name}({offer.Recruit.ShownTitle}) 합류"
                : $"{offer.Name}({offer.Template.Job}) 고용");
            return true;
        }

        public bool Dismiss(SoulMercenary hero)
        {
            if (!Roster.Contains(hero) || Roster.Count <= 1) return false;
            Roster.Remove(hero); PartyOf(hero)?.Members.Remove(hero);
            Record(hero).Status = SoulRecordStatus.Dismissed;
            // bound gear leaves with its wearer (design: equipment cannot be taken back)
            Changed($"{hero.Name} 해고 — 장비도 함께 떠났습니다");
            return true;
        }

        // ── equipment (guild roster) ─────────────────────────────
        // Six slots: main hand, off hand, body, head, two accessories. What goes on is bound for good — it cannot
        // be taken off, and whatever it replaces is destroyed (DESIGN_EQUIPMENT_MERCENARIES.md §4).

        public const int AccessorySlots = 2;

        // A bow takes both hands, but its quiver hangs beside it.
        public static bool Quivered(SoulItem main, SoulItem off) => main != null && off != null && main.WeaponTag == "bow" && off.Data.Kind == "화살통";

        public static string EquipBlock(SoulMercenary hero, SoulItem item)
        {
            if (item.Slot == SoulEquipSlot.OffHand)
            {
                var main = hero.Equipped(SoulEquipSlot.MainHand);
                if (main != null && main.TwoHanded && !Quivered(main, item)) return $"양손 무기({main.Name})를 들고 있습니다";
            }
            return null;
        }

        // What equipping this destroys. Accessories fill a free slot first; with both taken, `replace` (or the
        // first one) goes. A two-handed weapon takes the off hand with it.
        public static List<SoulItem> Replaced(SoulMercenary hero, SoulItem item, SoulItem replace = null)
        {
            var lost = new List<SoulItem>();
            if (item.Slot == SoulEquipSlot.Accessory)
            {
                var worn = hero.Equipment.FindAll(e => e.Slot == SoulEquipSlot.Accessory);
                if (worn.Count >= AccessorySlots) lost.Add(replace != null && worn.Contains(replace) ? replace : worn[0]);
                return lost;
            }
            var same = hero.Equipped(item.Slot);
            if (same != null) lost.Add(same);
            if (item.Slot == SoulEquipSlot.MainHand && item.TwoHanded)
            {
                var off = hero.Equipped(SoulEquipSlot.OffHand);
                if (off != null && !Quivered(item, off)) lost.Add(off);
            }
            return lost;
        }

        // What a find is worth to this one (0: nothing for it): an upgrade of the same kind (한손검, 판금 갑옷 …) as what
        // it wears in that slot, or an empty slot it suits — armour or a helmet it can carry without being weighed
        // down, a shield for one who fights in front, a 마도서 for a mage, a 화살통 for a bow, a 성물 for a priest — or
        // an accessory for a free (or cheaper) slot. What it replaces goes back to the storehouse (the bag, down there).
        public static int UpgradeGain(SoulMercenary hero, SoulItem item, out SoulItem replace)
        {
            replace = null;
            if (EquipBlock(hero, item) != null) return 0;
            if (item.Slot == SoulEquipSlot.Accessory)
            {
                // judged by what its bonuses do for this one (운 for a mage: nothing), not by its price
                float value = Usefulness(hero, item);
                if (value <= 0) return 0;
                var worn = hero.Equipment.FindAll(e => e.Slot == SoulEquipSlot.Accessory);
                if (worn.Count < AccessorySlots) return Mathf.Max(1, Mathf.RoundToInt(value * 100));
                worn.Sort((a, b) => Usefulness(hero, a).CompareTo(Usefulness(hero, b)));
                replace = worn[0];
                return Mathf.RoundToInt((value - Usefulness(hero, replace)) * 100);
            }
            var current = hero.Equipped(item.Slot);
            if (Burdened(hero, item, current)) return 0;
            if (current == null) return Suits(hero, item) ? Mathf.Max(1, item.Price) : 0;
            if (current.Data.Kind != item.Data.Kind || current.TwoHanded != item.TwoHanded) return 0;
            int lost = 0;
            foreach (var old in Replaced(hero, item)) lost += old.Price;
            return item.Price - lost;
        }

        // What an item's bonuses are worth to this one: each bonus in units of the best enchant of its stat, times
        // how much the stat matters to it (StatWorth).
        public static float Usefulness(SoulMercenary hero, SoulItem item)
        {
            float total = 0;
            foreach (var bonus in item.Bonuses)
            {
                float unit = 0;
                foreach (var option in SoulItemRules.Enchants) if (option.Stat == bonus.Stat && option.Values.Length > 0) unit = Mathf.Max(unit, option.Values[option.Values.Length - 1]);
                total += StatWorth(hero, bonus.Stat) * bonus.Value / (unit > 0 ? unit : 1);
            }
            return total;
        }

        // 운 helps only the 길잡이 (its sense, its finds); 마력, MP and spell power only those who cast; a main stat in
        // its tendencies fully, in its weak ones not at all, others a little; HP, armour, resistances … anyone.
        public static float StatWorth(SoulMercenary hero, StatType stat)
        {
            bool caster = hero.Job == "마법사" || hero.Job == "성직자" || hero.Job == "소환사";
            if (stat == StatType.Luck) return hero.Job == "길잡이" ? 1f : 0f;
            if (stat == StatType.Magic || stat == StatType.MaxMp || stat == StatType.MpRegen || stat == StatType.SpellPower || stat == StatType.ManaRegenRate) return caster ? 1f : 0f;
            if (!HeroStatPipeline.IsUpper(stat)) return .6f;
            if (System.Array.IndexOf(hero.WeakGrowth, stat) >= 0) return 0f;
            foreach (var weight in hero.GrowthWeights) if (weight.Stat == stat && weight.Value > 0) return 1f;
            return .4f;
        }

        // An empty slot this find belongs in.
        static bool Suits(SoulMercenary hero, SoulItem item)
        {
            switch (item.Slot)
            {
                case SoulEquipSlot.Body: case SoulEquipSlot.Head: return true;
                case SoulEquipSlot.OffHand:
                    var main = hero.Equipped(SoulEquipSlot.MainHand);
                    string kind = item.Data.Kind ?? "";
                    if (kind == "마도서") return hero.Job == "마법사";
                    if (kind == "성물") return hero.Job == "성직자";
                    if (kind == "화살통") return main != null && main.WeaponTag == "bow";
                    bool shield = kind.Contains("방패") || kind.Contains("버클러");
                    return shield && (hero.Role == SoulRole.Tank || hero.Role == SoulRole.Melee) && !SoulDungeonSession.Backliner(hero);
                default: return false; // a weapon only ever replaces one of its kind
            }
        }

        // Worn instead of `current`, this would weigh it down (load over 100%: 무거움).
        static bool Burdened(SoulMercenary hero, SoulItem item, SoulItem current)
            => (hero.GearWeight - (current != null ? current.Weight : 0) + item.Weight) / SoulItemRules.CarryLimit(hero.Stats.Total(StatType.Strength)) > 1f;

        // The one of these a find is worth most to, and what it would replace (null: nobody needs it).
        public static SoulMercenary BestTaker(IEnumerable<SoulMercenary> heroes, SoulItem item, out SoulItem replace)
        {
            SoulMercenary best = null;
            replace = null;
            int bestGain = 0;
            foreach (var hero in heroes)
            {
                if (hero == null) continue;
                int gain = UpgradeGain(hero, item, out var old);
                if (gain > bestGain) { best = hero; bestGain = gain; replace = old; }
            }
            return best;
        }

        // Puts it on; returns what it replaced (the caller puts that back in the storehouse / the bag).
        public static List<SoulItem> Wear(SoulMercenary hero, SoulItem item, SoulItem replace, SoulStatRules rules)
        {
            var lost = Replaced(hero, item, replace);
            foreach (var old in lost) hero.Equipment.Remove(old);
            hero.Equipment.Add(item);
            float hp = hero.Hp;
            hero.Rebuild(rules);
            hero.Hp = Mathf.Min(hp, hero.Stats.Total(StatType.MaxHp));
            return lost;
        }

        // 자동 장착 (guild): the storehouse's best for this one, slot by slot, as long as something is an upgrade
        // (UpgradeGain — the same judgement as in the dungeon). What it replaces goes back to the storehouse.
        public SoulItem NextUpgrade(SoulMercenary hero, out SoulItem replace)
        {
            replace = null;
            SoulItem best = null;
            int bestGain = 0;
            foreach (var item in Inventory)
            {
                int gain = UpgradeGain(hero, item, out var old);
                if (gain > bestGain) { best = item; bestGain = gain; replace = old; }
            }
            return best;
        }

        public List<SoulItem> AutoEquip(SoulMercenary hero)
        {
            var worn = new List<SoulItem>();
            if (hero == null || !Roster.Contains(hero)) return worn;
            for (int guard = 0; guard < 12; guard++)
            {
                var item = NextUpgrade(hero, out var replace);
                if (item == null || !Equip(hero, item, replace)) break;
                worn.Add(item);
            }
            return worn;
        }

        // What a swap would change on this one, stat by stat (0 left out): two copies made the same way — as it is and
        // with the swap — so only the swap tells between them (a copy lacks what the village lays on top: the guild's
        // abilities, a blessing …); add the change to the mercenary's own numbers.
        public Dictionary<StatType, float> SwapChanges(SoulMercenary hero, SoulItem item, SoulItem replace = null)
        {
            var changes = new Dictionary<StatType, float>();
            var before = SoulSave.Clone(hero, Data);
            var after = SoulSave.Clone(hero, Data);
            if (before == null || after == null) return changes;
            var gone = new List<int>();
            foreach (var old in Replaced(hero, item, replace)) { int at = hero.Equipment.IndexOf(old); if (at >= 0) gone.Add(at); }
            gone.Sort(); gone.Reverse();
            foreach (int at in gone) if (at < after.Equipment.Count) after.Equipment.RemoveAt(at);
            after.Equipment.Add(item);
            before.Rebuild(Rules);
            after.Rebuild(Rules);
            foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
            {
                float change = after.Stats.Total(stat) - before.Stats.Total(stat);
                if (Mathf.Abs(change) >= .005f) changes[stat] = change;
            }
            return changes;
        }

        // The helmet's look on or off (like a pattern's lock): its stats count either way.
        public void ToggleHelmet(SoulMercenary hero)
        {
            if (hero == null) return;
            hero.ShowHelmet = !hero.ShowHelmet;
            hero.Rebuild(Rules);
            Changed();
        }

        public bool Equip(SoulMercenary hero, SoulItem item, SoulItem replace = null)
        {
            if (!Inventory.Contains(item) || !Roster.Contains(hero) || EquipBlock(hero, item) != null) return false;
            var lost = Replaced(hero, item, replace);
            foreach (var old in lost) hero.Equipment.Remove(old);
            Inventory.Remove(item);
            Inventory.AddRange(lost); // replaced, not destroyed: back to the storehouse
            hero.Equipment.Add(item);
            hero.Rebuild(Rules);
            Changed($"{hero.Name}: {item.Name} 장착" + (lost.Count > 0 ? $" — {string.Join(", ", lost.ConvertAll(e => e.Name))} 창고로" : ""));
            return true;
        }

        // ── shop ─────────────────────────────────────────────────

        public List<SoulItem> Stock() => ShopStock;

        // New stock every day: each template the shop level allows, at a rolled grade (level 1:
        // 평범한 only; level 2 some 좋은; level 3 some 정교한). Accessories come blank (enchant-only).
        public void RefreshStock()
        {
            ShopStock.Clear();
            int level = Level(SoulBuildingKind.Shop);
            foreach (var data in Data.ShopEquipment)
                if (data != null && data.ShopTier > 0 && data.ShopTier <= level) ShopStock.Add(new SoulItem(data, SoulItemRules.ShopGrade(level, random)));
            Changed();
        }

        public static int SellPrice(SoulItem item) => item.SellPrice;

        public bool Buy(SoulItem item)
        {
            if (!ShopStock.Contains(item) || StorageFull || !Spend(ItemPrice(item))) return false;
            ShopStock.Remove(item);
            Inventory.Add(item);
            Changed($"{item.Name} 구매 (-{ItemPrice(item)})");
            return true;
        }

        // 일괄 판매 (창고): all at once, one line in the log. What sells: what is in the storehouse.
        public int SellMany(IEnumerable<SoulItem> items)
        {
            if (Level(SoulBuildingKind.Shop) <= 0) return 0;
            int gold = 0, count = 0;
            foreach (var item in new List<SoulItem>(items))
            {
                if (!Inventory.Remove(item)) continue;
                gold += SellPrice(item); count++;
            }
            if (count == 0) return 0;
            Wallet.Add(eItem.Gold, gold);
            Changed($"장비 {count}개 판매 (+{gold})");
            return gold;
        }

        // Nobody in the company would put it on (UpgradeGain), and nothing makes it worth keeping for itself (a
        // unique piece, enchants, special options): what 일괄 판매 picks by itself.
        public bool Surplus(SoulItem item)
            => !item.Data.Unique && item.Enchants.Count == 0 && item.Specials.Count == 0 && Roster.TrueForAll(hero => UpgradeGain(hero, item, out _) <= 0);

        public bool Sell(SoulItem item)
        {
            if (Level(SoulBuildingKind.Shop) <= 0 || !Inventory.Remove(item)) return false;
            Wallet.Add(eItem.Gold, SellPrice(item));
            Changed($"{item.Name} 판매 (+{SellPrice(item)})");
            return true;
        }

        public bool UsePotion(SoulMercenary hero)
        {
            if (Potions <= 0 || hero.Wounds <= 0) return false;
            Potions--;
            hero.HealWounds(1);
            Changed($"{hero.Name}: 회복 포션 — 부상 {hero.Wounds}단계");
            return true;
        }

        // ── church ───────────────────────────────────────────────

        public int HealCost(SoulMercenary hero)
            => Mathf.RoundToInt(25 * hero.Wounds * (1 - .2f * Mathf.Max(0, Level(SoulBuildingKind.Church) - 1)) * (Event == SoulVillageEvent.Plague ? 2 : 1));

        // Everyone at home with a wound, at once: the sum of their costs.
        public int HealAllCost()
        {
            int cost = 0;
            foreach (var hero in Roster) if (hero.Wounds > 0) cost += HealCost(hero);
            return cost;
        }

        public bool HealAllAtChurch()
        {
            var hurt = Roster.FindAll(hero => hero.Wounds > 0);
            if (Level(SoulBuildingKind.Church) <= 0 || hurt.Count == 0 || !Spend(HealAllCost())) return false;
            foreach (var hero in hurt) hero.HealWounds(SoulMercenary.MaxWounds);
            Changed($"성당에서 {hurt.Count}명의 부상을 모두 치료");
            return true;
        }

        public bool HealAtChurch(SoulMercenary hero)
        {
            if (Level(SoulBuildingKind.Church) <= 0 || hero.Wounds <= 0 || !Spend(HealCost(hero))) return false;
            hero.HealWounds(SoulMercenary.MaxWounds);
            Changed($"{hero.Name}: 성당에서 부상 치료");
            return true;
        }

        public static readonly SoulBlessing[] Blessings =
        {
            new SoulBlessing { Id = "life", Name = "생명의 가호", Description = "다음 출정 동안 최대 HP +20", Cost = 80, ChurchLevel = 1,
                Bonuses = new[] { new SoulStatBonus { Stat = StatType.MaxHp, Value = 20 } } },
            new SoulBlessing { Id = "breath", Name = "숨결의 가호", Description = "다음 출정 동안 최대 스태미나 +30, 스태미나 재생 +1", Cost = 100, ChurchLevel = 2,
                Bonuses = new[] { new SoulStatBonus { Stat = StatType.MaxStamina, Value = 30 }, new SoulStatBonus { Stat = StatType.StaminaRegen, Value = 1 } } },
            new SoulBlessing { Id = "courage", Name = "용기의 가호", Description = "다음 출정 동안 정신력 +5 (공포에 강해짐)", Cost = 140, ChurchLevel = 3,
                Bonuses = new[] { new SoulStatBonus { Stat = StatType.Will, Value = 5 } } },
        };

        public bool Bless(SoulBlessing blessing)
        {
            if (Level(SoulBuildingKind.Church) < blessing.ChurchLevel || Blessing != null || !Spend(blessing.Cost)) return false;
            Blessing = blessing;
            Changed($"{blessing.Name}를 받았습니다 — 다음 출정에 적용");
            return true;
        }

        // ── training ground ──────────────────────────────────────

        public static readonly SoulTraining[] Trainings =
        {
            new SoulTraining { Id = "stamina", Name = "지구력 훈련", Description = "최대 스태미나 +10", BaseCost = 60, Level = 1, Stat = StatType.MaxStamina, Amount = 10, Minutes = 40 },
            new SoulTraining { Id = "strength", Name = "근력 단련", Description = "근력 +1", BaseCost = 90, Level = 1, Stat = StatType.Strength, Amount = 1, Minutes = 60 },
            new SoulTraining { Id = "vitality", Name = "체력 단련", Description = "체력 +1", BaseCost = 90, Level = 1, Stat = StatType.Vitality, Amount = 1, Minutes = 60 },
            new SoulTraining { Id = "learn", Name = "패턴 수련", Description = "새 패턴 셋 중 하나를 익힙니다", BaseCost = 200, Level = 1, LearnPattern = true, Minutes = 120 },
            new SoulTraining { Id = "agility", Name = "민첩 단련", Description = "민첩 +1", BaseCost = 90, Level = 2, Stat = StatType.Agility, Amount = 1, Minutes = 60 },
            new SoulTraining { Id = "magic", Name = "마력 명상", Description = "마력 +1", BaseCost = 90, Level = 2, Stat = StatType.Magic, Amount = 1, Minutes = 60 },
            new SoulTraining { Id = "will", Name = "정신 수양", Description = "정신력 +1", BaseCost = 110, Level = 3, Stat = StatType.Will, Amount = 1, Minutes = 90 },
        };

        // Hero id → the training it is doing now (one at a time).
        public readonly Dictionary<string, SoulDrill> Drills = new Dictionary<string, SoulDrill>();
        public SoulDrill DrillOf(SoulMercenary hero) => hero != null && Drills.TryGetValue(hero.Id, out var drill) ? drill : null;
        public bool IsTraining(SoulMercenary hero) => DrillOf(hero) != null;

        // Dearer the more it was done: base × (1 + n) × (1 + n/4) — 1, 2.5, 4.5, 7, 10 … times the base.
        public int TrainingCost(SoulMercenary hero, SoulTraining training)
        {
            int n = TimesTrained(hero, training);
            return Mathf.RoundToInt(training.BaseCost * (1 + n) * (1 + n * .25f) * TrainingCostScale);
        }

        // Longer the more it was done: +25% of the base time each time before.
        public float TrainingMinutes(SoulMercenary hero, SoulTraining training) => TrainingMinutes(training) * (1 + .25f * TimesTrained(hero, training));

        // How often one drill can be done by one mercenary (the guild's 훈련 한계 raises it).
        public const int BaseTrainingCap = 5, TrainingCapStep = 5;
        public int TrainingCap => BaseTrainingCap + TrainingCapStep * PerkLevel(SoulPerk.TrainingCap);

        public int TimesTrained(SoulMercenary hero, SoulTraining training)
            => TrainingCounts.TryGetValue(hero.Id + ":" + training.Id, out int count) ? count : 0;

        // A pattern the mercenary could learn: from the pool, compatible, not owned yet.
        List<SoulPatternData> Learnable(SoulMercenary hero)
        {
            var owned = hero.OwnedPatterns();
            var result = new List<SoulPatternData>();
            foreach (var pattern in Data.TrainingPatterns)
                if (pattern != null && pattern.Compatible(hero) && !owned.Contains(pattern)) result.Add(pattern);
            return result;
        }

        // Pattern training: the three offered to a mercenary (null: none waiting).
        public readonly Dictionary<string, List<SoulPatternData>> PatternOffers = new Dictionary<string, List<SoulPatternData>>();
        public List<SoulPatternData> PatternOffer(SoulMercenary hero) => PatternOffers.TryGetValue(hero.Id, out var offer) ? offer : null;

        public bool ChoosePattern(SoulMercenary hero, SoulPatternData pattern)
        {
            var offer = PatternOffer(hero);
            // bought at the training ground: no level-up pick is spent
            if (offer == null || !offer.Contains(pattern) || !pattern.Compatible(hero) || hero.AllPatterns().Contains(pattern)) return false;
            hero.LearnedPatterns.Add(pattern);
            hero.Rebuild(Rules);
            PatternOffers.Remove(hero.Id);
            Changed($"{hero.Name}: 새 패턴 '{pattern.Id}' 습득");
            return true;
        }

        public string TrainBlock(SoulMercenary hero, SoulTraining training)
        {
            if (IsTraining(hero)) return "훈련 중";
            if (Level(SoulBuildingKind.Training) < training.Level) return $"훈련소 {training.Level}단계 필요";
            if (!training.LearnPattern && TimesTrained(hero, training) >= TrainingCap) return $"훈련 한계 ({TrainingCap}회) — 길드 능력 '훈련 한계'로 늘어납니다";
            if (training.LearnPattern && Learnable(hero).Count == 0) return "익힐 수 있는 패턴이 없습니다";
            if (PatternOffer(hero) != null) return "고를 패턴이 남아 있습니다";
            if (Gold < TrainingCost(hero, training)) return "금화 부족";
            return null;
        }

        public bool Train(SoulMercenary hero, SoulTraining training, out string result)
        {
            result = TrainBlock(hero, training);
            if (result != null || !Spend(TrainingCost(hero, training))) return false;
            float minutes = TrainingMinutes(hero, training); // this time's length (before it counts)
            string key = hero.Id + ":" + training.Id;
            TrainingCounts[key] = TimesTrained(hero, training) + 1;
            Drills[hero.Id] = new SoulDrill { Training = training, EndsAt = Clock + minutes };
            result = $"{hero.Name}: {training.Name}";
            Changed(result);
            return true;
        }

        // ── auto training (자동 훈련) ──
        // A mercenary on auto training, idle at home, starts a drill by itself: the stat training worth most to it for
        // the gold (StatWorth ÷ cost — its tendencies first, the cheaper as the same drill grows dearer), never pattern
        // training (a choice is the player's), never past its cap, and never below AutoTrainReserve gold. Going down to
        // the dungeon pauses the drill; it goes on when the mercenary is back.
        public const int AutoTrainReserve = 300;
        public readonly HashSet<string> AutoTrain = new HashSet<string>();
        public bool AutoTraining(SoulMercenary hero) => hero != null && AutoTrain.Contains(hero.Id);

        public void ToggleAutoTrain(SoulMercenary hero)
        {
            if (hero == null) return;
            if (!AutoTrain.Remove(hero.Id)) AutoTrain.Add(hero.Id);
            AutoDrills();
            Changed();
        }

        public SoulTraining AutoPick(SoulMercenary hero)
        {
            SoulTraining best = null;
            float bestPrice = float.MaxValue;
            foreach (var training in Trainings)
            {
                if (training.LearnPattern || Level(SoulBuildingKind.Training) < training.Level || TimesTrained(hero, training) >= TrainingCap) continue;
                float worth = StatWorth(hero, training.Stat);
                if (worth < .5f) continue;
                float price = TrainingCost(hero, training) / worth;
                if (price < bestPrice) { bestPrice = price; best = training; }
            }
            return best;
        }

        void AutoDrills()
        {
            if (AutoTrain.Count == 0 || Level(SoulBuildingKind.Training) <= 0) return;
            foreach (var hero in Roster.ToArray())
            {
                if (!AutoTraining(hero) || IsTraining(hero) || PatternOffer(hero) != null) continue;
                var training = AutoPick(hero);
                if (training == null) continue;
                if (Gold - TrainingCost(hero, training) < AutoTrainReserve || TrainBlock(hero, training) != null) continue;
                Train(hero, training, out _);
            }
        }

        // Down to the dungeon: the drill stops where it is; back home, it goes on.
        void PauseDrill(SoulMercenary hero)
        {
            var drill = DrillOf(hero);
            if (drill != null && !drill.IsPaused) drill.Paused = drill.Left(Clock);
        }

        void ResumeDrill(SoulMercenary hero)
        {
            var drill = DrillOf(hero);
            if (drill == null || !drill.IsPaused) return;
            drill.EndsAt = Clock + drill.Paused;
            drill.Paused = -1;
        }

        // Trainings whose time is up take effect (called as the village clock runs).
        void FinishDrills()
        {
            if (Drills.Count == 0) return;
            foreach (var pair in new List<KeyValuePair<string, SoulDrill>>(Drills))
            {
                if (pair.Value.IsPaused)
                {
                    // away: it waits — unless the mercenary is gone for good
                    if (!Roster.Exists(h => h.Id == pair.Key) && !Away.Exists(t => t.Party.Exists(h => h.Id == pair.Key))) Drills.Remove(pair.Key);
                    continue;
                }
                if (Clock < pair.Value.EndsAt) continue;
                Drills.Remove(pair.Key);
                var hero = Roster.Find(h => h.Id == pair.Key);
                if (hero != null) Complete(hero, pair.Value.Training);
            }
        }

        void Complete(SoulMercenary hero, SoulTraining training)
        {
            string result;
            if (training.LearnPattern)
            {
                // three to choose from (kept until one is picked)
                var options = Learnable(hero);
                for (int i = options.Count - 1; i > 0; i--) { int j = random.Next(i + 1); var t = options[i]; options[i] = options[j]; options[j] = t; }
                PatternOffers[hero.Id] = options.GetRange(0, Mathf.Min(PatternChoices, options.Count));
                result = $"{hero.Name}: 패턴 수련";
            }
            else
            {
                bool twice = random.NextDouble() < DoubleTrainingChance; // 집중 훈련
                hero.TrainedStats[training.Stat] = (hero.TrainedStats.TryGetValue(training.Stat, out float value) ? value : 0) + training.Amount * (twice ? 2 : 1);
                result = $"{hero.Name}: {training.Name} ({training.Description})" + (twice ? " — 집중 훈련: 두 배" : "");
            }
            hero.Rebuild(Rules);
            RestoreAll(hero);
            Changed(result);
        }

        // ── expeditions ──────────────────────────────────────────

        public const string BlessingKey = "blessing";

        public bool Wiped => Roster.Count == 0 && Away.Count == 0; // nobody left: hire at the guild to go on

        // In the village everyone is rested (wounds are not: they need a healer).
        static void RestoreAll(SoulMercenary hero)
        {
            hero.Hp = hero.Stats.Total(StatType.MaxHp);
            hero.Mp = hero.Stats.Total(StatType.MaxMp);
            hero.Stamina = hero.Stats.Total(StatType.MaxStamina);
        }
    }
}
