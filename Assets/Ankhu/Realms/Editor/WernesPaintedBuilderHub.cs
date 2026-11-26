using System;
using Ankhu.Realms.Core.Content;
using UnityEditor;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// The way into Wernes (docs/PLAY_ORDER.md): the terrace reunion with Anpu and the Memory House hub.
    /// Terrace layout: a rock ledge x 0..13 with a parapet at the back, Anpu at the edge x 10.8, steps cut down
    /// the cliff from x 13.2 eastward with grey figures sitting on them, the green fields of Wernes far below.
    /// Hub layout: a one-room house x 0..12, the false door in the back wall at x 6, two panels either side,
    /// a table set for one at x 2.2. Walk lane z -2..2, back wall z 2.9.
    /// Build only these two: -executeMethod Ankhu.Realms.EditorTools.RealmSceneBuilder.BuildWernesHubCli
    /// </summary>
    public static partial class RealmSceneBuilder
    {
        [MenuItem("Ankhu/Realms/Build Wernes Terrace and Hub")]
        public static void BuildWernesHubMenu() => BuildWernesHub();

        public static void BuildWernesHubCli()
        {
            try { BuildWernesHub(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void BuildWernesHub()
        {
            RealmKit.EnsureDirs();
            BuildTerraceScene();
            BuildHubScene();
            AddWernesToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Realms] Wernes terrace and hub built.");
        }

        private static void AddMark(Ctx ctx, string name, Vector3 pos)
        {
            ctx.director.marks.Add(new RealmSceneDirector.Mark { name = name, transform = Mark(ctx.world, name, pos) });
        }

        // ------------------------------------------------------------------ The terrace above Wernes

        private static void BuildTerraceScene()
        {
            Ctx ctx = Begin(WernesPaintedScripts.Terrace, new Color(0.42f, 0.36f, 0.34f), 0.006f, 0.6f);
            Transform w = ctx.world;
            Material rock = RealmKit.Sandstone;
            Material grey = RealmKit.Lit("GreyShade", new Color(0.52f, 0.52f, 0.54f), 0.05f);
            Material green = RealmKit.Lit("WernesGreen", new Color(0.30f, 0.46f, 0.20f), 0.1f);
            Material barley = RealmKit.Lit("WernesBarley", new Color(0.66f, 0.60f, 0.30f), 0.1f);

            // The ledge Ankhu walks on, and the cliff falling away in front of and behind it.
            Transform ledge = RealmKit.Group("Ledge", w);
            RealmKit.Box("Floor", ledge, new Vector3(6.6f, -0.2f, 0f), new Vector3(14.4f, 0.4f, 5.6f), RealmKit.Limestone);
            RealmKit.Box("CliffFront", ledge, new Vector3(6.6f, -10.4f, -2.9f), new Vector3(14.4f, 20f, 0.4f), rock, false);
            RealmKit.Box("CliffWest", ledge, new Vector3(-1.2f, 2.5f, 1.5f), new Vector3(1.6f, 5.4f, 3.2f), rock, false);
            RealmKit.Box("CliffWestTop", ledge, new Vector3(-0.8f, 5.6f, 2.2f), new Vector3(2.4f, 1.0f, 2.2f), rock, false);
            for (int i = 0; i < 6; i++)
                RealmKit.Box("Parapet" + i, ledge, new Vector3(0.9f + i * 2.1f, 0.35f, 2.55f), new Vector3(1.7f, 0.7f, 0.4f), rock, false);
            // A worn shrine niche in the cliff by the start, where the way out of Borrowed Dawn comes through.
            RealmKit.Box("Niche", ledge, new Vector3(-0.35f, 1.3f, 0f), new Vector3(0.2f, 2.6f, 1.4f), RealmKit.Shadow, false);
            RealmKit.Box("NicheLintel", ledge, new Vector3(-0.3f, 2.75f, 0f), new Vector3(0.3f, 0.3f, 1.8f), RealmKit.Limestone, false);

            // Steps cut down the cliff, going east and down toward the fields.
            Transform steps = RealmKit.Group("Steps", w);
            for (int i = 0; i < 12; i++)
                RealmKit.Box("Step" + i, steps, new Vector3(13.6f + i * 0.7f, -0.25f - i * 0.42f, -0.2f), new Vector3(0.7f, 0.5f, 3.2f), RealmKit.Limestone, false);
            RealmKit.Box("StepWall", steps, new Vector3(17.4f, -1.5f, 1.9f), new Vector3(8f, 4f, 0.6f), rock, false);
            // The grey figures who forgot. They sit with their heads down and never look up.
            Transform figures = RealmKit.Group("GreyFigures", w);
            for (int i = 0; i < 5; i++)
            {
                int s = 2 + i * 2;
                Vector3 seat = new Vector3(13.6f + s * 0.7f, -0.25f - s * 0.42f + 0.25f, -0.6f + (i % 2) * 1.0f);
                Transform f = RealmKit.Group("Figure" + i, figures, seat);
                RealmKit.Shape(PrimitiveType.Capsule, "Body", f, new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.45f, 0.4f), grey);
                RealmKit.Shape(PrimitiveType.Sphere, "Head", f, new Vector3(0.12f, 0.92f, 0f), Vector3.one * 0.26f, grey);
            }

            // The fields of Wernes far below: barley and green squares, canals, palms, a river.
            Transform far = RealmKit.Group("FarFields", w);
            RealmKit.Box("Plain", far, new Vector3(20f, -40f, 60f), new Vector3(260f, 0.2f, 120f), green, false);
            var rnd = new System.Random(29);
            for (int r = 0; r < 6; r++)
                for (int c = 0; c < 10; c++)
                {
                    if (rnd.NextDouble() < 0.45) continue;
                    RealmKit.Box("Plot" + r + "_" + c, far, new Vector3(-40f + c * 12f, -39.8f, 18f + r * 12f), new Vector3(10.5f, 0.1f, 10.5f), barley, false);
                }
            for (int c = 0; c < 4; c++)
                RealmKit.Box("Canal" + c, far, new Vector3(20f, -39.75f, 24f + c * 24f), new Vector3(240f, 0.1f, 1.2f), RealmKit.Lapis, false);
            RealmKit.Box("River", far, new Vector3(20f, -39.7f, 110f), new Vector3(260f, 0.1f, 9f), RealmKit.Lapis, false);
            for (int p = 0; p < 14; p++)
            {
                Vector3 at = new Vector3(-30f + p * 9f + (float)rnd.NextDouble() * 4f, -40f, 16f + (float)rnd.NextDouble() * 60f);
                RealmKit.Shape(PrimitiveType.Cylinder, "PalmTrunk" + p, far, at + new Vector3(0f, 3f, 0f), new Vector3(0.5f, 3f, 0.5f), RealmKit.Wood);
                RealmKit.Shape(PrimitiveType.Sphere, "PalmCrown" + p, far, at + new Vector3(0f, 6.4f, 0f), new Vector3(4f, 1.6f, 4f), RealmKit.Reed);
            }

            RealmKit.Brazier("NicheLamp", w, new Vector3(0.6f, 0f, 1.9f), 8f, 1.4f);
            RealmKit.Brazier("EdgeLamp", w, new Vector3(12.4f, 0f, 1.9f), 8f, 1.6f);
            RealmKit.Motes(w, new Vector3(7f, 2f, 0f), new Vector3(14f, 3f, 4f), new Color(1f, 0.9f, 0.7f, 0.55f), 70);

            RealmKit.Blocker(w, new Vector3(6.6f, 3f, -2.6f), new Vector3(14.4f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(6.6f, 3f, 2.6f), new Vector3(14.4f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(-0.1f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));
            RealmKit.Blocker(w, new Vector3(13.7f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));

            Cast(ctx, new Vector3(1.4f, 0f, -0.4f), 90f, new Vector3(10.8f, 0f, 0.6f));
            AddMark(ctx, "mark_steps", new Vector3(13.1f, 0f, -0.2f));

            Zone(ctx, "edge", new Vector3(10.4f, 1f, 0f), new Vector3(3.2f, 4f, 5f), -1, 1);

            Shot(ctx, "cam_terrace", new Vector3(6.2f, 4.8f, -17f), new Vector3(7.4f, 0.4f, 4f), 42f, new Vector3(4.5f, 3f, 0f), new Vector3(10f, 10f, 8f), 0.04f, 0);
            Shot(ctx, "cam_edge", new Vector3(8.2f, 3.4f, -9.5f), new Vector3(13.5f, -0.6f, 3f), 44f, new Vector3(11.6f, 3f, 0f), new Vector3(4.6f, 10f, 8f), 0.04f, 1);
            ApplyPaint(ctx, WernesPaintedScripts.Terrace, new[]
            {
                new Rect4(new Vector3(6.6f, 0.01f, 0f), new Vector2(14.4f, 5.6f)),
            });
            Finish(ctx, WernesPaintedScripts.Terrace);
        }

        // ------------------------------------------------------------------ The Memory House

        private static readonly string[] PanelLetters = { "a", "b", "c", "d" };
        private static readonly float[] PanelX = { 3.5f, 4.6f, 7.4f, 8.5f };
        // Each panel shows the memory it was earned by: the falcon over water, a shabti, a door, the birth goddess.
        private static readonly string[] PanelSigns = { "G5 N35", "A53", "O31", "F31" };

        private static void BuildHubScene()
        {
            Ctx ctx = Begin(WernesPaintedScripts.Hub, new Color(0.10f, 0.08f, 0.09f), 0.008f, 0.5f);
            Transform w = ctx.world;
            Material brick = RealmKit.Lit("MudBrick", new Color(0.46f, 0.34f, 0.24f), 0.1f, 0f,
                RealmKit.StoneTexture("mudbrick", new Color(0.34f, 0.24f, 0.16f), new Color(0.56f, 0.42f, 0.30f), 1.5f, 71), 1.5f);
            Material plaster = RealmKit.Lit("Plaster", new Color(0.86f, 0.80f, 0.66f), 0.1f);
            Material linen = RealmKit.Lit("Linen", new Color(0.60f, 0.53f, 0.42f), 0.05f);
            Color gold = new Color(1f, 0.8f, 0.42f, 1f);
            Color faint = new Color(0.55f, 0.38f, 0.22f, 0.45f);

            // ------------------------------------------------------------ the house
            Transform house = RealmKit.Group("House", w);
            RealmKit.Box("Floor", house, new Vector3(6f, -0.2f, 0.2f), new Vector3(12.4f, 0.4f, 5.4f), RealmKit.Limestone);
            RealmKit.Box("BackWall", house, new Vector3(6f, 1.9f, 2.9f), new Vector3(12.4f, 3.8f, 0.5f), brick);
            RealmKit.Box("WestWallBack", house, new Vector3(-0.25f, 1.9f, 2.0f), new Vector3(0.5f, 3.8f, 2.2f), brick);
            RealmKit.Box("WestWallFront", house, new Vector3(-0.25f, 1.9f, -1.75f), new Vector3(0.5f, 3.8f, 1.7f), brick);
            RealmKit.Box("WestLintel", house, new Vector3(-0.25f, 3.3f, 0f), new Vector3(0.5f, 1.0f, 1.8f), brick);
            RealmKit.Box("EastWall", house, new Vector3(12.25f, 1.9f, 0.2f), new Vector3(0.5f, 3.8f, 5.8f), brick);
            RealmKit.Box("FrontStub", house, new Vector3(6f, 0.25f, -2.75f), new Vector3(12.9f, 0.5f, 0.4f), brick, false);
            RealmKit.Box("RoofBeams", house, new Vector3(6f, 3.9f, 2.0f), new Vector3(12.9f, 0.18f, 2.2f), RealmKit.Wood, false);
            for (int b = 0; b < 7; b++)
                RealmKit.Box("Joist" + b, house, new Vector3(0.6f + b * 1.8f, 3.85f, 0.6f), new Vector3(0.14f, 0.14f, 4.6f), RealmKit.Wood, false);
            RealmKit.Box("Plaster", house, new Vector3(6f, 1.7f, 2.64f), new Vector3(11.6f, 3.0f, 0.02f), plaster, false);
            RealmKit.Box("FriezeRed", house, new Vector3(6f, 3.3f, 2.62f), new Vector3(11.6f, 0.12f, 0.02f), RealmKit.Ochre, false);
            RealmKit.Box("FriezeBlue", house, new Vector3(6f, 3.42f, 2.62f), new Vector3(11.6f, 0.08f, 0.02f), RealmKit.Lapis, false);
            RealmKit.Box("Dado", house, new Vector3(6f, 0.2f, 2.62f), new Vector3(11.6f, 0.4f, 0.02f), RealmKit.Ochre, false);
            // Daylight through the west doorway, the way in from the terrace.
            RealmKit.Box("DayBeyond", w, new Vector3(-1.6f, 1.6f, 0f), new Vector3(0.1f, 3.2f, 2.2f), plaster, false);

            // A table set for one: a loaf, a jar, one cup. Nobody has eaten here.
            Transform table = RealmKit.Group("Table", house, new Vector3(2.2f, 0f, 1.7f));
            RealmKit.Box("Top", table, new Vector3(0f, 0.7f, 0f), new Vector3(1.4f, 0.08f, 0.7f), RealmKit.Wood);
            RealmKit.Box("Legs", table, new Vector3(0f, 0.33f, 0f), new Vector3(1.2f, 0.66f, 0.5f), RealmKit.Shadow, false);
            RealmKit.Shape(PrimitiveType.Sphere, "Loaf", table, new Vector3(-0.35f, 0.8f, 0f), new Vector3(0.3f, 0.14f, 0.22f), RealmKit.Ochre);
            RealmKit.Shape(PrimitiveType.Cylinder, "Cup", table, new Vector3(0.1f, 0.8f, -0.05f), new Vector3(0.12f, 0.06f, 0.12f), RealmKit.Lapis);
            RealmKit.Shape(PrimitiveType.Sphere, "Jar", table, new Vector3(0.45f, 0.9f, 0.1f), new Vector3(0.24f, 0.36f, 0.24f), RealmKit.Ochre);
            RealmKit.Box("Stool", house, new Vector3(2.2f, 0.28f, 0.9f), new Vector3(0.6f, 0.08f, 0.5f), RealmKit.Wood);
            RealmKit.Box("Mat", house, new Vector3(10.4f, 0.02f, 1.2f), new Vector3(2.2f, 0.03f, 1.4f), RealmKit.ReedDry, false);
            RealmKit.Box("Chest", house, new Vector3(11.3f, 0.35f, 2.2f), new Vector3(1.0f, 0.7f, 0.6f), RealmKit.Wood);
            RealmKit.Box("Hanging", house, new Vector3(10.4f, 2.0f, 2.58f), new Vector3(1.4f, 2.2f, 0.02f), linen, false);

            // ------------------------------------------------------------ the false door
            Transform fd = RealmKit.Group("FalseDoor", w, new Vector3(6f, 0f, 2.6f));
            for (int k = 0; k < 3; k++)
            {
                float wd = 2.6f - k * 0.5f, ht = 3.0f - k * 0.3f, z = -0.06f - k * 0.06f;
                RealmKit.Box("Frame" + k, fd, new Vector3(0f, ht * 0.5f, z), new Vector3(wd, ht, 0.08f), RealmKit.Limestone, false);
            }
            RealmKit.Box("Lintel", fd, new Vector3(0f, 2.75f, -0.26f), new Vector3(2.8f, 0.36f, 0.14f), RealmKit.Sandstone, false);
            // His name over the door, the one word it opens for.
            DGlyph("LintelName", fd, new Vector3(0f, 2.75f, -0.36f), Vector3.zero, "S34 N35 Aa1 G43", 0.24f, false, gold);
            Transform slab = RealmKit.Group("Slab", fd, new Vector3(0f, 1.05f, -0.24f));
            RealmKit.Box("Stone", slab, Vector3.zero, new Vector3(1.0f, 2.1f, 0.08f), RealmKit.Sandstone, false);
            // What lies beyond once it gives way: warm light from a field.
            Transform beyond = RealmKit.Group("Beyond", fd, new Vector3(0f, 1.05f, -0.2f));
            PQuad(beyond, "Light", FlipMat("fx_glow_hub", FxTex("fx_glow"), new Color(1f, 0.82f, 0.5f, 1f), 1.6f, 0.25f, 1.1f, 0.05f, true), Vector3.zero, new Vector2(1.6f, 2.8f));
            PQuad(beyond, "Sparks", FlipMat("fx_glint_hub", FxTex("fx_glint"), new Color(1f, 0.9f, 0.6f, 1f), 1.3f, 0.5f, 2.2f, 0.2f, true), new Vector3(0f, 0.2f, -0.05f), new Vector2(1.2f, 1.2f));
            Light fdLight = InnerGlow(fd, new Vector3(0f, 1.3f, -0.9f), 0f, 7f);
            fdLight.color = new Color(1f, 0.78f, 0.45f);
            RealmObjectView fdv = View(ctx, fd, "fdoor", new Vector3(0f, 2.3f, -0.4f));
            var shut = Pose(fdv, "shut", 0.6f);
            Part(shut, slab, new Vector3(0f, 1.05f, -0.24f), Vector3.zero, Vector3.one);
            shut.hide.Add(beyond.gameObject); shut.lights.Add(fdLight); shut.lightIntensities.Add(0.2f);
            // "Gives way like water": the slab sinks into the threshold and the light comes up behind it.
            var open = Pose(fdv, "open", 2.6f);
            Part(open, slab, new Vector3(0f, 0.02f, -0.24f), Vector3.zero, new Vector3(1f, 0.02f, 1f));
            open.show.Add(beyond.gameObject); open.lights.Add(fdLight); open.lightIntensities.Add(2.6f);

            // ------------------------------------------------------------ four panels that light as rooms are done
            for (int i = 0; i < 4; i++)
            {
                string l = PanelLetters[i];
                Transform panel = RealmKit.Group("Panel_" + l.ToUpperInvariant(), w, new Vector3(PanelX[i], 0f, 2.56f));
                RealmKit.Box("Frame", panel, new Vector3(0f, 1.8f, 0f), new Vector3(0.9f, 1.3f, 0.04f), RealmKit.Ochre, false);
                RealmKit.Box("Field", panel, new Vector3(0f, 1.8f, -0.03f), new Vector3(0.76f, 1.16f, 0.02f), plaster, false);
                DGlyph("Ghost", panel, new Vector3(0f, 1.8f, -0.06f), Vector3.zero, PanelSigns[i], 0.32f, false, faint);
                Transform lit = RealmKit.Group("Lit", panel, new Vector3(0f, 1.8f, -0.08f));
                PQuad(lit, "Glow", FlipMat("fx_glow_panel_" + l, FxTex("fx_glow"), new Color(1f, 0.76f, 0.4f, 1f), 1.2f, 0.18f, 0.9f + i * 0.15f, 0.04f, true), Vector3.zero, new Vector2(1.5f, 1.9f));
                DGlyph("Sign", lit, new Vector3(0f, 0f, -0.02f), Vector3.zero, PanelSigns[i], 0.36f, false, gold);
                Light pl = InnerGlow(panel, new Vector3(0f, 1.8f, -0.8f), 0f, 3.5f);
                pl.color = new Color(1f, 0.8f, 0.5f);
                RealmObjectView pv = View(ctx, panel, "panel_" + l, new Vector3(0f, 2.6f, -0.3f));
                var dark = Pose(pv, "dark", 0.5f); dark.hide.Add(lit.gameObject); dark.lights.Add(pl); dark.lightIntensities.Add(0f);
                var on = Pose(pv, "lit", 2.2f); on.show.Add(lit.gameObject); on.lights.Add(pl); on.lightIntensities.Add(1.2f);
            }

            RealmKit.Brazier("TableLamp", w, new Vector3(0.7f, 0f, 2.1f), 7f, 1.3f);
            RealmKit.Brazier("DoorLampW", w, new Vector3(2.6f, 0f, 2.2f), 7f, 1.2f);
            RealmKit.Brazier("DoorLampE", w, new Vector3(9.6f, 0f, 2.2f), 7f, 1.2f);
            RealmKit.Motes(w, new Vector3(6f, 1.8f, 1f), new Vector3(11f, 3f, 4f), new Color(1f, 0.85f, 0.6f, 0.6f), 90);

            RealmKit.Blocker(w, new Vector3(6f, 3f, -2.5f), new Vector3(12.4f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(6f, 3f, 2.4f), new Vector3(12.4f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(0.2f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));
            RealmKit.Blocker(w, new Vector3(11.9f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));

            Cast(ctx, new Vector3(1.2f, 0f, -0.6f), 90f, new Vector3(7.4f, 0f, 1.0f));
            AddMark(ctx, "mark_door", new Vector3(7.2f, 0f, 1.0f));

            Zone(ctx, "door", new Vector3(6f, 1f, 0.6f), new Vector3(2.6f, 4f, 4.4f), -1, 2);

            Shot(ctx, "cam_house", new Vector3(6f, 4.4f, -14.5f), new Vector3(6f, 1.7f, 1f), 40f, new Vector3(3f, 3f, 0f), new Vector3(6.4f, 10f, 8f), 0.04f, 0);
            Shot(ctx, "cam_door", new Vector3(6.6f, 2.6f, -7.6f), new Vector3(6f, 1.6f, 2.4f), 42f, new Vector3(8.4f, 3f, 0f), new Vector3(7.6f, 10f, 8f), 0.04f, 1);
            ApplyPaint(ctx, WernesPaintedScripts.Hub, new[]
            {
                new Rect4(new Vector3(6f, 0.01f, 0.2f), new Vector2(12.4f, 5.4f)),
            });
            Finish(ctx, WernesPaintedScripts.Hub);
        }
    }
}
