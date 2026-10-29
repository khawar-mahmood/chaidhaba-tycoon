using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Optional rewarded offers in a side bubble: 2x cash for 2 minutes, or a cash bag worth about 60 s
    /// of income. None in the first 2.5 minutes of play, 45 s on screen, one at a time, 3-minute cooldown. Ignoring costs nothing.
    /// Plus a "short of cash" top-up when Rafi runs out of money on an unlock tile (see NeedCash).
    /// </summary>
    public class Offers : MonoBehaviour
    {
        public string Current;
        float visibleLeft, cooldown;
        int failures;
        bool nextIsBoost = true;
        bool waiting;

        // "short of cash": standing on a tile Rafi can't finish paying for, a rewarded ad tops up exactly the gap
        public const string TopUp = "topup";
        UnlockTile topUpTile;
        double topUpAmount;
        float topUpCooldown, topUpAway;

        /// <summary>
        /// Called every frame while Rafi stands on a tile with no cash left to put in. Offers the missing amount when
        /// it's a fair top-up (at most half the tile's price, or 40), no other offer is showing, and not too soon
        /// after the last one.
        /// </summary>
        public void NeedCash(UnlockTile tile, double shortfall)
        {
            if (Current == TopUp && tile == topUpTile) { topUpAway = 0; return; }
            if (Current != null || waiting || topUpCooldown > 0 || failures >= 2) return;
            if (shortfall < 3 || shortfall > System.Math.Max(40, tile.Cfg.cost * 0.5)) return;
            topUpTile = tile;
            topUpAmount = System.Math.Ceiling(shortfall);
            topUpAway = 0;
            Current = TopUp;
            Game.I.Hud.ShowOffer("+" + Fmt.Cash(topUpAmount) + " coins", "coin");
        }

        void Update()
        {
            var g = Game.I;
            if (g == null || !g.Started || g.WorldPaused || waiting) return;
            float dt = Time.deltaTime;
            if (failures >= 2) { if (Current != null) Hide(); return; }
            topUpCooldown -= dt;
            if (Current == TopUp)
            {
                // gone once Rafi steps off (after a moment) or the tile no longer needs it
                topUpAway += dt;
                bool stillShort = topUpTile && topUpTile.ZoneActive && !topUpTile.Affordable;
                if (!stillShort || topUpAway > 2.5f) { Hide(); topUpCooldown = Mathf.Max(topUpCooldown, 15f); }
                return;
            }
            if (g.S.playSeconds < g.Eco.rules.offerFirstAt) return;

            if (Current != null)
            {
                visibleLeft -= dt;
                if (visibleLeft <= 0) { Hide(); cooldown = 60f; }
                return;
            }
            if (g.BoostLeft > 0) return;
            cooldown -= dt;
            if (cooldown > 0) return;
            Show(nextIsBoost ? "boost" : "cashbag");
        }

        void Show(string id)
        {
            var cfg = Game.I.Eco.Offer(id);
            if (cfg == null) return;
            Current = id;
            visibleLeft = Game.I.Eco.rules.offerVisible;
            Debug.Log($"[Offers] showing {id} at {Game.I.S.playSeconds:0}s of play");
            Game.I.Hud.ShowOffer(id == "boost" ? "2x cash, 2 min" : "Cash bag", cfg.icon);
            Game.I.Guide?.OfferShown();
        }

        void Hide()
        {
            Current = null;
            Game.I.Hud.HideOffer();
        }

        public void Accept()
        {
            if (Current == null || waiting) return;
            var id = Current;
            if (id == TopUp)
            {
                double amt = topUpAmount;
                Hide();
                waiting = true;
                WebBridge.RewardedBreak("small", ok =>
                {
                    waiting = false;
                    var g = Game.I;
                    if (ok)
                    {
                        failures = 0;
                        var from = g.Player.transform.position + Vector3.up * 2;
                        g.AddCash(amt, g.Player.transform.position, false);
                        g.Hud.CashFly(from, 10);
                        g.WUI.FloatText(from + Vector3.up * 0.5f, "+" + Fmt.Cash(amt), Pal.CashGold);
                        Synth.Play("coin", 1f);
                        topUpCooldown = 90f;
                    }
                    else { failures++; topUpCooldown = 30f; }
                });
                return;
            }
            var cfg = Game.I.Eco.Offer(id);
            Hide();
            waiting = true;
            WebBridge.RewardedBreak(cfg.size, ok =>
            {
                waiting = false;
                var g = Game.I;
                if (ok)
                {
                    failures = 0;
                    nextIsBoost = !nextIsBoost;
                    if (id == "boost")
                    {
                        g.BoostLeft = g.Eco.rules.boostDuration;
                        cooldown = g.Eco.rules.offerCooldown;
                        g.Hud.Toast("2x cash!", "x2");
                        Synth.Play("tada");
                    }
                    else
                    {
                        double amt = System.Math.Round(g.IncomeSeconds(cfg.value));
                        g.AddCash(amt, g.Player.transform.position, false);
                        g.Hud.CashFly(g.Player.transform.position + Vector3.up * 2, 14);
                        g.WUI.FloatText(g.Player.transform.position + Vector3.up * 2.5f, "+" + Fmt.Cash(amt), Pal.CashGold);
                        Synth.Play("coin", 1f);
                        cooldown = g.Eco.rules.offerCooldown;
                    }
                }
                else
                {
                    failures++;
                    cooldown = g.Eco.rules.offerRetryAfterFail;
                }
            });
        }
    }
}
