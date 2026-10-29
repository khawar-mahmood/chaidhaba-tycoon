using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Upgrade levels and every derived stat. cost(n) = base * growth^n * location multiplier.
    /// Rafi and Franchise upgrades are kept forever; the rest belong to the current location.
    /// </summary>
    public class Upgrades
    {
        readonly Game g;
        public Upgrades(Game g) { this.g = g; }

        Economy Eco => g.Eco;
        SaveData S => g.S;
        LocSave LS => g.LS;

        public static string Key(string id, string sub) => string.IsNullOrEmpty(sub) ? id : id + ":" + sub;

        public int Level(string id, string sub = null)
        {
            var u = Eco.Upgrade(id);
            if (u != null && u.global) return S.global.Get(Key(id, sub));
            return LS.upgrades.Get(Key(id, sub));
        }

        public int Perk(string id) => S.perks.Get(id);

        public double Cost(UpgradeCfg u, int level) => Math.Round(u.baseCost * Math.Pow(u.growth, level) * g.Multiplier);

        public bool IsMax(UpgradeCfg u, string sub = null) => Level(u.id, sub) >= u.max;

        public double NextCost(UpgradeCfg u, string sub = null) => Cost(u, Level(u.id, sub));

        public bool CanAfford(UpgradeCfg u, string sub = null) => !IsMax(u, sub) && g.S.cash >= NextCost(u, sub);

        public bool Buy(UpgradeCfg u, string sub = null, bool free = false)
        {
            if (IsMax(u, sub)) return false;
            double cost = NextCost(u, sub);
            if (!free && !g.Spend(cost)) return false;
            int lvl = Level(u.id, sub) + 1;
            if (u.global) S.global.Set(Key(u.id, sub), lvl);
            else LS.upgrades.Set(Key(u.id, sub), lvl);
            S.stats.upgradesBought++;
            GameEvents.UpgradeBought?.Invoke(u.id);
            g.OnStatsChanged();
            g.RequestSave();
            return true;
        }

        // ---------- staff ----------
        public int StaffLevel(StaffSave s, string id) => id == "staffSpeed" ? s.speed : id == "staffCarry" ? s.carry : 0;

        public bool BuyStaff(StaffSave s, UpgradeCfg u, bool free = false)
        {
            int lvl = StaffLevel(s, u.id);
            if (lvl >= u.max) return false;
            if (!free && !g.Spend(Cost(u, lvl))) return false;
            if (u.id == "staffSpeed") s.speed++;
            else if (u.id == "staffCarry") s.carry++;
            S.stats.upgradesBought++;
            GameEvents.UpgradeBought?.Invoke(u.id);
            g.OnStatsChanged();
            g.RequestSave();
            return true;
        }

        float PL(string id, float def) { var u = Eco.Upgrade(id); return u != null ? u.perLevel : def; }

        // ---------- Rafi ----------
        public float WalkSpeed => Eco.player.walkSpeed + Level("walk") * PL("walk", 0.4f);
        public int Carry => Eco.player.carry + Level("carry") * Mathf.RoundToInt(PL("carry", 1));
        public float PickInterval => Eco.player.pickInterval * Mathf.Pow(PL("quickHands", 0.8f), Level("quickHands"));
        public float MagnetRadius => Eco.player.magnetRadius + Level("magnet") * PL("magnet", 0.5f);
        public float TileFillTime => Eco.player.tileFillTime * Mathf.Pow(PL("fastPay", 0.75f), Level("fastPay"));

        // ---------- Counter ----------
        public float ServeInterval => Eco.player.serveInterval * Mathf.Pow(PL("serveSpeed", 0.82f), Level("serveSpeed"));
        public float PatienceMult => (1 + Level("comfort") * PL("comfort", 0.15f)) * (1 + Perk("loyalCustomers") * PerkPL("loyalCustomers", 0.1f));
        public float TipMult => 1 + Level("tipJar") * PL("tipJar", 0.1f);
        public int QueueMax => Eco.rules.queueBase + Level("queue") * Mathf.RoundToInt(PL("queue", 2));

        // ---------- Stations ----------
        public float PrepTime(ItemId id)
        {
            var it = Eco.Item(id);
            float t = it != null ? it.prep : 2f;
            return t * Mathf.Pow(PL("prep", 0.88f), Level("prep", id.ToString())) * Mathf.Pow(1 - PerkPL("expressKitchen", 0.08f), Perk("expressKitchen"));
        }

        public int TrayCap(ItemId id) => Eco.rules.trayBase + Level("tray", id.ToString()) * Mathf.RoundToInt(PL("tray", 2));

        public int Batch(ItemId id)
        {
            var it = Eco.Item(id);
            int b = it != null ? it.batch : 1;
            return Mathf.RoundToInt(b * (1 + Level("batch", id.ToString()) * PL("batch", 0.25f)));
        }

        public double Price(ItemId id)
        {
            var it = Eco.Item(id);
            double p = it != null ? it.price : 5;
            return p * g.Multiplier * (1 + Level("price", id.ToString()) * PL("price", 0.15f)) * (1 + Perk("familyRecipe") * PerkPL("familyRecipe", 0.1f));
        }

        // ---------- Dhaba ----------
        public float ArrivalMult => 1 + Level("advertising") * PL("advertising", 0.08f);
        public float EatSpeedMult => 1 + Level("comfySeating") * PL("comfySeating", 0.15f);

        // ---------- Staff ----------
        public float StaffSpeed(StaffSave s) => 2.5f * (1 + s.speed * PL("staffSpeed", 0.12f));     // a calm walk, well under Rafi's 4
        public int StaffCarry(StaffSave s) => 2 + s.carry;
        public float CashierInterval(StaffSave s) => 0.9f / (1 + s.speed * PL("staffSpeed", 0.12f));

        float PerkPL(string id, float def) { var p = Eco.Perk(id); return p != null ? p.perLevel : def; }

        /// <summary>Upgrade cards visible in a tab right now (tabs appear only when useful).</summary>
        public List<(UpgradeCfg u, string sub)> Cards(string tab)
        {
            var list = new List<(UpgradeCfg, string)>();
            if (tab == "stations")
            {
                foreach (var item in g.Loc.UnlockedItems())
                {
                    foreach (var u in Eco.upgrades)
                    {
                        if (u.tab != "stations") continue;
                        if (u.items != null && u.items.Length > 0 && Array.IndexOf(u.items, item.ToString().ToLowerInvariant()) < 0) continue;
                        list.Add((u, item.ToString()));
                    }
                }
                return list;
            }
            foreach (var u in Eco.upgrades)
            {
                if (u.tab != tab) continue;
                if (!string.IsNullOrEmpty(u.needs) && !g.Loc.Has(u.needs)) continue;
                list.Add((u, null));
            }
            return list;
        }

        public bool TabVisible(string tab)
        {
            switch (tab)
            {
                case "rafi": return S.upgradesSeen;
                case "counter": return g.Loc.Has("lane2");
                case "stations": return g.Loc.Has("fryer1") || g.S.location > 1;
                case "dhaba": return g.S.location >= 2;
                default: return false;
            }
        }

        public bool AnyAffordable(string tab)
        {
            if (!TabVisible(tab)) return false;
            foreach (var (u, sub) in Cards(tab)) if (CanAfford(u, sub)) return true;
            return false;
        }

        public bool AnyAffordable()
        {
            foreach (var t in new[] { "rafi", "counter", "stations", "dhaba" }) if (AnyAffordable(t)) return true;
            return false;
        }

        /// <summary>Human readable "current → next" for a card.</summary>
        public string EffectText(UpgradeCfg u, string sub)
        {
            int l = Level(u.id, sub);
            bool max = l >= u.max;
            string Arrow(string a, string b) => max ? a : $"{a} → {b}";
            switch (u.id)
            {
                case "walk": return Arrow($"{WalkAt(l):0.0} m/s", $"{WalkAt(l + 1):0.0} m/s");
                case "carry": return Arrow($"{Eco.player.carry + l}", $"{Eco.player.carry + l + 1}") + " items";
                case "quickHands": return Arrow($"{Eco.player.pickInterval * Mathf.Pow(u.perLevel, l):0.00}s", $"{Eco.player.pickInterval * Mathf.Pow(u.perLevel, l + 1):0.00}s");
                case "magnet": return Arrow($"{Eco.player.magnetRadius + l * u.perLevel:0.0} m", $"{Eco.player.magnetRadius + (l + 1) * u.perLevel:0.0} m");
                case "fastPay": return Arrow($"{Mathf.Round(100 * Mathf.Pow(u.perLevel, l))}%", $"{Mathf.Round(100 * Mathf.Pow(u.perLevel, l + 1))}%") + " fill time";
                case "serveSpeed": return Arrow($"{Eco.player.serveInterval * Mathf.Pow(u.perLevel, l):0.00}s", $"{Eco.player.serveInterval * Mathf.Pow(u.perLevel, l + 1):0.00}s");
                case "comfort": return Arrow($"+{l * 15}%", $"+{(l + 1) * 15}%") + " patience";
                case "tipJar": return Arrow($"+{l * 10}%", $"+{(l + 1) * 10}%") + " tips";
                case "queue": return Arrow($"{Eco.rules.queueBase + l * 2}", $"{Eco.rules.queueBase + (l + 1) * 2}") + " spots";
                case "advertising": return Arrow($"+{l * 8}%", $"+{(l + 1) * 8}%") + " arrivals";
                case "comfySeating": return Arrow($"+{l * 15}%", $"+{(l + 1) * 15}%") + " eat speed";
                case "franchise": return Arrow($"{10 + l * 2}%", $"{10 + (l + 1) * 2}%");
            }
            if (sub != null)
            {
                var id = Items.Parse(sub);
                var it = Eco.Item(id);
                switch (u.id)
                {
                    case "prep": return Arrow($"{it.prep * Mathf.Pow(u.perLevel, l):0.0}s", $"{it.prep * Mathf.Pow(u.perLevel, l + 1):0.0}s");
                    case "tray": return Arrow($"{Eco.rules.trayBase + l * 2}", $"{Eco.rules.trayBase + (l + 1) * 2}") + " items";
                    case "price":
                    {
                        double baseP = it.price * g.Multiplier * (1 + Perk("familyRecipe") * 0.1f);
                        return Arrow(Fmt.Cash(baseP * (1 + l * u.perLevel)), Fmt.Cash(baseP * (1 + (l + 1) * u.perLevel)));
                    }
                    case "batch": return Arrow($"{Mathf.RoundToInt(it.batch * (1 + l * u.perLevel))}", $"{Mathf.RoundToInt(it.batch * (1 + (l + 1) * u.perLevel))}") + " per batch";
                }
            }
            return "";
        }

        float WalkAt(int l) => Eco.player.walkSpeed + l * PL("walk", 0.4f);
    }
}
