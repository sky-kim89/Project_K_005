using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // A mercenary at the training ground swings over and over: its attack motion, a short pause on the idle
    // frame, again. On a portrait's face image (the training popup) or drawn by a village walker (Frame).
    public sealed class SoulAttackLoop : MonoBehaviour
    {
        const float FrameTime = 1f / 12, Pause = .3f;
        SoulMercenary hero;
        Image image;
        Sprite idle;
        float time;

        // On a SoulUi.Portrait: its "Face" image plays the motion.
        public static void Attach(RectTransform portrait, SoulMercenary hero)
        {
            foreach (var image in portrait.GetComponentsInChildren<Image>(true))
            {
                if (image.name != "Face") continue;
                var loop = image.gameObject.AddComponent<SoulAttackLoop>();
                loop.hero = hero; loop.image = image; loop.idle = image.sprite;
                return;
            }
        }

        // The frame `time` seconds into the loop; null during the pause (or until the frames are drawn).
        public static Sprite Frame(SoulMercenary hero, float time)
        {
            var frames = SoulPortraits.Attack(hero);
            if (frames == null) return null;
            int index = (int)(time % (frames.Length * FrameTime + Pause) / FrameTime);
            return index < frames.Length ? frames[index] : null;
        }

        void Update()
        {
            time += Time.unscaledDeltaTime;
            image.sprite = Frame(hero, time) ?? idle;
        }
    }
}
