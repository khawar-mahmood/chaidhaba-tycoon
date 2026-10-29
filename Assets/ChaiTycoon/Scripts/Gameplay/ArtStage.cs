using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Builds a whole stage from its Blender layout: the environment mesh, every prop where the level
    /// designer put it, and the crowd in their poses. Stages 2-5 have art and layouts but no gameplay yet,
    /// so this is what the editor's Chai > Art Preview menu shows.
    /// </summary>
    public static class ArtStage
    {
        public static readonly string[] Names = { "", "Roadside stall", "Highway dhaba", "GT Road truck stop", "City food street", "Mountain tourist cafe" };

        // ground cover and backdrops get no ink outline (GDD: outlines only on things the player uses)
        static bool Outlined(string prop) =>
            prop != "GrassTuft" && prop != "Rock" && prop != "SnowPile" && prop != "Mountain" && prop != "ClayWall";

        public static Transform Build(int stage, Transform parent = null)
        {
            if (stage > 1 && !ArtLib.Load("art_stage" + stage)) return null;
            if (!ArtLib.Core) return null;
            var root = new GameObject("ArtStage" + stage).transform;
            if (parent) root.SetParent(parent, false);
            ArtLib.Obj("Env_Stage" + stage, root, false);
            var layout = ArtLib.Layout("Stage" + stage);
            if (layout == null) return root;
            foreach (var e in layout)
            {
                if (e.name == "chr") { SpawnCharacter(e, root); continue; }
                var go = ArtLib.Obj(e.name, root, Outlined(e.name));
                if (!go) continue;
                go.transform.localPosition = e.pos;
                go.transform.localRotation = Quaternion.Euler(0, e.rotY, 0);
                go.transform.localScale = Vector3.one * e.scale;
            }
            return root;
        }

        /// <summary>extra = look|pose|cups, e.g. "Rafi|walk|6" or "MotherA|sit|0".</summary>
        static void SpawnCharacter(ArtLib.Entry e, Transform root)
        {
            var parts = e.extra.Split('|');
            if (parts.Length < 2) return;
            var rig = Models.Character(new CharSpec { look = parts[0] }, root, parts[0]);
            rig.transform.localPosition = e.pos;
            rig.transform.localRotation = Quaternion.Euler(0, e.rotY, 0);
            switch (parts[1])
            {
                case "sit":
                    rig.sitting = true;
                    rig.legL.localRotation = rig.legR.localRotation = Quaternion.Euler(-85, 0, 0);
                    break;
                case "walk":
                    rig.legL.localRotation = Quaternion.Euler(-28, 0, 0);
                    rig.legR.localRotation = Quaternion.Euler(28, 0, 0);
                    rig.armL.localRotation = Quaternion.Euler(22, 0, 0);
                    rig.armR.localRotation = Quaternion.Euler(-22, 0, 0);
                    break;
                case "wave":
                    rig.armR.localRotation = Quaternion.Euler(0, 0, -150);
                    break;
            }
            int cups = parts.Length > 2 && int.TryParse(parts[2], out var n) ? n : 0;
            if (cups > 0)
            {
                rig.armL.localRotation = rig.armR.localRotation = Quaternion.Euler(-78, 0, 0);
                float y = 0;
                for (int i = 0; i < cups; i++)
                {
                    var cup = Models.Item(ItemId.Chai, rig.carryAnchor);
                    cup.transform.localPosition = new Vector3(0, y, 0);
                    y += Items.Height(ItemId.Chai);
                }
            }
        }
    }
}
