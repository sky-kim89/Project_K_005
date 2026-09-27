using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // Sorting bands (UnitSortingSetup): the ground below the units, the air above them.
    public static class SoulFx
    {
        public const int Ground = 90, Air = 210, High = 230;

        static Material additive;
        static bool looked;
        // Glow and sparks add light (Resources/SoulFxAdditive); without it they blend as usual.
        public static Material Additive
        {
            get
            {
                if (!looked) { looked = true; additive = Resources.Load<Material>("SoulFxAdditive"); }
                return additive;
            }
        }

        // Effects keep up with a sped-up expedition (not all the way: ×8 would be a blur).
        public static float Speed => Mathf.Clamp(SoulExpeditionRunner.GameSpeed, 1f, 3f);
        public static float Delta => Time.deltaTime * Speed;

        // The ground is seen at a slant: a circle on it is half as tall as it is wide.
        public const float GroundSquash = .5f;

        public static SpriteRenderer Renderer(string name, Transform parent, Sprite sprite, Color color, int order, bool glow)
        {
            var renderer = SoulSprites.Renderer(name, parent, sprite, color, order);
            if (glow && Additive != null) renderer.sharedMaterial = Additive;
            return renderer;
        }

        // A frame animation: `size` world units across (the sprite's width), laid on the ground if `ground`.
        public static SoulFxAnim Anim(Transform parent, Sprite[] frames, Vector3 position, float size, float fps = 18, int order = Air,
            Color? color = null, bool glow = false, bool ground = false, float angle = 0, float loop = 0, Transform follow = null, float height = -1)
        {
            if (frames == null || frames.Length == 0) return null;
            var renderer = Renderer("Fx", parent, frames[0], color ?? Color.white, order, glow);
            float width = frames[0].bounds.size.x;
            float scale = size / Mathf.Max(.01f, width);
            float vertical = height > 0 ? height / Mathf.Max(.01f, frames[0].bounds.size.y) : scale;
            renderer.transform.position = position;
            renderer.transform.rotation = Quaternion.Euler(0, 0, angle);
            renderer.transform.localScale = new Vector3(scale, vertical * (ground ? GroundSquash : 1), 1);
            var anim = renderer.gameObject.AddComponent<SoulFxAnim>();
            anim.Setup(renderer, frames, fps, loop, follow, follow != null ? position - follow.position : Vector3.zero);
            return anim;
        }

        // A burst of particles: `count` of them, flung at `speed` (±40%) within `spread` degrees around `direction`
        // (360: every way), pulled by `gravity` (negative: they rise), each living `life` seconds.
        public static void Particles(Transform parent, Sprite sprite, Vector3 position, int count, float speed, Color from, Color to,
            float life = .6f, float gravity = 0, float size = .12f, float direction = 90, float spread = 360, float jitter = .1f,
            int order = High, bool glow = true, float drag = 1.5f, bool spin = false)
        {
            if (sprite == null || count <= 0) return;
            var host = new GameObject("Particles");
            host.transform.SetParent(parent, false);
            host.transform.position = position;
            var system = host.AddComponent<SoulFxParticles>();
            system.Setup(sprite, count, speed, from, to, life, gravity, size, direction, spread, jitter, order, glow, drag, spin);
        }
    }

    public sealed class SoulFxAnim : MonoBehaviour
    {
        SpriteRenderer renderer;
        Sprite[] frames;
        float fps, loop, age, fade = 1;
        Transform follow;
        Vector3 offset;
        Color color;

        public void Setup(SpriteRenderer target, Sprite[] sprites, float rate, float loopFor, Transform followed, Vector3 shift)
        {
            renderer = target; frames = sprites; fps = rate; loop = loopFor; follow = followed; offset = shift;
            color = target.color;
        }

        // Grows (or shrinks) while it plays: the scale multiplies from 1 to `to`.
        Vector3 baseScale; float growTo = 1;
        public SoulFxAnim Grow(float to) { baseScale = transform.localScale; growTo = to; return this; }

        public void Stop() => loop = 0;

        void Update()
        {
            age += SoulFx.Delta;
            if (follow != null) transform.position = follow.position + offset;
            float length = frames.Length / fps;
            int frame = Mathf.FloorToInt(age * fps);
            if (loop > 0 && age < loop) frame %= frames.Length;
            else if (loop > 0 && frame >= frames.Length) { fade -= SoulFx.Delta * 4; frame = frames.Length - 1; }
            if (loop <= 0 && frame >= frames.Length) { Destroy(gameObject); return; }
            renderer.sprite = frames[Mathf.Clamp(frame, 0, frames.Length - 1)];
            if (growTo != 1 && baseScale != Vector3.zero)
                transform.localScale = baseScale * Mathf.Lerp(1, growTo, Mathf.Clamp01(age / Mathf.Max(.01f, loop > 0 ? loop : length)));
            if (loop > 0 && age >= loop)
            {
                var c = color; c.a *= Mathf.Clamp01(fade); renderer.color = c;
                if (fade <= 0) Destroy(gameObject);
            }
        }
    }

    // A handful of particles, each its own little sprite: flung out, slowed by drag, pulled by gravity, fading from
    // one colour to another.
    public sealed class SoulFxParticles : MonoBehaviour
    {
        readonly List<Transform> bits = new List<Transform>();
        readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>();
        Vector3[] velocity;
        float[] lives, spins;
        float age, life, gravity, drag, size;
        Color from, to;

        public void Setup(Sprite sprite, int count, float speed, Color start, Color end, float lifetime, float pull, float scale, float direction,
            float spread, float jitter, int order, bool glow, float damping, bool spin)
        {
            from = start; to = end; life = lifetime; gravity = pull; drag = damping; size = scale;
            velocity = new Vector3[count]; lives = new float[count]; spins = new float[count];
            float spriteSize = Mathf.Max(.01f, sprite.bounds.size.x);
            for (int i = 0; i < count; i++)
            {
                var renderer = SoulFx.Renderer("Bit", transform, sprite, start, order, glow);
                float angle = (direction + Random.Range(-spread / 2, spread / 2)) * Mathf.Deg2Rad;
                float pace = speed * Random.Range(.6f, 1.4f);
                velocity[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * (spread >= 360 ? SoulFx.GroundSquash + .3f : 1), 0) * pace;
                renderer.transform.localPosition = new Vector3(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter) * .6f, 0);
                renderer.transform.localScale = Vector3.one * (scale / spriteSize) * Random.Range(.7f, 1.3f);
                if (spin) { renderer.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360f)); spins[i] = Random.Range(-360f, 360f); }
                else if (sprite == SoulFxArt.Arrow || sprite == SoulFxArt.Shard || sprite == SoulFxArt.Fang)
                    renderer.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(velocity[i].y, velocity[i].x) * Mathf.Rad2Deg);
                lives[i] = life * Random.Range(.7f, 1.15f);
                bits.Add(renderer.transform);
                sprites.Add(renderer);
            }
        }

        void Update()
        {
            float dt = SoulFx.Delta;
            age += dt;
            bool any = false;
            for (int i = 0; i < bits.Count; i++)
            {
                if (bits[i] == null) continue;
                float t = age / lives[i];
                if (t >= 1) { Destroy(bits[i].gameObject); bits[i] = null; continue; }
                any = true;
                velocity[i] *= Mathf.Exp(-drag * dt);
                velocity[i].y -= gravity * dt;
                bits[i].localPosition += velocity[i] * dt;
                if (spins[i] != 0) bits[i].Rotate(0, 0, spins[i] * dt);
                var c = Color.Lerp(from, to, t);
                c.a *= 1 - t * t;
                sprites[i].color = c;
            }
            if (!any) Destroy(gameObject);
        }
    }

    // A jagged bolt stretched from one point to another, flickering through its shapes, then gone.
    public sealed class SoulFxBolt : MonoBehaviour
    {
        SpriteRenderer renderer;
        Sprite[] frames;
        float age;
        const float Life = .28f;

        public static void Spawn(Transform parent, Vector3 from, Vector3 to, float thickness = .5f)
        {
            var frames = SoulFxArt.Bolt();
            var renderer = SoulFx.Renderer("Bolt", parent, frames[0], Color.white, SoulFx.High, true);
            Vector3 gap = to - from;
            renderer.transform.position = from;
            renderer.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(gap.y, gap.x) * Mathf.Rad2Deg);
            renderer.transform.localScale = new Vector3(gap.magnitude / frames[0].bounds.size.x, thickness / frames[0].bounds.size.y, 1);
            var bolt = renderer.gameObject.AddComponent<SoulFxBolt>();
            bolt.renderer = renderer; bolt.frames = frames;
            SoulFx.Particles(parent, SoulFxArt.Spark, to, 6, 2.5f, new Color(1, 1, .8f), new Color(.5f, .7f, 1f), .3f);
        }

        void Update()
        {
            age += SoulFx.Delta;
            renderer.sprite = frames[Mathf.FloorToInt(age * 30) % frames.Length];
            renderer.color = new Color(1, 1, 1, 1 - age / Life);
            if (age >= Life) Destroy(gameObject);
        }
    }
}
