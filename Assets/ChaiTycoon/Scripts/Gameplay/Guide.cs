using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// First-time user experience. Follows the current level task and shows exactly what to do next:
    /// a bouncing floor arrow, a one-line instruction in the guide panel, a camera nudge when the target
    /// changes, and a tapping hand on UI buttons. The first levels are fully guided; later ones only
    /// guide when the player seems stuck or taps a task. New mechanics get a one-time tip when they
    /// first appear (rickshaws, tables, staff, tea leaves, stars, offers).
    /// </summary>
    public class Guide : MonoBehaviour
    {
        const int FullyGuidedLevels = 5;

        Transform arrow;
        bool billboard;
        float t, focusTimer;
        int focusIndex = -1;
        string lastStepKey;
        float stepAge;

        class Tip
        {
            public string id, text, icon;
            public Vector3? world;
            public System.Func<RectTransform> ui;
            public float time = 4.5f;
        }
        readonly Queue<Tip> tips = new Queue<Tip>();
        Tip tip;
        float tipLeft;

        void Start()
        {
            var painted = Skin.Get("arrow");
            if (painted != null)
            {
                // the painted yellow arrow of the UI, unlit and always facing the camera: as bright as the HUD
                arrow = new GameObject("guideArrow").transform;
                var q = Models.AlphaQuad("sprite", arrow, painted.texture, Vector3.zero, new Vector2(0.95f, 1.03f), Color.white);
                q.transform.localRotation = Quaternion.Euler(-90, 0, 0);    // stand the flat quad up, its picture facing the camera
                q.sortingOrder = 50;
                billboard = true;
            }
            else
            {
                var mb = new MeshBuilder();
                mb.Prism(Vector3.zero, new[] { new Vector2(-0.22f, 0.25f), new Vector2(0.22f, 0.25f), new Vector2(0.22f, -0.05f), new Vector2(0.45f, -0.05f), new Vector2(0f, -0.55f), new Vector2(-0.45f, -0.05f), new Vector2(-0.22f, -0.05f) }, 0.16f, Pal.Marigold, Quaternion.Euler(-90, 0, 0));
                arrow = Models.Obj("guideArrow", mb, null, true).transform;
            }
            arrow.gameObject.SetActive(false);

            GameEvents.RickshawParked += c => Queue("rickshaw", "Rickshaws park by the road. Bring them their order!", "rickshaw", c.SP != null ? c.SP.DropPos : (Vector3?)null);
            GameEvents.CustomerSeated += c => Queue("table", "Guests sit at the table. Drop their food on it!", "chair", c.Table != null ? c.Table.Center : (Vector3?)null);
            GameEvents.Hired += r =>
            {
                if (r == "cashier") Queue("cashier", "Your cashier now collects counter cash for you.", "staff", null);
                if (r == "waiter" && Game.I.Loc.HelperCount <= 1)
                {
                    Queue("waiter", "Your helper serves that queue for you. Upgrade helpers here!", "staff", null, () => Game.I.Hud.StaffButtonRect);
                    Queue("cook", "Stand at a stove, then tap Hire a cook: a cook keeps it cooking on its own.", "staff", Game.I.Loc.Stations.Count > 0 ? Game.I.Loc.Stations[0].transform.position : (Vector3?)null);
                }
                if (r == "cook") Queue("cookHired", "That station now cooks by itself. Hire a cook for each station!", "staff", null);
            };
            GameEvents.StarGained += s => Queue("star", "More stars bring customers faster!", "star", null);
            GameEvents.LeavesChanged += () =>
            {
                if (Game.I.S.leaves > 0) Queue("leaf", "Tea leaves buy perks. Tap here to spend them!", "leaf", null, () => Game.I.Hud.LeafRect);
            };
        }

        void OnDestroy() { if (arrow) Destroy(arrow.gameObject); }

        /// <summary>Tapping a task in the Level card points at it for a few seconds.</summary>
        public void Focus(int taskIndex)
        {
            focusIndex = taskIndex;
            focusTimer = 5f;
            lastStepKey = null; // allow a camera nudge
        }

        public void Queue(string id, string text, string icon, Vector3? world, System.Func<RectTransform> ui = null)
        {
            var s = Game.I.S;
            if (s.tipsShown.Contains(id)) return;
            s.tipsShown.Add(id);
            tips.Enqueue(new Tip { id = id, text = text, icon = icon, world = world, ui = ui });
        }

        public void OfferShown() => Queue("offer", "Optional: watch an ad for 2x cash!", "tv", null, () => Game.I.Hud.OfferRect);

        // ------------------------------------------------------------ main loop

        void Update()
        {
            var g = Game.I;
            if (g == null || g.Loc == null || g.Progression == null || arrow == null) return;
            float dt = Time.unscaledDeltaTime;
            t += dt;
            stepAge += dt;
            if (focusTimer > 0) focusTimer -= dt;

            var prog = g.Progression;
            bool firstLevel = prog.LevelIndex == 0 && g.S.location == 1;
            // level 1: nobody walks off while you build the kettle and counter, brew and serve your first chai
            Customer.FreezePatience = firstLevel && prog.FirstOpen() >= 0 && prog.FirstOpen() < 4;
            if (!g.S.tutorialDone && prog.LevelIndex >= 1) { g.S.tutorialDone = true; g.RequestSave(); }

            Vector3? target = null;
            string hint = null;
            Sprite icon = null;
            RectTransform ui = null;

            // movement hint before the very first step
            g.Hud.SetMoveHint(!g.Started && firstLevel);

            // one-time tips take priority for a few seconds
            if (tip == null && tips.Count > 0 && !Panel.AnyOpen && !g.Hud.Level.BannerShowing)
            {
                tip = tips.Dequeue();
                tipLeft = tip.time;
                if (tip.world.HasValue) g.Cam.PanTo(tip.world.Value);
            }
            if (tip != null)
            {
                tipLeft -= Time.unscaledDeltaTime;
                hint = tip.text;
                icon = SpriteGen.Icon(tip.icon);
                target = tip.world;
                ui = tip.ui?.Invoke();
                if (tipLeft <= 0) tip = null;
            }
            else
            {
                int idx = focusTimer > 0 && focusIndex >= 0 && prog.IsActive(focusIndex) ? focusIndex : prog.GuideIndex();
                var task = prog.Task(idx);
                bool guided = prog.LevelIndex < FullyGuidedLevels && g.S.location == 1;
                bool stuck = g.Player.IdleTime > 6f;
                if (task != null && (guided || stuck || focusTimer > 0))
                {
                    Resolve(task, idx, out target, out hint, out icon, out ui);
                    // a task with its own description says what it is and why, as long as the step itself is ready
                    // (not "earn X more first"); an earn task's description always applies
                    // (with the Upgrades panel open, its own "tap Buy" step wins)
                    if (!string.IsNullOrEmpty(task.desc) && !fallback && (target.HasValue || ui != null || task.type == "earn")
                        && !(task.type == "upgrade" && g.Hud.Upgrades.IsOpen)) hint = task.desc;
                    if (!g.Started && firstLevel) hint = g.Hud != null && (InputReader.UsedTouch || WebBridge.IsMobile) ? "Drag anywhere to walk to the kettle" : "Drag anywhere, or use <color=#E0711C>WASD</color>, to walk to the kettle";
                    var key = prog.LevelIndex + ":" + idx + ":" + (target.HasValue ? Mathf.RoundToInt(target.Value.x) + "," + Mathf.RoundToInt(target.Value.z) : ui != null ? ui.name : "-");
                    if (key != lastStepKey)
                    {
                        lastStepKey = key;
                        stepAge = 0;
                        // nudge the camera toward a new target that's far from the player
                        if (target.HasValue && g.Started && Zones.Flat(target.Value, g.Player.transform.position) > 5f) g.Cam.PanTo(target.Value);
                    }
                }
            }

            // hide the arrow once the player is standing on the spot
            bool near = target.HasValue && Zones.Flat(g.Player.transform.position, target.Value) < 0.9f;
            g.Hud.SetHint(hint, icon);
            g.Hud.PointAtUI(ui);
            g.Hud.SetPointer(near ? null : target);
            if (target.HasValue && !near && ui == null && !Panel.AnyOpen)
            {
                arrow.gameObject.SetActive(true);
                float bob = Mathf.Abs(Mathf.Sin(t * 4f)) * 0.45f;
                arrow.position = target.Value + new Vector3(0, 1.9f + bob, 0);
                var cam = Camera.main;
                arrow.rotation = billboard && cam
                    ? cam.transform.rotation * Quaternion.Euler(0, 0, Mathf.Sin(t * 2.2f) * 8f)     // faces the camera, rocking a little
                    : Quaternion.Euler(0, Mathf.Sin(t * 2.2f) * 25f, 0);
                arrow.localScale = Vector3.one * (1f + 0.15f * Mathf.Clamp01(1 - stepAge * 2));
            }
            else arrow.gameObject.SetActive(false);

            if (g.Hud.Upgrades.IsOpen == false) g.Hud.Upgrades.Highlight = null;
        }

        // ------------------------------------------------------------ what to do for a task

        // set by Resolve when the step can't be done yet (short of cash, no coins or guests yet) and the hint says
        // what to do meanwhile; a task's own description waits until the step is ready
        bool fallback;

        void Resolve(TaskCfg task, int idx, out Vector3? target, out string hint, out Sprite icon, out RectTransform ui)
        {
            var g = Game.I;
            var p = g.Player;
            var loc = g.Loc;
            target = null; hint = task.text; ui = null;
            fallback = false;
            icon = LevelCard.TaskIcon(task);

            switch (task.type)
            {
                case "pick":
                {
                    var id = Items.Parse(task.target);
                    var st = loc.BestStation(id, p.transform.position);
                    if (st != null) { target = st.PickupPos; hint = Cook(st, id); }
                    break;
                }
                case "serve":
                {
                    var id = Items.Parse(task.target);
                    if (p.Stack.Has(id))
                    {
                        var to = BestTargetFor(id) ?? FirstWithDemand(id);
                        if (to != null) { target = to.ServePos; hint = $"Bring the {Items.Short(id)} to the {PlaceName(to)}"; }
                        else hint = "Wait for a customer who wants it";
                    }
                    else
                    {
                        var st = loc.BestStation(id, p.transform.position);
                        if (st != null) { target = st.PickupPos; hint = Cook(st, id); }
                    }
                    break;
                }
                case "collect":
                {
                    var pile = RichestPile();
                    if (pile != null) { target = pile.ZonePos; hint = "Walk over the coins to collect your cash"; icon = SpriteGen.Icon("coin"); }
                    else { ServeLoop(out target, out hint, out icon); fallback = true; }
                    break;
                }
                case "hire" when task.target == "cook":
                {
                    // cooks have no tile: they're hired with the card at a stove that has none yet
                    Station best = null;
                    float bestD = float.MaxValue;
                    foreach (var st in loc.Stations)
                    {
                        if (!loc.CanHireCook(st)) continue;
                        float d = Zones.Flat(p.transform.position, st.PickupPos);
                        if (d < bestD) { bestD = d; best = st; }
                    }
                    if (best == null) break;
                    // a cook costs a rewarded ad, not cash
                    target = best.PickupPos;
                    hint = $"Stand at the {StationName(best)} and tap <color=#1B6DB3>Watch ad</color> for a free cook";
                    break;
                }
                case "build":
                case "hire":
                case "expand":
                {
                    string id = task.type == "expand" ? "expand" : task.target;
                    if (!loc.Tiles.TryGetValue(id, out var tile) || !tile.Revealed || tile.Built) break;
                    if (g.S.cash + 0.5 >= tile.Remaining)
                    {
                        target = tile.transform.position;
                        hint = task.type == "hire" || tile.Cfg.kind == "staff" ? $"Stand on the tile to hire the <color=#1B6DB3>{tile.Cfg.name.ToLowerInvariant()}</color>" : $"Stand on the tile to build the <color=#1B6DB3>{tile.Cfg.name.ToLowerInvariant()}</color>";
                        if (task.type == "expand") hint = "Stand on the big tile to expand!";
                    }
                    else
                    {
                        ServeLoop(out target, out hint, out icon); fallback = true;
                        hint = $"Earn {Fmt.Cash(System.Math.Ceiling(tile.Remaining - g.S.cash))} more for the {tile.Cfg.name.ToLowerInvariant()}";
                    }
                    break;
                }
                case "upgrade":
                {
                    var up = g.Hud.Upgrades;
                    var cfg = string.IsNullOrEmpty(task.target) ? null : g.Eco.Upgrade(task.target);
                    double cost = cfg != null ? g.Up.NextCost(cfg) : CheapestUpgradeCost();
                    if (up.IsOpen)
                    {
                        ui = up.CardButton(task.target);
                        up.Highlight = task.target;
                        hint = cfg != null ? $"Tap Buy to upgrade your {cfg.name.ToLowerInvariant()}" : "Tap Buy on any upgrade";
                        if (ui == null) hint = "Close the panel and earn a bit more";
                    }
                    else if (g.S.cash >= cost)
                    {
                        ui = g.Hud.UpgradesButtonRect;
                        hint = "Tap Upgrades";
                        icon = SpriteGen.Icon("up");
                    }
                    else
                    {
                        ServeLoop(out target, out hint, out icon); fallback = true;
                        hint = $"Earn {Fmt.Cash(System.Math.Ceiling(cost - g.S.cash))} more for an upgrade";
                    }
                    break;
                }
                case "serveType":
                {
                    bool diner = task.target == "diner";
                    var list = new List<IServeTarget>();
                    if (diner) foreach (var tb in loc.Tables) list.Add(tb);
                    else foreach (var r in loc.RickshawSpots) list.Add(r);
                    if (!GuideAmong(list, diner ? "guests" : "rickshaw driver", out target, out hint))
                    {
                        ServeLoop(out target, out hint, out icon); fallback = true;
                        if (!diner) { hint = "Rickshaws are on the way. Keep serving!"; break; }
                        bool eating = false, full = true;
                        foreach (var tb in loc.Tables)
                        {
                            foreach (var s in tb.Seats) if (s.Who != null && s.Who.Eating) eating = true;
                            if (tb.FreeSeats > 0) full = false;
                        }
                        CashPile tablePile = null;
                        foreach (var tb in loc.Tables) if (tb.Pile.Amount > 0) tablePile = tb.Pile;
                        if (tablePile != null && p.Stack.Count == 0) { target = tablePile.ZonePos; hint = "Collect the cash the guests left"; icon = SpriteGen.Icon("coin"); }
                        else if (eating) hint = "Guests are eating. Serve the counter meanwhile!";
                        else if (full) hint = "Tables are full. Serve the counter meanwhile!";
                        else hint = "Guests are on their way. Keep serving!";
                    }
                    break;
                }
                default: // serveCustomers, fast, earn
                    ServeLoop(out target, out hint, out icon);
                    if (task.type == "fast") hint = "Serve customers quickly for tips!";
                    break;
            }
        }

        /// <summary>The everyday loop: serve what you carry, else fetch what someone wants, else collect cash.</summary>
        void ServeLoop(out Vector3? target, out string hint, out Sprite icon)
        {
            var g = Game.I;
            var p = g.Player;
            target = null; icon = null;
            hint = "Keep serving customers";
            // food already waiting on a counter tray for the front customer: just go stand there
            foreach (var l in g.Loc.Lanes)
            {
                var tray = l.TrayItemForFront;
                if (tray == null || l.Manned) continue;
                target = l.ServePos;
                hint = $"Stand at the counter to hand over the {Items.Short(tray.Value)} on the tray";
                icon = IconBaker.Item(tray.Value);
                return;
            }
            foreach (var s in g.Loc.AllServeTargets())
                if (s.FrontWantsAnyOf(p.Stack)) { target = s.ServePos; hint = $"Serve the {PlaceName(s)}"; return; }
            // carrying something nobody can take right now: leave it on the counter, then fetch the right order
            if (!p.Stack.IsEmpty)
            {
                ServicePoint lane = null;
                ItemId leave = default;
                foreach (var l in g.Loc.Lanes)
                {
                    var id = l.SuggestedLeave(p.Stack);
                    if (id == null) continue;
                    if (lane == null || Zones.Flat(l.ServePos, p.transform.position) < Zones.Flat(lane.ServePos, p.transform.position)) { lane = l; leave = id.Value; }
                }
                if (lane != null)
                {
                    target = lane.ServePos;
                    hint = $"Nobody here wants the {Items.Short(leave)}. Stand at the counter to leave it on the tray";
                    icon = IconBaker.Item(leave);
                    return;
                }
            }
            foreach (var s in g.Loc.AllServeTargets())
            {
                foreach (var id in g.Loc.UnlockedItems())
                {
                    if (s.Demand(id) <= 0 || p.Stack.Has(id)) continue;
                    var st = g.Loc.BestStation(id, p.transform.position);
                    if (st == null) continue;
                    target = st.PickupPos;
                    hint = Cook(st, id);
                    icon = IconBaker.Item(id);
                    return;
                }
            }
            var pile = RichestPile();
            if (pile != null) { target = pile.ZonePos; hint = "Collect your cash"; icon = SpriteGen.Icon("coin"); }
        }

        bool GuideAmong(List<IServeTarget> list, string who, out Vector3? target, out string hint)
        {
            var p = Game.I.Player;
            target = null; hint = null;
            foreach (var s in list)
                if (s.FrontWantsAnyOf(p.Stack)) { target = s.ServePos; hint = $"Bring the order to the {PlaceName(s)}"; return true; }
            foreach (var s in list)
                foreach (var id in Game.I.Loc.UnlockedItems())
                {
                    if (s.Demand(id) <= 0 || p.Stack.Has(id)) continue;
                    var st = Game.I.Loc.BestStation(id, p.transform.position);
                    if (st == null) continue;
                    target = st.PickupPos;
                    hint = $"{Cook(st, id)} for the {who}";
                    return true;
                }
            return false;
        }

        IServeTarget BestTargetFor(ItemId id)
        {
            foreach (var s in Game.I.Loc.AllServeTargets())
            {
                if (s is ServicePoint sp && sp.FrontWants(id)) return s;
                if (s is DiningTable dt) foreach (var c in dt.Waiting()) if (c.Remaining.Contains(id)) return s;
            }
            return null;
        }

        IServeTarget FirstWithDemand(ItemId id)
        {
            foreach (var s in Game.I.Loc.AllServeTargets()) if (s.Demand(id) > 0) return s;
            return Game.I.Loc.Lanes.Count > 0 ? Game.I.Loc.Lanes[0] : null;
        }

        CashPile RichestPile()
        {
            var loc = Game.I.Loc;
            CashPile best = null;
            void Check(CashPile c) { if (c != null && c.Amount > 0 && (best == null || c.Amount > best.Amount)) best = c; }
            if (!loc.HasStaff("cashier")) Check(loc.CounterPile);
            Check(loc.StandPile);
            foreach (var t in loc.Tables) Check(t.Pile);
            return best;
        }

        double CheapestUpgradeCost()
        {
            var g = Game.I;
            double best = double.MaxValue;
            foreach (var tab in new[] { "rafi", "counter", "stations", "dhaba" })
            {
                if (!g.Up.TabVisible(tab)) continue;
                foreach (var (u, sub) in g.Up.Cards(tab)) if (!g.Up.IsMax(u, sub)) best = System.Math.Min(best, g.Up.NextCost(u, sub));
            }
            return best == double.MaxValue ? 0 : best;
        }

        /// <summary>Stations only cook while someone stands on their ring.</summary>
        static string Cook(Station st, ItemId id) =>
            $"Stand at the {StationName(st)} to {(st.Type == "kettle" ? "brew" : st.Type == "fryer" ? "fry" : "cook")} {Items.Short(id)}";

        static string StationName(Station st)
        {
            switch (st.Type)
            {
                case "kettle": return "kettle";
                case "tawa": return "tawa";
                case "fryer": return "fryer";
                default: return st.Type;
            }
        }

        static string PlaceName(IServeTarget s)
        {
            switch (s.ServeKind)
            {
                case "table": return "table";
                case "rickshaw": return "rickshaw";
                default: return "counter";
            }
        }

        static Sprite TaskSprite(TaskCfg t) => LevelCard.TaskIcon(t);      // the same picture as the task card
    }
}
