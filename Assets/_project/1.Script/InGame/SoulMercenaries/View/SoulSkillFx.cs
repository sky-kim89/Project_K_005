using System;
using UnityEngine;

namespace SoulMercenaries
{
    // What a skill looks like when it goes off (SkillFx events): its shape from what it does (a blade, a ring, a blast,
    // a column, a beam, a breath …) and its colours from its element — a few signature skills drawn by hand.
    public static class SoulSkillFx
    {
        public enum Look { Slash, Spin, Nova, Blast, Pillar, Bolt, Beam, Arrow, Cone, Vortex, Sigil, Puff, Rain, Meteor, Shout, Ward, Heal, Drain, Leap, Tongue, Field, Throw }

        public struct Style
        {
            public Look Look;
            public SoulFxPalette Palette;
            public float Scale;  // a signature skill drawn bigger
            public Style(Look look, SoulFxPalette palette, float scale = 1) { Look = look; Palette = palette; Scale = scale; }
        }

        static string Base(SoulActiveSkillData skill)
        {
            string id = string.IsNullOrEmpty(skill.SoulId) ? skill.name : skill.SoulId;
            return id.EndsWith("_plus") ? id.Substring(0, id.Length - 5) : id;
        }

        public static SoulFxPalette PaletteOf(SoulActiveSkillData skill)
        {
            if ((skill.SoulId ?? "").StartsWith("summon_light")) return SoulFxPalette.Holy;
            switch (skill.DamageKind)
            {
                case SoulDamageKind.Fire: case SoulDamageKind.Burn: return SoulFxPalette.Fire;
                case SoulDamageKind.Cold: case SoulDamageKind.Frostbite: return SoulFxPalette.Ice;
                case SoulDamageKind.Lightning: return SoulFxPalette.Lightning;
                case SoulDamageKind.Arcane: return SoulFxPalette.Arcane;
                case SoulDamageKind.Poison: return SoulFxPalette.Poison;
                case SoulDamageKind.Bleed: return SoulFxPalette.Blood;
                case SoulDamageKind.Stone: return SoulFxPalette.Stone;
                case SoulDamageKind.Wind: return SoulFxPalette.Wind;
            }
            return skill.DamageSchool == SoulDamageSchool.Magic ? SoulFxPalette.Arcane : SoulFxPalette.Steel;
        }

        public static Style For(SoulActiveSkillData skill)
        {
            switch (Base(skill))
            {
                // signatures
                case "meteor": return new Style(Look.Meteor, SoulFxPalette.Fire, 1.3f);
                case "soul_arrow_rain": return new Style(Look.Rain, SoulFxPalette.Steel);
                case "blizzard": return new Style(Look.Rain, SoulFxPalette.Ice);
                case "judgment": return new Style(Look.Pillar, SoulFxPalette.Holy);
                case "miracle": case "resurrection": return new Style(Look.Pillar, SoulFxPalette.Holy, 1.4f);
                case "soul_gods_wrath": return new Style(Look.Pillar, SoulFxPalette.Lightning, 1.2f);
                case "roar": return new Style(Look.Shout, SoulFxPalette.Beast);
                case "war_cry": return new Style(Look.Shout, SoulFxPalette.Fire);
                case "guardian_cry": case "bastion": return new Style(Look.Shout, SoulFxPalette.Steel);
                case "soul_wolf_howl": return new Style(Look.Shout, SoulFxPalette.Dark);
                case "soul_light_barrier": case "soul_crystal_wall": return new Style(Look.Shout, SoulFxPalette.Holy);
                case "soul_whirlpool": return new Style(Look.Vortex, SoulFxPalette.Water, 1.2f);
                case "soul_abyss_gate": return new Style(Look.Vortex, SoulFxPalette.Dark, 1.3f);
                case "confusion": return new Style(Look.Vortex, SoulFxPalette.Arcane, .7f);
                case "smoke_bomb": case "shortcut": return new Style(Look.Puff, SoulFxPalette.Smoke);
                case "soul_shadow_hide": case "soul_bat_form": return new Style(Look.Puff, SoulFxPalette.Dark);
                case "soul_plague_cloud": return new Style(Look.Puff, SoulFxPalette.Poison, 1.2f);
                case "soul_molt": return new Style(Look.Puff, SoulFxPalette.Beast, .7f);
                case "soul_tongue_lash": return new Style(Look.Tongue, SoulFxPalette.Blood);
                case "soul_blood_suck": case "soul_life_drain": return new Style(Look.Drain, SoulFxPalette.Blood);
                case "soul_soul_sap": return new Style(Look.Drain, SoulFxPalette.Arcane);
                case "soul_curse_doll": case "curse_weakness": case "soul_death_sentence": return new Style(Look.Sigil, SoulFxPalette.Dark);
                case "soul_seal": return new Style(Look.Sigil, SoulFxPalette.Holy);
                case "petrify_gaze": return new Style(Look.Sigil, SoulFxPalette.Stone);
                case "soul_agony_curse": return new Style(Look.Sigil, SoulFxPalette.Blood);
                case "slow_spell": return new Style(Look.Sigil, SoulFxPalette.Wind);
                case "frost_prison": case "frost_shard": return new Style(Look.Blast, SoulFxPalette.Ice);
                case "heaven_blade": return new Style(Look.Spin, SoulFxPalette.Steel, 1.3f);
                case "soul_tail_sweep": return new Style(Look.Spin, SoulFxPalette.Beast);
                case "issen": return new Style(Look.Slash, SoulFxPalette.Steel, 1.6f);
                case "execution": return new Style(Look.Slash, SoulFxPalette.Blood, 1.3f);
                case "soul_bone_breaker": return new Style(Look.Slash, SoulFxPalette.Stone, 1.2f);
                case "soul_paralyze_claw": return new Style(Look.Slash, SoulFxPalette.Poison);
                case "soul_pickpocket": case "rage_charge": return new Style(Look.Slash, SoulFxPalette.Beast);
                case "chain_lightning": return new Style(Look.Bolt, SoulFxPalette.Lightning);
                case "piercing_shot": case "alert_shot": return new Style(Look.Arrow, SoulFxPalette.Holy);
                case "soul_shadow_arrow": return new Style(Look.Arrow, SoulFxPalette.Dark);
                case "soul_bone_spear": return new Style(Look.Beam, SoulFxPalette.Steel);
                case "soul_tidal": return new Style(Look.Beam, SoulFxPalette.Water, 2f);
                case "soul_fire_breath": return new Style(Look.Cone, SoulFxPalette.Fire);
                case "soul_dragon_breath": return new Style(Look.Cone, SoulFxPalette.Fire, 1.3f);
                case "soul_hellfire_smash": case "soul_flame_burst": return new Style(Look.Blast, SoulFxPalette.Fire, 1.1f);
                case "soul_splash": return new Style(Look.Nova, SoulFxPalette.Water);
                case "soul_boulder_throw": return new Style(Look.Throw, SoulFxPalette.Stone);
                case "soul_slime_spit": return new Style(Look.Throw, SoulFxPalette.Poison);
                case "soul_dive_bomb": return new Style(Look.Leap, SoulFxPalette.Fire, 1.3f);
                case "soul_shadow_pounce": return new Style(Look.Leap, SoulFxPalette.Dark);
                case "mana_shield": case "soul_dragon_scale": return new Style(Look.Ward, SoulFxPalette.Water);
                case "frenzy": case "soul_demon_pact": case "soul_blood_pact": case "soul_beastform": case "soul_war_frenzy": return new Style(Look.Ward, SoulFxPalette.Blood);
                case "iron_wall": case "soul_statue": case "soul_bone_shield": case "taunt": case "soul_crystal_reflect": return new Style(Look.Ward, SoulFxPalette.Steel);
                case "battle_focus": case "soul_plunder": return new Style(Look.Ward, SoulFxPalette.Holy);
                case "soul_mud_roll": return new Style(Look.Ward, SoulFxPalette.Beast);
                case "soul_bone_rise": return new Style(Look.Ward, SoulFxPalette.Dark);
                case "soul_troll_regen": return new Style(Look.Heal, SoulFxPalette.Poison);
                case "soul_devour": return new Style(Look.Heal, SoulFxPalette.Blood);
                case "fire_enchant": return new Style(Look.Ward, SoulFxPalette.Fire);
                case "frost_enchant": return new Style(Look.Ward, SoulFxPalette.Ice);
                case "haste": return new Style(Look.Ward, SoulFxPalette.Wind);
            }
            // the rest, from what it does
            var palette = PaletteOf(skill);
            bool harm = SoulDungeonSession.Harmful(skill);
            bool blow = skill.Damage != null && (skill.Damage.Flat > 0 || (skill.Damage.Terms != null && skill.Damage.Terms.Length > 0));
            if (skill.Field != SoulFieldKind.None) return new Style(Look.Field, palette);
            if (!harm) return skill.Heal != null && (skill.Heal.Flat > 0 || (skill.Heal.Terms != null && skill.Heal.Terms.Length > 0)) || skill.ReviveRatio > 0
                ? new Style(Look.Heal, SoulFxPalette.Holy) : new Style(Look.Ward, SoulFxPalette.Holy);
            if (skill.LeapToTarget) return new Style(Look.Leap, SoulFxPalette.Stone);
            if (skill.Line) return SoulDungeonSession.Breath(skill) ? new Style(Look.Cone, palette) : new Style(Look.Beam, palette);
            if (skill.Chain > 0) return new Style(Look.Bolt, palette);
            if (skill.Burst)
            {
                if (skill.AtTarget) return new Style(Look.Blast, palette);
                if (!blow) return new Style(Look.Shout, palette);
                return skill.DamageKind == SoulDamageKind.Slash ? new Style(Look.Spin, palette) : new Style(Look.Nova, palette);
            }
            if (skill.Trigger == SoulTrigger.Cast) return blow ? new Style(Look.Blast, palette) : new Style(Look.Sigil, palette);
            if (!blow) return new Style(Look.Sigil, palette);
            return skill.DamageSchool == SoulDamageSchool.Physical ? new Style(Look.Slash, palette) : new Style(Look.Blast, palette, .7f);
        }

        static Vector3 World(Vector2 map) => SoulWorldView.ToWorld(map);
        static Vector3 WorldDir(Vector2 dir) => new Vector3(dir.x, -dir.y, 0);
        static float Angle(Vector3 dir) => Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        static Color Tone(SoulFxPalette palette, int shade) => SoulFxArt.Colors(palette)[shade];

        public static void Play(Transform fx, SoulCombatEvent evt, SoulUnitView actor, SoulUnitView target)
        {
            var skill = evt.Skill;
            if (skill == null || fx == null) return;
            var style = For(skill);
            switch (evt.Phase)
            {
                case SoulFxPhase.Link: Link(fx, style, actor, target, evt); break;
                case SoulFxPhase.Victim: Victim(fx, style, skill, evt, target); break;
                default: Land(fx, style, skill, evt, actor, target); break;
            }
        }

        // one touched: a spark of the element (a blow), a rising glitter (a heal or a blessing)
        static void Victim(Transform fx, Style style, SoulActiveSkillData skill, SoulCombatEvent evt, SoulUnitView target)
        {
            Vector3 at = target != null ? target.BodyPosition : World(evt.Point) + Vector3.up * .5f;
            var pal = style.Palette;
            if (style.Look == Look.Heal || style.Look == Look.Ward || style.Look == Look.Shout && !SoulDungeonSession.Harmful(skill))
            {
                SoulFx.Anim(fx, new[] { SoulFxArt.Glow }, at, 1.3f, 8, SoulFx.Air, WithAlpha(Tone(pal, 1), .55f), true, false, 0, .35f, target != null ? target.transform : null);
                SoulFx.Particles(fx, SoulFxArt.Mote, at + Vector3.down * .35f, 10, .9f, Tone(pal, 0), Tone(pal, 2), .8f, -1.6f, .09f, 90, 70, .3f);
                return;
            }
            if (style.Look == Look.Drain) return; // the stream to the caster says it
            SoulFx.Particles(fx, style.Look == Look.Slash || style.Look == Look.Spin ? SoulFxArt.Spark : SoulFxArt.Ember, at, 7, 2.6f, Tone(pal, 0), Tone(pal, 2), .35f, 2f, .1f);
            SoulFx.Anim(fx, new[] { SoulFxArt.Glow }, at, .8f, 8, SoulFx.Air, WithAlpha(Tone(pal, 1), .7f), true, false, 0, .12f);
        }

        // a chain's jump: a bolt from one body to the next
        static void Link(Transform fx, Style style, SoulUnitView from, SoulUnitView to, SoulCombatEvent evt)
        {
            // a spirit's bolt leaves the spirit (hovering beside its caller), not the caller
            Vector3 start = evt.Skill != null && evt.Skill.Field == SoulFieldKind.Spirit ? World(evt.Point) + Vector3.up * .55f : from != null ? from.BodyPosition : World(evt.Point);
            Vector3 end = to != null ? to.BodyPosition : World(evt.Point);
            SoulFxBolt.Spawn(fx, start, end, .55f);
            SoulFx.Anim(fx, new[] { SoulFxArt.Glow }, end, 1.2f, 8, SoulFx.High, new Color(.8f, .9f, 1f, .8f), true, false, 0, .15f);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        static void Land(Transform fx, Style style, SoulActiveSkillData skill, SoulCombatEvent evt, SoulUnitView actor, SoulUnitView target)
        {
            var pal = style.Palette;
            Vector3 point = World(evt.Point);
            Vector3 dir = WorldDir(evt.Direction);
            float radius = Mathf.Max(.6f, evt.Amount); // an area is drawn as big as it hits; Scale is for the rest
            Vector3 body = actor != null ? actor.BodyPosition : point + Vector3.up * .5f;
            Vector3 targetBody = target != null ? target.BodyPosition : point + Vector3.up * .5f;
            switch (style.Look)
            {
                case Look.Slash:
                {
                    float size = 1.7f * style.Scale;
                    SoulFx.Anim(fx, SoulFxArt.Slash(pal), targetBody, size, 26, SoulFx.High, null, true, false, Angle(dir));
                    if (style.Scale >= 1.5f) // 일섬: a line of light across the target
                        SoulFx.Anim(fx, SoulFxArt.Beam(SoulFxPalette.Holy), targetBody - dir * 1.4f, 2.8f, 30, SoulFx.High, null, true, false, Angle(dir), 0, null, .35f);
                    SoulFx.Particles(fx, SoulFxArt.Spark, targetBody, 10, 3.2f, Tone(pal, 0), Tone(pal, 2), .35f, 1.5f, .1f, Angle(dir), 110);
                    break;
                }
                case Look.Spin:
                    SoulFx.Anim(fx, SoulFxArt.Spin(pal), point + Vector3.up * .45f, radius * 2f, 24, SoulFx.High, null, true, true);
                    SoulFx.Anim(fx, SoulFxArt.Nova(SoulFxPalette.Smoke), point, radius * 2, 18, SoulFx.Ground, WithAlpha(Color.white, .6f), false, true);
                    SoulFx.Particles(fx, SoulFxArt.Spark, point + Vector3.up * .4f, 16, 3.5f, Tone(pal, 0), Tone(pal, 2), .45f, 1, .1f);
                    Shake(.15f * style.Scale, point);
                    break;
                case Look.Nova:
                    SoulFx.Anim(fx, SoulFxArt.Nova(pal), point, radius * 2, 20, SoulFx.Ground, null, true, true);
                    SoulFx.Particles(fx, pal == SoulFxPalette.Water ? SoulFxArt.Drop : SoulFxArt.Rock, point + Vector3.up * .2f, 12, 2.8f, Tone(pal, 1), Tone(pal, 3), .55f, 4, .12f, 90, 160);
                    Shake(.12f, point);
                    break;
                case Look.Blast:
                {
                    float size = Mathf.Max(1.3f * style.Scale, radius * 2);
                    SoulFx.Anim(fx, SoulFxArt.Blast(pal), point + Vector3.up * size * .25f, size, 20, SoulFx.High, null, false);
                    SoulFx.Anim(fx, new[] { SoulFxArt.Glow }, point + Vector3.up * .3f, size * 1.4f, 8, SoulFx.Air, WithAlpha(Tone(pal, 1), .6f), true, false, 0, .18f);
                    SoulFx.Anim(fx, SoulFxArt.Nova(pal), point, size * 1.1f, 20, SoulFx.Ground, WithAlpha(Color.white, .7f), true, true);
                    var bit = pal == SoulFxPalette.Ice ? SoulFxArt.Shard : pal == SoulFxPalette.Stone ? SoulFxArt.Rock : SoulFxArt.Ember;
                    SoulFx.Particles(fx, bit, point + Vector3.up * .3f, 14, 3.2f, Tone(pal, 0), Tone(pal, 3), .7f, 3, .11f, 90, 360, .15f, SoulFx.High, true, 1.5f, bit == SoulFxArt.Rock);
                    if (size >= 2.5f) Shake(.25f, point);
                    break;
                }
                case Look.Pillar:
                {
                    int count = Base(skill) == "soul_gods_wrath" ? 5 : 1;
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 spot = point + (i == 0 ? Vector3.zero : new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-.5f, .5f), 0) * radius * .8f);
                        SoulFx.Anim(fx, SoulFxArt.Pillar(pal), spot, 1.1f * style.Scale, 18, SoulFx.High, null, true, false, 0, 0, null, 4.2f * style.Scale);
                        SoulFx.Anim(fx, SoulFxArt.Nova(pal), spot, 1.8f * style.Scale, 18, SoulFx.Ground, null, true, true);
                        SoulFx.Particles(fx, SoulFxArt.Mote, spot + Vector3.up * .3f, 12, 1.2f, Tone(pal, 0), Tone(pal, 2), .9f, -2.2f, .09f, 90, 60, .4f);
                    }
                    Shake(.2f * count, point);
                    break;
                }
                case Look.Bolt: break; // the links draw it
                case Look.Beam:
                {
                    float length = Mathf.Max(2, evt.Amount);
                    SoulFx.Anim(fx, SoulFxArt.Beam(pal), body, length, 22, SoulFx.High, null, true, false, Angle(dir), 0, null, .5f * style.Scale);
                    SoulFx.Particles(fx, pal == SoulFxPalette.Water ? SoulFxArt.Drop : SoulFxArt.Spark, body + dir * length * .6f, 10, 2.5f, Tone(pal, 0), Tone(pal, 2), .45f, pal == SoulFxPalette.Water ? 4 : 0, .1f, Angle(dir), 60);
                    break;
                }
                case Look.Arrow:
                {
                    float length = Mathf.Max(2, evt.Amount > 0 ? evt.Amount : Vector3.Distance(body, targetBody));
                    SoulProjectileFx.Spawn(fx, SoulFxArt.Arrow, body, body + dir * length, 30f);
                    SoulFx.Anim(fx, SoulFxArt.Beam(pal), body, length, 24, SoulFx.High, WithAlpha(Color.white, .85f), true, false, Angle(dir), 0, null, .3f);
                    break;
                }
                case Look.Cone:
                {
                    float length = Mathf.Max(2, evt.Amount) * 1.05f;
                    SoulFx.Anim(fx, SoulFxArt.Cone(pal), body, length, 16, SoulFx.High, null, false, false, Angle(dir), 0, null, length * (style.Scale > 1 ? .8f : .62f));
                    SoulFx.Anim(fx, new[] { SoulFxArt.Glow }, body + dir * length * .35f, length * .9f, 8, SoulFx.Air, WithAlpha(Tone(pal, 1), .45f), true, false, Angle(dir), .3f, null, length * .45f);
                    SoulFx.Particles(fx, SoulFxArt.Ember, body + dir * .5f, 18, 5f, Tone(pal, 0), Tone(pal, 3), .7f, -.8f, .1f, Angle(dir), 50);
                    break;
                }
                case Look.Vortex:
                    SoulFx.Anim(fx, SoulFxArt.Vortex(pal), point, radius * 2.1f, 16, SoulFx.Ground, null, true, true, 0, .9f).Grow(.35f);
                    SoulFx.Particles(fx, SoulFxArt.Mote, point + Vector3.up * .3f, 14, 1.8f, Tone(pal, 0), Tone(pal, 2), .8f, -1, .1f);
                    if (style.Scale >= 1.2f) Shake(.18f, point);
                    break;
                case Look.Sigil:
                {
                    Vector3 on = target != null ? target.transform.position : point;
                    SoulFx.Anim(fx, SoulFxArt.Sigil(pal), on, 1.6f, 12, SoulFx.Ground, null, true, true, 0, .8f, target != null ? target.transform : null);
                    SoulFx.Particles(fx, SoulFxArt.Mote, on + Vector3.up * .1f, 12, .8f, Tone(pal, 0), Tone(pal, 2), 1f, -1.8f, .08f, 90, 50, .4f);
                    if (Base(skill) == "soul_death_sentence") SoulFx.Anim(fx, SoulFxArt.Pillar(pal), on, .9f, 16, SoulFx.High, null, true, false, 0, 0, null, 3f);
                    break;
                }
                case Look.Puff:
                    SoulFx.Anim(fx, SoulFxArt.Puff(pal), point + Vector3.up * .45f, Mathf.Max(1.6f, radius * 2) * style.Scale, 14, SoulFx.High);
                    if (Base(skill) == "soul_bat_form")
                        SoulFx.Particles(fx, SoulFxArt.Bat, point + Vector3.up * .6f, 8, 3f, new Color(.5f, .3f, .6f), new Color(.2f, .1f, .25f), .9f, -1f, .22f, 90, 180);
                    break;
                case Look.Rain:
                {
                    bool snow = pal == SoulFxPalette.Ice;
                    for (int i = 0; i < 3; i++)
                        SoulFx.Particles(fx, snow ? SoulFxArt.Flake : SoulFxArt.Arrow, point + new Vector3((i - 1) * radius * .5f, 3.2f, 0), snow ? 10 : 6, 6f,
                            snow ? Color.white : new Color(.9f, .85f, .75f), snow ? Tone(pal, 1) : new Color(.6f, .55f, .45f), .55f, 3f, snow ? .16f : .3f, -90, 18, radius * .45f, SoulFx.High, false, 0);
                    SoulFx.Anim(fx, SoulFxArt.Nova(pal), point, radius * 2, 14, SoulFx.Ground, WithAlpha(Color.white, .8f), true, true);
                    break;
                }
                case Look.Meteor:
                    SoulFxFall.Spawn(fx, point + new Vector3(-2.2f, 6f, 0), point + Vector3.up * .3f, 1.1f * style.Scale, pal, () =>
                    {
                        SoulFx.Anim(fx, SoulFxArt.Blast(pal), point + Vector3.up * radius * .5f, radius * 2.4f, 18, SoulFx.High);
                        SoulFx.Anim(fx, SoulFxArt.Nova(SoulFxPalette.Stone), point, radius * 2.6f, 16, SoulFx.Ground, null, false, true);
                        SoulFx.Particles(fx, SoulFxArt.Rock, point + Vector3.up * .3f, 16, 4f, Tone(SoulFxPalette.Stone, 1), Tone(SoulFxPalette.Stone, 3), .8f, 6, .16f, 90, 160, .3f, SoulFx.High, false, 1, true);
                        SoulFx.Particles(fx, SoulFxArt.Ember, point + Vector3.up * .3f, 20, 3.5f, Tone(pal, 0), Tone(pal, 3), 1f, -1f, .1f);
                        Shake(.45f, point);
                    });
                    break;
                case Look.Throw:
                    SoulFxFall.Spawn(fx, body, targetBody, pal == SoulFxPalette.Stone ? .55f : .35f, pal, () =>
                    {
                        SoulFx.Anim(fx, SoulFxArt.Blast(pal), targetBody, 1.1f, 22, SoulFx.High);
                        SoulFx.Particles(fx, pal == SoulFxPalette.Stone ? SoulFxArt.Rock : SoulFxArt.Drop, targetBody, 10, 2.5f, Tone(pal, 1), Tone(pal, 3), .5f, 5, .12f, 90, 180, .1f, SoulFx.High, false, 1, pal == SoulFxPalette.Stone);
                    }, .45f);
                    break;
                case Look.Shout:
                    for (int i = 0; i < 3; i++)
                    {
                        var ring = SoulFx.Anim(fx, SoulFxArt.Nova(pal), point, radius * 2 * (.7f + i * .2f), 16 - i * 3, SoulFx.Ground, null, true, true);
                        if (ring != null) ring.Grow(1 + i * .1f);
                    }
                    SoulFx.Anim(fx, new[] { SoulFxArt.Glow }, body, 1.6f, 8, SoulFx.Air, WithAlpha(Tone(pal, 1), .6f), true, false, 0, .25f, actor != null ? actor.transform : null);
                    break;
                case Look.Ward:
                    SoulFx.Anim(fx, SoulFxArt.Nova(pal), point, 1.6f, 18, SoulFx.Ground, null, true, true);
                    SoulFx.Anim(fx, new[] { SoulFxArt.Glow }, body, 1.7f, 8, SoulFx.Air, WithAlpha(Tone(pal, 1), .65f), true, false, 0, .35f, actor != null ? actor.transform : null);
                    SoulFx.Particles(fx, SoulFxArt.Mote, point + Vector3.up * .1f, 14, 1f, Tone(pal, 0), Tone(pal, 2), .9f, -2f, .09f, 90, 60, .35f);
                    break;
                case Look.Heal:
                    if (style.Scale > 1) SoulFx.Anim(fx, SoulFxArt.Pillar(pal), point, 1.4f * style.Scale, 16, SoulFx.High, null, true, false, 0, 0, null, 4.5f);
                    SoulFx.Anim(fx, SoulFxArt.Nova(pal), point, Mathf.Max(1.4f, radius), 16, SoulFx.Ground, null, true, true);
                    SoulFx.Particles(fx, SoulFxArt.Star, point + Vector3.up * .3f, 8, .9f, Tone(pal, 0), Tone(pal, 1), .9f, -1.4f, .12f, 90, 120, .4f);
                    break;
                case Look.Drain:
                {
                    // the life (or the soul) streams from the target to the caster
                    Vector3 from = targetBody, to = body;
                    if (skill.Burst) from = point + Vector3.up * .4f;
                    Vector3 gap = to - from;
                    SoulFx.Particles(fx, SoulFxArt.Mote, from, 16, gap.magnitude * 1.6f, Tone(pal, 0), Tone(pal, 1), .6f, 0, .11f, Angle(gap), 35, .3f, SoulFx.High, true, .2f);
                    SoulFx.Anim(fx, SoulFxArt.Nova(pal), point, Mathf.Max(1.2f, radius * 2), 16, SoulFx.Ground, null, true, true);
                    break;
                }
                case Look.Leap:
                    SoulFx.Anim(fx, SoulFxArt.Nova(pal == SoulFxPalette.Stone ? SoulFxPalette.Smoke : pal), point, radius * 2.2f, 18, SoulFx.Ground, null, pal != SoulFxPalette.Stone, true);
                    SoulFx.Anim(fx, SoulFxArt.Puff(SoulFxPalette.Smoke), point + Vector3.up * .3f, radius * 1.6f, 16, SoulFx.Air, WithAlpha(Color.white, .75f));
                    if (pal == SoulFxPalette.Fire) SoulFx.Anim(fx, SoulFxArt.Blast(pal), point + Vector3.up * .5f, radius * 2, 18, SoulFx.High);
                    SoulFx.Particles(fx, SoulFxArt.Rock, point + Vector3.up * .1f, 10, 3f, Tone(SoulFxPalette.Stone, 1), Tone(SoulFxPalette.Stone, 3), .5f, 6, .12f, 90, 160, .2f, SoulFx.High, false, 1, true);
                    Shake(.2f * style.Scale, point);
                    break;
                case Look.Tongue:
                    SoulFx.Anim(fx, SoulFxArt.Beam(SoulFxPalette.Blood), body, Vector3.Distance(body, targetBody), 30, SoulFx.High, new Color(1f, .7f, .8f), false, false, Angle(targetBody - body), 0, null, .22f);
                    break;
                case Look.Field:
                    SoulFx.Anim(fx, SoulFxArt.Puff(pal == SoulFxPalette.Steel ? SoulFxPalette.Smoke : pal), point + Vector3.up * .25f, Mathf.Max(1.2f, radius * 1.4f), 16, SoulFx.Air, WithAlpha(Color.white, .7f));
                    break;
            }
        }

        static void Shake(float power, Vector3 at) => CameraShaker.Impulse(Mathf.Min(.5f, power), at);
    }

    // Something that falls or is thrown to a point (a meteor from the sky, a boulder, a gob of slime), then lands.
    public sealed class SoulFxFall : MonoBehaviour
    {
        Vector3 from, to;
        float duration, age, arc;
        Action landed;
        SpriteRenderer body;
        SoulFxPalette palette;

        public static void Spawn(Transform parent, Vector3 from, Vector3 to, float size, SoulFxPalette palette, Action onLand, float arc = 0)
        {
            var sprite = palette == SoulFxPalette.Stone || palette == SoulFxPalette.Fire ? SoulFxArt.Rock : SoulFxArt.Bubble;
            var renderer = SoulFx.Renderer("Falling", parent, sprite, SoulFxArt.Colors(palette)[palette == SoulFxPalette.Fire ? 2 : 1], SoulFx.High, false);
            renderer.transform.position = from;
            renderer.transform.localScale = Vector3.one * size / Mathf.Max(.01f, sprite.bounds.size.x);
            var fall = renderer.gameObject.AddComponent<SoulFxFall>();
            fall.from = from; fall.to = to; fall.landed = onLand; fall.body = renderer; fall.palette = palette; fall.arc = arc;
            fall.duration = Mathf.Clamp(Vector3.Distance(from, to) / 11f, .18f, .6f);
        }

        void Update()
        {
            float dt = SoulFx.Delta;
            age += dt;
            float t = Mathf.Clamp01(age / duration);
            transform.position = Vector3.Lerp(from, to, t) + Vector3.up * arc * 4 * t * (1 - t);
            transform.Rotate(0, 0, 540 * dt);
            // a fiery trail behind a meteor
            if (palette == SoulFxPalette.Fire && Time.frameCount % 2 == 0)
                SoulFx.Particles(transform.parent, SoulFxArt.Ember, transform.position, 3, .6f, SoulFxArt.Colors(palette)[0], SoulFxArt.Colors(palette)[3], .35f, -1, .12f);
            if (t >= 1)
            {
                landed?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}
