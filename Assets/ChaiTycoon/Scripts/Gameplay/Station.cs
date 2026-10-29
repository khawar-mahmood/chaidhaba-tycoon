using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Cooks only while Rafi or a helper stands on its floor ring: each item goes into the tray beside it
    /// (and straight into the hands of whoever stands there), up to the tray limit. Nobody there, no cooking;
    /// stepping off keeps the half-cooked progress for next time. A hired cook standing behind it keeps it
    /// cooking all the time, so stations can be automated one by one.
    /// </summary>
    public class Station : MonoBehaviour, IZone
    {
        public ItemId Item;
        public string Type;
        public string Id;                // the unlock that built it ("kettle1" for the first kettle)
        public Staff Cook;               // a hired cook standing behind it keeps it cooking on its own
        public Vector3 PickupPos;
        public float PickupRadius = 0.8f;     // a little wider than the 1.35 m ring so stepping on it counts
        public Transform TrayOrigin;
        public readonly List<ItemObj> Tray = new List<ItemObj>();
        public float Timer;
        public bool Built = true;

        readonly Dictionary<Agent, float> pickTimers = new Dictionary<Agent, float>();
        WorldUI.StationTag stationTag;
        WorldUI.RingLabel ringLabel;
        WorldUI.HireCookButton hireCook;
        float fxTimer;
        Hotspot hot;
        bool sideTable;          // kit stalls stack their stock 2 x 2 on a side table instead of a row along the front
        Vector3? steamLocal;     // where the steam rises, from the art's meta

        public int Cap => Game.I.Up.TrayCap(Item);
        public float Prep => Game.I.Up.PrepTime(Item);
        public bool Full => Tray.Count >= Cap;
        /// <summary>Someone (Rafi or a helper) is standing on the ring, so the station is cooking.</summary>
        public bool Manned => pickTimers.Count > 0 || (Cook != null && Cook.AtPost);
        public bool Cooking => Manned && !Full;
        /// <summary>Someone other than this agent is standing at the station.</summary>
        public bool MannedByOther(Agent who)
        {
            foreach (var a in pickTimers.Keys) if (a != who) return true;
            return false;
        }
        bool playerHere;
        /// <summary>Rafi is standing on this station's ring.</summary>
        public bool PlayerHere => playerHere;
        /// <summary>The hire-a-cook card is up for this station (its count tag steps aside).</summary>
        public bool HireCardShown;

        public static Station Create(Transform parent, string id, string type, ItemId item, Vector3 pos, bool startFull)
        {
            string artName = ArtName(type);
            var art = ArtLib.Get(artName);
            // Kit*Station meta: tray origin (x, y, z) and steam point (x, y, z), see export_art.py
            var meta = art ? ArtLib.Meta(artName) : null;
            bool kit = meta != null && meta.Length >= 6;
            var go = Models.Obj("station_" + type, art ? art : Models.StationMesh(type), parent);
            go.transform.position = pos;
            var st = go.AddComponent<Station>();
            st.Id = id;
            st.Type = type;
            st.Item = item;
            st.PickupPos = pos + new Vector3(0, 0, -1.55f);
            st.sideTable = kit;
            if (kit && (meta[3] != 0 || meta[4] != 0)) st.steamLocal = new Vector3(meta[3], meta[4], meta[5]);
            if (kit) st.AddPanFood();
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.6f, 0.06f);
            col.size = new Vector3(kit ? 2.2f : 2.04f, 1.2f, 1.2f);
            NavGrid.MarkDirty();
            var tray = new GameObject("tray").transform;
            tray.SetParent(go.transform, false);
            // the older Blender stations keep a front strip clear for the stock; their top is a little lower
            tray.localPosition = kit ? new Vector3(meta[0], meta[1], meta[2])
                : art ? new Vector3(0.14f, 0.98f, -0.38f) : new Vector3(0.14f, 1.03f, -0.36f);
            st.TrayOrigin = tray;
            st.hot = Hotspot.Attach(Models.AlphaQuad("pickupMark", go.transform, SpriteGen.RingSprite(128, 10).texture, new Vector3(0, 0.02f, -1.55f), new Vector2(1.35f, 1.35f), Pal.WithA(Color.white, 0.45f), false), Pal.Marigold);
            Zones.Add(st);
            st.stationTag = Game.I.WUI.AddStationTag(st);
            st.hireCook = Game.I.WUI.AddHireCook(st);
            // no name pill under the station ring: the counter sits right in front of the stations, so it landed
            // on the counter tops (the stove and its food say what it makes)
            if (startFull)
            {
                int n = st.Cap;
                for (int i = 0; i < n; i++) st.Produce(false);
            }
            return st;
        }

        // food that always sits in the pan once the station is built: a paratha on the tawa, samosas in the
        // fryer's oil. Positions are the kit stoves' cooking surfaces (measured in Blender); the steam rises there too.
        readonly List<Transform> panFood = new List<Transform>();

        void AddPanFood()
        {
            Vector3 c; Vector3[] spots;
            if (Type == "tawa") { c = new Vector3(-0.32f, 0.705f, 0f); spots = new[] { Vector3.zero }; }
            else if (Type == "fryer")
            {
                c = new Vector3(-0.33f, 0.77f, -0.05f);
                spots = new[] { new Vector3(-0.11f, 0, -0.05f), new Vector3(0.09f, 0, -0.08f), new Vector3(0f, 0, 0.1f) };
            }
            else return;
            steamLocal = c + new Vector3(0, 0.12f, 0);
            var mesh = Models.ItemMesh(Item);
            for (int i = 0; i < spots.Length; i++)
            {
                var f = Models.Obj("panFood", mesh, transform);
                f.transform.localPosition = c + spots[i];
                f.transform.localRotation = Quaternion.Euler(0, i * 120f + 20f, 0);
                panFood.Add(f.transform);
            }
        }

        void TickPanFood(bool cooking)
        {
            // a little sizzle while it cooks
            for (int i = 0; i < panFood.Count; i++)
            {
                var f = panFood[i];
                float k = cooking ? Mathf.Sin(Time.time * 9f + i * 2.1f) : 0f;
                f.localScale = Vector3.one * (1f + 0.04f * k);
                if (Type == "fryer") f.localRotation = Quaternion.Euler(cooking ? k * 4f : 0, i * 120f + 20f, 0);
            }
        }

        static string ArtName(string type)
        {
            string kit = type == "kettle" ? "KitChaiStation" : type == "tawa" ? "KitTawaStation" : type == "fryer" ? "KitKarahiStation" : null;
            if (ArtLib.Has(kit)) return kit;
            switch (type)
            {
                case "kettle": return "KettleStation";
                case "tawa": return "TawaStation";
                case "fryer": return "FryerStation";
                default: return null;
            }
        }

        Vector3 SlotLocal(int i)
        {
            if (sideTable)
            {
                int c = i % 2, row = (i / 2) % 2, lay = i / 4;
                return new Vector3((c - 0.5f) * 0.34f, lay * Items.Height(Item), row * 0.36f);
            }
            int col = i % 4, layer = i / 4;
            return new Vector3((col - 1.5f) * 0.42f, layer * Items.Height(Item), 0);
        }

        void Produce(bool fx)
        {
            var go = Models.Item(Item);
            var it = go.GetComponent<ItemObj>();
            go.transform.SetParent(TrayOrigin, false);
            go.transform.localPosition = SlotLocal(Tray.Count);
            go.transform.localRotation = Quaternion.Euler(0, Random.Range(-15f, 15f), 0);
            Tray.Add(it);
            if (fx) Tw.PopIn(go.transform, 0.3f);
            GameEvents.ItemMade?.Invoke(Item);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            int cap = Cap;
            // only cooks while someone stands on the ring; half-cooked progress waits for them to come back
            bool cooking = Manned && Tray.Count < cap;
            if (cooking)
            {
                Timer += dt;
                if (Timer >= Prep)
                {
                    Timer = 0;
                    int n = Mathf.Min(Game.I.Up.Batch(Item), cap - Tray.Count);
                    for (int i = 0; i < n; i++) Produce(true);
                    if (Type == "fryer") Synth.Play("sizzle", 1f, 0.5f, 0.5f);
                    if (IsVisible()) Synth.Play("pop", 0.8f, 0.35f, 0.15f);
                }
            }
            else if (Tray.Count >= cap) Timer = 0;
            if (panFood.Count > 0) TickPanFood(cooking);
            // Rafi's ring fills up as the next item cooks
            if (playerHere) hot.SetProgress(cooking ? Mathf.Clamp01(Timer / Prep) : 0f);

            fxTimer -= dt;
            if (fxTimer <= 0)
            {
                fxTimer = Random.Range(0.5f, 0.9f);
                if (Type == "kettle") Game.I.Fx.Steam(SteamAt(new Vector3(-0.36f, 1.85f, 0.24f)), cooking ? 1f : 0.5f);
                else if (Type == "fryer" && cooking) Game.I.Fx.Steam(SteamAt(new Vector3(-0.24f, 1.5f, 0.22f)), 0.6f);
                else if (Type == "tawa" && cooking) Game.I.Fx.Steam(SteamAt(new Vector3(-0.3f, 1.3f, 0.22f)), 0.5f);
            }
        }

        Vector3 SteamAt(Vector3 fallback) => transform.TransformPoint(steamLocal ?? fallback);

        bool IsVisible()
        {
            var cam = Camera.main;
            if (!cam) return false;
            var v = cam.WorldToViewportPoint(transform.position);
            return v.z > 0 && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
        }

        public ItemObj TakeOne()
        {
            if (Tray.Count == 0) return null;
            var it = Tray[Tray.Count - 1];
            Tray.RemoveAt(Tray.Count - 1);
            return it;
        }

        // ---------- IZone ----------
        public bool ZoneActive => Built;
        public bool Contains(Vector3 p, float extra) => Zones.Flat(p, PickupPos) < PickupRadius + extra;

        public void OnEnter(Agent who) { pickTimers[who] = who.PickInterval; if (who.IsPlayer) { playerHere = true; hot.SetOn(true); } }

        public void OnStay(Agent who, float dt)
        {
            if (!pickTimers.TryGetValue(who, out var t)) t = who.PickInterval;
            t += dt;
            float interval = who.PickInterval;
            while (t >= interval && Tray.Count > 0 && !who.Stack.IsFull)
            {
                t -= interval;
                var it = TakeOne();
                who.Stack.Add(it);
                if (who.IsPlayer)
                {
                    Synth.Play("clink", 1f + Random.Range(-0.1f, 0.1f), 0.35f);
                    GameEvents.ItemPicked?.Invoke(Item);
                }
            }
            if (Tray.Count == 0 || who.Stack.IsFull) t = Mathf.Min(t, interval);
            pickTimers[who] = t;
        }

        public void OnExit(Agent who)
        {
            pickTimers.Remove(who);
            if (who.IsPlayer) { playerHere = false; hot.SetOn(false); hot.SetProgress(0); }
        }

        void OnDestroy()
        {
            Zones.Remove(this);
            if (stationTag != null) stationTag.Remove();
            ringLabel?.Remove();
            hireCook?.Remove();
        }
    }
}
