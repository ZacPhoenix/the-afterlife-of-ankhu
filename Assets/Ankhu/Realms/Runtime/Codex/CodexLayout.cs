using System.Collections.Generic;
using System.Text;
using Ankhu.Realms.Core;
using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// Pagination of the codex. Pages 0 and 1 (the first spread) are the spell pages, laid out by
    /// <see cref="CodexPages"/>. From page 2 on, every Lore, Clue and Memory entry flows in written order,
    /// measured with the real fonts, splitting long bodies across pages. Any number of entries paginates.
    /// </summary>
    public sealed class CodexLayout
    {
        // Book geometry, canvas units (reference height 1080).
        public const float PageW = 610f;
        public const float PageH = 860f;
        public const float MarginSpine = 62f;
        public const float MarginOuter = 64f;
        public const float MarginTop = 96f;
        public const float MarginBottom = 96f;
        public const float ContentW = PageW - MarginSpine - MarginOuter;
        public const float ContentH = PageH - MarginTop - MarginBottom;

        // Entry typography.
        public const int BodySize = 24;
        public const float BodySpacing = 1.02f;
        public const int TitleSize = 27;
        public const int AddendumSize = 22;
        public const float HeaderRowH = 22f;
        public const float HeadingGlyphH = 46f;
        public const float EntryGap = 30f;
        public const float DividerH = 34f;

        public enum PieceKind { Header, Body, Image, Addendum, Divider, Continued }

        public sealed class Piece
        {
            public PieceKind Kind;
            public CodexEntry Entry;
            public string Text;
            public bool FirstOfBody;
            public float Y;
            public float H;
        }

        public sealed class Page
        {
            public int Index;
            public readonly List<Piece> Pieces = new List<Piece>();
            /// <summary>Entries shown (or begun) on this page, for read marking.</summary>
            public readonly List<string> EntryIds = new List<string>();
            public string RunningHead = string.Empty;
        }

        public readonly List<Page> Pages = new List<Page>();
        public int SpreadCount => Pages.Count / 2;

        public static readonly CodexKind[] FlowKinds = { CodexKind.Lore, CodexKind.Clue, CodexKind.Memory };

        public static CodexLayout Build(CodexJournal journal)
        {
            var layout = new CodexLayout();
            var spellL = new Page { Index = 0, RunningHead = "WORDS OF POWER" };
            var spellR = new Page { Index = 1, RunningHead = "NAMES" };
            foreach (CodexEntry e in journal.Entries(CodexKind.Word, CodexKind.Name))
            {
                spellL.EntryIds.Add(e.Id);
                spellR.EntryIds.Add(e.Id);
            }
            layout.Pages.Add(spellL);
            layout.Pages.Add(spellR);

            Page cur = null;
            float y = 0f;

            void NewPage()
            {
                cur = new Page { Index = layout.Pages.Count };
                layout.Pages.Add(cur);
                y = 0f;
            }

            void Add(Piece p)
            {
                p.Y = y;
                cur.Pieces.Add(p);
                if (!cur.EntryIds.Contains(p.Entry.Id)) cur.EntryIds.Add(p.Entry.Id);
                if (string.IsNullOrEmpty(cur.RunningHead) && !string.IsNullOrEmpty(p.Entry.Realm)) cur.RunningHead = p.Entry.Realm.ToUpperInvariant();
                y += p.H;
            }

            float oneLine = Mathf.Ceil(MeasureBody("Ag") + 2f);

            foreach (CodexEntry e in journal.Entries(FlowKinds))
            {
                if (cur == null) NewPage();
                else if (y > 0f)
                {
                    // Divider between entries, unless the page is nearly full.
                    if (y + DividerH + HeaderHeight(e) + oneLine * 2f > ContentH) NewPage();
                    else Add(new Piece { Kind = PieceKind.Divider, Entry = e, H = DividerH });
                }

                float hh = HeaderHeight(e);
                if (y + hh + oneLine * 2f > ContentH && y > 0f) NewPage();
                Add(new Piece { Kind = PieceKind.Header, Entry = e, H = hh });

                if (e.Kind == CodexKind.Memory)
                {
                    float ih = ImageHeight();
                    if (y + ih > ContentH) NewPage();
                    Add(new Piece { Kind = PieceKind.Image, Entry = e, H = ih });
                }

                FlowText(e, e.Body, PieceKind.Body, true);
                if (e.Addenda != null)
                {
                    foreach (string a in e.Addenda)
                    {
                        if (string.IsNullOrWhiteSpace(a)) continue;
                        FlowText(e, a, PieceKind.Addendum, false);
                    }
                }
                y += EntryGap * 0.5f;
            }

            void FlowText(CodexEntry e, string text, PieceKind kind, bool firstBody)
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                string[] words = text.Split(' ');
                int start = 0;
                bool first = firstBody;
                while (start < words.Length)
                {
                    float room = ContentH - y;
                    string rest = Join(words, start, words.Length);
                    float full = Measure(kind, rest);
                    if (full <= room)
                    {
                        Add(new Piece { Kind = kind, Entry = e, Text = rest, FirstOfBody = first, H = full + Gap(kind) });
                        return;
                    }
                    // Largest prefix that fits.
                    int lo = 0, hi = words.Length - start;
                    while (lo < hi)
                    {
                        int mid = (lo + hi + 1) / 2;
                        if (Measure(kind, Join(words, start, start + mid)) <= room) lo = mid; else hi = mid - 1;
                    }
                    if (lo < 3)
                    {
                        if (y <= 0f) lo = Mathf.Max(1, lo); // a page can not hold even this: force progress
                        else { NewPage(); continue; }
                    }
                    string part = Join(words, start, start + lo);
                    Add(new Piece { Kind = kind, Entry = e, Text = part, FirstOfBody = first, H = Measure(kind, part) });
                    first = false;
                    start += lo;
                    NewPage();
                    Add(new Piece { Kind = PieceKind.Continued, Entry = e, H = HeaderRowH + 10f });
                }
            }

            int content = layout.Pages.Count - 2;
            if (content % 2 == 1) layout.Pages.Add(new Page { Index = layout.Pages.Count });
            // A blank spread at the end, so the player sees the pages still to be written.
            if (content % 2 == 0)
            {
                layout.Pages.Add(new Page { Index = layout.Pages.Count });
                layout.Pages.Add(new Page { Index = layout.Pages.Count });
            }
            return layout;
        }

        /// <summary>The spread that holds this entry, or -1.</summary>
        public int SpreadOf(string id)
        {
            foreach (Page p in Pages)
            {
                if (p.EntryIds.Contains(id)) return p.Index / 2;
            }
            return -1;
        }

        // ------------------------------------------------------------------ measuring

        public static float HeaderHeight(CodexEntry e)
        {
            float h = HeaderRowH + 12f;
            if (HasSigns(e)) h += HeadingGlyphH + 12f;
            h += CodexArt.MeasureHeight(e.Title ?? string.Empty, CodexArt.Caps, TitleSize, FontStyle.Normal, 1f, ContentW) + 14f;
            return Mathf.Ceil(h);
        }

        public static bool HasSigns(CodexEntry e)
        {
            if (string.IsNullOrWhiteSpace(e.Signs)) return false;
            foreach (string s in e.Signs.Split(' '))
            {
                if (s.Length > 0 && GlyphAtlas.Has(s)) return true;
            }
            return false;
        }

        public static float ImageHeight() => Mathf.Round(ContentW * 0.52f) + 22f;

        public static float MeasureBody(string text) => CodexArt.MeasureHeight(text, CodexArt.Body, BodySize, FontStyle.Italic, BodySpacing, ContentW);

        public static float AddendumIndent => 26f;

        private static float Measure(PieceKind kind, string text)
        {
            if (kind == PieceKind.Addendum)
                return Mathf.Ceil(CodexArt.MeasureHeight(text, CodexArt.Body, AddendumSize, FontStyle.Italic, BodySpacing, ContentW - AddendumIndent) * 1.03f) + 2f;
            return Mathf.Ceil(MeasureBody(text) * 1.03f) + 2f;
        }

        private static float Gap(PieceKind kind) => kind == PieceKind.Addendum ? 8f : 6f;

        private static string Join(string[] words, int from, int to)
        {
            var sb = new StringBuilder();
            for (int i = from; i < to; i++)
            {
                if (i > from) sb.Append(' ');
                sb.Append(words[i]);
            }
            return sb.ToString();
        }
    }
}
