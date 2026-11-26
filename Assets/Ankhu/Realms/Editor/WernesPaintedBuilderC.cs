using Ankhu.Realms.Core.Content;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Wernes Room C, The House With One Door. A one-room mud-brick house, cut away toward the camera, with a false
    /// door on its back wall. The upper sluice is jammed open, so the channel runs across the house floor and out
    /// through the east door, which the wind bangs open and shut. Outside, the seedling plot drinks only through
    /// the door, a lower gate passes water on to the father's old plot with its one dry sapling, and a ditch gate
    /// drains that plot.
    /// Layout: house x 0..12 (false door at x 3.2), door at x 12, front plot x 14.6..19.4, lower gate x 20.6,
    /// back plot x 22..27, ditch gate x 28.4, ditch x 29.6.
    /// </summary>
    public static partial class RealmSceneBuilder
    {
        private const float DoorX = 12f;

        private static void BuildHouseC()
        {
            Ctx ctx = Begin(WernesPaintedScripts.HouseC, new Color(0.07f, 0.08f, 0.12f), 0.010f, 0.4f);
            Transform w = ctx.world;
            Ground(w, 140f, RealmKit.Mud);
            Dunes(w, 13, 70f);
            Material brick = RealmKit.Lit("MudBrick", new Color(0.46f, 0.34f, 0.24f), 0.1f, 0f,
                RealmKit.StoneTexture("mudbrick", new Color(0.34f, 0.24f, 0.16f), new Color(0.56f, 0.42f, 0.30f), 1.5f, 71), 1.5f);
            Material plaster = RealmKit.Lit("Plaster", new Color(0.86f, 0.80f, 0.66f), 0.1f);
            Material thread = RealmKit.Emissive("BindThread", new Color(0.3f, 0.9f, 1f), new Color(0.6f, 3.2f, 4.2f));
            Material sealGlow = RealmKit.Emissive("SealGold", new Color(1f, 0.8f, 0.4f), new Color(4.2f, 2.8f, 0.9f));
            Material dryEarth = RealmKit.Lit("DryEarth", new Color(0.62f, 0.50f, 0.34f), 0.05f, 0f,
                RealmKit.StoneTexture("dryearth", new Color(0.48f, 0.38f, 0.26f), new Color(0.72f, 0.60f, 0.42f), 0.4f, 83), 3f);
            Material wetEarth = RealmKit.Lit("WetEarth", new Color(0.20f, 0.16f, 0.12f), 0.7f);
            Material sprout = RealmKit.Lit("Sprout", new Color(0.36f, 0.62f, 0.24f), 0.3f);
            Material grain = RealmKit.Lit("Grain", new Color(0.72f, 0.58f, 0.24f), 0.2f);
            Material linen = RealmKit.Lit("Linen", new Color(0.88f, 0.84f, 0.74f), 0.05f);
            Material niche = RealmKit.Emissive("NicheGlow", new Color(1f, 0.75f, 0.4f), new Color(1.6f, 0.9f, 0.3f));

            // Far fields and the canal behind the house, with the jammed upper gate at the west end.
            Transform far = RealmKit.Group("FarFields", w);
            for (int row = 0; row < 4; row++)
                RealmKit.Box("Grain" + row, far, new Vector3(14f, 0.3f, 15f + row * 3f), new Vector3(50f, 0.6f, 1.6f), grain, false);
            RealmKit.Box("CanalBed", w, new Vector3(10f, -0.9f, 6.4f), new Vector3(44f, 0.2f, 3f), RealmKit.Mud, false);
            RealmKit.Box("CanalWater", w, new Vector3(10f, -0.15f, 6.4f), new Vector3(44f, 0.05f, 3f), RealmKit.Water, false)
                .AddComponent<WaterMotion>().amplitude = 0.015f;
            RealmKit.Box("CanalBank", w, new Vector3(10f, -0.1f, 8.2f), new Vector3(44f, 0.4f, 0.8f), RealmKit.Limestone, false);
            Reeds(w, new Vector3(10f, 0f, 9.1f), new Vector3(42f, 0f, 1.2f), 200, 61);

            Transform upper = RealmKit.Group("UpperGate", w, new Vector3(-1.6f, 0f, 4.4f));
            RealmKit.Box("JambL", upper, new Vector3(-0.8f, 1.0f, 0f), new Vector3(0.4f, 2.0f, 0.8f), RealmKit.Sandstone);
            RealmKit.Box("JambR", upper, new Vector3(0.8f, 1.0f, 0f), new Vector3(0.4f, 2.0f, 0.8f), RealmKit.Sandstone);
            RealmKit.Box("Head", upper, new Vector3(0f, 2.15f, 0f), new Vector3(2.2f, 0.32f, 0.9f), RealmKit.Sandstone);
            RealmKit.Box("JammedBoard", upper, new Vector3(0f, 1.85f, 0f), new Vector3(1.2f, 0.5f, 0.14f), RealmKit.Wood, false, new Vector3(0f, 0f, 9f));
            RealmKit.Box("Wedge", upper, new Vector3(0.45f, 1.5f, 0f), new Vector3(0.18f, 0.3f, 0.2f), RealmKit.Shadow, false, new Vector3(0f, 0f, 30f));
            RealmKit.Box("Spill", upper, new Vector3(0f, -0.04f, -1.6f), new Vector3(0.9f, 0.04f, 3.2f), RealmKit.WaterRough, false)
                .AddComponent<WaterMotion>().amplitude = 0.03f;

            // The house: back wall with a wind-shutter window, west wall, east wall with the doorway, cut away in front.
            Transform house = RealmKit.Group("House", w);
            RealmKit.Box("Floor", house, new Vector3(6f, 0.03f, 0.2f), new Vector3(12f, 0.06f, 5.4f), RealmKit.Limestone, false);
            RealmKit.Box("BackWallL", house, new Vector3(3.5f, 1.8f, 2.9f), new Vector3(7f, 3.6f, 0.5f), brick);
            RealmKit.Box("BackWallR", house, new Vector3(10.5f, 1.8f, 2.9f), new Vector3(3f, 3.6f, 0.5f), brick);
            RealmKit.Box("BackWallUnder", house, new Vector3(8f, 0.8f, 2.9f), new Vector3(2f, 1.6f, 0.5f), brick);
            RealmKit.Box("BackWallOver", house, new Vector3(8f, 3.25f, 2.9f), new Vector3(2f, 0.7f, 0.5f), brick);
            RealmKit.Box("WestWall", house, new Vector3(-0.25f, 1.8f, 0.2f), new Vector3(0.5f, 3.6f, 5.8f), brick);
            RealmKit.Box("EastWallBack", house, new Vector3(DoorX + 0.25f, 1.8f, 2.0f), new Vector3(0.5f, 3.6f, 2.2f), brick);
            RealmKit.Box("EastWallFront", house, new Vector3(DoorX + 0.25f, 1.8f, -1.75f), new Vector3(0.5f, 3.6f, 1.7f), brick);
            RealmKit.Box("EastLintel", house, new Vector3(DoorX + 0.25f, 3.1f, 0f), new Vector3(0.5f, 1.0f, 1.8f), brick);
            RealmKit.Box("FrontStub", house, new Vector3(6f, 0.25f, -2.75f), new Vector3(12.5f, 0.5f, 0.4f), brick, false);
            RealmKit.Box("RoofBeams", house, new Vector3(6f, 3.7f, 2.0f), new Vector3(12.5f, 0.18f, 2.2f), RealmKit.Wood, false);
            for (int b = 0; b < 7; b++)
                RealmKit.Box("Joist" + b, house, new Vector3(0.6f + b * 1.8f, 3.65f, 0.6f), new Vector3(0.14f, 0.14f, 4.6f), RealmKit.Wood, false);
            RealmKit.Box("Plaster", house, new Vector3(5f, 1.6f, 2.64f), new Vector3(9.6f, 2.8f, 0.02f), plaster, false);
            // A bench, a water jar, a reed mat: someone lived here.
            RealmKit.Box("Bench", house, new Vector3(6.6f, 0.35f, 2.1f), new Vector3(2.2f, 0.12f, 0.6f), RealmKit.Wood);
            RealmKit.Box("BenchLegL", house, new Vector3(5.7f, 0.15f, 2.1f), new Vector3(0.12f, 0.3f, 0.5f), RealmKit.Wood, false);
            RealmKit.Box("BenchLegR", house, new Vector3(7.5f, 0.15f, 2.1f), new Vector3(0.12f, 0.3f, 0.5f), RealmKit.Wood, false);
            RealmKit.Shape(PrimitiveType.Sphere, "Jar", house, new Vector3(9.6f, 0.45f, 2.2f), new Vector3(0.55f, 0.8f, 0.55f), RealmKit.Ochre);
            RealmKit.Box("Mat", house, new Vector3(6.6f, 0.065f, 0.9f), new Vector3(2.4f, 0.01f, 1.2f), RealmKit.ReedDry, false);

            // The channel across the floor, always running, because the upper gate is jammed.
            RealmKit.Box("FloorChannel", house, new Vector3(6f, 0.05f, 1.5f), new Vector3(12.6f, 0.05f, 0.6f), RealmKit.Water, false)
                .AddComponent<WaterMotion>().amplitude = 0.012f;
            RealmKit.Box("ChannelLipF", house, new Vector3(6f, 0.09f, 1.15f), new Vector3(12.6f, 0.08f, 0.1f), RealmKit.Sandstone, false);
            RealmKit.Box("ChannelLipB", house, new Vector3(6f, 0.09f, 1.85f), new Vector3(12.6f, 0.08f, 0.1f), RealmKit.Sandstone, false);

            // Wind: the window shutter rattles, and a linen hanging lifts and falls.
            Transform shutter = RealmKit.Group("Shutter", house, new Vector3(7.05f, 2.6f, 2.6f));
            RealmKit.Box("Board", shutter, new Vector3(0.45f, -0.45f, -0.05f), new Vector3(0.9f, 0.9f, 0.06f), RealmKit.Wood, false);
            var shs = shutter.gameObject.AddComponent<Sway>(); shs.axis = Vector3.up; shs.degrees = 22f; shs.speed = 1.6f;
            Transform hang = RealmKit.Group("Hanging", house, new Vector3(1.4f, 3.4f, 2.55f));
            RealmKit.Box("Cloth", hang, new Vector3(0f, -0.8f, -0.02f), new Vector3(0.9f, 1.6f, 0.02f), linen, false);
            var hs = hang.gameObject.AddComponent<Sway>(); hs.axis = Vector3.right; hs.degrees = 10f; hs.speed = 1.1f;
            Transform lampT = RealmKit.Group("HouseLamp", house, new Vector3(9.0f, 3.1f, 1.2f));
            Transform lampHang = RealmKit.Group("Hang", lampT);
            RealmKit.Box("Cord", lampHang, new Vector3(0f, -0.2f, 0f), new Vector3(0.02f, 0.4f, 0.02f), RealmKit.ReedDry, false);
            RealmKit.Shape(PrimitiveType.Sphere, "Flame", lampHang, new Vector3(0f, -0.45f, 0f), Vector3.one * 0.14f, RealmKit.GoldGlow);
            InnerGlow(lampHang, new Vector3(0f, -0.45f, -0.3f), 1.4f, 7f);
            var lsw = lampHang.gameObject.AddComponent<Sway>(); lsw.degrees = 9f; lsw.speed = 1.5f;

            // The false door: a stepped stone frame, a lintel with the words, a painting above, a niche that warms.
            Transform fd = RealmKit.Group("FalseDoor", w, new Vector3(3.2f, 0f, 2.6f));
            for (int k = 0; k < 3; k++)
            {
                float wd = 2.6f - k * 0.5f, ht = 3.0f - k * 0.3f, z = -0.06f - k * 0.06f;
                RealmKit.Box("Frame" + k, fd, new Vector3(0f, ht * 0.5f, z), new Vector3(wd, ht, 0.08f), RealmKit.Limestone, false);
            }
            RealmKit.Box("Lintel", fd, new Vector3(0f, 2.75f, -0.26f), new Vector3(2.8f, 0.36f, 0.14f), RealmKit.Sandstone, false);
            RealmKit.GlyphPlate("LintelWords", fd, new Vector3(0f, 2.75f, -0.34f), new Vector3(0f, 180f, 0f), "O31 D28 S20 Aa1 X1 G17", 0.22f, false, new Color(1f, 0.78f, 0.4f, 0.8f));
            RealmKit.Box("Vignette", fd, new Vector3(0f, 3.3f, -0.05f), new Vector3(2.2f, 0.6f, 0.04f), plaster, false);
            RealmKit.GlyphPlate("VignetteScene", fd, new Vector3(0f, 3.3f, -0.08f), new Vector3(0f, 180f, 0f), "A17 O31 M20", 0.26f, false, new Color(0.25f, 0.4f, 0.75f, 0.9f));
            Transform slab = RealmKit.Group("Slab", fd, new Vector3(0f, 1.05f, -0.24f));
            RealmKit.Box("Stone", slab, Vector3.zero, new Vector3(1.0f, 2.1f, 0.08f), RealmKit.Sandstone, false);
            Transform nicheGlow = RealmKit.Group("Niche", fd, new Vector3(0f, 1.05f, -0.21f));
            RealmKit.Box("Glow", nicheGlow, Vector3.zero, new Vector3(0.9f, 2.0f, 0.02f), niche, false);
            Light fdLight = InnerGlow(fd, new Vector3(0f, 1.3f, -0.9f), 0f, 6f);
            fdLight.color = new Color(1f, 0.72f, 0.38f);
            RealmObjectView fdv = View(ctx, fd, "fdoor", new Vector3(0f, 2.2f, -0.4f));
            var fdark = Pose(fdv, "dark", 1.2f); fdark.hide.Add(nicheGlow.gameObject); fdark.lights.Add(fdLight); fdark.lightIntensities.Add(0f);
            var flit = Pose(fdv, "lit", 2.4f); flit.show.Add(nicheGlow.gameObject); flit.lights.Add(fdLight); flit.lightIntensities.Add(1.6f);
            var fwarm = Pose(fdv, "warm", 2.4f); fwarm.show.Add(nicheGlow.gameObject); fwarm.lights.Add(fdLight); fwarm.lightIntensities.Add(3.0f);
            // Once walked through, the slab stands back like a door left ajar.
            var memRoot = new GameObject("Memory").transform; memRoot.SetParent(w, false);
            RealmObjectView mv = memRoot.gameObject.AddComponent<RealmObjectView>(); mv.objectId = "memory"; ctx.director.views.Add(mv);
            var mu = Pose(mv, "unseen", 0.4f); Part(mu, slab, new Vector3(0f, 1.05f, -0.24f), Vector3.zero);
            var ms = Pose(mv, "seen", 2.8f); Part(ms, slab, new Vector3(0.38f, 1.05f, -0.1f), new Vector3(0f, -55f, 0f));

            // The door. It hangs on the north jamb and swings outward. Free, a loose leaf that shivers in the wind.
            // Held or sealed, a firm leaf that does not.
            Transform hinge = RealmKit.Group("DoorHinge", w, new Vector3(DoorX + 0.1f, 0f, 0.9f));
            Transform loose = RealmKit.Group("LooseLeaf", hinge);
            Transform shiver = RealmKit.Group("Shiver", loose);
            DoorLeaf(shiver);
            var dsw = shiver.gameObject.AddComponent<Sway>(); dsw.axis = Vector3.up; dsw.degrees = 4f; dsw.speed = 3.1f;
            Transform firm = RealmKit.Group("FirmLeaf", hinge);
            DoorLeaf(firm);
            GameObject stream = RealmKit.Box("DoorStream", w, new Vector3(DoorX + 1.3f, 0.04f, 0.5f), new Vector3(2.6f, 0.04f, 0.7f), RealmKit.WaterRough, false);
            stream.AddComponent<WaterMotion>().amplitude = 0.03f;
            RealmObjectView dv = View(ctx, hinge, "door", new Vector3(0f, 2.6f, -0.9f));
            var dshut = Pose(dv, "shut", 0.35f); Part(dshut, hinge, hinge.localPosition, Vector3.zero); dshut.hide.Add(stream);
            var dopen = Pose(dv, "open", 0.5f); Part(dopen, hinge, hinge.localPosition, new Vector3(0f, -100f, 0f)); dopen.show.Add(stream);

            // The binding: a cyan thread from the leaf to the jamb, or a gold bolt and seal glyph from inside.
            Transform holdT = RealmKit.Group("Thread", w, new Vector3(DoorX - 0.1f, 1.4f, 0f));
            RealmKit.Box("Strand", holdT, Vector3.zero, new Vector3(0.03f, 0.03f, 1.8f), thread, false);
            RealmKit.Shape(PrimitiveType.Sphere, "Knot", holdT, new Vector3(0f, 0f, -0.9f), Vector3.one * 0.14f, thread);
            Light holdLight = InnerGlow(holdT, new Vector3(-0.4f, 0.2f, 0f), 0f, 5f);
            holdLight.color = new Color(0.4f, 0.9f, 1f);
            Transform bolt = RealmKit.Group("Bolt", w, new Vector3(DoorX - 0.15f, 1.25f, -0.4f));
            RealmKit.Box("Bar", bolt, Vector3.zero, new Vector3(0.1f, 0.12f, 0.9f), RealmKit.Gold, false);
            Transform sealMark = RealmKit.Group("SealMark", w, new Vector3(DoorX - 0.2f, 1.9f, 0f));
            RealmKit.GlyphPlate("Khetem", sealMark, Vector3.zero, new Vector3(0f, -90f, 0f), "S20", 0.4f, true, new Color(1f, 0.8f, 0.4f, 1f));
            RealmKit.Box("Seal", sealMark, new Vector3(0.02f, 0f, 0f), new Vector3(0.02f, 0.5f, 0.5f), sealGlow, false);
            Light sealLight = InnerGlow(sealMark, new Vector3(-0.6f, 0f, 0f), 0f, 6f);
            sealLight.color = new Color(1f, 0.8f, 0.45f);
            var modeRoot = new GameObject("DoorMode").transform; modeRoot.SetParent(w, false);
            RealmObjectView modev = modeRoot.gameObject.AddComponent<RealmObjectView>(); modev.objectId = "mode"; ctx.director.views.Add(modev);
            var mfree = Pose(modev, "free", 1.2f);
            mfree.show.Add(loose.gameObject); mfree.hide.Add(firm.gameObject); mfree.hide.Add(holdT.gameObject); mfree.hide.Add(sealMark.gameObject);
            Part(mfree, bolt, new Vector3(DoorX - 0.15f, 1.25f, -1.3f), Vector3.zero);
            mfree.lights.Add(holdLight); mfree.lightIntensities.Add(0f); mfree.lights.Add(sealLight); mfree.lightIntensities.Add(0f);
            var mheld = Pose(modev, "held", 1.2f);
            mheld.hide.Add(loose.gameObject); mheld.show.Add(firm.gameObject); mheld.show.Add(holdT.gameObject); mheld.hide.Add(sealMark.gameObject);
            Part(mheld, bolt, new Vector3(DoorX - 0.15f, 1.25f, -1.3f), Vector3.zero);
            mheld.lights.Add(holdLight); mheld.lightIntensities.Add(1.8f); mheld.lights.Add(sealLight); mheld.lightIntensities.Add(0f);
            var msealed = Pose(modev, "sealed", 2.2f);
            msealed.hide.Add(loose.gameObject); msealed.show.Add(firm.gameObject); msealed.hide.Add(holdT.gameObject); msealed.show.Add(sealMark.gameObject);
            Part(msealed, bolt, new Vector3(DoorX - 0.15f, 1.25f, -0.4f), Vector3.zero);
            msealed.lights.Add(holdLight); msealed.lightIntensities.Add(0f); msealed.lights.Add(sealLight); msealed.lightIntensities.Add(2.6f);

            // The yard: the seedling plot, the lower gate, the old plot with its sapling, the ditch gate and ditch.
            RealmKit.Box("YardWallBack", w, new Vector3(21.5f, 0.6f, 3.4f), new Vector3(19f, 1.2f, 0.4f), brick);
            Transform frontPlot = YardPlot(ctx, w, "FrontPlot", "front", new Vector3(17f, 0f, 0.9f), 4.8f, dryEarth, wetEarth, sprout, true, 31);
            YardPlot(ctx, w, "BackPlot", "back", new Vector3(24.5f, 0f, 0.9f), 5f, dryEarth, wetEarth, sprout, false, 37);
            WernesGate(ctx, w, "LowerGate", "lower", new Vector3(20.6f, 0f, 2.4f), "Z1 O31", new Vector3(20.6f, -0.04f, 1.2f), new Vector2(1.0f, 2.0f));
            WernesGate(ctx, w, "DitchGate", "ditch", new Vector3(28.4f, 0f, 2.4f), "Z1 Z1 O31", new Vector3(29.4f, -0.04f, 1.6f), new Vector2(1.6f, 0.6f));
            RealmKit.Box("DitchBed", w, new Vector3(30.2f, -0.6f, 1f), new Vector3(1.4f, 0.2f, 12f), RealmKit.Mud, false);
            RealmKit.Box("DitchWater", w, new Vector3(30.2f, -0.3f, 1f), new Vector3(1.2f, 0.04f, 12f), RealmKit.Water, false)
                .AddComponent<WaterMotion>().amplitude = 0.015f;
            Reeds(w, new Vector3(31.4f, 0f, 2f), new Vector3(0.8f, 0f, 4f), 50, 67);

            // A palm leaning over the yard wall, and a brazier at the gatepost.
            var pr = new System.Random(55);
            Transform palm = RealmKit.Group("Palm", w, new Vector3(22.6f, 0f, 5.0f));
            RealmKit.Shape(PrimitiveType.Cylinder, "Trunk", palm, new Vector3(0f, 2.4f, 0f), new Vector3(0.28f, 2.4f, 0.28f), RealmKit.Wood, true, new Vector3(0f, 0f, 6f));
            Transform crown = RealmKit.Group("Crown", palm, new Vector3(0.4f, 4.8f, 0f));
            for (int f = 0; f < 7; f++)
            {
                Transform frond = RealmKit.Group("Frond" + f, crown, Vector3.zero, new Vector3(0f, f * 51f + (float)pr.NextDouble() * 10f, 0f));
                RealmKit.Box("Leaf", frond, new Vector3(0f, -0.25f, 1.0f), new Vector3(0.35f, 0.04f, 2.0f), RealmKit.Reed, false, new Vector3(22f, 0f, 0f));
            }
            var cs = crown.gameObject.AddComponent<Sway>(); cs.axis = Vector3.forward; cs.degrees = 5f; cs.speed = 0.9f;

            RealmKit.Brazier("HouseLampWest", w, new Vector3(-1.4f, 0f, 1.6f), 8f, 1.8f);
            RealmKit.Brazier("GatePost", w, new Vector3(13.4f, 0f, 2.6f), 8f, 1.8f);
            RealmKit.Brazier("DitchLamp", w, new Vector3(27.2f, 0f, 2.9f), 8f, 1.8f);
            RealmKit.Motes(w, new Vector3(6f, 1.8f, 1f), new Vector3(11f, 3f, 4f), new Color(1f, 0.85f, 0.6f, 0.6f), 70);
            RealmKit.Motes(w, new Vector3(21f, 2.5f, 1f), new Vector3(18f, 5f, 6f), new Color(1f, 0.9f, 0.6f, 0.7f), 80);

            Cast(ctx, new Vector3(6.2f, 0f, -0.6f), 90f, new Vector3(1.6f, 0f, -1.0f));

            // Passing the doorway: only while the door stands open.
            var sideRoot = new GameObject("AnkhuSide").transform; sideRoot.SetParent(w, false);
            RealmObjectView sv = sideRoot.gameObject.AddComponent<RealmObjectView>(); sv.objectId = "side"; ctx.director.views.Add(sv);
            Pose(sv, "in", 0.1f); Pose(sv, "out", 0.1f);
            var ride = sideRoot.gameObject.AddComponent<RealmRide>();
            ride.view = sv;
            ride.rider = ctx.ankhu.transform;
            ride.forwardState = "out";
            ride.speed = 1.8f;
            ride.bob = 0.03f;
            ride.marks = new[] { new Vector3(DoorX - 1.2f, 0f, 0f), new Vector3(DoorX, 0f, -0.2f), new Vector3(DoorX + 1.4f, 0f, -0.2f) };

            // The house lane, the doorway (only the ride passes it), and the yard lane.
            RealmKit.Blocker(w, new Vector3(6f, 3f, -2.5f), new Vector3(12.4f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(6f, 3f, 2.5f), new Vector3(12.4f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(0.2f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));
            RealmKit.Blocker(w, new Vector3(DoorX + 0.2f, 3f, 0f), new Vector3(0.6f, 6f, 5.4f));
            RealmKit.Blocker(w, new Vector3(21.5f, 3f, -2.5f), new Vector3(19f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(21.5f, 3f, 2.8f), new Vector3(19f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(29.4f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));

            Zone(ctx, "falsedoor", new Vector3(3.2f, 1f, 0.2f), new Vector3(3.2f, 4f, 5f), -1, 2);
            Zone(ctx, "threshold", new Vector3(DoorX - 1.2f, 1f, 0.2f), new Vector3(2.4f, 4f, 5f), -1, 2);
            Zone(ctx, "house", new Vector3(6f, 1f, 0.2f), new Vector3(12f, 4f, 5f), -1, 0);
            Zone(ctx, "yard", new Vector3(DoorX + 1.6f, 1f, 0.2f), new Vector3(3.2f, 4f, 5f), -1, 2);
            Zone(ctx, "frontplot", new Vector3(17f, 1f, 0.2f), new Vector3(5.6f, 4f, 5f), -1, 1);
            Zone(ctx, "back", new Vector3(24.5f, 1f, 0.2f), new Vector3(8f, 4f, 5f), -1, 1);

            Shot(ctx, "cam_house", new Vector3(6f, 4.2f, -14f), new Vector3(6f, 1.8f, 1f), 40f, new Vector3(6f, 3f, 0f), new Vector3(12.6f, 10f, 8f), 0.04f, 0);
            Shot(ctx, "cam_yard", new Vector3(21.6f, 4.4f, -15.5f), new Vector3(21.6f, 1.9f, 1f), 40f, new Vector3(21.6f, 3f, 0f), new Vector3(18.4f, 12f, 8f), 0.04f, 1);
            ApplyPaint(ctx, WernesPaintedScripts.HouseC, new[]
            {
                new Rect4(new Vector3(6f, 0.07f, 0.2f), new Vector2(12f, 5.4f)),
                new Rect4(new Vector3(21.5f, 0.004f, 0.4f), new Vector2(19f, 10f)),
            });
            Finish(ctx, WernesPaintedScripts.HouseC);
        }

        /// <summary>A plank door leaf hanging from a hinge at its north edge, closing toward -z.</summary>
        private static void DoorLeaf(Transform parent)
        {
            for (int p = 0; p < 4; p++)
                RealmKit.Box("Plank" + p, parent, new Vector3(0f, 1.25f, -0.22f - p * 0.45f), new Vector3(0.1f, 2.5f, 0.43f), RealmKit.Wood, false);
            RealmKit.Box("BraceTop", parent, new Vector3(-0.07f, 2.0f, -0.9f), new Vector3(0.05f, 0.14f, 1.75f), RealmKit.Wood, false);
            RealmKit.Box("BraceLow", parent, new Vector3(-0.07f, 0.5f, -0.9f), new Vector3(0.05f, 0.14f, 1.75f), RealmKit.Wood, false);
            RealmKit.Box("BraceDiag", parent, new Vector3(-0.07f, 1.25f, -0.9f), new Vector3(0.05f, 0.12f, 2.1f), RealmKit.Wood, false, new Vector3(-38f, 0f, 0f));
        }

        /// <summary>A yard plot with four water levels: 0 dry, 1 wet, 2 flooded, 3 drowned. Seedlings, or one sapling.</summary>
        private static Transform YardPlot(Ctx ctx, Transform w, string name, string id, Vector3 pos, float width,
            Material dryEarth, Material wetEarth, Material sprout, bool seedlings, int seed)
        {
            Transform plot = RealmKit.Group(name, w, pos);
            RealmKit.Box("BankL", plot, new Vector3(-width * 0.5f - 0.15f, 0.12f, 0f), new Vector3(0.3f, 0.24f, 2.6f), RealmKit.Mud, false);
            RealmKit.Box("BankR", plot, new Vector3(width * 0.5f + 0.15f, 0.12f, 0f), new Vector3(0.3f, 0.24f, 2.6f), RealmKit.Mud, false);
            RealmKit.Box("BankF", plot, new Vector3(0f, 0.08f, -1.3f), new Vector3(width + 0.6f, 0.16f, 0.25f), RealmKit.Mud, false);
            Transform dry = RealmKit.Group("Dry", plot);
            RealmKit.Box("Bed", dry, new Vector3(0f, 0.02f, 0f), new Vector3(width, 0.04f, 2.4f), dryEarth, false);
            for (int f = 0; f < 4; f++)
                RealmKit.Box("Crack" + f, dry, new Vector3(-width * 0.33f + f * width * 0.22f, 0.045f, 0.1f * (f % 2)), new Vector3(0.04f, 0.01f, 1.5f), RealmKit.Shadow, false, new Vector3(0f, 18f - f * 11f, 0f));
            Transform wet = RealmKit.Group("Wet", plot);
            RealmKit.Box("Bed", wet, new Vector3(0f, 0.025f, 0f), new Vector3(width, 0.04f, 2.4f), wetEarth, false);
            for (int f = 0; f < 5; f++)
                RealmKit.Box("Furrow" + f, wet, new Vector3(0f, 0.06f, -0.9f + f * 0.45f), new Vector3(width - 0.2f, 0.06f, 0.12f), RealmKit.Mud, false);
            Transform flood = RealmKit.Group("Flood", plot, new Vector3(0f, -0.05f, 0f));
            RealmKit.Box("Sheet", flood, Vector3.zero, new Vector3(width + 0.2f, 0.04f, 2.6f), RealmKit.WaterRough, false)
                .AddComponent<WaterMotion>().amplitude = 0.03f;

            Transform life;
            var sr = new System.Random(seed);
            if (seedlings)
            {
                life = RealmKit.Group("Shoots", plot);
                for (int s = 0; s < 34; s++)
                {
                    Transform blade = RealmKit.Group("Blade" + s, life, new Vector3(-width * 0.45f + (float)sr.NextDouble() * width * 0.9f, 0.06f, -0.9f + (s % 5) * 0.45f),
                        new Vector3((float)sr.NextDouble() * 14f - 7f, (float)sr.NextDouble() * 360f, 0f));
                    RealmKit.Box("Leaf", blade, new Vector3(0f, 0.16f, 0f), new Vector3(0.04f, 0.32f, 0.02f), sprout, false);
                    var bs = blade.gameObject.AddComponent<Sway>(); bs.degrees = 8f; bs.speed = 1.1f + (float)sr.NextDouble();
                }
            }
            else
            {
                // The father's sapling: a bare stick that puts out a leaf once the plot is right.
                RealmKit.Shape(PrimitiveType.Cylinder, "Stick", plot, new Vector3(0.2f, 0.55f, 0.1f), new Vector3(0.07f, 0.55f, 0.07f), RealmKit.Wood, false, new Vector3(0f, 0f, 5f));
                RealmKit.Shape(PrimitiveType.Cylinder, "Twig", plot, new Vector3(0.32f, 0.95f, 0.1f), new Vector3(0.03f, 0.2f, 0.03f), RealmKit.Wood, false, new Vector3(0f, 0f, -35f));
                life = RealmKit.Group("Leaves", plot, new Vector3(0.24f, 1.12f, 0.1f));
                for (int s = 0; s < 3; s++)
                {
                    Transform lf = RealmKit.Group("Leaf" + s, life, Vector3.zero, new Vector3(0f, s * 120f, 0f));
                    RealmKit.Box("Blade", lf, new Vector3(0f, 0.04f, 0.14f), new Vector3(0.12f, 0.02f, 0.26f), sprout, false, new Vector3(-25f, 0f, 0f));
                }
                var ls = life.gameObject.AddComponent<Sway>(); ls.axis = Vector3.forward; ls.degrees = 6f; ls.speed = 1.3f;
            }
            Vector3 lifeAt = life.localPosition;
            Light glow = InnerGlow(plot, new Vector3(0f, 0.8f, -0.8f), 0f, 4f);
            glow.color = new Color(0.55f, 1f, 0.55f);

            RealmObjectView v = View(ctx, plot, id, new Vector3(0f, 0.8f, 0f));
            var p0 = Pose(v, "0", 1.8f); p0.show.Add(dry.gameObject); p0.hide.Add(wet.gameObject);
            Part(p0, flood, new Vector3(0f, -0.08f, 0f), Vector3.zero);
            Part(p0, life, lifeAt, seedlings ? new Vector3(0f, 0f, 20f) : Vector3.zero, seedlings ? new Vector3(1f, 0.25f, 1f) : Vector3.one * 0.02f);
            p0.lights.Add(glow); p0.lightIntensities.Add(0f);
            var p1 = Pose(v, "1", 3.0f); p1.show.Add(wet.gameObject); p1.hide.Add(dry.gameObject);
            Part(p1, flood, new Vector3(0f, -0.08f, 0f), Vector3.zero);
            Part(p1, life, lifeAt, Vector3.zero, Vector3.one);
            p1.lights.Add(glow); p1.lightIntensities.Add(0.9f);
            var p2 = Pose(v, "2", 2.2f); p2.show.Add(wet.gameObject); p2.hide.Add(dry.gameObject);
            Part(p2, flood, new Vector3(0f, 0.08f, 0f), Vector3.zero);
            Part(p2, life, lifeAt, Vector3.zero, seedlings ? new Vector3(1f, 0.8f, 1f) : Vector3.one * 0.5f);
            p2.lights.Add(glow); p2.lightIntensities.Add(0.2f);
            var p3 = Pose(v, "3", 2.2f); p3.show.Add(wet.gameObject); p3.hide.Add(dry.gameObject);
            Part(p3, flood, new Vector3(0f, 0.2f, 0f), Vector3.zero);
            Part(p3, life, lifeAt + new Vector3(0f, -0.04f, 0f), new Vector3(0f, 0f, seedlings ? 70f : 30f), seedlings ? new Vector3(1f, 0.4f, 1f) : Vector3.one * 0.02f);
            p3.lights.Add(glow); p3.lightIntensities.Add(0f);
            return plot;
        }

        /// <summary>A yard sluice gate with a lifting board, a turning wheel and a run of water shown while it is open.</summary>
        private static void WernesGate(Ctx ctx, Transform w, string name, string id, Vector3 pos, string glyphs, Vector3 runAt, Vector2 runSize)
        {
            Transform sluice = RealmKit.Group(name, w, pos);
            RealmKit.Box("JambL", sluice, new Vector3(-0.7f, 0.9f, 0f), new Vector3(0.36f, 1.8f, 0.7f), RealmKit.Sandstone);
            RealmKit.Box("JambR", sluice, new Vector3(0.7f, 0.9f, 0f), new Vector3(0.36f, 1.8f, 0.7f), RealmKit.Sandstone);
            RealmKit.Box("Head", sluice, new Vector3(0f, 1.95f, 0f), new Vector3(1.9f, 0.3f, 0.8f), RealmKit.Sandstone);
            RealmKit.GlyphPlate("Glyphs", sluice, new Vector3(0f, 1.95f, -0.42f), new Vector3(0f, 180f, 0f), glyphs, 0.2f, false, new Color(1f, 0.78f, 0.4f, 0.55f));
            Transform leaf = RealmKit.Group("Leaf", sluice, new Vector3(0f, 0.6f, 0f));
            RealmKit.Box("Board", leaf, Vector3.zero, new Vector3(1.04f, 1.2f, 0.14f), RealmKit.Wood);
            Transform wheel = RealmKit.Group("Wheel", sluice, new Vector3(0f, 2.3f, -0.2f));
            RealmKit.Shape(PrimitiveType.Cylinder, "Rim", wheel, Vector3.zero, new Vector3(0.55f, 0.03f, 0.55f), RealmKit.Wood, false, new Vector3(90f, 0f, 0f));
            RealmKit.Box("Spoke", wheel, Vector3.zero, new Vector3(0.55f, 0.05f, 0.05f), RealmKit.Wood, false);
            RealmKit.Box("Spoke2", wheel, Vector3.zero, new Vector3(0.05f, 0.55f, 0.05f), RealmKit.Wood, false);
            GameObject run = RealmKit.Box("Run", w, runAt, new Vector3(runSize.x, 0.04f, runSize.y), RealmKit.WaterRough, false);
            run.AddComponent<WaterMotion>().amplitude = 0.025f;
            Light glow = InnerGlow(sluice, new Vector3(0f, 0.6f, -0.8f), 0f, 4f);
            glow.color = new Color(0.6f, 0.8f, 1f);
            Light seal = InnerGlow(sluice, new Vector3(0f, 1.95f, -0.9f), 0f, 3f);
            seal.color = new Color(1f, 0.8f, 0.45f);
            RealmObjectView v = View(ctx, sluice, id, new Vector3(0f, 1.2f, -0.6f));
            var ss = Pose(v, "shut", 0.7f); Part(ss, leaf, new Vector3(0f, 0.6f, 0f), Vector3.zero); Part(ss, wheel, wheel.localPosition, Vector3.zero);
            ss.hide.Add(run); ss.lights.Add(glow); ss.lightIntensities.Add(0f); ss.lights.Add(seal); ss.lightIntensities.Add(1.2f);
            var so = Pose(v, "open", 2.0f); Part(so, leaf, new Vector3(0f, 1.6f, 0f), Vector3.zero); Part(so, wheel, wheel.localPosition, new Vector3(0f, 0f, 170f));
            so.show.Add(run); so.lights.Add(glow); so.lightIntensities.Add(0.9f); so.lights.Add(seal); so.lightIntensities.Add(0f);
        }
    }
}
