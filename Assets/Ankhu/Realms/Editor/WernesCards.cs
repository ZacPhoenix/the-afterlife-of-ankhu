using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Painted sprite cards for the Wernes puzzle pieces that must stay live (they move in front of the painting).
    /// Each grey primitive keeps its transform, poses and colliders, loses its renderer, and gains a camera-facing
    /// alpha-cut card painted in the same style as the backdrop.
    /// </summary>
    public static partial class RealmSceneBuilder
    {
        private const string CardDir = WernesArtDir + "/Sprites";

        // Card height in metres and where the glowing eyes and chest sit, measured off the sprite (fractions from the top).
        private const float ShabtiH = 1.95f, ShabtiBase = 0.05f, EyeFrac = 0.084f, EyeX = 0.045f, CartoucheFrac = 0.65f;

        private static void Cardify()
        {
            int n = 0;
            var workers = new List<Transform>();
            foreach (WorkerLoop wl in Object.FindObjectsByType<WorkerLoop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (wl.worker != null) workers.Add(wl.worker);
            foreach (Transform worker in workers)
            {
                bool named = worker.parent != null && worker.parent.name == "NamedShabti";
                n += CardShabti(worker, named ? "shabti_named" : "shabti_clay");
            }
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "Board" && t.parent != null && t.parent.name == "Leaf") { n += CardFor(t, "prop_board", t.localScale.x, t.localScale.y); }
                else if (t.name == "Wheel" && t.Find("Rim") != null) { n += CardWheel(t); }
                else if ((t.name == "JarL" || t.name == "JarR") && t.parent != null && t.parent.name == "Yoke") { n += CardFor(t, "prop_jar", t.localScale.x * 1.15f, t.localScale.y * 1.45f); }
                else if (t.name == "Dry" && t.Find("Bed") != null) { foreach (Transform c in t) Hide(c); }
                else if (t.name == "Wet" && t.Find("Bed") != null) { WetSheen(t); }
                else if (t.name == "Flood" && t.Find("Sheet") != null) { FloodSheen(t.Find("Sheet")); }
                else if (t.Find("Plank0") != null && t.Find("BraceTop") != null) { n += PaintedLeaf(t); }
                else if (t.name == "Slab" && t.Find("Stone") != null) { n += PaintedSlab(t); }
            }
            Debug.Log("[Wernes] painted cards: " + n);
            PaintMovers();
        }

        private static int CardShabti(Transform worker, string sprite)
        {
            Material m = CardMaterial(sprite, out float aspect);
            if (m == null) return 0;
            Transform head = worker.Find("Head");
            foreach (string s in new[] { "Body", "Collar", "Kilt" }) Hide(worker.Find(s));
            if (head != null) { Hide(head.Find("Face")); foreach (Transform c in head) if (c.name == "Wig") Hide(c); }

            Transform card = NewCard("Card", worker, Vector3.zero, Quaternion.identity);
            Quad(card, m, new Vector3(0f, ShabtiBase + ShabtiH * 0.5f, 0f), new Vector2(ShabtiH * aspect, ShabtiH));
            float top = ShabtiBase + ShabtiH;
            Transform eyes = head != null ? head.Find("Eyes") : null;
            if (eyes != null)
            {
                eyes.SetParent(card, false);
                eyes.localPosition = Vector3.zero; eyes.localRotation = Quaternion.identity;
                float ey = top - ShabtiH * EyeFrac;
                foreach (Transform e in eyes)
                {
                    e.localPosition = new Vector3(e.name == "EyeL" ? -EyeX : EyeX, ey, -0.03f);
                    e.localScale = Vector3.one * 0.035f;
                }
            }
            Transform chest = worker.Find("Chest");
            if (chest != null)
            {
                chest.SetParent(card, false);
                chest.localPosition = new Vector3(0f, top - ShabtiH * CartoucheFrac, -0.03f);
            }
            return 1;
        }

        private static int CardWheel(Transform wheel)
        {
            Material m = CardMaterial("prop_wheel", out float aspect);
            if (m == null) return 0;
            float d = 0f;
            foreach (Transform c in wheel) { d = Mathf.Max(d, c.localScale.x); Hide(c); }
            Transform card = NewCard("Card", wheel, Vector3.zero, Quaternion.identity);
            float h = d * 1.05f;
            Quad(card, m, Vector3.zero, new Vector2(h * aspect, h));
            return 1;
        }

        /// <summary>Replaces one primitive with a card of the given size, set where the primitive sat.</summary>
        private static int CardFor(Transform prim, string sprite, float w, float h)
        {
            Material m = CardMaterial(sprite, out _);
            if (m == null) return 0;
            Hide(prim);
            Transform card = NewCard(prim.name + "Card", prim.parent, prim.localPosition, prim.localRotation);
            Quad(card, m, Vector3.zero, new Vector2(w, h));
            return 1;
        }

        /// <summary>The painting already shows dry earth. Wet earth is a dark translucent sheen over it, with the shoots, no furrow boxes.</summary>
        private static void WetSheen(Transform wet)
        {
            Material sheen = RealmKit.Transparent("WetSheen", new Color(0.04f, 0.035f, 0.03f, 0.32f), 0.92f);
            foreach (Transform c in wet)
            {
                if (c.name == "Bed") { var r = c.GetComponent<Renderer>(); if (r != null) { r.sharedMaterial = sheen; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; } }
                else if (c.name.StartsWith("Furrow")) Hide(c);
            }
        }

        /// <summary>Standing floodwater over the painted earth: a thin moonlit glaze, not a slate box.</summary>
        private static void FloodSheen(Transform sheet)
        {
            var r = sheet.GetComponent<Renderer>();
            if (r == null) return;
            r.sharedMaterial = RealmKit.Transparent("FloodSheen", new Color(0.05f, 0.08f, 0.13f, 0.3f), 0.55f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A plank door leaf becomes one painted board in the leaf's plane, so it still swings on its hinge.</summary>
        private static int PaintedLeaf(Transform leaf)
        {
            Material m = CardMaterial("prop_board", out _);
            if (m == null) return 0;
            foreach (Transform c in leaf) if (c.name.StartsWith("Plank") || c.name.StartsWith("Brace")) Hide(c);
            var g = new GameObject("PaintedLeaf"); g.transform.SetParent(leaf, false);
            g.transform.localPosition = new Vector3(-0.05f, 1.25f, -0.9f);
            g.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Quad(g.transform, m, Vector3.zero, new Vector2(1.85f, 2.5f));
            return 1;
        }

        /// <summary>The false-door slab gets its painted stone face; it is a real plane so it can stand back ajar.</summary>
        private static int PaintedSlab(Transform slab)
        {
            Material m = CardMaterial("prop_slab", out float aspect);
            if (m == null) return 0;
            Transform stone = slab.Find("Stone");
            Hide(stone);
            var g = new GameObject("PaintedSlab"); g.transform.SetParent(slab, false);
            g.transform.localPosition = new Vector3(0f, 0f, -0.045f);
            Quad(g.transform, m, Vector3.zero, new Vector2(2.15f * aspect, 2.15f));
            return 1;
        }

        private static void Hide(Transform t)
        {
            if (t == null) return;
            var r = t.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }

        private static Transform NewCard(string name, Transform parent, Vector3 pos, Quaternion rot)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            g.AddComponent<CardFacing>();
            return g.transform;
        }

        private static void Quad(Transform card, Material m, Vector3 center, Vector2 size)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "CardQuad";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(card, false);
            q.transform.localPosition = center;
            // No-stretch rule: a hand-typed size within 5% of the image's aspect is a slip. Keep height, take width from the image.
            size = NoStretchCheck.SnapSmallError(m, size);
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static Material CardMaterial(string sprite, out float aspect)
        {
            aspect = 1f;
            string path = CardDir + "/" + sprite + ".png";
            if (!File.Exists(path)) { Debug.LogWarning("[Wernes] missing card " + path); return null; }
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
            string dir = WernesArtDir + "/Materials";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(WernesArtDir, "Materials");
            string mp = dir + "/card_" + sprite + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, mp); }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(0.93f, 0.9f, 0.86f, 1f));
            // Soft alpha blend, no depth write: a clipped card still wrote its whole quad into the depth texture,
            // and the soft ground mist drew a pale box around every shabti.
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
    }
}
