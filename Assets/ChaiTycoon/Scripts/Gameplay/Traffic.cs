using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>Painted trucks and cars rumble past on the far lane, horns blaring, for atmosphere.</summary>
    public class Traffic : MonoBehaviour
    {
        Location loc;
        float timer = 6f;
        readonly List<(Transform t, float speed)> cars = new List<(Transform, float)>();

        public void Init(Location l) { loc = l; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0 || loc == null) return;
            timer -= dt;
            if (timer <= 0 && cars.Count < 3)
            {
                timer = Random.Range(9f, 17f);
                bool truck = Random.value < 0.55f;
                bool ltr = Random.value < 0.5f;
                int v = Random.Range(0, truck ? 3 : 4);
                Mesh m = truck ? ArtLib.Get(new[] { "TruckPink", "TruckTeal", "TruckMarigold" }[v]) : ArtLib.Get(new[] { "CarRed", "CarBlue", "CarWhite", "Car" }[v]);
                if (!m) m = truck ? Models.TruckMesh(v) : Models.CarMesh(new[] { Pal.Chili, Pal.Royal, Pal.Hex("#E9E4DA"), Pal.Teal }[v]);
                var go = Models.Obj(truck ? "truck" : "car", m, transform);
                Models.Blob(go.transform, truck ? 4.5f : 3.2f, 0.3f);
                float z = ltr ? loc.RoadFarZ + 0.2f : loc.RoadFarZ - 0.1f;
                float x = ltr ? -loc.SpawnX - 8 : loc.SpawnX + 8;
                go.transform.position = new Vector3(x, 0, z);
                go.transform.rotation = Quaternion.LookRotation(ltr ? Vector3.right : Vector3.left);
                if (truck) go.transform.localScale = Vector3.one * 0.85f;
                cars.Add((go.transform, (ltr ? 1 : -1) * (truck ? 7f : 9.5f)));
                if (truck) Synth.Play("truckhorn", Random.Range(0.92f, 1.05f), 0.35f, 1f);
            }
            for (int i = cars.Count - 1; i >= 0; i--)
            {
                var (t, s) = cars[i];
                if (!t) { cars.RemoveAt(i); continue; }
                t.position += Vector3.right * s * dt;
                if (Mathf.Abs(t.position.x) > loc.SpawnX + 12)
                {
                    Destroy(t.gameObject);
                    cars.RemoveAt(i);
                }
            }
        }
    }
}
