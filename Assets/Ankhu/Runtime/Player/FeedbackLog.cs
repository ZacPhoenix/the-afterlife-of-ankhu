using System.Collections.Generic;
using Ankhu.Core;
using Ankhu.Integration;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Player
{
    /// <summary>
    /// Shows the most recent FeedbackBus lines bottom-left on a dark backing panel, newest at the
    /// bottom, coloured by outcome. Lines wrap inside the panel width and expire after a few seconds,
    /// so stale text never sits over the scene. The panel hides when there is nothing to show.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FeedbackLog : MonoBehaviour
    {
        public const int LineCount = 4;
        public const float LineLifetimeSeconds = 9f;
        public const float ReadingLifetimeSeconds = 16f;

        private struct Entry
        {
            public FeedbackSource Source;
            public CommandOutcome Outcome;
            public string Message;
            public float ExpiresAt;
        }

        [Tooltip("Oldest line first. The newest message goes in the last element.")]
        [SerializeField] private Text[] lines = new Text[0];
        [Tooltip("Backing panel behind the lines. Hidden while the log is empty.")]
        [SerializeField] private GameObject backing;

        private readonly List<Entry> entries = new List<Entry>(LineCount);

        public int EntryCount => entries.Count;

        public void Configure(Text[] lineTexts, GameObject backingPanel)
        {
            lines = lineTexts;
            backing = backingPanel;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            bool removed = false;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].ExpiresAt <= now)
                {
                    entries.RemoveAt(i);
                    removed = true;
                }
            }
            if (removed)
            {
                Redraw();
            }
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
            // Drop an identical line so repeated attempts do not fill the log with copies.
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].Message == message && entries[i].Outcome == outcome)
                {
                    entries.RemoveAt(i);
                }
            }
            bool reading = source == FeedbackSource.Inspect || source == FeedbackSource.Hint;
            float lifetime = reading ? ReadingLifetimeSeconds : LineLifetimeSeconds;
            entries.Add(new Entry { Source = source, Outcome = outcome, Message = message, ExpiresAt = Time.unscaledTime + lifetime });
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
            if (backing != null && backing.activeSelf != (entries.Count > 0))
            {
                backing.SetActive(entries.Count > 0);
            }
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
                    line.gameObject.SetActive(false);
                    continue;
                }
                line.gameObject.SetActive(true);
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
