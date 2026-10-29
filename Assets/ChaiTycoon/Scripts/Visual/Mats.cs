using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>Shared materials. One toon material for nearly everything (colour lives in vertices).</summary>
    public static class Mats
    {
        static Material toon, toonFlat, toonNoOutlineGlow;
        static readonly Dictionary<Texture, Material> alphaMats = new Dictionary<Texture, Material>();

        static Material Load(string res, string shader)
        {
            var m = Resources.Load<Material>("ChaiMats/" + res);
            if (m != null) return new Material(m);
            var s = Shader.Find(shader);
            if (s == null)
            {
                Debug.LogError("Missing shader " + shader);
                s = Shader.Find("Universal Render Pipeline/Unlit");
            }
            return new Material(s);
        }

        public static Material Toon => toon ? toon : (toon = Load("Toon", "Chai/Toon"));
        public static Material ToonFlat => toonFlat ? toonFlat : (toonFlat = Load("ToonFlat", "Chai/ToonFlat"));


        /// <summary>A fresh toon material instance (for objects that need their own glow / tint).</summary>
        public static Material ToonInstance(bool outline = true) => new Material(outline ? Toon : ToonFlat);

        public static Material Alpha(Texture tex)
        {
            if (tex != null && alphaMats.TryGetValue(tex, out var m) && m) return m;
            m = Load("UnlitAlpha", "Chai/UnlitAlpha");
            m.mainTexture = tex;
            m.SetTexture("_MainTex", tex);
            if (tex != null) alphaMats[tex] = m;
            return m;
        }

        public static Material AlphaInstance(Texture tex)
        {
            var m = Load("UnlitAlpha", "Chai/UnlitAlpha");
            m.SetTexture("_MainTex", tex);
            return m;
        }
    }
}
