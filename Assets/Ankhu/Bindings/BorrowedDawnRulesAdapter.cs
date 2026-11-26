using System;
using System.Collections.Generic;
using System.Globalization;
using Ankhu.BorrowedDawn;
using Ankhu.Integration;
using Rules = Ankhu.BorrowedDawn;
using CommandResult = Ankhu.Integration.CommandResult;

namespace Ankhu.Bindings
{
    /// <summary>
    /// Binds the engine-free Borrowed Dawn rules library (PuzzleSession, RuleProfile.Greybox) to the
    /// greybox's <see cref="IRoomRulesAdapter"/>. Pure translation: no rule is evaluated here.
    ///
    /// Location: the rules library keeps Ankhu's location in its state. Before every Speak or Interact
    /// the adapter moves the rules location to the zone the greybox reports, so reach checks run against
    /// where Ankhu physically stands. Zone changes during walking go through <see cref="SyncLocation"/>.
    /// </summary>
    public sealed class BorrowedDawnRulesAdapter : IRoomRulesAdapter, IHintTiers
    {
        private static readonly Dictionary<string, Location> ZoneToLocation = new Dictionary<string, Location>(StringComparer.Ordinal)
        {
            { ZoneIds.Floor, Location.Floor },
            { ZoneIds.Lift, Location.Lift },
            { ZoneIds.Gallery, Location.Gallery },
            { ZoneIds.Landing, Location.Landing },
            { ZoneIds.Bridge, Location.Bridge },
            { ZoneIds.Gate, Location.Gate },
        };

        private static readonly Dictionary<string, Command> InteractionToCommand = new Dictionary<string, Command>(StringComparer.Ordinal)
        {
            { InteractionIds.ReadInscription, Command.ReadInscription },
            { InteractionIds.TakeMirror, Command.TakeMirror },
            { InteractionIds.InstallMirror, Command.InstallMirror },
            { InteractionIds.StrikeBell, Command.StrikeBell },
            { InteractionIds.LowerLadder, Command.LowerLadder },
        };

        private readonly PuzzleSession session;
        private RoomSnapshot current;

        public event Action<RoomSnapshot, RoomSnapshot> StateChanged;

        /// <summary>
        /// Every command the rules evaluated, accepted or not, with the library's own outcome. Lets
        /// presentation react to specific refusals, such as Anpu's nudges in the teaching rooms.
        /// </summary>
        public event Action<Rules.CommandResult> RulesResolved;

        public RoomSnapshot Current => current;

        /// <summary>The rules location (one of ZoneIds) after the last accepted change.</summary>
        public string RulesZoneId => ToZoneId(session.State.Location);

        public PuzzleSession Session => session;

        /// <summary>The room being played: vocabulary, goal and start state.</summary>
        public RoomDefinition Room => session.Room;

        /// <summary>True once the room's goal holds.</summary>
        public bool IsSolved => session.IsSolved;

        public BorrowedDawnRulesAdapter() : this(new PuzzleSession(RuleProfile.Greybox))
        {
        }

        /// <summary>Plays one room of the sequence, for example a teaching room.</summary>
        public BorrowedDawnRulesAdapter(RoomDefinition room) : this(new PuzzleSession(room ?? throw new ArgumentNullException(nameof(room))))
        {
        }

        public BorrowedDawnRulesAdapter(PuzzleSession puzzleSession)
        {
            session = puzzleSession ?? throw new ArgumentNullException(nameof(puzzleSession));
            current = ToSnapshot(session.State);
            session.StateChanged += HandleSessionChanged;
            session.CommandResolved += HandleCommandResolved;
        }

        public CommandResult Speak(string rawText, PlayerContext context)
        {
            CommandResult moveFailure = SyncLocation(context.ZoneId);
            if (moveFailure != null)
            {
                return moveFailure;
            }

            SubmitResult submit = session.Submit(rawText ?? string.Empty);
            if (submit.Execution == null)
            {
                return new CommandResult(FromParse(submit.Parse.Status), submit.Message);
            }
            return FromExecution(submit.Execution);
        }

        public CommandResult Interact(string interactionId, PlayerContext context)
        {
            if (interactionId == null || !InteractionToCommand.TryGetValue(interactionId, out Command command))
            {
                return new CommandResult(CommandOutcome.InvalidTarget, "Nothing here responds to that.");
            }

            CommandResult moveFailure = SyncLocation(context.ZoneId);
            if (moveFailure != null)
            {
                return moveFailure;
            }
            return FromExecution(session.Execute(command));
        }

        public CommandResult Preview(string interactionId, PlayerContext context)
        {
            if (interactionId == null || !InteractionToCommand.TryGetValue(interactionId, out Command command))
            {
                return new CommandResult(CommandOutcome.InvalidTarget, "Nothing here responds to that.");
            }
            if (context.ZoneId == null || !ZoneToLocation.TryGetValue(context.ZoneId, out Location zone))
            {
                return new CommandResult(CommandOutcome.Rejected, "Ankhu is somewhere the chamber does not recognise.");
            }

            RoomState state = session.State;
            if (state.Location != zone)
            {
                Rules.CommandResult move = session.Room.Apply(state, Command.MoveTo(zone));
                if (!move.Accepted)
                {
                    return FromExecution(move);
                }
                state = move.State;
            }
            return FromExecution(session.Room.Apply(state, command));
        }

        /// <summary>
        /// Moves the rules location to the given zone. Returns null when the rules already agree or
        /// accepted the move. Returns the rejection otherwise, and the rules state is unchanged.
        /// </summary>
        public CommandResult SyncLocation(string zoneId)
        {
            if (zoneId == null || !ZoneToLocation.TryGetValue(zoneId, out Location target))
            {
                return new CommandResult(CommandOutcome.Rejected, "Ankhu is somewhere the chamber does not recognise.");
            }
            if (session.State.Location == target)
            {
                return null;
            }

            Rules.CommandResult result = session.Execute(Command.MoveTo(target));
            return result.Accepted ? null : FromExecution(result);
        }

        public CommandResult Undo()
        {
            return session.Undo()
                ? new CommandResult(CommandOutcome.Success, "Your previous action is undone.")
                : new CommandResult(CommandOutcome.NoChange, "Nothing to undo yet.");
        }

        public void Reset()
        {
            session.Reset();
        }

        /// <summary>
        /// Reports which gate conditions are still missing. It reads the library's derived flags and
        /// gives no solution steps. Authored hints belong to the rules library when it provides them.
        /// </summary>
        public string Hint(PlayerContext context)
        {
            RoomState s = session.State;
            RoomDefinition room = session.Room;
            if (room.Id != BorrowedDawnRooms.BorrowedDawnId)
            {
                if (session.IsSolved)
                {
                    return room.AnpuOnSolve;
                }
                IReadOnlyList<string> intro = room.AnpuIntro;
                return intro.Count == 0 ? room.Lesson : intro[intro.Count - 1];
            }
            if (s.Won)
            {
                return "The morning has returned.";
            }

            var missing = new List<string>(4);
            if (!s.SunAtGate) missing.Add("sun");
            if (!s.ShadowPresent) missing.Add("shadow");
            if (!s.EchoActive) missing.Add("sound");
            if (!s.NameDiscovered) missing.Add("a name");
            if (missing.Count == 0)
            {
                return "Sun, shadow and sound are present. Speak the Name at the threshold.";
            }
            return "The gate still waits for " + JoinNatural(missing) + ".";
        }

        /// <summary>
        /// The three sealed hint steps (Zac 2026-11-25). The nudge is <see cref="Hint"/>. The direction names the
        /// thing that must change next and the answer is the next deed in its spoken form, both read from a
        /// breadth first search of the rules from the current state. Walking steps are folded into the answer.
        /// </summary>
        /// <summary>
        /// Zac 2026-11-26 (the cistern, "don't know what to do"): the next words to speak on the shortest way to the
        /// goal, or null when the next step is a walk, the room is solved or no way is left.
        /// </summary>
        public string NextSpokenCommand()
        {
            if (session.IsSolved) return null;
            try
            {
                IReadOnlyList<Command> path = Ankhu.BorrowedDawn.Analysis.StateSpace.Explore(session.Room, session.State).ShortestSolution();
                if (path == null || path.Count == 0 || path[0].Kind == CommandKind.MoveTo) return null;
                return path[0].ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public string[] HintTiers(PlayerContext context)
        {
            string nudge = Hint(context);
            if (session.IsSolved)
            {
                return new[] { nudge, nudge, nudge };
            }
            IReadOnlyList<Command> path;
            try
            {
                path = Ankhu.BorrowedDawn.Analysis.StateSpace.Explore(session.Room, session.State).ShortestSolution();
            }
            catch (Exception)
            {
                path = null;
            }
            if (path == null || path.Count == 0)
            {
                return new[] { nudge, "No way forward is left from here. Undo with U, or hold R to begin again.", "Hold R to return the room to its first dawn." };
            }
            Location? walkTo = null;
            Command next = null;
            foreach (Command c in path)
            {
                if (c.Kind == CommandKind.MoveTo) { walkTo = c.Destination; continue; }
                next = c;
                break;
            }
            if (next == null)
            {
                string place = walkTo.HasValue ? walkTo.Value.ToString().ToLowerInvariant() : "the threshold";
                return new[] { nudge, "Only walking is left.", "Walk to the " + place + "." };
            }
            string where = walkTo.HasValue ? "From the " + walkTo.Value.ToString().ToLowerInvariant() + ", say: " : "Say: ";
            return new[] { nudge, DirectionFor(next), where + next + "." };
        }

        private static string DirectionFor(Command c)
        {
            switch (c.Kind)
            {
                case CommandKind.Flow: return "The water has to move before anything else can.";
                case CommandKind.Bind: return "Something here must be held still by a binding.";
                case CommandKind.Release: return "A binding you placed is now in the way. Let something go.";
                case CommandKind.WakeBearer:
                case CommandKind.SleepBearer: return "The bearer is the key to the next step.";
                case CommandKind.RecallBell:
                case CommandKind.StrikeBell:
                case CommandKind.EchoBell: return "Turn your mind to the bell and its sound.";
                case CommandKind.TakeMirror:
                case CommandKind.InstallMirror: return "The mirror can carry the sun where it is needed.";
                case CommandKind.ReadInscription: return "The wall holds words you have not read yet.";
                case CommandKind.SpeakName: return "Everything is ready. Only the Name is left to speak.";
                case CommandKind.CrossBridge: return "The way on lies across the bridge.";
                case CommandKind.LowerLadder: return "The ladder can bring the high place down to you.";
                default: return "Look again at what has not changed yet.";
            }
        }

        public string SaveState()
        {
            return session.State.Pack().ToString(CultureInfo.InvariantCulture);
        }

        public bool LoadState(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload) ||
                !int.TryParse(payload.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int packed))
            {
                return false;
            }

            RoomState state;
            try
            {
                state = RoomState.Unpack(packed);
            }
            catch (ArgumentException)
            {
                return false;
            }
            session.Load(state);
            return true;
        }

        public static string ToZoneId(Location location)
        {
            switch (location)
            {
                case Location.Floor: return ZoneIds.Floor;
                case Location.Lift: return ZoneIds.Lift;
                case Location.Gallery: return ZoneIds.Gallery;
                case Location.Landing: return ZoneIds.Landing;
                case Location.Bridge: return ZoneIds.Bridge;
                case Location.Gate: return ZoneIds.Gate;
                default: throw new ArgumentOutOfRangeException(nameof(location), location, null);
            }
        }

        public static RoomSnapshot ToSnapshot(RoomState s)
        {
            return new RoomSnapshot(
                water: s.Water == Rules.WaterSide.West ? Integration.WaterSide.West : Integration.WaterSide.East,
                mirror: ToMirror(s.Mirror),
                elevatorBound: s.IsBound(BindTarget.Elevator),
                shadowBound: s.IsBound(BindTarget.Shadow),
                bridgeBound: s.IsBound(BindTarget.Bridge),
                bindingsUsed: s.BindingCount,
                bindingCapacity: RoomState.BindingCapacity,
                bearerAwake: s.BearerAwake,
                inscriptionRead: s.NameDiscovered,
                bellRepaired: s.BellRepaired,
                bellStruck: s.BellStruck,
                bellEchoing: s.EchoActive,
                ladderLowered: s.LadderLowered,
                gateOpen: s.Won,
                elevatorHigh: s.ElevatorHigh,
                raftHigh: s.RaftRaised,
                lightScreenRaised: s.LightScreenRaised,
                shadowPresent: s.ShadowPresent,
                sunReachesGate: s.SunAtGate);
        }

        private static MirrorLocation ToMirror(MirrorPlace place)
        {
            switch (place)
            {
                case MirrorPlace.Raft: return MirrorLocation.Raft;
                case MirrorPlace.Carried: return MirrorLocation.Carried;
                case MirrorPlace.Socket: return MirrorLocation.Socket;
                default: throw new ArgumentOutOfRangeException(nameof(place), place, null);
            }
        }

        private static CommandResult FromExecution(Rules.CommandResult result)
        {
            CommandOutcome outcome;
            switch (result.Outcome)
            {
                case Outcome.Accepted:
                    outcome = CommandOutcome.Success;
                    break;
                case Outcome.AlreadyInState:
                    outcome = CommandOutcome.NoChange;
                    break;
                default:
                    outcome = result.Accepted ? CommandOutcome.Success : CommandOutcome.Rejected;
                    break;
            }
            return new CommandResult(outcome, result.Message);
        }

        private static CommandOutcome FromParse(ParseStatus status)
        {
            switch (status)
            {
                case ParseStatus.UnknownVerb: return CommandOutcome.NotUnderstood;
                case ParseStatus.Empty: return CommandOutcome.Incomplete;
                case ParseStatus.MissingArgument: return CommandOutcome.Incomplete;
                case ParseStatus.InvalidArgument: return CommandOutcome.InvalidTarget;
                case ParseStatus.TrailingWords: return CommandOutcome.InvalidTarget;
                default: return CommandOutcome.NotUnderstood;
            }
        }

        private static string JoinNatural(List<string> items)
        {
            if (items.Count == 1) return items[0];
            return string.Join(", ", items.GetRange(0, items.Count - 1)) + " and " + items[items.Count - 1];
        }

        private void HandleCommandResolved(Rules.CommandResult result)
        {
            RulesResolved?.Invoke(result);
        }

        private void HandleSessionChanged(RoomState before, RoomState after)
        {
            RoomSnapshot previous = current;
            current = ToSnapshot(after);
            StateChanged?.Invoke(previous, current);
        }
    }
}
