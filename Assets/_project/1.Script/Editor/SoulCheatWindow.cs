using System.Collections.Generic;
using SoulMercenaries;
using UnityEditor;
using UnityEngine;

// ============================================================
//  SoulCheatWindow.cs  [Editor Only]
//  Tools > Project K > 도구 > 영혼 용병단 치트
//
//  플레이 모드 전용. 시간 / 원정 / 재화·소모품 / 용병 / 던전.
// ============================================================

public class SoulCheatWindow : EditorWindow
{
    int tab;
    static readonly string[] Tabs = { "시간", "원정", "재화", "용병", "던전" };
    int gold = 10000, supplyCount = 5, levels = 1, renown = 100;
    Vector2 scroll;

    [MenuItem(ProjectKMenu.Tool + "영혼 용병단 치트", priority = ProjectKMenu.ToolPrio + 11)]
    static void Open() => GetWindow<SoulCheatWindow>("영혼 용병단 치트");

    static SoulCampaign Campaign => SoulCampaign.Current;

    void OnInspectorUpdate() => Repaint();

    void OnGUI()
    {
        if (!Application.isPlaying) { EditorGUILayout.HelpBox("플레이 모드에서만 사용할 수 있습니다.", MessageType.Warning); return; }
        tab = GUILayout.Toolbar(tab, Tabs);
        EditorGUILayout.Space(6);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (tab == 4) DungeonTab();
        else if (Campaign == null) EditorGUILayout.HelpBox("마을을 한 번 열어야 캠페인이 생깁니다.", MessageType.Info);
        else if (tab == 0) TimeTab();
        else if (tab == 1) TripTab();
        else if (tab == 2) WalletTab();
        else HeroTab();
        EditorGUILayout.EndScrollView();
    }

    // ── 시간 ─────────────────────────────────────────────────

    void TimeTab()
    {
        var c = Campaign;
        EditorGUILayout.LabelField("현재", $"{SoulClock.Text(c.Clock)}  ·  원정 {c.Away.Count}");
        SoulExpeditionRunner.Speed = EditorGUILayout.Slider("시간 배속", SoulExpeditionRunner.Speed, 0, 60);
        using (new EditorGUILayout.HorizontalScope())
            foreach (float speed in new[] { 0f, 1f, 5f, 20f, 60f })
                if (GUILayout.Button(speed == 0 ? "정지" : $"×{speed}")) SoulExpeditionRunner.Speed = speed;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("시각으로 이동 (오늘, 뒤로 가지 않음)", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("12:00")) GoTo(12 * 60);
            if (GUILayout.Button("18:00")) GoTo(18 * 60);
            if (GUILayout.Button("22:00")) GoTo(22 * 60);
            if (GUILayout.Button("23:55")) GoTo(23 * 60 + 55);
        }
        EditorGUILayout.LabelField("시간 흘려보내기 (원정도 진행)", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+10분")) Skip(10);
            if (GUILayout.Button("+1시간")) Skip(60);
            if (GUILayout.Button("+6시간")) Skip(360);
            if (GUILayout.Button("다음 날 00:00")) Skip(SoulClock.DayMinutes - SoulClock.TimeOfDay(c.Clock));
        }
        if (GUILayout.Button("오늘 입장 기록 초기화")) { c.EnteredOn.Clear(); c.Touch("[치트] 입장 기록 초기화"); }
    }

    // Straight to a time today: the clock jumps (parties below do not get the time in between).
    static void GoTo(float minuteOfDay)
    {
        var c = Campaign;
        float target = (SoulClock.Day(c.Clock) - 1) * SoulClock.DayMinutes + minuteOfDay;
        if (target <= c.Clock) return;
        c.Clock = target;
        c.Advance(0);
        c.Touch("[치트] " + SoulClock.Text(c.Clock));
    }

    // Village minutes, simulated in steps like play (parties below go on, capped by the frame backlog).
    static void Skip(float minutes)
    {
        var c = Campaign;
        bool watching = c.Watching != null && !c.Watching.Settled;
        float scale = watching ? SoulClock.DungeonRate : 1;   // Advance slows the clock while watching
        for (float left = minutes; left > 0; left -= 1)
            c.Advance(Mathf.Min(1, left) * scale, 50);
        c.Touch("[치트] " + SoulClock.Text(c.Clock));
    }

    // ── 원정 ─────────────────────────────────────────────────

    void TripTab()
    {
        var c = Campaign;
        if (c.Away.Count == 0) { EditorGUILayout.HelpBox("원정 중인 파티가 없습니다.", MessageType.Info); return; }
        foreach (var away in c.Away.ToArray())
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var s = away.Session;
                EditorGUILayout.LabelField($"{away.Name}  ·  {away.Status}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{s.Floor}층 · 목표 {s.TargetFloor}층 · 생존 {away.Alive}/{away.Party.Count} · 던전 {s.ClockText} · 남은 {SoulClock.DungeonHoursLeft(c.Clock, away.ReturnAt):0.0}시간 · 금화 {s.Gold} · 장비 {s.Inventory.Count} · 영혼석 {s.PreservationItems}");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("몬스터 전멸")) KillAll(s);
                    if (GUILayout.Button("파티 회복")) HealParty(s);
                    GUI.enabled = s.HasNextFloor;
                    if (GUILayout.Button("다음 층")) c.NextFloor(away);
                    GUI.enabled = true;
                    if (GUILayout.Button("지금 귀환")) c.Escape(away);
                }
            }
        }
    }

    // ── 재화 · 소모품 ─────────────────────────────────────────

    void WalletTab()
    {
        var c = Campaign;
        EditorGUILayout.LabelField("금화", $"{c.Gold}");
        using (new EditorGUILayout.HorizontalScope())
        {
            gold = EditorGUILayout.IntField(gold);
            if (GUILayout.Button("금화 추가", GUILayout.Width(100))) { c.Wallet.Add(eItem.Gold, gold); c.Touch($"[치트] 금화 +{gold}"); }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            renown = EditorGUILayout.IntField(renown);
            if (GUILayout.Button("명성 추가", GUILayout.Width(100))) { c.Renown += renown; c.Touch($"[치트] 명성 +{renown}"); }
        }
        if (GUILayout.Button("건물 전부 최대 레벨"))
        {
            foreach (var def in c.Data.Buildings) c.Levels[def.Kind] = def.UpgradeCosts == null || def.UpgradeCosts.Length <= 1 ? Mathf.Max(1, c.Level(def.Kind)) : c.MaxLevelOf(def.Kind);
            c.Touch("[치트] 건물 최대 레벨");
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("소모품", EditorStyles.boldLabel);
        supplyCount = EditorGUILayout.IntSlider("개수", supplyCount, 1, 50);
        if (GUILayout.Button($"모든 소모품 +{supplyCount}")) foreach (var supply in SoulSupplies.All) c.AddSupply(supply.Id, supplyCount);
        foreach (var supply in SoulSupplies.All)
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(supply.Name, $"{c.Supply(supply.Id)}");
                if (GUILayout.Button($"+{supplyCount}", GUILayout.Width(60))) c.AddSupply(supply.Id, supplyCount);
                if (GUILayout.Button("0", GUILayout.Width(30))) c.AddSupply(supply.Id, -c.Supply(supply.Id));
            }
    }

    // ── 용병 ─────────────────────────────────────────────────

    void HeroTab()
    {
        var c = Campaign;
        levels = EditorGUILayout.IntSlider("레벨업", levels, 1, 20);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button($"집에 있는 전원 +{levels}레벨")) { foreach (var hero in c.Roster) LevelUp(hero, levels); c.Touch($"[치트] 레벨 +{levels}"); }
            if (GUILayout.Button("전원 부상 · 피로 회복")) { foreach (var hero in c.Roster) Restore(hero); c.Touch("[치트] 회복"); }
        }
        EditorGUILayout.Space(4);
        foreach (var hero in c.Roster.ToArray())
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{hero.Name}  Lv.{hero.Level} {hero.Job}", $"{c.PartyOf(hero)?.Name ?? "-"} · 부상 {hero.Wounds} · 영혼 {hero.Souls.Count}/{hero.SoulSlots}");
                if (GUILayout.Button($"+{levels}Lv", GUILayout.Width(60))) { LevelUp(hero, levels); c.Touch(); }
                if (GUILayout.Button("회복", GUILayout.Width(50))) { Restore(hero); c.Touch(); }
            }
    }

    static void LevelUp(SoulMercenary hero, int count)
    {
        var random = new System.Random();
        for (int i = 0; i < count && hero.ExperienceToNext > 0; i++) hero.AddExperience(hero.ExperienceToNext, Campaign.Rules, random);
    }

    static void Restore(SoulMercenary hero)
    {
        hero.HealWounds(SoulMercenary.MaxWounds);
        SoulFatigue.Add(hero, -SoulFatigue.Max);
        hero.Statuses.Clear();
        hero.Hp = hero.Stats.Total(StatType.MaxHp);
        hero.Mp = hero.Stats.Total(StatType.MaxMp);
        hero.Stamina = hero.Stats.Total(StatType.MaxStamina);
    }

    // ── 던전 (보고 있는 화면) ──────────────────────────────────

    void DungeonTab()
    {
        var controller = FindFirstObjectByType<SoulGameplayController>();
        var session = controller != null ? controller.Session : null;
        if (session == null) { EditorGUILayout.HelpBox("던전 화면이 열려 있지 않습니다.", MessageType.Info); return; }
        EditorGUILayout.LabelField("현재", $"{session.Floor}층 · 남은 적 {session.Monsters.FindAll(m => m.Alive).Count} · 금화 {session.Gold} · 영혼석 {session.PreservationItems}");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("몬스터 전멸")) KillAll(session);
            if (GUILayout.Button("보스 처치")) foreach (var m in session.Monsters) if (m.Data.Guardian) m.Hp = 0;
            if (GUILayout.Button("파티 회복")) HealParty(session);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("금화 +1000")) session.Gold += 1000;
            if (GUILayout.Button("영혼석 +5")) { session.Pouch[SoulSupplies.SoulStone] = 5; session.StowStones(); }
            GUI.enabled = session.HasNextFloor;
            if (GUILayout.Button("다음 층")) controller.NextFloor();
            GUI.enabled = true;
        }
    }

    static void KillAll(SoulDungeonSession session)
    {
        foreach (var monster in session.Monsters) monster.Hp = 0;
    }

    static void HealParty(SoulDungeonSession session)
    {
        foreach (var hero in session.Mercenaries) if (hero.Alive) Restore(hero);
    }
}
