// ============================================================
//  BgmKey.cs
//  배경음 키. 값 이름이 그대로 파일명이다
//  (Assets/_project/5.Audio/BGM/<이름>.mp3).
//
//  ⚠ 이름을 바꾸면 파일도 같이 바꿔야 한다
//    AudioManager 가 enum 이름으로 클립을 찾는다 (SfxKey 와 같은 규칙).
//
//  ⚠ 곡을 화면마다 쪼개지 않는다
//    로비의 모든 패널(장수 선택·출전·유물)은 한 곡으로 이어진다.
//    패널을 옮길 때마다 곡이 끊기면 그게 더 거슬린다 —
//    바뀌는 지점은 "로비 ↔ 전투" 하나뿐이다 (LobbyManager.OnEnterFlow).
// ============================================================

public enum BgmKey
{
    None = 0,

    Lobby,    // 로비 전체 (Idle · Demo · Preparing · Standby · Returning)
    InGame,   // 전투 (Intro · Battle)
}
