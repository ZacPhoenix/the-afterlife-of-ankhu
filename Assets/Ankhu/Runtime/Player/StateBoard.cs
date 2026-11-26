using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ankhu.Player
{
    /// <summary>One room condition the HUD shows: a Gardiner sign, a name, its current value and whether it is met.</summary>
    public struct StateItem
    {
        public string Code;
        public string Label;
        public string Value;
        public bool Done;
        /// <summary>Optional text drawn beside the icon in the cartouche, such as "1/2" for bindings.</summary>
        public string Badge;

        public StateItem(string code, string label, string value, bool done, string badge = null)
        {
            Code = code;
            Label = label;
            Value = value;
            Done = done;
            Badge = badge;
        }
    }

    /// <summary>
    /// Zac 2026-11-25, state pick "B and C, plus A in the first room", all on papyrus.
    /// C: an always-on papyrus cartouche at top left, one ink sign per condition that turns gold when the
    /// condition is met, and the condition's word written under it for two seconds when it changes.
    /// B: hold L to unroll a ledger, THE ROOM SO FAR, with one line per condition and the keys underneath.
    /// A: a one-time key scroll the first time any puzzle room opens. Built at runtime under any canvas, so
    /// both the teaching rooms and the realms use it without a scene rebuild.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StateBoard : MonoBehaviour
    {
        public const Key LedgerKey = Key.L;
        public const string KeyScrollSeenPref = "ankhu.ui.keyscroll.seen";
        private const float IconSize = 46f;
        private const float IconGap = 12f;
        private const float WordSeconds = 2.2f;
        private const float KeyScrollSeconds = 11f;

        private static readonly string[] DefaultKeys =
        {
            "A D", "walk", "W S", "climb", "E", "use", "Q", "inspect", "ENTER", "speak", "U", "undo", "H", "hint", "hold R", "reset",
        };

        private RectTransform root;
        private RectTransform cartouche;
        private RectTransform iconRow;
        private Text changeWord;
        private CanvasGroup changeGroup;
        private RectTransform ledger;
        private CanvasGroup ledgerGroup;
        private RectTransform ledgerLines;
        private RectTransform ledgerKeys;
        private RectTransform resetBar;
        private Image resetFill;
        private Text resetLabel;
        private RectTransform keyScroll;
        private CanvasGroup keyScrollGroup;

        private readonly List<StateItem> items = new List<StateItem>();
        private readonly List<Image> icons = new List<Image>();
        private readonly List<Text> badges = new List<Text>();
        private readonly List<float> pulse = new List<float>();
        private IList<string> keys = DefaultKeys;
        private float wordTimer;
        private float ledgerT;
        private float keyScrollTimer = -1f;
        private bool ledgerDirty = true;
        private bool visible = true;

        /// <summary>When false (dialogue, codex, typing), the cartouche dims and the ledger will not open.</summary>
        public bool InputAllowed { get; set; } = true;

        /// <summary>Holding the ledger key. Exposed for capture tools.</summary>
        public bool LedgerOpen => ledgerT > 0.5f;

        public static StateBoard Install(RectTransform canvas, IList<string> keyPairs = null)
        {
            StateBoard board = canvas.GetComponent<StateBoard>();
            if (board == null) board = canvas.gameObject.AddComponent<StateBoard>();
            board.Build(canvas);
            if (keyPairs != null) board.SetKeys(keyPairs);
            return board;
        }

        private void Build(RectTransform canvas)
        {
            if (root != null) return;
            root = PapyrusKit.Rect("Papyrus State", canvas);
            PapyrusKit.Stretch(root);

            // C. Cartouche.
            cartouche = PapyrusKit.Rect("Cartouche", root);
            PapyrusKit.Place(cartouche, new Vector2(0f, 1f), new Vector2(22f, -18f), new Vector2(200f, 76f));
            RectTransform sheet = PapyrusKit.Sheet("Sheet", cartouche, 22f);
            PapyrusKit.Stretch(sheet);
            Image ring = PapyrusKit.Image("Ring", cartouche, PapyrusKit.Plain("cartouche"), new Color(PapyrusKit.Ink.r, PapyrusKit.Ink.g, PapyrusKit.Ink.b, 0.55f));
            ring.preserveAspect = false;
            PapyrusKit.Stretch(ring.rectTransform, 9f);
            iconRow = PapyrusKit.Rect("Icons", cartouche);
            PapyrusKit.Stretch(iconRow);
            RectTransform lcap = PapyrusKit.Keycap("L", cartouche, 24f, 13);
            lcap.anchorMin = lcap.anchorMax = new Vector2(1f, 0f);
            lcap.anchoredPosition = new Vector2(-4f, 2f);

            changeWord = PapyrusKit.Label("Change", root, "", PapyrusKit.Serif, 26, PapyrusKit.Ink, TextAnchor.UpperLeft);
            changeWord.fontStyle = FontStyle.Italic;
            var outline = changeWord.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.96f, 0.90f, 0.74f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            PapyrusKit.Place(changeWord.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -102f), new Vector2(700f, 36f));
            changeGroup = changeWord.gameObject.AddComponent<CanvasGroup>();
            changeGroup.alpha = 0f;

            // Hold R progress, a rubric line under the cartouche.
            resetBar = PapyrusKit.Rect("Reset", root);
            PapyrusKit.Place(resetBar, new Vector2(0f, 1f), new Vector2(22f, -100f), new Vector2(260f, 44f));
            RectTransform rsheet = PapyrusKit.Sheet("Sheet", resetBar, 14f);
            PapyrusKit.Stretch(rsheet);
            resetLabel = PapyrusKit.Label("Label", resetBar, "Keep holding R to begin again", PapyrusKit.Serif, 19, PapyrusKit.Ink, TextAnchor.UpperCenter);
            resetLabel.fontStyle = FontStyle.Italic;
            PapyrusKit.Stretch(resetLabel.rectTransform, 6f);
            resetFill = PapyrusKit.Image("Fill", resetBar, null, PapyrusKit.Rubric);
            resetFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            resetFill.rectTransform.anchorMax = new Vector2(0f, 0f);
            resetFill.rectTransform.pivot = new Vector2(0f, 0f);
            resetFill.rectTransform.anchoredPosition = new Vector2(16f, 9f);
            resetFill.rectTransform.sizeDelta = new Vector2(0f, 4f);
            resetBar.gameObject.SetActive(false);

            // B. Ledger, unrolls from the top centre while L is held.
            ledger = PapyrusKit.Rect("Ledger", root);
            PapyrusKit.Place(ledger, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(820f, 420f));
            RectTransform lsheet = PapyrusKit.Sheet("Sheet", ledger, 34f);
            PapyrusKit.Stretch(lsheet);
            Text title = PapyrusKit.Label("Title", ledger, "THE ROOM SO FAR", PapyrusKit.Caps, 30, PapyrusKit.Rubric, TextAnchor.UpperCenter);
            PapyrusKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(760f, 40f));
            Image rule = PapyrusKit.Image("Rule", ledger, null, new Color(PapyrusKit.Rubric.r, PapyrusKit.Rubric.g, PapyrusKit.Rubric.b, 0.7f));
            PapyrusKit.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(640f, 2f));
            ledgerLines = PapyrusKit.Rect("Lines", ledger);
            PapyrusKit.Place(ledgerLines, new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(720f, 300f));
            ledgerKeys = PapyrusKit.Rect("Keys", ledger);
            PapyrusKit.Place(ledgerKeys, new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(760f, 34f));
            ledgerKeys.pivot = new Vector2(0.5f, 0.5f);
            ledgerGroup = ledger.gameObject.AddComponent<CanvasGroup>();
            ledgerGroup.alpha = 0f;
            ledger.gameObject.SetActive(false);

            // A. One-time key scroll, the first time any puzzle room opens.
            if (PlayerPrefs.GetInt(KeyScrollSeenPref, 0) == 0)
            {
                BuildKeyScroll();
            }
        }

        private void BuildKeyScroll()
        {
            keyScroll = PapyrusKit.Rect("Key Scroll", root);
            PapyrusKit.Place(keyScroll, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(880f, 150f));
            RectTransform sheet = PapyrusKit.Sheet("Sheet", keyScroll, 30f);
            PapyrusKit.Stretch(sheet);
            RectTransform row1 = PapyrusKit.Rect("Row 1", keyScroll);
            PapyrusKit.Place(row1, new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(800f, 36f));
            row1.pivot = new Vector2(0.5f, 0.5f);
            PapyrusKit.KeyRow(row1, new[] { "A D", "walk", "W S", "climb", "E", "use", "Q", "inspect" }, 34f, 24, 34f);
            RectTransform row2 = PapyrusKit.Rect("Row 2", keyScroll);
            PapyrusKit.Place(row2, new Vector2(0.5f, 0.5f), new Vector2(0f, -26f), new Vector2(800f, 36f));
            row2.pivot = new Vector2(0.5f, 0.5f);
            PapyrusKit.KeyRow(row2, new[] { "ENTER", "speak", "U", "undo", "H", "hint", "hold L", "the room so far" }, 34f, 24, 34f);
            keyScrollGroup = keyScroll.gameObject.AddComponent<CanvasGroup>();
            keyScrollGroup.alpha = 0f;
            keyScrollTimer = 0f;
            PlayerPrefs.SetInt(KeyScrollSeenPref, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Clears the one-time flag so the key scroll shows again. For testing.</summary>
        public static void ForgetKeyScroll()
        {
            PlayerPrefs.DeleteKey(KeyScrollSeenPref);
        }

        public void SetKeys(IList<string> keyPairs)
        {
            keys = keyPairs ?? DefaultKeys;
            ledgerDirty = true;
        }

        public void SetVisible(bool show)
        {
            visible = show;
            if (root != null) root.gameObject.SetActive(show);
        }

        /// <summary>Replaces the shown conditions. Icons that flip state pulse and their word is written under the cartouche.</summary>
        public void SetItems(IList<StateItem> next)
        {
            if (root == null || next == null) return;
            bool rebuild = next.Count != items.Count;
            for (int i = 0; !rebuild && i < next.Count; i++)
            {
                if (next[i].Code != items[i].Code) rebuild = true;
            }
            string word = null;
            if (!rebuild)
            {
                for (int i = 0; i < next.Count; i++)
                {
                    if (next[i].Done != items[i].Done || next[i].Value != items[i].Value || next[i].Badge != items[i].Badge)
                    {
                        pulse[i] = 1f;
                        word = next[i].Label + "  " + next[i].Value;
                    }
                }
            }
            items.Clear();
            items.AddRange(next);
            if (rebuild) RebuildIcons();
            ApplyIcons();
            ledgerDirty = true;
            if (word != null)
            {
                changeWord.text = word;
                wordTimer = WordSeconds;
            }
        }

        public void SetResetProgress(float t)
        {
            if (resetBar == null) return;
            bool on = t > 0.001f;
            if (resetBar.gameObject.activeSelf != on) resetBar.gameObject.SetActive(on);
            if (on) resetFill.rectTransform.sizeDelta = new Vector2((resetBar.rect.width - 32f) * Mathf.Clamp01(t), 4f);
        }

        private void RebuildIcons()
        {
            for (int i = iconRow.childCount - 1; i >= 0; i--) Destroy(iconRow.GetChild(i).gameObject);
            icons.Clear();
            badges.Clear();
            pulse.Clear();
            float x = 26f;
            for (int i = 0; i < items.Count; i++)
            {
                RectTransform cell = PapyrusKit.Rect("Sign " + items[i].Code, iconRow);
                Image img = PapyrusKit.Image("Glyph", cell, PapyrusKit.Plain(items[i].Code), PapyrusKit.Ink);
                PapyrusKit.Stretch(img.rectTransform);
                float w = IconSize;
                Text badge = null;
                if (!string.IsNullOrEmpty(items[i].Badge))
                {
                    badge = PapyrusKit.Label("Badge", iconRow, items[i].Badge, PapyrusKit.Caps, 22, PapyrusKit.Ink, TextAnchor.MiddleLeft);
                    badge.fontStyle = FontStyle.Bold;
                    PapyrusKit.Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(x + IconSize + 2f, 0f), new Vector2(48f, IconSize));
                    badge.rectTransform.pivot = new Vector2(0f, 0.5f);
                    w += 44f;
                }
                PapyrusKit.Place(cell, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(IconSize, IconSize));
                cell.pivot = new Vector2(0f, 0.5f);
                icons.Add(img);
                badges.Add(badge);
                pulse.Add(0f);
                x += w + IconGap;
            }
            cartouche.sizeDelta = new Vector2(Mathf.Max(140f, x + 26f), cartouche.sizeDelta.y);
        }

        private void ApplyIcons()
        {
            for (int i = 0; i < icons.Count && i < items.Count; i++)
            {
                icons[i].color = items[i].Done ? PapyrusKit.Gold : PapyrusKit.Ink;
                if (badges[i] != null)
                {
                    badges[i].text = items[i].Badge;
                    badges[i].color = items[i].Done ? PapyrusKit.Gold : PapyrusKit.Ink;
                }
            }
        }

        private void RebuildLedger()
        {
            ledgerDirty = false;
            for (int i = ledgerLines.childCount - 1; i >= 0; i--) Destroy(ledgerLines.GetChild(i).gameObject);
            const float lineH = 50f;
            for (int i = 0; i < items.Count; i++)
            {
                StateItem it = items[i];
                RectTransform line = PapyrusKit.Rect("Line " + it.Code, ledgerLines);
                PapyrusKit.Place(line, new Vector2(0f, 1f), new Vector2(0f, -i * lineH), new Vector2(720f, lineH));
                Image g = PapyrusKit.Image("Glyph", line, PapyrusKit.Plain(it.Code), it.Done ? PapyrusKit.Gold : PapyrusKit.Ink);
                PapyrusKit.Place(g.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(40f, 40f));
                Text name = PapyrusKit.Label("Name", line, it.Label, PapyrusKit.Caps, 24, PapyrusKit.Rubric, TextAnchor.MiddleLeft);
                PapyrusKit.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(104f, 0f), new Vector2(200f, lineH));
                Text value = PapyrusKit.Label("Value", line, it.Value, PapyrusKit.Serif, 28, it.Done ? new Color(0.55f, 0.36f, 0.05f) : PapyrusKit.Ink, TextAnchor.MiddleLeft);
                value.fontStyle = it.Done ? FontStyle.BoldAndItalic : FontStyle.Italic;
                PapyrusKit.Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(310f, 0f), new Vector2(400f, lineH));
            }
            float h = 88f + items.Count * lineH + 30f + 70f;
            ledger.sizeDelta = new Vector2(820f, Mathf.Max(260f, h));
            PapyrusKit.KeyRow(ledgerKeys, keys, 30f, 20, 18f);
        }

        private void Update()
        {
            if (root == null || !visible) return;
            float dt = Time.unscaledDeltaTime;

            bool hold = InputAllowed && GameplayKeys.Held(LedgerKey);
            if (hold && ledgerDirty) RebuildLedger();
            ledgerT = Mathf.MoveTowards(ledgerT, hold ? 1f : 0f, dt / 0.18f);
            bool show = ledgerT > 0.001f;
            if (ledger.gameObject.activeSelf != show) ledger.gameObject.SetActive(show);
            if (show)
            {
                float e = 1f - (1f - ledgerT) * (1f - ledgerT);
                ledgerGroup.alpha = e;
                ledger.anchoredPosition = new Vector2(0f, Mathf.Lerp(ledger.sizeDelta.y * 0.35f, -24f, e));
                ledger.localScale = new Vector3(1f, Mathf.Lerp(0.6f, 1f, e), 1f);
            }

            for (int i = 0; i < icons.Count; i++)
            {
                if (pulse[i] <= 0f) { icons[i].rectTransform.localScale = Vector3.one; continue; }
                pulse[i] = Mathf.Max(0f, pulse[i] - dt / 0.9f);
                float s = 1f + 0.35f * Mathf.Sin(pulse[i] * Mathf.PI);
                icons[i].rectTransform.localScale = new Vector3(s, s, 1f);
                if (items[i].Done) icons[i].color = Color.Lerp(PapyrusKit.Gold, PapyrusKit.GoldBright, pulse[i]);
            }

            if (wordTimer > 0f)
            {
                wordTimer -= dt;
                float a = Mathf.Clamp01(Mathf.Min((WordSeconds - wordTimer) / 0.2f, wordTimer / 0.5f));
                changeGroup.alpha = a;
            }
            else if (changeGroup.alpha > 0f)
            {
                changeGroup.alpha = 0f;
            }
            if (resetBar.gameObject.activeSelf) changeGroup.alpha = 0f;

            if (keyScroll != null && keyScrollTimer >= 0f)
            {
                keyScrollTimer += dt;
                float a = Mathf.Clamp01(Mathf.Min(keyScrollTimer / 0.6f, (KeyScrollSeconds - keyScrollTimer) / 1.2f));
                keyScrollGroup.alpha = a;
                if (keyScrollTimer >= KeyScrollSeconds)
                {
                    Destroy(keyScroll.gameObject);
                    keyScroll = null;
                }
            }
        }
    }
}
