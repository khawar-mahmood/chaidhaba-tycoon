using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    [Serializable] public class KV { public string k; public int v; public KV(string k, int v) { this.k = k; this.v = v; } }
    [Serializable] public class KF { public string k; public float v; public KF(string k, float v) { this.k = k; this.v = v; } }

    public static class KVExt
    {
        public static int Get(this List<KV> list, string k, int def = 0)
        {
            if (list == null) return def;
            foreach (var e in list) if (e.k == k) return e.v;
            return def;
        }
        public static void Set(this List<KV> list, string k, int v)
        {
            foreach (var e in list) if (e.k == k) { e.v = v; return; }
            list.Add(new KV(k, v));
        }
        public static float Get(this List<KF> list, string k, float def = 0)
        {
            if (list == null) return def;
            foreach (var e in list) if (e.k == k) return e.v;
            return def;
        }
        public static void Set(this List<KF> list, string k, float v)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].k == k) { list[i].v = v; return; }
            list.Add(new KF(k, v));
        }
        public static void Remove(this List<KF> list, string k) => list.RemoveAll(e => e.k == k);
    }

    [Serializable]
    public class StaffSave
    {
        public string role;
        public int speed;
        public int carry;
        public string assigned;
    }

    [Serializable]
    public class LocSave
    {
        public int id;
        public bool done;
        public int stars = 1;
        public List<string> unlocks = new List<string>();
        public List<KF> tilePaid = new List<KF>();
        public List<KV> upgrades = new List<KV>();
        public List<StaffSave> staff = new List<StaffSave>();
        public double finalIncome;
    }

    [Serializable]
    public class LevelSave
    {
        public int location;
        public int level;
        public List<float> progress = new List<float>();
        public List<bool> done = new List<bool>();
    }

    [Serializable]
    public class Stats
    {
        public long served;
        public long chai;
        public double earned;
        public long upgradesBought;
    }

    [Serializable]
    public class SaveData
    {
        public int v = 1;
        public double cash = 25;     // a new game: exactly the first kettle (15) and the chai counter (10)
        public int leaves;
        public long lastSeen;
        public int location = 1;
        public int maxLocation = 1;
        public double playSeconds;
        public bool tutorialDone;
        public bool leafHintShown;
        public bool upgradesSeen;
        public float luckyTimer;
        public List<KV> perks = new List<KV>();
        public List<KV> global = new List<KV>();
        public List<LocSave> locations = new List<LocSave>();
        public LevelSave levels = new LevelSave();
        public List<string> tipsShown = new List<string>();
        public List<string> cosmeticsOwned = new List<string>();
        public string wornCap = "";
        public string wornKurta = "";
        public string wornApron = "";
        public List<string> decorOn = new List<string>();
        public Stats stats = new Stats();
        public bool music = true;
        public bool sfx = true;

        public LocSave Loc(int id)
        {
            foreach (var l in locations) if (l.id == id) return l;
            var n = new LocSave { id = id };
            locations.Add(n);
            return n;
        }
    }

    /// <summary>
    /// One JSON save under cdt_save_v1 with a backup copy under cdt_save_bak. Every storage call is
    /// wrapped so the game still runs in incognito or with storage blocked.
    /// </summary>
    public static class SaveSystem
    {
        public const string Key = "cdt_save_v1";
        public const string BackupKey = "cdt_save_bak";
        public static bool Disabled; // set when storage throws; we keep playing silently
        static bool startedBlank;    // this run found no save to load

        public static SaveData Load()
        {
            string main = WebBridge.StorageGet(Key), backup = WebBridge.StorageGet(BackupKey);
            Debug.Log($"[Save] load: main {(main == null ? "null" : main.Length.ToString())}, backup {(backup == null ? "null" : backup.Length.ToString())}");
            var s = TryParse(main);
            if (s == null) s = TryParse(backup);
            startedBlank = s == null;
            if (s == null) s = new SaveData();
            Migrate(s);
            return s;
        }

        static SaveData TryParse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var s = JsonUtility.FromJson<SaveData>(json);
                if (s == null || s.v <= 0) return null;
                return s;
            }
            catch (Exception e) { Debug.LogWarning("[Save] unreadable save: " + e.Message); return null; }
        }

        static void Migrate(SaveData s)
        {
            // v1 is the only version so far; future versions migrate step by step here.
            if (s.perks == null) s.perks = new List<KV>();
            if (s.global == null) s.global = new List<KV>();
            if (s.locations == null) s.locations = new List<LocSave>();
            if (s.levels == null) s.levels = new LevelSave();
            if (s.levels.progress == null) s.levels.progress = new List<float>();
            if (s.levels.done == null) s.levels.done = new List<bool>();
            if (s.tipsShown == null) s.tipsShown = new List<string>();
            if (s.cosmeticsOwned == null) s.cosmeticsOwned = new List<string>();
            if (s.decorOn == null) s.decorOn = new List<string>();
            if (s.stats == null) s.stats = new Stats();
            if (s.location <= 0) s.location = 1;
            if (s.maxLocation < s.location) s.maxLocation = s.location;
        }

        public static void Save(SaveData s)
        {
            if (s == null) return;
            try
            {
                s.lastSeen = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string json = JsonUtility.ToJson(s);
                string prev = WebBridge.StorageGet(Key);
                // a run that found no save must never write over a save that turns up later with more play on it
                if (startedBlank)
                {
                    var stored = TryParse(prev);
                    if (stored != null && stored.playSeconds > s.playSeconds)
                    {
                        Debug.LogWarning("[Save] kept the stored save: this run started blank");
                        return;
                    }
                    startedBlank = false;
                }
                if (!string.IsNullOrEmpty(prev)) WebBridge.StorageSet(BackupKey, prev);
                WebBridge.StorageSet(Key, json);
            }
            catch (Exception)
            {
                Disabled = true;
            }
        }

        public static void Wipe()
        {
            WebBridge.StorageSet(Key, "");
            WebBridge.StorageSet(BackupKey, "");
        }
    }
}
