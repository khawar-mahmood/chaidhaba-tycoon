using System;
using UnityEngine;

namespace ChaiTycoon
{
    public enum ItemId
    {
        Chai, Paratha, Samosa, Lassi, Yogurt, Daal, Roti, Pakora, Karahi, Biryani, RiceSack,
        Seekh, Kulfi, CornSoup, Chapli, DirtyDish
    }

    public static class Items
    {
        public static ItemId Parse(string s)
        {
            if (Enum.TryParse<ItemId>(s, true, out var id)) return id;
            Debug.LogWarning("Unknown item " + s);
            return ItemId.Chai;
        }

        /// <summary>Stacking height of one item in a carry stack or tray.</summary>
        public static float Height(ItemId id)
        {
            switch (id)
            {
                case ItemId.Chai: return 0.24f;
                case ItemId.Paratha: return 0.11f;
                case ItemId.Samosa: return 0.2f;
                case ItemId.Lassi: return 0.32f;
                case ItemId.DirtyDish: return 0.09f;
                case ItemId.RiceSack: return 0.4f;
                default: return 0.2f;
            }
        }

        /// <summary>Short everyday name for guide text ("chai", not "Doodh patti chai").</summary>
        public static string Short(ItemId id)
        {
            switch (id)
            {
                case ItemId.Chai: return "chai";
                case ItemId.Daal: return "daal";
                case ItemId.Karahi: return "karahi";
                case ItemId.Seekh: return "seekh kebab";
                case ItemId.CornSoup: return "corn soup";
                case ItemId.Chapli: return "chapli kebab";
                default: return id.ToString().ToLowerInvariant();
            }
        }

        public static string Name(ItemId id)
        {
            var c = Game.I != null ? Game.I.Eco.Item(id) : null;
            return c != null ? c.name : id.ToString();
        }
    }

    /// <summary>A carryable food object in the world (pooled).</summary>
    public class ItemObj : MonoBehaviour
    {
        public ItemId id;
        public Coroutine hop;
    }
}
