using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Makes a floor ring react to Rafi: it breathes gently while idle, pops and lights up when he steps
    /// on, and can show a filling disc for actions that take a moment.
    /// </summary>
    public class Hotspot : MonoBehaviour
    {
        Material mat, fillMat;
        Transform fill;
        Color idleColor, onColor;
        Vector3 baseScale;
        bool on;
        float blend, pop, t, progress, shownProgress;

        public static Hotspot Attach(MeshRenderer ring, Color onColor)
        {
            var h = ring.gameObject.AddComponent<Hotspot>();
            h.mat = ring.sharedMaterial;
            h.idleColor = h.mat.GetColor("_BaseColor");
            h.onColor = Pal.WithA(onColor, 0.95f);
            h.baseScale = ring.transform.localScale;
            h.t = Random.value * 10f; // rings don't breathe in sync
            return h;
        }

        public bool On => on;

        public void SetOn(bool v)
        {
            if (v == on) return;
            on = v;
            if (!v) { progress = 0; return; }
            pop = 1;
            Synth.Play("tick", 1.7f, 0.22f, 0.02f);
            Game.I?.Fx.Puff(transform.position + Vector3.up * 0.05f, 4, Pal.Lerp(onColor, Color.white, 0.4f), 0.25f);
        }

        /// <summary>0..1 fill shown inside the ring (0 hides it).</summary>
        public void SetProgress(float p)
        {
            progress = p;
            if (p > 0 && fill == null)
            {
                var r = Models.AlphaQuad("fill", transform.parent, SpriteGen.BlobTexture(), transform.localPosition + Vector3.up * 0.002f, Vector2.one, Pal.WithA(onColor, 0.55f), false);
                fill = r.transform;
                fillMat = r.sharedMaterial;
                fill.localScale = Vector3.zero;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            t += dt;
            blend = Mathf.MoveTowards(blend, on ? 1f : 0f, dt * 7f);
            pop = Mathf.MoveTowards(pop, 0f, dt * 3.5f);

            float breathe = 1f + Mathf.Sin(t * (on ? 7f : 2.2f)) * (on ? 0.035f : 0.025f);
            float s = Mathf.Lerp(1f, 1.1f, blend) * breathe + Mathf.Sin(pop * Mathf.PI) * 0.18f;
            transform.localScale = new Vector3(baseScale.x * s, 1, baseScale.z * s);
            mat.SetColor("_BaseColor", Color.Lerp(idleColor, onColor, blend));

            if (fill != null)
            {
                shownProgress = Mathf.MoveTowards(shownProgress, on ? progress : 0f, dt * 4f);
                float f = shownProgress * baseScale.x * 0.95f;
                fill.localScale = new Vector3(f, 1, f);
                fillMat.SetColor("_BaseColor", Pal.WithA(onColor, 0.6f + 0.35f * shownProgress));
            }
        }

        void OnDestroy()
        {
            if (fill) Destroy(fill.gameObject);
        }
    }
}
