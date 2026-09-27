using UnityEngine;

namespace SoulMercenaries
{
    // Every floor has its own look and feel: a signature colour filter over the screen, a tint on the dungeon
    // itself (ground, rock, lava), and how far the party sees — a dark mine shortens sight, a lit lake lengthens it.
    public sealed class SoulFloorTheme
    {
        public readonly int Floor;
        public readonly string Name;
        public readonly string Mood;
        public readonly Color Filter; // screen filter (alpha = how strong)
        public readonly Color Tint;   // multiplied onto ground, rock and lava
        public readonly float Vision; // multiplies the vision radius

        SoulFloorTheme(int floor, string name, string mood, Color filter, Color tint, float vision)
        {
            Floor = floor; Name = name; Mood = mood; Filter = filter; Tint = tint; Vision = vision;
        }

        static readonly SoulFloorTheme[] Floors =
        {
            new SoulFloorTheme(1, "광산", "푸른 결정 광맥이 영롱하게 빛나지만 갱도는 어둡다",
                new Color(.30f, .52f, .95f, .20f), new Color(.72f, .84f, 1f), .85f),
            new SoulFloorTheme(2, "이끼 동굴", "축축한 이끼와 포자, 발밑이 물러 습하다",
                new Color(.35f, .75f, .40f, .16f), new Color(.80f, .95f, .78f), .95f),
            new SoulFloorTheme(3, "무너진 갱도", "무너진 지주와 흙먼지가 시야를 흐린다",
                new Color(.80f, .62f, .32f, .18f), new Color(1f, .90f, .74f), .90f),
            new SoulFloorTheme(4, "지하 호수", "맑은 지하수가 빛을 반사해 멀리까지 보인다",
                new Color(.25f, .80f, .80f, .15f), new Color(.80f, 1f, 1f), 1.10f),
            new SoulFloorTheme(5, "용암 지대", "갈라진 바닥 사이로 용암이 흐르고 열기가 오른다",
                new Color(1f, .40f, .15f, .20f), new Color(1f, .82f, .70f), 1f),
            new SoulFloorTheme(6, "뼈 무덤", "쌓인 뼈와 마른 공기, 소리가 울린다",
                new Color(.85f, .82f, .70f, .17f), new Color(.92f, .90f, .84f), .90f),
            new SoulFloorTheme(7, "영혼 성소", "보랏빛 마력이 떠다니는 봉인된 성소",
                new Color(.60f, .35f, 1f, .20f), new Color(.86f, .78f, 1f), .95f),
            new SoulFloorTheme(8, "심연의 왕좌", "검붉은 심연, 빛이 거의 닿지 않는다",
                new Color(.55f, .08f, .20f, .26f), new Color(.70f, .58f, .62f), .75f),
        };

        public static int Count => Floors.Length;
        public static SoulFloorTheme For(int floor) => Floors[Mathf.Clamp(floor, 1, Floors.Length) - 1];
    }
}
