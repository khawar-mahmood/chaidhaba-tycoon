using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChaiTycoon
{
    /// <summary>
    /// Owns the run: config, save, wallet, pause state and every system. The whole game is built
    /// from code on load, straight into play (no title screen, per the GDD's onboarding rules).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class Game : MonoBehaviour
    {
        public static Game I;

        public Economy Eco;
        public SaveData S;
        public LocationCfg LocCfg;
        public LocSave LS;
        public Upgrades Up;

        public Location Loc;
        public Player Player;
        public CameraRig Cam;
        public Hud Hud;
        public WorldUI WUI;
        public FX Fx;
        public Progression Progression;
        public Offers Offers;
        public Guide Guide;
        public CustomerSpawner Spawner;
        public Light Sun;

        public float Multiplier => LocCfg != null ? LocCfg.multiplier : 1f;
        public bool Started { get; private set; }
        public bool WorldPaused => pauseRequests > 0 || hidden || WebBridge.AdPlaying;
        public string DebugPauseState => $"requests={pauseRequests} hidden={hidden} ad={WebBridge.AdPlaying} panels={Panel.OpenCount}";
        public float BoostLeft;
        /// <summary>Debug / test time acceleration (1 in normal play).</summary>
        public static float SpeedMultiplier = 1f;
        public double IncomeRate; // smoothed cash per second
        public float SessionTime; // unscaled seconds this session, for pacing offers

        int pauseRequests;
        bool hidden;
        double incomeAcc;
        float incomeTimer, saveTimer;
        bool saveQueued;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; SpeedMultiplier = 1f; }

        /// <summary>Everything is built and the game is running (false while the loading screen is up).</summary>
        public bool Booted { get; private set; }
        LoadingScreen loading;
        double awaySeconds;
        const float SdkWaitSeconds = 10f;

        void Awake()
        {
            I = this;
            GameEvents.ResetAll();
            Zones.All.Clear();
            Application.targetFrameRate = 60;
            Time.timeScale = 1;
            WebBridge.Init();
            WebBridge.AdStateChanged += OnAdState;

            Eco = Economy.Load();
            SetupRendering();
            SetupEventSystem();
            DisplayAdapt.Ensure();
            // the first frame shows the loading screen instead of a blank grey view while the rest builds
            loading = LoadingScreen.Show();
        }

        /// <summary>
        /// Boots over a few frames behind the loading screen: the SDK and the save, audio, the art pack, then the
        /// dhaba itself (UI, world and systems in one frame, since they reference each other). Each step's time is logged.
        /// </summary>
        System.Collections.IEnumerator Start()
        {
            var total = System.Diagnostics.Stopwatch.StartNew();
            var sw = new System.Diagnostics.Stopwatch();
            void Step(string name, float progress, Action a)
            {
                sw.Restart();
                a();
                Debug.Log($"[Boot] {name}: {sw.ElapsedMilliseconds} ms");
                loading.Progress(progress);
            }

            yield return null;          // let the loading screen draw
            // the save lives in the SDK's Data module, which only works once the SDK's async init is done
            for (float waited = 0; WebBridge.SdkPending && waited < SdkWaitSeconds; waited += Time.unscaledDeltaTime) yield return null;
            WebBridge.StopWaitingForSdk();
            Step("save", 0.1f, LoadSave);
            yield return null;
            Step("audio", 0.25f, () =>
            {
                Synth.Create();
                Synth.I.SfxOn = S.sfx;
                Synth.I.MusicOn = S.music;
            });
            yield return null;
            Step("art pack", 0.45f, () => ArtLib.Load("art_core"));
            yield return null;
            Step("dhaba", 0.9f, () =>
            {
                Fx = new GameObject("FX").AddComponent<FX>();
                WUI = WorldUI.Create();
                Hud = Hud.Create();
                Cam = CameraRig.Create();

                EnterLocation(S.location, true);

                Progression = new Progression(this);
                Loc.RefreshTiles(false); // tiles the saved level is asking for
                Loc.RefreshGrowth(Progression.DisplayLevel, false);   // the decor earned by the levels so far
                Offers = gameObject.AddComponent<Offers>();
                Guide = gameObject.AddComponent<Guide>();
                Hud.Refresh();
            });
            yield return null;          // the first real frame (shaders, shadows) renders under the loading screen
            yield return null;
            Booted = true;
            loading.Hide();
            Debug.Log($"[Boot] ready in {total.ElapsedMilliseconds} ms");
            Hud.ShowLoadingDone();
            double offline = OfflinePanel.Earnings(awaySeconds);
            if (offline >= 5) Hud.Offline.Show(offline, awaySeconds);
            ApplyPause();               // the first GameplayStart: CrazyGames counts the initial download up to here
        }

        void LoadSave()
        {
            S = SaveSystem.Load();
            // time since the last save (before the first new save overwrites it), for the offline earnings
            awaySeconds = S.lastSeen > 0 ? Math.Max(0, (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - S.lastSeen) / 1000.0) : 0;
            Up = new Upgrades(this);
        }

        // ------------------------------------------------------------------ setup

        void SetupRendering()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            // the reference look: real sun shadows, ambient occlusion (renderer feature) and warm colour grading
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.antialiasing = AntialiasingMode.None;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 120f;
            cam.cullingMask &= ~(1 << 31);

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.msaaSampleCount = 4;
                urp.renderScale = 1f;
                urp.supportsHDR = true;
                urp.shadowDistance = 45f;
            }

            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { Sun = l; break; }
            if (Sun == null)
            {
                Sun = new GameObject("Sun").AddComponent<Light>();
                Sun.type = LightType.Directional;
            }
            Sun.shadows = LightShadows.Soft;
            // late-afternoon sun from the back left: shadows fall towards the bottom right of the screen
            Sun.transform.rotation = Quaternion.Euler(42, 132, 0);
            RenderSettings.sun = Sun;
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            PostFX.Ensure();
        }

        public void ApplyMood(int location) => ApplyMood(location, Sun, Camera.main);

        public static void ApplyMood(int location, Light sun, Camera cam)
        {
            // Only the light, ambient and fog colours change between locations.
            Color key, shade, sky;
            switch (location)
            {
                case 2: key = Pal.Hex("#FFF1D6"); shade = Pal.Hex("#A897B8"); sky = Pal.Hex("#BFE0F2"); break;
                case 3: key = Pal.Hex("#FFC98F"); shade = Pal.Hex("#A68A9C"); sky = Pal.Hex("#E8C49A"); break;
                case 4: key = Pal.Hex("#8F98E6"); shade = Pal.Hex("#4E4C7E"); sky = Pal.Hex("#2A2F5C"); break;
                case 5: key = Pal.Hex("#E6F0FF"); shade = Pal.Hex("#9AA3C2"); sky = Pal.Hex("#C9D6E6"); break;
                default: key = Pal.Hex("#FFE6C2"); shade = Pal.Hex("#B39CB0"); sky = Pal.Hex("#F6D2B4"); break;
            }
            if (sun)
            {
                sun.color = key;
                sun.intensity = 1.35f;
                // the props use the standard Lit shader: real sun shadows on them and the ground
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.82f;
                sun.shadowBias = 0.05f; sun.shadowNormalBias = 0.4f;
            }
            Shader.SetGlobalVector("_ChaiShadeColor", (Vector4)shade.linear);
            // sky / horizon / ground ambient for the Lit props, from the same mood colours
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(sky, Color.white, 0.3f) * 0.85f;
            RenderSettings.ambientEquatorColor = Color.Lerp(shade, sky, 0.5f) * 0.7f;
            RenderSettings.ambientGroundColor = shade * 0.45f;
            if (cam) cam.backgroundColor = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = 34f;
            RenderSettings.fogEndDistance = 75f;
        }

        void SetupEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var m = go.AddComponent<InputSystemUIInputModule>();
            m.AssignDefaultActions();
        }

        // ------------------------------------------------------------------ locations

        public void EnterLocation(int id, bool initial)
        {
            if (Loc != null)
            {
                Loc.Teardown();
                Destroy(Loc.gameObject);
                Loc = null;
            }
            Spawner?.Clear();
            S.location = id;
            S.maxLocation = Mathf.Max(S.maxLocation, id);
            LocCfg = Eco.Location(id);
            LS = S.Loc(id);
            ApplyMood(id);

            var go = new GameObject("Location" + id);
            Loc = Location.Create(id, go);
            Loc.Build();

            if (Player == null) Player = Player.Create(Loc.PlayerSpawn);
            else Player.Teleport(Loc.PlayerSpawn);
            Player.ApplyCosmetics();
            Cam.SetTarget(Player.transform, Loc.CamBounds, Loc.CamDistance, true);

            if (Spawner == null) Spawner = gameObject.AddComponent<CustomerSpawner>();
            Spawner.Begin(Loc, initial && !S.tutorialDone);
            Loc.RestoreStaff();
            Hud?.Refresh();
        }

        /// <summary>Bought the expansion tile: celebration, then the world map shows the road ahead.</summary>
        public void StartExpansion()
        {
            LS.done = true;
            LS.finalIncome = IncomeRate;

            GameEvents.Expanded?.Invoke();
            WebBridge.HappyTime();
            AddLeaves(5, Player.transform.position + Vector3.up * 2);
            var next = Eco.Location(S.location + 1);
            if (next != null && next.playable) S.maxLocation = Mathf.Max(S.maxLocation, next.id);
            Save();
            Hud.Refresh();
            string head = next != null ? next.name : "Golden Kettle!";
            Hud.Celebrate.Show(head, next != null && next.playable ? "The road ahead is open!" : "Expansion complete!", () =>
            {
                if (next != null && next.playable) Travel(next.id);
                else Hud.Map.Open();
            });
        }

        public void Travel(int id)
        {
            var cfg = Eco.Location(id);
            if (cfg == null || !cfg.playable) return;
            bool firstVisit = S.Loc(id).unlocks.Count == 0 && S.Loc(id).tilePaid.Count == 0 && !S.Loc(id).done;
            PushPause();
            WebBridge.CommercialBreak(() =>
            {
                PopPause();
                EnterLocation(id, false);
                Progression.ResetForLocation();
                Loc.RefreshGrowth(Progression.DisplayLevel, false);
                if (firstVisit && cfg.arrivalGift > 0)
                {
                    double gift = cfg.arrivalGift * (1 + Up.Perk("headStart") * 0.5);
                    AddCash(gift, Player.transform.position, false);
                    Hud.CashFly(Player.transform.position + Vector3.up * 2, 14);
                    Hud.Toast("Welcome gift: " + Fmt.Cash(gift), "bag");
                }
                Hud.Refresh();
                Save();
            });
        }

        // ------------------------------------------------------------------ wallet

        /// <summary>Applies the 2x boost to money a customer pays.</summary>
        public double Income(double amount) => BoostLeft > 0 ? amount * Eco.rules.boostMultiplier : amount;

        public void AddCash(double amount, Vector3? worldFrom = null, bool countAsIncome = true)
        {
            if (amount <= 0) return;
            S.cash += amount;
            if (countAsIncome)
            {
                S.stats.earned += amount;
                incomeAcc += amount;
                GameEvents.CashEarned?.Invoke(amount);
            }
            GameEvents.CashChanged?.Invoke();
        }

        public bool Spend(double amount)
        {
            if (amount <= 0) return true;
            if (S.cash + 1e-6 < amount) return false;
            S.cash = Math.Max(0, S.cash - amount);
            GameEvents.CashChanged?.Invoke();
            return true;
        }

        public void AddLeaves(int n, Vector3? worldFrom = null)
        {
            if (n == 0) return;
            S.leaves = Mathf.Max(0, S.leaves + n);
            if (n > 0) Hud.LeafFly(n, worldFrom);
            GameEvents.LeavesChanged?.Invoke();
            Hud.Refresh();
            if (n > 0 && !S.leafHintShown)
            {
                S.leafHintShown = true;
                // the guide explains tea leaves the first time they arrive
            }
            RequestSave();
        }

        /// <summary>About N seconds of current income, never less than a small floor.</summary>
        public double IncomeSeconds(float seconds) => Math.Max(IncomeRate * seconds, 20 * Multiplier * seconds / 60.0 * 3);

        public void OnStatsChanged()
        {
            Player?.RefreshStats();
            Hud?.Refresh();
            Loc?.OnStatsChanged();
        }

        // ------------------------------------------------------------------ flow

        public void OnFirstMove()
        {
            if (Started) return;
            Started = true;
            Synth.I.StartMusic();
        }

        public void PushPause()
        {
            pauseRequests++;
            ApplyPause();
        }

        public void PopPause()
        {
            pauseRequests = Mathf.Max(0, pauseRequests - 1);
            ApplyPause();
        }

        /// <summary>
        /// A level just ended, the natural moment for a midgame ad. Never on closing a panel (CrazyGames doesn't allow
        /// ads on navigation) and not in the first 5 minutes of play, as CrazyGames advises; the SDK itself allows one
        /// every 3 minutes at most.
        /// </summary>
        public void LevelBreak()
        {
            if (!Started || WorldPaused || S.playSeconds < Eco.rules.midgameFirstAt) return;
            PushPause();
            WebBridge.CommercialBreak(PopPause);
        }

        public void ApplyPause()
        {
            Time.timeScale = WorldPaused ? 0f : SpeedMultiplier;
            AudioListener.pause = WebBridge.AdPlaying || hidden;
            if (Booted)
            {
                // not for a hidden tab: CrazyGames handles focus itself and asks games not to report it
                if (pauseRequests > 0 || WebBridge.AdPlaying) WebBridge.GameplayStop();
                else WebBridge.GameplayStart();
            }
        }

        void OnAdState(bool playing)
        {
            AudioListener.volume = playing ? 0 : 1;
            ApplyPause();
        }

        void OnApplicationFocus(bool focus) { if (!focus) Save(); }

        void OnApplicationPause(bool paused)
        {
#if UNITY_EDITOR
            // the editor sends this when its window loses focus; don't freeze the sim for that
            if (paused) Save();
#else
            SetHidden(paused);
#endif
        }

        /// <summary>Tab hidden or phone locked: simulation and audio paused.</summary>
        public void SetHidden(bool h)
        {
            if (hidden == h) return;
            hidden = h;
            if (h) Save();
            ApplyPause();
        }

        public void RequestSave() => saveQueued = true;

        public void Save()
        {
            saveQueued = false;
            saveTimer = 0;
            SaveSystem.Save(S);
        }

        void Update()
        {
            if (!Booted) return;
            float udt = Time.unscaledDeltaTime;
            float dt = Time.deltaTime;
            SessionTime += udt;
            if (!WorldPaused && Started)
            {
                S.playSeconds += dt;
                if (BoostLeft > 0) BoostLeft = Mathf.Max(0, BoostLeft - dt);
            }

            incomeTimer += dt;
            if (incomeTimer >= 5f)
            {
                double inst = incomeAcc / incomeTimer;
                IncomeRate = IncomeRate <= 0 ? inst : IncomeRate * 0.85 + inst * 0.15;
                incomeAcc = 0;
                incomeTimer = 0;
            }

            saveTimer += udt;
            if (saveTimer >= Eco.rules.autosave || (saveQueued && saveTimer > 0.5f)) Save();

            if (Application.isEditor && UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.f9Key.wasPressedThisFrame) { AddCash(1000, null, false); Hud.Toast("+1000 (debug)", "coin"); }
                if (kb.f10Key.wasPressedThisFrame) { SpeedMultiplier = SpeedMultiplier > 1 ? 1 : 4; ApplyPause(); }
            }
        }

        void OnDestroy()
        {
            WebBridge.AdStateChanged -= OnAdState;
            if (I == this) { Save(); I = null; }
        }

        /// <summary>Debug/editor helper: wipe the save and reload.</summary>
        public void ResetProgress()
        {
            SaveSystem.Wipe();
            S = new SaveData();
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
