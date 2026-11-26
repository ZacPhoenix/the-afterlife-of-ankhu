using System;
using System.Collections.Generic;

namespace Ankhu.Realms.Core
{
    public enum Speaker { Narrator, Ankhu, Anpu, Keeper, Guardian, Announcer, Memory, Neferet, Mother, Hori, Father, Other }

    public enum StepKind
    {
        /// <summary>A spoken line with subtitle. VoiceKey resolves to a clip if one is recorded.</summary>
        Say,
        /// <summary>Ankhu writes an entry in the codex (with the quill animation and toast).</summary>
        Write,
        /// <summary>A memory flashback: a painted still (Image key) with lines over it.</summary>
        Memory,
        /// <summary>Pause in seconds (Value).</summary>
        Wait,
        /// <summary>Show a centered title card (Text, Value seconds).</summary>
        Title,
        /// <summary>Fade out and load the next scene in the build order.</summary>
        NextScene,
        /// <summary>Freeze or free the player (Value 0 = freeze, 1 = free).</summary>
        Control,
        /// <summary>Move a named actor (Text = actor) to a named mark (Image = mark).</summary>
        MoveActor,
        /// <summary>Raise a scene event by name (Text), for the director to react to (camera, animation).</summary>
        Cue,
        /// <summary>Set a rules-free flag (Text) that conditions can test.</summary>
        Flag,
        /// <summary>Return to the title screen (end of the playable slice).</summary>
        EndSlice,
        /// <summary>Teach a verb now (Text = verb). Also writes word.&lt;verb&gt; in the codex.</summary>
        Learn,
        /// <summary>Set a rules object's state directly (Text = object id, Image = state).</summary>
        SetState,
        /// <summary>A line from no one (narration of what the world does), shown as a quiet caption.</summary>
        Caption
    }

    public sealed class Step
    {
        public StepKind Kind;
        public Speaker Who;
        public string Text = string.Empty;
        public string VoiceKey = string.Empty;
        public string Image = string.Empty;
        public float Value;

        public static Step Say(Speaker who, string key, string text) => new Step { Kind = StepKind.Say, Who = who, VoiceKey = key, Text = text };
        public static Step Write(string codexId) => new Step { Kind = StepKind.Write, Text = codexId };
        public static Step Memory(string image, Speaker who, string key, string text) => new Step { Kind = StepKind.Memory, Image = image, Who = who, VoiceKey = key, Text = text };
        public static Step Wait(float seconds) => new Step { Kind = StepKind.Wait, Value = seconds };
        public static Step Title(string text, float seconds = 4f) => new Step { Kind = StepKind.Title, Text = text, Value = seconds };
        public static Step Next() => new Step { Kind = StepKind.NextScene };
        public static Step Freeze() => new Step { Kind = StepKind.Control, Value = 0f };
        public static Step Free() => new Step { Kind = StepKind.Control, Value = 1f };
        public static Step Move(string actor, string mark) => new Step { Kind = StepKind.MoveActor, Text = actor, Image = mark };
        public static Step Cue(string name) => new Step { Kind = StepKind.Cue, Text = name };
        public static Step Flag(string name) => new Step { Kind = StepKind.Flag, Text = name };
        public static Step End() => new Step { Kind = StepKind.EndSlice };
        public static Step Learn(string verb) => new Step { Kind = StepKind.Learn, Text = verb };
        public static Step Set(string id, string state) => new Step { Kind = StepKind.SetState, Text = id, Image = state };
        public static Step Caption(string text) => new Step { Kind = StepKind.Caption, Text = text };
    }

    public enum TriggerKind
    {
        /// <summary>When the scene starts.</summary>
        Start,
        /// <summary>The first time Ankhu enters zone Key.</summary>
        EnterZone,
        /// <summary>When a successful spell raises event Key.</summary>
        Event,
        /// <summary>When the room's goal first holds.</summary>
        Solved,
        /// <summary>When Ankhu uses (E) the object or inscription Key.</summary>
        Use,
        /// <summary>The first failed spell of judgement Key (for example "Refused"), a gentle nudge.</summary>
        Failure,
        /// <summary>No successful spell for Value seconds while the room is unsolved (repeatable once each).</summary>
        Idle
    }

    public sealed class Beat
    {
        public string Id;
        public TriggerKind When;
        public string Key = string.Empty;
        public float Value;
        /// <summary>False lets the beat fire every time (inscriptions). Default fires once.</summary>
        public bool Once = true;
        /// <summary>Flags that must be set for this beat to fire.</summary>
        public string[] RequiresFlags = Array.Empty<string>();
        /// <summary>Flags that stop this beat from firing.</summary>
        public string[] ForbidsFlags = Array.Empty<string>();
        public List<Step> Steps = new List<Step>();

        public Beat(string id, TriggerKind when, string key = "", params Step[] steps)
        {
            Id = id;
            When = when;
            Key = key ?? string.Empty;
            Steps.AddRange(steps);
        }
    }

    /// <summary>An inscription or object Ankhu can read or use with E. Shown as a prompt when he is in Zone.</summary>
    public sealed class Usable
    {
        public string Id;
        public string Prompt = "Read";
        public string Zone;
        /// <summary>A deed done with the hands when E is pressed ("ride beam"), judged by the room's rules. Empty for none.</summary>
        public string Deed = string.Empty;
        /// <summary>The prompt shows only while these hold (empty means always).</summary>
        public Cond[] When = Array.Empty<Cond>();
    }

    /// <summary>Everything a realm scene does that is not geometry: its rules, its beats, its usables.</summary>
    /// <summary>Where Anpu is while a realm room plays (Zac 2026-11-26: he stays where he says he waits).</summary>
    public enum AnpuPresence
    {
        /// <summary>He walks at Ankhu's shoulder.</summary>
        Follows,
        /// <summary>He stands where the scene places him and turns to face Ankhu. A Move step walks him to a mark, and he stays there.</summary>
        Waits,
        /// <summary>He is not in the room. His lines, if any, are heard as a voice.</summary>
        Absent
    }

    public sealed class SceneScript
    {
        public string SceneName;
        public string Realm;
        public string Title;
        public RoomDef Room;
        public List<Beat> Beats = new List<Beat>();
        public List<Usable> Usables = new List<Usable>();
        /// <summary>Verbs and names that should already be in the codex when this scene loads directly (dev skip).</summary>
        public string[] CodexOnEntry = Array.Empty<string>();
        /// <summary>Where Anpu is in this room.</summary>
        public AnpuPresence Anpu = AnpuPresence.Follows;
        /// <summary>
        /// Objects whose lights live in real time (flame and fire light breathing) whatever state the puzzle holds.
        /// Zac 2026-11-26: the environment animates continuously, the puzzle only moves on commands.
        /// </summary>
        public string[] LiveLights = Array.Empty<string>();

        public IEnumerable<Step> AllSteps()
        {
            foreach (Beat b in Beats)
                foreach (Step s in b.Steps)
                    yield return s;
        }
    }
}
