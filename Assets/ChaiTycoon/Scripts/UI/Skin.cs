using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>
    /// The painted UI from the mockups (Assets/UI/slicing), copied to Resources/UISkin by
    /// Art/UI/copy_ui_skin.ps1. The pieces are drawn for 1920x1080 and the HUD canvas is 1280x720, so every
    /// sprite is made at 150 pixels per unit (2/3 size on the canvas). Pieces that stretch get 9-slice borders
    /// here. Without the folder the HUD falls back to the generated SpriteGen shapes.
    /// </summary>
    public static class Skin
    {
        public const float PPU = 150f;

        // mockup text colours
        public static readonly Color Ink = Pal.Hex("#3A1D12");
        public static readonly Color Orange = Pal.Hex("#E0711C");
        public static readonly Color Blue = Pal.Hex("#1B6DB3");
        public static readonly Color Soft = Pal.Hex("#7A6155");

        // 9-slice borders in source pixels (left, bottom, right, top)
        static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { "coin_bar", new Vector4(110, 0, 44, 0) }, { "leaf_bar", new Vector4(105, 0, 44, 0) },
            { "level_bar", new Vector4(140, 0, 44, 0) }, { "level_bar_first", new Vector4(140, 0, 44, 0) },
            { "hint_bar", new Vector4(150, 0, 44, 0) },
            { "bar_fill", new Vector4(15, 0, 15, 0) }, { "bar_back", new Vector4(15, 0, 15, 0) }, { "bar_grey", new Vector4(15, 0, 15, 0) },
            { "inv_bg", new Vector4(44, 44, 44, 44) },
            { "inv_empty", new Vector4(24, 0, 24, 0) }, { "inv_full", new Vector4(24, 0, 24, 0) },
            { "inv_bar_grey", new Vector4(24, 0, 24, 0) }, { "inv_bar_green", new Vector4(24, 0, 24, 0) },
            { "bubble_body", new Vector4(36, 36, 36, 36) },
            { "btn_green", new Vector4(42, 42, 42, 42) }, { "btn_grey", new Vector4(42, 42, 42, 42) },
            { "btn_yellow", new Vector4(50, 50, 50, 50) }, { "btn_cyan", new Vector4(50, 50, 50, 50) },
            { "btn_red", new Vector4(38, 38, 38, 38) },
            { "card_perk", new Vector4(44, 44, 44, 44) }, { "card_shop", new Vector4(44, 44, 44, 44) },
            { "staff_row", new Vector4(44, 44, 44, 44) },
            { "panel_blank", new Vector4(130, 130, 130, 130) },
            { "offer_bg", new Vector4(44, 44, 44, 44) },
            { "tile_bg", new Vector4(30, 30, 30, 30) }, { "tile_glow", new Vector4(40, 40, 40, 40) },
        };

        // a few pieces go by another name
        static readonly Dictionary<string, string> Alias = new Dictionary<string, string> { { "btn_green", "btn_price" } };

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static bool? on;

        /// <summary>Whether the painted UI is in the build.</summary>
        public static bool On => on ?? (on = Get("coin_bar") != null).Value;

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            var file = Alias.TryGetValue(name, out var a) ? a : name;
            var tex = Resources.Load<Texture2D>("UISkin/" + file);
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                Borders.TryGetValue(name, out var b);
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect, b);
                s.name = name;
            }
            cache[name] = s;
            return s;
        }

        /// <summary>A piece's natural size on the 1280x720 canvas.</summary>
        public static Vector2 Size(string name)
        {
            var s = Get(name);
            return s ? s.rect.size / 1.5f : Vector2.zero;
        }

        /// <summary>An image of a piece (sliced when it has borders), or the fallback sprite when the skin is missing.</summary>
        public static Image Img(Transform parent, string name, Sprite fallback = null, Color? fallbackColor = null)
        {
            var s = Get(name);
            var img = UIKit.Image(parent, s ? s : fallback, s ? Color.white : fallbackColor ?? Color.white, name);
            if (s && s.border == Vector4.zero) img.type = Image.Type.Simple;
            return img;
        }

        /// <summary>The painted button for one of the button colours the UI code uses, or null.</summary>
        public static Sprite ButtonFor(Color c)
        {
            if (!On) return null;
            if (Near(c, Pal.Parrot)) return Get("btn_green");
            if (Near(c, Pal.ButtonGrey)) return Get("btn_grey");
            if (Near(c, Pal.Marigold)) return Get("btn_yellow");
            if (Near(c, Pal.Teal)) return Get("btn_cyan");
            if (Near(c, Pal.Chili)) return Get("btn_red");
            return null;
        }

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.04f;

        /// <summary>A UI icon by the names the config uses: the painted one where the skin has it, else the generated one.</summary>
        public static Sprite Icon(string name)
        {
            string painted = null;
            switch (name)
            {
                case "rickshaw": painted = "icon_rickshaw"; break;
                case "counter": case "queue": painted = "icon_lane"; break;
                case "leaf": painted = "icon_leaf"; break;
            }
            var s = painted != null ? Get(painted) : null;
            return s != null ? s : SpriteGen.Icon(name);
        }

        /// <summary>The painted face for a staff role: the girl in the red apron for cooks, the waiter for everyone else.</summary>
        public static Sprite Face(string role) => Get(role == "cook" ? "face_girl" : "face_man");

        /// <summary>Painted food icons for the stage 1 menu (other items keep their rendered icons).</summary>
        public static Sprite Item(ItemId id)
        {
            switch (id)
            {
                case ItemId.Chai: return Get("icon_chai");
                case ItemId.Paratha: return Get("icon_paratha");
                case ItemId.Samosa: return Get("icon_samosa");
                default: return null;
            }
        }

        /// <summary>Dark ink text in the mockup style (Baloo, no outline).</summary>
        public static Text Label(Transform parent, string s, float size, Color c, TextAnchor a = TextAnchor.MiddleLeft, bool bold = true)
        {
            var t = UIKit.Text(parent, s, Mathf.RoundToInt(size), c, a, bold);
            return t;
        }

        /// <summary>White text with a dark outline, as on the painted buttons.</summary>
        public static Text ButtonLabel(Transform parent, string s, float size, float outline = 1.6f)
        {
            var t = UIKit.Text(parent, s, Mathf.RoundToInt(size), Color.white, TextAnchor.MiddleCenter, true);
            // one Outline: it already draws four offset copies. Stacking several outlines each copy the copies, and
            // the strokes merge into a solid block around the text.
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = Ink;
            o.effectDistance = new Vector2(outline, -outline);
            return t;
        }

        /// <summary>
        /// Places a child by mockup pixels: (x, y) is its top-left inside a parent drawn at 1920 scale, (w, h) its
        /// size, all divided by 1.5 for the canvas.
        /// </summary>
        public static RectTransform At(RectTransform rt, float x, float y, float w, float h) =>
            UIKit.TopLeft(rt, x / 1.5f, y / 1.5f, w / 1.5f, h / 1.5f);

        /// <summary>A plain image button (the painting is the whole button: close, pause, settings...).</summary>
        public static Button SpriteButton(Transform parent, string name, System.Action onClick, Sprite fallback = null, Color? fallbackColor = null)
        {
            var img = Img(parent, name, fallback, fallbackColor);
            img.raycastTarget = true;
            img.preserveAspect = true;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
            b.onClick.AddListener(() => { Synth.Play("click"); onClick?.Invoke(); });
            img.gameObject.AddComponent<PressFx>();
            return b;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); on = null; }
    }
}
