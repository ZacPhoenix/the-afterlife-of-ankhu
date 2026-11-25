using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ankhu.Realms.Core.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Rosetau, the realm after Wernes, built side-on for the painted 2.5D look (docs/PAINTED_ROOM_SPEC.md).
    /// Three rooms: The Laugh in the Dark, The Room of the Mourners, The Mother's Room. Design: docs/ROSETAU_DESIGN.md.
    ///   Unity -batchmode -projectPath . -executeMethod Ankhu.Realms.EditorTools.RealmSceneBuilder.BuildRosetauCli -logFile rosetau.log
    ///   ... RealmSceneBuilder.CaptureRosetauPlatesCli   greybox plates for painting, art_gen/rosetau/plates
    ///   ... RealmSceneBuilder.CaptureRosetauCli         start and end captures, art_gen/realms/captures
    ///   ... RealmSceneBuilder.AuditRosetauCli           painted audit, art_gen/realms/audit/painted_audit_rosetau.md
    /// Paintings live at Art/Rosetau/&lt;scene&gt;_&lt;camera&gt;.png and sprite cards at Art/Rosetau/Sprites.
    /// Layout convention as Wernes: x across the screen, y up, z depth, walk lane z -2..2, back wall near z 3.
    /// </summary>
    public static partial class RealmSceneBuilder
    {
        public const string RosetauArtDir = "Assets/Ankhu/Realms/Art/Rosetau";
        public static string RosetauPlateDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../art_gen/rosetau/plates"));

        [MenuItem("Ankhu/Realms/Build Rosetau")]
        public static void BuildRosetauMenu() => BuildRosetau();

        public static void BuildRosetauCli() => RunCli(BuildRosetau);

        public static void BuildRosetau()
        {
            RealmKit.EnsureDirs();
            EnsureFolder(RosetauArtDir);
            EnsureFolder(RosetauArtDir + "/Sprites");
            EnsureFolder(RosetauArtDir + "/Materials");
            BuildLaughA();
            BuildMournersB();
            BuildMotherC();
            BuildTwoWaysD();
            BuildFinaleE();
            AddRosetauToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Rosetau] scenes built.");
        }

        private static void RunCli(Action a)
        {
            try { a(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Appends the Rosetau_* scenes at the end of Build Settings without moving anything else.</summary>
        public static void AddRosetauToBuildSettings()
        {
            string[] ours = RosetauScripts.Scenes.Select(s => RealmKit.SceneDir + "/" + s + ".unity").Where(File.Exists).ToArray();
            var list = EditorBuildSettings.scenes.Where(s => !ours.Contains(s.path)).ToList();
            list.AddRange(ours.Select(p => new EditorBuildSettingsScene(p, true)));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ painted pieces

        /// <summary>A sprite card material from Art/Rosetau/Sprites, falling back to the Wernes sprites. Null if neither has it.</summary>
        private static Material RCard(string sprite, out float aspect)
        {
            aspect = 1f;
            string path = RosetauArtDir + "/Sprites/" + sprite + ".png";
            if (!File.Exists(path)) path = WernesArtDir + "/Sprites/" + sprite + ".png";
            if (!File.Exists(path)) { Debug.LogWarning("[Rosetau] missing card " + sprite); return null; }
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp != null && (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp || imp.maxTextureSize < 2048 || imp.npotScale != TextureImporterNPOTScale.None))
            {
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.alphaIsTransparency = true; imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Clamp;
                imp.maxTextureSize = 2048; imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return null;
            aspect = tex.width / (float)tex.height;
            if (imp != null) { imp.GetSourceTextureWidthAndHeight(out int sw, out int sh); if (sh > 0) aspect = sw / (float)sh; }
            string mp = RosetauArtDir + "/Materials/card_r_" + sprite + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, mp); }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(0.93f, 0.9f, 0.86f, 1f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_Cull", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// A painted figure card standing on its feet at pos, height metres tall. With no sprite yet it is a grey
        /// stand-in block, which the painted audit reports, so a missing sprite can never pass for done.
        /// </summary>
        private static GameObject RFigure(Transform parent, string name, string sprite, Vector3 pos, float height)
        {
            Material m = RCard(sprite, out float aspect);
            Transform card = NewCard(name, parent, pos, Quaternion.identity);
            if (m != null) Quad(card, m, new Vector3(0f, height * 0.5f, 0f), new Vector2(height * aspect, height));
            else RealmKit.Box("StandIn_" + sprite, card, new Vector3(0f, height * 0.5f, 0f), new Vector3(height * 0.5f, height, 0.2f), RealmKit.Limestone, false);
            return card.gameObject;
        }

        /// <summary>An additive painted glow on Ankhu/PaintedFlipbook, saved under Art/Rosetau/Materials.</summary>
        private static Material RFx(string matName, string sprite, Color tint, float gain, float pulse, float pulseSpeed, float flicker)
        {
            Texture2D tex = FxTex(sprite);
            if (tex == null) return null;
            Shader sh = Shader.Find("Ankhu/PaintedFlipbook");
            if (sh == null) { Debug.LogError("[Rosetau] Ankhu/PaintedFlipbook did not compile"); return null; }
            string path = RosetauArtDir + "/Materials/fx_r_" + matName + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Cols", 1f); m.SetFloat("_Rows", 1f);
            m.SetFloat("_FrameStart", 0f); m.SetFloat("_FrameCount", 1f); m.SetFloat("_Fps", 0f); m.SetFloat("_Loop", 1f);
            m.SetColor("_Tint", tint);
            m.SetFloat("_Gain", gain);
            m.SetFloat("_PulseAmp", pulse); m.SetFloat("_PulseSpeed", pulseSpeed); m.SetFloat("_Flicker", flicker);
            m.SetFloat("_Sway", 0f); m.SetFloat("_SwaySpeed", 1.1f);
            m.SetFloat("_AlphaCut", 0.004f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A camera-facing glow quad group, returned so poses can show or hide it.</summary>
        private static GameObject RGlow(Transform parent, string name, Material m, Vector3 pos, Vector2 size)
        {
            Transform g = NewCard(name, parent, pos, Quaternion.identity);
            if (m != null) Quad(g, m, Vector3.zero, size);
            return g.gameObject;
        }

        private static Material RPaintingMaterial(string name, Texture2D tex)
        {
            string path = RosetauArtDir + "/Materials/painting_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background + 10;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// The Wernes paint pass for a Rosetau scene: each camera with a painting gets it on a locked backdrop quad,
        /// the set moves to the hidden layer (colliders stay), particles take painted sprites, and the cast's shadows
        /// fall on invisible catchers. Rosetau's live pieces are already cards, so there is no cardify step.
        /// </summary>
        private static void ApplyRosetauPaint(Ctx ctx, string sceneName, Rect4[] catchers)
        {
            int painted = 0;
            foreach (var shot in ctx.shots)
            {
                string path = RosetauArtDir + "/" + sceneName + "_" + shot.name + ".png";
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
                mr.sharedMaterial = RPaintingMaterial(sceneName + "_" + shot.name, tex);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                cam.farClipPlane = 340f;
                quad.AddComponent<Ankhu.Opening.BackdropFit>().Configure(cam, 300f, tex.width / (float)tex.height);
                shot.follow = 0f;
                painted++;
            }
            if (painted == 0) { Debug.Log("[Rosetau] " + sceneName + ": no paintings yet, greybox only"); return; }
            RenderSettings.fog = false;
            PaintParticles();
            foreach (Renderer r in SetRenderers())
            {
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
            Debug.Log("[Rosetau] " + sceneName + ": " + painted + " painted cameras");
        }

        /// <summary>Sets every view to its first pose in the saved scene, so the editor shows the room as play opens it.</summary>
        private static void SnapAllFirst(Ctx ctx)
        {
            foreach (RealmObjectView v in ctx.director.views)
                if (v != null && v.poses.Count > 0) SnapFirst(v.poses[0]);
        }

        private static RealmObjectView QuietView(Ctx ctx, Transform root, string id)
        {
            var v = root.gameObject.AddComponent<RealmObjectView>();
            v.objectId = id;
            ctx.director.views.Add(v);
            return v;
        }

        private static void Lamp(RealmObjectView.StatePose p, Light l, float i) { p.lights.Add(l); p.lightIntensities.Add(i); }

        // ------------------------------------------------------------------ Room A: The Laugh in the Dark

        private static readonly float[] MouthX = { 0f, -3f, 2.5f, 8f };

        private static void BuildLaughA()
        {
            string scene = RosetauScripts.LaughA;
            Ctx ctx = Begin(scene, new Color(0.03f, 0.03f, 0.05f), 0.012f, 0.2f);
            Transform w = ctx.world;
            Ground(w, 120f, RealmKit.Basalt);
            Material rock = RealmKit.Lit("RosetauRock", new Color(0.22f, 0.20f, 0.22f), 0.2f, 0f,
                RealmKit.StoneTexture("rosetaurock", new Color(0.10f, 0.09f, 0.10f), new Color(0.30f, 0.27f, 0.28f), 0.8f, 117), 2f);

            // The descending stair at the west end, with the spell 117 lintel over its foot.
            Transform stair = RealmKit.Group("Stair", w);
            for (int k = 0; k < 6; k++)
                RealmKit.Box("Step" + k, stair, new Vector3(-15.2f + k * 0.5f, 1.35f - k * 0.25f, 0.6f), new Vector3(0.55f, 0.25f, 3.4f), rock, false);
            RealmKit.Box("StairWallBack", stair, new Vector3(-13.8f, 2.6f, 2.6f), new Vector3(3.6f, 5.2f, 0.6f), rock, false);
            RealmKit.Box("JambW", stair, new Vector3(-12.1f, 1.6f, 2.2f), new Vector3(0.5f, 3.2f, 0.7f), RealmKit.Basalt, false);
            RealmKit.Box("Lintel", stair, new Vector3(-13.4f, 3.35f, 1.9f), new Vector3(3.2f, 0.5f, 0.5f), RealmKit.Basalt, false);
            RealmKit.GlyphPlate("Spell117", stair, new Vector3(-13.4f, 3.35f, 1.62f), new Vector3(0f, 180f, 0f), "D21 N35 D21 O34 X1 G1 O1", 0.26f, false, new Color(1f, 0.78f, 0.4f, 0.85f));

            // The long back wall with three low doorways.
            Transform wall = RealmKit.Group("Wall", w);
            float[] edges = { -12f, MouthX[1], MouthX[2], MouthX[3], 11.5f };
            for (int s = 0; s < 4; s++)
            {
                float a = s == 0 ? edges[0] : edges[s] + 0.8f, b = s == 3 ? edges[4] : edges[s + 1] - 0.8f;
                RealmKit.Box("Pier" + s, wall, new Vector3((a + b) * 0.5f, 2.4f, 3.2f), new Vector3(b - a, 4.8f, 0.6f), rock, false);
            }
            Material voidMat = RealmKit.Shadow;
            var mouthViews = new RealmObjectView[4];
            string[] chamberSprite = { "", "a_chamber_knuckle", "a_chamber_top", "a_chamber_sandal" };
            Material doorGlow = RFx("a_door_glow", "fx_glow", new Color(1f, 0.82f, 0.5f, 1f), 1.4f, 0.25f, 1.3f, 0.1f);
            for (int k = 1; k <= 3; k++)
            {
                Transform m = RealmKit.Group("Mouth" + k, wall, new Vector3(MouthX[k], 0f, 3.2f));
                RealmKit.Box("JambL", m, new Vector3(-0.95f, 1.15f, -0.32f), new Vector3(0.3f, 2.3f, 0.2f), RealmKit.Basalt, false);
                RealmKit.Box("JambR", m, new Vector3(0.95f, 1.15f, -0.32f), new Vector3(0.3f, 2.3f, 0.2f), RealmKit.Basalt, false);
                RealmKit.Box("Head", m, new Vector3(0f, 2.45f, -0.32f), new Vector3(2.2f, 0.34f, 0.24f), RealmKit.Basalt, false);
                RealmKit.Box("Over", m, new Vector3(0f, 3.7f, 0f), new Vector3(1.6f, 2.2f, 0.6f), rock, false);
                RealmKit.Box("Void", m, new Vector3(0f, 1.15f, 0.25f), new Vector3(1.6f, 2.3f, 0.1f), voidMat, false);
                RealmKit.Box("Threshold", m, new Vector3(0f, 0.04f, -0.45f), new Vector3(1.8f, 0.08f, 0.4f), RealmKit.Basalt, false);

                GameObject glow = RGlow(m, "LitGlow", doorGlow, new Vector3(0f, 1.2f, -0.1f), new Vector2(2.6f, 3.0f));
                GameObject chamber = RFigure(m, "Chamber", chamberSprite[k], new Vector3(0f, 0.05f, -0.05f), 2.2f);
                Light l = InnerGlow(m, new Vector3(0f, 1.3f, -1.2f), 0f, 6f);
                RealmObjectView v = View(ctx, m, "mouth" + k, new Vector3(0f, 2.8f, -0.5f));
                var pd = Pose(v, "dark", 0.8f); pd.hide.Add(glow); pd.hide.Add(chamber); Lamp(pd, l, 0f);
                var pl = Pose(v, "lit", 1.2f); pl.show.Add(glow); pl.hide.Add(chamber); Lamp(pl, l, 2.2f);
                var po = Pose(v, "open", 2.0f); po.hide.Add(glow); po.show.Add(chamber); Lamp(po, l, 1.2f);
                mouthViews[k] = v;
                Zone(ctx, "mouth" + k, new Vector3(MouthX[k], 1f, 0.2f), new Vector3(3.0f, 4f, 5f), -1, 2);
            }

            // The laugh: gold glints that hang in each doorway while it sounds, thinner as it fades, one cyan bead held.
            Transform laughRoot = RealmKit.Group("Laugh", w);
            Material glint = RFx("a_laugh_glint", "fx_glint", new Color(1f, 0.85f, 0.55f, 1f), 1.6f, 0.5f, 3.2f, 0.3f);
            Material glintDim = RFx("a_laugh_glint_dim", "fx_glint", new Color(1f, 0.85f, 0.55f, 0.5f), 0.8f, 0.4f, 2.0f, 0.2f);
            Material held = RFx("a_laugh_held", "fx_bead_cyan", new Color(0.7f, 0.95f, 1f, 1f), 1.5f, 0.35f, 1.4f, 0.1f);
            Transform sounding = RealmKit.Group("Sounding", laughRoot);
            Transform fading = RealmKit.Group("Fading", laughRoot);
            for (int k = 1; k <= 3; k++)
            {
                RGlow(sounding, "Glint" + k, glint, new Vector3(MouthX[k], 2.0f, 2.6f), new Vector2(1.4f, 1.4f));
                RGlow(fading, "Glint" + k, glintDim, new Vector3(MouthX[k], 2.2f, 2.6f), new Vector2(0.8f, 0.8f));
            }
            GameObject heldGo = RGlow(laughRoot, "Held", held, new Vector3(2.5f, 3.4f, 1.8f), new Vector2(1.2f, 1.2f));
            RealmObjectView lv = View(ctx, laughRoot, "laugh", new Vector3(2.5f, 3.2f, 1.8f));
            var ls = Pose(lv, "silent", 0.6f); ls.hide.Add(sounding.gameObject); ls.hide.Add(fading.gameObject); ls.hide.Add(heldGo);
            var lso = Pose(lv, "sounding", 0.4f); lso.show.Add(sounding.gameObject); lso.hide.Add(fading.gameObject); lso.hide.Add(heldGo);
            var lf = Pose(lv, "fading", 0.8f); lf.hide.Add(sounding.gameObject); lf.show.Add(fading.gameObject); lf.hide.Add(heldGo);
            var lh = Pose(lv, "held", 0.8f); lh.hide.Add(sounding.gameObject); lh.hide.Add(fading.gameObject); lh.show.Add(heldGo);

            // The child in the last doorway: finger at his lips, then holding the falcon up.
            Transform childRoot = RealmKit.Group("Child", w, new Vector3(MouthX[3], 0f, 2.7f));
            GameObject finger = RFigure(childRoot, "ChildFinger", "a_child_finger", new Vector3(0f, 0.05f, 0f), 1.25f);
            GameObject holding = RFigure(childRoot, "ChildFalcon", "a_child_falcon", new Vector3(0f, 0.05f, 0f), 1.25f);
            Light childLight = InnerGlow(childRoot, new Vector3(0f, 1.2f, -1.0f), 0f, 5f);
            childLight.color = new Color(1f, 0.86f, 0.62f);
            RealmObjectView cv = View(ctx, childRoot, "child", new Vector3(0f, 1.6f, -0.3f));
            var ch = Pose(cv, "hidden", 0.4f); ch.hide.Add(finger); ch.hide.Add(holding); Lamp(ch, childLight, 0f);
            var cs = Pose(cv, "seen", 1.6f); cs.show.Add(finger); cs.hide.Add(holding); Lamp(cs, childLight, 1.6f);
            var chd = Pose(cv, "holding", 1.0f); chd.hide.Add(finger); chd.show.Add(holding); Lamp(chd, childLight, 2.2f);

            RealmKit.Brazier("StairLamp", w, new Vector3(-10.6f, 0f, 2.2f), 8f, 1.6f);
            RealmKit.Brazier("MidLamp", w, new Vector3(-0.3f, 0f, 2.4f), 7f, 1.3f);
            RealmKit.Brazier("EastLamp", w, new Vector3(10.4f, 0f, 2.4f), 7f, 1.3f);
            RealmKit.Motes(w, new Vector3(0f, 2f, 1f), new Vector3(22f, 3f, 4f), new Color(1f, 0.85f, 0.6f, 0.45f), 80);

            Cast(ctx, new Vector3(-11.2f, 0f, -0.4f), 90f, new Vector3(-13.0f, 0f, -0.6f));

            // Hori's clay falcon in Ankhu's hand, carried from Wernes. Given, it leaves his hand for the child's.
            Transform hand = RealmKit.Group("FalconHand", ctx.ankhu.transform, new Vector3(0.28f, 1.05f, 0.25f));
            GameObject falcon = RFigure(hand, "Falcon", "abc_falcon", Vector3.zero, 0.28f);
            Transform fview = RealmKit.Group("FalconView", w);
            RealmObjectView fv = View(ctx, fview, "falcon", new Vector3(-11f, 1.6f, -0.4f));
            var fh = Pose(fv, "hand", 0.3f); fh.show.Add(falcon);
            var fg = Pose(fv, "given", 0.6f); fg.hide.Add(falcon);

            RealmKit.Blocker(w, new Vector3(-1f, 3f, -2.5f), new Vector3(25f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(-1f, 3f, 2.7f), new Vector3(25f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(-12.4f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));
            RealmKit.Blocker(w, new Vector3(11.2f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));

            Zone(ctx, "entry", new Vector3(-10.4f, 1f, 0.2f), new Vector3(3.6f, 4f, 5f), -1, 1);

            Shot(ctx, "cam_stair", new Vector3(-7.6f, 4.2f, -15f), new Vector3(-7.6f, 1.9f, 1f), 40f, new Vector3(-9f, 3f, 0f), new Vector3(9f, 10f, 8f), 0.04f, 0);
            Shot(ctx, "cam_mouths", new Vector3(2.6f, 4.2f, -15f), new Vector3(2.6f, 1.9f, 1f), 40f, new Vector3(3.25f, 3f, 0f), new Vector3(16.5f, 10f, 8f), 0.04f, 0);

            SnapAllFirst(ctx);
            ApplyRosetauPaint(ctx, scene, new[] { new Rect4(new Vector3(-0.5f, 0.012f, 0.2f), new Vector2(24f, 5.4f)) });
            Finish(ctx, scene);
        }

        // ------------------------------------------------------------------ Room B: The Room of the Mourners

        private static void BuildMournersB()
        {
            string scene = RosetauScripts.MournersB;
            Ctx ctx = Begin(scene, new Color(0.04f, 0.035f, 0.05f), 0.010f, 0.25f);
            Transform w = ctx.world;
            Ground(w, 120f, RealmKit.Limestone);
            Material plaster = RealmKit.Lit("TombPlaster", new Color(0.80f, 0.72f, 0.56f), 0.1f);
            Material linen = RealmKit.Lit("Linen", new Color(0.88f, 0.84f, 0.74f), 0.05f);
            Material black = RealmKit.Lit("BierBlack", new Color(0.08f, 0.07f, 0.06f), 0.4f);

            // The hall: plastered back wall with a painted band, the inscription panel at the west end, the door east.
            Transform hall = RealmKit.Group("Hall", w);
            RealmKit.Box("BackWall", hall, new Vector3(-1.2f, 2.4f, 3.2f), new Vector3(17.6f, 4.8f, 0.5f), plaster, false);
            RealmKit.Box("DoorWallR", hall, new Vector3(9.4f, 2.4f, 3.2f), new Vector3(1.2f, 4.8f, 0.5f), plaster, false);
            RealmKit.Box("DoorOver", hall, new Vector3(8.0f, 3.85f, 3.2f), new Vector3(1.6f, 1.9f, 0.5f), plaster, false);
            RealmKit.Box("Dado", hall, new Vector3(0f, 0.3f, 2.92f), new Vector3(19f, 0.6f, 0.06f), RealmKit.Ochre, false);
            RealmKit.Box("Frieze", hall, new Vector3(0f, 4.3f, 2.92f), new Vector3(19f, 0.3f, 0.06f), RealmKit.Lapis, false);
            RealmKit.Box("WestWall", hall, new Vector3(-9.8f, 2.4f, 0.4f), new Vector3(0.5f, 4.8f, 5.8f), plaster, false);
            RealmKit.Box("Panel", hall, new Vector3(-6.8f, 2.2f, 2.9f), new Vector3(2.4f, 2.0f, 0.06f), RealmKit.Limestone, false);
            RealmKit.GlyphPlate("Lament", hall, new Vector3(-6.8f, 2.2f, 2.84f), new Vector3(0f, 180f, 0f), "W19 D21 O1 V31", 0.32f, false, new Color(0.25f, 0.35f, 0.7f, 0.9f));

            // Ausar on the lion bier, set paint.
            Transform bier = RealmKit.Group("Bier", w, new Vector3(0f, 0f, 1.3f));
            RealmKit.Box("Bed", bier, new Vector3(0f, 0.85f, 0f), new Vector3(3.2f, 0.18f, 1.0f), black, true);
            for (int i = 0; i < 4; i++)
                RealmKit.Box("Leg" + i, bier, new Vector3(i < 2 ? -1.4f : 1.4f, 0.4f, i % 2 == 0 ? -0.4f : 0.4f), new Vector3(0.14f, 0.8f, 0.14f), black, false);
            RealmKit.Box("LionHead", bier, new Vector3(-1.75f, 1.2f, 0f), new Vector3(0.4f, 0.55f, 0.45f), RealmKit.Gold, false);
            RealmKit.Box("LionTail", bier, new Vector3(1.7f, 1.05f, 0f), new Vector3(0.3f, 0.12f, 0.12f), RealmKit.Gold, false);
            RealmKit.Shape(PrimitiveType.Capsule, "Ausar", bier, new Vector3(0f, 1.18f, 0f), new Vector3(0.55f, 1.35f, 0.45f), linen, false, new Vector3(0f, 0f, 90f));
            RealmKit.Box("Mask", bier, new Vector3(-1.05f, 1.28f, 0f), new Vector3(0.36f, 0.3f, 0.36f), RealmKit.Gold, false);

            // The two mourners. Bound, keening and quiet are three painted poses of each.
            string[] who = { "auset", "nebethet" };
            string[] sprite = { "b_auset", "b_nebet" };
            float[] wx = { -2.5f, 2.5f };
            string[] zone = { "head", "feet" };
            for (int i = 0; i < 2; i++)
            {
                Transform root = RealmKit.Group(who[i] == "auset" ? "Auset" : "NebetHet", w, new Vector3(wx[i], 0f, 1.0f));
                GameObject bound = RFigure(root, "Bound", sprite[i] + "_bound", Vector3.zero, 1.5f);
                GameObject keen = RFigure(root, "Keening", sprite[i] + "_keen", Vector3.zero, 1.5f);
                GameObject quiet = RFigure(root, "Quiet", sprite[i] + "_quiet", Vector3.zero, 1.5f);
                Material voiceMat = RFx("b_voice_" + i, "fx_streak", i == 0 ? new Color(1f, 0.85f, 0.6f, 0.8f) : new Color(0.75f, 0.85f, 1f, 0.8f), 1.0f, 0.5f, 2.4f, 0.2f);
                GameObject voice = RGlow(root, "Voice", voiceMat, new Vector3(i == 0 ? 0.9f : -0.9f, 1.9f, 0.1f), new Vector2(1.6f, 0.5f));
                RealmObjectView v = View(ctx, root, who[i], new Vector3(0f, 2.0f, -0.3f));
                var pb = Pose(v, "bound", 0.6f); pb.show.Add(bound); pb.hide.Add(keen); pb.hide.Add(quiet); pb.hide.Add(voice);
                var pk = Pose(v, "keening", 0.8f); pk.hide.Add(bound); pk.show.Add(keen); pk.hide.Add(quiet); pk.show.Add(voice);
                var pq = Pose(v, "quiet", 1.0f); pq.hide.Add(bound); pq.hide.Add(keen); pq.show.Add(quiet); pq.hide.Add(voice);
                Zone(ctx, zone[i], new Vector3(wx[i], 1f, 0.2f), new Vector3(2.6f, 4f, 5f), -1, 2);
            }

            // The answered lament: a thread of light across the bier between the two voices.
            Transform lament = RealmKit.Group("Lament", w);
            Material threadMat = RFx("b_lament", "fx_streak", new Color(1f, 0.9f, 0.7f, 1f), 1.5f, 0.4f, 1.6f, 0.15f);
            GameObject thread = RGlow(lament, "Thread", threadMat, new Vector3(0f, 2.4f, 1.0f), new Vector2(5.0f, 0.6f));
            RealmObjectView lav = View(ctx, lament, "lament", new Vector3(0f, 2.6f, 1.0f));
            var lu = Pose(lav, "unanswered", 0.8f); lu.hide.Add(thread);
            var la = Pose(lav, "answered", 1.2f); la.show.Add(thread);

            // Four lamps at the bier corners, lit one by one.
            Transform lamps = RealmKit.Group("Lamps", w);
            Vector3[] lampPos = { new Vector3(-1.9f, 0f, 0.3f), new Vector3(1.9f, 0f, 0.3f), new Vector3(-1.9f, 0f, 2.3f), new Vector3(1.9f, 0f, 2.3f) };
            Material flameMat = RFx("b_flame", "fx_ember", new Color(1f, 0.8f, 0.5f, 1f), 1.6f, 0.2f, 5f, 0.5f);
            var flames = new GameObject[4];
            var lights = new Light[4];
            for (int i = 0; i < 4; i++)
            {
                Transform st = RealmKit.Group("Lamp" + i, lamps, lampPos[i]);
                RealmKit.Shape(PrimitiveType.Cylinder, "Stand", st, new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 0.55f, 0.12f), RealmKit.Wood);
                RealmKit.Shape(PrimitiveType.Cylinder, "Bowl", st, new Vector3(0f, 1.13f, 0f), new Vector3(0.36f, 0.05f, 0.36f), RealmKit.Ochre);
                flames[i] = RGlow(st, "Flame", flameMat, new Vector3(0f, 1.38f, 0f), new Vector2(0.5f, 0.6f));
                lights[i] = InnerGlow(st, new Vector3(0f, 1.5f, -0.3f), 0f, 6f);
            }
            RealmObjectView lpv = View(ctx, lamps, "lamps", new Vector3(0f, 2.2f, 1.3f));
            for (int n = 0; n <= 4; n++)
            {
                var p = Pose(lpv, n.ToString(), 1.2f);
                for (int i = 0; i < 4; i++)
                {
                    (i < n ? p.show : p.hide).Add(flames[i]);
                    Lamp(p, lights[i], i < n ? 1.5f : 0f);
                }
            }

            // Breath over the dead man's chest once the room is full of light.
            Transform full = RealmKit.Group("Breath", w, new Vector3(0f, 1.55f, 1.3f));
            Material breathMat = RFx("b_breath", "fx_glow", new Color(1f, 0.9f, 0.7f, 0.9f), 1.0f, 0.6f, 0.8f, 0f);
            GameObject breath = RGlow(full, "Glow", breathMat, Vector3.zero, new Vector2(1.6f, 1.0f));
            RealmObjectView bv = QuietView(ctx, full, "full");
            var bn = Pose(bv, "no", 0.4f); bn.hide.Add(breath);
            var by = Pose(bv, "yes", 2.4f); by.show.Add(breath);

            // The far door: sealed with a painted mud seal, then open on warm light.
            Transform door = RealmKit.Group("Door", w, new Vector3(8.0f, 0f, 3.2f));
            RealmKit.Box("Opening", door, new Vector3(0f, 1.45f, 0.2f), new Vector3(1.6f, 2.9f, 0.1f), RealmKit.Shadow, false);
            RealmKit.Box("JambL", door, new Vector3(-0.92f, 1.45f, -0.3f), new Vector3(0.24f, 2.9f, 0.2f), RealmKit.Limestone, false);
            RealmKit.Box("JambR", door, new Vector3(0.92f, 1.45f, -0.3f), new Vector3(0.24f, 2.9f, 0.2f), RealmKit.Limestone, false);
            GameObject sealed_ = RFigure(door, "Sealed", "b_door_sealed", new Vector3(0f, 0f, -0.05f), 2.9f);
            Material beyondMat = RFx("b_beyond", "fx_glow", new Color(1f, 0.8f, 0.55f, 1f), 1.3f, 0.15f, 0.7f, 0f);
            GameObject beyond = RGlow(door, "Beyond", beyondMat, new Vector3(0f, 1.4f, 0.05f), new Vector2(2.2f, 3.2f));
            Light doorLight = InnerGlow(door, new Vector3(0f, 1.4f, -1.0f), 0f, 7f);
            RealmObjectView dv = View(ctx, door, "door", new Vector3(0f, 3.2f, -0.4f));
            var ds = Pose(dv, "sealed", 0.6f); ds.show.Add(sealed_); ds.hide.Add(beyond); Lamp(ds, doorLight, 0f);
            var dop = Pose(dv, "open", 2.0f); dop.hide.Add(sealed_); dop.show.Add(beyond); Lamp(dop, doorLight, 2.4f);
            Zone(ctx, "door", new Vector3(8.0f, 1f, 0.2f), new Vector3(2.6f, 4f, 5f), -1, 2);

            RealmKit.Brazier("WestLamp", w, new Vector3(-8.6f, 0f, 2.3f), 8f, 1.4f);
            RealmKit.Motes(w, new Vector3(0f, 2f, 1f), new Vector3(18f, 3f, 4f), new Color(1f, 0.85f, 0.6f, 0.5f), 70);

            Cast(ctx, new Vector3(-8.4f, 0f, -0.4f), 90f, new Vector3(-9.2f, 0f, -0.8f));

            RealmKit.Blocker(w, new Vector3(0f, 3f, -2.5f), new Vector3(20f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(0f, 3f, 2.8f), new Vector3(20f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(-9.6f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));
            RealmKit.Blocker(w, new Vector3(9.6f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));
            // The bier blocks the lane's back half. Its bed collider stops walking through it.

            Zone(ctx, "wall", new Vector3(-6.8f, 1f, 0.2f), new Vector3(2.6f, 4f, 5f), -1, 2);

            Shot(ctx, "cam_hall", new Vector3(0f, 4.4f, -16f), new Vector3(0f, 1.8f, 1f), 40f, new Vector3(0f, 3f, 0f), new Vector3(20f, 10f, 8f), 0.04f, 0);

            SnapAllFirst(ctx);
            ApplyRosetauPaint(ctx, scene, new[] { new Rect4(new Vector3(0f, 0.012f, 0.2f), new Vector2(19f, 5.4f)) });
            Finish(ctx, scene);
        }

        // ------------------------------------------------------------------ Room C: The Mother's Room

        private static void BuildMotherC()
        {
            string scene = RosetauScripts.MotherC;
            Ctx ctx = Begin(scene, new Color(0.06f, 0.05f, 0.05f), 0.008f, 0.3f);
            Transform w = ctx.world;
            Ground(w, 120f, RealmKit.Mud);
            Material brick = RealmKit.Lit("MudBrick", new Color(0.46f, 0.34f, 0.24f), 0.1f, 0f,
                RealmKit.StoneTexture("mudbrick", new Color(0.34f, 0.24f, 0.16f), new Color(0.56f, 0.42f, 0.30f), 1.5f, 71), 1.5f);
            Material plaster = RealmKit.Lit("Plaster", new Color(0.86f, 0.80f, 0.66f), 0.1f);
            Material thread = RealmKit.Lit("LoomThread", new Color(0.86f, 0.82f, 0.70f), 0.05f);

            // The house in Abdju, cut away toward the camera.
            Transform house = RealmKit.Group("House", w);
            RealmKit.Box("Floor", house, new Vector3(0f, 0.03f, 0.2f), new Vector3(16f, 0.06f, 5.4f), RealmKit.Limestone, false);
            RealmKit.Box("BackWall", house, new Vector3(0f, 1.8f, 2.9f), new Vector3(16.5f, 3.6f, 0.5f), brick, false);
            RealmKit.Box("Plaster", house, new Vector3(0f, 1.5f, 2.64f), new Vector3(14f, 2.6f, 0.02f), plaster, false);
            RealmKit.Box("WestWall", house, new Vector3(-8.25f, 1.8f, 0.2f), new Vector3(0.5f, 3.6f, 5.8f), brick, false);
            RealmKit.Box("EastWall", house, new Vector3(8.25f, 1.8f, 0.2f), new Vector3(0.5f, 3.6f, 5.8f), brick, false);
            RealmKit.Box("FrontStub", house, new Vector3(0f, 0.25f, -2.75f), new Vector3(16.5f, 0.5f, 0.4f), brick, false);
            RealmKit.Box("RoofBeams", house, new Vector3(0f, 3.7f, 2.0f), new Vector3(16.5f, 0.18f, 2.2f), RealmKit.Wood, false);
            for (int b = 0; b < 9; b++)
                RealmKit.Box("Joist" + b, house, new Vector3(-7.2f + b * 1.8f, 3.65f, 0.6f), new Vector3(0.14f, 0.14f, 4.6f), RealmKit.Wood, false);
            RealmKit.Box("Window", house, new Vector3(3.2f, 2.8f, 2.62f), new Vector3(0.9f, 0.5f, 0.04f), RealmKit.Shadow, false);
            RealmKit.Box("Mat", house, new Vector3(-1.6f, 0.065f, 1.2f), new Vector3(2.4f, 0.01f, 1.4f), RealmKit.ReedDry, false);

            // The shelf by the door: the faience Heru and the clay falcon he brought back.
            Transform shelf = RealmKit.Group("Shelf", w, new Vector3(-5.6f, 0f, 2.5f));
            RealmKit.Box("Board", shelf, new Vector3(0f, 1.5f, 0f), new Vector3(1.6f, 0.08f, 0.4f), RealmKit.Wood, false);
            RealmKit.Box("Board2", shelf, new Vector3(0f, 0.9f, 0f), new Vector3(1.6f, 0.08f, 0.4f), RealmKit.Wood, false);
            RealmKit.Shape(PrimitiveType.Capsule, "Faience", shelf, new Vector3(-0.3f, 1.72f, 0f), new Vector3(0.12f, 0.18f, 0.12f), RealmKit.Lapis);
            RealmKit.Shape(PrimitiveType.Sphere, "ClayFalcon", shelf, new Vector3(0.3f, 1.66f, 0f), new Vector3(0.18f, 0.2f, 0.12f), RealmKit.Ochre);
            RealmKit.Shape(PrimitiveType.Sphere, "Loaf", shelf, new Vector3(0f, 1.0f, 0f), new Vector3(0.35f, 0.16f, 0.3f), RealmKit.Sandstone);

            // The upright loom against the back wall. The shuttle runs across the warp while the loom works.
            Transform loom = RealmKit.Group("Loom", w, new Vector3(-1.6f, 0f, 2.3f));
            RealmKit.Box("PostL", loom, new Vector3(-1.1f, 1.3f, 0f), new Vector3(0.12f, 2.6f, 0.12f), RealmKit.Wood, false);
            RealmKit.Box("PostR", loom, new Vector3(1.1f, 1.3f, 0f), new Vector3(0.12f, 2.6f, 0.12f), RealmKit.Wood, false);
            RealmKit.Box("TopBeam", loom, new Vector3(0f, 2.5f, 0f), new Vector3(2.5f, 0.12f, 0.12f), RealmKit.Wood, false);
            RealmKit.Box("LowBeam", loom, new Vector3(0f, 0.35f, 0f), new Vector3(2.5f, 0.12f, 0.12f), RealmKit.Wood, false);
            RealmKit.Box("Warp", loom, new Vector3(0f, 1.42f, 0.02f), new Vector3(2.0f, 2.0f, 0.02f), thread, false);
            RealmKit.Box("Cloth", loom, new Vector3(0f, 0.65f, 0.0f), new Vector3(2.0f, 0.5f, 0.04f), RealmKit.Lit("Linen", new Color(0.88f, 0.84f, 0.74f), 0.05f), false);
            Transform shuttle = NewCard("Shuttle", loom, new Vector3(-0.8f, 1.05f, -0.15f), Quaternion.identity);
            Material shuttleMat = RCard("c_shuttle", out float sa);
            if (shuttleMat != null) Quad(shuttle, shuttleMat, Vector3.zero, new Vector2(0.5f * sa, 0.5f));
            else RealmKit.Box("StandIn_c_shuttle", shuttle, Vector3.zero, new Vector3(0.45f, 0.08f, 0.08f), RealmKit.Wood, false);
            var loomDrive = new GameObject("WorkDriver"); loomDrive.transform.SetParent(loom, false);
            var lwl = loomDrive.AddComponent<WorkerLoop>(); lwl.worker = shuttle; lwl.from = new Vector3(-0.8f, 1.05f, -0.15f); lwl.to = new Vector3(0.8f, 1.05f, -0.15f); lwl.period = 2.2f;
            RealmObjectView lmv = View(ctx, loom, "loom", new Vector3(0f, 2.8f, -0.3f));
            var lms = Pose(lmv, "still", 0.6f); lms.hide.Add(loomDrive);
            var lmw = Pose(lmv, "working", 0.8f); lmw.show.Add(loomDrive);
            Zone(ctx, "loom", new Vector3(-1.6f, 1f, 0.2f), new Vector3(3.0f, 4f, 5f), -1, 2);

            // The mother at her loom: weeping with her face in her hands, listening, and remembered at her work.
            Transform mother = RealmKit.Group("Mother", w, new Vector3(-2.3f, 0f, 1.4f));
            GameObject weep = RFigure(mother, "Weeping", "c_mother_weep", Vector3.zero, 1.35f);
            GameObject listen = RFigure(mother, "Listening", "c_mother_listen", Vector3.zero, 1.35f);
            GameObject remembered = RFigure(mother, "Remembered", "c_mother_remembered", Vector3.zero, 1.35f);
            RealmObjectView mv = View(ctx, mother, "mother", new Vector3(0f, 1.8f, -0.3f));
            var mw = Pose(mv, "weeping", 0.6f); mw.show.Add(weep); mw.hide.Add(listen); mw.hide.Add(remembered);
            var ml = Pose(mv, "listening", 1.4f); ml.hide.Add(weep); ml.show.Add(listen); ml.hide.Add(remembered);
            var mr = Pose(mv, "remembered", 2.4f); mr.hide.Add(weep); mr.hide.Add(listen); mr.show.Add(remembered);

            // Her weeping, answered: a faint warmth around her once someone weeps with her.
            Transform weepRoot = RealmKit.Group("Weeping", w, new Vector3(-2.3f, 1.2f, 1.2f));
            Material warmMat = RFx("c_warm", "fx_glow", new Color(1f, 0.8f, 0.6f, 0.6f), 0.7f, 0.3f, 0.6f, 0f);
            GameObject warm = RGlow(weepRoot, "Warm", warmMat, Vector3.zero, new Vector2(2.2f, 2.2f));
            RealmObjectView wv = View(ctx, weepRoot, "weeping", new Vector3(0f, 0.8f, -0.3f));
            var wu = Pose(wv, "unanswered", 0.6f); wu.hide.Add(warm);
            var wa = Pose(wv, "answered", 1.6f); wa.show.Add(warm);

            // The saddle quern on the floor. The rubbing stone rocks while it grinds.
            Transform quern = RealmKit.Group("Quern", w, new Vector3(3.0f, 0f, 1.6f));
            RealmKit.Box("Saddle", quern, new Vector3(0f, 0.18f, 0f), new Vector3(1.2f, 0.36f, 0.6f), RealmKit.Basalt, false, new Vector3(0f, 0f, -6f));
            RealmKit.Shape(PrimitiveType.Cylinder, "GrainBowl", quern, new Vector3(0.95f, 0.1f, -0.1f), new Vector3(0.5f, 0.1f, 0.5f), RealmKit.Ochre);
            Transform stone = NewCard("RubbingStone", quern, new Vector3(-0.25f, 0.42f, -0.2f), Quaternion.identity);
            Material stoneMat = RCard("c_quern_stone", out float qa);
            if (stoneMat != null) Quad(stone, stoneMat, Vector3.zero, new Vector2(0.45f * qa, 0.45f));
            else RealmKit.Box("StandIn_c_quern_stone", stone, Vector3.zero, new Vector3(0.4f, 0.15f, 0.25f), RealmKit.Basalt, false);
            var quernDrive = new GameObject("WorkDriver"); quernDrive.transform.SetParent(quern, false);
            var qwl = quernDrive.AddComponent<WorkerLoop>(); qwl.worker = stone; qwl.from = new Vector3(-0.3f, 0.42f, -0.2f); qwl.to = new Vector3(0.3f, 0.45f, -0.2f); qwl.period = 1.4f;
            Material flourMat = RFx("c_flour", "fx_mote", new Color(1f, 0.95f, 0.85f, 0.7f), 0.8f, 0.5f, 3f, 0.3f);
            GameObject flour = RGlow(quern, "Flour", flourMat, new Vector3(0.6f, 0.45f, -0.3f), new Vector2(0.5f, 0.5f));
            RealmObjectView qv = View(ctx, quern, "quern", new Vector3(0f, 1.2f, -0.3f));
            var qs = Pose(qv, "still", 0.6f); qs.hide.Add(quernDrive); qs.hide.Add(flour);
            var qg = Pose(qv, "grinding", 0.8f); qg.show.Add(quernDrive); qg.show.Add(flour);
            Zone(ctx, "quern", new Vector3(3.0f, 1f, 0.2f), new Vector3(2.6f, 4f, 5f), -1, 2);

            // Her lamp in a wall niche. It lights itself once the loom and the quern are both going.
            Transform lampT = RealmKit.Group("Lamp", w, new Vector3(5.6f, 0f, 2.5f));
            RealmKit.Box("Niche", lampT, new Vector3(0f, 1.5f, 0.1f), new Vector3(0.7f, 0.7f, 0.1f), RealmKit.Shadow, false);
            RealmKit.Shape(PrimitiveType.Sphere, "LampBowl", lampT, new Vector3(0f, 1.25f, -0.05f), new Vector3(0.3f, 0.1f, 0.2f), RealmKit.Ochre);
            Material lflame = RFx("c_flame", "fx_ember", new Color(1f, 0.82f, 0.5f, 1f), 1.6f, 0.2f, 5f, 0.5f);
            Material lglow = RFx("c_lampglow", "fx_glow", new Color(1f, 0.75f, 0.45f, 0.8f), 1.0f, 0.15f, 1.0f, 0.1f);
            GameObject flame = RGlow(lampT, "Flame", lflame, new Vector3(0f, 1.45f, -0.1f), new Vector2(0.35f, 0.45f));
            GameObject halo = RGlow(lampT, "Halo", lglow, new Vector3(0f, 1.45f, -0.15f), new Vector2(2.6f, 2.6f));
            Light lampLight = InnerGlow(lampT, new Vector3(0f, 1.5f, -0.8f), 0f, 10f);
            RealmObjectView lpv = View(ctx, lampT, "lamp", new Vector3(0f, 2.0f, -0.3f));
            var lpd = Pose(lpv, "dark", 0.6f); lpd.hide.Add(flame); lpd.hide.Add(halo); Lamp(lpd, lampLight, 0f);
            var lpl = Pose(lpv, "lit", 2.4f); lpl.show.Add(flame); lpl.show.Add(halo); Lamp(lpl, lampLight, 2.6f);

            RealmKit.Brazier("DoorEmbers", w, new Vector3(-7.4f, 0f, 2.2f), 6f, 0.9f);
            RealmKit.Motes(w, new Vector3(0f, 1.8f, 1f), new Vector3(15f, 3f, 4f), new Color(1f, 0.85f, 0.6f, 0.45f), 60);

            Cast(ctx, new Vector3(-6.8f, 0f, -0.4f), 90f, new Vector3(-7.6f, 0f, -0.8f));
            // Anpu waits on the step of the first room. He is not a guest here.
            ctx.anpu.gameObject.SetActive(false);

            RealmKit.Blocker(w, new Vector3(0f, 3f, -2.5f), new Vector3(16.5f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(0f, 3f, 2.5f), new Vector3(16.5f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(-8.0f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));
            RealmKit.Blocker(w, new Vector3(8.0f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));

            Zone(ctx, "shelf", new Vector3(-5.6f, 1f, 0.2f), new Vector3(2.0f, 4f, 5f), -1, 2);

            Shot(ctx, "cam_house", new Vector3(0f, 4.2f, -15f), new Vector3(0f, 1.8f, 1f), 40f, new Vector3(0f, 3f, 0f), new Vector3(17f, 10f, 8f), 0.04f, 0);

            SnapAllFirst(ctx);
            ApplyRosetauPaint(ctx, scene, new[] { new Rect4(new Vector3(0f, 0.07f, 0.2f), new Vector2(16f, 5.4f)) });
            Finish(ctx, scene);
        }

        // ------------------------------------------------------------------ Room D: The Two Ways

        /// <summary>
        /// A black road where Ankhu walks, a channel of fire across the room, a blue road on the far bank where three
        /// faceless soldiers keep their posts. Stilled, the fire sinks. Named, each man crosses to the near bank.
        /// </summary>
        private static void BuildTwoWaysD()
        {
            string scene = RosetauScripts.TwoWaysD;
            Ctx ctx = Begin(scene, new Color(0.04f, 0.04f, 0.06f), 0.01f, 0.3f);
            Transform w = ctx.world;
            Ground(w, 120f, RealmKit.Basalt);
            Material black = RealmKit.Lit("BlackRoad", new Color(0.08f, 0.08f, 0.09f), 0.2f);
            Material blue = RealmKit.Lit("BlueRoad", new Color(0.16f, 0.30f, 0.52f), 0.2f);
            Material red = RealmKit.Lit("FireBed", new Color(0.42f, 0.10f, 0.05f), 0.1f);
            Material rock = RealmKit.Lit("TwoWaysRock", new Color(0.22f, 0.21f, 0.22f), 0.1f);

            Transform set = RealmKit.Group("TwoWays", w);
            RealmKit.Box("BlackRoad", set, new Vector3(0f, 0.02f, -0.6f), new Vector3(19f, 0.04f, 2.8f), black, false);
            RealmKit.Box("FireBed", set, new Vector3(0.5f, 0.03f, 1.15f), new Vector3(17f, 0.06f, 0.7f), red, false);
            RealmKit.Box("BlueRoad", set, new Vector3(0.5f, 0.02f, 2.1f), new Vector3(17f, 0.04f, 1.2f), blue, false);
            RealmKit.Box("BackWall", set, new Vector3(0f, 2.8f, 3.1f), new Vector3(20f, 5.6f, 0.5f), rock, false);
            RealmKit.Box("StairRock", set, new Vector3(-8.8f, 2.4f, 1.4f), new Vector3(1.6f, 4.8f, 3.0f), rock, false);
            for (int k = 0; k < 4; k++)
                RealmKit.Box("Step" + k, set, new Vector3(-7.6f, 0.15f + k * 0.3f, 0.2f + k * 0.5f), new Vector3(1.0f, 0.3f, 0.5f), rock, false);

            // The roster by the stair.
            Transform roster = RealmKit.Group("Roster", w, new Vector3(-6.4f, 0f, 2.6f));
            RealmKit.Box("Peg", roster, new Vector3(0f, 1.9f, 0.1f), new Vector3(0.08f, 0.08f, 0.2f), RealmKit.Wood, false);
            RealmKit.Box("Board", roster, new Vector3(0f, 1.5f, 0f), new Vector3(0.7f, 0.9f, 0.05f), RealmKit.Wood, false);
            Zone(ctx, "roster", new Vector3(-6.4f, 1f, 0.2f), new Vector3(2.0f, 4f, 5f), -1, 2);

            // The floor text, painted on the black road.
            RealmKit.Box("FloorText", set, new Vector3(-3.4f, 0.045f, -0.4f), new Vector3(2.2f, 0.01f, 1.0f), RealmKit.Ochre, false);
            Zone(ctx, "floor", new Vector3(-3.4f, 1f, 0.2f), new Vector3(2.2f, 4f, 5f), -1, 2);

            // The channel of fire: roaring tongues the whole length, or a low red crawl.
            Transform fire = RealmKit.Group("Fire", w, new Vector3(0.5f, 0f, 1.15f));
            Material tongue = RFx("d_tongue", "fx_ember", new Color(1f, 0.55f, 0.2f, 1f), 1.8f, 0.35f, 4f, 0.6f);
            Material roar = RFx("d_roar", "fx_glow", new Color(1f, 0.45f, 0.15f, 0.9f), 1.2f, 0.25f, 1.6f, 0.3f);
            Material crawl = RFx("d_crawl", "fx_glow", new Color(0.9f, 0.2f, 0.08f, 0.7f), 0.7f, 0.2f, 0.8f, 0.1f);
            var high = new List<GameObject>();
            for (int k = 0; k < 7; k++)
                high.Add(RGlow(fire, "Tongue" + k, tongue, new Vector3(-7.2f + k * 2.4f, 0.9f, 0f), new Vector2(2.4f, 1.9f)));
            high.Add(RGlow(fire, "Roar", roar, new Vector3(0f, 1.2f, -0.05f), new Vector2(18f, 3.2f)));
            GameObject low = RGlow(fire, "Crawl", crawl, new Vector3(0f, 0.15f, 0f), new Vector2(17f, 0.6f));
            Light fireLight = InnerGlow(fire, new Vector3(0f, 1.2f, -1.2f), 3f, 14f);
            RealmObjectView fv = View(ctx, fire, "fire", new Vector3(-2f, 2.6f, -0.3f));
            var fr = Pose(fv, "roaring", 0.8f); foreach (var g in high) fr.show.Add(g); fr.hide.Add(low); Lamp(fr, fireLight, 3f);
            var fl = Pose(fv, "low", 1.2f); foreach (var g in high) fl.hide.Add(g); fl.show.Add(low); Lamp(fl, fireLight, 0.7f);

            // Three soldiers at their posts. Heard, a cold light finds them. Named, each has a face and stands on the near bank.
            Material heardMat = RFx("d_heard", "fx_glow", new Color(0.6f, 0.75f, 1f, 0.7f), 0.8f, 0.3f, 0.8f, 0f);
            var men = new[] { ("shade_paser", "d_shade_shield", "d_paser_named", 0f, "post1"),
                              ("shade_nakht", "d_shade_empty", "d_nakht_named", 3f, "post2"),
                              ("shade_huy", "d_shade_bow", "d_huy_named", 6f, "post3") };
            foreach (var (id, faceless, named, x, post) in men)
            {
                Transform man = RealmKit.Group(id, w, new Vector3(x, 0f, 2.1f));
                GameObject f = RFigure(man, "Faceless", faceless, Vector3.zero, 1.75f);
                GameObject glow = RGlow(man, "Heard", heardMat, new Vector3(0f, 1.1f, 0.1f), new Vector2(1.8f, 2.4f));
                GameObject n = RFigure(man, "Named", named, new Vector3(0f, 0f, -1.6f), 1.75f);
                RealmObjectView v = View(ctx, man, id, new Vector3(0f, 2.3f, -0.3f));
                var p0 = Pose(v, "faceless", 0.6f); p0.show.Add(f); p0.hide.Add(glow); p0.hide.Add(n);
                var p1 = Pose(v, "heard", 1.0f); p1.show.Add(f); p1.show.Add(glow); p1.hide.Add(n);
                var p2 = Pose(v, "named", 2.0f); p2.hide.Add(f); p2.hide.Add(glow); p2.show.Add(n);
                Zone(ctx, post, new Vector3(x, 1f, 0.2f), new Vector3(2.4f, 4f, 5f), -1, 2);
            }

            RealmKit.Brazier("StairLamp", w, new Vector3(-7.4f, 0f, 2.4f), 7f, 1.2f);
            RealmKit.Motes(w, new Vector3(0f, 2f, 1f), new Vector3(18f, 3f, 4f), new Color(1f, 0.6f, 0.35f, 0.5f), 80);

            Cast(ctx, new Vector3(-7.2f, 0f, -0.6f), 90f, new Vector3(-8.0f, 0f, -1.0f));
            // Anpu waits on the stair. The watch is Ankhu's to relieve.
            ctx.anpu.gameObject.SetActive(false);

            RealmKit.Blocker(w, new Vector3(0f, 3f, -2.3f), new Vector3(20f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(0f, 3f, 0.95f), new Vector3(20f, 6f, 0.3f));
            RealmKit.Blocker(w, new Vector3(-8.6f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));
            RealmKit.Blocker(w, new Vector3(8.6f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));

            Shot(ctx, "cam_room", new Vector3(0f, 4.4f, -16f), new Vector3(0f, 1.6f, 1f), 40f, new Vector3(0f, 3f, 0f), new Vector3(19f, 10f, 8f), 0.04f, 0);

            SnapAllFirst(ctx);
            ApplyRosetauPaint(ctx, scene, new[] { new Rect4(new Vector3(0f, 0.05f, 0.2f), new Vector2(19f, 5.4f)) });
            Finish(ctx, scene);
        }

        // ------------------------------------------------------------------ Room E: The Shield Brought Home

        /// <summary>
        /// The foot of the stair, a mud-brick street in Abdju and the door of the house. The shield leans on the
        /// doorpost and the young widow sits in the dust. Placeholder for the hold: Anpu walks to mark_hold behind the
        /// wrapped shield card. A 3D pass can give him a real carrying pose later.
        /// </summary>
        private static void BuildFinaleE()
        {
            string scene = RosetauScripts.FinaleE;
            Ctx ctx = Begin(scene, new Color(0.07f, 0.06f, 0.07f), 0.008f, 0.3f);
            Transform w = ctx.world;
            Ground(w, 120f, RealmKit.Mud);
            Material brick = RealmKit.Lit("StreetBrick", new Color(0.46f, 0.34f, 0.24f), 0.1f);
            Material rock = RealmKit.Lit("StairRock", new Color(0.22f, 0.21f, 0.22f), 0.1f);

            Transform street = RealmKit.Group("Street", w);
            RealmKit.Box("Dust", street, new Vector3(0f, 0.02f, 0.2f), new Vector3(19f, 0.04f, 5.4f), RealmKit.Sandstone, false);
            RealmKit.Box("HouseFront", street, new Vector3(2.5f, 1.8f, 3.0f), new Vector3(7f, 3.6f, 0.5f), brick, false);
            RealmKit.Box("Neighbour", street, new Vector3(7.6f, 1.6f, 3.0f), new Vector3(3.2f, 3.2f, 0.5f), brick, false);
            RealmKit.Box("NeighbourDoor", street, new Vector3(7.6f, 1.0f, 2.74f), new Vector3(0.9f, 2.0f, 0.04f), RealmKit.Wood, false);
            RealmKit.Box("Doorway", street, new Vector3(2.0f, 1.1f, 2.74f), new Vector3(1.1f, 2.2f, 0.04f), RealmKit.Shadow, false);
            RealmKit.Box("Lintel", street, new Vector3(2.0f, 2.3f, 2.7f), new Vector3(1.5f, 0.2f, 0.12f), RealmKit.Limestone, false);
            // The last steps of the Rosetau stair come down out of the rock at left.
            RealmKit.Box("RockFace", street, new Vector3(-7.4f, 2.6f, 2.4f), new Vector3(4f, 5.2f, 1.6f), rock, false);
            for (int k = 0; k < 4; k++)
                RealmKit.Box("Step" + k, street, new Vector3(-6.6f, 0.12f + k * 0.24f, 0.2f + k * 0.45f), new Vector3(1.8f, 0.24f, 0.5f), rock, false);

            // The stela by the door, smooth, then cut with the couple at their offering table.
            Transform stela = RealmKit.Group("Stela", w, new Vector3(3.5f, 0f, 2.3f));
            GameObject blank = RFigure(stela, "Blank", "e_stela_blank", Vector3.zero, 1.1f);
            GameObject cut = RFigure(stela, "Cut", "e_stela_cut", Vector3.zero, 1.1f);
            GameObject loaf = RFigure(stela, "Loaf", "e_bread", new Vector3(0f, 0f, -0.3f), 0.3f);

            // The shield on the doorpost, then wrapped and standing the height of a man.
            Transform shield = RealmKit.Group("Shield", w, new Vector3(0.8f, 0f, 2.2f));
            GameObject lean = RFigure(shield, "Leaning", "e_shield_lean", Vector3.zero, 1.1f);
            GameObject wrapped = RFigure(shield, "Wrapped", "e_shield_wrapped", new Vector3(0.8f, 0f, -1.0f), 1.85f);
            RealmObjectView sv = View(ctx, shield, "shield", new Vector3(0f, 1.6f, -0.3f));
            var sl = Pose(sv, "leaning", 0.6f); sl.show.Add(lean); sl.hide.Add(wrapped);
            var sw = Pose(sv, "wrapped", 1.6f); sw.hide.Add(lean); sw.show.Add(wrapped);

            // Nakht's shade by the door. Housed, he is gone into the wrapped form. Opened, a breath of light at its face.
            Transform father = RealmKit.Group("Father", w, new Vector3(2.9f, 0f, 1.4f));
            GameObject shade = RFigure(father, "Shade", "d_nakht_named", Vector3.zero, 1.75f);
            Material breathMat = RFx("e_breath", "fx_glow", new Color(1f, 0.85f, 0.55f, 0.8f), 1.0f, 0.4f, 0.5f, 0f);
            GameObject breath = RGlow(w, "Breath", breathMat, new Vector3(1.6f, 1.6f, 1.05f), new Vector2(1.0f, 1.0f));
            RealmObjectView fav = View(ctx, father, "father", new Vector3(0f, 2.3f, -0.3f));
            var fw = Pose(fav, "waiting", 0.6f); fw.show.Add(shade); fw.hide.Add(breath);
            var fh = Pose(fav, "housed", 1.6f); fh.hide.Add(shade); fh.hide.Add(breath);
            var fo = Pose(fav, "opened", 2.0f); fo.hide.Add(shade); fo.show.Add(breath);

            // The mouth: the rite's light on the wrapped face.
            Transform mouth = RealmKit.Group("Mouth", w, new Vector3(1.6f, 1.6f, 1.0f));
            Material mouthMat = RFx("e_mouth", "fx_ember", new Color(1f, 0.9f, 0.6f, 1f), 1.4f, 0.2f, 2f, 0.2f);
            GameObject touch = RGlow(mouth, "Touch", mouthMat, Vector3.zero, new Vector2(0.4f, 0.4f));
            RealmObjectView mov = View(ctx, mouth, "mouth", new Vector3(0f, 0.6f, -0.3f));
            var mc = Pose(mov, "closed", 0.6f); mc.hide.Add(touch);
            var mo = Pose(mov, "open", 1.4f); mo.show.Add(touch);

            // The young widow in the dust, keening, then answered. When the bread is given she stands, and Nakht is beside her.
            Transform widow = RealmKit.Group("Widow", w, new Vector3(6.2f, 0f, 1.2f));
            GameObject keen = RFigure(widow, "Keening", "e_widow_keen", Vector3.zero, 1.15f);
            GameObject listen = RFigure(widow, "Listening", "e_widow_listen", Vector3.zero, 1.15f);
            GameObject stand = RFigure(widow, "Standing", "e_widow_stand", new Vector3(-0.4f, 0f, 0f), 1.62f);
            GameObject home = RFigure(widow, "Nakht", "d_nakht_named", new Vector3(-1.2f, 0f, 0f), 1.75f);
            RealmObjectView wv = View(ctx, widow, "mother", new Vector3(0f, 1.8f, -0.3f));
            var wk = Pose(wv, "keening", 0.6f); wk.show.Add(keen); wk.hide.Add(listen);
            var wa = Pose(wv, "answered", 1.4f); wa.hide.Add(keen); wa.show.Add(listen);

            Transform lament = RealmKit.Group("Lament", w, new Vector3(6.2f, 1.0f, 1.0f));
            Material warmMat = RFx("e_warm", "fx_glow", new Color(1f, 0.8f, 0.6f, 0.6f), 0.7f, 0.3f, 0.6f, 0f);
            GameObject warm = RGlow(lament, "Warm", warmMat, Vector3.zero, new Vector2(2.4f, 2.4f));
            RealmObjectView lv = View(ctx, lament, "lament", new Vector3(0f, 1.0f, -0.3f));
            var lu = Pose(lv, "unanswered", 0.6f); lu.hide.Add(warm);
            var la = Pose(lv, "answered", 1.6f); la.show.Add(warm);

            // The bread: carried, then on the offering place, and the house is whole.
            Transform bread = RealmKit.Group("Bread", w, new Vector3(3.5f, 0f, 2.0f));
            RealmObjectView bv = View(ctx, bread, "bread", new Vector3(0f, 1.0f, -0.3f));
            var bc = Pose(bv, "carried", 0.6f); bc.hide.Add(loaf); bc.show.Add(blank); bc.hide.Add(cut); bc.hide.Add(stand); bc.hide.Add(home);
            var bg = Pose(bv, "given", 2.4f); bg.show.Add(loaf); bg.hide.Add(blank); bg.show.Add(cut);
            bg.show.Add(stand); bg.show.Add(home); bg.hide.Add(keen); bg.hide.Add(listen); bg.hide.Add(wrapped); bg.hide.Add(breath); bg.hide.Add(touch);

            RealmKit.Brazier("DoorLamp", w, new Vector3(0.2f, 0f, 2.5f), 6f, 1.0f);
            RealmKit.Motes(w, new Vector3(0f, 2f, 1f), new Vector3(18f, 3f, 4f), new Color(1f, 0.88f, 0.65f, 0.45f), 60);

            Cast(ctx, new Vector3(-4.8f, 0f, -0.6f), 90f, new Vector3(-6.6f, 0.96f, 1.6f));
            // Placeholder for the hold. Anpu walks here and stands behind the wrapped form.
            AddMark(ctx, "mark_hold", new Vector3(1.6f, 0f, 1.7f));

            RealmKit.Blocker(w, new Vector3(0f, 3f, -2.5f), new Vector3(19f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(0f, 3f, 2.7f), new Vector3(19f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(-5.6f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));
            RealmKit.Blocker(w, new Vector3(9.0f, 3f, 0f), new Vector3(0.4f, 6f, 5.4f));

            Zone(ctx, "door", new Vector3(2.0f, 1f, 0.2f), new Vector3(4.2f, 4f, 5f), -1, 2);
            Zone(ctx, "street", new Vector3(6.4f, 1f, 0.2f), new Vector3(4.2f, 4f, 5f), -1, 2);

            Shot(ctx, "cam_street", new Vector3(0.5f, 4.2f, -15f), new Vector3(0.5f, 1.6f, 1f), 40f, new Vector3(0f, 3f, 0f), new Vector3(19f, 10f, 8f), 0.04f, 0);

            SnapAllFirst(ctx);
            ApplyRosetauPaint(ctx, scene, new[] { new Rect4(new Vector3(0f, 0.05f, 0.2f), new Vector2(19f, 5.4f)) });
            Finish(ctx, scene);
        }

        // ------------------------------------------------------------------ plates, captures, audit

        /// <summary>Optional "-rosetauScenes A,B" limits plate capture to those scenes.</summary>
        private static string[] RosetauFilter()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-rosetauScenes");
            return i >= 0 && i + 1 < args.Length ? args[i + 1].Split(',') : null;
        }

        /// <summary>Renders each camera of each Rosetau scene with only the set visible, at the plate size, for painting over.</summary>
        public static void CaptureRosetauPlatesCli() => RunCli(() =>
        {
            Directory.CreateDirectory(RosetauPlateDir);
            string[] only = RosetauFilter();
            foreach (string sceneName in RosetauScripts.Scenes)
            {
                string path = RealmKit.SceneDir + "/" + sceneName + ".unity";
                if (!File.Exists(path)) continue;
                if (only != null && Array.IndexOf(only, sceneName) < 0) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
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
                    cam.fieldOfView = PlateFov(fov);
                    RenderTo(cam, Path.Combine(RosetauPlateDir, sceneName + "_" + cam.name + ".png"), PlateW, PlateH);
                    cam.fieldOfView = fov;
                }
                Debug.Log("[Rosetau] plates for " + sceneName);
            }
        });

        public static void CaptureRosetauCli() => RunCli(() => RealmCapture.CaptureScripts(RosetauScripts.All()));

        /// <summary>Runs PaintedRoomAudit on each Rosetau scene and writes art_gen/realms/audit/painted_audit_rosetau.md.</summary>
        public static void AuditRosetauCli() => RunCli(() =>
        {
            var sb = new StringBuilder("# Painted room audit, Rosetau\n\n");
            int total = 0;
            foreach (string sceneName in RosetauScripts.Scenes)
            {
                string path = RealmKit.SceneDir + "/" + sceneName + ".unity";
                if (!File.Exists(path)) { sb.Append("## " + sceneName + "\n\nmissing scene\n\n"); total++; continue; }
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var bad = PaintedRoomAudit.AuditOpenScene(sceneName).Where(r => r.verdict == PaintedRoomAudit.Verdict.Violation).ToList();
                total += bad.Count;
                sb.Append("## " + sceneName + ": " + bad.Count + " violations\n\n");
                foreach (var r in bad) sb.Append("- " + r.path + " (" + r.material + ", " + r.shader + "): " + r.why + (r.poseOnly ? " [pose only]" : "") + "\n");
                sb.Append("\n");
            }
            sb.Insert(0, "Result: " + (total == 0 ? "PASS" : "FAIL, " + total + " violations") + "\n\n");
            Directory.CreateDirectory("art_gen/realms/audit");
            File.WriteAllText("art_gen/realms/audit/painted_audit_rosetau.md", sb.ToString());
            Debug.Log("[Rosetau] painted audit: " + total + " violations");
        });
    }
}
