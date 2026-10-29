using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChaiTycoon.EditorTools
{
    /// <summary>
    /// The Tripo characters (Rafi, Helper, Cook, Customer1-4), reduced and re-exported from Blender to
    /// Assets/ChaiTycoon/Crowd/&lt;Name&gt;/&lt;Name&gt;.fbx with &lt;Name&gt;_Base.png and &lt;Name&gt;_Normal.png.
    ///  * the FBX imports as Legacy animation (clips idle / walk / run / sit) without its own materials
    ///  * the normal map imports as a normal map (it came in as a colour texture, which made the shading look
    ///    distorted); colour maps at most 1024 px, normal maps 256 px
    ///  * a prefab per character goes to Resources/Crowd/&lt;Name&gt;.prefab: the model, a URP Lit material and an
    ///    Animation component with looping clips. Every character uses the same clips (Rafi's idle / walk /
    ///    run, the cook's sit; all seven share one Mixamo skeleton), with a per-model hip shift in
    ///    CrowdRigInfo. CharRig (Scripts/Visual/Models.cs) drives it at runtime.
    /// Rebuilt after the files import, or with Chai › Crowd › Rebuild Prefabs.
    /// </summary>
    [InitializeOnLoad]
    public class CrowdImport : AssetPostprocessor
    {
        // prefabs made before the crowd shared one set of clips are rebuilt once
        static CrowdImport()
        {
            EditorApplication.delayCall += () =>
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(OutDir + "/Rafi.prefab");
                if (p != null && p.GetComponent<CrowdRigInfo>() == null) Build();
            };
        }

        const string SrcDir = "Assets/ChaiTycoon/Crowd";
        const string OutDir = "Assets/ChaiTycoon/Resources/Crowd";

        bool Ours => assetPath.Replace('\\', '/').StartsWith(SrcDir + "/");

        void OnPreprocessModel()
        {
            if (!Ours) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Legacy;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.animationCompression = ModelImporterAnimationCompression.KeyframeReductionAndCompression;
        }

        void OnPreprocessTexture()
        {
            if (!Ours) return;
            var ti = (TextureImporter)assetImporter;
            bool normal = Path.GetFileNameWithoutExtension(assetPath).EndsWith("_Normal");
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal;
            ti.maxTextureSize = normal ? 256 : 1024;      // download budget: normal maps 256 px, colour 1024
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.crunchedCompression = true;
            ti.compressionQuality = 85;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var p in imported)
                if (p.Replace('\\', '/').StartsWith(SrcDir + "/")) { EditorApplication.delayCall -= Build; EditorApplication.delayCall += Build; return; }
        }

        /// <summary>Which model's clip the whole crowd uses for each animation.</summary>
        static readonly KeyValuePair<string, string>[] ClipSource =
        {
            new KeyValuePair<string, string>("idle", "Rafi"),
            new KeyValuePair<string, string>("walk", "Rafi"),
            new KeyValuePair<string, string>("run", "Rafi"),
            new KeyValuePair<string, string>("sit", "Cook"),
        };

        static IEnumerable<AnimationState> EnumStates(Animation a) { foreach (AnimationState s in a) yield return s; }

        static AnimationClip FindClip(string fbxPath, string key)
        {
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    string k = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
                    if (k == key) return c;
                }
            return null;
        }

        /// <summary>The hips' rest position (local to its parent) in a model as imported.</summary>
        static Vector3? HipsRest(GameObject model)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith(":Hips")) return t.localPosition;
            return null;
        }

        [MenuItem("Chai/Crowd/Rebuild Prefabs")]
        public static void Build()
        {
            if (!Directory.Exists(SrcDir)) return;
            Directory.CreateDirectory(OutDir);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var dir in Directory.GetDirectories(SrcDir))
            {
                var name = Path.GetFileName(dir);
                var fbxPath = $"{SrcDir}/{name}/{name}.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (!model) continue;

                // material
                var matPath = $"{OutDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                bool newMat = mat == null;
                if (newMat) mat = new Material(lit);
                mat.shader = lit;
                var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SrcDir}/{name}/{name}_Base.png");
                var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SrcDir}/{name}/{name}_Normal.png");
                mat.SetTexture("_BaseMap", baseTex); mat.mainTexture = baseTex;
                mat.SetColor("_BaseColor", Color.white);
                if (nrm) { mat.SetTexture("_BumpMap", nrm); mat.EnableKeyword("_NORMALMAP"); }
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.22f);
                if (newMat) AssetDatabase.CreateAsset(mat, matPath); else EditorUtility.SetDirty(mat);

                // prefab: model + material + looping legacy clips
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = name;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                    if (r is SkinnedMeshRenderer smr) { smr.updateWhenOffscreen = false; smr.quality = SkinQuality.Bone2; }
                }
                var anim = go.GetComponent<Animation>();
                if (anim == null) anim = go.AddComponent<Animation>();
                anim.playAutomatically = false;
                // always animate: CharRig adds the hip shift every frame on top of the pose the clip writes
                anim.cullingType = AnimationCullingType.AlwaysAnimate;
                foreach (var st in new List<AnimationState>(EnumStates(anim))) anim.RemoveClip(st.clip);
                // every model shares the same clips (same 65 Mixamo bones): Rafi's idle / walk / run, the cook's sit
                var info = go.GetComponent<CrowdRigInfo>();
                if (info == null) info = go.AddComponent<CrowdRigInfo>();
                var names = new List<string>(); var shifts = new List<Vector3>();
                var ownHips = HipsRest(model);
                foreach (var kv in ClipSource)
                {
                    var src = $"{SrcDir}/{kv.Value}/{kv.Value}.fbx";
                    var clip = FindClip(src, kv.Key);
                    if (clip == null) continue;
                    clip.wrapMode = WrapMode.Loop;
                    anim.AddClip(clip, kv.Key);
                    var srcModel = AssetDatabase.LoadAssetAtPath<GameObject>(src);
                    names.Add(kv.Key);
                    shifts.Add(ownHips.HasValue && srcModel ? ownHips.Value - (HipsRest(srcModel) ?? ownHips.Value) : Vector3.zero);
                }
                info.clips = names.ToArray();
                info.hipShift = shifts.ToArray();
                if (anim.GetClip("idle") != null) anim.clip = anim.GetClip("idle");
                PrefabUtility.SaveAsPrefabAsset(go, $"{OutDir}/{name}.prefab");
                Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
