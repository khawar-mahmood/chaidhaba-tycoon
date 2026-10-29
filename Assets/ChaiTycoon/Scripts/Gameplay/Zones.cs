using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>Every action happens automatically by walking onto a zone.</summary>
    public interface IZone
    {
        bool ZoneActive { get; }
        bool Contains(Vector3 p, float extraRadius);
        void OnEnter(Agent who);
        void OnStay(Agent who, float dt);
        void OnExit(Agent who);
    }

    /// <summary>Somewhere food gets handed over: a counter lane, a rickshaw spot or a dining table.</summary>
    public interface IServeTarget
    {
        Vector3 ServePos { get; }
        string ServeKind { get; }
        /// <summary>How many of this item the customers here still need.</summary>
        int Demand(ItemId id);
        /// <summary>Someone who can be served right now wants an item in this stack.</summary>
        bool FrontWantsAnyOf(CarryStack s);
        bool ServeOne(Agent who);
    }

    /// <summary>Anything that walks around and can carry items: the player and staff.</summary>
    public abstract class Agent : MonoBehaviour
    {
        public CarryStack Stack;
        public CharRig Rig;
        public abstract bool IsPlayer { get; }
        public virtual float PickInterval => 0.25f;
        public virtual float ServeInterval => 0.25f;
        public Vector3 Pos => transform.position;
    }

    public static class Zones
    {
        public static readonly List<IZone> All = new List<IZone>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => All.Clear();

        public static void Add(IZone z) { if (!All.Contains(z)) All.Add(z); }
        public static void Remove(IZone z) => All.Remove(z);

        public static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
