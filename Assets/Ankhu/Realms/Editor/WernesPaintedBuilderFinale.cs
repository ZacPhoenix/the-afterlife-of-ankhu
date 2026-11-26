using System;
using Ankhu.Painted;
using Ankhu.Realms.Core.Content;
using UnityEditor;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Zac 2026-11-25: the finale of the first realm, Ra's night barque over Wernes (picks: colossal sun disc,
    /// valley-long barque on the river, Mehen in the classical arch, Apep's shadow under the water, a parallax layer
    /// stack). One low camera behind Ankhu and Anpu at the terrace parapet looks down the valley. Painted layers at
    /// depth in front of the clean valley painting: the barque gliding left to right along the far river with its
    /// halo and rays, Apep's shadow sliding the other way under it and its eye opening, the parapet and brazier
    /// close in front of the cast. The camera creeps forward the whole time, so the layers part against the sky.
    /// Art: art_gen/ra_barque/finale, keyed by key_magenta.py into Art/Wernes/Sprites/fin_*.png.
    /// Build: -executeMethod Ankhu.Realms.EditorTools.RealmSceneBuilder.BuildWernesFinaleCli
    /// </summary>
    public static partial class RealmSceneBuilder
    {
        // The camera, and where the far river and horizon sit in the valley painting (fraction of image height from
        // the top). The painting is fitted by BackdropFit, which crops a 3:2 image top and bottom on a 16:9 screen.
        private static readonly Vector3 FinCamPos = new Vector3(0f, 1.55f, -3.4f);
        private static readonly Vector3 FinCamLook = new Vector3(0f, 1.2f, 40f);
        private const float FinFov = 42f;
        private const float FinRiverIy = 0.53f;   // the far bright river the barque rides on
        private const float FinHorizonIy = 0.44f; // the hills and the burning glow on the horizon
        private const float FinApepIy = 0.585f;    // the broad near reach of the river Apep slides under
        private const float FinBarqueDepth = 170f, FinApepDepth = 150f, FinGlowDepth = 260f, FinParapetZ = 1.0f;

        [MenuItem("Ankhu/Realms/Build Wernes Finale (Ra's barque)")]
        public static void BuildWernesFinaleMenu() => BuildWernesFinale();

        public static void BuildWernesFinaleCli()
        {
            try { BuildWernesFinale(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// One Unity launch for the finale: build it, audit it against the painted spec, check the play order and
        /// render the shot at the start, middle and end of the barque's glide with every layer shown.
        /// Writes art_gen/realms/audit/finale_check.md and art_gen/ra_barque/captures/fin_*.png.
        /// </summary>
        public static void CheckWernesFinaleCli()
        {
            int code = 1;
            try
            {
                BuildWernesFinale();
                string path = RealmKit.SceneDir + "/" + WernesPaintedScripts.Finale + ".unity";
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                var rows = PaintedRoomAudit.AuditOpenScene(WernesPaintedScripts.Finale);
                var bad = rows.FindAll(r => r.verdict == PaintedRoomAudit.Verdict.Violation);
                int playBad = PlayOrderCheck.Run(out string playReport);
                var sb = new System.Text.StringBuilder("# Wernes finale check\n\n");
                sb.AppendLine("PaintedRoomAudit: " + rows.Count + " visible renderers, " + bad.Count + " violations (" + (bad.Count == 0 ? "PASS" : "FAIL") + ")");
                foreach (var r in bad) sb.AppendLine("- `" + r.path + "` " + r.material + ": " + r.why);
                sb.AppendLine();
                sb.AppendLine("PlayOrderCheck: " + playBad + " problems");
                sb.AppendLine();
                sb.AppendLine(playReport);

                // Show every layer, set each glide to t, render.
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                string capDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../art_gen/ra_barque/captures"));
                System.IO.Directory.CreateDirectory(capDir);
                foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t.name == "Barque" || t.name == "ApepShadow" || t.name == "Eye") t.gameObject.SetActive(true);
                Camera cam = null;
                foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (c.name == "cam_finale") cam = c;
                if (cam == null) Debug.LogError("[Finale] cam_finale not found, no captures");
                var drifts = UnityEngine.Object.FindObjectsByType<PaintedDrift>(FindObjectsSortMode.None);
                foreach (float k in new[] { 0f, 0.5f, 1f })
                {
                    foreach (var d in drifts) if (!d.fade) d.transform.localPosition = Vector3.Lerp(d.from, d.to, k);
                    if (cam != null)
                    {
                        foreach (Transform child in cam.transform) child.gameObject.SetActive(child.name == "Painting");
                        RenderTo(cam, System.IO.Path.Combine(capDir, "fin_t" + Mathf.RoundToInt(k * 100) + ".png"), 1920, 1080);
                    }
                }
                string md = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../art_gen/realms/audit/finale_check.md"));
                System.IO.File.WriteAllText(md, sb.ToString());
                Debug.Log("[Finale] check written " + md + " audit violations " + bad.Count + ", play order problems " + playBad);
                code = bad.Count == 0 ? 0 : 1;
            }
            catch (Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(code);
        }

        public static void BuildWernesFinale()
        {
            RealmKit.EnsureDirs();
            BuildFinaleScene();
            AddWernesToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Realms] Wernes finale built.");
        }

        private static void BuildFinaleScene()
        {
            string scene = WernesPaintedScripts.Finale;
            Ctx ctx = Begin(scene, new Color(0.12f, 0.08f, 0.16f), 0.004f, 0.55f);
            Transform w = ctx.world;

            // Colliders only: the ledge they stand on and walls so nobody walks off. Moved to the hidden layer by ApplyPaint.
            Transform ledge = RealmKit.Group("Ledge", w);
            RealmKit.Box("Floor", ledge, new Vector3(0f, -0.2f, 0f), new Vector3(8f, 0.4f, 4f), RealmKit.Limestone);
            RealmKit.Blocker(w, new Vector3(0f, 3f, 1.2f), new Vector3(8f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(0f, 3f, -1.8f), new Vector3(8f, 6f, 0.4f));
            RealmKit.Blocker(w, new Vector3(-3.6f, 3f, 0f), new Vector3(0.4f, 6f, 4f));
            RealmKit.Blocker(w, new Vector3(3.6f, 3f, 0f), new Vector3(0.4f, 6f, 4f));
            RealmKit.Motes(w, new Vector3(0f, 1.8f, 0.6f), new Vector3(7f, 2.4f, 2f), new Color(1f, 0.85f, 0.6f, 0.5f), 50);

            // At the parapet, seen from behind, looking down the valley.
            Cast(ctx, new Vector3(-0.75f, 0f, 0.15f), 0f, new Vector3(0.85f, 0f, 0.25f));
            Shot(ctx, "cam_finale", FinCamPos, FinCamLook, FinFov, new Vector3(0f, 2f, 0f), new Vector3(10f, 8f, 8f), 0f, 0);
            ApplyPaint(ctx, scene, new Rect4[0]);

            // Everything below is added after ApplyPaint, so it stays on the visible layer. All of it is painted.
            Camera cam = ctx.shots[0].camera;
            Transform layers = RealmKit.Group("PaintedLayers", w);
            float aspect = 16f / 9f;

            // The horizon burning before the barque appears, and brighter once it does.
            Vector3 glowAt = FinView(cam, aspect, 0.5f, FinHorizonIy, FinGlowDepth);
            float wGlow = FinViewWidth(cam, aspect, FinGlowDepth);
            FinCard(layers, "HorizonGlow", FinFx("horizon", "fx_glow", new Color(1f, 0.72f, 0.42f, 0.85f), 1.4f, 0.12f, 0.35f, 0.04f), glowAt, new Vector2(wGlow * 0.9f, wGlow * 0.22f), cam);

            // The barque, valley-long on the far river, gliding left to right (its prow, Mehen's head, faces right).
            Texture2D barqueTex = SpriteTex("fin_barque", out float ba);
            float wView = FinViewWidth(cam, aspect, FinBarqueDepth);
            float bw = wView * 0.50f, bh = bw / ba;
            Vector3 riverL = FinView(cam, aspect, 0.26f, FinRiverIy, FinBarqueDepth);
            Vector3 riverR = FinView(cam, aspect, 0.54f, FinRiverIy, FinBarqueDepth);
            Vector3 up = cam.transform.up * (bh * 0.47f); // the hull's waterline sits near the card's bottom
            var barque = new GameObject("Barque").transform;
            barque.SetParent(layers, false);
            barque.rotation = cam.transform.rotation;
            var bd = barque.gameObject.AddComponent<PaintedDrift>();
            bd.from = riverL + up; bd.to = riverR + up; bd.seconds = 75f;
            Vector3 discOff = new Vector3(0f, bh * 0.05f, 0f); // the disc's centre in the card
            FinLocalCard(barque, "Halo", FinFx("halo", "fx_glow", new Color(1f, 0.62f, 0.30f, 1f), 2.2f, 0.18f, 0.6f, 0.05f), discOff + new Vector3(0f, 0f, 0.6f), new Vector2(bh * 1.7f, bh * 1.7f));
            for (int i = 0; i < 7; i++)
            {
                Transform ray = FinLocalCard(barque, "Ray" + i, FinFx("ray" + i, "fx_streak", new Color(1f, 0.78f, 0.45f, 0.55f), 1.2f, 0.3f, 0.4f + i * 0.07f, 0.1f), discOff + new Vector3(0f, 0f, 0.4f), new Vector2(bh * 2.6f, bh * 0.05f));
                ray.localRotation = Quaternion.Euler(0f, 0f, i * 180f / 7f + 8f);
            }
            FinLocalCard(barque, "Hull", FlipMat("flip_fin_barque", barqueTex, new Color(0.92f, 0.88f, 0.85f, 1f), 1.15f, 0.03f, 0.5f, 0.02f, false), Vector3.zero, new Vector2(bw, bh));
            FinLocalCard(barque, "DiscBloom", FinFx("bloom", "fx_glow", new Color(1f, 0.8f, 0.5f, 0.5f), 1.0f, 0.25f, 0.9f, 0.06f), discOff - new Vector3(0f, 0f, 0.4f), new Vector2(bh * 0.9f, bh * 0.9f));
            // The river taking its light: glints in a column under the disc, moving with it.
            for (int i = 0; i < 6; i++)
                FinLocalCard(barque, "Glint" + i, FinFx("glint" + i, "fx_glint", new Color(1f, 0.75f, 0.4f, 0.8f), 1.4f, 0.5f, 1.3f + i * 0.31f, 0.4f),
                    new Vector3((i % 2 == 0 ? -1f : 1f) * bh * 0.05f * i, -bh * (0.52f + i * 0.05f), -0.3f), new Vector2(bh * (0.5f - i * 0.05f), bh * 0.035f));
            barque.gameObject.SetActive(false);

            // Apep's shadow under the water, nearer than the barque and sliding the other way, then its eye opens.
            float wA = FinViewWidth(cam, aspect, FinApepDepth);
            // Foreshortened into a flat band that stays inside the river reach, not spread over the fields.
            float aw = wA * 0.72f, ah = FinViewWidth(cam, aspect, FinApepDepth) / aspect * 0.13f;
            var apep = new GameObject("ApepShadow").transform;
            apep.SetParent(layers, false);
            apep.rotation = cam.transform.rotation;
            var ad = apep.gameObject.AddComponent<PaintedDrift>();
            ad.from = FinView(cam, aspect, 0.66f, FinApepIy, FinApepDepth);
            ad.to = FinView(cam, aspect, 0.40f, FinApepIy, FinApepDepth);
            ad.seconds = 45f;
            // No-stretch (Zac 2026-11-26): the one fin_apep card was shown 3.66x wide. The coils are now four section
            // cards, each painted at its own 2.61:1, laid along the band. Layout in band widths (aw), from
            // _staging/cards/apep_layout.json: part, centre x, centre y, width, height.
            var apepParts = new (string part, float cx, float cy, float w, float h)[]
            {
                ("tail", -0.35223f, -0.00119f, 0.29553f, 0.11317f),
                ("body1", -0.11983f, 0.00131f, 0.27770f, 0.10651f),
                ("body2", 0.10604f, -0.00202f, 0.28245f, 0.10818f),
                ("head", 0.34653f, 0f, 0.30694f, 0.11769f),
            };
            foreach (var p in apepParts)
            {
                Texture2D partTex = SpriteTex("fin_apep_" + p.part, out _);
                Material apepMat = FlipMat("flip_fin_apep_" + p.part, partTex, new Color(0.22f, 0.2f, 0.36f, 0.92f), 1f, 0.06f, 0.25f, 0f, false, 0.004f);
                // Under the water: drawn before the barque even though it is nearer, so it never covers the hull.
                if (apepMat != null) apepMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 20;
                Transform apepCard = FinLocalCard(apep, "Coils_" + p.part, apepMat, new Vector3(p.cx * aw, p.cy * aw, 0f), new Vector2(p.w * aw, p.h * aw));
                MeshRenderer apepMr = apepCard != null ? apepCard.GetComponentInChildren<MeshRenderer>() : null;
                if (apepMr != null)
                {
                    var fade = apepMr.gameObject.AddComponent<PaintedDrift>();
                    fade.from = fade.to = fade.transform.localPosition; fade.seconds = 8f; fade.fade = true; fade.fadeFrom = 0f; fade.fadeTo = 1f;
                }
            }
            Texture2D eyeTex = SpriteTex("fin_apep_eye", out float ea);
            Material eyeMat = FlipMat("flip_fin_apep_eye", eyeTex, Color.white, 1.6f, 0f, 0f, 0f, false);
            if (eyeMat != null)
            {
                eyeMat.SetFloat("_Cols", 4f); eyeMat.SetFloat("_Rows", 2f); eyeMat.SetFloat("_FrameCount", 8f);
                eyeMat.SetFloat("_Fps", 5f); eyeMat.SetFloat("_Loop", 0f);
            }
            float eh = ah * 0.95f;
            Transform eye = FinLocalCard(apep, "Eye", eyeMat, new Vector3(aw * 0.38f, -ah * 0.22f, -0.5f), new Vector2(eh * ea / 2f, eh));
            eye.GetComponentInChildren<MeshRenderer>().gameObject.AddComponent<FlipbookPlay>();
            eye.gameObject.SetActive(false);
            apep.gameObject.SetActive(false);

            // The parapet and its brazier, just beyond the cast, along the bottom of the frame.
            Texture2D parTex = SpriteTex("fin_parapet", out float pa);
            float dPar = FinParapetZ - FinCamPos.z;
            float pw = FinViewWidth(cam, aspect, dPar) * 1.06f, ph = pw / pa;
            Vector3 parBottom = FinView(cam, aspect, 0.5f, 1.13f, dPar, true);
            var parapet = FinCard(layers, "Parapet", FlipMat("flip_fin_parapet", parTex, Color.white, 1f, 0f, 0f, 0f, false), parBottom + cam.transform.up * (ph * 0.5f), new Vector2(pw, ph), cam);
            Texture2D flameTex = SpriteTex("fin_flame", out float fa);
            Material flameMat = FlipMat("flip_fin_flame", flameTex, new Color(1f, 0.92f, 0.8f, 1f), 1.5f, 0.08f, 3f, 0.1f, true);
            if (flameMat != null)
            {
                flameMat.SetFloat("_Cols", 4f); flameMat.SetFloat("_Rows", 4f); flameMat.SetFloat("_FrameCount", 16f);
                flameMat.SetFloat("_Fps", 12f); flameMat.SetFloat("_Loop", 1f);
            }
            float fh = ph * FinFlameH;
            FinLocalCard(parapet, "Flame", flameMat, new Vector3(pw * (FinBrazierU - 0.5f), ph * (0.5f - FinBrazierV) + fh * 0.4f, -0.02f), new Vector2(fh * fa, fh));
            FinLocalCard(parapet, "FlameGlow", FinFx("flameglow", "fx_glow", new Color(1f, 0.6f, 0.3f, 0.5f), 1.2f, 0.2f, 2.2f, 0.25f), new Vector3(pw * (FinBrazierU - 0.5f), ph * (0.5f - FinBrazierV) + fh * 0.4f, 0.01f), new Vector2(fh * 2.4f, fh * 2.4f));

            // The camera creeps toward the parapet the whole scene, so the near layers part against the far ones.
            var dolly = cam.gameObject.AddComponent<PaintedDrift>();
            dolly.from = cam.transform.localPosition;
            dolly.to = cam.transform.localPosition + cam.transform.forward * 0.9f + Vector3.up * 0.08f;
            dolly.seconds = 80f;

            ctx.director.cues.Add(new RealmSceneDirector.CueHook { cue = "barque_rise", activate = new[] { barque.gameObject } });
            ctx.director.cues.Add(new RealmSceneDirector.CueHook { cue = "apep_pass", activate = new[] { apep.gameObject } });
            ctx.director.cues.Add(new RealmSceneDirector.CueHook { cue = "apep_eye", activate = new[] { eye.gameObject } });

            Finish(ctx, scene);
        }

        // Where the brazier's ember bowl sits in the parapet sprite (fraction of width, and of height from the top),
        // and the flame's height as a fraction of the sprite's height. Set from the keyed art.
        private const float FinBrazierU = 0.847f, FinBrazierV = 0.075f, FinFlameH = 0.6f;

        /// <summary>A world point on the screen: u across, iy down the painting (fraction from its top), d metres out.</summary>
        private static Vector3 FinView(Camera cam, float aspect, float u, float iy, float d, bool viewportV = false)
        {
            float v = viewportV ? 1f - iy : 0.5f - (iy - 0.5f) * FinCropScale(cam, aspect);
            float h = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Transform t = cam.transform;
            return t.position + t.forward * d + t.right * ((u - 0.5f) * h * aspect) + t.up * ((v - 0.5f) * h);
        }

        /// <summary>A finale-only additive glow, so its tint and pulse never change the shared fx_ materials of the rooms.</summary>
        private static Material FinFx(string name, string sprite, Color tint, float gain, float pulse, float pulseSpeed, float flicker)
            => FlipMat("fx_fin_" + name, FxTex(sprite), tint, gain, pulse, pulseSpeed, flicker, true);

        private static float FinViewWidth(Camera cam, float aspect, float d) => 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * aspect;

        /// <summary>How much BackdropFit enlarges a 3:2 painting to cover the screen width (the crop top and bottom).</summary>
        private static float FinCropScale(Camera cam, float aspect) => Mathf.Max(1f, aspect / 1.5f);

        private static Transform FinCard(Transform parent, string name, Material m, Vector3 pos, Vector2 size, Camera faceCam)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.position = pos;
            g.rotation = faceCam.transform.rotation;
            if (m != null) Quad(g, m, Vector3.zero, size);
            return g;
        }

        private static Transform FinLocalCard(Transform parent, string name, Material m, Vector3 local, Vector2 size)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.localPosition = local;
            g.localRotation = Quaternion.identity;
            if (m != null) Quad(g, m, Vector3.zero, size);
            return g;
        }
    }
}
