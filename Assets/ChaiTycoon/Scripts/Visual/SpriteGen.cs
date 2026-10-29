using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Generates every UI sprite and texture at runtime (rounded panels, rings, icons) with a tiny
    /// signed-distance painter, so the build ships no image files.
    /// </summary>
    public static class SpriteGen
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();

        // ---------- SDF primitives (pixel space, negative inside) ----------
        public delegate float Sdf(Vector2 p);

        public static Sdf Circle(Vector2 c, float r) => p => (p - c).magnitude - r;
        public static Sdf Ellipse(Vector2 c, Vector2 r) => p =>
        {
            var q = new Vector2((p.x - c.x) / r.x, (p.y - c.y) / r.y);
            float k = q.magnitude;
            return (k - 1f) * Mathf.Min(r.x, r.y);
        };
        public static Sdf Box(Vector2 c, Vector2 half, float round = 0, float rotDeg = 0) => p =>
        {
            var d = p - c;
            if (rotDeg != 0)
            {
                float a = -rotDeg * Mathf.Deg2Rad;
                d = new Vector2(d.x * Mathf.Cos(a) - d.y * Mathf.Sin(a), d.x * Mathf.Sin(a) + d.y * Mathf.Cos(a));
            }
            var q = new Vector2(Mathf.Abs(d.x), Mathf.Abs(d.y)) - half + Vector2.one * round;
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - round;
        };
        public static Sdf Seg(Vector2 a, Vector2 b, float r) => p =>
        {
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        };
        public static Sdf Poly(params Vector2[] v) => p =>
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s *= -1;
            }
            return s * Mathf.Sqrt(d);
        };
        public static Sdf Union(params Sdf[] s) => p => { float d = float.MaxValue; foreach (var f in s) d = Mathf.Min(d, f(p)); return d; };
        public static Sdf Sub(Sdf a, Sdf b) => p => Mathf.Max(a(p), -b(p));
        public static Sdf Inter(Sdf a, Sdf b) => p => Mathf.Max(a(p), b(p));
        public static Sdf Ring(Vector2 c, float r, float w) => p => Mathf.Abs((p - c).magnitude - r) - w;

        public static Vector2[] StarPts(Vector2 c, int n, float ro, float ri, float rot = 90)
        {
            var pts = new Vector2[n * 2];
            for (int i = 0; i < n * 2; i++)
            {
                float a = (rot + i * 180f / n) * Mathf.Deg2Rad;
                float r = i % 2 == 0 ? ro : ri;
                pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return pts;
        }

        public struct Layer
        {
            public Sdf sdf; public Color color;
            public Layer(Sdf s, Color c) { sdf = s; color = c; }
        }

        /// <summary>Paints layers back-to-front with an ink outline around their union.</summary>
        public static Texture2D Paint(int w, int h, IList<Layer> layers, float outline, Color? outlineColor = null, bool mip = false)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mip) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            var ink = outlineColor ?? Pal.Ink;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float du = float.MaxValue;
                for (int i = 0; i < layers.Count; i++) du = Mathf.Min(du, layers[i].sdf(p));
                Color c = new Color(ink.r, ink.g, ink.b, 0);
                if (outline > 0)
                {
                    float a = Mathf.Clamp01(0.5f - (du - outline));
                    c = new Color(ink.r, ink.g, ink.b, a * ink.a);
                }
                for (int i = 0; i < layers.Count; i++)
                {
                    float d = layers[i].sdf(p);
                    float a = Mathf.Clamp01(0.5f - d) * layers[i].color.a;
                    if (a <= 0) continue;
                    var lc = layers[i].color;
                    // "over" compositing
                    float outA = a + c.a * (1 - a);
                    if (outA > 0)
                    {
                        c.r = (lc.r * a + c.r * c.a * (1 - a)) / outA;
                        c.g = (lc.g * a + c.g * c.a * (1 - a)) / outA;
                        c.b = (lc.b * a + c.b * c.a * (1 - a)) / outA;
                    }
                    c.a = outA;
                }
                px[y * w + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(mip, false);
            return tex;
        }

        public static Sprite ToSprite(Texture2D tex, Vector4 border = default, float ppu = 100)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border);
        }

        static Sprite Cached(string key, Func<Sprite> make)
        {
            if (cache.TryGetValue(key, out var s) && s) return s;
            s = make();
            s.name = key;
            cache[key] = s;
            return s;
        }

        // ---------- UI building blocks ----------

        /// <summary>White rounded rect with an ink outline; tint via Image.color. 9-sliced.</summary>
        public static Sprite Panel(int radius = 16, int outline = 3) => Cached($"panel{radius}_{outline}", () =>
        {
            int s = radius * 2 + 8;
            var tex = Paint(s, s, new[] { new Layer(Box(new Vector2(s / 2f, s / 2f), new Vector2(s / 2f - outline - 0.5f, s / 2f - outline - 0.5f), radius - outline), Color.white) }, outline);
            float b = radius + 2;
            return ToSprite(tex, new Vector4(b, b, b, b));
        });

        /// <summary>Rounded rect without outline (fills, bars, shadows).</summary>
        public static Sprite Rounded(int radius = 16) => Cached($"round{radius}", () =>
        {
            int s = radius * 2 + 4;
            var tex = Paint(s, s, new[] { new Layer(Box(new Vector2(s / 2f, s / 2f), new Vector2(s / 2f - 0.5f, s / 2f - 0.5f), radius), Color.white) }, 0);
            float b = radius + 1;
            return ToSprite(tex, new Vector4(b, b, b, b));
        });

        /// <summary>Button: white fill, darker bottom lip, ink outline. Tint via Image.color.</summary>
        public static Sprite Button(int radius = 18) => Cached($"btn{radius}", () =>
        {
            int s = radius * 2 + 16;
            float o = 3;
            var c = new Vector2(s / 2f, s / 2f);
            var half = new Vector2(s / 2f - o - 0.5f, s / 2f - o - 0.5f);
            var outer = Box(c, half, radius - o);
            var lip = Inter(outer, Box(new Vector2(s / 2f, o + 3), new Vector2(s, 6), 0));
            var tex = Paint(s, s, new[] { new Layer(outer, Color.white), new Layer(lip, new Color(0.72f, 0.72f, 0.72f, 1)) }, o);
            float b = radius + 4;
            return ToSprite(tex, new Vector4(b, b + 4, b, b));
        });

        public static Sprite SoftShadow() => Cached("softshadow", () =>
        {
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            var box = Box(new Vector2(s / 2f, s / 2f), new Vector2(14, 14), 10);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = box(new Vector2(x + 0.5f, y + 0.5f));
                float a = Mathf.Clamp01(1f - (d + 4) / 16f);
                a = a * a * 0.45f;
                px[y * s + x] = new Color(0.17f, 0.12f, 0.18f, a);
            }
            tex.SetPixels32(px); tex.Apply();
            return ToSprite(tex, new Vector4(28, 28, 28, 28));
        });

        public static Sprite Circle(int size = 64, int outline = 0) => Cached($"circle{size}_{outline}", () =>
            ToSprite(Paint(size, size, new[] { new Layer(Circle(new Vector2(size / 2f, size / 2f), size / 2f - outline - 1), Color.white) }, outline)));

        /// <summary>Thick white ring for radial fill (patience, tile progress).</summary>
        public static Sprite RingSprite(int size = 128, float thickness = 12) => Cached($"ring{size}_{thickness}", () =>
            ToSprite(Paint(size, size, new[] { new Layer(Ring(new Vector2(size / 2f, size / 2f), size / 2f - thickness / 2f - 1, thickness / 2f), Color.white) }, 0)));

        public static Texture2D BlobTexture() => TexCached("blob", () =>
        {
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = new Vector2(x + 0.5f - s / 2f, y + 0.5f - s / 2f).magnitude / (s / 2f);
                float a = Mathf.Clamp01(1f - d);
                a = Mathf.SmoothStep(0, 1, a * 1.6f);
                px[y * s + x] = new Color(1, 1, 1, a);
            }
            tex.SetPixels32(px); tex.Apply();
            return tex;
        });

        /// <summary>Square floor tile with rounded corners and a thick border (unlock pads).</summary>
        public static Texture2D TileTexture() => TexCached("tile", () =>
        {
            int s = 128;
            var c = new Vector2(s / 2f, s / 2f);
            var outer = Box(c, new Vector2(60, 60), 18);
            var inner = Box(c, new Vector2(50, 50), 12);
            return Paint(s, s, new[] { new Layer(outer, Color.white), new Layer(inner, new Color(1, 1, 1, 0.35f)) }, 0);
        });

        static Texture2D TexCached(string key, Func<Texture2D> make)
        {
            if (texCache.TryGetValue(key, out var t) && t) return t;
            t = make();
            t.name = key;
            texCache[key] = t;
            return t;
        }

        // ---------- Icons (128 px, ink outline) ----------

        const int I = 128;
        static Vector2 V(float x, float y) => new Vector2(x, y);

        public static Sprite Icon(string name)
        {
            return Cached("icon_" + name, () => ToSprite(DrawIcon(name)));
        }

        static Texture2D DrawIcon(string name)
        {
            var L = new List<Layer>();
            var c = V(64, 64);
            switch (name)
            {
                case "coin":
                    L.Add(new Layer(Circle(c, 50), Pal.CashGold));
                    L.Add(new Layer(Ring(c, 36, 4), Pal.Mul(Pal.Marigold, 0.85f)));
                    L.Add(new Layer(Box(V(64, 64), V(7, 20), 3), Pal.Mul(Pal.Marigold, 0.85f)));
                    L.Add(new Layer(Poly(StarPts(V(88, 92), 4, 13, 3, 90)), Color.white));
                    break;
                case "leaf":
                {
                    var leaf = Inter(Circle(V(40, 40), 70), Circle(V(88, 88), 70));
                    L.Add(new Layer(leaf, Pal.Parrot));
                    L.Add(new Layer(Seg(V(22, 22), V(100, 100), 3.5f), Pal.Mul(Pal.Parrot, 0.7f)));
                    L.Add(new Layer(Seg(V(60, 60), V(44, 82), 2.5f), Pal.Mul(Pal.Parrot, 0.7f)));
                    L.Add(new Layer(Seg(V(76, 76), V(92, 58), 2.5f), Pal.Mul(Pal.Parrot, 0.7f)));
                    break;
                }
                case "star":
                    L.Add(new Layer(Poly(StarPts(c, 5, 54, 24)), Pal.Marigold));
                    L.Add(new Layer(Poly(StarPts(V(58, 72), 5, 14, 7)), new Color(1, 1, 1, 0.55f)));
                    break;
                case "star_empty":
                    L.Add(new Layer(Poly(StarPts(c, 5, 54, 24)), new Color(0.85f, 0.8f, 0.72f, 1)));
                    break;
                case "gear":
                {
                    var teeth = new List<Sdf> { Circle(c, 38) };
                    for (int i = 0; i < 8; i++) teeth.Add(Box(c + new Vector2(Mathf.Cos(i * Mathf.PI / 4), Mathf.Sin(i * Mathf.PI / 4)) * 42, V(9, 11), 3, i * 45 + 90));
                    L.Add(new Layer(Sub(Union(teeth.ToArray()), Circle(c, 16)), Pal.Cream));
                    break;
                }
                case "map":
                    L.Add(new Layer(Poly(V(14, 30), V(44, 20), V(84, 34), V(114, 22), V(114, 98), V(84, 110), V(44, 96), V(14, 108)), Pal.Cream));
                    L.Add(new Layer(Seg(V(28, 46), V(56, 80), 4), Pal.Chili));
                    L.Add(new Layer(Seg(V(56, 80), V(96, 56), 4), Pal.Chili));
                    L.Add(new Layer(Circle(V(96, 56), 9), Pal.TruckPink));
                    break;
                case "up":
                    L.Add(new Layer(Poly(V(64, 112), V(110, 62), V(82, 62), V(82, 16), V(46, 16), V(46, 62), V(18, 62)), Pal.Parrot));
                    break;
                case "staff":
                    L.Add(new Layer(Circle(V(64, 82), 22), Pal.Skins[1]));
                    L.Add(new Layer(Box(V(64, 30), V(36, 22), 18), Pal.Chili));
                    L.Add(new Layer(Box(V(64, 100), V(24, 8), 6), Pal.Parrot));
                    break;
                case "tv":
                    L.Add(new Layer(Box(V(64, 58), V(50, 36), 12), Pal.Ink));
                    L.Add(new Layer(Box(V(64, 58), V(40, 27), 6), Pal.Teal));
                    L.Add(new Layer(Poly(V(54, 72), V(54, 44), V(78, 58)), Color.white));
                    L.Add(new Layer(Seg(V(48, 104), V(60, 94), 4), Pal.Ink));
                    L.Add(new Layer(Seg(V(80, 104), V(68, 94), 4), Pal.Ink));
                    break;
                case "lock":
                    L.Add(new Layer(Ring(V(64, 76), 22, 7), Pal.DarkSteel));
                    L.Add(new Layer(Box(V(64, 46), V(34, 28), 8), Pal.Steel));
                    L.Add(new Layer(Circle(V(64, 50), 7), Pal.Ink));
                    break;
                case "check":
                    L.Add(new Layer(Union(Seg(V(28, 64), V(52, 38), 10), Seg(V(52, 38), V(100, 92), 10)), Pal.Parrot));
                    break;
                case "close":
                    L.Add(new Layer(Union(Seg(V(34, 34), V(94, 94), 10), Seg(V(34, 94), V(94, 34), 10)), Pal.Cream));
                    break;
                case "note":
                    L.Add(new Layer(Union(Ellipse(V(44, 34), V(18, 13)), Ellipse(V(92, 44), V(18, 13)), Box(V(60, 74), V(4, 40), 0), Box(V(108, 84), V(4, 40), 0),
                        Poly(V(56, 112), V(112, 124), V(112, 104), V(56, 92))), Pal.Cream));
                    break;
                case "speaker":
                    L.Add(new Layer(Union(Box(V(34, 64), V(14, 18), 3), Poly(V(40, 64), V(76, 104), V(76, 24))), Pal.Cream));
                    L.Add(new Layer(Sub(Ring(V(74, 64), 24, 5), Box(V(54, 64), V(22, 60), 0)), Pal.Cream));
                    L.Add(new Layer(Sub(Ring(V(74, 64), 40, 5), Box(V(54, 64), V(22, 60), 0)), Pal.Cream));
                    break;
                case "heart":
                    L.Add(new Layer(Union(Circle(V(44, 78), 26), Circle(V(84, 78), 26), Poly(V(20, 70), V(108, 70), V(64, 18))), Pal.TruckPink));
                    L.Add(new Layer(Circle(V(40, 84), 7), new Color(1, 1, 1, 0.6f)));
                    break;
                case "cloud":
                    L.Add(new Layer(Union(Circle(V(40, 56), 22), Circle(V(66, 70), 28), Circle(V(92, 56), 22), Box(V(66, 46), V(40, 12), 10)), new Color(0.62f, 0.62f, 0.66f, 1)));
                    L.Add(new Layer(Seg(V(46, 24), V(42, 12), 3), Pal.Teal));
                    L.Add(new Layer(Seg(V(70, 26), V(66, 10), 3), Pal.Teal));
                    break;
                case "hand":
                    L.Add(new Layer(Union(Box(V(64, 46), V(30, 30), 14), Box(V(46, 92), V(9, 30), 9), Box(V(90, 60), V(8, 22), 8, -30)), Color.white));
                    break;
                case "arrow_down":
                    L.Add(new Layer(Poly(V(64, 10), V(118, 66), V(84, 66), V(84, 118), V(44, 118), V(44, 66), V(10, 66)), Pal.Marigold));
                    break;
                case "plus":
                    L.Add(new Layer(Union(Box(c, V(10, 42), 4), Box(c, V(42, 10), 4)), Pal.Parrot));
                    break;
                case "clock":
                    L.Add(new Layer(Circle(c, 48), Pal.Cream));
                    L.Add(new Layer(Seg(c, V(64, 96), 5), Pal.Ink));
                    L.Add(new Layer(Seg(c, V(88, 64), 5), Pal.Ink));
                    break;
                case "bolt":
                    L.Add(new Layer(Poly(V(74, 120), V(28, 58), V(58, 58), V(46, 8), V(100, 74), V(68, 74)), Pal.Marigold));
                    break;
                case "boot":
                    L.Add(new Layer(Union(Box(V(54, 74), V(18, 38), 8), Box(V(70, 26), V(36, 14), 10)), Pal.Chili));
                    L.Add(new Layer(Box(V(54, 104), V(20, 6), 2), Pal.Marigold));
                    L.Add(new Layer(Seg(V(14, 70), V(30, 70), 4), Pal.Ink));
                    L.Add(new Layer(Seg(V(10, 50), V(30, 50), 4), Pal.Ink));
                    break;
                case "stack":
                    for (int i = 0; i < 4; i++)
                    {
                        L.Add(new Layer(Box(V(64, 22 + i * 26), V(30, 11), 5), Pal.Cream));
                        L.Add(new Layer(Box(V(64, 28 + i * 26), V(24, 4), 2), Pal.ChaiTea));
                    }
                    break;
                case "magnet":
                    L.Add(new Layer(Sub(Sub(Circle(V(64, 52), 42), Circle(V(64, 52), 18)), Box(V(64, 100), V(60, 48), 0)), Pal.Chili));
                    L.Add(new Layer(Box(V(34, 76), V(12, 24), 0), Pal.Chili));
                    L.Add(new Layer(Box(V(94, 76), V(12, 24), 0), Pal.Chili));
                    L.Add(new Layer(Box(V(34, 104), V(12, 8), 0), Pal.Steel));
                    L.Add(new Layer(Box(V(94, 104), V(12, 8), 0), Pal.Steel));
                    break;
                case "hand_coin":
                    L.Add(new Layer(Box(V(56, 40), V(40, 22), 14), Pal.Skins[1]));
                    L.Add(new Layer(Circle(V(82, 84), 26), Pal.CashGold));
                    L.Add(new Layer(Poly(StarPts(V(90, 92), 4, 8, 2)), Color.white));
                    break;
                case "jar":
                    L.Add(new Layer(Box(V(64, 54), V(36, 44), 14), new Color(0.75f, 0.9f, 0.95f, 1)));
                    L.Add(new Layer(Box(V(64, 104), V(28, 8), 3), Pal.DarkWood));
                    L.Add(new Layer(Circle(V(52, 40), 13), Pal.CashGold));
                    L.Add(new Layer(Circle(V(76, 46), 13), Pal.CashGold));
                    L.Add(new Layer(Circle(V(62, 64), 13), Pal.CashGold));
                    break;
                case "queue":
                    for (int i = 0; i < 3; i++)
                    {
                        L.Add(new Layer(Circle(V(28 + i * 36, 84), 13), Pal.Skins[i]));
                        L.Add(new Layer(Box(V(28 + i * 36, 44), V(15, 24), 10), Pal.Clothes[i * 2]));
                    }
                    break;
                case "smile":
                    L.Add(new Layer(Circle(c, 50), Pal.Marigold));
                    L.Add(new Layer(Circle(V(46, 76), 7), Pal.Ink));
                    L.Add(new Layer(Circle(V(82, 76), 7), Pal.Ink));
                    L.Add(new Layer(Sub(Ring(V(64, 62), 26, 5), Box(V(64, 84), V(40, 24), 0)), Pal.Ink));
                    break;
                case "tray":
                    L.Add(new Layer(Box(V(64, 34), V(52, 10), 6), Pal.Steel));
                    L.Add(new Layer(Box(V(40, 64), V(16, 20), 4), Pal.Cream));
                    L.Add(new Layer(Box(V(84, 64), V(16, 20), 4), Pal.Cream));
                    L.Add(new Layer(Box(V(64, 104), V(9, 9), 2), Pal.Parrot));
                    L.Add(new Layer(Box(V(64, 104), V(3, 15), 1), Pal.Parrot));
                    L.Add(new Layer(Box(V(64, 104), V(15, 3), 1), Pal.Parrot));
                    break;
                case "price":
                    L.Add(new Layer(Circle(V(56, 56), 40), Pal.CashGold));
                    L.Add(new Layer(Box(V(56, 56), V(6, 18), 2), Pal.Mul(Pal.Marigold, 0.8f)));
                    L.Add(new Layer(Poly(V(98, 124), V(124, 94), V(108, 94), V(108, 66), V(88, 66), V(88, 94), V(72, 94)), Pal.Parrot));
                    break;
                case "batch":
                    for (int i = 0; i < 4; i++) L.Add(new Layer(Poly(V(30 + (i % 2) * 40, 18 + (i / 2) * 50), V(58 + (i % 2) * 40, 18 + (i / 2) * 50), V(44 + (i % 2) * 40, 56 + (i / 2) * 50)), Pal.Samosa));
                    break;
                case "chair":
                    L.Add(new Layer(Box(V(64, 54), V(44, 10), 4), Pal.DarkWood));
                    L.Add(new Layer(Box(V(28, 30), V(6, 20), 2), Pal.DarkWood));
                    L.Add(new Layer(Box(V(100, 30), V(6, 20), 2), Pal.DarkWood));
                    L.Add(new Layer(Box(V(64, 70), V(40, 8), 3), Pal.TruckPink));
                    break;
                case "megaphone":
                    L.Add(new Layer(Poly(V(24, 52), V(24, 76), V(96, 108), V(96, 20)), Pal.Chili));
                    L.Add(new Layer(Box(V(30, 46), V(6, 16), 3), Pal.DarkWood));
                    break;
                case "truck":
                    L.Add(new Layer(Box(V(56, 64), V(44, 30), 8), Pal.TruckPink));
                    L.Add(new Layer(Box(V(104, 54), V(16, 20), 6), Pal.Teal));
                    L.Add(new Layer(Circle(V(34, 30), 13), Pal.Ink));
                    L.Add(new Layer(Circle(V(96, 30), 13), Pal.Ink));
                    L.Add(new Layer(Poly(StarPts(V(56, 68), 5, 16, 7)), Pal.Marigold));
                    break;
                case "kettle":
                    L.Add(new Layer(Box(V(64, 54), V(36, 32), 16), Pal.Steel));
                    L.Add(new Layer(Box(V(64, 92), V(14, 6), 3), Pal.DarkSteel));
                    L.Add(new Layer(Seg(V(96, 60), V(118, 84), 6), Pal.Steel));
                    L.Add(new Layer(Sub(Ring(V(64, 92), 26, 5), Box(V(64, 70), V(40, 22), 0)), Pal.Ink));
                    break;
                case "bag":
                    L.Add(new Layer(Box(V(64, 52), V(42, 38), 18), Pal.DarkWood));
                    L.Add(new Layer(Box(V(64, 98), V(18, 12), 6), Pal.DarkWood));
                    L.Add(new Layer(Circle(V(64, 50), 20), Pal.CashGold));
                    L.Add(new Layer(Box(V(64, 50), V(4, 12), 1), Pal.Mul(Pal.Marigold, 0.8f)));
                    break;
                case "x2":
                {
                    // "2x": a hooked 2 (arc + diagonal + base) and a small x
                    L.Add(new Layer(Circle(c, 52), Pal.Parrot));
                    var arc = Inter(Ring(V(50, 78), 15, 6), Poly(V(28, 77), V(28, 104), V(74, 104), V(74, 62), V(56, 62), V(50, 77)));
                    var two = Union(arc, Seg(V(63, 72), V(35, 42), 6.5f), Seg(V(35, 42), V(66, 42), 6.5f));
                    L.Add(new Layer(two, Color.white));
                    L.Add(new Layer(Union(Seg(V(76, 64), V(96, 42), 5.5f), Seg(V(76, 42), V(96, 64), 5.5f)), Color.white));
                    break;
                }
                case "cap":
                    L.Add(new Layer(Union(Box(V(64, 60), V(40, 26), 20), Box(V(88, 40), V(34, 8), 6)), Pal.Marigold));
                    break;
                case "shirt":
                    L.Add(new Layer(Poly(V(40, 112), V(88, 112), V(118, 86), V(102, 70), V(90, 80), V(90, 14), V(38, 14), V(38, 80), V(26, 70), V(10, 86)), Pal.Marigold));
                    break;
                case "apron":
                    L.Add(new Layer(Poly(V(40, 100), V(88, 100), V(88, 76), V(104, 60), V(100, 14), V(28, 14), V(24, 60), V(40, 76)), Pal.Teal));
                    L.Add(new Layer(Seg(V(44, 100), V(56, 120), 4), Pal.Ink));
                    L.Add(new Layer(Seg(V(84, 100), V(72, 120), 4), Pal.Ink));
                    break;
                case "flower":
                    for (int i = 0; i < 6; i++) L.Add(new Layer(Circle(c + new Vector2(Mathf.Cos(i * Mathf.PI / 3), Mathf.Sin(i * Mathf.PI / 3)) * 28, 20), Pal.Marigold));
                    L.Add(new Layer(Circle(c, 18), Pal.Chili));
                    break;
                case "book":
                    L.Add(new Layer(Box(V(64, 64), V(46, 54), 8), Pal.Chili));
                    L.Add(new Layer(Box(V(70, 64), V(36, 46), 4), Pal.Cream));
                    L.Add(new Layer(Inter(Circle(V(58, 58), 30), Circle(V(82, 82), 30)), Pal.Parrot));
                    break;
                case "sign":
                    L.Add(new Layer(Box(V(64, 30), V(6, 30), 2), Pal.DarkWood));
                    L.Add(new Layer(Box(V(64, 82), V(50, 28), 8), Pal.TruckPink));
                    L.Add(new Layer(Poly(StarPts(V(64, 82), 5, 16, 7)), Pal.Marigold));
                    break;
                case "canopy":
                    L.Add(new Layer(Poly(V(10, 76), V(118, 76), V(100, 112), V(28, 112)), Pal.TruckPink));
                    L.Add(new Layer(Box(V(40, 94), V(8, 18), 0), Pal.Teal));
                    L.Add(new Layer(Box(V(88, 94), V(8, 18), 0), Pal.Teal));
                    L.Add(new Layer(Box(V(20, 40), V(5, 36), 2), Pal.DarkWood));
                    L.Add(new Layer(Box(V(108, 40), V(5, 36), 2), Pal.DarkWood));
                    break;
                case "counter":
                    L.Add(new Layer(Box(V(64, 54), V(54, 30), 6), Pal.DarkWood));
                    L.Add(new Layer(Box(V(64, 48), V(46, 16), 4), Pal.TruckPink));
                    L.Add(new Layer(Poly(StarPts(V(64, 48), 5, 12, 5)), Pal.Marigold));
                    L.Add(new Layer(Box(V(64, 90), V(58, 8), 4), Pal.Clay));
                    break;
                case "rickshaw":
                    L.Add(new Layer(Box(V(60, 64), V(42, 30), 16), Pal.Parrot));
                    L.Add(new Layer(Box(V(60, 96), V(46, 10), 6), Pal.Ink));
                    L.Add(new Layer(Circle(V(30, 30), 13), Pal.Ink));
                    L.Add(new Layer(Circle(V(96, 30), 13), Pal.Ink));
                    L.Add(new Layer(Poly(StarPts(V(60, 66), 5, 14, 6)), Pal.Marigold));
                    break;
                case "road":
                    L.Add(new Layer(Poly(V(44, 10), V(84, 10), V(108, 118), V(20, 118)), Pal.Asphalt));
                    L.Add(new Layer(Box(V(64, 30), V(4, 10), 1), Color.white));
                    L.Add(new Layer(Box(V(64, 66), V(4, 10), 1), Color.white));
                    L.Add(new Layer(Box(V(64, 102), V(4, 10), 1), Color.white));
                    break;
                default:
                    L.Add(new Layer(Circle(c, 40), Pal.ButtonGrey));
                    break;
            }
            return Paint(I, I, L, 5);
        }
    }
}
