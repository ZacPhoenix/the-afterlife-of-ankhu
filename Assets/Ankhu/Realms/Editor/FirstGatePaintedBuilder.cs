using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ankhu.Painted;
using Ankhu.Realms.Core;
using Ankhu.Realms.Core.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// The First Gate as a painted room (docs/PAINTED_ROOM_SPEC.md), Zac's picks 2026-11-25: look B, the gate set into
    /// cavern rock with a teal channel across the court, layout C, one deep court painting with a slow push in, the three
    /// who mind the gate as painted cards. Sad of Voice is a limestone statue, the Eavesdropper a basalt listener standing
    /// straight with a hand cupped behind a normal ear, Face-downward a man lying prostrate with a crocodile-hide back. Each figure cross
    /// fades from its dormant card to its awake card when named. The greybox Realm_01_Arit1 is left as it is.
    ///   Unity -batchmode -projectPath . -executeMethod Ankhu.Realms.EditorTools.RealmSceneBuilder.BuildFirstGatePaintedCli -logFile fg.log
    ///   ... RealmSceneBuilder.CaptureFirstGatePlatesCli   set-only plate, art_gen/first_gate/plates
    ///   ... RealmSceneBuilder.CheckFirstGateCli           audit and start/end captures, art_gen/realms/audit/first_gate_check.md
    /// The painting lives at Art/FirstGate/Realm_01_Arit1_Painted_cam_court.png and the cards at Art/FirstGate/Sprites.
    /// </summary>
    public static partial class RealmSceneBuilder
    {
        public const string FirstGateScene = "Realm_01_Arit1_Painted";
        public const string FirstGateArtDir = "Assets/Ankhu/Realms/Art/FirstGate";
        public static string FirstGateArtGen => Path.GetFullPath(Path.Combine(Application.dataPath, "../art_gen/first_gate"));

        private const float ChannelZ = 6.2f;

        [MenuItem("Ankhu/Realms/Build First Gate (painted)")]
        public static void BuildFirstGatePaintedMenu() => BuildFirstGatePainted();

        public static void BuildFirstGatePaintedCli() => RunCli(BuildFirstGatePainted);

        public static void BuildFirstGatePainted()
        {
            RealmKit.EnsureDirs();
            EnsureFolder(FirstGateArtDir + "/Sprites");
            EnsureFolder(FirstGateArtDir + "/Materials");
            BuildFirstGate();
            string path = RealmKit.SceneDir + "/" + FirstGateScene + ".unity";
            // The painted twin takes the greybox gate's place in the chain (PlayOrder.PaintedOf), title stays at 0.
            if (!EditorBuildSettings.scenes.Any(s => s.path == path))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(path, true) }).ToArray();
            PlayOrderCheck.ApplyBuildOrder();
            Debug.Log("[FirstGate] built " + path);
        }

        // ------------------------------------------------------------------ painted pieces

        /// <summary>A sprite texture from Art/FirstGate/Sprites with the card import (alpha, clamp, no NPOT scaling).</summary>
        private static Texture2D FgTex(string sprite, out float aspect)
        {
            aspect = 1f;
            string path = FirstGateArtDir + "/Sprites/" + sprite + ".png";
            if (!File.Exists(path)) { Debug.LogWarning("[FirstGate] missing card " + sprite); return null; }
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
            return tex;
        }

        /// <summary>An Ankhu/PaintedFlipbook material saved under Art/FirstGate/Materials, so PaintedDrift can fade it.</summary>
        private static Material FgFlip(string matName, Texture2D tex, Color tint, float gain, float pulse, float pulseSpeed, float flicker, bool additive, float sway = 0f)
        {
            if (tex == null) return null;
            Shader sh = Shader.Find("Ankhu/PaintedFlipbook");
            if (sh == null) { Debug.LogError("[FirstGate] Ankhu/PaintedFlipbook did not compile"); return null; }
            string path = FirstGateArtDir + "/Materials/" + matName + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Cols", 1f); m.SetFloat("_Rows", 1f);
            m.SetFloat("_FrameStart", 0f); m.SetFloat("_FrameCount", 1f); m.SetFloat("_Fps", 0f); m.SetFloat("_Loop", 1f);
            m.SetColor("_Tint", tint);
            m.SetFloat("_Gain", gain);
            m.SetFloat("_PulseAmp", pulse); m.SetFloat("_PulseSpeed", pulseSpeed); m.SetFloat("_Flicker", flicker);
            m.SetFloat("_Sway", sway); m.SetFloat("_SwaySpeed", 1.1f);
            m.SetFloat("_AlphaCut", 0.004f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + (additive ? 10 : 0);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material FgCardMat(string sprite, out float aspect)
        {
            Texture2D tex = FgTex(sprite, out aspect);
            return FgFlip("flip_fg_" + sprite, tex, new Color(0.93f, 0.9f, 0.86f, 1f), 1f, 0f, 1f, 0f, false);
        }

        private static Material FgFx(string matName, string fx, Color tint, float gain, float pulse, float pulseSpeed, float flicker, float sway = 0f)
            => FgFlip("fx_fg_" + matName, FxTex(fx), tint, gain, pulse, pulseSpeed, flicker, true, sway);

        /// <summary>A quad on its own child so a PaintedDrift on it fades only that quad.</summary>
        private static Transform FgQuad(Transform parent, string name, Material m, Vector3 center, Vector2 size)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = center;
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return q.transform;
        }

        private static void FgFade(Transform quad, float from, float to, float seconds, float delay = 0f)
        {
            var d = quad.gameObject.AddComponent<PaintedDrift>();
            d.from = d.to = quad.localPosition;
            d.seconds = seconds; d.delay = delay;
            d.fade = true; d.fadeFrom = from; d.fadeTo = to;
        }

        /// <summary>
        /// A named figure as three camera-facing cards: Dormant, DormantOut (the same picture, fading away) and Awake
        /// (fading in, with an additive glow). height is the card height in metres, or width when wide is set.
        /// The answered pose is short, so the dormant card drops at once while its fading copy and the awake card cross.
        /// </summary>
        private static void FgFigure(Ctx ctx, Transform w, string id, string sprite, Vector3 pos, float size, bool wide, Vector3 glowAt, Vector2 glowSize, Vector3 glyphAnchor, GameObject[] alwaysShown)
        {
            Transform root = RealmKit.Group(char.ToUpper(id[0]) + id.Substring(1), w, pos);
            Material dm = FgCardMat(sprite + "_dormant", out float da);
            Material am = FgCardMat(sprite + "_awake", out float aa);
            float dh = wide ? size / da : size, ah = wide ? size / aa : size;

            Transform dormant = NewCard("Dormant", root, Vector3.zero, Quaternion.identity);
            Transform dormantOut = NewCard("DormantOut", root, Vector3.zero, Quaternion.identity);
            Transform awake = NewCard("Awake", root, Vector3.zero, Quaternion.identity);
            if (dm != null)
            {
                FgQuad(dormant, "CardQuad", dm, new Vector3(0f, dh * 0.5f, 0f), new Vector2(dh * da, dh));
                FgFade(FgQuad(dormantOut, "CardQuad", dm, new Vector3(0f, dh * 0.5f, 0f), new Vector2(dh * da, dh)), 1f, 0f, 1.8f);
            }
            else RealmKit.Box("StandIn_" + sprite, dormant, new Vector3(0f, size * 0.5f, 0f), new Vector3(size * 0.4f, size, 0.2f), RealmKit.Limestone, false);
            if (am != null)
            {
                Material glow = FgFx(id + "_wake", "fx_glow", new Color(1f, 0.82f, 0.5f, 0.55f), 1.2f, 0.25f, 1.6f, 0.08f);
                if (glow != null) FgFade(FgQuad(awake, "WakeGlow", glow, glowAt, glowSize), 0f, 1f, 2.2f, 0.3f);
                FgFade(FgQuad(awake, "CardQuad", am, new Vector3(0f, ah * 0.5f, -0.01f), new Vector2(ah * aa, ah)), 0f, 1f, 1.8f);
            }
            else RealmKit.Box("StandIn_" + sprite + "_awake", awake, new Vector3(0f, size * 0.5f, 0f), new Vector3(size * 0.4f, size, 0.2f), RealmKit.Basalt, false);

            RealmObjectView v = View(ctx, root, id, glyphAnchor);
            foreach (string st in new[] { "unknown", "known" })
            {
                var p = Pose(v, st, 0.6f);
                p.show.Add(dormant.gameObject); p.hide.Add(dormantOut.gameObject); p.hide.Add(awake.gameObject);
                foreach (GameObject g in alwaysShown) p.show.Add(g);
            }
            var pa = Pose(v, "answered", 0.1f);
            pa.hide.Add(dormant.gameObject); pa.show.Add(dormantOut.gameObject); pa.show.Add(awake.gameObject);
            foreach (GameObject g in alwaysShown) pa.show.Add(g);
        }

        /// <summary>
        /// Zac 2026-11-26: sound rings beside a figure's head while it speaks, so the player sees which of the three is
        /// talking. Three painted fx_soundring quads on one camera-facing card, run by TalkMark.
        /// </summary>
        private static void FgTalkMark(Transform w, Speaker who, Vector3 at, float size)
        {
            Material ring = FgFx("talk_ring", "fx_soundring", new Color(1f, 0.86f, 0.55f, 0.85f), 1.4f, 0f, 1f, 0.05f);
            if (ring == null) return;
            Transform c = NewCard("TalkMark_" + who, w, at, Quaternion.identity);
            for (int i = 0; i < 3; i++) FgQuad(c, "Ring" + i, ring, new Vector3(0f, 0f, -0.01f * i), new Vector2(size, size));
            var m = c.gameObject.AddComponent<TalkMark>();
            m.speaker = who;
            m.ringSize = size;
        }

        /// <summary>A painted brazier flame: the 4x4 fin_flame flipbook over the bowl, with a warm halo behind it.</summary>
        private static void FgFlame(Transform w, Vector3 bowl, float h, Material flame, Material halo, float fa)
        {
            Transform c = NewCard("Flame", w, bowl, Quaternion.identity);
            if (halo != null) Quad(c, halo, new Vector3(0f, h * 0.35f, 0.02f), new Vector2(h * 2.6f, h * 2.6f));
            if (flame != null) Quad(c, flame, new Vector3(0f, h * 0.42f, 0f), new Vector2(h * fa, h));
        }

        // ------------------------------------------------------------------ the room

        private static void BuildFirstGate()
        {
            Ctx ctx = Begin(FirstGateScene, new Color(0.04f, 0.04f, 0.07f), 0.01f, 0.25f);
            ctx.director.sceneName = WernesScripts.Gate; // the script and the play order key off the greybox name
            Transform w = ctx.world;
            Ground(w, 90f, RealmKit.Limestone);
            Material rock = RealmKit.Lit("FirstGateRock", new Color(0.26f, 0.22f, 0.27f), 0.2f, 0f,
                RealmKit.StoneTexture("firstgaterock", new Color(0.12f, 0.10f, 0.14f), new Color(0.34f, 0.29f, 0.34f), 0.8f, 131), 2f);

            // The set, which only the plate and the colliders ever see.
            Transform court = RealmKit.Group("Court", w);
            for (int x = -4; x <= 4; x++)
                for (int z = -6; z <= 3; z++)
                    RealmKit.Box("Pave", court, new Vector3(x * 1.6f, 0.03f, z * 1.6f), new Vector3(1.56f, 0.06f, 1.56f), ((x + z) & 1) == 0 ? RealmKit.Limestone : RealmKit.Sandstone, false);
            RealmKit.Wall("WallL", w, new Vector3(-8.2f, 0f, -1.5f), new Vector3(0.8f, 2.2f, 16f), RealmKit.Sandstone);
            RealmKit.Wall("WallR", w, new Vector3(8.2f, 0f, -1.5f), new Vector3(0.8f, 2.2f, 16f), RealmKit.Sandstone);
            Transform cavern = RealmKit.Group("Cavern", w);
            RealmKit.Box("RockL", cavern, new Vector3(-15f, 8f, 4f), new Vector3(10f, 16f, 30f), rock, false);
            RealmKit.Box("RockR", cavern, new Vector3(15f, 8f, 4f), new Vector3(10f, 16f, 30f), rock, false);
            RealmKit.Box("RockBack", cavern, new Vector3(0f, 9f, 13f), new Vector3(40f, 18f, 4f), rock, false);
            RealmKit.Box("RockRoof", cavern, new Vector3(0f, 17f, 2f), new Vector3(40f, 2f, 30f), rock, false);

            // The channel across the court, bridged at the middle.
            Transform channel = RealmKit.Group("Channel", w);
            RealmKit.Box("ChannelBed", channel, new Vector3(0f, -0.02f, ChannelZ), new Vector3(15.6f, 0.04f, 0.8f), RealmKit.Water, false);
            RealmKit.Box("LipN", channel, new Vector3(0f, 0.06f, ChannelZ + 0.48f), new Vector3(15.6f, 0.12f, 0.16f), RealmKit.Basalt, false);
            RealmKit.Box("LipS", channel, new Vector3(0f, 0.06f, ChannelZ - 0.48f), new Vector3(15.6f, 0.12f, 0.16f), RealmKit.Basalt, false);
            RealmKit.Box("Bridge", channel, new Vector3(0f, 0.08f, ChannelZ), new Vector3(3f, 0.16f, 1.3f), RealmKit.Limestone, false);

            // The pylon cut into the rock.
            Transform pylon = RealmKit.Group("Pylon", w, new Vector3(0f, 0f, 9f));
            RealmKit.PylonTower("Tower_L", pylon, new Vector3(-5.2f, 0f, 0f), 6.4f, 3f, 11f, RealmKit.Sandstone);
            RealmKit.PylonTower("Tower_R", pylon, new Vector3(5.2f, 0f, 0f), 6.4f, 3f, 11f, RealmKit.Sandstone);
            RealmKit.Box("GateLintel", pylon, new Vector3(0f, 7.6f, 0f), new Vector3(4.2f, 1.6f, 3.2f), RealmKit.Sandstone);
            RealmKit.Box("WingedDisk", pylon, new Vector3(0f, 7.6f, -1.62f), new Vector3(3.6f, 0.3f, 0.06f), RealmKit.Gold, false);
            RealmKit.Box("Passage", pylon, new Vector3(0f, 3.4f, 1.4f), new Vector3(4f, 6.8f, 0.1f), RealmKit.Shadow, false);
            for (int i = 0; i < 5; i++)
                RealmKit.Box("Avenue" + i, w, new Vector3(0f, 0.03f, 11f + i * 2f), new Vector3(3.6f, 0.06f, 1.9f), RealmKit.Limestone, false);

            // Plinth and bier stay in the painting. The figures on them are cards.
            RealmKit.Box("GuardianPlinth", w, new Vector3(4.2f, 0.3f, 4.8f), new Vector3(1.8f, 0.6f, 1.8f), RealmKit.Sandstone);
            RealmKit.Box("KeeperBier", w, new Vector3(4.8f, 0.2f, -2.4f), new Vector3(3.4f, 0.4f, 1.2f), RealmKit.Sandstone);

            // Braziers: two flank the door, two light the court. Stands and bowls are painted, the flames are live cards.
            var bowls = new List<Vector3>();
            foreach (Vector3 b in new[] { new Vector3(-3.0f, 0f, 7.0f), new Vector3(3.0f, 0f, 7.0f), new Vector3(-6.4f, 0f, -3.6f), new Vector3(6.6f, 0f, 1.0f) })
            {
                RealmKit.Brazier("Brazier", w, b, 9f, b.z > 5f ? 2.6f : 2.2f);
            }
            // Zac 2026-11-26: the flames floated off the bowls. The painted braziers drifted from the set ones, so each
            // flame sits where its painted bowl is (measured in the court render, unprojected with UnprojectFirstGateCli).
            bowls.AddRange(new[] { new Vector3(-2.66f, 1.3f, 5.65f), new Vector3(2.73f, 1.3f, 5.87f), new Vector3(-5.58f, 1.3f, -4.16f), new Vector3(5.47f, 1.3f, -0.07f) });

            // ---------------------------------------------------------------- live pieces

            // The gate leaves: painted bronze on pivots, swinging in, and the light of the way beyond fading up behind them.
            Transform gate = RealmKit.Group("Gate", w, new Vector3(0f, 0f, 9f));
            Transform gl = RealmKit.Group("Leaf_L", gate, new Vector3(-2.0f, 0f, -1.4f));
            Transform gr = RealmKit.Group("Leaf_R", gate, new Vector3(2.0f, 0f, -1.4f));
            Material leafL = FgCardMat("fg_leaf_l", out _), leafR = FgCardMat("fg_leaf_r", out _);
            if (leafL != null) FgQuad(gl, "LeafQuad", leafL, new Vector3(1.133f, 3.4f, 0f), new Vector2(2.267f, 6.8f));
            else RealmKit.Box("StandIn_fg_leaf_l", gl, new Vector3(1f, 3.4f, 0f), new Vector3(2f, 6.8f, 0.1f), RealmKit.Wood, false);
            if (leafR != null) FgQuad(gr, "LeafQuad", leafR, new Vector3(-1.133f, 3.4f, 0.01f), new Vector2(2.267f, 6.8f));
            else RealmKit.Box("StandIn_fg_leaf_r", gr, new Vector3(-1f, 3.4f, 0f), new Vector3(2f, 6.8f, 0.1f), RealmKit.Wood, false);
            var doorBar = new GameObject("DoorBar");
            doorBar.transform.SetParent(gate, false);
            doorBar.transform.localPosition = new Vector3(0f, 2f, -1.2f);
            doorBar.AddComponent<BoxCollider>().size = new Vector3(4.2f, 4f, 0.6f);
            Transform shaftGroup = RealmKit.Group("Shaft", gate);
            Material shaftMat = FgFx("shaft", "fx_glow", new Color(1f, 0.82f, 0.5f, 0.9f), 1.6f, 0.12f, 0.8f, 0.04f);
            if (shaftMat != null)
            {
                FgFade(FgQuad(shaftGroup, "ShaftGlow", shaftMat, new Vector3(0f, 3.0f, 0.4f), new Vector2(6.4f, 6.4f)), 0f, 1f, 3.2f, 0.4f);
                FgFade(FgQuad(shaftGroup, "SillGlow", shaftMat, new Vector3(0f, 0.15f, -1.6f), new Vector2(2.4f, 2.4f)), 0f, 0.7f, 3.2f, 0.9f);
            }
            Light beyondLight = InnerGlow(gate, new Vector3(0f, 3f, 2f), 0f, 16f);
            beyondLight.color = new Color(1f, 0.85f, 0.55f);
            RealmObjectView gateView = View(ctx, gate, "gate", new Vector3(0f, 3.4f, -1.6f));
            var gs = Pose(gateView, "shut", 0.2f);
            Part(gs, gl, gl.localPosition, Vector3.zero); Part(gs, gr, gr.localPosition, Vector3.zero);
            gs.hide.Add(shaftGroup.gameObject); gs.show.Add(doorBar); Lamp(gs, beyondLight, 0f);
            var go = Pose(gateView, "open", 3.5f);
            Part(go, gl, gl.localPosition, new Vector3(0f, -78f, 0f)); Part(go, gr, gr.localPosition, new Vector3(0f, 78f, 0f));
            go.show.Add(shaftGroup.gameObject); go.hide.Add(doorBar); Lamp(go, beyondLight, 4.5f);

            // The guardian's name on its plinth stays live gold, written to read left to right from the court.
            Transform plinthName = RealmKit.GlyphPlate("PlinthName", w, new Vector3(4.2f, 0.32f, 3.86f), Vector3.zero, "F21 G17", 0.3f, false, new Color(1f, 0.8f, 0.45f, 0.9f));

            // The three who mind the gate.
            FgFigure(ctx, w, "announcer", "fg_sad", new Vector3(-4.2f, 0f, 4.8f), 2.7f, false,
                new Vector3(0f, 2.3f, 0.05f), new Vector2(2.2f, 2.2f), new Vector3(0f, 3.4f, 0f), new GameObject[0]);
            FgFigure(ctx, w, "guardian", "fg_ear", new Vector3(4.2f, 0.6f, 4.8f), 2.6f, false,
                new Vector3(0f, 2.2f, 0.05f), new Vector2(2.2f, 2.2f), new Vector3(0f, 3.2f, 0f), new[] { plinthName.gameObject });
            // Zac 2026-11-26: he read as lying off the back of the bier, feet past its end. The painted bier sits left of the
            // greybox one, so the card moves left onto it, forward to mid-slab, and a little smaller so the feet stay on stone.
            FgFigure(ctx, w, "keeper", "fg_croc", new Vector3(4.8f, 0.38f, -2.85f), 3.0f, true,
                new Vector3(-1.2f, 0.4f, 0.05f), new Vector2(1.5f, 1.5f), new Vector3(0f, 1.4f, 0f), new GameObject[0]);

            // Sound rings beside each head, lit only while that figure speaks.
            FgTalkMark(w, Speaker.Announcer, new Vector3(-3.6f, 2.55f, 4.6f), 0.9f);
            FgTalkMark(w, Speaker.Guardian, new Vector3(3.6f, 3.0f, 4.6f), 0.9f);
            FgTalkMark(w, Speaker.Keeper, new Vector3(3.55f, 1.35f, -3.0f), 0.8f);

            // Ambient: flames, embers, motes, and the channel's moving water.
            Texture2D flameTex = SpriteTex("fin_flame", out float fa);
            Material flame = FgFlip("flip_fg_flame", flameTex, new Color(1f, 0.92f, 0.8f, 1f), 1.5f, 0.08f, 3f, 0.1f, true);
            if (flame != null) { flame.SetFloat("_Cols", 4f); flame.SetFloat("_Rows", 4f); flame.SetFloat("_FrameCount", 16f); flame.SetFloat("_Fps", 12f); }
            Material halo = FgFx("flame_halo", "fx_glow", new Color(1f, 0.6f, 0.3f, 0.5f), 1.2f, 0.2f, 2.2f, 0.25f);
            foreach (Vector3 b in bowls) FgFlame(w, b, 0.95f, flame, halo, fa);
            RealmKit.Motes(w, new Vector3(0f, 3f, 1f), new Vector3(16f, 6f, 14f), new Color(1f, 0.85f, 0.55f, 0.8f), 70);
            BuildChannelShimmer(w);

            // Colliders.
            RealmKit.Blocker(w, new Vector3(0f, 2f, -9.6f), new Vector3(18f, 4f, 0.4f));
            RealmKit.Blocker(w, new Vector3(-7.6f, 2f, -1.5f), new Vector3(0.4f, 4f, 16.5f));
            RealmKit.Blocker(w, new Vector3(7.6f, 2f, -1.5f), new Vector3(0.4f, 4f, 16.5f));
            RealmKit.Blocker(w, new Vector3(-4.75f, 2f, ChannelZ), new Vector3(6.5f, 4f, 0.8f));
            RealmKit.Blocker(w, new Vector3(4.75f, 2f, ChannelZ), new Vector3(6.5f, 4f, 0.8f));
            RealmKit.Blocker(w, new Vector3(-2.2f, 2f, 15f), new Vector3(0.4f, 4f, 14f));
            RealmKit.Blocker(w, new Vector3(2.2f, 2f, 15f), new Vector3(0.4f, 4f, 14f));
            RealmKit.Blocker(w, new Vector3(0f, 2f, 21f), new Vector3(5f, 4f, 0.4f));

            Cast(ctx, new Vector3(0f, 0f, -8.6f), 0f, new Vector3(-1.3f, 0f, -9.0f));
            foreach (GameObject who in new[] { ctx.ankhu, ctx.anpu.gameObject })
                who.AddComponent<PaintedActorGrade>().Configure(new Color(0.84f, 0.76f, 0.66f, 1f), 0.7f);
            FgContactShadow(ctx.ankhu.transform, 0.9f);
            FgContactShadow(ctx.anpu.transform, 1.0f);

            // The court trigger starts a step in front of where Ankhu stands, so the announcer calls as he walks in.
            Zone(ctx, "court", new Vector3(0f, 1f, -0.6f), new Vector3(16f, 4f, 13f), -1, 1);
            Zone(ctx, "plinth", new Vector3(4.2f, 1f, 2.9f), new Vector3(3f, 4f, 2.4f), -1, 2);
            Zone(ctx, "fallen", new Vector3(4.6f, 1f, -2.4f), new Vector3(3.6f, 4f, 3.4f), -1, 2);
            Zone(ctx, "beyond", new Vector3(0f, 1f, 13f), new Vector3(4f, 4f, 6f), -1, 3);
            Shot(ctx, "cam_court", new Vector3(0f, 6.5f, -21f), new Vector3(0f, 2f, 4f), 42f, new Vector3(0f, 1f, 4f), new Vector3(30f, 8f, 40f), 0f, 0);
            var zoom = ctx.shots[0].camera.gameObject.AddComponent<PaintedZoom>();
            zoom.fromFov = 42f; zoom.toFov = 38f; zoom.seconds = 90f; zoom.delay = 3f;

            SnapAllFirst(ctx);
            ApplyFirstGatePaint(ctx, new[] { new Rect4(new Vector3(0f, 0.012f, -1.5f), new Vector2(15f, 16f)) });
            Finish(ctx, FirstGateScene);
        }

        /// <summary>A soft dark blot on the floor under a cast member, so the feet sit on the painted stone.</summary>
        private static void FgContactShadow(Transform who, float size)
        {
            Material m = FgFlip("flip_fg_contact", FxTex("fx_glow"), new Color(0f, 0f, 0f, 0.6f), 1f, 0f, 1f, 0f, false);
            if (m == null) return;
            m.renderQueue = 3050;
            Transform q = FgQuad(who, "ContactShadow", m, new Vector3(0f, 0.02f, 0f), new Vector2(size, size));
            q.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        /// <summary>
        /// The channel's moving surface. A painted ripple sheet if one exists (fg_water_shimmer, 4x2 frames), otherwise
        /// glints and streaks breathing and swaying out of step along the water.
        /// </summary>
        private static void BuildChannelShimmer(Transform w)
        {
            Transform water = RealmKit.Group("ChannelWater", w);
            Texture2D sheet = FgTex("fg_water_shimmer", out float sa);
            if (sheet != null)
            {
                Material m = FgFlip("fx_fg_water_shimmer", sheet, new Color(0.6f, 1f, 0.95f, 0.8f), 1.2f, 0.15f, 0.9f, 0f, true);
                m.SetFloat("_Cols", 4f); m.SetFloat("_Rows", 2f); m.SetFloat("_FrameCount", 8f); m.SetFloat("_Fps", 6f);
                for (int s = -1; s <= 1; s += 2)
                {
                    Transform c = NewCard("Shimmer", water, new Vector3(s * 4.75f, 0.06f, ChannelZ), Quaternion.identity);
                    Quad(c, m, Vector3.zero, new Vector2(6.4f, 0.5f));
                }
                return;
            }
            var mats = new[]
            {
                FgFx("water_glint_a", "fx_glint", new Color(0.55f, 1f, 0.95f, 0.85f), 1.3f, 0.7f, 2.3f, 0.35f, 0.04f),
                FgFx("water_glint_b", "fx_glint", new Color(0.65f, 1f, 0.9f, 0.85f), 1.1f, 0.8f, 3.1f, 0.3f, 0.05f),
                FgFx("water_streak", "fx_streak", new Color(0.5f, 0.95f, 1f, 0.6f), 0.9f, 0.5f, 1.4f, 0.15f, 0.06f),
            };
            var rnd = new System.Random(61);
            for (int i = 0; i < 14; i++)
            {
                float x = -7.2f + i * 1.1f + (float)rnd.NextDouble() * 0.4f;
                if (Mathf.Abs(x) < 1.6f) continue; // under the bridge
                Material m = mats[i % 3];
                if (m == null) continue;
                Transform c = NewCard("Glint" + i, water, new Vector3(x, 0.05f, ChannelZ + ((float)rnd.NextDouble() - 0.5f) * 0.3f), Quaternion.identity);
                bool streak = i % 3 == 2;
                Quad(c, m, Vector3.zero, streak ? new Vector2(1.2f, 0.252f) : new Vector2(0.5f, 0.24f));
            }
        }

        /// <summary>The painting on the court camera, the set to the hidden layer, particles painted, shadows on catchers.</summary>
        private static void ApplyFirstGatePaint(Ctx ctx, Rect4[] catchers)
        {
            var shot = ctx.shots[0];
            string path = FirstGateArtDir + "/" + FirstGateScene + "_" + shot.name + ".png";
            if (!File.Exists(path)) { Debug.Log("[FirstGate] no painting yet, greybox only"); return; }
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp != null && (imp.mipmapEnabled || imp.maxTextureSize < 2048 || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.npotScale != TextureImporterNPOTScale.None))
            {
                imp.mipmapEnabled = false; imp.maxTextureSize = 2048; imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.npotScale = TextureImporterNPOTScale.None; imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return;
            Camera cam = shot.camera;
            cam.cullingMask &= ~(1 << PaintLayer);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Painting";
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(cam.transform, false);
            var mr = quad.GetComponent<MeshRenderer>();
            string mp = FirstGateArtDir + "/Materials/painting_" + FirstGateScene + "_" + shot.name + ".mat";
            var pm = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (pm == null) { pm = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(pm, mp); }
            pm.SetTexture("_BaseMap", tex);
            pm.SetColor("_BaseColor", Color.white);
            pm.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background + 10;
            EditorUtility.SetDirty(pm);
            mr.sharedMaterial = pm;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            cam.farClipPlane = 340f;
            quad.AddComponent<Ankhu.Opening.BackdropFit>().Configure(cam, 300f, NoStretchCheck.SourceAspect(tex)); // source aspect, never the imported size (no-stretch rule)
            shot.follow = 0f;

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
            Debug.Log("[FirstGate] painted");
        }

        // ------------------------------------------------------------------ plates, audit, captures

        /// <summary>Renders the court camera with only the set visible, at the plate size, for painting over.</summary>
        public static void CaptureFirstGatePlatesCli() => RunCli(() =>
        {
            string dir = Path.Combine(FirstGateArtGen, "plates");
            Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene(RealmKit.SceneDir + "/" + FirstGateScene + ".unity", OpenSceneMode.Single);
            foreach (var v in UnityEngine.Object.FindObjectsByType<RealmObjectView>(FindObjectsSortMode.None))
                if (v.poses.Count > 0) SnapFirst(v.poses[0]);
            var set = new HashSet<Renderer>(SetRenderers());
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) r.enabled = set.Contains(r);
            foreach (ParticleSystem ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) ps.gameObject.SetActive(false);
            foreach (Camera cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                foreach (Transform child in cam.transform) child.gameObject.SetActive(false);
                cam.cullingMask = ~0;
                float fov = cam.fieldOfView;
                cam.fieldOfView = PlateFov(fov);
                RenderTo(cam, Path.Combine(dir, FirstGateScene + "_" + cam.name + ".png"), PlateW, PlateH);
                cam.fieldOfView = fov;
            }
            Debug.Log("[FirstGate] plate written to " + dir);
        });

        /// <summary>
        /// Painted audit of the scene, then two edit-mode stills from the court camera: the room as it opens, and the room
        /// with all three answered and the gate open (fades shown at their end). Writes art_gen/realms/audit/first_gate_check.md.
        /// </summary>
        /// <summary>
        /// Where a pixel of the court painting lands in the world: -fgPx "x,y,height;..." (1920x1080 capture pixels, y down)
        /// prints the point on the plane at that height. Used to seat live cards on painted things that drifted from the set.
        /// </summary>
        public static void UnprojectFirstGateCli() => RunCli(() =>
        {
            EditorSceneManager.OpenScene(RealmKit.SceneDir + "/" + FirstGateScene + ".unity", OpenSceneMode.Single);
            Camera cam = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c => c.name == "cam_court");
            cam.aspect = 16f / 9f;
            string[] a = System.Environment.GetCommandLineArgs();
            foreach (string item in a[Array.IndexOf(a, "-fgPx") + 1].Split(';'))
            {
                float[] v = item.Split(',').Select(float.Parse).ToArray();
                Vector3 vp = new Vector3(v[0] / 1920f, 1f - v[1] / 1080f, 0f);
                Ray r = cam.ViewportPointToRay(vp);
                float t = (v[2] - r.origin.y) / r.direction.y;
                Vector3 w = r.origin + r.direction * t;
                Vector3 back = cam.WorldToViewportPoint(w);
                Debug.Log("[FirstGate] px " + item + " -> world " + w.ToString("F2") + " check " + (back.x * 1920f).ToString("F0") + "," + ((1f - back.y) * 1080f).ToString("F0"));
            }
        });

        public static void CheckFirstGateCli() => RunCli(() =>
        {
            EditorSceneManager.OpenScene(RealmKit.SceneDir + "/" + FirstGateScene + ".unity", OpenSceneMode.Single);
            var rows = PaintedRoomAudit.AuditOpenScene(FirstGateScene);
            var bad = rows.Where(r => r.verdict == PaintedRoomAudit.Verdict.Violation).ToList();
            var sb = new StringBuilder("# First Gate painted check\n\n");
            sb.Append("Painted audit: " + (bad.Count == 0 ? "PASS, 0 violations" : "FAIL, " + bad.Count + " violations") + " of " + rows.Count + " renderers\n\n");
            foreach (var r in bad) sb.Append("- " + r.path + " (" + r.material + ", " + r.shader + "): " + r.why + (r.poseOnly ? " [pose only]" : "") + "\n");
            foreach (var g in rows.GroupBy(r => r.verdict)) sb.Append("- " + g.Key + ": " + g.Count() + "\n");

            string dir = Path.Combine(FirstGateArtGen, "captures");
            Directory.CreateDirectory(dir);
            Camera cam = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c => c.name == "cam_court");
            var views = UnityEngine.Object.FindObjectsByType<RealmObjectView>(FindObjectsSortMode.None);
            foreach (var v in views) if (v.poses.Count > 0) SnapFirst(v.poses[0]);
            RenderTo(cam, Path.Combine(dir, "fg_start.png"), 1920, 1080);
            foreach (var v in views) if (v.poses.Count > 0) SnapFirst(v.poses[v.poses.Count - 1]);
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "DormantOut") t.gameObject.SetActive(false);
            RenderTo(cam, Path.Combine(dir, "fg_end.png"), 1920, 1080);
            sb.Append("\nCaptures: " + dir + "\\fg_start.png, fg_end.png\n");
            Directory.CreateDirectory("art_gen/realms/audit");
            File.WriteAllText("art_gen/realms/audit/first_gate_check.md", sb.ToString());
            Debug.Log("[FirstGate] check: " + bad.Count + " violations");
        });
    }
}
