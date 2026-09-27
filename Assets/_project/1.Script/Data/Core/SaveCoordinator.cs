using System;
using System.Collections;
using UnityEngine;

// ============================================================
//  SaveCoordinator.cs
//  1프레임 지연 일괄 저장을 처리하는 경량 MonoBehaviour.
//
//  UserDataManager.RequestSave() 가 처음 호출될 때
//  자동으로 씬에 생성(DontDestroyOnLoad)되며,
//  같은 프레임 내 추가 요청은 무시하고 다음 프레임에 한 번만 실행한다.
//
//  직접 사용하지 말 것 — UserDataManager 가 내부적으로 관리한다.
// ============================================================

public class SaveCoordinator : MonoBehaviour
{
    static SaveCoordinator _instance;

    bool _pendingSave;
    Action _onSave;

    // ── 내부 API (UserDataManager 전용) ──────────────────────

    internal static void Request(Action onSave)
    {
        EnsureExists();
        if (!_instance._pendingSave)
        {
            _instance._onSave     = onSave;
            _instance._pendingSave = true;
            _instance.StartCoroutine(_instance.SaveNextFrame());
        }
    }

    // ── 내부 ─────────────────────────────────────────────────

    static void EnsureExists()
    {
        if (_instance != null) return;

        var go = new GameObject("[SaveCoordinator]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SaveCoordinator>();
    }

    IEnumerator SaveNextFrame()
    {
        yield return null;   // 1프레임 대기
        _onSave?.Invoke();
        _pendingSave = false;
    }
}
