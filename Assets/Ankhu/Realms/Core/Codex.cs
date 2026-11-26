using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ankhu.Realms.Core
{
    public enum CodexKind
    {
        /// <summary>A word of power. Filed under Craft or Heart on the roll.</summary>
        Word,
        /// <summary>A name Ankhu has recovered (his own, a god's, a gate's).</summary>
        Name,
        /// <summary>Knowledge about the Duat: a place, a god, a rule of the land.</summary>
        Lore,
        /// <summary>A puzzle clue he wrote down.</summary>
        Clue,
        /// <summary>A memory of his life that returned, or a realization about it.</summary>
        Memory
    }

    /// <summary>One thing Ankhu writes on his roll. Ids are stable ("word.open", "lore.wernes").</summary>
    public sealed class CodexEntry
    {
        public string Id;
        public CodexKind Kind;
        public string Title;
        /// <summary>Gardiner codes for the heading glyphs, space separated. May be empty.</summary>
        public string Signs = string.Empty;
        public string Transliteration = string.Empty;
        /// <summary>The body text in Ankhu's own voice, first person.</summary>
        public string Body = string.Empty;
        /// <summary>For words: what the word does, one line ("Opens what is shut: doors, shrines, mouths.").</summary>
        public string Effect = string.Empty;
        /// <summary>Realm where it was written (for the sheet's rubric).</summary>
        public string Realm = string.Empty;
        /// <summary>Optional extra lines appended when a later event deepens the entry.</summary>
        public List<string> Addenda = new List<string>();
    }

    /// <summary>
    /// The entries the game can write, keyed by id. Content lives in <see cref="CodexContent"/> and
    /// <see cref="CodexRollCatalog"/>.
    /// </summary>
    public static class CodexCatalog
    {
        private static Dictionary<string, CodexEntry> all;

        public static IReadOnlyDictionary<string, CodexEntry> All
        {
            get
            {
                if (all == null)
                {
                    all = new Dictionary<string, CodexEntry>();
                    foreach (CodexEntry e in CodexContent.Entries()) all[e.Id] = e;
                    foreach (CodexEntry e in CodexRollCatalog.ExtraEntries())
                    {
                        if (!all.ContainsKey(e.Id)) all[e.Id] = e;
                    }
                }
                return all;
            }
        }

        public static CodexEntry Get(string id) => id != null && All.TryGetValue(id, out CodexEntry e) ? e : null;
    }

    /// <summary>A margin note on the roll: a hint in Anpu's hand, or a line Ankhu adds beside an open question.</summary>
    public sealed class CodexNoteRecord
    {
        public CodexHand Hand;
        public string Scene;
        public string Text;
    }

    /// <summary>
    /// What Ankhu has written so far, in the order he wrote it. Holds entries, revisions, epithets on the
    /// cartouche, open questions in the margins and notes. Serializes to a small line format so the engine-free
    /// core needs no JSON library. "codex1" saves (entries only) still load.
    /// </summary>
    public sealed class CodexJournal
    {
        private readonly List<string> order = new List<string>();
        private readonly HashSet<string> seen = new HashSet<string>();
        private readonly HashSet<string> unread = new HashSet<string>();

        // The roll's running order: entry ids, and "?scene" tokens where a scene's margin group was opened.
        private readonly List<string> timeline = new List<string>();
        private readonly List<string> revisions = new List<string>();
        private readonly HashSet<string> revisionsUnread = new HashSet<string>();
        private readonly List<string> epithets = new List<string>();
        private readonly HashSet<string> epithetsUnread = new HashSet<string>();
        private readonly List<string> asked = new List<string>();
        private readonly HashSet<string> answered = new HashSet<string>();
        private readonly HashSet<string> answeredUnread = new HashSet<string>();
        private readonly List<CodexNoteRecord> notes = new List<CodexNoteRecord>();

        public event Action<CodexEntry> Written;
        public event Action<CodexRevision> Revised;
        public event Action<CodexEpithet> EpithetAdded;
        public event Action<CodexQuestion> Asked;
        public event Action<CodexQuestion> Answered;
        public event Action<CodexNoteRecord> Noted;

        public IReadOnlyList<string> Order => order;
        public IReadOnlyList<string> Timeline => timeline;
        public IReadOnlyList<string> RevisionIds => revisions;
        public IReadOnlyList<string> EpithetIds => epithets;
        public IReadOnlyList<string> AskedIds => asked;
        public IReadOnlyList<CodexNoteRecord> Notes => notes;

        public bool Has(string id) => seen.Contains(id);
        public bool IsUnread(string id) => unread.Contains(id);
        public int UnreadCount => unread.Count + revisionsUnread.Count + epithetsUnread.Count;
        public bool HasRevision(string id) => revisions.Contains(id);
        public bool IsRevisionUnread(string id) => revisionsUnread.Contains(id);
        public bool HasEpithet(string id) => epithets.Contains(id);
        public bool IsEpithetUnread(string id) => epithetsUnread.Contains(id);
        public bool WasAsked(string id) => asked.Contains(id);
        public bool IsAnswered(string id) => answered.Contains(id);
        public bool IsAnswerUnread(string id) => answeredUnread.Contains(id);

        /// <summary>Revisions applied to an entry, oldest first.</summary>
        public IEnumerable<CodexRevision> RevisionsOf(string entryId)
        {
            foreach (string rid in revisions)
            {
                CodexRevision r = CodexRollCatalog.Revision(rid);
                if (r != null && r.EntryId == entryId) yield return r;
            }
        }

        public IEnumerable<CodexEntry> Entries(params CodexKind[] kinds)
        {
            foreach (string id in order)
            {
                CodexEntry e = CodexCatalog.Get(id);
                if (e != null && (kinds.Length == 0 || Array.IndexOf(kinds, e.Kind) >= 0)) yield return e;
            }
        }

        /// <summary>Write an entry. Returns false if it was already written (nothing changes).</summary>
        public bool Write(string id)
        {
            CodexEntry e = CodexCatalog.Get(id);
            if (e == null || seen.Contains(id)) return false;
            seen.Add(id);
            order.Add(id);
            timeline.Add(id);
            unread.Add(id);
            Written?.Invoke(e);
            return true;
        }

        /// <summary>Strike an entry's current line and write the true one under it. The entry must already be written.</summary>
        public bool Revise(string revisionId)
        {
            CodexRevision r = CodexRollCatalog.Revision(revisionId);
            if (r == null || revisions.Contains(revisionId) || !seen.Contains(r.EntryId)) return false;
            revisions.Add(revisionId);
            revisionsUnread.Add(revisionId);
            Revised?.Invoke(r);
            return true;
        }

        public bool AddEpithet(string epithetId)
        {
            CodexEpithet ep = CodexRollCatalog.Epithet(epithetId);
            if (ep == null || epithets.Contains(epithetId)) return false;
            epithets.Add(epithetId);
            epithetsUnread.Add(epithetId);
            EpithetAdded?.Invoke(ep);
            return true;
        }

        public bool Ask(string questionId)
        {
            CodexQuestion q = CodexRollCatalog.Question(questionId);
            if (q == null || asked.Contains(questionId)) return false;
            EnsureGroup(q.Scene);
            asked.Add(questionId);
            Asked?.Invoke(q);
            return true;
        }

        public bool Answer(string questionId)
        {
            CodexQuestion q = CodexRollCatalog.Question(questionId);
            if (q == null || answered.Contains(questionId)) return false;
            if (!asked.Contains(questionId)) Ask(questionId);
            answered.Add(questionId);
            answeredUnread.Add(questionId);
            Answered?.Invoke(q);
            return true;
        }

        public void Note(CodexHand hand, string scene, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            scene = scene ?? string.Empty;
            string clean = text.Trim();
            foreach (CodexNoteRecord n in notes)
            {
                if (n.Scene == scene && n.Text == clean) return;
            }
            EnsureGroup(scene);
            var rec = new CodexNoteRecord { Hand = hand, Scene = scene, Text = clean };
            notes.Add(rec);
            Noted?.Invoke(rec);
        }

        private void EnsureGroup(string scene)
        {
            string token = "?" + (scene ?? string.Empty);
            if (!timeline.Contains(token)) timeline.Add(token);
        }

        /// <summary>Mark an entry, revision, epithet or answered question as read (ids never collide across kinds).</summary>
        public void MarkRead(string id)
        {
            unread.Remove(id);
            revisionsUnread.Remove(id);
            epithetsUnread.Remove(id);
            answeredUnread.Remove(id);
        }

        public void MarkAllRead()
        {
            unread.Clear();
            revisionsUnread.Clear();
            epithetsUnread.Clear();
            answeredUnread.Clear();
        }

        public string Save()
        {
            var sb = new StringBuilder("codex2");
            foreach (string t in timeline)
            {
                sb.Append('\n');
                if (t.StartsWith("?", StringComparison.Ordinal)) sb.Append("@G ").Append(t.Substring(1));
                else sb.Append(unread.Contains(t) ? "*" : "").Append(t);
            }
            foreach (string r in revisions) sb.Append('\n').Append(revisionsUnread.Contains(r) ? "@R* " : "@R ").Append(r);
            foreach (string e in epithets) sb.Append('\n').Append(epithetsUnread.Contains(e) ? "@E* " : "@E ").Append(e);
            foreach (string q in asked) sb.Append('\n').Append("@Q ").Append(q);
            foreach (string q in answered) sb.Append('\n').Append(answeredUnread.Contains(q) ? "@A* " : "@A ").Append(q);
            foreach (CodexNoteRecord n in notes)
            {
                sb.Append('\n').Append("@N ").Append(n.Hand == CodexHand.Anpu ? "anpu" : "ankhu").Append('|')
                  .Append(Escape(n.Scene)).Append('|').Append(Escape(n.Text));
            }
            return sb.ToString();
        }

        public bool Load(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return false;
            string[] lines = payload.Split('\n');
            if (lines.Length == 0) return false;
            string head = lines[0].Trim();
            if (head != "codex1" && head != "codex2") return false;
            Clear();
            foreach (string raw in lines.Skip(1))
            {
                string line = raw.TrimEnd('\r');
                if (line.Trim().Length == 0) continue;
                if (line.StartsWith("@", StringComparison.Ordinal))
                {
                    LoadDirective(line);
                    continue;
                }
                line = line.Trim();
                bool isUnread = line[0] == '*';
                string id = isUnread ? line.Substring(1) : line;
                if (CodexCatalog.Get(id) == null || seen.Contains(id)) continue;
                seen.Add(id);
                order.Add(id);
                timeline.Add(id);
                if (isUnread) unread.Add(id);
            }
            return true;
        }

        private void LoadDirective(string line)
        {
            if (line.Length < 3) return;
            char k = line[1];
            bool star = line[2] == '*';
            int sp = line.IndexOf(' ');
            string arg = sp >= 0 ? line.Substring(sp + 1) : string.Empty;
            switch (k)
            {
                case 'G':
                    if (!timeline.Contains("?" + arg)) timeline.Add("?" + arg);
                    break;
                case 'R':
                    if (CodexRollCatalog.Revision(arg) != null && !revisions.Contains(arg))
                    {
                        revisions.Add(arg);
                        if (star) revisionsUnread.Add(arg);
                    }
                    break;
                case 'E':
                    if (CodexRollCatalog.Epithet(arg) != null && !epithets.Contains(arg))
                    {
                        epithets.Add(arg);
                        if (star) epithetsUnread.Add(arg);
                    }
                    break;
                case 'Q':
                    if (CodexRollCatalog.Question(arg) != null && !asked.Contains(arg)) asked.Add(arg);
                    break;
                case 'A':
                    if (CodexRollCatalog.Question(arg) != null && answered.Add(arg) && star) answeredUnread.Add(arg);
                    break;
                case 'N':
                {
                    string[] parts = arg.Split('|');
                    if (parts.Length < 3) break;
                    notes.Add(new CodexNoteRecord
                    {
                        Hand = parts[0] == "anpu" ? CodexHand.Anpu : CodexHand.Ankhu,
                        Scene = Unescape(parts[1]),
                        Text = Unescape(parts[2]),
                    });
                    break;
                }
            }
        }

        private static string Escape(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("|", "\\p").Replace("\n", "\\n");

        private static string Unescape(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    sb.Append(n == 'p' ? '|' : n == 'n' ? '\n' : n);
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public void Clear()
        {
            order.Clear(); seen.Clear(); unread.Clear();
            timeline.Clear();
            revisions.Clear(); revisionsUnread.Clear();
            epithets.Clear(); epithetsUnread.Clear();
            asked.Clear(); answered.Clear(); answeredUnread.Clear();
            notes.Clear();
        }
    }
}
