using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  PopupManager.cs
//  팝업 전체를 관리하는 Singleton.
//
//  사용법:
//    PopupManager.Instance.Open(PopupType.Alert);
//    var p = PopupManager.Instance.Open<AlertPopup>(PopupType.Alert);
//
//    PopupManager.Instance.Close(PopupType.Alert);
//    PopupManager.Instance.CloseTop();
//    PopupManager.Instance.CloseAll();
//
//    bool opened = PopupManager.Instance.IsOpen(PopupType.Alert);
//    var  p      = PopupManager.Instance.Get<AlertPopup>(PopupType.Alert);
// ============================================================

public class PopupManager : Singleton<PopupManager>
{
    [Header("팝업 프리팹 목록 (PopupBase.PopupType 으로 자동 분류)")]
    [SerializeField] PopupBase[] _prefabs;

    // 팝업 전용 캔버스 — 씬이 아니라 PopupManager 가 소유한다 (EnsurePopupRoot 참조)
    Transform _popupRoot;

    /// <summary>임시 캔버스를 만들 때만 쓰는 정렬 순서 (로비 0 · 인게임 10 보다 위).</summary>
    const int PopupSortingOrder = 200;

    [Header("블로커 색상")]
    [SerializeField] Color _blockerColor = new Color(0f, 0f, 0f, 0.45f);

    // ── 내부 자료구조 ─────────────────────────────────────────

    readonly Dictionary<PopupType, PopupBase> _prefabMap  = new();
    readonly Dictionary<PopupType, PopupBase> _pool       = new();   // 재사용 대기
    readonly Dictionary<PopupType, PopupBase> _openByType = new();   // 현재 열린 것
    readonly List<PopupBase>                  _stack      = new();   // 열린 순서

    GameObject _blocker;   // 단일 블로커

    // ── Unity 생명주기 ────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        // ⚠ 씬 두 개(Lobby·InGame)가 동시에 떠 있으면 PopupManager 도 둘이 된다
        //   Singleton 이 나중 것을 지우는데, 씬마다 등록된 팝업 프리팹이 다르다.
        //   그냥 사라지면 그쪽 씬의 팝업만 "등록된 프리팹이 없습니다" 로 죽는다.
        //   지워지기 전에 자기 목록을 살아남는 쪽에 넘긴다.
        if (Instance != this)
        {
            Instance.AdoptPrefabs(_prefabs);
            return;
        }

        RegisterPrefabs(_prefabs);
        EnsurePopupRoot();
    }

    /// <summary>다른 씬의 PopupManager 가 넘겨준 프리팹을 흡수한다 (이미 있는 타입은 유지).</summary>
    void AdoptPrefabs(PopupBase[] prefabs)
    {
        if (prefabs == null) return;
        foreach (var p in prefabs)
            if (p != null && !_prefabMap.ContainsKey(p.PopupType))
                _prefabMap[p.PopupType] = p;
    }

    void RegisterPrefabs(PopupBase[] prefabs)
    {
        if (prefabs == null) return;
        foreach (var p in prefabs)
            if (p != null) _prefabMap[p.PopupType] = p;
    }

    // ── 공개 API — 열기 ──────────────────────────────────────

    public PopupBase Open(PopupType type, Action onClose = null, bool noBlocker = false)
        => RegisterAndOpen(type, onClose, noBlocker);

    public T Open<T>(PopupType type, Action onClose = null, bool noBlocker = false) where T : PopupBase
        => RegisterAndOpen(type, onClose, noBlocker) as T;

    // ── 공개 API — 닫기 ──────────────────────────────────────

    public void Close(PopupType type, Action onComplete = null)
    {
        if (_openByType.TryGetValue(type, out var popup))
            popup.Close(onComplete);
    }

    public void CloseTop(Action onComplete = null)
    {
        if (_stack.Count > 0)
            _stack[_stack.Count - 1].Close(onComplete);
    }

    public void CloseAll(Action onComplete = null)
    {
        for (int i = _stack.Count - 1; i >= 0; i--)
        {
            var popup = _stack[i];
            if (popup != null && popup.IsOpen)
                popup.Close(i == 0 ? onComplete : null);
        }
    }

    // ── 공개 API — 조회 ──────────────────────────────────────

    public bool IsOpen(PopupType type) => _openByType.ContainsKey(type);
    public bool HasAnyOpen             => _stack.Count > 0;
    public int  OpenCount              => _stack.Count;

    public T Get<T>(PopupType type) where T : PopupBase
    {
        _openByType.TryGetValue(type, out var popup);
        return popup as T;
    }

    // ── 내부 — 팝업 생성·등록 ────────────────────────────────

    PopupBase RegisterAndOpen(PopupType type, Action onClose, bool noBlocker)
    {
        EnsurePopupRoot();

        if (!_prefabMap.TryGetValue(type, out var prefab))
        {
            Debug.LogError($"[PopupManager] PopupType.{type} 에 등록된 프리팹이 없습니다. PopupManager Inspector 의 Prefabs 배열을 확인하세요.");
            return null;
        }

        // 같은 타입이 이미 열려 있으면 기존 것 먼저 닫기
        if (_openByType.TryGetValue(type, out var existing))
        {
            if (existing != null)
            {
                Debug.LogWarning($"[PopupManager] {type} 이미 열려 있음 → 기존 팝업 닫기.");
                existing.Close();
            }
            else
            {
                // 씬 언로드 등으로 오브젝트가 파괴된 경우 — 스택/맵에서 제거만
                _openByType.Remove(type);
                _stack.RemoveAll(p => p == null || p.PopupType == type);
            }
        }

        // 풀에서 꺼내거나 새로 생성
        _pool.TryGetValue(type, out var popup);
        if (popup != null)
        {
            _pool.Remove(type);
        }
        else
        {
            _pool.Remove(type); // 파괴된 항목이 있으면 제거
            var go = Instantiate(prefab.gameObject, _popupRoot);
            popup = go.GetComponent<PopupBase>();
            if (popup == null)
            {
                Debug.LogError($"[PopupManager] {prefab.name} 에 PopupBase 컴포넌트가 없습니다.");
                Destroy(go);
                return null;
            }
        }

        // 등록
        _openByType[type] = popup;
        _stack.Add(popup);

        // 항상 최상위 sibling으로 이동 (pool 재사용 시 blocker보다 아래에 위치하던 버그 방지)
        popup.transform.SetAsLastSibling();

        // noBlocker = true 이면 블로커 상태를 변경하지 않음
        if (!noBlocker) UpdateBlocker();

        popup.OpenInternal(HandlePopupClosed, onClose);
        return popup;
    }

    // ── 내부 — 팝업 닫힘 콜백 ────────────────────────────────

    void HandlePopupClosed(PopupBase popup)
    {
        // 닫히는 도중 같은 타입의 새 팝업이 열렸을 수 있으므로 동일 참조일 때만 제거
        if (_openByType.TryGetValue(popup.PopupType, out var registered) && registered == popup)
            _openByType.Remove(popup.PopupType);
        _stack.Remove(popup);

        popup.gameObject.SetActive(false);
        _pool[popup.PopupType] = popup;

        UpdateBlocker();
    }

    // ── 내부 — 블로커 ─────────────────────────────────────────

    void UpdateBlocker()
    {
        if (_stack.Count == 0)
        {
            if (_blocker != null) _blocker.SetActive(false);
            return;
        }

        if (_blocker == null) _blocker = CreateBlocker();
        _blocker.SetActive(true);

        // 스택 마지막(가장 위) 팝업 바로 아래에 배치 → 2번째 팝업 열릴 때 첫 팝업이 블로커 뒤에 위치
        int idx = _stack[_stack.Count - 1].transform.GetSiblingIndex();
        _blocker.transform.SetSiblingIndex(Mathf.Max(0, idx - 1));
    }

    GameObject CreateBlocker()
    {
        var go = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_popupRoot, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color         = _blockerColor;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition    = Selectable.Transition.None;
        btn.onClick.AddListener(() =>
        {
            if (_stack.Count > 0 && _stack[_stack.Count - 1].BlockBackgroundClose) return;
            CloseTop();
        });

        return go;
    }

    // ── 내부 — 루트 보장 ─────────────────────────────────────

    /// <summary>
    /// 팝업이 붙을 자리를 확보한다.
    ///
    /// ■ 전용 캔버스는 이미 있다
    ///   씬 구조가 DontDestroyOnLoad(루트) > CanvasPopup(Canvas) > PopupManager 다.
    ///   PopupManager 는 그 캔버스 안에 들어 있고, 루트에 DontDestroyOnLoad 컴포넌트가
    ///   붙어 있어 Singleton 이 부모를 떼지 않는다 — 씬을 바꿔도 캔버스째 살아남는다.
    ///   그러니 새로 만들 것 없이 지금 자리를 그대로 쓰면 된다.
    ///
    /// ⚠ 씬 캔버스를 빌리지 않는다
    ///   예전엔 화면에서 아무 캔버스나 찾아 썼다. 씬이 하나뿐일 때는 정답이었지만
    ///   Lobby·InGame 이 함께 떠 있는 지금은 둘 중 하나를 임의로 잡게 되고,
    ///   그 캔버스가 꺼지는 순간 팝업이 통째로 사라진다.
    /// </summary>
    void EnsurePopupRoot()
    {
        if (_popupRoot != null) return;

        // ① 전용 캔버스(CanvasPopup) 안에 들어 있는 정상 배치
        if (GetComponentInParent<Canvas>() != null)
        {
            _popupRoot = transform;
            return;
        }

        // ② 캔버스를 자식으로 들고 있는 배치
        var child = GetComponentInChildren<Canvas>(includeInactive: true);
        if (child != null)
        {
            _popupRoot = child.transform;
            return;
        }

        // ③ 최후 수단 — 캔버스가 하나도 없는 배치에서만 직접 만든다.
        //   씬 캔버스를 빌리는 것보다 낫다. 로비(0)·인게임(10)보다 위로 올린다.
        var go = new GameObject("PopupCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = PopupSortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        Debug.LogWarning("[PopupManager] 전용 캔버스를 찾지 못해 임시로 만들었습니다 — CanvasPopup 아래에 두세요.");
        _popupRoot = go.transform;
    }
}
