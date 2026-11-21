using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ankhu.Opening;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// The building kit for realm scenes: materials, procedural textures, architecture pieces (columns with
    /// papyrus capitals, pylons with cavetto cornices, stelae, braziers), sky, post-processing and characters.
    /// Everything is generated into Assets/Ankhu/Realms/Generated so a rebuild is always clean.
    /// </summary>
    public static class RealmKit
    {
        public const string Root = "Assets/Ankhu/Realms";
        public const string Gen = Root + "/Generated";
        public const string MatDir = Gen + "/Materials";
        public const string TexDir = Gen + "/Textures";
        public const string SceneDir = Gen + "/Scenes";

        public const string AnkhuPrefab = "Assets/Characters/Ankhu/Ankhu.prefab";
        public const string AnpuPrefab = "Assets/Characters/Anpu/Anpu.prefab";
        public const string AnkhuController = "Assets/Characters/Ankhu/Ankhu.controller";
        public const string AnpuEscortController = "Assets/Ankhu/Opening/Generated/Anpu_Escort.controller";
        public const string AnpuController = "Assets/Characters/Anpu/Anpu.controller";
        public const string Ambience = "Assets/Ankhu/Opening/Audio/walk_ambience.wav";
        public const string FootstepDir = "Assets/Ankhu/Opening/Audio/Footsteps";

        private static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static void EnsureDirs()
        {
            foreach (string d in new[] { Gen, MatDir, TexDir, SceneDir })
                if (!AssetDatabase.IsValidFolder(d))
                {
                    string parent = Path.GetDirectoryName(d).Replace('\\', '/');
                    AssetDatabase.CreateFolder(parent, Path.GetFileName(d));
                }
            cache.Clear();
        }

        // ------------------------------------------------------------------ textures

        /// <summary>Tileable stone texture: layered value noise with soft strata and pitting. Saved once.</summary>
        public static Texture2D StoneTexture(string name, Color a, Color b, float strata, int seed)
        {
            string path = TexDir + "/" + name + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int n = 512;
            var t = new Texture2D(n, n, TextureFormat.RGB24, true);
            var rnd = new System.Random(seed);
            float ox = rnd.Next(0, 1000), oy = rnd.Next(0, 1000);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n, v = y / (float)n;
                    float f = 0f, amp = 0.5f, freq = 4f;
                    for (int o = 0; o < 5; o++)
                    {
                        f += amp * Tile(u, v, freq, ox + o * 31f, oy + o * 17f);
                        amp *= 0.5f;
                        freq *= 2f;
                    }
                    float band = strata > 0f ? 0.5f + 0.5f * Mathf.Sin((v * 9f + f * 1.6f) * Mathf.PI * 2f) : 0.5f;
                    float pit = Tile(u, v, 48f, ox + 99f, oy + 7f);
                    float k = Mathf.Clamp01(f * 0.9f + (band - 0.5f) * strata * 0.35f - (pit > 0.78f ? 0.18f : 0f));
                    t.SetPixel(x, y, Color.Lerp(a, b, k));
                }
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Tile(float u, float v, float freq, float ox, float oy)
        {
            // Periodic noise: blend four samples so the texture tiles.
            float a = Mathf.PerlinNoise(u * freq + ox, v * freq + oy);
            float b = Mathf.PerlinNoise((u - 1f) * freq + ox, v * freq + oy);
            float c = Mathf.PerlinNoise(u * freq + ox, (v - 1f) * freq + oy);
            float d = Mathf.PerlinNoise((u - 1f) * freq + ox, (v - 1f) * freq + oy);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        // ------------------------------------------------------------------ materials

        public static Material Lit(string name, Color color, float smooth = 0.15f, float metal = 0f, Texture2D tex = null, float tiling = 1f)
        {
            if (cache.TryGetValue(name, out Material m)) return m;
            string path = MatDir + "/" + name + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            }
            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            cache[name] = m;
            return m;
        }

        public static Material Emissive(string name, Color baseColor, Color emission)
        {
            Material m = Lit(name, baseColor, 0.3f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Transparent(string name, Color color, float smooth)
        {
            if (cache.TryGetValue(name, out Material m)) return m;
            string path = MatDir + "/" + name + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0.1f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            cache[name] = m;
            return m;
        }

        /// <summary>Additive unlit particle material (fire, motes, glow cards).</summary>
        public static Material Additive(string name, Color tint, Texture2D tex = null)
        {
            if (cache.TryGetValue(name, out Material m)) return m;
            string path = MatDir + "/" + name + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (isNew) m = new Material(sh);
            m.SetColor("_BaseColor", tint);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            cache[name] = m;
            return m;
        }

        public static Texture2D SoftDot()
        {
            string path = TexDir + "/soft_dot.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            Texture2D t = UiKit.SoftDot(64, 2f);
            File.WriteAllBytes(path, t.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Palette.
        public static Material Sandstone => Lit("Sandstone", new Color(0.78f, 0.66f, 0.50f), 0.12f, 0f, StoneTexture("sandstone", new Color(0.62f, 0.50f, 0.36f), new Color(0.90f, 0.80f, 0.64f), 1f, 11), 2f);
        public static Material Limestone => Lit("Limestone", new Color(0.86f, 0.82f, 0.74f), 0.18f, 0f, StoneTexture("limestone", new Color(0.70f, 0.66f, 0.58f), new Color(0.95f, 0.92f, 0.86f), 0.3f, 23), 2f);
        public static Material Basalt => Lit("Basalt", new Color(0.30f, 0.30f, 0.33f), 0.35f, 0f, StoneTexture("basalt", new Color(0.10f, 0.10f, 0.12f), new Color(0.32f, 0.31f, 0.34f), 0f, 37), 2f);
        public static Material Sand => Lit("Sand", new Color(0.70f, 0.60f, 0.45f), 0.05f, 0f, StoneTexture("sand", new Color(0.55f, 0.46f, 0.34f), new Color(0.80f, 0.70f, 0.54f), 0.6f, 41), 10f);
        public static Material Mud => Lit("RiverMud", new Color(0.30f, 0.25f, 0.20f), 0.45f, 0f, StoneTexture("mud", new Color(0.18f, 0.15f, 0.12f), new Color(0.36f, 0.30f, 0.23f), 0.2f, 53), 6f);
        public static Material Gold => Lit("GoldLeaf", new Color(0.95f, 0.72f, 0.30f), 0.75f, 1f);
        public static Material Lapis => Lit("LapisPaint", new Color(0.12f, 0.20f, 0.52f), 0.35f);
        public static Material Ochre => Lit("RedOchre", new Color(0.55f, 0.20f, 0.12f), 0.15f);
        public static Material Wood => Lit("Cedar", new Color(0.42f, 0.27f, 0.16f), 0.25f, 0f, StoneTexture("cedar", new Color(0.30f, 0.18f, 0.10f), new Color(0.50f, 0.33f, 0.20f), 2f, 61), 1f);
        public static Material Reed => Lit("Reed", new Color(0.42f, 0.48f, 0.26f), 0.2f);
        public static Material ReedDry => Lit("ReedDry", new Color(0.66f, 0.56f, 0.32f), 0.2f);
        public static Material Water => Transparent("Water", new Color(0.05f, 0.10f, 0.16f, 0.88f), 0.96f);
        public static Material WaterRough => Transparent("WaterRough", new Color(0.08f, 0.13f, 0.18f, 0.9f), 0.8f);
        public static Material Shadow => Lit("Shadow", new Color(0.04f, 0.04f, 0.05f), 0f);
        public static Material GoldGlow => Emissive("GoldGlow", new Color(0.9f, 0.7f, 0.3f), new Color(4f, 2.6f, 0.9f));
        public static Material EyeGlow => Emissive("EyeGlow", new Color(0.9f, 0.8f, 0.5f), new Color(6f, 4.5f, 1.6f));
        public static Material Moon => Emissive("Moon", new Color(0.9f, 0.92f, 1f), new Color(2.2f, 2.3f, 2.6f));

        // ------------------------------------------------------------------ geometry

        public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool collide = true, Vector3 euler = default)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collide) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        public static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collide = false, Vector3 euler = default)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = scale;
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collide) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        public static Transform Group(string name, Transform parent, Vector3 pos = default, Vector3 euler = default)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(euler);
            return g.transform;
        }

        /// <summary>An invisible wall (keeps Ankhu in the playable area).</summary>
        public static void Blocker(Transform parent, Vector3 pos, Vector3 size)
        {
            var g = new GameObject("Blocker");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            var c = g.AddComponent<BoxCollider>();
            c.size = size;
        }

        /// <summary>A papyrus-bundle column: base, banded shaft, open papyrus capital, abacus.</summary>
        public static Transform Column(string name, Transform parent, Vector3 pos, float height, float radius, Material stone, bool painted = true)
        {
            Transform c = Group(name, parent, pos);
            Shape(PrimitiveType.Cylinder, "Base", c, new Vector3(0f, 0.15f, 0f), new Vector3(radius * 2.6f, 0.15f, radius * 2.6f), stone, true);
            float shaft = height * 0.78f;
            Shape(PrimitiveType.Cylinder, "Shaft", c, new Vector3(0f, 0.3f + shaft * 0.5f, 0f), new Vector3(radius * 2f, shaft * 0.5f, radius * 2f), stone, true);
            if (painted)
            {
                for (int i = 0; i < 3; i++)
                    Shape(PrimitiveType.Cylinder, "Band" + i, c, new Vector3(0f, 0.3f + shaft * (0.82f + i * 0.04f), 0f), new Vector3(radius * 2.06f, 0.035f, radius * 2.06f), i == 1 ? Lapis : Ochre);
            }
            // Capital: flared stack.
            float y = 0.3f + shaft;
            int steps = 6;
            for (int i = 0; i < steps; i++)
            {
                float k = (i + 1f) / steps;
                float r = Mathf.Lerp(radius * 1.05f, radius * 1.85f, Mathf.Sqrt(k));
                float h = height * 0.17f / steps;
                Shape(PrimitiveType.Cylinder, "Capital" + i, c, new Vector3(0f, y + h * 0.5f, 0f), new Vector3(r * 2f, h * 0.5f, r * 2f), stone);
                y += h;
            }
            Box("Abacus", c, new Vector3(0f, y + height * 0.025f, 0f), new Vector3(radius * 2.2f, height * 0.05f, radius * 2.2f), stone, false);
            return c;
        }

        /// <summary>A wall with a torus moulding and cavetto cornice on top.</summary>
        public static Transform Wall(string name, Transform parent, Vector3 center, Vector3 size, Material stone)
        {
            Transform w = Group(name, parent, center);
            Box("Body", w, new Vector3(0f, size.y * 0.5f, 0f), size, stone);
            Box("Torus", w, new Vector3(0f, size.y + 0.06f, 0f), new Vector3(size.x + 0.1f, 0.12f, size.z + 0.1f), Ochre, false);
            for (int i = 0; i < 3; i++)
            {
                float k = (i + 1) / 3f;
                Box("Cornice" + i, w, new Vector3(0f, size.y + 0.12f + i * 0.14f, 0f), new Vector3(size.x + 0.12f + k * 0.5f, 0.14f, size.z + 0.12f + k * 0.5f), stone, false);
            }
            return w;
        }

        /// <summary>A pylon tower: stacked, slightly battered blocks with a cornice.</summary>
        public static Transform PylonTower(string name, Transform parent, Vector3 basePos, float width, float depth, float height, Material stone)
        {
            Transform t = Group(name, parent, basePos);
            int tiers = 6;
            for (int i = 0; i < tiers; i++)
            {
                float k = i / (float)tiers;
                float w = Mathf.Lerp(width, width * 0.86f, k);
                float d = Mathf.Lerp(depth, depth * 0.86f, k);
                float h = height / tiers;
                Box("Tier" + i, t, new Vector3(0f, h * (i + 0.5f), 0f), new Vector3(w, h + 0.01f, d), stone, i == 0);
            }
            BoxCollider col = t.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, height * 0.5f, 0f);
            col.size = new Vector3(width, height, depth);
            for (int i = 0; i < 3; i++)
                Box("Cornice" + i, t, new Vector3(0f, height + 0.15f + i * 0.2f, 0f), new Vector3(width * 0.86f + i * 0.25f, 0.2f, depth * 0.86f + i * 0.25f), stone, false);
            Box("TorusBand", t, new Vector3(0f, height * 0.985f, 0f), new Vector3(width * 0.87f, 0.14f, depth * 0.87f), Ochre, false);
            return t;
        }

        /// <summary>A brazier on a stand, with fire particles and a warm flickering light.</summary>
        public static Light Brazier(string name, Transform parent, Vector3 pos, float lightRange = 9f, float intensity = 2.4f)
        {
            Transform b = Group(name, parent, pos);
            Shape(PrimitiveType.Cylinder, "Stand", b, new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 0.55f, 0.12f), Basalt, true);
            Shape(PrimitiveType.Cylinder, "Foot", b, new Vector3(0f, 0.04f, 0f), new Vector3(0.6f, 0.04f, 0.6f), Basalt);
            Shape(PrimitiveType.Sphere, "Bowl", b, new Vector3(0f, 1.18f, 0f), new Vector3(0.75f, 0.32f, 0.75f), Gold);
            Shape(PrimitiveType.Sphere, "Embers", b, new Vector3(0f, 1.3f, 0f), new Vector3(0.55f, 0.12f, 0.55f), Emissive("Embers", new Color(0.6f, 0.2f, 0.05f), new Color(5f, 1.6f, 0.3f)));
            Fire(b, new Vector3(0f, 1.32f, 0f), 1f);
            var lgo = new GameObject("Light");
            lgo.transform.SetParent(b, false);
            lgo.transform.localPosition = new Vector3(0f, 1.9f, 0f);
            Light l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.6f, 0.28f);
            l.range = lightRange;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            lgo.AddComponent<FlameFlicker>().baseIntensity = intensity;
            return l;
        }

        public static ParticleSystem Fire(Transform parent, Vector3 pos, float scale)
        {
            var go = new GameObject("Fire");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.3f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f * scale, 0.55f * scale);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f, 0.9f), new Color(1f, 0.8f, 0.35f, 0.8f));
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 40f * scale;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 8f;
            sh.radius = 0.18f * scale;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.5f), new GradientColorKey(new Color(0.4f, 0.1f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Additive("FireAdd", new Color(1f, 0.7f, 0.4f, 1f), SoftDot());
            return ps;
        }

        /// <summary>Slow floating motes of light in the air (the Duat is never quite empty).</summary>
        public static void Motes(Transform parent, Vector3 center, Vector3 extent, Color color, int count = 60)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startColor = color;
            main.maxParticles = count * 2;
            main.prewarm = true;
            main.loop = true;
            var em = ps.emission;
            em.rateOverTime = count / 8f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = extent;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Additive("MoteAdd", Color.white, SoftDot());
        }

        /// <summary>A gold glyph plate: one quad per sign from the glyph atlas PNGs, laid out in a row or column.</summary>
        public static Transform GlyphPlate(string name, Transform parent, Vector3 pos, Vector3 euler, string signs, float size, bool vertical, Color tint)
        {
            Transform g = Group(name, parent, pos, euler);
            string[] codes = (signs ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < codes.Length; i++)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Glyphs/Resources/RealmGlyphs/" + codes[i] + ".png");
                if (tex == null) continue;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = codes[i];
                UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(g, false);
                float offset = (i - (codes.Length - 1) * 0.5f) * size * 1.05f;
                q.transform.localPosition = vertical ? new Vector3(0f, -offset, 0f) : new Vector3(offset, 0f, 0f);
                float aspect = tex.width / (float)tex.height;
                q.transform.localScale = new Vector3(size * aspect, size, 1f);
                q.GetComponent<MeshRenderer>().sharedMaterial = Additive("Glyph_" + codes[i], tint, tex);
            }
            return g;
        }

        // ------------------------------------------------------------------ atmosphere

        public static void NightSky(Transform parent, Color fog, float fogDensity)
        {
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.27f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.16f, 0.21f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.08f, 0.07f);

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.transform.SetParent(parent, false);
            moon.type = LightType.Directional;
            moon.color = new Color(0.62f, 0.72f, 1f);
            moon.intensity = 0.75f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.85f;
            moon.transform.rotation = Quaternion.Euler(35f, -140f, 0f);

            Shape(PrimitiveType.Sphere, "Moon", parent, new Vector3(-90f, 70f, 160f), Vector3.one * 9f, Moon);
            // Stars: a big shell of tiny particles.
            var go = new GameObject("Stars");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = 100000f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.85f, 1f), new Color(1f, 0.95f, 0.85f));
            main.maxParticles = 1500;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1500) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Hemisphere;
            sh.radius = 220f;
            sh.radiusThickness = 0f;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Additive("StarAdd", Color.white, SoftDot());
        }

        public static VolumeProfile PostProfile(string name, float exposure, float bloom, Color tint)
        {
            string path = Gen + "/" + name + "_Volume.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var b = profile.Add<Bloom>(true);
            b.intensity.Override(bloom);
            b.threshold.Override(0.95f);
            b.scatter.Override(0.72f);
            b.tint.Override(new Color(1f, 0.9f, 0.75f));
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(exposure);
            ca.contrast.Override(14f);
            ca.saturation.Override(-4f);
            ca.colorFilter.Override(tint);
            var v = profile.Add<Vignette>(true);
            v.intensity.Override(0.33f);
            v.smoothness.Override(0.45f);
            var fg = profile.Add<FilmGrain>(true);
            fg.type.Override(FilmGrainLookup.Thin1);
            fg.intensity.Override(0.18f);
            var sm = profile.Add<ShadowsMidtonesHighlights>(true);
            sm.shadows.Override(new Vector4(0.92f, 0.96f, 1.12f, 0f));
            sm.highlights.Override(new Vector4(1.08f, 1.02f, 0.9f, 0f));
            foreach (VolumeComponent c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        public static void GlobalVolume(Transform parent, VolumeProfile profile)
        {
            var go = new GameObject("PostProcess");
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            vol.sharedProfile = profile;
        }

        // ------------------------------------------------------------------ characters

        public static GameObject Character(string name, string prefabPath, Vector3 pos, float yaw, RuntimeAnimatorController controller, out Animator animator)
        {
            animator = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            if (prefab == null)
            {
                Debug.LogWarning("[Realms] Missing prefab " + prefabPath + ", using a capsule.");
                Shape(PrimitiveType.Capsule, "Stand-in", root.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.6f, 0.9f, 0.6f), Limestone);
                return root;
            }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            for (int pass = 0; pass < 2; pass++)
                foreach (MonoBehaviour mb in model.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
                    if (mb != null && mb.GetType().Namespace != null && mb.GetType().Namespace.StartsWith("Ankhu.Player", StringComparison.Ordinal))
                        UnityEngine.Object.DestroyImmediate(mb);
            foreach (Rigidbody2D rb in model.GetComponentsInChildren<Rigidbody2D>(true)) UnityEngine.Object.DestroyImmediate(rb);
            foreach (Collider2D c in model.GetComponentsInChildren<Collider2D>(true)) UnityEngine.Object.DestroyImmediate(c);
            animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (controller != null) animator.runtimeAnimatorController = controller;
            }
            return root;
        }

        public static Transform FindBone(Transform root, params string[] names)
        {
            foreach (string n in names)
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.EndsWith(n, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        public static AudioClip[] Footsteps()
        {
            if (!Directory.Exists(FootstepDir)) return new AudioClip[0];
            return Directory.GetFiles(FootstepDir, "*.wav").Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p.Replace('\\', '/'))).Where(c => c != null).ToArray();
        }
    }
}
