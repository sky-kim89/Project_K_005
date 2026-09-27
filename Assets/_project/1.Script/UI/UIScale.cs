using UnityEngine;

// ============================================================
//  UIScale.cs
//  UI 사이즈 공통 상수.
//  모든 Creator 스크립트가 이 값을 참조한다.
//
//  레이아웃별 세부 수치(패딩, 패널 높이 등)는
//  각 Creator 안에서 자유롭게 결정한다.
// ============================================================

public static class UIScale
{
    // ── 기준 해상도 (Canvas Scaler) ───────────────────────────
    //  ⚠ 실제 씬 설정과 다르다.
    //    Lobby  캔버스 = 1920×1080 (가로)  ← 팝업 대부분이 여기 뜬다
    //    InGame 캔버스 = 1080×1920 (세로)
    //    아래 폰트 값은 **세로 여유 1080** 기준으로 잡혀 있다.
    public const float RefWidth  = 1080f;
    public const float RefHeight = 1920f;
    public const float Match     = 0.5f;   // matchWidthOrHeight

    // ── 폰트 크기 ──────────────────────────────────────────────
    //  모바일 실기 확인 후 상향 (2026-08-05).
    //  세로 1080 캔버스에서 24는 1080p 단말 기준 실측 약 1.5mm 로,
    //  안드로이드 최소 권장(14sp ≈ 35px)에 한참 못 미쳤다.
    //  작은 단계일수록 더 크게 올렸다.
    public const float FontSm = 34f;   // 24 → 34  보조 설명, 수치
    public const float FontMd = 42f;   // 30 → 42  일반 텍스트, 탭 레이블
    public const float FontLg = 56f;   // 44 → 56  섹션 제목
    public const float FontXl = 76f;   // 62 → 76  팝업 강조 제목

    // ── 버튼 높이 ──────────────────────────────────────────────
    //  폰트가 커진 만큼 같이 올려야 라벨이 눌린다.
    public const float BtnSm  = 100f;  //  90 → 100  탭, 소형 버튼
    public const float BtnMd  = 132f;  // 120 → 132  일반 버튼
    public const float BtnLg  = 164f;  // 150 → 164  주요 액션 (전투 시작 등)

    // ── 아이콘 크기 (정사각형) ─────────────────────────────────
    public const float IconSm = 64f;   // 재화 아이콘, 인라인 아이콘
    public const float IconMd = 96f;   // 카드 썸네일, 패널 아이콘

    // ── 텍스트가 실제로 차지하는 높이 ──────────────────────────
    //  글자를 담는 칸의 높이를 폰트보다 작게 잡으면 아래가 잘린다.
    //  TMP 한 줄은 폰트 크기의 약 1.25배(디센더 포함)를 쓴다.
    //  칸 높이를 손으로 적지 말고 아래 값을 기준으로 잡을 것.
    public static float Line(float fontSize) => Mathf.Ceil(fontSize * 1.25f);

    public static readonly float RowSm = Line(FontSm);   //  43 — 보조 텍스트 한 줄
    public static readonly float RowMd = Line(FontMd);   //  53 — 일반 텍스트 한 줄
    public static readonly float RowLg = Line(FontLg);   //  70 — 섹션 제목 한 줄

    /// <summary>라벨이 눌리지 않는 최소 버튼 높이 (좌우 여백 포함).</summary>
    public static float BtnFor(float fontSize) => Mathf.Ceil(fontSize * 1.7f);

    // ── 캔버스 한계 ────────────────────────────────────────────
    //  Lobby 캔버스가 1920×1080 이라 팝업이 쓸 수 있는 세로는 1080 뿐이다.
    //  팝업 높이는 이 값을 넘기면 위아래가 잘린다.
    public const float LobbyCanvasH = 1080f;
    public const float PopupMaxH    = 1000f;   // 위아래 40 여백 확보
}
