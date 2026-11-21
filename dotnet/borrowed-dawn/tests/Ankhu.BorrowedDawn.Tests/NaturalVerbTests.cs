using System.Linq;
using Ankhu.BorrowedDawn;
using NUnit.Framework;

namespace Ankhu.BorrowedDawn.Tests
{
    /// <summary>Aliases and feedback added after the first Unity playtest.</summary>
    [TestFixture]
    public class NaturalVerbTests
    {
        private static readonly string[] TakeVerbs = { "take", "carry", "grab", "lift", "pick up", "collect" };
        private static readonly string[] PlaceVerbs = { "install", "place", "put", "set", "insert", "mount" };
        private static readonly string[] MirrorNouns = { "mirror", "sun mirror", "the mirror", "the sun mirror" };
        private static readonly string[] SocketSuffixes = { "", " in socket", " into the socket", " on the socket", " onto socket", " in the sun socket", " into sun socket" };

        private static readonly object[] TakeCases =
            TakeVerbs.SelectMany(v => MirrorNouns.Select(n => (object)(v + " " + n))).ToArray();

        private static readonly object[] PlaceCases =
            PlaceVerbs.SelectMany(v => MirrorNouns.SelectMany(n => SocketSuffixes.Select(s => (object)(v + " " + n + s)))).ToArray();

        [TestCaseSource(nameof(TakeCases))]
        public void TakeAliases_ParseToTakeMirror(string text)
        {
            ParseResult r = SpellParser.Parse(text);
            Assert.That(r.Status, Is.EqualTo(ParseStatus.Ok), r.Feedback);
            Assert.That(r.Command, Is.EqualTo(Command.TakeMirror));
        }

        [TestCaseSource(nameof(PlaceCases))]
        public void PlaceAliases_ParseToInstallMirror(string text)
        {
            ParseResult r = SpellParser.Parse(text);
            Assert.That(r.Status, Is.EqualTo(ParseStatus.Ok), r.Feedback);
            Assert.That(r.Command, Is.EqualTo(Command.InstallMirror));
        }

        [TestCase("Carry the mirror!")]
        [TestCase("  PUT   the Mirror, in the Socket.  ")]
        public void Aliases_SurviveCaseAndPunctuation(string text)
        {
            Assert.That(SpellParser.Parse(text).Success, Is.True);
        }

        [TestCase("carry", "take")]
        [TestCase("take", "take")]
        [TestCase("grab", "take")]
        [TestCase("place", "install")]
        [TestCase("put", "install")]
        [TestCase("insert the", "install")]
        public void BareVerb_IsMissingArgument_WithSpokenVerbInFeedback(string text, string canonical)
        {
            ParseResult r = SpellParser.Parse(text);
            Assert.That(r.Status, Is.EqualTo(ParseStatus.MissingArgument));
            Assert.That(r.Verb, Is.EqualTo(canonical));
            string spoken = text.Replace(" the", "");
            Assert.That(r.SpokenVerb, Is.EqualTo(spoken));
            Assert.That(r.Feedback, Does.StartWith(char.ToUpperInvariant(spoken[0]) + spoken.Substring(1) + " what?"));
            Assert.That(r.Feedback, Does.Contain("mirror"));
        }

        [Test]
        public void PutMirrorOnBell_IsTrailingWords()
        {
            ParseResult r = SpellParser.Parse("put mirror on bell");
            Assert.That(r.Status, Is.EqualTo(ParseStatus.TrailingWords));
            Assert.That(r.OffendingText, Is.EqualTo("on"));
        }

        [Test]
        public void CarryRunsTheMirrorStepInSession()
        {
            var session = new PuzzleSession(RuleProfile.Greybox);
            foreach (string step in new[] { "flow west", "board elevator", "flow east", "bind elevator", "flow west" })
                Assert.That(session.Submit(step).Accepted, Is.True, step);
            Assert.That(session.Submit("carry the mirror").Accepted, Is.True);
            Assert.That(session.State.Mirror, Is.EqualTo(MirrorPlace.Carried));
        }

        [TestCase("flwo west", "flow")]
        [TestCase("bnid shadow", "bind")]
        [TestCase("releas elevator", "release")]
        [TestCase("crry mirror", "carry")]
        public void UnknownVerb_SuggestsNearestVerb(string text, string suggestion)
        {
            ParseResult r = SpellParser.Parse(text);
            Assert.That(r.Status, Is.EqualTo(ParseStatus.UnknownVerb));
            Assert.That(r.Suggestion, Is.EqualTo(suggestion));
            Assert.That(r.Feedback, Does.Contain("Did you mean “" + suggestion + "”?"));
        }

        [TestCase("dance")]
        [TestCase("he who returns the dawn")]
        [TestCase("xyzzy")]
        public void UnknownVerb_FarFromEverything_HasNoSuggestion(string text)
        {
            ParseResult r = SpellParser.Parse(text);
            Assert.That(r.Status, Is.EqualTo(ParseStatus.UnknownVerb));
            Assert.That(r.Suggestion, Is.Null);
            Assert.That(r.Feedback, Does.Not.Contain("Did you mean"));
        }

        [Test]
        public void UnknownFeedback_NeverListsSolutionSteps()
        {
            foreach (string text in new[] { "fly west", "dance", "hello", "open gate", "xyzzy", "flwo west" })
            {
                string fb = SpellParser.Parse(text).Feedback.ToLowerInvariant();
                foreach (string leak in new[] { "shadow", "bearer", "bell", "west", "elevator", "returns", "morning" })
                {
                    if (text.Contains(leak)) continue;
                    Assert.That(fb, Does.Not.Contain(leak), $"{text} -> {fb}");
                }
            }
        }

        [Test]
        public void Greybox_UnreadName_MatchesNonsenseFeedback()
        {
            var gate = new RoomState(WaterSide.East, Location.Gate, BindingSet.Bridge, MirrorPlace.Raft, false, false, false, false, false, false);
            string ruleMessage = RoomRules.Apply(gate, Command.SpeakName, RuleProfile.Greybox).Message;
            string nonsense = SpellParser.Parse("he who returns the dawn").Feedback;
            Assert.That(ruleMessage, Is.EqualTo(nonsense));
        }
    }
}
