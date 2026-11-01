using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>
    /// One panel holds every upgrade, split into tabs that appear only when useful. Cards never reorder,
    /// so players learn where things are.
    /// </summary>
    public class UpgradePanel : Panel
    {
        protected override string Title => "Upgrades";
        static readonly string[] TabIds = { "rafi", "counter", "stations", "dhaba" };
        static readonly string[] TabNames = { "Rafi", "Counter", "Stations", "Dhaba" };

        readonly List<(Button btn, Image img, Image dot, string id)> tabs = new List<(Button, Image, Image, string)>();
        string current = "rafi";
        RectTransform content;
        ScrollRect scroll;
        readonly List<(Card card, UpgradeCfg u, string sub)> cards = new List<(Card, UpgradeCfg, string)>();
        string adCardKey; // one free-with-ad card per visit
        bool adUsed;
        // the 2x cash strip above the cards on every tab, the same place each time
        GameObject boostAd;
        Button boostCash;
        Image boostCashImg, boostAdImg;
        Text boostText, boostCashText;
        const float BoostRowH = 84;

        protected override void Build()
        {
            var tabRow = UIKit.Rect(body, "tabs");
            UIKit.Place(tabRow, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(900, 58));
            for (int i = 0; i < TabIds.Length; i++)
            {
                string id = TabIds[i];
                var b = UIKit.Button(tabRow, Pal.Teal, TabNames[i], () => { current = id; Refresh(); }, 26);
                UIKit.TopLeft((RectTransform)b.transform, i * 190, 0, 182, 60);
                var dot = UIKit.Dot(b.transform, new Vector2(-6, -6), 20);
                tabs.Add((b, b.GetComponent<Image>(), dot, id));
            }
            var area = UIKit.Rect(body, "area");
            UIKit.Stretch(area, 0);
            area.offsetMax = new Vector2(0, -74);
            scroll = MakeScroll(area, out content);
        }

        public void OpenAt(string tab)
        {
            current = tab;
            Open();
        }

        public override void Open()
        {
            adUsed = false;
            adCardKey = null;
            base.Open();
            scroll.verticalNormalizedPosition = 1;
        }

        public override void Refresh()
        {
            var g = Game.I;
            if (!g.Up.TabVisible(current)) current = "rafi";
            for (int i = 0; i < tabs.Count; i++)
            {
                var (btn, img, dot, id) = tabs[i];
                bool vis = g.Up.TabVisible(id);
                btn.gameObject.SetActive(vis);
                UIKit.Paint(img, id == current ? Pal.Marigold : Pal.Teal);
                dot.gameObject.SetActive(id != current && g.Up.AnyAffordable(id));
            }
            BuildCards();
        }

        void BuildCards()
        {
            foreach (Transform t in content) Destroy(t.gameObject);
            cards.Clear();
            var g = Game.I;
            var list = g.Up.Cards(current);
            const float w = CardW, h = CardH, gap = CardGap;
            float y = 0;
            int col = 0;
            string lastGroup = null;

            boostText = null;
            if (g.Offers.BoostVisible) { BuildBoostRow(w * 3 + gap * 2); y = BoostRowH + gap; }

            // pick the one card that may be offered for free with an ad: an upgrade the player can't afford yet
            if (!adUsed && adCardKey == null && g.S.playSeconds > 300 && WebBridge.RewardsAvailable)
            {
                foreach (var (u, sub) in list)
                    if (!g.Up.IsMax(u, sub) && !g.Up.CanAfford(u, sub)) { adCardKey = u.id + sub; break; }
            }

            foreach (var (u, sub) in list)
            {
                if (current == "stations" && sub != lastGroup)
                {
                    if (col != 0) { y += h + gap; col = 0; }
                    lastGroup = sub;
                    var head = UIKit.Text(content, Items.Name(Items.Parse(sub)), 24, Pal.Ink, TextAnchor.MiddleLeft);
                    UIKit.TopLeft(head.rectTransform, 6, y, 600, 34);
                    var hi = UIKit.Image(content, IconBaker.Item(Items.Parse(sub)), Color.white);
                    hi.rectTransform.anchorMin = hi.rectTransform.anchorMax = new Vector2(0, 1);
                    head.rectTransform.anchoredPosition = new Vector2(46, -y);
                    UIKit.TopLeft(hi.rectTransform, 2, y - 2, 38, 38);
                    y += 40;
                }
                var c = MakeCard(content, new Vector2(col * (w + gap), y), new Vector2(w, h));
                var uu = u; var ss = sub; var card = c;
                c.onBuy = () => Buy(card, uu, ss);
                cards.Add((c, u, sub));
                col++;
                if (col >= 3) { col = 0; y += h + gap; }
            }
            if (col != 0) y += h + gap;
            content.sizeDelta = new Vector2(0, y);
            LiveRefresh();
        }

        void BuildBoostRow(float width)
        {
            var row = Skin.Img(content, "card_shop", SpriteGen.Panel(16, 3)).rectTransform;
            UIKit.TopLeft(row, 0, 0, width, BoostRowH);
            var icon = UIKit.Image(row, SpriteGen.Icon("x2"), Color.white, "icon");
            icon.preserveAspect = true;
            UIKit.TopLeft(icon.rectTransform, 18, 14, 56, 56);
            boostText = UIKit.Text(row, "", 21, Skin.Ink, TextAnchor.MiddleLeft);
            UIKit.TopLeft(boostText.rectTransform, 88, 12, width - 88 - 410, 60);
            boostText.horizontalOverflow = HorizontalWrapMode.Wrap;
            boostText.resizeTextForBestFit = true; boostText.resizeTextMinSize = 14; boostText.resizeTextMaxSize = 21;

            boostCash = UIKit.Button(row, Pal.Parrot, "", BuyBoost, 22, SpriteGen.Icon("coin"), 34);
            UIKit.TopLeft((RectTransform)boostCash.transform, width - 400, 14, 186, 56);
            boostCashImg = boostCash.GetComponent<Image>();
            boostCashText = boostCash.GetComponentInChildren<Text>();
            var ad = UIKit.Button(row, Pal.Marigold, "Watch ad", WatchForBoost, 22, SpriteGen.Icon("tv"), 34);
            UIKit.TopLeft((RectTransform)ad.transform, width - 200, 14, 186, 56);
            boostAdImg = ad.GetComponent<Image>();
            boostAd = ad.gameObject;
        }

        void BuyBoost()
        {
            if (Game.I.Offers.BuyBoost()) { Synth.Play("buy"); LiveRefresh(); }
            else Shake(boostCash.transform);
        }

        void WatchForBoost() => Game.I.Offers.WatchForBoost(LiveRefresh);

        void RefreshBoostRow()
        {
            if (boostText == null) return;
            var g = Game.I;
            var o = g.Offers;
            bool ready = o.BoostReady;
            double price = o.BoostCost;
            boostText.text = g.BoostLeft > 0 ? "2x cash running: " + Fmt.Clock(g.BoostLeft)
                : o.BoostCooldown > 0 ? "2x cash back in " + Fmt.Clock(o.BoostCooldown)
                : WebBridge.AdBlocked ? "Double your cash for 2 minutes\nAd blocker on: ad bonuses are off"
                : "Double your cash for 2 minutes";
            boostCashText.text = Fmt.Cash(price);
            UIKit.Paint(boostCashImg, ready && g.S.cash >= price ? Pal.Parrot : Pal.ButtonGrey);
            bool ad = o.BoostAdOffered;
            if (boostAd.activeSelf != ad) boostAd.SetActive(ad);
            if (ad) UIKit.Paint(boostAdImg, ready ? Pal.Marigold : Pal.ButtonGrey);
        }

        void Buy(Card c, UpgradeCfg u, string sub)
        {
            var g = Game.I;
            if (g.Up.IsMax(u, sub)) return;
            if (adCardKey == u.id + sub && !g.Up.CanAfford(u, sub))
            {
                WebBridge.RewardedBreak(ok =>
                {
                    if (ok && g.Up.Buy(u, sub, true))
                    {
                        adUsed = true;
                        adCardKey = null;
                        Bought(c);
                    }
                    if (!WebBridge.RewardsAvailable) adCardKey = null;
                    LiveRefresh();
                });
                return;
            }
            if (g.Up.Buy(u, sub)) Bought(c);
            else Shake(c.btn.transform);
        }

        void Bought(Card c)
        {
            Synth.Play("buy");
            Tw.Punch(c.rt, 0.06f, 0.2f, true);
            Game.I.Fx.Sparkle(Game.I.Player.transform.position + Vector3.up * 1.6f, 3);
            // rebuild only when the cards themselves changed (a buy can unlock a card or a tab); rebuilt buttons
            // spring in from small, so rebuilding on every buy made all of them jump at once
            if (SameCards()) { LiveRefresh(); RefreshTabs(); }
            else Refresh();
        }

        bool SameCards()
        {
            var list = Game.I.Up.Cards(current);
            if (list.Count != cards.Count) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Item1 != cards[i].u || list[i].Item2 != cards[i].sub) return false;
            return true;
        }

        void RefreshTabs()
        {
            var g = Game.I;
            foreach (var (btn, img, dot, id) in tabs) btn.gameObject.SetActive(g.Up.TabVisible(id));
        }

        protected override void LiveRefresh()
        {
            var g = Game.I;
            RefreshBoostRow();
            foreach (var (c, u, sub) in cards)
            {
                int lvl = g.Up.Level(u.id, sub);
                c.icon.sprite = SpriteGen.Icon(u.icon);
                c.name.text = sub != null ? u.name : u.name;
                c.effect.text = g.Up.EffectText(u, sub);
                SetPips(c, lvl, u.max);
                if (lvl >= u.max) SetButton(c, "max", "MAX");
                else
                {
                    double cost = g.Up.NextCost(u, sub);
                    if (g.S.cash >= cost) SetButton(c, "buy", "Buy  " + Fmt.Cash(cost));
                    else if (adCardKey == u.id + sub) SetButton(c, "ad", "Free: watch ad");
                    else SetButton(c, "grey", Fmt.Cash(cost));
                }
            }
            for (int i = 0; i < tabs.Count; i++)
            {
                var (btn, img, dot, id) = tabs[i];
                dot.gameObject.SetActive(id != current && g.Up.AnyAffordable(id));
            }
        }

        /// <summary>Guide hook: the card's Buy button for an upgrade (switching to its tab), or null.</summary>
        public RectTransform CardButton(string upgradeId)
        {
            if (!IsOpen) return null;
            if (string.IsNullOrEmpty(upgradeId))
            {
                // any affordable card on the current tab
                foreach (var (c, u, sub) in cards) if (Game.I.Up.CanAfford(u, sub)) return (RectTransform)c.btn.transform;
                return null;
            }
            var cfg = Game.I.Eco.Upgrade(upgradeId);
            if (cfg != null && cfg.tab != current && Game.I.Up.TabVisible(cfg.tab)) { current = cfg.tab; Refresh(); }
            foreach (var (c, u, sub) in cards) if (u.id == upgradeId) return (RectTransform)c.btn.transform;
            return null;
        }

        /// <summary>Pulses a card so the eye lands on it.</summary>
        protected override void Update()
        {
            base.Update();
            if (Highlight == null) return;
            foreach (var (c, u, sub) in cards)
                if (u.id == Highlight)
                    c.bg.color = Color.Lerp(Color.white, Pal.Hex("#FFE6A0"), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
        }

        public string Highlight;
    }
}
