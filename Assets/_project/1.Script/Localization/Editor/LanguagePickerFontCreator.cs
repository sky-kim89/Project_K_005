#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// ============================================================
//  LanguagePickerFontCreator.cs  [Editor Only]
//  언어 선택 드롭다운에 뜨는 글자만 담은 TMP 폰트 에셋을 굽는다.
//
//  사용: Tools > Project K > 아이콘·텍스처 > 언어 선택 폰트
//  산출: Assets/_project/6.Fonts/LanguagePickerFont.asset
//        + LiberationSans SDF 의 폴백표에 자동 등록
//
//  ══════════════════════════════════════════════════════════
//  ■ 왜 필요한가 — 이 프로젝트에는 가나·한자를 그릴 폰트가 없다
//  ══════════════════════════════════════════════════════════
//
//    기본 폰트 LiberationSans SDF 의 폴백표는 둘뿐이다.
//      · TDS_RPG_2 SDF          정적 · 글리프 11,172자가 **전부 한글** (가나 0 · 한자 0)
//      · LiberationSans SDF - Fallback  동적 · 원본이 LiberationSans.ttf 라 라틴뿐
//
//    그래서 일본어·중국어는 **기기(윈도우·안드로이드 등)에 깔린 폰트**를 런타임에
//    폰트 에셋으로 만들어 그려 왔다 (LocalizationFontFallback). 본문은 그것으로 되지만
//    **드롭다운만은 안 된다** — 한국어를 쓰는 중에도 목록에는 日本語·简体中文 이
//    제 나라 글자로 동시에 떠야 하기 때문이다. 기기에 그 폰트가 없거나 런타임 폰트가
//    파괴되면 그 줄만 빈칸이 된다 (2026-09-16 에 실제로 그랬다).
//
//    이 도구는 그 줄에 필요한 글자만 **미리 구워** 문제의 뿌리를 없앤다.
//
//  ══════════════════════════════════════════════════════════
//  ■ 왜 열 줄 전부가 아니라 "모자란 글자만" 인가
//  ══════════════════════════════════════════════════════════
//
//    라틴은 기본 폰트가 이미 갖고 있고(Español 의 ñ·é, Português 의 ê,
//    Français 의 ç 까지 Latin-1 안이다) 한글은 TDS_RPG_2 가 갖고 있다.
//    실제로 모자란 것은 **가나·한자 아홉 자 남짓**이라 512×512 한 장이면 넉넉하다.
//
//    ⚠ 목록을 손으로 적지 않는다 — LocalizationManager.SupportedLanguageNames 에서
//      뽑아 기본 폰트에 **실제로 없는 것**만 남긴다. 그래야 언어를 더했을 때
//      이 파일을 함께 고치는 것을 잊어도 조용히 틀리지 않는다.
//
//  ⚠ 언어를 추가하면 이 도구를 다시 돌릴 것. 안 돌리면 그 줄만 □ 로 뜬다.
//    (Verify 가 굽는 순간 어느 글자가 빠졌는지 이름을 대고 에러를 낸다)
//
//  ⚠ 원본 .otf 는 16MB 다. 그것은 **굽는 재료일 뿐** 결과물에는 아홉 자만 들어간다.
// ============================================================

public static class LanguagePickerFontCreator
{
    // ⚠ Resources 밖이다 (2026-09-16) — 이 16MB 는 **굽는 재료일 뿐** 빌드에 실리지 않는다.
    //   Resources 안에 두면 아무도 안 써도 통째로 빌드에 들어간다.
    const string SourceFont  = "Assets/_project/6.Fonts/Source/NotoSansCJKsc-Regular.otf";
    const string PrimaryFont = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string OutputPath  = "Assets/_project/6.Fonts/LanguagePickerFont.asset";
    const string AssetName   = "LanguagePickerFont";
    const string Tag         = "LanguagePickerFontCreator";

    // ── 굽기 설정 ────────────────────────────────────────────
    //
    //  ⚠ 한자는 획이 빽빽해 SamplingSize 를 낮추면 뭉갠다.
    //    90 은 드롭다운 글자 크기(UIScale.FontSm~FontMd)에서 획이 살아 있는 하한이다.
    //    글자가 열 자 남짓이라 이렇게 키워도 512 한 장에 들어간다.
    const int SamplingSize = 90;
    const int Padding      = 9;
    const int AtlasSize    = 512;

    [MenuItem(ProjectKMenu.Icon + "언어 선택 폰트", priority = ProjectKMenu.IconPrio + 17)]
    public static void Create()
    {
        var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFont);
        if (source == null)
        {
            Debug.LogError($"[{Tag}] 원본 글꼴을 찾지 못했습니다 — {SourceFont}");
            return;
        }

        var primary = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PrimaryFont);
        if (primary == null)
        {
            Debug.LogError($"[{Tag}] 기본 폰트를 찾지 못했습니다 — {PrimaryFont}");
            return;
        }

        // ⚠ 먼저 우리 것을 폴백표에서 뺀다
        //   빼지 않으면 두 번째 실행부터 "이미 그릴 수 있다" 로 판정돼 한 글자도 안 굽는다.
        DetachSelf(primary);

        string needed = CollectMissing(primary);
        if (needed.Length == 0)
        {
            Debug.LogWarning($"[{Tag}] 기본 폰트가 이미 모든 글자를 그릴 수 있습니다 — 굽지 않았습니다. " +
                             "CJK 폰트를 폴백에 따로 넣었다면 이 도구는 필요 없습니다.");
            return;
        }

        // ⚠ 동적으로 만들어 글자를 채운 뒤 정적으로 잠근다
        //   정적으로 시작하면 글리프를 추가할 수 없어 빈 아틀라스가 나온다
        //   (DamageFontCreator 와 같은 순서다).
        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(
            source, SamplingSize, Padding, GlyphRenderMode.SDFAA,
            AtlasSize, AtlasSize,
            AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);

        if (font == null)
        {
            Debug.LogError($"[{Tag}] 폰트 에셋 생성에 실패했습니다 — {SourceFont} 를 읽을 수 없습니다.");
            return;
        }

        font.name = AssetName;
        font.TryAddCharacters(needed, out string missing);

        font.atlasPopulationMode = AtlasPopulationMode.Static;
        font.ReadFontAssetDefinition();

        if (!Verify(font, needed, missing)) return;

        Save(font);
        Attach(primary, font);

        Debug.Log($"[{Tag}] 완료 — {OutputPath} " +
                  $"(글자 {font.characterTable.Count}자 / 아틀라스 {AtlasSize}×{AtlasSize} 1장) · " +
                  $"{primary.name} 폴백표에 등록했습니다.");
    }

    // ── 무엇을 구울 것인가 ───────────────────────────────────

    /// <summary>
    /// 드롭다운 열 줄에서 <b>기본 폰트가 못 그리는 글자만</b> 모은다.
    ///
    /// ⚠ 폴백까지 훑어서 판정한다 (<c>searchFallbacks: true</c>) — 한글은 TDS_RPG_2 가
    ///   이미 갖고 있으므로 여기서 빠진다. 같은 글자를 두 번 구울 이유가 없다.
    /// ⚠ <c>tryAddCharacter: false</c> 여야 한다 — true 면 동적 폴백이 그 자리에서
    ///   글리프를 만들어 "그릴 수 있다" 고 답해 버린다. 그건 런타임에만 참인 답이다.
    /// </summary>
    static string CollectMissing(TMP_FontAsset primary)
    {
        var seen = new HashSet<char>();
        var sb   = new StringBuilder();

        foreach (string name in LocalizationManager.SupportedLanguageNames)
            foreach (char ch in name)
            {
                if (ch == ' ' || !seen.Add(ch)) continue;
                if (primary.HasCharacter(ch, searchFallbacks: true, tryAddCharacter: false)) continue;
                sb.Append(ch);
            }

        return sb.ToString();
    }

    /// <summary>
    /// 필요한 글자가 실제로 다 들어갔는지 본다.
    ///
    /// ⚠ 조용한 실패를 시끄러운 실패로 바꾸는 자리다 — 글리프가 빠져도 TMP 는
    ///   에러를 내지 않고 화면에 □ 를 그릴 뿐이다. 언어를 더하고 이 도구를 돌렸는데
    ///   원본 글꼴에 그 글자가 없으면 여기서 이름을 대고 멈춘다.
    /// </summary>
    static bool Verify(TMP_FontAsset font, string needed, string missing)
    {
        if (!string.IsNullOrEmpty(missing))
        {
            Debug.LogError($"[{Tag}] 원본 글꼴에 없는 글자가 있습니다 — \"{missing}\". " +
                           $"그 글자는 화면에서 □ 로 나옵니다. 다른 글꼴이 필요합니다 ({SourceFont}).");
            return false;
        }

        var absent = new StringBuilder();
        foreach (char ch in needed)
            if (!font.HasCharacter(ch, searchFallbacks: false, tryAddCharacter: false))
                absent.Append(ch);

        if (absent.Length > 0)
        {
            Debug.LogError($"[{Tag}] 아틀라스에 담기지 못한 글자가 있습니다 — \"{absent}\". " +
                           $"아틀라스({AtlasSize}×{AtlasSize})가 모자랍니다 — AtlasSize 를 키우거나 " +
                           "SamplingSize 를 낮추고 다시 구울 것.");
            return false;
        }

        return true;
    }

    // ── 폴백표 연결 ──────────────────────────────────────────
    //
    //  ⚠ 런타임에 등록하지 않고 **에셋에 박는다** (사용자 지시, 2026-09-16)
    //    한글이 TDS_RPG_2 로 그려지는 것과 같은 길이다. 런타임 생성 폰트는
    //    플레이를 나갈 때 파괴돼 MissingReferenceException 을 남기지만,
    //    에셋 참조는 그런 수명이 없다 — 코드가 아예 관여하지 않는다.

    static void DetachSelf(TMP_FontAsset primary)
    {
        List<TMP_FontAsset> table = primary.fallbackFontAssetTable;
        if (table == null) return;

        table.RemoveAll(f => f == null || f.name == AssetName);
        EditorUtility.SetDirty(primary);
    }

    static void Attach(TMP_FontAsset primary, TMP_FontAsset font)
    {
        primary.fallbackFontAssetTable ??= new List<TMP_FontAsset>();

        // ⚠ 맨 앞에 둔다 — 한자·가나를 그릴 수 있는 유일한 폴백이라,
        //   동적 폴백(LiberationSans SDF - Fallback)이 먼저 걸려 빈 글리프를 만들기 전에
        //   이쪽이 답하게 한다.
        primary.fallbackFontAssetTable.Insert(0, font);

        EditorUtility.SetDirty(primary);
        AssetDatabase.SaveAssets();
    }

    // ── 저장 ─────────────────────────────────────────────────

    /// <summary>
    /// 에셋으로 굳힌다. 아틀라스·머티리얼은 <b>서브에셋</b>으로 붙인다 —
    /// 따로 두면 폰트만 옮겼을 때 조용히 분홍색(머티리얼 없음)이 된다.
    ///
    /// ⚠ Resources 밖에 둔다 — Resources 안의 것은 쓰이든 안 쓰이든 전부 빌드에 실린다.
    ///   이 에셋은 LiberationSans SDF 가 참조하므로 그 의존으로 저절로 빌드에 들어간다.
    /// </summary>
    static void Save(TMP_FontAsset font)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

        // 통째로 다시 만든다 — 옛 서브에셋이 남아 아틀라스가 두 장이 되는 것을 막는다.
        if (File.Exists(OutputPath)) AssetDatabase.DeleteAsset(OutputPath);

        AssetDatabase.CreateAsset(font, OutputPath);

        for (int i = 0; i < font.atlasTextures.Length; i++)
        {
            Texture2D atlas = font.atlasTextures[i];
            if (atlas == null) continue;

            atlas.name = font.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, font);
        }

        font.material.name = font.name + " Material";
        AssetDatabase.AddObjectToAsset(font.material, font);

        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
#endif
