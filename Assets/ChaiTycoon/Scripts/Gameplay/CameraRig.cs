using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Fixed 3/4 top-down camera (50°) that follows the player closely within the location's bounds. Short pans to new
    /// unlocks (under 1.5 s, never blocking input) and a tiny shake on builds.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        Transform target;
        Bounds bounds;
        public float Pitch = 50f;
        public float Distance = 21f;
        float distanceTarget = 21f;
        Vector3 focus, focusVel;
        Vector3 panPoint;
        float panT = -1;
        float shake;
        public Vector3 FocusOffset = new Vector3(0, 0, -1.2f);
        Vector3? overrideFocus;
        float overrideDistance;

        public static CameraRig Create()
        {
            var cam = Camera.main;
            var rig = cam.gameObject.AddComponent<CameraRig>();
            rig.Cam = cam;
            return rig;
        }

        public void SetTarget(Transform t, Bounds b, float distance, bool snap)
        {
            target = t;
            bounds = b;
            distanceTarget = distance;
            if (snap)
            {
                Distance = distance;
                focus = Clamp(t.position + FocusOffset);
                Apply();
            }
        }

        Vector3 Clamp(Vector3 p)
        {
            var min = bounds.min; var max = bounds.max;
            return new Vector3(Mathf.Clamp(p.x, min.x, max.x), 0, Mathf.Clamp(p.z, min.z, max.z));
        }

        /// <summary>Short look at a new unlock, then straight back to the player.</summary>
        public void PanTo(Vector3 p)
        {
            panPoint = new Vector3(p.x, 0, p.z - 1.2f);
            panT = 0;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        /// <summary>Used by the travel celebration and world map to fly somewhere.</summary>
        public void Override(Vector3? p, float distance = 0)
        {
            overrideFocus = p;
            overrideDistance = distance;
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            Vector3 desired = Clamp(target.position + FocusOffset);
            float smooth = 0.16f;     // follows closely (a little softness so turns don't jerk)
            if (panT >= 0)
            {
                panT += dt;
                // 0.45 s out, 0.4 s hold, 0.45 s back
                float w = panT < 0.45f ? Tw.InOutQuad(panT / 0.45f) : panT < 0.85f ? 1 : 1 - Tw.InOutQuad((panT - 0.85f) / 0.45f);
                desired = Vector3.Lerp(desired, panPoint, Mathf.Clamp01(w));
                smooth = 0.12f;
                if (panT > 1.3f) panT = -1;
            }
            float distT = distanceTarget;
            if (overrideFocus.HasValue)
            {
                desired = overrideFocus.Value;
                smooth = 0.5f;
                if (overrideDistance > 0) distT = overrideDistance;
            }
            focus = Vector3.SmoothDamp(focus, desired, ref focusVel, smooth, Mathf.Infinity, Mathf.Max(dt, 0.0001f));
            Distance = Mathf.Lerp(Distance, distT, 1 - Mathf.Exp(-dt * 2f));
            Apply();
            if (shake > 0)
            {
                shake = Mathf.Max(0, shake - dt * 0.8f);
                if (!WebBridge.AdPlaying)
                    transform.position += new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0) * shake * 0.6f;
            }
        }

        void Apply()
        {
            var rot = Quaternion.Euler(Pitch, 0, 0);
            transform.rotation = rot;
            transform.position = focus - rot * Vector3.forward * Distance * AspectFit();
        }

        Camera cam;

        /// <summary>
        /// The framing is made for 16:9. Narrower windows (CrazyGames resizes its frame freely: 4:3, square, a phone's
        /// odd sizes) pull the camera back so the yard still fits across; wider ones keep the 16:9 distance.
        /// </summary>
        float AspectFit()
        {
            if (!cam) cam = GetComponent<Camera>();
            if (!cam) cam = Camera.main;
            float aspect = cam ? cam.aspect : 16f / 9f;
            return Mathf.Clamp((16f / 9f) / Mathf.Max(0.3f, aspect), 1f, 1.7f);
        }

        public void SetDistance(float d) => distanceTarget = d;
    }
}
