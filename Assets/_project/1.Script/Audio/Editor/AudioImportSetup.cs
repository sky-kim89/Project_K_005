using UnityEditor;
using UnityEngine;

// ============================================================
//  AudioImportSetup.cs  [Editor Only]
//  Assets/_project/5.Audio 아래 클립의 임포트 설정을 자동으로 잡는다.
//
//  ■ 왜 후처리기인가
//    새 효과음을 넣을 때마다 인스펙터에서 손으로 고르면 반드시 빠뜨린다.
//    파일을 폴더에 떨구는 순간 설정이 붙게 한다.
//
//  ■ 설정 근거 — 효과음 (SFX/)
//    ForceToMono          — 2D 재생이라 스테레오가 의미 없고 메모리만 두 배
//    DecompressOnLoad     — 1초 미만 효과음은 압축 해제 비용이 재생 지연으로 온다
//    PreloadAudioData     — 첫 타격에서 끊기지 않게 미리 올린다 (파일이 작아 부담 없음)
//    LoadInBackground off — 미리 올릴 거라 필요 없다
//
//  ■ 설정 근거 — 배경음 (BGM/)
//    ⚠ 효과음 설정을 그대로 쓰면 안 된다
//      몇 분짜리 곡을 DecompressOnLoad + PCM 으로 올리면 한 곡이 수십 MB 짜리
//      비압축 데이터로 메모리에 상주한다. 모바일에서 그것만으로 죽는다.
//    Streaming            — 디스크에서 흘려 보낸다 (메모리 상주 없음)
//    Vorbis               — 압축한 채로 둔다
//    PreloadAudioData off — 스트리밍이라 미리 올릴 것이 없다
//    LoadInBackground on  — 첫 재생에서 메인 스레드를 막지 않는다
//    ForceToMono off      — 곡은 스테레오가 그대로 살아야 넓게 들린다
// ============================================================

public class AudioImportSetup : AssetPostprocessor
{
    const string TargetFolder = "Assets/_project/5.Audio/";
    const string BgmFolder    = "Assets/_project/5.Audio/BGM/";

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(TargetFolder)) return;

        var importer = (AudioImporter)assetImporter;
        bool isBgm   = assetPath.StartsWith(BgmFolder);

        importer.forceToMono      = !isBgm;
        importer.loadInBackground = isBgm;

        // ⚠ preloadAudioData 는 AudioImporter 에 없다
        //   Unity 2022.2 에서 AudioImporterSampleSettings 로 옮겨졌다.
        //   importer.preloadAudioData 로 쓰면 컴파일 에러가 난다.
        var s = importer.defaultSampleSettings;
        s.loadType          = isBgm ? AudioClipLoadType.Streaming
                                    : AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = isBgm ? AudioCompressionFormat.Vorbis
                                    : AudioCompressionFormat.PCM;
        s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        s.preloadAudioData  = !isBgm;
        importer.defaultSampleSettings = s;
    }

    [MenuItem(ProjectKMenu.Tool + "사운드 임포트 설정 다시 적용", priority = ProjectKMenu.ToolPrio + 20)]
    public static void Reapply()
    {
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_project/5.Audio" }))
        {
            AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)).SaveAndReimport();
            n++;
        }
        Debug.Log($"[AudioImportSetup] 클립 {n}개 재임포트 완료");
    }
}
