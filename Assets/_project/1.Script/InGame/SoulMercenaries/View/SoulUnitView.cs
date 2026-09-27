using Assets.PixelFantasy.Common.Scripts;
using TMPro;
using UnityEngine;

namespace SoulMercenaries
{
    // One combatant on the map: a PixelHeroes character driven by the session state and combat events.
    // Mirrors the original UnitAnimationSync mapping (Idle/Run/Ready bools, attack/Hit triggers, Die).
    public sealed class SoulUnitView : MonoBehaviour
    {
        const float BaseScale = .72f;      // PixelHeroes 64px frame at 16 PPU → about 1.2 cells tall
        const float ReferenceHeight = 1.7f; // design height that maps to BaseScale
        const float ReferenceBuild = 70f / 1.7f; // kg per meter of a standard human: girth 1

        // Height sets the size (x: against 1.7 m); weight relative to height sets the girth (y: a heavy, short body
        // looks stout). The world and every portrait draw a body with it.
        public static Vector2 BodyShape(float height, float weight)
        {
            float size = Mathf.Clamp(height / ReferenceHeight, .6f, 1.8f);
            float build = weight / Mathf.Max(.5f, height);
            return new Vector2(size, Mathf.Clamp(Mathf.Sqrt(build / ReferenceBuild), .85f, 1.45f));
        }

        static readonly string[] BoolParams = { "Idle", "Ready", "Walk", "Run", "Crouch", "Crawl", "Jump", "Fall", "Land", "Block", "Climb", "Die" };

        public SoulCombatant Unit { get; private set; }
        public bool IsMonsterSprite { get; private set; }

        Transform visual;
        Animator animator;
        UnitAppearanceBridge appearance;
        SpriteRenderer[] renderers;
        Color[] baseColors;
        Color tint = Color.white;
        SpriteRenderer hpFill, hpLocked, selection;
        Transform hud;
        SpriteRenderer statusIcon, body, shadow;
        TextMeshPro statusText;
        bool hidden;
        Vector2 lastPosition;
        string state;
        float facing = 1, flash, deadTime, headHeight, girth = 1, lastAction = -10, lastStep = -10;
        Color colorMultiplier = Color.white;
        bool dead;
        string appliedLook;

        public static SoulUnitView Create(SoulCombatant unit, GameObject characterPrefab, Transform parent)
        {
            var root = new GameObject(unit.CombatId);
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<SoulUnitView>();
            view.Build(unit, characterPrefab);
            view.Face(unit.Facing.x); // a waiting formation looks toward the way in
            return view;
        }

        void Build(SoulCombatant unit, GameObject characterPrefab)
        {
            Unit = unit;
            lastPosition = unit.Position;
            transform.position = SoulWorldView.ToWorld(unit.Position);

            selection = SoulSprites.Renderer("Selection", transform, SoulSprites.Ring, new Color(.3f, 1f, 1f, .9f), 5);
            selection.transform.localScale = new Vector3(1.1f, .55f, 1);
            selection.enabled = false;
            shadow = SoulSprites.Renderer("Shadow", transform, SoulSprites.Disc, new Color(0, 0, 0, .35f), 4);
            shadow.transform.localScale = new Vector3(.8f, .32f, 1);

            visual = Instantiate(characterPrefab, transform).transform;
            visual.name = "Character";
            visual.localPosition = Vector3.zero; // the PixelHeroes demo prefab root sits at x = -4
            var creature = visual.GetComponent<Creature>();
            animator = creature.Animator;
            // the PixelHeroes clips fire animation events (SitDown: SetBool "Crouched") at the animator's object
            if (animator != null && animator.GetComponent<SoulAnimationEvents>() == null) animator.gameObject.AddComponent<SoulAnimationEvents>();
            body = creature.Body;
            appearance = visual.gameObject.AddComponent<UnitAppearanceBridge>();
            UnitSortingSetup.Apply(visual.gameObject);
            ApplyLook(true);
            statusFx = gameObject.AddComponent<SoulStatusFx>();
            statusFx.Setup(this);

            hud = new GameObject("Hud").transform;
            hud.SetParent(transform, false);
            var back = SoulSprites.Renderer("HpBack", hud, SoulSprites.Pixel, new Color(0, 0, 0, .75f), 800);
            back.transform.localScale = new Vector3(.8f, .09f, 1);
            hpFill = SoulSprites.Renderer("HpFill", hud, SoulSprites.Pixel, unit is SoulMercenary ? SoulUi.Good : SoulUi.HpColor, 801);
            hpLocked = SoulSprites.Renderer("HpLocked", hud, SoulSprites.Pixel, SoulUi.LockedHp, 801); // max HP locked by wounds
            statusIcon = SoulSprites.Renderer("StatusIcon", hud, null, Color.white, 803);
            statusIcon.transform.localPosition = new Vector3(0, .32f, 0);
            statusText = SoulSprites.WorldText("Status", hud, "", 2.6f, SoulUi.Accent, 802);
            statusText.transform.localPosition = new Vector3(0, .22f, 0);
            SetBool("Idle");
        }

        // Re-applies race/soul look and body scale; cheap no-op when nothing changed.
        public void ApplyLook(bool force = false)
        {
            string key;
            if (Unit is SoulMonster monster)
            {
                var data = monster.Data;
                key = data.UseMonsterSprite ? "monster:" + data.MonsterRace : JsonUtility.ToJson(data.Appearance);
                if (force || key != appliedLook)
                {
                    IsMonsterSprite = data.UseMonsterSprite;
                    if (data.UseMonsterSprite) appearance.ApplyMonsterRace(data.MonsterRace);
                    else appearance.ApplyCustom(data.Appearance);
                    tint = data.Tint;
                }
            }
            else
            {
                key = JsonUtility.ToJson(Unit.Stats.Appearance);
                if (force || key != appliedLook) appearance.ApplyCustom(Unit.Stats.Appearance);
            }
            if (force || key != appliedLook)
            {
                appliedLook = key;
                renderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
                baseColors = new Color[renderers.Length];
                for (int i = 0; i < renderers.Length; i++) baseColors[i] = tint; // not the live color: a Hit clip may be mid-flash
            }
            var shape = BodyShape(Unit.Stats.Height, Unit.Stats.Weight);
            float scale = BaseScale * shape.x;
            girth = shape.y;
            visual.localScale = new Vector3(scale * girth * facing, scale, 1);
            headHeight = 1.25f * scale / BaseScale;
        }

        public void SetSelected(bool selected) => selection.enabled = selected && !dead && !hidden;

        // Live animated frame of the merged PixelHeroes sheet: the HUD uses it as a portrait.
        public Sprite BodySprite => body != null ? body.sprite : null;

        // Units in unexplored cells are not drawn at all (fog of war).
        public void SetHidden(bool value)
        {
            if (hidden == value || dead) return;
            hidden = value;
            visual.gameObject.SetActive(!value);
            hud.gameObject.SetActive(!value);
            shadow.enabled = !value;
        }

        public void PlayAttack(SoulPatternData pattern, SoulCombatant target, bool magic = false)
        {
            if (dead || hidden) return;
            if (target != null) Face(target.Position.x - Unit.Position.x);
            animator.SetTrigger(IsMonsterSprite ? "Slash" : magic ? "Jab" : Motion(pattern)); // staff thrust while casting
            lastAction = Time.time;
        }

        // Dodge pattern succeeded: the PixelHeroes roll (a short tumble) instead of a hit reaction.
        // Heals, buffs and potions: the raised-hands blessing, not a swing.
        public void PlaySupport(SoulCombatant target)
        {
            if (dead || hidden) return;
            if (target != null && target != Unit) Face(target.Position.x - Unit.Position.x);
            animator.SetTrigger(IsMonsterSprite ? "Jab" : "Heal");
            lastAction = Time.time;
        }

        public void PlayRoll()
        {
            if (dead || hidden) return;
            animator.SetTrigger("Roll");
            lastAction = Time.time;
        }

        // from: the attacker (the body is thrown back away from it for a moment).
        public void PlayHit(Vector3? from = null)
        {
            if (dead || hidden) return;
            animator.SetTrigger("Hit");
            lastAction = Time.time;
            flash = .18f;
            if (from.HasValue)
            {
                var away = transform.position - from.Value;
                away.z = 0;
                recoil = away.sqrMagnitude > 1e-4f ? away.normalized * RecoilDistance : Vector3.zero;
            }
        }

        const float RecoilDistance = .28f;
        Vector3 recoil;

        public void PlayDeath()
        {
            if (dead) return;
            if (hidden) { dead = true; gameObject.SetActive(false); return; } // died in the fog
            dead = true;
            SetBool("Die");
            hud.gameObject.SetActive(false);
            selection.enabled = false;
        }

        // 소생: back on its feet.
        public void PlayRevive()
        {
            if (!dead) return;
            dead = false;
            deadTime = 0;
            colorMultiplier = Color.white;
            gameObject.SetActive(!hidden);
            hud.gameObject.SetActive(true);
            SetBool("Idle");
            Flash(new Color(1f, .95f, .6f));
        }

        public void Flash(Color color) { flashColor = color; flash = .35f; }
        Color flashColor = new Color(1f, .2f, .2f);

        // For the status and buff pictures (SoulStatusFx): the merged body sprite, how tall and how wide the body is.
        public SpriteRenderer BodyRenderer => body;
        public float BodyHeight => headHeight;
        public float BodyWidth => headHeight * .42f * girth;
        public bool Dead => dead;
        public bool Hidden => hidden;
        SoulStatusFx statusFx;

        public Vector3 HeadPosition => transform.position + Vector3.up * headHeight;
        public Vector3 BodyPosition => transform.position + Vector3.up * (headHeight * .45f);

        static string Motion(SoulPatternData pattern)
        {
            if (pattern == null) return "Slash";
            if (pattern.CastsSkills || pattern.DamageSchool == SoulDamageSchool.Magic) return "Jab"; // staff thrust while casting
            if (pattern.WeaponTag == "bow") return "Shot";
            if (pattern.WeaponTag == "fist") return "Jab"; // a straight punch
            if (pattern.Knockback > 0) return "Push";
            return pattern.DamageKind == SoulDamageKind.Pierce ? "Jab" : "Slash";
        }

        void Update()
        {
            if (Unit == null) return;
            if (dead)
            {
                deadTime += Time.deltaTime;
                if (deadTime > 1.2f) colorMultiplier = new Color(1, 1, 1, Mathf.Clamp01(1 - (deadTime - 1.2f) / .6f));
                if (deadTime > 1.8f) gameObject.SetActive(false);
                return;
            }

            // Knockback teleports the logical position; smooth it on screen.
            Vector3 target = SoulWorldView.ToWorld(Unit.Position);
            transform.position = Vector3.Lerp(transform.position, target, 1 - Mathf.Exp(-18f * Time.deltaTime));

            if (hidden) { lastPosition = Unit.Position; return; } // inactive animator: nothing to drive
            Vector2 delta = Unit.Position - lastPosition;
            lastPosition = Unit.Position;
            bool stepped = delta.sqrMagnitude > .008f * .008f; // ignore tiny body-separation nudges
            if (stepped && Mathf.Abs(delta.x) > 1e-4f) Face(delta.x);
            // Keep running through short pauses (a tick spent on a replan or a strike) so the run cycle does not
            // restart every few frames.
            if (stepped) lastStep = Time.time;
            bool moving = Time.time - lastStep < .2f;

            // PixelHeroes' SoloState sets "Action" on attack/hit states and clears it only when the clip plays
            // to the end. An interrupted clip (hit, stun freeze) leaves it on and every Idle/Run transition
            // stays blocked — the unit slides around frozen. Release it once no action was started recently.
            if (Time.time - lastAction > .9f) animator.SetBool("Action", false);
            int pose = Unit is SoulMercenary rester ? rester.RestPose : 0;
            recoil = Vector3.Lerp(recoil, Vector3.zero, 1 - Mathf.Exp(-9f * Time.deltaTime));
            visual.localPosition = recoil;
            if (Unit.DownTime > 0) SetBool("Die"); // knocked down: on the ground
            else if (Unit.Disabled) SetBool("Idle");
            else if (moving) SetBool("Run");
            else if (pose > 0) SetBool(pose == 1 ? "Crouch" : "Die"); // resting: sitting, or lying down
            else SetBool(Unit.Cooldown > 0 ? "Ready" : "Idle");
            animator.speed = Unit.Disabled && Unit.DownTime <= 0 ? 0 : 1;

            hud.localPosition = Vector3.up * headHeight;
            float locked = Unit is SoulMercenary hurt ? hurt.LockedHp : 0;
            float full = Mathf.Max(1, Unit.Stats.Total(StatType.MaxHp) + locked);
            float ratio = Mathf.Clamp01(Unit.Hp / full), lockShare = Mathf.Clamp01(locked / full);
            hpFill.transform.localScale = new Vector3(.8f * ratio, .09f, 1);
            hpFill.transform.localPosition = new Vector3(-.4f + .4f * ratio, 0, 0);
            hpLocked.enabled = lockShare > 0;
            hpLocked.transform.localScale = new Vector3(.8f * lockShare, .09f, 1);
            hpLocked.transform.localPosition = new Vector3(.4f - .4f * lockShare, 0, 0);
            // Status as an icon above the HP bar (text only when the icon set is missing).
            var first = Unit.Statuses.Count == 0 ? SoulStatus.None : Unit.Statuses[0].Kind;
            var sprite = first == SoulStatus.None ? null : SoulIconSet.Status(first);
            statusIcon.sprite = sprite;
            statusIcon.enabled = sprite != null;
            if (sprite != null) statusIcon.transform.localScale = Vector3.one * (.34f / Mathf.Max(.01f, sprite.bounds.size.y));
            statusText.text = sprite != null || first == SoulStatus.None ? "" : SoulCombat.StatusName(first);

            if (flash > 0)
            {
                flash -= Time.deltaTime;
                colorMultiplier = Color.Lerp(Color.white, flashColor, Mathf.Clamp01(flash / .18f));
                if (flash <= 0) { colorMultiplier = Color.white; flashColor = new Color(1f, .2f, .2f); }
            }
        }

        void Face(float dx)
        {
            if (Mathf.Abs(dx) < 1e-4f) return;
            facing = dx > 0 ? 1 : -1;
            var scale = visual.localScale;
            visual.localScale = new Vector3(Mathf.Abs(scale.x) * facing, scale.y, scale.z);
        }

        // After the Animator: PixelHeroes' Hit/Heal clips key SpriteRenderer.color, which would wipe tint and flashes.
        void LateUpdate() => ApplyColors(statusFx != null ? colorMultiplier * statusFx.Tint : colorMultiplier);

        void ApplyColors(Color multiplier)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = baseColors[i] * multiplier;
        }

        void SetBool(string param)
        {
            if (state == param || animator == null) return;
            state = param;
            foreach (string p in BoolParams) animator.SetBool(p, false);
            animator.SetBool(param, true);
        }
    }

    // Takes the animation events the PixelHeroes clips send to the animator's object (their demo's CharacterAnimation
    // did): a bool the controller has is set, one it has not is left alone — no "no receiver" warning either way.
    public sealed class SoulAnimationEvents : MonoBehaviour
    {
        Animator animator;
        readonly System.Collections.Generic.HashSet<string> bools = new System.Collections.Generic.HashSet<string>();

        void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var parameter in animator.parameters) if (parameter.type == AnimatorControllerParameterType.Bool) bools.Add(parameter.name);
        }

        public void SetBool(string param) { if (animator != null && bools.Contains(param)) animator.SetBool(param, true); }
        public void UnsetBool(string param) { if (animator != null && bools.Contains(param)) animator.SetBool(param, false); }
    }
}
