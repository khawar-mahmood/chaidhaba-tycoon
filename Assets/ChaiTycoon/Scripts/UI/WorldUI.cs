using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>
    /// Screen-space overlay for things that live in the world (bubbles, tray counters, floating
    /// numbers). They always face the camera and keep the same screen size at any zoom.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class WorldUI : MonoBehaviour
    {
        Canvas canvas;
        RectTransform root;
        readonly List<Widget> widgets = new List<Widget>();
        public RectTransform Root => root;

        public static WorldUI Create()
        {
            var c = UIKit.Canvas("WorldUI", 5);
            // clickable only where a widget has a button (hire cook); everything else ignores clicks
            c.GetComponent<GraphicRaycaster>().enabled = true;
            var w = c.gameObject.AddComponent<WorldUI>();
            w.canvas = c;
            w.root = UIKit.Rect(c.transform, "root");
            UIKit.Stretch(w.root, 0);
            return w;
        }

        public Vector2 ToCanvas(Vector3 world, out bool visible)
        {
            var cam = Camera.main;
            var sp = cam.WorldToScreenPoint(world);
            visible = sp.z > 0;
            return new Vector2(sp.x, sp.y) / canvas.scaleFactor;
        }

        public Vector2 ScreenToCanvas(Vector2 sp) => sp / canvas.scaleFactor;
        public Vector2 CanvasSize => root.rect.size;
        /// <summary>Height of the HUD strip along the top of the screen that world widgets stay below.</summary>
        const float TopMargin = 124f;   // the star bar, pause, settings and the next-unlock chip under the stars

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = widgets.Count - 1; i >= 0; i--)
            {
                var w = widgets[i];
                if (!w.alive || w.rt == null) { widgets.RemoveAt(i); continue; }
                if (w.anchor != null)
                {
                    Vector3 wp;
                    try { wp = w.anchor(); }
                    catch (Exception) { w.Remove(); continue; }
                    var p = ToCanvas(wp, out bool vis) + w.offset;
                    // never under the top HUD strip (stars, pause, settings): push the widget down below it
                    float top = p.y + w.rt.sizeDelta.y * (1 - w.rt.pivot.y);
                    float limit = CanvasSize.y - TopMargin;
                    if (top > limit) p.y -= top - limit;
                    w.rt.anchoredPosition = p;
                    if (w.rt.gameObject.activeSelf != (vis && w.Visible)) w.rt.gameObject.SetActive(vis && w.Visible);
                }
                w.Tick(dt);
            }
        }

        T Add<T>(T w) where T : Widget
        {
            widgets.Add(w);
            return w;
        }

        RectTransform NewRoot(string name, Vector2 size)
        {
            var rt = UIKit.Rect(root, name);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            return rt;
        }

        // ------------------------------------------------------------------ widgets

        public abstract class Widget
        {
            public RectTransform rt;
            public Func<Vector3> anchor;
            public Vector2 offset;
            public bool alive = true;
            public bool Visible = true;
            public virtual void Tick(float dt) { }
            public void Remove()
            {
                if (!alive) return;
                alive = false;
                if (rt) UnityEngine.Object.Destroy(rt.gameObject);
            }
        }

        public class OrderBubble : Widget
        {
            Customer c;
            WorldUI ui;
            CanvasGroup group;
            Image ring, ringBg, bg;
            readonly List<(ItemId id, Image icon, Text count, Image check, int need)> entries = new List<(ItemId, Image, Text, Image, int)>();
            float shake;

            public OrderBubble(WorldUI ui, Customer cust)
            {
                c = cust;
                this.ui = ui;
                var counts = new List<(ItemId, int)>();
                foreach (var id in cust.Order)
                {
                    int idx = counts.FindIndex(e => e.Item1 == id);
                    if (idx >= 0) counts[idx] = (id, counts[idx].Item2 + 1);
                    else counts.Add((id, 1));
                }
                int n = Mathf.Min(3, counts.Count);
                // compact: smaller than the other bubbles so it hides less of the counter and whoever stands behind it
                float w = 16 + n * 44;
                rt = ui.NewRoot("bubble", new Vector2(w, 54));
                group = rt.gameObject.AddComponent<CanvasGroup>();
                bg = BubbleBody(rt);
                BubbleTail(rt, 0.9f);
                for (int i = 0; i < n; i++)
                {
                    var (id, cnt) = counts[i];
                    var ic = UIKit.Image(rt, IconBaker.Item(id), Color.white, "icon");
                    UIKit.Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10 + i * 44, 1), new Vector2(42, 42));
                    ic.preserveAspect = true;
                    var t = UIKit.OutlinedText(ic.transform, cnt > 1 ? "x" + cnt : "", 16, Color.white, TextAnchor.LowerRight, 1.5f);
                    UIKit.Stretch(t.rectTransform, 0);
                    t.rectTransform.offsetMax = new Vector2(4, 0);
                    var chk = UIKit.Image(ic.transform, SpriteGen.Icon("check"), Color.white, "check");
                    UIKit.Place(chk.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(32, 32));
                    chk.enabled = false;
                    entries.Add((id, ic, t, chk, cnt));
                }
                // patience ring badge on the top-left corner
                ringBg = PatienceRing(rt, new Vector2(0, 1), new Vector2(3, -3), 28, out ring);
                anchor = () => c.BubbleAnchor;
                offset = new Vector2(0, 18);
                Tw.PopIn(rt, 0.25f, true);
            }

            public void Served(ItemId id)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e.id != id) continue;
                    int left = 0;
                    foreach (var r in c.Remaining) if (r == id) left++;
                    e.count.text = left > 1 ? "x" + left : "";
                    if (left == 0)
                    {
                        e.check.enabled = true;
                        e.icon.color = new Color(1, 1, 1, 0.55f);
                        Tw.PopIn(e.check.transform, 0.25f, true);
                    }
                    Tw.Punch(e.icon.transform, 0.25f, 0.2f, true);
                    break;
                }
            }

            public override void Tick(float dt)
            {
                if (c == null) { Remove(); return; }
                // see-through while Rafi or a helper is behind the bubble, so they never disappear behind it
                group.alpha = Mathf.MoveTowards(group.alpha, CoversAgent(ui, rt, c.transform.position) ? 0.28f : 1f, dt * 6f);
                float p = c.Patience01;
                ring.fillAmount = p;
                ring.color = p > 0.5f ? Color.Lerp(Pal.PatienceYellow, Pal.PatienceGreen, (p - 0.5f) * 2f) : Color.Lerp(Pal.PatienceRed, Pal.PatienceYellow, p * 2f);
                float secondsLeft = c.Patience;
                if (secondsLeft < 3f && !Customer.FreezePatience)
                {
                    shake += dt * 40f;
                    rt.anchoredPosition += new Vector2(Mathf.Sin(shake) * 3f, 0);
                }
            }
        }

        // ---- the painted order bubble (UI/Skin.cs); cream shapes when the skin is missing ----

        internal static Image BubbleBody(RectTransform rt)
        {
            var bg = Skin.Img(rt, "bubble_body", SpriteGen.Panel(22, 3), Pal.Cream);
            UIKit.Stretch(bg.rectTransform, 0);
            return bg;
        }

        internal static void BubbleTail(RectTransform rt, float scale)
        {
            if (Skin.Get("bubble_tail") != null)
            {
                // the tail's top rows are the body's bottom edge with the gap, so it's drawn over the body
                var tail = Skin.Img(rt, "bubble_tail");
                var size = Skin.Size("bubble_tail") * scale;
                UIKit.Place(tail.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0.7f * scale, 5.4f * scale), size);
                return;
            }
            var t = UIKit.Image(rt, SpriteGen.Icon("arrow_down"), Pal.Cream, "tail");
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, 6), new Vector2(20 * scale, 16 * scale));
            t.transform.SetAsFirstSibling();
        }

        /// <summary>The round patience badge: a white disc with a ring that empties (tinted green to red).</summary>
        internal static Image PatienceRing(RectTransform parent, Vector2 anchor, Vector2 pos, float size, out Image ring)
        {
            var bg = Skin.Img(parent, "ring_bg", SpriteGen.Circle(64, 3), Pal.Cream);
            UIKit.Place(bg.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var white = Skin.Get("ring_white");
            ring = UIKit.Image(bg.transform, white != null ? white : SpriteGen.RingSprite(64, 12), Pal.PatienceGreen, "ring");
            UIKit.Stretch(ring.rectTransform, white != null ? size * 0.12f : 3);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = false;
            return bg;
        }

        /// <summary>Whether a canvas-space box (pivot at the bottom centre) hides Rafi or a helper standing farther away.</summary>
        internal static bool CoversAgent(WorldUI ui, RectTransform rt, Vector3 ownerPos)
        {
            var g = Game.I;
            if (g == null || g.Player == null) return false;
            var cam = Camera.main;
            if (!cam) return false;
            var pos = rt.anchoredPosition; var size = rt.sizeDelta;
            var box = new Rect(pos.x - size.x * 0.5f - 12, pos.y - 30, size.x + 24, size.y + 40);
            float ownerDepth = cam.WorldToScreenPoint(ownerPos).z;
            bool Hidden(Agent a)
            {
                if (a == null) return false;
                var p = a.transform.position;
                // only agents behind the customer (farther from the camera) get covered
                if (cam.WorldToScreenPoint(p).z < ownerDepth - 0.2f) return false;
                var body = ui.ToCanvas(p + Vector3.up * 0.9f, out bool v1);
                var head = ui.ToCanvas(p + Vector3.up * 1.7f, out bool v2);
                return (v1 && box.Contains(body)) || (v2 && box.Contains(head));
            }
            if (Hidden(g.Player)) return true;
            if (g.Loc != null) foreach (var s in g.Loc.StaffList) if (Hidden(s)) return true;
            return false;
        }

        public class MiniRing : Widget
        {
            Customer c;
            Image ring;
            public MiniRing(WorldUI ui, Customer cust)
            {
                c = cust;
                rt = ui.NewRoot("mini", new Vector2(24, 24));
                PatienceRing(rt, new Vector2(0.5f, 0.5f), Vector2.zero, 24, out ring);
                anchor = () => c.BubbleAnchor;
                offset = new Vector2(0, 8);
            }

            public override void Tick(float dt)
            {
                if (c == null) { Remove(); return; }
                float p = c.Patience01;
                ring.fillAmount = p;
                ring.color = p > 0.5f ? Color.Lerp(Pal.PatienceYellow, Pal.PatienceGreen, (p - 0.5f) * 2f) : Color.Lerp(Pal.PatienceRed, Pal.PatienceYellow, p * 2f);
            }
        }

        public class StationTag : Widget
        {
            Station s;
            Text label;
            Image bg, prog, pbg, fullTag;
            int lastCount = -1, lastCap = -1;

            public StationTag(WorldUI ui, Station st)
            {
                s = st;
                rt = ui.NewRoot("tag", new Vector2(78, 30));
                bg = UIKit.Image(rt, SpriteGen.Panel(14, 3), Pal.Cream);
                UIKit.Stretch(bg.rectTransform, 0);
                label = UIKit.Text(rt, "", 20, Pal.Ink);
                UIKit.Stretch(label.rectTransform, 0);
                label.rectTransform.offsetMin = new Vector2(22, 0);
                pbg = UIKit.Image(rt, SpriteGen.Circle(64, 0), new Color(0, 0, 0, 0.12f));
                UIKit.Place(pbg.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(20, 20));
                prog = UIKit.Image(rt, SpriteGen.Circle(64, 0), Pal.Parrot);
                UIKit.Place(prog.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(20, 20));
                prog.type = Image.Type.Filled;
                prog.fillMethod = Image.FillMethod.Radial360;
                prog.fillOrigin = (int)Image.Origin360.Top;
                // a full tray shows the mockup's red "FULL" pill instead
                if (Skin.Get("tag_full") != null)
                {
                    fullTag = Skin.Img(rt, "tag_full");
                    UIKit.Place(fullTag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Skin.Size("tag_full"));
                    fullTag.gameObject.SetActive(false);
                }
                anchor = () => s.transform.position + new Vector3(0.74f, 1.5f, -0.3f);   // just over the side table's stock
                offset = Vector2.zero;
            }

            public override void Tick(float dt)
            {
                if (s == null) { Remove(); return; }
                int n = s.Tray.Count, cap = s.Cap;
                // less clutter: the count shows only while Rafi works the station, or as the FULL tag
                Visible = n >= cap || s.PlayerHere;     // the hire-a-cook card sits below the ring, clear of this
                bool full = n >= cap;
                prog.fillAmount = full ? 1 : Mathf.Clamp01(s.Timer / Mathf.Max(0.01f, s.Prep));
                prog.color = full ? Pal.Chili : Pal.Parrot;
                if (n != lastCount || cap != lastCap)
                {
                    lastCount = n; lastCap = cap;
                    label.text = full ? "FULL" : $"{n}/{cap}";
                    bg.color = full ? Pal.Hex("#FFD9D2") : Pal.Cream;
                    label.color = full ? Pal.Chili : Pal.Ink;
                    if (fullTag != null)
                    {
                        if (full && !fullTag.gameObject.activeSelf) Tw.PopIn(fullTag.transform, 0.25f, true);
                        fullTag.gameObject.SetActive(full);
                        bg.enabled = label.enabled = pbg.enabled = prog.enabled = !full;
                    }
                }
            }
        }

        public class Zzz : Widget
        {
            Text t;
            float k;
            public Zzz(WorldUI ui, Transform head)
            {
                rt = ui.NewRoot("zzz", new Vector2(60, 30));
                t = UIKit.OutlinedText(rt, "zzz", 22, Color.white, TextAnchor.MiddleCenter, 1.5f);
                UIKit.Stretch(t.rectTransform, 0);
                anchor = () => head ? head.position : Vector3.zero;
                offset = new Vector2(20, 6);
                Visible = false;
            }

            public override void Tick(float dt)
            {
                k += dt;
                t.rectTransform.anchoredPosition = new Vector2(0, Mathf.Sin(k * 3f) * 4f);
            }
        }

        /// <summary>One bubble per table listing everything its seated guests still want.</summary>
        public class TableBubble : Widget
        {
            DiningTable t;
            Image ring, bg;
            RectTransform icons;
            string sig = "";
            readonly List<Image> pool = new List<Image>();

            public TableBubble(WorldUI ui, DiningTable table)
            {
                t = table;
                rt = ui.NewRoot("tableBubble", new Vector2(90, 70));
                bg = BubbleBody(rt);
                BubbleTail(rt, 1.1f);
                icons = UIKit.Rect(rt, "icons");
                UIKit.Stretch(icons, 0);
                PatienceRing(rt, new Vector2(0, 1), new Vector2(4, -4), 34, out ring);
                anchor = () => t.Center + new Vector3(0, 1.55f, 0);
                offset = new Vector2(0, 10);
                Tw.PopIn(rt, 0.25f, true);
            }

            public override void Tick(float dt)
            {
                if (t == null) { Remove(); return; }
                // aggregate what the seated guests still need
                var counts = new SortedDictionary<ItemId, int>();
                foreach (var c in t.Waiting())
                    foreach (var r in c.Remaining) counts[r] = counts.TryGetValue(r, out var n) ? n + 1 : 1;
                var s = "";
                foreach (var kv in counts) s += kv.Key + ":" + kv.Value + ",";
                if (s != sig)
                {
                    sig = s;
                    foreach (var img in pool) UnityEngine.Object.Destroy(img.gameObject);
                    pool.Clear();
                    int i = 0;
                    foreach (var kv in counts)
                    {
                        if (i >= 3) break;
                        var ic = UIKit.Image(icons, IconBaker.Item(kv.Key), Color.white, "icon");
                        ic.preserveAspect = true;
                        UIKit.Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14 + i * 58, 2), new Vector2(56, 56));
                        var tx = UIKit.OutlinedText(ic.transform, kv.Value > 1 ? "x" + kv.Value : "", 20, Color.white, TextAnchor.LowerRight, 1.5f);
                        UIKit.Stretch(tx.rectTransform, 0);
                        tx.rectTransform.offsetMax = new Vector2(4, 0);
                        pool.Add(ic);
                        i++;
                    }
                    rt.sizeDelta = new Vector2(24 + Mathf.Max(1, i) * 58, 70);
                    Tw.Punch(rt, 0.12f, 0.2f, true);
                }
                float p = t.MinPatience01;
                ring.fillAmount = p;
                ring.color = p > 0.5f ? Color.Lerp(Pal.PatienceYellow, Pal.PatienceGreen, (p - 0.5f) * 2f) : Color.Lerp(Pal.PatienceRed, Pal.PatienceYellow, p * 2f);
            }
        }

        /// <summary>Seat occupancy above each table ("2/4"), so its capacity reads at a glance.</summary>
        public class TableTag : Widget
        {
            DiningTable t;
            Text label;
            Image bg;
            int last = -1;

            public TableTag(WorldUI ui, DiningTable table)
            {
                t = table;
                rt = ui.NewRoot("tableTag", new Vector2(84, 32));
                bg = UIKit.Image(rt, SpriteGen.Panel(14, 3), Pal.Cream);
                UIKit.Stretch(bg.rectTransform, 0);
                var ic = UIKit.Image(rt, SpriteGen.Icon("chair"), Color.white);
                UIKit.Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(28, 28));
                label = UIKit.Text(rt, "", 20, Pal.Ink);
                UIKit.Stretch(label.rectTransform, 0);
                label.rectTransform.offsetMin = new Vector2(30, 0);
                anchor = () => t.Center + new Vector3(t.HalfW + 0.2f, 0.8f, 0.5f);
            }

            public override void Tick(float dt)
            {
                if (t == null) { Remove(); return; }
                int occ = t.Occupied;
                if (occ == last) return;
                last = occ;
                label.text = $"{occ}/{t.Capacity}";
                bool full = occ >= t.Capacity;
                bg.color = full ? Pal.Hex("#FFE7C2") : Pal.Cream;
            }
        }

        /// <summary>A small name pill just below a floor ring ("Chai", "Serve"...), as in the reference layout.</summary>
        public class RingLabel : Widget
        {
            readonly Func<bool> show;

            public RingLabel(WorldUI ui, Func<Vector3> ringEdge, string text, Func<bool> show, bool above)
            {
                this.show = show;
                float w = 26 + text.Length * 11;
                rt = ui.NewRoot("ringLabel", new Vector2(w, 26));
                var bg = UIKit.Image(rt, SpriteGen.Panel(13, 2), Pal.WithA(Pal.Cream, 0.92f));
                UIKit.Stretch(bg.rectTransform, 0);
                var label = UIKit.Text(rt, text, 17, Pal.Ink);
                UIKit.Stretch(label.rectTransform, 0);
                anchor = ringEdge;
                offset = new Vector2(0, above ? 4 : -30);     // pivot is the pill's bottom: just over or under the ring
            }

            public override void Tick(float dt)
            {
                if (show != null) Visible = show();
            }
        }

        /// <summary>
        /// "Hire a cook" card: only while Rafi stands on a station's ring and that station has no cook yet, popping
        /// up over his head. The cook's face, what a cook does, and a price button (green when affordable).
        /// </summary>
        public class HireCookButton : Widget
        {
            readonly Station st;
            readonly Image btn;
            readonly Text cost;
            bool shown;

            public HireCookButton(WorldUI ui, Station station)
            {
                st = station;
                // the "hire a cook" card of the UIv2 mockup: cream panel, the cook's face on an orange tile, title,
                // one line on what a cook does, and a green price button (all in the panel's pixels / 1.5)
                bool v2 = Skin.Get("cook_panel") != null;
                rt = ui.NewRoot("hireCook", v2 ? Skin.Size("cook_panel") : new Vector2(206, 92));
                var card = v2 ? Skin.Img(rt, "cook_panel") : BubbleBody(rt);
                UIKit.Stretch(card.rectTransform, 0);
                card.raycastTarget = true;
                if (!v2) BubbleTail(rt, 0.8f);
                var tile = Skin.Img(rt, "cook_icon_bg", SpriteGen.Rounded(14), Pal.Hex("#FFC27A"));
                Skin.At(tile.rectTransform, 16, 22, 77, 80);
                var por = UIKit.Image(tile.transform, Skin.Get("cook_face") ?? Game.I.Hud.StaffPortrait("cook"), Color.white, "portrait");
                UIKit.Stretch(por.rectTransform, 3);
                por.rectTransform.offsetMin = new Vector2(3, 0);
                por.preserveAspect = true;
                var title = UIKit.Text(rt, "Hire a cook", 21, Skin.Ink, TextAnchor.MiddleLeft);
                Skin.At(title.rectTransform, 103, 14, 205, 40);
                var sub = UIKit.Text(rt, "keeps this stove cooking", 13, Skin.Soft, TextAnchor.MiddleLeft, false);
                Skin.At(sub.rectTransform, 103, 52, 205, 26);
                sub.resizeTextForBestFit = true; sub.resizeTextMinSize = 9; sub.resizeTextMaxSize = 13;
                sub.horizontalOverflow = HorizontalWrapMode.Wrap;
                // a cook is free for a rewarded ad: the green button carries the TV icon and "Watch ad"
                var b = UIKit.Button(rt, Pal.Parrot, null, Click);
                Skin.At((RectTransform)b.transform, 114, 88, 123, 45);
                btn = b.GetComponent<Image>();
                SetPrice(true);
                var tv = UIKit.Image(b.transform, SpriteGen.Icon("tv"), Color.white, "tv");
                tv.preserveAspect = true;
                UIKit.Place(tv.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(7, 1), new Vector2(20, 20));
                // shrinks to fit inside the button (it's only 123 wide)
                cost = Skin.ButtonLabel(b.transform, "Watch ad", 15, 1f);
                UIKit.Stretch(cost.rectTransform, 0);
                cost.rectTransform.offsetMin = new Vector2(29, 4);
                cost.rectTransform.offsetMax = new Vector2(-8, -4);
                cost.horizontalOverflow = HorizontalWrapMode.Wrap;
                cost.verticalOverflow = VerticalWrapMode.Truncate;
                cost.resizeTextForBestFit = true;
                cost.resizeTextMinSize = 9;
                cost.resizeTextMaxSize = 15;
                // on the open floor in front of the ring (towards the counters), hanging below it: never over the stove,
                // its cooking food or Rafi
                anchor = () => st.PickupPos + new Vector3(0, 0, -0.75f);
                offset = new Vector2(0, -rt.sizeDelta.y - 6);
                Visible = false;
            }

            /// <summary>The card's own green button (UIv2), grey while an ad is already playing.</summary>
            void SetPrice(bool affordable)
            {
                var green = Skin.Get("cook_price");
                if (affordable && green != null) { btn.sprite = green; btn.type = Image.Type.Simple; btn.color = Color.white; }
                else UIKit.Paint(btn, affordable ? Pal.Parrot : Pal.ButtonGrey);
            }

            void Click()
            {
                if (WebBridge.AdPlaying) return;
                WebBridge.RewardedBreak("small", ok =>
                {
                    var loc = Game.I.Loc;
                    if (ok && st != null && loc.HireCook(st, true)) { Game.I.Fx.Confetti(st.transform.position + Vector3.up * 1.5f, 10); return; }
                    if (rt) Tw.Punch(rt, 0.15f, 0.25f, true);
                    if (!ok) Game.I.Hud.Toast("No ad right now. Try again in a moment", "tv");
                });
            }

            public override void Tick(float dt)
            {
                if (st == null) { Remove(); return; }
                var loc = Game.I.Loc;
                Visible = loc != null && st.PlayerHere && loc.CanHireCook(st) && !Panel.AnyOpen;
                st.HireCardShown = Visible;
                if (Visible && !shown) Tw.PopIn(rt, 0.25f, true);
                shown = Visible;
                if (!Visible) return;
                SetPrice(!WebBridge.AdPlaying);
            }
        }

        /// <summary>A small icon bobbing over someone's head (the VIP star) until they're gone.</summary>
        public class Badge : Widget
        {
            readonly Transform head;
            float t;
            public Badge(WorldUI ui, Transform head, Sprite s)
            {
                this.head = head;
                rt = ui.NewRoot("badge", new Vector2(30, 29));
                var img = UIKit.Image(rt, s, Color.white, "icon");
                img.preserveAspect = true;
                UIKit.Stretch(img.rectTransform, 0);
                anchor = () => head ? head.position + Vector3.up * 0.35f : Vector3.zero;
                Tw.PopIn(rt, 0.3f, true);
            }

            public override void Tick(float dt)
            {
                if (!head) { Remove(); return; }
                t += dt;
                offset = new Vector2(0, Mathf.Sin(t * 3f) * 3f);
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 2f) * 8f);
            }
        }

        public Badge AddBadge(Transform head, Sprite s) => Add(new Badge(this, head, s));

        public HireCookButton AddHireCook(Station st) => Add(new HireCookButton(this, st));

        public RingLabel AddRingLabel(Func<Vector3> ringEdge, string text, Func<bool> show = null, bool above = false) => Add(new RingLabel(this, ringEdge, text, show, above));

        public TableBubble AddTableBubble(DiningTable t) => Add(new TableBubble(this, t));
        public TableTag AddTableTag(DiningTable t) => Add(new TableTag(this, t));

        public StationTag AddStationTag(Station s) => Add(new StationTag(this, s));
        public OrderBubble AddBubble(Customer c) => Add(new OrderBubble(this, c));
        public MiniRing AddMiniRing(Customer c) => Add(new MiniRing(this, c));
        public Zzz AddZzz(Transform head) => Add(new Zzz(this, head));

        // ------------------------------------------------------------------ one-shots

        /// <summary>"+20" in cash gold: rises 40 px and fades over 0.6 s.</summary>
        public void FloatText(Vector3 world, string text, Color color, string sub = null)
        {
            var p = ToCanvas(world, out bool vis);
            if (!vis) return;
            var rt = NewRoot("float", new Vector2(200, 50));
            rt.anchoredPosition = p;
            var t = UIKit.OutlinedText(rt, text, 30, color, TextAnchor.MiddleCenter, 2f);
            UIKit.Stretch(t.rectTransform, 0);
            Text s = null;
            if (sub != null)
            {
                s = UIKit.OutlinedText(rt, sub, 20, Pal.Hex("#FFF1A8"), TextAnchor.MiddleCenter, 1.5f);
                UIKit.Place(s.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, 2), new Vector2(200, 26));
            }
            Tw.Run(0.75f, k =>
            {
                if (!rt) return;
                rt.anchoredPosition = p + new Vector2(0, Tw.OutQuad(k) * 44);
                float a = k < 0.6f ? 1 : 1 - (k - 0.6f) / 0.4f;
                t.color = Pal.WithA(color, a);
                foreach (var o in t.GetComponents<Outline>()) o.effectColor = Pal.WithA(Pal.Ink, a);
                if (s) s.color = Pal.WithA(s.color, a);
                rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.6f, 1.1f, k / 0.15f) : 1f);
            }, () => { if (rt) Destroy(rt.gameObject); }, true);
        }

        public void Emote(Vector3 world, string icon)
        {
            var p = ToCanvas(world, out bool vis);
            if (!vis) return;
            var rt = NewRoot("emote", new Vector2(56, 56));
            rt.anchoredPosition = p;
            var img = UIKit.Image(rt, SpriteGen.Icon(icon), Color.white);
            UIKit.Stretch(img.rectTransform, 0);
            Tw.Run(0.9f, k =>
            {
                if (!rt) return;
                rt.anchoredPosition = p + new Vector2(0, Tw.OutQuad(k) * 36);
                rt.localScale = Vector3.one * (k < 0.2f ? Tw.OutBack(k / 0.2f) : 1);
                img.color = new Color(1, 1, 1, k < 0.7f ? 1 : 1 - (k - 0.7f) / 0.3f);
            }, () => { if (rt) Destroy(rt.gameObject); }, true);
        }
    }
}
