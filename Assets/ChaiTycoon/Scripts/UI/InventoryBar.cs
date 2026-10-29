using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>
    /// Bottom-left HUD card showing what Rafi is carrying, as in the mockup: a stack icon with the count (2/4),
    /// four dashed slots and a bar under them ("Empty hands", filling green, "Hands full"). Up to four items
    /// get a slot each; a bigger stack groups the slots by item with a count.
    /// </summary>
    public class InventoryBar
    {
        const int MaxSlots = 4;
        const float BarX = 93, BarW = 252, BarY = 69, BarH = 30;
        RectTransform box;
        Text countText;
        Image stackIcon, barBack, barFill, barEmpty, barFull;
        readonly List<Slot> slots = new List<Slot>();
        string sig;

        class Slot
        {
            public RectTransform rt;
            public Image icon;
            public Text n;
        }

        public RectTransform Rect => box;

        public static InventoryBar Create(RectTransform root)
        {
            var b = new InventoryBar();
            var panel = Skin.Img(root, "inv_bg", SpriteGen.Panel(18, 3), Pal.Cream);
            b.box = panel.rectTransform;
            UIKit.Place(b.box, Vector2.zero, Vector2.zero, new Vector2(23, 39), new Vector2(363, 107));

            // stack icon on the painted circle, the count under it
            b.stackIcon = Skin.Img(b.box, "inv_stack", SpriteGen.Icon("stack"));     // always the stack icon
            b.stackIcon.preserveAspect = true;
            UIKit.Place(b.stackIcon.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(49, -34), new Vector2(30, 40));
            b.countText = UIKit.Text(b.box, "0/4", 30, Skin.Ink);
            UIKit.Place(b.countText.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(49, -78), new Vector2(84, 34));

            for (int i = 0; i < MaxSlots; i++)
            {
                var bg = Skin.Img(b.box, "inv_slot", SpriteGen.Rounded(12), Pal.Mul(Pal.Cream, 0.93f));
                UIKit.Place(bg.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(126 + i * 63, -33), new Vector2(52, 52));
                var icon = UIKit.Image(bg.transform, null, Color.white, "icon");
                icon.preserveAspect = true;
                UIKit.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42, 42));
                var n = UIKit.OutlinedText(bg.transform, "", 20, Color.white, TextAnchor.LowerRight, 1.6f);
                UIKit.Place(n.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(6, -6), new Vector2(50, 30));
                b.slots.Add(new Slot { rt = bg.rectTransform, icon = icon, n = n });
            }

            // the bar: painted "Empty hands" / "Hands full", or a grey bar filling green in between
            b.barBack = Bar(b.box, "inv_bar_grey", new Color(0.4f, 0.4f, 0.39f));
            b.barFill = Bar(b.box, "inv_bar_green", Pal.Parrot);
            b.barEmpty = Bar(b.box, "inv_empty", new Color(0.4f, 0.4f, 0.39f));
            b.barFull = Bar(b.box, "inv_full", Pal.Parrot);
            if (!Skin.On)
            {
                var t = UIKit.Text(b.barEmpty.transform, "Empty hands", 18, Color.white);
                UIKit.Stretch(t.rectTransform, 0);
                t = UIKit.Text(b.barFull.transform, "Hands full", 18, Color.white);
                UIKit.Stretch(t.rectTransform, 0);
            }
            return b;
        }

        static Image Bar(RectTransform box, string name, Color fallback)
        {
            var img = Skin.Img(box, name, SpriteGen.Rounded(14), fallback);
            UIKit.TopLeft(img.rectTransform, BarX, BarY, BarW, BarH);
            return img;
        }

        public void Refresh(CarryStack stack)
        {
            if (stack == null || box == null) return;
            var sb = new StringBuilder();
            sb.Append(stack.capacity).Append(':');
            foreach (var it in stack.items) sb.Append((int)it.id).Append(',');
            var s = sb.ToString();
            if (s == sig) return;
            bool grew = sig != null && s.Length > sig.Length;
            sig = s;

            // one slot per item while they fit, else grouped by item (in the order they were picked up)
            var order = new List<ItemId>();
            var counts = new Dictionary<ItemId, int>();
            foreach (var it in stack.items)
            {
                if (!counts.ContainsKey(it.id)) { counts[it.id] = 0; order.Add(it.id); }
                counts[it.id]++;
            }
            bool each = stack.items.Count <= MaxSlots;
            for (int i = 0; i < slots.Count; i++)
            {
                var sl = slots[i];
                ItemId? id = null;
                int n = 0;
                if (each && i < stack.items.Count) { id = stack.items[i].id; n = 1; }
                else if (!each && i < order.Count) { id = order[i]; n = counts[order[i]]; }
                sl.icon.enabled = id.HasValue;
                if (id.HasValue) sl.icon.sprite = IconBaker.Item(id.Value);
                sl.n.text = n > 1 ? "x" + n : "";
            }

            bool full = stack.IsFull;
            int count = stack.Count;
            countText.text = count + "/" + stack.capacity;
            barEmpty.gameObject.SetActive(count == 0);
            barFull.gameObject.SetActive(full);
            barBack.gameObject.SetActive(count > 0 && !full);
            barFill.gameObject.SetActive(count > 0 && !full);
            if (count > 0 && !full)
                barFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(BarH, BarW * count / Mathf.Max(1f, stack.capacity)), BarH);

            // a little bump so pick-ups and hand-overs are felt in the HUD too
            var t = box;
            float amp = grew ? 0.08f : 0.04f;
            Tw.Run(0.22f, k => { if (t) t.localScale = Vector3.one * (1 + Mathf.Sin(k * Mathf.PI) * amp); }, () => { if (t) t.localScale = Vector3.one; }, true);
        }
    }
}
