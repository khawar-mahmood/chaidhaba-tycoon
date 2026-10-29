using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// A dine-in table with a fixed number of seats (its capacity). Guests sit down and order; walking up
    /// to the table places matching food from your stack onto their plates. Standing still on the table's
    /// ring puts down whatever else Rafi carries, guests or no guests: it waits on the table and guests who
    /// sit down and order it help themselves. They eat, pay into the table's cash pile and leave.
    /// </summary>
    public class DiningTable : MonoBehaviour, IZone, IServeTarget
    {
        public class Seat
        {
            public Vector3 Pos;
            public Vector3 Face;
            public Vector3 Plate;
            public Customer Who;
        }

        public readonly List<Seat> Seats = new List<Seat>();
        public CashPile Pile;
        public float HalfW;
        public const float HalfD = 0.48f;
        public const float CharpaiTop = 0.5f;   // the charpai weave, where guests' food goes
        public float TopY = 0.74f;              // height of the surface food is put on
        public string Id;

        readonly Dictionary<Agent, float> timers = new Dictionary<Agent, float>();
        WorldUI.TableBubble bubble;
        WorldUI.TableTag seatTag;
        Hotspot hot;
        WorldUI.RingLabel ringLabel;

        public int Capacity => Seats.Count;
        public Vector3 Center => transform.position;
        // in front of the table (the open side facing the road), never between two tables
        public Vector3 ServePos => Center + new Vector3(0, 0, -1.75f);
        public string ServeKind => "table";

        public int Occupied
        {
            get { int n = 0; foreach (var s in Seats) if (s.Who != null) n++; return n; }
        }

        public int FreeSeats => Capacity - Occupied;

        public static DiningTable Create(Transform parent, string id, Vector3 pos, int seatsPerSide)
        {
            float halfW = 0.35f + seatsPerSide * 0.45f;
            // the kit's charpai is table and seat at once, as in the reference layout: guests sit on its long edges
            // and their food goes on the weave between them. Otherwise the truck-art table between two benches.
            bool charpai = seatsPerSide == 2 && ArtLib.Has("KitCharpaiTable");
            var art = charpai ? ArtLib.Get("KitCharpaiTable")
                : seatsPerSide == 2 ? ArtLib.Get(ArtLib.Has("KitDiningSet") ? "KitDiningSet" : "DiningTable") : null;
            if (charpai) halfW = 1.0f;
            float seatZ = charpai ? 0.62f : 0.98f, plateZ = charpai ? 0.2f : 0.24f;
            var go = Models.Obj("table_" + id, art ? art : TableMesh(seatsPerSide, halfW), parent);
            go.transform.position = pos;
            var t = go.AddComponent<DiningTable>();
            t.Id = id;
            t.HalfW = halfW;
            t.TopY = charpai ? CharpaiTop : 0.74f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < seatsPerSide; i++)
                {
                    float x = (i - (seatsPerSide - 1) * 0.5f) * 0.9f;
                    t.Seats.Add(new Seat
                    {
                        Pos = pos + new Vector3(x, 0, side * seatZ),
                        Face = new Vector3(0, 0, -side),
                        Plate = pos + new Vector3(x, t.TopY, side * plateZ)
                    });
                }
            }
            if (charpai)
                AddBox(go.transform, new Vector3(0, 0.25f, 0), new Vector3(2.0f, 0.5f, 1.1f));
            else
            {
                // colliders: tabletop and the two benches
                AddBox(go.transform, new Vector3(0, 0.37f, 0), new Vector3(halfW * 2, 0.74f, HalfD * 2));
                AddBox(go.transform, new Vector3(0, 0.22f, 0.98f), new Vector3(halfW * 2, 0.45f, 0.5f));
                AddBox(go.transform, new Vector3(0, 0.22f, -0.98f), new Vector3(halfW * 2, 0.45f, 0.5f));
            }

            // white floor ring where you stand to serve, same as the counter
            t.hot = Hotspot.Attach(Models.AlphaQuad("serveMark", go.transform, SpriteGen.RingSprite(128, 10).texture, new Vector3(0, 0.02f, -1.75f), new Vector2(1.3f, 1.3f), Pal.WithA(Color.white, 0.45f), false), Pal.TruckPink);
            t.ringLabel = Game.I.WUI.AddRingLabel(() => t.ServePos + new Vector3(0, 0, -0.72f), "Table");
            t.Pile = CashPile.Create(go.transform, pos + new Vector3(halfW - 0.22f, t.TopY, 0.05f), pos + new Vector3(halfW - 0.4f, 0, -1.7f));
            Zones.Add(t);
            t.seatTag = Game.I.WUI.AddTableTag(t);
            return t;
        }

        static void AddBox(Transform parent, Vector3 c, Vector3 size)
        {
            var go = new GameObject("col");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<BoxCollider>();
            b.center = c;
            b.size = size;
            NavGrid.MarkDirty();
        }

        static Mesh TableMesh(int seatsPerSide, float halfW) => Models.Cached("diningTable_" + seatsPerSide, b =>
        {
            float w = halfW * 2;
            // wooden table with a truck-art runner
            b.BevelBox(new Vector3(0, 0.69f, 0), new Vector3(w, 0.08f, HalfD * 2), 0.06f, Pal.Hex("#A9784F"));
            b.Box(new Vector3(0, 0.735f, 0), new Vector3(w - 0.25f, 0.012f, 0.22f), Pal.TruckPink);
            b.Box(new Vector3(0, 0.737f, 0), new Vector3(w - 0.25f, 0.012f, 0.08f), Pal.Marigold);
            foreach (var c in new[] { new Vector3(w / 2 - 0.12f, 0, HalfD - 0.1f), new Vector3(-w / 2 + 0.12f, 0, HalfD - 0.1f), new Vector3(w / 2 - 0.12f, 0, -HalfD + 0.1f), new Vector3(-w / 2 + 0.12f, 0, -HalfD + 0.1f) })
                b.Cylinder(c, 0.06f, 0.05f, 0.66f, 6, Pal.DarkWood);
            // charpai-style benches on both long sides (woven seat, wooden frame)
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * 0.98f;
                b.Box(new Vector3(0, 0.42f, z), new Vector3(w, 0.06f, 0.48f), Pal.Hex("#E9D9B0"));
                for (int i = 0; i < Mathf.RoundToInt(w / 0.32f); i++)
                    b.Box(new Vector3(-w / 2 + 0.16f + i * 0.32f, 0.452f, z), new Vector3(0.04f, 0.01f, 0.46f), Pal.Chili);
                b.Box(new Vector3(0, 0.42f, z + 0.24f), new Vector3(w + 0.08f, 0.08f, 0.06f), Pal.DarkWood);
                b.Box(new Vector3(0, 0.42f, z - 0.24f), new Vector3(w + 0.08f, 0.08f, 0.06f), Pal.DarkWood);
                foreach (var dx in new[] { -w / 2, w / 2 })
                {
                    b.Box(new Vector3(dx, 0.42f, z), new Vector3(0.06f, 0.08f, 0.5f), Pal.DarkWood);
                    b.Cylinder(new Vector3(dx, 0, z + 0.2f), 0.045f, 0.04f, 0.42f, 6, Pal.DarkWood);
                    b.Cylinder(new Vector3(dx, 0, z - 0.2f), 0.045f, 0.04f, 0.42f, 6, Pal.DarkWood);
                }
            }
            return b;
        });

        // ------------------------------------------------------------ seating

        public int FindFreeSeat()
        {
            for (int i = 0; i < Seats.Count; i++) if (Seats[i].Who == null) return i;
            return -1;
        }

        public void Reserve(int seat, Customer c) => Seats[seat].Who = c;

        public void Vacate(Customer c)
        {
            foreach (var s in Seats) if (s.Who == c) s.Who = null;
        }

        /// <summary>Guests sitting at the table and still waiting for food.</summary>
        public IEnumerable<Customer> Waiting()
        {
            foreach (var s in Seats)
            {
                var c = s.Who;
                if (c != null && c.Seated && !c.Eating && c.Remaining.Count > 0) yield return c;
            }
        }

        public bool HasWaiting
        {
            get { foreach (var _ in Waiting()) return true; return false; }
        }

        public float MinPatience01
        {
            get
            {
                float m = 1;
                foreach (var c in Waiting()) m = Mathf.Min(m, c.Patience01);
                return m;
            }
        }

        // ------------------------------------------------------------ IServeTarget

        /// <summary>How many of this item the guests still need, minus what already waits on the table.</summary>
        public int Demand(ItemId id)
        {
            int n = 0;
            foreach (var s in Seats)
                if (s.Who != null && !s.Who.Eating)
                    foreach (var r in s.Who.Remaining) if (r == id) n++;
            return Mathf.Max(0, n - StockCount(id));
        }

        public bool FrontWantsAnyOf(CarryStack st)
        {
            foreach (var c in Waiting())
                foreach (var r in c.Remaining)
                    if (st.Has(r)) return true;
            return false;
        }

        public bool ServeOne(Agent who)
        {
            if (who.Stack.IsEmpty) return false;
            for (int i = who.Stack.items.Count - 1; i >= 0; i--)
            {
                var id = who.Stack.items[i].id;
                foreach (var c in Waiting())
                {
                    if (!c.Remaining.Contains(id)) continue;
                    var it = who.Stack.Take(id);
                    c.Receive(it, who);
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------ IZone (player / waiter drop-off)

        public bool ZoneActive => true;

        public bool Contains(Vector3 p, float extra)
        {
            var d = p - Center;
            // all round the table, reaching well past the serve ring at the front
            return Mathf.Abs(d.x) < HalfW + 0.95f + extra && d.z < 1.9f + extra && d.z > -2.6f - extra;
        }

        public void OnEnter(Agent who) { timers[who] = who.ServeInterval; if (who.IsPlayer) hot.SetOn(true); }

        public void OnExit(Agent who)
        {
            timers.Remove(who); idle.Remove(who); lastPos.Remove(who);
            if (who.IsPlayer) { hot.SetOn(false); hot.SetProgress(0); }
        }

        public void OnStay(Agent who, float dt)
        {
            if (!timers.TryGetValue(who, out var t)) t = who.ServeInterval;
            t += dt;
            float interval = who.ServeInterval;
            bool served = false;
            while (t >= interval)
            {
                if (!ServeOne(who)) { t = Mathf.Min(t, interval); break; }
                served = true;
                t -= interval;
            }
            timers[who] = t;
            // putting food down is Rafi's call; staff only hand it to guests
            if (who.IsPlayer) TickLeave(who, dt, served);
        }

        // ------------------------------------------------------------ food left on the table

        public const float LeaveDelay = 0.6f;
        static readonly float[] StockX = { -0.75f, -0.45f, -0.15f, 0.15f, 0.45f, 0.75f };
        readonly ItemObj[] stock = new ItemObj[StockX.Length];
        readonly Dictionary<Agent, float> idle = new Dictionary<Agent, float>();
        readonly Dictionary<Agent, Vector3> lastPos = new Dictionary<Agent, Vector3>();
        float selfServe;

        /// <summary>Spots along the middle of the table top, between the guests' plates.</summary>
        Vector3 StockSlot(int i) => transform.TransformPoint(new Vector3(StockX[i], TopY, 0));

        public int StockCount(ItemId id)
        {
            int n = 0;
            foreach (var it in stock) if (it != null && it.id == id) n++;
            return n;
        }

        public bool StockHasSpace => System.Array.IndexOf(stock, null) >= 0;

        /// <summary>The top-most carried item Rafi would put down here: anything a seated guest can't take right now.</summary>
        public ItemId? LeavableIn(CarryStack st)
        {
            if (!StockHasSpace || st.IsEmpty || FrontWantsAnyOf(st)) return null;
            return st.items[st.items.Count - 1].id;
        }

        /// <summary>0..1 while Rafi stands on the ring about to leave something on the table.</summary>
        public float LeaveProgress(Agent who) => idle.TryGetValue(who, out var s) && LeavableIn(who.Stack) != null ? Mathf.Clamp01(s / LeaveDelay) : 0f;

        void TickLeave(Agent who, float dt, bool served)
        {
            var p = who.Pos;
            float speed = lastPos.TryGetValue(who, out var lp) ? Zones.Flat(p, lp) / Mathf.Max(dt, 1e-4f) : 99f;
            lastPos[who] = p;
            idle.TryGetValue(who, out var s);
            // only standing still on the ring, so walking round the table never empties your hands
            bool onRing = Zones.Flat(p, ServePos) < 0.95f;
            s = served || !onRing || speed > 0.6f || LeavableIn(who.Stack) == null ? 0 : s + dt;
            if (s >= LeaveDelay && LeaveOne(who)) s = LeaveDelay - who.ServeInterval;
            idle[who] = s;
            if (who.IsPlayer) hot.SetProgress(Mathf.Clamp01(s / LeaveDelay));
        }

        bool LeaveOne(Agent who)
        {
            int slot = System.Array.IndexOf(stock, null);
            var id = LeavableIn(who.Stack);
            if (slot < 0 || id == null) return false;
            var it = who.Stack.Take(id.Value);
            stock[slot] = it;
            var t = it.transform;
            t.SetParent(transform, true);
            var start = t.position;
            var startRot = t.rotation;
            var end = StockSlot(slot);
            it.hop = Tw.Run(0.22f, k =>
            {
                if (!t) return;
                var q = Vector3.Lerp(start, end, Tw.OutQuad(k));
                q.y += Mathf.Sin(k * Mathf.PI) * 0.5f;
                t.position = q;
                t.rotation = Quaternion.Slerp(startRot, Quaternion.identity, k);
            }, () => { if (t) t.position = end; it.hop = null; });
            if (who.IsPlayer)
            {
                Synth.Play("pop", 0.9f, 0.4f);
                Game.I.Guide?.Queue("tableStock", "Food left on the table waits there. Guests who order it help themselves!", "chair", Center);
            }
            return true;
        }

        /// <summary>A seated guest whose order is waiting on the table takes it onto their plate.</summary>
        void SelfServe()
        {
            selfServe -= Time.deltaTime;
            if (selfServe > 0) return;
            foreach (var c in Waiting())
                for (int i = 0; i < stock.Length; i++)
                {
                    var it = stock[i];
                    if (it == null || !c.Remaining.Contains(it.id)) continue;
                    stock[i] = null;
                    if (it.hop != null) { Tw.Stop(it.hop); it.hop = null; }
                    c.Receive(it, null);
                    Synth.Play("clink", 1.2f, 0.4f);
                    selfServe = 0.35f;
                    return;
                }
        }

        void Update()
        {
            SelfServe();
            // each seated guest shows their own order bubble now (Customer.UpdateUI); the table's combined bubble stays off
            if (bubble != null) { bubble.Remove(); bubble = null; }
        }

        void OnDestroy()
        {
            Zones.Remove(this);
            bubble?.Remove();
            seatTag?.Remove();
            ringLabel?.Remove();
        }
    }
}
