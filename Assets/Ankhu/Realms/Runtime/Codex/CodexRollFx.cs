using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Realms
{
    /// <summary>
    /// Writes a uGUI Text in by hand: quads appear one after another as <see cref="Reveal"/> grows, each fading
    /// in over about one character, glossy wet black first and drying to the text colour. Also records every glyph
    /// quad so the reed pen can follow the writing and strikes can be drawn through whole lines.
    /// Reveal &lt; 0 means fully shown (the default), so a Text with this effect looks normal until a scribe uses it.
    /// </summary>
    public sealed class CodexReveal : BaseMeshEffect
    {
        /// <summary>Number of glyph quads written so far (fractional). Negative = everything, dry.</summary>
        public float Reveal = -1f;
        /// <summary>How many quads behind the pen are still wet.</summary>
        public float WetSpan = 9f;
        /// <summary>0..1, how glossy the whole text still is (dries after the pen lifts).</summary>
        public float Gloss;
        public Color WetColor = new Color(0.05f, 0.03f, 0.02f, 1f);

        private readonly List<Rect> quads = new List<Rect>();
        public IReadOnlyList<Rect> Quads => quads;
        public int QuadCount => quads.Count;
        public bool HasMesh { get; private set; }

        private static readonly List<UIVertex> verts = new List<UIVertex>();

        public static CodexReveal Attach(Text t)
        {
            CodexReveal r = t.GetComponent<CodexReveal>();
            if (r == null) r = t.gameObject.AddComponent<CodexReveal>();
            return r;
        }

        public void Refresh()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            verts.Clear();
            vh.GetUIVertexStream(verts);
            quads.Clear();
            // The stream is 6 vertices per quad (two triangles).
            int count = verts.Count / 6;
            for (int q = 0; q < count; q++)
            {
                float xmin = float.MaxValue, ymin = float.MaxValue, xmax = float.MinValue, ymax = float.MinValue;
                for (int k = 0; k < 6; k++)
                {
                    Vector3 p = verts[q * 6 + k].position;
                    if (p.x < xmin) xmin = p.x;
                    if (p.x > xmax) xmax = p.x;
                    if (p.y < ymin) ymin = p.y;
                    if (p.y > ymax) ymax = p.y;
                }
                quads.Add(new Rect(xmin, ymin, xmax - xmin, ymax - ymin));
            }
            HasMesh = count > 0;

            if (Reveal >= 0f || Gloss > 0.001f)
            {
                for (int q = 0; q < count; q++)
                {
                    float a = Reveal < 0f ? 1f : Mathf.Clamp01(Reveal - q);
                    float wet = Reveal < 0f ? Gloss : Mathf.Max(Gloss, Mathf.Clamp01(1f - (Reveal - q) / Mathf.Max(1f, WetSpan)));
                    for (int k = 0; k < 6; k++)
                    {
                        UIVertex v = verts[q * 6 + k];
                        Color c = v.color;
                        Color target = Color.Lerp(c, new Color(WetColor.r, WetColor.g, WetColor.b, c.a), wet * 0.85f);
                        target.a = c.a * a;
                        v.color = target;
                        verts[q * 6 + k] = v;
                    }
                }
                vh.Clear();
                vh.AddUIVertexTriangleStream(verts);
            }
        }

        /// <summary>Local position (in the Text's rect space) just after quad i, where the reed tip sits.</summary>
        public Vector2 PenPoint(float reveal)
        {
            if (quads.Count == 0) return Vector2.zero;
            int i = Mathf.Clamp(Mathf.FloorToInt(reveal), 0, quads.Count - 1);
            Rect r = quads[i];
            float f = Mathf.Clamp01(reveal - i);
            return new Vector2(Mathf.Lerp(r.xMin, r.xMax, f), r.yMin + r.height * 0.25f);
        }

        /// <summary>Glyph runs grouped by line: x from, x to, y centre (local). Used for strike lines.</summary>
        public List<Vector3> LineSpans()
        {
            var spans = new List<Vector3>();
            if (quads.Count == 0) return spans;
            float lineH = 0f;
            foreach (Rect r in quads) lineH = Mathf.Max(lineH, r.height);
            float tol = Mathf.Max(4f, lineH * 0.45f);
            float x0 = quads[0].xMin, x1 = quads[0].xMax, ySum = quads[0].center.y;
            int n = 1;
            for (int i = 1; i < quads.Count; i++)
            {
                Rect r = quads[i];
                float yc = ySum / n;
                if (Mathf.Abs(r.center.y - yc) > tol)
                {
                    spans.Add(new Vector3(x0, x1, yc));
                    x0 = r.xMin; x1 = r.xMax; ySum = r.center.y; n = 1;
                    continue;
                }
                x0 = Mathf.Min(x0, r.xMin);
                x1 = Mathf.Max(x1, r.xMax);
                ySum += r.center.y;
                n++;
            }
            spans.Add(new Vector3(x0, x1, ySum / n));
            return spans;
        }
    }

    /// <summary>
    /// Red (or green) strike lines through every line of a Text, drawn by hand left to right, one line after
    /// another. Builds itself once the Text has a mesh. <see cref="Progress"/> runs 0..1 over all lines.
    /// </summary>
    public sealed class CodexStrike : MonoBehaviour
    {
        public Text Target;
        public Color Color = CodexArt.Ochre;
        public float Thickness = 2.2f;
        /// <summary>0 = nothing drawn, 1 = every line struck. Starts at 1 (already struck) unless a scribe sets 0.</summary>
        public float Progress = 1f;

        private readonly List<Image> lines = new List<Image>();
        private readonly List<float> widths = new List<float>();
        private bool built;

        public int LineCount => lines.Count;
        public bool Built => built;

        public static CodexStrike Attach(Text target, Color color)
        {
            var go = new GameObject("Strike", typeof(RectTransform));
            go.transform.SetParent(target.transform.parent, false);
            var s = go.AddComponent<CodexStrike>();
            s.Target = target;
            s.Color = color;
            RectTransform r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = Vector2.zero;
            CodexReveal.Attach(target);
            return s;
        }

        /// <summary>Right point of the line being struck now, in this strike's parent space (for the pen).</summary>
        public Vector2 PenPoint()
        {
            if (!built || lines.Count == 0) return Vector2.zero;
            float per = 1f / lines.Count;
            int i = Mathf.Clamp(Mathf.FloorToInt(Progress / per), 0, lines.Count - 1);
            RectTransform r = lines[i].rectTransform;
            return r.anchoredPosition + new Vector2(r.sizeDelta.x, 0f);
        }

        private void LateUpdate()
        {
            if (!built) TryBuild();
            if (!built) return;
            float per = 1f / Mathf.Max(1, lines.Count);
            for (int i = 0; i < lines.Count; i++)
            {
                float k = Mathf.Clamp01((Progress - i * per) / per);
                // A hand stroke: quick start, slower end.
                k = 1f - (1f - k) * (1f - k);
                RectTransform r = lines[i].rectTransform;
                r.sizeDelta = new Vector2(widths[i] * k, Thickness);
                lines[i].enabled = k > 0.001f;
            }
        }

        private void TryBuild()
        {
            if (Target == null) return;
            CodexReveal rev = Target.GetComponent<CodexReveal>();
            if (rev == null || !rev.HasMesh) return;
            RectTransform parent = (RectTransform)transform.parent;
            foreach (Vector3 span in rev.LineSpans())
            {
                Vector3 a = parent.InverseTransformPoint(Target.transform.TransformPoint(new Vector3(span.x - 3f, span.z, 0f)));
                Vector3 b = parent.InverseTransformPoint(Target.transform.TransformPoint(new Vector3(span.y + 3f, span.z, 0f)));
                Image img = CodexArt.Solid("StrikeLine", transform, Color);
                RectTransform r = img.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 0.5f);
                // This object sits at the parent's top left: convert from parent-centre space.
                Vector2 topLeft = new Vector2(parent.rect.xMin, parent.rect.yMax);
                r.anchoredPosition = new Vector2(a.x, a.y) - topLeft;
                // A scribe's line is never perfectly level.
                r.localEulerAngles = new Vector3(0f, 0f, Random.Range(-0.6f, 0.6f));
                lines.Add(img);
                widths.Add(Mathf.Max(4f, b.x - a.x));
            }
            built = lines.Count > 0;
        }
    }

    /// <summary>The reed pen that writes on the roll. A dark reed with a wet tip, gently bobbing as it writes.</summary>
    public sealed class CodexPen
    {
        public RectTransform Root;
        private RawImage tipGlow;
        private CanvasGroup group;
        private Vector2 pos;
        private float bob;

        public static CodexPen Build(RectTransform parent)
        {
            var p = new CodexPen();
            p.Root = CodexArt.Rect("ReedPen", parent);
            p.Root.anchorMin = p.Root.anchorMax = new Vector2(0.5f, 0.5f);
            p.Root.pivot = new Vector2(0.06f, 0.06f);
            p.Root.sizeDelta = new Vector2(30f, 190f);
            p.group = p.Root.gameObject.AddComponent<CanvasGroup>();
            p.group.blocksRaycasts = false;
            p.group.alpha = 0f;

            // Soft shadow of the reed on the papyrus, offset as if lit from the upper left.
            Image shadow = CodexArt.Solid("Shadow", p.Root, new Color(0.12f, 0.06f, 0.02f, 0.16f));
            RectTransform sr = shadow.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(0f, 0f);
            sr.pivot = new Vector2(0.5f, 0f);
            sr.sizeDelta = new Vector2(12f, 176f);
            sr.anchoredPosition = new Vector2(16f, -8f);
            sr.localEulerAngles = new Vector3(0f, 0f, -5f);

            // A solid reed: a rush stem cut to a chisel point, darker where it has drunk the ink, with growth nodes
            // and a light edge where the lamp catches it.
            Image shaft = CodexArt.Solid("Shaft", p.Root, new Color(0.66f, 0.52f, 0.30f, 1f));
            RectTransform shr = shaft.rectTransform;
            shr.anchorMin = shr.anchorMax = new Vector2(0f, 0f);
            shr.pivot = new Vector2(0.5f, 0f);
            shr.sizeDelta = new Vector2(11f, 168f);
            shr.anchoredPosition = new Vector2(4f, 18f);
            CodexGradient.Add(shaft, new Color(1.25f, 1.15f, 0.98f, 1f), new Color(0.62f, 0.55f, 0.45f, 1f), false);
            Image lit = CodexArt.Solid("Lit", p.Root, new Color(1f, 0.92f, 0.72f, 0.55f));
            RectTransform lr = lit.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0f, 0f);
            lr.pivot = new Vector2(0.5f, 0f);
            lr.sizeDelta = new Vector2(2f, 150f);
            lr.anchoredPosition = new Vector2(1.5f, 30f);
            for (int i = 0; i < 3; i++)
            {
                Image node = CodexArt.Solid("Node", p.Root, new Color(0.42f, 0.30f, 0.15f, 1f));
                RectTransform nr = node.rectTransform;
                nr.anchorMin = nr.anchorMax = new Vector2(0f, 0f);
                nr.pivot = new Vector2(0.5f, 0.5f);
                nr.sizeDelta = new Vector2(13f, 3f);
                nr.anchoredPosition = new Vector2(4f, 66f + i * 46f);
            }
            Image stain = CodexArt.Solid("InkStain", p.Root, new Color(0.16f, 0.10f, 0.06f, 1f));
            RectTransform st = stain.rectTransform;
            st.anchorMin = st.anchorMax = new Vector2(0f, 0f);
            st.pivot = new Vector2(0.5f, 0f);
            st.sizeDelta = new Vector2(10f, 22f);
            st.anchoredPosition = new Vector2(4f, 6f);
            CodexGradient.Add(stain, Color.white, new Color(1f, 1f, 1f, 0f), true);
            Image tip = CodexArt.Solid("Tip", p.Root, new Color(0.06f, 0.04f, 0.03f, 1f));
            RectTransform tr = tip.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 0f);
            tr.pivot = new Vector2(0.5f, 0f);
            tr.sizeDelta = new Vector2(6f, 12f);
            tr.anchoredPosition = new Vector2(4f, 0f);
            p.tipGlow = CodexArt.Raw("Wet", p.Root, UiKitDot(), new Color(0.05f, 0.03f, 0.02f, 0.35f));
            RectTransform gr = p.tipGlow.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0f, 0f);
            gr.pivot = new Vector2(0.5f, 0.5f);
            gr.sizeDelta = new Vector2(14f, 14f);
            gr.anchoredPosition = new Vector2(4f, 0f);
            p.Root.localEulerAngles = new Vector3(0f, 0f, -36f);
            return p;
        }

        private static Texture UiKitDot() => Ankhu.Opening.UiKit.SoftDot(32, 2f);

        /// <summary>Move the tip toward a point (in the pen's parent space). Snap on the first frame it shows.</summary>
        public void Follow(Vector2 target, float dt, bool snap = false)
        {
            bob += dt * 17f;
            pos = snap ? target : Vector2.Lerp(pos, target, 1f - Mathf.Exp(-dt * 26f));
            Root.anchoredPosition = pos + new Vector2(0f, Mathf.Abs(Mathf.Sin(bob)) * 3f);
            Root.localEulerAngles = new Vector3(0f, 0f, -36f + Mathf.Sin(bob * 0.5f) * 2.5f);
        }

        public float Alpha
        {
            get => group.alpha;
            set => group.alpha = Mathf.Clamp01(value);
        }
    }
}
