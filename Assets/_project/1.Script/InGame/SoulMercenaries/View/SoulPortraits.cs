using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // Mercenary portraits, drawn by the game's GeneralPortraitProvider from the look the stats build (race, look,
    // gear, souls) — the idle frame, untrimmed, so SoulUi.Portrait can frame the face. Views poll Version to
    // redraw when a portrait arrives; a changed look is a new cache key, so it is drawn again.
    public static class SoulPortraits
    {
        public static int Version { get; private set; }
        static readonly HashSet<string> pending = new HashSet<string>();

        public static Sprite For(SoulMercenary hero)
        {
            if (hero == null) return null;
            var sprite = Get("soul:" + hero.Id, hero.Stats.Appearance);
            if (sprite != null) shapes[sprite] = Shape(hero);
            return sprite;
        }

        // Height and weight in a portrait (as in the world, SoulUnitView.BodyShape): each mercenary's portrait
        // remembers its body; the figure is scaled with it about the middle of the frame, so the face stays in view —
        // a tall one fills more of it, a short one less, a heavy one is wider.
        static readonly Dictionary<Sprite, Vector2> shapes = new Dictionary<Sprite, Vector2>();
        public static Vector2 Shape(SoulCombatant unit) => unit != null ? SoulUnitView.BodyShape(unit.Stats.Height, unit.Stats.Weight) : Vector2.one;
        public static Vector2 ShapeOf(Sprite sprite) => sprite != null && shapes.TryGetValue(sprite, out var shape) ? shape : Vector2.one;

        // A figure image anchored at its feet `feet` below a frame `frame` tall (the dungeon's live portraits): sized to
        // the unit's body about the frame's middle.
        public static void Fit(UnityEngine.UI.Image figure, SoulCombatant unit, float feet, float frame)
        {
            if (figure == null) return;
            var shape = Shape(unit);
            Place(figure.rectTransform, shape, feet, frame);
        }

        // Scales a feet-anchored figure (pivot at its bottom, `feet` = its y) by `shape`, keeping the point at the frame's
        // middle where it was.
        public static void Place(RectTransform figure, Vector2 shape, float feet, float frame)
        {
            figure.localScale = new Vector3(shape.x * shape.y, shape.x, 1);
            float about = shape.x < 1 ? .35f : 1f; // shorter: mostly about the feet, or it floats (a dwarf)
            figure.anchoredPosition = new Vector2(0, feet + (1 - shape.x) * (frame * .5f - feet) * about);
        }

        // Someone no longer in the company (the codex): drawn from the look kept in its record.
        public static Sprite For(string id, string look)
            => string.IsNullOrEmpty(look) ? null : Get("record:" + id, JsonUtility.FromJson<UnitAppearanceData>(look));

        // The mercenary's attack motion (bow: Shot, staff / wand: Jab, otherwise Slash), frame by frame; null
        // until drawn (poll Version like a portrait).
        public static Sprite[] Attack(SoulMercenary hero)
        {
            var look = hero?.Stats.Appearance;
            if (look == null) return null;
            string key = "attack:" + hero.Id + "|" + JsonUtility.ToJson(look);
            var cached = GeneralPortraitProvider.GetCachedFrames(key);
            if (cached != null) return cached;
            if (pending.Add(key))
                GeneralPortraitProvider.RequestFrames(key, look, AttackClip(look.Weapon), 6, _ => { pending.Remove(key); Version++; });
            return null;
        }

        static string AttackClip(string weapon)
        {
            if (string.IsNullOrEmpty(weapon)) return "Slash";
            if (weapon.Contains("Bow")) return "Shot";
            foreach (string caster in new[] { "Wand", "Staff", "Stick", "Stuff", "Skepter" }) if (weapon.Contains(caster)) return "Jab";
            return "Slash";
        }

        static Sprite Get(string id, UnitAppearanceData look)
        {
            if (look == null) return null;
            string key = id + "|" + JsonUtility.ToJson(look);
            var cached = GeneralPortraitProvider.GetCached(key);
            if (cached != null) return cached;
            if (pending.Add(key))
                GeneralPortraitProvider.Request(key, look, false, null, _ => { pending.Remove(key); Version++; });
            return null;
        }
    }
}
