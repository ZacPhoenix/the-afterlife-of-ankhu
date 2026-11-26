using System.Collections;
using System.Collections.Generic;
using Ankhu.Opening;
using Ankhu.Player;
using Ankhu.Realms.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Realms
{
    /// <summary>
    /// The realm screen layer: the dialogue panel (speaker name in gold capitals over the line), centred title
    /// cards, full-screen memory flashbacks, the "E Read" use prompt, the hint line and the key legend.
    /// Built in code at runtime.
    /// </summary>
    public sealed class RealmHud : MonoBehaviour
    {
        private static readonly Color PanelDark = new Color(0.02f, 0.025f, 0.045f, 0.72f);
        private static readonly Color Gold = new Color(0.96f, 0.79f, 0.40f, 1f);
        private static readonly Color Text = new Color(0.95f, 0.92f, 0.85f, 1f);

        private Canvas canvas;
        private CanvasGroup dialogueGroup;
        private Text speakerLabel;
        private Text lineLabel;
        private Image speakerRule;
        private CanvasGroup titleGroup;
        private Text titleLabel;
        private Text titleSub;
        private CanvasGroup memoryGroup;
        private RawImage memoryImage;
        private Text memorySpeaker;
        private Text memoryLine;
        private CanvasGroup promptGroup;
        private RectTransform promptRoot;
        private RectTransform promptSheet;
        private RectTransform promptRow;
        private string promptShown;
        private Vector3? promptWorld;
        private RectTransform glintRoot;
        private CanvasGroup glintGroup;
        private Image glintStar;
        private float glintTarget;
        private Vector3? glintWorld;
        private const float GlintSize = 54f;
        private CanvasGroup hintGroup;
        private Text hintLabel;
        private Text legend;
        private StateBoard board;
        private HintScroll hintScroll;

        /// <summary>Zac 2026-11-25 picks: keys the realm ledger lists under its lines.</summary>
        public static readonly string[] RealmKeys = { "A D", "walk", "E", "use", "ENTER", "speak", "TAB", "codex", "H", "hint" };
        private float dialogueTarget;
        private float promptTarget;
        private float hintHideAt;
        private float memoryTarget;
        private Texture2D vignette;

        public bool MemoryShowing => memoryTarget > 0f;

        public static RealmHud Create()
        {
            var go = new GameObject("RealmHud");
            var hud = go.AddComponent<RealmHud>();
            hud.Build();
            return hud;
        }

        private void Build()
        {
            canvas = UiKit.Canvas("RealmHudCanvas", 300, transform);

            // Memory flashback, under the dialogue so lines read over it.
            RectTransform mem = UiKit.Rect("Memory", canvas.transform);
            UiKit.Stretch(mem);
            memoryGroup = mem.gameObject.AddComponent<CanvasGroup>();
            memoryGroup.alpha = 0f;
            Image memBack = UiKit.Solid("MemoryBlack", mem, new Color(0.03f, 0.02f, 0.015f, 0.96f));
            UiKit.Stretch(memBack.rectTransform);
            memoryImage = UiKit.Raw("MemoryImage", mem, null);
            UiKit.Stretch(memoryImage.rectTransform, -40f);
            memoryImage.color = new Color(1f, 0.93f, 0.82f, 1f);
            vignette = MakeVignette(256);
            RawImage vig = UiKit.Raw("MemoryVignette", mem, vignette);
            UiKit.Stretch(vig.rectTransform);
            memorySpeaker = UiKit.Label("MemorySpeaker", mem, "", RealmFonts.Caps, 26, Gold, TextAnchor.LowerCenter);
            UiKit.Place(memorySpeaker.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1400f, 40f));
            memoryLine = UiKit.Label("MemoryLine", mem, "", RealmFonts.Body, 42, Text, TextAnchor.UpperCenter);
            memoryLine.fontStyle = FontStyle.Italic;
            UiKit.Place(memoryLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1400f, 130f));
            AddShadow(memoryLine, 0.9f);

            // Dialogue panel: a soft dark band at the bottom with a thin gold rule under the speaker.
            RectTransform dlg = UiKit.Rect("Dialogue", canvas.transform);
            UiKit.Place(dlg, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(1500f, 190f));
            dialogueGroup = dlg.gameObject.AddComponent<CanvasGroup>();
            dialogueGroup.alpha = 0f;
            RawImage band = UiKit.Raw("Band", dlg, MakeBand(64));
            UiKit.Stretch(band.rectTransform, -10f);
            band.color = PanelDark;
            speakerLabel = UiKit.Label("Speaker", dlg, "", RealmFonts.Caps, 24, Gold, TextAnchor.MiddleCenter);
            UiKit.Place(speakerLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(800f, 34f));
            speakerRule = UiKit.Solid("Rule", dlg, new Color(Gold.r, Gold.g, Gold.b, 0.55f));
            UiKit.Place(speakerRule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(220f, 1.5f));
            lineLabel = UiKit.Label("Line", dlg, "", RealmFonts.Body, 38, Text, TextAnchor.UpperCenter);
            UiKit.Place(lineLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(1380f, 120f));
            AddShadow(lineLabel, 0.7f);

            // Title card.
            RectTransform title = UiKit.Rect("Title", canvas.transform);
            UiKit.Stretch(title);
            titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;
            titleLabel = UiKit.Label("TitleMain", title, "", RealmFonts.Caps, 92, Gold, TextAnchor.MiddleCenter);
            UiKit.Place(titleLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1700f, 140f));
            AddShadow(titleLabel, 0.8f);
            Image titleRule = UiKit.Solid("TitleRule", title, new Color(Gold.r, Gold.g, Gold.b, 0.6f));
            UiKit.Place(titleRule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(380f, 1.5f));
            titleSub = UiKit.Label("TitleSub", title, "", RealmFonts.Body, 40, Text, TextAnchor.MiddleCenter);
            titleSub.fontStyle = FontStyle.Italic;
            UiKit.Place(titleSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(1500f, 70f));
            AddShadow(titleSub, 0.8f);

            // Zac 2026-11-25, prompts pick B "painted glint first": a soft gold glint marks a usable thing when
            // Ankhu is near it, and only when he stands at it does a papyrus slip with the keycap and verb appear,
            // hung over the thing itself. Falls back to centre-low when there is no camera to project through.
            glintRoot = UiKit.Rect("Glint", canvas.transform);
            glintRoot.anchorMin = glintRoot.anchorMax = glintRoot.pivot = new Vector2(0.5f, 0.5f);
            glintRoot.sizeDelta = new Vector2(GlintSize, GlintSize);
            glintGroup = glintRoot.gameObject.AddComponent<CanvasGroup>();
            glintGroup.alpha = 0f;
            Image halo = PapyrusKit.Image("Halo", glintRoot, PapyrusKit.Plain("fx_mote"), new Color(1f, 0.78f, 0.36f, 0.35f));
            PapyrusKit.Stretch(halo.rectTransform, -GlintSize * 0.25f);
            glintStar = PapyrusKit.Image("Star", glintRoot, PapyrusKit.Plain("fx_flare") ?? PapyrusKit.Plain("fx_spark"), PapyrusKit.GoldBright);
            PapyrusKit.Stretch(glintStar.rectTransform);

            RectTransform pr = UiKit.Rect("Prompt", canvas.transform);
            UiKit.Place(pr, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(320f, 60f));
            pr.pivot = new Vector2(0.5f, 0f);
            promptRoot = pr;
            promptGroup = pr.gameObject.AddComponent<CanvasGroup>();
            promptGroup.alpha = 0f;
            promptSheet = PapyrusKit.Sheet("Slip", pr, 14f);
            PapyrusKit.Stretch(promptSheet);
            promptRow = PapyrusKit.Rect("Row", pr);
            promptRow.anchorMin = promptRow.anchorMax = promptRow.pivot = new Vector2(0.5f, 0.5f);
            promptRow.anchoredPosition = new Vector2(0f, 1f);

            // Hint line, top centre.
            RectTransform hr = UiKit.Rect("Hint", canvas.transform);
            UiKit.Place(hr, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(1300f, 90f));
            hintGroup = hr.gameObject.AddComponent<CanvasGroup>();
            hintGroup.alpha = 0f;
            RawImage hb = UiKit.Raw("HintBand", hr, MakeBand(64));
            UiKit.Stretch(hb.rectTransform, -6f);
            hb.color = new Color(0.02f, 0.03f, 0.07f, 0.6f);
            hintLabel = UiKit.Label("HintText", hr, "", RealmFonts.Body, 32, new Color(0.88f, 0.9f, 1f, 1f), TextAnchor.MiddleCenter);
            hintLabel.fontStyle = FontStyle.Italic;
            UiKit.Stretch(hintLabel.rectTransform, 12f);

            // Key legend, bottom left, quiet.
            legend = UiKit.Label("Legend", canvas.transform, "ENTER  speak      E  use      TAB  codex      H  hint", RealmFonts.Caps, 17,
                new Color(0.9f, 0.85f, 0.72f, 0.55f), TextAnchor.LowerLeft);
            UiKit.Place(legend.rectTransform, new Vector2(0f, 0f), new Vector2(28f, 18f), new Vector2(900f, 30f));

            // Zac 2026-11-25 picks: the papyrus cartouche and ledger (state B+C) and the sealed hint scroll (hints B)
            // replace the quiet legend and the top hint line for H. The hint line stays for Anpu's spoken asides.
            board = StateBoard.Install((RectTransform)canvas.transform, RealmKeys);
            hintScroll = HintScroll.Install((RectTransform)canvas.transform);
            legend.gameObject.SetActive(false);
        }

        private static void AddShadow(Graphic g, float a)
        {
            Shadow s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, a);
            s.effectDistance = new Vector2(2f, -2f);
        }

        public static string SpeakerName(Speaker who)
        {
            switch (who)
            {
                case Speaker.Ankhu: return "Ankhu";
                case Speaker.Anpu: return "Anpu";
                case Speaker.Keeper: return "Face-downward";
                case Speaker.Guardian: return "Eavesdropper";
                case Speaker.Announcer: return "Sad of Voice";
                case Speaker.Neferet: return "Neferet";
                case Speaker.Mother: return "His mother";
                case Speaker.Hori: return "Hori";
                case Speaker.Father: return "His father";
                case Speaker.Other: return "";
                default: return "";
            }
        }

        public void ShowLine(Speaker who, string text)
        {
            string name = SpeakerName(who);
            speakerLabel.text = name.ToUpperInvariant();
            speakerRule.enabled = name.Length > 0;
            speakerLabel.color = who == Speaker.Anpu ? new Color(0.78f, 0.84f, 1f) : Gold;
            lineLabel.text = text;
            dialogueTarget = 1f;
        }

        public void HideLine() { dialogueTarget = 0f; }

        public void ShowMemory(Texture tex, Speaker who, string text)
        {
            if (memoryTarget <= 0f && tex != null) memoryImage.rectTransform.localScale = Vector3.one * 1.0f;
            if (tex != null) memoryImage.texture = tex;
            memoryImage.enabled = memoryImage.texture != null;
            memorySpeaker.text = SpeakerName(who).ToUpperInvariant();
            memoryLine.text = text;
            memoryTarget = 1f;
        }

        public void HideMemory() { memoryTarget = 0f; }

        public IEnumerator Title(string text, float seconds)
        {
            string[] parts = (text ?? string.Empty).Split('\n');
            titleLabel.text = parts[0].ToUpperInvariant();
            titleSub.text = parts.Length > 1 ? parts[1] : string.Empty;
            float t = 0f;
            float fade = 1.1f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Min(Mathf.Clamp01(t / fade), Mathf.Clamp01((seconds - t) / fade));
                titleGroup.alpha = a * a * (3f - 2f * a);
                titleLabel.rectTransform.localScale = Vector3.one * (1.04f - 0.04f * Mathf.Clamp01(t / seconds));
                yield return null;
            }
            titleGroup.alpha = 0f;
        }

        public void ShowPrompt(string key, string label) => ShowPrompt(key, label, null);

        /// <summary>The adjacent state: a papyrus slip with the keycap and verb, hung over <paramref name="world"/> when given.</summary>
        public void ShowPrompt(string key, string label, Vector3? world)
        {
            string shown = key + "|" + label;
            if (shown != promptShown)
            {
                promptShown = shown;
                float w = PapyrusKit.KeyRow(promptRow, new[] { key, label }, 34f, 26, 24f);
                promptRoot.sizeDelta = new Vector2(w + 44f, 60f);
            }
            promptWorld = world;
            promptTarget = 1f;
        }

        public void HidePrompt() { promptTarget = 0f; }

        /// <summary>The near state: a soft gold glint over a usable thing, no words. Null hides it.</summary>
        public void ShowGlint(Vector3? world)
        {
            if (world.HasValue) glintWorld = world;
            glintTarget = world.HasValue ? 1f : 0f;
        }

        /// <summary>Screen position (canvas units) of a world point, or null when it is behind the camera or there is none.</summary>
        private Vector2? ToCanvas(Vector3 world)
        {
            Camera cam = Camera.main;
            if (cam == null) return null;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, sp, null, out Vector2 local)
                ? local : (Vector2?)null;
        }

        private void PlaceWorldUi(float dt)
        {
            glintGroup.alpha = Mathf.MoveTowards(glintGroup.alpha, glintTarget * (1f - promptGroup.alpha), dt * 3f);
            if (glintGroup.alpha > 0f && glintWorld.HasValue)
            {
                Vector2? at = ToCanvas(glintWorld.Value);
                if (at.HasValue) glintRoot.anchoredPosition = at.Value;
                float t = Time.unscaledTime;
                float pulse = 0.82f + 0.18f * Mathf.Sin(t * 2.6f);
                glintStar.rectTransform.localScale = Vector3.one * pulse;
                glintStar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 18f);
            }
            if (promptGroup.alpha > 0f)
            {
                Vector2? at = promptWorld.HasValue ? ToCanvas(promptWorld.Value) : null;
                if (at.HasValue)
                {
                    promptRoot.anchorMin = promptRoot.anchorMax = new Vector2(0.5f, 0.5f);
                    Vector2 half = ((RectTransform)canvas.transform).rect.size * 0.5f;
                    Vector2 p = at.Value + new Vector2(0f, 40f);
                    float hw = promptRoot.sizeDelta.x * 0.5f + 20f;
                    p.x = Mathf.Clamp(p.x, -half.x + hw, half.x - hw);
                    p.y = Mathf.Clamp(p.y, -half.y + 260f, half.y - 140f);
                    promptRoot.anchoredPosition = p;
                }
                else
                {
                    promptRoot.anchorMin = promptRoot.anchorMax = new Vector2(0.5f, 0f);
                    promptRoot.anchoredPosition = new Vector2(0f, 250f);
                }
                float k = promptGroup.alpha;
                promptRoot.localScale = new Vector3(1f, Mathf.Lerp(0.6f, 1f, 1f - (1f - k) * (1f - k)), 1f);
            }
        }

        public void ShowHint(string text, float seconds = 7f)
        {
            hintLabel.text = text;
            hintHideAt = Time.unscaledTime + seconds;
        }

        public void SetLegendVisible(bool on)
        {
            if (board != null) board.InputAllowed = on;
        }

        private readonly List<StateItem> stateNow = new List<StateItem>();
        private readonly List<StateItem> stateShown = new List<StateItem>();

        /// <summary>
        /// The room's goal conditions as cartouche icons and ledger lines: one sign per goal object (its gloss's last
        /// sign, usually the determinative), the object's name, its state, gold once the condition holds.
        /// Cheap to call every frame: the board is only touched when something changed.
        /// </summary>
        public void SetRoomState(RealmRoom room)
        {
            if (board == null || room == null) return;
            stateNow.Clear();
            foreach (Cond c in room.Def.Goal)
            {
                RoomObject o = room.Def.Find(c.Id);
                string code = "V10";
                if (o != null && o.Gloss != null && o.Gloss.Signs.Count > 0)
                {
                    string last = o.Gloss.Signs[o.Gloss.Signs.Count - 1];
                    if (PapyrusKit.Plain(last) != null) code = last;
                }
                room.State.TryGetValue(c.Id, out string v);
                string label = o != null && !string.IsNullOrEmpty(o.Name) ? o.Name : c.Id;
                stateNow.Add(new StateItem(code, label.ToUpperInvariant(), (v ?? "").Replace('_', ' '), c.Holds(room.State)));
            }
            bool same = stateNow.Count == stateShown.Count;
            for (int i = 0; same && i < stateNow.Count; i++)
                same = stateNow[i].Code == stateShown[i].Code && stateNow[i].Value == stateShown[i].Value && stateNow[i].Done == stateShown[i].Done;
            if (same) return;
            stateShown.Clear();
            stateShown.AddRange(stateNow);
            board.SetItems(stateShown);
        }

        /// <summary>The H press: loads the tiers (seals close again when they change) and breaks the next seal.</summary>
        public void ShowHintTiers(string[] tiers)
        {
            if (hintScroll == null) { if (tiers != null && tiers.Length > 0) ShowHint(tiers[0], 8f); return; }
            hintScroll.SetTiers(tiers);
            hintScroll.BreakNext();
        }

        public void HideHintScroll()
        {
            if (hintScroll != null) hintScroll.Hide();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            dialogueGroup.alpha = Mathf.MoveTowards(dialogueGroup.alpha, dialogueTarget, dt * 4f);
            promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, promptTarget, dt * 5f);
            PlaceWorldUi(dt);
            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, Time.unscaledTime < hintHideAt ? 1f : 0f, dt * 3f);
            memoryGroup.alpha = Mathf.MoveTowards(memoryGroup.alpha, memoryTarget, dt * (memoryTarget > 0f ? 0.9f : 1.4f));
            if (memoryGroup.alpha > 0f)
            {
                float s = memoryImage.rectTransform.localScale.x + dt * 0.006f;
                memoryImage.rectTransform.localScale = Vector3.one * Mathf.Min(s, 1.12f);
            }
        }

        private static Texture2D MakeBand(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = Mathf.Abs(x / (size - 1f) - 0.5f) * 2f;
                    float v = Mathf.Abs(y / (size - 1f) - 0.5f) * 2f;
                    float a = Mathf.Clamp01(1f - Mathf.Pow(u, 6f)) * Mathf.Clamp01(1f - Mathf.Pow(v, 3f));
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }

        private static Texture2D MakeVignette(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f) * 2f - 1f;
                    float v = y / (size - 1f) * 2f - 1f;
                    float d = Mathf.Sqrt(u * u * 0.8f + v * v);
                    float a = Mathf.SmoothStep(0.55f, 1.25f, d);
                    float bottom = Mathf.SmoothStep(0.55f, 0f, y / (size - 1f)) * 0.85f;
                    t.SetPixel(x, y, new Color(0.04f, 0.025f, 0.01f, Mathf.Clamp01(Mathf.Max(a, bottom))));
                }
            t.Apply();
            return t;
        }
    }
}
