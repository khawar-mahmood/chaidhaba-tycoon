using System;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>Mirrors Resources/economy.json. Every price, cost, growth rate and timer lives there.</summary>
    [Serializable]
    public class Economy
    {
        public int version;
        public PlayerCfg player;
        public RulesCfg rules;
        public ItemCfg[] items;
        public CustomerCfg[] customers;
        public UpgradeCfg[] upgrades;
        public LocationCfg[] locations;
        public PerkCfg[] perks;
        public CosmeticCfg[] cosmetics;
        public OfferCfg[] offers;

        public static Economy Load()
        {
            var ta = Resources.Load<TextAsset>("economy");
            if (ta == null) { Debug.LogError("economy.json missing"); return new Economy(); }
            return JsonUtility.FromJson<Economy>(ta.text);
        }

        public ItemCfg Item(ItemId id)
        {
            string s = id.ToString();
            foreach (var i in items) if (string.Equals(i.id, s, StringComparison.OrdinalIgnoreCase)) return i;
            return null;
        }

        public CustomerCfg Customer(string id)
        {
            foreach (var c in customers) if (c.id == id) return c;
            return null;
        }

        public UpgradeCfg Upgrade(string id)
        {
            foreach (var u in upgrades) if (u.id == id) return u;
            return null;
        }

        public LocationCfg Location(int id)
        {
            foreach (var l in locations) if (l.id == id) return l;
            return null;
        }

        public PerkCfg Perk(string id)
        {
            foreach (var p in perks) if (p.id == id) return p;
            return null;
        }

        public CosmeticCfg Cosmetic(string id)
        {
            foreach (var c in cosmetics) if (c.id == id) return c;
            return null;
        }

        public OfferCfg Offer(string id)
        {
            foreach (var o in offers) if (o.id == id) return o;
            return null;
        }
    }

    [Serializable]
    public class PlayerCfg
    {
        public float walkSpeed = 4f;
        public int carry = 4;
        public float pickInterval = 0.15f;
        public float serveInterval = 0.15f;
        public float magnetRadius = 1f;
        public float tileFillTime = 1.2f;
    }

    [Serializable]
    public class RulesCfg
    {
        public float fastTipThreshold = 0.5f;
        public float fastTipBonus = 0.2f;
        public int queueBase = 6;
        public int trayBase = 4;
        public float luckyTipInterval = 300f;
        public float boostDuration = 120f;
        public float boostMultiplier = 2f;
        public float boostCostSeconds = 60f;
        public float offerFirstAt = 150f;
        public float offerCooldown = 180f;
        public float offerRetryAfterFail = 60f;
        public float midgameFirstAt = 300f;
        public float autosave = 10f;
        public float oldLocationShare = 0.1f;
        public float missionsAppearAt = 120f;
        public float fastServeSeconds = 10f;
        public float maxCustomersOnScreen = 30f;
        // a cook per station keeps it cooking on its own; extra helpers (waiters) carry and serve
        public float cookCost = 90f;
        public float cookCostGrowth = 1.6f;
        public float helperCost = 260f;
        public float helperCostGrowth = 1.9f;
        public int maxHelpers = 3;
    }

    [Serializable]
    public class ItemCfg
    {
        public string id;
        public string name;
        public string station;
        public float prep;
        public float price;
        public int batch = 1;
        public bool cheap;
    }

    [Serializable]
    public class CustomerCfg
    {
        public string id;
        public string name;
        public float patience;
        public float pay = 1f;
        public float tip;
        public int minItems = 1;
        public int maxItems = 1;
        public bool cheapOnly;
        public float weight = 1f;
        public string needs; // unlock id that enables this type, empty = always
    }

    [Serializable]
    public class UpgradeCfg
    {
        public string id;
        public string name;
        public string tab; // rafi, counter, stations, staff, dhaba, franchise
        public string icon;
        public string effect;
        public int max;
        public float baseCost;
        public float growth;
        public float perLevel;
        public bool perItem;
        public string[] items; // limit a per-item upgrade to these items (batch size)
        public string[] roles; // limit a staff upgrade to these roles
        public string needs; // unlock id that reveals this card
        public bool global; // kept forever across locations
    }

    [Serializable]
    public class LocationCfg
    {
        public int id;
        public string name;
        public string shortName;
        public float multiplier = 1f;
        public float arrivalGift;
        public float[] spawnByStar;
        public string[] menu;
        public UnlockCfg[] unlocks;
        public LevelCfg[] levels;
        public bool playable;
    }

    /// <summary>A level is a small set of tasks; finishing them all levels you up.</summary>
    [Serializable]
    public class LevelCfg
    {
        public string title;
        public bool sequential; // tasks one at a time (used for the first-time levels)
        public float reward;
        public int leaves;
        public string cosmetic;
        public TaskCfg[] tasks;
    }

    [Serializable]
    public class TaskCfg
    {
        // pick, serve, serveCustomers, serveType, fast, collect, earn, build, hire, upgrade, expand
        public string type;
        public string target;
        public int count = 1;
        public float reward;
        public string text;
        /// <summary>Optional longer line for the hint bar while this task is the one to do (what it is and why).</summary>
        public string desc;
        public string icon;
    }

    [Serializable]
    public class UnlockCfg
    {
        public string id;
        public string name;
        public string kind; // station, lane, stand, staff, decor, expand
        public string arg;
        public string icon;
        public float cost;
        public int stars;
    }

    [Serializable]
    public class PerkCfg
    {
        public string id;
        public string name;
        public string effect;
        public string icon;
        public int[] costs;
        public float perLevel;
        public int stage = 1;
    }

    [Serializable]
    public class CosmeticCfg
    {
        public string id;
        public string name;
        public string slot; // cap, kurta, apron, decor
        public string color;
        public int price; // in tea leaves; 0 = mission reward only
        public string icon;
    }

    [Serializable]
    public class OfferCfg
    {
        public string id;
        public string name;
        public float cooldown;
        public float value;
        public string icon;
    }
}
