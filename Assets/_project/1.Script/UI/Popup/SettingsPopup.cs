using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 공통 설정 팝업. 빌드 타깃의 Unity 기본 디파인으로 플랫폼 전용 행만 남긴다.
// Android : 효과음 · 배경음 · 언어 · 알림
// Steam   : 효과음 · 배경음 · 언어 · 해상도 · 전체 화면
public sealed class SettingsPopup : PopupBase
{
    const string ResolutionWidthPrefKey = "ProjectK.SteamResolutionWidth";
    const string ResolutionHeightPrefKey = "ProjectK.SteamResolutionHeight";
    const string FullscreenPrefKey = "ProjectK.SteamFullscreen";

    [Header("헤더")]
    [SerializeField] Button _closeButton;
    [SerializeField] TextMeshProUGUI _platformText;

    [Header("공통")]
    [SerializeField] Slider _sfxSlider;
    [SerializeField] TextMeshProUGUI _sfxValue;
    [SerializeField] Slider _bgmSlider;
    [SerializeField] TextMeshProUGUI _bgmValue;
    [SerializeField] TMP_Dropdown _languageDropdown;

    [Header("Android")]
    [SerializeField] Button _notificationButton;
    [SerializeField] Image _notificationPill;
    [SerializeField] TextMeshProUGUI _notificationState;

    [Header("Steam")]
    [SerializeField] Button _resolutionButton;
    [SerializeField] Image _resolutionPill;
    [SerializeField] TextMeshProUGUI _resolutionState;
    [SerializeField] Button _fullscreenButton;
    [SerializeField] Image _fullscreenPill;
    [SerializeField] TextMeshProUGUI _fullscreenState;

    [Header("플랫폼별 높이")]
    [SerializeField] RectTransform _panelRect;
    [SerializeField] RectTransform _borderRect;
    [SerializeField] float _androidHeight;
    [SerializeField] float _steamHeight;
    [SerializeField] float _sharedHeight;

    static readonly Color PillOn    = new(0.16f, 0.50f, 0.34f, 1f);
    static readonly Color PillOff   = new(0.28f, 0.28f, 0.34f, 1f);
    static readonly Color PillValue = new(0.18f, 0.34f, 0.58f, 1f);

    protected override void OnBeforeOpen()
    {
        // 언어 이름은 원어 고정 — 지금 언어로 되번역하지 않는다.
        // ⚠ 글꼴은 건드리지 않는다 — 가나·한자는 LanguagePickerFont 가 기본 폰트 폴백표에서 그린다
        //   (PausePopup.OnBeforeOpen 주석 참고).
        foreach (var label in _languageDropdown.GetComponentsInChildren<LocalizedText>(true))
            label.enabled = false;
        _closeButton.onClick.RemoveAllListeners();
        _closeButton.onClick.AddListener(() => Close());
        _sfxSlider.onValueChanged.RemoveAllListeners();
        _sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        _bgmSlider.onValueChanged.RemoveAllListeners();
        _bgmSlider.onValueChanged.AddListener(SetBgmVolume);
        _languageDropdown.onValueChanged.RemoveAllListeners();
        _languageDropdown.ClearOptions();
        _languageDropdown.AddOptions(new List<string>(LocalizationManager.SupportedLanguageNames));
        _languageDropdown.onValueChanged.AddListener(SetLanguage);

        ApplyPlatform();
        RefreshCommon();
    }

    void ApplyPlatform()
    {
#if UNITY_ANDROID
        _platformText.text = "ANDROID";
        _notificationButton.gameObject.SetActive(true);
        _resolutionButton.gameObject.SetActive(false);
        _fullscreenButton.gameObject.SetActive(false);
        SetHeight(_androidHeight);

        _notificationButton.onClick.RemoveAllListeners();
        _notificationButton.onClick.AddListener(ToggleNotifications);
        RefreshNotifications();
#elif UNITY_STANDALONE
        _platformText.text = "STEAM";
        _notificationButton.gameObject.SetActive(false);
        _resolutionButton.gameObject.SetActive(true);
        _fullscreenButton.gameObject.SetActive(true);
        SetHeight(_steamHeight);

        _resolutionButton.onClick.RemoveAllListeners();
        _resolutionButton.onClick.AddListener(ToggleResolution);
        _fullscreenButton.onClick.RemoveAllListeners();
        _fullscreenButton.onClick.AddListener(ToggleFullscreen);
        RefreshDisplay();
#else
        _platformText.text = "EDITOR";
        _notificationButton.gameObject.SetActive(false);
        _resolutionButton.gameObject.SetActive(false);
        _fullscreenButton.gameObject.SetActive(false);
        SetHeight(_sharedHeight);
#endif
    }

    void SetHeight(float height)
    {
        _panelRect.sizeDelta = new Vector2(_panelRect.sizeDelta.x, height);
        _borderRect.sizeDelta = new Vector2(_borderRect.sizeDelta.x, height + 6f);
    }

    void SetSfxVolume(float volume)
    {
        var settings = UserDataManager.Instance.Get<BattleSettingsData>();
        settings.SetSfxVolume(volume);
        UserDataManager.Instance.RequestSave();
        _sfxValue.text = VolumeText(volume);
    }

    void SetBgmVolume(float volume)
    {
        var settings = UserDataManager.Instance.Get<BattleSettingsData>();
        settings.SetBgmVolume(volume);
        UserDataManager.Instance.RequestSave();
        _bgmValue.text = VolumeText(volume);
    }

    void SetLanguage(int index)
    {
        var localization = LocalizationManager.Instance;
        localization.SetLanguageIndex(index);
        ApplyPlatform();
        RefreshCommon();
    }

    void RefreshCommon()
    {
        var settings = UserDataManager.Instance.Get<BattleSettingsData>();
        _sfxSlider.SetValueWithoutNotify(settings.SavedSfxVolume);
        _sfxValue.text = VolumeText(settings.SavedSfxVolume);
        _bgmSlider.SetValueWithoutNotify(settings.SavedBgmVolume);
        _bgmValue.text = VolumeText(settings.SavedBgmVolume);

        var localization = LocalizationManager.Instance;
        _languageDropdown.SetValueWithoutNotify(localization.CurrentLanguageIndex);
        _languageDropdown.RefreshShownValue();
    }

    void ToggleNotifications()
    {
        AndroidLocalNotificationManager.ToggleFromSettings();
        RefreshNotifications();
    }

    void RefreshNotifications()
    {
        if (AndroidLocalNotificationManager.PermissionNeeded)
        {
            _notificationPill.color = new Color(0.58f, 0.38f, 0.12f, 1f);
            _notificationState.text = LocalizationManager.Instance.Get("권한 필요");
            return;
        }

        SetToggle(_notificationPill, _notificationState,
                  AndroidLocalNotificationManager.Enabled);
    }

    void ToggleResolution()
    {
        List<Vector2Int> resolutions = AvailableResolutions();
        int index = (FindResolutionIndex(resolutions, SelectedResolution) + 1) % resolutions.Count;
        Vector2Int selected = resolutions[index];
        PlayerPrefs.SetInt(ResolutionWidthPrefKey, selected.x);
        PlayerPrefs.SetInt(ResolutionHeightPrefKey, selected.y);
        PlayerPrefs.Save();
        ApplySteamDisplay();
        RefreshDisplay();
    }

    void ToggleFullscreen()
    {
        PlayerPrefs.SetInt(FullscreenPrefKey, Fullscreen ? 0 : 1);
        PlayerPrefs.Save();
        ApplySteamDisplay();
        RefreshDisplay();
    }

    void RefreshDisplay()
    {
        Vector2Int resolution = SelectedResolution;
        _resolutionPill.color = PillValue;
        _resolutionState.text = $"{resolution.x} × {resolution.y}";
        SetToggle(_fullscreenPill, _fullscreenState, Fullscreen);
    }

    static Vector2Int SelectedResolution
    {
        get
        {
            List<Vector2Int> resolutions = AvailableResolutions();
            var saved = new Vector2Int(
                PlayerPrefs.GetInt(ResolutionWidthPrefKey, Screen.width),
                PlayerPrefs.GetInt(ResolutionHeightPrefKey, Screen.height));
            int index = FindResolutionIndex(resolutions, saved);
            return index >= 0 ? resolutions[index] : resolutions[^1];
        }
    }

    static bool Fullscreen => PlayerPrefs.HasKey(FullscreenPrefKey)
        ? PlayerPrefs.GetInt(FullscreenPrefKey) != 0
        : Screen.fullScreenMode != FullScreenMode.Windowed;

#if UNITY_STANDALONE && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
    static void ApplySteamDisplay()
    {
#if UNITY_STANDALONE && !UNITY_EDITOR
        if (!PlayerPrefs.HasKey(ResolutionWidthPrefKey) &&
            !PlayerPrefs.HasKey(FullscreenPrefKey))
            return;

        Vector2Int resolution = SelectedResolution;
        Screen.SetResolution(resolution.x, resolution.y,
            Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
    }

    static List<Vector2Int> AvailableResolutions()
    {
        var result = new List<Vector2Int>();
        foreach (Resolution resolution in Screen.resolutions)
        {
            var size = new Vector2Int(resolution.width, resolution.height);
            if (size.x < 1024 || size.y < 576 || result.Contains(size)) continue;
            result.Add(size);
        }

        var current = new Vector2Int(Screen.width, Screen.height);
        if (current.x >= 1024 && current.y >= 576 && !result.Contains(current))
            result.Add(current);

        if (result.Count == 0)
            result.Add(current);

        result.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return result;
    }

    static int FindResolutionIndex(IReadOnlyList<Vector2Int> resolutions, Vector2Int target)
    {
        for (int i = 0; i < resolutions.Count; i++)
            if (resolutions[i] == target) return i;
        return -1;
    }

    static string VolumeText(float volume) => $"{Mathf.RoundToInt(volume * 100f)}%";

    void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_ANDROID
        if (hasFocus && IsOpen) RefreshNotifications();
#endif
    }

    static void SetToggle(Image pill, TextMeshProUGUI label, bool enabled)
    {
        pill.color = enabled ? PillOn : PillOff;
        label.text = LocalizationManager.Instance.Get(enabled ? "켜짐" : "꺼짐");
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void CheckResolutionSelection()
    {
        var resolutions = new List<Vector2Int> { new(1280, 720), new(1280, 800) };
        Debug.Assert(FindResolutionIndex(resolutions, new Vector2Int(1280, 800)) == 1);
    }
#endif
}
