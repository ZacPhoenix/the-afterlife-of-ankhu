using System.Collections.Generic;
using Ankhu.Realms.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Realms
{
    /// <summary>
    /// Builds one page of the codex as uGUI: papyrus, gutter shade, gold frieze and corners, folio
    /// (Egyptian numerals plus small Arabic), and the content. Even page indices are left hand pages.
    /// Pages are built under an inactive root so their ink and seal effects start when first shown.
    /// </summary>
    public static class CodexPages
    {
        private const float L = CodexLayout.PageW;
        private const float H = CodexLayout.PageH;

        public sealed class Built
        {
            public RectTransform Root;
            public CanvasGroup Content;
        }

        public static Built Build(CodexLayout layout, int index, Transform pool)
        {
            bool left = index % 2 == 0;
            var go = new GameObject("Page_" + (index + 1), typeof(RectTransform));
            go.SetActive(false);
            var root = (RectTransform)go.transform;
            root.SetParent(pool, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(L, H);

            BuildPaper(root, left, index);

            RectTransform content = CodexArt.Rect("Content", root);
            content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            float x0 = left ? CodexLayout.MarginOuter : CodexLayout.MarginSpine;
            content.anchoredPosition = new Vector2(x0, -CodexLayout.MarginTop);
            content.sizeDelta = new Vector2(CodexLayout.ContentW, CodexLayout.ContentH);
            var cg = content.gameObject.AddComponent<CanvasGroup>();
            cg.interactable = false;
            cg.blocksRaycasts = false;

            CodexLayout.Page page = index < layout.Pages.Count ? layout.Pages[index] : null;
            if (index == 0) BuildSpellsLeft(content);
            else if (index == 1) BuildSpellsRight(content);
            else if (page != null && page.Pieces.Count > 0) BuildPieces(content, page);
            else BuildBlank(content, layout, index);

            BuildFolio(root, left, index, page != null ? page.RunningHead : string.Empty);
            return new Built { Root = root, Content = cg };
        }

        /// <summary>A plain papyrus leaf (both faces of the fan pages when the book opens).</summary>
        public static RectTransform Blank(Transform parent, bool left)
        {
            RectTransform r = CodexArt.Rect(left ? "BlankLeft" : "BlankRight", parent);
            UiStretch(r);
            BuildPaper(r, left, left ? 98 : 99);
            return r;
        }

        // ------------------------------------------------------------------ paper and furniture

        private static void BuildPaper(RectTransform root, bool left, int index)
        {
            RawImage paper = CodexArt.Raw("Papyrus", root, CodexArt.Tex("papyrus_page"), Color.white);
            UiStretch(paper.rectTransform);
            // Mirror the sheet on the left so the deckled edge is always outside. Every other leaf is flipped
            // vertically too, so neighbouring pages do not show the same fibres.
            bool flipV = (index / 2) % 2 == 1;
            paper.uvRect = new UnityEngine.Rect(left ? 1f : 0f, flipV ? 1f : 0f, left ? -1f : 1f, flipV ? -1f : 1f);
            if (!CodexArt.HasTex("papyrus_page")) paper.color = new Color(0.90f, 0.82f, 0.64f, 1f);

            RawImage gutter = CodexArt.Raw("Gutter", root, CodexArt.Tex("page_gutter"), Color.white);
            RectTransform g = gutter.rectTransform;
            g.anchorMin = new Vector2(left ? 1f : 0f, 0f);
            g.anchorMax = new Vector2(left ? 1f : 0f, 1f);
            g.pivot = new Vector2(left ? 1f : 0f, 0.5f);
            g.sizeDelta = new Vector2(150f, 0f);
            g.anchoredPosition = Vector2.zero;
            gutter.uvRect = left ? new UnityEngine.Rect(1f, 0f, -1f, 1f) : new UnityEngine.Rect(0f, 0f, 1f, 1f);
        }

        private static void BuildFolio(RectTransform root, bool left, int index, string runningHead)
        {
            // Gold frieze along the top margin, between the corner ornaments.
            const float friezeH = 24f;
            float inner = 46f, outer = 96f;
            RawImage frieze = CodexArt.Raw("Frieze", root, CodexArt.Tex("gold_frieze", true), Color.white);
            RectTransform fr = frieze.rectTransform;
            fr.anchorMin = fr.anchorMax = new Vector2(0f, 1f);
            fr.pivot = new Vector2(0f, 1f);
            float fw = L - inner - outer;
            fr.sizeDelta = new Vector2(fw, friezeH);
            fr.anchoredPosition = new Vector2(left ? outer : inner, -44f);
            frieze.uvRect = new UnityEngine.Rect(0f, 0f, fw / (friezeH * 8f), 1f);

            // Double rule above the folio.
            for (int i = 0; i < 2; i++)
            {
                Image rule = CodexArt.Solid("Rule", root, new Color(CodexArt.GoldInk.r, CodexArt.GoldInk.g, CodexArt.GoldInk.b, i == 0 ? 0.75f : 0.45f));
                RectTransform rr = rule.rectTransform;
                rr.anchorMin = rr.anchorMax = new Vector2(0f, 0f);
                rr.pivot = new Vector2(0f, 0f);
                rr.sizeDelta = new Vector2(fw, i == 0 ? 2f : 1f);
                rr.anchoredPosition = new Vector2(left ? outer : inner, 70f - i * 5f);
                CodexGradient.Add(rule, left ? new Color(1, 1, 1, 0.2f) : Color.white, left ? Color.white : new Color(1, 1, 1, 0.2f), false);
            }

            // Gold corner ornaments at the outer corners.
            for (int i = 0; i < 2; i++)
            {
                bool top = i == 0;
                RawImage c = CodexArt.Raw("Corner", root, CodexArt.Tex("corner_ornament"), Color.white);
                RectTransform cr = c.rectTransform;
                Vector2 a = new Vector2(left ? 0f : 1f, top ? 1f : 0f);
                cr.anchorMin = cr.anchorMax = a;
                cr.pivot = new Vector2(0.5f, 0.5f);
                cr.sizeDelta = new Vector2(86f, 86f);
                cr.anchoredPosition = new Vector2(left ? 64f : -64f, top ? -58f : 58f);
                cr.localScale = new Vector3(left ? 1f : -1f, top ? 1f : -1f, 1f);
            }

            // Folio: Egyptian numerals, the Arabic number small beside them.
            int number = index + 1;
            RectTransform folio = CodexArt.Rect("Folio", root);
            folio.anchorMin = folio.anchorMax = new Vector2(left ? 0f : 1f, 0f);
            folio.pivot = new Vector2(left ? 0f : 1f, 0f);
            CodexArt.Numeral(folio, number, 22f, CodexArt.InkSoft, out float nw);
            Text arabic = CodexArt.Label("Arabic", folio, number.ToString(), CodexArt.Caps, 13, CodexArt.OchreSoft, TextAnchor.LowerLeft);
            RectTransform ar = arabic.rectTransform;
            ar.anchorMin = ar.anchorMax = ar.pivot = Vector2.zero;
            ar.sizeDelta = new Vector2(40f, 18f);
            float aw = CodexArt.MeasureWidth(number.ToString(), CodexArt.Caps, 13, FontStyle.Normal);
            ar.anchoredPosition = new Vector2(nw + 10f, -1f);
            folio.sizeDelta = new Vector2(nw + 10f + aw, 22f);
            folio.anchoredPosition = new Vector2(left ? 70f : -70f, 34f);

            if (!string.IsNullOrEmpty(runningHead))
            {
                Text head = CodexArt.Label("RunningHead", root, runningHead, CodexArt.Caps, 13, CodexArt.OchreSoft, TextAnchor.MiddleCenter);
                RectTransform hr = head.rectTransform;
                hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0f);
                hr.pivot = new Vector2(0.5f, 0f);
                hr.sizeDelta = new Vector2(300f, 20f);
                hr.anchoredPosition = new Vector2(left ? 8f : -8f, 36f);
            }
        }

        // ------------------------------------------------------------------ spell pages

        private struct SpellPlan
        {
            public List<CodexEntry> Words;
            public int Slots;
            public int LeftCount;
            public int Cols;
            public float CardH;
            public List<CodexEntry> Names;
            public int NameSlots;
        }

        private const float SpellHeadH = 164f;
        private const float NameRowH = 150f;
        private const float NamesHeadH = 112f;

        private static SpellPlan PlanSpells()
        {
            var p = new SpellPlan { Words = new List<CodexEntry>(), Names = new List<CodexEntry>() };
            foreach (CodexEntry e in CodexStore.Journal.Entries(CodexKind.Word)) p.Words.Add(e);
            foreach (CodexEntry e in CodexStore.Journal.Entries(CodexKind.Name)) p.Names.Add(e);
            int catalogWords = 0, catalogNames = 0;
            foreach (CodexEntry e in CodexCatalog.All.Values)
            {
                if (e.Kind == CodexKind.Word) catalogWords++;
                else if (e.Kind == CodexKind.Name) catalogNames++;
            }
            p.Slots = Mathf.Max(p.Words.Count, catalogWords);
            p.NameSlots = Mathf.Max(p.Names.Count, catalogNames, 1);

            int nameRows = Mathf.CeilToInt(p.NameSlots / 2f);
            float budgetL = CodexLayout.ContentH - SpellHeadH;
            float budgetR = CodexLayout.ContentH - NamesHeadH - nameRows * NameRowH;
            p.Cols = 1;
            p.CardH = 128f;
            int capL = Mathf.FloorToInt(budgetL / p.CardH), capR = Mathf.Max(0, Mathf.FloorToInt(budgetR / p.CardH));
            if (p.Slots > capL + capR)
            {
                p.Cols = 2;
                p.CardH = 150f;
                capL = Mathf.FloorToInt(budgetL / p.CardH) * 2;
                capR = Mathf.Max(0, Mathf.FloorToInt(budgetR / p.CardH)) * 2;
                if (p.Slots > capL + capR)
                {
                    // Squeeze the rows until everything fits on the two spell pages.
                    int rows = Mathf.CeilToInt(p.Slots / 2f) + 1;
                    p.CardH = Mathf.Max(70f, (budgetL + Mathf.Max(0f, budgetR)) / rows);
                    capL = Mathf.FloorToInt(budgetL / p.CardH) * 2;
                    capR = Mathf.Max(0, Mathf.FloorToInt(budgetR / p.CardH)) * 2;
                }
            }
            int balanced = Mathf.CeilToInt(p.Slots * capL / (float)Mathf.Max(1, capL + capR));
            p.LeftCount = Mathf.Clamp(Mathf.Max(balanced, p.Slots - capR), 0, Mathf.Min(capL, p.Slots));
            if (p.Cols == 2 && p.LeftCount % 2 == 1 && p.LeftCount < capL && p.LeftCount < p.Slots) p.LeftCount++;
            return p;
        }

        private static void BuildSpellsLeft(RectTransform content)
        {
            SpellPlan p = PlanSpells();
            float w = CodexLayout.ContentW;

            Text title = CodexArt.Label("Title", content, "THE WORDS OF POWER", CodexArt.Caps, 31, CodexArt.Ochre, TextAnchor.MiddleCenter);
            CodexArt.TopLeft(title.rectTransform, 0f, 0f, w, 40f);
            // medu netjer, "the god's words": what Egyptians called their own writing.
            RectTransform gHolder = CodexArt.TopLeft(CodexArt.Rect("Heading", content), 0f, 48f, w, 44f);
            AddGoldGlyphs(gHolder, "S43 R8", 40f, w, true);
            Text sub = CodexArt.Label("Sub", content, "Given by Anpu. Spoken aloud, they move the Duat.", CodexArt.Body, 20, CodexArt.InkSoft,
                TextAnchor.MiddleCenter, FontStyle.Italic);
            CodexArt.TopLeft(sub.rectTransform, 0f, 98f, w, 28f);
            Divider(content, 132f, 320f);

            float y = SpellHeadH;
            for (int i = 0; i < p.LeftCount; i++) y = PlaceCard(content, p, i, i, y);
        }

        private static void BuildSpellsRight(RectTransform content)
        {
            SpellPlan p = PlanSpells();
            float w = CodexLayout.ContentW;
            float y = 0f;
            int onRight = p.Slots - p.LeftCount;
            for (int i = 0; i < onRight; i++) y = PlaceCard(content, p, p.LeftCount + i, i, y);
            if (onRight > 0) y += 18f;

            Text title = CodexArt.Label("NamesTitle", content, "NAMES", CodexArt.Caps, 27, CodexArt.Ochre, TextAnchor.MiddleCenter);
            CodexArt.TopLeft(title.rectTransform, 0f, y, w, 34f);
            RectTransform gHolder = CodexArt.TopLeft(CodexArt.Rect("Heading", content), 0f, y + 38f, w, 34f);
            AddGoldGlyphs(gHolder, "D21 N35 Z1", 30f, w, true);
            Text sub = CodexArt.Label("Sub", content, "What is named can be called.", CodexArt.Body, 19, CodexArt.InkSoft, TextAnchor.MiddleCenter, FontStyle.Italic);
            CodexArt.TopLeft(sub.rectTransform, 0f, y + 74f, w, 26f);
            y += NamesHeadH;

            float cw = (w - 26f) / 2f;
            for (int i = 0; i < p.NameSlots; i++)
            {
                int row = i / 2, col = i % 2;
                bool lone = p.NameSlots % 2 == 1 && i == p.NameSlots - 1;
                float x = lone ? (w - cw) / 2f : col * (cw + 26f);
                RectTransform cell = CodexArt.TopLeft(CodexArt.Rect("Name", content), x, y + row * NameRowH, cw, NameRowH - 10f);
                NameCell(cell, i < p.Names.Count ? p.Names[i] : null, cw, NameRowH - 10f);
            }
        }

        private static float PlaceCard(RectTransform content, SpellPlan p, int slot, int indexOnPage, float y)
        {
            float w = CodexLayout.ContentW;
            float cw = p.Cols == 1 ? w : (w - 18f) / 2f;
            int col = p.Cols == 1 ? 0 : indexOnPage % 2;
            float x = col * (cw + 18f);
            RectTransform card = CodexArt.TopLeft(CodexArt.Rect("Card", content), x, y, cw, p.CardH - 8f);
            WordCard(card, slot < p.Words.Count ? p.Words[slot] : null, cw, p.CardH - 8f, p.Cols == 2, indexOnPage);
            bool rowDone = p.Cols == 1 || col == 1;
            return rowDone ? y + p.CardH : y;
        }

        private static void WordCard(RectTransform card, CodexEntry e, float w, float h, bool compact, int order)
        {
            float glyphW = compact ? Mathf.Min(w * 0.40f, 92f) : 156f;
            float glyphH = compact ? 38f : 54f;
            float tx = glyphW + 18f;
            float tw = w - tx - 4f;

            // Hairline under the card.
            Image line = CodexArt.Solid("Line", card, new Color(CodexArt.Ink.r, CodexArt.Ink.g, CodexArt.Ink.b, 0.16f));
            RectTransform lr = line.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 0f); lr.pivot = new Vector2(0.5f, 0f);
            lr.sizeDelta = new Vector2(0f, 1f); lr.anchoredPosition = new Vector2(0f, -2f);
            CodexGradient.Add(line, new Color(1, 1, 1, 0f), Color.white, false);

            RectTransform glyphBox = CodexArt.TopLeft(CodexArt.Rect("GlyphBox", card), 0f, 0f, glyphW, h);

            if (e == null)
            {
                // A word still to come: a faint blank cartouche and ruled lines.
                Sprite cart = GlyphAtlas.Cartouche;
                float ch = Mathf.Min(h * 0.55f, 60f);
                Image c = CodexArt.SlicedImage("BlankCartouche", glyphBox, cart, new Color(CodexArt.Ink.r, CodexArt.Ink.g, CodexArt.Ink.b, 0.17f), 256f / ch);
                CodexArt.Centered(c.rectTransform, Vector2.zero, new Vector2(glyphW * 0.92f, ch));
                c.rectTransform.anchorMin = c.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                for (int i = 0; i < 3; i++)
                {
                    Image ruled = CodexArt.Solid("Ruled", card, new Color(CodexArt.Ink.r, CodexArt.Ink.g, CodexArt.Ink.b, 0.10f));
                    float lw = tw * (i == 0 ? 0.42f : i == 1 ? 0.86f : 0.64f);
                    CodexArt.TopLeft(ruled.rectTransform, tx, h * 0.30f + i * h * 0.20f, lw, i == 0 ? 3f : 1.5f);
                }
                return;
            }

            GameObject ink = card.gameObject;
            CodexInkDry dry = CodexInkDry.Attach(ink, e.Id, 0.25f + order * 0.18f);

            RawImage bleed = CodexArt.Raw("Wash", glyphBox, CodexArt.Tex("ink_bleed"), new Color(CodexArt.GoldInk.r, CodexArt.GoldInk.g, CodexArt.GoldInk.b, 0.13f));
            CodexArt.Centered(bleed.rectTransform, Vector2.zero, new Vector2(glyphW * 0.95f, h * 0.85f));
            dry.Bleed = CodexArt.Raw("InkBleed", glyphBox, CodexArt.Tex("ink_bleed"), new Color(0, 0, 0, 0));
            CodexArt.Centered(dry.Bleed.rectTransform, Vector2.zero, new Vector2(glyphW * 1.1f, h));
            AddGoldGlyphs(glyphBox, e.Signs, glyphH, glyphW - 12f, false);

            Image sep = CodexArt.Solid("Sep", card, new Color(CodexArt.GoldInk.r, CodexArt.GoldInk.g, CodexArt.GoldInk.b, 0.45f));
            CodexArt.TopLeft(sep.rectTransform, glyphW + 6f, h * 0.14f, 1f, h * 0.72f);
            sep.gameObject.AddComponent<CodexMidFade>();

            int titleSize = compact ? 22 : 29;
            Text title = CodexArt.Label("Word", card, (e.Title ?? string.Empty).ToUpperInvariant(), CodexArt.Caps, titleSize, CodexArt.Ink, TextAnchor.UpperLeft);
            CodexArt.TopLeft(title.rectTransform, tx, compact ? 8f : 12f, tw, titleSize + 8f);
            if (!string.IsNullOrEmpty(e.Transliteration))
            {
                Text tr = CodexArt.Label("Translit", card, e.Transliteration, CodexArt.Body, compact ? 18 : 22, CodexArt.Ochre, TextAnchor.UpperRight, FontStyle.Italic);
                CodexArt.TopLeft(tr.rectTransform, tx, compact ? 8f : 14f, tw - 34f, titleSize + 6f);
            }
            float ey = (compact ? 8f : 12f) + titleSize + 10f;
            Text effect = CodexArt.Label("Effect", card, e.Effect, CodexArt.Body, compact ? 16 : 19, CodexArt.InkSoft, TextAnchor.UpperLeft, FontStyle.Italic, 0.98f);
            effect.resizeTextForBestFit = true;
            effect.resizeTextMinSize = compact ? 12 : 14;
            effect.resizeTextMaxSize = compact ? 16 : 19;
            effect.verticalOverflow = VerticalWrapMode.Truncate;
            CodexArt.TopLeft(effect.rectTransform, tx, ey, tw, Mathf.Max(20f, h - ey - 8f));

            Seal(card, e.Id, new Vector2(-2f, -4f), compact ? 24f : 30f);
        }

        private static void NameCell(RectTransform cell, CodexEntry e, float w, float h)
        {
            float ch = 88f;
            Sprite cart = GlyphAtlas.Cartouche;
            RectTransform top = CodexArt.TopLeft(CodexArt.Rect("Cartouche", cell), (w - Mathf.Min(w, 236f)) / 2f, 0f, Mathf.Min(w, 236f), ch);
            if (e == null)
            {
                Image c = CodexArt.SlicedImage("Blank", top, cart, new Color(CodexArt.Ink.r, CodexArt.Ink.g, CodexArt.Ink.b, 0.17f), 256f / ch);
                UiStretch(c.rectTransform);
                Image ruled = CodexArt.Solid("Ruled", cell, new Color(CodexArt.Ink.r, CodexArt.Ink.g, CodexArt.Ink.b, 0.10f));
                CodexArt.TopLeft(ruled.rectTransform, w * 0.3f, ch + 22f, w * 0.4f, 2f);
                return;
            }
            CodexInkDry.Attach(cell.gameObject, e.Id, 0.4f);
            Image frame = CodexArt.SlicedImage("Frame", top, cart, CodexArt.GoldInk, 256f / ch);
            UiStretch(frame.rectTransform);
            CodexGradient.Add(frame, new Color(1.18f, 1.12f, 1f, 1f), new Color(0.82f, 0.74f, 0.62f, 1f), true);
            RectTransform inner = CodexArt.Rect("Inner", top);
            inner.anchorMin = inner.anchorMax = new Vector2(0.5f, 0.5f);
            inner.anchoredPosition = new Vector2(-10f, 0f);
            inner.sizeDelta = new Vector2(Mathf.Min(w, 236f) - 90f, ch - 30f);
            AddGoldGlyphs(inner, e.Signs, 44f, Mathf.Min(w, 236f) - 92f, false);

            Text name = CodexArt.Label("Name", cell, (e.Title ?? string.Empty).ToUpperInvariant(), CodexArt.Caps, 23, CodexArt.Ink, TextAnchor.UpperCenter);
            CodexArt.TopLeft(name.rectTransform, 0f, ch + 6f, w, 30f);
            if (!string.IsNullOrEmpty(e.Transliteration))
            {
                Text tr = CodexArt.Label("Translit", cell, e.Transliteration, CodexArt.Body, 19, CodexArt.Ochre, TextAnchor.UpperCenter, FontStyle.Italic);
                CodexArt.TopLeft(tr.rectTransform, 0f, ch + 34f, w, 24f);
            }
            Seal(top, e.Id, new Vector2(4f, 6f), 28f);
        }

        // ------------------------------------------------------------------ flowing entries

        private static void BuildPieces(RectTransform content, CodexLayout.Page page)
        {
            float w = CodexLayout.ContentW;
            var groups = new Dictionary<string, RectTransform>();
            int order = 0;
            foreach (CodexLayout.Piece piece in page.Pieces)
            {
                CodexEntry e = piece.Entry;
                if (piece.Kind == CodexLayout.PieceKind.Divider)
                {
                    Divider(content, piece.Y + piece.H * 0.5f - 11f, 260f);
                    continue;
                }
                // All pieces of one entry on this page dry together.
                if (!groups.TryGetValue(e.Id, out RectTransform g))
                {
                    g = CodexArt.TopLeft(CodexArt.Rect("Entry_" + e.Id, content), 0f, 0f, w, CodexLayout.ContentH);
                    CodexInkDry dry = CodexInkDry.Attach(g.gameObject, e.Id, 0.3f + order * 0.35f);
                    dry.Bleed = CodexArt.Raw("InkBleed", g, CodexArt.Tex("ink_bleed"), new Color(0, 0, 0, 0));
                    CodexArt.TopLeft(dry.Bleed.rectTransform, w * 0.15f, piece.Y, w * 0.7f, Mathf.Min(220f, piece.H + 40f));
                    groups[e.Id] = g;
                    order++;
                }
                switch (piece.Kind)
                {
                    case CodexLayout.PieceKind.Header: Header(g, e, piece.Y, w); break;
                    case CodexLayout.PieceKind.Image: MemoryImage(g, e, piece.Y, w, piece.H); break;
                    case CodexLayout.PieceKind.Body: BodyText(g, piece, w); break;
                    case CodexLayout.PieceKind.Addendum: Addendum(g, piece, w); break;
                    case CodexLayout.PieceKind.Continued:
                        Text c = CodexArt.Label("Continued", g, (e.Title ?? string.Empty).ToUpperInvariant() + ", CONTINUED", CodexArt.Caps, 13,
                            CodexArt.OchreSoft, TextAnchor.MiddleLeft);
                        CodexArt.TopLeft(c.rectTransform, 0f, piece.Y, w, CodexLayout.HeaderRowH);
                        break;
                }
            }
        }

        private static void Header(RectTransform g, CodexEntry e, float y, float w)
        {
            string kind = CodexArt.KindLabel(e.Kind);
            string realm = string.IsNullOrEmpty(e.Realm) ? string.Empty : "   ·   " + e.Realm.ToUpperInvariant();
            string label = kind + realm;
            float lw = CodexArt.MeasureWidth(label, CodexArt.Caps, 14, FontStyle.Normal);
            float iconS = 20f;
            float rowW = iconS + 8f + lw;
            float x0 = (w - rowW) / 2f;
            RawImage icon = CodexArt.Raw("KindIcon", g, CodexArt.KindIcon(e.Kind), CodexArt.Ochre);
            CodexArt.TopLeft(icon.rectTransform, x0, y + 1f, iconS, iconS);
            Text k = CodexArt.Label("Kind", g, label, CodexArt.Caps, 14, CodexArt.OchreSoft, TextAnchor.MiddleLeft);
            CodexArt.TopLeft(k.rectTransform, x0 + iconS + 8f, y, lw + 8f, CodexLayout.HeaderRowH);
            // Short gold rules either side of the kind row.
            for (int s = 0; s < 2; s++)
            {
                Image r = CodexArt.Solid("Rule", g, new Color(CodexArt.GoldInk.r, CodexArt.GoldInk.g, CodexArt.GoldInk.b, 0.6f));
                float rw = Mathf.Max(0f, x0 - 26f);
                CodexArt.TopLeft(r.rectTransform, s == 0 ? 6f : x0 + rowW + 20f, y + CodexLayout.HeaderRowH * 0.5f, rw - 6f, 1f);
                CodexGradient.Add(r, s == 0 ? new Color(1, 1, 1, 0f) : Color.white, s == 0 ? Color.white : new Color(1, 1, 1, 0f), false);
            }
            float yy = y + CodexLayout.HeaderRowH + 12f;
            if (CodexLayout.HasSigns(e))
            {
                RectTransform holder = CodexArt.TopLeft(CodexArt.Rect("Heading", g), 0f, yy, w, CodexLayout.HeadingGlyphH);
                AddGoldGlyphs(holder, e.Signs, CodexLayout.HeadingGlyphH, w * 0.8f, false);
                yy += CodexLayout.HeadingGlyphH + 12f;
            }
            Text title = CodexArt.Label("Title", g, (e.Title ?? string.Empty).ToUpperInvariant(), CodexArt.Caps, CodexLayout.TitleSize, CodexArt.Ochre, TextAnchor.UpperCenter);
            float th = CodexArt.MeasureHeight(title.text, CodexArt.Caps, CodexLayout.TitleSize, FontStyle.Normal, 1f, w);
            CodexArt.TopLeft(title.rectTransform, 0f, yy, w, th + 4f);
            Seal(g, e.Id, new Vector2(0f, -y), 30f);
        }

        private static void BodyText(RectTransform g, CodexLayout.Piece piece, float w)
        {
            string text = piece.Text;
            if (piece.FirstOfBody)
            {
                // Rubric: the first words in red, as the scribes began a section.
                int cut = text.IndexOf(' ');
                if (cut > 0)
                {
                    int second = text.IndexOf(' ', cut + 1);
                    if (second > 0 && second < 18) cut = second;
                    text = "<color=#" + CodexArt.Hex(CodexArt.Ochre) + ">" + text.Substring(0, cut) + "</color>" + text.Substring(cut);
                }
            }
            Text t = CodexArt.Label("Body", g, text, CodexArt.Body, CodexLayout.BodySize, CodexArt.Ink, TextAnchor.UpperLeft, FontStyle.Italic, CodexLayout.BodySpacing);
            CodexArt.TopLeft(t.rectTransform, 0f, piece.Y, w, piece.H + 6f);
        }

        private static void Addendum(RectTransform g, CodexLayout.Piece piece, float w)
        {
            float ind = CodexLayout.AddendumIndent;
            RawImage dot = CodexArt.Raw("Dot", g, CodexArt.Disc, CodexArt.Ochre);
            CodexArt.TopLeft(dot.rectTransform, ind * 0.35f, piece.Y + 10f, 7f, 7f);
            Text t = CodexArt.Label("Addendum", g, piece.Text, CodexArt.Body, CodexLayout.AddendumSize, CodexArt.InkSoft, TextAnchor.UpperLeft, FontStyle.Italic, CodexLayout.BodySpacing);
            CodexArt.TopLeft(t.rectTransform, ind, piece.Y, w - ind, piece.H + 6f);
        }

        private static void MemoryImage(RectTransform g, CodexEntry e, float y, float w, float h)
        {
            float ih = h - 22f;
            float iw = w;
            Texture2D painted = Resources.Load<Texture2D>("RealmMemories/" + e.Id);
            RectTransform frame = CodexArt.TopLeft(CodexArt.Rect("Memory", g), 0f, y + 4f, iw, ih);
            if (painted != null)
            {
                RawImage img = CodexArt.Raw("Painting", frame, painted, Color.white);
                UiStretch(img.rectTransform);
                // Cover fit: crop the painting to the frame's aspect.
                float ta = painted.width / (float)Mathf.Max(1, painted.height);
                float fa = iw / ih;
                img.uvRect = ta > fa ? new UnityEngine.Rect((1f - fa / ta) / 2f, 0f, fa / ta, 1f) : new UnityEngine.Rect(0f, (1f - ta / fa) / 2f, 1f, ta / fa);
                // A warm wash so the painting sits on papyrus.
                Image wash = CodexArt.Solid("Wash", frame, new Color(0.55f, 0.38f, 0.18f, 0.10f));
                UiStretch(wash.rectTransform);
            }
            else
            {
                RawImage img = CodexArt.Raw("Sepia", frame, CodexArt.Tex("memory_placeholder"), new Color(1f, 1f, 1f, 0.92f));
                UiStretch(img.rectTransform);
                RectTransform orn = CodexArt.Rect("Ornament", frame);
                CodexArt.Centered(orn, new Vector2(0f, -ih * 0.30f), new Vector2(iw, 30f));
                AddGoldGlyphs(orn, string.IsNullOrWhiteSpace(e.Signs) ? "F34" : e.Signs, 28f, iw * 0.5f, false);
            }
            Image border = CodexArt.SlicedImage("Frame", frame, CodexArt.Sliced("card_frame", new Vector4(40, 40, 40, 40)), CodexArt.GoldInk, 1.4f);
            border.rectTransform.anchorMin = Vector2.zero;
            border.rectTransform.anchorMax = Vector2.one;
            border.rectTransform.offsetMin = new Vector2(-10f, -10f);
            border.rectTransform.offsetMax = new Vector2(10f, 10f);
        }

        private static void BuildBlank(RectTransform content, CodexLayout layout, int index)
        {
            RawImage reed = CodexArt.Raw("Watermark", content, CodexArt.Tex("icon_reed"), new Color(CodexArt.Ink.r, CodexArt.Ink.g, CodexArt.Ink.b, 0.06f));
            CodexArt.Centered(reed.rectTransform, new Vector2(0f, 20f), new Vector2(150f, 150f));
            reed.rectTransform.anchorMin = reed.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            // Only the first blank page carries a line in Ankhu's hand.
            for (int i = 2; i < layout.Pages.Count; i++)
            {
                if (layout.Pages[i].Pieces.Count > 0) continue;
                if (i != index) break;
                Text t = CodexArt.Label("Waiting", content, "Blank still. The road will write them.", CodexArt.Body, 21,
                    new Color(CodexArt.Ink.r, CodexArt.Ink.g, CodexArt.Ink.b, 0.34f), TextAnchor.MiddleCenter, FontStyle.Italic);
                RectTransform r = t.rectTransform;
                CodexArt.Centered(r, new Vector2(0f, -90f), new Vector2(CodexLayout.ContentW, 40f));
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                break;
            }
        }

        // ------------------------------------------------------------------ bits

        public static CodexArt.GlyphGroup AddGoldGlyphs(RectTransform holder, string signs, float height, float maxW, bool shadow)
        {
            CodexArt.GlyphGroup gg = CodexArt.Glyphs(holder, signs, height, CodexArt.GoldInk, maxW);
            gg.Root.anchorMin = gg.Root.anchorMax = new Vector2(0.5f, 0.5f);
            gg.Root.anchoredPosition = Vector2.zero;
            foreach (RawImage s in gg.Signs)
            {
                CodexGradient.Add(s, new Color(1.22f, 1.18f, 1.05f, 1f), new Color(0.80f, 0.70f, 0.58f, 1f), true);
                CodexArt.Shadow(s, CodexArt.GoldShadow, new Vector2(0.8f, -1.2f));
            }
            return gg;
        }

        private static void Divider(RectTransform content, float y, float width)
        {
            RawImage d = CodexArt.Raw("Divider", content, CodexArt.Tex("divider"), new Color(CodexArt.GoldInk.r * 0.9f, CodexArt.GoldInk.g * 0.85f, CodexArt.GoldInk.b, 0.85f));
            CodexArt.TopLeft(d.rectTransform, (CodexLayout.ContentW - width) / 2f, y, width, 22f);
        }

        private static void Seal(RectTransform parent, string id, Vector2 topRightOffset, float size)
        {
            RawImage s = CodexArt.Raw("Seal", parent, CodexArt.Tex("seal"), Color.white);
            RectTransform r = s.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(size, size);
            r.anchoredPosition = new Vector2(-size * 0.5f + topRightOffset.x, -size * 0.5f + topRightOffset.y);
            CodexSeal.Attach(s, id);
        }

        public static void UiStretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            r.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    /// <summary>Fades a thin rule at both ends (a vertical gradient that peaks in the middle).</summary>
    public sealed class CodexMidFade : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount != 4) return;
            // Split the quad in two so the alpha peaks at the centre.
            UnityEngine.Rect r = ((RectTransform)transform).rect;
            Color c = graphic.color;
            vh.Clear();
            float[] ys = { r.yMin, (r.yMin + r.yMax) * 0.5f, r.yMax };
            float[] a = { 0f, 1f, 0f };
            for (int i = 0; i < 3; i++)
            {
                Color ca = new Color(c.r, c.g, c.b, c.a * a[i]);
                vh.AddVert(new Vector3(r.xMin, ys[i]), ca, Vector2.zero);
                vh.AddVert(new Vector3(r.xMax, ys[i]), ca, Vector2.zero);
            }
            vh.AddTriangle(0, 2, 3); vh.AddTriangle(0, 3, 1);
            vh.AddTriangle(2, 4, 5); vh.AddTriangle(2, 5, 3);
        }
    }
}
