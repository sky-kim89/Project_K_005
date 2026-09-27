using System;
using System.Collections.Generic;
using SoulMercenaries;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SoulMercenariesContentCreator
{
    const string DataPath = "Assets/_project/Data/SoulMercenaries";
    const string ScenePath = "Assets/Scenes/SoulMercenaries.unity";
    const string UnitPrefabPath = "Assets/_project/2.Prefabs/Unit/SoulUnit.prefab";
    const string CharacterPrefabPath = "Assets/PixelFantasy/PixelHeroes/FantasyHeroes/Prefabs/Character.prefab";
    const string GroundPath = "Assets/_project/3.Textures/BG/BG1.png";
    const string VillageScenePath = "Assets/Scenes/SoulVillage.unity";
    const string VillageArtDir = "Assets/_project/3.Textures/Village";

    // PixelHeroes demo character without its demo controls and physics: the session moves units.
    static GameObject CreateUnitPrefab()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
        if (source == null) throw new Exception("Missing " + CharacterPrefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = "SoulUnit";
        instance.transform.position = Vector3.zero; // demo prefab root is offset to x = -4
        // Dependents first so RequireComponent never blocks a removal.
        foreach (string type in new[] { "CharacterControls", "CharacterController2D", "CharacterAnimation" })
            foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null && component.GetType().Name == type) UnityEngine.Object.DestroyImmediate(component);
        foreach (var collider in instance.GetComponentsInChildren<Collider2D>(true)) UnityEngine.Object.DestroyImmediate(collider);
        foreach (var body in instance.GetComponentsInChildren<Rigidbody2D>(true)) UnityEngine.Object.DestroyImmediate(body);
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, UnitPrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
        return prefab;
    }

    static Sprite FirstSprite(string path)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite sprite) return sprite;
        return null;
    }

    static UnitAppearanceData Look(string skin, string eyes)
        => new UnitAppearanceData { Body = skin, Head = skin, Ears = skin, Eyes = eyes };

    static void MonsterLook(SoulMonsterData monster, EnemyRace race, Color tint, float height, float weight)
    {
        monster.UseMonsterSprite = true; monster.MonsterRace = race; monster.Tint = tint;
        monster.Height = height; monster.Weight = weight;
        EditorUtility.SetDirty(monster);
    }

    [MenuItem("Tools/Project K/영혼 용병단/기본 데이터와 플레이 장면 생성")]
    public static void Create()
    {
        System.IO.Directory.CreateDirectory(DataPath);
        SoulMercenariesAssetBuilder.BuildIcons();
        var rules = Asset("StatRules", (SoulStatRules item) =>
        {
            item.Conversions = SoulStatRules.Defaults;
            item.UpperStatInfo = new[]
            {
                Info(StatType.Strength, "근력", new Color(.9f, .4f, .3f)),
                Info(StatType.Vitality, "체력", new Color(.4f, .85f, .4f)),
                Info(StatType.Agility, "민첩", new Color(.5f, .8f, .9f)),
                Info(StatType.Magic, "마력", new Color(.7f, .5f, .95f)),
                Info(StatType.Will, "정신력", new Color(.95f, .85f, .5f)),
                Info(StatType.Luck, "운", new Color(1f, .85f, .3f)),
                Info(StatType.Regeneration, "재생력", new Color(.45f, .9f, .55f)),
                Info(StatType.Durability, "내구도", new Color(.72f, .76f, .84f)),
                Info(StatType.AntiMagic, "항마력", new Color(.9f, .45f, .85f)),
                Info(StatType.Recovery, "회복력", new Color(.35f, .85f, .85f)),
                Info(StatType.SlashPower, "절삭력", new Color(.95f, .38f, .38f)),
                Info(StatType.ImpactPower, "타격력", new Color(1, .6f, .2f)),
                Info(StatType.PiercePower, "관통력", new Color(.8f, .8f, .3f)),
            };
        });

        var slash = Pattern("Slash", "베기", SoulPatternCategory.Attack, 6, 1.15f, 1.3f,
            SoulDamageSchool.Physical, SoulDamageKind.Slash, Value(0, Term(StatType.Attack, 1), Term(StatType.SlashPower, .8f)));
        var swing = Pattern("Swing", "휘두르기", SoulPatternCategory.Attack, 7, 1.4f, 1.4f,
            SoulDamageSchool.Physical, SoulDamageKind.Impact, Value(0, Term(StatType.Attack, .9f), Term(StatType.ImpactPower, .9f)));
        slash.Pierce = Value(0, Term(StatType.SlashPower, .4f)); EditorUtility.SetDirty(slash);
        swing.Pierce = Value(1, Term(StatType.ImpactPower, .5f));
        swing.Status = Status(SoulStatus.Stun, Value(.08f, Term(StatType.ImpactPower, .01f)), Value(.8f)); EditorUtility.SetDirty(swing);
        var thrust = Pattern("Thrust", "찌르기", SoulPatternCategory.Attack, 6, 1.2f, 1.4f,
            SoulDamageSchool.Physical, SoulDamageKind.Pierce, Value(0, Term(StatType.Attack, 1), Term(StatType.PiercePower, .8f)));
        thrust.Pierce = Value(0, Term(StatType.PiercePower, .3f));
        thrust.Status = Status(SoulStatus.Bleed, Value(.35f), Value(4), Value(1, Term(StatType.PiercePower, .3f))); EditorUtility.SetDirty(thrust);
        var bow = Pattern("Bow", "활쏘기", SoulPatternCategory.Attack, 6, 1.5f, 4f,
            SoulDamageSchool.Physical, SoulDamageKind.Pierce, Value(0, Term(StatType.Attack, .8f), Term(StatType.PiercePower, .9f)));
        bow.WeaponTag = "bow"; bow.Tags = new[] { "bow" }; EditorUtility.SetDirty(bow);
        var charge = Pattern("Charge", "돌진", SoulPatternCategory.Attack, 9, 1.7f, 1.8f,
            SoulDamageSchool.Physical, SoulDamageKind.Impact, Value(0, Term(StatType.Attack, 1.25f), Term(StatType.ImpactPower, .6f)));
        charge.Knockback = .8f; EditorUtility.SetDirty(charge);
        var guard = Pattern("Guard", "방패 막기", SoulPatternCategory.Defense, 4, .5f, 0,
            SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        guard.DefenseMode = SoulDefenseMode.Guard; guard.GuardMultiplier = .5f; EditorUtility.SetDirty(guard);
        var dodge = Pattern("Dodge", "회피", SoulPatternCategory.Defense, 4, .5f, 0,
            SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        // 회피: a short sidestep out of the incoming area — cheap, but it only clears a narrow attack.
        dodge.DefenseMode = SoulDefenseMode.Dodge; dodge.RollDistance = 1.5f; EditorUtility.SetDirty(dodge);
        // 구르기: the same idea with ground under it — costs more stamina and clears far wider areas, which is
        // why it is tried before 회피.
        var rollPattern = Pattern("Roll", "구르기", SoulPatternCategory.Defense, 8, .5f, 0,
            SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        rollPattern.DefenseMode = SoulDefenseMode.Roll; rollPattern.RollDistance = 2.2f; EditorUtility.SetDirty(rollPattern);
        var approach = Pattern("Approach", "접근", SoulPatternCategory.Movement, 2, .7f, 0,
            SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        var flank = Pattern("Flank", "측면 이동", SoulPatternCategory.Movement, 3, .7f, 0,
            SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());

        var adaptability = Asset("PassiveAdaptability", (SoulPassiveSkillData item) =>
        { item.SoulId = "adaptability"; item.SkillName = "적응력"; item.AlwaysBonuses = new[] { Bonus(StatType.PatternCostReduce, .05f) }; item.Icon = IconSprite("passive:adaptability"); });
        var sturdy = Asset("PassiveSturdyBuild", (SoulPassiveSkillData item) =>
        { item.SoulId = "sturdy_build"; item.SkillName = "단단한 체구"; item.AlwaysBonuses = new[] { Bonus(StatType.KnockbackResist, .4f) }; item.Icon = IconSprite("passive:sturdy_build"); });
        var human = Asset("RaceHuman", (SoulRaceData item) =>
        { item.Id = "인간"; item.Height = 1.7f; item.Weight = 70; item.StartingPatterns = new[] { guard, approach }; item.InnatePassives = new[] { adaptability }; // 베기: the 검사's, not every human's
          item.GrowthWeights = System.Array.Empty<SoulStatBonus>(); // human: balanced
          item.Appearance = Look("Human#F6CA9F", "Human"); });
        var dwarf = Asset("RaceDwarf", (SoulRaceData item) =>
        { item.Id = "드워프"; item.Height = 1.35f; item.Weight = 85; item.Bonuses = new[] { Bonus(StatType.Durability, 2) }; item.ForbiddenPatternTags = new[] { "bow" }; item.StartingPatterns = new[] { guard, approach }; item.InnatePassives = new[] { sturdy };
          item.GrowthWeights = new[] { Bonus(StatType.Vitality, 3), Bonus(StatType.Durability, 3), Bonus(StatType.Strength, 2), Bonus(StatType.ImpactPower, 2) };
          item.Appearance = Look("Human#E69C69", "Human"); item.Appearance.Helmet = "DwarfHelm"; item.Appearance.Armor = "DwarfTunic"; });
        // placeholders: CreateEquipmentCatalog fills in every field once the patterns exist
        var sword = Asset("EquipmentSword", (SoulEquipmentData item) => { item.Id = "sword"; item.WeaponTag = "sword"; item.Slot = SoulEquipSlot.MainHand; });
        var axe = Asset("EquipmentAxe", (SoulEquipmentData item) => { item.Id = "axe"; item.WeaponTag = "axe"; item.Slot = SoulEquipSlot.MainHand; });
        var rage = Asset("SkillRageCharge", (SoulActiveSkillData item) =>
        {
            item.SoulId = "rage_charge"; item.SkillName = "분노의 돌격"; item.Trigger = SoulTrigger.InRange;
            item.RequiredPattern = charge; item.Cooldown = 8;
            item.Costs = new[] { new SoulResourceCost { Resource = SoulResource.Stamina, Amount = 9 } };
            item.DamageSchool = SoulDamageSchool.Physical; item.DamageKind = SoulDamageKind.Impact;
            item.Damage = Value(10, Term(StatType.Strength, .8f)); item.Knockback = 1.2f; item.Icon = IconSprite("skill:rage_charge");
        });
        var tracking = Asset("PassiveTracking", (SoulPassiveSkillData item) =>
        { item.SoulId = "tracking"; item.SkillName = "약점 추적"; item.SoulEvent = SoulTrigger.OnAttackLanded; item.Every = 3; item.Damage = Value(0, Term(StatType.Agility, .5f)); item.Icon = IconSprite("passive:tracking"); });
        var stoneSkin = Asset("PassiveStoneSkin", (SoulPassiveSkillData item) =>
        { item.SoulId = "stone_skin"; item.SkillName = "석피부"; item.AlwaysBonuses = new[] { Bonus(StatType.Durability, 4) }; item.Icon = IconSprite("passive:stone_skin"); });
        var boarSoul = Asset("SoulBoar", (SoulData item) =>
        { item.Id = "boar"; item.OriginMonster = "멧돼지"; item.CharacteristicStats = new[] { Bonus(StatType.Strength, 3) }; item.Patterns = new[] { charge }; item.ActiveSkills = new[] { rage }; item.WeightMultiplier = 1.15f; });
        var wolfSoul = Asset("SoulWolf", (SoulData item) =>
        { item.Id = "wolf"; item.OriginMonster = "그림자 늑대"; item.CharacteristicStats = new[] { Bonus(StatType.Agility, 3) }; item.Patterns = new[] { flank }; item.Passives = new[] { tracking }; });
        var stoneSoul = Asset("SoulStone", (SoulData item) =>
        { item.Id = "stone"; item.OriginMonster = "고대 석상"; item.CharacteristicStats = new[] { Bonus(StatType.Durability, 4) }; item.Patterns = new[] { guard }; item.Passives = new[] { stoneSkin }; item.WeightMultiplier = 1.3f;
          item.Appearance = new SoulAppearancePatch { Body = "Human#9C9FA3", Head = "Human#9C9FA3", Ears = "Human#9C9FA3" }; });
        var boar = Monster("MonsterBoar", "멧돼지", 5, 4, new[] { swing, approach }, boarSoul, .02f);
        var wolf = Monster("MonsterWolf", "그림자 늑대", 4, 6, new[] { thrust, approach }, wolfSoul, .02f);
        var stone = Monster("MonsterStone", "고대 석상", 12, 6, new[] { swing, guard, approach }, stoneSoul, .02f);
        MonsterLook(boar, EnemyRace.Hog, Color.white, 1.5f, 95);
        boar.DisplayName = "멧돼지"; wolf.DisplayName = "그림자 늑대"; stone.DisplayName = "고대 석상";
        boarSoul.Source = boar; wolfSoul.Source = wolf; stoneSoul.Source = stone;
        EditorUtility.SetDirty(boarSoul); EditorUtility.SetDirty(wolfSoul); EditorUtility.SetDirty(stoneSoul);
        MonsterLook(wolf, EnemyRace.Wolf, new Color(.55f, .55f, .72f), 1.4f, 55);
        MonsterLook(stone, EnemyRace.Troll, new Color(.72f, .74f, .80f), 2.1f, 160);
        var ria = Asset("MercenaryRia", (SoulMercenaryData item) =>
        { item.Id = "ria"; item.DisplayName = "리아"; item.Race = human; item.StartingEquipment = new[] { sword }; item.BaseStats = new[] { Bonus(StatType.Strength, 5), Bonus(StatType.Vitality, 5), Bonus(StatType.Agility, 4), Bonus(StatType.Magic, 2), Bonus(StatType.Will, 3), Bonus(StatType.Durability, 3), Bonus(StatType.SlashPower, 3) };
          item.Look = new SoulAppearancePatch { Hair = "Hair2#8A4836", Armor = "TravelerTunic", Shield = "IronBuckler" };
          item.GrowthWeights = new[] { Bonus(StatType.Strength, 2), Bonus(StatType.SlashPower, 2) };
          item.Job = "검사"; item.Role = SoulRole.Melee; });
        var thor = Asset("MercenaryThor", (SoulMercenaryData item) =>
        { item.Id = "thor"; item.DisplayName = "토르"; item.Race = dwarf; item.StartingEquipment = new[] { axe }; item.BaseStats = new[] { Bonus(StatType.Strength, 6), Bonus(StatType.Vitality, 6), Bonus(StatType.Agility, 2), Bonus(StatType.Will, 4), Bonus(StatType.Durability, 6), Bonus(StatType.ImpactPower, 5) };
          item.Look = new SoulAppearancePatch { Hair = "Hair5#C64524" };
          item.GrowthWeights = new[] { Bonus(StatType.ImpactPower, 2) };
          item.Job = "수호자"; item.Role = SoulRole.Tank; });
        // Elf mage (design 4 / 6 "마법사 공격안"): no free physical attack, casts attack skills with MP.
        var keepDistance = Pattern("KeepDistance", "거리 유지", SoulPatternCategory.Movement, 1, .6f, 0,
            SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        keepDistance.MoveStyle = SoulMoveStyle.KeepDistance; keepDistance.Priority = 1; EditorUtility.SetDirty(keepDistance);
        flank.MoveStyle = SoulMoveStyle.Flank; flank.Priority = 1; EditorUtility.SetDirty(flank); // a wolf soul changes how the unit closes in
        var cast = Pattern("MagicCast", "마법 시전", SoulPatternCategory.Attack, 3, 1.3f, 4.5f,
            SoulDamageSchool.Magic, SoulDamageKind.Arcane, Value());
        cast.CastsSkills = true; cast.Tags = new[] { "magic" }; EditorUtility.SetDirty(cast);
        var manaBolt = Pattern("ManaBolt", "마력탄", SoulPatternCategory.Attack, 2, 1.1f, 3.5f,
            SoulDamageSchool.Magic, SoulDamageKind.Arcane, Value(2, Term(StatType.Magic, .9f)));
        manaBolt.ManaCost = 3; manaBolt.Tags = new[] { "magic" }; EditorUtility.SetDirty(manaBolt);
        var fireBolt = Asset("SkillFireBolt", (SoulActiveSkillData item) =>
        {
            item.SoulId = "fire_bolt"; item.SkillName = "화염탄"; item.Trigger = SoulTrigger.Cast;
            // Attack magic is dear and rare (MP comes back slowly), and hits hard when it comes: 24 MP for an
            // explosion of 18 + magic x2.2 (x spell power) and a burn on everyone within 1.4 cells.
            item.RequiredPattern = null; item.CastTime = 1.4f; item.Cooldown = 4; item.Range = 4.5f;
            item.Costs = new[] { new SoulResourceCost { Resource = SoulResource.Mana, Amount = 24 } };
            item.DamageSchool = SoulDamageSchool.Magic; item.DamageKind = SoulDamageKind.Fire;
            item.Damage = Value(18, Term(StatType.Magic, 2.2f));
            item.Status = Status(SoulStatus.Burn, Value(.6f), Value(4), Value(3, Term(StatType.Magic, .3f)));
            item.Radius = Value(1.4f); // explodes on the target: everyone within 1.4 cells
            item.Icon = IconSprite("skill:fire_bolt");
        });
        var frostShard = Asset("SkillFrostShard", (SoulActiveSkillData item) =>
        {
            item.SoulId = "frost_shard"; item.SkillName = "서리 파편"; item.Trigger = SoulTrigger.Cast;
            // 30 MP: 22 + magic x2.6 (x spell power) and a hard chill on everyone within 1.3 cells.
            item.RequiredPattern = null; item.CastTime = 1.5f; item.Cooldown = 8; item.Range = 4f;
            item.Costs = new[] { new SoulResourceCost { Resource = SoulResource.Mana, Amount = 30 } };
            item.DamageSchool = SoulDamageSchool.Magic; item.DamageKind = SoulDamageKind.Cold;
            item.Damage = Value(22, Term(StatType.Magic, 2.6f));
            item.Status = Status(SoulStatus.Chill, Value(.9f), Value(3, Term(StatType.Magic, .1f)));
            item.Radius = Value(1.3f); // shards burst around the target
            item.Icon = IconSprite("skill:frost_shard");
        });
        var precise = Asset("PassivePreciseSenses", (SoulPassiveSkillData item) =>
        { item.SoulId = "precise_senses"; item.SkillName = "정교한 감각"; item.AlwaysBonuses = new[] { Bonus(StatType.Accuracy, .05f) }; item.Icon = IconSprite("passive:precise_senses"); });
        var elf = Asset("RaceElf", (SoulRaceData item) =>
        {
            item.Id = "엘프"; item.Height = 1.8f; item.Weight = 60; item.Bonuses = new[] { Bonus(StatType.Agility, 1), Bonus(StatType.Magic, 1) };
            item.StartingPatterns = new[] { dodge, keepDistance }; item.InnatePassives = new[] { precise };
            item.GrowthWeights = new[] { Bonus(StatType.Agility, 3), Bonus(StatType.Magic, 3), Bonus(StatType.PiercePower, 2) };
            item.Appearance = Look("Elf#F9E6CF", "Elf");
        });
        var staff = Asset("EquipmentStaff", (SoulEquipmentData item) => { item.Id = "staff"; item.WeaponTag = "staff"; item.Slot = SoulEquipSlot.MainHand; item.BasicAttack = null; });
        sword.BasicAttack = slash; EditorUtility.SetDirty(sword);
        axe.BasicAttack = swing; EditorUtility.SetDirty(axe);
        var sera = Asset("MercenarySera", (SoulMercenaryData item) =>
        {
            item.Id = "sera"; item.DisplayName = "세라"; item.Race = elf; item.StartingEquipment = new[] { staff };
            item.BaseStats = new[] { Bonus(StatType.Strength, 2), Bonus(StatType.Vitality, 4), Bonus(StatType.Agility, 4), Bonus(StatType.Magic, 6), Bonus(StatType.Will, 4), Bonus(StatType.AntiMagic, 2), Bonus(StatType.Recovery, 3) };
            item.Patterns = new SoulPatternData[0]; item.ActiveSkills = new[] { fireBolt, frostShard };
            item.Look = new SoulAppearancePatch { Hair = "Hair9#E6E0C8", Armor = "BlueWizardTunic" };
            item.GrowthWeights = new[] { Bonus(StatType.Magic, 3), Bonus(StatType.Recovery, 2), Bonus(StatType.AntiMagic, 1) };
            item.Job = "마법사"; item.Role = SoulRole.Ranged;
        });
        // ── design 6/9: encounter taunt, counter, cover; design 5: leap shockwave; design 4: beastkin ──
        // 위협 (the asset keeps its old file name "Taunt"): a support pattern that raises the user's own threat for a
        // while so monsters turn to it. 도발 is a skill of its own and no longer needs this pattern.
        var tauntPattern = Pattern("Taunt", "위협", SoulPatternCategory.Support, 4, .6f, 0, SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        tauntPattern.SupportTarget = SoulSupportTarget.Self; tauntPattern.UseWhen = SoulUseWhen.InCombat; tauntPattern.Cooldown = 12;
        tauntPattern.Buffs = new[] { new SoulBuffEffect { Stat = StatType.Threat, Amount = Value(1.5f, Term(StatType.Durability, .05f)) } };
        tauntPattern.BuffDuration = Value(6); EditorUtility.SetDirty(tauntPattern);
        var taunt = Asset("SkillTaunt", (SoulActiveSkillData item) =>
        {
            // design 9.2 example: threat 200% (fixed), armor 20 + vitality ×150%
            item.SoulId = "taunt"; item.SkillName = "도발"; item.Trigger = SoulTrigger.Encounter;
            item.RequiredPattern = null; item.Cooldown = 12;
            item.Costs = new[] { new SoulResourceCost { Resource = SoulResource.Stamina, Amount = 6 } };
            item.SelfBuffs = new[] { new SoulBuffEffect { Stat = StatType.Threat, Amount = Value(2) }, new SoulBuffEffect { Stat = StatType.Armor, Amount = Value(20, Term(StatType.Vitality, 1.5f)) } };
            item.Duration = Value(6);
            item.Icon = IconSprite("skill:taunt");
        });
        var leap = Asset("SkillLeapShockwave", (SoulActiveSkillData item) =>
        {
            // design 5 example: leap; with weight >= threshold, damage 10 + weight ×10% + strength ×10%
            item.SoulId = "leap_shockwave"; item.SkillName = "도약 충격파"; item.Trigger = SoulTrigger.InRange;
            item.Range = 4.5f; item.Cooldown = 10; item.LeapToTarget = true; item.WeightThreshold = 90;
            item.Costs = new[] { new SoulResourceCost { Resource = SoulResource.Stamina, Amount = 12 } };
            item.DamageSchool = SoulDamageSchool.Physical; item.DamageKind = SoulDamageKind.Impact;
            item.Damage = Value(10, Term(StatType.BodyWeight, .1f), Term(StatType.Strength, .1f));
            item.Radius = Value(1.6f); item.Knockback = .6f;
            item.Icon = IconSprite("skill:leap_shockwave");
        });
        var groundSlam = Asset("SkillGroundSlam", (SoulActiveSkillData item) =>
        {
            item.SoulId = "ground_slam"; item.SkillName = "대지 강타"; item.Trigger = SoulTrigger.InRange;
            item.Range = 4f; item.Cooldown = 7; item.LeapToTarget = true; item.WeightThreshold = 90;
            item.DamageSchool = SoulDamageSchool.Physical; item.DamageKind = SoulDamageKind.Impact;
            item.Damage = Value(6, Term(StatType.BodyWeight, .08f), Term(StatType.Strength, .3f));
            item.Radius = Value(2f); item.Knockback = .9f;
            item.Icon = IconSprite("skill:ground_slam");
        });
        var counter = Pattern("Counter", "받아치기", SoulPatternCategory.Defense, 5, .5f, 0, SoulDamageSchool.Physical, SoulDamageKind.Slash,
            Value(3, Term(StatType.Agility, .8f), Term(StatType.Strength, .5f)));
        counter.DefenseMode = SoulDefenseMode.Counter; counter.GuardMultiplier = .7f; EditorUtility.SetDirty(counter);
        var cover = Pattern("Cover", "엄폐", SoulPatternCategory.Defense, 3, .5f, 0, SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        cover.DefenseMode = SoulDefenseMode.Cover; EditorUtility.SetDirty(cover);
        var claw = Pattern("Claw", "할퀴기", SoulPatternCategory.Attack, 4, .8f, 1.1f, SoulDamageSchool.Physical, SoulDamageKind.Slash,
            Value(0, Term(StatType.Attack, .8f), Term(StatType.SlashPower, .6f)));
        claw.WeaponTag = "claw"; claw.Status = Status(SoulStatus.Bleed, Value(.2f), Value(3), Value(1, Term(StatType.SlashPower, .2f))); EditorUtility.SetDirty(claw);
        var hunter = Asset("PassiveHunterInstinct", (SoulPassiveSkillData item) =>
        { item.SoulId = "hunter_instinct"; item.SkillName = "사냥 본능"; item.AlwaysBonuses = new[] { Bonus(StatType.ExecuteBonus, .25f) }; item.Icon = IconSprite("passive:hunter_instinct"); });
        var beastkin = Asset("RaceBeastkin", (SoulRaceData item) =>
        {
            item.Id = "수인"; item.Height = 1.75f; item.Weight = 80; item.Bonuses = new[] { Bonus(StatType.Strength, 1), Bonus(StatType.Agility, 1) };
            item.StartingPatterns = new[] { counter, flank }; item.InnatePassives = new[] { hunter };
            item.GrowthWeights = new[] { Bonus(StatType.Strength, 3), Bonus(StatType.Agility, 3), Bonus(StatType.SlashPower, 1) };
            item.Appearance = Look("Werewolf", "Werewolf");
        });
        var claws = Asset("EquipmentClaws", (SoulEquipmentData item) => { item.Id = "claws"; item.WeaponTag = "claw"; item.Slot = SoulEquipSlot.MainHand; item.BasicAttack = claw; });
        var karon = Asset("MercenaryKaron", (SoulMercenaryData item) =>
        {
            item.Id = "karon"; item.DisplayName = "카론"; item.Race = beastkin; item.StartingEquipment = new[] { claws };
            item.BaseStats = new[] { Bonus(StatType.Strength, 5), Bonus(StatType.Vitality, 4), Bonus(StatType.Agility, 6), Bonus(StatType.Will, 2), Bonus(StatType.SlashPower, 3) };
            item.GrowthWeights = new[] { Bonus(StatType.Agility, 1) };
            item.Job = "투사"; item.Role = SoulRole.Melee;
        });
        // ── pathfinder (길잡이): luck first, map options instead of fighting power ──
        var pathfinderEye = Asset("PassivePathfinderEye", (SoulPassiveSkillData item) =>
        { item.SoulId = "pathfinder_eye"; item.SkillName = "길잡이의 눈"; item.MapTraits = SoulMapTrait.SeeThroughWalls | SoulMapTrait.WideVision; item.Icon = IconSprite("passive:pathfinder_eye"); });
        var cartography = Asset("PassiveCartography", (SoulPassiveSkillData item) =>
        { item.SoulId = "cartography"; item.SkillName = "지도 제작"; item.MapTraits = SoulMapTrait.RevealExit | SoulMapTrait.RevealGuardian | SoulMapTrait.RouteSense | SoulMapTrait.RevealTreasure; item.Icon = IconSprite("passive:cartography"); });
        var secretPaths = Asset("PassiveSecretPaths", (SoulPassiveSkillData item) =>
        { item.SoulId = "secret_paths"; item.SkillName = "숨은 길 찾기"; item.MapTraits = SoulMapTrait.DetectHiddenDoors; item.Icon = IconSprite("passive:secret_paths"); });
        var shortBow = Asset("EquipmentShortBow", (SoulEquipmentData item) => { item.Id = "short_bow"; item.WeaponTag = "bow"; item.Slot = SoulEquipSlot.MainHand; item.Patterns = new[] { bow }; item.BasicAttack = bow; });
        var luka = Asset("MercenaryLuka", (SoulMercenaryData item) =>
        {
            item.Id = "luka"; item.DisplayName = "루카"; item.Race = human; item.StartingEquipment = new[] { shortBow };
            // A guide, not a fighter: finds the way, the boss, the treasure and the portals; weak in combat.
            item.BaseStats = new[] { Bonus(StatType.Luck, 8), Bonus(StatType.Agility, 5), Bonus(StatType.Vitality, 3), Bonus(StatType.Strength, 1), Bonus(StatType.Will, 3) };
            item.Patterns = new[] { keepDistance }; item.Passives = new[] { pathfinderEye, cartography, secretPaths };
            item.GrowthWeights = new[] { Bonus(StatType.Luck, 3), Bonus(StatType.Agility, 2) };
            item.Look = new SoulAppearancePatch { Hair = "Hair6#5D2C28", Helmet = "ArcherHood", Armor = "ArcherTunic" };
            item.Job = "길잡이"; item.Role = SoulRole.Support;
        });

        // ── goblin archer: a ranged monster, so bodies in the line of fire matter (design 5 가림) ──
        var goblinSoul = Asset("SoulGoblin", (SoulData item) =>
        {
            item.Id = "goblin"; item.OriginMonster = "고블린 궁수"; item.CharacteristicStats = new[] { Bonus(StatType.Agility, 2), Bonus(StatType.PiercePower, 2) };
            item.Patterns = new[] { bow }; item.CorePattern = bow; item.HeightMultiplier = .95f; // dwarves cannot absorb it (bow core)
        });
        var goblin = Monster("MonsterGoblinArcher", "고블린 궁수", 4, 3, new[] { bow, dodge, keepDistance }, goblinSoul, .02f);
        goblin.Stats = new[] { Bonus(StatType.Vitality, 4), Bonus(StatType.Strength, 3), Bonus(StatType.Agility, 5), Bonus(StatType.PiercePower, 3) };
        goblin.UseMonsterSprite = false; goblin.Appearance = new UnitAppearanceData { Body = "Goblin", Head = "Goblin", Ears = "Goblin", Eyes = "Goblin", Weapon = "CurvedBow" };
        goblin.Height = 1.2f; goblin.Weight = 40; goblin.DisplayName = "고블린 궁수";
        EditorUtility.SetDirty(goblin);
        goblinSoul.Source = goblin; EditorUtility.SetDirty(goblinSoul);

        // ── more patterns (design 6) ──
        var whirl = Pattern("Whirl", "회전 베기", SoulPatternCategory.Attack, 9, 1.5f, 1.2f, SoulDamageSchool.Physical, SoulDamageKind.Slash,
            Value(0, Term(StatType.Attack, .9f), Term(StatType.SlashPower, .6f)));
        whirl.AreaRadius = 1.4f; whirl.Priority = 1; EditorUtility.SetDirty(whirl);
        var doubleThrust = Pattern("DoubleThrust", "연속 찌르기", SoulPatternCategory.Attack, 7, 1.3f, 1.4f, SoulDamageSchool.Physical, SoulDamageKind.Pierce,
            Value(0, Term(StatType.Attack, 1), Term(StatType.PiercePower, .8f)));
        doubleThrust.Hits = 2; doubleThrust.Priority = 1; doubleThrust.Status = Status(SoulStatus.Bleed, Value(.2f), Value(3), Value(1, Term(StatType.PiercePower, .2f))); EditorUtility.SetDirty(doubleThrust);
        var kite = Pattern("Kite", "치고 빠지기", SoulPatternCategory.Movement, 2, .6f, 0, SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        kite.MoveStyle = SoulMoveStyle.Kite; kite.Priority = 2; EditorUtility.SetDirty(kite);
        var rush = Pattern("Rush", "돌입", SoulPatternCategory.Movement, 3, .7f, 0, SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        rush.MoveStyle = SoulMoveStyle.Rush; rush.Priority = 2; EditorUtility.SetDirty(rush);
        var escort = Pattern("Escort", "호위", SoulPatternCategory.Movement, 2, .7f, 0, SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        escort.MoveStyle = SoulMoveStyle.Escort; escort.Priority = 2; EditorUtility.SetDirty(escort);
        var alertShotPattern = Pattern("AlertShot", "경계 사격", SoulPatternCategory.Encounter, 0, .6f, 5f, SoulDamageSchool.Physical, SoulDamageKind.Pierce, Value());
        alertShotPattern.WeaponTag = "bow"; EditorUtility.SetDirty(alertShotPattern);
        var alertShot = Asset("SkillAlertShot", (SoulActiveSkillData item) =>
        {
            item.SoulId = "alert_shot"; item.SkillName = "경계 사격"; item.Trigger = SoulTrigger.Encounter;
            item.RequiredPattern = alertShotPattern; item.Range = 5f; item.Cooldown = 8;
            item.Costs = new[] { new SoulResourceCost { Resource = SoulResource.Stamina, Amount = 4 } };
            item.DamageSchool = SoulDamageSchool.Physical; item.DamageKind = SoulDamageKind.Pierce;
            item.Damage = Value(1, Term(StatType.Agility, .3f));
            item.Icon = IconSprite("skill:alert_shot");
        });
        luka.Patterns = new[] { keepDistance, alertShotPattern }; luka.ActiveSkills = new[] { alertShot }; EditorUtility.SetDirty(luka);
        stoneSoul.Patterns = new[] { guard, escort }; EditorUtility.SetDirty(stoneSoul);
        goblin.Patterns = new[] { bow, rollPattern, kite }; EditorUtility.SetDirty(goblin);
        goblinSoul.Patterns = new[] { bow, kite }; EditorUtility.SetDirty(goblinSoul);
        wolfSoul.Patterns = new[] { flank, doubleThrust }; EditorUtility.SetDirty(wolfSoul);
        boarSoul.Patterns = new[] { charge, rush }; EditorUtility.SetDirty(boarSoul);

        // ── status patterns: debuffs carried by a hit, and support patterns (buffs, coating, first aid) ──
        var ankle = Pattern("AnkleStrike", "발목 공격", SoulPatternCategory.Attack, 3, .8f, 1.2f, SoulDamageSchool.Physical, SoulDamageKind.Slash,
            Value(0, Term(StatType.Attack, .5f)));
        ankle.Status = Status(SoulStatus.Slow, Value(.75f), Value(4, Term(StatType.Agility, .05f)));
        ankle.Status.Grade = 2; ankle.Status.GradeBonus = Value(0, Term(StatType.Agility, .04f)); EditorUtility.SetDirty(ankle);
        var ambush = Pattern("Ambush", "암습", SoulPatternCategory.Attack, 6, 1f, 4f, SoulDamageSchool.Physical, SoulDamageKind.Pierce,
            Value(2, Term(StatType.Attack, .9f), Term(StatType.PiercePower, .5f)));
        ambush.Ambush = true; ambush.BackstabMultiplier = 1.6f; ambush.Cooldown = 8;
        ambush.Sizing = SoulAreaSizing.Explicit; ambush.Shape = SoulAreaShape.Cone; ambush.Anchor = SoulAreaAnchor.Forward; ambush.AreaSize = 1.3f; ambush.AreaWidth = 70;
        ambush.Status = Status(SoulStatus.Confuse, Value(.35f, Term(StatType.Agility, .01f)), Value(3));
        ambush.Status.GradeBonus = Value(0, Term(StatType.Agility, .05f)); EditorUtility.SetDirty(ambush);
        var coatPoison = Pattern("ApplyPoison", "독 바르기", SoulPatternCategory.Support, 3, 1f, 0, SoulDamageSchool.Physical, SoulDamageKind.Poison, Value());
        coatPoison.SupportTarget = SoulSupportTarget.Self; coatPoison.UseWhen = SoulUseWhen.InCombat; coatPoison.Cooldown = 18;
        coatPoison.Imbue = Status(SoulStatus.Poison, Value(.5f), Value(5), Value(1));
        coatPoison.Imbue.GradeBonus = Value(0, Term(StatType.Agility, .04f)); coatPoison.ImbueDuration = Value(12); EditorUtility.SetDirty(coatPoison);
        var encourage = Pattern("Encourage", "격려", SoulPatternCategory.Support, 4, .8f, 0, SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        encourage.SupportTarget = SoulSupportTarget.Allies; encourage.UseWhen = SoulUseWhen.InCombat; encourage.SupportRadius = 5; encourage.Cooldown = 16;
        encourage.Buffs = new[] { new SoulBuffEffect { Stat = StatType.StaminaRegen, Amount = Value(3, Term(StatType.Will, .2f)) } };
        encourage.BuffDuration = Value(8); EditorUtility.SetDirty(encourage);
        var firstAid = Pattern("FirstAid", "응급 처치", SoulPatternCategory.Support, 3, 1.2f, 0, SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        firstAid.SupportTarget = SoulSupportTarget.Allies; firstAid.UseWhen = SoulUseWhen.OutOfCombat; firstAid.SupportRadius = 3; firstAid.Cooldown = 2;
        firstAid.HealRatio = .05f; EditorUtility.SetDirty(firstAid);
        luka.Patterns = new[] { keepDistance, alertShotPattern, encourage, firstAid }; EditorUtility.SetDirty(luka);
        karon.Patterns = new[] { ambush, coatPoison }; EditorUtility.SetDirty(karon);
        ria.Patterns = new[] { ankle }; EditorUtility.SetDirty(ria);
        // MP: meditation between fights, a short breath in one (only when MP runs low)
        var meditate = Pattern("Meditate", "명상", SoulPatternCategory.Support, 0, 1.5f, 0, SoulDamageSchool.Magic, SoulDamageKind.Arcane, Value());
        meditate.SupportTarget = SoulSupportTarget.Self; meditate.UseWhen = SoulUseWhen.OutOfCombat; meditate.Cooldown = 2;
        meditate.ManaRatio = .05f; meditate.Cooldown = 5; meditate.UseBelow = .95f; meditate.Tags = new[] { "magic" };
        // 명상 is a passive now (always faster MP), no longer a pattern: the old pattern points at it for saves
        var staffBound = Asset("PassiveStaffBound", (SoulPassiveSkillData item) =>
        {
            item.SoulId = "staff_bound"; item.SkillName = "지팡이의 마력"; item.Description = "마력은 지팡이를 타고 흐릅니다. 지팡이가 아닌 무기를 들면 마법 효과가 40% 줄어듭니다.";
            item.AlwaysBonuses = new[] { Bonus(StatType.StaffBound, .4f) }; item.Icon = IconSprite("passive:staff_bound");
        });
        var meditation = Asset("PassiveMeditation", (SoulPassiveSkillData item) =>
        {
            item.SoulId = "meditation"; item.SkillName = "명상"; item.Description = "MP 회복 속도가 50% 빨라집니다.";
            item.AlwaysBonuses = new[] { Bonus(StatType.ManaRegenRate, .5f) }; item.Icon = IconSprite("passive:meditation");
        });
        meditate.NowPassive = meditation; EditorUtility.SetDirty(meditate);
        // 기도: in a fight, a little HP for the most hurt ally around (a pattern: stamina, not MP — so very little)
        var prayer = Pattern("Prayer", "기도", SoulPatternCategory.Support, 6, 1.2f, 0, SoulDamageSchool.Magic, SoulDamageKind.Arcane, Value());
        prayer.SupportTarget = SoulSupportTarget.Allies; prayer.UseWhen = SoulUseWhen.InCombat; prayer.SupportRadius = 4; prayer.Cooldown = 4;
        prayer.HealRatio = 0; prayer.Heal = Value(4, Term(StatType.Magic, .2f)); prayer.UseBelow = .9f; EditorUtility.SetDirty(prayer);
        // 주문 집중 (마법사): while no attack spell can go, stamina gathers focus for the next one
        var spellFocus = Pattern("SpellFocus", "주문 집중", SoulPatternCategory.Support, 8, 1f, 0, SoulDamageSchool.Magic, SoulDamageKind.Arcane, Value());
        spellFocus.SupportTarget = SoulSupportTarget.Self; spellFocus.UseWhen = SoulUseWhen.InCombat; spellFocus.Cooldown = 2; spellFocus.SpellFocus = true;
        spellFocus.Tags = new[] { "magic" }; EditorUtility.SetDirty(spellFocus);
        // 자연 교감 (소환사): the party (its spirits too) a little quicker, stacking up to ten
        var communion = Pattern("NatureCommunion", "자연 교감", SoulPatternCategory.Support, 6, 1f, 0, SoulDamageSchool.Magic, SoulDamageKind.Arcane, Value());
        communion.SupportTarget = SoulSupportTarget.Allies; communion.UseWhen = SoulUseWhen.InCombat; communion.SupportRadius = 6; communion.Cooldown = 5;
        communion.Buffs = new[] { new SoulBuffEffect { Stat = StatType.ActionSpeed, Amount = Value(.02f) } }; communion.BuffDuration = Value(12); communion.MaxStacks = 10;
        EditorUtility.SetDirty(communion);
        var manaBreath = Pattern("ManaBreath", "마력 호흡", SoulPatternCategory.Support, 3, 1.2f, 0, SoulDamageSchool.Magic, SoulDamageKind.Arcane, Value());
        manaBreath.SupportTarget = SoulSupportTarget.Self; manaBreath.UseWhen = SoulUseWhen.InCombat; manaBreath.Cooldown = 20; // MP is scarce: a small gulp now and then
        manaBreath.ManaRatio = .1f; manaBreath.UseBelow = .4f; manaBreath.Tags = new[] { "magic" }; EditorUtility.SetDirty(manaBreath);
        // Magic is a skill, not a pattern: 마력탄 is the cheap everyday spell, 화염탄 / 서리 파편 the strong ones.
        var manaBoltSkill = Asset("SkillManaBolt", (SoulActiveSkillData item) =>
        {
            item.SoulId = "mana_bolt"; item.SkillName = "마력탄"; item.Trigger = SoulTrigger.Cast;
            item.RequiredPattern = null; item.CastTime = 1.1f; item.Cooldown = 2; item.Range = 3.5f;
            item.Costs = new[] { new SoulResourceCost { Resource = SoulResource.Mana, Amount = 12 } };
            item.DamageSchool = SoulDamageSchool.Magic; item.DamageKind = SoulDamageKind.Arcane;
            item.Damage = Value(8, Term(StatType.Magic, 1.4f)); item.Radius = Value(.9f);
            item.Icon = IconSprite("skill:mana_bolt");
        });
        // a healing spell: the one skill (besides potions and the church) that closes wounds
        var healingLight = Asset("SkillHealingLight", (SoulActiveSkillData item) =>
        {
            item.SoulId = "healing_light"; item.SkillName = "치유의 빛"; item.Trigger = SoulTrigger.AllyHurt; item.RequiredJob = "성직자";
            item.RequiredPattern = null; item.CastTime = 1f; item.Cooldown = 18; item.Range = 4f;
            item.Costs = new[] { new SoulResourceCost { Resource = SoulResource.Mana, Amount = 12 } };
            item.Heal = Value(10, Term(StatType.Magic, 1.2f)); item.HealsWounds = true; item.Damage = new SoulValue();
            item.WoundChance = Value(.25f, Term(StatType.Magic, .01f)); // 25% + 마력 1당 1% (마력 7: 32%)
            item.Icon = IconSprite("skill:healing_light");
        });
        // The mage's other side: element enchants for the allies' weapons, buffs, and curses on the enemy.
        SoulActiveSkillData Enchant(string file, string id, string name, SoulStatusApply imbue, SoulBuffEffect[] buffs, float mana, float cooldown)
            => Asset(file, (SoulActiveSkillData item) =>
            {
                item.SoulId = id; item.SkillName = name; item.Trigger = SoulTrigger.AllySupport; item.RequiredPattern = null;
                item.CastTime = 1f; item.Cooldown = cooldown; item.Range = 0; item.Radius = Value(5);
                item.Costs = new[] { new SoulResourceCost { Resource = SoulResource.Mana, Amount = mana } };
                item.Imbue = imbue ?? new SoulStatusApply(); item.SelfBuffs = buffs ?? new SoulBuffEffect[0];
                item.Duration = Value(10, Term(StatType.Magic, .3f)); item.Damage = new SoulValue(); item.Heal = new SoulValue();
                item.Icon = IconSprite("skill:" + id);
            });
        SoulActiveSkillData Curse(string file, string id, string name, SoulStatusApply status, float range, float radius, float mana, float cooldown)
            => Asset(file, (SoulActiveSkillData item) =>
            {
                item.SoulId = id; item.SkillName = name; item.Trigger = SoulTrigger.Cast; item.RequiredPattern = null;
                item.CastTime = 1f; item.Cooldown = cooldown; item.Range = range; item.Radius = Value(radius);
                item.Costs = new[] { new SoulResourceCost { Resource = SoulResource.Mana, Amount = mana } };
                item.DamageSchool = SoulDamageSchool.Magic; item.DamageKind = SoulDamageKind.Arcane; item.Damage = new SoulValue();
                item.Status = status; item.Icon = IconSprite("skill:" + id);
            });
        var fireStatus = Status(SoulStatus.Burn, Value(.35f, Term(StatType.Magic, .01f)), Value(3), Value(1, Term(StatType.Magic, .2f)));
        var fireEnchant = Enchant("SkillFireEnchant", "fire_enchant", "화염 부여", fireStatus,
            new[] { new SoulBuffEffect { Stat = StatType.Attack, Amount = Value(2, Term(StatType.Magic, .2f)) } }, 14, 20);
        var frostEnchant = Enchant("SkillFrostEnchant", "frost_enchant", "냉기 부여", Status(SoulStatus.Chill, Value(.4f, Term(StatType.Magic, .01f)), Value(2.5f)), null, 12, 20);
        var haste = Enchant("SkillHaste", "haste", "신속", null,
            new[] { new SoulBuffEffect { Stat = StatType.ActionSpeed, Amount = Value(.12f, Term(StatType.Magic, .005f)) }, new SoulBuffEffect { Stat = StatType.MoveSpeed, Amount = Value(.3f) } }, 14, 24);
        var weakStatus = Status(SoulStatus.Weaken, Value(.8f, Term(StatType.Magic, .01f)), Value(6));
        weakStatus.GradeBonus = Value(0, Term(StatType.Magic, .08f));
        var curseWeakness = Curse("SkillCurseWeakness", "curse_weakness", "약화의 저주", weakStatus, 4.5f, 1.4f, 10, 5);
        var slowStatus = Status(SoulStatus.Slow, Value(.8f, Term(StatType.Magic, .01f)), Value(5));
        slowStatus.Grade = 2;
        var slowSpell = Curse("SkillSlow", "slow_spell", "둔화", slowStatus, 4f, 1.6f, 12, 7);
        sera.Patterns = new[] { manaBreath, keepDistance, spellFocus }; sera.Passives = new[] { meditation, staffBound };
        // attack magic (dear and strong) first, then the curses and the allies' enchants and haste
        sera.ActiveSkills = new[] { fireBolt, frostShard, curseWeakness, slowSpell, fireEnchant, frostEnchant, haste }; EditorUtility.SetDirty(sera);

        // The priest: church-trained, the only one who heals, and in the end brings the fallen back (소생, Lv.30).
        var resurrection = Asset("SkillResurrection", (SoulActiveSkillData item) =>
        {
            item.SoulId = "resurrection"; item.SkillName = "소생"; item.Trigger = SoulTrigger.AllyFallen;
            item.RequiredJob = "성직자"; item.ReviveRatio = .3f;
            item.RequiredPattern = null; item.CastTime = 2f; item.Cooldown = 120; item.Range = 4f;
            item.Costs = new[] { new SoulResourceCost { Resource = SoulResource.Mana, Amount = 40 } };
            item.Damage = new SoulValue(); item.Heal = new SoulValue();
            item.Icon = IconSprite("skill:resurrection");
        });
        var priest = Asset("MercenaryPriest", (SoulMercenaryData item) =>
        {
            item.Id = "priest"; item.DisplayName = "마리엘"; item.Race = human; item.StartingEquipment = new[] { staff };
            item.BaseStats = new[] { Bonus(StatType.Strength, 2), Bonus(StatType.Vitality, 5), Bonus(StatType.Agility, 3), Bonus(StatType.Magic, 5), Bonus(StatType.Will, 6), Bonus(StatType.Luck, 2), Bonus(StatType.Recovery, 2) };
            // 소생 is not hers from the start: it has to be learned somewhere (the route is still to be decided)
            item.Patterns = new[] { keepDistance, prayer }; item.Passives = new[] { meditation, staffBound }; item.ActiveSkills = new[] { healingLight };
            item.GrowthWeights = new[] { Bonus(StatType.Will, 3), Bonus(StatType.Magic, 2), Bonus(StatType.Recovery, 1) };
            item.Look = new SoulAppearancePatch { Hair = "Hair6#E8E0C0", Armor = "TravelerTunic" };
            item.Job = "성직자"; item.Role = SoulRole.Support;
        });
        // 궁수: the bow is its trade — it keeps its distance and shoots (no map sense, unlike the 길잡이)
        Asset("MercenaryArcher", (SoulMercenaryData item) =>
        {
            item.Id = "archer"; item.DisplayName = "실라스"; item.Race = human; item.StartingEquipment = new[] { shortBow };
            item.BaseStats = new[] { Bonus(StatType.Agility, 6), Bonus(StatType.Strength, 3), Bonus(StatType.Vitality, 3), Bonus(StatType.Will, 2), Bonus(StatType.Luck, 2), Bonus(StatType.PiercePower, 3) };
            item.Patterns = new[] { keepDistance }; item.Passives = new SoulPassiveSkillData[0];
            item.GrowthWeights = new[] { Bonus(StatType.Agility, 3), Bonus(StatType.PiercePower, 2), Bonus(StatType.Strength, 1) };
            item.Look = new SoulAppearancePatch { Hair = "Hair3#8A4836", Helmet = "ArcherHood", Armor = "ArcherTunic" };
            item.Job = "궁수"; item.Role = SoulRole.Ranged;
        });
        // 소환사: spirits fight at its side (SoulFieldKind.Spirit); itself it stays behind, like a mage
        Asset("MercenarySummoner", (SoulMercenaryData item) =>
        {
            item.Id = "summoner"; item.DisplayName = "이리스"; item.Race = elf; item.StartingEquipment = new[] { staff };
            item.BaseStats = new[] { Bonus(StatType.Magic, 6), Bonus(StatType.Will, 5), Bonus(StatType.Vitality, 3), Bonus(StatType.Agility, 3), Bonus(StatType.Luck, 1), Bonus(StatType.Recovery, 2) };
            item.Patterns = new[] { keepDistance, communion }; item.Passives = new[] { meditation, staffBound };
            item.GrowthWeights = new[] { Bonus(StatType.Magic, 3), Bonus(StatType.Will, 2), Bonus(StatType.Recovery, 1) };
            item.Look = new SoulAppearancePatch { Hair = "Hair9#B8C8F8", Armor = "DruidRobe" };
            item.Job = "소환사"; item.Role = SoulRole.Ranged;
        });
        // Stamina: a free pattern now and then, a follow-up out of an evasion, a second wind when running dry.
        var effortless = Asset("PassiveEffortless", (SoulPassiveSkillData item) =>
        { item.SoulId = "effortless"; item.SkillName = "무념 동작"; item.AlwaysBonuses = new[] { Bonus(StatType.FreePatternChance, .15f) }; item.Icon = IconSprite("passive:effortless"); });
        var followup = Pattern("EvadeFollowup", "회피 반격", SoulPatternCategory.Chain, 0, .3f, 0, SoulDamageSchool.Physical, SoulDamageKind.Slash, Value());
        followup.ChainTrigger = SoulChainTrigger.Evade; followup.FollowupWindow = 1.5f; followup.FollowupMultiplier = 1.3f; followup.FollowupInstant = true; EditorUtility.SetDirty(followup);
        var secondWind = Pattern("SecondWind", "호흡 조절", SoulPatternCategory.Support, 0, 1.2f, 0, SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        secondWind.SupportTarget = SoulSupportTarget.Self; secondWind.UseWhen = SoulUseWhen.InCombat; secondWind.Cooldown = 25;
        secondWind.StaminaRatio = .25f; secondWind.UseBelow = .3f; EditorUtility.SetDirty(secondWind);
        // stamina passives
        var endurance = Asset("PassiveEndurance", (SoulPassiveSkillData item) =>
        { item.SoulId = "endurance"; item.SkillName = "지구력"; item.AlwaysBonuses = new[] { Bonus(StatType.MaxStamina, 20) }; item.Icon = IconSprite("passive:endurance"); });
        var battleBreath = Asset("PassiveBattleBreath", (SoulPassiveSkillData item) =>
        { item.SoulId = "battle_breath"; item.SkillName = "전투 호흡"; item.SoulEvent = SoulTrigger.OnKill; item.Stamina = 10; item.Icon = IconSprite("passive:battle_breath"); });
        var familiar = Asset("PassiveFamiliarWeapon", (SoulPassiveSkillData item) =>
        { item.SoulId = "familiar_weapon"; item.SkillName = "몸에 익은 무기"; item.AlwaysBonuses = new[] { Bonus(StatType.RepeatCostReduce, .2f) }; item.Icon = IconSprite("passive:familiar_weapon"); });
        var tireless = Asset("PassiveTireless", (SoulPassiveSkillData item) =>
        { item.SoulId = "tireless"; item.SkillName = "피로 무시"; item.AlwaysBonuses = new[] { Bonus(StatType.TirelessWalk, .3f) }; item.Icon = IconSprite("passive:tireless"); });
        var adrenaline = Asset("PassiveAdrenaline", (SoulPassiveSkillData item) =>
        { item.SoulId = "adrenaline"; item.SkillName = "아드레날린"; item.AlwaysBonuses = new[] { Bonus(StatType.LowHpCostReduce, .3f) }; item.Icon = IconSprite("passive:adrenaline"); });
        // chains (연계)
        var guardThrust = Pattern("GuardThrust", "막고 찌르기", SoulPatternCategory.Chain, 0, .3f, 0, SoulDamageSchool.Physical, SoulDamageKind.Pierce, Value());
        guardThrust.ChainTrigger = SoulChainTrigger.Guard; guardThrust.FollowupWindow = 1.5f; guardThrust.FollowupMultiplier = 1f;
        guardThrust.FollowupArmorIgnore = .5f; guardThrust.FollowupInstant = true; EditorUtility.SetDirty(guardThrust);
        var rollSlash = Pattern("RollSlash", "구르며 베기", SoulPatternCategory.Chain, 0, .3f, 0, SoulDamageSchool.Physical, SoulDamageKind.Slash,
            Value(0, Term(StatType.Attack, .6f), Term(StatType.SlashPower, .4f)));
        rollSlash.ChainTrigger = SoulChainTrigger.Roll; rollSlash.StrikeRadius = 1f; EditorUtility.SetDirty(rollSlash);
        var comboFinish = Pattern("ComboFinish", "연타 마무리", SoulPatternCategory.Chain, 0, .3f, 0, SoulDamageSchool.Physical, SoulDamageKind.Slash, Value());
        comboFinish.ChainTrigger = SoulChainTrigger.Combo; comboFinish.ComboHits = 3; comboFinish.FollowupWindow = 4; comboFinish.FollowupMultiplier = 1.5f;
        comboFinish.FollowupInstant = false; EditorUtility.SetDirty(comboFinish);
        var kiteMaster = Pattern("KiteMaster", "치고 빠지기 강화", SoulPatternCategory.Movement, 2, .6f, 0, SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        kiteMaster.MoveStyle = SoulMoveStyle.Kite; kiteMaster.Priority = 3; kiteMaster.FreeRetreat = true; EditorUtility.SetDirty(kiteMaster);
        // support: a deliberate pause for breath, a shout that makes every pattern free for a moment
        var catchBreath = Pattern("CatchBreath", "숨 돌리기", SoulPatternCategory.Support, 0, 1.5f, 0, SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        catchBreath.SupportTarget = SoulSupportTarget.Self; catchBreath.UseWhen = SoulUseWhen.InCombat; catchBreath.Cooldown = 12;
        catchBreath.StaminaRatio = .2f; catchBreath.UseBelow = .5f; EditorUtility.SetDirty(catchBreath);
        var kihap = Pattern("Kihap", "기합", SoulPatternCategory.Support, 20, .6f, 0, SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        kihap.SupportTarget = SoulSupportTarget.Self; kihap.UseWhen = SoulUseWhen.InCombat; kihap.Cooldown = 30;
        kihap.Buffs = new[] { new SoulBuffEffect { Stat = StatType.FreePatternChance, Amount = Value(1) } }; kihap.BuffDuration = Value(5); EditorUtility.SetDirty(kihap);

        dwarf.InnatePassives = new[] { sturdy, endurance }; EditorUtility.SetDirty(dwarf);
        karon.Passives = new[] { effortless, battleBreath }; karon.Patterns = new[] { claw, ambush, coatPoison, comboFinish }; EditorUtility.SetDirty(karon);
        luka.Patterns = new[] { keepDistance, alertShotPattern, encourage, firstAid, followup };
        luka.Passives = new[] { pathfinderEye, cartography, secretPaths, tireless }; EditorUtility.SetDirty(luka);
        ria.Patterns = new[] { slash, ankle, secondWind, guardThrust, kihap }; ria.Passives = new[] { familiar }; EditorUtility.SetDirty(ria);
        thor.Patterns = new[] { tauntPattern, secondWind, guardThrust, catchBreath }; thor.Passives = new[] { adrenaline }; EditorUtility.SetDirty(thor);

        thor.Patterns = new[] { swing, tauntPattern }; thor.ActiveSkills = new[] { leap, taunt }; EditorUtility.SetDirty(thor);

        var sting = Pattern("PoisonSting", "독침", SoulPatternCategory.Attack, 4, 1.1f, 1.3f, SoulDamageSchool.Physical, SoulDamageKind.Pierce,
            Value(1, Term(StatType.Attack, .7f)));
        sting.Status = Status(SoulStatus.Poison, Value(.45f), Value(4), Value(1.5f)); sting.Tags = new[] { "venom" }; EditorUtility.SetDirty(sting);
        var venom = Asset("PassiveVenomBody", (SoulPassiveSkillData item) =>
        { item.SoulId = "venom_body"; item.SkillName = "독성 체질"; item.AlwaysBonuses = new[] { Bonus(StatType.Regeneration, 1) }; item.Icon = IconSprite("passive:venom_body"); });
        var slugSoul = Asset("SoulSlug", (SoulData item) =>
        {
            item.Id = "slug"; item.OriginMonster = "독 슬라임"; item.CharacteristicStats = new[] { Bonus(StatType.Regeneration, 2), Bonus(StatType.Vitality, 1) };
            item.Patterns = new[] { sting }; item.Passives = new[] { venom }; item.CorePattern = sting; item.HeightMultiplier = .95f;
        });
        var slug = Monster("MonsterSlug", "독 민달팽이", 6, 3, new[] { sting, approach }, slugSoul, .02f);
        MonsterLook(slug, EnemyRace.Slug, new Color(.75f, 1f, .7f), 1.1f, 60);
        slug.DisplayName = "독 슬라임"; slugSoul.Source = slug; EditorUtility.SetDirty(slugSoul);
        var guardian = Monster("MonsterGuardian", "유적 수호자", 34, 10, new[] { swing, guard, approach }, stoneSoul, .4f);
        guardian.Stats = new[] { Bonus(StatType.Vitality, 34), Bonus(StatType.Strength, 10), Bonus(StatType.Durability, 6), Bonus(StatType.ImpactPower, 6), Bonus(StatType.Will, 8) };
        guardian.ActiveSkills = new[] { groundSlam }; guardian.Guardian = true; guardian.Experience = 30; guardian.Gold = 40;
        MonsterLook(guardian, EnemyRace.Troll, new Color(.55f, .58f, .68f), 2.8f, 230);
        guardian.DisplayName = "유적 수호자";
        EditorUtility.SetDirty(guardian);

        // design 13 test room (small, fixed layout) — rule checks rely on these coordinates
        var testDungeon = Asset("DungeonTest", (SoulDungeonData item) =>
        {
            item.MapRows = "################\n#..............#\n#...##.........#\n#...##..###....#\n#.......#......#\n#.......#..##..#\n#..##......##..#\n#..##..........#\n#..............#\n################";
            item.PartyStart = new Vector2Int(2, 7); item.Exit = new Vector2Int(14, 1);
            item.Monsters = new[] { Spawn(boar, 6, 7), Spawn(wolf, 10, 2), Spawn(stone, 13, 7) };
            item.LevelPatternPool = new[] { thrust, swing, charge, dodge, rollPattern, flank, bow, keepDistance, counter, cover, whirl, doubleThrust, kite, rush, escort, ankle, ambush, coatPoison, tauntPattern, encourage, firstAid, manaBreath, followup, secondWind, guardThrust, rollSlash, comboFinish, kiteMaster, catchBreath, kihap };
        });

        // boss loot: a defeated guardian drops a chest with two of these
        // boss loot (filled in by CreateEquipmentCatalog)
        var guardianBlade = Asset("EquipmentGuardianBlade", (SoulEquipmentData item) => { item.Id = "guardian_blade"; item.Unique = true; });
        var ruinShield = Asset("EquipmentRuinShield", (SoulEquipmentData item) => { item.Id = "ruin_shield"; item.Unique = true; });
        var ancientRing = Asset("EquipmentAncientRing", (SoulEquipmentData item) => { item.Id = "ancient_ring"; item.Unique = true; });
        var hunterBow = Asset("EquipmentHunterBow", (SoulEquipmentData item) => { item.Id = "hunter_bow"; item.Unique = true; });

        // ── encounter monsters: goblin warriors hold the front line for the archers; the berserker is the elite ──
        var goblinWarrior = Monster("MonsterGoblinWarrior", "고블린 전사", 6, 5, new[] { swing, guard, approach }, goblinSoul, .015f);
        goblinWarrior.Stats = new[] { Bonus(StatType.Vitality, 6), Bonus(StatType.Strength, 5), Bonus(StatType.Durability, 2), Bonus(StatType.Agility, 2) };
        goblinWarrior.UseMonsterSprite = false;
        goblinWarrior.Appearance = new UnitAppearanceData { Body = "Goblin", Head = "Goblin", Ears = "Goblin", Eyes = "Goblin", Weapon = "Longsword" };
        goblinWarrior.Height = 1.25f; goblinWarrior.Weight = 48; goblinWarrior.DisplayName = "고블린 전사";
        EditorUtility.SetDirty(goblinWarrior);

        // An elite fights alone (or at the head of a mixed horde), so on its own it must weigh more than a whole
        // goblin pack — ValidateEncounterStrength measures that.
        var chiefSweep = Telegraph("TelegraphChiefSweep", "chief_sweep", "광전사의 휩쓸기", SoulAreaShape.Cone, SoulAreaAnchor.Forward, 3.2f, 100, 1f, 4f, 2.8f,
            Value(8, Term(StatType.Attack, 1.2f)), .8f);
        var goblinChief = Monster("MonsterGoblinChief", "고블린 광전사", 80, 16, new[] { swing, guard, approach }, goblinSoul, .12f);
        goblinChief.Stats = new[] { Bonus(StatType.Vitality, 80), Bonus(StatType.Strength, 16), Bonus(StatType.Durability, 6), Bonus(StatType.Agility, 4), Bonus(StatType.Will, 8), Bonus(StatType.ImpactPower, 4), Bonus(StatType.Threat, 1.5f) };
        goblinChief.UseMonsterSprite = false;
        goblinChief.Appearance = new UnitAppearanceData { Body = "Goblin", Head = "Goblin", Ears = "Goblin", Eyes = "Goblin", Weapon = "Greataxe" };
        goblinChief.Height = 1.6f; goblinChief.Weight = 80; goblinChief.Tint = new Color(1f, .82f, .78f);
        goblinChief.Elite = true; goblinChief.Telegraphs = new[] { chiefSweep }; goblinChief.Experience = 30; goblinChief.Gold = 25;
        goblinChief.DisplayName = "고블린 광전사";
        EditorUtility.SetDirty(goblinChief);

        // ── boss patterns, always in this order: around itself → a line ahead → a wide cone → rocks on the target ──
        var bossSlam = Telegraph("TelegraphBossSlam", "boss_slam", "대지 강타", SoulAreaShape.Circle, SoulAreaAnchor.Self, 3.2f, 1, 1.6f, 1.5f, 2.5f,
            Value(10, Term(StatType.BodyWeight, .05f), Term(StatType.Strength, .5f)), 1f);
        var bossCleave = Telegraph("TelegraphBossCleave", "boss_cleave", "파쇄 참격", SoulAreaShape.Box, SoulAreaAnchor.Forward, 7f, 2.2f, 1.2f, 1.5f, 5f,
            Value(9, Term(StatType.Strength, .6f)), .4f);
        var bossSweep = Telegraph("TelegraphBossSweep", "boss_sweep", "부채꼴 휩쓸기", SoulAreaShape.Cone, SoulAreaAnchor.Forward, 4.5f, 110, 1.1f, 1.5f, 3.5f,
            Value(8, Term(StatType.Strength, .5f)), .6f);
        var bossRocks = Telegraph("TelegraphBossRocks", "boss_rocks", "낙석", SoulAreaShape.Circle, SoulAreaAnchor.Target, 1.8f, 1, 1.4f, 2.5f, 8f,
            Value(9, Term(StatType.Strength, .4f)), 0);
        guardian.Stats = new[] { Bonus(StatType.Vitality, 140), Bonus(StatType.Strength, 16), Bonus(StatType.Durability, 10), Bonus(StatType.ImpactPower, 8), Bonus(StatType.Will, 12), Bonus(StatType.Threat, 2.5f) };
        guardian.ActiveSkills = new SoulActiveSkillData[0]; // its telegraphed patterns replace the leap slam
        guardian.Telegraphs = new[] { bossSlam, bossCleave, bossSweep, bossRocks };
        EditorUtility.SetDirty(guardian);

        // the play dungeon: 370×238 tiles (about 44× the first 56×36 sample), a new map on every play. Singles near the
        // start, packs (3–8) waiting in formation at the ways in, hordes (elite + packs, 10+) in large halls,
        // the boss alone in the farthest hall; a portal at each edge.
        var dungeon = Asset("DungeonRuins", (SoulDungeonData item) =>
        {
            item.Procedural = true; item.Width = 370; item.Height = 238; item.Halls = 6; item.EmptyRooms = .35f;
            item.MapRows = ""; item.Monsters = new SoulMonsterSpawn[0]; item.Exits = new Vector2Int[0];
            item.Singles = new[] { boar, wolf, slug };
            item.Elites = new[] { goblinChief };
            item.Packs = new[]
            {
                Pack("고블린 무리", new[] { goblinWarrior }, new[] { goblin }, 4, 6, .4f, .12f, 1f),
                Pack("늑대 무리", new[] { wolf }, null, 3, 6, 0, .12f, .75f),
                Pack("멧돼지 떼", new[] { boar }, null, 3, 4, 0, .12f, .45f),
                Pack("슬라임 무리", new[] { slug }, null, 4, 8, 0, .35f, 1f),
                Pack("석상 수비대", new[] { stone }, new[] { goblin }, 3, 5, .4f, .6f, 1f),
            };
            item.Boss = guardian;
            item.BossGatesExit = false; // the boss is optional: its chest is the reward, the portals are always open
            item.BossLoot = new[] { guardianBlade, ruinShield, ancientRing, hunterBow }; item.BossLootCount = 2;
            item.LevelPatternPool = new[] { thrust, swing, charge, dodge, rollPattern, flank, bow, keepDistance, counter, cover, whirl, doubleThrust, kite, rush, escort, ankle, ambush, coatPoison, tauntPattern, encourage, firstAid, manaBreath, followup, secondWind, guardThrust, rollSlash, comboFinish, kiteMaster, catchBreath, kihap };
        });
        dungeon.Floors = FloorRosters(boar, wolf, slug, stone, goblin, goblinWarrior, goblinChief, guardian, swing, thrust, bow, guard, approach, dodge,
            keepDistance, charge, flank, whirl, manaBolt, chiefSweep, new[] { bossSlam, bossCleave, bossSweep, bossRocks });
        EditorUtility.SetDirty(dungeon);

        var unitPrefab = CreateUnitPrefab();
        SoulMercenariesAssetBuilder.BuildPopupPrefabs();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .04f, .07f); camera.orthographic = true;
        cameraObject.transform.position = new Vector3(0, 0, -10);
        var root = new GameObject("Soul Mercenaries");
        var controller = root.AddComponent<SoulGameplayController>();
        controller.Dungeon = dungeon; controller.StatRules = rules; controller.Party = new[] { ria, thor, sera, karon, luka };
        var hud = new GameObject("HUD").AddComponent<SoulHudView>();
        hud.Controller = controller;
        var world = new GameObject("World").AddComponent<SoulWorldView>();
        world.Controller = controller; world.Hud = hud; world.Camera = camera;
        hud.World = world;
        world.CharacterPrefab = unitPrefab;
        world.Ground = FirstSprite(GroundPath);
        world.MagicBolt = FirstSprite("Assets/_project/3.Textures/FX/projectile_magicbolt.png");
        world.Arrow = FirstSprite("Assets/_project/3.Textures/FX/projectile_arrow.png");
        SoulMercenariesAssetBuilder.CreatePopupManager();
        var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>(); // same module as the original scenes
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(entry => entry.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Soul Mercenaries gameplay scene created: " + ScenePath);

        var catalog = CreateEquipmentCatalog(slash, swing, thrust, claw, bow);
        var dungeonData = dungeon;
        dungeonData.LootTable = catalog.FindAll(item => !item.Unique).ToArray();
        dungeonData.BossLoot = new[] { guardianBlade, ruinShield, ancientRing, hunterBow, Load("EquipmentFireSword"), Load("EquipmentStormStaff"), Load("EquipmentShadowDagger") };
        dungeonData.BossLootCount = 2;
        CreateJobSkills(dungeonData);
        CreateSoulSkills();
        CreateFxMaterial();
        BakeMonsterPortraits(unitPrefab);
        ManaBound(); // before the books: their upgraded copies follow
        dungeonData.SkillBooks = SkillBookPool();
        EditorUtility.SetDirty(dungeonData);
        RefineSouls();
        AssetDatabase.SaveAssets();
        var priestData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryPriest.asset");
        var newJobs = new[] { AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryArcher.asset"), AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenarySummoner.asset") };
        ImportSpirits();
        var village = CreateVillage(rules, dungeon, new[] { ria, thor, sera, karon, luka }, newJobs, priestData,
            catalog.FindAll(item => item.ShopTier > 0).ToArray(), dungeon.LevelPatternPool);
        CreateVillageScene(village);
    }

    // Every asset of a type in the data folder (the save file finds things again through these).
    static T[] All<T>() where T : ScriptableObject
    {
        var list = new List<T>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DataPath }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) list.Add(asset);
        }
        return list.ToArray();
    }

    static void Scale(SoulValue value, float k)
    {
        if (value == null) return;
        value.Flat *= k;
        if (value.Terms != null) for (int i = 0; i < value.Terms.Length; i++) value.Terms[i].Factor *= k;
    }

    // The stronger form a skill turns into at the library (level 3 + one more book): the same skill, stronger
    // and quicker. Recreated from the base every time the content is built.
    // Every monster's idle frame, baked once into one sprite sheet (SoulMonsterData.Portrait): a soul's picture
    // without that monster on screen, with nothing loaded but this sheet. CharacterBuilder monsters: their look and
    // tint, the whole 64-pixel frame. Sprite-library monsters (boar, wolf, slime, troll kinds — one library shared by
    // several of them): the library's first idle frame, tinted as that monster, cut to what is drawn (so a card shows
    // it big).
    const string MonsterSheetPath = "Assets/_project/3.Textures/Icons/SoulMonsterPortraits.png";
    const int PortraitCell = 64, PortraitColumns = 8;
    static void BakeMonsterPortraits(GameObject unitPrefab)
    {
        var monsters = new List<SoulMonsterData>();
        foreach (string guid in AssetDatabase.FindAssets("t:SoulMonsterData", new[] { DataPath }))
        {
            var monster = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(AssetDatabase.GUIDToAssetPath(guid));
            if (monster != null && (monster.UseMonsterSprite ? EnemyMonsterCatalog.Current != null : monster.Appearance != null)) monsters.Add(monster);
        }
        monsters.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (monsters.Count == 0) return;
        int rows = (monsters.Count + PortraitColumns - 1) / PortraitColumns;
        var sheet = new Texture2D(PortraitCell * PortraitColumns, PortraitCell * rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        sheet.SetPixels32(new Color32[sheet.width * sheet.height]);
        var metas = new List<SpriteMetaData>();
        var sheets = new Dictionary<Texture2D, Texture2D>(); // a library's sheet, read from its file (the import is not readable)
        for (int i = 0; i < monsters.Count; i++)
        {
            var monster = monsters[i];
            int cellX = i % PortraitColumns * PortraitCell, cellY = (rows - 1 - i / PortraitColumns) * PortraitCell;
            if (monster.UseMonsterSprite)
            {
                var source = EnemyMonsterCatalog.Current.Get(monster.MonsterRace).GetSprite("Idle", "0");
                if (source == null) continue;
                if (!sheets.TryGetValue(source.texture, out var readable))
                {
                    readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    readable.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(source.texture)));
                    sheets[source.texture] = readable;
                }
                var rect = source.rect;
                var pixels = readable.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
                int w = (int)rect.width, h = (int)rect.height, minX = w, minY = h, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (pixels[y * w + x].a > 0) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
                if (maxX < 0) continue;
                int bw = Mathf.Min(maxX - minX + 1, PortraitCell), bh = Mathf.Min(maxY - minY + 1, PortraitCell);
                int px = cellX + (PortraitCell - bw) / 2, py = cellY + (PortraitCell - bh) / 2;
                var cut = new Color[bw * bh];
                for (int y = 0; y < bh; y++)
                    for (int x = 0; x < bw; x++)
                    {
                        var c = pixels[(minY + y) * w + minX + x];
                        cut[y * bw + x] = new Color(c.r * monster.Tint.r, c.g * monster.Tint.g, c.b * monster.Tint.b, c.a);
                    }
                sheet.SetPixels(px, py, bw, bh, cut);
                metas.Add(new SpriteMetaData { name = monster.name, rect = new Rect(px, py, bw, bh), alignment = (int)SpriteAlignment.BottomCenter, pivot = new Vector2(.5f, 0) });
                continue;
            }
            var temp = (GameObject)UnityEngine.Object.Instantiate(unitPrefab);
            var builder = temp.GetComponentInChildren<Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts.CharacterBuilder>(true);
            var look = monster.Appearance;
            builder.Body = look.Body; builder.Head = look.Head; builder.Ears = look.Ears; builder.Eyes = look.Eyes; builder.Hair = look.Hair;
            builder.Armor = look.Armor; builder.Helmet = look.Helmet; builder.Mask = look.Mask; builder.Horns = look.Horns; builder.Cape = look.Cape;
            builder.Weapon = look.Weapon; builder.Shield = look.Shield; builder.Back = look.Back; builder.Firearm = look.Firearm;
            builder.Rebuild();
            var frame = builder.Texture.GetPixels(0, 832, PortraitCell, PortraitCell); // Idle_0
            for (int p = 0; p < frame.Length; p++) frame[p] *= monster.Tint;
            sheet.SetPixels(cellX, cellY, PortraitCell, PortraitCell, frame);
            metas.Add(new SpriteMetaData { name = monster.name, rect = new Rect(cellX, cellY, PortraitCell, PortraitCell), alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(.5f, 8f / PortraitCell) });
            UnityEngine.Object.DestroyImmediate(temp);
        }
        foreach (var readable in sheets.Values) UnityEngine.Object.DestroyImmediate(readable);
        Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts.CharacterBuilder.ClearSharedCache();
        System.IO.File.WriteAllBytes(MonsterSheetPath, sheet.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(sheet);
        AssetDatabase.ImportAsset(MonsterSheetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(MonsterSheetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 16;
#pragma warning disable CS0618 // the sprite rects of a generated sheet: the plain importer list is enough here
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
        importer.SaveAndReimport();
        var sprites = new Dictionary<string, Sprite>();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(MonsterSheetPath)) if (asset is Sprite sprite) sprites[sprite.name] = sprite;
        foreach (var monster in monsters)
        {
            monster.Portrait = sprites.TryGetValue(monster.name, out var portrait) ? portrait : null;
            EditorUtility.SetDirty(monster);
        }
    }

    // Magic is strong and dear: what holds it back is MP, not waiting. Every mage's and priest's spell (not a 기술)
    // costs ManaCostScale × the MP it is written with, hits ManaPowerScale × as hard (damage, heals) with
    // ManaBuffScale × the blessing, and comes back ManaCooldownScale × as soon (long ones — 소생, 기적 — keep their
    // wait): a caster runs out of MP long before its spells come off cooldown.
    public const float ManaCostScale = 1.6f, ManaPowerScale = 1.8f, ManaBuffScale = 1.5f, ManaCooldownScale = .4f, ManaCooldownKept = 40f;
    static void ManaBound()
    {
        var spells = new HashSet<SoulActiveSkillData>();
        bool Caster(string job) => job == "마법사" || job == "성직자" || job == "소환사";
        foreach (string guid in AssetDatabase.FindAssets("t:SoulMercenaryData", new[] { DataPath }))
        {
            var template = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(AssetDatabase.GUIDToAssetPath(guid));
            if (template != null && Caster(template.Job) && template.ActiveSkills != null) foreach (var skill in template.ActiveSkills) if (skill != null) spells.Add(skill);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:SoulActiveSkillData", new[] { DataPath }))
        {
            var skill = AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill != null && Caster(skill.RequiredJob) && !skill.name.EndsWith("Plus")) spells.Add(skill);
        }
        var scaled = new HashSet<SoulValue>(); // a value shared by two spells is scaled once
        void Power(SoulValue value, float k) { if (value != null && scaled.Add(value)) Scale(value, k); }
        foreach (var skill in spells)
        {
            if (skill.Technique || skill.Costs == null) continue;
            bool mana = false;
            var costs = (SoulResourceCost[])skill.Costs.Clone();
            for (int i = 0; i < costs.Length; i++)
                if (costs[i].Resource == SoulResource.Mana) { costs[i].Amount = Mathf.Round(costs[i].Amount * ManaCostScale); mana = true; }
            if (!mana) continue;
            skill.Costs = costs;
            Power(skill.Damage, ManaPowerScale); Power(skill.Heal, ManaPowerScale);
            if (skill.SelfBuffs != null) foreach (var buff in skill.SelfBuffs) Power(buff.Amount, ManaBuffScale);
            if (skill.Imbue != null && skill.Imbue.Kind != SoulStatus.None) Power(skill.Imbue.DamagePerSecond, ManaBuffScale);
            if (skill.Cooldown > 0 && skill.Cooldown <= ManaCooldownKept) skill.Cooldown = Mathf.Max(2f, Mathf.Round(skill.Cooldown * ManaCooldownScale * 10) / 10);
            EditorUtility.SetDirty(skill);
        }
    }

    static SoulActiveSkillData Upgraded(SoulActiveSkillData source, string file, string name, float power = 1.5f)
    {
        var item = Asset(file, (SoulActiveSkillData copy) =>
        {
            EditorUtility.CopySerialized(source, copy);
            copy.SoulId = source.SoulId + "_plus"; copy.SkillName = name; copy.UpgradeTo = null;
            Scale(copy.Damage, power); Scale(copy.Heal, power); Scale(copy.Duration, 1.25f);
            if (copy.SelfBuffs != null) foreach (var buff in copy.SelfBuffs) Scale(buff.Amount, power);
            if (copy.Status != null) { Scale(copy.Status.Chance, 1.2f); copy.Status.Grade = Mathf.Min(3, copy.Status.Grade + 1); }
            if (copy.Imbue != null && copy.Imbue.Kind != SoulStatus.None) { Scale(copy.Imbue.Chance, 1.2f); Scale(copy.Imbue.DamagePerSecond, power); }
            copy.Cooldown *= .8f;
            copy.Description = (source.Description ?? "") + " (서고에서 강화된 상위 스킬)";
        });
        item.name = file;
        return item;
    }

    // Each job's own skills (skill books, 직업 전용), a few for anyone, the 전설's signature skills — and the
    // actions among them made patterns (질주, 발차기, 투척 단검, 연속 할퀴기, 독화살), learned by level and training.
    // A skill never repeats a pattern; a non-mage's one-blow technique (기술) pays MP without being magic.
    static void CreateJobSkills(SoulDungeonData dungeon)
    {
        SoulResourceCost[] Stamina(float amount) => new[] { new SoulResourceCost { Resource = SoulResource.Stamina, Amount = amount } };
        SoulResourceCost[] Mana(float amount) => new[] { new SoulResourceCost { Resource = SoulResource.Mana, Amount = amount } };
        SoulBuffEffect Buff(StatType stat, SoulValue amount) => new SoulBuffEffect { Stat = stat, Amount = amount };
        SoulPatternData LoadPattern(string file) => AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/" + file + ".asset");

        // ── the actions: patterns ──
        var sprint = Pattern("Sprint", "질주", SoulPatternCategory.Support, 4, .3f, 0, SoulDamageSchool.Physical, SoulDamageKind.Impact, Value());
        sprint.SupportTarget = SoulSupportTarget.Self; sprint.UseWhen = SoulUseWhen.InCombat; sprint.Cooldown = 18; sprint.UseBelow = .5f;
        sprint.Buffs = new[] { Buff(StatType.MoveSpeed, Value(.5f)) }; sprint.BuffDuration = Value(4); EditorUtility.SetDirty(sprint);
        var kick = Pattern("Kick", "발차기", SoulPatternCategory.Attack, 4, .6f, 1.1f, SoulDamageSchool.Physical, SoulDamageKind.Impact,
            Value(0, Term(StatType.Attack, .5f), Term(StatType.ImpactPower, .3f)));
        kick.Knockback = 1.1f; kick.Cooldown = 6; kick.Status = Status(SoulStatus.Stun, Value(.3f), Value(.8f)); EditorUtility.SetDirty(kick);
        var knife = Pattern("ThrowingKnife", "투척 단검", SoulPatternCategory.Attack, 3, .7f, 4f, SoulDamageSchool.Physical, SoulDamageKind.Pierce,
            Value(0, Term(StatType.Attack, .45f), Term(StatType.PiercePower, .4f)));
        knife.Cooldown = 4; EditorUtility.SetDirty(knife);
        var rake = Pattern("Rake", "연속 할퀴기", SoulPatternCategory.Attack, 8, 1.4f, 1.1f, SoulDamageSchool.Physical, SoulDamageKind.Slash,
            Value(0, Term(StatType.Attack, .6f), Term(StatType.SlashPower, .4f)));
        rake.WeaponTag = "claw"; rake.Hits = 3; rake.Priority = 1; rake.Cooldown = 5;
        rake.Status = Status(SoulStatus.Bleed, Value(.35f), Value(3), Value(1, Term(StatType.SlashPower, .2f))); EditorUtility.SetDirty(rake);
        var poisonArrow = Pattern("PoisonArrow", "독화살", SoulPatternCategory.Attack, 6, 1f, 5f, SoulDamageSchool.Physical, SoulDamageKind.Pierce,
            Value(0, Term(StatType.Attack, .8f), Term(StatType.PiercePower, .5f)));
        poisonArrow.WeaponTag = "bow"; poisonArrow.Tags = new[] { "bow" }; poisonArrow.Priority = 1; poisonArrow.Cooldown = 7;
        poisonArrow.Status = Status(SoulStatus.Poison, Value(.7f), Value(5), Value(2, Term(StatType.Agility, .2f))); EditorUtility.SetDirty(poisonArrow);
        var pool = new List<SoulPatternData>(dungeon.LevelPatternPool);
        foreach (var pattern in new[] { sprint, kick, knife, rake, poisonArrow }) if (!pool.Contains(pattern)) pool.Add(pattern);
        dungeon.LevelPatternPool = pool.ToArray();

        // skills that were patterns after all (or said again what another skill says): gone
        foreach (string gone in new[] { "SkillBladeStorm", "SkillCounterStance", "SkillShieldBash", "SkillPoisonArrow", "SkillFireball" })
        {
            AssetDatabase.DeleteAsset(DataPath + "/" + gone + ".asset");
            AssetDatabase.DeleteAsset(DataPath + "/" + gone + "Plus.asset");
        }

        void Skill(string file, string id, string name, string job, string description, Action<SoulActiveSkillData> set)
            => Asset(file, (SoulActiveSkillData item) =>
            {
                item.SoulId = id; item.SkillName = name; item.RequiredJob = job; item.Description = description; item.RequiredPattern = null;
                item.Burst = item.Mass = item.OncePerFloor = item.LeapToTarget = item.HealsWounds = false; item.WeightThreshold = 0;
                item.Technique = item.DrainMana = item.Line = item.Cleanse = item.Shortcut = false;
                item.ExecuteBelow = 0; item.Chain = 0; item.Field = SoulFieldKind.None; item.GuardShare = 0; item.HpCost = 0;
                item.Damage = Value(); item.Heal = Value(); item.Chance = Value(); item.Radius = Value(); item.Duration = Value();
                item.Status = new SoulStatusApply(); item.Imbue = new SoulStatusApply(); item.SelfBuffs = new SoulBuffEffect[0];
                item.Knockback = 0; item.ReviveRatio = 0; item.CastTime = 0; item.Range = 0; item.UpgradeTo = null; item.BookTier = 1;
                item.DamageSchool = SoulDamageSchool.Physical; item.DamageKind = SoulDamageKind.Slash;
                item.SpiritLook = null; item.SpiritInterval = 1.2f; item.AtTarget = false; item.Barrier = Value(); item.Pull = 0;
                item.Icon = IconSprite("skill:" + id);
                item.Instant = InstantSkills.Contains(file);
                set(item);
            });
        SoulDamageSchool Magic = SoulDamageSchool.Magic;

        // 검사: one-blow techniques (MP)
        Skill("SkillIssen", "issen", "일섬", "검사", "숨을 고른 뒤 단 한 번에 벱니다. 마력을 쏟는 검사의 비기.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.Technique = true; s.Range = 2.2f; s.CastTime = 1f; s.Cooldown = 20; s.Costs = Mana(16);
            s.Damage = Value(18, Term(StatType.Strength, 1.8f), Term(StatType.Agility, .8f)); s.Knockback = .5f;
        });
        Skill("SkillExecution", "execution", "처형", "검사", "쓰러지기 직전의 적을 확실히 끝냅니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.Technique = true; s.ExecuteBelow = .3f; s.Range = 2f; s.CastTime = .8f; s.Cooldown = 12; s.Costs = Mana(12);
            s.Damage = Value(14, Term(StatType.Strength, 2f));
        });
        // 수호자
        Skill("SkillGuardianCry", "guardian_cry", "수호의 함성", "수호자", "함성으로 동료의 몸과 마음을 단단히 합니다.", s =>
        {
            s.Trigger = SoulTrigger.AllySupport; s.Radius = Value(4); s.CastTime = .6f; s.Cooldown = 25; s.Costs = Stamina(10); s.Duration = Value(8);
            s.SelfBuffs = new[] { Buff(StatType.Armor, Value(6, Term(StatType.Vitality, .5f))), Buff(StatType.WoundResist, Value(.2f)) };
        });
        Skill("SkillIronWall", "iron_wall", "철벽", "수호자", "무너지기 직전, 온몸을 굳혀 버팁니다. 적의 시선도 끌어옵니다.", s =>
        {
            s.Trigger = SoulTrigger.LowHealth; s.Range = 3f; s.CastTime = .4f; s.Cooldown = 30; s.Costs = Stamina(8); s.Duration = Value(6);
            s.SelfBuffs = new[] { Buff(StatType.Armor, Value(10, Term(StatType.Vitality, 1f))), Buff(StatType.KnockbackResist, Value(.6f)),
                Buff(StatType.DownResist, Value(.4f)), Buff(StatType.Threat, Value(1.5f)) };
        });
        // 투사
        Skill("SkillRoar", "roar", "포효", "투사", "짐승의 포효로 주변의 적을 겁먹게 합니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.Burst = true; s.Range = 2.5f; s.Radius = Value(3.5f); s.CastTime = .8f; s.Cooldown = 16; s.Costs = Stamina(8);
            var fear = Status(SoulStatus.Fear, Value(.6f), Value(3)); fear.GradeBonus = Value(0, Term(StatType.Will, .05f)); s.Status = fear;
        });
        Skill("SkillFrenzy", "frenzy", "광폭화", "투사", "쓰러지기 직전, 피에 굶주린 광기에 몸을 맡깁니다.", s =>
        {
            s.Trigger = SoulTrigger.LowHealth; s.Range = 3f; s.CastTime = .3f; s.Cooldown = 30; s.Costs = Stamina(5); s.Duration = Value(8);
            s.SelfBuffs = new[] { Buff(StatType.ActionSpeed, Value(.25f)), Buff(StatType.LifeSteal, Value(.08f)), Buff(StatType.Attack, Value(3, Term(StatType.Strength, .3f))) };
        });
        // 길잡이
        Skill("SkillSmokeBomb", "smoke_bomb", "연막탄", "길잡이", "위기에 연막을 터뜨려 적을 혼란에 빠뜨리고 몸을 뺍니다.", s =>
        {
            s.Trigger = SoulTrigger.LowHealth; s.Burst = true; s.Range = 3f; s.Radius = Value(3); s.CastTime = .4f; s.Cooldown = 25; s.Costs = Stamina(5); s.Duration = Value(4);
            s.Status = Status(SoulStatus.Confuse, Value(.6f), Value(3));
            s.SelfBuffs = new[] { Buff(StatType.Evasion, Value(.15f)), Buff(StatType.MoveSpeed, Value(.3f)) };
        });
        Skill("SkillTrap", "trap", "덫 설치", "길잡이", "적을 발견하면 발밑에 덫을 놓습니다. 밟은 적은 묶이고 다칩니다.", s =>
        {
            s.Trigger = SoulTrigger.Encounter; s.Field = SoulFieldKind.Trap; s.Radius = Value(1.3f); s.Duration = Value(20); s.CastTime = .6f; s.Cooldown = 20; s.Costs = Stamina(6);
            s.DamageKind = SoulDamageKind.Pierce; s.Damage = Value(6, Term(StatType.Agility, .6f));
            s.Status = Status(SoulStatus.Stun, Value(.7f), Value(1.5f));
        });
        Skill("SkillPiercingShot", "piercing_shot", "관통 사격", "길잡이", "마력을 실은 화살이 일직선 위의 적을 모두 꿰뚫습니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.Line = true; s.Technique = true; s.RequiredPattern = LoadPattern("Bow"); s.Range = 6f; s.CastTime = 1f; s.Cooldown = 14; s.Costs = Mana(12);
            s.DamageKind = SoulDamageKind.Pierce; s.Damage = Value(12, Term(StatType.Agility, 1.2f), Term(StatType.PiercePower, .6f));
        });
        // 마법사
        Skill("SkillFrostPrison", "frost_prison", "빙결 감옥", "마법사", "대상을 얼음 속에 가둡니다.", s =>
        {
            s.Trigger = SoulTrigger.Cast; s.Range = 5f; s.Radius = Value(.8f); s.CastTime = 1.4f; s.Cooldown = 14; s.Costs = Mana(20);
            s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Cold; s.Status = Status(SoulStatus.Freeze, Value(.8f), Value(2.5f));
        });
        Skill("SkillBlizzard", "blizzard", "눈보라", "마법사", "넓은 곳에 눈보라를 일으켜 얼리고 느리게 합니다.", s =>
        {
            s.Trigger = SoulTrigger.Cast; s.Range = 5f; s.Radius = Value(2.8f); s.CastTime = 2f; s.Cooldown = 12; s.Costs = Mana(32);
            s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Cold; s.Damage = Value(14, Term(StatType.Magic, 1.6f));
            var slow = Status(SoulStatus.Slow, Value(.8f), Value(4)); slow.Grade = 2; s.Status = slow;
        });
        Skill("SkillConfusion", "confusion", "혼란의 주문", "마법사", "적의 정신을 흐려 누가 적인지 모르게 합니다.", s =>
        {
            s.Trigger = SoulTrigger.Cast; s.Range = 5f; s.Radius = Value(1.2f); s.CastTime = 1.4f; s.Cooldown = 14; s.Costs = Mana(18);
            s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Arcane; s.Status = Status(SoulStatus.Confuse, Value(.7f), Value(4));
        });
        Skill("SkillPetrifyGaze", "petrify_gaze", "석화의 시선", "마법사", "눈을 마주친 적을 돌로 굳힙니다.", s =>
        {
            s.Trigger = SoulTrigger.Cast; s.Range = 4.5f; s.Radius = Value(.8f); s.CastTime = 1.6f; s.Cooldown = 20; s.Costs = Mana(24);
            s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Stone; s.Status = Status(SoulStatus.Petrify, Value(.45f), Value(3));
        });
        Skill("SkillArcaneBurst", "arcane_burst", "비전 폭발", "마법사", "남은 마력을 모두 터뜨립니다. 많이 남았을수록 강합니다.", s =>
        {
            s.Trigger = SoulTrigger.Cast; s.DrainMana = true; s.Range = 4.5f; s.Radius = Value(1.6f); s.CastTime = 1.8f; s.Cooldown = 20; s.Costs = Mana(20);
            s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Arcane; s.Damage = Value(12, Term(StatType.Magic, 1.2f));
        });
        Skill("SkillChainLightning", "chain_lightning", "연쇄 번개", "마법사", "번개가 적에서 적으로 튕겨 나갑니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.Chain = 3; s.Range = 5f; s.CastTime = 1.2f; s.Cooldown = 8; s.Costs = Mana(22);
            s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Lightning; s.Damage = Value(14, Term(StatType.Magic, 1.8f));
            s.Status = Status(SoulStatus.Stun, Value(.15f), Value(.5f));
        });
        Skill("SkillManaShield", "mana_shield", "마나 보호막", "마법사", "싸움이 시작되면 마력의 막을 둘러 받는 피해 일부를 MP로 막습니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.Range = 6f; s.CastTime = .6f; s.Cooldown = 30; s.Costs = Mana(10); s.Duration = Value(12);
            s.SelfBuffs = new[] { Buff(StatType.ManaShield, Value(.5f)) };
        });
        // 성직자
        Skill("SkillPrayer", "prayer", "축복의 기도", "성직자", "기도로 주변 동료를 치유하고 상처가 아물게 합니다.", s =>
        {
            s.Trigger = SoulTrigger.AllySupport; s.Radius = Value(5); s.CastTime = 1.2f; s.Cooldown = 28; s.Costs = Mana(16); s.Duration = Value(10);
            s.Heal = Value(6, Term(StatType.Magic, .6f));
            s.SelfBuffs = new[] { Buff(StatType.HpRegen, Value(1.5f, Term(StatType.Magic, .12f))) };
        });
        // the priest's blessings: protection, life and a consecrated blow — not the mage's elements and speed
        Skill("SkillWardPrayer", "ward_prayer", "수호의 기도", "성직자", "기도의 막으로 주변 동료를 감싸 몸과 마음을 지킵니다.", s =>
        {
            s.Trigger = SoulTrigger.AllySupport; s.Radius = Value(5); s.CastTime = 1f; s.Cooldown = 26; s.Costs = Mana(12); s.Duration = Value(12);
            s.SelfBuffs = new[] { Buff(StatType.Armor, Value(4, Term(StatType.Will, .5f))), Buff(StatType.MagicResist, Value(.15f)) };
        });
        Skill("SkillLifeBlessing", "life_blessing", "생명의 축복", "성직자", "동료의 몸에 생명력을 불어넣어 상처가 저절로 아물게 합니다.", s =>
        {
            s.Trigger = SoulTrigger.AllySupport; s.Radius = Value(5); s.CastTime = 1f; s.Cooldown = 30; s.Costs = Mana(12); s.Duration = Value(15);
            s.SelfBuffs = new[] { Buff(StatType.HpRegen, Value(2, Term(StatType.Will, .2f))), Buff(StatType.WoundResist, Value(.15f)) };
        });
        Skill("SkillConsecrate", "consecrate", "축성", "성직자", "동료의 무기를 축성합니다. 망자에게 더 깊이 박히고 두려움을 잊게 합니다.", s =>
        {
            s.Trigger = SoulTrigger.AllySupport; s.Radius = Value(5); s.CastTime = .8f; s.Cooldown = 25; s.Costs = Mana(10); s.Duration = Value(12);
            s.SelfBuffs = new[] { Buff(StatType.HolyBane, Value(.3f)), Buff(StatType.Accuracy, Value(5)), Buff(StatType.FearResist, Value(.3f)) };
        });
        Skill("SkillJudgment", "judgment", "심판의 빛", "성직자", "하늘의 빛으로 적을 태우고 겁먹게 합니다.", s =>
        {
            s.Trigger = SoulTrigger.Cast; s.Range = 5f; s.Radius = Value(1.4f); s.CastTime = 1.4f; s.Cooldown = 10; s.Costs = Mana(20);
            s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Arcane; s.Damage = Value(12, Term(StatType.Magic, 1.4f));
            s.Status = Status(SoulStatus.Fear, Value(.5f), Value(3));
        });
        Skill("SkillPurify", "purify", "정화", "성직자", "동료에게 걸린 독·출혈·저주를 모두 걷어냅니다.", s =>
        {
            s.Trigger = SoulTrigger.AllyHurt; s.Cleanse = true; s.Range = 5f; s.CastTime = .8f; s.Cooldown = 14; s.Costs = Mana(10);
            s.Heal = Value(4, Term(StatType.Magic, .3f));
        });
        Skill("SkillSacrifice", "sacrifice", "희생", "성직자", "자신의 생명을 덜어 쓰러져 가는 동료를 살립니다.", s =>
        {
            s.Trigger = SoulTrigger.AllyHurt; s.HpCost = .3f; s.Range = 4f; s.CastTime = .8f; s.Cooldown = 25; s.Costs = Mana(6); s.HealsWounds = true;
            s.Heal = Value(20, Term(StatType.Magic, 1.2f), Term(StatType.Vitality, .8f));
        });
        Skill("SkillSanctuary", "sanctuary", "성역", "성직자", "발밑에 성역을 펼쳐 안에 있는 동료를 계속 치유합니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.Field = SoulFieldKind.Sanctuary; s.Range = 6f; s.Radius = Value(2.5f); s.Duration = Value(10); s.CastTime = 1.4f; s.Cooldown = 30; s.Costs = Mana(24);
            s.Heal = Value(2, Term(StatType.Magic, .25f));
        });
        // 성직자: a shield, a vow of regeneration, a rain of blessing
        Skill("SkillHolyShield", "holy_shield", "신성한 방패", "성직자", "공격받는 동료를 빛의 방패로 감쌉니다. 방패가 먼저 피해를 받아냅니다.", s =>
        {
            s.Trigger = SoulTrigger.AllyHurt; s.Range = 5f; s.CastTime = .6f; s.Cooldown = 10; s.Costs = Mana(14); s.Duration = Value(8);
            s.Barrier = Value(15, Term(StatType.Magic, 2.5f), Term(StatType.Will, 1f));
        });
        Skill("SkillRenewalVow", "renewal_vow", "재생의 서약", "성직자", "가장 다친 동료의 몸에 서약을 새깁니다. 한동안 상처가 스스로 아뭅니다.", s =>
        {
            s.Trigger = SoulTrigger.AllyHurt; s.Range = 5f; s.CastTime = .8f; s.Cooldown = 12; s.Costs = Mana(12); s.Duration = Value(10);
            s.SelfBuffs = new[] { Buff(StatType.HpRegen, Value(2, Term(StatType.Magic, .25f), Term(StatType.Will, .1f))) };
        });
        Skill("SkillBlessedRain", "blessed_rain", "축복의 비", "성직자", "넓게 내리는 축복의 비. 곁의 모두가 조금씩, 여러 번 치유됩니다.", s =>
        {
            s.Trigger = SoulTrigger.AllySupport; s.Radius = Value(6); s.CastTime = 1.2f; s.Cooldown = 20; s.Costs = Mana(18); s.Duration = Value(6);
            s.Heal = Value(3, Term(StatType.Magic, .3f));
            s.SelfBuffs = new[] { Buff(StatType.HpRegen, Value(1.5f, Term(StatType.Magic, .12f))) };
        });
        // 마법사: a wall of fire, a lance of ice, a gravity well, a thunderbolt
        Skill("SkillFireWall", "fire_wall", "화염 벽", "마법사", "앞으로 일직선의 불길을 세웁니다. 지나가는 적은 불에 탑니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.Field = SoulFieldKind.Hazard; s.Line = true; s.Range = 5f; s.Radius = Value(.8f); s.Duration = Value(6);
            s.CastTime = 1f; s.Cooldown = 14; s.Costs = Mana(20);
            s.DamageSchool = SoulDamageSchool.Magic; s.DamageKind = SoulDamageKind.Fire; s.Damage = Value(2, Term(StatType.Magic, .3f));
            s.Status = Status(SoulStatus.Burn, Value(.4f), Value(3), Value(1, Term(StatType.Magic, .1f)));
        });
        Skill("SkillIceLance", "ice_lance", "얼음 창", "마법사", "얼음 창이 일직선 위의 적을 모두 꿰뚫고 얼립니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.Line = true; s.Range = 6f; s.CastTime = .9f; s.Cooldown = 10; s.Costs = Mana(18);
            s.DamageSchool = SoulDamageSchool.Magic; s.DamageKind = SoulDamageKind.Cold; s.Damage = Value(8, Term(StatType.Magic, 1.1f));
            s.Status = Status(SoulStatus.Chill, Value(.6f), Value(3));
        });
        Skill("SkillGravityWell", "gravity_well", "중력장", "마법사", "한 점에 중력을 모읍니다. 주변의 적이 한가운데로 끌려와 느려집니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.Burst = true; s.AtTarget = true; s.Range = 6f; s.Radius = Value(2.8f); s.Pull = 1.8f;
            s.CastTime = 1.2f; s.Cooldown = 16; s.Costs = Mana(22);
            s.DamageSchool = SoulDamageSchool.Magic; s.DamageKind = SoulDamageKind.Arcane; s.Damage = Value(2, Term(StatType.Magic, .4f));
            var heavy = Status(SoulStatus.Slow, Value(.8f), Value(4)); heavy.Grade = 2; s.Status = heavy;
        });
        Skill("SkillThunderStrike", "thunder_strike", "낙뢰", "마법사", "긴 주문 끝에 하늘에서 벼락이 내리꽂힙니다. 맞은 적은 잠시 움직이지 못합니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.Burst = true; s.AtTarget = true; s.Range = 7f; s.Radius = Value(1.3f); s.CastTime = 2.4f; s.Cooldown = 18; s.Costs = Mana(30);
            s.DamageSchool = SoulDamageSchool.Magic; s.DamageKind = SoulDamageKind.Lightning; s.Damage = Value(14, Term(StatType.Magic, 1.8f));
            s.Status = Status(SoulStatus.Stun, Value(.7f), Value(1.5f));
        });
        // 궁수 — the bow's own (a 궁수 shoots them; they need the bow pattern)
        Skill("SkillQuickShot", "quick_shot", "속사", "궁수", "시위를 당기자마자 놓습니다. 가볍지만 쉼 없이 날아갑니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.RequiredPattern = LoadPattern("Bow"); s.Range = 5.5f; s.CastTime = .3f; s.Cooldown = 6; s.Costs = Stamina(8);
            s.DamageKind = SoulDamageKind.Pierce; s.Damage = Value(3, Term(StatType.Agility, .7f), Term(StatType.PiercePower, .7f));
        });
        Skill("SkillArrowRain", "arrow_rain", "화살비", "궁수", "하늘로 쏘아 올린 화살이 한 곳에 비처럼 쏟아집니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.RequiredPattern = LoadPattern("Bow"); s.Burst = true; s.AtTarget = true; s.Range = 6f; s.Radius = Value(2.2f);
            s.CastTime = 1f; s.Cooldown = 16; s.Costs = Stamina(16);
            s.DamageKind = SoulDamageKind.Pierce; s.Damage = Value(3, Term(StatType.Agility, .5f), Term(StatType.PiercePower, .5f));
        });
        Skill("SkillAimedShot", "aimed_shot", "조준 사격", "궁수", "숨을 참고 오래 겨눈 한 발. 급소를 꿰뚫고 몸을 밀어냅니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.RequiredPattern = LoadPattern("Bow"); s.Range = 6.5f; s.CastTime = 1.6f; s.Cooldown = 14; s.Costs = Stamina(12); s.Knockback = .4f;
            s.DamageKind = SoulDamageKind.Pierce; s.Damage = Value(10, Term(StatType.Agility, 1.4f), Term(StatType.PiercePower, 1.2f));
        });
        Skill("SkillPinShot", "pin_shot", "발 묶기", "궁수", "다리를 노린 화살. 맞은 적은 한동안 제대로 걷지 못합니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.RequiredPattern = LoadPattern("Bow"); s.Range = 5.5f; s.CastTime = .6f; s.Cooldown = 12; s.Costs = Stamina(8);
            s.DamageKind = SoulDamageKind.Pierce; s.Damage = Value(2, Term(StatType.Agility, .4f));
            var pin = Status(SoulStatus.Slow, Value(.85f), Value(4)); pin.Grade = 2; s.Status = pin;
        });
        // 소환사 — spirits at its side
        void Summon(string file, string id, string name, string look, string description, float interval, Action<SoulActiveSkillData> more)
            => Skill(file, id, name, "소환사", description, s =>
            {
                s.Trigger = SoulTrigger.InRange; s.Field = SoulFieldKind.Spirit; s.SpiritLook = look; s.SpiritInterval = interval;
                s.Range = 6f; s.Radius = Value(5f); s.Duration = Value(18); s.CastTime = 1f; s.Cooldown = 8; s.Costs = Mana(16);
                s.DamageSchool = SoulDamageSchool.Magic;
                more(s);
            });
        Summon("SkillSummonFire", "summon_fire", "불의 정령", "fire", "불의 정령을 곁에 부릅니다. 가까운 적에게 불덩이를 쏘아 태웁니다.", 1.4f, s =>
        {
            s.DamageKind = SoulDamageKind.Fire; s.Damage = Value(2, Term(StatType.Magic, .35f));
            s.Status = Status(SoulStatus.Burn, Value(.25f), Value(3), Value(1, Term(StatType.Magic, .1f)));
        });
        Summon("SkillSummonFrost", "summon_frost", "얼음 정령", "frost", "얼음 정령을 곁에 부릅니다. 서리 조각으로 적의 발을 얼립니다.", 1.6f, s =>
        {
            s.DamageKind = SoulDamageKind.Cold; s.Damage = Value(2, Term(StatType.Magic, .3f));
            s.Status = Status(SoulStatus.Chill, Value(.35f), Value(2.5f));
        });
        Summon("SkillSummonWind", "summon_wind", "바람 늑대", "wind", "바람의 늑대를 곁에 부릅니다. 약하지만 쉴 새 없이 물어뜯습니다.", .8f, s =>
        {
            s.DamageKind = SoulDamageKind.Wind; s.Damage = Value(1, Term(StatType.Magic, .2f));
            s.Status = Status(SoulStatus.Bleed, Value(.2f), Value(3), Value(1, Term(StatType.Magic, .05f)));
        });
        Summon("SkillSummonLight", "summon_light", "빛의 정령", "light", "빛의 정령을 곁에 부릅니다. 곁의 가장 다친 동료를 조금씩 치유합니다.", 2f, s =>
        {
            s.DamageKind = SoulDamageKind.Arcane; s.Heal = Value(3, Term(StatType.Magic, .4f));
        });
        // the templates' own (someone not from the roster)
        var archerTemplate = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryArcher.asset");
        var summonerTemplate = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenarySummoner.asset");
        archerTemplate.ActiveSkills = new[] { AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/SkillQuickShot.asset") };
        summonerTemplate.ActiveSkills = new[] { AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/SkillSummonFire.asset") };
        EditorUtility.SetDirty(archerTemplate); EditorUtility.SetDirty(summonerTemplate);

        // 누구나
        Skill("SkillWarCry", "war_cry", "전투 함성", null, "우렁찬 함성으로 주변 동료의 공격에 힘을 싣습니다.", s =>
        {
            s.Trigger = SoulTrigger.AllySupport; s.Radius = Value(4); s.CastTime = .6f; s.Cooldown = 25; s.Costs = Stamina(8); s.Duration = Value(8);
            s.SelfBuffs = new[] { Buff(StatType.Attack, Value(3, Term(StatType.Strength, .2f))) };
        });
        Skill("SkillBattleFocus", "battle_focus", "전장의 집중", null, "싸움에 온 신경을 모아 더 정확하고 날카롭게 칩니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.Range = 3f; s.CastTime = .4f; s.Cooldown = 25; s.Costs = Stamina(6); s.Duration = Value(8);
            s.SelfBuffs = new[] { Buff(StatType.Accuracy, Value(.1f)), Buff(StatType.CritChance, Value(.1f)) };
        });

        // 전설 고유
        Skill("SkillHeavenBlade", "heaven_blade", "천검", "검사", "레온의 비기. 마력을 실어 한순간에 주변의 모든 적을 베고 깊은 상처를 남깁니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.Burst = true; s.Technique = true; s.Range = 2.2f; s.Radius = Value(2.8f); s.CastTime = 1f; s.Cooldown = 14; s.Costs = Mana(18);
            s.Damage = Value(10, Term(StatType.Strength, 1f), Term(StatType.Agility, .6f)); s.Knockback = .4f;
            var bleed = Status(SoulStatus.Bleed, Value(.8f), Value(5), Value(3, Term(StatType.Strength, .2f))); bleed.Grade = 2; s.Status = bleed;
        });
        Skill("SkillBastion", "bastion", "불괴의 성채", "수호자", "바론의 비기. 동료를 성벽처럼 단단히 하고, 그들이 받는 피해의 40%를 대신 받습니다.", s =>
        {
            s.Trigger = SoulTrigger.AllySupport; s.GuardShare = .4f; s.Radius = Value(5); s.CastTime = .8f; s.Cooldown = 30; s.Costs = Stamina(12); s.Duration = Value(8);
            s.SelfBuffs = new[] { Buff(StatType.Armor, Value(12, Term(StatType.Vitality, .8f))), Buff(StatType.WoundResist, Value(.3f)), Buff(StatType.KnockbackResist, Value(.5f)) };
        });
        Skill("SkillMeteor", "meteor", "운석", "마법사", "이솔데의 비기. 긴 주문 끝에 하늘에서 불타는 바위를 떨어뜨립니다.", s =>
        {
            s.Trigger = SoulTrigger.Cast; s.Range = 6f; s.Radius = Value(3); s.CastTime = 2.6f; s.Cooldown = 16; s.Costs = Mana(48);
            s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Fire; s.Damage = Value(35, Term(StatType.Magic, 3.4f)); s.Knockback = 1f;
            s.Status = Status(SoulStatus.Stun, Value(.6f), Value(1.5f));
        });
        Skill("SkillMiracle", "miracle", "기적", "성직자", "엘레나의 비기. 쓰러진 동료 모두를 일으키고 모두의 상처를 어루만집니다.", s =>
        {
            s.Trigger = SoulTrigger.AllyFallen; s.Mass = true; s.OncePerFloor = true; s.Range = 6f; s.CastTime = 2.4f; s.Cooldown = 60; s.Costs = Mana(30);
            s.ReviveRatio = .5f; s.Heal = Value(20, Term(StatType.Magic, 1.5f));
        });
        Skill("SkillPackCall", "pack_call", "무리의 부름", "투사", "라그나의 비기. 늑대 영혼들을 불러 한동안 주변의 적을 쉴 새 없이 물어뜯게 합니다.", s =>
        {
            s.Trigger = SoulTrigger.InRange; s.Field = SoulFieldKind.Aura; s.Range = 2.5f; s.Radius = Value(2.2f); s.Duration = Value(10); s.CastTime = .8f; s.Cooldown = 30; s.Costs = Stamina(12);
            s.Damage = Value(3, Term(StatType.Strength, .35f)); s.Status = Status(SoulStatus.Bleed, Value(.3f), Value(3), Value(1, Term(StatType.Strength, .1f)));
        });
        Skill("SkillShortcut", "shortcut", "지름길", "길잡이", "에단의 비기. 물러날 때, 아는 길로 파티를 곧장 출구 곁으로 데려갑니다.", s =>
        {
            s.Trigger = SoulTrigger.Always; s.Shortcut = true; s.OncePerFloor = true; s.CastTime = .5f; s.Cooldown = 0; s.Costs = Stamina(10);
        });
    }

    // Monster souls' own skills (영혼 스킬): each soul brings a skill of its monster's kind — the bosses' stronger in
    // every way (damage, reach, status grade). A refined soul (three fused) brings the stronger form (… (정제)).
    // Summons, swallowing and hiding are drawn as what they do on the field: spirits biting around (an aura), a long
    // stun with poison, a moment unseen with a sure next blow.
    static void CreateSoulSkills()
    {
        SoulResourceCost[] Stamina(float amount) => new[] { new SoulResourceCost { Resource = SoulResource.Stamina, Amount = amount } };
        SoulResourceCost[] Mana(float amount) => new[] { new SoulResourceCost { Resource = SoulResource.Mana, Amount = amount } };
        SoulBuffEffect Buff(StatType stat, SoulValue amount) => new SoulBuffEffect { Stat = stat, Amount = amount };
        var made = new Dictionary<string, SoulActiveSkillData>();
        void Skill(string file, string icon, string name, string description, Action<SoulActiveSkillData> set)
            => made[file] = Asset("SoulSkill" + file, (SoulActiveSkillData item) =>
            {
                item.SoulId = "soul_" + icon; item.SkillName = name; item.RequiredJob = null; item.Description = description; item.RequiredPattern = null;
                item.Burst = item.Mass = item.OncePerFloor = item.LeapToTarget = item.HealsWounds = false; item.WeightThreshold = 0;
                item.Technique = item.DrainMana = item.Line = item.Cleanse = item.Shortcut = false;
                item.ExecuteBelow = 0; item.Chain = 0; item.Field = SoulFieldKind.None; item.GuardShare = 0; item.HpCost = 0;
                item.AtTarget = item.Dispel = item.Behind = false; item.Width = 0; item.Pull = 0; item.Drain = 0; item.Sap = 0; item.Steal = 0; item.Retreat = 0;
                item.Damage = Value(); item.Heal = Value(); item.Chance = Value(); item.Radius = Value(); item.Duration = Value();
                item.Status = new SoulStatusApply(); item.Extra = new SoulStatusApply(); item.Imbue = new SoulStatusApply(); item.SelfBuffs = new SoulBuffEffect[0];
                item.Knockback = 0; item.ReviveRatio = 0; item.CastTime = 0; item.Range = 0; item.UpgradeTo = null; item.BookTier = 1;
                item.DamageSchool = SoulDamageSchool.Physical; item.DamageKind = SoulDamageKind.Impact; item.Trigger = SoulTrigger.InRange;
                item.Icon = IconSprite("skill:soul_" + icon);
                item.Instant = InstantSkills.Contains("SoulSkill" + file);
                set(item);
            });
        const SoulDamageSchool Phys = SoulDamageSchool.Physical, Magic = SoulDamageSchool.Magic, Other = SoulDamageSchool.Other;
        SoulStatusApply St(SoulStatus kind, float chance, float duration, SoulValue dps = null, int grade = 1)
        { var s = Status(kind, Value(chance), Value(duration), dps); s.Grade = grade; return s; }

        // ── 1층대 ──
        Skill("MudRoll", "mud_roll", "진흙 뒹굴기", "쓰러질 듯하면 진흙을 뒤집어써 가죽을 단단히 하고 상처를 다스립니다.", s =>
        { s.Trigger = SoulTrigger.LowHealth; s.Range = 3; s.Cooldown = 30; s.Costs = Stamina(6); s.Duration = Value(8); s.CastTime = .5f;
          s.SelfBuffs = new[] { Buff(StatType.Armor, Value(8, Term(StatType.Vitality, .6f))), Buff(StatType.HpRegen, Value(2)) }; });
        Skill("WolfHowl", "wolf_howl", "늑대 울음", "무리를 부르는 울음. 주변 동료의 발과 손이 빨라집니다.", s =>
        { s.Trigger = SoulTrigger.AllySupport; s.Radius = Value(5); s.Cooldown = 25; s.Costs = Stamina(8); s.Duration = Value(8); s.CastTime = .6f;
          s.SelfBuffs = new[] { Buff(StatType.MoveSpeed, Value(.2f)), Buff(StatType.ActionSpeed, Value(.1f)) }; });
        Skill("ShadowPounce", "shadow_pounce", "그림자 습격", "그림자처럼 대상의 등 뒤로 뛰어들어 벱니다.", s =>
        { s.LeapToTarget = true; s.Behind = true; s.Range = 4; s.Radius = Value(.9f); s.Cooldown = 12; s.Costs = Stamina(9);
          s.DamageKind = SoulDamageKind.Slash; s.Damage = Value(6, Term(StatType.Agility, 1f)); });
        Skill("Statue", "statue", "석상화", "몸을 돌로 굳혀 거의 모든 피해를 버팁니다. 그동안은 움직이지 못합니다.", s =>
        { s.Trigger = SoulTrigger.LowHealth; s.Range = 3; s.Cooldown = 30; s.Costs = Stamina(5); s.Duration = Value(5); s.CastTime = .4f;
          s.SelfBuffs = new[] { Buff(StatType.Armor, Value(30, Term(StatType.Vitality, 1.5f))), Buff(StatType.MoveSpeed, Value(-.6f)), Buff(StatType.KnockbackResist, Value(1)) }; });
        Skill("ArrowRain", "arrow_rain", "화살비", "대상 지점에 화살을 퍼붓습니다.", s =>
        { s.Burst = true; s.AtTarget = true; s.Range = 6; s.Radius = Value(2); s.Cooldown = 14; s.Costs = Stamina(10); s.CastTime = 1f;
          s.DamageKind = SoulDamageKind.Pierce; s.Damage = Value(4, Term(StatType.Agility, .6f)); });
        Skill("Dynamite", "dynamite", "폭약 설치", "적을 보면 발밑에 폭약을 묻습니다. 밟은 적은 날아갑니다.", s =>
        { s.Trigger = SoulTrigger.Encounter; s.Field = SoulFieldKind.Trap; s.Radius = Value(1.6f); s.Duration = Value(20); s.Cooldown = 20; s.Costs = Stamina(8); s.CastTime = .6f;
          s.Damage = Value(10, Term(StatType.Strength, .6f)); s.Knockback = 1.2f; });
        Skill("Pickpocket", "pickpocket", "소매치기", "치는 김에 주머니를 텁니다. 맞힐 때마다 금화를 챙깁니다.", s =>
        { s.Steal = 5; s.Range = 2; s.Cooldown = 6; s.Costs = Stamina(4); s.CastTime = .5f; s.DamageKind = SoulDamageKind.Slash; s.Damage = Value(2, Term(StatType.Agility, .3f)); });
        Skill("CurseDoll", "curse_doll", "저주 인형", "인형에 바늘을 꽂아 대상을 약하게 하고 겁에 질리게 합니다.", s =>
        { s.Range = 5; s.Cooldown = 12; s.Costs = Mana(16); s.CastTime = 1.4f; s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Arcane;
          s.Status = St(SoulStatus.Weaken, .9f, 6, null, 2); s.Extra = St(SoulStatus.Fear, .5f, 3); });
        Skill("AcidPool", "acid_pool", "독액 웅덩이", "독액을 흘려 웅덩이를 만듭니다. 안에 선 적은 중독됩니다.", s =>
        { s.Field = SoulFieldKind.Hazard; s.Range = 3; s.Radius = Value(1.8f); s.Duration = Value(8); s.Cooldown = 18; s.Costs = Stamina(7); s.CastTime = .6f;
          s.DamageSchool = Other; s.DamageKind = SoulDamageKind.Poison; s.Damage = Value(1, Term(StatType.Vitality, .15f));
          s.Status = St(SoulStatus.Poison, .6f, 4, Value(1.5f)); });
        Skill("FlameBurst", "flame_burst", "불꽃 폭발", "위기에 몸 안의 불을 터뜨려 주변을 태웁니다.", s =>
        { s.Trigger = SoulTrigger.LowHealth; s.Burst = true; s.Range = 3; s.Radius = Value(2.5f); s.Cooldown = 30; s.Costs = Stamina(6); s.CastTime = .4f;
          s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Fire; s.Damage = Value(8, Term(StatType.Vitality, .6f), Term(StatType.Magic, .6f));
          s.Status = St(SoulStatus.Burn, .6f, 4, Value(2, Term(StatType.Magic, .2f))); });
        Skill("Split", "split", "분열", "몸에서 불씨 슬라임들이 떨어져 나와 한동안 주변 적을 태웁니다.", s =>
        { s.Field = SoulFieldKind.Aura; s.Range = 3; s.Radius = Value(2); s.Duration = Value(10); s.Cooldown = 30; s.Costs = Stamina(10); s.CastTime = .6f;
          s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Fire; s.Damage = Value(2, Term(StatType.Magic, .25f)); s.Status = St(SoulStatus.Burn, .3f, 3, Value(1)); });
        Skill("IceFloor", "ice_floor", "빙판", "주변 바닥을 얼려 적의 발을 묶습니다.", s =>
        { s.Field = SoulFieldKind.Hazard; s.Range = 3; s.Radius = Value(2.2f); s.Duration = Value(8); s.Cooldown = 18; s.Costs = Stamina(7); s.CastTime = .6f;
          s.Status = St(SoulStatus.Slow, .8f, 2, null, 2); });
        Skill("PlagueCloud", "plague_cloud", "역병 구름", "역병을 내뿜어 주변 적을 중독시키고 쇠약하게 합니다.", s =>
        { s.Burst = true; s.Range = 2.5f; s.Radius = Value(3); s.Cooldown = 16; s.Costs = Stamina(8); s.CastTime = .8f;
          s.Status = St(SoulStatus.Poison, .8f, 6, Value(2, Term(StatType.Vitality, .2f))); s.Extra = St(SoulStatus.Weaken, .5f, 5); });
        Skill("BloodSuck", "blood_suck", "체액 흡수", "주변 적의 피를 빨아들여 제 것으로 만듭니다.", s =>
        { s.Burst = true; s.Drain = .6f; s.Range = 2.5f; s.Radius = Value(2.5f); s.Cooldown = 14; s.Costs = Stamina(8); s.CastTime = .8f;
          s.DamageSchool = Other; s.DamageKind = SoulDamageKind.Bleed; s.Damage = Value(4, Term(StatType.Vitality, .4f)); });

        // ── 늪·호수 ──
        Skill("TongueLash", "tongue_lash", "혀 휘감기", "긴 혀로 멀리 있는 적을 낚아채 끌어옵니다.", s =>
        { s.Pull = 3.5f; s.Range = 5; s.Cooldown = 10; s.Costs = Stamina(6); s.CastTime = .5f; s.Damage = Value(2, Term(StatType.Agility, .3f)); });
        Skill("SlimeSpit", "slime_spit", "점액 뱉기", "끈적한 점액을 뱉어 적을 느리게 합니다.", s =>
        { s.Range = 4.5f; s.Cooldown = 8; s.Costs = Stamina(4); s.CastTime = .5f; s.DamageSchool = Other; s.DamageKind = SoulDamageKind.Poison;
          s.Damage = Value(1, Term(StatType.Agility, .2f)); s.Status = St(SoulStatus.Slow, .8f, 3); });
        Skill("TrollRegen", "troll_regen", "재생의 포효", "트롤의 피가 끓어올라 상처가 눈에 보이게 아뭅니다.", s =>
        { s.Trigger = SoulTrigger.LowHealth; s.Range = 3; s.Cooldown = 35; s.Costs = Stamina(6); s.Duration = Value(10); s.CastTime = .5f;
          s.SelfBuffs = new[] { Buff(StatType.HpRegen, Value(5, Term(StatType.Vitality, .4f))) }; });
        Skill("BoulderThrow", "boulder_throw", "바위 던지기", "바위를 집어 던져 적을 쓰러뜨립니다.", s =>
        { s.Range = 5; s.Cooldown = 12; s.Costs = Stamina(10); s.CastTime = 1f; s.Damage = Value(10, Term(StatType.Strength, .8f)); s.Knockback = .8f;
          s.Status = St(SoulStatus.Stun, .5f, 1.2f); });
        Skill("Splash", "splash", "물보라", "물을 거세게 뿜어 주변 적을 밀어내고 발을 묶습니다.", s =>
        { s.Burst = true; s.Range = 2.5f; s.Radius = Value(2.5f); s.Cooldown = 12; s.Costs = Stamina(7); s.CastTime = .6f;
          s.DamageKind = SoulDamageKind.Wind; s.Damage = Value(3, Term(StatType.Strength, .3f)); s.Knockback = 1.3f; s.Status = St(SoulStatus.Slow, .6f, 3); });
        Skill("Tidal", "tidal", "해일", "물결을 일으켜 일직선 위의 적을 쓸어냅니다.", s =>
        { s.Line = true; s.Width = .9f; s.Range = 5; s.Cooldown = 12; s.Costs = Mana(18); s.CastTime = 1.2f;
          s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Cold; s.Damage = Value(10, Term(StatType.Magic, 1.2f)); s.Knockback = 1.5f; });
        // 늪의 군주 (보스)
        Skill("BogSummon", "bog_summon", "늪 소환", "늪의 군주의 권능. 대상 지점에 넓은 늪을 불러 적을 가라앉히고 썩게 합니다.", s =>
        { s.Field = SoulFieldKind.Hazard; s.AtTarget = true; s.Range = 5; s.Radius = Value(3.5f); s.Duration = Value(12); s.Cooldown = 25; s.Costs = Stamina(12); s.CastTime = 1f;
          s.DamageSchool = Other; s.DamageKind = SoulDamageKind.Poison; s.Damage = Value(3, Term(StatType.Vitality, .3f));
          s.Status = St(SoulStatus.Slow, .8f, 2, null, 2); s.Extra = St(SoulStatus.Poison, .6f, 5, Value(3)); });
        Skill("Swallow", "swallow", "삼키기", "늪의 군주의 권능. 대상을 통째로 삼켜 꼼짝 못 하게 하고 소화액으로 녹입니다.", s =>
        { s.Range = 2; s.Cooldown = 20; s.Costs = Stamina(12); s.CastTime = 1f; s.DamageSchool = Other; s.DamageKind = SoulDamageKind.Poison;
          s.Damage = Value(8, Term(StatType.Vitality, .6f), Term(StatType.Strength, .4f));
          s.Status = St(SoulStatus.Stun, .9f, 3, null, 2); s.Extra = St(SoulStatus.Poison, .9f, 5, Value(4, Term(StatType.Vitality, .2f)), 2); });
        // 호수의 주인 (보스)
        Skill("Whirlpool", "whirlpool", "소용돌이", "호수의 주인의 권능. 소용돌이로 주변 적을 한 점으로 끌어모읍니다.", s =>
        { s.Burst = true; s.Pull = 3; s.Range = 3; s.Radius = Value(4.5f); s.Cooldown = 18; s.Costs = Mana(22); s.CastTime = 1.2f;
          s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Cold; s.Damage = Value(8, Term(StatType.Magic, .8f)); s.Status = St(SoulStatus.Slow, .8f, 3, null, 2); });
        Skill("DragonScale", "dragon_scale", "수룡의 비늘", "호수의 주인의 권능. 물의 비늘이 받는 피해를 마력으로 막고 마법을 튕겨냅니다.", s =>
        { s.Range = 6; s.Cooldown = 30; s.Costs = Mana(10); s.Duration = Value(15); s.CastTime = .6f;
          s.SelfBuffs = new[] { Buff(StatType.ManaShield, Value(.6f)), Buff(StatType.MagicResist, Value(.15f)) }; });

        // ── 화염 지대 ──
        Skill("BoneBreaker", "bone_breaker", "뼈 부수기", "뼈째 부서지라 내리쳐 적을 약하게 만듭니다.", s =>
        { s.Range = 2; s.Cooldown = 12; s.Costs = Stamina(10); s.CastTime = .9f; s.Damage = Value(8, Term(StatType.Strength, 1.2f)); s.Status = St(SoulStatus.Weaken, .7f, 5); });
        Skill("WarFrenzy", "war_frenzy", "전쟁의 광기", "광기로 날뛰어 적의 시선을 끌고 더 세게 칩니다.", s =>
        { s.Range = 3; s.Cooldown = 25; s.Costs = Stamina(8); s.Duration = Value(8); s.CastTime = .4f;
          s.SelfBuffs = new[] { Buff(StatType.Threat, Value(2)), Buff(StatType.Attack, Value(4, Term(StatType.Strength, .3f))) }; });
        Skill("TailSweep", "tail_sweep", "꼬리 휩쓸기", "꼬리로 주변을 쓸어 적을 넘어뜨립니다.", s =>
        { s.Burst = true; s.Range = 2; s.Radius = Value(2.2f); s.Cooldown = 12; s.Costs = Stamina(8); s.CastTime = .7f;
          s.Damage = Value(4, Term(StatType.Agility, .4f)); s.Status = St(SoulStatus.Stun, .5f, 1); });
        Skill("Molt", "molt", "탈피", "허물을 벗어 몸에 걸린 모든 것을 떨쳐내고 잠시 날렵해집니다.", s =>
        { s.Cleanse = true; s.Range = 6; s.Cooldown = 20; s.Costs = Stamina(5); s.Duration = Value(4); s.CastTime = .3f;
          s.SelfBuffs = new[] { Buff(StatType.Evasion, Value(.2f)) }; });
        Skill("FireBreath", "fire_breath", "화염 숨결", "앞으로 불길을 뿜어 일직선 위의 적을 태웁니다.", s =>
        { s.Line = true; s.Width = .8f; s.Range = 3.5f; s.Cooldown = 10; s.Costs = Stamina(9); s.CastTime = .8f;
          s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Fire; s.Damage = Value(6, Term(StatType.Strength, .4f), Term(StatType.Vitality, .3f));
          s.Status = St(SoulStatus.Burn, .6f, 4, Value(2)); });
        Skill("LavaPool", "lava_pool", "용암 웅덩이", "대상 지점을 녹여 용암 웅덩이를 만듭니다.", s =>
        { s.Field = SoulFieldKind.Hazard; s.AtTarget = true; s.Range = 5; s.Radius = Value(2); s.Duration = Value(8); s.Cooldown = 14; s.Costs = Mana(18); s.CastTime = 1.2f;
          s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Fire; s.Damage = Value(4, Term(StatType.Magic, .45f)); s.Status = St(SoulStatus.Burn, .5f, 3, Value(2)); });
        // 화염 드라코 (보스)
        Skill("DragonBreath", "dragon_breath", "용의 숨결", "화염 드라코의 권능. 넓게 퍼지는 불길로 앞의 모든 것을 태웁니다.", s =>
        { s.Line = true; s.Width = 1.6f; s.Range = 5; s.Cooldown = 14; s.Costs = Stamina(14); s.CastTime = 1.2f;
          s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Fire; s.Damage = Value(16, Term(StatType.Strength, .8f), Term(StatType.Magic, .8f));
          s.Status = St(SoulStatus.Burn, .9f, 5, Value(4, Term(StatType.Strength, .2f)), 2); });
        Skill("DiveBomb", "dive_bomb", "비상 강하", "화염 드라코의 권능. 높이 날아올라 대상에게 불타며 내리꽂힙니다.", s =>
        { s.LeapToTarget = true; s.Range = 6; s.Radius = Value(2.5f); s.Cooldown = 16; s.Costs = Stamina(14);
          s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Fire; s.Damage = Value(14, Term(StatType.Strength, 1f)); s.Knockback = 1.4f; });

        // ── 언데드 ──
        Skill("BoneShield", "bone_shield", "뼈 방패", "뼈를 둘러 막고, 때린 자에게 되돌려줍니다.", s =>
        { s.Range = 3; s.Cooldown = 25; s.Costs = Stamina(6); s.Duration = Value(10); s.CastTime = .5f;
          s.SelfBuffs = new[] { Buff(StatType.Armor, Value(8, Term(StatType.Vitality, .5f))), Buff(StatType.Thorns, Value(.2f)) }; });
        Skill("BoneRise", "bone_rise", "되살아나는 뼈", "쓰러질 듯하면 뼈가 다시 맞춰져, 잠시 동안 쓰러질 피해를 한 번 버팁니다.", s =>
        { s.Trigger = SoulTrigger.LowHealth; s.Range = 3; s.Cooldown = 60; s.Costs = Stamina(4); s.Duration = Value(10); s.CastTime = .3f;
          s.SelfBuffs = new[] { Buff(StatType.DeathDefy, Value(1)) }; });
        Skill("BoneSpear", "bone_spear", "뼈 창", "뼈를 창처럼 쏘아 일직선 위의 적을 꿰뚫습니다.", s =>
        { s.Line = true; s.Range = 5.5f; s.Cooldown = 10; s.Costs = Mana(16); s.CastTime = 1.1f;
          s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Pierce; s.Damage = Value(10, Term(StatType.Magic, 1.3f)); });
        Skill("Devour", "devour", "시체 먹기", "쓰러뜨린 적을 뜯어먹어 기운을 되찾습니다.", s =>
        { s.Trigger = SoulTrigger.OnKill; s.Cooldown = 4; s.Costs = Stamina(2); s.CastTime = .5f; s.Heal = Value(8, Term(StatType.Vitality, .8f)); });
        Skill("ParalyzeClaw", "paralyze_claw", "마비 손톱", "썩은 손톱이 파고들어 몸을 굳게 합니다.", s =>
        { s.Range = 2; s.Cooldown = 10; s.Costs = Stamina(6); s.CastTime = .6f; s.DamageKind = SoulDamageKind.Slash;
          s.Damage = Value(4, Term(StatType.Strength, .4f)); s.Status = St(SoulStatus.Stun, .45f, 1.2f); });
        Skill("BloodPact", "blood_pact", "피의 계약", "한동안 준 피해만큼 피를 빨아들입니다.", s =>
        { s.Range = 3; s.Cooldown = 30; s.Costs = Stamina(8); s.Duration = Value(10); s.CastTime = .4f; s.SelfBuffs = new[] { Buff(StatType.LifeSteal, Value(.3f)) }; });
        Skill("BatForm", "bat_form", "박쥐 변신", "위기에 박쥐 떼로 흩어져 몸을 빼냅니다.", s =>
        { s.Trigger = SoulTrigger.LowHealth; s.Retreat = 3; s.Range = 3; s.Cooldown = 25; s.Costs = Stamina(6); s.Duration = Value(4); s.CastTime = .3f;
          s.SelfBuffs = new[] { Buff(StatType.Evasion, Value(.3f)), Buff(StatType.MoveSpeed, Value(.3f)) }; });
        Skill("LifeDrain", "life_drain", "생명 흡수", "대상의 생명을 빨아들여 준 피해만큼 회복합니다.", s =>
        { s.Drain = 1f; s.Range = 5; s.Cooldown = 10; s.Costs = Mana(16); s.CastTime = 1.2f;
          s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Arcane; s.Damage = Value(8, Term(StatType.Magic, 1f)); });
        Skill("Beastform", "beastform", "야수화", "달의 피가 깨어나 한동안 짐승이 됩니다.", s =>
        { s.Range = 3; s.Cooldown = 45; s.Costs = Stamina(12); s.Duration = Value(20); s.CastTime = .6f;
          s.SelfBuffs = new[] { Buff(StatType.Attack, Value(6, Term(StatType.Strength, .3f))), Buff(StatType.ActionSpeed, Value(.15f)), Buff(StatType.HpRegen, Value(3)) }; });
        // 뼈의 왕 (보스)
        Skill("DeathSentence", "death_sentence", "죽음의 선고", "뼈의 왕의 권능. 대상에게 죽음을 선고해 극한의 공포와 쇠약에 빠뜨립니다.", s =>
        { s.Range = 5.5f; s.Cooldown = 16; s.Costs = Mana(24); s.CastTime = 1.6f; s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Arcane;
          s.Status = St(SoulStatus.Fear, .9f, 5, null, 3); s.Extra = St(SoulStatus.Weaken, .9f, 6, null, 2); });
        Skill("LegionOfDead", "legion_of_dead", "망자의 군세", "뼈의 왕의 권능. 해골 병사들이 일어나 한동안 주변 적을 베어댑니다.", s =>
        { s.Field = SoulFieldKind.Aura; s.Range = 3; s.Radius = Value(2.8f); s.Duration = Value(12); s.Cooldown = 35; s.Costs = Mana(20); s.CastTime = 1f;
          s.DamageKind = SoulDamageKind.Slash; s.Damage = Value(5, Term(StatType.Will, .5f)); s.Status = St(SoulStatus.Fear, .2f, 2); });

        // ── 성소·마계 ──
        Skill("LightBarrier", "light_barrier", "빛의 방벽", "빛의 막으로 주변 동료를 마법과 공포에서 지킵니다.", s =>
        { s.Trigger = SoulTrigger.AllySupport; s.Radius = Value(5); s.Cooldown = 25; s.Costs = Mana(12); s.Duration = Value(10); s.CastTime = .8f;
          s.SelfBuffs = new[] { Buff(StatType.MagicResist, Value(.2f)), Buff(StatType.FearResist, Value(.3f)) }; });
        Skill("Seal", "seal", "봉인", "대상을 봉인해 잠시 아무 스킬도 쓰지 못하게 합니다.", s =>
        { s.Range = 4.5f; s.Cooldown = 16; s.Costs = Mana(14); s.CastTime = 1f; s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Arcane;
          s.Status = St(SoulStatus.Silence, .7f, 4); });
        Skill("HellfireSmash", "hellfire_smash", "지옥불 강타", "땅을 내리쳐 지옥불을 터뜨립니다.", s =>
        { s.Burst = true; s.Range = 2; s.Radius = Value(2.5f); s.Cooldown = 14; s.Costs = Stamina(12); s.CastTime = 1f;
          s.DamageKind = SoulDamageKind.Fire; s.Damage = Value(10, Term(StatType.Strength, .9f)); s.Knockback = .8f; s.Status = St(SoulStatus.Burn, .6f, 4, Value(3)); });
        Skill("DemonPact", "demon_pact", "악마의 계약", "피를 바쳐 한동안 악마의 힘을 빌립니다.", s =>
        { s.HpCost = .2f; s.Range = 3; s.Cooldown = 40; s.Duration = Value(12); s.CastTime = .4f;
          s.SelfBuffs = new[] { Buff(StatType.Attack, Value(8, Term(StatType.Strength, .5f))), Buff(StatType.ActionSpeed, Value(.2f)), Buff(StatType.LifeSteal, Value(.1f)) }; });
        Skill("AgonyCurse", "agony_curse", "고통의 저주", "대상에게 아물지 않는 상처를 새깁니다.", s =>
        { s.Range = 5; s.Cooldown = 12; s.Costs = Mana(16); s.CastTime = 1.2f; s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Arcane;
          s.Status = St(SoulStatus.Bleed, .9f, 8, Value(3, Term(StatType.Magic, .4f)), 2); });
        Skill("SoulSap", "soul_sap", "영혼 착취", "대상의 마력과 기력을 빼앗아 제 것으로 삼습니다.", s =>
        { s.Sap = .35f; s.Range = 4.5f; s.Cooldown = 14; s.Costs = Mana(8); s.CastTime = 1f;
          s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Arcane; s.Damage = Value(4, Term(StatType.Magic, .5f)); });
        Skill("ShadowArrow", "shadow_arrow", "그림자 화살", "그림자를 엮은 화살이 일직선 위의 적을 꿰뚫고 겁먹게 합니다.", s =>
        { s.Line = true; s.Range = 6; s.Cooldown = 10; s.Costs = Stamina(8); s.CastTime = .9f; s.DamageKind = SoulDamageKind.Pierce;
          s.Damage = Value(8, Term(StatType.Agility, .8f), Term(StatType.PiercePower, .4f)); s.Status = St(SoulStatus.Fear, .4f, 2); });
        Skill("ShadowHide", "shadow_hide", "그림자 숨기", "그림자에 녹아들어 잠시 눈에 띄지 않고, 다음 공격을 급소에 꽂습니다.", s =>
        { s.Range = 4; s.Cooldown = 30; s.Costs = Stamina(8); s.Duration = Value(4); s.CastTime = .3f;
          s.SelfBuffs = new[] { Buff(StatType.Threat, Value(-.9f)), Buff(StatType.Evasion, Value(.4f)), Buff(StatType.CritChance, Value(.5f)) }; });
        Skill("Plunder", "plunder", "약탈 본능", "적을 쓰러뜨릴 때마다 한동안 전리품과 금화를 더 챙깁니다.", s =>
        { s.Trigger = SoulTrigger.OnKill; s.Cooldown = 5; s.Duration = Value(60); s.CastTime = .2f;
          s.SelfBuffs = new[] { Buff(StatType.LootFind, Value(.15f)), Buff(StatType.GoldFind, Value(.15f)) }; });
        Skill("CrystalWall", "crystal_wall", "결정 방벽", "결정을 솟게 해 주변 동료를 감쌉니다.", s =>
        { s.Trigger = SoulTrigger.AllySupport; s.Radius = Value(5); s.Cooldown = 28; s.Costs = Stamina(10); s.Duration = Value(10); s.CastTime = .8f;
          s.SelfBuffs = new[] { Buff(StatType.Armor, Value(8, Term(StatType.Vitality, .4f))), Buff(StatType.MagicResist, Value(.15f)) }; });
        Skill("CrystalReflect", "crystal_reflect", "결정 반사", "몸의 결정이 받은 마법의 일부를 시전자에게 되돌립니다.", s =>
        { s.Range = 6; s.Cooldown = 30; s.Costs = Stamina(6); s.Duration = Value(10); s.CastTime = .4f; s.SelfBuffs = new[] { Buff(StatType.SpellReflect, Value(.4f)) }; });
        // 타락한 반신 (보스)
        Skill("GodsWrath", "gods_wrath", "신의 분노", "타락한 반신의 권능. 하늘을 갈라 넓은 범위에 벼락을 떨어뜨립니다.", s =>
        { s.Burst = true; s.Technique = true; s.Range = 3; s.Radius = Value(5); s.Cooldown = 20; s.Costs = Mana(24); s.CastTime = 1.5f;
          s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Lightning; s.Damage = Value(20, Term(StatType.Strength, 1f), Term(StatType.Will, 1.2f));
          s.Status = St(SoulStatus.Stun, .6f, 1.5f, null, 2); });
        // 심연의 군주 (보스)
        Skill("AbyssGate", "abyss_gate", "심연의 문", "심연의 군주의 권능. 심연을 열어 주변 적을 빨아들인 뒤 터뜨립니다.", s =>
        { s.Burst = true; s.Technique = true; s.Pull = 3; s.Range = 3; s.Radius = Value(5); s.Cooldown = 20; s.Costs = Mana(20); s.CastTime = 1.4f;
          s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Arcane; s.Damage = Value(18, Term(StatType.Strength, .9f), Term(StatType.Will, .9f));
          s.Status = St(SoulStatus.Fear, .5f, 3, null, 2); });
        Skill("Void", "void", "공허", "심연의 군주의 권능. 대상의 모든 강화를 지우고 입을 막습니다.", s =>
        { s.Dispel = true; s.Range = 4; s.Cooldown = 15; s.Costs = Stamina(8); s.CastTime = .8f;
          s.DamageSchool = Magic; s.DamageKind = SoulDamageKind.Arcane; s.Damage = Value(6, Term(StatType.Will, .6f)); s.Status = St(SoulStatus.Silence, .6f, 3, null, 2); });

        // which soul brings which (the borrowed mage skills give way to the monsters' own)
        void Give(string soulFile, params string[] files)
        {
            var soul = AssetDatabase.LoadAssetAtPath<SoulData>(DataPath + "/" + soulFile + ".asset");
            if (soul == null) return;
            var list = new List<SoulActiveSkillData>();
            if (soulFile == "SoulBoar") list.AddRange(soul.ActiveSkills); // 분노의 돌격 stays
            foreach (string file in files) list.Add(made[file]);
            soul.ActiveSkills = list.ToArray();
            EditorUtility.SetDirty(soul);
        }
        foreach (var (soul, files) in SoulSkillMap) Give(soul, files);

        // a boss fights with its own soul's skills (always warned: a monster winds up)
        foreach (string soulFile in new[] { "SoulToadLord", "SoulLakeDrake", "SoulFireDrake", "SoulBoneKing", "SoulDemigod", "SoulAbyssLord" })
        {
            var soul = AssetDatabase.LoadAssetAtPath<SoulData>(DataPath + "/" + soulFile + ".asset");
            if (soul == null || soul.Source == null) continue;
            var list = new List<SoulActiveSkillData>();
            foreach (var skill in soul.Source.ActiveSkills) if (skill != null && !skill.name.StartsWith("SoulSkill")) list.Add(skill);
            list.AddRange(soul.ActiveSkills);
            soul.Source.ActiveSkills = list.ToArray();
            EditorUtility.SetDirty(soul.Source);
        }

        // 회피 본능: only the quick, slippery kinds evade at all
        foreach (string path in AssetDatabase.FindAssets("t:SoulMonsterData", new[] { DataPath }))
        {
            var monster = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(AssetDatabase.GUIDToAssetPath(path));
            if (monster == null) continue;
            monster.Evasive = System.Array.IndexOf(EvasiveMonsters, monster.name) >= 0;
            monster.Undead = System.Array.IndexOf(UndeadMonsters, monster.name) >= 0;
            EditorUtility.SetDirty(monster);
        }
        // the older skills a mercenary fires at once
        foreach (string file in new[] { "SkillAlertShot", "SkillRageCharge" })
        {
            var skill = AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/" + file + ".asset");
            if (skill != null) { skill.Instant = true; EditorUtility.SetDirty(skill); }
        }
    }

    // 언데드 (퇴마 hits them harder).
    public static readonly string[] UndeadMonsters =
        { "MonsterSkeleton", "MonsterSkeletonArcher", "MonsterSkeletonMage", "MonsterZombie", "MonsterRotten", "MonsterBoneKnight", "MonsterBoneKing", "MonsterVampire", "MonsterVampireMage" };

    // 회피 본능 (monster trait): dodgers, rollers, kiters — the rest stand and take it.
    public static readonly string[] EvasiveMonsters =
        { "MonsterWolf", "MonsterGoblinArcher", "MonsterFrogShooter", "MonsterLizard", "MonsterSkeletonArcher", "MonsterVampire", "MonsterWerewolf", "MonsterDarkElf", "MonsterBeastfolk" };

    // A mercenary uses these at once (reactions, quick strikes, spells that are lightning-fast, whatever it lays on the
    // ground); every other harmful skill warns first. A monster always warns.
    public static readonly HashSet<string> InstantSkills = new HashSet<string>
    {
        "SkillExecution", "SkillSmokeBomb", "SkillChainLightning", "SkillTrap", "SkillPackCall", "SkillAlertShot", "SkillRageCharge",
        "SoulSkillPickpocket", "SoulSkillTongueLash", "SoulSkillSlimeSpit", "SoulSkillParalyzeClaw", "SoulSkillFlameBurst", "SoulSkillBloodSuck",
        "SoulSkillSeal", "SoulSkillVoid", "SoulSkillSoulSap", "SoulSkillLifeDrain", "SoulSkillCurseDoll", "SoulSkillAgonyCurse",
        "SoulSkillDynamite", "SoulSkillAcidPool", "SoulSkillIceFloor", "SoulSkillSplit", "SoulSkillLegionOfDead", "SoulSkillLavaPool", "SoulSkillBogSummon",
    };

    public static readonly (string soul, string[] files)[] SoulSkillMap =
    {
        ("SoulBoar", new[] { "MudRoll" }), ("SoulWolf", new[] { "WolfHowl", "ShadowPounce" }), ("SoulStone", new[] { "Statue" }),
        ("SoulGoblin", new[] { "ArrowRain" }), ("SoulGoblinMiner", new[] { "Dynamite", "Pickpocket" }), ("SoulGoblinShaman", new[] { "CurseDoll" }),
        ("SoulSlug", new[] { "AcidPool" }), ("SoulFireSlime", new[] { "FlameBurst", "Split" }), ("SoulFrostSlime", new[] { "IceFloor" }),
        ("SoulPlagueSlime", new[] { "PlagueCloud" }), ("SoulBloodSlime", new[] { "BloodSuck" }),
        ("SoulFrog", new[] { "TongueLash", "SlimeSpit" }), ("SoulTroll", new[] { "TrollRegen", "BoulderThrow" }), ("SoulFishman", new[] { "Splash" }),
        ("SoulFishCaller", new[] { "Tidal" }), ("SoulToadLord", new[] { "BogSummon", "Swallow" }), ("SoulLakeDrake", new[] { "Whirlpool", "DragonScale" }),
        ("SoulOrc", new[] { "BoneBreaker", "WarFrenzy" }), ("SoulLizard", new[] { "TailSweep", "Molt" }), ("SoulFireLizard", new[] { "FireBreath" }),
        ("SoulFireCaller", new[] { "LavaPool" }), ("SoulFireDrake", new[] { "DragonBreath", "DiveBomb" }),
        ("SoulSkeleton", new[] { "BoneShield", "BoneRise" }), ("SoulSkeletonMage", new[] { "BoneSpear" }), ("SoulGhoul", new[] { "Devour", "ParalyzeClaw" }),
        ("SoulVampire", new[] { "BloodPact", "BatForm" }), ("SoulVampireMage", new[] { "LifeDrain" }), ("SoulWerewolf", new[] { "Beastform" }),
        ("SoulBoneKing", new[] { "DeathSentence", "LegionOfDead" }),
        ("SoulSanctum", new[] { "LightBarrier", "Seal" }), ("SoulDemon", new[] { "HellfireSmash", "DemonPact" }), ("SoulDemonWarlock", new[] { "AgonyCurse", "SoulSap" }),
        ("SoulDarkElf", new[] { "ShadowArrow", "ShadowHide" }), ("SoulBeastfolk", new[] { "Plunder" }), ("SoulCrystalGolem", new[] { "CrystalWall", "CrystalReflect" }),
        ("SoulDemigod", new[] { "GodsWrath" }), ("SoulAbyssLord", new[] { "AbyssGate", "Void" }),
    };
    public static readonly string[] BossSoulSkills = { "BogSummon", "Swallow", "Whirlpool", "DragonScale", "DragonBreath", "DiveBomb", "DeathSentence", "LegionOfDead", "GodsWrath", "AbyssGate", "Void" };

    // The additive material the effects glow with (SoulFx.Additive): in Resources so the build carries its shader.
    static void CreateFxMaterial()
    {
        var shader = Shader.Find("SoulMercenaries/FxAdditive");
        if (shader == null) { Debug.LogWarning("SoulMercenaries/FxAdditive shader not found: effects blend without glow"); return; }
        System.IO.Directory.CreateDirectory("Assets/Resources");
        const string path = "Assets/Resources/SoulFxAdditive.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) AssetDatabase.CreateAsset(new Material(shader) { name = "SoulFxAdditive" }, path);
        else { material.shader = shader; EditorUtility.SetDirty(material); }
    }

    // For now every mercenary of a job knows its job's skills and anyone's from the start (the books still teach them
    // to others; the stronger forms come from the library). A conditional one comes before the rest of its job's.
    public static readonly (string job, string[] files)[] JobSkillFiles =
    {
        ("검사", new[] { "SkillExecution", "SkillIssen" }),
        ("수호자", new[] { "SkillIronWall", "SkillGuardianCry" }),
        ("투사", new[] { "SkillFrenzy", "SkillRoar" }),
        ("길잡이", new[] { "SkillSmokeBomb", "SkillTrap", "SkillPiercingShot" }),
        ("마법사", new[] { "SkillManaShield", "SkillChainLightning", "SkillFrostPrison", "SkillBlizzard", "SkillConfusion", "SkillPetrifyGaze", "SkillArcaneBurst" }),
        ("성직자", new[] { "SkillPurify", "SkillSacrifice", "SkillPrayer", "SkillSanctuary", "SkillJudgment", "SkillWardPrayer", "SkillLifeBlessing", "SkillConsecrate", "SkillHolyShield", "SkillRenewalVow", "SkillBlessedRain" }),
        ("궁수", new[] { "SkillQuickShot", "SkillArrowRain", "SkillAimedShot", "SkillPinShot" }),
        ("소환사", new[] { "SkillSummonFire", "SkillSummonFrost", "SkillSummonWind", "SkillSummonLight" }),
    };
    public static readonly string[] CommonSkillFiles = { "SkillWarCry", "SkillBattleFocus" };

    // A test of an older mechanic sees the mercenary as it was before the job skills were handed out.
    static void Classic(SoulMercenary hero)
    {
        var given = new HashSet<string>(CommonSkillFiles);
        foreach (var (_, files) in JobSkillFiles) foreach (string file in files) given.Add(file);
        hero.StartingActives.RemoveAll(skill => skill != null && given.Contains(skill.name));
    }

    static void GiveJobSkills(SoulMercenaryData[] templates)
    {
        foreach (var template in templates)
        {
            if (template == null) continue;
            var list = new List<SoulActiveSkillData>(template.ActiveSkills ?? new SoulActiveSkillData[0]);
            var files = new List<string>();
            foreach (var (job, mine) in JobSkillFiles) if (job == template.Job) files.AddRange(mine);
            files.AddRange(CommonSkillFiles);
            foreach (string file in files)
            {
                var skill = AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/" + file + ".asset");
                if (skill != null && !list.Contains(skill)) list.Add(skill);
            }
            template.ActiveSkills = list.ToArray();
            EditorUtility.SetDirty(template);
        }
    }

    // Skill books: which tier each skill drops at and what it becomes (library level 3).
    static SoulActiveSkillData[] SkillBookPool()
    {
        SoulActiveSkillData S(string file) => AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/" + file + ".asset");
        var books = new List<SoulActiveSkillData>();
        void Book(string file, int tier, string upgradeFile, string upgradeName)
        {
            var skill = S(file);
            if (skill == null) return;
            skill.BookTier = tier;
            skill.UpgradeTo = upgradeFile != null ? Upgraded(skill, upgradeFile, upgradeName) : null;
            EditorUtility.SetDirty(skill);
            books.Add(skill);
        }
        Book("SkillRageCharge", 1, "SkillRageChargePlus", "광란의 돌격");
        Book("SkillTaunt", 1, "SkillTauntPlus", "대도발");
        Book("SkillFireEnchant", 1, "SkillFireEnchantPlus", "업화 부여");
        Book("SkillFrostEnchant", 1, "SkillFrostEnchantPlus", "혹한 부여");
        Book("SkillHaste", 1, "SkillHastePlus", "가속");
        Book("SkillCurseWeakness", 1, "SkillCurseWeaknessPlus", "쇠락의 저주");
        Book("SkillAlertShot", 1, "SkillAlertShotPlus", "정밀 경계 사격");
        Book("SkillSlow", 2, "SkillSlowPlus", "속박");
        Book("SkillLeapShockwave", 2, "SkillLeapShockwavePlus", "대지 강타");
        Book("SkillHealingLight", 2, "SkillHealingLightPlus", "치유의 광휘");
        Book("SkillResurrection", 3, null, null);
        // each job's own (직업 전용)
        Book("SkillIssen", 2, "SkillIssenPlus", "일섬·극");
        Book("SkillExecution", 2, "SkillExecutionPlus", "단죄");
        Book("SkillGuardianCry", 2, "SkillGuardianCryPlus", "불굴의 함성");
        Book("SkillIronWall", 1, "SkillIronWallPlus", "금강 철벽");
        Book("SkillRoar", 1, "SkillRoarPlus", "공포의 포효");
        Book("SkillFrenzy", 2, "SkillFrenzyPlus", "피의 광폭화");
        Book("SkillSmokeBomb", 1, "SkillSmokeBombPlus", "짙은 연막");
        Book("SkillTrap", 1, "SkillTrapPlus", "올가미 덫");
        Book("SkillPiercingShot", 2, "SkillPiercingShotPlus", "섬광 관통");
        Book("SkillFrostPrison", 1, "SkillFrostPrisonPlus", "영겁의 얼음");
        Book("SkillBlizzard", 2, "SkillBlizzardPlus", "혹한의 폭풍");
        Book("SkillConfusion", 1, "SkillConfusionPlus", "광란의 주문");
        Book("SkillPetrifyGaze", 2, "SkillPetrifyGazePlus", "메두사의 눈");
        Book("SkillArcaneBurst", 2, "SkillArcaneBurstPlus", "비전 붕괴");
        Book("SkillChainLightning", 2, "SkillChainLightningPlus", "뇌신의 사슬");
        Book("SkillManaShield", 1, "SkillManaShieldPlus", "마력 장벽");
        Book("SkillPrayer", 1, "SkillPrayerPlus", "축복의 성가");
        Book("SkillJudgment", 1, "SkillJudgmentPlus", "최후의 심판");
        Book("SkillPurify", 1, "SkillPurifyPlus", "성결");
        Book("SkillSacrifice", 2, "SkillSacrificePlus", "순교");
        Book("SkillSanctuary", 2, "SkillSanctuaryPlus", "대성역");
        Book("SkillWardPrayer", 1, "SkillWardPrayerPlus", "수호의 성가");
        Book("SkillLifeBlessing", 1, "SkillLifeBlessingPlus", "생명의 은총");
        Book("SkillConsecrate", 1, "SkillConsecratePlus", "대축성");
        Book("SkillHolyShield", 1, "SkillHolyShieldPlus", "성벽의 방패");
        Book("SkillRenewalVow", 1, "SkillRenewalVowPlus", "영원의 서약");
        Book("SkillBlessedRain", 2, "SkillBlessedRainPlus", "은총의 폭우");
        Book("SkillFireWall", 2, "SkillFireWallPlus", "업화의 벽");
        Book("SkillIceLance", 1, "SkillIceLancePlus", "빙하의 창");
        Book("SkillGravityWell", 2, "SkillGravityWellPlus", "특이점");
        Book("SkillThunderStrike", 2, "SkillThunderStrikePlus", "천벌");
        Book("SkillQuickShot", 1, "SkillQuickShotPlus", "섬광 속사");
        Book("SkillArrowRain", 2, "SkillArrowRainPlus", "화살 폭우");
        Book("SkillAimedShot", 2, "SkillAimedShotPlus", "필살 저격");
        Book("SkillPinShot", 1, "SkillPinShotPlus", "꿰뚫는 족쇄");
        Book("SkillSummonFire", 1, "SkillSummonFirePlus", "화염 대정령");
        Book("SkillSummonFrost", 1, "SkillSummonFrostPlus", "빙설 대정령");
        Book("SkillSummonWind", 1, "SkillSummonWindPlus", "폭풍 늑대");
        Book("SkillSummonLight", 2, "SkillSummonLightPlus", "광휘의 대정령");
        // anyone's
        Book("SkillWarCry", 1, "SkillWarCryPlus", "전쟁의 함성");
        Book("SkillBattleFocus", 1, "SkillBattleFocusPlus", "명경지수");
        return books.ToArray();
    }

    // Three of a soul fuse into its refined form at the altar: one grade up, half again as strong.
    static void RefineSouls()
    {
        foreach (var soul in All<SoulData>())
        {
            if (soul.name.EndsWith("Refined")) continue;
            var source = soul;
            var refined = Asset(soul.name + "Refined", (SoulData copy) =>
            {
                EditorUtility.CopySerialized(source, copy);
                copy.Id = source.Id + "_refined"; copy.Grade = source.Grade + 1; copy.Refined = null;
                copy.OriginMonster = source.OriginMonster + "(정제)";
                var stats = new SoulStatBonus[source.CharacteristicStats.Length];
                for (int i = 0; i < stats.Length; i++) stats[i] = new SoulStatBonus { Stat = source.CharacteristicStats[i].Stat, Value = source.CharacteristicStats[i].Value * 1.5f };
                copy.CharacteristicStats = stats;
                var skills = new SoulActiveSkillData[source.ActiveSkills.Length];
                for (int i = 0; i < skills.Length; i++)
                {
                    var skill = source.ActiveSkills[i];
                    skills[i] = skill != null && skill.name.StartsWith("SoulSkill") ? Upgraded(skill, skill.name + "Refined", skill.SkillName + " (정제)") : skill;
                }
                copy.ActiveSkills = skills;
            });
            refined.name = soul.name + "Refined";
            soul.Refined = refined;
            EditorUtility.SetDirty(soul);
        }
    }

    // ── equipment catalog (DESIGN_EQUIPMENT_MERCENARIES.md §1.5, §5) ──

    static SoulEquipmentData Load(string file) => AssetDatabase.LoadAssetAtPath<SoulEquipmentData>(DataPath + "/" + file + ".asset");

    // One template. Options are the 평범한 values of its kind (grade and material scale them); the look is what
    // the wearer shows. Shop tier 0: never sold; drop floor: the shallowest floor it can drop on.
    static SoulEquipmentData Gear(string file, string id, string name, string kind, SoulEquipSlot slot, float weight, SoulMaterial material,
        int price, int shopTier, int dropFloor, SoulAppearancePatch look, params SoulStatBonus[] bonuses)
        => Asset(file, (SoulEquipmentData item) =>
        {
            item.Id = id; item.DisplayName = name; item.Kind = kind; item.Slot = slot; item.BaseWeight = weight; item.Material = material;
            item.Price = price; item.ShopTier = shopTier; item.DropFloor = dropFloor; item.Look = look; item.Bonuses = bonuses;
            item.Unique = false; item.TwoHanded = false; item.WeaponTag = null; item.BasicAttack = null;
            item.Patterns = System.Array.Empty<SoulPatternData>();
        });

    static SoulEquipmentData Weapon(SoulEquipmentData item, string tag, bool twoHanded, SoulPatternData basic, SoulPatternData granted = null)
    {
        item.WeaponTag = tag; item.TwoHanded = twoHanded; item.BasicAttack = basic;
        item.Patterns = granted != null ? new[] { granted } : System.Array.Empty<SoulPatternData>();
        EditorUtility.SetDirty(item);
        return item;
    }

    static SoulEquipmentData Unique(SoulEquipmentData item, int grade)
    {
        item.Unique = true; item.FixedGrade = grade; item.ShopTier = 0;
        EditorUtility.SetDirty(item);
        return item;
    }

    static SoulAppearancePatch W(string sprite) => new SoulAppearancePatch { Weapon = sprite };
    static SoulAppearancePatch Sh(string sprite) => new SoulAppearancePatch { Shield = sprite };
    static SoulAppearancePatch Ar(string sprite) => new SoulAppearancePatch { Armor = sprite };
    static SoulAppearancePatch He(string sprite) => new SoulAppearancePatch { Helmet = sprite };

    static List<SoulEquipmentData> CreateEquipmentCatalog(SoulPatternData slash, SoulPatternData swing, SoulPatternData thrust, SoulPatternData claw, SoulPatternData bow)
    {
        const SoulEquipSlot Main = SoulEquipSlot.MainHand, Off = SoulEquipSlot.OffHand, Body = SoulEquipSlot.Body, Head = SoulEquipSlot.Head, Acc = SoulEquipSlot.Accessory;
        var M = SoulMaterial.None;
        // bare fists in iron (야크, 하쿠): quick straight punches
        var punch = Pattern("Punch", "주먹질", SoulPatternCategory.Attack, 3, .7f, 1f, SoulDamageSchool.Physical, SoulDamageKind.Impact,
            Value(0, Term(StatType.Attack, .8f), Term(StatType.ImpactPower, .8f)));
        punch.WeaponTag = "fist"; punch.Pierce = Value(1, Term(StatType.ImpactPower, .4f)); EditorUtility.SetDirty(punch);
        var all = new List<SoulEquipmentData>
        {
            // main hand — the starting kit keeps its old asset names
            Weapon(Gear("EquipmentSword", "sword", "철검", "한손검", Main, 3f, SoulMaterial.Iron, 80, 1, 1, W("IronSword"), Bonus(StatType.Attack, 5), Bonus(StatType.SlashPower, 1)), "sword", false, slash),
            Weapon(Gear("EquipmentRustyDagger", "rusty_dagger", "녹슨 단검", "단검", Main, 1.2f, SoulMaterial.Iron, 40, 1, 1, W("RustedShortSword"), Bonus(StatType.Attack, 3), Bonus(StatType.PiercePower, 1)), "dagger", false, thrust),
            Weapon(Gear("EquipmentSteelSword", "steel_sword", "강철 장검", "한손검", Main, 3f, SoulMaterial.Steel, 110, 2, 2, W("Longsword"), Bonus(StatType.Attack, 5), Bonus(StatType.SlashPower, 1)), "sword", false, slash),
            Weapon(Gear("EquipmentKnightSword", "knight_sword", "기사의 검", "한손검", Main, 3f, SoulMaterial.Steel, 140, 3, 4, W("RoyalLongsword"), Bonus(StatType.Attack, 5), Bonus(StatType.SlashPower, 1), Bonus(StatType.Accuracy, .02f)), "sword", false, slash),
            Weapon(Gear("EquipmentGreatsword", "greatsword", "대검", "양손검", Main, 6.5f, SoulMaterial.Iron, 150, 2, 2, W("Greatsword"), Bonus(StatType.Attack, 9), Bonus(StatType.SlashPower, 2)), "sword", true, slash),
            Weapon(Gear("EquipmentBlackGreatsword", "black_greatsword", "흑철 대검", "양손검", Main, 6.5f, SoulMaterial.BlackIron, 220, 0, 4, W("BlackBroadsword"), Bonus(StatType.Attack, 9), Bonus(StatType.SlashPower, 2)), "sword", true, slash),
            Weapon(Gear("EquipmentKatana", "katana", "카타나", "곡도", Main, 2.8f, SoulMaterial.Steel, 170, 3, 3, W("Katana"), Bonus(StatType.Attack, 6), Bonus(StatType.SlashPower, 2), Bonus(StatType.Accuracy, .03f)), "sword", false, slash),
            Weapon(Gear("EquipmentAxe", "axe", "나무꾼 도끼", "한손도끼", Main, 3.5f, SoulMaterial.Iron, 80, 1, 1, W("WoodcutterAxe"), Bonus(StatType.Attack, 6), Bonus(StatType.ImpactPower, 1)), "axe", false, swing),
            Weapon(Gear("EquipmentBattleAxe", "battle_axe", "전투 도끼", "양손도끼", Main, 7.5f, SoulMaterial.Steel, 170, 2, 2, W("BattleAxe"), Bonus(StatType.Attack, 11), Bonus(StatType.ImpactPower, 2)), "axe", true, swing),
            Weapon(Gear("EquipmentMace", "mace", "메이스", "둔기", Main, 4f, SoulMaterial.Iron, 80, 1, 1, W("Mace"), Bonus(StatType.Attack, 5), Bonus(StatType.ImpactPower, 2)), "mace", false, swing),
            Weapon(Gear("EquipmentMorgenstern", "morgenstern", "모닝스타", "둔기", Main, 4f, SoulMaterial.Steel, 150, 3, 3, W("Morgenstern"), Bonus(StatType.Attack, 5), Bonus(StatType.ImpactPower, 2), Bonus(StatType.BleedResist, .02f)), "mace", false, swing),
            Weapon(Gear("EquipmentDwarfHammer", "dwarf_hammer", "드워프 망치", "전투 망치", Main, 8f, SoulMaterial.Steel, 200, 3, 3, W("LargeDwarfHammer"), Bonus(StatType.Attack, 10), Bonus(StatType.ImpactPower, 3)), "mace", true, swing),
            Weapon(Gear("EquipmentHalberd", "halberd", "할버드", "창", Main, 5f, SoulMaterial.Iron, 130, 2, 2, W("Halberd"), Bonus(StatType.Attack, 7), Bonus(StatType.PiercePower, 2)), "spear", true, thrust),
            Weapon(Gear("EquipmentClaws", "claws", "사냥 칼", "발톱", Main, 1.5f, SoulMaterial.Steel, 90, 1, 1, W("HunterKnife"), Bonus(StatType.Attack, 4), Bonus(StatType.SlashPower, 1)), "claw", true, claw),
            Weapon(Gear("EquipmentKnuckles", "knuckles", "철권갑", "권갑", Main, 1.2f, SoulMaterial.Iron, 80, 1, 1, new SoulAppearancePatch(), Bonus(StatType.Attack, 4), Bonus(StatType.ImpactPower, 1)), "fist", true, punch, punch),
            Weapon(Gear("EquipmentShortBow", "short_bow", "짧은 활", "단궁", Main, 1.5f, SoulMaterial.Wood, 90, 1, 1, W("ShortBow"), Bonus(StatType.Attack, 4), Bonus(StatType.PiercePower, 1)), "bow", true, bow, bow),
            Weapon(Gear("EquipmentCurvedBow", "curved_bow", "곡궁", "단궁", Main, 1.5f, SoulMaterial.Spiritwood, 160, 3, 3, W("CurvedBow"), Bonus(StatType.Attack, 4), Bonus(StatType.PiercePower, 1)), "bow", true, bow, bow),
            Weapon(Gear("EquipmentLongBow", "long_bow", "장궁", "장궁", Main, 2.5f, SoulMaterial.Wood, 140, 2, 2, W("LongBow"), Bonus(StatType.Attack, 6), Bonus(StatType.PiercePower, 2)), "bow", true, bow, bow),
            Weapon(Gear("EquipmentStaff", "staff", "견습 지팡이", "지팡이", Main, 2.5f, SoulMaterial.Wood, 90, 1, 1, W("MagicWand"), Bonus(StatType.Attack, 1), Bonus(StatType.Magic, 2)), "staff", true, swing),
            Weapon(Gear("EquipmentElderStaff", "elder_staff", "장로의 지팡이", "지팡이", Main, 2.5f, SoulMaterial.Spiritwood, 180, 3, 3, W("ElderStaff"), Bonus(StatType.Attack, 1), Bonus(StatType.Magic, 3)), "staff", true, swing),
            Weapon(Gear("EquipmentCrystalWand", "crystal_wand", "수정 완드", "완드", Main, .5f, SoulMaterial.Spiritwood, 130, 2, 2, W("CrystalWand"), Bonus(StatType.Magic, 1), Bonus(StatType.MaxMp, 10)), "wand", false, swing),
            Weapon(Gear("EquipmentPriestWand", "priest_wand", "사제의 홀", "성직자 홀", Main, 2f, SoulMaterial.Iron, 110, 1, 2, W("PriestWand"), Bonus(StatType.Attack, 2), Bonus(StatType.Will, 1), Bonus(StatType.Magic, 1)), "holy", false, swing),
            // off hand
            Gear("EquipmentWoodenBuckler", "wooden_buckler", "나무 버클러", "버클러", Off, 3f, SoulMaterial.Wood, 60, 1, 1, Sh("WoodenBuckler"), Bonus(StatType.Armor, 2)),
            Gear("EquipmentSteelShield", "steel_shield", "강철 방패", "카이트 방패", Off, 6f, SoulMaterial.Steel, 140, 2, 2, Sh("SteelShield"), Bonus(StatType.Armor, 4), Bonus(StatType.PhysicalResist, .02f)),
            Gear("EquipmentTowerShield", "tower_shield", "탑 방패", "타워 방패", Off, 11f, SoulMaterial.Iron, 180, 3, 3, Sh("TowerShield"), Bonus(StatType.Armor, 7), Bonus(StatType.KnockbackResist, .1f)),
            Gear("EquipmentGrimoire", "grimoire", "견습 마도서", "마도서", Off, 1f, SoulMaterial.Cloth, 120, 2, 2, new SoulAppearancePatch(), Bonus(StatType.Magic, 2), Bonus(StatType.MaxMp, 15)),
            Gear("EquipmentQuiver", "quiver", "가죽 화살통", "화살통", Off, .8f, SoulMaterial.Leather, 50, 1, 1, new SoulAppearancePatch(), Bonus(StatType.Accuracy, .03f)),
            Gear("EquipmentRelic", "relic", "작은 성물", "성물", Off, .6f, M, 90, 1, 2, new SoulAppearancePatch(), Bonus(StatType.Will, 2)),
            // body
            Gear("EquipmentTravelerTunic", "traveler_tunic", "여행자 옷", "가죽 갑옷", Body, 5f, SoulMaterial.Leather, 70, 1, 1, Ar("TravelerTunic"), Bonus(StatType.Armor, 3), Bonus(StatType.Evasion, .02f)),
            Gear("EquipmentLeatherArmor", "thief_tunic", "도적 튜닉", "가죽 갑옷", Body, 5f, SoulMaterial.Leather, 110, 2, 2, Ar("ThiefTunic"), Bonus(StatType.Armor, 3), Bonus(StatType.Evasion, .03f)),
            Gear("EquipmentGladiatorArmor", "gladiator", "검투사 경갑", "경갑", Body, 7f, SoulMaterial.Leather, 130, 2, 2, Ar("Gladiator"), Bonus(StatType.Armor, 5), Bonus(StatType.Attack, 1)),
            Gear("EquipmentMilitiaArmor", "militia_armor", "민병 갑옷", "사슬 갑옷", Body, 10f, SoulMaterial.Iron, 100, 1, 1, Ar("MilitiamanArmor"), Bonus(StatType.Armor, 6)),
            Gear("EquipmentLegionArmor", "legion_armor", "군단 갑옷", "사슬 갑옷", Body, 10f, SoulMaterial.Steel, 160, 2, 3, Ar("LegionaryArmor"), Bonus(StatType.Armor, 6)),
            Gear("EquipmentPlateArmor", "plate_armor", "강철 판금", "판금 갑옷", Body, 18f, SoulMaterial.Steel, 240, 3, 3, Ar("IronKnight"), Bonus(StatType.Armor, 10), Bonus(StatType.PhysicalResist, .04f)),
            Gear("EquipmentMithrilChain", "mithril_chain", "미스릴 사슬", "사슬 갑옷", Body, 10f, SoulMaterial.Mithril, 320, 0, 4, Ar("GuardianTunic"), Bonus(StatType.Armor, 6)),
            Gear("EquipmentWizardRobe", "wizard_robe", "마법사 로브", "천 로브", Body, 2f, SoulMaterial.Cloth, 60, 1, 1, Ar("BlueWizardTunic"), Bonus(StatType.Armor, 1), Bonus(StatType.MagicResist, .03f)),
            Gear("EquipmentSilkRobe", "silk_robe", "비단 로브", "천 로브", Body, 2f, SoulMaterial.Silk, 170, 3, 3, Ar("FireWizardRobe"), Bonus(StatType.Armor, 1), Bonus(StatType.MagicResist, .03f)),
            Gear("EquipmentPriestRobe", "priest_robe", "사제복", "성직자 예복", Body, 3f, SoulMaterial.Cloth, 90, 1, 2, Ar("Priest"), Bonus(StatType.Armor, 2), Bonus(StatType.Will, 1), Bonus(StatType.MagicResist, .04f)),
            // head
            Gear("EquipmentArcherHood", "archer_hood", "궁수 두건", "두건", Head, .5f, SoulMaterial.Cloth, 40, 1, 1, He("ArcherHood"), Bonus(StatType.Armor, 1)),
            Gear("EquipmentWizardHat", "wizard_hat", "마법사 모자", "마법사 모자", Head, .5f, SoulMaterial.Silk, 90, 2, 2, He("BlueWizzardHat"), Bonus(StatType.Magic, 1)),
            Gear("EquipmentIronHelmet", "iron_helmet", "철투구", "철투구", Head, 3f, SoulMaterial.Iron, 70, 1, 1, He("IronKnightHelmet"), Bonus(StatType.Armor, 3)),
            Gear("EquipmentVikingHelmet", "viking_helmet", "바이킹 투구", "대투구", Head, 5f, SoulMaterial.Iron, 130, 2, 2, He("VikingHelmet"), Bonus(StatType.Armor, 5), Bonus(StatType.KnockbackResist, .05f)),
            // accessories: blank — enchant only
            Gear("EquipmentCopperRing", "copper_ring", "구리 반지", "반지", Acc, .1f, M, 60, 1, 1, new SoulAppearancePatch()),
            Gear("EquipmentSilverNecklace", "silver_necklace", "은 목걸이", "목걸이", Acc, .2f, M, 80, 2, 1, new SoulAppearancePatch()),
            Gear("EquipmentBoneCharm", "bone_charm", "뼈 부적", "부적", Acc, .3f, M, 70, 1, 1, new SoulAppearancePatch()),
            Gear("EquipmentBracelet", "bracelet", "가죽 팔찌", "팔찌", Acc, .2f, M, 70, 2, 1, new SoulAppearancePatch()),
        };
        // named items (boss loot, drops): options as written, grade fixed
        all.Add(Unique(Weapon(Gear("EquipmentGuardianBlade", "guardian_blade", "수호자의 대검", "양손검", Main, 6.5f, SoulMaterial.Steel, 900, 0, 1, W("GiantBlade"),
            Bonus(StatType.Attack, 12), Bonus(StatType.Strength, 4), Bonus(StatType.SlashPower, 2)), "sword", true, slash), 5));
        all.Add(Unique(Gear("EquipmentRuinShield", "ruin_shield", "유적 방패", "카이트 방패", Off, 6f, SoulMaterial.Bronze, 400, 0, 1, Sh("AncientGreatShield"),
            Bonus(StatType.Armor, 5), Bonus(StatType.Durability, 3), Bonus(StatType.Vitality, 2), Bonus(StatType.MaxStamina, 20)), 3));
        all.Add(Unique(Gear("EquipmentAncientRing", "ancient_ring", "고대의 반지", "반지", Acc, .1f, M, 600, 0, 1, new SoulAppearancePatch(),
            Bonus(StatType.Luck, 3), Bonus(StatType.StaminaRegen, 1.5f)), 4));
        all.Add(Unique(Weapon(Gear("EquipmentHunterBow", "hunter_bow", "사냥꾼의 장궁", "장궁", Main, 2.5f, SoulMaterial.Wood, 400, 0, 1, W("BattleBow"),
            Bonus(StatType.Attack, 8), Bonus(StatType.Agility, 3), Bonus(StatType.PiercePower, 2)), "bow", true, bow, bow), 3));
        all.Add(Unique(Gear("EquipmentStaminaCharm", "stamina_charm", "지구력의 부적", "부적", Acc, .3f, M, 260, 0, 1, new SoulAppearancePatch(),
            Bonus(StatType.MaxStamina, 25), Bonus(StatType.StaminaRegen, 1)), 3));
        all.Add(Unique(Weapon(Gear("EquipmentFireSword", "fire_sword", "화염의 검", "한손검", Main, 3f, SoulMaterial.Steel, 700, 0, 3, W("FireSword"),
            Bonus(StatType.Attack, 8), Bonus(StatType.FireAttack, 4)), "sword", false, slash), 4));
        all.Add(Unique(Weapon(Gear("EquipmentStormStaff", "storm_staff", "폭풍 지팡이", "지팡이", Main, 2.5f, SoulMaterial.Spiritwood, 700, 0, 3, W("StormStaff"),
            Bonus(StatType.Magic, 3), Bonus(StatType.LightningAttack, 4), Bonus(StatType.Attack, 2)), "staff", true, swing), 4));
        all.Add(Unique(Weapon(Gear("EquipmentShadowDagger", "shadow_dagger", "그림자 단검", "단검", Main, 1.2f, SoulMaterial.Mithril, 700, 0, 3, W("MarderDagger"),
            Bonus(StatType.Attack, 6), Bonus(StatType.PiercePower, 2), Bonus(StatType.Evasion, .03f)), "dagger", false, thrust), 4));
        // the shop keeps the charm on its shelf (a named item, sold as written)
        all.Find(item => item.Id == "stamina_charm").ShopTier = 2;
        AssignItemIcons(all);
        return all;
    }

    const string ItemIconDir = "Assets/_project/3.Textures/Icons/SoulItems";

    // Icons made by scratch item_icons.py: <Folder>_<Part>.png from the look, kind_<x>.png for the rest.
    static void AssignItemIcons(List<SoulEquipmentData> items)
    {
        AssetDatabase.Refresh();
        foreach (string file in System.IO.Directory.GetFiles(ItemIconDir, "*.png"))
        {
            if (!(AssetImporter.GetAtPath(file.Replace('\\', '/')) is TextureImporter importer)) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ItemIconDir}/{name}.png");
        foreach (var item in items)
        {
            var look = item.Look;
            string kind = item.Kind == "반지" ? "kind_ring" : item.Kind == "목걸이" ? "kind_necklace" : item.Kind == "부적" ? "kind_charm"
                : item.Kind == "팔찌" ? "kind_bracelet" : item.Kind == "권갑" ? "kind_knuckle" : item.Kind == "마도서" ? "kind_grimoire" : item.Kind == "화살통" ? "kind_quiver" : item.Kind == "성물" ? "kind_relic" : null;
            item.Icon = !string.IsNullOrEmpty(look.Weapon) ? Icon("Weapon_" + look.Weapon) : !string.IsNullOrEmpty(look.Shield) ? Icon("Shield_" + look.Shield)
                : !string.IsNullOrEmpty(look.Armor) ? Icon("Armor_" + look.Armor) : !string.IsNullOrEmpty(look.Helmet) ? Icon("Helmet_" + look.Helmet)
                : kind != null ? Icon(kind) : null;
            EditorUtility.SetDirty(item);
        }
    }

    // ── village ──────────────────────────────────────────────

    static Sprite VillageSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{VillageArtDir}/{name}.png");

    // Pixel art (32 px a cell): single sprites, point filtered, uncompressed.
    static void VillageSpriteSettings()
    {
        AssetDatabase.Refresh();
        foreach (string file in System.IO.Directory.GetFiles(VillageArtDir, "*.png"))
        {
            string path = file.Replace('\\', '/');
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 32;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    // 비성향: what a job truly has no use for — no level-up point ever goes there.
    // How many skills a recruit comes with, by job and grade: casters live by their spells (마법사 the most).
    public static (int fewest, int most) KitRange(string job, SoulStyleRarity rarity)
    {
        int lift = job == "마법사" ? 1 : 0;
        if (rarity == SoulStyleRarity.Normal) return job == "마법사" ? (2, 3) : job == "성직자" || job == "소환사" ? (1, 2) : (0, 1);
        return rarity == SoulStyleRarity.Special ? (2 + lift, 3 + lift) : (3 + lift, 4 + lift);
    }

    // The summoned spirits' sheet (Resources/SoulSpirits.png, 48×40 cells: fire, frost, wind, light — two frames each).
    const string SpiritSheetPath = "Assets/Resources/SoulSpirits.png";
    static void ImportSpirits()
    {
        AssetDatabase.ImportAsset(SpiritSheetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(SpiritSheetPath) as TextureImporter;
        if (importer == null) throw new Exception("Missing " + SpiritSheetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 16;
        var looks = new[] { "fire", "frost", "wind", "light" };
        var metas = new List<SpriteMetaData>();
        for (int row = 0; row < looks.Length; row++)
            for (int frame = 0; frame < 2; frame++)
                metas.Add(new SpriteMetaData { name = $"{looks[row]}_{frame}", rect = new Rect(48 * frame, 40 * (looks.Length - 1 - row), 48, 40), alignment = (int)SpriteAlignment.BottomCenter });
#pragma warning disable CS0618 // a generated sheet: the plain importer list is enough
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
        importer.SaveAndReimport();
    }

    static StatType[] WeakGrowthOf(string job)
    {
        switch (job)
        {
            case "검사": return new[] { StatType.Magic, StatType.ImpactPower, StatType.PiercePower };
            case "수호자": return new[] { StatType.Magic, StatType.PiercePower, StatType.Luck };
            case "마법사": return new[] { StatType.Strength, StatType.SlashPower, StatType.ImpactPower, StatType.PiercePower };
            case "투사": return new[] { StatType.Magic, StatType.ImpactPower, StatType.PiercePower };
            case "길잡이": return new[] { StatType.Magic, StatType.SlashPower, StatType.ImpactPower };
            case "성직자": return new[] { StatType.SlashPower, StatType.ImpactPower, StatType.PiercePower };
            case "궁수": return new[] { StatType.Magic, StatType.SlashPower, StatType.ImpactPower };
            case "소환사": return new[] { StatType.Strength, StatType.SlashPower, StatType.ImpactPower, StatType.PiercePower };
            default: return new StatType[0];
        }
    }

    // The roster (용병 명단): every mercenary an asset of its own under Recruits/ — the two founders and the guild's
    // people, each job with a 일반, one or two 정예 and a 전설. A higher grade has more on top of its job (stats,
    // growth, patterns), its own trait (고유 특성), its own outfit under no armour and, for a 전설, a cape.
    static SoulRecruitData[] Recruits(SoulMercenaryData[] jobs, SoulMercenaryData priest)
    {
        if (!AssetDatabase.IsValidFolder(DataPath + "/Recruits")) AssetDatabase.CreateFolder(DataPath, "Recruits");
        SoulMercenaryData Job(string job) => job == "성직자" ? priest : System.Array.Find(jobs, j => j.Job == job);
        T[] Load<T>(params string[] files) where T : UnityEngine.Object
        {
            var found = new List<T>();
            foreach (string file in files) { var asset = AssetDatabase.LoadAssetAtPath<T>(DataPath + "/" + file + ".asset"); if (asset != null) found.Add(asset); }
            return found.ToArray();
        }
        SoulPatternData[] P(params string[] files) => Load<SoulPatternData>(files);
        SoulActiveSkillData[] A(params string[] files) => Load<SoulActiveSkillData>(files);
        SoulStatBonus[] S(params SoulStatBonus[] bonuses) => bonuses;
        SoulAppearancePatch Look(string hair, string outfit = null, string cape = null) => new SoulAppearancePatch { Hair = hair, Armor = outfit, Cape = cape };
        // 고유 특성: a passive of its own (the first of its passives)
        SoulPassiveSkillData Trait(string id, string name, string description, Action<SoulPassiveSkillData> more, params SoulStatBonus[] bonuses)
            => Asset("PassiveSig_" + id, (SoulPassiveSkillData item) =>
            {
                item.SoulId = "sig_" + id; item.SkillName = name; item.Description = description; item.AlwaysBonuses = bonuses;
                item.SoulEvent = SoulTrigger.Always; item.MapTraits = 0; item.Icon = IconSprite("passive:sig_" + id);
                more?.Invoke(item);
            });
        SoulPassiveSkillData[] With(SoulPassiveSkillData trait, params string[] files)
        {
            var list = new List<SoulPassiveSkillData> { trait };
            list.AddRange(Load<SoulPassiveSkillData>(files));
            return list.ToArray();
        }
        SoulRecruitData R(string id, string name, string job, SoulStyleRarity rarity, string title, string concept, StatType up, StatType down,
            SoulStatBonus[] stats, SoulStatBonus[] growth, SoulPatternData[] patterns, SoulActiveSkillData[] actives, SoulPassiveSkillData[] passives, SoulAppearancePatch look, bool founder = false)
            => Asset("Recruits/Recruit_" + id, (SoulRecruitData item) =>
            {
                item.Id = id; item.DisplayName = name; item.Template = Job(job); item.Rarity = rarity; item.Founder = founder;
                item.Title = title; item.Concept = concept; item.Up = up; item.Down = down;
                item.Stats = stats ?? new SoulStatBonus[0]; item.Growth = growth ?? new SoulStatBonus[0];
                item.Patterns = patterns ?? new SoulPatternData[0]; item.Actives = actives ?? new SoulActiveSkillData[0];
                item.Passives = passives ?? new SoulPassiveSkillData[0]; item.Look = look;
            });
        const StatType Str = StatType.Strength, Vit = StatType.Vitality, Agi = StatType.Agility, Mag = StatType.Magic, Wil = StatType.Will, Luk = StatType.Luck;
        const SoulStyleRarity N = SoulStyleRarity.Normal, Elite = SoulStyleRarity.Special, Legend = SoulStyleRarity.Rare;

        // ── the rest of the roster (용병 100여 명): each with a race of its own, a look drawn from its id, a trait ──
        SoulRaceData RaceOf(string race) => AssetDatabase.LoadAssetAtPath<SoulRaceData>(DataPath + "/" + (race == "드워프" ? "RaceDwarf" : race == "엘프" ? "RaceElf" : race == "수인" ? "RaceBeastkin" : "RaceHuman") + ".asset");
        string[] OutfitsOf(string job) => job switch
        {
            "검사" => new[] { "MilitiamanArmor", "LegionaryArmor", "CrossKnight", "TournamentArmor", "BlueKnight", "CaptainArmor" },
            "수호자" => new[] { "IronKnight", "HeavyKnightArmor", "GuardianTunic", "Spartan", "CaptainArmor", "DwarfTunic" },
            "마법사" => new[] { "BlueWizardTunic", "FireWizardRobe", "NecromancerRobe", "DruidRobe", "DeathRobe", "DarkNecromant" },
            "투사" => new[] { "Gladiator", "BanditTunic", "Chief", "Squaw", "HornsKnight", "ThiefTunic" },
            "길잡이" => new[] { "ArcherTunic", "ThiefTunic", "MusketeerTunic", "NinjaTunic", "Arabian", "PirateCostume" },
            "궁수" => new[] { "ArcherTunic", "GreenElfTunic", "MusketeerTunic", "ShinobuTunic", "Link", "CavalrymanArmor" },
            "소환사" => new[] { "DruidRobe", "NecromancerRobe", "BlueWizardTunic", "GreenElfTunic", "DeathRobe", "Arabian" },
            _ => new[] { "ClericRobe", "Priest", "MonkRobe", "Angel", "DruidRobe", "DemigodArmour" },
        };
        string[] hairColours = { "2B2522", "5D2C28", "8A4836", "A5672F", "C64524", "D8B25A", "E6E0C8", "7C7C84", "3A3F6E", "4E6E3A", "B8C8F8", "E07090" };
        string[] capeColours = { "8A1C1C", "2E3E6E", "5A3A9A", "2A6A4A", "6A3A1A", "F0E8D0", "1A1A1A" };
        SoulAppearancePatch AutoLook(string id, SoulStyleRarity rarity, string job)
        {
            int h = 17; foreach (char c in id) h = h * 31 + c; h &= int.MaxValue;
            string hair = $"Hair{1 + h % 15}#{hairColours[(h / 15) % hairColours.Length]}";
            var outfits = OutfitsOf(job);
            string outfit = rarity == SoulStyleRarity.Normal ? null : outfits[(h / 7) % outfits.Length];
            string cape = rarity == SoulStyleRarity.Rare ? $"Cape#{capeColours[(h / 5) % capeColours.Length]}" : null;
            return Look(hair, outfit, cape);
        }
        SoulRecruitData X(string id, string name, string job, string race, SoulStyleRarity rarity, string title, string concept, StatType up, StatType down,
            SoulStatBonus[] stats, SoulStatBonus[] growth, SoulPatternData[] patterns, SoulActiveSkillData[] actives, SoulPassiveSkillData[] passives, int price)
        {
            var recruit = R(id, name, job, rarity, title, concept, up, down, stats, growth, patterns, actives, passives, AutoLook(id, rarity, job));
            recruit.Race = RaceOf(race);
            recruit.PriceShift = price;
            EditorUtility.SetDirty(recruit);
            return recruit;
        }
        const SoulStyleRarity Elite2 = SoulStyleRarity.Special, Legend2 = SoulStyleRarity.Rare;

        var roster = new[]
        {
            // 창단 멤버: the company's first two (the job as it is)
            R("ria", "리아", "검사", N, null, "용병단을 함께 세운 검사입니다.", Str, Str, null, null, null, null, null, default, true),
            R("thor", "토르", "수호자", N, null, "용병단을 함께 세운 드워프 수호자입니다.", Str, Str, null, null, null, null, null, default, true),

            // 검사
            R("hans", "한스", "검사", N, null, "기본기에 충실한 검사입니다.", Vit, Luk, null, null, null, null, null, Look("Hair1#5D3A22")),
            R("serin", "세린", "검사", Elite, "검무사", "구르며 베는 기교파. 날렵하지만 힘은 조금 약합니다.", Luk, Wil,
                S(Bonus(Agi, 4), Bonus(Wil, 1), Bonus(Str, -1)), S(Bonus(Agi, 2)), P("Roll", "RollSlash"), null,
                With(Trait("serin", "칼춤", "공격이 15% 확률로 두 번 들어갑니다.", null, Bonus(StatType.DoubleHit, .15f))),
                Look("Hair11#2B2522", "ShinobuTunic")),
            R("leon", "레온", "검사", Legend, "검성의 제자", "빠르고 정확한 연격, 흔들림 없는 마음. 타고난 검재입니다.", Str, Luk,
                S(Bonus(Str, 3), Bonus(Agi, 3), Bonus(Wil, 3), Bonus(Vit, 1)), S(Bonus(Str, 2), Bonus(Agi, 1), Bonus(Wil, 1)), P("DoubleThrust", "EvadeFollowup"), A("SkillHeavenBlade"),
                With(Trait("leon", "검성의 경지", "공격이 20% 확률로 두 번 들어가고, 대상 방어력의 20%를 무시하며, 명중이 8% 오릅니다.", null,
                    Bonus(StatType.DoubleHit, .2f), Bonus(StatType.ArmorShred, .2f), Bonus(StatType.Accuracy, .08f)), "PassiveEffortless"),
                Look("Hair3#E6E0C8", "Samurai", "Cape#8A1C1C")),

            // 수호자
            R("bram", "브람", "수호자", N, null, "묵묵히 앞을 지키는 수호자입니다.", Str, Agi, null, null, null, null, null, Look("Hair5#7C7C84")),
            R("doyun", "도윤", "수호자", Elite, "방패 수도사", "동료를 가리고 북돋습니다. 공격은 약합니다.", Vit, Agi,
                S(Bonus(Wil, 3), Bonus(Vit, 2), Bonus(Str, -1)), S(Bonus(Wil, 2)), P("Cover", "Encourage"), null,
                With(Trait("doyun", "수호 서약", "부상이 쌓이는 속도가 30% 느리고, 넉백 저항이 40% 오릅니다.", null,
                    Bonus(StatType.WoundResist, .3f), Bonus(StatType.KnockbackResist, .4f))),
                Look("Hair3#5D2C28", "MonkRobe")),
            R("baron", "바론", "수호자", Legend, "불굴의 요새", "쓰러지지 않는 벽. 돌 같은 피부를 타고났습니다.", Wil, Agi,
                S(Bonus(Vit, 5), Bonus(Wil, 3), Bonus(StatType.Durability, 3), Bonus(Str, 1)), S(Bonus(Vit, 2), Bonus(Wil, 1)), P("GuardThrust", "Taunt"), A("SkillBastion"),
                With(Trait("baron", "불굴", "한 층에 한 번, 쓰러질 피해를 HP 1로 버팁니다. 근접 피격 피해의 20%를 되돌려주고, 부상이 40% 느리게 쌓입니다.", null,
                    Bonus(StatType.DeathDefy, 1), Bonus(StatType.Thorns, .2f), Bonus(StatType.WoundResist, .4f)), "PassiveStoneSkin"),
                Look("Hair5#D8D8D8", "IronKnight", "Cape#2E3E6E")),

            // 마법사
            R("organ", "오르간", "마법사", N, null, "배운 대로 주문을 외는 마법사입니다.", Mag, Str, null, null, null, null, null, Look("Hair7#A5672F")),
            R("bella", "벨라", "마법사", Elite, "화염술사", "강한 마력을 거칠게 씁니다. 몸이 약합니다.", Agi, Wil,
                S(Bonus(Mag, 4), Bonus(Agi, 1), Bonus(Vit, -1)), S(Bonus(Mag, 2)), P("ManaBreath"), A("SkillFireBolt"),
                With(Trait("bella", "불꽃 혈통", "MP를 쓰는 스킬의 효과가 15% 오르고, 공격에 화염 피해가 더해집니다.", null,
                    Bonus(StatType.SpellPower, .15f), Bonus(StatType.FireAttack, 3))),
                Look("Hair10#C64524", "FireWizardRobe")),
            R("isolde", "이솔데", "마법사", Legend, "대마도사의 후계", "타고난 마력. 마력탄과 냉기를 자유롭게 다룹니다.", Wil, Vit,
                S(Bonus(Mag, 5), Bonus(Wil, 3), Bonus(StatType.Recovery, 2)), S(Bonus(Mag, 2), Bonus(Wil, 1)), P("ManaBreath"), A("SkillManaBolt", "SkillFrostShard", "SkillMeteor"),
                With(Trait("isolde", "마력의 원천", "MP를 쓰는 스킬의 효과가 30% 오르고, 스킬이 25% 확률로 MP·스태미나를 쓰지 않습니다.", null,
                    Bonus(StatType.SpellPower, .3f), Bonus(StatType.FreeSkillChance, .25f)), "PassiveBattleBreath"),
                Look("Hair12#B8C8F8", "DemigodArmour", "Cape#5A3A9A")),

            // 투사
            R("kai", "카이", "투사", N, null, "본능대로 싸우는 투사입니다.", Agi, Wil, null, null, null, null, null, Look("Hair8#2B2522")),
            R("mira", "미라", "투사", Elite, "광전사", "피를 볼수록 강해집니다. 물러설 줄을 모릅니다.", Vit, Luk,
                S(Bonus(Str, 3), Bonus(Vit, 2), Bonus(Wil, -1)), S(Bonus(Str, 2)), P("Rush"), null,
                With(Trait("mira", "피의 갈증", "준 피해의 5%를 HP로 회복하고, 적을 쓰러뜨리면 스태미나를 10% 회복합니다.", null,
                    Bonus(StatType.LifeSteal, .05f), Bonus(StatType.KillStamina, .1f)), "PassiveAdrenaline"),
                Look("Hair13#8A1A1A", "Gladiator")),
            R("asha", "아샤", "투사", Elite, "사냥꾼", "약한 곳을 노리는 추적자. 힘보다 눈입니다.", Luk, Vit,
                S(Bonus(Agi, 3), Bonus(Luk, 2), Bonus(Str, -1)), S(Bonus(Agi, 2)), P("AnkleStrike"), null,
                With(Trait("asha", "급소 사냥", "HP 35% 미만 적에게 근접 피해가 30%, 엘리트·보스에게 피해가 10% 늘어납니다.", null,
                    Bonus(StatType.ExecuteBonus, .3f), Bonus(StatType.EliteDamage, .1f)), "PassiveTracking"),
                Look("Hair14#D8B25A", "ThiefTunic")),
            R("ragna", "라그나", "투사", Legend, "무리의 우두머리", "타고난 포식자. 연타를 마무리할 줄 압니다.", Str, Mag,
                S(Bonus(Str, 4), Bonus(Agi, 4), Bonus(Vit, 2)), S(Bonus(Str, 1), Bonus(Agi, 1), Bonus(Vit, 1)), P("ComboFinish", "Rush"), A("SkillPackCall"),
                With(Trait("ragna", "포식자", "준 피해의 6%를 HP로, 적을 쓰러뜨리면 스태미나 15%를 회복하고, HP 35% 미만 적에게 근접 피해가 35% 늘어납니다.", null,
                    Bonus(StatType.LifeSteal, .06f), Bonus(StatType.KillStamina, .15f), Bonus(StatType.ExecuteBonus, .35f)), "PassiveAdrenaline"),
                Look("Hair15#1A1A1A", "Chief", "Cape#6A3A1A")),

            // 길잡이
            R("teo", "테오", "길잡이", N, null, "길은 잘 알지만 싸움은 서툽니다.", Agi, Str, null, null, null, null, null, Look("Hair4#8A4836")),
            R("rin", "린", "길잡이", Elite, "명사수", "활도 쏠 줄 아는 길잡이. 운은 조금 덜합니다.", Agi, Wil,
                S(Bonus(Agi, 4), Bonus(Wil, 1), Bonus(StatType.PiercePower, 2), Bonus(Luk, -1)), S(Bonus(Agi, 2)), P("Kite"), null,
                With(Trait("rin", "매의 눈", "대상 방어력의 15%를 무시하고, 명중이 5% 오릅니다.", null,
                    Bonus(StatType.ArmorShred, .15f), Bonus(StatType.Accuracy, .05f))),
                Look("Hair9#2B2522", "MusketeerTunic")),
            R("yuna", "유나", "길잡이", Elite, "보물 사냥꾼", "숨은 것을 귀신같이 찾습니다. 몸은 약합니다.", Luk, Str,
                S(Bonus(Luk, 4), Bonus(Agi, 1), Bonus(Vit, -1)), S(Bonus(Luk, 2)), null, null,
                With(Trait("yuna", "보물 코", "몬스터 금화가 25%, 장비 드롭 확률이 20% 오릅니다 (파티에서 가장 높은 것만).", null,
                    Bonus(StatType.GoldFind, .25f), Bonus(StatType.LootFind, .2f))),
                Look("Hair2#E0B040", "PirateCostume")),
            R("ethan", "에단", "길잡이", Legend, "전설의 탐험가", "지치지 않는 발걸음. 어떤 길도 두렵지 않습니다.", Agi, Str,
                S(Bonus(Luk, 4), Bonus(Agi, 3), Bonus(Wil, 2), Bonus(Vit, 1)), S(Bonus(Luk, 1), Bonus(Agi, 1), Bonus(Wil, 1)), P("KiteMaster", "SecondWind"), A("SkillShortcut"),
                With(Trait("ethan", "전설의 발걸음", "숨은 보물 상자를 찾아내고, 장비 드롭 확률이 20%, 영혼 드롭 확률이 15% 오릅니다.",
                    t => t.MapTraits = SoulMapTrait.RevealTreasure, Bonus(StatType.LootFind, .2f), Bonus(StatType.SoulFind, .15f)), "PassiveTireless"),
                Look("Hair4#A07040", "Arabian", "Cape#2A6A4A")),

            // 성직자 (the church)
            R("mariel", "마리엘", "성직자", N, null, "기도와 치유를 배운 사제입니다.", Wil, Str, null, null, null, null, null, Look("Hair6#E6E0C8")),
            R("rosa", "로사", "성직자", Elite, "치유사", "치유에 전념합니다. 싸움은 멀리합니다.", Mag, Agi,
                S(Bonus(Wil, 3), Bonus(Mag, 2), Bonus(Str, -1)), S(Bonus(Wil, 2)), P("FirstAid"), null,
                With(Trait("rosa", "자애의 손", "스킬이 15% 확률로 MP·스태미나를 쓰지 않고, MP를 쓰는 스킬의 효과가 10% 오릅니다.", null,
                    Bonus(StatType.FreeSkillChance, .15f), Bonus(StatType.SpellPower, .1f))),
                Look("Hair12#E07090", "ClericRobe")),
            R("elena", "엘레나", "성직자", Legend, "성녀", "기적을 부르는 신앙. 성스러운 손길을 씁니다.", Wil, Vit,
                S(Bonus(Wil, 5), Bonus(Mag, 4), Bonus(Vit, 1)), S(Bonus(Wil, 2), Bonus(Mag, 1)), null, A("SkillHolyTouch", "SkillMiracle"),
                With(Trait("elena", "성녀의 가호", "한 층에 한 번, 쓰러질 피해를 HP 1로 버팁니다. MP를 쓰는 스킬의 효과가 25% 오르고, 부상이 30% 느리게 쌓입니다.", null,
                    Bonus(StatType.DeathDefy, 1), Bonus(StatType.SpellPower, .25f), Bonus(StatType.WoundResist, .3f))),
                Look("Hair3#F0E0A0", "Priest", "Cape#F0E8D0")),

            X("gareth", "가렛", "검사", "인간", N, "늦깎이 검사", "뒤늦게 검을 잡았지만 누구보다 빨리 배웁니다.", Wil, Luk,
                null, null, null, null,
                With(Trait("gareth", "늦깎이", "경험치를 15% 더 얻습니다.", null, Bonus(StatType.ExpGainBonus, 0.15f))), 0),
            X("owen", "오웬", "검사", "인간", N, "겁 많은 신참", "무섭지만 피하는 법만큼은 빨리 익혔습니다.", Agi, Wil,
                null, null, null, null,
                With(Trait("owen", "살고 싶다", "회피가 8% 오르지만 공포 저항이 20% 낮습니다.", null, Bonus(StatType.Evasion, 0.08f), Bonus(StatType.FearResist, -0.2f))), 0),
            X("marta", "마르타", "검사", "인간", N, "대장장이 딸", "손질이 잘된 장비를 아낍니다.", Vit, Mag,
                null, null, null, null,
                With(Trait("marta", "손질된 장비", "방어력이 3, 내구도가 2 오릅니다.", null, Bonus(StatType.Armor, 3f), Bonus(StatType.Durability, 2f))), 0),
            X("fien", "피엔", "검사", "엘프", N, "가벼운 검", "날렵하게 찌르고 빠집니다.", Agi, Str,
                null, null, null, null,
                null, 0),
            X("duran", "두란", "검사", "드워프", N, "검 든 드워프", "도끼 대신 검을 고른 고집쟁이.", Vit, Agi,
                null, null, null, null,
                null, 0),
            X("jude", "쥬드", "검사", "인간", N, "도박꾼", "크게 걸고 크게 잃습니다.", Luk, Wil,
                null, null, null, null,
                With(Trait("jude", "한 판 승부", "치명타 확률이 8% 오르고 명중이 5% 낮아집니다.", null, Bonus(StatType.CritChance, 0.08f), Bonus(StatType.Accuracy, -0.05f))), 0),
            X("camilla", "카밀라", "검사", "인간", Elite2, "쌍검사", "두 자루 검으로 쉴 새 없이 벱니다. 방어는 허술합니다.", Agi, Vit,
                S(Bonus(Agi, 3f), Bonus(Str, 2f), Bonus(Vit, -1f)), S(Bonus(Agi, 2f)), P("DoubleThrust"), null,
                With(Trait("camilla", "쌍검", "공격이 18% 확률로 두 번 들어가지만 방어력이 3 낮습니다.", null, Bonus(StatType.DoubleHit, 0.18f), Bonus(StatType.Armor, -3f))), 0),
            X("volk", "볼크", "검사", "인간", Elite2, "광검사", "벨수록 미쳐 갑니다.", Str, Wil,
                S(Bonus(Str, 4f), Bonus(Vit, 1f), Bonus(Wil, -1f)), S(Bonus(Str, 2f)), null, null,
                With(Trait("volk", "피의 광기", "HP가 낮을수록 피해가 최대 40%까지 늘어납니다.", null, Bonus(StatType.LowHpDamage, 0.4f)), "PassiveAdrenaline"), 0),
            X("ian", "이안", "검사", "엘프", Elite2, "바람검", "바람처럼 치고 빠집니다.", Agi, Vit,
                S(Bonus(Agi, 4f), Bonus(Luk, 1f), Bonus(Str, -1f)), S(Bonus(Agi, 2f)), P("Kite"), null,
                With(Trait("ian", "바람걸음", "이동 속도가 15%, 회피가 5% 오릅니다.", null, Bonus(StatType.MoveSpeed, 0.15f), Bonus(StatType.Evasion, 0.05f))), 0),
            X("hargen", "하르겐", "검사", "드워프", Elite2, "파쇄검", "갑옷째 쪼갭니다.", Str, Agi,
                S(Bonus(Str, 3f), Bonus(Vit, 2f), Bonus(StatType.ImpactPower, 2f), Bonus(Agi, -1f)), S(Bonus(Str, 2f)), null, null,
                With(Trait("hargen", "파쇄", "대상 방어력의 25%를 무시합니다.", null, Bonus(StatType.ArmorShred, 0.25f))), 0),
            X("seira", "세이라", "검사", "인간", Elite2, "결투가", "일대일이라면 누구에게도 지지 않습니다.", Agi, Luk,
                S(Bonus(Agi, 3f), Bonus(Str, 2f), Bonus(Luk, -1f)), S(Bonus(Agi, 1f), Bonus(Str, 1f)), P("Counter"), null,
                With(Trait("seira", "결투", "4칸 안에 적이 하나뿐이면 공격력이 30% 오릅니다.", null, Bonus(StatType.DuelFocus, 0.3f))), 0),
            X("rohan", "로한", "검사", "인간", Elite2, "퇴역 대장", "전장을 아는 목소리가 동료를 움직입니다.", Wil, Agi,
                S(Bonus(Wil, 3f), Bonus(Str, 2f), Bonus(Agi, -1f)), S(Bonus(Wil, 2f)), null, A("SkillWarCry"),
                With(Trait("rohan", "노련한 지휘", "위협이 50%, 공포 저항이 30% 오릅니다.", null, Bonus(StatType.Threat, 0.5f), Bonus(StatType.FearResist, 0.3f))), 0),
            X("arte", "아르테", "검사", "엘프", Legend2, "월광검", "빛 없는 곳에서 더 날카로워지는 검.", Agi, Vit,
                S(Bonus(Agi, 4f), Bonus(Str, 3f), Bonus(Wil, 2f), Bonus(Luk, 1f)), S(Bonus(Agi, 2f), Bonus(Str, 1f), Bonus(Wil, 1f)), P("RollSlash", "Roll"), A("SkillIssen"),
                With(Trait("arte", "월광", "어두운 층에서 명중이 10%, 치명타 확률이 15% 오르고, 치명타 피해가 30% 늘어납니다.", null, Bonus(StatType.DarkSight, 1f), Bonus(StatType.CritDamage, 0.3f)), "PassiveEffortless"), 0),
            X("gail", "가일", "검사", "인간", Legend2, "불사의 검", "죽음의 문턱에서 돌아올 때마다 더 강해집니다.", Vit, Luk,
                S(Bonus(Str, 4f), Bonus(Vit, 4f), Bonus(Wil, 2f)), S(Bonus(Str, 1f), Bonus(Vit, 2f)), P("Whirl"), A("SkillExecution"),
                With(Trait("gail", "불사", "한 층에 한 번 쓰러질 피해를 버티고, 그 뒤 10초간 광폭해집니다(행동 속도·흡혈↑).", null, Bonus(StatType.DeathDefy, 1f), Bonus(StatType.DefyRage, 10f))), 0),
            X("gordon", "고든", "수호자", "드워프", N, "문지기", "한 걸음도 물러서지 않습니다.", Vit, Agi,
                null, null, null, null,
                With(Trait("gordon", "버티기", "넉백 저항이 40%, 다운 저항이 20% 오릅니다.", null, Bonus(StatType.KnockbackResist, 0.4f), Bonus(StatType.DownResist, 0.2f))), 0),
            X("hanna", "한나", "수호자", "인간", N, "방패 견습", "막는 법부터 배웠습니다.", Wil, Str,
                null, null, null, null,
                With(Trait("hanna", "막기 연습", "패턴 비용이 10% 줄어듭니다.", null, Bonus(StatType.PatternCostReduce, 0.1f))), 0),
            X("musoe", "무쇠", "수호자", "드워프", N, "광부 출신", "광맥 냄새를 맡습니다.", Str, Mag,
                null, null, null, null,
                Load<SoulPassiveSkillData>("PassiveOreSense"), 0),
            X("toby", "토비", "수호자", "인간", N, "짐꾼", "짐을 잔뜩 지고도 지치지 않습니다.", Vit, Luk,
                null, null, null, null,
                With(Trait("toby", "짐꾼", "가방 포션의 회복량이 25% 늘고, 최대 스태미나가 10 오릅니다.", null, Bonus(StatType.PotionPower, 0.25f), Bonus(StatType.MaxStamina, 10f))), 0),
            X("edgar", "에드가", "수호자", "인간", N, "늙은 병사", "몸은 굼떠도 마음은 흔들리지 않습니다.", Wil, Agi,
                null, S(Bonus(Wil, 1f)), null, null,
                With(Trait("edgar", "노병", "공포 저항이 30% 오르지만 이동이 10% 느립니다.", null, Bonus(StatType.FearResist, 0.3f), Bonus(StatType.MoveSpeed, -0.1f))), 0),
            X("brunhild", "브룬힐트", "수호자", "드워프", Elite2, "방패벽", "동료 앞에 서면 벽이 됩니다.", Vit, Agi,
                S(Bonus(Vit, 3f), Bonus(Wil, 2f), Bonus(Agi, -1f)), S(Bonus(Vit, 2f)), null, A("SkillGuardianCry"),
                With(Trait("brunhild", "방패벽", "방어력이 6, 넉백 저항이 30% 오릅니다.", null, Bonus(StatType.Armor, 6f), Bonus(StatType.KnockbackResist, 0.3f))), 0),
            X("greta", "그레타", "수호자", "인간", Elite2, "가시 방패", "때리는 쪽이 더 아픕니다.", Vit, Luk,
                S(Bonus(Vit, 3f), Bonus(Str, 2f), Bonus(StatType.Durability, 2f), Bonus(Luk, -1f)), S(Bonus(Vit, 2f)), null, null,
                With(Trait("greta", "가시", "근접 피격 피해의 25%를 되돌려줍니다.", null, Bonus(StatType.Thorns, 0.25f))), 0),
            X("olaf", "올라프", "수호자", "드워프", Elite2, "돌격 방패", "방패째 들이받아 쓰러뜨립니다.", Str, Wil,
                S(Bonus(Str, 3f), Bonus(Vit, 2f), Bonus(Wil, -1f)), S(Bonus(Str, 2f)), P("Charge", "Kick"), null,
                With(Trait("olaf", "돌파", "넉백 저항·스턴 내성이 30% 오르고 패턴 비용이 10% 줄어듭니다.", null, Bonus(StatType.KnockbackResist, 0.3f), Bonus(StatType.StunResist, 0.3f), Bonus(StatType.PatternCostReduce, 0.1f))), 0),
            X("sebastian", "세바스찬", "수호자", "인간", Elite2, "기사", "맹세한 대로 맨 앞에 섭니다.", Wil, Luk,
                S(Bonus(Vit, 3f), Bonus(Wil, 2f), Bonus(Luk, -1f)), S(Bonus(Vit, 1f), Bonus(Wil, 1f)), null, A("SkillIronWall"),
                With(Trait("sebastian", "기사도", "부상이 30% 느리게 쌓이고 위협이 50% 오릅니다.", null, Bonus(StatType.WoundResist, 0.3f), Bonus(StatType.Threat, 0.5f))), 0),
            X("lumen", "루멘", "수호자", "엘프", Elite2, "결계 수호자", "마법을 막는 결계를 두릅니다.", Mag, Str,
                S(Bonus(Wil, 3f), Bonus(Mag, 2f), Bonus(Str, -1f)), S(Bonus(Wil, 2f)), null, A("SkillManaShield"),
                With(Trait("lumen", "결계", "마법 저항이 20%, 마법 반사가 15% 오릅니다.", null, Bonus(StatType.MagicResist, 0.2f), Bonus(StatType.SpellReflect, 0.15f))), 0),
            X("gor", "고르", "수호자", "드워프", Legend2, "산의 심장", "산이 무너지지 않듯 쓰러지지 않습니다.", Vit, Agi,
                S(Bonus(Vit, 5f), Bonus(Str, 2f), Bonus(Wil, 2f), Bonus(StatType.Durability, 3f)), S(Bonus(Vit, 2f), Bonus(Wil, 1f)), P("GuardThrust"), A("SoulSkillStatue", "SkillIronWall"),
                With(Trait("gor", "산의 심장", "석화에 걸리지 않고, 다운 저항·넉백 저항이 최대가 됩니다.", null, Bonus(StatType.PetrifyResist, 1f), Bonus(StatType.DownResist, 0.75f), Bonus(StatType.KnockbackResist, 0.9f)), "PassiveStoneSkin"), 0),
            X("aegis", "아이기스", "수호자", "인간", Legend2, "성벽의 맹세", "곁의 동료가 받는 상처를 늘 나눠 짊어집니다.", Wil, Luk,
                S(Bonus(Vit, 4f), Bonus(Wil, 4f), Bonus(Str, 1f)), S(Bonus(Vit, 1f), Bonus(Wil, 2f)), null, A("SkillGuardianCry"),
                With(Trait("aegis", "성벽의 맹세", "5칸 안의 동료가 받는 피해의 20%를 늘 대신 받고, 부상이 30% 느리게 쌓입니다.", null, Bonus(StatType.GuardAura, 0.2f), Bonus(StatType.WoundResist, 0.3f))), 0),
            X("nate", "네이트", "마법사", "인간", N, "서생", "책이라면 금세 읽어 냅니다.", Mag, Vit,
                null, null, null, null,
                With(Trait("nate", "독서가", "서고에서 스킬북을 쓸 때 30% 확률로 책이 남습니다.", null, Bonus(StatType.BookDiscount, 0.3f))), 0),
            X("alma", "알마", "마법사", "엘프", N, "약초 마법사", "상처엔 약초가 최고라 믿습니다.", Wil, Str,
                null, null, null, null,
                With(Trait("alma", "약초 지식", "HP 재생이 1 오르고 회복력이 2 오릅니다.", null, Bonus(StatType.HpRegen, 1f), Bonus(StatType.Recovery, 2f))), 0),
            X("pipi", "피피", "마법사", "엘프", N, "불장난꾼", "불이라면 뭐든 좋아합니다.", Mag, Wil,
                null, null, null, null,
                With(Trait("pipi", "불장난", "불 계열 스킬의 효과가 25% 오릅니다.", null, Bonus(StatType.FireFocus, 0.25f))), 0),
            X("oscar", "오스카", "마법사", "인간", N, "명상가 지망생", "숨을 고르면 마력이 차오릅니다.", Wil, Str,
                null, null, null, null,
                With(Trait("oscar", "고요한 숨", "MP 재생이 0.15 오릅니다.", null, Bonus(StatType.MpRegen, 0.15f))), 0),
            X("lucy", "루시", "마법사", "인간", N, "병약한 천재", "몸은 약해도 마력은 넘칩니다.", Mag, Vit,
                null, null, null, null,
                With(Trait("lucy", "넘치는 마력", "최대 MP가 20 오르고 최대 HP가 10 줄어듭니다.", null, Bonus(StatType.MaxMp, 20f), Bonus(StatType.MaxHp, -10f))), 0),
            X("aidan", "에이단", "마법사", "엘프", Elite2, "빙결술사", "숨결마저 얼어붙습니다.", Mag, Vit,
                S(Bonus(Mag, 3f), Bonus(Wil, 2f), Bonus(Vit, -1f)), S(Bonus(Mag, 2f)), null, A("SkillFrostPrison", "SkillBlizzard"),
                With(Trait("aidan", "한기", "공격에 냉기가 더해지고 거는 상태 이상이 한 단계 강해집니다.", null, Bonus(StatType.ColdAttack, 3f), Bonus(StatType.StatusGrade, 1f))), 0),
            X("zahara", "자하라", "마법사", "인간", Elite2, "번개술사", "번개가 한 번 더 튕깁니다.", Mag, Wil,
                S(Bonus(Mag, 4f), Bonus(Agi, 1f), Bonus(Wil, -1f)), S(Bonus(Mag, 2f)), null, A("SkillChainLightning"),
                With(Trait("zahara", "뇌운", "연쇄 스킬이 한 번 더 튕기고 공격에 번개가 더해집니다.", null, Bonus(StatType.ChainBonus, 1f), Bonus(StatType.LightningAttack, 3f))), 0),
            X("morgan", "모르간", "마법사", "인간", Elite2, "저주술사", "저주가 뼛속까지 스며듭니다.", Wil, Vit,
                S(Bonus(Mag, 3f), Bonus(Wil, 2f), Bonus(Vit, -1f)), S(Bonus(Wil, 2f)), null, A("SkillCurseWeakness"),
                With(Trait("morgan", "깊은 저주", "거는 상태 이상이 한 단계 강해지고 MP를 쓰는 스킬의 효과가 10% 오릅니다.", null, Bonus(StatType.StatusGrade, 1f), Bonus(StatType.SpellPower, 0.1f))), 0),
            X("sylvia", "실비아", "마법사", "엘프", Elite2, "환영술사", "어디에 있는지 알 수 없습니다.", Agi, Str,
                S(Bonus(Mag, 3f), Bonus(Agi, 2f), Bonus(Str, -1f)), S(Bonus(Agi, 1f), Bonus(Mag, 1f)), null, A("SkillConfusion"),
                With(Trait("sylvia", "환영", "회피가 15% 오릅니다.", null, Bonus(StatType.Evasion, 0.15f))), 0),
            X("theron", "테론", "마법사", "인간", Elite2, "전투 마법사", "검을 쥐고도 주문을 욉니다.", Str, Luk,
                S(Bonus(Mag, 3f), Bonus(Str, 2f), Bonus(Luk, -1f)), S(Bonus(Mag, 1f), Bonus(Str, 1f)), P("Slash"), null,
                With(Trait("theron", "검과 주문", "지팡이가 아닌 무기를 들어도 마법 효과가 40% 덜 줄어듭니다.", null, Bonus(StatType.BattleCaster, 0.4f))), 0),
            X("astaro", "아스타로", "마법사", "엘프", Legend2, "시간술사", "흐르는 시간을 조금 앞당깁니다.", Wil, Str,
                S(Bonus(Mag, 4f), Bonus(Wil, 4f), Bonus(Agi, 2f)), S(Bonus(Mag, 1f), Bonus(Wil, 2f)), null, A("SkillHaste"),
                With(Trait("astaro", "시간 가속", "모든 스킬의 재사용 대기가 25% 줄고 행동이 10% 빨라집니다.", null, Bonus(StatType.SkillCooldownReduce, 0.25f), Bonus(StatType.ActionSpeed, 0.1f)), "PassiveBattleBreath"), 0),
            X("belian", "벨리안", "마법사", "인간", Legend2, "흑마도사", "죽음에서 마력을 거둡니다.", Mag, Luk,
                S(Bonus(Mag, 5f), Bonus(Wil, 3f), Bonus(Vit, 1f)), S(Bonus(Mag, 2f), Bonus(Wil, 1f)), null, A("SkillCurseWeakness", "SoulSkillLifeDrain"),
                With(Trait("belian", "죽음의 수확", "적을 쓰러뜨리면 최대 MP의 12%를 회복하고 영혼 드롭 확률이 15% 오릅니다.", null, Bonus(StatType.KillMana, 0.12f), Bonus(StatType.SoulFind, 0.15f))), 0),
            X("nova", "노바", "마법사", "엘프", Legend2, "별의 목소리", "하늘의 마력을 끌어옵니다.", Mag, Vit,
                S(Bonus(Mag, 5f), Bonus(Wil, 3f), Bonus(Luk, 2f)), S(Bonus(Mag, 2f), Bonus(Luk, 1f)), null, A("SkillArcaneBurst", "SkillChainLightning"),
                With(Trait("nova", "별빛", "최대 MP가 30, MP를 쓰는 스킬의 효과가 20% 오릅니다.", null, Bonus(StatType.MaxMp, 30f), Bonus(StatType.SpellPower, 0.2f))), 0),
            X("bor", "보르", "투사", "수인", N, "싸움꾼", "지치지 않고 싸웁니다.", Vit, Mag,
                null, null, null, null,
                With(Trait("bor", "투지", "최대 스태미나가 20 오릅니다.", null, Bonus(StatType.MaxStamina, 20f))), 0),
            X("ruru", "루루", "투사", "수인", N, "날쌘 발", "누구보다 먼저 닿습니다.", Agi, Str,
                null, null, null, null,
                With(Trait("ruru", "날쌘 발", "이동 속도가 15% 오릅니다.", null, Bonus(StatType.MoveSpeed, 0.15f))), 0),
            X("grom", "그롬", "투사", "수인", N, "먹보", "먹는 만큼 힘이 납니다.", Vit, Agi,
                null, null, null, null,
                With(Trait("grom", "대식가", "가방 포션의 회복량이 40% 늘어납니다.", null, Bonus(StatType.PotionPower, 0.4f))), 0),
            X("tik", "티크", "투사", "수인", N, "사냥꾼 견습", "약한 곳을 노리는 법을 배우는 중입니다.", Agi, Wil,
                null, null, null, null,
                Load<SoulPassiveSkillData>("PassiveTracking"), 0),
            X("yak", "야크", "투사", "드워프", N, "맨주먹", "주먹이 곧 무기입니다.", Str, Agi,
                null, null, null, null,
                With(Trait("yak", "무쇠 주먹", "타격력이 3 오릅니다.", null, Bonus(StatType.ImpactPower, 3f))), 0),
            X("fenri", "펜리", "투사", "수인", Elite2, "늑대 피", "피 냄새를 맡으면 달립니다.", Agi, Mag,
                S(Bonus(Agi, 3f), Bonus(Str, 2f), Bonus(Mag, -1f)), S(Bonus(Agi, 2f)), P("Rush"), null,
                With(Trait("fenri", "늑대 피", "적을 쓰러뜨리면 스태미나 15%를 회복하고 이동 속도가 10% 오릅니다.", null, Bonus(StatType.KillStamina, 0.15f), Bonus(StatType.MoveSpeed, 0.1f))), 0),
            X("haku", "하쿠", "투사", "수인", Elite2, "권법가", "연타 끝에 결정타를 꽂습니다.", Agi, Vit,
                S(Bonus(Agi, 3f), Bonus(Str, 2f), Bonus(Vit, -1f)), S(Bonus(Agi, 1f), Bonus(Str, 1f)), P("ComboFinish", "Kick"), null,
                With(Trait("haku", "권법", "공격이 12% 확률로 두 번 들어가고 행동이 8% 빨라집니다.", null, Bonus(StatType.DoubleHit, 0.12f), Bonus(StatType.ActionSpeed, 0.08f))), 0),
            X("zena", "제나", "투사", "인간", Elite2, "검투사", "관중이 많을수록, 적이 많을수록 신이 납니다.", Str, Luk,
                S(Bonus(Str, 3f), Bonus(Vit, 2f), Bonus(Luk, -1f)), S(Bonus(Str, 2f)), null, null,
                With(Trait("zena", "검투장", "3칸 안의 적 하나마다 공격력이 8% 오릅니다 (최대 5명).", null, Bonus(StatType.CrowdFury, 0.08f))), 0),
            X("nua", "누아", "투사", "엘프", Elite2, "독손톱", "손톱에 독을 바릅니다.", Agi, Vit,
                S(Bonus(Agi, 3f), Bonus(Luk, 2f), Bonus(Vit, -1f)), S(Bonus(Agi, 2f)), P("ApplyPoison"), null,
                With(Trait("nua", "독손톱", "공격에 독이 더해지고 거는 상태 이상이 한 단계 강해집니다.", null, Bonus(StatType.ToxicAttack, 3f), Bonus(StatType.StatusGrade, 1f))), 0),
            X("bahar", "바하르", "투사", "수인", Legend2, "백수의 왕", "그가 포효하면 짐승도 사람도 몸을 낮춥니다.", Str, Mag,
                S(Bonus(Str, 4f), Bonus(Vit, 3f), Bonus(Wil, 3f)), S(Bonus(Str, 1f), Bonus(Vit, 1f), Bonus(Wil, 1f)), null, A("SkillRoar"),
                With(Trait("bahar", "왕의 위엄", "3칸 안의 적이 수시로 공포에 떱니다.", null, Bonus(StatType.FearAura, 0.5f)), "PassiveAdrenaline"), 0),
            X("kin", "킨", "투사", "수인", Legend2, "폭풍발톱", "눈으로 따라갈 수 없는 연타.", Agi, Wil,
                S(Bonus(Agi, 5f), Bonus(Str, 3f), Bonus(Luk, 2f)), S(Bonus(Agi, 2f), Bonus(Str, 1f)), P("Rake"), null,
                With(Trait("kin", "폭풍", "행동이 25% 빨라지고 공격이 15% 확률로 두 번 들어갑니다.", null, Bonus(StatType.ActionSpeed, 0.25f), Bonus(StatType.DoubleHit, 0.15f))), 0),
            X("gar", "가르", "투사", "수인", Legend2, "피의 군주", "피를 마실수록 멈추지 않습니다.", Vit, Wil,
                S(Bonus(Str, 4f), Bonus(Vit, 4f), Bonus(Agi, 2f)), S(Bonus(Str, 1f), Bonus(Vit, 2f)), null, A("SkillFrenzy"),
                With(Trait("gar", "피의 군주", "준 피해의 8%를 흡혈하고 적을 쓰러뜨릴 때마다 스태미나 20%를 회복합니다.", null, Bonus(StatType.LifeSteal, 0.08f), Bonus(StatType.KillStamina, 0.2f))), 0),
            X("finn", "핀", "길잡이", "인간", N, "지도쟁이", "한 번 본 길은 잊지 않습니다.", Wil, Str,
                null, null, null, null,
                With(Trait("finn", "지도 그리기", "시야가 넓어집니다.", t => t.MapTraits = SoulMapTrait.WideVision)), 0),
            X("mei", "메이", "길잡이", "엘프", N, "눈 밝은", "먼 곳의 반짝임을 봅니다.", Agi, Str,
                null, null, null, null,
                With(Trait("mei", "밝은 눈", "숨은 보물 상자를 찾아내고 명중이 5% 오릅니다.", t => t.MapTraits = SoulMapTrait.RevealTreasure, Bonus(StatType.Accuracy, 0.05f))), 0),
            X("skip", "스킵", "길잡이", "인간", N, "발빠른", "하루 종일 걸어도 멀쩡합니다.", Agi, Vit,
                null, null, null, null,
                With(Trait("skip", "가벼운 발", "지친 걸음이 30% 덜 느려집니다.", null, Bonus(StatType.TirelessWalk, 0.3f))), 0),
            X("olga", "올가", "길잡이", "드워프", N, "광맥꾼", "금 냄새를 맡습니다.", Luk, Agi,
                null, null, null, null,
                With(Trait("olga", "금맥", "몬스터 금화가 20% 늘어납니다 (파티에서 가장 높은 것만).", null, Bonus(StatType.GoldFind, 0.2f))), 0),
            X("jesse", "제시", "길잡이", "인간", N, "덫 해체꾼", "숨긴 것은 보이면 끝입니다.", Agi, Str,
                null, null, null, null,
                With(Trait("jesse", "덫 감각", "숨은 문을 찾아냅니다.", t => t.MapTraits = SoulMapTrait.DetectHiddenDoors)), 0),
            X("reina", "레이나", "길잡이", "엘프", Elite2, "명궁", "화살 한 대로 끝냅니다.", Agi, Vit,
                S(Bonus(Agi, 3f), Bonus(Luk, 1f), Bonus(StatType.PiercePower, 2f), Bonus(Vit, -1f)), S(Bonus(Agi, 2f)), null, null,
                With(Trait("reina", "급소 사격", "치명타 확률이 10%, 치명타 피해가 25% 오릅니다.", null, Bonus(StatType.CritChance, 0.1f), Bonus(StatType.CritDamage, 0.25f))), 0),
            X("cob", "코브", "길잡이", "인간", Elite2, "도적", "주머니와 상자라면 뭐든 엽니다.", Luk, Wil,
                S(Bonus(Luk, 3f), Bonus(Agi, 2f), Bonus(Wil, -1f)), S(Bonus(Luk, 2f)), null, A("SoulSkillPickpocket"),
                With(Trait("cob", "도둑의 눈", "숨은 보물 상자를 찾아내고 장비 드롭 확률이 15% 오릅니다.", t => t.MapTraits = SoulMapTrait.RevealTreasure, Bonus(StatType.LootFind, 0.15f))), 0),
            X("nile", "나일", "길잡이", "인간", Elite2, "척후", "적보다 먼저 보고 먼저 쏩니다.", Agi, Luk,
                S(Bonus(Agi, 3f), Bonus(Wil, 2f), Bonus(Luk, -1f)), S(Bonus(Agi, 2f)), null, null,
                With(Trait("nile", "척후", "시야가 넓어지고 명중이 8% 오릅니다.", t => t.MapTraits = SoulMapTrait.WideVision, Bonus(StatType.Accuracy, 0.08f))), 0),
            X("batel", "바텔", "길잡이", "드워프", Elite2, "폭약꾼", "폭약 냄새가 몸에 배었습니다.", Str, Agi,
                S(Bonus(Str, 2f), Bonus(Vit, 2f), Bonus(Luk, 1f), Bonus(Agi, -1f)), S(Bonus(Str, 2f)), null, A("SoulSkillDynamite", "SkillTrap"),
                With(Trait("batel", "발파", "거는 상태 이상이 한 단계 강해지고 화염 저항이 30% 오릅니다.", null, Bonus(StatType.StatusGrade, 1f), Bonus(StatType.FireResist, 0.3f))), 0),
            X("aura", "아우라", "길잡이", "엘프", Legend2, "바람의 사수", "바람을 타고 쏘는 화살은 멈추지 않습니다.", Agi, Str,
                S(Bonus(Agi, 5f), Bonus(Luk, 3f), Bonus(Wil, 2f)), S(Bonus(Agi, 2f), Bonus(Luk, 1f)), P("KiteMaster"), A("SkillPiercingShot"),
                With(Trait("aura", "바람의 사수", "스킬 재사용 대기가 30% 줄고 행동이 15% 빨라집니다.", null, Bonus(StatType.SkillCooldownReduce, 0.3f), Bonus(StatType.ActionSpeed, 0.15f))), 0),
            X("crow", "크로우", "길잡이", "인간", Legend2, "그림자 추적자", "보이지 않는 곳에서 목을 노립니다.", Agi, Vit,
                S(Bonus(Agi, 4f), Bonus(Luk, 3f), Bonus(Str, 3f)), S(Bonus(Agi, 2f), Bonus(Str, 1f)), P("Ambush"), A("SoulSkillShadowHide"),
                With(Trait("crow", "그림자", "치명타 확률 15%, 치명타 피해 40%가 오르고 적의 시선을 덜 받습니다.", null, Bonus(StatType.CritChance, 0.15f), Bonus(StatType.CritDamage, 0.4f), Bonus(StatType.Threat, -0.4f))), 0),
            X("sian", "시안", "길잡이", "엘프", Legend2, "숲의 독수리", "숲의 모든 길과 숨은 방을 압니다.", Agi, Str,
                S(Bonus(Agi, 4f), Bonus(Luk, 3f), Bonus(Wil, 3f)), S(Bonus(Agi, 2f), Bonus(Luk, 1f)), P("PoisonArrow"), null,
                With(Trait("sian", "숲의 눈", "숨은 문과 보물을 찾아내고 공격에 독이 더해집니다.", t => t.MapTraits = SoulMapTrait.DetectHiddenDoors | SoulMapTrait.RevealTreasure, Bonus(StatType.ToxicAttack, 4f))), 0),
            X("philip", "필립", "성직자", "인간", N, "수련 사제", "기도가 조금씩 깊어집니다.", Wil, Str,
                null, null, null, null,
                With(Trait("philip", "깊은 기도", "MP를 쓰는 스킬의 효과가 10% 오릅니다.", null, Bonus(StatType.SpellPower, 0.1f))), 0),
            X("agnes", "아그네스", "성직자", "인간", N, "간호 수녀", "상처를 꼼꼼히 봅니다.", Wil, Agi,
                null, null, null, null,
                With(Trait("agnes", "간호", "치유 스킬이 부상을 하나 더 치료합니다.", null, Bonus(StatType.WoundMend, 1f))), 0),
            X("tom", "톰", "성직자", "인간", N, "탁발승", "보수 대신 한 끼 밥이면 됩니다. 고용비가 쌉니다.", Vit, Luk,
                null, null, null, null,
                null, -80),
            X("sui", "수이", "성직자", "엘프", N, "숲의 사제", "곁에 있으면 상처가 아뭅니다.", Mag, Str,
                null, null, null, null,
                With(Trait("sui", "숲의 숨결", "4칸 안의 동료(자신 포함) HP 재생이 1 오릅니다.", null, Bonus(StatType.RegenAura, 1f))), 0),
            X("harin", "하린", "성직자", "인간", N, "성가대", "노래하듯 기도합니다.", Mag, Str,
                null, null, null, null,
                With(Trait("harin", "성가", "최대 MP가 15 오릅니다.", null, Bonus(StatType.MaxMp, 15f))), 0),
            X("seraphin", "세라핀", "성직자", "인간", Elite2, "전투 사제", "철퇴로 때리고 기도로 감쌉니다.", Str, Mag,
                S(Bonus(Str, 3f), Bonus(Vit, 2f), Bonus(Mag, -1f)), S(Bonus(Str, 1f), Bonus(Vit, 1f)), P("Swing", "Kihap"), null,
                With(Trait("seraphin", "전투 기도", "4칸 안의 동료 HP 재생이 1.5 오르고 준 피해의 5%를 흡혈합니다.", null, Bonus(StatType.RegenAura, 1.5f), Bonus(StatType.LifeSteal, 0.05f))), 0),
            X("gabriel", "가브리엘", "성직자", "인간", Elite2, "퇴마사", "망자를 돌려보내는 법을 압니다.", Wil, Luk,
                S(Bonus(Wil, 3f), Bonus(Mag, 2f), Bonus(Luk, -1f)), S(Bonus(Wil, 2f)), null, A("SkillJudgment"),
                With(Trait("gabriel", "퇴마", "언데드에게 주는 피해가 40% 늘어납니다.", null, Bonus(StatType.HolyBane, 0.4f))), 0),
            X("noel", "노엘", "성직자", "엘프", Elite2, "축복사", "축복이 오래 머뭅니다.", Mag, Str,
                S(Bonus(Mag, 3f), Bonus(Wil, 2f), Bonus(Str, -1f)), S(Bonus(Mag, 2f)), null, A("SkillPrayer", "SkillHaste"),
                With(Trait("noel", "긴 축복", "동료에게 거는 강화의 지속 시간이 50% 늘어납니다.", null, Bonus(StatType.BuffDuration, 0.5f))), 0),
            X("magda", "마그다", "성직자", "인간", Elite2, "해독사", "독은 모두 그녀의 손에서 풀립니다.", Wil, Str,
                S(Bonus(Wil, 3f), Bonus(Vit, 2f), Bonus(Str, -1f)), S(Bonus(Wil, 2f)), null, A("SkillPurify"),
                With(Trait("magda", "해독", "독에 걸리지 않고 스킬 재사용 대기가 15% 줄어듭니다.", null, Bonus(StatType.PoisonResist, 1f), Bonus(StatType.SkillCooldownReduce, 0.15f))), 0),
            X("eve", "이브", "성직자", "인간", Elite2, "순교자", "제 몸을 내어 동료를 살립니다.", Vit, Luk,
                S(Bonus(Vit, 3f), Bonus(Wil, 2f), Bonus(Luk, -1f)), S(Bonus(Vit, 1f), Bonus(Wil, 1f)), null, A("SkillSacrifice"),
                With(Trait("eve", "순교", "최대 HP가 20 오르고 치유 스킬이 부상을 하나 더 치료합니다.", null, Bonus(StatType.MaxHp, 20f), Bonus(StatType.WoundMend, 1f))), 0),
            X("luciel", "루시엘", "성직자", "인간", Legend2, "대주교", "그의 기도 앞에 죽음도 물러섭니다.", Wil, Str,
                S(Bonus(Wil, 5f), Bonus(Mag, 4f), Bonus(Vit, 1f)), S(Bonus(Wil, 2f), Bonus(Mag, 1f)), null, A("SkillResurrection", "SkillPrayer"),
                With(Trait("luciel", "대주교", "스킬 재사용 대기가 30% 줄고 MP를 쓰는 스킬의 효과가 20% 오릅니다.", null, Bonus(StatType.SkillCooldownReduce, 0.3f), Bonus(StatType.SpellPower, 0.2f))), 0),
            X("aria", "아리아", "성직자", "엘프", Legend2, "생명의 노래", "노래가 들리는 동안 누구도 쉽게 쓰러지지 않습니다.", Mag, Str,
                S(Bonus(Mag, 4f), Bonus(Wil, 4f), Bonus(Luk, 2f)), S(Bonus(Mag, 1f), Bonus(Wil, 2f)), null, A("SkillSanctuary"),
                With(Trait("aria", "생명의 노래", "4칸 안의 동료 HP 재생이 3 오르고, 부상이 20% 느리게 쌓입니다.", null, Bonus(StatType.RegenAura, 3f), Bonus(StatType.WoundResist, 0.2f))), 0),
            // 궁수
            X("dale", "데일", "궁수", "인간", N, "사냥꾼", "짐승을 쫓던 활솜씨로 먹고삽니다.", Agi, Wil,
                null, null, null, null,
                With(Trait("dale", "사냥꾼의 눈", "명중이 5% 오릅니다.", null, Bonus(StatType.Accuracy, 0.05f))), 0),
            X("pip", "핍", "궁수", "엘프", N, "숲지기", "나무 사이를 소리 없이 걷습니다.", Agi, Str,
                null, null, null, null,
                With(Trait("pip", "숲의 발걸음", "이동 속도가 8% 오릅니다.", null, Bonus(StatType.MoveSpeed, 0.08f))), 0),
            X("mina", "미나", "궁수", "인간", N, "과녁 연습생", "과녁 한가운데만 봅니다.", Agi, Vit,
                null, null, null, null,
                With(Trait("mina", "한가운데", "치명타 확률이 5% 오릅니다.", null, Bonus(StatType.CritChance, 0.05f))), 0),
            X("lyra", "리라", "궁수", "엘프", Elite2, "속사수", "시위를 당기는 손이 보이지 않습니다.", Agi, Str,
                S(Bonus(Agi, 3f), Bonus(StatType.PiercePower, 2f), Bonus(Str, -1f)), S(Bonus(Agi, 2f)), P("Kite"), A("SkillQuickShot"),
                With(Trait("lyra", "속사", "행동이 10% 빨라집니다.", null, Bonus(StatType.ActionSpeed, 0.1f))), 0),
            X("bran", "브란", "궁수", "인간", Elite2, "저격수", "한 발이면 충분합니다.", Agi, Vit,
                S(Bonus(Agi, 2f), Bonus(StatType.PiercePower, 3f), Bonus(Vit, -1f)), S(Bonus(StatType.PiercePower, 2f)), null, A("SkillAimedShot"),
                With(Trait("bran", "저격", "명중이 5%, 치명타 피해가 30% 오릅니다.", null, Bonus(StatType.Accuracy, 0.05f), Bonus(StatType.CritDamage, 0.3f))), 0),
            X("sylvaine", "실베인", "궁수", "엘프", Legend2, "달의 사수", "달빛 아래 쏜 화살은 빗나가지 않습니다.", Agi, Str,
                S(Bonus(Agi, 5f), Bonus(StatType.PiercePower, 3f), Bonus(Wil, 2f)), S(Bonus(Agi, 2f), Bonus(StatType.PiercePower, 1f)), P("Kite"), A("SkillArrowRain", "SkillAimedShot"),
                With(Trait("sylvaine", "월시", "치명타 확률이 10%, 치명타 피해가 25% 오릅니다.", null, Bonus(StatType.CritChance, 0.1f), Bonus(StatType.CritDamage, 0.25f))), 0),
            X("hawk", "호크", "궁수", "인간", Legend2, "매의 눈", "하늘 높이서 먹이를 고르듯 적을 고릅니다.", Agi, Mag,
                S(Bonus(Agi, 4f), Bonus(Luk, 2f), Bonus(StatType.PiercePower, 2f)), S(Bonus(Agi, 2f), Bonus(Luk, 1f)), P("Kite"), A("SkillPinShot", "SkillQuickShot"),
                With(Trait("hawk", "매의 눈", "명중이 10% 오르고 엘리트와 보스에게 20% 더 강합니다.", null, Bonus(StatType.Accuracy, 0.1f), Bonus(StatType.EliteDamage, 0.2f))), 0),
            // 소환사
            X("lumi", "루미", "소환사", "엘프", N, "정령 친구", "어릴 적부터 정령과 놀았습니다.", Mag, Str,
                null, null, null, null,
                With(Trait("lumi", "정령 친구", "최대 MP가 10 오릅니다.", null, Bonus(StatType.MaxMp, 10f))), 0),
            X("odo", "오도", "소환사", "인간", N, "견습 소환사", "주문을 외울 때 혀가 꼬입니다. 대신 끈기는 있습니다.", Wil, Agi,
                null, null, null, null,
                With(Trait("odo", "끈기", "MP 재생이 0.1 오릅니다.", null, Bonus(StatType.MpRegen, 0.1f))), 0),
            X("sora", "소라", "소환사", "인간", N, "바람을 듣는 아이", "바람이 하는 말을 알아듣습니다.", Mag, Vit,
                null, null, null, null,
                With(Trait("sora", "바람의 귀", "이동 속도가 5% 오르고 최대 MP가 5 오릅니다.", null, Bonus(StatType.MoveSpeed, 0.05f), Bonus(StatType.MaxMp, 5f))), 0),
            X("ember", "엠버", "소환사", "엘프", Elite2, "불꽃 조련사", "불의 정령을 강아지처럼 부립니다.", Mag, Wil,
                S(Bonus(Mag, 3f), Bonus(Wil, 2f), Bonus(Vit, -1f)), S(Bonus(Mag, 2f)), null, A("SkillSummonFire"),
                With(Trait("ember", "불씨", "화염 피해가 20% 오릅니다.", null, Bonus(StatType.FireFocus, 0.2f))), 0),
            X("nerea", "네레아", "소환사", "엘프", Elite2, "서리 계약자", "얼음 정령과 맺은 약속을 지킵니다.", Wil, Str,
                S(Bonus(Wil, 3f), Bonus(Mag, 2f), Bonus(Str, -1f)), S(Bonus(Mag, 1f), Bonus(Wil, 1f)), null, A("SkillSummonFrost"),
                With(Trait("nerea", "서리 계약", "MP를 쓰는 스킬의 효과가 10% 오르고 마법 저항이 10% 오릅니다.", null, Bonus(StatType.SpellPower, 0.1f), Bonus(StatType.MagicResist, 0.1f))), 0),
            X("aeris", "에리스", "소환사", "엘프", Legend2, "사대 정령의 주인", "불과 얼음과 빛이 그녀를 따릅니다.", Mag, Str,
                S(Bonus(Mag, 5f), Bonus(Wil, 3f), Bonus(StatType.Recovery, 2f)), S(Bonus(Mag, 2f), Bonus(Wil, 1f)), null, A("SkillSummonFire", "SkillSummonFrost", "SkillSummonLight"),
                With(Trait("aeris", "정령 군주", "MP를 쓰는 스킬의 효과가 20% 오르고 스킬 재사용 대기가 15% 줄어듭니다.", null, Bonus(StatType.SpellPower, 0.2f), Bonus(StatType.SkillCooldownReduce, 0.15f))), 0),
            X("kael", "카엘", "소환사", "인간", Legend2, "늑대 무리의 주인", "바람의 늑대들이 그의 곁을 떠나지 않습니다.", Wil, Luk,
                S(Bonus(Wil, 4f), Bonus(Mag, 3f), Bonus(Agi, 2f)), S(Bonus(Mag, 1f), Bonus(Wil, 2f)), null, A("SkillSummonWind", "SkillSummonLight"),
                With(Trait("kael", "무리의 주인", "최대 MP가 20 오르고 MP 회복 속도가 30% 빨라집니다.", null, Bonus(StatType.MaxMp, 20f), Bonus(StatType.ManaRegenRate, 0.3f))), 0),
        };
        // what each knows, by grade: 일반 its job's one plain skill (a few none at all), 정예 that and its own (two or
        // three), 전설 that, its own and the job's best (three or four)
        var basic = new Dictionary<string, string> { { "검사", "SkillBattleFocus" }, { "수호자", "SkillTaunt" }, { "마법사", "SkillFireBolt" },
            { "투사", "SkillRoar" }, { "길잡이", "SkillAlertShot" }, { "성직자", "SkillHealingLight" }, { "궁수", "SkillQuickShot" }, { "소환사", "SkillSummonFire" } };
        // what even a plain caster knows beside its first spell (마법사 one or two more, 성직자 / 소환사 maybe one)
        var plainExtra = new Dictionary<string, string[]>
        {
            { "마법사", new[] { "SkillFrostShard", "SkillCurseWeakness", "SkillSlow", "SkillFireEnchant", "SkillFrostEnchant", "SkillHaste" } },
            { "성직자", new[] { "SkillWardPrayer", "SkillLifeBlessing", "SkillConsecrate", "SkillPurify" } },
            { "소환사", new[] { "SkillSummonFrost", "SkillSummonWind", "SkillSummonLight" } },
        };
        var skillLess = new HashSet<string> { "owen", "toby", "ruru", "skip" };
        // a battle priest goes in with its mace; every other priest heals from behind
        foreach (var recruit in roster)
            if (recruit != null) { recruit.Frontline = recruit.Id == "seraphin" || recruit.Id == "theron"; EditorUtility.SetDirty(recruit); } // 전투 사제, 전투 마법사
        // who fights with its fists wears 권갑, not the job's hunting knife
        var knuckles = AssetDatabase.LoadAssetAtPath<SoulEquipmentData>(DataPath + "/EquipmentKnuckles.asset");
        var fists = new HashSet<string> { "yak", "haku" };
        foreach (var recruit in roster)
            if (recruit != null) { recruit.Weapon = fists.Contains(recruit.Id) ? knuckles : null; EditorUtility.SetDirty(recruit); }
        foreach (var recruit in roster)
        {
            if (recruit == null || recruit.Template == null) continue;
            var kit = new List<SoulActiveSkillData>();
            void Add(SoulActiveSkillData skill) { if (skill != null && !kit.Contains(skill)) kit.Add(skill); }
            if (!skillLess.Contains(recruit.Id) && basic.TryGetValue(recruit.Job, out string first)) Add(A(first).Length > 0 ? A(first)[0] : null);
            var (fewest, most) = KitRange(recruit.Job, recruit.Rarity);
            if (recruit.Rarity == SoulStyleRarity.Normal && plainExtra.TryGetValue(recruit.Job, out var extras))
            {
                int g = 11; foreach (char c in recruit.Id) g = g * 31 + c; g &= int.MaxValue;
                int count = fewest - 1 + g % (most - fewest + 1); // e.g. a plain mage: 2 or 3 in all
                for (int i = 0; kit.Count < 1 + count && i < extras.Length; i++) Add(A(extras[(g / 3 + i) % extras.Length])[0]);
            }
            if (recruit.Rarity != SoulStyleRarity.Normal)
            {
                foreach (var own in recruit.Actives) Add(own);
                int want = fewest, cap = most;
                // a priest of some standing carries a blessing (unless its own skills already bless)
                if (recruit.Job == "성직자" && kit.Count < cap && !kit.Exists(s => s != null && s.Trigger == SoulTrigger.AllySupport))
                {
                    var blessings = new[] { "SkillWardPrayer", "SkillLifeBlessing", "SkillConsecrate" };
                    int k = 7; foreach (char c in recruit.Id) k = k * 31 + c; k &= int.MaxValue;
                    Add(A(blessings[k % blessings.Length])[0]);
                }
                var pool = new List<string>();
                foreach (var (job, files) in JobSkillFiles) if (job == recruit.Job) pool.AddRange(files);
                pool.AddRange(CommonSkillFiles);
                int h = 17; foreach (char c in recruit.Id) h = h * 31 + c; h &= int.MaxValue;
                for (int i = 0; kit.Count < want && i < pool.Count; i++) Add(A(pool[(h + i) % pool.Count])[0]);
                while (kit.Count > cap) kit.RemoveAt(0); // its own skills matter more than the plain one
            }
            recruit.Actives = kit.ToArray();
            EditorUtility.SetDirty(recruit);
        }
        return System.Array.FindAll(roster, e => e.Template != null);
    }

    static SoulVillageData CreateVillage(SoulStatRules rules, SoulDungeonData dungeon, SoulMercenaryData[] party, SoulMercenaryData[] newJobs, SoulMercenaryData priest, SoulEquipmentData[] shop, SoulPatternData[] trainingPool)
    {
        VillageSpriteSettings();
        var tiles = "., *=#".Replace(" ", "");
        return Asset("Village", (SoulVillageData item) =>
        {
            item.StatRules = rules; item.Dungeon = dungeon;
            item.DungeonScene = "SoulMercenaries"; item.VillageScene = "SoulVillage";
            item.StartingRoster = party; item.StartingGold = 300; item.StartingPotions = 2; item.StartingStones = 1;
            var jobs = new List<SoulMercenaryData>(party); jobs.AddRange(newJobs);
            foreach (var job in jobs) { job.WeakGrowth = WeakGrowthOf(job.Job); EditorUtility.SetDirty(job); }
            if (priest != null) { priest.WeakGrowth = WeakGrowthOf(priest.Job); EditorUtility.SetDirty(priest); }
            item.Recruits = Recruits(jobs.ToArray(), priest);
            item.HireTemplates = jobs.ToArray(); item.HirePrice = 150; item.PriestTemplates = priest != null ? new[] { priest } : new SoulMercenaryData[0];
            item.ShopEquipment = shop; item.PotionPrice = 40; item.StonePrice = 60;
            item.TrainingPatterns = trainingPool;
            // 28 × 14: the main road across the middle, the plaza (merchant's stand) at its heart
            item.GroundRows =
                "....,....................*..\n" +
                "...*...............,,.......\n" +
                "..........,.......,.....,...\n" +
                "...........,,....*...,,...,.\n" +
                "..........,.....,......,....\n" +
                "...=.*...=#########=.*.=.,..\n" +
                "==========#########=========\n" +
                "...=....,=###==##=#*..=....,\n" +
                ".............==,.....*......\n" +
                ".............==.*........*..\n" +
                "....,,.......==.............\n" +
                ".............==.......,.....\n" +
                "*............==...*,..,.....\n" +
                "............,==.......,.,...";
            item.TileKeys = ".,*=#";
            item.TileSprites = new[] { VillageSprite("tile_grass"), VillageSprite("tile_grass2"), VillageSprite("tile_flowers"), VillageSprite("tile_path"), VillageSprite("tile_plaza") };
            item.DoorLeft = VillageSprite("door_left"); item.DoorRight = VillageSprite("door_right");
            item.Buildings = new[]
            {
                new SoulBuildingDef { Kind = SoulBuildingKind.Guild, Name = "길드", Description = "용병을 고용하고, 용병단을 편성·장비하고, 길드 능력을 올립니다.",
                    Cell = new Vector2Int(1, 1), Size = new Vector2Int(5, 4), StartLevel = 1, UpgradeCosts = new[] { 0, 800, 2500, 6000, 14000 },
                    Sprites = new[] { VillageSprite("plot_5x4"), VillageSprite("guild_1"), VillageSprite("guild_2"), VillageSprite("guild_3"), VillageSprite("guild_4"), VillageSprite("guild_5") },
                    LevelNotes = new[] { "고용 후보 3명 · 용병단 8명 · 파티 정원 4명", "고용 후보 4명 · 용병단 14명 · 정예 등장 · 파티 정원 5명 · 파티 2개",
                        "고용 후보 5명 · 용병단 22명 · 파티 3개", "고용 후보 6명 · 용병단 34명 · 전설 등장 · 파티 5개", "고용 후보 7명 · 용병단 50명 · 파티 6개" } },
                new SoulBuildingDef { Kind = SoulBuildingKind.Shop, Name = "상점", Description = "장비·영혼석·회복 포션을 사고, 쓰지 않는 장비를 팝니다.",
                    Cell = new Vector2Int(12, 2), Size = new Vector2Int(4, 3), StartLevel = 1, UpgradeCosts = new[] { 0, 250, 600 },
                    Sprites = new[] { VillageSprite("plot_4x3"), VillageSprite("shop_1"), VillageSprite("shop_2"), VillageSprite("shop_3") },
                    LevelNotes = new[] { "평범한 장비 · 기본 물약과 두루마리", "좋은 장비가 섞여 들어옵니다 · 마나 물약·질풍/숨결의 두루마리", "정교한 장비까지 · 상급 물약·만능약·정화의 두루마리" } },
                new SoulBuildingDef { Kind = SoulBuildingKind.Church, Name = "성당", Description = "부상을 치료하고, 금화를 바쳐 다음 출정에 축복을 받습니다.",
                    Cell = new Vector2Int(21, 8), Size = new Vector2Int(4, 5), StartLevel = 0, UpgradeCosts = new[] { 200, 350, 700 },
                    Sprites = new[] { VillageSprite("plot_4x5"), VillageSprite("church_1"), VillageSprite("church_2"), VillageSprite("church_3") },
                    LevelNotes = new[] { "부상 치료 · 생명의 가호", "치료비 20% 할인 · 숨결의 가호", "치료비 40% 할인 · 용기의 가호" } },
                new SoulBuildingDef { Kind = SoulBuildingKind.Training, Name = "훈련소", Description = "금화로 용병을 훈련합니다: 스탯, 최대 스태미나, 새 패턴.",
                    Cell = new Vector2Int(1, 8), Size = new Vector2Int(5, 4), StartLevel = 0, UpgradeCosts = new[] { 250, 400, 800 },
                    Sprites = new[] { VillageSprite("plot_5x4"), VillageSprite("training_1"), VillageSprite("training_2"), VillageSprite("training_3") },
                    LevelNotes = new[] { "지구력·근력·체력 훈련, 패턴 수련", "민첩·마력 훈련, 패턴 교체", "정신 수양 · 대련장 (패턴·장비를 시험하는 모의전)" } },
                new SoulBuildingDef { Kind = SoulBuildingKind.Blacksmith, Name = "대장간", Description = "장비에 무작위 마법 부여를 합니다. 부여는 언제나 성공하지만, 쓸모없는 옵션이 붙을 수도 있습니다.",
                    Cell = new Vector2Int(17, 2), Size = new Vector2Int(4, 3), StartLevel = 0, UpgradeCosts = new[] { 300, 500, 900 },
                    Sprites = new[] { VillageSprite("plot_4x3"), VillageSprite("blacksmith_1"), VillageSprite("blacksmith_2"), VillageSprite("blacksmith_3") },
                    LevelNotes = new[] { "무작위 부여", "재부여 · 높은 수치 확률 증가", "옵션 고정 · 최상급 확률 증가" } },
                new SoulBuildingDef { Kind = SoulBuildingKind.Storage, Name = "창고", Description = "소모품과 장비를 보관하고, 출정에 가져갈 물약 벨트와 두루마리 주머니를 챙깁니다.",
                    Cell = new Vector2Int(22, 2), Size = new Vector2Int(4, 3), StartLevel = 1, UpgradeCosts = new[] { 0, 250, 550 },
                    Sprites = new[] { VillageSprite("plot_4x3"), VillageSprite("storage_1"), VillageSprite("storage_2"), VillageSprite("storage_3") },
                    LevelNotes = new[] { "벨트 4칸 · 주머니 3칸 · 장비 25개", "벨트 5칸 · 주머니 4칸 · 장비 40개 · 도둑을 막음", "벨트 6칸 · 주머니 5칸 · 장비 55개" } },
                new SoulBuildingDef { Kind = SoulBuildingKind.Library, Name = "서고", Description = "던전에서 가져온 스킬북을 등록하고, 금화를 내고 용병에게 스킬을 가르칩니다.",
                    Cell = new Vector2Int(7, 1), Size = new Vector2Int(4, 4), StartLevel = 0, UpgradeCosts = new[] { 250, 600, 1200 },
                    Sprites = new[] { VillageSprite("plot_4x4"), VillageSprite("library_1"), VillageSprite("library_2"), VillageSprite("library_3") },
                    LevelNotes = new[] { "스킬 등록·배우기 (스킬 Lv.1까지)", "같은 책으로 스킬 Lv.2까지", "스킬 Lv.3 · 한 권 더: 상위 스킬로 강화" } },
                new SoulBuildingDef { Kind = SoulBuildingKind.Memorial, Name = "추모비", Description = "용병단과 함께한 모든 용병의 기록 — 쓰러진 이들의 이름도 여기 남습니다.",
                    Cell = new Vector2Int(8, 9), Size = new Vector2Int(3, 3), StartLevel = 1, UpgradeCosts = new[] { 0 },
                    Sprites = new[] { VillageSprite("plot_3x3"), VillageSprite("memorial_1") }, LevelNotes = new string[0] },
                new SoulBuildingDef { Kind = SoulBuildingKind.SoulAltar, Name = "영혼 제단", Description = "보관한 영혼을 흡수시키고, 흡수한 영혼을 떼어내고, 같은 영혼을 합쳐 더 강하게 만듭니다.",
                    Cell = new Vector2Int(16, 9), Size = new Vector2Int(3, 3), StartLevel = 0, UpgradeCosts = new[] { 300, 600, 1000 },
                    Sprites = new[] { VillageSprite("plot_3x3"), VillageSprite("altar_1"), VillageSprite("altar_2"), VillageSprite("altar_3") },
                    LevelNotes = new[] { "보관 영혼 흡수 · 영혼 분리", "영혼 합성 (같은 영혼 3개)", "합성 비용 절반" } },
                new SoulBuildingDef { Kind = SoulBuildingKind.Merchant, Name = "떠돌이 상인", Description = "가끔 광장에 들르는 행상. 귀한 물약과 두루마리, 스킬북을 팝니다 — 아주 드물게 귀환의 두루마리도.",
                    Cell = new Vector2Int(12, 5), Size = new Vector2Int(3, 2), StartLevel = 1, UpgradeCosts = new[] { 0 },
                    Sprites = new[] { VillageSprite("merchant_1"), VillageSprite("merchant_1") }, LevelNotes = new string[0] },
                new SoulBuildingDef { Kind = SoulBuildingKind.Board, Name = "게시판", Description = "마을 사람들과 길드가 붙인 의뢰. 등급이 높을수록 보수가 큽니다.",
                    Cell = new Vector2Int(19, 7), Size = new Vector2Int(2, 2), StartLevel = 1, UpgradeCosts = new[] { 0, 300, 700 },
                    Sprites = new[] { VillageSprite("board_1"), VillageSprite("board_1"), VillageSprite("board_2"), VillageSprite("board_3") },
                    LevelNotes = new[] { "의뢰 4개 · 동시에 1개 · B급까지", "의뢰 6개 · 동시에 2개 · A급까지", "의뢰 8개 · 동시에 3개 · S급까지" } },
            };
            for (int i = 0; i < item.Buildings.Length; i++)
            {
                string room = item.Buildings[i].Kind == SoulBuildingKind.SoulAltar ? "altar" : item.Buildings[i].Kind.ToString().ToLowerInvariant();
                item.Buildings[i].Interior = VillageSprite("interior_" + room);
            }
            item.AllEquipment = All<SoulEquipmentData>(); item.AllSkills = All<SoulActiveSkillData>(); item.AllPatterns = All<SoulPatternData>();
            item.AllPassives = All<SoulPassiveSkillData>(); item.AllSouls = All<SoulData>(); item.AllRaces = All<SoulRaceData>();
            item.AllMercenaries = All<SoulMercenaryData>(); item.AllMonsters = All<SoulMonsterData>();
        });
    }

    static void CreateVillageScene(SoulVillageData village)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.1f, .14f, .09f); camera.orthographic = true;
        cameraObject.transform.position = new Vector3(0, 0, -10);
        var view = new GameObject("Soul Village").AddComponent<SoulVillageView>();
        view.Data = village;
        SoulMercenariesAssetBuilder.CreatePopupManager();
        var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        EditorSceneManager.SaveScene(scene, VillageScenePath);
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(entry => entry.path == VillageScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(VillageScenePath, true)); // the game starts in the village
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Soul Mercenaries village scene created: " + VillageScenePath);
    }

    // Equipment: bound on equip, the replaced piece goes back to the storehouse, a two-hander takes the off hand, weight loads
    // the wearer, drops follow the floor and only drops carry special options.
    // A new company starts with two mercenaries (no mage): checks that need one recruit it from the village's list.
    static SoulMercenary Mage(SoulCampaign campaign)
    {
        var mage = campaign.Roster.Find(h => h.Job == "마법사");
        if (mage != null) return mage;
        var data = System.Array.Find(campaign.Data.StartingRoster, d => d != null && d.Job == "마법사");
        if (data == null) return null;
        mage = SoulDungeonSession.RecruitOne(data, campaign.Rules);
        campaign.Roster.Add(mage);
        campaign.Record(mage);
        return mage;
    }

    static void ValidateEquipmentRules(SoulCampaign campaign, SoulMercenary hero)
    {
        SoulItem Make(string file, int grade) { var data = Load(file); Require(data != null, file + " exists"); var item = new SoulItem(data, grade); campaign.Inventory.Add(item); return item; }

        var shield = Make("EquipmentWoodenBuckler", 2);
        Require(campaign.Equip(hero, shield) && !campaign.Inventory.Contains(shield) && hero.Equipped(SoulEquipSlot.OffHand) == shield, "a shield goes to the off hand");
        var greatsword = Make("EquipmentGreatsword", 2);
        var lost = SoulCampaign.Replaced(hero, greatsword);
        Require(lost.Contains(shield) && lost.Count == 2, "a two-hander replaces the weapon and the shield");
        Require(campaign.Equip(hero, greatsword) && hero.Equipped(SoulEquipSlot.OffHand) == null && campaign.Inventory.Contains(shield), "the replaced pieces go back to the storehouse");
        campaign.Inventory.RemoveAll(i => lost.Contains(i));
        var buckler = Make("EquipmentWoodenBuckler", 2);
        Require(SoulCampaign.EquipBlock(hero, buckler) != null && !campaign.Equip(hero, buckler), "no shield beside a two-hander");
        campaign.Inventory.Remove(buckler);
        Require(campaign.Sell(Make("EquipmentCopperRing", 2)), "unworn gear can be sold");

        // grades scale the base options; the 평범한 sword is the reference
        var plain = new SoulItem(Load("EquipmentSword"), 2);
        var fine = new SoulItem(Load("EquipmentSword"), 4);
        float Attack(SoulItem item) { float sum = 0; foreach (var b in item.Bonuses) if (b.Stat == StatType.Attack) sum += b.Value; return sum; }
        Require(Mathf.Abs(Attack(plain) - 5) < .01f && Attack(fine) > Attack(plain) * 1.6f, "grade scales the base options");
        Require(new SoulItem(Load("EquipmentCopperRing"), 1).EnchantSlots == 1 && plain.EnchantSlots == 1 && new SoulItem(Load("EquipmentSword"), 6).EnchantSlots == 3, "enchant slots by grade (+1 on accessories)");

        // weight: plate on a weak body is heavy — patterns cost more stamina
        var mage = Mage(campaign);
        Require(mage != null, "the mage is in the roster");
        var thrust = AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/Thrust.asset");
        SoulCombat.PatternCost(mage, thrust, out float light, out _);
        var plate = Make("EquipmentPlateArmor", 2);
        var tower = Make("EquipmentTowerShield", 2);
        Require(campaign.Equip(mage, plate), "the mage puts on plate");
        float weightBefore = mage.Stats.Total(StatType.BodyWeight);
        SoulCombat.PatternCost(mage, thrust, out float heavy, out _);
        Require(mage.Stats.Total(StatType.LoadRatio) > 1 && heavy > light * 1.15f, $"heavy gear costs stamina (load {mage.Stats.Total(StatType.LoadRatio):0.00})");
        Require(mage.Stats.Radius == HeroStatPipeline.BodyRadius(mage.Stats.Height, mage.Race.Weight), "gear weighs, but the body keeps its size");
        Require(weightBefore > mage.Race.Weight, "gear adds to the body weight");
        campaign.Inventory.Remove(tower);

        // drops: floor 1 almost never gives a good item, floor 8 often; special options only on drops
        var random = new System.Random(3);
        var pool = new List<SoulEquipmentData>(AssetDatabase.LoadAssetAtPath<SoulDungeonData>(DataPath + "/DungeonRuins.asset").LootTable);
        int good1 = 0, good8 = 0, specials = 0;
        for (int i = 0; i < 2000; i++)
        {
            var shallow = SoulItemRules.Roll(pool, 1, SoulDropSource.Monster, 0, random);
            var deep = SoulItemRules.Roll(pool, 8, SoulDropSource.Monster, 0, random);
            if (shallow.Grade >= 3) good1++;
            if (deep.Grade >= 3) good8++;
            specials += deep.Specials.Count;
            Require(shallow.Grade <= 3, "no 정교한 or better on floor 1");
        }
        Require(good1 < 60 && good8 > 1200 && specials > 50, $"floor decides quality (floor 1: {good1}, floor 8: {good8} good of 2000, {specials} special options)");
        foreach (var item in campaign.Stock()) Require(item.Specials.Count == 0 && !item.Dropped, "shop items never carry special options");

        // special options work
        var vampire = new SoulItem(Load("EquipmentSword"), 3) { Dropped = true };
        vampire.Specials.Add("vampiric");
        Require(vampire.Name.StartsWith("흡혈의"), "a special option names the item");
        var cursed = new SoulItem(Load("EquipmentSword"), 2) { Dropped = true };
        cursed.Specials.Add("cursed");
        Require(Attack(cursed) > Attack(plain) * 1.45f, "cursed: stronger base options");
    }

    [MenuItem("Tools/Project K/영혼 용병단/핵심 규칙 검증")]
    public static void Validate()
    {
        // icons come from the game's SpriteManager (Atlas_SoulMercenaries), not a store of their own
        var sprites = Resources.Load<SpriteManager>("SpriteManager");
        var soulAtlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>("Assets/_project/3.Textures/Icons/Atlas_SoulMercenaries.spriteatlas");
        Require(sprites != null && soulAtlas != null, "SpriteManager has the soul mercenaries atlas");
        UnityEditor.U2D.SpriteAtlasUtility.PackAtlases(new[] { soulAtlas }, EditorUserBuildSettings.activeBuildTarget);
        Require(sprites.Get("stat_Strength") != null && sprites.Get("ui_gold") != null && sprites.Get("status_Bleed") != null, "SpriteManager serves the soul icons");
        var dungeon = AssetDatabase.LoadAssetAtPath<SoulDungeonData>(DataPath + "/DungeonTest.asset");
        var ruins = AssetDatabase.LoadAssetAtPath<SoulDungeonData>(DataPath + "/DungeonRuins.asset");
        var rules = AssetDatabase.LoadAssetAtPath<SoulStatRules>(DataPath + "/StatRules.asset");
        var hero = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryRia.asset");
        var dwarfHero = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryThor.asset");
        var soul = AssetDatabase.LoadAssetAtPath<SoulData>(DataPath + "/SoulBoar.asset");
        var thrust = AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/Thrust.asset");
        var bow = AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/Bow.asset");
        Require(dungeon != null && rules != null && hero != null && dwarfHero != null && soul != null && thrust != null && bow != null, "Create sample content first");

        // Guaranteed first soul: kill the first monster and let the session reward it.
        var dropSession = new SoulDungeonSession(dungeon, rules, new[] { hero }, 1);
        dropSession.Monsters[0].Hp = 0;
        dropSession.Tick(.01f);
        Require(dropSession.Stash.Count == 1, "first soul drop is guaranteed");

        var session = new SoulDungeonSession(dungeon, rules, new[] { hero, dwarfHero }, 1);
        var unit = session.Mercenaries[0];
        var dwarf = session.Mercenaries[1];
        Require(unit.Stats.Total(StatType.Strength) == 5, "base strength (the sword gives attack, not strength)");
        Require(unit.Stats.From("equipment:0", StatType.Attack) >= 5, "the sword's attack sits on its own source layer");
        Require(session.Map.FindPath(dungeon.PartyStart, dungeon.Exit, unit.Stats.Radius).Count > 0, "obstacle path");

        // Race passives use the same passive path as soul passives.
        SoulCombat.PatternCost(unit, thrust, out float humanCost, out _);
        Require(humanCost < thrust.StaminaCost, "human adaptability lowers pattern stamina");
        Require(dwarf.Stats.Total(StatType.KnockbackResist) > 0, "dwarf sturdy build knockback resist");
        Require(SoulCombat.KnockbackDistance(unit, dwarf, 1) < SoulCombat.KnockbackDistance(dwarf, unit, 1), "weight and resist shape knockback");
        Require(dwarf.Fit(bow) == SoulPatternFit.RaceBlocked && unit.Fit(bow) == SoulPatternFit.NeedsWeapon, "soul preview pattern fit");

        // Status: bleed stacks, control states grant immunity after they end.
        var target = session.Monsters[0];
        var random = new System.Random(1);
        Require(Apply(target, SoulStatus.Bleed, 4, 2, random) && Apply(target, SoulStatus.Bleed, 4, 2, random), "bleed applies");
        Require(target.Statuses.Find(s => s.Kind == SoulStatus.Bleed).Stacks == 2, "bleed accumulates");
        float hp = target.Hp;
        SoulCombat.TickResources(target, .05f);
        Require(target.Hp < hp, "bleed deals damage over time");
        Require(Apply(target, SoulStatus.Stun, .01f, 0, random) && target.Disabled, "stun disables");
        SoulCombat.TickResources(target, .05f);
        Require(!target.Disabled && !SoulCombat.ApplyStatus(target, SoulStatus.Stun, 1, 1, 0, random), "control immunity after stun");

        // Mage: casts a Cast-trigger skill through the casting pattern, waits when MP is short.
        var seraData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenarySera.asset");
        Require(seraData != null, "mage sample exists");
        var mageSession = new SoulDungeonSession(dungeon, rules, new[] { seraData }, 3);
        var mage = mageSession.Mercenaries[0];
        var foe = mageSession.Monsters[0];
        mage.Position = foe.Position + Vector2.right * 3f;
        mage.Mp = 0;
        mageSession.Tick(.02f);
        // (a stamina skill of anyone's — 전장의 집중 — may come first: it costs no MP)
        Require(mage.Action == "MP 회복 대기" || mage.Action == "마력 호흡" || mage.Action == "전장의 집중" || mage.Action == "전투 함성",
            "mage waits for MP or breathes for it (" + mage.Action + ")");
        mage.Mp = mage.Stats.Total(StatType.MaxMp);
        mage.Cooldown = 0;
        mageSession.CombatEvents.Clear();
        mage.Position = foe.Position + Vector2.right * 3f;
        mageSession.Tick(.02f);
        Require(mageSession.CombatEvents.Exists(e => e.Kind == SoulEventKind.Skill && e.Actor == mage), "mage casts an attack skill (" + mage.Action + ")");
        Require(mage.Mp < mage.Stats.Total(StatType.MaxMp), "casting spends MP");
        Require(!mage.Patterns().Exists(p => p.Category == SoulPatternCategory.Attack), "magic is a skill: the mage owns no attack pattern");

        // Weapon basic attack: a mercenary without any attack pattern still swings its weapon at half power.
        var bareRace = ScriptableObject.CreateInstance<SoulRaceData>();
        bareRace.Id = "test"; bareRace.StartingPatterns = new[] { AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/Approach.asset") };
        var bare = ScriptableObject.CreateInstance<SoulMercenaryData>();
        bare.Id = "bare"; bare.DisplayName = "bare"; bare.Race = bareRace; bare.StartingEquipment = hero.StartingEquipment;
        bare.BaseStats = hero.BaseStats;
        var basicSession = new SoulDungeonSession(dungeon, rules, new[] { bare }, 5);
        var fighter = basicSession.Mercenaries[0];
        fighter.Position = basicSession.Monsters[0].Position + Vector2.left * .9f;
        basicSession.Tick(.02f);
        Require(fighter.Action == "기본 공격", "weapon basic attack fallback (" + fighter.Action + ")");
        UnityEngine.Object.DestroyImmediate(bare); UnityEngine.Object.DestroyImmediate(bareRace);

        // A soul lands at full weight (HeroStatPipeline.SoulPower), so the check is relative to what it carries.
        var drop = new SoulDrop(soul); session.Stash.Add(drop);
        float beforeSoul = unit.Stats.Total(StatType.Strength), carried = 0;
        foreach (var bonus in soul.CharacteristicStats) if (bonus.Stat == StatType.Strength) carried += bonus.Value;
        Require(carried > 0, "the test soul carries strength");
        Require(session.Absorb(unit, drop)
            && Mathf.Abs(unit.Stats.Total(StatType.Strength) - (beforeSoul + carried * HeroStatPipeline.SoulPower)) < .01f,
            $"soul absorption lands at x{HeroStatPipeline.SoulPower} ({beforeSoul} + {carried} -> {unit.Stats.Total(StatType.Strength)})");
        Require(unit.Stats.From("soul:0", StatType.Attack) > 0, "soul derived layer");
        Require(!unit.Absorb(soul, rules), "level one slot limit");
        int points = session.StatPointsPerLevel;
        Require(unit.AddExperience(SoulMercenary.ExperienceFor(1), rules, new System.Random(1)) == 1 && unit.Level == 2, "levels come by themselves");
        float levelLayer = 0;
        foreach (var entry in unit.LevelStats) { levelLayer += unit.Stats.From("mercenary:level", entry.Key); Require(HeroStatPipeline.IsUpper(entry.Key), "growth only raises upper stats"); }
        Require(Mathf.Approximately(levelLayer, points), "automatic growth spends every point");
        Require(!unit.CanPickPattern && session.OpenLevel(unit) == null, "no pattern pick before level 10");

        // Tendency: a dwarf's growth leans to vitality/durability over many rolls.
        var dwarfRolls = new Dictionary<StatType, int>();
        var growthRandom = new System.Random(9);
        for (int i = 0; i < 400; i++)
            foreach (var entry in dwarf.RollGrowth(1, growthRandom))
                dwarfRolls[entry.Key] = (dwarfRolls.TryGetValue(entry.Key, out int n) ? n : 0) + entry.Value;
        dwarfRolls.TryGetValue(StatType.Durability, out int durable);
        dwarfRolls.TryGetValue(StatType.Luck, out int lucky);
        Require(durable > lucky, "race tendency weights growth");

        var skill = soul.ActiveSkills[0];
        unit.Position = session.Monsters[0].Position + Vector2.left; // inside the charge pattern range
        SoulSkillUsePolicy.Costs(unit, skill, out float stamina, out _);
        unit.Stamina = stamina;
        Require(SoulSkillUsePolicy.TryCommit(unit, skill, session.Monsters[0]), "skill cost commit");
        Require(Mathf.Abs(unit.Stamina) < .001f && !SoulSkillUsePolicy.TryCommit(unit, skill, session.Monsters[0]), "single charge and cooldown");
        ValidateDesignRules(dungeon, ruins, rules, hero, dwarfHero, soul, bow);
        Debug.Log("Soul Mercenaries core checks passed");
    }

    static void ValidateDesignRules(SoulDungeonData dungeon, SoulDungeonData ruins, SoulStatRules rules, SoulMercenaryData ria,
        SoulMercenaryData thorData, SoulData boarSoul, SoulPatternData bow)
    {
        var karonData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryKaron.asset");
        var taunt = AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/SkillTaunt.asset");
        Require(ruins != null && karonData != null && taunt != null, "new sample content exists");

        // Large map: about 44× the first sample and the start reaches the exit for the widest mercenary.
        var big = new SoulDungeonSession(ruins, rules, new[] { ria, thorData, karonData }, 2);
        Require(big.Map.Width * big.Map.Height >= 40 * 56 * 36, "large map size");
        Require(big.Map.FindPath(big.PartyStart, big.Exit, .45f).Count > 0, "large map start → exit path");
        foreach (var portal in big.Exits)
            Require(big.Map.Reachable(big.Map.Center(big.PartyStart), big.Map.Center(portal), .45f), $"large map start → portal {portal}");

        // Layout: start near the center, a portal at each of the four edges, the boss away from all of them.
        var size = new Vector2(big.Map.Width, big.Map.Height);
        Require(Vector2.Distance(big.PartyStart, size / 2) < Mathf.Min(size.x, size.y) * .2f, $"start near the map center ({big.PartyStart})");
        bool north = false, east = false, south = false, west = false;
        foreach (var portal in big.Exits)
        {
            north |= portal.y <= 2; south |= portal.y >= big.Map.Height - 3;
            west |= portal.x <= 2; east |= portal.x >= big.Map.Width - 3;
        }
        Require(big.Exits.Count == 4 && north && east && south && west, "a portal at each edge");
        var boss = big.Monsters.Find(m => m.Data.Guardian);
        Require(Vector2.Distance(boss.Position, big.Map.Center(big.PartyStart)) > 60, "boss far from the start");
        foreach (var portal in big.Exits) Require(Vector2.Distance(boss.Position, big.Map.Center(portal)) > 60, $"boss away from portal {portal}");
        Require(big.ExitOpen, "portals open with the boss alive (the boss is optional)");
        bool portalSeen = false;
        foreach (var portal in big.Exits) portalSeen |= big.IsExplored(portal);
        Require(!portalSeen && big.IsExplored(big.Map.Cell(big.Mercenaries[0].Position)), "fog of war: start seen, portals unknown");

        // Boss → treasure chest → equipment.
        boss.Hp = 0;
        big.Tick(.02f);
        var bossChest = big.Chests.Find(c => !c.Hidden);
        Require(bossChest != null && big.Chests.FindAll(c => !c.Hidden).Count == 1 && !bossChest.Opened, "boss drops a treasure chest");
        big.Mercenaries[0].Position = bossChest.Position + Vector2.right * .5f;
        big.Tick(.02f);
        Require(bossChest.Opened && big.Inventory.Count == ruins.BossLootCount, $"chest gives equipment ({big.Inventory.Count})");

        // Walking across a portal does not leave the stage; heading for it does.
        var leaver = big.Mercenaries[0];
        leaver.Position = big.Map.Center(big.Exits[0]);
        big.Tick(.02f);
        Require(!big.Finished, "crossing a portal while exploring does not leave");
        leaver.Position = big.Map.Center(big.Exits[0] + new Vector2Int(0, 2));
        big.SetDestination(leaver, big.Exits[0], out _);
        for (int i = 0; i < 100 && !big.Finished; i++) big.Tick(.05f);
        Require(big.Finished, "a mercenary sent to a portal leaves the stage");

        // Taunt: a timed buff on the combat layer raises threat, then expires.
        var session = new SoulDungeonSession(dungeon, rules, new[] { ria, thorData }, 4);
        var thor = session.Mercenaries[1];
        float threat = thor.Stats.Total(StatType.Threat), armor = thor.Stats.Total(StatType.Armor);
        SoulCombat.ApplySelfBuffs(thor, taunt);
        Require(thor.Stats.Total(StatType.Threat) > threat + 1.5f && thor.Stats.Total(StatType.Armor) > armor + 20, "taunt raises threat and armor");
        thor.TickTimedBuffs(10);
        Require(Mathf.Approximately(thor.Stats.Total(StatType.Threat), threat), "taunt expires");

        // Leap shockwave: a light dwarf only leaps; with the boar soul (weight ×1.15 ≥ 90kg) the landing hits.
        Require(LeapEvents(dungeon, rules, thorData, null) == "leap", "leap without shockwave under the weight threshold");
        Require(LeapEvents(dungeon, rules, thorData, boarSoul) == "leap+shockwave", "shockwave once heavy enough");

        // Counter: beastkin strikes back at melee attackers.
        var counterSession = new SoulDungeonSession(dungeon, rules, new[] { karonData }, 6);
        var karon = counterSession.Mercenaries[0];
        var attacker = counterSession.Monsters[0];
        var random = new System.Random(3);
        SoulHitResult counterHit = default;
        for (int i = 0; i < 50 && !counterHit.Countered; i++)
        {
            karon.Stamina = karon.Stats.Total(StatType.MaxStamina);
            counterHit = SoulCombat.Resolve(attacker, karon, new SoulHit { School = SoulDamageSchool.Physical, Raw = 8, Melee = true }, random);
        }
        Require(counterHit.Countered && counterHit.CounterDamage > 0, "counter strikes back");
        Require(Mathf.Approximately(karon.Stats.Total(StatType.ExecuteBonus), .25f), "hunter instinct");

        // Core pattern: a soul whose core pattern the race cannot use is not absorbable at all.
        var coreSoul = ScriptableObject.CreateInstance<SoulData>();
        coreSoul.Id = "test_core"; coreSoul.OriginMonster = "test"; coreSoul.CharacteristicStats = new[] { Bonus(StatType.Agility, 1) };
        coreSoul.Patterns = new[] { bow }; coreSoul.CorePattern = bow;
        var drop = new SoulDrop(coreSoul);
        session.Stash.Add(drop);
        Require(session.AbsorbBlockReason(thor, drop) != null && !session.Absorb(thor, drop), "core pattern blocks absorption");
        UnityEngine.Object.DestroyImmediate(coreSoul);

        // Move orders: the mercenary keeps walking but fights what is in reach on the way.
        var order = new SoulDungeonSession(dungeon, rules, new[] { ria }, 9);
        order.AutoExplore = false; // measure the order alone (auto exploration would take over on arrival)
        var runner = order.Mercenaries[0];
        runner.Position = order.Monsters[0].Position + Vector2.left;
        var before = runner.Position;
        Require(order.SetDestination(runner, dungeon.PartyStart, out _) && order.HasMoveOrder(runner), "move order registered");
        bool struck = false;
        var trace = new System.Text.StringBuilder();
        for (int i = 0; i < 120; i++) // 6s: a stun from the monster may interrupt the march
        {
            order.Tick(.05f);
            if (i % 6 == 0) trace.Append($" {runner.Position.x:0.00},{runner.Position.y:0.00}:{runner.Action}:{runner.Stamina:0}");
            struck |= order.CombatEvents.Exists(e => e.Kind == SoulEventKind.Attack && e.Actor == runner);
            order.CombatEvents.Clear();
        }
        var home = order.Map.Center(dungeon.PartyStart);
        Require(struck, "ordered mercenary attacks enemies in reach on the way");
        Require(Vector2.Distance(runner.Position, home) < Vector2.Distance(before, home) - .5f, "ordered mercenary keeps moving (" + runner.Action + ")" + trace);

        // Sight: walls block line of sight (radius-0 clearance used to pass through walls).
        Require(!order.Map.Straight(order.Map.Center(new Vector2Int(2, 3)), order.Map.Center(new Vector2Int(6, 3)), 0), "walls block sight");
        Require(order.Map.Straight(order.Map.Center(new Vector2Int(1, 1)), order.Map.Center(new Vector2Int(6, 1)), 0), "open line of sight");
        runner.Stamina = 0;
        order.SetDestination(runner, new Vector2Int(8, 8), out _);
        var tiredFrom = runner.Position;
        for (int i = 0; i < 10; i++) { runner.Stamina = 0; order.Tick(.05f); }
        Require(runner.Position != tiredFrom, "exhausted mercenary still walks under an order (" + runner.Action + ")");

        ValidateWallsAndExploration(dungeon, ruins, rules, ria, thorData);
        ValidateLeaderAndPatterns(dungeon, rules, ria, thorData);
        ValidateNavigation(dungeon, ruins, rules, ria, thorData);
        ValidateEncounters(ruins, rules, ria, thorData);
        ValidateTelegraphs(rules, ria, thorData);
        ValidateEncounterStrength(rules, ria, thorData);
        ValidateStatusPatterns(dungeon, rules, ria, thorData);
        ValidateStaminaAndMana(dungeon, rules, ria, thorData);
        ValidateVillage(dungeon, rules, ria, thorData);
        ValidateProgression(dungeon, ruins, rules, ria, thorData);
        ValidateHiddenAndObjects(dungeon, ruins, rules, ria, thorData);

        // Mental growth: witnessing an ally fall becomes Will after the dungeon.
        var mental = new SoulDungeonSession(dungeon, rules, new[] { ria, thorData }, 8);
        var survivor = mental.Mercenaries[0];
        float will = survivor.Stats.Total(StatType.Will);
        mental.Mercenaries[1].Hp = 0;
        foreach (var monster in mental.Monsters) monster.Hp = 0;
        survivor.Position = mental.Map.Center(dungeon.Exit);
        mental.AutoExplore = false;
        mental.SetDestination(survivor, dungeon.Exit, out _);
        mental.Tick(.02f);
        Require(mental.Finished && survivor.Stats.Total(StatType.Will) >= will + 2 && mental.MentalReport.Count == 1, "mental growth after the battle");
    }

    static void ValidateWallsAndExploration(SoulDungeonData dungeon, SoulDungeonData ruins, SoulStatRules rules, SoulMercenaryData ria, SoulMercenaryData thorData)
    {
        var lukaData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryLuka.asset");
        var seraData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenarySera.asset");
        Require(lukaData != null, "pathfinder sample exists");

        // Three wall kinds: wall (walk ✗ sight ✗), lava (walk ✗ sight ✓), hidden door (walk ✗ until found / forced ✓, sight ✗).
        var room = ScriptableObject.CreateInstance<SoulDungeonData>();
        room.MapRows = "##########\n#..#.~...#\n#..+..~..#\n#........#\n##########";
        room.PartyStart = new Vector2Int(1, 2); room.Exit = new Vector2Int(8, 3);
        var map = new SoulMap(room.MapRows);
        Require(!map.Sight(map.Center(new Vector2Int(2, 1)), map.Center(new Vector2Int(4, 1))), "wall blocks sight");
        Require(map.Sight(map.Center(new Vector2Int(4, 1)), map.Center(new Vector2Int(6, 1))) && !map.Open(5, 1), "lava: sight yes, walk no");
        Require(!map.Open(3, 2) && map.Passable(3, 2) && !map.Transparent(3, 2), "hidden door: forced only, blocks sight");
        Require(map.Discover(new Vector2Int(3, 2)) && map.Open(3, 2), "found hidden door is walkable");

        // Pathfinder: finds the door nearby and sees past walls; others do not.
        var withGuide = new SoulDungeonSession(room, rules, new[] { lukaData }, 1);
        Require(withGuide.Map.IsDiscovered(new Vector2Int(3, 2)), "pathfinder detects hidden doors");
        // Seeing past walls fills the map, but the dungeon view stays black there (only the minimap knows).
        Require(withGuide.IsExplored(new Vector2Int(4, 1)) && !withGuide.IsSeen(new Vector2Int(4, 1)), "pathfinder maps past walls without lighting them");
        var without = new SoulDungeonSession(room, rules, new[] { ria }, 1);
        Require(!without.Map.IsDiscovered(new Vector2Int(3, 2)) && !without.IsExplored(new Vector2Int(4, 1)), "normal sight is blocked by walls");
        UnityEngine.Object.DestroyImmediate(room);

        // Map knowledge: a pathfinder knows the exit and the guardian from the start.
        var bigGuide = new SoulDungeonSession(ruins, rules, new[] { lukaData }, 2);
        var bigPlain = new SoulDungeonSession(ruins, rules, new[] { ria }, 2);
        Require(bigGuide.ExitKnown && !bigPlain.ExitKnown, "pathfinder reveals the exit");
        Require(bigGuide.Knows(bigGuide.Monsters.Find(m => m.Data.Guardian)) && !bigPlain.Knows(bigPlain.Monsters.Find(m => m.Data.Guardian)), "pathfinder reveals the guardian");

        // Body blocking: a big body right in the line of fire takes most ranged shots, a thin offset one fewer.
        var cover = new SoulDungeonSession(dungeon, rules, new[] { seraData, thorData, lukaData }, 3);
        var shooter = cover.Monsters[0];
        var sera = cover.Mercenaries[0];
        var thor = cover.Mercenaries[1];
        var luka = cover.Mercenaries[2];
        shooter.Position = new Vector2(6.5f, 8.5f);
        sera.Position = new Vector2(12.5f, 8.5f);
        thor.Position = new Vector2(9.5f, 8.5f);
        luka.Position = new Vector2(20f, 20f);
        int thorBlocks = 0;
        for (int i = 0; i < 300; i++) if (cover.InterceptFor(shooter, sera) == thor) thorBlocks++;
        thor.Position = new Vector2(20f, 20f);
        luka.Position = new Vector2(9.5f, 8.8f);
        int lukaBlocks = 0;
        for (int i = 0; i < 300; i++) if (cover.InterceptFor(shooter, sera) == luka) lukaBlocks++;
        Require(thorBlocks > 150 && lukaBlocks < thorBlocks, $"body blocking scales with size and alignment ({thorBlocks} vs {lukaBlocks})");

        // Automatic exploration: with no orders the party uncovers the dungeon by itself.
        var explore = new SoulDungeonSession(ruins, rules, new[] { thorData, ria }, 4);
        int before = Count(explore.Explored);
        for (int i = 0; i < 1600 && !explore.Defeated; i++) explore.Tick(.05f);
        Require(Count(explore.Explored) > before + 250, $"auto exploration uncovers the map ({before} → {Count(explore.Explored)})");
    }

    static void ValidateLeaderAndPatterns(SoulDungeonData dungeon, SoulStatRules rules, SoulMercenaryData ria, SoulMercenaryData thorData)
    {
        var seraData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenarySera.asset");
        var karonData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryKaron.asset");
        var whirl = AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/Whirl.asset");

        // 구르기 / 회피 / 치고 빠지기: evasion is positional, and Agility is the gate — an attack can only be
        // evaded by a unit quick enough to read it inside its wind-up. 그림자 늑대's 찌르기 is a fast lane
        // (0.24s), so nimble 카론 (Agility 7, reacts in 0.21s) slips it and slow 토르 (Agility 2, 0.63s) cannot.
        var pit = ScriptableObject.CreateInstance<SoulDungeonData>();
        var pitRows = new System.Text.StringBuilder();
        for (int y = 0; y < 16; y++) pitRows.Append(y == 0 || y == 15 ? new string('#', 26) : "#" + new string('.', 24) + "#").Append(y < 15 ? "\n" : "");
        pit.MapRows = pitRows.ToString();
        pit.PartyStart = new Vector2Int(5, 8); pit.Exit = new Vector2Int(24, 1);
        pit.Monsters = new[] { Spawn(AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterWolf.asset"), 13, 8) };

        int Evades(SoulMercenaryData who, string patternFile, SoulEventKind kind)
        {
            var evasion = AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/" + patternFile + ".asset");
            var session = new SoulDungeonSession(pit, rules, new[] { who }, 17);
            session.AutoExplore = false;
            var unit = session.Mercenaries[0];
            foreach (var owned in unit.Patterns())
                if (owned != null && SoulCombat.Evasive(owned) && owned != evasion) unit.StartingPatterns.Remove(owned);
            if (!unit.Patterns().Contains(evasion)) unit.LearnedPatterns.Add(evasion);
            unit.Rebuild(rules);
            var striker = session.Monsters[0];
            int count = 0;
            for (int i = 0; i < 900; i++)
            {
                unit.Hp = unit.Stats.Total(StatType.MaxHp);
                unit.Stamina = unit.Stats.Total(StatType.MaxStamina);
                striker.Hp = striker.Stats.Total(StatType.MaxHp);
                session.Tick(.05f);
                foreach (var e in session.CombatEvents) if (e.Kind == kind && e.Target == unit) count++;
                session.CombatEvents.Clear();
                if (Vector2.Distance(unit.Position, striker.Position) > 1.6f) unit.Position = striker.Position + Vector2.left;
            }
            return count;
        }

        int quickRolls = Evades(karonData, "Roll", SoulEventKind.Roll), slowRolls = Evades(thorData, "Roll", SoulEventKind.Roll);
        int sidesteps = Evades(karonData, "Dodge", SoulEventKind.Evade), kiteSteps = Evades(karonData, "Kite", SoulEventKind.Evade);
        Debug.Log($"EVASION vs 그림자 늑대 찌르기 — 카론 구르기 {quickRolls}, 토르 구르기 {slowRolls}, 카론 회피 {sidesteps}, 카론 치고빠지기 {kiteSteps}");
        Require(quickRolls > 0, $"a nimble fighter rolls out of ordinary attacks ({quickRolls})");
        Require(slowRolls < quickRolls, $"agility gates evasion (카론 {quickRolls}, 토르 {slowRolls})");
        Require(sidesteps > 0, $"회피 sidesteps out of a lane attack ({sidesteps})");
        Require(kiteSteps > 0, $"치고 빠지기 doubles as an evasive step ({kiteSteps})");
        UnityEngine.Object.DestroyImmediate(pit);

        // Leader: the tank walks off under an order; the mage (not ordered) follows instead of staying behind.
        var follow = new SoulDungeonSession(dungeon, rules, new[] { thorData, seraData }, 12);
        follow.AutoExplore = false;
        var tank = follow.Mercenaries[0];
        var mage = follow.Mercenaries[1];
        Require(follow.Leader == tank, "tank leads by default");
        var mageStart = mage.Position;
        follow.SetDestination(tank, new Vector2Int(8, 1), out _);
        for (int i = 0; i < 120; i++) follow.Tick(.05f);
        Require(Vector2.Distance(mage.Position, mageStart) > 2f && Vector2.Distance(mage.Position, tank.Position) < SoulDungeonSession.TetherRadius + 1.5f,
            $"followers keep up with the leader ({Vector2.Distance(mage.Position, tank.Position):0.0})");

        // A ranged leader under an order fights what it sees first, then moves on.
        var careful = new SoulDungeonSession(dungeon, rules, new[] { seraData }, 13);
        careful.AutoExplore = false;
        var archer = careful.Mercenaries[0];
        careful.SetLeader(archer);
        archer.Position = careful.Monsters[0].Position + Vector2.right * 3f;
        careful.SetDestination(archer, new Vector2Int(14, 8), out _);
        bool castFirst = false;
        for (int i = 0; i < 20 && !castFirst; i++)
        {
            careful.Tick(.05f);
            castFirst = careful.CombatEvents.Exists(e => e.Kind == SoulEventKind.Skill && e.Actor == archer);
            careful.CombatEvents.Clear();
        }
        Require(castFirst && careful.HasMoveOrder(archer), "ranged leader engages before moving on");

        // Assist: only the mage can see the enemy (melee sight is short); the swordsman must still join in.
        var assist = new SoulDungeonSession(dungeon, rules, new[] { seraData, ria }, 15);
        assist.AutoExplore = false;
        var caster = assist.Mercenaries[0];
        var sword = assist.Mercenaries[1];
        var foe = assist.Monsters[0];
        caster.Position = foe.Position + Vector2.right * 4.3f;
        sword.Position = foe.Position + Vector2.right * 6f;
        bool joined = false;
        for (int i = 0; i < 100 && !joined; i++)
        {
            assist.Tick(.05f);
            joined = assist.CombatEvents.Exists(e => e.Kind == SoulEventKind.Attack && e.Actor == sword);
            assist.CombatEvents.Clear();
        }
        Require(joined, "melee joins a fight only the ranged ally can see (" + sword.Action + ")");

        // Two melee sent to the same cell do not push each other forever.
        var crowd = new SoulDungeonSession(dungeon, rules, new[] { ria, karonData }, 16);
        crowd.AutoExplore = false;
        var goal = new Vector2Int(1, 1);
        foreach (var hero in crowd.Mercenaries) crowd.SetDestination(hero, goal, out _);
        for (int i = 0; i < 200; i++) crowd.Tick(.05f);
        foreach (var hero in crowd.Mercenaries)
            Require(Vector2.Distance(hero.Position, crowd.Map.Center(goal)) < 2f, $"{hero.Name} reaches a crowded destination ({hero.Position}, {hero.Action})");

        // Spin attack: one swing hits every enemy around.
        var spin = new SoulDungeonSession(dungeon, rules, new[] { karonData }, 14);
        spin.AutoExplore = false;
        var karon = spin.Mercenaries[0];
        karon.LearnedPatterns.Add(whirl);
        var boar = spin.Monsters[0];
        var wolf = spin.Monsters[1];
        karon.Position = boar.Position + Vector2.left * .9f;
        wolf.Position = boar.Position + new Vector2(-.9f, .95f);
        var struck = new HashSet<SoulCombatant>();
        for (int i = 0; i < 80 && struck.Count < 2; i++)
        {
            karon.Stamina = karon.Stats.Total(StatType.MaxStamina);
            spin.Tick(.05f);
            foreach (var e in spin.CombatEvents) if (e.Kind == SoulEventKind.Hit && e.Actor == karon) struck.Add(e.Target);
            spin.CombatEvents.Clear();
        }
        Require(struck.Count >= 2, $"spin attack hits several enemies ({struck.Count})");
    }

    static void ValidateNavigation(SoulDungeonData dungeon, SoulDungeonData ruins, SoulStatRules rules, SoulMercenaryData ria, SoulMercenaryData thorData)
    {
        // Fine grid: an order lands on the exact point, not on the tile center.
        var fine = new SoulDungeonSession(dungeon, rules, new[] { ria }, 21);
        fine.AutoExplore = false;
        var walker = fine.Mercenaries[0];
        foreach (var monster in fine.Monsters) monster.Hp = 0; // nothing to fight: only the walk is checked
        var spot = fine.Map.Center(new Vector2Int(5, 8)) + new Vector2(.3f, -.25f);
        Require(fine.SetDestination(walker, spot, out Vector2 actual) && Vector2.Distance(actual, spot) < .01f, "point order kept as given");
        for (int i = 0; i < 300 && !fine.HasArrived(walker); i++) fine.Tick(.05f);
        for (int i = 0; i < 20; i++) fine.Tick(.05f);
        Require(Vector2.Distance(walker.Position, spot) < .12f, $"walks to the exact point ({walker.Position} vs {spot})");
        Require(fine.Map.NavWidth == fine.Map.Width * SoulMap.NavResolution && SoulMap.NavResolution >= 3, "navigation grid finer than tiles");

        // A click on a wall snaps to the nearest point the body fits and can reach.
        var map = fine.Map;
        Require(map.ClosestReachable(map.Center(new Vector2Int(4, 2)), walker.Position, walker.Stats.Radius, out var snapped)
            && map.Clear(snapped, walker.Stats.Radius) && Vector2.Distance(snapped, map.Center(new Vector2Int(4, 2))) < 1.2f, "wall click snaps next to the wall");

        // Hidden door: closed for path finding until found, then routes go through it.
        var doorMap = new SoulMap("#######\n#..#..#\n#..+..#\n#..#..#\n#######");
        Vector2 left = doorMap.Center(new Vector2Int(1, 2)), right = doorMap.Center(new Vector2Int(5, 2));
        Require(!doorMap.Reachable(left, right, .36f), "undiscovered door blocks routes");
        doorMap.Discover(new Vector2Int(3, 2));
        var path = new SoulPath();
        Require(doorMap.Reachable(left, right, .36f) && doorMap.Plan(left, right, .36f, path) && !path.Partial, "found door opens routes");

        // A body too wide for a gap goes around (1-wide corridor fits every class up to .45).
        var gapMap = new SoulMap("#########\n#.......#\n#.#####.#\n#.......#\n#########");
        Require(gapMap.Reachable(gapMap.Center(new Vector2Int(1, 1)), gapMap.Center(new Vector2Int(7, 3)), .45f), "1-wide corridors fit the widest body");

        // 100× map: long routes are planned in stretches, each fast; reachability is instant.
        var bigSession = new SoulDungeonSession(ruins, rules, new[] { ria }, 23);
        var big = bigSession.Map;
        Vector2 from = big.Center(bigSession.PartyStart), to = big.Center(bigSession.Exit);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Require(big.Reachable(from, to, .45f), "large map: exit reachable");
        double reachMs = watch.Elapsed.TotalMilliseconds;
        big.Plan(from, to, .36f, path); // warm up
        watch.Restart();
        for (int i = 0; i < 20; i++) big.Plan(from, to, .36f, path);
        double planMs = watch.Elapsed.TotalMilliseconds / 20;
        Require(path.Found && path.Partial, "large map: a long route comes in stretches");
        int stretches = 0;
        Vector2 at = from;
        double worst = 0;
        while (stretches < 500)
        {
            var one = System.Diagnostics.Stopwatch.StartNew();
            Require(big.Plan(at, to, .36f, path), $"large map: stretch {stretches} planned");
            worst = System.Math.Max(worst, one.Elapsed.TotalMilliseconds);
            at = path.Points[path.Points.Count - 1];
            stretches++;
            if (!path.Partial) break;
        }
        Require(Vector2.Distance(at, to) < .01f, "large map: stretches end at the exit");
        Debug.Log($"NAV large map {big.Width}x{big.Height} tiles ({big.NavWidth}x{big.NavHeight} nav): reach {reachMs:0.00}ms, stretch {planMs:0.00}ms avg / {worst:0.00}ms worst, {stretches} stretches");
        Require(planMs < 25 && worst < 60, $"large map: planning stays cheap ({planMs:0.00}ms / {worst:0.00}ms)");

        // Many monsters, one party: a tick only touches what is near the party.
        var session = new SoulDungeonSession(ruins, rules, new[] { ria, thorData }, 22);
        for (int i = 0; i < 20; i++) session.Tick(.05f);
        watch.Restart();
        for (int i = 0; i < 200; i++) session.Tick(.05f);
        double tickMs = watch.Elapsed.TotalMilliseconds / 200;
        Debug.Log($"NAV large map session: {session.Monsters.Count} monsters, {tickMs:0.000}ms per tick");
        Require(tickMs < 8, $"large map tick stays cheap ({tickMs:0.000}ms)");
    }

    static void ValidateEncounters(SoulDungeonData ruins, SoulStatRules rules, SoulMercenaryData ria, SoulMercenaryData thorData)
    {
        var warrior = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterGoblinWarrior.asset");
        var archer = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterGoblinArcher.asset");

        // A seed decides the whole map: the same seed rebuilds it, another seed makes a new one.
        var a = SoulDungeonGenerator.Generate(ruins, 101);
        var b = SoulDungeonGenerator.Generate(ruins, 101);
        var c = SoulDungeonGenerator.Generate(ruins, 202);
        Require(a.Rows == b.Rows && a.Spawns.Count == b.Spawns.Count, "same seed, same map");
        Require(a.Rows != c.Rows, "another seed, another map");

        // The way home goes around what is tolled: a ring, two equal ways — the route takes the untolled one.
        var ring = new SoulMap("#########\n#.......#\n#.#####.#\n#.......#\n#########");
        var tolls = new float[ring.Width * ring.Height];
        var way = new List<Vector2Int>();
        for (int x = 3; x <= 5; x++) tolls[1 * ring.Width + x] = 8;
        Require(ring.TileRoute(new Vector2Int(1, 1), new Vector2Int(7, 3), tolls, way) && !way.Contains(new Vector2Int(4, 1)) && way.Contains(new Vector2Int(4, 3)), "the way home avoids the tolled side (top)");
        System.Array.Clear(tolls, 0, tolls.Length);
        for (int x = 3; x <= 5; x++) tolls[3 * ring.Width + x] = 8;
        Require(ring.TileRoute(new Vector2Int(1, 1), new Vector2Int(7, 3), tolls, way) && way.Contains(new Vector2Int(4, 1)) && way[way.Count - 1] == new Vector2Int(7, 3), "the way home avoids the tolled side (bottom)");

        int singles = 0, elites = 0, packs = 0, hordes = 0, bosses = 0, packMonsters = 0, mixed = 0, formations = 0, ordered = 0;
        float nearest = float.MaxValue;
        float far = 1;
        foreach (var e in a.Encounters) far = Mathf.Max(far, Vector2Int.Distance(e.Center, a.Start));
        foreach (var e in a.Encounters)
        {
            switch (e.Kind)
            {
                case SoulEncounterKind.Single:
                    singles++;
                    Require(e.Count == 1 && Vector2Int.Distance(e.Center, a.Start) <= far * .35f, $"singles stay near the start ({e.Name} at {e.Center})");
                    nearest = Mathf.Min(nearest, Vector2Int.Distance(e.Center, a.Start));
                    break;
                case SoulEncounterKind.Elite:
                    elites++;
                    Require(e.Count == 1, "an elite stands alone");
                    break;
                case SoulEncounterKind.Pack:
                    packs++;
                    packMonsters += e.Count;
                    // 3–8; out at the rim two packs can share a room, led by an elite (2 × 8 + 1)
                    Require(e.Count >= 3 && e.Count <= 17, $"a pack is 3–8, at the rim up to two packs and an elite ({e.Name} {e.Count})");
                    var members = a.Spawns.FindAll(spawn => spawn.Group == e.Group);
                    var front = members.FindAll(spawn => spawn.Monster == warrior);
                    var back = members.FindAll(spawn => spawn.Monster == archer);
                    if (front.Count == 0 || back.Count == 0) break;
                    mixed++;
                    if (!e.Formation) break;
                    formations++;
                    // Waiting in formation: the melee line stands nearer the way in (the facing) than the archers.
                    float f = 0, r = 0;
                    foreach (var spawn in front) f += Vector2.Dot(spawn.Cell, spawn.Facing);
                    foreach (var spawn in back) r += Vector2.Dot(spawn.Cell, spawn.Facing);
                    if (f / front.Count > r / back.Count) ordered++;
                    break;
                case SoulEncounterKind.Horde:
                    hordes++;
                    Require(e.Count >= 10, $"a horde is 10+ ({e.Name} {e.Count})");
                    break;
                case SoulEncounterKind.Boss:
                    if (!a.BossArena.Contains(e.Center)) break; // a hidden stage's boss (behind a secret wall)
                    bosses++;
                    Require(a.Spawns.FindAll(spawn => a.BossArena.Contains(spawn.Cell)).Count == 1, "the boss is alone in its arena");
                    break;
            }
        }
        Debug.Log($"ENC seed 101: {a.Spawns.Count} monsters — singles {singles}, elites {elites}, packs {packs} ({packMonsters}), hordes {hordes}, boss {bosses}; goblin packs {mixed}, in formation {formations} ({ordered} ordered)");
        Require(singles > 0 && elites > 0 && hordes >= 3 && bosses == 1, "every encounter size appears");
        Require(singles >= 25 && nearest <= 14, $"plenty of lone monsters around the start ({singles}, nearest {nearest:0} tiles)");
        Require(packMonsters > a.Spawns.Count * .55f, $"most monsters are in packs ({packMonsters}/{a.Spawns.Count})");
        Require(mixed > 0 && formations > 0 && ordered >= formations * .8f, $"goblin packs: warriors in front, archers behind ({ordered}/{formations})");

        // A pack fights as one: the member that sees the party pulls in the one behind the wall; a lone monster
        // in the same spot keeps waiting.
        var room = ScriptableObject.CreateInstance<SoulDungeonData>();
        room.MapRows = "##############\n#......#.....#\n#......#.....#\n#............#\n##############";
        room.PartyStart = new Vector2Int(1, 1); room.Exit = new Vector2Int(12, 3);
        room.Monsters = new[]
        {
            new SoulMonsterSpawn { Monster = warrior, Cell = new Vector2Int(4, 1), Group = 1 },
            new SoulMonsterSpawn { Monster = warrior, Cell = new Vector2Int(10, 1), Group = 1 },
            new SoulMonsterSpawn { Monster = warrior, Cell = new Vector2Int(11, 2) },
        };
        var alert = new SoulDungeonSession(room, rules, new[] { ria }, 31);
        alert.AutoExplore = false;
        var hero = alert.Mercenaries[0];
        var hidden = alert.Monsters[1];
        var lone = alert.Monsters[2];
        Require(!alert.Map.Sight(hidden.Position, hero.Position), "pack test: the second member cannot see the party");
        bool joined = false;
        for (int i = 0; i < 60 && !joined; i++)
        {
            hero.Hp = hero.Stats.Total(StatType.MaxHp);
            alert.Tick(.05f);
            joined = hidden.CurrentTarget == hero;
        }
        Require(joined && lone.CurrentTarget == null, $"a pack engages together ({hidden.Action} / lone {lone.Action})");
        UnityEngine.Object.DestroyImmediate(room);
    }

    static void ValidateTelegraphs(SoulStatRules rules, SoulMercenaryData ria, SoulMercenaryData thorData)
    {
        // Area shapes (map units, facing +x).
        var t = new SoulTelegraph { Origin = Vector2.zero, Direction = Vector2.right };
        t.Shape = SoulAreaShape.Circle; t.Anchor = SoulAreaAnchor.Self; t.Size = 2;
        Require(t.Contains(new Vector2(1.5f, 0), 0) && !t.Contains(new Vector2(2.5f, 0), 0) && t.Contains(new Vector2(2.5f, 0), .6f), "circle area");
        t.Shape = SoulAreaShape.Cone; t.Anchor = SoulAreaAnchor.Forward; t.Size = 3; t.Width = 90;
        Require(t.Contains(new Vector2(2, .5f), 0) && !t.Contains(new Vector2(1, 2.5f), 0) && !t.Contains(new Vector2(-1, 0), 0), "cone area");
        t.Shape = SoulAreaShape.Box; t.Size = 4; t.Width = 2;
        Require(t.Contains(new Vector2(3, .8f), 0) && !t.Contains(new Vector2(3, 1.3f), 0) && !t.Contains(new Vector2(-.5f, 0), 0), "box ahead");
        t.Anchor = SoulAreaAnchor.Self;
        Require(t.Contains(new Vector2(-1.5f, 0), 0) && !t.Contains(new Vector2(-2.5f, 0), 0), "box around the caster");

        // The boss: patterns in a fixed order, and mercenaries who see the red area step out before the hit.
        var boss = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterGuardian.asset");
        var arena = ScriptableObject.CreateInstance<SoulDungeonData>();
        var rows = new System.Text.StringBuilder();
        for (int y = 0; y < 16; y++) rows.Append(y == 0 || y == 15 ? new string('#', 26) : "#" + new string('.', 24) + "#").Append(y < 15 ? "\n" : "");
        arena.MapRows = rows.ToString();
        arena.PartyStart = new Vector2Int(5, 8); arena.Exit = new Vector2Int(24, 1);
        arena.Monsters = new[] { new SoulMonsterSpawn { Monster = boss, Cell = new Vector2Int(13, 8) } };
        var fight = new SoulDungeonSession(arena, rules, new[] { thorData, ria }, 32);
        fight.AutoExplore = false;
        for (int i = 0; i < fight.Mercenaries.Count; i++) fight.Mercenaries[i].Position = fight.Monsters[0].Position + new Vector2(-2.5f, i * 1.2f - .6f);
        foreach (var hero in fight.Mercenaries) hero.StatusImmunity[SoulStatus.Fear] = 9999; // dodging is measured, not nerve
        foreach (var hero in fight.Mercenaries) // reading red areas is measured: no follow-ups pulling the unit back into a swing
            foreach (var pattern in hero.AllPatterns()) if (pattern.Category == SoulPatternCategory.Chain || pattern.Category == SoulPatternCategory.Support) hero.ToggleLock(pattern);
        var order = new List<string>();
        int struck = 0, inside = 0;
        var caughtBy = new List<string>();
        for (int i = 0; i < 1600 && order.Count < 8; i++)
        {
            foreach (var hero in fight.Mercenaries) { hero.Hp = hero.Stats.Total(StatType.MaxHp); hero.Stamina = hero.Stats.Total(StatType.MaxStamina); } // measure dodging, not survival
            fight.Tick(.05f);
            foreach (var e in fight.CombatEvents) if (e.Kind == SoulEventKind.Telegraph && !string.IsNullOrEmpty(e.Label)) order.Add(e.Label);
            // On the tick of a hit the boss did nothing else (it was winding up): every result it caused is the area's.
            if (fight.CombatEvents.Exists(e => e.Kind == SoulEventKind.Impact))
            {
                struck++;
                var caught = new HashSet<SoulCombatant>();
                foreach (var e in fight.CombatEvents)
                    if (e.Actor == fight.Monsters[0] && e.Target is SoulMercenary
                        && (e.Kind == SoulEventKind.Hit || e.Kind == SoulEventKind.Miss || e.Kind == SoulEventKind.Evade || e.Kind == SoulEventKind.Roll || e.Kind == SoulEventKind.Guard))
                        caught.Add(e.Target);
                inside += caught.Count;
                foreach (var who in caught) caughtBy.Add(((SoulMercenary)who).Name);
            }
            fight.CombatEvents.Clear();
        }
        var names = new List<string>();
        foreach (var pattern in boss.Telegraphs) names.Add(pattern.SkillName);
        var slow = fight.Mercenaries[0];
        var quick = fight.Mercenaries[1];
        Debug.Log($"TELEGRAPH reaction: {slow.Name} agility {slow.Stats.Total(StatType.Agility):0} → {SoulDungeonSession.ReactionTime(slow):0.00}s, {quick.Name} agility {quick.Stats.Total(StatType.Agility):0} → {SoulDungeonSession.ReactionTime(quick):0.00}s; caught {string.Join(", ", caughtBy)}");
        Require(SoulDungeonSession.ReactionTime(slow) > SoulDungeonSession.ReactionTime(quick) + .1f, "agility: the nimble mercenary reacts sooner");

        // Alone against the boss, eight patterns each: the slow tank is caught more often than the nimble fighter.
        var karonData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryKaron.asset");
        int Caught(SoulMercenaryData who, int seed)
        {
            var solo = new SoulDungeonSession(arena, rules, new[] { who }, seed);
            solo.AutoExplore = false;
            var hero = solo.Mercenaries[0];
            // Only the reaction is measured: no presence fear (checked in ValidateStatusPatterns), and patterns
            // that spend stamina on something else than getting out of the way are locked.
            hero.StatusImmunity[SoulStatus.Fear] = 9999;
            foreach (var pattern in hero.AllPatterns()) if (pattern.Category == SoulPatternCategory.Support || pattern.Ambush) hero.ToggleLock(pattern);
            hero.Position = solo.Monsters[0].Position + Vector2.left * 1.5f;
            int hits = 0, patterns = 0;
            for (int i = 0; i < 2400 && patterns < 8; i++)
            {
                hero.Hp = hero.Stats.Total(StatType.MaxHp);
                hero.Stamina = hero.Stats.Total(StatType.MaxStamina); // reaction is measured, not the stamina economy
                solo.Tick(.05f);
                if (solo.CombatEvents.Exists(e => e.Kind == SoulEventKind.Impact))
                {
                    patterns++;
                    if (solo.CombatEvents.Exists(e => e.Actor == solo.Monsters[0] && e.Target == hero && e.Kind != SoulEventKind.Telegraph && e.Kind != SoulEventKind.Impact)) hits++;
                }
                solo.CombatEvents.Clear();
            }
            return hits;
        }
        int tankHits = Caught(thorData, 41) + Caught(thorData, 42), nimbleHits = Caught(karonData, 41) + Caught(karonData, 42);
        Debug.Log($"TELEGRAPH solo, 16 patterns each: 토르 caught {tankHits}, 카론 caught {nimbleHits}");
        Require(nimbleHits < tankHits, $"high agility dodges more (토르 {tankHits}, 카론 {nimbleHits})");
        Require(order.Count >= names.Count, $"the boss uses its patterns ({string.Join(", ", order)})");
        for (int i = 0; i < names.Count; i++) Require(order[i] == names[i], $"boss patterns come in a fixed order ({string.Join(", ", order)})");
        float ratio = inside / (float)Mathf.Max(1, struck * fight.Mercenaries.Count);
        Debug.Log($"TELEGRAPH {struck} hits, mercenaries caught {inside} ({ratio:P0}); order {string.Join(" → ", order)}");
        // Reaction depends on agility (a slow tank is caught often), so only "some get out" is required here.
        // The dwarf tank (agility 2) reads almost nothing and is caught every time; the rest must get out often.
        Require(struck >= names.Count && ratio < .8f, $"mercenaries step out of red areas ({inside} caught in {struck} hits)");

        // Stunned, a mercenary cannot step out: the hit lands.
        var stunned = new SoulDungeonSession(arena, rules, new[] { thorData }, 33);
        stunned.AutoExplore = false;
        var victim = stunned.Mercenaries[0];
        victim.Position = stunned.Monsters[0].Position + Vector2.left * 1.2f;
        victim.Statuses.Add(new SoulStatusState { Kind = SoulStatus.Stun, Remaining = 999 });
        bool hit = false;
        for (int i = 0; i < 200 && !hit; i++)
        {
            victim.Hp = victim.Stats.Total(StatType.MaxHp);
            stunned.Tick(.05f);
            hit = stunned.CombatEvents.Exists(e => e.Kind == SoulEventKind.Hit && e.Target == victim && e.Actor == stunned.Monsters[0])
                && stunned.Telegraphs.Exists(x => x.Struck);
            stunned.CombatEvents.Clear();
        }
        Require(hit, "a stunned mercenary is caught by the red area");
        UnityEngine.Object.DestroyImmediate(arena);
    }

    // Encounter weight: an elite alone must hit a party harder than a goblin pack, the boss far harder than an
    // elite. Measured as the damage the same party takes until the monsters fall (the party is kept alive).
    // The village board: grades, targets and rewards vary; a finished notice pays gold and its extra.
    static void ValidateBoard(SoulCampaign campaign)
    {
        campaign.Active.Clear();
        campaign.RefreshBoard();
        Require(campaign.Board.Count == 4 && campaign.QuestSlots == 1, $"board 1: four notices, one taken at a time ({campaign.Board.Count})");
        var kinds = new HashSet<SoulQuestKind>();
        var bonuses = new HashSet<SoulQuestBonus>();
        for (int i = 0; i < 60; i++)
        {
            campaign.RefreshBoard();
            foreach (var q in campaign.Board)
            {
                Require(q.Grade >= 1 && q.Grade <= 3 && q.Reward > 0 && !string.IsNullOrEmpty(q.Title) && q.Floor >= 1, $"a sound notice ({q.Title} {q.GradeName})");
                kinds.Add(q.Kind); bonuses.Add(q.Bonus);
            }
        }
        Require(kinds.Count >= 5 && bonuses.Count >= 3, $"the board varies ({kinds.Count} kinds, {bonuses.Count} rewards)");
        var quest = campaign.Board[0];
        Require(campaign.Accept(quest) && !campaign.Accept(campaign.Board[0]), "one notice at a time on a small board");
        quest.Progress = quest.Goal;
        int gold = campaign.Gold, stones = campaign.Stones, items = campaign.Inventory.Count, books = campaign.Books.Count, vault = campaign.Vault.Count;
        int supply = quest.Bonus == SoulQuestBonus.Supply ? campaign.Supply(quest.BonusId) : 0;
        Require(campaign.Claim(quest) && campaign.Gold == gold + campaign.QuestReward(quest), "the notice pays its gold");
        bool extra = quest.Bonus switch
        {
            SoulQuestBonus.SoulStone => campaign.Stones == stones + quest.BonusCount,
            SoulQuestBonus.Supply => campaign.Supply(quest.BonusId) == supply + quest.BonusCount,
            SoulQuestBonus.Equipment => campaign.Inventory.Count == items + 1,
            SoulQuestBonus.SkillBook => campaign.Books.Count == books + 1,
            SoulQuestBonus.Soul => campaign.Vault.Count == vault + 1,
            _ => true,
        };
        Require(extra, $"and its extra ({quest.Bonus})");
        ValidatePerks(campaign);
    }

    // Guild abilities: gated by guild level and renown, paid in gold, felt where they apply.
    static void ValidatePerks(SoulCampaign campaign)
    {
        campaign.SetAmount(eItem.Gold, 100000);
        campaign.Perks.Remove(SoulPerk.PartySize);
        campaign.Levels[SoulBuildingKind.Guild] = 1;
        Require(campaign.BuyPerk(SoulPerk.PartySize) && campaign.PerkBlock(SoulPerk.PartySize) != null, "a fourth member at guild level 1, a fifth wants guild level 2");
        campaign.Perks.Remove(SoulPerk.PartySize);
        campaign.Levels[SoulBuildingKind.Guild] = 3;
        campaign.Renown = 1000;
        Require(campaign.BuyPerk(SoulPerk.PartySize) && campaign.BuyPerk(SoulPerk.PartySize) && campaign.PartySize == 5 && !campaign.BuyPerk(SoulPerk.PartySize), "파티 정원: 3 → 5");
        Require(campaign.PerkBlock(SoulPerk.ReturnScrollShop) != null, "귀환 두루마리 조달 wants guild level 4");
        campaign.Levels[SoulBuildingKind.Guild] = 5;
        var training = SoulCampaign.Trainings[1];
        var hero = campaign.Roster[0];
        int cost = campaign.TrainingCost(hero, training);
        Require(campaign.BuyPerk(SoulPerk.TrainingCost) && campaign.TrainingCost(hero, training) < cost, "훈련 비용 절감");
        float strength = hero.Stats.Total(StatType.Strength);
        Require(campaign.BuyPerk(SoulPerk.Strength) && Mathf.Abs(hero.Stats.Total(StatType.Strength) - strength - 1) < .01f, "근력 +1 on every mercenary");
        Require(!campaign.BuyPerk(SoulPerk.CarryStack) && campaign.CarryLimit == SoulCampaign.CarryPerKind, "준비물 가방 is gone: 3 of a kind");
        Require(campaign.BuyPerk(SoulPerk.ReturnScrollShop) && campaign.SupplyStock().Exists(s => s.Id == SoulSupplies.ReturnScroll), "귀환 두루마리 조달");
        campaign.BuyPerk(SoulPerk.PotionPower); campaign.BuyPerk(SoulPerk.CautiousRetreat); campaign.BuyPerk(SoulPerk.SoulStones);
        var perks = campaign.ExpeditionPerks();
        Require(Mathf.Abs(perks.PotionPower - 1.1f) < .001f && Mathf.Abs(perks.CrisisHealth - .3f) < .001f && perks.FreeStones == 1, "expedition perks");
        // a save from when the company started with two parties keeps its parties
        var old = SoulSave.Write(campaign);
        old.PartyBase = 0;
        var migrated = SoulSave.Read(old, campaign.Data);
        Require(migrated.PartyLimit == Mathf.Min(SoulCampaign.MaxParties, campaign.PartyLimit + 1), "an older save keeps the second party it started with");
    }

    static void ValidateEncounterStrength(SoulStatRules rules, SoulMercenaryData ria, SoulMercenaryData thorData)
    {
        var karonData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryKaron.asset");
        var warrior = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterGoblinWarrior.asset");
        var archer = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterGoblinArcher.asset");
        var elite = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterGoblinChief.asset");
        var boss = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterGuardian.asset");
        var party = new[] { thorData, ria, karonData };

        (float taken, float seconds) Fight(params SoulMonsterData[] monsters)
        {
            var arena = ScriptableObject.CreateInstance<SoulDungeonData>();
            var rows = new System.Text.StringBuilder();
            for (int y = 0; y < 18; y++) rows.Append(y == 0 || y == 17 ? new string('#', 30) : "#" + new string('.', 28) + "#").Append(y < 17 ? "\n" : "");
            arena.MapRows = rows.ToString();
            arena.PartyStart = new Vector2Int(6, 9); arena.Exit = new Vector2Int(28, 1);
            arena.Monsters = new SoulMonsterSpawn[monsters.Length];
            for (int i = 0; i < monsters.Length; i++)
                arena.Monsters[i] = new SoulMonsterSpawn { Monster = monsters[i], Cell = new Vector2Int(15 + i % 2 * 2, 6 + i / 2 * 2 + i % 2) };
            var fight = new SoulDungeonSession(arena, rules, party, 77);
            fight.AutoExplore = false;
            for (int i = 0; i < fight.Mercenaries.Count; i++) fight.Mercenaries[i].Position = fight.Map.Center(new Vector2Int(13, 6 + i * 2)); // inside perception: every fight starts at once
            float taken = 0, time = 0, lastBlow = 0, monsterHp = float.MaxValue;
            // ends when the monsters fall, or when nothing has been hit for 15 s (a straggler backed out of sight;
            // without auto exploration nobody goes after it)
            while (time < 600 && time - lastBlow < 15 && fight.Monsters.Exists(m => m.Alive))
            {
                float hpNow = 0;
                foreach (var m in fight.Monsters) hpNow += m.Hp;
                if (hpNow < monsterHp - .01f) lastBlow = time;
                monsterHp = hpNow;
                var before = new float[fight.Mercenaries.Count];
                for (int i = 0; i < before.Length; i++) before[i] = fight.Mercenaries[i].Hp;
                fight.Tick(.05f);
                time += .05f;
                for (int i = 0; i < before.Length; i++)
                {
                    var hero = fight.Mercenaries[i];
                    if (before[i] - hero.Hp > .01f) lastBlow = time;
                    taken += Mathf.Max(0, before[i] - hero.Hp);
                    hero.Hp = hero.Stats.Total(StatType.MaxHp);
                    hero.HealWounds(SoulMercenary.MaxWounds); // kept alive and unhurt: the monsters are measured
                }
                fight.CombatEvents.Clear();
            }
            UnityEngine.Object.DestroyImmediate(arena);
            return (taken, time);
        }

        var pack = Fight(warrior, warrior, warrior, archer, archer);
        var alone = Fight(elite);
        var guardian = Fight(boss);
        Debug.Log($"STRENGTH party damage taken / fight length: goblin pack (5) {pack.taken:0} in {pack.seconds:0.0}s, elite {alone.taken:0} in {alone.seconds:0.0}s, boss {guardian.taken:0} in {guardian.seconds:0.0}s");
        Require(alone.taken > pack.taken * 1.2f, $"an elite alone is stronger than a goblin pack ({alone.taken:0} vs {pack.taken:0})");
        Require(guardian.taken > alone.taken * 2f, $"the boss is far stronger than an elite ({guardian.taken:0} vs {alone.taken:0})");
    }

    // Locks, debuff grades, poison stages, support patterns, ambush and fear at first sight.
    static void ValidateStatusPatterns(SoulDungeonData dungeon, SoulStatRules rules, SoulMercenaryData ria, SoulMercenaryData thorData)
    {
        var karonData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryKaron.asset");
        var lukaData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryLuka.asset");
        var warrior = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterGoblinWarrior.asset");
        var elite = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterGoblinChief.asset");
        var threatPattern = AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/Taunt.asset");
        var random = new System.Random(3);

        // Lock: still owned, never used.
        var basic = new SoulDungeonSession(dungeon, rules, new[] { ria, thorData }, 4);
        var thor = basic.Mercenaries[1];
        Require(thor.Patterns().Contains(threatPattern), "thor owns 위협");
        thor.ToggleLock(threatPattern);
        Require(!thor.Patterns().Contains(threatPattern) && thor.AllPatterns().Contains(threatPattern), "a locked pattern stays owned but is not used");
        thor.ToggleLock(threatPattern);
        Require(thor.Patterns().Contains(threatPattern), "unlocking gives it back");

        // Grades: slow 2 takes 25% of move speed; fear 1/3 takes 10%/30% of attack.
        var ria1 = basic.Mercenaries[0];
        float move = ria1.Stats.Total(StatType.MoveSpeed), attack = ria1.Stats.Total(StatType.Attack);
        Require(SoulCombat.ApplyStatus(ria1, SoulStatus.Slow, 1, 5, 0, random, 2) && ria1.Grade(SoulStatus.Slow) >= 1, "slow applies with a grade");
        Require(ria1.Stats.Total(StatType.MoveSpeed) < move * .9f, $"slow lowers move speed ({ria1.Stats.Total(StatType.MoveSpeed):0.00} of {move:0.00})");
        ria1.Statuses.Clear(); SoulCombat.RefreshStatusEffects(ria1);
        Require(Mathf.Abs(ria1.Stats.Total(StatType.MoveSpeed) - move) < .01f, "slow ends cleanly");
        SoulCombat.ApplyStatus(ria1, SoulStatus.Fear, 1, 5, 0, random, 1);
        float fear1 = ria1.Stats.Total(StatType.Attack);
        ria1.Statuses.Clear(); ria1.StatusImmunity.Clear(); SoulCombat.RefreshStatusEffects(ria1);
        SoulCombat.ApplyStatus(ria1, SoulStatus.Fear, 1, 5, 0, random, 3);
        float fear3 = ria1.Stats.Total(StatType.Attack);
        Debug.Log($"STATUS fear: attack {attack:0.0} → grade 1 {fear1:0.0}, grade {ria1.Grade(SoulStatus.Fear)} {fear3:0.0}");
        Require(fear1 < attack && fear3 < fear1, "a higher fear grade weakens more");
        ria1.Statuses.Clear(); ria1.StatusImmunity.Clear(); SoulCombat.RefreshStatusEffects(ria1);

        // Poison: a share of max HP a second, then weakness after 4 s, then no regeneration after 8 s.
        ria1.Hp = ria1.Stats.Total(StatType.MaxHp);
        SoulCombat.ApplyStatus(ria1, SoulStatus.Poison, 1, 20, 0, random, 1);
        float before = ria1.Hp;
        for (int i = 0; i < 20; i++) SoulCombat.TickResources(ria1, .05f);
        Require(before - ria1.Hp > ria1.Stats.Total(StatType.MaxHp) * .005f, $"poison takes a share of max HP ({before - ria1.Hp:0.0})");
        for (int i = 0; i < 70; i++) SoulCombat.TickResources(ria1, .05f);
        Require(ria1.Stats.Total(StatType.Attack) < attack, "long poison weakens");
        for (int i = 0; i < 90; i++) SoulCombat.TickResources(ria1, .05f);
        Require(ria1.Stats.Total(StatType.HpRegen) <= .001f, "longer poison stops regeneration");
        ria1.Statuses.Clear(); SoulCombat.RefreshStatusEffects(ria1);

        // Arena helper: an open room, the party two cells from the monsters.
        SoulDungeonSession Arena(SoulMercenaryData[] party, params SoulMonsterData[] monsters)
        {
            var arena = ScriptableObject.CreateInstance<SoulDungeonData>();
            var rows = new System.Text.StringBuilder();
            for (int y = 0; y < 14; y++) rows.Append(y == 0 || y == 13 ? new string('#', 24) : "#" + new string('.', 22) + "#").Append(y < 13 ? "\n" : "");
            arena.MapRows = rows.ToString();
            arena.PartyStart = new Vector2Int(4, 7); arena.Exit = new Vector2Int(22, 1);
            arena.Monsters = new SoulMonsterSpawn[monsters.Length];
            for (int i = 0; i < monsters.Length; i++) arena.Monsters[i] = new SoulMonsterSpawn { Monster = monsters[i], Cell = new Vector2Int(14, 6 + i * 2) };
            var fight = new SoulDungeonSession(arena, rules, party, 91);
            fight.AutoExplore = false;
            for (int i = 0; i < fight.Mercenaries.Count; i++) fight.Mercenaries[i].Position = fight.Map.Center(new Vector2Int(monsters.Length > 0 ? 12 : 5, 6 + i * 2));
            return fight;
        }

        // First aid: out of combat the support heals the most hurt ally by 5% of max HP.
        var calm = Arena(new[] { lukaData, ria });
        var hurt = calm.Mercenaries[1];
        hurt.Hp = hurt.Stats.Total(StatType.MaxHp) * .5f;
        float hurtBefore = hurt.Hp;
        for (int i = 0; i < 6; i++) calm.Tick(.05f);
        Require(hurt.Hp >= hurtBefore + hurt.Stats.Total(StatType.MaxHp) * .045f, $"first aid heals out of combat ({hurtBefore:0} → {hurt.Hp:0})");

        // Encourage: in a fight the support raises allies' stamina regeneration.
        var rally = Arena(new[] { lukaData, ria }, warrior);
        bool encouraged = false;
        for (int i = 0; i < 80 && !encouraged; i++) { rally.Tick(.05f); encouraged = rally.Mercenaries[1].TimedBuffs.Exists(b => b.Id == "격려"); foreach (var m in rally.Mercenaries) m.Hp = m.Stats.Total(StatType.MaxHp); }
        Require(encouraged, "encourage buffs allies in combat");

        // Ambush and coating: the beastkin blinks behind its target, and coats its claws with poison.
        var stab = Arena(new[] { karonData }, warrior);
        var karon = stab.Mercenaries[0];
        bool blinked = false, coated = false, poisoned = false;
        for (int i = 0; i < 300 && !(blinked && poisoned); i++)
        {
            stab.Tick(.05f);
            blinked |= stab.CombatEvents.Exists(e => e.Kind == SoulEventKind.Leap && e.Actor == karon);
            coated |= karon.ImbueRemaining > 0;
            poisoned |= stab.Monsters[0].Has(SoulStatus.Poison);
            stab.CombatEvents.Clear();
            karon.Hp = karon.Stats.Total(StatType.MaxHp);
            if (!stab.Monsters[0].Alive) break;
        }
        Require(blinked, "ambush blinks behind the target");
        // Out of breath (wounds make patterns dearer): no blinking at all until the strike can be paid for.
        var tired = Arena(new[] { karonData }, warrior);
        var winded = tired.Mercenaries[0];
        winded.AddWounds(2);
        int leaps = 0;
        for (int i = 0; i < 60; i++)
        {
            winded.Stamina = 0;
            winded.Hp = winded.Stats.Total(StatType.MaxHp);
            tired.Tick(.05f);
            leaps += tired.CombatEvents.FindAll(e => e.Kind == SoulEventKind.Leap && e.Actor == winded).Count;
            tired.CombatEvents.Clear();
        }
        Require(leaps == 0, $"no ambush blink without the stamina to strike ({leaps} blinks)");
        Require(coated, "독 바르기 coats the attacks");
        Debug.Log($"STATUS ambush {blinked}, coated {coated}, poisoned {poisoned}");

        // Presence: an elite frightens a mercenary of little will at first sight.
        var scare = Arena(new[] { lukaData }, elite);
        var luka = scare.Mercenaries[0];
        for (int i = 0; i < 10 && !luka.Has(SoulStatus.Fear); i++) scare.Tick(.05f);
        Debug.Log($"STATUS presence: elite threat {scare.Monsters[0].Stats.Total(StatType.Threat):0.00} vs {luka.Name} nerve {SoulDungeonSession.Nerve(luka):0.00} → fear grade {luka.Grade(SoulStatus.Fear)}");
        Require(luka.Has(SoulStatus.Fear), "an elite frightens a timid mercenary");
    }

    // Stamina economy (fight < out of combat < rest), evasion that costs more when chained and waits for the
    // motion to end, keeping stamina when HP allows, magic as areas, spending MP where it counts, and a
    // monster hit from beyond its perception turning on the attacker.
    static void ValidateStaminaAndMana(SoulDungeonData dungeon, SoulStatRules rules, SoulMercenaryData ria, SoulMercenaryData thorData)
    {
        var seraData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenarySera.asset");
        var warrior = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterGoblinWarrior.asset");
        var archer = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterGoblinArcher.asset");
        var elite = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterGoblinChief.asset");

        // Conversions: stamina regeneration reads vitality; recovery leans to MP.
        float Factor(StatType output, StatType input)
        {
            foreach (var conversion in SoulStatRules.Effective(rules))
                if (conversion.Output == output) foreach (var term in conversion.Formula.Terms) if (term.Stat == input) return term.Factor;
            return 0;
        }
        Require(Factor(StatType.StaminaRegen, StatType.Vitality) > 0, "stamina regeneration grows with vitality");
        Require(Factor(StatType.MpRegen, StatType.Recovery) > Factor(StatType.MpRegen, StatType.Magic) && Factor(StatType.MpRegen, StatType.Recovery) < .1f, "MP comes back slowly, mostly with recovery");

        SoulDungeonSession Arena(SoulMercenaryData[] party, Vector2Int[] cells, params SoulMonsterData[] monsters)
        {
            var arena = ScriptableObject.CreateInstance<SoulDungeonData>();
            var rows = new System.Text.StringBuilder();
            for (int y = 0; y < 14; y++) rows.Append(y == 0 || y == 13 ? new string('#', 26) : "#" + new string('.', 24) + "#").Append(y < 13 ? "\n" : "");
            arena.MapRows = rows.ToString();
            arena.PartyStart = new Vector2Int(3, 7); arena.Exit = new Vector2Int(24, 1);
            arena.Monsters = new SoulMonsterSpawn[monsters.Length];
            for (int i = 0; i < monsters.Length; i++) arena.Monsters[i] = new SoulMonsterSpawn { Monster = monsters[i], Cell = cells[i] };
            var fight = new SoulDungeonSession(arena, rules, party, 57);
            fight.AutoExplore = false;
            foreach (var hero in fight.Mercenaries) hero.StatusImmunity[SoulStatus.Fear] = 9999; // nerve is checked elsewhere
            return fight;
        }

        // Stamina comes back slower in a fight than out of one.
        var quiet = Arena(new[] { ria }, new Vector2Int[0]);
        var rested = quiet.Mercenaries[0];
        rested.Stamina = 0;
        for (int i = 0; i < 20; i++) quiet.Tick(.05f);
        var busy = Arena(new[] { ria }, new[] { new Vector2Int(8, 7) }, warrior);
        var fighter = busy.Mercenaries[0];
        fighter.Position = busy.Map.Center(new Vector2Int(6, 7));
        foreach (var pattern in fighter.AllPatterns()) if (pattern.StaminaRatio > 0) fighter.ToggleLock(pattern); // regeneration alone
        fighter.Stamina = 0;
        for (int i = 0; i < 20; i++) { fighter.Stamina = Mathf.Max(fighter.Stamina, 0); busy.Tick(.05f); fighter.Hp = fighter.Stats.Total(StatType.MaxHp); }
        Debug.Log($"STAMINA regen 1 s: out of combat {rested.Stamina:0.00}, in combat ≤ {fighter.Stamina:0.00}");
        Require(fighter.Stamina < rested.Stamina * .8f, "stamina comes back slower in a fight");

        // Evasion: never a second one during the motion, and chained ones cost more.
        var dodgy = Arena(new[] { seraData }, new[] { new Vector2Int(8, 6), new Vector2Int(8, 8), new Vector2Int(9, 7) }, warrior, warrior, warrior);
        var sera = dodgy.Mercenaries[0];
        sera.Position = dodgy.Map.Center(new Vector2Int(6, 7));
        var times = new List<float>();
        float clock = 0, staminaSpent = 0;
        for (int i = 0; i < 400; i++)
        {
            float before = sera.Stamina;
            dodgy.Tick(.05f); clock += .05f;
            if (dodgy.CombatEvents.Exists(e => (e.Kind == SoulEventKind.Evade || e.Kind == SoulEventKind.Roll) && e.Target == sera))
            {
                times.Add(clock);
                staminaSpent += Mathf.Max(0, before - sera.Stamina);
            }
            dodgy.CombatEvents.Clear();
            sera.Hp = sera.Stats.Total(StatType.MaxHp) * .3f; // desperate: evades whenever it can pay, so the spacing shows
        }
        float gap = float.MaxValue;
        for (int i = 1; i < times.Count; i++) gap = Mathf.Min(gap, times[i] - times[i - 1]);
        Debug.Log($"EVADE {times.Count} evasions, shortest gap {(times.Count > 1 ? gap : 0):0.00}s, stamina spent {staminaSpent:0.0}");
        Require(times.Count < 2 || gap >= .4f, $"no evasion while still evading (gap {gap:0.00}s)");

        // Plenty of HP, little stamina: take the hit.
        var saver = Arena(new[] { seraData }, new[] { new Vector2Int(8, 7) }, warrior);
        var calm = saver.Mercenaries[0];
        calm.Position = saver.Map.Center(new Vector2Int(7, 7));
        int evasions = 0;
        for (int i = 0; i < 120; i++)
        {
            calm.Stamina = calm.Stats.Total(StatType.MaxStamina) * .3f; // below the reserve
            calm.Hp = calm.Stats.Total(StatType.MaxHp);
            saver.Tick(.05f);
            evasions += saver.CombatEvents.FindAll(e => (e.Kind == SoulEventKind.Evade || e.Kind == SoulEventKind.Roll) && e.Target == calm).Count;
            saver.CombatEvents.Clear();
        }
        Require(evasions == 0, $"with plenty of HP and little stamina the hit is taken ({evasions} evasions)");

        // Magic is an area: one fire bolt reaches both goblins standing together.
        var blast = Arena(new[] { seraData }, new[] { new Vector2Int(12, 7), new Vector2Int(13, 7) }, warrior, warrior);
        var mage = blast.Mercenaries[0];
        Classic(mage);
        mage.Position = blast.Map.Center(new Vector2Int(9, 7));
        bool circle = false;
        for (int i = 0; i < 120 && !circle; i++)
        {
            blast.Tick(.05f);
            circle = blast.Telegraphs.Exists(t => t.Caster == mage && t.Shape == SoulAreaShape.Circle && t.Anchor == SoulAreaAnchor.Target);
            mage.Hp = mage.Stats.Total(StatType.MaxHp);
        }
        Require(circle, "attack magic lands as a circle on the target");
        int cursed = 0;
        for (int i = 0; i < 900 && cursed < 2; i++)
        {
            // the bolts come first; kept alive and the MP kept up, the curse follows between them
            blast.Tick(.05f); mage.Hp = mage.Stats.Total(StatType.MaxHp); mage.Mp = mage.Stats.Total(StatType.MaxMp);
            foreach (var m in blast.Monsters) m.Hp = m.Stats.Total(StatType.MaxHp);
            cursed = blast.Monsters.FindAll(m => m.Has(SoulStatus.Weaken)).Count;
        }
        Require(cursed >= 2, $"the curse circle lands on everyone inside ({cursed} cursed)");

        // MP: with little to spare, a lone ordinary goblin is not worth a spell.
        var thrift = Arena(new[] { seraData }, new[] { new Vector2Int(12, 7) }, archer);
        var saving = thrift.Mercenaries[0];
        Classic(saving);
        saving.Position = thrift.Map.Center(new Vector2Int(9, 7));
        foreach (var pattern in saving.AllPatterns()) if (pattern.ManaRatio > 0) saving.ToggleLock(pattern);
        saving.Mp = saving.Stats.Total(StatType.MaxMp) * .3f; // below the reserve: even the cheap 마력탄 waits
        bool spent = false;
        for (int i = 0; i < 20; i++) { thrift.Tick(.05f); spent |= thrift.CombatEvents.Exists(e => e.Kind == SoulEventKind.Skill && e.Actor == saving && !e.Support); thrift.CombatEvents.Clear(); saving.Hp = saving.Stats.Total(StatType.MaxHp); }
        Require(!spent, $"MP is kept for when it counts ({saving.Action})");

        // 무념 동작: with a certain chance a pattern is paid for with nothing.
        var free = Arena(new[] { ria }, new Vector2Int[0]).Mercenaries[0];
        var slash = AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/Slash.asset");
        free.AddTimedBuff("test_free", new[] { new SoulStatBonus { Stat = StatType.FreePatternChance, Value = 1 } }, 99);
        float full = free.Stamina;
        SoulCombat.Pay(free, slash);
        Require(Mathf.Approximately(free.Stamina, full), "a free pattern costs no stamina");

        // 호흡 조절: in a fight, running dry, a quarter of the stamina comes back.
        var winded = Arena(new[] { ria }, new[] { new Vector2Int(8, 7) }, warrior);
        var gasp = winded.Mercenaries[0];
        gasp.Position = winded.Map.Center(new Vector2Int(6, 7));
        gasp.Stamina = gasp.Stats.Total(StatType.MaxStamina) * .1f;
        bool breathed = false;
        for (int i = 0; i < 40 && !breathed; i++)
        {
            winded.Tick(.05f);
            breathed = gasp.Stamina >= gasp.Stats.Total(StatType.MaxStamina) * .3f;
            gasp.Hp = gasp.Stats.Total(StatType.MaxHp);
        }
        Require(breathed, $"second wind restores stamina when running dry ({gasp.Stamina:0}/{gasp.Stats.Total(StatType.MaxStamina):0})");

        // 회피 반격: an evasion opens a free, stronger follow-up attack.
        // (on a copy of the mage, who evades often: an elf with 회피)
        var dancerData = UnityEngine.Object.Instantiate(seraData);
        dancerData.Patterns = new[] { AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/EvadeFollowup.asset") };
        var counterplay = Arena(new[] { dancerData }, new[] { new Vector2Int(8, 6), new Vector2Int(8, 8), new Vector2Int(9, 7) }, warrior, warrior, warrior);
        var scout = counterplay.Mercenaries[0];
        scout.Position = counterplay.Map.Center(new Vector2Int(6, 7));
        bool opened = false;
        for (int i = 0; i < 300 && !opened; i++)
        {
            scout.Hp = scout.Stats.Total(StatType.MaxHp) * .3f;
            scout.Stamina = scout.Stats.Total(StatType.MaxStamina);
            counterplay.Tick(.05f);
            opened = scout.FollowupRemaining > 0 || (scout.Action != null && scout.Action.StartsWith("회피 반격"));
            counterplay.CombatEvents.Clear();
        }
        Require(opened, "an evasion opens a follow-up attack");
        UnityEngine.Object.DestroyImmediate(dancerData);

        // Stamina passives, chains and support (지구력, 전투 호흡, 몸에 익은 무기, 아드레날린, 막고 찌르기, 연타 마무리, 기합).
        var karonData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryKaron.asset");
        var tank = Arena(new[] { thorData }, new Vector2Int[0]).Mercenaries[0];
        Require(tank.Passives().Exists(p => p.SkillName == "지구력"), "dwarves carry 지구력");
        var swingPattern = AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/Swing.asset");
        SoulCombat.PatternCost(tank, swingPattern, out float calmCost, out _);
        tank.Hp = tank.Stats.Total(StatType.MaxHp) * .3f;
        SoulCombat.PatternCost(tank, swingPattern, out float desperateCost, out _);
        Require(desperateCost < calmCost * .8f, $"아드레날린: cheaper under 35% HP ({calmCost:0.0} → {desperateCost:0.0})");
        var swordsman = Arena(new[] { ria }, new Vector2Int[0]).Mercenaries[0];
        SoulCombat.PatternCost(swordsman, slash, out float firstCost, out _);
        swordsman.LastAttack = slash; swordsman.AttackStreak = 2;
        SoulCombat.PatternCost(swordsman, slash, out float thirdCost, out _);
        Require(thirdCost < firstCost * .7f, $"몸에 익은 무기: the same attack again costs less ({firstCost:0.0} → {thirdCost:0.0})");
        var hunter = Arena(new[] { karonData }, new Vector2Int[0]).Mercenaries[0];
        hunter.Stamina = 0;
        SoulCombat.FirePassives(hunter, SoulTrigger.OnKill, null);
        Require(hunter.Stamina >= 9.99f, "전투 호흡: a kill gives stamina back");

        var blocker = Arena(new[] { ria }, new[] { new Vector2Int(8, 7) }, warrior);
        var shield = blocker.Mercenaries[0];
        bool riposte = false;
        for (int i = 0; i < 60 && !riposte; i++)
        {
            shield.Stamina = shield.Stats.Total(StatType.MaxStamina);
            var blow = SoulCombat.Resolve(blocker.Monsters[0], shield, new SoulHit { School = SoulDamageSchool.Physical, Raw = 5, Melee = true }, new System.Random(i));
            riposte = blow.Guarded && shield.FollowupRemaining > 0 && shield.FollowupArmorIgnore >= .5f;
        }
        Require(riposte, "막고 찌르기: a guarded blow opens an armor-piercing follow-up");

        var brawl = Arena(new[] { karonData }, new[] { new Vector2Int(8, 7) }, warrior);
        var brawler = brawl.Mercenaries[0];
        brawler.Position = brawl.Map.Center(new Vector2Int(7, 7));
        bool finisher = false, shouted = false;
        for (int i = 0; i < 400 && !finisher; i++)
        {
            brawl.Tick(.05f);
            finisher = brawler.FollowupRemaining > 0 && brawler.FollowupMultiplier >= 1.5f;
            brawler.Hp = brawler.Stats.Total(StatType.MaxHp);
            brawl.Monsters[0].Hp = brawl.Monsters[0].Stats.Total(StatType.MaxHp);
        }
        Require(finisher, "연타 마무리: three hits on one target open a stronger free attack");
        var shout = Arena(new[] { ria }, new[] { new Vector2Int(8, 7) }, warrior);
        var shouter = shout.Mercenaries[0];
        shouter.Position = shout.Map.Center(new Vector2Int(7, 7));
        for (int i = 0; i < 200 && !shouted; i++)
        {
            shout.Tick(.05f);
            shouted = shouter.TimedBuffs.Exists(b => b.Id == "기합");
            shouter.Hp = shouter.Stats.Total(StatType.MaxHp);
        }
        Require(shouted, "기합: every pattern free for a moment");

        // 명상: a passive now — MP comes back faster, always; no 명상 pattern on anyone
        var still = Arena(new[] { seraData }, new Vector2Int[0]);
        var monk = still.Mercenaries[0];
        Require(monk.Stats.Total(StatType.ManaRegenRate) >= .5f && !monk.Patterns().Exists(p => p != null && p.name == "Meditate"),
            $"명상 is a passive: MP recovery ×{1 + monk.Stats.Total(StatType.ManaRegenRate):0.0}");
        monk.Mp = monk.Stats.Total(StatType.MaxMp) * .2f;
        float mpBefore = monk.Mp;
        for (int i = 0; i < 20; i++) still.Tick(.05f);
        Require(monk.Mp > mpBefore, $"MP comes back out of combat ({mpBefore:0} → {monk.Mp:0})");

        // A monster shot from beyond its perception turns on the shooter.
        var sniped = Arena(new[] { seraData }, new[] { new Vector2Int(14, 7) }, elite);
        var sniper = sniped.Mercenaries[0];
        Classic(sniper); // (a freezing spell would keep the brute where it is)
        sniper.Position = sniped.Map.Center(new Vector2Int(10, 7));
        var brute = sniped.Monsters[0];
        float start = Vector2.Distance(brute.Position, sniper.Position);
        bool struck = false;
        for (int i = 0; i < 200; i++)
        {
            sniped.Tick(.05f);
            struck |= brute.Hp < brute.Stats.Total(StatType.MaxHp) || brute.Statuses.Count > 0;
            sniper.Hp = sniper.Stats.Total(StatType.MaxHp);
            if (struck && Vector2.Distance(brute.Position, sniper.Position) < start - 1f) break;
        }
        Debug.Log($"PROVOKE elite start {start:0.0} → {Vector2.Distance(brute.Position, sniper.Position):0.0}, hit {struck}, action {brute.Action}");
        Require(struck && Vector2.Distance(brute.Position, sniper.Position) < start - 1f, "a monster hit from range goes after the attacker");
    }

    // Wounds, potions, the healing spell, and the village loop: build, hire, buy/equip/sell, heal, bless, train,
    // go down and come back with the loot and the quest done.
    // Hire concepts: most hires of a job are plain, some lean one way (a trade-off and a pattern), a few are gifted;
    // a small spread makes even two plain ones differ.
    // The guild's roster: fixed people — each name once, every job with a plain one and one with a leaning, each
    // concept of its own job, the same person every time; 비성향 stats never grow.
    static void ValidateHireStyles(SoulVillageData village, SoulStatRules rules)
    {
        var recruits = village.Recruits;
        Require(recruits.Length >= 20, $"the roster has its people ({recruits.Length})");
        var names = new HashSet<string>(); var ids = new HashSet<string>();
        foreach (var entry in recruits)
        {
            Require(entry != null && names.Add(entry.DisplayName) && ids.Add(entry.Id) && entry.Template != null && (entry.Founder || entry.Up != entry.Down),
                $"{entry?.DisplayName}: one name, one id, a job and its own difference");
            if (entry.Rarity == SoulStyleRarity.Normal) continue;
            var trait = entry.Signature;
            Require(trait != null && !string.IsNullOrEmpty(trait.Description) && trait.Icon != null && !string.IsNullOrEmpty(entry.Title) && !string.IsNullOrEmpty(entry.Look.Armor),
                $"{entry.DisplayName} ({SoulHireStyles.RarityName(entry.Rarity)}): a title, a trait of its own with an icon, an outfit");
            Require(entry.Rarity != SoulStyleRarity.Rare || !string.IsNullOrEmpty(entry.Look.Cape), $"{entry.DisplayName}: a 전설 wears a cape");
        }
        Require(System.Array.FindAll(recruits, e => e.Founder).Length == SoulCampaign.StartingMercenaries, "the founders are the starting company");
        var jobs = new List<SoulMercenaryData>(village.HireTemplates); jobs.AddRange(village.PriestTemplates);
        SoulMercenary Make(SoulRecruitData entry, string id) => SoulHireOffer.Of(entry, 0, 2).Make(rules, id);
        float Main(SoulMercenary hero) { float sum = 0; foreach (var stat in new[] { StatType.Strength, StatType.Vitality, StatType.Agility, StatType.Magic, StatType.Will, StatType.Luck }) sum += hero.Stats.Total(stat); return sum; }
        foreach (var job in jobs)
        {
            var mine = System.Array.FindAll(recruits, e => e.Template == job && !e.Founder);
            var plainOne = System.Array.Find(mine, e => e.Rarity == SoulStyleRarity.Normal);
            var eliteOne = System.Array.Find(mine, e => e.Rarity == SoulStyleRarity.Special);
            var legendOne = System.Array.Find(mine, e => e.Rarity == SoulStyleRarity.Rare);
            Require(plainOne != null && eliteOne != null && legendOne != null, $"{job.Job}: a 일반, a 정예 and a 전설");
            float p = Main(Make(plainOne, "grade_p")), e = Main(Make(eliteOne, "grade_e")), l = Main(Make(legendOne, "grade_l"));
            Require(l >= e + 4 && e >= p + 2, $"{job.Job}: the grades stand apart ({p:0} · {e:0} · {l:0})");
            Require(job.WeakGrowth.Length > 0, $"{job.Job} has its 비성향");
        }
        var first = System.Array.Find(recruits, e => !e.Founder);
        var once = Make(first, "fixed_a"); var twice = Make(first, "fixed_b");
        bool same = true;
        foreach (var stat in new[] { StatType.Strength, StatType.Vitality, StatType.Agility, StatType.Magic, StatType.Will, StatType.Luck })
            same &= Mathf.Abs(once.Stats.Total(stat) - twice.Stats.Total(stat)) < .01f;
        Require(same && once.Style == twice.Style && once.Recruit == first, $"{first.DisplayName} is the same person every time");
        var swordsman = System.Array.Find(village.HireTemplates, t => t.Job == "검사");
        SoulMercenary Plain() => new SoulHireOffer { Template = swordsman, Name = "시험" }.Make(rules, "plain_swordsman");
        var plain = Plain();
        var dancer = Make(System.Array.Find(recruits, e => e.Id == "serin"), "style_serin");
        Require(dancer.Stats.Total(StatType.Agility) >= plain.Stats.Total(StatType.Agility) + 3.99f && dancer.StartingPatterns.Exists(p => p.name == "RollSlash")
            && !plain.StartingPatterns.Exists(p => p.name == "RollSlash") && dancer.Stats.Total(StatType.DoubleHit) >= .149f,
            "세린 (정예 검무사): nimbler than a plain swordsman, rolls and slashes, and her 칼춤 strikes twice");
        var leon = Make(System.Array.Find(recruits, e => e.Id == "leon"), "style_leon");
        Require(leon.Rarity == SoulStyleRarity.Rare && leon.Stats.Total(StatType.ArmorShred) >= .199f, "레온 (전설): his trait is on him");
        // 비성향: a swordsman never grows in magic, however many levels
        var growth = plain.RollGrowth(3000, new System.Random(5));
        Require(!growth.ContainsKey(StatType.Magic) && !growth.ContainsKey(StatType.ImpactPower) && growth.ContainsKey(StatType.Strength), "비성향 stats get no level-up points");
        // the trip report sees every point a level brings (durability and the powers too, not only the main six)
        var before = SoulTripStart.Of(plain);
        int levels = plain.AddExperience(2000, rules, new System.Random(3));
        float gained = 0;
        foreach (var stat in SoulTripStart.Grown) gained += SoulTripStart.Grew(plain, stat) - before.Stats[stat];
        Require(levels > 0 && Mathf.Abs(gained - levels * SoulStatRules.PointsPerLevel(rules)) < .01f, $"the report counts every level-up point ({gained:0} of {levels * SoulStatRules.PointsPerLevel(rules)})");
        // 다운 저항 (from will): a steady mercenary stays up through most of the falls a wounded, breathless one takes
        int Falls(float resist)
        {
            var target = Plain();
            var attacker = Plain();
            target.AddWounds(2);
            if (resist > 0) target.AddTimedBuff("test_down", new[] { new SoulStatBonus { Stat = StatType.DownResist, Value = resist } }, 999);
            var roll = new System.Random(9);
            int falls = 0;
            for (int i = 0; i < 200; i++)
            {
                target.Stamina = 0; target.DownTime = 0; target.Hp = target.Stats.Total(StatType.MaxHp);
                SoulCombat.Resolve(attacker, target, new SoulHit { School = SoulDamageSchool.Physical, Raw = 5, Melee = true }, roll);
                if (target.DownTime > 0) falls++;
            }
            return falls;
        }
        int shaky = Falls(0), steady = Falls(.75f);
        Require(shaky > 50 && steady < shaky * .5f, $"다운 저항 keeps a mercenary on its feet ({shaky} → {steady} falls of 200)");
    }

    // The job skills, anyone's, the 전설's own and the action patterns: books for each, areas, chains, traps, shields,
    // guards, cleansing, 기적 (everyone back, once a floor) and 지름길.
    static void ValidateJobSkills(SoulVillageData village, SoulDungeonData dungeon, SoulStatRules rules)
    {
        SoulActiveSkillData S(string file) => AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/" + file + ".asset");
        foreach (string file in new[] { "SkillIssen", "SkillExecution", "SkillGuardianCry", "SkillIronWall", "SkillRoar", "SkillFrenzy", "SkillSmokeBomb", "SkillTrap",
            "SkillPiercingShot", "SkillFrostPrison", "SkillBlizzard", "SkillConfusion", "SkillPetrifyGaze", "SkillArcaneBurst", "SkillChainLightning", "SkillManaShield",
            "SkillPrayer", "SkillJudgment", "SkillPurify", "SkillSacrifice", "SkillSanctuary", "SkillWarCry", "SkillBattleFocus" })
        {
            var skill = S(file);
            Require(skill != null && skill.Icon != null && !string.IsNullOrEmpty(skill.Description)
                && System.Array.IndexOf(village.Dungeon.SkillBooks, skill) >= 0 && skill.UpgradeTo != null, $"{file}: a skill book with an icon and a stronger form");
        }
        foreach (var every in All<SoulActiveSkillData>()) Require(every.HasEffect(), $"{every.name} does something");
        foreach (string gone in new[] { "SkillBladeStorm", "SkillCounterStance", "SkillShieldBash", "SkillPoisonArrow", "SkillFireball" })
            Require(S(gone) == null, $"{gone} (a pattern, or said by another skill) is gone");
        foreach (string file in new[] { "Sprint", "Kick", "ThrowingKnife", "Rake", "PoisonArrow" })
        {
            var pattern = AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/" + file + ".asset");
            Require(pattern != null && pattern.Icon != null && System.Array.IndexOf(village.Dungeon.LevelPatternPool, pattern) >= 0
                && System.Array.IndexOf(village.TrainingPatterns, pattern) >= 0, $"{file}: an action pattern, learned by level and training");
        }
        // what a grade knows: 일반 at most one skill and no extra pattern, 정예 two or three and a pattern or two,
        // 전설 three or four and up to three patterns (a chain among them)
        foreach (var recruit in village.Recruits)
        {
            int skills = recruit.Actives.Length, patterns = recruit.Patterns.Length;
            var hero = SoulHireOffer.Of(recruit, 0, 2).Make(rules, "kit_" + recruit.Id);
            var (fewest, most) = KitRange(recruit.Job, recruit.Rarity);
            bool ok = skills <= most && (recruit.Rarity == SoulStyleRarity.Normal ? skills >= fewest - (recruit.Job == "마법사" || recruit.Job == "성직자" || recruit.Job == "소환사" ? 0 : 1) && patterns == 0
                : skills >= fewest && patterns <= (recruit.Rarity == SoulStyleRarity.Special ? 2 : 3));
            Require(ok && hero.StartingActives.Count == skills, $"{recruit.DisplayName} ({SoulHireStyles.RarityName(recruit.Rarity)}): {skills} skills, {patterns} patterns, {hero.StartingActives.Count} on the body");
        }
        SoulRecruitData Who(string id) => System.Array.Find(village.Recruits, r => r.Id == id);
        SoulMercenary Make(string id, string tag) => SoulHireOffer.Of(Who(id), 0, 2).Make(rules, tag + "_" + id);
        foreach (var (id, file) in new[] { ("leon", "SkillHeavenBlade"), ("baron", "SkillBastion"), ("isolde", "SkillMeteor"), ("elena", "SkillMiracle"), ("ragna", "SkillPackCall"), ("ethan", "SkillShortcut") })
            Require(S(file) != null && S(file).Icon != null && Make(id, "own").AllActiveSkills().Contains(S(file)), $"{id} knows {file}");
        // a technique pays MP without being a spell; 처형 only on the nearly dead
        Require(SoulSkillUsePolicy.Magical(S("SkillChainLightning")) && !SoulSkillUsePolicy.Magical(S("SkillIssen")) && !SoulSkillUsePolicy.Magical(S("SkillHeavenBlade")),
            "기술 (일섬, 천검) pays MP but is no spell");

        var swordsmanData = System.Array.Find(village.HireTemplates, t => t.Job == "검사");
        var brawlerData = System.Array.Find(village.HireTemplates, t => t.Job == "투사");
        var mageData = System.Array.Find(village.HireTemplates, t => t.Job == "마법사");
        var guideData = System.Array.Find(village.HireTemplates, t => t.Job == "길잡이");
        var priestData = village.PriestTemplates[0];
        SoulDungeonSession Arena(IEnumerable<SoulMercenary> party, int seed, bool keepOne)
        {
            var arena = new SoulDungeonSession(dungeon, rules, party, seed, 1) { AutoExplore = false };
            foreach (var m in arena.Monsters) if (!keepOne || m != arena.Monsters[0]) m.Position = new Vector2(-500, -500);
            return arena;
        }
        void Only(SoulMercenary hero, SoulActiveSkillData skill)
        {
            hero.StartingActives.Clear(); hero.LearnedActives.Clear();
            Require(hero.LearnSkill(skill), $"{hero.Name} learns {skill.SkillName}");
            hero.Mp = hero.Stats.Total(StatType.MaxMp);
        }
        // 처형: not on a healthy foe, yes on one nearly gone
        {
            var hero = SoulDungeonSession.RecruitOne(swordsmanData, rules, "exec", "처형자");
            var arena = Arena(new[] { hero }, 70, true);
            Only(hero, S("SkillExecution"));
            var foe = arena.Monsters[0];
            hero.Position = foe.Position + Vector2.right * 1f;
            Require(!SoulSkillUsePolicy.CanUse(hero, S("SkillExecution"), foe), "처형 waits for a foe near its end");
            foe.Hp = foe.Stats.Total(StatType.MaxHp) * .2f;
            Require(SoulSkillUsePolicy.CanUse(hero, S("SkillExecution"), foe), "처형 finishes one at 20% HP");
        }
        // 천검 (a burst): one swing, everyone around is cut
        {
            var leon = Make("leon", "burst");
            var arena = new SoulDungeonSession(dungeon, rules, new[] { leon }, 71, 1) { AutoExplore = false };
            Only(leon, S("SkillHeavenBlade"));
            var ring = arena.Monsters.GetRange(0, Mathf.Min(3, arena.Monsters.Count));
            foreach (var m in arena.Monsters) if (!ring.Contains(m)) m.Position = new Vector2(-500, -500);
            leon.Position = ring[0].Position + Vector2.right * .9f;
            for (int i = 1; i < ring.Count; i++) ring[i].Position = ring[0].Position + new Vector2(i == 1 ? 0 : .5f, i == 1 ? .6f : -.6f);
            var full = ring.ConvertAll(m => m.Hp);
            bool used = false;
            for (int i = 0; i < 120 && !used; i++)
            {
                arena.Tick(.05f);
                leon.Hp = leon.Stats.Total(StatType.MaxHp);
                used = arena.CombatEvents.Exists(e => e.Kind == SoulEventKind.Shockwave && e.Actor == leon);
            }
            int cut = 0;
            for (int i = 0; i < ring.Count; i++) if (ring[i].Hp < full[i] || !ring[i].Alive) cut++;
            Require(used && cut >= 2 && leon.Mp < leon.Stats.Total(StatType.MaxMp), $"천검 hits everyone around, paid in MP ({cut} of {ring.Count})");
        }
        // 연쇄 번개: the bolt jumps on
        {
            var mage = SoulDungeonSession.RecruitOne(mageData, rules, "chain", "번개술사");
            var arena = new SoulDungeonSession(dungeon, rules, new[] { mage }, 74, 1) { AutoExplore = false };
            Only(mage, S("SkillChainLightning"));
            var group = arena.Monsters.GetRange(0, Mathf.Min(3, arena.Monsters.Count));
            foreach (var m in arena.Monsters) if (!group.Contains(m)) m.Position = new Vector2(-500, -500);
            for (int i = 1; i < group.Count; i++) group[i].Position = group[0].Position + new Vector2(0, i == 1 ? .8f : -.8f);
            mage.Position = group[0].Position + Vector2.right * 3f;
            var full = group.ConvertAll(m => m.Hp);
            int struck = 0;
            for (int i = 0; i < 160 && struck < 2; i++)
            {
                arena.Tick(.05f);
                mage.Hp = mage.Stats.Total(StatType.MaxHp);
                struck = 0;
                for (int k = 0; k < group.Count; k++) if (group[k].Hp < full[k] || !group[k].Alive) struck++;
            }
            Require(struck >= 2, $"연쇄 번개 jumps from foe to foe ({struck})");
        }
        // 덫 설치: laid on sight, sprung by the first foe that comes
        {
            var guide = SoulDungeonSession.RecruitOne(guideData, rules, "trapper", "사냥꾼");
            var arena = Arena(new[] { guide }, 75, true);
            Only(guide, S("SkillTrap"));
            var foe = arena.Monsters[0];
            guide.Position = foe.Position + Vector2.right * 3f;
            float full = foe.Hp;
            bool laid = false, sprung = false;
            for (int i = 0; i < 300 && !sprung; i++)
            {
                arena.Tick(.05f);
                guide.Hp = guide.Stats.Total(StatType.MaxHp);
                laid |= arena.FieldCount > 0;
                sprung = laid && arena.FieldCount == 0;
            }
            Require(laid && sprung, $"덫 설치: laid ({laid}) and sprung ({sprung})");
        }
        // 마나 보호막 and 대신 맞기: MP and a guardian take part of a blow
        {
            var roll = new System.Random(3);
            var brute = SoulDungeonSession.RecruitOne(brawlerData, rules, "brute", "때리는 자");
            var mage = SoulDungeonSession.RecruitOne(mageData, rules, "shield", "막는 자");
            float Blow(SoulMercenary target)
            {
                target.Hp = target.Stats.Total(StatType.MaxHp);
                var hit = SoulCombat.Resolve(brute, target, new SoulHit { School = SoulDamageSchool.Magic, Kind = SoulDamageKind.Arcane, Raw = 30 }, new System.Random(3));
                return target.Stats.Total(StatType.MaxHp) - target.Hp;
            }
            float bare = Blow(mage);
            mage.AddTimedBuff("test_shield", new[] { new SoulStatBonus { Stat = StatType.ManaShield, Value = .5f } }, 99);
            mage.Mp = mage.Stats.Total(StatType.MaxMp);
            float shielded = Blow(mage);
            Require(shielded < bare * .7f && mage.Mp < mage.Stats.Total(StatType.MaxMp), $"마나 보호막: MP pays for part of it ({bare:0.#} → {shielded:0.#})");
            var baron = Make("baron", "guard");
            var ward = SoulDungeonSession.RecruitOne(swordsmanData, rules, "ward", "지켜지는 자");
            baron.Position = ward.Position + Vector2.right;
            float alone = Blow(ward);
            ward.AddTimedBuff("bastion", new[] { new SoulStatBonus { Stat = StatType.Armor, Value = 0 } }, 99);
            ward.Guardian = baron; ward.GuardShare = .4f; ward.GuardBuff = "bastion";
            baron.Hp = baron.Stats.Total(StatType.MaxHp);
            float guarded = Blow(ward);
            Require(guarded < alone * .7f && baron.Hp < baron.Stats.Total(StatType.MaxHp), $"불괴의 성채: the guardian takes its share ({alone:0.#} → {guarded:0.#})");
        }
        // 정화: the poison is gone
        {
            var priest = SoulDungeonSession.RecruitOne(priestData, rules, "purifier", "정화자");
            var ally = SoulDungeonSession.RecruitOne(swordsmanData, rules, "poisoned", "중독자");
            var arena = Arena(new[] { priest, ally }, 76, false);
            Only(priest, S("SkillPurify"));
            ally.Position = priest.Position + Vector2.right * 1.5f;
            for (int i = 0; i < 20 && ally.Statuses.Count == 0; i++) SoulCombat.ApplyStatus(ally, SoulStatus.Poison, 1, 30, 1, new System.Random(i));
            bool clean = false;
            for (int i = 0; i < 80 && !clean; i++) { arena.Tick(.05f); clean = ally.Statuses.Count == 0; }
            Require(clean, "정화 lifts the poison");
        }
        // 지름길: retreating from far away, the party is at the way out at once — once a floor
        {
            var ethan = Make("ethan", "short");
            var mate = SoulDungeonSession.RecruitOne(swordsmanData, rules, "mate", "동료");
            var arena = new SoulDungeonSession(dungeon, rules, new[] { ethan, mate }, 77, 1) { AutoExplore = false };
            Vector2 home = arena.Map.Center(arena.PartyStart);
            SoulMonster far = null;
            foreach (var m in arena.Monsters) if (far == null || Vector2.Distance(m.Position, home) > Vector2.Distance(far.Position, home)) far = m;
            Vector2 away = far.Position;
            foreach (var m in arena.Monsters) m.Position = new Vector2(-500, -500);
            ethan.Position = away; mate.Position = away + Vector2.right * .3f;
            float before = Vector2.Distance(ethan.Position, home);
            arena.OrderEscape();
            for (int i = 0; i < 40; i++) arena.Tick(.05f);
            float after = Vector2.Distance(ethan.Position, home);
            Require(before > SoulDungeonSession.ShortcutFrom && after < 3, $"지름길 takes the party to the way out ({before:0.#} → {after:0.#})");
        }
        // 포효: no damage, fear on those around
        {
            var arena = new SoulDungeonSession(dungeon, rules, new[] { brawlerData }, 72) { AutoExplore = false };
            var hero = arena.Mercenaries[0];
            Only(hero, S("SkillRoar"));
            foreach (var m in arena.Monsters) if (m != arena.Monsters[0]) m.Position = new Vector2(-500, -500);
            hero.Position = arena.Monsters[0].Position + Vector2.right * 1.2f;
            bool feared = false;
            int roars = 0;
            for (int i = 0; i < 400 && !feared; i++)
            {
                arena.Tick(.05f);
                hero.Hp = hero.Stats.Total(StatType.MaxHp);
                hero.Stamina = hero.Stats.Total(StatType.MaxStamina);
                if (hero.SkillCooldowns.TryGetValue("roar", out float wait) && wait > 0) { roars++; hero.SkillCooldowns.Clear(); } // roar again (the fear is a roll)
                feared = arena.Monsters[0].Grade(SoulStatus.Fear) > 0;
            }
            Require(feared, $"포효 frightens the enemy ({roars} roars)");
        }
        // 기적: both fallen come back at once — and only once on a floor
        {
            var elena = Make("elena", "miracle");
            var a = SoulDungeonSession.RecruitOne(swordsmanData, rules, "miracle_a", "갑");
            var b = SoulDungeonSession.RecruitOne(brawlerData, rules, "miracle_b", "을");
            var chapel = Arena(new[] { elena, a, b }, 73, false);
            a.Position = elena.Position + Vector2.right * 1.2f; b.Position = elena.Position + Vector2.left * 1.2f;
            elena.Mp = elena.Stats.Total(StatType.MaxMp);
            a.Hp = 0; b.Hp = 0;
            for (int i = 0; i < 80 && !(a.Alive && b.Alive); i++) chapel.Tick(.05f);
            Require(a.Alive && b.Alive, "기적 brings every fallen ally back at once");
            a.Hp = 0; b.Hp = 0;
            elena.SkillCooldowns.Clear(); elena.Mp = elena.Stats.Total(StatType.MaxMp);
            for (int i = 0; i < 80; i++) chapel.Tick(.05f);
            Require(!a.Alive && !b.Alive, "기적 works once a floor");
        }
    }

    // Monster souls' skills: every soul brings its own, a refined one the stronger form; the bosses' outdo the rest;
    // a monster always warns (예고), a mercenary only for what is not instant; only 회피 본능 monsters evade.
    static void ValidateSoulSkills(SoulVillageData village, SoulDungeonData dungeon, SoulStatRules rules)
    {
        SoulActiveSkillData S(string file) => AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/" + file + ".asset");
        int count = 0;
        foreach (var (soulFile, files) in SoulSkillMap)
        {
            var soul = AssetDatabase.LoadAssetAtPath<SoulData>(DataPath + "/" + soulFile + ".asset");
            Require(soul != null, $"{soulFile} exists");
            foreach (string file in files)
            {
                var skill = S("SoulSkill" + file);
                Require(skill != null && skill.Icon != null && !string.IsNullOrEmpty(skill.Description) && System.Array.IndexOf(soul.ActiveSkills, skill) >= 0,
                    $"{soul.OriginMonster}'s soul brings {file}");
                var refined = soul.Refined != null ? System.Array.Find(soul.Refined.ActiveSkills, s => s != null && s.name == "SoulSkill" + file + "Refined") : null;
                Require(refined != null && refined.SkillName.EndsWith("(정제)"), $"{soul.OriginMonster} refined brings {file} (정제)");
                count++;
            }
        }
        Require(count >= 55, $"monster souls bring their skills ({count})");
        float Flat(string file) => S("SoulSkill" + file).Damage.Flat;
        float Reach(string file) => S("SoulSkill" + file).Radius.Flat;
        Require(Flat("DragonBreath") > Flat("FireBreath") * 2 && Reach("GodsWrath") > Reach("HellfireSmash") && Reach("Whirlpool") > Reach("Splash")
            && Reach("BogSummon") > Reach("AcidPool") && S("SoulSkillDeathSentence").Status.Grade > S("SoulSkillCurseDoll").Extra.Grade
            && Flat("AbyssGate") > Flat("HellfireSmash"), "a boss soul's skill outdoes its kin");
        foreach (string boss in new[] { "MonsterToadLord", "MonsterLakeDrake", "MonsterFireDrake", "MonsterBoneKing", "MonsterFallenDemigod", "MonsterAbyssLord" })
        {
            var monster = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/" + boss + ".asset");
            Require(monster != null && System.Array.Exists(monster.ActiveSkills, s => s != null && s.name.StartsWith("SoulSkill")), $"{boss} fights with its soul's skills");
        }

        // who warns: a monster always, a mercenary unless instant
        var arena = new SoulDungeonSession(dungeon, rules, new[] { System.Array.Find(village.HireTemplates, t => t.Job == "투사") }, 81, 1) { AutoExplore = false };
        var hero = arena.Mercenaries[0];
        var monsterUnit = arena.Monsters[0];
        Require(SoulDungeonSession.Warned(monsterUnit, S("SoulSkillPickpocket")) && !SoulDungeonSession.Warned(hero, S("SoulSkillPickpocket"))
            && SoulDungeonSession.Warned(hero, S("SoulSkillTailSweep")) && !SoulDungeonSession.Warned(hero, S("SkillWarCry")), "a monster always warns; a mercenary's instant skill does not");
        // a mercenary's warned skill: the circle first, the blow when it lands
        hero.StartingActives.Clear(); hero.LearnedActives.Clear();
        Require(hero.LearnSkill(S("SoulSkillTailSweep")), "learn 꼬리 휩쓸기");
        foreach (var m in arena.Monsters) if (m != monsterUnit) m.Position = new Vector2(-500, -500);
        hero.Position = monsterUnit.Position + Vector2.right * 1f;
        float full = monsterUnit.Hp;
        bool warned = false, landed = false;
        for (int i = 0; i < 160 && !landed; i++)
        {
            arena.Tick(.05f);
            hero.Hp = hero.Stats.Total(StatType.MaxHp);
            warned |= arena.Telegraphs.Exists(t => t.Skill == S("SoulSkillTailSweep") && !t.Struck);
            landed = warned && arena.Telegraphs.Exists(t => t.Skill == S("SoulSkillTailSweep") && t.Struck);
        }
        Require(warned && landed, $"꼬리 휩쓸기 warns, then lands ({warned}, {landed})");

        // 회피 본능
        var wolf = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterWolf.asset");
        var boar = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterBoar.asset");
        Require(wolf.Evasive && !boar.Evasive, "the wolf evades, the boar does not");
        foreach (var path in AssetDatabase.FindAssets("t:SoulMonsterData", new[] { DataPath }))
        {
            var monster = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(AssetDatabase.GUIDToAssetPath(path));
            bool dodges = System.Array.Exists(monster.Patterns, p => p != null && p.Category == SoulPatternCategory.Defense && (p.DefenseMode == SoulDefenseMode.Dodge || p.DefenseMode == SoulDefenseMode.Roll));
            Require(!dodges || monster.Evasive, $"{monster.name}: a dodging pattern goes with 회피 본능");
        }
        var plain = arena.Monsters.Find(m => !m.Data.Evasive);
        if (plain != null)
        {
            plain.AddTimedBuff("test_evasion", new[] { new SoulStatBonus { Stat = StatType.Evasion, Value = 5 } }, 99);
            int missed = 0;
            for (int i = 0; i < 40; i++)
            {
                plain.Hp = plain.Stats.Total(StatType.MaxHp);
                if (SoulCombat.Resolve(hero, plain, new SoulHit { School = SoulDamageSchool.Physical, Kind = SoulDamageKind.Impact, Raw = 5, Melee = true }, new System.Random(i)).Dodged) missed++;
            }
            Require(missed < 10 && !SoulDungeonSession.CanEvade(plain), $"a monster without 회피 본능 cannot slip a blow ({missed} of 40 missed)");
        }
    }

    // The roster of a hundred: races of their own, prices, and the traits that are rules (결투, 검투장, 성벽의 맹세,
    // 숲의 숨결, 독서가, 퇴마 …) doing what they say.
    static void ValidateRosterTraits(SoulVillageData village, SoulDungeonData dungeon, SoulStatRules rules)
    {
        Require(village.Recruits.Length >= 95, $"the roster has its hundred ({village.Recruits.Length})");
        SoulRecruitData Who(string id) => System.Array.Find(village.Recruits, r => r.Id == id);
        SoulMercenary Make(string id) => SoulHireOffer.Of(Who(id), 0, 2).Make(rules, "trait_" + id);
        Require(Make("fien").Race.Id == "엘프" && Make("duran").Race.Id == "드워프" && Make("yak").Race.Id == "드워프" && Make("hans").Race.Id == "인간",
            "a recruit can be of a race of its own");
        var campaign = new SoulCampaign(village, 91);
        campaign.Levels[SoulBuildingKind.Church] = 1;
        Require(campaign.OfferFor(Who("tom")).Price < campaign.OfferFor(Who("mariel")).Price, "탁발승 톰 asks less");
        Require(Make("nate").Stats.Total(StatType.BookDiscount) > Make("organ").Stats.Total(StatType.BookDiscount), "독서가 may keep the book it reads");
        var skeleton = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterSkeleton.asset");
        var boar = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterBoar.asset");
        Require(skeleton.Undead && !boar.Undead, "the skeleton is undead, the boar is not");

        SoulDungeonSession Arena(IEnumerable<SoulMercenary> party, int keep, int seed)
        {
            var arena = new SoulDungeonSession(dungeon, rules, party, seed, 1) { AutoExplore = false };
            for (int i = 0; i < arena.Monsters.Count; i++) if (i >= keep) arena.Monsters[i].Position = new Vector2(-500, -500);
            return arena;
        }
        float Fury(SoulMercenary hero) { float sum = 0; foreach (var b in hero.TimedBuffs) if (b.Id == "trait_fury") foreach (var x in b.Bonuses) sum += x.Value; return sum; }
        // 결투: one foe near — stronger
        var seira = Make("seira");
        var duel = Arena(new[] { seira }, 1, 92);
        seira.Position = duel.Monsters[0].Position + Vector2.right * 2f;
        for (int i = 0; i < 8; i++) { duel.Tick(.05f); seira.Hp = seira.Stats.Total(StatType.MaxHp); }
        Require(Fury(seira) > 0, "결투: one foe near, the attack rises");
        // 검투장: more foes near — stronger still
        var zena = Make("zena");
        var crowd = Arena(new[] { zena }, 3, 93);
        zena.Position = crowd.Monsters[0].Position + Vector2.right * 1f;
        for (int i = 1; i < 3; i++) crowd.Monsters[i].Position = crowd.Monsters[0].Position + new Vector2(0, i == 1 ? .7f : -.7f);
        for (int i = 0; i < 8; i++) { crowd.Tick(.05f); zena.Hp = zena.Stats.Total(StatType.MaxHp); }
        float many = Fury(zena);
        Require(many > 0, $"검투장: foes around, the attack rises ({many:0.#})");
        // 성벽의 맹세 and 숲의 숨결: the others are covered and mended
        var aegis = Make("aegis"); var sui = Make("sui"); var ward = Make("hans");
        var camp = Arena(new[] { aegis, sui, ward }, 0, 94);
        aegis.Position = ward.Position + Vector2.right; sui.Position = ward.Position + Vector2.left;
        for (int i = 0; i < 8; i++) camp.Tick(.05f);
        Require(ward.Guardian == aegis && ward.GuardShare > 0, "성벽의 맹세: a companion close by is covered");
        Require(ward.TimedBuffs.Exists(b => b.Id.StartsWith("trait_regen")), "숲의 숨결: a companion close by mends faster");

        // at the way out, a monster in sight that is not after the party (asleep, standing off) keeps nobody from it
        {
            var ria = Make("hans"); var thor = Make("bram");
            var door = new SoulDungeonSession(dungeon, rules, new[] { ria, thor }, 98, 1) { AutoExplore = false };
            SoulInteractable portal = null;
            foreach (var thing in door.Interactables) if (thing.Kind == SoulObjectKind.Escape) portal = thing;
            Require(portal != null, "the entrance has its escape portal");
            var idle = door.Monsters[0];
            foreach (var m in door.Monsters) if (m != idle) m.Position = new Vector2(-500, -500);
            thor.Position = portal.Position;
            Vector2 FreeNear(Vector2 at, float from, float to)
            {
                for (float r = from; r <= to; r += .25f)
                    for (int k = 0; k < 16; k++)
                    {
                        var spot = at + new Vector2(Mathf.Cos(k * Mathf.PI / 8), Mathf.Sin(k * Mathf.PI / 8)) * r;
                        if (door.Map.Clear(spot, .4f) && door.Map.Sight(at, spot)) return spot;
                    }
                return at;
            }
            ria.Position = FreeNear(portal.Position, 3f, 3.8f);
            idle.Position = FreeNear(portal.Position, 5.5f, 6.5f);
            for (int i = 0; i < 400 && !idle.Has(SoulStatus.Stun); i++) SoulCombat.ApplyStatus(idle, SoulStatus.Stun, 1, 60, 0, new System.Random(i)); // standing off, noticing nobody
            door.OrderEscape();
            for (int i = 0; i < 200 && !door.Recalled && door.RecallLeft <= 0; i++) door.Tick(.05f);
            Require(door.Recalled || door.RecallLeft > 0, $"a monster standing off does not keep the party from the escape portal ({door.Plan}, {Vector2.Distance(ria.Position, portal.Position):0.0} from it)");
            // one at the portal, the other stuck a few cells short (here: it cannot move at all): after a while they go anyway
            var ria2 = Make("hans"); var thor2 = Make("bram");
            var stuck = new SoulDungeonSession(dungeon, rules, new[] { ria2, thor2 }, 99, 1) { AutoExplore = false };
            foreach (var m in stuck.Monsters) m.Position = new Vector2(-500, -500);
            thor2.Position = portal.Position;
            ria2.Position = portal.Position;
            for (int y = 0; y < stuck.Map.Height && ria2.Position == portal.Position; y++)
                for (int x = 0; x < stuck.Map.Width; x++)
                {
                    var spot = stuck.Map.Center(new Vector2Int(x, y));
                    float d = Vector2.Distance(spot, portal.Position);
                    if (d >= 6f && d <= 8f && stuck.Map.Clear(spot, .4f)) { ria2.Position = spot; break; }
                }
            Require(Vector2.Distance(ria2.Position, portal.Position) >= 6f, "a spot a few cells short of the portal");
            ria2.AddTimedBuff("test_rooted", new[] { new SoulStatBonus { Stat = StatType.MoveSpeed, Value = -ria2.Stats.Total(StatType.MoveSpeed) } }, 999);
            stuck.OrderEscape();
            float waited = 0;
            for (int i = 0; i < 400 && !stuck.Recalled && stuck.RecallLeft <= 0; i++) { stuck.Tick(.05f); waited += .05f; }
            Require((stuck.Recalled || stuck.RecallLeft > 0) && waited > SoulDungeonSession.StallEscape, $"a party stuck a few cells short of the portal still gets out ({waited:0.0} s, {Vector2.Distance(ria2.Position, portal.Position):0.0} from it)");
        }

        // the deeper the wounds, the sooner the potion
        var sipper = Make("hans");
        sipper.Hp = sipper.Stats.Total(StatType.MaxHp) * .36f;
        bool fresh = SoulSupplies.Wants(SoulSupplies.HealPotion, sipper, true);
        sipper.AddWounds(3);
        sipper.Hp = sipper.Stats.Total(StatType.MaxHp) * .36f;
        Require(!fresh && SoulSupplies.Wants(SoulSupplies.HealPotion, sipper, true), "at 36% HP: no potion yet unhurt, one down with three wounds");

        // small parties turn back sooner: alone at the first wound, two at two wounds
        var solo = new SoulDungeonSession(dungeon, rules, new[] { Make("hans") }, 95, 1) { Unattended = true };
        foreach (var m in solo.Monsters) m.Position = new Vector2(-500, -500);
        solo.Mercenaries[0].AddWounds(1);
        for (int i = 0; i < 20; i++) solo.Tick(.05f);
        Require(solo.Plan == SoulPartyPlan.Retreat, $"alone: one wound and it turns back ({solo.Plan})");
        var pair = new SoulDungeonSession(dungeon, rules, new[] { Make("hans"), Make("bram") }, 96, 1) { Unattended = true };
        foreach (var m in pair.Monsters) m.Position = new Vector2(-500, -500);
        pair.Pouch.Clear(); pair.Belt.Clear();
        pair.Belt[SoulSupplies.HealPotion] = 2; // a potion a head: no worry yet
        pair.Mercenaries[0].AddWounds(1);
        for (int i = 0; i < 20; i++) pair.Tick(.05f);
        Require(pair.Plan != SoulPartyPlan.Retreat && pair.PotionCaution == 0, $"two with potions: one wound, on it goes ({pair.Plan})");
        pair.Belt.Clear();                           // the potions are gone: one wound is enough now
        for (int i = 0; i < 20; i++) pair.Tick(.05f);
        Require(pair.PotionCaution == 2 && pair.RetreatWounds == 1 && pair.Plan == SoulPartyPlan.Retreat, $"two without potions: one wound and it turns back ({pair.Plan})");
        var trio = new SoulDungeonSession(dungeon, rules, new[] { Make("hans"), Make("bram"), Make("teo") }, 97, 1) { Unattended = true };
        trio.Belt.Clear(); trio.Belt[SoulSupplies.HealPotion] = 1;
        Require(trio.PotionCaution == 1 && trio.RetreatWounds == SoulDungeonSession.WoundLimit, "three with one potion: warier, the same wound limit");
        trio.Belt.Clear();
        Require(trio.PotionCaution == 2 && trio.RetreatWounds == SoulDungeonSession.WoundLimit - 1, "three without potions: a wound sooner");

        // a goblin archer pressed by a melee mercenary backs off a little, then stands and shoots: the ground it
        // gives (what its own moves open between them) and how much of the time the mercenary is not in reach
        // (unlimited backing off, as before, for comparison)
        (float given, float apart) Pressed(string chaserId, float backoff)
        {
            float keep = SoulDungeonSession.MonsterBackoff;
            SoulDungeonSession.MonsterBackoff = backoff;
            var press = new SoulDungeonSession(village.Dungeon, rules, new[] { Make(chaserId) }, 90, 1) { AutoExplore = false };
            foreach (var m in press.Monsters) m.Position = new Vector2(-500, -500);
            var archerData = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterGoblinArcher.asset");
            Vector2 open = press.Map.Center(press.PartyStart); // the roomiest spot on the floor: room to back off
            float roomiest = 0;
            for (int y = 0; y < press.Map.Height; y++)
                for (int x = 0; x < press.Map.Width; x++)
                {
                    var c = press.Map.Center(new Vector2Int(x, y));
                    float room = 0;
                    while (room < 6 && press.Map.Clear(c, room + .5f)) room += .5f;
                    if (room > roomiest) { roomiest = room; open = c; }
                }
            var archer = press.AddMonster(archerData, open);
            var chaser = press.Mercenaries[0];
            for (int k = 0; k < 16; k++)
            {
                var spot = archer.Position + new Vector2(Mathf.Cos(k * Mathf.PI / 8), Mathf.Sin(k * Mathf.PI / 8)) * 3f;
                if (press.Map.Clear(spot, .5f) && press.Map.Straight(archer.Position, spot, .5f)) { chaser.Position = spot; break; }
            }
            float given = 0, apart = 0;
            const int ticks = 300;
            for (int i = 0; i < ticks; i++)
            {
                chaser.Hp = chaser.Stats.Total(StatType.MaxHp);
                archer.Hp = archer.Stats.Total(StatType.MaxHp); // both stay in the fight: only where the archer stands matters
                var was = archer.Position;
                press.Tick(.05f);
                given += Mathf.Max(0, Vector2.Distance(archer.Position, chaser.Position) - Vector2.Distance(was, chaser.Position));
                if (Vector2.Distance(archer.Position, chaser.Position) > 1.8f) apart += 1f / ticks;
            }
            SoulDungeonSession.MonsterBackoff = keep;
            return (given, apart);
        }
        foreach (string id in new[] { "bram", "hans" })
        {
            var before = Pressed(id, 999f);
            var now = Pressed(id, SoulDungeonSession.MonsterBackoff);
            Debug.Log($"ARCHER vs {id}: unlimited gave {before.given:0.0} cells, apart {before.apart:P0} — now gave {now.given:0.0}, apart {now.apart:P0}");
            Require(now.given <= Mathf.Max(4f, before.given * .6f), $"a pressed goblin archer gives only a little ground ({id}: {now.given:0.0} cells, unlimited {before.given:0.0})");
        }

        // a long chase ends: past MonsterLeash cells from where it began, with nobody in reach, a monster turns back
        {
            var chase = new SoulDungeonSession(village.Dungeon, rules, new[] { Make("hans") }, 88, 1) { AutoExplore = false };
            foreach (var m in chase.Monsters) m.Position = new Vector2(-500, -500);
            var wolfData = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterWolf.asset");
            var runner = chase.Mercenaries[0];
            // two open spots MonsterLeash + 2 apart that a unit can walk between
            Vector2 from = Vector2.zero, to = Vector2.zero; bool found = false;
            for (int y = 2; y < chase.Map.Height - 2 && !found; y += 2)
                for (int x = 2; x < chase.Map.Width - 2 && !found; x += 2)
                {
                    var a = chase.Map.Center(new Vector2Int(x, y));
                    var b = a + Vector2.right * (SoulDungeonSession.MonsterLeash + 2);
                    if (chase.Map.Clear(a, 1f) && chase.Map.Clear(b, 1f) && chase.Map.Clear(b + Vector2.right * 3.2f, .5f) && chase.Map.Straight(b, b + Vector2.right * 3.2f, .5f)
                        && chase.Map.ClosestReachable(b, a, .5f, out var reached) && Vector2.Distance(reached, b) < .1f) { from = a; to = b; found = true; }
                }
            Require(found, "a stretch of floor for the chase test");
            var wolf = chase.AddMonster(wolfData, from);
            runner.Position = from + Vector2.right * 1.5f;
            for (int i = 0; i < 20 && wolf.CurrentTarget == null; i++) chase.Tick(.05f);
            Require(wolf.CurrentTarget == runner, "the wolf takes up the chase");
            wolf.Position = to;                                   // it followed far
            runner.Position = to + Vector2.right * 3.2f;          // the prey still in sight, out of reach
            for (int i = 0; i < 10; i++) { runner.Hp = runner.Stats.Total(StatType.MaxHp); chase.Tick(.05f); }
            float back = Vector2.Distance(wolf.Position, from);
            Require(chase.GivingUp(wolf) && wolf.CurrentTarget == null && back < SoulDungeonSession.MonsterLeash + 2, $"too far from where it began, the wolf gives up and heads back ({back:0.0} from home)");
        }

        // a priest heals from behind: it lands no blows while a front liner fights
        {
            var line = new SoulDungeonSession(village.Dungeon, rules, new[] { Make("hans"), Make("mariel") }, 87, 1) { AutoExplore = false };
            foreach (var m in line.Monsters) m.Position = new Vector2(-500, -500);
            var wolfData = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterWolf.asset");
            Vector2 open = line.Map.Center(line.PartyStart);
            var wolf = line.AddMonster(wolfData, open + Vector2.right * 2f);
            var sword = line.Mercenaries[0]; var healer = line.Mercenaries[1];
            sword.Position = open; healer.Position = open + Vector2.left * 1f;
            Require(SoulDungeonSession.Backliner(healer) && !SoulDungeonSession.Backliner(sword), "a priest is a backliner, a swordsman is not");
            float nearest = float.MaxValue;
            for (int i = 0; i < 160 && wolf.Alive; i++)
            {
                sword.Hp = sword.Stats.Total(StatType.MaxHp); healer.Hp = healer.Stats.Total(StatType.MaxHp);
                line.Tick(.05f);
                if (i > 40) nearest = Mathf.Min(nearest, Vector2.Distance(healer.Position, wolf.Position));
            }
            Require(nearest > 1.6f, $"the priest keeps behind the front ({nearest:0.0} from the wolf at the closest)");
        }

        // the player's 탈출 is not argued with: the party walks on even with a monster at its heels
        {
            var flee = new SoulDungeonSession(village.Dungeon, rules, new[] { Make("hans"), Make("bram") }, 86, 1) { AutoExplore = false };
            Vector2 home = flee.Map.Center(flee.PartyStart);
            SoulMonster far = null;
            foreach (var m in flee.Monsters) if (far == null || Vector2.Distance(m.Position, home) > Vector2.Distance(far.Position, home)) far = m;
            foreach (var m in flee.Monsters) if (m != far) m.Position = new Vector2(-500, -500);
            var a = flee.Mercenaries[0]; var b = flee.Mercenaries[1];
            a.Position = far.Position + Vector2.right * 1.2f; b.Position = a.Position + Vector2.right * .5f;
            for (int i = 0; i < 10; i++) flee.Tick(.05f);
            float start = Vector2.Distance(a.Position, home);
            flee.OrderEscape();
            bool stood = false;
            for (int i = 0; i < 80; i++) { a.Hp = a.Stats.Total(StatType.MaxHp); b.Hp = b.Stats.Total(StatType.MaxHp); flee.Tick(.05f); stood |= flee.Standing; }
            float now = Vector2.Distance(a.Position, home);
            Require(!stood && now < start - 2f, $"ordered out, the party keeps walking ({start:0.0} → {now:0.0} from the way out, stood {stood})");
        }

        // clear of pursuers on the way out, a party with a healer stops and has its wounds seen to
        {
            var rest = new SoulDungeonSession(village.Dungeon, rules, new[] { Make("hans"), Make("mariel") }, 85, 1) { Unattended = true, AutoExplore = false };
            foreach (var m in rest.Monsters) m.Position = new Vector2(-500, -500);
            Vector2 home = rest.Map.Center(rest.PartyStart);
            var hurt = rest.Mercenaries[0];
            foreach (var h in rest.Mercenaries) h.Position = home + Vector2.right * 30f;
            foreach (var h in rest.Mercenaries) if (!rest.Map.Clear(h.Position, .5f) && rest.Map.ClosestReachable(h.Position, home, .5f, out var spot)) h.Position = spot;
            rest.Mercenaries[1].Position = hurt.Position + Vector2.right * 6f; // out of the heal's reach: the healer comes over
            if (!rest.Map.Clear(rest.Mercenaries[1].Position, .5f) && rest.Map.ClosestReachable(rest.Mercenaries[1].Position, hurt.Position, .5f, out var near)) rest.Mercenaries[1].Position = near;
            hurt.AddWounds(2);
            rest.Belt[SoulSupplies.HealPotion] = 2; // a potion a head: one wound left is no reason to go home
            rest.BeginRetreat("test");
            bool rested = false;
            for (int i = 0; i < 800 && hurt.Wounds >= 2; i++) { rest.Tick(.05f); rested |= rest.Resting; }
            Require(rested && hurt.Wounds < 2, $"a safe breather on the way out mends wounds (rested {rested}, wounds {hurt.Wounds})");
            // mended, fit again: the retreat is called off and the exploring goes on
            for (int i = 0; i < 1600 && rest.Plan == SoulPartyPlan.Retreat; i++) rest.Tick(.05f);
            Require(rest.Plan != SoulPartyPlan.Retreat && !rest.Resting, $"mended on the way out, the party explores on ({rest.Plan}, wounds {hurt.Wounds}, hp {hurt.Hp / hurt.Stats.Total(StatType.MaxHp):P0}, mp {rest.Mercenaries[1].Mp:0}, resting {rest.Resting})");
        }
        // a breather that cannot mend what it waits for (someone bleeding) ends and does not start straight again
        {
            var stuck = new SoulDungeonSession(village.Dungeon, rules, new[] { Make("hans"), Make("bram") }, 83, 1) { Unattended = true, AutoExplore = false };
            foreach (var m in stuck.Monsters) m.Position = new Vector2(-500, -500);
            var torn = stuck.Mercenaries[0];
            torn.AddWounds(SoulMercenary.MaxWounds);            // bleeds: never back to health by resting
            stuck.BeginRetreat("test");
            float rested = 0, longest = 0, run = 0;
            for (int i = 0; i < 2400; i++)
            {
                stuck.Tick(.05f);
                if (stuck.Resting) { rested += .05f; run += .05f; longest = Mathf.Max(longest, run); } else run = 0;
                if (stuck.Recalled || stuck.Plan != SoulPartyPlan.Retreat) break;
            }
            Require(longest <= SoulDungeonSession.BreatherMax + 1f, $"a breather ends (longest {longest:0} s, {rested:0} s in all)");
        }

        // every priest of standing carries a blessing
        foreach (var recruit in village.Recruits)
            if (recruit.Job == "성직자" && recruit.Rarity != SoulStyleRarity.Normal)
                Require(System.Array.Exists(recruit.Actives, s => s != null && s.Trigger == SoulTrigger.AllySupport), $"{recruit.DisplayName} has a blessing");

        // magic is strong and dear: 치유의 빛 costs 19 MP, heals 1.8 × as much, back in 7.2 s — MP is what runs out;
        // its wound mending is a roll on 마력
        {
            var light = AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/SkillHealingLight.asset");
            float cost = System.Array.Find(light.Costs, c => c.Resource == SoulResource.Mana).Amount;
            Require(Mathf.Approximately(cost, 19) && Mathf.Approximately(light.Cooldown, 7.2f) && Mathf.Approximately(light.Heal.Flat, 18f),
                $"치유의 빛: {cost} MP, {light.Cooldown} s, heal {light.Heal.Flat}");
            var priest = Make("mariel");
            float chance = light.WoundChance.Evaluate(priest.Stats.Combat);
            Require(light.WoundRoll && Mathf.Approximately(chance, .25f + priest.Stats.Total(StatType.Magic) * .01f), $"치유의 빛 mends a wound by chance: 25% + 마력 1% ({chance:P0} at 마력 {priest.Stats.Total(StatType.Magic):0})");
        }

        // 치명타: the stats read as they are meant and a crit lands harder
        {
            Require(SoulStatRules.CombatName(StatType.CritChance) == "치명타 확률" && SoulDescribe.Signed(StatType.CritChance, .08f) == "+8%"
                && SoulDescribe.Signed(StatType.CritDamage, .25f) == "+25%", "치명타 stats show as 치명타 확률 +8% · 치명타 피해 +25%");
            var striker = Make("hans"); var dummy = Make("bram");
            striker.AddTimedBuff("test_crit", new[] { new SoulStatBonus { Stat = StatType.CritChance, Value = 1f }, new SoulStatBonus { Stat = StatType.Accuracy, Value = 5f } }, 99);
            dummy.Hp = 99999;
            var plain = SoulCombat.Resolve(Make("hans"), dummy, new SoulHit { School = SoulDamageSchool.Physical, Kind = SoulDamageKind.Slash, Raw = 40, Melee = true }, new System.Random(1));
            dummy.Hp = 99999;
            var crit = SoulCombat.Resolve(striker, dummy, new SoulHit { School = SoulDamageSchool.Physical, Kind = SoulDamageKind.Slash, Raw = 40, Melee = true }, new System.Random(1));
            Require(crit.Critical && crit.Damage > plain.Damage * 1.2f, $"a 치명타 lands harder ({plain.Damage:0} → {crit.Damage:0})");
        }

        // provisions: 2 kinds (up to 4 with the guild's 준비물 칸), 3 of each
        {
            var packer = new SoulCampaign(village, 95);
            Require(packer.CarryKinds == 2 && packer.CarryLimit == 3, $"a party starts with 2 kinds of provisions, 3 of each ({packer.CarryKinds} × {packer.CarryLimit})");
            packer.Perks[SoulPerk.CarryKinds] = 2;
            Require(packer.CarryKinds == 4, "준비물 칸: 4 kinds at most");
        }

        // gear found in the dungeon goes on whoever needs it — an empty slot too — and the rest to the storehouse
        {
            var finder = new SoulDungeonSession(village.Dungeon, rules, new[] { Make("hans"), Make("bram") }, 82, 1) { AutoExplore = false };
            var bare = finder.Mercenaries[0];
            var hood = bare.Equipped(SoulEquipSlot.Head);
            if (hood != null) bare.Equipment.Remove(hood);
            bare.Rebuild(rules);
            var helmetData = System.Array.Find(village.Dungeon.LootTable, e => e != null && e.Slot == SoulEquipSlot.Head && e.BaseWeight <= 3f);
            Require(helmetData != null, "a light helmet in the loot table");
            finder.Inventory.Add(new SoulItem(helmetData, 2));
            finder.Tick(.05f);
            Require(finder.Inventory.Count == 0 && bare.Equipped(SoulEquipSlot.Head) != null || finder.Mercenaries[1].Equipped(SoulEquipSlot.Head)?.Data == helmetData,
                $"a found helmet goes on someone with an empty head slot (left: {finder.Inventory.Count})");
            var quiverData = System.Array.Find(village.Dungeon.LootTable, e => e != null && e.Kind == "화살통");
            if (quiverData != null)
            {
                finder.Inventory.Add(new SoulItem(quiverData, 1));
                finder.Tick(.05f);
                Require(finder.Inventory.Count == 1, "a quiver nobody with a bow needs stays in the bag (for the storehouse)");
            }
        }

        // every monster has its baked picture, one sheet for all (a soul's card without the monster on screen); the
        // sprite-library ones sharing a library each get their own (tinted) picture
        {
            Texture2D sheetTexture = null;
            var libraryPictures = new Dictionary<EnemyRace, HashSet<string>>();
            foreach (string guid in AssetDatabase.FindAssets("t:SoulMonsterData", new[] { DataPath }))
            {
                var monster = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(AssetDatabase.GUIDToAssetPath(guid));
                if (monster == null) continue;
                Require(monster.Portrait != null, $"{monster.Name} has a baked portrait");
                Require(sheetTexture == null || monster.Portrait.texture == sheetTexture, $"{monster.Name}: all portraits share one sheet");
                sheetTexture = monster.Portrait.texture;
                if (!monster.UseMonsterSprite) continue;
                if (!libraryPictures.TryGetValue(monster.MonsterRace, out var names)) libraryPictures[monster.MonsterRace] = names = new HashSet<string>();
                Require(names.Add(monster.Portrait.name) && monster.Portrait.rect.width < PortraitCell, $"{monster.Name}: its own picture, cut to the body");
            }
        }
        // accessories go by what they do for the wearer: a +운 ring is nothing to a mage
        {
            var luckRing = new SoulItem(AssetDatabase.LoadAssetAtPath<SoulEquipmentData>(DataPath + "/EquipmentCopperRing.asset"), 3);
            luckRing.Enchants.Add(new SoulEnchant { Option = "luck", Tier = 3 });
            var magicRing = new SoulItem(AssetDatabase.LoadAssetAtPath<SoulEquipmentData>(DataPath + "/EquipmentCopperRing.asset"), 3);
            magicRing.Enchants.Add(new SoulEnchant { Option = "mag", Tier = 3 });
            var mage = Make("oscar");
            foreach (var worn in mage.Equipment.FindAll(e => e.Slot == SoulEquipSlot.Accessory)) mage.Equipment.Remove(worn);
            Require(SoulCampaign.UpgradeGain(mage, luckRing, out _) == 0 && SoulCampaign.UpgradeGain(mage, magicRing, out _) > 0,
                "a mage takes a +마력 ring, never a +운 one");
        }

        // the new jobs: a 궁수 shoots from afar, a 소환사's spirit fights at its side; a mage out of MP never steps up
        {
            Require(System.Array.Exists(village.HireTemplates, t => t.Job == "궁수") && System.Array.Exists(village.HireTemplates, t => t.Job == "소환사")
                && System.Array.FindAll(village.Recruits, r => r.Job == "궁수").Length >= 5 && System.Array.FindAll(village.Recruits, r => r.Job == "소환사").Length >= 5, "궁수 and 소환사 are hired");
            Require(SoulSpiritArt.Frames("fire").Length == 2 && SoulSpiritArt.Frames("light").Length == 2, "the spirits' pictures are there");
            var call = new SoulDungeonSession(village.Dungeon, rules, new[] { Make("hans"), Make("aeris") }, 81, 1) { AutoExplore = false };
            foreach (var m in call.Monsters) m.Position = new Vector2(-500, -500);
            var wolfData = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterWolf.asset");
            Vector2 open = call.Map.Center(call.PartyStart);
            var wolf = call.AddMonster(wolfData, open + Vector2.right * 3f);
            var front = call.Mercenaries[0]; var caller = call.Mercenaries[1];
            front.Position = open + Vector2.right * 1.6f; caller.Position = open;
            float hp = wolf.Stats.Total(StatType.MaxHp);
            bool spirit = false; float nearest = float.MaxValue;
            for (int i = 0; i < 200 && wolf.Alive; i++)
            {
                front.Hp = front.Stats.Total(StatType.MaxHp); caller.Hp = caller.Stats.Total(StatType.MaxHp); wolf.Hp = Mathf.Max(wolf.Hp, 1);
                call.Tick(.05f);
                spirit |= call.SpiritsOf(caller) > 0;
                if (i > 40) nearest = Mathf.Min(nearest, Vector2.Distance(caller.Position, wolf.Position));
            }
            Require(spirit && call.SpiritsOf(caller) <= SoulDungeonSession.SpiritCap, $"a summoner calls its spirits ({call.SpiritsOf(caller)})");
            var mageRun = new SoulDungeonSession(village.Dungeon, rules, new[] { Make("hans"), Make("oscar") }, 80, 1) { AutoExplore = false };
            foreach (var m in mageRun.Monsters) m.Position = new Vector2(-500, -500);
            var foe = mageRun.AddMonster(wolfData, open + Vector2.right * 2f);
            var tank = mageRun.Mercenaries[0]; var mage = mageRun.Mercenaries[1];
            tank.Position = open + Vector2.right * .8f; mage.Position = open + Vector2.left * .5f;
            float closest = float.MaxValue;
            for (int i = 0; i < 200; i++)
            {
                tank.Hp = tank.Stats.Total(StatType.MaxHp); mage.Hp = mage.Stats.Total(StatType.MaxHp); mage.Mp = 0; foe.Hp = foe.Stats.Total(StatType.MaxHp);
                mageRun.Tick(.05f);
                if (i > 40) closest = Mathf.Min(closest, Vector2.Distance(mage.Position, foe.Position));
            }
            Require(closest > 1.6f, $"a mage out of MP stays behind, no blows ({closest:0.0} from the wolf at the closest)");
        }

        // casters: no attack patterns (the frontline ones keep theirs); a weapon that is not a staff costs them 40% of their magic
        {
            var mage = Make("oscar"); var battleMage = Make("theron"); var chaplain = Make("mariel"); var warPriest = Make("seraphin");
            bool Fights(SoulMercenary hero) => hero.AllPatterns().Exists(p => p != null && p.Category == SoulPatternCategory.Attack && !p.CastsSkills);
            Require(!Fights(mage) && !Fights(chaplain) && !Fights(Make("lumi")) && Fights(battleMage) && Fights(warPriest) && Fights(Make("hans")) && Fights(Make("bram")),
                "no attack pattern comes with a mage, a priest or a summoner (베기 is the 검사's) — the 전투 마법사 and 전투 사제 bring theirs");
            var sword = new SoulItem(AssetDatabase.LoadAssetAtPath<SoulEquipmentData>(DataPath + "/EquipmentSword.asset"), 1);
            float staffPower = SoulSkillUsePolicy.Focus(mage);
            foreach (var hero in new[] { mage, battleMage }) { hero.Equipment.RemoveAll(e => e.Slot == SoulEquipSlot.MainHand); hero.Equipment.Add(sword.Copy()); hero.Rebuild(rules); }
            Require(Mathf.Approximately(staffPower, 1f) && Mathf.Abs(SoulSkillUsePolicy.Focus(mage) - .6f) < .01f && Mathf.Approximately(SoulSkillUsePolicy.Focus(battleMage), 1f),
                $"with a sword a mage's magic is 40% down ({SoulSkillUsePolicy.Focus(mage):0.00}); a 전투 마법사 keeps it ({SoulSkillUsePolicy.Focus(battleMage):0.00})");
        }
        // 자동 장착 at the guild: the storehouse's better gear goes on, what it replaces goes back to the storehouse
        {
            var armory = new SoulCampaign(village, 74);
            var knight = armory.Roster[0];
            var sword = knight.Equipped(SoulEquipSlot.MainHand);
            var better = new SoulItem(sword.Data, Mathf.Min(6, sword.Grade + 2));
            var junk = new SoulItem(sword.Data, 1);
            armory.Inventory.Add(better); armory.Inventory.Add(junk);
            var worn = armory.AutoEquip(knight);
            Require(worn.Contains(better) && knight.Equipped(SoulEquipSlot.MainHand) == better && armory.Inventory.Contains(junk) && !armory.Inventory.Contains(better) && armory.Inventory.Contains(sword)
                && armory.NextUpgrade(knight, out _) == null, $"자동 장착 wears the better sword and leaves the worse one ({worn.Count} worn)");
        }

        // the swap screen's numbers are what the swap really does (the guild's abilities on top included)
        {
            var fitting = new SoulCampaign(village, 73);
            fitting.BuyPerk(SoulPerk.Strength);
            var knight = fitting.Roster[0];
            var sword = knight.Equipped(SoulEquipSlot.MainHand);
            var better = new SoulItem(sword.Data, Mathf.Min(6, sword.Grade + 2));
            fitting.Inventory.Add(better);
            var predicted = fitting.SwapChanges(knight, better);
            var was = new Dictionary<StatType, float>();
            foreach (StatType stat in System.Enum.GetValues(typeof(StatType))) was[stat] = knight.Stats.Total(stat);
            Require(fitting.Equip(knight, better), "the better sword goes on");
            foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
            {
                float real = knight.Stats.Total(stat) - was[stat];
                predicted.TryGetValue(stat, out float told);
                Require(Mathf.Abs(real - told) < .01f, $"the swap screen tells {stat} right ({told:0.##} shown, {real:0.##} real)");
            }
        }

        // a quiver hangs beside a bow (both hands), not beside a greatsword; 일괄 판매 sells the lot at once
        {
            var range = new SoulCampaign(village, 72);
            var bowman = SoulDungeonSession.RecruitOne(System.Array.Find(village.HireTemplates, t => t.Job == "궁수"), rules);
            range.Roster.Add(bowman);
            var quiver = new SoulItem(AssetDatabase.LoadAssetAtPath<SoulEquipmentData>(DataPath + "/EquipmentQuiver.asset"), 2);
            range.Inventory.Add(quiver);
            Require(SoulCampaign.EquipBlock(bowman, quiver) == null && range.Equip(bowman, quiver) && bowman.Equipped(SoulEquipSlot.OffHand) == quiver, "a quiver goes beside a bow");
            var longBow = new SoulItem(AssetDatabase.LoadAssetAtPath<SoulEquipmentData>(DataPath + "/EquipmentLongBow.asset"), 3);
            Require(!SoulCampaign.Replaced(bowman, longBow).Contains(quiver), "a new bow keeps the quiver");
            var junk = new List<SoulItem>();
            for (int g = 1; g <= 3; g++) { var piece = new SoulItem(AssetDatabase.LoadAssetAtPath<SoulEquipmentData>(DataPath + "/EquipmentCopperRing.asset"), 1); range.Inventory.Add(piece); junk.Add(piece); }
            range.Levels[SoulBuildingKind.Shop] = 1;
            int gold = range.Gold, price = 0; foreach (var piece in junk) price += SoulCampaign.SellPrice(piece);
            Require(range.SellMany(junk) == price && range.Gold == gold + price && !range.Inventory.Exists(junk.Contains), "일괄 판매 sells the lot");
        }

        // the helmet's look: hidden by default, shown with the eye; saved with the mercenary
        {
            var dressing = new SoulCampaign(village, 71);
            var wearer = dressing.Roster[0];
            var helmetData = System.Array.Find(village.Dungeon.LootTable, e => e != null && e.Slot == SoulEquipSlot.Head && !string.IsNullOrEmpty(e.Look.Helmet));
            var helmet = new SoulItem(helmetData, 2);
            dressing.Inventory.Add(helmet);
            Require(dressing.Equip(wearer, helmet) && string.IsNullOrEmpty(wearer.Stats.Appearance.Helmet), "a worn helmet does not show by default");
            dressing.ToggleHelmet(wearer);
            Require(wearer.Stats.Appearance.Helmet == helmetData.Look.Helmet, "the eye shows it");
            var back = SoulSave.Read(SoulSave.Write(dressing), village).Roster.Find(h => h.Id == wearer.Id);
            Require(back != null && back.ShowHelmet && back.Stats.Appearance.Helmet == helmetData.Look.Helmet, "the choice is saved");
        }

        // the guild shows a mix of jobs
        {
            var guild = new SoulCampaign(village, 75);
            guild.RefreshOffers();
            var shown = new HashSet<string>();
            foreach (var offer in guild.Offers) shown.Add(offer.Recruit.Job);
            Require(shown.Count == guild.Offers.Count, $"the hire list shows different jobs ({string.Join(", ", shown)})");
        }

        // 주문 집중: gathered while no spell can go (3 at most), all spent on the next attack spell (+25% each);
        // 자연 교감: the party quicker by 2% a stack, stacking to ten
        {
            var focusPattern = AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/SpellFocus.asset");
            var communionPattern = AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/NatureCommunion.asset");
            var fight = new SoulDungeonSession(village.Dungeon, rules, new[] { Make("hans"), Make("oscar"), Make("lumi") }, 76, 1) { AutoExplore = false };
            foreach (var m in fight.Monsters) m.Position = new Vector2(-500, -500);
            var wolfData = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterWolf.asset");
            Vector2 open = fight.Map.Center(fight.PartyStart);
            var front = fight.Mercenaries[0]; var mage = fight.Mercenaries[1]; var caller = fight.Mercenaries[2];
            front.Position = open; mage.Position = open + Vector2.left * 2f; caller.Position = open + Vector2.left * 2.5f + Vector2.up * .8f;
            var wolf = fight.AddMonster(wolfData, open + Vector2.right * 1.2f);
            Require(mage.Patterns().Contains(focusPattern) && caller.Patterns().Contains(communionPattern), "mages know 주문 집중, summoners 자연 교감");
            int most = 0, stacks = 0;
            for (int i = 0; i < 600; i++)
            {
                foreach (var h in fight.Mercenaries) h.Hp = h.Stats.Total(StatType.MaxHp);
                mage.Mp = 0; caller.Mp = 0; wolf.Hp = wolf.Stats.Total(StatType.MaxHp);
                fight.Tick(.05f);
                most = Mathf.Max(most, mage.Focus);
                var held = front.TimedBuffs.Find(b => b.Id == communionPattern.Id);
                if (held != null) stacks = Mathf.Max(stacks, held.Stacks);
            }
            Require(most == SoulDungeonSession.MaxFocus, $"out of MP in a fight, a mage gathers focus up to {SoulDungeonSession.MaxFocus} ({most})");
            var held2 = front.TimedBuffs.Find(b => b.Id == communionPattern.Id);
            Require(stacks >= 3 && stacks <= 10 && held2 != null && Mathf.Abs(held2.Bonuses[0].Value - .02f * held2.Stacks) < .001f,
                $"자연 교감 stacks on the party, 2% a stack ({stacks} stacks)");
            var bolt = AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/SkillFireBolt.asset");
            mage.Mp = mage.Stats.Total(StatType.MaxMp); mage.SkillCooldowns.Clear(); mage.FocusPower = 1; mage.Focus = 0;
            float plain = SoulSkillUsePolicy.Power(mage, bolt);
            mage.Focus = 3;
            Require(SoulSkillUsePolicy.TryCommit(mage, bolt, wolf) && mage.Focus == 0 && Mathf.Abs(SoulSkillUsePolicy.Power(mage, bolt) / plain - 1.75f) < .01f,
                "the next attack spell takes all the focus: +75% for three");
        }

        // 기도: a priest in a fight mends the most hurt ally a little (4 + 마력×20%), for stamina
        {
            var prayerPattern = AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/Prayer.asset");
            var chaplain = Make("mariel");
            Require(prayerPattern != null && chaplain.Patterns().Contains(prayerPattern), "every priest knows 기도");
            var pray = new SoulDungeonSession(village.Dungeon, rules, new[] { Make("hans"), chaplain }, 77, 1) { AutoExplore = false };
            foreach (var m in pray.Monsters) m.Position = new Vector2(-500, -500);
            var wolfData = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterWolf.asset");
            Vector2 open = pray.Map.Center(pray.PartyStart);
            var hurt = pray.Mercenaries[0]; var priest = pray.Mercenaries[1];
            hurt.Position = open; priest.Position = open + Vector2.left * 2f;
            var wolf = pray.AddMonster(wolfData, open + Vector2.right * 1.2f);
            float max = hurt.Stats.Total(StatType.MaxHp);
            priest.Mp = 0; // no 치유의 빛: only the prayer
            float expected = prayerPattern.Heal.Evaluate(priest.Stats.Combat), jump = 0;
            for (int i = 0; i < 100 && jump < expected * .8f; i++)
            {
                hurt.Hp = max * .5f; priest.Mp = 0; wolf.Hp = wolf.Stats.Total(StatType.MaxHp);
                float before = hurt.Hp;
                pray.Tick(.05f);
                jump = Mathf.Max(jump, hurt.Hp - before);
            }
            Require(jump >= expected * .8f && jump < max * .2f, $"기도 mends a little ({jump:0.0} of 4 + 마력×20% = {expected:0.0})");
        }

        // the new spells: a shield takes a blow first, a wall of fire is a line of burning ground, a mage caught up close
        // swings its staff (no 베기) and steps back
        {
            var shieldSkill = AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/SkillHolyShield.asset");
            var holder = Make("hans");
            holder.Barrier = 30; holder.AddTimedBuff(SoulCombat.BarrierKey, new[] { new SoulStatBonus { Stat = StatType.Armor, Value = 0 } }, 10);
            float hp0 = holder.Hp;
            SoulCombat.Resolve(Make("bram"), holder, new SoulHit { School = SoulDamageSchool.Magic, Kind = SoulDamageKind.Arcane, Raw = 20 }, new System.Random(3));
            Require(shieldSkill != null && shieldSkill.HasEffect() && holder.Hp == hp0 && holder.Barrier < 30, $"신성한 방패 takes the blow first (hp {hp0:0} → {holder.Hp:0}, shield {holder.Barrier:0})");
            var wall = new SoulDungeonSession(village.Dungeon, rules, new[] { Make("oscar") }, 79, 1) { AutoExplore = false };
            foreach (var m in wall.Monsters) m.Position = new Vector2(-500, -500);
            var wolfData = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterWolf.asset");
            Vector2 open = wall.Map.Center(wall.PartyStart);
            var fireWall = AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/SkillFireWall.asset");
            var burner = wall.Mercenaries[0]; burner.Position = open;
            var mark = wall.AddMonster(wolfData, open + Vector2.right * 4.5f);
            burner.Mp = burner.Stats.Total(StatType.MaxMp);
            wall.CastSkill(burner, mark, fireWall, out bool cast);
            for (int i = 0; i < 60 && wall.FieldCount < 2; i++) wall.Tick(.05f);
            Require(wall.FieldCount >= 2, $"화염 벽 lays a line of burning ground ({wall.FieldCount} patches)");
            var close = new SoulDungeonSession(village.Dungeon, rules, new[] { Make("hans"), Make("oscar") }, 78, 1) { AutoExplore = false };
            foreach (var m in close.Monsters) m.Position = new Vector2(-500, -500);
            var sword = close.Mercenaries[0]; var mage = close.Mercenaries[1];
            Vector2 roomy = open; float room = 0;
            for (int y = 0; y < close.Map.Height; y++)
                for (int x = 0; x < close.Map.Width; x++)
                {
                    var c = close.Map.Center(new Vector2Int(x, y));
                    float r = 0; while (r < 4 && close.Map.Clear(c, r + .5f)) r += .5f;
                    if (r > room) { room = r; roomy = c; }
                }
            var biter = close.AddMonster(wolfData, roomy + Vector2.right * .9f);
            mage.Position = roomy; sword.Position = roomy + Vector2.right * 3f;
            bool swung = false, slashed = false, stepped = false;
            for (int i = 0; i < 120; i++)
            {
                mage.Mp = 0; mage.Hp = mage.Stats.Total(StatType.MaxHp); sword.Hp = sword.Stats.Total(StatType.MaxHp); biter.Hp = biter.Stats.Total(StatType.MaxHp);
                biter.CurrentTarget = mage;
                close.Tick(.05f);
                swung |= mage.Action == "기본 공격"; slashed |= mage.Action == "베기";
                stepped |= swung && mage.Action == "치고 빠지기";
            }
            Require(swung && stepped && !slashed && mage.Equipped(SoulEquipSlot.MainHand)?.BasicAttack?.Id == "휘두르기",
                $"a mage caught up close swings its staff (휘두르기), never 베기, and steps back (swung {swung}, stepped {stepped}, slashed {slashed})");
        }

        // an old save's 명상 pattern turns into the passive
        {
            var old = new SoulCampaign(village, 94);
            var sage = old.Roster[0];
            var meditatePattern = AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/Meditate.asset");
            sage.LearnedPatterns.Add(meditatePattern);
            var back = SoulSave.Read(SoulSave.Write(old), village).Roster.Find(h => h.Id == sage.Id);
            Require(back != null && !back.LearnedPatterns.Contains(meditatePattern) && back.StartingPassives.Contains(meditatePattern.NowPassive), "a saved 명상 pattern becomes the 명상 passive");
        }

        // running away, a healer still heals on the way
        {
            var run = new SoulDungeonSession(village.Dungeon, rules, new[] { Make("hans"), Make("mariel") }, 84, 1) { AutoExplore = false };
            foreach (var m in run.Monsters) m.Position = new Vector2(-500, -500);
            Vector2 home = run.Map.Center(run.PartyStart);
            var hurt = run.Mercenaries[0]; var healer = run.Mercenaries[1];
            hurt.Position = home + Vector2.right * 30f;
            if (!run.Map.Clear(hurt.Position, .5f) && run.Map.ClosestReachable(hurt.Position, home, .5f, out var spot)) hurt.Position = spot;
            healer.Position = hurt.Position + Vector2.right * .6f;
            var light = healer.ActiveSkills().Find(s => s != null && s.SoulId == "healing_light");
            float mend = light.Heal.Evaluate(healer.Stats.Combat), max = hurt.Stats.Total(StatType.MaxHp);
            hurt.Hp = Mathf.Max(max * .55f, max - mend);
            run.OrderEscape();
            float jump = 0;
            for (int i = 0; i < 80 && jump < mend * .5f; i++) { float last = hurt.Hp; run.Tick(.05f); jump = Mathf.Max(jump, hurt.Hp - last); }
            Require(jump >= mend * .5f, $"a heal still goes up while running away (best jump {jump:0} of {mend:0})");
        }

        // the fist fighters punch: 권갑 in the main hand, no hunting knife
        foreach (string id in new[] { "yak", "haku" })
        {
            var fighter = Make(id);
            var main = fighter.Equipment.Find(item => item.Slot == SoulEquipSlot.MainHand);
            Require(main != null && main.Id == "knuckles" && main.BasicAttack != null && main.BasicAttack.WeaponTag == "fist" && main.Data.Icon != null,
                $"{id} fights with 권갑 ({(main != null ? main.Data.DisplayName : "nothing")})");
        }
        // a heal is used once it would not be wasted, not only below half HP
        {
            var carer = new SoulDungeonSession(dungeon, rules, new[] { Make("hans"), Make("mariel") }, 91, 1) { AutoExplore = false };
            foreach (var m in carer.Monsters) m.Position = new Vector2(-500, -500);
            var patient = carer.Mercenaries[0]; var healer = carer.Mercenaries[1];
            healer.Position = patient.Position + Vector2.right;
            var light = healer.ActiveSkills().Find(s => s != null && s.SoulId == "healing_light");
            Require(light != null, "마리엘 knows 치유의 빛");
            float mend = light.Heal.Evaluate(healer.Stats.Combat);
            float max = patient.Stats.Total(StatType.MaxHp);
            patient.Hp = Mathf.Max(max * .55f, max - mend);
            float before = patient.Hp, jump = 0;
            for (int i = 0; i < 80 && jump < mend * .5f; i++) { float last = patient.Hp; carer.Tick(.05f); jump = Mathf.Max(jump, patient.Hp - last); }
            Require(jump >= mend * .5f, $"치유의 빛 mends an ally at {before / max:P0} missing its worth ({mend:0} heal, best jump {jump:0})");
        }

        // on the way out: the walk costs no stamina; a pursuer that catches one gets the whole party; a fight given
        // up as lost is taken up again once it shrinks (a retreat for other reasons is not)
        {
            var walk = new SoulDungeonSession(dungeon, rules, new[] { Make("hans"), Make("bram") }, 94, 1) { AutoExplore = false };
            Vector2 home = walk.Map.Center(walk.PartyStart);
            SoulMonster far = null;
            foreach (var m in walk.Monsters) if (far == null || Vector2.Distance(m.Position, home) > Vector2.Distance(far.Position, home)) far = m;
            Vector2 away = far.Position;
            foreach (var m in walk.Monsters) m.Position = new Vector2(-500, -500);
            walk.Mercenaries[0].Position = away; walk.Mercenaries[1].Position = away + Vector2.right * .3f;
            var breath = new float[2];
            for (int k = 0; k < 2; k++) { var h = walk.Mercenaries[k]; h.Stamina = h.Stats.Total(StatType.MaxStamina) * .5f; breath[k] = h.Stamina; }
            walk.OrderEscape();
            for (int i = 0; i < 80; i++) walk.Tick(.05f);
            Require(Vector2.Distance(walk.Mercenaries[0].Position, away) > 2 && walk.Mercenaries[0].Stamina >= breath[0] && walk.Mercenaries[1].Stamina >= breath[1],
                $"the walk home costs no stamina ({breath[0]:0} → {walk.Mercenaries[0].Stamina:0}, walked {Vector2.Distance(walk.Mercenaries[0].Position, away):0.0})");
        }
        SoulDungeonSession Chase(int seed, bool unattended, out SoulMercenary caughtOne, out SoulMercenary other, out SoulMonster chaser)
        {
            var a = Make("hans"); var b = Make("bram");
            var s = new SoulDungeonSession(dungeon, rules, new[] { a, b }, seed, 1) { AutoExplore = false, Unattended = unattended };
            SoulMonster weak = null;
            foreach (var m in s.Monsters) if (weak == null && !m.Data.Elite && !m.Data.Guardian) weak = m;
            foreach (var m in s.Monsters) if (m != weak) m.Position = new Vector2(-500, -500);
            a.Position = weak.Position;
            for (int k = 0; k < 16; k++)
            {
                var spot = weak.Position + new Vector2(Mathf.Cos(k * Mathf.PI / 8), Mathf.Sin(k * Mathf.PI / 8)) * 1.1f;
                if (s.Map.Clear(spot, .4f)) { a.Position = spot; break; }
            }
            b.Position = a.Position;
            for (float r = 7f; r >= 5f && b.Position == a.Position; r -= .25f)
                for (int k = 0; k < 16; k++)
                {
                    var spot = a.Position + new Vector2(Mathf.Cos(k * Mathf.PI / 8), Mathf.Sin(k * Mathf.PI / 8)) * r;
                    if (s.Map.Clear(spot, .4f) && s.Map.Sight(a.Position, spot)) { b.Position = spot; break; }
                }
            weak.CurrentTarget = a;
            caughtOne = a; other = b; chaser = weak;
            return s;
        }
        {
            var caughtRun = Chase(93, true, out var caughtHero, out var mate, out var chaser);
            float apart = Vector2.Distance(mate.Position, caughtHero.Position);
            caughtRun.BeginRetreat("test"); // an automatic retreat (the player's 탈출 walks on regardless)
            bool stood = false, joined = false;
            for (int i = 0; i < 80 && chaser.Alive; i++)
            {
                caughtRun.Tick(.05f);
                stood |= caughtRun.Standing;
                joined |= mate.CurrentTarget == chaser;
            }
            Require(apart > 4.5f && stood && (joined || !chaser.Alive || Vector2.Distance(mate.Position, caughtHero.Position) < apart - 1),
                $"caught on the way out, the whole party turns and fights ({apart:0.0} apart → {Vector2.Distance(mate.Position, caughtHero.Position):0.0}, stood {stood}, joined {joined})");
        }
        {
            var lost = Chase(92, true, out var _, out var _, out var _);
            lost.BeginRetreat("test", oddsLost: true);
            for (int i = 0; i < 40 && lost.Plan == SoulPartyPlan.Retreat; i++) lost.Tick(.05f);
            Require(lost.Plan != SoulPartyPlan.Retreat, $"a fight given up as lost is taken up again once it is winnable ({lost.Plan})");
            var home = Chase(92, true, out var _, out var _, out var _);
            home.BeginRetreat("test");
            for (int i = 0; i < 40; i++) home.Tick(.05f);
            Require(home.Plan == SoulPartyPlan.Retreat, $"a retreat for other reasons goes on ({home.Plan})");
        }
    }

    static void ValidateVillage(SoulDungeonData dungeon, SoulStatRules rules, SoulMercenaryData ria, SoulMercenaryData thorData)
    {
        var village = AssetDatabase.LoadAssetAtPath<SoulVillageData>(DataPath + "/Village.asset");
        var seraData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenarySera.asset");
        Require(village != null && village.Buildings.Length == System.Enum.GetValues(typeof(SoulBuildingKind)).Length && village.TileSprites.Length > 0 && village.DoorLeft != null, "village data with every building, tiles and doors");
        foreach (var sold in village.ShopEquipment) Require(sold.Icon != null, $"{sold.Name} has an icon");
        foreach (var found in village.Dungeon.LootTable) Require(found.Icon != null, $"{found.Name} has an icon");
        foreach (var def in village.Buildings) Require(def.Interior != null, $"{def.Name} has an interior");
        foreach (var def in village.Buildings)
            Require(def.Sprites != null && def.Sprites.Length == Mathf.Max(2, def.UpgradeCosts.Length + 1) && System.Array.TrueForAll(def.Sprites, s => s != null), $"{def.Name}: a sprite for the plot and each level");
        var rows = village.GroundRows.Split('\n');
        foreach (var a in village.Buildings)
        {
            Require(a.Cell.x >= 0 && a.Cell.y >= 0 && a.Cell.x + a.Size.x <= rows[0].Length && a.Cell.y + a.Size.y <= rows.Length, $"{a.Name} fits the map");
            foreach (var b in village.Buildings)
                if (a.Kind < b.Kind) Require(!new RectInt(a.Cell, a.Size).Overlaps(new RectInt(b.Cell, b.Size)), $"{a.Name} and {b.Name} do not overlap");
        }

        // Wounds: damage piles up into wounds; they cost stamina and are not rested away.
        var session = new SoulDungeonSession(dungeon, rules, new[] { ria, thorData }, 21);
        var hero = session.Mercenaries[0];
        float fresh = SoulCombat.WoundCostScale(hero);
        hero.TakeWoundDamage(hero.Stats.Total(StatType.MaxHp) * (SoulMercenary.WoundThreshold * 2 + .01f));
        Require(hero.Wounds == 2 && SoulCombat.WoundCostScale(hero) > fresh && SoulCombat.WoundFailChance(hero) == 0, $"damage leaves wounds that cost ({hero.Wounds})");
        hero.AddWounds(2);
        Require(SoulCombat.WoundFailChance(hero) > 0, "from the fourth wound patterns can fail");
        hero.HealWounds(2);
        // A potion is drunk under 30% HP and closes a wound.
        session.Potions = 1;
        hero.Hp = hero.Stats.Total(StatType.MaxHp) * .2f;
        session.Tick(.05f);
        Require(session.Potions == 0 && hero.Wounds == 1 && hero.Hp > hero.Stats.Total(StatType.MaxHp) * .5f, "a potion heals and closes a wound");

        // Healing is the priest's: the mage cannot use it. The healing spell mends the most hurt ally and a wound.
        var priestData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryPriest.asset");
        var healingLight = AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/SkillHealingLight.asset");
        var mageUnit = new SoulDungeonSession(dungeon, rules, new[] { seraData }, 24).Mercenaries[0];
        Require(SoulSkillUsePolicy.Blocked(mageUnit, healingLight) != null, "a mage cannot heal");
        Require(mageUnit.ActiveSkills().Exists(s => SoulSkillUsePolicy.Magical(s) && s.Damage != null && s.Damage.Evaluate(mageUnit.Stats.Combat) > 0), "the mage has attack magic again");
        // attack magic is dear (a big share of the pool) and MP comes back slowly; spell power grows with magic and
        // falls with any weapon that is not a staff, a wand or a holy symbol
        var bolt = mageUnit.ActiveSkills().Find(s => s.SoulId == "fire_bolt");
        SoulSkillUsePolicy.Costs(mageUnit, bolt, out _, out float boltMana);
        Require(boltMana >= mageUnit.Stats.Total(StatType.MaxMp) * .3f && mageUnit.Stats.Total(StatType.MpRegen) < .6f,
            $"attack magic is dear and slow to come back ({boltMana:0} of {mageUnit.Stats.Total(StatType.MaxMp):0} MP, {mageUnit.Stats.Total(StatType.MpRegen):0.00}/s)");
        float staffPower = SoulSkillUsePolicy.SpellPower(mageUnit);
        var bowData = System.Array.Find(village.AllEquipment, e => e != null && e.Id == "short_bow");
        mageUnit.Equipment.RemoveAll(i => i.Slot == SoulEquipSlot.MainHand);
        mageUnit.Equipment.Add(new SoulItem(bowData, 2));
        mageUnit.Rebuild(rules);
        Require(staffPower > 1.3f && SoulSkillUsePolicy.SpellPower(mageUnit) < staffPower * .6f, $"a bow in hand halves the magic ({staffPower:0.00} → {SoulSkillUsePolicy.SpellPower(mageUnit):0.00})");
        var clinic = new SoulDungeonSession(dungeon, rules, new[] { priestData, ria }, 22);
        clinic.AutoExplore = false;
        var healer = clinic.Mercenaries[0]; var patient = clinic.Mercenaries[1];
        patient.Position = healer.Position + Vector2.right * 1.5f;
        patient.AddWounds(2);
        patient.Hp = patient.Stats.Total(StatType.MaxHp) * .3f;
        bool mended = false;
        for (int i = 0; i < 40 && !mended; i++) { clinic.Tick(.05f); mended = patient.Wounds == 1; }
        Require(mended, "치유의 빛 closes a wound on a hurt ally");

        // 소생: no priest knows it by nature or by level — only once learned does it bring the fallen back.
        var resurrection = AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/SkillResurrection.asset");
        var shrine = new SoulDungeonSession(dungeon, rules, new[] { priestData, ria }, 25);
        shrine.AutoExplore = false;
        var elder = shrine.Mercenaries[0]; var lost = shrine.Mercenaries[1];
        lost.Position = elder.Position + Vector2.right * 1.5f;
        while (elder.Level < 40) elder.AddExperience(elder.ExperienceToNext, rules, new System.Random(1));
        Require(!elder.AllActiveSkills().Contains(resurrection), "levels never teach a skill");
        lost.Hp = 0;
        for (int i = 0; i < 10; i++) shrine.Tick(.05f);
        Require(!lost.Alive, "a priest who never learned 소생 cannot revive");
        Require(elder.LearnSkill(resurrection), "a skill is learned from somewhere");
        elder.Mp = elder.Stats.Total(StatType.MaxMp);
        for (int i = 0; i < 40 && !lost.Alive; i++) shrine.Tick(.05f);
        Require(lost.Alive, "소생 brings the fallen back");

        // Mage support: an enchant or a buff lands on the allies in a fight.
        var warrior = AssetDatabase.LoadAssetAtPath<SoulMonsterData>(DataPath + "/MonsterGoblinWarrior.asset");
        var support = new SoulDungeonSession(dungeon, rules, new[] { seraData, ria }, 26);
        support.AutoExplore = false;
        support.Mercenaries[0].Position = support.Monsters[0].Position + Vector2.right * 3f;
        support.Mercenaries[1].Position = support.Monsters[0].Position + Vector2.right * 2f;
        bool enchanted = false;
        for (int i = 0; i < 100 && !enchanted; i++)
        {
            support.Tick(.05f);
            foreach (var m in support.Mercenaries) m.Hp = m.Stats.Total(StatType.MaxHp);
            enchanted = support.Mercenaries[1].ImbueRemaining > 0 || support.Mercenaries[1].TimedBuffs.Exists(b => b.Id == "haste");
        }
        Require(enchanted, "the mage enchants or hastens the allies");

        // The village loop.
        var campaign = new SoulCampaign(village, 5);
        Require(campaign.Roster.Count == SoulCampaign.StartingMercenaries && campaign.Parties.Count == SoulCampaign.StartingParties && campaign.Parties[0].Members.Count == 2 && campaign.PartySize == 3,
            "the company starts with two mercenaries, two parties, both in the first one");
        var starter = campaign.Active.Find(q => q.Kind == SoulQuestKind.Explore);
        Require(campaign.Active.Count == 1 && starter != null && starter.Title == "던전 탐사" && campaign.QuestReward(starter) == SoulCampaign.StarterReward && !starter.Done,
            "a new company has one notice taken: 던전 탐사 for 300 gold");
        // who comes to the guild: 희귀 from level 2, 특별 from level 3
        for (int guild = 1; guild <= 3; guild++)
        {
            campaign.Levels[SoulBuildingKind.Guild] = guild;
            var seen = new HashSet<SoulStyleRarity>();
            for (int day = 0; day < 40; day++)
            {
                campaign.RefreshOffers();
                foreach (var o in campaign.Offers) seen.Add(o.Rarity);
            }
            Require(seen.Contains(SoulStyleRarity.Rare) == (guild >= SoulCampaign.RareGuildLevel) && seen.Contains(SoulStyleRarity.Special) == (guild >= SoulCampaign.SpecialGuildLevel),
                $"guild {guild}: offers {string.Join(", ", seen)}");
        }
        campaign.Levels[SoulBuildingKind.Guild] = 1;
        campaign.RefreshOffers();
        campaign.SetAmount(eItem.Gold, 20000);
        Require(campaign.Upgrade(SoulBuildingKind.Church) && campaign.Upgrade(SoulBuildingKind.Training) && campaign.Level(SoulBuildingKind.Church) == 1, "build the church and the training ground");
        Require(campaign.Upgrade(SoulBuildingKind.Guild) && campaign.Level(SoulBuildingKind.Guild) == 2, "upgrade the guild");
        // one party grows to five first (guild 1–2), then more parties open with the guild's level
        Require(campaign.Parties.Count == 1 && campaign.BuyPerk(SoulPerk.PartySize) && campaign.BuyPerk(SoulPerk.PartySize) && campaign.PartySize == 5,
            "a full party of five by guild level 2");
        Require(campaign.BuyPerk(SoulPerk.PartyCount) && campaign.Parties.Count == 2 && campaign.Parties[1].Number == 2 && campaign.PerkBlock(SoulPerk.PartyCount) != null,
            "a second party at guild level 2; the third wants guild level 3");
        Require(campaign.RosterLimit >= campaign.PartyLimit * campaign.PartySize, "the roster holds the parties");
        int roster = campaign.Roster.Count;
        Require(campaign.Hire(campaign.Offers[0]) && campaign.Roster.Count == roster + 1, "hire at the guild");
        // names: nobody in the company and nobody on the list shares one, day after day
        for (int day = 0; day < 6; day++)
        {
            campaign.RefreshOffers();
            var names = new HashSet<string>();
            bool unique = true;
            foreach (var h in campaign.Roster) unique &= names.Add(h.Name);
            foreach (var o in campaign.Offers) unique &= names.Add(o.Name);
            Require(unique, $"no two mercenaries or offers share a name ({string.Join(", ", names)})");
        }
        var newcomer = campaign.Roster[campaign.Roster.Count - 1];
        Require(newcomer.Recruit != null && newcomer.Recruit.Job == newcomer.Job && newcomer.Style == newcomer.Recruit.Id, "a hire is someone on the roster");
        ValidateHireStyles(village, rules);
        ValidateJobSkills(village, dungeon, rules);
        ValidateSoulSkills(village, dungeon, rules);
        ValidateRosterTraits(village, dungeon, rules);
        var first = campaign.Roster[0];
        Require(campaign.Upgrade(SoulBuildingKind.Shop), "upgrade the shop");
        var gear = campaign.Stock().Find(item => item.Id == "stamina_charm");
        Require(gear != null && campaign.Buy(gear) && campaign.Inventory.Contains(gear), "an upgraded shop stocks the stamina charm");
        float stamina = first.Stats.Total(StatType.MaxStamina);
        Require(campaign.Equip(first, gear) && first.Equipment.Contains(gear) && first.Stats.Total(StatType.MaxStamina) > stamina, "equip bought gear");
        stamina = first.Stats.Total(StatType.MaxStamina);
        ValidateEquipmentRules(campaign, first);
        first.AddWounds(3);
        Require(campaign.HealAtChurch(first) && first.Wounds == 0, "the church heals every wound");
        // all at once: everyone at home with a wound, for the sum of their costs
        foreach (var h in campaign.Roster) h.AddWounds(2);
        int allCost = campaign.HealAllCost(), goldBefore = campaign.Gold;
        Require(allCost > 0 && campaign.HealAllAtChurch() && campaign.Roster.TrueForAll(h => h.Wounds == 0) && campaign.Gold == goldBefore - allCost,
            "the church heals everyone's wounds at once");
        // the dungeon's game speed goes round 1 → 2 → 4 → 8 → 1
        SoulExpeditionRunner.GameSpeed = 1;
        var speeds = new List<float>();
        for (int i = 0; i < 4; i++) { SoulExpeditionRunner.NextGameSpeed(); speeds.Add(SoulExpeditionRunner.GameSpeed); }
        Require(speeds[0] == 2 && speeds[1] == 4 && speeds[2] == 8 && speeds[3] == 1, "game speed x1 / x2 / x4 / x8");
        Require(campaign.Train(first, SoulCampaign.Trainings[0], out _) && campaign.IsTraining(first) && first.Stats.Total(StatType.MaxStamina) < stamina + 9.99f
            && campaign.TrainBlock(first, SoulCampaign.Trainings[1]) != null, "training takes time");
        campaign.Advance(SoulCampaign.Trainings[0].Minutes + 1);
        Require(!campaign.IsTraining(first) && first.Stats.Total(StatType.MaxStamina) >= stamina + 9.99f, "stamina training");
        int learned = first.LearnedPatterns.Count;
        var learn = System.Array.Find(SoulCampaign.Trainings, t => t.LearnPattern);
        Require(campaign.Train(first, learn, out string lesson) && first.LearnedPatterns.Count == learned, "pattern training: " + lesson);
        campaign.Advance(learn.Minutes + 1);
        var offer = campaign.PatternOffer(first);
        Require(offer != null && offer.Count > 0 && offer.Count <= 3 && campaign.TrainBlock(first, learn) != null, "pattern training offers up to three");
        Require(campaign.ChoosePattern(first, offer[0]) && first.LearnedPatterns.Count == learned + 1 && campaign.PatternOffer(first) == null, "one of them is learned");
        // 자동 훈련: a mercenary at home trains by itself, a stat of its tendencies, and the choice is saved
        {
            var drilling = new SoulCampaign(village, 88);
            drilling.Levels[SoulBuildingKind.Training] = 1;
            drilling.SetAmount(eItem.Gold, 5000);
            drilling.Clock = (SoulClock.Day(drilling.Clock) - 1) * SoulClock.DayMinutes + 8 * 60;
            var trainee = drilling.Roster.Find(h => drilling.PartyOf(h) != null);
            drilling.ToggleAutoTrain(trainee);
            drilling.Advance(1);
            var drill = drilling.DrillOf(trainee);
            Require(drill != null && !drill.Training.LearnPattern && SoulCampaign.StatWorth(trainee, drill.Training.Stat) >= .5f, "auto training starts a drill that suits it");
            var reloaded = SoulSave.Read(SoulSave.Write(drilling), village);
            Require(reloaded.AutoTrain.Contains(trainee.Id), "auto training is saved");
            // a mercenary in training can still go down: the drill pauses and goes on once it is back
            var home = drilling.PartyOf(trainee);
            float left = drill.Left(drilling.Clock);
            Require(drilling.DepartBlock(home) == null, "training does not keep a party home");
            var away = drilling.Depart(home, 5);
            Require(away != null && drill.IsPaused && Mathf.Abs(drill.Paused - left) < .01f, "the drill pauses while it is away");
            var pausedSave = SoulSave.Read(SoulSave.Write(drilling), village);
            Require(pausedSave.Drills.TryGetValue(trainee.Id, out var kept) && kept.IsPaused, "a paused drill is saved as paused");
        }
        // the cap: a drill so often and no more; the guild's 훈련 한계 raises it; each time dearer and longer
        {
            var capped = new SoulCampaign(village, 89);
            capped.Levels[SoulBuildingKind.Training] = 1;
            capped.SetAmount(eItem.Gold, 1000000);
            var lifter = capped.Roster[0];
            var strength = System.Array.Find(SoulCampaign.Trainings, t => t.Id == "strength");
            int firstCost = capped.TrainingCost(lifter, strength); float firstTime = capped.TrainingMinutes(lifter, strength);
            for (int i = 0; i < SoulCampaign.BaseTrainingCap; i++) { Require(capped.Train(lifter, strength, out string why), "train up to the cap: " + why); capped.Advance(capped.TrainingMinutes(lifter, strength) * 2 + 1); }
            Require(capped.TrainingCost(lifter, strength) > firstCost * 5 && capped.TrainingMinutes(lifter, strength) > firstTime * 2, "each drill dearer and longer than the last");
            Require(capped.TrainBlock(lifter, strength) != null, "no more past the cap");
            capped.Renown = 1000;
            Require(capped.BuyPerk(SoulPerk.TrainingCap) && capped.TrainBlock(lifter, strength) == null, "the guild's 훈련 한계 lifts it");
        }
        // the roster fits every party the guild can field
        {
            var roomy = new SoulCampaign(village, 90);
            roomy.Levels[SoulBuildingKind.Guild] = 3;
            roomy.Renown = 100000;
            roomy.SetAmount(eItem.Gold, 1000000);
            while (roomy.BuyPerk(SoulPerk.PartyCount)) { }
            while (roomy.BuyPerk(SoulPerk.PartySize)) { }
            Require(roomy.RosterLimit >= roomy.PartyLimit * roomy.PartySize, $"the roster holds every party ({roomy.RosterLimit} for {roomy.PartyLimit}×{roomy.PartySize})");
        }
        Require(campaign.Bless(SoulCampaign.Blessings[0]), "a blessing at the church");

        var quest = campaign.Board.Find(q => q.Kind == SoulQuestKind.Hunt) ?? campaign.Board[0];
        Require(!campaign.Accept(quest), "the starter notice holds the small board's only slot");
        campaign.Levels[SoulBuildingKind.Board] = 2;
        Require(campaign.Accept(quest), "accept a quest");
        int gold = campaign.Gold;
        var trip = campaign.Depart(campaign.Parties[0], 23, dungeon);
        Require(trip != null && campaign.InExpedition && trip.Party[0].Buffs.ContainsKey(SoulCampaign.BlessingKey) && !campaign.Roster.Contains(trip.Party[0]),
            "the blessing goes down with the party, which leaves the roster while away");
        var run = trip.Session;
        run.Gold = 123;
        run.DefeatedCounts[quest.TargetId ?? "x"] = quest.Goal;
        run.Mercenaries[1].Hp = 0;
        var report = campaign.Return(trip);
        Debug.Log("VILLAGE return: " + string.Join(" / ", report));
        Require(!campaign.InExpedition && (campaign.Gold == gold + 123 || campaign.Event == SoulVillageEvent.Thief) && !campaign.Parties[0].Members[0].Buffs.ContainsKey(SoulCampaign.BlessingKey), "back in the village with the gold, the blessing spent");
        var carried = campaign.Roster.Find(h => h.Name == run.Mercenaries[1].Name);
        Require(carried != null && carried.Alive && carried.Wounds == SoulCampaign.FallenWounds && campaign.Fallen.Count == 0
            && campaign.Records.Find(r => r.Id == carried.Id)?.Status != SoulRecordStatus.Fallen,
            $"보통: the fallen is carried home with {SoulCampaign.FallenWounds} wounds ({carried?.Wounds})");
        // 최상: the dungeon kills for good; the choice survives a save
        {
            var hardcore = new SoulCampaign(village, 92) { Difficulty = SoulDifficulty.Extreme };
            Require(hardcore.HasBuilding(SoulBuildingKind.Memorial) && !campaign.HasBuilding(SoulBuildingKind.Memorial) && campaign.HasBuilding(SoulBuildingKind.Guild),
                "the 추모비 stands only at 최상");
            Require(SoulSave.Read(SoulSave.Write(hardcore), village).Difficulty == SoulDifficulty.Extreme, "the difficulty is saved");
            var deepTrip = hardcore.Depart(hardcore.Parties[0], 24, dungeon);
            Require(deepTrip != null, "최상: a party goes down");
            string gone = deepTrip.Session.Mercenaries[0].Name;
            deepTrip.Session.Mercenaries[0].Hp = 0;
            hardcore.Return(deepTrip);
            Require(!hardcore.Roster.Exists(h => h.Name == gone) && hardcore.Fallen.Count == 1, "최상: the fallen are dead and gone");
        }
        if (quest.Kind == SoulQuestKind.Hunt) Require(quest.Done && campaign.Claim(quest), "a finished quest pays at the board");
        int purse = campaign.Gold;
        Require(starter.Done && campaign.Claim(starter) && campaign.Gold == purse + SoulCampaign.StarterReward, "back from the dungeon: 던전 탐사 pays 300 gold");
        campaign.Levels[SoulBuildingKind.Board] = 1;
        ValidateBoard(campaign);
        ValidateVillageSystems(village, dungeon, rules);
        ValidateExpeditions(village, dungeon, rules);
    }

    // The village clock and the portal window, parties running in the background, the party's own decisions.
    static void ValidateExpeditions(SoulVillageData village, SoulDungeonData dungeon, SoulStatRules rules)
    {
        var campaign = new SoulCampaign(village, 11);
        Require(campaign.Clock == SoulClock.Start && campaign.CanDepart && campaign.Parties.Count == 1, "the portal is open at any hour; the company starts with one party");
        campaign.Perks[SoulPerk.PartyCount] = 1; campaign.SyncParties(); // a second party (the guild's 파티 편성)
        var stay = campaign.Parties[0].Members[campaign.Parties[0].Members.Count - 1];
        Require(campaign.Assign(stay, campaign.Parties[1]) && campaign.PartyOf(stay) == campaign.Parties[1], "a mercenary moves to the second party");
        var first = campaign.Depart(campaign.Parties[0], 41, dungeon);
        Require(first != null && Mathf.Approximately(first.ReturnAt, SoulClock.Start + SoulClock.StayMinutes)
            && Mathf.Abs(SoulClock.DungeonHoursLeft(campaign.Clock, first.ReturnAt) - 48) < .01f, "six hours up here: 48 dungeon hours");
        Require(campaign.IsAway(campaign.Parties[0]) && campaign.DepartBlock(campaign.Parties[0]) != null && campaign.HeadCount == campaign.Roster.Count + first.Party.Count,
            "the party is away");
        Require(campaign.FindHero(first.Party[0].Id) == first.Party[0] && !campaign.Roster.Contains(first.Party[0]), "a mercenary away is still found (reports, the memorial draw it)");
        campaign.Advance(30);
        var late = campaign.Depart(campaign.Parties[1], 42, dungeon);
        Require(late != null && late.Number == 2 && Mathf.Approximately(late.ReturnAt, SoulClock.Start + 30 + SoulClock.StayMinutes), "a second party half an hour later");
        var saved = SoulSave.Read(SoulSave.Write(campaign), village);
        Require(saved.Roster.Count == campaign.HeadCount && Mathf.Approximately(saved.Clock, campaign.Clock) && saved.Away.Count == 0,
            "a save with parties away keeps them on the roster, and the clock");
        // Unwatched, both run at 8× the village until 19:00 (or until they escape or fall).
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int ticks = 0;
        while (campaign.Away.Count > 0 && campaign.Clock < SoulClock.Start + 30 + SoulClock.StayMinutes + 1)
        {
            foreach (var trip in campaign.Away) ticks += Mathf.RoundToInt(.5f * SoulClock.DungeonRate / SoulCampaign.Step);
            campaign.Advance(.5f, 100000);
        }
        Debug.Log($"EXPEDITIONS background run: {first.Session.Floor}층 {first.Status} / {late.Session.Floor}층 {late.Status}, ~{ticks} ticks in {watch.ElapsedMilliseconds}ms");
        Require(campaign.Away.Count == 0 && first.Settled && late.Settled && campaign.Expeditions == 2, "every party is back six hours later");
        Require(!campaign.Parties[0].Members.Exists(h => campaign.Roster.Contains(h)) || campaign.DepartBlock(campaign.Parties[0]) != null, "once a day");
        Require(first.Session.DungeonMinutes > 20, $"the party lived through dungeon time ({first.Session.ClockText})"); // (a lone one goes home at its first wound)
        foreach (var hero in first.Party) Require(campaign.Roster.Contains(hero) != (!hero.Alive || !first.Session.Mercenaries.Contains(hero)), "the living are home, the fallen gone");
        // A new day: the guild, the shop and the merchant turn over.
        int day = SoulClock.Day(campaign.Clock);
        campaign.Advance(SoulClock.DayMinutes - SoulClock.TimeOfDay(campaign.Clock) + 1);
        Require(SoulClock.Day(campaign.Clock) == day + 1 && campaign.Log.Exists(line => line.Contains("일차가 밝았습니다")), "a new day in the village");
        // Late in the evening: cut short at midnight, and everyone is out when the day turns.
        var evening = campaign.Parties.Find(p => p.Members.Count > 0 && campaign.DepartBlock(p) == null);
        if (evening != null)
        {
            campaign.Clock = (SoulClock.Day(campaign.Clock) - 1) * SoulClock.DayMinutes + 22 * 60;
            var night = campaign.Depart(evening, 43, dungeon);
            Require(night != null && Mathf.Abs(SoulClock.DungeonHoursLeft(campaign.Clock, night.ReturnAt) - 16) < .01f, "going in at 22:00 leaves 16 dungeon hours");
            campaign.Advance(121, 100000);
            Require(night.Settled && campaign.Away.Count == 0 && !campaign.EnteredToday(night.Party[0]), "midnight: everyone out, a new day to go again");
        }

        // Too wounded to fight (rest does not heal wounds): the party leaves through the portal at the entrance.
        var hurt = new SoulDungeonSession(dungeon, rules, village.StartingRoster, 43) { Unattended = true };
        foreach (var m in hurt.Monsters) m.Position = new Vector2(-500, -500);
        foreach (var hero in hurt.Mercenaries) hero.AddWounds(SoulMercenary.MaxWounds);
        int scrolls = SoulDungeonSession.Count(hurt.Pouch, SoulSupplies.ReturnScroll);
        for (int i = 0; i < 2400 && !hurt.Recalled; i++) hurt.Tick(.05f);
        Require(hurt.Recalled && hurt.Plan == SoulPartyPlan.Retreat && SoulDungeonSession.Count(hurt.Pouch, SoulSupplies.ReturnScroll) == scrolls,
            "too wounded and no scroll: the party walks out through the entrance portal");
        // Wounds lock max HP: 10% each, back when healed.
        var locked = hurt.Mercenaries[0];
        locked.HealWounds(SoulMercenary.MaxWounds);
        float fullHp = locked.Stats.Total(StatType.MaxHp);
        locked.AddWounds(2);
        Require(Mathf.Abs(locked.Stats.Total(StatType.MaxHp) - fullHp * .8f) < .01f && Mathf.Abs(locked.FullMaxHp - fullHp) < .01f && locked.Hp <= fullHp * .8f + .01f,
            "two wounds lock 20% of max HP");
        locked.HealWounds(2);
        Require(Mathf.Abs(locked.Stats.Total(StatType.MaxHp) - fullHp) < .01f && locked.LockedHp == 0, "healed wounds unlock it");
        // The player's 탈출: the party heads out (even without automatic control) and can be called back.
        var runaway = new SoulDungeonSession(dungeon, rules, village.StartingRoster, 46) { AutoExplore = false };
        foreach (var m in runaway.Monsters) m.Position = new Vector2(-500, -500);
        var start = runaway.Map.Center(runaway.PartyStart);
        // somewhere open and walkable some way from the entrance (floor 1 has a way out on each side of it)
        var away = start;
        for (int r = 8; r < 24 && away == start; r++)
            foreach (var dir in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                var cell = runaway.PartyStart + dir * r;
                if (away == start && runaway.Map.Open(cell) && runaway.Map.Clear(runaway.Map.Center(cell), .5f) && runaway.Map.Reachable(start, runaway.Map.Center(cell), .45f)) away = runaway.Map.Center(cell);
            }
        for (int i = 0; i < runaway.Mercenaries.Count; i++) runaway.Mercenaries[i].Position = away + new Vector2(.3f * i, 0);
        runaway.OrderEscape();
        for (int i = 0; i < 20; i++) runaway.Tick(.05f);
        Require(runaway.Plan == SoulPartyPlan.Retreat && runaway.Mercenaries.Exists(h => runaway.HasMoveOrder(h)), "탈출: the party heads for the way out");
        runaway.CancelRetreat();
        Require(runaway.Plan == SoulPartyPlan.Explore && !runaway.EscapeOrdered && !runaway.Mercenaries.Exists(h => runaway.HasMoveOrder(h)), "탈출 called back: exploring again");
        // together: a slow one sets the pace, nobody runs off ahead
        runaway.OrderEscape();
        var slowpoke = runaway.Mercenaries[0];
        slowpoke.AddTimedBuff("test_slow", new[] { new SoulStatBonus { Stat = StatType.MoveSpeed, Value = -slowpoke.Stats.Total(StatType.MoveSpeed) * .6f } }, 999);
        float spread = 0;
        for (int i = 0; i < 160; i++)
        {
            runaway.Tick(.05f);
            foreach (var h in runaway.Mercenaries) spread = Mathf.Max(spread, Vector2.Distance(h.Position, slowpoke.Position));
        }
        Require(spread <= SoulDungeonSession.GroupGap + 2.5f, $"the party keeps together on the way out (spread {spread:0.0})");
        // worn: someone 피곤 (fatigue tier 1) — the camp kit comes out at once
        var weary = new SoulDungeonSession(dungeon, rules, village.StartingRoster, 47) { Unattended = true };
        foreach (var m in weary.Monsters) m.Position = new Vector2(-500, -500);
        weary.Pouch[SoulSupplies.CampKit] = 1;
        SoulFatigue.Add(weary.Mercenaries[0], SoulFatigue.TierFrom[1] + 2);
        for (int i = 0; i < 40 && weary.Camping <= 0; i++) weary.Tick(.05f);
        Require(weary.Camping > 0, "a tired party camps before going on");
        // no camp kit: 피곤 is walked on; 지침 makes it rest where it stands — back under 지침, not until 피곤 is gone
        var drowsy = new SoulDungeonSession(dungeon, rules, village.StartingRoster, 48) { Unattended = true };
        foreach (var m in drowsy.Monsters) m.Position = new Vector2(-500, -500);
        drowsy.Pouch.Remove(SoulSupplies.CampKit);
        SoulFatigue.Add(drowsy.Mercenaries[0], SoulFatigue.TierFrom[1] + 1 - drowsy.Mercenaries[0].Fatigue);
        for (int i = 0; i < 40; i++) drowsy.Tick(.05f);
        Require(drowsy.Plan != SoulPartyPlan.Recover, $"피곤 without a camp kit: it goes on ({drowsy.Plan})");
        SoulFatigue.Add(drowsy.Mercenaries[0], SoulFatigue.TierFrom[2] + 1 - drowsy.Mercenaries[0].Fatigue);
        for (int i = 0; i < 40 && drowsy.Plan != SoulPartyPlan.Recover; i++) drowsy.Tick(.05f);
        Require(drowsy.Plan == SoulPartyPlan.Recover, $"지침 without a camp kit: it rests where it stands ({drowsy.Plan})");
        for (int i = 0; i < 4000 && drowsy.Plan == SoulPartyPlan.Recover; i++) drowsy.Tick(.1f);
        float restedAt = drowsy.Mercenaries[0].Fatigue;
        Require(drowsy.Plan != SoulPartyPlan.Recover && restedAt <= SoulDungeonSession.RestedFatigue + .1f && SoulFatigue.Tier(restedAt) == 1,
            $"it rests back under 지침 and goes on still 피곤 ({restedAt:0.0})");

        // the watchtower shows what it overlooks in the dungeon view, not only on the minimap
        {
            var lookout = new SoulDungeonSession(village.Dungeon, rules, village.StartingRoster, 49) { AutoExplore = false };
            SoulInteractable tower = null;
            foreach (var thing in lookout.Interactables) if (thing.Kind == SoulObjectKind.Watchtower) tower = thing;
            Require(tower != null, "floor 1 has a watchtower");
            var far = lookout.Map.Cell(tower.Position) + new Vector2Int(8, 0);
            lookout.Use(tower, lookout.Mercenaries[0]);
            int seen = 0;
            for (int y = -10; y <= 10; y++)
                for (int x = -10; x <= 10; x++)
                    if (lookout.IsSeen(lookout.Map.Cell(tower.Position) + new Vector2Int(x, y))) seen++;
            Require(seen > 300, $"the watchtower lights the dungeon view around it ({seen} tiles seen)");
        }

        // warp traps from floor 3: they throw the party into an elite, a horde or the boss; a thrown party with
        // someone near death stays where it is on its way out
        {
            var low = SoulDungeonGenerator.Generate(village.Dungeon, 51, 2);
            Require(!low.Traps.Exists(t => t.kind == SoulTrapKind.Warp), "no warp traps before floor 3");
            var company = new List<SoulMercenary>();
            foreach (var data in village.StartingRoster) company.Add(SoulDungeonSession.RecruitOne(data, rules, data.Id, data.DisplayName));
            var deep = new SoulDungeonSession(village.Dungeon, rules, company, 51, SoulDungeonGenerator.WarpTrapFloor) { Unattended = true, AutoExplore = false };
            var plate = deep.Traps.Find(t => t.Kind == SoulTrapKind.Warp);
            Require(plate != null && deep.Traps.FindAll(t => t.Kind == SoulTrapKind.Warp).Count >= 2, "floor 3 has warp traps");
            Require(Vector2.Distance(plate.Target, plate.Position) > 5, "a warp trap sends somewhere else");
            foreach (var m in deep.Monsters) m.Position = new Vector2(-500, -500);
            plate.Found = false; // (the pathfinder's sense may have marked it)
            deep.Mercenaries[0].Position = plate.Position;
            deep.Mercenaries[1].Position = plate.Position + Vector2.right * 1.5f;
            for (int i = 0; i < 4 && !deep.Warped; i++) deep.Tick(.05f);
            var thrown = deep.Mercenaries[0];
            Require(deep.Warped && Vector2.Distance(thrown.Position, plate.Target) < 3, $"the warp trap throws the party ({Vector2.Distance(thrown.Position, plate.Target):0.0} from its target)");
            thrown.Hp = thrown.Stats.Total(StatType.MaxHp) * .15f;
            deep.BeginRetreat("test");
            Vector2 there = thrown.Position;
            for (int i = 0; i < 40; i++) deep.Tick(.05f);
            Require(deep.Hiding && Vector2.Distance(thrown.Position, there) < 1, $"thrown and near death: it stays where it is ({deep.Hiding}, moved {Vector2.Distance(thrown.Position, there):0.0})");
        }

        // Wounds within reason: it goes on.
        var scratched = new SoulDungeonSession(dungeon, rules, village.StartingRoster, 44) { Unattended = true };
        foreach (var hero in scratched.Mercenaries) hero.AddWounds(2);
        Require(scratched.PartyCanFight(), "a few wounds do not stop the party");
        scratched.Mercenaries[0].AddWounds(1);
        scratched.Mercenaries[1].AddWounds(1);
        scratched.Mercenaries[2].AddWounds(1);
        Require(!scratched.PartyCanFight(), "three of five with three wounds: fighting is hard");
        var torn = scratched.Mercenaries[3];
        torn.AddWounds(SoulMercenary.MaxWounds);
        torn.Hp = torn.Stats.Total(StatType.MaxHp) * .5f;
        float halfHp = torn.Hp;
        for (int i = 0; i < 40; i++) SoulCombat.TickResources(torn, .05f);
        Require(torn.Hp < halfHp && torn.Hp >= 1, "the fifth wound bleeds (never below 1 HP)");
        // 탈진: straight home. 지침: it rests where it stands (slowly) until rested, then explores on.
        var spent = new SoulDungeonSession(dungeon, rules, village.StartingRoster, 45) { Unattended = true };
        foreach (var m in spent.Monsters) m.Position = new Vector2(-500, -500);
        SoulFatigue.Add(spent.Mercenaries[0], 95);
        for (int i = 0; i < 40; i++) spent.Tick(.05f);
        Require(spent.Plan == SoulPartyPlan.Retreat, "탈진: the party goes home");
        var tired = new SoulDungeonSession(dungeon, rules, village.StartingRoster, 45) { Unattended = true };
        foreach (var m in tired.Monsters) m.Position = new Vector2(-500, -500);
        foreach (var hero in tired.Mercenaries) SoulFatigue.Add(hero, 75);
        for (int i = 0; i < 40; i++) tired.Tick(.05f);
        Require(tired.Plan == SoulPartyPlan.Recover && !tired.Recalled, "지침: the party rests where it stands");
        for (int i = 0; i < 8000 && tired.Plan == SoulPartyPlan.Recover; i++) tired.Tick(.05f);
        Require(tired.Plan == SoulPartyPlan.Explore && tired.Mercenaries.TrueForAll(h => h.Fatigue <= SoulDungeonSession.RestedFatigue + .1f), $"rested (back under 지침), it explores on ({tired.ClockText})");
        // slept on the floor MaxFloorSleeps times already: worn out again, it goes home
        {
            var sleepy = new SoulDungeonSession(dungeon, rules, village.StartingRoster, 46) { Unattended = true };
            foreach (var m in sleepy.Monsters) m.Position = new Vector2(-500, -500);
            sleepy.FloorSleeps = SoulDungeonSession.MaxFloorSleeps;
            foreach (var hero in sleepy.Mercenaries) SoulFatigue.Add(hero, 75);
            for (int i = 0; i < 40; i++) sleepy.Tick(.05f);
            Require(sleepy.Plan == SoulPartyPlan.Retreat, "after sleeping on the floor too often, the party goes home");
        }
        ValidatePartyOrders(village, dungeon, rules);
    }

    // Party orders and what a party does with its finds: auto-buy, the target floor, camp and mending, spoils.
    static void ValidatePartyOrders(SoulVillageData village, SoulDungeonData dungeon, SoulStatRules rules)
    {
        var shopper = new SoulCampaign(village, 12);
        var squad = shopper.Parties[0];
        shopper.SetCarry(squad, new SoulCarry[0]); // the starting provisions back on the shelf
        shopper.SetAmount(eItem.PotionHeal, 1);
        shopper.SetAmount(eItem.SoulStone, 4);
        shopper.SetCarry(squad, new[] { new SoulCarry { Id = SoulSupplies.HealPotion, Count = 5 }, new SoulCarry { Id = SoulSupplies.SoulStone, Count = 5 } });
        Require(shopper.Carried(squad, SoulSupplies.HealPotion) == 1 && shopper.Supply(SoulSupplies.HealPotion) == 0 && shopper.Carried(squad, SoulSupplies.SoulStone) == 3 && shopper.Supply(SoulSupplies.SoulStone) == 1,
            "provisions leave the store the moment they are given (what there is, 3 of a kind at most)");
        Require(shopper.SetCarryCount(squad, SoulSupplies.SoulStone, 2) && shopper.Supply(SoulSupplies.SoulStone) == 2
            && shopper.SetCarryCount(squad, SoulSupplies.SoulStone, 5) && shopper.Supply(SoulSupplies.SoulStone) == 1, "taking some back puts them on the shelf again");
        shopper.SetTarget(squad, 2, SoulExploreMode.Farm);
        int gold = shopper.Gold;
        var bought = shopper.Depart(squad, 51, dungeon);
        Require(bought != null && bought.Session.Potions == 1 && bought.Session.PreservationItems >= 3 && shopper.Supply(SoulSupplies.HealPotion) == 0
            && !bought.Session.Pouch.ContainsKey(SoulSupplies.SoulStone) && shopper.Gold == gold && squad.Carry.Count == 0, $"provisions go down as they were given (nothing bought): 1 potion, 3 soul stones ({bought?.Session.PreservationItems})");
        Require(bought.Session.TargetFloor == 2 && bought.Session.TargetMode == SoulExploreMode.Farm, "the party's order goes down with it");

        // At the target floor: hunt there, never through a portal.
        var aim = new SoulDungeonSession(dungeon, rules, village.StartingRoster, 52) { TargetFloor = 1, TargetMode = SoulExploreMode.Hunt, Unattended = true };
        Require(aim.AtTarget && aim.EffectiveMode == SoulExploreMode.Hunt, "at the target floor the party hunts");
        foreach (var m in aim.Monsters) m.Hp = 0;
        for (int i = 0; i < 1200 && !aim.Finished; i++) aim.Tick(.05f);
        Require(aim.ExitOpen && !aim.Finished, "and never leaves through a portal there");

        // A finished camp mends a wound; the mending scroll one on everyone.
        var camp = new SoulDungeonSession(dungeon, rules, village.StartingRoster, 53) { AutoExplore = false };
        foreach (var m in camp.Monsters) m.Position = new Vector2(-500, -500);
        camp.Pouch[SoulSupplies.CampKit] = 1;
        camp.Mercenaries[0].AddWounds(2);
        Require(camp.UseSupply(SoulSupplies.CampKit) && camp.CampFire, "camp out of combat, around a fire");
        for (int i = 0; i < 120; i++) camp.Tick(.05f);
        Require(camp.Mercenaries[0].Wounds == 1, $"a finished camp mends a wound ({camp.Mercenaries[0].Wounds})");
        camp.Pouch[SoulSupplies.MendScroll] = 1;
        Require(camp.UseSupply(SoulSupplies.MendScroll) && camp.Mercenaries[0].Wounds == 0, "the mending scroll heals a wound");
        // Too wounded, with a mending scroll: read it before giving up.
        var mend = new SoulDungeonSession(dungeon, rules, village.StartingRoster, 54) { Unattended = true };
        foreach (var m in mend.Monsters) m.Position = new Vector2(-500, -500);
        foreach (var hero in mend.Mercenaries) hero.AddWounds(3);
        mend.Pouch[SoulSupplies.MendScroll] = 1;
        mend.Belt[SoulSupplies.HealPotion] = mend.Mercenaries.Count; // a potion a head: only the scroll decides it (none would send it home sooner)
        for (int i = 0; i < 100; i++) mend.Tick(.05f);
        Require(SoulDungeonSession.Count(mend.Pouch, SoulSupplies.MendScroll) == 0 && mend.Plan == SoulPartyPlan.Explore && mend.PartyCanFight(), "the mending scroll keeps the party going");

        // Survival: the starting company sent down the real dungeon for a whole stay comes back alive.
        int wipes = 0;
        for (int run = 0; run < 3; run++)
        {
            var company = new SoulCampaign(village, 70 + run);
            var sent = company.Depart(company.Parties[0], 700 + run * 13);
            var clockWatch = System.Diagnostics.Stopwatch.StartNew();
            while (company.Away.Count > 0) company.Advance(.5f, 100000);
            var trip = sent.Session;
            Debug.Log($"SURVIVAL run {run}: {trip.Floor}층 {sent.Status} · 생존 {sent.Alive}/{sent.Party.Count} · 던전 {trip.ClockText} · {clockWatch.ElapsedMilliseconds}ms · {string.Join(" / ", trip.Events.GetRange(0, Mathf.Min(20, trip.Events.Count)))}");
            if (trip.Defeated) wipes++;
        }
        // A three-strong starting party does not always make it (no free way home without a return scroll).
        Require(wipes < 3, $"sent down with automatic control, the company usually comes home ({wipes}/3 wiped)");

        // One day's dungeon: two parties on the same floor, one monster, one reward (to whoever killed it).
        var today = new SoulDungeonDay();
        var partyA = new SoulDungeonSession(dungeon, rules, SoulDungeonSession.Recruit(village.StartingRoster, rules), 61, 1, 0, today) { AutoExplore = false };
        var partyB = new SoulDungeonSession(dungeon, rules, SoulDungeonSession.Recruit(village.StartingRoster, rules), 62, 1, 0, today) { AutoExplore = false };
        Require(partyA.Monsters == partyB.Monsters && partyA.Map == partyB.Map && partyA.FloorParties.Count == 2, "parties going down the same day share the floor");
        var prey = partyA.Monsters.Find(m => m.Alive);
        prey.Hp = 0;
        partyA.Tick(.05f); partyB.Tick(.05f); partyA.Tick(.05f); partyB.Tick(.05f);
        int rewards = 0;
        foreach (var n in partyA.DefeatedCounts.Values) rewards += n;
        foreach (var n in partyB.DefeatedCounts.Values) rewards += n;
        Require(rewards == 1, $"a monster both parties can see is rewarded once ({rewards})");
        var visitor = partyB.Mercenaries[0];
        Require(partyA.Shared.Heroes.Contains(visitor), "the floor knows every party's heroes (monsters go for any of them)");

        // Spoils: a soul to whoever has room, the rest into a soul stone; an upgrade on whoever wears that kind.
        var spoils = new SoulDungeonSession(dungeon, rules, village.StartingRoster, 55, 1) { Unattended = true };
        foreach (var m in spoils.Monsters) m.Position = new Vector2(-500, -500);
        var soul = System.Array.Find(village.AllSouls, s => s.Id == "boar"); // one the swordsman and the guardian need
        if (soul != null)
        {
            foreach (var hero in spoils.Mercenaries) while (hero.HasFreeSoulSlot && hero.Absorb(soul, rules)) { }
            var kept = new SoulDrop(soul);
            spoils.Stash.Add(kept);
            for (int i = 0; i < 20; i++) spoils.Tick(.05f);
            Require(kept.Preserved && spoils.PreservationItems == 0, "no room left: the soul goes into the soul stone");
        }
        // souls go to whom they suit best: a boar's charge or a wolf's flanking suits a swordsman more than a mage
        var mage = SoulDungeonSession.RecruitOne(System.Array.Find(village.StartingRoster, d => d.Job == "마법사"), rules);
        var swordsman = SoulDungeonSession.RecruitOne(System.Array.Find(village.StartingRoster, d => d.Job == "검사"), rules);
        var boarSoul = System.Array.Find(village.AllSouls, s => s.Id == "boar");
        var wolfSoul = System.Array.Find(village.AllSouls, s => s.Id == "wolf");
        // (a soul is its stats first: the wolf's agility goes to one who grows agility, not to a swordsman who does not)
        bool swordsmanAgile = System.Array.Exists(swordsman.GrowthWeights, g => g.Stat == StatType.Agility && g.Value > 0) || System.Array.Exists(swordsman.Race.GrowthWeights, g => g.Stat == StatType.Agility && g.Value > 0);
        Require(boarSoul != null && wolfSoul != null && SoulDungeonSession.SoulFit(swordsman, boarSoul) > SoulDungeonSession.SoulFit(mage, boarSoul)
            && (swordsmanAgile || SoulDungeonSession.SoulFit(swordsman, wolfSoul) < SoulDungeonSession.SoulNeed),
            $"souls suit their takers (boar: mage {SoulDungeonSession.SoulFit(mage, boarSoul):0.#} · swordsman {SoulDungeonSession.SoulFit(swordsman, boarSoul):0.#}; wolf: mage {SoulDungeonSession.SoulFit(mage, wolfSoul):0.#} · swordsman {SoulDungeonSession.SoulFit(swordsman, wolfSoul):0.#})");
        var picky = new SoulDungeonSession(dungeon, rules, new List<SoulMercenary> { mage }, 56, 1, 1) { Unattended = true };
        foreach (var m in picky.Monsters) m.Position = new Vector2(-500, -500);
        var boarDrop = new SoulDrop(boarSoul);
        picky.Stash.Add(boarDrop);
        for (int i = 0; i < 20; i++) picky.Tick(.05f);
        Require(!mage.Souls.Contains(boarSoul) && !boarDrop.Preserved && !picky.Stash.Contains(boarDrop), "a soul nobody needs is let go (a boar's for a lone mage)");
        // ordered to hunt on floor 2: on floor 1 it takes the portal it knows, the guardian alive or not
        {
            var climbers = new List<SoulMercenary>();
            foreach (var data in village.StartingRoster) if (climbers.Count < 2) climbers.Add(SoulDungeonSession.RecruitOne(data, rules));
            var climb = new SoulDungeonSession(village.Dungeon, rules, climbers, 57, 1, 1) { Unattended = true, TargetFloor = 2, TargetMode = SoulExploreMode.Hunt };
            climb.Perks.RevealExits = true;
            foreach (var m in climb.Monsters) m.Position = new Vector2(-500, -500);
            for (int i = 0; i < 6000 && !climb.Finished; i++) climb.Tick(.05f);
            Require(climb.Finished && climb.HasNextFloor && climb.BossAlive,
                $"on the way to its target floor the party goes up the first portal it knows (finished {climb.Finished} · open {climb.ExitOpen} · known {climb.ExitKnown} · guardian {climb.Monsters.Exists(m => m.Data.Guardian)} · plan {climb.Plan} · leader {climb.Leader?.Position} → exit {climb.Exit} · alive {climb.Living})");
        }
        // the luck of a goblin miner: the pathfinder grows in luck, the guardian does not
        var minerSoul = System.Array.Find(village.AllSouls, s => s.Id == "goblin_miner");
        var guide = SoulDungeonSession.RecruitOne(System.Array.Find(village.HireTemplates, d => d.Job == "길잡이"), rules);
        var guardian = SoulDungeonSession.RecruitOne(System.Array.Find(village.StartingRoster, d => d.Job == "수호자"), rules);
        // (its passive alone is not reason enough: the guardian and the summoner, no luck in them, let it go)
        var summoner = SoulDungeonSession.RecruitOne(System.Array.Find(village.HireTemplates, d => d.Job == "소환사") ?? System.Array.Find(village.StartingRoster, d => d.Job == "소환사"), rules);
        var wizard = SoulDungeonSession.RecruitOne(System.Array.Find(village.HireTemplates, d => d.Job == "마법사") ?? System.Array.Find(village.StartingRoster, d => d.Job == "마법사"), rules);
        Require(minerSoul != null && SoulDungeonSession.SoulFit(guide, minerSoul) > SoulDungeonSession.SoulFit(summoner, minerSoul)
            && SoulDungeonSession.SoulFit(summoner, minerSoul) < SoulDungeonSession.SoulNeed && SoulDungeonSession.SoulFit(wizard, minerSoul) < SoulDungeonSession.SoulNeed,
            $"a miner's soul (luck, a strength trap, gold) is not for the casters (guide {SoulDungeonSession.SoulFit(guide, minerSoul):0.#} / guardian {SoulDungeonSession.SoulFit(guardian, minerSoul):0.#} / summoner {SoulDungeonSession.SoulFit(summoner, minerSoul):0.#} / mage {SoulDungeonSession.SoulFit(wizard, minerSoul):0.#})");
        // a soul is its stats first: a summoner does not take a dark elf's soul (pierce and agility) for its hiding
        // skill and passive
        {
            var darkElfSoul = System.Array.Find(village.AllSouls, s => s.Id == "dark_elf");
            Require(darkElfSoul == null || SoulDungeonSession.SoulFit(summoner, darkElfSoul) < SoulDungeonSession.SoulNeed,
                $"a dark elf's soul is not for a summoner ({(darkElfSoul != null ? SoulDungeonSession.SoulFit(summoner, darkElfSoul) : 0):0.#})");
        }
        // soul stones: kept for the target floor; there the best souls go in, a better one taking the worst one's stone
        {
            var lowSoul = System.Array.Find(village.AllSouls, s => s.Id == "boar");
            var highSoul = System.Array.Find(village.AllSouls, s => s != null && s.Grade > lowSoul.Grade);
            var carriers = new List<SoulMercenary>();
            foreach (var data in village.StartingRoster) if (carriers.Count < 2) carriers.Add(SoulDungeonSession.RecruitOne(data, rules));
            foreach (var hero in carriers) while (hero.HasFreeSoulSlot && hero.Absorb(lowSoul, rules)) { } // no room for more
            var early = new SoulDungeonSession(village.Dungeon, rules, carriers, 58, 1, 1) { Unattended = true, TargetFloor = 2, TargetMode = SoulExploreMode.Hunt };
            foreach (var m in early.Monsters) m.Position = new Vector2(-500, -500);
            var passing = new SoulDrop(lowSoul);
            early.Stash.Add(passing);
            for (int i = 0; i < 20; i++) early.Tick(.05f);
            Require(!passing.Preserved && early.PreservationItems == 1, "no soul stone spent before the target floor");
            var there = new SoulDungeonSession(village.Dungeon, rules, carriers, 59, 2, 1) { Unattended = true, TargetFloor = 2, TargetMode = SoulExploreMode.Hunt };
            foreach (var m in there.Monsters) m.Position = new Vector2(-500, -500);
            var first = new SoulDrop(lowSoul);
            there.Stash.Add(first);
            for (int i = 0; i < 20; i++) there.Tick(.05f);
            Require(first.Preserved && there.PreservationItems == 0, "on the target floor a soul goes into the stone");
            if (highSoul != null)
            {
                var better = new SoulDrop(highSoul);
                there.Stash.Add(better);
                for (int i = 0; i < 20; i++) there.Tick(.05f);
                Require(better.Preserved && !first.Preserved, $"a better soul ({highSoul.OriginMonster}, grade {highSoul.Grade}) takes the stone of a worse one");
            }
        }
        Debug.Log($"SOULFIT miner: guide {SoulDungeonSession.SoulFit(guide, minerSoul):0.#} / guardian {SoulDungeonSession.SoulFit(guardian, minerSoul):0.#} / summoner {SoulDungeonSession.SoulFit(summoner, minerSoul):0.#} / mage {SoulDungeonSession.SoulFit(wizard, minerSoul):0.#}");
        // a passive is judged too: mana to one who casts nothing is nothing, gold alone is a side benefit
        {
            var meditationSoul = System.Array.Find(village.AllSouls, s => System.Array.Exists(s.Passives, p => p != null && System.Array.Exists(p.AlwaysBonuses, b => b.Stat == StatType.ManaRegenRate || b.Stat == StatType.MpRegen)));
            if (meditationSoul != null)
                Debug.Log($"SOULFIT {meditationSoul.Id}: guardian {SoulDungeonSession.SoulFit(guardian, meditationSoul):0.#} / mage {SoulDungeonSession.SoulFit(wizard, meditationSoul):0.#}");
        }
        // a second boar is worth less to the swordsman than the first (nothing new comes with it)
        float firstBoar = SoulDungeonSession.SoulFit(swordsman, boarSoul);
        swordsman.Absorb(boarSoul, rules);
        Require(SoulDungeonSession.SoulFit(swordsman, boarSoul) < firstBoar, "a second copy of a soul brings nothing new");
        mage.Absorb(boarSoul, rules); // given by hand: the charge to try out below
        // ...and uses it: spells, and the charge on whoever reaches it, turn about (the lock is there to stop it)
        var foe = picky.Monsters.Find(m => m.Alive);
        bool casts = false, charges = false;
        for (int i = 0; i < 800 && !(casts && charges) && mage.Alive; i++)
        {
            foe.Position = mage.Position + Vector2.right * 1.1f;
            foe.Hp = foe.Stats.Total(StatType.MaxHp);
            mage.Hp = mage.Stats.Total(StatType.MaxHp);
            picky.CombatEvents.Clear();
            picky.Tick(.05f);
            foreach (var evt in picky.CombatEvents)
            {
                if (evt.Actor != mage) continue;
                casts |= evt.Kind == SoulEventKind.Skill;
                charges |= evt.Kind == SoulEventKind.Attack && evt.Pattern != null && evt.Pattern.name == "Charge";
            }
        }
        Require(casts && charges, $"a mage with a charge casts and charges in turn (spell {casts}, charge {charges})");
        var wearer = spoils.Mercenaries.Find(h => h.Equipped(SoulEquipSlot.MainHand) != null && !h.Equipped(SoulEquipSlot.MainHand).Data.Unique && h.Equipped(SoulEquipSlot.MainHand).Grade < 5);
        if (wearer != null)
        {
            var weapon = wearer.Equipped(SoulEquipSlot.MainHand);
            var better = new SoulItem(weapon.Data, weapon.Grade + 1);
            var worse = new SoulItem(weapon.Data, 1);
            spoils.Inventory.Add(worse);
            spoils.Inventory.Add(better);
            for (int i = 0; i < 20; i++) spoils.Tick(.05f);
            Require(spoils.Mercenaries.Exists(h => h.Equipment.Contains(better)) && !spoils.Inventory.Contains(better) && spoils.Inventory.Contains(worse),
                "a better weapon of the same kind goes on someone; a worse one is carried home");
        }
    }

    // Supplies and fatigue in the dungeon; blacksmith, library, soul altar, merchant, renown, save and sparring.
    static void ValidateVillageSystems(SoulVillageData village, SoulDungeonData dungeon, SoulStatRules rules)
    {
        var campaign = new SoulCampaign(village, 9);
        campaign.SetAmount(eItem.Gold, 100000);
        campaign.SetCarry(campaign.Parties[0], new SoulCarry[0]); // the starting provisions back on the shelf

        // ── belt and pouch ──
        campaign.SetAmount(SoulSupplies.Get(SoulSupplies.HealPotion).Item, 3);
        campaign.SetAmount(SoulSupplies.Get("potion_stamina").Item, 2);
        campaign.SetAmount(SoulSupplies.Get("scroll_fury").Item, 2);
        campaign.SetAmount(SoulSupplies.Get(SoulSupplies.ReturnScroll).Item, 1);
        campaign.SetAmount(SoulSupplies.Get(SoulSupplies.CampKit).Item, 1);
        campaign.SetAmount(eItem.ScrollHeal, 0);
        campaign.SetAmount(eItem.SoulStone, 0);
        var squad = campaign.Parties[0];
        campaign.AutoFill(squad);
        Require(squad.Carry.Count > 0 && squad.Carry.Count <= campaign.CarryKinds && squad.Carry.TrueForAll(c => SoulSupplies.Packed(c.Id)), "auto-fill packs no enhancement scroll");
        Require(SoulSupplies.PlayerScroll("scroll_fury") && !SoulSupplies.PlayerScroll(SoulSupplies.ReturnScroll) && SoulSupplies.Packed(SoulSupplies.MendScroll), "the enhancement scrolls stay with whoever watches");
        campaign.SetCarry(squad, new[]
        {
            new SoulCarry { Id = SoulSupplies.HealPotion, Count = 3 }, new SoulCarry { Id = "scroll_fury", Count = 2 }, new SoulCarry { Id = SoulSupplies.ReturnScroll, Count = 1 },
            new SoulCarry { Id = "potion_stamina", Count = 2 }, new SoulCarry { Id = SoulSupplies.CampKit, Count = 1 },
            new SoulCarry { Id = "antidote", Count = 1 }, new SoulCarry { Id = "potion_mana", Count = 1 }, new SoulCarry { Id = "panacea", Count = 1 },
        });
        Require(squad.Carry.Count == campaign.CarryKinds && !squad.Carry.Exists(c => c.Id == "scroll_fury") && squad.Carry[0].Count == 3,
            $"provisions: at most {campaign.CarryKinds} kinds, never an enhancement scroll");
        var trip = campaign.Depart(squad, 31, dungeon);
        var run = trip.Session;
        run.AutoExplore = false;
        Require(campaign.Supply(SoulSupplies.HealPotion) == 0 && run.Potions == 3 && SoulDungeonSession.Count(run.Pouch, SoulSupplies.ReturnScroll) == 1
            && SoulDungeonSession.Count(run.Pouch, SoulSupplies.CampKit) == 0 && SoulDungeonSession.Count(run.Pouch, "scroll_fury") == 0 && campaign.Supply("scroll_fury") == 2,
            "the expedition takes its provisions — two kinds (the enhancement scrolls stay in store)");
        campaign.AddSupply("scroll_fury", -2); // read while watching, as the HUD does (from the store)
        run.Pouch["scroll_fury"] = 2;
        var hero = run.Mercenaries[0];
        foreach (var m in run.Monsters) m.Position = new Vector2(-500, -500); // far away: out of combat
        hero.Hp = hero.Stats.Total(StatType.MaxHp) * .2f;
        run.Tick(.05f);
        Require(run.Potions == 2 && hero.Hp > hero.Stats.Total(StatType.MaxHp) * .5f, "a potion drinks itself below 30% HP");
        float attack = hero.Stats.Total(StatType.Attack);
        Require(run.UseSupply("scroll_fury") && hero.Stats.Total(StatType.Attack) > attack * 1.2f, "a scroll strengthens the party");
        Require(run.SupplyBlock("scroll_fury") != null, "scrolls share a cooldown");
        // fatigue: eight dungeon hours (8 real minutes) tire, a camp kit rests
        for (int i = 0; i < 1200 * 8; i++) run.Tick(.05f);
        Require(hero.Fatigue >= SoulFatigue.TierFrom[1], $"eight dungeon hours tire the party ({hero.Fatigue:0}, clock {run.ClockText})");
        var thrust = AssetDatabase.LoadAssetAtPath<SoulPatternData>(DataPath + "/Thrust.asset");
        SoulCombat.PatternCost(hero, thrust, out float tiredCost, out _);
        float fatigue = hero.Fatigue;
        run.Pouch[SoulSupplies.CampKit] = 1; // (not among this party's two kinds)
        Require(run.UseSupply(SoulSupplies.CampKit), "camp with the kit out of combat");
        for (int i = 0; i < 100; i++) run.Tick(.05f);
        Require(hero.Fatigue < fatigue - 50, $"a finished camp takes fatigue away ({fatigue:0} → {hero.Fatigue:0})");
        SoulCombat.PatternCost(hero, thrust, out float restedCost, out _);
        Require(restedCost < tiredCost, "tired mercenaries pay more for their patterns");
        // the escape portal at the entrance: everyone gathered there, no fight — home with the loot
        var exitRun = new SoulDungeonSession(dungeon, rules, trip.Party, 33, 1, 0) { AutoExplore = false };
        var portal = exitRun.Interactables.Find(t => t.Kind == SoulObjectKind.Escape);
        Require(portal != null && Vector2.Distance(portal.Position, exitRun.Map.Center(exitRun.PartyStart)) < 2, "an escape portal stands at the entrance");
        foreach (var m in exitRun.Monsters) m.Position = new Vector2(-500, -500);
        exitRun.Mercenaries[0].Position = portal.Position + Vector2.right * 12;
        exitRun.Tick(.05f);
        Require(exitRun.EscapeBlock(portal) != null && exitRun.Interact(portal, exitRun.Mercenaries[1]) && !exitRun.Recalled, "the portal waits for the whole party");
        exitRun.Mercenaries[0].Position = portal.Position + Vector2.right;
        Require(exitRun.Interact(portal, exitRun.Mercenaries[0]) && exitRun.Recalled, "the whole party at the portal: home");
        // the return scroll: never in a fight, and ten quiet seconds bring everyone home with the loot
        Require(run.UseSupply(SoulSupplies.ReturnScroll) && run.RecallLeft > 0, "the return chant starts out of combat");
        for (int i = 0; i < 220 && !run.Recalled; i++) run.Tick(.05f);
        Require(run.Recalled, "the return scroll takes the party home");
        run.Gold = 50;
        var inventory = campaign.Inventory.Count;
        var betterSword = new SoulItem(AssetDatabase.LoadAssetAtPath<SoulEquipmentData>(DataPath + "/EquipmentSword.asset"), 5) { Dropped = true };
        run.Inventory.Add(betterSword);
        run.Inventory.Add(new SoulItem(AssetDatabase.LoadAssetAtPath<SoulEquipmentData>(DataPath + "/EquipmentQuiver.asset"), 1) { Dropped = true });
        var oldSword = trip.Party.Find(h => h.Equipped(SoulEquipSlot.MainHand)?.Data.Kind == "한손검")?.Equipped(SoulEquipSlot.MainHand);
        bool swordsman = oldSword != null;
        bool archer = trip.Party.Exists(h => h.Equipped(SoulEquipSlot.MainHand)?.WeaponTag == "bow" && h.Equipped(SoulEquipSlot.OffHand) == null);
        campaign.Return(trip);
        Require((!swordsman || trip.Party.Exists(h => h.Equipment.Contains(betterSword)) && campaign.Inventory.Contains(oldSword))
            && (archer || campaign.Inventory.Exists(i => i.Data.Kind == "화살통"))
            && campaign.Roster.Count >= SoulCampaign.StartingMercenaries && campaign.Supply("scroll_fury") == 1 && squad.Carry.Count == 0,
            "home with the loot: the better sword on the swordsman, what nobody needs in the storehouse; unused supplies back on the shelf");
        var report = campaign.Reports.Count > 0 ? campaign.Reports[0] : null;
        Require(report != null && report.Party == squad.Number && report.Gold == 50 && report.Heroes.Count == trip.Party.Count && !report.Seen, "the expedition's report waits in the village");
        // the report's numbers: blows given are counted as they land
        var brawl = new SoulDungeonSession(dungeon, rules, SoulDungeonSession.Recruit(village.StartingRoster, rules), 34, 1, 0) { AutoExplore = false };
        brawl.Mercenaries[0].Position = brawl.Monsters[0].Position + Vector2.right * 1.2f;
        for (int i = 0; i < 400 && brawl.Tally.For(brawl.Mercenaries[0]).Dealt <= 0; i++) brawl.Tick(.05f);
        Require(brawl.Tally.For(brawl.Mercenaries[0]).Dealt > 0, "blows landed are counted for the report");
        // nobody can go down: the night passes at once, to 08:00 the next day
        var sleeper = new SoulCampaign(village, 10);
        int today = SoulClock.Day(sleeper.Clock);
        sleeper.SkipToMorning();
        Require(SoulClock.Day(sleeper.Clock) == today + 1 && Mathf.Abs(SoulClock.TimeOfDay(sleeper.Clock) - SoulClock.Start) < .01f, "skipping to the next morning");
        Require(squad.Members.TrueForAll(h => h.Fatigue == 0), "a night in the village takes all fatigue");
        var fight = new SoulDungeonSession(dungeon, rules, squad.Members, 32, 1, 0) { AutoExplore = false };
        fight.Pouch[SoulSupplies.ReturnScroll] = 1;
        var near = fight.Monsters[0];
        fight.Mercenaries[0].Position = near.Position + Vector2.right * 1.2f;
        for (int i = 0; i < 20; i++) fight.Tick(.05f);
        Require(!fight.InCombat || fight.SupplyBlock(SoulSupplies.ReturnScroll) != null, "no return scroll in a fight");
        foreach (var h in campaign.Roster) { h.HealWounds(SoulMercenary.MaxWounds); h.Hp = h.Stats.Total(StatType.MaxHp); h.Statuses.Clear(); }

        // ── renown gates the third level ──
        campaign.Renown = 0;
        Require(campaign.Upgrade(SoulBuildingKind.Blacksmith) && campaign.Upgrade(SoulBuildingKind.Blacksmith) && campaign.UpgradeBlock(SoulBuildingKind.Blacksmith) != null
            && !campaign.Upgrade(SoulBuildingKind.Blacksmith), "a third level needs renown");
        campaign.Renown = 150;
        Require(campaign.Upgrade(SoulBuildingKind.Blacksmith) && campaign.SmithLevel == 3, "known companies build higher");

        // ── blacksmith ──
        var item = new SoulItem(AssetDatabase.LoadAssetAtPath<SoulEquipmentData>(DataPath + "/EquipmentSword.asset"), 5);
        campaign.Inventory.Add(item);
        Require(campaign.EnchantBlock(item) != null && !campaign.Enchant(item, out _), "the storehouse's pieces are not enchanted");
        var smithed = campaign.Roster.Find(h => SoulCampaign.EquipBlock(h, item) == null);
        Require(smithed != null && campaign.Equip(smithed, item), "worn, it can be");
        Require(campaign.Enchant(item, out _) && campaign.Enchant(item, out _) && item.Enchants.Count == 2 && campaign.EnchantBlock(item) != null, "enchant every free slot, always succeeding");
        Require(campaign.ToggleEnchantLock(item, 0), "lock one option");
        {
            // the second enchant is far dearer than the first; each re-roll dearer and 1% likelier to land high
            var blank = new SoulItem(item.Data, item.Grade);
            int one = campaign.EnchantCost(blank);
            blank.Enchants.Add(new SoulEnchant { Option = item.Enchants[0].Option });
            Require(campaign.EnchantCost(blank) >= one * 8, "a second enchant costs far more");
            int reroll = campaign.RerollCost(blank);
            blank.Rerolls = 4;
            var odds0 = SoulItemRules.TierChances(1); var odds4 = SoulItemRules.TierChances(1, 4);
            Require(campaign.RerollCost(blank) > reroll * 2 && Mathf.Abs(odds4[3] - odds0[3] - .04f) < .001f && Mathf.Abs(odds4[0] - odds0[0] + .04f) < .001f,
                "re-rolls grow dearer and luckier");
        }
        string kept = item.Enchants[0].Option;
        for (int i = 0; i < 5; i++) Require(campaign.Reroll(item, out _), "re-roll");
        Require(item.Enchants[0].Option == kept && item.Enchants[0].Option != item.Enchants[1].Option, "the locked option stays through re-rolls, no duplicates");
        var wearer = campaign.Roster[0];
        var ring = new SoulItem(AssetDatabase.LoadAssetAtPath<SoulEquipmentData>(DataPath + "/EquipmentCopperRing.asset"), 2);
        campaign.Inventory.Add(ring);
        Require(ring.Bonuses.Length == 0 && campaign.Equip(wearer, ring) && campaign.Enchant(ring, out _) && ring.Enchants.Count == 1, "a blank ring worn and enchanted afterwards");

        // ── library: a book spent on one mercenary — learn it, a level up, then the stronger form; levels are its own ──
        var healing = AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/SkillHealingLight.asset");
        var fire = AssetDatabase.LoadAssetAtPath<SoulActiveSkillData>(DataPath + "/SkillFireEnchant.asset");
        Require(fire.UpgradeTo != null && dungeon != null, "skills have stronger forms");
        var mage = Mage(campaign);
        var knight = campaign.Roster.Find(h => h.Job != "마법사" && h.Job != "성직자");
        mage.StartingActives.Remove(fire); mage.LearnedActives.Remove(fire); mage.SkillLevels.Clear();
        campaign.Books.Add(fire); campaign.Books.Add(healing);
        Require(campaign.BookBlock(mage, fire, out _) != null, "no library, no reading");
        Require(campaign.Upgrade(SoulBuildingKind.Library), "build the library");
        Require(campaign.BookBlock(knight, healing, out _) != null, "healing is for priests only");
        int goldBefore = campaign.Gold, booksBefore = campaign.Books.Count;
        Require(campaign.BookBlock(mage, fire, out var firstUse) == null && firstUse == SoulCampaign.BookUse.Learn && campaign.UseBook(mage, fire, out _)
            && mage.AllActiveSkills().Contains(fire) && SoulCampaign.HeroSkillLevel(mage, fire) == 1 && campaign.Gold == goldBefore && campaign.Books.Count == booksBefore - 1,
            "a book teaches its skill (the book is spent, no gold)");
        campaign.Books.Add(fire);
        Require(campaign.BookBlock(mage, fire, out _) != null, "a level-1 library holds level 1");
        Require(campaign.Upgrade(SoulBuildingKind.Library) && campaign.BookBlock(mage, fire, out var up) == null && up == SoulCampaign.BookUse.LevelUp && campaign.UseBook(mage, fire, out _)
            && SoulCampaign.HeroSkillLevel(mage, fire) == 2 && SoulSkillUsePolicy.Power(mage, fire) > 1.1f, "a second book: level 2, stronger");
        var other = Mage(campaign) != mage ? Mage(campaign) : campaign.Roster.Find(h => h != mage && SoulCampaign.KnownSkills(h).Contains(fire));
        if (other != null && other != mage) Require(other.SkillLevel(fire) <= 1, "levels are each mercenary's own");
        campaign.Renown = 400;
        Require(campaign.Upgrade(SoulBuildingKind.Library), "library level 3");
        campaign.Books.Add(fire); campaign.Books.Add(fire);
        Require(campaign.UseBook(mage, fire, out _) && SoulCampaign.HeroSkillLevel(mage, fire) == 3, "level 3");
        string upgraded = null;
        Require(campaign.BookBlock(mage, fire, out var last) == null && last == SoulCampaign.BookUse.Upgrade && campaign.UseBook(mage, fire, out upgraded)
            && mage.AllActiveSkills().Contains(fire.UpgradeTo) && !mage.AllActiveSkills().Contains(fire), "one more book: the stronger form: " + upgraded);
        // an older save's shared shelf: its level moves onto those who know the skill; a shelved skill nobody knows is a book again
        {
            var shelf = new SoulCampaign(village, 93);
            var shelfMage = Mage(shelf);
            var knownSkill = SoulCampaign.KnownSkills(shelfMage)[0];
            shelf.Library[knownSkill] = 2; shelf.Library[healing] = 1;
            bool anyoneHeals = shelf.Roster.Exists(h => SoulCampaign.KnownSkills(h).Contains(healing));
            shelf.MoveShelfOntoMercenaries();
            Require(shelfMage.SkillLevel(knownSkill) == 2 && shelf.Library.Count == 0 && (anyoneHeals || shelf.Books.Contains(healing)), "an old shelf moves onto the mercenaries");
        }

        // ── soul altar ──
        var boarSoul = AssetDatabase.LoadAssetAtPath<SoulData>(DataPath + "/SoulBoar.asset");
        Require(boarSoul.Refined != null && boarSoul.Refined.Grade == boarSoul.Grade + 1, "souls have a refined form");
        for (int i = 0; i < 3; i++) campaign.Vault.Add(boarSoul);
        Require(campaign.FuseBlock(boarSoul) != null, "fusing needs the altar at level 2");
        Require(campaign.Upgrade(SoulBuildingKind.SoulAltar) && campaign.Upgrade(SoulBuildingKind.SoulAltar) && campaign.Fuse(boarSoul) && campaign.Vault.Contains(boarSoul.Refined) && !campaign.Vault.Contains(boarSoul), "three souls fuse into one");
        var taker = campaign.Roster.Find(h => campaign.AbsorbBlock(h, boarSoul.Refined) == null);
        Require(taker != null && campaign.AbsorbAtAltar(taker, boarSoul.Refined) && taker.Souls.Contains(boarSoul.Refined), "absorb a kept soul at the altar");
        Require(campaign.Release(taker, boarSoul.Refined) && campaign.Vault.Contains(boarSoul.Refined), "take a soul back out");

        // ── merchant ──
        campaign.RollMerchant();
        Require(campaign.MerchantHere && campaign.BuyFromMerchant(campaign.MerchantStock[0]), "the wandering merchant sells");
        foreach (var offer in campaign.MerchantStock) if (offer.Item != null) Require(offer.Item.Specials.Count == 0, "the merchant has no special options");

        // ── codex ──
        Require(campaign.Records.Count >= campaign.Roster.Count, "every mercenary is in the codex");

        // ── save and load ──
        var saved = SoulSave.Write(campaign);
        string json = JsonUtility.ToJson(saved);
        // gold, stones and supplies are ItemData's: saved as that section, read back into the loaded campaign
        var wallet = new ItemData();
        wallet.Deserialize(campaign.Wallet.Serialize());
        var loaded = SoulSave.Read(JsonUtility.FromJson<SoulSave.Game>(json), village, wallet);
        Require(!json.Contains("\"Gold\":" + campaign.Gold) || campaign.Gold == 0, "save: gold is not in the campaign section");
        Require(loaded.Gold == campaign.Gold && loaded.Stones == campaign.Stones && loaded.Roster.Count == campaign.Roster.Count && loaded.Parties[0].Members.Count == campaign.Parties[0].Members.Count
            && loaded.Parties[0].TargetFloor == campaign.Parties[0].TargetFloor && loaded.Parties[0].Carry.Count == campaign.Parties[0].Carry.Count
            && loaded.Parties.Count == campaign.Parties.Count && loaded.Reports.Count == campaign.Reports.Count, "save: gold, roster, parties and reports");
        var everyone = new List<SoulMercenary>(campaign.Roster);
        foreach (var away in campaign.Away) everyone.AddRange(away.Party);
        var lostStyle = loaded.Roster.Find(h => h.Style != everyone.Find(o => o.Id == h.Id)?.Style);
        Require(lostStyle == null, $"save: each mercenary keeps its concept ({lostStyle?.Name}: {lostStyle?.Style} ≠ {everyone.Find(o => o.Id == lostStyle?.Id)?.Style})");
        var loadedWearer = loaded.Roster.Find(h => h.Id == wearer.Id);
        Require(loadedWearer.Equipment.Count == wearer.Equipment.Count && loadedWearer.Equipment.Exists(e => e.Id == "copper_ring" && e.Enchants.Count == 1), "save: worn items with enchants");
        Require(loaded.Roster.Exists(h => h.Equipment.Exists(e => e.Enchants.Count == 2 && e.Enchants[0].Locked)), "save: worn items with enchants and locks");
        Require(loaded.Roster.Find(h => h.Id == mage.Id).SkillLevel(fire.UpgradeTo) == 1 && loaded.Level(SoulBuildingKind.Library) == 3 && loaded.Renown == campaign.Renown, "save: skill levels, buildings, renown");
        var loadedMage = loaded.Roster.Find(h => h.Id == mage.Id);
        Require(loadedMage.Level == mage.Level && Mathf.Abs(loadedMage.Stats.Total(StatType.Magic) - mage.Stats.Total(StatType.Magic)) < .01f && loadedMage.AllActiveSkills().Contains(fire.UpgradeTo), "save: a mercenary comes back the same");
        Require(loaded.Records.Count == campaign.Records.Count && loaded.Supply(SoulSupplies.HealPotion) == campaign.Supply(SoulSupplies.HealPotion), "save: codex and supplies");

        // an old save file (gold and supplies inside) moves them into the wallet
        var legacyGame = JsonUtility.FromJson<SoulSave.Game>(json);
        legacyGame.Gold = 777; legacyGame.Stones = 4;
        legacyGame.Supplies.Add(new SoulSave.Pair { Key = SoulSupplies.CampKit, Value = 3 });
        var legacyWallet = new ItemData();
        var migrated = SoulSave.Read(legacyGame, village, legacyWallet, true);
        Require(migrated.Gold == 777 && migrated.Stones == 4 && migrated.Supply(SoulSupplies.CampKit) == 3 && legacyWallet.Get(eItem.Gold) == 777, "an old save file's gold and supplies go into ItemData");

        // ── sparring ──
        var sparring = campaign.Parties[0].Members;
        var before = sparring.ConvertAll(h => (h.Wounds, h.Hp));
        var spar = SoulSparring.Run(campaign, sparring, SoulSparOpponent.Pack, 1, 3);
        float dealt = 0;
        foreach (var line in spar.Lines) dealt += line.Dealt;
        Require(spar.Seconds > 0 && spar.Lines.Count == sparring.Count && dealt > 0, $"sparring fights on copies ({(spar.Won ? "won" : "lost")} in {spar.Seconds:0}s, {sparring.Count} members, {spar.Lines.Count} lines, dealt {dealt:0})");
        Require(sparring.TrueForAll(h => h.Wounds == before[sparring.IndexOf(h)].Wounds && Mathf.Abs(h.Hp - before[sparring.IndexOf(h)].Hp) < .01f), "sparring hurts nobody");
        Debug.Log($"VILLAGE systems: spar {spar.Opponent} {(spar.Won ? "won" : "lost")} {spar.Seconds:0.0}s, save {json.Length} chars");
    }

    static void ValidateProgression(SoulDungeonData dungeon, SoulDungeonData ruins, SoulStatRules rules, SoulMercenaryData ria, SoulMercenaryData thorData)
    {
        // Curve: about 335 experience to level 5 (today's pace), about 1,900 to level 10 (floor 1 cleared), cap 50.
        int total5 = 0, total10 = 0;
        for (int level = 1; level < 10; level++) { if (level < 5) total5 += SoulMercenary.ExperienceFor(level); total10 += SoulMercenary.ExperienceFor(level); }
        Debug.Log($"PROGRESS experience to Lv5 {total5}, to Lv10 {total10}, Lv49→50 {SoulMercenary.ExperienceFor(49)}");
        Require(total5 > 300 && total5 < 380 && total10 > 1700 && total10 < 2100 && SoulMercenary.MaxLevel == 50, "experience curve");

        // First defeat of a species: ten times the experience; the second: once. Each mercenary keeps a bestiary.
        var hunt = new SoulDungeonSession(dungeon, rules, new[] { ria }, 51);
        hunt.AutoExplore = false;
        var hero = hunt.Mercenaries[0];
        var boar = hunt.Monsters[0];
        int TotalXp(SoulMercenary h) { int sum = h.Experience; for (int l = 1; l < h.Level; l++) sum += SoulMercenary.ExperienceFor(l); return sum; }
        int before = TotalXp(hero);
        boar.Hp = 0;
        hunt.Tick(.02f);
        int first = TotalXp(hero) - before;
        Require(hero.Codex.TryGetValue(boar.Data.Id, out var entry) && entry.Defeated == 1, "bestiary records the species");
        var again = new SoulMonster(boar.Data, boar.Position, rules, "again");
        hunt.Monsters.Add(again);
        int expBefore = TotalXp(hero);
        again.Hp = 0;
        hunt.Tick(.02f);
        int second = TotalXp(hero) - expBefore;
        Require(second == boar.Data.Experience && hero.Codex[boar.Data.Id].Defeated == 2, $"a repeat kill pays once ({second})");
        Require(first == boar.Data.Experience * SoulDungeonSession.FirstKillMultiplier, $"a first kill pays ten times ({first})");

        // Milestones: levels 10, 20 … give a pattern to pick (the only manual step) and a soul slot.
        var grower = SoulDungeonSession.Recruit(new[] { thorData }, rules)[0];
        int slots = grower.SoulSlots;
        while (grower.Level < 10) grower.AddExperience(grower.ExperienceToNext, rules, new System.Random(grower.Level));
        Require(grower.SoulSlots == slots + 1 && grower.CanPickPattern, "level 10: a soul slot and a pattern pick");
        var milestone = new SoulDungeonSession(dungeon, rules, new[] { grower }, 52, 1);
        var choice = milestone.OpenLevel(grower);
        Require(choice != null && choice.Patterns.Length == 3 && milestone.OpenLevel(grower) == choice, "three patterns to choose from, no reroll");
        Require(milestone.ConfirmLevel(choice, choice.Patterns[0]) && !grower.CanPickPattern && grower.Patterns().Contains(choice.Patterns[0]), "the picked pattern is learned");
        while (grower.Level < SoulMercenary.MaxLevel) grower.AddExperience(grower.ExperienceToNext, rules, new System.Random(grower.Level));
        Require(grower.Level == 50 && grower.SoulSlots == 6 && grower.PatternPicks == 4, "level 50: six soul slots, four more picks");

        // Floors: monsters grow exponentially; clearing a floor raises the grade (9 → 8 …); the party goes down.
        var floor1 = new SoulDungeonSession(ruins, rules, new[] { ria, thorData }, 53);
        var floor3 = new SoulDungeonSession(ruins, rules, SoulDungeonSession.Recruit(new[] { ria, thorData }, rules), 53, 3);
        // every floor has its own monsters (a type shared by both is compared)
        Require(ruins.Floors.Length == SoulDungeonSession.FinalFloor && ruins.Roster(1).Boss != ruins.Roster(8).Boss
            && !ruins.Roster(8).All().Exists(m => ruins.Roster(1).All().Contains(m) && !m.Guardian), "each floor has its own monsters");
        var floorSouls = new HashSet<SoulData>();
        for (int f = 1; f <= SoulDungeonSession.FinalFloor; f++)
            foreach (var monster in ruins.Roster(f).All())
            {
                Require(monster.DroppedSoul != null && monster.DroppedSoul.CharacteristicStats.Length > 0, $"{monster.Name} drops a soul");
                floorSouls.Add(monster.DroppedSoul);
            }
        Require(floorSouls.Count >= 30, $"the floors hold many kinds of soul ({floorSouls.Count})");
        Require(floor3.Monsters.Exists(m => ruins.Roster(3).All().Contains(m.Data)) && !floor3.Monsters.Exists(m => m.Data == ruins.Roster(1).Boss), "floor 3 spawns its own roster");
        var m1 = floor1.Monsters.Find(m => !m.Data.Guardian && floor3.Monsters.Exists(o => o.Data == m.Data));
        var m3 = m1 != null ? floor3.Monsters.Find(m => m.Data == m1.Data) : null;
        Require(m3 != null && Mathf.Abs(m3.Stats.Total(StatType.MaxHp) / m1.Stats.Total(StatType.MaxHp) - SoulDungeonSession.FloorPower(3)) < .05f, "floor 3 monsters: health ×2.56");
        var leaver = floor1.Mercenaries[0];
        Require(leaver.Grade == 9, "grade 9 at the start");
        leaver.Position = floor1.Map.Center(floor1.Exits[0] + new Vector2Int(0, 2));
        floor1.AutoExplore = false;
        floor1.SetDestination(leaver, floor1.Exits[0], out _);
        for (int i = 0; i < 100 && !floor1.Finished; i++) floor1.Tick(.05f);
        Require(floor1.Finished && leaver.Grade == 8 && floor1.HasNextFloor, "floor 1 cleared: grade 8");
        var floor2 = floor1.NextFloor(54);
        Require(floor2.Floor == 2 && floor2.Mercenaries[0] == leaver && leaver.Hp == leaver.Stats.Total(StatType.MaxHp), "the party goes down to floor 2, rested");
        leaver.HighestFloorCleared = SoulDungeonSession.FinalFloor;
        Require(leaver.Grade == 1, "all eight floors: grade 1");

        // Pathfinder: hidden chests behind secret doors; aims for the party (hunt / farm / break through).
        var lukaData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryLuka.asset");
        var guided = new SoulDungeonSession(ruins, rules, new[] { thorData, lukaData }, 55);
        var hidden = guided.Chests.FindAll(c => c.Hidden);
        Require(hidden.Count >= 4, $"hidden treasure chests ({hidden.Count})");
        foreach (var chest in hidden)
            Require(!guided.Map.Reachable(guided.Map.Center(guided.PartyStart), chest.Position, .3f), "a hidden chest sits behind a closed secret door");
        Require(guided.CanChooseRoute && hidden.Exists(c => guided.Knows(c)) && hidden.Exists(c => !guided.Knows(c)), "a pathfinder's luck marks some hidden chests (not all)");
        var plain = new SoulDungeonSession(ruins, rules, new[] { thorData }, 55);
        Require(!plain.CanChooseRoute && !plain.Chests.Exists(c => c.Hidden && plain.Knows(c)), "without one they stay unknown");
        plain.ExploreMode = SoulExploreMode.Advance;
        Require(plain.EffectiveMode == SoulExploreMode.Explore, "no aim without a pathfinder");

        guided.ExploreMode = SoulExploreMode.Advance;
        guided.Tick(.6f); guided.Tick(.6f);
        var aim = guided.Destination(guided.Leader);
        bool toPortal = false;
        foreach (var portal in guided.Exits) toPortal |= Vector2Int.Distance(aim, portal) < 2;
        Require(toPortal, $"advance: the party heads for a portal ({aim})");
        guided.ExploreMode = SoulExploreMode.Farm;
        for (int i = 0; i < 12; i++) guided.Tick(.05f);
        var boss = guided.Monsters.Find(m => m.Data.Guardian);
        Require(Vector2.Distance(guided.DestinationPoint(guided.Leader), boss.Position) < 3, "farm: the party heads for the boss");

        // A pathfinder in combat is weak.
        var lukaHero = guided.Mercenaries[1];
        var tank = guided.Mercenaries[0];
        Debug.Log($"PROGRESS pathfinder attack {lukaHero.Stats.Total(StatType.Attack):0.0} vs tank {tank.Stats.Total(StatType.Attack):0.0}");
        Require(lukaHero.Stats.Total(StatType.Attack) < tank.Stats.Total(StatType.Attack) * .7f, "the pathfinder hits much weaker");
    }

    static void ValidateHiddenAndObjects(SoulDungeonData dungeon, SoulDungeonData ruins, SoulStatRules rules, SoulMercenaryData ria, SoulMercenaryData thorData)
    {
        var lukaData = AssetDatabase.LoadAssetAtPath<SoulMercenaryData>(DataPath + "/MercenaryLuka.asset");

        // Luck: more luck, more marked in advance, and a wider reach for secret doors and traps.
        Require(SoulDungeonSession.LuckChance(9) > SoulDungeonSession.LuckChance(4) && SoulDungeonSession.LuckChance(-1) == 0 && SoulDungeonSession.LuckChance(100) <= .95f, "luck chance");

        // Hidden stages behind secret walls: a vault, traps and monsters, or a boss; every kind appears over a few maps.
        var kinds = new HashSet<SoulHiddenStageKind>();
        int stages = 0, traps = 0, objects = 0;
        for (int seed = 61; seed < 67; seed++)
        {
            var layout = SoulDungeonGenerator.Generate(ruins, seed);
            var map = new SoulMap(layout.Rows);
            foreach (var stage in layout.HiddenStages)
            {
                stages++;
                kinds.Add(stage.Kind);
                Require(map.Tile(stage.Door) == SoulTile.HiddenDoor, "a hidden stage is behind a secret door");
                Require(!map.Reachable(map.Center(layout.Start), map.Center(SoulDungeonGenerator.Center(stage.Rect)), .3f), "a hidden stage is closed until found");
                Require(layout.Chests.Exists(chest => stage.Rect.Contains(chest.Cell)), "a hidden stage holds treasure");
            }
            traps += layout.Traps.Count;
            objects += layout.Objects.Count;
        }
        Debug.Log($"HIDDEN 6 maps: {stages} hidden stages ({string.Join(", ", kinds)}), {traps} traps, {objects} objects");
        Require(stages >= 8 && kinds.Count == 3 && traps > 0 && objects >= 6 * 20, "hidden stages of every kind, traps, and plenty of objects");

        // Traps: stepping on one hurts; a pathfinder in reach spots and disarms it first.
        var trapped = new SoulDungeonSession(dungeon, rules, new[] { ria }, 62);
        trapped.AutoExplore = false;
        foreach (var monster in trapped.Monsters) monster.Hp = 0;
        var walker = trapped.Mercenaries[0];
        walker.Position = trapped.Map.Center(new Vector2Int(1, 8));
        var trap = new SoulTrap { Position = trapped.Map.Center(new Vector2Int(5, 8)), Kind = SoulTrapKind.Spikes };
        trapped.Traps.Add(trap);
        trapped.SetDestination(walker, trapped.Map.Center(new Vector2Int(8, 8)), out Vector2 _);
        float hp = walker.Hp;
        for (int i = 0; i < 80 && !trap.Sprung; i++) trapped.Tick(.05f);
        Require(trap.Sprung && walker.Hp < hp, "a trap springs under a mercenary");
        var guided = new SoulDungeonSession(dungeon, rules, new[] { lukaData }, 63);
        guided.AutoExplore = false;
        var guide = guided.Mercenaries[0];
        guide.Position = guided.Map.Center(new Vector2Int(1, 8));
        var spotted = new SoulTrap { Position = guided.Map.Center(new Vector2Int(3, 8)), Kind = SoulTrapKind.Poison };
        guided.Traps.Add(spotted);
        guided.Tick(.05f);
        Require(spotted.Found && !spotted.Sprung, "a pathfinder spots and disarms a trap in reach");

        // Objects: walk up and use; each does its thing once (the campfire again after a while).
        var camp = new SoulDungeonSession(dungeon, rules, new[] { ria, thorData }, 64);
        camp.AutoExplore = false;
        foreach (var monster in camp.Monsters) monster.Hp = 0;
        camp.Tick(.05f);
        var user = camp.Mercenaries[0];
        user.Position = camp.Map.Center(new Vector2Int(1, 8));
        SoulInteractable Place(SoulObjectKind kind, Vector2 offset)
        {
            var thing = new SoulInteractable { Kind = kind, Position = camp.Map.Center(new Vector2Int(6, 8)) + offset, Insight = true, WarpDoor = new Vector2Int(-1, -1) };
            camp.Interactables.Add(thing);
            return thing;
        }
        var spring = Place(SoulObjectKind.Spring, Vector2.zero);
        user.Hp = 5;
        // Too far from everyone: a click does nothing and says why; with a mercenary near it, it is used there and then.
        var farSpring = Place(SoulObjectKind.Spring, new Vector2(SoulDungeonSession.InteractReach + 10, 0));
        Require(camp.InteractBlock(farSpring) != null && !camp.Interact(farSpring, user) && !farSpring.Used, "nobody near: the object cannot be used");
        camp.Interactables.Remove(farSpring);
        Require(camp.Interact(spring, user) && spring.Used, "a mercenary near it: a click uses the object at once");
        Require(user.Hp == user.Stats.Total(StatType.MaxHp) && !camp.CanUse(spring) && !camp.Interact(spring, user), "the spring heals the party, once");

        float attack = user.Stats.Total(StatType.Attack);
        camp.Use(Place(SoulObjectKind.Blessing, Vector2.zero), user);
        Require(user.Stats.Total(StatType.Attack) > attack, "the blessing raises attack");

        camp.Use(Place(SoulObjectKind.Altar, Vector2.zero), user);
        Require(camp.ExperienceBoost > 0, "the altar boosts experience");

        foreach (var hero in camp.Mercenaries) hero.Hp = 1;
        var fire = Place(SoulObjectKind.Campfire, Vector2.zero);
        foreach (var hero in camp.Mercenaries) hero.Position = camp.Map.Center(new Vector2Int(1, 8));
        // a campsite far away: the party walks over, sits around it, then camps
        Require(camp.Interact(fire, user) && camp.GoingToCamp && camp.Camping <= 0, "a click on a campsite calls the party over");
        for (int i = 0; i < 1200 && camp.Camping <= 0; i++) camp.Tick(.05f);
        Require(camp.Camping > 0 && camp.Mercenaries.TrueForAll(h => !h.Alive || Vector2.Distance(h.Position, fire.Position) <= SoulDungeonSession.InteractReach),
            $"gathered around the campsite, the camp begins (camping {camp.Camping:0.0} · going {camp.GoingToCamp} · {string.Join(", ", camp.Mercenaries.ConvertAll(h => $"{Vector2.Distance(h.Position, fire.Position):0.0}"))} · {string.Join(" / ", camp.Events.GetRange(0, Mathf.Min(4, camp.Events.Count)))})");
        for (int i = 0; i < 100; i++) camp.Tick(.05f);
        Require(camp.Mercenaries.TrueForAll(h => h.Hp >= h.Stats.Total(StatType.MaxHp) * .95f) && fire.Cooldown > 0 && !fire.Used, "camping restores the party; the fire can be used again later");

        // clicked in the middle of a fight: the fight is finished first, then the party gathers and camps
        {
            var fire2 = Place(SoulObjectKind.Campfire, new Vector2(0, 1));
            var foe = camp.AddMonster(camp.Monsters[0].Data, user.Position + Vector2.right * 1.2f);
            for (int i = 0; i < 10; i++) camp.Tick(.05f);
            bool wasFighting = camp.InCombat;
            Require(camp.Interact(fire2, user) && camp.GoingToCamp && camp.Camping <= 0, "a campsite clicked during a fight waits for it");
            for (int i = 0; i < 20; i++) camp.Tick(.05f);
            Require(wasFighting && camp.Camping <= 0, "no camp while the fight goes on");
            foe.Hp = 0;
            for (int i = 0; i < 1200 && camp.Camping <= 0; i++) camp.Tick(.05f);
            Require(camp.Camping > 0, "the fight over, the party gathers and camps");
            for (int i = 0; i < 100; i++) camp.Tick(.05f);
        }
        int explored = Count(camp.Explored);
        camp.Use(Place(SoulObjectKind.Watchtower, new Vector2(8, 0)), user);
        Require(Count(camp.Explored) > explored, "the watchtower reveals the land around it");

        var warp = Place(SoulObjectKind.Warp, Vector2.zero);
        warp.WarpKind = SoulWarpKind.Boss;
        warp.WarpTarget = camp.Map.Center(new Vector2Int(13, 2));
        camp.Use(warp, user);
        Require(camp.Mercenaries.TrueForAll(h => Vector2.Distance(h.Position, warp.WarpTarget) < 2.5f), "the warp stone moves the whole party");

        int monsters = camp.Monsters.Count;
        var mimic = Place(SoulObjectKind.Suspicious, Vector2.zero);
        mimic.Mimic = true;
        camp.Use(mimic, user);
        Require(camp.Monsters.Count > monsters && camp.Monsters[camp.Monsters.Count - 1].Alive, "a mimic calls monsters");
        int goldBefore = camp.Gold; // the test room has no loot list: the gold shows the payout
        camp.Use(Place(SoulObjectKind.Suspicious, Vector2.zero), user);
        Require(camp.Gold > goldBefore, "a real treasure chest pays out");

        // the fortune statue is retired: no floor places one any more (its places go to the other objects)
        for (int seed = 0; seed < 6; seed++)
        {
            var floorRun = new SoulDungeonSession(ruins, rules, new[] { ria, thorData }, 300 + seed);
            Require(!floorRun.Interactables.Exists(t => t.Kind == SoulObjectKind.Fortune), "no fortune statue on a floor");
        }

        // Floors: each has its own name, screen filter and sight. Darker floors uncover less at a glance.
        var names = new HashSet<string>();
        for (int floor = 1; floor <= SoulDungeonSession.FinalFloor; floor++)
        {
            var theme = SoulFloorTheme.For(floor);
            names.Add(theme.Name);
            Require(theme.Filter.a > .05f && theme.Vision >= .5f && theme.Vision <= 1.5f, $"floor {floor} theme values");
        }
        Require(names.Count == SoulDungeonSession.FinalFloor && SoulFloorTheme.Count == SoulDungeonSession.FinalFloor, "every floor has its own look");
        var mine = new SoulDungeonSession(ruins, rules, new[] { ria }, 66);
        var abyss = new SoulDungeonSession(ruins, rules, SoulDungeonSession.Recruit(new[] { ria }, rules), 66, SoulDungeonSession.FinalFloor);
        Require(mine.Theme.Name == "광산" && abyss.Theme.Vision < mine.Theme.Vision && Count(abyss.Explored) < Count(mine.Explored),
            $"the dark last floor sees less than the mine ({Count(abyss.Explored)} vs {Count(mine.Explored)})");

        // A pathfinder's luck also tells warp destinations and mimics apart before using them.
        var luck = new SoulDungeonSession(ruins, rules, new[] { lukaData }, 65);
        Require(luck.Interactables.Exists(thing => thing.Insight) && luck.Interactables.Exists(thing => !thing.Insight), "luck gives insight into some objects");
    }

    static int Count(bool[] cells)
    {
        int count = 0;
        foreach (bool cell in cells) if (cell) count++;
        return count;
    }

    static string LeapEvents(SoulDungeonData dungeon, SoulStatRules rules, SoulMercenaryData thorData, SoulData soul)
    {
        var session = new SoulDungeonSession(dungeon, rules, new[] { thorData }, 5);
        var thor = session.Mercenaries[0];
        if (soul != null) Require(thor.Absorb(soul, rules), "absorb for leap test");
        thor.Stamina = thor.Stats.Total(StatType.MaxStamina);
        thor.Position = session.Monsters[0].Position + Vector2.right * 3f;
        session.CombatEvents.Clear();
        session.Tick(.02f);
        bool leap = session.CombatEvents.Exists(e => e.Kind == SoulEventKind.Leap);
        bool wave = session.CombatEvents.Exists(e => e.Kind == SoulEventKind.Shockwave);
        return (leap ? "leap" : "") + (wave ? "+shockwave" : "");
    }

    static T Asset<T>(string file, Action<T> initialize) where T : ScriptableObject
    {
        string path = DataPath + "/" + file + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<T>(path);
        if (item != null) { initialize(item); EditorUtility.SetDirty(item); return item; }
        item = ScriptableObject.CreateInstance<T>();
        initialize(item); AssetDatabase.CreateAsset(item, path);
        return item;
    }

    static SoulPatternData Pattern(string file, string id, SoulPatternCategory category, float cost, float time, float range,
        SoulDamageSchool school, SoulDamageKind kind, SoulValue damage)
        => Asset(file, (SoulPatternData item) =>
        { item.Id = id; item.Category = category; item.StaminaCost = cost; item.ActionTime = time; item.Range = range; item.DamageSchool = school; item.DamageKind = kind; item.Damage = damage; item.Icon = IconSprite("pattern:" + file); });

    static SoulMonsterData Monster(string file, string id, float vitality, float strength, SoulPatternData[] patterns, SoulData soul, float chance)
        => Asset(file, (SoulMonsterData item) =>
        { item.Id = id; item.Stats = new[] { Bonus(StatType.Vitality, vitality), Bonus(StatType.Strength, strength) }; item.Patterns = patterns; item.DroppedSoul = soul; item.SoulDropChance = chance; });

    static SoulTelegraphData Telegraph(string file, string id, string name, SoulAreaShape shape, SoulAreaAnchor anchor, float size, float width,
        float windUp, float rest, float trigger, SoulValue damage, float knockback)
        => Asset(file, (SoulTelegraphData item) =>
        {
            item.Id = id; item.SkillName = name; item.Shape = shape; item.Anchor = anchor; item.Size = size; item.Width = width;
            item.WindUp = windUp; item.Rest = rest; item.TriggerRange = trigger; item.Damage = damage; item.Knockback = knockback;
        });

    // ── floors: every floor has its own monsters, fitting its theme (SoulFloorTheme) ──
    // Base numbers stay near the first floor's (SoulDungeonSession.FloorPower scales every floor); what changes
    // is who fights and how: goblins in the mine, frogfolk and slugs in the moss, orcs in the collapsed tunnels,
    // fishmen by the lake, fire lizards on the lava, the dead in the bone pits, vampires in the sanctum, demons
    // at the abyss. Each floor has singles, two or three packs (a front line, sometimes shooters behind it),
    // an elite and a guardian of its own.
    static SoulFloorRoster[] FloorRosters(SoulMonsterData boar, SoulMonsterData wolf, SoulMonsterData slug, SoulMonsterData stone,
        SoulMonsterData goblin, SoulMonsterData goblinWarrior, SoulMonsterData goblinChief, SoulMonsterData guardian,
        SoulPatternData swing, SoulPatternData thrust, SoulPatternData bow, SoulPatternData guard, SoulPatternData approach, SoulPatternData dodge,
        SoulPatternData keepDistance, SoulPatternData charge, SoulPatternData flank, SoulPatternData whirl, SoulPatternData manaBolt,
        SoulTelegraphData chiefSweep, SoulTelegraphData[] bossTelegraphs)
    {
        SoulMonsterData Humanoid(string file, string name, string race, string weapon, SoulPatternData[] patterns, float height, float weight,
            SoulStatBonus[] stats, string armor = "", string helmet = "", string shield = "", string horns = "", Color? tint = null)
        {
            bool ears = Array.IndexOf(new[] { "DarkElf", "Demon", "Elf", "Furry", "Goblin", "Merman", "Orc", "Teddy", "Vampire", "Werewolf" }, race) >= 0;
            return Asset(file, (SoulMonsterData item) =>
            {
                item.Id = name; item.DisplayName = name; item.Patterns = patterns; item.Stats = stats ?? new SoulStatBonus[0];
                item.DroppedSoul = null; item.SoulDropChance = 0;
                item.UseMonsterSprite = false;
                item.Appearance = new UnitAppearanceData { Body = race, Head = race, Ears = ears ? race : "", Eyes = race, Weapon = weapon, Armor = armor, Helmet = helmet, Shield = shield, Horns = horns };
                item.Height = height; item.Weight = weight; item.Tint = tint ?? Color.white;
                item.Elite = false; item.Guardian = false; item.Telegraphs = new SoulTelegraphData[0]; item.ActiveSkills = new SoulActiveSkillData[0];
                item.Experience = 8; item.Gold = 5;
            });
        }
        SoulMonsterData Beast(string file, string name, EnemyRace race, Color tint, SoulPatternData[] patterns, float height, float weight, SoulStatBonus[] stats)
            => Asset(file, (SoulMonsterData item) =>
            {
                item.Id = name; item.DisplayName = name; item.Patterns = patterns; item.Stats = stats ?? new SoulStatBonus[0];
                item.DroppedSoul = null; item.SoulDropChance = 0;
                item.UseMonsterSprite = true; item.MonsterRace = race; item.Tint = tint; item.Height = height; item.Weight = weight;
                item.Elite = false; item.Guardian = false; item.Telegraphs = new SoulTelegraphData[0]; item.ActiveSkills = new SoulActiveSkillData[0];
                item.Experience = 8; item.Gold = 5;
            });
        // An elite: alone it must outweigh a whole pack (the goblin berserker's numbers).
        SoulMonsterData Elite(SoulMonsterData monster)
        {
            monster.Stats = new[] { Bonus(StatType.Vitality, 80), Bonus(StatType.Strength, 16), Bonus(StatType.Durability, 6), Bonus(StatType.Agility, 4), Bonus(StatType.Will, 8), Bonus(StatType.ImpactPower, 4), Bonus(StatType.Threat, 1.5f) };
            monster.Elite = true; monster.Telegraphs = new[] { chiefSweep }; monster.Experience = 30; monster.Gold = 25;
            EditorUtility.SetDirty(monster);
            return monster;
        }
        // A guardian: the ruin guardian's numbers and its four telegraphed patterns.
        SoulMonsterData Boss(SoulMonsterData monster)
        {
            monster.Stats = new[] { Bonus(StatType.Vitality, 140), Bonus(StatType.Strength, 16), Bonus(StatType.Durability, 10), Bonus(StatType.ImpactPower, 8), Bonus(StatType.Will, 12), Bonus(StatType.Threat, 2.5f) };
            monster.Guardian = true; monster.Telegraphs = bossTelegraphs; monster.Experience = 30; monster.Gold = 40;
            EditorUtility.SetDirty(monster);
            return monster;
        }
        SoulStatBonus[] Melee(float vitality, float strength, float agility = 2, float durability = 1)
            => new[] { Bonus(StatType.Vitality, vitality), Bonus(StatType.Strength, strength), Bonus(StatType.Agility, agility), Bonus(StatType.Durability, durability) };
        SoulStatBonus[] Shooter(float vitality, float agility)
            => new[] { Bonus(StatType.Vitality, vitality), Bonus(StatType.Strength, 3), Bonus(StatType.Agility, agility), Bonus(StatType.PiercePower, 3) };
        SoulStatBonus[] Caster(float vitality, float magic)
            => new[] { Bonus(StatType.Vitality, vitality), Bonus(StatType.Magic, magic), Bonus(StatType.Will, 3), Bonus(StatType.Agility, 3) };
        SoulFloorRoster Floor(string name, SoulMonsterData[] singles, SoulMonsterData elite, SoulMonsterData boss, params SoulPackTemplate[] packs)
            => new SoulFloorRoster { Name = name, Singles = singles, Elites = new[] { elite }, Boss = boss, Packs = packs };

        var hitAndGuard = new[] { swing, guard, approach };
        var spear = new[] { thrust, approach };
        var archer = new[] { bow, dodge, keepDistance };
        var caster = new[] { manaBolt, keepDistance };

        // 1 광산 — goblins dig for blue crystals; the crystal golem guards the deepest vein
        var miner = Humanoid("MonsterGoblinMiner", "고블린 광부", "Goblin", "Pickaxe", new[] { swing, approach }, 1.2f, 45, Melee(5, 4), helmet: "MinerHelment");
        var golem = Boss(Beast("MonsterCrystalGolem", "결정 골렘", EnemyRace.Troll, new Color(.62f, .78f, 1f), hitAndGuard, 2.8f, 240, null));
        // 2 이끼 동굴 — frogfolk with spears and bows, slugs on the walls, a moss troll, the toad lord
        var frog = Humanoid("MonsterFrogSpear", "개구리인 창병", "Froggy", "Bident", spear, 1.3f, 55, Melee(6, 4, 4));
        var frogShooter = Humanoid("MonsterFrogShooter", "개구리인 사냥꾼", "Froggy", "ShortBow", archer, 1.25f, 50, Shooter(4, 5));
        var mossTroll = Elite(Beast("MonsterMossTroll", "이끼 트롤", EnemyRace.Troll, new Color(.62f, .9f, .55f), hitAndGuard, 2.2f, 170, null));
        var toadLord = Boss(Humanoid("MonsterToadLord", "늪의 군주", "Froggy", "Crusher", hitAndGuard, 2.6f, 260, null, armor: "Chief", tint: new Color(.8f, 1f, .75f)));
        // 3 무너진 갱도 — orc miners and raiders among the fallen beams; the ruin guardian
        var orcMiner = Humanoid("MonsterOrcMiner", "오크 광부", "Orc", "LargePickaxe", new[] { swing, approach }, 1.7f, 90, Melee(8, 5, 2, 2), armor: "MinerArmour", helmet: "MinerHelment");
        var orc = Humanoid("MonsterOrcRaider", "오크 약탈자", "Orc", "Axe", hitAndGuard, 1.75f, 95, Melee(8, 6, 2, 2), armor: "BanditTunic", shield: "WoodenBuckler");
        var orcChief = Elite(Humanoid("MonsterOrcChief", "오크 우두머리", "Orc", "Greataxe", hitAndGuard, 2f, 130, null, armor: "Chief", helmet: "HornsHelmet"));
        // 4 지하 호수 — fishmen from the water, lizardfolk on the shore, the lake's drake
        var lizard = Humanoid("MonsterLizard", "도마뱀인", "Lizard", "Saber", new[] { swing, dodge, approach }, 1.6f, 70, Melee(6, 5, 5));
        var fishman = Humanoid("MonsterFishSpear", "어인 창병", "Merman", "Bident", spear, 1.6f, 75, Melee(7, 5, 3, 2));
        var fishCaster = Humanoid("MonsterFishCaller", "어인 물술사", "Merman", "WaterWand", caster, 1.55f, 65, Caster(5, 5));
        var fishGuard = Elite(Humanoid("MonsterFishWarden", "어인 파수꾼", "Merman", "GuardianHalberd", hitAndGuard, 1.9f, 110, null, armor: "GuardianTunic", shield: "BlueShield"));
        var lakeDrake = Boss(Humanoid("MonsterLakeDrake", "호수의 주인", "Drakosha", "Lance", hitAndGuard, 2.7f, 250, null, horns: "Drakosha", tint: new Color(.65f, .95f, 1f)));
        // 5 용암 지대 — fire lizards and scorched boars; the lava troll, the fire drake
        var fireLizard = Humanoid("MonsterFireLizard", "불도마뱀", "FireLizard", "Cleaver", new[] { swing, charge, approach }, 1.6f, 75, Melee(7, 6, 3));
        var fireCaster = Humanoid("MonsterFireCaller", "불도마뱀 주술사", "FireLizard", "FireWand", caster, 1.55f, 65, Caster(5, 6));
        var lavaBoar = Beast("MonsterLavaBoar", "용암 멧돼지", EnemyRace.Hog, new Color(1f, .62f, .5f), new[] { charge, swing, approach }, 1.6f, 110, Melee(7, 6));
        var lavaTroll = Elite(Beast("MonsterLavaTroll", "용암 트롤", EnemyRace.Troll, new Color(1f, .6f, .42f), hitAndGuard, 2.3f, 180, null));
        var fireDrake = Boss(Humanoid("MonsterFireDrake", "화염 드라코", "Drakosha", "FireSword", new[] { swing, charge, approach }, 2.7f, 250, null, armor: "FireWarriorArmor", horns: "Drakosha", tint: new Color(1f, .75f, .7f)));
        // 6 뼈 무덤 — skeleton ranks, shambling dead, the bone knight, the bone king
        var skeleton = Humanoid("MonsterSkeleton", "해골 병사", "Skeleton", "RustedShortSword", hitAndGuard, 1.7f, 45, Melee(5, 5, 3), shield: "WoodenBuckler");
        var skeletonArcher = Humanoid("MonsterSkeletonArcher", "해골 궁수", "Skeleton", "LongBow", archer, 1.7f, 40, Shooter(4, 5));
        var zombie = Humanoid("MonsterZombie", "구울", "ZombieA", "", new[] { swing, approach }, 1.7f, 80, Melee(9, 5, 1));
        var rotten = Humanoid("MonsterRotten", "썩은 시체", "ZombieB", "WoodenClub", new[] { swing, approach }, 1.75f, 85, Melee(10, 4, 1));
        var boneKnight = Elite(Humanoid("MonsterBoneKnight", "해골 기사", "Skeleton", "BlackBroadsword", hitAndGuard, 1.9f, 90, null, armor: "DarkKnight", helmet: "DarkKnight", shield: "KnightShield"));
        var boneKing = Boss(Humanoid("MonsterBoneKing", "뼈의 왕", "Skeleton", "DeathScythe", new[] { swing, approach }, 2.6f, 200, null, armor: "King", helmet: "King"));
        // 7 영혼 성소 — vampires and their mages, werewolves; the sanctum's demigods
        var vampire = Humanoid("MonsterVampire", "흡혈귀", "Vampire", "Epee", new[] { thrust, dodge, approach }, 1.8f, 70, Melee(7, 6, 5), armor: "Dracula");
        var vampireCaster = Humanoid("MonsterVampireMage", "흡혈 마도사", "Vampire", "SkullWand", caster, 1.8f, 65, Caster(6, 6), armor: "NecromancerRobe");
        var werewolf = Humanoid("MonsterWerewolf", "늑대인간", "Werewolf", "", new[] { swing, dodge, flank }, 1.9f, 100, Melee(8, 7, 5));
        var sanctumGuard = Elite(Humanoid("MonsterSanctumGuard", "성소 수호자", "Demigod", "GuardianHalberd", hitAndGuard, 2f, 120, null, armor: "Angel", helmet: "Angel"));
        var fallenDemigod = Boss(Humanoid("MonsterFallenDemigod", "타락한 반신", "Demigod", "GiantSword", new[] { swing, whirl, approach }, 2.8f, 240, null, armor: "DemigodArmour", tint: new Color(.85f, .75f, 1f)));
        // 8 심연의 왕좌 — demon soldiers and warlocks, the demon general, the abyss lord
        var demon = Humanoid("MonsterDemon", "악마 병사", "Demon", "Slasher", hitAndGuard, 1.9f, 100, Melee(9, 7, 3, 2), armor: "HornsKnight", horns: "Demon");
        var demonCaster = Humanoid("MonsterDemonWarlock", "악마 주술사", "Demon", "NecromancerStaff", caster, 1.85f, 80, Caster(7, 7), armor: "DarkNecromant", horns: "Demon");
        var demonGeneral = Elite(Humanoid("MonsterDemonGeneral", "악마 장군", "Demon", "Executioner", hitAndGuard, 2.2f, 150, null, armor: "DarkKnight", helmet: "HornsKnightHelmet"));
        var abyssLord = Boss(Humanoid("MonsterAbyssLord", "심연의 군주", "Demon", "MasterGreataxe", new[] { swing, whirl, approach }, 3f, 300, null, armor: "Executioner", horns: "Demon", tint: new Color(.9f, .6f, .65f)));

        // ── more kinds: a slime for each element, casters behind the lines, assassins, beastfolk ──
        T Get<T>(string file) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(DataPath + "/" + file + ".asset");
        var sting = Get<SoulPatternData>("PoisonSting");
        var fireSpit = Pattern("FireSpit", "불씨 뱉기", SoulPatternCategory.Attack, 4, 1.1f, 1.4f, SoulDamageSchool.Physical, SoulDamageKind.Fire, Value(1, Term(StatType.Attack, .7f)));
        fireSpit.Status = Status(SoulStatus.Burn, Value(.45f), Value(3), Value(1.5f)); EditorUtility.SetDirty(fireSpit);
        var frostSpit = Pattern("FrostSpit", "서리 뱉기", SoulPatternCategory.Attack, 4, 1.1f, 1.4f, SoulDamageSchool.Physical, SoulDamageKind.Cold, Value(1, Term(StatType.Attack, .7f)));
        frostSpit.Status = Status(SoulStatus.Chill, Value(.5f), Value(2.5f)); EditorUtility.SetDirty(frostSpit);
        var dreadBolt = Pattern("DreadBolt", "공포탄", SoulPatternCategory.Attack, 2, 1.2f, 3.5f, SoulDamageSchool.Magic, SoulDamageKind.Arcane, Value(2, Term(StatType.Magic, .8f)));
        dreadBolt.ManaCost = 3; dreadBolt.Tags = new[] { "magic" }; dreadBolt.Status = Status(SoulStatus.Fear, Value(.3f), Value(2)); EditorUtility.SetDirty(dreadBolt);
        var fireSlime = Beast("MonsterFireSlime", "불 슬라임", EnemyRace.Slug, new Color(1f, .55f, .35f), new[] { fireSpit, approach }, 1.1f, 60, Melee(6, 3));
        var frostSlime = Beast("MonsterFrostSlime", "얼음 슬라임", EnemyRace.Slug, new Color(.6f, .85f, 1f), new[] { frostSpit, approach }, 1.1f, 60, Melee(6, 3));
        var plagueSlime = Beast("MonsterPlagueSlime", "역병 슬라임", EnemyRace.Slug, new Color(.78f, .55f, .95f), new[] { sting, approach }, 1.2f, 70, Melee(8, 3));
        var bloodSlime = Beast("MonsterBloodSlime", "핏빛 슬라임", EnemyRace.Slug, new Color(.95f, .32f, .38f), new[] { swing, approach }, 1.3f, 80, Melee(9, 4));
        var goblinShaman = Humanoid("MonsterGoblinShaman", "고블린 주술사", "Goblin", "GreenWand", caster, 1.15f, 38, Caster(4, 4), armor: "DruidRobe");
        var darkElf = Humanoid("MonsterDarkElf", "다크엘프 암살자", "DarkElf", "MarderDagger", new[] { Get<SoulPatternData>("Ambush"), dodge, flank }, 1.7f, 55, Melee(6, 5, 6), armor: "NinjaTunic", helmet: "NinjaMask");
        var beastfolk = Humanoid("MonsterBeastfolk", "야수인 사냥꾼", "Furry", "Pitchfork", new[] { thrust, dodge, flank }, 1.6f, 70, Melee(7, 5, 4), armor: "BanditTunic");
        var skeletonMage = Humanoid("MonsterSkeletonMage", "해골 마법사", "Skeleton", "NecromancerStaff", new[] { dreadBolt, keepDistance }, 1.7f, 40, Caster(4, 5), armor: "DeathRobe", helmet: "DeathHood");

        // ── souls: every family pulls a mercenary its own way ──
        // elemental hits, control, treasure hunting, curses, hit-and-run, a giant's body, stamina on kills, counters,
        // magic defence, spells for anyone, thorns, surviving a death blow, wound resistance, lifesteal, frenzy,
        // healing others, executing, soul farming, armour-piercing, loot and secret doors — bosses hand out actives.
        SoulPassiveSkillData Passive(string file, string id, string name, Action<SoulPassiveSkillData> set)
            => Asset(file, (SoulPassiveSkillData item) =>
            {
                item.SoulId = id; item.SkillName = name; item.SoulEvent = SoulTrigger.Always; item.Every = 1;
                item.AlwaysBonuses = new SoulStatBonus[0]; item.Damage = new SoulValue(); item.Heal = new SoulValue(); item.Stamina = 0;
                item.MapTraits = SoulMapTrait.None; item.Icon = IconSprite("passive:" + id);
                set(item);
            });
        SoulPassiveSkillData Bonuses(string file, string id, string name, params SoulStatBonus[] bonuses)
            => Passive(file, id, name, item => item.AlwaysBonuses = bonuses);
        SoulData Soul(string file, string id, string origin, SoulStatBonus[] stats, SoulPatternData[] patterns = null, SoulActiveSkillData[] actives = null,
            SoulPassiveSkillData[] passives = null, float height = 1, float weight = 1)
            => Asset(file, (SoulData item) =>
            {
                item.Id = id; item.OriginMonster = origin; item.Grade = 1; item.CharacteristicStats = stats;
                item.Patterns = patterns ?? new SoulPatternData[0]; item.ActiveSkills = actives ?? new SoulActiveSkillData[0];
                item.Passives = passives ?? new SoulPassiveSkillData[0]; item.HeightMultiplier = height; item.WeightMultiplier = weight; item.CorePattern = null;
            });
        void Drops(SoulData soul, params SoulMonsterData[] monsters)
        {
            soul.Source = monsters[0]; EditorUtility.SetDirty(soul);
            foreach (var monster in monsters)
            {
                monster.DroppedSoul = soul; monster.SoulDropChance = monster.Guardian ? .4f : monster.Elite ? .12f : .02f;
                EditorUtility.SetDirty(monster);
            }
        }
        var cast = Get<SoulPatternData>("MagicCast");
        var holyTouch = Asset("SkillHolyTouch", (SoulActiveSkillData item) =>
        {
            item.SoulId = "holy_touch"; item.SkillName = "성스러운 손길"; item.Trigger = SoulTrigger.AllyHurt; item.RequiredJob = ""; item.RequiredPattern = null;
            item.CastTime = 1f; item.Cooldown = 16; item.Range = 4f;
            item.Costs = new[] { new SoulResourceCost { Resource = SoulResource.Mana, Amount = 10 } };
            item.Heal = Value(6, Term(StatType.Will, 1f)); item.HealsWounds = false; item.Damage = new SoulValue();
            item.Icon = IconSprite("skill:holy_touch");
        });

        Drops(Soul("SoulFireSlime", "fire_slime", "불 슬라임", new[] { Bonus(StatType.Magic, 1), Bonus(StatType.Vitality, 1) }, new[] { fireSpit }, null,
            new[] { Bonuses("PassiveFireBody", "fire_body", "불꽃 체질", Bonus(StatType.FireAttack, 3), Bonus(StatType.BurnResist, .2f)) }), fireSlime);
        Drops(Soul("SoulFrostSlime", "frost_slime", "얼음 슬라임", new[] { Bonus(StatType.Will, 2) }, new[] { frostSpit }, null,
            new[] { Bonuses("PassiveFrostBody", "frost_body", "냉기 체질", Bonus(StatType.ColdAttack, 3), Bonus(StatType.ChillResist, .2f)) }), frostSlime);
        Drops(Soul("SoulPlagueSlime", "plague_slime", "역병 슬라임", new[] { Bonus(StatType.Vitality, 2) }, null, null,
            new[] { Passive("PassivePlague", "plague", "역병 전파", p => { p.SoulEvent = SoulTrigger.OnAttackLanded; p.Every = 2; p.Damage = Value(1, Term(StatType.Vitality, .4f)); p.AlwaysBonuses = new[] { Bonus(StatType.ToxicAttack, 2) }; }) }), plagueSlime);
        Drops(Soul("SoulBloodSlime", "blood_slime", "핏빛 슬라임", new[] { Bonus(StatType.Regeneration, 3) }, null, null,
            new[] { Passive("PassiveDevour", "devour", "포식", p => { p.SoulEvent = SoulTrigger.OnKill; p.Heal = Value(3, Term(StatType.Vitality, 1f)); }) }), bloodSlime);
        Drops(Soul("SoulGoblinMiner", "goblin_miner", "고블린 광부", new[] { Bonus(StatType.Luck, 2) }, null, null,
            new[] { Passive("PassiveOreSense", "ore_sense", "광맥 감각", p => { p.MapTraits = SoulMapTrait.RevealTreasure; p.AlwaysBonuses = new[] { Bonus(StatType.GoldFind, .15f) }; }) }), miner);
        Drops(Soul("SoulGoblinShaman", "goblin_shaman", "고블린 주술사", new[] { Bonus(StatType.Magic, 2) }, new[] { cast }, new[] { Get<SoulActiveSkillData>("SkillCurseWeakness") }), goblinShaman);
        Drops(Soul("SoulFrog", "frog", "개구리인", new[] { Bonus(StatType.Agility, 2) }, new[] { Get<SoulPatternData>("Kite") }, null,
            new[] { Bonuses("PassiveFrogLegs", "frog_legs", "개구리 다리", Bonus(StatType.Evasion, .05f), Bonus(StatType.TirelessWalk, .2f)) }), frog, frogShooter);
        Drops(Soul("SoulTroll", "troll", "트롤", new[] { Bonus(StatType.Vitality, 4), Bonus(StatType.Regeneration, 2) }, null, null,
            new[] { Bonuses("PassiveTrollBlood", "troll_blood", "트롤의 피", Bonus(StatType.KnockbackResist, .3f), Bonus(StatType.WoundResist, .2f)) }, 1.1f, 1.35f), mossTroll, lavaTroll);
        Drops(Soul("SoulOrc", "orc", "오크", new[] { Bonus(StatType.Strength, 2), Bonus(StatType.Vitality, 1) }, new[] { whirl }, null,
            new[] { Bonuses("PassiveWarBreath", "war_breath", "전장의 숨결", Bonus(StatType.KillStamina, .15f)) }, 1.05f, 1.15f), orc, orcMiner, orcChief);
        Drops(Soul("SoulLizard", "lizard", "도마뱀인", new[] { Bonus(StatType.Agility, 2), Bonus(StatType.SlashPower, 1) }, new[] { Get<SoulPatternData>("Counter") }, null,
            new[] { Bonuses("PassiveScales", "scales", "비늘 몸놀림", Bonus(StatType.Evasion, .06f)) }), lizard);
        Drops(Soul("SoulFishman", "fishman", "어인", new[] { Bonus(StatType.AntiMagic, 3) }, null, null,
            new[] { Bonuses("PassiveTideGuard", "tide_guard", "조류의 가호", Bonus(StatType.FireResist, .2f), Bonus(StatType.ColdResist, .2f), Bonus(StatType.LightningResist, .2f)) }), fishman, fishGuard);
        Drops(Soul("SoulFishCaller", "fish_caller", "어인 물술사", new[] { Bonus(StatType.Magic, 3) }, new[] { cast }, new[] { Get<SoulActiveSkillData>("SkillFrostShard") }), fishCaster);
        Drops(Soul("SoulFireLizard", "fire_lizard", "불도마뱀", new[] { Bonus(StatType.Durability, 2), Bonus(StatType.Strength, 1) }, null, null,
            new[] { Bonuses("PassiveMagmaScale", "magma_scale", "용암 비늘", Bonus(StatType.Thorns, .12f), Bonus(StatType.BurnResist, .3f)) }), fireLizard);
        Drops(Soul("SoulFireCaller", "fire_caller", "불도마뱀 주술사", new[] { Bonus(StatType.Magic, 3) }, new[] { cast }, new[] { Get<SoulActiveSkillData>("SkillFireBolt") }), fireCaster);
        Drops(Soul("SoulSkeleton", "skeleton", "해골", new[] { Bonus(StatType.Will, 2), Bonus(StatType.Durability, 1) }, null, null,
            new[] { Bonuses("PassiveUndying", "undying", "불굴의 뼈", Bonus(StatType.DeathDefy, 1), Bonus(StatType.FearResist, .3f)) }, 1f, .8f), skeleton, skeletonArcher, boneKnight);
        Drops(Soul("SoulSkeletonMage", "skeleton_mage", "해골 마법사", new[] { Bonus(StatType.Magic, 2), Bonus(StatType.Will, 1) }, new[] { dreadBolt }), skeletonMage);
        Drops(Soul("SoulGhoul", "ghoul", "구울", new[] { Bonus(StatType.Vitality, 3) }, null, null,
            new[] { Bonuses("PassiveRotFlesh", "rot_flesh", "썩은 살", Bonus(StatType.WoundResist, .3f), Bonus(StatType.PoisonResist, .3f)) }), zombie, rotten);
        Drops(Soul("SoulVampire", "vampire", "흡혈귀", new[] { Bonus(StatType.Agility, 2) }, null, null,
            new[] { Bonuses("PassiveBloodThirst", "blood_thirst", "흡혈", Bonus(StatType.LifeSteal, .05f)) }), vampire);
        Drops(Soul("SoulVampireMage", "vampire_mage", "흡혈 마도사", new[] { Bonus(StatType.Magic, 3) }, new[] { cast }, new[] { Get<SoulActiveSkillData>("SkillSlow") }), vampireCaster);
        Drops(Soul("SoulWerewolf", "werewolf", "늑대인간", new[] { Bonus(StatType.Strength, 2), Bonus(StatType.Agility, 2) }, null, null,
            new[] { Bonuses("PassiveFrenzy", "frenzy", "광폭", Bonus(StatType.DoubleHit, .1f), Bonus(StatType.LowHpCostReduce, .2f)) }), werewolf);
        Drops(Soul("SoulSanctum", "sanctum", "성소 수호자", new[] { Bonus(StatType.Will, 3) }, null, new[] { holyTouch }), sanctumGuard);
        Drops(Soul("SoulDemon", "demon", "악마", new[] { Bonus(StatType.Strength, 3) }, null, null,
            new[] { Bonuses("PassiveExecution", "execution", "처형 본능", Bonus(StatType.ExecuteBonus, .25f), Bonus(StatType.EliteDamage, .1f)) }), demon, demonGeneral);
        Drops(Soul("SoulDemonWarlock", "demon_warlock", "악마 주술사", new[] { Bonus(StatType.Magic, 2), Bonus(StatType.Will, 1) }, null, null,
            new[] { Bonuses("PassiveSoulHarvest", "soul_harvest", "영혼 수확", Bonus(StatType.SoulFind, .15f)) }), demonCaster);
        Drops(Soul("SoulDarkElf", "dark_elf", "다크엘프", new[] { Bonus(StatType.PiercePower, 2), Bonus(StatType.Agility, 1) }, new[] { Get<SoulPatternData>("Ambush") }, null,
            new[] { Bonuses("PassiveVitalSpot", "vital_spot", "급소 파악", Bonus(StatType.ArmorShred, .15f)) }), darkElf);
        Drops(Soul("SoulBeastfolk", "beastfolk", "야수인", new[] { Bonus(StatType.Luck, 1), Bonus(StatType.Agility, 1) }, null, null,
            new[] { Passive("PassiveScent", "scent", "전리품 냄새", p => { p.MapTraits = SoulMapTrait.DetectHiddenDoors; p.AlwaysBonuses = new[] { Bonus(StatType.LootFind, .15f) }; }) }), beastfolk);
        Drops(Get<SoulData>("SoulBoar"), boar, lavaBoar);
        // guardians: rare souls that bring an active skill or a signature passive
        Drops(Soul("SoulCrystalGolem", "crystal_golem", "결정 골렘", new[] { Bonus(StatType.Durability, 4), Bonus(StatType.AntiMagic, 2) }, null, new[] { Get<SoulActiveSkillData>("SkillGroundSlam") },
            new[] { Bonuses("PassiveCrystalHide", "crystal_hide", "결정 갑피", Bonus(StatType.KnockbackResist, .4f), Bonus(StatType.PetrifyResist, .5f)) }, 1.1f, 1.4f), golem);
        Drops(Soul("SoulToadLord", "toad_lord", "늪의 군주", new[] { Bonus(StatType.Vitality, 4) }, new[] { Get<SoulPatternData>("ApplyPoison") }, null,
            new[] { Bonuses("PassiveSwampBlood", "swamp_blood", "늪의 피", Bonus(StatType.ToxicAttack, 5), Bonus(StatType.PoisonResist, .5f)) }, 1f, 1.3f), toadLord);
        Drops(Soul("SoulLakeDrake", "lake_drake", "호수의 주인", new[] { Bonus(StatType.Will, 2), Bonus(StatType.Magic, 2) }, null, new[] { Get<SoulActiveSkillData>("SkillFrostEnchant") }), lakeDrake);
        Drops(Soul("SoulFireDrake", "fire_drake", "화염 드라코", new[] { Bonus(StatType.Strength, 3), Bonus(StatType.Magic, 2) }, null, new[] { Get<SoulActiveSkillData>("SkillFireEnchant") }), fireDrake);
        Drops(Soul("SoulBoneKing", "bone_king", "뼈의 왕", new[] { Bonus(StatType.Will, 4) }, new[] { dreadBolt }, null,
            new[] { Bonuses("PassiveDeathLord", "death_lord", "망자의 왕", Bonus(StatType.DeathDefy, 1), Bonus(StatType.FearResist, .5f), Bonus(StatType.WoundResist, .3f)) }), boneKing);
        Drops(Soul("SoulDemigod", "demigod", "타락한 반신", new[] { Bonus(StatType.Will, 3), Bonus(StatType.Strength, 2) }, null, new[] { Get<SoulActiveSkillData>("SkillHaste") }), fallenDemigod);
        Drops(Soul("SoulAbyssLord", "abyss_lord", "심연의 군주", new[] { Bonus(StatType.Strength, 4), Bonus(StatType.Will, 3) }, null, null,
            new[] { Bonuses("PassiveAbyss", "abyss", "심연의 권능", Bonus(StatType.ExecuteBonus, .3f), Bonus(StatType.LifeSteal, .04f), Bonus(StatType.DoubleHit, .08f)) }, 1.1f, 1.2f), abyssLord);

        return new[]
        {
            Floor("1층 광산", new[] { boar, slug, miner }, goblinChief, golem,
                Pack("고블린 무리", new[] { goblinWarrior }, new[] { goblin, goblinShaman }, 4, 6, .4f, .12f, 1f),
                Pack("고블린 광부 무리", new[] { miner }, null, 3, 5, 0, .12f, .8f),
                Pack("멧돼지 떼", new[] { boar }, null, 3, 4, 0, .12f, .45f)),
            Floor("2층 이끼 동굴", new[] { slug, frog, beastfolk }, mossTroll, toadLord,
                Pack("개구리인 무리", new[] { frog }, new[] { frogShooter }, 4, 6, .4f, .12f, 1f),
                Pack("슬라임 무리", new[] { slug }, null, 4, 8, 0, .12f, 1f),
                Pack("야수인 사냥대", new[] { beastfolk }, new[] { goblinShaman }, 3, 5, .3f, .3f, 1f)),
            Floor("3층 무너진 갱도", new[] { wolf, orcMiner, darkElf }, orcChief, guardian,
                Pack("오크 약탈대", new[] { orc }, new[] { goblin }, 4, 6, .35f, .12f, 1f),
                Pack("오크 광부 무리", new[] { orcMiner }, null, 3, 5, 0, .12f, .7f),
                Pack("석상 수비대", new[] { stone }, new[] { goblin }, 3, 5, .4f, .5f, 1f)),
            Floor("4층 지하 호수", new[] { lizard, fishman, frostSlime }, fishGuard, lakeDrake,
                Pack("어인 무리", new[] { fishman }, new[] { fishCaster }, 4, 6, .4f, .12f, 1f),
                Pack("도마뱀인 무리", new[] { lizard }, null, 3, 5, 0, .12f, .8f),
                Pack("얼음 슬라임 무리", new[] { frostSlime }, null, 4, 7, 0, .12f, .7f)),
            Floor("5층 용암 지대", new[] { fireLizard, lavaBoar, fireSlime }, lavaTroll, fireDrake,
                Pack("불도마뱀 무리", new[] { fireLizard }, new[] { fireCaster }, 4, 6, .4f, .12f, 1f),
                Pack("용암 멧돼지 떼", new[] { lavaBoar }, null, 3, 4, 0, .12f, .6f),
                Pack("불 슬라임 무리", new[] { fireSlime }, null, 4, 7, 0, .12f, .8f)),
            Floor("6층 뼈 무덤", new[] { skeleton, zombie, plagueSlime }, boneKnight, boneKing,
                Pack("해골 부대", new[] { skeleton }, new[] { skeletonArcher, skeletonMage }, 4, 7, .4f, .12f, 1f),
                Pack("시체 떼", new[] { zombie, rotten }, null, 4, 8, 0, .12f, .8f),
                Pack("역병 슬라임 무리", new[] { plagueSlime }, null, 4, 7, 0, .3f, 1f)),
            Floor("7층 영혼 성소", new[] { vampire, werewolf, darkElf }, sanctumGuard, fallenDemigod,
                Pack("흡혈귀 무리", new[] { vampire }, new[] { vampireCaster }, 4, 6, .4f, .12f, 1f),
                Pack("늑대인간 무리", new[] { werewolf }, null, 3, 5, 0, .12f, .8f),
                Pack("그림자 암살단", new[] { darkElf }, new[] { vampireCaster }, 3, 5, .3f, .3f, 1f)),
            Floor("8층 심연의 왕좌", new[] { demon, werewolf, bloodSlime }, demonGeneral, abyssLord,
                Pack("악마 군단", new[] { demon }, new[] { demonCaster }, 4, 7, .4f, .12f, 1f),
                Pack("심연 혼성군", new[] { werewolf, skeleton }, new[] { skeletonArcher, skeletonMage }, 4, 6, .35f, .3f, 1f),
                Pack("핏빛 슬라임 무리", new[] { bloodSlime }, null, 4, 7, 0, .12f, .8f)),
        };
    }

    static SoulPackTemplate Pack(string name, SoulMonsterData[] front, SoulMonsterData[] back, int min, int max, float backShare, float minDepth, float maxDepth)
        => new SoulPackTemplate { Name = name, Front = front ?? new SoulMonsterData[0], Back = back ?? new SoulMonsterData[0], Min = min, Max = max, BackShare = backShare, MinDepth = minDepth, MaxDepth = maxDepth };

    static SoulStatusApply Status(SoulStatus kind, SoulValue chance, SoulValue duration, SoulValue dps = null)
        => new SoulStatusApply { Kind = kind, Chance = chance, Duration = duration, DamagePerSecond = dps ?? new SoulValue() };
    static SoulStatInfo Info(StatType stat, string name, Color color)
        => new SoulStatInfo { Id = stat, DisplayName = name, Description = SoulDescribe.UpperDescription(stat), Color = color, Icon = IconSprite("stat:" + stat) };

    static Sprite IconSprite(string key) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_project/3.Textures/Icons/SoulMercenaries/{key.Replace(':', '_')}.png");
    static SoulStatBonus Bonus(StatType stat, float value) => new SoulStatBonus { Stat = stat, Value = value };
    static SoulStatTerm Term(StatType stat, float factor) => new SoulStatTerm { Stat = stat, Factor = factor };
    static SoulValue Value(float flat = 0, params SoulStatTerm[] terms) => new SoulValue { Flat = flat, Terms = terms };
    static SoulMonsterSpawn Spawn(SoulMonsterData monster, int x, int y) => new SoulMonsterSpawn { Monster = monster, Cell = new Vector2Int(x, y) };
    // Resist makes a single roll probabilistic; retry so the check stays seed independent.
    static bool Apply(SoulCombatant target, SoulStatus kind, float duration, float dps, System.Random random)
    {
        for (int i = 0; i < 50; i++) if (SoulCombat.ApplyStatus(target, kind, 1, duration, dps, random)) return true;
        return false;
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
