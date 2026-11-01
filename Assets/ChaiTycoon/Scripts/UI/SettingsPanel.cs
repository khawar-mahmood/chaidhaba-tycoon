using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>
    /// Settings, as in the mockup: the painted panel already has the title and the two green bars, so the
    /// toggles are see-through buttons over the bars with the sound / music icon and an ON / OFF label.
    /// No reset button: wiping the save from the game was removed (the editor can still clear PlayerPrefs).
    /// </summary>
    public class SettingsPanel : Panel
    {
        protected override string Title => "Settings";
        protected override string SkinName => "panel_settings";
        protected override bool TitleBaked => Skin.On;
        protected override Vector2 Size => Skin.On ? Skin.Size(SkinName) : new Vector2(612, 411);
        protected override Vector3 CloseAt => new Vector3(33, 29, 44);
        protected override Vector4 BodyInsets => Vector4.zero;
        Image sfxIcon, musicIcon;
        Text sfxTxt, musicTxt;

        protected override void Build()
        {
            // the body is the whole panel here: children are placed in the mockup's pixels
            BuildToggle(154, out sfxIcon, out sfxTxt, () =>
            {
                var s = Game.I.S;
                s.sfx = !s.sfx;
                Synth.I.SfxOn = s.sfx;
            });
            BuildToggle(297, out musicIcon, out musicTxt, () =>
            {
                var s = Game.I.S;
                s.music = !s.music;
                Synth.I.MusicOn = s.music;
            });

            var ver = UIKit.Text(body, "v" + Application.version, 16, Skin.Soft, TextAnchor.LowerRight, false);
            Skin.At(ver.rectTransform, 560, 520, 300, 40);
        }

        void BuildToggle(float y, out Image icon, out Text label, System.Action toggle)
        {
            var hit = UIKit.Image(body, Skin.On ? null : SpriteGen.Button(18), Skin.On ? new Color(1, 1, 1, 0) : Pal.Parrot, "toggle");
            Skin.At(hit.rectTransform, 100, y, 710, 123);
            hit.raycastTarget = true;
            var b = hit.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() =>
            {
                Synth.Play("click");
                toggle();
                Game.I.RequestSave();
                Refresh();
                Game.I.Hud.RefreshMute();
                Tw.Punch(hit.transform, 0.06f, 0.18f, true);
            });
            icon = UIKit.Image(hit.transform, null, Color.white, "icon");
            icon.preserveAspect = true;
            Skin.At(icon.rectTransform, 40, 14, 95, 92);
            label = Skin.ButtonLabel(hit.transform, "", 34, 2f);
            Skin.At(label.rectTransform, 160, 10, 520, 100);
        }

        public override void Refresh()
        {
            var s = Game.I.S;
            sfxIcon.sprite = Skin.Get(s.sfx ? "icon_sound" : "icon_sound_off") ?? SpriteGen.Icon("speaker");
            musicIcon.sprite = Skin.Get(s.music ? "icon_music" : "icon_music_off") ?? SpriteGen.Icon("note");
            sfxTxt.text = s.sfx ? "Sound:ON" : "Sound:OFF";
            musicTxt.text = s.music ? "Music:ON" : "Music:OFF";
        }
    }
}
