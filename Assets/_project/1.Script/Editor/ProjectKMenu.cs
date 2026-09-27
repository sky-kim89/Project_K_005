// ============================================================
//  ProjectKMenu.cs  [Editor Only]
//  프로젝트 전체 에디터 메뉴 경로·정렬 우선순위의 단일 정의처.
//
//  ⚠ 규칙
//    1. 루트는 "Tools/Project K/" 하나뿐이다. 새 루트를 만들지 말 것.
//    2. 모든 [MenuItem] 은 이 파일의 상수를 조합해서 쓴다.
//       문자열을 직접 적으면 다시 어긋나므로 금지.
//         [MenuItem(ProjectKMenu.Popup + "BattleResult",
//                   priority = ProjectKMenu.PrefabPrio + 11)]
//    3. 그룹 첫 항목은 "▶ 전체 생성", priority 는 그룹 기준값 그대로.
//       개별 항목은 기준값 + 11 부터 시작한다.
//       (Unity 는 priority 차이가 11 이상일 때 구분선을 넣는다.)
//
//  메뉴 구조
//    Tools/Project K/
//      ├─ 씬 이동/          씬 로드 단축키
//      ├─ 씬 셋업/          씬 계층 구성 (프리팹은 만들지 않음)
//      ├─ 프리팹 생성/      로비 · 팝업 · 인게임 · 이펙트
//      ├─ 데이터 생성/      ScriptableObject · Database
//      ├─ 아이콘·텍스처/    PNG · 머티리얼 · 일러스트
//      ├─ 도구/             에디터 윈도우 · 링커
//      └─ 출시 설정/        Steam · Google Play 권장값
// ============================================================

public static class ProjectKMenu
{
    // ── 루트 ─────────────────────────────────────────────────
    public const string Root = "Tools/Project K/";

    // ── 그룹 경로 ─────────────────────────────────────────────
    public const string Scene  = Root + "씬 이동/";
    public const string Setup  = Root + "씬 셋업/";
    public const string Prefab = Root + "프리팹 생성/";
    public const string Data   = Root + "데이터 생성/";
    public const string Icon   = Root + "아이콘·텍스처/";
    public const string Tool   = Root + "도구/";
    public const string Release = Root + "출시 설정/";

    // ── 프리팹 생성 하위 그룹 ─────────────────────────────────
    public const string Lobby  = Prefab + "로비/";
    public const string Popup  = Prefab + "팝업/";
    public const string InGame = Prefab + "인게임/";
    public const string Fx     = Prefab + "이펙트/";

    // ── 정렬 우선순위 ─────────────────────────────────────────
    public const int ScenePrio  = 0;
    public const int SetupPrio  = 20;
    public const int PrefabPrio = 40;
    public const int DataPrio   = 100;
    public const int IconPrio   = 200;
    public const int ToolPrio   = 300;
    public const int ReleasePrio = 400;
}
