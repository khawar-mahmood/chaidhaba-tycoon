using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChaiTycoon.EditorTools
{
    /// <summary>
    /// Makes the standard URP Lit materials the Blender props use, in Resources/Art/Mats, from the textures
    /// export_art.py writes to Resources/Art/Tex: one material per texture set (&lt;Name&gt;_Base, plus _Normal and
    /// _MetalSmooth when there are), and Palette for the plain-coloured props. Runs after those textures import,
    /// from Chai › Art › Rebuild Materials, and at the start of the automated check.
    /// </summary>
    public class ArtMaterials : AssetPostprocessor
    {
        const string TexDir = "Assets/ChaiTycoon/Resources/Art/Tex";
        const string MatDir = "Assets/ChaiTycoon/Resources/Art/Mats";

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var p in imported)
                if (p.Replace('\\', '/').StartsWith(TexDir)) { EditorApplication.delayCall -= Ensure; EditorApplication.delayCall += Ensure; return; }
        }

        [MenuItem("Chai/Art/Rebuild Materials")]
        public static void Ensure()
        {
            if (!Directory.Exists(TexDir)) return;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (!lit) { Debug.LogError("ArtMaterials: URP Lit shader not found"); return; }
            Directory.CreateDirectory(MatDir);
            foreach (var file in Directory.GetFiles(TexDir))
            {
                if (file.EndsWith(".meta")) continue;
                var name = Path.GetFileNameWithoutExtension(file);
                if (name == "Palette") Make("Palette", Load(file), null, null, lit);
                else if (name.EndsWith("_Base"))
                {
                    var set = name.Substring(0, name.Length - 5);
                    Make(set, Load(file), Find(set + "_Normal"), Find(set + "_MetalSmooth"), lit);
                }
            }
            AssetDatabase.SaveAssets();
        }

        static Texture2D Load(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\', '/'));

        static Texture2D Find(string name)
        {
            foreach (var file in Directory.GetFiles(TexDir, name + ".*"))
                if (!file.EndsWith(".meta")) return Load(file);
            return null;
        }

        static void Make(string name, Texture2D baseMap, Texture2D normal, Texture2D metal, Shader lit)
        {
            if (!baseMap) return;
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = m == null;
            if (created) m = new Material(lit);
            m.shader = lit;
            m.SetTexture("_BaseMap", baseMap);
            m.SetColor("_BaseColor", Color.white);
            m.mainTexture = baseMap;
            m.SetFloat("_WorkflowMode", 1);                     // metallic
            m.SetFloat("_SmoothnessTextureChannel", 0);         // smoothness in the metallic map's alpha
            m.SetFloat("_EnvironmentReflections", 1);
            m.SetFloat("_SpecularHighlights", 1);
            if (normal)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 1);
                m.EnableKeyword("_NORMALMAP");
            }
            else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
            if (metal)
            {
                m.SetTexture("_MetallicGlossMap", metal);
                m.SetFloat("_Smoothness", 1);                   // scales the map's smoothness
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else
            {
                m.SetTexture("_MetallicGlossMap", null);
                m.DisableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Metallic", 0);
                m.SetFloat("_Smoothness", 0.2f);                // Blender's plain materials are fairly rough
            }
            if (created) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
        }
    }
}
