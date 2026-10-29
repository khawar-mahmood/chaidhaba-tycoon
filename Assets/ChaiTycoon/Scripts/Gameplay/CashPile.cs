using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>Customers pay into a pile of coins; walk over it and the coins fly into your counter.</summary>
    public class CashPile : MonoBehaviour, IZone
    {
        public double Amount;
        public Vector3 ZonePos;
        public Hotspot Hot; // floor mark that lights up while Rafi stands on it
        public Transform StackOrigin;
        readonly List<Transform> coins = new List<Transform>();
        float sparkleTimer;
        public float BaseRadius = 0.2f;

        public static CashPile Create(Transform parent, Vector3 stackPos, Vector3 zonePos)
        {
            var go = new GameObject("cashPile");
            go.transform.SetParent(parent, false);
            go.transform.position = stackPos;
            var p = go.AddComponent<CashPile>();
            p.StackOrigin = go.transform;
            p.ZonePos = zonePos;
            Zones.Add(p);
            return p;
        }

        int TargetCoins()
        {
            if (Amount <= 0) return 0;
            double unit = 5 * Game.I.Multiplier;
            return Mathf.Clamp(Mathf.CeilToInt((float)(Amount / unit)), 1, 20);
        }

        Vector3 CoinLocal(int i)
        {
            // little piles of five
            int pile = i / 5, inPile = i % 5;
            float ang = pile * 2.1f;
            var off = pile == 0 ? Vector3.zero : new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 0.22f;
            return off + new Vector3(0, inPile * 0.045f, 0);
        }

        /// <summary>Adds money (already boosted) with coins hopping from the customer.</summary>
        public void Add(double amount, Vector3 from, double tip = 0)
        {
            Amount += amount;
            Game.I.WUI.FloatText(from + Vector3.up * 2.0f, "+" + Fmt.Cash(amount - tip), Pal.CashGold, tip > 0 ? "+" + Fmt.Cash(tip) + " tip" : null);
            int target = TargetCoins();
            int spawn = 0;
            while (coins.Count < target && spawn < 5)
            {
                var c = Models.Obj("coin", Models.CoinMesh(), StackOrigin).transform;
                int idx = coins.Count;
                coins.Add(c);
                spawn++;
                var start = from + Vector3.up * 1.2f;
                c.position = start;
                float delay = spawn * 0.05f;
                Tw.Run(0.35f + delay, k =>
                {
                    if (!c) return;
                    float u = Mathf.Clamp01((k * (0.35f + delay) - delay) / 0.35f);
                    var end = StackOrigin.TransformPoint(CoinLocal(idx));
                    var p = Vector3.Lerp(start, end, u);
                    p.y += Mathf.Sin(u * Mathf.PI) * 0.8f;
                    c.position = p;
                    c.rotation = Quaternion.Euler(u * 360f, 0, 0);
                }, () => { if (c) { c.localPosition = CoinLocal(idx); c.localRotation = Quaternion.identity; } });
            }
            Synth.Play("coin", Random.Range(0.95f, 1.1f), 0.6f, 0.06f);
        }

        /// <summary>Collects everything; coins fly to the HUD.</summary>
        public void Collect(Transform toward, bool byStaff = false)
        {
            if (Amount <= 0) return;
            double amt = Amount;
            Amount = 0;
            Game.I.AddCash(amt, StackOrigin.position);
            Game.I.Hud.CashFly(StackOrigin.position + Vector3.up * 0.3f, Mathf.Clamp(coins.Count, 3, 12));
            // the coins stream into Rafi one after another, each landing with a tick a little higher than the last
            int n = 0;
            foreach (var c in coins)
            {
                if (!c) continue;
                var tr = c;
                var start = tr.position;
                tr.SetParent(null, true);
                int i = n++;
                float delay = Mathf.Min(i, 11) * 0.035f, fly = 0.3f;
                Tw.Run(fly + delay, k =>
                {
                    if (!tr) return;
                    float u = Mathf.Clamp01((k * (fly + delay) - delay) / fly);
                    var end = toward ? toward.position + Vector3.up * 1.2f : start + Vector3.up;
                    var p = Vector3.Lerp(start, end, Tw.InQuad(u));
                    p.y += Mathf.Sin(u * Mathf.PI) * 0.6f;
                    tr.position = p;
                    tr.localScale = Vector3.one * (1 - u * 0.6f);
                }, () =>
                {
                    if (tr) Destroy(tr.gameObject);
                    if (!byStaff) Synth.Play("tick", 1.1f + Mathf.Min(i, 11) * 0.06f, 0.55f, 0.015f);
                });
            }
            coins.Clear();
            Synth.Play("coin", 1.2f, byStaff ? 0.4f : 0.9f, 0.05f);
            if (!byStaff) GameEvents.CashCollected?.Invoke(amt);
        }

        void Update()
        {
            if (coins.Count >= 8)
            {
                sparkleTimer -= Time.deltaTime;
                if (sparkleTimer <= 0)
                {
                    sparkleTimer = 0.6f;
                    Game.I.Fx.Sparkle(StackOrigin.position + new Vector3(Random.Range(-0.2f, 0.2f), 0.3f, Random.Range(-0.2f, 0.2f)), 1);
                }
            }
        }

        // ---------- IZone ----------
        public bool ZoneActive => true;
        /// <summary>Extra reach on top of the magnet upgrade: walking past a pile pulls the coins in, no need to step on the mark.</summary>
        public const float PullRadius = 0.8f;
        public bool Contains(Vector3 p, float extra) => Zones.Flat(p, ZonePos) < BaseRadius + Game.I.Up.MagnetRadius + PullRadius + extra;
        public void OnEnter(Agent who) { if (who.IsPlayer) Hot?.SetOn(true); }
        public void OnExit(Agent who) { if (who.IsPlayer) Hot?.SetOn(false); }
        public void OnStay(Agent who, float dt)
        {
            if (who.IsPlayer && Amount > 0) Collect(who.transform);
        }

        void OnDestroy() => Zones.Remove(this);
    }
}
