// ============================================================
//  IconGenerator.cs  [Editor Only]
//  아이콘 PNG 를 코드로 그려 저장하는 도구 (픽셀 페인터 P + 저장·임포트 설정).
//  그림 자체는 IconArt (글리프·배경·테두리) 와 SoulMercenariesAssetBuilder 가 정한다.
// ============================================================
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class IconGenerator
{
    public static Color32 Hex(string h)
    {
        h = h.TrimStart('#');
        byte r = Convert.ToByte(h.Substring(0, 2), 16);
        byte g = Convert.ToByte(h.Substring(2, 2), 16);
        byte b = Convert.ToByte(h.Substring(4, 2), 16);
        return new Color32(r, g, b, 255);
    }

    public static void Save(int w, int h, string assetPath, Action<P> draw)
    {
        var painter = new P(w, h);
        draw(painter);
        painter.Save(assetPath);
    }

    public static void EnsureDir(string assetPath)
    {
        string full = Path.Combine(Application.dataPath, "..", assetPath);
        Directory.CreateDirectory(full);
    }

    public static void ApplySpriteImportSettings(string folder, int size)
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png")) continue;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType          = TextureImporterType.Sprite;
            importer.spriteImportMode     = SpriteImportMode.Single;
            importer.spritePivot          = new Vector2(0.5f, 0.5f);
            importer.filterMode           = FilterMode.Bilinear;
            importer.textureCompression   = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize       = 128;
            importer.alphaIsTransparency  = true;
            importer.SaveAndReimport();
        }
    }

    // ═══════════════════════════════════════════════════════
    //  ■ Painter — 픽셀 그리기 헬퍼
    // ═══════════════════════════════════════════════════════
    public class P
    {
        public int W, H;
        readonly Color32[] px;

        public P(int w, int h)
        {
            W = w; H = h;
            px = new Color32[w * h];
        }

        int Idx(int x, int y) => (H - 1 - y) * W + x;

        public void BlendPixel(int x, int y, Color32 c)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return;
            int i = Idx(x, y);
            if (c.a == 255) { px[i] = c; return; }
            float a = c.a / 255f, ea = px[i].a / 255f;
            float oa = a + ea * (1 - a);
            if (oa < 0.001f) { px[i] = default; return; }
            px[i] = new Color32(
                (byte)Mathf.RoundToInt((c.r * a + px[i].r * ea * (1 - a)) / oa),
                (byte)Mathf.RoundToInt((c.g * a + px[i].g * ea * (1 - a)) / oa),
                (byte)Mathf.RoundToInt((c.b * a + px[i].b * ea * (1 - a)) / oa),
                (byte)Mathf.RoundToInt(oa * 255));
        }

        // ── 배경 ──────────────────────────────────────────
        public void BgGradient(Color32 dark, Color32 mid)
        {
            int cx = W / 2, cy = H / 2;
            float maxD = Mathf.Sqrt(cx * cx + cy * cy);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                // 둥근 모서리 마스크 (r=10)
                const int R = 10;
                int dx = 0, dy = 0;
                if (x < R && y < R)       { dx = R-x; dy = R-y; }
                else if (x>=W-R && y<R)   { dx = x-(W-R-1); dy = R-y; }
                else if (x<R && y>=H-R)   { dx = R-x; dy = y-(H-R-1); }
                else if (x>=W-R && y>=H-R){ dx = x-(W-R-1); dy = y-(H-R-1); }
                if (dx*dx + dy*dy > R*R && (dx>0||dy>0)) continue;

                float t = Mathf.Clamp01(Mathf.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy)) / maxD);
                BlendPixel(x, y, Color32.Lerp(mid, dark, t * t));
            }
        }

        // ── 테두리 ────────────────────────────────────────
        public void RoundedBorder(int r, int thick, Color32 col)
        {
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int dx = 0, dy = 0;
                bool corner = false;
                if (x < r && y < r)         { dx=r-x;   dy=r-y;   corner=true; }
                else if(x>=W-r && y<r)      { dx=x-(W-r-1); dy=r-y; corner=true; }
                else if(x<r && y>=H-r)      { dx=r-x;   dy=y-(H-r-1); corner=true; }
                else if(x>=W-r && y>=H-r)   { dx=x-(W-r-1); dy=y-(H-r-1); corner=true; }

                bool onEdge;
                if (corner)
                {
                    float d = Mathf.Sqrt(dx*dx + dy*dy);
                    onEdge = d >= r - thick && d <= r;
                }
                else
                {
                    onEdge = x < thick || x >= W - thick || y < thick || y >= H - thick;
                }
                if (onEdge) BlendPixel(x, y, col);
            }
        }

        // ── 도형 ──────────────────────────────────────────
        public void FillRect(int x, int y, int w, int h, Color32 c)
        {
            for (int py=y; py<y+h; py++) for (int px2=x; px2<x+w; px2++) BlendPixel(px2,py,c);
        }

        public void FillRRect(int x, int y, int w, int h, int rad, Color32 c)
        {
            for (int py=y; py<y+h; py++)
            for (int px2=x; px2<x+w; px2++)
            {
                int dx=0, dy=0;
                if (px2<x+rad && py<y+rad)     { dx=x+rad-px2; dy=y+rad-py; }
                else if(px2>=x+w-rad && py<y+rad){ dx=px2-(x+w-rad-1); dy=y+rad-py; }
                else if(px2<x+rad && py>=y+h-rad){ dx=x+rad-px2; dy=py-(y+h-rad-1); }
                else if(px2>=x+w-rad && py>=y+h-rad){ dx=px2-(x+w-rad-1); dy=py-(y+h-rad-1); }
                if (dx*dx+dy*dy <= rad*rad || (dx==0&&dy==0)) BlendPixel(px2,py,c);
            }
        }

        public void FillCircle(int cx, int cy, int r, Color32 c)
        {
            for (int y=cy-r; y<=cy+r; y++) for (int x=cx-r; x<=cx+r; x++)
                if ((x-cx)*(x-cx)+(y-cy)*(y-cy)<=r*r) BlendPixel(x,y,c);
        }

        public void FillCircleAlpha(int cx, int cy, int r, Color32 c)
            => FillCircle(cx, cy, r, c);

        public void DrawCircle(int cx, int cy, int r, int thick, Color32 c)
        {
            for (int y=cy-r-thick; y<=cy+r+thick; y++) for (int x=cx-r-thick; x<=cx+r+thick; x++)
            {
                int d2 = (x-cx)*(x-cx)+(y-cy)*(y-cy);
                if (d2>=(r-thick)*(r-thick) && d2<=(r+thick)*(r+thick)) BlendPixel(x,y,c);
            }
        }

        public void FillCircleGrad(int cx, int cy, int r, Color32 inner, Color32 mid, Color32 outer)
        {
            for (int y=cy-r; y<=cy+r; y++) for (int x=cx-r; x<=cx+r; x++)
            {
                int d2 = (x-cx)*(x-cx)+(y-cy)*(y-cy);
                if (d2 > r*r) continue;
                float t = Mathf.Sqrt(d2) / r;
                Color32 c = t < 0.5f ? Color32.Lerp(inner, mid, t*2) : Color32.Lerp(mid, outer, (t-0.5f)*2);
                BlendPixel(x, y, c);
            }
        }

        public void FillEllipse(int cx, int cy, int rx, int ry, Color32 c)
        {
            for (int y=cy-ry; y<=cy+ry; y++) for (int x=cx-rx; x<=cx+rx; x++)
            {
                float dx = (float)(x-cx)/rx, dy2 = (float)(y-cy)/ry;
                if (dx*dx+dy2*dy2 <= 1f) BlendPixel(x,y,c);
            }
        }

        public void DrawLine(int x1, int y1, int x2, int y2, Color32 c, int thick=1)
        {
            int dx = Mathf.Abs(x2-x1), dy = Mathf.Abs(y2-y1);
            int sx = x1<x2?1:-1, sy = y1<y2?1:-1;
            int err = dx-dy, x=x1, y=y1;
            int h2 = thick/2;
            while (true)
            {
                for (int py=y-h2; py<=y+h2; py++) for (int px2=x-h2; px2<=x+h2; px2++) BlendPixel(px2,py,c);
                if (x==x2&&y==y2) break;
                int e2=2*err;
                if (e2>-dy){err-=dy;x+=sx;}
                if (e2< dx){err+=dx;y+=sy;}
            }
        }

        public void FillTri(int x1,int y1, int x2,int y2, int x3,int y3, Color32 c)
        {
            int minX=Mathf.Min(x1,Mathf.Min(x2,x3)), maxX=Mathf.Max(x1,Mathf.Max(x2,x3));
            int minY=Mathf.Min(y1,Mathf.Min(y2,y3)), maxY=Mathf.Max(y1,Mathf.Max(y2,y3));
            for (int py=minY; py<=maxY; py++) for (int px2=minX; px2<=maxX; px2++)
            {
                float d1=Sign(px2,py,x1,y1,x2,y2), d2=Sign(px2,py,x2,y2,x3,y3), d3=Sign(px2,py,x3,y3,x1,y1);
                if (!((d1<0||d2<0||d3<0)&&(d1>0||d2>0||d3>0))) BlendPixel(px2,py,c);
            }
        }
        float Sign(int px,int py,int x1,int y1,int x2,int y2) => (px-x2)*(y1-y2)-(float)(x1-x2)*(py-y2);

        public void Save(string assetPath)
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(full, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
