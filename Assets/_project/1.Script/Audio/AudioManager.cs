using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  AudioManager.cs
//  효과음 + 배경음 전체를 관리하는 Singleton.
//
//  사용법:
//    AudioManager.Instance?.Play(SfxKey.UI_Click);
//    AudioManager.Instance?.Play(SfxKey.ATK_Knight, 0.5f);   // 볼륨 절반
//    AudioManager.Instance?.PlayBgm(BgmKey.Lobby);           // 같은 곡이면 무시
//
//  ■ 배경음은 소스가 따로 하나다
//    효과음은 풀에서 빌려 쓰지만(Rent) 배경음은 전용 AudioSource 를 loop 로 돌린다.
//    풀에 섞으면 다음 Rent 가 재생 중인 배경음을 덮어쓴다.
//
//  ■ 켜기/끄기는 저장 설정이 정한다 (BattleSettingsData)
//    PausePopup 의 효과음·배경음 토글이 그 값을 쓴다. 여기서는 매 프레임
//    그 값을 배경음 소스에 반영하고(LateUpdate), 효과음은 Play 입구에서 막는다.
//    → 어디서 토글하든 따로 알려 줄 필요가 없다.
//
//  ■ 클립은 Inspector 배열로 들고 있다 (Resources 안 씀)
//    PopupManager 가 프리팹 배열을 PopupType 으로 자동 분류하는 것과 같은 방식이다.
//    여기서는 클립의 **파일명이 곧 SfxKey 값**이라 이름으로 분류한다
//    (UI_Click.wav → SfxKey.UI_Click).
//
//    새 효과음은 Assets/_project/5.Audio/SFX 에 넣고 인스펙터의
//    [Load SFX Clips From Folder] 버튼을 누르면 배열이 다시 채워진다
//    (PopupManager 의 [Load Popup Prefabs From Folder] 와 같다).
//
//  ■ 키마다 예산이 따로 있다
//    전체 초당 상한 하나로 막으면, 수가 많은 소리(궁수 화살)가 예산을 전부
//    써버려 수가 적은 소리(기사 1명의 검격)가 영영 안 들린다.
//    상한을 키 단위로 걸면 화살이 잘려도 검격 예산은 그대로다 —
//    화면에서 '일어나고 있는 종류' 가 전부 귀에 남는 게 목적이다.
//
//  ■ 초과분은 버린다
//    재생 중인 소리를 끊고 새 걸 넣으면 타격음이 뚝뚝 끊겨 더 거슬린다.
//
//  ■ 배속과 무관하게 재생한다
//    TopBarUI 가 timeScale 을 1/2/3 으로 바꾸지만 소리는 늘 원래 속도다.
//    AudioSource 는 timeScale 의 영향을 받지 않으므로 아무것도 하지 않으면 된다.
//    시간 계산도 전부 unscaledTime 을 쓴다 — 일시정지(timeScale=0) 중에도
//    예산이 정상적으로 반납돼야 팝업 버튼 소리가 계속 난다.
// ============================================================

public class AudioManager : Singleton<AudioManager>
{
    [Header("효과음 클립 (파일명 = SfxKey 값으로 자동 분류)")]
    [SerializeField] AudioClip[] _clips;

    [Header("배경음 클립 (파일명 = BgmKey 값으로 자동 분류)")]
    [SerializeField] AudioClip[] _bgmClips;

    [Header("전체 볼륨")]
    [Range(0f, 1f)]
    [SerializeField] float _masterVolume = 1f;

    [Header("배경음 볼륨")]
    //  ⚠ 효과음과 같은 크기로 틀면 안 된다
    //    BGM 원본이 효과음보다 훨씬 크게 녹음돼 있어 그대로 재생하면
    //    전투음이 통째로 묻힌다. 실제로 들어 보고 0.2 까지 내렸다 —
    //    배경음은 '있는 줄 모르게' 깔려야 타격음·스킬음이 앞으로 나온다.
    [Range(0f, 1f)]
    [SerializeField] float _bgmVolume = 0.2f;

    // ── 예산 정의 ────────────────────────────────────────────
    //  PerSecond  : 1초에 이만큼까지만 재생
    //  Concurrent : 동시에 울릴 수 있는 개수
    //  Volume     : 기본 볼륨 (0~1)
    readonly struct Budget
    {
        public readonly int   PerSecond, Concurrent;
        public readonly float Volume;
        public Budget(int p, int c, float v) { PerSecond = p; Concurrent = c; Volume = v; }
    }

    static readonly Dictionary<SfxKey, Budget> Budgets = new()
    {
        // UI 는 사람이 누른 만큼만 나므로 넉넉히
        { SfxKey.UI_Click,       new Budget(20, 6, 0.55f) },
        { SfxKey.UI_Click_Back,  new Budget(20, 6, 0.55f) },
        { SfxKey.UI_Popup_Open,  new Budget(10, 4, 0.50f) },
        { SfxKey.UI_Popup_Close, new Budget(10, 4, 0.50f) },

        // 평타 — 직업마다 예산이 분리돼 있는 것이 핵심
        { SfxKey.ATK_Archer,     new Budget( 8, 6, 0.25f) },  // 사거리가 길어 발사 빈도 1위
        { SfxKey.ATK_Knight,     new Budget( 6, 5, 0.25f) },
        { SfxKey.ATK_Mage,       new Budget( 5, 4, 0.28f) },
        { SfxKey.ATK_Shield,     new Budget( 4, 3, 0.28f) },  // 수가 적고 공속도 느리다

    };

    static readonly Budget Default = new(10, 4, 0.6f);
    static readonly Budget RareSkill = new(6, 3, 0.72f);
    static readonly Budget BossSkill = new(4, 2, 0.76f);

    // ── 내부 자료구조 ─────────────────────────────────────────

    class Slot
    {
        public float WindowStart;
        public int   FiredInWindow;
        public int   Playing;
    }

    readonly Dictionary<SfxKey, AudioClip> _clipMap = new();
    readonly Dictionary<SfxKey, Slot>      _slots   = new();
    readonly List<AudioSource>             _sources = new();
    readonly List<(AudioSource src, SfxKey key, float until)> _busy = new();

    readonly HashSet<SfxKey> _warned = new();   // 없는 키 경고는 한 번만

    // ⚠ _sources(효과음 풀)에 넣지 않는다 — Rent 가 빌려 가 배경음을 덮어쓴다
    readonly Dictionary<BgmKey, AudioClip> _bgmMap = new();
    AudioSource _bgmSource;
    BgmKey      _currentBgm = BgmKey.None;
    readonly HashSet<BgmKey> _bgmWarned = new();

    // ── Unity 생명주기 ────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        foreach (var clip in _clips)
        {
            if (clip == null) continue;
            if (System.Enum.TryParse(clip.name, out SfxKey key) && key != SfxKey.None)
                _clipMap[key] = clip;
            else
                Debug.LogWarning($"[AudioManager] '{clip.name}' 은 SfxKey 에 없는 이름입니다. " +
                                 "파일명과 SfxKey 값이 같아야 분류됩니다.");
        }

        foreach (var clip in _bgmClips)
        {
            if (clip == null) continue;
            if (System.Enum.TryParse(clip.name, out BgmKey key) && key != BgmKey.None)
                _bgmMap[key] = clip;
            else
                Debug.LogWarning($"[AudioManager] '{clip.name}' 은 BgmKey 에 없는 이름입니다. " +
                                 "파일명과 BgmKey 값이 같아야 분류됩니다.");
        }

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake  = false;
        _bgmSource.loop         = true;
        _bgmSource.spatialBlend = 0f;
        _bgmSource.volume       = _bgmVolume * _masterVolume * BattleSettingsData.BgmVolume;
    }

    // 재생이 끝난 만큼 예산을 돌려준다.
    // ⚠ 여기서 Stop() 을 부르면 안 된다 — 그 사이 같은 AudioSource 가
    //   Rent() 로 재사용돼 새 소리를 내고 있을 수 있고, 그걸 끊어 버린다.
    //   AudioSource 는 클립이 끝나면 알아서 멈춘다.
    void LateUpdate()
    {
        // 배경음 설정 반영 — 토글한 쪽이 따로 알려 주지 않아도 여기서 맞춰진다.
        // (음소거는 Stop 이 아니라 mute 다 — 다시 켜면 끊긴 자리에서 이어진다)
        if (_bgmSource != null)
        {
            _bgmSource.mute   = !BattleSettingsData.BgmEnabled;
            _bgmSource.volume = _bgmVolume * _masterVolume * BattleSettingsData.BgmVolume;
        }

        float now = Time.unscaledTime;
        for (int i = _busy.Count - 1; i >= 0; i--)
        {
            if (now < _busy[i].until) continue;
            SfxKey key = _busy[i].key;
            _busy.RemoveAt(i);
            if (_slots.TryGetValue(key, out var s) && s.Playing > 0) s.Playing--;
        }
    }

    // ── 공개 API ─────────────────────────────────────────────

    public void Play(SfxKey key) => Play(key, 1f);

    /// <summary>
    /// 게임 시간 기준으로 늦춰 재생한다. 메테오·일도양단처럼 예고 뒤 판정이 나는
    /// 스킬은 배속이 바뀌어도 이펙트와 같은 프레임에 소리가 붙어야 한다.
    /// </summary>
    public void PlayDelayed(SfxKey key, float delay)
    {
        if (key == SfxKey.None) return;
        if (delay <= 0f) { Play(key); return; }
        StartCoroutine(PlayAfterDelay(key, delay));
    }

    IEnumerator PlayAfterDelay(SfxKey key, float delay)
    {
        yield return new WaitForSeconds(delay);
        Play(key);
    }

    /// <summary>volumeScale 로 개별 호출의 크기를 더 줄일 수 있다 (0~1).</summary>
    public void Play(SfxKey key, float volumeScale)
    {
        if (key == SfxKey.None) return;
        if (!BattleSettingsData.SfxEnabled) return;   // PausePopup 의 효과음 토글

        if (!_clipMap.TryGetValue(key, out var clip))
        {
            if (_warned.Add(key))
                Debug.LogError($"[AudioManager] SfxKey.{key} 에 연결된 클립이 없습니다. " +
                               "AudioManager Inspector 의 Clips 배열을 확인하세요.");
            return;
        }

        int raw = (int)key;
        Budget b = Budgets.TryGetValue(key, out var found) ? found
                 : raw is >= 121 and <= 130 or 135 or 136 ? RareSkill
                 : raw is >= 131 and <= 134 ? BossSkill
                 : Default;
        if (!TakeBudget(key, b)) return;

        var src = Rent();
        src.clip   = clip;
        src.volume = b.Volume * volumeScale * _masterVolume * BattleSettingsData.SfxVolume;

        // 스킬음은 이펙트 판정에 맞춘 완성 큐다. 피치를 흔들면 화살 폭풍의
        // 0.45초 간격 같은 내부 타이밍도 함께 틀어지므로 원속도로 재생한다.
        src.pitch = raw >= 101 ? 1f : Random.Range(0.88f, 1.12f);
        src.Play();

        _busy.Add((src, key, Time.unscaledTime + clip.length / src.pitch + 0.05f));
    }

    public void SetMasterVolume(float v) => _masterVolume = Mathf.Clamp01(v);

    // ── 공개 API — 배경음 ────────────────────────────────────

    /// <summary>지금 흐르고 있는 곡. 같은 곡을 다시 요청하면 아무 일도 하지 않는다.</summary>
    public BgmKey CurrentBgm => _currentBgm;

    /// <summary>
    /// 배경음을 바꾼다. 이미 같은 곡이 흐르고 있으면 그대로 둔다 —
    /// 로비 안에서 화면을 옮길 때마다 곡이 처음부터 다시 시작하면 안 된다.
    /// </summary>
    public void PlayBgm(BgmKey key)
    {
        if (key == BgmKey.None) { StopBgm(); return; }
        if (_bgmSource == null) return;
        if (_currentBgm == key && _bgmSource.isPlaying) return;

        if (!_bgmMap.TryGetValue(key, out var clip))
        {
            if (_bgmWarned.Add(key))
                Debug.LogError($"[AudioManager] BgmKey.{key} 에 연결된 클립이 없습니다. " +
                               "AudioManager Inspector 의 [Load Audio Clips From Folder] 를 누르세요.");
            return;
        }

        _currentBgm       = key;
        _bgmSource.clip   = clip;
        _bgmSource.volume = _bgmVolume * _masterVolume * BattleSettingsData.BgmVolume;
        _bgmSource.Play();
    }

    public void StopBgm()
    {
        _currentBgm = BgmKey.None;
        _bgmSource?.Stop();
    }

    // ── 내부 — 예산 ──────────────────────────────────────────

    bool TakeBudget(SfxKey key, Budget b)
    {
        if (!_slots.TryGetValue(key, out var s))
            _slots[key] = s = new Slot { WindowStart = Time.unscaledTime };

        float now = Time.unscaledTime;
        if (now - s.WindowStart >= 1f)
        {
            s.WindowStart   = now;
            s.FiredInWindow = 0;
        }

        if (s.FiredInWindow >= b.PerSecond)  return false;
        if (s.Playing       >= b.Concurrent) return false;

        s.FiredInWindow++;
        s.Playing++;
        return true;
    }

    // ── 내부 — AudioSource 풀 ────────────────────────────────

    AudioSource Rent()
    {
        foreach (var s in _sources)
            if (!s.isPlaying) return s;

        var src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake  = false;
        src.spatialBlend = 0f;      // 2D — 유닛이 화면에 가득 차 있어 정위감이 의미 없다
        _sources.Add(src);
        return src;
    }
}
