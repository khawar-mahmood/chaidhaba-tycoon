using System.Globalization;

namespace ChaiTycoon
{
    public static class Fmt
    {
        /// <summary>Amounts over 10,000 are shortened (12.5K, 1.2M) so counters never overflow.</summary>
        public static string Cash(double v)
        {
            if (v < 0) return "-" + Cash(-v);
            if (v < 10000) return ((long)System.Math.Floor(v)).ToString("#,0", CultureInfo.InvariantCulture);
            string[] units = { "K", "M", "B", "T" };
            double x = v;
            int u = -1;
            while (x >= 1000 && u < units.Length - 1) { x /= 1000; u++; }
            string s = x >= 100 ? System.Math.Floor(x).ToString("0", CultureInfo.InvariantCulture)
                : (System.Math.Floor(x * 10) / 10).ToString("0.#", CultureInfo.InvariantCulture);
            return s + units[u];
        }

        public static string Time(double seconds)
        {
            int s = (int)seconds;
            if (s >= 3600) return $"{s / 3600} h {(s % 3600) / 60} min";
            if (s >= 60) return $"{s / 60} min";
            return $"{s} s";
        }

        public static string Clock(double seconds)
        {
            int s = (int)seconds;
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
