using UnityEditor;
using UnityEngine;

namespace ChaiTycoon.EditorTools
{
    /// <summary>
    /// Import settings for the textures export_art.py writes to Resources/Art/Tex:
    ///  * &lt;Kit&gt;_Base: the kit's colour map (sRGB, at most 1024 px)
    ///  * &lt;Kit&gt;_Normal: the kit's normal map (at most 256 px: fine detail only, and the smallest download)
    ///  * &lt;Kit&gt;_MetalSmooth: metallic in RGB, smoothness in alpha (linear, at most 1024 px)
    ///  * Palette: one texel per plain colour, so it must stay sharp and exact (point filter, no mipmaps, uncompressed)
    /// The large maps are crunch-compressed to keep the WebGL build small.
    /// </summary>
    public class ArtTexImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Resources/Art/Tex/")) return;
            var ti = (TextureImporter)assetImporter;
            var file = System.IO.Path.GetFileNameWithoutExtension(path);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.anisoLevel = 4;                                   // sharp at the game camera's slant
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.crunchedCompression = true;
            ti.compressionQuality = 90;
            // download budget for CrazyGames (50 MB initial download, 20 MB for the mobile homepage): colour and metal/smoothness maps at most 1024 px, normal maps 256 px
            ti.maxTextureSize = 1024;
            if (file == "Palette")
            {
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.crunchedCompression = false;
                ti.maxTextureSize = 64;
            }
            if (file.EndsWith("_Normal")) ti.maxTextureSize = 256;
            if (file.StartsWith("Ground")) ti.wrapMode = TextureWrapMode.Repeat;     // tiles across the whole ground
            if (file.EndsWith("_Normal")) ti.textureType = TextureImporterType.NormalMap;
            else if (file.EndsWith("_MetalSmooth"))
            {
                ti.sRGBTexture = false;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
            }
        }
    }
}
