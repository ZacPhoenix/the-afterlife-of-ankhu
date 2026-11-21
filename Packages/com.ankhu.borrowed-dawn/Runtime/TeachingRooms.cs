using System.Collections.Generic;

namespace Ankhu.BorrowedDawn
{
    /// <summary>
    /// Three small rooms in which Anpu teaches the Borrowed Dawn mechanics one at a time, before the full room.
    /// They reuse the Greybox rules and the same parser, so the Unity kit can build them from the same parts.
    /// Anpu's lines are provisional dialogue for the greybox, not final script.
    /// </summary>
    public static class TeachingRooms
    {
        /// <summary>Room 1. Speaking moves water, and water carries the lift.</summary>
        public static RoomDefinition Cistern { get; } = new RoomDefinition(
            id: "teach-1-cistern",
            title: "The Cistern",
            profile: RuleProfile.Greybox,
            start: RoomState.Initial,
            allows: c => c.Kind == CommandKind.Flow || c.Kind == CommandKind.MoveTo,
            isGoal: s => s.Location == Location.Gallery,
            lesson: "flow west and flow east move the water, and the water moves the lift.",
            anpuIntro: new[]
            {
                "Your voice has come back to you, Ankhu. Here, words are the only hands you have.",
                "Water listens to a direction. Say flow, then west or east.",
                "The gallery is above us. Watch what the water carries before you ask it to move.",
            },
            anpuOnSolve: "You did not climb. You asked, and the water carried you.",
            anpuNudges: new Dictionary<Outcome, string>
            {
                { Outcome.ElevatorNotHere, "The platform rides on the water. Where must the water go for it to come down to you?" },
                { Outcome.NoLandingAtThisHeight, "You stand between floors. Let the water choose your height." },
                { Outcome.LadderRaised, "The ladder is rolled up above. There is another way to rise." },
                { Outcome.NotYetTaught, "That word has no power here yet. In this room only the water listens." },
            });

        /// <summary>Room 2. A binding holds what its support no longer holds, and must be given back before release.</summary>
        public static RoomDefinition HeldStep { get; } = new RoomDefinition(
            id: "teach-2-held-step",
            title: "The Held Step",
            profile: RuleProfile.Greybox,
            start: RoomState.Initial.WithLocation(Location.Lift),
            allows: c => c.Kind == CommandKind.Flow || c.Kind == CommandKind.MoveTo || c.Kind == CommandKind.TakeMirror
                || ((c.Kind == CommandKind.Bind || c.Kind == CommandKind.Release) && c.Target == BindTarget.Elevator),
            isGoal: s => s.Mirror == MirrorPlace.Carried && s.Location == Location.Gallery && s.Bindings == BindingSet.None,
            lesson: "bind holds a thing after its support leaves. release lets it go, but not while the binding bears the whole weight.",
            anpuIntro: new[]
            {
                "The mirror floats on the western raft. The raft rises only when the water goes west, and then your platform sinks.",
                "Some things must stay where they are after their support is gone. Say bind, then the thing you would hold.",
                "Bring the mirror to the gallery, and leave nothing bound behind you.",
            },
            anpuOnSolve: "You held only as long as you needed to. Remember the feeling of letting go.",
            anpuNudges: new Dictionary<Outcome, string>
            {
                { Outcome.MirrorNotReachable, "The raft rises only with the water in the west. Can your platform stay high without the water?" },
                { Outcome.BindingBearsLoad, "That thread carries the whole platform now. Give its weight back to the water first." },
                { Outcome.ElevatorNotReachable, "You can only hold the platform while you stand on it, high in the shaft." },
                { Outcome.NotYetTaught, "That word has no power here yet. Hold, release, and move the water." },
            });

        /// <summary>
        /// Room 3. A bound shadow outlasts its owner, and the voice holds only two threads. Ankhu arrives still
        /// holding the lift and the bridge from earlier, so the binding limit is felt immediately.
        /// </summary>
        public static RoomDefinition LingeringShadow { get; } = new RoomDefinition(
            id: "teach-3-lingering-shadow",
            title: "The Lingering Shadow",
            profile: RuleProfile.Greybox,
            start: new RoomState(WaterSide.West, Location.Floor, BindingSet.Elevator | BindingSet.Bridge, MirrorPlace.Socket,
                bearerAwake: false, nameDiscovered: false, bellRepaired: false, bellStruck: false, echoActive: false, won: false),
            allows: c => c.Kind == CommandKind.Flow || c.Kind == CommandKind.Bind || c.Kind == CommandKind.Release
                || c.Kind == CommandKind.WakeBearer || c.Kind == CommandKind.SleepBearer,
            isGoal: s => s.SunAtGate && s.ShadowPresent,
            lesson: "bind shadow before wake bearer, and free a thread first because the voice holds only two.",
            anpuIntro: new[]
            {
                "The far seal wants light. The near seal wants shadow. The gate ahead will ask for both at once.",
                "The sleeper stands in the beam and casts the shadow. Wake him and he walks out of the light. His shadow follows him.",
                "Unless you hold it first. And you still hold two threads from before. Your voice can carry no more than two.",
            },
            anpuOnSolve: "The shadow stays though its owner has gone. You will need this again.",
            anpuNudges: new Dictionary<Outcome, string>
            {
                { Outcome.BindingCapacityFull, "Your voice already holds two threads. Which one do you still need?" },
                { Outcome.NoShadowToBind, "There is no shadow to hold. Perhaps its owner should rest again." },
                { Outcome.BindingBearsLoad, "That thread carries the platform. Give its weight to the water first." },
                { Outcome.NotYetTaught, "That word has no power here yet. Hold, release, wake, sleep, and move the water." },
            });

        /// <summary>The teaching sequence in play order, followed by the full room.</summary>
        public static IReadOnlyList<RoomDefinition> Sequence { get; } = new[]
        {
            Cistern,
            HeldStep,
            LingeringShadow,
            RoomDefinition.BorrowedDawn(RuleProfile.Greybox),
        };
    }
}
