using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>The GDD colour palette. All colours are sRGB.</summary>
    public static class Pal
    {
        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString(h, out var c);
            return c;
        }

        // Environment
        public static readonly Color Sand = Hex("#E8D2A6");
        public static readonly Color Clay = Hex("#C98B5E");
        public static readonly Color DarkWood = Hex("#6B4A33");
        public static readonly Color LeafGreen = Hex("#7FA85A");
        public static readonly Color BlobShadow = Hex("#3B2F45");
        public static readonly Color Asphalt = Hex("#6E6470");
        public static readonly Color Dirt = Hex("#D9BC8A");
        public static readonly Color Stone = Hex("#B9AFA6");
        public static readonly Color Steel = Hex("#C9CDD3");
        public static readonly Color DarkSteel = Hex("#5D6470");
        public static readonly Color Charcoal = Hex("#3E3A40");
        public static readonly Color White = Hex("#FBF7EF");

        // Truck-art accents
        public static readonly Color TruckPink = Hex("#F2459A");
        public static readonly Color Marigold = Hex("#FFC21A");
        public static readonly Color Parrot = Hex("#2FBF5B");
        public static readonly Color Chili = Hex("#E8382F");
        public static readonly Color Teal = Hex("#14A8B5");
        public static readonly Color Royal = Hex("#2E5BD8");
        public static readonly Color Purple = Hex("#8E44C9");

        // Food
        public static readonly Color ChaiTea = Hex("#B0703C");
        public static readonly Color Paratha = Hex("#E9B45C");
        public static readonly Color Samosa = Hex("#D9922E");
        public static readonly Color Lassi = Hex("#FFF6E0");

        // Skin
        public static readonly Color[] Skins =
        {
            Hex("#F1C9A5"), Hex("#D9A47A"), Hex("#B97C55"), Hex("#8C5A3C")
        };

        // UI
        public static readonly Color Ink = Hex("#2B1E2F");
        public static readonly Color Cream = Hex("#FFF4DE");
        public static readonly Color CashGold = Hex("#FFD23F");
        public static readonly Color ButtonGrey = Hex("#B8AFA4");
        public static readonly Color PatienceGreen = Hex("#4CCB5E");
        public static readonly Color PatienceYellow = Hex("#FFC21A");
        public static readonly Color PatienceRed = Hex("#E8382F");

        // Muted clothing colours for customers, so served food stands out
        public static readonly Color[] Clothes =
        {
            Hex("#8FA3B8"), Hex("#B79E86"), Hex("#9AAE8C"), Hex("#C4A9A0"), Hex("#A69CB8"),
            Hex("#D8CFC0"), Hex("#7F8C99"), Hex("#B5967A"), Hex("#9DB3A8"), Hex("#C9B79C")
        };

        public static readonly Color[] HairColors = { Hex("#2A2224"), Hex("#3B2A22"), Hex("#1E1A1C"), Hex("#4A3A30") };

        public static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
        public static Color WithA(Color c, float a) => new Color(c.r, c.g, c.b, a);
        public static Color Lerp(Color a, Color b, float t) => Color.Lerp(a, b, t);
    }
}
