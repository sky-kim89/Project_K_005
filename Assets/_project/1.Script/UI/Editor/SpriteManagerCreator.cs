#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

// ============================================================
//  SpriteManagerCreator.cs  [Editor Only]
//  영혼 용병단 아이콘 아틀라스 + SpriteManager.asset 생성.
//  SoulMercenariesAssetBuilder.BuildIcons() 가 아이콘을 만든 뒤 부른다.
// ============================================================

public static class SpriteManagerCreator
{
    const string ResourcesDir  = "Assets/Resources";
    const string ManagerPath   = "Assets/Resources/SpriteManager.asset";
    const string IconRoot      = "Assets/_project/3.Textures/Icons";
    const string SoulAtlasPath = IconRoot + "/Atlas_SoulMercenaries.spriteatlas";

    [MenuItem(ProjectKMenu.Data + "SpriteManager + 아틀라스", priority = ProjectKMenu.DataPrio + 20)]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder(ResourcesDir))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var soulAtlas = CreateAtlas(SoulAtlasPath, new[] { $"{IconRoot}/SoulMercenaries" });
        AssetDatabase.SaveAssets();

        var manager = AssetDatabase.LoadAssetAtPath<SpriteManager>(ManagerPath);
        if (manager == null)
        {
            manager = ScriptableObject.CreateInstance<SpriteManager>();
            AssetDatabase.CreateAsset(manager, ManagerPath);
        }

        var so = new SerializedObject(manager);
        so.Update();
        so.FindProperty("_soulAtlas").objectReferenceValue = soulAtlas;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ── 헬퍼 ─────────────────────────────────────────────────

    /// <summary>
    /// UI 아틀라스 패킹 설정. **회전·타이트 패킹을 반드시 끈다.**
    ///
    /// ⚠ enableRotation 이 켜져 있으면 아이콘이 화면에서 돌아간다
    ///   패커는 자리를 아끼려고 스프라이트를 90° 눕혀 넣는다. 스프라이트 자체는
    ///   회전 정보를 갖지만 **UGUI Image(Type.Simple)는 그 회전을 못 읽는다** —
    ///   UV 사각형만 그대로 쓰므로 화면에는 돌아간 그림이 나온다.
    ///   패커가 "넣기 좋을 때만" 눕히기 때문에 몇 장만 증상이 나타나,
    ///   원본 PNG 를 아무리 열어 봐도 멀쩡해서 원인을 찾기 어렵다.
    ///   (2026-08-26: '영웅의 기상'(ability_b11)이 뒤집혀 보인 원인이 이것이다)
    ///
    /// ⚠ enableTightPacking 도 UI 에서는 끈다
    ///   알파를 따라 다각형으로 잘라 넣으면 스프라이트 사각형과 실제 그림 영역이
    ///   어긋난다. Image 는 사각형 기준으로 그리므로 그림이 밀리거나 잘린다.
    ///   텍스처 쪽 Mesh Type = Full Rect 와 짝이다 (IconImportSetup 참고).
    ///
    /// 유니티 문서도 캔버스에서 쓰는 스프라이트는 두 옵션을 끄라고 명시한다.
    /// </summary>
    static void ApplyUIPackingSettings(SpriteAtlas atlas)
    {
        var packing = SpriteAtlasExtensions.GetPackingSettings(atlas);
        packing.enableRotation     = false;
        packing.enableTightPacking = false;
        packing.padding            = 4;
        SpriteAtlasExtensions.SetPackingSettings(atlas, packing);

        // 아이콘은 확대해서 쓰는 그림이라 압축 아티팩트가 그대로 보인다.
        var tex = SpriteAtlasExtensions.GetTextureSettings(atlas);
        tex.filterMode      = FilterMode.Bilinear;
        tex.generateMipMaps = false;
        tex.sRGB            = true;
        SpriteAtlasExtensions.SetTextureSettings(atlas, tex);
    }

    static SpriteAtlas CreateAtlas(string atlasPath, string[] sourceFolders)
    {
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
        if (atlas == null)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, atlasPath);
        }

        ApplyUIPackingSettings(atlas);

        // 기존 packable 초기화 후 폴더 재등록
        SpriteAtlasExtensions.Remove(atlas, SpriteAtlasExtensions.GetPackables(atlas));

        var packables = new Object[sourceFolders.Length];
        for (int i = 0; i < sourceFolders.Length; i++)
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(sourceFolders[i]);
            if (folder == null)
                Debug.LogWarning($"[SpriteManagerCreator] 폴더 없음(아직 미생성): {sourceFolders[i]}");
            packables[i] = folder;
        }

        SpriteAtlasExtensions.Add(atlas, packables);
        EditorUtility.SetDirty(atlas);
        return atlas;
    }
}
#endif
