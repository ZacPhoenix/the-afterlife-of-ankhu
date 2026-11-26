using System.Collections.Generic;

namespace Ankhu.Realms.Core.Content
{
    /// <summary>
    /// The way into Wernes and the house at its centre, per docs/PLAY_ORDER.md and the Wernes design
    /// (design/next_area/next_area_design_v1.md, story bible). The terrace is the reunion with Anpu after
    /// Borrowed Dawn, where Ankhu says his son's name for the first time. The Memory House is the hub: Anpu
    /// waits there, a false door leads into each memory room in turn, and the talk moves on each time Ankhu
    /// comes back (flags "progress.*" from Ankhu.Opening.WernesProgress, set by the director at Start).
    /// </summary>
    public static partial class WernesPaintedScripts
    {
        public const string Terrace = "Wernes_Terrace";
        public const string Hub = "Wernes_Hub";

        // ---------------------------------------------------------------- The terrace reunion

        /// <summary>
        /// Anpu waits at the terrace edge. Walking to him starts the talk and he asks for the name Ankhu said in
        /// the doorway during Borrowed Dawn. Speaking "Hori" solves the room. Then Anpu leads him down the steps.
        /// </summary>
        private static SceneScript BuildTerrace()
        {
            var L = new Lines("wernes_terrace");
            var room = new RoomDef
            {
                Id = "wernes_terrace",
                Title = "The Terrace Above Wernes",
                Verbs = new HashSet<string> { "name" },
                Goal = new[] { C("son", "named") },
            };
            room.Objects.Add(Obj("son", "son", "unnamed", null, null));
            room.Objects.Add(Obj("asked", "asked", "no", null, null));
            room.Objects.Add(Obj("ankhu", "Ankhu", "terrace", null, null));

            room.Rules.Add(new SpellRule
            {
                Verb = "name", Target = "son", NameValue = "hori", From = new[] { "unnamed" }, To = "named",
                Requires = new[] { C("asked", "yes") }, Event = "hori", Refusal = "Not yet. Go to Anpu first.",
                Success = "The name goes out over the fields and something down there turns toward it.",
            });

            room.Hints.Add(new Hint { When = new[] { C("asked", "no") }, Text = "Anpu is waiting at the edge of the terrace." });
            room.Hints.Add(new Hint { When = new[] { C("asked", "yes") }, Text = "Anpu asked for the name you said in the doorway. Speak it." });

            var s = new SceneScript { SceneName = Terrace, Realm = "Wernes", Title = "The Terrace Above Wernes", Room = room, Anpu = AnpuPresence.Waits }; // at the terrace edge

            s.Beats.Add(new Beat("arrive", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Title("THE TERRACE ABOVE WERNES", 4f),
                Step.Caption("Steps cut into the cliff go down toward green fields. Anpu is waiting at the edge."),
                Step.Free()));

            s.Beats.Add(new Beat("reunion", TriggerKind.EnterZone, "edge",
                Step.Freeze(),
                Step.Set("asked", "yes"),
                L.S(Speaker.Ankhu, "You said you would be here."),
                L.S(Speaker.Anpu, "I am always where I say."),
                L.S(Speaker.Ankhu, "In there, when the light came through, I was somewhere else. A doorway. A child in my hands. I said a name."),
                L.S(Speaker.Anpu, "Say it again."),
                Step.Free()));

            s.Beats.Add(new Beat("murmur", TriggerKind.Idle, "", L.S(Speaker.Ankhu, "A small name. I used to say it a hundred times a day."))
                { Value = 40f });

            s.Beats.Add(new Beat("solved", TriggerKind.Solved, "",
                Step.Freeze(),
                L.S(Speaker.Anpu, "You have a son."),
                L.S(Speaker.Ankhu, "Had. I had a son. I am dead, Anpu. Everything I have is in the past tense."),
                L.S(Speaker.Anpu, "Down there are fields that were given to you. Things were planted in them while you were alive. You never came to see what grew."),
                L.S(Speaker.Ankhu, "I never had a field. I had a table in the house of embalming."),
                L.S(Speaker.Anpu, "Then the field has waited a long time. Come."),
                Step.Write("name.hori"),
                Step.Move("Anpu", "mark_steps"),
                Step.Wait(1.2f),
                Step.Caption("Grey figures sit on the steps below. They do not look up as the two of them pass."),
                L.S(Speaker.Ankhu, "Who are they?"),
                L.S(Speaker.Anpu, "They forgot."),
                Step.Wait(1.0f),
                Step.Next()));
            return s;
        }

        // ---------------------------------------------------------------- The Memory House (hub)

        /// <summary>
        /// Ankhu's house in Wernes, built for him and never lived in. A false door (BD 92) opens when its owner
        /// says his own name, and it leads to the next memory room he has not finished. Four panels around the
        /// door light as rooms are done. Anpu waits by the door and says something new each time Ankhu returns.
        /// After all four, the door opens one last time and the chapter ends.
        /// </summary>
        private static SceneScript BuildHub()
        {
            var L = new Lines("wernes_hub");
            var room = new RoomDef
            {
                Id = "wernes_hub",
                Title = "The Memory House",
                Verbs = new HashSet<string> { "name" },
                Goal = new[] { C("fdoor", "open") },
            };
            room.Objects.Add(Obj("fdoor", "false door", "shut", new WordGloss("false door", "ka door", "O31 D28"), new[] { "door" },
                "false door", "door", "ka door"));
            foreach (string l in new[] { "a", "b", "c", "d" })
                room.Objects.Add(Obj("panel_" + l, "panel " + l.ToUpperInvariant(), "dark", null, null));
            room.Objects.Add(Obj("false_door_panels", "lit panels", "0", null, null));
            room.Objects.Add(Obj("ankhu", "Ankhu", "house", null, null));

            room.Rules.Add(new SpellRule
            {
                Verb = "name", Target = "fdoor", NameValue = "ankhu", From = new[] { "shut" }, To = "open", Event = "door_opens",
                Success = "The painted door gives way like water. Beyond it, light on a field.",
                Refusal = "It is already open.",
            });

            room.Hints.Add(new Hint { When = new[] { C("fdoor", "shut") }, Text = "Read the false door (E). It was made for one person only." });

            var s = new SceneScript { SceneName = Hub, Realm = "Wernes", Title = "The Memory House", Room = room, Anpu = AnpuPresence.Waits }; // by the false door
            s.Usables.Add(new Usable { Id = "read_door", Prompt = "Read the false door", Zone = "door" });

            // Panels and the count, from the rooms already finished. These run first, before any talk.
            foreach (string l in new[] { "A", "B", "C", "D" })
                s.Beats.Add(new Beat("panel_" + l, TriggerKind.Start, "", Step.Set("panel_" + l.ToLowerInvariant(), "lit"))
                    { RequiresFlags = new[] { "progress.done." + l } });
            for (int n = 1; n <= 4; n++)
                s.Beats.Add(new Beat("count_" + n, TriggerKind.Start, "", Step.Set("false_door_panels", n.ToString()))
                    { RequiresFlags = new[] { "progress.count." + n } });

            // First visit, straight from the terrace.
            s.Beats.Add(new Beat("first", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Title("THE MEMORY HOUSE", 4f),
                Step.Caption("A house of painted mud brick. Swept floors, a table set for one, no footprints but theirs."),
                L.S(Speaker.Anpu, "This is your house in Wernes. It was built for you, and nobody has lived in it."),
                L.S(Speaker.Ankhu, "A false door. The dead come and go through these."),
                L.S(Speaker.Anpu, "Read what is written on it."),
                Step.Free()) { RequiresFlags = new[] { "progress.count.0" } });

            // Coming back from each room. The talk moves on every time.
            s.Beats.Add(new Beat("back_a", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Caption("The first panel beside the door is lit. A falcon over water."),
                L.S(Speaker.Ankhu, "He said the falcon would not fall because I was holding him."),
                L.S(Speaker.Anpu, "Did it fall?"),
                L.S(Speaker.Ankhu, "I do not remember. I remember that I was holding him."),
                Step.Free()) { RequiresFlags = new[] { "progress.back.A" } });

            s.Beats.Add(new Beat("back_b", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Caption("A second panel is lit. Three small figures with hoes."),
                L.S(Speaker.Ankhu, "The shabti worked for me all those years. I never once thought of them."),
                L.S(Speaker.Anpu, "They thought of you. It is what they were made for."),
                Step.Free()) { RequiresFlags = new[] { "progress.back.B" } });

            s.Beats.Add(new Beat("back_c", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Caption("A third panel is lit. A door, half open."),
                L.S(Speaker.Ankhu, "He said he would bring me something from the north."),
                L.S(Speaker.Anpu, "What did you want?"),
                L.S(Speaker.Ankhu, "For the door to open."),
                Step.Free()) { RequiresFlags = new[] { "progress.back.C" } });

            s.Beats.Add(new Beat("back_d", TriggerKind.Start, "",
                Step.Freeze(),
                Step.Caption("All four panels are lit. The house is warm for the first time."),
                L.S(Speaker.Ankhu, "She was the loudest person in our street. I thought it was shameful."),
                L.S(Speaker.Anpu, "The gods could hear her."),
                Step.Wait(0.8f),
                L.S(Speaker.Anpu, "There is one more field with your name on it."),
                L.S(Speaker.Ankhu, "I had one field. I have seen all of it."),
                L.S(Speaker.Anpu, "You have seen what was given to you. Not what you gave away."),
                Step.Free()) { RequiresFlags = new[] { "progress.back.D" } });

            // The door. Its text is Book of the Dead spell 92 (Allen, The Book of the Dead, p. 76).
            s.Beats.Add(new Beat("read_door", TriggerKind.Use, "read_door",
                Step.Caption("\"Wide-open or tight-shut and asleep, open wide for the soul therein.\""),
                L.S(Speaker.Ankhu, "It opens for the one it was made for. It wants to hear who that is.")) { Once = false });

            s.Beats.Add(new Beat("murmur", TriggerKind.Idle, "", L.S(Speaker.Anpu, "It is your door. Tell it so.")) { Value = 45f });

            s.Beats.Add(new Beat("solved", TriggerKind.Solved, "",
                Step.Freeze(),
                Step.Caption("The door opens on a field Ankhu has not seen yet."),
                L.S(Speaker.Anpu, "Go on. I will be here."),
                Step.Wait(1.0f),
                Step.Next()) { ForbidsFlags = new[] { "progress.all" } });

            s.Beats.Add(new Beat("solved_all", TriggerKind.Solved, "",
                Step.Freeze(),
                Step.Caption("The door opens on the terrace steps. The grey figures are still sitting there."),
                L.S(Speaker.Ankhu, "One of them looked up."),
                L.S(Speaker.Anpu, "Sometimes they do."),
                Step.Wait(1.5f),
                Step.Next()) { RequiresFlags = new[] { "progress.all" } });
            return s;
        }
    }
}
