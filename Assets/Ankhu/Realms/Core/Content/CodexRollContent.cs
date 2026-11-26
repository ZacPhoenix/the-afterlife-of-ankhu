using System;
using System.Collections.Generic;

namespace Ankhu.Realms.Core
{
    /// <summary>Where an entry is filed on the roll. Craft = the priest's hand-words of the Pavilion, Heart = the words he never used on himself.</summary>
    public enum CodexSection { Craft, Heart, Names, Account }

    /// <summary>Whose hand wrote a line: Ankhu's reed, or Anpu's taller hand in the margin (hints).</summary>
    public enum CodexHand { Ankhu, Anpu }

    /// <summary>A line Ankhu strikes in red and writes again, truer, beneath. The old line is never deleted.</summary>
    public sealed class CodexRevision
    {
        public string Id;
        public string EntryId;
        /// <summary>The line that is struck. Empty means the entry's whole body.</summary>
        public string OldText = string.Empty;
        public string NewText;
        /// <summary>Where it was rewritten, for the small red note beside it.</summary>
        public string Realm = string.Empty;
    }

    /// <summary>A title added after his name on the roll's outer wrap.</summary>
    public sealed class CodexEpithet
    {
        public string Id;
        public string Text;
        /// <summary>Written in red, as scribes wrote what mattered most (true of voice).</summary>
        public bool Rubric;
    }

    /// <summary>An open question Ankhu writes in red in the margin when he enters a room. Cleared when When holds.</summary>
    public sealed class CodexQuestion
    {
        public string Id;
        /// <summary>The scene title the question belongs to (the margin group's heading).</summary>
        public string Scene;
        public string Text;
        /// <summary>What he writes after it, in black, once answered. May be empty.</summary>
        public string Answer = string.Empty;
        /// <summary>Room state that answers it. Empty means it is answered only by code.</summary>
        public Cond[] When = Array.Empty<Cond>();
    }

    /// <summary>
    /// Everything the roll needs beyond the entries in <see cref="CodexContent"/>: sections, people, revisions,
    /// epithets, margin questions, and the roll's own extra entries (flickers, Memory 0, realizations).
    /// Story text marked (ours) is proposed in /mnt/project-files/design/codex/codex_build_spec_v1.md and defers
    /// to the story bible.
    /// </summary>
    public static class CodexRollCatalog
    {
        public const string RenSigns = "S34 N35 Aa1 G43";

        private static readonly HashSet<string> HeartWords = new HashSet<string>
        {
            "word.open", "word.wake", "word.still", "word.seal",
        };

        // Who an entry is about, for the person filters. An entry can belong to several people.
        private static readonly Dictionary<string, string[]> People = new Dictionary<string, string[]>
        {
            { "name.ankhu", new[] { "Self" } },
            { "name.hori", new[] { "Hori" } },
            { "memory.shoulders", new[] { "Hori", "Neferet" } },
            { "memory.reed_boat", new[] { "Hori", "Neferet" } },
            { "memory.door_dawn", new[] { "Father" } },
            { "memory.mother_sand", new[] { "Meryt", "Father" } },
            { "memory.naming", new[] { "Meryt" } },
            { "lore.birth_bricks", new[] { "Meryt" } },
            { "clue.shabti_chest", new[] { "Self" } },
            { "lore.shabti_row", new[] { "Self" } },
            { "acct.f1", new[] { "Neferet" } },
            { "acct.f2", new[] { "Hori" } },
            { "acct.f3", new[] { "Father" } },
            { "mem.dawn_hori", new[] { "Hori", "Neferet" } },
            { "acct.anpu", new[] { "Anpu", "Hori" } },
            { "acct.mother", new[] { "Meryt" } },
            { "acct.hori_reeds", new[] { "Hori" } },
            { "acct.work", new[] { "Self" } },
            { "acct.neferet", new[] { "Neferet" } },
        };

        /// <summary>The order person chips appear in, once each has an entry.</summary>
        public static readonly string[] PersonOrder = { "Hori", "Neferet", "Father", "Meryt", "Anpu", "Self" };

        // A flicker stays faint until the entry that names its person is written.
        private static readonly Dictionary<string, string> FaintUntil = new Dictionary<string, string>
        {
            { "acct.f1", "acct.neferet" },
            { "acct.f2", "name.hori" },
            { "acct.f3", "memory.door_dawn" },
        };

        // Entries whose writing is a keystone: the roll opens itself and his hand writes them.
        private static readonly HashSet<string> PresentedIds = new HashSet<string>
        {
            "lore.codex", "name.ankhu", "name.hori", "mem.dawn_hori",
        };

        public static CodexSection Section(CodexEntry e)
        {
            if (e == null) return CodexSection.Account;
            switch (e.Kind)
            {
                case CodexKind.Word: return HeartWords.Contains(e.Id) ? CodexSection.Heart : CodexSection.Craft;
                case CodexKind.Name:
                case CodexKind.Lore: return CodexSection.Names;
                default: return CodexSection.Account;
            }
        }

        public static string[] PersonsOf(string entryId) =>
            entryId != null && People.TryGetValue(entryId, out string[] p) ? p : Array.Empty<string>();

        /// <summary>The entry that turns this flicker into full ink, or null if it is not a flicker.</summary>
        public static string FaintUntilId(string entryId) =>
            entryId != null && FaintUntil.TryGetValue(entryId, out string id) ? id : null;

        /// <summary>
        /// True when writing this entry should stop the scene, open the roll at it and let his hand write it:
        /// every word of power and memory, plus the listed keystones. Lore and clues only toast.
        /// </summary>
        public static bool IsPresented(CodexEntry e)
        {
            if (e == null) return false;
            if (PresentedIds.Contains(e.Id)) return true;
            if (FaintUntil.ContainsKey(e.Id)) return false;
            return e.Kind == CodexKind.Word || e.Kind == CodexKind.Memory;
        }

        // ------------------------------------------------------------------ revisions

        private static Dictionary<string, CodexRevision> revisions;

        public static CodexRevision Revision(string id)
        {
            if (revisions == null)
            {
                revisions = new Dictionary<string, CodexRevision>();
                foreach (CodexRevision r in AllRevisions()) revisions[r.Id] = r;
            }
            return id != null && revisions.TryGetValue(id, out CodexRevision v) ? v : null;
        }

        private static IEnumerable<CodexRevision> AllRevisions()
        {
            // (ours) Six revisions, one per land from the Water of Ausar on. Wernes only writes.
            yield return new CodexRevision
            {
                Id = "rev.father", EntryId = "memory.door_dawn",
                OldText = "That is where the word seal comes from, for me.",
                NewText = "He went out to keep us. The door closed behind a man who loved us. A seal can be a hand over a cup.",
                Realm = "Field of Offerings",
            };
            yield return new CodexRevision
            {
                Id = "rev.anpu", EntryId = "acct.anpu",
                NewText = "Anpu walked my son down this road. He was not alone. Neither was I.",
                Realm = "Water of Ausar",
            };
            yield return new CodexRevision
            {
                Id = "rev.mother", EntryId = "acct.mother",
                NewText = "Her grief was my grief, said out loud. I left because I could not bear my own.",
                Realm = "Rosetau",
            };
            yield return new CodexRevision
            {
                Id = "rev.hori_reeds", EntryId = "acct.hori_reeds",
                NewText = "It was Heru. And still the joy was real, and it was for Hori.",
                Realm = "Cavern of Seker",
            };
            yield return new CodexRevision
            {
                Id = "rev.work", EntryId = "acct.work",
                NewText = "I worked so I would not have to feel. The work carried my name like a shabti.",
                Realm = "Lake of Fire",
            };
            yield return new CodexRevision
            {
                Id = "rev.neferet", EntryId = "acct.neferet",
                NewText = "Grief chose for her. I could not have carried it for her.",
                Realm = "Field of Offerings",
            };
        }

        // ------------------------------------------------------------------ epithets

        private static Dictionary<string, CodexEpithet> epithets;
        private static List<CodexEpithet> epithetOrder;

        public static CodexEpithet Epithet(string id)
        {
            BuildEpithets();
            return id != null && epithets.TryGetValue(id, out CodexEpithet v) ? v : null;
        }

        public static IReadOnlyList<CodexEpithet> Epithets
        {
            get { BuildEpithets(); return epithetOrder; }
        }

        private static void BuildEpithets()
        {
            if (epithets != null) return;
            epithets = new Dictionary<string, CodexEpithet>();
            epithetOrder = new List<CodexEpithet>
            {
                new CodexEpithet { Id = "ep.ren", Text = "Ankhu" },
                new CodexEpithet { Id = "ep.meryt", Text = "born of Meryt" },
                new CodexEpithet { Id = "ep.hori", Text = "father of Hori" },
                new CodexEpithet { Id = "ep.anpu", Text = "beloved of Anpu" },
                new CodexEpithet { Id = "ep.neferet", Text = "husband of Neferet" },
                new CodexEpithet { Id = "ep.true", Text = "true of voice", Rubric = true },
            };
            foreach (CodexEpithet e in epithetOrder) epithets[e.Id] = e;
        }

        // ------------------------------------------------------------------ margin questions

        private static Dictionary<string, CodexQuestion> questions;

        public static CodexQuestion Question(string id)
        {
            BuildQuestions();
            return id != null && questions.TryGetValue(id, out CodexQuestion v) ? v : null;
        }

        /// <summary>Questions that belong to a scene title, in authored order.</summary>
        public static IEnumerable<CodexQuestion> QuestionsFor(string scene)
        {
            BuildQuestions();
            foreach (CodexQuestion q in questionOrder)
            {
                if (q.Scene == scene) yield return q;
            }
        }

        private static List<CodexQuestion> questionOrder;

        /// <summary>Other content files add their rooms' questions here (the clue design owns the wording).</summary>
        public static void Register(CodexQuestion q)
        {
            BuildQuestions();
            if (q == null || string.IsNullOrEmpty(q.Id) || questions.ContainsKey(q.Id)) return;
            questions[q.Id] = q;
            questionOrder.Add(q);
        }

        private static void BuildQuestions()
        {
            if (questions != null) return;
            questions = new Dictionary<string, CodexQuestion>();
            questionOrder = new List<CodexQuestion>();
            // The First Gate, as the example of the pattern (ours). Room ids from WernesScripts.Arit1.
            Add(new CodexQuestion
            {
                Id = "q.arit1.three", Scene = "The First Gate",
                Text = "Three stand at this gate. What are they called?",
                Answer = "Sad of Voice, Eavesdropper, Face-downward.",
                When = new[] { new Cond("announcer", "known", "answered"), new Cond("guardian", "known", "answered"), new Cond("keeper", "known", "answered") },
            });
            Add(new CodexQuestion
            {
                Id = "q.arit1.open", Scene = "The First Gate",
                Text = "What opens a gate that will not be pushed?",
                Answer = "Being known. Each of them answered to his name.",
                When = new[] { new Cond("gate", "open") },
            });

            // Wernes, written by the next-area thread (object ids follow wernes_build_spec_v2.md section 4).

            // ---- Room A: The Field That Waits
            Add(new CodexQuestion
            {
                Id = "q.fieldwaits.basin", Scene = "The Field That Waits",
                Text = "The basin is full. Why will it not show the sky?",
                Answer = "Because something was still moving it. Water only remembers when it is left alone.",
                When = new[] { new Cond("basin", "full"), new Cond("shabti_a", "asleep"), new Cond("shabti_b", "asleep") },
            });
            Add(new CodexQuestion
            {
                Id = "q.fieldwaits.ledge", Scene = "The Field That Waits",
                Text = "Something small waits on the high ledge. How does a man get up there?",
                Answer = "On the beam, held at the top of its swing.",
                When = new[] { new Cond("falcon", "hand", "perch") },
            });
            Add(new CodexQuestion
            {
                Id = "q.fieldwaits.face", Scene = "The Field That Waits",
                Text = "Whose is the face I could not see in the water?",
                Answer = string.Empty,   // answered by code when Memory A plays in the next room
            });

            // ---- Room B: The Shabti With My Name
            Add(new CodexQuestion
            {
                Id = "q.shabti.water", Scene = "The Shabti With My Name",
                Text = "Three fields, one canal. Why does the water reach none of them?",
                Answer = "It divides when more than one gate is open. One field at a time.",
                When = new[] { new Cond("plot1", "wet"), new Cond("plot2", "wet") },
            });
            Add(new CodexQuestion
            {
                Id = "q.shabti.name", Scene = "The Shabti With My Name",
                Text = "The fine one across the water will not answer to \"shabti\". What is it called?",
                Answer = "Ankhu. It has my name on its chest.",
                When = new[] { new Cond("named_shabti", "read") },
            });
            Add(new CodexQuestion
            {
                Id = "q.shabti.stop", Scene = "The Shabti With My Name",
                Text = "What happens when it stops?",
                Answer = "Everything it held back comes at once. Shut the fields first.",
                When = new[] { new Cond("named_shabti", "still") },
            });

            // ---- Room C: The House With One Door
            Add(new CodexQuestion
            {
                Id = "q.house.shut", Scene = "The House With One Door",
                Text = "Everything here opens. What word shuts a thing and lets it go?",
                Answer = "Khetem. My father's door taught it.",
                When = new[] { new Cond("false_door", "entered") },
            });
            Add(new CodexQuestion
            {
                Id = "q.house.held", Scene = "The House With One Door",
                Text = "I held the door shut. Why is it not done?",
                Answer = "Held is not shut. A sealed door needs no one holding it.",
                When = new[] { new Cond("door", "sealed") },
            });
            Add(new CodexQuestion
            {
                Id = "q.house.plots", Scene = "The House With One Door",
                Text = "The seedlings drown, then dry. How much water is enough?",
                Answer = "Wet, not flooded. Both fields, then shut every way in.",
                When = new[] { new Cond("front_plot", "wet"), new Cond("back_plot", "wet"), new Cond("lower", "shut"), new Cond("ditch", "shut") },
            });

            // ---- The hub: the memory house
            Add(new CodexQuestion
            {
                Id = "q.house_hub.whose", Scene = "The Memory House",
                Text = "Whose house is this, and why have I never lived in it?",
                Answer = "Mine. Given at my burial. I was busy.",
                When = new[] { new Cond("false_door_panels", "3") },
            });
        }

        private static void Add(CodexQuestion q)
        {
            questions[q.Id] = q;
            questionOrder.Add(q);
        }

        // ------------------------------------------------------------------ the roll's own entries

        public static IEnumerable<CodexEntry> ExtraEntries()
        {
            // Flickers from the God's Pavilion: faint until the person is named.
            yield return Acct("acct.f1", "Water rising", "N35A N35A N35A",
                "Water rising. I watched it rise once, from a roof. Someone was laughing beside me.", "The God's Pavilion");
            yield return Acct("acct.f2", "A small weight", "G5",
                "Something small held on to my head. I was holding it up.", "The God's Pavilion");
            yield return Acct("acct.f3", "The shadow on the door", "O31",
                "A man walked out of the light, and his shadow stayed on the door. I was very small.", "The God's Pavilion");

            // Memory 0, after Borrowed Dawn (without his mother, per the Wernes build spec v2, D4).
            yield return new CodexEntry
            {
                Id = "mem.dawn_hori", Kind = CodexKind.Memory, Title = "The dawn he was named", Signs = "N5 G5",
                Body = "The sun came through the door. Neferet was tired and smiling on the mat. I lifted him into the light and said his name so the gods would hear. Hori. I had a son.",
                Realm = "The Borrowed Dawn",
            };

            // Room C, The House With One Door, solved (requested by the Wernes thread).
            yield return new CodexEntry
            {
                Id = "lore.one_door", Kind = CodexKind.Lore, Title = "The one door", Signs = "O31 S20",
                Body = "A door held is not a door shut. My father's door had its bolt on the outside, at the height of my eyes, and I waited by it for years. This one I sealed with my own word, and let go of, and the water stayed out.",
                Effect = "Khetem shuts a thing so no one has to hold it.",
                Realm = "Wernes",
            };

            // Lines he will later strike and write again (ours). Written in their own lands.
            yield return Acct("acct.anpu", "What Anpu did", "E16",
                "Anpu took my son to his table and did not save him. I wore his face for forty years and he did nothing.", "Water of Ausar");
            yield return Acct("acct.mother", "My mother's voice", "S34 A17",
                "Her grief was too loud. I left the house to be free of it.", "Rosetau");
            yield return Acct("acct.hori_reeds", "In the reeds", "G5 M17",
                "I found my son in the reeds. He ran from me, laughing, the way he used to.", "Rosetau");
            yield return Acct("acct.work", "Forty years", "U1",
                "I worked so the dead would be served well. Someone had to.", "Lake of Fire");
            yield return Acct("acct.neferet", "Neferet", "M17 N35 X1",
                "She chose the river over me.", "Water of Ausar");

            // The last sheet, written in the Hall.
            yield return Acct("acct.true_name", "My name", RenSigns,
                "I have read my roll aloud. Some lines I wrote twice. The second line is the true one. This is my name.", "Hall of Two Maats");
        }

        private static CodexEntry Acct(string id, string title, string signs, string body, string realm) =>
            new CodexEntry { Id = id, Kind = CodexKind.Memory, Title = title, Signs = signs, Body = body, Realm = realm };
    }
}
