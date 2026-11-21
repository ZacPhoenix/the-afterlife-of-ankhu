using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ankhu.BorrowedDawn
{
    public enum ParseStatus
    {
        Ok = 0,
        Empty,
        UnknownVerb,
        MissingArgument,
        InvalidArgument,
        TrailingWords,
    }

    public sealed class ParseResult
    {
        public ParseStatus Status { get; }
        public bool Success => Status == ParseStatus.Ok;
        /// <summary>The parsed command when <see cref="Success"/>, otherwise null.</summary>
        public Command Command { get; }
        /// <summary>Space-joined tokens after normalization and article removal.</summary>
        public string Normalized { get; }
        /// <summary>Canonical verb recognised before the failure, if any (for "bind ..." style feedback).</summary>
        public string Verb { get; }
        /// <summary>The first token that could not be used, if any.</summary>
        public string OffendingText { get; }
        /// <summary>Canonical arguments the recognised verb accepts. Empty when no verb was recognised.</summary>
        public IReadOnlyList<string> ExpectedArguments { get; }

        internal ParseResult(ParseStatus status, Command command, string normalized, string verb, string offending, IReadOnlyList<string> expected)
        {
            Status = status;
            Command = command;
            Normalized = normalized;
            Verb = verb;
            OffendingText = offending;
            ExpectedArguments = expected ?? Array.Empty<string>();
        }

        /// <summary>Player-facing feedback for a failed parse. Never names the gate's Name.</summary>
        public string Feedback
        {
            get
            {
                switch (Status)
                {
                    case ParseStatus.Ok: return string.Empty;
                    case ParseStatus.Empty: return string.Empty;
                    case ParseStatus.MissingArgument:
                        return "“" + Verb + "” needs something to act on: " + string.Join(", ", ExpectedArguments) + ".";
                    case ParseStatus.InvalidArgument:
                        return "“" + Verb + "” cannot act on “" + OffendingText + "”. Try: " + string.Join(", ", ExpectedArguments) + ".";
                    case ParseStatus.TrailingWords:
                        return "The words after “" + Verb + "” trail off at “" + OffendingText + "”. Keep the spell short.";
                    default:
                        return "The chamber does not understand “" + OffendingText + "”. Try flow west, bind shadow, wake bearer, recall bell, or echo bell.";
                }
            }
        }
    }

    /// <summary>
    /// Deterministic parser for the Borrowed Dawn vocabulary. No fuzzy matching and no state access:
    /// the same text always yields the same result. Every accepted spelling is listed in the alias tables.
    /// Normalization: Unicode NFKC, lower case (invariant), any character that is not a letter or digit
    /// becomes a space, whitespace collapses, and the articles "the", "a" and "an" are dropped.
    /// </summary>
    public static class SpellParser
    {
        private enum ArgKind
        {
            None,
            Direction,
            BindTarget,
            Location,
            Fixed,
        }

        private sealed class VerbRule
        {
            public string[] Phrase;
            public string Canonical;
            public ArgKind Arg;
            /// <summary>For ArgKind.Fixed: the accepted object aliases.</summary>
            public Dictionary<string[], Command> FixedObjects;
            public Func<WaterSide, Command> DirectionFactory;
            public Func<BindTarget, Command> BindFactory;
            public Func<Location, Command> LocationFactory;
            /// <summary>For ArgKind.None, or for a verb whose object is optional.</summary>
            public Command Intransitive;
        }

        public static readonly IReadOnlyList<string> Articles = new[] { "the", "a", "an" };

        public static readonly IReadOnlyDictionary<string, WaterSide> DirectionAliases = new Dictionary<string, WaterSide>
        {
            { "west", WaterSide.West }, { "w", WaterSide.West },
            { "east", WaterSide.East }, { "e", WaterSide.East },
        };

        public static readonly IReadOnlyDictionary<string, BindTarget> BindTargetAliases = new Dictionary<string, BindTarget>
        {
            { "elevator", BindTarget.Elevator }, { "lift", BindTarget.Elevator },
            { "shadow", BindTarget.Shadow },
            { "bridge", BindTarget.Bridge }, { "pendulum", BindTarget.Bridge },
        };

        public static readonly IReadOnlyDictionary<string, Location> LocationAliases = new Dictionary<string, Location>
        {
            { "floor", Location.Floor }, { "ground", Location.Floor },
            { "elevator", Location.Lift }, { "lift", Location.Lift },
            { "gallery", Location.Gallery }, { "balcony", Location.Gallery },
            { "landing", Location.Landing }, { "bell landing", Location.Landing },
            { "gate", Location.Gate }, { "threshold", Location.Gate },
            { "bridge", Location.Bridge },
        };

        /// <summary>The gate's Name after normalization. Speaking it only works once discovered (see RoomRules).</summary>
        public static readonly IReadOnlyList<string> GateName = new[] { "he", "who", "returns", "morning" };

        private static readonly string[][] NamePrefixes = { new string[0], new[] { "speak" }, new[] { "say" } };

        private static readonly VerbRule[] Rules = BuildRules();

        private static VerbRule[] BuildRules()
        {
            var bearer = new[] { "bearer", "statue" };
            var bell = new[] { "bell", "resonator" };
            var mirror = new[] { "mirror", "sun mirror" };
            var inscription = new[] { "inscription", "inscriptions", "gallery" };

            Dictionary<string[], Command> Obj(string[] aliases, Command c) =>
                aliases.ToDictionary(a => Tokenize(a), a => c);

            var rules = new List<VerbRule>
            {
                new VerbRule { Phrase = T("flow"), Canonical = "flow", Arg = ArgKind.Direction, DirectionFactory = Command.Flow },
                new VerbRule { Phrase = T("bind"), Canonical = "bind", Arg = ArgKind.BindTarget, BindFactory = Command.Bind },
                new VerbRule { Phrase = T("release"), Canonical = "release", Arg = ArgKind.BindTarget, BindFactory = Command.Release },
                new VerbRule { Phrase = T("unbind"), Canonical = "release", Arg = ArgKind.BindTarget, BindFactory = Command.Release },
                new VerbRule { Phrase = T("wake"), Canonical = "wake", Arg = ArgKind.Fixed, FixedObjects = Obj(bearer, Command.WakeBearer) },
                new VerbRule { Phrase = T("awaken"), Canonical = "wake", Arg = ArgKind.Fixed, FixedObjects = Obj(bearer, Command.WakeBearer) },
                new VerbRule { Phrase = T("sleep"), Canonical = "sleep", Arg = ArgKind.Fixed, FixedObjects = Obj(bearer, Command.SleepBearer) },
                new VerbRule { Phrase = T("recall"), Canonical = "recall", Arg = ArgKind.Fixed, FixedObjects = Obj(bell, Command.RecallBell) },
                new VerbRule { Phrase = T("strike"), Canonical = "strike", Arg = ArgKind.Fixed, FixedObjects = Obj(bell, Command.StrikeBell) },
                new VerbRule { Phrase = T("ring"), Canonical = "strike", Arg = ArgKind.Fixed, FixedObjects = Obj(bell, Command.StrikeBell) },
                new VerbRule { Phrase = T("echo"), Canonical = "echo", Arg = ArgKind.Fixed, FixedObjects = Obj(bell, Command.EchoBell) },
                new VerbRule { Phrase = T("take"), Canonical = "take", Arg = ArgKind.Fixed, FixedObjects = Obj(mirror, Command.TakeMirror) },
                new VerbRule { Phrase = T("pick up"), Canonical = "take", Arg = ArgKind.Fixed, FixedObjects = Obj(mirror, Command.TakeMirror) },
                new VerbRule { Phrase = T("install"), Canonical = "install", Arg = ArgKind.Fixed, FixedObjects = Obj(mirror, Command.InstallMirror) },
                new VerbRule { Phrase = T("place"), Canonical = "install", Arg = ArgKind.Fixed, FixedObjects = Obj(mirror, Command.InstallMirror) },
                new VerbRule { Phrase = T("read"), Canonical = "read", Arg = ArgKind.Fixed, FixedObjects = Obj(inscription, Command.ReadInscription) },
                new VerbRule { Phrase = T("board"), Canonical = "board", Arg = ArgKind.Fixed, FixedObjects = Obj(new[] { "elevator", "lift" }, Command.MoveTo(Location.Lift)) },
                new VerbRule { Phrase = T("enter"), Canonical = "enter", Arg = ArgKind.Fixed, FixedObjects = Obj(new[] { "gallery", "balcony" }, Command.MoveTo(Location.Gallery)) },
                new VerbRule { Phrase = T("descend"), Canonical = "descend", Arg = ArgKind.Fixed, FixedObjects = Obj(new[] { "to floor", "to ground" }, Command.MoveTo(Location.Floor)), Intransitive = Command.MoveTo(Location.Floor) },
                new VerbRule { Phrase = T("cross"), Canonical = "cross", Arg = ArgKind.Fixed, FixedObjects = Obj(new[] { "bridge" }, Command.CrossBridge) },
                new VerbRule { Phrase = T("lower"), Canonical = "lower", Arg = ArgKind.Fixed, FixedObjects = Obj(new[] { "ladder", "hatch" }, Command.LowerLadder) },
                new VerbRule { Phrase = T("go to"), Canonical = "go to", Arg = ArgKind.Location, LocationFactory = Command.MoveTo },
                new VerbRule { Phrase = T("go"), Canonical = "go to", Arg = ArgKind.Location, LocationFactory = Command.MoveTo },
                new VerbRule { Phrase = T("walk to"), Canonical = "go to", Arg = ArgKind.Location, LocationFactory = Command.MoveTo },
                new VerbRule { Phrase = T("move to"), Canonical = "go to", Arg = ArgKind.Location, LocationFactory = Command.MoveTo },
            };
            // Longest phrase first so "go to" wins over "go".
            return rules.OrderByDescending(r => r.Phrase.Length).ToArray();
        }

        private static string[] T(string s) => Tokenize(s);

        /// <summary>Normalizes raw input into tokens. Public so UI and tests share one definition.</summary>
        public static string[] Tokenize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return Array.Empty<string>();
            }
            string text = raw.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
            var sb = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
            }
            return sb.ToString()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => !Articles.Contains(t))
                .ToArray();
        }

        public static ParseResult Parse(string raw)
        {
            string[] tokens = Tokenize(raw);
            string normalized = string.Join(" ", tokens);
            if (tokens.Length == 0)
            {
                return new ParseResult(ParseStatus.Empty, null, normalized, null, null, null);
            }

            foreach (var prefix in NamePrefixes)
            {
                if (tokens.Length == prefix.Length + GateName.Count && StartsWith(tokens, 0, prefix) && StartsWith(tokens, prefix.Length, GateName.ToArray()))
                {
                    return Ok(Command.SpeakName, normalized);
                }
            }

            foreach (var rule in Rules)
            {
                if (!StartsWith(tokens, 0, rule.Phrase))
                {
                    continue;
                }
                int i = rule.Phrase.Length;
                return ParseArgument(rule, tokens, i, normalized);
            }

            return new ParseResult(ParseStatus.UnknownVerb, null, normalized, null, tokens[0], null);
        }

        private static ParseResult ParseArgument(VerbRule rule, string[] tokens, int i, string normalized)
        {
            IReadOnlyList<string> expected = Expected(rule);
            if (i == tokens.Length)
            {
                if (rule.Intransitive != null)
                {
                    return Ok(rule.Intransitive, normalized);
                }
                return new ParseResult(ParseStatus.MissingArgument, null, normalized, rule.Canonical, null, expected);
            }

            Command cmd = null;
            int consumed = 0;
            switch (rule.Arg)
            {
                case ArgKind.Direction:
                    if (DirectionAliases.TryGetValue(tokens[i], out var dir)) { cmd = rule.DirectionFactory(dir); consumed = 1; }
                    break;
                case ArgKind.BindTarget:
                    if (BindTargetAliases.TryGetValue(tokens[i], out var bt)) { cmd = rule.BindFactory(bt); consumed = 1; }
                    break;
                case ArgKind.Location:
                    foreach (var kv in LocationAliases.OrderByDescending(k => Tokenize(k.Key).Length))
                    {
                        var phrase = Tokenize(kv.Key);
                        if (StartsWith(tokens, i, phrase)) { cmd = rule.LocationFactory(kv.Value); consumed = phrase.Length; break; }
                    }
                    break;
                case ArgKind.Fixed:
                    foreach (var kv in rule.FixedObjects.OrderByDescending(k => k.Key.Length))
                    {
                        if (StartsWith(tokens, i, kv.Key)) { cmd = kv.Value; consumed = kv.Key.Length; break; }
                    }
                    break;
            }

            if (cmd == null)
            {
                return new ParseResult(ParseStatus.InvalidArgument, null, normalized, rule.Canonical, tokens[i], expected);
            }
            if (i + consumed < tokens.Length)
            {
                return new ParseResult(ParseStatus.TrailingWords, null, normalized, rule.Canonical, tokens[i + consumed], expected);
            }
            return Ok(cmd, normalized);
        }

        private static IReadOnlyList<string> Expected(VerbRule rule)
        {
            switch (rule.Arg)
            {
                case ArgKind.Direction: return new[] { "west", "east" };
                case ArgKind.BindTarget: return new[] { "elevator", "shadow", "bridge" };
                case ArgKind.Location: return new[] { "floor", "elevator", "gallery", "landing", "bridge", "gate" };
                case ArgKind.Fixed:
                    // First alias of each distinct command, in declaration order.
                    return rule.FixedObjects.GroupBy(k => k.Value).Select(g => string.Join(" ", g.First().Key)).ToArray();
                default: return Array.Empty<string>();
            }
        }

        private static ParseResult Ok(Command c, string normalized) =>
            new ParseResult(ParseStatus.Ok, c, normalized, null, null, null);

        private static bool StartsWith(string[] tokens, int at, string[] phrase)
        {
            if (at + phrase.Length > tokens.Length) return false;
            for (int k = 0; k < phrase.Length; k++)
            {
                if (!string.Equals(tokens[at + k], phrase[k], StringComparison.Ordinal)) return false;
            }
            return true;
        }
    }
}
