using System;

// ============================================================
//  StatType.cs
//  스텟 시스템 관련 enum / 구조체 정의
// ============================================================

// ── 스텟 종류 ─────────────────────────────────────────────────
public enum StatType
{
    MaxHp        = 0,
    Defense      = 1,   // 방어율 0~1
    Attack       = 2,
    AttackRange  = 3,
    AttackSpeed  = 4,   // 초당 공격 횟수
    MoveSpeed    = 5,
    CritChance    = 6,   // 크리티컬 확률 0~1
    CritDamage    = 7,   // 크리티컬 배율 (기본 1.5)
    SoldierCount         = 8,   // 장군이 지휘하는 병사 수
    CommandPower         = 9,   // 병사 지휘력 — 1포인트당 병사 스텟 1% 증가
    SkillCooldownReduce  = 10,  // 스킬 쿨다운 감소율 (0~1, 예: 0.1 = 10% 감소)
    // 전투 스텟이지만 번호는 15 — 아래 '시스템 전용' 뒤에 있다. DefensePenetration 참고.

    // ── 시스템 전용 스탯 (TraitData.Effects 에서만 사용) ──────
    GeneralSlotBonus     = 11,  // 장수 배치 슬롯 추가 수 (NormalMode 에서 집계)
    AllStatPenalty       = 12,  // 전체 능력치 패널티 비율 (TraitApplier 에서 집계 후 적용)
    EquipSlotBonus       = 13,  // 장비 슬롯 추가 수 (TraitApplier 에서 집계)
    ExpGainBonus         = 14,  // 경험치 획득 비율 가산 (InGameManager 전투 보상에서 집계)

    /// <summary>
    /// 방어율 관통 (0~1). 공격자가 가진 값만큼 <b>대상의 최종 방어율에서 그대로 뺀다.</b>
    ///   대상 방어율 0.70, 관통 0.10 → 실효 방어율 0.60 (피해 0.30 → 0.40)
    ///
    /// ⚠ 곱연산(방어율 × (1-관통))이 아니다
    ///   Defense 는 이 게임에서 "깎이는 비율" 자체다. 곱연산으로 하면 방어율이
    ///   높을수록 관통 1%p 의 값어치가 폭증해(0.9 → 0.81 은 피해 90% 증가) 후반
    ///   보스가 한 스택만 쌓여도 아군이 즉사한다. 뺄셈은 스택 수에 비례해
    ///   선형으로 오르므로 광폭화 스택 설계와 맞는다.
    ///
    /// 실제 적용은 DamageMath.AfterDefense 한 곳뿐이다. 공격자 → HitEvent(발사체면
    /// ProjectileComponent) → 피격 계산 순서로 값이 실려 간다.
    /// </summary>
    DefensePenetration   = 15,

    // Soul Mercenaries: upper stats. Existing combat IDs remain stable for serialized assets.
    Strength = 16, Vitality = 17, Agility = 18, Magic = 19, Will = 20,
    Luck = 21, Regeneration = 22, Durability = 23, AntiMagic = 24,
    Recovery = 25, SlashPower = 26, ImpactPower = 27, PiercePower = 28,

    // Calculated combat stats. Current HP/MP/stamina live in runtime resource components.
    MaxMp = 29, MaxStamina = 30, Armor = 31, PhysicalResist = 32,
    MagicResist = 33, HpRegen = 34, MpRegen = 35, StaminaRegen = 36,
    Accuracy = 37, Evasion = 38, ActionSpeed = 39,
    BleedResist = 40, PoisonResist = 41, BurnResist = 42,
    ChillResist = 43, FearResist = 44, StunResist = 45, PetrifyResist = 46,

    // Passive effect stats (race/soul passives). KnockbackResist 0~0.9, PatternCostReduce 0~0.3.
    KnockbackResist = 47, PatternCostReduce = 48,
    // Threat: monsters prefer targets with high threat (taunt). BodyWeight: body data mirrored as a stat so
    // skill formulas can reference it (leap shockwave). ExecuteBonus: extra melee damage ratio vs low-HP targets.
    Threat = 49, BodyWeight = 50, ExecuteBonus = 51,
    // Chance (0~1) that a pattern is used without paying its stamina (passive 무념 동작).
    FreePatternChance = 52,
    // Stamina passives: cost cut per consecutive use of one attack (몸에 익은 무기), walking speed kept when out of
    // stamina (피로 무시, added to the base 50%), cost cut under 35% HP (아드레날린).
    RepeatCostReduce = 53, TirelessWalk = 54, LowHpCostReduce = 55,
    // Elements (equipment): extra damage of that element on a weapon hit, and the share of that element's
    // damage taken away (0~0.75). Toxic is the poison element (PoisonResist stays the status resist).
    FireAttack = 56, ColdAttack = 57, LightningAttack = 58, EarthAttack = 59, WindAttack = 60, ToxicAttack = 61,
    FireResist = 62, ColdResist = 63, LightningResist = 64, EarthResist = 65, WindResist = 66, ToxicResist = 67,
    // Worn weight ÷ carry limit (set by the stat pipeline): above 1 patterns cost more stamina.
    LoadRatio = 68,
    // Special options (dungeon drops only).
    LifeSteal = 69, KillStamina = 70, EliteDamage = 71, DoubleHit = 72, ArmorShred = 73, FreeSkillChance = 74,
    Thorns = 75, DeathDefy = 76, WoundResist = 77, GoldFind = 78, LootFind = 79, SoulFind = 80, CursedHp = 81,
    // Soul Mercenaries: magic power (0.36 = +36%) - every mana skill's effect scales with it (SoulSkillUsePolicy.SpellPower).
    SpellPower = 82,
    // Share (0~0.75) of the knockdowns (wounded and out of breath) a mercenary stands through; it also shortens the fall.
    DownResist = 83,
    // 마나 보호막: share (0~0.8) of the damage taken that MP pays for instead (1 MP a point).
    ManaShield = 84,
    // 결정 반사: share of the magic damage taken that goes back to the caster.
    SpellReflect = 85,
    // The roster's traits (용병 특징): situational bonuses, auras and small rules — SoulStatRules.CombatName says each.
    DuelFocus = 86,
    CrowdFury = 87,
    DarkSight = 88,
    FearAura = 89,
    RegenAura = 90,
    GuardAura = 91,
    DefyRage = 92,
    BookDiscount = 93,
    FireFocus = 94,
    ChainBonus = 95,
    BattleCaster = 96,
    PotionPower = 97,
    HolyBane = 98,
    StatusGrade = 99,
    KillMana = 100,
    LowHpDamage = 101,
    WoundMend = 102,
    BuffDuration = 103,
    ManaRegenRate = 104,   // MP 회복 속도 (비율, 명상)
    StaffBound = 105,      // 지팡이 아닌 무기를 들면 마법 효과가 이만큼 감소 (비율, 마법사·성직자·소환사)

    // 새 스텟은 여기에 순서대로 추가 — 다른 코드 수정 불필요 (최대 127개)
}

// ── 레이어 간 결합 방식 ────────────────────────────────────────
public enum CombineMode
{
    Add,
    Multiply,
    Max,

    /// <summary>
    /// 감소율 곱연산 — 각 레이어가 "남은 양"을 순서대로 깎는다.
    ///     결과 = 1 - Π(1 - 레이어값)
    /// 쿨타임 감소처럼 여러 출처가 겹치는 감소율에 쓴다.
    ///
    /// 출처가 하나면 값이 그대로 나온다 (10% → 10%). 겹칠 때만 완만해진다
    /// (10% + 10% → 19%). 구조상 100% 를 넘지 않는다.
    /// 음수(페널티) 레이어는 (1-v) > 1 이 되어 총량을 되돌린다 — 의도된 동작.
    /// </summary>
    MultiplyResidual,
}

// ── 단일 스텟 수정자 ───────────────────────────────────────────
[Serializable]
public struct StatModifier
{
    public StatType Type;
    public float    Value;

    [UnityEngine.Tooltip("레이어 키 — 비워두면 base 레이어에 추가됩니다.\n" +
                         "예) equip_sword / buff_rage / skill_passive")]
    public string Key;
}
