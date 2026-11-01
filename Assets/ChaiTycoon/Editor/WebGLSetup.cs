using CrazyGames;
using UnityEditor;
using UnityEngine;

namespace ChaiTycoon.EditorTools
{
    /// <summary>
    /// WebGL player settings for CrazyGames, applied whenever the editor loads (so they can't drift); see the README:
    ///  * the SDK's template (Assets/WebGLTemplates/CrazyGames; on crazygames.com the site runs the loader itself and
    ///    sizes the canvas to the frame),
    ///  * no Unity splash and a background colour matching the loading screen.
    /// </summary>
    [InitializeOnLoad]
    public static class WebGLSetup
    {
        const string Template = "PROJECT:CrazyGames";
        const string IconPath = "Assets/ChaiTycoon/Icons/icon512.png";

        static WebGLSetup()
        {
            EditorApplication.delayCall += Apply;
        }

        [MenuItem("Chai/Web/Apply CrazyGames Settings")]
        public static void Apply()
        {
            bool changed = false;
            if (PlayerSettings.WebGL.template != Template) { PlayerSettings.WebGL.template = Template; changed = true; }
            if (PlayerSettings.SplashScreen.show) { PlayerSettings.SplashScreen.show = false; changed = true; }
            if (PlayerSettings.SplashScreen.showUnityLogo) { PlayerSettings.SplashScreen.showUnityLogo = false; changed = true; }
            var bg = new Color(0xF6 / 255f, 0xD2 / 255f, 0xB4 / 255f, 1f);
            if (PlayerSettings.SplashScreen.backgroundColor != bg) { PlayerSettings.SplashScreen.backgroundColor = bg; changed = true; }

            // CrazyGames' publishing settings (docs.crazygames.com/resources/optimization-tips): Brotli (it only takes
            // compressed builds), hashed file names, explicitly thrown exceptions only, data caching, engine code
            // stripped, managed stripping High (drop to Medium if something breaks in the build), IL2CPP optimised for
            // size, and the WebAssembly features the SDK's release build turns on. That build also sets Disk Size LTO.
            if (PlayerSettings.WebGL.compressionFormat != WebGLCompressionFormat.Brotli) { PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli; changed = true; }
            if (!PlayerSettings.WebGL.nameFilesAsHashes) { PlayerSettings.WebGL.nameFilesAsHashes = true; changed = true; }
            if (PlayerSettings.WebGL.exceptionSupport != WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly) { PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly; changed = true; }
            if (!PlayerSettings.WebGL.dataCaching) { PlayerSettings.WebGL.dataCaching = true; changed = true; }
            if (!PlayerSettings.WebGL.webAssemblyBigInt) { PlayerSettings.WebGL.webAssemblyBigInt = true; changed = true; }
            if (!PlayerSettings.WebGL.webAssemblyTable) { PlayerSettings.WebGL.webAssemblyTable = true; changed = true; }
            if (!PlayerSettings.WebGL.wasm2023) { PlayerSettings.WebGL.wasm2023 = true; changed = true; }
            if (!PlayerSettings.stripEngineCode) { PlayerSettings.stripEngineCode = true; changed = true; }
            var web = UnityEditor.Build.NamedBuildTarget.WebGL;
            if (PlayerSettings.GetManagedStrippingLevel(web) != ManagedStrippingLevel.High) { PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.High); changed = true; }
            if (PlayerSettings.GetIl2CppCodeGeneration(web) != UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize) { PlayerSettings.SetIl2CppCodeGeneration(web, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize); changed = true; }

            // the store icon (512 px, Art/Store/icon512.png) as the game's default icon
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
                Debug.Log("[Chai] WebGL settings: CrazyGames template " + PlayerSettings.WebGL.template);
            }
        }

        /// <summary>
        /// Applies the settings and runs the SDK's release build into Builds/CrazyGamesRelease (the main build plus its
        /// ASTC data file), then opens it on localhost. The game ships for desktop browsers, so the low-memory phone
        /// builds are off. Upload every file in that folder to the developer portal. Also from the command line:
        ///   Unity.exe -batchmode -quit -projectPath D:\ChaiTycoon -executeMethod ChaiTycoon.EditorTools.WebGLSetup.BuildForCrazyGames
        /// </summary>
        [MenuItem("Chai/Web/Build for CrazyGames")]
        public static void BuildForCrazyGames()
        {
            Apply();
            new Builder().DoReleaseBuild(new AdditionalBuildOptions { supportsMobile = false });
        }
    }
}
