using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChaiTycoon
{
    /// <summary>
    /// Renders 3D models into transparent UI sprites at startup so icons always match the world art
    /// (with the same ink outline), without shipping any image files.
    /// </summary>
    public static class IconBaker
    {
        const int Layer = 31;
        const int RenderSize = 192;
        const int OutSize = 96;
        static Camera cam;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Vector3 Stage = new Vector3(0, -400, 0);

        static void EnsureCamera()
        {
            if (cam) return;
            var go = new GameObject("[IconCam]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(1, 0, 1, 0);
            cam.cullingMask = 1 << Layer;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;
        }

        public static Sprite Item(ItemId id) => Skin.Item(id) ?? Mesh("item_" + id, Models.ItemMesh(id), new Vector3(38, 0, 0), 1.25f);

        public static Sprite Mesh(string key, Mesh mesh, Vector3 viewEuler, float zoom = 1f)
        {
            if (cache.TryGetValue(key, out var s) && s) return s;
            var go = Models.Obj("iconTmp", mesh, null);
            s = Bake(key, go, viewEuler, zoom);
            UnityEngine.Object.Destroy(go);
            return s;
        }

        public static Sprite FromObject(string key, GameObject go, Vector3 viewEuler, float zoom = 1f)
        {
            if (cache.TryGetValue(key, out var s) && s) return s;
            return Bake(key, go, viewEuler, zoom);
        }

        static Sprite Bake(string key, GameObject go, Vector3 viewEuler, float zoom)
        {
            EnsureCamera();
            var oldPos = go.transform.position;
            var oldParent = go.transform.parent;
            go.transform.SetParent(null, true);
            go.transform.position = Stage;
            var layers = new List<(GameObject, int)>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                layers.Add((t.gameObject, t.gameObject.layer));
                t.gameObject.layer = Layer;
            }

            // fit bounds
            Bounds b = new Bounds(Stage, Vector3.zero);
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r.name == "blob") continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }

            var rot = Quaternion.Euler(viewEuler);
            cam.transform.rotation = rot;
            cam.transform.position = b.center - rot * Vector3.forward * 20f;
            // project bounds onto camera plane to size ortho
            float ext = 0;
            for (int i = 0; i < 8; i++)
            {
                var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var lp = Quaternion.Inverse(rot) * (c - b.center);
                ext = Mathf.Max(ext, Mathf.Abs(lp.x), Mathf.Abs(lp.y));
            }
            cam.orthographicSize = Mathf.Max(0.05f, ext * 1.08f / zoom);

            var rt = RenderTexture.GetTemporary(RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            cam.targetTexture = rt;
            var req = new RenderPipeline.StandardRequest();
            bool rendered = false;
            try
            {
                if (RenderPipeline.SupportsRenderRequest(cam, req))
                {
                    req.destination = rt;
                    RenderPipeline.SubmitRenderRequest(cam, req);
                    rendered = true;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("Icon render request failed: " + e.Message); }
            if (!rendered) cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var big = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false);
            big.ReadPixels(new Rect(0, 0, RenderSize, RenderSize), 0, 0);
            big.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);

            foreach (var (g, l) in layers) if (g) g.layer = l;
            go.transform.SetParent(oldParent, true);
            go.transform.position = oldPos;

            var tex = Downsample(big);
            UnityEngine.Object.Destroy(big);
            var sprite = Sprite.Create(tex, new Rect(0, 0, OutSize, OutSize), new Vector2(0.5f, 0.5f), 100);
            sprite.name = key;
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>2x2 box downsample; keys out the magenta background when the pipeline drops alpha.</summary>
        static Texture2D Downsample(Texture2D big)
        {
            var src = big.GetPixels32();
            bool alphaKept = src[0].a < 128; // corner should be transparent background
            var outTex = new Texture2D(OutSize, OutSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var dst = new Color32[OutSize * OutSize];
            int f = RenderSize / OutSize;
            for (int y = 0; y < OutSize; y++)
            for (int x = 0; x < OutSize; x++)
            {
                float r = 0, g = 0, bl = 0, a = 0;
                for (int dy = 0; dy < f; dy++)
                for (int dx = 0; dx < f; dx++)
                {
                    var p = src[(y * f + dy) * RenderSize + (x * f + dx)];
                    float pa;
                    if (alphaKept) pa = p.a / 255f;
                    else pa = (p.r > 235 && p.g < 30 && p.b > 235) ? 0 : 1;
                    r += p.r * pa; g += p.g * pa; bl += p.b * pa; a += pa;
                }
                int n = f * f;
                if (a > 0.001f) dst[y * OutSize + x] = new Color32((byte)(r / a), (byte)(g / a), (byte)(bl / a), (byte)(255 * a / n));
                else dst[y * OutSize + x] = new Color32(0, 0, 0, 0);
            }
            outTex.SetPixels32(dst);
            outTex.Apply();
            return outTex;
        }
    }
}
