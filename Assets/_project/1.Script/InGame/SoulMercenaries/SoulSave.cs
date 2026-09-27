using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SoulMercenaries
{
    // Saving the campaign: the village, the company and its gear go into the SoulCampaignData section of
    // UserDataManager (saved with every other section); gold, soul stones and supplies are ItemData's. Assets are
    // written by their asset name and found again through the village data's content index. An expedition in
    // progress is not saved: loading a game that was quit with parties away brings them home empty-handed
    // (they are saved on the roster; what they took down is gone). The village clock is saved.
    public static class SoulSave
    {
        public const int Version = 1;
        static SoulCampaignData Section => UserDataManager.Instance.Get<SoulCampaignData>();
        public static ItemData Wallet => UserDataManager.Instance.Get<ItemData>();
        // Before the save sections the campaign was a JSON file; it is read once, moved in, and renamed.
        static string LegacyPath => System.IO.Path.Combine(Application.persistentDataPath, "soul_mercenaries_save.json");

        [Serializable] public sealed class Pair { public string Key; public float Value; }
        [Serializable] public sealed class Item { public string Data; public int Grade, Rerolls; public bool Dropped; public List<SoulEnchant> Enchants = new List<SoulEnchant>(); public List<string> Specials = new List<string>(); }
        [Serializable] public sealed class Codex { public string Monster; public int Defeated, Kills, FirstFloor; }
        [Serializable]
        public sealed class Hero
        {
            public string Id, Name, Race, Job, Style;
            public bool ShowHelmet;
            public int Role, Level, Experience, PatternPicks, HighestFloorCleared, Wounds;
            public float WoundDamage;
            public SoulAppearancePatch Look;
            public List<Pair> BaseStats = new List<Pair>(), LevelStats = new List<Pair>(), TrainedStats = new List<Pair>(), MentalStats = new List<Pair>(), GrowthWeights = new List<Pair>();
            public List<string> Experiences = new List<string>(), Souls = new List<string>(), LearnedPatterns = new List<string>(), StartingPatterns = new List<string>();
            public List<string> StartingActives = new List<string>(), LearnedActives = new List<string>(), StartingPassives = new List<string>(), Locked = new List<string>();
            public List<string> WeakGrowth = new List<string>();
            public List<Pair> SkillLevels = new List<Pair>(); // its own level of each skill (the library's books raise them)
            public List<Item> Equipment = new List<Item>();
            public List<Codex> Codex = new List<Codex>();
        }
        [Serializable] public sealed class Offer { public string Template, Name, Recruit; public int Price, GearGrade; }
        [Serializable] public sealed class Quest { public int Kind; public string Title, TargetId; public int Goal, Progress, Reward, Grade, Floor, Bonus, BonusCount; public string BonusId; }
        [Serializable] public sealed class Merchant { public Item Item; public string Supply, Book; public int Price; }
        [Serializable]
        public sealed class Squad
        {
            public List<string> Members = new List<string>(), Belt = new List<string>(), Pouch = new List<string>(); // Belt / Pouch: legacy sockets
            public List<SoulCarry> Carry = new List<SoulCarry>();
            public int Target = 2, Mode = (int)SoulExploreMode.Hunt;
        }
        [Serializable]
        public sealed class Game
        {
            public int Version, Expeditions, Renown, Event, Hired;
            public int PartyBase; // 1: saved since the company starts with one party (older saves started with two)
            public int Gold, Stones; // legacy file only — now in ItemData
            public bool Disgraced, InExpedition;
            public float Clock;
            public int WorldSeed;
            public List<Pair> EnteredOn = new List<Pair>();
            public List<string> PatternOffers = new List<string>(); // "heroId|pattern|pattern|pattern"
            public string Blessing;
            public List<Pair> Levels = new List<Pair>(), Library = new List<Pair>(), TrainingCounts = new List<Pair>();
            public List<string> AutoTrain = new List<string>(); // mercenaries on 자동 훈련
            public List<Pair> Drills = new List<Pair>(); // "heroId|trainingId" → the clock it ends at
            public List<Pair> Perks = new List<Pair>();  // guild abilities: name → level
            public List<Pair> Supplies = new List<Pair>(); // legacy file only — now in ItemData
            public List<string> Vault = new List<string>();
            public List<string> BeltPlan = new List<string>(), PouchPlan = new List<string>(), Party = new List<string>(); // legacy: one party
            public List<Squad> Parties = new List<Squad>();
            public List<string> Books = new List<string>(), Log = new List<string>(), Fallen = new List<string>();
            public int Difficulty; // SoulDifficulty (0 보통 — saves from before had no choice)
            public List<Item> Inventory = new List<Item>(), ShopStock = new List<Item>();
            public List<Hero> Roster = new List<Hero>();
            public List<Offer> Offers = new List<Offer>();
            public List<Quest> Board = new List<Quest>(), Active = new List<Quest>();
            public List<SoulMercenaryRecord> Records = new List<SoulMercenaryRecord>();
            public List<Merchant> Merchant = new List<Merchant>();
            public bool CarryTaken; // parties' provisions are out of the store (an older save kept them as a plan)
            public List<SoulTripReport> Reports = new List<SoulTripReport>();
        }

        // ── write ────────────────────────────────────────────────

        static string Key(UnityEngine.Object asset) => asset != null ? asset.name : "";
        static List<string> Keys<T>(IEnumerable<T> assets) where T : UnityEngine.Object { var list = new List<string>(); foreach (var a in assets) if (a != null) list.Add(a.name); return list; }
        static List<Pair> LevelPairs(IDictionary<string, int> levels) { var list = new List<Pair>(); foreach (var e in levels) list.Add(new Pair { Key = e.Key, Value = e.Value }); return list; }
        static List<Pair> Pairs(IDictionary<StatType, float> stats) { var list = new List<Pair>(); foreach (var e in stats) list.Add(new Pair { Key = e.Key.ToString(), Value = e.Value }); return list; }

        public static Item Write(SoulItem item)
        {
            var dto = new Item { Data = Key(item.Data), Grade = item.Grade, Dropped = item.Dropped, Rerolls = item.Rerolls };
            foreach (var e in item.Enchants) dto.Enchants.Add(new SoulEnchant { Option = e.Option, Tier = e.Tier, Locked = e.Locked });
            dto.Specials.AddRange(item.Specials);
            return dto;
        }

        public static Hero Write(SoulMercenary hero)
        {
            var dto = new Hero
            {
                Id = hero.Id, Name = hero.Name, Race = Key(hero.Race), Job = hero.Job, Role = (int)hero.Role, Look = hero.Look, Style = hero.Style, ShowHelmet = hero.ShowHelmet,
                Level = hero.Level, Experience = hero.Experience, PatternPicks = hero.PatternPicks, HighestFloorCleared = hero.HighestFloorCleared,
                Wounds = hero.Wounds, WoundDamage = hero.WoundDamage,
                BaseStats = Pairs(hero.BaseStats), LevelStats = Pairs(hero.LevelStats), TrainedStats = Pairs(hero.TrainedStats), MentalStats = Pairs(hero.MentalStats),
                SkillLevels = LevelPairs(hero.SkillLevels),
                Souls = Keys(hero.Souls), LearnedPatterns = Keys(hero.LearnedPatterns), StartingPatterns = Keys(hero.StartingPatterns),
                StartingActives = Keys(hero.StartingActives), LearnedActives = Keys(hero.LearnedActives), StartingPassives = Keys(hero.StartingPassives),
            };
            foreach (var g in hero.GrowthWeights) dto.GrowthWeights.Add(new Pair { Key = g.Stat.ToString(), Value = g.Value });
            foreach (var stat in hero.WeakGrowth) dto.WeakGrowth.Add(stat.ToString());
            dto.Experiences.AddRange(hero.Experiences);
            foreach (var locked in hero.Locked) if (locked != null) dto.Locked.Add(locked.name);
            foreach (var item in hero.Equipment) dto.Equipment.Add(Write(item));
            foreach (var entry in hero.Codex.Values) dto.Codex.Add(new Codex { Monster = Key(entry.Monster), Defeated = entry.Defeated, Kills = entry.Kills, FirstFloor = entry.FirstFloor });
            return dto;
        }

        public static Game Write(SoulCampaign campaign)
        {
            var game = new Game
            {
                Version = Version, PartyBase = 1, Expeditions = campaign.Expeditions, Renown = campaign.Renown, Difficulty = (int)campaign.Difficulty,
                Event = (int)campaign.Event, Hired = campaign.HiredCount, Disgraced = campaign.Disgraced, InExpedition = campaign.InExpedition, Clock = campaign.Clock, WorldSeed = campaign.WorldSeed,
                Blessing = campaign.Blessing?.Id,
                Vault = Keys(campaign.Vault), Books = Keys(campaign.Books), Log = new List<string>(campaign.Log), Fallen = new List<string>(campaign.Fallen),
                Records = new List<SoulMercenaryRecord>(campaign.Records),
                CarryTaken = true, Reports = new List<SoulTripReport>(campaign.Reports),
            };
            foreach (var e in campaign.Levels) game.Levels.Add(new Pair { Key = e.Key.ToString(), Value = e.Value });
            foreach (var e in campaign.Library) game.Library.Add(new Pair { Key = Key(e.Key), Value = e.Value });
            foreach (var e in campaign.TrainingCounts) game.TrainingCounts.Add(new Pair { Key = e.Key, Value = e.Value });
            game.AutoTrain.AddRange(campaign.AutoTrain);
            foreach (var e in campaign.Perks) game.Perks.Add(new Pair { Key = e.Key.ToString(), Value = e.Value });
            foreach (var e in campaign.Drills)
                game.Drills.Add(e.Value.IsPaused ? new Pair { Key = e.Key + "|" + e.Value.Training.Id + "|p", Value = e.Value.Paused } // paused: the minutes left
                    : new Pair { Key = e.Key + "|" + e.Value.Training.Id, Value = e.Value.EndsAt });
            foreach (var e in campaign.EnteredOn) game.EnteredOn.Add(new Pair { Key = e.Key, Value = e.Value });
            foreach (var e in campaign.PatternOffers) game.PatternOffers.Add(e.Key + "|" + string.Join("|", Keys(e.Value)));
            foreach (var item in campaign.Inventory) game.Inventory.Add(Write(item));
            foreach (var item in campaign.ShopStock) game.ShopStock.Add(Write(item));
            foreach (var hero in campaign.Roster) game.Roster.Add(Write(hero));
            foreach (var trip in campaign.Away) foreach (var hero in trip.Party) game.Roster.Add(Write(hero)); // home again on a reload
            foreach (var party in campaign.Parties)
                game.Parties.Add(new Squad
                {
                    Members = party.Members.ConvertAll(hero => hero.Id), Carry = party.Carry.ConvertAll(c => new SoulCarry { Id = c.Id, Count = c.Count }),
                    Target = party.TargetFloor, Mode = (int)party.TargetMode,
                });
            foreach (var offer in campaign.Offers) game.Offers.Add(new Offer { Template = Key(offer.Template), Name = offer.Name, Recruit = offer.Recruit != null ? offer.Recruit.Id : null, Price = offer.Price, GearGrade = offer.GearGrade });
            foreach (var quest in campaign.Board) game.Board.Add(Write(quest));
            foreach (var quest in campaign.Active) game.Active.Add(Write(quest));
            foreach (var offer in campaign.MerchantStock)
                game.Merchant.Add(new Merchant { Item = offer.Item != null ? Write(offer.Item) : new Item(), Supply = offer.Supply, Book = Key(offer.Book), Price = offer.Price });
            return game;
        }

        static Quest Write(SoulQuest quest)
            => new Quest { Kind = (int)quest.Kind, Title = quest.Title, TargetId = quest.TargetId, Goal = quest.Goal, Progress = quest.Progress, Reward = quest.Reward,
                Grade = quest.Grade, Floor = quest.Floor, Bonus = (int)quest.Bonus, BonusId = quest.BonusId, BonusCount = quest.BonusCount };

        // immediate: written now, not next frame (the app is going to the background or quitting)
        public static bool Save(SoulCampaign campaign, bool immediate = false)
        {
            if (campaign == null) return false;
            Section.Json = JsonUtility.ToJson(Write(campaign));
            if (immediate) UserDataManager.Instance.SaveAll();
            else UserDataManager.Instance.RequestSave();
            return true;
        }

        public static void Delete()
        {
            Section.Json = null;
            UserDataManager.Instance.RequestSave();
        }

        // ── read ─────────────────────────────────────────────────

        sealed class Index
        {
            readonly Dictionary<string, UnityEngine.Object> byName = new Dictionary<string, UnityEngine.Object>();
            public Index(SoulVillageData data)
            {
                void Add<T>(IEnumerable<T> assets) where T : UnityEngine.Object { if (assets == null) return; foreach (var a in assets) if (a != null) byName[typeof(T).Name + ":" + a.name] = a; }
                Add(data.AllEquipment); Add(data.AllSkills); Add(data.AllPatterns); Add(data.AllPassives); Add(data.AllSouls);
                Add(data.AllRaces); Add(data.AllMercenaries); Add(data.AllMonsters);
                // whatever the village refers to directly counts too
                Add(data.ShopEquipment); Add(data.HireTemplates); Add(data.PriestTemplates); Add(data.StartingRoster); Add(data.TrainingPatterns);
                if (data.Dungeon != null) { Add(data.Dungeon.LootTable); Add(data.Dungeon.BossLoot); Add(data.Dungeon.SkillBooks); Add(data.Dungeon.LevelPatternPool); }
            }
            public T Get<T>(string name) where T : UnityEngine.Object
                => !string.IsNullOrEmpty(name) && byName.TryGetValue(typeof(T).Name + ":" + name, out var asset) ? asset as T : null;
            public List<T> All<T>(IEnumerable<string> names) where T : UnityEngine.Object
            {
                var list = new List<T>();
                if (names != null) foreach (string name in names) { var asset = Get<T>(name); if (asset != null) list.Add(asset); }
                return list;
            }
        }

        static SoulItem Read(Item dto, Index index)
        {
            var data = index.Get<SoulEquipmentData>(dto?.Data);
            if (data == null) return null;
            var item = new SoulItem(data, dto.Grade) { Dropped = dto.Dropped, Rerolls = dto.Rerolls };
            foreach (var e in dto.Enchants) if (SoulItemRules.Enchant(e.Option) != null) item.Enchants.Add(new SoulEnchant { Option = e.Option, Tier = Mathf.Clamp(e.Tier, 0, 3), Locked = e.Locked });
            foreach (string id in dto.Specials) if (SoulItemRules.Special(id) != null) item.Specials.Add(id);
            return item;
        }

        static Dictionary<StatType, float> Stats(List<Pair> pairs)
        {
            var stats = new Dictionary<StatType, float>();
            foreach (var pair in pairs) if (Enum.TryParse(pair.Key, out StatType stat)) stats[stat] = pair.Value;
            return stats;
        }

        static SoulMercenary Read(Hero dto, Index index, SoulStatRules rules)
        {
            var race = index.Get<SoulRaceData>(dto.Race);
            if (race == null) return null;
            var hero = new SoulMercenary(dto.Id, dto.Name, race, Stats(dto.BaseStats), rules) { Look = dto.Look, Job = dto.Job, Role = (SoulRole)dto.Role, Style = string.IsNullOrEmpty(dto.Style) ? null : dto.Style, ShowHelmet = dto.ShowHelmet };
            foreach (var e in Stats(dto.LevelStats)) hero.LevelStats[e.Key] = e.Value;
            foreach (var e in Stats(dto.TrainedStats)) hero.TrainedStats[e.Key] = e.Value;
            if (dto.SkillLevels != null) foreach (var pair in dto.SkillLevels) if (!string.IsNullOrEmpty(pair.Key)) hero.SkillLevels[pair.Key] = Mathf.Clamp((int)pair.Value, 1, SoulCampaign.MaxSkillLevel);
            foreach (var e in Stats(dto.MentalStats)) hero.MentalStats[e.Key] = e.Value;
            var growth = new List<SoulStatBonus>();
            foreach (var e in Stats(dto.GrowthWeights)) growth.Add(new SoulStatBonus { Stat = e.Key, Value = e.Value });
            hero.GrowthWeights = growth.ToArray();
            var weak = new List<StatType>();
            foreach (string name in dto.WeakGrowth) if (Enum.TryParse(name, out StatType stat)) weak.Add(stat);
            hero.WeakGrowth = weak.ToArray();
            foreach (string e in dto.Experiences) hero.Experiences.Add(e);
            hero.Souls.AddRange(index.All<SoulData>(dto.Souls));
            hero.LearnedPatterns.AddRange(index.All<SoulPatternData>(dto.LearnedPatterns));
            hero.StartingPatterns.AddRange(index.All<SoulPatternData>(dto.StartingPatterns));
            hero.StartingActives.AddRange(index.All<SoulActiveSkillData>(dto.StartingActives));
            hero.LearnedActives.AddRange(index.All<SoulActiveSkillData>(dto.LearnedActives));
            hero.StartingPassives.AddRange(index.All<SoulPassiveSkillData>(dto.StartingPassives));
            foreach (var item in dto.Equipment) { var loaded = Read(item, index); if (loaded != null) hero.Equipment.Add(loaded); }
            foreach (string name in dto.Locked)
            {
                UnityEngine.Object locked = index.Get<SoulPatternData>(name);
                if (locked == null) locked = index.Get<SoulActiveSkillData>(name);
                if (locked != null) hero.Locked.Add(locked);
            }
            foreach (var entry in dto.Codex)
            {
                var monster = index.Get<SoulMonsterData>(entry.Monster);
                if (monster != null) hero.Codex[monster.Id] = new SoulCodexEntry { Monster = monster, Defeated = entry.Defeated, Kills = entry.Kills, FirstFloor = entry.FirstFloor };
            }
            hero.RestoreProgress(dto.Level, dto.Experience, dto.PatternPicks, dto.Wounds, dto.WoundDamage);
            hero.HighestFloorCleared = dto.HighestFloorCleared;
            hero.Rebuild(rules);
            hero.Hp = hero.Stats.Total(StatType.MaxHp);
            hero.Mp = hero.Stats.Total(StatType.MaxMp);
            hero.Stamina = hero.Stats.Total(StatType.MaxStamina);
            return hero;
        }

        static SoulQuest Read(Quest dto)
            => new SoulQuest { Kind = (SoulQuestKind)dto.Kind, Title = dto.Title, TargetId = dto.TargetId, Goal = dto.Goal, Progress = dto.Progress, Reward = dto.Reward,
                Grade = Mathf.Max(1, dto.Grade), Floor = Mathf.Max(1, dto.Floor), Bonus = (SoulQuestBonus)dto.Bonus, BonusId = dto.BonusId, BonusCount = dto.BonusCount };

        public static SoulCampaign Load(SoulVillageData data)
        {
            if (data == null) return null;
            string json = Section.Json;
            bool legacy = string.IsNullOrEmpty(json) && File.Exists(LegacyPath);
            try
            {
                if (legacy) json = File.ReadAllText(LegacyPath);
                if (string.IsNullOrEmpty(json)) return null;
                var game = JsonUtility.FromJson<Game>(json);
                if (game == null || game.Version != Version) return null;
                var campaign = Read(game, data, Wallet, legacy);
                if (legacy)
                {
                    Save(campaign);
                    File.Move(LegacyPath, LegacyPath + ".migrated");
                }
                return campaign;
            }
            catch (Exception e) { Debug.LogWarning("Soul Mercenaries load failed: " + e.Message); return null; }
        }

        // legacy: an old file, whose gold / stones / supplies still have to go into the wallet
        public static SoulCampaign Read(Game game, SoulVillageData data, ItemData wallet = null, bool legacy = false)
        {
            var index = new Index(data);
            var campaign = SoulCampaign.Empty(data, wallet);
            if (legacy || wallet == null)
            {
                campaign.SetAmount(eItem.Gold, game.Gold);
                foreach (var supply in SoulSupplies.All) campaign.SetAmount(supply.Item, 0);
                campaign.SetAmount(eItem.SoulStone, game.Stones); // a supply now: after the reset
                foreach (var pair in game.Supplies) { var def = SoulSupplies.Get(pair.Key); if (def != null) campaign.SetAmount(def.Item, (int)pair.Value); }
            }
            campaign.Expeditions = game.Expeditions; campaign.Renown = game.Renown;
            campaign.Difficulty = (SoulDifficulty)Mathf.Clamp(game.Difficulty, 0, (int)SoulDifficulty.Extreme);
            campaign.Clock = game.Clock > 0 ? game.Clock : SoulClock.Start;
            campaign.WorldSeed = game.WorldSeed != 0 ? game.WorldSeed : System.Environment.TickCount & int.MaxValue;
            foreach (var pair in game.EnteredOn) campaign.EnteredOn[pair.Key] = (int)pair.Value;
            foreach (string line in game.PatternOffers)
            {
                var parts = line.Split('|');
                var offer = index.All<SoulPatternData>(new List<string>(parts).GetRange(1, parts.Length - 1));
                if (parts.Length > 1 && offer.Count > 0) campaign.PatternOffers[parts[0]] = offer;
            }
            campaign.Event = (SoulVillageEvent)game.Event; campaign.Disgraced = game.Disgraced; campaign.HiredCount = game.Hired;
            campaign.Blessing = Array.Find(SoulCampaign.Blessings, b => b.Id == game.Blessing);
            foreach (var pair in game.Levels) if (Enum.TryParse(pair.Key, out SoulBuildingKind kind)) campaign.Levels[kind] = (int)pair.Value;
            foreach (var building in data.Buildings) if (!campaign.Levels.ContainsKey(building.Kind)) campaign.Levels[building.Kind] = building.StartLevel;
            foreach (var pair in game.TrainingCounts) campaign.TrainingCounts[pair.Key] = (int)pair.Value;
            if (game.AutoTrain != null) foreach (string id in game.AutoTrain) campaign.AutoTrain.Add(id);
            foreach (var pair in game.Perks)
                if (Enum.TryParse(pair.Key, out SoulPerk perk) && SoulCampaign.PerkDef(perk) != null) campaign.Perks[perk] = Mathf.Clamp((int)pair.Value, 0, SoulCampaign.PerkDef(perk).MaxLevel);
            if (game.PartyBase == 0) // an older save: it started with two parties, so its 파티 편성 counts one more now
                campaign.Perks[SoulPerk.PartyCount] = Mathf.Min(campaign.PerkLevel(SoulPerk.PartyCount) + 1, SoulCampaign.PerkDef(SoulPerk.PartyCount).MaxLevel);
            foreach (var pair in game.Drills)
            {
                var parts = pair.Key.Split('|');
                var training = parts.Length >= 2 ? Array.Find(SoulCampaign.Trainings, t => t.Id == parts[1]) : null;
                if (training == null) continue;
                campaign.Drills[parts[0]] = parts.Length == 3 ? new SoulDrill { Training = training, Paused = pair.Value } : new SoulDrill { Training = training, EndsAt = pair.Value };
            }
            foreach (var pair in game.Library) { var skill = index.Get<SoulActiveSkillData>(pair.Key); if (skill != null) campaign.Library[skill] = (int)pair.Value; }
            campaign.Vault.AddRange(index.All<SoulData>(game.Vault));
            campaign.Books.AddRange(index.All<SoulActiveSkillData>(game.Books));
            campaign.Log.AddRange(game.Log); campaign.Fallen.AddRange(game.Fallen); campaign.Records.AddRange(game.Records);
            foreach (var dto in game.Inventory) { var item = Read(dto, index); if (item != null) campaign.Inventory.Add(item); }
            foreach (var dto in game.ShopStock) { var item = Read(dto, index); if (item != null) campaign.ShopStock.Add(item); }
            foreach (var dto in game.Roster) { var hero = Read(dto, index, data.StatRules); if (hero != null) campaign.Roster.Add(hero); }
            foreach (var hero in campaign.Roster)
            {
                // who it is on the roster (an older save kept a concept id: found again by name and job)
                hero.Recruit = FindRecruit(data, hero.Style, hero.Name, hero.Job);
                hero.Style = hero.Recruit != null ? hero.Recruit.Id : null;
                CatchUpSkills(hero, data);
            }
            // parties (an older save had one party and one belt / pouch: they become party 1); only the open ones —
            // members of a party the guild has not opened (an older save had five) wait unassigned
            campaign.SyncParties();
            var squads = game.Parties.Count > 0 ? game.Parties : new List<Squad> { new Squad { Members = game.Party, Belt = game.BeltPlan, Pouch = game.PouchPlan } };
            for (int i = 0; i < squads.Count && i < campaign.Parties.Count; i++)
            {
                var party = campaign.Parties[i];
                party.TargetFloor = Mathf.Clamp(squads[i].Target, 1, SoulDungeonSession.FinalFloor);
                party.TargetMode = squads[i].Mode == (int)SoulExploreMode.Farm ? SoulExploreMode.Farm : SoulExploreMode.Hunt;
                // provisions; an older save had sockets (a full stack each: JsonUtility keeps an empty one as "")
                var carry = new List<SoulCarry>(squads[i].Carry);
                foreach (string id in squads[i].Belt) Socket(carry, id);
                foreach (string id in squads[i].Pouch) Socket(carry, id);
                if (game.CarryTaken) party.Carry.AddRange(carry);
                else campaign.SetCarry(party, carry); // an older plan: out of the store now (what there is)
                foreach (string id in squads[i].Members)
                {
                    var hero = campaign.Roster.Find(h => h.Id == id);
                    if (hero != null && campaign.PartyOf(hero) == null && party.Members.Count < campaign.PartySize) party.Members.Add(hero);
                }
            }
            foreach (var dto in game.Offers)
            {
                var template = index.Get<SoulMercenaryData>(dto.Template);
                if (template == null) continue;
                var recruit = FindRecruit(data, dto.Recruit, dto.Name, template.Job);
                campaign.Offers.Add(recruit != null ? SoulHireOffer.Of(recruit, dto.Price, Mathf.Clamp(dto.GearGrade, 1, 4))
                    : new SoulHireOffer { Template = template, Name = dto.Name, Price = dto.Price, GearGrade = Mathf.Clamp(dto.GearGrade, 1, 3) });
            }
            foreach (var dto in game.Board) campaign.Board.Add(Read(dto));
            foreach (var dto in game.Active) campaign.Active.Add(Read(dto));
            foreach (var dto in game.Merchant)
                campaign.MerchantStock.Add(new SoulMerchantOffer { Item = Read(dto.Item, index), Supply = string.IsNullOrEmpty(dto.Supply) ? null : dto.Supply, Book = index.Get<SoulActiveSkillData>(dto.Book), Price = dto.Price });
            campaign.MerchantStock.RemoveAll(o => o.Item == null && o.Book == null && o.Supply == null);
            campaign.MoveShelfOntoMercenaries(); // an older save's shared shelf levels onto the mercenaries
            campaign.ApplyGuildStats();
            // older saves left every socket "automatic" (all empty)
            if (!game.CarryTaken && !campaign.Parties.Exists(p => p.Carry.Count > 0)) campaign.AutoFill(campaign.Parties[0]);
            // a list from when more was allowed: trimmed to the kinds and counts a party may take now (the rest back to the store)
            foreach (var party in campaign.Parties)
                if (!campaign.IsAway(party) && (party.Carry.Count > campaign.CarryKinds || party.Carry.Exists(c => c.Count > campaign.CarryLimit)))
                    campaign.SetCarry(party, new List<SoulCarry>(party.Carry));
            campaign.Reports.AddRange(game.Reports);
            // quit in the dungeon: nobody dies, but what was taken down (and anything found) is gone
            if (game.InExpedition) campaign.Log.Insert(0, "지난 출정은 끝까지 기록되지 않았습니다 — 가져간 소모품은 잃었습니다");
            return campaign;
        }

        // A skill a job's template gained since the save (the mage's attack magic, given back) joins those who have
        // the job; nothing is taken away.
        static SoulRecruitData FindRecruit(SoulVillageData data, string id, string name, string job)
        {
            if (data.Recruits == null) return null;
            if (!string.IsNullOrEmpty(id))
                foreach (var recruit in data.Recruits) if (recruit != null && recruit.Id == id) return recruit;
            foreach (var recruit in data.Recruits) if (recruit != null && recruit.DisplayName == name && recruit.Job == job) return recruit;
            return null;
        }

        static void CatchUpSkills(SoulMercenary hero, SoulVillageData data)
        {
            // a pattern that became a passive (명상): the passive in its place
            bool turned = false;
            foreach (var list in new[] { hero.StartingPatterns, hero.LearnedPatterns })
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var passive = list[i] != null ? list[i].NowPassive : null;
                    if (passive == null) continue;
                    if (!hero.StartingPassives.Contains(passive)) hero.StartingPassives.Add(passive);
                    list.RemoveAt(i);
                    turned = true;
                }
            if (turned) hero.Rebuild(data.StatRules);
            if (SoulHireStyles.CatchUp(hero)) hero.Rebuild(data.StatRules); // a trait its recruit has now
            SoulMercenaryData template = null;
            foreach (var list in new[] { data.HireTemplates, data.StartingRoster, data.PriestTemplates })
                if (template == null && list != null) template = System.Array.Find(list, t => t != null && t.Job == hero.Job);
            if (template == null) return;
            if (hero.WeakGrowth.Length == 0 && template.WeakGrowth != null) hero.WeakGrowth = (StatType[])template.WeakGrowth.Clone(); // 비성향 came later
            // the job's own passives and patterns given since (명상, 지팡이의 마력; 베기 · 휘두르기 · 할퀴기 moved from race to job)
            bool gained = false;
            if (template.Patterns != null)
                foreach (var pattern in template.Patterns)
                    if (pattern != null && pattern.NowPassive == null && !hero.StartingPatterns.Contains(pattern) && !hero.LearnedPatterns.Contains(pattern)) { hero.StartingPatterns.Add(pattern); gained = true; }
            if (template.Passives != null)
                foreach (var passive in template.Passives)
                    if (passive != null && !hero.StartingPassives.Contains(passive)) { hero.StartingPassives.Add(passive); gained = true; }
            if (gained) hero.Rebuild(data.StatRules);
            if (hero.Recruit != null || template.ActiveSkills == null) return; // someone on the roster has its own kit
            bool added = false;
            foreach (var skill in template.ActiveSkills)
                if (skill != null && !hero.StartingActives.Contains(skill) && !hero.AllActiveSkills().Contains(skill)) { hero.StartingActives.Add(skill); added = true; }
            if (added) hero.Rebuild(data.StatRules);
        }

        static void Socket(List<SoulCarry> carry, string id)
        {
            var def = SoulSupplies.Get(id);
            if (def == null || !SoulSupplies.Packed(id)) return;
            var entry = carry.Find(c => c.Id == id);
            if (entry == null) carry.Add(new SoulCarry { Id = id, Count = Mathf.Min(def.MaxStack, SoulCampaign.CarryPerKind) });
            else entry.Count = Mathf.Min(SoulCampaign.CarryPerKind, entry.Count + def.MaxStack);
        }

        // A deep copy of a mercenary (sparring fights on copies, never on the real company).
        public static SoulMercenary Clone(SoulMercenary hero, SoulVillageData data)
        {
            var copy = Read(Write(hero), new Index(data), data.StatRules);
            if (copy == null) return null;
            foreach (var entry in hero.SkillLevels) copy.SkillLevels[entry.Key] = entry.Value;
            return copy;
        }
    }
}
