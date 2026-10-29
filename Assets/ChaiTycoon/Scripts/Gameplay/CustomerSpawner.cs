using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Spawns customers on a timer that shortens as the star rating and the level grow, and tops up any
    /// counter that runs short so none ever stands empty. The counter queues hold QueueMax people plus 3 per
    /// extra counter; extras wait at the roadside so the screen never gets crowded.
    /// </summary>
    public class CustomerSpawner : MonoBehaviour
    {
        public Location Loc { get; private set; }
        public readonly List<Customer> Active = new List<Customer>();
        public int Lost;
        readonly List<Customer> waiting = new List<Customer>();
        readonly System.Random rng = new System.Random();
        float timer, rickTimer, waitCheck, dinerTimer, topUpGap;
        bool[] bikeTaken = new bool[0];
        bool running;

        public void Begin(Location loc, bool tutorialStart)
        {
            Clear();
            Loc = loc;
            running = true;
            bikeTaken = new bool[loc.BikeSpots.Count];
            timer = 0;
            rickTimer = 4f;
            dinerTimer = 3f;
            // a new game has no counter yet: the tutorial customers come when the chai counter opens (see Update)
            tutorialPending = tutorialStart;
            if (!tutorialStart && Loc.Lanes.Count > 0)
                for (int i = 0; i < 2; i++) SpawnDirect(PickLane(), null);
        }

        bool tutorialPending;

        public void Clear()
        {
            foreach (var c in Active.ToArray()) if (c) Destroy(c.gameObject);
            Active.Clear();
            waiting.Clear();
            running = false;
        }

        public void Forget(Customer c)
        {
            Active.Remove(c);
            waiting.Remove(c);
        }

        public void OnCustomerLeft(Customer c) => waiting.Remove(c);

        int CounterQueued()
        {
            int n = 0;
            foreach (var l in Loc.Lanes) n += l.Queue.Count;
            return n;
        }

        /// <summary>How many may stand in the counter queues together: the queue upgrade's limit, plus room for every extra counter.</summary>
        int CounterCap => Game.I.Up.QueueMax + 3 * Mathf.Max(0, Loc.Lanes.Count - 1);

        /// <summary>0 on the first level, 1 on the last: the stall gets busier as the player progresses.</summary>
        float Progress01
        {
            get
            {
                var p = Game.I.Progression;
                if (p == null || !p.HasLevels) return 0.5f;
                return Mathf.Clamp01(p.LevelIndex / (float)Mathf.Max(1, p.Levels.Length - 1));
            }
        }

        /// <summary>
        /// The open counter that is furthest below the queue length it should keep (customers already walking
        /// over count, since they join the queue as they set off), or null when every counter has enough.
        /// </summary>
        ServicePoint ThinLane()
        {
            // 2 people per counter at the start, 4 near the end, so whoever serves there never stands idle; a
            // counter with its own helper keeps one more coming
            int want = 2 + Mathf.RoundToInt(Progress01 * 2f);
            var menu = Loc.UnlockedItems();
            ServicePoint best = null;
            foreach (var l in Loc.Lanes)
            {
                if (!l.HasSpace || l.Queue.Count >= want + (Loc.LaneHasHelper(l) ? 1 : 0)) continue;
                if (l.Only.HasValue && !menu.Contains(l.Only.Value)) continue;
                if (best == null || l.Queue.Count < best.Queue.Count) best = l;
            }
            return best;
        }

        ServicePoint PickLane()
        {
            // the shortest queue whose food can be made; ties go to a random one so every queue gets customers
            ServicePoint best = null;
            var menu = Loc.UnlockedItems();
            int ties = 0;
            foreach (var l in Loc.Lanes)
            {
                if (!l.HasSpace) continue;
                if (l.Only.HasValue && !menu.Contains(l.Only.Value)) continue;
                if (best == null || l.Queue.Count < best.Queue.Count) { best = l; ties = 1; }
                else if (l.Queue.Count == best.Queue.Count && rng.Next(++ties) == 0) best = l;
            }
            return best;
        }

        void Update()
        {
            if (!running || Loc == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            var g = Game.I;
            if (!g.Started) return;
            if (tutorialPending && Loc.Lanes.Count > 0)
            {
                // three customers already waiting at the new chai counter, so the first serve comes quickly
                tutorialPending = false;
                for (int i = 0; i < 3; i++) SpawnDirect(Loc.Lanes[0], ItemId.Chai);
                Customer.FreezePatience = true;
            }

            // each counter lane brings its own stream of customers ("more customers at once"); none before a counter
            // and the stream speeds up a quarter by the end of the stage, and 15% more for every helper
            // (helpers keep up with a busier stall, so they always have someone to serve)
            float interval = Loc.SpawnInterval / g.Up.ArrivalMult / (1f + 0.6f * Mathf.Max(0, Loc.Lanes.Count - 1))
                             * Mathf.Lerp(1f, 0.75f, Progress01) / (1f + 0.15f * Loc.HelperCount);
            if (Loc.Lanes.Count > 0)
            {
                timer += dt;
                topUpGap -= dt;
                bool room = Active.Count < g.Eco.rules.maxCustomersOnScreen;
                if (timer >= interval && room)
                {
                    timer = Random.Range(-0.4f, 0.4f);
                    SpawnWalker(false);
                }
                // never an empty counter: a counter short of customers gets one on the way straight away
                else if (topUpGap <= 0 && room && waiting.Count == 0 && CounterQueued() < CounterCap && ThinLane() != null)
                {
                    topUpGap = 0.8f;
                    SpawnWalker(true);
                }
            }

            if (Loc.RickshawSpots.Count > 0)
            {
                rickTimer += dt;
                if (rickTimer >= Mathf.Max(7f, interval * 1.8f))
                {
                    foreach (var sp in Loc.RickshawSpots)
                    {
                        if (sp.Queue.Count == 0)
                        {
                            SpawnRickshaw(sp);
                            rickTimer = Random.Range(-2f, 0f);
                            break;
                        }
                    }
                }
            }

            // dine-in guests arrive in small groups whenever a table has free seats
            if (Loc.Tables.Count > 0)
            {
                dinerTimer += dt;
                float dInterval = Mathf.Max(8f, Loc.SpawnInterval * 2.2f) / g.Up.ArrivalMult / Mathf.Max(1, Loc.Tables.Count * 0.75f);
                if (dinerTimer >= dInterval && Active.Count < g.Eco.rules.maxCustomersOnScreen)
                    dinerTimer = SpawnDiners() ? Random.Range(-1.5f, 0f) : dInterval * 0.6f;
            }

            waitCheck -= dt;
            if (waitCheck <= 0)
            {
                waitCheck = 0.3f;
                PromoteWaiting();
            }

            g.S.luckyTimer += dt * (1 + g.Up.Perk("luckyTips") * 0.25f);
        }

        /// <summary>About every 5 minutes of play a customer tips a tea leaf.</summary>
        public bool RollLuckyLeaf()
        {
            var g = Game.I;
            if (g.S.luckyTimer < g.Eco.rules.luckyTipInterval) return false;
            if (g.S.leaves == 0 && !g.S.leafHintShown) return false; // first leaf comes from a mission
            g.S.luckyTimer = 0;
            return true;
        }

        void PromoteWaiting()
        {
            while (waiting.Count > 0 && CounterQueued() < CounterCap)
            {
                var lane = PickLane();
                if (lane == null) break;
                var c = waiting[0];
                waiting.RemoveAt(0);
                if (c == null) continue;
                JoinLane(lane, c);
            }
            // tidy waiting spots
            for (int i = 0; i < waiting.Count; i++)
            {
                var c = waiting[i];
                if (c != null && c.State == Customer.St.Waiting) { }
            }
        }

        /// <summary>
        /// The order for a customer joining a counter queue: the chai, paratha and samosa queues order only their item
        /// (one, sometimes two; samosas come in pairs more often), the mixed queue two or three different things.
        /// </summary>
        List<ItemId> MakeOrderFor(CustomerCfg cfg, ServicePoint lane)
        {
            if (lane == null || lane.Kind != "counter") return MakeOrder(cfg);
            var order = new List<ItemId>();
            if (lane.Only.HasValue)
            {
                var id = lane.Only.Value;
                var ic = Game.I.Eco.Item(id);
                bool pair = ic != null && ic.batch > 1 ? rng.NextDouble() < 0.6 : rng.NextDouble() < 0.3;
                order.Add(id);
                if (pair) order.Add(id);
                return order;
            }
            var menu = new List<ItemId>(Loc.UnlockedItems());
            if (menu.Count == 0) menu.Add(ItemId.Chai);
            int kinds = Mathf.Min(menu.Count, rng.NextDouble() < 0.35 ? 3 : 2);
            for (int i = 0; i < kinds; i++)
            {
                int k = rng.Next(menu.Count);
                order.Add(menu[k]);
                menu.RemoveAt(k);
            }
            if (order.Count == 1) order.Add(order[0]);       // only one item on the menu yet: a double order
            order.Sort();
            return order;
        }

        /// <summary>Lines a customer up in a counter queue, giving them that queue's kind of order.</summary>
        void JoinLane(ServicePoint lane, Customer c)
        {
            c.SetOrder(MakeOrderFor(c.Cfg, lane));
            lane.Join(c);
        }

        List<ItemId> MakeOrder(CustomerCfg cfg)
        {
            var menu = Loc.UnlockedItems();
            var pool = new List<ItemId>();
            foreach (var id in menu)
            {
                var ic = Game.I.Eco.Item(id);
                if (cfg.cheapOnly && ic != null && !ic.cheap) continue;
                pool.Add(id);
            }
            if (pool.Count == 0) pool.Add(ItemId.Chai);
            int n = rng.Next(cfg.minItems, cfg.maxItems + 1);
            // once the menu grows, locals often order a pair (chai with a paratha, say)
            if (cfg.id == "walkin" && pool.Count >= 2 && rng.NextDouble() < 0.45) n = 2;
            var order = new List<ItemId>();
            for (int i = 0; i < n; i++)
            {
                // chai is the heart of the stall: slightly more common
                ItemId pick = pool[rng.Next(pool.Count)];
                if (pool.Count > 1 && rng.NextDouble() < 0.25) pick = ItemId.Chai;
                order.Add(pick);
                // batch items come in bigger orders
                var ic = Game.I.Eco.Item(pick);
                if (ic != null && ic.batch > 1 && order.Count < 4 && rng.NextDouble() < 0.6) order.Add(pick);
            }
            if (order.Count > 4) order.RemoveRange(4, order.Count - 4);
            order.Sort();
            return order;
        }

        CustomerCfg PickWalkerType()
        {
            var g = Game.I;
            float total = 0;
            var opts = new List<CustomerCfg>();
            foreach (var c in g.Eco.customers)
            {
                if (c.weight <= 0) continue;
                if (!string.IsNullOrEmpty(c.needs) && !Loc.Has(c.needs)) continue;
                opts.Add(c);
                total += c.weight;
            }
            float r = (float)rng.NextDouble() * total;
            foreach (var c in opts)
            {
                r -= c.weight;
                if (r <= 0) return c;
            }
            return opts.Count > 0 ? opts[0] : g.Eco.Customer("walkin");
        }

        Customer SpawnDirect(ServicePoint lane, ItemId? item)
        {
            if (lane == null) return null;
            var cfg = Game.I.Eco.Customer("walkin");
            var order = item.HasValue ? new List<ItemId> { item.Value } : MakeOrderFor(cfg, lane);
            var slot = lane.Slots[Mathf.Min(lane.Queue.Count, lane.Slots.Count - 1)];
            var c = Customer.Create(this, cfg, order, slot, rng);
            c.transform.rotation = Quaternion.LookRotation(lane.FaceDir);
            Active.Add(c);
            lane.Join(c);
            return c;
        }

        /// <summary>A walk-in from either end of the street. A top-up walks (bikes stop further away and join the queue later).</summary>
        void SpawnWalker(bool topUp)
        {
            var cfg = PickWalkerType();
            var order = MakeOrder(cfg);
            bool left = Loc.LeftOpen && rng.NextDouble() < 0.5;      // parked rickshaws fill the left end of the road edge
            float sx = left ? -Loc.SpawnX : Loc.SpawnX;

            int bikeIdx = -1;
            if (cfg.id == "student" && !topUp)
            {
                for (int i = 0; i < bikeTaken.Length; i++) if (!bikeTaken[i]) { bikeIdx = i; break; }
            }

            Customer c;
            if (bikeIdx >= 0)
            {
                bikeTaken[bikeIdx] = true;
                var spot = Loc.BikeSpots[bikeIdx];
                var start = new Vector3(sx, 0, spot.z - 0.45f);       // along the near lane, past the parked bikes
                c = Customer.Create(this, cfg, order, start, rng);
                var bike = Models.Obj("bike", Models.BicycleMesh(new[] { Pal.Chili, Pal.Royal, Pal.Teal, Pal.Parrot }[rng.Next(4)]), null).transform;
                Models.Blob(bike, 1.2f, 0.25f);
                c.RideBike(bike, spot, bikeIdx);
                Active.Add(c);
                c.State = Customer.St.Arriving;
                c.MoveTo(() =>
                {
                    c.MoveTo(() => { c.Dismount(); Route(c); }, spot);
                }, new Vector3(spot.x, 0, spot.z - 0.45f));
            }
            else
            {
                var start = new Vector3(sx, 0, Location.StreetZ);    // walking along the road edge; in through the wall's opening
                c = Customer.Create(this, cfg, order, start, rng);
                Active.Add(c);
                c.State = Customer.St.Arriving;
                Route(c);
            }
        }

        void Route(Customer c)
        {
            if (CounterQueued() < CounterCap)
            {
                var lane = PickLane();
                if (lane != null) { JoinLane(lane, c); return; }
            }
            // roadside wait
            int idx = waiting.Count % Mathf.Max(1, Loc.WaitSpots.Count);
            waiting.Add(c);
            c.WaitAt(Loc.WaitSpots[idx] + new Vector3(Random.Range(-0.2f, 0.2f), 0, Random.Range(-0.15f, 0.15f)));
        }

        bool SpawnDiners()
        {
            var table = Loc.TableWithFreeSeats(1);
            if (table == null) return false;
            var cfg = Game.I.Eco.Customer("diner");
            if (cfg == null) return false;
            int size = Mathf.Min(table.FreeSeats, rng.NextDouble() < 0.25 ? 1 : rng.NextDouble() < 0.6 ? 2 : 3);
            bool left = Loc.LeftOpen && (table.Center.x < 0 || rng.NextDouble() < 0.35);
            float sx = left ? -Loc.SpawnX : Loc.SpawnX;
            for (int i = 0; i < size; i++)
            {
                int seat = table.FindFreeSeat();
                if (seat < 0) break;
                var start = new Vector3(sx + (left ? -1 : 1) * i * 0.9f, 0, Location.StreetZ - (i % 2) * 0.25f);
                var c = Customer.Create(this, cfg, MakeOrder(cfg), start, rng);
                Active.Add(c);
                c.GoSit(table, seat);
            }
            return true;
        }

        void SpawnRickshaw(ServicePoint sp)
        {
            var cfg = Game.I.Eco.Customer("rickshaw");
            var order = MakeOrder(cfg);
            var start = new Vector3(-Loc.SpawnX - 4, 0, sp.Slots[0].z);
            var c = Customer.Create(this, cfg, order, start, rng);
            c.transform.rotation = Quaternion.LookRotation(Vector3.right);
            Active.Add(c);
            sp.Join(c);
            Synth.Play("horn", 1f, 0.45f, 0.5f);
        }

        public void FreeBikeSpot(int i)
        {
            if (i >= 0 && i < bikeTaken.Length) bikeTaken[i] = false;
        }
    }
}
