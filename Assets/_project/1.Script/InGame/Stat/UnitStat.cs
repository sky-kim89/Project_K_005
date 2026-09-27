using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// ============================================================
//  UnitStat
//  유닛 스텟 관리 핵심 클래스 (순수 C# — ECS / MB 둘 다 사용 가능)
//
//  ■ 레이어(key) 시스템
//    - 스텟 수정자는 출처를 나타내는 key와 함께 등록
//    - key 없이 추가하면 "base" 레이어에 등록
//    - 같은 key 내부 → 항상 덧셈
//    - 다른 key 간 결합 → StatType별 CombineMode 설정 (기본 Add)
//
//  ■ 캐싱
//    - Get() 호출 시 dirty 이면 전체 재계산 후 캐시 저장
//    - 이후 수정 없으면 캐시 값 반환
//
//  ■ 정산 (아웃게임 → 인게임)
//    - Settle() 호출 → 현재 모든 스텟을 1회 계산해
//      단일 "settled" 레이어로 압축한 새 UnitStat 반환
//    - 인게임에서는 settled 기반으로 버프/디버프만 추가
// ============================================================

public class UnitStat
{
    // ── 상수 ──────────────────────────────────────────────────
    public const string BaseKey    = "base";
    public const string SettledKey = "settled";

    /// <summary>
    /// 장수 전용 보너스 레이어 — 병사 환산에서 빠지는 자리.
    ///
    /// ⚠ '어디서 왔나' 가 아니라 '누구를 올리나' 로 가른다
    ///   패시브·특성·도감·장비의 평범한 "공격력 증가" 는 부대 전체를 올리는
    ///   옵션이므로 병사에게도 간다. 반대로 명시적으로 장수를 지목한 옵션
    ///   (AbilityTarget.Unit_General)만 이 레이어에 담아 병사에서 뺀다.
    ///   출처로 가르면 "패시브로 얻은 공격력 증가" 가 병사에게 안 가서
    ///   같은 문구의 옵션이 출처에 따라 다르게 동작한다.
    /// </summary>
    public const string GeneralOnlyKey = "general_only";

    /// <summary>
    /// 장수 전용 레이어임을 나타내는 접미사. "ability@g" 처럼 붙인다.
    ///
    /// ⚠ 왜 레이어를 하나로 안 두고 출처별로 나누나
    ///   병사 환산에서 걷어내는 것과, 화면에 "어빌리티 +30" 으로 분해해 보여 주는 것은
    ///   서로 다른 요구다. 예전엔 전자만 보고 general_only 한 통에 몰아넣었더니
    ///   로비가 분해를 못 해서 자기 딕셔너리를 따로 만들었고, 그 순간 계산이 둘로 갈렸다.
    ///   출처를 보존한 채 접미사로 표시하면 한 자료구조가 둘 다 답한다.
    /// </summary>
    public const string GeneralOnlySuffix = "@g";

    /// <summary>출처 이름에 장수 전용 표시를 붙인다 ("ability" → "ability@g").</summary>
    public static string GeneralOnly(string sourceKey) => sourceKey + GeneralOnlySuffix;

    public static bool IsGeneralOnlyKey(string key) =>
        !string.IsNullOrEmpty(key) &&
        (key == GeneralOnlyKey || key.EndsWith(GeneralOnlySuffix, StringComparison.Ordinal));

    // ── 데이터 ────────────────────────────────────────────────
    // key(레이어) → (StatType → 해당 레이어 내 합산값)
    readonly Dictionary<string, Dictionary<StatType, float>> _layers      = new();
    // StatType별 레이어 결합 방식
    readonly Dictionary<StatType, CombineMode>               _combineModes = new();
    // 계산 결과 캐시
    readonly Dictionary<StatType, float>                     _cache        = new();
    bool _dirty = true;

    // ── 이벤트 ────────────────────────────────────────────────
    /// <summary>스텟이 변경될 때마다 발생 — UI 갱신 등에 사용</summary>
    public event Action OnStatChanged;

    // ── 추가 ──────────────────────────────────────────────────
    /// <summary>
    /// 스텟 수정자를 레이어에 더한다.
    /// key 없으면 "base" 레이어에 추가.
    /// </summary>
    public void Add(StatType type, float value, string key = null)
    {
        key = NormalizeKey(key);
        GetOrCreateLayer(key)[type] = GetLayerValue(key, type) + value;
        MarkDirty();
    }

    /// <summary>해당 레이어의 스텟 값을 덮어쓴다</summary>
    public void Set(StatType type, float value, string key = null)
    {
        key = NormalizeKey(key);
        GetOrCreateLayer(key)[type] = value;
        MarkDirty();
    }

    /// <summary>여러 수정자를 한 번에 적용</summary>
    public void Apply(IList<StatModifier> modifiers)
    {
        if (modifiers == null || modifiers.Count == 0) return;
        foreach (var mod in modifiers)
            Add(mod.Type, mod.Value, mod.Key);
    }

    // ── 제거 ──────────────────────────────────────────────────
    /// <summary>특정 key(레이어) 전체 제거 — 장비 해제, 버프 종료 등에 사용</summary>
    public void RemoveKey(string key)
    {
        if (_layers.Remove(NormalizeKey(key)))
            MarkDirty();
    }

    /// <summary>특정 레이어의 특정 스텟만 제거</summary>
    public void Remove(StatType type, string key = null)
    {
        key = NormalizeKey(key);
        if (_layers.TryGetValue(key, out var layer) && layer.Remove(type))
            MarkDirty();
    }

    // ── 조회 ──────────────────────────────────────────────────
    /// <summary>
    /// 최종 스텟 값 반환 (캐시 우선 — 변동 없으면 재계산 없음)
    /// </summary>
    public float Get(StatType type)
    {
        if (_dirty) RecalculateAll();
        return _cache.TryGetValue(type, out float v) ? v : 0f;
    }

    /// <summary>min/max 범위 제한 포함 조회</summary>
    public float GetClamped(StatType type, float min, float max)
        => Mathf.Clamp(Get(type), min, max);

    public bool  HasKey(string key)     => _layers.ContainsKey(NormalizeKey(key));
    public IEnumerable<string> GetKeys() => _layers.Keys;

    /// <summary>
    /// 특정 레이어가 이 스탯에 넣은 몫. 없으면 0.
    /// 화면 분해("장비 +119")가 이걸 읽는다 — 계산과 표시가 같은 자료를 본다.
    /// </summary>
    public float GetLayer(string key, StatType type)
    {
        if (!_layers.TryGetValue(NormalizeKey(key), out var layer)) return 0f;
        return layer.TryGetValue(type, out float v) ? v : 0f;
    }

    /// <summary>
    /// 출처 하나의 총합 — 공용 레이어와 장수 전용 레이어를 함께 센다.
    /// (예: "ability" 와 "ability@g")
    /// </summary>
    public float GetSource(string sourceKey, StatType type)
        => GetLayer(sourceKey, type) + GetLayer(GeneralOnly(sourceKey), type);

    /// <summary>
    /// 이름이 prefix 로 시작하는 레이어를 전부 더한다.
    /// 장비처럼 슬롯마다 레이어가 갈리는 출처("equip_0"·"equip_1"…)를 한 줄로 볼 때 쓴다.
    /// </summary>
    public float GetPrefixed(string prefix, StatType type)
    {
        prefix = NormalizeKey(prefix);
        float sum = 0f;
        foreach (var layerKv in _layers)
        {
            if (!layerKv.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (layerKv.Value.TryGetValue(type, out float v)) sum += v;
        }
        return sum;
    }

    // ── 결합 모드 설정 ─────────────────────────────────────────
    public void SetCombineMode(StatType type, CombineMode mode)
    {
        _combineModes[type] = mode;
        MarkDirty();
    }

    public CombineMode GetCombineMode(StatType type)
        => _combineModes.TryGetValue(type, out var m) ? m : CombineMode.Add;

    // ── 정산 ──────────────────────────────────────────────────
    /// <summary>
    /// 아웃게임 → 인게임 전환용 1회 정산.
    /// 현재 스텟을 전부 계산해 단일 "settled" 레이어로 압축한 새 UnitStat 반환.
    /// 반환된 UnitStat에 인게임 버프/디버프 레이어를 추가하면 됨.
    /// </summary>
    public UnitStat Settle()
    {
        if (_dirty) RecalculateAll();

        var settled = new UnitStat();

        // 결합 모드 복사
        foreach (var kv in _combineModes)
            settled.SetCombineMode(kv.Key, kv.Value);

        // 계산된 모든 값을 "settled" 레이어 하나에 압축
        foreach (var kv in _cache)
            if (kv.Value != 0f)
                settled.Set(kv.Key, kv.Value, SettledKey);

        return settled;
    }

    /// <summary>
    /// 특정 레이어만 뺀 깊은 복사. 병사 환산의 원본을 만들 때 쓴다.
    /// (장수 전용 보너스를 빼고 나머지는 그대로 물려주는 용도)
    /// </summary>
    /// <summary>
    /// 장수 전용 레이어를 전부 걷어낸 사본 — 병사 환산의 원본이다.
    ///
    /// ⚠ 레이어 하나만 지우면 안 된다
    ///   장수 전용은 출처별로 나뉘어 있다 (passive@g · ability@g · relic@g).
    ///   하나만 지우면 나머지가 병사에게 그대로 흘러간다.
    /// </summary>
    public UnitStat CloneWithoutGeneralOnly()
    {
        var clone = new UnitStat();
        foreach (var kv in _combineModes)
            clone.SetCombineMode(kv.Key, kv.Value);

        foreach (var layerKv in _layers)
        {
            if (IsGeneralOnlyKey(layerKv.Key)) continue;
            foreach (var statKv in layerKv.Value)
                clone.Set(statKv.Key, statKv.Value, layerKv.Key);
        }
        return clone;
    }

    public UnitStat CloneWithout(string excludeKey)
    {
        excludeKey = NormalizeKey(excludeKey);

        var clone = new UnitStat();
        foreach (var kv in _combineModes)
            clone.SetCombineMode(kv.Key, kv.Value);

        foreach (var layerKv in _layers)
        {
            if (layerKv.Key == excludeKey) continue;
            foreach (var statKv in layerKv.Value)
                clone.Set(statKv.Key, statKv.Value, layerKv.Key);
        }
        return clone;
    }

    /// <summary>깊은 복사 — 독립적인 UnitStat 인스턴스 생성</summary>
    public UnitStat Clone()
    {
        var clone = new UnitStat();
        foreach (var kv in _combineModes)
            clone.SetCombineMode(kv.Key, kv.Value);
        foreach (var layerKv in _layers)
            foreach (var statKv in layerKv.Value)
                clone.Set(statKv.Key, statKv.Value, layerKv.Key);
        return clone;
    }

    // ── 강제 dirty ────────────────────────────────────────────
    public void SetDirty() => MarkDirty();

    // ── 디버그 ────────────────────────────────────────────────
    public override string ToString()
    {
        if (_dirty) RecalculateAll();
        var sb = new StringBuilder("[UnitStat]\n");
        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            if (_cache.TryGetValue(type, out float v) && v != 0f)
                sb.AppendLine($"  {type,-15}: {v:F2}  (mode: {GetCombineMode(type)})");
        }
        sb.AppendLine($"  Layers: [{string.Join(", ", _layers.Keys)}]");
        return sb.ToString();
    }

    // ── 내부 ──────────────────────────────────────────────────
    void RecalculateAll()
    {
        _cache.Clear();
        foreach (StatType type in Enum.GetValues(typeof(StatType)))
            _cache[type] = Calculate(type);
        _dirty = false;
    }

    float Calculate(StatType type)
    {
        var mode = GetCombineMode(type);

        switch (mode)
        {
            case CombineMode.Add:
            {
                float sum = 0f;
                foreach (var layer in _layers.Values)
                    if (layer.TryGetValue(type, out float v)) sum += v;
                return sum;
            }
            case CombineMode.Multiply:
            {
                float product = 1f;
                bool  any     = false;
                foreach (var layer in _layers.Values)
                    if (layer.TryGetValue(type, out float v)) { product *= v; any = true; }
                return any ? product : 0f;
            }
            case CombineMode.Max:
            {
                float max = float.MinValue;
                bool  any = false;
                foreach (var layer in _layers.Values)
                    if (layer.TryGetValue(type, out float v))
                    { if (v > max) max = v; any = true; }
                return any ? max : 0f;
            }
            case CombineMode.MultiplyResidual:
            {
                // 1 - Π(1 - v). 레이어 하나면 그 값 그대로 나온다.
                // Settle() 로 한 레이어에 압축된 뒤에도 값이 보존된다.
                float remain = 1f;
                foreach (var layer in _layers.Values)
                    if (layer.TryGetValue(type, out float v)) remain *= (1f - v);
                return 1f - remain;
            }
        }
        return 0f;
    }

    void MarkDirty()
    {
        _dirty = true;
        OnStatChanged?.Invoke();
    }

    Dictionary<StatType, float> GetOrCreateLayer(string key)
    {
        if (!_layers.TryGetValue(key, out var layer))
            _layers[key] = layer = new Dictionary<StatType, float>();
        return layer;
    }

    float GetLayerValue(string key, StatType type)
        => _layers.TryGetValue(key, out var layer) && layer.TryGetValue(type, out float v) ? v : 0f;

    static string NormalizeKey(string key)
        => string.IsNullOrWhiteSpace(key) ? BaseKey : key;
}
