using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Optional rewards. 2x cash for 2 minutes lives in the Upgrades panel, for an ad or for coins, because CrazyGames
    /// doesn't allow rewarded buttons on the active gameplay screen and wants an alternative to the ad. None in the
    /// first 2.5 minutes of play, 3-minute cooldown. Plus a "short of cash" top-up bubble when Rafi runs out of money
    /// on an unlock tile (see NeedCash), the one offer that stays in play: it answers a need the moment it comes up.
    /// </summary>
    public class Offers : MonoBehaviour
    {
        public string Current;
        float cooldown;
        int failures;
        bool waiting, blockNoticed;

        // "short of cash": standing on a tile Rafi can't finish paying for, a rewarded ad tops up exactly the gap
        public const string TopUp = "topup";
        UnlockTile topUpTile;
        double topUpAmount;
        float topUpCooldown, topUpAway;

        bool AdsWork => failures < 2 && WebBridge.RewardsAvailable;

        /// <summary>The 2x card has been earned: past the first minutes of play.</summary>
        public bool BoostVisible => Game.I.S.playSeconds >= Game.I.Eco.rules.offerFirstAt;
        public bool BoostReady => Game.I.BoostLeft <= 0 && cooldown <= 0 && !waiting;
        public bool BoostAdOffered => AdsWork;
        public float BoostCooldown => Mathf.Max(0, cooldown);
        /// <summary>The coin price: half of what the boost should earn.</summary>
        public double BoostCost => System.Math.Round(Game.I.IncomeSeconds(Game.I.Eco.rules.boostCostSeconds));

        public bool BuyBoost()
        {
            if (!BoostReady || !Game.I.Spend(BoostCost)) return false;
            StartBoost();
            return true;
        }

        public void WatchForBoost(System.Action done)
        {
            if (!BoostReady || !AdsWork) return;
            waiting = true;
            WebBridge.RewardedBreak(ok =>
            {
                waiting = false;
                if (ok) { failures = 0; StartBoost(); }
                else { failures++; cooldown = Game.I.Eco.rules.offerRetryAfterFail; }
                done?.Invoke();
            });
        }

        void StartBoost()
        {
            var g = Game.I;
            g.BoostLeft = g.Eco.rules.boostDuration;
            cooldown = g.Eco.rules.offerCooldown;
            g.Hud.Toast("2x cash!", "x2");
            Synth.Play("tada");
        }

        /// <summary>
        /// Called every frame while Rafi stands on a tile with no cash left to put in. Offers the missing amount when
        /// it's a fair top-up (at most half the tile's price, or 40), no other offer is showing, and not too soon
        /// after the last one.
        /// </summary>
        public void NeedCash(UnlockTile tile, double shortfall)
        {
            if (Current == TopUp && tile == topUpTile) { topUpAway = 0; return; }
            if (Current != null || waiting || topUpCooldown > 0 || !AdsWork) return;
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
            if (g == null) return;
            if (WebBridge.AdBlocked && !blockNoticed)
            {
                blockNoticed = true;
                g.Hud.Toast("Ad blocker on: ad bonuses are off", "tv");
            }
            if (!g.Started || g.WorldPaused || waiting) return;
            float dt = Time.deltaTime;
            topUpCooldown -= dt;
            if (g.BoostLeft <= 0) cooldown -= dt;
            if (!AdsWork) { if (Current != null) Hide(); return; }
            if (Current == TopUp)
            {
                // gone once Rafi steps off (after a moment) or the tile no longer needs it
                topUpAway += dt;
                bool stillShort = topUpTile && topUpTile.ZoneActive && !topUpTile.Affordable;
                if (!stillShort || topUpAway > 2.5f) { Hide(); topUpCooldown = Mathf.Max(topUpCooldown, 15f); }
            }
        }

        void Hide()
        {
            Current = null;
            Game.I.Hud.HideOffer();
        }

        public void Accept()
        {
            if (Current != TopUp || waiting) return;
            double amt = topUpAmount;
            Hide();
            waiting = true;
            WebBridge.RewardedBreak(ok =>
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
        }
    }
}
