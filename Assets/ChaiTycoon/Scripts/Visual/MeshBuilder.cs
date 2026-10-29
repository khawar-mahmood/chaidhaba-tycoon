using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Builds flat-shaded, vertex-coloured low-poly meshes out of transformed primitives.
    /// Each primitive also stores smoothed normals in UV3 so the toon outline stays closed.
    /// </summary>
    public class MeshBuilder
    {
        readonly List<Vector3> pos = new List<Vector3>(512);
        readonly List<Vector3> nrm = new List<Vector3>(512);
        readonly List<Color> col = new List<Color>(512);
        readonly List<Vector3> outl = new List<Vector3>(512);
        readonly List<int> tri = new List<int>(1024);

        Matrix4x4 m = Matrix4x4.identity;
        /// <summary>Extra transform applied after each primitive's own (e.g. to scale a whole model).</summary>
        public Matrix4x4 Root = Matrix4x4.identity;
        Color c = Color.white;
        int primStart;
        Vector3 primCenter;

        public int VertexCount => pos.Count;

        static Matrix4x4 TRS(Vector3 p, Quaternion r, Vector3 s) => Matrix4x4.TRS(p, r, s);

        // ---------- primitive API (positions in the builder's local space) ----------

        public MeshBuilder Box(Vector3 center, Vector3 size, Color color, Quaternion? rot = null)
        {
            Begin(TRS(center, rot ?? Quaternion.identity, size), color);
            Vector3 P(float x, float y, float z) => new Vector3(x * 0.5f, y * 0.5f, z * 0.5f);
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1));
            Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1));
            Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1));
            Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1));
            Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));
            Quad(P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), P(-1, -1, -1));
            End();
            return this;
        }

        /// <summary>Box with chamfered (bevelled) vertical edges, reads softer than a plain box.</summary>
        public MeshBuilder BevelBox(Vector3 center, Vector3 size, float bevel, Color color, Quaternion? rot = null)
        {
            var r = rot ?? Quaternion.identity;
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            float b = Mathf.Min(bevel, Mathf.Min(hx, hz) * 0.9f);
            // octagon footprint extruded in y
            var ring = new Vector3[]
            {
                new Vector3(-hx + b, 0, -hz), new Vector3(hx - b, 0, -hz), new Vector3(hx, 0, -hz + b), new Vector3(hx, 0, hz - b),
                new Vector3(hx - b, 0, hz), new Vector3(-hx + b, 0, hz), new Vector3(-hx, 0, hz - b), new Vector3(-hx, 0, -hz + b)
            };
            Begin(TRS(center, r, Vector3.one), color);
            for (int i = 0; i < 8; i++)
            {
                var a = ring[i]; var d = ring[(i + 1) % 8];
                Quad(a + Vector3.down * hy, a + Vector3.up * hy, d + Vector3.up * hy, d + Vector3.down * hy);
            }
            for (int i = 1; i < 7; i++)
            {
                Tri(ring[0] + Vector3.up * hy, ring[i] + Vector3.up * hy, ring[i + 1] + Vector3.up * hy);
                Tri(ring[0] + Vector3.down * hy, ring[i] + Vector3.down * hy, ring[i + 1] + Vector3.down * hy);
            }
            End();
            return this;
        }

        /// <summary>Cylinder/cone from base (y=0) to top (y=height), centred on 'basePos'.</summary>
        public MeshBuilder Cylinder(Vector3 basePos, float radiusBottom, float radiusTop, float height, int sides, Color color, Quaternion? rot = null, bool caps = true)
        {
            Begin(TRS(basePos, rot ?? Quaternion.identity, Vector3.one), color);
            primCenter = new Vector3(0, height * 0.5f, 0);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var b0 = new Vector3(Mathf.Cos(a0) * radiusBottom, 0, Mathf.Sin(a0) * radiusBottom);
                var b1 = new Vector3(Mathf.Cos(a1) * radiusBottom, 0, Mathf.Sin(a1) * radiusBottom);
                var t0 = new Vector3(Mathf.Cos(a0) * radiusTop, height, Mathf.Sin(a0) * radiusTop);
                var t1 = new Vector3(Mathf.Cos(a1) * radiusTop, height, Mathf.Sin(a1) * radiusTop);
                Quad(b0, t0, t1, b1);
                if (caps)
                {
                    if (radiusBottom > 0.0001f) Tri(new Vector3(0, 0, 0), b0, b1);
                    if (radiusTop > 0.0001f) Tri(new Vector3(0, height, 0), t0, t1);
                }
            }
            End();
            return this;
        }

        /// <summary>Low-poly sphere (icosphere, subdiv 0 or 1) or ellipsoid via scale.</summary>
        public MeshBuilder Sphere(Vector3 center, Vector3 size, Color color, int subdiv = 1, Quaternion? rot = null)
        {
            Begin(TRS(center, rot ?? Quaternion.identity, size), color);
            var mesh = Ico(subdiv);
            for (int i = 0; i < mesh.Count; i += 3) Tri(mesh[i], mesh[i + 1], mesh[i + 2]);
            End();
            return this;
        }

        public MeshBuilder Sphere(Vector3 center, float diameter, Color color, int subdiv = 1) =>
            Sphere(center, Vector3.one * diameter, color, subdiv);

        /// <summary>Upper half of a UV sphere (hair, domes, caps).</summary>
        public MeshBuilder Dome(Vector3 center, Vector3 size, int sides, int rings, Color color, Quaternion? rot = null)
        {
            Begin(TRS(center, rot ?? Quaternion.identity, size), color);
            primCenter = new Vector3(0, 0.1f, 0);
            for (int r = 0; r < rings; r++)
            {
                float p0 = r * Mathf.PI * 0.5f / rings, p1 = (r + 1) * Mathf.PI * 0.5f / rings;
                for (int s = 0; s < sides; s++)
                {
                    float a0 = s * Mathf.PI * 2 / sides, a1 = (s + 1) * Mathf.PI * 2 / sides;
                    Vector3 V(float a, float p) => new Vector3(Mathf.Cos(a) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Sin(a) * Mathf.Cos(p)) * 0.5f;
                    if (r == rings - 1) Tri(V(a0, p0), V(a0, p1), V(a1, p0));
                    else Quad(V(a0, p0), V(a0, p1), V(a1, p1), V(a1, p0));
                }
            }
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2 / sides, a1 = (s + 1) * Mathf.PI * 2 / sides;
                Tri(Vector3.zero, new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 0.5f, new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 0.5f);
            }
            End();
            return this;
        }

        /// <summary>Triangular wedge (samosa, roof). Footprint triangle in xz, extruded in y.</summary>
        public MeshBuilder Wedge(Vector3 center, Vector3 size, Color color, Quaternion? rot = null)
        {
            Begin(TRS(center, rot ?? Quaternion.identity, size), color);
            var a = new Vector3(0, 0, 0.5f); var b = new Vector3(0.5f, 0, -0.5f); var d = new Vector3(-0.5f, 0, -0.5f);
            var up = new Vector3(0, 0.5f, 0); var dn = new Vector3(0, -0.5f, 0);
            Tri(a + up, b + up, d + up);
            Tri(a + dn, b + dn, d + dn);
            Quad(a + dn, a + up, b + up, b + dn);
            Quad(b + dn, b + up, d + up, d + dn);
            Quad(d + dn, d + up, a + up, a + dn);
            End();
            return this;
        }

        /// <summary>Pyramid-like samosa: triangle base with a raised, pinched top ridge.</summary>
        public MeshBuilder Tetra(Vector3 center, Vector3 size, Color color, Quaternion? rot = null)
        {
            Begin(TRS(center, rot ?? Quaternion.identity, size), color);
            var a = new Vector3(0, -0.5f, 0.55f); var b = new Vector3(0.5f, -0.5f, -0.35f); var d = new Vector3(-0.5f, -0.5f, -0.35f);
            var t = new Vector3(0, 0.5f, 0);
            Tri(a, b, d); Tri(a, t, b); Tri(b, t, d); Tri(d, t, a);
            End();
            return this;
        }

        /// <summary>Flat torus ring lying in xz plane (cup handles, coins rims, garlands).</summary>
        public MeshBuilder Torus(Vector3 center, float radius, float thickness, int sides, int tubeSides, Color color, Quaternion? rot = null)
        {
            Begin(TRS(center, rot ?? Quaternion.identity, Vector3.one), color);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                for (int j = 0; j < tubeSides; j++)
                {
                    float b0 = j * Mathf.PI * 2 / tubeSides, b1 = (j + 1) * Mathf.PI * 2 / tubeSides;
                    Vector3 V(float a, float b) => new Vector3(Mathf.Cos(a) * (radius + Mathf.Cos(b) * thickness), Mathf.Sin(b) * thickness, Mathf.Sin(a) * (radius + Mathf.Cos(b) * thickness));
                    // winding fixed per-face from the tube centre
                    var tubeC = new Vector3(Mathf.Cos((a0 + a1) * 0.5f) * radius, 0, Mathf.Sin((a0 + a1) * 0.5f) * radius);
                    primCenter = tubeC;
                    Quad(V(a0, b0), V(a0, b1), V(a1, b1), V(a1, b0));
                }
            }
            primCenter = Vector3.zero;
            End(smoothFromCenter: false);
            return this;
        }

        /// <summary>Flat quad in the xz plane facing up (decals, stripes). No outline normals needed.</summary>
        public MeshBuilder FlatQuad(Vector3 center, Vector2 size, Color color, float yaw = 0)
        {
            Begin(TRS(center, Quaternion.Euler(0, yaw, 0), new Vector3(size.x, 1, size.y)), color);
            primCenter = new Vector3(0, -1, 0);
            Quad(new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f));
            End();
            return this;
        }

        /// <summary>Flat disc in the xz plane facing up.</summary>
        public MeshBuilder FlatDisc(Vector3 center, float radius, int sides, Color color)
        {
            Begin(TRS(center, Quaternion.identity, Vector3.one), color);
            primCenter = new Vector3(0, -1, 0);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                Tri(Vector3.zero, new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius, new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius);
            }
            End();
            return this;
        }

        /// <summary>Vertical flat panel facing -z (signs). Two-sided by default.</summary>
        public MeshBuilder Panel(Vector3 center, Vector2 size, Color color, Quaternion? rot = null, bool twoSided = true)
        {
            Begin(TRS(center, rot ?? Quaternion.identity, new Vector3(size.x, size.y, 1)), color);
            primCenter = new Vector3(0, 0, 1);
            Quad(new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0));
            if (twoSided)
            {
                primCenter = new Vector3(0, 0, -1);
                Quad(new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(-0.5f, -0.5f, 0));
            }
            End();
            return this;
        }

        /// <summary>Arbitrary convex polygon extruded in y (flowers, stars, leaves on signs).</summary>
        public MeshBuilder Prism(Vector3 center, Vector2[] outline, float height, Color color, Quaternion? rot = null)
        {
            Begin(TRS(center, rot ?? Quaternion.identity, Vector3.one), color);
            int n = outline.Length;
            Vector3 cxz = Vector3.zero;
            foreach (var p in outline) cxz += new Vector3(p.x, 0, p.y);
            cxz /= n;
            primCenter = cxz + Vector3.up * height * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var a = new Vector3(outline[i].x, 0, outline[i].y);
                var b = new Vector3(outline[(i + 1) % n].x, 0, outline[(i + 1) % n].y);
                Quad(a, a + Vector3.up * height, b + Vector3.up * height, b);
                // fans from centroid so star shapes work too
                var ct = cxz + Vector3.up * height;
                var cb = cxz;
                TriFixed(ct, a + Vector3.up * height, b + Vector3.up * height, Vector3.up);
                TriFixed(cb, a, b, Vector3.down);
            }
            End();
            return this;
        }

        public static Vector2[] StarOutline(int points, float outer, float inner)
        {
            var arr = new Vector2[points * 2];
            for (int i = 0; i < points * 2; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / points;
                float r = (i % 2 == 0) ? outer : inner;
                arr[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return arr;
        }

        public static Vector2[] CircleOutline(int sides, float r, float sx = 1, float sy = 1)
        {
            var arr = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                arr[i] = new Vector2(Mathf.Cos(a) * r * sx, Mathf.Sin(a) * r * sy);
            }
            return arr;
        }

        /// <summary>
        /// Adds ready-made triangles (art exported from Blender). Each part is one primitive, so the outline
        /// normals are smoothed within a part just like the procedural shapes.
        /// </summary>
        public MeshBuilder Raw(Vector3[] verts, int[] tris, Color[] triColors, int[] triParts)
        {
            int cur = int.MinValue;
            for (int t = 0; t < triColors.Length; t++)
            {
                if (triParts[t] != cur)
                {
                    if (cur != int.MinValue) End();
                    Begin(Matrix4x4.identity, triColors[t]);
                    cur = triParts[t];
                }
                c = triColors[t].linear;
                Emit(verts[tris[t * 3]], verts[tris[t * 3 + 1]], verts[tris[t * 3 + 2]]);
            }
            if (cur != int.MinValue) End();
            return this;
        }

        // ---------- internals ----------

        void Begin(Matrix4x4 mat, Color color)
        {
            m = mat;
            c = color.linear;
            primStart = pos.Count;
            primCenter = Vector3.zero;
        }

        void End(bool smoothFromCenter = false)
        {
            // Smoothed normals for the outline: average face normals of coincident vertices within this primitive.
            var sums = new Dictionary<Vector3Int, Vector3>();
            for (int i = primStart; i < pos.Count; i++)
            {
                var k = Key(pos[i]);
                sums.TryGetValue(k, out var s);
                sums[k] = s + nrm[i];
            }
            for (int i = primStart; i < pos.Count; i++)
            {
                var s = sums[Key(pos[i])];
                outl.Add(s.sqrMagnitude > 1e-8f ? s.normalized : nrm[i]);
            }
        }

        static Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f));

        void Quad(Vector3 a, Vector3 b, Vector3 d, Vector3 e)
        {
            Tri(a, b, d);
            Tri(a, d, e);
        }

        /// <summary>Adds a triangle, auto-fixing its winding so it faces away from the primitive centre.</summary>
        void Tri(Vector3 a, Vector3 b, Vector3 d)
        {
            var n = Vector3.Cross(b - a, d - a);
            if (n.sqrMagnitude < 1e-12f) return;
            var mid = (a + b + d) / 3f;
            if (Vector3.Dot(n, mid - primCenter) < 0) { var t = b; b = d; d = t; }
            Emit(a, b, d);
        }

        void TriFixed(Vector3 a, Vector3 b, Vector3 d, Vector3 desiredNormal)
        {
            var n = Vector3.Cross(b - a, d - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, desiredNormal) < 0) { var t = b; b = d; d = t; }
            Emit(a, b, d);
        }

        void Emit(Vector3 a, Vector3 b, Vector3 d)
        {
            var rm = Root * m;
            a = rm.MultiplyPoint3x4(a); b = rm.MultiplyPoint3x4(b); d = rm.MultiplyPoint3x4(d);
            var n = Vector3.Cross(b - a, d - a);
            if (n.sqrMagnitude < 1e-14f) return;
            // a mirrored matrix would flip the winding; Cross in world space keeps it consistent
            n.Normalize();
            int i = pos.Count;
            pos.Add(a); pos.Add(b); pos.Add(d);
            nrm.Add(n); nrm.Add(n); nrm.Add(n);
            col.Add(c); col.Add(c); col.Add(c);
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
        }

        static readonly Dictionary<int, List<Vector3>> icoCache = new Dictionary<int, List<Vector3>>();

        static List<Vector3> Ico(int subdiv)
        {
            if (icoCache.TryGetValue(subdiv, out var cached)) return cached;
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            int[] f =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            var tris = new List<Vector3>();
            for (int i = 0; i < f.Length; i++) tris.Add(v[f[i]].normalized * 0.5f);
            for (int s = 0; s < subdiv; s++)
            {
                var next = new List<Vector3>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    var a = tris[i]; var b = tris[i + 1]; var d = tris[i + 2];
                    var ab = ((a + b) * 0.5f).normalized * 0.5f;
                    var bd = ((b + d) * 0.5f).normalized * 0.5f;
                    var da = ((d + a) * 0.5f).normalized * 0.5f;
                    next.AddRange(new[] { a, ab, da, b, bd, ab, d, da, bd, ab, bd, da });
                }
                tris = next;
            }
            icoCache[subdiv] = tris;
            return tris;
        }

        public Mesh Build(string name = "mesh")
        {
            var mesh = new Mesh { name = name };
            if (pos.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(pos);
            mesh.SetNormals(nrm);
            mesh.SetColors(col);
            mesh.SetUVs(3, outl);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}
