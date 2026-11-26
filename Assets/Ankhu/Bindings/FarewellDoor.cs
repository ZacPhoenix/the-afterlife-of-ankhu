using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Bindings
{
    /// <summary>
    /// Zac 2026-11-24, pick 9 "A door closes between them": Anpu's farewell before Borrowed Dawn is a painted
    /// 2.5D beat. The approved concept (art_gen/next_area/out/anpu_B.png) is the plate: Anpu outside, Ankhu inside
    /// the great doors, looking back through the gap. The two carved leaves are cut out as their own layers and swing
    /// shut on their hinges (a leaf swung inward reads narrower, so closing widens it), stone-heavy, until they meet
    /// over Ankhu. Slow push-in, brazier flicker, drifting dust, then a thud with shake, a dust burst and a gold seam.
    /// Art: Resources/Farewell/farewell_plate.png, farewell_leaf_l.png, farewell_leaf_r.png (plate pixel space 1536x1024).
    /// Sorted under Anpu's subtitles (110) and the scene fader (1000).
    /// </summary>
    public sealed class FarewellDoor : MonoBehaviour
    {
        private const float PlateW = 1536f, PlateH = 1024f;
        // Leaf rects in plate pixels (x0, y0, x1, y1) and where they meet.
        private static readonly Rect LeafL = Rect.MinMaxRect(650f, 150f, 911f, 832f);
        private static readonly Rect LeafR = Rect.MinMaxRect(1036f, 150f, 1287f, 832f);
        private const float MeetX = 973f;
        private static readonly Vector2 Brazier = new Vector2(140f, 470f);

        private RectTransform stage, leafL, leafR, seam;
        private CanvasGroup group;
        private Image leafShadeL, leafShadeR, seamImg, black;
        private RawImage brazierGlow, gapLight;
        private Mote[] motes;
        private float shake;
        private float pushFrom = 1.08f, pushTo = 1.10f, pushStart;
        private static Texture2D soft;

        public bool Closing { get; private set; }
        public bool Closed { get; private set; }

        private struct Mote { public RectTransform r; public Image img; public Vector2 pos, vel; public float life, age, size; }

        public static FarewellDoor Open()
        {
            var plate = Resources.Load<Texture2D>("Farewell/farewell_plate");
            var l = Resources.Load<Texture2D>("Farewell/farewell_leaf_l");
            var r = Resources.Load<Texture2D>("Farewell/farewell_leaf_r");
            if (plate == null || l == null || r == null) return null;
            var go = new GameObject("FarewellDoor", typeof(RectTransform));
            var d = go.AddComponent<FarewellDoor>();
            d.Build(plate, l, r);
            return d;
        }

        private static Texture2D Soft
        {
            get
            {
                if (soft != null) return soft;
                const int n = 64;
                soft = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
                soft.SetPixels32(px);
                soft.Apply();
                return soft;
            }
        }

        private RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Places r in plate pixel space (top-left origin) with the given pivot.</summary>
        private static void PlacePx(RectTransform r, Rect px, Vector2 pivot)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = pivot;
            r.sizeDelta = new Vector2(px.width, px.height);
            r.anchoredPosition = new Vector2(px.xMin + pivot.x * px.width, -(px.yMin + (1f - pivot.y) * px.height));
        }

        private void Build(Texture2D plate, Texture2D l, Texture2D r)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            black = Child("Black", transform).gameObject.AddComponent<Image>();
            black.color = Color.black;
            var br = black.rectTransform; br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = br.offsetMax = Vector2.zero;

            stage = Child("Stage", transform);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = new Vector2(PlateW, PlateH);

            var p = Child("Plate", stage).gameObject.AddComponent<RawImage>();
            p.texture = plate;
            PlacePx(p.rectTransform, new Rect(0f, 0f, PlateW, PlateH), new Vector2(0.5f, 0.5f));

            // Cool light spilling from the corridor through the gap; it narrows with the doors.
            gapLight = Child("GapLight", stage).gameObject.AddComponent<RawImage>();
            gapLight.texture = Soft;
            gapLight.color = new Color(0.45f, 0.6f, 1f, 0.18f);
            PlacePx(gapLight.rectTransform, Rect.MinMaxRect(880f, 140f, 1066f, 850f), new Vector2(0.5f, 0.5f));

            brazierGlow = Child("BrazierGlow", stage).gameObject.AddComponent<RawImage>();
            brazierGlow.texture = Soft;
            brazierGlow.color = new Color(1f, 0.62f, 0.25f, 0.3f);
            PlacePx(brazierGlow.rectTransform, new Rect(Brazier.x - 220f, Brazier.y - 220f, 440f, 440f), new Vector2(0.5f, 0.5f));

            leafL = Child("LeafL", stage);
            leafL.gameObject.AddComponent<RawImage>().texture = l;
            PlacePx(leafL, LeafL, new Vector2(0f, 0.5f)); // hinge on the outer jamb
            leafShadeL = Child("Shade", leafL).gameObject.AddComponent<Image>();
            Fill(leafShadeL.rectTransform);
            leafShadeL.color = new Color(0f, 0f, 0f, 0f);

            leafR = Child("LeafR", stage);
            leafR.gameObject.AddComponent<RawImage>().texture = r;
            PlacePx(leafR, LeafR, new Vector2(1f, 0.5f));
            leafShadeR = Child("Shade", leafR).gameObject.AddComponent<Image>();
            Fill(leafShadeR.rectTransform);
            leafShadeR.color = new Color(0f, 0f, 0f, 0f);

            seamImg = Child("Seam", stage).gameObject.AddComponent<Image>();
            seam = seamImg.rectTransform;
            PlacePx(seam, Rect.MinMaxRect(MeetX - 3f, 150f, MeetX + 3f, 832f), new Vector2(0.5f, 0.5f));
            seamImg.color = new Color(1f, 0.85f, 0.45f, 0f);

            motes = new Mote[48];
            for (int i = 0; i < motes.Length; i++)
            {
                var m = new Mote();
                m.img = Child("Mote" + i, stage).gameObject.AddComponent<Image>();
                m.img.sprite = Sprite.Create(Soft, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
                m.r = m.img.rectTransform;
                m.r.anchorMin = m.r.anchorMax = new Vector2(0f, 1f);
                Respawn(ref m, true, false);
                motes[i] = m;
            }

            Fit();
            pushStart = Time.unscaledTime;
            StartCoroutine(FadeIn());
        }

        private static void Fill(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }

        private void Respawn(ref Mote m, bool anywhere, bool burst)
        {
            if (burst)
            {
                m.pos = new Vector2(MeetX + Random.Range(-40f, 40f), 828f + Random.Range(-6f, 4f));
                float side = Random.value < 0.5f ? -1f : 1f;
                m.vel = new Vector2(side * Random.Range(60f, 260f), Random.Range(-90f, -20f));
                m.life = Random.Range(1.2f, 2.4f);
                m.size = Random.Range(10f, 26f);
            }
            else
            {
                // Lazy dust in the warm air before the door.
                m.pos = new Vector2(Random.Range(520f, 1400f), anywhere ? Random.Range(200f, 820f) : 840f);
                m.vel = new Vector2(Random.Range(-6f, 6f), Random.Range(-22f, -8f));
                m.life = Random.Range(5f, 10f);
                m.size = Random.Range(3f, 7f);
            }
            m.age = anywhere ? Random.Range(0f, m.life) : 0f;
            m.r.sizeDelta = new Vector2(m.size, m.size);
        }

        /// <summary>Scales the stage so the plate covers the screen.</summary>
        private float CoverScale()
        {
            Rect cr = ((RectTransform)transform).rect;
            if (cr.width <= 0f) return 1.25f;
            return Mathf.Max(cr.width / PlateW, cr.height / PlateH);
        }

        private void Fit()
        {
            float push = Mathf.Lerp(pushFrom, pushTo, Mathf.Clamp01((Time.unscaledTime - pushStart) / 14f));
            float s = CoverScale() * push;
            stage.localScale = new Vector3(s, s, 1f);
            // Push toward the gap, a touch right of centre, with any shake on top.
            Vector2 focus = new Vector2((MeetX - PlateW * 0.5f) * (push - 1f) * -s, 0f);
            stage.anchoredPosition = focus + Random.insideUnitCircle * shake;
        }

        private IEnumerator FadeIn()
        {
            for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 1.4f);
                group.alpha = k * k * (3f - 2f * k);
                yield return null;
            }
            group.alpha = 1f;
        }

        private void Update()
        {
            Fit();
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 24f);

            float n = Mathf.PerlinNoise(Time.unscaledTime * 3.1f, 0.37f);
            float n2 = Mathf.PerlinNoise(Time.unscaledTime * 9.7f, 4.1f);
            brazierGlow.color = new Color(1f, 0.62f, 0.25f, 0.18f + 0.16f * n + 0.06f * n2);
            float bs = 1f + 0.06f * (n - 0.5f);
            brazierGlow.rectTransform.localScale = new Vector3(bs, bs, 1f);

            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < motes.Length; i++)
            {
                var m = motes[i];
                m.age += dt;
                if (m.age >= m.life)
                {
                    if (m.size > 9f && !Closed) { m.img.color = Color.clear; motes[i] = m; continue; } // spent burst grain
                    Respawn(ref m, false, false);
                }
                m.vel *= m.size > 9f ? (1f - 1.6f * dt) : 1f;
                m.pos += (m.vel + new Vector2(Mathf.Sin(Time.unscaledTime * 0.7f + i) * 4f, 0f)) * dt;
                float a = Mathf.Sin(Mathf.Clamp01(m.age / m.life) * Mathf.PI);
                bool burst = m.size > 9f;
                m.img.color = burst ? new Color(0.78f, 0.6f, 0.42f, 0.45f * a) : new Color(1f, 0.86f, 0.6f, 0.5f * a);
                m.r.anchoredPosition = new Vector2(m.pos.x, -m.pos.y);
                motes[i] = m;
            }
        }

        /// <summary>
        /// The leaves swing shut over <paramref name="seconds"/>: a slow heavy start, gathering speed, a hard stop.
        /// </summary>
        public IEnumerator Close(float seconds)
        {
            Closing = true;
            float targetL = (MeetX - LeafL.xMin) / LeafL.width;
            float targetR = (LeafR.xMax - MeetX) / LeafR.width;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / seconds);
                float e = k * k * k * 0.75f + k * 0.25f; // stone: barely moving, then all at once
                leafL.localScale = new Vector3(Mathf.Lerp(1f, targetL, e), 1f, 1f);
                leafR.localScale = new Vector3(Mathf.Lerp(1f, targetR, e), 1f, 1f);
                // Swinging toward the plane of the wall turns each leaf into the light.
                float shade = 0.22f * (1f - e);
                leafShadeL.color = new Color(0f, 0f, 0f, shade * Mathf.Clamp01(k * 6f));
                leafShadeR.color = new Color(0f, 0f, 0f, shade * Mathf.Clamp01(k * 6f));
                gapLight.color = new Color(0.45f, 0.6f, 1f, 0.18f + 0.22f * e * (1f - e) * 4f * (1f - k));
                gapLight.rectTransform.localScale = new Vector3(Mathf.Lerp(1f, 0.15f, e), 1f, 1f);
                if (k > 0.5f) shake = Mathf.Max(shake, 1.5f * k); // grinding
                yield return null;
            }
            leafL.localScale = new Vector3(targetL, 1f, 1f);
            leafR.localScale = new Vector3(targetR, 1f, 1f);
            leafShadeL.color = leafShadeR.color = new Color(0f, 0f, 0f, 0f);
            gapLight.color = new Color(0f, 0f, 0f, 0f);
            Closed = true;
            shake = 14f;
            for (int i = 0; i < motes.Length; i++)
            {
                if (i % 3 == 0) continue; // keep some air dust
                var m = motes[i];
                Respawn(ref m, false, true);
                motes[i] = m;
            }
            Closing = false;
            for (float t = 0f; t < 2.2f; t += Time.unscaledDeltaTime)
            {
                float k = t / 2.2f;
                seamImg.color = new Color(1f, 0.85f, 0.45f, (t < 0.12f ? t / 0.12f : 1f) * (1f - k) * (1f - k));
                seam.localScale = new Vector3(1f + 2f * (1f - k), 1f, 1f);
                yield return null;
            }
            seamImg.color = new Color(1f, 0.85f, 0.45f, 0f);
        }
    }
}
