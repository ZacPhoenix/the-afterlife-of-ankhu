using UnityEngine;

namespace Ankhu.World
{
    /// <summary>
    /// Flat greybox colours. The base set is black, deep blue, gold, red and white plus neutral
    /// greys. Cyan marks water and violet marks binding threads.
    /// </summary>
    public static class GreyboxPalette
    {
        public static readonly Color Black = new Color(0.05f, 0.05f, 0.06f, 1f);
        public static readonly Color DeepBlue = new Color(0.07f, 0.10f, 0.22f, 1f);
        public static readonly Color Gold = new Color(0.92f, 0.73f, 0.26f, 1f);
        public static readonly Color GoldDim = new Color(0.52f, 0.42f, 0.18f, 1f);
        public static readonly Color Red = new Color(0.78f, 0.18f, 0.15f, 1f);
        public static readonly Color White = new Color(1f, 1f, 1f, 1f);

        public static readonly Color GreyDark = new Color(0.22f, 0.22f, 0.24f, 1f);
        public static readonly Color Stone = new Color(0.46f, 0.46f, 0.48f, 1f);
        public static readonly Color StoneLight = new Color(0.68f, 0.68f, 0.70f, 1f);
        public static readonly Color UpperSlab = new Color(0.60f, 0.55f, 0.48f, 1f);
        public static readonly Color BackWall = new Color(0.17f, 0.19f, 0.27f, 1f);

        public static readonly Color Bronze = new Color(0.55f, 0.40f, 0.22f, 1f);
        public static readonly Color BronzeDull = new Color(0.38f, 0.30f, 0.20f, 1f);
        public static readonly Color Water = new Color(0.20f, 0.85f, 0.92f, 0.45f);
        public static readonly Color Violet = new Color(0.64f, 0.32f, 0.96f, 1f);
        public static readonly Color ShadowFigure = new Color(0.02f, 0.02f, 0.04f, 0.85f);
        public static readonly Color BeamCore = new Color(1f, 1f, 1f, 1f);
        public static readonly Color BeamGlow = new Color(0.98f, 0.80f, 0.35f, 0.40f);
        public static readonly Color SunShaft = new Color(1f, 0.86f, 0.50f, 0.22f);
        public static readonly Color DawnBeyond = new Color(1f, 0.83f, 0.55f, 1f);

        public static Color WithAlpha(Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }
    }

    /// <summary>
    /// Depth plan for the 3D greybox. Gameplay physics is Physics2D in the XY plane and the
    /// player walks at z = 0. Positive z is further from the camera.
    /// </summary>
    public static class GreyboxDepth
    {
        public const float PlayerZ = 0f;
        public const float BackWallZ = 3.25f;
        public const float BackWallDepth = 0.5f;
        public const float WalkCentreZ = 0.5f;
        public const float WalkDepth = 3f;
        /// <summary>Plane for props, beam and statues. Behind the player so nothing hides Ankhu.</summary>
        public const float PropZ = 1.2f;
        public const float BeamZ = 1.2f;
        public const float ForegroundZ = -2f;
    }

    /// <summary>Sorting orders, used by LineRenderers and any 2D renderers added later.</summary>
    public static class GreyboxSorting
    {
        public const int BackWall = -100;
        public const int Props = -5;
        public const int Structure = 0;
        public const int Beam = 5;
        public const int Indicator = 6;
        public const int Player = 10;
    }

    /// <summary>Shader lookup and per-renderer tinting shared by the runtime views and the builder.</summary>
    public static class GreyboxRender
    {
        public const string LitShader = "Universal Render Pipeline/Lit";
        public const string LitFallbackShader = "Standard";
        public const string UnlitShader = "Universal Render Pipeline/Unlit";
        public const string UnlitFallbackShader = "Sprites/Default";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static MaterialPropertyBlock block;

        public static Shader FindLitShader()
        {
            return FindWithFallback(LitShader, LitFallbackShader);
        }

        public static Shader FindUnlitShader()
        {
            return FindWithFallback(UnlitShader, UnlitFallbackShader);
        }

        private static Shader FindWithFallback(string preferred, string fallback)
        {
            Shader shader = Shader.Find(preferred);
            if (shader != null)
            {
                return shader;
            }
            shader = Shader.Find(fallback);
            if (shader != null)
            {
                return shader;
            }
            Debug.LogError($"Ankhu greybox: neither '{preferred}' nor '{fallback}' shader was found. Check the render pipeline setup.");
            return null;
        }

        /// <summary>
        /// Tints one renderer at runtime without touching the shared material asset. Writes both
        /// _BaseColor (URP) and _Color (built-in and sprite shaders).
        /// </summary>
        public static void Tint(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        public static void SetVisible(Renderer renderer, bool visible)
        {
            if (renderer != null && renderer.enabled != visible)
            {
                renderer.enabled = visible;
            }
        }

        public static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        /// <summary>Smoothstep easing used by every mover.</summary>
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
