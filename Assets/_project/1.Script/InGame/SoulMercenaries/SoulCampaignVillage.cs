using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // One line of the mercenary codex (추모비): everyone who ever served, the fallen kept with where they fell.
    [System.Serializable]
    public sealed class SoulMercenaryRecord
    {
        public string Id, Name, Race, Job;
        public int Level = 1, Grade = 9, Kills, Expeditions, JoinedAt, FellOnFloor;
        public SoulRecordStatus Status;
        public string Look; // appearance (JSON) as last seen: the codex draws the fallen from it
    }

    public enum SoulRecordStatus { Serving, Fallen, Dismissed }

    public enum SoulVillageEvent { None, Festival, Plague, Thief, Pilgrim }

    public sealed class SoulMerchantOffer
    {
        public SoulItem Item;
        public string Supply;
        public SoulActiveSkillData Book;
        public int Price;
        public string Name => Item != null ? Item.Name : Book != null ? $"스킬북 '{Book.SkillName}'" : SoulSupplies.Get(Supply)?.Name ?? Supply;
    }

    // Village life beyond the buildings' own services: renown, the codex of mercenaries, the soul altar, the
    // wandering merchant and what happens from day to day.
    public sealed partial class SoulCampaign
    {
        // ── renown ───────────────────────────────────────────────

        public int Renown;
        static readonly int[] renownFrom = { 0, 100, 300, 700 };
        static readonly string[] renownNames = { "무명", "알려진", "이름난", "전설의" };

        public int RenownTier
        {
            get
            {
                for (int i = renownFrom.Length - 1; i > 0; i--) if (Renown >= renownFrom[i]) return i;
                return 0;
            }
        }

        public string RenownName => renownNames[RenownTier];
        public static string RenownNameOf(int tier) => renownNames[Mathf.Clamp(tier, 0, renownNames.Length - 1)];
        public int NextRenown => RenownTier + 1 < renownFrom.Length ? renownFrom[RenownTier + 1] : -1;
        // Quest rewards: +15% a renown tier; −30% for the next return after the company was wiped out.
        public bool Disgraced;
        public float QuestScale => (1 + .15f * RenownTier) * (Disgraced ? .7f : 1f);
        public int QuestReward(SoulQuest quest) => quest.Kind == SoulQuestKind.Explore ? quest.Reward : Mathf.RoundToInt(quest.Reward * QuestScale);

        void GainRenown(int amount, List<string> report, string why)
        {
            if (amount <= 0) return;
            int tier = RenownTier;
            Renown += amount;
            report?.Add($"명성 +{amount} ({why})");
            if (RenownTier > tier) report?.Add(SoulUi.Colored($"용병단이 '{RenownName}' 용병단이 되었습니다!", SoulUi.Accent));
        }

        // The third level of any building wants a company people have heard of.
        public string UpgradeBlock(SoulBuildingKind kind)
        {
            // the third level wants 알려진, the fourth 이름난, the fifth 전설의
            int tier = Mathf.Min(Level(kind) - 1, renownFrom.Length - 1);
            if (tier >= 1 && RenownTier < tier) return $"명성 '{renownNames[tier]}' 필요 ({Renown}/{renownFrom[tier]})";
            return null;
        }

        // ── mercenary codex (추모비) ──────────────────────────────

        public readonly List<SoulMercenaryRecord> Records = new List<SoulMercenaryRecord>();

        public SoulMercenaryRecord Record(SoulMercenary hero)
        {
            var record = Records.Find(r => r.Id == hero.Id);
            if (record != null) return record;
            record = new SoulMercenaryRecord { Id = hero.Id, Name = hero.Name, Race = hero.Race.Id, Job = hero.Job, JoinedAt = Expeditions };
            Records.Add(record);
            UpdateRecord(hero);
            return record;
        }

        void UpdateRecord(SoulMercenary hero)
        {
            var record = Records.Find(r => r.Id == hero.Id);
            if (record == null) return;
            record.Level = hero.Level;
            record.Grade = hero.Grade;
            record.Look = JsonUtility.ToJson(hero.Stats.Appearance);
        }

        // ── soul altar (영혼 제단) ─────────────────────────────────

        public int AltarLevel => Level(SoulBuildingKind.SoulAltar);
        public static int ReleaseCost(SoulData soul) => 150 * Mathf.Max(1, soul.Grade);
        public int FuseCost(SoulData soul) => Mathf.RoundToInt(100 * Mathf.Max(1, soul.Grade) * (AltarLevel >= 3 ? .5f : 1f));
        public const int FuseCount = 3;

        public string AbsorbBlock(SoulMercenary hero, SoulData soul)
        {
            if (AltarLevel <= 0) return "영혼 제단을 지어야 합니다";
            if (!Vault.Contains(soul)) return "보관 중인 영혼이 아닙니다";
            if (!hero.HasFreeSoulSlot) return "영혼 슬롯이 없습니다 (10레벨마다 +1)";
            if (hero.CoreBlocked(soul)) return "종족이 핵심 패턴을 쓸 수 없습니다";
            return null;
        }

        public bool AbsorbAtAltar(SoulMercenary hero, SoulData soul)
        {
            if (AbsorbBlock(hero, soul) != null || !hero.Absorb(soul, Rules)) return false;
            Vault.Remove(soul);
            Changed($"{hero.Name}: {soul.OriginMonster}의 영혼 흡수");
            return true;
        }

        public bool Release(SoulMercenary hero, SoulData soul)
        {
            if (AltarLevel <= 0 || !hero.Souls.Contains(soul) || !Spend(ReleaseCost(soul))) return false;
            hero.ReleaseAtChurch(soul, Rules);
            Vault.Add(soul);
            Changed($"{hero.Name}: {soul.OriginMonster}의 영혼을 떼어 보관");
            return true;
        }

        public string FuseBlock(SoulData soul)
        {
            if (AltarLevel < 2) return "영혼 제단 2단계 필요";
            if (soul.Refined == null) return "더 강해질 수 없는 영혼";
            if (Vault.FindAll(s => s == soul).Count < FuseCount) return $"같은 영혼 {FuseCount}개 필요";
            if (Gold < FuseCost(soul)) return "금화 부족";
            return null;
        }

        public bool Fuse(SoulData soul)
        {
            if (FuseBlock(soul) != null) return false;
            Spend(FuseCost(soul));
            for (int i = 0; i < FuseCount; i++) Vault.Remove(soul);
            Vault.Add(soul.Refined);
            Changed($"영혼 합성: {soul.OriginMonster} ×{FuseCount} → {soul.Refined.OriginMonster} ({soul.Refined.Grade}등급)");
            return true;
        }

        // ── the wandering merchant and village events ────────────

        public readonly List<SoulMerchantOffer> MerchantStock = new List<SoulMerchantOffer>();
        public bool MerchantHere => MerchantStock.Count > 0;
        public SoulVillageEvent Event;
        public float PriceScale => Event == SoulVillageEvent.Festival ? .8f : 1f;
        public int ItemPrice(SoulItem item) => Mathf.RoundToInt(item.Price * PriceScale);

        public string EventText
        {
            get
            {
                switch (Event)
                {
                    case SoulVillageEvent.Festival: return "수확 축제 — 상점 20% 할인";
                    case SoulVillageEvent.Plague: return "역병 — 성당 치료비 2배 (만능약 1개를 나눠 받았습니다)";
                    case SoulVillageEvent.Thief: return "도둑이 들었습니다";
                    case SoulVillageEvent.Pilgrim: return "순례자 — 다음 출정에 축복을 무료로 받았습니다";
                    default: return null;
                }
            }
        }

        // A new day: the guild's hires and board, the shop's stock, maybe the merchant, maybe an event.
        void NewDay()
        {
            ClearDungeon();
            var report = new List<string>();
            VillageTurn(report);
            RefreshOffers();
            RefreshStock();
            RefreshBoard(); // new notices every morning
            Note(report);
            Changed($"{SoulClock.Day(Clock)}일차가 밝았습니다");
        }

        // Every day: maybe the merchant, maybe an event (one at a time).
        void VillageTurn(List<string> report)
        {
            MerchantStock.Clear();
            if (random.NextDouble() < .35 + .05 * RenownTier) RollMerchant();
            if (MerchantHere) report.Add("떠돌이 상인이 광장에 왔습니다");
            Event = SoulVillageEvent.None;
            double roll = random.NextDouble();
            if (roll < .15) Event = SoulVillageEvent.Festival;
            else if (roll < .25) Event = SoulVillageEvent.Plague;
            else if (roll < .35) Event = SoulVillageEvent.Thief;
            else if (roll < .45 && Blessing == null) Event = SoulVillageEvent.Pilgrim;
            switch (Event)
            {
                case SoulVillageEvent.Plague: AddSupply("panacea", 1); break;
                case SoulVillageEvent.Thief:
                    if (StorageLevel >= 2) { report.Add("도둑이 들었지만 튼튼한 창고가 막아냈습니다"); break; }
                    int stolen = Gold / 10;
                    Wallet.Spend(eItem.Gold, stolen);
                    report.Add($"도둑이 금화 {stolen}을 훔쳐 갔습니다 (창고 2단계면 막을 수 있습니다)");
                    break;
                case SoulVillageEvent.Pilgrim: Blessing = Blessings[0]; break;
            }
            if (EventText != null && Event != SoulVillageEvent.Thief) report.Add(EventText);
        }

        public void RollMerchant()
        {
            var rare = new[] { "panacea", "potion_greater", "scroll_purify", "scroll_gale" };
            for (int i = 0; i < 2; i++)
            {
                var supply = SoulSupplies.Get(rare[random.Next(rare.Length)]);
                MerchantStock.Add(new SoulMerchantOffer { Supply = supply.Id, Price = Mathf.RoundToInt(supply.Price * 1.3f) });
            }
            // the return scroll: rarely, and dear
            if (random.NextDouble() < .12) MerchantStock.Add(new SoulMerchantOffer { Supply = SoulSupplies.ReturnScroll, Price = SoulSupplies.Get(SoulSupplies.ReturnScroll).Price });
            var books = new List<SoulActiveSkillData>();
            if (Data.Dungeon != null) foreach (var book in Data.Dungeon.SkillBooks) if (book != null && book.BookTier <= Mathf.Min(2, 1 + RenownTier)) books.Add(book);
            if (books.Count > 0)
            {
                var book = books[random.Next(books.Count)];
                MerchantStock.Add(new SoulMerchantOffer { Book = book, Price = 250 * book.BookTier });
            }
            // one good piece of gear (made, not found: no special options)
            if (Data.Dungeon != null && Data.Dungeon.LootTable.Length > 0)
            {
                var data = Data.Dungeon.LootTable[random.Next(Data.Dungeon.LootTable.Length)];
                var item = new SoulItem(data, random.NextDouble() < .3 ? 4 : 3);
                MerchantStock.Add(new SoulMerchantOffer { Item = item, Price = Mathf.RoundToInt(item.Price * 1.2f) });
            }
        }

        public bool BuyFromMerchant(SoulMerchantOffer offer)
        {
            if (!MerchantStock.Contains(offer) || !Spend(offer.Price)) return false;
            MerchantStock.Remove(offer);
            if (offer.Item != null) Inventory.Add(offer.Item);
            else if (offer.Book != null) Books.Add(offer.Book);
            else AddSupply(offer.Supply, 1);
            Changed($"떠돌이 상인: {offer.Name} 구매 (-{offer.Price})");
            return true;
        }

        // ── the return: what the village makes of an expedition ──

        void AfterReturn(SoulDungeonSession session, List<string> report)
        {
            Books.AddRange(session.FoundBooks);
            if (session.FoundBooks.Count > 0) report.Add($"스킬북 {session.FoundBooks.Count}권 — 서고에 등록할 수 있습니다");
            foreach (var hero in session.Mercenaries)
            {
                var record = Record(hero);
                session.Kills.TryGetValue(hero, out int kills);
                record.Kills += kills;
                record.Expeditions++;
                UpdateRecord(hero);
                if (!hero.Alive && Permadeath) { record.Status = SoulRecordStatus.Fallen; record.FellOnFloor = session.Floor; }
            }
            int cleared = session.Finished ? session.Floor : session.Floor - 1;
            bool wiped = session.Defeated;
            GainRenown(10 * Mathf.Max(0, cleared) + 5 * session.ElitesDefeated + 25 * session.BossesDefeated, report, "출정");
            Disgraced = wiped;
            if (wiped) report.Add(SoulUi.Colored("전멸한 용병단 — 다음 의뢰 보상 −30%", SoulUi.Bad));
        }
    }
}
