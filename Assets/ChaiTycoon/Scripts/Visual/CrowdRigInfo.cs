using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// On each crowd prefab (made by Editor/CrowdImport.cs). The crowd shares one set of clips (Rafi's idle,
    /// walk and run, the cook's sit), and those clips carry their source's hip height. Every model's hips sit
    /// a little higher or lower, so CharRig adds this model's shift to the hips after the animation.
    /// </summary>
    public class CrowdRigInfo : MonoBehaviour
    {
        public string[] clips = new string[0];
        public Vector3[] hipShift = new Vector3[0];

        public Vector3 ShiftFor(string clip)
        {
            for (int i = 0; i < clips.Length; i++) if (clips[i] == clip) return hipShift[i];
            return Vector3.zero;
        }
    }
}
