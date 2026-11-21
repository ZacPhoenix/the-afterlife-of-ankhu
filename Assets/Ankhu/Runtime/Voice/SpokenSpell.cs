using System;
using System.Collections.Generic;

namespace Ankhu.Voice
{
    /// <summary>What kind of utterance Ankhu is voicing. Players can mute deeds separately.</summary>
    public enum SpellVoiceCategory
    {
        /// <summary>A word of power: flow, bind, release, wake, sleep, recall, echo and later verbs.</summary>
        WordOfPower,
        /// <summary>A Name or epithet spoken alone, such as the gate's Name.</summary>
        Name,
        /// <summary>A physical act typed as text: take, place, read, strike, cross, lower.</summary>
        Deed,
    }

    /// <summary>
    /// One clause of a spell in voice terms. <see cref="PhraseId"/> selects whole-phrase recordings
    /// (keys <c>clause.{PhraseId}.{position}</c>). <see cref="Words"/> is the canonical word sequence used
    /// when no phrase recording exists (keys <c>word.{word}.{lead|final}</c>).
    /// </summary>
    public sealed class SpokenClause
    {
        public string PhraseId { get; }
        public IReadOnlyList<string> Words { get; }

        public SpokenClause(string phraseId, IReadOnlyList<string> words)
        {
            PhraseId = phraseId;
            Words = words ?? Array.Empty<string>();
        }

        public override string ToString() => PhraseId ?? string.Join(" ", Words);
    }

    /// <summary>
    /// A successful spell as Ankhu should say it: canonical words, never the raw typed text, so
    /// "grab mirror" and "take the mirror" sound the same. Engine-free.
    /// </summary>
    public sealed class SpokenSpell
    {
        public SpellVoiceCategory Category { get; }
        public IReadOnlyList<SpokenClause> Clauses { get; }

        public SpokenSpell(SpellVoiceCategory category, IReadOnlyList<SpokenClause> clauses)
        {
            Category = category;
            Clauses = clauses ?? Array.Empty<SpokenClause>();
        }

        public SpokenSpell(SpellVoiceCategory category, params SpokenClause[] clauses)
            : this(category, (IReadOnlyList<SpokenClause>)clauses)
        {
        }

        public override string ToString() => string.Join(" and ", Clauses);
    }
}
