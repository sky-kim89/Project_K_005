using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// ============================================================
//  LocalizationManager.cs
//  Resources/Localization/LocalizationTable.txt 가 모든 UI 문구의 정본이다.
//
//  - 기존 enum 영문 키와 현재 한국어 문구를 모두 조회할 수 있다.
//  - 선택 언어 값이 비어 있으면 영어를 반환한다.
//  - 최초 실행은 기기 언어를 저장하되 Splash 만 영어로 표시한다.
//  - 다음 실행부터 Splash 도 저장된 언어를 사용한다.
// ============================================================

public enum GameLanguage
{
    Korean,
    English,
    Japanese,
    ChineseSimplified,
    ChineseTraditional,
    SpanishLatinAmerica,
    PortugueseBrazil,
    German,
    French,
    Indonesian,
}

/// <summary>
/// 고른 언어의 글자를 그릴 <b>기기 폰트</b>를 찾아 TMP 폴백으로 연결한다 (본문 전용).
///
/// ⚠ 언어 선택 드롭다운은 여기를 지나지 않는다 — 가나·한자를 미리 구운
///   LanguagePickerFont 가 기본 폰트의 폴백표에 박혀 있다
///   (`아이콘·텍스처 &gt; 언어 선택 폰트`). 그쪽은 런타임 코드가 관여하지 않는다.
/// </summary>
public static class LocalizationFontFallback
{
    /// <summary>런타임에 만들어 TMP 폴백에 넣은 폰트들. 파괴되면 목록에서 걷어내야 한다.</summary>
    static readonly List<TMP_FontAsset> _runtimeFonts = new();

    /// <summary>
    /// 파괴된 런타임 폰트를 TMP 폴백 목록에서 뺀다.
    ///
    /// ⚠ 안 빼면 TMP 가 그 폰트의 아틀라스를 읽다 터진다
    ///   "MissingReferenceException: m_AtlasTextures of TMP_FontAsset doesn't exist anymore".
    ///   런타임 폰트는 HideFlags.DontSave 라 플레이 모드를 나갈 때 파괴되는데,
    ///   TMP_Settings.fallbackFontAssets 와 이 클래스의 정적 캐시는 그대로 남는다
    ///   (도메인 리로드를 끄면 특히 잘 남는다).
    /// ⚠ 하나라도 잃었으면 '확인함' 표시를 푼다 — 안 그러면 다시 등록하지 않아
    ///   그 언어만 글자가 통째로 안 보인다.
    /// </summary>
    static void PruneDestroyed()
    {
        TMP_Settings.fallbackFontAssets?.RemoveAll(font => font == null);

        if (_runtimeFonts.RemoveAll(font => font == null) <= 0) return;

        _japaneseChecked           = false;
        _simplifiedChineseChecked  = false;
        _traditionalChineseChecked = false;
        _latinChecked              = false;
    }

    static void RegisterFallback(TMP_FontAsset font)
    {
        PruneDestroyed();
        _runtimeFonts.Add(font);

        var fallbacks = TMP_Settings.fallbackFontAssets;
        if (fallbacks != null && !fallbacks.Contains(font)) fallbacks.Add(font);
    }

    /// <summary>
    /// 씬이 열리기 <b>전에</b> 폴백 목록을 청소한다.
    ///
    /// ⚠ TMP 설정 에셋은 에디터 메모리에 그대로 남는다
    ///   디스크의 TMP Settings.asset 은 폴백이 비어 있지만, 지난 플레이에서 우리가 넣은 항목은
    ///   **로드된 에셋 인스턴스**에 남는다. 그 폰트들은 플레이를 나갈 때 파괴되므로,
    ///   다음 플레이의 첫 글자가 그려지는 순간 TMP 가 사라진 아틀라스를 읽어 터진다.
    ///   LocalizationManager 가 만들어질 때 청소해서는 이미 늦다 — 스플래시가 먼저 그려진다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void PruneBeforeSceneLoad()
    {
        _runtimeFonts.Clear();

        TMP_Settings.fallbackFontAssets?.RemoveAll(font => font == null);

        _japaneseChecked           = false;
        _simplifiedChineseChecked  = false;
        _traditionalChineseChecked = false;
        _latinChecked              = false;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 플레이를 나갈 때 우리가 넣은 폴백을 되돌린다 — 파괴될 참조를 설정 에셋에 남기지 않는다.
    /// ⚠ 에디터에서만이다. 빌드에서는 앱이 통째로 내려가므로 남을 곳이 없다.
    /// </summary>
    [UnityEditor.InitializeOnLoadMethod]
    static void HookPlayModeExit()
    {
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(UnityEditor.PlayModeStateChange change)
    {
        if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode) return;

        var fallbacks = TMP_Settings.fallbackFontAssets;
        if (fallbacks != null)
            foreach (TMP_FontAsset font in _runtimeFonts)
                fallbacks.Remove(font);

        _runtimeFonts.Clear();
    }
#endif

    // ⚠ 번들 CJK 폰트를 런타임에 만드는 길은 **없앴다**
    //   NotoSansCJKsc-Regular.otf(16MB)를 Resources 밖(_project/6.Fonts/Source)으로 옮겨 빌드에서 뺐다 —
    //   이제 그것은 `아이콘·텍스처 > 언어 선택 폰트` 가 쓰는 **굽는 재료**일 뿐이다.
    //   · 언어 선택 드롭다운의 가나·한자 → 미리 구운 LanguagePickerFont 가 기본 폰트의 폴백표에서 그린다
    //   · 본문 전체의 CJK        → 아래 기기 폰트가 그린다 (없으면 EnsureFont 가 경고한다)
    //   옛 BundledFont 로 검색해 찾아온 것이라면 위 둘 중 하나를 볼 것.

    static readonly (string family, string style)[] JapaneseFonts =
    {
        ("Noto Sans CJK JP", "Regular"),
        ("Noto Sans JP", "Regular"),
        ("Noto Sans Japanese", "Regular"),
        ("Yu Gothic UI", "Regular"),
        ("Yu Gothic", "Regular"),
        ("Meiryo", "Regular"),
        ("Hiragino Sans", "W3"),
        ("Hiragino Kaku Gothic ProN", "W3"),
    };

    static readonly (string family, string style)[] SimplifiedChineseFonts =
    {
        ("Noto Sans CJK SC", "Regular"),
        ("Noto Sans SC", "Regular"),
        ("Microsoft YaHei UI", "Regular"),
        ("Microsoft YaHei", "Regular"),
        ("SimHei", "Regular"),
        ("PingFang SC", "Regular"),
        ("Heiti SC", "Regular"),
        ("Droid Sans Fallback", "Regular"),
    };

    static readonly (string family, string style)[] TraditionalChineseFonts =
    {
        ("Noto Sans CJK TC", "Regular"),
        ("Noto Sans TC", "Regular"),
        ("Microsoft JhengHei UI", "Regular"),
        ("Microsoft JhengHei", "Regular"),
        ("PingFang TC", "Regular"),
        ("Heiti TC", "Regular"),
        ("Droid Sans Fallback", "Regular"),
    };

    static readonly (string family, string style)[] LatinFonts =
    {
        ("Noto Sans", "Regular"),
        ("Roboto", "Regular"),
        ("Arial", "Regular"),
        ("Segoe UI", "Regular"),
        ("Helvetica", "Regular"),
        ("Liberation Sans", "Regular"),
    };

    static bool _japaneseChecked;
    static bool _japaneseAvailable;
    static bool _simplifiedChineseChecked;
    static bool _simplifiedChineseAvailable;
    static bool _traditionalChineseChecked;
    static bool _traditionalChineseAvailable;
    static bool _latinChecked;
    static bool _latinAvailable;

    public static bool EnsureFor(GameLanguage language)
    {
        PruneDestroyed();

        return language switch
        {
            GameLanguage.Japanese => EnsureFont(
                JapaneseFonts, "日本語", ref _japaneseChecked, ref _japaneseAvailable),
            GameLanguage.ChineseSimplified => EnsureFont(
                SimplifiedChineseFonts, "简体中文", ref _simplifiedChineseChecked, ref _simplifiedChineseAvailable),
            GameLanguage.ChineseTraditional => EnsureFont(
                TraditionalChineseFonts, "繁體中文", ref _traditionalChineseChecked, ref _traditionalChineseAvailable),
            GameLanguage.SpanishLatinAmerica or
            GameLanguage.PortugueseBrazil or
            GameLanguage.German or
            GameLanguage.French or
            GameLanguage.Indonesian => EnsureFont(
                LatinFonts, "œ", ref _latinChecked, ref _latinAvailable),
            _ => true,
        };
    }

    static bool EnsureFont(
        (string family, string style)[] candidates,
        string testCharacters,
        ref bool checkedFont,
        ref bool available)
    {
        if (checkedFont) return available;

        checkedFont = true;
        for (int i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(candidate.family, candidate.style);
            if (font == null) continue;
            bool supportsLabel = true;
            foreach (char character in testCharacters)
                supportsLabel &= font.HasCharacter(character, false, true);
            if (!supportsLabel)
            {
                UnityEngine.Object.Destroy(font);
                continue;
            }

            font.name = $"Runtime {candidate.family} {candidate.style}";
            font.hideFlags = HideFlags.DontSave;
            RegisterFallback(font);
            available = true;
            break;
        }

        // ⚠ 못 찾았으면 **못 찾았다고 말한다**
        //   예전엔 번들 폰트를 만들어 available = true 로 덮었다. 그 번들이 빌드에서 빠진 지금
        //   같은 자리에서 true 를 돌려주면 "폰트가 있다" 는 거짓말이 되고, 화면에는 □ 만 뜬다.
        //   드롭다운은 이 경로와 무관하다 — 구운 에셋이 따로 그린다.
        if (!available)
            Debug.LogWarning(
                $"[Localization] 이 기기에 \"{testCharacters}\" 를 그릴 폰트가 없습니다 — " +
                "해당 언어의 본문이 □ 로 보일 수 있습니다.");

        return available;
    }
}

public class LocalizationManager : SingletonPure<LocalizationManager>
{
    public const string TableResourcePath = "Localization/LocalizationTable";

    const string LanguagePrefKey = "ProjectK.Language";

    static readonly GameLanguage[] LanguageOrder =
    {
        GameLanguage.Korean,
        GameLanguage.English,
        GameLanguage.Japanese,
        GameLanguage.ChineseSimplified,
        GameLanguage.ChineseTraditional,
        GameLanguage.SpanishLatinAmerica,
        GameLanguage.PortugueseBrazil,
        GameLanguage.German,
        GameLanguage.French,
        GameLanguage.Indonesian,
    };

    /// <summary>
    /// 언어 선택 드롭다운에 뜨는 <b>고정 표기</b>. 각 언어를 그 언어의 글자로 적는다.
    ///
    /// ⚠ <b>번역하지 않는다. 표(LocalizationTable)를 지나지 않는다</b>
    ///   제 나라 말로 적혀 있어야 그 나라 사람이 제 언어를 찾는다. 지금 언어가 무엇이든
    ///   이 열 줄은 그대로다 — 그래서 <see cref="SetLanguage"/> 와 무관하고,
    ///   읽는 데 Instance(표 로딩)도 필요 없다.
    ///
    /// ⚠ <b>이 배열이 곧 폰트 굽기의 입력이다</b> — LanguagePickerFontCreator 가 여기서
    ///   글자를 뽑아 아틀라스를 굽는다. 언어를 더하면 폰트를 <b>다시 구울 것</b>.
    ///   안 구우면 그 줄만 □ 로 뜬다 (그 도구의 Verify 가 굽는 순간 잡아 준다).
    ///
    /// ⚠ 순서는 <see cref="LanguageOrder"/> 와 한 묶음이다 — 드롭다운 인덱스가 곧 그 배열의 자리다.
    /// </summary>
    public static readonly string[] SupportedLanguageNames =
    {
        "한국어",
        "English",
        "日本語",
        "简体中文",
        "繁體中文",
        "Español (Latinoamérica)",
        "Português (Brasil)",
        "Deutsch",
        "Français",
        "Bahasa Indonesia",
    };

    readonly Dictionary<string, Row> _byKey    = new(StringComparer.Ordinal);
    readonly Dictionary<string, Row> _byKorean = new(StringComparer.Ordinal);
    readonly Dictionary<string, Row> _byTranslation = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> _renderedSources = new(StringComparer.Ordinal);
    readonly List<Row> _replaceRows = new();
    readonly List<Row> _reverseRows = new();
    readonly bool _hadSavedLanguageAtStartup;

    GameLanguage _language;

    public event Action LanguageChanged;

    public GameLanguage CurrentLanguage => _language;
    public int CurrentLanguageIndex
    {
        get
        {
            for (int i = 0; i < LanguageOrder.Length; i++)
                if (LanguageOrder[i] == _language) return i;
            return 1;
        }
    }

    public LocalizationManager()
    {
        // ⚠ 드롭다운 표기와 언어 순서는 한 묶음이다 — 어긋나면 "일본어를 골랐는데 중국어가 켜진다"
        //   가 되는데, 화면만 보고는 원인을 짚을 수 없다. 조용히 틀리느니 여기서 터뜨린다.
        if (SupportedLanguageNames.Length != LanguageOrder.Length)
            Debug.LogError(
                $"[Localization] 드롭다운 표기 {SupportedLanguageNames.Length}개와 " +
                $"언어 순서 {LanguageOrder.Length}개가 어긋났습니다 — 둘은 같은 자리를 가리켜야 합니다.");

        LoadTable();

        _hadSavedLanguageAtStartup = PlayerPrefs.HasKey(LanguagePrefKey);
        _language = _hadSavedLanguageAtStartup
            ? ParseLanguage(PlayerPrefs.GetString(LanguagePrefKey))
            : DeviceLanguage(Application.systemLanguage);
        LocalizationFontFallback.EnsureFor(_language);
        SortReverseRows();

        if (!_hadSavedLanguageAtStartup)
            SaveLanguage();

#if UNITY_EDITOR
        CheckNumericSources();
#endif
    }

#if UNITY_EDITOR
    void CheckNumericSources()
    {
        // A translated mercenary count must never turn an unrelated currency into a count.
        Remember("30", "30명");
        Remember("1,200", "1,200명");
        Debug.Assert(SourceForRendered("30") == "30");
        Debug.Assert(SourceForRendered("1,200") == "1,200");
        Debug.Assert(SourceForRendered("+15.5%") == "+15.5%");
    }
#endif

    public void SetLanguage(GameLanguage language)
    {
        if (_language == language) return;

        _language = language;
        LocalizationFontFallback.EnsureFor(language);
        SortReverseRows();
        SaveLanguage();
        LanguageChanged?.Invoke();
    }

    public void SetLanguageIndex(int index) => SetLanguage(LanguageOrder[index]);

    public string Get(string key) => GetAndRemember(key, _language);

    public string Format(string key, params object[] args)
    {
        if (!TryGetRow(key, out Row row))
            return string.Format(key, args);

        object[] sourceArgs = SourceArgs(args);
        object[] targetArgs = TranslateArgs(sourceArgs, _language);
        string source = string.Format(row.Korean, sourceArgs);
        string result = string.Format(Value(row, _language), targetArgs);
        Remember(result, source);
        return result;
    }

    public string GetForSplash(string key)
        => GetAndRemember(key, _hadSavedLanguageAtStartup ? _language : GameLanguage.English);

    /// <summary>이미 번역되어 TMP 에 들어온 문자열에서 원래 한국어 키 문구를 복원한다.</summary>
    public string SourceForRendered(string rendered)
    {
        if (rendered == null) return string.Empty; // runtime-built TMP before its first assignment
        // Numbers are shared by currencies and stats; never infer a Korean count suffix.
        if (!HasLetters(rendered)) return rendered;
        if (_language == GameLanguage.Korean || ContainsKorean(rendered)) return rendered;
        if (_renderedSources.TryGetValue(rendered, out string source)) return source;
        if (_byTranslation.TryGetValue(rendered, out Row exact))
            return exact.Korean;

        string result = rendered;
        for (int i = 0; i < _reverseRows.Count; i++)
        {
            Row row = _reverseRows[i];
            string localized = Value(row, _language);
            if (localized.Length >= 2 && localized.Length <= 128 && HasLetters(localized) &&
                localized != row.Korean &&
                result.IndexOf(localized, StringComparison.Ordinal) >= 0)
                result = result.Replace(localized, row.Korean);
        }
        return result;
    }

    /// <summary>
    /// TMP 에 최종 조립된 문구를 번역한다. 완전 일치를 우선하고,
    /// 숫자가 끼어드는 런타임 문구는 짧은 표 항목을 긴 순서로 치환한다.
    /// </summary>
    public string LocalizeText(string source, bool splash = false)
    {
        GameLanguage language = splash && !_hadSavedLanguageAtStartup
            ? GameLanguage.English
            : _language;

        if (string.IsNullOrEmpty(source)) return source ?? string.Empty;
        if (TryGetRow(source, out Row exact))
            return Value(exact, language);

        if (language == GameLanguage.Korean || !ContainsKorean(source))
            return source;

        string result = source;
        for (int i = 0; i < _replaceRows.Count; i++)
        {
            Row row = _replaceRows[i];
            if (result.IndexOf(row.Korean, StringComparison.Ordinal) >= 0)
                result = result.Replace(row.Korean, Value(row, language));
        }
        return result;
    }

    public bool HasEntry(string key) => TryGetRow(key, out _);

    void LoadTable()
    {
        TextAsset asset = Resources.Load<TextAsset>(TableResourcePath);
        if (asset == null)
            throw new InvalidOperationException($"Localization table not found: Resources/{TableResourcePath}.txt");

        string[] lines = asset.text.Split('\n');
        for (int lineNo = 1; lineNo < lines.Length; lineNo++)
        {
            string line = lines[lineNo].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line[0] == '#') continue;

            string[] columns = line.Split('\t');
            if (columns.Length < 6 || columns.Length > 11)
                throw new FormatException($"Localization table line {lineNo + 1} must have 6 to 11 columns.");

            string english = Decode(columns[2]);

            var row = new Row(
                Decode(columns[0]), Decode(columns[1]), english, Decode(columns[3]),
                Decode(columns[4]), Decode(columns[5]),
                columns.Length > 6  ? Decode(columns[6])  : english,
                columns.Length > 7  ? Decode(columns[7])  : english,
                columns.Length > 8  ? Decode(columns[8])  : english,
                columns.Length > 9  ? Decode(columns[9])  : english,
                columns.Length > 10 ? Decode(columns[10]) : english);
            if (string.IsNullOrEmpty(row.Key) || string.IsNullOrEmpty(row.Korean))
                throw new FormatException($"Localization table line {lineNo + 1} has an empty key or Korean value.");
            if (_byKey.ContainsKey(row.Key))
                throw new FormatException($"Duplicate localization key: {row.Key}");

            _byKey.Add(row.Key, row);
            _byKorean.TryAdd(row.Korean, row);
            AddTranslation(row.English, row);
            AddTranslation(row.Japanese, row);
            AddTranslation(row.ChineseSimplified, row);
            AddTranslation(row.ChineseTraditional, row);
            AddTranslation(row.SpanishLatinAmerica, row);
            AddTranslation(row.PortugueseBrazil, row);
            AddTranslation(row.German, row);
            AddTranslation(row.French, row);
            AddTranslation(row.Indonesian, row);
        }

        _replaceRows.AddRange(_byKorean.Values);
        _replaceRows.RemoveAll(row => row.Korean.Length > 128);
        _replaceRows.Sort((a, b) => b.Korean.Length.CompareTo(a.Korean.Length));

        _reverseRows.AddRange(_byKorean.Values);
    }

    void AddTranslation(string value, Row row)
    {
        if (!string.IsNullOrEmpty(value))
            _byTranslation.TryAdd(value, row);
    }

    string GetAndRemember(string key, GameLanguage language)
    {
        if (!TryGetRow(key, out Row row)) return key;

        string result = Value(row, language);
        Remember(result, row.Korean);
        return result;
    }

    object[] SourceArgs(object[] args)
    {
        var result = new object[args.Length];
        for (int i = 0; i < args.Length; i++)
            result[i] = args[i] is string value ? SourceForRendered(value) : args[i];
        return result;
    }

    object[] TranslateArgs(object[] sourceArgs, GameLanguage language)
    {
        var result = new object[sourceArgs.Length];
        for (int i = 0; i < sourceArgs.Length; i++)
            result[i] = sourceArgs[i] is string value ? LocalizeText(value, language) : sourceArgs[i];
        return result;
    }

    string LocalizeText(string source, GameLanguage language)
    {
        if (TryGetRow(source, out Row exact)) return Value(exact, language);
        if (language == GameLanguage.Korean || !ContainsKorean(source)) return source;

        string result = source;
        for (int i = 0; i < _replaceRows.Count; i++)
        {
            Row row = _replaceRows[i];
            if (result.IndexOf(row.Korean, StringComparison.Ordinal) >= 0)
                result = result.Replace(row.Korean, Value(row, language));
        }
        return result;
    }

    void Remember(string rendered, string source)
    {
        if (rendered == source || !HasLetters(rendered)) return;
        if (_renderedSources.Count >= 2048) _renderedSources.Clear();
        _renderedSources[rendered] = source;
    }

    bool TryGetRow(string key, out Row row)
        => _byKey.TryGetValue(key, out row) || _byKorean.TryGetValue(key, out row);

    static string Value(Row row, GameLanguage language)
    {
        string value = language switch
        {
            GameLanguage.Korean => row.Korean,
            GameLanguage.Japanese => row.Japanese,
            GameLanguage.ChineseSimplified => row.ChineseSimplified,
            GameLanguage.ChineseTraditional => row.ChineseTraditional,
            GameLanguage.SpanishLatinAmerica => row.SpanishLatinAmerica,
            GameLanguage.PortugueseBrazil => row.PortugueseBrazil,
            GameLanguage.German => row.German,
            GameLanguage.French => row.French,
            GameLanguage.Indonesian => row.Indonesian,
            _ => row.English,
        };
        return string.IsNullOrEmpty(value) ? row.English : value;
    }

    static GameLanguage DeviceLanguage(SystemLanguage language)
        => language switch
        {
            SystemLanguage.Korean   => GameLanguage.Korean,
            SystemLanguage.Japanese => GameLanguage.Japanese,
            SystemLanguage.ChineseSimplified => GameLanguage.ChineseSimplified,
            SystemLanguage.ChineseTraditional => GameLanguage.ChineseTraditional,
            SystemLanguage.Spanish => GameLanguage.SpanishLatinAmerica,
            SystemLanguage.Portuguese => GameLanguage.PortugueseBrazil,
            SystemLanguage.German => GameLanguage.German,
            SystemLanguage.French => GameLanguage.French,
            SystemLanguage.Indonesian => GameLanguage.Indonesian,
            _ => GameLanguage.English,
        };

    static GameLanguage ParseLanguage(string value)
        => Enum.TryParse(value, out GameLanguage language) ? language : GameLanguage.English;

    static bool ContainsKorean(string value)
    {
        for (int i = 0; i < value.Length; i++)
            if (value[i] is >= '\uac00' and <= '\ud7a3') return true;
        return false;
    }

    static bool HasLetters(string value)
    {
        if (string.IsNullOrEmpty(value)) return false; // TMP created at runtime reports its text before it is set
        foreach (char c in value)
            if (char.IsLetter(c)) return true;
        return false;
    }

    static string Decode(string value)
        => value.Replace("\\n", "\n").Replace("\\t", "\t");

    void SaveLanguage()
    {
        PlayerPrefs.SetString(LanguagePrefKey, _language.ToString());
        PlayerPrefs.Save();
    }

    void SortReverseRows()
        => _reverseRows.Sort((a, b) =>
            Value(b, _language).Length.CompareTo(Value(a, _language).Length));

    readonly struct Row
    {
        public readonly string Key;
        public readonly string Korean;
        public readonly string English;
        public readonly string Japanese;
        public readonly string ChineseSimplified;
        public readonly string ChineseTraditional;
        public readonly string SpanishLatinAmerica;
        public readonly string PortugueseBrazil;
        public readonly string German;
        public readonly string French;
        public readonly string Indonesian;

        public Row(
            string key,
            string korean,
            string english,
            string japanese,
            string chineseSimplified,
            string chineseTraditional,
            string spanishLatinAmerica,
            string portugueseBrazil,
            string german,
            string french,
            string indonesian)
        {
            Key     = key;
            Korean  = korean;
            English = english;
            Japanese = japanese;
            ChineseSimplified = chineseSimplified;
            ChineseTraditional = chineseTraditional;
            SpanishLatinAmerica = spanishLatinAmerica;
            PortugueseBrazil = portugueseBrazil;
            German = german;
            French = french;
            Indonesian = indonesian;
        }
    }
}
