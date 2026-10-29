using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ChaiTycoon.EditorTools
{
    /// <summary>Editor helpers for testing: fixed 16:9 game view and screenshots.</summary>
    public static class ChaiDevTools
    {
        static Type GameViewType => typeof(Editor).Assembly.GetType("UnityEditor.GameView");

        [MenuItem("Chai/Game View 1280x720")]
        public static void SetGameView1280() => SetGameViewSize(1280, 720, "Chai 1280x720");

        [MenuItem("Chai/Game View 844x390 (phone)")]
        public static void SetGameViewPhone() => SetGameViewSize(844, 390, "Chai phone 844x390");

        public static bool SetGameViewSize(int w, int h, string label)
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleType.GetProperty("instance").GetValue(null, null);
            var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)GameViewSizeGroupType.Standalone });
            var groupType = group.GetType();
            int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            int found = -1;
            for (int i = 0; i < total; i++)
            {
                var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                var st = size.GetType();
                int sw = (int)st.GetProperty("width").GetValue(size);
                int sh = (int)st.GetProperty("height").GetValue(size);
                if (sw == w && sh == h) { found = i; break; }
            }
            if (found < 0)
            {
                var gvsType = asm.GetType("UnityEditor.GameViewSize");
                var gvstType = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = gvsType.GetConstructor(new[] { gvstType, typeof(int), typeof(int), typeof(string) });
                var fixedRes = Enum.ToObject(gvstType, 1);
                var newSize = ctor.Invoke(new[] { fixedRes, w, h, label });
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                found = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
            }
            var gv = EditorWindow.GetWindow(GameViewType, false, null, false);
            var prop = GameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            prop.SetValue(gv, found);
            gv.Repaint();
            return true;
        }

        static bool pumping;

        /// <summary>Stop play, refresh/recompile, then re-enter play (optionally with a fresh save).</summary>
        [MenuItem("Chai/Restart Play (fresh save)")]
        public static void RestartFresh() => Restart(true);

        public static void Restart(bool wipe)
        {
            SessionState.SetBool("chai_play_after_reload", true);
            SessionState.SetBool("chai_wipe", wipe);
            EditorApplication.isPlaying = false;
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            EditorApplication.delayCall += TryPlay;
        }

        static void TryPlay()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!EditorApplication.isPlaying) EditorApplication.delayCall += TryPlay;
                return;
            }
            if (!SessionState.GetBool("chai_play_after_reload", false)) return;
            SessionState.SetBool("chai_play_after_reload", false);
            if (SessionState.GetBool("chai_wipe", false)) PlayerPrefs.DeleteAll();
            var logEntries = Type.GetType("UnityEditor.LogEntries, UnityEditor");
            logEntries?.GetMethod("Clear")?.Invoke(null, null);
            EditorApplication.isPlaying = true;
        }

        /// <summary>Keeps the player loop ticking while the editor window is in the background.</summary>
        [InitializeOnLoadMethod]
        public static void StartPump()
        {
            if (SessionState.GetBool("chai_play_after_reload", false)) EditorApplication.delayCall += TryPlay;
            if (pumping) return;
            pumping = true;
            EditorApplication.update += () =>
            {
                if (EditorApplication.isPlaying && !EditorApplication.isPaused) EditorApplication.QueuePlayerLoopUpdate();
            };
        }

        /// <summary>Stop the Profiler recording (stepping thousands of frames with it on runs the editor out of memory).</summary>
        public static void DisableProfiler()
        {
            UnityEditorInternal.ProfilerDriver.enabled = false;
            UnityEngine.Profiling.Profiler.enabled = false;
            var pwType = typeof(Editor).Assembly.GetType("UnityEditor.ProfilerWindow");
            if (pwType != null)
                foreach (var w in Resources.FindObjectsOfTypeAll(pwType)) ((EditorWindow)w).Close();
        }

        /// <summary>Advance the game by N frames of fixed 1/30 s (works while the editor is in the background).</summary>
        public static void StepFrames(int n, float dt = 1f / 30f)
        {
            if (UnityEditorInternal.ProfilerDriver.enabled) DisableProfiler();
            Time.captureDeltaTime = dt;
            for (int i = 0; i < n; i++) EditorApplication.Step();
        }

        /// <summary>Renders the main camera plus the overlay canvases into a PNG.</summary>
        public static void Snapshot(string path, int w = 1280, int h = 720, Camera camera = null)
        {
            var cam = camera ? camera : Camera.main;
            if (!cam) return;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var changed = new System.Collections.Generic.List<Canvas>();
            foreach (var c in canvases)
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.05f + (20 - c.sortingOrder) * 0.002f;
                changed.Add(c);
            }
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            var req = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req)) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = prevTarget;
            foreach (var c in changed) { c.renderMode = RenderMode.ScreenSpaceOverlay; }
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        // ------------------------------------------------------------------ art preview (stages 2-5)

        const string PreviewRoot = "ArtPreview (not saved)";

        [MenuItem("Chai/Art Preview/Stage 2 - Highway Dhaba")] static void PreviewStage2() => PreviewStage(2);
        [MenuItem("Chai/Art Preview/Stage 3 - GT Road Truck Stop")] static void PreviewStage3() => PreviewStage(3);
        [MenuItem("Chai/Art Preview/Stage 4 - City Food Street")] static void PreviewStage4() => PreviewStage(4);
        [MenuItem("Chai/Art Preview/Stage 5 - Mountain Cafe")] static void PreviewStage5() => PreviewStage(5);
        [MenuItem("Chai/Art Preview/Clear")] public static void ClearPreview()
        {
            var old = GameObject.Find(PreviewRoot);
            if (old) UnityEngine.Object.DestroyImmediate(old);
        }

        /// <summary>
        /// Builds a stage's Blender art (environment, props, crowd) in edit mode, lit with that stage's mood,
        /// with a preview camera at the game's 50-degree angle. Nothing is saved into the scene.
        /// </summary>
        public static Camera PreviewStage(int stage)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop play mode first."); return null; }
            ClearPreview();
            var holder = new GameObject(PreviewRoot);
            var root = ArtStage.Build(stage, holder.transform);
            if (!root) { UnityEngine.Object.DestroyImmediate(holder); Debug.LogWarning($"No art pack for stage {stage}."); return null; }
            holder.transform.position = new Vector3(0, 0, 200);   // away from the stage 1 scene content
            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(holder.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 35f; cam.nearClipPlane = 1f; cam.farClipPlane = 160f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.localPosition = new Vector3(0, 30f, -24f);
            camGo.transform.localRotation = Quaternion.Euler(50, 0, 0);
            Light sun = null;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            Game.ApplyMood(stage, sun, cam);
            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
            if (SceneView.lastActiveSceneView)
            {
                SceneView.lastActiveSceneView.LookAt(holder.transform.position + new Vector3(0, 0, 1), Quaternion.Euler(50, 0, 0), 26f);
                SceneView.lastActiveSceneView.Repaint();
            }
            return cam;
        }

        public static void Shot(string path)
        {
            ScreenCapture.CaptureScreenshot(path);
            var gv = EditorWindow.GetWindow(GameViewType, false, null, false);
            gv.Repaint();
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
