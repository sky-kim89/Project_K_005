using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // Runtime uGUI builder for the Soul Mercenaries screens. Colors follow the original popups
    // (EditorUIBuilder.Pop); rounded 9-slice panels come from the PixelFantasy UI kit via SoulIconSet.
    public static class SoulUi
    {
        public static readonly Color PanelBg = new Color(0.070f, 0.075f, 0.130f, .97f);
        public static readonly Color PanelBorder = new Color(0.24f, 0.30f, 0.52f, 1f);
        public static readonly Color HeaderBg = new Color(0.095f, 0.110f, 0.200f, 1f);
        public static readonly Color SlotBg = new Color(0.105f, 0.115f, 0.190f, 1f);
        public static readonly Color SlotHover = new Color(0.150f, 0.170f, 0.280f, 1f);
        public static readonly Color SubText = new Color(0.72f, 0.76f, 0.90f, 1f);
        public static readonly Color SectionLbl = new Color(0.66f, 0.70f, 0.86f, 1f);
        public static readonly Color TabActive = new Color(0.24f, 0.40f, 0.74f, 1f);
        public static readonly Color TabInactive = new Color(0.155f, 0.175f, 0.275f, 1f);
        public static readonly Color Accent = new Color(1f, .78f, .30f, 1f);
        public static readonly Color Good = new Color(.45f, .85f, .45f, 1f);
        public static readonly Color Bad = new Color(.95f, .40f, .38f, 1f);
        public static readonly Color Soul = new Color(.75f, .55f, 1f, 1f);
        public static readonly Color HpColor = new Color(.86f, .27f, .27f, 1f);
        public static readonly Color StaminaColor = new Color(.93f, .76f, .25f, 1f);
        public static readonly Color ManaColor = new Color(.33f, .55f, .95f, 1f);

        // The building blocks come from the pool (SoulUiPool): recycled, not made anew, each time a screen rebuilds.
        public static RectTransform Rect(string name, Transform parent)
            => (RectTransform)SoulUiPool.Take(SoulUiPool.Kind.Rect, name, parent).transform;

        public static Image Panel(string name, Transform parent, Color color, bool round = false)
        {
            var image = SoulUiPool.Take(SoulUiPool.Kind.Image, name, parent).GetComponent<Image>();
            image.color = color;
            if (round) Round(image);
            return image;
        }

        public static void Round(Image image, float corner = 1f)
        {
            var sprite = SoulIconSet.Current != null ? SoulIconSet.Current.Panel : null;
            if (sprite == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = corner; // < 1 = rounder corners
        }

        // Rounded frame: a border-colored rounded image behind an inset rounded fill.
        public static RectTransform Framed(string name, Transform parent, Color fill, Color border, float thickness = 2f)
        {
            var frame = Panel(name, parent, border, true).rectTransform;
            var inner = Panel("Fill", frame, fill, true);
            inner.raycastTarget = false;
            Stretch(inner.rectTransform, thickness);
            return frame;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, string name = "Text")
        {
            var tmp = SoulUiPool.Take(SoulUiPool.Kind.Text, name, parent).GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        public static Image Icon(Transform parent, Sprite sprite, float size, string name = "Icon")
        {
            var image = SoulUiPool.Take(SoulUiPool.Kind.Image, name, parent).GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            ((RectTransform)image.transform).sizeDelta = new Vector2(size, size);
            Layout(image.gameObject, size, size);
            return image;
        }

        static readonly string[] BackLabels = { "나가기", "닫기", "아니오", "취소" };

        // Every clickable soul element: a Button that plays the game's click sound (AudioManager). It carries
        // UIClickSfx's mark, so a popup's UIClickSfx.Bind does not give it a second sound.
        public static Button Clickable(GameObject target, bool back = false)
        {
            var button = target.AddComponent<Button>();
            target.AddComponent<UIClickSfxMark>();
            var key = back ? SfxKey.UI_Click_Back : SfxKey.UI_Click;
            button.onClick.AddListener(() => AudioManager.Instance?.Play(key));
            return button;
        }

        public static Button Button(Transform parent, string label, Color color, UnityAction onClick, float size = 22, Sprite icon = null)
        {
            var image = Panel("Button", parent, color, true);
            var button = Clickable(image.gameObject, System.Array.IndexOf(BackLabels, label) >= 0);
            var colors = button.colors;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1);
            colors.pressedColor = new Color(.8f, .8f, .8f, 1);
            colors.disabledColor = new Color(.45f, .45f, .5f, .6f);
            colors.fadeDuration = 0; // rebuilt disabled: no flash of the enabled colour
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            var row = Rect("Content", image.transform);
            Stretch(row, 4);
            var layout = Horizontal(row.gameObject, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            if (icon != null) Icon(row, icon, size * 1.3f);
            if (!string.IsNullOrEmpty(label))
            {
                var text = Text(row, label, size, Color.white, TextAlignmentOptions.Center, "Label");
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
            return button;
        }

        // A square tile: the icon on a dark ground inside a frame (items: the grade colour).
        public static RectTransform Tile(Transform parent, Sprite sprite, float size, Color frame)
        {
            var box = Framed("Tile", parent, new Color(.06f, .065f, .1f, 1f), frame, 2);
            Layout(box.gameObject, size, size);
            var icon = Icon(box, sprite, size - 10);
            Stretch(icon.rectTransform, 5);
            icon.raycastTarget = false;
            return box;
        }

        // A character portrait: the 64 px animation frame scaled so the body fills the box, cut by a mask.
        public static RectTransform Portrait(Transform parent, Sprite sprite, float size, Sprite fallback = null, float zoom = 3.1f, float lift = .62f)
        {
            var box = Framed("Portrait", parent, new Color(.06f, .065f, .1f, 1f), PanelBorder, 2);
            Layout(box.gameObject, size, size);
            if (sprite == null)
            {
                var icon = Icon(box, fallback, size - 10);
                Stretch(icon.rectTransform, 5);
                return box;
            }
            var mask = Rect("Mask", box);
            Stretch(mask, 2);
            mask.gameObject.AddComponent<RectMask2D>();
            var face = Icon(mask, sprite, 10, "Face");
            face.raycastTarget = false;
            var rect = face.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
            rect.sizeDelta = new Vector2(size * zoom, size * zoom);   // the body sits low in its frame (zoom 3.1: the face)
            var body = SoulPortraits.ShapeOf(sprite);                 // height and weight: taller, shorter, stouter
            SoulPortraits.Place(rect, body, -size * lift, size);
            return box;
        }

        // A compact dropdown (TMP): picks one of the options.
        public static TMP_Dropdown Dropdown(Transform parent, IList<string> options, int value, UnityAction<int> onChange, float size = 18)
        {
            var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            go.transform.SetParent(parent, false);
            var dropdown = go.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            dropdown.SetValueWithoutNotify(value);
            go.GetComponent<Image>().color = TabInactive;
            dropdown.captionText.fontSize = size;
            dropdown.captionText.color = Color.white;
            dropdown.itemText.fontSize = size;
            dropdown.itemText.color = Color.white;
            dropdown.template.GetComponent<Image>().color = PanelBg;
            var toggle = dropdown.template.GetComponentInChildren<Toggle>(true);
            if (toggle != null)
            {
                var colors = toggle.colors;
                colors.normalColor = SlotBg; colors.highlightedColor = SlotHover; colors.selectedColor = TabActive; colors.pressedColor = TabActive;
                toggle.colors = colors;
                if (toggle.graphic != null) toggle.graphic.color = Accent;
            }
            foreach (var image in go.GetComponentsInChildren<Image>(true))
                if (image.name == "Arrow") image.color = SubText;
            var scrollbar = dropdown.template.GetComponentInChildren<Scrollbar>(true);
            if (scrollbar != null) scrollbar.GetComponent<Image>().color = SlotBg;
            dropdown.RefreshShownValue();
            dropdown.onValueChanged.AddListener(onChange);
            return dropdown;
        }

        public static void SetLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = label;
        }

        // Icon + value, the basic "image first" unit of every screen. Clickable when onClick is given.
        public static RectTransform Chip(Transform parent, Sprite icon, string value, Color color, float height = 40, UnityAction onClick = null, Color? background = null)
        {
            var image = Panel("Chip", parent, background ?? SlotBg, true);
            var rect = image.rectTransform;
            var layout = Horizontal(image.gameObject, 6, 5);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(6, 10, 4, 4);
            Layout(image.gameObject, -1, height);
            Icon(rect, icon, height - 8);
            var text = Text(rect, value, height * .5f, color);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (onClick != null)
            {
                image.raycastTarget = true;
                var button = Clickable(image.gameObject);
                button.onClick.AddListener(onClick);
            }
            else image.raycastTarget = false;
            return rect;
        }

        // A horizontal bar: returns the fill RectTransform, whose anchorMax.x is the ratio.
        public static RectTransform Bar(Transform parent, Color fill, float height)
        {
            var back = Panel("Bar", parent, new Color(0, 0, 0, .55f));
            Round(back, 3f); // thin bars need small corners
            back.raycastTarget = false;
            Layout(back.gameObject, -1, height);
            var fillImage = Panel("Fill", back.transform, fill);
            Round(fillImage, 3f);
            fillImage.raycastTarget = false;
            var fillRect = fillImage.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            return fillRect;
        }

        public static void SetRatio(RectTransform fill, float ratio)
            => fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1);

        public static readonly Color LockedHp = new Color(.42f, .42f, .46f, .95f);

        // An HP bar over the full max HP: the share wounds lock is grey at the right end.
        public static void SetHp(RectTransform fill, SoulCombatant unit)
        {
            float locked = unit is SoulMercenary hero ? hero.LockedHp : 0;
            float full = Mathf.Max(1, unit.Stats.Total(StatType.MaxHp) + locked);
            SetRatio(fill, unit.Hp / full);
            var back = fill.parent;
            var mark = back.Find("Locked") as RectTransform;
            if (mark == null)
            {
                var image = Panel("Locked", back, LockedHp);
                Round(image, 3f);
                image.raycastTarget = false;
                mark = image.rectTransform;
                mark.offsetMin = mark.offsetMax = Vector2.zero;
            }
            mark.gameObject.SetActive(locked > 0);
            mark.anchorMin = new Vector2(1 - Mathf.Clamp01(locked / full), 0);
            mark.anchorMax = Vector2.one;
        }

        // Icon + bar + number in one row (HP / stamina / MP).
        public static RectTransform IconBar(Transform parent, Sprite icon, Color fill, float height, out TextMeshProUGUI value, float iconSize = 18)
        {
            var row = Rect("IconBar", parent);
            var layout = Horizontal(row.gameObject, 6);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandHeight = false;
            Layout(row.gameObject, -1, Mathf.Max(height, iconSize));
            Icon(row, icon, Mathf.Max(height, iconSize));
            var bar = Bar(row, fill, height);
            Layout(bar.parent.gameObject, -1, height, 1);
            value = Text(row, "", Mathf.Max(14, height * .9f), SubText, TextAlignmentOptions.MidlineRight);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            Layout(value.gameObject, 90, -1);
            return bar;
        }

        public static VerticalLayoutGroup Vertical(GameObject go, float spacing, float padding = 0, bool expandWidth = true)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = expandWidth;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup Horizontal(GameObject go, float spacing, float padding = 0)
        {
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return layout;
        }

        public static GridLayoutGroup Grid(GameObject go, Vector2 cell, Vector2 spacing)
        {
            var grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = spacing;
            return grid;
        }

        // width/height < 0 leaves that axis to the layout; flexibleWidth 1 fills the row.
        public static LayoutElement Layout(GameObject go, float width, float height, float flexibleWidth = -1)
        {
            if (!go.TryGetComponent(out LayoutElement element)) element = go.AddComponent<LayoutElement>();
            if (width >= 0) { element.preferredWidth = width; element.minWidth = width; }
            if (height >= 0) { element.preferredHeight = height; element.minHeight = height; }
            if (flexibleWidth >= 0) element.flexibleWidth = flexibleWidth;
            return element;
        }

        public static RectTransform Row(Transform parent, float height, float spacing = 8)
        {
            var row = Rect("Row", parent);
            var layout = Horizontal(row.gameObject, spacing);
            layout.childAlignment = TextAnchor.MiddleLeft;
            // fixed height: a force-expanding row would otherwise report itself flexible and soak up spare space
            Layout(row.gameObject, -1, height).flexibleHeight = 0;
            return row;
        }

        // Rows that wrap: a grid whose cells size to content is awkward in uGUI, so flowing chips use fixed cells.
        public static RectTransform Flow(Transform parent, Vector2 cell, float spacing = 6)
        {
            var flow = Rect("Flow", parent);
            Grid(flow.gameObject, cell, new Vector2(spacing, spacing)); // the parent layout reads its preferred height
            return flow;
        }

        // Vertical scroll list; returns the content that children should be added to.
        public static RectTransform Scroll(Transform parent, out ScrollRect scroll)
        {
            var root = Rect("Scroll", parent);
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0); // receives drag/scroll
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            Vertical(content.gameObject, 10, 6);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        public static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        // Everything under it goes back to the pool (what the pool did not make is destroyed, as before).
        public static void Clear(Transform parent) => SoulUiPool.ReleaseChildren(parent);

        public static Canvas OverlayCanvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            return canvas;
        }

        public static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);
        public static string Colored(string text, Color color) => $"<color=#{Hex(color)}>{text}</color>";
    }
}
