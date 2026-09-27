using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // What a click did, where it is seen: a banner pops over everything (scale punch, sparks, a chime), holds and
    // fades. SoulExpeditionRunner raises one for every new line of the campaign log — training, enchanting,
    // equipping, healing, buying, building, hiring, a party coming home.
    public sealed class SoulFeedback : MonoBehaviour
    {
        const float PopTime = .22f, HoldTime = 1.6f, FadeTime = .35f, SparkLife = .7f;
        static Canvas canvas;
        static SoulFeedback current;

        RectTransform box;
        CanvasGroup group;
        RectTransform[] sparks;
        Vector2[] sparkVelocity;
        float age;

        public static void Banner(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (canvas == null)
            {
                canvas = SoulUi.OverlayCanvas("SoulFeedbackCanvas", 400);
                DontDestroyOnLoad(canvas.gameObject);
            }
            if (current != null) Destroy(current.gameObject); // the newest one replaces it
            var root = SoulUi.Rect("Banner", canvas.transform);
            root.anchorMin = root.anchorMax = new Vector2(.5f, .72f);
            root.sizeDelta = new Vector2(900, 120);
            current = root.gameObject.AddComponent<SoulFeedback>();
            current.Build(root, text);
            AudioManager.Instance?.Play(SfxKey.SKILL_WarBanner);
        }

        void Build(RectTransform root, string text)
        {
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            box = SoulUi.Framed("Box", root, new Color(.07f, .06f, .1f, 1f), SoulUi.Accent, 3);
            SoulUi.Stretch(box);
            var glow = SoulUi.Panel("Glow", box, new Color(1f, .82f, .35f, .18f), true);
            SoulUi.Stretch(glow.rectTransform, 6);
            var label = SoulUi.Text(box, text, 30, Color.white, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            SoulUi.Stretch(label.rectTransform, 18);
            sparks = new RectTransform[10];
            sparkVelocity = new Vector2[sparks.Length];
            for (int i = 0; i < sparks.Length; i++)
            {
                var spark = SoulUi.Icon(root, SoulSprites.Diamond, 22, "Spark");
                spark.color = new Color(1f, .85f, .4f);
                sparks[i] = spark.rectTransform;
                float angle = i * Mathf.PI * 2 / sparks.Length + Random.Range(-.2f, .2f);
                sparkVelocity[i] = new Vector2(Mathf.Cos(angle) * 520, Mathf.Sin(angle) * 180) * Random.Range(.7f, 1.2f);
            }
            box.localScale = Vector3.one * .5f;
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            // punch: in past full size, then settle
            float scale = age < PopTime ? Mathf.Lerp(.5f, 1.12f, age / PopTime) : Mathf.Lerp(1.12f, 1f, Mathf.Clamp01((age - PopTime) / .12f));
            box.localScale = Vector3.one * scale;
            for (int i = 0; i < sparks.Length; i++)
            {
                float t = Mathf.Clamp01(age / SparkLife);
                sparks[i].anchoredPosition = sparkVelocity[i] * t * (1 - t * .45f);
                sparks[i].localScale = Vector3.one * (1 - t);
                sparks[i].localRotation = Quaternion.Euler(0, 0, age * 360);
            }
            float fade = age - PopTime - HoldTime;
            group.alpha = fade <= 0 ? 1 : 1 - fade / FadeTime;
            if (fade >= FadeTime) Destroy(gameObject);
        }

        void OnDestroy() { if (current == this) current = null; }
    }
}
