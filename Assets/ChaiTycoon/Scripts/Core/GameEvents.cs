using System;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>Gameplay events that missions, stats and UI listen to.</summary>
    public static class GameEvents
    {
        public static Action<ItemId> ItemServed;
        public static Action<Customer> CustomerServed;
        public static Action<double> CashEarned;
        public static Action<string> Hired;
        public static Action<string> UpgradeBought;
        public static Action<string> Unlocked;
        public static Action<int> StarGained;
        public static Action Expanded;
        public static Action<ItemId> ItemMade;
        public static Action CashChanged;
        public static Action LeavesChanged;
        public static Action<ItemId> ItemPicked;      // the player picked an item up from a station
        public static Action<double> CashCollected;   // the player walked over a cash pile
        public static Action<Customer> CustomerSeated;
        public static Action<Customer> RickshawParked;
        public static Action<int> LevelUp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetAll()
        {
            ItemServed = null; CustomerServed = null; CashEarned = null; Hired = null; UpgradeBought = null;
            Unlocked = null; StarGained = null; Expanded = null; ItemMade = null; CashChanged = null; LeavesChanged = null;
            ItemPicked = null; CashCollected = null; CustomerSeated = null; RickshawParked = null; LevelUp = null;
        }
    }
}
