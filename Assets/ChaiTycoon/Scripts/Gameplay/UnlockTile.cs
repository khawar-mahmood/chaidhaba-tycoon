using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>
    /// A square pad on the floor with an icon and a price. Standing on it drains your cash into it with a
    /// filling ring; at full it builds the item with a pop. Partial payments are kept (and saved).
    /// </summary>
    public class UnlockTile : MonoBehaviour, IZone
    {
        public UnlockCfg Cfg;
        public float Size = 2f;
        public double Paid;
        public bool Locked;
        public bool Revealed;
        public bool Built;
        public Action Completed;

        Canvas canvas;
        RectTransform root;
        Image bg, ring, icon, lockImg, glow;
        Text price, label;
        float stand, coinTimer, tickTimer, bounce;
        bool wobbled;
        double Cost => Cfg.cost;
        public double Remaining => Math.Max(0, Cost - Paid);

        public static UnlockTile Create(Transform parent, UnlockCfg cfg, Vector3 pos, float size, Sprite iconSprite, string caption = null)
        {
            var go = new GameObject("tile_" + cfg.id);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var t = go.AddComponent<UnlockTile>();
            t.Cfg = cfg;
            t.Size = size;
            t.Paid = Game.I.LS.tilePaid.Get(cfg.id);
            t.BuildVisual(iconSprite, caption);
            Zones.Add(t);
            t.SetRevealed(false, false);
            return t;
        }

        /// <summary>The picture on the tile (also used by the HUD's next-unlock chip).</summary>
        public Sprite IconSprite { get; private set; }

        void BuildVisual(Sprite iconSprite, string caption)
        {
            IconSprite = iconSprite;
            var cgo = new GameObject("canvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            cgo.transform.localPosition = new Vector3(0, 0.03f, 0);
            cgo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = -10;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3;
            root = (RectTransform)cgo.transform;
            float px = Size * 100f;
            root.sizeDelta = new Vector2(px, px);
            root.localScale = Vector3.one * 0.01f;

            // the hotspot card of the mockup: green glow when affordable, the cream card, its name on top, the icon,
            // and a coin with the price; paying fills the card from the bottom
            bool skin = Skin.Get("tile_bg") != null;
            glow = skin ? UIKit.Image(root, Skin.Get("tile_glow"), new Color(1, 1, 1, 0)) : UIKit.Image(root, SpriteGen.Rounded(28), Pal.WithA(Pal.Marigold, 0.0f));
            UIKit.Stretch(glow.rectTransform, skin ? -px * 0.08f : -14);
            bg = skin ? UIKit.Image(root, Skin.Get("tile_bg"), Color.white) : UIKit.Image(root, SpriteGen.Panel(22, 6), new Color(1, 0.97f, 0.9f, 0.85f));
            UIKit.Stretch(bg.rectTransform, 0);
            ring = UIKit.Image(root, skin ? Skin.Get("tile_bg") : SpriteGen.Panel(22, 6), Pal.WithA(Pal.Marigold, 0.5f));
            UIKit.Stretch(ring.rectTransform, 0);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Vertical;
            ring.fillOrigin = (int)Image.OriginVertical.Bottom;
            ring.fillAmount = 0;

            bool named = !string.IsNullOrEmpty(caption);
            float iconSize = px * (named ? 0.42f : 0.5f);
            icon = UIKit.Image(root, iconSprite, Color.white);
            icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            icon.rectTransform.anchoredPosition = new Vector2(0, named ? px * 0.02f : px * 0.08f);
            icon.preserveAspect = true;

            lockImg = UIKit.Image(root, SpriteGen.Icon("lock"), Color.white);
            lockImg.rectTransform.sizeDelta = new Vector2(px * 0.22f, px * 0.22f);
            lockImg.rectTransform.anchoredPosition = new Vector2(px * 0.3f, px * 0.3f);

            var coin = UIKit.Image(root, Skin.Get("coin_small") ?? SpriteGen.Icon("coin"), Color.white);
            coin.preserveAspect = true;
            coin.rectTransform.sizeDelta = new Vector2(px * 0.15f, px * 0.15f);
            coin.rectTransform.anchoredPosition = new Vector2(-px * 0.16f, -px * 0.32f);
            price = UIKit.Text(root, Fmt.Cash(Remaining), Mathf.RoundToInt(px * 0.15f), Skin.Ink, TextAnchor.MiddleLeft, true);
            price.rectTransform.pivot = new Vector2(0, 0.5f);
            price.rectTransform.sizeDelta = new Vector2(px * 0.6f, px * 0.22f);
            price.rectTransform.anchoredPosition = new Vector2(-px * 0.07f, -px * 0.31f);
            price.fontStyle = FontStyle.Italic;

            if (named)
            {
                label = UIKit.Text(root, caption, Mathf.RoundToInt(px * 0.1f), Skin.Ink, TextAnchor.MiddleCenter, true);
                label.fontStyle = FontStyle.Italic;
                label.rectTransform.sizeDelta = new Vector2(px * 0.92f, px * 0.18f);
                label.rectTransform.anchoredPosition = new Vector2(0, px * 0.36f);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = Mathf.RoundToInt(px * 0.05f);
                label.resizeTextMaxSize = Mathf.RoundToInt(px * 0.1f);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
        }

        public void SetRevealed(bool on, bool animate)
        {
            Revealed = on;
            gameObject.SetActive(on && !Built);
            if (on && animate)
            {
                Tw.PopIn(root, 0.4f);
                Game.I.Fx.Puff(transform.position, 6, Pal.Dirt, 0.4f);
            }
        }

        // ---------------- IZone ----------------
        public bool ZoneActive => Revealed && !Built && gameObject.activeSelf;

        public bool Contains(Vector3 p, float extra)
        {
            var c = transform.position;
            float h = Size * 0.5f + extra;
            return Mathf.Abs(p.x - c.x) < h && Mathf.Abs(p.z - c.z) < h;
        }

        public void OnEnter(Agent who)
        {
            if (!who.IsPlayer) return;
            stand = 0;
            wobbled = false;
        }

        public void OnExit(Agent who)
        {
            if (!who.IsPlayer) return;
            if (Paid > 0) { Game.I.LS.tilePaid.Set(Cfg.id, (float)Paid); Game.I.RequestSave(); }
        }

        public void OnStay(Agent who, float dt)
        {
            if (!who.IsPlayer) return;
            var g = Game.I;
            stand += dt;
            if (stand < 0.45f) return; // walking across a tile never pays into it
            // a fractional remainder (cash and saved progress aren't whole numbers) can be paid with under 1 cash
            if (Locked || g.S.cash < Math.Min(0.999, Remaining - 1e-6))
            {
                if (!Locked) g.Offers?.NeedCash(this, Remaining);    // out of cash on the tile: offer the gap for an ad
                if (!wobbled)
                {
                    wobbled = true;
                    Wobble();
                    Synth.Play("thud", 1f, 0.7f);
                    if (Locked) g.Hud.Toast("Finish every unlock first", "lock");
                }
                return;
            }
            double rate = Cost / Math.Max(0.2, g.Up.TileFillTime);
            double chunk = Math.Min(Math.Max(rate * dt, 1), Math.Min(Remaining, Math.Floor(g.S.cash)));
            if (Remaining < 1) chunk = Math.Min(Remaining, g.S.cash);
            if (chunk <= 0) return;
            g.Spend(chunk);
            g.LS.tilePaid.Set(Cfg.id, (float)(Paid + chunk));
            Paid += chunk;

            coinTimer -= dt;
            if (coinTimer <= 0)
            {
                coinTimer = 0.07f;
                g.Fx.CoinArc(who.transform.position + Vector3.up * 1.2f, transform.position + Vector3.up * 0.1f);
            }
            tickTimer -= dt;
            if (tickTimer <= 0)
            {
                tickTimer = 0.06f;
                Synth.Play("tick", 1f + (float)(Paid / Cost) * 0.8f, 0.5f, 0.04f);
            }
            if (Paid >= Cost - 0.001)
            {
                Paid = Cost;
                Complete();
            }
        }

        void Wobble()
        {
            // the tile canvas lies flat on the ground (90 degrees about x): wobble about that, never stand it up
            var flat = Quaternion.Euler(90, 0, 0);
            Tw.Run(0.35f, k =>
            {
                if (!root) return;
                root.localRotation = flat * Quaternion.Euler(0, 0, Mathf.Sin(k * Mathf.PI * 4) * 8f * (1 - k));
            }, () => { if (root) root.localRotation = flat; });
        }

        void Complete()
        {
            Built = true;
            Game.I.LS.tilePaid.Remove(Cfg.id);
            Synth.Play("tada", 1f, 0.9f);
            Game.I.Fx.Puff(transform.position, 10, Pal.Dirt, 0.5f);
            Game.I.Fx.Confetti(transform.position + Vector3.up * 0.5f, 18);
            var r = root;
            Tw.Run(0.2f, k => { if (r) r.localScale = Vector3.one * 0.01f * (1 - k); }, () =>
            {
                gameObject.SetActive(false);
            });
            Completed?.Invoke();
        }

        void Update()
        {
            if (!Revealed || Built) return;
            var g = Game.I;
            double rem = Remaining;
            bool affordable = !Locked && g.S.cash >= rem;
            ring.fillAmount = (float)(Paid / Cost);
            price.text = Fmt.Cash(Math.Ceiling(rem));
            lockImg.enabled = Locked;
            var target = Locked ? new Color(0.78f, 0.75f, 0.72f, 0.8f) : affordable ? Color.white : new Color(0.97f, 0.95f, 0.9f, 0.85f);
            bg.color = Color.Lerp(bg.color, target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 8));
            icon.color = Locked ? new Color(0.6f, 0.6f, 0.6f, 0.75f) : Color.white;
            price.color = affordable ? Skin.Ink : Pal.Mul(Skin.Ink, 1.8f);
            float gA = affordable ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f) : 0f;
            var gc = Skin.On ? Color.white : Pal.Marigold;
            glow.color = Pal.WithA(gc, Mathf.MoveTowards(glow.color.a, gA, Time.unscaledDeltaTime * 4));
            if (affordable)
            {
                bounce += Time.unscaledDeltaTime;
                icon.rectTransform.localScale = Vector3.one * (1 + Mathf.Abs(Mathf.Sin(bounce * 4f)) * 0.12f);
            }
            else icon.rectTransform.localScale = Vector3.one;
        }

        public bool Affordable => Revealed && !Built && !Locked && Game.I.S.cash >= Remaining;

        void OnDestroy() => Zones.Remove(this);
    }
}
