using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Realms
{
    /// <summary>Small gold seal on an unread entry. Fades away once the entry is marked read.</summary>
    public sealed class CodexSeal : MonoBehaviour
    {
        public string EntryId;
        private RawImage image;
        private float alpha = -1f;
        private float phase;

        public static CodexSeal Attach(RawImage img, string id)
        {
            var s = img.gameObject.AddComponent<CodexSeal>();
            s.image = img;
            s.EntryId = id;
            s.phase = Random.value * 6f;
            return s;
        }

        private void OnEnable()
        {
            if (image == null) image = GetComponent<RawImage>();
            if (alpha < 0f) alpha = CodexStore.IsUnread(EntryId) ? 1f : 0f;
            Apply();
        }

        private void Update()
        {
            float target = CodexStore.IsUnread(EntryId) ? 1f : 0f;
            alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime * (target > alpha ? 3f : 0.6f));
            phase += Time.unscaledDeltaTime;
            Apply();
        }

        private void Apply()
        {
            if (image == null) return;
            float shine = 0.92f + 0.08f * Mathf.Sin(phase * 2.2f);
            image.color = new Color(shine, shine, shine, Mathf.Clamp01(alpha));
            image.enabled = alpha > 0.001f;
            float s = 1f + (1f - Mathf.Clamp01(alpha)) * 0.25f;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>
    /// Ink drying: an unread entry appears the first time its page is shown, darker and glossy, then settles.
    /// Read entries show at once.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CodexInkDry : MonoBehaviour
    {
        public string EntryId;
        public float Delay;
        public RawImage Bleed;
        private CanvasGroup group;
        private bool played;
        private float t = -1f;
        private const float Duration = 1.5f;

        public static CodexInkDry Attach(GameObject go, string id, float delay)
        {
            go.AddComponent<CanvasGroup>();
            var d = go.AddComponent<CodexInkDry>();
            d.EntryId = id;
            d.Delay = delay;
            return d;
        }

        private void OnEnable()
        {
            group = GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            if (!played && CodexStore.IsUnread(EntryId) && CodexBook.IsOpen)
            {
                played = true;
                t = -Delay;
                group.alpha = 0f;
            }
            else if (t < 0f || t >= Duration)
            {
                group.alpha = 1f;
                t = Duration;
                if (Bleed != null) Bleed.enabled = false;
            }
        }

        private void Update()
        {
            if (t >= Duration) return;
            if (t < 0f && !CodexBook.PagesVisible) return;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Duration);
            // Ink soaks in fast, then the gloss dries off slowly.
            group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 2.2f));
            if (Bleed != null)
            {
                Bleed.enabled = true;
                Bleed.color = new Color(CodexArt.Ink.r, CodexArt.Ink.g, CodexArt.Ink.b, 0.10f * Mathf.Sin(k * Mathf.PI));
                float s = Mathf.Lerp(0.6f, 1.15f, k);
                Bleed.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            if (k >= 1f && Bleed != null) Bleed.enabled = false;
        }
    }

    /// <summary>Easing helpers, unscaled time throughout the codex (gameplay may be paused).</summary>
    public static class CodexEase
    {
        public static float OutCubic(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 3f); }
        public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float InOutCubic(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f; }
        public static float OutBack(float t, float s = 1.4f) { t = Mathf.Clamp01(t) - 1f; return t * t * ((s + 1f) * t + s) + 1f; }
        public static float InQuad(float t) { t = Mathf.Clamp01(t); return t * t; }
        public static float OutQuad(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
    }
}
