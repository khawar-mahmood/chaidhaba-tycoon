using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// A playable location. Every location keeps the same logic: road and arrivals at the bottom,
    /// counter in the middle, stations along the back wall, and the expansion tile at the edge.
    /// </summary>
    public class Location : MonoBehaviour
    {
        public int Id;
        public LocationCfg Cfg;
        public LocSave LS;

        public Vector3 PlayerSpawn = new Vector3(-3.5f, 0, 2.6f);
        public Bounds CamBounds;
        public float CamDistance = 21f;
        public float SidewalkZ = -5.5f, RoadNearZ = -7.0f, RoadFarZ = -8.9f, SpawnX = 18f;

        // Customers walk along the road edge outside the front wall (z -6.75) and come in through its opening in
        // front of the queues; they leave the same way. Students park their bikes at the kerb right of the opening.
        public const float StreetZ = -7.15f, BikeZ = -7.65f;
        const float FrontWallZ = -6.75f, GateMinX = -4.2f, GateMaxX = 2.2f, GateInZ = -5.9f;

        public static bool OnStreet(Vector3 p) => p.z < FrontWallZ + 0.2f;

        /// <summary>The left end of the road edge is free to walk (rickshaws park there once the stand is built).</summary>
        public bool LeftOpen => RickshawSpots.Count == 0;

        /// <summary>The walk from <paramref name="from"/> through <paramref name="then"/>: in through the wall's opening first when starting on the street.</summary>
        public Vector3[] ViaGate(Vector3 from, params Vector3[] then)
        {
            if (!OnStreet(from) || then.Length == 0) return then;
            float gx = Mathf.Clamp(then[0].x, GateMinX, GateMaxX);
            var pts = new List<Vector3> { new Vector3(gx, 0, StreetZ), new Vector3(gx, 0, GateInZ) };
            pts.AddRange(then);
            return pts.ToArray();
        }

        /// <summary>The walk from inside the yard out through the opening and along the road edge to <paramref name="streetX"/>.</summary>
        public Vector3[] ToStreet(Vector3 from, float streetX)
        {
            var pts = new List<Vector3>();
            if (!OnStreet(from))
            {
                float gx = Mathf.Clamp(from.x, GateMinX, GateMaxX);
                pts.Add(new Vector3(gx, 0, GateInZ));
                pts.Add(new Vector3(gx, 0, StreetZ));
            }
            pts.Add(new Vector3(streetX, 0, StreetZ));
            return pts.ToArray();
        }
        const float TillX = 4.15f;          // the till stands at the right end of the counters
        /// <summary>Depth of the serving counter (and till): 2 m nearer the road than the first layout, so the cooking
        /// row and the counter have room between them. Lanes, the cash pile, queue and staff posts follow it.</summary>
        public const float CounterZ = -1.8f;
        const float LaneZ = CounterZ + 1.6f;   // where Rafi hands food over, on the yard side
        public bool QueueBarriers = false;  // the reference layout has customers line up without barriers
        public Vector3 StaffEntry = new Vector3(9.2f, 0, 0.4f);
        public Vector3 StaffInside = new Vector3(4.2f, 0, 1.6f);

        public readonly List<Station> Stations = new List<Station>();
        public readonly List<ServicePoint> Lanes = new List<ServicePoint>();
        public readonly List<ServicePoint> RickshawSpots = new List<ServicePoint>();
        public readonly List<DiningTable> Tables = new List<DiningTable>();
        public readonly List<Vector3> WaitSpots = new List<Vector3>();
        public readonly List<Vector3> BikeSpots = new List<Vector3>();
        public readonly Dictionary<string, UnlockTile> Tiles = new Dictionary<string, UnlockTile>();
        public readonly List<Staff> StaffList = new List<Staff>();
        public CashPile StandPile;
        /// <summary>Each counter queue has its own cash pile on its counter (collected by standing at the queue).</summary>
        public readonly List<CashPile> LanePiles = new List<CashPile>();
        /// <summary>The counter pile holding the most cash (null before the first counter).</summary>
        public CashPile CounterPile
        {
            get
            {
                CashPile best = null;
                foreach (var p in LanePiles) if (p && (best == null || p.Amount > best.Amount)) best = p;
                return best;
            }
        }

        readonly Dictionary<string, Action<bool>> builders = new Dictionary<string, Action<bool>>();
        readonly Dictionary<string, Vector3> tilePos = new Dictionary<string, Vector3>();
        readonly Dictionary<string, Vector3> staffPosts = new Dictionary<string, Vector3>();
        Transform world;
        Traffic traffic;

        public static Location Create(int id, GameObject go)
        {
            var l = go.AddComponent<Location>();
            l.Id = id;
            l.Cfg = Game.I.LocCfg;
            l.LS = Game.I.LS;
            return l;
        }

        public bool Has(string id) => LS.unlocks.Contains(id);
        public int Stars => Mathf.Clamp(LS.stars, 1, 5);

        public float SpawnInterval
        {
            get
            {
                var arr = Cfg.spawnByStar;
                if (arr == null || arr.Length == 0) return 6f;
                return arr[Mathf.Clamp(Stars - 1, 0, arr.Length - 1)];
            }
        }

        public List<ItemId> UnlockedItems()
        {
            var list = new List<ItemId>();
            foreach (var m in Cfg.menu)
            {
                var id = Items.Parse(m);
                foreach (var s in Stations) if (s.Item == id && !list.Contains(id)) { list.Add(id); break; }
            }
            return list;
        }

        public bool AllUnlocksDone()
        {
            foreach (var u in Cfg.unlocks) if (u.kind != "expand" && !Has(u.id)) return false;
            return true;
        }

        // ------------------------------------------------------------------ build

        public void Build()
        {
            world = new GameObject("world").transform;
            world.SetParent(transform, false);
            BuildStall();
            // saves from before the first kettle and chai counter were unlocks: they already had both
            if (Id == 1 && LS.unlocks.Count > 0)
            {
                if (!Has("kettle1")) LS.unlocks.Add("kettle1");
                if (!Has("lane1")) LS.unlocks.Add("lane1");
            }
            foreach (var u in Cfg.unlocks)
            {
                if (Has(u.id) && builders.TryGetValue(u.id, out var b)) b(true);
            }
            CreateTiles();
            RefreshTiles(false);
            traffic = gameObject.AddComponent<Traffic>();
            traffic.Init(this);
            RefreshDecor();
            GameEvents.LevelUp += OnLevelUp;
        }

        public void Teardown()
        {
            foreach (var s in StaffList) if (s) Destroy(s.gameObject);
            StaffList.Clear();
        }

        // ---------------- the dhaba fills out as you level up: one more piece of decor per level ----------------

        // each piece with a first-choice spot and a fallback (stage 1 yard edges)
        static readonly (string prop, Vector3[] spots)[] Growth =
        {
            ("KitPottedPlant", new[] { new Vector3(-8.1f, 0, 2.4f), new Vector3(-8.2f, 0, 1.2f) }),
            ("KitPottedPlant", new[] { new Vector3(5.2f, 0, 0.9f), new Vector3(4.9f, 0, 1.6f) }),
            ("KitWaterDrum", new[] { new Vector3(6.0f, 0, 6.9f), new Vector3(9.4f, 0, 6.4f) }),
            ("KitChaiCart", new[] { new Vector3(9.4f, 0, -2.4f), new Vector3(9.6f, 0, -1.2f) }),
            ("KitBucket", new[] { new Vector3(-7.9f, 0, 0.6f), new Vector3(-8.3f, 0, 1.8f) }),
            ("KitGasCylinder", new[] { new Vector3(3.8f, 0, 6.9f), new Vector3(9.2f, 0, 5.2f) }),
            ("KitPottedPlant", new[] { new Vector3(9.6f, 0, 3.6f), new Vector3(9.6f, 0, 4.8f) }),
            ("KitRugRack", new[] { new Vector3(-8.6f, 0, 4.6f), new Vector3(9.6f, 0, 0.4f) }),
        };

        // room the game needs later: the station row, counters and their queues, tables, unlock and helper tiles,
        // the rickshaw stand and the road approach
        static readonly Bounds[] KeepClear =
        {
            new Bounds(new Vector3(-2.75f, 0, 5.4f), new Vector3(12.0f, 4, 2.2f)),      // stations
            new Bounds(new Vector3(-0.65f, 0, -1.1f), new Vector3(10.9f, 4, 3.8f)),     // counters, till, serve rings
            new Bounds(new Vector3(-1.2f, 0, 1.3f), new Vector3(9.8f, 4, 2.2f)),        // helper tiles behind the counters
            new Bounds(new Vector3(-1.2f, 0, -4.2f), new Vector3(8.6f, 4, 4.6f)),       // the four queues
            new Bounds(new Vector3(6.2f, 0, 3.3f), new Vector3(3.6f, 4, 3.2f)),         // table 1 (statics initialise in file order: no Table1 here)
            new Bounds(new Vector3(6.2f, 0, -0.9f), new Vector3(3.6f, 4, 3.2f)),        // table 2
            new Bounds(new Vector3(10.6f, 0, 1.7f), new Vector3(3.2f, 4, 3.2f)),        // expand
            new Bounds(new Vector3(8.6f, 0, 1.3f), new Vector3(2.2f, 4, 2.2f)),         // table helper
            new Bounds(new Vector3(-7.6f, 0, -1.2f), new Vector3(2.2f, 4, 2.2f)),       // rickshaw helper
            new Bounds(new Vector3(7.6f, 0, 5.6f), new Vector3(2.2f, 4, 2.2f)),         // canopy
            new Bounds(new Vector3(-6.85f, 0, -3.2f), new Vector3(3.2f, 4, 3.2f)),      // rickshaw stand
            new Bounds(new Vector3(0, 0, -6.0f), new Vector3(40f, 4, 5.0f)),            // road approach
        };

        int growthBuilt;

        void OnLevelUp(int _) { if (Game.I.Progression != null) RefreshGrowth(Game.I.Progression.DisplayLevel, true); }
        void OnDestroy() => GameEvents.LevelUp -= OnLevelUp;

        /// <summary>Adds the decor for every level reached so far (stage 1); new pieces pop in with a puff.</summary>
        public void RefreshGrowth(int level, bool animate)
        {
            if (Id != 1) return;
            int want = Mathf.Clamp(level - 1, 0, Growth.Length);
            while (growthBuilt < want)
            {
                PlaceGrowth(Growth[growthBuilt].prop, Growth[growthBuilt].spots, animate);
                growthBuilt++;
            }
        }

        void PlaceGrowth(string prop, Vector3[] spots, bool animate)
        {
            if (!ArtLib.Has(prop)) return;
            Physics.SyncTransforms();     // colliders built this frame (load, an unlock) count for the overlap check
            var go = ArtLib.Obj(prop, world);
            if (!go) return;
            var mf = go.GetComponent<MeshFilter>();
            var size = mf && mf.sharedMesh ? mf.sharedMesh.bounds.size : Vector3.one * 0.6f;
            foreach (var spot in spots)
            {
                var footprint = new Bounds(spot, new Vector3(size.x + 0.4f, 4, size.z + 0.4f));
                bool clear = true;
                foreach (var k in KeepClear) if (k.Intersects(footprint)) { clear = false; break; }
                // nothing solid already there (walls, props from the layout, built things)
                if (clear && Physics.CheckBox(spot + Vector3.up, new Vector3(size.x * 0.5f + 0.2f, 0.9f, size.z * 0.5f + 0.2f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    clear = false;
                if (!clear) continue;
                go.transform.position = spot;
                go.transform.rotation = Quaternion.Euler(0, growthBuilt * 67f, 0);     // the same turn every load
                AddPropCollider(go, prop);
                if (animate)
                {
                    PopIn(go);
                    Game.I.Fx.Puff(spot, 6, Pal.Dirt, 0.4f);
                }
                return;
            }
            Destroy(go);     // both spots taken: skip this piece
        }

        void BuildStall()
        {
            // the camera follows Rafi at a fixed angle, 20% closer than the first layout (23 m); the bounds only stop it
            // at the yard's edges
            CamBounds = new Bounds(new Vector3(0.5f, 0, -0.6f), new Vector3(12f, 0, 8.4f));
            CamDistance = 18.4f;
            PlayerSpawn = new Vector3(-5.6f, 0, 2.5f);     // a step from the first kettle: the first pickup within seconds

            if (ArtLib.Has("Env_Stage1")) BuildStallArt();
            else BuildStallProcedural();

            // player bounds (customers walk in from beyond them, so walking routes ignore these)
            AddCollider(new Vector3(0, 1, -6.3f), new Vector3(40, 2, 0.6f), NavGrid.FenceName);
            AddCollider(new Vector3(-11.6f, 1, 0), new Vector3(0.6f, 2, 30), NavGrid.FenceName);
            AddCollider(new Vector3(11.8f, 1, 0), new Vector3(0.6f, 2, 30), NavGrid.FenceName);
            BuildStallLogic();
        }

        // Things the game builds itself (stations, counters, tables...) or that would sit in the customers' path.
        static readonly HashSet<string> managedArt = new HashSet<string>
        {
            "KettleStation", "TawaStation", "FryerStation", "CounterChai", "CounterSamosa", "TillTable", "DiningTable",
            "KitChaiStation", "KitTawaStation", "KitKarahiStation", "KitDiningSet", "KitCounter", "KitTill", "KitCanopy", "KitQueueBarrier", "KitRickshawStand", "KitCharpaiTable",
            "KitchenCanopy", "RickshawStand", "Rickshaw", "Car", "CarRed", "CarWhite", "CarBlue", "TruckPink", "TruckTeal",
            "TruckMarigold", "QueuePost", "QueueRope", "RopePost", "Steam", "Barrel"
        };

        /// <summary>Stage 1 dressed with the Blender art: environment mesh, back wall, greenery and signs.</summary>
        void BuildStallArt()
        {
            ArtLib.Obj("Env_Stage1", world, false);
            foreach (var e in ArtLib.Layout("Stage1") ?? new List<ArtLib.Entry>())
            {
                if (managedArt.Contains(e.name)) continue;
                // keep the customers' approach (sidewalk, wait spots, bike spots) clear
                bool approach = e.pos.z < -3.8f && e.pos.z > -6.6f && e.pos.x > -9.5f && e.pos.x < 9.6f;
                if (approach && e.name != "GrassTuft" && !e.name.StartsWith("Kit")) continue;   // kit pieces are placed with this in mind
                var go = ArtLib.Obj(e.name, world, e.name != "GrassTuft" && e.name != "Rock");
                if (!go) continue;
                go.transform.position = e.pos;
                go.transform.rotation = Quaternion.Euler(0, e.rotY, 0);
                go.transform.localScale = Vector3.one * e.scale;
                if (e.name.StartsWith("Kit")) AddPropCollider(go, e.name);
            }
            // the stone walls get their colliders from AddPropCollider; this keeps the back edge closed regardless
            AddCollider(new Vector3(0, 1, 7.9f), new Vector3(19f, 2, 0.4f));
        }

        void BuildStallProcedural()
        {
            var flat = new MeshBuilder();
            var props = new MeshBuilder();

            // ground, yard, road
            flat.FlatQuad(new Vector3(0, -0.01f, 0), new Vector2(90, 60), Pal.Sand);
            flat.FlatQuad(new Vector3(0, 0.0f, 3.9f), new Vector2(16.6f, 6.8f), Pal.Hex("#E3BC92"));
            for (int x = -8; x < 8; x++)
                for (int z = 1; z < 7; z++)
                    if ((x + z) % 2 == 0) flat.FlatQuad(new Vector3(x + 0.5f, 0.004f, z + 0.4f), new Vector2(0.96f, 0.96f), Pal.Hex("#DDB389"));
            flat.FlatQuad(new Vector3(0, 0.003f, SidewalkZ + 0.15f), new Vector2(90, 1.7f), Pal.Dirt);
            flat.Box(new Vector3(0, 0.05f, -6.4f), new Vector3(90, 0.1f, 0.3f), Pal.Stone);
            flat.FlatQuad(new Vector3(0, 0.005f, -8.1f), new Vector2(90, 3.4f), Pal.Asphalt);
            for (int i = -22; i <= 22; i++) flat.FlatQuad(new Vector3(i * 2.2f, 0.01f, -8.05f), new Vector2(1.1f, 0.12f), Pal.Hex("#F3EDE2"));
            flat.Box(new Vector3(0, 0.05f, -9.85f), new Vector3(90, 0.1f, 0.3f), Pal.Stone);

            // back wall with a truck-art border and side walls
            flat.Box(new Vector3(0, 1.1f, 7.6f), new Vector3(17.2f, 2.2f, 0.4f), Pal.Clay);
            flat.Box(new Vector3(0, 2.25f, 7.6f), new Vector3(17.5f, 0.12f, 0.5f), Pal.Hex("#A86C45"));
            props.Box(new Vector3(0, 1.75f, 7.38f), new Vector3(16.4f, 0.14f, 0.05f), Pal.Teal);
            props.Box(new Vector3(0, 0.35f, 7.38f), new Vector3(16.4f, 0.14f, 0.05f), Pal.TruckPink);
            for (int i = 0; i < 12; i++)
            {
                float x = -7.6f + i * 1.38f;
                props.Prism(new Vector3(x, 1.05f, 7.37f), MeshBuilder.StarOutline(6, 0.2f, 0.09f), 0.03f, i % 2 == 0 ? Pal.Marigold : Pal.TruckPink, Quaternion.Euler(-90, 0, 0));
            }
            flat.Box(new Vector3(-8.5f, 0.55f, 5.1f), new Vector3(0.35f, 1.1f, 5.4f), Pal.Clay);
            flat.Box(new Vector3(8.5f, 0.55f, 5.1f), new Vector3(0.35f, 1.1f, 5.4f), Pal.Clay);
            flat.Box(new Vector3(-8.5f, 1.14f, 5.1f), new Vector3(0.45f, 0.08f, 5.5f), Pal.Hex("#A86C45"));
            flat.Box(new Vector3(8.5f, 1.14f, 5.1f), new Vector3(0.45f, 0.08f, 5.5f), Pal.Hex("#A86C45"));
            AddCollider(new Vector3(0, 1, 7.6f), new Vector3(17.2f, 2, 0.4f));
            AddCollider(new Vector3(-8.5f, 1, 5.1f), new Vector3(0.35f, 2, 5.4f));
            AddCollider(new Vector3(8.5f, 1, 5.1f), new Vector3(0.35f, 2, 5.4f));

            // hand-painted sign over the back wall (kettle picture, no baked text)
            props.BevelBox(new Vector3(0, 2.95f, 7.55f), new Vector3(4.4f, 1.3f, 0.14f), 0.1f, Pal.TruckPink);
            props.Box(new Vector3(0, 2.95f, 7.47f), new Vector3(4.0f, 0.95f, 0.04f), Pal.Marigold);
            props.Sphere(new Vector3(0, 2.9f, 7.43f), new Vector3(0.62f, 0.52f, 0.05f), Pal.Teal, 0);
            props.Box(new Vector3(0.42f, 2.98f, 7.43f), new Vector3(0.36f, 0.09f, 0.05f), Pal.Teal, Quaternion.Euler(0, 0, 35));
            props.Box(new Vector3(0, 3.22f, 7.43f), new Vector3(0.16f, 0.12f, 0.05f), Pal.Teal);
            props.Torus(new Vector3(-0.45f, 2.92f, 7.43f), 0.14f, 0.035f, 8, 4, Pal.Teal, Quaternion.Euler(90, 0, 0));
            for (int s = -1; s <= 1; s += 2)
            {
                props.Prism(new Vector3(s * 1.45f, 2.95f, 7.42f), MeshBuilder.StarOutline(6, 0.3f, 0.14f), 0.03f, Pal.Chili, Quaternion.Euler(-90, 0, 0));
                props.Sphere(new Vector3(s * 1.45f, 2.95f, 7.39f), new Vector3(0.14f, 0.14f, 0.05f), Pal.Marigold, 0);
                props.Prism(new Vector3(s * 0.95f, 3.35f, 7.42f), MeshBuilder.StarOutline(5, 0.12f, 0.05f), 0.03f, Pal.Parrot, Quaternion.Euler(-90, 0, 0));
                props.Prism(new Vector3(s * 0.95f, 2.55f, 7.42f), MeshBuilder.StarOutline(5, 0.12f, 0.05f), 0.03f, Pal.Parrot, Quaternion.Euler(-90, 0, 0));
            }
            props.Box(new Vector3(-1.9f, 2.0f, 7.62f), new Vector3(0.12f, 1.4f, 0.12f), Pal.DarkWood);
            props.Box(new Vector3(1.9f, 2.0f, 7.62f), new Vector3(0.12f, 1.4f, 0.12f), Pal.DarkWood);

            // greenery and props
            Models.Tree(props, new Vector3(-10.6f, 0, 6.6f), 1.1f, 0);
            Models.Tree(props, new Vector3(-12.4f, 0, 2.2f), 1.0f, 1);
            Models.Tree(props, new Vector3(10.9f, 0, 6.2f), 1.15f, 1);
            Models.Tree(props, new Vector3(12.8f, 0, 2.6f), 0.95f, 0);
            Models.Tree(props, new Vector3(-14.5f, 0, -2.6f), 1.0f, 0);
            Models.Tree(props, new Vector3(15f, 0, -3.2f), 1.1f, 1);
            Models.Tree(props, new Vector3(-6f, 0, 10.5f), 1.3f, 0);
            Models.Tree(props, new Vector3(4.5f, 0, 10.8f), 1.2f, 1);
            Models.Bush(props, new Vector3(-9.6f, 0, 4.0f), 1.0f);
            Models.Bush(props, new Vector3(9.8f, 0, 3.6f), 1.1f);
            Models.Bush(props, new Vector3(-2.5f, 0, 9.0f), 1.3f);
            Models.Bush(props, new Vector3(1.5f, 0, 9.2f), 1.1f);
            Models.Bush(props, new Vector3(-11.5f, 0, -4.6f), 0.9f);
            Models.Bush(props, new Vector3(12.2f, 0, -4.4f), 1.0f);
            Models.PotPlant(props, new Vector3(-7.7f, 0, 6.8f), Pal.Clay);
            Models.PotPlant(props, new Vector3(7.7f, 0, 6.8f), Pal.Teal);
            Models.PotPlant(props, new Vector3(2.85f, 0, 0.35f), Pal.TruckPink);
            Models.MilkCan(props, new Vector3(-7.4f, 0, 5.9f));
            Models.MilkCan(props, new Vector3(-7.0f, 0, 6.5f));
            Models.Crate(props, new Vector3(7.3f, 0, 5.8f), 10, Pal.Hex("#B98A5C"));
            Models.Crate(props, new Vector3(7.3f, 0.44f, 5.8f), -6, Pal.Hex("#A87B50"));

            // road sign toward the highway, beside the expansion tile
            props.Box(new Vector3(11.2f, 1.1f, -1.9f), new Vector3(0.12f, 2.2f, 0.12f), Pal.DarkSteel);
            props.Prism(new Vector3(11.2f, 2.1f, -1.96f), new[] { new Vector2(-0.7f, -0.25f), new Vector2(0.4f, -0.25f), new Vector2(0.75f, 0f), new Vector2(0.4f, 0.25f), new Vector2(-0.7f, 0.25f) }, 0.06f, Pal.Parrot, Quaternion.Euler(-90, 0, 0));

            Models.Obj("ground", flat, world, false);
            Models.Obj("props", props, world, true);
        }

        // ---------------- stage 1: four counter queues, each for one kind of order ----------------

        static readonly string[] LaneIds = { "lane1", "lane2", "lane3", "lane4" };
        static readonly float[] LaneXs = { -4.8f, -2.4f, 0f, 2.4f };
        static readonly ItemId?[] LaneItems = { ItemId.Chai, ItemId.Paratha, ItemId.Samosa, null };
        static readonly string[] LaneLabels = { "Chai", "Paratha", "Samosa", "Mixed" };
        const float CounterLen = 2.4f;     // the kit counter (3 m) at 80%, so four queues fit side by side
        static readonly Vector3 Table1 = new Vector3(6.2f, 0, 3.3f), Table2 = new Vector3(6.2f, 0, -0.9f);

        /// <summary>Unlock id of each helper and the job it does: one queue, the rickshaws or the tables.</summary>
        public static readonly Dictionary<string, string> HelperJobs = new Dictionary<string, string>
        {
            { "helper_chai", "lane0" }, { "helper_paratha", "lane1" }, { "helper_samosa", "lane2" }, { "helper_mixed", "lane3" },
            { "helper_rickshaw", "rickshaw" }, { "helper_tables", "tables" },
        };

        /// <summary>Where a helper waits for work: beside its queue, by the rickshaw stand, or between the tables.</summary>
        public Vector3 HelperPost(string job)
        {
            if (job == "rickshaw") return new Vector3(-6.4f, 0, -1.7f);
            if (job == "tables") return new Vector3(8.0f, 0, 1.3f);
            if (!string.IsNullOrEmpty(job) && job.StartsWith("lane") && int.TryParse(job.Substring(4), out int i) && i >= 0 && i < LaneXs.Length)
                return new Vector3(LaneXs[i] + 0.75f, 0, LaneZ + 0.45f);
            return staffPosts.TryGetValue("waiter", out var p) ? p : PlayerSpawn;
        }

        public ServicePoint LaneByIndex(int i)
        {
            foreach (var l in Lanes) if (l.Index == i) return l;
            return null;
        }

        void BuildStallLogic()
        {
            // pre-built: only the till. The first kettle and the chai counter are the first things you unlock.
            var till = ArtLib.Obj(ArtLib.Has("KitTill") ? "KitTill" : "TillTable", world);   // the kit's truck-art table
            if (till) till.transform.position = new Vector3(TillX, 0, CounterZ);
            else
            {
                till = Models.Obj("tillTable", Models.CounterMesh(1.1f), world);
                till.transform.position = new Vector3(TillX, 0, CounterZ);
                var reg = Models.Obj("till", Models.TillMesh(), world);
                reg.transform.position = new Vector3(TillX + 0.1f, 0.85f, CounterZ + 0.22f);
                reg.transform.rotation = Quaternion.Euler(0, 180, 0);
            }
            AddCollider(new Vector3(TillX, 0.5f, CounterZ), new Vector3(1.1f, 1f, 0.9f));

            // tile positions (on the floor where the thing will appear)
            tilePos["kettle1"] = new Vector3(-6.6f, 0, 3.85f);
            tilePos["kettle2"] = new Vector3(-4.4f, 0, 3.85f);
            tilePos["tawa1"] = new Vector3(-2.2f, 0, 3.85f);
            tilePos["tawa2"] = new Vector3(0.0f, 0, 3.85f);
            tilePos["fryer1"] = new Vector3(2.2f, 0, 3.85f);
            for (int i = 0; i < LaneIds.Length; i++) tilePos[LaneIds[i]] = new Vector3(LaneXs[i], 0, LaneZ);
            // helpers: a tile just behind their queue (towards the stoves), by the stand, between the tables
            tilePos["helper_chai"] = new Vector3(LaneXs[0], 0, LaneZ + 1.5f);
            tilePos["helper_paratha"] = new Vector3(LaneXs[1], 0, LaneZ + 1.5f);
            tilePos["helper_samosa"] = new Vector3(LaneXs[2], 0, LaneZ + 1.5f);
            tilePos["helper_mixed"] = new Vector3(LaneXs[3], 0, LaneZ + 1.5f);
            tilePos["helper_rickshaw"] = new Vector3(-7.6f, 0, -1.2f);
            tilePos["helper_tables"] = new Vector3(8.6f, 0, 1.3f);
            tilePos["rickshaw"] = new Vector3(-7.1f, 0, -3.6f);
            tilePos["cashier"] = new Vector3(TillX, 0, LaneZ - 0.1f);
            tilePos["canopy"] = new Vector3(7.6f, 0, 5.6f);
            tilePos["expand"] = new Vector3(10.6f, 0, 1.7f);
            tilePos["table1"] = Table1;
            tilePos["table2"] = Table2;
            staffPosts["cashier"] = new Vector3(TillX, 0, CounterZ + 0.8f);
            staffPosts["waiter"] = new Vector3(-3.1f, 0, LaneZ + 0.4f);

            builders["kettle1"] = inst => AddStation("kettle1", "kettle", ItemId.Chai, new Vector3(-6.6f, 0, 5.4f), inst, false);   // empty: standing at it is how chai gets made
            builders["kettle2"] = inst => AddStation("kettle2", "kettle", ItemId.Chai, new Vector3(-4.4f, 0, 5.4f), inst, false);
            builders["tawa1"] = inst => AddStation("tawa1", "tawa", ItemId.Paratha, new Vector3(-2.2f, 0, 5.4f), inst, false);
            builders["tawa2"] = inst => AddStation("tawa2", "tawa", ItemId.Paratha, new Vector3(0.0f, 0, 5.4f), inst, false);
            builders["fryer1"] = inst => AddStation("fryer1", "fryer", ItemId.Samosa, new Vector3(2.2f, 0, 5.4f), inst, false);
            for (int i = 0; i < LaneIds.Length; i++)
            {
                int idx = i;
                builders[LaneIds[i]] = inst =>
                {
                    var seg = BuildCounter(new Vector3(LaneXs[idx], 0, CounterZ), CounterLen);
                    if (!inst) PopIn(seg);
                    AddLane(idx);
                };
            }
            foreach (var kv in HelperJobs)
            {
                var job = kv.Value;
                builders[kv.Key] = inst => { if (!inst) Hire("waiter", job); };
            }
            builders["rickshaw"] = BuildRickshawStand;
            builders["cashier"] = inst => { if (!inst) Hire("cashier"); };
            builders["canopy"] = inst =>
            {
                // a row of the kit's parasols behind the stations (older art: the striped canopy), and shade over every table
                var c = ArtLib.Obj(ArtLib.Has("KitCanopy") ? "KitCanopy" : "KitchenCanopy", world);
                if (!c) c = Models.Obj("canopy", Models.CanopyMesh(14.0f, 1.4f), world);
                c.transform.position = new Vector3(-2.2f, 0, 6.85f);
                if (!inst) PopIn(c);
                foreach (var t in Tables) AddTableShade(t, inst);
                canopyBuilt = true;
            };
            builders["expand"] = inst => { if (!inst) Game.I.StartExpansion(); };
            builders["table1"] = inst => BuildTable("table1", Table1, 2, inst);
            builders["table2"] = inst => BuildTable("table2", Table2, 2, inst);

            // overflow customers wait just inside the wall, right of the queues (clear of the wash corner's bucket
            // and sink); students park their bikes at the kerb outside, right of the opening
            for (int i = 0; i < 4; i++) WaitSpots.Add(new Vector3(4.0f + i * 0.75f, 0, -5.3f + (i % 2) * 0.35f));
            for (int i = 0; i < 3; i++) BikeSpots.Add(new Vector3(3.3f + i * 0.85f, 0, BikeZ));
        }

        bool canopyBuilt;

        /// <summary>Parasols over a dining table (the shade canopy's row of parasols, smaller).</summary>
        void AddTableShade(DiningTable t, bool instant)
        {
            var c = ArtLib.Obj(ArtLib.Has("KitCanopy") ? "KitCanopy" : "KitchenCanopy", world);
            if (!c) c = Models.Obj("tableShade", Models.CanopyMesh(2.6f, 1.2f), world);
            c.transform.position = t.Center + new Vector3(0, 0, 0.1f);
            c.transform.localScale = Vector3.one * 0.78f;
            if (!instant) PopIn(c);
        }

        /// <summary>Solid footprint for a kit decor prop (bin, planter, chair, lamp), so Rafi walks around it.</summary>
        static void AddPropCollider(GameObject go, string name)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh || name == "KitGrass") return;          // walk-through ground cover
            var b = mf.sharedMesh.bounds;
            var col = go.AddComponent<BoxCollider>();
            NavGrid.MarkDirty();
            if (name == "KitStreetLamp") { col.center = new Vector3(0, 1, 0); col.size = new Vector3(0.4f, 2, 0.4f); return; }   // pole only
            if (name == "KitTree") { col.center = new Vector3(0, 1, 0); col.size = new Vector3(0.6f, 2, 0.6f); return; }        // trunk only
            col.center = new Vector3(b.center.x, 1, b.center.z);
            col.size = new Vector3(b.size.x, 2, b.size.z);
        }

        void AddCollider(Vector3 c, Vector3 size, string name = "col")
        {
            var go = new GameObject(name);
            go.transform.SetParent(world, false);
            go.transform.position = c;
            go.AddComponent<BoxCollider>().size = size;
            NavGrid.MarkDirty();
        }

        GameObject BuildCounter(Vector3 pos, float len)
        {
            // the counter is two of the kit's truck-art tables per lane (older art: painted counters, alternating)
            var c = ArtLib.Obj(ArtLib.Has("KitCounter") ? "KitCounter" : Lanes.Count % 2 == 0 ? "CounterChai" : "CounterSamosa", world);
            if (c) c.transform.localScale = new Vector3(len / 3f, 1, 1);     // the art counters are 3 m long
            else c = Models.Obj("counter", Models.CounterMesh(len), world);
            c.transform.position = pos;
            AddCollider(pos + Vector3.up * 0.5f, new Vector3(len, 1f, 0.9f));
            return c;
        }

        /// <summary>
        /// Opens counter queue <paramref name="index"/> (0 chai, 1 paratha, 2 samosa, 3 mixed): its serve ring on the yard
        /// side, its cash pile on the counter (collected from the ring), and the customers' line towards the road.
        /// </summary>
        void AddLane(int index)
        {
            var drop = new Vector3(LaneXs[index], 0, LaneZ);
            var pile = CashPile.Create(world, new Vector3(drop.x, 0.9f, CounterZ - 0.28f), new Vector3(drop.x, 0, LaneZ + 0.05f));
            LanePiles.Add(pile);
            var sp = ServicePoint.Create(world, "counter", drop, pile, LaneLabels[index]);
            sp.Index = index;
            sp.Only = LaneItems[index];
            // the front customer stands a step back from the counter, the rest spaced out behind them
            // five places, 0.7 m apart, so the last one still sits inside the front wall's opening
            for (int i = 0; i < 5; i++) sp.Slots.Add(new Vector3(drop.x, 0, CounterZ - 1.3f - i * 0.7f));
            sp.FaceDir = Vector3.forward;
            // trays either side of the hand-over spot for things nobody at the front wants
            // two rows of four on the 2.4 m counter (its cash sits in the middle, on the customer side)
            bool artCounter = ArtLib.Has("KitCounter") || ArtLib.Has("CounterChai");    // items sit on the art counter itself
            sp.SetShelf(new[]
            {
                new Vector2(-0.92f, -0.12f), new Vector2(-0.62f, -0.12f), new Vector2(0.62f, -0.12f), new Vector2(0.92f, -0.12f),
                new Vector2(-0.92f, 0.13f), new Vector2(-0.62f, 0.13f), new Vector2(0.62f, 0.13f), new Vector2(0.92f, 0.13f),
            }, CounterZ + 0.05f, artCounter ? 0.9f : 0.85f, !artCounter);   // on the counter top, wherever the counter stands
            Lanes.Add(sp);
            // the reference layout has no queue barriers: customers just line up. (Set QueueBarriers to use the
            // crowd barriers, or the older posts and ropes.)
            if (!QueueBarriers) { }
            else if (ArtLib.Has("KitQueueBarrier"))
            {
                foreach (float side in new[] { -0.6f, 0.6f })
                    for (int i = 0; i < 2; i++)
                    {
                        var b = ArtLib.Obj("KitQueueBarrier", world);
                        b.transform.position = new Vector3(drop.x + side, 0, -1.4f - i * 1.3f);
                        b.transform.rotation = Quaternion.Euler(0, 90, 0);
                    }
            }
            else if (ArtLib.Has("QueuePost"))
            {
                foreach (float side in new[] { -0.6f, 0.6f })
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var p = ArtLib.Obj("QueuePost", world);
                        p.transform.position = new Vector3(drop.x + side, 0, -0.75f - i * 1.3f);
                        if (i < 2)
                        {
                            var r = ArtLib.Obj("QueueRope", world);
                            if (r) r.transform.position = new Vector3(drop.x + side, 0, -0.75f - i * 1.3f);
                        }
                    }
                }
            }
        }

        void AddStation(string id, string type, ItemId item, Vector3 pos, bool instant, bool startFull)
        {
            var st = Station.Create(world, id, type, item, pos, startFull);
            Stations.Add(st);
            if (!instant)
            {
                PopIn(st.gameObject);
                if (type == "kettle") Synth.Play("whistle", 1.1f, 0.5f);
                if (type == "fryer") Synth.Play("sizzle", 1f, 0.6f);
            }
        }

        void BuildTable(string id, Vector3 pos, int seatsPerSide, bool instant)
        {
            var t = DiningTable.Create(world, id, pos, seatsPerSide);
            Tables.Add(t);
            if (canopyBuilt) AddTableShade(t, instant);     // a table added after the shade canopy gets its parasols too
            if (!instant)
            {
                PopIn(t.gameObject);
                // the table appears on its own tile: step Rafi out in front of it
                var pl = Game.I.Player;
                var pp = pl.transform.position;
                if (Mathf.Abs(pp.x - pos.x) < t.HalfW + 0.5f && Mathf.Abs(pp.z - pos.z) < 1.65f)
                    pl.Teleport(new Vector3(pp.x, 0, pos.z - 1.75f), keepStack: true);
            }
        }

        void BuildRickshawStand(bool instant)
        {
            // no stand building: the rickshaws park at the road edge and their drivers' cash drops on the ground
            // beside them (the benches and shelter only crowded the corner next to the chai counter)
            if (!instant)
            {
                Game.I.Fx.Puff(new Vector3(-7.1f, 0.05f, -3.9f), 8, Pal.Marigold, 0.4f);
                Synth.Play("horn", 1f, 0.6f);
            }
            BuildRickshawSpots();
        }

        void BuildRickshawSpots()
        {
            StandPile = CashPile.Create(world, new Vector3(-7.1f, 0.02f, -3.9f), new Vector3(-7.1f, 0, -3.9f));
            foreach (var x in new[] { -8.8f, -5.4f })
            {
                var sp = ServicePoint.Create(world, "rickshaw", new Vector3(x, 0, -5.35f), StandPile);
                sp.Slots.Add(new Vector3(x, 0, RoadNearZ));
                sp.FaceDir = Vector3.right;
                RickshawSpots.Add(sp);
            }
        }

        void PopIn(GameObject go)
        {
            Tw.PopIn(go.transform, 0.4f);
            Game.I.Fx.Puff(go.transform.position, 10, Pal.Dirt, 0.6f);
            Game.I.Cam.Shake(0.12f);
        }

        // ------------------------------------------------------------------ unlock tiles

        void CreateTiles()
        {
            foreach (var u in Cfg.unlocks)
            {
                if (Has(u.id)) continue;
                if (!tilePos.TryGetValue(u.id, out var p)) continue;
                Sprite icon;
                string caption = null;
                float size = 1.4f;      // unlock tiles: a compact card on the floor (the expansion a bit bigger)
                switch (u.kind)
                {
                    case "station":
                        icon = IconBaker.Item(Items.Parse(u.arg));
                        break;
                    case "staff":
                        icon = Game.I.Hud.StaffPortrait(u.arg);
                        caption = u.name;
                        break;
                    case "expand":
                        icon = Skin.Icon(u.icon);
                        caption = u.name;
                        size = 2.2f;
                        break;
                    default:
                        icon = Skin.Icon(u.icon);
                        caption = u.name;
                        break;
                }
                var t = UnlockTile.Create(world, u, p, size, icon, caption);
                var cfg = u;
                t.Completed = () => OnTileCompleted(cfg);
                Tiles[u.id] = t;
            }
        }

        public void RefreshTiles(bool pan)
        {
            var prog = Game.I.Progression;
            if (Cfg.levels != null && Cfg.levels.Length > 0)
            {
                // task-driven: a tile appears when a level task asks for it
                var wanted = new HashSet<string>();
                if (prog != null) foreach (var id in prog.WantedUnlocks()) wanted.Add(id);
                UnlockTile first = null;
                foreach (var u in Cfg.unlocks)
                {
                    if (u.kind == "expand" || Has(u.id) || !Tiles.TryGetValue(u.id, out var t)) continue;
                    bool reveal = wanted.Contains(u.id) || t.Paid > 0;
                    if (reveal && !t.Revealed) { t.SetRevealed(true, pan); if (first == null) first = t; }
                    else if (!reveal && t.Revealed) t.SetRevealed(false, false);
                }
                if (Tiles.TryGetValue("expand", out var exp) && !Has("expand"))
                {
                    if (!exp.Revealed) exp.SetRevealed(true, false);
                    bool open = wanted.Contains("expand");
                    if (open && exp.Locked && pan && first == null) first = exp;
                    exp.Locked = !open;
                }
                if (pan && first != null) Game.I.Cam.PanTo(first.transform.position);
                return;
            }

            int shown = 0;
            bool allDone = true;
            UnlockTile panTo = null;
            foreach (var u in Cfg.unlocks)
            {
                if (u.kind == "expand") continue;
                if (Has(u.id)) continue;
                allDone = false;
                if (!Tiles.TryGetValue(u.id, out var t)) continue;
                bool reveal = shown < 2;
                shown++;
                if (reveal && !t.Revealed)
                {
                    t.SetRevealed(true, pan);
                    if (pan && panTo == null) panTo = t;
                }
                else if (!reveal && t.Revealed) t.SetRevealed(false, false);
            }
            if (Tiles.TryGetValue("expand", out var ex) && !Has("expand"))
            {
                if (!ex.Revealed) ex.SetRevealed(true, false);
                ex.Locked = !allDone;
                if (allDone && pan) panTo = ex;
            }
            if (panTo != null) Game.I.Cam.PanTo(panTo.transform.position);
        }

        void OnTileCompleted(UnlockCfg u)
        {
            var g = Game.I;
            if (!LS.unlocks.Contains(u.id)) LS.unlocks.Add(u.id);
            if (builders.TryGetValue(u.id, out var b)) b(false);
            g.Player.Depenetrate();
            if (u.stars > 0)
            {
                LS.stars = Mathf.Min(5, LS.stars + u.stars);
                GameEvents.StarGained?.Invoke(LS.stars);
                g.AddLeaves(2, transform.position);
                g.Hud.StarGained(LS.stars);
            }
            GameEvents.Unlocked?.Invoke(u.id);
            if (u.kind != "expand") g.Hud.Toast(u.name + "!", u.kind == "station" ? null : u.icon, u.kind == "station" ? IconBaker.Item(Items.Parse(u.arg)) : null);
            RefreshTiles(true);
            g.OnStatsChanged();
            g.Save();
        }

        // ------------------------------------------------------------------ decor cosmetics

        GameObject decor;

        /// <summary>Shop decorations (garlands, menu board, fairy lights) shown in every location.</summary>
        public void RefreshDecor()
        {
            if (decor) Destroy(decor);
            var on = Game.I.S.decorOn;
            if (on.Count == 0) return;
            var mb = new MeshBuilder();
            if (on.Contains("decor_garlands"))
            {
                // marigold garlands draped along the back wall and the counter front
                for (int i = 0; i < 28; i++)
                {
                    float x = -7.8f + i * 0.58f;
                    float y = 2.0f - Mathf.Sin((i % 7) / 6f * Mathf.PI) * 0.35f;
                    mb.Sphere(new Vector3(x, y, 7.36f), 0.2f, i % 3 == 0 ? Pal.Chili : Pal.Marigold, 0);
                }
                foreach (var l in Lanes)
                    for (int i = 0; i < 8; i++)
                    {
                        float x = l.DropPos.x - 1.4f + i * 0.4f;
                        mb.Sphere(new Vector3(x, 0.92f - Mathf.Sin(i / 7f * Mathf.PI) * 0.25f, -0.28f), 0.15f, i % 2 == 0 ? Pal.Marigold : Pal.Chili, 0);
                    }
            }
            if (on.Contains("decor_menu"))
            {
                var p = new Vector3(3.0f, 0, -0.9f);
                mb.Box(p + new Vector3(-0.45f, 0.7f, 0), new Vector3(0.08f, 1.4f, 0.08f), Pal.DarkWood, Quaternion.Euler(-10, 0, 0));
                mb.Box(p + new Vector3(0.45f, 0.7f, 0), new Vector3(0.08f, 1.4f, 0.08f), Pal.DarkWood, Quaternion.Euler(-10, 0, 0));
                mb.BevelBox(p + new Vector3(0, 1.1f, -0.08f), new Vector3(1.1f, 0.9f, 0.08f), 0.06f, Pal.Ink, Quaternion.Euler(-10, 0, 0));
                for (int r = 0; r < 3; r++)
                {
                    mb.Box(p + new Vector3(-0.15f, 1.35f - r * 0.24f, -0.15f + r * 0.04f), new Vector3(0.55f, 0.05f, 0.02f), Pal.Cream, Quaternion.Euler(-10, 0, 0));
                    mb.Sphere(p + new Vector3(0.33f, 1.35f - r * 0.24f, -0.15f + r * 0.04f), new Vector3(0.12f, 0.12f, 0.03f), new[] { Pal.ChaiTea, Pal.Paratha, Pal.Samosa }[r], 0);
                }
            }
            if (on.Contains("decor_lights"))
            {
                for (int i = 0; i < 34; i++)
                {
                    float x = -8f + i * 0.48f;
                    float y = 3.6f - Mathf.Abs(Mathf.Sin(i / 6f * Mathf.PI)) * 0.3f;
                    mb.Sphere(new Vector3(x, y, 7.2f), 0.13f, new[] { Pal.Marigold, Pal.TruckPink, Pal.Teal, Pal.Parrot }[i % 4], 0);
                }
            }
            decor = Models.Obj("decor", mb, world, true);
        }

        // ------------------------------------------------------------------ staff

        /// <summary>Hires a staff member; a helper (waiter) gets its job: "lane0".."lane3", "rickshaw" or "tables".</summary>
        void Hire(string role, string job = "")
        {
            var data = new StaffSave { role = role, assigned = job };
            LS.staff.Add(data);
            var post = role == "waiter" ? HelperPost(job) : staffPosts.TryGetValue(role, out var p) ? p : PlayerSpawn;
            var s = Staff.Create(this, role, data, post, true);
            StaffList.Add(s);
            GameEvents.Hired?.Invoke(role);
            Game.I.Hud.Refresh();
        }

        public void RestoreStaff()
        {
            foreach (var s in StaffList) if (s) Destroy(s.gameObject);
            StaffList.Clear();
            foreach (var d in LS.staff)
            {
                Vector3 post;
                if (d.role == "cook")
                {
                    var st = StationById(d.assigned);
                    if (st == null) continue;                      // its station isn't in this location
                    post = CookPost(st);
                }
                else post = d.role == "waiter" ? HelperPost(d.assigned) : staffPosts.TryGetValue(d.role, out var p) ? p : PlayerSpawn;
                var s = Staff.Create(this, d.role, d, post, false);
                StaffList.Add(s);
            }
        }

        // ---------------- cooks: one per station, so stations can be automated one by one ----------------

        public Station StationById(string id)
        {
            foreach (var s in Stations) if (s.Id == id) return s;
            return null;
        }

        /// <summary>Behind the stove part of the station (the stock table is on its right), facing the yard.</summary>
        public Vector3 CookPost(Station st) => st.transform.position + new Vector3(-0.35f, 0, 1.2f);

        /// <summary>Cooks walk round the right end of the station row and along the back to their station.</summary>
        public Vector3[] CookRoute(Vector3 post)
        {
            float backZ = post.z;
            float endX = 3.9f;
            foreach (var s in Stations) endX = Mathf.Max(endX, s.transform.position.x + 1.6f);
            return new[] { StaffEntry, StaffInside, new Vector3(endX, 0, 3.2f), new Vector3(endX, 0, backZ), post };
        }

        int CountRole(string role)
        {
            int n = 0;
            foreach (var d in LS.staff) if (d.role == role) n++;
            return n;
        }

        /// <summary>
        /// Cooks become available with the level that asks for one (level 4 of the roadside stall); a location
        /// without levels opens them with the first helper.
        /// </summary>
        public bool CooksAvailable
        {
            get
            {
                if (CountRole("cook") > 0) return true;
                var p = Game.I.Progression;
                if (p != null && p.HasLevels) return p.Reached("hire", "cook");
                return HelperCount > 0;
            }
        }

        public double CookCost => Game.I.Eco.rules.cookCost * Math.Pow(Game.I.Eco.rules.cookCostGrowth, CountRole("cook"));

        public bool CanHireCook(Station st) => CooksAvailable && st != null && st.Cook == null && st.Built;

        /// <summary>Hires a cook for a stove: free after a rewarded ad (the hire card), or for cash.</summary>
        public bool HireCook(Station st, bool free = false)
        {
            if (!CanHireCook(st)) return false;
            var g = Game.I;
            if (!free && !g.Spend(CookCost)) return false;
            var data = new StaffSave { role = "cook", assigned = st.Id };
            LS.staff.Add(data);
            var s = Staff.Create(this, "cook", data, CookPost(st), true);
            StaffList.Add(s);
            GameEvents.Hired?.Invoke("cook");
            Synth.Play("buy");
            g.Hud.Refresh();
            g.Save();
            return true;
        }

        public int HelperCount => CountRole("waiter");

        /// <summary>A helper works this counter queue (its job is "lane0".."lane3").</summary>
        public bool LaneHasHelper(ServicePoint lane)
        {
            if (lane == null) return false;
            string job = "lane" + lane.Index;
            foreach (var d in LS.staff) if (d.role == "waiter" && d.assigned == job) return true;
            return false;
        }

        /// <summary>Helpers now come from their own unlock tiles (one per queue, the rickshaws and the tables), not the Staff panel.</summary>
        public bool CanHireHelper => false;

        public double HelperCost => Game.I.Eco.rules.helperCost * Math.Pow(Game.I.Eco.rules.helperCostGrowth, Math.Max(0, HelperCount - 1));

        public bool HireHelper()
        {
            if (!CanHireHelper) return false;
            var g = Game.I;
            if (!g.Spend(HelperCost)) return false;
            Hire("waiter");
            Synth.Play("buy");
            g.Save();
            return true;
        }

        public bool HasStaff(string role)
        {
            foreach (var s in LS.staff) if (s.role == role) return true;
            return false;
        }

        public void OnStatsChanged()
        {
            foreach (var s in StaffList) if (s) s.RefreshStats();
        }

        // ------------------------------------------------------------------ queries for staff / tutorial

        /// <summary>The station to fetch this item from: stock waiting counts most, then distance. A station someone
        /// else is already standing at (cooking) is avoided, so helpers spread out instead of sharing.</summary>
        public Station BestStation(ItemId item, Vector3 near, Agent who = null)
        {
            if (who == null) who = Game.I.Player;
            Station best = null;
            float bestScore = float.MinValue;
            foreach (var s in Stations)
            {
                if (s.Item != item) continue;
                float score = s.Tray.Count * 3f - Zones.Flat(s.PickupPos, near) * 0.2f - (s.MannedByOther(who) ? 4f : 0f);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        /// <summary>Where the waiter serves: counter lanes and dining tables.</summary>
        public IEnumerable<IServeTarget> WaiterTargets()
        {
            foreach (var l in Lanes) yield return l;
            foreach (var t in Tables) yield return t;
        }

        /// <summary>Everywhere food can be handed over, including the rickshaw stand.</summary>
        public IEnumerable<IServeTarget> AllServeTargets()
        {
            foreach (var l in Lanes) yield return l;
            foreach (var t in Tables) yield return t;
            foreach (var r in RickshawSpots) yield return r;
        }

        public int CounterDemand(ItemId item)
        {
            int n = 0;
            foreach (var l in WaiterTargets()) n += l.Demand(item);
            return n;
        }

        public IServeTarget LaneWanting(CarryStack s)
        {
            foreach (var l in WaiterTargets()) if (l.FrontWantsAnyOf(s)) return l;
            return null;
        }

        public IServeTarget LaneWithDemand(CarryStack s)
        {
            foreach (var l in WaiterTargets())
                foreach (var it in s.items)
                    if (l.Demand(it.id) > 0) return l;
            return null;
        }

        public DiningTable TableWithFreeSeats(int need)
        {
            DiningTable best = null;
            foreach (var t in Tables)
                if (t.FreeSeats >= need && (best == null || t.FreeSeats > best.FreeSeats)) best = t;
            return best;
        }

        public Station FirstStation(ItemId id)
        {
            foreach (var s in Stations) if (s.Item == id) return s;
            return null;
        }

        /// <summary>Cheapest revealed unlock the player can afford right now (for the soft guide arrow).</summary>
        public UnlockTile CheapestAffordable()
        {
            UnlockTile best = null;
            foreach (var t in Tiles.Values)
            {
                if (!t.Affordable) continue;
                if (best == null || t.Remaining < best.Remaining) best = t;
            }
            return best;
        }
    }
}
