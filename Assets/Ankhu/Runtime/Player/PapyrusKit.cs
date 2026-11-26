using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Player
{
    /// <summary>
    /// Zac 2026-11-25, puzzle UI pick A "Papyrus and ink": every panel is torn papyrus with the red and black
    /// ruled border, carbon ink words and rubric red for refusals and keys. One shared kit so the teaching rooms
    /// (built here in Ankhu.Runtime) and the realms (Ankhu.Realms, which references this assembly) draw the
    /// same sheet, keycap and glyph icon. Reads the same Resources textures as the speech bubble's Painted skin.
    /// </summary>
    public static class PapyrusKit
    {
        public const string Folder = "RealmGlyphs/";
        public const float CornerUnits = 30f;

        public static readonly Color Ink = new Color(0.125f, 0.086f, 0.055f, 1f);
        public static readonly Color InkSoft = new Color(0.30f, 0.22f, 0.14f, 1f);
        public static readonly Color InkFaint = new Color(0.36f, 0.27f, 0.17f, 0.55f);
        public static readonly Color Rubric = new Color(0.59f, 0.18f, 0.10f, 1f);
        public static readonly Color Gold = new Color(0.80f, 0.55f, 0.12f, 1f);
        public static readonly Color GoldBright = new Color(1f, 0.80f, 0.36f, 1f);
        public static readonly Color Wax = new Color(0.55f, 0.10f, 0.07f, 1f);

        private static Font serif;
        private static Font caps;
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Sprites.Clear();

        public static Font Serif => serif != null ? serif : (serif = Font.CreateDynamicFontFromOSFont(new[] { "Cormorant Garamond", "Palatino Linotype", "Book Antiqua", "Georgia" }, 48));
        public static Font Caps => caps != null ? caps : (caps = Font.CreateDynamicFontFromOSFont(new[] { "Cinzel", "Palatino Linotype", "Book Antiqua", "Georgia" }, 48));

        public static bool Painted => Paper != null;
        public static Sprite Paper => SlicedSprite("ui_papyrus");
        public static Sprite Frame => SlicedSprite("ui_papyrus_frame");

        /// <summary>A plain sprite for a Resources texture in RealmGlyphs (a Gardiner sign or an fx texture), or null.</summary>
        public static Sprite Plain(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Sprites.TryGetValue(name, out Sprite s) && s != null) return s;
            var tex = Resources.Load<Texture2D>(Folder + name);
            if (tex == null) { Sprites[name] = null; return null; }
            s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            s.name = name;
            s.hideFlags = HideFlags.DontSave;
            Sprites[name] = s;
            return s;
        }

        private static Sprite SlicedSprite(string name)
        {
            string key = name + "#sliced";
            if (Sprites.TryGetValue(key, out Sprite s) && s != null) return s;
            var tex = Resources.Load<Texture2D>(Folder + name);
            if (tex == null) return null;
            // Borders are authored at 72 px on a 256 tall source. Scale them if Unity resized the texture on import.
            float b = Mathf.Min(72f * tex.height / 256f, Mathf.Min(tex.width, tex.height) * 0.49f);
            s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            s.name = name;
            s.hideFlags = HideFlags.DontSave;
            Sprites[key] = s;
            return s;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = anchor;
            r.anchorMax = anchor;
            r.pivot = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        public static void Stretch(RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform r = Rect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            img.preserveAspect = sprite != null && sprite.border == Vector4.zero;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, Font font, int size, Color color, TextAnchor align)
        {
            RectTransform r = Rect(name, parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>
        /// A torn papyrus sheet with its ruled frame, stretched over <paramref name="parent"/>. corner is the canvas
        /// size of the sliced corner (30 matches the bubble, smaller suits small chips). Falls back to a dark panel.
        /// </summary>
        public static RectTransform Sheet(string name, Transform parent, float corner = CornerUnits, bool frame = true)
        {
            RectTransform root = Rect(name, parent);
            Sprite paper = Paper;
            if (paper != null)
            {
                Sliced("Paper", root, paper, corner);
                Sprite f = frame ? Frame : null;
                if (f != null) Sliced("Frame", root, f, corner);
            }
            else
            {
                Image fallback = Image("Panel", root, null, new Color(0.93f, 0.86f, 0.70f, 0.96f));
                Stretch(fallback.rectTransform);
            }
            return root;
        }

        private static void Sliced(string name, RectTransform parent, Sprite s, float corner)
        {
            Image img = Image(name, parent, s, Color.white);
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.fillCenter = true;
            img.preserveAspect = false;
            img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, s.border.x / Mathf.Max(4f, corner));
            Stretch(img.rectTransform);
        }

        /// <summary>
        /// A papyrus keycap: a small sheet with an ink rule and the key in rubric capitals. Width grows with the label.
        /// Returns the cap's rect, pivot at its centre, so callers place it with anchoredPosition.
        /// </summary>
        public static RectTransform Keycap(string label, Transform parent, float height = 34f, int fontSize = 18)
        {
            RectTransform cap = Rect("Key " + label, parent);
            cap.anchorMin = cap.anchorMax = cap.pivot = new Vector2(0.5f, 0.5f);
            RectTransform sheet = Sheet("Cap", cap, 9f, true);
            Stretch(sheet);
            Text t = Label("Label", cap, label, Caps, fontSize, Rubric, TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold;
            Stretch(t.rectTransform);
            float w = Mathf.Max(height, t.preferredWidth + 20f);
            cap.sizeDelta = new Vector2(w, height);
            return cap;
        }

        /// <summary>
        /// Lays out "[KEY] words   [KEY] words" as keycaps and ink labels in one row, centred on the row's origin.
        /// Pairs alternate key, label. Returns the row width.
        /// </summary>
        public static float KeyRow(RectTransform row, IList<string> pairs, float height = 32f, int fontSize = 22, float gap = 30f)
        {
            for (int i = row.childCount - 1; i >= 0; i--) Object.Destroy(row.GetChild(i).gameObject);
            var items = new List<RectTransform>();
            var widths = new List<float>();
            float total = 0f;
            for (int i = 0; i + 1 < pairs.Count; i += 2)
            {
                RectTransform cap = Keycap(pairs[i], row, height, Mathf.RoundToInt(fontSize * 0.8f));
                Text t = Label("Verb " + pairs[i + 1], row, pairs[i + 1], Serif, fontSize, Ink, TextAnchor.MiddleLeft);
                t.fontStyle = FontStyle.Italic;
                float tw = t.preferredWidth;
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                t.rectTransform.pivot = new Vector2(0f, 0.5f);
                t.rectTransform.sizeDelta = new Vector2(tw + 4f, height);
                items.Add(cap); widths.Add(cap.sizeDelta.x);
                items.Add(t.rectTransform); widths.Add(tw);
                total += cap.sizeDelta.x + 8f + tw + (i + 2 < pairs.Count ? gap : 0f);
            }
            float x = -total * 0.5f;
            for (int i = 0; i < items.Count; i += 2)
            {
                items[i].anchoredPosition = new Vector2(x + widths[i] * 0.5f, 0f);
                x += widths[i] + 8f;
                items[i + 1].anchoredPosition = new Vector2(x, 0f);
                x += widths[i + 1] + gap;
            }
            return total;
        }

        /// <summary>A red wax seal: a deep red disc with a darker rim, built from the soft mote texture.</summary>
        public static RectTransform Seal(Transform parent, float size)
        {
            RectTransform r = Rect("Seal", parent);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(size, size);
            Sprite dot = Plain("fx_mote");
            Image rim = Image("Rim", r, dot, new Color(0.30f, 0.05f, 0.03f, 0.95f));
            Stretch(rim.rectTransform, -size * 0.12f);
            Image body = Image("Wax", r, dot, Wax);
            Stretch(body.rectTransform, size * 0.02f);
            Image sheen = Image("Sheen", r, dot, new Color(1f, 0.55f, 0.45f, 0.35f));
            Place(sheen.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-size * 0.14f, size * 0.14f), new Vector2(size * 0.42f, size * 0.42f));
            sheen.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return r;
        }

        /// <summary>Overlay canvas at 1920x1080, matched on height like the realm canvases.</summary>
        public static Canvas Canvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.layer = 5;
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920f, 1080f);
            s.matchWidthOrHeight = 1f;
            return c;
        }
    }
}
