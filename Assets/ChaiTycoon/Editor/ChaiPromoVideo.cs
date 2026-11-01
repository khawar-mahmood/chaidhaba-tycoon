using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChaiTycoon.EditorTools
{
    /// <summary>
    /// The two CrazyGames preview videos, rendered from the real game (Chai → Web → Render CrazyGames Videos):
    /// silent H.264 MP4s at 1920×1080 and 1080×1620 in Art/Store/videos, about 18 seconds long.
    /// Stages the full stall on a fresh save, lets the test bot fill it up, then steps the game exactly one video frame
    /// at a time, so the footage is true 1× speed however slow the rendering is (CrazyGames doesn't allow sped-up
    /// footage). Each video opens on its static cover, as CrazyGames asks. The landscape video keeps the HUD; the
    /// portrait one has none, because the HUD wasn't laid out for a tall frame. The editor's own save is put back.
    /// </summary>
    public static class ChaiPromoVideo
    {
        const string Out = "Art/Store/videos";
        const string Covers = "Art/Store/covers";
        const int Fps = 30, LandscapeW = 1920, LandscapeH = 1080, PortraitW = 1080, PortraitH = 1620;
        const float WarmUpSeconds = 100f, CoverSeconds = 1f, PortraitPullBack = 1.2f;
        const uint BitRate = 10000000;

        enum Step { Off, WaitingForGame, WarmingUp, Recording, Restoring }

        static Step step;
        static float seconds, warmStart;
        static int frame, frames;
        static Camera cam;
        static RenderTexture landscapeRt, portraitRt;
        static Texture2D landscapeTex, portraitTex;
        static MediaEncoder landscapeVideo, portraitVideo;
        static readonly List<Canvas> canvases = new List<Canvas>();

        [MenuItem("Chai/Web/Render CrazyGames Videos")]
        public static void RenderVideos() => Run(18f);

        public static void Run(float length)
        {
            if (step != Step.Off || EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[Video] already busy"); return; }
            Directory.CreateDirectory(Out);
            seconds = length;
            EditorSceneManager.OpenScene("Assets/ChaiTycoon/Scenes/Main.unity");
            ArtMaterials.Ensure();
            ShaderUtil.allowAsyncCompilation = false;
            ChaiPromoShots.BackUpAndClearSave();
            step = Step.WaitingForGame;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            try
            {
                switch (step)
                {
                    case Step.WaitingForGame:
                        if (!EditorApplication.isPlaying || Game.I == null || !Game.I.Booted) break;
                        ChaiPromoShots.Stage();
                        // the cooks' task only counts as reached once the stage has set the level
                        foreach (var st in Game.I.Loc.Stations) Game.I.Loc.HireCook(st, true);
                        AutoPlayer.Run(false);         // Rafi works the stall; no upgrades, so the cash stays put
                        warmStart = (float)Game.I.S.playSeconds;
                        EditorApplication.isPaused = true;
                        step = Step.WarmingUp;
                        Debug.Log("[Video] staged, warming up");
                        break;
                    case Step.WarmingUp:
                        ChaiDevTools.StepFrames(50, 0.1f);
                        if (Game.I.S.playSeconds - warmStart >= WarmUpSeconds) Begin();
                        break;
                    case Step.Recording:
                        ChaiDevTools.StepFrames(1, 1f / Fps);
                        Capture();
                        if (++frame % Fps == 0) Debug.Log($"[Video] {frame / Fps} s of {seconds:0}");
                        if (frame >= frames) Finish();
                        break;
                    case Step.Restoring:
                        if (EditorApplication.isPlayingOrWillChangePlaymode) break;
                        ChaiPromoShots.RestoreSave();
                        Time.captureDeltaTime = 0;
                        ShaderUtil.allowAsyncCompilation = true;
                        EditorApplication.update -= Tick;
                        step = Step.Off;
                        Debug.Log("[Video] done: " + Out);
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Video] failed: " + e);
                Finish();
            }
        }

        static void Begin()
        {
            cam = Camera.main;
            foreach (var d in Object.FindObjectsByType<DisplayAdapt>(FindObjectsSortMode.None)) d.enabled = false;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = 1f;

            landscapeRt = new RenderTexture(LandscapeW, LandscapeH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            portraitRt = new RenderTexture(PortraitW, PortraitH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            landscapeTex = new Texture2D(LandscapeW, LandscapeH, TextureFormat.RGBA32, false);
            portraitTex = new Texture2D(PortraitW, PortraitH, TextureFormat.RGBA32, false);

            // the HUD draws into the camera's picture while recording (overlay canvases only reach the screen)
            canvases.Clear();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.05f + (20 - c.sortingOrder) * 0.002f;
                canvases.Add(c);
            }
            cam.targetTexture = landscapeRt;

            landscapeVideo = NewVideo("preview_landscape_1920x1080.mp4", LandscapeW, LandscapeH);
            portraitVideo = NewVideo("preview_portrait_1080x1620.mp4", PortraitW, PortraitH);
            AddCover(landscapeVideo, landscapeTex, $"{Covers}/cover_landscape_1920x1080.png");
            AddCover(portraitVideo, portraitTex, $"{Covers}/cover_portrait_1080x1620.png");

            frame = 0;
            frames = Mathf.RoundToInt((seconds - CoverSeconds) * Fps);
            step = Step.Recording;
            Debug.Log("[Video] recording");
        }

        static MediaEncoder NewVideo(string name, int w, int h)
        {
            var h264 = new H264EncoderAttributes { gopSize = Fps, numConsecutiveBFrames = 0, profile = VideoEncodingProfile.H264High };
            var attrs = new VideoTrackEncoderAttributes(h264)
            {
                frameRate = new MediaRational(Fps),
                width = (uint)w,
                height = (uint)h,
                targetBitRate = BitRate,
            };
            return new MediaEncoder(Path.Combine(Out, name), attrs);
        }

        /// <summary>The static cover as the opening frame, copied into a frame texture: LoadImage changes the format of the texture it fills, and the encoder only takes RGBA32.</summary>
        static void AddCover(MediaEncoder video, Texture2D frameTex, string path)
        {
            var cover = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            cover.LoadImage(File.ReadAllBytes(path));
            frameTex.SetPixels32(cover.GetPixels32());
            frameTex.Apply();
            Object.DestroyImmediate(cover);
            for (int i = 0; i < Mathf.RoundToInt(CoverSeconds * Fps); i++) video.AddFrame(frameTex);
        }

        static void Capture()
        {
            Render(landscapeRt, landscapeTex);
            landscapeVideo.AddFrame(landscapeTex);

            // the taller picture: no HUD, Rafi in the middle, pulled back a little so the stall still fits across
            var pos = cam.transform.position;
            var player = Game.I.Player.transform.position + Vector3.up;
            float distance = Vector3.Dot(player - pos, cam.transform.forward);
            cam.transform.position = player - cam.transform.forward * (distance * PortraitPullBack);
            foreach (var c in canvases) c.enabled = false;
            cam.targetTexture = portraitRt;
            Render(portraitRt, portraitTex);
            portraitVideo.AddFrame(portraitTex);
            cam.targetTexture = landscapeRt;
            foreach (var c in canvases) c.enabled = true;
            cam.transform.position = pos;
        }

        static void Render(RenderTexture rt, Texture2D tex)
        {
            Canvas.ForceUpdateCanvases();
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
        }

        static void Finish()
        {
            landscapeVideo?.Dispose();
            portraitVideo?.Dispose();
            landscapeVideo = portraitVideo = null;
            if (cam) cam.targetTexture = null;
            foreach (var c in canvases) if (c) { c.enabled = true; c.renderMode = RenderMode.ScreenSpaceOverlay; }
            canvases.Clear();
            if (landscapeRt) { landscapeRt.Release(); Object.DestroyImmediate(landscapeRt); }
            if (portraitRt) { portraitRt.Release(); Object.DestroyImmediate(portraitRt); }
            if (landscapeTex) Object.DestroyImmediate(landscapeTex);
            if (portraitTex) Object.DestroyImmediate(portraitTex);
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = false;
            step = Step.Restoring;
        }
    }
}
