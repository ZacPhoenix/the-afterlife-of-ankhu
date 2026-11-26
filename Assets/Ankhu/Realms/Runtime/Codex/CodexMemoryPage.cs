using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Realms
{
    /// <summary>
    /// Zac 2026-11-24, pick 7 "The Book paints it": before each Wernes memory film the Codex opens a papyrus page
    /// over the room and paints the memory's vignette onto it. A sepia underdrawing comes first, then colour goes on
    /// in brush bands behind a gold brush tip. The painting grows to the film's frame and washes to the cream that
    /// every memory film opens on, so the film takes over without a cut.
    /// Stills live in Resources/RealmCodex/MemoryStills/&lt;id&gt;.png (cut from each film at 2.5 s, letterbox removed).
    /// With no still the page is skipped and the film plays as before.
    /// </summary>
    public sealed class CodexMemoryPage : MonoBehaviour
    {
        /// <summary>Frame 0 of every memory film, sampled from the mp4s (253, 234, 202).</summary>
        public static readonly Color FilmCream = new Color(253f / 255f, 234f / 255f, 202f / 255f, 1f);

        private const int Bands = 7;
        private const float BandSeconds = 0.62f;
        private const float BandStagger = 0.3f;

        private Texture2D still;
        private RectTransform root, page, art;
        private Image dim, cream;
        private RawImage paper, under;
        private RawImage[] bands;
        private Image tip;
        private CanvasGroup pageGroup, ornaments;
        private Vector2 artSize;

        /// <summary>Opens the page for this memory, or returns null when there is no still for it.</summary>
        public static CodexMemoryPage Open(string id)
        {
            var tex = Resources.Load<Texture2D>("RealmCodex/MemoryStills/" + id);
            if (tex == null) return null;
            var go = new GameObject("CodexMemoryPage_" + id, typeof(RectTransform));
            DontDestroyOnLoad(go);
            var p = go.AddComponent<CodexMemoryPage>();
            p.still = tex;
            p.Build();
            return p;
        }

        private RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }

        private static void Centre(RectTransform r, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero; r.sizeDelta = size;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31990; // just under MemoryFlashback (32000), so the film covers it when it starts
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            root = (RectTransform)transform;

            dim = Child("Dim", root).gameObject.AddComponent<Image>();
            Stretch(dim.rectTransform);
            dim.color = new Color(CodexArt.Night.r, CodexArt.Night.g, CodexArt.Night.b, 0f);
            dim.raycastTarget = false;

            page = Child("Page", root);
            Centre(page, new Vector2(1300f, 680f));
            pageGroup = page.gameObject.AddComponent<CanvasGroup>();
            pageGroup.alpha = 0f;

            var shadow = Child("Shadow", page).gameObject.AddComponent<RawImage>();
            shadow.texture = CodexArt.Tex("book_shadow");
            shadow.color = new Color(0f, 0f, 0f, 0.7f);
            Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(-60f, -70f); shadow.rectTransform.offsetMax = new Vector2(60f, 40f);

            paper = Child("Papyrus", page).gameObject.AddComponent<RawImage>();
            paper.texture = CodexArt.Tex("papyrus_page");
            paper.color = Color.white;
            Stretch(paper.rectTransform);

            // The picture keeps the still's own aspect (the films' 2.39 picture, letterbox cut away).
            float aspect = (float)still.width / Mathf.Max(1, still.height);
            artSize = new Vector2(1120f, 1120f / aspect);
            art = Child("Art", root);
            Centre(art, artSize);

            under = Child("Underdrawing", art).gameObject.AddComponent<RawImage>();
            under.texture = still;
            Stretch(under.rectTransform);
            under.color = new Color(0.36f, 0.25f, 0.15f, 0f);

            bands = new RawImage[Bands];
            for (int i = 0; i < Bands; i++)
            {
                var b = Child("Band" + i, art).gameObject.AddComponent<RawImage>();
                b.texture = still;
                bool rtl = (i % 2) == 1; // the brush works back and forth down the page
                float v1 = 1f - (float)i / Bands, v0 = 1f - (float)(i + 1) / Bands;
                var r = b.rectTransform;
                r.anchorMin = new Vector2(rtl ? 1f : 0f, v0);
                r.anchorMax = new Vector2(rtl ? 1f : 0f, v1);
                r.pivot = new Vector2(rtl ? 1f : 0f, 0.5f);
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = new Vector2(0f, 0f);
                b.uvRect = new Rect(0f, v0, 0f, v1 - v0);
                b.color = new Color(1f, 0.86f, 0.62f, 0.92f);
                bands[i] = b;
            }

            var glow = Child("TipGlow", art).gameObject.AddComponent<RawImage>();
            glow.texture = CodexArt.Disc;
            glow.color = new Color(CodexArt.GoldTop.r, CodexArt.GoldTop.g, CodexArt.GoldTop.b, 0f);
            Centre(glow.rectTransform, new Vector2(120f, 120f));
            tip = Child("Tip", art).gameObject.AddComponent<Image>();
            tip.color = new Color(1f, 0.95f, 0.78f, 0f);
            Centre(tip.rectTransform, new Vector2(16f, 16f));
            tip.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
            glowTip = glow;

            var orn = Child("Ornaments", art);
            Stretch(orn);
            ornaments = orn.gameObject.AddComponent<CanvasGroup>();
            ornaments.alpha = 0f;
            Texture corner = CodexArt.Tex("corner_ornament");
            for (int c = 0; c < 4; c++)
            {
                var o = Child("Corner" + c, orn).gameObject.AddComponent<RawImage>();
                o.texture = corner;
                o.color = CodexArt.GoldInk;
                var r = o.rectTransform;
                float x = (c % 2 == 0) ? 0f : 1f, y = (c < 2) ? 1f : 0f;
                r.anchorMin = r.anchorMax = new Vector2(x, y);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(x == 0f ? -8f : 8f, y == 0f ? -8f : 8f);
                r.sizeDelta = new Vector2(84f, 84f);
                r.localScale = new Vector3(x == 0f ? 1f : -1f, y == 1f ? 1f : -1f, 1f);
            }
            // A thin gold rule around the picture, four strips.
            for (int e = 0; e < 4; e++)
            {
                var strip = Child("Rule" + e, orn).gameObject.AddComponent<Image>();
                strip.color = CodexArt.GoldInk;
                var r = strip.rectTransform;
                bool horiz = e < 2;
                float at = (e % 2 == 0) ? 0f : 1f;
                r.anchorMin = horiz ? new Vector2(0f, at) : new Vector2(at, 0f);
                r.anchorMax = horiz ? new Vector2(1f, at) : new Vector2(at, 1f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = horiz ? new Vector2(0f, at == 0f ? -5f : 5f) : new Vector2(at == 0f ? -5f : 5f, 0f);
                r.sizeDelta = horiz ? new Vector2(10f, 3f) : new Vector2(3f, 10f);
            }

            cream = Child("Cream", art).gameObject.AddComponent<Image>();
            Stretch(cream.rectTransform);
            cream.color = new Color(FilmCream.r, FilmCream.g, FilmCream.b, 0f);
        }

        private RawImage glowTip;

        private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>The page opens and the vignette is painted onto it. About four and a half seconds.</summary>
        public IEnumerator PaintIn()
        {
            float t0 = Time.unscaledTime;
            float paintStart = 0.75f;
            float bandsEnd = paintStart + 0.5f + (Bands - 1) * BandStagger + BandSeconds;
            float total = bandsEnd + 0.9f;
            while (true)
            {
                float t = Time.unscaledTime - t0;
                // Page rises out of the dark.
                float open = Ease(t / 0.7f);
                dim.color = new Color(dim.color.r, dim.color.g, dim.color.b, 0.78f * open);
                pageGroup.alpha = open;
                float s = Mathf.Lerp(0.9f, 1f, open);
                page.localScale = new Vector3(s, s, 1f);
                page.anchoredPosition = new Vector2(0f, Mathf.Lerp(-40f, 0f, open));
                art.localScale = page.localScale;
                art.anchoredPosition = page.anchoredPosition;

                // Sepia underdrawing settles in first.
                float ud = Ease((t - paintStart) / 0.9f);
                under.color = new Color(under.color.r, under.color.g, under.color.b, 0.55f * ud);

                // Colour in brush bands, each warming to true colour after the brush passes.
                int active = -1; float activeK = 0f;
                for (int i = 0; i < Bands; i++)
                {
                    float bs = paintStart + 0.5f + i * BandStagger;
                    float k = Mathf.Clamp01((t - bs) / BandSeconds);
                    float kk = 1f - Mathf.Pow(1f - k, 2.2f);
                    var b = bands[i];
                    bool rtl = (i % 2) == 1;
                    b.rectTransform.sizeDelta = new Vector2(artSize.x * kk, 2f); // a hair of overlap hides band seams
                    float v0 = 1f - (float)(i + 1) / Bands;
                    b.uvRect = rtl ? new Rect(1f - kk, v0, kk, 1f / Bands) : new Rect(0f, v0, kk, 1f / Bands);
                    float warm = Ease((t - bs - BandSeconds * 0.6f) / 1.1f);
                    b.color = Color.Lerp(new Color(1f, 0.84f, 0.6f, k > 0f ? 0.9f : 0f), Color.white, warm);
                    if (k > 0f && k < 1f) { active = i; activeK = kk; }
                }

                // The gold brush tip rides the leading edge of the band being painted.
                if (active >= 0)
                {
                    bool rtl = (active % 2) == 1;
                    float x = (rtl ? 1f - activeK : activeK) * artSize.x - artSize.x * 0.5f;
                    float y = artSize.y * (0.5f - (active + 0.5f) / Bands);
                    y += Mathf.Sin(Time.unscaledTime * 23f) * 6f;
                    tip.rectTransform.anchoredPosition = new Vector2(x, y);
                    glowTip.rectTransform.anchoredPosition = new Vector2(x, y);
                    tip.color = new Color(tip.color.r, tip.color.g, tip.color.b, 1f);
                    float pulse = 0.55f + 0.2f * Mathf.Sin(Time.unscaledTime * 11f);
                    glowTip.color = new Color(glowTip.color.r, glowTip.color.g, glowTip.color.b, pulse);
                }
                else
                {
                    tip.color = new Color(tip.color.r, tip.color.g, tip.color.b, Mathf.MoveTowards(tip.color.a, 0f, Time.unscaledDeltaTime * 4f));
                    glowTip.color = new Color(glowTip.color.r, glowTip.color.g, glowTip.color.b, Mathf.MoveTowards(glowTip.color.a, 0f, Time.unscaledDeltaTime * 3f));
                }

                ornaments.alpha = Ease((t - bandsEnd + 0.3f) / 0.6f);
                if (t >= total) break;
                yield return null;
            }
        }

        /// <summary>
        /// The painting grows to where the film's picture sits, the room goes black like the film's letterbox,
        /// and the picture washes to the film's opening cream. Then the film can start under nothing but cream.
        /// </summary>
        public IEnumerator GrowToFilm()
        {
            Rect cr = root.rect;
            float filmW = Mathf.Min(cr.width, cr.height * 16f / 9f);
            float scale = filmW / artSize.x;
            Vector3 s0 = art.localScale; Vector2 p0 = art.anchoredPosition;
            float t0 = Time.unscaledTime;
            const float grow = 1.1f, wash = 0.75f;
            while (true)
            {
                float t = Time.unscaledTime - t0;
                float g = Ease(t / grow);
                float sc = Mathf.Lerp(s0.x, scale, g);
                art.localScale = new Vector3(sc, sc, 1f);
                art.anchoredPosition = Vector2.Lerp(p0, Vector2.zero, g);
                page.localScale = art.localScale * 1.0f;
                page.anchoredPosition = art.anchoredPosition;
                pageGroup.alpha = 1f - Ease(t / (grow * 0.8f));
                ornaments.alpha = 1f - Ease(t / (grow * 0.6f));
                dim.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.78f, 1f, g));
                float w = Ease((t - grow * 0.75f) / wash);
                cream.color = new Color(FilmCream.r, FilmCream.g, FilmCream.b, w);
                if (t >= grow * 0.75f + wash) break;
                yield return null;
            }
        }

        /// <summary>Stays (cream) until the film's own overlay is fully up, then goes. Guarded so it never sticks.</summary>
        public IEnumerator HandOff(string id)
        {
            float guard = Time.unscaledTime + 6f;
            string name = "MemoryFlashback_" + id;
            while (Time.unscaledTime < guard)
            {
                var go = GameObject.Find(name);
                if (go == null) break; // the film failed or ended at once, nothing to hand to
                var g = go.GetComponent<CanvasGroup>();
                if (g != null && g.alpha >= 0.99f) break;
                yield return null;
            }
            yield return null;
            Destroy(gameObject);
        }
    }
}
