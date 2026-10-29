using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>
    /// A simple illustrated road map: each dhaba is a stop along one highway, with its stars, passive
    /// income and a Travel button; locked stops show their unlock price.
    /// </summary>
    public class WorldMap : Panel
    {
        protected override string Title => "The road ahead";
        protected override Vector2 Size => new Vector2(1100, 620);
        RectTransform road;

        protected override void Build()
        {
            road = UIKit.Rect(body, "road");
            UIKit.Stretch(road, 0);
        }

        public override void Refresh()
        {
            foreach (Transform t in road) Destroy(t.gameObject);
            var g = Game.I;
            // the highway: a winding asphalt band with dashes
            var band = UIKit.Image(road, SpriteGen.Rounded(18), Pal.Asphalt);
            UIKit.Place(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1000, 36));
            for (int i = 0; i < 20; i++)
            {
                var d = UIKit.Image(road, SpriteGen.Rounded(3), Pal.Hex("#F3EDE2"));
                UIKit.Place(d.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-475 + i * 50, -10), new Vector2(24, 5));
            }
            for (int i = 0; i < g.Eco.locations.Length; i++)
            {
                var loc = g.Eco.locations[i];
                var ls = g.S.Loc(loc.id);
                bool here = g.S.location == loc.id;
                bool reached = loc.id <= g.S.maxLocation;
                bool nextUp = loc.id == g.S.maxLocation + 1 || (loc.id == g.S.maxLocation + 1);
                float x = -400 + i * 200;
                float y = i % 2 == 0 ? 80 : -110;

                var pin = UIKit.Image(road, SpriteGen.Circle(64, 4), reached ? Pal.TruckPink : Pal.ButtonGrey);
                UIKit.Place(pin.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -10), new Vector2(34, 34));

                var card = UIKit.Image(road, SpriteGen.Panel(16, 3), here ? Pal.Hex("#FFF0C2") : Color.white);
                UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(190, 168));
                var name = UIKit.Text(card.transform, loc.shortName, 22, Pal.Ink, TextAnchor.UpperCenter);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                UIKit.TopLeft(name.rectTransform, 8, 10, 174, 54);

                var info = UIKit.Text(card.transform, "", 18, Pal.Mul(Pal.Ink, 1.7f), TextAnchor.MiddleCenter, false);
                UIKit.TopLeft(info.rectTransform, 8, 66, 174, 36);

                if (reached)
                {
                    for (int s = 0; s < 5; s++)
                    {
                        var st = UIKit.Image(card.transform, SpriteGen.Icon(s < ls.stars ? "star" : "star_empty"), Color.white);
                        UIKit.TopLeft(st.rectTransform, 24 + s * 29, 66, 28, 28);
                    }
                    if (here) info.text = "";
                    else info.text = "";
                    if (here)
                    {
                        var you = UIKit.OutlinedText(card.transform, "You are here", 18, Pal.Marigold, TextAnchor.MiddleCenter, 1.4f);
                        UIKit.Place(you.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(180, 40));
                    }
                    else
                    {
                        int id = loc.id;
                        var b = UIKit.Button(card.transform, Pal.Parrot, "Travel", () => Travel(id), 22);
                        UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(150, 50));
                    }
                }
                else
                {
                    var lockI = UIKit.Image(card.transform, SpriteGen.Icon("lock"), Color.white);
                    UIKit.TopLeft(lockI.rectTransform, 74, 62, 42, 42);
                    var prev = g.Eco.Location(loc.id - 1);
                    double price = 0;
                    if (prev != null) foreach (var u in prev.unlocks) if (u.kind == "expand") price = u.cost;
                    string txt = loc.playable ? Fmt.Cash(price) : "Opening soon";
                    var p = UIKit.Text(card.transform, txt, 20, Pal.Ink, TextAnchor.MiddleCenter);
                    UIKit.Place(p.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(180, 40));
                }
            }
        }

        void Travel(int id)
        {
            var g = Game.I;
            var cfg = g.Eco.Location(id);
            if (cfg == null || !cfg.playable) return;
            Close();
            g.Travel(id);
        }
    }
}
