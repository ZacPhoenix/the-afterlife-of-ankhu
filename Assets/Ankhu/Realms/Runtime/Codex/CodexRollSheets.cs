using System.Collections.Generic;
using System.Linq;
using Ankhu.Realms.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Realms
{
    /// <summary>One sheet (kollema) glued into Ankhu's roll, built as uGUI. Holds the parts a scribe animates.</summary>
    public sealed class CodexSheetView : MonoBehaviour
    {
        public string Token;
        public CodexEntry Entry;
        public string Scene;
        public float Width;
        public RectTransform Root;
        public CanvasGroup Content;
        public CodexArt.GlyphGroup Heading;
        public Text Title;
        public Text Translit;
        public Text Effect;
        public Text Body;
        public RawImage Bleed;
        public RawImage UnreadDot;
        public bool Faint;
        public readonly List<RevisionView> Revisions = new List<RevisionView>();
        public readonly List<QuestionView> Questions = new List<QuestionView>();
        public readonly List<Text> Notes = new List<Text>();
        public readonly List<string> ReadIds = new List<string>();

        public sealed class RevisionView
        {
            public CodexRevision Revision;
            public Text Old;
            public CodexStrike Strike;
            public Text New;
            public Text Note;
        }

        public sealed class QuestionView
        {
            public CodexQuestion Question;
            public Text Text;
            public CodexStrike Strike;
            public Text Answer;
        }
    }

    /// <summary>
    /// Builds the sheets of the roll. Layout follows a Book of the Dead papyrus such as Ani's: a vignette band on
    /// top, a double red rule, a red rubric, then the text in black. English body text, real hieroglyphs as headings.
    /// Sizes are canvas units at the 1080p reference.
    /// </summary>
    public static class CodexRollSheets
    {
        public const float SheetW = 500f;
        public const float GroupW = 380f;
        public const float SheetH = 740f;
        public const float Pad = 44f;
        public const float BandTop = 36f;
        public const float BandH = 150f;

        public static readonly Color Rubric = CodexArt.Ochre;
        public static readonly Color AnswerGreen = new Color(0.20f, 0.40f, 0.24f, 1f);
        public static readonly Color AnpuInk = new Color(0.10f, 0.16f, 0.38f, 1f);

        public static string SectionLabel(CodexSection s)
        {
            switch (s)
            {
                case CodexSection.Craft: return "CRAFT";
                case CodexSection.Heart: return "HEART";
                case CodexSection.Names: return "NAMES";
                default: return "ACCOUNT";
            }
        }

        /// <summary>The word the parser accepts for an entry, if it has one the player can tap into the bubble.</summary>
        public static string SpeakableWord(CodexEntry e)
        {
            if (e == null) return null;
            if (e.Kind == CodexKind.Word && e.Id.StartsWith("word.")) return e.Id.Substring(5);
            if (e.Kind == CodexKind.Name && !string.IsNullOrEmpty(e.Title)) return e.Title.ToLowerInvariant();
            return null;
        }

        public static float WidthOf(string token) => token.StartsWith("?") ? GroupW : SheetW;

        // ------------------------------------------------------------------ entry sheet

        public static CodexSheetView BuildEntry(CodexEntry e, RectTransform parent, int index, System.Action<string> onWord)
        {
            CodexSheetView v = NewSheet("Sheet_" + e.Id, parent, SheetW, index, out RectTransform content);
            v.Token = e.Id;
            v.Entry = e;
            v.ReadIds.Add(e.Id);
            string until = CodexRollCatalog.FaintUntilId(e.Id);
            v.Faint = until != null && !CodexStore.Has(until);

            float w = SheetW - Pad * 2f;
            BuildBand(v, content, e, w);

            float y = BandTop + BandH + 12f;
            DoubleRule(content, y, w);
            y += 16f;

            CodexSection section = CodexRollCatalog.Section(e);
            string kind = v.Faint ? "A FLICKER" : CodexArt.KindLabel(e.Kind);
            string rubric = kind + "   ·   " + SectionLabel(section) + (string.IsNullOrEmpty(e.Realm) ? "" : "   ·   " + e.Realm.ToUpperInvariant());
            Text rub = CodexArt.Label("Rubric", content, rubric, CodexArt.Caps, 14, CodexArt.OchreSoft, TextAnchor.UpperLeft);
            rub.horizontalOverflow = HorizontalWrapMode.Overflow;
            CodexArt.TopLeft(rub.rectTransform, Pad, y, w, 20f);
            y += 26f;

            string titleText = (e.Title ?? string.Empty).ToUpperInvariant();
            // The title keeps one line when it fits: room is only kept back for a transliteration when there is one.
            float titleW = string.IsNullOrEmpty(e.Transliteration) ? w : w - 120f;
            int titleSize = CodexArt.MeasureWidth(titleText, CodexArt.Caps, 30, FontStyle.Normal) <= titleW ? 30 : 26;
            v.Title = CodexArt.Label("Title", content, titleText, CodexArt.Caps, titleSize, CodexArt.Ink, TextAnchor.UpperLeft);
            float th = Mathf.Max(36f, CodexArt.MeasureHeight(titleText, CodexArt.Caps, titleSize, FontStyle.Normal, 1f, titleW) + 4f);
            CodexArt.TopLeft(v.Title.rectTransform, Pad, y, titleW + 8f, th);
            CodexReveal.Attach(v.Title);
            if (!string.IsNullOrEmpty(e.Transliteration))
            {
                v.Translit = CodexArt.Label("Translit", content, e.Transliteration, CodexArt.Body, 22, Rubric, TextAnchor.UpperRight, FontStyle.Italic);
                CodexArt.TopLeft(v.Translit.rectTransform, Pad + w - 140f, y + 4f, 140f, 30f);
                CodexReveal.Attach(v.Translit);
            }
            string word = SpeakableWord(e);
            if (word != null && onWord != null && !v.Faint) WordButton(content, Pad, y - 4f, Mathf.Min(titleW, CodexArt.MeasureWidth(titleText, CodexArt.Caps, titleSize, FontStyle.Normal) + 34f), th + 8f, word, onWord);
            y += th + 10f;

            if (e.Kind == CodexKind.Word && !string.IsNullOrEmpty(e.Effect))
            {
                float eh = CodexArt.MeasureHeight(e.Effect, CodexArt.Body, 20, FontStyle.Normal, 1.0f, w) + 6f;
                v.Effect = CodexArt.Label("Effect", content, e.Effect, CodexArt.Body, 20, CodexArt.InkSoft, TextAnchor.UpperLeft, FontStyle.Normal, 1.0f);
                CodexArt.TopLeft(v.Effect.rectTransform, Pad, y, w, eh);
                CodexReveal.Attach(v.Effect);
                y += eh + 12f;
                Image hair = CodexArt.Solid("Hair", content, new Color(CodexArt.Ink.r, CodexArt.Ink.g, CodexArt.Ink.b, 0.18f));
                CodexArt.TopLeft(hair.rectTransform, Pad, y - 6f, w * 0.4f, 1f);
            }

            // Body. A revised entry shows the struck line, then the true one beneath.
            List<CodexRevision> revs = CodexStore.Journal.RevisionsOf(e.Id).ToList();
            string body = e.Body ?? string.Empty;
            float room = SheetH - 70f - y;
            int size = FitSize(body, revs, w, room);

            CodexRevision whole = revs.FirstOrDefault(r => string.IsNullOrEmpty(r.OldText) || !body.Contains(r.OldText));
            string keep = body;
            if (whole == null)
            {
                foreach (CodexRevision r in revs) keep = keep.Replace(r.OldText, string.Empty).TrimEnd();
            }

            if (whole == null && keep.Length > 0)
            {
                v.Body = BodyLabel(content, "Body", keep, size, Pad, y, w, v.Faint ? CodexArt.InkSoft : CodexArt.Ink);
                y += Measure(keep, size, w) + 10f;
            }
            foreach (CodexRevision r in revs)
            {
                string old = whole == r ? body : r.OldText;
                var rv = new CodexSheetView.RevisionView { Revision = r };
                rv.Old = BodyLabel(content, "Old", old, size, Pad, y, w, new Color(CodexArt.Ink.r, CodexArt.Ink.g, CodexArt.Ink.b, 0.78f));
                rv.Strike = CodexStrike.Attach(rv.Old, Rubric);
                y += Measure(old, size, w) + 12f;
                rv.New = BodyLabel(content, "New", r.NewText, size, Pad, y, w, CodexArt.Ink);
                y += Measure(r.NewText, size, w) + 6f;
                if (!string.IsNullOrEmpty(r.Realm))
                {
                    rv.Note = CodexArt.Label("Rewritten", content, "WRITTEN AGAIN IN " + r.Realm.ToUpperInvariant(), CodexArt.Caps, 12, CodexArt.OchreSoft, TextAnchor.UpperRight);
                    CodexArt.TopLeft(rv.Note.rectTransform, Pad, y, w, 18f);
                    y += 22f;
                }
                // A small red mark in the margin: this sheet was revised.
                RawImage mark = CodexArt.Raw("RevisedMark", content, CodexArt.Disc, Rubric);
                CodexArt.TopLeft(mark.rectTransform, Pad - 22f, y - Measure(r.NewText, size, w) - 14f, 8f, 8f);
                v.Revisions.Add(rv);
                v.ReadIds.Add(r.Id);
            }
            if (v.Body == null && v.Revisions.Count == 0 && body.Length > 0)
            {
                v.Body = BodyLabel(content, "Body", body, size, Pad, y, w, CodexArt.Ink);
            }

            if (e.Addenda != null)
            {
                foreach (string a in e.Addenda)
                {
                    if (string.IsNullOrWhiteSpace(a)) continue;
                    float ah = Measure(a, size - 2, w - 24f);
                    if (y + ah > SheetH - 70f) break;
                    BodyLabel(content, "Addendum", a, size - 2, Pad + 24f, y, w - 24f, CodexArt.InkSoft);
                    y += ah + 6f;
                }
            }

            v.Bleed = CodexArt.Raw("InkBleed", content, CodexArt.Tex("ink_bleed"), new Color(0f, 0f, 0f, 0f));
            CodexArt.TopLeft(v.Bleed.rectTransform, Pad, BandTop + BandH, w, 260f);
            v.Bleed.enabled = false;

            if (v.Faint) v.Content.alpha = 0.5f;
            Folio(v.Root, index, e.Realm);
            AddUnreadDot(v);
            return v;
        }

        private static int FitSize(string body, List<CodexRevision> revs, float w, float room)
        {
            for (int size = 23; size > 16; size--)
            {
                float h = Measure(body, size, w) + 10f;
                foreach (CodexRevision r in revs) h += Measure(r.NewText, size, w) + 40f;
                if (h <= room) return size;
            }
            return 16;
        }

        private static float Measure(string text, int size, float w) =>
            Mathf.Ceil(CodexArt.MeasureHeight(text, CodexArt.Body, size, FontStyle.Italic, 1.05f, w) * 1.03f) + 2f;

        private static Text BodyLabel(RectTransform parent, string name, string text, int size, float x, float y, float w, Color color)
        {
            Text t = CodexArt.Label(name, parent, text, CodexArt.Body, size, color, TextAnchor.UpperLeft, FontStyle.Italic, 1.05f);
            t.supportRichText = false;
            CodexArt.TopLeft(t.rectTransform, x, y, w, Measure(text, size, w) + 6f);
            CodexReveal.Attach(t);
            return t;
        }

        // ------------------------------------------------------------------ margin group sheet

        public static CodexSheetView BuildGroup(string scene, RectTransform parent, int index)
        {
            CodexSheetView v = NewSheet("Margin_" + scene, parent, GroupW, index, out RectTransform content);
            v.Token = "?" + scene;
            v.Scene = scene;
            float w = GroupW - Pad * 2f;
            float y = BandTop + 10f;

            Text head = CodexArt.Label("Head", content, "IN THE MARGIN", CodexArt.Caps, 15, Rubric, TextAnchor.UpperCenter);
            CodexArt.TopLeft(head.rectTransform, Pad, y, w, 22f);
            y += 26f;
            Text place = CodexArt.Label("Place", content, string.IsNullOrEmpty(scene) ? "" : scene.ToUpperInvariant(), CodexArt.Caps, 22, CodexArt.Ink, TextAnchor.UpperCenter);
            CodexArt.TopLeft(place.rectTransform, Pad, y, w, 30f);
            y += 40f;
            DoubleRule(content, y, w);
            y += 22f;

            CodexJournal j = CodexStore.Journal;
            foreach (CodexQuestion q in CodexRollCatalog.QuestionsFor(scene))
            {
                if (!j.WasAsked(q.Id)) continue;
                var qv = new CodexSheetView.QuestionView { Question = q };
                float qh = MeasureQ(q.Text, w - 18f);
                RawImage bullet = CodexArt.Raw("Bullet", content, CodexArt.Disc, Rubric);
                CodexArt.TopLeft(bullet.rectTransform, Pad, y + 11f, 7f, 7f);
                qv.Text = CodexArt.Label("Question", content, q.Text, CodexArt.Body, 22, Rubric, TextAnchor.UpperLeft, FontStyle.Italic, 1.05f);
                qv.Text.supportRichText = false;
                CodexArt.TopLeft(qv.Text.rectTransform, Pad + 18f, y, w - 18f, qh + 6f);
                CodexReveal.Attach(qv.Text);
                y += qh + 6f;
                if (j.IsAnswered(q.Id))
                {
                    qv.Strike = CodexStrike.Attach(qv.Text, AnswerGreen);
                    qv.Text.color = new Color(AnswerGreen.r, AnswerGreen.g, AnswerGreen.b, 0.85f);
                    if (!string.IsNullOrEmpty(q.Answer))
                    {
                        float ah = MeasureQ(q.Answer, w - 18f);
                        qv.Answer = CodexArt.Label("Answer", content, q.Answer, CodexArt.Body, 22, CodexArt.Ink, TextAnchor.UpperLeft, FontStyle.Italic, 1.05f);
                        qv.Answer.supportRichText = false;
                        CodexArt.TopLeft(qv.Answer.rectTransform, Pad + 18f, y, w - 18f, ah + 6f);
                        CodexReveal.Attach(qv.Answer);
                        y += ah + 6f;
                    }
                    v.ReadIds.Add(q.Id);
                }
                y += 14f;
                v.Questions.Add(qv);
            }

            foreach (CodexNoteRecord n in j.Notes)
            {
                if (n.Scene != scene) continue;
                bool anpu = n.Hand == CodexHand.Anpu;
                float nh = CodexArt.MeasureHeight(n.Text, CodexArt.Body, 21, anpu ? FontStyle.Normal : FontStyle.Italic, 1.05f, w - 26f) + 8f;
                if (y + nh > SheetH - 70f) break;
                if (anpu && GlyphAtlas.Has("E16"))
                {
                    RectTransform jackal = CodexArt.TopLeft(CodexArt.Rect("Jackal", content), Pad - 4f, y, 24f, 24f);
                    CodexArt.Glyphs(jackal, "E16", 20f, AnpuInk, 24f);
                }
                Text t = CodexArt.Label("Note", content, n.Text, CodexArt.Body, 21, anpu ? AnpuInk : CodexArt.InkSoft, TextAnchor.UpperLeft,
                    anpu ? FontStyle.Normal : FontStyle.Italic, 1.05f);
                t.supportRichText = false;
                CodexArt.TopLeft(t.rectTransform, Pad + 26f, y, w - 26f, nh);
                CodexReveal.Attach(t);
                v.Notes.Add(t);
                y += nh + 10f;
            }

            Folio(v.Root, index, null);
            return v;
        }

        private static float MeasureQ(string text, float w) =>
            Mathf.Ceil(CodexArt.MeasureHeight(text, CodexArt.Body, 22, FontStyle.Italic, 1.05f, w) * 1.03f) + 2f;

        // ------------------------------------------------------------------ shared furniture

        private static CodexSheetView NewSheet(string name, RectTransform parent, float width, int index, out RectTransform content)
        {
            RectTransform root = CodexArt.Rect(name, parent);
            root.anchorMin = root.anchorMax = new Vector2(0f, 0.5f);
            root.pivot = new Vector2(0f, 0.5f);
            root.sizeDelta = new Vector2(width, SheetH);
            CodexSheetView v = root.gameObject.AddComponent<CodexSheetView>();
            v.Root = root;
            v.Width = width;

            // Papyrus: each sheet a slightly different tone and fibre direction, like a real roll.
            RawImage paper = CodexArt.Raw("Papyrus", root, CodexArt.Tex("papyrus_page"), Color.white);
            CodexPages.UiStretch(paper.rectTransform);
            float h = Hash(index);
            // Sample only the inner fibre of the page texture: its outer band is a torn edge and a flat margin.
            float u0 = 0.14f + h * 0.18f;
            paper.uvRect = new UnityEngine.Rect(u0, 0.16f + (index % 2) * 0.06f, width / SheetW * 0.44f, 0.6f);
            float tone = 0.94f + h * 0.08f;
            paper.color = CodexArt.HasTex("papyrus_page") ? new Color(tone, tone * 0.985f, tone * 0.95f, 1f) : new Color(0.90f * tone, 0.82f * tone, 0.63f * tone, 1f);

            // Top and bottom edges darken a little where hands held the roll.
            Image topEdge = CodexArt.Solid("EdgeTop", root, new Color(0.35f, 0.22f, 0.08f, 0.22f));
            CodexArt.TopLeft(topEdge.rectTransform, 0f, 0f, width, 26f);
            CodexGradient.Add(topEdge, Color.white, new Color(1, 1, 1, 0), true);
            Image botEdge = CodexArt.Solid("EdgeBottom", root, new Color(0.35f, 0.22f, 0.08f, 0.24f));
            CodexArt.TopLeft(botEdge.rectTransform, 0f, SheetH - 30f, width, 30f);
            CodexGradient.Add(botEdge, new Color(1, 1, 1, 0), Color.white, true);

            // The glued join on the left: a thin overlap ridge.
            Image join = CodexArt.Solid("Join", root, new Color(0.30f, 0.18f, 0.06f, 0.30f));
            CodexArt.TopLeft(join.rectTransform, 0f, 0f, 2f, SheetH);
            Image ridge = CodexArt.Solid("Ridge", root, new Color(1f, 0.95f, 0.80f, 0.25f));
            CodexArt.TopLeft(ridge.rectTransform, 2f, 0f, 6f, SheetH);
            CodexGradient.Add(ridge, Color.white, new Color(1, 1, 1, 0), false);

            content = CodexArt.Rect("Content", root);
            CodexPages.UiStretch(content);
            v.Content = content.gameObject.AddComponent<CanvasGroup>();
            v.Content.interactable = true;
            v.Content.blocksRaycasts = true;
            return v;
        }

        private static void BuildBand(CodexSheetView v, RectTransform content, CodexEntry e, float w)
        {
            RectTransform band = CodexArt.TopLeft(CodexArt.Rect("Vignette", content), Pad, BandTop, w, BandH);
            Texture2D painted = e.Kind == CodexKind.Memory ? Resources.Load<Texture2D>("RealmMemories/" + e.Id) : null;
            if (painted != null && !v.Faint)
            {
                RawImage img = CodexArt.Raw("Painting", band, painted, Color.white);
                CodexPages.UiStretch(img.rectTransform);
                float ta = painted.width / (float)Mathf.Max(1, painted.height);
                float fa = w / BandH;
                img.uvRect = ta > fa ? new UnityEngine.Rect((1f - fa / ta) / 2f, 0f, fa / ta, 1f) : new UnityEngine.Rect(0f, (1f - ta / fa) / 2f, 1f, ta / fa);
                // Painted onto papyrus: a warm wash and a soft inner edge.
                Image wash = CodexArt.Solid("Wash", band, new Color(0.62f, 0.44f, 0.20f, 0.16f));
                CodexPages.UiStretch(wash.rectTransform);
                Image frame = CodexArt.Solid("FrameTop", band, Rubric);
                CodexArt.TopLeft(frame.rectTransform, 0f, -4f, w, 2f);
                return;
            }
            if (CodexLayout.HasSigns(e))
            {
                v.Heading = CodexPages.AddGoldGlyphs(band, e.Signs, 92f, w * 0.86f, false);
                return;
            }
            RawImage reed = CodexArt.Raw("Watermark", band, CodexArt.Tex("icon_reed"), new Color(CodexArt.Ink.r, CodexArt.Ink.g, CodexArt.Ink.b, 0.08f));
            CodexArt.Centered(reed.rectTransform, Vector2.zero, new Vector2(110f, 110f));
        }

        private static void DoubleRule(RectTransform content, float y, float w)
        {
            Image a = CodexArt.Solid("Rule", content, new Color(Rubric.r, Rubric.g, Rubric.b, 0.85f));
            CodexArt.TopLeft(a.rectTransform, Pad, y, w, 2f);
            Image b = CodexArt.Solid("Rule2", content, new Color(Rubric.r, Rubric.g, Rubric.b, 0.55f));
            CodexArt.TopLeft(b.rectTransform, Pad, y + 5f, w, 1f);
        }

        private static void Folio(RectTransform root, int index, string realm)
        {
            RectTransform folio = CodexArt.Rect("Folio", root);
            folio.anchorMin = folio.anchorMax = new Vector2(0.5f, 0f);
            folio.pivot = new Vector2(0.5f, 0f);
            CodexArt.Numeral(folio, index + 1, 20f, CodexArt.InkSoft, out float nw);
            folio.sizeDelta = new Vector2(nw, 20f);
            folio.anchoredPosition = new Vector2(0f, 26f);
            for (int i = 0; i < folio.childCount; i++)
            {
                RectTransform c = (RectTransform)folio.GetChild(i);
                c.anchorMin = c.anchorMax = Vector2.zero;
            }
        }

        private static void AddUnreadDot(CodexSheetView v)
        {
            v.UnreadDot = CodexArt.Raw("Unread", v.Root, CodexArt.Disc, Rubric);
            RectTransform r = v.UnreadDot.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(12f, 12f);
            r.anchoredPosition = new Vector2(-26f, -18f);
            v.UnreadDot.enabled = false;
        }

        private static void WordButton(RectTransform content, float x, float y, float w, float h, string word, System.Action<string> onWord)
        {
            Image hit = CodexArt.Solid("Word_" + word, content, new Color(CodexArt.GoldInk.r, CodexArt.GoldInk.g, CodexArt.GoldInk.b, 0f));
            hit.raycastTarget = true;
            CodexArt.TopLeft(hit.rectTransform, x - 8f, y, w, h);
            CodexWordTag tag = hit.gameObject.AddComponent<CodexWordTag>();
            tag.Word = word;
            tag.OnWord = onWord;
            // A thin gold underline says: this word can be spoken.
            Image line = CodexArt.Solid("Speakable", content, new Color(CodexArt.GoldInk.r, CodexArt.GoldInk.g, CodexArt.GoldInk.b, 0.55f));
            CodexArt.TopLeft(line.rectTransform, x, y + h - 6f, w - 34f, 1.5f);
            line.raycastTarget = false;
        }

        private static float Hash(int i)
        {
            float x = Mathf.Sin(i * 12.9898f + 4.1414f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }
    }

    /// <summary>Marks a tappable word on the roll, for gamepad selection.</summary>
    public sealed class CodexWordTag : MonoBehaviour
    {
        public string Word;
        public System.Action<string> OnWord;
    }
}
