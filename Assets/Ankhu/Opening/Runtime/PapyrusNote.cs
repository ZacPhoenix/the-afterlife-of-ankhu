using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Opening
{
    /// <summary>
    /// Zac 2026-11-25: one shared on-screen note in the painted papyrus look of the speech bubble (the torn sheet,
    /// the red and black ruled frame, carbon ink words, keys in rubric red). Used for control hints such as
    /// "Use W A S D to walk." Lives in Ankhu.Opening so the opening and the realms can both call it, and it reads
    /// the same Resources textures as the bubble's Painted skin, so a UI restyle of those textures carries over.
    /// </summary>
    public sealed class PapyrusNote : MonoBehaviour
    {
        public const string ResourceFolder = "RealmGlyphs/";
        public static readonly Color Ink = new Color(0.125f, 0.086f, 0.055f, 1f);
        public static readonly Color Rubric = new Color(0.59f, 0.18f, 0.10f, 1f);

        private const float CornerUnits = 30f;
        private const float PadX = 64f;
        private const float Height = 96f;

        private CanvasGroup group;
        private RectTransform sheet;
        private Text label;
        private bool dismissing;

        /// <summary>
        /// Shows a note centred above the subtitle line. Wrap keys in [ ] to set them in rubric red,
        /// for example "Use [W A S D] to walk." Returns the note so the caller can dismiss it.
        /// </summary>
        public static PapyrusNote Show(string text, int order = 210)
        {
            Canvas c = UiKit.Canvas("PapyrusNote", order);
            var note = c.gameObject.AddComponent<PapyrusNote>();
            note.Build(c.transform, text);
            return note;
        }

        /// <summary>Fades the note out and removes it.</summary>
        public void Dismiss(float seconds = 0.9f)
        {
            if (dismissing || this == null) return;
            dismissing = true;
            StartCoroutine(Fade(0f, seconds, true));
        }

        private void Build(Transform root, string text)
        {
            sheet = UiKit.Rect("Sheet", root);
            group = sheet.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Sprite paper = Sliced("ui_papyrus");
            Sprite frame = Sliced("ui_papyrus_frame");
            bool painted = paper != null;
            if (painted)
            {
                AddSliced("Paper", sheet, paper);
                if (frame != null) AddSliced("Frame", sheet, frame);
            }
            else
            {
                Image fallback = UiKit.Solid("Panel", sheet, new Color(0.02f, 0.025f, 0.045f, 0.72f));
                UiKit.Stretch(fallback.rectTransform);
            }

            Color ink = painted ? Ink : UiKit.Parchment;
            Color keys = painted ? Rubric : UiKit.Gold;
            float w;
            if (painted && text.IndexOf('[') >= 0)
            {
                // Zac 2026-11-25 puzzle UI picks: keys sit on small papyrus keycaps, one cap per key, in the
                // same look as the realm ledger and prompts.
                w = Mathf.Max(320f, BuildKeyLine(sheet, text, ink, paper, frame) + PadX * 2f);
            }
            else
            {
                string rich = text.Replace("[", "<b><color=#" + ColorUtility.ToHtmlStringRGB(keys) + ">").Replace("]", "</color></b>");
                label = UiKit.Label("Line", sheet, rich, UiKit.Serif, 36, ink, TextAnchor.MiddleCenter);
                label.supportRichText = true;
                UiKit.Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(PadX * 0.5f, 4f);
                label.rectTransform.offsetMax = new Vector2(-PadX * 0.5f, 0f);
                w = Mathf.Max(320f, label.preferredWidth + PadX * 2f);
            }
            UiKit.Place(sheet, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(w, Height));
            StartCoroutine(Fade(1f, 0.6f, false));
        }

        private static void AddSliced(string name, RectTransform parent, Sprite s, float corner = CornerUnits)
        {
            Image img = UiKit.Solid(name, parent, Color.white);
            img.sprite = s;
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            // Same corner scale as the speech bubble: the sliced border shows at 30 canvas units.
            img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, s.border.x / corner);
            UiKit.Stretch(img.rectTransform);
        }

        /// <summary>
        /// Lays "Use [W A S D] to walk." out as ink words and one papyrus keycap per key, centred in the sheet.
        /// Returns the line width.
        /// </summary>
        private float BuildKeyLine(RectTransform parent, string text, Color ink, Sprite paper, Sprite frame)
        {
            const float CapH = 46f, Gap = 7f, Space = 12f;
            var items = new System.Collections.Generic.List<RectTransform>();
            var widths = new System.Collections.Generic.List<float>();
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('[', i);
                string words = (open < 0 ? text.Substring(i) : text.Substring(i, open - i)).Trim();
                if (words.Length > 0)
                {
                    Text t = UiKit.Label("Words", parent, words, UiKit.Serif, 36, ink, TextAnchor.MiddleCenter);
                    float tw = t.preferredWidth;
                    items.Add(t.rectTransform); widths.Add(tw);
                }
                if (open < 0) break;
                int close = text.IndexOf(']', open);
                if (close < 0) close = text.Length;
                foreach (string key in text.Substring(open + 1, close - open - 1).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    RectTransform cap = UiKit.Rect("Key " + key, parent);
                    AddSliced("Cap", cap, paper, 9f);
                    if (frame != null) AddSliced("CapFrame", cap, frame, 9f);
                    Text k = UiKit.Label("Label", cap, key, UiKit.Serif, 28, Rubric, TextAnchor.MiddleCenter);
                    k.fontStyle = FontStyle.Bold;
                    UiKit.Stretch(k.rectTransform);
                    float cw = Mathf.Max(CapH, k.preferredWidth + 22f);
                    items.Add(cap); widths.Add(cw);
                }
                i = close + 1;
            }
            float total = 0f;
            for (int n = 0; n < widths.Count; n++) total += widths[n] + (n + 1 < widths.Count ? Gap + (IsCap(items[n]) && IsCap(items[n + 1]) ? 0f : Space) : 0f);
            float x = -total * 0.5f;
            for (int n = 0; n < items.Count; n++)
            {
                RectTransform r = items[n];
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(widths[n], IsCap(r) ? CapH : 50f);
                r.anchoredPosition = new Vector2(x + widths[n] * 0.5f, 2f);
                x += widths[n] + Gap + (n + 1 < items.Count && IsCap(r) && IsCap(items[n + 1]) ? 0f : Space);
            }
            return total;
        }

        private static bool IsCap(RectTransform r) => r.name.StartsWith("Key ");

        private static Sprite Sliced(string name)
        {
            var tex = Resources.Load<Texture2D>(ResourceFolder + name);
            if (tex == null) return null;
            // Borders are authored at 72 px on a 256 tall source. Scale them if Unity resized the texture on import.
            float b = Mathf.Min(72f * tex.height / 256f, Mathf.Min(tex.width, tex.height) * 0.49f);
            Sprite s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            s.name = name;
            s.hideFlags = HideFlags.DontSave;
            return s;
        }

        private IEnumerator Fade(float to, float seconds, bool destroyAfter)
        {
            float from = group.alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                group.alpha = Mathf.Lerp(from, to, k);
                sheet.anchoredPosition = new Vector2(0f, 250f + (to > 0f ? -8f * (1f - k) : 6f * k));
                yield return null;
            }
            group.alpha = to;
            if (destroyAfter) Destroy(gameObject);
        }
    }
}
