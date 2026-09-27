using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // New kinds go at the end: saves keep them as numbers.
    public enum SoulQuestKind { Hunt, Elite, Boss, Floor, Souls, Chests, Hidden, Explore }
    public enum SoulQuestBonus { None, Supply, SoulStone, Equipment, SkillBook, Soul }

    // A notice on the village board (게시판): a grade (D … S), a target on one floor, a reward — gold and one
    // thing more (provisions, soul stones, a piece of gear, a skill book or a soul).
    public sealed class SoulQuest
    {
        public SoulQuestKind Kind;
        public string Title, TargetId;
        public int Goal, Progress, Reward;
        public int Grade = 1, Floor = 1;
        public SoulQuestBonus Bonus;
        public string BonusId;
        public int BonusCount;
        public bool Done => Progress >= Goal;

        public static readonly string[] GradeNames = { "D", "C", "B", "A", "S" };
        public string GradeName => GradeNames[Mathf.Clamp(Grade, 1, GradeNames.Length) - 1];
    }

    public sealed partial class SoulCampaign
    {
        public readonly List<SoulQuest> Board = new List<SoulQuest>();
        public readonly List<SoulQuest> Active = new List<SoulQuest>();

        // Board level: 1 → 4 notices, 1 taken at a time, up to B; 2 → 6, 2, A; 3 → 8, 3, S.
        public int BoardLevel => Mathf.Max(1, Level(SoulBuildingKind.Board));
        public int QuestSlots => BoardLevel;
        int BoardSize => 2 + 2 * BoardLevel;
        int TopGrade => 2 + BoardLevel;

        static readonly int[] FloorGrade = { 1, 1, 1, 2, 2, 3, 3, 4, 5 }; // by floor (index = floor)
        static readonly int[] GradeGold = { 0, 150, 300, 550, 900, 1500 };

        // A new set of notices (every morning, and when the board is built up); taken quests stay.
        public void RefreshBoard()
        {
            Board.Clear();
            if (Data.Dungeon != null)
            {
                // the floors the company can reach: the first two, one past the deepest cleared
                int reach = 2;
                foreach (var hero in Roster) reach = Mathf.Max(reach, hero.HighestFloorCleared + 1);
                foreach (var trip in Away) foreach (var hero in trip.Party) reach = Mathf.Max(reach, hero.HighestFloorCleared + 1);
                reach = Mathf.Min(reach, SoulDungeonSession.FinalFloor);
                for (int i = 0; i < BoardSize; i++)
                {
                    var quest = RollQuest(reach);
                    if (quest != null) Board.Add(quest);
                }
            }
            Changed();
        }

        SoulQuest RollQuest(int reach)
        {
            // deeper floors less often; never past what the board handles
            int floor = Mathf.Clamp(1 + (int)(Mathf.Sqrt((float)random.NextDouble()) * reach), 1, reach);
            while (floor > 1 && FloorGrade[floor] > TopGrade) floor--;
            var roster = Data.Dungeon.Roster(floor);
            var monsters = roster.All().FindAll(m => !m.Guardian && !m.Elite);
            var quest = new SoulQuest { Floor = floor, Grade = FloorGrade[floor] };
            float gold = 1;
            int roll = random.Next(100);
            if (roll < 38 && monsters.Count > 0)
            {
                var monster = monsters[random.Next(monsters.Count)];
                quest.Kind = SoulQuestKind.Hunt; quest.TargetId = monster.Id;
                quest.Goal = 5 * (1 + random.Next(3));
                quest.Title = $"{monster.Name} {quest.Goal}마리 토벌";
                gold = .4f + quest.Goal / 12f;
            }
            else if (roll < 53 && roster.Elites.Length > 0)
            {
                var elite = roster.Elites[random.Next(roster.Elites.Length)];
                quest.Kind = SoulQuestKind.Elite; quest.TargetId = elite.Id; quest.Goal = 1;
                quest.Title = $"{elite.Name} 처치"; gold = 1.2f;
            }
            else if (roll < 63 && roster.Boss != null)
            {
                quest.Kind = SoulQuestKind.Boss; quest.TargetId = roster.Boss.Id; quest.Goal = 1;
                quest.Title = $"{roster.Boss.Name} 토벌"; quest.Grade++; gold = 1.4f;
            }
            else if (roll < 73)
            {
                quest.Kind = SoulQuestKind.Floor; quest.Goal = floor;
                quest.Title = $"{floor}층 돌파"; gold = 1.1f;
            }
            else if (roll < 84 && monsters.Exists(m => m.DroppedSoul != null))
            {
                var souled = monsters.FindAll(m => m.DroppedSoul != null);
                var monster = souled[random.Next(souled.Count)];
                quest.Kind = SoulQuestKind.Souls; quest.TargetId = monster.Id; quest.Goal = 1;
                quest.Title = $"{monster.Name}의 영혼 확보"; quest.Grade++; gold = 1.2f;
            }
            else if (roll < 93)
            {
                quest.Kind = SoulQuestKind.Chests; quest.Goal = 2 + quest.Grade;
                quest.Title = $"보물 상자 {quest.Goal}개 열기"; gold = .9f;
            }
            else
            {
                quest.Kind = SoulQuestKind.Hidden; quest.Goal = 1 + quest.Grade / 3;
                quest.Title = $"숨겨진 방 {quest.Goal}곳 찾기"; gold = 1.1f;
            }
            quest.Grade = Mathf.Clamp(quest.Grade, 1, Mathf.Min(5, TopGrade));
            quest.Reward = Mathf.RoundToInt(GradeGold[quest.Grade] * gold / 10f) * 10;
            RollBonus(quest, roster);
            return quest;
        }

        // The extra reward: better and rarer with the grade.
        void RollBonus(SoulQuest quest, SoulFloorRoster roster)
        {
            int grade = quest.Grade;
            double roll = random.NextDouble();
            SoulData soul = null;
            if (grade >= 3)
            {
                var souled = roster.All().FindAll(m => m.DroppedSoul != null && (grade >= 5 || !m.Guardian));
                if (souled.Count > 0) soul = souled[random.Next(souled.Count)].DroppedSoul;
            }
            var books = new List<SoulActiveSkillData>();
            if (Data.Dungeon != null) foreach (var book in Data.Dungeon.SkillBooks) if (book != null && book.BookTier <= Mathf.Min(3, (grade + 1) / 2)) books.Add(book);

            if (grade >= 4 && roll < .15) { quest.Bonus = SoulQuestBonus.Supply; quest.BonusId = SoulSupplies.ReturnScroll; quest.BonusCount = 1; return; }
            if (soul != null && roll < (grade >= 5 ? .6 : .3)) { quest.Bonus = SoulQuestBonus.Soul; quest.BonusId = soul.name; quest.BonusCount = 1; return; }
            if (grade >= 2 && books.Count > 0 && roll < .5) { quest.Bonus = SoulQuestBonus.SkillBook; quest.BonusId = books[random.Next(books.Count)].name; quest.BonusCount = 1; return; }
            if (grade >= 2 && roll < .75) { quest.Bonus = SoulQuestBonus.Equipment; quest.BonusCount = 1; return; }
            if (roll < .88) { quest.Bonus = SoulQuestBonus.SoulStone; quest.BonusCount = grade; return; }
            string[] supplies = grade <= 1 ? new[] { SoulSupplies.HealPotion, "potion_stamina", SoulSupplies.CampKit }
                : grade <= 3 ? new[] { SoulSupplies.MendScroll, SoulSupplies.HealPotion, "scroll_fury", "scroll_guard" }
                : new[] { "potion_greater", "panacea", SoulSupplies.MendScroll };
            quest.Bonus = SoulQuestBonus.Supply; quest.BonusId = supplies[random.Next(supplies.Length)];
            quest.BonusCount = quest.BonusId == SoulSupplies.CampKit || quest.BonusId == SoulSupplies.MendScroll ? 1 + grade / 3 : 2 + grade;
        }

        public string BonusName(SoulQuest quest)
        {
            switch (quest.Bonus)
            {
                case SoulQuestBonus.Supply: return $"{SoulSupplies.Get(quest.BonusId)?.Name} ×{quest.BonusCount}";
                case SoulQuestBonus.SoulStone: return $"영혼석 ×{quest.BonusCount}";
                case SoulQuestBonus.Equipment: return "장비 1개";
                case SoulQuestBonus.SkillBook: { var book = FindAsset(Data.AllSkills, quest.BonusId); return book != null ? $"스킬북 '{book.SkillName}'" : "스킬북"; }
                case SoulQuestBonus.Soul: { var soul = FindAsset(Data.AllSouls, quest.BonusId); return soul != null ? $"{soul.OriginMonster}의 영혼" : "영혼"; }
                default: return null;
            }
        }

        public Sprite BonusIcon(SoulQuest quest)
        {
            switch (quest.Bonus)
            {
                case SoulQuestBonus.Supply: return SoulIconSet.Ui(SoulSupplies.Get(quest.BonusId)?.Icon ?? "potion");
                case SoulQuestBonus.SoulStone: return SoulIconSet.Ui("preserve");
                case SoulQuestBonus.Equipment: return SoulIconSet.Ui("equipment");
                case SoulQuestBonus.SkillBook: { var book = FindAsset(Data.AllSkills, quest.BonusId); return book != null && book.Icon != null ? book.Icon : SoulIconSet.Ui("skill"); }
                case SoulQuestBonus.Soul: return SoulIconSet.Ui("soul");
                default: return null;
            }
        }

        static T FindAsset<T>(T[] assets, string name) where T : Object
            => assets == null || string.IsNullOrEmpty(name) ? null : System.Array.Find(assets, a => a != null && a.name == name);

        public bool Accept(SoulQuest quest)
        {
            if (!Board.Contains(quest) || Active.Count >= QuestSlots || Level(SoulBuildingKind.Board) <= 0) return false;
            Board.Remove(quest); Active.Add(quest);
            Changed($"의뢰 수락: [{quest.GradeName}] {quest.Title}");
            return true;
        }

        public bool Claim(SoulQuest quest)
        {
            if (!Active.Contains(quest) || !quest.Done) return false;
            Active.Remove(quest);
            int reward = QuestReward(quest);
            Wallet.Add(eItem.Gold, reward);
            string bonus = GiveBonus(quest);
            Renown += 5 + 5 * quest.Grade;
            Disgraced = false;
            Changed($"의뢰 완료: [{quest.GradeName}] {quest.Title} (+{reward} 금화" + (bonus != null ? $" · {bonus}" : "") + ")");
            return true;
        }

        string GiveBonus(SoulQuest quest)
        {
            switch (quest.Bonus)
            {
                case SoulQuestBonus.Supply: AddSupply(quest.BonusId, quest.BonusCount); break;
                case SoulQuestBonus.SoulStone: Wallet.Add(eItem.SoulStone, quest.BonusCount); break;
                case SoulQuestBonus.SkillBook: { var book = FindAsset(Data.AllSkills, quest.BonusId); if (book == null) return null; Books.Add(book); break; }
                case SoulQuestBonus.Soul: { var soul = FindAsset(Data.AllSouls, quest.BonusId); if (soul == null) return null; Vault.Add(soul); break; }
                case SoulQuestBonus.Equipment:
                {
                    if (Data.Dungeon == null || Data.Dungeon.LootTable.Length == 0) return null;
                    var item = SoulItemRules.Roll(Data.Dungeon.LootTable, quest.Floor + quest.Grade, SoulDropSource.Chest, 0, random);
                    if (item == null) return null;
                    Inventory.Add(item);
                    return item.Name;
                }
                default: return null;
            }
            return BonusName(quest);
        }

        // The first notice of a new company (던전 탐사): go down once and come back — 300 gold, taken from the start.
        public const int StarterReward = 300;
        public static SoulQuest StarterQuest()
            => new SoulQuest { Kind = SoulQuestKind.Explore, Title = "던전 탐사", Goal = 1, Reward = StarterReward, Grade = 1, Floor = 1 };

        // Where a quest would stand if this session came back now (the dungeon HUD shows it); 던전 탐사 counts on the return.
        public static int ProgressWith(SoulQuest quest, SoulDungeonSession session)
        {
            int progress = quest.Progress;
            if (session != null)
                switch (quest.Kind)
                {
                    case SoulQuestKind.Hunt:
                    case SoulQuestKind.Elite:
                    case SoulQuestKind.Boss: progress += session.DefeatedCounts.TryGetValue(quest.TargetId ?? "", out int kills) ? kills : 0; break;
                    case SoulQuestKind.Floor: progress = Mathf.Max(progress, session.Finished ? session.Floor : session.Floor - 1); break;
                    case SoulQuestKind.Souls: progress += session.SoulsByMonster.TryGetValue(quest.TargetId ?? "", out int souls) ? souls : 0; break;
                    case SoulQuestKind.Chests: progress += session.ChestsOpened; break;
                    case SoulQuestKind.Hidden: progress += session.HiddenFound; break;
                }
            return Mathf.Min(progress, quest.Goal);
        }

        // After an expedition: what it did toward each quest taken.
        void CountQuests(SoulDungeonSession session, List<string> report)
        {
            foreach (var quest in Active)
            {
                int before = quest.Progress;
                quest.Progress = quest.Kind == SoulQuestKind.Explore ? quest.Goal : ProgressWith(quest, session);
                if (quest.Progress != before) report.Add($"의뢰 '{quest.Title}' {quest.Progress}/{quest.Goal}" + (quest.Done ? " — 게시판에서 보상 수령" : ""));
            }
        }
    }
}
