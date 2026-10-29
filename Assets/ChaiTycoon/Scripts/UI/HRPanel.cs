using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>
    /// "Staff" panel, laid out as the staff mockup: one painted row per staff member with a red portrait
    /// circle, name and job, Speed / Carry upgrades with green price buttons, and Assign for waiters.
    /// </summary>
    public class HRPanel : Panel
    {
        protected override string Title => "Staff";
        protected override string SkinName => "panel_staff";
        protected override bool TitleBaked => Skin.On;
        protected override Vector2 Size => Skin.On ? Skin.Size(SkinName) : new Vector2(913, 609);
        protected override Vector3 CloseAt => new Vector3(35, 25, 65);
        protected override Vector4 BodyInsets => new Vector4(35, 36, 40, 123);
        const float RowW = 838, RowH = 139, RowGap = 10;
        RectTransform content;
        ScrollRect scroll;

        class Row
        {
            public Staff s;
            public Text zone;
            public Button[] btns = new Button[2];
            public Text[] btnText = new Text[2];
            public Image[] btnImg = new Image[2];
            public Image[] coin = new Image[2];
            public Text[] lvl = new Text[2];
            public UpgradeCfg[] ups = new UpgradeCfg[2];
        }
        readonly List<Row> rows = new List<Row>();
        Button hireBtn;
        Image hireCoin;

        protected override void Build()
        {
            scroll = MakeScroll(body, out content);
        }

        public bool AnyAffordable()
        {
            var g = Game.I;
            if (g?.Loc == null) return false;
            if (g.Loc.CanHireHelper && g.S.cash >= g.Loc.HelperCost) return true;
            foreach (var s in g.Loc.StaffList)
            {
                if (!s) continue;
                foreach (var u in UpgradesFor(s.Role))
                {
                    int l = g.Up.StaffLevel(s.Data, u.id);
                    if (l < u.max && g.S.cash >= g.Up.Cost(u, l)) return true;
                }
            }
            return false;
        }

        static List<UpgradeCfg> UpgradesFor(string role)
        {
            var list = new List<UpgradeCfg>();
            if (role == "cook") return list;          // cooks stay put: speed and carry don't apply
            foreach (var u in Game.I.Eco.upgrades)
            {
                if (u.tab != "staff") continue;
                if (u.roles != null && u.roles.Length > 0 && System.Array.IndexOf(u.roles, role) < 0) continue;
                list.Add(u);
            }
            return list;
        }

        /// <summary>The painted waiter portraits for the helpers, the rendered model for everyone else.</summary>
        /// <summary>The painted bust for a row: the girl in the red apron for cooks, the waiter for everyone else.</summary>
        public static Sprite Portrait(string role) =>
            Skin.Get(role == "cook" ? "staff_waiter2" : "staff_waiter1") ?? Game.I.Hud.StaffPortrait(role);

        Image PortraitCircle(Transform parent, string role, float x, float y, float size)
        {
            // the orange rounded tile with the bust standing on its lower edge
            var circle = Skin.Img(parent, "staff_portrait", SpriteGen.Circle(64, 3), Models.StaffCapColor(role));
            UIKit.TopLeft(circle.rectTransform, x, y, size, size * 1.04f);
            var por = UIKit.Image(circle.transform, Portrait(role), Color.white, "portrait");
            UIKit.Stretch(por.rectTransform, 0);
            por.rectTransform.offsetMin = new Vector2(size * 0.04f, size * 0.03f);
            por.rectTransform.offsetMax = new Vector2(-size * 0.04f, -size * 0.1f);
            por.preserveAspect = true;
            return circle;
        }

        /// <summary>A green price button: coin and amount (white, outlined).</summary>
        Button PriceButton(Transform parent, float x, float y, float w, float h, System.Action onClick, out Image img, out Text text, out Image coin)
        {
            var b = UIKit.Button(parent, Pal.Parrot, "", onClick, 28);
            UIKit.TopLeft((RectTransform)b.transform, x, y, w, h);
            img = b.GetComponent<Image>();
            text = b.GetComponentInChildren<Text>();
            text.rectTransform.offsetMin = new Vector2(h * 0.72f, 4);
            coin = Skin.Img(b.transform, "coin", SpriteGen.Icon("coin"));
            coin.preserveAspect = true;
            UIKit.Place(coin.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(h * 0.25f, 2), new Vector2(h * 0.62f, h * 0.62f));
            return b;
        }

        public override void Refresh()
        {
            foreach (Transform t in content) Destroy(t.gameObject);
            rows.Clear();
            var g = Game.I;
            float y = 0;
            foreach (var s in g.Loc.StaffList)
            {
                if (!s) continue;
                var r = new Row { s = s };
                var bg = Skin.Img(content, "staff_row", SpriteGen.Panel(16, 3));
                UIKit.TopLeft(bg.rectTransform, 0, y, RowW, RowH);
                PortraitCircle(bg.transform, s.Role, 18, 15, 105);
                var name = UIKit.Text(bg.transform, s.Role == "waiter" ? "Helper" : Cap(s.Role), 34, Skin.Ink, TextAnchor.MiddleLeft);
                UIKit.TopLeft(name.rectTransform, 141, 22, 175, 42);
                r.zone = UIKit.Text(bg.transform, "", 20, Skin.Soft, TextAnchor.MiddleLeft);
                UIKit.TopLeft(r.zone.rectTransform, 141, 62, 180, 32);
                r.zone.horizontalOverflow = HorizontalWrapMode.Wrap;
                r.zone.resizeTextForBestFit = true; r.zone.resizeTextMinSize = 13; r.zone.resizeTextMaxSize = 20;

                var ups = UpgradesFor(s.Role);
                float x = 318;
                for (int i = 0; i < ups.Count && i < 2; i++)
                {
                    var u = ups[i];
                    r.ups[i] = u;
                    // the painted "Speed" / "Carry" labels, with the level after them
                    var painted = Skin.Get(u.id == "staffSpeed" ? "label_speed" : u.id == "staffCarry" ? "label_carry" : "");
                    float lw = 0;
                    if (painted != null)
                    {
                        var lab = UIKit.Image(bg.transform, painted, Color.white, "label");
                        lw = painted.rect.width / 1.5f * 0.82f;
                        UIKit.TopLeft(lab.rectTransform, x - 4, 14, lw, painted.rect.height / 1.5f * 0.82f);
                    }
                    else
                    {
                        var ic = UIKit.Image(bg.transform, SpriteGen.Icon(u.icon), Color.white);
                        UIKit.TopLeft(ic.rectTransform, x, 14, 32, 32);
                        lw = 36;
                    }
                    r.lvl[i] = UIKit.Text(bg.transform, "", 22, Skin.Ink, TextAnchor.MiddleLeft);
                    UIKit.TopLeft(r.lvl[i].rectTransform, x - 4 + lw + 4, 14, 70, 32);
                    int idx = i;
                    var row = r;
                    r.btns[i] = PriceButton(bg.transform, x, 58, 165, 60, () => BuyStaff(row, idx), out r.btnImg[i], out r.btnText[i], out r.coin[i]);
                    x += 182;
                }
                if (ups.Count == 0)
                {
                    var t = UIKit.Text(bg.transform, s.Role == "cook" ? "Keeps the station cooking" : "", 20, Skin.Soft, TextAnchor.MiddleLeft);
                    UIKit.TopLeft(t.rectTransform, 330, 50, 340, 40);
                }
                if (s.Role == "waiter" && !s.HasJob)      // helpers with a job (a queue, rickshaws, tables) stay on it
                {
                    var row = r;
                    var a = Skin.SpriteButton(bg.transform, "btn_assign", () => { row.s.CycleAssignment(); LiveRefresh(); }, SpriteGen.Button(18), Pal.Teal);
                    UIKit.TopLeft((RectTransform)a.transform, 683, 33, 144, 70);
                    if (!Skin.On)
                    {
                        var at = UIKit.OutlinedText(a.transform, "Assign", 22, Color.white);
                        UIKit.Stretch(at.rectTransform, 0);
                    }
                }
                rows.Add(r);
                y += RowH + RowGap;
            }
            // one more helper to carry and serve, while there's room for one
            hireBtn = null;
            if (g.Loc.CanHireHelper)
            {
                var bg = Skin.Img(content, "staff_row", SpriteGen.Panel(16, 3));
                UIKit.TopLeft(bg.rectTransform, 0, y, RowW, RowH);
                bg.color = new Color(1f, 0.96f, 0.86f);
                var circle = PortraitCircle(bg.transform, "waiter", 18, 15, 105);
                circle.color = new Color(1, 1, 1, 0.75f);
                var t = UIKit.Text(bg.transform, $"Hire another helper ({g.Loc.HelperCount}/{g.Eco.rules.maxHelpers})", 30, Skin.Ink, TextAnchor.MiddleLeft);
                UIKit.TopLeft(t.rectTransform, 141, 26, 420, 40);
                var d = UIKit.Text(bg.transform, "Picks up food and serves the counter and tables", 19, Skin.Soft, TextAnchor.MiddleLeft);
                UIKit.TopLeft(d.rectTransform, 141, 66, 470, 30);
                hireBtn = PriceButton(bg.transform, 640, 38, 180, 64, () =>
                {
                    if (Game.I.Loc.HireHelper()) Refresh();
                    else if (hireBtn) Shake(hireBtn.transform);
                }, out _, out _, out hireCoin);
                y += RowH + RowGap;
            }
            content.sizeDelta = new Vector2(0, y);
            LiveRefresh();
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        void BuyStaff(Row r, int i)
        {
            var g = Game.I;
            var u = r.ups[i];
            if (u == null) return;
            if (g.Up.BuyStaff(r.s.Data, u))
            {
                Synth.Play("buy");
                Tw.Punch(r.btns[i].transform, 0.1f, 0.2f, true);
                r.s.RefreshStats();
                LiveRefresh();
            }
            else Shake(r.btns[i].transform);
        }

        protected override void LiveRefresh()
        {
            var g = Game.I;
            if (hireBtn)
            {
                double hc = g.Loc.HelperCost;
                UIKit.Paint(hireBtn.GetComponent<Image>(), g.S.cash >= hc ? Pal.Parrot : Pal.ButtonGrey);
                hireBtn.GetComponentInChildren<Text>().text = Fmt.Cash(hc);
            }
            foreach (var r in rows)
            {
                if (!r.s) continue;
                r.zone.text = r.s.Role == "cashier" ? "Collects counter cash" : r.s.Role == "waiter" ? (r.s.HasJob ? "Serves " + r.s.AssignmentLabel : "Carries: " + r.s.AssignmentLabel)
                    : r.s.Role == "cook" ? "Cooks at the " + r.s.StationLabel : "";
                for (int i = 0; i < 2; i++)
                {
                    var u = r.ups[i];
                    if (u == null || r.btns[i] == null) continue;
                    int l = g.Up.StaffLevel(r.s.Data, u.id);
                    r.lvl[i].text = $"{l}/{u.max}";
                    if (l >= u.max)
                    {
                        UIKit.Paint(r.btnImg[i], Pal.ButtonGrey);
                        r.btnText[i].text = "MAX";
                        r.btnText[i].rectTransform.offsetMin = new Vector2(6, 4);
                        r.coin[i].enabled = false;
                        continue;
                    }
                    double cost = g.Up.Cost(u, l);
                    UIKit.Paint(r.btnImg[i], g.S.cash >= cost ? Pal.Parrot : Pal.ButtonGrey);
                    r.btnText[i].text = Fmt.Cash(cost);
                }
            }
        }
    }
}
