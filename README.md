# 영혼을 흡수하는 용병단 — 플레이 가능한 Unity 게임 기반

Unity **6000.0.71f1**에서 이 폴더를 프로젝트로 엽니다. 기획은 [GAME_DESIGN.md](GAME_DESIGN.md)를 따릅니다.

## 처음 실행

1. 메뉴 **Tools → Project K → 영혼 용병단 → 기본 데이터와 플레이 장면 생성**을 실행합니다.
   - 아이콘(`3.Textures/Icons/SoulMercenaries`, `Resources/SoulIconSet`), 샘플 데이터(`Data/SoulMercenaries`), 유닛 프리팹, 팝업 프리팹(`2.Prefabs/UI/SoulMercenaryPopup`, `SoulMapPopup`), 플레이 장면(`Scenes/SoulMercenaries.unity`)을 만듭니다.
   - 다시 실행하면 샘플 에셋 값이 생성기의 기본값으로 **덮어써집니다**. 직접 조정할 데이터는 복제해서 쓰세요.
2. **Tools → Project K → 영혼 용병단 → 핵심 규칙 검증**으로 규칙을 확인합니다 (콘솔에 `core checks passed`).
3. `SoulMercenaries.unity`를 열고 Game 뷰를 1920×1080으로 맞춘 뒤 실행합니다.

## 조작

- **좌클릭**: 선택한 용병(또는 파티 전체) 이동 · **드래그/우클릭 드래그/방향키**: 화면 이동 · **휠**: 확대 · **Space**: 선택 용병 따라가기
- 맵의 용병이나 하단 카드를 누르면 선택합니다. **선택된 카드를 한 번 더 누르면** 용병 상세 팝업이 열립니다.
- 상단: 금화·보존석·보관 영혼, **영혼**(임시 보관함), **지도**(전체 지도 팝업), **파티 전체 이동** 토글
- 우상단 미니맵: 접기/펼치기, 확대(전체 지도), 따라가기. 미니맵 클릭은 카메라 이동, 전체 지도 클릭은 이동 지시입니다.
- 용병 상세 팝업: **능력치**(스탯 아이콘을 누르면 출처·영향 툴팁), **영혼**(흡수·보존, 패턴 적합도 미리보기), **성장**(레벨업 패턴 3택, 스탯은 성향에 따라 자동)
- 탐험하지 않은 곳은 어둡게 가려지고 이동 지시가 되지 않습니다. 유적 수호자를 쓰러뜨리면 출구가 열립니다.
- **자동 탐사**(상단 토글, 기본 ON): 지시가 없으면 파티가 스스로 탐사·전투하고 출구로 나갑니다. 이동 지시를 주면 그 지시가 우선하며, 경로의 적은 공격하면서 이동합니다.
- 파티: 리아(검사)·토르(수호자, 탱커)·세라(마법사)·카론(투사)·루카(길잡이: 벽 너머 시야·출구/수호자 위치·숨겨진 문 발견, 운으로 영혼 드롭률 증가)

## 구현된 기획 규칙 (요약)

- 영혼 흡수·보존·퇴장 시 소멸, 레벨당 슬롯, 핵심 패턴 흡수 불가, 종족 제약·무기 필요 판정
- 패턴: 공격(베기·휘두르기·찌르기·돌진·활·할퀴기·독침·마법 시전·마력탄), 방어(막기·회피·받아치기·엄폐), 이동(접근·측면·거리 유지), 조우(도발)
- 스킬: 도발(위협도·방어 버프), 도약 충격파(몸무게 90kg 이상일 때만 충격파), 화염탄·서리 파편(마법사 MP 시전), 분노의 돌격
- 상태 이상 8종, 넉백(몸무게 비율), 가림(원거리 공격을 앞 아군이 대신 맞음), 위협도 기반 타겟팅
- 종족 4종: 인간(적응력)·드워프(단단한 체구)·엘프(정교한 감각)·수인(사냥 본능)
- 정신력: 동료의 죽음·다수 처치 경험이 던전 종료 후 성장으로 반영
- 자동 성장 스탯(종족·직업 성향), 무기 기본 공격(50%), 유닛 간 밀어내기

## 코드 위치

- `Assets/_project/1.Script/InGame/SoulMercenaries/` — 규칙 (`SoulSession` 진행·AI, `SoulCombat` 피해·상태·비용, `SoulStats` 스탯 계산, `SoulMap` 타일·3×3 이동 격자·계층 길찾기)
  - `View/` — 표시와 입력만 담당: `SoulWorldView`(맵·카메라·안개), `SoulUnitView`(PixelHeroes 캐릭터), `SoulHudView`(HUD), `SoulMercenaryPopup`·`SoulMapPopup`(PopupManager 팝업), `SoulMinimap`, `SoulTooltip`, `SoulDescribe`(설명·툴팁 문구)
- `Assets/_project/1.Script/Editor/SoulMercenariesContentCreator.cs` — 샘플 데이터·장면 생성과 규칙 검증
- `Assets/_project/1.Script/Editor/SoulMercenariesAssetBuilder.cs` — 아이콘(원작 IconArt), 팝업 프리팹, PopupManager 배치, 대형 맵 생성

원작 `Project_K_001`의 출처별 스탯 레이어(`UnitStat`), PopupManager/PopupBase 팝업 구조, IconArt 아이콘 키트, PixelHeroes 외형(`UnitAppearanceBridge`)을 재사용합니다. `prototype/` 폴더는 초기 웹 시제품입니다.
