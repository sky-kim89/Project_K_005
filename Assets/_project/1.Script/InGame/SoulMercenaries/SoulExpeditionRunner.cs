using UnityEngine;

namespace SoulMercenaries
{
    // Keeps the village clock and every expedition going across scene loads (village, dungeon view). Only while
    // the game runs: closed or in the background, time stands still. It also carries the one AudioListener (the
    // scenes have none: without it nothing is heard and Unity warns every frame).
    public sealed class SoulExpeditionRunner : MonoBehaviour
    {
        // Longest frame the clock takes at once (a hitch or a return from the background does not jump ahead).
        const float MaxFrame = .25f;
        static SoulExpeditionRunner instance;
        // Clock multiplier (the cheat window; 1 in play).
        public static float Speed = 1f;
        // The player's game speed while watching a party in the dungeon (the HUD's button): 1, 2, 4 or 8. The village
        // itself always runs at 1 (watching slows it to the dungeon's pace, so at x8 it runs at its usual pace).
        public static readonly float[] GameSpeeds = { 1f, 2f, 4f, 8f };
        public static float GameSpeed = 1f;
        public static void NextGameSpeed()
        {
            int at = System.Array.IndexOf(GameSpeeds, GameSpeed);
            GameSpeed = GameSpeeds[(at + 1) % GameSpeeds.Length];
        }

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("SoulExpeditionRunner");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SoulExpeditionRunner>();
            if (FindAnyObjectByType<AudioListener>() == null) go.AddComponent<AudioListener>();
        }

        string shownLog;
        SoulCampaign shownCampaign;

        void Update()
        {
            var campaign = SoulCampaign.Current;
            if (campaign == null) return;
            float watching = campaign.Watching != null && !campaign.Watching.Settled ? GameSpeed : 1f;
            campaign.Advance(Mathf.Min(Time.unscaledDeltaTime, MaxFrame) * Speed * watching);
            // what just happened (the campaign's newest log line): a banner — not what was there before
            string latest = campaign.Log.Count > 0 ? campaign.Log[0] : null;
            if (campaign != shownCampaign) { shownCampaign = campaign; shownLog = latest; }
            else if (latest != shownLog) { shownLog = latest; SoulFeedback.Banner(latest); }
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Save();
            Debug.Log($"[ExitTimer] quit → save {watch.ElapsedMilliseconds} ms");
        }
        static void Save() { if (SoulCampaign.Current != null) SoulSave.Save(SoulCampaign.Current, immediate: true); }
    }
}
