using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ankhu.Realms.Core
{
    /// <summary>One English word and the Egyptian it becomes on screen. Signs are Gardiner codes.</summary>
    public sealed class WordGloss
    {
        public string English { get; }
        public string Transliteration { get; }
        public IReadOnlyList<string> Signs { get; }
        public bool Cartouche { get; }

        public WordGloss(string english, string transliteration, string signs, bool cartouche = false)
        {
            English = english;
            Transliteration = transliteration;
            Signs = (signs ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Cartouche = cartouche;
        }
    }

    /// <summary>A thing in a room that words can act on. State is a short lowercase token ("shut", "open").</summary>
    public sealed class RoomObject
    {
        public string Id;
        public string Name;
        /// <summary>Every phrase the player may type for it, lowercase, articles removed ("door", "shrine door").</summary>
        public string[] Aliases = Array.Empty<string>();
        public string Initial;
        /// <summary>The glyphs for its name when it is the object of a spell.</summary>
        public WordGloss Gloss;
        /// <summary>Zones from which words reach it. Empty means anywhere in the room.</summary>
        public string[] Zones = Array.Empty<string>();
        /// <summary>Phrases that reach it but that it will not answer to, with what the room says instead
        /// (a shabti that answers only to its owner's name).</summary>
        public Dictionary<string, string> Refuses = new Dictionary<string, string>();
    }

    /// <summary>A required state: object Id must be in one of States.</summary>
    public sealed class Cond
    {
        public string Id;
        public string[] States;
        public Cond(string id, params string[] states) { Id = id; States = states; }
        public bool Holds(IReadOnlyDictionary<string, string> s) => s.TryGetValue(Id, out string v) && Array.IndexOf(States, v) >= 0;
    }

    /// <summary>
    /// One thing a word can do. Verb plus target object (Target null for a word said to the room itself).
    /// From lists the target states it applies to, To is the new state. Requires are extra conditions.
    /// If the verb and target match but From or Requires fail, Refusal is the reply and nothing changes.
    /// </summary>
    public sealed class SpellRule
    {
        public string Verb;
        public string Target;
        public string[] From = Array.Empty<string>();
        public string To;
        public Cond[] Requires = Array.Empty<Cond>();
        /// <summary>Other objects that change at the same time (id, new state).</summary>
        public (string id, string state)[] Also = Array.Empty<(string, string)>();
        /// <summary>For the "name" verb: the name that must be spoken (lowercase, as tokenized).</summary>
        public string NameValue;
        /// <summary>Optional glyphs for a spoken name that the lexicon does not know.</summary>
        public WordGloss NameGloss;
        public string Success = string.Empty;
        public string Refusal = "The word finds nothing to hold.";
        /// <summary>Story event raised on success (memory, dialogue, gate). Empty for none.</summary>
        public string Event = string.Empty;
    }

    /// <summary>A hint that applies while When holds. The first matching hint is given.</summary>
    public sealed class Hint
    {
        public Cond[] When = Array.Empty<Cond>();
        public string Text;
    }

    public sealed class RoomDef
    {
        public string Id;
        public string Title;
        public string Subtitle = string.Empty;
        public List<RoomObject> Objects = new List<RoomObject>();
        public List<SpellRule> Rules = new List<SpellRule>();
        /// <summary>Verbs Ankhu knows in this room. A verb outside this set is "a word that does not come yet".</summary>
        public HashSet<string> Verbs = new HashSet<string>();
        public Cond[] Goal = Array.Empty<Cond>();
        public List<Hint> Hints = new List<Hint>();

        public RoomObject Find(string id) => Objects.FirstOrDefault(o => o.Id == id);
    }

    public enum Judgement
    {
        Success,
        NoChange,
        NotUnderstood,
        UnknownVerb,
        Incomplete,
        InvalidTarget,
        OutOfReach,
        Refused,
        Ambiguous
    }

    public sealed class SpellResult
    {
        public Judgement Outcome;
        public string Message = string.Empty;
        public string Verb = string.Empty;
        public string TargetId = string.Empty;
        public string Event = string.Empty;
        public List<WordGloss> Words = new List<WordGloss>();
        public List<(string id, string from, string to)> Changes = new List<(string, string, string)>();
        /// <summary>Every story event raised, the spell's own first, then any automatic consequences.</summary>
        public List<string> Events = new List<string>();
        public bool Accepted => Outcome == Judgement.Success;
        public override string ToString() => $"{Outcome} {Verb} {TargetId} {Message}";
    }

    /// <summary>
    /// The rules for one room: parse, judge, apply. Deterministic, engine free, undoable.
    /// </summary>
    public sealed class RealmRoom
    {
        private readonly Dictionary<string, string> state = new Dictionary<string, string>();
        private readonly Stack<Dictionary<string, string>> undo = new Stack<Dictionary<string, string>>();

        public RoomDef Def { get; }
        public IReadOnlyDictionary<string, string> State => state;
        public event Action<SpellResult> Changed;

        public RealmRoom(RoomDef def)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            Reset();
        }

        public void Reset()
        {
            state.Clear();
            undo.Clear();
            foreach (RoomObject o in Def.Objects) state[o.Id] = o.Initial;
        }

        public string Get(string id) => state.TryGetValue(id, out string v) ? v : null;

        public bool Solved => Def.Goal.Length > 0 && Def.Goal.All(c => c.Holds(state));

        public bool Undo()
        {
            if (undo.Count == 0) return false;
            Dictionary<string, string> prev = undo.Pop();
            state.Clear();
            foreach (var kv in prev) state[kv.Key] = kv.Value;
            return true;
        }

        public string Hint()
        {
            foreach (Hint h in Def.Hints)
                if (h.When.All(c => c.Holds(state))) return h.Text;
            return "Listen to the room. Every closed thing is waiting for a word.";
        }

        /// <summary>Judge and, if accepted, apply one typed utterance spoken from the given zone.</summary>
        public SpellResult Speak(string raw, string zone)
        {
            SpellResult r = Judge(raw, zone, out SpellRule rule);
            if (r.Outcome != Judgement.Success) return r;

            undo.Push(new Dictionary<string, string>(state));
            if (rule.Target != null && rule.To != null)
            {
                r.Changes.Add((rule.Target, state[rule.Target], rule.To));
                state[rule.Target] = rule.To;
            }
            foreach (var a in rule.Also)
            {
                r.Changes.Add((a.id, Get(a.id), a.state));
                state[a.id] = a.state;
            }
            if (!string.IsNullOrEmpty(rule.Event)) r.Events.Add(rule.Event);
            ApplyAuto(r);
            Changed?.Invoke(r);
            return r;
        }

        /// <summary>
        /// Automatic consequences: rules with Verb "auto" fire whenever their conditions hold and their target is
        /// not already in the To state. They settle in order, at most a few passes, so derived states stay true.
        /// </summary>
        private void ApplyAuto(SpellResult r)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                bool any = false;
                foreach (SpellRule a in Def.Rules)
                {
                    if (a.Verb != "auto" || a.Target == null) continue;
                    if (Get(a.Target) == a.To || !Applies(a)) continue;
                    r.Changes.Add((a.Target, Get(a.Target), a.To));
                    state[a.Target] = a.To;
                    foreach (var x in a.Also) { r.Changes.Add((x.id, Get(x.id), x.state)); state[x.id] = x.state; }
                    if (!string.IsNullOrEmpty(a.Event)) r.Events.Add(a.Event);
                    any = true;
                }
                if (!any) break;
            }
        }

        /// <summary>Set a state directly (story beats: a name learned, a door unlocked by a memory). Not undoable.</summary>
        public void Force(string id, string value)
        {
            if (!state.ContainsKey(id)) return;
            state[id] = value;
        }

        /// <summary>Teach a verb mid-room (a memory gives Ankhu the word).</summary>
        public void Learn(string verb) { Def.Verbs.Add(verb); }

        /// <summary>Judge without changing anything.</summary>
        public SpellResult Preview(string raw, string zone) => Judge(raw, zone, out _);

        private SpellResult Judge(string raw, string zone, out SpellRule chosen)
        {
            chosen = null;
            var r = new SpellResult();
            List<string> tokens = Tokenize(raw);
            if (tokens.Count == 0)
            {
                r.Outcome = Judgement.NotUnderstood;
                r.Message = "Silence. The Duat waits for a word.";
                return r;
            }

            // A name spoken alone (or "I am X", "my name is X") is the verb "name".
            string verb = Lexicon.VerbOf(tokens[0]);
            List<string> rest = tokens.Skip(1).ToList();
            if (verb == null)
            {
                string spokenName = NameIn(tokens);
                if (spokenName != null && Def.Verbs.Contains("name"))
                {
                    verb = "name";
                    rest = new List<string> { spokenName };
                }
            }

            if (verb == null)
            {
                r.Outcome = Lexicon.IsKnownWord(tokens[0]) ? Judgement.UnknownVerb : Judgement.NotUnderstood;
                r.Message = r.Outcome == Judgement.UnknownVerb
                    ? "You know this word exists, but your mouth cannot shape it yet."
                    : "The words fall like sand. They are not words of power.";
                return r;
            }

            r.Verb = verb;
            if (!Def.Verbs.Contains(verb))
            {
                r.Outcome = Judgement.UnknownVerb;
                r.Message = "You know this word exists, but your mouth cannot shape it yet.";
                return r;
            }

            if (verb == "name")
            {
                string n = string.Join(" ", rest);
                SpellRule nameRule = Def.Rules.FirstOrDefault(x => x.Verb == "name" && x.NameValue == n && Reaches(x, zone))
                                     ?? Def.Rules.FirstOrDefault(x => x.Verb == "name" && x.NameValue == n);
                r.Words.Add(nameRule?.NameGloss ?? Lexicon.NameGloss(n));
                if (nameRule == null)
                {
                    r.Outcome = Judgement.InvalidTarget;
                    r.Message = "The name echoes and is not answered.";
                    return r;
                }
                return Finish(r, nameRule, zone, out chosen);
            }

            r.Words.Add(Lexicon.Gloss(verb));
            List<SpellRule> forVerb = Def.Rules.Where(x => x.Verb == verb && verb != "auto").ToList();
            if (forVerb.Count == 0)
            {
                r.Outcome = Judgement.InvalidTarget;
                r.Message = "Nothing here answers that word.";
                return r;
            }

            RoomObject target = null;
            string phrase = string.Join(" ", rest);
            if (phrase.Length > 0)
            {
                List<RoomObject> found = MatchObjects(phrase, out string alias);
                if (found.Count == 0)
                {
                    r.Outcome = Judgement.InvalidTarget;
                    r.Message = $"There is no \"{phrase}\" here to {Lexicon.Gloss(verb).English}.";
                    return r;
                }
                if (found.Count > 1)
                {
                    // Several things answer to the phrase: the ones within reach of where Ankhu stands decide.
                    List<RoomObject> near = found.Where(o => o.Zones.Length == 0 || zone == null || Array.IndexOf(o.Zones, zone) >= 0).ToList();
                    if (near.Count == 1) found = near;
                    else
                    {
                        r.Outcome = near.Count == 0 ? Judgement.OutOfReach : Judgement.Ambiguous;
                        r.Message = near.Count == 0
                            ? $"No {alias} is close enough. Walk to the one you mean."
                            : $"Which {alias}? Several hear you. Stand beside the one you mean.";
                        return r;
                    }
                }
                target = found[0];
                if (target.Refuses != null && target.Refuses.TryGetValue(alias, out string refusal))
                {
                    r.TargetId = target.Id;
                    r.Outcome = Judgement.Refused;
                    r.Message = refusal;
                    return r;
                }
            }
            else
            {
                // A bare verb picks the one thing in reach it could act on now.
                var candidates = forVerb.Where(x => x.Target != null && Reaches(x, zone) && Applies(x)).Select(x => x.Target).Distinct().ToList();
                if (candidates.Count == 0) candidates = forVerb.Where(x => x.Target != null && Reaches(x, zone)).Select(x => x.Target).Distinct().ToList();
                if (candidates.Count == 1) target = Def.Find(candidates[0]);
                else if (forVerb.Any(x => x.Target == null)) target = null;
                else
                {
                    r.Outcome = Judgement.Incomplete;
                    r.Message = $"{Cap(Lexicon.Gloss(verb).English)} what? Name the thing.";
                    return r;
                }
            }

            if (target != null && phrase.Length == 0 && target.Refuses != null && target.Refuses.TryGetValue("", out string bare))
            {
                r.TargetId = target.Id;
                r.Outcome = Judgement.Refused;
                r.Message = bare;
                return r;
            }
            if (target != null) { r.TargetId = target.Id; if (target.Gloss != null) r.Words.Add(target.Gloss); }

            List<SpellRule> matching = forVerb.Where(x => x.Target == (target?.Id)).ToList();
            if (matching.Count == 0)
            {
                r.Outcome = Judgement.InvalidTarget;
                r.Message = target != null
                    ? $"The {target.Name} does not answer to \"{Lexicon.Gloss(verb).English}\"."
                    : "Nothing here answers that word.";
                return r;
            }

            SpellRule rule = matching.FirstOrDefault(x => Reaches(x, zone) && Applies(x));
            if (rule == null)
            {
                SpellRule reachable = matching.FirstOrDefault(x => Reaches(x, zone));
                if (reachable == null)
                {
                    r.Outcome = Judgement.OutOfReach;
                    r.Message = $"The {target?.Name ?? "word"} is too far. Come closer.";
                    return r;
                }
                if (target != null && reachable.To != null && Get(target.Id) == reachable.To && reachable.Requires.All(c => c.Holds(state)))
                {
                    r.Outcome = Judgement.NoChange;
                    r.Message = $"The {target.Name} is already {reachable.To}.";
                    return r;
                }
                r.Outcome = Judgement.Refused;
                r.Message = reachable.Refusal;
                return r;
            }
            return Finish(r, rule, zone, out chosen);
        }

        private SpellResult Finish(SpellResult r, SpellRule rule, string zone, out SpellRule chosen)
        {
            chosen = null;
            if (!Reaches(rule, zone))
            {
                r.Outcome = Judgement.OutOfReach;
                r.Message = "Too far. Come closer.";
                return r;
            }
            if (!Applies(rule))
            {
                r.Outcome = Judgement.Refused;
                r.Message = rule.Refusal;
                return r;
            }
            chosen = rule;
            r.Outcome = Judgement.Success;
            r.Message = rule.Success;
            r.Event = rule.Event;
            if (string.IsNullOrEmpty(r.TargetId)) r.TargetId = rule.Target ?? string.Empty;
            return r;
        }

        private bool Applies(SpellRule x)
        {
            if (x.Target != null && x.From.Length > 0 && Array.IndexOf(x.From, Get(x.Target)) < 0) return false;
            return x.Requires.All(c => c.Holds(state));
        }

        private bool Reaches(SpellRule x, string zone)
        {
            if (x.Target == null) return true;
            RoomObject o = Def.Find(x.Target);
            return o == null || o.Zones.Length == 0 || zone == null || Array.IndexOf(o.Zones, zone) >= 0;
        }

        /// <summary>Every object whose longest matching alias is the longest found, and that alias.</summary>
        private List<RoomObject> MatchObjects(string phrase, out string alias)
        {
            var best = new List<RoomObject>();
            int bestLen = 0;
            alias = phrase;
            foreach (RoomObject o in Def.Objects)
            {
                int len = 0;
                string hit = null;
                foreach (string a in o.Aliases.Concat(new[] { o.Id.ToLowerInvariant() }))
                    if ((phrase == a || phrase.EndsWith(" " + a) || phrase.StartsWith(a + " ")) && a.Length > len) { len = a.Length; hit = a; }
                if (len == 0) continue;
                if (len > bestLen) { best.Clear(); bestLen = len; alias = hit; }
                if (len == bestLen) best.Add(o);
            }
            return best;
        }

        private string NameIn(List<string> tokens)
        {
            var names = Def.Rules.Where(x => x.Verb == "name" && x.NameValue != null).Select(x => x.NameValue).Distinct().ToList();
            names.AddRange(Lexicon.KnownNames);
            string joined = string.Join(" ", tokens);
            foreach (string prefix in new[] { "", "i am ", "my name is ", "i am called ", "name ", "speak ", "say " })
                foreach (string n in names)
                    if (joined == prefix + n) return n;
            return null;
        }

        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private static readonly HashSet<string> Fillers = new HashSet<string>
        {
            "the", "a", "an", "o", "oh", "thou", "thee", "ye", "you", "please", "now", "to", "your", "thy", "this", "that", "be"
        };

        public static List<string> Tokenize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            var sb = new StringBuilder();
            foreach (char c in raw.ToLowerInvariant())
                sb.Append(char.IsLetter(c) || c == '-' ? c : ' ');
            var words = sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            // "be still" keeps "still" as the verb; drop fillers everywhere except when that leaves nothing.
            var kept = words.Where(w => !Fillers.Contains(w)).ToList();
            return kept.Count > 0 ? kept : words;
        }
    }
}
