using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Ankhu.BorrowedDawn.Tests
{
    [TestFixture]
    public class ParserTests
    {
        private static void AssertParses(string text, Command expected)
        {
            ParseResult r = SpellParser.Parse(text);
            Assert.That(r.Status, Is.EqualTo(ParseStatus.Ok), $"'{text}' -> {r.Status} ({r.Feedback})");
            Assert.That(r.Success, Is.True);
            Assert.That(r.Command, Is.EqualTo(expected), $"'{text}'");
            Assert.That(r.Feedback, Is.Empty);
        }

        public static IEnumerable<TestCaseData> AcceptedSpellings()
        {
            TestCaseData C(string text, Command c) => new TestCaseData(text, c).SetName($"Parse_Ok({text.Replace(".", "․")})");

            // Case, whitespace, punctuation, quotes, articles.
            yield return C("FLOW WEST", Command.Flow(WaterSide.West));
            yield return C("  flow    east  ", Command.Flow(WaterSide.East));
            yield return C("\tflow\nwest\r\n", Command.Flow(WaterSide.West));
            yield return C("flow, west.", Command.Flow(WaterSide.West));
            yield return C("bind THE  elevator!!", Command.Bind(BindTarget.Elevator));
            yield return C("bind a shadow", Command.Bind(BindTarget.Shadow));
            yield return C("bind an bridge?", Command.Bind(BindTarget.Bridge));
            yield return C("“flow west”", Command.Flow(WaterSide.West));
            yield return C("‘wake bearer’", Command.WakeBearer);
            yield return C("\"strike bell\"", Command.StrikeBell);
            yield return C("flow w", Command.Flow(WaterSide.West));
            yield return C("flow e", Command.Flow(WaterSide.East));

            // Full-width characters via NFKC.
            yield return C("ｆｌｏｗ ｗｅｓｔ", Command.Flow(WaterSide.West));
            yield return C("ＢＩＮＤ　ＳＨＡＤＯＷ", Command.Bind(BindTarget.Shadow));

            // Aliases.
            yield return C("bind lift", Command.Bind(BindTarget.Elevator));
            yield return C("bind pendulum", Command.Bind(BindTarget.Bridge));
            yield return C("release pendulum", Command.Release(BindTarget.Bridge));
            yield return C("unbind lift", Command.Release(BindTarget.Elevator));
            yield return C("unbind shadow", Command.Release(BindTarget.Shadow));
            yield return C("wake statue", Command.WakeBearer);
            yield return C("awaken bearer", Command.WakeBearer);
            yield return C("awaken the statue", Command.WakeBearer);
            yield return C("sleep statue", Command.SleepBearer);
            yield return C("recall resonator", Command.RecallBell);
            yield return C("ring bell", Command.StrikeBell);
            yield return C("ring the resonator", Command.StrikeBell);
            yield return C("strike resonator", Command.StrikeBell);
            yield return C("echo resonator", Command.EchoBell);
            yield return C("take sun mirror", Command.TakeMirror);
            yield return C("take the sun-mirror", Command.TakeMirror);
            yield return C("pick up the mirror", Command.TakeMirror);
            yield return C("install sun mirror", Command.InstallMirror);
            yield return C("place mirror", Command.InstallMirror);
            yield return C("read gallery", Command.ReadInscription);
            yield return C("read inscriptions", Command.ReadInscription);
            yield return C("board lift", Command.MoveTo(Location.Lift));
            yield return C("enter balcony", Command.MoveTo(Location.Gallery));
            yield return C("descend", Command.MoveTo(Location.Floor));
            yield return C("descend to ground", Command.MoveTo(Location.Floor));
            yield return C("go to bell landing", Command.MoveTo(Location.Landing));
            yield return C("go to the bell landing", Command.MoveTo(Location.Landing));
            yield return C("go landing", Command.MoveTo(Location.Landing));
            yield return C("walk to gate", Command.MoveTo(Location.Gate));
            yield return C("walk to threshold", Command.MoveTo(Location.Gate));
            yield return C("move to ground", Command.MoveTo(Location.Floor));
            yield return C("go to elevator", Command.MoveTo(Location.Lift));
            yield return C("go to balcony", Command.MoveTo(Location.Gallery));
            yield return C("cross the bridge", Command.CrossBridge);

            // The Name, with and without articles and prefixes.
            yield return C("he who returns the morning", Command.SpeakName);
            yield return C("he who returns morning", Command.SpeakName);
            yield return C("He Who Returns The Morning.", Command.SpeakName);
            yield return C("speak he who returns the morning", Command.SpeakName);
            yield return C("say: “He Who Returns the Morning!”", Command.SpeakName);
            yield return C("SAY he who returns morning", Command.SpeakName);
        }

        [TestCaseSource(nameof(AcceptedSpellings))]
        public void Parse_AcceptedSpelling(string text, Command expected) => AssertParses(text, expected);

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("the")]
        [TestCase("a an the")]
        [TestCase("!?,.")]
        [TestCase(null)]
        public void Parse_Empty(string text)
        {
            ParseResult r = SpellParser.Parse(text);
            Assert.That(r.Status, Is.EqualTo(ParseStatus.Empty));
            Assert.That(r.Command, Is.Null);
            Assert.That(r.Normalized, Is.Empty);
            Assert.That(r.Feedback, Is.Empty);
        }

        [TestCase("bind", "bind", new[] { "elevator", "shadow", "bridge" })]
        [TestCase("flow", "flow", new[] { "west", "east" })]
        [TestCase("wake", "wake", new[] { "bearer" })]
        [TestCase("unbind", "release", new[] { "elevator", "shadow", "bridge" })]
        [TestCase("go to", "go to", new[] { "floor", "elevator", "gallery", "landing", "bridge", "gate" })]
        [TestCase("take the", "take", new[] { "mirror" })]
        public void Parse_MissingArgument(string text, string verb, string[] expected)
        {
            ParseResult r = SpellParser.Parse(text);
            Assert.That(r.Status, Is.EqualTo(ParseStatus.MissingArgument));
            Assert.That(r.Command, Is.Null);
            Assert.That(r.Verb, Is.EqualTo(verb));
            Assert.That(r.ExpectedArguments, Is.EqualTo(expected));
            Assert.That(r.Feedback, Does.Contain(verb));
        }

        [TestCase("bind gate", "bind", "gate")]
        [TestCase("flow north", "flow", "north")]
        [TestCase("wake bell", "wake", "bell")]
        [TestCase("release the mirror", "release", "mirror")]
        [TestCase("go to moon", "go to", "moon")]
        [TestCase("descend alone", "descend", "alone")]
        public void Parse_InvalidArgument(string text, string verb, string offending)
        {
            ParseResult r = SpellParser.Parse(text);
            Assert.That(r.Status, Is.EqualTo(ParseStatus.InvalidArgument));
            Assert.That(r.Command, Is.Null);
            Assert.That(r.Verb, Is.EqualTo(verb));
            Assert.That(r.OffendingText, Is.EqualTo(offending));
            Assert.That(r.ExpectedArguments, Is.Not.Empty);
            Assert.That(r.Feedback, Does.Contain(offending));
        }

        [TestCase("flow west now", "flow", "now")]
        [TestCase("bind shadow please", "bind", "please")]
        [TestCase("cross bridge quickly", "cross", "quickly")]
        [TestCase("go to bell landing now", "go to", "now")]
        public void Parse_TrailingWords(string text, string verb, string offending)
        {
            ParseResult r = SpellParser.Parse(text);
            Assert.That(r.Status, Is.EqualTo(ParseStatus.TrailingWords));
            Assert.That(r.Command, Is.Null);
            Assert.That(r.Verb, Is.EqualTo(verb));
            Assert.That(r.OffendingText, Is.EqualTo(offending));
        }

        [TestCase("fly west", "fly")]
        [TestCase("he who returns the dawn", "he")]
        [TestCase("he who returns", "he")]
        [TestCase("speak he who returns the dawn", "speak")]
        [TestCase("say", "say")]
        [TestCase("help", "help")]
        public void Parse_UnknownVerb(string text, string offending)
        {
            ParseResult r = SpellParser.Parse(text);
            Assert.That(r.Status, Is.EqualTo(ParseStatus.UnknownVerb));
            Assert.That(r.Command, Is.Null);
            Assert.That(r.Verb, Is.Null);
            Assert.That(r.OffendingText, Is.EqualTo(offending));
            Assert.That(r.ExpectedArguments, Is.Empty);
        }

        [Test]
        public void Parse_NormalizedText_DropsArticlesAndPunctuation()
        {
            Assert.That(SpellParser.Parse("Bind THE  Elevator!!").Normalized, Is.EqualTo("bind elevator"));
            Assert.That(SpellParser.Tokenize("  Go to the BELL-landing "), Is.EqualTo(new[] { "go", "to", "bell", "landing" }));
        }

        public static IEnumerable<Command> AllCommands() => RoomRules.AllCommands;

        [TestCaseSource(nameof(AllCommands))]
        public void RoundTrip_ToStringParsesBackToEqualCommand(Command cmd)
        {
            ParseResult r = SpellParser.Parse(cmd.ToString());
            Assert.That(r.Success, Is.True, $"'{cmd}' -> {r.Status}");
            Assert.That(r.Command, Is.EqualTo(cmd));
            Assert.That(r.Command.GetHashCode(), Is.EqualTo(cmd.GetHashCode()));
        }

        [Test]
        public void AllCommands_AreDistinct()
        {
            Assert.That(RoomRules.AllCommands.Distinct().Count(), Is.EqualTo(RoomRules.AllCommands.Count));
            Assert.That(RoomRules.AllCommands.Select(c => c.ToString()).Distinct().Count(), Is.EqualTo(RoomRules.AllCommands.Count));
        }

        private static readonly string[] FailingInputs =
        {
            "bind", "flow", "wake", "sleep", "recall", "strike", "ring", "echo", "take", "pick up", "install",
            "place", "read", "board", "enter", "cross", "go", "go to", "walk to", "move to", "release", "unbind",
            "bind gate", "flow north", "wake bell", "read the bell", "enter gate", "board bridge",
            "flow west now", "he", "he who", "he who returns the dawn", "he who the morning returns ",
            "speak", "say", "say the", "speak he who", "fly west", "open gate", "speak name", "say name",
            "speak the name", "name", "gate", "xyzzy", "help", "hint",
        };

        [Test]
        public void FailureFeedback_NeverLeaksTheName()
        {
            foreach (string text in FailingInputs)
            {
                ParseResult r = SpellParser.Parse(text);
                Assert.That(r.Success, Is.False, text);
                string fb = r.Feedback.ToLowerInvariant();
                Assert.That(fb, Does.Not.Contain("returns"), text);
                Assert.That(fb, Does.Not.Contain("morning"), text);
                foreach (string e in r.ExpectedArguments)
                {
                    Assert.That(e, Does.Not.Contain("returns").And.Not.Contain("morning"));
                }
            }
        }

        [Test]
        public void FailureFeedback_IsNonEmptyForEveryNonEmptyFailure()
        {
            foreach (string text in FailingInputs)
            {
                ParseResult r = SpellParser.Parse(text);
                Assert.That(r.Status, Is.Not.EqualTo(ParseStatus.Empty), text);
                Assert.That(r.Feedback, Is.Not.Empty, text);
            }
        }

        [Test]
        public void RuleRejectionMessages_NeverLeakTheName_BeforeItIsDiscovered()
        {
            foreach (RoomState s in TestSupport.ReachableStates)
            {
                if (s.NameDiscovered) continue;
                foreach (Command c in RoomRules.AllCommands)
                {
                    CommandResult r = RoomRules.Apply(s, c);
                    if (r.Accepted) continue;
                    string m = r.Message.ToLowerInvariant();
                    Assert.That(m, Does.Not.Contain("returns").And.Not.Contain("morning"), $"{c} from [{s}]: {r.Message}");
                }
            }
        }

        [Test]
        public void Parser_IsStateless_SameInputSameResult()
        {
            foreach (string text in FailingInputs.Concat(TestSupport.CanonicalSolution))
            {
                ParseResult a = SpellParser.Parse(text);
                ParseResult b = SpellParser.Parse(text);
                Assert.That(a.Status, Is.EqualTo(b.Status));
                Assert.That(a.Command, Is.EqualTo(b.Command));
                Assert.That(a.Normalized, Is.EqualTo(b.Normalized));
            }
        }
    }
}
