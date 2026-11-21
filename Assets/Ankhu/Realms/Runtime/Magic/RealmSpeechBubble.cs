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
        public const float GlyphEm = 72f;
        private const float PadX = 30f;
        private const float PadTop = 20f;
        private const float PadBottom = 17f;
        private const float TailH = 17f;
        private const float TailW = 26f;
        private const float CornerUnits = 15f;
        private const float MinWidth = 230f;
        private const float ScreenMargin = 18f;
        private const float HintHeight = 24f;
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
        private Image caret, borderImage, hairlineImage, lozengeImage;
        private Text placeholder, hint, reply;

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

            float mult = 28f / CornerUnits;   // panel sprite corners are 28 px, shown as about 15 units
            shadow = Sliced("Shadow", body, GlyphAtlas.UiShadow, new Color(0f, 0f, 0f, 0.55f), 1.25f).rectTransform;
            lapisGlow = Sliced("Lapis Glow", body, GlyphAtlas.UiShadow, new Color(MagicPalette.LapisLight.r, MagicPalette.LapisLight.g, MagicPalette.LapisLight.b, 0.20f), 1.6f).rectTransform;
            tailGold = Simple("Tail Edge", body, GlyphAtlas.UiTail, new Color(MagicPalette.Gold.r, MagicPalette.Gold.g, MagicPalette.Gold.b, 0.85f)).rectTransform;
            panel = Sliced("Panel", body, GlyphAtlas.UiPanel, MagicPalette.Obsidian, mult).rectTransform;
            borderImage = Sliced("Border", body, GlyphAtlas.UiBorder, new Color(MagicPalette.Gold.r, MagicPalette.Gold.g, MagicPalette.Gold.b, 0.78f), mult);
            border = borderImage.rectTransform;

            glintWindow = NewRect("Border Glint", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            var glintMask = glintWindow.gameObject.AddComponent<RectMask2D>();
            glintMask.softness = new Vector2Int(70, 0);
            glintBorder = Sliced("Glint", glintWindow, GlyphAtlas.UiBorder, new Color(1f, 0.95f, 0.8f, 0.95f), mult).rectTransform;
            SetAnchor(glintBorder, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            sheenMask = NewRect("Sheen Mask", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            var sm = sheenMask.gameObject.AddComponent<RectMask2D>();
            sm.softness = new Vector2Int(36, 8);
            sheen = Simple("Sheen", sheenMask, GlyphAtlas.UiSheen, new Color(MagicPalette.Gold.r, MagicPalette.Gold.g, MagicPalette.Gold.b, 0.075f)).rectTransform;
            SetAnchor(sheen, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            hairlineImage = Simple("Hairline", body, GlyphAtlas.UiHairline, new Color(MagicPalette.Gold.r, MagicPalette.Gold.g, MagicPalette.Gold.b, 0.42f));
            hairline = hairlineImage.rectTransform;
            SetAnchor(hairline, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
            lozengeImage = Simple("Lozenge", body, null, MagicPalette.Gold);
            lozenge = lozengeImage.rectTransform;
            SetAnchor(lozenge, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
            lozenge.sizeDelta = new Vector2(6.5f, 6.5f);
            lozenge.localRotation = Quaternion.Euler(0f, 0f, 45f);

            tailFill = Simple("Tail Fill", body, GlyphAtlas.UiTail, MagicPalette.Obsidian).rectTransform;

            textRow = NewRect("Text Row", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            glyphRow = NewRect("Glyph Row", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            fxLayer = NewRect("UI Motes", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            caret = Simple("Caret", textRow, null, MagicPalette.Gold);
            caret.rectTransform.sizeDelta = new Vector2(2.4f, FontSize * 0.95f);

            placeholder = Label("Placeholder", textRow, bodyFont, FontSize, new Color(MagicPalette.Quiet.r, MagicPalette.Quiet.g, MagicPalette.Quiet.b, 0.5f), TextAnchor.MiddleCenter);
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.text = Placeholder;

            reply = Label("Reply", body, bodyFont, ReplyFontSize, MagicPalette.Quiet, TextAnchor.MiddleCenter);
            reply.fontStyle = FontStyle.Italic;
            reply.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetAnchor(reply.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            hint = Label("Hint", body, capsFont, 14, MagicPalette.Quiet, TextAnchor.MiddleCenter);
            hint.supportRichText = true;
            string k = ColorUtility.ToHtmlStringRGB(MagicPalette.Gold);
            hint.text = "<color=#" + k + ">ENTER</color>  speak      <color=#" + k + ">ESC</color>  cancel      <color=#" + k + ">UP</color>  recall";
            SetAnchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            canvas.enabled = false;
        }

        // ------------------------------------------------------------------ public control: typing

        /// <summary>Opens the bubble for typing, empty.</summary>
        public void BeginTyping()
        {
            ClearContent();
            typing = true;
            lastTypeTime = Now;
            pendingSince = -1f;
            ShowNow();
        }

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
        }

        /// <summary>Typing ended. keepText true leaves the words on show while the rules judge them.</summary>
        public void EndTyping(bool keepText)
        {
            typing = false;
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
                    col = Color.Lerp(MagicPalette.GoldPale, col, Ease.Progress(now, l.Born, 0.4f));
                }
                if (pending)
                {
                    // Words weighed: a slow gold breath travels along the line.
                    float wave = 0.5f + 0.5f * Mathf.Sin(now * 4f - i * 0.35f);
                    col = Color.Lerp(col, MagicPalette.GoldPale, 0.22f * wave);
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
                l.Rect.anchoredPosition = new Vector2(l.X - textWidth * 0.5f, 0f) + off;
                l.Rect.localScale = new Vector3(sc, sc, 1f);
            }

            // Caret and placeholder.
            bool showCaret = typing;
            if (showCaret)
            {
                float since = now - lastTypeTime;
                float blink = since < 0.5f ? 1f : 0.5f + 0.5f * Mathf.Cos((since - 0.5f) * Mathf.PI * 2f / 1.05f);
                Color c = MagicPalette.Gold;
                c.a = Mathf.Clamp01(blink * 1.2f);
                caret.color = c;
                float x = letters.Count > 0 ? textWidth * 0.5f + 3f : -placeholder.preferredWidth * 0.5f - 6f;
                caret.rectTransform.anchoredPosition = new Vector2(x, 0f);
            }
            caret.enabled = showCaret;
            bool showPlaceholder = typing && letters.Count == 0;
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
                Color core = Color.Lerp(MagicPalette.WhiteHot, MagicPalette.Gold, Ease.OutCubic(Ease.Progress(now, s.AppearAt + 0.05f, 0.55f)));
                float glow = 0.42f + 0.58f * (1f - Ease.OutCubic(Ease.Progress(now, s.AppearAt, 0.7f)));
                glow += 0.07f * Mathf.Sin(now * 3.1f + s.Phase);
                if (shimmerP >= 0f && shimmerP <= 1.6f)
                {
                    float band = Mathf.Lerp(-0.25f, 1.25f, Ease.InOutSine(Mathf.Clamp01(shimmerP)));
                    float d = (s.RowX01 - band) / 0.14f;
                    float h = Mathf.Exp(-d * d);
                    core = Color.Lerp(core, MagicPalette.WhiteHot, h * 0.85f);
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
                Color c = Color.Lerp(MagicPalette.WhiteHot, MagicPalette.Gold, Ease.OutCubic(Ease.Progress(now, g.AppearAt + 0.1f, 0.5f)));
                c = Color.Lerp(c, MagicPalette.WhiteHot, fade);
                c.a = a * 0.95f;
                g.Frame.color = c;
                Color gc = MagicPalette.Gold;
                gc.a = a * (0.45f + 0.4f * fade + 0.06f * Mathf.Sin(now * 2.7f));
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
                contentW = letters.Count > 0 ? textWidth * textScale + 12f : placeholder.preferredWidth + 16f;
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
            hairlineImage.color = new Color(MagicPalette.Gold.r, MagicPalette.Gold.g, MagicPalette.Gold.b, hairA);
            lozengeImage.color = new Color(MagicPalette.Gold.r, MagicPalette.Gold.g, MagicPalette.Gold.b, hairA * 2f);
            borderImage.color = new Color(MagicPalette.Gold.r, MagicPalette.Gold.g, MagicPalette.Gold.b, 0.70f + 0.08f * Mathf.Sin(now * 1.7f));

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
                Color rc = MagicPalette.Quiet;
                rc.a *= Ease.OutCubic(Ease.Progress(now, replyAt, 0.45f));
                reply.color = rc;
            }

            hint.enabled = curHint > 2f;
            if (hint.enabled)
            {
                hint.rectTransform.sizeDelta = new Vector2(hint.preferredWidth + 10f, HintHeight);
                hint.rectTransform.anchoredPosition = new Vector2(0f, PadBottom + curHint * 0.5f - 3f);
                Color hc = MagicPalette.Quiet;
                hc.a = 0.62f * Mathf.Clamp01(curHint / HintHeight);
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
            float tailLimit = Mathf.Max(0f, half - CornerUnits - TailW * 0.5f - 4f);
            float tailX = Mathf.Clamp(-bodyX, -tailLimit, tailLimit);
            SetAnchor(tailGold, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
            tailGold.sizeDelta = new Vector2(TailW, TailH);
            tailGold.anchoredPosition = new Vector2(tailX, 0.6f);
            SetAnchor(tailFill, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
            tailFill.sizeDelta = new Vector2(TailW - 3.6f, TailH - 0.4f);
            tailFill.anchoredPosition = new Vector2(tailX, 2.4f);
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
                        m.Color = new Color(MagicPalette.Sand.r + v, MagicPalette.Sand.g + v, MagicPalette.Sand.b + v * 0.8f, 0.9f);
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
                l.Text = Label("Letter", textRow, bodyFont, FontSize, MagicPalette.Ink, TextAnchor.MiddleCenter);
                l.Text.supportRichText = false;
                l.Rect = l.Text.rectTransform;
                SetAnchor(l.Rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            }
            l.Text.enabled = true;
            l.Text.color = new Color(1f, 1f, 1f, 0f);
            l.TintFrom = l.TintTo = MagicPalette.Ink;
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
