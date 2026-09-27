// ============================================================
//  EnemyRace.cs
//  적군 종족 Enum.
//  WaveData / SpawnEntry 의 EnemyRace 필드로 웨이브마다 지정.
//  EnemyAppearanceRoller 가 이 값으로 Body/Head/Eyes/Ears 를 결정.
// ============================================================

public enum EnemyRace
{
    Orc,
    Goblin,
    Skeleton,
    Demon,
    Vampire,
    ZombieA,
    ZombieB,
    FireLizard,
    Lizard,
    Froggy,
    Drakosha,
    Werewolf,
    Demigod,
    Furry,
    Merman,
    Teddy,

    // FantasyHeroes/Bonus/Monsters — 기존 enum 직렬화 값을 지키기 위해 끝에 추가한다.
    Hog,
    Slug,
    Troll,
    Wolf,
}
