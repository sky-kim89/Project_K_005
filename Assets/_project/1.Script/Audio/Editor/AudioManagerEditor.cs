using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
//  AudioManagerEditor.cs  [Editor Only]
//  AudioManager 인스펙터의 [Load Audio Clips From Folder] 버튼.
//
//  SFX/ 와 BGM/ 을 각각 훑어 _clips · _bgmClips 배열을 다시 채운다.
//  분류 규칙은 런타임과 같다 — **파일명이 곧 enum 값**이다.
//  (UI_Click.wav → SfxKey.UI_Click,  Lobby.mp3 → BgmKey.Lobby)
// ============================================================

[CustomEditor(typeof(AudioManager))]
public class AudioManagerEditor : Editor
{
    const string SfxFolder = "Assets/_project/5.Audio/SFX";
    const string BgmFolder = "Assets/_project/5.Audio/BGM";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(12);
        GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f);

        if (GUILayout.Button("Load Audio Clips From Folder", GUILayout.Height(32)))
            LoadClips((AudioManager)target);

        GUI.backgroundColor = Color.white;
    }

    public static void LoadClips(AudioManager manager)
    {
        var so = new SerializedObject(manager);

        int sfx = Fill<SfxKey>(so, "_clips",    SfxFolder);
        int bgm = Fill<BgmKey>(so, "_bgmClips", BgmFolder);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AudioManagerEditor] 효과음 {sfx}개 · 배경음 {bgm}개 로드 완료");
    }

    /// <summary>
    /// 폴더의 클립 중 이름이 T(enum) 값과 일치하는 것만 배열 프로퍼티에 채운다.
    ///
    /// ⚠ 이름이 안 맞는 파일은 배열에 넣지 않는다
    ///   넣어 두면 런타임에 경고만 쌓이고 소리는 안 난다 — 여기서 걸러야
    ///   "왜 무음인지" 를 임포트 시점에 알 수 있다.
    /// </summary>
    static int Fill<T>(SerializedObject so, string field, string folder) where T : struct, Enum
    {
        var prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogError($"[AudioManagerEditor] AudioManager 에 '{field}' 필드가 없습니다.");
            return 0;
        }

        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning($"[AudioManagerEditor] 폴더가 없습니다: {folder}");
            prop.arraySize = 0;
            return 0;
        }

        var found = new List<AudioClip>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip == null) continue;

            if (!Enum.TryParse(clip.name, out T key) || Convert.ToInt32(key) == 0)
            {
                Debug.LogWarning($"[AudioManagerEditor] '{clip.name}' 은 {typeof(T).Name} 에 없는 이름이라 건너뜁니다.");
                continue;
            }
            found.Add(clip);
        }

        prop.arraySize = found.Count;
        for (int i = 0; i < found.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = found[i];

        // 연결이 빠진 키를 바로 알려 준다 — 무음으로 남으면 원인을 찾기 어렵다.
        foreach (T key in Enum.GetValues(typeof(T)))
        {
            if (Convert.ToInt32(key) == 0) continue;   // None
            if (!found.Exists(c => c.name == key.ToString()))
                Debug.LogWarning($"[AudioManagerEditor] {typeof(T).Name}.{key} 에 해당하는 클립이 없습니다 — 무음으로 남습니다.");
        }

        return found.Count;
    }
}
