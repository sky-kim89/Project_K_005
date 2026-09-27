using UnityEditor;
using UnityEngine;

// ============================================================
//  PlayModeExitTimer.cs  [Editor Only]
//  플레이 종료가 오래 걸리는 원인 확인용: 종료 단계별 소요 시간을 콘솔에 남긴다.
//    [ExitTimer] quit → save … ms   (SoulExpeditionRunner: 종료 저장)
//    [ExitTimer] play mode exit … ms (ExitingPlayMode → EnteredEditMode 전체)
// ============================================================

[InitializeOnLoad]
static class PlayModeExitTimer
{
    static readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

    static PlayModeExitTimer()
    {
        EditorApplication.playModeStateChanged -= Changed;
        EditorApplication.playModeStateChanged += Changed;
    }

    static void Changed(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode) watch.Restart();
        else if (change == PlayModeStateChange.EnteredEditMode && watch.IsRunning)
        {
            watch.Stop();
            Debug.Log($"[ExitTimer] play mode exit {watch.ElapsedMilliseconds} ms");
        }
    }
}
