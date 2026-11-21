using System;
using System.Collections.Generic;

namespace Ankhu.BorrowedDawn
{
    /// <summary>Stable reason codes. Presentation and localization key off these, never off message text.</summary>
    public enum Outcome
    {
        Accepted = 0,
        PuzzleComplete,
        AlreadyInState,
        NotAdjacent,
        ElevatorNotHere,
        NoLandingAtThisHeight,
        WalkwayIncomplete,
        BridgeSwinging,
        BindingAlreadyHeld,
        BindingCapacityFull,
        ElevatorNotReachable,
        NoShadowToBind,
        BridgeNotReachable,
        NotBound,
        WouldStrandAtGate,
        WrongLocation,
        MirrorAlreadyCollected,
        MirrorNotReachable,
        MirrorNotCarried,
        BellBroken,
        BellSilent,
        NameUnknown,
        GateConditionsUnmet,
        LadderRaised,
        BindingBearsLoad,
        UnavailableInProfile,
    }

    /// <summary>Result of applying a command. On rejection <see cref="State"/> is the unchanged input state.</summary>
    public sealed class CommandResult
    {
        public Outcome Outcome { get; }
        public bool Accepted => Outcome == Outcome.Accepted;
        public RoomState Previous { get; }
        public RoomState State { get; }
        public Command Command { get; }
        public string Message { get; }

        internal CommandResult(Outcome outcome, RoomState previous, RoomState state, Command command, string message)
        {
            Outcome = outcome;
            Previous = previous;
            State = state;
            Command = command;
            Message = message;
        }
    }

    /// <summary>
    /// Pure transition function for the Borrowed Dawn room. A port of prototype/borrowed-dawn.html
    /// (functions move and command) under <see cref="RuleProfile.Prototype"/>, with tightenings documented in README.md,
    /// plus the <see cref="RuleProfile.Greybox"/> walking-layout variant:
    /// movement checks adjacency explicitly, a second strike of the bell is rejected instead of being a
    /// no-op success, and every rejection returns the input state untouched.
    /// </summary>
    public static class RoomRules
    {
        /// <summary>Walkable edges, mirroring the prototype's navigation buttons. Gallery to Lift is absent in the prototype.</summary>
        private static readonly Dictionary<Location, Location[]> AdjacentPrototype = new Dictionary<Location, Location[]>
        {
            { Location.Floor, new[] { Location.Lift } },
            { Location.Lift, new[] { Location.Floor, Location.Gallery } },
            { Location.Gallery, new[] { Location.Floor, Location.Landing } },
            { Location.Landing, new[] { Location.Gallery, Location.Gate } },
            { Location.Gate, new[] { Location.Landing } },
            { Location.Bridge, new Location[0] },
        };

        /// <summary>
        /// Greybox walking layout. Gallery and lift connect when the elevator is high. Floor and gallery connect
        /// only through the ladder hatch. Landing to gate may be entered as one hop or through the bridge zone.
        /// </summary>
        private static readonly Dictionary<Location, Location[]> AdjacentGreybox = new Dictionary<Location, Location[]>
        {
            { Location.Floor, new[] { Location.Lift, Location.Gallery } },
            { Location.Lift, new[] { Location.Floor, Location.Gallery } },
            { Location.Gallery, new[] { Location.Lift, Location.Landing, Location.Floor } },
            { Location.Landing, new[] { Location.Gallery, Location.Bridge, Location.Gate } },
            { Location.Bridge, new[] { Location.Landing, Location.Gate } },
            { Location.Gate, new[] { Location.Bridge, Location.Landing } },
        };

        private static Dictionary<Location, Location[]> Adjacent(RuleProfile p) =>
            p == RuleProfile.Greybox ? AdjacentGreybox : AdjacentPrototype;

        /// <summary>Locations walkable from <paramref name="from"/> when their conditions hold.</summary>
        public static IReadOnlyList<Location> Neighbours(Location from, RuleProfile profile = RuleProfile.Prototype) => Adjacent(profile)[from];

        /// <summary>Every syntactically distinct command. Used by the solver and by fuzz tests.</summary>
        public static IReadOnlyList<Command> AllCommands { get; } = BuildAllCommands();

        private static Command[] BuildAllCommands()
        {
            var list = new List<Command>
            {
                Command.Flow(WaterSide.West), Command.Flow(WaterSide.East),
            };
            foreach (BindTarget t in Enum.GetValues(typeof(BindTarget)))
            {
                list.Add(Command.Bind(t));
                list.Add(Command.Release(t));
            }
            list.Add(Command.WakeBearer);
            list.Add(Command.SleepBearer);
            list.Add(Command.RecallBell);
            list.Add(Command.StrikeBell);
            list.Add(Command.EchoBell);
            list.Add(Command.TakeMirror);
            list.Add(Command.InstallMirror);
            list.Add(Command.ReadInscription);
            list.Add(Command.SpeakName);
            foreach (Location l in Enum.GetValues(typeof(Location)))
            {
                list.Add(Command.MoveTo(l));
            }
            list.Add(Command.CrossBridge);
            list.Add(Command.LowerLadder);
            return list.ToArray();
        }

        /// <summary>Applies a command under the <see cref="RuleProfile.Prototype"/> rules.</summary>
        public static CommandResult Apply(RoomState s, Command c) => Apply(s, c, RuleProfile.Prototype);

        public static CommandResult Apply(RoomState s, Command c, RuleProfile p)
        {
            if (s is null) throw new ArgumentNullException(nameof(s));
            if (c is null) throw new ArgumentNullException(nameof(c));

            if (s.Won)
            {
                return Reject(s, c, Outcome.PuzzleComplete, "The morning has returned. Restart to play again.");
            }

            switch (c.Kind)
            {
                case CommandKind.MoveTo: return Move(s, c, c.Destination, p);
                case CommandKind.CrossBridge:
                    if (s.Location == Location.Landing || s.Location == Location.Bridge) return Move(s, c, Location.Gate, p);
                    if (s.Location == Location.Gate) return Move(s, c, Location.Landing, p);
                    return Reject(s, c, Outcome.WrongLocation, "Reach the bell landing to cross the bridge.");
                case CommandKind.Flow: return Flow(s, c);
                case CommandKind.Bind: return Bind(s, c);
                case CommandKind.Release: return Release(s, c, p);
                case CommandKind.TakeMirror: return TakeMirror(s, c, p);
                case CommandKind.LowerLadder:
                    if (p != RuleProfile.Greybox)
                        return Reject(s, c, Outcome.UnavailableInProfile, "There is no ladder in this chamber.");
                    if (s.Location != Location.Gallery)
                        return Reject(s, c, Outcome.WrongLocation, "The ladder hatch is in the upper gallery.");
                    if (s.LadderLowered)
                        return Reject(s, c, Outcome.AlreadyInState, "The ladder already reaches the floor.");
                    return Accept(s, c, s.WithLadderLowered(true), "The ladder unrolls from the hatch to the floor below.");
                case CommandKind.InstallMirror:
                    if (s.Location != Location.Floor || s.Mirror != MirrorPlace.Carried)
                        return Reject(s, c, Outcome.MirrorNotCarried, "Carry the mirror to the ground-floor socket first.");
                    return Accept(s, c, s.WithMirror(MirrorPlace.Socket), "The mirror catches sunlight. The Bearer casts a shadow onto the seal.");
                case CommandKind.ReadInscription:
                    if (s.Location != Location.Gallery)
                        return Reject(s, c, Outcome.WrongLocation, "Reach the upper gallery to read its inscriptions.");
                    if (s.NameDiscovered)
                        return Reject(s, c, Outcome.AlreadyInState, "The name is “He Who Returns the Morning.” The relief shows a shadow remaining after its owner leaves.");
                    return Accept(s, c, s.WithNameDiscovered(true), "The gate is “He Who Returns the Morning.” A relief depicts a body departing while its shadow remains.");
                case CommandKind.WakeBearer:
                    if (s.BearerAwake)
                        return Reject(s, c, Outcome.AlreadyInState, "The Bearer is already in its alcove.");
                    return Accept(s, c, s.WithBearerAwake(true), "The Bearer walks into its alcove. " +
                        (s.IsBound(BindTarget.Shadow) ? "Its shadow remains." : "Its shadow follows. Perhaps sleep can restore it."));
                case CommandKind.SleepBearer:
                    if (!s.BearerAwake)
                        return Reject(s, c, Outcome.AlreadyInState, "The Bearer is already at rest.");
                    return Accept(s, c, s.WithBearerAwake(false), "The Bearer returns to its marked pedestal and sleeps.");
                case CommandKind.RecallBell:
                    if (s.Location != Location.Landing)
                        return Reject(s, c, Outcome.WrongLocation, "Reach the bell landing first.");
                    if (s.BellRepaired)
                        return Reject(s, c, Outcome.AlreadyInState, "The bell is already whole.");
                    return Accept(s, c, s.WithBellRepaired(true), "Gold fills the bell’s cracks. It remembers its intact form.");
                case CommandKind.StrikeBell:
                    if (s.Location != Location.Landing || !s.BellRepaired)
                        return Reject(s, c, Outcome.BellBroken, "Stand at the bell landing and repair the bell before striking it.");
                    if (s.BellStruck)
                        return Reject(s, c, Outcome.AlreadyInState, "The bell's note still hangs in the resonator.");
                    return Accept(s, c, s.WithBellStruck(true), "A clear bronze note fills the chamber. The resonator remembers it.");
                case CommandKind.EchoBell:
                    if (!s.BellStruck)
                        return Reject(s, c, Outcome.BellSilent, "The bell has no clear sound to remember. Repair it, then strike it.");
                    if (s.EchoActive)
                        return Reject(s, c, Outcome.AlreadyInState, "The bell is already repeating its toll.");
                    return Accept(s, c, s.WithEchoActive(true), "The bell repeats its remembered note. This uses no binding.");
                case CommandKind.SpeakName:
                    if (s.Location != Location.Gate)
                        return Reject(s, c, Outcome.WrongLocation, "Speak the gate’s name from its threshold.");
                    if (!s.NameDiscovered)
                        return Reject(s, c, Outcome.NameUnknown, p == RuleProfile.Greybox
                            ? SpellParser.UnknownWordFeedback("he", SpellParser.Suggest("he"))
                            : "Learn the name from the gallery first.");
                    if (!s.SunAtGate || !s.ShadowPresent || !s.EchoActive)
                        return Reject(s, c, Outcome.GateConditionsUnmet, "The gate hears you, but its sun, shadow, and sound must all be present.");
                    return Accept(s, c, s.WithWon(true), "The gate opens. Dawn pours into the chamber.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(c), c.Kind, "Unhandled command kind.");
            }
        }

        private static CommandResult Move(RoomState s, Command c, Location to, RuleProfile p)
        {
            Location from = s.Location;
            if (from == to)
                return Reject(s, c, Outcome.AlreadyInState, "Ankhu is already there.");
            if (Array.IndexOf(Adjacent(p)[from], to) < 0)
                return Reject(s, c, Outcome.NotAdjacent, "That place cannot be reached directly from here.");
            if (p == RuleProfile.Greybox)
                return MoveGreybox(s, c, from, to);
            if (from == Location.Floor && to == Location.Lift && s.ElevatorHigh)
                return Reject(s, c, Outcome.ElevatorNotHere, "The elevator is above you. Send the water west to lower it.");
            if (from == Location.Lift && ((to == Location.Gallery && !s.ElevatorHigh) || (to == Location.Floor && s.ElevatorHigh)))
                return Reject(s, c, Outcome.NoLandingAtThisHeight, "There is no landing at this height. Change the water direction.");
            if (((from == Location.Gallery && to == Location.Landing) || (from == Location.Landing && to == Location.Gallery)) && !s.ElevatorHigh)
                return Reject(s, c, Outcome.WalkwayIncomplete, "The elevator must be high to complete the upper walkway.");
            if ((to == Location.Gate || from == Location.Gate) && !s.BridgeHeld)
                return Reject(s, c, Outcome.BridgeSwinging, "The pendulum is moving. Bind the bridge before crossing.");
            return Accept(s, c, s.WithLocation(to), "Ankhu moves to the " + to.ToString().ToLowerInvariant() + ".");
        }

        private static CommandResult MoveGreybox(RoomState s, Command c, Location from, Location to)
        {
            bool Between(Location a, Location b) => (from == a && to == b) || (from == b && to == a);
            if (Between(Location.Floor, Location.Lift) && s.ElevatorHigh)
                return Reject(s, c, from == Location.Floor ? Outcome.ElevatorNotHere : Outcome.NoLandingAtThisHeight,
                    from == Location.Floor ? "The elevator is above you. Send the water west to lower it." : "There is no landing at this height. Change the water direction.");
            if (Between(Location.Lift, Location.Gallery) && !s.ElevatorHigh)
                return Reject(s, c, from == Location.Gallery ? Outcome.ElevatorNotHere : Outcome.NoLandingAtThisHeight,
                    from == Location.Gallery ? "The elevator is below the gallery. Send the water east to raise it." : "There is no landing at this height. Change the water direction.");
            if (Between(Location.Floor, Location.Gallery) && !s.LadderLowered)
                return Reject(s, c, Outcome.LadderRaised, from == Location.Floor ? "The hatch above is closed. Its ladder is still rolled up in the gallery." : "The ladder is still rolled up beside the hatch.");
            if ((from == Location.Gate || from == Location.Bridge || to == Location.Gate || to == Location.Bridge) && !s.BridgeHeld)
                return Reject(s, c, Outcome.BridgeSwinging, "The pendulum is moving. Bind the bridge before crossing.");
            return Accept(s, c, s.WithLocation(to), "Ankhu moves to the " + to.ToString().ToLowerInvariant() + ".");
        }

        private static CommandResult Flow(RoomState s, Command c)
        {
            if (s.Water == c.Direction)
                return Reject(s, c, Outcome.AlreadyInState, "The water is already in the " + c.Direction.ToString().ToLowerInvariant() + " basin.");
            return Accept(s, c, s.WithWater(c.Direction), c.Direction == WaterSide.West
                ? "Water pours west. The raft rises."
                : "Water returns east. The elevator gains buoyant support.");
        }

        private static CommandResult Bind(RoomState s, Command c)
        {
            BindTarget t = c.Target;
            if (s.IsBound(t))
                return Reject(s, c, Outcome.BindingAlreadyHeld, "That binding is already held.");
            if (!s.HasFreeBinding)
                return Reject(s, c, Outcome.BindingCapacityFull, "Both bindings are occupied. Find another way to support something, then release it.");
            switch (t)
            {
                case BindTarget.Elevator:
                    if (!s.ElevatorHigh || !(s.Location == Location.Gallery || s.Location == Location.Landing || s.Location == Location.Lift))
                        return Reject(s, c, Outcome.ElevatorNotReachable, "Stand on the raised elevator or the upper walkway before anchoring it.");
                    break;
                case BindTarget.Shadow:
                    if (!s.ShadowPresent)
                        return Reject(s, c, Outcome.NoShadowToBind, "A shadow must fall on the seal before you can bind it.");
                    break;
                case BindTarget.Bridge:
                    if (s.Location != Location.Landing)
                        return Reject(s, c, Outcome.BridgeNotReachable, "Reach the bell landing to address the bridge.");
                    break;
            }
            string msg = t == BindTarget.Bridge
                ? "The bridge settles horizontally and holds."
                : "A binding thread holds the " + t.ToString().ToLowerInvariant() + ".";
            return Accept(s, c, s.WithBindings(s.Bindings | t.ToFlag()), msg);
        }

        private static CommandResult Release(RoomState s, Command c, RuleProfile p)
        {
            BindTarget t = c.Target;
            if (!s.IsBound(t))
                return Reject(s, c, Outcome.NotBound, "That target is not bound.");
            if (p == RuleProfile.Greybox && t == BindTarget.Elevator && s.Water == WaterSide.West)
                return Reject(s, c, Outcome.BindingBearsLoad, "The elevator hangs on the binding alone. Let the water carry it before you let go.");
            if (t == BindTarget.Bridge && (s.Location == Location.Gate || s.Location == Location.Bridge))
                return Reject(s, c, Outcome.WouldStrandAtGate, "Cross back before releasing your only return path.");
            return Accept(s, c, s.WithBindings(s.Bindings & ~t.ToFlag()), "You release the " + t.ToString().ToLowerInvariant() + ".");
        }

        private static CommandResult TakeMirror(RoomState s, Command c, RuleProfile p)
        {
            if (p == RuleProfile.Greybox)
            {
                if (s.Location != Location.Lift)
                    return Reject(s, c, Outcome.WrongLocation, "The raft is only within reach from the raised elevator platform.");
                if (s.Mirror != MirrorPlace.Raft)
                    return Reject(s, c, Outcome.MirrorAlreadyCollected, "You have already collected the mirror.");
                if (s.Water != WaterSide.West || !s.ElevatorHigh)
                    return Reject(s, c, Outcome.MirrorNotReachable, "The raft must be raised and the elevator held high beside it.");
                return Accept(s, c, s.WithMirror(MirrorPlace.Carried), "You lift the sun-mirror from the raft. Bring it to the socket below.");
            }
            if (s.Location != Location.Gallery)
                return Reject(s, c, Outcome.WrongLocation, "The mirror is collected from the upper gallery balcony.");
            if (s.Mirror != MirrorPlace.Raft)
                return Reject(s, c, Outcome.MirrorAlreadyCollected, "You have already collected the mirror.");
            if (s.Water != WaterSide.West || !s.ElevatorHigh)
                return Reject(s, c, Outcome.MirrorNotReachable, "The raft must be raised and the elevator must hold the balcony connection.");
            return Accept(s, c, s.WithMirror(MirrorPlace.Carried), "You carry the sun-mirror. Descend to the socket below.");
        }

        private static CommandResult Accept(RoomState s, Command c, RoomState next, string msg) =>
            new CommandResult(Outcome.Accepted, s, next, c, msg);

        private static CommandResult Reject(RoomState s, Command c, Outcome o, string msg) =>
            new CommandResult(o, s, s, c, msg);
    }
}
