using UnityEditor;

namespace ChaiTycoon.EditorTools
{
    /// <summary>
    /// Enter play mode without reloading every script (the 7–9 s grey Game view after pressing Play was the
    /// editor's domain reload, which never happens in a web build). Statics that must start clean have a
    /// SubsystemRegistration reset (Game, WebBridge, InputReader, Panel, Skin, ArtLib, ...). To go back:
    /// Project Settings › Editor › Enter Play Mode Settings › tick Reload Domain, and remove this file.
    /// </summary>
    [InitializeOnLoad]
    public static class FastPlayMode
    {
        static FastPlayMode()
        {
            EditorApplication.delayCall += () =>
            {
                const EnterPlayModeOptions want = EnterPlayModeOptions.DisableDomainReload;
                if (EditorSettings.enterPlayModeOptionsEnabled && EditorSettings.enterPlayModeOptions == want) return;
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions = want;
                UnityEngine.Debug.Log("[Chai] Play mode now starts without a domain reload");
            };
        }
    }
}
