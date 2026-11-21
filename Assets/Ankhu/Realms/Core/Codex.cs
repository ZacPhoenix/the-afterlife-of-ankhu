using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ankhu.Realms.Core
{
    public enum CodexKind
    {
        /// <summary>A word of power. Shown on the first two pages, the spell pages.</summary>
        Word,
        /// <summary>A name Ankhu has recovered (his own, a god's, a gate's).</summary>
        Name,
        /// <summary>Knowledge about the Duat: a place, a god, a rule of the land.</summary>
        Lore,
        /// <summary>A puzzle clue he wrote down.</summary>
        Clue,
        /// <summary>A memory of his life that returned.</summary>
        Memory
    }

    /// <summary>One thing Ankhu writes in the codex. Ids are stable ("word.open", "lore.wernes").</summary>
    public sealed class CodexEntry
    {
        public string Id;
        public CodexKind Kind;
        public string Title;
        /// <summary>Gardiner codes for the heading glyphs, space separated. May be empty.</summary>
        public string Signs = string.Empty;
        public string Transliteration = string.Empty;
        /// <summary>The body text in Ankhu's own voice, first person, past tense or present.</summary>
        public string Body = string.Empty;
        /// <summary>For words: what the word does, one line ("Opens what is shut: doors, shrines, mouths.").</summary>
        public string Effect = string.Empty;
        /// <summary>Realm where it was written (for the page header).</summary>
        public string Realm = string.Empty;
        /// <summary>Optional extra lines appended when a later event deepens the entry.</summary>
        public List<string> Addenda = new List<string>();
    }

    /// <summary>
    /// The entries the game can write, keyed by id. Content lives in <see cref="CodexContent"/>.
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
                }
                return all;
            }
        }

        public static CodexEntry Get(string id) => id != null && All.TryGetValue(id, out CodexEntry e) ? e : null;
    }

    /// <summary>
    /// What Ankhu has written so far, in the order he wrote it. Serializes to a small line format so the
    /// engine-free core needs no JSON library. The Unity side persists the string (PlayerPrefs or a file).
    /// </summary>
    public sealed class CodexJournal
    {
        private readonly List<string> order = new List<string>();
        private readonly HashSet<string> seen = new HashSet<string>();
        private readonly HashSet<string> unread = new HashSet<string>();
        private readonly Dictionary<string, int> addendaShown = new Dictionary<string, int>();

        public event Action<CodexEntry> Written;

        public IReadOnlyList<string> Order => order;
        public bool Has(string id) => seen.Contains(id);
        public bool IsUnread(string id) => unread.Contains(id);
        public int UnreadCount => unread.Count;

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
            unread.Add(id);
            Written?.Invoke(e);
            return true;
        }

        public void MarkRead(string id) => unread.Remove(id);
        public void MarkAllRead() => unread.Clear();

        public string Save()
        {
            var sb = new StringBuilder("codex1");
            foreach (string id in order) sb.Append('\n').Append(unread.Contains(id) ? "*" : "").Append(id);
            return sb.ToString();
        }

        public bool Load(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return false;
            string[] lines = payload.Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != "codex1") return false;
            order.Clear(); seen.Clear(); unread.Clear();
            foreach (string raw in lines.Skip(1))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                bool isUnread = line[0] == '*';
                string id = isUnread ? line.Substring(1) : line;
                if (CodexCatalog.Get(id) == null || seen.Contains(id)) continue;
                seen.Add(id);
                order.Add(id);
                if (isUnread) unread.Add(id);
            }
            return true;
        }

        public void Clear() { order.Clear(); seen.Clear(); unread.Clear(); }
    }
}
