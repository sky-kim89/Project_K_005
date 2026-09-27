using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // Entering a building: the two door halves swing shut over the screen, the building's popup opens behind them,
    // then the doors swing open onto it. Runs on its own overlay canvas above the popups (sorting 260).
    public sealed class SoulDoorTransition : MonoBehaviour
    {
        const float CloseTime = .2f, HoldTime = .08f, OpenTime = .5f;
        static SoulDoorTransition running;

        RectTransform left, right;
        Image shade;

        public static bool Busy => running != null;

        public static void Play(Sprite leftSprite, Sprite rightSprite, Action behindDoors)
        {
            if (running != null) return;
            var canvas = SoulUi.OverlayCanvas("DoorTransition", 260);
            running = canvas.gameObject.AddComponent<SoulDoorTransition>();
            running.Build(canvas.transform, leftSprite, rightSprite);
            running.StartCoroutine(running.Run(behindDoors));
        }

        void Build(Transform canvas, Sprite leftSprite, Sprite rightSprite)
        {
            shade = SoulUi.Panel("Shade", canvas, new Color(0, 0, 0, 0));
            SoulUi.Stretch(shade.rectTransform);
            left = Half(canvas, leftSprite, new Vector2(0, 0), new Vector2(.5f, 1), new Vector2(0, .5f));
            right = Half(canvas, rightSprite, new Vector2(.5f, 0), new Vector2(1, 1), new Vector2(1, .5f));
        }

        // A half pivots on its outer edge, so scaling x from 0 to 1 swings it shut toward the middle.
        static RectTransform Half(Transform canvas, Sprite sprite, Vector2 min, Vector2 max, Vector2 pivot)
        {
            var image = SoulUi.Panel("Door", canvas, sprite != null ? Color.white : new Color(.42f, .25f, .12f));
            image.sprite = sprite;
            image.raycastTarget = true; // nothing is clicked through a door
            var rect = image.rectTransform;
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = new Vector3(0, 1, 1);
            return rect;
        }

        IEnumerator Run(Action behindDoors)
        {
            yield return Swing(0, 1, CloseTime, false);
            try { behindDoors?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            yield return new WaitForSecondsRealtime(HoldTime);
            yield return Swing(1, 0, OpenTime, true);
            running = null;
            Destroy(gameObject);
        }

        // Opening: a door turning on its hinge looks narrower fast, then slows (ease out) and darkens as it turns.
        IEnumerator Swing(float from, float to, float time, bool opening)
        {
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / time);
                k = opening ? 1 - Mathf.Pow(1 - k, 3) : k * k;
                Set(Mathf.Lerp(from, to, k), opening ? 1 - k : k);
                yield return null;
            }
            Set(to, opening ? 0 : 1);
        }

        void Set(float scale, float dark)
        {
            left.localScale = right.localScale = new Vector3(Mathf.Max(0, scale), 1, 1);
            var tint = Color.Lerp(new Color(.55f, .5f, .45f), Color.white, scale);
            left.GetComponent<Image>().color = right.GetComponent<Image>().color = tint;
            shade.color = new Color(0, 0, 0, .55f * dark);
        }
    }
}
