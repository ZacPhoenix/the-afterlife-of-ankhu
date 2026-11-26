using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ankhu.Realms.Core.Content;
using UnityEditor;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Wernes in story bible order, built side-on for the painted 2.5D look (spec v2). Only builds the new
    /// Wernes_* scenes. Never touches the Realm_0x scenes.
    ///   Unity -batchmode -projectPath . -executeMethod Ankhu.Realms.EditorTools.RealmSceneBuilder.BuildWernesPaintedCli -logFile wernes.log
    /// Layout convention: x runs across the screen, y is up, z is depth. The walk lane is z -2..2.
    /// </summary>
    public static partial class RealmSceneBuilder
    {
        [MenuItem("Ankhu/Realms/Build Wernes Painted (bible order)")]
        public static void BuildWernesPaintedMenu() => BuildWernesPainted();

        public static void BuildWernesPaintedCli()
        {
            try
            {
                BuildWernesPainted();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildWernesPainted()
        {
            RealmKit.EnsureDirs();
            BuildFieldA();
            BuildShabtiB();
            BuildHouseC();
            BuildBricksD();
            AddWernesToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Realms] Wernes painted scenes built.");
        }

        /// <summary>Appends the Wernes_* scenes at the end of Build Settings, sorted, without moving anything else.</summary>
        public static void AddWernesToBuildSettings()
        {
            string[] ours = AssetDatabase.FindAssets("t:Scene", new[] { RealmKit.SceneDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileName(p).StartsWith("Wernes_"))
                .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal).ToArray();
            var list = EditorBuildSettings.scenes.Where(s => !ours.Contains(s.path)).ToList();
            list.AddRange(ours.Select(p => new EditorBuildSettingsScene(p, true)));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ Room A: The Field That Waits

        private static readonly Vector3 ShadufPivot = new Vector3(7f, 3.2f, 0.5f);
        private const float BeamHalf = 3.5f, BeamUp = 22f, BeamDown = -40f;

        private static Vector3 BeamEnd(float deg, float along)
        {
            float r = deg * Mathf.Deg2Rad;
            return ShadufPivot + new Vector3(Mathf.Cos(r) * along, Mathf.Sin(r) * along, 0f);
        }

        private static void BuildFieldA()
        {
            Ctx ctx = Begin(WernesPaintedScripts.FieldA, new Color(0.05f, 0.08f, 0.13f), 0.010f, 0.4f);
            Transform w = ctx.world;
            Ground(w, 140f, RealmKit.Mud);
            Dunes(w, 7, 70f);
            Material clay = RealmKit.Lit("ShabtiClay", new Color(0.30f, 0.50f, 0.52f), 0.35f);
            Material brick = RealmKit.Lit("MudBrick", new Color(0.46f, 0.34f, 0.24f), 0.1f, 0f,
                RealmKit.StoneTexture("mudbrick", new Color(0.34f, 0.24f, 0.16f), new Color(0.56f, 0.42f, 0.30f), 1.5f, 71), 1.5f);
            Material thread = RealmKit.Emissive("BindThread", new Color(0.3f, 0.9f, 1f), new Color(0.6f, 3.2f, 4.2f));
            Material falconClay = RealmKit.Lit("FalconClay", new Color(0.62f, 0.36f, 0.22f), 0.25f);

            // Far fields with grey workers who never look up, and a band of gold grain.
            Transform far = RealmKit.Group("FarFields", w);
            var rnd = new System.Random(5);
            for (int row = 0; row < 5; row++)
                RealmKit.Box("Grain" + row, far, new Vector3(0f, 0.3f, 14f + row * 3f), new Vector3(70f, 0.6f, 1.6f),
                    RealmKit.Lit("Grain", new Color(0.72f, 0.58f, 0.24f), 0.2f), false);
            for (int i = 0; i < 11; i++)
            {
                Transform grey = RealmKit.Group("GreyOne" + i, far, new Vector3(-24f + (float)rnd.NextDouble() * 48f, 0f, 15f + (float)rnd.NextDouble() * 12f));
                Transform bent = RealmKit.Group("Bent", grey, Vector3.zero, new Vector3(35f, (float)rnd.NextDouble() * 360f, 0f));
                RealmKit.Shape(PrimitiveType.Capsule, "Body", bent, new Vector3(0f, 0.8f, 0f), new Vector3(0.45f, 0.8f, 0.45f),
                    RealmKit.Lit("GreySoul", new Color(0.45f, 0.47f, 0.5f), 0.05f));
                var sw = bent.gameObject.AddComponent<Sway>(); sw.axis = Vector3.right; sw.degrees = 9f; sw.speed = 0.7f + (float)rnd.NextDouble() * 0.5f;
            }

            // The canal behind everything, full of stars, with reeds on both banks.
            RealmKit.Box("CanalBed", w, new Vector3(0f, -0.9f, 4.6f), new Vector3(70f, 0.2f, 3.2f), RealmKit.Mud, false);
            RealmKit.Box("CanalWater", w, new Vector3(0f, -0.15f, 4.6f), new Vector3(70f, 0.05f, 3.2f), RealmKit.Water, false)
                .AddComponent<WaterMotion>().amplitude = 0.015f;
            RealmKit.Box("CanalBank", w, new Vector3(0f, -0.1f, 6.5f), new Vector3(70f, 0.4f, 0.8f), RealmKit.Limestone, false);
            Reeds(w, new Vector3(0f, 0f, 7.4f), new Vector3(60f, 0f, 1.4f), 280, 31);
            Reeds(w, new Vector3(-15f, 0f, 3.6f), new Vector3(3f, 0f, 1.6f), 50, 33);

            // Sluice from the canal into a short channel that ends in the trough.
            Transform sluice = RealmKit.Group("Sluice", w, new Vector3(-11.5f, 0f, 2.6f));
            RealmKit.Box("JambL", sluice, new Vector3(-0.9f, 1.1f, 0f), new Vector3(0.45f, 2.2f, 0.9f), RealmKit.Sandstone);
            RealmKit.Box("JambR", sluice, new Vector3(0.9f, 1.1f, 0f), new Vector3(0.45f, 2.2f, 0.9f), RealmKit.Sandstone);
            RealmKit.Box("Head", sluice, new Vector3(0f, 2.35f, 0f), new Vector3(2.4f, 0.36f, 1.0f), RealmKit.Sandstone);
            RealmKit.GlyphPlate("SluiceGlyphs", sluice, new Vector3(0f, 2.35f, -0.52f), new Vector3(0f, 180f, 0f), "E34 N35 O31", 0.24f, false, new Color(1f, 0.78f, 0.4f, 0.55f));
            Transform leaf = RealmKit.Group("Leaf", sluice, new Vector3(0f, 0.75f, 0f));
            RealmKit.Box("Board", leaf, Vector3.zero, new Vector3(1.35f, 1.5f, 0.16f), RealmKit.Wood);
            RealmKit.Box("ChannelBed", w, new Vector3(-9.6f, -0.2f, 1.9f), new Vector3(3.2f, 0.1f, 1.1f), RealmKit.Mud, false);
            GameObject channelWater = RealmKit.Box("ChannelWater", w, new Vector3(-9.6f, -0.06f, 1.9f), new Vector3(3.1f, 0.04f, 1.0f), RealmKit.Water, false);
            channelWater.AddComponent<WaterMotion>().amplitude = 0.02f;
            RealmKit.Box("Trough", w, new Vector3(-7.6f, 0.3f, 1.9f), new Vector3(1.6f, 0.6f, 1.1f), RealmKit.Limestone);
            GameObject troughWater = RealmKit.Box("TroughWater", w, new Vector3(-7.6f, 0.58f, 1.9f), new Vector3(1.4f, 0.04f, 0.9f), RealmKit.WaterRough, false);
            RealmObjectView sv = View(ctx, sluice, "sluice", new Vector3(0f, 1.3f, -0.6f));
            var ss = Pose(sv, "shut", 0.2f); Part(ss, leaf, new Vector3(0f, 0.75f, 0f), Vector3.zero); ss.hide.Add(channelWater); ss.hide.Add(troughWater);
            var so = Pose(sv, "open", 2.0f); Part(so, leaf, new Vector3(0f, 1.95f, 0f), Vector3.zero); so.show.Add(channelWater); so.show.Add(troughWater);

            // A painted wall vignette: a worker seated by a pool, a falcon and the sun in the water.
            Transform vig = RealmKit.Group("Vignette", w, new Vector3(-3.6f, 0f, 2.4f));
            RealmKit.Box("Wall", vig, new Vector3(0f, 1.3f, 0f), new Vector3(2.6f, 2.6f, 0.4f), brick);
            RealmKit.Box("Panel", vig, new Vector3(0f, 1.45f, -0.21f), new Vector3(2.1f, 1.5f, 0.02f), RealmKit.Lit("Plaster", new Color(0.86f, 0.80f, 0.66f), 0.1f), false);
            RealmKit.GlyphPlate("Scene", vig, new Vector3(0f, 1.45f, -0.24f), new Vector3(0f, 180f, 0f), "A53 N37 G5 N5", 0.42f, false, new Color(0.25f, 0.4f, 0.75f, 0.9f));

            // The basin, its perch and the lotus. Water level and stillness are separate views.
            Transform basin = RealmKit.Group("Basin", w, new Vector3(0f, 0f, 0.8f));
            RealmKit.Shape(PrimitiveType.Cylinder, "Plinth", basin, new Vector3(0f, 0.3f, 0f), new Vector3(4.2f, 0.3f, 2.6f), RealmKit.Sandstone, true);
            RealmKit.Shape(PrimitiveType.Cylinder, "Floor", basin, new Vector3(0f, 0.6f, 0f), new Vector3(3.7f, 0.01f, 2.2f), RealmKit.Shadow);
            for (int i = 0; i < 22; i++)
            {
                float a = i * Mathf.PI * 2f / 22f;
                RealmKit.Box("RimStone" + i, basin, new Vector3(Mathf.Cos(a) * 1.95f, 0.82f, Mathf.Sin(a) * 1.18f), new Vector3(0.62f, 0.44f, 0.26f),
                    RealmKit.Limestone, false, new Vector3(0f, -a * Mathf.Rad2Deg + 90f, 0f));
            }
            Transform waterBody = RealmKit.Group("WaterBody", basin, new Vector3(0f, 0.72f, 0f));
            GameObject rippled = RealmKit.Shape(PrimitiveType.Cylinder, "Rippled", waterBody, Vector3.zero, new Vector3(3.6f, 0.02f, 2.1f), RealmKit.WaterRough);
            rippled.AddComponent<WaterMotion>().amplitude = 0.035f;
            GameObject mirror = RealmKit.Shape(PrimitiveType.Cylinder, "Mirror", waterBody, new Vector3(0f, 0.01f, 0f), new Vector3(3.6f, 0.02f, 2.1f), RealmKit.Water);
            Transform sunIn = RealmKit.GlyphPlate("SunInWater", waterBody, new Vector3(0.4f, 0.04f, 0.2f), new Vector3(90f, 0f, 0f), "N5", 0.8f, false, new Color(1f, 0.6f, 0.3f, 0.8f));
            GameObject shadowBar = RealmKit.Box("ThreadShadow", waterBody, new Vector3(0.6f, 0.03f, 0f), new Vector3(3.2f, 0.01f, 0.18f), RealmKit.Shadow, false, new Vector3(0f, 25f, 0f));
            GameObject pour = RealmKit.Box("Pour", basin, new Vector3(-1.9f, 1.35f, -0.2f), new Vector3(0.08f, 0.8f, 0.08f), RealmKit.WaterRough, false);
            Light basinGlow = InnerGlow(basin, new Vector3(0f, 2.0f, 0f), 0f, 7f);
            RealmObjectView bv = View(ctx, basin, "basin", new Vector3(0f, 1.4f, 0f));
            var b0 = Pose(bv, "0", 0.2f); b0.hide.Add(waterBody.gameObject); Part(b0, waterBody, new Vector3(0f, 0.6f, 0f), Vector3.zero);
            var b1 = Pose(bv, "1", 1.8f); b1.show.Add(waterBody.gameObject); Part(b1, waterBody, new Vector3(0f, 0.76f, 0f), Vector3.zero);
            var b2 = Pose(bv, "2", 1.8f); b2.show.Add(waterBody.gameObject); Part(b2, waterBody, new Vector3(0f, 0.94f, 0f), Vector3.zero);
            var settleRoot = new GameObject("Settle").transform; settleRoot.SetParent(basin, false);
            RealmObjectView stv = settleRoot.gameObject.AddComponent<RealmObjectView>(); stv.objectId = "settle"; ctx.director.views.Add(stv);
            foreach (string st in new[] { "none", "carrying" })
            {
                var p = Pose(stv, st, 1.0f); p.show.Add(rippled); p.show.Add(pour); p.hide.Add(mirror); p.hide.Add(sunIn.gameObject); p.hide.Add(shadowBar);
                p.lights.Add(basinGlow); p.lightIntensities.Add(0f);
            }
            var pShadow = Pose(stv, "shadow", 1.5f); pShadow.show.Add(mirror); pShadow.show.Add(shadowBar); pShadow.hide.Add(rippled); pShadow.hide.Add(pour); pShadow.hide.Add(sunIn.gameObject);
            pShadow.lights.Add(basinGlow); pShadow.lightIntensities.Add(0.6f);
            var pStill = Pose(stv, "still", 3.0f); pStill.show.Add(mirror); pStill.show.Add(sunIn.gameObject); pStill.hide.Add(rippled); pStill.hide.Add(pour); pStill.hide.Add(shadowBar);
            pStill.lights.Add(basinGlow); pStill.lightIntensities.Add(3.2f);

            Transform lotus = RealmKit.Group("Lotus", w, new Vector3(-0.6f, 1.0f, 0.9f));
            RealmKit.Shape(PrimitiveType.Cylinder, "Stem", lotus, new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.3f, 0.05f), RealmKit.Reed);
            Transform petals = RealmKit.Group("Petals", lotus, new Vector3(0f, 0.65f, 0f));
            var petalList = new List<Transform>();
            for (int i = 0; i < 8; i++)
            {
                Transform pivot = RealmKit.Group("PetalPivot" + i, petals, Vector3.zero, new Vector3(0f, i * 45f, 0f));
                Transform petal = RealmKit.Group("Petal" + i, pivot, Vector3.zero, new Vector3(-8f, 0f, 0f));
                RealmKit.Shape(PrimitiveType.Sphere, "Leaf", petal, new Vector3(0f, 0.2f, 0.04f), new Vector3(0.13f, 0.28f, 0.06f), RealmKit.Lit("LotusPetal", new Color(0.86f, 0.9f, 1f), 0.4f));
                petalList.Add(petal);
            }
            petals.gameObject.AddComponent<Sway>().degrees = 2f;
            Light lotusLight = InnerGlow(lotus, new Vector3(0f, 1.0f, 0f), 0f, 5f);
            RealmObjectView lv = View(ctx, lotus, "lotus", new Vector3(0f, 0.9f, 0f));
            var lc = Pose(lv, "shut", 0.2f);
            foreach (Transform p in petalList) Part(lc, p, Vector3.zero, new Vector3(-8f, 0f, 0f));
            lc.lights.Add(lotusLight); lc.lightIntensities.Add(0f);
            var lo = Pose(lv, "open", 3.0f);
            foreach (Transform p in petalList) Part(lo, p, Vector3.zero, new Vector3(-62f, 0f, 0f));
            lo.lights.Add(lotusLight); lo.lightIntensities.Add(2.4f);

            Transform perch = RealmKit.Group("Perch", w, new Vector3(1.9f, 0.9f, 0.3f));
            RealmKit.Shape(PrimitiveType.Cylinder, "Post", perch, new Vector3(0f, 0.25f, 0f), new Vector3(0.12f, 0.25f, 0.12f), RealmKit.Sandstone);
            RealmKit.Box("Top", perch, new Vector3(0f, 0.52f, 0f), new Vector3(0.32f, 0.06f, 0.24f), RealmKit.Sandstone, false);

            // Shabti A: carries water from the trough to the basin while the sluice is open.
            Transform shA = Shabti(ctx, w, "ShabtiA", "shabti_a", new Vector3(-6.4f, 0f, 1.0f), clay, true,
                new Vector3(-1.2f, 0f, 0f), new Vector3(3.6f, 0f, 0f), 5.5f);

            // The shaduf: post, pivot, beam with a bucket at one end and a footplate at the other.
            Transform shaduf = RealmKit.Group("Shaduf", w, new Vector3(ShadufPivot.x, 0f, ShadufPivot.z + 0.45f));
            RealmKit.Box("PostL", shaduf, new Vector3(-0.3f, ShadufPivot.y * 0.5f, 0f), new Vector3(0.22f, ShadufPivot.y, 0.22f), RealmKit.Wood);
            RealmKit.Box("PostR", shaduf, new Vector3(0.3f, ShadufPivot.y * 0.5f, 0f), new Vector3(0.22f, ShadufPivot.y, 0.22f), RealmKit.Wood);
            RealmKit.Box("Axle", shaduf, new Vector3(0f, ShadufPivot.y, -0.2f), new Vector3(0.12f, 0.12f, 0.7f), RealmKit.Wood, false);
            RealmKit.Shape(PrimitiveType.Cylinder, "Well", w, new Vector3(3.4f, 0.2f, 1.6f), new Vector3(1.2f, 0.2f, 1.2f), RealmKit.Limestone, true);
            Transform beam = RealmKit.Group("BeamPivot", w, ShadufPivot);
            RealmKit.Box("Beam", beam, Vector3.zero, new Vector3(BeamHalf * 2f + 0.3f, 0.16f, 0.16f), RealmKit.Wood, false);
            Transform plate = RealmKit.Group("Footplate", beam, new Vector3(BeamHalf, 0f, 0f));
            RealmKit.Box("Plate", plate, new Vector3(0.25f, 0.05f, 0f), new Vector3(0.9f, 0.08f, 0.7f), RealmKit.Wood, false);
            Transform bucket = RealmKit.Group("Bucket", beam, new Vector3(-BeamHalf, 0f, 0f));
            Transform hang = RealmKit.Group("Hang", bucket);
            RealmKit.Box("Rope", hang, new Vector3(0f, -0.6f, 0f), new Vector3(0.03f, 1.2f, 0.03f), RealmKit.ReedDry, false);
            RealmKit.Shape(PrimitiveType.Sphere, "Pot", hang, new Vector3(0f, -1.35f, 0f), new Vector3(0.45f, 0.4f, 0.45f), RealmKit.Ochre);
            var hangSway = hang.gameObject.AddComponent<Sway>(); hangSway.degrees = 6f; hangSway.speed = 1.6f;
            // The binding: a cyan knot at the pivot and a thread to the ground stake.
            Transform bind = RealmKit.Group("Binding", beam);
            RealmKit.Shape(PrimitiveType.Cylinder, "Knot", bind, Vector3.zero, new Vector3(0.36f, 0.12f, 0.36f), thread, false, new Vector3(0f, 0f, 90f));
            RealmKit.Shape(PrimitiveType.Cylinder, "Thread", bind, new Vector3(0.9f, 0f, 0f), new Vector3(0.03f, 0.9f, 0.03f), thread, false, new Vector3(0f, 0f, 90f));
            Light bindLight = InnerGlow(bind, new Vector3(0f, 0.3f, -0.4f), 0f, 5f);
            bindLight.color = new Color(0.4f, 0.9f, 1f);
            RealmObjectView beamV = View(ctx, beam, "beam", new Vector3(0f, 0.6f, 0f));
            var bUp = Pose(beamV, "up", 1.2f); Part(bUp, beam, ShadufPivot, new Vector3(0f, 0f, BeamUp)); Part(bUp, plate, plate.localPosition, new Vector3(0f, 0f, -BeamUp)); Part(bUp, bucket, bucket.localPosition, new Vector3(0f, 0f, -BeamUp));
            var bDn = Pose(beamV, "down", 1.2f); Part(bDn, beam, ShadufPivot, new Vector3(0f, 0f, BeamDown)); Part(bDn, plate, plate.localPosition, new Vector3(0f, 0f, -BeamDown)); Part(bDn, bucket, bucket.localPosition, new Vector3(0f, 0f, -BeamDown));
            var holdRoot = new GameObject("Hold").transform; holdRoot.SetParent(w, false);
            RealmObjectView hv = holdRoot.gameObject.AddComponent<RealmObjectView>(); hv.objectId = "hold"; ctx.director.views.Add(hv);
            var hFree = Pose(hv, "free", 0.5f); hFree.hide.Add(bind.gameObject); hFree.lights.Add(bindLight); hFree.lightIntensities.Add(0f);
            var hBound = Pose(hv, "bound", 0.8f); hBound.show.Add(bind.gameObject); hBound.lights.Add(bindLight); hBound.lightIntensities.Add(2.2f);

            // Shabti B works the shaduf rope beside the post.
            Shabti(ctx, w, "ShabtiB", "shabti_b", new Vector3(5.6f, 0f, 1.2f), clay, false,
                new Vector3(-0.25f, 0f, 0f), new Vector3(0.25f, 0f, 0f), 2.4f);

            // The ledge: a mud-brick terrace, Hori's clay falcon set up high on it.
            RealmKit.Box("Ledge", w, new Vector3(13.75f, 2.25f, 0.4f), new Vector3(5.5f, 4.5f, 4.2f), brick);
            RealmKit.Box("LedgeCap", w, new Vector3(13.75f, 4.55f, 0.4f), new Vector3(5.7f, 0.12f, 4.4f), RealmKit.Limestone, false);
            RealmKit.Blocker(w, new Vector3(10.9f, 6.2f, 0.4f), new Vector3(0.2f, 3.2f, 4.2f));
            RealmKit.Blocker(w, new Vector3(16.6f, 6.2f, 0.4f), new Vector3(0.2f, 3.2f, 4.2f));
            RealmKit.Blocker(w, new Vector3(13.75f, 6.2f, -1.8f), new Vector3(5.7f, 3.2f, 0.2f));
            RealmKit.Blocker(w, new Vector3(13.75f, 6.2f, 2.4f), new Vector3(5.7f, 3.2f, 0.2f));

            Cast(ctx, new Vector3(-13.2f, 0f, -0.6f), 90f, new Vector3(-14.4f, 0f, -1.2f));

            Transform falcon = new GameObject("Falcon").transform; falcon.SetParent(w, false);
            GameObject fLedge = Falcon(falcon, "OnLedge", new Vector3(15.2f, 4.62f, 0.6f), falconClay).gameObject;
            Transform hand = RealmKit.Group("FalconHand", ctx.ankhu.transform, new Vector3(0.28f, 1.05f, 0.25f));
            GameObject fHand = Falcon(hand, "InHand", Vector3.zero, falconClay).gameObject;
            GameObject fPerch = Falcon(perch, "OnPerch", new Vector3(0f, 0.56f, 0f), falconClay).gameObject;
            Light falconGlint = InnerGlow(fLedge.transform, new Vector3(0f, 0.5f, -0.3f), 0.9f, 2.5f);
            RealmObjectView fv = falcon.gameObject.AddComponent<RealmObjectView>(); fv.objectId = "falcon"; ctx.director.views.Add(fv);
            void FalconPose(string st, GameObject on)
            {
                var p = Pose(fv, st, 0.4f);
                foreach (GameObject g in new[] { fLedge, fHand, fPerch }) (g == on ? p.show : p.hide).Add(g);
            }
            FalconPose("ledge", fLedge); FalconPose("hand", fHand); FalconPose("perch", fPerch); FalconPose("kept", fHand);

            // Riding the held beam: hop onto the low end, walk up the beam, step off onto the ledge.
            var placeRoot = new GameObject("AnkhuPlace").transform; placeRoot.SetParent(w, false);
            RealmObjectView av = placeRoot.gameObject.AddComponent<RealmObjectView>(); av.objectId = "ankhu"; ctx.director.views.Add(av);
            Pose(av, "ground", 0.1f); Pose(av, "ledge", 0.1f);
            var ride = placeRoot.gameObject.AddComponent<RealmRide>();
            ride.view = av;
            ride.rider = ctx.ankhu.transform;
            Vector3 low = BeamEnd(BeamUp, -BeamHalf), high = BeamEnd(BeamUp, BeamHalf);
            ride.marks = new[]
            {
                new Vector3(3.6f, 0f, 0.1f),
                new Vector3(low.x + 0.3f, low.y + 0.12f, ShadufPivot.z),
                new Vector3(high.x - 0.1f, high.y + 0.12f, ShadufPivot.z),
                new Vector3(12.2f, 4.62f, 0.3f),
            };

            // Life: lamps, motes, a few swaying reeds by the path.
            RealmKit.Brazier("WestLamp", w, new Vector3(-13.8f, 0f, 1.6f), 9f, 2.2f);
            RealmKit.Brazier("BasinLamp", w, new Vector3(-2.2f, 0f, 2.2f), 8f, 1.8f);
            RealmKit.Brazier("LedgeLamp", w, new Vector3(16.0f, 4.6f, 1.8f), 8f, 2.0f);
            RealmKit.Motes(w, new Vector3(0f, 2.5f, 1f), new Vector3(32f, 5f, 6f), new Color(1f, 0.9f, 0.6f, 0.7f), 90);

            // Keep Ankhu on the lane.
            RealmKit.Blocker(w, new Vector3(0f, 3f, -2.4f), new Vector3(40f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(0f, 3f, 3.2f), new Vector3(40f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(-15.4f, 3f, 0f), new Vector3(0.4f, 6f, 6f));
            RealmKit.Blocker(w, new Vector3(17.2f, 4f, 0f), new Vector3(0.4f, 8f, 6f));

            Zone(ctx, "canal", new Vector3(-11.6f, 1f, 0.2f), new Vector3(4.2f, 4f, 5f));
            Zone(ctx, "trough", new Vector3(-7.4f, 1f, 0.2f), new Vector3(4.2f, 4f, 5f));
            Zone(ctx, "vignette", new Vector3(-3.7f, 1f, 0.2f), new Vector3(2.6f, 4f, 5f));
            Zone(ctx, "basin", new Vector3(0.2f, 1f, 0.2f), new Vector3(4.6f, 4f, 5f));
            Zone(ctx, "beamfoot", new Vector3(3.6f, 1f, 0.2f), new Vector3(2.2f, 4f, 5f));
            Zone(ctx, "shaduf", new Vector3(7.6f, 1f, 0.2f), new Vector3(5.8f, 4f, 5f));
            Zone(ctx, "ledge", new Vector3(13.75f, 5.6f, 0.4f), new Vector3(5.5f, 2f, 4.2f));

            Shot(ctx, "cam_west", new Vector3(-5.5f, 4.2f, -15f), new Vector3(-5.5f, 1.9f, 1f), 40f, new Vector3(-6f, 3f, 0f), new Vector3(20f, 10f, 8f), 0.05f, 0);
            Shot(ctx, "cam_east", new Vector3(8.6f, 5.0f, -16f), new Vector3(8.6f, 2.8f, 1f), 40f, new Vector3(9f, 3f, 0f), new Vector3(16.4f, 12f, 8f), 0.05f, 1);
            ApplyPaint(ctx, WernesPaintedScripts.FieldA, new[]
            {
                new Rect4(new Vector3(-2.75f, 0.004f, 0.5f), new Vector2(27.5f, 12f)),
                new Rect4(new Vector3(13.75f, 4.624f, 0.4f), new Vector2(5.5f, 4.2f)),
            });
            Finish(ctx, WernesPaintedScripts.FieldA);
        }

        // ------------------------------------------------------------------ painted plates

        public const int PaintLayer = 29;
        public const int PlateW = 1536, PlateH = 1024;
        public const string WernesArtDir = "Assets/Ankhu/Realms/Art/Wernes";
        public static string PlateDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../art_gen/wernes/plates"));

        /// <summary>The vertical fov that keeps a 16:9 camera's horizontal view at the 3:2 plate size.</summary>
        public static float PlateFov(float fov) =>
            2f * Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * (16f / 9f) / (PlateW / (float)PlateH)) * Mathf.Rad2Deg;

        /// <summary>Native plates (Zac, 2026-11-26): 1672x941, the 16:9 size Codex paints, at the game camera's own fov, no widening.</summary>
        public const int NativeW = 1672, NativeH = 941;

        /// <summary>"-plateNative" on the command line captures native plates into plates_native. Without it the old 1536x1024 widened plates are kept.</summary>
        public static bool NativePlates => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-plateNative") >= 0;
        public static int CaptureW => NativePlates ? NativeW : PlateW;
        public static int CaptureH => NativePlates ? NativeH : PlateH;
        public static float CaptureFov(float fov) => NativePlates ? fov : PlateFov(fov);
        public static string CaptureDir(string plateDir) => NativePlates ? plateDir + "_native" : plateDir;

        /// <summary>
        /// Anything a pose moves, shows or hides, any worker loop, water, sway, particles and the cast stay live 3D.
        /// Everything else is the set, which the painting replaces.
        /// </summary>
        private static HashSet<Transform> MoverRoots()
        {
            var set = new HashSet<Transform>();
            foreach (RealmObjectView v in UnityEngine.Object.FindObjectsByType<RealmObjectView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var p in v.poses)
                {
                    foreach (var pp in p.parts) if (pp.part != null) set.Add(pp.part);
                    foreach (GameObject g in p.show) if (g != null) set.Add(g.transform);
                    foreach (GameObject g in p.hide) if (g != null) set.Add(g.transform);
                }
            foreach (WorkerLoop wl in UnityEngine.Object.FindObjectsByType<WorkerLoop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { if (wl.worker != null) set.Add(wl.worker); if (wl.jars != null) set.Add(wl.jars); }
            return set;
        }

        private static bool IsMover(Transform t, HashSet<Transform> roots)
        {
            // Far fields are always painted, whatever moves inside them.
            for (Transform a = t; a != null; a = a.parent) if (a.name == "FarFields") return false;
            // Ambient sway and water are painted too. Live copies sat over the painting as floating shapes and a blue strip.
            // Only what a pose drives, the cast and particles stay live 3D.
            for (Transform a = t; a != null; a = a.parent)
            {
                if (roots.Contains(a)) return true;
                if (a.GetComponent<CardFacing>()) return true;
                if (a.GetComponent<ParticleSystem>() || a.GetComponent<Animator>()
                    || a.GetComponent<CharacterController>() || a.GetComponent<RealmFollower>()) return true;
            }
            return false;
        }

        public static List<Renderer> SetRenderers()
        {
            var roots = MoverRoots();
            return UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => !(r is ParticleSystemRenderer) && !IsMover(r.transform, roots)).ToList();
        }

        /// <summary>
        /// If paintings exist for this scene's cameras, the set moves to the hidden layer (colliders stay), each painted
        /// camera gets its painting on a locked backdrop quad, and the cast's shadows fall on invisible catchers.
        /// </summary>
        private struct Rect4 { public Vector3 center; public Vector2 size; public Rect4(Vector3 c, Vector2 s) { center = c; size = s; } }

        private static void ApplyPaint(Ctx ctx, string sceneName, Rect4[] catchers)
        {
            int painted = 0;
            foreach (var shot in ctx.shots)
            {
                string path = WernesArtDir + "/" + sceneName + "_" + shot.name + ".png";
                if (!File.Exists(path)) continue;
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp != null && (imp.mipmapEnabled || imp.maxTextureSize < 2048 || imp.textureCompression != TextureImporterCompression.Uncompressed))
                {
                    imp.mipmapEnabled = false; imp.maxTextureSize = 2048; imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport();
                }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;
                Camera cam = shot.camera;
                cam.cullingMask &= ~(1 << PaintLayer);
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Painting";
                UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(cam.transform, false);
                var mr = quad.GetComponent<MeshRenderer>();
                mr.sharedMaterial = PaintingMaterial(sceneName + "_" + shot.name, tex);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                cam.farClipPlane = 340f;
                quad.AddComponent<Ankhu.Opening.BackdropFit>().Configure(cam, 300f, NoStretchCheck.SourceAspect(tex)); // source aspect, never the imported size (no-stretch rule)
                shot.follow = 0f;
                painted++;
            }
            if (painted == 0) { Debug.Log("[Wernes] " + sceneName + ": no paintings yet, greybox only"); return; }
            // The painting carries its own atmosphere. Scene fog at the backdrop's 300 m would paint it over solid fog colour.
            RenderSettings.fog = false;
            // Moving puzzle pieces become painted sprite cards before the set is sorted into painted and live.
            Cardify();
            FlameCards();
            foreach (Renderer r in SetRenderers())
            {
                // The painting quads were just made and must stay on the visible layer.
                if (r.name == "Painting" && r.transform.parent != null && r.transform.parent.GetComponent<Camera>() != null) continue;
                r.gameObject.layer = PaintLayer;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Material catcherMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Ankhu/Opening/Generated/shadow_catcher.mat");
            foreach (Rect4 k in catchers)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Quad);
                c.name = "ShadowCatcher";
                UnityEngine.Object.DestroyImmediate(c.GetComponent<Collider>());
                c.transform.SetParent(ctx.world, false);
                c.transform.position = k.center;
                c.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                c.transform.localScale = new Vector3(k.size.x, k.size.y, 1f);
                var cr = c.GetComponent<MeshRenderer>();
                if (catcherMat != null) cr.sharedMaterial = catcherMat;
                cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Debug.Log("[Wernes] " + sceneName + ": " + painted + " painted cameras");
        }

        private static Material PaintingMaterial(string name, Texture2D tex)
        {
            string dir = WernesArtDir + "/Materials";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(WernesArtDir, "Materials");
            string path = dir + "/painting_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background + 10;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Renders each camera of each Wernes painted scene with only the set visible, at the plate size, for painting over.</summary>
        /// <summary>Optional "-wernesScenes A,B" on the command line limits plate capture to those scenes.</summary>
        private static string[] SceneFilter()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-wernesScenes");
            return i >= 0 && i + 1 < args.Length ? args[i + 1].Split(',') : null;
        }

        public static void CaptureWernesPlatesCli()
        {
            try
            {
                string plateDir = CaptureDir(PlateDir);
                Directory.CreateDirectory(plateDir);
                string[] only = SceneFilter();
                foreach (var script in WernesPaintedScripts.All())
                {
                    string path = RealmKit.SceneDir + "/" + script.SceneName + ".unity";
                    if (!File.Exists(path)) continue;
                    if (only != null && System.Array.IndexOf(only, script.SceneName) < 0) continue;
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                    foreach (var v in UnityEngine.Object.FindObjectsByType<RealmObjectView>(FindObjectsSortMode.None))
                        if (v.poses.Count > 0) SnapFirst(v.poses[0]);
                    var set = new HashSet<Renderer>(SetRenderers());
                    var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                    foreach (Renderer r in all) r.enabled = set.Contains(r);
                    foreach (ParticleSystem ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) ps.gameObject.SetActive(false);
                    foreach (Camera cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    {
                        foreach (Transform child in cam.transform) child.gameObject.SetActive(false);
                        cam.cullingMask = ~0;
                        float fov = cam.fieldOfView;
                        cam.fieldOfView = CaptureFov(fov);
                        RenderTo(cam, Path.Combine(plateDir, script.SceneName + "_" + cam.name + ".png"), CaptureW, CaptureH);
                        cam.fieldOfView = fov;
                    }
                    foreach (Renderer r in all) r.enabled = true;
                    Debug.Log("[Wernes] plates for " + script.SceneName);
                }
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void SnapFirst(RealmObjectView.StatePose pose)
        {
            foreach (var pp in pose.parts)
            {
                if (pp.part == null) continue;
                pp.part.localPosition = pp.localPosition;
                pp.part.localRotation = Quaternion.Euler(pp.localEuler);
                pp.part.localScale = pp.localScale;
            }
            foreach (GameObject g in pose.show) if (g != null) g.SetActive(true);
            foreach (GameObject g in pose.hide) if (g != null) g.SetActive(false);
            for (int i = 0; i < pose.lights.Count && i < pose.lightIntensities.Count; i++)
                if (pose.lights[i] != null) pose.lights[i].intensity = pose.lightIntensities[i];
        }

        private static void RenderTo(Camera cam, string file, int w, int h)
        {
            bool was = cam.enabled;
            cam.enabled = true;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = prev;
            cam.enabled = was;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }

        /// <summary>A clay shabti with a yoke and two jars; its work loop runs only while it is carrying.</summary>
        private static Transform Shabti(Ctx ctx, Transform w, string name, string id, Vector3 pos, Material clay, bool yoke, Vector3 from, Vector3 to, float period)
        {
            Transform root = RealmKit.Group(name, w, pos);
            Transform worker = RealmKit.Group("Worker", root);
            RealmKit.Shape(PrimitiveType.Capsule, "Body", worker, new Vector3(0f, 0.85f, 0f), new Vector3(0.5f, 0.75f, 0.42f), clay);
            Transform head = RealmKit.Group("Head", worker, new Vector3(0f, 1.62f, 0f));
            RealmKit.Shape(PrimitiveType.Sphere, "Face", head, new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.34f, 0.3f), clay);
            RealmKit.Box("Wig", head, new Vector3(0f, 0.12f, 0.05f), new Vector3(0.34f, 0.36f, 0.3f), RealmKit.Shadow, false);
            Transform eyes = RealmKit.Group("Eyes", head);
            RealmKit.Shape(PrimitiveType.Sphere, "EyeL", eyes, new Vector3(-0.06f, 0.14f, -0.14f), Vector3.one * 0.04f, RealmKit.EyeGlow);
            RealmKit.Shape(PrimitiveType.Sphere, "EyeR", eyes, new Vector3(0.06f, 0.14f, -0.14f), Vector3.one * 0.04f, RealmKit.EyeGlow);
            Transform jars = null;
            if (yoke)
            {
                jars = RealmKit.Group("Yoke", worker, new Vector3(0f, 1.42f, 0f));
                RealmKit.Box("Pole", jars, Vector3.zero, new Vector3(1.4f, 0.05f, 0.05f), RealmKit.Wood, false);
                RealmKit.Shape(PrimitiveType.Sphere, "JarL", jars, new Vector3(-0.66f, -0.3f, 0f), new Vector3(0.24f, 0.32f, 0.24f), RealmKit.Ochre);
                RealmKit.Shape(PrimitiveType.Sphere, "JarR", jars, new Vector3(0.66f, -0.3f, 0f), new Vector3(0.24f, 0.32f, 0.24f), RealmKit.Ochre);
            }
            else
            {
                Transform arms = RealmKit.Group("Arms", worker, new Vector3(0f, 1.3f, -0.2f));
                RealmKit.Box("Rope", arms, new Vector3(0.3f, 0.9f, 0f), new Vector3(0.03f, 1.8f, 0.03f), RealmKit.ReedDry, false);
                var pull = arms.gameObject.AddComponent<Sway>(); pull.axis = Vector3.right; pull.degrees = 14f; pull.speed = 2.4f;
            }
            var driver = new GameObject("WorkDriver");
            driver.transform.SetParent(root, false);
            WorkerLoop loop = driver.AddComponent<WorkerLoop>();
            loop.worker = worker; loop.jars = jars; loop.from = from; loop.to = to; loop.period = period;
            RealmObjectView v = View(ctx, root, id, new Vector3(0f, 1.7f, 0f));
            var sa = Pose(v, "asleep", 0.8f); Part(sa, head, head.localPosition, new Vector3(28f, 0f, 0f)); sa.hide.Add(eyes.gameObject); sa.hide.Add(driver);
            var sc = Pose(v, "carrying", 1.0f); Part(sc, head, head.localPosition, Vector3.zero); sc.show.Add(eyes.gameObject); sc.show.Add(driver);
            return root;
        }

        /// <summary>A small clay falcon toy: body, head, beak, tail, on a tiny base.</summary>
        private static Transform Falcon(Transform parent, string name, Vector3 pos, Material mat)
        {
            Transform f = RealmKit.Group(name, parent, pos);
            RealmKit.Shape(PrimitiveType.Sphere, "Body", f, new Vector3(0f, 0.12f, 0f), new Vector3(0.14f, 0.18f, 0.12f), mat);
            RealmKit.Shape(PrimitiveType.Sphere, "Head", f, new Vector3(0f, 0.26f, -0.03f), new Vector3(0.09f, 0.09f, 0.09f), mat);
            RealmKit.Box("Beak", f, new Vector3(0f, 0.25f, -0.08f), new Vector3(0.025f, 0.025f, 0.05f), RealmKit.Shadow, false);
            RealmKit.Box("Tail", f, new Vector3(0f, 0.04f, 0.08f), new Vector3(0.08f, 0.02f, 0.12f), mat, false, new Vector3(-25f, 0f, 0f));
            RealmKit.Shape(PrimitiveType.Cylinder, "Base", f, new Vector3(0f, 0.01f, 0f), new Vector3(0.12f, 0.01f, 0.12f), mat);
            return f;
        }
    }
}
