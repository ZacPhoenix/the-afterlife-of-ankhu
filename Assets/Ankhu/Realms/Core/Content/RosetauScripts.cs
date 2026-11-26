using System.Collections.Generic;

namespace Ankhu.Realms.Core.Content
{
    /// <summary>
    /// Rosetau, the realm after Wernes: the mother's rooms, painted 2.5D. Design and sources:
    /// docs/ROSETAU_DESIGN.md. Five rooms in play order. Each solve gives one painted memory of the mother,
    /// shown in the living papyrus style. The gods here help. Anpu waits at the threshold of the first room.
    /// Rules are deterministic and every utterance, deed or wait is one tick, as in Wernes.
    /// </summary>
    public static class RosetauScripts
    {
        public const string LaughA = "Rosetau_A_Laugh";
        public const string MournersB = "Rosetau_B_Mourners";
        public const string MotherC = "Rosetau_C_Mother";
        public const string TwoWaysD = "Rosetau_D_TwoWays";
        public const string FinaleE = "Rosetau_E_Finale";

        public static readonly string[] Scenes = { LaughA, MournersB, MotherC, TwoWaysD, FinaleE };

        public static IEnumerable<SceneScript> All()
        {
            yield return BuildLaughA();
            yield return BuildMournersB();
            yield return BuildMotherC();
            yield return BuildTwoWaysD();
            yield return BuildFinaleE();
        }

        public static SceneScript ByScene(string scene)
        {
            foreach (SceneScript s in All()) if (s.SceneName == scene) return s;
            return null;
        }

        private sealed class Lines
        {
            private readonly string scene;
            private readonly Dictionary<Speaker, int> counts = new Dictionary<Speaker, int>();
            public Lines(string scene) { this.scene = scene; }
            private string Key(Speaker who)
            {
                counts.TryGetValue(who, out int n);
                counts[who] = ++n;
                return $"dialogue.{who.ToString().ToLowerInvariant()}.{scene}.{n}";
            }
            public Step S(Speaker who, string text) => Step.Say(who, Key(who), text);
            public Step M(string image, Speaker who, string text) => Step.Memory(image, who, Key(who), text);
        }

        private static RoomObject Obj(string id, string name, string initial, WordGloss gloss, string[] zones, params string[] aliases)
            => new RoomObject { Id = id, Name = name, Initial = initial, Gloss = gloss, Zones = zones ?? new string[0], Aliases = aliases };

        private static Cond C(string id, params string[] states) => new Cond(id, states);

        // Glosses. Spellings and sources are listed in Lexicon.cs next to the same words.
        private static readonly WordGloss LaughG = new WordGloss("laugh", "sebet", "O34 D58 V13 A2");
        private static readonly WordGloss DoorG = new WordGloss("door", "aa", "D36 G1 O31");
        private static readonly WordGloss ChildG = new WordGloss("child", "khered", "F32 D21 D46 A17");
        private static readonly WordGloss FalconG = new WordGloss("falcon", "bik", "G5");
        private static readonly WordGloss AusetG = new WordGloss("Auset", "aset", "Q1 X1 B1");
        private static readonly WordGloss NebetHetG = new WordGloss("Nebet-Het", "nebet-hut", "O9 X1 B1");
        private static readonly WordGloss LinenG = new WordGloss("linen", "menkhet", "Y5 N35 Aa1 X1 S28");
        private static readonly WordGloss KeenG = new WordGloss("lament", "iakeb", "M17 G1 V31 D58 A28");
        private static readonly WordGloss LampG = new WordGloss("lamp", "khebes", "Aa1 D58 S29 Q7");
        private static readonly WordGloss LoomG = new WordGloss("loom", "sekhet", "O34 Aa1 X1 S28");
        private static readonly WordGloss QuernG = new WordGloss("quern", "nedj", "N35 I10 U32");
        private static readonly WordGloss MotherG = new WordGloss("mother", "mut", "G14 X1 B1");
        private static readonly WordGloss FireG = new WordGloss("fire", "sedjet", "O34 I10 X1 Q7");
        private static readonly WordGloss SoldierG = new WordGloss("soldier", "mesha", "G17 N37 D36 A12");
        private static readonly WordGloss ShieldG = new WordGloss("shield", "ikem", "M17 V31 G17 F27");
        private static readonly WordGloss BreadG = new WordGloss("bread", "ta", "X1 X4");
        private static readonly WordGloss MouthG = new WordGloss("mouth", "ra", "D21 Z1");
        private static readonly WordGloss FatherG = new WordGloss("father", "it", "M17 X1 I9 A1");
        private static readonly WordGloss NakhtG = new WordGloss("Nakht", "nakht", "N35 Aa1 X1 D40 A1", true);
        private static readonly WordGloss PaserG = new WordGloss("Paser", "paser", "G40 G1 S29 D21 A21", true);
        private static readonly WordGloss HuyG = new WordGloss("Huy", "huy", "V28 G43 M17 M17 A1", true);

        // ---------------------------------------------------------------- Room A: The Laugh in the Dark

        /// <summary>
        /// Three low doorways in a dark wall. A child's laugh comes every few ticks and bounces off all three, then
        /// thins and is gone. Echo (wesheb) holds it, and while it is held it answers from one doorway only, which
        /// lights. Open the lit doorway (standing at it) and the next round begins. The true doorway is the middle,
        /// then the left, then the right. A dark doorway is only stone. After the third, the child stands in the
        /// last doorway: the young Heru, finger at his lips. Give him Hori's clay falcon. Goal: falcon given.
        /// </summary>
        private static SceneScript BuildLaughA()
        {
            var L = new Lines("rosetau_a");
            var room = new RoomDef
            {
                Id = "rosetau_a",
                Title = "The Laugh in the Dark",
                Verbs = new HashSet<string> { "open", "wake", "still", "release", "echo" },
                Goal = new[] { C("falcon", "given") },
            };
            room.Objects.Add(Obj("laugh", "laugh", "silent", LaughG, null, "laugh", "laughter", "laughing", "giggle", "voice", "sound", "child's laugh"));
            string[] side = { "", "left", "middle", "right" };
            for (int k = 1; k <= 3; k++)
            {
                room.Objects.Add(Obj("mouth" + k, side[k] + " doorway", "dark", DoorG, new[] { "mouth" + k },
                    "door", "doorway", "mouth", "passage", "opening", side[k] + " door", side[k] + " doorway", side[k] + " mouth", side[k] + " passage"));
            }
            room.Objects.Add(Obj("round", "round", "1", null, null));
            room.Objects.Add(Obj("clock", "clock", "1", null, null));
            room.Objects.Add(Obj("heard", "heard", "no", null, null));
            room.Objects.Add(Obj("child", "child", "hidden", ChildG, null, "child", "boy", "heru", "young heru", "heru the child", "hori", "son", "kid"));
            room.Objects.Add(Obj("falcon", "clay falcon", "hand", FalconG, null, "falcon", "clay falcon", "toy", "bird", "hori's falcon"));

            // The true doorway for each round. Round 4 means all three are found.
            int TrueMouth(string round) => round == "1" ? 2 : round == "2" ? 1 : round == "3" ? 3 : 0;

            // The world clock. The laugh comes when the clock reaches 2, thins, then is gone. Held, it stays, and the
            // true doorway lights while it is held.
            room.Tick = st =>
            {
                switch (st["laugh"])
                {
                    case "sounding": st["laugh"] = "fading"; break;
                    case "fading": st["laugh"] = "silent"; st["clock"] = "0"; break;
                    case "silent":
                        if (st["round"] == "4") break;
                        int c = int.Parse(st["clock"]) + 1;
                        if (c >= 2) { st["laugh"] = "sounding"; st["clock"] = "0"; }
                        else st["clock"] = c.ToString();
                        break;
                }
                int t = TrueMouth(st["round"]);
                for (int k = 1; k <= 3; k++)
                {
                    string id = "mouth" + k;
                    if (st[id] == "open") continue;
                    st[id] = st["laugh"] == "held" && k == t ? "lit" : "dark";
                }
            };

            room.Rules.Add(new SpellRule { Verb = "auto", Target = "heard", To = "yes", Requires = new[] { C("laugh", "sounding") }, Event = "first_laugh" });

            room.Rules.Add(new SpellRule { Verb = "echo", Target = "laugh", From = new[] { "sounding", "fading" }, To = "held", Event = "laugh_held",
                Success = "Wesheb. You answer the laugh, and it answers back from one doorway only.",
                Refusal = "There is nothing to answer yet. Wait for it." });

            string[] nextRound = { "", "3", "2", "4" }; // mouth1 ends round 2, mouth2 ends round 1, mouth3 ends round 3
            string[] needRound = { "", "2", "1", "3" };
            for (int k = 1; k <= 3; k++)
            {
                room.Rules.Add(new SpellRule { Verb = "open", Target = "mouth" + k, From = new[] { "lit" }, To = "open",
                    Requires = new[] { C("round", needRound[k]) },
                    Also = new[] { ("round", nextRound[k]), ("laugh", "silent"), ("clock", "0") },
                    Event = "found_" + needRound[k],
                    Success = "Wen. The doorway opens on a small dry chamber.",
                    Refusal = "Only stone. The doorway ends a step in.", RefusalEvent = "wrong_mouth" });
            }

            room.Rules.Add(new SpellRule { Verb = "auto", Target = "child", To = "seen", Requires = new[] { C("round", "4"), C("child", "hidden") }, Event = "child_seen" });
            room.Rules.Add(new SpellRule { Verb = "give", Target = "falcon", From = new[] { "hand" }, To = "given", Requires = new[] { C("child", "seen") },
                Also = new[] { ("child", "holding") }, Event = "falcon_given",
                Success = "Rdi. He takes the falcon in both hands.", Refusal = "There is no one here to give it to." });
            room.Rules.Add(new SpellRule { Verb = "give", Target = "child", From = new[] { "seen" }, To = "holding", Requires = new[] { C("falcon", "hand") },
                Also = new[] { ("falcon", "given") }, Event = "falcon_given",
                Success = "Rdi. He takes the falcon in both hands.", Refusal = "Not yet. Find him first." });

            room.Hints.Add(new Hint { When = new[] { C("laugh", "silent"), C("round", "1", "2", "3") }, Text = "Wait. The laugh comes again." });
            room.Hints.Add(new Hint { When = new[] { C("laugh", "sounding", "fading") }, Text = "Answer it, so it stays, and listen for where it answers from." });
            room.Hints.Add(new Hint { When = new[] { C("laugh", "held") }, Text = "One doorway is lit. Stand at it and open it." });
            room.Hints.Add(new Hint { When = new[] { C("child", "seen") }, Text = "You are still carrying the clay falcon from the field." });

            var s = new SceneScript { SceneName = LaughA, Realm = "Rosetau", Title = "The Laugh in the Dark", Room = room,
                CodexOnEntry = new[] { "word.echo", "word.open", "lore.rosetau" } };
            s.Usables.Add(new Usable { Id = "read_lintel", Prompt = "Read the lintel", Zone = "entry" });

            s.Beats.Add(new Beat("arrive", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Title("ROSETAU", 3f),
                Step.Title("THE LAUGH IN THE DARK", 3.5f),
                Step.Caption("A passage of dark stone, going down. Three low doorways in the far wall. Anpu stops on the last step."),
                L.S(Speaker.Anpu, "This is Rosetau. The roads here go by water and by land, and all of them go down."),
                L.S(Speaker.Ankhu, "You're stopping? You walked me through every room in Wernes."),
                L.S(Speaker.Anpu, "Those rooms were fields and houses. These rooms are your mother's. I was never a guest in her house."),
                L.S(Speaker.Ankhu, "Neither was I, much."),
                L.S(Speaker.Anpu, "Then go and be one. I will wait on this step."),
                Step.Free()));

            // Book of the Dead spell 117, its title (Faulkner, The Ancient Egyptian Book of the Dead, 1985, p. 113).
            s.Beats.Add(new Beat("read_lintel", TriggerKind.Use, "read_lintel",
                Step.Caption("As it is written: \"Spell for taking the way to Rosetau.\""),
                L.S(Speaker.Ankhu, "We cut that over plenty of tomb doors. I never once thought about who'd walk under it.")) { Once = false });

            s.Beats.Add(new Beat("first_laugh", TriggerKind.Event, "first_laugh",
                Step.Freeze(),
                Step.Caption("A child laughs somewhere in the stone. The sound comes back from all three doorways at once."),
                L.S(Speaker.Ankhu, "Hori? Hori, it's me."),
                Step.Caption("The laugh thins away."),
                L.S(Speaker.Ankhu, "In a tomb shaft you call out and listen for where the room opens. It answers you from the open side."),
                Step.Free()));

            s.Beats.Add(new Beat("held", TriggerKind.Event, "laugh_held",
                Step.Caption("The laugh hangs in the air. Light gathers in one doorway.")) { Once = false });
            s.Beats.Add(new Beat("wrong", TriggerKind.Event, "wrong_mouth",
                L.S(Speaker.Ankhu, "Wrong one. It's just a niche.")) { Once = false });

            s.Beats.Add(new Beat("found1", TriggerKind.Event, "found_1",
                Step.Caption("In the chamber, a wooden spinning top lies on its side."),
                L.S(Speaker.Ankhu, "I made Hori one like that. Cedar, from an offcut of a coffin lid. He spun it on the bread board till Neferet took it off him.")));
            s.Beats.Add(new Beat("found2", TriggerKind.Event, "found_2",
                Step.Caption("In the chamber, five knucklebones and a reed whistle."),
                L.S(Speaker.Ankhu, "Those aren't Hori's. Those are mine. I lost that whistle in the canal the year Father went north.")));
            s.Beats.Add(new Beat("found3", TriggerKind.Event, "found_3",
                Step.Caption("In the chamber, a child's sandal woven from papyrus, very small."),
                L.S(Speaker.Ankhu, "She kept my sandal. I didn't know she kept anything.")));

            s.Beats.Add(new Beat("child", TriggerKind.Event, "child_seen",
                Step.Freeze(),
                Step.Caption("A boy stands in the last doorway. His head is shaved but for one side lock, and he holds a finger to his lips."),
                L.S(Speaker.Ankhu, "Hori."),
                Step.Caption("The boy keeps his finger at his lips. A small cobra rises on his brow."),
                L.S(Speaker.Ankhu, "No. You're the young Heru. Mother had a faience of you over the bread oven, finger to your mouth like that, to keep the house quiet."),
                L.S(Speaker.Ankhu, "It never worked on her."),
                Step.Learn("give"),
                L.S(Speaker.Ankhu, "I've carried this since the field. Rdi. It should go to a falcon."),
                Step.Free()));

            s.Beats.Add(new Beat("murmur", TriggerKind.Idle, "", L.S(Speaker.Ankhu, "It comes, and it goes. I only need it to stay once."))
                { Value = 45f });

            s.Beats.Add(new Beat("solved", TriggerKind.Solved, "",
                Step.Freeze(),
                Step.Caption("The boy holds the clay falcon up to the dark and laughs out loud. Then he steps back through the doorway, and it stays open."),
                Step.Cue("flashback:ros_news"),
                L.S(Speaker.Ankhu, "I was six. A man from the garrison brought Father's shield home and not Father. The pay stopped that month."),
                L.S(Speaker.Ankhu, "She screamed in the street till the neighbours came out. I held my ears shut. I've never told anyone I hated her for it."),
                Step.Write("memory.news"),
                Step.Wait(1.5f),
                Step.Next()));
            return s;
        }

        // ---------------------------------------------------------------- Room B: The Room of the Mourners

        /// <summary>
        /// Ausar lies wrapped on a lion bier. Auset kneels at his head and Nebet-Het at his feet, both with linen
        /// bands tied over their mouths in Ankhu's own knot. Still (hetep) on a mourner is refused, and calls up the
        /// memory of the boy hushing his mother. Release (weha) each band at her side and she keens. Echo (wesheb)
        /// answers the two of them, and while the lament is answered and both keen, one lamp lights each tick.
        /// A lone keener runs out of breath after two ticks and must be released again. Four lamps open the door.
        /// Goal: door open.
        /// </summary>
        private static SceneScript BuildMournersB()
        {
            var L = new Lines("rosetau_b");
            var room = new RoomDef
            {
                Id = "rosetau_b",
                Title = "The Room of the Mourners",
                Verbs = new HashSet<string> { "open", "wake", "still", "release", "echo", "give" },
                Goal = new[] { C("door", "open") },
            };
            var auset = Obj("auset", "Auset", "bound", AusetG, new[] { "head" },
                "auset", "isis", "mourner", "first mourner", "woman", "kneeling woman", "sister", "kite", "her", "mourner at the head", "aset");
            var nebet = Obj("nebethet", "Nebet-Het", "bound", NebetHetG, new[] { "feet" },
                "nebet-het", "nebet het", "nebethet", "nephthys", "mourner", "second mourner", "woman", "kneeling woman", "sister", "kite", "her", "mourner at the feet");
            room.Objects.Add(auset);
            room.Objects.Add(nebet);
            room.Objects.Add(Obj("band", "linen band", "tied", LinenG, null, "band", "bands", "linen", "gag", "knot", "wrapping", "bandage"));
            room.Objects.Add(Obj("lament", "lament", "unanswered", KeenG, null, "lament", "keen", "keening", "mourning", "wail", "wailing", "voices", "song", "cry"));
            room.Objects.Add(Obj("lamps", "lamps", "0", LampG, null, "lamp", "lamps", "light"));
            room.Objects.Add(Obj("door", "door", "sealed", DoorG, new[] { "door" }, "door", "far door", "doorway", "seal"));
            room.Objects.Add(Obj("breath_a", "breath", "0", null, null));
            room.Objects.Add(Obj("breath_b", "breath", "0", null, null));
            room.Objects.Add(Obj("full", "full", "no", null, null));

            // The world clock. Two keening and answered: a lamp lights. A lone keener tires and falls quiet.
            room.Tick = st =>
            {
                bool a = st["auset"] == "keening", b = st["nebethet"] == "keening";
                if (a && b)
                {
                    st["breath_a"] = "0"; st["breath_b"] = "0";
                    int n = int.Parse(st["lamps"]);
                    if (st["lament"] == "answered" && n < 4) st["lamps"] = (n + 1).ToString();
                    return;
                }
                foreach (var (who, breath) in new[] { ("auset", "breath_a"), ("nebethet", "breath_b") })
                {
                    if (st[who] != "keening") { st[breath] = "0"; continue; }
                    int br = int.Parse(st[breath]) + 1;
                    if (br >= 3) { st[who] = "quiet"; st[breath] = "0"; st["lament"] = "unanswered"; }
                    else st[breath] = br.ToString();
                }
            };

            foreach (string who in new[] { "auset", "nebethet" })
            {
                room.Rules.Add(new SpellRule { Verb = "release", Target = who, From = new[] { "bound" }, To = "keening", Event = "released",
                    Success = "Weha. The knot gives. She draws a long breath and keens." });
                room.Rules.Add(new SpellRule { Verb = "release", Target = who, From = new[] { "quiet" }, To = "keening", Event = "rekeen",
                    Success = "She lifts her head and keens again." });
                room.Rules.Add(new SpellRule { Verb = "still", Target = who, From = new[] { "never" }, To = "never",
                    Refusal = "She is not loud for you. She is loud for him.", RefusalEvent = "hush_refused" });
                room.Rules.Add(new SpellRule { Verb = "wake", Target = who, From = new[] { "quiet" }, To = "keening", Event = "rekeen",
                    Success = "Res. She lifts her head and keens again.", Refusal = "She is not asleep. She is grieving." });
            }
            room.Rules.Add(new SpellRule { Verb = "release", Target = "band", From = new[] { "never" }, To = "never",
                Refusal = "Two bands, two women. Stand by the one you mean and name her." });

            room.Rules.Add(new SpellRule { Verb = "echo", Target = "lament", From = new[] { "unanswered" }, To = "answered",
                Requires = new[] { C("auset", "keening"), C("nebethet", "keening") }, Event = "lament_answered",
                Success = "Wesheb. You answer them, and the two voices answer each other over the bier.",
                Refusal = "One voice alone. Her sister has not answered her yet." });

            room.Rules.Add(new SpellRule { Verb = "auto", Target = "full", To = "yes", Requires = new[] { C("lamps", "4") }, Event = "lamps_full" });
            room.Rules.Add(new SpellRule { Verb = "open", Target = "door", From = new[] { "sealed" }, To = "open", Requires = new[] { C("lamps", "4") },
                Event = "door_open", Success = "Wen. The seal on the door breaks clean.",
                Refusal = "The door is sealed, and the room is dark. Not yet." });

            room.Hints.Add(new Hint { When = new[] { C("auset", "bound"), C("nebethet", "bound") }, Text = "Their mouths are bound with linen. You tied knots like that every day." });
            room.Hints.Add(new Hint { When = new[] { C("lament", "unanswered"), C("lamps", "0", "1", "2", "3") }, Text = "Both must keen at once, and someone must answer them." });
            room.Hints.Add(new Hint { When = new[] { C("lament", "answered"), C("lamps", "0", "1", "2", "3") }, Text = "Let them keen. The lamps are catching." });
            room.Hints.Add(new Hint { When = new[] { C("lamps", "4") }, Text = "The door at the far end." });

            var s = new SceneScript { SceneName = MournersB, Realm = "Rosetau", Title = "The Room of the Mourners", Room = room,
                CodexOnEntry = new[] { "word.release", "word.echo", "word.still" } };
            s.Usables.Add(new Usable { Id = "read_wall", Prompt = "Read the wall", Zone = "wall" });

            s.Beats.Add(new Beat("arrive", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Title("THE ROOM OF THE MOURNERS", 4f),
                Step.Caption("A wrapped body lies on a lion-headed bier. A woman kneels at its head and another at its feet. Linen bands are tied across their mouths."),
                L.S(Speaker.Ankhu, "That's my knot. Double turn, flat tuck, so it won't show through the outer layer. I've tied it ten thousand times."),
                L.S(Speaker.Ankhu, "That's Ausar on the bier. And those are his sisters, and somebody's wrapped their mouths shut like the dead."),
                Step.Free()));

            // The refrain of the Lamentations of Isis and Nephthys, Papyrus Bremner-Rhind (British Museum EA 10188), col. 1.
            s.Beats.Add(new Beat("read_wall", TriggerKind.Use, "read_wall",
                Step.Caption("As it is written: \"Come to your house, come to your house.\""),
                L.S(Speaker.Ankhu, "The two kites. Priestesses sang this over the dead. Two of them, one at each end.")) { Once = false });

            s.Beats.Add(new Beat("hush", TriggerKind.Event, "hush_refused",
                Step.Freeze(),
                Step.Caption("The word dies in the air. Ankhu's hand is still raised."),
                Step.Cue("flashback:ros_hush"),
                L.S(Speaker.Ankhu, "Every night for a year. I thought I was helping her."),
                Step.Write("memory.hush"),
                Step.Free()));

            s.Beats.Add(new Beat("released", TriggerKind.Event, "released",
                Step.Caption("The band falls into her lap. She keens, high and long, and the sound fills the room.")));
            s.Beats.Add(new Beat("quiet", TriggerKind.Event, "rekeen",
                L.S(Speaker.Ankhu, "She can't keep it up alone. Nobody can.")));
            s.Beats.Add(new Beat("answered", TriggerKind.Event, "lament_answered",
                Step.Caption("The two voices meet over the bier, one high, one low. A lamp at the corner of the bier catches."),
                L.S(Speaker.Ankhu, "One answers the other, and I answer both. Like the two priestesses at a burial.")));
            s.Beats.Add(new Beat("full", TriggerKind.Event, "lamps_full",
                Step.Caption("All four lamps burn. The linen over the dead man's chest rises once, as if with breath."),
                L.S(Speaker.Ankhu, "I wrapped men for their families for twenty years. I never stayed for the singing.")));

            s.Beats.Add(new Beat("murmur", TriggerKind.Idle, "", L.S(Speaker.Ankhu, "Two women, two bands. And nobody answering them."))
                { Value = 45f });

            s.Beats.Add(new Beat("solved", TriggerKind.Solved, "",
                Step.Freeze(),
                Step.Caption("The far door opens. The two women keep singing behind him. Ankhu picks the two linen bands up off the floor and folds them into his belt."),
                Step.Cue("flashback:ros_leaving"),
                L.S(Speaker.Ankhu, "The temple at Abdju took boys who could copy signs. There was bread in it. I sent her half of every ration."),
                L.S(Speaker.Ankhu, "I told myself it was for her. It was also somewhere quiet."),
                Step.Write("memory.leaving"),
                Step.Wait(1.5f),
                Step.Next()));
            return s;
        }

        // ---------------------------------------------------------------- Room C: The Mother's Room

        /// <summary>
        /// A one-room house. The mother sits at her loom in shadow with her face in her hands, weeping. Still is
        /// refused. Her loom, her quern and her lamp will not move while she weeps alone. Echo (wesheb) answers her,
        /// and she lifts her head. Then wake (res) the loom and the quern, and once both are working the lamp catches
        /// by itself. Remember (sekha) her with the lamp lit. Goal: mother remembered.
        /// </summary>
        private static SceneScript BuildMotherC()
        {
            var L = new Lines("rosetau_c");
            var room = new RoomDef
            {
                Id = "rosetau_c",
                Title = "The Mother's Room",
                Verbs = new HashSet<string> { "open", "wake", "still", "release", "echo", "give", "remember" },
                Goal = new[] { C("mother", "remembered") },
            };
            var mother = Obj("mother", "mother", "weeping", MotherG, null,
                "mother", "mum", "mama", "mut", "woman", "her", "my mother", "weeping woman", "weaver");
            room.Objects.Add(mother);
            room.Objects.Add(Obj("weeping", "weeping", "unanswered", KeenG, null, "weeping", "crying", "grief", "sobbing", "tears", "lament", "voice"));
            room.Objects.Add(Obj("loom", "loom", "still", LoomG, new[] { "loom" }, "loom", "weaving", "warp", "shuttle", "cloth", "thread"));
            room.Objects.Add(Obj("quern", "quern", "still", QuernG, new[] { "quern" }, "quern", "grindstone", "millstone", "grain", "stone"));
            room.Objects.Add(Obj("lamp", "lamp", "dark", LampG, null, "lamp", "light", "oil lamp", "wick"));

            room.Rules.Add(new SpellRule { Verb = "still", Target = "mother", From = new[] { "never" }, To = "never",
                Refusal = "No. Not again.", RefusalEvent = "hush_again" });
            room.Rules.Add(new SpellRule { Verb = "still", Target = "weeping", From = new[] { "never" }, To = "never",
                Refusal = "No. Not again.", RefusalEvent = "hush_again" });

            room.Rules.Add(new SpellRule { Verb = "echo", Target = "weeping", From = new[] { "unanswered" }, To = "answered",
                Also = new[] { ("mother", "listening") }, Event = "answered",
                Success = "Wesheb. You weep with her, out loud. She lifts her head." });
            room.Rules.Add(new SpellRule { Verb = "echo", Target = "mother", From = new[] { "weeping" }, To = "listening",
                Also = new[] { ("weeping", "answered") }, Event = "answered",
                Success = "Wesheb. You weep with her, out loud. She lifts her head." });

            room.Rules.Add(new SpellRule { Verb = "wake", Target = "loom", From = new[] { "still" }, To = "working", Requires = new[] { C("weeping", "answered") },
                Event = "loom_on", Success = "Res. The heddle lifts. The shuttle runs across and back.",
                Refusal = "Her hands are over her face. The loom does not move while she weeps alone.", RefusalEvent = "not_yet" });
            room.Rules.Add(new SpellRule { Verb = "wake", Target = "quern", From = new[] { "still" }, To = "grinding", Requires = new[] { C("weeping", "answered") },
                Event = "quern_on", Success = "Res. The rubbing stone rocks on the saddle. Flour gathers at the lip.",
                Refusal = "The grain lies where she left it. Nothing in this room moves while she weeps alone.", RefusalEvent = "not_yet" });
            room.Rules.Add(new SpellRule { Verb = "wake", Target = "lamp", From = new[] { "never" }, To = "never",
                Refusal = "It is her lamp. She lights it when the work is going." });

            room.Rules.Add(new SpellRule { Verb = "auto", Target = "lamp", To = "lit", Requires = new[] { C("loom", "working"), C("quern", "grinding") }, Event = "lamp_lit" });

            room.Rules.Add(new SpellRule { Verb = "remember", Target = "mother", From = new[] { "listening" }, To = "remembered", Requires = new[] { C("lamp", "lit") },
                Event = "remembered", Success = "Sekha. You remember her.",
                Refusal = "Not like this. Not in the dark with her face in her hands." });

            room.Hints.Add(new Hint { When = new[] { C("weeping", "unanswered") }, Text = "In the last room, the lamps lit for a lament that was answered." });
            room.Hints.Add(new Hint { When = new[] { C("weeping", "answered"), C("lamp", "dark") }, Text = "Her loom and her quern. She was never still in her life." });
            room.Hints.Add(new Hint { When = new[] { C("lamp", "lit") }, Text = "Remember her, the way she was." });

            var s = new SceneScript { SceneName = MotherC, Realm = "Rosetau", Title = "The Mother's Room", Room = room,
                CodexOnEntry = new[] { "word.wake", "word.echo", "lore.mothers_house" } };
            s.Usables.Add(new Usable { Id = "look_bread", Prompt = "Look at the shelf", Zone = "shelf" });

            s.Beats.Add(new Beat("arrive", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Title("THE MOTHER'S ROOM", 4f),
                Step.Caption("A one-room house of mud brick. A loom, a quern, an unlit lamp. A woman sits at the loom with her face in her hands."),
                L.S(Speaker.Ankhu, "This is our house in Abdju. The year after Father."),
                L.S(Speaker.Ankhu, "She sat like that at the loom every evening. I used to go out so I wouldn't have to hear it."),
                Step.Free()));

            s.Beats.Add(new Beat("shelf", TriggerKind.Use, "look_bread",
                Step.Caption("On the shelf, a faience figure of a child with a finger at his lips, and a clay falcon beside it."),
                L.S(Speaker.Ankhu, "He brought it back.")) { Once = false });

            s.Beats.Add(new Beat("hush_again", TriggerKind.Event, "hush_again",
                L.S(Speaker.Ankhu, "I did that for a year. I'm not doing it again.")) { Once = false });
            s.Beats.Add(new Beat("not_yet", TriggerKind.Event, "not_yet",
                L.S(Speaker.Ankhu, "Nothing moves while she's like this. It didn't then either.")));

            s.Beats.Add(new Beat("answered", TriggerKind.Event, "answered",
                Step.Freeze(),
                Step.Caption("Ankhu weeps aloud. The woman lifts her head and looks at him."),
                L.S(Speaker.Ankhu, "It was the same grief. That's why I couldn't stand in a room with it. Hers was mine with the door open."),
                L.S(Speaker.Mother, "There you are."),
                Step.Free()));
            s.Beats.Add(new Beat("loom", TriggerKind.Event, "loom_on",
                L.S(Speaker.Mother, "Hand me the thread, then, if you're staying.")));
            s.Beats.Add(new Beat("quern", TriggerKind.Event, "quern_on",
                L.S(Speaker.Ankhu, "She ground every grain I sent her. She wouldn't let a boy do it.")));
            // Room guides 2026-11-26: remember is the winning word here and no earlier room teaches it, so the
            // lamp catching writes it on the roll (its glyphs are in the lexicon as sekha).
            s.Beats.Add(new Beat("lamp", TriggerKind.Event, "lamp_lit",
                Step.Caption("The lamp catches by itself. The room is warm and loud with work."),
                Step.Learn("remember")));

            s.Beats.Add(new Beat("murmur", TriggerKind.Idle, "", L.S(Speaker.Ankhu, "I used to walk out of this room. That's all I ever did with it."))
                { Value = 45f });

            s.Beats.Add(new Beat("solved", TriggerKind.Solved, "",
                Step.Freeze(),
                Step.Cue("flashback:ros_old_mother"),
                L.S(Speaker.Ankhu, "She lived with us for twenty years after Hori was born. Neferet let her run the house. She was still the loudest woman in the street. Hori thought she was the funniest person alive."),
                L.S(Speaker.Ankhu, "She died old, in her sleep, with thread in her lap. I washed her and wrapped her myself, every layer. I took my time."),
                Step.Write("memory.old_mother"),
                Step.Caption("The house is quiet. A red light shows under the back door, and the air smells of burning."),
                Step.Wait(1.5f),
                Step.Next()));
            return s;
        }

        // ---------------------------------------------------------------- Room D: The Two Ways

        /// <summary>
        /// The Book of Two Ways: a black water road and a blue land road, and between them a channel of fire. On the
        /// far bank three faceless soldier shades keep a watch nobody relieved. A garrison roster by the stair lists
        /// three men: Nakht of Abdju, shield-bearer. Paser of Iunu, shield-bearer. Huy of Abdju, archer. One shade
        /// carries a shield, one a bow, and one has an empty shield strap, because his shield was carried home.
        /// The fire roars and drowns every voice. Still (hetep) quiets it for a few ticks, the one place in Rosetau
        /// still is welcome. While it is low, echo (wesheb) a shade and he calls back his town, and the father
        /// whistles the tune of the reed whistle Ankhu lost. Then speak his name (ren) standing at his post. A wrong
        /// name turns no one. Goal: all three named, so the watch is relieved.
        /// </summary>
        private static SceneScript BuildTwoWaysD()
        {
            var L = new Lines("rosetau_d");
            var room = new RoomDef
            {
                Id = "rosetau_d",
                Title = "The Two Ways",
                Verbs = new HashSet<string> { "open", "wake", "still", "release", "echo", "give", "remember", "name" },
                Goal = new[] { C("watch", "relieved") },
            };
            room.Objects.Add(Obj("fire", "fire", "roaring", FireG, null,
                "fire", "flame", "flames", "channel", "channel of fire", "lake of fire", "red channel", "blaze", "burning"));
            room.Objects.Add(Obj("fclock", "fclock", "0", null, null));
            room.Objects.Add(Obj("watch", "watch", "kept", null, null));

            // (id, post zone, what he carries, his town, the alias words for what he carries)
            var men = new[]
            {
                ("shade_paser", "post1", "Paser", "paser", new[] { "left", "shield", "man with the shield", "soldier with the shield", "shield bearer", "shield-bearer" }),
                ("shade_nakht", "post2", "Nakht", "nakht", new[] { "middle", "strap", "empty", "man with the empty strap", "empty-handed", "empty handed", "soldier with no shield", "man with no shield" }),
                ("shade_huy", "post3", "Huy", "huy", new[] { "right", "bow", "archer", "bowman", "man with the bow", "soldier with the bow" }),
            };
            foreach (var (id, post, _, _, words) in men)
            {
                var aliases = new List<string> { "shade", "soldier", "man", "guard", "watchman", "shadow", "ghost", "faceless man" };
                aliases.Add(words[0] + " shade"); aliases.Add(words[0] + " soldier"); aliases.Add(words[0] + " man");
                for (int k = 1; k < words.Length; k++) aliases.Add(words[k]);
                room.Objects.Add(Obj(id, "soldier", "faceless", SoldierG, new[] { post }, aliases.ToArray()));
            }

            // The fire comes back up three ticks after it is stilled.
            room.Tick = st =>
            {
                if (st["fire"] != "low") { st["fclock"] = "0"; return; }
                int c = int.Parse(st["fclock"]) + 1;
                if (c >= 3) { st["fire"] = "roaring"; st["fclock"] = "0"; }
                else st["fclock"] = c.ToString();
            };

            room.Rules.Add(new SpellRule { Verb = "still", Target = "fire", From = new[] { "roaring" }, To = "low", Also = new[] { ("fclock", "0") },
                Event = "fire_low", Success = "Hetep. The fire sinks to a red crawl along the channel.",
                Refusal = "It is already low. Be quick." });

            string[] town = { "iunu", "abdju", "abdju2" };
            for (int k = 0; k < men.Length; k++)
            {
                string id = men[k].Item1;
                room.Rules.Add(new SpellRule { Verb = "echo", Target = id, From = new[] { "faceless" }, To = "heard", Requires = new[] { C("fire", "low") },
                    Event = "heard_" + town[k], Success = "Wesheb. You call across the channel, and he calls back.",
                    Refusal = "The fire roars between you. Your voice does not carry." });
                room.Rules.Add(new SpellRule { Verb = "echo", Target = id, From = new[] { "heard" }, To = "heard", Requires = new[] { C("fire", "low") },
                    Event = "heard_" + town[k], Success = "Wesheb. He calls back again.",
                    Refusal = "The fire roars between you. Your voice does not carry." });
                room.Rules.Add(new SpellRule { Verb = "still", Target = id, From = new[] { "never" }, To = "never",
                    Refusal = "He has been still long enough." });
            }

            // Names. For each name, the wrong posts come first, so a name spoken from nowhere in particular turns no
            // one, then the right post. Standing at a post decides which rule hears it.
            WordGloss[] nameG = { PaserG, NakhtG, HuyG };
            for (int n = 0; n < men.Length; n++)
            {
                string value = men[n].Item4;
                for (int k = 0; k < men.Length; k++)
                {
                    if (k == n) continue;
                    room.Rules.Add(new SpellRule { Verb = "name", Target = men[k].Item1, NameValue = value, NameGloss = nameG[n],
                        From = new[] { "never" }, To = "never",
                        Refusal = "Nobody turns to that name.", RefusalEvent = "wrong_name" });
                }
                room.Rules.Add(new SpellRule { Verb = "name", Target = men[n].Item1, NameValue = value, NameGloss = nameG[n],
                    From = new[] { "heard" }, To = "named", Requires = new[] { C("fire", "low") },
                    Event = value + "_named", Success = "Ren. " + men[n].Item3 + ".",
                    Refusal = "The fire takes the name. Quiet it, and let him answer you first." });
            }

            room.Rules.Add(new SpellRule { Verb = "auto", Target = "watch", To = "relieved",
                Requires = new[] { C("shade_paser", "named"), C("shade_nakht", "named"), C("shade_huy", "named") }, Event = "relieved" });

            room.Hints.Add(new Hint { When = new[] { C("fire", "roaring"), C("shade_paser", "faceless"), C("shade_nakht", "faceless"), C("shade_huy", "faceless") },
                Text = "Nobody can hear anything over that fire. You've quieted worse." });
            room.Hints.Add(new Hint { When = new[] { C("fire", "low"), C("shade_paser", "faceless", "heard"), C("shade_nakht", "faceless", "heard") },
                Text = "The fire is low. Call to them while it lasts." });
            room.Hints.Add(new Hint { When = new[] { C("watch", "kept") },
                Text = "The roster by the stair has three names, their towns and what they carried. Match each man to his line, stand at his post and say it." });

            var s = new SceneScript { SceneName = TwoWaysD, Realm = "Rosetau", Title = "The Two Ways", Room = room,
                CodexOnEntry = new[] { "word.still", "word.echo", "word.name", "lore.two_ways" } };
            s.Usables.Add(new Usable { Id = "read_roster", Prompt = "Read the roster", Zone = "roster" });
            s.Usables.Add(new Usable { Id = "read_floor", Prompt = "Read the floor", Zone = "floor" });

            s.Beats.Add(new Beat("arrive", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Title("THE TWO WAYS", 4f),
                Step.Caption("Two roads run away into the dark, one black as water, one blue. Between them a channel of fire. On the far bank, three men in soldiers' kilts stand at their posts. They have no faces."),
                L.S(Speaker.Ankhu, "The old coffins from the Hare nome had this painted on the floor boards. Water road, land road, and fire between. I copied one once for a master who wanted the pattern."),
                L.S(Speaker.Ankhu, "I never thought anybody stood on it."),
                Step.Free()));

            // The garrison roster. The names are common New Kingdom names (Ranke, Die altagyptischen Personennamen).
            s.Beats.Add(new Beat("read_roster", TriggerKind.Use, "read_roster",
                Step.Caption("A wooden board on a peg, written in black ink: \"Nakht of Abdju, shield-bearer. Paser of Iunu, shield-bearer. Huy of Abdju, archer.\""),
                L.S(Speaker.Ankhu, "Nakht of Abdju. That's him. That's Father."),
                L.S(Speaker.Ankhu, "They read these lists out at the temple gate when the boats came back. She took me every time.")) { Once = false });

            // Coffin Texts spell 1130, from the Book of Two Ways (Lichtheim, Ancient Egyptian Literature I, 1973, p. 131).
            s.Beats.Add(new Beat("read_floor", TriggerKind.Use, "read_floor",
                Step.Caption("As it is written: \"I made the four winds, that every man might breathe in his time.\""),
                L.S(Speaker.Ankhu, "Every man. It says every man, not every man with a tomb.")) { Once = false });

            s.Beats.Add(new Beat("fire_low", TriggerKind.Event, "fire_low",
                Step.Caption("The fire sinks. The three faceless men lift their heads."),
                L.S(Speaker.Ankhu, "That's the first thing in Rosetau I've been glad to quiet."),
                Step.Flag("fire_low_seen")));
            // Room guides 2026-11-26: only from the second still on, so the first still does not caption twice.
            s.Beats.Add(new Beat("fire_low_again", TriggerKind.Event, "fire_low",
                Step.Caption("The fire sinks again.")) { Once = false, RequiresFlags = new[] { "fire_low_seen" } });

            s.Beats.Add(new Beat("heard_paser", TriggerKind.Event, "heard_iunu",
                Step.Caption("The shade with the shield raises it. A young man's voice calls back across the channel."),
                L.S(Speaker.Other, "Iunu! Iunu!")) { Once = false });
            s.Beats.Add(new Beat("heard_nakht", TriggerKind.Event, "heard_abdju",
                Step.Caption("The shade in the middle has a shield strap across his chest and nothing on it. He calls back, then whistles four notes, falling."),
                L.S(Speaker.Father, "Abdju!"),
                L.S(Speaker.Ankhu, "That's the tune. That's my whistle's tune. He made it up.")) { Once = false });
            s.Beats.Add(new Beat("heard_huy", TriggerKind.Event, "heard_abdju2",
                Step.Caption("The shade with the bow lifts it over his head. An older man's voice calls back."),
                L.S(Speaker.Other, "Abdju! Abdju!")) { Once = false });

            s.Beats.Add(new Beat("wrong", TriggerKind.Event, "wrong_name",
                L.S(Speaker.Ankhu, "Not him. Look at what he's carrying.")) { Once = false });

            s.Beats.Add(new Beat("paser", TriggerKind.Event, "paser_named",
                Step.Caption("The shade with the shield has a face now, young and tired. He lowers the shield and walks across the fire, and it does not touch him."),
                L.S(Speaker.Ankhu, "Go home, Paser.")));
            s.Beats.Add(new Beat("huy", TriggerKind.Event, "huy_named",
                Step.Caption("The archer has a face now, lined and grey at the temples. He unstrings his bow, nods once and crosses the fire."),
                L.S(Speaker.Ankhu, "Somebody in Abdju waited for you too.")));
            s.Beats.Add(new Beat("nakht", TriggerKind.Event, "nakht_named",
                Step.Freeze(),
                Step.Caption("The middle shade has a face now. A broad sunburnt face with a broken nose. He looks at Ankhu for a long time."),
                L.S(Speaker.Father, "Ankhu? You've gone grey."),
                L.S(Speaker.Ankhu, "I'm older than you now, Father. By a long way."),
                L.S(Speaker.Father, "Paser and Huy were with me at the end. I'm not crossing while they're still stood here."),
                Step.Free()));

            s.Beats.Add(new Beat("murmur", TriggerKind.Idle, "", L.S(Speaker.Ankhu, "I was six. I don't even know which one is his face."))
                { Value = 45f });

            s.Beats.Add(new Beat("solved", TriggerKind.Solved, "",
                Step.Freeze(),
                Step.Caption("Nakht crosses the fire last. He stops in front of Ankhu. He is a head shorter than his son."),
                Step.Cue("flashback:ros_father"),
                L.S(Speaker.Ankhu, "I blew it every night. Then I dropped it in the canal, and the shield came home the same month. I thought for years that was why."),
                L.S(Speaker.Father, "They never brought me back, did they."),
                L.S(Speaker.Ankhu, "Only the shield. She kept it by the door."),
                L.S(Speaker.Father, "Then bring me home on that."),
                Step.Write("memory.father"),
                Step.Wait(1.5f),
                Step.Next()));
            return s;
        }

        // ---------------------------------------------------------------- Room E: The Shield Brought Home

        /// <summary>
        /// The last step of Rosetau is the doorway of the house at Abdju, the day the shield came home. The shield
        /// leans on the doorpost. The mother, young, keens in the street. Nakht's shade waits by the door with nothing
        /// to go into. Bind (tjes) the father is refused: there is no body. Bind the shield with the two linen bands
        /// Ankhu carried out of the Room of the Mourners, and the shade goes into it. Anpu steps off the stair for the
        /// first time in Rosetau and holds the wrapped form upright, as in the Opening of the Mouth vignettes. Echo
        /// (wesheb) answers the mother, the thing the boy could not do. Then open (wen) the mouth, which needs the
        /// form held up and the lament answered. Give bread once the mouth is open. Goal: bread given.
        /// </summary>
        private static SceneScript BuildFinaleE()
        {
            var L = new Lines("rosetau_e");
            var room = new RoomDef
            {
                Id = "rosetau_e",
                Title = "The Shield Brought Home",
                Verbs = new HashSet<string> { "open", "wake", "still", "bind", "release", "echo", "give", "remember", "name" },
                Goal = new[] { C("bread", "given") },
            };
            room.Objects.Add(Obj("shield", "shield", "leaning", ShieldG, new[] { "door" },
                "shield", "cowhide shield", "father's shield", "his shield"));
            room.Objects.Add(Obj("bands", "linen bands", "carried", LinenG, null, "band", "bands", "linen", "linen bands", "wrapping", "bandages"));
            room.Objects.Add(Obj("father", "father", "waiting", FatherG, new[] { "door" },
                "father", "dad", "nakht", "shade", "soldier", "my father", "him"));
            room.Objects.Add(Obj("holder", "holder", "stair", null, null));
            room.Objects.Add(Obj("mother", "mother", "keening", MotherG, new[] { "street" },
                "mother", "mum", "mama", "mut", "woman", "widow", "my mother", "her", "wailing woman"));
            room.Objects.Add(Obj("lament", "lament", "unanswered", KeenG, new[] { "street" },
                "lament", "keen", "keening", "wail", "wailing", "scream", "screaming", "grief", "weeping", "crying"));
            room.Objects.Add(Obj("mouth", "mouth", "closed", MouthG, new[] { "door" }, "mouth", "his mouth", "father's mouth", "lips"));
            room.Objects.Add(Obj("bread", "bread", "carried", BreadG, null, "bread", "loaf", "offering", "bread offering"));

            room.Rules.Add(new SpellRule { Verb = "bind", Target = "father", From = new[] { "never" }, To = "never",
                Refusal = "There is nothing of him to wrap. They never brought him home.", RefusalEvent = "no_body" });
            room.Rules.Add(new SpellRule { Verb = "bind", Target = "shield", From = new[] { "leaning" }, To = "wrapped", Requires = new[] { C("bands", "carried") },
                Also = new[] { ("bands", "used"), ("father", "housed") }, Event = "shield_wrapped",
                Success = "Tjes. Double turn, flat tuck. The two bands go round the shield, and round, and it stands up by itself." });
            room.Rules.Add(new SpellRule { Verb = "bind", Target = "bands", From = new[] { "carried" }, To = "used", Requires = new[] { C("shield", "leaning") },
                Also = new[] { ("shield", "wrapped"), ("father", "housed") }, Event = "shield_wrapped",
                Success = "Tjes. Double turn, flat tuck. The two bands go round the shield, and round, and it stands up by itself.",
                Refusal = "Bind them round what?" });

            room.Rules.Add(new SpellRule { Verb = "auto", Target = "holder", To = "holding", Requires = new[] { C("father", "housed") }, Event = "anpu_holds" });

            room.Rules.Add(new SpellRule { Verb = "still", Target = "mother", From = new[] { "never" }, To = "never",
                Refusal = "No. Not her. Not ever again.", RefusalEvent = "hush_last" });
            room.Rules.Add(new SpellRule { Verb = "still", Target = "lament", From = new[] { "never" }, To = "never",
                Refusal = "No. Not her. Not ever again.", RefusalEvent = "hush_last" });
            room.Rules.Add(new SpellRule { Verb = "echo", Target = "mother", From = new[] { "keening" }, To = "answered",
                Also = new[] { ("lament", "answered") }, Event = "lament_answered",
                Success = "Wesheb. You keen with her, in the street, out loud." });
            room.Rules.Add(new SpellRule { Verb = "echo", Target = "lament", From = new[] { "unanswered" }, To = "answered",
                Also = new[] { ("mother", "answered") }, Event = "lament_answered",
                Success = "Wesheb. You keen with her, in the street, out loud." });

            room.Rules.Add(new SpellRule { Verb = "open", Target = "mouth", From = new[] { "closed" }, To = "open",
                Requires = new[] { C("holder", "holding"), C("lament", "answered") }, Also = new[] { ("father", "opened") },
                Event = "mouth_open", Success = "Wen. You touch the mouth of the wrapped form, and the mouth of the man.",
                Refusal = "Not yet. In every Opening you ever served, someone held the dead man up and someone mourned him.", RefusalEvent = "not_ready" });

            room.Rules.Add(new SpellRule { Verb = "give", Target = "bread", From = new[] { "carried" }, To = "given", Requires = new[] { C("mouth", "open") },
                Event = "bread_given", Success = "Rdi. He takes the loaf.",
                Refusal = "He can't eat. His mouth was never opened.", RefusalEvent = "cant_eat" });
            room.Rules.Add(new SpellRule { Verb = "give", Target = "father", From = new[] { "opened" }, To = "opened", Requires = new[] { C("bread", "carried") },
                Also = new[] { ("bread", "given") }, Event = "bread_given", Success = "Rdi. He takes the loaf.",
                Refusal = "He can't eat. His mouth was never opened.", RefusalEvent = "cant_eat" });

            room.Hints.Add(new Hint { When = new[] { C("shield", "leaning") }, Text = "No body ever came home. Only the shield. And you still have the bands from the mourners' room." });
            room.Hints.Add(new Hint { When = new[] { C("lament", "unanswered") }, Text = "She's screaming in the street, the way she did. You held your ears that day." });
            room.Hints.Add(new Hint { When = new[] { C("holder", "holding"), C("lament", "answered"), C("mouth", "closed") }, Text = "He's held up and he's mourned. You know the rite. Open his mouth." });
            room.Hints.Add(new Hint { When = new[] { C("mouth", "open") }, Text = "Read the stela by the door. You know what the dead want." });

            var s = new SceneScript { SceneName = FinaleE, Realm = "Rosetau", Title = "The Shield Brought Home", Room = room,
                CodexOnEntry = new[] { "word.bind", "word.open", "lore.opening_of_the_mouth", "lore.abdju_stela" } };
            s.Usables.Add(new Usable { Id = "read_stela", Prompt = "Read the stela", Zone = "door" });

            s.Beats.Add(new Beat("arrive", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Title("THE SHIELD BROUGHT HOME", 4f),
                Step.Caption("The stair of Rosetau ends at a door in a mud-brick street. A cowhide shield leans on the doorpost. A young woman sits in the dust and wails. Anpu stands on the last step."),
                L.S(Speaker.Ankhu, "This is the day. This is the exact day."),
                L.S(Speaker.Anpu, "You came down by water and by land. This is where the roads come out."),
                L.S(Speaker.Ankhu, "Where's the boy? I was right there, by the door, holding my ears."),
                L.S(Speaker.Anpu, "You are standing where he stood."),
                Step.Free()));

            // The offering formula, the commonest text on Egyptian stelae (Gardiner, Egyptian Grammar, 3rd ed. 1957, p. 170).
            s.Beats.Add(new Beat("read_stela", TriggerKind.Use, "read_stela",
                Step.Caption("A small limestone stela by the door, its face smoothed and never cut. Ankhu reads the words that should be on it: \"An offering which the king gives, of bread and beer, oxen and fowl.\" (the offering formula)"),
                L.S(Speaker.Ankhu, "Abdju's full of stones like this, for people who died somewhere else. We couldn't pay a cutter. It stood there blank my whole life.")) { Once = false });

            s.Beats.Add(new Beat("no_body", TriggerKind.Event, "no_body",
                L.S(Speaker.Father, "They sent the shield. It's the only bit of me that came back."),
                L.S(Speaker.Ankhu, "I've wrapped men they found in the river. I've wrapped what was left after a wall came down. I can wrap a shield.")));

            s.Beats.Add(new Beat("wrapped", TriggerKind.Event, "shield_wrapped",
                Step.Freeze(),
                Step.Caption("The shield stands upright in the linen, the height of a man. The shade steps into it and is gone."),
                L.S(Speaker.Ankhu, "The same bands. The same knot. This time it's to keep someone in, not to keep someone quiet."),
                Step.Free()));

            // The Opening of the Mouth vignette: Anubis holds the mummy upright before the tomb while the women mourn
            // (Papyrus of Hunefer, British Museum EA 9901/5).
            s.Beats.Add(new Beat("anpu", TriggerKind.Event, "anpu_holds",
                Step.Freeze(),
                Step.Caption("Anpu comes down off the last step. He walks to the door and takes the wrapped form in both arms, and holds it upright, facing the street."),
                Step.Move("Anpu", "mark_hold"),
                L.S(Speaker.Ankhu, "You came off the step."),
                L.S(Speaker.Anpu, "In Hunefer's book I hold him up while the women mourn. Somebody has to stand behind him. I do it for all of them."),
                Step.Free()));

            s.Beats.Add(new Beat("hush_last", TriggerKind.Event, "hush_last",
                L.S(Speaker.Ankhu, "I put my hands over my ears that day, and then over her mouth for a year. I'm not doing either.")) { Once = false });

            s.Beats.Add(new Beat("answered", TriggerKind.Event, "lament_answered",
                Step.Freeze(),
                Step.Caption("Ankhu sits down in the dust beside the young woman and keens with her. The neighbours' doors stay shut. She looks at him."),
                L.S(Speaker.Ankhu, "Let them hear. Let the whole street hear it."),
                L.S(Speaker.Mother, "Who taught you to wail like that? You sound like a goat."),
                L.S(Speaker.Ankhu, "You did."),
                Step.Free()));

            s.Beats.Add(new Beat("not_ready", TriggerKind.Event, "not_ready",
                L.S(Speaker.Ankhu, "There's an order to it. I've done it a thousand times for strangers.")) { Once = false });
            s.Beats.Add(new Beat("cant_eat", TriggerKind.Event, "cant_eat",
                L.S(Speaker.Ankhu, "All those years she left bread on the step, and he couldn't eat a crumb of it.")) { Once = false });

            s.Beats.Add(new Beat("opened", TriggerKind.Event, "mouth_open",
                Step.Freeze(),
                Step.Caption("The linen over the face of the wrapped form moves, as if with breath. Anpu holds it steady."),
                L.S(Speaker.Father, "Is there any bread? I've been on watch since the flood."),
                L.S(Speaker.Mother, "Nakht?"),
                Step.Free()));

            s.Beats.Add(new Beat("murmur", TriggerKind.Idle, "", L.S(Speaker.Ankhu, "The bands, the god, the mourning, the mouth. I know this rite better than my own name."))
                { Value = 45f });

            s.Beats.Add(new Beat("solved", TriggerKind.Solved, "",
                Step.Freeze(),
                Step.Caption("The young woman gets up out of the dust. Nakht takes her hand. They go in through the door together, and she is laughing at something he says."),
                Step.Caption("The stela by the door is cut now. A man and a woman sit at an offering table. Above them, two names."),
                Step.Cue("flashback:ros_whistle"),
                L.S(Speaker.Ankhu, "She learned his tune. She played it to Hori, badly, every night he'd let her. I never asked her where she'd learned it."),
                Step.Write("memory.whistle"),
                L.S(Speaker.Anpu, "You have been a guest in her house now."),
                L.S(Speaker.Ankhu, "I have, haven't I. Took me long enough."),
                Step.Title("END OF ROSETAU", 4f),
                Step.Wait(1f),
                Step.Next()));
            return s;
        }
    }
}
