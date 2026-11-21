using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ankhu.Opening;
using Ankhu.Realms.Core;
using Ankhu.Realms.Core.Content;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Builds the realm scenes that follow the opening walk, entirely from code, and appends them to Build
    /// Settings right after Opening_WalkDuat.
    ///   Unity -batchmode -projectPath . -executeMethod Ankhu.Realms.EditorTools.RealmSceneBuilder.BuildAllCli -logFile realms.log
    /// </summary>
    public static class RealmSceneBuilder
    {
        private const string WalkScenePath = "Assets/Ankhu/Generated/Scenes/Opening/Opening_WalkDuat.unity";

        [UnityEditor.MenuItem("Ankhu/Realms/Build Wernes Scenes")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
        }

        public static void BuildAllCli()
        {
            try
            {
                BuildAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildAll()
        {
            RealmKit.EnsureDirs();
            BuildArit1();
            BuildField();
            BuildHouse();
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Realms] Wernes scenes built.");
        }

        // ------------------------------------------------------------------ shared scaffolding

        private sealed class Ctx
        {
            public Scene scene;
            public Transform world;
            public RealmSceneDirector director;
            public FixedCameraDirector cams;
            public readonly List<FixedCameraDirector.Shot> shots = new List<FixedCameraDirector.Shot>();
            public GameObject ankhu;
            public Animator ankhuAnim;
            public WalkPlayer player;
            public RealmFollower anpu;
        }

        private static Ctx Begin(string sceneName, Color fog, float fogDensity, float exposure)
        {
            var ctx = new Ctx();
            ctx.scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ctx.world = new GameObject("World").transform;
            Transform env = RealmKit.Group("Atmosphere", ctx.world);
            RealmKit.NightSky(env, fog, fogDensity);
            RealmKit.GlobalVolume(env, RealmKit.PostProfile(sceneName, exposure, 1.15f, new Color(1f, 0.97f, 0.93f)));
            ctx.cams = new GameObject("Cameras").AddComponent<FixedCameraDirector>();
            ctx.director = new GameObject("RealmDirector").AddComponent<RealmSceneDirector>();
            ctx.director.sceneName = sceneName;
            ctx.director.cameras = ctx.cams;
            ctx.director.ambience = AssetDatabase.LoadAssetAtPath<AudioClip>(RealmKit.Ambience);
            new GameObject("Listener").AddComponent<AudioListener>();
            return ctx;
        }

        private static void Cast(Ctx ctx, Vector3 ankhuPos, float yaw, Vector3 anpuPos)
        {
            ctx.ankhu = RealmKit.Character("Ankhu", RealmKit.AnkhuPrefab, ankhuPos, yaw, null, out ctx.ankhuAnim);
            var cc = ctx.ankhu.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;
            ctx.player = ctx.ankhu.AddComponent<WalkPlayer>();
            ctx.player.Configure(ctx.cams, ctx.ankhuAnim, RealmKit.Footsteps());

            RuntimeAnimatorController anpuCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(RealmKit.AnpuEscortController);
            GameObject anpu = RealmKit.Character("Anpu", RealmKit.AnpuPrefab, anpuPos, yaw, anpuCtrl, out Animator anpuAnim);
            var acc = anpu.AddComponent<CharacterController>();
            acc.height = 2.1f;
            acc.radius = 0.32f;
            acc.center = new Vector3(0f, 1.05f, 0f);
            ctx.anpu = anpu.AddComponent<RealmFollower>();
            ctx.anpu.Configure(ctx.ankhu.transform, anpuAnim);

            ctx.director.player = ctx.player;
            ctx.director.playerAnimator = ctx.ankhuAnim;
            Transform head = RealmKit.FindBone(ctx.ankhu.transform, "Head");
            if (head == null)
            {
                head = new GameObject("HeadAnchor").transform;
                head.SetParent(ctx.ankhu.transform, false);
                head.localPosition = new Vector3(0f, 1.75f, 0f);
            }
            ctx.director.playerHead = head;
            ctx.director.anpu = ctx.anpu;
            ctx.director.actors.Add(new RealmSceneDirector.ActorRef { name = "Anpu", transform = anpu.transform, animator = anpuAnim });

            foreach (GameObject who in new[] { ctx.ankhu, anpu })
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = who.name + "_Shadow";
                UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                q.GetComponent<MeshRenderer>().sharedMaterial = RealmKit.Additive("BlobShade", new Color(0f, 0f, 0f, 0f));
                q.AddComponent<BlobShadow>().Configure(who.transform, 0f, 0.55f, 0.75f);
                UnityEngine.Object.DestroyImmediate(q);
            }
        }

        /// <summary>A fixed camera and the zone in which it is live.</summary>
        private static void Shot(Ctx ctx, string name, Vector3 pos, Vector3 lookAt, float fov, Vector3 zoneCenter, Vector3 zoneSize, float follow = 0.12f, int priority = 0)
        {
            var camGo = new GameObject(name);
            camGo.transform.SetParent(ctx.cams.transform, false);
            camGo.transform.position = pos;
            camGo.transform.rotation = Quaternion.LookRotation(lookAt - pos, Vector3.up);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.07f);
            cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            int index = ctx.shots.Count;
            if (index > 0) cam.enabled = false;
            ctx.shots.Add(new FixedCameraDirector.Shot { name = name, camera = cam, follow = follow, aimHeight = 1.3f });
            Zone(ctx, "", zoneCenter, zoneSize, index, priority);
        }

        private static RealmZone Zone(Ctx ctx, string id, Vector3 center, Vector3 size, int cameraIndex = -1, int priority = 0)
        {
            var g = new GameObject(string.IsNullOrEmpty(id) ? "CamZone" + cameraIndex : "Zone_" + id);
            g.transform.SetParent(ctx.director.transform, false);
            g.transform.position = center;
            var z = g.AddComponent<RealmZone>();
            z.zoneId = id;
            z.size = size;
            z.cameraIndex = cameraIndex;
            z.priority = priority;
            ctx.director.zones.Add(z);
            return z;
        }

        private static RealmObjectView View(Ctx ctx, Transform root, string id, Vector3 glyphAnchor)
        {
            var v = root.gameObject.AddComponent<RealmObjectView>();
            v.objectId = id;
            root.gameObject.AddComponent<GoldMagic>().Configure(id, glyphAnchor);
            ctx.director.views.Add(v);
            return v;
        }

        private static RealmObjectView.StatePose Pose(RealmObjectView v, string state, float seconds = 1.6f)
        {
            var p = new RealmObjectView.StatePose { state = state, seconds = seconds };
            v.poses.Add(p);
            return p;
        }

        private static void Part(RealmObjectView.StatePose p, Transform part, Vector3 pos, Vector3 euler, Vector3? scale = null)
        {
            p.parts.Add(new RealmObjectView.PartPose { part = part, localPosition = pos, localEuler = euler, localScale = scale ?? part.localScale });
        }

        private static void Finish(Ctx ctx, string sceneName)
        {
            ctx.cams.Configure(ctx.shots.ToArray(), ctx.ankhu.transform);
            var refs = new GameObject("MagicShaderRefs").AddComponent<MagicShaderRefs>();
            refs.shaders = new[]
            {
                Shader.Find("Ankhu/Realms/MagicAdditive"), Shader.Find("Ankhu/Realms/GoldRim"),
                Shader.Find("Universal Render Pipeline/Particles/Unlit"), Shader.Find("Universal Render Pipeline/Unlit"),
            }.Where(x => x != null).ToArray();
            string path = RealmKit.SceneDir + "/" + sceneName + ".unity";
            EditorSceneManager.SaveScene(ctx.scene, path);
            Debug.Log("[Realms] Saved " + path);
        }

        private static void Ground(Transform world, float size, Material mat)
        {
            GameObject g = RealmKit.Box("Ground", world, new Vector3(0f, -0.5f, 0f), new Vector3(size, 1f, size), mat);
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void Dunes(Transform world, int seed, float radius)
        {
            var rnd = new System.Random(seed);
            Transform d = RealmKit.Group("Dunes", world);
            for (int i = 0; i < 16; i++)
            {
                float a = (float)(rnd.NextDouble() * Math.PI * 2);
                float r = radius + (float)rnd.NextDouble() * radius * 0.6f;
                float w = 30f + (float)rnd.NextDouble() * 50f;
                RealmKit.Shape(PrimitiveType.Sphere, "Dune" + i, d, new Vector3(Mathf.Cos(a) * r, -2f, Mathf.Sin(a) * r),
                    new Vector3(w, 6f + (float)rnd.NextDouble() * 10f, w * 0.6f), RealmKit.Sand, false, new Vector3(0f, a * Mathf.Rad2Deg, 0f));
            }
        }

        private static void Reeds(Transform parent, Vector3 center, Vector3 extent, int count, int seed)
        {
            var rnd = new System.Random(seed);
            Transform g = RealmKit.Group("Reeds", parent, center);
            for (int i = 0; i < count; i++)
            {
                float x = ((float)rnd.NextDouble() - 0.5f) * extent.x;
                float z = ((float)rnd.NextDouble() - 0.5f) * extent.z;
                float h = 1.2f + (float)rnd.NextDouble() * 1.6f;
                float lean = ((float)rnd.NextDouble() - 0.5f) * 16f;
                RealmKit.Shape(PrimitiveType.Cylinder, "Reed" + i, g, new Vector3(x, h * 0.5f, z), new Vector3(0.035f, h * 0.5f, 0.035f),
                    rnd.NextDouble() > 0.4 ? RealmKit.Reed : RealmKit.ReedDry, false, new Vector3(lean, 0f, lean * 0.6f));
                if (rnd.NextDouble() > 0.55)
                    RealmKit.Shape(PrimitiveType.Sphere, "Umbel" + i, g, new Vector3(x + lean * 0.01f * h, h + 0.05f, z), new Vector3(0.22f, 0.12f, 0.22f), RealmKit.ReedDry);
            }
        }

        // ------------------------------------------------------------------ Arit 1: the First Gate

        private static void BuildArit1()
        {
            Ctx ctx = Begin(WernesScripts.Gate, new Color(0.05f, 0.06f, 0.10f), 0.011f, 0.3f);
            Transform w = ctx.world;
            Ground(w, 160f, RealmKit.Sand);
            Dunes(w, 11, 85f);
            // The lamp road arriving from the south.
            Transform road = RealmKit.Group("Road", w);
            for (int i = 0; i < 8; i++)
                RealmKit.Box("Slab" + i, road, new Vector3(0f, 0.03f, -20f + i * 1.9f), new Vector3(3.2f, 0.06f, 1.8f), RealmKit.Limestone, false);
            for (int s = -1; s <= 1; s += 2)
            {
                RealmKit.Brazier("RoadLamp", w, new Vector3(s * 2.4f, 0f, -14f), 8f, 2.0f);
                RealmKit.Brazier("CourtLamp", w, new Vector3(s * 4.4f, 0f, -5.5f), 9f, 2.3f);
                RealmKit.Brazier("GateLamp", w, new Vector3(s * 2.8f, 0f, 6.4f), 8f, 2.6f);
            }
            // Court pavement.
            Transform court = RealmKit.Group("Court", w);
            for (int x = -4; x <= 4; x++)
                for (int z = -5; z <= 4; z++)
                    RealmKit.Box("Pave", court, new Vector3(x * 1.6f, 0.03f, z * 1.6f), new Vector3(1.56f, 0.06f, 1.56f), ((x + z) & 1) == 0 ? RealmKit.Limestone : RealmKit.Sandstone, false);
            // Low enclosure wall.
            RealmKit.Wall("WallL", w, new Vector3(-8.2f, 0f, -0.5f), new Vector3(0.8f, 2.2f, 17f), RealmKit.Sandstone);
            RealmKit.Wall("WallR", w, new Vector3(8.2f, 0f, -0.5f), new Vector3(0.8f, 2.2f, 17f), RealmKit.Sandstone);

            // The gate in its pylon.
            Transform pylon = RealmKit.Group("Pylon", w, new Vector3(0f, 0f, 9f));
            RealmKit.PylonTower("Tower_L", pylon, new Vector3(-5.2f, 0f, 0f), 6.4f, 3f, 11f, RealmKit.Sandstone);
            RealmKit.PylonTower("Tower_R", pylon, new Vector3(5.2f, 0f, 0f), 6.4f, 3f, 11f, RealmKit.Sandstone);
            RealmKit.Box("GateLintel", pylon, new Vector3(0f, 7.6f, 0f), new Vector3(4.2f, 1.6f, 3.2f), RealmKit.Sandstone);
            RealmKit.Box("WingedDisk", pylon, new Vector3(0f, 7.6f, -1.62f), new Vector3(3.6f, 0.3f, 0.06f), RealmKit.Gold, false);
            RealmKit.Shape(PrimitiveType.Sphere, "Disk", pylon, new Vector3(0f, 7.62f, -1.66f), new Vector3(0.7f, 0.7f, 0.1f), RealmKit.Gold);
            for (int s = -1; s <= 1; s += 2)
            {
                RealmKit.Box("Flagpole", pylon, new Vector3(s * 3.0f, 6.5f, -1.7f), new Vector3(0.14f, 13f, 0.14f), RealmKit.Wood, false);
                RealmKit.Box("Pennant", pylon, new Vector3(s * 3.4f, 12.2f, -1.7f), new Vector3(0.8f, 0.5f, 0.02f), RealmKit.Ochre, false);
                RealmKit.GlyphPlate("TowerGlyphs", pylon, new Vector3(s * 5.2f, 5.6f, -1.55f), new Vector3(0f, 180f, 0f), "O31 O1 N35 S34", 0.9f, true, new Color(1f, 0.78f, 0.42f, 0.35f));
            }
            Transform gate = RealmKit.Group("Gate", w, new Vector3(0f, 0f, 9f));
            Transform gl = RealmKit.Group("Leaf_L", gate, new Vector3(-2.0f, 0f, -0.6f));
            RealmKit.Box("Panel", gl, new Vector3(1.0f, 3.4f, 0f), new Vector3(2.0f, 6.8f, 0.3f), RealmKit.Wood);
            Transform gr = RealmKit.Group("Leaf_R", gate, new Vector3(2.0f, 0f, -0.6f));
            RealmKit.Box("Panel", gr, new Vector3(-1.0f, 3.4f, 0f), new Vector3(2.0f, 6.8f, 0.3f), RealmKit.Wood);
            foreach (Transform lf in new[] { gl, gr })
            {
                float sx = lf == gl ? 1f : -1f;
                for (int i = 0; i < 4; i++)
                    RealmKit.Box("Stud", lf, new Vector3(sx * 1.0f, 1.2f + i * 1.5f, -0.18f), new Vector3(1.8f, 0.1f, 0.05f), RealmKit.Gold, false);
            }
            Transform bolt = RealmKit.Group("Bolt", gate, new Vector3(0f, 3.4f, -0.85f));
            RealmKit.Box("Bar", bolt, Vector3.zero, new Vector3(2.4f, 0.22f, 0.22f), RealmKit.Gold, false);
            Light beyondLight = InnerGlow(gate, new Vector3(0f, 3f, 3f), 0f, 16f);
            beyondLight.color = new Color(1f, 0.85f, 0.55f);
            GameObject shaft = RealmKit.Box("LightShaft", gate, new Vector3(0f, 3.4f, 1.2f), new Vector3(3.8f, 6.8f, 0.05f), RealmKit.Additive("GateShaft", new Color(1f, 0.8f, 0.45f, 0.5f), RealmKit.SoftDot()), false);
            RealmObjectView gateView = View(ctx, gate, "gate", new Vector3(0f, 3.4f, -1f));
            var gs = Pose(gateView, "shut", 0.2f);
            Part(gs, gl, gl.localPosition, Vector3.zero); Part(gs, gr, gr.localPosition, Vector3.zero);
            Part(gs, bolt, bolt.localPosition, Vector3.zero);
            gs.hide.Add(shaft); gs.lights.Add(beyondLight); gs.lightIntensities.Add(0f);
            var go = Pose(gateView, "open", 3.5f);
            Part(go, gl, gl.localPosition, new Vector3(0f, 95f, 0f)); Part(go, gr, gr.localPosition, new Vector3(0f, -95f, 0f));
            Part(go, bolt, bolt.localPosition + new Vector3(0f, -3f, -0.4f), new Vector3(0f, 0f, 90f));
            go.show.Add(shaft); go.lights.Add(beyondLight); go.lightIntensities.Add(4.5f);
            for (int i = 0; i < 6; i++)
                RealmKit.Box("Avenue" + i, w, new Vector3(0f, 0.03f, 12f + i * 2f), new Vector3(3.6f, 0.06f, 1.9f), RealmKit.Limestone, false);

            // The three who mind the gate. The announcer stands left, the guardian on its plinth centre-right, the keeper lies on its face.
            Transform announcer = Statue(w, "Announcer", new Vector3(-4.2f, 0f, 4.8f), 25f, out Transform aHead, RealmKit.Limestone);
            Transform guardian = Statue(w, "Guardian", new Vector3(4.2f, 0.6f, 4.8f), -25f, out Transform gHead, RealmKit.Basalt);
            RealmKit.Box("GuardianPlinth", w, new Vector3(4.2f, 0.3f, 4.8f), new Vector3(1.8f, 0.6f, 1.8f), RealmKit.Sandstone);
            RealmKit.GlyphPlate("PlinthName", w, new Vector3(4.2f, 0.32f, 3.88f), new Vector3(0f, 180f, 0f), "F21 G17", 0.3f, false, new Color(1f, 0.8f, 0.45f, 0.9f));
            Transform keeper = Fallen(w, "Keeper", new Vector3(5.6f, 0f, -2.4f), out GameObject keeperEyes, out Transform keeperBody);

            RealmObjectView av = View(ctx, announcer, "announcer", new Vector3(0f, 3.6f, 0f));
            foreach (string st in new[] { "unknown", "known" }) { var p = Pose(av, st, 0.6f); Part(p, aHead, aHead.localPosition, new Vector3(18f, 0f, 0f)); p.hide.Add(aHead.Find("Eyes").gameObject); }
            var aa = Pose(av, "answered", 2.2f); Part(aa, aHead, aHead.localPosition, new Vector3(-6f, 0f, 0f)); aa.show.Add(aHead.Find("Eyes").gameObject);
            RealmObjectView gv = View(ctx, guardian, "guardian", new Vector3(0f, 3.6f, 0f));
            foreach (string st in new[] { "unknown", "known" }) { var p = Pose(gv, st, 0.6f); Part(p, gHead, gHead.localPosition, new Vector3(0f, 70f, 0f)); p.hide.Add(gHead.Find("Eyes").gameObject); }
            var ga = Pose(gv, "answered", 2.2f); Part(ga, gHead, gHead.localPosition, new Vector3(0f, -10f, 6f)); ga.show.Add(gHead.Find("Eyes").gameObject);
            RealmObjectView kv = View(ctx, keeper, "keeper", new Vector3(0f, 1.2f, 0f));
            foreach (string st in new[] { "unknown", "known" }) { var p = Pose(kv, st, 0.6f); Part(p, keeperBody, keeperBody.localPosition, Vector3.zero); p.hide.Add(keeperEyes); }
            var ka = Pose(kv, "answered", 2.6f); Part(ka, keeperBody, keeperBody.localPosition + new Vector3(0f, 0.15f, 0f), new Vector3(-24f, 0f, 0f)); ka.show.Add(keeperEyes);

            RealmKit.Motes(w, new Vector3(0f, 3f, 2f), new Vector3(18f, 6f, 20f), new Color(1f, 0.85f, 0.55f, 0.8f), 80);
            RealmKit.Blocker(w, new Vector3(0f, 2f, -21f), new Vector3(20f, 4f, 1f));
            RealmKit.Blocker(w, new Vector3(-2.2f, 2f, -14.5f), new Vector3(1f, 4f, 13f));
            RealmKit.Blocker(w, new Vector3(2.2f, 2f, -14.5f), new Vector3(1f, 4f, 13f));
            RealmKit.Blocker(w, new Vector3(-5.2f, 2f, -8.6f), new Vector3(5f, 4f, 1f));
            RealmKit.Blocker(w, new Vector3(5.2f, 2f, -8.6f), new Vector3(5f, 4f, 1f));
            RealmKit.Blocker(w, new Vector3(0f, 2f, 24f), new Vector3(10f, 4f, 1f));
            RealmKit.Blocker(w, new Vector3(-2.2f, 2f, 17f), new Vector3(1f, 4f, 14f));
            RealmKit.Blocker(w, new Vector3(2.2f, 2f, 17f), new Vector3(1f, 4f, 14f));

            Cast(ctx, new Vector3(0f, 0f, -18f), 0f, new Vector3(-1.4f, 0f, -19f));
            Zone(ctx, "road", new Vector3(0f, 1f, -14f), new Vector3(4f, 4f, 12f));
            Zone(ctx, "court", new Vector3(0f, 1f, -1f), new Vector3(16f, 4f, 15f), -1, 1);
            Zone(ctx, "plinth", new Vector3(4.2f, 1f, 2.9f), new Vector3(3f, 4f, 2.4f), -1, 2);
            Zone(ctx, "fallen", new Vector3(4.6f, 1f, -2.4f), new Vector3(3.4f, 4f, 3.6f), -1, 2);
            Zone(ctx, "beyond", new Vector3(0f, 1f, 15f), new Vector3(4f, 4f, 8f), -1, 3);
            Shot(ctx, "cam_road", new Vector3(2.6f, 2.2f, -22f), new Vector3(0f, 3.5f, 6f), 44f, new Vector3(0f, 1f, -14f), new Vector3(5f, 4f, 12f));
            Shot(ctx, "cam_court", new Vector3(-5.5f, 4.8f, -9.5f), new Vector3(2f, 3.2f, 7f), 52f, new Vector3(0f, 1f, -1f), new Vector3(16f, 4f, 15f), 0.1f, 1);
            Shot(ctx, "cam_gate", new Vector3(-3.6f, 1.0f, 1.0f), new Vector3(1f, 5.6f, 9f), 56f, new Vector3(0f, 1f, 5.4f), new Vector3(9f, 4f, 5f), 0.06f, 2);
            Shot(ctx, "cam_keeper", new Vector3(0.6f, 2.6f, -6.4f), new Vector3(5.6f, 0.6f, -2.2f), 48f, new Vector3(5f, 1f, -2.4f), new Vector3(5f, 4f, 4.4f), 0.06f, 3);
            Shot(ctx, "cam_beyond", new Vector3(0f, 2.4f, 4.5f), new Vector3(0f, 1.6f, 20f), 52f, new Vector3(0f, 1f, 15f), new Vector3(4f, 4f, 10f), 0.08f, 4);
            Finish(ctx, WernesScripts.Gate);
        }

        /// <summary>A figure carved lying on its face, arms at its sides (the keeper of the first gate).</summary>
        private static Transform Fallen(Transform parent, string name, Vector3 pos, out GameObject eyes, out Transform body)
        {
            Transform s = RealmKit.Group(name, parent, pos, new Vector3(0f, 90f, 0f));
            RealmKit.Box("Bier", s, new Vector3(0f, 0.2f, 0f), new Vector3(1.2f, 0.4f, 3.2f), RealmKit.Sandstone);
            body = RealmKit.Group("Body", s, new Vector3(0f, 0.4f, 0f));
            RealmKit.Box("Torso", body, new Vector3(0f, 0.2f, 0.2f), new Vector3(0.7f, 0.36f, 1.5f), RealmKit.Basalt, false);
            RealmKit.Box("Legs", body, new Vector3(0f, 0.16f, -0.95f), new Vector3(0.5f, 0.3f, 1.1f), RealmKit.Basalt, false);
            RealmKit.Box("Head", body, new Vector3(0f, 0.22f, 1.2f), new Vector3(0.42f, 0.38f, 0.44f), RealmKit.Basalt, false);
            RealmKit.Box("Wig", body, new Vector3(0f, 0.3f, 1.14f), new Vector3(0.5f, 0.3f, 0.5f), RealmKit.Lapis, false);
            Transform e = RealmKit.Group("Eyes", body);
            RealmKit.Shape(PrimitiveType.Sphere, "EyeL", e, new Vector3(-0.1f, 0.12f, 1.43f), Vector3.one * 0.06f, RealmKit.EyeGlow);
            RealmKit.Shape(PrimitiveType.Sphere, "EyeR", e, new Vector3(0.1f, 0.12f, 1.43f), Vector3.one * 0.06f, RealmKit.EyeGlow);
            eyes = e.gameObject;
            return s;
        }

        // ------------------------------------------------------------------ Room 1.1: the Field That Waits

        private static void BuildField()
        {
            Ctx ctx = Begin(WernesScripts.Field, new Color(0.04f, 0.07f, 0.12f), 0.013f, 0.35f);
            Transform w = ctx.world;
            Ground(w, 160f, RealmKit.Mud);
            Dunes(w, 5, 85f);
            // Distant fields: rows of gold grain and the grey workers who never look up.
            Transform far = RealmKit.Group("FarFields", w);
            var rnd = new System.Random(3);
            for (int row = 0; row < 7; row++)
                RealmKit.Box("Grain" + row, far, new Vector3(-6f, 0.25f, 22f + row * 3f), new Vector3(60f, 0.5f, 1.6f), RealmKit.Lit("Grain", new Color(0.72f, 0.58f, 0.24f), 0.2f), false);
            for (int i = 0; i < 9; i++)
            {
                Vector3 p = new Vector3(-22f + (float)rnd.NextDouble() * 40f, 0f, 24f + (float)rnd.NextDouble() * 16f);
                Transform grey = RealmKit.Group("GreyOne" + i, far, p, new Vector3(0f, (float)rnd.NextDouble() * 360f, 0f));
                RealmKit.Shape(PrimitiveType.Capsule, "Body", grey, new Vector3(0f, 0.8f, 0f), new Vector3(0.45f, 0.8f, 0.45f), RealmKit.Lit("GreySoul", new Color(0.45f, 0.47f, 0.5f), 0.05f), false, new Vector3(35f, 0f, 0f));
            }
            // The canal along the back, full of stars, and its bank.
            RealmKit.Box("CanalBed", w, new Vector3(0f, -0.9f, 9f), new Vector3(60f, 0.2f, 4f), RealmKit.Mud, false);
            GameObject canal = RealmKit.Box("CanalWater", w, new Vector3(0f, -0.15f, 9f), new Vector3(60f, 0.05f, 4f), RealmKit.Water, false);
            RealmKit.Box("CanalBankN", w, new Vector3(0f, -0.1f, 11.4f), new Vector3(60f, 0.4f, 0.8f), RealmKit.Limestone);
            Reeds(w, new Vector3(0f, 0f, 12.4f), new Vector3(50f, 0f, 1.6f), 260, 21);
            Reeds(w, new Vector3(-13f, 0f, 2f), new Vector3(6f, 0f, 14f), 120, 23);

            // The sluice from the canal into a channel that runs south past the worker.
            Transform sluice = RealmKit.Group("Sluice", w, new Vector3(-4.5f, 0f, 6.6f));
            RealmKit.Box("JambL", sluice, new Vector3(-1.0f, 1.2f, 0f), new Vector3(0.5f, 2.4f, 1.0f), RealmKit.Sandstone);
            RealmKit.Box("JambR", sluice, new Vector3(1.0f, 1.2f, 0f), new Vector3(0.5f, 2.4f, 1.0f), RealmKit.Sandstone);
            RealmKit.Box("Head", sluice, new Vector3(0f, 2.55f, 0f), new Vector3(2.6f, 0.4f, 1.1f), RealmKit.Sandstone);
            RealmKit.GlyphPlate("SluiceGlyphs", sluice, new Vector3(0f, 2.55f, -0.58f), new Vector3(0f, 180f, 0f), "E34 N35 O31", 0.26f, false, new Color(1f, 0.78f, 0.4f, 0.5f));
            Transform leaf = RealmKit.Group("Leaf", sluice, new Vector3(0f, 0.8f, 0f));
            RealmKit.Box("Board", leaf, Vector3.zero, new Vector3(1.5f, 1.6f, 0.18f), RealmKit.Wood);
            RealmKit.Box("ChannelBed", w, new Vector3(-4.5f, -0.25f, 2.4f), new Vector3(1.5f, 0.1f, 7.4f), RealmKit.Mud, false);
            RealmKit.Box("ChannelLipL", w, new Vector3(-5.35f, 0.05f, 2.4f), new Vector3(0.2f, 0.3f, 7.4f), RealmKit.Limestone, false);
            RealmKit.Box("ChannelLipR", w, new Vector3(-3.65f, 0.05f, 2.4f), new Vector3(0.2f, 0.3f, 7.4f), RealmKit.Limestone, false);
            GameObject channelWater = RealmKit.Box("ChannelWater", w, new Vector3(-4.5f, -0.08f, 2.4f), new Vector3(1.45f, 0.04f, 7.4f), RealmKit.Water, false);
            channelWater.AddComponent<WaterMotion>().amplitude = 0.02f;
            RealmObjectView sv = View(ctx, sluice, "sluice", new Vector3(0f, 1.4f, -0.6f));
            var ss = Pose(sv, "shut", 0.2f); Part(ss, leaf, new Vector3(0f, 0.8f, 0f), Vector3.zero); ss.hide.Add(channelWater);
            var so = Pose(sv, "open", 2.0f); Part(so, leaf, new Vector3(0f, 2.1f, 0f), Vector3.zero); so.show.Add(channelWater);

            // The raised stone basin with the lotus bud standing in it.
            Transform basin = RealmKit.Group("Basin", w, new Vector3(3.6f, 0f, 2.4f));
            RealmKit.Shape(PrimitiveType.Cylinder, "Plinth", basin, new Vector3(0f, 0.35f, 0f), new Vector3(2.6f, 0.35f, 2.6f), RealmKit.Sandstone, true);
            RealmKit.Shape(PrimitiveType.Cylinder, "Rim", basin, new Vector3(0f, 0.85f, 0f), new Vector3(2.4f, 0.15f, 2.4f), RealmKit.Limestone);
            RealmKit.Shape(PrimitiveType.Cylinder, "Hollow", basin, new Vector3(0f, 0.86f, 0f), new Vector3(2.1f, 0.15f, 2.1f), RealmKit.Shadow);
            GameObject waterRippled = RealmKit.Shape(PrimitiveType.Cylinder, "WaterRippled", basin, new Vector3(0f, 0.98f, 0f), new Vector3(2.1f, 0.02f, 2.1f), RealmKit.WaterRough);
            waterRippled.AddComponent<WaterMotion>().amplitude = 0.03f;
            GameObject waterMirror = RealmKit.Shape(PrimitiveType.Cylinder, "WaterMirror", basin, new Vector3(0f, 0.99f, 0f), new Vector3(2.1f, 0.02f, 2.1f), RealmKit.Water);
            Transform sunInWater = RealmKit.GlyphPlate("SunInWater", basin, new Vector3(0f, 1.02f, 0.2f), new Vector3(90f, 0f, 0f), "N5", 0.7f, false, new Color(1f, 0.7f, 0.35f, 0.7f));
            GameObject pourStream = RealmKit.Box("Pour", basin, new Vector3(-1.1f, 1.4f, 0f), new Vector3(0.08f, 0.9f, 0.08f), RealmKit.WaterRough, false);
            Light basinGlow = InnerGlow(basin, new Vector3(0f, 1.8f, 0f), 0f, 6f);
            RealmObjectView bv = View(ctx, basin, "basin", new Vector3(0f, 1.3f, 0f));
            var bd = Pose(bv, "dry", 0.2f); bd.hide.Add(waterRippled); bd.hide.Add(waterMirror); bd.hide.Add(sunInWater.gameObject); bd.hide.Add(pourStream);
            bd.lights.Add(basinGlow); bd.lightIntensities.Add(0f);
            var br = Pose(bv, "rippled", 1.5f); br.show.Add(waterRippled); br.show.Add(pourStream); br.hide.Add(waterMirror); br.hide.Add(sunInWater.gameObject);
            var bm = Pose(bv, "mirror", 2.5f); bm.show.Add(waterMirror); bm.show.Add(sunInWater.gameObject); bm.hide.Add(waterRippled); bm.hide.Add(pourStream);
            bm.lights.Add(basinGlow); bm.lightIntensities.Add(2.8f);

            Transform lotus = RealmKit.Group("Lotus", w, new Vector3(3.6f, 1.0f, 2.4f));
            RealmKit.Shape(PrimitiveType.Cylinder, "Stem", lotus, new Vector3(0f, 0.35f, 0f), new Vector3(0.05f, 0.35f, 0.05f), RealmKit.Reed);
            Transform petals = RealmKit.Group("Petals", lotus, new Vector3(0f, 0.75f, 0f));
            var petalList = new List<Transform>();
            for (int i = 0; i < 8; i++)
            {
                Transform pivot = RealmKit.Group("PetalPivot" + i, petals, Vector3.zero, new Vector3(0f, i * 45f, 0f));
                Transform petal = RealmKit.Group("Petal" + i, pivot, Vector3.zero, new Vector3(-8f, 0f, 0f));
                RealmKit.Shape(PrimitiveType.Sphere, "Leaf", petal, new Vector3(0f, 0.22f, 0.04f), new Vector3(0.14f, 0.3f, 0.06f), RealmKit.Lit("LotusPetal", new Color(0.86f, 0.9f, 1f), 0.4f));
                petalList.Add(petal);
            }
            Light lotusLight = InnerGlow(lotus, new Vector3(0f, 1.1f, 0f), 0f, 5f);
            RealmObjectView lv = View(ctx, lotus, "lotus", new Vector3(0f, 1.0f, 0f));
            var lc = Pose(lv, "closed", 0.2f);
            foreach (Transform p in petalList) Part(lc, p, Vector3.zero, new Vector3(-8f, 0f, 0f));
            lc.lights.Add(lotusLight); lc.lightIntensities.Add(0f);
            var lo = Pose(lv, "open", 3.0f);
            foreach (Transform p in petalList) Part(lo, p, Vector3.zero, new Vector3(-62f, 0f, 0f));
            lo.lights.Add(lotusLight); lo.lightIntensities.Add(2.2f);

            // The shabti: a painted clay worker with a yoke and two jars, asleep on its feet.
            Transform shabti = RealmKit.Group("Shabti", w, new Vector3(-1.6f, 0f, 2.2f));
            Transform worker = RealmKit.Group("Worker", shabti);
            Material clay = RealmKit.Lit("Clay", new Color(0.33f, 0.47f, 0.52f), 0.35f);
            RealmKit.Shape(PrimitiveType.Capsule, "Body", worker, new Vector3(0f, 0.85f, 0f), new Vector3(0.5f, 0.75f, 0.42f), clay, true);
            Transform sHead = RealmKit.Group("Head", worker, new Vector3(0f, 1.62f, 0f));
            RealmKit.Shape(PrimitiveType.Sphere, "Face", sHead, new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.34f, 0.3f), clay);
            RealmKit.Box("Wig", sHead, new Vector3(0f, 0.12f, 0.05f), new Vector3(0.34f, 0.36f, 0.3f), RealmKit.Shadow, false);
            Transform sEyes = RealmKit.Group("Eyes", sHead);
            RealmKit.Shape(PrimitiveType.Sphere, "EyeL", sEyes, new Vector3(-0.06f, 0.14f, -0.14f), Vector3.one * 0.04f, RealmKit.EyeGlow);
            RealmKit.Shape(PrimitiveType.Sphere, "EyeR", sEyes, new Vector3(0.06f, 0.14f, -0.14f), Vector3.one * 0.04f, RealmKit.EyeGlow);
            Transform jars = RealmKit.Group("Yoke", worker, new Vector3(0f, 1.42f, 0f));
            RealmKit.Box("Pole", jars, Vector3.zero, new Vector3(1.4f, 0.05f, 0.05f), RealmKit.Wood, false);
            RealmKit.Shape(PrimitiveType.Sphere, "JarL", jars, new Vector3(-0.66f, -0.3f, 0f), new Vector3(0.24f, 0.32f, 0.24f), RealmKit.Ochre);
            RealmKit.Shape(PrimitiveType.Sphere, "JarR", jars, new Vector3(0.66f, -0.3f, 0f), new Vector3(0.24f, 0.32f, 0.24f), RealmKit.Ochre);
            var driver = new GameObject("WorkDriver");
            driver.transform.SetParent(shabti, false);
            WorkerLoop loop = driver.AddComponent<WorkerLoop>();
            loop.worker = worker;
            loop.jars = jars;
            loop.from = new Vector3(-2.2f, 0f, 0.2f);
            loop.to = new Vector3(3.4f, 0f, 0.2f);
            loop.period = 6f;
            RealmObjectView shv = View(ctx, shabti, "shabti", new Vector3(0f, 1.6f, 0f));
            var sa = Pose(shv, "asleep", 0.8f); Part(sa, sHead, sHead.localPosition, new Vector3(28f, 0f, 0f)); sa.hide.Add(sEyes.gameObject); sa.hide.Add(driver);
            var sc = Pose(shv, "carrying", 1.0f); Part(sc, sHead, sHead.localPosition, Vector3.zero); sc.show.Add(sEyes.gameObject); sc.show.Add(driver);
            var se = Pose(shv, "empty", 1.0f); Part(se, sHead, sHead.localPosition, Vector3.zero); se.show.Add(sEyes.gameObject); se.show.Add(driver);

            for (int s = -1; s <= 1; s += 2) RealmKit.Brazier("FieldLamp", w, new Vector3(s * 7.5f, 0f, -1f), 10f, 2.4f);
            RealmKit.Brazier("SluiceLamp", w, new Vector3(-6.8f, 0f, 6.2f), 8f, 2.2f);
            RealmKit.Column("Obelisk", w, new Vector3(9f, 0f, 9f), 7f, 0.4f, RealmKit.Basalt, false);
            RealmKit.Motes(w, new Vector3(0f, 2f, 3f), new Vector3(22f, 4f, 16f), new Color(1f, 0.9f, 0.6f, 0.7f), 70);
            // The dyke path out, east.
            for (int i = 0; i < 6; i++)
                RealmKit.Box("Dyke" + i, w, new Vector3(9f + i * 1.8f, 0.05f, -3f), new Vector3(1.7f, 0.1f, 2.4f), RealmKit.Limestone, false);

            RealmKit.Blocker(w, new Vector3(0f, 2f, -9f), new Vector3(36f, 4f, 1f));
            RealmKit.Blocker(w, new Vector3(-11f, 2f, 0f), new Vector3(1f, 4f, 20f));
            RealmKit.Blocker(w, new Vector3(0f, 2f, 7.8f), new Vector3(30f, 4f, 0.6f));
            RealmKit.Blocker(w, new Vector3(18f, 2f, -3f), new Vector3(1f, 4f, 6f));
            RealmKit.Blocker(w, new Vector3(13f, 2f, 2.6f), new Vector3(10f, 4f, 1f));

            Cast(ctx, new Vector3(-6f, 0f, -6f), 45f, new Vector3(-7.4f, 0f, -7f));
            Zone(ctx, "field", new Vector3(0f, 1f, -0.5f), new Vector3(22f, 4f, 16f));
            Zone(ctx, "exit", new Vector3(15.5f, 1f, -3f), new Vector3(4f, 4f, 4f), -1, 2);
            Shot(ctx, "cam_field", new Vector3(-1.5f, 5.2f, -13.5f), new Vector3(0.5f, 0.8f, 4f), 50f, new Vector3(-2f, 1f, -1f), new Vector3(18f, 4f, 16f));
            Shot(ctx, "cam_basin", new Vector3(8.6f, 2.4f, -3.4f), new Vector3(1.5f, 1.2f, 3.4f), 50f, new Vector3(5f, 1f, 0f), new Vector3(6f, 4f, 12f), 0.08f, 1);
            Shot(ctx, "cam_dyke", new Vector3(8.5f, 3.2f, -9f), new Vector3(15f, 0.6f, -2f), 50f, new Vector3(13.5f, 1f, -3f), new Vector3(9f, 4f, 6f), 0.1f, 2);
            Finish(ctx, WernesScripts.Field);
        }

        // ------------------------------------------------------------------ Room 1.2: the House With One Door

        private static void BuildHouse()
        {
            Ctx ctx = Begin(WernesScripts.House, new Color(0.05f, 0.07f, 0.12f), 0.013f, 0.35f);
            Transform w = ctx.world;
            Ground(w, 160f, RealmKit.Mud);
            Dunes(w, 9, 85f);
            Reeds(w, new Vector3(0f, 0f, 14f), new Vector3(40f, 0f, 3f), 220, 31);

            // The walled plot (x -4..4, z -1..7), entered from the south.
            Transform plotWalls = RealmKit.Group("PlotWalls", w);
            RealmKit.Wall("North", plotWalls, new Vector3(0f, 0f, 7.6f), new Vector3(9.2f, 1.6f, 0.6f), RealmKit.Sandstone);
            RealmKit.Wall("West", plotWalls, new Vector3(-4.6f, 0f, 3.3f), new Vector3(0.6f, 1.6f, 9.2f), RealmKit.Sandstone);
            RealmKit.Wall("East", plotWalls, new Vector3(4.6f, 0f, 3.3f), new Vector3(0.6f, 1.6f, 9.2f), RealmKit.Sandstone);
            RealmKit.Wall("SouthL", plotWalls, new Vector3(-3.1f, 0f, -1.0f), new Vector3(2.4f, 1.0f, 0.6f), RealmKit.Sandstone);
            RealmKit.Wall("SouthR", plotWalls, new Vector3(3.1f, 0f, -1.0f), new Vector3(2.4f, 1.0f, 0.6f), RealmKit.Sandstone);
            // The plot surface and seedlings.
            Transform plot = RealmKit.Group("Plot", w, new Vector3(0f, 0f, 3.6f));
            RealmKit.Box("Soil", plot, new Vector3(0f, -0.12f, 0f), new Vector3(8.4f, 0.1f, 6.4f), RealmKit.Mud, false);
            var seedlings = new List<Transform>();
            for (int x = -3; x <= 3; x++)
                for (int z = -2; z <= 2; z++)
                {
                    Transform sd = RealmKit.Group("Seedling", plot, new Vector3(x * 1.1f, -0.07f, z * 1.1f));
                    RealmKit.Shape(PrimitiveType.Cylinder, "Stalk", sd, new Vector3(0f, 0.2f, 0f), new Vector3(0.03f, 0.2f, 0.03f), RealmKit.Reed);
                    RealmKit.Shape(PrimitiveType.Sphere, "Leaf", sd, new Vector3(0.05f, 0.38f, 0f), new Vector3(0.18f, 0.06f, 0.08f), RealmKit.Reed);
                    seedlings.Add(sd);
                }
            GameObject flood = RealmKit.Box("Flood", plot, new Vector3(0f, 0.28f, 0f), new Vector3(8.4f, 0.05f, 6.4f), RealmKit.WaterRough, false);
            flood.AddComponent<WaterMotion>().amplitude = 0.04f;
            GameObject scour = RealmKit.Box("Scour", plot, new Vector3(0f, 0.08f, 0f), new Vector3(8.4f, 0.04f, 6.4f), RealmKit.WaterRough, false);
            scour.AddComponent<WaterMotion>().amplitude = 0.08f;
            GameObject wetSheen = RealmKit.Box("WetSheen", plot, new Vector3(0f, -0.06f, 0f), new Vector3(8.4f, 0.02f, 6.4f), RealmKit.Transparent("WetSheen", new Color(0.1f, 0.1f, 0.12f, 0.45f), 0.9f), false);
            RealmObjectView pv = View(ctx, plot, "plot", new Vector3(0f, 0.8f, 0f));
            var pdw = Pose(pv, "drowning", 1.2f); pdw.show.Add(flood); pdw.hide.Add(scour); pdw.hide.Add(wetSheen);
            foreach (Transform sd in seedlings) Part(pdw, sd, sd.localPosition, Vector3.zero);
            var psc = Pose(pv, "scoured", 1.2f); psc.show.Add(scour); psc.hide.Add(flood); psc.hide.Add(wetSheen);
            foreach (Transform sd in seedlings) Part(psc, sd, sd.localPosition, new Vector3(0f, 0f, -48f));
            var pwt = Pose(pv, "wet", 2.4f); pwt.show.Add(wetSheen); pwt.hide.Add(flood); pwt.hide.Add(scour);
            foreach (Transform sd in seedlings) Part(pwt, sd, sd.localPosition, Vector3.zero, new Vector3(1.2f, 1.25f, 1.2f));

            // Upper sluice: high on the north wall, jammed open, pouring in.
            Transform upper = RealmKit.Group("UpperSluice", w, new Vector3(-2.2f, 1.6f, 7.6f));
            RealmKit.Box("JambL", upper, new Vector3(-0.8f, 0.7f, 0f), new Vector3(0.4f, 1.4f, 0.9f), RealmKit.Sandstone, false);
            RealmKit.Box("JambR", upper, new Vector3(0.8f, 0.7f, 0f), new Vector3(0.4f, 1.4f, 0.9f), RealmKit.Sandstone, false);
            RealmKit.Box("Head", upper, new Vector3(0f, 1.5f, 0f), new Vector3(2.1f, 0.3f, 1.0f), RealmKit.Sandstone, false);
            Transform uLeaf = RealmKit.Group("Leaf", upper, new Vector3(0f, 1.25f, 0f));
            RealmKit.Box("Board", uLeaf, Vector3.zero, new Vector3(1.2f, 1.0f, 0.14f), RealmKit.Wood, false);
            RealmKit.Box("Wedge", uLeaf, new Vector3(0.5f, -0.5f, -0.1f), new Vector3(0.2f, 0.2f, 0.2f), RealmKit.Basalt, false);
            GameObject uPour = RealmKit.Box("Pour", upper, new Vector3(0f, -0.4f, -0.6f), new Vector3(1.0f, 1.6f, 0.08f), RealmKit.WaterRough, false, new Vector3(-20f, 0f, 0f));
            uPour.AddComponent<WaterMotion>().amplitude = 0.02f;
            RealmKit.GlyphPlate("UpperGlyphs", upper, new Vector3(0f, 1.5f, -0.52f), new Vector3(0f, 180f, 0f), "Aa1 X1 G17 S20", 0.22f, false, new Color(0.8f, 0.7f, 0.5f, 0.3f));
            RealmObjectView uv = View(ctx, upper, "upper", new Vector3(0f, 0.8f, -0.5f));
            var uo = Pose(uv, "open", 0.4f); Part(uo, uLeaf, new Vector3(0f, 1.25f, 0f), Vector3.zero); uo.show.Add(uPour);
            var us = Pose(uv, "shut", 2.0f); Part(us, uLeaf, new Vector3(0f, 0.5f, 0f), Vector3.zero); us.hide.Add(uPour);

            // Lower sluice: front right, draining to a ditch outside the wall.
            Transform lower = RealmKit.Group("LowerSluice", w, new Vector3(4.6f, 0f, 1.2f), new Vector3(0f, 90f, 0f));
            RealmKit.Box("JambL", lower, new Vector3(-0.7f, 0.7f, 0f), new Vector3(0.35f, 1.4f, 0.8f), RealmKit.Sandstone, false);
            RealmKit.Box("JambR", lower, new Vector3(0.7f, 0.7f, 0f), new Vector3(0.35f, 1.4f, 0.8f), RealmKit.Sandstone, false);
            Transform lLeaf = RealmKit.Group("Leaf", lower, new Vector3(0f, 0.5f, 0f));
            RealmKit.Box("Board", lLeaf, Vector3.zero, new Vector3(1.05f, 0.95f, 0.14f), RealmKit.Wood, false);
            GameObject ditch = RealmKit.Box("DitchWater", w, new Vector3(7f, -0.1f, 1.2f), new Vector3(4f, 0.04f, 1.0f), RealmKit.WaterRough, false);
            RealmObjectView lowv = View(ctx, lower, "lower", new Vector3(0f, 0.8f, 0f));
            var lsh = Pose(lowv, "shut", 0.4f); Part(lsh, lLeaf, new Vector3(0f, 0.5f, 0f), Vector3.zero); lsh.hide.Add(ditch);
            var lop = Pose(lowv, "open", 1.8f); Part(lop, lLeaf, new Vector3(0f, 1.35f, 0f), Vector3.zero); lop.show.Add(ditch);

            // The false door on the west wall, lit.
            Transform fd = RealmKit.Group("FalseDoor", w, new Vector3(-4.2f, 0f, 3.4f), new Vector3(0f, 90f, 0f));
            RealmKit.Box("Frame", fd, new Vector3(0f, 1.35f, 0.05f), new Vector3(1.6f, 2.7f, 0.12f), RealmKit.Limestone, false);
            RealmKit.Box("Recess1", fd, new Vector3(0f, 1.25f, -0.02f), new Vector3(1.1f, 2.3f, 0.06f), RealmKit.Sandstone, false);
            RealmKit.Box("Niche", fd, new Vector3(0f, 1.1f, -0.06f), new Vector3(0.55f, 1.8f, 0.04f), RealmKit.Shadow, false);
            RealmKit.Box("Drum", fd, new Vector3(0f, 2.15f, -0.08f), new Vector3(0.7f, 0.12f, 0.1f), RealmKit.Ochre, false);
            RealmKit.GlyphPlate("Offering", fd, new Vector3(0f, 2.48f, -0.1f), new Vector3(0f, 180f, 0f), "R4 X1 Q3", 0.2f, false, new Color(1f, 0.8f, 0.45f, 0.9f));
            GameObject doorGlow = RealmKit.Box("DoorGlow", fd, new Vector3(0f, 1.1f, -0.09f), new Vector3(0.5f, 1.75f, 0.02f), RealmKit.Additive("FalseDoorGlow", new Color(1f, 0.75f, 0.4f, 0.65f), RealmKit.SoftDot()), false);
            Light fdLight = InnerGlow(fd, new Vector3(0f, 1.4f, -0.8f), 1.8f, 5f);
            RealmObjectView fdv = View(ctx, fd, "falsedoor", new Vector3(0f, 1.4f, -0.3f));
            var fdd = Pose(fdv, "dark", 0.2f); fdd.show.Add(doorGlow); fdd.lights.Add(fdLight); fdd.lightIntensities.Add(1.8f);
            var fdr = Pose(fdv, "remembered", 2f); fdr.hide.Add(doorGlow); fdr.lights.Add(fdLight); fdr.lightIntensities.Add(0.4f);

            RealmKit.Brazier("PlotLamp_L", w, new Vector3(-2.2f, 0f, -2.2f), 9f, 2.3f);
            RealmKit.Brazier("PlotLamp_R", w, new Vector3(2.2f, 0f, -2.2f), 9f, 2.3f);
            RealmKit.Motes(w, new Vector3(0f, 1.6f, 2f), new Vector3(14f, 3f, 12f), new Color(1f, 0.88f, 0.6f, 0.7f), 50);

            // The plot is walkable along a raised edge path inside the walls, and the soil itself.
            RealmKit.Blocker(w, new Vector3(0f, 2f, -8f), new Vector3(20f, 4f, 1f));
            RealmKit.Blocker(w, new Vector3(-6f, 2f, -4f), new Vector3(1f, 4f, 9f));
            RealmKit.Blocker(w, new Vector3(6f, 2f, -4f), new Vector3(1f, 4f, 9f));

            Cast(ctx, new Vector3(0f, 0f, -6f), 0f, new Vector3(-1.4f, 0f, -7f));
            Zone(ctx, "outside", new Vector3(0f, 1f, -4.5f), new Vector3(11f, 4f, 6f));
            Zone(ctx, "plot", new Vector3(0f, 1f, 3.4f), new Vector3(8.6f, 4f, 8.4f), -1, 1);
            Zone(ctx, "falsedoor", new Vector3(-3.4f, 1f, 3.4f), new Vector3(1.8f, 4f, 2.4f), -1, 2);
            Shot(ctx, "cam_outside", new Vector3(3.2f, 4.2f, -11.5f), new Vector3(-0.5f, 0.6f, 3.5f), 50f, new Vector3(0f, 1f, -4.5f), new Vector3(12f, 4f, 7f));
            Shot(ctx, "cam_plot", new Vector3(3.4f, 4.6f, -0.6f), new Vector3(-2.2f, 0.8f, 5.6f), 56f, new Vector3(0f, 1f, 3.4f), new Vector3(9f, 4f, 9f), 0.08f, 1);
            Finish(ctx, WernesScripts.House);
        }

        private static Transform Mark(Transform parent, string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            return g.transform;
        }

        private static Light InnerGlow(Transform parent, Vector3 localPos, float intensity, float range = 9f)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.78f, 0.42f);
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            return l;
        }

        /// <summary>A tall standing divine figure from simple solids: kilt, torso, broad collar, wig, head on a pivot.</summary>
        private static Transform Statue(Transform parent, string name, Vector3 pos, float yaw, out Transform head, Material stone)
        {
            Transform s = RealmKit.Group(name, parent, pos, new Vector3(0f, 180f + yaw, 0f));
            RealmKit.Box("Plinth", s, new Vector3(0f, 0.25f, 0f), new Vector3(1.4f, 0.5f, 1.4f), RealmKit.Sandstone);
            RealmKit.Box("Legs", s, new Vector3(0f, 1.1f, 0f), new Vector3(0.6f, 1.2f, 0.4f), stone, false);
            RealmKit.Box("Kilt", s, new Vector3(0f, 1.75f, 0f), new Vector3(0.8f, 0.5f, 0.5f), RealmKit.Limestone, false);
            RealmKit.Box("Belt", s, new Vector3(0f, 2.0f, 0f), new Vector3(0.82f, 0.08f, 0.52f), RealmKit.Gold, false);
            RealmKit.Box("Torso", s, new Vector3(0f, 2.55f, 0f), new Vector3(0.85f, 1.0f, 0.45f), stone, false);
            RealmKit.Shape(PrimitiveType.Cylinder, "Collar", s, new Vector3(0f, 3.0f, 0f), new Vector3(1.0f, 0.06f, 0.6f), RealmKit.Gold);
            RealmKit.Box("ArmL", s, new Vector3(-0.55f, 2.45f, 0f), new Vector3(0.22f, 1.1f, 0.26f), stone, false);
            RealmKit.Box("ArmR", s, new Vector3(0.55f, 2.45f, 0.05f), new Vector3(0.22f, 1.1f, 0.26f), stone, false);
            RealmKit.Box("Staff", s, new Vector3(0.7f, 2.2f, 0.25f), new Vector3(0.07f, 3.6f, 0.07f), RealmKit.Gold, false);
            RealmKit.Shape(PrimitiveType.Cylinder, "Collider", s, new Vector3(0f, 1.5f, 0f), new Vector3(1.2f, 1.5f, 1.2f), RealmKit.Shadow, true).GetComponent<MeshRenderer>().enabled = false;
            head = RealmKit.Group("Head", s, new Vector3(0f, 3.15f, 0f));
            RealmKit.Box("Wig", head, new Vector3(0f, 0.32f, 0.04f), new Vector3(0.62f, 0.7f, 0.5f), RealmKit.Lapis, false);
            RealmKit.Box("Face", head, new Vector3(0f, 0.32f, -0.14f), new Vector3(0.42f, 0.52f, 0.3f), stone, false);
            RealmKit.Box("Beard", head, new Vector3(0f, 0.0f, -0.18f), new Vector3(0.1f, 0.24f, 0.08f), RealmKit.Lapis, false);
            Transform eyes = RealmKit.Group("Eyes", head);
            RealmKit.Shape(PrimitiveType.Sphere, "EyeL", eyes, new Vector3(-0.1f, 0.4f, -0.3f), Vector3.one * 0.06f, RealmKit.EyeGlow);
            RealmKit.Shape(PrimitiveType.Sphere, "EyeR", eyes, new Vector3(0.1f, 0.4f, -0.3f), Vector3.one * 0.06f, RealmKit.EyeGlow);
            return s;
        }

        private static void Pillar(Transform parent, string name, Vector3 pos, string signs)
        {
            Transform p = RealmKit.Group(name, parent, pos);
            RealmKit.Box("Shaft", p, new Vector3(0f, 1.6f, 0f), new Vector3(0.9f, 3.2f, 0.9f), RealmKit.Sandstone);
            RealmKit.Box("Cap", p, new Vector3(0f, 3.3f, 0f), new Vector3(1.1f, 0.2f, 1.1f), RealmKit.Sandstone, false);
            float face = pos.x < 0 ? 0.46f : -0.46f;
            RealmKit.GlyphPlate("Name", p, new Vector3(face, 2.1f, 0f), new Vector3(0f, pos.x < 0 ? -90f : 90f, 0f), signs, 0.42f, true, new Color(1f, 0.8f, 0.45f, 0.9f));
            InnerGlow(p, new Vector3(face * 2.4f, 2.4f, 0f), 0.8f, 3.5f);
        }

        // ------------------------------------------------------------------ build settings

        public static void AddToBuildSettings()
        {
            string[] realm = AssetDatabase.FindAssets("t:Scene", new[] { RealmKit.SceneDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileName(p).StartsWith("Realm_"))
                .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal)
                .ToArray();
            var list = EditorBuildSettings.scenes.Where(s => !realm.Contains(s.path)).ToList();
            int walk = list.FindIndex(s => s.path == WalkScenePath);
            var add = realm.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
            if (walk >= 0) list.InsertRange(walk + 1, add);
            else list.AddRange(add);
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[Realms] Build Settings: " + string.Join(", ", list.Select(s => Path.GetFileNameWithoutExtension(s.path))));
        }
    }
}
