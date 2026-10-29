using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Customers are the game's clock: they arrive, queue, show what they want with a patience ring,
    /// pay into a cash pile when served, and walk off carrying their food. A lost customer is only a
    /// lost sale — no fail state.
    /// </summary>
    public class Customer : MonoBehaviour
    {
        public enum St { Arriving, Waiting, ToSlot, InQueue, Leaving }

        public CustomerCfg Cfg;
        public string Type => Cfg.id;
        public CharRig Rig;
        public CarryStack Hands;
        public readonly List<ItemId> Order = new List<ItemId>();
        public readonly List<ItemId> Remaining = new List<ItemId>();

        /// <summary>Replaces the order (set when the customer joins a counter queue, before they reach it).</summary>
        public void SetOrder(List<ItemId> order)
        {
            Order.Clear(); Order.AddRange(order);
            Remaining.Clear(); Remaining.AddRange(order);
        }

        public float Patience, PatienceMax;
        public ServicePoint SP;
        public int SlotIndex = -1;
        public bool AtSlot;
        public float FrontTime;
        public St State;
        public bool IsVehicle; // rickshaw: the driver stays seated
        public bool OnBike;
        public bool LuckyLeaf;

        // dine-in
        public DiningTable Table;
        public bool Seated, Eating;
        public bool IsDiner => Table != null;
        int seatIndex = -1;
        float eatTimer, eatTotal;
        readonly List<ItemObj> plate = new List<ItemObj>();

        public static bool FreezePatience;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => FreezePatience = false;
        /// <summary>Chance that a new customer is a VIP (pays double).</summary>
        public const double VipChance = 0.05;
        public bool Vip;

        readonly List<Vector3> path = new List<Vector3>();
        Action onPathDone;
        float moveSpeed = 2.1f;   // a brisk walk (the walk clip at ~2.5x)
        float curSpeed;
        GameObject vehicle;
        Transform bike;
        Vector3 bikeSpot;
        int bikeSpotIndex = -1;
        WorldUI.OrderBubble bubble;
        WorldUI.MiniRing mini;
        bool leaving;
        CustomerSpawner spawner;
        double paidPartial;

        public Vector3 BubbleAnchor => IsVehicle ? transform.position + Vector3.up * 2.3f : Rig.headTop.position;

        // ------------------------------------------------------------ creation

        public static Customer Create(CustomerSpawner sp, CustomerCfg cfg, List<ItemId> order, Vector3 pos, System.Random r)
        {
            var go = new GameObject("cust_" + cfg.id);
            go.transform.position = pos;
            var c = go.AddComponent<Customer>();
            c.spawner = sp;
            c.Cfg = cfg;
            c.Order.AddRange(order);
            c.Remaining.AddRange(order);
            c.PatienceMax = cfg.patience * Game.I.Up.PatienceMult;
            // now and then a VIP (from level 2, never a rickshaw): a gold star overhead, a bit less patient, pays double
            var prog = Game.I.Progression;
            if (cfg.id != "rickshaw" && prog != null && prog.DisplayLevel >= 2 && r.NextDouble() < VipChance)
            {
                c.Vip = true;
                c.PatienceMax *= 0.85f;
            }
            c.Patience = c.PatienceMax;

            var spec = Models.CustomerSpec(cfg.id, r);
            c.Rig = Models.Character(spec, go.transform, "rig");
            c.Rig.NoRun = true;     // customers only ever walk
            c.Hands = go.AddComponent<CarryStack>();
            c.Hands.anchor = c.Rig.carryAnchor;
            c.Hands.rig = c.Rig;
            c.Hands.capacity = 99;
            if (c.Vip) Game.I.WUI.AddBadge(c.Rig.headTop, Skin.Get("star_fill") ?? SpriteGen.Icon("star"));

            if (cfg.id == "rickshaw")
            {
                c.IsVehicle = true;
                var body = new[] { Pal.Parrot, Pal.Marigold, Pal.Teal }[r.Next(3)];
                var art = ArtLib.Get("Rickshaw");
                c.vehicle = Models.Obj("rickshaw", art ? art : Models.RickshawMesh(body), go.transform);
                Models.Blob(c.vehicle.transform, 2.6f, 0.3f);
                // low in the cab so the driver's head stays under the canopy roof (underside ~1.6 m)
                c.Rig.transform.localPosition = new Vector3(0, -0.12f, 0.42f);
                c.Rig.transform.localScale *= 0.9f;
                c.Rig.sitting = true;
                foreach (Transform t in c.Rig.transform) if (t.name == "blob") t.gameObject.SetActive(false);
                c.moveSpeed = 7f;
            }
            return c;
        }

        public void RideBike(Transform bikeT, Vector3 spot, int spotIndex)
        {
            bike = bikeT;
            bikeSpot = spot;
            bikeSpotIndex = spotIndex;
            OnBike = true;
            bike.SetParent(transform, false);
            bike.localPosition = Vector3.zero;
            bike.localRotation = Quaternion.identity;
            Rig.transform.localPosition = new Vector3(0, 0.25f, -0.15f);
            Rig.sitting = true;
            moveSpeed = 4.5f;
        }

        // ------------------------------------------------------------ movement

        public void MoveTo(Action done, params Vector3[] pts)
        {
            // on foot they walk round counters, stoves and tables; rickshaws and bikes keep to their lanes
            if (IsVehicle || OnBike) { path.Clear(); path.AddRange(pts); }
            else NavGrid.Route(transform.position, pts, path);
            onPathDone = done;
        }

        static bool OffScreen(Vector3 p)
        {
            var cam = Camera.main;
            if (!cam) return false;
            var v = cam.WorldToViewportPoint(p + Vector3.up);
            return v.z <= 0 || v.x < -0.08f || v.x > 1.08f || v.y < -0.08f || v.y > 1.08f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            var pos = transform.position;
            curSpeed = 0;
            if (path.Count > 0)
            {
                var target = path[0];
                target.y = 0;
                var d = target - pos;
                d.y = 0;
                // out of sight a walker hurries (three times the pace), so the long stretch of road from the
                // edge of the map doesn't keep the counters waiting; on screen everyone strolls
                float step = moveSpeed * dt * (!IsVehicle && OffScreen(pos) ? 3f : 1f);
                if (d.magnitude <= step)
                {
                    transform.position = target;
                    path.RemoveAt(0);
                    curSpeed = moveSpeed;
                    if (path.Count == 0)
                    {
                        var cb = onPathDone;
                        onPathDone = null;
                        cb?.Invoke();
                    }
                }
                else
                {
                    transform.position = pos + d.normalized * step;
                    curSpeed = moveSpeed;
                    var rot = Quaternion.LookRotation(d.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1 - Mathf.Exp(-dt * (IsVehicle ? 6f : 12f)));
                }
            }
            else if (State == St.InQueue && SP != null)
            {
                var rot = Quaternion.LookRotation(SP.FaceDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1 - Mathf.Exp(-dt * 8f));
            }
            else if (Seated && Table != null)
            {
                var rot = Quaternion.LookRotation(Table.Seats[seatIndex].Face, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1 - Mathf.Exp(-dt * 10f));
            }

            Rig.Tick(OnBike || IsVehicle || Seated ? 0 : curSpeed, dt);

            if (State == St.InQueue && AtSlot && !leaving && !Eating)
            {
                bool servable = SlotIndex == 0 || IsVehicle || Seated;
                if (servable) FrontTime += dt;
                if (!FreezePatience)
                {
                    Patience -= dt * (servable ? 1f : 0.4f); // waiting further back is less stressful
                    if (Patience <= 0) Leave(false);
                }
            }
            if (Eating) TickEating(dt);
            UpdateUI();
        }

        void TickEating(float dt)
        {
            eatTimer -= dt;
            float k = Mathf.Clamp01(1 - eatTimer / Mathf.Max(0.01f, eatTotal));
            // little bites: the food on the plate shrinks as they eat
            foreach (var it in plate) if (it) it.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.35f, k);
            if (Mathf.Repeat(eatTimer, 0.9f) < dt) Rig.Hop(0.08f);
            if (eatTimer <= 0) { Eating = false; Complete(); }
        }

        void UpdateUI()
        {
            // the front of a queue, a rickshaw driver, or a seated guest still waiting for food
            bool showBubble = State == St.InQueue && AtSlot && !leaving
                              && (IsDiner ? Seated && !Eating && Remaining.Count > 0 : SlotIndex == 0 || IsVehicle);
            // further back in the queue only a worried customer shows their patience (keeps the queue uncluttered)
            bool showMini = State == St.InQueue && AtSlot && !leaving && !showBubble && !Eating && Remaining.Count > 0 && Patience01 < 0.5f;
            if (showBubble && bubble == null) bubble = Game.I.WUI.AddBubble(this);
            if (!showBubble && bubble != null) { bubble.Remove(); bubble = null; }
            if (showMini && mini == null) mini = Game.I.WUI.AddMiniRing(this);
            if (!showMini && mini != null) { mini.Remove(); mini = null; }
        }

        public float Patience01 => PatienceMax > 0 ? Mathf.Clamp01(Patience / PatienceMax) : 1;

        // ------------------------------------------------------------ queue

        public void SetSlot(ServicePoint sp, int idx)
        {
            SP = sp;
            SlotIndex = idx;
            if (leaving) return;
            State = St.ToSlot;
            AtSlot = false;
            var slot = sp.Slots[Mathf.Min(idx, sp.Slots.Count - 1)];
            if (IsVehicle)
            {
                // drive along the road lane to the parking spot
                MoveTo(ArrivedAtSlot, new Vector3(slot.x, 0, slot.z));
                return;
            }
            MoveTo(ArrivedAtSlot, spawner.Loc.ViaGate(transform.position, slot));
        }

        void ArrivedAtSlot()
        {
            if (leaving) return;
            State = St.InQueue;
            AtSlot = true;
            if (IsVehicle) { Synth.Play("horn", UnityEngine.Random.Range(0.95f, 1.05f), 0.5f, 0.3f); GameEvents.RickshawParked?.Invoke(this); }
        }

        // ------------------------------------------------------------ dine-in

        float SideX(DiningTable t, Vector3 seatPos) => t.Center.x + (seatPos.x >= t.Center.x ? 1 : -1) * (t.HalfW + 0.6f);

        /// <summary>Walk around the table to a reserved seat and sit down.</summary>
        public void GoSit(DiningTable t, int seat)
        {
            Table = t;
            seatIndex = seat;
            t.Reserve(seat, this);
            State = St.ToSlot;
            AtSlot = false;
            var s = t.Seats[seat];
            float sx = SideX(t, s.Pos);
            MoveTo(Sit, spawner.Loc.ViaGate(transform.position, new Vector3(sx, 0, s.Pos.z), s.Pos));
        }

        void Sit()
        {
            if (leaving) return;
            Seated = true;
            State = St.InQueue;
            AtSlot = true;
            Rig.sitting = true;
            transform.position = Table.Seats[seatIndex].Pos;
            transform.rotation = Quaternion.LookRotation(Table.Seats[seatIndex].Face);
            Rig.Hop(0.12f);
            GameEvents.CustomerSeated?.Invoke(this);
        }

        public void WaitAt(Vector3 spot)
        {
            State = St.Waiting;
            MoveTo(() => { transform.rotation = Quaternion.LookRotation(Vector3.forward); }, spawner.Loc.ViaGate(transform.position, spot));
        }

        // ------------------------------------------------------------ serving

        public void Receive(ItemObj it, Agent from)
        {
            if (it == null) return;
            Remaining.Remove(it.id);
            if (IsDiner)
            {
                // the food is placed on the table in front of the guest
                var t = it.transform;
                var start = t.position;
                int idx = plate.Count;
                var seat = Table.Seats[seatIndex];
                float side = Order.Count > 1 ? (idx % 2 == 0 ? -0.13f : 0.13f) : 0f;
                var end = seat.Plate + new Vector3(side, 0, (idx / 2) * 0.16f * seat.Face.z);
                t.SetParent(Table.transform, true);
                plate.Add(it);
                Tw.Run(0.25f, k =>
                {
                    if (!t) return;
                    var p = Vector3.Lerp(start, end, Tw.OutQuad(k));
                    p.y += Mathf.Sin(k * Mathf.PI) * 0.6f;
                    t.position = p;
                    t.rotation = Quaternion.Slerp(t.rotation, Quaternion.identity, k);
                });
            }
            else if (IsVehicle)
            {
                var t = it.transform;
                var start = t.position;
                Tw.Run(0.25f, k =>
                {
                    if (!t) return;
                    var end = transform.position + Vector3.up * 1.2f;
                    var p = Vector3.Lerp(start, end, k);
                    p.y += Mathf.Sin(k * Mathf.PI) * 0.6f;
                    t.position = p;
                    t.localScale = Vector3.one * (1 - k * 0.7f);
                }, () => { if (t) Destroy(t.gameObject); });
            }
            else if (SP != null && SP.Kind == "counter")
            {
                // set down on the counter in front of the customer first (side by side for a bigger order),
                // then a moment later they pick it up
                var t = it.transform;
                t.SetParent(null, true);
                var start = t.position;
                var startRot = t.rotation;
                int idx = Order.Count - Remaining.Count - 1;
                var spot = new Vector3(SP.DropPos.x + (idx - (Order.Count - 1) * 0.5f) * 0.24f, 0.9f, Location.CounterZ - 0.15f);
                Tw.Run(0.22f, k =>
                {
                    if (!t) return;
                    var p = Vector3.Lerp(start, spot, Tw.OutQuad(k));
                    p.y += Mathf.Sin(k * Mathf.PI) * 0.5f;
                    t.position = p;
                    t.rotation = Quaternion.Slerp(startRot, Quaternion.identity, k);
                }, () =>
                {
                    if (!t) return;
                    Tw.Punch(t, 0.15f, 0.15f);
                    Tw.Run(0.32f, k => { }, () =>
                    {
                        if (!t) return;
                        if (this && Hands) Hands.Add(it, 0.22f);
                        else Destroy(t.gameObject);       // they're gone already: clear the counter
                    });
                });
            }
            else Hands.Add(it, 0.2f);
            Rig.Hop(0.15f);
            bubble?.Served(it.id);
            if (from != null && from.IsPlayer) Synth.Play("clink", 1.2f, 0.5f);
            GameEvents.ItemServed?.Invoke(it.id);
            Game.I.S.stats.served++;
            if (it.id == ItemId.Chai) Game.I.S.stats.chai++;
            if (Remaining.Count == 0)
            {
                if (IsDiner)
                {
                    // dine-in guests eat at the table before paying
                    Eating = true;
                    eatTotal = eatTimer = 5.5f / Game.I.Up.EatSpeedMult;
                    if (mini != null) { mini.Remove(); mini = null; }
                }
                else Complete();
            }
        }

        void Complete()
        {
            var g = Game.I;
            double basePay = 0;
            foreach (var id in Order) basePay += g.Up.Price(id);
            basePay *= Cfg.pay;
            if (Vip) basePay *= 2;
            double tip = 0;
            bool fast = Patience01 >= g.Eco.rules.fastTipThreshold;
            if (fast) tip += basePay * g.Eco.rules.fastTipBonus;
            tip += basePay * Cfg.tip;
            tip *= g.Up.TipMult;
            double total = g.Income(basePay + tip);
            double tipShown = g.Income(tip);
            var pile = IsDiner ? Table.Pile : SP?.Pile;
            pile?.Add(total, transform.position, tipShown);
            g.Fx.Emote(this, "heart");
            if (Vip) { g.Hud.Toast("VIP paid double!", "star"); Synth.Play("bell", 1.2f, 0.6f); }
            Synth.Play("wah", UnityEngine.Random.Range(0.9f, 1.25f), 0.7f, 0.15f);
            if (LuckyLeaf || spawner.RollLuckyLeaf())
            {
                g.AddLeaves(1, transform.position + Vector3.up * 2f);
                g.Hud.Toast("Lucky tip!", "leaf");
            }
            GameEvents.CustomerServed?.Invoke(this);
            Leave(true);
        }

        public void Leave(bool happy)
        {
            if (leaving) return;
            leaving = true;
            var g = Game.I;
            if (!happy) spawner.Lost++;
            if (!happy)
            {
                // pay for anything already handed over; nothing is ever lost
                var pile = IsDiner ? Table.Pile : SP?.Pile;
                if (Order.Count != Remaining.Count && pile != null)
                {
                    double pay = 0;
                    var given = new List<ItemId>(Order);
                    foreach (var r in Remaining) given.Remove(r);
                    foreach (var id in given) pay += g.Up.Price(id) * Cfg.pay;
                    if (pay > 0) pile.Add(g.Income(pay), transform.position);
                }
                g.Fx.Emote(this, "cloud");
                Synth.Play("hmm", UnityEngine.Random.Range(0.9f, 1.1f), 0.6f, 0.2f);
            }
            if (bubble != null) { bubble.Remove(); bubble = null; }
            if (mini != null) { mini.Remove(); mini = null; }
            SP?.Remove(this);
            spawner.OnCustomerLeft(this);
            State = St.Leaving;

            var loc = spawner.Loc;
            var pos = transform.position;
            float exitX = pos.x < 0 ? -loc.SpawnX : loc.SpawnX;
            if (UnityEngine.Random.value < 0.3f) exitX = -exitX;
            if (!loc.LeftOpen) exitX = loc.SpawnX;      // parked rickshaws fill the left end of the road edge
            if (IsDiner)
            {
                var seatPos = Table.Seats[seatIndex].Pos;
                float sx = SideX(Table, seatPos);
                Table.Vacate(this);
                Seated = false;
                Eating = false;
                Rig.sitting = false;
                foreach (var it in plate) if (it) Destroy(it.gameObject);
                plate.Clear();
                var side = new Vector3(sx, 0, seatPos.z);
                var walk = new List<Vector3> { side };
                walk.AddRange(loc.ToStreet(side, exitX));
                MoveTo(Despawn, walk.ToArray());
            }
            else if (IsVehicle)
            {
                Synth.Play("horn", 1.1f, 0.35f, 0.3f);
                MoveTo(Despawn, new Vector3(pos.x + 3f, 0, pos.z), new Vector3(loc.SpawnX + 6, 0, pos.z));
            }
            else if (bike != null)
            {
                // out to the kerb, along to the bike, and off down the road
                var walk = new List<Vector3>(loc.ToStreet(pos, bikeSpot.x));
                walk.Add(bikeSpot + new Vector3(0, 0, 0.5f));
                MoveTo(() =>
                {
                    Mount();
                    // pull out past the other parked bikes, then away along the near lane
                    float lane = bikeSpot.z - 0.45f;
                    MoveTo(Despawn, new Vector3(bikeSpot.x + Mathf.Sign(exitX) * 0.9f, 0, lane), new Vector3(exitX + Mathf.Sign(exitX) * 4, 0, lane));
                }, walk.ToArray());
            }
            else
            {
                MoveTo(Despawn, loc.ToStreet(pos, exitX));
            }
        }

        /// <summary>Student parks the bike and walks; called by the spawner on arrival.</summary>
        public void Dismount()
        {
            if (bike == null) return;
            OnBike = false;
            bike.SetParent(spawner.Loc.transform, true);
            Rig.transform.localPosition = Vector3.zero;
            Rig.sitting = false;
            transform.position = bike.position + Vector3.forward * 0.5f;      // step off onto the kerb
            moveSpeed = 2.1f;
        }

        void Mount()
        {
            if (bike == null) return;
            transform.position = bike.position;
            transform.rotation = bike.rotation;
            bike.SetParent(transform, true);
            bike.localPosition = Vector3.zero;
            bike.localRotation = Quaternion.identity;
            Rig.transform.localPosition = new Vector3(0, 0.25f, -0.15f);
            Rig.sitting = true;
            OnBike = true;
            moveSpeed = 4.5f;
            spawner.FreeBikeSpot(bikeSpotIndex);
        }

        void Despawn()
        {
            Hands.Clear();
            if (bike) Destroy(bike.gameObject);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (bubble != null) bubble.Remove();
            if (mini != null) mini.Remove();
            if (spawner != null) spawner.Forget(this);
        }
    }
}
