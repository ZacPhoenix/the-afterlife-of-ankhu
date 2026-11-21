using System;
using System.Collections;
using System.Collections.Generic;
using Ankhu.Opening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ankhu.Realms
{
    /// <summary>
    /// Anpu's codex, the journal Ankhu writes in. One DontDestroyOnLoad singleton with its own overlay canvas
    /// (sort order 500): the open book, the HUD icon with its unread badge, and the "written in the codex" toasts.
    /// Built entirely from code. Call <see cref="Ensure"/> once from any realm bootstrap.
    ///
    /// Controls: Tab or C toggles, Escape closes, A/D, Left/Right, Q/E or the mouse wheel turn pages, a click
    /// on a page corner turns it, Home jumps to the spell pages. Gamepad: View (Select) toggles, B closes,
    /// shoulders or d-pad turn, Y jumps to the spell pages. Nothing reacts while <see cref="InputBlocked"/> is set.
    /// </summary>
    public sealed class CodexBook : MonoBehaviour
    {
        public const int SortOrder = 500;

        private static CodexBook instance;

        /// <summary>True from the moment the book starts opening until it has finished closing.</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>Raised with true when the book starts to open and false when it is fully closed. Freeze gameplay on it.</summary>
        public static event Action<bool> OpenChanged;

        /// <summary>Raised when a page turn starts, with the new spread index (for a paper sound).</summary>
        public static event Action<int> PageTurned;

        /// <summary>Set while another UI owns the keyboard (the speech bubble), so typing "c" does not open the book.</summary>
        public static bool InputBlocked { get; set; }

        /// <summary>Hide the HUD icon (title screen, cutscenes). The book and toasts still work.</summary>
        public static bool HudVisible { get; set; } = true;

        /// <summary>True when the last input came from a gamepad. Hints show gamepad buttons then.</summary>
        public static bool GamepadMode { get; private set; }

        /// <summary>Frame on which Escape (or B) closed the book. A pause menu should ignore Escape on this frame.</summary>
        public static int EscapeConsumedFrame { get; private set; } = -1;

        public static CodexBook Instance => instance;

        /// <summary>True while the pages are on screen and readable (ink may dry, seals may fade).</summary>
        public static bool PagesVisible => instance != null && (instance.state == State.Open || instance.state == State.Turning);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            IsOpen = false;
            OpenChanged = null;
            PageTurned = null;
            InputBlocked = false;
            HudVisible = true;
            GamepadMode = false;
            EscapeConsumedFrame = -1;
        }

        /// <summary>Create the codex UI if it does not exist yet. Safe to call every scene.</summary>
        public static CodexBook Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("AnkhuCodex");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CodexBook>();
            instance.Build();
            return instance;
        }

        public static void Open() { Ensure().RequestOpen(null); }

        /// <summary>Open the book at the spread holding this entry.</summary>
        public static void OpenAt(string entryId) { Ensure().RequestOpen(entryId); }

        public static void Close() { if (instance != null) instance.RequestClose(); }

        public static void Toggle()
        {
            CodexBook b = Ensure();
            if (IsOpen) b.RequestClose(); else b.RequestOpen(null);
        }

        // ================================================================== construction

        private enum State { Closed, Opening, Open, Turning, Closing }

        private const float W = CodexLayout.PageW;
        private const float H = CodexLayout.PageH;
        private const float Board = 22f;

        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform layer;
        private CanvasGroup layerGroup;
        private CanvasGroup dimGroup;
        private RectTransform bookRoot;
        private CanvasGroup bookGroup;
        private RectTransform boardLeft, boardRight, coverFront;
        private RectTransform slotLeft, slotRight, leafFront, leafBack, pool;
        private Image shadeFront, shadeBack, coverShade, boardShade;
        private RawImage sweepLeft, sweepRight;
        private readonly RectTransform[] fanFront = new RectTransform[3];
        private readonly RectTransform[] fanBack = new RectTransform[3];
        private readonly Image[] fanShadeFront = new Image[3];
        private readonly Image[] fanShadeBack = new Image[3];
        private RectTransform zoneLeft, zoneRight, zoneLeftTop, zoneRightTop;
        private RawImage curlLeft, curlRight;
        private float curlLeftSize, curlRightSize;
        private RectTransform hints;
        private bool hintsGamepad;
        private CanvasGroup hintsGroup;
        private CodexHud hud;
        private CodexToast toast;

        private CodexLayout layout;
        private int builtCount = -1;
        private readonly Dictionary<int, CodexPages.Built> cache = new Dictionary<int, CodexPages.Built>();
        private readonly Dictionary<RectTransform, int> slotPage = new Dictionary<RectTransform, int>();
        private int spread;
        private State state = State.Closed;
        private bool closeQueued;
        private int turnQueued;
        private float readTimer;
        private bool spreadMarked;
        private float wheelCooldown;
        private bool prevCursorVisible;
        private CursorLockMode prevCursorLock;

        private void Build()
        {
            canvas = UiKit.Canvas("CodexCanvas", SortOrder, transform);
            canvasRect = (RectTransform)canvas.transform;

            hud = CodexHud.Build(canvasRect);

            layer = CodexArt.Rect("BookLayer", canvasRect);
            CodexPages.UiStretch(layer);
            layerGroup = layer.gameObject.AddComponent<CanvasGroup>();
            // Blocks clicks to other UI while the book is open (gameplay is frozen anyway).
            layerGroup.blocksRaycasts = true;

            // The world behind: darkened, warm, vignetted.
            RectTransform dim = CodexArt.Rect("Dim", layer);
            CodexPages.UiStretch(dim);
            dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            Image dark = CodexArt.Solid("Dark", dim, new Color(0.02f, 0.012f, 0.008f, 0.66f));
            dark.raycastTarget = true;
            CodexPages.UiStretch(dark.rectTransform);
            Image warm = CodexArt.Solid("Warm", dim, new Color(0.28f, 0.16f, 0.06f, 0.10f));
            CodexPages.UiStretch(warm.rectTransform);
            RawImage vig = CodexArt.Raw("Vignette", dim, CodexArt.Tex("vignette"), Color.white);
            CodexPages.UiStretch(vig.rectTransform);
            RawImage glow = CodexArt.Raw("Glow", dim, UiKit.SoftDot(64, 1.6f), new Color(1f, 0.72f, 0.35f, 0.07f));
            CodexArt.Centered(glow.rectTransform, Vector2.zero, new Vector2(1900f, 1300f));

            bookRoot = CodexArt.Rect("Book", layer);
            CodexArt.Centered(bookRoot, Vector2.zero, new Vector2((W + Board) * 2f, H + Board * 2f));
            bookGroup = bookRoot.gameObject.AddComponent<CanvasGroup>();

            Image shadow = CodexArt.SlicedImage("Shadow", bookRoot, CodexArt.Sliced("book_shadow", new Vector4(100, 100, 100, 100)), new Color(0f, 0f, 0f, 0.85f), 0.5f);
            CodexArt.Centered(shadow.rectTransform, new Vector2(0f, -22f), new Vector2((W + Board) * 2f + 150f, H + Board * 2f + 140f));

            boardLeft = BuildBoard("BoardLeft", true);
            boardRight = BuildBoard("BoardRight", false);

            slotLeft = Slot("SlotLeft", boardLeft, true);
            slotLeft.anchorMin = slotLeft.anchorMax = new Vector2(1f, 0.5f);
            slotLeft.anchoredPosition = Vector2.zero;
            slotRight = Slot("SlotRight", boardRight, false);
            slotRight.anchorMin = slotRight.anchorMax = new Vector2(0f, 0.5f);
            slotRight.anchoredPosition = Vector2.zero;

            // The crease in the middle.
            Image crease = CodexArt.Solid("Crease", bookRoot, new Color(0.10f, 0.05f, 0.02f, 0.45f));
            CodexArt.Centered(crease.rectTransform, Vector2.zero, new Vector2(3f, H));
            CodexGradient.Add(crease, new Color(1, 1, 1, 0.3f), new Color(1, 1, 1, 0.3f), true);

            sweepLeft = CodexArt.Raw("SweepLeft", bookRoot, CodexArt.Tex("page_gutter"), new Color(1, 1, 1, 0));
            sweepLeft.rectTransform.anchorMin = sweepLeft.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            sweepLeft.rectTransform.pivot = new Vector2(1f, 0.5f);
            sweepLeft.uvRect = new UnityEngine.Rect(1f, 0f, -1f, 1f);
            sweepRight = CodexArt.Raw("SweepRight", bookRoot, CodexArt.Tex("page_gutter"), new Color(1, 1, 1, 0));
            sweepRight.rectTransform.anchorMin = sweepRight.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            sweepRight.rectTransform.pivot = new Vector2(0f, 0.5f);

            // Fan leaves for the opening. The first to turn lies on top on the right, the last to land on top on the left.
            for (int i = 2; i >= 0; i--)
            {
                fanFront[i] = Slot("FanFront" + i, bookRoot, false);
                CodexPages.Blank(fanFront[i], false);
                fanShadeFront[i] = Shade(fanFront[i]);
                fanFront[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < 3; i++)
            {
                fanBack[i] = Slot("FanBack" + i, bookRoot, true);
                CodexPages.Blank(fanBack[i], true);
                fanShadeBack[i] = Shade(fanBack[i]);
                fanBack[i].gameObject.SetActive(false);
            }

            leafFront = Slot("LeafFront", bookRoot, false);
            leafBack = Slot("LeafBack", bookRoot, true);
            shadeFront = Shade(leafFront);
            shadeBack = Shade(leafBack);
            leafFront.gameObject.SetActive(false);
            leafBack.gameObject.SetActive(false);

            coverFront = CodexArt.Rect("Cover", bookRoot);
            coverFront.anchorMin = coverFront.anchorMax = new Vector2(0.5f, 0.5f);
            coverFront.pivot = new Vector2(0f, 0.5f);
            coverFront.sizeDelta = new Vector2(W + Board, H + Board * 2f);
            RawImage coverTex = CodexArt.Raw("Leather", coverFront, CodexArt.Tex("cover_leather"), Color.white);
            CodexPages.UiStretch(coverTex.rectTransform);
            if (!CodexArt.HasTex("cover_leather")) coverTex.color = new Color(0.22f, 0.12f, 0.07f, 1f);
            coverShade = Shade(coverFront);
            boardShade = Shade(boardLeft);

            zoneLeft = Zone("ZoneLeft", new Vector2(0f, 0f), new Vector2(-W - Board, -H * 0.5f - Board));
            zoneRight = Zone("ZoneRight", new Vector2(1f, 0f), new Vector2(W + Board, -H * 0.5f - Board));
            zoneLeftTop = Zone("ZoneLeftTop", new Vector2(0f, 1f), new Vector2(-W - Board, H * 0.5f + Board));
            zoneRightTop = Zone("ZoneRightTop", new Vector2(1f, 1f), new Vector2(W + Board, H * 0.5f + Board));
            curlRight = Curl("CurlRight", false);
            curlLeft = Curl("CurlLeft", true);

            pool = CodexArt.Rect("Pool", bookRoot);
            pool.gameObject.SetActive(false);

            hints = CodexArt.Rect("Hints", layer);
            hints.anchorMin = hints.anchorMax = new Vector2(0.5f, 0f);
            hints.pivot = new Vector2(0.5f, 0f);
            hints.anchoredPosition = new Vector2(0f, 14f);
            hints.sizeDelta = new Vector2(900f, 34f);
            hintsGroup = hints.gameObject.AddComponent<CanvasGroup>();
            BuildHints(false);

            RectTransform toastLayer = CodexArt.Rect("Toasts", canvasRect);
            CodexPages.UiStretch(toastLayer);
            toast = gameObject.AddComponent<CodexToast>();
            toast.Init(toastLayer);

            layer.gameObject.SetActive(false);
        }

        private RectTransform BuildBoard(string name, bool left)
        {
            RectTransform b = CodexArt.Rect(name, bookRoot);
            b.anchorMin = b.anchorMax = new Vector2(0.5f, 0.5f);
            b.pivot = new Vector2(left ? 1f : 0f, 0.5f);
            b.anchoredPosition = Vector2.zero;
            b.sizeDelta = new Vector2(W + Board, H + Board * 2f);
            RawImage leather = CodexArt.Raw("Leather", b, CodexArt.Tex("board_leather"), Color.white);
            CodexPages.UiStretch(leather.rectTransform);
            if (left) leather.uvRect = new UnityEngine.Rect(1f, 0f, -1f, 1f);
            if (!CodexArt.HasTex("board_leather")) leather.color = new Color(0.20f, 0.11f, 0.06f, 1f);
            // Page block thickness: a few sheets peeking out at the fore edge and bottom.
            for (int k = 3; k >= 1; k--)
            {
                RawImage sheet = CodexArt.Raw("Sheet" + k, b, CodexArt.Tex("papyrus_page"), new Color(0.80f - k * 0.05f, 0.72f - k * 0.05f, 0.56f - k * 0.04f, 1f));
                RectTransform r = sheet.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(left ? 1f : 0f, 0.5f);
                r.pivot = new Vector2(left ? 1f : 0f, 0.5f);
                r.sizeDelta = new Vector2(W, H);
                r.anchoredPosition = new Vector2(left ? -k * 3f : k * 3f, -k * 2.6f);
                sheet.uvRect = left ? new UnityEngine.Rect(1f, 0f, -1f, 1f) : new UnityEngine.Rect(0f, 0f, 1f, 1f);
            }
            return b;
        }

        private static RectTransform Slot(string name, Transform parent, bool left)
        {
            RectTransform s = CodexArt.Rect(name, parent);
            s.anchorMin = s.anchorMax = new Vector2(0.5f, 0.5f);
            s.pivot = new Vector2(left ? 1f : 0f, 0.5f);
            s.anchoredPosition = Vector2.zero;
            s.sizeDelta = new Vector2(W, H);
            return s;
        }

        private static Image Shade(RectTransform parent)
        {
            Image s = CodexArt.Solid("Shade", parent, new Color(0.08f, 0.04f, 0.015f, 0f));
            CodexPages.UiStretch(s.rectTransform);
            return s;
        }

        private RectTransform Zone(string name, Vector2 corner, Vector2 pos)
        {
            RectTransform z = CodexArt.Rect(name, bookRoot);
            z.anchorMin = z.anchorMax = new Vector2(0.5f, 0.5f);
            z.pivot = corner;
            z.anchoredPosition = pos;
            z.sizeDelta = new Vector2(190f, 190f);
            return z;
        }

        private RawImage Curl(string name, bool left)
        {
            RawImage c = CodexArt.Raw(name, bookRoot, CodexArt.Tex("corner_curl"), Color.white);
            RectTransform r = c.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(left ? 0f : 1f, 0f);
            r.anchoredPosition = new Vector2(left ? -W : W, -H * 0.5f);
            r.sizeDelta = Vector2.zero;
            if (left) c.uvRect = new UnityEngine.Rect(1f, 0f, -1f, 1f);
            return c;
        }

        private void BuildHints(bool gamepad)
        {
            hintsGamepad = gamepad;
            for (int i = hints.childCount - 1; i >= 0; i--) Destroy(hints.GetChild(i).gameObject);
            var items = gamepad
                ? new[] { new[] { "LB", "RB" }, new[] { "Y" }, new[] { "B" } }
                : new[] { new[] { "Q", "E" }, new[] { "HOME" }, new[] { "TAB" } };
            string[] labels = { "Turn the page", "Words of power", "Close" };
            float x = 0f;
            var row = CodexArt.Rect("Row", hints);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0f, 0.5f);
            for (int i = 0; i < items.Length; i++)
            {
                foreach (string k in items[i]) x += CodexChips.Key(row, k, x, 26f) + 6f;
                x += 4f;
                Text t = CodexArt.Label("Label", row, labels[i], CodexArt.Body, 19, CodexArt.GoldDim, TextAnchor.MiddleLeft, FontStyle.Italic);
                float lw = CodexArt.MeasureWidth(labels[i], CodexArt.Body, 19, FontStyle.Italic) + 4f;
                RectTransform r = t.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2(x, 0f);
                r.sizeDelta = new Vector2(lw, 28f);
                x += lw + 38f;
            }
            x -= 38f;
            row.sizeDelta = new Vector2(x, 30f);
            row.anchoredPosition = new Vector2(-x * 0.5f, 0f);
        }

        // ================================================================== pages

        private void Repaginate()
        {
            foreach (KeyValuePair<int, CodexPages.Built> kv in cache)
            {
                if (kv.Value.Root != null) Destroy(kv.Value.Root.gameObject);
            }
            cache.Clear();
            slotPage.Clear();
            layout = CodexLayout.Build(CodexStore.Journal);
            builtCount = CodexStore.Journal.Order.Count;
            spread = Mathf.Clamp(spread, 0, Mathf.Max(0, layout.SpreadCount - 1));
        }

        private CodexPages.Built Page(int index)
        {
            if (index < 0 || layout == null || index >= layout.Pages.Count) return null;
            if (cache.TryGetValue(index, out CodexPages.Built b) && b.Root != null) return b;
            b = CodexPages.Build(layout, index, pool);
            cache[index] = b;
            return b;
        }

        /// <summary>Put page <paramref name="index"/> into a slot (or empty it with -1). A page lives in one slot at a time.</summary>
        private void Assign(RectTransform slot, int index)
        {
            if (slotPage.TryGetValue(slot, out int prev) && prev >= 0 && prev != index)
            {
                bool elsewhere = false;
                foreach (KeyValuePair<RectTransform, int> kv in slotPage)
                {
                    if (kv.Key != slot && kv.Value == prev) elsewhere = true;
                }
                if (!elsewhere && cache.TryGetValue(prev, out CodexPages.Built old) && old.Root != null)
                {
                    old.Root.gameObject.SetActive(false);
                    old.Root.SetParent(pool, false);
                }
            }
            slotPage[slot] = index;
            if (index < 0) return;
            var keys = new List<RectTransform>(slotPage.Keys);
            foreach (RectTransform k in keys)
            {
                if (k != slot && slotPage[k] == index) slotPage[k] = -1;
            }
            CodexPages.Built b = Page(index);
            if (b == null) return;
            b.Root.SetParent(slot, false);
            CodexPages.UiStretch(b.Root);
            b.Root.SetAsFirstSibling();
            b.Root.gameObject.SetActive(true);
        }

        private void ShowSpread(int s)
        {
            spread = s;
            Assign(slotLeft, s * 2);
            Assign(slotRight, s * 2 + 1);
            readTimer = 0f;
            spreadMarked = false;
        }

        private void SetContentAlpha(int index, float a)
        {
            if (cache.TryGetValue(index, out CodexPages.Built b) && b.Content != null) b.Content.alpha = a;
        }

        private int PickOpeningSpread(string entryId)
        {
            if (!string.IsNullOrEmpty(entryId))
            {
                int s = layout.SpreadOf(entryId);
                if (s >= 0) return s;
            }
            // Jump to the first unread entry, or else reopen where the reader left off.
            foreach (string id in CodexStore.Journal.Order)
            {
                if (!CodexStore.IsUnread(id)) continue;
                int s = layout.SpreadOf(id);
                if (s >= 0) return s;
            }
            return Mathf.Clamp(spread, 0, Mathf.Max(0, layout.SpreadCount - 1));
        }

        // ================================================================== open and close

        private void RequestOpen(string entryId)
        {
            if (state == State.Closing) { closeQueued = false; return; }
            if (state != State.Closed) return;
            Repaginate();
            int target = PickOpeningSpread(entryId);
            IsOpen = true;
            prevCursorVisible = Cursor.visible;
            prevCursorLock = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            OpenChanged?.Invoke(true);
            StartCoroutine(OpenRoutine(target));
        }

        private void RequestClose()
        {
            if (state == State.Open) StartCoroutine(CloseRoutine());
            else if (state == State.Opening || state == State.Turning) closeQueued = true;
        }

        private float FitScale()
        {
            Vector2 size = canvasRect.rect.size;
            if (size.x <= 1f || size.y <= 1f) return 1f;
            float bw = (W + Board) * 2f + 60f, bh = H + Board * 2f + 110f;
            return Mathf.Min(1.05f, size.x / bw, size.y / bh);
        }

        private Vector2 ClosedOffset(float s) => new Vector2(-(W + Board) * 0.5f * s, 0f);

        private IEnumerator OpenRoutine(int target)
        {
            state = State.Opening;
            layer.gameObject.SetActive(true);
            layerGroup.alpha = 1f;
            hintsGroup.alpha = 0f;
            float s = FitScale();
            Vector2 from = hud.IconCenter(canvasRect);
            float fromS = 0.07f;

            ShowSpread(target);
            SetContentAlpha(target * 2, 0f);
            SetContentAlpha(target * 2 + 1, 0f);
            slotLeft.gameObject.SetActive(false);
            SetScaleX(boardLeft, 0f);
            boardLeft.gameObject.SetActive(false);
            coverFront.gameObject.SetActive(true);
            SetScaleX(coverFront, 1f);
            coverShade.color = Shadow(0f);
            curlLeft.rectTransform.sizeDelta = curlRight.rectTransform.sizeDelta = Vector2.zero;
            curlLeftSize = curlRightSize = 0f;

            // 1. The closed codex rises from the HUD icon to the centre of the screen.
            const float fly = 0.42f;
            for (float t = 0f; t < fly; t += Time.unscaledDeltaTime)
            {
                float k = CodexEase.OutCubic(t / fly);
                bookRoot.anchoredPosition = Vector2.LerpUnclamped(from, ClosedOffset(s), k);
                float sc = Mathf.LerpUnclamped(fromS, s, CodexEase.OutBack(t / fly, 0.9f));
                bookRoot.localScale = new Vector3(sc, sc, 1f);
                bookRoot.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-9f, 0f, k));
                bookGroup.alpha = Mathf.Clamp01(t / 0.12f);
                dimGroup.alpha = k;
                yield return null;
            }
            bookRoot.anchoredPosition = ClosedOffset(s);
            bookRoot.localScale = new Vector3(s, s, 1f);
            bookRoot.localEulerAngles = Vector3.zero;
            bookGroup.alpha = 1f;
            dimGroup.alpha = 1f;

            // 2. The cover swings open, and the book slides to centre on its spine.
            boardLeft.gameObject.SetActive(true);
            yield return Flip(coverFront, boardLeft, true, 0.46f, coverShade, boardShade, k =>
                bookRoot.anchoredPosition = Vector2.Lerp(ClosedOffset(s), Vector2.zero, CodexEase.InOutCubic(k)));
            coverFront.gameObject.SetActive(false);
            bookRoot.anchoredPosition = Vector2.zero;

            // 3. A few blank leaves fan over.
            int landed = 0;
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                fanFront[idx].gameObject.SetActive(true);
                fanBack[idx].gameObject.SetActive(true);
                SetScaleX(fanFront[idx], 1f);
                SetScaleX(fanBack[idx], 0f);
                StartCoroutine(Chain(Flip(fanFront[idx], fanBack[idx], true, 0.36f - idx * 0.03f, fanShadeFront[idx], fanShadeBack[idx], null,
                    idx == 2 ? 0.7f : 0f), () =>
                {
                    landed++;
                    slotLeft.gameObject.SetActive(true);
                    fanFront[idx].gameObject.SetActive(false);
                    if (idx < 2) fanBack[idx].gameObject.SetActive(false);
                }));
                float wait = 0.085f;
                for (float t = 0f; t < wait; t += Time.unscaledDeltaTime) yield return null;
            }
            while (landed < 3) yield return null;
            fanBack[2].gameObject.SetActive(false);

            // 4. The writing appears.
            const float fade = 0.32f;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                float k = CodexEase.OutQuad(t / fade);
                SetContentAlpha(spread * 2, k);
                SetContentAlpha(spread * 2 + 1, k);
                hintsGroup.alpha = k;
                yield return null;
            }
            SetContentAlpha(spread * 2, 1f);
            SetContentAlpha(spread * 2 + 1, 1f);
            hintsGroup.alpha = 1f;
            state = State.Open;
            if (closeQueued) { closeQueued = false; StartCoroutine(CloseRoutine()); }
        }

        private IEnumerator CloseRoutine()
        {
            state = State.Closing;
            float s = bookRoot.localScale.x;
            const float fade = 0.14f;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / fade;
                SetContentAlpha(spread * 2, k);
                SetContentAlpha(spread * 2 + 1, k);
                hintsGroup.alpha = k;
                yield return null;
            }
            curlLeft.rectTransform.sizeDelta = curlRight.rectTransform.sizeDelta = Vector2.zero;

            coverFront.gameObject.SetActive(true);
            SetScaleX(coverFront, 0f);
            yield return Flip(coverFront, boardLeft, false, 0.40f, coverShade, boardShade, k =>
                bookRoot.anchoredPosition = Vector2.Lerp(Vector2.zero, ClosedOffset(s), CodexEase.InOutCubic(k)));
            boardLeft.gameObject.SetActive(false);

            Vector2 to = hud.IconCenter(canvasRect);
            const float fly = 0.36f;
            Vector2 start = bookRoot.anchoredPosition;
            for (float t = 0f; t < fly; t += Time.unscaledDeltaTime)
            {
                float k = CodexEase.InCubic(t / fly);
                bookRoot.anchoredPosition = Vector2.LerpUnclamped(start, to, k);
                float sc = Mathf.Lerp(s, 0.07f, k);
                bookRoot.localScale = new Vector3(sc, sc, 1f);
                bookRoot.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(0f, -9f, k));
                bookGroup.alpha = 1f - Mathf.Clamp01((t / fly - 0.75f) * 4f);
                dimGroup.alpha = 1f - CodexEase.OutQuad(t / fly);
                yield return null;
            }

            SetContentAlpha(spread * 2, 1f);
            SetContentAlpha(spread * 2 + 1, 1f);
            layer.gameObject.SetActive(false);
            state = State.Closed;
            closeQueued = false;
            turnQueued = 0;
            IsOpen = false;
            Cursor.visible = prevCursorVisible;
            Cursor.lockState = prevCursorLock;
            hud.Arrive();
            OpenChanged?.Invoke(false);
        }

        // ================================================================== page turns

        private bool CanTurn(int dir)
        {
            if (layout == null) return false;
            int next = spread + dir;
            return next >= 0 && next < layout.SpreadCount;
        }

        private void RequestTurn(int dir)
        {
            if (state == State.Turning)
            {
                if (Mathf.Abs(turnQueued + dir) <= 2) turnQueued += dir;
                return;
            }
            if (state != State.Open || !CanTurn(dir)) return;
            StartCoroutine(TurnRoutine(dir));
        }

        private void JumpTo(int target)
        {
            if (state != State.Open || layout == null) return;
            target = Mathf.Clamp(target, 0, layout.SpreadCount - 1);
            if (target == spread) return;
            StartCoroutine(TurnRoutine(target - spread));
        }

        /// <summary>Turn by <paramref name="delta"/> spreads. Larger jumps riffle one leaf and land on the target.</summary>
        private IEnumerator TurnRoutine(int delta)
        {
            state = State.Turning;
            int from = spread;
            int to = Mathf.Clamp(from + delta, 0, layout.SpreadCount - 1);
            bool forward = to > from;
            PageTurned?.Invoke(to);
            curlLeftSize = curlRightSize = 0f;
            curlLeft.rectTransform.sizeDelta = curlRight.rectTransform.sizeDelta = Vector2.zero;

            if (forward)
            {
                // Old right page lifts away to reveal the new right page, the new left page lands on the left.
                Assign(slotRight, to * 2 + 1);
                Assign(leafFront, from * 2 + 1);
                Assign(leafBack, to * 2);
                SetScaleX(leafFront, 1f);
                SetScaleX(leafBack, 0f);
            }
            else
            {
                Assign(slotLeft, to * 2);
                Assign(leafBack, from * 2);
                Assign(leafFront, to * 2 + 1);
                SetScaleX(leafBack, 1f);
                SetScaleX(leafFront, 0f);
            }
            leafFront.gameObject.SetActive(true);
            leafBack.gameObject.SetActive(true);
            SetContentAlpha(to * 2, 1f);
            SetContentAlpha(to * 2 + 1, 1f);

            float dur = Mathf.Abs(to - from) > 1 ? 0.62f : 0.52f;
            yield return Flip(leafFront, leafBack, forward, dur, shadeFront, shadeBack, null);

            if (forward) Assign(slotLeft, to * 2);
            else Assign(slotRight, to * 2 + 1);
            Assign(leafFront, -1);
            Assign(leafBack, -1);
            leafFront.gameObject.SetActive(false);
            leafBack.gameObject.SetActive(false);
            ShowSpread(to);
            state = State.Open;

            if (closeQueued) { closeQueued = false; turnQueued = 0; StartCoroutine(CloseRoutine()); yield break; }
            if (turnQueued != 0)
            {
                int q = turnQueued > 0 ? 1 : -1;
                turnQueued -= q;
                if (CanTurn(q)) StartCoroutine(TurnRoutine(q)); else turnQueued = 0;
            }
        }

        /// <summary>
        /// One leaf turning on the spine. Forward: the front (right side) folds to the spine, then the back unfolds
        /// on the left. The leaf lifts a little at mid turn, darkens edge on, and sweeps a shadow over the page below.
        /// </summary>
        private IEnumerator Flip(RectTransform front, RectTransform back, bool forward, float dur, Image fShade, Image bShade,
            Action<float> progress, float shadowStrength = 1f)
        {
            RectTransform first = forward ? front : back;
            RectTransform second = forward ? back : front;
            Image firstShade = forward ? fShade : bShade;
            Image secondShade = forward ? bShade : fShade;
            RawImage firstSweep = shadowStrength > 0f ? (forward ? sweepRight : sweepLeft) : null;
            RawImage secondSweep = shadowStrength > 0f ? (forward ? sweepLeft : sweepRight) : null;
            float lift = 0.035f;
            SetScaleX(first, 1f);
            SetScaleX(second, 0f);
            float half = dur * 0.5f;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / dur);
                progress?.Invoke(k);
                float ly = 1f + lift * Mathf.Sin(k * Mathf.PI);
                if (t < half)
                {
                    float sx = Mathf.Cos(CodexEase.InQuad(t / half) * Mathf.PI * 0.5f);
                    first.localScale = new Vector3(sx, ly, 1f);
                    second.localScale = new Vector3(0f, ly, 1f);
                    if (firstShade != null) firstShade.color = Shadow(0.55f * (1f - sx));
                    Sweep(firstSweep, sx, shadowStrength * 0.6f * Mathf.Sin((1f - sx) * Mathf.PI * 0.5f), forward);
                    Sweep(secondSweep, 0f, 0f, !forward);
                }
                else
                {
                    float u = (t - half) / half;
                    float sx = Mathf.Sin(CodexEase.OutQuad(u) * Mathf.PI * 0.5f);
                    first.localScale = new Vector3(0f, ly, 1f);
                    second.localScale = new Vector3(sx, ly, 1f);
                    if (secondShade != null) secondShade.color = Shadow(0.55f * (1f - sx));
                    Sweep(firstSweep, 0f, 0f, forward);
                    Sweep(secondSweep, sx, shadowStrength * 0.6f * (1f - sx), !forward);
                }
                yield return null;
            }
            progress?.Invoke(1f);
            first.localScale = new Vector3(0f, 1f, 1f);
            second.localScale = Vector3.one;
            if (firstShade != null) firstShade.color = Shadow(0f);
            if (secondShade != null) secondShade.color = Shadow(0f);
            Sweep(firstSweep, 0f, 0f, forward);
            Sweep(secondSweep, 0f, 0f, !forward);
        }

        /// <summary>Shadow strip cast on the page under a turning leaf, just beyond its free edge.</summary>
        private static void Sweep(RawImage sweep, float leafScale, float alpha, bool rightSide)
        {
            if (sweep == null) return;
            sweep.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            sweep.enabled = alpha > 0.002f;
            float edge = W * leafScale;
            RectTransform r = sweep.rectTransform;
            r.sizeDelta = new Vector2(Mathf.Lerp(60f, 220f, 1f - leafScale), H);
            r.anchoredPosition = new Vector2(rightSide ? edge : -edge, 0f);
        }

        private static IEnumerator Chain(IEnumerator a, Action then)
        {
            yield return a;
            then?.Invoke();
        }

        private static Color Shadow(float a) => new Color(0.08f, 0.04f, 0.015f, Mathf.Clamp01(a));

        private static void SetScaleX(RectTransform r, float x)
        {
            Vector3 s = r.localScale;
            r.localScale = new Vector3(x, 1f, s.z == 0f ? 1f : s.z);
        }

        // ================================================================== frame update

        private void Update()
        {
            ReadDevice();
            HandleInput();

            if (state == State.Open)
            {
                // Something was written while the book is open: lay it out again in place.
                if (builtCount != CodexStore.Journal.Order.Count)
                {
                    Repaginate();
                    ShowSpread(spread);
                }
                readTimer += Time.unscaledDeltaTime;
                if (!spreadMarked && readTimer > 1.8f)
                {
                    spreadMarked = true;
                    MarkSpreadRead();
                }
                UpdateCurls();
            }
            if (state != State.Closed)
            {
                float s = FitScale();
                if (state == State.Open || state == State.Turning) bookRoot.localScale = new Vector3(s, s, 1f);
            }
            if (hintsGamepad != GamepadMode && layer.gameObject.activeSelf) BuildHints(GamepadMode);
            hud.Tick(HudVisible && !IsOpen, CodexStore.UnreadCount);
        }

        private void MarkSpreadRead()
        {
            if (layout == null) return;
            var ids = new List<string>();
            for (int i = spread * 2; i <= spread * 2 + 1 && i < layout.Pages.Count; i++) ids.AddRange(layout.Pages[i].EntryIds);
            CodexStore.MarkRead(ids);
        }

        private void ReadDevice()
        {
            Gamepad gp = Gamepad.current;
            if (gp != null)
            {
                foreach (InputControl c in gp.allControls)
                {
                    if (c is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame) { GamepadMode = true; return; }
                }
            }
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) { GamepadMode = false; return; }
            Mouse m = Mouse.current;
            if (m != null && (m.leftButton.wasPressedThisFrame || m.delta.ReadValue().sqrMagnitude > 4f)) GamepadMode = false;
        }

        private void HandleInput()
        {
            if (InputBlocked) return;
            Keyboard kb = Keyboard.current;
            Gamepad gp = Gamepad.current;
            Mouse mouse = Mouse.current;

            bool toggle = (kb != null && (kb.tabKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame))
                          || (gp != null && gp.selectButton.wasPressedThisFrame);
            if (toggle)
            {
                if (IsOpen) RequestClose(); else RequestOpen(null);
                return;
            }

            if (!IsOpen)
            {
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && HudVisible && hud.Contains(mouse.position.ReadValue()))
                    RequestOpen(null);
                return;
            }

            bool escape = (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.buttonEast.wasPressedThisFrame);
            if (escape)
            {
                EscapeConsumedFrame = Time.frameCount;
                RequestClose();
                return;
            }

            bool next = (kb != null && (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame))
                        || (gp != null && (gp.rightShoulder.wasPressedThisFrame || gp.dpad.right.wasPressedThisFrame));
            bool prev = (kb != null && (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.pageUpKey.wasPressedThisFrame))
                        || (gp != null && (gp.leftShoulder.wasPressedThisFrame || gp.dpad.left.wasPressedThisFrame));
            bool home = (kb != null && kb.homeKey.wasPressedThisFrame) || (gp != null && gp.buttonNorth.wasPressedThisFrame);
            if (next) RequestTurn(1);
            else if (prev) RequestTurn(-1);
            else if (home) JumpTo(0);

            if (mouse == null || state != State.Open && state != State.Turning) return;
            wheelCooldown -= Time.unscaledDeltaTime;
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f && wheelCooldown <= 0f)
            {
                wheelCooldown = 0.28f;
                RequestTurn(wheel < 0f ? 1 : -1);
            }
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 p = mouse.position.ReadValue();
                if (Hit(zoneRight, p) || Hit(zoneRightTop, p)) RequestTurn(1);
                else if (Hit(zoneLeft, p) || Hit(zoneLeftTop, p)) RequestTurn(-1);
                else if (!Hit(bookRoot, p)) RequestClose();
            }
        }

        private void UpdateCurls()
        {
            Mouse mouse = Mouse.current;
            Vector2 p = mouse != null ? mouse.position.ReadValue() : new Vector2(-9999f, -9999f);
            bool overR = mouse != null && (Hit(zoneRight, p) || Hit(zoneRightTop, p));
            bool overL = mouse != null && (Hit(zoneLeft, p) || Hit(zoneLeftTop, p));
            float pulse = 6f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f));
            float tr = CanTurn(1) ? (overR ? 84f : 26f + pulse) : 0f;
            float tl = CanTurn(-1) ? (overL ? 84f : 0f) : 0f;
            curlRightSize = Mathf.Lerp(curlRightSize, tr, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            curlLeftSize = Mathf.Lerp(curlLeftSize, tl, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            curlRight.rectTransform.sizeDelta = new Vector2(curlRightSize, curlRightSize);
            curlLeft.rectTransform.sizeDelta = new Vector2(curlLeftSize, curlLeftSize);
        }

        private static bool Hit(RectTransform r, Vector2 screen) =>
            r != null && r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, screen, null);

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                if (IsOpen)
                {
                    IsOpen = false;
                    OpenChanged?.Invoke(false);
                }
            }
        }
    }
}
