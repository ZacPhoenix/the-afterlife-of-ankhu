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
        // Spellings checked on 24 November 2026. Sources, one per entry:
        //   [F]   R. O. Faulkner, A Concise Dictionary of Middle Egyptian (1962), via the Wiktionary Egyptian lemma pages
        //   [TLA] Thesaurus Linguae Aegyptiae lemma pages (thesaurus-linguae-aegyptiae.de)
        //   [SS]  Tale of the Shipwrecked Sailor, as cited on the Wiktionary lemma page
        //   [PH]  spelled with uniliteral signs from the attested transliteration plus the standard determinative (verify)
        // Full table with links: design/spellcast/hieroglyph_spellings.md in the project files.
        private static readonly Dictionary<string, WordGloss> Verbs = new Dictionary<string, WordGloss>
        {
            // Wernes, the first land
            ["open"] = new WordGloss("open", "wn", "E34 N35 O31"),             // wn, open: hare + water + door [F]
            ["seal"] = new WordGloss("seal", "khetem", "Aa1 X1 G17 S20"),      // khtm, seal: kh t m + seal on cord [F]
            ["wake"] = new WordGloss("wake", "res", "D21 O34 D4"),             // rs, be awake [F] (eye determinative, verify)
            ["still"] = new WordGloss("still", "hetep", "R4 X1 Q3"),           // htp, be at peace: offering table + t p [F]
            ["name"] = new WordGloss("name", "ren", "D21 N35"),                // rn, name [F]
            // The priest's craft (Borrowed Dawn and the teaching rooms)
            ["flow"] = new WordGloss("flow", "baah", "D58 D36 V28 N35A"),      // b-aa-h(i), of liquids: to flow, flood [Wiktionary bꜥḥj] + water det [PH]
            ["bind"] = new WordGloss("bind", "tjes", "S24 O34"),               // tz, tie, knot: knot sign + z [Wiktionary ṯz, Rediu-Khnum stele]
            ["release"] = new WordGloss("release", "weha", "P4 D36 V1 D40"),   // wh-a, loosen, untie bonds [Wiktionary wḥꜥ]
            ["recall"] = new WordGloss("recall", "sekha", "S29 Aa1 M12 G1"),   // s-kh-a, remember, bring to mind [SS 128-129]
            ["echo"] = new WordGloss("echo", "wesheb", "G43 N37 D58 A2"),      // wsb, answer [Wiktionary wšb] + speech det [PH]
            ["sleep"] = new WordGloss("sleep", "sedjer", "O34 M36 D21 A55"),   // sdr, lie down, sleep [SS 41-45] + bed det (verify)
            ["strike"] = new WordGloss("strike", "hui", "V28 G43 D40"),        // hwj, strike [F] (verify)
            ["read"] = new WordGloss("read", "shedi", "N37 D46 Y1"),           // sdj, recite, read [F] (verify)
            // Later lands (taught later, listed so they read as true words)
            ["see"] = new WordGloss("see", "maa", "U2 G1 D4"),                 // m33, see [F]
            ["calm"] = new WordGloss("calm", "qebeb", "N29 D58 D58 N35A"),     // qbb, be cool, calm [F] (verify)
            ["ward"] = new WordGloss("ward", "sa", "V17"),                     // s3, protection amulet [F]
            ["become"] = new WordGloss("become", "kheper", "L1 D21"),          // khpr, come into being [F]
            ["weigh"] = new WordGloss("weigh", "mekhat", "U38"),               // mkh3t, the balance (noun) [F]
            ["give"] = new WordGloss("give", "rdi", "D21 D37"),                // rdj, give [F]
            ["rise"] = new WordGloss("rise", "weben", "G43 D58 N35 N5"),       // wbn, rise, shine: w b n + sun det [F]
            ["remember"] = new WordGloss("remember", "sekha", "S29 Aa1 M12 G1"), // as recall [SS 128-129]
        };

        /// <summary>
        /// Words the painted rooms speak that are not Realm verbs: their own verbs and the directions. Kept apart from
        /// <see cref="Aliases"/> so the Realm rules do not start treating them as known verbs.
        /// </summary>
        private static readonly Dictionary<string, string> RoomVerbAliases = new Dictionary<string, string>
        {
            ["flow"] = "flow", ["bind"] = "bind", ["release"] = "release", ["unbind"] = "release",
            ["wake"] = "wake", ["awaken"] = "wake", ["sleep"] = "sleep", ["recall"] = "recall",
            ["strike"] = "strike", ["ring"] = "strike", ["echo"] = "echo", ["read"] = "read",
        };

        private static readonly Dictionary<string, WordGloss> Directions = new Dictionary<string, WordGloss>
        {
            ["west"] = new WordGloss("west", "imentet", "R14 X1 N25"),          // jmnt, the West: west sign + t + desert det [F]
            ["east"] = new WordGloss("east", "iabtet", "R15 X1 N25"),           // j3bt, the East: east sign + t + desert det [F]
            ["w"] = new WordGloss("west", "imentet", "R14 X1 N25"),             // the parser's short west
            ["e"] = new WordGloss("east", "iabtet", "R15 X1 N25"),              // the parser's short east
        };

        /// <summary>
        /// Object words the Borrowed Dawn and teaching rooms accept after a spell verb ("bind the elevator").
        /// Zac 2026-11-24: every word a room accepts gets its signs, so these fly instead of fading.
        /// </summary>
        private static readonly Dictionary<string, WordGloss> Nouns = new Dictionary<string, WordGloss>
        {
            ["bearer"] = new WordGloss("bearer", "tut", "X1 G43 X1 A53"),        // twt, statue, image: t w t + statue det [F]
            ["statue"] = new WordGloss("statue", "tut", "X1 G43 X1 A53"),        // twt [F]
            ["bell"] = new WordGloss("bell", "seshshet", "Y8 X1"),               // sššt, sistrum, the temple's sounding bronze [F] (no Egyptian bell, verify)
            ["resonator"] = new WordGloss("resonator", "seshshet", "Y8 X1"),     // as bell
            ["elevator"] = new WordGloss("elevator", "fai", "I9 G1 A9"),         // f3j, lift, carry: f 3 + man with load det [F]
            ["lift"] = new WordGloss("lift", "fai", "I9 G1 A9"),                 // f3j [F]
            ["shadow"] = new WordGloss("shadow", "shut", "N37 G43 X1 S35"),      // swt, shade, shadow: sh w t + sunshade det [F]
            ["bridge"] = new WordGloss("bridge", "bridge", "N36 O1"),            // canal + structure, as the Wernes B footbridge (verify)
            ["pendulum"] = new WordGloss("pendulum", "bridge", "N36 O1"),        // the pendulum is the bridge in Borrowed Dawn
            ["inscription"] = new WordGloss("inscription", "sesh", "Y3 Y1"),     // ss, writing: scribe's kit + papyrus roll det [F]
            ["inscriptions"] = new WordGloss("inscriptions", "sesh", "Y3 Y1"),   // ss [F]
            ["gallery"] = new WordGloss("gallery", "at", "D36 X1 O1"),           // at, room, chamber: arm + t + house det [F]
            // Rosetau (2026-11-25). The mother's rooms.
            ["laugh"] = new WordGloss("laugh", "sebet", "O34 D58 V13 A2"),       // sbt, laugh: s b t + man with hand to mouth det [F]
            ["laughter"] = new WordGloss("laugh", "sebet", "O34 D58 V13 A2"),    // sbt [F]
            ["child"] = new WordGloss("child", "khered", "F32 D21 D46 A17"),     // hrd, child: kh r d + child det [F]
            ["boy"] = new WordGloss("child", "khered", "F32 D21 D46 A17"),       // hrd [F]
            ["falcon"] = new WordGloss("falcon", "bik", "G5"),                  // bjk, falcon: falcon sign [F]
            ["door"] = new WordGloss("door", "aa", "D36 G1 O31"),                // 3, door leaf: arm + vulture + door det [F]
            ["doorway"] = new WordGloss("door", "aa", "D36 G1 O31"),             // 3 [F]
            ["linen"] = new WordGloss("linen", "menkhet", "Y5 N35 Aa1 X1 S28"),  // mnht, linen, cloth: mn n kh t + cloth det [F]
            ["band"] = new WordGloss("linen", "menkhet", "Y5 N35 Aa1 X1 S28"),   // mnht [F]
            ["lament"] = new WordGloss("lament", "iakeb", "M17 G1 V31 D58 A28"), // j3kb, mourn, lament: i 3 k b + man with arms raised det [F]
            ["keen"] = new WordGloss("lament", "iakeb", "M17 G1 V31 D58 A28"),   // j3kb [F]
            ["weeping"] = new WordGloss("lament", "iakeb", "M17 G1 V31 D58 A28"),// j3kb, mourning [F]
            ["lamp"] = new WordGloss("lamp", "khebes", "Aa1 D58 S29 Q7"),        // hbs, lamp: kh b s + brazier det [F]
            ["lamps"] = new WordGloss("lamp", "khebes", "Aa1 D58 S29 Q7"),       // hbs [F]
            ["loom"] = new WordGloss("loom", "sekhet", "O34 Aa1 X1 S28"),        // sht, weave: s kh t + cloth det [PH] (verify)
            ["quern"] = new WordGloss("quern", "nedj", "N35 I10 U32"),           // nd, grind: n dj + pestle and mortar det [F]
            ["mother"] = new WordGloss("mother", "mut", "G14 X1 B1"),            // mwt, mother: vulture + t + woman det [F]
            // Rosetau D and E (2026-11-25). The father's rooms.
            ["fire"] = new WordGloss("fire", "sedjet", "O34 I10 X1 Q7"),         // sdt, fire, flame: s dj t + brazier det [F]
            ["flames"] = new WordGloss("fire", "sedjet", "O34 I10 X1 Q7"),
            ["soldier"] = new WordGloss("soldier", "mesha", "G17 N37 D36 A12"),  // msh3, army, soldiery: m sh a + soldier det [F, verify]
            ["shield"] = new WordGloss("shield", "ikem", "M17 V31 G17 F27"),     // ikm, shield: i k m + hide det [F]
            ["bread"] = new WordGloss("bread", "ta", "X1 X4"),                   // t, bread: t + loaf det [F]
            ["mouth"] = new WordGloss("mouth", "ra", "D21 Z1"),                  // r, mouth: mouth + stroke [F]
            ["father"] = new WordGloss("father", "it", "M17 X1 I9 A1"),          // it, father: i t f + man det [F]
        };

        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            ["open"] = "open", ["unseal"] = "open", ["unbar"] = "open", ["unlock"] = "open", ["part"] = "open",
            ["seal"] = "seal", ["close"] = "seal", ["shut"] = "seal", ["bar"] = "seal",
            ["wake"] = "wake", ["awake"] = "wake", ["awaken"] = "wake", ["waken"] = "wake",
            ["still"] = "still", ["hush"] = "still", ["quiet"] = "still", ["peace"] = "still", ["stillness"] = "still",
            ["flow"] = "flow", ["bind"] = "bind", ["release"] = "release", ["loose"] = "release",
            ["recall"] = "recall", ["echo"] = "echo", ["see"] = "see", ["calm"] = "calm", ["ward"] = "ward",
            // The Egyptian itself, as Ankhu recovers it in Wernes.
            ["wen"] = "open", ["wn"] = "open", ["res"] = "wake", ["hetep"] = "still", ["htp"] = "still", ["khetem"] = "seal",
            ["tjes"] = "bind", ["wekha"] = "release", ["ren"] = "name",
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
            ["auset"] = new WordGloss("Auset", "aset", "Q1 X1 B1", false),        // 3st, Isis: seat + t + woman det [F]
            ["isis"] = new WordGloss("Auset", "aset", "Q1 X1 B1", false),
            ["nebethet"] = new WordGloss("Nebet-Het", "nebet-hut", "O9 X1 B1", false), // nbt-hwt, Nephthys: basket on house + t + woman det [F]
            ["nephthys"] = new WordGloss("Nebet-Het", "nebet-hut", "O9 X1 B1", false),
            // Rosetau D: the father and his two comrades. Common New Kingdom names (Ranke, Die altagyptischen Personennamen).
            ["nakht"] = new WordGloss("Nakht", "nakht", "N35 Aa1 X1 D40 A1", true),  // nht, strong: n kh t + arm with stick + man det [F]
            ["paser"] = new WordGloss("Paser", "paser", "G40 G1 S29 D21 A21", true), // p3-sr, the official: pintail + vulture + s r + official det [Ranke]
            ["huy"] = new WordGloss("Huy", "huy", "V28 G43 M17 M17 A1", true),      // hwy: h w y + man det [Ranke, verify]
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

        /// <summary>
        /// The gloss for one typed token in any room: a Realm verb or alias, a painted room verb, a direction or a
        /// Name. Returns false for words with no Egyptian form (nouns such as "lift"), which simply fade.
        /// </summary>
        public static bool TryGlossToken(string token, out WordGloss gloss)
        {
            gloss = null;
            if (string.IsNullOrEmpty(token)) return false;
            string t = token.ToLowerInvariant();
            string verb = VerbOf(t);
            if (verb == null) RoomVerbAliases.TryGetValue(t, out verb);
            if (verb != null && Verbs.TryGetValue(verb, out gloss))
            {
                // Show the word as typed (the player's English), with the canonical Egyptian.
                gloss = new WordGloss(t, gloss.Transliteration, string.Join(" ", gloss.Signs), gloss.Cartouche);
                return true;
            }
            if (Directions.TryGetValue(t, out gloss)) return true;
            if (Nouns.TryGetValue(t, out gloss)) return true;
            if (Names.TryGetValue(t, out gloss)) return true;
            gloss = null;
            return false;
        }

        public static IEnumerable<string> AllVerbs => Verbs.Keys.ToList();

        /// <summary>Every direction and noun token the lexicon glosses (for the glyph coverage check).</summary>
        public static IEnumerable<string> AllNounAndDirectionTokens => Directions.Keys.Concat(Nouns.Keys).ToList();
    }
}
