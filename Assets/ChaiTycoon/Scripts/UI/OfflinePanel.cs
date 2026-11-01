using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>
    /// "Welcome back!": what the dhaba earned while the player was away. A quarter of the average earning rate
    /// over the time away, capped at 2 hours (+2 h per Long nap perk level). Collect, or watch an ad for double.
    /// </summary>
    public class OfflinePanel : Panel
    {
        protected override string Title => "Welcome back!";
        protected override Vector2 Size => new Vector2(580, 420);
        protected override Vector4 BodyInsets => new Vector4(40, 36, 40, 112);

        public const float MinAway = 120f, Share = 0.25f;
        Text away, amountText;
        double amount;
        bool paid;
        RectTransform collectRt;
        GameObject doubleBtn;
        const int ButtonFont = 24;
        static readonly Vector2 ButtonSize = new Vector2(226, 70);

        /// <summary>What the time away is worth (0 when it's too short or there's no history yet).</summary>
        public static double Earnings(double awaySeconds)
        {
            var g = Game.I;
            if (awaySeconds < MinAway || g.S.playSeconds < 60 || g.S.stats.earned <= 0) return 0;
            double rate = g.S.stats.earned / g.S.playSeconds;
            double capHours = 2 + g.Up.Perk("longNap") * 2;
            return Math.Floor(rate * Math.Min(awaySeconds, capHours * 3600) * Share);
        }

        protected override void Build()
        {
            away = UIKit.Text(body, "", 20, Skin.Soft, TextAnchor.MiddleCenter, false);
            UIKit.Place(away.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(480, 30));
            var coin = Skin.Img(body, "coin", SpriteGen.Icon("coin"));
            coin.preserveAspect = true;
            UIKit.Place(coin.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(-92, -72), new Vector2(54, 54));
            amountText = UIKit.Text(body, "", 50, Skin.Ink, TextAnchor.MiddleLeft);
            UIKit.Place(amountText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 0.5f), new Vector2(-56, -72), new Vector2(260, 60));
            // CrazyGames: the way out without an ad must match the ad button in size, font and colour
            var collect = UIKit.Button(body, Pal.Parrot, "Collect", () => Pay(1), ButtonFont);
            collectRt = (RectTransform)collect.transform;
            UIKit.Place(collectRt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-120, 4), ButtonSize);
            var dbl = UIKit.Button(body, Pal.Parrot, "2x  Watch ad", WatchForDouble, ButtonFont, SpriteGen.Icon("tv"), 36);
            UIKit.Place((RectTransform)dbl.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(120, 4), ButtonSize);
            doubleBtn = dbl.gameObject;
        }

        public void Show(double earned, double awaySeconds)
        {
            amount = earned;
            paid = false;
            Open();
            bool ads = WebBridge.RewardsAvailable;
            doubleBtn.SetActive(ads);
            UIKit.Place(collectRt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(ads ? -120 : 0, 4), ButtonSize);
            away.text = "While you were away (" + Duration(awaySeconds) + "), your dhaba earned";
            amountText.text = Fmt.Cash(amount);
        }

        static string Duration(double s)
        {
            int m = (int)(s / 60);
            if (m < 60) return m + " min";
            int h = m / 60;
            m %= 60;
            return m == 0 ? h + " h" : h + " h " + m + " min";
        }

        void WatchForDouble()
        {
            if (paid) return;
            WebBridge.RewardedBreak(ok => Pay(ok ? 2 : 1));
        }

        void Pay(int mult)
        {
            if (paid) return;
            paid = true;
            var g = Game.I;
            double total = amount * mult;
            var from = g.Player.transform.position + Vector3.up * 2f;
            g.AddCash(total, g.Player.transform.position, false);
            g.Hud.CashFly(from, 14);
            Synth.Play("coin", 1f);
            if (mult > 1) g.Hud.Toast("Doubled: +" + Fmt.Cash(total), "coin");
            Close();
        }

        public override void Close()
        {
            if (!paid) { Pay(1); return; }    // closing without choosing still pays out
            base.Close();
        }
    }
}
