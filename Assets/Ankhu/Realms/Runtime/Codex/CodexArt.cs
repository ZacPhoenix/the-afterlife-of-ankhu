using System.Collections.Generic;
using Ankhu.Opening;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Realms
{
    /// <summary>
    /// Look of the codex: ink colours, fonts, the generated textures under Resources/RealmCodex
    /// (made by Art/Codex/_tools/make_codex_art.py), and small builders for uGUI pieces
    /// (labels, hieroglyph groups, Egyptian numerals).
    /// </summary>
    public static class CodexArt
    {
        public const string ResourceFolder = "RealmCodex/";

        // ------------------------------------------------------------------ inks
        /// <summary>Carbon ink, the black brown of soot and gum. Body text.</summary>
        public static readonly Color Ink = new Color(0.17f, 0.115f, 0.075f, 1f);
        public static readonly Color InkSoft = new Color(0.30f, 0.21f, 0.14f, 0.92f);
        public static readonly Color InkFaint = new Color(0.30f, 0.21f, 0.14f, 0.20f);
        /// <summary>Red ochre. Scribes wrote rubrics (headings, first words) in red.</summary>
        public static readonly Color Ochre = new Color(0.62f, 0.19f, 0.09f, 1f);
        public static readonly Color OchreSoft = new Color(0.62f, 0.24f, 0.12f, 0.78f);
        /// <summary>Gold leaf on papyrus, darker than UI gold so it reads on a light page.</summary>
        public static readonly Color GoldInk = new Color(0.80f, 0.57f, 0.19f, 1f);
        public static readonly Color GoldTop = new Color(0.98f, 0.82f, 0.45f, 1f);
        public static readonly Color GoldLow = new Color(0.62f, 0.40f, 0.11f, 1f);
        public static readonly Color GoldShadow = new Color(0.25f, 0.13f, 0.03f, 0.55f);
        /// <summary>UI gold on dark backgrounds (toasts, HUD).</summary>
        public static readonly Color Gold = UiKit.Gold;
        public static readonly Color GoldDim = UiKit.GoldDim;
        public static readonly Color Parchment = UiKit.Parchment;
        public static readonly Color Night = new Color(0.035f, 0.024f, 0.016f, 1f);

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        // ------------------------------------------------------------------ fonts
        public static Font Caps => RealmFonts.Caps;
        public static Font Body => RealmFonts.Body;

        // ------------------------------------------------------------------ textures
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> warned = new HashSet<string>();
        private static Texture2D disc;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            textures.Clear();
            sprites.Clear();
            warned.Clear();
            disc = null;
        }

        /// <summary>Texture from Resources/RealmCodex. A white texture stands in (with one warning) if it is missing.</summary>
        public static Texture2D Tex(string name, bool repeat = false)
        {
            if (textures.TryGetValue(name, out Texture2D t) && t != null) return t;
            t = Resources.Load<Texture2D>(ResourceFolder + name);
            if (t == null)
            {
                if (warned.Add(name)) Debug.LogWarning("[Ankhu.Realms] Missing codex texture Resources/" + ResourceFolder + name);
                return Texture2D.whiteTexture;
            }
            t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (t.filterMode == FilterMode.Point) t.filterMode = FilterMode.Bilinear;
            textures[name] = t;
            return t;
        }

        public static bool HasTex(string name) => Tex(name) != Texture2D.whiteTexture;

        /// <summary>9 sliced sprite over a codex texture. Border in texture pixels (left, bottom, right, top).</summary>
        public static Sprite Sliced(string name, Vector4 border)
        {
            if (sprites.TryGetValue(name, out Sprite s) && s != null) return s;
            Texture2D t = Tex(name);
            s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.name = name;
            s.hideFlags = HideFlags.DontSave;
            sprites[name] = s;
            return s;
        }

        /// <summary>Crisp anti aliased white disc, for badges.</summary>
        public static Texture2D Disc
        {
            get
            {
                if (disc != null) return disc;
                const int n = 64;
                disc = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "CodexDisc", hideFlags = HideFlags.DontSave };
                var px = new Color32[n * n];
                float h = (n - 1) * 0.5f;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x - h) * (x - h) + (y - h) * (y - h));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(h - 1f - d + 0.5f) * 255f));
                    }
                }
                disc.SetPixels32(px);
                disc.Apply();
                return disc;
            }
        }

        public static Texture KindIcon(Ankhu.Realms.Core.CodexKind kind)
        {
            switch (kind)
            {
                case Ankhu.Realms.Core.CodexKind.Word: return Tex("kind_word");
                case Ankhu.Realms.Core.CodexKind.Name: return Tex("kind_name");
                case Ankhu.Realms.Core.CodexKind.Clue: return Tex("kind_clue");
                case Ankhu.Realms.Core.CodexKind.Memory: return Tex("kind_memory");
                default: return Tex("kind_lore");
            }
        }

        public static string KindLabel(Ankhu.Realms.Core.CodexKind kind)
        {
            switch (kind)
            {
                case Ankhu.Realms.Core.CodexKind.Word: return "WORD OF POWER";
                case Ankhu.Realms.Core.CodexKind.Name: return "NAME";
                case Ankhu.Realms.Core.CodexKind.Clue: return "CLUE";
                case Ankhu.Realms.Core.CodexKind.Memory: return "MEMORY";
                default: return "LORE";
            }
        }

        // ------------------------------------------------------------------ uGUI builders

        public static RectTransform Rect(string name, Transform parent) => UiKit.Rect(name, parent);

        /// <summary>Rect anchored at the top left of its parent, placed at (x, -y) with the given size.</summary>
        public static RectTransform TopLeft(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        public static RectTransform Centered(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        public static RawImage Raw(string name, Transform parent, Texture tex, Color color)
        {
            RawImage img = UiKit.Raw(name, parent, tex);
            img.color = color;
            return img;
        }

        public static Image Solid(string name, Transform parent, Color color) => UiKit.Solid(name, parent, color);

        public static Image SlicedImage(string name, Transform parent, Sprite sprite, Color color, float pixelsPerUnitMultiplier = 1f)
        {
            Image img = UiKit.Solid(name, parent, color);
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, Font font, int size, Color color, TextAnchor align,
            FontStyle style = FontStyle.Normal, float lineSpacing = 1f)
        {
            Text t = UiKit.Label(name, parent, text, font, size, color, align);
            t.fontStyle = style;
            t.lineSpacing = lineSpacing;
            t.supportRichText = true;
            return t;
        }

        /// <summary>Soft dark drop shadow under a label or glyph, for gold on papyrus and text on dark.</summary>
        public static void Shadow(Graphic g, Color color, Vector2 distance)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = color;
            s.effectDistance = distance;
        }

        // ------------------------------------------------------------------ text measuring

        private static TextGenerator generator;

        /// <summary>Height a wrapped label needs at the given width, in canvas units (scale 1).</summary>
        public static float MeasureHeight(string text, Font font, int size, FontStyle style, float lineSpacing, float width)
        {
            if (string.IsNullOrEmpty(text) || font == null) return 0f;
            if (generator == null) generator = new TextGenerator(1024);
            var s = new TextGenerationSettings
            {
                font = font,
                fontSize = size,
                fontStyle = style,
                lineSpacing = lineSpacing,
                richText = true,
                scaleFactor = 1f,
                textAnchor = TextAnchor.UpperLeft,
                alignByGeometry = false,
                resizeTextForBestFit = false,
                color = Color.white,
                generationExtents = new Vector2(width, 0f),
                pivot = Vector2.zero,
                horizontalOverflow = HorizontalWrapMode.Wrap,
                verticalOverflow = VerticalWrapMode.Overflow,
                updateBounds = false,
                generateOutOfBounds = true,
            };
            return generator.GetPreferredHeight(text, s);
        }

        public static float MeasureWidth(string text, Font font, int size, FontStyle style)
        {
            if (string.IsNullOrEmpty(text) || font == null) return 0f;
            if (generator == null) generator = new TextGenerator(1024);
            var s = new TextGenerationSettings
            {
                font = font, fontSize = size, fontStyle = style, lineSpacing = 1f, richText = true, scaleFactor = 1f,
                textAnchor = TextAnchor.UpperLeft, color = Color.white, generationExtents = new Vector2(4096f, 0f),
                pivot = Vector2.zero, horizontalOverflow = HorizontalWrapMode.Overflow, verticalOverflow = VerticalWrapMode.Overflow,
                generateOutOfBounds = true,
            };
            return generator.GetPreferredWidth(text, s);
        }

        // ------------------------------------------------------------------ hieroglyph groups

        /// <summary>One placed sign of a glyph group, with its optional halo.</summary>
        public sealed class GlyphGroup
        {
            public RectTransform Root;
            public float Width;
            public readonly List<RawImage> Signs = new List<RawImage>();
            public readonly List<RawImage> Glows = new List<RawImage>();
        }

        private struct SignBox
        {
            public string Code;
            public Rect Ink;
            public float W, H;
            public bool Flat;
        }

        /// <summary>
        /// Lays out Gardiner codes (space separated) as a hieroglyph group: tall signs stand alone, consecutive
        /// flat signs stack in a quadrat, as a scribe would arrange them. Signs are cropped to their ink box.
        /// The root is centred on its parent at (0, 0) with size (Width, height). Scaled down to fit maxWidth.
        /// </summary>
        public static GlyphGroup Glyphs(Transform parent, string signs, float height, Color color, float maxWidth, bool glow = false, Color? glowColor = null)
        {
            var group = new GlyphGroup();
            RectTransform root = Rect("Glyphs", parent);
            group.Root = root;
            Centered(root, Vector2.zero, new Vector2(0f, height));
            if (string.IsNullOrWhiteSpace(signs)) return group;

            float E = height;
            float perEm = E / 0.66f;
            var columns = new List<List<SignBox>>();
            foreach (string raw in signs.Split(new[] { ' ', '-', ':', '*' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (!GlyphAtlas.Has(raw)) continue;
                Rect ink = GlyphAtlas.InkRect(raw);
                float aspect = GlyphAtlas.Aspect(raw);
                float wEm = ink.width * aspect;
                float hEm = ink.height;
                var b = new SignBox { Code = raw, Ink = ink, W = wEm * perEm, H = hEm * perEm, Flat = hEm < 0.26f && wEm > hEm * 1.1f };
                if (b.H > E) { float k = E / b.H; b.W *= k; b.H *= k; }
                List<SignBox> last = columns.Count > 0 ? columns[columns.Count - 1] : null;
                if (b.Flat && last != null && last.Count < 3 && last[0].Flat)
                {
                    float stackH = b.H + E * 0.07f;
                    foreach (SignBox s in last) stackH += s.H + E * 0.07f;
                    if (stackH <= E * 1.02f) { last.Add(b); continue; }
                }
                columns.Add(new List<SignBox> { b });
            }
            if (columns.Count == 0) return group;

            float gap = E * 0.10f;
            float x = 0f;
            var placed = new List<KeyValuePair<SignBox, Vector2>>();
            var sizes = new List<Vector2>();
            foreach (List<SignBox> col in columns)
            {
                float cw = 0f, ch = 0f;
                foreach (SignBox s in col) { cw = Mathf.Max(cw, s.W); ch += s.H; }
                ch += (col.Count - 1) * E * 0.07f;
                float k = ch > E ? E / ch : 1f;
                float y = E * 0.5f + ch * k * 0.5f;
                foreach (SignBox s in col)
                {
                    float w = s.W * k, h = s.H * k;
                    placed.Add(new KeyValuePair<SignBox, Vector2>(s, new Vector2(x + cw * k * 0.5f, y - h * 0.5f)));
                    sizes.Add(new Vector2(w, h));
                    y -= h + E * 0.07f * k;
                }
                x += cw * k + gap;
            }
            float total = x - gap;
            float scale = maxWidth > 0f && total > maxWidth ? maxWidth / total : 1f;
            root.sizeDelta = new Vector2(total, E);
            root.localScale = new Vector3(scale, scale, 1f);
            group.Width = total * scale;

            for (int i = 0; i < placed.Count; i++)
            {
                SignBox s = placed[i].Key;
                Vector2 c = placed[i].Value;
                Vector2 size = sizes[i];
                Sprite sp = GlyphAtlas.Get(s.Code);
                if (sp == null) continue;
                Rect uv = SpriteUv(sp, s.Ink, 0.012f);
                if (glow)
                {
                    Sprite gs = GlyphAtlas.GetGlow(s.Code);
                    if (gs != null)
                    {
                        const float pad = 0.10f;
                        Rect guv = SpriteUv(gs, s.Ink, pad);
                        float aspect = GlyphAtlas.Aspect(s.Code);
                        float gw = size.x * (s.Ink.width + pad * 2f / aspect) / Mathf.Max(0.001f, s.Ink.width);
                        float gh = size.y * (s.Ink.height + pad * 2f) / Mathf.Max(0.001f, s.Ink.height);
                        RawImage g = Raw("Glow_" + s.Code, root, gs.texture, glowColor ?? new Color(1f, 0.85f, 0.45f, 0f));
                        g.uvRect = guv;
                        PlaceInGroup(g.rectTransform, c, new Vector2(gw, gh));
                        group.Glows.Add(g);
                    }
                }
                RawImage img = Raw("Sign_" + s.Code, root, sp.texture, color);
                img.uvRect = uv;
                PlaceInGroup(img.rectTransform, c, size);
                group.Signs.Add(img);
            }
            // Glows drawn first, signs on top.
            foreach (RawImage g in group.Glows) g.transform.SetAsFirstSibling();
            return group;
        }

        private static void PlaceInGroup(RectTransform r, Vector2 centerFromBottomLeft, Vector2 size)
        {
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = centerFromBottomLeft;
            r.sizeDelta = size;
        }

        private static Rect SpriteUv(Sprite sp, Rect ink, float pad)
        {
            Texture tex = sp.texture;
            Rect tr = sp.textureRect;
            float tw = tex.width, th = tex.height;
            float aspect = tr.height > 0f ? tr.width / tr.height : 1f;
            float px = pad / Mathf.Max(0.5f, aspect);
            float l = Mathf.Max(-0.2f, ink.xMin - px), r = Mathf.Min(1.2f, ink.xMax + px);
            float b = Mathf.Max(-0.2f, ink.yMin - pad), t = Mathf.Min(1.2f, ink.yMax + pad);
            return new Rect((tr.x + l * tr.width) / tw, (tr.y + b * tr.height) / th, (r - l) * tr.width / tw, (t - b) * tr.height / th);
        }

        // ------------------------------------------------------------------ Egyptian numerals

        /// <summary>
        /// Page number in Egyptian numerals: coils (100), heel bones (10), strokes (1), highest first.
        /// More than four of a sign split into two rows, as the scribes wrote them. Returns the width.
        /// The root is anchored at its parent's bottom left, pivot at its own bottom left.
        /// </summary>
        public static RectTransform Numeral(Transform parent, int n, float height, Color color, out float width)
        {
            RectTransform root = Rect("Numeral", parent);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.zero;
            float x = 0f;
            n = Mathf.Clamp(n, 0, 999);
            AddNumeralRun(root, "num_100", n / 100, height, color, ref x);
            AddNumeralRun(root, "num_10", n / 10 % 10, height, color, ref x);
            AddNumeralRun(root, "num_1", n % 10, height, color, ref x);
            width = Mathf.Max(0f, x - height * 0.08f);
            root.sizeDelta = new Vector2(width, height);
            return root;
        }

        private static void AddNumeralRun(RectTransform root, string tex, int count, float height, Color color, ref float x)
        {
            if (count <= 0) return;
            Texture2D t = Tex(tex);
            float aspect = t.height > 0 ? t.width / (float)t.height : 0.5f;
            // Narrow glyph textures carry side padding: tighten the advance.
            float tight = tex == "num_1" ? 0.45f : 0.78f;
            int rows = count > 4 ? 2 : 1;
            int perRow = Mathf.CeilToInt(count / (float)rows);
            float h = rows == 1 ? height : height * 0.52f;
            float w = h * aspect;
            float adv = w * tight;
            for (int i = 0; i < count; i++)
            {
                int row = i / perRow, col = i % perRow;
                RawImage img = Raw("N", root, t, color);
                RectTransform r = img.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
                r.sizeDelta = new Vector2(w, h);
                float yy = rows == 1 ? 0f : (row == 0 ? height - h : 0f);
                r.anchoredPosition = new Vector2(x + col * adv - (w - adv) * 0.5f, yy);
            }
            x += perRow * adv + height * 0.14f;
        }
    }

    /// <summary>Vertex colour gradient for any uGUI graphic (text, images). Multiplies the graphic colour.</summary>
    [DisallowMultipleComponent]
    public sealed class CodexGradient : BaseMeshEffect
    {
        public Color From = Color.white;
        public Color To = Color.white;
        /// <summary>True: From at the top, To at the bottom. False: From at the left, To at the right.</summary>
        public bool Vertical = true;

        public static CodexGradient Add(Graphic g, Color from, Color to, bool vertical)
        {
            var e = g.gameObject.AddComponent<CodexGradient>();
            e.From = from;
            e.To = to;
            e.Vertical = vertical;
            return e;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            UnityEngine.Rect r = ((RectTransform)transform).rect;
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float t = Vertical ? Mathf.InverseLerp(r.yMax, r.yMin, v.position.y) : Mathf.InverseLerp(r.xMin, r.xMax, v.position.x);
                Color c = Color.Lerp(From, To, t);
                Color baseC = v.color;
                v.color = baseC * c;
                vh.SetUIVertex(v, i);
            }
        }
    }
}
