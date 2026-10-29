using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Walking routes for customers and staff (only Rafi has a CharacterController; they just walk between
    /// points): a 0.25 m grid of the yard marked from the solid colliders, A* over it, and the result pulled
    /// straight wherever nothing is in the way. A leg with a clear line stays a straight walk.
    /// </summary>
    public static class NavGrid
    {
        const float Cell = 0.25f;
        const float Radius = 0.26f;          // a body's half width, kept clear of everything solid
        const float EndSkip = 0.45f;         // the first and last bit of a leg may graze (a slot right at a counter)
        const float MinX = -11.25f, MaxX = 11.5f, MinZ = -6.0f, MaxZ = 7.7f;

        static int W, H;
        static bool[] blocked;
        static bool dirty = true;
        static float changedAt = -99f, builtAt = -99f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { blocked = null; dirty = true; changedAt = builtAt = -99f; }

        /// <summary>
        /// Something solid was added: the grid is redrawn before the next route, and a few more times over the
        /// next second and a half while new pieces pop in to full size.
        /// </summary>
        public static void MarkDirty() { dirty = true; changedAt = Time.time; }

        /// <summary>The fences that keep Rafi in the yard: customers walk in from beyond them, so routes ignore them.</summary>
        public const string FenceName = "fence";

        static readonly Collider[] hits = new Collider[16];

        static void Build()
        {
            dirty = false;
            builtAt = Time.time;
            Physics.SyncTransforms();
            W = Mathf.CeilToInt((MaxX - MinX) / Cell);
            H = Mathf.CeilToInt((MaxZ - MinZ) / Cell);
            if (blocked == null || blocked.Length != W * H) blocked = new bool[W * H];
            var half = new Vector3(Cell * 0.5f + Radius, 0.35f, Cell * 0.5f + Radius);
            for (int z = 0; z < H; z++)
                for (int x = 0; x < W; x++)
                {
                    var c = CellPos(x + z * W);
                    int n = Physics.OverlapBoxNonAlloc(new Vector3(c.x, 0.45f, c.z), half, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    bool b = false;
                    for (int i = 0; i < n && !b; i++) b = Solid(hits[i]);
                    blocked[x + z * W] = b;
                }
        }

        static bool Solid(Collider c) => c != null && !(c is CharacterController) && c.gameObject.name != FenceName;

        static Vector3 CellPos(int i) => new Vector3(MinX + (i % W + 0.5f) * Cell, 0, MinZ + (i / W + 0.5f) * Cell);

        static int CellOf(Vector3 p)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((p.x - MinX) / Cell), 0, W - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((p.z - MinZ) / Cell), 0, H - 1);
            return x + z * W;
        }

        static bool Blocked(Vector3 p)
        {
            if (p.x < MinX || p.x >= MaxX || p.z < MinZ || p.z >= MaxZ) return false;   // out on the street: open
            return blocked[CellOf(p)];
        }

        /// <summary>
        /// Fills <paramref name="into"/> with the waypoints for walking from <paramref name="from"/> through
        /// <paramref name="pts"/>, with detours round anything solid. The given points are always kept.
        /// </summary>
        public static void Route(Vector3 from, IList<Vector3> pts, List<Vector3> into)
        {
            into.Clear();
            float now = Time.time;
            if (dirty || blocked == null || (now - changedAt < 1.5f && now - builtAt > 0.3f)) Build();
            var a = from;
            for (int i = 0; i < pts.Count; i++)
            {
                var b = pts[i];
                if (!Clear(a, b, EndSkip)) Detour(a, b, into);
                into.Add(b);
                a = b;
            }
        }

        /// <summary>No blocked cell along the line (ignoring <paramref name="skip"/> metres at each end).</summary>
        static bool Clear(Vector3 a, Vector3 b, float skip)
        {
            var d = b - a;
            d.y = 0;
            float len = d.magnitude;
            if (len < 0.01f) return true;
            int n = Mathf.CeilToInt(len / (Cell * 0.5f));
            for (int i = 1; i < n; i++)
            {
                float s = len * i / n;
                if (s < skip || len - s < skip) continue;
                if (Blocked(a + d * (s / len))) return false;
            }
            return true;
        }

        // ------------------------------------------------------------ A*

        static float[] gCost;
        static int[] parent, seen;
        static int stamp;
        static readonly List<int> heap = new List<int>();
        static float[] fCost;
        static readonly List<int> cells = new List<int>();

        static void Detour(Vector3 a, Vector3 b, List<Vector3> into)
        {
            int s = NearestFree(CellOf(a)), g = NearestFree(CellOf(b));
            if (s < 0 || g < 0 || s == g || !Search(s, g)) return;     // no way round: walk straight as before
            cells.Clear();
            for (int c = g; c != s; c = parent[c]) cells.Add(c);
            cells.Add(s);
            cells.Reverse();
            // pull the cell chain straight: from each corner, jump to the furthest cell still in clear sight,
            // until the goal itself is in sight
            var cur = a;
            int k = -1;
            while (k < cells.Count - 1 && !Clear(cur, b, EndSkip))
            {
                int far = k + 1;
                for (int j = cells.Count - 1; j > k + 1; j--)
                    if (Clear(cur, CellPos(cells[j]), k < 0 ? EndSkip : 0f)) { far = j; break; }
                cur = CellPos(cells[far]);
                into.Add(cur);
                k = far;
            }
        }

        static int NearestFree(int c)
        {
            if (!blocked[c]) return c;
            int cx = c % W, cz = c / W;
            for (int r = 1; r <= 8; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || z < 0 || x >= W || z >= H) continue;
                        if (!blocked[x + z * W]) return x + z * W;
                    }
            return -1;
        }

        static bool Search(int s, int g)
        {
            int n = W * H;
            if (gCost == null || gCost.Length != n) { gCost = new float[n]; fCost = new float[n]; parent = new int[n]; seen = new int[n]; stamp = 0; }
            stamp += 2;                        // seen == stamp: open, stamp + 1: closed
            heap.Clear();
            gCost[s] = 0;
            fCost[s] = H8(s, g);
            seen[s] = stamp;
            Push(s);
            int gx = g % W, gz = g / W;
            while (heap.Count > 0)
            {
                int c = Pop();
                if (c == g) return true;
                if (seen[c] == stamp + 1) continue;
                seen[c] = stamp + 1;
                int cx = c % W, cz = c / W;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || z < 0 || x >= W || z >= H) continue;
                        int nb = x + z * W;
                        if (blocked[nb] || seen[nb] == stamp + 1) continue;
                        // diagonals don't cut corners
                        if (dx != 0 && dz != 0 && (blocked[cx + dx + cz * W] || blocked[cx + (cz + dz) * W])) continue;
                        float cost = gCost[c] + (dx != 0 && dz != 0 ? 1.4142f : 1f);
                        if (seen[nb] == stamp && cost >= gCost[nb]) continue;
                        gCost[nb] = cost;
                        fCost[nb] = cost + H8(nb, g);
                        parent[nb] = c;
                        seen[nb] = stamp;
                        Push(nb);
                    }
            }
            return false;
        }

        static float H8(int a, int b)
        {
            int dx = Mathf.Abs(a % W - b % W), dz = Mathf.Abs(a / W - b / W);
            return Mathf.Max(dx, dz) + 0.4142f * Mathf.Min(dx, dz);
        }

        static void Push(int c)
        {
            heap.Add(c);
            int i = heap.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (fCost[heap[p]] <= fCost[heap[i]]) break;
                (heap[p], heap[i]) = (heap[i], heap[p]);
                i = p;
            }
        }

        static int Pop()
        {
            int top = heap[0];
            int last = heap[heap.Count - 1];
            heap.RemoveAt(heap.Count - 1);
            if (heap.Count > 0)
            {
                heap[0] = last;
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < heap.Count && fCost[heap[l]] < fCost[heap[m]]) m = l;
                    if (r < heap.Count && fCost[heap[r]] < fCost[heap[m]]) m = r;
                    if (m == i) break;
                    (heap[m], heap[i]) = (heap[i], heap[m]);
                    i = m;
                }
            }
            return top;
        }
    }
}
