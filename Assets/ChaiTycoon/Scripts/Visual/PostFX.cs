using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChaiTycoon
{
    /// <summary>
    /// The colour grade of the reference picture: a warm, slightly saturated golden-afternoon look with a soft
    /// highlight bloom and a light vignette. One global volume, built in code like the rest of the scene.
    /// </summary>
    public static class PostFX
    {
        static Volume volume;

        public static void Ensure()
        {
            if (volume) return;
            var go = new GameObject("PostFX");
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = p;

            var tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);           // keeps the textures' own colours

            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.35f);
            ca.contrast.Override(10f);
            ca.saturation.Override(4f);
            ca.colorFilter.Override(new Color(1f, 0.985f, 0.96f));

            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(5f);

            var lgg = p.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(1f, 0.99f, 0.97f, 0.03f));     // warm, slightly lifted shadows

            var bloom = p.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.6f);

            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.2f);
            vig.smoothness.Override(0.5f);
        }
    }
}
