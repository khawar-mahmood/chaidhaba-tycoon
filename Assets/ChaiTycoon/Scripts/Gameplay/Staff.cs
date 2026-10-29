using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Staff never fully replace the player: Rafi always moves faster and carries more. Each works one
    /// zone, marked by a ring on the floor in their cap colour.
    /// </summary>
    public class Staff : Agent
    {
        public override bool IsPlayer => false;
        public override float PickInterval => 0.3f;
        public override float ServeInterval => 0.32f;

        public string Role;
        public StaffSave Data;
        Location loc;
        Vector3 post;
        readonly List<Vector3> path = new List<Vector3>();
        Action onArrive;
        float speed, actTimer, idleTimer, pickWait;
        bool walkingIn;
        Transform ring;
        MeshRenderer ringRenderer;
        WorldUI.Zzz zzz;
        IZone current;

        enum WS { Idle, ToStation, Picking, ToLane, Serving, ToTray }
        WS ws = WS.Idle;
        Station station;
        IServeTarget lane;

        public static Staff Create(Location loc, string role, StaffSave data, Vector3 post, bool walkIn)
        {
            var go = new GameObject("staff_" + role);
            var s = go.AddComponent<Staff>();
            s.loc = loc;
            s.Role = role;
            s.Data = data;
            s.post = post;
            go.transform.SetParent(loc.transform, false);
            s.Rig = Models.Character(Models.StaffSpec(role, loc.StaffList.Count), go.transform, "rig");
            s.Stack = go.AddComponent<CarryStack>();
            s.Stack.anchor = s.Rig.carryAnchor;
            s.Stack.rig = s.Rig;
            s.RefreshStats();

            var ringTex = SpriteGen.RingSprite(128, 14).texture;
            s.ringRenderer = Models.AlphaQuad("staffRing", loc.transform, ringTex, Vector3.zero, new Vector2(1.2f, 1.2f), Pal.WithA(Models.StaffCapColor(role), 0.5f), false);
            s.ring = s.ringRenderer.transform;
            s.zzz = Game.I.WUI.AddZzz(s.Rig.headTop);

            if (walkIn)
            {
                // come in from the road on the entrance's side, not from the far end of the map
                var start = new Vector3(Mathf.Sign(loc.StaffEntry.x) * Mathf.Min(loc.SpawnX, Mathf.Abs(loc.StaffEntry.x) + 4f), 0, loc.SidewalkZ);
                go.transform.position = start;
                s.walkingIn = true;
                var entry = loc.StaffEntry;
                s.Go(() =>
                {
                    s.walkingIn = false;
                    s.transform.rotation = Quaternion.LookRotation(Vector3.back);
                    s.Rig.Wave();
                    Synth.Play("whistle", 1f, 0.6f);
                }, role == "cook" ? loc.CookRoute(post) : new[] { entry, loc.StaffInside, post });
            }
            else go.transform.position = post;
            if (role == "cook") { s.station = loc.StationById(data.assigned); if (s.station != null) s.station.Cook = s; }
            return s;
        }

        public void RefreshStats()
        {
            var up = Game.I.Up;
            speed = up.StaffSpeed(Data);
            Stack.capacity = up.StaffCarry(Data);
        }

        /// <summary>
        /// A helper's job: "lane0".."lane3" (one counter queue), "rickshaw" (the rickshaw spots) or "tables" (dine-in).
        /// Older saves' helpers have no job (Auto: every queue and table, carrying whatever's needed most).
        /// </summary>
        public bool HasJob => Data.assigned == "rickshaw" || Data.assigned == "tables" || (Data.assigned != null && Data.assigned.StartsWith("lane"));

        /// <summary>Empty assignment = Auto: each trip carries whatever the waiting customers need most.</summary>
        public bool Auto => !HasJob && (string.IsNullOrEmpty(Data.assigned) || Data.assigned == "Auto");

        public ItemId Assigned
        {
            get
            {
                if (!Auto && !HasJob) return Items.Parse(Data.assigned);
                return tripItem;
            }
        }

        ItemId tripItem = ItemId.Chai;

        /// <summary>Where this helper serves: its queue, the rickshaw spots, the tables, or (no job) all queues and tables.</summary>
        IEnumerable<IServeTarget> Targets()
        {
            var job = Data.assigned;
            if (job == "rickshaw") { foreach (var r in loc.RickshawSpots) yield return r; yield break; }
            if (job == "tables") { foreach (var t in loc.Tables) yield return t; yield break; }
            if (job != null && job.StartsWith("lane") && int.TryParse(job.Substring(4), out int i))
            {
                var l = loc.LaneByIndex(i);
                if (l != null) yield return l;
                yield break;
            }
            foreach (var t in loc.WaiterTargets()) yield return t;
        }

        int TargetDemand(ItemId item)
        {
            int n = 0;
            foreach (var t in Targets()) n += t.Demand(item);
            return n;
        }

        IServeTarget FrontWanting(CarryStack s)
        {
            foreach (var t in Targets()) if (t.FrontWantsAnyOf(s)) return t;
            return null;
        }

        IServeTarget TargetWanting(CarryStack s)
        {
            foreach (var t in Targets()) if (t.FrontWantsAnyOf(s)) return t;
            foreach (var t in Targets())
                foreach (var it in s.items)
                    if (t.Demand(it.id) > 0) return t;
            return null;
        }

        /// <summary>Customers ready to be served count most, then anyone else waiting (within this helper's job).</summary>
        ItemId BusiestItem()
        {
            // a single-item queue: always that item
            if (Data.assigned != null && Data.assigned.StartsWith("lane") && int.TryParse(Data.assigned.Substring(4), out int li))
            {
                var lane = loc.LaneByIndex(li);
                if (lane != null && lane.Only.HasValue) return lane.Only.Value;
            }
            ItemId best = tripItem;
            float bestScore = 0;
            foreach (var id in loc.UnlockedItems())
            {
                float score = 0;
                foreach (var t in Targets())
                {
                    if (t is ServicePoint sp) { var f = sp.Front; if (f != null) foreach (var r in f.Remaining) if (r == id) score += 3; }
                    else if (t is DiningTable dt) foreach (var c in dt.Waiting()) foreach (var r in c.Remaining) if (r == id) score += 3;
                    score += t.Demand(id);
                }
                var st = loc.BestStation(id, transform.position, this);
                if (st != null && (st.Tray.Count > 0 || st.Cooking)) score *= 1.5f;
                if (score > bestScore) { bestScore = score; best = id; }
            }
            return best;
        }

        /// <summary>What the Staff panel says this helper does.</summary>
        public string AssignmentLabel
        {
            get
            {
                var job = Data.assigned;
                if (job == "rickshaw") return "the rickshaws";
                if (job == "tables") return "the dining tables";
                if (job != null && job.StartsWith("lane") && int.TryParse(job.Substring(4), out int i))
                    return i == 0 ? "the chai queue" : i == 1 ? "the paratha queue" : i == 2 ? "the samosa queue" : "the mixed queue";
                return Auto ? "Auto" : Items.Name(Assigned);
            }
        }

        public void CycleAssignment()
        {
            if (Role == "cook" || HasJob) return;          // a cook belongs to one station, a helper to its job
            var items = new List<ItemId>(loc.UnlockedItems());
            if (items.Count == 0) return;
            if (Auto) Data.assigned = items[0].ToString();
            else
            {
                int i = items.IndexOf(Assigned);
                Data.assigned = i + 1 < items.Count ? items[i + 1].ToString() : "Auto";
            }
            Stack.Clear();
            ws = WS.Idle;
            path.Clear();
            ExitZone();
        }

        void Go(Action arrive, params Vector3[] pts)
        {
            NavGrid.Route(transform.position, pts, path);     // round the stoves and counters, not through them
            onArrive = arrive;
        }

        void ExitZone()
        {
            if (current != null) { current.OnExit(this); current = null; }
        }

        void EnterZone(IZone z)
        {
            if (current == z) return;
            ExitZone();
            current = z;
            z.OnEnter(this);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            float spd = 0;
            if (path.Count > 0)
            {
                var pos = transform.position;
                var target = path[0];
                var d = target - pos;
                d.y = 0;
                float step = speed * dt;
                if (d.magnitude <= step)
                {
                    transform.position = new Vector3(target.x, 0, target.z);
                    path.RemoveAt(0);
                    spd = speed;
                    if (path.Count == 0) { var cb = onArrive; onArrive = null; cb?.Invoke(); }
                }
                else
                {
                    transform.position = pos + d.normalized * step;
                    spd = speed;
                    var rot = Quaternion.LookRotation(d.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1 - Mathf.Exp(-dt * 12f));
                }
            }
            Rig.Tick(spd, dt);
            // a walk-in whose path was cut short (e.g. its assignment changed on the way) still ends: start working
            if (walkingIn && path.Count == 0) { walkingIn = false; onArrive = null; }
            if (walkingIn) { UpdateRing(false); return; }

            bool idle = false;
            switch (Role)
            {
                case "cashier": idle = ThinkCashier(dt); break;
                case "waiter": idle = ThinkWaiter(dt); break;
                case "cook": idle = ThinkCook(dt); break;
            }
            idleTimer = idle ? idleTimer + dt : 0;
            UpdateRing(idleTimer > 1.5f);
        }

        void UpdateRing(bool idle)
        {
            Vector3 p = post;
            if (Role == "waiter" && station != null) p = station.PickupPos;
            ring.position = new Vector3(p.x, 0.025f, p.z);
            var c = Models.StaffCapColor(Role);
            float a = idle ? 0.75f + 0.2f * Mathf.Sin(Time.time * 4f) : 0.35f;
            ringRenderer.sharedMaterial.SetColor("_BaseColor", Pal.WithA(c, a));
            zzz.Visible = false;     // staff never nap: a quiet moment just shows the glowing ring at their post
        }

        public string StationLabel => station == null ? "stall" : station.Type == "fryer" ? "samosa fryer" : station.Type;

        /// <summary>A cook at their post behind the station (the station cooks on its own while they're there).</summary>
        public bool AtPost => Role == "cook" && !walkingIn && path.Count == 0 && Zones.Flat(transform.position, post) < 0.3f;

        // ---------------- cook: stands behind one station and keeps it cooking ----------------
        bool ThinkCook(float dt)
        {
            if (station == null) return true;
            if (path.Count > 0) return false;
            if (Zones.Flat(transform.position, post) > 0.3f) { Go(null, post); return false; }
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.back), 1 - Mathf.Exp(-dt * 6f));
            // busy while the station cooks; dozes when its tray is full
            if (!station.Full && Time.time % 1.2f < dt) Rig.Squash(0.06f);
            return station.Full;
        }

        // ---------------- cashier: counter cash collects itself ----------------
        bool ThinkCashier(float dt)
        {
            if (path.Count > 0) return false;
            if (Zones.Flat(transform.position, post) > 0.3f) { Go(null, post); return false; }
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.back), 1 - Mathf.Exp(-dt * 6f));
            var pile = loc.CounterPile;      // the fullest counter pile
            if (pile == null || pile.Amount <= 0) return true;
            actTimer += dt;
            if (actTimer >= Game.I.Up.CashierInterval(Data))
            {
                actTimer = 0;
                pile.Collect(transform, true);
                Rig.Squash(0.1f);
            }
            return false;
        }

        // ---------------- waiter: carries one station's items to the counter ----------------
        bool ThinkWaiter(float dt)
        {
            if ((Auto || HasJob) && ws == WS.Idle && Stack.Count == 0) tripItem = BusiestItem();
            var item = Assigned;
            // pick the station when free; keep it while fetching so we never stand at one and cook at another
            if (ws == WS.Idle || station == null || station.Item != item) station = loc.BestStation(item, transform.position, this);
            if (station == null) return true;

            switch (ws)
            {
                case WS.Idle:
                {
                    if (Stack.Count > 0) { ws = WS.ToLane; lane = null; return false; }
                    // food waiting on a counter tray with nobody there to hand it over
                    foreach (var t in Targets())
                    {
                        if (!(t is ServicePoint l) || !l.TrayReady || l.Manned) continue;
                        lane = l;
                        ws = WS.ToTray;
                        ExitZone();
                        Go(() => ws = WS.Serving, l.ServePos + new Vector3(0.25f, 0, 0.2f));
                        return false;
                    }
                    int demand = TargetDemand(item);
                    if (demand <= 0)
                    {
                        // a station with its own cook needs nobody on its ring: wait by the counter instead
                        if (station.Cook != null)
                        {
                            var wait = post;      // by its queue (or the stand, or the tables)
                            if (path.Count == 0 && Zones.Flat(transform.position, wait) > 0.4f) { ExitZone(); Go(null, wait); }
                            return path.Count == 0;
                        }
                        // wait on the station's ring: a helper standing there keeps it cooking for later
                        if (path.Count == 0 && Zones.Flat(transform.position, station.PickupPos) > 0.6f)
                        {
                            ExitZone();
                            Go(null, station.PickupPos + new Vector3(0, 0, -0.2f));
                        }
                        if (path.Count > 0) return false;
                        EnterZone(station);
                        return station.Full;
                    }
                    ws = WS.ToStation;
                    pickWait = 0;
                    ExitZone();
                    Go(() => ws = WS.Picking, station.PickupPos + new Vector3(0, 0, -0.2f));
                    return false;
                }
                case WS.ToStation:
                    if (path.Count == 0) ws = WS.Picking;
                    return false;
                case WS.ToTray:
                    if (path.Count == 0) ws = WS.Serving;
                    return false;
                case WS.Picking:
                {
                    // standing on the ring cooks the order; take each item as it's done
                    EnterZone(station);
                    int want = Mathf.Min(Stack.capacity, Mathf.Max(1, TargetDemand(item)));
                    if (Stack.Count < want)
                    {
                        int before = Stack.Count;
                        if (station.Tray.Count > 0) station.OnStay(this, dt);
                        pickWait = Stack.Count != before ? 0 : pickWait + dt;
                        // wait for the rest of the order unless the station has stalled
                        if (Stack.Count == 0 || pickWait < station.Prep + 1f) return false;
                    }
                    ExitZone();
                    ws = WS.ToLane;
                    lane = null;
                    return false;
                }
                case WS.ToLane:
                {
                    if (path.Count > 0) return false;
                    if (Stack.Count == 0) { ws = WS.Idle; return false; }
                    var target = TargetWanting(Stack);
                    if (target == null)
                    {
                        // nobody needs it right now: wait at the counter
                        var wait = post;
                        if (Zones.Flat(transform.position, wait) > 0.4f) Go(null, wait);
                        return true;
                    }
                    lane = target;
                    ExitZone();
                    Go(() => ws = WS.Serving, lane.ServePos + new Vector3(0.25f, 0, 0.2f));
                    return false;
                }
                case WS.Serving:
                {
                    if (lane == null) { ws = WS.ToLane; return false; }
                    EnterZone((IZone)lane);
                    if (lane.FrontWantsAnyOf(Stack) || lane is ServicePoint sp && sp.TrayReady)
                    {
                        ((IZone)lane).OnStay(this, dt);
                        return false;
                    }
                    ExitZone();
                    ws = Stack.Count > 0 ? WS.ToLane : WS.Idle;
                    if (ws == WS.ToLane && FrontWanting(Stack) == null && lane.Demand(Stack.items[0].id) > 0)
                    {
                        // the next customer in this lane wants it: stay put
                        ws = WS.Serving;
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }

        void OnDestroy()
        {
            ExitZone();
            if (Role == "cook" && station != null && station.Cook == this) station.Cook = null;
            if (ring) Destroy(ring.gameObject);
            zzz?.Remove();
        }
    }
}
