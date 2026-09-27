using System;
using SoulMercenaries;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  ConfirmPopup.cs
//  확인/취소 팝업 (PopupType.Confirm).
//
//  사용법:
//    ConfirmPopup.Ask("dismiss", "정말 해고하시겠습니까?", () => { ... });
//
//  "다시 묻지 않기" 를 체크하면 그 종류(kind)는 이후 묻지 않고 바로 실행한다.
//  기록은 BattleSettingsData 에 남는다 (설정이므로 환생과 무관).
//  화면은 런타임에 만든다 (프리팹은 루트 + 이 컴포넌트뿐 — SoulMercenariesAssetBuilder).
// ============================================================
public sealed class ConfirmPopup : PopupBase
{
    string _kind;
    Action _yes;
    bool _skip;
    TextMeshProUGUI _question;
    Image _tick;

    static BattleSettingsData Settings => UserDataManager.Instance.Get<BattleSettingsData>();

    public static void Ask(string kind, string question, Action yes)
    {
        if (Settings != null && Settings.SkipsConfirm(kind)) { yes?.Invoke(); return; }
        var popup = PopupManager.Instance != null ? PopupManager.Instance.Open<ConfirmPopup>(PopupType.Confirm) : null;
        if (popup == null) { yes?.Invoke(); return; }
        popup.Show(kind, question, yes);
    }

    void Show(string kind, string question, Action yes)
    {
        if (_question == null) Build();
        _kind = kind;
        _yes = yes;
        _skip = false;
        _tick.enabled = false;
        _question.text = question;
    }

    void Build()
    {
        var root = (RectTransform)transform;
        SoulUi.Stretch(root);
        var shade = SoulUi.Panel("Shade", transform, new Color(0, 0, 0, .7f));
        SoulUi.Stretch(shade.rectTransform);
        shade.gameObject.AddComponent<Button>().onClick.AddListener(() => Close());

        var window = SoulUi.Framed("Window", transform, SoulUi.PanelBg, SoulUi.Accent, 3);
        window.anchorMin = window.anchorMax = new Vector2(.5f, .5f);
        window.sizeDelta = new Vector2(680, 300);
        window.GetComponent<Image>().raycastTarget = true;
        _question = SoulUi.Text(window, "", 28, Color.white, TextAlignmentOptions.Center);
        SoulUi.Place(_question.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, -160), new Vector2(-24, -20));

        var check = SoulUi.Rect("DontAsk", window);
        SoulUi.Place(check, Vector2.zero, Vector2.zero, new Vector2(28, 92), new Vector2(260, 128));
        check.gameObject.AddComponent<Image>().color = Color.clear;
        var box = SoulUi.Framed("Box", check, SoulUi.SlotBg, SoulUi.PanelBorder, 2);
        SoulUi.Place(box, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, -15), new Vector2(30, 15));
        _tick = SoulUi.Panel("Tick", box, SoulUi.Accent, true);
        SoulUi.Stretch(_tick.rectTransform, 6);
        var label = SoulUi.Text(check, "다시 묻지 않기", 20, SoulUi.SubText, TextAlignmentOptions.MidlineLeft);
        SoulUi.Place(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(42, 0), Vector2.zero);
        check.gameObject.AddComponent<Button>().onClick.AddListener(() => { _skip = !_skip; _tick.enabled = _skip; });

        var no = SoulUi.Button(window, "아니오", SoulUi.TabInactive, () => Close(), 22);
        SoulUi.Place((RectTransform)no.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-200, 20), new Vector2(-24, 76));
        var ok = SoulUi.Button(window, "예", new Color(.2f, .5f, .28f), Accept, 22);
        SoulUi.Place((RectTransform)ok.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-392, 20), new Vector2(-216, 76));
    }

    void Accept()
    {
        if (_skip && Settings != null)
        {
            Settings.SetSkipConfirm(_kind, true);
            UserDataManager.Instance.RequestSave();
        }
        var yes = _yes;
        _yes = null;
        Close();
        yes?.Invoke();
    }
}
