using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  ItemData.cs
//  재화·아이템 보유 수량 저장 섹션 (ISaveSection).
//
//  사용법:
//    var items = UserDataManager.Instance.Get<ItemData>();
//    items.Add(eItem.Gold, 500);
//    if (items.CanSpend(eItem.Gold, 100)) items.Spend(eItem.Gold, 100);
//    UserDataManager.Instance.RequestSave();
//
//  세이브 섹션 밖에서 독립 인스턴스로도 쓸 수 있다 (테스트·모의전의 캠페인 지갑).
// ============================================================

public class ItemData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.ItemData;

    // 수량이 바뀐 아이템과 변경 후 수량
    public static event Action<eItem, int> OnItemChanged;

    ItemRawData _raw = new();

    // ── 조회 ─────────────────────────────────────────────────

    public int Get(eItem item) => _raw.Get(item);

    public bool CanSpend(eItem item, int amount) => Get(item) >= amount;

    // ── 획득 / 소비 ──────────────────────────────────────────

    public void Add(eItem item, int amount)
    {
        if (amount <= 0) return;
        _raw.Add(item, amount);
        OnItemChanged?.Invoke(item, _raw.Get(item));
    }

    /// <returns>소비 성공 여부. 잔액 부족이면 false 반환하고 수량 변경 없음.</returns>
    public bool Spend(eItem item, int amount)
    {
        if (!CanSpend(item, amount)) return false;
        _raw.Add(item, -amount);
        OnItemChanged?.Invoke(item, _raw.Get(item));
        return true;
    }

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize() => JsonUtility.ToJson(_raw);

    public void SetDefaults() => _raw = new ItemRawData();   // 새 캠페인이 시작 금화·소모품을 넣는다

    public void Deserialize(string json) => _raw = JsonUtility.FromJson<ItemRawData>(json) ?? new ItemRawData();

    // ── 직렬화 전용 내부 클래스 ──────────────────────────────
    // JsonUtility 는 Dictionary 직렬화를 지원하지 않으므로 병렬 List 사용.

    [Serializable]
    class ItemRawData
    {
        public List<int> Keys   = new();
        public List<int> Values = new();

        public int Get(eItem item)
        {
            int idx = Keys.IndexOf((int)item);
            return idx < 0 ? 0 : Values[idx];
        }

        public void Set(eItem item, int value)
        {
            int key = (int)item;
            int idx = Keys.IndexOf(key);
            if (idx < 0) { Keys.Add(key); Values.Add(Mathf.Max(0, value)); }
            else Values[idx] = Mathf.Max(0, value);
        }

        public void Add(eItem item, int delta) => Set(item, Get(item) + delta);
    }
}
