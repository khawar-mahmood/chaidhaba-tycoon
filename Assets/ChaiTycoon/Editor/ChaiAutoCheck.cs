using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChaiTycoon.EditorTools
{
    /// <summary>
    /// Unattended art + gameplay check, run from the command line (the project must not be open):
    ///   Unity.exe -projectPath D:\ChaiTycoon -executeMethod ChaiTycoon.EditorTools.ChaiAutoCheck.Run
    /// Renders the stage 2-5 art previews, plays stage 1 from a fresh save with the test bot for about
    /// nine game minutes (screenshots on the way), writes Screenshots/autocheck/log.txt and quits.
    /// The editor's own save is put back afterwards. Play mode reloads the domain, so the state lives in
    /// SessionState and a file, and the [InitializeOnLoad] hook picks the run up again.
    /// </summary>
    public static class ChaiAutoCheck
    {
        const string Out = "Screenshots/autocheck";
        const string Phase = "chai_autocheck_phase";      // 0 off, 1 waiting for play, 2 playing, 3 restoring
        const string BackupFile = "Library/autocheck_save_backup.txt";
        const float EndAt = 540f;                          // game seconds of bot play
        static readonly float[] ShotAt = { 4f, 120f, 240f, 360f, 480f };

        static string LogPath => Out + "/log.txt";

        public static void Run()
        {
            Directory.CreateDirectory(Out);
            File.WriteAllText(LogPath, "Chai auto check " + System.DateTime.Now + "\n");
            EditorSceneManager.OpenScene("Assets/ChaiTycoon/Scenes/Main.unity");
            ArtMaterials.Ensure();
            // compile shaders before rendering: an asynchronously compiling shader draws nothing in a snapshot
            ShaderUtil.allowAsyncCompilation = false;
            var warm = ChaiDevTools.PreviewStage(2);                // first render warms up the shaders; not kept
            if (warm) ChaiDevTools.Snapshot($"{Out}/warmup.png", 320, 180, warm);
            for (int s = 2; s <= 5; s++)
            {
                var cam = ChaiDevTools.PreviewStage(s);
                if (cam) ChaiDevTools.Snapshot($"{Out}/stage{s}.png", 1280, 720, cam);
                Note($"stage {s} preview: " + (cam ? "ok" : "no art"));
            }
            ChaiDevTools.ClearPreview();

            // fresh save for the bot; keep the editor's save to put back (first undo an interrupted run's swap)
            RestoreSave();
            File.WriteAllText(BackupFile, PlayerPrefs.GetString(SaveSystem.Key, "") + "\n\u0001\n" + PlayerPrefs.GetString(SaveSystem.BackupKey, ""));
            PlayerPrefs.DeleteKey(SaveSystem.Key);
            PlayerPrefs.DeleteKey(SaveSystem.BackupKey);
            PlayerPrefs.Save();
            SessionState.SetInt(Phase, 1);
            SessionState.SetInt("chai_autocheck_shot", 0);
            SessionState.SetInt("chai_autocheck_errors", 0);
            Hook();
            EditorApplication.isPlaying = true;
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
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (SessionState.GetInt(Phase, 0) != 2) return;
            SessionState.SetInt("chai_autocheck_errors", SessionState.GetInt("chai_autocheck_errors", 0) + 1);
            Note($"{type}: {msg}\n{stack}");
        }

        static void Note(string s) => File.AppendAllText(LogPath, s + "\n");

        static void Tick()
        {
            int phase = SessionState.GetInt(Phase, 0);
            if (phase == 1 && EditorApplication.isPlaying && Game.I != null && Game.I.Booted)
            {
                SessionState.SetInt(Phase, 2);
                AutoPlayer.Run();
                Note("play started, bot running");
                return;
            }
            if (phase == 2 && EditorApplication.isPlaying)
            {
                if (Game.I == null) return;
                EditorApplication.isPaused = true;
                ChaiDevTools.StepFrames(150, 0.1f);   // 15 s of game time per editor tick
                float t = (float)Game.I.S.playSeconds;
                int shot = SessionState.GetInt("chai_autocheck_shot", 0);
                if (shot < ShotAt.Length && t >= ShotAt[shot])
                {
                    ChaiDevTools.Snapshot($"{Out}/play_{shot}_{Mathf.RoundToInt(t)}s.png");
                    if (shot == ShotAt.Length - 1) CloseUps();
                    SessionState.SetInt("chai_autocheck_shot", shot + 1);
                }
                if (t >= EndAt)
                {
                    Note($"stopped at {Fmt.Clock(t)}: level {Game.I.Progression.DisplayLevel}, cash {Fmt.Cash(Game.I.S.cash)}, lost {Game.I.Spawner.Lost}, errors {SessionState.GetInt("chai_autocheck_errors", 0)}");
                    if (AutoPlayer.Inst) Note(AutoPlayer.Inst.Log.ToString());
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
                Note("done");
                EditorApplication.Exit(0);
                return;
            }
            // someone stopped play mode by hand during the run: put the editor save back and leave the editor open
            if ((phase == 1 || phase == 2) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetInt(Phase, 0);
                RestoreSave();
                Note("play was stopped by hand: run abandoned, editor save restored");
            }
        }

        /// <summary>A run that ended without restoring (the editor closed or play stopped): put the save back.</summary>
        [InitializeOnLoadMethod]
        static void RestoreLeftover()
        {
            if (SessionState.GetInt(Phase, 0) == 0 && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(BackupFile))
                EditorApplication.delayCall += () =>
                {
                    if (SessionState.GetInt(Phase, 0) != 0 || EditorApplication.isPlayingOrWillChangePlaymode) return;
                    RestoreSave();
                    Debug.Log("ChaiAutoCheck: restored the editor save left over from an interrupted check.");
                };
        }

        static void RestoreSave()
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

        /// <summary>Close views of the kitchen row and the dining sets.</summary>
        static void CloseUps()
        {
            var go = new GameObject("AutoCheckCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            var main = Camera.main;
            if (main) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }
            go.transform.position = new Vector3(-2.2f, 5.2f, 0.4f);
            go.transform.LookAt(new Vector3(-2.2f, 1.0f, 5.4f));
            ChaiDevTools.Snapshot($"{Out}/closeup_kitchen.png", 1280, 720, cam);
            go.transform.position = new Vector3(5.0f, 6.0f, -4.4f);
            go.transform.LookAt(new Vector3(5.0f, 0.4f, 1.2f));
            ChaiDevTools.Snapshot($"{Out}/closeup_tables.png", 1280, 720, cam);
            Object.DestroyImmediate(go);
        }
    }
}
