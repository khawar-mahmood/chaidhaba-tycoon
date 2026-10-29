using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// The visible tower of items in a character's hands. It sways with movement — a tall,
    /// wobbling stack is the game's signature visual.
    /// </summary>
    public class CarryStack : MonoBehaviour
    {
        public Transform anchor;
        public readonly List<ItemObj> items = new List<ItemObj>();
        public int capacity = 4;
        public CharRig rig;
        public Action Changed;
        /// <summary>The player's stack: landings play a rising pop, and a chime when it fills up.</summary>
        public bool Juicy;

        readonly HashSet<ItemObj> landing = new HashSet<ItemObj>();
        Vector3 lastPos;
        Vector2 sway, swayVel;

        public int Count => items.Count;
        public bool IsFull => items.Count >= capacity;
        public bool IsEmpty => items.Count == 0;

        public bool Has(ItemId id)
        {
            foreach (var i in items) if (i.id == id) return true;
            return false;
        }

        public int CountOf(ItemId id)
        {
            int n = 0;
            foreach (var i in items) if (i.id == id) n++;
            return n;
        }

        float SlotY(int idx)
        {
            float y = 0;
            for (int i = 0; i < idx && i < items.Count; i++) y += Items.Height(items[i].id);
            return y;
        }

        public float TopY => SlotY(items.Count);

        /// <summary>Adds an item with a hop from wherever it currently is.</summary>
        public void Add(ItemObj it, float hopTime = 0.22f)
        {
            items.Add(it);
            int idx = items.Count - 1;
            landing.Add(it);
            var t = it.transform;
            t.SetParent(null, true);
            var start = t.position;
            var startRot = t.rotation;
            if (it.hop != null) Tw.Stop(it.hop);
            it.hop = Tw.Run(hopTime, k =>
            {
                if (!t || !anchor) return;
                int i = items.IndexOf(it);
                if (i < 0) return;
                var end = anchor.TransformPoint(new Vector3(0, SlotY(i), 0));
                var p = Vector3.Lerp(start, end, Tw.OutQuad(k));
                p.y += Mathf.Sin(k * Mathf.PI) * 0.7f;
                t.position = p;
                t.rotation = Quaternion.Slerp(startRot, anchor.rotation, k);
            }, () =>
            {
                landing.Remove(it);
                if (!t || !anchor) return;
                if (!items.Contains(it)) return;
                t.SetParent(anchor, true);
                t.localRotation = Quaternion.identity;
                rig?.Squash(0.08f);
                // the landing: the item bounces in scale, and on Rafi's stack each one sounds a little higher
                var baseScale = t.localScale;
                Tw.Run(0.2f, k => { if (t) t.localScale = baseScale * (1f + Mathf.Sin(k * Mathf.PI) * 0.35f); }, () => { if (t) t.localScale = baseScale; });
                if (Juicy)
                {
                    int i = items.IndexOf(it);
                    Synth.Play("pop", Mathf.Min(1.9f, 1f + i * 0.09f), 0.75f, 0.02f);
                    if (IsFull && i == items.Count - 1) Synth.Play("ding", 1.5f, 0.5f, 0.1f);
                }
            });
            if (rig) rig.carrying = true;
            Changed?.Invoke();
        }

        /// <summary>Removes the top-most item of this type (or any, when id is null).</summary>
        public ItemObj Take(ItemId? id)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (id.HasValue && items[i].id != id.Value) continue;
                var it = items[i];
                items.RemoveAt(i);
                landing.Remove(it);
                if (it.hop != null) { Tw.Stop(it.hop); it.hop = null; }
                it.transform.SetParent(null, true);
                if (rig) rig.carrying = items.Count > 0;
                Changed?.Invoke();
                return it;
            }
            return null;
        }

        public void Clear()
        {
            foreach (var i in items) if (i) Destroy(i.gameObject);
            items.Clear();
            landing.Clear();
            if (rig) rig.carrying = false;
            Changed?.Invoke();
        }

        void LateUpdate()
        {
            if (!anchor) return;
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            var vel = (anchor.position - lastPos) / dt;
            lastPos = anchor.position;
            var local = Quaternion.Inverse(anchor.rotation) * vel;
            var target = new Vector2(-local.x, -local.z) * 0.02f;
            // spring toward the lag target so the tower wobbles after stopping
            swayVel += ((target - sway) * 90f - swayVel * 9f) * dt;
            sway += swayVel * dt;
            sway = Vector2.ClampMagnitude(sway, 0.35f);

            int n = items.Count;
            float y = 0;
            for (int i = 0; i < n; i++)
            {
                var it = items[i];
                float h = Items.Height(it.id);
                if (!landing.Contains(it) && it.transform.parent == anchor)
                {
                    float f = Mathf.Pow((i + 1) / 6f, 1.4f);
                    var target3 = new Vector3(sway.x * f, y, sway.y * f);
                    var lp = it.transform.localPosition;
                    it.transform.localPosition = Vector3.Lerp(lp, target3, 1 - Mathf.Exp(-dt * 22f));
                    it.transform.localRotation = Quaternion.Euler(sway.y * f * 25f, 0, -sway.x * f * 25f);
                }
                y += h;
            }
        }
    }
}
