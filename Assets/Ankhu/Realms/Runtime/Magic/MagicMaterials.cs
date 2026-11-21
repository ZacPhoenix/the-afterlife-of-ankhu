using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ankhu.Realms
{
    /// <summary>
    /// Runtime materials for the words of power. Shaders resolve in this order: anything handed to
    /// <see cref="RegisterShaders"/> (or a <see cref="MagicShaderRefs"/> in the scene), then Shader.Find.
    /// The preferred additive shader is "Ankhu/Realms/MagicAdditive" (Art/Glyphs/Resources/RealmShaders,
    /// always in builds because it sits under Resources and has no keywords). If it is missing, URP
    /// "Universal Render Pipeline/Particles/Unlit" is configured for additive blending, then "Sprites/Default".
    /// </summary>
    public static class MagicMaterials
    {
        public const string AdditiveShaderName = "Ankhu/Realms/MagicAdditive";
        public const string RimShaderName = "Ankhu/Realms/GoldRim";
        public const string UrpParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";
        public const string UrpUnlit = "Universal Render Pipeline/Unlit";
        public const string SpritesDefault = "Sprites/Default";

        public static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        public static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        public static readonly int ColorId = Shader.PropertyToID("_Color");
        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private static readonly Dictionary<string, Shader> registered = new Dictionary<string, Shader>();
        private static readonly Dictionary<Texture, Material> additiveByTexture = new Dictionary<Texture, Material>();
        private static readonly Dictionary<Texture, Material> alphaByTexture = new Dictionary<Texture, Material>();
        private static Material additiveTemplate;
        private static Material alphaTemplate;
        private static Material rimMaterial;
        private static Mesh quad;
        private static bool warned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            registered.Clear();
            additiveByTexture.Clear();
            alphaByTexture.Clear();
            additiveTemplate = null;
            alphaTemplate = null;
            rimMaterial = null;
            quad = null;
            warned = false;
        }

        /// <summary>
        /// Registers shaders by their own name so lookups never depend on Shader.Find in a build.
        /// Call from an editor-built scene object (see <see cref="MagicShaderRefs"/>) or any bootstrap.
        /// </summary>
        public static void RegisterShaders(params Shader[] shaders)
        {
            if (shaders == null) return;
            foreach (Shader s in shaders)
            {
                if (s != null) registered[s.name] = s;
            }
        }

        /// <summary>
        /// Optional template materials made in the editor (for example with <see cref="ConfigureAdditive"/> and saved
        /// as assets). Runtime materials are cloned from them, which keeps their shader variant in the build.
        /// </summary>
        public static void RegisterTemplates(Material additive, Material alphaBlended)
        {
            if (additive != null) { additiveTemplate = additive; additiveByTexture.Clear(); }
            if (alphaBlended != null) { alphaTemplate = alphaBlended; alphaByTexture.Clear(); }
        }

        public static Shader FindShader(string name)
        {
            if (registered.TryGetValue(name, out Shader s) && s != null) return s;
            s = Shader.Find(name);
            if (s != null) registered[name] = s;
            return s;
        }

        private static Shader BestEffectShader()
        {
            Shader s = FindShader(AdditiveShaderName);
            if (s != null) return s;
            s = FindShader(UrpParticlesUnlit);
            if (s != null) return s;
            s = FindShader(UrpUnlit);
            if (s != null) return s;
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("[Ankhu.Realms] Magic shaders not found, falling back to Sprites/Default. See README_MAGIC.md.");
            }
            s = FindShader(SpritesDefault);
            return s != null ? s : FindShader("UI/Default");
        }

        /// <summary>Additive (SrcAlpha, One) material for a texture. Cached per texture, never destroyed.</summary>
        public static Material Additive(Texture texture)
        {
            Texture key = texture != null ? texture : Texture2D.whiteTexture;
            if (additiveByTexture.TryGetValue(key, out Material m) && m != null) return m;
            m = additiveTemplate != null ? new Material(additiveTemplate) : new Material(BestEffectShader());
            m.name = "Magic Additive " + key.name;
            m.hideFlags = HideFlags.DontSave;
            ConfigureAdditive(m);
            SetTexture(m, key);
            additiveByTexture[key] = m;
            return m;
        }

        /// <summary>Alpha blended material for dust and smoke. Cached per texture.</summary>
        public static Material AlphaBlended(Texture texture)
        {
            Texture key = texture != null ? texture : Texture2D.whiteTexture;
            if (alphaByTexture.TryGetValue(key, out Material m) && m != null) return m;
            Shader urp = FindShader(UrpParticlesUnlit);
            m = alphaTemplate != null ? new Material(alphaTemplate) : new Material(urp != null ? urp : BestEffectShader());
            m.name = "Magic Alpha " + key.name;
            m.hideFlags = HideFlags.DontSave;
            ConfigureAlphaBlend(m);
            SetTexture(m, key);
            alphaByTexture[key] = m;
            return m;
        }

        /// <summary>The gold rim shell material (shared, tinted per renderer by property block). Null if the shader is missing.</summary>
        public static Material Rim()
        {
            if (rimMaterial != null) return rimMaterial;
            Shader s = FindShader(RimShaderName);
            if (s == null) return null;
            rimMaterial = new Material(s) { name = "Magic Gold Rim", hideFlags = HideFlags.DontSave, renderQueue = (int)RenderQueue.Transparent };
            return rimMaterial;
        }

        public static void SetTexture(Material m, Texture t)
        {
            if (m.HasProperty(MainTexId)) m.SetTexture(MainTexId, t);
            if (m.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, t);
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
        }

        public static void SetColor(MaterialPropertyBlock block, Color c)
        {
            block.SetColor(ColorId, c);
            block.SetColor(BaseColorId, c);
        }

        /// <summary>
        /// Sets a material up for additive transparent blending. Correct for the URP Particles/Unlit and Unlit
        /// shaders (which read the _Surface, _Blend, _SrcBlend, _DstBlend, _ZWrite and _Cull floats and the
        /// _SURFACE_TYPE_TRANSPARENT keyword, normally written by their inspector) and harmless for others.
        /// Editor builders can call it on a material asset to make a template.
        /// </summary>
        public static void ConfigureAdditive(Material m)
        {
            if (m == null) return;
            SetFloat(m, "_Surface", 1f);           // Transparent
            SetFloat(m, "_Blend", 2f);             // Additive
            SetFloat(m, "_BlendOp", (float)BlendOp.Add);
            SetFloat(m, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(m, "_DstBlend", (float)BlendMode.One);
            SetFloat(m, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloat(m, "_DstBlendAlpha", (float)BlendMode.One);
            SetFloat(m, "_ZWrite", 0f);
            SetFloat(m, "_Cull", (float)CullMode.Off);
            SetFloat(m, "_AlphaClip", 0f);
            SetFloat(m, "_ColorMode", 0f);         // Multiply vertex colour
            SetFloat(m, "_SoftParticlesEnabled", 0f);
            SetFloat(m, "_CameraFadingEnabled", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_SOFTPARTICLES_ON");
            m.DisableKeyword("_FADING_ON");
            m.DisableKeyword("_COLORADDSUBDIFF_ON");
            m.DisableKeyword("_COLOROVERLAY_ON");
            m.DisableKeyword("_COLORCOLOR_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            SetColor(m, Color.white);
        }

        /// <summary>Same as <see cref="ConfigureAdditive"/> but standard alpha blending (SrcAlpha, OneMinusSrcAlpha).</summary>
        public static void ConfigureAlphaBlend(Material m)
        {
            if (m == null) return;
            ConfigureAdditive(m);
            SetFloat(m, "_Blend", 0f);             // Alpha
            SetFloat(m, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(m, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        }

        private static void SetFloat(Material m, string name, float v)
        {
            if (m.HasProperty(name)) m.SetFloat(name, v);
        }

        /// <summary>A unit quad in the XY plane centred on the origin, front facing -Z, white vertex colour.</summary>
        public static Mesh Quad
        {
            get
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "Magic Quad", hideFlags = HideFlags.DontSave };
                quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
                quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
                quad.colors32 = new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) };
                quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                quad.RecalculateBounds();
                return quad;
            }
        }
    }
}
