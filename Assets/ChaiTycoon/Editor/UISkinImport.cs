using UnityEditor;
using UnityEngine;

namespace ChaiTycoon.EditorTools
{
    /// <summary>
    /// Import settings for the UI pieces Art/UI/copy_ui_skin.ps1 writes to Resources/UISkin. They load as plain
    /// textures and UI/Skin.cs makes the (9-sliced) sprites at runtime. No mipmaps (screen UI), clamped, and
    /// crunch-compressed to keep the WebGL build small (the script pads every image to a multiple of 4 px so
    /// it can be block-compressed).
    /// </summary>
    public class UISkinImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/UISkin/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.crunchedCompression = true;
            ti.compressionQuality = 85;
        }
    }
}
