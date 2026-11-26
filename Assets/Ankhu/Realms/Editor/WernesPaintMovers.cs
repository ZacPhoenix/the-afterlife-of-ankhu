using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// The second half of docs/PAINTED_ROOM_SPEC.md for Wernes: every live puzzle piece that Cardify does not already
    /// turn into a painted card is swapped here. The primitive keeps its transform, pose and collider and loses its
    /// renderer. A painted quad from the room's sprite atlas takes its place, in the piece's own plane when the piece
    /// turns in z (lids, reeds, bricks) so it never billboards out of its swing. Glows and beads are painted fx sprites
    /// on Ankhu/PaintedFlipbook, additive, with a slow pulse or flicker. Particles keep their motion and get painted
    /// fx textures. Anything not covered stays visible to PaintedRoomAudit, so a miss shows up as a violation.
    /// </summary>
    public static partial class RealmSceneBuilder
    {
        private const string FxDir = WernesArtDir + "/Sprites/FX";

        private static void PaintMovers()
        {
            int n = PaintParticles();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform p = t.parent;
                string pn = p != null ? p.name : "";
                // ---------------------------------------------------------------- Room D, the birth bricks
                if (t.name == "Body" && pn.StartsWith("Brick") && t.Find("Clay") != null) n += PaintBrick(t);
                else if (t.name == "Moon" && t.GetComponent<Renderer>() != null) { Hide(t); n++; } // the painting carries its own moon
                else if (t.name == "Meskhenet") n += PaintMeskhenet(t);
                else if (t.name == "Basin" && t.Find("Fill") != null) n += PaintBasin(t);
                else if (t.name == "Basket" && t.Find("Knot") != null) n += PaintBasket(t);
                else if (t.name == "Snare" && t.Find("Tie") != null) n += PaintSnare(t);
                else if (t.name == "Cry" && t.Find("Echo") != null) n += PaintCry(t);
                else if (t.name == "Misread" && t.Find("Shiver") != null)
                {
                    Hide(t.Find("Shiver"));
                    PQuad(t, "ShiverFx", FxMat("fx_streak", new Color(0.55f, 0.7f, 1f, 1f), 1.6f, 0.35f, 2.5f, 0.4f), new Vector3(0f, 0f, -0.05f), new Vector2(4.8f, 0.34f));
                    n++;
                }
                else n += PaintABC(t, pn);
            }
            Debug.Log("[Wernes] painted movers: " + n);
        }

        // ------------------------------------------------------------------ Room D pieces

        private static int PaintBrick(Transform body)
        {
            foreach (string s in new[] { "Clay", "Face" }) Hide(body.Find(s));
            Transform mother = body.Find("Mother");
            if (mother != null) foreach (Renderer r in mother.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            // The brick sprite shows the front and a sliver of the top, so it stands a little taller than the clay box.
            Material brick = SpriteMat("d_brick", out float a);
            PQuad(body, "BrickCard", brick, new Vector3(0f, 0.37f, -0.17f), new Vector2(1.0f, 1.0f / a));
            Transform scrape = body.Find("Scrape");
            if (scrape != null)
            {
                foreach (Transform m in scrape) Hide(m);
                Material sm = SpriteMat("d_scrape", out float sa);
                PQuad(scrape, "ScrapeCard", sm, new Vector3(0.1f, 0.33f, -0.2f), new Vector2(0.36f * sa, 0.36f));
            }
            Transform rim = body.Find("WakeRim");
            if (rim != null)
            {
                // The rim was a scaled glow box. Unscale it so the lit brick can sit exactly over the plain one, then
                // let it breathe: the awake pose shows it and the brick seems to glow from inside its seams.
                Hide(rim);
                rim.localPosition = Vector3.zero; rim.localScale = Vector3.one;
                Material lit = FlipMat("flip_d_brick_lit", SpriteTex("d_brick_lit", out float la), Color.white, 1.05f, 0.18f, 1.8f, 0.05f, false);
                PQuad(rim, "LitCard", lit, new Vector3(0f, 0.37f, -0.18f), new Vector2(1.0f, 1.0f / la));
            }
            return 1;
        }

        private static int PaintMeskhenet(Transform god)
        {
            int n = 0;
            Transform lid = god.Find("Lid"), eye = god.Find("Eye"), fist = god.Find("Fist");
            if (lid != null)
            {
                Hide(lid.Find("Line"));
                PQuad(lid, "ShutCard", FlipMat("flip_d_eye_shut", SpriteTex("d_eye_shut", out float a), Color.white, 1f, 0.12f, 0.9f, 0f, false), new Vector3(0f, 0f, -0.01f), new Vector2(0.3f, 0.3f / a));
                n++;
            }
            if (eye != null)
            {
                Hide(eye.Find("Iris"));
                PQuad(eye, "OpenCard", FlipMat("flip_d_eye_open", SpriteTex("d_eye_open", out float a), Color.white, 1.1f, 0.15f, 1.4f, 0.04f, false), new Vector3(0f, 0f, -0.01f), new Vector2(0.34f, 0.34f / a));
                n++;
            }
            if (fist != null)
            {
                Hide(fist.Find("Hand"));
                PQuad(fist, "HandCard", FlipMat("flip_d_hand_ankh", SpriteTex("d_hand_ankh", out float a), Color.white, 1f, 0.1f, 1.1f, 0f, false), new Vector3(0f, 0.12f, -0.01f), new Vector2(0.52f * a, 0.52f));
                n++;
            }
            return n;
        }

        private static int PaintBasin(Transform basin)
        {
            Transform cracks = basin.Find("Cracks");
            if (cracks != null)
            {
                foreach (Transform c in cracks) Hide(c);
                Material m = SpriteMat("d_basin_cracks", out float a);
                PQuad(cracks, "CracksCard", m, new Vector3(0f, 0.46f, -0.08f), new Vector2(0.92f, 0.92f / a));
            }
            Transform fill = basin.Find("Fill");
            Transform sheet = fill != null ? fill.Find("Sheet") : null;
            if (sheet != null)
            {
                Hide(sheet);
                // Moonlit water with a slow shimmer. The fill pose already grows it from a small pool to the brim.
                Material w = FlipMat("flip_d_basin_water", SpriteTex("d_basin_water", out float a), Color.white, 1.05f, 0.14f, 1.2f, 0.06f, false);
                PQuad(fill, "WaterCard", w, new Vector3(0f, 0.02f, -0.1f), new Vector2(0.9f, 0.9f / a));
            }
            return 1;
        }

        private static int PaintBasket(Transform basket)
        {
            Transform lid = basket.Find("Lid");
            if (lid != null)
            {
                foreach (Transform c in lid) Hide(c);
                Material m = SpriteMat("d_basket_lid", out float a);
                PQuad(lid, "LidCard", m, new Vector3(0f, 0.08f, -0.36f), new Vector2(0.84f, 0.84f / a));
            }
            Transform knot = basket.Find("Knot");
            if (knot != null)
            {
                foreach (Transform c in knot) Hide(c);
                Material m = SpriteMat("d_basket_knot", out float a);
                PQuad(knot, "KnotCard", m, new Vector3(0f, 0f, -0.03f), new Vector2(0.82f, 0.82f / a));
            }
            return 1;
        }

        private static int PaintSnare(Transform snare)
        {
            Material reed = FlipMat("flip_d_reed", SpriteTex("d_reed", out float a), Color.white, 1f, 0f, 1f, 0f, false, 0.03f);
            foreach (Transform bar in snare)
            {
                if (!bar.name.StartsWith("Bar")) continue;
                Hide(bar.Find("Reed"));
                PQuad(bar, "ReedCard", reed, new Vector3(0f, 0.38f, -0.03f), new Vector2(0.8f * a, 0.8f));
            }
            Transform tie = snare.Find("Tie");
            if (tie != null)
            {
                Hide(tie.Find("Band"));
                Material m = SpriteMat("d_snare_band", out float ba);
                PQuad(tie, "BandCard", m, new Vector3(0f, 0f, -0.06f), new Vector2(0.96f, 0.96f / ba));
            }
            return 1;
        }

        private static int PaintCry(Transform cry)
        {
            Material gold = FxMat("fx_bead", new Color(1f, 0.9f, 0.7f, 1f), 1.2f, 0.25f, 2.2f, 0.2f);
            Material cyan = FxMat("fx_bead_cyan", new Color(0.8f, 1f, 1f, 1f), 1.2f, 0.3f, 1.7f, 0.15f);
            foreach (Transform ring in cry)
            {
                bool echo = ring.name == "Echo";
                if (!echo && !ring.name.StartsWith("Ring")) continue;
                // Snapshot first: the glints are added to the same ring, and enumerating a growing list never ends.
                var beads = new System.Collections.Generic.List<Transform>();
                foreach (Transform b in ring) if (b.name.StartsWith("Bead") && b.GetComponent<Renderer>() != null) beads.Add(b);
                foreach (Transform bead in beads)
                {
                    Hide(bead);
                    // Sibling, not child: the bead sphere is scaled to a few centimetres.
                    PQuad(ring, "Glint" + bead.name.Substring(4), echo ? cyan : gold, bead.localPosition + new Vector3(0f, 0f, -0.02f), Vector2.one * (echo ? 0.11f : 0.13f));
                }
            }
            return 1;
        }

        // ------------------------------------------------------------------ particles

        /// <summary>Fire, motes and stars keep their simulation and lose the procedural soft dot.</summary>
        private static int PaintParticles()
        {
            int n = 0;
            foreach (ParticleSystemRenderer r in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string name = r.gameObject.name;
                if (name == "Stars") { r.enabled = false; n++; continue; } // the painting carries its own sky
                string sprite = name == "Fire" ? "fx_ember" : name == "Motes" ? "fx_mote" : null;
                if (sprite == null) continue;
                Texture2D tex = FxTex(sprite);
                if (tex == null) continue;
                r.sharedMaterial = RealmKit.Additive(sprite, name == "Fire" ? new Color(1f, 0.85f, 0.7f, 1f) : Color.white, tex);
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ helpers

        private static void PQuad(Transform parent, string name, Material m, Vector3 pos, Vector2 size)
        {
            if (m == null) return;
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            Quad(g.transform, m, Vector3.zero, size);
        }

        private static Material SpriteMat(string sprite, out float aspect) => CardMaterial(sprite, out aspect);

        private static Texture2D SpriteTex(string sprite, out float aspect)
        {
            // CardMaterial sets the import (clamp, no NPOT scaling, alpha) and reports the true aspect.
            CardMaterial(sprite, out aspect);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(WernesArtDir + "/Sprites/" + sprite + ".png");
        }

        private static Texture2D FxTex(string sprite)
        {
            string path = FxDir + "/" + sprite + ".png";
            if (!File.Exists(path)) { Debug.LogWarning("[Wernes] missing fx " + path); return null; }
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp != null && (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp || imp.npotScale != TextureImporterNPOTScale.None))
            {
                imp.npotScale = TextureImporterNPOTScale.None; imp.alphaIsTransparency = true; imp.mipmapEnabled = true;
                imp.wrapMode = TextureWrapMode.Clamp; imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>An additive painted glow: one fx sprite on Ankhu/PaintedFlipbook with pulse and flicker.</summary>
        private static Material FxMat(string sprite, Color tint, float gain, float pulse, float pulseSpeed, float flicker)
            => FlipMat(sprite, FxTex(sprite), tint, gain, pulse, pulseSpeed, flicker, true);

        /// <summary>A one-frame Ankhu/PaintedFlipbook material, alpha blended or additive, with optional breathing and sway.</summary>
        private static Material FlipMat(string matName, Texture2D tex, Color tint, float gain, float pulse, float pulseSpeed, float flicker, bool additive, float sway = 0f)
        {
            if (tex == null) return null;
            Shader sh = Shader.Find("Ankhu/PaintedFlipbook");
            if (sh == null) { Debug.LogError("[Wernes] Ankhu/PaintedFlipbook did not compile"); return null; }
            string dir = WernesArtDir + "/Materials";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(WernesArtDir, "Materials");
            string path = dir + "/" + matName + ".mat";
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
            m.SetFloat("_SrcBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.SrcAlpha : UnityEngine.Rendering.BlendMode.SrcAlpha));
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + (additive ? 10 : 0);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
