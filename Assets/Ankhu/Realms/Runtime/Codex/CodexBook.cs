using System;
using System.Collections;
using System.Collections.Generic;
using Ankhu.Opening;
using Ankhu.Realms.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ankhu.Realms
{
    /// <summary>
    /// Ankhu's roll: his own Book of Going Forth by Day, which Anpu gives him blank at the First Gate. One
    /// DontDestroyOnLoad singleton with its own overlay canvas (sort order 500): the roll, the HUD icon with its
    /// unread badge, and the small "written" toasts. Built entirely from code. The class keeps its old name so
    /// every scene that called the bound codex keeps working.
    ///
    /// The roll unrolls left to right between two ebony spindles. The outer wrap on the left carries his name in
    /// a cartouche and the epithets he earns. Sheets are glued in the order he lived them. Chips above filter by
    /// section (Craft, Heart, Names, Account) or by person. His reed pen writes what matters on screen, strikes
    /// old lines in red and writes the true line beneath. Every known word is a button that drops into the
    /// speech bubble (<see cref="WordChosen"/>).
    ///
    /// Controls: Tab or C toggles, Escape closes, A/D, arrows, mouse wheel or drag scroll, Q/E change the filter,
    /// Home jumps to the newest word, End to the newest sheet, click a word to speak it. Gamepad: View toggles,
    /// B closes, d-pad or left stick scrolls, shoulders change the filter, d-pad up/down picks a word, A speaks it,
    /// Y jumps to the newest word. Nothing reacts while <see cref="InputBlocked"/> is set, except during a
    /// presented moment (<see cref="Present"/>), which the player always closes.
    /// </summary>
    public sealed partial class CodexBook : MonoBehaviour
    {
        public const int SortOrder = 500;

        private static CodexBook instance;

        /// <summary>True from the moment the roll starts opening until it has finished closing.</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>Raised with true when the roll starts to open and false when it is fully closed. Freeze gameplay on it.</summary>
        public static event Action<bool> OpenChanged;

        /// <summary>Raised when the roll unrolls or scrolls a sheet (for a papyrus sound), with the first visible sheet.</summary>
        public static event Action<int> PageTurned;

        /// <summary>
        /// The player chose a known word on the roll. Raised after the roll has finished closing, with the word as
        /// the parser accepts it ("bind", "hori"). The speech bubble inserts it.
        /// </summary>
        public static event Action<string> WordChosen;

        /// <summary>Set while another UI owns the keyboard (the speech bubble), so typing "c" does not open the roll.</summary>
        public static bool InputBlocked { get; set; }

        /// <summary>Hide the HUD icon (title screen, cutscenes). The roll and toasts still work.</summary>
        public static bool HudVisible { get; set; } = true;

        /// <summary>True when the last input came from a gamepad. Hints show gamepad buttons then.</summary>
        public static bool GamepadMode { get; private set; }

        /// <summary>Frame on which Escape (or B) closed the roll. A pause menu should ignore Escape on this frame.</summary>
        public static int EscapeConsumedFrame { get; private set; } = -1;

        public static CodexBook Instance => instance;

        /// <summary>True while the sheets are on screen and readable.</summary>
        public static bool PagesVisible => instance != null && instance.state == State.Open;

        /// <summary>True while the reed pen is writing.</summary>
        public static bool Scribing => instance != null && instance.scribing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            IsOpen = false;
            OpenChanged = null;
            PageTurned = null;
            nextOrigin = null;
            HallResults.Clear();
            HallLineRead = null;
            WordChosen = null;
            InputBlocked = false;
            HudVisible = true;
            GamepadMode = false;
            EscapeConsumedFrame = -1;
        }

        /// <summary>Create the roll UI if it does not exist yet. Safe to call every scene.</summary>
        public static CodexBook Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("AnkhuRoll");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CodexBook>();
            instance.Build();
            // Zac 2026-11-25: InputBlocked is set each frame by RealmSceneDirector and was left true when a realm
            // scene unloaded mid-freeze, so Tab did nothing in the teaching rooms and Borrowed Dawn, which have no
            // director to clear it. Every scene now starts unblocked and a director re-blocks it if needed.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => InputBlocked = false;
            return instance;
        }

        public static void Open() { Ensure().RequestOpen(null); }

        /// <summary>Open the roll at the sheet holding this entry.</summary>
        public static void OpenAt(string entryId) { Ensure().RequestOpen(entryId); }

        public static void Close() { if (instance != null) instance.RequestClose(); }

        public static void Toggle()
        {
            CodexBook b = Ensure();
            if (IsOpen) b.RequestClose(); else b.RequestOpen(null);
        }

        private static Vector2? nextOrigin;
        private static int nextOriginFrame = -1;

        /// <summary>
        /// The next open flies out from this screen point instead of the HUD icon (the 3D roll in Ankhu's hands
        /// sets it every frame while he holds it). Stale after a few frames.
        /// </summary>
        public static void SetNextOpenOrigin(Vector2 screen)
        {
            nextOrigin = screen;
            nextOriginFrame = Time.frameCount;
        }

        // ================================================================== presented moments

        /// <summary>
        /// A keystone: write the entry, open the roll at it, let his hand write it while his voice reads it,
        /// then wait for the player to close the roll. Yield on it from a scene beat. The very first presented
        /// entry also writes his name on the wrap.
        /// </summary>
        public static IEnumerator Present(string entryId)
        {
            CodexBook b = Ensure();
            if (!CodexStore.Journal.HasEpithet("ep.ren"))
            {
                b.Queue("ep:ep.ren");
                CodexStore.AddEpithet("ep.ren");
            }
            if (CodexStore.Has(entryId))
            {
                yield break;
            }
            b.Queue("entry:" + entryId);
            CodexStore.Write(entryId, true);
            yield return b.PresentRoutine(entryId);
        }

        /// <summary>Strike an old line in red and write the true one beneath it, on screen.</summary>
        public static IEnumerator PresentRevision(string revisionId)
        {
            CodexRevision r = CodexRollCatalog.Revision(revisionId);
            if (r == null || CodexStore.Journal.HasRevision(revisionId) || !CodexStore.Has(r.EntryId)) yield break;
            CodexBook b = Ensure();
            b.Queue("rev:" + revisionId);
            CodexStore.Revise(revisionId);
            yield return b.PresentRoutine(r.EntryId);
        }

        /// <summary>Write an epithet after his name on the wrap, on screen.</summary>
        public static IEnumerator PresentEpithet(string epithetId)
        {
            if (CodexRollCatalog.Epithet(epithetId) == null || CodexStore.Journal.HasEpithet(epithetId)) yield break;
            CodexBook b = Ensure();
            b.Queue("ep:" + epithetId);
            CodexStore.AddEpithet(epithetId);
            yield return b.PresentRoutine(null);
        }

        private IEnumerator PresentRoutine(string entryId)
        {
            presenting = true;
            if (state == State.Closed || state == State.Closing)
            {
                while (state == State.Closing) yield return null;
                RequestOpen(entryId);
            }
            else if (entryId != null)
            {
                dirty = true;
            }
            while (state != State.Open) yield return null;
            // Let the queue start, then wait for the pen to lift.
            yield return null;
            while (scribing || scribeQueue.Count > 0) yield return null;
            continueAt = Time.unscaledTime + 0.35f;
            while (state != State.Closed) yield return null;
            presenting = false;
        }

        private void Queue(string item)
        {
            if (!scribeQueue.Contains(item)) scribeQueue.Add(item);
        }

        // ================================================================== construction

        private enum State { Closed, Opening, Open, Closing }

        private const float SheetH = CodexRollSheets.SheetH;
        private const float SpindleW = 46f;
        private const float WrapW = 270f;
        private const float ViewW = 1340f;
        private const float TotalW = SpindleW + WrapW + ViewW + SpindleW;
        private const float TailW = 260f;

        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform layer;
        private CanvasGroup layerGroup;
        private CanvasGroup dimGroup;
        private RectTransform rollRoot;
        private CanvasGroup rollGroup;
        private RectTransform leftSpindle, rightSpindle, rightBulk;
        private RectTransform wrap;
        private RectTransform cord;
        private CanvasGroup cordGroup;
        private RectTransform viewport;
        private RectTransform content;
        private RectTransform curlShade;
        private RectTransform penLayer;
        private CodexPen pen;
        private RectTransform chipsRow;
        private CanvasGroup chipsGroup;
        private RectTransform hints;
        private CanvasGroup hintsGroup;
        private CanvasGroup continueGroup;
        private CodexHud hud;
        private CodexToast toast;
        private AudioSource voice;

        // Wrap content.
        private RectTransform wrapContent;
        private readonly List<Text> epithetTexts = new List<Text>();

        private readonly List<CodexSheetView> sheets = new List<CodexSheetView>();
        private readonly List<float> sheetX = new List<float>();
        private readonly List<CodexWordTag> words = new List<CodexWordTag>();
        private readonly List<string> filters = new List<string>();
        private readonly List<RectTransform> chipRects = new List<RectTransform>();
        private string filter = "All";
        private float contentW;
        private float scroll, scrollTarget, scrollVel;
        private float reveal;
        private State state = State.Closed;
        private bool closeQueued;
        private bool presenting;
        private bool scribing;
        private float continueAt = float.PositiveInfinity;
        private readonly List<string> scribeQueue = new List<string>();
        private readonly HashSet<string> dryQueue = new HashSet<string>();
        private string pendingWord;
        private bool dirty;
        private int wordCursor = -1;
        private bool dragging;
        private float dragLastX;
        private float readTimer;
        private int lastFirstVisible = -1;
        private bool prevCursorVisible;
        private CursorLockMode prevCursorLock;
        private bool hintsGamepad;
        private float skipArmedAt;

        private void Build()
        {
            canvas = UiKit.Canvas("RollCanvas", SortOrder, transform);
            canvasRect = (RectTransform)canvas.transform;

            hud = CodexHud.Build(canvasRect);

            layer = CodexArt.Rect("RollLayer", canvasRect);
            CodexPages.UiStretch(layer);
            layerGroup = layer.gameObject.AddComponent<CanvasGroup>();
            layerGroup.blocksRaycasts = true;

            // The world behind: darkened, warm, vignetted, with lamplight pooled where the roll lies.
            RectTransform dim = CodexArt.Rect("Dim", layer);
            CodexPages.UiStretch(dim);
            dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            Image dark = CodexArt.Solid("Dark", dim, new Color(0.02f, 0.012f, 0.008f, 0.78f));
            dark.raycastTarget = true;
            CodexPages.UiStretch(dark.rectTransform);
            Image warm = CodexArt.Solid("Warm", dim, new Color(0.28f, 0.16f, 0.06f, 0.10f));
            CodexPages.UiStretch(warm.rectTransform);
            RawImage vig = CodexArt.Raw("Vignette", dim, CodexArt.Tex("vignette"), Color.white);
            CodexPages.UiStretch(vig.rectTransform);
            RawImage glow = CodexArt.Raw("Lamp", dim, UiKit.SoftDot(64, 1.6f), new Color(1f, 0.72f, 0.35f, 0.09f));
            CodexArt.Centered(glow.rectTransform, Vector2.zero, new Vector2(2100f, 1250f));

            rollRoot = CodexArt.Rect("Roll", layer);
            CodexArt.Centered(rollRoot, Vector2.zero, new Vector2(TotalW, SheetH));
            rollGroup = rollRoot.gameObject.AddComponent<CanvasGroup>();

            Image shadow = CodexArt.SlicedImage("Shadow", rollRoot, CodexArt.Sliced("book_shadow", new Vector4(100, 100, 100, 100)), new Color(0f, 0f, 0f, 0.8f), 0.5f);
            shadow.rectTransform.anchorMin = new Vector2(0f, 0f);
            shadow.rectTransform.anchorMax = new Vector2(0f, 1f);
            shadow.rectTransform.pivot = new Vector2(0f, 0.5f);
            shadow.rectTransform.anchoredPosition = new Vector2(-60f, -26f);
            shadow.rectTransform.sizeDelta = new Vector2(TotalW + 120f, 150f);
            shadowRect = shadow.rectTransform;

            // The viewport: papyrus revealed between the wrap and the right spindle.
            viewport = CodexArt.Rect("Viewport", rollRoot);
            viewport.anchorMin = viewport.anchorMax = new Vector2(0f, 0.5f);
            viewport.pivot = new Vector2(0f, 0.5f);
            viewport.anchoredPosition = new Vector2(SpindleW + WrapW, 0f);
            viewport.sizeDelta = new Vector2(0f, SheetH);
            viewport.gameObject.AddComponent<RectMask2D>();
            // Blank papyrus still to be written on, behind the sheets (the inner fibre of the texture, never its torn edge).
            RawImage viewBack = CodexArt.Raw("Back", viewport, CodexArt.Tex("papyrus_page"), new Color(0.93f, 0.91f, 0.86f, 1f));
            CodexPages.UiStretch(viewBack.rectTransform);
            viewBack.uvRect = new UnityEngine.Rect(0.16f, 0.2f, 0.62f, 0.6f);
            if (!CodexArt.HasTex("papyrus_page")) viewBack.color = new Color(0.82f, 0.72f, 0.52f, 1f);
            viewBack.raycastTarget = true;

            content = CodexArt.Rect("Sheets", viewport);
            content.anchorMin = content.anchorMax = new Vector2(0f, 0.5f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, SheetH);

            // The roll's ends: sheets pass under a soft fade at the wrap and curl away into the right spindle,
            // so nothing is ever cut mid-word against a hard edge.
            leftFade = EdgeFade("FadeLeft", viewport, true, 70f);
            rightFade = EdgeFade("FadeRight", viewport, false, 120f);

            // Shading where the papyrus curls onto the right spindle.
            Image curl = CodexArt.Solid("Curl", viewport, new Color(0.18f, 0.10f, 0.03f, 0.42f));
            curlShade = curl.rectTransform;
            curlShade.anchorMin = new Vector2(1f, 0f);
            curlShade.anchorMax = new Vector2(1f, 1f);
            curlShade.pivot = new Vector2(1f, 0.5f);
            curlShade.sizeDelta = new Vector2(70f, 0f);
            curlShade.anchoredPosition = Vector2.zero;
            CodexGradient.Add(curl, new Color(1, 1, 1, 0), Color.white, false);
            curl.raycastTarget = false;

            BuildWrap();
            leftSpindle = BuildSpindle("SpindleLeft", false);
            leftSpindle.anchoredPosition = new Vector2(0f, 0f);
            rightSpindle = BuildSpindle("SpindleRight", true);

            penLayer = CodexArt.Rect("PenLayer", rollRoot);
            CodexPages.UiStretch(penLayer);
            pen = CodexPen.Build(penLayer);

            chipsRow = CodexArt.Rect("Filters", layer);
            chipsRow.anchorMin = chipsRow.anchorMax = new Vector2(0.5f, 0.5f);
            chipsRow.pivot = new Vector2(0.5f, 0f);
            chipsRow.sizeDelta = new Vector2(1200f, 34f);
            chipsGroup = chipsRow.gameObject.AddComponent<CanvasGroup>();
            chipsGroup.alpha = 0f;

            hints = CodexArt.Rect("Hints", layer);
            hints.anchorMin = hints.anchorMax = new Vector2(0.5f, 0f);
            hints.pivot = new Vector2(0.5f, 0f);
            hints.anchoredPosition = new Vector2(0f, 16f);
            hints.sizeDelta = new Vector2(1200f, 34f);
            hintsGroup = hints.gameObject.AddComponent<CanvasGroup>();
            BuildHints(false);

            RectTransform cont = CodexArt.Rect("Continue", layer);
            cont.anchorMin = cont.anchorMax = new Vector2(0.5f, 0f);
            cont.pivot = new Vector2(0.5f, 0f);
            cont.anchoredPosition = new Vector2(0f, 60f);
            cont.sizeDelta = new Vector2(600f, 40f);
            continueGroup = cont.gameObject.AddComponent<CanvasGroup>();
            continueGroup.alpha = 0f;
            continueLabel = CodexArt.Label("Label", cont, ContinueText, CodexArt.Body, 22, CodexArt.GoldDim, TextAnchor.MiddleCenter, FontStyle.Italic);
            CodexPages.UiStretch(continueLabel.rectTransform);

            RectTransform toastLayer = CodexArt.Rect("Toasts", canvasRect);
            CodexPages.UiStretch(toastLayer);
            toast = gameObject.AddComponent<CodexToast>();
            toast.Init(toastLayer);

            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.ignoreListenerPause = true;

            layer.gameObject.SetActive(false);

            CodexStore.Changed += OnStoreChanged;
            CodexStore.WrittenAny += OnWrittenAny;
            CodexStore.Revised += OnRevised;
            CodexStore.EpithetAdded += OnEpithet;
            CodexStore.Asked += OnAsked;
            CodexStore.Answered += OnAnswered;
            CodexStore.Noted += OnNoted;
        }

        private void OnRevised(CodexRevision r) { Queue("rev:" + r.Id); hud.Arrive(); }
        private void OnEpithet(CodexEpithet ep) { Queue("ep:" + ep.Id); hud.Arrive(); }
        private void OnAsked(CodexQuestion q) { Queue("ask:" + q.Id); hud.Arrive(); }
        private void OnAnswered(CodexQuestion q) { Queue("ans:" + q.Id); hud.Arrive(); }
        private void OnNoted(CodexNoteRecord n) { Queue("note:" + n.Scene + "|" + n.Text); hud.Arrive(); }

        private RectTransform shadowRect;
        private RectTransform leftFade, rightFade;

        /// <summary>A strip of papyrus fading from opaque at the roll's edge to clear, laid over the sheets.</summary>
        private static RectTransform EdgeFade(string name, RectTransform parent, bool left, float width)
        {
            RawImage img = CodexArt.Raw(name, parent, CodexArt.Tex("papyrus_page"), new Color(0.90f, 0.86f, 0.78f, 1f));
            if (!CodexArt.HasTex("papyrus_page")) img.color = new Color(0.82f, 0.72f, 0.52f, 1f);
            img.uvRect = new UnityEngine.Rect(left ? 0.2f : 0.6f, 0.2f, 0.06f, 0.6f);
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            r.anchorMin = new Vector2(left ? 0f : 1f, 0f);
            r.anchorMax = new Vector2(left ? 0f : 1f, 1f);
            r.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            r.sizeDelta = new Vector2(width, 0f);
            r.anchoredPosition = Vector2.zero;
            Color solid = Color.white, clear = new Color(1f, 1f, 1f, 0f);
            CodexGradient.Add(img, left ? solid : clear, left ? clear : solid, false);
            return r;
        }

        private void OnStoreChanged() { dirty = true; }

        private void OnWrittenAny(CodexEntry e)
        {
            if (e == null) return;
            if (!scribeQueue.Contains("entry:" + e.Id)) dryQueue.Add(e.Id);
        }

        private RectTransform BuildSpindle(string name, bool right)
        {
            RectTransform s = CodexArt.Rect(name, rollRoot);
            s.anchorMin = s.anchorMax = new Vector2(0f, 0.5f);
            s.pivot = new Vector2(0f, 0.5f);
            s.sizeDelta = new Vector2(SpindleW, SheetH + 70f);

            if (right)
            {
                // Papyrus still rolled up on the right spindle: a cylinder of fibre that thins as it unrolls.
                Image bulk = CodexArt.Solid("Rolled", s, new Color(0.82f, 0.70f, 0.48f, 1f));
                rightBulk = bulk.rectTransform;
                rightBulk.anchorMin = rightBulk.anchorMax = new Vector2(0.5f, 0.5f);
                rightBulk.pivot = new Vector2(0.5f, 0.5f);
                rightBulk.sizeDelta = new Vector2(64f, SheetH);
                CylinderShade(bulk);
                RawImage fib = CodexArt.Raw("Fibre", rightBulk, CodexArt.Tex("papyrus_page"), new Color(1f, 1f, 1f, 0.55f));
                CodexPages.UiStretch(fib.rectTransform);
                fib.uvRect = new UnityEngine.Rect(0f, 0f, 0.08f, 1f);
            }

            // The ebony rod, with gold caps that stand out above and below the papyrus.
            Image rod = CodexArt.Solid("Rod", s, new Color(0.09f, 0.06f, 0.045f, 1f));
            rod.rectTransform.anchorMin = rod.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rod.rectTransform.sizeDelta = new Vector2(right ? 26f : 30f, SheetH + 44f);
            CylinderShade(rod);
            for (int i = 0; i < 2; i++)
            {
                bool top = i == 0;
                RectTransform cap = CodexArt.Rect(top ? "CapTop" : "CapBottom", s);
                cap.anchorMin = cap.anchorMax = new Vector2(0.5f, top ? 1f : 0f);
                cap.pivot = new Vector2(0.5f, 0.5f);
                cap.anchoredPosition = new Vector2(0f, top ? -12f : 12f);
                cap.sizeDelta = new Vector2(52f, 22f);
                Image knob = CodexArt.Solid("Knob", cap, new Color(0.10f, 0.07f, 0.05f, 1f));
                CodexPages.UiStretch(knob.rectTransform);
                CylinderShade(knob);
                Image band = CodexArt.Solid("Gold", cap, CodexArt.GoldInk);
                band.rectTransform.anchorMin = new Vector2(0f, top ? 0f : 1f);
                band.rectTransform.anchorMax = new Vector2(1f, top ? 0f : 1f);
                band.rectTransform.pivot = new Vector2(0.5f, top ? 0f : 1f);
                band.rectTransform.sizeDelta = new Vector2(0f, 6f);
                band.rectTransform.anchoredPosition = Vector2.zero;
                CodexGradient.Add(band, new Color(1.25f, 1.15f, 0.9f, 1f), new Color(0.7f, 0.55f, 0.35f, 1f), false);
                RawImage gleam = CodexArt.Raw("Gleam", cap, UiKit.SoftDot(32, 2f), new Color(1f, 0.85f, 0.5f, 0.35f));
                CodexArt.Centered(gleam.rectTransform, new Vector2(-8f, 0f), new Vector2(18f, 14f));
                gleam.rectTransform.anchorMin = gleam.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            }
            return s;
        }

        /// <summary>Two gradient halves read as a lit cylinder: highlight left of centre, falling to shadow right.</summary>
        private static void CylinderShade(Image img)
        {
            CodexGradient.Add(img, new Color(1.25f, 1.18f, 1.05f, 1f), new Color(0.55f, 0.5f, 0.45f, 1f), false);
        }

        private void BuildWrap()
        {
            wrap = CodexArt.Rect("Wrap", rollRoot);
            wrap.anchorMin = wrap.anchorMax = new Vector2(0f, 0.5f);
            wrap.pivot = new Vector2(0f, 0.5f);
            wrap.anchoredPosition = new Vector2(SpindleW * 0.5f, 0f);
            wrap.sizeDelta = new Vector2(WrapW + SpindleW * 0.5f, SheetH);

            // The outer wrap is the most handled papyrus: darker, warmer.
            RawImage paper = CodexArt.Raw("Papyrus", wrap, CodexArt.Tex("papyrus_page"), new Color(0.80f, 0.70f, 0.54f, 1f));
            CodexPages.UiStretch(paper.rectTransform);
            paper.uvRect = new UnityEngine.Rect(0.5f, 0.16f, 0.3f, 0.66f);
            if (!CodexArt.HasTex("papyrus_page")) paper.color = new Color(0.74f, 0.62f, 0.42f, 1f);
            Image edge = CodexArt.Solid("Edge", wrap, new Color(0.25f, 0.14f, 0.05f, 0.35f));
            edge.rectTransform.anchorMin = new Vector2(1f, 0f);
            edge.rectTransform.anchorMax = new Vector2(1f, 1f);
            edge.rectTransform.pivot = new Vector2(1f, 0.5f);
            edge.rectTransform.sizeDelta = new Vector2(24f, 0f);
            edge.rectTransform.anchoredPosition = Vector2.zero;
            CodexGradient.Add(edge, new Color(1, 1, 1, 0), Color.white, false);

            wrapContent = CodexArt.Rect("Name", wrap);
            CodexPages.UiStretch(wrapContent);

            // The linen cord that ties the roll shut. It slips off when the roll opens.
            cord = CodexArt.Rect("Cord", rollRoot);
            cord.anchorMin = cord.anchorMax = new Vector2(0f, 0.5f);
            cord.pivot = new Vector2(0.5f, 0.5f);
            cord.anchoredPosition = new Vector2(SpindleW + WrapW - 10f, -60f);
            cord.sizeDelta = new Vector2(26f, SheetH * 0.36f);
            cordGroup = cord.gameObject.AddComponent<CanvasGroup>();
            for (int i = 0; i < 2; i++)
            {
                Image strand = CodexArt.Solid("Strand", cord, new Color(0.55f, 0.13f, 0.07f, 1f));
                strand.rectTransform.anchorMin = strand.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                strand.rectTransform.sizeDelta = new Vector2(7f, SheetH * 0.36f);
                strand.rectTransform.anchoredPosition = new Vector2(i == 0 ? -5f : 5f, 0f);
                strand.rectTransform.localEulerAngles = new Vector3(0f, 0f, i == 0 ? 4f : -3f);
                CylinderShade(strand);
            }
            Image seal = CodexArt.Solid("ClaySeal", cord, new Color(0.45f, 0.28f, 0.16f, 1f));
            seal.rectTransform.anchorMin = seal.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            seal.rectTransform.sizeDelta = new Vector2(30f, 26f);
            seal.rectTransform.anchoredPosition = new Vector2(0f, -6f);
        }

        private void RebuildWrap(string animateEpithet)
        {
            for (int i = wrapContent.childCount - 1; i >= 0; i--) Destroy(wrapContent.GetChild(i).gameObject);
            epithetTexts.Clear();
            float w = WrapW - 36f;
            float x = 26f;
            float y = 82f;
            CodexJournal j = CodexStore.Journal;

            Text head = CodexArt.Label("Head", wrapContent, "THE BOOK OF GOING FORTH BY DAY", CodexArt.Caps, 15, CodexArt.OchreSoft, TextAnchor.UpperCenter);
            head.horizontalOverflow = HorizontalWrapMode.Wrap;
            CodexArt.TopLeft(head.rectTransform, x, 26f, w, 40f);

            bool hasRen = j.HasEpithet("ep.ren");
            RectTransform cart = CodexArt.TopLeft(CodexArt.Rect("Cartouche", wrapContent), x + 4f, y, w - 8f, 96f);
            Image frame = CodexArt.SlicedImage("Frame", cart, GlyphAtlas.Cartouche, hasRen ? CodexArt.GoldInk : new Color(CodexArt.Ink.r, CodexArt.Ink.g, CodexArt.Ink.b, 0.18f), 256f / 96f);
            CodexPages.UiStretch(frame.rectTransform);
            if (hasRen)
            {
                CodexGradient.Add(frame, new Color(1.2f, 1.12f, 1f, 1f), new Color(0.8f, 0.7f, 0.58f, 1f), true);
                RectTransform inner = CodexArt.Rect("Inner", cart);
                CodexArt.Centered(inner, new Vector2(-12f, 0f), new Vector2(w - 100f, 60f));
                CodexArt.GlyphGroup gg = CodexPages.AddGoldGlyphs(inner, CodexRollCatalog.RenSigns, 52f, w - 100f, false);
                renGlyphs = gg;
                if (animateEpithet == "ep.ren")
                    foreach (RawImage sgn in gg.Signs) sgn.color = new Color(sgn.color.r, sgn.color.g, sgn.color.b, 0f);
            }
            y += 112f;

            foreach (CodexEpithet ep in CodexRollCatalog.Epithets)
            {
                if (!j.HasEpithet(ep.Id)) continue;
                bool name = ep.Id == "ep.ren";
                string text = name ? "ANKHU" : (ep.Rubric ? ep.Text.ToUpperInvariant() : ep.Text);
                Font font = name || ep.Rubric ? CodexArt.Caps : CodexArt.Body;
                int size = name ? 30 : ep.Rubric ? 22 : 24;
                Color c = ep.Rubric ? CodexArt.Ochre : CodexArt.Ink;
                Text t = CodexArt.Label("Epithet_" + ep.Id, wrapContent, text, font, size, c, TextAnchor.UpperCenter, name || ep.Rubric ? FontStyle.Normal : FontStyle.Italic);
                t.supportRichText = false;
                float h = CodexArt.MeasureHeight(text, font, size, FontStyle.Italic, 1f, w) + 6f;
                CodexArt.TopLeft(t.rectTransform, x, y, w, h);
                CodexReveal rv = CodexReveal.Attach(t);
                if (ep.Id == animateEpithet) rv.Reveal = 0f;
                epithetTexts.Add(t);
                y += h + (name ? 14f : 8f);
                if (name)
                {
                    Image rule = CodexArt.Solid("Rule", wrapContent, new Color(CodexArt.Ochre.r, CodexArt.Ochre.g, CodexArt.Ochre.b, 0.6f));
                    CodexArt.TopLeft(rule.rectTransform, x + w * 0.3f, y - 8f, w * 0.4f, 1.5f);
                    y += 6f;
                }
            }
            if (!hasRen)
            {
                Text blank = CodexArt.Label("Blank", wrapContent, "Blank still.", CodexArt.Body, 21, new Color(CodexArt.Ink.r, CodexArt.Ink.g, CodexArt.Ink.b, 0.35f), TextAnchor.UpperCenter, FontStyle.Italic);
                CodexArt.TopLeft(blank.rectTransform, x, y, w, 28f);
            }
        }

        private CodexArt.GlyphGroup renGlyphs;

        private IEnumerator GlyphsIn(CodexArt.GlyphGroup gg, float delay)
        {
            foreach (RawImage s in gg.Signs) s.color = new Color(s.color.r, s.color.g, s.color.b, 0f);
            for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
            foreach (RawImage s in gg.Signs)
            {
                Color c = s.color;
                for (float t = 0f; t < 0.32f; t += Time.unscaledDeltaTime)
                {
                    float k = CodexEase.OutCubic(t / 0.32f);
                    s.color = new Color(c.r, c.g, c.b, k);
                    s.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, k);
                    yield return null;
                }
                s.color = new Color(c.r, c.g, c.b, 1f);
                s.rectTransform.localScale = Vector3.one;
            }
        }

        // ================================================================== hints and filters

        private void BuildHints(bool gamepad)
        {
            hintsGamepad = gamepad;
            for (int i = hints.childCount - 1; i >= 0; i--) Destroy(hints.GetChild(i).gameObject);
            var items = gamepad
                ? new[] { new[] { "L" }, new[] { "LB", "RB" }, new[] { "A" }, new[] { "Y" }, new[] { "B" } }
                : new[] { new[] { "A", "D" }, new[] { "Q", "E" }, new[] { "CLICK" }, new[] { "HOME" }, new[] { "TAB" } };
            string[] labels = { "Unroll", "Filter", "Speak a word", "Words of power", "Close" };
            float x = 0f;
            RectTransform row = CodexArt.Rect("Row", hints);
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
                x += lw + 34f;
            }
            x -= 34f;
            row.sizeDelta = new Vector2(x, 30f);
            row.anchoredPosition = new Vector2(-x * 0.5f, 0f);
        }

        private void RebuildFilters()
        {
            filters.Clear();
            filters.Add("All");
            var have = new HashSet<CodexSection>();
            var people = new HashSet<string>();
            foreach (string id in CodexStore.Journal.Order)
            {
                CodexEntry e = CodexCatalog.Get(id);
                if (e == null) continue;
                have.Add(CodexRollCatalog.Section(e));
                string until = CodexRollCatalog.FaintUntilId(id);
                if (until != null && !CodexStore.Has(until)) continue;
                foreach (string p in CodexRollCatalog.PersonsOf(id)) people.Add(p);
            }
            foreach (CodexSection s in new[] { CodexSection.Craft, CodexSection.Heart, CodexSection.Names, CodexSection.Account })
            {
                if (have.Contains(s)) filters.Add(s.ToString());
            }
            foreach (string p in CodexRollCatalog.PersonOrder)
            {
                if (people.Contains(p)) filters.Add(p);
            }
            if (!filters.Contains(filter)) filter = "All";

            for (int i = chipsRow.childCount - 1; i >= 0; i--) Destroy(chipsRow.GetChild(i).gameObject);
            chipRects.Clear();
            float x = 0f;
            const float gap = 26f;
            var built = new List<RectTransform>();
            foreach (string f in filters)
            {
                bool on = f == filter;
                bool person = Array.IndexOf(CodexRollCatalog.PersonOrder, f) >= 0;
                string label = f.ToUpperInvariant();
                float tw = CodexArt.MeasureWidth(label, CodexArt.Caps, 17, FontStyle.Normal);
                RectTransform chip = CodexArt.Rect("Chip_" + f, chipsRow);
                chip.anchorMin = chip.anchorMax = new Vector2(0f, 0.5f);
                chip.pivot = new Vector2(0f, 0.5f);
                chip.anchoredPosition = new Vector2(x, 0f);
                chip.sizeDelta = new Vector2(tw + 8f, 32f);
                Text t = CodexArt.Label("Label", chip, label, CodexArt.Caps, 17, on ? CodexArt.Gold : new Color(CodexArt.GoldDim.r, CodexArt.GoldDim.g, CodexArt.GoldDim.b, person ? 0.7f : 0.55f), TextAnchor.MiddleCenter);
                CodexPages.UiStretch(t.rectTransform);
                if (on)
                {
                    Image u = CodexArt.Solid("Underline", chip, CodexArt.Gold);
                    u.rectTransform.anchorMin = new Vector2(0f, 0f);
                    u.rectTransform.anchorMax = new Vector2(1f, 0f);
                    u.rectTransform.pivot = new Vector2(0.5f, 0f);
                    u.rectTransform.sizeDelta = new Vector2(0f, 2f);
                    u.rectTransform.anchoredPosition = new Vector2(0f, -2f);
                }
                if (person && (built.Count == 0 || Array.IndexOf(CodexRollCatalog.PersonOrder, filters[built.Count - 1]) < 0))
                {
                    // A small dot divides the sections from the people.
                    RawImage dot = CodexArt.Raw("Dot", chipsRow, CodexArt.Disc, CodexArt.GoldDim);
                    dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    dot.rectTransform.sizeDelta = new Vector2(6f, 6f);
                    dot.rectTransform.anchoredPosition = new Vector2(x - gap * 0.5f, 0f);
                }
                chipRects.Add(chip);
                built.Add(chip);
                x += tw + 8f + gap;
            }
            x -= gap;
            float shift = -x * 0.5f;
            foreach (RectTransform c in chipsRow)
            {
                c.anchoredPosition += new Vector2(shift + chipsRow.sizeDelta.x * 0.5f, 0f);
            }
        }

        private bool Passes(CodexSheetView v)
        {
            if (filter == "All") return true;
            if (v.Entry == null) return false;
            if (Enum.TryParse(filter, out CodexSection s)) return CodexRollCatalog.Section(v.Entry) == s;
            if (v.Faint) return false;
            return Array.IndexOf(CodexRollCatalog.PersonsOf(v.Entry.Id), filter) >= 0;
        }

        // ================================================================== sheets

        private void Rebuild()
        {
            string anchorToken = FirstVisibleToken();
            foreach (CodexSheetView v in sheets)
            {
                if (v != null) Destroy(v.gameObject);
            }
            sheets.Clear();
            sheetX.Clear();
            words.Clear();
            wordCursor = -1;

            string animEpithet = null;
            foreach (string q in scribeQueue)
            {
                if (q.StartsWith("ep:")) { animEpithet = q.Substring(3); break; }
            }
            RebuildWrap(animEpithet);
            RebuildFilters();

            float x = 0f;
            int index = 0;
            foreach (string token in CodexStore.Journal.Timeline)
            {
                CodexSheetView v;
                if (token.StartsWith("?", StringComparison.Ordinal))
                {
                    v = CodexRollSheets.BuildGroup(token.Substring(1), content, index);
                }
                else
                {
                    CodexEntry e = CodexCatalog.Get(token);
                    if (e == null) continue;
                    v = CodexRollSheets.BuildEntry(e, content, index, ChooseWord);
                }
                index++;
                if (!Passes(v))
                {
                    Destroy(v.gameObject);
                    continue;
                }
                v.Root.anchoredPosition = new Vector2(x, 0f);
                sheets.Add(v);
                sheetX.Add(x);
                x += v.Width;
                foreach (CodexWordTag tag in v.GetComponentsInChildren<CodexWordTag>(true)) words.Add(tag);
            }
            contentW = x;
            content.sizeDelta = new Vector2(contentW + TailW, SheetH);
            dirty = false;
            builtSignature = Signature();
            PrepareScribeTargets();

            if (anchorToken != null)
            {
                int i = sheets.FindIndex(v => v.Token == anchorToken);
                if (i >= 0) scroll = scrollTarget = Mathf.Clamp(sheetX[i], 0f, MaxScroll);
            }
            scroll = scrollTarget = Mathf.Clamp(scrollTarget, 0f, MaxScroll);
        }

        private string builtSignature;

        private static string Signature()
        {
            CodexJournal j = CodexStore.Journal;
            int answered = 0;
            foreach (string q in j.AskedIds) if (j.IsAnswered(q)) answered++;
            return j.Timeline.Count + "/" + j.RevisionIds.Count + "/" + j.EpithetIds.Count + "/" + j.AskedIds.Count + "/" + answered + "/" + j.Notes.Count;
        }

        private float MaxScroll => Mathf.Max(0f, contentW + TailW - ViewW);

        private string FirstVisibleToken()
        {
            for (int i = 0; i < sheets.Count; i++)
            {
                if (sheetX[i] + sheets[i].Width > scroll + 20f) return sheets[i].Token;
            }
            return null;
        }

        private int SheetIndexOf(string entryId)
        {
            if (entryId == null) return -1;
            return sheets.FindIndex(v => v.Token == entryId);
        }

        /// <summary>Scroll so the sheet sits near the right of the view, the newest writing under the hand.</summary>
        private float ScrollFor(int i)
        {
            if (i < 0 || i >= sheets.Count) return scrollTarget;
            float right = sheetX[i] + sheets[i].Width;
            return Mathf.Clamp(right - ViewW + 120f, 0f, MaxScroll);
        }

        /// <summary>Hide what the pen is about to write, so the first frame does not flash finished text.</summary>
        private void PrepareScribeTargets()
        {
            foreach (string item in scribeQueue)
            {
                if (item.StartsWith("entry:"))
                {
                    int i = SheetIndexOf(item.Substring(6));
                    if (i < 0) continue;
                    CodexSheetView v = sheets[i];
                    foreach (Text t in EntryTexts(v)) Hide(t);
                    if (v.Heading != null) foreach (RawImage s in v.Heading.Signs) s.color = new Color(s.color.r, s.color.g, s.color.b, 0f);
                }
                else if (item.StartsWith("rev:"))
                {
                    CodexRevision r = CodexRollCatalog.Revision(item.Substring(4));
                    int i = r != null ? SheetIndexOf(r.EntryId) : -1;
                    if (i < 0) continue;
                    foreach (CodexSheetView.RevisionView rv in sheets[i].Revisions)
                    {
                        if (rv.Revision.Id != r.Id) continue;
                        rv.Strike.Progress = 0f;
                        Hide(rv.New);
                        if (rv.Note != null) rv.Note.canvasRenderer.SetAlpha(0f);
                    }
                }
                else if (item.StartsWith("ask:") || item.StartsWith("ans:"))
                {
                    string qid = item.Substring(4);
                    foreach (CodexSheetView v in sheets)
                    {
                        foreach (CodexSheetView.QuestionView qv in v.Questions)
                        {
                            if (qv.Question.Id != qid) continue;
                            if (item.StartsWith("ask:")) Hide(qv.Text);
                            else
                            {
                                if (qv.Strike != null) qv.Strike.Progress = 0f;
                                if (qv.Answer != null) Hide(qv.Answer);
                            }
                        }
                    }
                }
                else if (item.StartsWith("note:"))
                {
                    string key = item.Substring(5);
                    foreach (CodexSheetView v in sheets)
                    {
                        if (v.Scene == null) continue;
                        foreach (Text t in v.Notes)
                        {
                            if (v.Scene + "|" + t.text == key) Hide(t);
                        }
                    }
                }
            }
        }

        private static IEnumerable<Text> EntryTexts(CodexSheetView v)
        {
            if (v.Title != null) yield return v.Title;
            if (v.Translit != null) yield return v.Translit;
            if (v.Effect != null) yield return v.Effect;
            if (v.Body != null) yield return v.Body;
        }

        private static void Hide(Text t)
        {
            if (t == null) return;
            CodexReveal r = CodexReveal.Attach(t);
            r.Reveal = 0f;
            r.Refresh();
        }

        // ================================================================== open and close

        private void RequestOpen(string entryId)
        {
            if (state == State.Closing) { closeQueued = false; return; }
            if (state != State.Closed) return;
            Rebuild();
            int target = SheetIndexOf(entryId);
            if (target < 0)
            {
                // The first unread sheet, else where the reader left off.
                for (int i = 0; i < sheets.Count && target < 0; i++)
                {
                    foreach (string id in sheets[i].ReadIds)
                    {
                        if (CodexStore.Journal.IsUnread(id) || CodexStore.Journal.IsRevisionUnread(id) || CodexStore.Journal.IsAnswerUnread(id)) { target = i; break; }
                    }
                }
            }
            if (target >= 0) scroll = scrollTarget = ScrollFor(target);
            else if (lastFirstVisible < 0) scroll = scrollTarget = MaxScroll;
            IsOpen = true;
            prevCursorVisible = Cursor.visible;
            prevCursorLock = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            OpenChanged?.Invoke(true);
            StartCoroutine(OpenRoutine());
        }

        private void RequestClose()
        {
            if (state == State.Open && !scribing) StartCoroutine(CloseRoutine());
            else if (state == State.Opening || scribing) closeQueued = true;
        }

        private float FitScale()
        {
            Vector2 size = canvasRect.rect.size;
            if (size.x <= 1f || size.y <= 1f) return 1f;
            float bw = TotalW + 80f, bh = SheetH + 250f;
            return Mathf.Min(1.0f, size.x / bw, size.y / bh);
        }

        private void ApplyReveal(float r)
        {
            reveal = Mathf.Clamp01(r);
            float vw = ViewW * reveal;
            viewport.sizeDelta = new Vector2(vw, SheetH);
            rightSpindle.anchoredPosition = new Vector2(SpindleW + WrapW + vw - SpindleW * 0.5f, 0f);
            // The rolled-up papyrus on the right thins as the roll opens.
            if (rightBulk != null) rightBulk.sizeDelta = new Vector2(Mathf.Lerp(72f, 34f, reveal), SheetH);
            // Keep the open roll centred on screen.
            float shownW = SpindleW + WrapW + vw + SpindleW;
            rollRoot.sizeDelta = new Vector2(TotalW, SheetH);
            rollOffsetX = (TotalW - shownW) * 0.5f;
            if (shadowRect != null) shadowRect.sizeDelta = new Vector2(shownW + 120f, 150f);
            curlShade.gameObject.SetActive(vw > 4f);
        }

        private float rollOffsetX;
        private Vector2 RollCentre => new Vector2(rollOffsetX * rollRoot.localScale.x, 26f);

        private IEnumerator OpenRoutine()
        {
            state = State.Opening;
            layer.gameObject.SetActive(true);
            layerGroup.alpha = 1f;
            hintsGroup.alpha = 0f;
            chipsGroup.alpha = 0f;
            continueGroup.alpha = 0f;
            pen.Alpha = 0f;
            float s = FitScale();
            Vector2 from = hud.IconCenter(canvasRect);
            bool fromHands = false;
            if (nextOrigin.HasValue && Time.frameCount - nextOriginFrame <= 3 &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, nextOrigin.Value, null, out Vector2 local))
            {
                from = local;
                fromHands = true;
            }
            nextOrigin = null;
            ApplyReveal(0f);
            cord.anchoredPosition = new Vector2(SpindleW + WrapW - 10f, -60f);
            cordGroup.alpha = 1f;
            content.anchoredPosition = new Vector2(-scroll, 0f);

            // 1. The world dims first, then the closed roll comes up out of the icon (or out of his hands), small and
            //    glowing, and settles centre stage at two thirds size. It grows to full size as it unrolls, so the closed
            //    roll never stands in the scene like a post.
            const float dimIn = 0.14f;
            for (float t = 0f; t < dimIn; t += Time.unscaledDeltaTime)
            {
                dimGroup.alpha = CodexEase.OutQuad(t / dimIn) * 0.85f;
                rollGroup.alpha = 0f;
                yield return null;
            }
            float closedScale = s * 0.66f;
            float fly = fromHands ? 0.58f : 0.42f;
            for (float t = 0f; t < fly; t += Time.unscaledDeltaTime)
            {
                float k = CodexEase.OutCubic(t / fly);
                float sc = Mathf.LerpUnclamped(fromHands ? 0.14f : 0.06f, closedScale, CodexEase.OutBack(t / fly, 0.7f));
                rollRoot.localScale = new Vector3(sc, sc, 1f);
                rollRoot.anchoredPosition = Vector2.LerpUnclamped(from, RollCentre, k);
                rollRoot.localEulerAngles = Vector3.zero;
                rollGroup.alpha = Mathf.Clamp01(t / 0.1f);
                dimGroup.alpha = Mathf.Lerp(0.85f, 1f, k);
                yield return null;
            }
            rollRoot.localScale = new Vector3(closedScale, closedScale, 1f);
            rollGroup.alpha = 1f;
            dimGroup.alpha = 1f;

            // 2. The cord slips off.
            const float slip = 0.28f;
            Vector2 c0 = cord.anchoredPosition;
            for (float t = 0f; t < slip; t += Time.unscaledDeltaTime)
            {
                float k = CodexEase.InQuad(t / slip);
                cord.anchoredPosition = c0 + new Vector2(18f * k, -120f * k);
                cord.localEulerAngles = new Vector3(0f, 0f, -14f * k);
                cordGroup.alpha = 1f - k;
                rollRoot.anchoredPosition = RollCentre;
                yield return null;
            }
            cordGroup.alpha = 0f;

            // 3. The right spindle travels and the papyrus unrolls.
            PageTurned?.Invoke(0);
            const float unroll = 0.78f;
            for (float t = 0f; t < unroll; t += Time.unscaledDeltaTime)
            {
                float k = CodexEase.InOutCubic(t / unroll);
                ApplyReveal(k);
                float sc = Mathf.Lerp(closedScale, s, CodexEase.OutCubic(t / unroll));
                rollRoot.localScale = new Vector3(sc, sc, 1f);
                rollRoot.anchoredPosition = RollCentre;
                chipsGroup.alpha = Mathf.Clamp01((t / unroll - 0.6f) * 2.5f);
                yield return null;
            }
            ApplyReveal(1f);
            rollRoot.localScale = new Vector3(s, s, 1f);
            rollRoot.anchoredPosition = RollCentre;

            const float fade = 0.25f;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                float k = t / fade;
                chipsGroup.alpha = Mathf.Max(chipsGroup.alpha, k);
                hintsGroup.alpha = k;
                yield return null;
            }
            chipsGroup.alpha = 1f;
            hintsGroup.alpha = 1f;
            state = State.Open;
            readTimer = 0f;
            StartCoroutine(RunScribes());
            if (closeQueued && !presenting) { closeQueued = false; StartCoroutine(CloseRoutine()); }
        }

        private IEnumerator CloseRoutine()
        {
            state = State.Closing;
            continueAt = float.PositiveInfinity;
            const float fade = 0.16f;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / fade;
                hintsGroup.alpha = Mathf.Min(hintsGroup.alpha, k);
                chipsGroup.alpha = Mathf.Min(chipsGroup.alpha, k);
                continueGroup.alpha = Mathf.Min(continueGroup.alpha, k);
                pen.Alpha = Mathf.Min(pen.Alpha, k);
                yield return null;
            }
            hintsGroup.alpha = chipsGroup.alpha = continueGroup.alpha = 0f;

            // Rolls up while it shrinks, then drops into the icon and is gone, the world brightening as it goes.
            float s0 = rollRoot.localScale.x;
            float closedScale = s0 * 0.66f;
            const float rollUp = 0.42f;
            for (float t = 0f; t < rollUp; t += Time.unscaledDeltaTime)
            {
                float k = CodexEase.InOutCubic(t / rollUp);
                ApplyReveal(1f - k);
                float sc = Mathf.Lerp(s0, closedScale, k);
                rollRoot.localScale = new Vector3(sc, sc, 1f);
                rollRoot.anchoredPosition = RollCentre;
                dimGroup.alpha = 1f - 0.25f * k;
                yield return null;
            }
            ApplyReveal(0f);
            cordGroup.alpha = 1f;
            cord.anchoredPosition = new Vector2(SpindleW + WrapW - 10f, -60f);
            cord.localEulerAngles = Vector3.zero;

            Vector2 to = hud.IconCenter(canvasRect);
            Vector2 start = rollRoot.anchoredPosition;
            const float fly = 0.34f;
            for (float t = 0f; t < fly; t += Time.unscaledDeltaTime)
            {
                float k = CodexEase.InCubic(t / fly);
                rollRoot.anchoredPosition = Vector2.LerpUnclamped(start, to, k);
                float sc = Mathf.Lerp(closedScale, 0.05f, CodexEase.InQuad(t / fly));
                rollRoot.localScale = new Vector3(sc, sc, 1f);
                rollRoot.localEulerAngles = Vector3.zero;
                rollGroup.alpha = 1f - Mathf.Clamp01((t / fly - 0.45f) * 1.9f);
                dimGroup.alpha = 0.75f * (1f - CodexEase.OutQuad(t / fly));
                yield return null;
            }
            rollGroup.alpha = 0f;
            dimGroup.alpha = 0f;

            layer.gameObject.SetActive(false);
            state = State.Closed;
            closeQueued = false;
            IsOpen = false;
            Cursor.visible = prevCursorVisible;
            Cursor.lockState = prevCursorLock;
            hud.Arrive();
            OpenChanged?.Invoke(false);
            if (pendingWord != null)
            {
                string w = pendingWord;
                pendingWord = null;
                try { WordChosen?.Invoke(w); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }

        private void ChooseWord(string word)
        {
            if (state != State.Open || scribing || presenting || hallMode) return;
            pendingWord = word;
            RequestClose();
        }

        // ================================================================== the reed pen

        private IEnumerator RunScribes()
        {
            // Unread sheets written while the roll was closed dry in place: glossy, then matte.
            foreach (CodexSheetView v in sheets)
            {
                if (v.Entry == null || !dryQueue.Contains(v.Entry.Id)) continue;
                StartCoroutine(Dry(EntryTexts(v), v.Bleed));
            }
            dryQueue.Clear();

            while (scribeQueue.Count > 0 && state == State.Open)
            {
                string item = scribeQueue[0];
                scribing = true;
                skipArmedAt = Time.unscaledTime + 0.6f;
                skip = false;
                if (item.StartsWith("entry:")) yield return WriteEntry(item.Substring(6));
                else if (item.StartsWith("rev:")) yield return WriteRevision(item.Substring(4));
                else if (item.StartsWith("ep:")) yield return WriteEpithet(item.Substring(3));
                else if (item.StartsWith("ask:")) yield return WriteQuestion(item.Substring(4), false);
                else if (item.StartsWith("ans:")) yield return WriteQuestion(item.Substring(4), true);
                else if (item.StartsWith("note:")) yield return WriteNote(item.Substring(5));
                scribeQueue.Remove(item);
            }
            yield return LiftPen();
            scribing = false;
            if (closeQueued && !presenting) { closeQueued = false; StartCoroutine(CloseRoutine()); }
        }

        private bool skip;

        private IEnumerator ScrollTo(int i)
        {
            if (i < 0) yield break;
            scrollTarget = ScrollFor(i);
            for (float t = 0f; t < 1.2f && Mathf.Abs(scroll - scrollTarget) > 2f; t += Time.unscaledDeltaTime) yield return null;
        }

        private Vector2 PenPos(Graphic g, Vector2 local) =>
            penLayer.InverseTransformPoint(g.transform.TransformPoint(local));

        private IEnumerator WriteEntry(string entryId)
        {
            int i = SheetIndexOf(entryId);
            if (i < 0) yield break;
            yield return ScrollTo(i);
            CodexSheetView v = sheets[i];
            Canvas.ForceUpdateCanvases();

            AudioClip clip = RealmVoice.Find("codex." + entryId);
            if (clip != null) { voice.clip = clip; voice.Play(); }
            int total = 0;
            foreach (Text t in EntryTexts(v)) total += CodexReveal.Attach(t).QuadCount;
            // Match the hand to the voice when there is one, else a steady, legible pace.
            float rate = clip != null ? Mathf.Clamp(total / Mathf.Max(0.8f, clip.length * 0.92f), 18f, 70f) : 34f;

            if (v.Bleed != null)
            {
                v.Bleed.enabled = true;
                StartCoroutine(BleedPulse(v.Bleed));
            }

            bool first = true;
            if (v.Heading != null)
            {
                foreach (RawImage s in v.Heading.Signs)
                {
                    Vector2 target = PenPos(s, new Vector2(0f, -s.rectTransform.rect.height * 0.3f));
                    if (first) { pen.Follow(target, 0f, true); first = false; }
                    Color c = s.color;
                    for (float t = 0f; t < 0.34f && !skip; t += Time.unscaledDeltaTime)
                    {
                        float k = CodexEase.OutCubic(t / 0.34f);
                        pen.Alpha = Mathf.MoveTowards(pen.Alpha, 1f, Time.unscaledDeltaTime * 5f);
                        pen.Follow(target + new Vector2(Mathf.Sin(t * 30f) * 6f, Mathf.Cos(t * 24f) * 8f), Time.unscaledDeltaTime);
                        s.color = new Color(c.r, c.g, c.b, k);
                        s.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, k);
                        PollSkip();
                        yield return null;
                    }
                    s.color = new Color(c.r, c.g, c.b, 1f);
                    s.rectTransform.localScale = Vector3.one;
                }
            }

            foreach (Text t in EntryTexts(v))
            {
                yield return WriteText(t, t == v.Title || t == v.Translit ? Mathf.Max(rate, 26f) : rate, first);
                first = false;
            }
            StartCoroutine(Dry(EntryTexts(v), null));
            CodexStore.MarkRead(entryId);
        }

        private IEnumerator WriteText(Text t, float rate, bool snapPen)
        {
            if (t == null) yield break;
            CodexReveal r = CodexReveal.Attach(t);
            r.Reveal = 0f;
            r.Refresh();
            yield return null;
            int n = r.QuadCount;
            if (n == 0) { r.Reveal = -1f; r.Refresh(); yield break; }
            if (snapPen) pen.Follow(PenPos(t, r.PenPoint(0f)), 0f, true);
            float shown = 0f;
            while (shown < n)
            {
                float dt = Time.unscaledDeltaTime;
                PollSkip();
                shown = skip ? n : shown + rate * dt;
                r.Reveal = Mathf.Min(shown, n);
                r.Gloss = 0f;
                r.Refresh();
                pen.Alpha = Mathf.MoveTowards(pen.Alpha, 1f, dt * 5f);
                pen.Follow(PenPos(t, r.PenPoint(r.Reveal)), dt);
                yield return null;
            }
            r.Reveal = -1f;
            r.Gloss = 1f;
            r.Refresh();
        }

        private IEnumerator Dry(IEnumerable<Text> texts, RawImage bleed)
        {
            var list = new List<CodexReveal>();
            foreach (Text t in texts)
            {
                if (t == null) continue;
                CodexReveal r = CodexReveal.Attach(t);
                list.Add(r);
            }
            if (bleed != null) { bleed.enabled = true; StartCoroutine(BleedPulse(bleed)); }
            const float dur = 1.4f;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float g = 1f - CodexEase.OutQuad(t / dur);
                foreach (CodexReveal r in list)
                {
                    if (r == null || r.Reveal >= 0f) continue;
                    r.Gloss = g;
                    r.Refresh();
                }
                yield return null;
            }
            foreach (CodexReveal r in list)
            {
                if (r == null || r.Reveal >= 0f) continue;
                r.Gloss = 0f;
                r.Refresh();
            }
        }

        private IEnumerator BleedPulse(RawImage bleed)
        {
            const float dur = 1.6f;
            for (float t = 0f; t < dur && bleed != null; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                bleed.color = new Color(CodexArt.Ink.r, CodexArt.Ink.g, CodexArt.Ink.b, 0.10f * Mathf.Sin(k * Mathf.PI));
                float s = Mathf.Lerp(0.7f, 1.12f, k);
                bleed.rectTransform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (bleed != null) bleed.enabled = false;
        }

        private IEnumerator WriteRevision(string revisionId)
        {
            CodexRevision r = CodexRollCatalog.Revision(revisionId);
            int i = r != null ? SheetIndexOf(r.EntryId) : -1;
            if (i < 0) yield break;
            yield return ScrollTo(i);
            CodexSheetView.RevisionView rv = sheets[i].Revisions.Find(x => x.Revision.Id == revisionId);
            if (rv == null) yield break;
            Canvas.ForceUpdateCanvases();
            // Wait for the strike to measure its lines.
            for (int f = 0; f < 3 && !rv.Strike.Built; f++) yield return null;

            // A pause before the red pen: he reads the old line again.
            for (float t = 0f; t < 0.7f && !skip; t += Time.unscaledDeltaTime) { PollSkip(); yield return null; }

            int lines = Mathf.Max(1, rv.Strike.LineCount);
            float dur = 0.42f * lines;
            bool snapped = false;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                PollSkip();
                rv.Strike.Progress = skip ? 1f : t / dur;
                pen.Alpha = Mathf.MoveTowards(pen.Alpha, 1f, Time.unscaledDeltaTime * 6f);
                Vector2 p = (Vector2)penLayer.InverseTransformPoint(rv.Strike.transform.TransformPoint(rv.Strike.PenPoint()));
                pen.Follow(p, Time.unscaledDeltaTime, !snapped);
                snapped = true;
                if (skip) break;
                yield return null;
            }
            rv.Strike.Progress = 1f;

            AudioClip clip = RealmVoice.Find("codex." + revisionId);
            if (clip != null) { voice.clip = clip; voice.Play(); }
            int n = CodexReveal.Attach(rv.New).QuadCount;
            float rate = clip != null ? Mathf.Clamp(n / Mathf.Max(0.8f, clip.length * 0.92f), 16f, 60f) : 28f;
            yield return WriteText(rv.New, rate, false);
            if (rv.Note != null) StartCoroutine(FadeIn(rv.Note, 0.6f));
            StartCoroutine(Dry(new[] { rv.New }, sheets[i].Bleed));
            CodexStore.MarkRead(revisionId);
        }

        private IEnumerator WriteEpithet(string epithetId)
        {
            Text target = epithetTexts.Find(t => t.name == "Epithet_" + epithetId);
            if (target == null) yield break;
            AudioClip clip = RealmVoice.Find("codex." + epithetId);
            if (clip != null) { voice.clip = clip; voice.Play(); }
            // His name: the pen draws the four signs inside the cartouche, then writes ANKHU beneath it.
            if (epithetId == "ep.ren" && renGlyphs != null)
            {
                bool first = true;
                foreach (RawImage sg in renGlyphs.Signs)
                {
                    Vector2 at = PenPos(sg, new Vector2(0f, -sg.rectTransform.rect.height * 0.3f));
                    if (first) { pen.Follow(at, 0f, true); first = false; }
                    Color c = sg.color;
                    for (float t = 0f; t < 0.42f && !skip; t += Time.unscaledDeltaTime)
                    {
                        float k = CodexEase.OutCubic(t / 0.42f);
                        pen.Alpha = Mathf.MoveTowards(pen.Alpha, 1f, Time.unscaledDeltaTime * 5f);
                        pen.Follow(at + new Vector2(Mathf.Sin(t * 28f) * 6f, Mathf.Cos(t * 22f) * 9f), Time.unscaledDeltaTime);
                        sg.color = new Color(c.r, c.g, c.b, k);
                        sg.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, k);
                        PollSkip();
                        yield return null;
                    }
                    sg.color = new Color(c.r, c.g, c.b, 1f);
                    sg.rectTransform.localScale = Vector3.one;
                }
                for (float t = 0f; t < 0.3f && !skip; t += Time.unscaledDeltaTime) { PollSkip(); yield return null; }
            }
            yield return WriteText(target, 12f, epithetId != "ep.ren");
            StartCoroutine(Dry(new[] { target }, null));
            CodexStore.MarkRead(epithetId);
        }

        private IEnumerator WriteQuestion(string questionId, bool answer)
        {
            foreach (CodexSheetView v in sheets)
            {
                foreach (CodexSheetView.QuestionView qv in v.Questions)
                {
                    if (qv.Question.Id != questionId) continue;
                    yield return ScrollTo(sheets.IndexOf(v));
                    Canvas.ForceUpdateCanvases();
                    if (!answer)
                    {
                        yield return WriteText(qv.Text, 30f, true);
                        yield break;
                    }
                    if (qv.Strike != null)
                    {
                        for (int f = 0; f < 3 && !qv.Strike.Built; f++) yield return null;
                        float dur = 0.4f * Mathf.Max(1, qv.Strike.LineCount);
                        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
                        {
                            PollSkip();
                            qv.Strike.Progress = skip ? 1f : t / dur;
                            pen.Alpha = Mathf.MoveTowards(pen.Alpha, 1f, Time.unscaledDeltaTime * 6f);
                            pen.Follow((Vector2)penLayer.InverseTransformPoint(qv.Strike.transform.TransformPoint(qv.Strike.PenPoint())), Time.unscaledDeltaTime);
                            if (skip) break;
                            yield return null;
                        }
                        qv.Strike.Progress = 1f;
                    }
                    if (qv.Answer != null) yield return WriteText(qv.Answer, 30f, false);
                    CodexStore.MarkRead(questionId);
                    yield break;
                }
            }
        }

        private IEnumerator WriteNote(string key)
        {
            foreach (CodexSheetView v in sheets)
            {
                if (v.Scene == null) continue;
                foreach (Text t in v.Notes)
                {
                    if (v.Scene + "|" + t.text != key) continue;
                    yield return ScrollTo(sheets.IndexOf(v));
                    Canvas.ForceUpdateCanvases();
                    yield return WriteText(t, 30f, true);
                    yield break;
                }
            }
        }

        private IEnumerator LiftPen()
        {
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                pen.Alpha = 1f - t / 0.35f;
                pen.Root.anchoredPosition += new Vector2(0f, Time.unscaledDeltaTime * 90f);
                yield return null;
            }
            pen.Alpha = 0f;
        }

        private static IEnumerator FadeIn(Graphic g, float dur)
        {
            for (float t = 0f; t < dur && g != null; t += Time.unscaledDeltaTime)
            {
                g.canvasRenderer.SetAlpha(t / dur);
                yield return null;
            }
            if (g != null) g.canvasRenderer.SetAlpha(1f);
        }

        private void PollSkip()
        {
            if (skip || Time.unscaledTime < skipArmedAt) return;
            Keyboard kb = Keyboard.current;
            Gamepad gp = Gamepad.current;
            Mouse m = Mouse.current;
            if ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) ||
                (gp != null && gp.buttonSouth.wasPressedThisFrame) ||
                (m != null && m.leftButton.wasPressedThisFrame))
            {
                skip = true;
                voice.Stop();
            }
        }

        // ================================================================== frame update

        private void Update()
        {
            ReadDevice();
            HandleInput();

            if (state == State.Open)
            {
                if (dirty && !scribing && Signature() != builtSignature)
                {
                    Rebuild();
                    if (scribeQueue.Count > 0) StartCoroutine(RunScribes());
                }
                else if (dirty && !scribing) dirty = false;
                UpdateRead();
                UpdateWordHover();
                UpdateHallPrompt();
                if (presenting && !scribing && Time.unscaledTime > continueAt)
                    continueGroup.alpha = Mathf.MoveTowards(continueGroup.alpha, 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 2.2f), Time.unscaledDeltaTime * 2f);
            }
            if (state == State.Open || state == State.Opening)
            {
                scroll = Mathf.SmoothDamp(scroll, scrollTarget, ref scrollVel, 0.16f, Mathf.Infinity, Time.unscaledDeltaTime);
                content.anchoredPosition = new Vector2(-Mathf.Round(scroll), 0f);
                float s = FitScale();
                if (state == State.Open) rollRoot.localScale = new Vector3(s, s, 1f);
                rollRoot.anchoredPosition = state == State.Open ? RollCentre : rollRoot.anchoredPosition;
                Vector2 rp = rollRoot.anchoredPosition;
                chipsRow.anchoredPosition = new Vector2(rp.x, rp.y + (SheetH * 0.5f + 34f) * rollRoot.localScale.y);
                int first = FirstVisibleIndex();
                if (first != lastFirstVisible) { lastFirstVisible = first; PageTurned?.Invoke(first); }
            }
            if (hintsGamepad != GamepadMode && layer.gameObject.activeSelf) BuildHints(GamepadMode);
            hud.Tick(HudVisible && !IsOpen, CodexStore.UnreadCount);
        }

        private int FirstVisibleIndex()
        {
            for (int i = 0; i < sheets.Count; i++)
            {
                if (sheetX[i] + sheets[i].Width > scroll + 20f) return i;
            }
            return -1;
        }

        private void UpdateRead()
        {
            readTimer += Time.unscaledDeltaTime;
            if (readTimer < 1.6f || scribing) return;
            readTimer = 0f;
            var ids = new List<string>();
            for (int i = 0; i < sheets.Count; i++)
            {
                CodexSheetView v = sheets[i];
                bool inView = sheetX[i] >= scroll - 30f && sheetX[i] + v.Width <= scroll + ViewW + 30f;
                if (inView) ids.AddRange(v.ReadIds);
                if (v.UnreadDot != null)
                {
                    bool unread = false;
                    foreach (string id in v.ReadIds)
                        if (CodexStore.Journal.IsUnread(id) || CodexStore.Journal.IsRevisionUnread(id) || CodexStore.Journal.IsAnswerUnread(id)) unread = true;
                    v.UnreadDot.enabled = unread && !inView;
                }
            }
            if (ids.Count > 0)
            {
                bool any = false;
                foreach (string id in ids)
                    if (CodexStore.Journal.IsUnread(id) || CodexStore.Journal.IsRevisionUnread(id) || CodexStore.Journal.IsAnswerUnread(id)) any = true;
                if (any) CodexStore.MarkRead(ids);
            }
            foreach (string ep in CodexStore.Journal.EpithetIds)
            {
                if (CodexStore.Journal.IsEpithetUnread(ep)) { CodexStore.MarkRead(ep); break; }
            }
        }

        private void UpdateWordHover()
        {
            Mouse m = Mouse.current;
            Vector2 p = m != null ? m.position.ReadValue() : new Vector2(-9999f, -9999f);
            CodexWordTag hover = null;
            if (!GamepadMode && m != null)
            {
                foreach (CodexWordTag w in words)
                {
                    if (w != null && Hit((RectTransform)w.transform, p) && Hit(viewport, p)) { hover = w; break; }
                }
            }
            else if (GamepadMode && wordCursor >= 0 && wordCursor < words.Count) hover = words[wordCursor];
            foreach (CodexWordTag w in words)
            {
                if (w == null) continue;
                Image img = w.GetComponent<Image>();
                float target = w == hover ? 0.16f : 0f;
                Color c = img.color;
                c.a = Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * 1.2f);
                img.color = c;
            }
            if (!GamepadMode && hover != null && m.leftButton.wasPressedThisFrame && !presenting && !hallMode) ChooseWord(hover.Word);
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
                if (gp.leftStick.ReadValue().sqrMagnitude > 0.25f) { GamepadMode = true; return; }
            }
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) { GamepadMode = false; return; }
            Mouse m = Mouse.current;
            if (m != null && (m.leftButton.wasPressedThisFrame || m.delta.ReadValue().sqrMagnitude > 4f)) GamepadMode = false;
        }

        private void HandleInput()
        {
            if (InputBlocked && !presenting && !hallMode) return;
            // While the speech field takes typing (teaching rooms, Borrowed Dawn), Tab and C belong to the text.
            if (Ankhu.Core.InputGate.TextEntryActive && !presenting && !hallMode) return;
            Keyboard kb = Keyboard.current;
            Gamepad gp = Gamepad.current;
            Mouse mouse = Mouse.current;
            if (hallMode)
            {
                HallInput(kb, gp, mouse);
                return;
            }

            bool toggle = (kb != null && (kb.tabKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame))
                          || (gp != null && gp.selectButton.wasPressedThisFrame);
            if (presenting)
            {
                // A presented moment closes on any confirming key once the pen has lifted.
                bool go = toggle || (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                          || (gp != null && (gp.buttonEast.wasPressedThisFrame || gp.buttonSouth.wasPressedThisFrame));
                if (state == State.Open && !scribing && Time.unscaledTime > continueAt && go) RequestClose();
                if (state == State.Open && !scribing) Navigate(kb, gp, mouse, false);
                return;
            }

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
            if (state != State.Open) return;
            Navigate(kb, gp, mouse, true);
        }

        private void Navigate(Keyboard kb, Gamepad gp, Mouse mouse, bool allowWords)
        {
            float dt = Time.unscaledDeltaTime;
            bool next = (kb != null && (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame))
                        || (gp != null && gp.dpad.right.wasPressedThisFrame);
            bool prev = (kb != null && (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.pageUpKey.wasPressedThisFrame))
                        || (gp != null && gp.dpad.left.wasPressedThisFrame);
            if (next) StepSheet(1);
            else if (prev) StepSheet(-1);

            if (gp != null)
            {
                float sx = gp.leftStick.ReadValue().x;
                if (Mathf.Abs(sx) > 0.2f) scrollTarget = Mathf.Clamp(scrollTarget + sx * 1100f * dt, 0f, MaxScroll);
            }

            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) scrollTarget = Mathf.Clamp(scrollTarget - Mathf.Sign(wheel) * 160f, 0f, MaxScroll);
                Vector2 p = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame && Hit(viewport, p)) { dragging = true; dragLastX = p.x; }
                if (dragging)
                {
                    if (!mouse.leftButton.isPressed) dragging = false;
                    else
                    {
                        float s = Mathf.Max(0.01f, rollRoot.lossyScale.x);
                        scrollTarget = Mathf.Clamp(scrollTarget - (p.x - dragLastX) / s, 0f, MaxScroll);
                        scroll = scrollTarget;
                        dragLastX = p.x;
                    }
                }
                if (allowWords && mouse.leftButton.wasPressedThisFrame)
                {
                    for (int i = 0; i < chipRects.Count; i++)
                    {
                        if (Hit(chipRects[i], p)) { SetFilter(filters[i]); return; }
                    }
                    if (!Hit(rollRoot, p) && !Hit(chipsRow, p)) RequestClose();
                }
            }

            if (!allowWords) return;
            bool fNext = (kb != null && kb.eKey.wasPressedThisFrame) || (gp != null && gp.rightShoulder.wasPressedThisFrame);
            bool fPrev = (kb != null && kb.qKey.wasPressedThisFrame) || (gp != null && gp.leftShoulder.wasPressedThisFrame);
            if (fNext || fPrev)
            {
                int i = filters.IndexOf(filter);
                i = (i + (fNext ? 1 : -1) + filters.Count) % filters.Count;
                SetFilter(filters[i]);
            }
            bool home = (kb != null && kb.homeKey.wasPressedThisFrame) || (gp != null && gp.buttonNorth.wasPressedThisFrame);
            bool end = kb != null && kb.endKey.wasPressedThisFrame;
            if (home)
            {
                int last = sheets.FindLastIndex(v => v.Entry != null && v.Entry.Kind == CodexKind.Word);
                if (last >= 0) scrollTarget = ScrollFor(last);
            }
            else if (end) scrollTarget = MaxScroll;

            if (gp != null && words.Count > 0)
            {
                bool down = gp.dpad.down.wasPressedThisFrame, up = gp.dpad.up.wasPressedThisFrame;
                if (down || up)
                {
                    wordCursor = wordCursor < 0 ? NearestWord() : (wordCursor + (down ? 1 : -1) + words.Count) % words.Count;
                    CodexSheetView owner = words[wordCursor].GetComponentInParent<CodexSheetView>();
                    if (owner != null) scrollTarget = ScrollFor(sheets.IndexOf(owner));
                }
                if (wordCursor >= 0 && gp.buttonSouth.wasPressedThisFrame) ChooseWord(words[wordCursor].Word);
            }
        }

        private int NearestWord()
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < words.Count; i++)
            {
                CodexSheetView owner = words[i].GetComponentInParent<CodexSheetView>();
                int si = owner != null ? sheets.IndexOf(owner) : -1;
                if (si < 0) continue;
                float d = Mathf.Abs(sheetX[si] - (scroll + ViewW * 0.5f));
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private void StepSheet(int dir)
        {
            if (sheets.Count == 0) return;
            float cur = scrollTarget;
            if (dir > 0)
            {
                for (int i = 0; i < sheets.Count; i++)
                {
                    if (sheetX[i] > cur + 4f) { scrollTarget = Mathf.Clamp(sheetX[i], 0f, MaxScroll); return; }
                }
                scrollTarget = MaxScroll;
            }
            else
            {
                for (int i = sheets.Count - 1; i >= 0; i--)
                {
                    if (sheetX[i] < cur - 4f) { scrollTarget = Mathf.Clamp(sheetX[i], 0f, MaxScroll); return; }
                }
                scrollTarget = 0f;
            }
        }

        private void SetFilter(string f)
        {
            if (f == filter) return;
            filter = f;
            StartCoroutine(Refilter());
        }

        private IEnumerator Refilter()
        {
            CanvasGroup g = content.GetComponent<CanvasGroup>();
            if (g == null) g = content.gameObject.AddComponent<CanvasGroup>();
            for (float t = 0f; t < 0.14f; t += Time.unscaledDeltaTime) { g.alpha = 1f - t / 0.14f; yield return null; }
            g.alpha = 0f;
            scroll = scrollTarget = 0f;
            Rebuild();
            scroll = scrollTarget = filter == "All" ? MaxScroll : 0f;
            content.anchoredPosition = new Vector2(-scroll, 0f);
            for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime) { g.alpha = t / 0.22f; yield return null; }
            g.alpha = 1f;
        }

        private static bool Hit(RectTransform r, Vector2 screen) =>
            r != null && r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, screen, null);

        private void OnDestroy()
        {
            CodexStore.Changed -= OnStoreChanged;
            CodexStore.WrittenAny -= OnWrittenAny;
            CodexStore.Revised -= OnRevised;
            CodexStore.EpithetAdded -= OnEpithet;
            CodexStore.Asked -= OnAsked;
            CodexStore.Answered -= OnAnswered;
            CodexStore.Noted -= OnNoted;
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
