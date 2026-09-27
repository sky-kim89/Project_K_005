using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PopupManager))]
public class PopupManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(12);
        GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f);

        if (GUILayout.Button("Load Popup Prefabs From Folder", GUILayout.Height(32)))
            LoadPopupPrefabs((PopupManager)target);

        GUI.backgroundColor = Color.white;
    }

    public static void LoadPopupPrefabs(PopupManager manager)
    {
        const string folderPath = "Assets/_project/2.Prefabs/UI";

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogWarning($"[PopupManagerEditor] 폴더가 없습니다: {folderPath}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        var      found = new System.Collections.Generic.List<PopupBase>();

        foreach (string guid in guids)
        {
            string    path   = AssetDatabase.GUIDToAssetPath(guid);
            var       prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var       popup  = prefab != null ? prefab.GetComponent<PopupBase>() : null;
            if (popup != null)
                found.Add(popup);
        }

        var so   = new SerializedObject(manager);
        var prop = so.FindProperty("_prefabs");

        prop.arraySize = found.Count;
        for (int i = 0; i < found.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = found[i];

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();

        Debug.Log($"[PopupManagerEditor] {found.Count}개 팝업 프리팹 로드 완료 ({folderPath})");
    }
}
