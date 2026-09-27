using UnityEngine;
using UnityEngine.Rendering;

// ============================================================
//  UnitSortingSetup.cs
//  유닛의 앞뒤(그리는 순서)를 Unity 기본 기능만으로 세운다.
//
//  ■ 이 프로젝트의 정렬 규칙
//    Project Settings > Graphics > Transparency Sort Mode = Custom Axis (0,1,0)
//    → 스프라이트는 Y 가 작을수록(화면 아래) 앞에 그려진다.
//    유닛 원점은 발밑이므로, 이것만으로 "앞에 선 유닛이 앞에 그려진다" 가 성립해야 한다.
//
//  ■ 그런데 두 가지가 그것을 막고 있었다
//
//    ① SpriteSortPoint 가 Center 였다  (프리팹 기본값)
//       축 정렬이 보는 값은 transform 위치가 아니라 **스프라이트 bounds 의 중심**이다.
//       유닛 원점은 발밑이고 그림은 위로 뻗으므로 중심은 늘 (키/2) 만큼 위에 있다.
//       보스는 루트 스케일이 2배라 그 (키/2) 도 두 배가 된다 —
//       같은 자리에 서 있어도 정렬값이 훨씬 커져 **항상 뒤로 밀렸다.**
//       ("보스 위로 아군이 올라가는" 증상의 정체)
//
//       → SpriteSortPoint.Pivot 으로 바꾸면 스프라이트 피벗(= 발밑)을 본다.
//         PixelHeroes 스프라이트 피벗은 (0.5, 0.125) 로 발밑에 있다.
//         크기와 무관해지므로 보스도 제 발 위치대로 정렬된다.
//
//    ② 한 유닛이 여러 order 를 갖고 있다  (그림자 -1 · 몸통 100 · 무기 105)
//       order 는 축 정렬보다 먼저 적용된다. 그래서 order 만 놓고 보면
//       **뒤에 선 유닛의 무기(105)가 앞에 선 유닛의 몸(100)을 덮는다.**
//       Y 를 아무리 정확히 봐도 이건 안 고쳐진다.
//
//       → SortingGroup 으로 유닛을 통째로 한 덩어리로 만든다.
//         -1 / 100 / 105 는 **그 유닛 안에서만** 의미를 갖고,
//         유닛과 유닛 사이는 그룹 위치(= 발밑)로 축 정렬이 판정한다.
//
//  ■ 왜 코드로 붙이는가
//    프리팹 5종(Boss·Elite·Enemy·General·Soldier)을 각각 고치는 대신
//    모든 유닛이 반드시 지나는 길목(UnitRuntimeBridge.SpawnEntity)에서 한 번 세운다.
//    프리팹을 손보다 하나를 빠뜨리면 그 유닛만 조용히 어긋난다.
//
//  ⚠ 외형 조립이 끝난 뒤에 불러야 한다
//    CharacterBuilder 가 SpriteRenderer 를 갈아 끼우므로, 그 전에 훑으면
//    이미 사라진 렌더러에 값을 쓰게 된다.
// ============================================================

public static class UnitSortingSetup
{
    /// <summary>
    /// 유닛 덩어리가 설 자리. 바닥 이펙트(90)와 공중 이펙트(200) 사이다.
    ///
    /// 그룹 하나로 묶이므로 유닛은 이제 order 를 **하나만** 쓴다.
    /// 예전처럼 Y 에 따라 order 를 벌릴 필요가 없어져, 이펙트 대역과 다툴 일도 없다.
    /// </summary>
    public const int UnitGroupOrder = 100;

    public static void Apply(GameObject unit)
    {
        // ── ① 유닛을 한 덩어리로 ──────────────────────────────
        if (!unit.TryGetComponent<SortingGroup>(out var group))
            group = unit.AddComponent<SortingGroup>();

        group.sortingOrder = UnitGroupOrder;

        // ── ② 정렬 기준점을 발밑(피벗)으로 ────────────────────
        //   그룹이 앞뒤를 정하더라도, 기준점이 bounds 중심이면 큰 유닛이
        //   여전히 불리하다. 두 겹 다 맞춰 둔다.
        var renderers = unit.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].spriteSortPoint = SpriteSortPoint.Pivot;
    }
}
