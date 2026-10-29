using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChaiTycoon
{
    /// <summary>
    /// Top-left HUD card: the current level, its progress, and its tasks. Tasks tick off with a check;
    /// tapping a task points the guide arrow at it. Finishing a level plays a "Level Up!" banner.
    /// </summary>
    public class LevelCard : MonoBehaviour
    {
        RectTransform card, rowsRoot, banner;
        Text levelNum, titleText, levelLabel, bannerTitle, bannerSub;
        Image bar, badge, bannerBg, head;
        bool collapsed;
        float bannerT = -1;
        public bool BannerShowing => bannerT >= 0;

        class Row
        {
            public RectTransform rt;
            public Image bg, icon, check, barBg, bar, pill;
            public Text text, prog;
            public int index = -1;
        }
        readonly List<Row> rows = new List<Row>();
        // the mockup's level card and task rows (1920 px / 1.5)
        const float W = 311, HeadH = 83, RowTop = 93, RowH = 80, RowStep = 88;
        // the painted task bar's orange tile spans x 27-139 px of 979 (border shown at 1.2x density): its centre is 46
        // canvas units in and it ends at 78, so the text starts clear of it
        const float TileX = 46, TextX = 88;

        /// <summary>The card in <paramref name="parent"/>; the level-up banner goes on <paramref name="bannerParent"/>.</summary>
        public static LevelCard Create(RectTransform parent, RectTransform bannerParent = null)
        {
            var rt = UIKit.Rect(parent, "levelCard");
            UIKit.TopLeft(rt, 27, 105, W, 450);
            var lc = rt.gameObject.AddComponent<LevelCard>();
            lc.card = rt;
            lc.Build(bannerParent ? bannerParent : parent);
            return lc;
        }

        void Build(RectTransform parent)
        {
            // header: the painted level bar (number in the yellow circle, or the cup tile on level 1), level and title
            head = Skin.Img(card, "level_bar", SpriteGen.Panel(18, 3), Pal.Hex("#FFF0C8"));
            UIKit.TopLeft(head.rectTransform, 0, 0, W, HeadH);
            head.raycastTarget = true;
            var hb = head.gameObject.AddComponent<Button>();
            hb.transition = Selectable.Transition.None;
            hb.onClick.AddListener(() => { collapsed = !collapsed; Synth.Play("click"); Refresh(); });

            badge = UIKit.Image(head.transform, Skin.On ? null : SpriteGen.Circle(64, 4), Skin.On ? Color.clear : Pal.Marigold, "badge");
            UIKit.Place(badge.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40, 0), new Vector2(60, 60));
            levelNum = UIKit.Text(badge.transform, "1", 40, Skin.Ink);    // dark on the yellow circle, no outline (as in the mockup)
            UIKit.Stretch(levelNum.rectTransform, 0);
            levelNum.rectTransform.offsetMin = new Vector2(0, 2);

            levelLabel = UIKit.Text(head.transform, "Level 1", 30, Skin.Ink, TextAnchor.MiddleLeft);
            UIKit.TopLeft(levelLabel.rectTransform, 100, 9, 200, 36);
            titleText = UIKit.Text(head.transform, "", 21, Skin.Orange, TextAnchor.MiddleLeft);
            UIKit.TopLeft(titleText.rectTransform, 100, 41, 200, 28);
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.verticalOverflow = VerticalWrapMode.Truncate;
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMinSize = 13;
            titleText.resizeTextMaxSize = 21;

            // level progress: a thin bar along the header's foot (the tasks show the detail)
            var barBg = UIKit.Image(head.transform, SpriteGen.Rounded(4), new Color(0.23f, 0.11f, 0.07f, 0.12f));
            UIKit.TopLeft(barBg.rectTransform, 100, 70, 190, 6);
            barBg.gameObject.SetActive(!Skin.On);   // the mockup card has no bar: the task rows show the progress
            bar = UIKit.Image(barBg.transform, SpriteGen.Rounded(4), Pal.Parrot);
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            UIKit.Stretch(bar.rectTransform, 0);

            rowsRoot = UIKit.Rect(card, "rows");
            UIKit.TopLeft(rowsRoot, 0, RowTop, W, 360);
            for (int i = 0; i < 4; i++) rows.Add(MakeRow(i));

            // level-up banner (top centre, non-blocking): the pink ribbon with its stars
            banner = UIKit.Rect(parent, "levelUp");
            UIKit.Place(banner, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -175), Skin.On ? new Vector2(368, 95) : new Vector2(560, 130));
            if (Skin.On)
            {
                bannerBg = Skin.Img(banner, "level_banner");
                UIKit.Stretch(bannerBg.rectTransform, 0);
                bannerTitle = Skin.ButtonLabel(banner, "LEVEL UP!", 32, 2.4f);
                UIKit.Place(bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 18), new Vector2(220, 42));
                bannerSub = UIKit.Text(banner, "", 17, Skin.Ink, TextAnchor.MiddleCenter);
                UIKit.Place(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(2, -24), new Vector2(170, 24));
                bannerSub.resizeTextForBestFit = true; bannerSub.resizeTextMinSize = 11; bannerSub.resizeTextMaxSize = 17;
                bannerSub.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            else
            {
                bannerBg = UIKit.Image(banner, SpriteGen.Panel(24, 4), Pal.TruckPink);
                UIKit.Stretch(bannerBg.rectTransform, 0);
                var inner = UIKit.Image(banner, SpriteGen.Panel(18, 3), Pal.Marigold);
                UIKit.Stretch(inner.rectTransform, 10);
                for (int i = 0; i < 2; i++)
                {
                    var st = UIKit.Image(banner, SpriteGen.Icon("star"), Color.white);
                    UIKit.Place(st.rectTransform, new Vector2(i, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? 52 : -52, 6), new Vector2(64, 64));
                }
                bannerTitle = UIKit.OutlinedText(banner, "LEVEL UP!", 46, Color.white, TextAnchor.MiddleCenter, 2.6f);
                UIKit.Place(bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(420, 60));
                bannerSub = UIKit.Text(banner, "", 22, Pal.Ink, TextAnchor.MiddleCenter);
                UIKit.Place(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(420, 30));
            }
            banner.gameObject.SetActive(false);
        }

        Row MakeRow(int i)
        {
            // a task row: the painted task bar (icon in its orange tile), text, progress bar and a dark count pill
            var r = new Row();
            var bg = Skin.Img(rowsRoot, "hint_bar", SpriteGen.Panel(14, 3), Pal.Cream);
            bg.pixelsPerUnitMultiplier = 1.2f;
            r.bg = bg;
            r.rt = bg.rectTransform;
            UIKit.TopLeft(r.rt, 0, i * RowStep, W, RowH);
            bg.raycastTarget = true;
            var b = bg.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var row = r;
            b.onClick.AddListener(() =>
            {
                if (row.index < 0) return;
                Synth.Play("click");
                Game.I.Guide?.Focus(row.index);
                Tw.Punch(row.rt, 0.08f, 0.2f, true);
            });
            r.icon = UIKit.Image(r.rt, SpriteGen.Icon("coin"), Color.white);
            UIKit.Place(r.icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Skin.On ? TileX : 38, Skin.On ? 2 : 0), new Vector2(46, 46));   // centred in the painted tile
            r.icon.preserveAspect = true;
            r.text = UIKit.Text(r.rt, "", 18, Skin.Ink, TextAnchor.MiddleLeft);
            UIKit.TopLeft(r.text.rectTransform, TextX, 9, W - TextX - 12, 26);
            r.text.horizontalOverflow = HorizontalWrapMode.Wrap;
            r.text.verticalOverflow = VerticalWrapMode.Truncate;
            r.text.resizeTextForBestFit = true;
            r.text.resizeTextMinSize = 12;
            r.text.resizeTextMaxSize = 18;
            r.barBg = Skin.Img(r.rt, "bar_grey", SpriteGen.Rounded(5), new Color(0.17f, 0.12f, 0.18f, 0.16f));
            UIKit.TopLeft(r.barBg.rectTransform, TextX, 44, 222 - TextX, 19);
            r.bar = Skin.Img(r.barBg.transform, "bar_fill", SpriteGen.Rounded(5), Pal.Parrot);
            r.bar.rectTransform.anchorMin = Vector2.zero;
            r.bar.rectTransform.anchorMax = new Vector2(0, 1);
            r.bar.rectTransform.pivot = new Vector2(0, 0.5f);
            r.bar.rectTransform.offsetMin = Vector2.zero;
            r.bar.rectTransform.offsetMax = Vector2.zero;
            r.pill = UIKit.Image(r.rt, SpriteGen.Rounded(12), Skin.Ink, "pill");
            UIKit.TopLeft(r.pill.rectTransform, 228, 40, 70, 26);
            r.prog = UIKit.Text(r.pill.transform, "", 18, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(r.prog.rectTransform, 0);
            r.prog.rectTransform.offsetMin = new Vector2(0, 1);
            r.prog.resizeTextForBestFit = true; r.prog.resizeTextMinSize = 10; r.prog.resizeTextMaxSize = 18;
            r.prog.horizontalOverflow = HorizontalWrapMode.Wrap;
            r.check = UIKit.Image(r.rt, SpriteGen.Icon("check"), Color.white);
            UIKit.Place(r.check.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(40, 40));
            r.check.enabled = false;
            return r;
        }

        public static Sprite TaskIcon(TaskCfg t)
        {
            switch (t.type)
            {
                case "pick":
                case "serve":
                    if (!string.IsNullOrEmpty(t.target)) return IconBaker.Item(Items.Parse(t.target));
                    break;
                case "hire": return Game.I.Hud.StaffPortrait(t.target);
                case "build":
                {
                    var u = FindUnlock(t.target);
                    if (u != null && u.kind == "station") return IconBaker.Item(Items.Parse(u.arg));
                    if (u != null && u.kind == "staff") return Game.I.Hud.StaffPortrait(u.arg);
                    break;
                }
            }
            return Skin.Icon(string.IsNullOrEmpty(t.icon) ? "check" : t.icon);
        }

        public static UnlockCfg FindUnlock(string id)
        {
            foreach (var u in Game.I.LocCfg.unlocks) if (u.id == id) return u;
            return null;
        }

        public void Refresh()
        {
            var g = Game.I;
            var p = g?.Progression;
            if (p == null || !p.HasLevels || (g.Hud != null && g.Hud.Minimal)) { card.gameObject.SetActive(false); return; }
            if (!card.gameObject.activeSelf)
            {
                // first appearance after the first sale: the card springs in
                card.gameObject.SetActive(true);
                Tw.PopIn(card, 0.35f, true);
            }
            var cur = p.Current;
            levelNum.text = p.DisplayLevel.ToString();
            levelLabel.text = cur != null ? "Level " + p.DisplayLevel : "All done";
            // level 1 shows the painted cup tile instead of a number
            bool first = Skin.On && p.DisplayLevel <= 1;
            if (Skin.On) head.sprite = Skin.Get(first ? "level_bar_first" : "level_bar");
            levelNum.enabled = !first;
            titleText.text = cur != null ? cur.title : "Every task complete!";
            bar.fillAmount = cur != null && p.TaskCount > 0 ? (float)p.DoneCount / p.TaskCount : 1f;

            int shown = 0;
            bool small = collapsed || Screen.height < 380;
            if (cur != null && !small)
            {
                // more visible tasks than rows: drop the oldest finished ones so the active task stays on the card
                int skipDone = -rows.Count;
                for (int i = 0; i < p.TaskCount; i++) if (p.IsVisible(i)) skipDone++;
                for (int i = 0; i < p.TaskCount && shown < rows.Count; i++)
                {
                    if (!p.IsVisible(i)) continue;
                    if (skipDone > 0 && p.IsDone(i)) { skipDone--; continue; }
                    var r = rows[shown];
                    var t = cur.tasks[i];
                    bool done = p.IsDone(i);
                    r.index = i;
                    r.rt.gameObject.SetActive(true);
                    UIKit.TopLeft(r.rt, 0, shown * RowStep, W, RowH);
                    r.text.text = t.text;
                    r.icon.sprite = TaskIcon(t);
                    float prog = p.ProgressOf(i);
                    bool showBar = t.count > 1 && !done;
                    float frac = Mathf.Clamp01(prog / Mathf.Max(1, t.count));
                    r.barBg.gameObject.SetActive(showBar);
                    r.pill.gameObject.SetActive(showBar);
                    r.bar.enabled = frac > 0.01f;
                    r.bar.rectTransform.anchorMax = new Vector2(frac, 1);
                    r.prog.text = done || t.count <= 1 ? "" : t.type == "earn" ? $"{Fmt.Cash(prog)}/{Fmt.Cash(t.count)}" : $"{(int)prog}/{t.count}";
                    r.check.enabled = done;
                    var tint = done ? Pal.Hex("#E3FBD6") : p.GuideIndex() == i ? Pal.Hex("#FFF7E6") : Color.white;
                    r.bg.color = Skin.On ? tint : (tint == Color.white ? Pal.Cream : tint);
                    r.text.color = done ? Pal.Mul(Skin.Ink, 1.9f) : Skin.Ink;
                    // single-step tasks centre their text
                    UIKit.TopLeft(r.text.rectTransform, TextX, showBar ? 9 : 27, W - TextX - (done ? 56 : 12), 26);
                    shown++;
                }
            }
            for (int i = shown; i < rows.Count; i++) { rows[i].rt.gameObject.SetActive(false); rows[i].index = -1; }
        }

        public void TaskCompleted(int i)
        {
            Refresh();
            foreach (var r in rows)
                if (r.index == i)
                {
                    Tw.PopIn(r.check.transform, 0.3f, true);
                    Tw.Punch(r.rt, 0.1f, 0.3f, true);
                }
            Game.I.Hud.Toast("Task complete!", "check");
        }

        public void LevelUp(int newLevel, LevelCfg finished, LevelCfg next)
        {
            Refresh();
            Tw.Punch(card, 0.12f, 0.35f, true);
            Tw.PopIn(badge.transform, 0.4f, true);
            bannerTitle.text = Skin.On ? "LEVEL " + newLevel : "LEVEL " + newLevel + "!";
            bannerSub.text = next != null ? next.title : "Stage complete!";
            banner.gameObject.SetActive(true);
            banner.SetAsLastSibling();
            banner.localScale = Vector3.zero;
            Tw.Run(0.4f, k => { if (banner) banner.localScale = Vector3.one * Tw.Pop(k); }, null, true);
            bannerT = 0;
            Synth.Play("tada");
            Synth.Play("cheer", 1.1f, 0.5f);
            var g = Game.I;
            g.Fx.Confetti(g.Player.transform.position + Vector3.up * 2.2f, 30);
        }

        void Update()
        {
            if (bannerT >= 0)
            {
                bannerT += Time.unscaledDeltaTime;
                banner.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(bannerT * 4f) * 2f);
                if (bannerT > 2.4f)
                {
                    bannerT = -1;
                    Tw.Run(0.2f, k => { if (banner) banner.localScale = Vector3.one * (1 - k); }, () => banner.gameObject.SetActive(false), true);
                }
            }
        }

        /// <summary>The on-screen rect of a task row, for the guide's pointing hand.</summary>
        public RectTransform RowRect(int taskIndex)
        {
            foreach (var r in rows) if (r.index == taskIndex && r.rt.gameObject.activeSelf) return r.rt;
            return null;
        }
    }
}
