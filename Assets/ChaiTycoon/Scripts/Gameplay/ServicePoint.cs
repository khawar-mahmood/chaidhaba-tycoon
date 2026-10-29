using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// A place customers wait to be served: a counter lane (walk-in queue) or a rickshaw parking spot.
    /// Standing in its drop-off zone hands matching items from your stack to the front customer.
    /// </summary>
    public class ServicePoint : MonoBehaviour, IZone, IServeTarget
    {
        public string Kind = "counter"; // counter, rickshaw
        public Vector3 DropPos;
        public float DropRadius = 0.78f;
        public readonly List<Vector3> Slots = new List<Vector3>();
        public Vector3 FaceDir = Vector3.forward;
        public readonly List<Customer> Queue = new List<Customer>();
        public CashPile Pile;
        public bool Open = true;
        /// <summary>A counter queue for one item (chai, paratha, samosa); null = the mixed-order queue.</summary>
        public ItemId? Only;
        /// <summary>Which counter queue this is (0 chai, 1 paratha, 2 samosa, 3 mixed); -1 for rickshaw spots.</summary>
        public int Index = -1;

        readonly Dictionary<Agent, float> timers = new Dictionary<Agent, float>();
        Hotspot hot;
        WorldUI.RingLabel ringLabel;

        public static ServicePoint Create(Transform parent, string kind, Vector3 drop, CashPile pile, string label = null)
        {
            var go = new GameObject("sp_" + kind);
            go.transform.SetParent(parent, false);
            go.transform.position = drop;
            var sp = go.AddComponent<ServicePoint>();
            sp.Kind = kind;
            sp.DropPos = drop;
            sp.Pile = pile;
            sp.hot = Hotspot.Attach(Models.AlphaQuad("dropMark", go.transform, SpriteGen.RingSprite(128, 10).texture, new Vector3(0, 0.02f, 0), new Vector2(1.3f, 1.3f), Pal.WithA(Color.white, 0.45f), false), Pal.Parrot);
            sp.ringLabel = kind == "counter"
                ? Game.I.WUI.AddRingLabel(() => sp.DropPos + new Vector3(0, 0, -2.05f), label ?? "Serve", () => sp.Open)   // under the counter's front edge, where customers queue
                : Game.I.WUI.AddRingLabel(() => sp.DropPos + new Vector3(0, 0, -0.7f), kind == "rickshaw" ? "Rickshaw" : kind, () => sp.Open);
            Zones.Add(sp);
            return sp;
        }

        public Vector3 ServePos => DropPos;
        public string ServeKind => Kind;
        public int Capacity => Slots.Count;
        public bool HasSpace => Open && Queue.Count < Slots.Count;

        public Customer Front
        {
            get
            {
                foreach (var c in Queue) if (c.SlotIndex == 0 && c.AtSlot && c.Remaining.Count > 0) return c;
                return null;
            }
        }

        public void Join(Customer c)
        {
            Queue.Add(c);
            c.SetSlot(this, Queue.Count - 1);
        }

        public void Remove(Customer c)
        {
            int i = Queue.IndexOf(c);
            if (i < 0) return;
            Queue.RemoveAt(i);
            for (int j = i; j < Queue.Count; j++) Queue[j].SetSlot(this, j);
        }

        /// <summary>How many of this item the waiting customers still need, minus what's left on the counter.</summary>
        public int Demand(ItemId id)
        {
            int n = 0;
            foreach (var c in Queue) foreach (var r in c.Remaining) if (r == id) n++;
            return Mathf.Max(0, n - (HasShelf ? CounterShelfCount(id) : 0));
        }

        public bool FrontWants(ItemId id)
        {
            var f = Front;
            return f != null && f.Remaining.Contains(id);
        }

        public bool FrontWantsAnyOf(CarryStack s)
        {
            var f = Front;
            if (f == null) return false;
            foreach (var r in f.Remaining) if (s.Has(r)) return true;
            return false;
        }

        // ---------- IZone ----------
        public bool ZoneActive => Open;
        public bool Contains(Vector3 p, float extra) => Zones.Flat(p, DropPos) < DropRadius + extra;
        public void OnEnter(Agent who) { timers[who] = who.ServeInterval; if (who.IsPlayer) hot.SetOn(true); }
        public void OnExit(Agent who) { timers.Remove(who); idle.Remove(who); lastPos.Remove(who); if (who.IsPlayer) { hot.SetOn(false); hot.SetProgress(0); } }

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
            // putting things down is Rafi's call; staff only ever hand food over
            if (shelf.Length > 0 && who.IsPlayer) TickLeave(who, dt, served);
        }

        public bool ServeOne(Agent who)
        {
            var f = Front;
            if (f == null || who.Stack.IsEmpty) return false;
            // hand over the top-most item the customer still needs
            for (int i = who.Stack.items.Count - 1; i >= 0; i--)
            {
                var id = who.Stack.items[i].id;
                if (!f.Remaining.Contains(id)) continue;
                var it = who.Stack.Take(id);
                f.Receive(it, who);

                return true;
            }
            return false;
        }

        // ---------- counter shelf ----------
        // Standing still at the counter with things nobody here can take right now leaves them on the
        // counter top. The trays along the counter are shared: a customer at any lane who orders one is
        // handed it from the nearest tray, as long as someone is standing at that customer's lane.

        public const float LeaveDelay = 0.6f;
        public readonly List<Vector3> ShelfSlots = new List<Vector3>();
        ItemObj[] shelf = new ItemObj[0];
        float shelfServe;
        readonly Dictionary<Agent, float> idle = new Dictionary<Agent, float>();
        readonly Dictionary<Agent, Vector3> lastPos = new Dictionary<Agent, Vector3>();

        public bool HasShelf => shelf.Length > 0;

        /// <summary>Someone (Rafi or staff) is standing at this counter.</summary>
        public bool Manned
        {
            get { foreach (var a in timers.Keys) if (a != null) return true; return false; }
        }

        /// <summary>The front customer wants something waiting on a counter tray; it's handed over once someone stands here.</summary>
        public bool TrayReady => TrayItemForFront != null;

        public ItemId? TrayItemForFront
        {
            get
            {
                var f = Front;
                if (f == null || !HasShelf) return null;
                foreach (var l in Game.I.Loc.Lanes)
                    foreach (var it in l.shelf) if (it != null && f.Remaining.Contains(it.id)) return it.id;
                return null;
            }
        }

        /// <summary>How many of this item sit on all the counter trays together.</summary>
        public static int CounterShelfCount(ItemId id)
        {
            int n = 0;
            foreach (var l in Game.I.Loc.Lanes) n += l.ShelfCount(id);
            return n;
        }

        /// <summary>Takes one of this item off the tray closest to this lane.</summary>
        ItemObj TakeFromTrays(ItemId id)
        {
            ServicePoint best = null;
            int bestSlot = -1;
            foreach (var l in Game.I.Loc.Lanes)
                for (int i = 0; i < l.shelf.Length; i++)
                {
                    var it = l.shelf[i];
                    if (it == null || it.id != id) continue;
                    if (best == null || Zones.Flat(l.ShelfSlots[i], DropPos) < Zones.Flat(best.ShelfSlots[bestSlot], DropPos)) { best = l; bestSlot = i; }
                }
            if (best == null) return null;
            var taken = best.shelf[bestSlot];
            best.shelf[bestSlot] = null;
            return taken;
        }
        public bool ShelfHasSpace => System.Array.IndexOf(shelf, null) >= 0;

        /// <summary>Tray slots as (x, z) offsets from the hand-over spot along the counter top at height y.</summary>
        public void SetShelf(Vector2[] slots, float z, float y, bool drawTrays = true)
        {
            ShelfSlots.Clear();
            foreach (var s in slots) ShelfSlots.Add(new Vector3(DropPos.x + s.x, y, z + s.y));
            shelf = new ItemObj[slots.Length];
            if (!drawTrays) return;
            var trays = Models.Obj("shelfTrays", Models.Cached("shelfTrays2", b =>
            {
                foreach (var side in new[] { -1, 1 })
                {
                    b.BevelBox(new Vector3(side * 0.965f, 0.012f, 0), new Vector3(0.82f, 0.025f, 0.6f), 0.03f, Pal.Steel);
                    b.Box(new Vector3(side * 0.965f, 0.026f, 0), new Vector3(0.7f, 0.006f, 0.48f), Pal.Mul(Pal.Steel, 0.9f));
                }
                return b;
            }), transform);
            trays.transform.position = new Vector3(DropPos.x, y, z);
        }

        public int ShelfCount(ItemId id)
        {
            int n = 0;
            foreach (var it in shelf) if (it != null && it.id == id) n++;
            return n;
        }

        /// <summary>
        /// Whether the guide should suggest leaving it: nobody at a table or rickshaw is waiting for it
        /// (the trays only serve the counter) and nobody in this queue ordered it, unless your hands are full.
        /// </summary>
        public bool WorthLeaving(ItemId id, CarryStack st)
        {
            foreach (var s in Game.I.Loc.AllServeTargets())
                if (s.ServeKind != "counter" && s.Demand(id) > 0) return false;
            return Demand(id) == 0 || st.IsFull;
        }

        /// <summary>
        /// The top-most carried item Rafi would put down here, if any. Customer or no customer, anything can
        /// go on the counter; only the front customer is served first, and what the customer stepping up
        /// to the counter right now ordered is handed over a moment later instead.
        /// </summary>
        public ItemId? LeavableIn(CarryStack st)
        {
            if (!ShelfHasSpace || FrontWantsAnyOf(st)) return null;
            Customer arriving = null;
            foreach (var c in Queue) if (c.SlotIndex == 0 && !c.AtSlot) arriving = c;
            for (int i = st.items.Count - 1; i >= 0; i--)
            {
                var id = st.items[i].id;
                if (arriving != null && arriving.Remaining.Contains(id)) continue;
                return id;
            }
            return null;
        }

        /// <summary>What the guide suggests putting down: only things that are just in the way.</summary>
        public ItemId? SuggestedLeave(CarryStack st)
        {
            if (!ShelfHasSpace || FrontWantsAnyOf(st)) return null;
            for (int i = st.items.Count - 1; i >= 0; i--)
                if (WorthLeaving(st.items[i].id, st)) return st.items[i].id;
            return null;
        }

        /// <summary>0..1 while Rafi stands here about to leave something on the tray.</summary>
        public float LeaveProgress(Agent who) => idle.TryGetValue(who, out var s) && LeavableIn(who.Stack) != null ? Mathf.Clamp01(s / LeaveDelay) : 0f;

        void TickLeave(Agent who, float dt, bool served)
        {
            var p = who.Pos;
            float speed = lastPos.TryGetValue(who, out var lp) ? Zones.Flat(p, lp) / Mathf.Max(dt, 1e-4f) : 99f;
            lastPos[who] = p;
            idle.TryGetValue(who, out var s);
            // only while standing still with nothing to hand over, so walking past never empties your hands
            s = served || speed > 0.6f || LeavableIn(who.Stack) == null ? 0 : s + dt;
            if (s >= LeaveDelay && LeaveOne(who)) s = LeaveDelay - who.ServeInterval;
            idle[who] = s;
            if (who.IsPlayer) hot?.SetProgress(Mathf.Clamp01(s / LeaveDelay));
        }

        bool LeaveOne(Agent who)
        {
            int slot = System.Array.IndexOf(shelf, null);
            var id = LeavableIn(who.Stack);
            if (slot < 0 || id == null) return false;
            var it = who.Stack.Take(id.Value);
            shelf[slot] = it;
            var t = it.transform;
            t.SetParent(transform, true);
            var start = t.position;
            var startRot = t.rotation;
            var end = ShelfSlots[slot];
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
                Game.I.Guide?.Queue("shelf", "Extra food waits on the counter tray. Stand here to hand it over!", "counter", DropPos);
            }
            return true;
        }

        void Update()
        {
            if (shelf.Length == 0) return;
            shelfServe -= Time.deltaTime;
            // the tray isn't self-service: someone has to be at the counter to hand it over
            if (shelfServe > 0 || !Manned) return;
            var want = TrayItemForFront;
            if (want == null) return;
            var f = Front;
            {
                var it = TakeFromTrays(want.Value);
                if (it == null) return;
                if (it.hop != null) { Tw.Stop(it.hop); it.hop = null; }
                it.transform.SetParent(null, true);
                f.Receive(it, null);
                Synth.Play("clink", 1.2f, 0.4f);
                shelfServe = 0.3f;
                return;
            }
        }

        void OnDestroy() { Zones.Remove(this); ringLabel?.Remove(); }
    }
}
