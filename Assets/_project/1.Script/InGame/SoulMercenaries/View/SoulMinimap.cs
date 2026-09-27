using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // Explored-map texture (1 px per cell) with live markers. Used by the always-on HUD minimap and the
    // full map popup; the owner decides what a click means (camera jump or move order).
    public sealed class SoulMinimap : MonoBehaviour, IPointerClickHandler
    {
        static readonly Color Unknown = new Color(.03f, .035f, .06f, 1f);
        static readonly Color Floor = new Color(.45f, .34f, .22f, 1f);
        static readonly Color Wall = new Color(.22f, .22f, .24f, 1f);
        static readonly Color ExitColor = new Color(1f, .82f, .3f, 1f);
        static readonly Color LavaColor = new Color(.95f, .38f, .1f, 1f);
        static readonly Color DoorColor = new Color(.62f, .42f, .2f, 1f);

        public Action<Vector2Int> OnCellClicked;

        SoulGameplayController controller;
        SoulWorldView world;
        RawImage image;
        Texture2D texture;
        RectTransform markers;
        readonly List<Image> pool = new List<Image>();
        RectTransform view;
        int revision = -1, logged;
        bool exitShown;
        Color32[] pixels;
        float markerScale = 1;

        public static SoulMinimap Create(Transform parent, SoulGameplayController controller, SoulWorldView world, float markerScale = 1)
        {
            var rect = SoulUi.Rect("Minimap", parent);
            SoulUi.Stretch(rect);
            var minimap = rect.gameObject.AddComponent<SoulMinimap>();
            minimap.controller = controller;
            minimap.world = world;
            minimap.markerScale = markerScale;
            minimap.Build();
            return minimap;
        }

        void Build()
        {
            var map = controller.Session.Map;
            texture = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var fitter = SoulUi.Rect("Fit", transform);
            SoulUi.Stretch(fitter, 4);
            var aspect = fitter.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = map.Width / (float)map.Height;
            image = fitter.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            markers = SoulUi.Rect("Markers", fitter);
            SoulUi.Stretch(markers);
            // camera view: four thin edges (an Outline effect would fill the whole quad)
            view = SoulUi.Rect("View", markers);
            foreach (var edge in new[] { (Vector2.zero, new Vector2(1, 0)), (new Vector2(0, 1), Vector2.one), (Vector2.zero, new Vector2(0, 1)), (new Vector2(1, 0), Vector2.one) })
            {
                var line = SoulUi.Panel("Edge", view, new Color(1, 1, 1, .85f));
                line.raycastTarget = false;
                line.rectTransform.anchorMin = edge.Item1;
                line.rectTransform.anchorMax = edge.Item2;
                line.rectTransform.sizeDelta = new Vector2(2, 2);
            }
        }

        void OnDestroy() { if (texture != null) Destroy(texture); }

        void Update()
        {
            var session = controller.Session;
            if (session == null) return;
            if (revision != session.ExploreRevision || exitShown != session.ExitKnown) Paint(session);
            var size = ((RectTransform)image.transform).rect.size;
            var map = session.Map;
            int used = 0;
            foreach (var monster in session.Monsters)
                if (monster.Alive && session.Knows(monster)) // guardians show through the fog for a pathfinder
                    Marker(ref used, monster.Position, monster.Data.Guardian ? new Color(.85f, .35f, 1f) : SoulUi.Bad, monster.Data.Guardian ? 11 : 7, size);
            foreach (var thing in session.Interactables)
                if (!thing.Used && session.Knows(thing)) Marker(ref used, thing.Position, new Color(.3f, .9f, 1f), 8, size);
            foreach (var stage in session.HiddenStages)
                if (session.Knows(stage)) Marker(ref used, stage.Rect.center, stage.Found ? new Color(.7f, .3f, .9f) : new Color(1f, .35f, 1f), 12, size);
            foreach (var chest in session.Chests)
                if (!chest.Opened && session.Knows(chest)) Marker(ref used, chest.Position, ExitColor, 10, size);
            var selected = controller.SelectedMercenary;
            foreach (var hero in session.Mercenaries)
                if (hero.Alive) Marker(ref used, hero.Position, hero == selected ? new Color(.3f, 1f, 1f) : SoulUi.Good, hero == selected ? 11 : 8, size);
            for (int i = used; i < pool.Count; i++) pool[i].enabled = false;

            if (world != null)
            {
                var rect = world.ViewRect; // map coords, y down
                view.anchorMin = view.anchorMax = Vector2.zero;
                view.pivot = new Vector2(0, 1);
                view.anchoredPosition = new Vector2(rect.xMin / map.Width * size.x, (1 - rect.yMin / map.Height) * size.y);
                view.sizeDelta = new Vector2(rect.width / map.Width * size.x, rect.height / map.Height * size.y);
                view.SetAsLastSibling();
            }
        }

        void Marker(ref int used, Vector2 position, Color color, float pixels, Vector2 size)
        {
            if (used >= pool.Count)
            {
                var dot = SoulUi.Panel("Dot", markers, Color.white, true);
                dot.raycastTarget = false;
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = Vector2.zero;
                pool.Add(dot);
            }
            var marker = pool[used++];
            var map = controller.Session.Map;
            marker.enabled = true;
            marker.color = color;
            marker.rectTransform.sizeDelta = Vector2.one * pixels * markerScale;
            marker.rectTransform.anchoredPosition = new Vector2(position.x / map.Width * size.x, (1 - position.y / map.Height) * size.y);
        }

        // First call paints everything unknown; later calls only the tiles explored since (append-only log).
        void Paint(SoulDungeonSession session)
        {
            revision = session.ExploreRevision;
            exitShown = session.ExitKnown;
            var map = session.Map;
            if (pixels == null)
            {
                pixels = new Color32[map.Width * map.Height];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Unknown;
            }
            var log = session.ExploreLog;
            for (; logged < log.Count; logged++) PaintCell(session, new Vector2Int(log[logged] % map.Width, log[logged] / map.Width));
            foreach (var exit in session.Exits) PaintCell(session, exit);
            texture.SetPixels32(pixels);
            texture.Apply();
        }

        void PaintCell(SoulDungeonSession session, Vector2Int cell)
        {
            var map = session.Map;
            var tile = map.Tile(cell);
            Color color = session.IsExit(cell) && session.IsExitKnown(cell) ? ExitColor
                : !session.IsExplored(cell) ? Unknown
                : tile == SoulTile.Lava ? LavaColor
                : tile == SoulTile.HiddenDoor ? (map.IsDiscovered(cell) ? DoorColor : Wall)
                : map.Open(cell) ? Floor : Wall;
            pixels[(map.Height - 1 - cell.y) * map.Width + cell.x] = color; // texture rows go bottom-up
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var rect = (RectTransform)image.transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out var local)) return;
            var size = rect.rect.size;
            var map = controller.Session.Map;
            float u = (local.x - rect.rect.xMin) / size.x, v = (local.y - rect.rect.yMin) / size.y;
            if (u < 0 || u > 1 || v < 0 || v > 1) return;
            OnCellClicked?.Invoke(new Vector2Int(Mathf.Clamp((int)(u * map.Width), 0, map.Width - 1), Mathf.Clamp((int)((1 - v) * map.Height), 0, map.Height - 1)));
        }
    }
}
