// ============================================================
//  ISaveSection.cs
//  저장 가능한 데이터 섹션 인터페이스.
//
//  UserDataManager 에 등록된 모든 섹션은 이 인터페이스를 구현한다.
//  SaveAll() 호출 시 등록된 섹션들이 순서대로 직렬화·저장된다.
//
//  새 데이터 섹션 추가 방법:
//  1. ISaveSection 을 구현하는 클래스 생성
//  2. UserDataManager.RegisterSection() 으로 등록
// ============================================================

// ── 저장 키 ──────────────────────────────────────────────────
// 새 섹션 추가 시 여기에 값을 추가한다.
public enum SaveKey
{
    // ⚠ 번호는 세이브(PlayerPrefs "Save_<번호>")에 남는다 — 바꾸거나 재사용하지 말 것.
    ItemData        = 2,   // 금화·영혼석·소모품
    BattleSettings  = 13,  // 플레이어 설정
    SoulCampaign    = 18,  // 영혼 용병단 — 마을·용병·장비·의뢰
}

public interface ISaveSection
{
    /// <summary>이 섹션의 저장 키.</summary>
    SaveKey SaveKey { get; }

    /// <summary>데이터를 JSON 문자열로 직렬화해 반환.</summary>
    string Serialize();

    /// <summary>JSON 문자열로부터 데이터를 복원.</summary>
    void Deserialize(string json);

    /// <summary>데이터가 없을 때 기본값으로 초기화.</summary>
    void SetDefaults();
}
