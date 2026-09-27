using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // Full dungeon map (PopupType.SoulMap). Clicking an explored cell gives a move order — for the selected
    // mercenary or the whole party, same as clicking the world. The battle keeps running behind it.
    public sealed class SoulMapPopup : PopupBase
    {
        const float Width = 1640, Height = 940;

        SoulGameplayController controller;
        SoulWorldView world;
        SoulMinimap minimap;
        TextMeshProUGUI modeLabel;
        bool built;

        public static void Open(SoulGameplayController controller, SoulWorldView world)
        {
            var manager = PopupManager.Instance;
            if (manager == null) { Debug.LogWarning("[SoulMapPopup] PopupManager 가 씬에 없습니다."); return; }
            var popup = manager.IsOpen(PopupType.SoulMap) ? manager.Get<SoulMapPopup>(PopupType.SoulMap) : manager.Open<SoulMapPopup>(PopupType.SoulMap);
            popup?.Bind(controller, world);
        }

        void Bind(SoulGameplayController controller, SoulWorldView world)
        {
            this.controller = controller;
            this.world = world;
            if (built) return;
            built = true;

            var root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
            root.sizeDelta = new Vector2(Width, Height);
            var frame = SoulUi.Framed("Frame", transform, SoulUi.PanelBg, SoulUi.PanelBorder, 3);
            SoulUi.Stretch(frame);

            var header = SoulUi.Row(frame, 60, 14);
            SoulUi.Place(header, new Vector2(0, 1), Vector2.one, new Vector2(20, -74), new Vector2(-90, -14));
            SoulUi.Icon(header, SoulIconSet.Ui("map"), 48);
            var title = SoulUi.Text(header, "던전 지도", 32, Color.white);
            title.fontStyle = FontStyles.Bold;
            SoulUi.Layout(title.gameObject, 200, -1);
            modeLabel = SoulUi.Text(header, "", 22, SoulUi.SubText);
            SoulUi.Layout(modeLabel.gameObject, -1, -1, 1);
            Legend(header, SoulIconSet.Ui("dot_hero"), "용병");
            Legend(header, SoulIconSet.Ui("dot_monster"), "몬스터");
            Legend(header, SoulIconSet.Ui("exit"), "출구");

            var close = SoulUi.Button(frame, SoulIconSet.Ui("close") == null ? "X" : "", SoulUi.Bad, () => Close(), 26, SoulIconSet.Ui("close"));
            SoulUi.Place((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-72, -70), new Vector2(-20, -18));

            var area = SoulUi.Panel("Area", frame, new Color(0, 0, 0, .4f), true).rectTransform;
            SoulUi.Place(area, Vector2.zero, Vector2.one, new Vector2(20, 20), new Vector2(-20, -86));
            minimap = SoulMinimap.Create(area, controller, world, 2.2f);
            minimap.OnCellClicked = cell => world.Command(cell);
        }

        static void Legend(Transform parent, Sprite icon, string label)
        {
            SoulUi.Icon(parent, icon, 30);
            var text = SoulUi.Text(parent, label, 20, SoulUi.SubText);
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        void Update()
        {
            if (controller == null || modeLabel == null) return;
            var hero = controller.SelectedMercenary;
            modeLabel.text = controller.MoveParty ? "클릭: 파티 전체 이동" : hero != null ? $"클릭: {hero.Name} 이동" : "";
        }
    }
}
