using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // The elements an effect can be made of (skills pick one from their damage kind; statuses have their own).
    public enum SoulFxPalette { Fire, Ice, Lightning, Arcane, Holy, Dark, Poison, Water, Stone, Blood, Steel, Wind, Beast, Smoke }

    // Pixel-art effect frames, painted once in code and cached: explosions, rings, slashes, pillars, bolts, beams,
    // breaths, vortices, sigils, smoke, pools, particles — and the status overlays (ice, stone, flames …).
    // Everything is drawn in four shades of its palette, point-filtered, so it sits with the 16-PPU characters.
    public static class SoulFxArt
    {
        public const float Ppu = 32f; // effect pixels are half a character pixel: finer, still pixel art

        static readonly Dictionary<SoulFxPalette, Color[]> Palettes = new Dictionary<SoulFxPalette, Color[]>
        {
            { SoulFxPalette.Fire, Hex("FFF6C0", "FFB030", "F05010", "7A1C10") },
            { SoulFxPalette.Ice, Hex("FFFFFF", "B8F2FF", "58B4F0", "2452A0") },
            { SoulFxPalette.Lightning, Hex("FFFFFF", "FFF590", "A8D4FF", "4060C8") },
            { SoulFxPalette.Arcane, Hex("FFE8FF", "E488FF", "9444E0", "3E1C80") },
            { SoulFxPalette.Holy, Hex("FFFFFF", "FFF2A8", "F4C448", "A07018") },
            { SoulFxPalette.Dark, Hex("D8B0FF", "8844C8", "44146A", "140822") },
            { SoulFxPalette.Poison, Hex("E8FFB8", "98E444", "44A424", "145218") },
            { SoulFxPalette.Water, Hex("F2FFFF", "88D4FF", "3486E4", "123E84") },
            { SoulFxPalette.Stone, Hex("F2EAD4", "B4A484", "746452", "3E3830") },
            { SoulFxPalette.Blood, Hex("FFC8C8", "F44848", "A41424", "4E0812") },
            { SoulFxPalette.Steel, Hex("FFFFFF", "D8E6F4", "8C9CB8", "424C64") },
            { SoulFxPalette.Wind, Hex("FFFFFF", "E6F4FF", "A8C8E8", "6484A8") },
            { SoulFxPalette.Beast, Hex("FFF2D4", "E8A864", "A86430", "52300E") },
            { SoulFxPalette.Smoke, Hex("EEEEF0", "AAAAB2", "66666E", "303036") },
        };

        public static Color[] Colors(SoulFxPalette palette) => Palettes[palette];
        public static Color Main(SoulFxPalette palette) => Palettes[palette][1];

        static Color[] Hex(params string[] codes)
        {
            var colors = new Color[codes.Length];
            for (int i = 0; i < codes.Length; i++) ColorUtility.TryParseHtmlString("#" + codes[i], out colors[i]);
            return colors;
        }

        // A value 0..1 → one of the four shades (or nothing): the pixel-art look.
        static Color Shade(Color[] pal, float v, float alpha = 1)
        {
            if (v > .82f) return WithAlpha(pal[0], alpha);
            if (v > .6f) return WithAlpha(pal[1], alpha);
            if (v > .36f) return WithAlpha(pal[2], alpha);
            if (v > .16f) return WithAlpha(pal[3], alpha * .9f);
            return Color.clear;
        }

        static Color WithAlpha(Color c, float a) { c.a *= Mathf.Clamp01(a); return c; }

        // ── noise ──
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static float Noise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float x, float y, int seed) => Noise(x, y, seed) * .65f + Noise(x * 2.1f, y * 2.1f, seed + 7) * .35f;

        // ── cache ──
        static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        static Sprite[] Frames(string key, int width, int height, int frames, Vector2 pivot, Func<int, int, int, Color> paint, bool repeat = false)
        {
            if (cache.TryGetValue(key, out var hit)) return hit;
            var result = new Sprite[frames];
            for (int f = 0; f < frames; f++)
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset,
                };
                var pixels = new Color[width * height];
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) pixels[y * width + x] = paint(x, y, f);
                texture.SetPixels(pixels);
                texture.Apply();
                result[f] = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, Ppu, 0, SpriteMeshType.FullRect);
                result[f].hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            cache[key] = result;
            return result;
        }

        static readonly Vector2 Mid = new Vector2(.5f, .5f);
        static float Ease(float t) => 1 - (1 - t) * (1 - t);

        // ── explosions ──
        // A ball of the element: a white flash, a noisy fireball that swells, then breaks into holes and is gone.
        public static Sprite[] Blast(SoulFxPalette palette)
        {
            var pal = Colors(palette);
            const int n = 96, frames = 10;
            var smoke = Colors(SoulFxPalette.Smoke);
            return Frames("blast" + palette, n, n, frames, Mid, (x, y, f) =>
            {
                float t = f / (float)(frames - 1);
                float dx = (x - 47.5f) / 47.5f, dy = (y - 47.5f) / 47.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                float noise = Fbm(x * .11f + f * .55f, y * .11f - f * .4f, 11);
                float hot = Noise(x * .23f - f * .9f, y * .23f + f * .6f, 29);          // bright knots inside
                float radius = .2f + .74f * Ease(Mathf.Min(1, t * 1.35f));
                float v = (1 - r / radius) * 1.3f + (noise - .5f) * .75f;
                v *= 1.15f - t * .7f;
                v += (hot - .55f) * .5f * (1 - t);
                if (t < .3f) v += Mathf.Clamp01(1 - r / (.38f * (1 - t))) * (1 - t * 3); // the first white flash
                if (t > .45f && noise < (t - .45f) * 1.5f) v *= .2f;                      // it breaks up
                float alpha = 1 - Mathf.Max(0, t - .72f) * 2.8f;
                var c = Shade(pal, v, alpha);
                // a curl of smoke round the fire as it spends itself
                if (c.a <= 0 && t > .25f)
                {
                    float rim = 1 - Mathf.Abs(r - radius * .95f) / (.12f + t * .1f) + (noise - .5f) * .6f;
                    if (rim > .45f) return WithAlpha(smoke[rim > .75f ? 2 : 3], (.55f + .2f * rim) * alpha);
                }
                return c;
            });
        }

        // A ring that runs outward on the ground (drawn round; the ground squashes it).
        public static Sprite[] Nova(SoulFxPalette palette)
        {
            var pal = Colors(palette);
            const int n = 64, frames = 8;
            return Frames("nova" + palette, n, n, frames, Mid, (x, y, f) =>
            {
                float t = f / (float)(frames - 1);
                float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float radius = .18f + .8f * Ease(t), width = .2f * (1 - t) + .05f;
                float jag = (Noise(angle * 5 + 20, f * .8f, 3) - .5f) * .12f;
                float v = 1 - Mathf.Abs(r - radius + jag) / width;
                if (r < radius) v = Mathf.Max(v, (1 - t) * .5f * (r / radius) - .05f);
                return Shade(pal, v * (1.1f - t * .5f));
            });
        }

        // ── blades ──
        // A crescent swept from one end to the other, bright at its head, the tail fading; it points right.
        public static Sprite[] Slash(SoulFxPalette palette)
        {
            var pal = Colors(palette);
            const int n = 64, frames = 7;
            return Frames("slash" + palette, n, n, frames, Mid, (x, y, f) =>
            {
                float t = f / (float)(frames - 1);
                float dx = x - 20f, dy = y - 31.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; // -75 … 75 is the arc
                if (a < -80 || a > 80) return Color.clear;
                float head = -75 + 150 * Mathf.Min(1, t * 1.8f);
                if (a > head) return Color.clear;
                float along = Mathf.Clamp01(1 - (head - a) / 150f);
                float thick = 7.5f * Mathf.Sqrt(Mathf.Clamp01(1 - Mathf.Abs(a) / 78f)) * (1 - Mathf.Max(0, t - .55f) * 1.8f);
                float v = 1 - Mathf.Abs(r - 30f) / Mathf.Max(.5f, thick);
                v = v * (.45f + .7f * along) + (r > 30 ? .15f : 0);
                return Shade(pal, v);
            });
        }

        // A full turn of the blade around the body.
        public static Sprite[] Spin(SoulFxPalette palette)
        {
            var pal = Colors(palette);
            const int n = 64, frames = 8;
            return Frames("spin" + palette, n, n, frames, Mid, (x, y, f) =>
            {
                float t = f / (float)(frames - 1);
                float dx = x - 31.5f, dy = y - 31.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = (Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360) % 360;
                float head = 360 * Mathf.Min(1, t * 1.5f);
                float behind = (head - a + 360) % 360;
                if (behind > 300) return Color.clear;
                float along = 1 - behind / 300f;
                float thick = 6f * (.4f + .6f * along) * (1 - Mathf.Max(0, t - .6f) * 2f);
                float v = 1 - Mathf.Abs(r - 26f) / Mathf.Max(.5f, thick);
                return Shade(pal, v * (.4f + .75f * along));
            });
        }

        // ── columns and bolts ──
        public static Sprite[] Pillar(SoulFxPalette palette)
        {
            var pal = Colors(palette);
            const int w = 32, h = 112, frames = 8;
            return Frames("pillar" + palette, w, h, frames, new Vector2(.5f, .06f), (x, y, f) =>
            {
                float t = f / (float)(frames - 1);
                float width = 2 + 11 * Mathf.Sin(Mathf.PI * Mathf.Min(1, t * 1.2f));
                float streak = Noise(x * .7f, y * .06f - f * 1.3f, 5);
                float v = 1 - Mathf.Abs(x - 15.5f) / width + (streak - .5f) * .6f;
                v *= Mathf.Clamp01(1.25f - y / (float)h);                                   // thins toward the top
                float ground = 1 - new Vector2((x - 15.5f) / 15.5f, (y - 6f) / 5f).magnitude; // flare where it hits
                v = Mathf.Max(v, ground * (1.3f - t));
                return Shade(pal, v, 1 - Mathf.Max(0, t - .75f) * 3f);
            });
        }

        // A jagged bolt from left to right (three shapes), stretched between two points by the player.
        public static Sprite[] Bolt()
        {
            var pal = Colors(SoulFxPalette.Lightning);
            const int w = 128, h = 32, frames = 3;
            var paths = new float[frames][];
            var rng = new System.Random(7);
            for (int f = 0; f < frames; f++)
            {
                paths[f] = new float[w];
                float yv = 16, target = 16;
                for (int x = 0; x < w; x++)
                {
                    if (x % 9 == 0) target = 16 + (float)(rng.NextDouble() - .5) * (x < 6 || x > w - 8 ? 2 : 20);
                    yv = Mathf.Lerp(yv, target, .45f);
                    paths[f][x] = yv;
                }
            }
            return Frames("bolt", w, h, frames, new Vector2(0, .5f), (x, y, f) =>
            {
                float d = Mathf.Abs(y - paths[f][x]);
                if (d < .8f) return pal[0];
                if (d < 1.8f) return pal[1];
                if (d < 3.2f) return WithAlpha(pal[2], .8f);
                if (d < 5f) return WithAlpha(pal[3], .45f);
                return Color.clear;
            });
        }

        // A straight streak with a bright head at the right (arrows, spears, bolts of force, a wave).
        public static Sprite[] Beam(SoulFxPalette palette)
        {
            var pal = Colors(palette);
            const int w = 96, h = 20, frames = 6;
            return Frames("beam" + palette, w, h, frames, new Vector2(0, .5f), (x, y, f) =>
            {
                float t = f / (float)(frames - 1);
                float thick = 6.5f * (1 - t) + 1.2f;
                float streak = Noise(x * .12f - f * 1.7f, y * .5f, 9);
                float v = 1 - Mathf.Abs(y - 9.5f) / thick + (streak - .5f) * .5f;
                v *= .55f + .6f * (x / (float)w);                        // brighter toward the head
                if (x > w - 10) v += (1 - Mathf.Abs(y - 9.5f) / 4f) * .6f; // the head
                return Shade(pal, v, 1 - Mathf.Max(0, t - .6f) * 2.5f);
            });
        }

        // A breath fanning out to the right from the left edge: tongues of the element rushing outward.
        public static Sprite[] Cone(SoulFxPalette palette)
        {
            var pal = Colors(palette);
            const int w = 72, h = 64, frames = 7;
            return Frames("cone" + palette, w, h, frames, new Vector2(0, .5f), (x, y, f) =>
            {
                float t = f / (float)(frames - 1);
                float dx = x, dy = y - 31.5f, d = Mathf.Sqrt(dx * dx + dy * dy) / w;
                float angle = Mathf.Abs(Mathf.Atan2(dy, Mathf.Max(.1f, dx)) * Mathf.Rad2Deg);
                if (angle > 32) return Color.clear;
                float reach = Ease(Mathf.Min(1, t * 1.6f));
                if (d > reach) return Color.clear;
                float flame = Fbm(x * .14f - f * 1.8f, y * .18f, 13);
                float edge = angle / 32f;                                   // 0 on the axis, 1 at the rim
                float v = (1 - edge) * .55f + (1 - d) * .45f + (flame - .5f) * .8f;
                v += Mathf.Clamp01(1 - angle / 9f) * (1 - d) * .45f;        // a white-hot core along the axis
                v -= edge * edge * .35f;                                    // the rim burns dark
                if (t > .55f) v -= (t - .55f) * 2f;                         // it dies away
                return Shade(pal, v, 1 - Mathf.Max(0, t - .7f) * 2.5f);
            });
        }

        // Arms spiralling in (whirlpools, rifts, confusion).
        public static Sprite[] Vortex(SoulFxPalette palette)
        {
            var pal = Colors(palette);
            const int n = 64, frames = 8;
            return Frames("vortex" + palette, n, n, frames, Mid, (x, y, f) =>
            {
                float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > 1) return Color.clear;
                float angle = Mathf.Atan2(dy, dx);
                float arm = Mathf.Sin(3 * angle + r * 9 - f * (Mathf.PI * 2 / frames) * 2);
                float v = (arm * .5f + .5f) * (1 - r) * 1.5f + (1 - r * 3) * .6f;
                return Shade(pal, v);
            });
        }

        // A magic circle: two rings, runes, a star; it turns (drawn round, laid on the ground).
        public static Sprite[] Sigil(SoulFxPalette palette)
        {
            var pal = Colors(palette);
            const int n = 64, frames = 6;
            return Frames("sigil" + palette, n, n, frames, Mid, (x, y, f) =>
            {
                float dx = x - 31.5f, dy = y - 31.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) + f * (Mathf.PI / 3f / frames);
                float v = 0;
                if (Mathf.Abs(r - 29) < 1.3f) v = .9f;
                else if (Mathf.Abs(r - 22) < .9f) v = .7f;
                else if (r > 22.5f && r < 28.5f)
                {
                    float rune = Mathf.Repeat(angle * 16 / (Mathf.PI * 2), 1);
                    if (rune < .35f && Hash(Mathf.FloorToInt(angle * 16 / (Mathf.PI * 2) + 64), Mathf.FloorToInt(r), 3) > .45f) v = .65f;
                }
                else if (r < 21.5f)
                {
                    // a five-pointed star, turning with the rings: distance to its five chords
                    var local = Rotate(new Vector2(dx, dy), -f * (Mathf.PI / 3f / frames));
                    for (int k = 0; k < 5; k++)
                    {
                        float a1 = Mathf.PI / 2 + k * Mathf.PI * 2 / 5, a2 = a1 + Mathf.PI * 4 / 5;
                        var p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * 21; var p2 = new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * 21;
                        if (DistanceToSegment(local, p1, p2) < .9f) v = Mathf.Max(v, .62f);
                    }
                }
                float pulse = .85f + .15f * Mathf.Sin(f * Mathf.PI * 2 / frames);
                return Shade(pal, v * pulse);
            });
        }

        static Vector2 Rotate(Vector2 v, float a) => new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        // Billows of smoke that swell and thin out.
        public static Sprite[] Puff(SoulFxPalette palette)
        {
            var pal = Colors(palette);
            const int n = 64, frames = 8;
            return Frames("puff" + palette, n, n, frames, Mid, (x, y, f) =>
            {
                float t = f / (float)(frames - 1);
                float v = 0;
                for (int k = 0; k < 6; k++)
                {
                    float a = k * 1.05f + .4f, spread = 6 + 16 * Ease(t);
                    float cx = 31.5f + Mathf.Cos(a) * spread, cy = 31.5f + Mathf.Sin(a) * spread * .8f + t * 6;
                    float rad = 9 + 6 * Ease(t);
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    v = Mathf.Max(v, 1 - d / rad);
                }
                float noise = Fbm(x * .2f, y * .2f + f * .4f, 21);
                v = v * 1.2f + (noise - .5f) * .6f - t * .55f;
                return Shade(pal, v, 1 - Mathf.Max(0, t - .6f) * 2.2f);
            });
        }

        // A pool on the ground (flat oval) that bubbles (loops).
        public static Sprite[] Pool(SoulFxPalette palette)
        {
            var pal = Colors(palette);
            const int w = 64, h = 32, frames = 6;
            return Frames("pool" + palette, w, h, frames, Mid, (x, y, f) =>
            {
                float dx = (x - 31.5f) / 31.5f, dy = (y - 15.5f) / 15.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = Fbm(Mathf.Atan2(dy, dx) * 2 + 10, 3, 17) * .18f;
                if (r > .92f - edge) return Color.clear;
                float swirl = Fbm(x * .12f + f * .35f, y * .24f - f * .2f, 31);
                float v = .35f + swirl * .45f + (r > .78f - edge ? .25f : 0);
                // bubbles: a few spots that rise and pop in turn
                for (int k = 0; k < 5; k++)
                {
                    int phase = (f + k * 2) % frames;
                    float bx = 10 + Hash(k, 1, 5) * 44, by = 6 + Hash(k, 2, 5) * 18;
                    float br = phase < 3 ? 1 + phase : 0;
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(bx, by));
                    if (br > 0 && Mathf.Abs(d - br) < .7f) v = .95f;
                }
                return Shade(pal, v, .92f);
            });
        }

        // A soft round glow (not quantized) for auras and flashes.
        public static Sprite Glow => Frames("glow", 32, 32, 1, Mid, (x, y, f) =>
        {
            float r = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(16, 16)) / 16f;
            float v = Mathf.Clamp01(1 - r);
            return new Color(1, 1, 1, v * v);
        })[0];

        // ── particles (white: tinted by the player) ──
        public static Sprite Spark => Frames("p_spark", 5, 5, 1, Mid, (x, y, f) => (x == 2 || y == 2) ? new Color(1, 1, 1, x == 2 && y == 2 ? 1 : .7f) : Color.clear)[0];
        public static Sprite Mote => Frames("p_mote", 4, 4, 1, Mid, (x, y, f) => new Color(1, 1, 1, (x == 0 || x == 3) && (y == 0 || y == 3) ? 0 : (x is 1 or 2) && (y is 1 or 2) ? 1 : .55f))[0];
        public static Sprite Ember => Frames("p_ember", 2, 2, 1, Mid, (x, y, f) => Color.white)[0];
        public static Sprite Flake => Frames("p_flake", 7, 7, 1, Mid, (x, y, f) =>
            (x == 3 || y == 3 || x == y || x == 6 - y) && !(x == 3 && y == 3 && false) ? new Color(1, 1, 1, (x == 3 && y == 3) ? 1 : .8f) : Color.clear)[0];
        public static Sprite Bubble => Frames("p_bubble", 6, 6, 1, Mid, (x, y, f) =>
        {
            float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(3, 3));
            return d > 2.1f && d < 3f ? Color.white : (x == 2 && y == 3 ? Color.white : Color.clear);
        })[0];
        public static Sprite Drop => Frames("p_drop", 3, 6, 1, Mid, (x, y, f) =>
            (y <= 2 && (x != 1 ? y >= 1 : true)) || (x == 1 && y <= 4) ? Color.white : Color.clear)[0];
        public static Sprite Star => Frames("p_star", 9, 9, 1, Mid, (x, y, f) =>
        {
            int dx = Mathf.Abs(x - 4), dy = Mathf.Abs(y - 4);
            return (dx == 0 && dy <= 4) || (dy == 0 && dx <= 4) || (dx == dy && dx <= 2) ? new Color(1, 1, 1, dx + dy <= 1 ? 1 : .85f) : Color.clear;
        })[0];
        public static Sprite Shard => Frames("p_shard", 5, 9, 1, Mid, (x, y, f) =>
            Mathf.Abs(x - 2) <= 2 - Mathf.Abs(y - 4) / 2 ? new Color(1, 1, 1, x == 2 ? 1 : .7f) : Color.clear)[0];
        public static Sprite Arrow => Frames("p_arrow", 11, 5, 1, Mid, (x, y, f) =>
            (y == 2 && x <= 8) || (x >= 8 && Mathf.Abs(y - 2) <= 10 - x) || (x <= 1 && (y == 1 || y == 3)) ? Color.white : Color.clear)[0];
        public static Sprite Rock => Frames("p_rock", 6, 6, 1, Mid, (x, y, f) =>
            Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(3, 3)) < 2.8f ? new Color(1, 1, 1, Hash(x, y, 4) > .3f ? 1 : .8f) : Color.clear)[0];
        public static Sprite Bat => Frames("p_bat", 9, 5, 1, Mid, (x, y, f) =>
            (y == 2 && x >= 3 && x <= 5) || (y == 3 && (x == 1 || x == 2 || x == 6 || x == 7)) || (y == 4 && (x == 0 || x == 8)) || (y == 1 && x == 4) ? Color.white : Color.clear)[0];
        public static Sprite Fang => Frames("p_fang", 5, 7, 1, Mid, (x, y, f) => Mathf.Abs(x - 2) <= y / 3 ? Color.white : Color.clear)[0];

        // ── status overlays ──
        // A block of ice a body stands in: faceted, see-through, a rim of white where the light catches it.
        public static Sprite IceBlock => Frames("s_iceblock", 36, 48, 1, new Vector2(.5f, 0), (x, y, f) =>
        {
            int w = 36, h = 48;
            bool rim = x == 0 || x == w - 1 || y == 0 || y == h - 1 || (x < 3 && y > h - 4) || (x > w - 4 && y > h - 4);
            if ((x < 2 || x > w - 3) && (y > h - 3)) return Color.clear; // bevelled corners
            float facet = ((x * 3 + y * 2) / 17) % 2 == 0 ? .1f : 0;
            bool shine = (x + y) % 23 == 0 || (x + y) % 23 == 1;
            if (rim) return new Color(.85f, .97f, 1f, .9f);
            if (shine && y > 8) return new Color(1, 1, 1, .75f);
            if (x == w / 2 + (y / 7) % 3 - 1 && y % 5 != 0) return new Color(.6f, .85f, 1f, .45f); // a crack
            return new Color(.55f + facet, .82f + facet, 1f, .34f + facet);
        })[0];

        // Tiling textures laid over the body's own silhouette (the unit's sprite mask).
        public static Sprite IceSkin => Frames("s_iceskin", 16, 16, 1, Mid, (x, y, f) =>
        {
            float n = Fbm(x * .35f, y * .35f, 41);
            bool line = Mathf.Abs((x - y * .6f) % 7) < .6f || Mathf.Abs((x + y * .8f + 3) % 9) < .5f;
            return line ? new Color(1, 1, 1, .8f) : new Color(.62f + n * .3f, .88f, 1f, .62f);
        }, true)[0];

        public static Sprite StoneSkin => Frames("s_stoneskin", 16, 16, 1, Mid, (x, y, f) =>
        {
            float n = Fbm(x * .45f, y * .45f, 43);
            bool crack = Mathf.Abs(Noise(x * .3f, y * .3f, 47) - .5f) < .045f;
            float g = .42f + n * .32f;
            return crack ? new Color(.18f, .16f, .14f, .95f) : new Color(g, g * .96f, g * .9f, .9f);
        }, true)[0];

        public static Sprite FrostSkin => Frames("s_frostskin", 16, 16, 1, Mid, (x, y, f) =>
            Hash(x, y, 49) > .82f ? new Color(1, 1, 1, .85f) : new Color(.7f, .9f, 1f, .25f), true)[0];

        public static Sprite Tint => Frames("s_tint", 4, 4, 1, Mid, (x, y, f) => Color.white, true)[0];

        // Little flames (loop) that lick up from a burning body.
        public static Sprite[] Flame(SoulFxPalette palette)
        {
            var pal = Colors(palette);
            const int w = 10, h = 16, frames = 4;
            return Frames("s_flame" + palette, w, h, frames, new Vector2(.5f, 0), (x, y, f) =>
            {
                float dx = (x - 4.5f) / 4.5f, ty = y / (float)h;
                float width = (1 - ty) * (1 - ty * .3f);
                float wobble = (Noise(y * .4f - f * 1.4f, f, 51) - .5f) * .5f;
                float v = 1 - Mathf.Abs(dx - wobble * ty) / Mathf.Max(.05f, width);
                v = v * (1.1f - ty * .6f) + (Noise(x * .6f, y * .5f - f, 53) - .5f) * .3f;
                return Shade(pal, v);
            });
        }

        // A spiral (confusion) and a chain seal (silence), both small, over the head.
        public static Sprite[] Swirl => Frames("s_swirl", 16, 16, 4, Mid, (x, y, f) =>
        {
            float dx = x - 7.5f, dy = y - 7.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Atan2(dy, dx) + f * Mathf.PI / 2;
            float spiral = Mathf.Repeat(a / (Mathf.PI * 2) * 3.5f - r * .45f, 1);
            return r < 7.5f && spiral < .28f ? new Color(1, 1, 1, 1 - r / 9) : Color.clear;
        });

        public static Sprite Seal => Frames("s_seal", 15, 15, 1, Mid, (x, y, f) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(7, 7));
            bool ring = Mathf.Abs(d - 6.3f) < .8f;
            bool cross = (Mathf.Abs(x - y) <= 0 || Mathf.Abs(x + y - 14) <= 0) && d < 6;
            bool link = cross && (x + y) % 3 != 0;
            return ring ? Color.white : link ? new Color(1, 1, 1, .9f) : Color.clear;
        })[0];

        public static Sprite DownArrow => Frames("s_down", 7, 8, 1, Mid, (x, y, f) =>
            (x == 3 && y >= 3) || (y <= 3 && Mathf.Abs(x - 3) <= y) ? Color.white : Color.clear)[0];

        // A shackle ring laid on the ground (둔화).
        public static Sprite Shackle => Frames("s_shackle", 32, 32, 1, Mid, (x, y, f) =>
        {
            float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(16, 16));
            bool ring = Mathf.Abs(d - 13) < 1.6f;
            bool link = ring && Mathf.Repeat(Mathf.Atan2(y - 16, x - 16) * 12 / (Mathf.PI * 2), 1) < .6f;
            return link ? Color.white : ring ? new Color(1, 1, 1, .45f) : Color.clear;
        })[0];

        // A wisp of dread (공포): a small purple flame that wavers.
        public static Sprite[] Wisp => Flame(SoulFxPalette.Dark);
    }
}
