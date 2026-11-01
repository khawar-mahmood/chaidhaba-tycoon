using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>
    /// Tea-leaf spending: permanent perks (kept forever, every location) and cosmetics (no gameplay effect).
    /// Leaves are never sold for money and never needed to progress.
    /// </summary>
    public class RecipeBook : Panel
    {
        protected override string Title => "Recipe book";
        protected override string SkinName => "panel_recipe";
        protected override bool TitleBaked => Skin.On;
        protected override Vector3 CloseAt => new Vector3(50, 51, 63);
        protected override Vector4 BodyInsets => new Vector4(44, 38, 44, 129);
        string tab = "perks";
        RectTransform content;
        ScrollRect scroll;
        Image perksTab, shopTab;
        Button leafAd;
        readonly List<(Card c, PerkCfg p, CosmeticCfg cos)> cards = new List<(Card, PerkCfg, CosmeticCfg)>();
        float leafAdCooldownUntil = -9999;

        protected override void Build()
        {
            // the active tab is yellow, the other cyan (Perks / Shop in the mockup)
            var b1 = UIKit.Button(body, Pal.Teal, "Perks", () => { tab = "perks"; Refresh(); }, 34);
            UIKit.TopLeft((RectTransform)b1.transform, 0, 0, 238, 76);
            perksTab = b1.GetComponent<Image>();
            var b2 = UIKit.Button(body, Pal.Teal, "Shop", () => { tab = "shop"; Refresh(); }, 34);
            UIKit.TopLeft((RectTransform)b2.transform, 244, 0, 238, 76);
            shopTab = b2.GetComponent<Image>();
            leafAd = UIKit.Button(body, Pal.Marigold, "+3 leaves", WatchForLeaves, 22, SpriteGen.Icon("tv"), 34);
            UIKit.Place((RectTransform)leafAd.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -8), new Vector2(220, 60));
            var area = UIKit.Rect(body, "area");
            UIKit.Stretch(area, 0);
            area.offsetMax = new Vector2(0, -98);
            scroll = MakeScroll(area, out content);
        }

        public bool AnyAffordable()
        {
            var g = Game.I;
            if (g == null) return false;
            foreach (var p in g.Eco.perks)
            {
                if (p.stage > g.S.maxLocation) continue;
                int l = g.S.perks.Get(p.id);
                if (l < p.costs.Length && g.S.leaves >= p.costs[l]) return true;
            }
            return false;
        }

        void WatchForLeaves()
        {
            WebBridge.RewardedBreak(ok =>
            {
                if (ok)
                {
                    Game.I.AddLeaves(3);
                    leafAdCooldownUntil = Time.unscaledTime + 1200;
                }
                Refresh();
            });
        }

        public override void Refresh()
        {
            var g = Game.I;
            UIKit.Paint(perksTab, tab == "perks" ? Pal.Marigold : Pal.Teal);
            UIKit.Paint(shopTab, tab == "shop" ? Pal.Marigold : Pal.Teal);
            foreach (Transform t in content) Destroy(t.gameObject);
            cards.Clear();
            const float w = CardW, h = CardH, gap = CardGap;
            int i = 0;
            if (tab == "perks")
            {
                foreach (var p in g.Eco.perks)
                {
                    if (p.stage > g.S.maxLocation) continue;
                    var c = MakeCard(content, new Vector2((i % 3) * (w + gap), (i / 3) * (h + gap)), new Vector2(w, h));
                    var pp = p; var cc = c;
                    c.onBuy = () => BuyPerk(cc, pp);
                    cards.Add((c, p, null));
                    i++;
                }
            }
            else
            {
                foreach (var cos in g.Eco.cosmetics)
                {
                    if (cos.price <= 0 && !g.S.cosmeticsOwned.Contains(cos.id)) continue;
                    var c = MakeCard(content, new Vector2((i % 3) * (w + gap), (i / 3) * (ShopCardH + gap)), new Vector2(w, ShopCardH), true);
                    var cs = cos; var cc = c;
                    c.onBuy = () => BuyCosmetic(cc, cs);
                    cards.Add((c, null, cos));
                    i++;
                }
            }
            content.sizeDelta = new Vector2(0, Mathf.CeilToInt(i / 3f) * ((tab == "perks" ? h : ShopCardH) + gap));
            LiveRefresh();
        }

        // the next perk is within 3 leaves → offer the leaf bundle ad
        bool LeafAdAvailable()
        {
            var g = Game.I;
            if (!WebBridge.RewardsAvailable || Time.unscaledTime < leafAdCooldownUntil) return false;
            foreach (var p in g.Eco.perks)
            {
                if (p.stage > g.S.maxLocation) continue;
                int l = g.S.perks.Get(p.id);
                if (l < p.costs.Length && g.S.leaves < p.costs[l] && p.costs[l] - g.S.leaves <= 3) return true;
            }
            return false;
        }

        void BuyPerk(Card c, PerkCfg p)
        {
            var g = Game.I;
            int l = g.S.perks.Get(p.id);
            if (l >= p.costs.Length) return;
            int cost = p.costs[l];
            if (g.S.leaves < cost) { Shake(c.btn.transform); return; }
            g.S.leaves -= cost;
            g.S.perks.Set(p.id, l + 1);
            GameEvents.LeavesChanged?.Invoke();
            Synth.Play("chime");
            Tw.Punch(c.rt, 0.06f, 0.2f, true);
            g.OnStatsChanged();
            g.RequestSave();
            LiveRefresh();
        }

        void BuyCosmetic(Card c, CosmeticCfg cos)
        {
            var g = Game.I;
            bool owned = g.S.cosmeticsOwned.Contains(cos.id);
            if (!owned)
            {
                if (g.S.leaves < cos.price) { Shake(c.btn.transform); return; }
                g.S.leaves -= cos.price;
                g.S.cosmeticsOwned.Add(cos.id);
                GameEvents.LeavesChanged?.Invoke();
                Synth.Play("chime");
            }
            Wear(cos, !IsWorn(cos));
            Tw.Punch(c.rt, 0.06f, 0.2f, true);
            g.RequestSave();
            LiveRefresh();
        }

        public static bool IsWorn(CosmeticCfg cos)
        {
            var s = Game.I.S;
            switch (cos.slot)
            {
                case "cap": return s.wornCap == cos.id;
                case "kurta": return s.wornKurta == cos.id;
                case "apron": return s.wornApron == cos.id;
                default: return s.decorOn.Contains(cos.id);
            }
        }

        public static void Wear(CosmeticCfg cos, bool on)
        {
            var s = Game.I.S;
            switch (cos.slot)
            {
                case "cap": s.wornCap = on ? cos.id : ""; break;
                case "kurta": s.wornKurta = on ? cos.id : ""; break;
                case "apron": s.wornApron = on ? cos.id : ""; break;
                default:
                    if (on && !s.decorOn.Contains(cos.id)) s.decorOn.Add(cos.id);
                    if (!on) s.decorOn.Remove(cos.id);
                    Game.I.Loc?.RefreshDecor();
                    return;
            }
            Game.I.Player.ApplyCosmetics();
        }

        /// <summary>The mockup's painted icon for a perk or cosmetic, when there is one.</summary>
        static Sprite PaintedIcon(string id)
        {
            switch (id)
            {
                case "familyRecipe": return Skin.Get("icon_family_recipe");
                case "loyalCustomers": return Skin.Get("icon_smile");
                case "headStart": return Skin.Get("icon_jar");
                case "cap_embroidered": return Skin.Get("icon_cap");
                case "kurta_marigold": return Skin.Get("icon_kurta");
                case "apron_peacock": return Skin.Get("icon_apron");
                case "decor_garlands": return Skin.Get("icon_garlands");
                case "decor_menu": return Skin.Get("icon_menuboard");
                case "decor_lights": return Skin.Get("icon_lights");
                default: return null;
            }
        }

        protected override void LiveRefresh()
        {
            var g = Game.I;
            leafAd.gameObject.SetActive(LeafAdAvailable());
            foreach (var (c, p, cos) in cards)
            {
                if (p != null)
                {
                    int l = g.S.perks.Get(p.id);
                    c.icon.sprite = PaintedIcon(p.id) ?? SpriteGen.Icon(p.icon);
                    c.name.text = p.name;
                    c.effect.text = p.effect;
                    SetPips(c, l, p.costs.Length);
                    if (l >= p.costs.Length) SetButton(c, "max", "MAX");
                    else SetButton(c, g.S.leaves >= p.costs[l] ? "buy" : "grey", p.costs[l] + " leaves");
                }
                else
                {
                    bool owned = g.S.cosmeticsOwned.Contains(cos.id);
                    var painted = PaintedIcon(cos.id);
                    c.icon.sprite = painted ?? SpriteGen.Icon(cos.icon);
                    c.icon.color = painted != null ? Color.white : Pal.Lerp(Pal.Hex(cos.color), Color.white, 0.15f);
                    c.name.text = cos.name;
                    c.effect.text = cos.slot == "decor" ? "Decoration, all locations" : "For Rafi (" + cos.slot + ")";
                    SetPips(c, 0, 0);
                    if (owned) SetButton(c, "buy", IsWorn(cos) ? "Take off" : "Wear");
                    else SetButton(c, g.S.leaves >= cos.price ? "buy" : "grey", cos.price + " leaves");
                }
            }
        }
    }
}
