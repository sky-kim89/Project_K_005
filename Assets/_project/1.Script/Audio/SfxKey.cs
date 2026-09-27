// ============================================================
//  SfxKey.cs
//  효과음 키. 값 이름이 그대로 파일명이다
//  (Assets/_project/5.Audio/SFX/<이름>.wav).
//
//  ⚠ 이름을 바꾸면 파일도 같이 바꿔야 한다
//    AudioManager 가 enum 이름으로 클립을 찾는다.
//
//  ⚠ 키를 잘게 쪼개지 않는다
//    탭 전환·이벤트 선택지·확인 버튼은 전부 그냥 버튼이다.
//    UI_Click 하나로 받고, "뒤로 가는 동작" 만 UI_Click_Back 으로 나눈다.
// ============================================================

public enum SfxKey
{
    None = 0,

    // ── UI ───────────────────────────────────────────────────
    UI_Click,        // 모든 일반 버튼
    UI_Click_Back,   // 닫기·취소·뒤로
    UI_Popup_Open,
    UI_Popup_Close,

    // ── 평타 (직업별) ────────────────────────────────────────
    ATK_Knight,      // 검
    ATK_Shield,      // 둔기·방패
    ATK_Archer,      // 활
    ATK_Mage,        // 마법탄

    // ── 액티브 스킬 ──────────────────────────────────────────
    // ActiveSkillId + 100. Creator 가 이 규칙으로 SO 를 자동 연결한다.
    SKILL_HeavyStrike       = 101,
    SKILL_VolleyFire       = 102,
    SKILL_LeapStrike       = 103,
    SKILL_HealAura         = 104,
    SKILL_TargetHeal       = 105,
    SKILL_ChargeSoldier    = 106,
    SKILL_SummonSkeleton   = 107,
    SKILL_PoisonZone       = 108,
    SKILL_Meteor           = 109,
    SKILL_Blizzard         = 110,
    SKILL_SacrificeSoldier = 111,
    SKILL_Bind             = 112,
    SKILL_SuicideSoldier   = 113,
    SKILL_Berserker        = 114,
    SKILL_IronShield       = 115,
    SKILL_ArrowRain        = 116,
    SKILL_BattleCry        = 117,
    SKILL_Shockwave        = 118,
    SKILL_SwiftStrike      = 119,
    SKILL_SummonElite      = 120,

    SKILL_Bisect           = 121,
    SKILL_ArrowStorm       = 122,
    SKILL_GravityCollapse  = 123,
    SKILL_Bulwark          = 124,
    SKILL_ChainLightning   = 125,
    SKILL_DeathSentence    = 126,
    SKILL_BloodPrice       = 127,
    SKILL_PiercingDash     = 128,
    SKILL_WarBanner        = 129,
    SKILL_Gravestone       = 130,

    SKILL_BossCharge       = 131,
    SKILL_BossSlam         = 132,
    SKILL_BossEnrage       = 133,
    SKILL_BossJumpShockwave = 134,

    // 판정보다 먼저 재생되어야 하는 선행음. 메인 키는 판정 시점에 그대로 쓴다.
    SKILL_Bisect_Cast          = 135,
    SKILL_GravityCollapse_Cast = 136,
}
