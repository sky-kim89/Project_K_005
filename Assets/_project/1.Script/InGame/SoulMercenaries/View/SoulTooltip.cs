using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // Tooltip (icon, title, icon rows, section headers). Lives on its own overlay canvas above popups (200)
    // so it works from the HUD and from PopupManager popups alike. Show() opens it on click; Attach() opens it
    // on mouse hover and on tap (mobile). Any later click or tap closes it.
    public sealed class SoulTooltip : MonoBehaviour
    {
        const float Width = 520;

        static SoulTooltip instance;
        RectTransform panel, rows, costs, canvasRect;
        Image icon;
        TextMeshProUGUI title;
        int openedFrame, hideFrame = -1;

        public static bool IsOpen => instance != null && instance.panel.gameObject.activeSelf;

        // A hovered element that is rebuilt (a list refreshed under the mouse) leaves and a new one enters a frame
        // later: the hide waits two frames so the reopen replaces it instead of the tooltip blinking.
        public static void HideSoon()
        {
            if (instance != null && instance.panel.gameObject.activeSelf) instance.hideFrame = Time.frameCount;
        }

        public static void Show(RectTransform anchor, Sprite titleIcon, string titleText, IEnumerable<SoulLine> lines)
        {
            if (instance == null) Create();
            instance.Open(anchor, titleIcon, titleText, lines);
        }

        // Hover (and click, for touch) opens the tooltip; the content is built when it opens so it stays current.
        public static void Attach(Component target, Func<Sprite> icon, Func<string> title, Func<IEnumerable<SoulLine>> lines)
        {
            var graphic = target.GetComponent<Graphic>();
            if (graphic == null)
            {
                var hit = target.gameObject.AddComponent<Image>(); // plain rows need something to hover
                hit.color = Color.clear;
                graphic = hit;
            }
            graphic.raycastTarget = true;
            var hover = target.gameObject.AddComponent<SoulTooltipHover>();
            hover.Open = () => Show((RectTransform)target.transform, icon(), title(), lines());
        }

        public static void Attach(Component target, Sprite icon, string title, Func<IEnumerable<SoulLine>> lines)
            => Attach(target, () => icon, () => title, lines);

        public static void Hide()
        {
            if (instance != null) instance.panel.gameObject.SetActive(false);
        }

        static void Create()
        {
            var canvas = SoulUi.OverlayCanvas("SoulTooltipCanvas", 300);
            instance = canvas.gameObject.AddComponent<SoulTooltip>();
            instance.canvasRect = (RectTransform)canvas.transform;
            var frame = SoulUi.Framed("Tooltip", canvas.transform, new Color(.05f, .055f, .1f, 1f), SoulUi.Accent, 2); // opaque: in linear colour even 2% of the gold behind turns it brown
            frame.GetComponent<Image>().raycastTarget = false; // never steal the hover from what it describes
            frame.pivot = new Vector2(0, 1);
            frame.anchorMin = frame.anchorMax = new Vector2(0, 1);
            frame.sizeDelta = new Vector2(Width, 100);
            var layout = SoulUi.Vertical(frame.gameObject, 8, 16);
            layout.childForceExpandWidth = true;
            var fitter = frame.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // the frame's inner fill must not take part in the layout
            SoulUi.Layout(frame.GetChild(0).gameObject, -1, -1).ignoreLayout = true; // (a recycled object may already carry one)

            var header = SoulUi.Row(frame, 44, 10);
            instance.icon = SoulUi.Icon(header, null, 44);
            instance.title = SoulUi.Text(header, "", 26, Color.white);
            instance.title.fontStyle = FontStyles.Bold;
            SoulUi.Layout(instance.title.gameObject, -1, -1, 1);
            // the price at the right end of the title (stamina / MP / HP icons with their numbers)
            instance.costs = SoulUi.Rect("Costs", header);
            var costLayout = SoulUi.Horizontal(instance.costs.gameObject, 4);
            costLayout.childAlignment = TextAnchor.MiddleRight;
            costLayout.childForceExpandHeight = false;
            instance.rows = SoulUi.Rect("Rows", frame);
            SoulUi.Vertical(instance.rows.gameObject, 6);
            instance.panel = frame;
            frame.gameObject.SetActive(false);
        }

        void Open(RectTransform anchor, Sprite titleIcon, string titleText, IEnumerable<SoulLine> lines)
        {
            icon.sprite = titleIcon;
            icon.enabled = titleIcon != null;
            title.text = titleText;
            SoulUi.Clear(rows);
            SoulUi.Clear(costs);
            bool priced = false;
            foreach (var line in lines)
            {
                if (line.Cost)
                {
                    if (priced) SoulUi.Layout(SoulUi.Rect("Gap", costs).gameObject, 8, 1);
                    SoulUi.Icon(costs, line.Icon, 28);
                    var price = SoulUi.Text(costs, line.Text, 22, Color.white, TextAlignmentOptions.MidlineRight);
                    price.fontStyle = FontStyles.Bold;
                    price.textWrappingMode = TextWrappingModes.NoWrap;
                    priced = true;
                    continue;
                }
                if (line.Header)
                {
                    var header = SoulUi.Text(rows, line.Text, 18, SoulUi.Accent, TextAlignmentOptions.BottomLeft);
                    header.fontStyle = FontStyles.Bold;
                    SoulUi.Layout(header.gameObject, -1, 30);
                    var rule = SoulUi.Panel("Rule", rows, new Color(SoulUi.Accent.r, SoulUi.Accent.g, SoulUi.Accent.b, .35f));
                    rule.raycastTarget = false;
                    SoulUi.Layout(rule.gameObject, -1, 2);
                    continue;
                }
                var row = SoulUi.Rect("Line", rows);
                var layout = SoulUi.Horizontal(row.gameObject, 8);
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childForceExpandHeight = false;
                if (line.Icon != null) SoulUi.Icon(row, line.Icon, 28);
                var text = SoulUi.Text(row, line.Text, 20, line.Icon == null ? SoulUi.SubText : Color.white, TextAlignmentOptions.TopLeft);
                SoulUi.Layout(text.gameObject, -1, -1, 1);
            }
            costs.gameObject.SetActive(priced);
            panel.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            Place(anchor);
            openedFrame = Time.frameCount;
            hideFrame = -1;
        }

        // Right of the anchor when it fits, otherwise left; always inside the screen.
        void Place(RectTransform anchor)
        {
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector2 topRight, topLeft;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, RectTransformUtility.WorldToScreenPoint(null, corners[2]), null, out topRight);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, RectTransformUtility.WorldToScreenPoint(null, corners[1]), null, out topLeft);
            Vector2 size = panel.rect.size, canvas = canvasRect.rect.size;
            // local points are relative to the canvas center; the panel anchors at its top-left corner
            float x = topRight.x + canvas.x / 2 + 8;
            if (x + size.x > canvas.x) x = topLeft.x + canvas.x / 2 - size.x - 8;
            float y = topRight.y - canvas.y / 2;
            x = Mathf.Clamp(x, 8, canvas.x - size.x - 8);
            y = Mathf.Clamp(y, -canvas.y + size.y + 8, -8);
            panel.anchoredPosition = new Vector2(x, y);
        }

        void Update()
        {
            if (panel.gameObject.activeSelf && Input.GetMouseButtonDown(0) && Time.frameCount != openedFrame) Hide();
            if (hideFrame >= 0 && Time.frameCount > hideFrame + 1) { hideFrame = -1; Hide(); }
        }
    }

    sealed class SoulTooltipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Action Open;
        bool shown;
        public void OnPointerEnter(PointerEventData eventData) { shown = true; Open?.Invoke(); }
        // A touch "leaves" the moment the finger lifts (right after the click), so on touch the tooltip stays
        // until the next tap anywhere; only a mouse (negative pointer ids) closes it by moving away.
        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.pointerId < 0) Close();
        }
        public void OnPointerClick(PointerEventData eventData) { shown = true; Open?.Invoke(); }
        void OnDisable() => Close();

        void Close()
        {
            if (shown) SoulTooltip.HideSoon();
            shown = false;
        }
    }
}
