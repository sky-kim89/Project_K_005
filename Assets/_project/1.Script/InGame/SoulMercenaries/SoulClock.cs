using UnityEngine;

namespace SoulMercenaries
{
    // Village time: one real second is one game minute (a day is 24 real minutes). The dungeon portal in the
    // village square is always open; a party comes back six hours after it went in, and at midnight everyone is
    // thrown out (the dungeon starts over the next day). Down there time runs eight times faster: six hours up
    // here are 48 hours down there (one dungeon minute = one simulated second). Going in late in the evening
    // leaves less time below.
    public static class SoulClock
    {
        public const float DayMinutes = 24 * 60;
        public const float Start = 8 * 60;                  // a new company wakes at 08:00 on day 1
        public const float StayMinutes = 6 * 60;            // village minutes a party stays down
        public const float DungeonRate = 8;                 // dungeon minutes per village minute
        public const float WatchRate = 1 / DungeonRate;     // watching a party, the village slows to its pace

        public static int Day(float clock) => Mathf.FloorToInt(clock / DayMinutes) + 1;
        public static float TimeOfDay(float clock) => clock - (Day(clock) - 1) * DayMinutes;

        // Six hours after going in — midnight at the latest.
        public static float ReturnTime(float departed) => Mathf.Min(departed + StayMinutes, Day(departed) * DayMinutes);

        // Dungeon hours left for a party due back at `returnAt`.
        public static float DungeonHoursLeft(float clock, float returnAt) => Mathf.Max(0, returnAt - clock) * DungeonRate / 60;

        public static string HourMinute(float clock)
        {
            int minutes = Mathf.FloorToInt(TimeOfDay(clock));
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        public static string Text(float clock) => $"{Day(clock)}일차 {HourMinute(clock)}";
    }
}
