// ============================================================
//  eItem.cs
//  게임 내 재화·아이템 타입 정의 (ItemData 가 수량을 센다).
//
//  범위 규칙:
//    0~99    Currency  — 기본 재화 (금화)
//    100~199 Material  — 재료 (영혼석: 번호는 그대로, 지금은 소모품 — SoulSupplies)
//    300~399 Supply    — 영혼 용병단 소모품 (SoulSupplies 가 효과를 정의)
//
//  ⚠ 값은 세이브에 숫자로 남는다 — 기존 항목의 번호를 바꾸거나 재사용하지 말 것.
// ============================================================

public enum eItem
{
    None = -1,

    // ── 기본 재화 (Currency) ─────────────────── 0~99
    Gold                = 0,

    // ── 재료 (Material) ─────────────────────── 100~199
    SoulStone           = 106,  // 영혼석 — 소모품: 흡수하지 않은 몬스터 영혼 하나를 담아 온다

    // ── 영혼 용병단 소모품 (Supply) ──────────── 300~399
    //  물약은 벨트, 두루마리·도구는 주머니에 담겨 던전으로 내려간다.
    PotionHeal          = 300,
    PotionGreater       = 301,
    PotionStamina       = 302,
    PotionMana          = 303,
    Antidote            = 304,
    Panacea             = 305,
    ScrollHeal          = 310,
    ScrollFury          = 311,
    ScrollGuard         = 312,
    ScrollGale          = 313,
    ScrollBreath        = 314,
    ScrollPurify        = 315,
    ScrollReturn        = 316,
    ScrollMend          = 317,
    CampKit             = 320,
}

// ── 표시 이름 ────────────────────────────────────────────────

public static class ItemExtensions
{
    public static string DisplayName(this eItem item) => item switch
    {
        eItem.Gold          => "금화",
        eItem.SoulStone     => "영혼석",
        eItem.PotionHeal    => "회복 포션",
        eItem.PotionGreater => "상급 회복 포션",
        eItem.PotionStamina => "스태미나 물약",
        eItem.PotionMana    => "마나 물약",
        eItem.Antidote      => "해독제",
        eItem.Panacea       => "만능약",
        eItem.ScrollHeal    => "치유의 두루마리",
        eItem.ScrollFury    => "투지의 두루마리",
        eItem.ScrollGuard   => "수호의 두루마리",
        eItem.ScrollGale    => "질풍의 두루마리",
        eItem.ScrollBreath  => "숨결의 두루마리",
        eItem.ScrollPurify  => "정화의 두루마리",
        eItem.ScrollReturn  => "귀환 두루마리",
        eItem.ScrollMend    => "봉합의 두루마리",
        eItem.CampKit       => "야영 도구",
        _                   => item.ToString(),
    };
}
