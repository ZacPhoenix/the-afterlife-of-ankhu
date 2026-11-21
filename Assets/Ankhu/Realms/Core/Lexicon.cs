using System.Collections.Generic;
using System.Linq;

namespace Ankhu.Realms.Core
{
    /// <summary>
    /// The words of power: English the player types, the canonical verb it means, and the Egyptian it becomes.
    /// Spellings follow standard Middle Egyptian dictionary forms (Faulkner), simplified to the signs the
    /// glyph atlas renders. Gardiner codes are space separated.
    /// </summary>
    public static class Lexicon
    {
        private static readonly Dictionary<string, WordGloss> Verbs = new Dictionary<string, WordGloss>
        {
            // Wernes, the first land
            ["open"] = new WordGloss("open", "wn", "E34 N35 O31"),
            ["seal"] = new WordGloss("seal", "khetem", "Aa1 X1 G17 S20"),
            ["wake"] = new WordGloss("wake", "res", "D21 O34 D4"),
            ["still"] = new WordGloss("still", "hetep", "R4 X1 Q3"),
            ["name"] = new WordGloss("name", "ren", "D21 N35"),
            // Water of Ausar and later lands (taught later, listed so they read as true words)
            ["flow"] = new WordGloss("flow", "bes", "D58 O34 N35A"),
            ["bind"] = new WordGloss("bind", "tjes", "V13 O34 V1"),
            ["release"] = new WordGloss("release", "wekha", "G43 Aa1 V28"),
            ["recall"] = new WordGloss("recall", "sekha", "O34 Aa1 D28"),
            ["echo"] = new WordGloss("echo", "wesheb", "G43 N37 D58 A2"),
            ["see"] = new WordGloss("see", "maa", "U2 G1 D4"),
            ["calm"] = new WordGloss("calm", "qeb", "N29 D58 N35A"),
            ["ward"] = new WordGloss("ward", "sa", "V17"),
            ["become"] = new WordGloss("become", "kheper", "L1 D21"),
            ["weigh"] = new WordGloss("weigh", "mekhat", "U38"),
            ["give"] = new WordGloss("give", "rdi", "D21 D37"),
            ["rise"] = new WordGloss("rise", "weben", "N5 G43 D58 N35"),
            ["remember"] = new WordGloss("remember", "sekha", "O34 Aa1 D28"),
        };

        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            ["open"] = "open", ["unseal"] = "open", ["unbar"] = "open", ["unlock"] = "open", ["part"] = "open",
            ["seal"] = "seal", ["close"] = "seal", ["shut"] = "seal", ["bar"] = "seal",
            ["wake"] = "wake", ["awake"] = "wake", ["awaken"] = "wake", ["waken"] = "wake",
            ["still"] = "still", ["hush"] = "still", ["quiet"] = "still", ["peace"] = "still", ["stillness"] = "still",
            ["flow"] = "flow", ["bind"] = "bind", ["release"] = "release", ["loose"] = "release",
            ["recall"] = "recall", ["echo"] = "echo", ["see"] = "see", ["calm"] = "calm", ["ward"] = "ward",
            ["become"] = "become", ["weigh"] = "weigh", ["give"] = "give", ["rise"] = "rise", ["remember"] = "remember",
        };

        private static readonly Dictionary<string, WordGloss> Names = new Dictionary<string, WordGloss>
        {
            ["ankhu"] = new WordGloss("Ankhu", "ankhu", "S34 N35 Aa1 G43", true),
            ["anpu"] = new WordGloss("Anpu", "inpu", "M17 N35 Q3 G43 E15", false),
            ["hori"] = new WordGloss("Hori", "hori", "G5 D21 M17", true),
            ["neferet"] = new WordGloss("Neferet", "neferet", "F35 X1 B1", true),
            ["ausar"] = new WordGloss("Ausar", "wsir", "Q1 D4 A40", false),
            ["heru"] = new WordGloss("Heru", "heru", "G5", false),
        };

        public static IEnumerable<string> KnownNames => Names.Keys;

        /// <summary>The canonical verb for a typed word, or null.</summary>
        public static string VerbOf(string token) => token != null && Aliases.TryGetValue(token, out string v) ? v : null;

        public static bool IsKnownWord(string token) => VerbOf(token) != null || (token != null && Names.ContainsKey(token));

        public static WordGloss Gloss(string verb) => Verbs.TryGetValue(verb, out WordGloss g) ? g : new WordGloss(verb, verb, string.Empty);

        public static WordGloss NameGloss(string name)
        {
            if (name != null && Names.TryGetValue(name, out WordGloss g)) return g;
            string shown = string.IsNullOrEmpty(name) ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
            return new WordGloss(shown, name, "D21 N35", true);
        }

        public static IEnumerable<string> AllVerbs => Verbs.Keys.ToList();
    }
}
