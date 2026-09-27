using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // Recycled UI objects. The screens rebuild their bodies whenever something changes (SoulUi.Clear, then the
    // factories again); instead of destroying and creating GameObjects, text meshes and images every time, cleared
    // objects go back here and the factories (SoulUi.Rect / Panel / Icon / Text) take them out again.
    //
    // Three kinds, by what the object is made of: a bare rect, an image, a text. On the way back an object loses
    // whatever was added to it after it was made (buttons, layout groups, tooltips, scroll rects …) and its own
    // component is put back to how a fresh one starts. A released object waits a frame before it is handed out again,
    // so nothing still running on it this frame (a click handler, a destroy in flight) meets it in a new place.
    public static class SoulUiPool
    {
        public enum Kind { Rect, Image, Text }

        const int Cap = 1500; // per kind: more than this and the extra are destroyed

        sealed class Entry { public GameObject Go; public int Frame; }

        static Transform shelf;
        static readonly Stack<GameObject>[] free = { new Stack<GameObject>(), new Stack<GameObject>(), new Stack<GameObject>() };
        static readonly List<Entry> resting = new List<Entry>();
        static readonly List<Component> components = new List<Component>();

        static Transform Shelf
        {
            get
            {
                if (shelf != null) return shelf;
                var go = new GameObject("SoulUiPool");
                go.SetActive(false);
                Object.DontDestroyOnLoad(go);
                shelf = go.transform;
                return shelf;
            }
        }

        // An object of that kind under `parent`, as if new.
        public static GameObject Take(Kind kind, string name, Transform parent)
        {
            Wake();
            var stack = free[(int)kind];
            while (stack.Count > 0)
            {
                var go = stack.Pop();
                if (go == null) continue; // destroyed with a closed popup while it waited
                go.name = name;
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                Reset(rect);
                Fresh(go, kind);
                go.SetActive(true);
                return go;
            }
            var made = new GameObject(name, typeof(RectTransform));
            made.transform.SetParent(parent, false);
            made.AddComponent<SoulPooled>().Kind = kind;
            if (kind == Kind.Image) made.AddComponent<Image>();
            else if (kind == Kind.Text) made.AddComponent<TextMeshProUGUI>();
            return made;
        }

        // Everything under `parent` back to the pool (objects not made by the pool are destroyed as before).
        public static void ReleaseChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) Release(parent.GetChild(i).gameObject);
        }

        public static void Release(GameObject go)
        {
            var mark = go.GetComponent<SoulPooled>();
            if (mark == null || resting.Count + free[(int)mark.Kind].Count >= Cap)
            {
                go.SetActive(false);
                Object.Destroy(go);
                return;
            }
            ReleaseChildren(go.transform);
            go.SetActive(false);
            go.transform.SetParent(Shelf, false);
            Strip(go, mark.Kind);
            resting.Add(new Entry { Go = go, Frame = Time.frameCount });
        }

        // Released a frame ago or more: free to hand out.
        static void Wake()
        {
            int frame = Time.frameCount;
            for (int i = resting.Count - 1; i >= 0; i--)
            {
                var entry = resting[i];
                if (entry.Frame >= frame) continue;
                resting.RemoveAt(i);
                if (entry.Go == null) continue;
                var mark = entry.Go.GetComponent<SoulPooled>();
                if (mark != null) free[(int)mark.Kind].Push(entry.Go);
            }
        }

        // What was added after it was made goes (the kind's own graphic, its renderer, a layout element and the mark stay).
        static void Strip(GameObject go, Kind kind)
        {
            go.GetComponents(components);
            foreach (var component in components)
            {
                if (component == null || component is Transform || component is SoulPooled || component is CanvasRenderer || component is LayoutElement) continue;
                if (kind == Kind.Image && component is Image) continue;
                if (kind == Kind.Text && component is TextMeshProUGUI) continue;
                Object.Destroy(component);
            }
            components.Clear();
        }

        static void Reset(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.sizeDelta = new Vector2(100, 100);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.gameObject.layer = 0;
        }

        // The kind's own component (and the layout element) back to a fresh one's settings.
        static void Fresh(GameObject go, Kind kind)
        {
            if (go.TryGetComponent(out LayoutElement layout))
            {
                layout.ignoreLayout = false;
                layout.minWidth = layout.minHeight = layout.preferredWidth = layout.preferredHeight = -1;
                layout.flexibleWidth = layout.flexibleHeight = -1;
                layout.layoutPriority = 1;
                layout.enabled = true;
            }
            if (go.TryGetComponent(out CanvasRenderer renderer))
            {
                // a scroll list's mask may have culled it (out of view) or clipped it: that stays on the renderer
                renderer.SetColor(Color.white);
                renderer.SetAlpha(1);
                renderer.cull = false;
                renderer.DisableRectClipping();
            }
            if (kind == Kind.Image)
            {
                var image = go.GetComponent<Image>();
                image.enabled = true;
                image.sprite = null;
                image.overrideSprite = null;
                image.material = null;
                image.color = Color.white;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.fillCenter = true;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillAmount = 1;
                image.pixelsPerUnitMultiplier = 1;
                image.useSpriteMesh = false;
                image.raycastTarget = true;
                image.maskable = true;
            }
            else if (kind == Kind.Text)
            {
                var text = go.GetComponent<TextMeshProUGUI>();
                text.enabled = true;
                text.text = "";
                text.fontStyle = FontStyles.Normal;
                text.enableAutoSizing = false;
                text.characterSpacing = text.wordSpacing = text.lineSpacing = text.paragraphSpacing = 0;
                text.margin = Vector4.zero;
                text.richText = true;
                text.maxVisibleCharacters = 99999;
                text.color = Color.white;
                text.raycastTarget = true;
                text.maskable = true;
                if (text.font != null && text.fontSharedMaterial != text.font.material) text.fontSharedMaterial = text.font.material; // an outline set on it is gone
            }
        }
    }

    // Marks an object the pool made (and so may take back).
    public sealed class SoulPooled : MonoBehaviour
    {
        public SoulUiPool.Kind Kind;
    }
}
