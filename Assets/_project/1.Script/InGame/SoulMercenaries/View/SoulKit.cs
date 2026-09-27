using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // Building blocks of the village screens: pictures first, words on hover. A body splits into the main area
    // (grids of tiles and cards that fill the width) and a side panel (what is selected, and what to do with it).
    public static class SoulKit
    {
        public static readonly Color PaneDark = new Color(.035f, .04f, .075f, .9f);
        public static readonly Color CardBg = new Color(.075f, .085f, .14f, 1f); // opaque: a frame colour behind would tint it (linear blending)
        public static readonly Color Empty = new Color(.22f, .23f, .3f);

        // main (flexible) | side (fixed width, right)
        public static (RectTransform main, RectTransform side) Split(RectTransform body, float sideWidth = 520, float gap = 16)
        {
            var main = SoulUi.Rect("Main", body);
            SoulUi.Place(main, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-sideWidth - gap, 0));
            var side = SoulUi.Rect("Side", body);
            SoulUi.Place(side, new Vector2(1, 0), Vector2.one, new Vector2(-sideWidth, 0), Vector2.zero);
            return (main, side);
        }

        // A scrolling column filling the area; returns where content goes.
        public static RectTransform Scroll(RectTransform area, float padding = 4)
        {
            var content = SoulUi.Scroll(area, out var scroll);
            SoulUi.Stretch((RectTransform)scroll.transform);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout != null) layout.padding = new RectOffset((int)padding, (int)padding + 8, (int)padding, (int)padding);
            return content;
        }

        // A row that slides sideways (drag it, or turn the wheel): fixed-size cards side by side filling the area's
        // height. Returns where the cards go.
        public static RectTransform Slider(RectTransform area, float spacing, out ScrollRect scroll)
        {
            var root = SoulUi.Rect("Slider", area);
            SoulUi.Stretch(root);
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false; // the wheel then slides it sideways
            scroll.scrollSensitivity = 40;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = SoulUi.Rect("Viewport", root);
            SoulUi.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0); // receives drag/scroll
            var content = SoulUi.Rect("Content", viewport);
            content.anchorMin = Vector2.zero;
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, .5f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            SoulUi.Horizontal(content.gameObject, spacing).childAlignment = TextAnchor.MiddleLeft;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        // The side panel: a dark card with its own scroll.
        public static RectTransform Side(RectTransform side)
        {
            var panel = SoulUi.Panel("Panel", side, new Color(.06f, .07f, .12f, .97f), true).rectTransform;
            SoulUi.Stretch(panel);
            var content = Scroll(panel, 14);
            content.GetComponent<VerticalLayoutGroup>().spacing = 10;
            return content;
        }

        // A short title with an optional count beside it — no explanations.
        public static void Section(Transform parent, string title, string count = null)
        {
            var row = SoulUi.Row(parent, 34, 10);
            var label = SoulUi.Text(row, title, 22, SoulUi.Accent, TextAlignmentOptions.BottomLeft);
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            SoulUi.Layout(label.gameObject, -1, -1);
            if (count != null)
            {
                var small = SoulUi.Text(row, count, 18, SoulUi.SubText, TextAlignmentOptions.BottomLeft);
                small.textWrappingMode = TextWrappingModes.NoWrap;
                SoulUi.Layout(small.gameObject, -1, -1, 1);
            }
        }

        public static RectTransform Grid(Transform parent, Vector2 cell, float spacing = 8)
        {
            var grid = SoulUi.Flow(parent, cell, spacing);
            return grid;
        }

        // A square tile: icon in a frame (grade colour), an optional badge (count, price, level) in the corner.
        public static RectTransform Tile(Transform parent, Sprite icon, float size, Color frame, string badge = null, Color? badgeColor = null,
            bool selected = false, bool dim = false, UnityAction onClick = null)
        {
            var box = SoulUi.Framed("Tile", parent, new Color(.05f, .055f, .09f, 1f), frame, selected ? 3 : 2);
            SoulUi.Layout(box.gameObject, size, size);
            var image = SoulUi.Icon(box, icon, size - 12);
            SoulUi.Stretch(image.rectTransform, 7);
            if (selected)
            {
                // chosen: an accent outline (four thin bars) inside the (grade) frame, so the grade still shows
                const float inset = 4, width = 3;
                foreach (var (min, max, from, to) in new[]
                {
                    (new Vector2(0, 1), new Vector2(1, 1), new Vector2(inset, -inset - width), new Vector2(-inset, -inset)),
                    (new Vector2(0, 0), new Vector2(1, 0), new Vector2(inset, inset), new Vector2(-inset, inset + width)),
                    (new Vector2(0, 0), new Vector2(0, 1), new Vector2(inset, inset), new Vector2(inset + width, -inset)),
                    (new Vector2(1, 0), new Vector2(1, 1), new Vector2(-inset - width, inset), new Vector2(-inset, -inset)),
                })
                {
                    var bar = SoulUi.Panel("Chosen", box, SoulUi.Accent, true);
                    bar.raycastTarget = false;
                    SoulUi.Place(bar.rectTransform, min, max, from, to);
                }
            }
            image.color = new Color(1, 1, 1, dim ? .3f : 1f);
            if (!string.IsNullOrEmpty(badge))
            {
                var pill = SoulUi.Panel("Badge", box, new Color(0, 0, 0, .78f), true);
                pill.raycastTarget = false;
                SoulUi.Place(pill.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-Mathf.Max(26, 10 + badge.Length * 10), 3), new Vector2(-3, 25));
                var text = SoulUi.Text(pill.transform, badge, 16, badgeColor ?? Color.white, TextAlignmentOptions.Center);
                text.fontStyle = FontStyles.Bold;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                SoulUi.Stretch(text.rectTransform);
            }
            if (onClick != null)
            {
                var button = SoulUi.Clickable(box.gameObject);
                var colors = button.colors;
                colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
                button.colors = colors;
                button.onClick.AddListener(onClick);
            }
            return box;
        }

        // Icon + number (the name is in the tooltip).
        public static RectTransform IconValue(Transform parent, Sprite icon, string value, Color color, float height = 36)
        {
            var chip = SoulUi.Chip(parent, icon, value, color, height);
            chip.GetComponent<Image>().raycastTarget = true;
            return chip;
        }

        public static Button Action(Transform parent, string label, Color color, bool enabled, UnityAction onClick, float height = 52, Sprite icon = null)
        {
            var button = SoulUi.Button(parent, label, enabled ? color : SoulUi.TabInactive, onClick, 20, icon);
            button.interactable = enabled;
            SoulUi.Layout(button.gameObject, -1, height);
            return button;
        }

        // The monster's baked picture (SoulMonsterData.Portrait, one sheet for all): sprite-library monsters are cut
        // to their body and drawn whole; part-built ones (goblins) are a full frame, zoomed onto the body like the
        // mercenary portraits. No picture: the soul orb.
        public static void MonsterPicture(RectTransform box, SoulMonsterData monster, float size)
        {
            var sprite = monster != null ? monster.Portrait : null;
            if (sprite != null && !monster.UseMonsterSprite)
            {
                box.gameObject.AddComponent<RectMask2D>();
                var face = SoulUi.Icon(box, sprite, 10, "Sprite");
                face.rectTransform.anchorMin = face.rectTransform.anchorMax = new Vector2(.5f, 0);
                face.rectTransform.pivot = new Vector2(.5f, 0);
                face.rectTransform.sizeDelta = new Vector2(size * 3.2f, size * 3.2f);
                face.rectTransform.anchoredPosition = new Vector2(0, -size * .62f);
                return;
            }
            var icon = SoulUi.Icon(box, sprite ?? SoulIconSet.Ui("soul"), 10);
            SoulUi.Stretch(icon.rectTransform, 3);
        }

        // The mercenary's job as a small badge in a corner of its portrait (corner: (0,0) bottom-left … (1,1) top-right).
        public static Image JobBadge(RectTransform portrait, SoulMercenary hero, float size, Vector2 corner)
        {
            var sprite = hero != null ? SoulIconSet.Job(hero.Job) : null;
            if (sprite == null) return null;
            var badge = SoulUi.Icon(portrait, sprite, size, "Job");
            var rect = badge.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = new Vector2(corner.x < .5f ? 2 : -2, corner.y < .5f ? 2 : -2);
            rect.sizeDelta = new Vector2(size, size);
            var layout = badge.GetComponent<LayoutElement>();
            if (layout != null) layout.ignoreLayout = true;
            return badge;
        }

        // A small picture of a soul for icons in lines and tooltip titles (the baked portrait, else the orb).
        public static Sprite SoulIcon(SoulData soul) => soul != null && soul.Source != null && soul.Source.Portrait != null ? soul.Source.Portrait : SoulIconSet.Ui("soul");

        // A soul as a tile: its monster's picture in the frame (badge, selection and dimming as any tile).
        public static RectTransform SoulTile(Transform parent, SoulData soul, float size, Color frame, string badge = null, Color? badgeColor = null,
            bool selected = false, bool dim = false, UnityAction onClick = null)
        {
            var tile = Tile(parent, null, size, frame, badge, badgeColor, selected, dim, onClick);
            var picture = SoulUi.Rect("Picture", tile);
            SoulUi.Stretch(picture, 5);
            var icon = tile.Find("Icon");
            picture.SetSiblingIndex(icon != null ? icon.GetSiblingIndex() + 1 : 1); // under the badge and the selection bars
            MonsterPicture(picture, soul != null ? soul.Source : null, size - 10);
            if (dim) foreach (var image in picture.GetComponentsInChildren<Image>()) image.color = new Color(1, 1, 1, .3f);
            return tile;
        }

        // Filled / empty dots (wounds, levels).
        public static void Pips(Transform parent, int filled, int max, Color on, Color off, float size = 12)
        {
            var row = SoulUi.Row(parent, size, 3);
            for (int i = 0; i < max; i++)
            {
                var dot = SoulUi.Panel("Pip", row, i < filled ? on : off, true);
                dot.raycastTarget = false;
                SoulUi.Layout(dot.gameObject, size, size);
            }
        }

        // Tooltip-style lines, drawn inline in a panel (the side panel's details).
        public static void Lines(Transform parent, IEnumerable<SoulLine> lines)
        {
            foreach (var line in lines)
            {
                if (line.Cost) continue; // a price belongs to a title
                if (line.Header)
                {
                    var header = SoulUi.Text(parent, line.Text, 18, SoulUi.Accent, TextAlignmentOptions.BottomLeft);
                    header.fontStyle = FontStyles.Bold;
                    SoulUi.Layout(header.gameObject, -1, 28);
                    continue;
                }
                var row = SoulUi.Rect("Line", parent);
                var layout = SoulUi.Horizontal(row.gameObject, 8);
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childForceExpandHeight = false;
                if (line.Icon != null) SoulUi.Icon(row, line.Icon, 26);
                var text = SoulUi.Text(row, line.Text, 18, line.Icon == null ? SoulUi.SubText : Color.white, TextAlignmentOptions.TopLeft);
                SoulUi.Layout(text.gameObject, -1, -1, 1);
            }
        }

        // A big header for the side panel: icon or portrait on the left, title and a subtitle.
        public static RectTransform Header(Transform parent, RectTransform picture, string title, string subtitle)
        {
            var row = SoulUi.Row(parent, 96, 14);
            if (picture != null) picture.SetParent(row, false);
            var texts = SoulUi.Rect("Texts", row);
            SoulUi.Vertical(texts.gameObject, 2);
            SoulUi.Layout(texts.gameObject, -1, -1, 1);
            var name = SoulUi.Text(texts, title, 26, Color.white, TextAlignmentOptions.BottomLeft);
            name.fontStyle = FontStyles.Bold;
            SoulUi.Layout(name.gameObject, -1, 40);
            var sub = SoulUi.Text(texts, subtitle, 17, SoulUi.SubText, TextAlignmentOptions.TopLeft);
            SoulUi.Layout(sub.gameObject, -1, 50);
            return row;
        }

        // ── drag & drop ──────────────────────────────────────────

        public static void Draggable(Component target, string payload, Sprite icon, Action droppedOutside = null)
        {
            var source = target.gameObject.AddComponent<SoulDragSource>();
            source.Payload = payload;
            source.Icon = icon;
            source.DroppedOutside = droppedOutside;
        }

        public static void DropZone(Component target, Func<string, bool> accept)
        {
            var graphic = target.GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = true;
            target.gameObject.AddComponent<SoulDropTarget>().Accept = accept;
        }
    }

    // Drag a tile: a ghost of its icon follows the pointer; letting go over a drop zone hands the payload over.
    public sealed class SoulDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public string Payload;
        public Sprite Icon;
        public Action DroppedOutside;
        static Canvas layer;
        Image ghost;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (layer == null) layer = SoulUi.OverlayCanvas("SoulDragLayer", 400);
            ghost = SoulUi.Icon(layer.transform, Icon, 72, "Ghost");
            ghost.color = new Color(1, 1, 1, .85f);
            ghost.raycastTarget = false;
            SoulTooltip.Hide();
            Move(eventData);
        }

        public void OnDrag(PointerEventData eventData) => Move(eventData);

        void Move(PointerEventData eventData)
        {
            if (ghost == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)layer.transform, eventData.position, null, out var local);
            ghost.rectTransform.anchoredPosition = local;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (ghost != null) Destroy(ghost.gameObject);
            var hit = eventData.pointerCurrentRaycast.gameObject;
            var zone = hit != null ? hit.GetComponentInParent<SoulDropTarget>() : null;
            if (zone != null && zone.Accept != null && zone.Accept(Payload)) return;
            DroppedOutside?.Invoke();
        }
    }

    public sealed class SoulDropTarget : MonoBehaviour
    {
        public Func<string, bool> Accept;
    }
}
