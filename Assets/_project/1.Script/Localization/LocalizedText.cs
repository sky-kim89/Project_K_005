using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
//  LocalizedText
//  TMP 의 현재 한국어 문구를 키로 보관하고 언어 변경·동적 갱신 때 번역한다.
//  Creator 가 만든 고정 텍스트에는 EditorUIBuilder 가 이 컴포넌트를 붙인다.
//  기존 프리팹과 런타임 생성 TMP 는 TEXT_CHANGED_EVENT 에서 자동 보완한다.
// ============================================================

[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class LocalizedText : MonoBehaviour
{
    public static void FitLabel(TextMeshProUGUI text)
    {
        text.fontSizeMax = Mathf.Max(UIScale.FontSm, text.fontSize);
        text.fontSizeMin = UIScale.FontSm;
        text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // Keep fixed popup bounds while allowing translated descriptions to use more lines.
    public static void ScrollDescription(TextMeshProUGUI text)
    {
        var rect = text.rectTransform;
        var viewport = new GameObject(text.name + "Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D))
            .GetComponent<RectTransform>();
        viewport.SetParent(rect.parent, false);
        viewport.SetSiblingIndex(rect.GetSiblingIndex());
        viewport.anchorMin = rect.anchorMin;
        viewport.anchorMax = rect.anchorMax;
        viewport.pivot = rect.pivot;
        viewport.sizeDelta = rect.sizeDelta;
        viewport.anchoredPosition = rect.anchoredPosition;
        viewport.GetComponent<Image>().color = Color.clear;
        rect.SetParent(viewport, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        text.enableAutoSizing = false;
        text.fontSize = UIScale.FontSm;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
        var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = rect;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = UIScale.RowMd;
    }

    TextMeshProUGUI _text;
    string _sourceText;
    string _renderedText;
    bool _isSplash;
    bool _needsMeshRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InstallRuntimeHook()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnAnyTextChanged);
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnAnyTextChanged);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            EnsureIn(root);
    }

    /// <summary>
    /// Creator 재생성 전의 기존 씬·프리팹까지 포함해 하위 TMP를 모두 로컬라이징한다.
    /// 이미 붙어 있는 컴포넌트는 그대로 두므로 반복 호출해도 안전하다.
    /// </summary>
    public static void EnsureIn(GameObject root)
    {
        foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (!text.TryGetComponent(out LocalizedText _))
                text.gameObject.AddComponent<LocalizedText>();
    }

    static void OnAnyTextChanged(UnityEngine.Object changed)
    {
        if (!Application.isPlaying || changed is not TextMeshProUGUI text) return;

        var localized = text.GetComponent<LocalizedText>();
        if (localized == null)
        {
            localized = text.gameObject.AddComponent<LocalizedText>();
            localized._needsMeshRefresh = true;
        }
        // Existing components capture assignments in LateUpdate, before TMP builds meshes.
    }

    void Awake()
    {
        _text       = GetComponent<TextMeshProUGUI>();
        _sourceText = LocalizationManager.Instance.SourceForRendered(_text.text);
        _isSplash   = gameObject.scene.name == "Splash";
    }

    // 켜져 있는 모든 LocalizedText. 언어 변경 이벤트에는 RefreshAll 하나만 구독한다.
    //   ⚠ 컴포넌트마다 LanguageChanged 를 +=/-= 하면 멀티캐스트 델리게이트가 매번 전체 목록을 복사해
    //     텍스트 수의 제곱으로 느려진다 — 수천 개가 한꺼번에 꺼지는 플레이 종료가 20초 넘게 걸렸다.
    static readonly System.Collections.Generic.HashSet<LocalizedText> _live = new System.Collections.Generic.HashSet<LocalizedText>();
    static LocalizationManager _hooked;

    static void RefreshAll()
    {
        foreach (var text in new System.Collections.Generic.List<LocalizedText>(_live))
            if (text != null) text.Refresh();
    }

    void OnEnable()
    {
        _live.Add(this);
        var manager = LocalizationManager.Instance;
        if (_hooked != manager)
        {
            if (_hooked != null) _hooked.LanguageChanged -= RefreshAll;
            manager.LanguageChanged += RefreshAll;
            _hooked = manager;
        }

        // ⚠ 꺼져 있는 동안 코드가 바꿔 놓은 문구를 여기서 다시 잡아야 한다
        //   _sourceText 는 TMP 의 TEXT_CHANGED_EVENT 로만 갱신되는데,
        //   그 이벤트는 메시를 다시 그릴 때 = 활성 상태에서만 온다.
        //   꺼진 채로 .text 를 바꿔 두고 켜는 코드가 많다 —
        //     · InfoTooltipUI.Show()      Fill() 로 내용을 채운 뒤 SetActive(true)
        //     · CurrencyWidget.OnEnable() 부모가 먼저 켜지며 수량을 찍는다
        //   그 상태에서 곧장 Refresh() 하면 옛 _sourceText 로 덮어써
        //   "이전 툴팁 내용" · "전투 전 골드" 가 그대로 남는다.
        if (_text.text != _renderedText)
            _sourceText = LocalizationManager.Instance.SourceForRendered(_text.text);

        Refresh();
    }

    void OnDisable()
    {
        _live.Remove(this);
    }

    void CaptureExternalText()
    {
        if (_text.text == _renderedText) return;

        _sourceText = LocalizationManager.Instance.SourceForRendered(_text.text);
        Refresh();
    }

    // TEXT_CHANGED_EVENT runs during TMP mesh generation. Changing text there can
    // lose the dirty flag; capture runtime assignments before the canvas rebuild.
    void LateUpdate()
    {
        CaptureExternalText();
        if (_needsMeshRefresh)
        {
            _needsMeshRefresh = false;
            _text.SetAllDirty();
        }
    }

    void Refresh()
    {
        string translated = LocalizationManager.Instance.LocalizeText(_sourceText, _isSplash);
        _renderedText = translated;
        if (_text.text != translated)
            _text.text = translated;
    }
}
