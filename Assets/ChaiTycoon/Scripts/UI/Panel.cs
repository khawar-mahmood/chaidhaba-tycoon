using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>
    /// Base for the small panels. The world pauses (gameplayStop) while a panel is open so no customer
    /// leaves while the player shops; closing signals a commercial break.
    /// The frame is a painted panel from the mockups (UI/Skin.cs): the blank orange frame with a live title by
    /// default, or a panel with its own painted header (Settings, Staff, Recipe book).
    /// </summary>
    public abstract class Panel : MonoBehaviour
    {
        public static int OpenCount;
        public static bool AnyOpen => OpenCount > 0;

        protected Hud hud;
        protected RectTransform frame, body;
        protected Text title;
        public bool IsOpen { get; private set; }
        protected virtual string SkinName => "panel_blank";
        /// <summary>The painted frame already says what the panel is.</summary>
        protected virtual bool TitleBaked => false;
        protected virtual Vector2 Size => Skin.On && SkinName != "panel_blank" ? Skin.Size(SkinName) : new Vector2(908, 597);
        // the blank frame's cream area has a rounded top-right corner (about 41 units round, inside a rim of 32
        // at the side and 37 on top): 50 in and 46 down keeps the whole cross on the cream with a small gap, as on
        // the Settings panel (checked by drawing the corner offline)
        /// <summary>Close button: top-right inset (x, y) and size (z) on the canvas.</summary>
        protected virtual Vector3 CloseAt => new Vector3(50, 46, 44);
        /// <summary>Body insets: left, bottom, right, top.</summary>
        protected virtual Vector4 BodyInsets => new Vector4(40, 38, 40, 118);
        protected virtual string Title => "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => OpenCount = 0;

        public static T Make<T>(Hud hud) where T : Panel
        {
            var rt = UIKit.Rect(hud.PanelLayer, typeof(T).Name);
            UIKit.Stretch(rt, 0);
            var p = rt.gameObject.AddComponent<T>();
            p.hud = hud;
            p.BuildFrame();
            p.Build();
            rt.gameObject.SetActive(false);
            return p;
        }

        void BuildFrame()
        {
            var dim = UIKit.Image(transform, null, new Color(0.12f, 0.08f, 0.14f, 0.55f), "dim");
            UIKit.Stretch(dim.rectTransform, 0);
            dim.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Close);

            var panel = Skin.Img(transform, SkinName, SpriteGen.Panel(18, 3), Pal.Cream);
            panel.raycastTarget = true; // eat clicks so they don't close
            frame = panel.rectTransform;
            UIKit.Place(frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), Size);

            title = UIKit.Text(frame, TitleBaked ? "" : Title, 48, Skin.Ink, TextAnchor.MiddleLeft);
            UIKit.TopLeft(title.rectTransform, 50, 34, 640, 64);
            title.gameObject.SetActive(!TitleBaked);

            var x = Skin.SpriteButton(frame, "btn_close", Close, SpriteGen.Icon("close"));
            var c = CloseAt;
            UIKit.TopRight((RectTransform)x.transform, c.x, c.y, c.z, c.z);

            body = UIKit.Rect(frame, "body");
            UIKit.Stretch(body, 0);
            var b = BodyInsets;
            body.offsetMin = new Vector2(b.x, b.y);
            body.offsetMax = new Vector2(-b.z, -b.w);
        }

        protected abstract void Build();
        public virtual void Refresh() { }

        public virtual void Open()
        {
            if (IsOpen) { Refresh(); return; }
            IsOpen = true;
            OpenCount++;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Game.I.PushPause();
            Refresh();
            UIKit.ScaleIn(frame);
            Synth.Play("swoosh", 1f, 0.6f);
        }

        public virtual void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            OpenCount = Mathf.Max(0, OpenCount - 1);
            UIKit.ScaleOut(frame, gameObject, () => !IsOpen);   // shrinks and fades, then deactivates
            Game.I.PopPause();
            Game.I.Hud.Refresh();
        }

        float tick;
        protected virtual void Update()
        {
            tick -= Time.unscaledDeltaTime;
            if (tick <= 0) { tick = 0.2f; LiveRefresh(); }
        }

        protected virtual void LiveRefresh() { }

        // ---------------- shared card widget (the perk card of the recipe book mockup) ----------------

        public const float CardW = 266, CardH = 232, ShopCardH = 152, CardGap = 14;

        public class Card
        {
            public RectTransform rt;
            public Image icon, bg;
            public Text name, effect, btnText;
            public Image btnImg, tvIcon;
            public Button btn;
            public RectTransform pips;
            public System.Action onBuy;
            public Image[] pipImgs;
            public string state;
        }

        /// <summary>A card: the tall perk card, or (compact) the short shop card of the mockups.</summary>
        protected Card MakeCard(Transform parent, Vector2 pos, Vector2 size, bool compact = false)
        {
            var c = new Card();
            var bg = Skin.Img(parent, compact ? "card_shop" : "card_perk", SpriteGen.Panel(16, 3));
            c.bg = bg;
            c.rt = bg.rectTransform;
            UIKit.TopLeft(c.rt, pos.x, pos.y, size.x, size.y);
            c.icon = UIKit.Image(c.rt, null, Color.white, "icon");
            c.icon.preserveAspect = true;
            if (compact) UIKit.TopLeft(c.icon.rectTransform, 22, 12, 56, 56);
            else UIKit.TopLeft(c.icon.rectTransform, 16, 22, 70, 70);
            c.name = UIKit.Text(c.rt, "", compact ? 19 : 21, Skin.Ink, TextAnchor.MiddleLeft);
            UIKit.TopLeft(c.name.rectTransform, compact ? 88 : 96, compact ? 12 : 20, size.x - (compact ? 98 : 106), 30);
            c.name.horizontalOverflow = HorizontalWrapMode.Wrap;
            c.name.resizeTextForBestFit = true; c.name.resizeTextMinSize = 14; c.name.resizeTextMaxSize = compact ? 19 : 21;
            c.name.verticalOverflow = VerticalWrapMode.Truncate;
            c.pips = UIKit.Rect(c.rt, "pips");
            UIKit.TopLeft(c.pips, compact ? 88 : 96, compact ? 44 : 56, size.x - (compact ? 98 : 106), 20);
            c.effect = UIKit.Text(c.rt, "", compact ? 16 : 17, Skin.Ink, TextAnchor.MiddleCenter, false);
            if (compact) UIKit.TopLeft(c.effect.rectTransform, 12, 68, size.x - 24, 24);
            else UIKit.TopLeft(c.effect.rectTransform, 12, 108, size.x - 24, 34);
            c.effect.horizontalOverflow = HorizontalWrapMode.Wrap;
            c.effect.resizeTextForBestFit = true; c.effect.resizeTextMinSize = 12; c.effect.resizeTextMaxSize = compact ? 16 : 17;
            c.effect.verticalOverflow = VerticalWrapMode.Truncate;
            c.btn = UIKit.Button(c.rt, Pal.Parrot, "", () => { if (c.state != "max") c.onBuy?.Invoke(); }, compact ? 22 : 24);
            c.btnImg = c.btn.GetComponent<Image>();
            c.btnText = c.btn.GetComponentInChildren<Text>();
            var brt = (RectTransform)c.btn.transform;
            if (compact) UIKit.Place(brt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(size.x - 66, 50));
            else UIKit.Place(brt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(size.x - 30, 60));
            c.tvIcon = UIKit.Image(brt, SpriteGen.Icon("tv"), Color.white);
            UIKit.Place(c.tvIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 3), new Vector2(38, 38));
            c.tvIcon.enabled = false;
            return c;
        }

        protected void SetPips(Card c, int level, int max)
        {
            if (c.pipImgs == null || c.pipImgs.Length != max)
            {
                foreach (Transform t in c.pips) Destroy(t.gameObject);
                c.pipImgs = new Image[max];
                float avail = c.pips.rect.width > 0 ? c.pips.rect.width : 160;
                float step = Mathf.Min(34, avail / Mathf.Max(1, max));
                float w = Mathf.Min(28, step - 4);
                for (int i = 0; i < max; i++)
                {
                    var p = Skin.Img(c.pips, "pip_empty", SpriteGen.Rounded(5));
                    UIKit.TopLeft(p.rectTransform, i * step, 0, w, w * 0.68f);
                    c.pipImgs[i] = p;
                }
            }
            var full = Skin.Get("pip_fill");
            var empty = Skin.Get("pip_empty");
            for (int i = 0; i < max; i++)
            {
                bool filled = i < level;
                if (full != null)
                {
                    var s = filled ? full : empty;
                    if (filled && c.pipImgs[i].sprite != s) Tw.PopIn(c.pipImgs[i].transform, 0.25f, true);
                    c.pipImgs[i].sprite = s;
                }
                else
                {
                    var col = filled ? Pal.Parrot : new Color(0.17f, 0.12f, 0.18f, 0.15f);
                    if (filled && c.pipImgs[i].color != col) Tw.PopIn(c.pipImgs[i].transform, 0.25f, true);
                    c.pipImgs[i].color = col;
                }
            }
        }

        /// <summary>Button states: buy (green), need more (grey), free with ad (yellow + TV), max (the painted Max button).</summary>
        protected void SetButton(Card c, string state, string text)
        {
            c.state = state;
            var max = Skin.Get("btn_max");
            c.btn.gameObject.SetActive(state != "max" || max != null);
            c.tvIcon.enabled = state == "ad";
            c.btnImg.preserveAspect = false;
            switch (state)
            {
                case "buy": UIKit.Paint(c.btnImg, Pal.Parrot); break;
                case "ad": UIKit.Paint(c.btnImg, Pal.Marigold); break;
                case "max":
                    if (max != null) { c.btnImg.sprite = max; c.btnImg.type = Image.Type.Simple; c.btnImg.preserveAspect = true; c.btnImg.color = Color.white; }
                    break;
                default: UIKit.Paint(c.btnImg, Pal.ButtonGrey); break;
            }
            c.btnText.text = state == "max" ? "" : text;
            c.btnText.rectTransform.offsetMin = new Vector2(state == "ad" ? 48 : 6, 6);
        }

        protected static void Shake(Transform t)
        {
            var basePos = ((RectTransform)t).anchoredPosition;
            Tw.Run(0.3f, k =>
            {
                if (t) ((RectTransform)t).anchoredPosition = basePos + new Vector2(Mathf.Sin(k * 40) * 8 * (1 - k), 0);
            }, () => { if (t) ((RectTransform)t).anchoredPosition = basePos; }, true);
            Synth.Play("thud", 1.2f, 0.6f);
        }

        protected ScrollRect MakeScroll(RectTransform parent, out RectTransform content)
        {
            var view = UIKit.Rect(parent, "scroll");
            UIKit.Stretch(view, 0);
            var img = view.gameObject.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.001f);
            img.raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30;
            content = UIKit.Rect(view, "content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 100);
            sr.content = content;
            sr.viewport = view;
            return sr;
        }
    }
}
