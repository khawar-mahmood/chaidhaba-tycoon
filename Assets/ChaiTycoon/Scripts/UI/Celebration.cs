using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>Full-screen truck-art banner with confetti after buying an expansion. Tap to skip.</summary>
    public class Celebration : MonoBehaviour
    {
        RectTransform root, banner;
        Text title, sub;
        Action done;
        float t;
        bool active;

        public static Celebration Make(Hud hud)
        {
            var rt = UIKit.Rect(hud.PanelLayer, "Celebration");
            UIKit.Stretch(rt, 0);
            var c = rt.gameObject.AddComponent<Celebration>();
            c.root = rt;
            c.Build();
            rt.gameObject.SetActive(false);
            return c;
        }

        void Build()
        {
            var dim = UIKit.Image(root, null, new Color(0.12f, 0.08f, 0.14f, 0.35f));
            UIKit.Stretch(dim.rectTransform, 0);
            dim.raycastTarget = true;
            var b = dim.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(Finish);

            banner = UIKit.Rect(root, "banner");
            UIKit.Place(banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(980, 220));
            var bg = UIKit.Image(banner, SpriteGen.Panel(24, 4), Pal.TruckPink);
            UIKit.Stretch(bg.rectTransform, 0);
            var inner = UIKit.Image(banner, SpriteGen.Panel(18, 3), Pal.Marigold);
            UIKit.Stretch(inner.rectTransform, 16);
            for (int i = 0; i < 2; i++)
            {
                var f = UIKit.Image(banner, SpriteGen.Icon("flower"), Color.white);
                UIKit.Place(f.rectTransform, new Vector2(i, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? 70 : -70, 0), new Vector2(100, 100));
            }
            for (int i = 0; i < 9; i++)
            {
                var s = UIKit.Image(banner, SpriteGen.Icon("star"), Color.white);
                UIKit.Place(s.rectTransform, new Vector2(0.1f + i * 0.1f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(34, 34));
            }
            title = UIKit.OutlinedText(banner, "", 64, Color.white, TextAnchor.MiddleCenter, 3f);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 18), new Vector2(760, 90));
            sub = UIKit.Text(banner, "", 28, Pal.Ink, TextAnchor.MiddleCenter);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -52), new Vector2(760, 40));
        }

        public void Show(string head, string subtitle, Action onDone)
        {
            title.text = head;
            sub.text = subtitle;
            done = onDone;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            active = true;
            t = 0;
            banner.localScale = Vector3.zero;
            Tw.Run(0.45f, k => { if (banner) banner.localScale = Vector3.one * Tw.Pop(k); }, null, true);
            Synth.Play("flourish");
            Synth.Play("cheer", 1f, 0.6f);
            var g = Game.I;
            g.Fx.Confetti(g.Player.transform.position + Vector3.up * 2, 40);
        }

        void Update()
        {
            if (!active) return;
            t += Time.unscaledDeltaTime;
            banner.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 3f) * 1.5f);
            if (t > 3.2f) Finish();
        }

        void Finish()
        {
            if (!active) return;
            active = false;
            root.gameObject.SetActive(false);
            var d = done;
            done = null;
            d?.Invoke();
        }
    }
}
