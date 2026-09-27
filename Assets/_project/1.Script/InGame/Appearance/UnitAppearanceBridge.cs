using Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts;
using UnityEngine;
using UnityEngine.U2D.Animation;

// ============================================================
//  UnitAppearanceBridge.cs
//  RuntimeBridge ↔ CharacterBuilder 를 연결하는 외형 적용 컴포넌트.
//
//  사용법:
//    유닛 프리팹에 CharacterBuilder 와 함께 부착한다.
//    RuntimeBridge.Initialize() 내에서:
//      GetComponent<UnitAppearanceBridge>()?.ApplyCustom(appearanceData);   // 용병·사람형 몬스터
//      GetComponent<UnitAppearanceBridge>()?.ApplyMonsterRace(race);        // 몬스터 스프라이트
//
//  주의:
//    [DefaultExecutionOrder(-100)] 로 CharacterBuilderBase.Awake()(order 0) 보다
//    먼저 실행해 RebuildOnStart = false 를 선점한다.
//    이렇게 하면 SpriteCollection / Character 가 없어도 NullReference 가 발생하지 않는다.
//
//  풀 재사용 최적화 (2단):
//    ① 인스턴스 단위 — 마지막으로 적용한 외형 키를 기억해 같은 조합이면 Rebuild() 자체를 건너뛴다.
//       (디스폰 시 키를 지우지 않는다. 지우면 같은 유닛이 다시 나올 때마다 재합성한다)
//    ② 전역 단위 — CharacterBuilder 의 공유 캐시가 같은 외형의 텍스처·SpriteLibraryAsset 을
//       모든 인스턴스에 돌려쓴다. 한 웨이브 적 20기는 조합이 같아 합성은 1회로 끝난다.
//
//  ⚠ 공유 캐시를 비우면 인스턴스 키도 같이 죽어야 한다 (InvalidateAll)
//    ①의 키는 "이 인스턴스에 그 외형이 이미 올라가 있다" 는 뜻이다. 그 근거는
//    ②가 들고 있는 텍스처·SpriteLibraryAsset 이다.
//    BattleManager.PrepareRoutine 이 스테이지마다 ClearSharedCache() +
//    Resources.UnloadUnusedAssets() 를 도는데, 그때 그 에셋들이 **파괴된다.**
//    키만 남으면 재사용된 유닛이 Rebuild 를 건너뛰고 **파괴된 스프라이트를 그대로 참조** —
//    전투는 멀쩡히 하는데 캐릭터만 안 보이는 유닛이 된다.
//    (환생·스테이지를 반복할수록 '이미 적용됨' 인스턴스가 늘어 증상이 누적됐다)
// ============================================================

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CharacterBuilder))]
public class UnitAppearanceBridge : MonoBehaviour
{
    CharacterBuilder _builder;

    // ── 공유 캐시 세대 ────────────────────────────────────────
    //  ClearSharedCache 가 돌 때마다 올라간다. 인스턴스 키는 자기가 만들어진
    //  세대에서만 유효하다 — 세대가 다르면 근거가 된 에셋이 이미 없다.
    static int _cacheGeneration;
    int        _appliedGeneration = -1;

    /// <summary>
    /// 모든 인스턴스의 외형 키를 무효화한다.
    /// CharacterBuilder.ClearSharedCache() 를 부른 직후에 반드시 함께 부를 것.
    /// </summary>
    public static void InvalidateAll() => _cacheGeneration++;

    // ── 몬스터 외형 캐시 키 ───────────────────────────────────
    EnemyRace _lastMonsterRace;
    bool      _hasAppliedMonster;

    void Awake()
    {
        _builder = GetComponent<CharacterBuilder>();
        // CharacterBuilderBase.Awake() 보다 먼저 실행돼 Rebuild() 자동 호출을 막는다.
        _builder.RebuildOnStart = false;
    }

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>몬스터 스프라이트 외형 적용 (EnemyMonsterCatalog 의 종족 시트).</summary>
    public void ApplyMonsterRace(EnemyRace race)
    {
        EnsureBuilder();
        if (_builder == null) return;
        if (_hasAppliedMonster && _appliedGeneration == _cacheGeneration && _lastMonsterRace == race) return;

        _lastMonsterRace   = race;
        _hasAppliedMonster = true;
        _appliedGeneration = _cacheGeneration;
        ApplyMonster(race);
    }

    /// <summary>
    /// 데이터가 직접 정한 외형 적용 (영혼 용병단: 종족 → 용병 → 장비 → 영혼 순으로 합성된 결과).
    /// 캐시 키가 없으므로 호출 쪽이 바뀔 때만 부른다.
    /// </summary>
    public void ApplyCustom(UnitAppearanceData data)
    {
        EnsureBuilder();
        if (_builder == null || data == null) return;
        _hasAppliedMonster = false;
        Apply(data);
    }

    // ── 내부 ─────────────────────────────────────────────────

    void EnsureBuilder()
    {
        if (_builder == null)
            _builder = GetComponent<CharacterBuilder>();
    }

    void Apply(UnitAppearanceData data)
    {
        // 몬스터 모드가 남긴 Attack → Slash 오버라이드를 지운다.
        _builder.Character.Body.GetComponent<SpriteLibrary>().RemoveOverride("Slash");

        _builder.Body    = data.Body;
        _builder.Head    = data.Head;
        _builder.Ears    = data.Ears;
        _builder.Eyes    = data.Eyes;
        _builder.Hair    = data.Hair;
        _builder.Armor   = data.Armor;
        _builder.Helmet  = data.Helmet;
        _builder.Mask    = data.Mask;
        _builder.Horns   = data.Horns;
        _builder.Cape    = data.Cape;
        _builder.Weapon  = data.Weapon;
        _builder.Shield  = data.Shield;
        _builder.Back    = data.Back;
        _builder.Firearm = data.Firearm;

        _builder.Rebuild();
    }

    void ApplyMonster(EnemyRace race)
    {
        var body = _builder.Character.Body;
        var source  = EnemyMonsterCatalog.Current.Get(race);
        var library = body.GetComponent<SpriteLibrary>();

        library.spriteLibraryAsset = source;
        // 기존 컨트롤러의 근접 공격 상태는 Slash를 찾고, 몬스터 원본은 Attack으로 묶여 있다.
        for (int i = 0; i < 4; i++)
            library.AddOverride(source.GetSprite("Attack", i.ToString()), "Slash", i.ToString());

        body.GetComponent<SpriteResolver>().SetCategoryAndLabel("Idle", "0");
        _builder.Character.Firearm.Renderer.enabled = false;
    }
}
