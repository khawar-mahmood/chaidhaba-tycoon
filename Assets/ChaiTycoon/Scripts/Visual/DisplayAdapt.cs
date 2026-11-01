using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChaiTycoon
{
    /// <summary>
    /// Dynamic resolution for the web: CrazyGames resizes the game frame at any time (window resizes, fullscreen,
    /// phones turning), and high-DPI screens multiply the pixels. The 3D view renders at a scale that keeps
    /// it near a pixel budget (1080p on desktop, 720p on phones), and drops in 10% steps when frames run
    /// slow, coming back up when there's headroom. The UI canvas always draws at full resolution, so text stays
    /// sharp. Camera framing for odd aspects is in CameraRig.
    /// </summary>
    public class DisplayAdapt : MonoBehaviour
    {
        static DisplayAdapt inst;
        int w, h;
        float cap = 1f, scale = 1f, avg = 1 / 60f, slow, fast;

        public static float RenderScale => inst ? inst.scale : 1f;

        public static void Ensure()
        {
            if (inst) return;
            inst = new GameObject("DisplayAdapt").AddComponent<DisplayAdapt>();
        }

        void Update()
        {
            if (Screen.width != w || Screen.height != h)
            {
                w = Screen.width; h = Screen.height;
                float budget = WebBridge.IsMobile || Application.isMobilePlatform ? 1280f * 720f : 1920f * 1080f;
                cap = Mathf.Clamp(Mathf.Sqrt(budget / Mathf.Max(1f, (float)w * h)), 0.5f, 1f);
                scale = Mathf.Min(scale, cap);
                if (scale <= 0) scale = cap;
                Apply();
            }
            if (Application.isEditor) return;     // the editor's frame rate says nothing about the player's machine

            // adapt to the frame rate (skip hitches and paused time)
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0 || dt > 0.25f || Game.I == null || !Game.I.Booted) return;
            avg = Mathf.Lerp(avg, dt, 0.05f);
            slow = avg > 1f / 40f ? slow + dt : 0;
            fast = avg < 1f / 56f ? fast + dt : 0;
            if (slow > 2f && scale > 0.55f) { scale = Mathf.Max(0.5f, scale - 0.1f); slow = 0; Apply(); }
            else if (fast > 8f && scale < cap) { scale = Mathf.Min(cap, scale + 0.1f); fast = 0; Apply(); }
        }

        void Apply()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = scale;
        }
    }
}
