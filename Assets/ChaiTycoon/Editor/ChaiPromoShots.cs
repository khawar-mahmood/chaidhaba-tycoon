using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChaiTycoon.EditorTools
{
    /// <summary>
    /// Store screenshots from the real game, run from the command line (the project must not be open):
    ///   Unity.exe -projectPath D:\ChaiTycoon -executeMethod ChaiTycoon.EditorTools.ChaiPromoShots.Run
    /// Builds the roadside stall on a fresh save (every stove, counter, table, the rickshaw stand, helpers
    /// and a cook at every stove; no canopy, whose parasols hide the tables), keeps the level card on "Table service"
    /// and hides the timed offer, lets the test bot play for a couple of
    /// minutes so the queues, tables and rickshaws fill up, then captures the game camera from a few closer
    /// framings, HUD included, three times each into Screenshots/promo. The editor's own save is put back.
    /// </summary>
    public static class ChaiPromoShots
    {
        const string Out = "Screenshots/promo";
        const string Phase = "chai_promo_phase";           // 0 off, 1 waiting for play, 2 playing, 3 restoring
        const string BackupFile = "Library/promo_save_backup.txt";
        static readonly float[] ShotAt = { 150f, 185f, 220f };     // game seconds after staging
        const int W = 1280, H = 720;

        public static void Run()
        {
            Directory.CreateDirectory(Out);
            File.WriteAllText(Out + "/log.txt", "Chai promo shots " + System.DateTime.Now + "\n");
            EditorSceneManager.OpenScene("Assets/ChaiTycoon/Scenes/Main.unity");
            ArtMaterials.Ensure();
            ShaderUtil.allowAsyncCompilation = false;
            BackUpAndClearSave();
            SessionState.SetInt(Phase, 1);
            SessionState.SetInt("chai_promo_shot", 0);
            SessionState.SetFloat("chai_promo_start", 0);
            Hook();
            EditorApplication.isPlaying = true;
        }

        /// <summary>Keeps the editor's save aside and clears it, so the run starts fresh. RestoreSave puts it back.</summary>
        internal static void BackUpAndClearSave()
        {
            RestoreSave();
            File.WriteAllText(BackupFile, PlayerPrefs.GetString(SaveSystem.Key, "") + "\n\u0001\n" + PlayerPrefs.GetString(SaveSystem.BackupKey, ""));
            PlayerPrefs.DeleteKey(SaveSystem.Key);
            PlayerPrefs.DeleteKey(SaveSystem.BackupKey);
            PlayerPrefs.Save();
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (SessionState.GetInt(Phase, 0) > 0) Hook();
        }

        static bool hooked;

        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            EditorApplication.update += Tick;
        }

        static void Note(string s) => File.AppendAllText(Out + "/log.txt", s + "\n");

        static void Tick()
        {
            int phase = SessionState.GetInt(Phase, 0);
            if (phase == 1 && EditorApplication.isPlaying && Game.I != null && Game.I.Booted)
            {
                Stage();
                SessionState.SetFloat("chai_promo_start", (float)Game.I.S.playSeconds);
                SessionState.SetInt(Phase, 2);
                AutoPlayer.Run(false);         // Rafi works the stall; no upgrades, so the cash stays put
                Note("staged; bot running");
                return;
            }
            if (phase == 2 && EditorApplication.isPlaying)
            {
                if (Game.I == null) return;
                EditorApplication.isPaused = true;
                ChaiDevTools.StepFrames(100, 0.1f);    // 10 s of game time per editor tick
                float t = (float)Game.I.S.playSeconds - SessionState.GetFloat("chai_promo_start", 0);
                int shot = SessionState.GetInt("chai_promo_shot", 0);
                if (shot < ShotAt.Length && t >= ShotAt[shot])
                {
                    Shots(shot);
                    SessionState.SetInt("chai_promo_shot", shot + 1);
                }
                if (shot >= ShotAt.Length)
                {
                    Note($"done at level {Game.I.Progression.DisplayLevel}, lost {Game.I.Spawner.Lost}");
                    SessionState.SetInt(Phase, 3);
                    EditorApplication.isPaused = false;
                    EditorApplication.isPlaying = false;
                }
                return;
            }
            if (phase == 3 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetInt(Phase, 0);
                RestoreSave();
                Note("save restored");
                EditorApplication.Exit(0);
                return;
            }
            if ((phase == 1 || phase == 2) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetInt(Phase, 0);
                RestoreSave();
                Note("play was stopped by hand: run abandoned, editor save restored");
            }
        }

        /// <summary>The full roadside stall, staffed, with the level card on "Table service".</summary>
        internal static void Stage()
        {
            var g = Game.I;
            var loc = g.Loc;
            var complete = typeof(Location).GetMethod("OnTileCompleted", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var u in g.LocCfg.unlocks)
            {
                // no canopy: its parasols cover the tables and the guests from above
                if (u.id == "expand" || u.id == "canopy" || loc.Has(u.id)) continue;
                complete.Invoke(loc, new object[] { u });
            }
            foreach (var st in loc.Stations) loc.HireCook(st, true);

            g.S.tutorialDone = true;
            g.S.upgradesSeen = true;
            g.S.stats.served = System.Math.Max(g.S.stats.served, 150L);
            g.S.cash = 4122;
            g.S.leaves = 7;
            SetLevel();
            g.OnStatsChanged();
        }

        /// <summary>"Table service" (level 10): the helper is hired, the guests and the cash still to go.</summary>
        static void SetLevel()
        {
            var g = Game.I;
            var levels = g.Progression.Levels;
            int idx = System.Array.FindIndex(levels, l => l.title == "Table service");
            g.S.levels.level = idx >= 0 ? idx : Mathf.Min(9, levels.Length - 1);
            g.S.levels.progress.Clear();
            g.S.levels.done.Clear();
            typeof(Progression).GetMethod("Ensure", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(g.Progression, null);
            g.Progression.PreCheck();
            g.Hud.Refresh();
        }

        /// <summary>Rafi with a mixed tray: one of each item the stoves have ready, up to his capacity.</summary>
        static void FillTray()
        {
            var g = Game.I;
            var stack = g.Player.Stack;
            var had = new System.Collections.Generic.HashSet<ItemId>();
            foreach (var st in g.Loc.Stations)
            {
                if (stack.Count >= stack.capacity || had.Contains(st.Item)) continue;
                var it = st.TakeOne();
                if (it == null) continue;
                stack.Add(it, 0f);
                had.Add(st.Item);
            }
        }

        /// <summary>One round of framings: the main camera itself moves (so the order bubbles follow), HUD included.</summary>
        static void Shots(int round)
        {
            var g = Game.I;
            var cam = Camera.main;
            if (!cam || g.Cam == null) return;
            SetLevel();
            FillTray();
            // the timed offer box covers the right of the screen: keep it out of the store shots
            var offer = typeof(Hud).GetField("offerBox", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(g.Hud) as RectTransform;
            if (offer) offer.gameObject.SetActive(false);
            var p = g.Player.transform.position;
            g.Cam.enabled = false;
            Shot($"{round}_rafi", cam, new Vector3(p.x, 0, p.z - 0.6f), 46f, 10.5f);
            Shot($"{round}_tables", cam, new Vector3(5.6f, 0, 1.0f), 44f, 11.5f);
            Shot($"{round}_counters", cam, new Vector3(-1.0f, 0, -2.8f), 46f, 12.5f);
            Shot($"{round}_kitchen", cam, new Vector3(-2.2f, 0, 3.6f), 50f, 11f);
            Shot($"{round}_wide", cam, new Vector3(0.4f, 0, -0.8f), 50f, 16f);
            g.Cam.enabled = true;
            Note($"round {round} at {Fmt.Clock((float)g.S.playSeconds)}");
        }

        static void Shot(string name, Camera cam, Vector3 focus, float pitch, float distance)
        {
            var rot = Quaternion.Euler(pitch, 0, 0);
            cam.transform.rotation = rot;
            cam.transform.position = focus - rot * Vector3.forward * distance;
            // a couple of near-zero frames so the world bubbles and labels catch up with the new view
            ChaiDevTools.StepFrames(2, 0.001f);
            ChaiDevTools.Snapshot($"{Out}/promo_{name}.png", W, H, cam);
        }

        /// <summary>A run that ended without restoring (stopped or killed): put the editor's own save back on next open.</summary>
        [InitializeOnLoadMethod]
        static void RestoreLeftover()
        {
            if (SessionState.GetInt(Phase, 0) == 0 && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(BackupFile))
                EditorApplication.delayCall += () =>
                {
                    if (SessionState.GetInt(Phase, 0) != 0 || EditorApplication.isPlayingOrWillChangePlaymode) return;
                    RestoreSave();
                    Debug.Log("ChaiPromoShots: restored the editor save left over from an interrupted run.");
                };
        }

        internal static void RestoreSave()
        {
            if (!File.Exists(BackupFile)) return;
            var parts = File.ReadAllText(BackupFile).Split(new[] { "\n\u0001\n" }, System.StringSplitOptions.None);
            Restore(SaveSystem.Key, parts[0]);
            Restore(SaveSystem.BackupKey, parts.Length > 1 ? parts[1] : "");
            PlayerPrefs.Save();
            File.Delete(BackupFile);
        }

        static void Restore(string key, string value)
        {
            if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetString(key, value);
        }
    }
}
