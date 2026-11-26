using Ankhu.Realms.Core.Content;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Wernes Room D, The Birth Bricks. The one-room house again, earlier: the night Ankhu was born. On the back wall
    /// the painted Meskhenet sleeps with an ankh in her fist. A dry basin, a tied midwife's basket and a quail chick
    /// in a reed snare each hold one sound-sign of his name. Four birth bricks stand in a row under an empty
    /// cartouche. Laid in reading order the bricks wake, the mother's cry sounds, and a seated man appears on the mat
    /// where the child lay. The cry fades and takes him with it unless it is answered (echo).
    /// Layout: goddess x 1.8, basin x 4.2, bricks x 6.4 / 7.6 / 8.8 / 10.0, seat x 11.2, mat x 13.2,
    /// basket x 14.6, snare x 16.0. Walk lane z -2..2, back wall z 2.9.
    /// </summary>
    public static partial class RealmSceneBuilder
    {
        private static readonly string[] BrickSignCodes = { "S34", "N35", "Aa1", "G43" };

        private static void BuildBricksD()
        {
            Ctx ctx = Begin(WernesPaintedScripts.BricksD, new Color(0.08f, 0.07f, 0.11f), 0.010f, 0.45f);
            Transform w = ctx.world;
            Ground(w, 140f, RealmKit.Mud);
            Dunes(w, 17, 70f);
            Material brick = RealmKit.Lit("MudBrick", new Color(0.46f, 0.34f, 0.24f), 0.1f, 0f,
                RealmKit.StoneTexture("mudbrick", new Color(0.34f, 0.24f, 0.16f), new Color(0.56f, 0.42f, 0.30f), 1.5f, 71), 1.5f);
            Material plaster = RealmKit.Lit("Plaster", new Color(0.86f, 0.80f, 0.66f), 0.1f);
            Material linen = RealmKit.Lit("Linen", new Color(0.60f, 0.53f, 0.42f), 0.05f);
            Material birthClay = RealmKit.Lit("BirthClay", new Color(0.58f, 0.44f, 0.30f), 0.08f, 0f,
                RealmKit.StoneTexture("birthclay", new Color(0.44f, 0.32f, 0.22f), new Color(0.66f, 0.52f, 0.38f), 0.8f, 89), 1f);
            Material wakeGlow = RealmKit.Emissive("BrickWake", new Color(1f, 0.8f, 0.45f), new Color(4.6f, 3.0f, 1.1f));
            Material cryBead = RealmKit.Emissive("CryBead", new Color(1f, 0.85f, 0.55f), new Color(2.2f, 1.15f, 0.3f));
            Material echoBead = RealmKit.Emissive("EchoBead", new Color(0.5f, 0.95f, 1f), new Color(0.35f, 1.4f, 2.2f));
            Material misreadGlow = RealmKit.Emissive("MisreadGlow", new Color(0.5f, 0.55f, 0.9f), new Color(0.8f, 1.0f, 2.6f));
            Material brickFace = RealmKit.Lit("BrickFace", new Color(0.17f, 0.10f, 0.07f), 0.1f);
            Material shrine = RealmKit.Lit("ShrineNight", new Color(0.06f, 0.07f, 0.13f), 0.1f);
            Color gold = new Color(1f, 0.8f, 0.42f, 1f);
            Color faint = new Color(0.55f, 0.38f, 0.22f, 0.45f);

            // ------------------------------------------------------------ the house
            Transform house = RealmKit.Group("House", w);
            RealmKit.Box("Floor", house, new Vector3(8.5f, 0.03f, 0.2f), new Vector3(17.4f, 0.06f, 5.4f), RealmKit.Limestone, false);
            RealmKit.Box("BackWall", house, new Vector3(8.5f, 1.9f, 2.9f), new Vector3(17.4f, 3.8f, 0.5f), brick);
            RealmKit.Box("WestWall", house, new Vector3(-0.25f, 1.9f, 0.2f), new Vector3(0.5f, 3.8f, 5.8f), brick);
            RealmKit.Box("EastWallBack", house, new Vector3(17.25f, 1.9f, 2.0f), new Vector3(0.5f, 3.8f, 2.2f), brick);
            RealmKit.Box("EastWallFront", house, new Vector3(17.25f, 1.9f, -1.75f), new Vector3(0.5f, 3.8f, 1.7f), brick);
            RealmKit.Box("EastLintel", house, new Vector3(17.25f, 3.3f, 0f), new Vector3(0.5f, 1.0f, 1.8f), brick);
            RealmKit.Box("FrontStub", house, new Vector3(8.5f, 0.25f, -2.75f), new Vector3(17.9f, 0.5f, 0.4f), brick, false);
            RealmKit.Box("RoofBeams", house, new Vector3(8.5f, 3.9f, 2.0f), new Vector3(17.9f, 0.18f, 2.2f), RealmKit.Wood, false);
            for (int b = 0; b < 10; b++)
                RealmKit.Box("Joist" + b, house, new Vector3(0.6f + b * 1.8f, 3.85f, 0.6f), new Vector3(0.14f, 0.14f, 4.6f), RealmKit.Wood, false);
            RealmKit.Box("Plaster", house, new Vector3(8.5f, 1.7f, 2.64f), new Vector3(16.6f, 3.0f, 0.02f), plaster, false);
            // A painted frieze: a band of red ochre and a band of lapis, the way a good house was finished.
            RealmKit.Box("FriezeRed", house, new Vector3(8.5f, 3.3f, 2.62f), new Vector3(16.6f, 0.12f, 0.02f), RealmKit.Ochre, false);
            RealmKit.Box("FriezeBlue", house, new Vector3(8.5f, 3.42f, 2.62f), new Vector3(16.6f, 0.08f, 0.02f), RealmKit.Lapis, false);
            RealmKit.Box("Dado", house, new Vector3(8.5f, 0.2f, 2.62f), new Vector3(16.6f, 0.4f, 0.02f), RealmKit.Ochre, false);
            // Night through the east doorway.
            RealmKit.Box("NightBeyond", w, new Vector3(18.6f, 1.6f, 0f), new Vector3(0.1f, 3.2f, 2.2f), RealmKit.Lapis, false);
            RealmKit.Motes(w, new Vector3(19.4f, 2f, 0f), new Vector3(2f, 3f, 2.4f), new Color(0.7f, 0.8f, 1f, 0.6f), 30);

            // Linen hung to make the birth corner, lifting in the draught from the door.
            for (int k = 0; k < 3; k++)
            {
                Transform hang = RealmKit.Group("Hanging" + k, house, new Vector3(12.0f + k * 1.0f, 3.6f, 2.5f));
                RealmKit.Box("Cloth", hang, new Vector3(0f, -0.9f, -0.02f), new Vector3(0.9f, 1.8f, 0.02f), linen, false);
                var hs = hang.gameObject.AddComponent<Sway>(); hs.axis = Vector3.right; hs.degrees = 7f + k * 2f; hs.speed = 0.9f + k * 0.25f;
            }
            // A stool for the midwife, water jars, a lamp on a cord over the mat.
            RealmKit.Box("Stool", house, new Vector3(12.4f, 0.28f, 1.9f), new Vector3(0.6f, 0.08f, 0.5f), RealmKit.Wood);
            RealmKit.Box("StoolLegs", house, new Vector3(12.4f, 0.12f, 1.9f), new Vector3(0.5f, 0.24f, 0.4f), RealmKit.Shadow, false);
            RealmKit.Shape(PrimitiveType.Sphere, "JarA", house, new Vector3(0.7f, 0.45f, 2.2f), new Vector3(0.55f, 0.8f, 0.55f), RealmKit.Ochre);
            RealmKit.Shape(PrimitiveType.Sphere, "JarB", house, new Vector3(5.3f, 0.35f, 2.3f), new Vector3(0.42f, 0.6f, 0.42f), RealmKit.Ochre);
            Transform lampT = RealmKit.Group("MatLamp", house, new Vector3(13.2f, 3.4f, 0.8f));
            Transform lampHang = RealmKit.Group("Hang", lampT);
            RealmKit.Box("Cord", lampHang, new Vector3(0f, -0.25f, 0f), new Vector3(0.02f, 0.5f, 0.02f), RealmKit.ReedDry, false);
            RealmKit.Shape(PrimitiveType.Sphere, "Flame", lampHang, new Vector3(0f, -0.55f, 0f), Vector3.one * 0.14f, RealmKit.GoldGlow);
            InnerGlow(lampHang, new Vector3(0f, -0.55f, -0.3f), 1.3f, 7f);
            var lsw = lampHang.gameObject.AddComponent<Sway>(); lsw.degrees = 8f; lsw.speed = 1.4f;

            // ------------------------------------------------------------ Meskhenet, painted on the wall
            Transform god = RealmKit.Group("Meskhenet", w, new Vector3(1.8f, 0f, 2.6f));
            RealmKit.Box("Panel", god, new Vector3(0f, 1.75f, -0.02f), new Vector3(2.4f, 2.9f, 0.02f), shrine, false);
            RealmKit.Box("PanelEdge", god, new Vector3(0f, 1.75f, -0.01f), new Vector3(2.56f, 3.06f, 0.01f), RealmKit.Ochre, false);
            DGlyph("Figure", god, new Vector3(0f, 1.55f, -0.05f), Vector3.zero, "B1", 1.9f, false, new Color(0.45f, 0.65f, 1f, 1f));
            DGlyph("Emblem", god, new Vector3(0.05f, 2.85f, -0.05f), Vector3.zero, "F31", 0.42f, false, new Color(1f, 0.5f, 0.3f, 1f));
            DGlyph("NameCol", god, new Vector3(-0.95f, 1.8f, -0.05f), Vector3.zero, "F31 S29 Aa1 N35 X1", 0.24f, true, faint);
            Transform lid = RealmKit.Group("Lid", god, new Vector3(0.12f, 2.42f, -0.07f));
            RealmKit.Box("Line", lid, Vector3.zero, new Vector3(0.18f, 0.025f, 0.01f), RealmKit.Shadow, false);
            Transform eye = RealmKit.Group("Eye", god, new Vector3(0.12f, 2.42f, -0.07f));
            RealmKit.Shape(PrimitiveType.Sphere, "Iris", eye, Vector3.zero, new Vector3(0.14f, 0.08f, 0.02f), RealmKit.EyeGlow);
            Transform fist = RealmKit.Group("Fist", god, new Vector3(0.55f, 1.35f, -0.08f));
            RealmKit.Shape(PrimitiveType.Sphere, "Hand", fist, Vector3.zero, new Vector3(0.22f, 0.18f, 0.06f), RealmKit.Ochre);
            Light godLight = InnerGlow(god, new Vector3(0f, 1.9f, -0.9f), 0f, 5f);
            godLight.color = new Color(1f, 0.82f, 0.5f);
            RealmObjectView gv = View(ctx, god, "goddess", new Vector3(0f, 2.9f, -0.4f));
            var gAsleep = Pose(gv, "asleep", 0.6f); gAsleep.show.Add(lid.gameObject); gAsleep.hide.Add(eye.gameObject);
            Part(gAsleep, fist, fist.localPosition, Vector3.zero); gAsleep.lights.Add(godLight); gAsleep.lightIntensities.Add(0f);
            var gAwake = Pose(gv, "awake", 2.2f); gAwake.hide.Add(lid.gameObject); gAwake.show.Add(eye.gameObject);
            Part(gAwake, fist, fist.localPosition + new Vector3(0.08f, -0.05f, 0f), new Vector3(0f, 0f, -40f), new Vector3(1.3f, 0.7f, 1f));
            gAwake.lights.Add(godLight); gAwake.lightIntensities.Add(1.4f);

            // ------------------------------------------------------------ the dry basin
            Transform basin = RealmKit.Group("Basin", w, new Vector3(4.2f, 0f, 1.3f));
            RealmKit.Shape(PrimitiveType.Cylinder, "Bowl", basin, new Vector3(0f, 0.25f, 0f), new Vector3(1.1f, 0.25f, 1.1f), RealmKit.Ochre, true);
            RealmKit.Shape(PrimitiveType.Cylinder, "Hollow", basin, new Vector3(0f, 0.46f, 0f), new Vector3(0.9f, 0.05f, 0.9f), RealmKit.Shadow);
            DGlyph("Scratch", basin, new Vector3(0f, 0.28f, -0.56f), Vector3.zero, "N35", 0.3f, false, faint);
            Transform cracks = RealmKit.Group("Cracks", basin);
            for (int c = 0; c < 3; c++)
                RealmKit.Box("Crack" + c, cracks, new Vector3(-0.2f + c * 0.2f, 0.49f, 0.05f * c), new Vector3(0.02f, 0.01f, 0.6f), RealmKit.Mud, false, new Vector3(0f, 25f - c * 30f, 0f));
            Transform fill = RealmKit.Group("Fill", basin, new Vector3(0f, 0.3f, 0f));
            RealmKit.Shape(PrimitiveType.Cylinder, "Sheet", fill, Vector3.zero, new Vector3(0.86f, 0.02f, 0.86f), RealmKit.Water)
                .AddComponent<WaterMotion>().amplitude = 0.01f;
            Light basinLight = InnerGlow(basin, new Vector3(0f, 0.9f, -0.6f), 0f, 4f);
            basinLight.color = new Color(0.55f, 0.8f, 1f);
            RealmObjectView bv = View(ctx, basin, "basin", new Vector3(0f, 1.1f, -0.4f));
            var bDry = Pose(bv, "dry", 0.6f); bDry.show.Add(cracks.gameObject); Part(bDry, fill, new Vector3(0f, 0.3f, 0f), Vector3.zero, new Vector3(0.6f, 1f, 0.6f));
            bDry.lights.Add(basinLight); bDry.lightIntensities.Add(0f);
            var bFull = Pose(bv, "full", 2.6f); bFull.hide.Add(cracks.gameObject); Part(bFull, fill, new Vector3(0f, 0.48f, 0f), Vector3.zero, Vector3.one);
            bFull.lights.Add(basinLight); bFull.lightIntensities.Add(1.2f);

            // ------------------------------------------------------------ the midwife's basket
            Transform basket = RealmKit.Group("Basket", w, new Vector3(14.6f, 0f, 1.4f));
            RealmKit.Shape(PrimitiveType.Cylinder, "Body", basket, new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.3f, 0.7f), RealmKit.ReedDry, true);
            for (int r = 0; r < 4; r++)
                RealmKit.Shape(PrimitiveType.Cylinder, "Weave" + r, basket, new Vector3(0f, 0.08f + r * 0.14f, 0f), new Vector3(0.72f, 0.012f, 0.72f), RealmKit.Wood);
            Transform bLid = RealmKit.Group("Lid", basket, new Vector3(0f, 0.62f, 0f));
            RealmKit.Shape(PrimitiveType.Cylinder, "Top", bLid, Vector3.zero, new Vector3(0.76f, 0.04f, 0.76f), RealmKit.ReedDry);
            RealmKit.Shape(PrimitiveType.Sphere, "Handle", bLid, new Vector3(0f, 0.07f, 0f), new Vector3(0.14f, 0.08f, 0.14f), RealmKit.Wood);
            Transform knot = RealmKit.Group("Knot", basket, new Vector3(0f, 0.5f, -0.36f));
            RealmKit.Shape(PrimitiveType.Sphere, "Tie", knot, Vector3.zero, Vector3.one * 0.1f, RealmKit.Ochre);
            RealmKit.Box("Cord", knot, new Vector3(0f, 0f, 0.36f), new Vector3(0.74f, 0.03f, 0.03f), RealmKit.Ochre, false);
            Light basketLight = InnerGlow(basket, new Vector3(0f, 0.9f, -0.5f), 0f, 3.5f);
            basketLight.color = new Color(1f, 0.7f, 0.5f);
            RealmObjectView kv = View(ctx, basket, "basket", new Vector3(0f, 1.0f, -0.4f));
            var kShut = Pose(kv, "shut", 0.6f); kShut.show.Add(knot.gameObject); Part(kShut, bLid, new Vector3(0f, 0.62f, 0f), Vector3.zero);
            kShut.lights.Add(basketLight); kShut.lightIntensities.Add(0f);
            var kOpen = Pose(kv, "open", 1.6f); kOpen.hide.Add(knot.gameObject); Part(kOpen, bLid, new Vector3(0.48f, 0.42f, 0.1f), new Vector3(0f, 0f, -70f));
            kOpen.lights.Add(basketLight); kOpen.lightIntensities.Add(1.1f);

            // ------------------------------------------------------------ the reed snare (its view is the quail's)
            Transform snare = RealmKit.Group("Snare", w, new Vector3(16.0f, 0f, 0.9f));
            var snareBars = new Transform[5];
            for (int s = 0; s < 5; s++)
            {
                snareBars[s] = RealmKit.Group("Bar" + s, snare, new Vector3(-0.4f + s * 0.2f, 0f, 0f));
                RealmKit.Box("Reed", snareBars[s], new Vector3(0f, 0.35f, 0f), new Vector3(0.04f, 0.7f, 0.04f), RealmKit.ReedDry, false);
            }
            Transform snareTie = RealmKit.Group("Tie", snare, new Vector3(0f, 0.7f, 0f));
            RealmKit.Box("Band", snareTie, Vector3.zero, new Vector3(0.9f, 0.04f, 0.06f), RealmKit.Ochre, false);

            // ------------------------------------------------------------ the held sign, above Ankhu's head
            Cast(ctx, new Vector3(2.6f, 0f, -0.6f), 90f, new Vector3(0.9f, 0f, -1.1f));
            Transform held = RealmKit.Group("HeldSign", ctx.ankhu.transform, new Vector3(0f, 2.2f, 0f));
            Transform heldBob = RealmKit.Group("Bob", held);
            var hb = heldBob.gameObject.AddComponent<Bob>(); hb.amplitude = 0.05f; hb.roll = 3f;
            Transform heldFace = RealmKit.Group("Face", heldBob);
            heldFace.gameObject.AddComponent<CardFacing>();
            Light heldLight = InnerGlow(heldBob, new Vector3(0f, 0f, -0.4f), 0.8f, 2.5f);
            heldLight.color = new Color(1f, 0.82f, 0.5f);

            // ------------------------------------------------------------ the four sound-signs
            SignView(ctx, w, heldFace, "ankh", "S34", "hidden", new Vector3(2.35f, 1.35f, 2.4f), new Vector3(2.3f, 0.7f, 1.6f), 0.01f, gold);
            SignView(ctx, w, heldFace, "water", "N35", "hidden", new Vector3(4.2f, 0.45f, 1.3f), new Vector3(4.2f, 1.25f, 1.3f), 0.01f, new Color(0.6f, 0.85f, 1f, 1f));
            SignView(ctx, w, heldFace, "placenta", "Aa1", "hidden", new Vector3(14.6f, 0.5f, 1.4f), new Vector3(14.6f, 1.3f, 1.2f), 0.01f, gold);
            Transform quail = SignView(ctx, w, heldFace, "quail", "G43", "snared", new Vector3(16.0f, 0.3f, 0.9f), new Vector3(15.6f, 0.4f, 0.2f), 0.7f, gold);
            // The quail's own view also carries the snare falling apart.
            RealmObjectView qv = quail.GetComponent<RealmObjectView>();
            foreach (var p in qv.poses)
            {
                bool caught = p.state == "snared";
                for (int s = 0; s < 5; s++)
                    Part(p, snareBars[s], snareBars[s].localPosition, caught ? Vector3.zero : new Vector3(0f, 0f, (s < 2 ? 1f : -1f) * (70f + s * 6f)));
                Part(p, snareTie, caught ? new Vector3(0f, 0.7f, 0f) : new Vector3(0.1f, 0.03f, -0.2f), caught ? Vector3.zero : new Vector3(0f, 30f, 0f));
            }

            // ------------------------------------------------------------ the empty cartouche over the bricks, and its seat
            Transform cart = RealmKit.Group("Cartouche", w, new Vector3(8.2f, 2.6f, 2.6f));
            RealmKit.Box("RopeTop", cart, new Vector3(0f, 0.45f, -0.03f), new Vector3(4.4f, 0.07f, 0.03f), RealmKit.Gold, false);
            RealmKit.Box("RopeLow", cart, new Vector3(0f, -0.45f, -0.03f), new Vector3(4.4f, 0.07f, 0.03f), RealmKit.Gold, false);
            RealmKit.Box("CapL", cart, new Vector3(-2.2f, 0f, -0.03f), new Vector3(0.07f, 0.9f, 0.03f), RealmKit.Gold, false);
            RealmKit.Box("CapR", cart, new Vector3(2.2f, 0f, -0.03f), new Vector3(0.07f, 0.9f, 0.03f), RealmKit.Gold, false);
            RealmKit.Box("Tie", cart, new Vector3(2.32f, 0f, -0.03f), new Vector3(0.1f, 1.1f, 0.03f), RealmKit.Gold, false);
            RealmKit.Box("Inside", cart, new Vector3(0f, 0f, -0.01f), new Vector3(4.36f, 0.84f, 0.01f), brickFace, false);
            DGlyph("Ghost", cart, new Vector3(0f, 0f, -0.05f), Vector3.zero, "S34 N35 Aa1 G43", 0.6f, false, new Color(0.5f, 0.35f, 0.2f, 0.18f));
            Light cartLight = InnerGlow(cart, new Vector3(0f, 0f, -0.9f), 0f, 5f);
            cartLight.color = new Color(1f, 0.8f, 0.45f);

            Transform seat = RealmKit.Group("Seat", w, new Vector3(11.2f, 0f, 2.6f));
            RealmKit.Box("Niche", seat, new Vector3(0f, 2.6f, -0.02f), new Vector3(0.8f, 1.0f, 0.02f), RealmKit.Shadow, false);
            RealmKit.Box("Ledge", seat, new Vector3(0f, 2.05f, -0.12f), new Vector3(0.9f, 0.08f, 0.24f), RealmKit.Limestone, false);
            Transform seatMan = RealmKit.Group("Man", seat, new Vector3(0f, 2.6f, -0.06f));
            DGlyph("A1", seatMan, Vector3.zero, Vector3.zero, "A1", 0.7f, false, gold);
            Light seatLight = InnerGlow(seat, new Vector3(0f, 2.4f, -0.9f), 0f, 4f);
            seatLight.color = new Color(1f, 0.82f, 0.5f);
            RealmObjectView sv = View(ctx, seat, "seat", new Vector3(0f, 3.2f, -0.4f));
            var sBare = Pose(sv, "bare", 0.4f); sBare.hide.Add(seatMan.gameObject); Part(sBare, seatMan, seatMan.localPosition, Vector3.zero, Vector3.one * 0.01f);
            sBare.lights.Add(seatLight); sBare.lightIntensities.Add(0f); sBare.lights.Add(cartLight); sBare.lightIntensities.Add(0f);
            var sMan = Pose(sv, "man", 3.0f); sMan.show.Add(seatMan.gameObject); Part(sMan, seatMan, seatMan.localPosition, Vector3.zero, Vector3.one);
            sMan.lights.Add(seatLight); sMan.lightIntensities.Add(2.4f); sMan.lights.Add(cartLight); sMan.lightIntensities.Add(2.8f);

            // ------------------------------------------------------------ the four birth bricks
            Transform row = RealmKit.Group("Bricks", w);
            var bodies = new Transform[4];
            var brickLights = new Light[4];
            var wakeStrips = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                Transform slot = RealmKit.Group("Brick" + (i + 1), row, new Vector3(6.4f + i * 1.2f, 0f, 1.7f));
                RealmKit.Box("Footing", slot, new Vector3(0f, 0.02f, 0f), new Vector3(1.0f, 0.04f, 0.5f), RealmKit.Shadow, false);
                Transform body = RealmKit.Group("Body", slot);
                bodies[i] = body;
                RealmKit.Box("Clay", body, new Vector3(0f, 0.34f, 0f), new Vector3(0.9f, 0.64f, 0.34f), birthClay);
                RealmKit.Box("Face", body, new Vector3(0f, 0.34f, -0.175f), new Vector3(0.78f, 0.52f, 0.01f), brickFace, false);
                DGlyph("Mother", body, new Vector3(-0.28f, 0.52f, -0.185f), Vector3.zero, "B1", 0.14f, false, faint);
                Transform scrape = RealmKit.Group("Scrape", body);
                for (int m = 0; m < 3; m++)
                    RealmKit.Box("Mark" + m, scrape, new Vector3(0.02f + m * 0.1f, 0.32f, -0.185f), new Vector3(0.03f, 0.32f, 0.005f), RealmKit.Mud, false, new Vector3(0f, 0f, 15f - m * 8f));
                var plates = new GameObject[4];
                for (int k = 0; k < 4; k++)
                    plates[k] = DGlyph("Sign_" + WernesPaintedScripts.NameSigns[k], body, new Vector3(0.04f, 0.32f, -0.19f), Vector3.zero, BrickSignCodes[k], 0.42f, false, gold).gameObject;
                wakeStrips[i] = RealmKit.Box("WakeRim", body, new Vector3(0f, 0.68f, -0.05f), new Vector3(0.92f, 0.04f, 0.36f), wakeGlow, false);
                brickLights[i] = InnerGlow(body, new Vector3(0f, 0.5f, -0.6f), 0f, 2.6f);
                brickLights[i].color = new Color(1f, 0.8f, 0.45f);

                RealmObjectView v = View(ctx, slot, "brick" + (i + 1), new Vector3(0f, 1.1f, -0.3f));
                var bare = Pose(v, "bare", 0.5f); bare.show.Add(scrape.gameObject);
                foreach (GameObject pl in plates) bare.hide.Add(pl);
                for (int k = 0; k < 4; k++)
                {
                    var on = Pose(v, WernesPaintedScripts.NameSigns[k], 0.9f);
                    on.hide.Add(scrape.gameObject);
                    for (int j = 0; j < 4; j++) (j == k ? on.show : on.hide).Add(plates[j]);
                }
            }
            Transform allRoot = RealmKit.Group("BricksAll", w);
            RealmObjectView av = allRoot.gameObject.AddComponent<RealmObjectView>(); av.objectId = "bricks"; ctx.director.views.Add(av);
            var aDull = Pose(av, "dull", 1.0f);
            var aAwake = Pose(av, "awake", 1.8f);
            var aSpent = Pose(av, "spent", 2.4f);
            for (int i = 0; i < 4; i++)
            {
                Part(aDull, bodies[i], Vector3.zero, Vector3.zero);
                Part(aAwake, bodies[i], new Vector3(0f, 0.14f, 0f), new Vector3(0f, 0f, (i % 2 == 0 ? 2f : -2f)));
                Part(aSpent, bodies[i], new Vector3(0f, -0.02f, 0f), new Vector3(0f, 0f, (i % 2 == 0 ? -1f : 1f)));
                aDull.hide.Add(wakeStrips[i]); aAwake.show.Add(wakeStrips[i]); aSpent.hide.Add(wakeStrips[i]);
                aDull.lights.Add(brickLights[i]); aDull.lightIntensities.Add(0f);
                aAwake.lights.Add(brickLights[i]); aAwake.lightIntensities.Add(1.8f);
                aSpent.lights.Add(brickLights[i]); aSpent.lightIntensities.Add(0.12f);
            }
            aDull.lights.Add(cartLight); aDull.lightIntensities.Add(0f);
            aAwake.lights.Add(cartLight); aAwake.lightIntensities.Add(1.6f);
            aSpent.lights.Add(cartLight); aSpent.lightIntensities.Add(0f);

            // A wrong order: a cold shiver runs along the row.
            Transform shiver = RealmKit.Group("Misread", w, new Vector3(8.2f, 0.7f, 1.45f));
            RealmKit.Box("Shiver", shiver, Vector3.zero, new Vector3(4.6f, 0.03f, 0.02f), misreadGlow, false);
            var shs = shiver.gameObject.AddComponent<Sway>(); shs.axis = Vector3.forward; shs.degrees = 1.5f; shs.speed = 7f;
            Light coldLight = InnerGlow(shiver, new Vector3(0f, 0.3f, -0.8f), 0f, 4f);
            coldLight.color = new Color(0.5f, 0.6f, 1f);
            Transform mrRoot = RealmKit.Group("MisreadView", w);
            RealmObjectView mrv = mrRoot.gameObject.AddComponent<RealmObjectView>(); mrv.objectId = "misread"; ctx.director.views.Add(mrv);
            var mNo = Pose(mrv, "no", 0.6f); mNo.hide.Add(shiver.gameObject); mNo.lights.Add(coldLight); mNo.lightIntensities.Add(0f);
            var mYes = Pose(mrv, "yes", 0.8f); mYes.show.Add(shiver.gameObject); mYes.lights.Add(coldLight); mYes.lightIntensities.Add(1.2f);

            // ------------------------------------------------------------ the mat, the seated man, and the cry
            Transform mat = RealmKit.Group("Mat", w, new Vector3(13.2f, 0f, 0.4f));
            RealmKit.Box("Reeds", mat, new Vector3(0f, 0.07f, 0f), new Vector3(1.8f, 0.02f, 1.1f), RealmKit.ReedDry, false);
            RealmKit.Box("Cloth", mat, new Vector3(0.1f, 0.09f, 0.05f), new Vector3(0.8f, 0.02f, 0.5f), linen, false, new Vector3(0f, 12f, 0f));
            Transform man = RealmKit.Group("SeatedMan", mat, new Vector3(0f, 0.6f, 0f));
            Transform manBob = RealmKit.Group("Bob", man);
            var mb = manBob.gameObject.AddComponent<Bob>(); mb.amplitude = 0.04f; mb.roll = 2f;
            DGlyph("A1", manBob, Vector3.zero, Vector3.zero, "A1", 0.6f, false, gold);
            Light manLight = InnerGlow(man, new Vector3(0f, 0.1f, -0.6f), 0f, 3f);
            manLight.color = new Color(1f, 0.82f, 0.5f);
            Transform heldMan = RealmKit.Group("HeldMan", heldFace);
            DGlyph("A1", heldMan, Vector3.zero, Vector3.zero, "A1", 0.5f, false, gold);
            RealmObjectView mv = View(ctx, mat, "man", new Vector3(0f, 1.3f, -0.3f));
            var mUnseen = Pose(mv, "unseen", 1.4f); Part(mUnseen, man, new Vector3(0f, 0.15f, 0f), Vector3.zero, Vector3.one * 0.01f);
            mUnseen.hide.Add(heldMan.gameObject); mUnseen.lights.Add(manLight); mUnseen.lightIntensities.Add(0f);
            var mMat = Pose(mv, "mat", 2.0f); Part(mMat, man, new Vector3(0f, 0.6f, 0f), Vector3.zero, Vector3.one);
            mMat.hide.Add(heldMan.gameObject); mMat.lights.Add(manLight); mMat.lightIntensities.Add(1.4f);
            var mHand = Pose(mv, "hand", 0.4f); Part(mHand, man, new Vector3(0f, 0.6f, 0f), Vector3.zero, Vector3.one * 0.01f);
            mHand.show.Add(heldMan.gameObject); mHand.lights.Add(manLight); mHand.lightIntensities.Add(0f);
            var mLaid = Pose(mv, "laid", 0.4f); Part(mLaid, man, new Vector3(0f, 0.15f, 0f), Vector3.zero, Vector3.one * 0.01f);
            mLaid.hide.Add(heldMan.gameObject); mLaid.lights.Add(manLight); mLaid.lightIntensities.Add(0f);

            // The cry: rings of gold beads that swell out from the mat and thin as it fades. Echo threads them with cyan.
            Transform cry = RealmKit.Group("Cry", w, new Vector3(13.2f, 1.4f, 0.4f));
            var rings = new Transform[3];
            for (int r = 0; r < 3; r++)
            {
                rings[r] = RealmKit.Group("Ring" + r, cry);
                int beads = 10 + r * 4;
                float rad = 0.45f + r * 0.4f;
                for (int b = 0; b < beads; b++)
                {
                    float a = b * Mathf.PI * 2f / beads;
                    RealmKit.Shape(PrimitiveType.Sphere, "Bead" + b, rings[r], new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad, 0f), Vector3.one * 0.06f, cryBead);
                }
                var rs = rings[r].gameObject.AddComponent<Sway>(); rs.axis = Vector3.forward; rs.degrees = 20f + r * 10f; rs.speed = 0.6f + r * 0.3f;
            }
            Transform echo = RealmKit.Group("Echo", cry);
            for (int b = 0; b < 18; b++)
            {
                float a = b * Mathf.PI * 2f / 18f;
                RealmKit.Shape(PrimitiveType.Sphere, "Bead" + b, echo, new Vector3(Mathf.Cos(a) * 1.05f, Mathf.Sin(a) * 1.05f, 0f), Vector3.one * 0.05f, echoBead);
            }
            var es = echo.gameObject.AddComponent<Sway>(); es.axis = Vector3.forward; es.degrees = 40f; es.speed = 0.5f;
            Light cryLight = InnerGlow(cry, new Vector3(0f, 0f, -0.8f), 0f, 6f);
            cryLight.color = new Color(1f, 0.82f, 0.5f);
            RealmObjectView cv = View(ctx, cry, "cry", new Vector3(0f, 1.2f, -0.2f));
            CryPose(Pose(cv, "silent", 1.2f), rings, 0.05f, cryLight, 0f, echo, false, cry, true);
            CryPose(Pose(cv, "sounding", 1.4f), rings, 1.0f, cryLight, 2.4f, echo, false, cry, false);
            CryPose(Pose(cv, "fading", 4.0f), rings, 0.45f, cryLight, 0.7f, echo, false, cry, false);
            CryPose(Pose(cv, "held", 1.6f), rings, 0.9f, cryLight, 2.0f, echo, true, cry, false);
            CryPose(Pose(cv, "answered", 2.4f), rings, 1.25f, cryLight, 3.2f, echo, true, cry, false);

            // ------------------------------------------------------------ light, dust, walls
            RealmKit.Brazier("WestLamp", w, new Vector3(0.6f, 0f, 1.6f), 8f, 1.6f);
            RealmKit.Brazier("BasinLamp", w, new Vector3(5.4f, 0f, 1.9f), 7f, 1.4f);
            RealmKit.Brazier("DoorLamp", w, new Vector3(16.6f, 0f, 2.2f), 7f, 1.5f);
            RealmKit.Motes(w, new Vector3(8.5f, 1.8f, 1f), new Vector3(16f, 3f, 4f), new Color(1f, 0.85f, 0.6f, 0.6f), 110);

            RealmKit.Blocker(w, new Vector3(8.5f, 3f, -2.5f), new Vector3(17.4f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(8.5f, 3f, 2.5f), new Vector3(17.4f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(0.2f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));
            RealmKit.Blocker(w, new Vector3(17.0f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));

            Zone(ctx, "goddess", new Vector3(1.8f, 1f, 0.2f), new Vector3(2.6f, 4f, 5f), -1, 1);
            Zone(ctx, "basin", new Vector3(4.2f, 1f, 0.2f), new Vector3(1.8f, 4f, 5f), -1, 1);
            for (int i = 0; i < 4; i++)
                Zone(ctx, "brick" + (i + 1), new Vector3(6.4f + i * 1.2f, 1f, 0.2f), new Vector3(1.15f, 4f, 5f), -1, 2);
            Zone(ctx, "seat", new Vector3(11.2f, 1f, 0.2f), new Vector3(1.15f, 4f, 5f), -1, 2);
            Zone(ctx, "mat", new Vector3(13.2f, 1f, 0.2f), new Vector3(1.9f, 4f, 5f), -1, 1);
            Zone(ctx, "basket", new Vector3(14.6f, 1f, 0.2f), new Vector3(0.9f, 4f, 5f), -1, 2);
            Zone(ctx, "snare", new Vector3(15.9f, 1f, 0.2f), new Vector3(1.6f, 4f, 5f), -1, 1);

            Shot(ctx, "cam_room", new Vector3(8.5f, 4.6f, -17.5f), new Vector3(8.5f, 1.8f, 1f), 40f, new Vector3(8.5f, 3f, 0f), new Vector3(17.6f, 10f, 8f), 0.04f, 0);
            Shot(ctx, "cam_bricks", new Vector3(8.8f, 3.4f, -10.5f), new Vector3(8.8f, 1.5f, 1.2f), 40f, new Vector3(8.8f, 3f, 0f), new Vector3(6.2f, 10f, 8f), 0.04f, 1);
            ApplyPaint(ctx, WernesPaintedScripts.BricksD, new[]
            {
                new Rect4(new Vector3(8.5f, 0.07f, 0.2f), new Vector2(17.4f, 5.4f)),
            });
            Finish(ctx, WernesPaintedScripts.BricksD);
        }

        /// <summary>
        /// One sound-sign of the name. It grows out of where it was kept (start state), floats loose where Ankhu can
        /// take it, rides above his head while held, and is gone once laid (the brick shows it then).
        /// </summary>
        private static Transform SignView(Ctx ctx, Transform w, Transform heldFace, string id, string code, string startState,
            Vector3 origin, Vector3 loosePos, float startScale, Color tint)
        {
            Transform root = RealmKit.Group("Sign_" + id, w, origin);
            Transform bob = RealmKit.Group("Bob", root);
            var b = bob.gameObject.AddComponent<Bob>(); b.amplitude = 0.05f; b.roll = 4f;
            DGlyph("Glyph", bob, Vector3.zero, Vector3.zero, code, 0.55f, false, tint);
            Light glow = InnerGlow(root, new Vector3(0f, 0f, -0.5f), 0f, 2.4f);
            glow.color = new Color(tint.r, tint.g, tint.b);
            Transform inHand = RealmKit.Group("Held_" + id, heldFace);
            DGlyph("Glyph", inHand, Vector3.zero, Vector3.zero, code, 0.5f, false, tint);

            RealmObjectView v = View(ctx, root, id, new Vector3(0f, 0.7f, -0.2f));
            var start = Pose(v, startState, 0.5f); Part(start, root, origin, Vector3.zero, Vector3.one * startScale);
            start.hide.Add(inHand.gameObject); start.lights.Add(glow); start.lightIntensities.Add(startScale > 0.1f ? 0.4f : 0f);
            var loose = Pose(v, "loose", 1.8f); Part(loose, root, loosePos, Vector3.zero, Vector3.one);
            loose.hide.Add(inHand.gameObject); loose.lights.Add(glow); loose.lightIntensities.Add(1.0f);
            var hand = Pose(v, "hand", 0.35f); Part(hand, root, loosePos, Vector3.zero, Vector3.one * 0.01f);
            hand.show.Add(inHand.gameObject); hand.lights.Add(glow); hand.lightIntensities.Add(0f);
            var laid = Pose(v, "laid", 0.35f); Part(laid, root, loosePos, Vector3.zero, Vector3.one * 0.01f);
            laid.hide.Add(inHand.gameObject); laid.lights.Add(glow); laid.lightIntensities.Add(0f);
            return root;
        }

        /// <summary>
        /// RealmKit.GlyphPlate shares one material per sign code across every room, so the first tint wins. Room D
        /// gives each tint its own material so a faint scratch and a bright gold sign of the same code can coexist.
        /// </summary>
        private static Transform DGlyph(string name, Transform parent, Vector3 pos, Vector3 euler, string signs, float size, bool vertical, Color tint)
        {
            Transform g = RealmKit.GlyphPlate(name, parent, pos, euler, signs, size, vertical, tint);
            string key = ColorUtility.ToHtmlStringRGBA(tint);
            foreach (MeshRenderer r in g.GetComponentsInChildren<MeshRenderer>(true))
            {
                var tex = r.sharedMaterial != null ? r.sharedMaterial.GetTexture("_BaseMap") as Texture2D : null;
                Material m = RealmKit.Additive("WD_" + key + "_" + r.gameObject.name, tint, tex);
                m.SetFloat("_Cull", 0f); // two-sided, so a plate reads whichever way it was turned
                r.sharedMaterial = m;
            }
            return g;
        }

        private static void CryPose(RealmObjectView.StatePose p, Transform[] rings, float spread, Light light, float intensity,
            Transform echo, bool echoed, Transform cry, bool silent)
        {
            for (int r = 0; r < rings.Length; r++)
                Part(p, rings[r], Vector3.zero, rings[r].localEulerAngles, Vector3.one * Mathf.Max(0.01f, spread * (0.8f + r * 0.2f)));
            Part(p, echo, Vector3.zero, echo.localEulerAngles, Vector3.one * (echoed ? 1f : 0.01f));
            if (echoed) p.show.Add(echo.gameObject); else p.hide.Add(echo.gameObject);
            if (silent) p.hide.Add(cry.gameObject); else p.show.Add(cry.gameObject);
            p.lights.Add(light); p.lightIntensities.Add(intensity);
        }
    }
}
