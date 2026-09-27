using System;
using TMPro;
using UnityEngine;

namespace SoulMercenaries
{
    // A label that follows a value every frame (a countdown) without rebuilding the popup around it.
    public sealed class SoulLiveText : MonoBehaviour
    {
        Func<string> text;
        TMP_Text label;

        public static void Bind(TMP_Text label, Func<string> text)
        {
            if (label == null) return;
            var live = label.gameObject.AddComponent<SoulLiveText>();
            live.label = label; live.text = text;
            live.Update();
        }

        void Update() => label.text = text();
    }
}
