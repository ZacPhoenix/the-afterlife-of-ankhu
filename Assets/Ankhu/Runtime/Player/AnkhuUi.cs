using Ankhu.Integration;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Player
{
    /// <summary>Colours for feedback lines, one per command outcome.</summary>
    public static class FeedbackPalette
    {
        public static readonly Color Success = new Color(1f, 0.82f, 0.36f);
        public static readonly Color NoChange = new Color(0.86f, 0.81f, 0.70f);
        public static readonly Color NotUnderstood = new Color(1f, 1f, 1f);
        public static readonly Color InvalidTarget = new Color(1f, 0.72f, 0.52f);
        public static readonly Color Incomplete = new Color(1f, 0.78f, 0.58f);
        public static readonly Color Rejected = new Color(1f, 0.50f, 0.45f);
        public static readonly Color NotConnected = new Color(0.62f, 0.62f, 0.62f);

        public static Color For(CommandOutcome outcome)
        {
            switch (outcome)
            {
                case CommandOutcome.Success: return Success;
                case CommandOutcome.NoChange: return NoChange;
                case CommandOutcome.NotUnderstood: return NotUnderstood;
                case CommandOutcome.InvalidTarget: return InvalidTarget;
                case CommandOutcome.Incomplete: return Incomplete;
                case CommandOutcome.Rejected: return Rejected;
                case CommandOutcome.NotConnected: return NotConnected;
                default: return NotUnderstood;
            }
        }
    }

    /// <summary>Small uGUI construction helpers shared by the rig factory. Works in edit mode and play mode.</summary>
    public static class AnkhuUi
    {
        private static Font cachedFont;

        public static Font DefaultFont
        {
            get
            {
                if (cachedFont == null)
                {
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return cachedFont;
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // built in UI layer
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        public static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment, Color color, bool outline)
        {
            RectTransform rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = string.Empty;
            if (outline)
            {
                var effect = rect.gameObject.AddComponent<Outline>();
                effect.effectColor = new Color(0f, 0f, 0f, 0.85f);
                effect.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return text;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Converts a world point to the local space of an overlay canvas rect (pivot centred).</summary>
        public static bool WorldToCanvas(Camera camera, RectTransform canvasRect, Vector3 world, out Vector2 local)
        {
            local = Vector2.zero;
            if (camera == null || canvasRect == null)
            {
                return false;
            }
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                return false;
            }
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local);
        }

        /// <summary>Unscaled preferred width of a string rendered with the settings of a Text.</summary>
        public static float MeasureWidth(Text text, string value)
        {
            TextGenerationSettings settings = text.GetGenerationSettings(Vector2.zero);
            return text.cachedTextGeneratorForLayout.GetPreferredWidth(value, settings) / text.pixelsPerUnit;
        }
    }
}
