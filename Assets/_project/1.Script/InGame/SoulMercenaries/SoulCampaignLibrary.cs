using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // The library (서고): a skill book found in the dungeon is spent on one mercenary. A skill it does not know yet is
    // learned (Lv.1); one it knows goes up a level (the library's building level caps it, level 3 is the top); at
    // level 3 one more book turns it into its stronger form (library level 3). Each mercenary keeps its own levels.
    // A book not wanted can be sold.
    public sealed partial class SoulCampaign
    {
        // Skills registered on the old shelf (older saves): moved onto the mercenaries when the save is read.
        public readonly Dictionary<SoulActiveSkillData, int> Library = new Dictionary<SoulActiveSkillData, int>();
        public readonly List<SoulActiveSkillData> Books = new List<SoulActiveSkillData>();

        public const int MaxSkillLevel = 3;
        public int LibraryLevel => Level(SoulBuildingKind.Library);

        public enum BookUse { Learn, LevelUp, Upgrade }

        // Skills the mercenary knows by itself (not those an item lends).
        public static List<SoulActiveSkillData> KnownSkills(SoulMercenary hero)
        {
            var list = new List<SoulActiveSkillData>();
            foreach (var skill in hero.StartingActives) if (skill != null && !list.Contains(skill)) list.Add(skill);
            foreach (var skill in hero.LearnedActives) if (skill != null && !list.Contains(skill)) list.Add(skill);
            return list;
        }

        // The mercenary's own level of a skill it knows (0: it does not know it).
        public static int HeroSkillLevel(SoulMercenary hero, SoulActiveSkillData skill)
            => skill == null || !KnownSkills(hero).Contains(skill) ? 0 : hero.SkillLevel(skill);

        // What a book would do for this mercenary, and why it cannot (null: it can).
        public string BookBlock(SoulMercenary hero, SoulActiveSkillData book, out BookUse use)
        {
            use = BookUse.Learn;
            if (LibraryLevel <= 0) return "서고를 지어야 합니다";
            if (hero == null || book == null || !Books.Contains(book)) return "없는 책";
            int level = HeroSkillLevel(hero, book);
            if (level == 0)
            {
                if (book.UpgradeTo != null && KnownSkills(hero).Contains(book.UpgradeTo)) return "이미 상위 스킬을 알고 있습니다";
                return SoulSkillUsePolicy.Blocked(hero, book);
            }
            if (level < MaxSkillLevel)
            {
                use = BookUse.LevelUp;
                return level < LibraryLevel ? null : $"서고 {level + 1}단계 필요 (지금 Lv.{level})";
            }
            use = BookUse.Upgrade;
            if (book.UpgradeTo == null) return "최고 단계입니다";
            return LibraryLevel >= 3 ? null : "상위 스킬로 바꾸려면 서고 3단계 필요";
        }

        // Spends the book on the mercenary (독서가: a chance it is not used up).
        public bool UseBook(SoulMercenary hero, SoulActiveSkillData book, out string result)
        {
            result = BookBlock(hero, book, out var use);
            if (result != null) return false;
            string id = SoulSkillUsePolicy.Id(book);
            switch (use)
            {
                case BookUse.Learn:
                    hero.LearnSkill(book);
                    hero.SkillLevels[id] = 1;
                    result = $"{hero.Name}: '{book.SkillName}' 습득";
                    break;
                case BookUse.LevelUp:
                    int level = HeroSkillLevel(hero, book) + 1;
                    hero.SkillLevels[id] = level;
                    result = $"{hero.Name}: '{book.SkillName}' Lv.{level}";
                    break;
                default:
                    var better = book.UpgradeTo;
                    int learned = hero.LearnedActives.IndexOf(book), starting = hero.StartingActives.IndexOf(book);
                    if (learned >= 0) hero.LearnedActives[learned] = better;
                    if (starting >= 0) hero.StartingActives[starting] = better;
                    hero.SkillLevels.Remove(id);
                    hero.SkillLevels[SoulSkillUsePolicy.Id(better)] = 1;
                    result = $"{hero.Name}: '{book.SkillName}' → '{better.SkillName}'로 강화!";
                    break;
            }
            bool kept = random.NextDouble() < Mathf.Clamp(hero.Stats.Total(StatType.BookDiscount), 0, .8f);
            if (!kept) Books.Remove(book);
            else result += " (책이 남았습니다)";
            hero.Rebuild(Rules);
            Changed("서고: " + result);
            return true;
        }

        public static int BookPrice(SoulActiveSkillData book) => 60 * book.BookTier;

        public bool SellBook(SoulActiveSkillData book)
        {
            if (!Books.Remove(book)) return false;
            Wallet.Add(eItem.Gold, BookPrice(book));
            Changed($"스킬북 '{book.SkillName}' 판매 (+{BookPrice(book)})");
            return true;
        }

        // Forgetting is free: it only frees the slot (and its level goes with it).
        public bool Forget(SoulMercenary hero, SoulActiveSkillData skill)
        {
            bool removed = hero.LearnedActives.Remove(skill) | hero.StartingActives.Remove(skill);
            if (!removed) return false;
            hero.SkillLevels.Remove(SoulSkillUsePolicy.Id(skill));
            hero.Rebuild(Rules);
            Changed($"{hero.Name}: '{skill.SkillName}'을(를) 잊었습니다");
            return true;
        }

        // An older save's shelf: each mercenary who knows a shelved skill takes its level; a shelved skill nobody
        // knows comes back as a book. The shelf is gone after this.
        public void MoveShelfOntoMercenaries()
        {
            if (Library.Count == 0) return;
            var everyone = new List<SoulMercenary>(Roster);
            foreach (var trip in Away) everyone.AddRange(trip.Party);
            foreach (var entry in Library)
            {
                bool known = false;
                foreach (var hero in everyone)
                {
                    if (!KnownSkills(hero).Contains(entry.Key)) continue;
                    hero.SkillLevels[SoulSkillUsePolicy.Id(entry.Key)] = Mathf.Clamp(entry.Value, 1, MaxSkillLevel);
                    known = true;
                }
                if (!known) Books.Add(entry.Key);
            }
            Library.Clear();
        }
    }
}
