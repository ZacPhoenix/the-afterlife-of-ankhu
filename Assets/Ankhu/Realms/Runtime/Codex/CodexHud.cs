using Ankhu.Opening;
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
        private const float IconSize = 92f;
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

        public static CodexHud Build(RectTransform canvas)
        {
            var h = new CodexHud();
            h.root = CodexArt.Rect("CodexHud", canvas);
            h.root.anchorMin = h.root.anchorMax = h.root.pivot = new Vector2(1f, 0f);
            h.root.anchoredPosition = new Vector2(-Margin.x, Margin.y);
            h.root.sizeDelta = new Vector2(IconSize, IconSize + 26f);
            h.group = h.root.gameObject.AddComponent<CanvasGroup>();
            h.group.blocksRaycasts = false;
            h.group.interactable = false;

            h.glow = CodexArt.Raw("Glow", h.root, UiKit.SoftDot(64, 1.8f), new Color(1f, 0.75f, 0.35f, 0f));
            RectTransform gr = h.glow.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 1f);
            gr.pivot = new Vector2(0.5f, 0.5f);
            gr.anchoredPosition = new Vector2(0f, -IconSize * 0.5f);
            gr.sizeDelta = new Vector2(IconSize * 2.1f, IconSize * 2.1f);

            RawImage img = CodexArt.Raw("Icon", h.root, CodexArt.Tex("codex_icon"), Color.white);
            h.icon = img.rectTransform;
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
            return h;
        }

        private bool keyGamepad;

        private void BuildKey(bool gamepad)
        {
            keyGamepad = gamepad;
            for (int i = keyRow.childCount - 1; i >= 0; i--) Object.Destroy(keyRow.GetChild(i).gameObject);
            float w = CodexChips.Key(keyRow, gamepad ? "VIEW" : "TAB", 0f, 22f);
            keyRow.sizeDelta = new Vector2(w, 22f);
        }

        /// <summary>Centre of the icon in the canvas, relative to the canvas centre (for the book's fly animation).</summary>
        public Vector2 IconCenter(RectTransform canvas)
        {
            Vector2 size = canvas.rect.size;
            return new Vector2(size.x * 0.5f - Margin.x - IconSize * 0.5f, -size.y * 0.5f + Margin.y + 26f + IconSize * 0.5f);
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
