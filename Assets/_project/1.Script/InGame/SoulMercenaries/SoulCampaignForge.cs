using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // The blacksmith (대장간): random enchants into empty slots (always succeeds, may land something useless),
    // re-rolls from level 2, one option kept through re-rolls from level 3. Works on stored and worn items alike.
    public sealed partial class SoulCampaign
    {
        public int SmithLevel => Level(SoulBuildingKind.Blacksmith);

        // Every item the smith can work on, with who wears it (null: in the storehouse).
        public List<(SoulItem item, SoulMercenary wearer)> ForgeItems()
        {
            var list = new List<(SoulItem, SoulMercenary)>();
            foreach (var hero in Roster) foreach (var item in hero.Equipment) list.Add((item, hero));
            foreach (var item in Inventory) list.Add((item, null));
            return list;
        }

        SoulMercenary Wearer(SoulItem item) => Roster.Find(hero => hero.Equipment.Contains(item));
        bool Owned(SoulItem item) => Inventory.Contains(item) || Wearer(item) != null;

        // 40 gold × grade price scale × the slot's step: the first cheap, the second ×8, the third ×30.
        static readonly int[] EnchantSteps = { 1, 8, 30 };
        public int EnchantCost(SoulItem item) => Mathf.RoundToInt(40 * SoulItemRules.PriceScale(item.Grade) * EnchantSteps[Mathf.Min(item.Enchants.Count, EnchantSteps.Length - 1)]);

        public string EnchantBlock(SoulItem item)
        {
            if (SmithLevel <= 0) return "대장간을 지어야 합니다";
            if (Inventory.Contains(item)) return "창고의 장비는 부여할 수 없습니다";
            if (!Owned(item)) return "용병단의 장비가 아닙니다";
            if (!item.HasFreeEnchantSlot) return item.EnchantSlots == 0 ? "부여 칸이 없는 등급입니다" : "빈 부여 칸이 없습니다";
            if (Gold < EnchantCost(item)) return "금화 부족";
            return null;
        }

        public bool Enchant(SoulItem item, out string result)
        {
            result = EnchantBlock(item);
            if (result != null) return false;
            var enchant = SoulItemRules.RollEnchant(item, SmithLevel, random);
            if (enchant == null) { result = "붙일 수 있는 옵션이 없습니다"; return false; }
            Spend(EnchantCost(item));
            item.Enchants.Add(enchant);
            Wearer(item)?.Rebuild(Rules);
            result = $"{item.Name}: {SoulItemRules.EnchantText(enchant)} ({SoulItemRules.TierNames[enchant.Tier]})";
            Changed("마법 부여 — " + result);
            return true;
        }

        public int RerollCost(SoulItem item)
        {
            float cost = 0;
            for (int i = 0; i < item.Enchants.Count; i++)
                if (!item.Enchants[i].Locked) cost += 40 * SoulItemRules.PriceScale(item.Grade) * EnchantSteps[Mathf.Min(i, EnchantSteps.Length - 1)] * .6f;
            return Mathf.RoundToInt(cost * (1 + .5f * item.Rerolls)); // each re-roll of this piece half again dearer
        }

        public string RerollBlock(SoulItem item)
        {
            if (SmithLevel < 2) return "대장간 2단계 필요";
            if (Inventory.Contains(item)) return "창고의 장비는 부여할 수 없습니다";
            if (!Owned(item)) return "용병단의 장비가 아닙니다";
            if (!item.Enchants.Exists(e => !e.Locked)) return "다시 굴릴 옵션이 없습니다";
            if (Gold < RerollCost(item)) return "금화 부족";
            return null;
        }

        // Every option not kept rolls again (kind and tier).
        public bool Reroll(SoulItem item, out string result)
        {
            result = RerollBlock(item);
            if (result != null) return false;
            Spend(RerollCost(item));
            var parts = new List<string>();
            for (int i = 0; i < item.Enchants.Count; i++)
            {
                if (item.Enchants[i].Locked) continue;
                var fresh = SoulItemRules.RollEnchant(item, SmithLevel, random, item.Enchants[i].Option, item.Rerolls);
                if (fresh == null) continue;
                item.Enchants[i] = fresh;
                parts.Add(SoulItemRules.EnchantText(fresh));
            }
            item.Rerolls++;
            Wearer(item)?.Rebuild(Rules);
            result = $"{item.Name}: " + string.Join(", ", parts);
            Changed("재부여 — " + result);
            return true;
        }

        // Level 3: keep one option through re-rolls (only one at a time).
        public bool ToggleEnchantLock(SoulItem item, int index)
        {
            if (SmithLevel < 3 || Wearer(item) == null || index < 0 || index >= item.Enchants.Count) return false;
            bool locking = !item.Enchants[index].Locked;
            foreach (var enchant in item.Enchants) enchant.Locked = false;
            item.Enchants[index].Locked = locking;
            Changed();
            return true;
        }
    }
}
