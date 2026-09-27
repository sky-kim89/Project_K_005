using System;
using TMPro;
using UnityEngine;

namespace SoulMercenaries
{
    // Small procedural pixel sprites for the dungeon view (walls, markers, bars). 16 PPU like PixelHeroes.
    public static class SoulSprites
    {
        static Sprite pixel, disc, ring, diamond;
        static readonly Sprite[] rocks = new Sprite[4];

        static Sprite lava, door, chest, chestOpen;
        static readonly System.Collections.Generic.Dictionary<int, Sprite> floorGrounds = new System.Collections.Generic.Dictionary<int, Sprite>();
        static readonly System.Collections.Generic.Dictionary<int, Sprite[]> floorWalls = new System.Collections.Generic.Dictionary<int, Sprite[]>();

        // Each floor's own ground (a seamless 128 px texture: 8×8 cells) and wall blocks (a 64×16 sheet of four
        // 16 px variants), from Resources/SoulFloors. Null: none for that floor (the old look is used).
        public static Sprite FloorGround(int floor)
        {
            if (floorGrounds.TryGetValue(floor, out var sprite)) return sprite;
            var texture = Resources.Load<Texture2D>($"SoulFloors/floor{floor}_ground");
            if (texture != null) texture.wrapMode = TextureWrapMode.Repeat;
            sprite = texture != null ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 16, 0, SpriteMeshType.FullRect) : null;
            return floorGrounds[floor] = sprite;
        }

        // ── 8-direction walls (47-tile blob autotile) ──
        // A wall cell's look comes from which of its 8 neighbours are rock too (bits: N 1, NE 2, E 4, SE 8, S 16,
        // SW 32, W 64, NW 128). A corner only counts when both sides next to it are rock, which leaves 47 shapes.
        // The atlas (Resources/SoulFloors/floor{n}_walls, 8 columns, top row first) holds the 47 in ascending
        // mask order, then again for each further variant.
        public const int WallN = 1, WallNE = 2, WallE = 4, WallSE = 8, WallS = 16, WallSW = 32, WallW = 64, WallNW = 128, WallShapes = 47;
        static int[] blobIndex;

        public static int ReduceWallMask(int mask)
        {
            int reduced = mask & (WallN | WallE | WallS | WallW);
            if ((mask & WallN) != 0 && (mask & WallE) != 0) reduced |= mask & WallNE;
            if ((mask & WallS) != 0 && (mask & WallE) != 0) reduced |= mask & WallSE;
            if ((mask & WallS) != 0 && (mask & WallW) != 0) reduced |= mask & WallSW;
            if ((mask & WallN) != 0 && (mask & WallW) != 0) reduced |= mask & WallNW;
            return reduced;
        }

        static int BlobIndex(int mask)
        {
            if (blobIndex == null)
            {
                var shapes = new System.Collections.Generic.SortedSet<int>();
                for (int m = 0; m < 256; m++) shapes.Add(ReduceWallMask(m));
                var order = new System.Collections.Generic.List<int>(shapes);
                blobIndex = new int[256];
                for (int m = 0; m < 256; m++) blobIndex[m] = order.IndexOf(ReduceWallMask(m));
            }
            return blobIndex[mask & 255];
        }

        // The floor's wall piece for this neighbour mask; null when the floor has no wall art.
        public static Sprite FloorWall(int floor, int mask, int variant)
        {
            if (!floorWalls.TryGetValue(floor, out var sprites))
            {
                var texture = Resources.Load<Texture2D>($"SoulFloors/floor{floor}_walls");
                sprites = null;
                if (texture != null)
                {
                    int columns = texture.width / 16, count = columns * (texture.height / 16);
                    count -= count % WallShapes;
                    sprites = new Sprite[count];
                    for (int i = 0; i < count; i++)
                        sprites[i] = Sprite.Create(texture, new Rect(i % columns * 16, texture.height - (i / columns + 1) * 16, 16, 16), new Vector2(.5f, .5f), 16, 0, SpriteMeshType.FullRect);
                }
                floorWalls[floor] = sprites;
            }
            if (sprites == null || sprites.Length == 0) return null;
            int variants = sprites.Length / WallShapes;
            return sprites[BlobIndex(mask) + WallShapes * (Mathf.Abs(variant) % variants)];
        }
        static readonly System.Collections.Generic.Dictionary<int, Sprite> cones = new System.Collections.Generic.Dictionary<int, Sprite>();

        // Wedge of `angle` degrees pointing +x, radius 1 unit, pivot at the apex (telegraphed cone attacks).
        public static Sprite Cone(float angle)
        {
            int key = Mathf.Clamp(Mathf.RoundToInt(angle), 1, 360);
            if (cones.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            const int size = 128;
            float half = key / 2f;
            sprite = Make(size, size, (x, y) =>
            {
                float dx = x + .5f - size / 2f, dy = y + .5f - size / 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > size / 2f) return Color.clear;
                return Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg) <= half ? Color.white : Color.clear;
            }, new Vector2(.5f, .5f), size / 2f);
            cones[key] = sprite;
            return sprite;
        }

        // Treasure chest dropped by a boss: wooden box, gold bands and lock; the open one shows a dark inside.
        public static Sprite Chest => chest != null ? chest : chest = MakeChest(false);
        static readonly System.Collections.Generic.Dictionary<SoulObjectKind, Sprite> objects = new System.Collections.Generic.Dictionary<SoulObjectKind, Sprite>();
        static Sprite trap, trapSprung;

        // Usable dungeon objects, 16 px, pivot near the bottom so they sort with units.
        public static Sprite Object(SoulObjectKind kind)
        {
            if (objects.TryGetValue(kind, out var sprite) && sprite != null) return sprite;
            Color stone = new Color(.42f, .41f, .44f), dark = new Color(.16f, .15f, .17f);
            Func<int, int, Color> paint;
            switch (kind)
            {
                case SoulObjectKind.Spring: // stone rim, blue water, a sparkle
                    paint = (x, y) =>
                    {
                        float dx = x - 7.5f, dy = (y - 5f) * 1.6f, d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > 7.5f) return Color.clear;
                        if (d > 5.8f) return stone;
                        return x == 5 && y == 6 ? Color.white : Color.Lerp(new Color(.35f, .75f, 1f), new Color(.1f, .35f, .8f), d / 6f);
                    };
                    break;
                case SoulObjectKind.Campfire: // two logs, a flame
                    paint = (x, y) =>
                    {
                        if (y <= 3 && Mathf.Abs((x - 7.5f) - (y - 1.5f) * 2) < 1.6f) return new Color(.45f, .27f, .12f);
                        if (y <= 3 && Mathf.Abs((x - 7.5f) + (y - 1.5f) * 2) < 1.6f) return new Color(.38f, .22f, .1f);
                        float w = (13 - y) * .45f;
                        if (y >= 3 && y <= 13 && Mathf.Abs(x - 7.5f) < w) return y < 7 || Mathf.Abs(x - 7.5f) < w * .5f ? new Color(1f, .85f, .3f) : new Color(1f, .45f, .1f);
                        return Color.clear;
                    };
                    break;
                case SoulObjectKind.Blessing: // pedestal, golden diamond
                    paint = (x, y) =>
                    {
                        if (y <= 4 && x >= 4 && x <= 11) return y == 4 ? stone * 1.2f : stone;
                        float d = Mathf.Abs(x - 7.5f) + Mathf.Abs(y - 10f);
                        return d <= 5 ? Color.Lerp(Color.white, new Color(1f, .78f, .25f), d / 5f) : Color.clear;
                    };
                    break;
                case SoulObjectKind.Altar: // dark stone block, violet glow
                    paint = (x, y) =>
                    {
                        if (y <= 7 && x >= 2 && x <= 13) return y == 7 ? new Color(.75f, .45f, 1f) : x == 2 || x == 13 ? dark : new Color(.27f, .22f, .32f);
                        float d = Mathf.Abs(x - 7.5f) * .8f + Mathf.Abs(y - 11f);
                        return d < 4 ? new Color(.8f, .5f, 1f, 1 - d / 4f) : Color.clear;
                    };
                    break;
                case SoulObjectKind.Warp: // swirling cyan ring
                    paint = (x, y) =>
                    {
                        float dx = x - 7.5f, dy = (y - 6f) * 1.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > 7.5f) return Color.clear;
                        float swirl = Mathf.Sin(Mathf.Atan2(dy, dx) * 3 + d * 1.3f);
                        return d > 5.5f ? new Color(.2f, .9f, 1f) : swirl > .3f ? new Color(.5f, 1f, 1f, .9f) : new Color(.05f, .2f, .35f, .9f);
                    };
                    break;
                case SoulObjectKind.Watchtower: // wooden tower
                    paint = (x, y) =>
                    {
                        if (y >= 11 && y <= 13 && x >= 3 && x <= 12) return new Color(.55f, .36f, .18f);
                        if (y == 14 && x >= 5 && x <= 10) return new Color(.8f, .2f, .15f);
                        if (y < 11 && (x == 4 || x == 11 || (x + y) % 5 == 0 && x > 4 && x < 11)) return new Color(.45f, .28f, .13f);
                        return Color.clear;
                    };
                    break;
                case SoulObjectKind.Suspicious: // a chest with a crooked lid and a red eye in the keyhole
                    paint = (x, y) =>
                    {
                        if (x < 1 || x > 14 || y < 1 || y > 12) return Color.clear;
                        if (x == 1 || x == 14 || y == 1 || y == 12 || y == 8) return new Color(.18f, .1f, .2f);
                        if ((x == 7 || x == 8) && y == 7) return new Color(1f, .2f, .2f);
                        return y > 8 ? new Color(.45f, .28f, .42f) : new Color(.38f, .22f, .34f);
                    };
                    break;
                case SoulObjectKind.Escape: // a violet gate on a stone step: the way home
                    paint = (x, y) =>
                    {
                        if (y <= 1 && x >= 1 && x <= 14) return stone;
                        float dx = x - 7.5f, dy = (y - 8f) * .85f, d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > 7f || y < 2) return Color.clear;
                        if (d > 5.6f) return new Color(.55f, .45f, .75f);
                        float swirl = Mathf.Sin(Mathf.Atan2(dy, dx) * 2 - d * 1.1f);
                        return swirl > .2f ? new Color(.85f, .7f, 1f, .95f) : new Color(.25f, .12f, .45f, .95f);
                    };
                    break;
                default: // fortune idol: golden head on a stone base
                    paint = (x, y) =>
                    {
                        if (y <= 3 && x >= 4 && x <= 11) return stone;
                        if (y >= 4 && y <= 8 && x >= 6 && x <= 9) return new Color(.85f, .65f, .2f);
                        float dx = x - 7.5f, dy = y - 11.5f;
                        return dx * dx + dy * dy <= 10 ? (x == 6 && y == 12 ? dark : new Color(1f, .8f, .3f)) : Color.clear;
                    };
                    break;
            }
            sprite = Make(16, 16, paint, new Vector2(.5f, .25f), 16);
            objects[kind] = sprite;
            return sprite;
        }

        // A floor trap: grey spikes on a plate; a sprung one stained red.
        public static Sprite Trap(bool sprung)
        {
            if (sprung ? trapSprung != null : trap != null) return sprung ? trapSprung : trap;
            var made = Make(16, 16, (x, y) =>
            {
                if (y < 3 || y > 12 || x < 2 || x > 13) return Color.clear;
                bool spike = (x % 4 == 1 || x % 4 == 2) && (y % 4 == 1 || y % 4 == 2);
                return spike ? (sprung ? new Color(.7f, .15f, .12f) : new Color(.75f, .75f, .78f)) : new Color(.2f, .19f, .21f, .8f);
            }, new Vector2(.5f, .5f), 16);
            if (sprung) trapSprung = made; else trap = made;
            return made;
        }
        public static Sprite ChestOpen => chestOpen != null ? chestOpen : chestOpen = MakeChest(true);

        static Sprite MakeChest(bool open) => Make(16, 16, (x, y) =>
        {
            if (x < 1 || x > 14 || y < 1 || y > (open ? 13 : 12)) return y == 0 && x > 1 && x < 14 ? new Color(0, 0, 0, .35f) : Color.clear;
            bool edge = x == 1 || x == 14 || y == 1 || y == (open ? 13 : 12);
            if (edge) return new Color(.2f, .12f, .06f);
            if (open && y >= 9) return y == 9 ? new Color(.95f, .78f, .3f) : new Color(.12f, .08f, .05f);
            if (!open && y == 8) return new Color(.2f, .12f, .06f);
            if (x == 4 || x == 11) return new Color(.95f, .78f, .3f);
            if (!open && (x == 7 || x == 8) && (y == 7 || y == 8 || y == 9)) return new Color(1f, .9f, .45f);
            return y > 8 ? new Color(.62f, .38f, .18f) : new Color(.5f, .3f, .14f);
        }, new Vector2(.5f, .3f), 16);

        // Molten tile: dark crust with glowing cracks (walk ✗, sight ✓).
        public static Sprite Lava => lava != null ? lava : lava = Make(16, 16, (x, y) =>
        {
            float wave = Mathf.Sin(x * .9f + y * .45f) + Mathf.Sin(y * 1.3f - x * .35f);
            if (wave > 1.2f) return new Color(1f, .85f, .35f);
            if (wave > .5f) return new Color(.95f, .45f, .12f);
            return new Color(.45f, .12f, .06f);
        }, new Vector2(.5f, .5f), 16);

        // Found hidden door: a wooden door set in the rock (walk ✓, sight ✗).
        public static Sprite Door => door != null ? door : door = Make(16, 16, (x, y) =>
        {
            if (x < 2 || x > 13 || y > 14) return new Color(.3f, .29f, .28f);
            if (x == 2 || x == 13 || y == 14) return new Color(.18f, .12f, .08f);
            if (x == 10 && y == 7) return new Color(1f, .85f, .4f);
            return (x % 4 == 0) ? new Color(.35f, .22f, .12f) : new Color(.52f, .34f, .18f);
        }, new Vector2(.5f, .5f), 16);

        public static Sprite Pixel => pixel != null ? pixel : pixel = Make(1, 1, (x, y) => Color.white, new Vector2(.5f, .5f), 1);
        public static Sprite Disc => disc != null ? disc : disc = Make(32, 32, (x, y) => Circle(x, y, 32, 0, 1), new Vector2(.5f, .5f), 32);
        public static Sprite Ring => ring != null ? ring : ring = Make(32, 32, (x, y) => Circle(x, y, 32, 3, 1), new Vector2(.5f, .5f), 32);
        public static Sprite Diamond => diamond != null ? diamond : diamond = Make(16, 16, (x, y) =>
        {
            float d = Mathf.Abs(x - 7.5f) + Mathf.Abs(y - 7.5f);
            if (d > 7.5f) return Color.clear;
            return d > 6f ? new Color(.45f, .3f, .05f) : Color.Lerp(Color.white, new Color(1, .8f, .25f), d / 6f);
        }, new Vector2(.5f, .5f), 16);

        // A boulder filling one map cell, shaded from the top-left with a dark outline.
        public static Sprite Rock(int variant)
        {
            variant = Mathf.Abs(variant) % rocks.Length;
            if (rocks[variant] != null) return rocks[variant];
            var seed = new System.Random(variant * 7919 + 17);
            float rx = 7.2f + (float)seed.NextDouble() * .7f, ry = 6.6f + (float)seed.NextDouble() * .9f;
            var noise = new float[16 * 16];
            for (int i = 0; i < noise.Length; i++) noise[i] = (float)seed.NextDouble();
            Color light = new Color(.62f, .60f, .56f), mid = new Color(.44f, .42f, .40f), dark = new Color(.25f, .24f, .24f);
            rocks[variant] = Make(16, 16, (x, y) =>
            {
                float nx = (x - 7.5f) / rx, ny = (y - 7.8f) / ry;
                float d = nx * nx + ny * ny;
                if (d > 1f) return y < 3 && d < 1.35f ? new Color(0, 0, 0, .35f) : Color.clear; // contact shadow
                if (d > .78f) return new Color(.14f, .13f, .13f);
                float shade = Mathf.Clamp01(.55f + (ny - nx) * .45f) + (noise[y * 16 + x] - .5f) * .18f;
                return shade > .72f ? light : shade > .42f ? mid : dark;
            }, new Vector2(.5f, .5f), 16);
            return rocks[variant];
        }

        static Color Circle(int x, int y, int size, float thickness, float alpha)
        {
            float r = size / 2f, d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r));
            float outer = Mathf.Clamp01(r - d);
            float inner = thickness > 0 ? Mathf.Clamp01(d - (r - thickness)) : 1;
            return new Color(1, 1, 1, outer * inner * alpha);
        }

        static Sprite Make(int width, int height, Func<int, int, Color> paint, Vector2 pivot, float ppu)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) texture.SetPixel(x, y, paint(x, y));
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, ppu, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        public static SpriteRenderer Renderer(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        public static TextMeshPro WorldText(string name, Transform parent, string text, float size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(4, 1);
            tmp.sortingOrder = order;
            tmp.outlineWidth = .25f;
            tmp.outlineColor = new Color32(0, 0, 0, 220);
            return tmp;
        }
    }

    // Visual-only projectile: the hit is already resolved by the session, this just shows where it came from.
    public sealed class SoulProjectileFx : MonoBehaviour
    {
        Vector3 from, to;
        float duration, age;

        public static void Spawn(Transform parent, Sprite sprite, Vector3 from, Vector3 to, float speed = 14f)
        {
            if (sprite == null) return;
            var renderer = SoulSprites.Renderer("Projectile", parent, sprite, Color.white, 850);
            var fx = renderer.gameObject.AddComponent<SoulProjectileFx>();
            fx.from = from; fx.to = to;
            fx.duration = Mathf.Max(.08f, Vector3.Distance(from, to) / speed);
            Vector3 direction = to - from;
            renderer.transform.SetPositionAndRotation(from, Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
        }

        void Update()
        {
            age += Time.deltaTime;
            transform.position = Vector3.Lerp(from, to, age / duration);
            if (age >= duration) Destroy(gameObject);
        }
    }

    // Expanding ring for area hits (leap shockwave).
    public sealed class SoulRingFx : MonoBehaviour
    {
        const float Life = .45f;
        SpriteRenderer ring;
        float radius, age;

        public static void Spawn(Transform parent, Vector3 position, float radius)
        {
            var renderer = SoulSprites.Renderer("Shockwave", parent, SoulSprites.Ring, new Color(1f, .85f, .5f, .9f), 850);
            renderer.transform.position = position;
            var fx = renderer.gameObject.AddComponent<SoulRingFx>();
            fx.ring = renderer;
            fx.radius = radius;
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Life);
            transform.localScale = new Vector3(2, 1, 1) * radius * Mathf.Lerp(.2f, 1f, 1 - (1 - t) * (1 - t));
            ring.color = new Color(1f, .85f, .5f, .9f * (1 - t));
            if (age >= Life) Destroy(gameObject);
        }
    }

    // Damage numbers and short callouts that rise and fade.
    // The summoned spirits' frames (Resources/SoulSpirits: "fire_0", "fire_1", …), by look.
    public static class SoulSpiritArt
    {
        static System.Collections.Generic.Dictionary<string, Sprite[]> frames;
        public static Sprite[] Frames(string look)
        {
            if (frames == null)
            {
                var found = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.SortedDictionary<string, Sprite>>();
                foreach (var sprite in Resources.LoadAll<Sprite>("SoulSpirits"))
                {
                    int cut = sprite.name.LastIndexOf('_');
                    string key = cut > 0 ? sprite.name.Substring(0, cut) : sprite.name;
                    if (!found.TryGetValue(key, out var list)) found[key] = list = new System.Collections.Generic.SortedDictionary<string, Sprite>();
                    list[sprite.name] = sprite;
                }
                frames = new System.Collections.Generic.Dictionary<string, Sprite[]>();
                foreach (var entry in found) frames[entry.Key] = new System.Collections.Generic.List<Sprite>(entry.Value.Values).ToArray();
            }
            if (look != null && frames.TryGetValue(look, out var shown)) return shown;
            foreach (var any in frames.Values) return any;
            return new[] { SoulFxArt.Glow };
        }
    }

    public sealed class SoulFloatingText : MonoBehaviour
    {
        const float Life = .9f;
        TextMeshPro text;
        Color color;
        float age;

        public static void Spawn(Transform parent, Vector3 position, string message, Color color, float size = 4f)
        {
            var tmp = SoulSprites.WorldText("Float", parent, message, size, color, 900);
            tmp.transform.position = position + new Vector3(UnityEngine.Random.Range(-.15f, .15f), 0, 0);
            var floating = tmp.gameObject.AddComponent<SoulFloatingText>();
            floating.text = tmp;
            floating.color = color;
        }

        void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * (1.1f * Time.deltaTime * (1 - age / Life));
            text.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(2 * (1 - age / Life)));
            if (age >= Life) Destroy(gameObject);
        }
    }
}
