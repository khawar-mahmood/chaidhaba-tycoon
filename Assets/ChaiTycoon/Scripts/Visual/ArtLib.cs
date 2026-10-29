using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Art modelled in Blender (Art/Blender/ChaiTycoon_Stage1.blend), exported to Resources/Art as compact
    /// binary packs, plus layouts (where each prop and character stands in a stage). Meshes are built on
    /// first use. Two kinds of mesh:
    ///  * props and the environment: normals, UVs and one submesh per material, drawn with Unity's standard
    ///    URP Lit materials from Resources/Art/Mats (made by Editor/ArtMaterials.cs). The kit props use the kits'
    ///    own texture sets; plain-coloured props use the shared palette texture.
    ///  * characters: flat-shaded, vertex-coloured triangles with one outline part per body part (toon shader).
    /// art_core holds stage 1 and every character; art_stage2..5 load only when needed.
    /// </summary>
    public static class ArtLib
    {
        public struct Entry
        {
            public string name;
            public Vector3 pos;
            public float rotY, scale;
            public string extra;
        }

        class Raw
        {
            public byte[] data;
            public int offset;
            public float[] meta;
            public int kind;        // 0 vertex-coloured toon (characters), 3 lit with materials (props, environment)
            public int version;     // 4+: UVs are int16 with a per-mesh scale (tiling ground textures)
        }

        static readonly Dictionary<string, Raw> raws = new Dictionary<string, Raw>();
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<Mesh, Material[]> meshMats = new Dictionary<Mesh, Material[]>();
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly Dictionary<string, List<Entry>> layouts = new Dictionary<string, List<Entry>>();
        static readonly HashSet<string> loaded = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            raws.Clear(); meshes.Clear(); meshMats.Clear(); mats.Clear(); layouts.Clear(); loaded.Clear();
        }

        /// <summary>The standard Lit materials for a prop mesh (one per submesh), or null for the toon-shaded art.</summary>
        public static Material[] MaterialsFor(Mesh mesh) => mesh != null && meshMats.TryGetValue(mesh, out var m) ? m : null;

        /// <summary>Loads a pack from Resources/Art (once). Returns false when it isn't there.</summary>
        public static bool Load(string pack)
        {
            if (loaded.Contains(pack)) return true;
            var ta = Resources.Load<TextAsset>("Art/" + pack);
            if (ta == null) return false;
            loaded.Add(pack);
            try { Parse(ta.bytes); }
            catch (System.Exception e) { Debug.LogWarning($"ArtLib: could not read {pack}: {e.Message}"); return false; }
            Resources.UnloadAsset(ta);
            return true;
        }

        public static bool Core => Load("art_core");

        public static bool Has(string name) => name != null && Core && raws.ContainsKey(name);

        public static float[] Meta(string name) => Has(name) ? raws[name].meta : null;

        public static List<Entry> Layout(string name) => layouts.TryGetValue(name, out var l) ? l : null;

        /// <summary>The mesh, or null when the art isn't available (callers fall back to the procedural model).</summary>
        public static Mesh Get(string name)
        {
            if (name == null) return null;
            if (meshes.TryGetValue(name, out var m) && m) return m;
            if (!Has(name)) return null;
            var raw = raws[name];
            if (raw.kind == 3)
            {
                m = BuildLit(name, raw, out var matNames);
                var list = new Material[matNames.Length];
                for (int i = 0; i < list.Length; i++) list[i] = Mat(matNames[i]);
                meshMats[m] = list;
            }
            else m = Build(name, raw);
            meshes[name] = m;
            return m;
        }

        static Material Mat(string name)
        {
            if (mats.TryGetValue(name, out var m) && m) return m;
            m = Resources.Load<Material>("Art/Mats/" + name);
            if (!m)
            {
                Debug.LogWarning($"ArtLib: missing material Art/Mats/{name} (open the project in the editor to create it)");
                m = Mats.Toon;
            }
            mats[name] = m;
            return m;
        }

        /// <summary>A GameObject with the art mesh and its materials, or null.</summary>
        public static GameObject Obj(string name, Transform parent, bool outline = true)
        {
            var m = Get(name);
            return m ? Models.Obj(name, m, parent, outline) : null;
        }

        // ------------------------------------------------------------------ parsing

        static string Str(BinaryReader r) => Encoding.UTF8.GetString(r.ReadBytes(r.ReadByte()));

        static void Parse(byte[] data)
        {
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "CDTM") throw new IOException("bad magic");
                int version = r.ReadUInt16();
                int meshCount = r.ReadUInt16();
                for (int i = 0; i < meshCount; i++)
                {
                    string name = Str(r);
                    int mc = r.ReadByte();
                    var meta = new float[mc];
                    for (int k = 0; k < mc; k++) meta[k] = r.ReadSingle();
                    int kind = version >= 2 ? r.ReadByte() : 0;
                    if (kind != 0 && kind != 3) throw new IOException($"{name}: unsupported mesh kind {kind}, re-export the art");
                    int start = (int)r.BaseStream.Position;
                    raws[name] = new Raw { data = data, offset = start, meta = meta, kind = kind, version = version };
                    // skip the geometry; it's decoded on first use
                    r.ReadSingle();
                    if (kind == 3)
                    {
                        if (version >= 4) r.ReadSingle();            // uv scale
                        int subs = r.ReadByte();
                        int tris = 0;
                        for (int s = 0; s < subs; s++) { Str(r); tris += (int)r.ReadUInt32(); }
                        int lvc = (int)r.ReadUInt32();
                        r.BaseStream.Position += lvc * 13;               // i16 x3 position, i8 x3 normal, u16 x2 uv
                        r.BaseStream.Position += tris * (lvc > 65535 ? 12 : 6);
                        continue;
                    }
                    int pal = r.ReadByte();
                    r.BaseStream.Position += pal * 3;
                    int vc = (int)r.ReadUInt32();
                    r.BaseStream.Position += vc * 6;
                    int tc = (int)r.ReadUInt32();
                    r.BaseStream.Position += tc * ((vc > 65535 ? 12 : 6) + 3);
                }
                int layoutCount = r.ReadUInt16();
                for (int i = 0; i < layoutCount; i++)
                {
                    string lname = Str(r);
                    int n = r.ReadUInt16();
                    var list = new List<Entry>(n);
                    for (int k = 0; k < n; k++)
                    {
                        var e = new Entry { name = Str(r) };
                        e.pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        e.rotY = r.ReadSingle();
                        e.scale = r.ReadSingle();
                        e.extra = Str(r);
                        list.Add(e);
                    }
                    layouts[lname] = list;
                }
            }
        }

        /// <summary>Characters: flat-shaded toon triangles with per-part outline normals.</summary>
        static Mesh Build(string name, Raw raw)
        {
            using (var r = new BinaryReader(new MemoryStream(raw.data)))
            {
                r.BaseStream.Position = raw.offset;
                float scale = r.ReadSingle();
                int pal = r.ReadByte();
                var colors = new Color[pal];
                for (int i = 0; i < pal; i++) colors[i] = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), 255);
                int vc = (int)r.ReadUInt32();
                var verts = new Vector3[vc];
                for (int i = 0; i < vc; i++) verts[i] = new Vector3(r.ReadInt16(), r.ReadInt16(), r.ReadInt16()) * scale;
                int tc = (int)r.ReadUInt32();
                bool wide = vc > 65535;
                var tris = new int[tc * 3];
                var tcol = new Color[tc];
                var tpart = new int[tc];
                for (int t = 0; t < tc; t++)
                {
                    for (int k = 0; k < 3; k++) tris[t * 3 + k] = wide ? (int)r.ReadUInt32() : r.ReadUInt16();
                    tcol[t] = colors[r.ReadByte()];
                    tpart[t] = r.ReadUInt16();
                }
                return new MeshBuilder().Raw(verts, tris, tcol, tpart).Build(name);
            }
        }

        /// <summary>Props and environment: Blender's normals and UVs, one submesh per material.</summary>
        static Mesh BuildLit(string name, Raw raw, out string[] matNames)
        {
            using (var r = new BinaryReader(new MemoryStream(raw.data)))
            {
                r.BaseStream.Position = raw.offset;
                float scale = r.ReadSingle();
                float uvScale = raw.version >= 4 ? r.ReadSingle() : 0f;
                int subs = r.ReadByte();
                matNames = new string[subs];
                var counts = new int[subs];
                for (int s = 0; s < subs; s++) { matNames[s] = Str(r); counts[s] = (int)r.ReadUInt32(); }
                int vc = (int)r.ReadUInt32();
                var verts = new Vector3[vc];
                var normals = new Vector3[vc];
                var uvs = new Vector2[vc];
                for (int i = 0; i < vc; i++)
                {
                    verts[i] = new Vector3(r.ReadInt16(), r.ReadInt16(), r.ReadInt16()) * scale;
                    normals[i] = new Vector3(r.ReadSByte(), r.ReadSByte(), r.ReadSByte()).normalized;
                    uvs[i] = raw.version >= 4 ? new Vector2(r.ReadInt16() * uvScale, r.ReadInt16() * uvScale)
                                              : new Vector2(r.ReadUInt16() / 65535f, r.ReadUInt16() / 65535f);
                }
                bool wide = vc > 65535;
                var m = new Mesh { name = name };
                if (wide) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.vertices = verts;
                m.normals = normals;
                m.uv = uvs;
                m.subMeshCount = subs;
                for (int s = 0; s < subs; s++)
                {
                    var tris = new int[counts[s] * 3];
                    for (int t = 0; t < tris.Length; t++) tris[t] = wide ? (int)r.ReadUInt32() : r.ReadUInt16();
                    m.SetTriangles(tris, s);
                }
                m.RecalculateTangents();       // for the kits' normal maps
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
