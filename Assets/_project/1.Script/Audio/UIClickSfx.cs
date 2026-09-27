using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  UIClickSfx.cs
//  씬·팝업 안의 모든 Button 에 클릭음을 한 번에 붙인다.
//
//  ■ 왜 버튼마다 컴포넌트를 달지 않나
//    버튼이 수백 개고, 대부분 에디터 Creator 가 코드로 만든다.
//    Creator 마다 소리 코드를 넣으면 새 버튼을 만들 때마다 빠뜨린다.
//    루트를 훑어 한 번에 거는 쪽이 빠뜨릴 일이 없다.
//
//  ■ 어디서 부르나
//    · PopupBase.OnAfterOpen  → 팝업 안의 버튼 (풀에서 재사용돼도 매번 검사)
//    · 씬 UI 루트             → Start 에서 한 번
//
//  ■ 뒤로 가는 버튼은 소리가 다르다
//    이름에 Close/Cancel/Back/Exit 가 들어가면 UI_Click_Back 을 쓴다.
//    앞으로/뒤로가 귀로 구분돼야 조작이 명확해진다.
//
//  ⚠ 두 번 걸리지 않게 표식을 남긴다
//    같은 버튼에 리스너가 두 번 붙으면 클릭 한 번에 소리가 두 번 난다.
// ============================================================

[DisallowMultipleComponent]
public class UIClickSfxMark : MonoBehaviour { }   // "이미 걸었음" 표식

public static class UIClickSfx
{
    static readonly string[] BackWords = { "close", "cancel", "back", "exit", "quit", "no" };

    /// <summary>root 아래의 모든 Button 에 클릭음을 건다. 이미 걸린 버튼은 건너뛴다.</summary>
    public static void Bind(GameObject root)
    {
        if (root == null) return;

        foreach (var btn in root.GetComponentsInChildren<Button>(true))
        {
            if (btn.GetComponent<UIClickSfxMark>() != null) continue;
            btn.gameObject.AddComponent<UIClickSfxMark>();

            SfxKey key = IsBackButton(btn.name) ? SfxKey.UI_Click_Back : SfxKey.UI_Click;
            btn.onClick.AddListener(() => AudioManager.Instance?.Play(key));
        }
    }

    static bool IsBackButton(string name)
    {
        string n = name.ToLowerInvariant();
        foreach (var w in BackWords)
            if (n.Contains(w)) return true;
        return false;
    }
}
