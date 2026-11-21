using System;
using System.Collections.Generic;
using Ankhu.Opening;
using Ankhu.Realms.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Realms
{
    /// <summary>
    /// "Written in the codex" notifications. Any new entry (book open or closed) gets a toast at the right edge:
    /// reed pen, the entry title, its glyphs, and a hint to read it. Up to three stack, the rest queue.
    /// A word of power gets a larger ceremonial toast at the top centre with shimmering glyphs, shown alone.
    /// Lives on the <see cref="CodexBook"/> object, created by <see cref="CodexBook.Ensure"/>.
    /// </summary>
    public sealed class CodexToast : MonoBehaviour
    {
        /// <summary>Raised when a toast appears (hook the scribble or gong sound here).</summary>
        public static event Action<CodexEntry> ToastShown;

        /// <summary>Hold new toasts in the queue (cutscenes, loading). They show once this is cleared.</summary>
        public static bool Suppressed { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ToastShown = null;
            Suppressed = false;
        }

        private const float SmallW = 600f;
        private const float SmallH = 116f;
        private const float SmallGap = 12f;
        private const float SmallTop = 170f;
        private const float SmallHold = 4.6f;
        private const float GrandHold = 5.2f;
        private const int MaxStack = 3;

        private sealed class Live
        {
            public CodexEntry Entry;
            public RectTransform Root;
            public CanvasGroup Group;
            public RawImage Icon;
            public Image LineTop, LineBottom;
            public CodexArt.GlyphGroup Glyphs;
            public float Age;
            public float Hold;
            public float Y;
            public bool Grand;
        }

        private RectTransform layer;
        private readonly Queue<CodexEntry> pending = new Queue<CodexEntry>();
        private readonly List<Live> live = new List<Live>();
        private Live grand;
        private float spawnCooldown;

        public void Init(RectTransform toastLayer)
        {
            layer = toastLayer;
        }

        private void OnEnable() { CodexStore.Written += Enqueue; }
        private void OnDisable() { CodexStore.Written -= Enqueue; }

        public void Enqueue(CodexEntry e)
        {
            if (e != null) pending.Enqueue(e);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            spawnCooldown -= dt;
            if (!Suppressed && pending.Count > 0 && grand == null && spawnCooldown <= 0f)
            {
                CodexEntry next = pending.Peek();
                if (next.Kind == CodexKind.Word)
                {
                    // A word of power waits for the smaller toasts to clear, then has the stage alone.
                    if (live.Count == 0)
                    {
                        pending.Dequeue();
                        grand = BuildGrand(next);
                        ToastShown?.Invoke(next);
                    }
                }
                else if (live.Count < MaxStack)
                {
                    pending.Dequeue();
                    Live t = BuildSmall(next);
                    live.Add(t);
                    spawnCooldown = 0.35f;
                    ToastShown?.Invoke(next);
                }
            }

            for (int i = live.Count - 1; i >= 0; i--)
            {
                Live t = live[i];
                t.Age += dt;
                if (t.Age > t.Hold + 0.5f)
                {
                    Destroy(t.Root.gameObject);
                    live.RemoveAt(i);
                }
            }
            for (int i = 0; i < live.Count; i++) AnimateSmall(live[i], i, dt);

            if (grand != null)
            {
                grand.Age += dt;
                AnimateGrand(grand);
                if (grand.Age > grand.Hold + 0.8f)
                {
                    Destroy(grand.Root.gameObject);
                    grand = null;
                    spawnCooldown = 0.25f;
                }
            }
        }

        // ------------------------------------------------------------------ small toast

        private Live BuildSmall(CodexEntry e)
        {
            var t = new Live { Entry = e, Hold = SmallHold };
            RectTransform r = CodexArt.Rect("Toast_" + e.Id, layer);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
            r.sizeDelta = new Vector2(SmallW, SmallH);
            t.Root = r;
            t.Group = r.gameObject.AddComponent<CanvasGroup>();
            t.Group.blocksRaycasts = false;
            t.Group.alpha = 0f;
            t.Y = -SmallTop - live.Count * (SmallH + SmallGap);
            r.anchoredPosition = new Vector2(80f, t.Y);

            RawImage band = CodexArt.Raw("Band", r, CodexArt.Tex("toast_band"), Color.white);
            CodexPages.UiStretch(band.rectTransform);
            t.LineTop = Hairline(r, true);
            t.LineBottom = Hairline(r, false);

            const float right = 44f;
            const float iconS = 58f;
            float textX = 190f;
            t.Icon = CodexArt.Raw("Reed", r, CodexArt.Tex("icon_reed"), CodexArt.Gold);
            RectTransform ir = t.Icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.anchoredPosition = new Vector2(textX - 46f, 2f);
            ir.sizeDelta = new Vector2(iconS, iconS);
            CodexArt.Shadow(t.Icon, new Color(0f, 0f, 0f, 0.6f), new Vector2(1f, -2f));

            float tw = SmallW - textX - right;
            string kind = CodexArt.KindLabel(e.Kind);
            Text line1 = CodexArt.Label("Written", r, "WRITTEN IN THE CODEX   ·   " + kind, CodexArt.Caps, 14, CodexArt.GoldDim, TextAnchor.UpperLeft);
            CodexArt.TopLeft(line1.rectTransform, textX, 16f, tw, 20f);
            Text title = CodexArt.Label("Title", r, e.Title, CodexArt.Body, 31, CodexArt.Parchment, TextAnchor.UpperLeft);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            CodexArt.TopLeft(title.rectTransform, textX, 36f, tw, 40f);
            CodexArt.Shadow(title, new Color(0f, 0f, 0f, 0.7f), new Vector2(1f, -1.5f));

            // Glyphs, then the hint, on the last line.
            RectTransform glyphRow = CodexArt.TopLeft(CodexArt.Rect("Glyphs", r), textX, 80f, 160f, 26f);
            t.Glyphs = CodexArt.Glyphs(glyphRow, e.Signs, 24f, CodexArt.Gold, 150f);
            t.Glyphs.Root.anchorMin = t.Glyphs.Root.anchorMax = new Vector2(0f, 0.5f);
            t.Glyphs.Root.pivot = new Vector2(0f, 0.5f);
            t.Glyphs.Root.anchoredPosition = Vector2.zero;
            Hint(r, textX + (t.Glyphs.Width > 0f ? t.Glyphs.Width + 22f : 0f), 93f);
            return t;
        }

        private static Image Hairline(RectTransform r, bool top)
        {
            Image l = CodexArt.Solid(top ? "LineTop" : "LineBottom", r, CodexArt.Gold);
            RectTransform lr = l.rectTransform;
            lr.anchorMin = new Vector2(0f, top ? 1f : 0f);
            lr.anchorMax = new Vector2(1f, top ? 1f : 0f);
            lr.pivot = new Vector2(1f, 0.5f);
            lr.sizeDelta = new Vector2(0f, 1.2f);
            lr.anchoredPosition = new Vector2(0f, top ? -6f : 6f);
            CodexGradient.Add(l, new Color(1, 1, 1, 0f), new Color(1, 1, 1, 0.85f), false);
            return l;
        }

        private static void Hint(RectTransform r, float x, float centerY)
        {
            RectTransform row = CodexArt.Rect("Hint", r);
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 0.5f);
            row.anchoredPosition = new Vector2(x, -centerY);
            row.sizeDelta = new Vector2(200f, 22f);
            float kw = CodexChips.Key(row, CodexBook.GamepadMode ? "VIEW" : "TAB", 0f, 21f);
            Text t = CodexArt.Label("Label", row, "to read", CodexArt.Body, 18, CodexArt.GoldDim, TextAnchor.MiddleLeft, FontStyle.Italic);
            RectTransform tr = t.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 0.5f);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.anchoredPosition = new Vector2(kw + 8f, 0f);
            tr.sizeDelta = new Vector2(120f, 24f);
        }

        private static void AnimateSmall(Live t, int index, float dt)
        {
            float target = -SmallTop - index * (SmallH + SmallGap);
            t.Y = Mathf.Lerp(t.Y, target, 1f - Mathf.Exp(-dt * 10f));
            float inK = CodexEase.OutCubic(t.Age / 0.5f);
            float outK = Mathf.Clamp01((t.Age - t.Hold) / 0.5f);
            float x = Mathf.Lerp(90f, -14f, inK) + CodexEase.InCubic(outK) * 70f;
            t.Root.anchoredPosition = new Vector2(x, t.Y);
            t.Group.alpha = inK * (1f - outK);
            // Hairlines draw in from the right, as if inked.
            float line = CodexEase.OutCubic((t.Age - 0.1f) / 0.7f);
            t.LineTop.rectTransform.localScale = new Vector3(line, 1f, 1f);
            t.LineBottom.rectTransform.localScale = new Vector3(line, 1f, 1f);
            // The reed nods while it writes.
            float nod = t.Age < 1.2f ? Mathf.Sin(t.Age * 14f) * 6f * (1f - t.Age / 1.2f) : 0f;
            t.Icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, nod);
            for (int i = 0; i < t.Glyphs.Signs.Count; i++)
            {
                float k = Mathf.Clamp01((t.Age - 0.35f - i * 0.10f) / 0.35f);
                Color c = t.Glyphs.Signs[i].color;
                c.a = k;
                t.Glyphs.Signs[i].color = c;
            }
        }

        // ------------------------------------------------------------------ ceremonial toast (words of power)

        private Live BuildGrand(CodexEntry e)
        {
            var t = new Live { Entry = e, Hold = GrandHold, Grand = true };
            RectTransform r = CodexArt.Rect("WordOfPower_" + e.Id, layer);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(0f, -250f);
            r.sizeDelta = new Vector2(1000f, 360f);
            t.Root = r;
            t.Group = r.gameObject.AddComponent<CanvasGroup>();
            t.Group.blocksRaycasts = false;
            t.Group.alpha = 0f;

            RawImage wash = CodexArt.Raw("Wash", r, UiKit.SoftDot(64, 1.2f), new Color(0.02f, 0.012f, 0.006f, 0.82f));
            CodexArt.Centered(wash.rectTransform, new Vector2(0f, -6f), new Vector2(1300f, 520f));
            RawImage warmth = CodexArt.Raw("Warmth", r, UiKit.SoftDot(64, 2f), new Color(1f, 0.70f, 0.30f, 0.10f));
            CodexArt.Centered(warmth.rectTransform, new Vector2(0f, 70f), new Vector2(560f, 300f));

            Text label = CodexArt.Label("Label", r, "A WORD OF POWER", CodexArt.Caps, 18, CodexArt.GoldDim, TextAnchor.MiddleCenter);
            CodexArt.Centered(label.rectTransform, new Vector2(0f, 150f), new Vector2(600f, 26f));
            t.LineTop = GrandRule(r, true);
            t.LineBottom = GrandRule(r, false);

            RectTransform gHolder = CodexArt.Rect("Glyphs", r);
            CodexArt.Centered(gHolder, new Vector2(0f, 82f), new Vector2(700f, 90f));
            t.Glyphs = CodexArt.Glyphs(gHolder, e.Signs, 86f, CodexArt.Gold, 640f, true, new Color(1f, 0.82f, 0.42f, 0f));
            t.Glyphs.Root.anchorMin = t.Glyphs.Root.anchorMax = new Vector2(0.5f, 0.5f);
            t.Glyphs.Root.anchoredPosition = Vector2.zero;
            foreach (RawImage s in t.Glyphs.Signs) CodexGradient.Add(s, new Color(1.1f, 1.08f, 1.02f, 1f), new Color(0.80f, 0.66f, 0.45f, 1f), true);

            Text title = CodexArt.Label("Title", r, (e.Title ?? string.Empty).ToUpperInvariant(), CodexArt.Caps, 66, CodexArt.Gold, TextAnchor.MiddleCenter);
            CodexArt.Centered(title.rectTransform, new Vector2(0f, -4f), new Vector2(900f, 80f));
            CodexGradient.Add(title, new Color(1.12f, 1.1f, 1.05f, 1f), new Color(0.78f, 0.62f, 0.40f, 1f), true);
            CodexArt.Shadow(title, new Color(0f, 0f, 0f, 0.75f), new Vector2(2f, -3f));

            string sub = string.IsNullOrEmpty(e.Transliteration) ? string.Empty : e.Transliteration;
            Text tr = CodexArt.Label("Translit", r, sub, CodexArt.Body, 27, CodexArt.GoldDim, TextAnchor.MiddleCenter, FontStyle.Italic);
            CodexArt.Centered(tr.rectTransform, new Vector2(0f, -56f), new Vector2(600f, 34f));
            Text effect = CodexArt.Label("Effect", r, e.Effect, CodexArt.Body, 25, CodexArt.Parchment, TextAnchor.MiddleCenter, FontStyle.Italic);
            CodexArt.Centered(effect.rectTransform, new Vector2(0f, -98f), new Vector2(860f, 60f));
            CodexArt.Shadow(effect, new Color(0f, 0f, 0f, 0.7f), new Vector2(1f, -1.5f));

            RectTransform hintRow = CodexArt.Rect("HintRow", r);
            CodexArt.Centered(hintRow, new Vector2(0f, -146f), new Vector2(160f, 24f));
            hintRow.anchorMin = hintRow.anchorMax = new Vector2(0.5f, 0.5f);
            Hint(hintRow, 18f, 12f);
            return t;
        }

        private static Image GrandRule(RectTransform r, bool left)
        {
            Image l = CodexArt.Solid(left ? "RuleLeft" : "RuleRight", r, CodexArt.Gold);
            RectTransform lr = l.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(left ? 1f : 0f, 0.5f);
            lr.anchoredPosition = new Vector2(left ? -140f : 140f, 150f);
            lr.sizeDelta = new Vector2(260f, 1.2f);
            CodexGradient.Add(l, left ? new Color(1, 1, 1, 0f) : Color.white, left ? Color.white : new Color(1, 1, 1, 0f), false);
            return l;
        }

        private static void AnimateGrand(Live t)
        {
            float a = t.Age;
            float inK = CodexEase.OutCubic(a / 0.7f);
            float outK = Mathf.Clamp01((a - t.Hold) / 0.8f);
            t.Group.alpha = inK * (1f - CodexEase.InQuad(outK));
            float s = Mathf.Lerp(1.10f, 1f, inK) + outK * 0.04f;
            t.Root.localScale = new Vector3(s, s, 1f);
            float rule = CodexEase.OutCubic((a - 0.2f) / 0.9f);
            t.LineTop.rectTransform.localScale = new Vector3(rule, 1f, 1f);
            t.LineBottom.rectTransform.localScale = new Vector3(rule, 1f, 1f);

            // Signs appear one by one with a flare, then shimmer in a slow wave.
            int n = t.Glyphs.Signs.Count;
            for (int i = 0; i < n; i++)
            {
                float start = 0.25f + i * 0.16f;
                float k = Mathf.Clamp01((a - start) / 0.45f);
                RawImage sign = t.Glyphs.Signs[i];
                Color c = sign.color;
                c.a = CodexEase.OutQuad(k);
                sign.color = c;
                float pop = 1f + 0.25f * (1f - CodexEase.OutBack(k, 2f));
                sign.rectTransform.localScale = new Vector3(pop, pop, 1f);
            }
            for (int i = 0; i < t.Glyphs.Glows.Count; i++)
            {
                float start = 0.25f + i * 0.16f;
                float k = Mathf.Clamp01((a - start) / 0.45f);
                float flare = Mathf.Sin(k * Mathf.PI) * 0.9f;
                float wave = k >= 1f ? 0.30f + 0.30f * Mathf.Sin(a * 3.1f - i * 0.9f) : 0f;
                RawImage g = t.Glyphs.Glows[i];
                Color c = g.color;
                c.a = Mathf.Clamp01(flare + wave);
                g.color = c;
                float gs = 1f + 0.06f * Mathf.Sin(a * 2.3f - i * 0.9f);
                g.rectTransform.localScale = new Vector3(gs, gs, 1f);
            }
        }
    }
}
