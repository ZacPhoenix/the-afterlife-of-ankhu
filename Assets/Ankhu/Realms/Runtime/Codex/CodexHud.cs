using Ankhu.Opening;
using Ankhu.Player;
using Ankhu.Realms.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Realms
{
    /// <summary>
    /// The small always visible codex at the bottom right of the screen, with an unread badge and a key hint.
    /// The book flies out of it when opened and back into it when closed. Click it to open.
    /// </summary>
    public sealed class CodexHud
    {
        // Zac 2026-11-25, codex pick A: a larger roll with a papyrus TAB keycap and a wax badge naming the new entry.
        private const float IconSize = 124f;
        private const float KeyHeight = 32f;
        private const float NewBadgeSeconds = 6.5f;
        private static readonly Vector2 Margin = new Vector2(34f, 30f);

        private RectTransform root;
        private CanvasGroup group;
        private RectTransform icon;
        private RawImage glow;
        private RectTransform badge;
        private Text badgeText;
        private RectTransform keyRow;
        private int shownCount = -1;
        private float punch;
        private float glowPhase;
        private RectTransform newBadge;
        private CanvasGroup newGroup;
        private Text newTitle;
        private float newShownFor = -1f;

        public static CodexHud Build(RectTransform canvas)
        {
            var h = new CodexHud();
            h.root = CodexArt.Rect("CodexHud", canvas);
            h.root.anchorMin = h.root.anchorMax = h.root.pivot = new Vector2(1f, 0f);
            h.root.anchoredPosition = new Vector2(-Margin.x, Margin.y);
            h.root.sizeDelta = new Vector2(IconSize, IconSize + KeyHeight + 6f);
            h.group = h.root.gameObject.AddComponent<CanvasGroup>();
            h.group.blocksRaycasts = false;
            h.group.interactable = false;

            h.glow = CodexArt.Raw("Glow", h.root, UiKit.SoftDot(64, 1.8f), new Color(1f, 0.75f, 0.35f, 0f));
            RectTransform gr = h.glow.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 1f);
            gr.pivot = new Vector2(0.5f, 0.5f);
            gr.anchoredPosition = new Vector2(0f, -IconSize * 0.5f);
            gr.sizeDelta = new Vector2(IconSize * 2.1f, IconSize * 2.1f);

            // The icon is his roll, tied with its cord. A painted Resources/RealmCodex/roll_icon replaces the drawn one.
            if (CodexArt.HasTex("roll_icon"))
            {
                RawImage img = CodexArt.Raw("Icon", h.root, CodexArt.Tex("roll_icon"), Color.white);
                h.icon = img.rectTransform;
            }
            else
            {
                h.icon = DrawnRoll(h.root);
            }
            h.icon.anchorMin = h.icon.anchorMax = new Vector2(0.5f, 1f);
            h.icon.pivot = new Vector2(0.5f, 0.5f);
            h.icon.anchoredPosition = new Vector2(0f, -IconSize * 0.5f);
            h.icon.sizeDelta = new Vector2(IconSize, IconSize);

            // Unread badge: red ochre disc in a gold ring.
            h.badge = CodexArt.Rect("Badge", h.icon);
            h.badge.anchorMin = h.badge.anchorMax = new Vector2(1f, 1f);
            h.badge.pivot = new Vector2(0.5f, 0.5f);
            h.badge.anchoredPosition = new Vector2(-14f, -14f);
            h.badge.sizeDelta = new Vector2(30f, 30f);
            RawImage ring = CodexArt.Raw("Ring", h.badge, CodexArt.Disc, CodexArt.Gold);
            CodexPages.UiStretch(ring.rectTransform);
            RawImage fill = CodexArt.Raw("Fill", h.badge, CodexArt.Disc, new Color(0.55f, 0.14f, 0.06f, 1f));
            CodexPages.UiStretch(fill.rectTransform);
            fill.rectTransform.offsetMin = new Vector2(2.5f, 2.5f);
            fill.rectTransform.offsetMax = new Vector2(-2.5f, -2.5f);
            CodexGradient.Add(fill, new Color(1.25f, 1.2f, 1.1f, 1f), new Color(0.8f, 0.75f, 0.7f, 1f), true);
            h.badgeText = CodexArt.Label("Count", h.badge, "0", CodexArt.Caps, 16, CodexArt.Parchment, TextAnchor.MiddleCenter);
            CodexPages.UiStretch(h.badgeText.rectTransform);
            h.badgeText.rectTransform.offsetMin = new Vector2(0f, 1f);

            h.keyRow = CodexArt.Rect("Key", h.root);
            h.keyRow.anchorMin = h.keyRow.anchorMax = new Vector2(0.5f, 0f);
            h.keyRow.pivot = new Vector2(0.5f, 0f);
            h.keyRow.anchoredPosition = new Vector2(0f, 0f);
            h.BuildKey(false);
            h.BuildNewBadge();
            CodexStore.Written += h.OnWritten;
            return h;
        }

        /// <summary>A papyrus slip to the left of the roll, pinned by a wax seal, naming the entry just written.</summary>
        private void BuildNewBadge()
        {
            newBadge = PapyrusKit.Rect("New Entry", root);
            newBadge.anchorMin = newBadge.anchorMax = new Vector2(0f, 1f);
            newBadge.pivot = new Vector2(1f, 0.5f);
            newBadge.anchoredPosition = new Vector2(-6f, -IconSize * 0.5f);
            newBadge.sizeDelta = new Vector2(300f, 62f);
            RectTransform sheet = PapyrusKit.Sheet("Slip", newBadge, 14f);
            PapyrusKit.Stretch(sheet);
            RectTransform seal = PapyrusKit.Seal(newBadge, 40f);
            seal.anchorMin = seal.anchorMax = new Vector2(0f, 0.5f);
            seal.anchoredPosition = new Vector2(30f, 0f);
            Text star = PapyrusKit.Label("Mark", seal, "NEW", PapyrusKit.Caps, 11, new Color(1f, 0.86f, 0.70f, 0.95f), TextAnchor.MiddleCenter);
            PapyrusKit.Stretch(star.rectTransform);
            Text head = PapyrusKit.Label("Head", newBadge, "WRITTEN IN THE CODEX", PapyrusKit.Caps, 13, PapyrusKit.Rubric, TextAnchor.UpperLeft);
            PapyrusKit.Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(60f, -9f), new Vector2(230f, 18f));
            newTitle = PapyrusKit.Label("Title", newBadge, "", PapyrusKit.Serif, 22, PapyrusKit.Ink, TextAnchor.UpperLeft);
            newTitle.fontStyle = FontStyle.Bold;
            PapyrusKit.Place(newTitle.rectTransform, new Vector2(0f, 1f), new Vector2(60f, -27f), new Vector2(230f, 28f));
            newGroup = newBadge.gameObject.AddComponent<CanvasGroup>();
            newGroup.alpha = 0f;
            newGroup.blocksRaycasts = false;
            newBadge.gameObject.SetActive(false);
        }

        private void OnWritten(CodexEntry e)
        {
            if (root == null) { CodexStore.Written -= OnWritten; return; }
            if (e == null || newBadge == null) return;
            newTitle.text = string.IsNullOrEmpty(e.Title) ? e.Id : e.Title;
            float w = Mathf.Clamp(newTitle.preferredWidth + 84f, 260f, 460f);
            newBadge.sizeDelta = new Vector2(w, 62f);
            newTitle.rectTransform.sizeDelta = new Vector2(w - 70f, 28f);
            newShownFor = 0f;
            newBadge.gameObject.SetActive(true);
            punch = 1f;
        }

        private bool keyGamepad;

        /// <summary>A small closed roll lying at a slant: papyrus cylinder, ebony caps, cream cord, a green heart scarab (roll E).</summary>
        private static RectTransform DrawnRoll(RectTransform parent)
        {
            RectTransform r = CodexArt.Rect("Icon", parent);
            RectTransform body = CodexArt.Rect("Roll", r);
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
            body.sizeDelta = new Vector2(IconSize * 0.82f, IconSize * 0.30f);
            body.localEulerAngles = new Vector3(0f, 0f, 28f);

            RawImage shadow = CodexArt.Raw("Shadow", r, UiKit.SoftDot(32, 1.5f), new Color(0f, 0f, 0f, 0.45f));
            shadow.rectTransform.anchorMin = shadow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            shadow.rectTransform.sizeDelta = new Vector2(IconSize * 0.9f, IconSize * 0.36f);
            shadow.rectTransform.anchoredPosition = new Vector2(3f, -IconSize * 0.2f);
            shadow.rectTransform.SetAsFirstSibling();

            RawImage paper = CodexArt.Raw("Papyrus", body, CodexArt.Tex("papyrus_page"), new Color(0.95f, 0.88f, 0.72f, 1f));
            if (!CodexArt.HasTex("papyrus_page")) paper.color = new Color(0.86f, 0.74f, 0.52f, 1f);
            paper.uvRect = new UnityEngine.Rect(0.3f, 0.3f, 0.3f, 0.12f);
            CodexPages.UiStretch(paper.rectTransform);
            paper.rectTransform.offsetMin = new Vector2(8f, 0f);
            paper.rectTransform.offsetMax = new Vector2(-8f, 0f);
            CodexGradient.Add(paper, new Color(1.2f, 1.15f, 1.05f, 1f), new Color(0.62f, 0.55f, 0.45f, 1f), true);

            for (int side = 0; side < 2; side++)
            {
                Image cap = CodexArt.Solid(side == 0 ? "CapLeft" : "CapRight", body, new Color(0.10f, 0.07f, 0.05f, 1f));
                RectTransform cr = cap.rectTransform;
                cr.anchorMin = cr.anchorMax = new Vector2(side == 0 ? 0f : 1f, 0.5f);
                cr.pivot = new Vector2(0.5f, 0.5f);
                cr.sizeDelta = new Vector2(11f, IconSize * 0.36f);
                cr.anchoredPosition = new Vector2(side == 0 ? 6f : -6f, 0f);
                CodexGradient.Add(cap, new Color(1.6f, 1.4f, 1.2f, 1f), new Color(0.6f, 0.55f, 0.5f, 1f), true);
                Image gold = CodexArt.Solid("Gold", cr, CodexArt.GoldInk);
                gold.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                gold.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                gold.rectTransform.sizeDelta = new Vector2(3f, -4f);
                gold.rectTransform.anchoredPosition = new Vector2(side == 0 ? -3f : 3f, 0f);
            }
            Image cord = CodexArt.Solid("Cord", body, new Color(0.88f, 0.82f, 0.68f, 1f));
            cord.rectTransform.anchorMin = cord.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            cord.rectTransform.sizeDelta = new Vector2(5f, IconSize * 0.33f);
            cord.rectTransform.anchoredPosition = new Vector2(-4f, 0f);
            Image cord2 = CodexArt.Solid("Cord2", body, new Color(0.74f, 0.68f, 0.55f, 1f));
            cord2.rectTransform.anchorMin = cord2.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            cord2.rectTransform.sizeDelta = new Vector2(4f, IconSize * 0.33f);
            cord2.rectTransform.anchoredPosition = new Vector2(4f, 0f);
            RawImage seal = CodexArt.Raw("Seal", body, CodexArt.Disc, new Color(0.16f, 0.52f, 0.32f, 1f));
            seal.rectTransform.anchorMin = seal.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            seal.rectTransform.sizeDelta = new Vector2(12f, 12f);
            seal.rectTransform.anchoredPosition = new Vector2(0f, -3f);
            return r;
        }

        private void BuildKey(bool gamepad)
        {
            keyGamepad = gamepad;
            for (int i = keyRow.childCount - 1; i >= 0; i--) Object.Destroy(keyRow.GetChild(i).gameObject);
            RectTransform cap = PapyrusKit.Keycap(gamepad ? "VIEW" : "TAB", keyRow, KeyHeight, 17);
            cap.anchorMin = cap.anchorMax = new Vector2(0.5f, 0.5f);
            cap.anchoredPosition = Vector2.zero;
            keyRow.sizeDelta = new Vector2(cap.sizeDelta.x, KeyHeight);
        }

        /// <summary>Centre of the icon in the canvas, relative to the canvas centre (for the book's fly animation).</summary>
        public Vector2 IconCenter(RectTransform canvas)
        {
            Vector2 size = canvas.rect.size;
            return new Vector2(size.x * 0.5f - Margin.x - IconSize * 0.5f, -size.y * 0.5f + Margin.y + KeyHeight + 6f + IconSize * 0.5f);
        }

        public bool Contains(Vector2 screen) =>
            group.alpha > 0.5f && RectTransformUtility.RectangleContainsScreenPoint(icon, screen, null);

        /// <summary>The book has just flown back in: a small settle bounce.</summary>
        public void Arrive() { punch = 0.6f; }

        public void Tick(bool visible, int unread)
        {
            float dt = Time.unscaledDeltaTime;
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, dt * (visible ? 2.5f : 8f));
            if (unread != shownCount)
            {
                if (unread > shownCount && shownCount >= 0) punch = 1f;
                shownCount = unread;
                badgeText.text = unread > 99 ? "99" : unread.ToString();
                badge.gameObject.SetActive(unread > 0);
            }
            punch = Mathf.MoveTowards(punch, 0f, dt * 1.6f);
            float s = 1f + 0.22f * Mathf.Sin(punch * Mathf.PI) * punch;
            icon.localScale = new Vector3(s, s, 1f);
            glowPhase += dt;
            float g = unread > 0 ? 0.16f + 0.10f * Mathf.Sin(glowPhase * 2.2f) : 0f;
            g += punch * 0.5f;
            Color c = glow.color;
            c.a = Mathf.MoveTowards(c.a, g, dt * 2f);
            glow.color = c;
            if (keyGamepad != CodexBook.GamepadMode) BuildKey(CodexBook.GamepadMode);

            if (newShownFor >= 0f && newBadge != null)
            {
                newShownFor += dt;
                float a = Mathf.Clamp01(newShownFor / 0.25f) * Mathf.Clamp01((NewBadgeSeconds - newShownFor) / 0.6f);
                newGroup.alpha = a * (visible ? 1f : 0f);
                float slide = 1f - Mathf.Clamp01(newShownFor / 0.35f);
                newBadge.anchoredPosition = new Vector2(-6f + 40f * slide * slide, -IconSize * 0.5f);
                if (newShownFor >= NewBadgeSeconds) { newShownFor = -1f; newBadge.gameObject.SetActive(false); }
            }
        }
    }

    /// <summary>Keycap chips for control hints ("TAB", "Q", "LB").</summary>
    public static class CodexChips
    {
        /// <summary>Adds a key chip with its left edge at x (vertically centred). Returns its width.</summary>
        public static float Key(RectTransform row, string label, float x, float height)
        {
            int size = Mathf.RoundToInt(height * 0.56f);
            float tw = CodexArt.MeasureWidth(label, CodexArt.Caps, size, FontStyle.Normal);
            float w = Mathf.Max(height * 1.15f, tw + height * 0.8f);
            RectTransform chip = CodexArt.Rect("Key_" + label, row);
            chip.anchorMin = chip.anchorMax = new Vector2(0f, 0.5f);
            chip.pivot = new Vector2(0f, 0.5f);
            chip.anchoredPosition = new Vector2(x, 0f);
            chip.sizeDelta = new Vector2(w, height);
            Image cap = CodexArt.SlicedImage("Cap", chip, CodexArt.Sliced("keycap", new Vector4(22, 22, 22, 22)), CodexArt.GoldDim, 22f / (height * 0.36f));
            CodexPages.UiStretch(cap.rectTransform);
            Text t = CodexArt.Label("Label", chip, label, CodexArt.Caps, size, CodexArt.Gold, TextAnchor.MiddleCenter);
            CodexPages.UiStretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(0f, 1f);
            return w;
        }
    }
}
