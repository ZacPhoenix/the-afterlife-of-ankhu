using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// The look of <see cref="RealmSpeechBubble"/>. Two skins ship:
    /// <see cref="Painted"/> (default): a torn sheet of papyrus with the red and black ruled border scribes drew round
    /// a vignette, words written in carbon ink, refusals in rubric red, hieroglyphs that appear in ink and gild
    /// before they fly. <see cref="Obsidian"/>: the original dark lapis panel with a gold hairline.
    /// Pick one per bubble with <see cref="RealmSpeechBubble.Skin"/>, or for every new bubble with <see cref="Default"/>.
    /// </summary>
    public sealed class BubbleSkin
    {
        public string Name;

        // Panel pieces. Sprites are fetched lazily so a skin can be declared before Resources load.
        public System.Func<Sprite> PanelSprite;
        public System.Func<Sprite> BorderSprite;
        public System.Func<Sprite> TailFillSprite;
        public System.Func<Sprite> TailEdgeSprite;
        public System.Func<Sprite> CaretSprite;
        /// <summary>How many sprite pixels make one canvas unit at the corners (bigger means smaller corners).</summary>
        public float CornerPixelsPerUnit;
        public Color PanelColor;
        public Color BorderColor;
        /// <summary>Added to the border alpha as a slow pulse (0 for a still border).</summary>
        public float BorderPulse;
        public Color ShadowColor;
        public Color AuraColor;
        public Color TailEdgeColor;
        public Color TailFillColor;
        public bool Glint;
        public float SheenAlpha;
        public Color RuleColor;
        /// <summary>Tail width and height in canvas units, and how far it reaches up into the sheet.</summary>
        public Vector2 TailSize;
        public float TailOverlap;

        // Words.
        public Color Ink;
        /// <summary>Colour a freshly typed letter starts at before it settles to <see cref="Ink"/>.</summary>
        public Color InkFresh;
        /// <summary>Colour the line breathes toward while the rules weigh the words.</summary>
        public Color Weighing;
        /// <summary>Colour the spoken words take as they become power.</summary>
        public Color Empowered;
        public Color Caret;
        public float CaretWidth;
        public Color Placeholder;
        public Color Reply;
        public Color Hint;
        public Color HintKeys;
        /// <summary>Colour refused words turn before they crumble.</summary>
        public Color Refused;
        /// <summary>Colour of the grains a refused word crumbles into.</summary>
        public Color Crumble;

        // Hieroglyphs in the bubble.
        /// <summary>True: signs appear in <see cref="Ink"/> as if written, then gild when the shimmer starts.</summary>
        public bool InkThenGild;
        /// <summary>Gold used for signs inside the bubble. Deeper than the flying gold so it reads on a light sheet.</summary>
        public Color SignGold;

        public static BubbleSkin Obsidian { get; } = new BubbleSkin
        {
            Name = "Obsidian",
            PanelSprite = () => GlyphAtlas.UiPanel,
            BorderSprite = () => GlyphAtlas.UiBorder,
            TailFillSprite = () => GlyphAtlas.UiTail,
            TailEdgeSprite = () => GlyphAtlas.UiTail,
            CaretSprite = () => null,
            CornerPixelsPerUnit = 28f / 15f,
            PanelColor = MagicPalette.Obsidian,
            BorderColor = new Color(MagicPalette.Gold.r, MagicPalette.Gold.g, MagicPalette.Gold.b, 0.70f),
            BorderPulse = 0.08f,
            ShadowColor = new Color(0f, 0f, 0f, 0.55f),
            AuraColor = new Color(MagicPalette.LapisLight.r, MagicPalette.LapisLight.g, MagicPalette.LapisLight.b, 0.20f),
            TailEdgeColor = new Color(MagicPalette.Gold.r, MagicPalette.Gold.g, MagicPalette.Gold.b, 0.85f),
            TailFillColor = MagicPalette.Obsidian,
            Glint = true,
            SheenAlpha = 0.075f,
            RuleColor = MagicPalette.Gold,
            TailSize = new Vector2(26f, 17f),
            TailOverlap = 0f,
            Refused = MagicPalette.Sand,
            Ink = MagicPalette.Ink,
            InkFresh = MagicPalette.GoldPale,
            Weighing = MagicPalette.GoldPale,
            Empowered = MagicPalette.Gold,
            Caret = MagicPalette.Gold,
            CaretWidth = 2.4f,
            Placeholder = new Color(MagicPalette.Quiet.r, MagicPalette.Quiet.g, MagicPalette.Quiet.b, 0.5f),
            Reply = MagicPalette.Quiet,
            Hint = MagicPalette.Quiet,
            HintKeys = MagicPalette.Gold,
            Crumble = MagicPalette.Sand,
            InkThenGild = false,
            SignGold = MagicPalette.Gold,
        };

        public static BubbleSkin Painted { get; } = new BubbleSkin
        {
            Name = "Painted",
            PanelSprite = () => GlyphAtlas.UiPapyrus,
            BorderSprite = () => GlyphAtlas.UiPapyrusFrame,
            TailFillSprite = () => GlyphAtlas.UiPapyrusTail,
            TailEdgeSprite = () => GlyphAtlas.UiPapyrusTailInk,
            CaretSprite = () => GlyphAtlas.UiInkCaret,
            // 72 px corners shown at 30 units: the torn edge and both rules stay crisp without crowding the words.
            CornerPixelsPerUnit = 72f / 30f,
            PanelColor = Color.white,
            BorderColor = new Color(1f, 1f, 1f, 0.95f),
            BorderPulse = 0f,
            ShadowColor = new Color(0.10f, 0.06f, 0.02f, 0.50f),
            AuraColor = new Color(1f, 0.72f, 0.32f, 0.10f),
            TailEdgeColor = Color.white,
            TailFillColor = Color.white,
            Glint = false,
            SheenAlpha = 0.06f,
            // The painted frame already rules the sheet, so the gold hairline under the top edge is left out.
            RuleColor = new Color(0.59f, 0.18f, 0.10f, 0f),
            TailSize = new Vector2(30f, 22f),
            TailOverlap = 8f,
            Refused = new Color(0.62f, 0.18f, 0.10f, 1f),
            Ink = new Color(0.125f, 0.086f, 0.055f, 1f),
            InkFresh = new Color(0.36f, 0.22f, 0.10f, 1f),
            Weighing = new Color(0.62f, 0.40f, 0.10f, 1f),
            Empowered = new Color(0.70f, 0.45f, 0.07f, 1f),
            Caret = new Color(0.125f, 0.086f, 0.055f, 1f),
            CaretWidth = 3.4f,
            Placeholder = new Color(0.36f, 0.27f, 0.17f, 0.55f),
            Reply = new Color(0.59f, 0.16f, 0.09f, 1f),
            Hint = new Color(0.33f, 0.25f, 0.16f, 1f),
            HintKeys = new Color(0.59f, 0.18f, 0.10f, 1f),
            Crumble = new Color(0.20f, 0.14f, 0.09f, 1f),
            InkThenGild = true,
            SignGold = new Color(0.78f, 0.52f, 0.10f, 1f),
        };

        /// <summary>The skin new bubbles use. Painted when its textures are present, otherwise Obsidian.</summary>
        public static BubbleSkin Default
        {
            get => defaultSkin ?? (GlyphAtlas.HasResource("ui_papyrus") ? Painted : Obsidian);
            set => defaultSkin = value;
        }

        private static BubbleSkin defaultSkin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => defaultSkin = null;
    }
}
