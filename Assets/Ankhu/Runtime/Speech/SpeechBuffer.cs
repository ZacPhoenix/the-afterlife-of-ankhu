using System;
using System.Text;

namespace Ankhu.Speech
{
    /// <summary>
    /// Editable text of one utterance in the speech bubble. Pure C# with no engine dependency
    /// so it can be unit tested. Only printable characters are accepted and the length is capped.
    /// </summary>
    public sealed class SpeechBuffer
    {
        public const int DefaultMaxLength = 64;

        private readonly StringBuilder builder;
        private string cachedText = string.Empty;
        private bool dirty;

        public SpeechBuffer(int maxLength = DefaultMaxLength)
        {
            if (maxLength < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLength), "Max length must be at least 1.");
            }
            MaxLength = maxLength;
            builder = new StringBuilder(maxLength);
        }

        public int MaxLength { get; }

        public int Length => builder.Length;

        public bool IsEmpty => builder.Length == 0;

        public bool IsFull => builder.Length >= MaxLength;

        public string Text
        {
            get
            {
                if (dirty)
                {
                    cachedText = builder.ToString();
                    dirty = false;
                }
                return cachedText;
            }
        }

        /// <summary>True when the text holds at least one non whitespace character.</summary>
        public bool CanSubmit
        {
            get
            {
                for (int i = 0; i < builder.Length; i++)
                {
                    if (!char.IsWhiteSpace(builder[i]))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// True for characters the bubble can show: no control characters (Enter, Tab, Backspace,
        /// Escape, Delete), no lone surrogate halves and no Unicode noncharacters or byte order marks.
        /// </summary>
        public static bool IsPrintable(char c)
        {
            if (char.IsControl(c) || char.IsSurrogate(c))
            {
                return false;
            }
            return c != '﻿' && c != '￾' && c != '￿';
        }

        /// <summary>Appends one character. Returns false if it was filtered or the buffer is full.</summary>
        public bool Append(char c)
        {
            if (!IsPrintable(c) || IsFull)
            {
                return false;
            }
            builder.Append(c);
            dirty = true;
            return true;
        }

        /// <summary>Appends each accepted character of a string. Returns how many were added.</summary>
        public int Append(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            int added = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (Append(text[i]))
                {
                    added++;
                }
            }
            return added;
        }

        /// <summary>Removes the last character. Returns false when the buffer was already empty.</summary>
        public bool Backspace()
        {
            if (builder.Length == 0)
            {
                return false;
            }
            builder.Length -= 1;
            dirty = true;
            return true;
        }

        public void Clear()
        {
            if (builder.Length == 0)
            {
                return;
            }
            builder.Length = 0;
            dirty = true;
        }

        /// <summary>Replaces the content, applying the same filter and cap as typing.</summary>
        public void SetText(string text)
        {
            Clear();
            Append(text);
        }

        public override string ToString() => Text;
    }
}
