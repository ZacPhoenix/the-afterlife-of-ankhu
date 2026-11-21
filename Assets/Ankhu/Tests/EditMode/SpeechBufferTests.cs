using Ankhu.Speech;
using NUnit.Framework;

namespace Ankhu.Tests
{
    public sealed class SpeechBufferTests
    {
        [Test]
        public void StartsEmptyAndCannotSubmit()
        {
            var buffer = new SpeechBuffer();
            Assert.AreEqual(string.Empty, buffer.Text);
            Assert.IsTrue(buffer.IsEmpty);
            Assert.IsFalse(buffer.CanSubmit);
            Assert.AreEqual(SpeechBuffer.DefaultMaxLength, buffer.MaxLength);
            Assert.AreEqual(64, buffer.MaxLength);
        }

        [Test]
        public void AppendAddsPrintableCharacters()
        {
            var buffer = new SpeechBuffer();
            Assert.IsTrue(buffer.Append('f'));
            Assert.IsTrue(buffer.Append('l'));
            Assert.AreEqual(3, buffer.Append("ow "));
            Assert.AreEqual("flow ", buffer.Text);
            Assert.IsTrue(buffer.CanSubmit);
        }

        [Test]
        public void AppendStopsAtMaxLength()
        {
            var buffer = new SpeechBuffer(5);
            Assert.AreEqual(5, buffer.Append("abcdefgh"));
            Assert.AreEqual("abcde", buffer.Text);
            Assert.IsTrue(buffer.IsFull);
            Assert.IsFalse(buffer.Append('z'));
            Assert.AreEqual("abcde", buffer.Text);
        }

        [Test]
        public void DefaultCapIsSixtyFour()
        {
            var buffer = new SpeechBuffer();
            buffer.Append(new string('a', 100));
            Assert.AreEqual(64, buffer.Length);
        }

        [TestCase('\r')]
        [TestCase('\n')]
        [TestCase('\t')]
        [TestCase('\b')]
        [TestCase('\u001B')]
        [TestCase('\u007F')]
        [TestCase('\0')]
        [TestCase('﻿')]
        [TestCase('\uD83D')]
        public void AppendRejectsControlAndUnprintableCharacters(char c)
        {
            var buffer = new SpeechBuffer();
            Assert.IsFalse(buffer.Append(c));
            Assert.IsTrue(buffer.IsEmpty);
        }

        [Test]
        public void AppendStringFiltersControlCharacters()
        {
            var buffer = new SpeechBuffer();
            Assert.AreEqual(4, buffer.Append("ri\rs\be"));
            Assert.AreEqual("rise", buffer.Text);
        }

        [Test]
        public void AppendAcceptsSpacesPunctuationAndAccents()
        {
            var buffer = new SpeechBuffer();
            buffer.Append("Ra, rise! é");
            Assert.AreEqual("Ra, rise! é", buffer.Text);
        }

        [Test]
        public void WhitespaceOnlyCannotSubmit()
        {
            var buffer = new SpeechBuffer();
            buffer.Append("   ");
            Assert.IsFalse(buffer.IsEmpty);
            Assert.IsFalse(buffer.CanSubmit);
        }

        [Test]
        public void BackspaceRemovesLastCharacter()
        {
            var buffer = new SpeechBuffer();
            buffer.Append("bind");
            Assert.IsTrue(buffer.Backspace());
            Assert.AreEqual("bin", buffer.Text);
        }

        [Test]
        public void BackspaceOnEmptyDoesNothing()
        {
            var buffer = new SpeechBuffer();
            Assert.IsFalse(buffer.Backspace());
            Assert.AreEqual(string.Empty, buffer.Text);
            buffer.Append('a');
            Assert.IsTrue(buffer.Backspace());
            Assert.IsFalse(buffer.Backspace());
            Assert.IsTrue(buffer.IsEmpty);
        }

        [Test]
        public void BackspaceAfterCapAllowsTypingAgain()
        {
            var buffer = new SpeechBuffer(3);
            buffer.Append("abc");
            buffer.Backspace();
            Assert.IsTrue(buffer.Append('z'));
            Assert.AreEqual("abz", buffer.Text);
        }

        [Test]
        public void ClearEmptiesTheBuffer()
        {
            var buffer = new SpeechBuffer();
            buffer.Append("release bridge");
            buffer.Clear();
            Assert.AreEqual(string.Empty, buffer.Text);
            Assert.IsFalse(buffer.CanSubmit);
            buffer.Clear();
            Assert.IsTrue(buffer.IsEmpty);
        }

        [Test]
        public void SetTextReplacesAndFiltersAndCaps()
        {
            var buffer = new SpeechBuffer(6);
            buffer.Append("old");
            buffer.SetText("new\tword here");
            Assert.AreEqual("newwor", buffer.Text);
        }

        [Test]
        public void ConstructorRejectsZeroLength()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SpeechBuffer(0));
        }
    }
}
