using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ChaiTycoon
{
    /// <summary>
    /// One input: move. A floating joystick for touch and mouse alike (press, drag, hold), or WASD/arrows.
    /// The joystick only starts on empty screen space.
    /// </summary>
    public static class InputReader
    {
        public static Vector2 Move { get; private set; }
        public static bool JoyActive { get; private set; }
        public static Vector2 JoyOrigin { get; private set; }
        public static Vector2 JoyPos { get; private set; }
        public static bool UsedTouch { get; private set; }
        public static bool AnyInput { get; private set; }

        /// <summary>Editor/test hook: drives the player like a joystick when set.</summary>
        public static Vector2? DebugOverride;

        static bool touchHeld, mouseHeld;
        static readonly List<RaycastResult> hits = new List<RaycastResult>();
        static int lastFrame = -1;

        // the editor enters play mode without reloading scripts: start each session with clean input
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Move = Vector2.zero; JoyActive = false; JoyOrigin = JoyPos = Vector2.zero;
            UsedTouch = AnyInput = false; DebugOverride = null;
            touchHeld = mouseHeld = false; lastFrame = -1;
        }

        public static float JoyRadius => 70f * UIScale;
        public static float UIScale => Mathf.Max(0.5f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));

        static bool OverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var ped = new PointerEventData(es) { position = screenPos };
            hits.Clear();
            es.RaycastAll(ped, hits);
            return hits.Count > 0;
        }

        /// <summary>The stick's direction for a pointer held at pos (origin trails behind past the rim).</summary>
        static Vector2 Stick(Vector2 pos)
        {
            var d = pos - JoyOrigin;
            float r = JoyRadius;
            if (d.magnitude > r)
            {
                // drag the origin along so direction changes stay responsive
                JoyOrigin = pos - d.normalized * r;
                d = d.normalized * r;
            }
            JoyPos = JoyOrigin + d;
            var v = d / r;
            return v.magnitude > 0.12f ? v : Vector2.zero;
        }

        /// <summary>Call once per frame (from the player).</summary>
        public static void Poll(Vector2 playerScreenPos, bool blocked)
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            Vector2 mv = Vector2.zero;
            AnyInput = false;

            // keyboard
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) mv.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) mv.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mv.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mv.x -= 1;
            }

            // touch and mouse work the same way: a floating joystick. Press on empty screen, drag to steer; while
            // the press is held Rafi keeps walking in the drag direction, even if the pointer stops moving or
            // slides over the HUD. Releasing stops him.
            bool joyNow = false;
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
            {
                var pos = ts.primaryTouch.position.ReadValue();
                if (!touchHeld && !OverUI(pos) && !blocked) { touchHeld = true; JoyOrigin = pos; }
                if (touchHeld) { joyNow = true; UsedTouch = true; mv += Stick(pos); }
            }
            else touchHeld = false;

            var mouse = Mouse.current;
            if (!joyNow && mouse != null && mouse.leftButton.isPressed)
            {
                var pos = mouse.position.ReadValue();
                if (!mouseHeld && mouse.leftButton.wasPressedThisFrame && !OverUI(pos) && !blocked) { mouseHeld = true; JoyOrigin = pos; }
                if (mouseHeld) { joyNow = true; mv += Stick(pos); }
            }
            else if (mouse == null || !mouse.leftButton.isPressed) mouseHeld = false;
            JoyActive = joyNow;

            if (DebugOverride.HasValue) mv = DebugOverride.Value;
            if (blocked) mv = Vector2.zero;
            if (mv.sqrMagnitude > 1) mv.Normalize();
            Move = mv;
            AnyInput = mv.sqrMagnitude > 0.01f;
        }
    }
}
