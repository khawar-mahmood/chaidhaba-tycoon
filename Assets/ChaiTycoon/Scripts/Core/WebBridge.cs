using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Browser bridge: localStorage (try/catch in JS), phone detection, tab visibility, and the Poki SDK through
    /// Poki's own Unity wrapper (Scripts/Poki/PokiUnitySDK.cs + Plugins/WebGL/PokiSDKBridge.jslib):
    ///  * init at startup, gameLoadingStart / gameLoadingFinished around the boot,
    ///  * gameplayStart / gameplayStop whenever play starts and stops (panels, pause, hidden tab, ads),
    ///  * commercialBreak at natural breaks, rewardedBreak only when the player asks for a reward,
    ///  * game audio muted and the world paused while an ad plays (AdStateChanged).
    /// In the editor and on non-web platforms ads are stubbed (an instant break, a rewarded ad that succeeds).
    /// </summary>
    public class WebBridge : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern string Chai_StorageGet(string key);
        [DllImport("__Internal")] static extern int Chai_StorageSet(string key, string value);
        [DllImport("__Internal")] static extern void Chai_Init(string goName);
        [DllImport("__Internal")] static extern int Chai_IsMobile();
#endif

        static WebBridge inst;
        static Action<bool> rewardCb;
        static Action breakCb;
        static bool gameplayRunning, loadingFinished;
        public static bool AdPlaying { get; private set; }
        public static event Action<bool> AdStateChanged;
        /// <summary>The Poki SDK reported ready (or an ad blocker: then breaks finish at once and rewards fail).</summary>
        public static bool SdkReady { get; private set; }

        // the editor enters play mode without reloading scripts: no ad or gameplay state carries over
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            inst = null; rewardCb = null; breakCb = null;
            gameplayRunning = loadingFinished = false;
            AdPlaying = false; SdkReady = false; AdStateChanged = null;
        }

        public static void Init()
        {
            if (inst) return;
            var go = new GameObject("WebBridge");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<WebBridge>();
#if UNITY_WEBGL && !UNITY_EDITOR
            try { Chai_Init(go.name); } catch (Exception e) { Debug.LogWarning(e); }
#endif
            try
            {
                var sdk = PokiUnitySDK.Instance;
                sdk.sdkInitializedCallback = () => { SdkReady = true; Debug.Log("[Poki] ready" + (sdk.adblocked ? " (ad blocker)" : "")); };
                sdk.commercialBreakCallBack = () => inst.OnCommercialBreakDone("");
                sdk.rewardedBreakCallBack = ok => inst.OnRewardedDone(ok ? "1" : "0");
                sdk.init();
                sdk.gameLoadingStart();
            }
            catch (Exception e) { Debug.LogWarning("[Poki] init failed: " + e.Message); }
        }

        static PokiUnitySDK Sdk => PokiUnitySDK.Instance;

        // ---------- storage ----------
        public static string StorageGet(string key)
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return Chai_StorageGet(key);
#else
                return PlayerPrefs.GetString(key, null);
#endif
            }
            catch (Exception) { return null; }
        }

        public static bool StorageSet(string key, string value)
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return Chai_StorageSet(key, value) == 1;
#else
                PlayerPrefs.SetString(key, value);
                PlayerPrefs.Save();
                return true;
#endif
            }
            catch (Exception) { return false; }
        }

        public static bool IsMobile
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                try { return Chai_IsMobile() == 1; } catch { return false; }
#else
                return Application.isMobilePlatform;
#endif
            }
        }

        // ---------- Poki ----------
        public static void GameLoadingFinished()
        {
            if (loadingFinished) return;
            loadingFinished = true;
            try { Sdk.gameLoadingFinished(); } catch { }
            Debug.Log("[Poki] gameLoadingFinished");
        }

        public static void GameplayStart()
        {
            if (gameplayRunning || AdPlaying) return;
            gameplayRunning = true;
            try { Sdk.gameplayStart(); } catch { }
        }

        public static void GameplayStop()
        {
            if (!gameplayRunning) return;
            gameplayRunning = false;
            try { Sdk.gameplayStop(); } catch { }
        }

        /// <summary>Progress events for Poki's analytics, e.g. ("level", "3", "complete").</summary>
        public static void Measure(string category, string what, string action)
        {
            try { Sdk.measure(category, what, action); } catch { }
        }

        /// <summary>Signals a natural pause; Poki decides whether an ad actually plays.</summary>
        public static void CommercialBreak(Action done)
        {
            GameplayStop();
            breakCb = done;
#if UNITY_WEBGL && !UNITY_EDITOR
            SetAd(true);
            try { Sdk.commercialBreak(); } catch { inst.OnCommercialBreakDone(""); }
#else
            inst.OnCommercialBreakDone("");
#endif
        }

        /// <summary>Plays a rewarded ad. The reward is only given when the SDK reports success.</summary>
        public static void RewardedBreak(string size, Action<bool> done)
        {
            GameplayStop();
            rewardCb = done;
            SetAd(true);
#if UNITY_WEBGL && !UNITY_EDITOR
            try { Sdk.rewardedBreak(size); } catch { inst.OnRewardedDone("0"); }
#else
            // Editor: pretend a short ad played successfully.
            inst.Invoke(nameof(EditorRewardDone), 0.6f);
#endif
        }

        void EditorRewardDone() => OnRewardedDone("1");

        static void SetAd(bool on)
        {
            AdPlaying = on;
            AdStateChanged?.Invoke(on);
        }

        public void OnCommercialBreakDone(string _)
        {
            SetAd(false);
            var cb = breakCb; breakCb = null;
            cb?.Invoke();
        }

        // called from JS via SendMessage
        public void OnVisibility(string hiddenFlag)
        {
            if (Game.I != null) Game.I.SetHidden(hiddenFlag == "1");
        }

        public void OnRewardedDone(string ok)
        {
            SetAd(false);
            var cb = rewardCb; rewardCb = null;
            cb?.Invoke(ok == "1");
        }
    }
}
