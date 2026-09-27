using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // What skills leave on the ground, drawn while it lasts: a trap or a charge of powder, a bubbling pool (poison,
    // lava, bog, ice), a sanctuary's turning circle of light, spirits wheeling round their caller.
    public sealed class SoulFieldFx : MonoBehaviour
    {
        sealed class Shown
        {
            public SpriteRenderer Ground, Glow;
            public readonly List<SpriteRenderer> Orbit = new List<SpriteRenderer>();
            public Sprite[] Frames;
            public SoulFxPalette Palette;
            public float Clock, Emit, Fade = 1;
            public bool Gone;
        }

        readonly Dictionary<SoulDungeonSession.SoulField, Shown> shown = new Dictionary<SoulDungeonSession.SoulField, Shown>();
        readonly HashSet<SoulDungeonSession.SoulField> live = new HashSet<SoulDungeonSession.SoulField>();

        static SoulFxPalette PaletteOf(SoulActiveSkillData skill)
        {
            string id = skill.SoulId ?? "";
            if (id.StartsWith("soul_acid_pool") || id.StartsWith("soul_bog_summon")) return SoulFxPalette.Poison;
            if (id.StartsWith("soul_ice_floor")) return SoulFxPalette.Ice;
            if (id.StartsWith("soul_lava_pool") || id.StartsWith("soul_split")) return SoulFxPalette.Fire;
            if (id.StartsWith("sanctuary")) return SoulFxPalette.Holy;
            if (id.StartsWith("pack_call")) return SoulFxPalette.Dark;
            if (id.StartsWith("soul_legion_of_dead")) return SoulFxPalette.Steel;
            return SoulSkillFx.PaletteOf(skill);
        }

        SpriteRenderer Make(string name, Sprite sprite, int order, bool glow)
        {
            var renderer = SoulFx.Renderer(name, transform, sprite, Color.white, order, glow);
            return renderer;
        }

        Shown Create(SoulDungeonSession.SoulField field)
        {
            var skill = field.Skill;
            var show = new Shown { Palette = PaletteOf(skill) };
            switch (skill.Field)
            {
                case SoulFieldKind.Trap:
                    show.Ground = Make("Trap", SoulSprites.Trap(false), SoulFx.Ground + 1, false);
                    if ((skill.SoulId ?? "").StartsWith("soul_dynamite")) show.Ground.color = new Color(1f, .6f, .5f);
                    break;
                case SoulFieldKind.Hazard:
                    show.Frames = SoulFxArt.Pool(show.Palette);
                    show.Ground = Make("Pool", show.Frames[0], SoulFx.Ground, false);
                    break;
                case SoulFieldKind.Sanctuary:
                    show.Frames = SoulFxArt.Sigil(SoulFxPalette.Holy);
                    show.Ground = Make("Sanctuary", show.Frames[0], SoulFx.Ground, true);
                    show.Glow = Make("Light", SoulFxArt.Glow, SoulFx.Ground - 1, true);
                    break;
                case SoulFieldKind.Spirit:
                    show.Frames = SoulSpiritArt.Frames(skill.SpiritLook);
                    show.Ground = Make("Spirit", show.Frames[0], SoulFx.Air, false);
                    show.Glow = Make("SpiritGlow", SoulFxArt.Glow, SoulFx.Air - 1, true);
                    break;
                case SoulFieldKind.Aura:
                    show.Frames = SoulFxArt.Nova(show.Palette);
                    show.Ground = Make("Ring", SoulFxArt.Nova(show.Palette)[2], SoulFx.Ground, true);
                    string id = skill.SoulId ?? "";
                    Sprite spirit = id.StartsWith("pack_call") ? SoulFxArt.Fang : id.StartsWith("soul_legion_of_dead") ? SoulFxArt.Shard : SoulFxArt.Flame(show.Palette)[0];
                    for (int i = 0; i < 4; i++) show.Orbit.Add(Make("Spirit", spirit, SoulFx.Air, true));
                    break;
            }
            return show;
        }

        static void Size(SpriteRenderer renderer, Vector3 at, float width, float height)
        {
            renderer.transform.position = at;
            var size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(width / Mathf.Max(.001f, size.x), height / Mathf.Max(.001f, size.y), 1);
        }

        // Every floor party's fields, in step with the session (visible ones only).
        public void Sync(IEnumerable<SoulDungeonSession> sessions, System.Func<Vector2, bool> seen)
        {
            live.Clear();
            foreach (var session in sessions)
                foreach (var field in session.Fields)
                {
                    live.Add(field);
                    if (!shown.ContainsKey(field)) shown[field] = Create(field);
                }
            float dt = SoulFx.Delta;
            var drop = new List<SoulDungeonSession.SoulField>();
            foreach (var entry in shown)
            {
                var field = entry.Key;
                var show = entry.Value;
                if (!live.Contains(field)) show.Gone = true;
                if (show.Gone) show.Fade -= dt * 3;
                if (show.Fade <= 0) { Clear(show); drop.Add(field); continue; }
                show.Clock += dt;
                bool visible = seen == null || seen(field.Position);
                Vector3 at = SoulWorldView.ToWorld(field.Position);
                float radius = field.Radius, alpha = Mathf.Clamp01(show.Fade) * Mathf.Clamp01(field.Remaining * 2 + (show.Gone ? 1 : 0));
                var pal = SoulFxArt.Colors(show.Palette);
                if (field.Skill.Field == SoulFieldKind.Spirit)
                {
                    // hovering at its caller's side, bobbing, softly lit
                    Vector3 spot = at + Vector3.up * (.55f + .08f * Mathf.Sin(show.Clock * 3f + field.Seat));
                    show.Ground.enabled = show.Glow.enabled = visible;
                    show.Ground.sprite = show.Frames[Mathf.FloorToInt(show.Clock * 2.5f) % show.Frames.Length];
                    Size(show.Ground, spot, .95f, .8f);
                    show.Ground.color = new Color(1, 1, 1, alpha);
                    Size(show.Glow, spot, 1.3f, 1.1f);
                    show.Glow.color = new Color(pal[1].r, pal[1].g, pal[1].b, (.35f + .1f * Mathf.Sin(show.Clock * 4)) * alpha);
                    if (visible && !show.Gone && (show.Emit -= dt) <= 0)
                    {
                        show.Emit = .25f;
                        SoulFx.Particles(transform, SoulFxArt.Mote, spot + new Vector3(Random.Range(-.3f, .3f), -.2f, 0), 1, .15f, pal[0], pal[2], .7f, -.5f, .05f, 90, 20);
                    }
                    continue;
                }
                if (show.Ground != null)
                {
                    show.Ground.enabled = visible;
                    if (show.Frames != null) show.Ground.sprite = show.Frames[Mathf.FloorToInt(show.Clock * 8) % show.Frames.Length];
                    if (field.Skill.Field == SoulFieldKind.Trap) Size(show.Ground, at, .8f, .8f);
                    else Size(show.Ground, at, radius * 2, radius * 2 * SoulFx.GroundSquash);
                    var c = show.Ground.color; c.a = field.Skill.Field == SoulFieldKind.Aura ? .35f * alpha : alpha; show.Ground.color = c;
                }
                if (show.Glow != null)
                {
                    show.Glow.enabled = visible;
                    Size(show.Glow, at + Vector3.up * .2f, radius * 2.4f, radius * 1.4f);
                    show.Glow.color = new Color(pal[1].r, pal[1].g, pal[1].b, (.3f + .1f * Mathf.Sin(show.Clock * 3)) * alpha);
                }
                for (int i = 0; i < show.Orbit.Count; i++)
                {
                    var spirit = show.Orbit[i];
                    spirit.enabled = visible;
                    float a = show.Clock * 3.2f + i * Mathf.PI / 2;
                    Vector3 spot = at + new Vector3(Mathf.Cos(a) * radius * .8f, .35f + Mathf.Sin(a) * radius * .8f * SoulFx.GroundSquash, 0);
                    Size(spirit, spot, .28f, .28f * spirit.sprite.bounds.size.y / Mathf.Max(.001f, spirit.sprite.bounds.size.x));
                    spirit.transform.rotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 90);
                    spirit.color = new Color(pal[1].r, pal[1].g, pal[1].b, alpha);
                }
                // what rises from it
                if (!visible || show.Gone) continue;
                if ((show.Emit -= dt) > 0) continue;
                switch (field.Skill.Field)
                {
                    case SoulFieldKind.Hazard:
                        show.Emit = .22f;
                        var bit = show.Palette == SoulFxPalette.Ice ? SoulFxArt.Flake : show.Palette == SoulFxPalette.Fire ? SoulFxArt.Ember : SoulFxArt.Bubble;
                        SoulFx.Particles(transform, bit, at + new Vector3(Random.Range(-radius, radius) * .7f, Random.Range(-radius, radius) * .35f, 0), 1, .3f,
                            pal[0], pal[2], .9f, -.8f, .1f, 90, 30, 0, SoulFx.Air, show.Palette != SoulFxPalette.Poison);
                        break;
                    case SoulFieldKind.Sanctuary:
                        show.Emit = .15f;
                        SoulFx.Particles(transform, SoulFxArt.Mote, at + new Vector3(Random.Range(-radius, radius) * .7f, Random.Range(-radius, radius) * .35f, 0), 1, .2f,
                            pal[0], pal[2], 1.1f, -1.2f, .08f, 90, 20);
                        break;
                    case SoulFieldKind.Aura:
                        show.Emit = .12f;
                        SoulFx.Particles(transform, SoulFxArt.Ember, at + Vector3.up * .3f, 2, 1.4f, pal[0], pal[3], .4f, 0, .06f);
                        break;
                    case SoulFieldKind.Trap:
                        show.Emit = .9f; // a spark now and then: armed
                        SoulFx.Particles(transform, SoulFxArt.Spark, at + Vector3.up * .15f, 1, .2f, new Color(1f, .9f, .5f), new Color(1f, .4f, .1f), .3f, 0, .07f);
                        break;
                }
            }
            foreach (var field in drop) shown.Remove(field);
        }

        static void Clear(Shown show)
        {
            if (show.Ground != null) Destroy(show.Ground.gameObject);
            if (show.Glow != null) Destroy(show.Glow.gameObject);
            foreach (var spirit in show.Orbit) if (spirit != null) Destroy(spirit.gameObject);
        }
    }
}
