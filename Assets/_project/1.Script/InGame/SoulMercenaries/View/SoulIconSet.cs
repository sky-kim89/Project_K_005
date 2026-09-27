using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // The UI's two 9-sliced frames (panel, button). Icons are looked up in SpriteManager (the game's sprite store,
    // Atlas_SoulMercenaries): a key like "stat:Strength" is the sprite "stat_Strength".
    [CreateAssetMenu(menuName = "Soul Mercenaries/Icon Set")]
    public sealed class SoulIconSet : ScriptableObject
    {
        [Tooltip("9-sliced rounded sprites from PixelFantasy UI (panels, buttons).")]
        public Sprite Panel, Button;

        static SoulIconSet current;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static SoulIconSet Current => current != null ? current : current = Resources.Load<SoulIconSet>(nameof(SoulIconSet));

        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key) || SpriteManager.Instance == null) return null;
            if (!cache.TryGetValue(key, out var sprite)) cache[key] = sprite = SpriteManager.Instance.Get(key.Replace(':', '_'));
            return sprite;
        }

        public static Sprite Stat(StatType stat) => Get("stat:" + stat) ?? (stat == StatType.SpellPower ? Get("stat:" + StatType.Magic) : null); // 마법 위력: the magic icon
        public static Sprite Status(SoulStatus status) => Get("status:" + status);
        public static Sprite Category(SoulPatternCategory category) => Get("category:" + category);
        public static Sprite Ui(string name) => Get("ui:" + name);

        // A job's badge (job names are Korean; the sprites go by these keys).
        public static string JobKey(string job)
        {
            switch (job)
            {
                case "검사": return "swordsman";
                case "수호자": return "guardian";
                case "투사": return "fighter";
                case "길잡이": return "pathfinder";
                case "마법사": return "mage";
                case "성직자": return "priest";
                case "궁수": return "archer";
                case "소환사": return "summoner";
                default: return null;
            }
        }
        public static Sprite Job(string job) { string key = JobKey(job); return key != null ? Get("job:" + key) : null; }
    }
}
