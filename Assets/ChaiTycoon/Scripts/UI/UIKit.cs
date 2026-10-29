using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>Helpers to build uGUI from code on the 1280x720 reference canvas.</summary>
    public static class UIKit
    {
        static Font font, bodyFont, builtin;
        static Font Builtin => builtin ? builtin : builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>Baloo 2 for titles, numbers and buttons.</summary>
        public static Font Font => font ? font : font = Resources.Load<Font>("Fonts/Baloo2-ExtraBold");

        /// <summary>Nunito for smaller descriptive text.</summary>
        public static Font BodyFont => bodyFont ? bodyFont : bodyFont = Resources.Load<Font>("Fonts/Nunito-ExtraBold");

        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            c.pixelPerfect = false;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1280, 720);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            go.AddComponent<AspectMatch>();
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static RectTransform Rect(Transform parent, string name = "rect")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Image(Transform parent, Sprite s, Color c, string name = "img")
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.color = c;
            img.raycastTarget = false;
            if (s != null && s.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Text Text(Transform parent, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, bool bold = true, string name = "txt")
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            // the bundled fonts are already bold cuts; only the built-in fallback needs faux bold
            var f = bold ? Font : BodyFont;
            t.font = f ? f : Builtin;
            t.text = s;
            t.fontSize = size;
            t.color = c;
            t.alignment = a;
            t.fontStyle = f || !bold ? FontStyle.Normal : FontStyle.Bold;
            // Baloo's tall line height (it also covers Devanagari) leaves big gaps between wrapped lines
            if (f) t.lineSpacing = bold ? 0.78f : 0.9f;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            return t;
        }

        /// <summary>World and HUD text gets an ink outline for contrast.</summary>
        public static Text OutlinedText(Transform parent, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, float width = 2.5f)
        {
            var t = Text(parent, s, size, c, a, true);
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = Pal.Ink;
            o.effectDistance = new Vector2(width, -width);
            var o2 = t.gameObject.AddComponent<Outline>();
            o2.effectColor = Pal.Ink;
            o2.effectDistance = new Vector2(-width, width);
            return t;
        }

        public static void Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Places a rect by anchor (0..1), pivot and size on the reference canvas.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>GDD top-left reference coordinates (y down) to an anchored top-left rect.</summary>
        public static RectTransform TopLeft(RectTransform rt, float x, float y, float w, float h) =>
            Place(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));

        public static RectTransform TopRight(RectTransform rt, float xFromRight, float y, float w, float h) =>
            Place(rt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-xFromRight, -y), new Vector2(w, h));

        public static RectTransform BottomRight(RectTransform rt, float xFromRight, float yFromBottom, float w, float h) =>
            Place(rt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-xFromRight, yFromBottom), new Vector2(w, h));

        public static Image PanelBox(Transform parent, Color fill, int radius = 16)
        {
            var shadow = Image(parent, SpriteGen.SoftShadow(), Color.white, "shadow");
            var p = Image(shadow.transform, SpriteGen.Panel(radius, 3), fill, "panel");
            Stretch(p.rectTransform, 0);
            var sh = shadow.rectTransform;
            p.rectTransform.offsetMin = new Vector2(10, 14);
            p.rectTransform.offsetMax = new Vector2(-10, -6);
            return p;
        }

        public static Button Button(Transform parent, Color color, string label, Action onClick, int fontSize = 26, Sprite iconSprite = null, float iconSize = 40)
        {
            var img = Image(parent, SpriteGen.Button(18), color, "btn");
            Paint(img, color);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { Synth.Play("click"); onClick?.Invoke(); });
            img.gameObject.AddComponent<PressFx>();
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
            if (iconSprite != null)
            {
                var ic = Image(img.transform, iconSprite, Color.white, "icon");
                ic.preserveAspect = true;
                Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 3), new Vector2(iconSize, iconSize));
            }
            if (label != null)
            {
                var t = OutlinedText(img.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter, 1.6f);
                Stretch(t.rectTransform, 0);
                t.rectTransform.offsetMin = new Vector2(iconSprite != null ? iconSize + 8 : 6, 6);
                t.rectTransform.offsetMax = new Vector2(-6, 0);
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = 14;
                t.resizeTextMaxSize = fontSize;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Truncate;
            }
            return b;
        }

        /// <summary>Recolours a button: the painted button of that colour when the skin has one, else a tinted shape.</summary>
        public static void Paint(Image img, Color color)
        {
            var s = Skin.ButtonFor(color);
            if (s != null) { img.sprite = s; img.color = Color.white; img.type = UnityEngine.UI.Image.Type.Sliced; }
            else { img.sprite = SpriteGen.Button(18); img.color = color; img.type = UnityEngine.UI.Image.Type.Sliced; }
        }

        public static Button IconButton(Transform parent, Sprite icon, Color bg, Action onClick, float iconScale = 0.7f)
        {
            var img = Image(parent, SpriteGen.Button(20), bg, "iconBtn");
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { Synth.Play("click"); onClick?.Invoke(); });
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
            img.gameObject.AddComponent<PressFx>();
            var ic = Image(img.transform, icon, Color.white, "icon");
            ic.preserveAspect = true;
            Stretch(ic.rectTransform, 0);
            ic.rectTransform.anchorMin = new Vector2(0.5f - iconScale / 2, 0.5f - iconScale / 2 + 0.03f);
            ic.rectTransform.anchorMax = new Vector2(0.5f + iconScale / 2, 0.5f + iconScale / 2 + 0.03f);
            ic.rectTransform.offsetMin = Vector2.zero;
            ic.rectTransform.offsetMax = Vector2.zero;
            return b;
        }

        public static Image Dot(Transform parent, Vector2 pos, float size = 22)
        {
            var painted = Skin.Get("dot");
            var d = Image(parent, painted != null ? painted : SpriteGen.Circle(64, 6), painted != null ? Color.white : Pal.Chili, "dot");
            Place(d.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            d.gameObject.AddComponent<DotPulse>();
            return d;
        }

        /// <summary>Panels scale in from 90% over 0.2 s.</summary>
        public static void ScaleIn(Transform t)
        {
            t.localScale = Vector3.one * 0.9f;
            var cg = t.GetComponent<CanvasGroup>();
            if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            Tw.Run(0.2f, k =>
            {
                if (!t) return;
                t.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, Tw.OutBack(k));
                cg.alpha = Mathf.Clamp01(k * 2.5f);
            }, null, true);
            // then the panel's buttons spring in one after another
            int i = 0;
            foreach (var fx in t.GetComponentsInChildren<PressFx>(false))
                fx.Pop(0.08f + Mathf.Min(i++, 14) * 0.035f);
        }

        /// <summary>Panels shrink and fade out over 0.12 s, then deactivate.</summary>
        public static void ScaleOut(Transform frame, GameObject panel, System.Func<bool> stillClosed = null)
        {
            var cg = frame.GetComponent<CanvasGroup>();
            if (cg == null) cg = frame.gameObject.AddComponent<CanvasGroup>();
            Tw.Run(0.12f, k =>
            {
                if (!frame) return;
                frame.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, k);
                cg.alpha = 1 - k;
            }, () =>
            {
                if (stillClosed != null && !stillClosed()) return;     // reopened meanwhile: leave it to the open animation
                if (panel) panel.SetActive(false);
                if (frame) { frame.localScale = Vector3.one; cg.alpha = 1; }
            }, true);
        }
    }

    /// <summary>
    /// Springy buttons: they grow a little under the mouse, squash when pressed and bounce back with an
    /// overshoot on release. Attention makes a button wiggle every few seconds (something to buy or claim).
    /// Runs on unscaled time, so it works while the game is paused behind a panel.
    /// </summary>
    public class PressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public bool Attention;
        Vector3 baseScale = Vector3.one;
        bool down, over, ready;
        float scale = 1f, vel, wiggleT, attentionTimer = 1.5f, hold;

        void Awake() { baseScale = transform.localScale; ready = true; }

        // every time a button appears it springs in from small
        void OnEnable() { scale = 0.35f; vel = 0; down = over = false; }

        /// <summary>Hide, then spring in after a delay (staggered panel entrances).</summary>
        public void Pop(float delay) { scale = 0f; vel = 0; hold = delay; transform.localScale = Vector3.zero; }

        public void OnPointerDown(PointerEventData e) { down = true; vel -= 2f; }
        public void OnPointerUp(PointerEventData e) { if (down) vel += 9f; down = false; }       // spring out past full size
        public void OnPointerEnter(PointerEventData e) => over = true;
        public void OnPointerExit(PointerEventData e) { over = false; down = false; }

        void Update()
        {
            if (!ready) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (hold > 0) { hold -= dt; transform.localScale = Vector3.zero; return; }
            float target = down ? 0.9f : over ? 1.06f : 1f;
            // damped spring towards the target size
            vel += ((target - scale) * 260f - vel * 16f) * dt;
            scale += vel * dt;
            float rot = 0;
            if (Attention && !down)
            {
                attentionTimer -= dt;
                if (attentionTimer <= 0) { attentionTimer = 2.6f; wiggleT = 0.5f; }
            }
            if (wiggleT > 0)
            {
                wiggleT -= dt;
                float k = wiggleT / 0.5f;
                rot = Mathf.Sin(k * Mathf.PI * 6) * 9f * k;
                scale += Mathf.Sin(k * Mathf.PI) * 0.004f;
            }
            transform.localScale = baseScale * scale;
            transform.localRotation = Quaternion.Euler(0, 0, rot);
        }
    }

    /// <summary>Red notification dots pulse gently so they catch the eye.</summary>
    public class DotPulse : MonoBehaviour
    {
        float t, offset;
        void Awake() => offset = UnityEngine.Random.value * 3f;
        void OnEnable() { t = 0; Tw.PopIn(transform, 0.3f, true); }
        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (t < 0.3f) return;                                     // let the pop-in finish
            float s = 1f + 0.12f * Mathf.Max(0, Mathf.Sin((t + offset) * 5f));
            transform.localScale = Vector3.one * s;
        }
    }

    /// <summary>Keeps the 16:9 reference layout fitting any aspect (match height on wide screens).</summary>
    public class AspectMatch : MonoBehaviour
    {
        CanvasScaler s;
        int w, h;
        void Awake() => s = GetComponent<CanvasScaler>();
        void Update()
        {
            if (Screen.width == w && Screen.height == h) return;
            w = Screen.width; h = Screen.height;
            float aspect = (float)w / Mathf.Max(1, h);
            s.matchWidthOrHeight = aspect >= 16f / 9f ? 1f : 0f;
        }
    }
}
