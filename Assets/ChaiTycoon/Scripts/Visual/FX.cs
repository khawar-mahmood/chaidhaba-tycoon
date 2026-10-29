using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Chunky cartoon effects (puffs, confetti, steam, sparkles, coin arcs) from pooled low-poly
    /// meshes. Particles shrink away instead of fading, so everything stays opaque and cheap.
    /// </summary>
    public class FX : MonoBehaviour
    {
        class P
        {
            public Transform t;
            public Vector3 vel, spin;
            public float life, maxLife, size, gravity, drag;
            public bool shrinkIn;
            public Vector3 from, to; public float arc; public bool isArc;
        }

        readonly List<P> live = new List<P>();
        readonly Dictionary<Mesh, Stack<Transform>> pool = new Dictionary<Mesh, Stack<Transform>>();
        readonly Dictionary<Transform, Mesh> meshOf = new Dictionary<Transform, Mesh>();
        public static bool LowQuality;

        static Mesh SphereMesh(Color c) => Models.Cached("fx_sphere_" + ColorUtility.ToHtmlStringRGB(c), b => b.Sphere(Vector3.zero, 1f, c, 0));
        static Mesh BoxMesh(Color c) => Models.Cached("fx_box_" + ColorUtility.ToHtmlStringRGB(c), b => b.Box(Vector3.zero, new Vector3(1f, 0.25f, 0.6f), c));
        static Mesh StarMesh(Color c) => Models.Cached("fx_star_" + ColorUtility.ToHtmlStringRGB(c), b => b.Prism(new Vector3(0, -0.1f, 0), MeshBuilder.StarOutline(4, 0.5f, 0.15f), 0.2f, c, Quaternion.Euler(-90, 0, 0)));

        Transform Get(Mesh m, bool outline)
        {
            if (pool.TryGetValue(m, out var st) && st.Count > 0)
            {
                var t = st.Pop();
                t.gameObject.SetActive(true);
                return t;
            }
            var go = Models.Obj("fx", m, transform, outline);
            meshOf[go.transform] = m;
            return go.transform;
        }

        void Release(Transform t)
        {
            t.gameObject.SetActive(false);
            var m = meshOf[t];
            if (!pool.TryGetValue(m, out var st)) pool[m] = st = new Stack<Transform>();
            st.Push(t);
        }

        P Spawn(Mesh m, Vector3 pos, Vector3 vel, float life, float size, bool outline = false)
        {
            if (live.Count > 220) return null;
            var t = Get(m, outline);
            t.position = pos;
            t.rotation = Random.rotation;
            t.localScale = Vector3.one * size;
            var p = new P { t = t, vel = vel, life = life, maxLife = life, size = size, spin = Random.insideUnitSphere * 360f };
            live.Add(p);
            return p;
        }

        public void Puff(Vector3 pos, int count, Color color, float size)
        {
            if (LowQuality) count = Mathf.Max(1, count / 2);
            var m = SphereMesh(Color.Lerp(color, Color.white, 0.35f));
            for (int i = 0; i < count; i++)
            {
                var dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) * 0.5f;
                var p = Spawn(m, pos + Vector3.up * 0.15f + dir * 0.2f, dir * Random.Range(1.2f, 2.5f) + Vector3.up * 0.8f, Random.Range(0.35f, 0.55f), size * Random.Range(0.7f, 1.2f));
                if (p != null) { p.drag = 5f; p.shrinkIn = true; }
            }
        }

        public void Steam(Vector3 pos, float scale = 1f)
        {
            if (LowQuality && Random.value < 0.5f) return;
            var m = SphereMesh(Pal.White);
            var p = Spawn(m, pos + Random.insideUnitSphere * 0.08f, new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.6f, 0.9f), Random.Range(-0.1f, 0.1f)), Random.Range(0.9f, 1.3f), 0.16f * scale);
            if (p != null) { p.shrinkIn = true; p.drag = 0.3f; p.spin = Vector3.zero; }
        }

        public void Confetti(Vector3 pos, int count)
        {
            if (LowQuality) count /= 2;
            Color[] cols = { Pal.TruckPink, Pal.Marigold, Pal.Parrot, Pal.Teal, Pal.Chili, Pal.Royal };
            for (int i = 0; i < count; i++)
            {
                var m = BoxMesh(cols[i % cols.Length]);
                var v = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(4f, 7f), Random.Range(-2.5f, 2.5f));
                var p = Spawn(m, pos, v, Random.Range(0.9f, 1.4f), 0.14f);
                if (p != null) { p.gravity = 11f; p.drag = 1.2f; p.spin = Random.insideUnitSphere * 720f; }
            }
        }

        public void Sparkle(Vector3 pos, int count)
        {
            var m = StarMesh(Pal.Hex("#FFF6C8"));
            for (int i = 0; i < count; i++)
            {
                var p = Spawn(m, pos + Random.insideUnitSphere * 0.15f, Vector3.up * 0.5f, 0.5f, 0.28f);
                if (p != null) { p.spin = new Vector3(0, 0, 360); p.shrinkIn = true; p.t.rotation = Quaternion.Euler(50, 0, 0); }
            }
        }

        public void CoinArc(Vector3 from, Vector3 to)
        {
            var p = Spawn(Models.CoinMesh(), from, Vector3.zero, 0.28f, 1f, true);
            if (p == null) return;
            p.isArc = true; p.from = from; p.to = to; p.arc = 1.0f;
            p.spin = new Vector3(720, 0, 0);
        }

        public void Emote(Customer c, string icon)
        {
            Game.I.WUI.Emote(c.BubbleAnchor + Vector3.up * 0.2f, icon);
            if (icon == "cloud") Puff(c.transform.position + Vector3.up * 2.0f, 3, Pal.Hex("#9EA0A8"), 0.3f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.life -= dt;
                if (p.life <= 0 || !p.t)
                {
                    if (p.t) Release(p.t);
                    live.RemoveAt(i);
                    continue;
                }
                float k = 1 - p.life / p.maxLife;
                if (p.isArc)
                {
                    var pos = Vector3.Lerp(p.from, p.to, k);
                    pos.y += Mathf.Sin(k * Mathf.PI) * p.arc;
                    p.t.position = pos;
                    p.t.Rotate(p.spin * dt, Space.Self);
                    continue;
                }
                p.vel.y -= p.gravity * dt;
                p.vel *= Mathf.Exp(-p.drag * dt);
                p.t.position += p.vel * dt;
                p.t.Rotate(p.spin * dt, Space.Self);
                float s = p.shrinkIn ? (k < 0.2f ? Mathf.Lerp(0.3f, 1f, k / 0.2f) : Mathf.Lerp(1f, 0f, (k - 0.2f) / 0.8f)) : (k > 0.7f ? Mathf.Lerp(1, 0, (k - 0.7f) / 0.3f) : 1);
                p.t.localScale = Vector3.one * p.size * Mathf.Max(0.001f, s);
            }
        }
    }
}
