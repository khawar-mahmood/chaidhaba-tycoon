using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>Rafi, the cheerful young chaiwala. He only ever moves; zones do the rest.</summary>
    public class Player : Agent
    {
        public override bool IsPlayer => true;
        public override float PickInterval => Game.I.Up.PickInterval;
        public override float ServeInterval => Game.I.Up.ServeInterval;

        CharacterController cc;
        readonly HashSet<IZone> inside = new HashSet<IZone>();
        readonly List<IZone> tmp = new List<IZone>();
        Vector3 velocity;
        float dustTimer, stepTimer;
        public float IdleTime { get; private set; }
        public float Speed { get; private set; }
        public Vector3 Facing => transform.forward;

        public static Player Create(Vector3 pos)
        {
            var go = new GameObject("Rafi");
            go.transform.position = pos;
            var p = go.AddComponent<Player>();
            p.cc = go.AddComponent<CharacterController>();
            p.cc.radius = 0.35f;
            p.cc.height = 1.6f;
            p.cc.center = new Vector3(0, 0.8f, 0);
            p.cc.stepOffset = 0.2f;
            p.cc.skinWidth = 0.03f;
            p.Stack = go.AddComponent<CarryStack>();
            p.BuildRig();
            p.RefreshStats();
            return p;
        }

        void BuildRig()
        {
            var spec = Models.RafiSpec();
            var s = Game.I.S;
            var cap = Game.I.Eco.Cosmetic(s.wornCap);
            var kurta = Game.I.Eco.Cosmetic(s.wornKurta);
            var apron = Game.I.Eco.Cosmetic(s.wornApron);
            if (cap != null) spec.hatColor = Pal.Hex(cap.color);
            if (kurta != null) spec.top = Pal.Lerp(Pal.Hex(kurta.color), Color.white, 0.25f);
            if (apron != null) spec.apronColor = Pal.Hex(apron.color);

            var old = Rig;
            Rig = Models.Character(spec, transform, "rig");
            Rig.transform.localPosition = Vector3.zero;
            Rig.transform.localRotation = Quaternion.identity;
            Stack.rig = Rig;
            Stack.Juicy = true;
            if (old != null)
            {
                foreach (var it in Stack.items) it.transform.SetParent(Rig.carryAnchor, true);
                Destroy(old.gameObject);
            }
            Stack.anchor = Rig.carryAnchor;
            Rig.carrying = Stack.Count > 0;
        }

        public void ApplyCosmetics() => BuildRig();

        public void RefreshStats()
        {
            Stack.capacity = Game.I.Up.Carry;
        }

        static readonly Collider[] overlaps = new Collider[16];

        /// <summary>
        /// Something was just built where Rafi stands (a table pops up on its own tile): push him out of
        /// any colliders he overlaps so he never gets trapped.
        /// </summary>
        public void Depenetrate()
        {
            Physics.SyncTransforms();
            for (int iter = 0; iter < 6; iter++)
            {
                var p = transform.position;
                int n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * 0.4f, p + Vector3.up * 1.2f, cc.radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
                bool moved = false;
                for (int i = 0; i < n; i++)
                {
                    var c = overlaps[i];
                    if (c == null || c == cc) continue;
                    if (Physics.ComputePenetration(cc, transform.position, transform.rotation, c, c.transform.position, c.transform.rotation, out var dir, out var dist))
                    {
                        dir.y = 0;
                        if (dir.sqrMagnitude < 1e-4f) dir = Vector3.back;
                        cc.enabled = false;
                        transform.position += dir.normalized * (dist + 0.08f);
                        cc.enabled = true;
                        moved = true;
                    }
                }
                if (!moved) break;
                Physics.SyncTransforms();
            }
        }

        public void Teleport(Vector3 pos, bool keepStack = false)
        {
            cc.enabled = false;
            transform.position = pos;
            if (!keepStack) transform.rotation = Quaternion.identity;
            cc.enabled = true;
            foreach (var z in inside) z.OnExit(this);
            inside.Clear();
            if (!keepStack) Stack.Clear();
        }

        void Update()
        {
            var g = Game.I;
            var cam = Camera.main;
            Vector2 sp = cam ? (Vector2)cam.WorldToScreenPoint(transform.position + Vector3.up * 0.9f) : Vector2.zero;
            InputReader.Poll(sp, g.WorldPaused || g.Hud.BlocksInput);
            var mv = InputReader.Move;
            float dt = Time.deltaTime;

            if (mv.sqrMagnitude > 0.01f && !g.Started && !g.WorldPaused) g.OnFirstMove();

            // camera looks straight down +z (no yaw), so screen axes map to world x/z
            var dir = new Vector3(mv.x, 0, mv.y);
            float target = g.Up.WalkSpeed * Mathf.Clamp01(dir.magnitude);
            var desired = dir.sqrMagnitude > 0.0001f ? dir.normalized * target : Vector3.zero;
            velocity = Vector3.MoveTowards(velocity, desired, dt * 40f);
            if (dt > 0)
            {
                var before = transform.position;
                cc.Move(velocity * dt);
                var p = transform.position;
                if (Mathf.Abs(p.y) > 0.001f) { cc.enabled = false; p.y = 0; transform.position = p; cc.enabled = true; }
                Speed = Zones.Flat(before, transform.position) / dt;
            }
            if (desired.sqrMagnitude > 0.01f)
            {
                var rot = Quaternion.LookRotation(desired.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1 - Mathf.Exp(-dt * 16f));
                IdleTime = 0;
                dustTimer -= dt;
                if (dustTimer <= 0 && Speed > 3f)
                {
                    dustTimer = Mathf.Lerp(0.35f, 0.18f, (g.Up.WalkSpeed - 4f) / 2f);
                    g.Fx.Puff(transform.position - transform.forward * 0.3f, 2, Pal.Dirt, 0.25f);
                }
                stepTimer -= dt;
                if (stepTimer <= 0) { stepTimer = 0.28f; Synth.Play("step", Random.Range(0.9f, 1.2f), 0.6f); }
            }
            else IdleTime += dt;
            Rig.Tick(Speed, dt);

            if (dt > 0) UpdateZones(dt);
        }

        void UpdateZones(float dt)
        {
            var pos = transform.position;
            tmp.Clear();
            tmp.AddRange(Zones.All);
            foreach (var z in tmp)
            {
                bool now = z.ZoneActive && z.Contains(pos, 0);
                bool was = inside.Contains(z);
                if (now && !was) { inside.Add(z); z.OnEnter(this); }
                else if (!now && was) { inside.Remove(z); z.OnExit(this); }
                if (now) z.OnStay(this, dt);
            }
            inside.RemoveWhere(z => !Zones.All.Contains(z));
        }

        public bool IsInside(IZone z) => inside.Contains(z);
    }
}
