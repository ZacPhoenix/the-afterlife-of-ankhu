using System.Collections.Generic;

namespace Ankhu.Realms.Core.Content
{
    /// <summary>
    /// Wernes in story bible order, painted 2.5D, per the Wernes build spec v2
    /// (/mnt/project-files/design/next_area/wernes_build_spec_v2.md). Rules are ported one to one from the
    /// solver design/next_area/solver/wernes_rooms_v2.py: every utterance, deed or wait is one tick.
    /// These are new scenes. The older Realm_02..04 scenes and scripts stay as they are.
    /// </summary>
    public static partial class WernesPaintedScripts
    {
        public const string FieldA = "Wernes_A_Field";

        public static IEnumerable<SceneScript> All()
        {
            yield return BuildTerrace();
            yield return BuildHub();
            yield return BuildFieldA();
            yield return BuildShabtiB();
            yield return BuildHouseC();
            yield return BuildBricksD();
            yield return BuildFinale();
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

        // ---------------------------------------------------------------- Room A: The Field That Waits

        /// <summary>
        /// Shabti A carries canal water to the trough (sluice open). Shabti B works the shaduf, whose beam swings
        /// up and down each tick while B carries and nothing holds it. The basin fills one step per tick while the
        /// sluice is open and both carry. Hori's clay falcon sits on the ledge, reached only by riding the beam while
        /// it is held at the top. Goal: basin full, both asleep, falcon on the perch, beam not held.
        /// </summary>
        private static SceneScript BuildFieldA()
        {
            var L = new Lines("wernes_a");
            var room = new RoomDef
            {
                Id = "wernes_a",
                Title = "The Field That Waits",
                Verbs = new HashSet<string> { "bind", "release", "ride", "take", "place" },
                Deeds = new HashSet<string> { "ride", "take", "place" },
                Goal = new[] { C("basin", "2"), C("shabti_a", "asleep"), C("shabti_b", "asleep"), C("falcon", "perch"), C("hold", "free") },
            };
            var shabti = new WordGloss("shabti", "shabti", "A53");
            room.Objects.Add(Obj("sluice", "sluice", "shut", new WordGloss("sluice", "sluice", "O31 N36"), new[] { "canal" }, "sluice", "sluice gate", "gate", "canal gate", "canal"));
            room.Objects.Add(Obj("shabti_a", "shabti at the trough", "asleep", shabti, new[] { "trough" },
                "shabti", "shabti a", "first shabti", "trough shabti", "worker", "servant"));
            room.Objects.Add(Obj("shabti_b", "shabti at the shaduf", "asleep", shabti, new[] { "shaduf" },
                "shabti", "shabti b", "second shabti", "shaduf shabti", "worker", "servant"));
            room.Objects.Add(Obj("beam", "shaduf beam", "down", new WordGloss("beam", "shaduf", "M3"), null, "beam", "shaduf", "arm", "shaduf beam", "pole", "sweep"));
            room.Objects.Add(Obj("hold", "binding", "free", null, null));
            room.Objects.Add(Obj("basin", "basin", "0", new WordGloss("basin", "she", "N37"), null, "basin", "pool", "water", "terrace basin"));
            room.Objects.Add(Obj("falcon", "clay falcon", "ledge", new WordGloss("falcon", "bik", "G5"), null, "falcon", "clay falcon", "toy", "bird"));
            room.Objects.Add(Obj("perch", "perch", "empty", null, new[] { "basin" }, "perch"));
            room.Objects.Add(Obj("ankhu", "Ankhu", "ground", null, null));
            room.Objects.Add(Obj("settle", "settle", "none", null, null));
            room.Objects.Add(Obj("lotus", "lotus", "shut", null, null, "lotus", "bud"));

            // The world clock, exactly w1_tick: runs after every action on the new state.
            room.Tick = s =>
            {
                bool bCarries = s["shabti_b"] == "carrying";
                if (bCarries && s["hold"] == "free") s["beam"] = s["beam"] == "up" ? "down" : "up";
                int lvl = int.Parse(s["basin"]);
                if (s["sluice"] == "open" && s["shabti_a"] == "carrying" && bCarries && lvl < 2) s["basin"] = (lvl + 1).ToString();
            };

            room.Rules.Add(new SpellRule { Verb = "open", Target = "sluice", From = new[] { "shut" }, To = "open",
                Success = "The gate lifts. Canal water slides into the channel.", Event = "sluice_open" });
            room.Rules.Add(new SpellRule { Verb = "seal", Target = "sluice", From = new[] { "open" }, To = "never",
                Refusal = "Your mouth cannot shape it yet." });

            foreach (string id in new[] { "shabti_a", "shabti_b" })
            {
                if (id == "shabti_b")
                    room.Rules.Add(new SpellRule { Verb = "wake", Target = id, From = new[] { "asleep" }, To = "carrying",
                        Requires = new[] { C("sluice", "shut") }, Success = "It lifts an empty bucket, carefully.", Event = "b_awake" });
                room.Rules.Add(new SpellRule { Verb = "wake", Target = id, From = new[] { "asleep" }, To = "carrying",
                    Success = id == "shabti_a" ? "It shoulders the yoke and goes down to the water." : "It takes the rope. The beam begins to swing.",
                    Event = id == "shabti_a" ? "a_awake" : "b_awake" });
                room.Rules.Add(new SpellRule { Verb = "still", Target = id, From = new[] { "carrying" }, To = "asleep",
                    Success = "It sets down its load and sits. Its hands rest on its knees.", Event = "stilled" });
                room.Rules.Add(new SpellRule { Verb = "bind", Target = id, From = new[] { "never" }, To = "never",
                    Refusal = "It is held. It is not resting.", RefusalEvent = "learn_hetep" });
            }

            // Hold the beam. Up is the useful moment, down is legal and says so.
            room.Rules.Add(new SpellRule { Verb = "bind", Target = "beam", From = new[] { "up" }, Requires = new[] { C("hold", "free") },
                Also = new[] { ("hold", "bound") }, Success = "A cyan thread holds the beam at the top. Its footplate rests against the ledge.",
                Refusal = "It is already held.", Event = "beam_bound_up" });
            room.Rules.Add(new SpellRule { Verb = "bind", Target = "beam", From = new[] { "down" }, Requires = new[] { C("hold", "free") },
                Also = new[] { ("hold", "bound") }, Success = "Held, at the bottom.", Refusal = "It is already held.", Event = "beam_bound_down" });
            room.Rules.Add(new SpellRule { Verb = "release", Target = "beam", From = new[] { "up", "down" }, Requires = new[] { C("hold", "bound") },
                Also = new[] { ("hold", "free") }, Success = "The thread lets go.", Refusal = "Nothing is holding it." });

            // Deeds (E). Riding needs the beam held and up, both ways.
            room.Rules.Add(new SpellRule { Verb = "ride", Target = "ankhu", From = new[] { "ground" }, To = "ledge",
                Requires = new[] { C("hold", "bound"), C("beam", "up") }, Event = "ride_up",
                Refusal = "The beam will not carry you unless it is held, and at the top." });
            room.Rules.Add(new SpellRule { Verb = "ride", Target = "ankhu", From = new[] { "ledge" }, To = "ground",
                Requires = new[] { C("hold", "bound"), C("beam", "up") }, Event = "ride_down",
                Refusal = "The footplate is not here. Hold the beam when it comes up." });
            room.Rules.Add(new SpellRule { Verb = "take", Target = "falcon", From = new[] { "ledge" }, To = "hand",
                Requires = new[] { C("ankhu", "ledge") }, Success = "A small clay falcon, rubbed smooth by a child's thumb.", Event = "falcon_taken",
                Refusal = "It is out of reach." });
            room.Rules.Add(new SpellRule { Verb = "place", Target = "perch", From = new[] { "empty" }, To = "falcon",
                Requires = new[] { C("falcon", "hand") }, Also = new[] { ("falcon", "perch") }, Event = "falcon_placed",
                Success = "The falcon sits on the rim, looking at the water." });

            // What the water says when it is nearly right (raised once each, as captions).
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "settle", To = "carrying",
                Requires = new[] { C("basin", "2"), C("shabti_a", "carrying") }, Event = "full_but_working" });
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "settle", To = "carrying",
                Requires = new[] { C("basin", "2"), C("shabti_b", "carrying") }, Event = "full_but_working" });
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "settle", To = "shadow",
                Requires = new[] { C("basin", "2"), C("shabti_a", "asleep"), C("shabti_b", "asleep"), C("hold", "bound") }, Event = "full_but_shadow" });
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "settle", To = "still",
                Requires = new[] { C("basin", "2"), C("shabti_a", "asleep"), C("shabti_b", "asleep"), C("hold", "free") } });

            room.Hints.Add(new Hint { When = new[] { C("sluice", "shut") }, Text = "The basin is fed from the canal. The sluice is shut." });
            room.Hints.Add(new Hint { When = new[] { C("basin", "0", "1") }, Text = "The water needs both workers carrying, and the sluice open." });
            room.Hints.Add(new Hint { When = new[] { C("falcon", "ledge") }, Text = "The beam can carry you if it is held at the top." });
            room.Hints.Add(new Hint { When = new[] { C("falcon", "hand") }, Text = "There is a perch on the basin rim." });
            room.Hints.Add(new Hint { When = new[] { C("basin", "2") }, Text = "The water needs everyone to rest." });

            var s = new SceneScript { SceneName = FieldA, Realm = "Wernes", Title = "The Field That Waits", Room = room,
                CodexOnEntry = new[] { "word.bind", "word.release" }, Anpu = AnpuPresence.Absent }; // Anpu stays at the hub ("Go on. I will be here.")
            s.Usables.Add(new Usable { Id = "examine_shabti", Prompt = "Look at the sleeping shabti", Zone = "trough", When = new[] { C("shabti_a", "asleep") } });
            s.Usables.Add(new Usable { Id = "ride_up", Prompt = "Step onto the footplate", Zone = "beamfoot", Deed = "ride",
                When = new[] { C("ankhu", "ground"), C("hold", "bound"), C("beam", "up") } });
            s.Usables.Add(new Usable { Id = "take_falcon", Prompt = "Take the clay falcon", Zone = "ledge", Deed = "take falcon", When = new[] { C("falcon", "ledge") } });
            s.Usables.Add(new Usable { Id = "ride_down", Prompt = "Step back onto the footplate", Zone = "ledge", Deed = "ride",
                When = new[] { C("ankhu", "ledge"), C("hold", "bound"), C("beam", "up") } });
            s.Usables.Add(new Usable { Id = "place_falcon", Prompt = "Set the falcon on the perch", Zone = "basin", Deed = "place perch", When = new[] { C("falcon", "hand") } });
            s.Usables.Add(new Usable { Id = "read_vignette", Prompt = "Look at the painting", Zone = "vignette" });

            s.Beats.Add(new Beat("arrive", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Title("THE FIELD THAT WAITS", 4f),
                Step.Caption("A drip falls into a cup. The cup shivers. The drip stops, and the cup holds the sky."),
                L.S(Speaker.Anpu, "Wen. Open."),
                Step.Learn("open"),
                L.S(Speaker.Ankhu, "Wen. I said that word at every tomb door in Abdju. I had forgotten it was mine."),
                Step.Free()));

            s.Beats.Add(new Beat("examine", TriggerKind.Use, "examine_shabti",
                Step.Freeze(),
                L.S(Speaker.Ankhu, "A shabti. Written on its legs: if I am called to work in the fields of the god, here I am."),
                L.S(Speaker.Ankhu, "It is asleep. Someone has to tell it to rise. Res."),
                Step.Learn("wake"),
                Step.Free()));

            s.Beats.Add(new Beat("hetep", TriggerKind.Event, "learn_hetep",
                L.S(Speaker.Ankhu, "Held is not resting. There is a word for rest. Hetep. Be at peace."),
                Step.Learn("still")));

            s.Beats.Add(new Beat("vignette", TriggerKind.Use, "read_vignette",
                L.S(Speaker.Ankhu, "A worker seated beside a pool. In the pool, a falcon and the sun.")) { Once = false });

            s.Beats.Add(new Beat("full_working", TriggerKind.Event, "full_but_working",
                Step.Caption("The water is full and cannot settle.")));
            s.Beats.Add(new Beat("full_shadow", TriggerKind.Event, "full_but_shadow",
                Step.Caption("A shadow lies across the water.")));
            s.Beats.Add(new Beat("ride_first", TriggerKind.Event, "ride_up",
                L.S(Speaker.Ankhu, "Somebody used to stand here. The plate is worn in the shape of a foot.")));
            s.Beats.Add(new Beat("falcon", TriggerKind.Event, "falcon_taken",
                L.S(Speaker.Ankhu, "Put up high, out of reach. For safekeeping.")));

            s.Beats.Add(new Beat("murmur", TriggerKind.Idle, "", L.S(Speaker.Ankhu, "It goes up and down whether I watch or not. I only need it up once."))
                { Value = 45f });

            s.Beats.Add(new Beat("solved", TriggerKind.Solved, "",
                Step.Freeze(),
                Step.Caption("The water stills. The sky is in it. Then a low sun that is not in the sky above."),
                Step.Set("lotus", "open"),
                Step.Cue("flashback:tease_basin"),
                Step.Wait(1.0f),
                L.S(Speaker.Ankhu, "I looked at the child."),
                Step.Set("falcon", "kept"),
                Step.Caption("Ankhu takes the clay falcon and keeps it."),
                Step.Write("lore.field_waits"),
                Step.Wait(1.5f),
                Step.Next()));
            return s;
        }

        // ---------------------------------------------------------------- Room B: The Shabti With My Name

        public const string ShabtiB = "Wernes_B_Shabti";

        /// <summary>
        /// Port of w4b (W4v3) in the solver. Three plots share one canal. Water reaches a plot only when exactly one
        /// sluice is open, and a blank shabti woken and stilled there carries one plot's worth (dry to wet, wet to
        /// drowned). Sluices open with wen and shut by hand. A drowned plot with its sluice shut can be drained by hand.
        /// The named shabti on the far bank works on. Its name is read from the far bank, reached over a footbridge
        /// that is crossable only while held. "hetep ankhu" works only from the near bank, after reading. The tick
        /// after it, every open sluice drowns its plot (the surge).
        /// Goal: all plots wet, named still, bridge free, Ankhu near.
        /// </summary>
        private static SceneScript BuildShabtiB()
        {
            var L = new Lines("wernes_b");
            var room = new RoomDef
            {
                Id = "wernes_b",
                Title = "The Shabti With My Name",
                Verbs = new HashSet<string> { "open", "wake", "still", "bind", "release", "turn", "drain", "cross", "read" },
                Deeds = new HashSet<string> { "turn", "drain", "cross", "read" },
                Goal = new[] { C("plot1", "wet"), C("plot2", "wet"), C("plot3", "wet"), C("named", "still"), C("bridge", "free"), C("side", "near") },
            };
            var shabti = new WordGloss("shabti", "shabti", "A53");
            var sluiceG = new WordGloss("sluice", "sluice", "O31 N36");
            string[] ord = { "", "first", "second", "third" };
            for (int i = 1; i <= 3; i++)
            {
                string z = "plot" + i;
                room.Objects.Add(Obj("sluice" + i, ord[i] + " sluice", "shut", sluiceG, new[] { z },
                    "sluice", "gate", "sluice gate", "sluice " + i, ord[i] + " sluice", "sluice " + ord[i]));
                room.Objects.Add(Obj("shabti" + i, ord[i] + " shabti", "asleep", shabti, new[] { z },
                    "shabti", "worker", "servant", "shabti " + i, ord[i] + " shabti", "blank shabti"));
                room.Objects.Add(Obj("plot" + i, ord[i] + " plot", "dry", new WordGloss("field", "sekhet", "M20"), new[] { z },
                    "plot", "field", "furrow", "furrows", "plot " + i, ord[i] + " plot"));
            }
            var named = Obj("named", "shabti with your name", "working", new WordGloss("Ankhu", "ankhu", "S34 N35 Aa1 G43", true), null,
                "ankhu", "painted shabti", "named shabti", "far shabti", "fine shabti", "my shabti", "shabti ankhu", "ankhu shabti");
            named.Refuses["still painted shabti"] = "It does not answer to that.";
            named.Refuses["still named shabti"] = "It does not answer to that.";
            named.Refuses["still far shabti"] = "It does not answer to that.";
            named.Refuses["still fine shabti"] = "It does not answer to that.";
            named.Refuses["bind painted shabti"] = "It is held. It is not resting. It pulls against the thread and works on.";
            room.Objects.Add(named);
            room.Objects.Add(Obj("chest", "chest of the painted shabti", "unread", null, new[] { "farbank" }, "chest", "name", "cartouche", "inscription", "writing"));
            room.Objects.Add(Obj("bridge", "footbridge", "free", new WordGloss("bridge", "bridge", "N36 O1"), null, "bridge", "footbridge", "planks", "plank bridge"));
            room.Objects.Add(Obj("side", "bank", "near", null, null));
            room.Objects.Add(Obj("surge", "surge", "none", null, null));
            room.Objects.Add(Obj("split", "split", "no", null, null));

            room.Tick = s =>
            {
                int open = 0;
                for (int i = 1; i <= 3; i++) if (s["sluice" + i] == "open") open++;
                for (int i = 1; i <= 3; i++)
                    if (s["shabti" + i] == "poured")
                    {
                        s["plot" + i] = s["plot" + i] == "dry" ? "wet" : "drowned";
                        s["shabti" + i] = "asleep";
                    }
                if (s["surge"] == "pending")
                {
                    bool hit = false;
                    for (int i = 1; i <= 3; i++) if (s["sluice" + i] == "open") { s["plot" + i] = "drowned"; hit = true; }
                    s["surge"] = hit ? "hit" : "spent";
                }
                if (open < 2) s["split"] = "no";
            };

            for (int i = 1; i <= 3; i++)
            {
                string sl = "sluice" + i, sh = "shabti" + i, pl = "plot" + i;
                var others = new List<Cond>();
                for (int j = 1; j <= 3; j++) if (j != i) others.Add(C("sluice" + j, "shut"));
                room.Rules.Add(new SpellRule { Verb = "open", Target = sl, From = new[] { "shut" }, To = "open",
                    Success = "The gate lifts. Water runs toward the plot.", Event = "sluice_open" });
                room.Rules.Add(new SpellRule { Verb = "turn", Target = sl, From = new[] { "open" }, To = "shut",
                    Success = "You wheel the gate down by hand.", Refusal = "It is already down." });
                room.Rules.Add(new SpellRule { Verb = "wake", Target = sh, From = new[] { "asleep" }, To = "carrying",
                    Success = "It rises and takes up its jars.", Event = "blank_awake" });
                var wet = new List<Cond>(others) { C(sl, "open") };
                room.Rules.Add(new SpellRule { Verb = "still", Target = sh, From = new[] { "carrying" }, To = "poured", Requires = wet.ToArray(),
                    Success = "It pours its last jar into the furrow, and sits.", Event = "poured" });
                room.Rules.Add(new SpellRule { Verb = "still", Target = sh, From = new[] { "carrying" }, To = "asleep",
                    Success = "It sets its jars down. There was no water to carry.", Event = "poured_dry" });
                room.Rules.Add(new SpellRule { Verb = "bind", Target = sh, From = new[] { "never" }, To = "never",
                    Refusal = "It is held. It is not resting." });
                room.Rules.Add(new SpellRule { Verb = "drain", Target = pl, From = new[] { "drowned" }, To = "dry", Requires = new[] { C(sl, "shut") },
                    Success = "You break the low bank and let the flood out. The plot is dry again.",
                    Refusal = "Water is still coming in. Shut its gate first." });
            }
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "split", To = "yes", Requires = new[] { C("sluice1", "open"), C("sluice2", "open") }, Event = "split" });
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "split", To = "yes", Requires = new[] { C("sluice1", "open"), C("sluice3", "open") }, Event = "split" });
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "split", To = "yes", Requires = new[] { C("sluice2", "open"), C("sluice3", "open") }, Event = "split" });

            room.Rules.Add(new SpellRule { Verb = "bind", Target = "bridge", From = new[] { "free" }, To = "held",
                Success = "A cyan thread holds the footbridge still across the current.", Refusal = "It is already held.", Event = "bridge_held" });
            room.Rules.Add(new SpellRule { Verb = "release", Target = "bridge", From = new[] { "held" }, To = "free",
                Success = "The thread lets go. The bridge swings out with the current.", Refusal = "Nothing is holding it." });
            room.Rules.Add(new SpellRule { Verb = "cross", Target = "side", From = new[] { "near" }, To = "far", Requires = new[] { C("bridge", "held") },
                Event = "cross_far", Refusal = "The bridge swings out over the water and back." });
            room.Rules.Add(new SpellRule { Verb = "cross", Target = "side", From = new[] { "far" }, To = "near", Requires = new[] { C("bridge", "held") },
                Event = "cross_near", Refusal = "The bridge swings out over the water and back." });
            room.Rules.Add(new SpellRule { Verb = "read", Target = "chest", From = new[] { "unread" }, To = "read", Requires = new[] { C("side", "far") },
                Event = "name_read", Refusal = "From here it is only a glint." });
            room.Rules.Add(new SpellRule { Verb = "still", Target = "named", From = new[] { "working" }, To = "still",
                Requires = new[] { C("chest", "read"), C("side", "near") }, Also = new[] { ("surge", "pending") }, Event = "named_stilled",
                Refusal = "It does not hear you. It answers only to its name, called across the water." });
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "surge", To = "spent", Requires = new[] { C("surge", "hit") }, Event = "surge_drowned" });

            room.Hints.Add(new Hint { When = new[] { C("plot1", "dry", "drowned") }, Text = "One canal, three fields. Feed them one at a time." });
            room.Hints.Add(new Hint { When = new[] { C("plot2", "dry", "drowned") }, Text = "Open one gate. Wake the worker beside it, and let it rest when the jar is poured." });
            room.Hints.Add(new Hint { When = new[] { C("plot3", "dry", "drowned") }, Text = "Every gate but one must be down." });
            room.Hints.Add(new Hint { When = new[] { C("chest", "unread") }, Text = "That worker has a name. Read it from the far side." });
            room.Hints.Add(new Hint { When = new[] { C("named", "working") }, Text = "Call it by its name from the near bank, with every gate down." });
            room.Hints.Add(new Hint { When = new[] { C("bridge", "held") }, Text = "Let the bridge go." });

            var s = new SceneScript { SceneName = ShabtiB, Realm = "Wernes", Title = "The Shabti With My Name", Room = room,
                CodexOnEntry = new[] { "word.open", "word.wake", "word.still", "word.bind", "word.release" }, Anpu = AnpuPresence.Absent };
            for (int i = 1; i <= 3; i++)
            {
                string z = "plot" + i;
                s.Usables.Add(new Usable { Id = "shut" + i, Prompt = "Wheel the gate shut", Zone = z, Deed = "turn sluice " + i, When = new[] { C("sluice" + i, "open") } });
                s.Usables.Add(new Usable { Id = "drain" + i, Prompt = "Break the bank and drain the plot", Zone = z, Deed = "drain plot " + i,
                    When = new[] { C("plot" + i, "drowned"), C("sluice" + i, "shut") } });
            }
            s.Usables.Add(new Usable { Id = "cross_over", Prompt = "Cross the bridge", Zone = "bridge", Deed = "cross", When = new[] { C("side", "near"), C("bridge", "held") } });
            // Room guides 2026-11-26: the chest first. Both prompts share the far bank and the first that holds
            // wins, so with cross_back first the chest could never be read while the bridge was held.
            s.Usables.Add(new Usable { Id = "read_chest", Prompt = "Read the shabti's chest", Zone = "farbank", Deed = "read chest", When = new[] { C("chest", "unread") } });
            s.Usables.Add(new Usable { Id = "cross_back", Prompt = "Cross back", Zone = "farbank", Deed = "cross", When = new[] { C("side", "far"), C("bridge", "held") } });
            s.Usables.Add(new Usable { Id = "look_named", Prompt = "Look across at the painted shabti", Zone = "bridge", When = new[] { C("chest", "unread"), C("side", "near") } });
            s.Usables.Add(new Usable { Id = "read_vignette", Prompt = "Look at the painting", Zone = "vignette" });

            s.Beats.Add(new Beat("arrive", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Title("THE SHABTI WITH MY NAME", 4f),
                Step.Caption("The water in the runnel at your feet bends toward the far bank."),
                L.S(Speaker.Ankhu, "All the water goes to that one. It must be very thirsty. Or very busy."),
                Step.Free()));
            s.Beats.Add(new Beat("look", TriggerKind.Use, "look_named",
                L.S(Speaker.Ankhu, "Something is written on its chest. It glints. I cannot read it from here.")) { Once = false });
            s.Beats.Add(new Beat("vignette", TriggerKind.Use, "read_vignette",
                L.S(Speaker.Ankhu, "Four workers resting under a sycamore. A fifth works on. Its fields are dry.")) { Once = false });
            s.Beats.Add(new Beat("split", TriggerKind.Event, "split", Step.Caption("The water divides and reaches neither.")));
            s.Beats.Add(new Beat("dry_pour", TriggerKind.Event, "poured_dry", Step.Caption("No water reached its plot. One gate open, and only one.")));
            s.Beats.Add(new Beat("first_cross", TriggerKind.Event, "cross_far",
                L.S(Speaker.Ankhu, "The planks are wet. It has been throwing water over this bank for a long time.")));
            s.Beats.Add(new Beat("read", TriggerKind.Event, "name_read",
                Step.Freeze(),
                L.S(Speaker.Ankhu, "Painted on its chest, in a cartouche. Ankhu."),
                L.S(Speaker.Ankhu, "My name. It has been carrying water under my name since the day I was buried."),
                L.S(Speaker.Ankhu, "It will not hear me from here. In life you call a worker from the bank."),
                Step.Free()));
            s.Beats.Add(new Beat("named", TriggerKind.Event, "named_stilled",
                Step.Freeze(),
                Step.Caption("Across the water, the painted shabti slows. Its jar tips. It looks up at the sound of its name."),
                Step.Wait(0.8f),
                Step.Cue("flashback:memA_river_sunset"),
                Step.Write("memory.shoulders"),
                Step.Caption("The painted shabti sits down in its channel for the first time."),
                L.S(Speaker.Ankhu, "It has been working under my name. So have I."),
                Step.Free()));
            s.Beats.Add(new Beat("surge", TriggerKind.Event, "surge_drowned",
                Step.Caption("When the work stops, everything it held back comes at once.")));
            s.Beats.Add(new Beat("murmur", TriggerKind.Idle, "", L.S(Speaker.Ankhu, "One canal. Three plots. And one worker who never stops."))
                { Value = 45f });
            s.Beats.Add(new Beat("solved", TriggerKind.Solved, "",
                Step.Freeze(),
                Step.Caption("Three plots stand wet and green. Across the water, a worker rests."),
                Step.Write("lore.shabti_row"),
                Step.Wait(1.5f),
                Step.Next()));
            return s;
        }

        // ---------------------------------------------------------------- Room C: The House With One Door

        public const string HouseC = "Wernes_C_House";

        /// <summary>
        /// Port of w2 (W2v2) in the solver. The upper sluice is jammed open, so water reaches the front plot
        /// (seedlings) only through the house door, which the wind swings every tick while it is free. The lower
        /// sluice passes front water on to the back plot (the father's plot), and the ditch drains the back plot.
        /// Levels: 0 dry, 1 wet, 2 flooded, 3 drowned. The false door lights when water moves or a plot floods.
        /// Walking into it plays Memory B and teaches seal (khetem). The door seals only from inside and only while
        /// shut. Ankhu passes the doorway only while it stands open.
        /// Goal: both plots wet, door sealed, both sluices shut.
        /// </summary>
        private static SceneScript BuildHouseC()
        {
            var L = new Lines("wernes_c");
            var room = new RoomDef
            {
                Id = "wernes_c",
                Title = "The House With One Door",
                Verbs = new HashSet<string> { "open", "wake", "still", "bind", "release", "cross", "enter" },
                Deeds = new HashSet<string> { "cross", "enter" },
                Goal = new[] { C("front", "1"), C("back", "1"), C("mode", "sealed"), C("lower", "shut"), C("ditch", "shut") },
            };
            var sluiceG = new WordGloss("sluice", "sluice", "O31 N36");
            var field = new WordGloss("field", "sekhet", "M20");
            room.Objects.Add(Obj("door", "door", "shut", new WordGloss("door", "sba", "O31"), null,
                "door", "front door", "house door", "plank door", "doorway"));
            room.Objects.Add(Obj("mode", "door's binding", "free", null, null));
            room.Objects.Add(Obj("lower", "lower sluice", "shut", sluiceG, null, "lower", "lower sluice", "lower gate", "sluice", "gate"));
            room.Objects.Add(Obj("ditch", "ditch sluice", "shut", sluiceG, null, "ditch", "ditch sluice", "ditch gate", "drain"));
            room.Objects.Add(Obj("front", "seedling plot", "0", field, null, "front plot", "seedlings", "seedling plot", "front field"));
            room.Objects.Add(Obj("back", "father's plot", "0", field, null, "back plot", "sapling", "back field", "old plot", "father's plot"));
            room.Objects.Add(Obj("side", "where Ankhu stands", "in", null, null));
            room.Objects.Add(Obj("fdoor", "false door", "dark", new WordGloss("false door", "ka door", "O31 D28"), null, "false door"));
            room.Objects.Add(Obj("memory", "memory", "unseen", null, null));
            room.Objects.Add(Obj("held", "held note", "none", null, null));
            room.Objects.Add(Obj("drown", "drowned note", "none", null, null));

            // The world clock, exactly w2_tick, plus the false door's glow.
            room.Tick = st =>
            {
                if (st["mode"] == "free") st["door"] = st["door"] == "open" ? "shut" : "open";
                int f = int.Parse(st["front"]), b = int.Parse(st["back"]);
                if (st["door"] == "open") f = System.Math.Min(3, f + 1);
                if (st["lower"] == "open" && f > 0) { f--; b = System.Math.Min(3, b + 1); }
                if (st["ditch"] == "open") b = System.Math.Max(0, b - 1);
                st["front"] = f.ToString();
                st["back"] = b.ToString();
                if (f >= 2 || b >= 2) st["fdoor"] = "warm";
                else if (st["fdoor"] != "dark" || st["lower"] == "open" || st["ditch"] == "open") st["fdoor"] = "lit";
            };

            room.Rules.Add(new SpellRule { Verb = "bind", Target = "door", From = new[] { "open", "shut" }, Requires = new[] { C("mode", "free") },
                Also = new[] { ("mode", "held") }, Success = "A cyan thread catches the door mid-swing and holds it.",
                Refusal = "It is already held, or sealed.", Event = "door_held" });
            room.Rules.Add(new SpellRule { Verb = "release", Target = "door", From = new[] { "open", "shut" }, Requires = new[] { C("mode", "held") },
                Also = new[] { ("mode", "free") }, Success = "The thread lets go. The wind takes the door again.", Refusal = "Nothing is holding it." });
            room.Rules.Add(new SpellRule { Verb = "seal", Target = "door", From = new[] { "shut" },
                Requires = new[] { C("mode", "free", "held"), C("side", "in"), C("memory", "seen") },
                Also = new[] { ("mode", "sealed") }, Event = "door_sealed",
                Success = "Khetem. The bolt slides home from the inside. The wind leans on the door and the door holds.",
                Refusal = "It must be shut to be sealed, and sealed from inside." });
            room.Rules.Add(new SpellRule { Verb = "open", Target = "door", From = new[] { "shut" }, To = "open",
                Requires = new[] { C("mode", "sealed"), C("side", "in") }, Also = new[] { ("mode", "free") },
                Success = "The bolt draws back. The wind has the door again.", Refusal = "It opens well enough on its own." });

            foreach (string sl in new[] { "lower", "ditch" })
            {
                room.Rules.Add(new SpellRule { Verb = "open", Target = sl, From = new[] { "shut" }, To = "open", Requires = new[] { C("side", "out") },
                    Success = sl == "lower" ? "The lower gate lifts. Water slides from the seedlings toward the old plot." : "The ditch gate lifts. The old plot begins to drain.",
                    Refusal = "The sluices are out in the yard.", Event = "sluice_open" });
                room.Rules.Add(new SpellRule { Verb = "seal", Target = sl, From = new[] { "open" }, To = "shut", Requires = new[] { C("side", "out"), C("memory", "seen") },
                    Success = "Khetem. The gate drops and stays down.", Refusal = "The sluices are out in the yard.", Event = "sluice_sealed" });
            }

            room.Rules.Add(new SpellRule { Verb = "cross", Target = "side", From = new[] { "in" }, To = "out", Requires = new[] { C("door", "open") },
                Event = "go_out", Refusal = "The door is shut. Wait for the wind." });
            room.Rules.Add(new SpellRule { Verb = "cross", Target = "side", From = new[] { "out" }, To = "in", Requires = new[] { C("door", "open") },
                Event = "go_in", Refusal = "The door is shut. Wait for the wind." });
            room.Rules.Add(new SpellRule { Verb = "enter", Target = "memory", From = new[] { "unseen" }, To = "seen",
                Requires = new[] { C("fdoor", "lit", "warm"), C("side", "in") }, Event = "memory_b",
                Refusal = "Painted stone. It does not open for you yet." });

            // What the room says when it is nearly right.
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "held", To = "held", Event = "held_not_shut",
                Requires = new[] { C("front", "1"), C("back", "1"), C("lower", "shut"), C("ditch", "shut"), C("mode", "held"), C("door", "shut") } });
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "held", To = "none", Requires = new[] { C("mode", "free", "sealed") } });
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "drown", To = "front", Requires = new[] { C("front", "3") }, Event = "front_drowned" });
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "drown", To = "none", Requires = new[] { C("front", "0", "1", "2") } });

            room.Hints.Add(new Hint { When = new[] { C("memory", "unseen"), C("fdoor", "dark") }, Text = "The seedlings drink only through the door. Watch where the water goes." });
            room.Hints.Add(new Hint { When = new[] { C("memory", "unseen") }, Text = "You need a word that shuts and stays shut. The false door is lit. Walk through it." });
            room.Hints.Add(new Hint { When = new[] { C("front", "2", "3") }, Text = "Too much comes through the door. The lower gate passes it on." });
            room.Hints.Add(new Hint { When = new[] { C("back", "2", "3") }, Text = "The old plot is flooded. The ditch drains it." });
            room.Hints.Add(new Hint { When = new[] { C("mode", "held") }, Text = "Held is not shut. Seal it, from inside, while it is shut." });
            room.Hints.Add(new Hint { When = new[] { C("lower", "open") }, Text = "When the plots are right, shut the gates for good." });
            room.Hints.Add(new Hint { When = new[] { C("ditch", "open") }, Text = "When the plots are right, shut the gates for good." });

            var s = new SceneScript { SceneName = HouseC, Realm = "Wernes", Title = "The House With One Door", Room = room,
                CodexOnEntry = new[] { "word.open", "word.wake", "word.still", "word.bind", "word.release", "lore.house" }, Anpu = AnpuPresence.Absent };
            s.Usables.Add(new Usable { Id = "go_out", Prompt = "Step out through the door", Zone = "threshold", Deed = "cross", When = new[] { C("side", "in"), C("door", "open") } });
            s.Usables.Add(new Usable { Id = "go_in", Prompt = "Step in through the door", Zone = "yard", Deed = "cross", When = new[] { C("side", "out"), C("door", "open") } });
            s.Usables.Add(new Usable { Id = "enter_false", Prompt = "Walk into the false door", Zone = "falsedoor", Deed = "enter memory",
                When = new[] { C("memory", "unseen"), C("fdoor", "lit", "warm") } });
            s.Usables.Add(new Usable { Id = "look_false", Prompt = "Look at the false door", Zone = "falsedoor", When = new[] { C("fdoor", "dark") } });
            s.Usables.Add(new Usable { Id = "read_vignette", Prompt = "Look at the painting over the door", Zone = "falsedoor", When = new[] { C("fdoor", "lit", "warm") } });
            s.Usables.Add(new Usable { Id = "look_sapling", Prompt = "Look at the sapling", Zone = "back" });

            s.Beats.Add(new Beat("arrive", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Title("THE HOUSE WITH ONE DOOR", 4f),
                Step.Caption("A plank door bangs open in the wind, and bangs shut, and bangs open."),
                L.S(Speaker.Ankhu, "One room. One door, and the wind has it."),
                Step.Free()));
            s.Beats.Add(new Beat("look_false", TriggerKind.Use, "look_false",
                L.S(Speaker.Ankhu, "A false door. For the dead to come and go. On the lintel: wide-open or tight-shut and asleep, open wide for the soul therein.")) { Once = false });
            s.Beats.Add(new Beat("vignette", TriggerKind.Use, "read_vignette",
                L.S(Speaker.Ankhu, "A boy at a door. Above him the same door, shut, over a field of green shoots.")) { Once = false });
            s.Beats.Add(new Beat("sapling", TriggerKind.Use, "look_sapling",
                L.S(Speaker.Ankhu, "One sapling, dry as a stick. Somebody planted it here and went away.")) { Once = false });
            s.Beats.Add(new Beat("first_out", TriggerKind.Event, "go_out",
                L.S(Speaker.Ankhu, "The upper gate is jammed open. Everything here opens. Nothing shuts. Except that door.")));
            s.Beats.Add(new Beat("lit", TriggerKind.Event, "sluice_open",
                Step.Caption("Behind you, inside the house, the false door warms.")));
            s.Beats.Add(new Beat("memory", TriggerKind.Event, "memory_b",
                Step.Freeze(),
                Step.Caption("The painted door is not stone. It is a door, at a child's height."),
                Step.Cue("flashback:memB_door_dawn"),
                L.S(Speaker.Ankhu, "He said he would bring me something from the north. The bolt was on the outside."),
                L.S(Speaker.Ankhu, "Khetem. Seal. I learned it from the wrong side of a door."),
                Step.Learn("seal"),
                Step.Write("memory.door_dawn"),
                Step.Free()));
            s.Beats.Add(new Beat("held", TriggerKind.Event, "held_not_shut",
                Step.Caption("The plots are right. The door is only held. Held is not shut.")) { Once = false });
            s.Beats.Add(new Beat("drowned", TriggerKind.Event, "front_drowned",
                Step.Caption("The seedlings are under water. Water still comes through the door.")) { Once = false });
            s.Beats.Add(new Beat("murmur", TriggerKind.Idle, "", L.S(Speaker.Ankhu, "Everything here opens. Nothing shuts. Except that door."))
                { Value = 45f });
            s.Beats.Add(new Beat("solved", TriggerKind.Solved, "",
                Step.Freeze(),
                Step.Caption("The seedlings stand. In the old plot the sapling puts out one leaf."),
                L.S(Speaker.Ankhu, "A door that shuts can keep something alive."),
                Step.Cue("flashback:sting_khetem_seedlings"),
                Step.Write("lore.one_door"),
                Step.Wait(1.5f),
                Step.Next()));
            return s;
        }

        // ---------------------------------------------------------------- Room D: The Birth Bricks

        public const string BricksD = "Wernes_D_Bricks";

        /// <summary>The four sound-signs of Ankhu's cartouche, in reading order (S34 N35 Aa1 G43).</summary>
        public static readonly string[] NameSigns = { "ankh", "water", "placenta", "quail" };

        /// <summary>
        /// Zac 2026-11-24, pick 4. Four painted birth bricks of Meskhenet, scraped bare. The four sound-signs of
        /// the cartouche lie scattered, each won back by one word: wake the sleeping goddess (the ankh in her fist),
        /// flow the dry basin (the water ripple), open the midwife's basket (the round kh sign), release the quail
        /// chick from its snare. One sign in the hands at a time. Laid in reading order the bricks wake and the
        /// mother's cry sounds, and while it sounds the seated man (A1) appears on the birthing mat. The cry thins
        /// on the next tick and is gone on the one after, taking the sign with it, so only echo (wesheb), which
        /// holds the cry, leaves time to take the man and set him at the end of the name.
        /// Goal: the seated man sits after the cartouche.
        /// </summary>
        private static SceneScript BuildBricksD()
        {
            var L = new Lines("wernes_d");
            var room = new RoomDef
            {
                Id = "wernes_d",
                Title = "The Birth Bricks",
                Verbs = new HashSet<string> { "open", "wake", "still", "bind", "release", "seal", "flow", "echo", "take", "place", "lift" },
                Deeds = new HashSet<string> { "take", "place", "lift" },
                Goal = new[] { C("seat", "man") },
            };
            string[] ord = { "", "first", "second", "third", "fourth" };
            var brickG = new WordGloss("birth brick", "meskhenet", "F31 S29 Aa1 N35 X1");
            for (int i = 1; i <= 4; i++)
            {
                room.Objects.Add(Obj("brick" + i, ord[i] + " birth brick", "bare", brickG, new[] { "brick" + i },
                    "brick", "birth brick", "brick " + i, ord[i] + " brick", "brick " + ord[i], ord[i] + " birth brick"));
            }
            room.Objects.Add(Obj("bricks", "the bricks", "dull", brickG, null, "bricks", "birth bricks", "all the bricks", "the bricks"));
            room.Objects.Add(Obj("goddess", "Meskhenet", "asleep", new WordGloss("Meskhenet", "meskhenet", "F31 S29 Aa1 N35 X1 B1"), null,
                "meskhenet", "goddess", "painted goddess", "woman", "painted woman", "her"));
            room.Objects.Add(Obj("basin", "basin", "dry", new WordGloss("basin", "she", "N37"), null, "basin", "dry basin", "bowl", "water basin"));
            room.Objects.Add(Obj("basket", "midwife's basket", "shut", new WordGloss("basket", "neb", "V30"), null, "basket", "midwife's basket", "midwife basket", "lid"));
            room.Objects.Add(Obj("ankh", "ankh", "hidden", new WordGloss("ankh", "ankh", "S34"), null, "ankh", "ankh sign", "life", "the ankh"));
            room.Objects.Add(Obj("water", "water sign", "hidden", new WordGloss("water", "n", "N35"), null, "water sign", "ripple", "water ripple", "wavy line"));
            room.Objects.Add(Obj("placenta", "round sign", "hidden", new WordGloss("placenta", "kh", "Aa1"), null, "round sign", "placenta", "sieve", "kh", "round"));
            room.Objects.Add(Obj("quail", "quail chick", "snared", new WordGloss("quail chick", "w", "G43"), null,
                "quail", "chick", "quail chick", "bird", "snare", "reed snare"));
            room.Objects.Add(Obj("man", "seated man", "unseen", new WordGloss("seated man", "s", "A1"), null, "man", "seated man", "little man", "sign on the mat"));
            room.Objects.Add(Obj("seat", "end of the cartouche", "bare", null, new[] { "seat" }, "cartouche", "end of the name", "end", "seat"));
            room.Objects.Add(Obj("cry", "cry", "silent", new WordGloss("cry", "sbH", "S29 D58 V28 A2"), null, "cry", "voice", "her voice", "shout", "wail", "sound"));
            room.Objects.Add(Obj("hand", "your hands", "none", null, null));
            room.Objects.Add(Obj("misread", "misread", "no", null, null));
            room.Objects.Add(Obj("fade", "fade", "none", null, null));

            // The world clock: the cry thins, then is gone, and takes the sign on the mat with it. Echo stops it.
            room.Tick = st =>
            {
                if (st["cry"] == "sounding") st["cry"] = "fading";
                else if (st["cry"] == "fading")
                {
                    st["cry"] = "silent";
                    st["bricks"] = "spent";
                    if (st["man"] == "mat" || st["man"] == "hand") st["man"] = "unseen";
                    if (st["hand"] == "man") st["hand"] = "none";
                }
            };

            // Winning the four signs back.
            room.Rules.Add(new SpellRule { Verb = "wake", Target = "goddess", From = new[] { "asleep" }, To = "awake", Also = new[] { ("ankh", "loose") },
                Success = "Res. The painted goddess opens her eyes, and opens her hand. An ankh drops to the floor and rings.", Event = "goddess_wakes",
                Refusal = "She is awake. She is watching the bricks." });
            room.Rules.Add(new SpellRule { Verb = "flow", Target = "basin", From = new[] { "dry" }, To = "full", Also = new[] { ("water", "loose") },
                Success = "Bes. Water wells up in the cracked basin, and its ripple stands up out of it as a sign.", Event = "basin_flows",
                Refusal = "It is full." });
            room.Rules.Add(new SpellRule { Verb = "open", Target = "basket", From = new[] { "shut" }, To = "open", Also = new[] { ("placenta", "loose") },
                Success = "Wen. The knot gives. Inside, wrapped like something precious, a round sign.", Event = "basket_open",
                Refusal = "It is open." });
            room.Rules.Add(new SpellRule { Verb = "release", Target = "quail", From = new[] { "snared" }, To = "loose",
                Success = "Wekha. The reed snare falls apart. The quail chick shakes itself and hops into the light.", Event = "quail_free",
                Refusal = "It is free already." });
            foreach (string sg in NameSigns)
            {
                room.Rules.Add(new SpellRule { Verb = "take", Target = sg, From = new[] { "loose" }, To = "hand", Requires = new[] { C("hand", "none") },
                    Also = new[] { ("hand", sg) }, Event = "sign_taken",
                    Refusal = "Your hands are full, or it is not yours to lift yet. One sign at a time." });
            }

            // Laying the name. Any sign on any bare brick; only the reading order wakes them.
            for (int i = 1; i <= 4; i++)
            {
                string b = "brick" + i;
                foreach (string sg in NameSigns)
                {
                    room.Rules.Add(new SpellRule { Verb = "place", Target = b, From = new[] { "bare" }, To = sg, Requires = new[] { C("hand", sg) },
                        Also = new[] { (sg, "laid"), ("hand", "none") }, Event = "sign_laid",
                        Success = "The sign settles into the clay as if it had always been there.",
                        Refusal = "Bring a sign to a bare brick." });
                    foreach (string other in NameSigns)
                    {
                        if (other == sg) continue;
                        // Swap: the held sign goes down and the one on the brick comes up, so a full row can be reordered.
                        room.Rules.Add(new SpellRule { Verb = "place", Target = b, From = new[] { other }, To = sg,
                            Requires = new[] { C("hand", sg), C("bricks", "dull", "spent") },
                            Also = new[] { (sg, "laid"), (other, "hand"), ("hand", other), ("bricks", "dull") }, Event = "sign_swapped",
                            Success = "You set your sign down and take up the one that was there.",
                            Refusal = "The bricks are awake. Leave them." });
                    }
                    room.Rules.Add(new SpellRule { Verb = "lift", Target = b, From = new[] { sg }, To = "bare",
                        Requires = new[] { C("hand", "none"), C("bricks", "dull", "spent") },
                        Also = new[] { (sg, "hand"), ("hand", sg), ("bricks", "dull") }, Event = "sign_lifted",
                        Success = "You prise the sign up again.",
                        Refusal = "Your hands are full, or the brick is bare, or it is awake." });
                }
            }

            string[] laid = { "ankh", "water", "placenta", "quail" };
            Cond[] inOrder = { C("brick1", "ankh"), C("brick2", "water"), C("brick3", "placenta"), C("brick4", "quail") };
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "bricks", To = "awake", Event = "bricks_wake",
                Requires = new[] { C("bricks", "dull"), inOrder[0], inOrder[1], inOrder[2], inOrder[3] },
                Also = new[] { ("cry", "sounding"), ("man", "mat") } });
            room.Rules.Add(new SpellRule { Verb = "wake", Target = "bricks", From = new[] { "spent" }, To = "awake", Requires = inOrder,
                Also = new[] { ("cry", "sounding"), ("man", "mat") }, Event = "bricks_rewake",
                Success = "Res. The bricks wake again, and the cry comes again.",
                Refusal = "The bricks wake only for a name, read in its order." });
            for (int i = 1; i <= 4; i++)
            {
                var wrong = new List<string>(laid);
                wrong.Remove(laid[i - 1]);
                var req = new List<Cond>();
                for (int j = 1; j <= 4; j++) req.Add(j == i ? C("brick" + j, wrong.ToArray()) : C("brick" + j, laid));
                room.Rules.Add(new SpellRule { Verb = "auto", Target = "misread", To = "yes", Requires = req.ToArray(), Event = "misread" });
                room.Rules.Add(new SpellRule { Verb = "auto", Target = "misread", To = "no", Requires = new[] { C("brick" + i, "bare") } });
            }
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "fade", To = "yes", Requires = new[] { C("bricks", "spent") }, Event = "cry_faded" });
            room.Rules.Add(new SpellRule { Verb = "auto", Target = "fade", To = "none", Requires = new[] { C("bricks", "dull", "awake") } });

            // Holding the cry, and the sign it brings.
            room.Rules.Add(new SpellRule { Verb = "echo", Target = "cry", From = new[] { "sounding", "fading" }, To = "held", Event = "cry_held",
                Success = "Wesheb. You answer the cry and it answers back. It hangs in the room and does not thin.",
                Refusal = "There is nothing to answer. The room is quiet." });
            room.Rules.Add(new SpellRule { Verb = "take", Target = "man", From = new[] { "mat" }, To = "hand", Requires = new[] { C("hand", "none") },
                Also = new[] { ("hand", "man") }, Event = "man_taken",
                Success = "A small seated man, painted on the mat where a child would lie. He comes up warm.",
                Refusal = "There is nothing on the mat." });
            room.Rules.Add(new SpellRule { Verb = "place", Target = "seat", From = new[] { "bare" }, To = "man", Requires = new[] { C("hand", "man") },
                Also = new[] { ("man", "laid"), ("hand", "none"), ("cry", "answered") }, Event = "named",
                Success = "The seated man sits down at the end of the name.",
                Refusal = "Only one sign sits after a name, outside the cartouche. It is none of these four." });

            room.Hints.Add(new Hint { When = new[] { C("goddess", "asleep") }, Text = "Meskhenet sleeps with something in her fist." });
            room.Hints.Add(new Hint { When = new[] { C("basin", "dry") }, Text = "A wavy line is scratched on the dry basin. Water would bring it up." });
            room.Hints.Add(new Hint { When = new[] { C("basket", "shut") }, Text = "The midwife's basket is tied shut." });
            room.Hints.Add(new Hint { When = new[] { C("quail", "snared") }, Text = "The quail chick is caught. Let it go." });
            room.Hints.Add(new Hint { When = new[] { C("misread", "yes") }, Text = "Your name, as it is written at the First Gate. The ankh first, because it is the strongest sign." });
            room.Hints.Add(new Hint { When = new[] { C("bricks", "spent") }, Text = "The cry is gone before you reach the mat. Make it stay. Then wake the bricks again." });
            room.Hints.Add(new Hint { When = new[] { C("cry", "sounding", "fading") }, Text = "Answer the cry, so it stays." });
            room.Hints.Add(new Hint { When = new[] { C("man", "mat"), C("cry", "held") }, Text = "Take the seated man from the mat." });
            room.Hints.Add(new Hint { When = new[] { C("hand", "man") }, Text = "He sits at the end of the name, after the cartouche." });

            var s = new SceneScript { SceneName = BricksD, Realm = "Wernes", Title = "The Birth Bricks", Room = room,
                CodexOnEntry = new[] { "word.open", "word.wake", "word.release", "word.flow", "word.echo", "name.ankhu" }, Anpu = AnpuPresence.Absent }; // lore.birth_bricks is the solve reward (room guides 2026-11-26)
            s.Usables.Add(new Usable { Id = "look_goddess", Prompt = "Look at the painted goddess", Zone = "goddess", When = new[] { C("goddess", "asleep") } });
            s.Usables.Add(new Usable { Id = "look_basin", Prompt = "Look at the basin", Zone = "basin", When = new[] { C("basin", "dry") } });
            s.Usables.Add(new Usable { Id = "look_basket", Prompt = "Look at the basket", Zone = "basket", When = new[] { C("basket", "shut") } });
            s.Usables.Add(new Usable { Id = "look_snare", Prompt = "Look at the quail chick", Zone = "snare", When = new[] { C("quail", "snared") } });
            s.Usables.Add(new Usable { Id = "take_ankh", Prompt = "Pick up the ankh", Zone = "goddess", Deed = "take ankh", When = new[] { C("ankh", "loose"), C("hand", "none") } });
            s.Usables.Add(new Usable { Id = "take_water", Prompt = "Lift the ripple from the water", Zone = "basin", Deed = "take water sign", When = new[] { C("water", "loose"), C("hand", "none") } });
            s.Usables.Add(new Usable { Id = "take_placenta", Prompt = "Take the round sign", Zone = "basket", Deed = "take round sign", When = new[] { C("placenta", "loose"), C("hand", "none") } });
            s.Usables.Add(new Usable { Id = "take_quail", Prompt = "Pick up the quail chick", Zone = "snare", Deed = "take quail", When = new[] { C("quail", "loose"), C("hand", "none") } });
            for (int i = 1; i <= 4; i++)
            {
                string b = "brick" + i;
                s.Usables.Add(new Usable { Id = "place" + i, Prompt = "Lay the sign on this brick", Zone = b, Deed = "place brick " + i,
                    When = new[] { C(b, "bare"), C("hand", NameSigns) } });
                s.Usables.Add(new Usable { Id = "swap" + i, Prompt = "Swap your sign for this one", Zone = b, Deed = "place brick " + i,
                    When = new[] { C(b, NameSigns), C("hand", NameSigns), C("bricks", "dull", "spent") } });
                s.Usables.Add(new Usable { Id = "lift" + i, Prompt = "Lift the sign from this brick", Zone = b, Deed = "lift brick " + i,
                    When = new[] { C(b, NameSigns), C("hand", "none"), C("bricks", "dull", "spent") } });
            }
            s.Usables.Add(new Usable { Id = "take_man", Prompt = "Take the seated man from the mat", Zone = "mat", Deed = "take man", When = new[] { C("man", "mat"), C("hand", "none") } });
            s.Usables.Add(new Usable { Id = "place_man", Prompt = "Set him at the end of the name", Zone = "seat", Deed = "place seat", When = new[] { C("hand", "man") } });
            s.Usables.Add(new Usable { Id = "look_cartouche", Prompt = "Look at the empty cartouche", Zone = "seat", When = new[] { C("seat", "bare"), C("hand", "none") } });

            s.Beats.Add(new Beat("arrive", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Title("THE BIRTH BRICKS", 4f),
                Step.Caption("Four bricks lie in a row on a swept floor. A woman's face is painted on the end of each."),
                L.S(Speaker.Ankhu, "Birth bricks. A mother crouches on these to bring a child into the world."),
                L.S(Speaker.Ankhu, "Somebody has scraped them bare. Four empty places, and over them a cartouche with nothing in it."),
                Step.Free()));
            s.Beats.Add(new Beat("look_goddess", TriggerKind.Use, "look_goddess",
                L.S(Speaker.Ankhu, "Meskhenet, asleep on the wall. She stands at every birth and writes what the child will be. Her fist is closed on something.")) { Once = false });
            s.Beats.Add(new Beat("look_basin", TriggerKind.Use, "look_basin",
                L.S(Speaker.Ankhu, "The midwife's basin, cracked dry. A wavy line is scratched in the rim.")) { Once = false });
            s.Beats.Add(new Beat("look_basket", TriggerKind.Use, "look_basket",
                L.S(Speaker.Ankhu, "A covered basket, tied shut with a birth knot. Whatever is inside was kept with care.")) { Once = false });
            s.Beats.Add(new Beat("look_snare", TriggerKind.Use, "look_snare",
                L.S(Speaker.Ankhu, "A quail chick in a reed snare. It is not afraid. It is waiting for somebody.")) { Once = false });
            s.Beats.Add(new Beat("look_cartouche", TriggerKind.Use, "look_cartouche",
                L.S(Speaker.Ankhu, "An empty cartouche, and a little space after it. A name ends with a sign that says what kind of thing it names.")) { Once = false });
            s.Beats.Add(new Beat("first_sign", TriggerKind.Event, "sign_taken",
                L.S(Speaker.Ankhu, "It is one of mine. One of the signs of my name.")));
            s.Beats.Add(new Beat("misread", TriggerKind.Event, "misread",
                Step.Caption("The bricks stay dull. The signs say something, but not a name."),
                L.S(Speaker.Ankhu, "The ankh first, because it is the strongest sign. Mother taught me that.")) { Once = false });
            s.Beats.Add(new Beat("wake", TriggerKind.Event, "bricks_wake",
                Step.Freeze(),
                Step.Caption("The four bricks wake. The painted women on their ends open their mouths."),
                L.S(Speaker.Mother, "He lives!"),
                Step.Caption("The cry is already thinning. On the birthing mat a small sign appears: a seated man."),
                L.S(Speaker.Ankhu, "That was her voice."),
                Step.Free()));
            s.Beats.Add(new Beat("rewake", TriggerKind.Event, "bricks_rewake",
                L.S(Speaker.Mother, "He lives!")) { Once = false });
            s.Beats.Add(new Beat("faded", TriggerKind.Event, "cry_faded",
                Step.Caption("The cry thins to nothing. The sign on the mat goes with it."),
                L.S(Speaker.Ankhu, "Gone before I could reach it. It has to stay.")) { Once = false });
            s.Beats.Add(new Beat("held", TriggerKind.Event, "cry_held",
                L.S(Speaker.Ankhu, "Wesheb. I am holding her voice in the room."),
                L.S(Speaker.Mother, "He lives! He lives!")));
            s.Beats.Add(new Beat("man", TriggerKind.Event, "man_taken",
                L.S(Speaker.Ankhu, "The man on the mat is him.")));
            s.Beats.Add(new Beat("murmur", TriggerKind.Idle, "", L.S(Speaker.Ankhu, "Four bricks, four bare places. A name is written in an order."))
                { Value = 45f });
            s.Beats.Add(new Beat("solved", TriggerKind.Solved, "",
                Step.Freeze(),
                Step.Caption("The seated man sits at the end of the cartouche. The name is whole, and the bricks are warm."),
                L.M("mem_naming", Speaker.Narrator, "A one-room house, before anything he can remember. A midwife, and a woman worn out and laughing with a newborn in her arms."),
                L.M("mem_naming", Speaker.Other, "What is he called?"),
                L.M("mem_naming", Speaker.Mother, "Ankhu. He lives. Say it so the gods hear."),
                L.S(Speaker.Ankhu, "She said it loud enough for the gods. I have said it quietly ever since."),
                Step.Write("memory.naming"),
                Step.Write("lore.birth_bricks"),
                Step.Wait(1.5f),
                Step.Next()));
            return s;
        }
    }
}
