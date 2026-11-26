using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Opening
{
    /// <summary>Small helpers that build uGUI objects in code, so the opening scenes need no hand-made prefabs.</summary>
    public static class UiKit
    {
        private static Font serif;
        private static Font caps;

        /// <summary>Body serif. Cormorant Garamond is the design font. Until it is added to the project,
        /// the Windows serif Palatino Linotype (then Book Antiqua, then Georgia) stands in.</summary>
        public static Font Serif
        {
            get
            {
                if (serif == null) serif = Font.CreateDynamicFontFromOSFont(new[] { "Cormorant Garamond", "Palatino Linotype", "Book Antiqua", "Georgia" }, 48);
                return serif;
            }
        }

        /// <summary>Caps serif for menu items. Cinzel is the design font, Palatino Linotype in capitals stands in.</summary>
        public static Font Caps
        {
            get
            {
                if (caps == null) caps = Font.CreateDynamicFontFromOSFont(new[] { "Cinzel", "Palatino Linotype", "Book Antiqua", "Georgia" }, 48);
                return caps;
            }
        }

        public static readonly Color Gold = new Color(0.95f, 0.80f, 0.48f, 1f);
        public static readonly Color GoldDim = new Color(0.94f, 0.88f, 0.76f, 1f);
        public static readonly Color Parchment = new Color(0.95f, 0.91f, 0.83f, 1f);

        public static Canvas Canvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920f, 1080f);
            s.matchWidthOrHeight = 1f;
            return c;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }

        public static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = anchor;
            r.anchorMax = anchor;
            r.pivot = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        public static Image Solid(string name, Transform parent, Color color)
        {
            RectTransform r = Rect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static RawImage Raw(string name, Transform parent, Texture tex)
        {
            RectTransform r = Rect(name, parent);
            var img = r.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, Font font, int size, Color color, TextAnchor align)
        {
            RectTransform r = Rect(name, parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Title shade: dark at the bottom (where the menu sits), a light lift under the logo, soft side vignette.</summary>
        public static Texture2D TitleShade(int size = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "TitleShade" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (size - 1f);
                float bottom = 0.82f * Mathf.Pow(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 0.0f, v)), 1.3f);
                float top = 0.30f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1f, v));
                for (int x = 0; x < size; x++)
                {
                    float u = Mathf.Abs(x / (size - 1f) - 0.5f) * 2f;
                    float side = 0.38f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, u));
                    float a = 1f - (1f - bottom) * (1f - top) * (1f - side);
                    px[y * size + x] = new Color32(6, 3, 2, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>A soft round dot for dust motes and blob shadows. Alpha falls off as (1 - r)^power.</summary>
        public static Texture2D SoftDot(int size = 64, float power = 2f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftDot" };
            var px = new Color32[size * size];
            float h = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - h) * (x - h) + (y - h) * (y - h)) / h;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), power);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
