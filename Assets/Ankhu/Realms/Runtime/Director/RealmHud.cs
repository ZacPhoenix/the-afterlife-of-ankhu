using System.Collections;
using Ankhu.Opening;
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
        private Text promptKey;
        private Text promptLabel;
        private CanvasGroup hintGroup;
        private Text hintLabel;
        private Text legend;
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

            // Use prompt, centre-low, above the dialogue band.
            RectTransform pr = UiKit.Rect("Prompt", canvas.transform);
            UiKit.Place(pr, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(700f, 54f));
            promptGroup = pr.gameObject.AddComponent<CanvasGroup>();
            promptGroup.alpha = 0f;
            Image keyBox = UiKit.Solid("KeyBox", pr, new Color(0.03f, 0.04f, 0.08f, 0.8f));
            UiKit.Place(keyBox.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-150f, 0f), new Vector2(44f, 44f));
            Outline o = keyBox.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.9f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            promptKey = UiKit.Label("Key", keyBox.rectTransform, "E", RealmFonts.Caps, 26, Gold, TextAnchor.MiddleCenter);
            UiKit.Stretch(promptKey.rectTransform);
            promptLabel = UiKit.Label("Action", pr, "", RealmFonts.Body, 32, Text, TextAnchor.MiddleLeft);
            UiKit.Place(promptLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(90f, 0f), new Vector2(440f, 50f));
            AddShadow(promptLabel, 0.8f);

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

        public void ShowPrompt(string key, string label)
        {
            promptKey.text = key;
            promptLabel.text = label;
            promptTarget = 1f;
        }

        public void HidePrompt() { promptTarget = 0f; }

        public void ShowHint(string text, float seconds = 7f)
        {
            hintLabel.text = text;
            hintHideAt = Time.unscaledTime + seconds;
        }

        public void SetLegendVisible(bool on) { legend.enabled = on; }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            dialogueGroup.alpha = Mathf.MoveTowards(dialogueGroup.alpha, dialogueTarget, dt * 4f);
            promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, promptTarget, dt * 5f);
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
