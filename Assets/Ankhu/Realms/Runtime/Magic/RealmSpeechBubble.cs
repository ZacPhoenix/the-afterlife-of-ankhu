using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Realms
{
    /// <summary>How a letter leaves the bubble.</summary>
    public enum LetterExit
    {
        /// <summary>Brightens, then breaks into gold motes that fly into its word's hieroglyphs.</summary>
        Gold,
        /// <summary>Turns to grey sand that falls and blows away.</summary>
        Sand,
        /// <summary>A word the spell did not use: fades upward quietly.</summary>
        Fade,
    }

    /// <summary>
    /// The view half of the words of power: a screen space speech bubble above Ankhu's head, with per letter
    /// English, the hieroglyph row and the UI particles. Built entirely in code by <see cref="SpellPresenter"/>,
    /// which drives it (input and sequencing live there). Units are canvas units at a 1080 px reference height,
    /// so the bubble keeps the same proportion of the screen at 1080p and 4K.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RealmSpeechBubble : MonoBehaviour
    {
        // ------------------------------------------------------------------ look and layout constants

        public const int FontSize = 34;
        // 72 read as a thin line of ink inside the painted bubble at 720p. The row is still fitted to the bubble width.
        public const float GlyphEm = 112f;
        private const float PadX = 30f;
        private const float PadTop = 20f;
        private const float PadBottom = 17f;
        private const float TailH = 17f;
        private const float TailW = 26f;
        private const float CornerUnits = 15f;
        private const float MinWidth = 230f;
        private const float ScreenMargin = 18f;
        private const float HintHeight = 28f;
        private const float ReplyMaxWidth = 640f;
        private const int ReplyFontSize = 26;
        private const float SnapDistance = 180f;
        public const float AnticipateSeconds = 0.16f;
        public const float ShimmerSeconds = 0.75f;
        private const string Placeholder = "Speak a word of power";

        // ------------------------------------------------------------------ nested state

        private sealed class Letter
        {
            public Text Text;
            public RectTransform Rect;
            public char Char;
            public float X;           // centre x relative to the left edge of the line
            public float Advance;
            public float Born;
            public bool PopIn;
            public Color TintFrom = MagicPalette.Ink;
            public Color TintTo = MagicPalette.Ink;
            public float TintStart;
            public float TintDuration;
            public float ExitAt = float.PositiveInfinity;
            public LetterExit Exit;
            public bool ExitSpawned;
            public bool Gone;
            public Vector2 MoteTarget;
            public Vector2 MoteSpread;
            public float ShakeStart = -1f;
            public float ShakeDuration;
            public float ShakeAmplitude;
        }

        /// <summary>One hieroglyph in the bubble.</summary>
        public sealed class Sign
        {
            public string Code;
            public RectTransform Rect;
            public Image Core;
            public Image Glow;
            public Vector2 Position;   // em box centre in glyph row units
            public float Size;         // em box height in glyph row units
            public float Aspect;
            public float AppearAt = float.PositiveInfinity;
            public float LaunchedAt = float.PositiveInfinity;
            public float RowX01;
            public float Phase;
            public SignGroup Group;
        }

        /// <summary>The signs of one spoken word, optionally in a cartouche.</summary>
        public sealed class SignGroup
        {
            public readonly List<Sign> Signs = new List<Sign>();
            public bool Cartouche;
            public float CenterX;
            public float Width;
            public Image Frame;
            public Image FrameGlow;
            public float FrameHeight;
            public float AppearAt = float.PositiveInfinity;
            public float FadeAt = float.PositiveInfinity;
        }

        private sealed class Mote
        {
            public Image Image;
            public RectTransform Rect;
            public int Kind;            // 0 homing gold, 1 free drift, 2 sand
            public Vector2 P0, P1, P2;  // homing bezier
            public Vector2 Pos, Vel;
            public float Born, Life, Size, Rotation, Spin;
            public Color Color;
            public bool Active;
        }

        // ------------------------------------------------------------------ fields

        private Canvas canvas;
        private RectTransform canvasRect;
        private CanvasGroup group;
        private RectTransform anchor;
        private RectTransform body;
        private RectTransform shadow, lapisGlow, panel, border, glintWindow, glintBorder, hairline, lozenge, sheenMask, sheen;
        private RectTransform tailGold, tailFill;
        private RectTransform textRow, glyphRow, fxLayer;
        private RectTransform liveGloss;
        private int liveGlossWords;
        private Image caret, borderImage, hairlineImage, lozengeImage;
        private Text placeholder, hint, reply, ghost;
        private string ghostWord;
        private float ghostWidth;
        private bool ghostOn;
        private string hintDefault = string.Empty;
        private string hintKeys = "963A1F";
        private IReadOnlyList<string> trayShown;
        private int traySelected = -1;

        private readonly List<Letter> letters = new List<Letter>();
        private readonly Stack<Letter> letterPool = new Stack<Letter>();
        private readonly List<SignGroup> groups = new List<SignGroup>();
        private readonly List<Sign> signs = new List<Sign>();
        private readonly List<Mote> motes = new List<Mote>();
        private string text = string.Empty;
        private float textWidth;
        private float glyphRowWidth;
        private Font bodyFont;
        private Font capsFont;
        private BubbleSkin skin;
        private GameObject glintGo;

        private bool visible;
        private bool typing;
        private float lastTypeTime;
        private float visibleSince;
        private float hideStart = -1f;
        private bool glyphPhase;
        private bool collapseMain;
        private float shimmerStart = -1f;
        private float anticipateAt = float.PositiveInfinity;
        private float replyAt = float.PositiveInfinity;
        private bool hasPosition;
        private Vector2 tip;
        private float curW = MinWidth, curMain = FontSize * 1.5f, curReply, curHint;
        private float pendingSince = -1f;

        /// <summary>Where the bubble points: Ankhu's head (or any transform near it).</summary>
        public Transform Head { get; set; }
        /// <summary>Camera used to place the bubble. Falls back to Camera.main.</summary>
        public Func<Camera> CameraSource { get; set; }
        /// <summary>World offset above <see cref="Head"/> where the tail points.</summary>
        public Vector3 HeadOffset { get; set; } = new Vector3(0f, 0.32f, 0f);

        /// <summary>The look. Set before the bubble first shows (see <see cref="BubbleSkin"/>). Changing it rebuilds the panel colours.</summary>
        public BubbleSkin Skin
        {
            get => skin;
            set { skin = value ?? BubbleSkin.Obsidian; if (canvas != null) ApplySkin(); }
        }

        public bool Visible => visible;
        /// <summary>True while the bubble is fading out.</summary>
        public bool IsHiding => hideStart >= 0f;

        /// <summary>The judged outcome starts: the words stop "being weighed".</summary>
        public void BeginPerformance()
        {
            pendingSince = -1f;
            typing = false;
            if (hideStart >= 0f) ShowNow();
        }
        public string Text => text;
        public IReadOnlyList<SignGroup> Groups => groups;
        public IReadOnlyList<Sign> Signs => signs;
        public Canvas Canvas => canvas;
        public float GlyphScale => glyphRow != null ? glyphRow.localScale.x : 1f;

        private static float Now => Time.unscaledTime;

        // ------------------------------------------------------------------ construction

        public static RealmSpeechBubble Create(Transform parent)
        {
            var go = new GameObject("Realm Speech Bubble", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            if (parent != null) go.transform.SetParent(parent, false);
            var view = go.AddComponent<RealmSpeechBubble>();
            view.skin = BubbleSkin.Default;
            view.Build();
            return view;
        }

        private void Build()
        {
            bodyFont = RealmFonts.Body;
            capsFont = RealmFonts.Caps;

            canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 480;
            canvas.pixelPerfect = false;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = 100f;
            canvasRect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            anchor = NewRect("Anchor", canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f));
            anchor.sizeDelta = Vector2.zero;
            body = NewRect("Body", anchor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f));

            shadow = Sliced("Shadow", body, GlyphAtlas.UiShadow, Color.clear, 1.25f).rectTransform;
            lapisGlow = Sliced("Aura", body, GlyphAtlas.UiShadow, Color.clear, 1.6f).rectTransform;
            tailGold = Simple("Tail Edge", body, null, Color.clear).rectTransform;
            panel = Sliced("Panel", body, null, Color.clear, 1f).rectTransform;
            borderImage = Sliced("Border", body, null, Color.clear, 1f);
            border = borderImage.rectTransform;

            glintWindow = NewRect("Border Glint", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            glintGo = glintWindow.gameObject;
            var glintMask = glintWindow.gameObject.AddComponent<RectMask2D>();
            glintMask.softness = new Vector2Int(70, 0);
            glintBorder = Sliced("Glint", glintWindow, null, new Color(1f, 0.95f, 0.8f, 0.95f), 1f).rectTransform;
            SetAnchor(glintBorder, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            sheenMask = NewRect("Sheen Mask", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            var sm = sheenMask.gameObject.AddComponent<RectMask2D>();
            sm.softness = new Vector2Int(36, 8);
            sheen = Simple("Sheen", sheenMask, GlyphAtlas.UiSheen, Color.clear).rectTransform;
            SetAnchor(sheen, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            hairlineImage = Simple("Hairline", body, GlyphAtlas.UiHairline, Color.clear);
            hairline = hairlineImage.rectTransform;
            SetAnchor(hairline, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
            lozengeImage = Simple("Lozenge", body, null, Color.clear);
            lozenge = lozengeImage.rectTransform;
            SetAnchor(lozenge, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
            lozenge.sizeDelta = new Vector2(6.5f, 6.5f);
            lozenge.localRotation = Quaternion.Euler(0f, 0f, 45f);

            tailFill = Simple("Tail Fill", body, null, Color.clear).rectTransform;

            textRow = NewRect("Text Row", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            glyphRow = NewRect("Glyph Row", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            fxLayer = NewRect("UI Motes", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            liveGloss = NewRect("Live Gloss", textRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            caret = Simple("Caret", textRow, null, Color.clear);

            placeholder = Label("Placeholder", textRow, bodyFont, FontSize, Color.clear, TextAnchor.MiddleCenter);
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.text = Placeholder;

            ghost = Label("Ghost", textRow, bodyFont, FontSize, Color.clear, TextAnchor.MiddleLeft);
            ghost.rectTransform.pivot = new Vector2(0f, 0.5f);
            ghost.enabled = false;

            reply = Label("Reply", body, bodyFont, ReplyFontSize, MagicPalette.Quiet, TextAnchor.MiddleCenter);
            reply.fontStyle = FontStyle.Italic;
            reply.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetAnchor(reply.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            hint = Label("Hint", body, capsFont, 14, MagicPalette.Quiet, TextAnchor.MiddleCenter);
            hint.supportRichText = true;
            SetAnchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            ApplySkin();
            canvas.enabled = false;
        }

        /// <summary>Pushes the skin's sprites and fixed colours onto the panel pieces.</summary>
        private void ApplySkin()
        {
            BubbleSkin k = skin ?? (skin = BubbleSkin.Obsidian);
            float mult = Mathf.Max(0.01f, k.CornerPixelsPerUnit);
            SetSprite(panel.GetComponent<Image>(), k.PanelSprite?.Invoke(), k.PanelColor, mult);
            SetSprite(borderImage, k.BorderSprite?.Invoke(), k.BorderColor, mult);
            SetSprite(glintBorder.GetComponent<Image>(), k.BorderSprite?.Invoke(), new Color(1f, 0.95f, 0.8f, 0.95f), mult);
            SetSprite(tailGold.GetComponent<Image>(), k.TailEdgeSprite?.Invoke(), k.TailEdgeColor, 1f);
            SetSprite(tailFill.GetComponent<Image>(), k.TailFillSprite?.Invoke(), k.TailFillColor, 1f);
            shadow.GetComponent<Image>().color = k.ShadowColor;
            lapisGlow.GetComponent<Image>().color = k.AuraColor;
            sheen.GetComponent<Image>().color = new Color(k.Empowered.r, k.Empowered.g, k.Empowered.b, k.SheenAlpha);
            glintGo.SetActive(k.Glint);
            Sprite caretSprite = k.CaretSprite?.Invoke();
            caret.sprite = caretSprite;
            caret.rectTransform.sizeDelta = new Vector2(k.CaretWidth, FontSize * (caretSprite != null ? 1.05f : 0.95f));
            placeholder.color = k.Placeholder;
            reply.color = k.Reply;
            string keys = ColorUtility.ToHtmlStringRGB(k.HintKeys);
            hintKeys = keys;
            hintDefault = "<color=#" + keys + ">ENTER</color>  speak      <color=#" + keys + ">ESC</color>  cancel      <color=#" + keys + ">UP</color>  recall";
            SetTray(trayShown, traySelected);
            foreach (Letter l in letters)
            {
                if (l.TintTo == l.TintFrom) l.TintFrom = l.TintTo = k.Ink;
            }
        }

        private static void SetSprite(Image img, Sprite sprite, Color color, float ppuMultiplier)
        {
            if (img == null) return;
            img.sprite = sprite;
            img.color = color;
            img.enabled = sprite != null || color.a > 0f;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, ppuMultiplier);
            }
            else
            {
                img.type = Image.Type.Simple;
            }
        }

        // ------------------------------------------------------------------ public control: typing

        /// <summary>Opens the bubble for typing, empty.</summary>
        public void BeginTyping()
        {
            ClearContent();
            typing = true;
            lastTypeTime = Now;
            pendingSince = -1f;
            RefreshGhost();
            ShowNow();
        }

        /// <summary>
        /// Zac 2026-11-26, the guided first spell: the word the player should type, shown in grey letters after what
        /// they have typed so far, with the hint line telling them what to do. Null turns it off.
        /// </summary>
        public string Ghost
        {
            get => ghostWord;
            set { ghostWord = string.IsNullOrEmpty(value) ? null : value; RefreshGhost(); SetTray(trayShown, traySelected); }
        }

        private static string Fold(string v) => (v ?? string.Empty).ToLowerInvariant().Replace('-', ' ');

        /// <summary>True when the typed text is the coached word, or the start of it.</summary>
        private bool GhostPrefix => ghostWord != null && Fold(ghostWord).StartsWith(Fold(text), StringComparison.Ordinal);
        private bool GhostComplete => ghostWord != null && Fold(text).Trim() == Fold(ghostWord);

        private void RefreshGhost()
        {
            ghostOn = typing && GhostPrefix && text.Length < ghostWord.Length;
            if (!ghostOn) ghostWidth = textWidth;
            if (ghost == null) return;
            ghost.enabled = ghostOn;
            if (!ghostOn) return;
            string rest = ghostWord.Substring(text.Length);
            if (bodyFont != null) bodyFont.RequestCharactersInTexture(rest, FontSize, FontStyle.Normal);
            float w = 0f;
            foreach (char ch in rest) w += AdvanceOf(ch);
            ghostWidth = textWidth + w;
            ghost.text = rest;
            Color c0 = skin != null ? skin.Ink : Color.white;
            ghost.color = new Color(c0.r, c0.g, c0.b, 0.32f);
            ghost.rectTransform.sizeDelta = new Vector2(w + 12f, FontSize * 1.6f);
        }

        /// <summary>Width the typed line is centred on: the typed text, or the whole coached word while it is being typed.</summary>
        private float CentreW => ghostOn ? Mathf.Max(textWidth, ghostWidth) : textWidth;

        /// <summary>Mirrors the typed text. New characters pop in, removed ones vanish.</summary>
        public void SetText(string value, bool animate = true)
        {
            value ??= string.Empty;
            if (value == text) return;
            int common = 0;
            while (common < value.Length && common < text.Length && value[common] == text[common]) common++;
            for (int i = letters.Count - 1; i >= common; i--) Recycle(i);
            if (bodyFont != null) bodyFont.RequestCharactersInTexture(value, FontSize, FontStyle.Normal);
            float x = common > 0 ? letters[common - 1].X + letters[common - 1].Advance * 0.5f : 0f;
            float now = Now;
            for (int i = common; i < value.Length; i++)
            {
                Letter l = Rent();
                l.Char = value[i];
                l.Text.text = value[i].ToString();
                l.Advance = AdvanceOf(value[i]);
                l.X = x + l.Advance * 0.5f;
                l.Rect.sizeDelta = new Vector2(l.Advance + 8f, FontSize * 1.6f);
                x += l.Advance;
                l.Born = now + (animate ? (i - common) * 0.012f : -1f);
                l.PopIn = animate;
                letters.Add(l);
            }
            text = value;
            textWidth = x;
            lastTypeTime = now;
            RefreshGhost();
        }

        /// <summary>One typed word the lexicon knows: its letter span, its signs and its sound.</summary>
        public struct LiveWord
        {
            public int From;
            public int To;
            public IReadOnlyList<string> Signs;
            public string Translit;
            public bool Target;
        }

        private const float LiveSignSize = 30f;
        private const float LiveSignY = 50f;
        private const float LiveTranslitY = -42f;
        private const float LiveExtraHeight = 92f;
        private const float LiveSignWide = 54f;
        private const int TrayFontSize = 20;

        /// <summary>
        /// Zac 2026-11-25, spell input pick A "Bubble refined": while typing, each known word wears its signs above
        /// it and its transliteration below. The noun the spell will act on is underlined in rubric. Call after
        /// <see cref="SetText"/> with spans into the current text. Null or empty clears it.
        /// </summary>
        public void SetLiveGloss(IReadOnlyList<LiveWord> words)
        {
            if (liveGloss == null) return;
            for (int i = liveGloss.childCount - 1; i >= 0; i--) Destroy(liveGloss.GetChild(i).gameObject);
            liveGlossWords = 0;
            // Letters back at their natural spacing first, then pushed apart below where glosses would touch.
            // Only while typing: after Enter the words keep their spacing for the performance.
            if (!typing) { RefreshGhost(); return; }
            float nx = 0f;
            foreach (Letter l in letters) { l.X = nx + l.Advance * 0.5f; nx += l.Advance; }
            textWidth = nx;
            if (words == null) { RefreshGhost(); return; }
            var built = new List<(RectTransform rect, LiveWord w, float half)>();
            foreach (LiveWord w in words)
            {
                if (w.From < 0 || w.To > letters.Count || w.To <= w.From) continue;
                Letter a = letters[w.From], b = letters[w.To - 1];
                float span = (b.X + b.Advance * 0.5f) - (a.X - a.Advance * 0.5f);
                RectTransform word = NewRect("Word " + w.From, liveGloss, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                liveGlossWords++;
                float half = span * 0.5f;

                if (w.Signs != null && w.Signs.Count > 0)
                {
                    // The sign textures are full em boxes and flat signs fill only a sliver of them, so each box is
                    // sized from its ink: the ink stands LiveSignSize tall or LiveSignWide across, whichever binds.
                    float total = 0f;
                    var sizes = new List<Vector2>();
                    var inks = new List<Rect>();
                    foreach (string code in w.Signs)
                    {
                        float asp = Mathf.Clamp(GlyphAtlas.Aspect(code), 0.3f, 2.2f);
                        Rect ink = GlyphAtlas.InkRect(code);
                        float side = Mathf.Min(LiveSignSize / Mathf.Max(0.05f, ink.height), LiveSignWide / Mathf.Max(0.05f, ink.width * asp));
                        Vector2 sz = new Vector2(side * asp, side);
                        sizes.Add(sz);
                        inks.Add(ink);
                        total += sz.x * ink.width + 6f;
                    }
                    float fit = total > span + 40f ? (span + 40f) / total : 1f;
                    float x = -total * fit * 0.5f;
                    half = Mathf.Max(half, total * fit * 0.5f);
                    for (int i = 0; i < w.Signs.Count; i++)
                    {
                        Sprite sp = GlyphAtlas.Get(w.Signs[i]);
                        if (sp == null) continue;
                        Vector2 sz = sizes[i] * fit;
                        Rect ink = inks[i];
                        float inkW = sz.x * ink.width;
                        Image img = Simple("Sign " + w.Signs[i], word, sp, w.Target ? skin.Empowered : skin.Ink);
                        img.preserveAspect = true;
                        img.rectTransform.sizeDelta = sz;
                        // Place the ink box (not the em box) on the sign line: ink centre x at the slot, ink bottom on the base.
                        float px = x + inkW * 0.5f - (ink.center.x - 0.5f) * sz.x;
                        float py = LiveSignY - LiveSignSize * 0.5f - (ink.yMin - 0.5f) * sz.y;
                        img.rectTransform.anchoredPosition = new Vector2(px, py);
                        x += inkW + 6f * fit;
                    }
                }
                if (!string.IsNullOrEmpty(w.Translit))
                {
                    Text t = Label("Translit", word, bodyFont, 28, new Color(skin.Ink.r, skin.Ink.g, skin.Ink.b, 0.92f), TextAnchor.MiddleCenter);
                    t.fontStyle = FontStyle.Italic;
                    t.text = w.Translit;
                    t.rectTransform.sizeDelta = new Vector2(Mathf.Max(span, t.preferredWidth) + 8f, 30f);
                    t.rectTransform.anchoredPosition = new Vector2(0f, LiveTranslitY);
                    half = Mathf.Max(half, t.preferredWidth * 0.5f);
                }
                if (w.Target)
                {
                    Image line = Simple("Target Rule", word, null, skin.Refused);
                    line.rectTransform.sizeDelta = new Vector2(span, 2.4f);
                    line.rectTransform.anchoredPosition = new Vector2(0f, -FontSize * 0.5f - 2f);
                }
                built.Add((word, w, half));
            }

            // Zac 2026-11-26 ("flow east"): a word's signs and sound are often wider than the word, so the next word's
            // gloss landed on top. Where two glosses would come closer than GlossGap, the later word and everything
            // after it moves right until they clear.
            const float GlossGap = 16f;
            float prevRight = float.NegativeInfinity;
            foreach (var g in built)
            {
                Letter a = letters[g.w.From], b = letters[g.w.To - 1];
                float centre = ((a.X - a.Advance * 0.5f) + (b.X + b.Advance * 0.5f)) * 0.5f;
                float push = prevRight + GlossGap - (centre - g.half);
                if (push > 0f)
                {
                    for (int i = g.w.From; i < letters.Count; i++) letters[i].X += push;
                    textWidth += push;
                    centre += push;
                }
                prevRight = centre + g.half;
            }
            RefreshGhost();
            foreach (var g in built)
            {
                Letter a = letters[g.w.From], b = letters[g.w.To - 1];
                float centre = ((a.X - a.Advance * 0.5f) + (b.X + b.Advance * 0.5f)) * 0.5f;
                g.rect.anchoredPosition = new Vector2(centre - CentreW * 0.5f, 0f);
            }
        }

        /// <summary>
        /// Zac 2026-11-25, codex pick B: the word tray. While typing, the known words that fit the word being typed
        /// sit on the hint line. Tab steps through them, the chosen one is bracketed in rubric, Enter inserts it.
        /// Null or empty restores the plain key hint.
        /// </summary>
        public void SetTray(IReadOnlyList<string> words, int selected)
        {
            trayShown = words;
            traySelected = selected;
            if (hint == null) return;
            if (ghostWord != null && typing && selected < 0)
            {
                // The guided first spell: no tray, just what to do next.
                hint.fontSize = TrayFontSize;
                string k = "<color=#" + hintKeys + ">";
                hint.text = GhostComplete ? k + "<b>ENTER</b></color>  to speak"
                    : GhostPrefix ? "Type  " + k + ghostWord.ToUpperInvariant() + "</color>      " + k + "ENTER</color>  speak"
                    : "Type  " + k + ghostWord.ToUpperInvariant() + "</color>      " + k + "BACKSPACE</color>  fix";
                return;
            }
            if (words == null || words.Count == 0) { hint.text = hintDefault; hint.fontSize = 14; return; }
            hint.fontSize = TrayFontSize;
            const int Window = 6;
            int start = selected < 0 ? 0 : Mathf.Clamp(selected - Window / 2, 0, Mathf.Max(0, words.Count - Window));
            int end = Mathf.Min(words.Count, start + Window);
            var sb = new System.Text.StringBuilder();
            sb.Append("<color=#").Append(hintKeys).Append(">TAB</color>  ");
            if (start > 0) sb.Append("…  ");
            for (int i = start; i < end; i++)
            {
                string w = words[i].ToUpperInvariant();
                if (i == selected) sb.Append("<color=#").Append(hintKeys).Append("><b>[ ").Append(w).Append(" ]</b></color>");
                else sb.Append(w);
                if (i < end - 1) sb.Append("   ");
            }
            if (end < words.Count) sb.Append("  …");
            sb.Append("      <color=#").Append(hintKeys).Append(">ENTER</color>  ").Append(selected >= 0 ? "insert" : "speak");
            hint.text = sb.ToString();
        }

        /// <summary>Typing ended. keepText true leaves the words on show while the rules judge them.</summary>
        public void EndTyping(bool keepText)
        {
            typing = false;
            RefreshGhost();
            SetLiveGloss(null);
            SetTray(null, -1);
            if (keepText && letters.Count > 0)
            {
                pendingSince = Now;
            }
            else
            {
                Hide();
            }
        }

        /// <summary>Seconds since the words were left pending, or -1.</summary>
        public float PendingSeconds => pendingSince < 0f ? -1f : Now - pendingSince;

        /// <summary>Shows a line of words outside typing (for a scripted spell). Replaces the content.</summary>
        public void ShowWords(string words)
        {
            ClearContent();
            typing = false;
            ShowNow();
            SetText(words, true);
        }

        // ------------------------------------------------------------------ public control: performance

        /// <summary>Letter index ranges (start inclusive, end exclusive) of each spoken word in the shown text, or (-1,-1).</summary>
        public List<Vector2Int> MatchWords(IReadOnlyList<SpokenWord> words)
        {
            var tokens = new List<Vector2Int>();
            int i = 0;
            while (i < text.Length)
            {
                if (!IsWordChar(text[i])) { i++; continue; }
                int s = i;
                while (i < text.Length && IsWordChar(text[i])) i++;
                tokens.Add(new Vector2Int(s, i));
            }
            var used = new bool[tokens.Count];
            var result = new List<Vector2Int>();
            int cursor = 0;
            for (int w = 0; w < words.Count; w++)
            {
                string[] parts = SplitWords(words[w].English);
                Vector2Int found = new Vector2Int(-1, -1);
                if (parts.Length > 0)
                {
                    found = FindTokens(tokens, used, parts, cursor);
                    if (found.x < 0) found = FindTokens(tokens, used, parts, 0);
                }
                if (found.x >= 0)
                {
                    for (int t = found.x; t < found.y; t++) used[t] = true;
                    cursor = found.y;
                    result.Add(new Vector2Int(tokens[found.x].x, tokens[found.y - 1].y));
                }
                else
                {
                    result.Add(new Vector2Int(-1, -1));
                }
            }
            return result;
        }

        private Vector2Int FindTokens(List<Vector2Int> tokens, bool[] used, string[] parts, int from)
        {
            for (int t = from; t + parts.Length <= tokens.Count; t++)
            {
                bool ok = true;
                for (int p = 0; p < parts.Length && ok; p++)
                {
                    if (used[t + p]) { ok = false; break; }
                    Vector2Int tok = tokens[t + p];
                    ok = string.Equals(text.Substring(tok.x, tok.y - tok.x), parts[p], StringComparison.OrdinalIgnoreCase);
                }
                if (ok) return new Vector2Int(t, t + parts.Length);
            }
            return new Vector2Int(-1, -1);
        }

        private static string[] SplitWords(string s)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(s)) return list.ToArray();
            int i = 0;
            while (i < s.Length)
            {
                if (!IsWordChar(s[i])) { i++; continue; }
                int st = i;
                while (i < s.Length && IsWordChar(s[i])) i++;
                list.Add(s.Substring(st, i - st));
            }
            return list.ToArray();
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '\'' || c == '-' || c == '’';

        public int LetterCount => letters.Count;
        public bool IsSpace(int i) => i < 0 || i >= letters.Count || char.IsWhiteSpace(letters[i].Char);

        public void TintLetter(int i, Color to, float start, float duration)
        {
            if (i < 0 || i >= letters.Count) return;
            Letter l = letters[i];
            l.TintFrom = CurrentTint(l, start);
            l.TintTo = to;
            l.TintStart = start;
            l.TintDuration = duration;
        }

        public void ShakeLetters(float start, float duration, float amplitude)
        {
            foreach (Letter l in letters)
            {
                l.ShakeStart = start;
                l.ShakeDuration = duration;
                l.ShakeAmplitude = amplitude;
            }
        }

        /// <summary>Schedules a letter's exit. Gold motes fly to <paramref name="target"/> (fx layer units) spread by <paramref name="spread"/>.</summary>
        public void ExitLetter(int i, LetterExit exit, float at, Vector2 target, Vector2 spread)
        {
            if (i < 0 || i >= letters.Count) return;
            Letter l = letters[i];
            l.Exit = exit;
            l.ExitAt = at;
            l.ExitSpawned = false;
            l.MoteTarget = target;
            l.MoteSpread = spread;
        }

        /// <summary>Centre of letters [from, to) in fx layer units.</summary>
        public Vector2 LetterCenter(int from, int to)
        {
            if (from < 0 || to <= from || to > letters.Count) return Vector2.zero;
            float x = (letters[from].X + letters[to - 1].X) * 0.5f;
            return new Vector2((x - textWidth * 0.5f) * textRow.localScale.x, 0f);
        }

        /// <summary>Lays out the hieroglyph row for the words (hidden until each sign's appear time). Returns false if no sign resolved.</summary>
        public bool BuildGlyphRow(IReadOnlyList<SpokenWord> words)
        {
            ClearGlyphs();
            float E = GlyphEm;
            float groupGap = 0.42f * E;
            float x = 0f;
            for (int w = 0; w < words.Count; w++)
            {
                SpokenWord word = words[w];
                var g = new SignGroup { Cartouche = word.Cartouche };
                float scale = word.Cartouche ? 0.86f : 1f;
                float e = E * scale;
                var columns = new List<List<Sign>>();
                var colInkH = new List<float>();
                var colW = new List<float>();
                foreach (string raw in word.Signs)
                {
                    if (!GlyphAtlas.Has(raw)) { GlyphAtlas.Get(raw); continue; }
                    Rect ink = GlyphAtlas.InkRect(raw);
                    float aspect = GlyphAtlas.Aspect(raw);
                    float inkW = ink.width * aspect * e;
                    float inkH = ink.height * e;
                    Sign s = NewSign(raw, e, aspect);
                    s.Group = g;
                    g.Signs.Add(s);
                    bool flat = inkH < 0.46f * e;
                    int last = columns.Count - 1;
                    bool stack = flat && last >= 0 && columns[last].Count < 3 && AllFlat(columns[last], e)
                                 && colInkH[last] + inkH + 0.1f * e <= 0.82f * e
                                 && Mathf.Abs(colW[last] - inkW) < 0.6f * Mathf.Max(colW[last], inkW) + 0.25f * e;
                    if (stack)
                    {
                        columns[last].Add(s);
                        colInkH[last] += inkH + 0.1f * e;
                        colW[last] = Mathf.Max(colW[last], inkW);
                    }
                    else
                    {
                        columns.Add(new List<Sign> { s });
                        colInkH.Add(inkH);
                        colW.Add(inkW);
                    }
                }
                if (g.Signs.Count == 0) { groups.Add(g); continue; } // keeps group index == word index

                float colGap = 0.12f * e;
                float innerW = 0f;
                for (int c = 0; c < columns.Count; c++) innerW += colW[c] + (c > 0 ? colGap : 0f);
                float padL = 0f, padR = 0f;
                if (g.Cartouche)
                {
                    g.FrameHeight = 1.14f * E;
                    padL = 0.40f * g.FrameHeight;
                    padR = 0.55f * g.FrameHeight;
                }
                g.Width = padL + innerW + padR;
                if (x > 0f) x += groupGap;
                float left = x;
                g.CenterX = left + g.Width * 0.5f;
                float cx = left + padL;
                for (int c = 0; c < columns.Count; c++)
                {
                    float colCenter = cx + colW[c] * 0.5f;
                    float total = colInkH[c];
                    float y = total * 0.5f;
                    foreach (Sign s in columns[c])
                    {
                        Rect ink = GlyphAtlas.InkRect(s.Code);
                        float inkH = ink.height * s.Size;
                        float inkCy = y - inkH * 0.5f;
                        if (columns[c].Count == 1) inkCy = 0f;
                        // Move the em box so the ink centre lands on (colCenter, inkCy).
                        float inkCxInBox = (ink.center.x - 0.5f) * s.Aspect * s.Size;
                        float inkCyInBox = (ink.center.y - 0.5f) * s.Size;
                        s.Position = new Vector2(colCenter - inkCxInBox, inkCy - inkCyInBox);
                        y -= inkH + 0.1f * s.Size;
                    }
                    cx += colW[c] + colGap;
                }
                x = left + g.Width;
                groups.Add(g);
            }
            glyphRowWidth = x;
            float shift = -glyphRowWidth * 0.5f;
            foreach (SignGroup g in groups)
            {
                g.CenterX += shift;
                foreach (Sign s in g.Signs)
                {
                    s.Position.x += shift;
                    s.RowX01 = glyphRowWidth > 1f ? (s.Position.x - shift) / glyphRowWidth : 0.5f;
                    s.Rect.anchoredPosition = s.Position;
                }
                if (g.Cartouche && g.Signs.Count > 0)
                {
                    g.FrameGlow = Sliced("Cartouche Glow", glyphRow, GlyphAtlas.CartoucheGlow, new Color(MagicPalette.Gold.r, MagicPalette.Gold.g, MagicPalette.Gold.b, 0f), 256f / g.FrameHeight);
                    g.Frame = Sliced("Cartouche", glyphRow, GlyphAtlas.Cartouche, new Color(MagicPalette.Gold.r, MagicPalette.Gold.g, MagicPalette.Gold.b, 0f), 256f / g.FrameHeight);
                    foreach (Image im in new[] { g.FrameGlow, g.Frame })
                    {
                        im.rectTransform.anchoredPosition = new Vector2(g.CenterX, 0f);
                        im.rectTransform.sizeDelta = new Vector2(g.Width, g.FrameHeight);
                        im.rectTransform.SetAsFirstSibling();
                    }
                }
            }
            glyphPhase = signs.Count > 0;
            return signs.Count > 0;
        }

        private static bool AllFlat(List<Sign> col, float e)
        {
            foreach (Sign s in col) if (GlyphAtlas.InkRect(s.Code).height * e >= 0.46f * e) return false;
            return true;
        }

        private Sign NewSign(string code, float size, float aspect)
        {
            var s = new Sign { Code = code, Size = size, Aspect = aspect, Phase = UnityEngine.Random.value * 6.28f };
            s.Rect = NewRect("Sign " + code, glyphRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            s.Rect.sizeDelta = new Vector2(size * aspect, size);
            s.Glow = Simple("Glow", s.Rect, GlyphAtlas.GetGlow(code), new Color(1f, 1f, 1f, 0f));
            Stretch(s.Glow.rectTransform);
            s.Core = Simple("Core", s.Rect, GlyphAtlas.Get(code), new Color(1f, 1f, 1f, 0f));
            Stretch(s.Core.rectTransform);
            s.Rect.localScale = Vector3.zero;
            signs.Add(s);
            return s;
        }

        /// <summary>Glyph group centre in fx layer units (where its gold motes converge).</summary>
        public Vector2 GroupCenter(int index)
        {
            if (index < 0 || index >= groups.Count) return Vector2.zero;
            return new Vector2(groups[index].CenterX * glyphRow.localScale.x, 0f);
        }

        public float GroupWidth(int index) => index < 0 || index >= groups.Count ? 0f : groups[index].Width * glyphRow.localScale.x;

        /// <summary>Scales the glyph row so it fits on screen. Call once after <see cref="BuildGlyphRow"/>.</summary>
        public void FitGlyphRow()
        {
            float max = Mathf.Max(200f, canvasRect.rect.width * 0.82f - PadX * 2f);
            float s = glyphRowWidth > max ? max / glyphRowWidth : 1f;
            glyphRow.localScale = new Vector3(s, s, 1f);
        }

        public void ScheduleSign(Sign s, float at) => s.AppearAt = at;
        public void ScheduleGroupFrame(SignGroup g, float at) => g.AppearAt = at;
        public void StartShimmer(float at) => shimmerStart = at;
        public void StartAnticipation(float at) => anticipateAt = at;

        /// <summary>Hides a sign at launch (it continues as a world quad) and flashes its cartouche away.</summary>
        public void LaunchSign(Sign s)
        {
            s.LaunchedAt = Now;
            if (s.Group != null && s.Group.FadeAt > Now) s.Group.FadeAt = Now;
            // A small flash where the sign left.
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = SignFxPosition(s);
                SpawnFreeMote(p, UnityEngine.Random.insideUnitCircle * 90f + Vector2.up * 40f, UnityEngine.Random.Range(5f, 9f), MagicPalette.GoldPale, 0.45f);
            }
        }

        private Vector2 SignFxPosition(Sign s) => s.Position * glyphRow.localScale.x;

        /// <summary>Screen pixel position of a sign's em box centre (overlay canvas, so world position is pixels).</summary>
        public Vector2 SignScreenPoint(Sign s) => RectTransformUtility.WorldToScreenPoint(null, s.Rect.position);

        /// <summary>Screen pixel height of a sign's em box at rest scale.</summary>
        public float SignScreenHeight(Sign s)
        {
            float scale = canvas.scaleFactor * glyphRow.localScale.y * anchor.localScale.y;
            return s.Size * scale;
        }

        /// <summary>Shows the quiet reply line.</summary>
        public void ShowReply(string message, float at)
        {
            reply.text = message ?? string.Empty;
            replyAt = string.IsNullOrEmpty(message) ? float.PositiveInfinity : at;
        }

        /// <summary>Collapses the main row (after the failed words have blown away).</summary>
        public void CollapseMainRow() => collapseMain = true;

        /// <summary>Fades the bubble out, then clears it.</summary>
        public void Hide()
        {
            if (!visible) return;
            typing = false;
            pendingSince = -1f;
            if (hideStart < 0f) hideStart = Now;
        }

        /// <summary>Clears and hides immediately.</summary>
        public void HideImmediate()
        {
            ClearContent();
            visible = false;
            hideStart = -1f;
            if (group != null) group.alpha = 0f;
            if (canvas != null) canvas.enabled = false;
        }

        private void ShowNow()
        {
            if (!visible || hideStart >= 0f)
            {
                visibleSince = Now;
                hasPosition = false;
            }
            visible = true;
            hideStart = -1f;
            canvas.enabled = true;
        }

        private void ClearContent()
        {
            for (int i = letters.Count - 1; i >= 0; i--) Recycle(i);
            text = string.Empty;
            textWidth = 0f;
            SetLiveGloss(null);
            ClearGlyphs();
            foreach (Mote m in motes) { m.Active = false; m.Image.enabled = false; }
            glyphPhase = false;
            collapseMain = false;
            shimmerStart = -1f;
            anticipateAt = float.PositiveInfinity;
            replyAt = float.PositiveInfinity;
            reply.text = string.Empty;
            pendingSince = -1f;
            textRow.localScale = Vector3.one;
            glyphRow.localScale = Vector3.one;
        }

        private void ClearGlyphs()
        {
            foreach (SignGroup g in groups)
            {
                if (g.Frame != null) Destroy(g.Frame.gameObject);
                if (g.FrameGlow != null) Destroy(g.FrameGlow.gameObject);
            }
            foreach (Sign s in signs) if (s.Rect != null) Destroy(s.Rect.gameObject);
            groups.Clear();
            signs.Clear();
            glyphRowWidth = 0f;
            glyphPhase = false;
        }

        // ------------------------------------------------------------------ frame update

        private void LateUpdate()
        {
            if (!visible) return;
            float now = Now;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            float appear = Ease.OutBack(Ease.Progress(now, visibleSince, 0.28f), 1.4f);
            float alpha = Ease.OutCubic(Ease.Progress(now, visibleSince, 0.18f));
            float scale = Mathf.Lerp(0.9f, 1f, appear);
            if (hideStart >= 0f)
            {
                float h = Ease.Progress(now, hideStart, 0.32f);
                alpha *= 1f - Ease.InQuad(h);
                scale *= Mathf.Lerp(1f, 0.96f, Ease.OutCubic(h));
                if (h >= 1f)
                {
                    HideImmediate();
                    return;
                }
            }
            group.alpha = alpha;
            anchor.localScale = new Vector3(scale, scale, 1f);

            UpdateLetters(now);
            UpdateSigns(now);
            UpdateMotes(now, dt);
            UpdateLayout(now, dt);
            UpdatePosition(dt);
        }

        private Color CurrentTint(Letter l, float now)
        {
            float p = Ease.Progress(now, l.TintStart, l.TintDuration);
            return Color.Lerp(l.TintFrom, l.TintTo, Ease.OutCubic(p));
        }

        private void UpdateLetters(float now)
        {
            bool pending = pendingSince >= 0f && !glyphPhase;
            int n = letters.Count;
            for (int i = 0; i < n; i++)
            {
                Letter l = letters[i];
                if (l.Gone) continue;
                Color col = CurrentTint(l, now);
                float a = 1f;
                Vector2 off = Vector2.zero;
                float sc = 1f;

                if (l.PopIn)
                {
                    float pa = Ease.Progress(now, l.Born, 0.2f);
                    a *= Ease.OutCubic(pa);
                    off.y -= (1f - Ease.OutCubic(pa)) * 8f;
                    sc *= Mathf.LerpUnclamped(1.25f, 1f, Ease.OutBack(pa, 2f));
                    col = Color.Lerp(skin.InkFresh, col, Ease.Progress(now, l.Born, 0.4f));
                }
                if (pending)
                {
                    // Words weighed: a slow gold breath travels along the line.
                    float wave = 0.5f + 0.5f * Mathf.Sin(now * 4f - i * 0.35f);
                    col = Color.Lerp(col, skin.Weighing, (skin.InkThenGild ? 0.45f : 0.22f) * wave);
                }
                if (l.ShakeStart >= 0f && now >= l.ShakeStart && now < l.ShakeStart + l.ShakeDuration)
                {
                    float t = now - l.ShakeStart;
                    float k = 1f - t / l.ShakeDuration;
                    k *= k;
                    off.x += l.ShakeAmplitude * Mathf.Sin(t * 62f + i * 1.7f) * k;
                    off.y += l.ShakeAmplitude * 0.45f * Mathf.Sin(t * 47f + i * 2.3f) * k;
                }
                if (now >= l.ExitAt)
                {
                    if (!l.ExitSpawned)
                    {
                        l.ExitSpawned = true;
                        SpawnExitMotes(l);
                    }
                    float dur = l.Exit == LetterExit.Gold ? 0.28f : (l.Exit == LetterExit.Sand ? 0.24f : 0.45f);
                    float pd = Ease.Progress(now, l.ExitAt, dur);
                    switch (l.Exit)
                    {
                        case LetterExit.Gold:
                            col = Color.Lerp(col, MagicPalette.WhiteHot, Ease.OutCubic(pd * 2f));
                            sc *= 1f + 0.22f * Ease.OutCubic(pd);
                            off.y += 5f * pd;
                            a *= 1f - Ease.InQuad(pd);
                            break;
                        case LetterExit.Sand:
                            a *= 1f - Ease.OutQuad(pd);
                            off.y -= 5f * pd;
                            break;
                        default:
                            a *= 1f - Ease.OutCubic(pd);
                            off.y += 7f * pd;
                            break;
                    }
                    if (pd >= 1f)
                    {
                        l.Gone = true;
                        l.Text.enabled = false;
                        continue;
                    }
                }
                col.a *= a;
                l.Text.color = col;
                l.Rect.anchoredPosition = new Vector2(l.X - CentreW * 0.5f, 0f) + off;
                l.Rect.localScale = new Vector3(sc, sc, 1f);
            }

            // Caret and placeholder.
            bool showCaret = typing;
            if (showCaret)
            {
                float since = now - lastTypeTime;
                float blink = since < 0.5f ? 1f : 0.5f + 0.5f * Mathf.Cos((since - 0.5f) * Mathf.PI * 2f / 1.05f);
                Color c = skin.Caret;
                c.a = Mathf.Clamp01(blink * 1.2f);
                caret.color = c;
                float x = ghostOn ? textWidth - CentreW * 0.5f + (letters.Count > 0 ? 3f : -6f)
                    : letters.Count > 0 ? textWidth * 0.5f + 3f : -placeholder.preferredWidth * 0.5f - 6f;
                caret.rectTransform.anchoredPosition = new Vector2(x, 0f);
            }
            caret.enabled = showCaret;
            bool showPlaceholder = typing && letters.Count == 0 && !ghostOn;
            if (ghostOn) ghost.rectTransform.anchoredPosition = new Vector2(textWidth - CentreW * 0.5f, 0f);
            placeholder.enabled = showPlaceholder;
            if (showPlaceholder)
            {
                placeholder.rectTransform.anchoredPosition = Vector2.zero;
                placeholder.rectTransform.sizeDelta = new Vector2(placeholder.preferredWidth + 10f, FontSize * 1.6f);
            }
        }

        private void UpdateSigns(float now)
        {
            float shimmerP = shimmerStart >= 0f ? (now - shimmerStart) / ShimmerSeconds : -1f;
            foreach (Sign s in signs)
            {
                if (now < s.AppearAt || now >= s.LaunchedAt)
                {
                    if (s.Rect.localScale.x != 0f) s.Rect.localScale = Vector3.zero;
                    continue;
                }
                float pa = Ease.Progress(now, s.AppearAt, 0.5f);
                float sc = Mathf.LerpUnclamped(0.3f, 1f, Ease.OutBack(pa, 2.2f));
                float a = Ease.OutCubic(Ease.Progress(now, s.AppearAt, 0.18f));
                Vector2 off = new Vector2(0f, -(1f - Ease.OutCubic(pa)) * 12f);
                Color core;
                float glow;
                if (skin.InkThenGild)
                {
                    // Written first: a wet gold flash where the motes land, settling into carbon ink. Then the
                    // shimmer gilds the ink, and the gilded sign is what flies.
                    float settle = Ease.OutCubic(Ease.Progress(now, s.AppearAt, 0.45f));
                    core = Color.Lerp(skin.SignGold, skin.Ink, settle);
                    float gild = shimmerStart >= 0f ? Ease.OutCubic(Mathf.Clamp01((now - shimmerStart - (s.RowX01 * 0.35f)) / 0.45f)) : 0f;
                    core = Color.Lerp(core, skin.SignGold, gild);
                    glow = 0.55f * (1f - settle) + gild * (0.55f + 0.06f * Mathf.Sin(now * 3.1f + s.Phase));
                }
                else
                {
                    core = Color.Lerp(MagicPalette.WhiteHot, MagicPalette.Gold, Ease.OutCubic(Ease.Progress(now, s.AppearAt + 0.05f, 0.55f)));
                    glow = 0.42f + 0.58f * (1f - Ease.OutCubic(Ease.Progress(now, s.AppearAt, 0.7f)));
                    glow += 0.07f * Mathf.Sin(now * 3.1f + s.Phase);
                }
                if (shimmerP >= 0f && shimmerP <= 1.6f)
                {
                    float band = Mathf.Lerp(-0.25f, 1.25f, Ease.InOutSine(Mathf.Clamp01(shimmerP)));
                    float d = (s.RowX01 - band) / 0.14f;
                    float h = Mathf.Exp(-d * d);
                    core = Color.Lerp(core, MagicPalette.WhiteHot, h * (skin.InkThenGild ? 0.55f : 0.85f));
                    glow += h * 0.55f;
                    sc *= 1f + 0.06f * h;
                }
                if (now >= anticipateAt)
                {
                    float dip = Ease.OutCubic(Ease.Progress(now, anticipateAt, AnticipateSeconds));
                    off.y -= 6f * dip;
                    sc *= 1f - 0.07f * dip;
                    glow += 0.35f * dip;
                    core = Color.Lerp(core, MagicPalette.WhiteHot, 0.5f * dip);
                }
                core.a = a;
                s.Core.color = core;
                Color g = MagicPalette.Gold;
                g.a = Mathf.Clamp01(glow) * a;
                s.Glow.color = g;
                s.Rect.anchoredPosition = s.Position + off;
                s.Rect.localScale = new Vector3(sc, sc, 1f);
            }
            foreach (SignGroup g in groups)
            {
                if (g.Frame == null) continue;
                float pa = Ease.Progress(now, g.AppearAt, 0.45f);
                float a = Ease.OutCubic(pa);
                float fade = Ease.Progress(now, g.FadeAt, 0.35f);
                a *= 1f - fade;
                float sx = Mathf.LerpUnclamped(0.75f, 1f, Ease.OutBack(pa, 1.6f)) * (1f + 0.08f * Ease.OutCubic(fade));
                Color c;
                if (skin.InkThenGild)
                {
                    c = Color.Lerp(skin.SignGold, skin.Ink, Ease.OutCubic(Ease.Progress(now, g.AppearAt, 0.45f)));
                    if (shimmerStart >= 0f) c = Color.Lerp(c, skin.SignGold, Ease.OutCubic(Mathf.Clamp01((now - shimmerStart) / 0.5f)));
                }
                else
                {
                    c = Color.Lerp(MagicPalette.WhiteHot, MagicPalette.Gold, Ease.OutCubic(Ease.Progress(now, g.AppearAt + 0.1f, 0.5f)));
                }
                c = Color.Lerp(c, MagicPalette.WhiteHot, fade);
                c.a = a * 0.95f;
                g.Frame.color = c;
                Color gc = MagicPalette.Gold;
                gc.a = a * (0.45f + 0.4f * fade + 0.06f * Mathf.Sin(now * 2.7f));
                if (skin.InkThenGild && shimmerStart < 0f) gc.a *= 0.25f;
                g.FrameGlow.color = gc;
                Vector3 ls = new Vector3(sx, 1f, 1f);
                g.Frame.rectTransform.localScale = ls;
                g.FrameGlow.rectTransform.localScale = ls;
            }
        }

        private void UpdateLayout(float now, float dt)
        {
            // Target sizes.
            float textScale = 1f;
            float maxContent = Mathf.Max(200f, canvasRect.rect.width * 0.82f - PadX * 2f);
            if (textWidth > maxContent) textScale = maxContent / textWidth;
            textRow.localScale = new Vector3(textScale, textScale, 1f);

            float contentW;
            if (typing)
            {
                contentW = ghostOn ? CentreW * textScale + 12f : letters.Count > 0 ? textWidth * textScale + 12f : placeholder.preferredWidth + 16f;
                contentW = Mathf.Max(contentW, hint.preferredWidth);
            }
            else if (glyphPhase)
            {
                contentW = glyphRowWidth * glyphRow.localScale.x;
                if (!AllLettersGone()) contentW = Mathf.Max(contentW, textWidth * textScale);
            }
            else
            {
                contentW = collapseMain ? 0f : textWidth * textScale;
            }
            bool replyOn = now >= replyAt;
            float replyW = 0f, replyH = 0f;
            if (replyOn)
            {
                RectTransform rr = reply.rectTransform;
                replyW = Mathf.Min(ReplyMaxWidth, reply.preferredWidth + 4f);
                rr.sizeDelta = new Vector2(replyW, 10f);
                replyH = reply.preferredHeight + (collapseMain ? 0f : 8f);
                contentW = Mathf.Max(contentW, replyW);
            }
            float targetW = Mathf.Max(MinWidth, contentW + PadX * 2f);
            float targetMain = collapseMain ? 0f : (glyphPhase ? GlyphEm * 1.28f * glyphRow.localScale.y : FontSize * 1.55f);
            if (typing && liveGlossWords > 0 && !collapseMain && !glyphPhase) targetMain += LiveExtraHeight * textRow.localScale.y;
            float targetHint = typing ? HintHeight : 0f;

            float k = 1f - Mathf.Exp(-dt * 14f);
            if (now - visibleSince < 0.02f) k = 1f;
            curW = Mathf.Lerp(curW, targetW, k);
            curMain = Mathf.Lerp(curMain, targetMain, k);
            curReply = Mathf.Lerp(curReply, replyOn ? replyH : 0f, k);
            curHint = Mathf.Lerp(curHint, targetHint, k);
            float H = PadBottom + curHint + curReply + curMain + PadTop;
            float W = curW;

            body.sizeDelta = new Vector2(W, H);
            SetFrame(panel, W, H, 0f, 0f);
            SetFrame(border, W, H, 0f, 0f);
            SetFrame(shadow, W, H, 44f, -10f);
            SetFrame(lapisGlow, W, H, 30f, 0f);

            // Border glint and sheen sweep.
            float gPeriod = 3.6f;
            float gp = ((now - visibleSince) % gPeriod) / gPeriod;
            float gx = Mathf.Lerp(-W * 0.5f - 120f, W * 0.5f + 120f, Ease.InOutSine(Mathf.Clamp01(gp * 1.4f)));
            glintWindow.sizeDelta = new Vector2(140f, H);
            glintWindow.anchoredPosition = new Vector2(gx, 0f);
            glintBorder.sizeDelta = new Vector2(W, H);
            glintBorder.anchoredPosition = new Vector2(-gx, 0f);

            sheenMask.sizeDelta = new Vector2(Mathf.Max(0f, W - 16f), Mathf.Max(0f, H - 6f));
            sheenMask.anchoredPosition = new Vector2(0f, 3f);
            float sPeriod = 5.2f;
            float sp = ((now - visibleSince + 1.1f) % sPeriod) / sPeriod;
            sheen.sizeDelta = new Vector2(240f, H + 20f);
            sheen.anchoredPosition = new Vector2(Mathf.Lerp(-W * 0.5f - 260f, W * 0.5f + 260f, Mathf.Clamp01(sp * 1.7f)), -10f);

            hairline.sizeDelta = new Vector2(Mathf.Max(0f, W - 70f), 1.6f);
            hairline.anchoredPosition = new Vector2(0f, -11f);
            lozenge.anchoredPosition = new Vector2(0f, -11f);
            float hairA = 0.42f * Mathf.Clamp01(curMain / (FontSize * 1.2f));
            Color rule = skin.RuleColor;
            hairlineImage.color = new Color(rule.r, rule.g, rule.b, hairA * rule.a);
            lozengeImage.color = new Color(rule.r, rule.g, rule.b, Mathf.Min(1f, hairA * 2f) * rule.a);
            Color bc = skin.BorderColor;
            borderImage.color = new Color(bc.r, bc.g, bc.b, Mathf.Clamp01(bc.a + skin.BorderPulse * Mathf.Sin(now * 1.7f)));

            float mainY = PadBottom + curHint + curReply + curMain * 0.5f;
            textRow.anchoredPosition = new Vector2(0f, mainY);
            glyphRow.anchoredPosition = new Vector2(0f, mainY);
            fxLayer.anchoredPosition = new Vector2(0f, mainY);

            reply.enabled = replyOn;
            if (replyOn)
            {
                RectTransform rr = reply.rectTransform;
                rr.sizeDelta = new Vector2(replyW, reply.preferredHeight);
                rr.anchoredPosition = new Vector2(0f, PadBottom + curHint + curReply * 0.5f);
                Color rc = skin.Reply;
                rc.a *= Ease.OutCubic(Ease.Progress(now, replyAt, 0.45f));
                reply.color = rc;
            }

            hint.enabled = curHint > 2f;
            if (hint.enabled)
            {
                hint.rectTransform.sizeDelta = new Vector2(hint.preferredWidth + 10f, HintHeight);
                hint.rectTransform.anchoredPosition = new Vector2(0f, PadBottom + curHint * 0.5f - 3f);
                Color hc = skin.Hint;
                // The word tray is read, not glanced at, so it gets ink at near full strength.
                bool tray = trayShown != null && trayShown.Count > 0;
                if (tray) hc = skin.Ink;
                hc.a = (tray ? 0.95f : 0.62f) * Mathf.Clamp01(curHint / HintHeight);
                hint.color = hc;
            }
        }

        private bool AllLettersGone()
        {
            foreach (Letter l in letters) if (!l.Gone && !char.IsWhiteSpace(l.Char)) return false;
            return true;
        }

        private void UpdatePosition(float dt)
        {
            Camera cam = FxQuad.ResolveCamera(CameraSource != null ? CameraSource() : null);
            Rect area = canvasRect.rect;
            float W = body.sizeDelta.x, H = body.sizeDelta.y;
            Vector2 target = tip;
            bool have = false;
            if (cam != null && Head != null)
            {
                Vector3 sp = cam.WorldToScreenPoint(Head.position + HeadOffset);
                if (sp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out Vector2 local))
                {
                    target = local;
                    have = true;
                }
            }
            if (!have && !hasPosition)
            {
                target = new Vector2(0f, area.height * 0.12f);
                have = true;
            }
            target.x = Mathf.Clamp(target.x, area.xMin + ScreenMargin + 24f, area.xMax - ScreenMargin - 24f);
            target.y = Mathf.Clamp(target.y, area.yMin + ScreenMargin, area.yMax - ScreenMargin - TailH - H);
            if (!hasPosition || (target - tip).magnitude > SnapDistance)
            {
                tip = target;
                hasPosition = true;
            }
            else
            {
                tip = Vector2.Lerp(tip, target, 1f - Mathf.Exp(-dt * 22f));
            }
            anchor.anchoredPosition = tip;

            float half = W * 0.5f;
            float minX = area.xMin + ScreenMargin + half - tip.x;
            float maxX = area.xMax - ScreenMargin - half - tip.x;
            float bodyX = minX > maxX ? (minX + maxX) * 0.5f : Mathf.Clamp(0f, minX, maxX);
            body.anchoredPosition = new Vector2(bodyX, TailH);
            float tw = skin.TailSize.x > 0f ? skin.TailSize.x : TailW;
            float th = skin.TailSize.y > 0f ? skin.TailSize.y : TailH;
            float up = skin.TailOverlap;
            float tailLimit = Mathf.Max(0f, half - CornerUnits - tw * 0.5f - 4f);
            float tailX = Mathf.Clamp(-bodyX, -tailLimit, tailLimit);
            // The tail hangs from its top edge with its apex on the tip (TailH below the body). A taller skin tail
            // reaches further up, and the overlap tucks its top into the sheet past the torn edge.
            SetAnchor(tailGold, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
            tailGold.sizeDelta = new Vector2(tw, th + up);
            tailGold.anchoredPosition = new Vector2(tailX, 0.6f - TailH + th + up);
            SetAnchor(tailFill, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
            tailFill.sizeDelta = new Vector2(tw - 3.6f, th + up - 0.4f);
            tailFill.anchoredPosition = new Vector2(tailX, 2.4f - TailH + th + up);
        }

        // ------------------------------------------------------------------ UI motes

        private void SpawnExitMotes(Letter l)
        {
            if (char.IsWhiteSpace(l.Char)) return;
            Vector2 p = new Vector2((l.X - textWidth * 0.5f) * textRow.localScale.x, 0f);
            float now = Now;
            switch (l.Exit)
            {
                case LetterExit.Gold:
                    for (int i = 0; i < 5; i++)
                    {
                        Mote m = RentMote();
                        m.Kind = 0;
                        m.P0 = p + UnityEngine.Random.insideUnitCircle * FontSize * 0.3f;
                        Vector2 end = l.MoteTarget + new Vector2(UnityEngine.Random.Range(-0.5f, 0.5f) * l.MoteSpread.x, UnityEngine.Random.Range(-0.5f, 0.5f) * l.MoteSpread.y);
                        m.P1 = Vector2.Lerp(m.P0, end, 0.3f) + new Vector2(UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(25f, 70f));
                        m.P2 = end;
                        m.Born = now;
                        m.Life = UnityEngine.Random.Range(0.42f, 0.7f);
                        m.Size = UnityEngine.Random.Range(5f, 11f);
                        m.Color = Color.Lerp(MagicPalette.Gold, MagicPalette.WhiteHot, UnityEngine.Random.value * 0.7f);
                        m.Image.sprite = i == 0 ? GlyphAtlas.SparkSprite : GlyphAtlas.MoteSprite;
                        if (i == 0) m.Size *= 2.4f;
                    }
                    SpawnFreeMote(p, new Vector2(UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(40f, 90f)), 6f, MagicPalette.GoldPale, 0.6f);
                    break;
                case LetterExit.Sand:
                    for (int i = 0; i < 8; i++)
                    {
                        Mote m = RentMote();
                        m.Kind = 2;
                        m.Pos = p + new Vector2(UnityEngine.Random.Range(-0.4f, 0.4f) * l.Advance, UnityEngine.Random.Range(-0.35f, 0.35f) * FontSize);
                        m.Vel = new Vector2(UnityEngine.Random.Range(-40f, 70f), UnityEngine.Random.Range(-10f, 70f));
                        m.Born = now + UnityEngine.Random.Range(0f, 0.08f);
                        m.Life = UnityEngine.Random.Range(0.8f, 1.3f);
                        m.Size = UnityEngine.Random.Range(2.5f, 5.5f);
                        float v = UnityEngine.Random.Range(-0.08f, 0.08f);
                        m.Color = new Color(skin.Crumble.r + v, skin.Crumble.g + v, skin.Crumble.b + v * 0.8f, 0.9f);
                        m.Spin = UnityEngine.Random.Range(-360f, 360f);
                        m.Image.sprite = GlyphAtlas.GrainSprite;
                    }
                    break;
                default:
                    SpawnFreeMote(p, new Vector2(UnityEngine.Random.Range(-20f, 20f), UnityEngine.Random.Range(20f, 50f)), 5f, new Color(MagicPalette.GoldPale.r, MagicPalette.GoldPale.g, MagicPalette.GoldPale.b, 0.5f), 0.7f);
                    break;
            }
        }

        private void SpawnFreeMote(Vector2 p, Vector2 v, float size, Color c, float life)
        {
            Mote m = RentMote();
            m.Kind = 1;
            m.Pos = p;
            m.Vel = v;
            m.Born = Now;
            m.Life = life;
            m.Size = size;
            m.Color = c;
            m.Image.sprite = GlyphAtlas.MoteSprite;
        }

        private Mote RentMote()
        {
            Mote m = null;
            foreach (Mote x in motes) if (!x.Active) { m = x; break; }
            if (m == null)
            {
                if (motes.Count >= 480)
                {
                    m = motes[UnityEngine.Random.Range(0, motes.Count)];
                }
                else
                {
                    m = new Mote();
                    m.Image = Simple("Mote", fxLayer, GlyphAtlas.MoteSprite, Color.clear);
                    m.Rect = m.Image.rectTransform;
                    motes.Add(m);
                }
            }
            m.Active = true;
            m.Rotation = UnityEngine.Random.Range(0f, 360f);
            m.Spin = 0f;
            m.Image.enabled = false;
            return m;
        }

        private void UpdateMotes(float now, float dt)
        {
            foreach (Mote m in motes)
            {
                if (!m.Active) continue;
                float age = now - m.Born;
                if (age < 0f) { m.Image.enabled = false; continue; }
                float t = age / m.Life;
                if (t >= 1f)
                {
                    m.Active = false;
                    m.Image.enabled = false;
                    continue;
                }
                float a, size = m.Size;
                Vector2 pos;
                switch (m.Kind)
                {
                    case 0:
                        float e = Ease.InOutCubic(t);
                        float u = 1f - e;
                        pos = u * u * m.P0 + 2f * u * e * m.P1 + e * e * m.P2;
                        a = Ease.OutCubic(t * 6f) * (t > 0.8f ? 1f - (t - 0.8f) / 0.2f : 1f);
                        size *= t > 0.75f ? Mathf.Lerp(1f, 1.6f, (t - 0.75f) / 0.25f) : 1f;
                        break;
                    case 2:
                        m.Vel.y -= 820f * dt;
                        m.Vel.x += 170f * dt;
                        m.Pos += m.Vel * dt;
                        m.Rotation += m.Spin * dt;
                        pos = m.Pos;
                        a = 1f - Ease.InQuad(t);
                        break;
                    default:
                        m.Vel *= Mathf.Exp(-dt * 2.2f);
                        m.Vel.y += 26f * dt;
                        m.Pos += m.Vel * dt;
                        pos = m.Pos;
                        a = Ease.OutCubic(t * 5f) * (1f - Ease.InQuad(t));
                        size *= 1f - 0.5f * t;
                        break;
                }
                m.Image.enabled = true;
                Color c = m.Color;
                c.a *= a;
                m.Image.color = c;
                m.Rect.anchoredPosition = pos;
                m.Rect.sizeDelta = new Vector2(size, size);
                m.Rect.localRotation = Quaternion.Euler(0f, 0f, m.Rotation);
            }
        }

        // ------------------------------------------------------------------ letters pool

        private Letter Rent()
        {
            Letter l = letterPool.Count > 0 ? letterPool.Pop() : null;
            if (l == null)
            {
                l = new Letter();
                l.Text = Label("Letter", textRow, bodyFont, FontSize, skin.Ink, TextAnchor.MiddleCenter);
                l.Text.supportRichText = false;
                l.Rect = l.Text.rectTransform;
                SetAnchor(l.Rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            }
            l.Text.enabled = true;
            l.Text.color = new Color(1f, 1f, 1f, 0f);
            l.TintFrom = l.TintTo = skin.Ink;
            l.TintStart = 0f;
            l.TintDuration = 0f;
            l.ExitAt = float.PositiveInfinity;
            l.ExitSpawned = false;
            l.Gone = false;
            l.ShakeStart = -1f;
            l.Rect.localScale = Vector3.one;
            return l;
        }

        private void Recycle(int index)
        {
            Letter l = letters[index];
            letters.RemoveAt(index);
            l.Text.enabled = false;
            letterPool.Push(l);
        }

        private float AdvanceOf(char c)
        {
            if (bodyFont != null && bodyFont.GetCharacterInfo(c, out CharacterInfo ci, FontSize, FontStyle.Normal) && ci.advance > 0)
            {
                return ci.advance;
            }
            return char.IsWhiteSpace(c) ? FontSize * 0.28f : FontSize * 0.5f;
        }

        // ------------------------------------------------------------------ uGUI helpers

        private static RectTransform NewRect(string name, Transform parent, Vector2 anchorPoint, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            SetAnchor(r, anchorPoint, pivot);
            return r;
        }

        private static void SetAnchor(RectTransform r, Vector2 anchorPoint, Vector2 pivot)
        {
            r.anchorMin = anchorPoint;
            r.anchorMax = anchorPoint;
            r.pivot = pivot;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private static void SetFrame(RectTransform r, float w, float h, float grow, float yOffset)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(w + grow * 2f, h + grow * 2f);
            r.anchoredPosition = new Vector2(0f, h * 0.5f + yOffset);
        }

        private static Image Simple(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform r = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            return img;
        }

        private static Image Sliced(string name, Transform parent, Sprite sprite, Color color, float ppuMultiplier)
        {
            Image img = Simple(name, parent, sprite, color);
            if (sprite != null && sprite.border != Vector4.zero)
            {
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, ppuMultiplier);
            }
            return img;
        }

        private static Text Label(string name, Transform parent, Font font, int size, Color color, TextAnchor align)
        {
            RectTransform r = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var t = r.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = false;
            return t;
        }
    }
}
