using System.Collections;
using UnityEngine;

// ============================================================
//  CameraShaker.cs
//  착탄·폭발의 "쿵" 을 만드는 카메라 흔들림.
//
//  ■ 쓰는 법
//      CameraShaker.Impulse(0.3f);            // 화면 중앙 기준
//      CameraShaker.Impulse(0.3f, worldPos);  // 화면 밖이면 약해진다
//
//  ■ 트라우마 방식
//    충격은 _trauma 에 누적되고 제곱으로 흔들림 세기가 된다.
//    선형이면 약한 충격이 과하게 느껴지고 강한 충격이 밋밋해진다.
//    누적은 1 로 클램프 — 비석 12기처럼 연타가 들어와도 화면이 터지지 않는다.
//
//  ⚠ 흔들림은 "렌더 직전에 얹고 프레임 끝에 뺀다"
//    BattleScrollManager 는 LateUpdate 에서 카메라 위치를 **읽어서** 스크롤을
//    계산하고 다시 쓴다. 오프셋을 프레임 너머로 남겨두면 그 값이 스크롤 위치로
//    흡수돼 카메라가 슬금슬금 밀린다(드리프트).
//    그래서 ExecutionOrder 를 가장 뒤로 밀어 모든 로직이 끝난 뒤 얹고,
//    WaitForEndOfFrame(= 렌더 이후) 에서 원위치시킨다.
//    화면에는 흔들려 보이지만 다른 코드가 읽는 좌표는 항상 흔들리지 않은 값이다.
//    (유닛 화면 클램프 UnitMovementSystem 도 카메라 좌표를 읽는다)
// ============================================================

[DefaultExecutionOrder(10000)]
public class CameraShaker : MonoBehaviour
{
    // ── 튜닝 ─────────────────────────────────────────────────
    const float MaxTrauma = 1f;
    const float Amplitude = 0.55f;   // 트라우마 1 일 때 최대 흔들림 (월드 단위)
    const float RotAmp    = 1.6f;    // 최대 기울기 (도)
    const float Decay     = 3.4f;    // 초당 감쇠량
    const float Frequency = 26f;     // 흔들림 빈도 — 낮으면 출렁이고 높으면 지직거린다

    static CameraShaker _instance;

    float      _trauma;
    float      _seed;
    Vector3    _appliedPos;
    Quaternion _appliedRot = Quaternion.identity;
    bool       _pending;

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>화면을 흔든다. amount 0.2 = 가벼운 타격, 0.6 = 큰 폭발.</summary>
    public static void Impulse(float amount)
    {
        var shaker = Ensure();
        if (shaker != null) shaker.Add(amount);
    }

    /// <summary>
    /// 월드 위치 기준 흔들림. 화면 밖에서 터진 것은 약하게 전달된다.
    /// 비석 12기가 화면 구석에 떨어질 때 중앙 착탄과 같은 세기로 흔들리면 어색하다.
    /// </summary>
    public static void Impulse(float amount, Vector3 worldPos)
    {
        var shaker = Ensure();
        if (shaker == null) return;

        var cam = shaker.GetComponent<Camera>();
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        Vector3 d = worldPos - cam.transform.position;
        float   n = Mathf.Max(Mathf.Abs(d.x) / halfW, Mathf.Abs(d.y) / halfH);

        // 화면 안(n<=1)은 그대로, 밖으로 나갈수록 빠르게 줄어 2화면 밖은 무시
        shaker.Add(amount * Mathf.Clamp01(1f - Mathf.Max(0f, n - 1f)));
    }

    // ── 내부 ─────────────────────────────────────────────────

    static CameraShaker Ensure()
    {
        if (_instance != null) return _instance;

        var cam = Camera.main;
        if (cam == null) return null;

        _instance = cam.GetComponent<CameraShaker>();
        if (_instance == null) _instance = cam.gameObject.AddComponent<CameraShaker>();
        return _instance;
    }

    void Awake()
    {
        _instance = this;
        _seed     = Random.value * 100f;
    }

    void OnDisable()
    {
        // 비활성화 순간 오프셋이 남아 있으면 그대로 굳는다
        Restore();
        _trauma = 0f;
    }

    void Add(float amount)
    {
        if (amount <= 0f) return;
        _trauma = Mathf.Min(MaxTrauma, _trauma + amount);
    }

    void LateUpdate()
    {
        if (_trauma <= 0f) return;

        // 제곱 — 약한 충격은 더 약하게, 강한 충격은 확실하게
        float shake = _trauma * _trauma;
        float t     = Time.unscaledTime * Frequency;

        // Perlin 은 -1~1 로 펴서 쓴다. Random 을 쓰면 프레임마다 튀어 지직거린다
        float nx = (Mathf.PerlinNoise(_seed,        t) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(_seed + 17f,  t) - 0.5f) * 2f;
        float nr = (Mathf.PerlinNoise(_seed + 53f,  t) - 0.5f) * 2f;

        _appliedPos = new Vector3(nx * Amplitude * shake, ny * Amplitude * shake, 0f);
        _appliedRot = Quaternion.Euler(0f, 0f, nr * RotAmp * shake);

        transform.position += _appliedPos;
        transform.rotation  = _appliedRot * transform.rotation;

        // ⚠ 배속 토글이 timeScale 을 바꾸므로 감쇠는 unscaled 로 돈다
        _trauma = Mathf.Max(0f, _trauma - Decay * Time.unscaledDeltaTime);

        if (!_pending)
        {
            _pending = true;
            StartCoroutine(RestoreAfterRender());
        }
    }

    IEnumerator RestoreAfterRender()
    {
        yield return new WaitForEndOfFrame();
        Restore();
        _pending = false;
    }

    void Restore()
    {
        if (_appliedPos == Vector3.zero && _appliedRot == Quaternion.identity) return;

        transform.position -= _appliedPos;
        transform.rotation  = Quaternion.Inverse(_appliedRot) * transform.rotation;

        _appliedPos = Vector3.zero;
        _appliedRot = Quaternion.identity;
    }
}
