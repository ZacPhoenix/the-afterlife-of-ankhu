using System.Collections.Generic;
using Ankhu.Core;
using Ankhu.Integration;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Player
{
    /// <summary>Shows the most recent FeedbackBus lines bottom-left, newest at the bottom, coloured by outcome.</summary>
    [DisallowMultipleComponent]
    public sealed class FeedbackLog : MonoBehaviour
    {
        public const int LineCount = 5;

        private struct Entry
        {
            public FeedbackSource Source;
            public CommandOutcome Outcome;
            public string Message;
        }

        [Tooltip("Oldest line first. The newest message goes in the last element.")]
        [SerializeField] private Text[] lines = new Text[0];

        private readonly List<Entry> entries = new List<Entry>(LineCount);

        public int EntryCount => entries.Count;

        public void Configure(Text[] lineTexts)
        {
            lines = lineTexts;
        }

        private void OnEnable()
        {
            FeedbackBus.Published += HandlePublished;
            Redraw();
        }

        private void OnDisable()
        {
            FeedbackBus.Published -= HandlePublished;
        }

        public void ClearLog()
        {
            entries.Clear();
            Redraw();
        }

        private void HandlePublished(FeedbackSource source, CommandOutcome outcome, string message)
        {
            entries.Add(new Entry { Source = source, Outcome = outcome, Message = message });
            int capacity = Mathf.Max(1, lines != null && lines.Length > 0 ? lines.Length : LineCount);
            while (entries.Count > capacity)
            {
                entries.RemoveAt(0);
            }
            Redraw();
        }

        private static string Prefix(FeedbackSource source)
        {
            switch (source)
            {
                case FeedbackSource.Speech: return "» ";
                case FeedbackSource.Inspect: return "· ";
                case FeedbackSource.Hint: return "? ";
                default: return string.Empty;
            }
        }

        private void Redraw()
        {
            if (lines == null)
            {
                return;
            }
            int slots = lines.Length;
            int offset = slots - entries.Count;
            for (int i = 0; i < slots; i++)
            {
                Text line = lines[i];
                if (line == null)
                {
                    continue;
                }
                int index = i - offset;
                if (index < 0)
                {
                    line.text = string.Empty;
                    continue;
                }
                Entry entry = entries[index];
                line.text = Prefix(entry.Source) + entry.Message;
                Color color = FeedbackPalette.For(entry.Outcome);
                // Older lines fade a little so the newest reads first.
                int age = entries.Count - 1 - index;
                color.a = Mathf.Lerp(1f, 0.55f, age / (float)Mathf.Max(1, slots - 1));
                line.color = color;
            }
        }
    }
}
