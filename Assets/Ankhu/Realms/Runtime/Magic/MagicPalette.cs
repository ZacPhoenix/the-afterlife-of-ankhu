using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>Colours of the words of power. UI colours are plain sRGB, world colours go through <see cref="Hdr"/>.</summary>
    public static class MagicPalette
    {
        /// <summary>Deep Egyptian gold, #F2C14E.</summary>
        public static readonly Color Gold = new Color(0.949f, 0.757f, 0.306f, 1f);
        /// <summary>Pale gold between gold and the white hot core.</summary>
        public static readonly Color GoldPale = new Color(1f, 0.89f, 0.62f, 1f);
        /// <summary>White hot core of a glowing sign.</summary>
        public static readonly Color WhiteHot = new Color(1f, 0.97f, 0.88f, 1f);
        /// <summary>Lapis lazuli, #1F3A93.</summary>
        public static readonly Color Lapis = new Color(0.122f, 0.227f, 0.576f, 1f);
        /// <summary>Lighter lapis for glows and hairlines.</summary>
        public static readonly Color LapisLight = new Color(0.30f, 0.45f, 0.95f, 1f);
        /// <summary>Bubble panel: obsidian with a lapis cast, translucent.</summary>
        public static readonly Color Obsidian = new Color(0.030f, 0.042f, 0.085f, 0.88f);
        /// <summary>Typed English.</summary>
        public static readonly Color Ink = new Color(0.96f, 0.92f, 0.84f, 1f);
        /// <summary>Placeholder and quiet reply text.</summary>
        public static readonly Color Quiet = new Color(0.78f, 0.75f, 0.69f, 0.82f);
        /// <summary>Failed words turn to this sand grey.</summary>
        public static readonly Color Sand = new Color(0.56f, 0.53f, 0.49f, 1f);

        /// <summary>
        /// A colour for Material.SetColor / MaterialPropertyBlock.SetColor whose rendered linear value is
        /// <paramref name="srgb"/> times <paramref name="intensity"/>. Unity converts colours from sRGB to linear
        /// per channel in a linear project, which would otherwise raise HDR values to the power 2.2.
        /// </summary>
        public static Color Hdr(Color srgb, float intensity, float alpha = 1f)
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                Color lin = srgb.linear * intensity;
                Color back = new Color(Mathf.LinearToGammaSpace(lin.r), Mathf.LinearToGammaSpace(lin.g), Mathf.LinearToGammaSpace(lin.b), alpha);
                return back;
            }
            return new Color(srgb.r * intensity, srgb.g * intensity, srgb.b * intensity, alpha);
        }
    }

    /// <summary>Easing curves, all mapping 0..1 to 0..1 (back and elastic overshoot).</summary>
    public static class Ease
    {
        public static float Clamp01(float t) => t < 0f ? 0f : (t > 1f ? 1f : t);
        public static float OutCubic(float t) { t = Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
        public static float InCubic(float t) { t = Clamp01(t); return t * t * t; }
        public static float InOutCubic(float t) { t = Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f; }
        public static float OutQuint(float t) { t = Clamp01(t); float u = 1f - t; return 1f - u * u * u * u * u; }
        public static float InQuad(float t) { t = Clamp01(t); return t * t; }
        public static float OutQuad(float t) { t = Clamp01(t); return 1f - (1f - t) * (1f - t); }
        public static float InOutSine(float t) { t = Clamp01(t); return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f; }
        public static float OutExpo(float t) { t = Clamp01(t); return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t); }

        /// <summary>Overshoots then settles. s = 1.70158 is the classic amount.</summary>
        public static float OutBack(float t, float s = 1.70158f)
        {
            t = Clamp01(t);
            float c3 = s + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + s * u * u;
        }

        /// <summary>Flight easing: a soft launch, a confident cruise and a quick arrival.</summary>
        public static float Flight(float t)
        {
            t = Clamp01(t);
            // Blend of in-out sine and a touch of ease-in at the end so the sign strikes the target.
            float a = InOutSine(t);
            float b = t * t;
            return Mathf.Lerp(a, b, t * 0.35f);
        }

        /// <summary>0 to 1 to 0 bell, peak at t = 0.5.</summary>
        public static float Bell(float t) { t = Clamp01(t); return Mathf.Sin(t * Mathf.PI); }

        /// <summary>Normalised progress of now between start and start + duration.</summary>
        public static float Progress(float now, float start, float duration)
        {
            if (duration <= 0f) return now >= start ? 1f : 0f;
            return Clamp01((now - start) / duration);
        }
    }
}
