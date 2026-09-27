using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    // A combatant's statuses and lasting buffs drawn on its own body, at its own size: stone or ice laid over its
    // silhouette (a sprite mask of the live body frame), a block of ice around it, flames licking up, stars round the
    // head, a dread wisp, a spiral, a seal, a shackle at the feet … Everything lives in the body's sorting group, so the
    // unit in front still hides it. Tint: how the body itself is coloured (grey stone, blue ice, a faded shadow).
    public sealed class SoulStatusFx : MonoBehaviour
    {
        SoulUnitView view;
        SpriteRenderer body;
        SpriteMask mask;
        SpriteRenderer skin, frost, glow, iceBlock, shackle, seal, swirl;
        readonly List<SpriteRenderer> stars = new List<SpriteRenderer>(), flames = new List<SpriteRenderer>(),
            wisps = new List<SpriteRenderer>(), arrows = new List<SpriteRenderer>();
        float clock, drip, bubble, ember;

        public Color Tint { get; private set; } = Color.white;

        public void Setup(SoulUnitView owner)
        {
            view = owner;
            body = owner.BodyRenderer;
        }

        SpriteRenderer Part(string name, Sprite sprite, int above, bool masked = false)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(body.transform, false);
            renderer.sprite = sprite;
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = body.sortingOrder + above;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            if (masked) renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            renderer.enabled = false;
            return renderer;
        }

        void Build()
        {
            mask = new GameObject("StatusMask").AddComponent<SpriteMask>();
            mask.transform.SetParent(body.transform, false);
            mask.alphaCutoff = .15f;
            mask.enabled = false;
            skin = Part("Skin", SoulFxArt.Tint, 3, true);
            skin.drawMode = SpriteDrawMode.Tiled;
            frost = Part("Frost", SoulFxArt.FrostSkin, 4, true);
            frost.drawMode = SpriteDrawMode.Tiled;
            glow = Part("Aura", SoulFxArt.Glow, -1);
            iceBlock = Part("IceBlock", SoulFxArt.IceBlock, 8);
            shackle = Part("Shackle", SoulFxArt.Shackle, -2);
            seal = Part("Seal", SoulFxArt.Seal, 9);
            swirl = Part("Swirl", SoulFxArt.Swirl[0], 9);
            for (int i = 0; i < 3; i++) stars.Add(Part("Star", SoulFxArt.Star, 9));
            for (int i = 0; i < 3; i++) flames.Add(Part("Flame", SoulFxArt.Flame(SoulFxPalette.Fire)[0], 6));
            for (int i = 0; i < 2; i++) wisps.Add(Part("Wisp", SoulFxArt.Wisp[0], 7));
            for (int i = 0; i < 2; i++) arrows.Add(Part("Weak", SoulFxArt.DownArrow, 7));
        }

        // A sprite placed in the world (its size in world units), whatever the flipped, scaled body it hangs under.
        static void Place(SpriteRenderer part, Vector3 world, float width, float height = -1)
        {
            part.transform.position = world;
            var parent = part.transform.parent.lossyScale;
            var size = part.sprite.bounds.size;
            float sx = width / Mathf.Max(.001f, size.x * Mathf.Abs(parent.x));
            float sy = (height > 0 ? height : width * size.y / Mathf.Max(.001f, size.x)) / Mathf.Max(.001f, size.y * Mathf.Abs(parent.y));
            part.transform.localScale = new Vector3(sx * Mathf.Sign(parent.x), sy, 1);
            part.transform.rotation = Quaternion.identity;
        }

        static bool Buffed(SoulCombatant unit, params string[] ids)
        {
            foreach (var buff in unit.TimedBuffs)
                foreach (string id in ids)
                    if (buff.Id.StartsWith(id)) return true;
            return false;
        }

        void Update()
        {
            var unit = view != null ? view.Unit : null;
            if (unit == null || body == null) return;
            bool show = !view.Dead && !view.Hidden;
            bool petrify = show && (unit.Has(SoulStatus.Petrify) || Buffed(unit, "soul_statue"));
            bool freeze = show && unit.Has(SoulStatus.Freeze);
            bool chill = show && unit.Has(SoulStatus.Chill);
            bool burn = show && unit.Has(SoulStatus.Burn);
            bool poison = show && unit.Has(SoulStatus.Poison);
            bool bleed = show && unit.Has(SoulStatus.Bleed);
            bool stun = show && unit.Has(SoulStatus.Stun);
            bool fear = show && unit.Has(SoulStatus.Fear);
            bool confuse = show && unit.Has(SoulStatus.Confuse);
            bool slow = show && unit.Has(SoulStatus.Slow);
            bool weaken = show && unit.Has(SoulStatus.Weaken);
            bool silence = show && unit.Has(SoulStatus.Silence);
            bool bubbleWard = show && Buffed(unit, "mana_shield", "soul_dragon_scale");
            bool rage = show && Buffed(unit, "frenzy", "soul_demon_pact", "soul_blood_pact", "soul_beastform", "soul_war_frenzy");
            bool steel = show && Buffed(unit, "iron_wall", "soul_bone_shield", "bastion", "soul_crystal_wall", "guardian_cry", "soul_crystal_reflect");
            bool holy = show && Buffed(unit, "soul_light_barrier", "prayer", "battle_focus");
            bool shadow = show && Buffed(unit, "soul_shadow_hide");
            bool any = petrify || freeze || chill || burn || poison || bleed || stun || fear || confuse || slow || weaken || silence || bubbleWard || rage || steel || holy || shadow;
            if (mask == null && !any) { Tint = Color.white; return; }
            if (mask == null) Build();

            float dt = SoulFx.Delta;
            clock += dt;
            float height = view.BodyHeight, width = view.BodyWidth;
            Vector3 feet = view.transform.position, head = view.HeadPosition;
            float pulse = .5f + .5f * Mathf.Sin(clock * 6);

            // the silhouette: the live body frame is the mask; a tiled skin over it
            var frame = body.sprite;
            mask.sprite = frame;
            mask.enabled = frame != null;
            Color skinColor = Color.clear;
            Sprite skinSprite = SoulFxArt.Tint;
            if (petrify) { skinSprite = SoulFxArt.StoneSkin; skinColor = Color.white; }
            else if (freeze) { skinSprite = SoulFxArt.IceSkin; skinColor = Color.white; }
            else if (steel) skinColor = new Color(.85f, .92f, 1f, .18f + .14f * pulse);
            else if (burn) skinColor = new Color(1f, .55f, .15f, .12f + .12f * pulse);
            else if (poison) skinColor = new Color(.45f, .95f, .25f, .12f + .1f * pulse);
            else if (rage) skinColor = new Color(1f, .2f, .15f, .1f + .12f * pulse);
            skin.enabled = frame != null && skinColor.a > 0;
            if (skin.enabled)
            {
                skin.sprite = skinSprite;
                skin.color = skinColor;
                skin.size = frame.bounds.size;
                skin.transform.localPosition = frame.bounds.center;
                skin.transform.localScale = Vector3.one;
                skin.transform.localRotation = Quaternion.identity;
            }
            frost.enabled = frame != null && (chill || freeze) && !petrify;
            if (frost.enabled)
            {
                frost.size = frame.bounds.size;
                frost.transform.localPosition = frame.bounds.center;
                frost.transform.localScale = Vector3.one;
                frost.color = new Color(1, 1, 1, freeze ? .55f : .3f + .15f * pulse);
            }

            // the body's own colour
            Color tint = Color.white;
            if (petrify) tint = new Color(.62f, .6f, .57f);
            else if (freeze) tint = new Color(.72f, .88f, 1.05f);
            else if (chill) tint = new Color(.86f, .95f, 1.08f);
            else if (burn) tint = new Color(1f, .82f + .1f * pulse, .7f);
            else if (poison) tint = new Color(.84f, 1f, .78f);
            if (weaken) tint *= new Color(.86f, .86f, .9f);
            if (shadow) tint.a = .42f;
            Tint = tint;

            // a block of ice, the body inside it
            iceBlock.enabled = freeze;
            if (freeze) Place(iceBlock, feet + Vector3.down * .04f, width * 1.7f, height * 1.1f);

            // an aura behind the body (a ward of water, a red rage, a holy light)
            glow.enabled = bubbleWard || rage || holy;
            if (glow.enabled)
            {
                glow.color = bubbleWard ? new Color(.45f, .75f, 1f, .45f + .15f * pulse) : rage ? new Color(1f, .25f, .15f, .4f + .2f * pulse) : new Color(1f, .9f, .5f, .35f + .15f * pulse);
                Place(glow, feet + Vector3.up * height * .48f, width * 3.2f, height * 1.5f);
            }

            // flames licking up from a burning body
            for (int i = 0; i < flames.Count; i++)
            {
                flames[i].enabled = burn;
                if (!burn) continue;
                var frames = SoulFxArt.Flame(SoulFxPalette.Fire);
                flames[i].sprite = frames[(Mathf.FloorToInt(clock * 12) + i) % frames.Length];
                float x = (i - 1) * width * .45f, y = height * (.15f + .22f * i) + Mathf.Sin(clock * 5 + i) * .03f;
                Place(flames[i], feet + new Vector3(x, y, 0), width * .45f);
            }

            // stars wheeling round a stunned head
            for (int i = 0; i < stars.Count; i++)
            {
                stars[i].enabled = stun;
                if (!stun) continue;
                float a = clock * 4 + i * Mathf.PI * 2 / 3;
                Place(stars[i], head + new Vector3(Mathf.Cos(a) * width * .75f, .12f + Mathf.Sin(a) * .07f, 0), .2f);
                stars[i].color = new Color(1f, .92f, .35f, Mathf.Sin(a) > 0 ? .7f : 1f);
                stars[i].sortingOrder = body.sortingOrder + (Mathf.Sin(a) > 0 ? -1 : 9); // behind the head on the far side
            }

            // dread: two purple wisps flanking the head
            for (int i = 0; i < wisps.Count; i++)
            {
                wisps[i].enabled = fear;
                if (!fear) continue;
                var frames = SoulFxArt.Wisp;
                wisps[i].sprite = frames[(Mathf.FloorToInt(clock * 10) + i * 2) % frames.Length];
                Place(wisps[i], head + new Vector3((i == 0 ? -1 : 1) * width * .8f, -.25f + Mathf.Sin(clock * 3 + i) * .05f, 0), .3f);
                wisps[i].color = new Color(1, 1, 1, .9f);
            }

            // weakened: arrows sinking by the shoulders
            for (int i = 0; i < arrows.Count; i++)
            {
                arrows[i].enabled = weaken;
                if (!weaken) continue;
                float sink = Mathf.Repeat(clock * .8f + i * .5f, 1);
                Place(arrows[i], feet + new Vector3((i == 0 ? -1 : 1) * width * .85f, height * (.85f - .45f * sink), 0), .16f);
                arrows[i].color = new Color(.75f, .7f, .95f, 1 - sink);
            }

            // confused: a spiral turning over the head · silenced: a chained seal
            swirl.enabled = confuse;
            if (confuse)
            {
                swirl.sprite = SoulFxArt.Swirl[Mathf.FloorToInt(clock * 10) % SoulFxArt.Swirl.Length];
                Place(swirl, head + Vector3.up * (silence ? .5f : .22f), .38f);
                swirl.color = new Color(.95f, .75f, 1f);
            }
            seal.enabled = silence;
            if (silence)
            {
                Place(seal, head + Vector3.up * .24f, .36f);
                seal.color = new Color(1f, .92f, .6f, .75f + .25f * pulse);
            }

            // slowed: a shackle on the ground round the feet
            shackle.enabled = slow;
            if (slow)
            {
                Place(shackle, feet, width * 2.4f, width * 2.4f * SoulFx.GroundSquash);
                shackle.color = new Color(.7f, .8f, 1f, .55f + .3f * pulse);
            }

            // what drips and rises: blood, poison bubbles, embers, frost
            if (bleed && (drip -= dt) <= 0)
            {
                drip = .35f;
                SoulFx.Particles(view.transform.parent, SoulFxArt.Drop, feet + new Vector3(Random.Range(-width, width) * .5f, height * Random.Range(.3f, .7f), 0),
                    1, .2f, new Color(.95f, .15f, .15f), new Color(.5f, .05f, .05f), .5f, 3f, .09f, -90, 30, 0, SoulFx.High, false);
            }
            if (poison && (bubble -= dt) <= 0)
            {
                bubble = .3f;
                SoulFx.Particles(view.transform.parent, SoulFxArt.Bubble, feet + new Vector3(Random.Range(-width, width) * .5f, height * Random.Range(.2f, .6f), 0),
                    1, .3f, new Color(.7f, 1f, .4f), new Color(.3f, .6f, .1f), .8f, -.9f, .1f, 90, 20, 0, SoulFx.High, false);
            }
            if ((burn || rage) && (ember -= dt) <= 0)
            {
                ember = .18f;
                SoulFx.Particles(view.transform.parent, SoulFxArt.Ember, feet + new Vector3(Random.Range(-width, width) * .5f, height * Random.Range(.2f, .8f), 0),
                    2, .4f, new Color(1f, .9f, .5f), new Color(1f, .3f, .1f), .6f, -1.4f, .06f, 90, 40);
            }
            if (chill && Random.value < dt * 3)
                SoulFx.Particles(view.transform.parent, SoulFxArt.Flake, feet + new Vector3(Random.Range(-width, width) * .6f, height * Random.Range(.4f, 1f), 0),
                    1, .2f, Color.white, new Color(.6f, .85f, 1f), .8f, .6f, .1f, -90, 40, 0, SoulFx.High, true, 1, true);
        }

        void OnDisable() { Tint = Color.white; }
    }
}
