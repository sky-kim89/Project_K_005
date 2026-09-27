using System;
using UnityEngine;
using UnityEngine.U2D.Animation;

// FantasyHeroes/Bonus/Monsters의 완성형 SpriteLibrary 참조.
// 적 프리팹은 그대로 두고 Body 라이브러리만 교체해 기존 Animator/ECS 로직을 재사용한다.
[CreateAssetMenu(fileName = "EnemyMonsterCatalog", menuName = "Project K/Enemy Monster Catalog")]
public sealed class EnemyMonsterCatalog : ScriptableObject
{
    static EnemyMonsterCatalog _current;
    public static EnemyMonsterCatalog Current
        => _current != null ? _current : (_current = Resources.Load<EnemyMonsterCatalog>(nameof(EnemyMonsterCatalog)));

    public SpriteLibraryAsset Hog;
    public SpriteLibraryAsset Slug;
    public SpriteLibraryAsset Troll;
    public SpriteLibraryAsset Wolf;

    public SpriteLibraryAsset Get(EnemyRace race) => race switch
    {
        EnemyRace.Hog   => Hog,
        EnemyRace.Slug  => Slug,
        EnemyRace.Troll => Troll,
        EnemyRace.Wolf  => Wolf,
        _ => throw new ArgumentOutOfRangeException(nameof(race), race, "몬스터 종족이 아닙니다."),
    };
}
