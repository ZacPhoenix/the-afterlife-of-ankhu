using Ankhu.Speech;
using NUnit.Framework;

namespace Ankhu.Tests
{
    public sealed class SpeechHistoryTests
    {
        [Test]
        public void PreviousOnEmptyHistoryReturnsNull()
        {
            var history = new SpeechHistory();
            Assert.IsNull(history.Previous("draft"));
            Assert.IsNull(history.Next());
            Assert.IsFalse(history.IsBrowsing);
        }

        [Test]
        public void PreviousWalksBackAndStopsAtOldest()
        {
            var history = new SpeechHistory();
            history.Push("flow east");
            history.Push("bind lift");
            history.Push("wake");
            Assert.AreEqual("wake", history.Previous(string.Empty));
            Assert.AreEqual("bind lift", history.Previous(string.Empty));
            Assert.AreEqual("flow east", history.Previous(string.Empty));
            Assert.AreEqual("flow east", history.Previous(string.Empty));
        }

        [Test]
        public void NextReturnsToTheDraft()
        {
            var history = new SpeechHistory();
            history.Push("one");
            history.Push("two");
            Assert.AreEqual("two", history.Previous("half typed"));
            Assert.AreEqual("one", history.Previous("ignored while browsing"));
            Assert.AreEqual("two", history.Next());
            Assert.AreEqual("half typed", history.Next());
            Assert.IsFalse(history.IsBrowsing);
            Assert.IsNull(history.Next());
        }

        [Test]
        public void KeepsOnlyTheLastTen()
        {
            var history = new SpeechHistory();
            for (int i = 0; i < 15; i++)
            {
                history.Push("word " + i);
            }
            Assert.AreEqual(10, history.Count);
            Assert.AreEqual("word 5", history[0]);
            Assert.AreEqual("word 14", history[9]);
        }

        [Test]
        public void IgnoresBlankAndImmediateRepeats()
        {
            var history = new SpeechHistory();
            history.Push("ring");
            history.Push("ring");
            history.Push("   ");
            history.Push(null);
            history.Push("ring again");
            history.Push("ring");
            Assert.AreEqual(3, history.Count);
        }

        [Test]
        public void PushStopsBrowsing()
        {
            var history = new SpeechHistory();
            history.Push("a");
            history.Push("b");
            history.Previous(string.Empty);
            Assert.IsTrue(history.IsBrowsing);
            history.Push("c");
            Assert.IsFalse(history.IsBrowsing);
            Assert.AreEqual("c", history.Previous(string.Empty));
        }

        [Test]
        public void ResetCursorStartsFromNewestAgain()
        {
            var history = new SpeechHistory();
            history.Push("a");
            history.Push("b");
            history.Previous(string.Empty);
            history.Previous(string.Empty);
            history.ResetCursor();
            Assert.AreEqual("b", history.Previous(string.Empty));
        }
    }
}
