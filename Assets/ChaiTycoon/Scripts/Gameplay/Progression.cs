using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Task-based progression. Each location has a list of levels; a level is a few tasks shown in the
    /// HUD. Tasks complete on their own (no claim button) and finishing them all levels you up. "Build"
    /// and "Hire" tasks are what reveal the matching unlock tile, so the tasks drive what comes next.
    /// </summary>
    public class Progression
    {
        readonly Game g;
        LevelSave L => g.S.levels;
        bool levelUpPending;

        public LevelCfg[] Levels => g.LocCfg.levels ?? new LevelCfg[0];
        public bool HasLevels => Levels.Length > 0;
        public LevelCfg Current => L.level < Levels.Length ? Levels[L.level] : null;
        public int LevelIndex => L.level;
        public int TaskCount => Current?.tasks?.Length ?? 0;
        public event Action Changed;

        /// <summary>Level number shown to the player, counting levels from earlier locations too.</summary>
        public int DisplayLevel
        {
            get
            {
                int n = 0;
                foreach (var loc in g.Eco.locations)
                {
                    if (loc.id >= g.S.location) break;
                    n += loc.levels?.Length ?? 0;
                }
                return n + L.level + 1;
            }
        }

        public Progression(Game game)
        {
            g = game;
            if (L.location != g.S.location) ResetForLocation();
            Ensure();
            GameEvents.ItemPicked += id => Progress("pick", id.ToString(), 1);
            GameEvents.ItemServed += id => Progress("serve", id.ToString(), 1);
            GameEvents.CustomerServed += OnCustomerServed;
            GameEvents.CashCollected += a => Progress("collect", null, 1);
            GameEvents.CashEarned += a => Progress("earn", null, (float)a);
            GameEvents.Unlocked += id => { Progress("build", id, 1); PreCheck(); };
            GameEvents.Hired += r => Progress("hire", r, 1);
            GameEvents.UpgradeBought += u => Progress("upgrade", u, 1);
            GameEvents.Expanded += () => Progress("expand", null, 1);
            PreCheck();
            OnTasksShown(false);
        }

        public void ResetForLocation()
        {
            L.location = g.S.location;
            L.level = 0;
            L.progress.Clear();
            L.done.Clear();
            levelUpPending = false;
            Ensure();
            if (g.Loc != null) { PreCheck(); OnTasksShown(); }
            Changed?.Invoke();
        }

        void Ensure()
        {
            int n = TaskCount;
            while (L.progress.Count < n) L.progress.Add(0);
            while (L.done.Count < n) L.done.Add(false);
            if (L.progress.Count > n) L.progress.RemoveRange(n, L.progress.Count - n);
            if (L.done.Count > n) L.done.RemoveRange(n, L.done.Count - n);
        }

        // ------------------------------------------------------------ queries

        public TaskCfg Task(int i) => Current != null && i >= 0 && i < TaskCount ? Current.tasks[i] : null;
        public bool IsDone(int i) => i < L.done.Count && L.done[i];
        public float ProgressOf(int i) => i < L.progress.Count ? L.progress[i] : 0;

        public int DoneCount
        {
            get { int n = 0; for (int i = 0; i < TaskCount; i++) if (IsDone(i)) n++; return n; }
        }

        public int FirstOpen()
        {
            for (int i = 0; i < TaskCount; i++) if (!IsDone(i)) return i;
            return -1;
        }

        /// <summary>Counts towards progress right now. Sequential levels work through tasks one at a time.</summary>
        public bool IsActive(int i)
        {
            if (Current == null || IsDone(i)) return false;
            return !Current.sequential || i == FirstOpen();
        }

        /// <summary>The current level, or an earlier one, has a task of this type and target (e.g. hire / cook).</summary>
        public bool Reached(string type, string target)
        {
            for (int l = 0; l <= L.level && l < Levels.Length; l++)
                foreach (var t in Levels[l].tasks)
                    if (t.type == type && string.Equals(t.target, target, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Shown in the HUD: finished tasks and the active ones (future steps of a sequence stay hidden).</summary>
        public bool IsVisible(int i) => IsDone(i) || IsActive(i);

        /// <summary>The task the guide arrow should help with.</summary>
        public int GuideIndex()
        {
            for (int i = 0; i < TaskCount; i++) if (IsActive(i)) return i;
            return -1;
        }

        /// <summary>Unlock tiles the current tasks are asking for.</summary>
        public IEnumerable<string> WantedUnlocks()
        {
            for (int i = 0; i < TaskCount; i++)
            {
                if (!IsActive(i)) continue;
                var t = Current.tasks[i];
                if (t.type == "build" || t.type == "hire") yield return t.target;
                else if (t.type == "expand") yield return "expand";
            }
        }

        // ------------------------------------------------------------ progress

        void OnCustomerServed(Customer c)
        {
            Progress("serveCustomers", null, 1);
            Progress("serveType", c.Type, 1);
            if (c.FrontTime <= g.Eco.rules.fastServeSeconds) Progress("fast", null, 1);
        }

        void Progress(string type, string target, float amount)
        {
            var cur = Current;
            if (cur == null) return;
            // every task in the level counts from the start (a sequential level only *shows* one at a
            // time), so nothing the player does early is ever lost
            for (int i = 0; i < TaskCount; i++)
            {
                if (IsDone(i)) continue;
                var t = cur.tasks[i];
                if (t.type != type) continue;
                if (!string.IsNullOrEmpty(t.target) && target != null && !string.Equals(t.target, target, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(t.target) && target == null) continue;
                L.progress[i] = Mathf.Min(t.count, L.progress[i] + amount);
                if (L.progress[i] >= t.count) Complete(i);
                else Changed?.Invoke();
            }
        }

        /// <summary>Tasks about something already true (built, hired) complete straight away.</summary>
        public void PreCheck()
        {
            var cur = Current;
            if (cur == null || g.Loc == null) return;
            for (int i = 0; i < TaskCount; i++)
            {
                if (IsDone(i)) continue;
                var t = cur.tasks[i];
                bool met = (t.type == "build" && g.Loc.Has(t.target)) || (t.type == "hire" && g.Loc.HasStaff(t.target)) || (t.type == "expand" && g.Loc.Has("expand"));
                if (met) { L.progress[i] = t.count; Complete(i); }
            }
        }

        void Complete(int i)
        {
            if (IsDone(i)) return;
            bool shown = IsActive(i);
            L.done[i] = true;
            L.progress[i] = Current.tasks[i].count;
            var t = Current.tasks[i];
            if (t.reward > 0)
            {
                var from = g.Player.transform.position + Vector3.up * 2f;
                g.AddCash(t.reward, from, false);
                g.Hud.CashFly(from, 6);
            }
            if (shown)
            {
                Synth.Play("ding", 1.1f, 0.8f);
                g.Hud.Level.TaskCompleted(i);
            }
            else g.Hud.Level.Refresh();
            g.RequestSave();
            Changed?.Invoke();
            if (DoneCount >= TaskCount)
            {
                if (!levelUpPending)
                {
                    levelUpPending = true;
                    Tw.Delay(0.9f, LevelUp, true);
                }
            }
            else OnTasksShown();
        }

        void LevelUp()
        {
            levelUpPending = false;
            var done = Current;
            if (done == null) return;
            var from = g.Player.transform.position + Vector3.up * 2f;
            if (done.reward > 0)
            {
                g.AddCash(done.reward, from, false);
                g.Hud.CashFly(from, 12);
            }
            if (done.leaves > 0) g.AddLeaves(done.leaves, from);
            if (!string.IsNullOrEmpty(done.cosmetic) && !g.S.cosmeticsOwned.Contains(done.cosmetic))
            {
                g.S.cosmeticsOwned.Add(done.cosmetic);
                var cos = g.Eco.Cosmetic(done.cosmetic);
                if (cos != null) { RecipeBook.Wear(cos, true); g.Hud.Toast(cos.name + "!", cos.icon); }
            }
            int finished = DisplayLevel;
            L.level++;
            L.progress.Clear();
            L.done.Clear();
            Ensure();
            g.Hud.Level.LevelUp(finished + 1, done, Current);
            GameEvents.LevelUp?.Invoke(L.level);
            PreCheck();
            OnTasksShown();
            g.Save();
            Changed?.Invoke();
        }

        /// <summary>New tasks on screen: reveal their tiles and open up the UI they need.</summary>
        void OnTasksShown(bool pan = true)
        {
            var cur = Current;
            if (cur != null)
                for (int i = 0; i < TaskCount; i++)
                    if (IsActive(i) && cur.tasks[i].type == "upgrade" && !g.S.upgradesSeen)
                    {
                        g.S.upgradesSeen = true;
                        g.Hud.Refresh();
                    }
            g.Loc?.RefreshTiles(pan);
        }

        /// <summary>Expansion bought: whatever is open in this location counts as done.</summary>
        public void FinishLocation()
        {
            Progress("expand", null, 1);
        }
    }
}
