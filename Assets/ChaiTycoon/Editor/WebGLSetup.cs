using UnityEditor;
using UnityEngine;

namespace ChaiTycoon.EditorTools
{
    /// <summary>
    /// WebGL player settings for Poki, applied whenever the editor loads (so they can't drift); see the README:
    ///  * Poki's template (Assets/WebGLTemplates/poki-template; on poki.com the page is generated from its
    ///    index.json, and Poki's loader sizes the canvas to the frame, 16:9 down to 9:16),
    ///  * no Unity splash and a background colour matching the loading screen.
    /// </summary>
    [InitializeOnLoad]
    public static class WebGLSetup
    {
        const string Template = "PROJECT:poki-template";
        const string IconPath = "Assets/ChaiTycoon/Icons/icon512.png";

        static WebGLSetup()
        {
            EditorApplication.delayCall += Apply;
        }

        [MenuItem("Chai/Web/Apply Poki Settings")]
        public static void Apply()
        {
            bool changed = false;
            if (PlayerSettings.WebGL.template != Template) { PlayerSettings.WebGL.template = Template; changed = true; }
            if (PlayerSettings.SplashScreen.show && Application.HasProLicense()) { PlayerSettings.SplashScreen.show = false; changed = true; }
            var bg = new Color(0xF6 / 255f, 0xD2 / 255f, 0xB4 / 255f, 1f);
            if (PlayerSettings.SplashScreen.backgroundColor != bg) { PlayerSettings.SplashScreen.backgroundColor = bg; changed = true; }

            // Poki's publishing settings (developers.poki.com/guide/sdk-unity): no compression (their servers
            // compress), hashed file names, explicitly thrown exceptions only, data caching, engine code stripped,
            // managed stripping High (drop to Medium if something breaks in the build)
            if (PlayerSettings.WebGL.compressionFormat != WebGLCompressionFormat.Disabled) { PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; changed = true; }
            if (!PlayerSettings.WebGL.nameFilesAsHashes) { PlayerSettings.WebGL.nameFilesAsHashes = true; changed = true; }
            if (PlayerSettings.WebGL.exceptionSupport != WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly) { PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly; changed = true; }
            if (!PlayerSettings.WebGL.dataCaching) { PlayerSettings.WebGL.dataCaching = true; changed = true; }
            if (!PlayerSettings.stripEngineCode) { PlayerSettings.stripEngineCode = true; changed = true; }
            var web = UnityEditor.Build.NamedBuildTarget.WebGL;
            if (PlayerSettings.GetManagedStrippingLevel(web) != ManagedStrippingLevel.High) { PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.High); changed = true; }

            // the store icon (512 px, Art/Store/icon512.png) as the game's default icon; the store screenshots and
            // the 100 px thumbnail live in the template (screenshots/N.jpg, N-small.jpg, thumbnail.jpg)
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
                var cur = PlayerSettings.GetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, IconKind.Any);
                if (cur == null || cur.Length == 0 || cur[0] != icon)
                {
                    PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
                    changed = true;
                }
            }
            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[Chai] WebGL settings: Poki template " + PlayerSettings.WebGL.template);
            }
        }

        const string BuildDir = "Builds/Poki";

        /// <summary>
        /// Applies the Poki settings, builds WebGL into Builds/Poki (replacing the last build), zips it next to it as
        /// Builds/ChaiTycoon_Poki_&lt;date&gt;.zip for the Poki upload, and opens the folder. Also from the command line:
        ///   Unity.exe -batchmode -quit -projectPath D:\ChaiTycoon -executeMethod ChaiTycoon.EditorTools.WebGLSetup.BuildForPoki
        /// </summary>
        [MenuItem("Chai/Web/Build for Poki")]
        public static void BuildForPoki()
        {
            Apply();
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            if (scenes.Count == 0) scenes.Add("Assets/ChaiTycoon/Scenes/Main.unity");

            if (System.IO.Directory.Exists(BuildDir)) System.IO.Directory.Delete(BuildDir, true);
            var opts = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = BuildDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var sum = report.summary;
            if (sum.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError($"[Chai] Poki build {sum.result}: {sum.totalErrors} error(s). See the console above.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            // Poki takes the build folder as one zip (index.html at its root)
            string zip = $"Builds/ChaiTycoon_Poki_{System.DateTime.Now:yyyyMMdd_HHmm}.zip";
            if (System.IO.File.Exists(zip)) System.IO.File.Delete(zip);
            System.IO.Compression.ZipFile.CreateFromDirectory(BuildDir, zip);
            long mb = new System.IO.FileInfo(zip).Length / (1024 * 1024);
            Debug.Log($"[Chai] Poki build done in {sum.totalTime.TotalSeconds:0} s: {BuildDir} ({sum.totalSize / (1024 * 1024)} MB), upload {zip} ({mb} MB)");
            if (!Application.isBatchMode) EditorUtility.RevealInFinder(zip);
        }
    }
}
