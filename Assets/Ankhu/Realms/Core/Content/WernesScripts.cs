using System.Collections.Generic;

namespace Ankhu.Realms.Core.Content
{
    /// <summary>
    /// The playable opening after the walk, following the full-game script v1
    /// (/mnt/project-files/design/story/ankhu_full_game_script_v1.md, sections 3.0, 3.1 and 6.2):
    ///   Realm_01_Arit1        The First Gate (Spell 144): learn and speak the three names. Anpu gives the codex.
    ///   Realm_02_Wernes_Field The Field That Waits: open, wake, still. Memory A, Hori on his shoulders.
    ///   Realm_03_Wernes_House The House With One Door: seal is unlearned until Memory B, the father's door.
    ///   Realm_04_Wernes_Row   Shabti Row: all four verbs, focus by standing near, and still spoken to his own name. Memory C.
    /// Line text is ours unless marked Allen. Voice keys are dialogue.&lt;speaker&gt;.&lt;scene&gt;.&lt;n&gt;.
    /// </summary>
    public static class WernesScripts
    {
        public const string Gate = "Realm_01_Arit1";
        public const string Field = "Realm_02_Wernes_Field";
        public const string House = "Realm_03_Wernes_House";
        public const string Row = "Realm_04_Wernes_Row";

        public static IEnumerable<SceneScript> All()
        {
            yield return BuildGate();
            yield return BuildField();
            yield return BuildHouse();
            yield return BuildRow();
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

        // ---------------------------------------------------------------- Arit 1: the First Gate

        /// <summary>Cue that starts the guided first spell: the director shows Enter, then the name in grey letters to type over.</summary>
        public const string TeachSpeakCue = "teach_speak:";

        private static SceneScript BuildGate()
        {
            var L = new Lines("arit1");
            var room = new RoomDef
            {
                Id = "arit1",
                Title = "The First Gate",
                Verbs = new HashSet<string> { "name" },
                Goal = new[] { new Cond("gate", "open") },
            };
            room.Objects.Add(Obj("announcer", "announcer", "unknown", new WordGloss("announcer", "smi", "A2"), null, "announcer", "herald", "caller"));
            room.Objects.Add(Obj("guardian", "guardian", "unknown", new WordGloss("guardian", "sawty", "V17 A1"), null, "guardian", "watcher"));
            room.Objects.Add(Obj("keeper", "keeper", "unknown", new WordGloss("keeper", "iry-aa", "O31 A1"), null, "keeper"));
            room.Objects.Add(Obj("gate", "gate", "shut", new WordGloss("gate", "arit", "O31 O1"), null, "gate", "arit", "door", "doors"));

            var announcerName = new WordGloss("Sad of Voice", "hu-kheru", "A2 P8", true);
            var guardianName = new WordGloss("Eavesdropper", "sedjem", "F21 G17", true);
            var keeperName = new WordGloss("Face-downward", "her-khed", "D2 D21 N35A", true);
            const string notYet = "You have not learned that name. A name you do not know is only noise here.";
            foreach (string n in new[] { "sad of voice", "sad-of-voice", "of voice" })
                room.Rules.Add(new SpellRule { Verb = "name", Target = "announcer", NameValue = n, NameGloss = announcerName, From = new[] { "known" }, To = "answered", Refusal = notYet, Event = "announcer_answered" });
            foreach (string n in new[] { "eavesdropper", "eaves-dropper", "eaves dropper" })
                room.Rules.Add(new SpellRule { Verb = "name", Target = "guardian", NameValue = n, NameGloss = guardianName, From = new[] { "known" }, To = "answered", Refusal = notYet, Event = "guardian_answered" });
            foreach (string n in new[] { "face-downward", "face downward", "numerous of shapes", "face-downward numerous of shapes", "face downward numerous of shapes" })
                room.Rules.Add(new SpellRule { Verb = "name", Target = "keeper", NameValue = n, NameGloss = keeperName, From = new[] { "known" }, To = "answered", Refusal = notYet, Event = "keeper_answered" });
            room.Rules.Add(new SpellRule
            {
                Verb = "auto", Target = "gate", From = new[] { "shut" }, To = "open",
                Requires = new[] { new Cond("announcer", "answered"), new Cond("guardian", "answered"), new Cond("keeper", "answered") },
                Event = "gate_open",
            });
            room.Rules.Add(new SpellRule
            {
                Verb = "name", Target = "gate", NameValue = "ankhu", From = new[] { "never" }, To = "never",
                Requires = new[] { new Cond("gate", "shut") },
                Refusal = "The gate is still listening. It wants the names of those who keep it.",
            });
            // Room guides 2026-11-26: once the gate stands open it no longer asks for names.
            room.Rules.Add(new SpellRule
            {
                Verb = "name", Target = "gate", NameValue = "ankhu", From = new[] { "never" }, To = "never",
                Requires = new[] { new Cond("gate", "open") },
                Refusal = "The gate already stands open. Walk through.",
            });
            // Zac 2026-11-26 playtest: Face-downward lies nearest the way in, so he is named first, with a guided
            // first spell. The plinth opens once he has answered, and Anpu names the announcer once the guardian has.
            room.Hints.Add(new Hint { When = new[] { new Cond("keeper", "unknown") }, Text = "The figure nearest you lies on its face. Go and look at it (E)." });
            room.Hints.Add(new Hint { When = new[] { new Cond("keeper", "known") }, Text = "Press Enter and speak the keeper's name: Face-downward." });
            room.Hints.Add(new Hint { When = new[] { new Cond("guardian", "unknown") }, Text = "The middle figure stands on a plinth with writing on it. Walk up to it and press E." });
            room.Hints.Add(new Hint { When = new[] { new Cond("guardian", "known") }, Text = "Press Enter and speak the guardian's name: Eavesdropper." });
            room.Hints.Add(new Hint { When = new[] { new Cond("announcer", "known") }, Text = "Press Enter and speak the announcer's name: Sad of Voice." });

            var s = new SceneScript { SceneName = Gate, Realm = "The First Gate", Title = "The First Gate", Room = room };
            s.Usables.Add(new Usable { Id = "keeper_figure", Prompt = "Look at the fallen figure", Zone = "fallen" });
            s.Usables.Add(new Usable { Id = "guardian_plinth", Prompt = "Read the plinth", Zone = "plinth", When = new[] { new Cond("keeper", "answered") } });

            // The voiced lines keep the keys they were recorded under (dialogue.<who>.arit1.<n>, numbered in the
            // order first written), so they are made here in that order and placed below in play order.
            Step anpu1 = L.S(Speaker.Anpu, "Seven gates stand between you and the scale. Each has three who mind it. One calls, one watches, one keeps.");
            Step ankhu1 = L.S(Speaker.Ankhu, "I know this. I have read it over the dead.");
            Step anpu2 = L.S(Speaker.Anpu, "You have read it. Now it is read over you.");
            Step anpu3 = L.S(Speaker.Anpu, "Take this. It is empty. Every soul who walks the Duat writes its own road.");
            Step ankhu2 = L.S(Speaker.Ankhu, "The pages are blank.");
            Step anpu4 = L.S(Speaker.Anpu, "So is every road, before it is walked.");
            Step announcer1 = L.S(Speaker.Announcer, "Who comes? Who comes to the gate in the night?");
            Step anpu5 = L.S(Speaker.Anpu, "That one is Sad of Voice. He announces everyone the same way. Do not take it to heart.");
            Step anpu6 = L.S(Speaker.Anpu, "Speak a name in the Duat and its owner must answer. Try his.");
            Step ankhu3 = L.S(Speaker.Ankhu, "Eavesdropper. The one who hears what is said at the gate.");
            Step guardian1 = L.S(Speaker.Guardian, "A son. A father. A husband. A man who stopped speaking.");
            Step ankhu4 = L.S(Speaker.Ankhu, "Those were Anpu's words on the road.");
            Step anpu7 = L.S(Speaker.Anpu, "He hears everything. That is his work.");
            Step ankhu5 = L.S(Speaker.Ankhu, "Eavesdropper.");
            Step ankhu6 = L.S(Speaker.Ankhu, "This one has no name written. It lies on its face, like the dead before the washing.");
            Step anpu8 = L.S(Speaker.Anpu, "It does not need one written. Face-downward, Numerous of Shapes. You have met.");
            Step announcer2 = L.S(Speaker.Announcer, "I know you now. I will announce you as I announce them all.");
            Step guardian2 = L.S(Speaker.Guardian, "Called by name. I listen for you now.");
            Step keeper1 = L.S(Speaker.Keeper, "Someone knows my name. I lift my face.");
            Step anpu9 = L.S(Speaker.Anpu, "A gate is not opened by force. It is opened by being known.");
            Step announcer3 = L.S(Speaker.Announcer, "Ankhu comes, who wore the face of Anpu over the dead.");
            Step anpu10 = L.S(Speaker.Anpu, "I can give you the first gate. I cannot give you the others.");
            Step anpu11 = L.S(Speaker.Anpu, "But I can teach you how to walk them. Below this gate is my pavilion, where the embalmers learn their craft. Come down.");
            // New 2026-11-26, not yet recorded (they play as text).
            Step anpu12 = L.S(Speaker.Anpu, "Let him call. Begin with the one nearest you, the one lying on its face. Go and look at it.");
            Step anpu13 = L.S(Speaker.Anpu, "Good. The one on the plinth has his name cut into the stone. Go and read it.");

            s.Beats.Add(new Beat("arrive", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Title("THE FIRST GATE\nSpell for knowing the gates", 4.5f),
                anpu1, ankhu1, anpu2,
                Step.Cue("anpu_offers_codex"),
                anpu3, ankhu2, anpu4,
                Step.Write("lore.codex"),
                Step.Write("lore.gate_one"),
                Step.Free()));

            s.Beats.Add(new Beat("gate_calls", TriggerKind.EnterZone, "court",
                Step.Freeze(),
                Step.Cue("announcer_calls"),
                announcer1,
                anpu12,
                Step.Free()));

            s.Beats.Add(new Beat("look_fallen", TriggerKind.Use, "keeper_figure",
                Step.Freeze(),
                ankhu6, anpu8,
                Step.Set("keeper", "known"),
                Step.Write("clue.keeper_name"),
                Step.Write("word.name"),
                anpu6,
                Step.Cue(TeachSpeakCue + "Face-downward"),
                Step.Free()));

            s.Beats.Add(new Beat("read_plinth", TriggerKind.Use, "guardian_plinth",
                Step.Freeze(),
                ankhu3,
                Step.Set("guardian", "known"),
                Step.Write("clue.guardian_name"),
                Step.Cue("guardian_repeats"),
                guardian1, ankhu4, anpu7,
                Step.Free()));
            s.Beats.Add(new Beat("read_plinth_again", TriggerKind.Use, "guardian_plinth", ankhu5) { Once = false, RequiresFlags = new[] { "plinth_read" } });
            s.Beats.Add(new Beat("plinth_flag", TriggerKind.Use, "guardian_plinth", Step.Flag("plinth_read")));

            s.Beats.Add(new Beat("k", TriggerKind.Event, "keeper_answered", Step.Wait(0.6f), keeper1, anpu13));
            s.Beats.Add(new Beat("g", TriggerKind.Event, "guardian_answered", Step.Wait(0.6f), guardian2, anpu5,
                Step.Set("announcer", "known"),
                Step.Write("clue.announcer_name")));
            s.Beats.Add(new Beat("a", TriggerKind.Event, "announcer_answered", Step.Wait(0.6f), announcer2));
            s.Beats.Add(new Beat("refused", TriggerKind.Failure, "Refused", anpu9));

            s.Beats.Add(new Beat("opened", TriggerKind.Solved, "",
                Step.Freeze(),
                Step.Cue("gate_opens"),
                Step.Wait(2.6f),
                announcer3,
                Step.Write("lore.known_at_gate"),
                anpu10, anpu11,
                Step.Flag("open"),
                Step.Free()));

            s.Beats.Add(new Beat("through", TriggerKind.EnterZone, "beyond", Step.Freeze(), Step.Next()) { RequiresFlags = new[] { "open" } });
            return s;
        }

        // ---------------------------------------------------------------- Room 1.1: the Field That Waits

        private static SceneScript BuildField()
        {
            var L = new Lines("field");
            var room = new RoomDef
            {
                Id = "field_that_waits",
                Title = "The Field That Waits",
                Verbs = new HashSet<string> { "name", "open", "wake", "still" },
                Goal = new[] { new Cond("lotus", "open") },
            };
            room.Objects.Add(Obj("sluice", "sluice", "shut", new WordGloss("sluice", "sba", "O31 N35A"), null,
                "sluice", "sluice gate", "floodgate", "gate", "canal", "channel"));
            room.Objects.Add(Obj("shabti", "shabti", "asleep", new WordGloss("shabti", "shabti", "A1 Z1"), null,
                "shabti", "worker", "figure", "servant", "clay figure", "clay man", "ushabti"));
            room.Objects.Add(Obj("basin", "basin", "dry", new WordGloss("water", "mu", "N35A"), null,
                "basin", "water", "pool", "bowl"));
            room.Objects.Add(Obj("lotus", "lotus", "closed", new WordGloss("lotus", "seshen", "M9"), null,
                "lotus", "bud", "flower", "lotus bud"));

            room.Rules.Add(new SpellRule { Verb = "open", Target = "sluice", From = new[] { "shut" }, To = "open", Event = "sluice_open",
                Success = "Water runs into the channel beside the worker. It cannot climb to the basin." });
            room.Rules.Add(new SpellRule { Verb = "wake", Target = "shabti", From = new[] { "asleep" }, To = "carrying",
                Requires = new[] { new Cond("sluice", "open") }, Also = new[] { ("basin", "rippled") }, Event = "shabti_carrying",
                Success = "It dips, carries and pours, and never stops. The basin fills. The bud rises and stays shut." });
            room.Rules.Add(new SpellRule { Verb = "wake", Target = "shabti", From = new[] { "asleep" }, To = "empty",
                Requires = new[] { new Cond("sluice", "shut") }, Event = "shabti_empty",
                Success = "It carries empty jars to the basin. It pours nothing, carefully." });
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "shabti", From = new[] { "empty" }, To = "carrying",
                Requires = new[] { new Cond("sluice", "open"), new Cond("shabti", "empty") }, Also = new[] { ("basin", "rippled") }, Event = "shabti_carrying" });
            room.Rules.Add(new SpellRule { Verb = "still", Target = "shabti", From = new[] { "carrying" }, To = "asleep",
                Also = new[] { ("basin", "mirror"), ("lotus", "open") }, Event = "mirror" });
            room.Rules.Add(new SpellRule { Verb = "still", Target = "shabti", From = new[] { "empty" }, To = "asleep",
                Success = "The worker rests. It had carried nothing." });
            room.Rules.Add(new SpellRule { Verb = "still", Target = "basin", From = new[] { "rippled" }, To = "mirror",
                Requires = new[] { new Cond("shabti", "never") },
                Refusal = "The water settles for a breath, and the next pour breaks it. The worker will not stop on its own." });
            room.Rules.Add(new SpellRule { Verb = "still", Target = "basin", From = new[] { "dry" }, To = "mirror",
                Requires = new[] { new Cond("shabti", "never") },
                Refusal = "There is no water in the basin to be still." });
            room.Rules.Add(new SpellRule { Verb = "open", Target = "lotus", From = new[] { "closed" }, To = "open",
                Requires = new[] { new Cond("basin", "never") },
                Refusal = "The bud has all the water it needs. It is waiting for something else." });
            room.Hints.Add(new Hint { When = new[] { new Cond("sluice", "shut") }, Text = "The canal is full and the sluice is shut. Open it." });
            room.Hints.Add(new Hint { When = new[] { new Cond("shabti", "asleep"), new Cond("basin", "dry") }, Text = "Water runs beside the clay worker, but cannot reach the basin. Wake the shabti." });
            room.Hints.Add(new Hint { When = new[] { new Cond("shabti", "empty") }, Text = "The worker carries nothing. The water has to reach it first." });
            room.Hints.Add(new Hint { When = new[] { new Cond("basin", "rippled") }, Text = "The bud has all the water it needs. The worker will not stop on its own. Still the shabti." });

            var s = new SceneScript { SceneName = Field, Realm = "Wernes", Title = "The Field That Waits", Room = room,
                CodexOnEntry = new[] { "lore.codex", "word.name", "clue.announcer_name", "clue.guardian_name", "clue.keeper_name" } };

            s.Beats.Add(new Beat("arrive", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Title("WERNES\nThe first land", 4.5f),
                L.S(Speaker.Ankhu, "Who are they? The grey ones, in the far fields. They never look up."),
                L.S(Speaker.Anpu, "They forgot."),
                Step.Write("lore.wernes"),
                Step.Wait(0.4f),
                L.S(Speaker.Anpu, "Here the dead are given fields. This one is yours. It is waiting."),
                L.S(Speaker.Anpu, "In the old tongue the word is wen. In yours, open. Say it to what is shut."),
                Step.Write("word.open"),
                Step.Free()));

            s.Beats.Add(new Beat("sluice", TriggerKind.Event, "sluice_open",
                Step.Wait(0.8f),
                L.S(Speaker.Ankhu, "The water cannot climb to the basin."),
                L.S(Speaker.Anpu, "Then something must carry it. Your worker sleeps on its feet. Res. Wake."),
                Step.Write("word.wake"),
                Step.Write("lore.shabti")));

            s.Beats.Add(new Beat("empty", TriggerKind.Event, "shabti_empty",
                Step.Wait(0.6f),
                L.S(Speaker.Anpu, "It does exactly what it is told. Nothing more.")));

            s.Beats.Add(new Beat("carrying", TriggerKind.Event, "shabti_carrying",
                Step.Wait(1.4f),
                L.S(Speaker.Ankhu, "The basin is full. The bud will not open."),
                L.S(Speaker.Anpu, "Every pour breaks the water. Look at the worker. It will never stop unless it is told."),
                L.S(Speaker.Anpu, "Hetep. Peace. The word is still."),
                Step.Write("word.still")));

            s.Beats.Add(new Beat("refused", TriggerKind.Failure, "Refused",
                L.S(Speaker.Anpu, "Not the water. What keeps breaking it?")));
            s.Beats.Add(new Beat("unknown", TriggerKind.Failure, "UnknownVerb",
                L.S(Speaker.Anpu, "That word is not yours yet. The land will give it to you when you need it.")));

            s.Beats.Add(new Beat("mirror", TriggerKind.Event, "mirror",
                Step.Freeze(),
                Step.Wait(1.6f),
                Step.Caption("The water settles. The sky is in it. Then a low sun that is not in the sky above."),
                L.M("mem_shoulders", Speaker.Hori, "He is flying. Look. He is flying to Heru."),
                L.M("mem_shoulders", Speaker.Ankhu, "He is clay. He will fall and break, and you will cry."),
                L.M("mem_shoulders", Speaker.Hori, "He will not fall. You are holding me, and I am holding him."),
                L.M("mem_shoulders", Speaker.Neferet, "Bring him back before dark. He will not sleep if he sees the stars come out."),
                Step.Write("memory.shoulders"),
                Step.Write("name.hori"),
                L.S(Speaker.Ankhu, "Hori. His falcon. I had forgotten the falcon."),
                L.S(Speaker.Anpu, "The worker stopped, and you remembered. That is the whole of Wernes."),
                Step.Flag("done"),
                Step.Free()));

            s.Beats.Add(new Beat("leave", TriggerKind.EnterZone, "exit", Step.Freeze(), Step.Next()) { RequiresFlags = new[] { "done" } });
            s.Beats.Add(new Beat("leave_early", TriggerKind.EnterZone, "exit",
                L.S(Speaker.Anpu, "Your field is still waiting.")) { Once = false, ForbidsFlags = new[] { "done" } });
            return s;
        }

        // ---------------------------------------------------------------- Room 1.2: the House With One Door

        private static SceneScript BuildHouse()
        {
            var L = new Lines("house");
            var room = new RoomDef
            {
                Id = "house_one_door",
                Title = "The House With One Door",
                Verbs = new HashSet<string> { "name", "open", "wake", "still" },
                Goal = new[] { new Cond("plot", "wet") },
            };
            room.Objects.Add(Obj("upper", "upper sluice", "open", new WordGloss("sluice", "sba", "O31 N35A"), null,
                "upper sluice", "upper", "top sluice", "high sluice", "upper gate", "jammed sluice"));
            room.Objects.Add(Obj("lower", "lower sluice", "shut", new WordGloss("sluice", "sba", "O31 N35A"), null,
                "lower sluice", "lower", "bottom sluice", "low sluice", "lower gate", "drain"));
            room.Objects.Add(Obj("plot", "plot", "drowning", new WordGloss("field", "sekhet", "M20"), null,
                "plot", "field", "seedlings", "garden", "seeds"));
            room.Objects.Add(Obj("falsedoor", "false door", "dark", new WordGloss("door", "aa", "O31"), null,
                "false door", "door", "stela"));

            room.Rules.Add(new SpellRule { Verb = "open", Target = "lower", From = new[] { "shut" }, To = "open", Event = "lower_open" });
            room.Rules.Add(new SpellRule { Verb = "seal", Target = "lower", From = new[] { "open" }, To = "shut" });
            room.Rules.Add(new SpellRule { Verb = "seal", Target = "upper", From = new[] { "open" }, To = "shut", Event = "upper_sealed" });
            room.Rules.Add(new SpellRule { Verb = "open", Target = "upper", From = new[] { "shut" }, To = "open" });
            room.Rules.Add(new SpellRule { Verb = "still", Target = "plot", From = new[] { "drowning", "scoured" }, To = "wet",
                Requires = new[] { new Cond("upper", "never") },
                Refusal = "The water calms, and the sluice above pours more in. Stillness cannot hold back a flood." });
            room.Rules.Add(new SpellRule { Verb = "open", Target = "falsedoor", From = new[] { "dark" }, To = "dark",
                Requires = new[] { new Cond("upper", "never") },
                Refusal = "A false door is not opened with a word. It is walked through. Go to it." });
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "plot", To = "drowning", Requires = new[] { new Cond("upper", "open"), new Cond("lower", "shut") } });
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "plot", To = "scoured", Requires = new[] { new Cond("upper", "open"), new Cond("lower", "open") }, Event = "scoured" });
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "plot", To = "wet", Requires = new[] { new Cond("upper", "shut") }, Event = "plot_wet" });
            room.Hints.Add(new Hint { When = new[] { new Cond("lower", "shut"), new Cond("falsedoor", "dark") }, Text = "The plot is drowning. Is there a way for the water to leave?" });
            room.Hints.Add(new Hint { When = new[] { new Cond("falsedoor", "dark") }, Text = "Nothing you know stops water. The false door on the wall is lit. Walk to it and press E." });
            room.Hints.Add(new Hint { When = new[] { new Cond("upper", "open") }, Text = "You have the word now. Seal the upper sluice." });

            var s = new SceneScript { SceneName = House, Realm = "Wernes", Title = "The House With One Door", Room = room,
                CodexOnEntry = new[] { "lore.codex", "word.name", "word.open", "word.wake", "word.still", "memory.shoulders" } };
            s.Usables.Add(new Usable { Id = "false_door", Prompt = "Walk through the false door", Zone = "falsedoor" });

            s.Beats.Add(new Beat("arrive", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Wait(0.6f),
                L.S(Speaker.Anpu, "Here you are not required to labour."),
                L.S(Speaker.Ankhu, "Then what am I required to do?"),
                L.S(Speaker.Anpu, "That is the question the land is asking."),
                Step.Wait(0.4f),
                L.S(Speaker.Ankhu, "The seedlings are drowning. The upper sluice is jammed open."),
                Step.Write("lore.house"),
                Step.Free()));

            s.Beats.Add(new Beat("lower", TriggerKind.Event, "lower_open",
                Step.Wait(1.2f),
                L.S(Speaker.Ankhu, "It drains, and the water above tears at the roots instead."),
                L.S(Speaker.Anpu, "Nothing you know stops water. There is a word for it. You have been afraid of it a long time.")));

            s.Beats.Add(new Beat("unknown", TriggerKind.Failure, "UnknownVerb",
                L.S(Speaker.Anpu, "You know it exists. Your mouth will not make it. Ask the door on the wall why.")));

            s.Beats.Add(new Beat("memory", TriggerKind.Use, "false_door",
                Step.Freeze(),
                Step.Cue("false_door"),
                Step.Wait(1.0f),
                L.M("mem_door_dawn", Speaker.Narrator, "The house of his childhood. One room. Dawn. He is six."),
                L.M("mem_door_dawn", Speaker.Father, "Mind your mother. I will bring you something from the north."),
                L.M("mem_door_dawn", Speaker.Ankhu, "I did not answer him. I wanted him to see that I was angry. Then the door shut."),
                L.M("mem_door_dawn", Speaker.Narrator, "The bolt is on the outside of the frame, at a child's eye height. The boy looks at it."),
                L.M("mem_door_dawn", Speaker.Ankhu, "He did not come back. I have hated that word since. Khetem. Seal."),
                Step.Learn("seal"),
                Step.Set("falsedoor", "remembered"),
                Step.Write("memory.door_dawn"),
                L.S(Speaker.Anpu, "A shut door kept the night out of that house for years. Use the word for something small."),
                Step.Free()));

            s.Beats.Add(new Beat("wet", TriggerKind.Solved, "",
                Step.Freeze(),
                Step.Wait(1.4f),
                Step.Caption("The plot settles to wet earth. The seedlings stand."),
                L.S(Speaker.Ankhu, "It held."),
                L.S(Speaker.Anpu, "A seal is not only a wall. Sometimes it is a hand over a cup."),
                Step.Write("lore.seal_saves"),
                Step.Wait(0.8f),
                Step.Next()));
            return s;
        }

        // ---------------------------------------------------------------- Room 1.3: Shabti Row

        private static readonly string[] Ordinal = { "first", "second", "third" };

        private static SceneScript BuildRow()
        {
            var L = new Lines("row");
            var room = new RoomDef
            {
                Id = "shabti_row",
                Title = "Shabti Row",
                Verbs = new HashSet<string> { "name", "open", "wake", "still", "seal" },
            };
            var goal = new List<Cond>();
            for (int i = 1; i <= 3; i++)
            {
                string n = Ordinal[i - 1], zone = "row" + i, sh = "shabti" + i, sl = "sluice" + i, pl = "plot" + i;
                room.Objects.Add(Obj(sh, n + " shabti", "asleep", new WordGloss("shabti", "shabti", "A1 Z1"), new[] { zone },
                    "shabti", "worker", "clay worker", "figure", "clay figure", n + " shabti", n + " worker", "blank shabti", "ushabti"));
                room.Objects.Add(Obj(sl, n + " sluice", "shut", new WordGloss("sluice", "sba", "O31 N35A"), new[] { zone },
                    "sluice", "gate", "sluice gate", n + " sluice", n + " gate"));
                room.Objects.Add(Obj(pl, n + " plot", "dry", new WordGloss("field", "sekhet", "M20"), new[] { zone },
                    "plot", "field", "furrows", "earth", "seedbed", n + " plot", n + " field"));

                room.Rules.Add(new SpellRule { Verb = "open", Target = sl, From = new[] { "shut" }, To = "open",
                    Requires = new[] { new Cond("fifth", "carrying"), new Cond(sh, "asleep") }, Also = new[] { (pl, "drowned") }, Event = "flood",
                    Success = "The canal is brimming. Water bursts through the sluice and drowns the furrows." });
                room.Rules.Add(new SpellRule { Verb = "open", Target = sl, From = new[] { "shut" }, To = "open",
                    Requires = new[] { new Cond("fifth", "carrying"), new Cond(sh, "carrying") },
                    Success = "Water pours in, and the clay worker bails it out as fast as it comes." });
                room.Rules.Add(new SpellRule { Verb = "open", Target = sl, From = new[] { "shut" }, To = "open",
                    Requires = new[] { new Cond("fifth", "asleep") }, Event = "low_canal",
                    Success = "The sluice lifts. The canal has sunk below it. Nothing comes through." });
                room.Rules.Add(new SpellRule { Verb = "seal", Target = sl, From = new[] { "open" }, To = "shut",
                    Success = "The sluice drops and holds." });
                room.Rules.Add(new SpellRule { Verb = "wake", Target = sh, From = new[] { "asleep" }, To = "carrying",
                    Requires = new[] { new Cond(pl, "drowned") }, Also = new[] { (pl, "wet") }, Event = "bail",
                    Refusal = "It lifts its head, looks at the earth, and sleeps again. There is nothing here for it to carry." });
                room.Rules.Add(new SpellRule { Verb = "still", Target = sh, From = new[] { "carrying" }, To = "asleep",
                    Success = "The clay worker sets down its jars." });
                room.Rules.Add(new SpellRule { Verb = "still", Target = pl, From = new[] { "drowned" }, To = "drowned",
                    Requires = new[] { new Cond(pl, "never") },
                    Refusal = "The flood lies quiet for a breath, and it is still a flood. Something has to carry it away." });
                room.Rules.Add(new SpellRule { Verb = "auto", Target = pl, From = new[] { "wet" }, To = "drowned",
                    Requires = new[] { new Cond(sh, "asleep"), new Cond(sl, "open"), new Cond("fifth", "carrying") }, Event = "redrown" });
                goal.Add(new Cond(pl, "wet"));
                goal.Add(new Cond(sh, "asleep"));
            }

            // The fifth shabti answers only to the name on its chest.
            const string notMine = "It does not turn. It does not answer to \"shabti\". There is something written on its chest.";
            RoomObject fifth = Obj("fifth", "painted shabti", "carrying", new WordGloss("Ankhu", "ankhu", "S34 N35 Aa1 G43", true), new[] { "row5" },
                "ankhu", "shabti", "worker", "figure", "painted shabti", "fine shabti", "fifth shabti", "last shabti", "painted worker", "fifth", "ushabti");
            foreach (string a in new[] { "", "shabti", "worker", "figure", "painted shabti", "fine shabti", "fifth shabti", "last shabti", "painted worker", "fifth", "ushabti" })
                fifth.Refuses[a] = notMine;
            room.Objects.Add(fifth);
            room.Rules.Add(new SpellRule { Verb = "still", Target = "fifth", From = new[] { "carrying" }, To = "asleep", Event = "fifth_still",
                Success = "The jars come down. The worn channel runs empty. The canal sinks." });
            room.Rules.Add(new SpellRule { Verb = "wake", Target = "fifth", From = new[] { "asleep" }, To = "carrying", Event = "fifth_wake",
                Success = "It lifts its jars again. It has always lifted them." });
            goal.Add(new Cond("fifth", "asleep"));
            room.Goal = goal.ToArray();

            room.Hints.Add(new Hint { When = new[] { new Cond("fifth", "asleep"), new Cond("plot1", "dry", "drowned"), }, Text = "The canal has sunk. Only the painted worker kept it full. Wake it again, and still it last." });
            room.Hints.Add(new Hint { When = new[] { new Cond("fifth", "asleep"), new Cond("plot2", "dry", "drowned") }, Text = "The canal has sunk. Only the painted worker kept it full. Wake it again, and still it last." });
            room.Hints.Add(new Hint { When = new[] { new Cond("fifth", "asleep"), new Cond("plot3", "dry", "drowned") }, Text = "The canal has sunk. Only the painted worker kept it full. Wake it again, and still it last." });
            for (int i = 1; i <= 3; i++)
            {
                string n = Ordinal[i - 1];
                room.Hints.Add(new Hint { When = new[] { new Cond("plot" + i, "drowned"), new Cond("shabti" + i, "asleep") }, Text = $"The {n} plot is flooded. Its clay worker could carry the water off. Stand by it and wake the shabti." });
                room.Hints.Add(new Hint { When = new[] { new Cond("plot" + i, "wet"), new Cond("shabti" + i, "carrying"), new Cond("sluice" + i, "open") }, Text = $"The {n} worker is bailing against an open sluice. Seal the sluice, then still the shabti." });
                room.Hints.Add(new Hint { When = new[] { new Cond("plot" + i, "wet"), new Cond("shabti" + i, "carrying") }, Text = $"The {n} plot is wet. Its worker can rest. Still the shabti." });
                room.Hints.Add(new Hint { When = new[] { new Cond("plot" + i, "dry") }, Text = $"The {n} plot is dry. The canal is brimming behind its sluice. Open it." });
            }
            room.Hints.Add(new Hint { When = new[] { new Cond("fifth", "carrying") }, Text = "Every plot is wet. One worker is still carrying at the end of the row. Read its chest, then still it by that name." });

            var s = new SceneScript { SceneName = Row, Realm = "Wernes", Title = "Shabti Row", Room = room,
                CodexOnEntry = new[] { "lore.codex", "word.name", "word.open", "word.wake", "word.still", "word.seal", "memory.shoulders", "memory.door_dawn" } };
            s.Usables.Add(new Usable { Id = "chest", Prompt = "Read the name on its chest", Zone = "row5" });

            s.Beats.Add(new Beat("arrive", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Title("SHABTI ROW", 3.5f),
                L.S(Speaker.Ankhu, "Three plots, three sluices, and the canal brimming behind them. Who keeps it so full?"),
                L.S(Speaker.Anpu, "Walk the row. Each worker hears whoever stands beside it."),
                Step.Write("lore.shabti_row"),
                Step.Free()));

            s.Beats.Add(new Beat("flood", TriggerKind.Event, "flood",
                Step.Wait(1.0f),
                L.S(Speaker.Ankhu, "Too much. The canal is too full."),
                L.S(Speaker.Anpu, "Then give the water somewhere to go. Your workers carry.")));

            s.Beats.Add(new Beat("ambiguous", TriggerKind.Failure, "Ambiguous",
                L.S(Speaker.Anpu, "They all answer to that word. Stand beside the one you mean.")));
            s.Beats.Add(new Beat("refused", TriggerKind.Failure, "Refused",
                L.S(Speaker.Anpu, "Look closer at what you are speaking to.")));

            s.Beats.Add(new Beat("chest", TriggerKind.Use, "chest",
                Step.Freeze(),
                Step.Cue("chest"),
                Step.Wait(0.6f),
                L.S(Speaker.Ankhu, "This one has my name."),
                L.S(Speaker.Anpu, "They put it in your tomb. It answers for you when the fields call."),
                L.S(Speaker.Ankhu, "It has not stopped."),
                L.S(Speaker.Anpu, "Nobody told it to."),
                Step.Write("clue.shabti_chest"),
                Step.Free()));

            s.Beats.Add(new Beat("fifth_still_early", TriggerKind.Event, "fifth_still",
                Step.Wait(1.0f),
                L.S(Speaker.Anpu, "It stopped. And the canal sank with it. Everything in this row was fed by its labour.")) { ForbidsFlags = new[] { "solved" } });

            s.Beats.Add(new Beat("solved", TriggerKind.Solved, "",
                Step.Flag("solved"),
                Step.Freeze(),
                Step.Wait(1.6f),
                Step.Caption("The painted worker sets down its jars. Its channel is worn a hand deep. For the first time it rests."),
                L.S(Speaker.Ankhu, "I made it to work so I could rest. I never rested."),
                Step.Wait(0.6f),
                L.M("mem_naming", Speaker.Narrator, "The same one-room house, earlier. A woman, exhausted and laughing, holds a newborn."),
                L.M("mem_naming", Speaker.Other, "What is he called?"),
                L.M("mem_naming", Speaker.Mother, "Ankhu. He lives. Say it so the gods hear. He lives."),
                L.M("mem_naming", Speaker.Narrator, "She is the loudest person in his life, and at the start that is joy."),
                Step.Write("memory.naming"),
                L.S(Speaker.Ankhu, "She shouted it. She shouted my name at the gods."),
                L.S(Speaker.Anpu, "And tonight you said it to stop. Remember how. You will need it."),
                Step.Wait(0.6f),
                Step.Title("End of the demo\nWernes continues at the second gate", 6f),
                Step.End()));
            return s;
        }
    }
}
