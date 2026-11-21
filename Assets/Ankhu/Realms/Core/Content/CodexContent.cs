using System.Collections.Generic;

namespace Ankhu.Realms.Core
{
    /// <summary>
    /// Every entry Ankhu can write in Anpu's codex. Bodies are in Ankhu's voice. Words carry their Egyptian
    /// spelling (Gardiner codes) and a one-line effect, shown on the two spell pages at the front of the book.
    /// Quotations marked (Allen) are from T. G. Allen, The Book of the Dead or Going Forth by Day (1974).
    /// </summary>
    public static partial class CodexContent
    {
        public static IEnumerable<CodexEntry> Entries()
        {
            // ------------------------------------------------------------ words of power (spell pages)
            yield return Word("word.open", "Open", "wn", "E34 N35 O31",
                "Opens what is shut: doors, shrines, sluices, the mouths of the dead.",
                "Anpu called it wen. The hare runs, the water ripples, the door swings. In the living world it is a common word. Here it moved stone.");
            yield return Word("word.name", "Name", "rn", "D21 N35",
                "Speak a name to call its owner, or to give your own. The Duat answers what it can name.",
                "Speak a name aloud and it is a key. My own name filled a blank cartouche. The names of the gatekeepers lift their faces.");
            yield return Word("word.seal", "Seal", "khtm", "Aa1 X1 G17 S20",
                "Shuts what is open and holds it shut: sluices, doors, wounds.",
                "The opposite of open, and its twin. A seal is a promise that something stays where it is.");
            yield return Word("word.still", "Still", "htp", "R4 X1 Q3",
                "Brings peace to what is restless: water, wind, a frightened thing.",
                "Hetep. The loaf on the offering mat. Peace, satisfaction, rest. Anpu said rivers remember, so they must be spoken to gently.");
            yield return Word("word.wake", "Wake", "rs", "D21 O34 D4",
                "Rouses what sleeps: guardians, ferrymen, the sleeping eye.",
                "Res. The open eye. It woke the ferry of Wernes, but only after the water was at peace. Some things will not wake into a storm.");

            // ------------------------------------------------------------ names
            yield return Name("name.ankhu", "Ankhu", "ankhu", "S34 N35 Aa1 G43",
                "My own name, written in the cartouche at Wernes. The ankh first, because it is the strongest sign. Mother taught me that.");

            yield return Name("name.hori", "Hori", "hori", "G5 D21 M17",
                "My son. He who belongs to Heru. He held a clay falcon up against the sun and said it was flying to Heru.");

            // ------------------------------------------------------------ lore
            yield return Lore("lore.codex", "The empty codex", "",
                "Anpu gave me this book at the landing of Wernes. The pages were blank. He said every soul who walks the Duat writes its own road. I will write what I learn here, so I do not lose it again.",
                "Wernes");
            yield return Lore("lore.wernes", "Wernes", "N35A M20",
                "The first land. Night-blue fields, gold grain, canals that hold the stars. The dead are given fields here. Far off there are grey figures who work and never look up. Anpu says they forgot. He did not say what.",
                "Wernes");
            yield return Lore("lore.shabti", "The shabti", "A1 Z1",
                "A clay worker, made to answer for the dead when the fields call them to labour. Mine was asleep on its feet with a yoke and two jars. It does exactly what it is told and nothing more. Once woken, it does not stop.",
                "Wernes");
            yield return Lore("lore.house", "The house with one door", "O1 O31",
                "A small walled plot with seedlings. The upper sluice is jammed open and the plot is drowning. A lower sluice drains to a ditch. On the wall stands a false door, the kind we carved in tombs so the dead could pass between the worlds.",
                "Wernes");
            yield return Lore("lore.seal_saves", "What a seal is for", "S20",
                "I sealed the upper sluice and the seedlings stood. Anpu said a seal is not only a wall. Sometimes it is a hand over a cup.",
                "Wernes");
            yield return Lore("lore.wernes_river", "The river of Wernes", "N35A N35A N35A",
                "Wernes floods when the dead come in great numbers. Its river is restless with them. The causeway to the ferry lies under water while the sluice stands open.",
                "Wernes");
            yield return Lore("lore.ferry", "The ferry of Wernes", "P1",
                "A ferry with a carved figure at the prow, who wakes only when the water is calm. It asked who woke it. When I answered, it said I woke it gently.",
                "Wernes");
            yield return Lore("lore.gate_one", "The seven gates", "O31 O1",
                "Anpu says there are seven gates (arits) on the road. Each is kept by three: a keeper, a guardian and an announcer. The old book says: know them and know their names. (Allen, Spell 144: \"Osiris N. knows you and knows your names.\")",
                "Wernes");
            yield return Lore("lore.known_at_gate", "Known at the first gate", "A2",
                "Once I had named all three, the gate opened and the announcer spoke my name: Ankhu, who wore the face of Anpu over the dead. A gate is not opened by force. It is opened by being known. Anpu says he can give me this gate and no other.",
                "Wernes");

            // ------------------------------------------------------------ clues
            yield return Clue("clue.keeper_name", "The first pillar", "D2 D21 N35A",
                "The keeper of the first gate: Face-downward, Numerous of Shapes. A figure carved lying on its face, the way I lay at the pool when I woke. Anpu said we have met. (Allen, Spell 144.)");
            yield return Clue("clue.guardian_name", "The second pillar", "F21 G17",
                "The guardian of the first gate: Eavesdropper. His name was cut in his plinth. He repeated Anpu's words from the road in my own hearing: a son, a father, a husband, a man who stopped speaking. (Allen, Spell 144.)");
            yield return Clue("clue.announcer_name", "The third pillar", "A2 P8",
                "The announcer of the first gate: Sad of Voice. He called out as I came near. Anpu says he announces everyone the same way. (Allen, Spell 144. The word sad is Allen's restoration.)");

            // ------------------------------------------------------------ memories
            yield return Memory("memory.mother_sand", "Mother teaches me my name", "S34",
                "I was six. My father had been gone a year to the war, and he did not come back. Mother wrote my name in the sand by the lamp, and guided my hand through it. A name that is written is a name that is kept, she said. I wrote it a hundred times so she would stop crying. I never told her that.",
                "Wernes");
            yield return Memory("memory.shoulders", "Hori on my shoulders", "G5 A17",
                "The river at sunset. Hori was on my shoulders with his clay falcon held up to the sun. He said it was flying to Heru. I told him it was clay and would break. He said it would not fall, because I was holding him and he was holding it. Neferet called from the bank to bring him home before dark. She said something else after that. I did not hear it. I was listening to the boy.",
                "Wernes");
            yield return Memory("memory.door_dawn", "The door at dawn", "O31 S20",
                "I was six. My father knelt in his soldier's kilt and said: mind your mother, I will bring you something from the north. I did not answer him. Then the door shut, and the bolt was on the outside, at my eye height. He did not come back. That is where the word seal comes from, for me.",
                "Wernes");
            yield return Memory("memory.reed_boat", "Hori's reed boat", "P1 A17",
                "Hori made a boat of reeds when he was three. Neferet said it was to carry me home from the temple, because I never came home early. It sank by evening. He cried and she laughed until he laughed too. I made him another. I came home late the next night anyway.",
                "Wernes");
        }

        private static CodexEntry Word(string id, string title, string translit, string signs, string effect, string body)
            => new CodexEntry { Id = id, Kind = CodexKind.Word, Title = title, Transliteration = translit, Signs = signs, Effect = effect, Body = body, Realm = "Wernes" };

        private static CodexEntry Name(string id, string title, string translit, string signs, string body)
            => new CodexEntry { Id = id, Kind = CodexKind.Name, Title = title, Transliteration = translit, Signs = signs, Body = body, Realm = "Wernes" };

        private static CodexEntry Lore(string id, string title, string signs, string body, string realm)
            => new CodexEntry { Id = id, Kind = CodexKind.Lore, Title = title, Signs = signs, Body = body, Realm = realm };

        private static CodexEntry Clue(string id, string title, string signs, string body)
            => new CodexEntry { Id = id, Kind = CodexKind.Clue, Title = title, Signs = signs, Body = body, Realm = "Wernes" };

        private static CodexEntry Memory(string id, string title, string signs, string body, string realm)
            => new CodexEntry { Id = id, Kind = CodexKind.Memory, Title = title, Signs = signs, Body = body, Realm = realm };
    }
}
