// ============================================================
//  PureSingleton<T>
//  MonoBehaviour 없는 순수 C# 싱글톤 베이스.
//
//  특징:
//  - new() 를 통해 파생 클래스가 자기 자신을 생성 (protected 생성자)
//  - 씬 전환과 무관하게 앱 종료까지 유지
//  - 스레드 안전 (lock 기반 지연 초기화)
//
//  사용법:
//    public class UserDataManager : PureSingleton<UserDataManager>
//    {
//        protected UserDataManager() { }
//        protected override void OnInitialize() { ... }
//    }
//    UserDataManager.Instance.DoSomething();
// ============================================================

public abstract class PureSingleton<T> where T : PureSingleton<T>, new()
{
    static T _instance;
    static readonly object _lock = new();

    public static T Instance
    {
        get
        {
            if (_instance != null) return _instance;

            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = new T();
                    _instance.OnInitialize();
                }
            }
            return _instance;
        }
    }

    // 파생 클래스가 new() 로 외부 생성되지 않도록 보호
    protected PureSingleton() { }

    /// <summary>처음 Instance 에 접근할 때 1회 호출. 초기화 로직을 여기에 작성.</summary>
    protected virtual void OnInitialize() { }
}
