using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    public enum SoulEquipSlot { MainHand, OffHand, Body, Head, Accessory }

    [Flags]
    public enum SoulSlotMask { None = 0, MainHand = 1, OffHand = 2, Body = 4, Head = 8, Accessory = 16, Armor = OffHand | Body | Head, All = 31 }

    public enum SoulMaterial { None, Cloth, Silk, Leather, Wood, Spiritwood, Bronze, Iron, Steel, Mithril, BlackIron }

    public enum SoulDropSource { Monster, Elite, Boss, Chest, HiddenChest, Mimic }

    // One enchant on an item: an option from SoulItemRules.Enchants at a value tier (0 lowest … 3 highest).
    [Serializable]
    public sealed class SoulEnchant
    {
        public string Option;
        public int Tier;
        public bool Locked; // kept when the smith re-rolls (blacksmith level 3)
    }

    public sealed class SoulEnchantOption
    {
        public string Id, Name;
        public StatType Stat;
        public float[] Values; // by tier
        public SoulSlotMask Slots;
        public bool Lightness; // lowers the item's own weight instead of a stat
    }

    public sealed class SoulSpecialOption
    {
        public string Id, Prefix, Description;
        public SoulSlotMask Slots;
        public SoulStatBonus[] Bonuses = Array.Empty<SoulStatBonus>();
        public float WeightScale = 1f;
        public float BaseScale = 1f;
        public int MinFloor = 1;
    }

    public sealed class SoulMaterialInfo
    {
        public SoulMaterial Material;
        public string Name;
        public float Weight = 1f, Base = 1f;
        public SoulStatBonus[] Traits = Array.Empty<SoulStatBonus>();
    }

    // A piece of equipment that exists in the game: the template (SoulEquipmentData) with its own grade, enchants
    // and special options. Two items from one template can differ; the template itself never changes.
    public sealed class SoulItem
    {
        public readonly SoulEquipmentData Data;
        public int Grade;                     // 1 조잡한 … 6 전설의
        public readonly List<SoulEnchant> Enchants = new List<SoulEnchant>();
        public readonly List<string> Specials = new List<string>();
        public bool Dropped;                  // came from the dungeon (only those carry special options)
        public int Rerolls;                   // times the smith re-rolled it: each one dearer, and 1% likelier to land high

        public SoulItem(SoulEquipmentData data, int grade)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Grade = data.Unique ? data.FixedGrade : Mathf.Clamp(grade, 1, SoulItemRules.MaxGrade);
        }

        public string Id => Data.Id;
        public string WeaponTag => Data.WeaponTag;
        public SoulEquipSlot Slot => Data.Slot;
        public bool TwoHanded => Data.TwoHanded;
        public SoulAppearancePatch Look => Data.Look;
        public SoulPatternData BasicAttack => Data.BasicAttack;
        public SoulPatternData[] Patterns => Data.Patterns;
        public SoulActiveSkillData[] ActiveSkills => Data.ActiveSkills;
        public SoulPassiveSkillData[] Passives => Data.Passives;
        public string GradeName => SoulItemRules.GradeName(Grade);
        public Color GradeColor => SoulItemRules.GradeColor(Grade);

        public string Name
        {
            get
            {
                string prefix = "";
                foreach (string id in Specials) { var special = SoulItemRules.Special(id); if (special != null) prefix += special.Prefix + " "; }
                return prefix + (Data.Unique ? Data.Name : GradeName + " " + Data.Name);
            }
        }

        public int EnchantSlots => SoulItemRules.EnchantSlots(Grade) + (Slot == SoulEquipSlot.Accessory ? 1 : 0);
        public bool HasFreeEnchantSlot => Enchants.Count < EnchantSlots;

        public float Weight
        {
            get
            {
                float weight = Data.BaseWeight * SoulItemRules.Material(Data.Material).Weight;
                foreach (string id in Specials) { var special = SoulItemRules.Special(id); if (special != null) weight *= special.WeightScale; }
                float light = 0;
                foreach (var enchant in Enchants) { var option = SoulItemRules.Enchant(enchant.Option); if (option != null && option.Lightness) light += option.Values[enchant.Tier]; }
                return Mathf.Max(0, weight * (1 - Mathf.Clamp(light, 0, .8f)));
            }
        }

        public int Price => Mathf.Max(1, Mathf.RoundToInt(Data.Price * (Data.Unique ? 1f : SoulItemRules.PriceScale(Grade)) * (1 + .25f * Enchants.Count + .6f * Specials.Count)));
        public int SellPrice => Price / 2;

        // Base options scaled by grade and material (unique items keep theirs as written), material traits,
        // enchants and the stat part of special options.
        public SoulStatBonus[] Bonuses
        {
            get
            {
                var list = new List<SoulStatBonus>();
                var material = SoulItemRules.Material(Data.Material);
                float scale = (Data.Unique ? 1f : SoulItemRules.GradeScale(Grade) * material.Base);
                foreach (string id in Specials) { var special = SoulItemRules.Special(id); if (special != null) scale *= special.BaseScale; }
                foreach (var bonus in Data.Bonuses) list.Add(new SoulStatBonus { Stat = bonus.Stat, Value = bonus.Value * scale });
                list.AddRange(material.Traits);
                foreach (var enchant in Enchants)
                {
                    var option = SoulItemRules.Enchant(enchant.Option);
                    if (option != null && !option.Lightness) list.Add(new SoulStatBonus { Stat = option.Stat, Value = option.Values[enchant.Tier] });
                }
                foreach (string id in Specials) { var special = SoulItemRules.Special(id); if (special != null) list.AddRange(special.Bonuses); }
                return list.ToArray();
            }
        }

        public SoulItem Copy()
        {
            var copy = new SoulItem(Data, Grade) { Dropped = Dropped };
            foreach (var enchant in Enchants) copy.Enchants.Add(new SoulEnchant { Option = enchant.Option, Tier = enchant.Tier, Locked = enchant.Locked });
            copy.Specials.AddRange(Specials);
            return copy;
        }
    }

    // Everything about items that is a rule rather than content: grades, materials, enchant and special options,
    // what drops where. Numbers follow DESIGN_EQUIPMENT_MERCENARIES.md (§1, §2, §9).
    public static class SoulItemRules
    {
        public const int MaxGrade = 6;
        static readonly string[] gradeNames = { "조잡한", "평범한", "좋은", "정교한", "명장의", "전설의" };
        static readonly float[] gradeScale = { .7f, 1f, 1.3f, 1.65f, 2.1f, 2.7f };
        static readonly int[] enchantSlots = { 0, 1, 1, 2, 2, 3 };
        static readonly float[] priceScale = { .5f, 1f, 2f, 4f, 8f, 15f };
        static readonly Color[] gradeColors =
        {
            new Color(.62f, .6f, .56f), new Color(.92f, .92f, .92f), new Color(.45f, .85f, .45f),
            new Color(.4f, .66f, 1f), new Color(.78f, .5f, 1f), new Color(1f, .7f, .25f)
        };
        public static readonly Color SpecialColor = new Color(.85f, .55f, 1f);

        static int G(int grade) => Mathf.Clamp(grade, 1, MaxGrade) - 1;
        public static string GradeName(int grade) => gradeNames[G(grade)];
        public static float GradeScale(int grade) => gradeScale[G(grade)];
        public static int EnchantSlots(int grade) => enchantSlots[G(grade)];
        public static float PriceScale(int grade) => priceScale[G(grade)];
        public static Color GradeColor(int grade) => gradeColors[G(grade)];

        public static string SlotName(SoulEquipSlot slot)
        {
            switch (slot)
            {
                case SoulEquipSlot.MainHand: return "주무기";
                case SoulEquipSlot.OffHand: return "보조";
                case SoulEquipSlot.Body: return "몸";
                case SoulEquipSlot.Head: return "머리";
                default: return "장신구";
            }
        }

        public static SoulSlotMask Mask(SoulEquipSlot slot) => (SoulSlotMask)(1 << (int)slot);

        // ── materials ────────────────────────────────────────────

        static SoulStatBonus B(StatType stat, float value) => new SoulStatBonus { Stat = stat, Value = value };

        public static readonly SoulMaterialInfo[] Materials =
        {
            new SoulMaterialInfo { Material = SoulMaterial.None, Name = "" },
            new SoulMaterialInfo { Material = SoulMaterial.Cloth, Name = "천" },
            new SoulMaterialInfo { Material = SoulMaterial.Silk, Name = "비단", Weight = .6f, Traits = new[] { B(StatType.MagicResist, .02f) } },
            new SoulMaterialInfo { Material = SoulMaterial.Leather, Name = "가죽", Traits = new[] { B(StatType.Evasion, .01f) } },
            new SoulMaterialInfo { Material = SoulMaterial.Wood, Name = "나무" },
            new SoulMaterialInfo { Material = SoulMaterial.Spiritwood, Name = "영목", Weight = .6f, Traits = new[] { B(StatType.Magic, 1) } },
            new SoulMaterialInfo { Material = SoulMaterial.Bronze, Name = "청동", Weight = 1.1f, Base = .9f },
            new SoulMaterialInfo { Material = SoulMaterial.Iron, Name = "철" },
            new SoulMaterialInfo { Material = SoulMaterial.Steel, Name = "강철", Weight = 1.1f, Base = 1.1f },
            new SoulMaterialInfo { Material = SoulMaterial.Mithril, Name = "미스릴", Weight = .5f, Base = 1.05f, Traits = new[] { B(StatType.MagicResist, .02f) } },
            new SoulMaterialInfo { Material = SoulMaterial.BlackIron, Name = "흑철", Weight = 1.3f, Base = 1.2f },
        };

        public static SoulMaterialInfo Material(SoulMaterial material)
        {
            foreach (var info in Materials) if (info.Material == material) return info;
            return Materials[0];
        }

        // ── load ─────────────────────────────────────────────────

        public static float CarryLimit(float strength) => 10 + Mathf.Max(0, strength) * 4;

        // Stamina cost multiplier by load ratio (light: evasion −10% handled by the caller).
        public static float LoadCostScale(float load)
            => load > 1.6f ? 1.7f : load > 1.3f ? 1.4f : load > 1f ? 1.2f : 1f;

        public static float LoadMoveScale(float load) => load > 1.6f ? .75f : load > 1.3f ? .9f : 1f;

        public static string LoadName(float load)
            => load > 1.6f ? "과적" : load > 1.3f ? "매우 무거움" : load > 1f ? "무거움" : load > .6f ? "보통" : "가벼움";

        public const float LightLoad = .6f, OverLoad = 1.6f;

        // ── enchant options (§2.3) ───────────────────────────────

        static SoulEnchantOption E(string id, string name, StatType stat, SoulSlotMask slots, params float[] values)
            => new SoulEnchantOption { Id = id, Name = name, Stat = stat, Slots = slots, Values = values };

        const SoulSlotMask W = SoulSlotMask.MainHand, A = SoulSlotMask.Accessory, All = SoulSlotMask.All;
        const SoulSlotMask Armor = SoulSlotMask.Armor;

        public static readonly SoulEnchantOption[] Enchants =
        {
            E("str", "근력", StatType.Strength, All, 1, 2, 3, 5),
            E("vit", "체력", StatType.Vitality, All, 1, 2, 3, 5),
            E("agi", "민첩", StatType.Agility, All, 1, 2, 3, 5),
            E("mag", "마력", StatType.Magic, All, 1, 2, 3, 5),
            E("will", "정신력", StatType.Will, All, 1, 2, 3, 5),
            E("luck", "운", StatType.Luck, All, 1, 2, 3, 5),
            E("dur", "내구도", StatType.Durability, All, 1, 2, 3, 5),
            E("anti", "항마력", StatType.AntiMagic, All, 1, 2, 3, 5),
            E("regen", "재생력", StatType.Regeneration, All, 1, 2, 3, 5),
            E("rec", "회복력", StatType.Recovery, All, 1, 2, 3, 5),
            E("slash", "절삭력", StatType.SlashPower, All, 1, 2, 3, 5),
            E("impact", "타격력", StatType.ImpactPower, All, 1, 2, 3, 5),
            E("pierce", "관통력", StatType.PiercePower, All, 1, 2, 3, 5),
            E("hp", "최대 HP", StatType.MaxHp, All, 8, 15, 25, 40),
            E("sta", "최대 스태미나", StatType.MaxStamina, All, 8, 15, 25, 40),
            E("mp", "최대 MP", StatType.MaxMp, All, 6, 12, 20, 30),
            E("sta_regen", "스태미나 재생", StatType.StaminaRegen, Armor | A, .3f, .6f, 1f, 1.5f),
            E("atk", "공격력", StatType.Attack, W | A, 1, 2, 3, 5),
            E("def", "방어력", StatType.Armor, Armor, 1, 2, 3, 5),
            E("acc", "명중", StatType.Accuracy, W | SoulSlotMask.Head | A, .02f, .04f, .06f, .09f),
            E("eva", "회피", StatType.Evasion, SoulSlotMask.Body | SoulSlotMask.Head | A, .01f, .02f, .03f, .05f),
            E("speed", "행동 속도", StatType.ActionSpeed, W | A, .02f, .04f, .06f, .1f),
            E("move", "이동 속도", StatType.MoveSpeed, SoulSlotMask.Body | A, .1f, .2f, .3f, .5f),
            E("fire", "화염 공격", StatType.FireAttack, W, 2, 4, 7, 12),
            E("cold", "냉기 공격", StatType.ColdAttack, W, 2, 4, 7, 12),
            E("light", "번개 공격", StatType.LightningAttack, W, 2, 4, 7, 12),
            E("earth", "대지 공격", StatType.EarthAttack, W, 2, 4, 7, 12),
            E("wind", "바람 공격", StatType.WindAttack, W, 2, 4, 7, 12),
            E("toxic", "독 공격", StatType.ToxicAttack, W, 2, 4, 7, 12),
            E("r_fire", "화염 저항", StatType.FireResist, Armor | A, .05f, .1f, .15f, .25f),
            E("r_cold", "냉기 저항", StatType.ColdResist, Armor | A, .05f, .1f, .15f, .25f),
            E("r_light", "번개 저항", StatType.LightningResist, Armor | A, .05f, .1f, .15f, .25f),
            E("r_earth", "대지 저항", StatType.EarthResist, Armor | A, .05f, .1f, .15f, .25f),
            E("r_wind", "바람 저항", StatType.WindResist, Armor | A, .05f, .1f, .15f, .25f),
            E("r_toxic", "독 저항", StatType.ToxicResist, Armor | A, .05f, .1f, .15f, .25f),
            E("r_bleed", "출혈 내성", StatType.BleedResist, Armor | A, .05f, .1f, .15f, .25f),
            E("r_poison", "중독 내성", StatType.PoisonResist, Armor | A, .05f, .1f, .15f, .25f),
            E("r_burn", "화상 내성", StatType.BurnResist, Armor | A, .05f, .1f, .15f, .25f),
            E("r_chill", "냉기 내성", StatType.ChillResist, Armor | A, .05f, .1f, .15f, .25f),
            E("r_fear", "공포 내성", StatType.FearResist, Armor | A, .05f, .1f, .15f, .25f),
            E("r_stun", "스턴 내성", StatType.StunResist, Armor | A, .05f, .1f, .15f, .25f),
            new SoulEnchantOption { Id = "lightness", Name = "가벼움", Slots = All, Values = new[] { .1f, .2f, .3f, .4f }, Lightness = true },
            E("free", "무소모 확률", StatType.FreePatternChance, A, .01f, .02f, .03f, .05f),
            E("kb", "넉백 저항", StatType.KnockbackResist, SoulSlotMask.Body | SoulSlotMask.OffHand, .05f, .1f, .15f, .25f),
        };

        public static SoulEnchantOption Enchant(string id)
        {
            foreach (var option in Enchants) if (option.Id == id) return option;
            return null;
        }

        public static string EnchantText(SoulEnchant enchant)
        {
            var option = Enchant(enchant.Option);
            if (option == null) return enchant.Option;
            float value = option.Values[Mathf.Clamp(enchant.Tier, 0, 3)];
            if (option.Lightness) return $"{option.Name} (무게 −{value * 100:0}%)";
            return $"{option.Name} {(IsRatio(option.Stat) ? $"+{value * 100:0.#}%" : $"+{value:0.##}")}";
        }

        public static readonly string[] TierNames = { "하급", "중급", "상급", "최상급" };

        public static bool IsRatio(StatType stat)
        {
            switch (stat)
            {
                case StatType.PhysicalResist: case StatType.MagicResist: case StatType.Accuracy: case StatType.Evasion:
                case StatType.BleedResist: case StatType.PoisonResist: case StatType.BurnResist: case StatType.ChillResist:
                case StatType.FearResist: case StatType.StunResist: case StatType.PetrifyResist: case StatType.KnockbackResist:
                case StatType.FreePatternChance: case StatType.ExecuteBonus:
                case StatType.FireResist: case StatType.ColdResist: case StatType.LightningResist: case StatType.EarthResist:
                case StatType.WindResist: case StatType.ToxicResist: case StatType.LoadRatio:
                case StatType.LifeSteal: case StatType.KillStamina: case StatType.EliteDamage: case StatType.DoubleHit:
                case StatType.ArmorShred: case StatType.FreeSkillChance: case StatType.Thorns: case StatType.WoundResist:
                case StatType.GoldFind: case StatType.LootFind: case StatType.SoulFind: case StatType.CursedHp:
                    return true;
                default: return false;
            }
        }

        // Tier chances by blacksmith level (1..3): 하급 / 중급 / 상급 / 최상급.
        static readonly float[][] tierChances =
        {
            new[] { .5f, .3f, .15f, .05f },
            new[] { .4f, .33f, .2f, .07f },
            new[] { .3f, .35f, .25f, .1f },
        };

        // The odds of each value tier: the smithy's, with 1% a re-roll moved from the lowest to the highest.
        public static float[] TierChances(int smithLevel, int rerolls = 0)
        {
            var chances = (float[])tierChances[Mathf.Clamp(smithLevel, 1, 3) - 1].Clone();
            float shift = Mathf.Min(.01f * rerolls, chances[0]);
            chances[0] -= shift;
            chances[chances.Length - 1] += shift;
            return chances;
        }

        public static int RollTier(int smithLevel, System.Random random, int rerolls = 0)
        {
            var chances = TierChances(smithLevel, rerolls);
            double roll = random.NextDouble();
            for (int i = 0; i < chances.Length; i++) { roll -= chances[i]; if (roll < 0) return i; }
            return 0;
        }

        // Any option the slot allows and the item does not carry yet — useful or not for its wearer: enchanting
        // always succeeds, it just may land something that does nothing for this mercenary.
        public static SoulEnchant RollEnchant(SoulItem item, int smithLevel, System.Random random, string keep = null, int rerolls = 0)
        {
            var mask = Mask(item.Slot);
            var options = new List<SoulEnchantOption>();
            foreach (var option in Enchants)
            {
                if ((option.Slots & mask) == 0) continue;
                if (item.Enchants.Exists(e => e.Option == option.Id && e.Option != keep)) continue;
                options.Add(option);
                // accessories lean to their kind (§1.5): a ring to main stats etc.
                if (item.Slot == SoulEquipSlot.Accessory && Favoured(item.Data.Kind, option)) options.Add(option);
            }
            if (options.Count == 0) return null;
            var pick = options[random.Next(options.Count)];
            return new SoulEnchant { Option = pick.Id, Tier = RollTier(smithLevel, random, rerolls) };
        }

        static bool Favoured(string kind, SoulEnchantOption option)
        {
            switch (kind)
            {
                case "반지": return option.Stat >= StatType.Strength && option.Stat <= StatType.Luck;
                case "목걸이": return option.Stat == StatType.MaxMp || (option.Stat >= StatType.FireResist && option.Stat <= StatType.ToxicResist);
                case "부적": return option.Stat == StatType.MaxStamina || (option.Stat >= StatType.BleedResist && option.Stat <= StatType.StunResist);
                case "팔찌": return option.Stat == StatType.Accuracy || option.Stat == StatType.ActionSpeed;
                default: return false;
            }
        }

        // ── special options (§9.4): dungeon drops only ───────────

        static SoulSpecialOption S(string id, string prefix, string description, SoulSlotMask slots, params SoulStatBonus[] bonuses)
            => new SoulSpecialOption { Id = id, Prefix = prefix, Description = description, Slots = slots, Bonuses = bonuses };

        public static readonly SoulSpecialOption[] SpecialOptions =
        {
            S("vampiric", "흡혈의", "준 피해의 4%를 HP로 회복", W, B(StatType.LifeSteal, .04f)),
            S("breath", "숨결의", "적을 쓰러뜨리면 스태미나 10% 회복", W, B(StatType.KillStamina, .1f)),
            S("executioner", "처형자의", "HP 35% 미만 적에게 근접 피해 +30%", W, B(StatType.ExecuteBonus, .3f)),
            S("hunter", "사냥꾼의", "엘리트·보스에게 피해 +15%", W, B(StatType.EliteDamage, .15f)),
            S("flurry", "연격의", "공격이 12% 확률로 두 번 들어감", W, B(StatType.DoubleHit, .12f)),
            S("shatter", "파쇄의", "대상 방어력의 15%를 무시", W, B(StatType.ArmorShred, .15f)),
            S("echo", "메아리의", "스킬이 15% 확률로 MP·스태미나를 쓰지 않음", W, B(StatType.FreeSkillChance, .15f)),
            S("thorns", "가시의", "근접 피격 때 받은 피해의 15%를 되돌려줌", SoulSlotMask.OffHand | SoulSlotMask.Body, B(StatType.Thorns, .15f)),
            S("unyielding", "불굴의", "넉백 저항 +90% · 스턴 내성 +50%", SoulSlotMask.Body, B(StatType.KnockbackResist, .9f), B(StatType.StunResist, .5f)),
            S("defiant", "거부의", "한 층에 한 번, 쓰러질 피해를 HP 1로 버팀", SoulSlotMask.Body, B(StatType.DeathDefy, 1)),
            S("sutured", "봉합의", "부상이 쌓이는 속도 −40%", SoulSlotMask.Body | SoulSlotMask.Head, B(StatType.WoundResist, .4f)),
            S("regen", "재생의", "HP 재생 +2", SoulSlotMask.Body, B(StatType.HpRegen, 2)),
            S("lucid", "명석의", "공포·혼란 내성 +30% · 스턴 내성 +15%", SoulSlotMask.Head, B(StatType.FearResist, .3f), B(StatType.StunResist, .15f)),
            new SoulSpecialOption { Id = "feather", Prefix = "깃털의", Description = "이 장비의 무게 −60%", Slots = All, WeightScale = .4f },
            S("greedy", "탐욕의", "몬스터 금화 +20% (파티에서 가장 높은 1개)", A, B(StatType.GoldFind, .2f)),
            S("lucky", "행운의", "장비 드롭 확률 +15% (파티에서 가장 높은 1개)", A, B(StatType.LootFind, .15f)),
            S("soulful", "영혼의", "영혼 드롭 확률 +10% (파티에서 가장 높은 1개)", A, B(StatType.SoulFind, .1f)),
            new SoulSpecialOption { Id = "cursed", Prefix = "저주받은", Description = "기본 옵션 ×1.5, 대신 최대 HP −15%", Slots = All, BaseScale = 1.5f,
                Bonuses = new[] { B(StatType.CursedHp, .15f) }, MinFloor = 6 },
        };

        public static SoulSpecialOption Special(string id)
        {
            foreach (var option in SpecialOptions) if (option.Id == id) return option;
            return null;
        }

        // ── drops (§9) ───────────────────────────────────────────

        // Grade weights (%) by floor: 조잡한 … 전설의.
        static readonly float[][] floorGrades =
        {
            new[] { 70f, 28.5f, 1.5f, 0, 0, 0 },
            new[] { 55f, 41f, 3.8f, .2f, 0, 0 },
            new[] { 35f, 52f, 11.5f, 1.5f, 0, 0 },
            new[] { 20f, 50f, 24f, 5.8f, .2f, 0 },
            new[] { 10f, 40f, 34f, 14f, 2f, 0 },
            new[] { 5f, 28f, 38f, 23f, 5.8f, .2f },
            new[] { 0, 18f, 37f, 32f, 12f, 1f },
            new[] { 0, 10f, 32f, 36f, 19f, 3f },
        };

        public static float[] GradeWeights(int floor) => floorGrades[Mathf.Clamp(floor, 1, floorGrades.Length) - 1];

        static readonly float[] specialChance = { 0, .01f, .04f, .12f, .3f, 1f };

        // Chance that an item of this kind drops at all (the chest-like sources always give one).
        public static float DropChance(SoulDropSource source)
        {
            switch (source)
            {
                case SoulDropSource.Monster: return .03f;
                case SoulDropSource.Elite: return .35f;
                case SoulDropSource.Mimic: return .6f;
                default: return 1f;
            }
        }

        static bool Upgraded(SoulDropSource source)
            => source == SoulDropSource.Elite || source == SoulDropSource.Boss || source == SoulDropSource.HiddenChest || source == SoulDropSource.Mimic;

        static float SpecialScale(SoulDropSource source)
        {
            switch (source)
            {
                case SoulDropSource.Elite: case SoulDropSource.HiddenChest: case SoulDropSource.Mimic: return 2f;
                case SoulDropSource.Boss: return 3f;
                case SoulDropSource.Chest: return 1.5f;
                default: return 1f;
            }
        }

        // Luck: every point of the party's best luck makes the highest grades a little likelier (×1.02 a point,
        // on the upper half of the table), never turning the table upside down.
        public static int RollGrade(int floor, SoulDropSource source, float luck, System.Random random)
        {
            // elites, bosses and hidden treasure roll on the next floor's line
            var weights = (float[])GradeWeights(Upgraded(source) ? floor + 1 : floor).Clone();
            float boost = Mathf.Pow(1.02f, Mathf.Clamp(luck, 0, 30));
            for (int i = 2; i < weights.Length; i++) weights[i] *= boost;
            float total = 0;
            foreach (float w in weights) total += w;
            double roll = random.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++) { roll -= weights[i]; if (roll < 0) return i + 1; }
            return 1;
        }

        public static SoulItem Roll(IList<SoulEquipmentData> pool, int floor, SoulDropSource source, float luck, System.Random random)
        {
            var candidates = new List<SoulEquipmentData>();
            foreach (var data in pool) if (data != null && !data.Unique && data.DropFloor <= floor) candidates.Add(data);
            if (candidates.Count == 0) return null;
            var item = new SoulItem(candidates[random.Next(candidates.Count)], RollGrade(floor, source, luck, random)) { Dropped = true };
            AddSpecials(item, floor, source, random);
            return item;
        }

        // A dropped unique (boss loot): fixed grade, but as a drop it may still carry special options.
        public static SoulItem Drop(SoulEquipmentData data, int floor, SoulDropSource source, System.Random random)
        {
            var item = new SoulItem(data, 2) { Dropped = true };
            AddSpecials(item, floor, source, random);
            return item;
        }

        public static void AddSpecials(SoulItem item, int floor, SoulDropSource source, System.Random random)
        {
            float chance = specialChance[G(item.Grade)] * (item.Grade >= MaxGrade ? 1 : SpecialScale(source)) * (floor <= 3 ? .5f : 1f);
            int count = item.Grade >= MaxGrade ? 2 : random.NextDouble() < chance ? 1 : 0;
            var mask = Mask(item.Slot);
            for (int i = 0; i < count; i++)
            {
                var options = new List<SoulSpecialOption>();
                foreach (var option in SpecialOptions)
                    if ((option.Slots & mask) != 0 && option.MinFloor <= floor && !item.Specials.Contains(option.Id)) options.Add(option);
                if (options.Count == 0) return;
                item.Specials.Add(options[random.Next(options.Count)].Id);
            }
        }

        // Shop stock grade by shop level: 1 평범 only; 2 some 좋은; 3 some 정교한.
        public static int ShopGrade(int shopLevel, System.Random random)
        {
            double roll = random.NextDouble();
            if (shopLevel >= 3) return roll < .2 ? 4 : roll < .6 ? 3 : 2;
            if (shopLevel == 2) return roll < .3 ? 3 : 2;
            return 2;
        }
    }
}
