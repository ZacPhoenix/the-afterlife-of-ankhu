using System;
using System.Collections.Generic;

namespace Ankhu.Voice
{
    /// <summary>Where a clause sits in a chain. Each position has its own recording so intonation fits.</summary>
    public enum ClausePosition
    {
        /// <summary>Spoken alone, falling final intonation ("Flow west.").</summary>
        Sole,
        /// <summary>First of a chain, continuation intonation ("Flow west,").</summary>
        First,
        /// <summary>Inside a chain of three, carries its own "and" ("and bind the bridge,").</summary>
        Medial,
        /// <summary>Last of a chain, carries its own "and", falling intonation ("and bind the bridge.").</summary>
        Final,
    }

    /// <summary>Gap rules for stitching. Mirrored in tools/voice/process_voice.py (CLAUSE_GAP, WORD_GAP).</summary>
    [Serializable]
    public struct SpellVoiceTiming
    {
        /// <summary>Seconds of silence added between clause recordings. Each clip already ends in about 110 ms of natural tail.</summary>
        public float ClauseGap;
        /// <summary>Seconds between stitched words. Negative overlaps the clips, which crossfade through their baked fades.</summary>
        public float WordGap;

        public static SpellVoiceTiming Default => new SpellVoiceTiming { ClauseGap = 0.12f, WordGap = 0f };
    }

    /// <summary>A chosen recording: which key, which take, how long it is.</summary>
    public readonly struct VoiceClipChoice
    {
        public readonly string Key;
        public readonly int Variant;
        public readonly double Duration;

        public VoiceClipChoice(string key, int variant, double duration)
        {
            Key = key;
            Variant = variant;
            Duration = duration;
        }
    }

    /// <summary>Supplies recordings by key. The Unity library implements it. Tests use a fake.</summary>
    public interface IVoiceClipCatalog
    {
        bool Contains(string key);

        /// <summary>Picks the take to use now (implementations rotate takes so repeats vary).</summary>
        bool TryPick(string key, out VoiceClipChoice choice);
    }

    /// <summary>One recording scheduled at <see cref="Start"/> seconds after the utterance begins.</summary>
    public readonly struct VoiceCue
    {
        public readonly VoiceClipChoice Clip;
        public readonly double Start;

        public VoiceCue(VoiceClipChoice clip, double start)
        {
            Clip = clip;
            Start = start;
        }

        public double End => Start + Clip.Duration;
        public override string ToString() => $"{Clip.Key}#{Clip.Variant}@{Start:0.000}";
    }

    public sealed class VoicePlan
    {
        public IReadOnlyList<VoiceCue> Cues { get; }
        /// <summary>Keys that had no recording at all. Non-empty means the line was spoken incompletely or not at all.</summary>
        public IReadOnlyList<string> Missing { get; }
        public double Duration { get; }
        public bool IsComplete => Missing.Count == 0 && Cues.Count > 0;

        public VoicePlan(IReadOnlyList<VoiceCue> cues, IReadOnlyList<string> missing)
        {
            Cues = cues;
            Missing = missing;
            double end = 0;
            foreach (VoiceCue c in cues)
            {
                end = Math.Max(end, c.End);
            }
            Duration = end;
        }
    }

    /// <summary>
    /// Turns a <see cref="SpokenSpell"/> into timed recordings. Engine-free and deterministic for a given catalog.
    ///
    /// Preference for each clause, best first:
    /// 1. The whole phrase recorded in its chain position (<c>clause.bind_bridge.final</c>).
    /// 2. The phrase recorded alone (<c>clause.bind_bridge.sole</c>), preceded by <c>word.and.lead</c> after the first clause.
    /// 3. Word by word (<c>word.bind.lead</c>, <c>word.the.lead</c>, <c>word.bridge.final</c>), the last word of the
    ///    utterance in its final form, every other word in its lead form, either form as a fallback.
    /// </summary>
    public static class SpellVoicePlanner
    {
        public const string AndWord = "and";

        public static string PositionName(ClausePosition position)
        {
            switch (position)
            {
                case ClausePosition.First: return "first";
                case ClausePosition.Medial: return "medial";
                case ClausePosition.Final: return "final";
                default: return "sole";
            }
        }

        public static ClausePosition PositionOf(int index, int count)
        {
            if (count <= 1) return ClausePosition.Sole;
            if (index == 0) return ClausePosition.First;
            return index == count - 1 ? ClausePosition.Final : ClausePosition.Medial;
        }

        public static string ClauseKey(string phraseId, ClausePosition position) => "clause." + phraseId + "." + PositionName(position);

        public static string WordKey(string word, bool final) => "word." + word + (final ? ".final" : ".lead");

        public static VoicePlan Plan(SpokenSpell spell, IVoiceClipCatalog catalog, SpellVoiceTiming timing)
        {
            if (spell == null) throw new ArgumentNullException(nameof(spell));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var cues = new List<VoiceCue>();
            var missing = new List<string>();
            double cursor = 0;
            int count = spell.Clauses.Count;

            void Add(VoiceClipChoice choice, double gap)
            {
                double start = Math.Max(0, cursor + gap);
                cues.Add(new VoiceCue(choice, start));
                cursor = start + choice.Duration;
            }

            for (int i = 0; i < count; i++)
            {
                SpokenClause clause = spell.Clauses[i];
                ClausePosition position = PositionOf(i, count);
                double clauseGap = i == 0 ? 0 : timing.ClauseGap;
                bool lastClause = i == count - 1;

                if (clause.PhraseId != null)
                {
                    if (catalog.TryPick(ClauseKey(clause.PhraseId, position), out VoiceClipChoice exact))
                    {
                        Add(exact, clauseGap);
                        continue;
                    }
                    string soleKey = ClauseKey(clause.PhraseId, ClausePosition.Sole);
                    if (position != ClausePosition.Sole && catalog.Contains(soleKey))
                    {
                        double gap = clauseGap;
                        if (i > 0 && catalog.TryPick(WordKey(AndWord, false), out VoiceClipChoice and))
                        {
                            Add(and, gap);
                            gap = timing.WordGap;
                        }
                        catalog.TryPick(soleKey, out VoiceClipChoice sole);
                        Add(sole, gap);
                        continue;
                    }
                }

                var words = new List<string>(clause.Words.Count + 1);
                if (i > 0)
                {
                    words.Add(AndWord);
                }
                words.AddRange(clause.Words);
                if (clause.Words.Count == 0)
                {
                    missing.Add(clause.PhraseId != null ? ClauseKey(clause.PhraseId, position) : "clause.<empty>");
                    continue;
                }

                for (int w = 0; w < words.Count; w++)
                {
                    bool final = lastClause && w == words.Count - 1;
                    double gap = w == 0 ? clauseGap : timing.WordGap;
                    if (catalog.TryPick(WordKey(words[w], final), out VoiceClipChoice choice) ||
                        catalog.TryPick(WordKey(words[w], !final), out choice))
                    {
                        Add(choice, gap);
                    }
                    else
                    {
                        missing.Add(WordKey(words[w], final));
                    }
                }
            }

            return new VoicePlan(cues, missing);
        }
    }
}
