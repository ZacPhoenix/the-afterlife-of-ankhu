using System.Collections.Generic;
using System.Linq;
using Ankhu.Voice;
using NUnit.Framework;

namespace Ankhu.Tests
{
    public sealed class SpellVoicePlannerTests
    {
        private sealed class FakeCatalog : IVoiceClipCatalog
        {
            private readonly Dictionary<string, double> clips = new Dictionary<string, double>();
            private readonly Dictionary<string, int> takes = new Dictionary<string, int>();
            private readonly Dictionary<string, int> last = new Dictionary<string, int>();

            public FakeCatalog With(string key, double seconds, int variants = 1)
            {
                clips[key] = seconds;
                takes[key] = variants;
                return this;
            }

            public bool Contains(string key) => clips.ContainsKey(key);

            public bool TryPick(string key, out VoiceClipChoice choice)
            {
                choice = default;
                if (!clips.TryGetValue(key, out double d)) return false;
                int v = last.TryGetValue(key, out int l) ? (l + 1) % takes[key] : 0;
                last[key] = v;
                choice = new VoiceClipChoice(key, v, d);
                return true;
            }
        }

        private static readonly SpellVoiceTiming Timing = new SpellVoiceTiming { ClauseGap = 0.12f, WordGap = 0f };

        private static SpokenSpell Spell(params (string id, string[] words)[] clauses) =>
            new SpokenSpell(SpellVoiceCategory.WordOfPower, clauses.Select(c => new SpokenClause(c.id, c.words)).ToArray());

        private static string[] Keys(VoicePlan plan) => plan.Cues.Select(c => c.Clip.Key).ToArray();

        [Test]
        public void SoleClauseUsesWholePhrase()
        {
            var catalog = new FakeCatalog().With("clause.flow_west.sole", 0.8).With("word.flow.lead", 0.3).With("word.west.final", 0.4);
            VoicePlan plan = SpellVoicePlanner.Plan(Spell(("flow_west", new[] { "flow", "west" })), catalog, Timing);
            Assert.That(Keys(plan), Is.EqualTo(new[] { "clause.flow_west.sole" }));
            Assert.That(plan.Cues[0].Start, Is.EqualTo(0));
            Assert.That(plan.Duration, Is.EqualTo(0.8).Within(1e-9));
            Assert.That(plan.IsComplete);
        }

        [Test]
        public void ChainUsesPositionalTakesWithClauseGap()
        {
            var catalog = new FakeCatalog()
                .With("clause.wake_bearer.first", 0.7)
                .With("clause.recall_bell.medial", 0.9)
                .With("clause.echo_bell.final", 1.0);
            VoicePlan plan = SpellVoicePlanner.Plan(Spell(("wake_bearer", new[] { "wake", "bearer" }),
                ("recall_bell", new[] { "recall", "the", "bell" }), ("echo_bell", new[] { "echo", "the", "bell" })), catalog, Timing);
            Assert.That(Keys(plan), Is.EqualTo(new[] { "clause.wake_bearer.first", "clause.recall_bell.medial", "clause.echo_bell.final" }));
            Assert.That(plan.Cues[1].Start, Is.EqualTo(0.7 + 0.12).Within(1e-6));
            Assert.That(plan.Cues[2].Start, Is.EqualTo(0.7 + 0.12 + 0.9 + 0.12).Within(1e-6));
        }

        [Test]
        public void MissingPositionFallsBackToSolePhraseWithAnd()
        {
            var catalog = new FakeCatalog().With("clause.bind_bridge.sole", 0.9).With("clause.flow_east.sole", 0.8).With("word.and.lead", 0.2);
            VoicePlan plan = SpellVoicePlanner.Plan(Spell(("bind_bridge", new[] { "bind", "the", "bridge" }), ("flow_east", new[] { "flow", "east" })), catalog, Timing);
            Assert.That(Keys(plan), Is.EqualTo(new[] { "clause.bind_bridge.sole", "word.and.lead", "clause.flow_east.sole" }));
            Assert.That(plan.Cues[2].Start, Is.EqualTo(0.9 + 0.12 + 0.2).Within(1e-6));
        }

        [Test]
        public void UnrecordedPhraseIsStitchedWordByWordWithFinalFormLast()
        {
            var catalog = new FakeCatalog()
                .With("word.open.lead", 0.3).With("word.the.lead", 0.1)
                .With("word.door.lead", 0.3).With("word.door.final", 0.4);
            VoicePlan plan = SpellVoicePlanner.Plan(Spell(("open_door", new[] { "open", "the", "door" })), catalog, Timing);
            Assert.That(Keys(plan), Is.EqualTo(new[] { "word.open.lead", "word.the.lead", "word.door.final" }));
            Assert.That(plan.Cues[2].Start, Is.EqualTo(0.4).Within(1e-9));
        }

        [Test]
        public void WordFormFallsBackToTheOtherPosition()
        {
            var catalog = new FakeCatalog().With("word.seal.lead", 0.3).With("word.gate.lead", 0.3);
            VoicePlan plan = SpellVoicePlanner.Plan(Spell(("seal_gate", new[] { "seal", "gate" })), catalog, Timing);
            Assert.That(Keys(plan), Is.EqualTo(new[] { "word.seal.lead", "word.gate.lead" }));
            Assert.That(plan.Missing, Is.Empty);
        }

        [Test]
        public void NegativeWordGapOverlapsClips()
        {
            var catalog = new FakeCatalog().With("word.seal.lead", 0.3).With("word.gate.final", 0.3);
            var overlap = new SpellVoiceTiming { ClauseGap = 0.12f, WordGap = -0.02f };
            VoicePlan plan = SpellVoicePlanner.Plan(Spell(("seal_gate", new[] { "seal", "gate" })), catalog, overlap);
            Assert.That(plan.Cues[1].Start, Is.EqualTo(0.28).Within(1e-6));
        }

        [Test]
        public void MissingWordsAreReportedNotInvented()
        {
            var catalog = new FakeCatalog().With("word.seal.lead", 0.3);
            VoicePlan plan = SpellVoicePlanner.Plan(Spell(("seal_gate", new[] { "seal", "gate" })), catalog, Timing);
            Assert.That(plan.Missing, Is.EqualTo(new[] { "word.gate.final" }));
            Assert.That(plan.IsComplete, Is.False);
        }

        [Test]
        public void NameWithoutRecordingIsMissingAndSilent()
        {
            var spell = new SpokenSpell(SpellVoiceCategory.Name, new SpokenClause("name_gate", null));
            VoicePlan plan = SpellVoicePlanner.Plan(spell, new FakeCatalog(), Timing);
            Assert.That(plan.Cues, Is.Empty);
            Assert.That(plan.Missing, Is.EqualTo(new[] { "clause.name_gate.sole" }));
        }

        [Test]
        public void RepeatedSpellRotatesTakes()
        {
            var catalog = new FakeCatalog().With("clause.flow_west.sole", 0.8, variants: 2);
            SpokenSpell spell = Spell(("flow_west", new[] { "flow", "west" }));
            int a = SpellVoicePlanner.Plan(spell, catalog, Timing).Cues[0].Clip.Variant;
            int b = SpellVoicePlanner.Plan(spell, catalog, Timing).Cues[0].Clip.Variant;
            int c = SpellVoicePlanner.Plan(spell, catalog, Timing).Cues[0].Clip.Variant;
            Assert.That(new[] { a, b, c }, Is.EqualTo(new[] { 0, 1, 0 }));
        }

        [TestCase(0, 1, ClausePosition.Sole)]
        [TestCase(0, 2, ClausePosition.First)]
        [TestCase(1, 2, ClausePosition.Final)]
        [TestCase(1, 3, ClausePosition.Medial)]
        [TestCase(2, 3, ClausePosition.Final)]
        public void PositionsFollowChainLength(int index, int count, ClausePosition expected)
        {
            Assert.That(SpellVoicePlanner.PositionOf(index, count), Is.EqualTo(expected));
        }
    }
}
