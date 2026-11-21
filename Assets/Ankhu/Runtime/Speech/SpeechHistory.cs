using System;
using System.Collections.Generic;

namespace Ankhu.Speech
{
    /// <summary>
    /// Recently submitted utterances, oldest first, for Up and Down arrow recall. Pure C# with no
    /// engine dependency. Browsing remembers the unsent draft and returns it after the newest entry.
    /// </summary>
    public sealed class SpeechHistory
    {
        public const int DefaultCapacity = 10;

        private readonly List<string> entries;
        private int cursor;
        private string draft;

        public SpeechHistory(int capacity = DefaultCapacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 1.");
            }
            Capacity = capacity;
            entries = new List<string>(capacity);
            cursor = 0;
        }

        public int Capacity { get; }

        public int Count => entries.Count;

        /// <summary>Entry by age, 0 is the oldest kept utterance.</summary>
        public string this[int index] => entries[index];

        /// <summary>True while Up has moved the cursor onto a stored entry.</summary>
        public bool IsBrowsing => cursor < entries.Count;

        /// <summary>
        /// Records a submitted utterance. Blank text is ignored and an exact repeat of the newest
        /// entry is not stored twice. The oldest entry drops out beyond <see cref="Capacity"/>.
        /// Browsing restarts from the newest entry.
        /// </summary>
        public void Push(string utterance)
        {
            if (!string.IsNullOrWhiteSpace(utterance))
            {
                bool repeat = entries.Count > 0 && string.Equals(entries[entries.Count - 1], utterance, StringComparison.Ordinal);
                if (!repeat)
                {
                    entries.Add(utterance);
                    while (entries.Count > Capacity)
                    {
                        entries.RemoveAt(0);
                    }
                }
            }
            ResetCursor();
        }

        /// <summary>
        /// Steps one entry back in time and returns it. The first step stores
        /// <paramref name="currentText"/> as the draft. Returns null when there is no history.
        /// At the oldest entry it keeps returning the oldest entry.
        /// </summary>
        public string Previous(string currentText)
        {
            if (entries.Count == 0)
            {
                return null;
            }
            if (!IsBrowsing)
            {
                draft = currentText ?? string.Empty;
            }
            if (cursor > 0)
            {
                cursor--;
            }
            return entries[cursor];
        }

        /// <summary>
        /// Steps one entry forward. Past the newest entry it returns the saved draft and stops
        /// browsing. Returns null when not browsing.
        /// </summary>
        public string Next()
        {
            if (!IsBrowsing)
            {
                return null;
            }
            cursor++;
            if (cursor >= entries.Count)
            {
                cursor = entries.Count;
                string restored = draft ?? string.Empty;
                draft = null;
                return restored;
            }
            return entries[cursor];
        }

        /// <summary>Stops browsing without changing the stored entries.</summary>
        public void ResetCursor()
        {
            cursor = entries.Count;
            draft = null;
        }

        public void Clear()
        {
            entries.Clear();
            ResetCursor();
        }
    }
}
