using UnityEngine;
using P = IconGenerator.P;

// ============================================================
//  IconArt.cs  [Editor Only]
//  아이콘 생성기들이 공유하는 "조합형" 아트 키트.
//
//  ■ 왜 만들었나
//    기존 어빌리티 아이콘 46장은 배경 2종(일반/고급)·테두리 1종에
//    심볼 8개를 돌려 쓰고 있었다. a01 과 a16, a10 과 a13 처럼
//    같은 그림이 색만 다른 경우까지 있어 전부 비슷해 보였다.
//
//  ■ 해법 — 아이콘 하나 = 5개 축의 조합
//      배경 6 × 테두리 5 × 글리프 28 × 뱃지 8 × 강조색
//    같은 글리프를 써야 하는 스킬끼리도 배경·테두리·뱃지가 달라 구분된다.
//
//  좌표: 좌상단 (0,0), y 증가 = 아래. 48×48 기준으로 중심 (24,24).
//  다른 크기(64 등)도 동작하도록 W/H 비율로 계산한다.
// ============================================================

public static class IconArt
{
    public enum Bg    { Radial, Diagonal, Split, Burst, Halo, Plate }
    public enum Frame { Round, Cut, Double, Notch, Rivet }
    public enum Badge { None, Plus, Minus, Up, Down, Star, Clock, Percent, Bolt }

    public enum Glyph
    {
        Soldiers, Sword, Shield, Heart, Bolt, Boot, Drop, Skull,
        Crown, Flame, Eye, Chain, Hourglass, Star, Coin, Gear,
        Bow, Aura, Horn, Banner, Potion, Fist, Anvil, Scales,
        Arrows, Pulse, Cross, Spiral,
    }

    public readonly struct Style
    {
        public readonly Glyph   G;
        public readonly Color32 Accent;
        public readonly Bg      Back;
        public readonly Frame   Fr;
        public readonly Badge   Bd;

        public Style(Glyph g, Color32 accent, Bg back, Frame fr, Badge bd)
        { G = g; Accent = accent; Back = back; Fr = fr; Bd = bd; }
    }

    // ══════════════════════════════════════════════════════════
    //  조립
    // ══════════════════════════════════════════════════════════

    public static void Compose(P p, Style s)
    {
        DrawBg(p, s.Back, s.Accent);
        DrawGlyph(p, s.G, s.Accent);
        Gloss(p);
        Vignette(p);
        DrawFrame(p, s.Fr, s.Accent);
        DrawBadge(p, s.Bd, s.Accent);
    }

    /// <summary>
    /// 이미 그려진 아이콘 위에 공통 마감만 얹는다.
    /// 액티브 스킬처럼 심볼이 이미 스킬마다 따로 그려져 있는 경우,
    /// 그림은 그대로 두고 테두리·명암만 통일해 한 벌로 보이게 만든다.
    /// </summary>
    public static void Overlay(P p, Color32 accent, Frame fr, Badge bd = Badge.None)
    {
        Gloss(p);
        Vignette(p);
        DrawFrame(p, fr, accent);
        DrawBadge(p, bd, accent);
    }

    /// <summary>좌상단 사선 하이라이트 — 평평한 그림에 유리질 입체감을 준다.</summary>
    static void Gloss(P p)
    {
        int W = p.W, H = p.H;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            if (!In(p, x, y)) continue;
            float d = (x + y) / (float)(W + H);          // 0 = 좌상단
            if (d > 0.42f) continue;
            byte a = (byte)Mathf.RoundToInt(Mathf.Lerp(38f, 0f, d / 0.42f));
            if (a > 0) p.BlendPixel(x, y, new Color32(255, 255, 255, a));
        }
    }

    /// <summary>가장자리를 어둡게 — 중앙 심볼이 앞으로 떠 보인다.</summary>
    static void Vignette(P p)
    {
        int W = p.W, H = p.H, cx = W / 2, cy = H / 2;
        float maxD = Mathf.Sqrt(cx * cx + cy * cy);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            if (!In(p, x, y)) continue;
            float t = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / maxD;
            if (t < 0.55f) continue;
            byte a = (byte)Mathf.RoundToInt(Mathf.Lerp(0f, 120f, (t - 0.55f) / 0.45f));
            p.BlendPixel(x, y, new Color32(4, 5, 10, a));
        }
    }

    // ══════════════════════════════════════════════════════════
    //  배경 — 전부 둥근 모서리 마스크 안에서만 그린다
    // ══════════════════════════════════════════════════════════

    const int CornerR = 10;

    /// <summary>둥근 사각형 마스크 안쪽인가.</summary>
    static bool In(P p, int x, int y)
    {
        if (x < 0 || y < 0 || x >= p.W || y >= p.H) return false;
        int W = p.W, H = p.H, R = CornerR;
        int dx = 0, dy = 0;
        if      (x < R      && y < R)      { dx = R - x;           dy = R - y; }
        else if (x >= W - R && y < R)      { dx = x - (W - R - 1); dy = R - y; }
        else if (x < R      && y >= H - R) { dx = R - x;           dy = y - (H - R - 1); }
        else if (x >= W - R && y >= H - R) { dx = x - (W - R - 1); dy = y - (H - R - 1); }
        else return true;
        return dx * dx + dy * dy <= R * R;
    }

    static void Fill(P p, System.Func<int, int, Color32> f)
    {
        for (int y = 0; y < p.H; y++)
        for (int x = 0; x < p.W; x++)
            if (In(p, x, y)) p.BlendPixel(x, y, f(x, y));
    }

    // 강조색에서 어두운 바탕 두 단계를 만든다 — 색만 바꿔도 계열이 유지된다
    static void Shades(Color32 accent, out Color32 dark, out Color32 mid)
    {
        Color a = accent;
        dark = Color32.Lerp(new Color32(6, 7, 14, 255),  a, 0.10f);
        mid  = Color32.Lerp(new Color32(14, 16, 30, 255), a, 0.30f);
    }

    static void DrawBg(P p, Bg bg, Color32 accent)
    {
        Shades(accent, out var dark, out var mid);
        int W = p.W, H = p.H, cx = W / 2, cy = H / 2;

        switch (bg)
        {
            case Bg.Radial:
            {
                float maxD = Mathf.Sqrt(cx * cx + cy * cy);
                Fill(p, (x, y) =>
                {
                    float t = Mathf.Clamp01(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / maxD);
                    return Color32.Lerp(mid, dark, t * t);
                });
                break;
            }
            case Bg.Diagonal:
            {
                Fill(p, (x, y) => ((x + y) / 6) % 2 == 0 ? mid : dark);
                break;
            }
            case Bg.Split:
            {
                Fill(p, (x, y) => y < cy ? mid : dark);
                // 경계선을 강조색으로 한 줄
                for (int x = 0; x < W; x++) if (In(p, x, cy)) p.BlendPixel(x, cy, Alpha(accent, 150));
                break;
            }
            case Bg.Burst:
            {
                Fill(p, (x, y) => dark);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * 30f * Mathf.Deg2Rad;
                    int ex = cx + Mathf.RoundToInt(Mathf.Cos(a) * W);
                    int ey = cy + Mathf.RoundToInt(Mathf.Sin(a) * H);
                    RayMasked(p, cx, cy, ex, ey, mid);
                }
                break;
            }
            case Bg.Halo:
            {
                Fill(p, (x, y) => dark);
                int r = Mathf.RoundToInt(W * 0.34f);
                for (int t = 0; t < 3; t++)
                    RingMasked(p, cx, cy, r - t * 4, Alpha(mid, (byte)(230 - t * 60)));
                break;
            }
            case Bg.Plate:
            {
                Fill(p, (x, y) => dark);
                int bandH = Mathf.RoundToInt(H * 0.42f);
                int top   = cy - bandH / 2;
                for (int y = top; y < top + bandH; y++)
                for (int x = 0; x < W; x++)
                    if (In(p, x, y)) p.BlendPixel(x, y, mid);
                for (int x = 0; x < W; x++)
                {
                    if (In(p, x, top))             p.BlendPixel(x, top,             Alpha(accent, 120));
                    if (In(p, x, top + bandH - 1)) p.BlendPixel(x, top + bandH - 1, Alpha(accent, 120));
                }
                break;
            }
        }
    }

    static void RayMasked(P p, int x0, int y0, int x1, int y1, Color32 c)
    {
        int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy, x = x0, y = y0;
        while (true)
        {
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
                if (In(p, x + ox, y + oy)) p.BlendPixel(x + ox, y + oy, c);
            if (x == x1 && y == y1) break;
            if (x < -4 || y < -4 || x > p.W + 4 || y > p.H + 4) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 <  dx) { err += dx; y += sy; }
        }
    }

    static void RingMasked(P p, int cx, int cy, int r, Color32 c)
    {
        for (int y = cy - r - 1; y <= cy + r + 1; y++)
        for (int x = cx - r - 1; x <= cx + r + 1; x++)
        {
            int d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
            if (d2 >= (r - 1) * (r - 1) && d2 <= (r + 1) * (r + 1) && In(p, x, y))
                p.BlendPixel(x, y, c);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  테두리
    // ══════════════════════════════════════════════════════════

    static void DrawFrame(P p, Frame fr, Color32 accent)
    {
        int W = p.W, H = p.H;
        Color32 dim = Alpha(accent, 200);

        switch (fr)
        {
            case Frame.Round:
                p.RoundedBorder(CornerR, 2, accent);
                break;

            case Frame.Cut:
                // 좌상 · 우하 모서리를 잘라낸 사선 테두리
                p.RoundedBorder(CornerR, 2, dim);
                p.DrawLine(0, 12, 12, 0, accent, 3);
                p.DrawLine(W - 13, H - 1, W - 1, H - 13, accent, 3);
                break;

            case Frame.Double:
                p.RoundedBorder(CornerR, 3, accent);
                InnerRect(p, 6, 1, Alpha(accent, 130));
                break;

            case Frame.Notch:
                // 위아래 굵은 띠 + 좌우 얇은 선
                p.FillRect(0, 0, W, 3, accent);
                p.FillRect(0, H - 3, W, 3, accent);
                p.FillRect(0, 0, 1, H, dim);
                p.FillRect(W - 1, 0, 1, H, dim);
                break;

            case Frame.Rivet:
                p.RoundedBorder(CornerR, 2, accent);
                int m = 9;
                p.FillCircle(m,     m,     2, accent);
                p.FillCircle(W - m, m,     2, accent);
                p.FillCircle(m,     H - m, 2, accent);
                p.FillCircle(W - m, H - m, 2, accent);
                break;
        }
    }

    static void InnerRect(P p, int inset, int thick, Color32 c)
    {
        int W = p.W, H = p.H;
        p.FillRect(inset, inset, W - inset * 2, thick, c);
        p.FillRect(inset, H - inset - thick, W - inset * 2, thick, c);
        p.FillRect(inset, inset, thick, H - inset * 2, c);
        p.FillRect(W - inset - thick, inset, thick, H - inset * 2, c);
    }

    // ══════════════════════════════════════════════════════════
    //  뱃지 — 우하단 소형 표식 (증감·확률·지속시간 등을 한 글자로)
    // ══════════════════════════════════════════════════════════

    static void DrawBadge(P p, Badge bd, Color32 accent)
    {
        if (bd == Badge.None) return;

        int cx = p.W - 11, cy = p.H - 11, r = 8;
        Color32 plate = new Color32(8, 9, 16, 240);
        Color32 ink   = Lighten(accent, 0.35f);

        p.FillCircle(cx, cy, r, plate);
        p.DrawCircle(cx, cy, r - 1, 1, accent);

        switch (bd)
        {
            case Badge.Plus:
                p.FillRect(cx - 4, cy - 1, 9, 2, ink);
                p.FillRect(cx - 1, cy - 4, 2, 9, ink);
                break;
            case Badge.Minus:
                p.FillRect(cx - 4, cy - 1, 9, 2, ink);
                break;
            case Badge.Up:
                p.FillTri(cx, cy - 5, cx - 4, cy + 3, cx + 4, cy + 3, ink);
                break;
            case Badge.Down:
                p.FillTri(cx, cy + 5, cx - 4, cy - 3, cx + 4, cy - 3, ink);
                break;
            case Badge.Star:
                for (int i = 0; i < 5; i++)
                {
                    float a1 = (i * 72 - 90) * Mathf.Deg2Rad;
                    float a2 = (i * 72 - 54) * Mathf.Deg2Rad;
                    p.DrawLine(cx + Round(Mathf.Cos(a1) * 5), cy + Round(Mathf.Sin(a1) * 5),
                               cx + Round(Mathf.Cos(a2) * 2), cy + Round(Mathf.Sin(a2) * 2), ink, 1);
                }
                p.FillCircle(cx, cy, 1, ink);
                break;
            case Badge.Clock:
                p.DrawCircle(cx, cy, 4, 1, ink);
                p.DrawLine(cx, cy, cx, cy - 3, ink, 1);
                p.DrawLine(cx, cy, cx + 2, cy + 1, ink, 1);
                break;
            case Badge.Percent:
                p.FillCircle(cx - 2, cy - 2, 1, ink);
                p.FillCircle(cx + 2, cy + 2, 1, ink);
                p.DrawLine(cx + 3, cy - 4, cx - 3, cy + 4, ink, 1);
                break;
            case Badge.Bolt:
                p.FillTri(cx + 1, cy - 5, cx - 3, cy + 1, cx + 1, cy + 1, ink);
                p.FillTri(cx - 1, cy + 5, cx + 3, cy - 1, cx - 1, cy - 1, ink);
                break;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  글리프 — 중앙 (24,24) 기준, 대략 30×30 안에 들어간다
    // ══════════════════════════════════════════════════════════

    static void DrawGlyph(P p, Glyph g, Color32 accent)
    {
        Color32 lit  = Lighten(accent, 0.45f);
        Color32 core = accent;
        Color32 dk   = Darken(accent, 0.45f);
        int cx = p.W / 2, cy = p.H / 2;

        switch (g)
        {
            case Glyph.Soldiers:      // 사람 셋
                Person(p, cx,      cy + 2, 7, core, dk);
                Person(p, cx - 10, cy + 4, 6, lit,  dk);
                Person(p, cx + 10, cy + 4, 6, lit,  dk);
                break;

            case Glyph.Sword:
                p.FillRect(cx - 2, cy - 14, 4, 20, lit);
                p.FillRect(cx - 1, cy - 13, 2, 18, core);
                p.FillRect(cx - 7, cy + 6, 14, 3, dk);      // 가드
                p.FillRect(cx - 1, cy + 9, 2, 5, dk);       // 손잡이
                p.FillTri(cx - 2, cy - 14, cx + 2, cy - 14, cx, cy - 18, lit);
                break;

            case Glyph.Shield:
                ShieldShape(p, cx, cy, 13, core, dk);
                p.DrawLine(cx, cy - 9, cx, cy + 9, lit, 2);
                break;

            case Glyph.Heart:
                p.FillCircle(cx - 5, cy - 3, 6, core);
                p.FillCircle(cx + 5, cy - 3, 6, core);
                p.FillTri(cx - 11, cy - 1, cx + 11, cy - 1, cx, cy + 13, core);
                p.FillCircle(cx - 4, cy - 5, 2, lit);
                break;

            case Glyph.Bolt:
                p.FillTri(cx + 3, cy - 15, cx - 8, cy + 3, cx + 2, cy + 3, core);
                p.FillTri(cx - 3, cy + 15, cx + 8, cy - 3, cx - 2, cy - 3, core);
                p.DrawLine(cx + 2, cy - 12, cx - 4, cy + 1, lit, 1);
                break;

            case Glyph.Boot:
                p.FillRect(cx - 7, cy - 10, 7, 15, core);
                p.FillRRect(cx - 7, cy + 3, 17, 7, 2, core);
                p.FillRect(cx - 7, cy + 8, 17, 2, dk);
                // 속도선
                p.DrawLine(cx + 6, cy - 8, cx + 14, cy - 8, lit, 1);
                p.DrawLine(cx + 4, cy - 3, cx + 13, cy - 3, lit, 1);
                break;

            case Glyph.Drop:
                p.FillCircle(cx, cy + 4, 8, core);
                p.FillTri(cx - 8, cy + 4, cx + 8, cy + 4, cx, cy - 13, core);
                p.FillCircle(cx - 3, cy + 3, 2, lit);
                break;

            case Glyph.Skull:
                p.FillCircle(cx, cy - 2, 11, core);
                p.FillRect(cx - 6, cy + 6, 12, 6, core);
                p.FillCircle(cx - 4, cy - 3, 3, dk);
                p.FillCircle(cx + 4, cy - 3, 3, dk);
                p.FillRect(cx - 1, cy + 2, 2, 3, dk);
                p.FillRect(cx - 5, cy + 8, 2, 4, dk);
                p.FillRect(cx + 3, cy + 8, 2, 4, dk);
                break;

            case Glyph.Crown:
                p.FillTri(cx - 13, cy + 6, cx - 13, cy - 8, cx - 6, cy + 2, core);
                p.FillTri(cx,      cy + 6, cx,      cy - 12, cx + 6, cy + 2, core);
                p.FillTri(cx + 13, cy + 6, cx + 13, cy - 8, cx + 6, cy + 2, core);
                p.FillRect(cx - 13, cy + 5, 27, 5, core);
                p.FillRect(cx - 13, cy + 9, 27, 2, dk);
                p.FillCircle(cx, cy - 12, 2, lit);
                break;

            case Glyph.Flame:
                p.FillTri(cx, cy - 15, cx - 9, cy + 8, cx + 9, cy + 8, core);
                p.FillCircle(cx, cy + 6, 8, core);
                p.FillTri(cx, cy - 6, cx - 4, cy + 8, cx + 4, cy + 8, lit);
                p.FillCircle(cx, cy + 6, 4, lit);
                break;

            case Glyph.Eye:
                p.FillEllipse(cx, cy, 15, 9, dk);
                p.FillEllipse(cx, cy, 13, 7, core);
                p.FillCircle(cx, cy, 5, dk);
                p.FillCircle(cx, cy, 3, lit);
                break;

            case Glyph.Chain:
                p.DrawCircle(cx - 7, cy - 5, 5, 2, core);
                p.DrawCircle(cx + 1, cy + 1, 5, 2, core);
                p.DrawCircle(cx + 9, cy + 7, 5, 2, lit);
                break;

            case Glyph.Hourglass:
                p.FillRect(cx - 10, cy - 13, 20, 3, core);
                p.FillRect(cx - 10, cy + 10, 20, 3, core);
                p.FillTri(cx - 9, cy - 10, cx + 9, cy - 10, cx, cy, core);
                p.FillTri(cx - 9, cy + 10, cx + 9, cy + 10, cx, cy, core);
                p.FillTri(cx - 5, cy - 7, cx + 5, cy - 7, cx, cy - 1, lit);
                break;

            case Glyph.Star:
                for (int i = 0; i < 5; i++)
                {
                    float a1 = (i * 72 - 90) * Mathf.Deg2Rad;
                    float a2 = (i * 72 - 54) * Mathf.Deg2Rad;
                    float a3 = (i * 72 - 18) * Mathf.Deg2Rad;
                    p.FillTri(cx + Round(Mathf.Cos(a1) * 14), cy + Round(Mathf.Sin(a1) * 14),
                              cx + Round(Mathf.Cos(a2) * 6),  cy + Round(Mathf.Sin(a2) * 6),
                              cx + Round(Mathf.Cos(a3) * 6),  cy + Round(Mathf.Sin(a3) * 6), core);
                }
                p.FillCircle(cx, cy, 6, core);
                p.FillCircle(cx, cy, 3, lit);
                break;

            case Glyph.Coin:
                p.FillCircle(cx, cy, 13, dk);
                p.FillCircle(cx, cy, 11, core);
                p.DrawCircle(cx, cy, 8, 1, lit);
                p.FillRect(cx - 1, cy - 6, 3, 12, dk);
                p.FillRect(cx - 4, cy - 4, 9, 2, dk);
                p.FillRect(cx - 4, cy + 2, 9, 2, dk);
                break;

            case Glyph.Gear:
                p.FillCircle(cx, cy, 11, core);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    int gx = cx + Round(Mathf.Cos(a) * 13);
                    int gy = cy + Round(Mathf.Sin(a) * 13);
                    p.FillCircle(gx, gy, 3, core);
                }
                p.FillCircle(cx, cy, 5, dk);
                break;

            case Glyph.Bow:
                for (int i = -12; i <= 12; i++)
                {
                    int bx = cx - 6 + Round(Mathf.Sqrt(Mathf.Max(0, 144 - i * i)) * 0.55f);
                    p.FillCircle(bx, cy + i, 2, core);
                }
                p.DrawLine(cx - 6, cy - 12, cx - 6, cy + 12, lit, 1);
                p.DrawLine(cx - 6, cy, cx + 12, cy, lit, 2);
                p.FillTri(cx + 12, cy - 3, cx + 12, cy + 3, cx + 16, cy, lit);
                break;

            case Glyph.Aura:
                p.FillCircle(cx, cy, 5, core);
                p.DrawCircle(cx, cy, 9,  1, Alpha(core, 200));
                p.DrawCircle(cx, cy, 13, 1, Alpha(core, 140));
                p.DrawCircle(cx, cy, 17, 1, Alpha(core, 80));
                p.FillCircle(cx, cy, 2, lit);
                break;

            case Glyph.Horn:
                for (int i = 0; i < 14; i++)
                {
                    int hx = cx - 12 + i * 2;
                    int hr = 2 + i / 3;
                    p.FillCircle(hx, cy + 4 - i / 2, hr, core);
                }
                p.DrawLine(cx + 8, cy - 8, cx + 15, cy - 12, lit, 1);
                p.DrawLine(cx + 9, cy - 3, cx + 16, cy - 4, lit, 1);
                break;

            case Glyph.Banner:
                p.FillRect(cx - 12, cy - 14, 3, 28, dk);
                p.FillRect(cx - 9, cy - 12, 20, 15, core);
                p.FillTri(cx - 9, cy + 3, cx + 11, cy + 3, cx + 1, cy + 11, core);
                p.FillRect(cx - 5, cy - 8, 12, 3, lit);
                break;

            case Glyph.Potion:
                p.FillRect(cx - 4, cy - 14, 8, 6, dk);
                p.FillCircle(cx, cy + 3, 10, dk);
                p.FillCircle(cx, cy + 4, 8, core);
                p.FillCircle(cx - 3, cy + 1, 2, lit);
                break;

            case Glyph.Fist:
                p.FillRRect(cx - 11, cy - 8, 20, 18, 4, core);
                p.FillRect(cx - 11, cy - 2, 20, 2, dk);
                p.FillRect(cx - 4, cy - 8, 2, 6, dk);
                p.FillRect(cx + 2, cy - 8, 2, 6, dk);
                p.FillRRect(cx + 7, cy - 4, 6, 10, 2, core);
                break;

            case Glyph.Anvil:
                p.FillRect(cx - 13, cy - 8, 26, 7, core);
                p.FillTri(cx - 13, cy - 1, cx + 13, cy - 1, cx + 6, cy + 3, core);
                p.FillRect(cx - 5, cy + 2, 10, 6, dk);
                p.FillRect(cx - 10, cy + 8, 20, 4, core);
                p.FillRect(cx - 13, cy - 8, 26, 2, lit);
                break;

            case Glyph.Scales:
                p.FillRect(cx - 1, cy - 13, 3, 24, core);
                p.FillRect(cx - 9, cy + 11, 19, 3, core);
                p.DrawLine(cx - 12, cy - 10, cx + 12, cy - 10, core, 2);
                p.FillTri(cx - 16, cy - 8, cx - 8, cy - 8, cx - 12, cy - 1, lit);
                p.FillTri(cx + 8,  cy - 8, cx + 16, cy - 8, cx + 12, cy - 1, dk);
                break;

            case Glyph.Arrows:
                p.DrawLine(cx - 12, cy - 12, cx + 12, cy + 12, core, 3);
                p.DrawLine(cx + 12, cy - 12, cx - 12, cy + 12, lit, 3);
                p.FillTri(cx + 12, cy + 6, cx + 6, cy + 12, cx + 14, cy + 14, core);
                p.FillTri(cx - 12, cy + 6, cx - 6, cy + 12, cx - 14, cy + 14, lit);
                break;

            case Glyph.Pulse:
                p.DrawLine(cx - 15, cy, cx - 7, cy, core, 2);
                p.DrawLine(cx - 7, cy, cx - 4, cy - 10, core, 2);
                p.DrawLine(cx - 4, cy - 10, cx, cy + 9, lit, 2);
                p.DrawLine(cx, cy + 9, cx + 4, cy - 4, core, 2);
                p.DrawLine(cx + 4, cy - 4, cx + 7, cy, core, 2);
                p.DrawLine(cx + 7, cy, cx + 15, cy, core, 2);
                break;

            case Glyph.Cross:
                p.FillRRect(cx - 4, cy - 13, 9, 27, 2, core);
                p.FillRRect(cx - 13, cy - 4, 27, 9, 2, core);
                p.FillRect(cx - 2, cy - 11, 5, 23, lit);
                p.FillRect(cx - 11, cy - 2, 23, 5, lit);
                break;

            case Glyph.Spiral:
                for (int i = 0; i < 46; i++)
                {
                    float t = i / 46f;
                    float a = t * Mathf.PI * 3.2f;
                    int r = Round(3 + t * 13);
                    p.FillCircle(cx + Round(Mathf.Cos(a) * r), cy + Round(Mathf.Sin(a) * r),
                                 t > 0.6f ? 2 : 1, Color32.Lerp(lit, core, t));
                }
                break;
        }
    }

    // ── 글리프 부품 ───────────────────────────────────────────

    static void Person(P p, int cx, int cy, int s, Color32 body, Color32 dk)
    {
        p.FillCircle(cx, cy - s, Mathf.Max(2, s / 2), body);          // 머리
        p.FillTri(cx - s, cy + s, cx + s, cy + s, cx, cy - s / 2, body); // 몸통
        p.FillRect(cx - s, cy + s, s * 2, 1, dk);
    }

    static void ShieldShape(P p, int cx, int cy, int r, Color32 body, Color32 dk)
    {
        for (int y = -r; y <= r; y++)
        {
            float t = (y + r) / (2f * r);              // 0(위) → 1(아래)
            int half = Round(r * Mathf.Lerp(1.0f, 0.15f, t * t));
            for (int x = -half; x <= half; x++)
                p.BlendPixel(cx + x, cy + y - 2, body);
        }
        p.FillRect(cx - r, cy - r - 2, r * 2 + 1, 2, dk);
    }

    // ── 색 유틸 ───────────────────────────────────────────────

    static int Round(float f) => Mathf.RoundToInt(f);

    static Color32 Alpha(Color32 c, byte a) => new Color32(c.r, c.g, c.b, a);

    public static Color32 Lighten(Color32 c, float t) => Color32.Lerp(c, new Color32(255, 255, 255, c.a), t);

    public static Color32 Darken(Color32 c, float t) => Color32.Lerp(c, new Color32(10, 10, 18, c.a), t);
}
