using Ankhu.Realms.Core.Content;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Wernes Room B, The Shabti With My Name. Three plots fed from one canal, each with a gate and a blank shabti,
    /// then a river with a footbridge that swings out with the current unless it is bound, and on the far bank a
    /// finer painted shabti that works a lush field under Ankhu's name.
    /// Layout: plots at x -9, -2.6, 3.8, the bridge zone at x 9.2, the river x 11..17, the far bank x 17..24.
    /// </summary>
    public static partial class RealmSceneBuilder
    {
        private static readonly float[] PlotX = { -9f, -2.6f, 3.8f };
        private const float RiverL = 11f, RiverR = 17f;

        private static void BuildShabtiB()
        {
            Ctx ctx = Begin(WernesPaintedScripts.ShabtiB, new Color(0.05f, 0.08f, 0.13f), 0.010f, 0.4f);
            Transform w = ctx.world;
            Ground(w, 140f, RealmKit.Mud);
            Dunes(w, 11, 70f);
            Material clay = RealmKit.Lit("ShabtiClay", new Color(0.30f, 0.50f, 0.52f), 0.35f);
            Material painted = RealmKit.Lit("PaintedFaience", new Color(0.16f, 0.52f, 0.62f), 0.6f);
            Material brick = RealmKit.Lit("MudBrick", new Color(0.46f, 0.34f, 0.24f), 0.1f, 0f,
                RealmKit.StoneTexture("mudbrick", new Color(0.34f, 0.24f, 0.16f), new Color(0.56f, 0.42f, 0.30f), 1.5f, 71), 1.5f);
            Material thread = RealmKit.Emissive("BindThread", new Color(0.3f, 0.9f, 1f), new Color(0.6f, 3.2f, 4.2f));
            Material dryEarth = RealmKit.Lit("DryEarth", new Color(0.62f, 0.50f, 0.34f), 0.05f, 0f,
                RealmKit.StoneTexture("dryearth", new Color(0.48f, 0.38f, 0.26f), new Color(0.72f, 0.60f, 0.42f), 0.4f, 83), 3f);
            Material wetEarth = RealmKit.Lit("WetEarth", new Color(0.20f, 0.16f, 0.12f), 0.7f);
            Material sprout = RealmKit.Lit("Sprout", new Color(0.36f, 0.62f, 0.24f), 0.3f);
            Material grain = RealmKit.Lit("Grain", new Color(0.72f, 0.58f, 0.24f), 0.2f);
            Material lush = RealmKit.Lit("LushGrain", new Color(0.52f, 0.62f, 0.22f), 0.25f);

            // Far fields behind the canal with grey workers bent over them, as in the first field.
            Transform far = RealmKit.Group("FarFields", w);
            var rnd = new System.Random(9);
            for (int row = 0; row < 5; row++)
                RealmKit.Box("Grain" + row, far, new Vector3(-4f, 0.3f, 14f + row * 3f), new Vector3(46f, 0.6f, 1.6f), grain, false);
            for (int i = 0; i < 9; i++)
            {
                Transform grey = RealmKit.Group("GreyOne" + i, far, new Vector3(-24f + (float)rnd.NextDouble() * 38f, 0f, 15f + (float)rnd.NextDouble() * 12f));
                Transform bent = RealmKit.Group("Bent", grey, Vector3.zero, new Vector3(35f, (float)rnd.NextDouble() * 360f, 0f));
                RealmKit.Shape(PrimitiveType.Capsule, "Body", bent, new Vector3(0f, 0.8f, 0f), new Vector3(0.45f, 0.8f, 0.45f),
                    RealmKit.Lit("GreySoul", new Color(0.45f, 0.47f, 0.5f), 0.05f));
                var sw = bent.gameObject.AddComponent<Sway>(); sw.axis = Vector3.right; sw.degrees = 9f; sw.speed = 0.7f + (float)rnd.NextDouble() * 0.5f;
            }

            // The canal behind the plots, ending where it spills into the river.
            float canalL = -22f, canalW = RiverL - canalL, canalC = (canalL + RiverL) * 0.5f;
            RealmKit.Box("CanalBed", w, new Vector3(canalC, -0.9f, 4.6f), new Vector3(canalW, 0.2f, 3.2f), RealmKit.Mud, false);
            RealmKit.Box("CanalWater", w, new Vector3(canalC, -0.15f, 4.6f), new Vector3(canalW, 0.05f, 3.2f), RealmKit.Water, false)
                .AddComponent<WaterMotion>().amplitude = 0.015f;
            RealmKit.Box("CanalBank", w, new Vector3(canalC, -0.1f, 6.5f), new Vector3(canalW, 0.4f, 0.8f), RealmKit.Limestone, false);
            Reeds(w, new Vector3(canalC, 0f, 7.4f), new Vector3(canalW - 2f, 0f, 1.4f), 220, 41);

            // The surge: a swell that runs down the canal the moment the named shabti stops.
            Transform swell = RealmKit.Group("Swell", w, new Vector3(canalC, -0.6f, 4.6f));
            GameObject crest = RealmKit.Box("Crest", swell, Vector3.zero, new Vector3(canalW, 0.5f, 3.0f), RealmKit.WaterRough, false);
            crest.AddComponent<WaterMotion>().amplitude = 0.08f;
            Light surgeLight = InnerGlow(swell, new Vector3(0f, 1.5f, -1.5f), 0f, 14f);
            surgeLight.color = new Color(0.5f, 0.8f, 1f);
            var surgeRoot = new GameObject("Surge").transform; surgeRoot.SetParent(w, false);
            RealmObjectView uv = surgeRoot.gameObject.AddComponent<RealmObjectView>(); uv.objectId = "surge"; ctx.director.views.Add(uv);
            foreach (string st in new[] { "none", "spent" })
            {
                var p = Pose(uv, st, 2.4f); Part(p, swell, new Vector3(canalC, -0.6f, 4.6f), Vector3.zero);
                p.lights.Add(surgeLight); p.lightIntensities.Add(0f);
                // At rest the canal is painted, so a sunken swell would show through it as a blue strip.
                p.hide.Add(crest);
            }
            foreach (string st in new[] { "pending", "hit" })
            {
                var p = Pose(uv, st, 0.9f); Part(p, swell, new Vector3(canalC, 0.25f, 4.6f), Vector3.zero);
                p.lights.Add(surgeLight); p.lightIntensities.Add(2.6f);
                p.show.Add(crest);
            }

            // A painted wall at the entrance: a shabti in a fine collar, a cartouche on its chest.
            Transform vig = RealmKit.Group("Vignette", w, new Vector3(-13.4f, 0f, 2.4f));
            RealmKit.Box("Wall", vig, new Vector3(0f, 1.3f, 0f), new Vector3(2.6f, 2.6f, 0.4f), brick);
            RealmKit.Box("Panel", vig, new Vector3(0f, 1.45f, -0.21f), new Vector3(2.1f, 1.5f, 0.02f), RealmKit.Lit("Plaster", new Color(0.86f, 0.80f, 0.66f), 0.1f), false);
            RealmKit.GlyphPlate("Scene", vig, new Vector3(0f, 1.45f, -0.24f), new Vector3(0f, 180f, 0f), "A53 S34 N35 Aa1 G43", 0.34f, false, new Color(0.25f, 0.4f, 0.75f, 0.9f));

            // Three plots, each with its own gate from the canal and its own blank shabti.
            for (int k = 0; k < 3; k++)
            {
                int i = k + 1;
                float x = PlotX[k];
                Transform plot = RealmKit.Group("Plot" + i, w, new Vector3(x, 0f, 1.5f));
                RealmKit.Box("BankL", plot, new Vector3(-2.55f, 0.12f, 0f), new Vector3(0.3f, 0.24f, 2.4f), RealmKit.Mud, false);
                RealmKit.Box("BankR", plot, new Vector3(2.55f, 0.12f, 0f), new Vector3(0.3f, 0.24f, 2.4f), RealmKit.Mud, false);
                RealmKit.Box("BankF", plot, new Vector3(0f, 0.08f, -1.2f), new Vector3(5.4f, 0.16f, 0.25f), RealmKit.Mud, false);
                Transform dry = RealmKit.Group("Dry", plot);
                RealmKit.Box("Bed", dry, new Vector3(0f, 0.02f, 0f), new Vector3(4.8f, 0.04f, 2.2f), dryEarth, false);
                for (int f = 0; f < 4; f++)
                    RealmKit.Box("Crack" + f, dry, new Vector3(-1.6f + f * 1.1f, 0.045f, 0.1f * (f % 2)), new Vector3(0.04f, 0.01f, 1.4f), RealmKit.Shadow, false, new Vector3(0f, 18f - f * 11f, 0f));
                Transform wet = RealmKit.Group("Wet", plot);
                RealmKit.Box("Bed", wet, new Vector3(0f, 0.025f, 0f), new Vector3(4.8f, 0.04f, 2.2f), wetEarth, false);
                for (int f = 0; f < 5; f++)
                    RealmKit.Box("Furrow" + f, wet, new Vector3(0f, 0.06f, -0.85f + f * 0.42f), new Vector3(4.6f, 0.06f, 0.12f), RealmKit.Mud, false);
                Transform shoots = RealmKit.Group("Shoots", wet);
                var sr = new System.Random(100 + i);
                for (int s = 0; s < 26; s++)
                {
                    Transform blade = RealmKit.Group("Blade" + s, shoots, new Vector3(-2.1f + (float)sr.NextDouble() * 4.2f, 0.06f, -0.85f + (s % 5) * 0.42f),
                        new Vector3((float)sr.NextDouble() * 14f - 7f, (float)sr.NextDouble() * 360f, 0f));
                    RealmKit.Box("Leaf", blade, new Vector3(0f, 0.16f, 0f), new Vector3(0.04f, 0.32f, 0.02f), sprout, false);
                    var bs = blade.gameObject.AddComponent<Sway>(); bs.degrees = 7f; bs.speed = 1.1f + (float)sr.NextDouble();
                }
                Transform flood = RealmKit.Group("Flood", plot, new Vector3(0f, -0.05f, 0f));
                RealmKit.Box("Sheet", flood, Vector3.zero, new Vector3(5.0f, 0.04f, 2.4f), RealmKit.WaterRough, false)
                    .AddComponent<WaterMotion>().amplitude = 0.03f;
                for (int s = 0; s < 6; s++)
                    RealmKit.Shape(PrimitiveType.Sphere, "Drowned" + s, flood, new Vector3(-2f + s * 0.8f, 0.03f, -0.4f + (s % 3) * 0.4f), new Vector3(0.12f, 0.02f, 0.05f), RealmKit.ReedDry);
                RealmObjectView pv = View(ctx, plot, "plot" + i, new Vector3(0f, 0.6f, 0f));
                var pd = Pose(pv, "dry", 1.6f); pd.show.Add(dry.gameObject); pd.hide.Add(wet.gameObject); pd.hide.Add(flood.gameObject);
                Part(pd, shoots, Vector3.zero, Vector3.zero, new Vector3(1f, 0.01f, 1f)); Part(pd, flood, new Vector3(0f, -0.05f, 0f), Vector3.zero);
                var pw = Pose(pv, "wet", 3.0f); pw.show.Add(wet.gameObject); pw.hide.Add(dry.gameObject); pw.hide.Add(flood.gameObject);
                Part(pw, shoots, Vector3.zero, Vector3.zero, Vector3.one); Part(pw, flood, new Vector3(0f, -0.05f, 0f), Vector3.zero);
                var pf = Pose(pv, "drowned", 2.2f); pf.show.Add(flood.gameObject); pf.show.Add(wet.gameObject); pf.hide.Add(dry.gameObject);
                Part(pf, shoots, Vector3.zero, new Vector3(0f, 0f, 70f), new Vector3(1f, 0.4f, 1f)); Part(pf, flood, new Vector3(0f, 0.16f, 0f), Vector3.zero);

                // The gate in the canal bank and the short channel that feeds the plot.
                Transform sluice = RealmKit.Group("Sluice" + i, w, new Vector3(x - 1.2f, 0f, 3.4f));
                RealmKit.Box("JambL", sluice, new Vector3(-0.8f, 1.0f, 0f), new Vector3(0.4f, 2.0f, 0.8f), RealmKit.Sandstone);
                RealmKit.Box("JambR", sluice, new Vector3(0.8f, 1.0f, 0f), new Vector3(0.4f, 2.0f, 0.8f), RealmKit.Sandstone);
                RealmKit.Box("Head", sluice, new Vector3(0f, 2.15f, 0f), new Vector3(2.2f, 0.32f, 0.9f), RealmKit.Sandstone);
                RealmKit.GlyphPlate("SluiceGlyphs", sluice, new Vector3(0f, 2.15f, -0.47f), new Vector3(0f, 180f, 0f),
                    i == 1 ? "Z1 O31" : i == 2 ? "Z1 Z1 O31" : "Z1 Z1 Z1 O31", 0.2f, false, new Color(1f, 0.78f, 0.4f, 0.55f));
                Transform leaf = RealmKit.Group("Leaf", sluice, new Vector3(0f, 0.7f, 0f));
                RealmKit.Box("Board", leaf, Vector3.zero, new Vector3(1.2f, 1.4f, 0.14f), RealmKit.Wood);
                Transform wheel = RealmKit.Group("Wheel", sluice, new Vector3(0f, 2.5f, -0.2f));
                RealmKit.Shape(PrimitiveType.Cylinder, "Rim", wheel, Vector3.zero, new Vector3(0.6f, 0.03f, 0.6f), RealmKit.Wood, false, new Vector3(90f, 0f, 0f));
                RealmKit.Box("Spoke", wheel, Vector3.zero, new Vector3(0.6f, 0.05f, 0.05f), RealmKit.Wood, false);
                GameObject channel = RealmKit.Box("ChannelWater", w, new Vector3(x - 1.2f, -0.04f, 2.6f), new Vector3(0.9f, 0.04f, 1.6f), RealmKit.Water, false);
                channel.AddComponent<WaterMotion>().amplitude = 0.02f;
                Light gateGlow = InnerGlow(sluice, new Vector3(0f, 0.6f, -0.8f), 0f, 4f);
                gateGlow.color = new Color(0.6f, 0.8f, 1f);
                RealmObjectView sv = View(ctx, sluice, "sluice" + i, new Vector3(0f, 1.2f, -0.6f));
                var ss = Pose(sv, "shut", 0.6f); Part(ss, leaf, new Vector3(0f, 0.7f, 0f), Vector3.zero); Part(ss, wheel, wheel.localPosition, Vector3.zero);
                ss.hide.Add(channel); ss.lights.Add(gateGlow); ss.lightIntensities.Add(0f);
                var so = Pose(sv, "open", 2.0f); Part(so, leaf, new Vector3(0f, 1.8f, 0f), Vector3.zero); Part(so, wheel, wheel.localPosition, new Vector3(0f, 0f, 170f));
                so.show.Add(channel); so.lights.Add(gateGlow); so.lightIntensities.Add(0.9f);

                // The blank shabti carries from the channel mouth across its plot and back.
                Transform sh = Shabti(ctx, w, "Shabti" + i, "shabti" + i, new Vector3(x + 1.9f, 0f, 0.4f), clay, true,
                    new Vector3(-2.9f, 0f, 1.6f), new Vector3(0.2f, 0f, 0.2f), 4.2f);
                RealmObjectView shv = sh.GetComponent<RealmObjectView>();
                Transform head = sh.Find("Worker/Head"), eyes = sh.Find("Worker/Head/Eyes");
                var poured = Pose(shv, "poured", 0.6f); Part(poured, head, head.localPosition, new Vector3(18f, 0f, 0f)); poured.show.Add(eyes.gameObject);
                RealmKit.Shape(PrimitiveType.Cylinder, "Mark", sh, new Vector3(0f, 0.01f, 0f), new Vector3(0.7f, 0.01f, 0.7f), RealmKit.Shadow);
            }

            // The river and its banks. It runs toward the camera, so the free bridge swings out over the near water.
            RealmKit.Box("RiverBed", w, new Vector3((RiverL + RiverR) * 0.5f, -1.2f, 1f), new Vector3(RiverR - RiverL, 0.2f, 40f), RealmKit.Mud, false);
            RealmKit.Box("River", w, new Vector3((RiverL + RiverR) * 0.5f, -0.25f, 1f), new Vector3(RiverR - RiverL, 0.05f, 40f), RealmKit.WaterRough, false)
                .AddComponent<WaterMotion>().amplitude = 0.05f;
            RealmKit.Box("RiverGlass", w, new Vector3((RiverL + RiverR) * 0.5f, -0.22f, 1f), new Vector3(RiverR - RiverL - 1f, 0.04f, 40f), RealmKit.Water, false)
                .AddComponent<WaterMotion>().amplitude = 0.02f;
            RealmKit.Box("NearBank", w, new Vector3(RiverL - 0.2f, -0.1f, 1f), new Vector3(0.5f, 0.4f, 40f), RealmKit.Limestone, false);
            RealmKit.Box("FarBank", w, new Vector3(RiverR + 0.2f, -0.1f, 1f), new Vector3(0.5f, 0.4f, 40f), RealmKit.Limestone, false);
            Reeds(w, new Vector3(RiverL - 0.6f, 0f, 6f), new Vector3(0.8f, 0f, 5f), 60, 47);
            Reeds(w, new Vector3(RiverR + 0.6f, 0f, 6f), new Vector3(0.8f, 0f, 5f), 60, 49);
            RealmKit.Motes(w, new Vector3((RiverL + RiverR) * 0.5f, 0.4f, 0f), new Vector3(5f, 0.6f, 16f), new Color(0.6f, 0.85f, 1f, 0.6f), 50);

            // Fixed posts on both banks; the near post carries a swinging lamp.
            foreach (float px in new[] { RiverL, RiverR })
                foreach (float pz in new[] { -0.75f, 0.75f })
                    RealmKit.Shape(PrimitiveType.Cylinder, "BridgePost", w, new Vector3(px, 0.6f, pz), new Vector3(0.18f, 0.6f, 0.18f), RealmKit.Wood, true);
            Transform lamp = RealmKit.Group("BridgeLamp", w, new Vector3(RiverL, 1.3f, -0.75f));
            Transform lampHang = RealmKit.Group("Hang", lamp);
            RealmKit.Box("Cord", lampHang, new Vector3(0f, -0.2f, 0f), new Vector3(0.02f, 0.4f, 0.02f), RealmKit.ReedDry, false);
            RealmKit.Shape(PrimitiveType.Sphere, "Flame", lampHang, new Vector3(0f, -0.45f, 0f), Vector3.one * 0.14f, RealmKit.GoldGlow);
            InnerGlow(lampHang, new Vector3(0f, -0.45f, -0.2f), 1.2f, 6f);
            var ls = lampHang.gameObject.AddComponent<Sway>(); ls.degrees = 8f; ls.speed = 1.4f;

            // The footbridge pivots on the near bank. Free, it swings downstream and bobs. Held, it spans the river.
            Transform bridge = RealmKit.Group("Bridge", w, new Vector3(RiverL, 0f, 0f), new Vector3(0f, 62f, 0f));
            Transform deck = RealmKit.Group("Deck", bridge);
            for (int p = 0; p < 12; p++)
                RealmKit.Box("Plank" + p, deck, new Vector3(0.25f + p * 0.5f, 0.36f, 0f), new Vector3(0.44f, 0.08f, 1.4f), RealmKit.Wood, false, new Vector3(0f, (p % 3 - 1) * 2f, 0f));
            RealmKit.Box("StringerL", deck, new Vector3(3f, 0.29f, -0.6f), new Vector3(6.1f, 0.08f, 0.1f), RealmKit.Wood, false);
            RealmKit.Box("StringerR", deck, new Vector3(3f, 0.29f, 0.6f), new Vector3(6.1f, 0.08f, 0.1f), RealmKit.Wood, false);
            RealmKit.Box("RopeRail", deck, new Vector3(3f, 1.0f, -0.72f), new Vector3(6.0f, 0.03f, 0.03f), RealmKit.ReedDry, false);
            var bob = deck.gameObject.AddComponent<Sway>(); bob.axis = Vector3.right; bob.degrees = 2.5f; bob.speed = 1.7f;
            Transform holdT = RealmKit.Group("Threads", w);
            RealmKit.Box("ThreadL", holdT, new Vector3((RiverL + RiverR) * 0.5f, 1.2f, -0.75f), new Vector3(RiverR - RiverL, 0.03f, 0.03f), thread, false);
            RealmKit.Box("ThreadR", holdT, new Vector3((RiverL + RiverR) * 0.5f, 1.2f, 0.75f), new Vector3(RiverR - RiverL, 0.03f, 0.03f), thread, false);
            RealmKit.Shape(PrimitiveType.Sphere, "KnotNear", holdT, new Vector3(RiverL, 1.2f, -0.75f), Vector3.one * 0.16f, thread);
            RealmKit.Shape(PrimitiveType.Sphere, "KnotFar", holdT, new Vector3(RiverR, 1.2f, -0.75f), Vector3.one * 0.16f, thread);
            Light bindLight = InnerGlow(holdT, new Vector3((RiverL + RiverR) * 0.5f, 1.6f, -1.2f), 0f, 7f);
            bindLight.color = new Color(0.4f, 0.9f, 1f);
            RealmObjectView brv = View(ctx, bridge, "bridge", new Vector3(3f, 1.2f, 0f));
            var bf = Pose(brv, "free", 2.2f); Part(bf, bridge, bridge.localPosition, new Vector3(0f, 62f, 0f)); bf.hide.Add(holdT.gameObject);
            bf.lights.Add(bindLight); bf.lightIntensities.Add(0f);
            var bh = Pose(brv, "held", 1.6f); Part(bh, bridge, bridge.localPosition, Vector3.zero); bh.show.Add(holdT.gameObject);
            bh.lights.Add(bindLight); bh.lightIntensities.Add(2.0f);

            // The far bank: a field far greener than ours, palms, and the painted shabti working it under Ankhu's name.
            Transform farField = RealmKit.Group("FarField", w, new Vector3(20.6f, 0f, 2.4f));
            for (int row = 0; row < 4; row++)
                RealmKit.Box("Lush" + row, farField, new Vector3(0f, 0.35f, row * 0.7f), new Vector3(6.2f, 0.7f, 0.45f), lush, false);
            var pr = new System.Random(77);
            foreach (var pp in new[] { new Vector3(18.2f, 0f, 5.2f), new Vector3(23.2f, 0f, 4.6f), new Vector3(25.6f, 0f, 7.5f) })
            {
                Transform palm = RealmKit.Group("Palm", w, pp);
                RealmKit.Shape(PrimitiveType.Cylinder, "Trunk", palm, new Vector3(0f, 2.4f, 0f), new Vector3(0.28f, 2.4f, 0.28f), RealmKit.Wood, true, new Vector3(0f, 0f, 4f));
                Transform crown = RealmKit.Group("Crown", palm, new Vector3(0.3f, 4.8f, 0f));
                for (int f = 0; f < 7; f++)
                {
                    Transform frond = RealmKit.Group("Frond" + f, crown, Vector3.zero, new Vector3(0f, f * 51f + (float)pr.NextDouble() * 10f, 0f));
                    RealmKit.Box("Leaf", frond, new Vector3(0f, -0.25f, 1.0f), new Vector3(0.35f, 0.04f, 2.0f), RealmKit.Reed, false, new Vector3(22f, 0f, 0f));
                }
                var cs = crown.gameObject.AddComponent<Sway>(); cs.axis = Vector3.forward; cs.degrees = 4f; cs.speed = 0.8f;
            }

            Transform named = Shabti(ctx, w, "NamedShabti", "named", new Vector3(20.4f, 0f, 1.0f), painted, true,
                new Vector3(-2.6f, 0f, 0f), new Vector3(2.4f, 0f, 0.6f), 4.8f);
            Transform nWorker = named.Find("Worker");
            RealmKit.Shape(PrimitiveType.Cylinder, "Collar", nWorker, new Vector3(0f, 1.42f, 0f), new Vector3(0.62f, 0.06f, 0.56f), RealmKit.Gold);
            RealmKit.Box("Wig", named.Find("Worker/Head"), new Vector3(0f, 0.1f, 0.04f), new Vector3(0.38f, 0.42f, 0.34f), RealmKit.Lapis, false);
            RealmKit.Box("Kilt", nWorker, new Vector3(0f, 0.5f, 0f), new Vector3(0.56f, 0.3f, 0.48f), RealmKit.Gold, false);
            // Rename the helper's poses to the named shabti's states, and sit it down when it is stilled.
            RealmObjectView nv = named.GetComponent<RealmObjectView>();
            foreach (var p in nv.poses) p.state = p.state == "asleep" ? "still" : "working";
            var nStill = nv.poses.Find(p => p.state == "still");
            Part(nStill, nWorker, new Vector3(0f, -0.3f, 0f), new Vector3(0f, 0f, 0f));
            nStill.seconds = 2.4f;

            // Its chest: a cartouche that catches the light only once it has been read.
            Transform chest = RealmKit.Group("Chest", nWorker, new Vector3(0f, 1.05f, -0.24f));
            RealmKit.GlyphPlate("Cartouche", chest, Vector3.zero, new Vector3(0f, 180f, 0f), "S34 N35 Aa1 G43", 0.11f, true, new Color(1f, 0.8f, 0.4f, 0.9f));
            Light nameLight = InnerGlow(chest, new Vector3(0f, 0f, -0.3f), 0f, 3f);
            RealmObjectView cv = View(ctx, chest, "chest", new Vector3(0f, 0.3f, -0.1f));
            var cu = Pose(cv, "unread", 0.4f); cu.lights.Add(nameLight); cu.lightIntensities.Add(0.15f);
            var cr = Pose(cv, "read", 2.0f); cr.lights.Add(nameLight); cr.lightIntensities.Add(2.4f);

            Cast(ctx, new Vector3(-15.6f, 0f, -0.6f), 90f, new Vector3(-16.6f, 0f, -1.2f));

            // Crossing: step up onto the held deck, walk it, step down on the far bank.
            var placeRoot = new GameObject("AnkhuSide").transform; placeRoot.SetParent(w, false);
            RealmObjectView av = placeRoot.gameObject.AddComponent<RealmObjectView>(); av.objectId = "side"; ctx.director.views.Add(av);
            Pose(av, "near", 0.1f); Pose(av, "far", 0.1f);
            var ride = placeRoot.gameObject.AddComponent<RealmRide>();
            ride.view = av;
            ride.rider = ctx.ankhu.transform;
            ride.forwardState = "far";
            ride.speed = 1.8f;
            ride.marks = new[]
            {
                new Vector3(RiverL - 0.8f, 0f, 0f),
                new Vector3(RiverL + 0.2f, 0.42f, 0f),
                new Vector3(RiverR - 0.2f, 0.42f, 0f),
                new Vector3(RiverR + 0.9f, 0f, 0f),
            };

            RealmKit.Brazier("WestLamp", w, new Vector3(-16.2f, 0f, 1.6f), 9f, 2.2f);
            RealmKit.Brazier("MidLamp", w, new Vector3(-5.8f, 0f, 2.8f), 8f, 1.8f);
            RealmKit.Brazier("GateLamp", w, new Vector3(7.4f, 0f, 2.6f), 8f, 1.8f);
            RealmKit.Brazier("FarLamp", w, new Vector3(23.4f, 0f, 1.8f), 8f, 2.0f);
            RealmKit.Motes(w, new Vector3(3f, 2.5f, 1f), new Vector3(42f, 5f, 6f), new Color(1f, 0.9f, 0.6f, 0.7f), 110);

            // Lane blockers on each bank, and the river edges that only the held bridge crosses.
            float nearC = (-17.6f + RiverL) * 0.5f, nearW = RiverL + 17.6f;
            RealmKit.Blocker(w, new Vector3(nearC, 3f, -2.4f), new Vector3(nearW, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(nearC, 3f, 3.2f), new Vector3(nearW, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(-17.6f, 3f, 0f), new Vector3(0.4f, 6f, 6f));
            RealmKit.Blocker(w, new Vector3(RiverL - 0.1f, 3f, 0f), new Vector3(0.3f, 6f, 6f));
            RealmKit.Blocker(w, new Vector3(RiverR + 0.1f, 3f, 0f), new Vector3(0.3f, 6f, 6f));
            RealmKit.Blocker(w, new Vector3(20.6f, 3f, -2.4f), new Vector3(7.4f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(20.6f, 3f, 3.2f), new Vector3(7.4f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(24.2f, 3f, 0f), new Vector3(0.4f, 6f, 6f));

            Zone(ctx, "vignette", new Vector3(-13.4f, 1f, 0.2f), new Vector3(2.4f, 4f, 5f));
            for (int k = 0; k < 3; k++) Zone(ctx, "plot" + (k + 1), new Vector3(PlotX[k], 1f, 0.2f), new Vector3(5.4f, 4f, 5f));
            Zone(ctx, "bridge", new Vector3(9.2f, 1f, 0.2f), new Vector3(3.4f, 4f, 5f));
            Zone(ctx, "farbank", new Vector3(20.6f, 1f, 0.2f), new Vector3(7f, 4f, 5f));

            Shot(ctx, "cam_west", new Vector3(-5.6f, 4.4f, -16f), new Vector3(-5.6f, 1.9f, 1f), 40f, new Vector3(-6f, 3f, 0f), new Vector3(23f, 10f, 8f), 0.05f, 0);
            Shot(ctx, "cam_east", new Vector3(14.4f, 4.6f, -16f), new Vector3(14.4f, 2.0f, 1f), 40f, new Vector3(15f, 3f, 0f), new Vector3(19f, 12f, 8f), 0.05f, 1);
            ApplyPaint(ctx, WernesPaintedScripts.ShabtiB, new[]
            {
                new Rect4(new Vector3(-3.4f, 0.004f, 0.5f), new Vector2(28.4f, 12f)),
                new Rect4(new Vector3(20.6f, 0.004f, 0.5f), new Vector2(7.4f, 12f)),
            });
            Finish(ctx, WernesPaintedScripts.ShabtiB);
        }
    }
}
