using UnityEngine;
using UnityEngine.U2D;

// ============================================================
//  SpriteManager.cs
//  게임 전역 스프라이트 조회 ScriptableObject 싱글턴.
//
//  초기화:
//    Assets/Resources/SpriteManager.asset 에 배치.
//    씬 로드 전 자동 로드.
//
//  사용법:
//    Sprite icon = SpriteManager.Instance.Get("item_gold");
//
//  아틀라스 → 폴더 매핑:
//    _soulAtlas ← Icons/SoulMercenaries/  (key "stat:Strength" → sprite "stat_Strength")
//
//  스프라이트 이름은 PNG 파일명(확장자 제외)과 동일해야 한다.
// ============================================================

[CreateAssetMenu(fileName = "SpriteManager", menuName = "ProjectK/SpriteManager")]
public class SpriteManager : ScriptableObject
{
    public static SpriteManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoLoad() => Instance = Resources.Load<SpriteManager>("SpriteManager");

    [Header("영혼 용병단 (스탯·상태·패턴·UI 아이콘)")]
    [SerializeField] SpriteAtlas _soulAtlas;

    // 전체 아틀라스를 순서대로 검색. 없으면 null.
    public Sprite Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        Sprite s;
        if (_soulAtlas != null && (s = _soulAtlas.GetSprite(name)) != null) return s;

        return null;
    }
}
