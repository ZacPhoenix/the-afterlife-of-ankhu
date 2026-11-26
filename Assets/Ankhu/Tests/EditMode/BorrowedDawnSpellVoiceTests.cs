using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ankhu.Bindings;
using Ankhu.BorrowedDawn;
using Ankhu.Integration;
using Ankhu.Voice;
using NUnit.Framework;
using CommandResult = Ankhu.Integration.CommandResult;

namespace Ankhu.Tests
{
    public sealed class BorrowedDawnSpellVoiceTests
    {
        private static readonly CommandResult Ok = new CommandResult(CommandOutcome.Success, "ok");
        private readonly BorrowedDawnSpellVoice resolver = new BorrowedDawnSpellVoice();

        private static IEnumerable<Command> AllCommands()
        {
            yield return Command.Flow(Ankhu.BorrowedDawn.WaterSide.West);
            yield return Command.Flow(Ankhu.BorrowedDawn.WaterSide.East);
            foreach (BindTarget t in new[] { BindTarget.Elevator, BindTarget.Shadow, BindTarget.Bridge })
            {
                yield return Command.Bind(t);
                yield return Command.Release(t);
            }
            yield return Command.WakeBearer;
            yield return Command.SleepBearer;
            yield return Command.RecallBell;
            yield return Command.StrikeBell;
            yield return Command.EchoBell;
            yield return Command.TakeMirror;
            yield return Command.InstallMirror;
            yield return Command.ReadInscription;
            yield return Command.SpeakName;
            yield return Command.CrossBridge;
            yield return Command.LowerLadder;
        }

        [TestCase("flow west", "flow_west")]
        [TestCase("FLOW  West!", "flow_west")]
        [TestCase("unbind the lift", "release_elevator")]
        [TestCase("bind pendulum", "bind_bridge")]
        [TestCase("awaken the statue", "wake_bearer")]
        [TestCase("recall resonator", "recall_bell")]
        [TestCase("speak he who returns the morning", "name_gate")]
        [TestCase("grab the mirror", "take_mirror")]
        [TestCase("put mirror into socket", "install_mirror")]
        public void AliasesAreVoicedInCanonicalForm(string typed, string phrase)
        {
            SpokenSpell spell = resolver.Resolve(typed, Ok);
            Assert.That(spell, Is.Not.Null);
            Assert.That(spell.Clauses.Single().PhraseId, Is.EqualTo(phrase));
        }

        [Test]
        public void CategoriesSeparateWordsNamesAndDeeds()
        {
            Assert.That(resolver.Resolve("bind shadow", Ok).Category, Is.EqualTo(SpellVoiceCategory.WordOfPower));
            Assert.That(resolver.Resolve("he who returns the morning", Ok).Category, Is.EqualTo(SpellVoiceCategory.Name));
            Assert.That(resolver.Resolve("strike bell", Ok).Category, Is.EqualTo(SpellVoiceCategory.Deed));
        }

        [TestCase("go to gallery")]
        [TestCase("board lift")]
        [TestCase("descend")]
        [TestCase("flwo west")]
        [TestCase("")]
        public void MovementAndUnparsedTextAreSilent(string typed)
        {
            Assert.That(resolver.Resolve(typed, Ok), Is.Null);
        }

        [Test]
        public void EveryVoicedCommandHasALineInTheVoiceCatalog()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "tools", "voice", "voice_lines.json");
            if (!File.Exists(path))
            {
                Assert.Ignore("tools/voice/voice_lines.json not found from " + Directory.GetCurrentDirectory());
            }
            var ids = new HashSet<string>(Regex.Matches(File.ReadAllText(path), "\"id\"\\s*:\\s*\"([a-z_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value));
            foreach (Command c in AllCommands())
            {
                SpokenSpell spell = BorrowedDawnSpellVoice.ForCommand(c);
                Assert.That(spell, Is.Not.Null, c.ToString());
                Assert.That(ids, Does.Contain(spell.Clauses.Single().PhraseId), c.ToString());
            }
        }

        [Test]
        public void EveryVoicedCommandRoundTripsThroughItsCanonicalText()
        {
            foreach (Command c in AllCommands())
            {
                SpokenSpell direct = BorrowedDawnSpellVoice.ForCommand(c);
                SpokenSpell parsed = resolver.Resolve(c.ToString(), Ok);
                Assert.That(parsed?.Clauses.Single().PhraseId, Is.EqualTo(direct.Clauses.Single().PhraseId), c.ToString());
            }
        }
    }
}
