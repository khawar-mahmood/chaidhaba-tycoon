using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>The pause button's panel: the world stops while it is open (as for every panel).</summary>
    public class PausePanel : Panel
    {
        protected override string Title => "Paused";
        protected override string SkinName => Skin.Get("panel_pause") != null ? "panel_pause" : "panel_blank";
        protected override bool TitleBaked => SkinName == "panel_pause";
        protected override Vector2 Size => TitleBaked ? Skin.Size(SkinName) : new Vector2(520, 380);
        // the same frame as Settings, so the cross sits where it does there
        protected override Vector3 CloseAt => TitleBaked ? new Vector3(33, 29, 44) : base.CloseAt;
        protected override Vector4 BodyInsets => TitleBaked ? Vector4.zero : new Vector4(40, 36, 40, 110);
        protected override bool CommercialBreakOnClose => false;

        protected override void Build()
        {
            System.Action settings = () => { Close(); hud.Settings.Open(); };
            if (TitleBaked && Skin.Get("btn_resume") != null && Skin.Get("btn_setting") != null)
            {
                // the painted Resume and Setting buttons, placed as in the pause mockup (1920 px / 1.5)
                var resume = Skin.SpriteButton(body, "btn_resume", Close);
                UIKit.Place((RectTransform)resume.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -181), Skin.Size("btn_resume"));
                var set = Skin.SpriteButton(body, "btn_setting", settings);
                UIKit.Place((RectTransform)set.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -291), Skin.Size("btn_setting"));
                return;
            }
            var r = UIKit.Button(body, Pal.Parrot, "Resume", Close, 36);
            UIKit.Place((RectTransform)r.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(330, 84));
            var s = UIKit.Button(body, Pal.Teal, "Settings", settings, 30);
            UIKit.Place((RectTransform)s.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(280, 70));
        }
    }
}
