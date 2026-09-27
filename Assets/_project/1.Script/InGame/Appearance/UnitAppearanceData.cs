// ============================================================
//  UnitAppearanceData.cs
//  외형 롤러가 반환하는 순수 데이터 구조체.
//  AllyAppearanceRoller / EnemyAppearanceRoller 가 채우고,
//  UnitAppearanceBridge 가 CharacterBuilder 에 적용한다.
//
//  문자열 형식 (CharacterBuilder 호환):
//    기본:       "Human"
//    피부/색상:  "Human#F6CA9F"
//    색상+HSV:  "Hair2#8A4836/5:0:-10"
// ============================================================

[System.Serializable]
public class UnitAppearanceData
{
    // ── 종족 / 신체 ──────────────────────────────────────────
    public string Body    = "";
    public string Head    = "";
    public string Ears    = "";
    public string Eyes    = "";

    // ── 외형 장비 ────────────────────────────────────────────
    public string Hair    = "";
    public string Armor   = "";
    public string Helmet  = "";
    public string Mask    = "";
    public string Horns   = "";
    public string Cape    = "";

    // ── 전투 장비 ────────────────────────────────────────────
    public string Weapon  = "";
    public string Shield  = "";
    public string Back    = "";
    public string Firearm = "";
}
