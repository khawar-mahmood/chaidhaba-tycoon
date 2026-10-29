using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>
    /// The gameplay HUD. Every button lives in a corner and the middle of the screen is left to the
    /// dhaba. Positions follow the GDD wireframe on the 1280x720 reference canvas.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        Canvas canvas;
        RectTransform root, fxLayer, panelLayer, topLeft;
        const float TopLeftScale = 0.66f;     // 10% up from 0.6
        public RectTransform PanelLayer => panelLayer;

        // cash / leaves
        RectTransform cashBox, leafBox, boostBox;
        Text cashText, leafText, boostText;
        Image cashIcon, leafIcon, leafDot;
        double shownCash = -1;
        int shownLeaves = -1;

        // stars
        readonly List<Image> stars = new List<Image>();
        RectTransform starBox;

        // levels & guide
        public LevelCard Level;
        public InventoryBar Inventory;
        RectTransform hintBox, uiHand;
        Text hintBoxText; Image hintIcon; RectTransform uiHandTarget; float uiHandT;

        // buttons
        Button upgradesBtn, hrBtn, mapBtn, settingsBtn, pauseBtn, muteBtn;
        Image muteIcon;
        bool firstSaleDone;

        void ToggleMute()
        {
            var s = Game.I.S;
            bool mute = s.sfx || s.music;
            s.sfx = s.music = !mute;
            Synth.I.SfxOn = s.sfx;
            Synth.I.MusicOn = s.music;
            Game.I.RequestSave();
            RefreshMute();
            if (Settings.IsOpen) Settings.Refresh();
        }

        /// <summary>Speaker icon on the mute button: the painted sound / sound-off icon when the skin has them.</summary>
        public void RefreshMute()
        {
            if (!muteIcon) return;
            bool on = Game.I.S.sfx || Game.I.S.music;
            var painted = Skin.Get(on ? "icon_sound" : "icon_sound_off");
            muteIcon.sprite = painted != null ? painted : SpriteGen.Icon("speaker");
            muteIcon.color = painted != null || on ? Color.white : new Color(1, 1, 1, 0.4f);
        }

        /// <summary>The very first customer served: a small party, so the loop clicks.</summary>
        void OnCustomerServed(Customer c)
        {
            if (firstSaleDone || c == null) return;
            firstSaleDone = true;
            var g = Game.I;
            g.Fx.Confetti(c.transform.position + Vector3.up * 2f, 26);
            Synth.Play("tada", 1.1f, 0.8f);
            Toast("First sale!", "coin");
            Tw.Punch(cashBox, 0.15f, 0.35f, true);
        }

        /// <summary>Tap the stars: what earns the next one at this dhaba.</summary>
        void ShowStarTip()
        {
            var g = Game.I;
            if (g?.LocCfg == null) return;
            foreach (var u in g.LocCfg.unlocks)
                if (u.stars > 0 && !g.LS.unlocks.Contains(u.id))
                {
                    Toast($"Next star: build the {u.name.ToLowerInvariant()} ({Fmt.Cash(u.cost)})", "star");
                    return;
                }
            Toast(g.Loc != null && g.Loc.Stars >= 5 ? "Five stars!" : "Every star here is yours. More at the next dhaba!", "star");
        }
        Image upgradesGlow, upgradesBadgeBg, hrDot, mapDot;
        Text upgradesBadge;

        // offers
        RectTransform offerBox;
        Image offerIcon;
        Text offerLabel;
        float offerWobble;

        // toast
        RectTransform toastBox;
        Text toastText;
        Image toastIcon;
        readonly Queue<(string text, string icon, Sprite sprite)> toasts = new Queue<(string, string, Sprite)>();
        float toastTime;

        // tutorial

        RectTransform handHint, keysHint, pointer;
        float hintT;

        // joystick
        Image joyRing, joyKnob;
        float joyAlpha;

        // rotate
        RectTransform rotateHint;
        bool rotatePaused;

        public UpgradePanel Upgrades;
        public HRPanel HR;
        public RecipeBook Recipe;
        public SettingsPanel Settings;
        public PausePanel Pause;
        public OfflinePanel Offline;
        public WorldMap Map;
        public Celebration Celebrate;
        readonly Dictionary<string, Sprite> portraits = new Dictionary<string, Sprite>();

        public bool BlocksInput => Panel.AnyOpen || rotatePaused;

        public static Hud Create()
        {
            var c = UIKit.Canvas("HUD", 10);
            var h = c.gameObject.AddComponent<Hud>();
            h.canvas = c;
            h.Build();
            return h;
        }

        void Build()
        {
            root = UIKit.Rect(canvas.transform, "root");
            UIKit.Stretch(root, 0);

            // joystick sits under everything else
            joyRing = UIKit.Image(root, SpriteGen.RingSprite(160, 10), new Color(1, 1, 1, 0));
            joyRing.rectTransform.sizeDelta = new Vector2(160, 160);
            joyRing.rectTransform.anchorMin = joyRing.rectTransform.anchorMax = Vector2.zero;
            joyKnob = UIKit.Image(root, SpriteGen.Circle(72, 4), new Color(1, 1, 1, 0));
            joyKnob.rectTransform.sizeDelta = new Vector2(72, 72);
            joyKnob.rectTransform.anchorMin = joyKnob.rectTransform.anchorMax = Vector2.zero;

            // the top-left group (cash, leaves, boost, level card and tasks) at 66% of the mockup size
            topLeft = UIKit.Rect(root, "topLeft");
            UIKit.Place(topLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(6, -4), new Vector2(560, 640));
            topLeft.localScale = Vector3.one * TopLeftScale;
            BuildCash();
            BuildStars();
            BuildNext();
            Level = LevelCard.Create(topLeft, root);
            Inventory = InventoryBar.Create(root);
            BuildButtons();
            firstSaleDone = Game.I.S.stats.served > 0;
            GameEvents.CustomerServed += OnCustomerServed;
            BuildOffer();
            BuildToast();
            BuildTutorial();

            panelLayer = UIKit.Rect(root, "panels");
            UIKit.Stretch(panelLayer, 0);
            fxLayer = UIKit.Rect(root, "fx");
            UIKit.Stretch(fxLayer, 0);

            Upgrades = Panel.Make<UpgradePanel>(this);
            HR = Panel.Make<HRPanel>(this);
            Recipe = Panel.Make<RecipeBook>(this);
            Settings = Panel.Make<SettingsPanel>(this);
            Pause = Panel.Make<PausePanel>(this);
            Offline = Panel.Make<OfflinePanel>(this);
            Map = Panel.Make<WorldMap>(this);
            Celebrate = Celebration.Make(this);

            rotateHint = UIKit.Rect(root, "rotate");
            UIKit.Stretch(rotateHint, 0);
            var dim = UIKit.Image(rotateHint, null, new Color(0.1f, 0.07f, 0.12f, 0.85f));
            UIKit.Stretch(dim.rectTransform, 0);
            dim.raycastTarget = true;
            var phone = UIKit.Image(rotateHint, SpriteGen.Rounded(14), Pal.Cream);
            UIKit.Place(phone.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(90, 150));
            Tw.Run(100000f, k => { if (phone) phone.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 2f) * 45f - 45f); }, null, true);
            var rt = UIKit.OutlinedText(rotateHint, "Rotate your phone", 40, Color.white);
            UIKit.Place(rt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(600, 60));
            rotateHint.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ cash & leaves

        // the painted coin and leaf bars of the mockup. With the skin their own coin / leaf is the icon (an invisible
        // marker over it is where flying coins land, and the number bounces); without it a generated icon is drawn.
        void BuildCash()
        {
            var cb = Skin.Img(topLeft, "coin_bar", SpriteGen.Panel(18, 3), Pal.Cream);
            cashBox = cb.rectTransform;
            UIKit.TopLeft(cashBox, 27, 20, 247, 67);
            cashIcon = UIKit.Image(cashBox, SpriteGen.Icon("coin"), Skin.On ? Color.clear : Color.white, "coinIcon");
            cashIcon.preserveAspect = true;
            UIKit.Place(cashIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(33, 1), new Vector2(64, 64));
            cashText = UIKit.Text(cashBox, "0", 42, Skin.Ink, TextAnchor.MiddleLeft);
            UIKit.Stretch(cashText.rectTransform, 0);
            cashText.rectTransform.offsetMin = new Vector2(84, 2);
            cashText.rectTransform.offsetMax = new Vector2(-12, 0);
            cashText.resizeTextForBestFit = true; cashText.resizeTextMinSize = 22; cashText.resizeTextMaxSize = 42;
            cashText.horizontalOverflow = HorizontalWrapMode.Wrap;
            cashText.verticalOverflow = VerticalWrapMode.Truncate;

            var lbImg = Skin.Img(topLeft, "leaf_bar", SpriteGen.Panel(18, 3), Pal.Cream);
            leafBox = lbImg.rectTransform;
            UIKit.TopLeft(leafBox, 284, 22, 191, 63);
            leafIcon = UIKit.Image(leafBox, SpriteGen.Icon("leaf"), Skin.On ? Color.clear : Color.white, "leafIcon");
            leafIcon.preserveAspect = true;
            UIKit.Place(leafIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(31, 1), new Vector2(62, 62));
            leafText = UIKit.Text(leafBox, "0", 40, Skin.Ink, TextAnchor.MiddleLeft);
            UIKit.Stretch(leafText.rectTransform, 0);
            leafText.rectTransform.offsetMin = new Vector2(80, 2);
            var lb = leafBox.gameObject.AddComponent<Button>();
            lb.transition = Selectable.Transition.None;
            lbImg.raycastTarget = true;
            lb.onClick.AddListener(() => { Synth.Play("click"); Recipe.Open(); });
            leafBox.gameObject.AddComponent<PressFx>();
            leafDot = UIKit.Dot(leafBox, new Vector2(-10, -8));
            leafBox.gameObject.SetActive(false);

            boostBox = UIKit.PanelBox(topLeft, Pal.Parrot, 14).rectTransform.parent as RectTransform;
            UIKit.TopLeft(boostBox, 344, 112, 160, 48);
            boostText = UIKit.OutlinedText(boostBox, "2x 2:00", 26, Color.white, TextAnchor.MiddleCenter, 1.6f);
            UIKit.Stretch(boostText.rectTransform, 0);
            boostText.rectTransform.offsetMin = new Vector2(0, 6);
            boostBox.gameObject.SetActive(false);
        }

        public Vector2 CashIconCanvasPos() => CanvasPos(cashIcon.rectTransform);
        public Vector2 LeafIconCanvasPos() => CanvasPos(leafBox.gameObject.activeSelf ? leafIcon.rectTransform : cashIcon.rectTransform);

        Vector2 CanvasPos(RectTransform rt)
        {
            var world = rt.TransformPoint(rt.rect.center);
            var sp = RectTransformUtility.WorldToScreenPoint(null, world);
            return sp / canvas.scaleFactor;
        }

        /// <summary>Coins fly from a world point to the cash counter, which pulses as it ticks up.</summary>
        public void CashFly(Vector3 world, int count)
        {
            var start = Game.I.WUI.ToCanvas(world, out bool vis);
            if (!vis) start = new Vector2(640, 360);
            var end = CashIconCanvasPos();
            for (int i = 0; i < count; i++)
            {
                var img = UIKit.Image(fxLayer, SpriteGen.Icon("coin"), Color.white);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = Vector2.zero;
                img.rectTransform.sizeDelta = new Vector2(34, 34);
                var jitter = UnityEngine.Random.insideUnitCircle * 40f;
                float delay = i * 0.035f;
                var rt = img.rectTransform;
                var mid = start + jitter + new Vector2(0, 60);
                Tw.Run(0.55f + delay, k =>
                {
                    if (!rt) return;
                    float u = Mathf.Clamp01((k * (0.55f + delay) - delay) / 0.55f);
                    var a = Vector2.Lerp(start, mid, u);
                    var b = Vector2.Lerp(mid, end, u);
                    rt.anchoredPosition = Vector2.Lerp(a, b, Tw.InQuad(u));
                    rt.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.7f, u);
                }, () =>
                {
                    if (rt) Destroy(rt.gameObject);
                    Tw.Punch(Skin.On ? cashText.transform : cashIcon.transform, 0.18f, 0.18f, true);
                }, true);
            }
        }

        public void LeafFly(int n, Vector3? world)
        {
            leafBox.gameObject.SetActive(true);
            Vector2 start = new Vector2(640, 360);
            if (world.HasValue)
            {
                var s = Game.I.WUI.ToCanvas(world.Value, out bool vis);
                if (vis) start = s;
            }
            var end = LeafIconCanvasPos();
            for (int i = 0; i < Mathf.Min(n, 6); i++)
            {
                var img = UIKit.Image(fxLayer, SpriteGen.Icon("leaf"), Color.white);
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.sizeDelta = new Vector2(44, 44);
                float delay = i * 0.08f;
                var mid = start + UnityEngine.Random.insideUnitCircle * 50 + new Vector2(0, 80);
                Tw.Run(0.8f + delay, k =>
                {
                    if (!rt) return;
                    float u = Mathf.Clamp01((k * (0.8f + delay) - delay) / 0.8f);
                    rt.anchoredPosition = Vector2.Lerp(Vector2.Lerp(start, mid, u), Vector2.Lerp(mid, end, u), Tw.InOutQuad(u));
                    rt.localRotation = Quaternion.Euler(0, 0, u * 360f);
                }, () =>
                {
                    if (rt) Destroy(rt.gameObject);
                    Tw.Punch(Skin.On ? leafText.transform : leafIcon.transform, 0.25f, 0.25f, true);
                    Synth.Play("ding", 1.3f, 0.5f);
                }, true);
            }
        }

        // ------------------------------------------------------------------ stars

        // the painted star bar has five grey stars; a yellow star is laid over each one earned
        // ------------------------------------------------------------------ next unlock

        // under the star bar: the cheapest open unlock, with the cash counting towards it. Tapping it looks at the tile.
        RectTransform nextBox, nextFill;
        Image nextIcon, nextFillImg;
        Text nextName, nextCount;
        UnlockTile nextTile;
        PressFx nextFx;

        void BuildNext()
        {
            var bg = Skin.Img(root, "hint_bar", SpriteGen.Panel(16, 3), Pal.Cream);
            bg.pixelsPerUnitMultiplier = 1.6f;
            bg.raycastTarget = true;
            nextBox = bg.rectTransform;
            UIKit.Place(nextBox, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(96, -74), new Vector2(250, 46));
            var b = bg.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() =>
            {
                Synth.Play("click");
                if (nextTile) Game.I.Cam.PanTo(nextTile.transform.position);
            });
            nextFx = bg.gameObject.AddComponent<PressFx>();
            nextIcon = UIKit.Image(nextBox, null, Color.white, "icon");
            nextIcon.preserveAspect = true;
            UIKit.Place(nextIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Skin.On ? 33 : 24, 1), new Vector2(32, 32));
            nextName = UIKit.Text(nextBox, "", 15, Skin.Ink, TextAnchor.MiddleLeft);
            UIKit.TopLeft(nextName.rectTransform, 64, 3, 116, 20);
            nextName.resizeTextForBestFit = true; nextName.resizeTextMinSize = 10; nextName.resizeTextMaxSize = 15;
            nextName.horizontalOverflow = HorizontalWrapMode.Wrap;
            var back = Skin.Img(nextBox, "bar_grey", SpriteGen.Rounded(5), new Color(0.17f, 0.12f, 0.18f, 0.16f));
            UIKit.TopLeft(back.rectTransform, 64, 24, 116, 14);
            nextFillImg = Skin.Img(back.transform, "bar_fill", SpriteGen.Rounded(5), Pal.Parrot);
            nextFill = nextFillImg.rectTransform;
            nextFill.anchorMin = Vector2.zero; nextFill.anchorMax = new Vector2(0, 1);
            nextFill.offsetMin = nextFill.offsetMax = Vector2.zero;
            nextCount = UIKit.Text(nextBox, "", 15, Skin.Ink, TextAnchor.MiddleRight);
            UIKit.Place(nextCount.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(64, 30));
            nextCount.resizeTextForBestFit = true; nextCount.resizeTextMinSize = 10; nextCount.resizeTextMaxSize = 15;
            nextCount.horizontalOverflow = HorizontalWrapMode.Wrap;
            nextBox.gameObject.SetActive(false);
        }

        void RefreshNext()
        {
            var g = Game.I;
            UnlockTile best = null;
            if (g.Loc != null && !Minimal && !Panel.AnyOpen)
                foreach (var t in g.Loc.Tiles.Values)
                    if (t && t.Revealed && !t.Built && !t.Locked && (best == null || t.Remaining < best.Remaining)) best = t;
            bool on = best != null;
            if (nextBox.gameObject.activeSelf != on)
            {
                nextBox.gameObject.SetActive(on);      // its PressFx springs it in when it appears
            }
            if (!on) { nextTile = null; return; }
            if (best != nextTile) { if (nextTile) nextFx.Pop(0); nextTile = best; }   // a new target: spring in again
            double rem = System.Math.Ceiling(best.Remaining);
            float frac = rem <= 0 ? 1f : Mathf.Clamp01((float)(g.S.cash / rem));
            bool ready = best.Affordable;
            nextIcon.sprite = best.IconSprite;
            nextName.text = ready ? "Ready to build!" : best.Cfg.name;
            nextName.color = ready ? Pal.Hex("#169A45") : Skin.Ink;
            nextFill.anchorMax = new Vector2(Mathf.Max(0.06f, frac), 1);
            nextFillImg.color = ready ? Pal.Hex("#7CE08A") : Color.white;
            nextCount.text = ready ? Fmt.Cash(rem) : Fmt.Cash(System.Math.Floor(g.S.cash)) + "/" + Fmt.Cash(rem);
            nextFx.Attention = ready;
        }

        // centres of the five grey stars in the painted bar, in its pixels (measured): x, and 32 px from the top
        static readonly float[] StarX = { 59, 123, 187, 251, 315 };
        const float StarY = 32;

        void BuildStars()
        {
            var bar = Skin.Get("star_bar");
            if (bar != null)
            {
                var barImg = Skin.Img(root, "star_bar");
                starBox = barImg.rectTransform;
                // tap the stars to hear what earns the next one
                barImg.raycastTarget = true;
                var sb = barImg.gameObject.AddComponent<Button>();
                sb.transition = Selectable.Transition.None;
                sb.onClick.AddListener(() => { Synth.Play("click"); ShowStarTip(); });
                UIKit.Place(starBox, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(96, -20), Skin.Size("star_bar"));   // natural size: the stars line up by pixel
                for (int i = 0; i < 5; i++)
                {
                    // the new bar is empty: a grey star in each slot, the yellow one over it once earned
                    if (Skin.Get("star_empty") != null)
                    {
                        var e = Skin.Img(starBox, "star_empty");
                        UIKit.Place(e.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(StarX[i] / 1.5f, -StarY / 1.5f), Skin.Size("star_empty"));
                    }
                    var s = Skin.Img(starBox, "star_fill");
                    UIKit.Place(s.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(StarX[i] / 1.5f, -StarY / 1.5f), Skin.Size("star_fill"));
                    stars.Add(s);
                }
                return;
            }
            starBox = UIKit.Rect(root, "stars");
            UIKit.Place(starBox, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(260, 52));
            for (int i = 0; i < 5; i++)
            {
                var s = UIKit.Image(starBox, SpriteGen.Icon("star_empty"), Color.white, "star");
                UIKit.Place(s.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(i * 52, 0), new Vector2(50, 50));
                stars.Add(s);
            }
        }

        public void StarGained(int n)
        {
            Refresh();
            if (n >= 1 && n <= 5)
            {
                var s = stars[n - 1];
                Tw.PopIn(s.transform, 0.45f, true);
                Synth.Play("bell");
                Toast("Star gained!", "star");
            }
        }

        /// <summary>A staff member's face for icons: the painted face when the skin has it, else a render of the model.</summary>
        public Sprite StaffPortrait(string role)
        {
            var face = Skin.Face(role);
            if (face != null) return face;
            if (portraits.TryGetValue(role, out var s) && s) return s;
            var rig = Models.Character(Models.StaffSpec(role, 0), null, "portrait");
            rig.Tick(0, 0.1f);
            s = IconBaker.FromObject("staff_" + role, rig.gameObject, new Vector3(12, 180, 0), 1.0f);
            Destroy(rig.gameObject);
            portraits[role] = s;
            return s;
        }

        // ------------------------------------------------------------------ buttons

        void BuildButtons()
        {
            // pause and settings: the painted round buttons, top right
            if (Skin.On)
            {
                settingsBtn = Skin.SpriteButton(root, "btn_settings", () => Settings.Open());
                UIKit.TopRight((RectTransform)settingsBtn.transform, 23, 13, 76, 76);
                pauseBtn = Skin.SpriteButton(root, "btn_pause", () => Pause.Open());
                UIKit.TopRight((RectTransform)pauseBtn.transform, 115, 13, 76, 76);
            }
            else
            {
                settingsBtn = UIKit.IconButton(root, SpriteGen.Icon("gear"), Pal.Hex("#7A6A80"), () => Settings.Open());
                UIKit.TopRight((RectTransform)settingsBtn.transform, 18, 18, 66, 66);
            }
            // one-tap mute (sound and music together) beside pause; Settings still has the separate toggles
            muteBtn = UIKit.IconButton(root, SpriteGen.Icon("speaker"), Pal.Teal, ToggleMute, 0.7f);
            UIKit.TopRight((RectTransform)muteBtn.transform, Skin.On ? 203 : 172, Skin.On ? 21 : 18, 60, 60);
            muteIcon = muteBtn.transform.Find("icon").GetComponent<Image>();
            RefreshMute();

            mapBtn = UIKit.IconButton(root, SpriteGen.Icon("map"), Pal.Teal, () => Map.Open());
            UIKit.TopRight((RectTransform)mapBtn.transform, Skin.On ? 28 : 96, Skin.On ? 100 : 18, 66, 66);
            mapDot = UIKit.Dot(mapBtn.transform, new Vector2(-8, -8));
            mapBtn.gameObject.SetActive(false);

            // Upgrade and Staff: the painted square buttons (their labels are part of the painting), bottom right
            bool painted = Skin.Get("btn_upgrade") != null;
            var ug = UIKit.Rect(root, "upgrades");
            UIKit.BottomRight(ug, 18, 18, 124, 124);
            upgradesGlow = UIKit.Image(ug, SpriteGen.Rounded(28), Pal.WithA(Pal.Marigold, 0f));
            UIKit.Stretch(upgradesGlow.rectTransform, -10);
            if (painted) upgradesBtn = Skin.SpriteButton(ug, "btn_upgrade", () => Upgrades.Open());
            else
            {
                upgradesBtn = UIKit.IconButton(ug, SpriteGen.Icon("up"), Pal.Marigold, () => Upgrades.Open(), 0.58f);
                var lbl = UIKit.OutlinedText(upgradesBtn.transform, "Upgrades", 20, Color.white, TextAnchor.LowerCenter, 1.5f);
                UIKit.Stretch(lbl.rectTransform, 0);
                lbl.rectTransform.offsetMin = new Vector2(0, 8);
            }
            UIKit.Stretch((RectTransform)upgradesBtn.transform, 0);
            // the count of affordable upgrades on the painted red notification dot
            upgradesBadgeBg = UIKit.Image(ug, Skin.Get("dot") ?? SpriteGen.Circle(64, 4), Skin.Get("dot") != null ? Color.white : Pal.Chili);
            UIKit.Place(upgradesBadgeBg.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-10, -10), new Vector2(40, 40));
            upgradesBadge = Skin.ButtonLabel(upgradesBadgeBg.transform, "1", 20, 1.2f);
            UIKit.Stretch(upgradesBadge.rectTransform, 0);
            upgradesBadge.rectTransform.offsetMin = new Vector2(0, 2);
            ug.gameObject.SetActive(false);

            if (painted)
            {
                hrBtn = Skin.SpriteButton(root, "btn_staff", () => HR.Open());
                UIKit.BottomRight((RectTransform)hrBtn.transform, 150, 20, 132, 120);
            }
            else
            {
                hrBtn = UIKit.IconButton(root, SpriteGen.Icon("staff"), Pal.Royal, () => HR.Open(), 0.62f);
                UIKit.BottomRight((RectTransform)hrBtn.transform, 158, 28, 96, 96);
                var hl = UIKit.OutlinedText(hrBtn.transform, "Staff", 18, Color.white, TextAnchor.LowerCenter, 1.5f);
                UIKit.Stretch(hl.rectTransform, 0);
                hl.rectTransform.offsetMin = new Vector2(0, 6);
            }
            hrDot = UIKit.Dot(hrBtn.transform, new Vector2(-8, -8), 30);
            hrBtn.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ offers

        // the watch-ad box of the mockup: "2x" badge and coins, DOUBLE CASH, a clock with the duration, Watch ad
        const float OfferY = -43;
        Image offer2x, offerClock;
        Text offerTime;

        void BuildOffer()
        {
            var bg = Skin.Img(root, "offer_bg", SpriteGen.Panel(22, 3), Pal.Cream);
            offerBox = bg.rectTransform;
            UIKit.Place(offerBox, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, OfferY), new Vector2(162, 192));
            bg.raycastTarget = true;
            var b = bg.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { Synth.Play("click"); Game.I.Offers.Accept(); });
            bg.gameObject.AddComponent<PressFx>();
            offerIcon = Skin.Img(offerBox, "offer_coins", SpriteGen.Icon("bag"));
            offerIcon.preserveAspect = true;
            Skin.At(offerIcon.rectTransform, 118, 40, 100, 79);
            offer2x = Skin.Img(offerBox, "offer_2x", SpriteGen.Icon("x2"));
            offer2x.preserveAspect = true;
            Skin.At(offer2x.rectTransform, 25, 6, 115, 116);
            offerLabel = UIKit.Text(offerBox, "DOUBLE CASH", 21, Skin.Ink, TextAnchor.MiddleCenter);
            Skin.At(offerLabel.rectTransform, 6, 120, 230, 40);
            offerLabel.resizeTextForBestFit = true; offerLabel.resizeTextMinSize = 12; offerLabel.resizeTextMaxSize = 21;
            offerLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            offerClock = Skin.Img(offerBox, "offer_clock", SpriteGen.Icon("clock"));
            Skin.At(offerClock.rectTransform, 58, 158, 36, 35);
            offerTime = UIKit.Text(offerBox, "2 min", 21, Skin.Ink, TextAnchor.MiddleLeft);
            Skin.At(offerTime.rectTransform, 104, 155, 120, 40);
            var watch = Skin.Img(offerBox, "btn_watchad", SpriteGen.Button(18), Pal.Marigold);
            watch.preserveAspect = true;
            Skin.At(watch.rectTransform, 13, 200, 220, 77);
            if (!Skin.On)
            {
                var wt = UIKit.Text(watch.transform, "Watch ad", 16, Pal.Ink);
                UIKit.Stretch(wt.rectTransform, 0);
            }
            offerBox.gameObject.SetActive(false);
        }

        public void ShowOffer(string label, string icon)
        {
            // the boost offer is "2x cash, 2 min": badge, title and duration; the cash bag shows just the coins
            bool boost = label.StartsWith("2x");
            offerLabel.text = boost ? "DOUBLE CASH" : label.ToUpperInvariant();
            int comma = label.IndexOf(',');
            offerTime.text = comma >= 0 ? label.Substring(comma + 1).Trim() : "";
            offer2x.gameObject.SetActive(boost);
            offerClock.gameObject.SetActive(offerTime.text.Length > 0);
            offerTime.gameObject.SetActive(offerTime.text.Length > 0);
            if (!Skin.On) offerIcon.sprite = SpriteGen.Icon(icon);
            Skin.At(offerIcon.rectTransform, boost ? 118 : 72, boost ? 40 : 24, 100, 79);
            offerBox.gameObject.SetActive(true);
            offerBox.anchoredPosition = new Vector2(200, OfferY);
            Tw.Run(0.35f, k => { if (offerBox) offerBox.anchoredPosition = new Vector2(Mathf.Lerp(200, -20, Tw.OutBack(k)), OfferY); }, null, true);
            offerWobble = 0;
        }

        public void HideOffer()
        {
            if (!offerBox.gameObject.activeSelf) return;
            Tw.Run(0.25f, k => { if (offerBox) offerBox.anchoredPosition = new Vector2(Mathf.Lerp(-20, 200, k), OfferY); }, () => offerBox.gameObject.SetActive(false), true);
        }

        // ------------------------------------------------------------------ toast

        void BuildToast()
        {
            // the hint bar's painting: icon in the orange tile, text beside it
            var bg = Skin.Img(root, "hint_bar", SpriteGen.Panel(20, 3), Pal.Cream);
            bg.pixelsPerUnitMultiplier = 1.25f;
            toastBox = bg.rectTransform;
            UIKit.Place(toastBox, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -128), new Vector2(440, 66));   // below the next-unlock chip
            toastIcon = UIKit.Image(toastBox, SpriteGen.Icon("star"), Color.white);
            UIKit.Place(toastIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Skin.On ? 42 : 40, 1), new Vector2(44, 44));
            toastIcon.preserveAspect = true;
            toastText = UIKit.Text(toastBox, "", 26, Skin.Ink, TextAnchor.MiddleCenter);
            UIKit.Stretch(toastText.rectTransform, 0);
            toastText.rectTransform.offsetMin = new Vector2(80, 2);
            toastText.rectTransform.offsetMax = new Vector2(-16, 0);
            toastBox.gameObject.SetActive(false);
        }

        /// <summary>Toasts last 1.5 s, one at a time, at most 3 waiting (oldest dropped).</summary>
        public void Toast(string text, string icon = null, Sprite sprite = null)
        {
            if (toasts.Count >= 3) toasts.Dequeue();
            toasts.Enqueue((text, icon, sprite));
        }

        void TickToast(float dt)
        {
            if (toastTime > 0)
            {
                toastTime -= dt;
                if (toastTime <= 0) toastBox.gameObject.SetActive(false);
                return;
            }
            if (toasts.Count == 0) return;
            var (text, icon, sprite) = toasts.Dequeue();
            toastText.text = text;
            var sp = sprite != null ? sprite : icon != null ? SpriteGen.Icon(icon) : null;
            toastIcon.enabled = sp != null;
            toastIcon.sprite = sp;
            toastText.rectTransform.offsetMin = new Vector2(80, 2);
            toastBox.gameObject.SetActive(true);
            UIKit.ScaleIn(toastBox);
            toastTime = 1.5f;
        }

        // ------------------------------------------------------------------ tutorial

        void BuildTutorial()
        {
            // guide panel: bottom centre, an icon plus one short instruction
            var hb = Skin.Img(root, "hint_bar", SpriteGen.Panel(22, 3), Pal.Cream);
            hintBox = hb.rectTransform;
            UIKit.Place(hintBox, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 179), new Vector2(650, 80));
            // the icon sits in the painted orange tile (a marigold circle without the skin)
            var iconBg = UIKit.Image(hb.transform, Skin.On ? null : SpriteGen.Circle(64, 4), Skin.On ? Color.clear : Pal.Marigold, "iconBg");
            UIKit.Place(iconBg.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Skin.On ? 54 : 42, 0), new Vector2(60, 60));
            hintIcon = UIKit.Image(iconBg.transform, SpriteGen.Icon("kettle"), Color.white, "icon");
            UIKit.Stretch(hintIcon.rectTransform, Skin.On ? 0 : 6);
            hintIcon.preserveAspect = true;
            hintBoxText = UIKit.Text(hb.transform, "", 27, Skin.Ink, TextAnchor.MiddleLeft);
            UIKit.Stretch(hintBoxText.rectTransform, 0);
            hintBoxText.rectTransform.offsetMin = new Vector2(112, 2);
            hintBoxText.rectTransform.offsetMax = new Vector2(-20, 0);
            hintBoxText.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintBoxText.resizeTextForBestFit = true;
            hintBoxText.resizeTextMinSize = 16;
            hintBoxText.resizeTextMaxSize = 27;
            hintBoxText.verticalOverflow = VerticalWrapMode.Truncate;
            hintBox.gameObject.SetActive(false);

            // tapping hand that points at UI buttons (Upgrades, a card, the leaf counter…)
            uiHand = UIKit.Rect(root, "uiHand");
            uiHand.anchorMin = uiHand.anchorMax = Vector2.zero;
            uiHand.sizeDelta = new Vector2(84, 84);
            var uh = UIKit.Image(uiHand, Skin.Get("hand_point") ?? SpriteGen.Icon("hand"), Color.white);
            uh.preserveAspect = true;
            UIKit.Stretch(uh.rectTransform, 0);
            if (Skin.Get("hand_point") == null) uh.rectTransform.localRotation = Quaternion.Euler(0, 0, 25);   // the painted hand already points up-left
            uiHand.gameObject.SetActive(false);

            handHint = UIKit.Rect(root, "hand");
            UIKit.Place(handHint, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(240, 120));
            var ring = UIKit.Image(handHint, SpriteGen.RingSprite(160, 10), new Color(1, 1, 1, 0.6f));
            UIKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110));
            var hand = UIKit.Image(handHint, Skin.Get("hand_point") ?? SpriteGen.Icon("hand"), Color.white, "handImg");
            hand.preserveAspect = true;
            UIKit.Place(hand.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.3f, 0.9f), Vector2.zero, new Vector2(80, 80));
            handHint.gameObject.SetActive(false);

            keysHint = UIKit.Rect(root, "keys");
            UIKit.Place(keysHint, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 92), new Vector2(200, 130));
            string[] keys = { "W", "A", "S", "D" };
            Vector2[] kp = { new Vector2(0, 27), new Vector2(-63, -27), new Vector2(0, -27), new Vector2(62, -27) };
            for (int i = 0; i < 4; i++)
            {
                // the painted keycaps already carry their letter
                var k = Skin.Img(keysHint, "key_" + keys[i].ToLowerInvariant(), SpriteGen.Button(12), Pal.Cream);
                k.preserveAspect = true;
                UIKit.Place(k.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), kp[i], new Vector2(60, 58));
                if (Skin.On) continue;
                var t = UIKit.Text(k.transform, keys[i], 28, Pal.Ink);
                UIKit.Stretch(t.rectTransform, 0);
                t.rectTransform.offsetMin = new Vector2(0, 6);
            }
            keysHint.gameObject.SetActive(false);

            pointer = UIKit.Rect(root, "pointer");
            pointer.sizeDelta = new Vector2(64, 64);
            pointer.anchorMin = pointer.anchorMax = Vector2.zero;
            var pimg = Skin.Img(pointer, "arrow", SpriteGen.Icon("arrow_down"));
            pimg.preserveAspect = true;
            UIKit.Stretch(pimg.rectTransform, 0);
            pointer.gameObject.SetActive(false);
        }

        /// <summary>Shows the guide panel with one short instruction (null hides it).</summary>
        /// <summary>
        /// First minute of a new game, until the first customer is served: little reading. The task card and
        /// next-unlock chip stay hidden; the bouncing arrow, the pointing hand and one hint line (each first task's
        /// description) do the guiding.
        /// </summary>
        public bool Minimal
        {
            get
            {
                var g = Game.I;
                if (g == null || g.S == null) return false;
                return g.S.stats.served == 0 && (g.Progression == null || g.Progression.DisplayLevel <= 1);
            }
        }

        public void SetHint(string text, Sprite icon = null)
        {
            bool on = !string.IsNullOrEmpty(text);
            if (on && hintBoxText.text != text)
            {
                hintBoxText.text = text;
                UIKit.ScaleIn(hintBox);
                Synth.Play("swoosh", 1.3f, 0.35f, 0.4f);
            }
            if (on) hintIcon.sprite = icon != null ? icon : SpriteGen.Icon("kettle");
            if (hintBox.gameObject.activeSelf != on) hintBox.gameObject.SetActive(on);
            if (!on) hintBoxText.text = "";
        }

        public RectTransform UpgradesButtonRect => (RectTransform)upgradesBtn.transform;
        public RectTransform StaffButtonRect => (RectTransform)hrBtn.transform;
        public RectTransform LeafRect => leafBox;
        public RectTransform OfferRect => offerBox;

        /// <summary>A tapping hand over a UI element (null hides it).</summary>
        public void PointAtUI(RectTransform target)
        {
            uiHandTarget = target != null && target.gameObject.activeInHierarchy ? target : null;
            uiHand.gameObject.SetActive(uiHandTarget != null);
            if (uiHandTarget != null) uiHand.SetAsLastSibling();
        }

        void TickUIHand(float dt)
        {
            if (uiHandTarget == null) return;
            if (!uiHandTarget.gameObject.activeInHierarchy) { PointAtUI(null); return; }
            uiHandT += dt;
            var world = uiHandTarget.TransformPoint(uiHandTarget.rect.center);
            var sp = RectTransformUtility.WorldToScreenPoint(null, world) / canvas.scaleFactor;
            float tap = Mathf.Abs(Mathf.Sin(uiHandT * 5f));
            uiHand.anchoredPosition = sp + new Vector2(26 + tap * 10, -34 - tap * 12);
            uiHand.localScale = Vector3.one * (1f - tap * 0.08f);
        }

        public void SetMoveHint(bool on)
        {
            bool touch = InputReader.UsedTouch || WebBridge.IsMobile || Application.isMobilePlatform;
            handHint.gameObject.SetActive(on && touch);
            keysHint.gameObject.SetActive(on && !touch);
        }

        /// <summary>Small arrow at the screen edge toward an off-screen target.</summary>
        public void SetPointer(Vector3? world)
        {
            if (!world.HasValue) { pointer.gameObject.SetActive(false); return; }
            var cam = Camera.main;
            var vp = cam.WorldToViewportPoint(world.Value);
            bool onScreen = vp.z > 0 && vp.x > 0.04f && vp.x < 0.96f && vp.y > 0.06f && vp.y < 0.94f;
            if (onScreen) { pointer.gameObject.SetActive(false); return; }
            pointer.gameObject.SetActive(true);
            var size = root.rect.size;
            var c = new Vector2(0.5f, 0.5f);
            var d = new Vector2(vp.x, vp.y) - c;
            if (vp.z < 0) d = -d;
            float m = Mathf.Max(Mathf.Abs(d.x) / 0.44f, Mathf.Abs(d.y) / 0.42f);
            var p = c + d / Mathf.Max(m, 0.001f);
            pointer.anchoredPosition = new Vector2(p.x * size.x, p.y * size.y);
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            pointer.localRotation = Quaternion.Euler(0, 0, ang + 90);
            pointer.localScale = Vector3.one * (1 + Mathf.Sin(Time.unscaledTime * 6) * 0.08f);
        }

        public void ShowLoadingDone() { }

        // ------------------------------------------------------------------ update

        public void Refresh()
        {
            var g = Game.I;
            if (g == null || g.S == null) return;
            leafBox.gameObject.SetActive(g.S.leaves > 0 || g.S.leafHintShown);
            int st = g.Loc != null ? g.Loc.Stars : 1;
            if (Skin.On) for (int i = 0; i < 5; i++) stars[i].enabled = i < st;
            else for (int i = 0; i < 5; i++) stars[i].sprite = SpriteGen.Icon(i < st ? "star" : "star_empty");
            bool upVis = g.S.upgradesSeen;
            var ug = upgradesBtn.transform.parent.gameObject;
            if (ug.activeSelf != upVis) { ug.SetActive(upVis); if (upVis) Tw.PopIn(ug.transform, 0.4f, true); }
            bool hrVis = g.Loc != null && g.Loc.LS.staff.Count > 0;
            if (hrBtn.gameObject.activeSelf != hrVis) hrBtn.gameObject.SetActive(hrVis);   // the button springs in by itself
            mapBtn.gameObject.SetActive(g.S.maxLocation > 1 || (g.Loc != null && g.Loc.Has("expand")));
            Level?.Refresh();
        }

        float refreshTimer;

        void Update()
        {
            var g = Game.I;
            if (g == null) return;
            float dt = Time.unscaledDeltaTime;

            if (g.Player != null) Inventory?.Refresh(g.Player.Stack);

            // cash counter ticks up
            if (shownCash < 0) shownCash = g.S.cash;
            if (Math.Abs(shownCash - g.S.cash) > 0.5)
            {
                shownCash = shownCash + (g.S.cash - shownCash) * (1 - Math.Exp(-dt * 10));
                if (Math.Abs(shownCash - g.S.cash) < 1) shownCash = g.S.cash;
            }
            else shownCash = g.S.cash;
            cashText.text = Fmt.Cash(Math.Floor(shownCash + 0.0001));
            if (shownLeaves != g.S.leaves) { shownLeaves = g.S.leaves; leafText.text = g.S.leaves.ToString(); }

            // boost timer
            bool boost = g.BoostLeft > 0;
            if (boostBox.gameObject.activeSelf != boost) boostBox.gameObject.SetActive(boost);
            if (boost)
            {
                boostText.text = "2x  " + Fmt.Clock(g.BoostLeft);
                boostText.color = g.BoostLeft < 10 && Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.25f ? Pal.Marigold : Color.white;
            }

            // affordable indicators
            refreshTimer -= dt;
            if (refreshTimer <= 0)
            {
                refreshTimer = 0.25f;
                int n = 0;
                foreach (var t in new[] { "rafi", "counter", "stations", "dhaba" })
                    if (g.Up.TabVisible(t)) foreach (var (u, sub) in g.Up.Cards(t)) if (g.Up.CanAfford(u, sub)) n++;
                upgradesBadgeBg.gameObject.SetActive(n > 0);
                upgradesBadge.text = n > 9 ? "9+" : n.ToString();
                hrDot.gameObject.SetActive(HR.AnyAffordable());
                // buttons with something to buy wiggle every few seconds
                upgradesBtn.GetComponent<PressFx>().Attention = n > 0;
                hrBtn.GetComponent<PressFx>().Attention = hrDot.gameObject.activeSelf;
                leafDot.gameObject.SetActive(Recipe.AnyAffordable());
                mapDot.gameObject.SetActive(false);
                Level?.Refresh();
                RefreshNext();
            }
            float glowA = upgradesBadgeBg.gameObject.activeSelf ? 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f) : 0f;
            upgradesGlow.color = Pal.WithA(Pal.Marigold, glowA);

            // offer wobble every 10 s
            if (offerBox.gameObject.activeSelf)
            {
                offerWobble += dt;
                float w = Mathf.Repeat(offerWobble, 10f);
                offerBox.localRotation = Quaternion.Euler(0, 0, w < 0.6f ? Mathf.Sin(w * 30f) * 8f * (1 - w / 0.6f) : 0);
            }

            TickToast(dt);
            TickJoystick(dt);
            TickUIHand(dt);

            // hand hint animation
            if (handHint.gameObject.activeSelf)
            {
                hintT += dt;
                var hand = handHint.Find("handImg") as RectTransform;
                if (hand) hand.anchoredPosition = new Vector2(Mathf.Sin(hintT * 2.5f) * 50f, Mathf.Cos(hintT * 2.5f) * 18f);
            }

            // rotate hint for portrait phones
            bool portrait = Screen.height > Screen.width * 1.05f && (WebBridge.IsMobile || Application.isMobilePlatform);
            if (portrait != rotateHint.gameObject.activeSelf)
            {
                rotateHint.gameObject.SetActive(portrait);
                rotateHint.SetAsLastSibling();
                if (portrait && !rotatePaused) { rotatePaused = true; g.PushPause(); }
                else if (!portrait && rotatePaused) { rotatePaused = false; g.PopPause(false); }
            }
        }

        void TickJoystick(float dt)
        {
            bool on = InputReader.JoyActive;
            joyAlpha = Mathf.MoveTowards(joyAlpha, on ? 1 : 0, dt / 0.3f);
            if (on)
            {
                float s = canvas.scaleFactor;
                joyRing.rectTransform.anchoredPosition = InputReader.JoyOrigin / s;
                joyKnob.rectTransform.anchoredPosition = InputReader.JoyPos / s;
            }
            joyRing.color = new Color(1, 1, 1, 0.5f * joyAlpha);
            joyKnob.color = new Color(1, 1, 1, 0.65f * joyAlpha);
        }
    }
}
