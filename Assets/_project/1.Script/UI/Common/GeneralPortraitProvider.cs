using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.PixelFantasy.Common.Scripts.CollectionScripts;
using Assets.PixelFantasy.Common.Scripts.Utils;
using Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts;
using UnityEngine;

// ============================================================
//  GeneralPortraitProvider.cs
//  외형(UnitAppearanceData)으로 초상화를 요청받아 한 프레임에 몇 장씩 합성해 돌려주는 공급자.
//
//  ■ 왜 비동기인가
//    초상화는 PNG 가 아니라 런타임 합성물이다 (레이어 5장 병합).
//    합성 1회 = GC 12~14MB. 도감 격자가 한 줄 넘어갈 때마다 6장을 동시에
//    합성하면 스크롤 내내 GC 스파이크가 찍힌다.
//    → 요청은 큐에 쌓고, 프레임당 FrameBudgetMs 만큼만 합성에 쓴다.
//      개수 제한이 아니라 시간 제한이라 기기가 빠를수록 저절로 빨리 채워진다.
//
//  ■ 캐시는 작은 텍스처만 들고 있다
//    합성 결과는 576×928(2MB) 시트다. 그 시트를 그대로 캐시하면
//    400명 × 2MB = 800MB 가 된다.
//    Idle_0 프레임만 잘라 독립 텍스처로 굽고(≈64×64, 16KB) 시트는 재사용한다.
//    → 400명 전부 캐시해도 10MB 안쪽이다.
//
//  ⚠ 시트(_sheet)는 한 장을 계속 덮어쓴다
//    캐시에 넘기는 건 잘라 만든 새 텍스처지 시트가 아니다.
//    시트로 Sprite.Create 를 하면 다음 합성 때 그림이 통째로 바뀐다.
//
//  ⚠ 요청은 "아직 필요한가" 를 들고 온다
//    빠르게 스크롤하면 이미 화면 밖으로 나간 칸의 요청이 큐에 남는다.
//    stillWanted 가 false 면 합성하지 않고 버린다 — 이게 없으면
//    큐가 400장까지 밀려 스크롤을 멈춰도 한참 동안 GC 가 튄다.
// ============================================================

public class GeneralPortraitProvider : MonoBehaviour
{
    /// <summary>
    /// 한 프레임에 합성에 쓸 시간(ms). 이 시간을 넘기면 다음 프레임으로 넘긴다.
    ///
    /// ⚠ "몇 장" 이 아니라 "몇 ms" 인 이유
    ///   장당 비용은 기기마다 몇 배씩 차이 난다. 장수로 제한하면 빠른 기기에서는
    ///   쓸데없이 느리고 느린 기기에서는 프레임이 튄다.
    ///   시간으로 끊으면 어느 기기에서든 "프레임을 지키는 한도 안에서 최대 속도" 가 된다.
    ///   16.6ms(60fps) 중 8ms 면 나머지 렌더·UI 가 돌 여유가 남는다.
    /// </summary>
    public static float FrameBudgetMs = 8f;

    /// <summary>캐시 상한. 넘으면 오래된 것부터 버린다.</summary>
    public static int CacheLimit = 512;

    // ⚠ 이름을 Request 로 두지 말 것 — 아래 Request() 메서드와 충돌한다 (CS0102).
    //   중첩 타입과 멤버는 같은 이름을 쓸 수 없다.
    struct PortraitJob
    {
        public string         Name;
        public Func<bool>     StillWanted;
        public Action<Sprite> OnReady;
        public UnitAppearanceData Look;
        public bool           Trim;       // false: Idle 프레임 그대로 (여백 유지)
        public string         Clip;       // 있으면 초상화 대신 그 동작의 프레임들 (Slash_0, Slash_1 …)
        public int            FrameCount;
        public Action<Sprite[]> OnFrames;
    }

    static GeneralPortraitProvider _inst;

    static GeneralPortraitProvider Inst
    {
        get
        {
            if (_inst != null) return _inst;

            var go = new GameObject("[GeneralPortraitProvider]");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<GeneralPortraitProvider>();
            return _inst;
        }
    }

    readonly Dictionary<string, Sprite> _cache = new();
    readonly Dictionary<string, Sprite[]> _frames = new();   // 동작 프레임 (훈련 중 공격 모션 등)
    readonly LinkedList<string>         _order = new();   // 오래된 것이 앞
    readonly Queue<PortraitJob>         _queue = new();

    CharacterBuilder _builder;
    Texture2D        _sheet;
    bool             _running;

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>이미 만들어 둔 초상화. 없으면 null (합성하지 않는다).</summary>
    public static Sprite GetCached(string unitName)
        => !string.IsNullOrEmpty(unitName) && Inst._cache.TryGetValue(unitName, out var s) ? s : null;

    /// <summary>
    /// 초상화를 요청한다. 캐시에 있으면 그 자리에서 콜백이 온다.
    /// 없으면 큐에 쌓였다가 몇 프레임 뒤에 온다.
    /// </summary>
    /// <param name="stillWanted">합성 직전에 물어본다. false 면 조용히 버린다.</param>
    /// <summary>
    /// 외형을 직접 주는 초상화 (영혼 용병단 용병처럼 이름으로 굴리지 않는 경우).
    /// key 는 캐시 키다 — 외형이 바뀌면 다른 키를 줘야 새로 합성된다.
    /// trim=false 면 Idle 프레임을 여백째 돌려준다 (프레임 기준으로 확대·정렬하는 UI 용).
    /// </summary>
    public static void Request(string key, UnitAppearanceData look, bool trim, Func<bool> stillWanted, Action<Sprite> onReady)
        => Enqueue(key, look, trim, stillWanted, onReady);

    /// <summary>이미 만들어 둔 동작 프레임. 없으면 null.</summary>
    public static Sprite[] GetCachedFrames(string key)
        => !string.IsNullOrEmpty(key) && Inst._frames.TryGetValue(key, out var f) ? f : null;

    /// <summary>
    /// 한 동작(clip = "Slash", "Jab", "Shot" …)의 프레임을 여백째 잘라 돌려준다 (피벗 = 발밑).
    /// 초상화와 같은 합성 큐를 탄다.
    /// </summary>
    public static void RequestFrames(string key, UnitAppearanceData look, string clip, int count, Action<Sprite[]> onReady)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(clip) || onReady == null) return;
        var inst = Inst;
        if (inst._frames.TryGetValue(key, out var cached)) { onReady(cached); return; }
        inst._queue.Enqueue(new PortraitJob { Name = key, Look = look, Clip = clip, FrameCount = count, OnFrames = onReady });
        if (!inst._running) inst.StartCoroutine(inst.Pump());
    }

    static void Enqueue(string unitName, UnitAppearanceData look, bool trim, Func<bool> stillWanted, Action<Sprite> onReady)
    {
        if (string.IsNullOrEmpty(unitName) || onReady == null) return;

        var inst = Inst;

        if (inst._cache.TryGetValue(unitName, out var cached))
        {
            inst.Touch(unitName);
            onReady(cached);
            return;
        }

        // 같은 이름이 큐에 두 번 들어가도 괜찮다 —
        // 먼저 처리된 쪽이 캐시에 넣으므로 뒤엣것은 꺼낼 때 캐시에서 바로 받는다.
        inst._queue.Enqueue(new PortraitJob
        {
            Name        = unitName,
            StillWanted = stillWanted,
            OnReady     = onReady,
            Look        = look,
            Trim        = trim,
        });

        if (!inst._running) inst.StartCoroutine(inst.Pump());
    }

    /// <summary>캐시를 비운다. 씬을 넘나들며 메모리를 되찾을 때.</summary>
    public static void ClearCache()
    {
        if (_inst == null) return;

        foreach (var sp in _inst._cache.Values)
            if (sp != null) Destroy(sp.texture);
        foreach (var frames in _inst._frames.Values)
            foreach (var sp in frames)
                if (sp != null) Destroy(sp.texture);

        _inst._cache.Clear();
        _inst._frames.Clear();
        _inst._order.Clear();
    }

    // ── 내부 ─────────────────────────────────────────────────

    IEnumerator Pump()
    {
        _running = true;

        var watch = new System.Diagnostics.Stopwatch();

        while (_queue.Count > 0)
        {
            watch.Restart();

            // 예산이 남는 동안 계속 뽑는다 — 개수 제한 없음.
            // 캐시 히트나 버려지는 요청은 거의 공짜라 이 루프에서 순식간에 빠진다.
            do
            {
                var req = _queue.Dequeue();

                if (req.Clip != null)
                {
                    if (!_frames.TryGetValue(req.Name, out var frames))
                    {
                        frames = BuildFrames(req);
                        if (frames == null) continue;
                        _frames[req.Name] = frames;
                    }
                    req.OnFrames?.Invoke(frames);
                    continue;
                }

                // 그 사이 다른 요청이 만들어 놨을 수 있다
                if (_cache.TryGetValue(req.Name, out var done))
                {
                    Touch(req.Name);
                    req.OnReady?.Invoke(done);
                    continue;
                }

                // 화면 밖으로 나간 칸의 요청은 버린다
                if (req.StillWanted != null && !req.StillWanted()) continue;

                var sprite = Build(req);
                if (sprite == null) continue;

                Store(req.Name, sprite);
                req.OnReady?.Invoke(sprite);
            }
            while (_queue.Count > 0 && watch.Elapsed.TotalMilliseconds < FrameBudgetMs);

            // ⚠ do-while 이라 프레임당 최소 1장은 반드시 처리된다
            //   while 로 두면 예산이 0 일 때 큐가 영원히 안 줄어든다.
            yield return null;
        }

        _running = false;
    }

    Sprite Build(PortraitJob req)
        => MergeSheet(req.Look) ? CropIdleFrame(_sheet, req.Trim) : null;

    Sprite[] BuildFrames(PortraitJob req)
    {
        if (!MergeSheet(req.Look)) return null;
        var list = new List<Sprite>();
        for (int i = 0; i < req.FrameCount; i++)
        {
            if (!CharacterBuilder.Layout.TryGetValue(req.Clip + "_" + i, out var l)) break;
            var px = _sheet.GetPixels(l[0], l[1], l[2], l[3]);
            if (!Array.Exists(px, c => c.a > 0.01f)) continue;   // 빈 칸 (그 동작에 없는 프레임)
            var tex = new Texture2D(l[2], l[3], TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixels(px);
            tex.Apply(false, false);
            list.Add(Sprite.Create(tex, new Rect(0, 0, l[2], l[3]), new Vector2(0.5f, 0f), 16, 0, SpriteMeshType.FullRect));
        }
        return list.Count > 0 ? list.ToArray() : null;
    }

    // 외형을 합성해 _sheet 에 그린다.
    bool MergeSheet(UnitAppearanceData data)
    {
        if (!EnsureBuilder()) return false;
        if (data == null) return false;

        _builder.Body    = data.Body;
        _builder.Head    = data.Head;
        _builder.Ears    = data.Ears;
        _builder.Eyes    = data.Eyes;
        _builder.Hair    = data.Hair;
        _builder.Armor   = data.Armor;
        _builder.Helmet  = data.Helmet;
        _builder.Mask    = data.Mask;
        _builder.Horns   = data.Horns;
        _builder.Cape    = data.Cape;
        _builder.Weapon  = data.Weapon;
        _builder.Shield  = data.Shield;
        _builder.Back    = data.Back;
        _builder.Firearm = data.Firearm ?? "";

        var layers = _builder.BuildLayers();
        if (layers.Count == 0) return false;

        if (_sheet == null)
            _sheet = new Texture2D(576, 928, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };

        TextureHelper.MergeLayers(_sheet, layers.Values.ToArray());
        return true;
    }

    bool EnsureBuilder()
    {
        if (_builder == null)
        {
            var go = new GameObject("Builder");
            go.transform.SetParent(transform, false);
            go.SetActive(false);              // 화면에 그릴 일이 없다 — 합성기만 쓴다
            _builder = go.AddComponent<CharacterBuilder>();
        }

        if (_builder.SpriteCollection == null)
            _builder.SpriteCollection = Resources.Load<SpriteCollection>("SpriteCollection");

        return _builder.SpriteCollection != null;
    }

    // Idle_0 프레임만 잘라 **독립 텍스처**로 굽는다.
    // 시트를 그대로 참조하면 다음 합성 때 이미 뿌려 둔 초상화가 전부 바뀐다.
    static Sprite CropIdleFrame(Texture2D sheet, bool trim = true)
    {
        var l = CharacterBuilder.Layout["Idle_0"];
        int fx = l[0], fy = l[1], fw = l[2], fh = l[3];

        var px = sheet.GetPixels(fx, fy, fw, fh);

        if (!trim)
        {
            var whole = new Texture2D(fw, fh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            whole.SetPixels(px);
            whole.Apply(false, false);
            return Sprite.Create(whole, new Rect(0, 0, fw, fh), new Vector2(0.5f, 0f), 16, 0, SpriteMeshType.FullRect);
        }

        // 투명 여백을 잘라낸다 — 칸 안에서 캐릭터가 최대한 크게 보이도록
        int minX = fw, maxX = -1, minY = fh, maxY = -1;
        for (int y = 0; y < fh; y++)
        for (int x = 0; x < fw; x++)
            if (px[y * fw + x].a > 0.01f)
            {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

        if (maxX < minX || maxY < minY) return null;   // 전부 투명 = 합성 실패

        const int Pad = 1;
        minX = Mathf.Max(0,      minX - Pad);
        minY = Mathf.Max(0,      minY - Pad);
        maxX = Mathf.Min(fw - 1, maxX + Pad);
        maxY = Mathf.Min(fh - 1, maxY + Pad);

        int w = maxX - minX + 1, h = maxY - minY + 1;

        var sub = new Color[w * h];
        for (int y = 0; y < h; y++)
            Array.Copy(px, (minY + y) * fw + minX, sub, y * w, w);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        tex.SetPixels(sub);
        tex.Apply(false, false);

        return Sprite.Create(tex, new Rect(0, 0, w, h),
                             new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
    }

    void Store(string name, Sprite sprite)
    {
        _cache[name] = sprite;
        Touch(name);

        while (_order.Count > CacheLimit)
        {
            string oldest = _order.First.Value;
            _order.RemoveFirst();

            if (_cache.TryGetValue(oldest, out var sp) && sp != null)
            {
                Destroy(sp.texture);
                Destroy(sp);
            }
            _cache.Remove(oldest);
        }
    }

    void Touch(string name)
    {
        _order.Remove(name);
        _order.AddLast(name);
    }
}
