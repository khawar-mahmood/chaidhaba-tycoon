using System;
using System.Runtime.InteropServices;
using CrazyGames;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Browser bridge: storage, phone detection, tab visibility, and the CrazyGames SDK (Assets/CrazySDK):
    ///  * Init at startup; Game.Start waits for it, because the save comes through the SDK's Data module,
    ///  * saves in CrazySDK.Data (synced for logged-in players), localStorage only if the SDK never came up,
    ///  * GameplayStart / GameplayStop whenever play starts and stops (panels, pause, ads). Not for a hidden tab:
    ///    the site handles focus itself,
    ///  * midgame ads at natural breaks, rewarded ads only when the player asks for a reward,
    ///  * game audio muted and the world paused while an ad plays (AdStateChanged),
    ///  * RewardsAvailable goes false when ads can't play (ad blocker, or ads off during Basic Launch, both known
    ///    at startup): every rewarded button hides then, and nothing the player needs may depend on an ad.
    /// In the editor ads are stubbed (an instant break, a rewarded ad that succeeds).
    /// </summary>
    public class WebBridge : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern string Chai_StorageGet(string key);
        [DllImport("__Internal")] static extern int Chai_StorageSet(string key, string value);
        [DllImport("__Internal")] static extern void Chai_Init(string goName);
        [DllImport("__Internal")] static extern int Chai_IsMobile();
        [DllImport("__Internal")] static extern int Chai_AdsDisabled();
#endif

        static WebBridge inst;
        static Action<bool> rewardCb;
        static Action breakCb;
        static bool gameplayRunning, adsOff;
        /// <summary>This session saves to localStorage, not the Data module (see StopWaitingForSdk).</summary>
        public static bool LocalSaves { get; private set; }
        public static bool AdPlaying { get; private set; }
        public static event Action<bool> AdStateChanged;
        /// <summary>The SDK finished its init; its modules can be used.</summary>
        public static bool SdkReady { get; private set; }
        /// <summary>Init was asked for and hasn't answered yet.</summary>
        public static bool SdkPending { get; private set; }
        /// <summary>A rewarded ad can play. False hides the rewarded buttons.</summary>
        public static bool RewardsAvailable => SdkReady && !adsOff;
        /// <summary>An ad blocker is on. CrazyGames wants a notice when that turns features off.</summary>
        public static bool AdBlocked { get; private set; }
        /// <summary>Running in the CrazyGames phone app, where notches and rounded corners can cover the screen edges.</summary>
        public static bool InApp
        {
            get
            {
                if (!SdkReady) return false;
                try
                {
                    string app = CrazySDK.User.SystemInfo.applicationType;
                    return app == "google_play_store" || app == "apple_store";
                }
                catch { return false; }
            }
        }

        // the editor enters play mode without reloading scripts: no ad or gameplay state carries over
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            inst = null; rewardCb = null; breakCb = null;
            gameplayRunning = adsOff = false;
            AdPlaying = LocalSaves = AdBlocked = false; SdkReady = SdkPending = false; AdStateChanged = null;
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
                SdkPending = true;
                CrazySDK.Init(OnSdkReady);
            }
            catch (Exception e) { SdkPending = false; Debug.LogWarning("[CrazyGames] init failed: " + e.Message); }
        }

        static void OnSdkReady()
        {
            SdkPending = false;
            SdkReady = true;
            Debug.Log("[CrazyGames] ready: " + CrazySDK.Environment);
#if UNITY_WEBGL && !UNITY_EDITOR
            try { if (Chai_AdsDisabled() == 1) { adsOff = true; Debug.Log("[CrazyGames] ads are off (Basic Launch)"); } } catch { }
#endif
            CrazySDK.Ad.HasAdblock(blocked => { if (blocked) { adsOff = true; AdBlocked = true; } });
        }

        /// <summary>
        /// The SDK didn't answer in time: play on with localStorage. That lasts the whole session, even if the SDK
        /// comes up later: this run started without the player's real save, so it must never write over it.
        /// </summary>
        public static void StopWaitingForSdk()
        {
            if (!SdkPending) return;
            SdkPending = false;
            LocalSaves = true;
            Debug.LogWarning("[CrazyGames] no answer from the SDK, saving locally for this session");
        }

        // ---------- storage ----------
        public static string StorageGet(string key)
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return SdkReady && !LocalSaves ? CrazySDK.Data.GetString(key, "") : Chai_StorageGet(key);
#else
                return PlayerPrefs.GetString(key, null);
#endif
            }
            catch (Exception e) { Debug.LogWarning("[Save] storage read failed: " + e.Message); return null; }
        }

        public static bool StorageSet(string key, string value)
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (!SdkReady || LocalSaves) return Chai_StorageSet(key, value) == 1;
                if (string.IsNullOrEmpty(value)) CrazySDK.Data.DeleteKey(key);
                else CrazySDK.Data.SetString(key, value);
                return true;
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

        // ---------- CrazyGames ----------
        public static void GameplayStart()
        {
            if (gameplayRunning || AdPlaying || !SdkReady) return;
            gameplayRunning = true;
            try { CrazySDK.Game.GameplayStart(); } catch { }
        }

        public static void GameplayStop()
        {
            if (!gameplayRunning) return;
            gameplayRunning = false;
            try { CrazySDK.Game.GameplayStop(); } catch { }
        }

        /// <summary>The site's celebration, for big moments only (finishing a stage, not every level).</summary>
        public static void HappyTime()
        {
            if (!SdkReady) return;
            try { CrazySDK.Game.HappyTime(); } catch { }
        }

        /// <summary>Signals a natural pause; the SDK decides whether an ad actually plays (one every 3 minutes at most).</summary>
        public static void CommercialBreak(Action done)
        {
            if (AdPlaying || !SdkReady) { done?.Invoke(); return; }
            GameplayStop();
            breakCb = done;
#if UNITY_WEBGL && !UNITY_EDITOR
            SetAd(true);
            try { CrazySDK.Ad.RequestAd(CrazyAdType.Midgame, null, e => { NoteAdError(e); BreakDone(); }, BreakDone); }
            catch { BreakDone(); }
#else
            BreakDone();
#endif
        }

        /// <summary>Plays a rewarded ad. The reward is only given when the SDK reports success.</summary>
        public static void RewardedBreak(Action<bool> done)
        {
            // RequestAd drops a second request without calling back, which would leave the game paused for good
            if (AdPlaying) { done?.Invoke(false); return; }
            GameplayStop();
            rewardCb = done;
            SetAd(true);
#if UNITY_WEBGL && !UNITY_EDITOR
            try { CrazySDK.Ad.RequestAd(CrazyAdType.Rewarded, null, RewardFailed, () => RewardDone(true)); }
            catch { RewardFailed(null); }
#else
            // Editor: pretend a short ad played successfully (in real time, because the ad itself pauses the world).
            inst.StartCoroutine(EditorReward());
#endif
        }

        static System.Collections.IEnumerator EditorReward()
        {
            yield return new WaitForSecondsRealtime(0.6f);
            RewardDone(true);
        }

        static void SetAd(bool on)
        {
            AdPlaying = on;
            AdStateChanged?.Invoke(on);
        }

        /// <summary>
        /// adsDisabled... and adblock last the session; unfilled and adCooldown pass. The docs say adsDisabled, while
        /// SDK 3.8.0 still sends adsDisabledBasicLaunch, so this matches both.
        /// </summary>
        static void NoteAdError(SdkError e)
        {
            if (e == null || e.code == null) return;
            Debug.Log("[CrazyGames] ad error: " + e.code);
            if (e.code.StartsWith("adsDisabled")) adsOff = true;
            if (e.code == "adblock") adsOff = AdBlocked = true;
        }

        static void RewardFailed(SdkError e)
        {
            NoteAdError(e);
            if (Game.I != null && Game.I.Hud != null)
                Game.I.Hud.Toast(adsOff ? "Ads aren't available right now" : "No ad right now. Try again in a moment", "tv");
            RewardDone(false);
        }

        static void BreakDone()
        {
            SetAd(false);
            var cb = breakCb; breakCb = null;
            cb?.Invoke();
        }

        static void RewardDone(bool ok)
        {
            SetAd(false);
            var cb = rewardCb; rewardCb = null;
            cb?.Invoke(ok);
        }

        // called from JS via SendMessage
        public void OnVisibility(string hiddenFlag)
        {
            if (Game.I != null) Game.I.SetHidden(hiddenFlag == "1");
        }
    }
}
