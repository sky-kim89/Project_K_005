using UnityEngine;

// ============================================================
//  Singleton<T>          — MonoBehaviour 싱글톤 (씬에 배치 필요)
//  SingletonPure<T>      — 순수 C# 싱글톤 (씬 독립, 자동 생성)
//
//  사용법:
//  public class PoolController : Singleton<PoolController> { }
//  public class GameSession    : SingletonPure<GameSession> { }
// ============================================================

// ── 순수 C# 싱글톤 ────────────────────────────────────────────

public abstract class SingletonPure<T> where T : SingletonPure<T>, new()
{
    static T _instance;
    public static T Instance => _instance ??= new T();
}

// ── MonoBehaviour 싱글톤 ──────────────────────────────────────

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindAnyObjectByType<T>();
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this as T)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this as T;
        // 상위 오브젝트에 DontDestroyOnLoad 컴포넌트가 있으면 그쪽에서 처리하므로 스킵.
        if (GetComponentInParent<DontDestroyOnLoad>() != null) return;
        // 루트 오브젝트만 DontDestroyOnLoad 지원 — 부모가 있으면 분리 후 적용.
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this as T)
            _instance = null;
    }
}
