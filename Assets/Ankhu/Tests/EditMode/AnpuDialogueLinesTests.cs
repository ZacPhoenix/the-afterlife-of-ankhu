using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Ankhu.BorrowedDawn;
using NUnit.Framework;

namespace Ankhu.Tests
{
    /// <summary>
    /// tools/voice/anpu_lines.json is what gets recorded for Anpu. It must match the dialogue the teaching rooms
    /// actually show, key for key and word for word, or a recorded line would play over different subtitle text.
    /// Regenerate the file (and the changed takes) when TeachingRooms changes.
    /// </summary>
    public sealed class AnpuDialogueLinesTests
    {
        private static Dictionary<string, string> LoadLines()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "tools", "voice", "anpu_lines.json");
            if (!File.Exists(path))
            {
                Assert.Ignore("tools/voice/anpu_lines.json not found from " + Directory.GetCurrentDirectory());
            }
            var lines = new Dictionary<string, string>();
            var entry = new Regex("\"key\"\\s*:\\s*\"(?<k>[^\"]+)\"\\s*,\\s*\"text\"\\s*:\\s*\"(?<t>(?:[^\"\\\\]|\\\\.)*)\"");
            foreach (Match m in entry.Matches(File.ReadAllText(path)))
            {
                lines[m.Groups["k"].Value] = Regex.Unescape(m.Groups["t"].Value);
            }
            return lines;
        }

        private static IEnumerable<KeyValuePair<string, string>> RoomLines()
        {
            foreach (RoomDefinition room in TeachingRooms.Sequence)
            {
                string prefix = "dialogue.anpu." + room.Id + ".";
                for (int i = 0; i < room.AnpuIntro.Count; i++)
                {
                    yield return new KeyValuePair<string, string>(prefix + "intro." + (i + 1), room.AnpuIntro[i]);
                }
                foreach (KeyValuePair<Outcome, string> nudge in room.AnpuNudges)
                {
                    yield return new KeyValuePair<string, string>(prefix + "nudge." + nudge.Key, nudge.Value);
                }
                if (!string.IsNullOrEmpty(room.AnpuOnSolve))
                {
                    yield return new KeyValuePair<string, string>(prefix + "solve", room.AnpuOnSolve);
                }
            }
        }

        [Test]
        public void EveryTeachingRoomLineHasARecordingEntryWithTheSameText()
        {
            Dictionary<string, string> lines = LoadLines();
            foreach (KeyValuePair<string, string> line in RoomLines())
            {
                Assert.That(lines.ContainsKey(line.Key), "missing from anpu_lines.json: " + line.Key);
                Assert.That(lines[line.Key], Is.EqualTo(line.Value), line.Key);
            }
        }

        [Test]
        public void CinematicRiseLineIsListed()
        {
            Assert.That(LoadLines()["dialogue.anpu.spell25.rise"], Is.EqualTo("Rise, Ankhu."));
        }
    }
}
