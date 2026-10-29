using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    public class CharSpec
    {
        public Color skin = Pal.Skins[1];
        public Color top = Pal.White;
        public Color bottom = Pal.Hex("#E8E2D6");
        public Color shoes = Pal.Hex("#5A3B2A");
        public Color hair = Pal.HairColors[0];
        public string hat = "none"; // none, cap, topi, turban, beanie
        public Color hatColor = Pal.Marigold;
        public bool apron;
        public Color apronColor = Pal.Chili;
        public float scale = 1f;
        public float headScale = 1f;
        public float bodyWidth = 1f;
        public bool backpack;
        public Color backpackColor = Pal.Royal;
        public bool scarf;
        public Color scarfColor = Pal.Purple;
        public bool glasses;
        public bool beard;
        public bool mustache;
        public bool longHair;
        /// <summary>Blender character to use (Chr_&lt;look&gt;_* in the art pack); null keeps the procedural one.</summary>
        public string look;

        public string Key() =>
            $"{ColorUtility.ToHtmlStringRGB(skin)}{ColorUtility.ToHtmlStringRGB(top)}{ColorUtility.ToHtmlStringRGB(bottom)}{ColorUtility.ToHtmlStringRGB(hair)}" +
            $"{hat}{ColorUtility.ToHtmlStringRGB(hatColor)}{apron}{ColorUtility.ToHtmlStringRGB(apronColor)}{headScale}{bodyWidth}{backpack}{ColorUtility.ToHtmlStringRGB(backpackColor)}{scarf}{ColorUtility.ToHtmlStringRGB(scarfColor)}{glasses}{beard}{mustache}{longHair}";
    }

    /// <summary>Procedural walk / carry / squash animation for every character.</summary>
    public class CharRig : MonoBehaviour
    {
        public Transform vis, legL, legR, armL, armR, carryAnchor, headTop, torso;
        public bool carrying;
        public bool sitting;
        float phase, moveBlend, carryBlend, squashV, squash, waveT = -1, cheerT = -1;
        public float Height = 1.6f;

        // ---------------- skinned crowd models: Mixamo-rigged, legacy clips idle / walk / run / sit ----------------
        Animation anim;
        Transform model, hips, upArmL, upArmR, foreArmL, foreArmR, handL, handR, upLegL, upLegR, kneeL, kneeR;
        string playing;
        float hipRest;
        CrowdRigInfo info;
        /// <summary>Walks only, never runs (customers).</summary>
        public bool NoRun;
        public bool Skinned => anim != null;

        public void SetupSkinned(Animation a, Transform m)
        {
            anim = a;
            model = m;
            info = m.GetComponent<CrowdRigInfo>();
            if (anim)
            {
                anim.cullingType = AnimationCullingType.AlwaysAnimate;   // the hip shift below is added every frame
                foreach (AnimationState s in anim) s.wrapMode = WrapMode.Loop;
            }
            Transform Find(string bone)
            {
                foreach (var t in m.GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith(":" + bone) || t.name == bone) return t;
                return null;
            }
            hips = Find("Hips");
            upArmL = Find("LeftArm"); upArmR = Find("RightArm");
            foreArmL = Find("LeftForeArm"); foreArmR = Find("RightForeArm");
            handL = Find("LeftHand"); handR = Find("RightHand");
            upLegL = Find("LeftUpLeg"); upLegR = Find("RightUpLeg");
            kneeL = Find("LeftLeg"); kneeR = Find("RightLeg");
            if (anim && anim.GetClip("idle")) { anim.Play("idle"); playing = "idle"; anim.Sample(); }
            hipRest = hips ? hips.position.y - transform.position.y : 0.9f;
        }

        /// <summary>Turns a bone so it points from itself towards its child along <paramref name="dir"/> (world), blended by w.</summary>
        static void AimBone(Transform bone, Transform child, Vector3 dir, float w)
        {
            if (!bone || !child) return;
            var cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-8f) return;
            var target = Quaternion.FromToRotation(cur.normalized, dir) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, target, w);
        }

        void Play(string clip, float speed = 1f)
        {
            if (anim.GetClip(clip) == null) clip = "idle";
            if (anim.GetClip(clip) == null) return;
            anim[clip].speed = speed;
            if (playing == clip) return;
            anim.CrossFade(clip, 0.18f);
            playing = clip;
        }

        void TickSkinned(float speed, float dt)
        {
            float moving = Mathf.Clamp01(speed / 2.5f);
            moveBlend = Mathf.MoveTowards(moveBlend, moving, dt * 6f);
            carryBlend = Mathf.MoveTowards(carryBlend, carrying ? 1 : 0, dt * 7f);
            // the clips play in place (their forward drift is taken out in Blender); playback speed follows the
            // ground speed so the feet keep pace: at crowd scale Rafi's walk clip covers ~0.82 m/s, his run ~3 m/s
            if (sitting) Play(anim.GetClip("sit") ? "sit" : "idle");
            else if (speed > 2.6f && !NoRun) Play("run", Mathf.Clamp(speed / 3.0f, 0.8f, 1.5f));
            else if (speed > 0.25f) Play("walk", Mathf.Clamp(speed / 0.82f, 0.7f, 2.6f));
            else Play("idle");
            if (waveT >= 0) { waveT += dt; if (waveT > 1.2f) waveT = -1; }
            if (cheerT >= 0) { cheerT += dt; if (cheerT > 1.0f) cheerT = -1; }
            // squash and stretch spring on top of the animation (same as the procedural rig)
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.012f), 1, 12);
            float h = Mathf.Min(dt, 0.144f) / steps;
            for (int k = 0; k < steps; k++)
            {
                squashV += (-squash * 180f - squashV * 14f) * h;
                squash += squashV * h;
            }
            squash = Mathf.Clamp(squash, -0.45f, 0.45f);
            squashV = Mathf.Clamp(squashV, -12f, 12f);
            float s = 1f + squash;
            vis.localScale = new Vector3(1f / Mathf.Sqrt(Mathf.Max(0.3f, s)), s, 1f / Mathf.Sqrt(Mathf.Max(0.3f, s)));
        }

        /// <summary>After the animation: arms forward while carrying, a wave or a cheer, and a seated pose.</summary>
        void LateUpdate()
        {
            if (!Skinned) return;
            // shared clips carry Rafi's (or the cook's) hip height: move the hips to this model's own
            if (info && hips && playing != null) hips.localPosition += info.ShiftFor(playing);
            var right = transform.right; var fwd = transform.forward;
            if (carryBlend > 0.01f)
            {
                // carrying masks the arms out of the clip: a fixed hold pose instead of the walk's swing. Upper arms
                // forward and down (elbows a little out), forearms level and forward, hands in under the stack.
                var up = transform.up;
                AimBone(upArmL, foreArmL, (fwd * 0.62f - up * 0.75f - right * 0.12f).normalized, carryBlend);
                AimBone(upArmR, foreArmR, (fwd * 0.62f - up * 0.75f + right * 0.12f).normalized, carryBlend);
                AimBone(foreArmL, handL, (fwd + right * 0.3f + up * 0.1f).normalized, carryBlend);
                AimBone(foreArmR, handR, (fwd - right * 0.3f + up * 0.1f).normalized, carryBlend);
            }
            if (waveT >= 0 && upArmR)
            {
                float w = Mathf.Sin(waveT * 14f) * 20f;
                upArmR.rotation = Quaternion.AngleAxis(150f + w, fwd) * upArmR.rotation;
            }
            if (cheerT >= 0)
            {
                float w = Mathf.Sin(cheerT * 16f) * 15f;
                if (upArmL) upArmL.rotation = Quaternion.AngleAxis(-150f - w, fwd) * upArmL.rotation;
                if (upArmR) upArmR.rotation = Quaternion.AngleAxis(150f + w, fwd) * upArmR.rotation;
            }
            if (sitting && anim.GetClip("sit") == null)
            {
                // thighs forward, shins down, hips onto the seat (about 0.5 m)
                if (upLegL) upLegL.rotation = Quaternion.AngleAxis(-85f, right) * upLegL.rotation;
                if (upLegR) upLegR.rotation = Quaternion.AngleAxis(-85f, right) * upLegR.rotation;
                if (kneeL) kneeL.rotation = Quaternion.AngleAxis(85f, right) * kneeL.rotation;
                if (kneeR) kneeR.rotation = Quaternion.AngleAxis(85f, right) * kneeR.rotation;
                vis.localPosition = new Vector3(0, 0.5f / Mathf.Max(0.01f, transform.lossyScale.y) - hipRest, 0);
            }
            else if (sitting && hips)
            {
                // the sit clip: lift or lower the body so the hips rest on a seat about 0.5 m up
                float sy = Mathf.Max(0.01f, transform.lossyScale.y);
                float hipNow = hips.position.y - transform.position.y;          // with the current offset
                float y = vis.localPosition.y + (0.5f - hipNow) / sy;
                vis.localPosition = new Vector3(0, Mathf.Lerp(vis.localPosition.y, y, 0.5f), 0);
            }
            else vis.localPosition = Vector3.zero;
        }

        public void Tick(float speed, float dt)
        {
            if (Skinned) { TickSkinned(speed, dt); return; }
            float moving = Mathf.Clamp01(speed / 2.5f);
            moveBlend = Mathf.MoveTowards(moveBlend, moving, dt * 6f);
            phase += dt * Mathf.Lerp(6f, 13f, Mathf.Clamp01(speed / 6f)) * (moveBlend > 0.01f ? 1 : 0);
            carryBlend = Mathf.MoveTowards(carryBlend, carrying ? 1 : 0, dt * 7f);

            float swing = Mathf.Sin(phase) * 38f * moveBlend;
            if (sitting)
            {
                legL.localRotation = Quaternion.Euler(-85, 0, 0);
                legR.localRotation = Quaternion.Euler(-85, 0, 0);
            }
            else
            {
                legL.localRotation = Quaternion.Euler(swing, 0, 0);
                legR.localRotation = Quaternion.Euler(-swing, 0, 0);
            }

            float armSwing = -swing * 0.8f;
            var armIdle = Quaternion.Euler(armSwing, 0, 0);
            var armCarry = Quaternion.Euler(-78, 0, 0);
            var l = Quaternion.Slerp(armIdle, armCarry, carryBlend);
            var r = Quaternion.Slerp(Quaternion.Euler(-armSwing, 0, 0), armCarry, carryBlend);
            if (waveT >= 0)
            {
                waveT += dt;
                float w = Mathf.Sin(waveT * 14f) * 25f;
                r = Quaternion.Euler(0, 0, -150 + w);
                if (waveT > 1.2f) waveT = -1;
            }
            if (cheerT >= 0)
            {
                cheerT += dt;
                float w = Mathf.Sin(cheerT * 16f) * 15f;
                l = Quaternion.Euler(0, 0, 160 + w);
                r = Quaternion.Euler(0, 0, -160 - w);
                if (cheerT > 1.0f) cheerT = -1;
            }
            armL.localRotation = l;
            armR.localRotation = r;

            // bounce + squash spring
            float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.07f * moveBlend;
            // stiff spring: integrate in small steps so a long frame (hitch, fast-forward) can't blow it up
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.012f), 1, 12);
            float h = Mathf.Min(dt, 0.144f) / steps;
            for (int k = 0; k < steps; k++)
            {
                squashV += (-squash * 180f - squashV * 14f) * h;
                squash += squashV * h;
            }
            squash = Mathf.Clamp(squash, -0.45f, 0.45f);
            squashV = Mathf.Clamp(squashV, -12f, 12f);
            vis.localPosition = new Vector3(0, bob, 0);
            float s = 1f + squash;
            vis.localScale = new Vector3(1f / Mathf.Sqrt(Mathf.Max(0.3f, s)), s, 1f / Mathf.Sqrt(Mathf.Max(0.3f, s)));
            vis.localRotation = Quaternion.Euler(moveBlend * 6f, 0, Mathf.Sin(phase) * 3f * moveBlend);
        }

        public void Squash(float amount = 0.18f) { squashV -= amount * 14f; }
        public void Hop(float amount = 0.25f) { squashV += amount * 14f; }
        public void Wave() { waveT = 0; }
        public void Cheer() { cheerT = 0; }
    }

    public static class Models
    {
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        static Mesh quadMesh;

        public static Mesh Cached(string key, System.Func<MeshBuilder, MeshBuilder> make)
        {
            if (meshCache.TryGetValue(key, out var m) && m) return m;
            m = make(new MeshBuilder()).Build(key);
            meshCache[key] = m;
            return m;
        }

        public static GameObject Obj(string name, Mesh mesh, Transform parent, bool outline = true)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            // Blender props and environment: standard URP Lit materials with real shadows; everything else toon
            var lit = ArtLib.MaterialsFor(mesh);
            if (lit != null) mr.sharedMaterials = lit;
            else mr.sharedMaterial = outline ? Mats.Toon : Mats.ToonFlat;
            mr.shadowCastingMode = lit != null ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = lit != null;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = lit != null ? UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes : UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }

        public static GameObject Obj(string name, MeshBuilder b, Transform parent, bool outline = true) => Obj(name, b.Build(name), parent, outline);

        // ---------------- quads with UVs for alpha sprites ----------------

        public static Mesh Quad()
        {
            if (quadMesh) return quadMesh;
            quadMesh = new Mesh { name = "quadXZ" };
            quadMesh.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            quadMesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quadMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            quadMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quadMesh.RecalculateBounds();
            return quadMesh;
        }

        public static MeshRenderer AlphaQuad(string name, Transform parent, Texture tex, Vector3 localPos, Vector2 size, Color color, bool sharedMat = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, 1, size.y);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var mr = go.AddComponent<MeshRenderer>();
            if (sharedMat && color == Color.white) mr.sharedMaterial = Mats.Alpha(tex);
            else
            {
                var m = Mats.AlphaInstance(tex);
                m.SetColor("_BaseColor", color);
                mr.sharedMaterial = m;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        static Material blobMat;
        public static GameObject Blob(Transform parent, float diameter, float alpha = 0.35f)
        {
            if (!blobMat)
            {
                blobMat = Mats.AlphaInstance(SpriteGen.BlobTexture());
                blobMat.SetColor("_BaseColor", Pal.WithA(Pal.BlobShadow, alpha));
                blobMat.renderQueue = 2990;
            }
            var go = new GameObject("blob");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0.03f, 0);
            go.transform.localScale = new Vector3(diameter, 1, diameter);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = blobMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ---------------- food ----------------

        public static Mesh ItemMesh(ItemId id) => Cached("item_" + id, b =>
        {
            var plate = Pal.Hex("#F4F1EA");
            switch (id)
            {
                case ItemId.Chai:
                    b.Cylinder(new Vector3(0, 0, 0), 0.19f, 0.2f, 0.03f, 10, plate);
                    b.Cylinder(new Vector3(0, 0.03f, 0), 0.1f, 0.14f, 0.18f, 9, Pal.Hex("#FFF8EA"));
                    b.Cylinder(new Vector3(0, 0.2f, 0), 0.128f, 0.128f, 0.012f, 9, Pal.ChaiTea);
                    b.Box(new Vector3(0.155f, 0.13f, 0), new Vector3(0.05f, 0.09f, 0.035f), Pal.Hex("#FFF8EA"));
                    b.Box(new Vector3(0, 0.12f, -0.125f), new Vector3(0.07f, 0.04f, 0.01f), Pal.Teal, Quaternion.Euler(-12, 0, 0));
                    break;
                case ItemId.Paratha:
                    b.Cylinder(Vector3.zero, 0.22f, 0.23f, 0.03f, 10, plate);
                    b.Cylinder(new Vector3(0, 0.03f, 0), 0.2f, 0.19f, 0.06f, 9, Pal.Paratha);
                    b.Sphere(new Vector3(0.07f, 0.09f, 0.05f), new Vector3(0.07f, 0.015f, 0.06f), Pal.Hex("#B9782F"), 0);
                    b.Sphere(new Vector3(-0.08f, 0.09f, -0.03f), new Vector3(0.06f, 0.015f, 0.05f), Pal.Hex("#B9782F"), 0);
                    b.Sphere(new Vector3(0.0f, 0.09f, -0.1f), new Vector3(0.05f, 0.015f, 0.04f), Pal.Hex("#B9782F"), 0);
                    b.Box(new Vector3(-0.02f, 0.1f, 0.06f), new Vector3(0.06f, 0.03f, 0.05f), Pal.Hex("#FFE9A0"));
                    break;
                case ItemId.Samosa:
                    b.Cylinder(Vector3.zero, 0.2f, 0.21f, 0.03f, 10, plate);
                    b.Tetra(new Vector3(0, 0.12f, 0.0f), new Vector3(0.28f, 0.18f, 0.26f), Pal.Samosa, Quaternion.Euler(0, 15, 0));
                    b.Sphere(new Vector3(0.12f, 0.05f, -0.08f), new Vector3(0.07f, 0.03f, 0.07f), Pal.LeafGreen, 0);
                    break;
                case ItemId.Lassi:
                    b.Cylinder(Vector3.zero, 0.09f, 0.12f, 0.3f, 9, Pal.Lassi);
                    b.Cylinder(new Vector3(0, 0.3f, 0), 0.12f, 0.1f, 0.03f, 9, Pal.Hex("#FFFDF5"));
                    b.Box(new Vector3(0.04f, 0.38f, 0), new Vector3(0.025f, 0.16f, 0.025f), Pal.TruckPink, Quaternion.Euler(0, 0, -12));
                    break;
                case ItemId.Yogurt:
                    b.Cylinder(Vector3.zero, 0.1f, 0.14f, 0.12f, 9, Pal.Clay);
                    b.Cylinder(new Vector3(0, 0.12f, 0), 0.14f, 0.11f, 0.06f, 9, Pal.Clay);
                    b.Cylinder(new Vector3(0, 0.17f, 0), 0.105f, 0.105f, 0.01f, 9, Pal.Lassi);
                    break;
                case ItemId.DirtyDish:
                    b.Cylinder(Vector3.zero, 0.2f, 0.22f, 0.03f, 10, Pal.Hex("#E4E0D8"));
                    b.Cylinder(new Vector3(0, 0.03f, 0), 0.18f, 0.2f, 0.03f, 10, Pal.Hex("#DCD7CE"));
                    b.Sphere(new Vector3(0.05f, 0.07f, 0.03f), new Vector3(0.06f, 0.02f, 0.05f), Pal.Hex("#A8743E"), 0);
                    break;
                default:
                    b.Cylinder(Vector3.zero, 0.2f, 0.21f, 0.03f, 10, plate);
                    b.Sphere(new Vector3(0, 0.09f, 0), new Vector3(0.3f, 0.12f, 0.3f), Pal.Samosa, 1);
                    break;
            }
            return b;
        });

        public static GameObject Item(ItemId id, Transform parent = null)
        {
            var go = Obj("item_" + id, ItemMesh(id), parent);
            go.AddComponent<ItemObj>().id = id;
            return go;
        }

        public static Mesh CoinMesh() => Cached("coin", b =>
        {
            b.Cylinder(Vector3.zero, 0.12f, 0.12f, 0.035f, 10, Pal.CashGold);
            b.Cylinder(new Vector3(0, 0.035f, 0), 0.07f, 0.07f, 0.006f, 8, Pal.Mul(Pal.Marigold, 0.95f));
            return b;
        });

        public static Mesh LeafMesh() => Cached("leaf3d", b =>
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float t = i / 9f * Mathf.PI;
                pts[i] = new Vector2(Mathf.Cos(t) * 0.16f, Mathf.Sin(t) * 0.07f);
            }
            var full = new List<Vector2>();
            for (int i = 0; i < 10; i++) full.Add(pts[i]);
            for (int i = 8; i >= 1; i--) full.Add(new Vector2(pts[i].x, -pts[i].y));
            b.Prism(Vector3.zero, full.ToArray(), 0.03f, Pal.Parrot);
            return b;
        });

        // ---------------- characters ----------------

        // ------------------------------------------------------------------ crowd (the user's Tripo characters)

        /// <summary>Height the crowd models are scaled to (they come in at about 0.98 m).</summary>
        public const float CrowdScale = 1.72f;
        static readonly Dictionary<string, GameObject> crowd = new Dictionary<string, GameObject>();

        static GameObject CrowdPrefab(string model)
        {
            if (crowd.TryGetValue(model, out var p)) return p;
            p = Resources.Load<GameObject>("Crowd/" + model);
            crowd[model] = p;
            return p;
        }

        /// <summary>Which crowd model plays a look: Rafi, the helpers, the cook, or one of four customers.</summary>
        public static string CrowdModel(string look)
        {
            if (string.IsNullOrEmpty(look)) return null;
            switch (look)
            {
                case "Rafi": return "Rafi";
                case "Cook": return "Cook";        // the girl in the red apron and cream dupatta
                case "Waiter": case "Cashier": case "Cleaner": case "Rider": case "Manager": return "Helper";
            }
            int h = 0;
            foreach (var ch in look) h = h * 31 + ch;
            return "Customer" + (1 + Mathf.Abs(h) % 4);
        }

        /// <summary>A rigged, animated crowd character (idle / walk / run / sit) on the same CharRig interface.</summary>
        static CharRig CrowdCharacter(string model, Transform parent, string name, float scale)
        {
            var prefab = CrowdPrefab(model);
            if (!prefab) return null;
            var root = new GameObject(name);
            if (parent) root.transform.SetParent(parent, false);
            var rig = root.AddComponent<CharRig>();
            var vis = new GameObject("vis").transform;
            vis.SetParent(root.transform, false);
            rig.vis = vis;
            var inst = Object.Instantiate(prefab, vis, false);
            inst.name = "model";
            inst.transform.localScale = Vector3.one * CrowdScale;
            // limbs other code may pose directly (the art preview): harmless stand-ins here
            rig.legL = new GameObject("legL").transform; rig.legL.SetParent(vis, false);
            rig.legR = new GameObject("legR").transform; rig.legR.SetParent(vis, false);
            rig.armL = new GameObject("armL").transform; rig.armL.SetParent(vis, false);
            rig.armR = new GameObject("armR").transform; rig.armR.SetParent(vis, false);
            var ca = new GameObject("carry").transform;
            ca.SetParent(vis, false);
            ca.localPosition = new Vector3(0, 0.98f, 0.42f);
            rig.carryAnchor = ca;
            var ht = new GameObject("headTop").transform;
            ht.SetParent(root.transform, false);
            ht.localPosition = new Vector3(0, 0.98f * CrowdScale + 0.08f, 0);
            rig.headTop = ht;
            rig.Height = 1.6f * scale;
            rig.SetupSkinned(inst.GetComponent<Animation>(), inst.transform);
            Blob(root.transform, 0.9f);
            root.transform.localScale = Vector3.one * scale;
            return rig;
        }

        public static CharRig Character(CharSpec s, Transform parent = null, string name = "char")
        {
            var cm = CrowdModel(s.look);
            if (cm != null)
            {
                var c = CrowdCharacter(cm, parent, name, s.scale > 0 ? s.scale : 1f);
                if (c != null) return c;
            }
            if (!string.IsNullOrEmpty(s.look) && ArtLib.Has($"Chr_{s.look}_Torso")) return ArtCharacter(s, parent, name);
            var root = new GameObject(name);
            if (parent) root.transform.SetParent(parent, false);
            var rig = root.AddComponent<CharRig>();
            var vis = new GameObject("vis").transform;
            vis.SetParent(root.transform, false);
            rig.vis = vis;
            string k = s.Key();
            float bw = s.bodyWidth;

            var torso = Obj("torso", Cached("torso_" + k, b => BuildTorso(b, s)), vis);
            rig.torso = torso.transform;

            var legMesh = Cached($"leg_{ColorUtility.ToHtmlStringRGB(s.bottom)}{ColorUtility.ToHtmlStringRGB(s.shoes)}", b =>
            {
                b.Box(new Vector3(0, -0.19f, 0), new Vector3(0.17f, 0.4f, 0.19f), s.bottom);
                b.Box(new Vector3(0, -0.41f, 0.04f), new Vector3(0.19f, 0.08f, 0.27f), s.shoes);
                return b;
            });
            rig.legL = Obj("legL", legMesh, vis).transform;
            rig.legL.localPosition = new Vector3(-0.11f, 0.45f, 0);
            rig.legR = Obj("legR", legMesh, vis).transform;
            rig.legR.localPosition = new Vector3(0.11f, 0.45f, 0);

            var armMesh = Cached($"arm_{ColorUtility.ToHtmlStringRGB(s.top)}{ColorUtility.ToHtmlStringRGB(s.skin)}", b =>
            {
                b.Cylinder(Vector3.zero, 0.08f, 0.065f, 0.4f, 7, s.top, Quaternion.Euler(180, 0, 0));
                b.Sphere(new Vector3(0, -0.46f, 0), 0.16f, s.skin, 0);
                return b;
            });
            rig.armL = Obj("armL", armMesh, vis).transform;
            rig.armL.localPosition = new Vector3(-0.3f * bw, 0.98f, 0);
            rig.armR = Obj("armR", armMesh, vis).transform;
            rig.armR.localPosition = new Vector3(0.3f * bw, 0.98f, 0);

            var ca = new GameObject("carry").transform;
            ca.SetParent(vis, false);
            ca.localPosition = new Vector3(0, 0.93f, 0.46f);
            rig.carryAnchor = ca;

            var ht = new GameObject("headTop").transform;
            ht.SetParent(root.transform, false);
            ht.localPosition = new Vector3(0, 1.75f, 0);
            rig.headTop = ht;
            rig.Height = 1.6f * s.scale;

            Blob(root.transform, 0.9f);
            root.transform.localScale = Vector3.one * s.scale;
            return rig;
        }

        /// <summary>
        /// A Blender-modelled character: the same five parts and pivots as the procedural one, so CharRig's
        /// walk, carry, sit, wave and cheer work unchanged.
        /// </summary>
        static CharRig ArtCharacter(CharSpec s, Transform parent, string name)
        {
            string k = "Chr_" + s.look + "_";
            var meta = ArtLib.Meta(k + "Torso");
            float bw = meta != null && meta.Length > 0 ? meta[0] : 1f;
            float sc = meta != null && meta.Length > 1 ? meta[1] : 1f;
            float top = meta != null && meta.Length > 2 ? meta[2] : 1.75f;

            var root = new GameObject(name);
            if (parent) root.transform.SetParent(parent, false);
            var rig = root.AddComponent<CharRig>();
            var vis = new GameObject("vis").transform;
            vis.SetParent(root.transform, false);
            rig.vis = vis;

            var recolor = s.look == "Rafi" ? RafiRecolor(s) : null;
            Mesh Part(string p) => recolor == null ? ArtLib.Get(k + p) : Recolored(k + p, recolor);
            rig.torso = Obj("torso", Part("Torso"), vis).transform;
            var leg = ArtLib.Get(k + "Leg");
            rig.legL = Obj("legL", leg, vis).transform;
            rig.legL.localPosition = new Vector3(-0.11f, 0.45f, 0);
            rig.legR = Obj("legR", leg, vis).transform;
            rig.legR.localPosition = new Vector3(0.11f, 0.45f, 0);
            rig.armL = Obj("armL", Part("ArmL"), vis).transform;
            rig.armL.localPosition = new Vector3(-0.3f * bw, 0.98f, 0);
            rig.armR = Obj("armR", Part("ArmR"), vis).transform;
            rig.armR.localPosition = new Vector3(0.3f * bw, 0.98f, 0);

            var ca = new GameObject("carry").transform;
            ca.SetParent(vis, false);
            ca.localPosition = new Vector3(0, 0.93f, 0.46f);
            rig.carryAnchor = ca;
            var ht = new GameObject("headTop").transform;
            ht.SetParent(root.transform, false);
            ht.localPosition = new Vector3(0, top, 0);
            rig.headTop = ht;
            rig.Height = (top - 0.15f) * sc;

            Blob(root.transform, 0.9f * Mathf.Max(1f, bw));
            root.transform.localScale = Vector3.one * sc;
            return rig;
        }

        // Rafi's default colours in the Blender model, swapped for the cap / kurta / apron cosmetics
        static Dictionary<Color32, Color> RafiRecolor(CharSpec s)
        {
            var def = RafiSpec();
            var map = new Dictionary<Color32, Color>();
            if (s.hatColor != def.hatColor)
            {
                map[Hex32("#FFC21A")] = s.hatColor;
                map[Hex32("#F2A81A")] = Pal.Mul(s.hatColor, 0.9f);
            }
            if (s.top != def.top) map[Hex32("#F4F1EA")] = s.top;
            if (s.apronColor != def.apronColor)
            {
                map[Hex32("#E8382F")] = s.apronColor;
                map[Hex32("#B92C24")] = Pal.Mul(s.apronColor, 0.8f);
            }
            return map.Count > 0 ? map : null;
        }

        static Color32 Hex32(string h) => Pal.Hex(h);

        static Mesh Recolored(string meshName, Dictionary<Color32, Color> map)
        {
            var key = meshName + "#";
            foreach (var kv in map) key += kv.Key.ToString() + ColorUtility.ToHtmlStringRGB(kv.Value);
            if (meshCache.TryGetValue(key, out var cached) && cached) return cached;
            var src = ArtLib.Get(meshName);
            var m = Object.Instantiate(src);
            m.name = key;
            var cols = m.colors;
            for (int i = 0; i < cols.Length; i++)
            {
                Color32 g = cols[i].gamma;
                foreach (var kv in map)
                {
                    if (Mathf.Abs(g.r - kv.Key.r) <= 2 && Mathf.Abs(g.g - kv.Key.g) <= 2 && Mathf.Abs(g.b - kv.Key.b) <= 2)
                    {
                        cols[i] = kv.Value.linear;
                        break;
                    }
                }
            }
            m.colors = cols;
            meshCache[key] = m;
            return m;
        }

        static MeshBuilder BuildTorso(MeshBuilder b, CharSpec s)
        {
            float bw = s.bodyWidth, hs = s.headScale;
            b.Cylinder(new Vector3(0, 0.38f, 0), 0.28f * bw, 0.22f * bw, 0.62f, 8, s.top);
            b.Sphere(new Vector3(0, 0.99f, 0), new Vector3(0.5f * bw, 0.2f, 0.4f), s.top, 1);
            float hy = 1.29f;
            var hc = new Vector3(0, hy, 0);
            b.Sphere(hc, new Vector3(0.52f, 0.5f, 0.5f) * hs, s.skin, 1);
            // face
            float fz = 0.235f * hs;
            b.Sphere(new Vector3(-0.095f * hs, hy + 0.03f * hs, fz), new Vector3(0.065f, 0.09f, 0.04f) * hs, Pal.Ink, 0);
            b.Sphere(new Vector3(0.095f * hs, hy + 0.03f * hs, fz), new Vector3(0.065f, 0.09f, 0.04f) * hs, Pal.Ink, 0);
            b.Sphere(new Vector3(-0.085f * hs, hy + 0.055f * hs, fz + 0.018f), Vector3.one * 0.025f * hs, Color.white, 0);
            b.Sphere(new Vector3(0.105f * hs, hy + 0.055f * hs, fz + 0.018f), Vector3.one * 0.025f * hs, Color.white, 0);
            b.Box(new Vector3(-0.1f * hs, hy + 0.115f * hs, fz - 0.01f), new Vector3(0.1f, 0.025f, 0.03f) * hs, s.hair, Quaternion.Euler(0, 0, -8));
            b.Box(new Vector3(0.1f * hs, hy + 0.115f * hs, fz - 0.01f), new Vector3(0.1f, 0.025f, 0.03f) * hs, s.hair, Quaternion.Euler(0, 0, 8));
            b.Sphere(new Vector3(0, hy - 0.09f * hs, fz + 0.005f), new Vector3(0.11f, 0.04f, 0.03f) * hs, Pal.Hex("#8A2E2A"), 0);
            if (s.mustache) b.Box(new Vector3(0, hy - 0.045f * hs, fz + 0.012f), new Vector3(0.16f, 0.035f, 0.03f) * hs, s.hair);
            if (s.beard) b.Sphere(new Vector3(0, hy - 0.13f * hs, 0.1f * hs), new Vector3(0.42f, 0.26f, 0.34f) * hs, s.hair, 1);
            if (s.glasses)
            {
                b.Box(new Vector3(0, hy + 0.035f * hs, fz + 0.03f), new Vector3(0.32f, 0.075f, 0.03f) * hs, Pal.Ink);
            }
            // hair
            if (s.hat != "turban" && s.hat != "beanie")
                b.Dome(new Vector3(0, hy + 0.04f * hs, -0.015f), new Vector3(0.55f, 0.46f, 0.55f) * hs, 8, 3, s.hair);
            if (s.longHair)
                b.Box(new Vector3(0, hy - 0.12f * hs, -0.17f * hs), new Vector3(0.44f, 0.42f, 0.16f) * hs, s.hair);
            // hats
            switch (s.hat)
            {
                case "cap":
                    b.Dome(new Vector3(0, hy + 0.1f * hs, 0), new Vector3(0.56f, 0.44f, 0.56f) * hs, 9, 3, s.hatColor);
                    b.Box(new Vector3(0, hy + 0.12f * hs, 0.25f * hs), new Vector3(0.34f, 0.035f, 0.24f) * hs, Pal.Mul(s.hatColor, 0.85f));
                    break;
                case "topi":
                    b.Cylinder(new Vector3(0, hy + 0.14f * hs, 0), 0.25f * hs, 0.24f * hs, 0.13f * hs, 9, s.hatColor);
                    break;
                case "turban":
                    b.Sphere(new Vector3(0, hy + 0.15f * hs, -0.01f), new Vector3(0.62f, 0.36f, 0.6f) * hs, s.hatColor, 1);
                    b.Sphere(new Vector3(0, hy + 0.2f * hs, 0.22f * hs), new Vector3(0.14f, 0.16f, 0.1f) * hs, Pal.Mul(s.hatColor, 0.85f), 0);
                    break;
                case "beanie":
                    b.Dome(new Vector3(0, hy + 0.02f * hs, 0), new Vector3(0.58f, 0.56f, 0.58f) * hs, 8, 3, s.hatColor);
                    b.Sphere(new Vector3(0, hy + 0.3f * hs, 0), Vector3.one * 0.12f * hs, Color.white, 0);
                    break;
            }
            if (s.apron)
            {
                b.Box(new Vector3(0, 0.7f, 0.255f * bw), new Vector3(0.4f * bw, 0.58f, 0.03f), s.apronColor, Quaternion.Euler(-5.5f, 0, 0));
                b.Box(new Vector3(0, 0.9f, 0.235f * bw), new Vector3(0.12f, 0.06f, 0.035f), Pal.Mul(s.apronColor, 0.8f));
            }
            if (s.backpack)
            {
                b.BevelBox(new Vector3(0, 0.76f, -0.29f * bw), new Vector3(0.38f, 0.44f, 0.18f), 0.05f, s.backpackColor);
            }
            if (s.scarf)
            {
                b.Torus(new Vector3(0, 1.04f, 0), 0.17f, 0.06f, 8, 5, s.scarfColor);
                b.Box(new Vector3(0.08f, 0.82f, 0.22f), new Vector3(0.1f, 0.34f, 0.04f), s.scarfColor, Quaternion.Euler(-6, 0, 0));
            }
            return b;
        }

        public static CharSpec RafiSpec()
        {
            return new CharSpec
            {
                skin = Pal.Skins[1], top = Pal.White, bottom = Pal.Hex("#ECE6DA"), hair = Pal.HairColors[0],
                hat = "cap", hatColor = Pal.Marigold, apron = true, apronColor = Pal.Chili, shoes = Pal.Hex("#6B3F2A"),
                headScale = 1.08f, look = "Rafi"
            };
        }

        public static Color StaffCapColor(string role)
        {
            switch (role)
            {
                case "cashier": return Pal.Parrot;
                case "waiter": return Pal.Chili;
                case "cleaner": return Pal.Royal;
                case "cook": return Color.white;
                case "rider": return Pal.Marigold;
                case "manager": return Pal.Ink;
                default: return Pal.Teal;
            }
        }

        public static CharSpec StaffSpec(string role, int seed)
        {
            var r = new System.Random(seed * 7919 + role.GetHashCode());
            var s = RafiSpec();
            s.skin = Pal.Skins[r.Next(Pal.Skins.Length)];
            s.hatColor = StaffCapColor(role);
            s.hat = role == "cook" ? "topi" : "cap";
            s.apron = role == "cook" || role == "waiter";
            s.apronColor = role == "cook" ? Pal.Hex("#F3EFE6") : Pal.Ink;
            s.top = role == "cashier" ? Pal.Hex("#DDEBD9") : role == "cleaner" ? Pal.Hex("#D5DEF0") : Pal.Hex("#F1ECE2");
            s.mustache = r.NextDouble() < 0.5;
            s.headScale = 1.02f;
            s.look = role.Length > 0 ? char.ToUpperInvariant(role[0]) + role.Substring(1) : null;
            return s;
        }

        // Blender looks per customer type (the art pack has three of most, GDD asset list)
        static readonly Dictionary<string, string[]> customerLooks = new Dictionary<string, string[]>
        {
            { "walkin", new[] { "WalkinA", "WalkinB", "WalkinC", "FatherA", "MotherA" } },
            { "student", new[] { "StudentA", "StudentB", "StudentC" } },
            { "rickshaw", new[] { "DriverA", "DriverB", "DriverC" } },
            { "diner", new[] { "FatherA", "MotherA", "Grandpa", "ChildA", "ChildB", "ChildC", "WalkinB", "WalkinC" } },
            { "family", new[] { "FatherA", "MotherA", "Grandpa", "ChildA", "ChildB", "ChildC" } },
            { "truck", new[] { "CrewA", "CrewB", "CrewC" } },
            { "critic", new[] { "Critic" } },
            { "tourist", new[] { "TouristA", "TouristB", "TouristC" } },
        };

        public static CharSpec CustomerSpec(string type, System.Random r)
        {
            var s = new CharSpec
            {
                skin = Pal.Skins[r.Next(Pal.Skins.Length)],
                top = Pal.Clothes[r.Next(Pal.Clothes.Length)],
                hair = Pal.HairColors[r.Next(Pal.HairColors.Length)],
                headScale = 0.88f,
                scale = 1.04f,
                bodyWidth = 0.92f,
            };
            s.bottom = r.NextDouble() < 0.5 ? s.top : Pal.Clothes[r.Next(Pal.Clothes.Length)];
            if (r.NextDouble() < 0.3) { s.bottom = Pal.Hex("#4F6684"); } // jeans
            double h = r.NextDouble();
            if (h < 0.18) { s.hat = "topi"; s.hatColor = Pal.Hex("#F2EEE6"); }
            else if (h < 0.28) { s.hat = "turban"; s.hatColor = r.NextDouble() < 0.5 ? Pal.Hex("#E9E2D2") : Pal.Hex("#B4876A"); }
            s.mustache = r.NextDouble() < 0.4;
            s.beard = !s.mustache && r.NextDouble() < 0.2;
            if (r.NextDouble() < 0.22) { s.longHair = true; s.mustache = false; s.beard = false; s.hat = "none"; s.scarf = r.NextDouble() < 0.7; s.scarfColor = Pal.Clothes[r.Next(Pal.Clothes.Length)]; }

            switch (type)
            {
                case "student":
                    s.scale = 0.95f; s.headScale = 0.95f;
                    s.backpack = true;
                    s.backpackColor = new[] { Pal.Royal, Pal.Teal, Pal.Chili, Pal.Parrot }[r.Next(4)];
                    s.beard = false; s.mustache = false;
                    s.top = new[] { Pal.Hex("#E7EEF5"), Pal.Hex("#F3E3C3"), Pal.Hex("#CFE2CF") }[r.Next(3)];
                    s.bottom = Pal.Hex("#4F6684");
                    break;
                case "rickshaw":
                    s.mustache = true; s.beard = false; s.longHair = false; s.scarf = false;
                    s.hat = r.NextDouble() < 0.5 ? "topi" : "none";
                    s.hatColor = Pal.Hex("#F2EEE6");
                    s.top = Pal.Hex("#9FA8B0");
                    break;
                case "critic":
                    s.glasses = true; s.scarf = true; s.scarfColor = Pal.Purple; s.hat = "none"; s.beard = false;
                    s.bodyWidth = 0.85f;
                    break;
            }
            if (!customerLooks.TryGetValue(type, out var looks)) looks = customerLooks["walkin"];
            s.look = looks[r.Next(looks.Length)];
            return s;
        }

        // ---------------- stations ----------------

        /// <summary>Wooden prep table, 1.7 x 1.0, top at 0.85. Front (toward player) is -z.</summary>
        static void Table(MeshBuilder b, float w = 1.7f, float d = 1.0f, float h = 0.85f, Color? top = null)
        {
            b.BevelBox(new Vector3(0, h - 0.05f, 0), new Vector3(w, 0.1f, d), 0.06f, top ?? Pal.Hex("#8A6243"));
            b.Box(new Vector3(0, (h - 0.1f) / 2f, 0.05f), new Vector3(w - 0.12f, h - 0.1f, d - 0.2f), Pal.Clay);
            b.Box(new Vector3(0, (h - 0.1f) * 0.55f, -d / 2 + 0.09f), new Vector3(w - 0.3f, 0.12f, 0.02f), Pal.TruckPink);
            for (int i = -1; i <= 1; i++)
                b.Sphere(new Vector3(i * 0.42f, (h - 0.1f) * 0.55f, -d / 2 + 0.085f), new Vector3(0.1f, 0.1f, 0.03f), i == 0 ? Pal.Marigold : Pal.Teal, 0);
        }

        public const float StationScale = 1.2f;

        public static Mesh StationMesh(string type) => Cached("station_" + type, b =>
        {
            b.Root = Matrix4x4.Scale(Vector3.one * StationScale);
            float h = 0.85f;
            switch (type)
            {
                case "kettle":
                    Table(b);
                    b.Cylinder(new Vector3(-0.3f, h, 0.2f), 0.26f, 0.24f, 0.12f, 8, Pal.Charcoal);
                    b.Cylinder(new Vector3(-0.3f, h + 0.12f, 0.2f), 0.24f, 0.28f, 0.34f, 10, Pal.Steel);
                    b.Cylinder(new Vector3(-0.3f, h + 0.46f, 0.2f), 0.28f, 0.16f, 0.1f, 10, Pal.Steel);
                    b.Cylinder(new Vector3(-0.3f, h + 0.56f, 0.2f), 0.06f, 0.04f, 0.06f, 6, Pal.DarkSteel);
                    b.Cylinder(new Vector3(-0.08f, h + 0.3f, 0.2f), 0.05f, 0.03f, 0.3f, 6, Pal.Steel, Quaternion.Euler(0, 0, -55));
                    b.Torus(new Vector3(-0.3f, h + 0.56f, 0.2f), 0.2f, 0.025f, 10, 4, Pal.DarkSteel, Quaternion.Euler(90, 0, 0));
                    // milk can and cups crate for flavour
                    b.Cylinder(new Vector3(0.55f, h, 0.3f), 0.13f, 0.13f, 0.32f, 8, Pal.Steel);
                    b.Cylinder(new Vector3(0.55f, h + 0.32f, 0.3f), 0.08f, 0.08f, 0.06f, 8, Pal.DarkSteel);
                    break;
                case "tawa":
                    Table(b);
                    b.Cylinder(new Vector3(-0.25f, h, 0.18f), 0.3f, 0.28f, 0.12f, 8, Pal.Charcoal);
                    b.Cylinder(new Vector3(-0.25f, h + 0.12f, 0.18f), 0.42f, 0.36f, 0.06f, 12, Pal.Hex("#2E2A30"));
                    b.Cylinder(new Vector3(-0.25f, h + 0.18f, 0.18f), 0.25f, 0.24f, 0.03f, 9, Pal.Paratha);
                    b.Box(new Vector3(0.5f, h + 0.03f, 0.25f), new Vector3(0.35f, 0.06f, 0.3f), Pal.Hex("#C7A27A"));
                    b.Sphere(new Vector3(0.5f, h + 0.1f, 0.25f), new Vector3(0.2f, 0.1f, 0.18f), Pal.Hex("#F3E6C9"), 0);
                    break;
                case "fryer":
                    Table(b);
                    b.Cylinder(new Vector3(-0.2f, h, 0.18f), 0.32f, 0.3f, 0.14f, 8, Pal.Charcoal);
                    b.Cylinder(new Vector3(-0.2f, h + 0.14f, 0.18f), 0.2f, 0.46f, 0.22f, 12, Pal.Hex("#3A3438"));
                    b.Cylinder(new Vector3(-0.2f, h + 0.3f, 0.18f), 0.4f, 0.4f, 0.02f, 12, Pal.Hex("#E9A23B"));
                    b.Tetra(new Vector3(-0.32f, h + 0.36f, 0.12f), new Vector3(0.2f, 0.12f, 0.18f), Pal.Samosa);
                    b.Tetra(new Vector3(-0.08f, h + 0.36f, 0.26f), new Vector3(0.2f, 0.12f, 0.18f), Pal.Samosa, Quaternion.Euler(0, 60, 0));
                    b.Torus(new Vector3(-0.2f, h + 0.33f, 0.18f), 0.46f, 0.03f, 12, 4, Pal.Hex("#3A3438"));
                    break;
                default:
                    Table(b);
                    break;
            }
            return b;
        });

        public static Mesh CounterMesh(float length) => Cached("counter_" + length, b =>
        {
            float h = 0.85f, d = 0.9f;
            b.BevelBox(new Vector3(0, h - 0.06f, 0), new Vector3(length, 0.12f, d + 0.1f), 0.08f, Pal.Hex("#9B6E4A"));
            b.Box(new Vector3(0, (h - 0.12f) / 2, 0), new Vector3(length - 0.1f, h - 0.12f, d - 0.05f), Pal.DarkWood);
            // truck-art front panel (customer side, -z)
            float fz = -d / 2 + 0.01f;
            b.Box(new Vector3(0, 0.46f, fz - 0.02f), new Vector3(length - 0.3f, 0.62f, 0.04f), Pal.TruckPink);
            b.Box(new Vector3(0, 0.78f, fz - 0.04f), new Vector3(length - 0.3f, 0.07f, 0.04f), Pal.Teal);
            b.Box(new Vector3(0, 0.14f, fz - 0.04f), new Vector3(length - 0.3f, 0.07f, 0.04f), Pal.Teal);
            int n = Mathf.Max(1, Mathf.RoundToInt(length / 0.75f));
            for (int i = 0; i < n; i++)
            {
                float x = -length / 2 + 0.15f + (i + 0.5f) * (length - 0.3f) / n;
                b.Prism(new Vector3(x, 0.46f, fz - 0.045f), MeshBuilder.StarOutline(5, 0.16f, 0.07f), 0.03f, i % 2 == 0 ? Pal.Marigold : Pal.Parrot, Quaternion.Euler(-90, 0, 0));
                b.Sphere(new Vector3(x, 0.46f, fz - 0.075f), new Vector3(0.07f, 0.07f, 0.03f), Pal.Chili, 0);
            }
            return b;
        });

        public static Mesh TillMesh() => Cached("till", b =>
        {
            b.BevelBox(new Vector3(0, 0.12f, 0), new Vector3(0.5f, 0.24f, 0.4f), 0.05f, Pal.Hex("#4D7A8C"));
            b.Box(new Vector3(0, 0.3f, 0.08f), new Vector3(0.36f, 0.14f, 0.06f), Pal.Hex("#E8F1E4"), Quaternion.Euler(-20, 0, 0));
            return b;
        });

        // ---------------- vehicles ----------------

        public static Mesh RickshawMesh(Color body) => Cached("rickshaw_" + ColorUtility.ToHtmlStringRGB(body), b =>
        {
            // faces +z
            b.BevelBox(new Vector3(0, 0.62f, -0.15f), new Vector3(1.3f, 0.6f, 1.5f), 0.2f, body);
            b.BevelBox(new Vector3(0, 0.55f, 0.85f), new Vector3(0.7f, 0.5f, 0.6f), 0.15f, body);
            b.Cylinder(new Vector3(0, 0.3f, 1.1f), 0.12f, 0.12f, 0.6f, 6, Pal.DarkSteel, Quaternion.Euler(-15, 0, 0));
            // canopy
            var roof = Pal.Mul(body, 0.55f);
            b.BevelBox(new Vector3(0, 1.66f, 0.0f), new Vector3(1.24f, 0.1f, 1.55f), 0.22f, roof);
            b.Box(new Vector3(-0.3f, 1.72f, 0.0f), new Vector3(0.12f, 0.04f, 1.3f), Pal.Marigold);
            b.Box(new Vector3(0.3f, 1.72f, 0.0f), new Vector3(0.12f, 0.04f, 1.3f), Pal.Marigold);
            b.Prism(new Vector3(0, 1.72f, 0.0f), MeshBuilder.StarOutline(6, 0.2f, 0.09f), 0.03f, Pal.TruckPink);
            b.Box(new Vector3(0, 1.57f, 0.78f), new Vector3(1.2f, 0.1f, 0.05f), Pal.TruckPink);
            b.Box(new Vector3(-0.58f, 1.25f, 0.74f), new Vector3(0.06f, 0.85f, 0.06f), Pal.Ink);
            b.Box(new Vector3(0.58f, 1.25f, 0.74f), new Vector3(0.06f, 0.85f, 0.06f), Pal.Ink);
            b.Box(new Vector3(-0.58f, 1.25f, -0.76f), new Vector3(0.06f, 0.85f, 0.06f), Pal.Ink);
            b.Box(new Vector3(0.58f, 1.25f, -0.76f), new Vector3(0.06f, 0.85f, 0.06f), Pal.Ink);
            b.Box(new Vector3(0, 1.25f, -0.78f), new Vector3(1.2f, 0.75f, 0.06f), roof);
            // seat
            b.Box(new Vector3(0, 0.98f, -0.35f), new Vector3(1.1f, 0.12f, 0.6f), Pal.Chili);
            // truck-art: flowers on the sides and fringe
            for (int side = -1; side <= 1; side += 2)
            {
                b.Prism(new Vector3(0.66f * side, 0.62f, -0.15f), MeshBuilder.StarOutline(6, 0.18f, 0.09f), 0.03f, Pal.Marigold, Quaternion.Euler(0, 0, side * 90));
                b.Sphere(new Vector3(0.69f * side, 0.62f, -0.15f), new Vector3(0.03f, 0.08f, 0.08f), Pal.Chili, 0);
                b.Box(new Vector3(0.66f * side, 0.4f, -0.15f), new Vector3(0.03f, 0.06f, 1.4f), Pal.Teal);
            }
            for (int i = 0; i < 7; i++)
                b.Wedge(new Vector3(-0.6f + i * 0.2f, 1.5f, 0.8f), new Vector3(0.14f, 0.03f, 0.1f), i % 2 == 0 ? Pal.Marigold : Pal.TruckPink, Quaternion.Euler(90, 0, 0));
            // headlight
            b.Sphere(new Vector3(0, 0.85f, 1.17f), new Vector3(0.18f, 0.18f, 0.08f), Pal.Hex("#FFF4C2"), 0);
            // wheels (oversized 1.3x)
            Wheel(b, new Vector3(0, 0.3f, 1.15f), 0.3f, 0.16f);
            Wheel(b, new Vector3(-0.68f, 0.3f, -0.55f), 0.3f, 0.16f);
            Wheel(b, new Vector3(0.68f, 0.3f, -0.55f), 0.3f, 0.16f);
            return b;
        });

        static void Wheel(MeshBuilder b, Vector3 c, float r, float w)
        {
            b.Cylinder(c + new Vector3(-w / 2, 0, 0), r, r, w, 10, Pal.Ink, Quaternion.Euler(0, 0, -90));
            b.Cylinder(c + new Vector3(-w / 2 - 0.01f, 0, 0), r * 0.45f, r * 0.45f, w + 0.02f, 8, Pal.Steel, Quaternion.Euler(0, 0, -90));
        }

        public static Mesh BicycleMesh(Color frame) => Cached("bike_" + ColorUtility.ToHtmlStringRGB(frame), b =>
        {
            // faces +z
            b.Torus(new Vector3(0, 0.36f, 0.55f), 0.32f, 0.045f, 12, 4, Pal.Ink, Quaternion.Euler(0, 0, 90));
            b.Torus(new Vector3(0, 0.36f, -0.55f), 0.32f, 0.045f, 12, 4, Pal.Ink, Quaternion.Euler(0, 0, 90));
            b.Box(new Vector3(0, 0.6f, 0.05f), new Vector3(0.08f, 0.08f, 0.95f), frame, Quaternion.Euler(-12, 0, 0));
            b.Box(new Vector3(0, 0.45f, -0.25f), new Vector3(0.08f, 0.5f, 0.08f), frame, Quaternion.Euler(-20, 0, 0));
            b.Box(new Vector3(0, 0.6f, 0.5f), new Vector3(0.08f, 0.5f, 0.08f), frame, Quaternion.Euler(15, 0, 0));
            b.Box(new Vector3(0, 0.9f, 0.55f), new Vector3(0.6f, 0.07f, 0.07f), Pal.DarkSteel);
            b.Box(new Vector3(0, 0.76f, -0.32f), new Vector3(0.16f, 0.07f, 0.26f), Pal.Ink);
            b.Box(new Vector3(0, 0.6f, -0.6f), new Vector3(0.3f, 0.05f, 0.3f), Pal.DarkSteel);
            return b;
        });

        public static Mesh TruckMesh(int style) => Cached("truck_" + style, b =>
        {
            Color body = style == 0 ? Pal.TruckPink : style == 1 ? Pal.Teal : Pal.Marigold;
            Color accent = style == 0 ? Pal.Teal : style == 1 ? Pal.Marigold : Pal.Chili;
            Color accent2 = style == 0 ? Pal.Marigold : style == 1 ? Pal.TruckPink : Pal.Royal;
            // faces +z ; cab at front
            b.BevelBox(new Vector3(0, 1.25f, 1.9f), new Vector3(2.2f, 1.6f, 1.5f), 0.25f, body);
            b.Box(new Vector3(0, 1.55f, 2.66f), new Vector3(1.9f, 0.6f, 0.05f), Pal.Hex("#BFE3EC"));
            // crown (truck-art forehead)
            b.Wedge(new Vector3(0, 2.35f, 2.35f), new Vector3(2.2f, 0.25f, 0.9f), accent, Quaternion.Euler(-90, 180, 0));
            b.Prism(new Vector3(0, 2.25f, 2.72f), MeshBuilder.StarOutline(6, 0.28f, 0.13f), 0.04f, accent2, Quaternion.Euler(-90, 0, 0));
            // cargo bed with tall wooden sides
            b.Box(new Vector3(0, 0.75f, -0.9f), new Vector3(2.3f, 0.3f, 4.2f), Pal.DarkWood);
            b.BevelBox(new Vector3(0, 1.75f, -0.9f), new Vector3(2.3f, 1.7f, 4.2f), 0.12f, body);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    float z = -2.5f + i * 1.05f;
                    b.Prism(new Vector3(1.16f * side, 1.8f, z), MeshBuilder.StarOutline(5, 0.3f, 0.13f), 0.04f, i % 2 == 0 ? accent : accent2, Quaternion.Euler(0, 0, side * 90));
                    b.Sphere(new Vector3(1.19f * side, 1.8f, z), new Vector3(0.04f, 0.14f, 0.14f), Pal.Chili, 0);
                }
                b.Box(new Vector3(1.17f * side, 2.45f, -0.9f), new Vector3(0.04f, 0.12f, 4.1f), accent);
                b.Box(new Vector3(1.17f * side, 1.1f, -0.9f), new Vector3(0.04f, 0.12f, 4.1f), accent);
            }
            // bumper eyes and chains
            b.Box(new Vector3(0, 0.55f, 2.68f), new Vector3(2.2f, 0.28f, 0.1f), Pal.Steel);
            b.Sphere(new Vector3(-0.5f, 0.55f, 2.74f), new Vector3(0.22f, 0.16f, 0.05f), Color.white, 0);
            b.Sphere(new Vector3(0.5f, 0.55f, 2.74f), new Vector3(0.22f, 0.16f, 0.05f), Color.white, 0);
            b.Sphere(new Vector3(-0.5f, 0.55f, 2.77f), new Vector3(0.1f, 0.1f, 0.04f), Pal.Ink, 0);
            b.Sphere(new Vector3(0.5f, 0.55f, 2.77f), new Vector3(0.1f, 0.1f, 0.04f), Pal.Ink, 0);
            for (int i = 0; i < 9; i++)
                b.Sphere(new Vector3(-1.0f + i * 0.25f, 0.32f, 2.7f), Vector3.one * 0.07f, Pal.Steel, 0);
            Wheel(b, new Vector3(-1.05f, 0.5f, 1.9f), 0.5f, 0.3f);
            Wheel(b, new Vector3(1.05f, 0.5f, 1.9f), 0.5f, 0.3f);
            Wheel(b, new Vector3(-1.05f, 0.5f, -2.0f), 0.5f, 0.3f);
            Wheel(b, new Vector3(1.05f, 0.5f, -2.0f), 0.5f, 0.3f);
            return b;
        });

        public static Mesh CarMesh(Color body) => Cached("car_" + ColorUtility.ToHtmlStringRGB(body), b =>
        {
            b.BevelBox(new Vector3(0, 0.55f, 0), new Vector3(1.6f, 0.55f, 3.2f), 0.3f, body);
            b.BevelBox(new Vector3(0, 1.05f, -0.2f), new Vector3(1.4f, 0.5f, 1.7f), 0.3f, Pal.Hex("#BFE3EC"));
            b.Box(new Vector3(0, 1.32f, -0.2f), new Vector3(1.42f, 0.06f, 1.6f), body);
            b.Sphere(new Vector3(-0.55f, 0.6f, 1.6f), new Vector3(0.25f, 0.14f, 0.06f), Pal.Hex("#FFF4C2"), 0);
            b.Sphere(new Vector3(0.55f, 0.6f, 1.6f), new Vector3(0.25f, 0.14f, 0.06f), Pal.Hex("#FFF4C2"), 0);
            Wheel(b, new Vector3(-0.8f, 0.32f, 1.0f), 0.32f, 0.2f);
            Wheel(b, new Vector3(0.8f, 0.32f, 1.0f), 0.32f, 0.2f);
            Wheel(b, new Vector3(-0.8f, 0.32f, -1.0f), 0.32f, 0.2f);
            Wheel(b, new Vector3(0.8f, 0.32f, -1.0f), 0.32f, 0.2f);
            return b;
        });

        // ---------------- props ----------------

        public static void Tree(MeshBuilder b, Vector3 p, float s, int variant)
        {
            b.Cylinder(p, 0.18f * s, 0.12f * s, 1.4f * s, 6, Pal.Hex("#7A5638"));
            Color g1 = Pal.LeafGreen, g2 = Pal.Mul(Pal.LeafGreen, 0.85f);
            if (variant % 2 == 0)
            {
                b.Sphere(p + new Vector3(0, 1.8f, 0) * s, new Vector3(1.6f, 1.3f, 1.6f) * s, g1, 1);
                b.Sphere(p + new Vector3(0.45f, 1.5f, 0.2f) * s, new Vector3(1.0f, 0.85f, 1.0f) * s, g2, 1);
                b.Sphere(p + new Vector3(-0.4f, 2.3f, -0.1f) * s, new Vector3(0.9f, 0.8f, 0.9f) * s, g2, 1);
            }
            else
            {
                b.Cylinder(p + new Vector3(0, 0.9f, 0) * s, 0.9f * s, 0.0f, 1.4f * s, 7, g2);
                b.Cylinder(p + new Vector3(0, 1.6f, 0) * s, 0.7f * s, 0.0f, 1.2f * s, 7, g1);
            }
        }

        public static void Bush(MeshBuilder b, Vector3 p, float s)
        {
            b.Sphere(p + new Vector3(0, 0.3f, 0) * s, new Vector3(0.9f, 0.6f, 0.8f) * s, Pal.Mul(Pal.LeafGreen, 0.9f), 1);
            b.Sphere(p + new Vector3(0.35f, 0.25f, 0.1f) * s, new Vector3(0.6f, 0.5f, 0.6f) * s, Pal.LeafGreen, 0);
        }

        public static void PotPlant(MeshBuilder b, Vector3 p, Color pot)
        {
            b.Cylinder(p, 0.18f, 0.24f, 0.36f, 8, pot);
            b.Sphere(p + new Vector3(0, 0.55f, 0), new Vector3(0.5f, 0.45f, 0.5f), Pal.LeafGreen, 1);
            b.Sphere(p + new Vector3(0.1f, 0.75f, 0.05f), new Vector3(0.14f, 0.14f, 0.14f), Pal.Marigold, 0);
        }

        public static void Bench(MeshBuilder b, Vector3 p, float yaw)
        {
            var r = Quaternion.Euler(0, yaw, 0);
            b.BevelBox(p + r * new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.1f, 0.5f), 0.05f, Pal.Hex("#9B6E4A"), r);
            b.Box(p + r * new Vector3(-0.75f, 0.2f, 0), new Vector3(0.12f, 0.4f, 0.42f), Pal.DarkWood, r);
            b.Box(p + r * new Vector3(0.75f, 0.2f, 0), new Vector3(0.12f, 0.4f, 0.42f), Pal.DarkWood, r);
        }

        public static void Charpai(MeshBuilder b, Vector3 p, float yaw)
        {
            var r = Quaternion.Euler(0, yaw, 0);
            b.Box(p + r * new Vector3(0, 0.42f, 0), new Vector3(1.9f, 0.06f, 1.0f), Pal.Hex("#E9D9B0"), r);
            for (int i = 0; i < 5; i++)
                b.Box(p + r * new Vector3(-0.76f + i * 0.38f, 0.455f, 0), new Vector3(0.05f, 0.012f, 0.98f), Pal.Chili, r);
            b.Box(p + r * new Vector3(0, 0.42f, 0.5f), new Vector3(2.0f, 0.1f, 0.08f), Pal.DarkWood, r);
            b.Box(p + r * new Vector3(0, 0.42f, -0.5f), new Vector3(2.0f, 0.1f, 0.08f), Pal.DarkWood, r);
            b.Box(p + r * new Vector3(0.96f, 0.42f, 0), new Vector3(0.08f, 0.1f, 1.0f), Pal.DarkWood, r);
            b.Box(p + r * new Vector3(-0.96f, 0.42f, 0), new Vector3(0.08f, 0.1f, 1.0f), Pal.DarkWood, r);
            foreach (var c in new[] { new Vector3(0.95f, 0, 0.48f), new Vector3(-0.95f, 0, 0.48f), new Vector3(0.95f, 0, -0.48f), new Vector3(-0.95f, 0, -0.48f) })
                b.Cylinder(p + r * c, 0.06f, 0.05f, 0.42f, 6, Pal.DarkWood);
        }

        public static void Crate(MeshBuilder b, Vector3 p, float yaw, Color c)
        {
            var r = Quaternion.Euler(0, yaw, 0);
            b.Box(p + new Vector3(0, 0.22f, 0), new Vector3(0.6f, 0.44f, 0.45f), c, r);
            b.Box(p + new Vector3(0, 0.44f, 0), new Vector3(0.62f, 0.04f, 0.47f), Pal.Mul(c, 0.8f), r);
        }

        public static void MilkCan(MeshBuilder b, Vector3 p)
        {
            b.Cylinder(p, 0.2f, 0.2f, 0.5f, 8, Pal.Steel);
            b.Cylinder(p + new Vector3(0, 0.5f, 0), 0.2f, 0.1f, 0.12f, 8, Pal.Steel);
            b.Cylinder(p + new Vector3(0, 0.62f, 0), 0.11f, 0.11f, 0.06f, 8, Pal.DarkSteel);
        }

        public static void Lamp(MeshBuilder b, Vector3 p)
        {
            b.Cylinder(p, 0.08f, 0.06f, 3.2f, 6, Pal.DarkSteel);
            b.Box(p + new Vector3(0.35f, 3.15f, 0), new Vector3(0.75f, 0.06f, 0.06f), Pal.DarkSteel);
            b.Cylinder(p + new Vector3(0.7f, 2.95f, 0), 0.16f, 0.06f, 0.2f, 6, Pal.Hex("#FFF4C2"));
        }

        /// <summary>Hand-painted sign board on two posts (no baked text: a big kettle and flowers).</summary>
        public static void SignBoard(MeshBuilder b, Vector3 p, float w, float h, float yaw)
        {
            var r = Quaternion.Euler(0, yaw, 0);
            b.Box(p + r * new Vector3(-w / 2 + 0.2f, h / 2 + 0.6f, 0.05f), new Vector3(0.12f, h + 1.2f, 0.12f), Pal.DarkWood, r);
            b.Box(p + r * new Vector3(w / 2 - 0.2f, h / 2 + 0.6f, 0.05f), new Vector3(0.12f, h + 1.2f, 0.12f), Pal.DarkWood, r);
            b.BevelBox(p + r * new Vector3(0, 1.2f + h / 2, 0), new Vector3(w, h, 0.1f), 0.08f, Pal.TruckPink, r);
            b.Box(p + r * new Vector3(0, 1.2f + h / 2, -0.06f), new Vector3(w - 0.25f, h - 0.25f, 0.02f), Pal.Marigold, r);
            // kettle silhouette
            var kc = p + r * new Vector3(0, 1.2f + h / 2 - 0.05f, -0.08f);
            b.Sphere(kc, new Vector3(h * 0.42f, h * 0.36f, 0.04f), Pal.Teal, 0, r);
            b.Box(kc + r * new Vector3(h * 0.28f, h * 0.05f, 0), new Vector3(h * 0.25f, 0.07f, 0.04f), Pal.Teal, r * Quaternion.Euler(0, 0, 35));
            b.Box(kc + r * new Vector3(0, h * 0.22f, 0), new Vector3(h * 0.12f, h * 0.08f, 0.04f), Pal.Teal, r);
            for (int i = 0; i < 2; i++)
            {
                float sx = (i == 0 ? -1 : 1) * (w / 2 - 0.35f);
                b.Prism(p + r * new Vector3(sx, 1.2f + h / 2, -0.1f), MeshBuilder.StarOutline(6, 0.2f, 0.09f), 0.03f, Pal.Chili, r * Quaternion.Euler(-90, 0, 0));
            }
        }

        public static Mesh CanopyMesh(float w, float d) => Cached($"canopy_{w}_{d}", b =>
        {
            // poles at the front, cloth slopes up to the back wall
            b.Cylinder(new Vector3(-w / 2, 0, -d / 2), 0.07f, 0.06f, 2.5f, 6, Pal.DarkWood);
            b.Cylinder(new Vector3(w / 2, 0, -d / 2), 0.07f, 0.06f, 2.5f, 6, Pal.DarkWood);
            int stripes = Mathf.RoundToInt(w / 0.8f);
            float sw = w / stripes;
            for (int i = 0; i < stripes; i++)
            {
                float x = -w / 2 + sw * (i + 0.5f);
                b.Box(new Vector3(x, 2.75f, 0), new Vector3(sw, 0.06f, d / Mathf.Cos(12 * Mathf.Deg2Rad)), i % 2 == 0 ? Pal.TruckPink : Pal.Teal, Quaternion.Euler(-12, 0, 0));
                b.Wedge(new Vector3(x, 2.4f, -d / 2 - 0.02f), new Vector3(sw * 0.9f, 0.05f, 0.25f), i % 2 == 0 ? Pal.Marigold : Pal.Parrot, Quaternion.Euler(90, 0, 180));
            }
            return b;
        });
    }
}
