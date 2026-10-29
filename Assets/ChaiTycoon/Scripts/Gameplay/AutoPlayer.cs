#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Test bot that plays like a reasonable new player (serve, collect, build, buy cheap upgrades,
    /// level tasks). Used to check pacing against the GDD's unlock timeline at high time scale.
    /// </summary>
    public class AutoPlayer : MonoBehaviour
    {
        public static AutoPlayer Inst;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Inst = null;
        public bool BuyUpgrades = true;
        public readonly StringBuilder Log = new StringBuilder();
        public string Reason => reason;
        public Vector3? Target => target;
        Vector3? target;
        string reason;
        float stuck;
        Vector3 lastPos;
        float decide;

        public static AutoPlayer Run(bool upgrades = true)
        {
            if (Inst) return Inst;
            Inst = new GameObject("AutoPlayer").AddComponent<AutoPlayer>();
            Inst.BuyUpgrades = upgrades;
            GameEvents.Unlocked += id => Inst.Note("UNLOCK " + id);
            GameEvents.Hired += r => Inst.Note("HIRE " + r);
            GameEvents.UpgradeBought += u => Inst.Note("UPGRADE " + u);
            GameEvents.LevelUp += l => Inst.Note("LEVEL UP -> " + (l + 1));
            return Inst;
        }

        public static void Stop()
        {
            if (Inst) Destroy(Inst.gameObject);
            Inst = null;
            InputReader.DebugOverride = null;
        }

        void Note(string s) => Log.AppendLine($"{Fmt.Clock(Game.I.S.playSeconds)}  {s}  (cash {Fmt.Cash(Game.I.S.cash)}, income {Game.I.IncomeRate:0.0}/s, lost {Game.I.Spawner.Lost})");

        void Update()
        {
            var g = Game.I;
            if (g == null || g.Loc == null) return;
            if (g.WorldPaused) { InputReader.DebugOverride = null; return; }
            var p = g.Player;
            decide -= Time.deltaTime;
            if (decide <= 0 || target == null)
            {
                decide = 1.5f; // commit to a target (re-decide on arrival) so the bot doesn't dither
                Decide(g, p);
            }
            if (target.HasValue)
            {
                var to = target.Value - p.transform.position;
                to.y = 0;
                var wp = Waypoint(p.transform.position, target.Value);
                var d = wp - p.transform.position; d.y = 0;
                if (to.magnitude < 0.35f) { InputReader.DebugOverride = Vector2.zero; stuck = 0; decide = Mathf.Min(decide, 0.25f); }
                else
                {
                    var dir = new Vector2(d.x, d.z).normalized;
                    // stuck against something: sidestep for a moment
                    if ((p.transform.position - lastPos).magnitude < 0.02f) stuck += Time.deltaTime; else stuck = Mathf.Max(0, stuck - Time.deltaTime);
                    if (stuck > 0.5f) dir = new Vector2(-dir.y, dir.x) + dir * 0.3f;
                    if (stuck > 1.2f) stuck = 0;
                    InputReader.DebugOverride = dir;
                }
            }
            else InputReader.DebugOverride = Vector2.zero;
            lastPos = p.transform.position;

        }

        static bool SegHits(Vector3 a, Vector3 b, Vector3 c, float hx, float hz)
        {
            for (int i = 0; i <= 20; i++)
            {
                var s = Vector3.Lerp(a, b, i / 20f);
                if (Mathf.Abs(s.x - c.x) < hx && Mathf.Abs(s.z - c.z) < hz) return true;
            }
            return false;
        }

        /// <summary>Route around dining tables along their front or back, whichever is shorter.</summary>
        Vector3? AroundTables(Vector3 from, Vector3 to)
        {
            foreach (var t in Game.I.Loc.Tables)
            {
                float hx = t.HalfW + 0.45f, hz = 1.25f + 0.4f;
                bool beside = Mathf.Abs(from.x - t.Center.x) < hx + 0.3f && Mathf.Abs(from.z - t.Center.z) < hz + 0.3f;
                // right beside it, only the table and benches themselves are in the way (no flip-flop at the edge)
                if (beside ? !SegHits(from, to, t.Center, t.HalfW + 0.25f, 1.3f) : !SegHits(from, to, t.Center, hx, hz)) continue;
                // the back lane stays clear of the counter; the front one is the open road side
                float nz = t.Center.z + 1.62f, sz = t.Center.z - 2.0f;
                float rz = Mathf.Abs(from.z - nz) + Mathf.Abs(to.z - nz) < Mathf.Abs(from.z - sz) + Mathf.Abs(to.z - sz) ? nz : sz;
                float side = from.x < t.Center.x ? -1 : 1;
                var c1 = new Vector3(t.Center.x + side * (t.HalfW + 0.8f), 0, rz);
                // on the lane: head for the corner on the target's side (stable as we pass the middle)
                if (Mathf.Abs(from.z - rz) < 0.4f)
                    return new Vector3(t.Center.x + (to.x < t.Center.x ? -1 : 1) * (t.HalfW + 0.8f), 0, rz);
                return c1;
            }
            return null;
        }

        /// <summary>Route around the counter row (four 2.4 m counters from x -6.0 and the till to 4.7, at z = Location.CounterZ).</summary>
        Vector3 Waypoint(Vector3 from, Vector3 to)
        {
            var around = AroundTables(from, to);
            if (around.HasValue) return around.Value;
            float minX = -6.05f, maxX = g_counterMaxX();
            float cz = Location.CounterZ;
            bool fromKitchen = from.z > cz + 0.55f, toKitchen = to.z > cz + 0.55f;
            // does the straight line pass through the counter (plus a body's width)?
            bool hits = false;
            for (int i = 0; i <= 24 && !hits; i++)
            {
                var s = Vector3.Lerp(from, to, i / 24f);
                hits = s.x > minX - 0.45f && s.x < maxX + 0.45f && s.z > cz - 0.85f && s.z < cz + 0.85f;
            }
            if (!hits) return to;
            if (fromKitchen == toKitchen)
            {
                // same side but hugging the counter: step away from it first
                float awayZ = fromKitchen ? cz + 1.05f : cz - 1.05f;
                if (Mathf.Abs(from.z - awayZ) > 0.3f) return new Vector3(from.x, 0, awayZ);
                return new Vector3(to.x, 0, awayZ);
            }
            float leftX = minX - 1.0f, rightX = maxX + 1.0f;
            float costL = Mathf.Abs(from.x - leftX) + Mathf.Abs(to.x - leftX);
            float costR = Mathf.Abs(from.x - rightX) + Mathf.Abs(to.x - rightX);
            float ex = costL < costR ? leftX : rightX;
            // walk to the counter's end on this side, then straight past it to the other side
            var near = new Vector3(ex, 0, fromKitchen ? 1.0f : -0.45f);
            var far = new Vector3(ex, 0, fromKitchen ? -0.45f : 1.0f);
            return Mathf.Abs(from.x - ex) < 0.5f ? far : near;
        }

        // the counter row runs from the chai counter (x -6.0) to the till's right end; gaps between unbuilt counters count as solid
        float g_counterMaxX() => 4.75f;

        void Decide(Game g, Player p)
        {
            var loc = g.Loc;
            // 1. build an affordable tile
            var tile = loc.CheapestAffordable();
            if (tile != null) { target = tile.transform.position; reason = "build " + tile.Cfg.id; return; }
            // the big goal: chip away at the expansion tile like a player saving up
            if (loc.Tiles.TryGetValue("expand", out var ex) && ex.Revealed && !ex.Built && !ex.Locked && g.S.cash >= 150)
            { target = ex.transform.position; reason = "save into expansion"; return; }
            // 2. buy a cheap upgrade
            if (BuyUpgrades && g.S.upgradesSeen) TryUpgrade(g);
            // stations only cook while we stand there: stay until the order we came for is in hand
            if (!p.Stack.IsFull)
                foreach (var st in loc.Stations)
                {
                    if (!p.IsInside(st) || Outstanding(loc, p, st.Item) <= 0) continue;
                    // a ready customer wants what we already carry and the stack is half full: go serve
                    if (p.Stack.Count * 2 >= p.Stack.capacity && AnyReadyWants(loc, p.Stack)) break;
                    target = st.PickupPos; reason = "cook " + st.Item; return;
                }
            // 3. cash piles
            var cp = loc.CounterPile;
            if (cp != null && cp.Amount > 0 && (p.Stack.Count == 0 || cp.Amount > 40 * g.Multiplier) && !loc.HasStaff("cashier")) { target = cp.ZonePos; reason = "collect"; return; }
            if (p.Stack.Count == 0)
            {
                if (loc.StandPile != null && loc.StandPile.Amount > 0) { target = loc.StandPile.ZonePos; reason = "collect stand"; return; }
                foreach (var t in loc.Tables) if (t.Pile.Amount > 0) { target = t.Pile.ZonePos; reason = "collect table"; return; }
            }
            // 4a. top up the stack for ready customers' other items first (mixed carry)
            if (!p.Stack.IsFull && p.Stack.Count > 0)
            {
                var frontNeed = new Dictionary<ItemId, int>();
                foreach (var sp in loc.AllServeTargets())
                    foreach (var c in Ready(sp))
                        foreach (var r in c.Remaining) frontNeed[r] = frontNeed.TryGetValue(r, out var n) ? n + 1 : 1;
                foreach (var it in p.Stack.items) if (frontNeed.ContainsKey(it.id)) frontNeed[it.id]--;
                foreach (var kv in frontNeed)
                {
                    if (kv.Value <= 0) continue;
                    var st = loc.BestStation(kv.Key, p.transform.position);
                    if (st != null && Zones.Flat(st.PickupPos, p.transform.position) < 6f) { target = st.PickupPos; reason = "top up " + kv.Key; return; }
                }
            }
            // 4b. hand over what's waiting on a counter tray, then serve what we carry
            // (whatever the current task is about first, like a player following the guide)
            foreach (var l in loc.Lanes)
                if (l.TrayReady && (!l.Manned || Zones.Flat(p.transform.position, l.ServePos) < 1f)) { target = l.ServePos; reason = "tray"; return; }
            if (p.Stack.Count > 0)
            {
                foreach (var sp in Prioritized(g))
                    if (sp.FrontWantsAnyOf(p.Stack)) { target = sp.ServePos; reason = "serve " + sp.ServeKind; return; }
            }
            // 4c. fetch for the task's customers before anyone else
            var focus = TaskKind(g);
            if (focus != null && !p.Stack.IsFull)
            {
                foreach (var sp in loc.AllServeTargets())
                {
                    if (sp.ServeKind != focus) continue;
                    foreach (var c in Ready(sp))
                        foreach (var r in c.Remaining)
                        {
                            if (p.Stack.Has(r)) continue;
                            var st = loc.BestStation(r, p.transform.position);
                            if (st != null) { target = st.PickupPos; reason = "fetch for " + focus; return; }
                        }
                }
            }
            // 5. pick up what's needed
            if (!p.Stack.IsFull)
            {
                Station best = null; int bestN = 0;
                foreach (var id in loc.UnlockedItems())
                {
                    int n = 0;
                    foreach (var sp in loc.AllServeTargets()) n += sp.Demand(id);
                    n -= p.Stack.CountOf(id);
                    if (n <= 0) continue;
                    var st = loc.BestStation(id, p.transform.position);
                    if (st != null && n > bestN) { best = st; bestN = n; }
                }
                if (best != null) { target = best.PickupPos; reason = "pick " + best.Item; return; }
            }
            if (p.Stack.Count > 0)
            {
                foreach (var sp in loc.AllServeTargets())
                    foreach (var it in p.Stack.items)
                        if (sp.Demand(it.id) > 0) { target = sp.ServePos; reason = "wait serve"; return; }
            }
            if (cp != null && cp.Amount > 0) { target = cp.ZonePos; reason = "collect"; return; }
            target = loc.Lanes.Count > 0 ? loc.Lanes[0].DropPos + new Vector3(0, 0, 1.2f) : loc.PlayerSpawn;
            reason = "idle";
        }

        /// <summary>How many of this item the customers still need beyond what we carry.</summary>
        static int Outstanding(Location loc, Player p, ItemId id)
        {
            int n = 0;
            foreach (var sp in loc.AllServeTargets()) n += sp.Demand(id);
            return n - p.Stack.CountOf(id);
        }

        static bool AnyReadyWants(Location loc, CarryStack s)
        {
            foreach (var sp in loc.AllServeTargets()) if (sp.FrontWantsAnyOf(s)) return true;
            return false;
        }

        static string TaskKind(Game g)
        {
            var t = g.Progression?.Task(g.Progression.GuideIndex());
            if (t == null || t.type != "serveType") return null;
            return t.target == "diner" ? "table" : t.target == "rickshaw" ? "rickshaw" : null;
        }

        static IEnumerable<IServeTarget> Prioritized(Game g)
        {
            var focus = TaskKind(g);
            if (focus != null) foreach (var s in g.Loc.AllServeTargets()) if (s.ServeKind == focus) yield return s;
            foreach (var s in g.Loc.AllServeTargets()) if (s.ServeKind != focus) yield return s;
        }

        static IEnumerable<Customer> Ready(IServeTarget s)
        {
            if (s is ServicePoint sp) { var f = sp.Front; if (f != null) yield return f; }
            else if (s is DiningTable t) foreach (var c in t.Waiting()) yield return c;
        }

        void TryUpgrade(Game g)
        {
            UpgradeCfg bu = null; string bs = null; double bc = double.MaxValue;
            foreach (var tab in new[] { "rafi", "counter", "stations", "dhaba" })
            {
                if (!g.Up.TabVisible(tab)) continue;
                foreach (var (u, sub) in g.Up.Cards(tab))
                {
                    if (g.Up.IsMax(u, sub)) continue;
                    double c = g.Up.NextCost(u, sub);
                    if (c < bc) { bc = c; bu = u; bs = sub; }
                }
            }
            // only buy if it leaves enough for the next unlock soon
            double nextTile = double.MaxValue;
            foreach (var t in g.Loc.Tiles.Values) if (t.Revealed && !t.Built && !t.Locked) nextTile = System.Math.Min(nextTile, t.Remaining);
            float share = g.Loc.AllUnlocksDone() ? 0.05f : 0.15f;
            if (bu != null && g.S.cash >= bc && (bc < nextTile * share || g.S.cash - bc > nextTile)) g.Up.Buy(bu, bs);
            // cooks (one per station, the busiest first) and extra helpers, on the same budget rule
            var loc = g.Loc;
            if (loc.CooksAvailable)
            {
                // a cook costs a rewarded ad: the bot "watches" one as soon as a stove can take a cook
                {
                    Station pick = null; int best = -1;
                    foreach (var st in loc.Stations)
                    {
                        if (!loc.CanHireCook(st)) continue;
                        int d = 0;
                        foreach (var sp in loc.AllServeTargets()) d += sp.Demand(st.Item);
                        if (d > best) { best = d; pick = st; }
                    }
                    if (pick != null) loc.HireCook(pick, true);
                }
            }
            if (loc.CanHireHelper)
            {
                double hc = loc.HelperCost;
                if (g.S.cash >= hc && g.S.cash - hc > nextTile) loc.HireHelper();
            }
        }
    }
}
#endif
