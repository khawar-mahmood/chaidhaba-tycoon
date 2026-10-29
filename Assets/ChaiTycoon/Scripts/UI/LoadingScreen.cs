using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>
    /// Shown from the very first frame while Game boots (audio, art, the dhaba), so the screen is never a blank
    /// grey: the warm background, a chai cup, the title and a progress bar. Fades out when the game is ready.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        RectTransform fill;
        CanvasGroup group;
        float shown, target;

        public static LoadingScreen Show()
        {
            var c = UIKit.Canvas("Loading", 1000);
            var ls = c.gameObject.AddComponent<LoadingScreen>();
            ls.group = c.gameObject.AddComponent<CanvasGroup>();
            var root = UIKit.Rect(c.transform, "root");
            UIKit.Stretch(root, 0);
            var bg = UIKit.Image(root, null, Pal.Hex("#F6D2B4"), "bg");
            UIKit.Stretch(bg.rectTransform, 0);
            bg.raycastTarget = true;

            var cup = UIKit.Image(root, Skin.Get("icon_chai") ?? SpriteGen.Icon("kettle"), Color.white, "cup");
            cup.preserveAspect = true;
            UIKit.Place(cup.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(110, 110));
            var title = UIKit.Text(root, "Chai Dhaba Tycoon", 46, Skin.Ink);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(800, 60));

            var back = Skin.Img(root, "bar_grey", SpriteGen.Rounded(8), new Color(0.23f, 0.11f, 0.07f, 0.15f));
            UIKit.Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -78), new Vector2(360, 26));
            var f = Skin.Img(back.transform, "bar_fill", SpriteGen.Rounded(8), Pal.Marigold);
            ls.fill = f.rectTransform;
            ls.fill.anchorMin = Vector2.zero;
            ls.fill.anchorMax = new Vector2(0, 1);
            ls.fill.offsetMin = ls.fill.offsetMax = Vector2.zero;
            var tip = UIKit.Text(root, "Brewing chai…", 22, Skin.Soft, TextAnchor.MiddleCenter, false);
            UIKit.Place(tip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -122), new Vector2(800, 34));
            return ls;
        }

        /// <summary>Progress 0..1; the bar eases towards it.</summary>
        public void Progress(float p) => target = Mathf.Max(target, Mathf.Clamp01(p));

        void Update()
        {
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 2.5f);
            fill.anchorMax = new Vector2(Mathf.Max(0.06f, shown), 1);
        }

        public void Hide()
        {
            target = 1;
            var g = group;
            Tw.Run(0.35f, k => { if (g) g.alpha = 1 - k; }, () => { if (this) Destroy(gameObject); }, true);
        }
    }
}
