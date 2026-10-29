using System;
using System.Collections;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>Minimal coroutine tweens. Unscaled tweens keep UI moving while the world is paused.</summary>
    public class Tw : MonoBehaviour
    {
        static Tw inst;
        static Tw I
        {
            get
            {
                if (inst) return inst;
                var go = new GameObject("[Tween]");
                DontDestroyOnLoad(go);
                inst = go.AddComponent<Tw>();
                return inst;
            }
        }

        public static Coroutine Run(float duration, Action<float> step, Action done = null, bool unscaled = false, MonoBehaviour host = null)
        {
            var h = host != null && host.isActiveAndEnabled ? host : I;
            return h.StartCoroutine(Routine(duration, step, done, unscaled));
        }

        public static Coroutine Delay(float seconds, Action done, bool unscaled = false, MonoBehaviour host = null)
        {
            var h = host != null && host.isActiveAndEnabled ? host : I;
            return h.StartCoroutine(DelayRoutine(seconds, done, unscaled));
        }

        public static void Stop(Coroutine c)
        {
            if (c != null && inst) inst.StopCoroutine(c);
        }

        static IEnumerator Routine(float d, Action<float> step, Action done, bool unscaled)
        {
            float t = 0;
            while (t < d)
            {
                try { step?.Invoke(d <= 0 ? 1 : t / d); }
                catch (Exception e) { Debug.LogException(e); yield break; }
                yield return null;
                t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            }
            try { step?.Invoke(1); done?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static IEnumerator DelayRoutine(float s, Action done, bool unscaled)
        {
            if (unscaled) yield return new WaitForSecondsRealtime(s);
            else yield return new WaitForSeconds(s);
            try { done?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ---------- easing ----------
        public static float OutQuad(float t) => 1 - (1 - t) * (1 - t);
        public static float InQuad(float t) => t * t;
        public static float InOutQuad(float t) => t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
        public static float OutCubic(float t) => 1 - Mathf.Pow(1 - t, 3);
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1;
            return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
        }
        /// <summary>0 → 1.1 → 1 pop used for every build / spawn.</summary>
        public static float Pop(float t) => t < 0.6f ? Mathf.Lerp(0, 1.12f, OutQuad(t / 0.6f)) : Mathf.Lerp(1.12f, 1f, InOutQuad((t - 0.6f) / 0.4f));

        /// <summary>Scale-pop a transform from zero to its current scale.</summary>
        public static void PopIn(Transform t, float dur = 0.35f, bool unscaled = false)
        {
            if (!t) return;
            var target = t.localScale;
            t.localScale = Vector3.zero;
            Run(dur, k => { if (t) t.localScale = target * Pop(k); }, null, unscaled);
        }

        static readonly System.Collections.Generic.Dictionary<Transform, Vector3> punchBase = new System.Collections.Generic.Dictionary<Transform, Vector3>();
        static readonly System.Collections.Generic.Dictionary<Transform, Coroutine> punchRun = new System.Collections.Generic.Dictionary<Transform, Coroutine>();

        /// <summary>Quick squash-and-stretch punch on a transform. Overlapping punches restart cleanly.</summary>
        public static void Punch(Transform t, float amount = 0.15f, float dur = 0.25f, bool unscaled = false)
        {
            if (!t) return;
            if (!punchBase.TryGetValue(t, out var baseS)) { baseS = t.localScale; punchBase[t] = baseS; }
            if (punchRun.TryGetValue(t, out var running) && running != null && inst) inst.StopCoroutine(running);
            punchRun[t] = Run(dur, k =>
            {
                if (!t) return;
                float s = Mathf.Sin(k * Mathf.PI) * amount * (1 - k * 0.5f);
                t.localScale = new Vector3(baseS.x * (1 + s), baseS.y * (1 - s), baseS.z * (1 + s));
            }, () => { if (t) t.localScale = baseS; punchRun.Remove(t); }, unscaled);
        }
    }
}
