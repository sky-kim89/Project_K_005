using System;
using System.Collections;
using UnityEngine;

#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

// Android 전용 로컬 알림. 앱을 떠날 때 내일부터 3일간 19:00 알림을 예약하고,
// 돌아오면 남은 예약을 지운다. 설정과 메시지는 기기 안에만 저장된다.
public sealed class AndroidLocalNotificationManager : MonoBehaviour
{
    const string EnabledPrefKey = "ProjectK.NotificationsEnabled";
    const string PermissionAskedPrefKey = "ProjectK.NotificationPermissionAsked";
    const string ChannelId      = "adventure_reminders";
    const int    FirstId        = 19001;
    const int    ReminderCount  = 3;

#if UNITY_ANDROID && !UNITY_EDITOR
    static AndroidLocalNotificationManager _instance;
#endif

    public static bool Enabled => PlayerPrefs.GetInt(EnabledPrefKey, 1) != 0;

    public static bool PermissionNeeded
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Enabled &&
                   AndroidNotificationCenter.UserPermissionToPost != PermissionStatus.Allowed;
#else
            return false;
#endif
        }
    }

    public static bool IsSupported
    {
        get
        {
#if UNITY_ANDROID
            return true;
#else
            return false;
#endif
        }
    }

    public static void SetEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(EnabledPrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

#if UNITY_ANDROID && !UNITY_EDITOR
        if (enabled)
            _instance.EnsurePermission();
        else
            CancelReminders();
#endif
    }

    public static void ToggleFromSettings()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (PermissionNeeded)
        {
            _instance.EnsurePermission();
            return;
        }
#endif
        SetEnabled(!Enabled);
    }

    public static DateTime ReminderTime(DateTime now, int daysFromToday)
        => now.Date.AddDays(daysFromToday).AddHours(19);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var go = new GameObject(nameof(AndroidLocalNotificationManager));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AndroidLocalNotificationManager>();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    void Awake()
    {
        var localization = LocalizationManager.Instance;
        var channel = new AndroidNotificationChannel(
            ChannelId,
            localization.Get("모험 알림"),
            localization.Get("모험을 이어가거나 새로 시작할 때 알려드려요."),
            Importance.Default)
        {
            EnableVibration = false,
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);
        CancelReminders();

        if (Enabled && PlayerPrefs.GetInt(PermissionAskedPrefKey, 0) == 0)
            EnsurePermission();
    }

    void EnsurePermission()
    {
        if (AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.Allowed)
            return;

        if (PlayerPrefs.GetInt(PermissionAskedPrefKey, 0) != 0)
        {
            AndroidNotificationCenter.OpenNotificationSettings(ChannelId);
            return;
        }

        StartCoroutine(RequestPermission());
    }

    IEnumerator RequestPermission()
    {
        var request = new PermissionRequest();
        while (request.Status == PermissionStatus.RequestPending)
            yield return null;

        PlayerPrefs.SetInt(PermissionAskedPrefKey, 1);
        PlayerPrefs.Save();

        if (request.Status != PermissionStatus.Allowed)
            CancelReminders();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            ScheduleReminders();
        else
            CancelReminders();
    }

    void OnApplicationQuit() => ScheduleReminders();

    static void ScheduleReminders()
    {
        CancelReminders();
        if (!Enabled || AndroidNotificationCenter.UserPermissionToPost != PermissionStatus.Allowed)
            return;

        bool runInProgress = UserDataManager.Instance.Get<StageProgressData>().RunInProgress;
        string title = LocalizationManager.Instance.Get("픽셀 제너럴");
        string text  = LocalizationManager.Instance.Get(runInProgress
            ? "진행 중인 런이 있어요! 모험을 이어가 보세요."
            : "새로운 모험을 시작해봐요!");

        DateTime now = DateTime.Now;
        for (int day = 1; day <= ReminderCount; day++)
        {
            var notification = new AndroidNotification(title, text, ReminderTime(now, day))
            {
                ShouldAutoCancel = true,
                ShowInForeground = false,
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(
                notification, ChannelId, FirstId + day - 1);
        }
    }

    static void CancelReminders()
    {
        for (int i = 0; i < ReminderCount; i++)
            AndroidNotificationCenter.CancelNotification(FirstId + i);
    }
#endif

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void CheckReminderTime()
    {
        var now = new DateTime(2026, 9, 3, 23, 30, 0);
        Debug.Assert(ReminderTime(now, 1) == new DateTime(2026, 9, 4, 19, 0, 0));
        Debug.Assert(ReminderTime(now, 3) == new DateTime(2026, 9, 6, 19, 0, 0));
        Debug.Log("[AndroidLocalNotificationManager] Reminder time checks passed.");
    }
#endif
}
