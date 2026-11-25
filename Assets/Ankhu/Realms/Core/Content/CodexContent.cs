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

            yield return Word("word.bind", "Bind", "tjes", "S24 O34",
                "Holds a moving thing where it is, with a thread of light. Held is not resting.",
                "Tjes. To tie, to knot. The beam stopped at the top and waited for me. The shabti would not take it. A held worker is still working.");
            yield return Word("word.release", "Release", "weha", "P4 D36 V1 D40",
                "Lets go of what is bound.",
                "Every knot is a promise to come back and untie it. The water would not settle while my thread lay across it.");

            yield return Word("word.flow", "Flow", "baah", "D58 D36 V28 N35A",
                "Brings water up and sends it on: basins, channels, dry beds.",
                "Baah. To flow, to flood, to well up. The midwife's basin was cracked dry, and the word filled it.");
            yield return Word("word.echo", "Echo", "wesheb", "G43 N37 D58 A2",
                "Answers a sound so it stays: a bell, a voice, a cry that would fade.",
                "Wesheb. To answer. A sound that is answered does not die away. I answered my mother's cry and it hung in the room.");

            // ------------------------------------------------------------ names
            yield return Name("name.ankhu", "Ankhu", "ankhu", "S34 N35 Aa1 G43",
                "My own name, written in the cartouche at the First Gate. The ankh first, because it is the strongest sign. Mother taught me that.");

            yield return Name("name.hori", "Hori", "hori", "G5 D21 M17",
                "My son. He who belongs to Heru. He held a clay falcon up against the sun and said it was flying to Heru.");

            // ------------------------------------------------------------ lore
            yield return Lore("lore.codex", "The empty codex", "",
                "Anpu gave me this roll at the First Gate. It was blank. He said every soul who walks the Duat writes its own road. I will write what I learn here, so I do not lose it again.",
                "Wernes");
            yield return Lore("lore.wernes", "Wernes", "N35A M20",
                "The first land. Night-blue fields, gold grain, canals that hold the stars. The dead are given fields here. Far off there are grey figures who work and never look up. Anpu says they forgot. He did not say what.",
                "Wernes");
            yield return Lore("lore.shabti", "The shabti", "A1 Z1",
                "A clay worker, made to answer for the dead when the fields call them to labour. Mine was asleep on its feet with a yoke and two jars. It does exactly what it is told and nothing more. Once woken, it does not stop.",
                "Wernes");
            yield return Lore("lore.field_waits", "The field that waits", "M20 N37",
                "A field worked by two clay servants and a shaduf that never stopped nodding. The water only went still when everyone rested, my thread included. In it, a low sun, and a man carrying a child. I looked at the child. I kept the clay falcon.",
                "Wernes");
            yield return Lore("lore.house", "The house with one door", "O1 O31",
                "A small walled plot with seedlings. The upper sluice is jammed open and the plot is drowning. A lower sluice drains to a ditch. On the wall stands a false door, the kind we carved in tombs so the dead could pass between the worlds.",
                "Wernes");
            yield return Lore("lore.birth_bricks", "The birth bricks", "F31 S29 Aa1 N35 X1",
                "Four bricks for a mother to crouch on, painted with the face of Meskhenet, who stands at every birth and writes what the child will be. Mine were scraped bare. When the four signs of my name lay on them in order, they woke and I heard my mother cry that I lived.",
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

            yield return Lore("lore.shabti_row", "Shabti Row", "A1 Z2",
                "A long field with three plots, three sluices and three blank clay workers. The canal behind them is brimming, fuller than any canal should be. At the far end a fifth worker, finer and painted, carries water without rest. Its channel is worn deep.",
                "Wernes");

            // ------------------------------------------------------------ clues
            yield return Clue("clue.shabti_chest", "The name on its chest", "S34 N35 Aa1 G43",
                "The painted shabti at the end of the row has a cartouche on its chest, and the name in it is mine. Anpu says it was put in my tomb to answer for me when the fields call. It does not answer to \"shabti\". It answers to Ankhu.");
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
            yield return Memory("memory.naming", "My mother names me", "S34 A17",
                "Before anything I can remember. The one-room house, a midwife, my mother worn out and laughing with me in her arms. Ankhu, she said. He lives. Say it so the gods hear. She was the loudest person in my life, and at the start that was joy.",
                "Wernes");
            yield return Memory("memory.reed_boat", "Hori's reed boat", "P1 A17",
                "Hori made a boat of reeds when he was three. Neferet said it was to carry me home from the temple, because I never came home early. It sank by evening. He cried and she laughed until he laughed too. I made him another. I came home late the next night anyway.",
                "Wernes");

            // Rosetau (2026-11-25). The mother's rooms. Design: docs/ROSETAU_DESIGN.md.
            yield return Lore("lore.rosetau", "Rosetau", "D21 G43 O1",
                "The place of passages, where the roads of the dead go down by water and by land. Spell 117 of the Book of the Dead is the spell for taking the way here. Anpu brings the dead as far as the first step and no further.",
                "Rosetau");
            yield return Lore("lore.mothers_house", "The house in Abdju", "O1 G14 X1 B1",
                "One room of mud brick, a loom, a quern, a lamp. My mother wove for other women and ground grain for us. She kept a faience of the young Heru over the bread oven, the one with his finger at his lips.",
                "Rosetau");
            yield return Memory("memory.news", "Father's shield", "F32 D21 D46 A17",
                "I was six. A man from the garrison brought my father's shield to our door and not my father. My mother sat down in the street and screamed until the neighbours came out. I held my ears shut. The pay stopped that month. I hated her for the noise, and I never told anyone.",
                "Rosetau");
            yield return Memory("memory.hush", "Hush", "R4 X1 Q3",
                "Every night for a year she wept on her mat, and every night I knelt by her and put my hand over her mouth. They'll hear you next door. I thought I was helping her.",
                "Rosetau");
            yield return Memory("memory.leaving", "The grain boat", "P1 G14 X1 B1",
                "At fourteen I took a grain boat to the temple school, because they took boys who could copy signs and there was bread in it. She stood on the bank and did not wave. I sent her half of every ration. It was for her. It was also somewhere quiet.",
                "Rosetau");
            yield return Memory("memory.old_mother", "My mother, old", "G14 X1 B1",
                "She lived with Neferet and me for twenty years after Hori was born. Still the loudest woman in the street. Hori thought she was the funniest person alive. She died old, in her sleep, with thread in her lap. I washed her and wrapped her myself, every layer, and I took my time.",
                "Rosetau");

            // Rosetau D and E (2026-11-25). The father's rooms.
            yield return Lore("lore.two_ways", "The Book of Two Ways", "N31 N31",
                "A map of the hereafter painted on the floor boards of coffins from the Hare nome at el-Bersha, in the Middle Kingdom. A black road of water and a blue road of land, with a channel of fire between them. It is part of the Coffin Texts, spells 1029 to 1130 (Lesko, The Ancient Egyptian Book of Two Ways, 1972).",
                "Rosetau");
            yield return Lore("lore.opening_of_the_mouth", "The Opening of the Mouth", "D21 Z1",
                "The rite that lets the dead eat, drink and speak. A priest touches the mouth of the mummy or the statue with an adze. In the Papyrus of Hunefer (British Museum EA 9901) Anpu holds the mummy upright at the tomb door while the women mourn in front of it. I served at hundreds.",
                "Rosetau");
            yield return Lore("lore.abdju_stela", "The stelae of Abdju", "O1 G14",
                "Abdju is the town of Ausar, and families who buried their dead somewhere else, or never got the body back, set up a stela there so the dead could share the god's offerings. The usual words are the offering formula: an offering which the king gives, of bread and beer, oxen and fowl.",
                "Rosetau");
            yield return Memory("memory.father", "Nakht on the river bank", "G17 N37 D36 A12",
                "A river bank at evening. My father lifted me onto his shoulders and put a reed whistle in my hand, and taught me a tune of four notes, falling. Blow it when you want me, he said. I'll hear it from the north. He went north with Paser and Huy. Only his shield came back.",
                "Rosetau");
            yield return Memory("memory.whistle", "The whistle in the courtyard", "G14 X1 B1",
                "Years later, in my own courtyard, my mother played Hori a tune on a reed whistle, badly, every night he let her. Four notes, falling. I never asked her where she had learned it. It was his.",
                "Rosetau");
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
