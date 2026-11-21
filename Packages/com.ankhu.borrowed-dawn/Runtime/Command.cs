using System;

namespace Ankhu.BorrowedDawn
{
    public enum CommandKind
    {
        Flow,
        Bind,
        Release,
        WakeBearer,
        SleepBearer,
        RecallBell,
        StrikeBell,
        EchoBell,
        TakeMirror,
        InstallMirror,
        ReadInscription,
        SpeakName,
        MoveTo,
        CrossBridge,
        LowerLadder,
    }

    /// <summary>
    /// A validated, parser-independent intent. The world only changes through <see cref="RoomRules.Apply(RoomState, Command, RuleProfile)"/>.
    /// Build instances with the static factories so argument shapes always match the kind.
    /// </summary>
    public sealed class Command : IEquatable<Command>
    {
        public CommandKind Kind { get; }
        public WaterSide Direction { get; }
        public BindTarget Target { get; }
        public Location Destination { get; }

        private Command(CommandKind kind, WaterSide direction = default, BindTarget target = default, Location destination = default)
        {
            Kind = kind;
            Direction = direction;
            Target = target;
            Destination = destination;
        }

        public static Command Flow(WaterSide direction) => new Command(CommandKind.Flow, direction: direction);
        public static Command Bind(BindTarget target) => new Command(CommandKind.Bind, target: target);
        public static Command Release(BindTarget target) => new Command(CommandKind.Release, target: target);
        public static Command MoveTo(Location destination) => new Command(CommandKind.MoveTo, destination: destination);
        public static readonly Command WakeBearer = new Command(CommandKind.WakeBearer);
        public static readonly Command SleepBearer = new Command(CommandKind.SleepBearer);
        public static readonly Command RecallBell = new Command(CommandKind.RecallBell);
        public static readonly Command StrikeBell = new Command(CommandKind.StrikeBell);
        public static readonly Command EchoBell = new Command(CommandKind.EchoBell);
        public static readonly Command TakeMirror = new Command(CommandKind.TakeMirror);
        public static readonly Command InstallMirror = new Command(CommandKind.InstallMirror);
        public static readonly Command ReadInscription = new Command(CommandKind.ReadInscription);
        public static readonly Command SpeakName = new Command(CommandKind.SpeakName);
        public static readonly Command CrossBridge = new Command(CommandKind.CrossBridge);
        public static readonly Command LowerLadder = new Command(CommandKind.LowerLadder);

        public bool Equals(Command other)
        {
            if (other is null || other.Kind != Kind)
            {
                return false;
            }
            switch (Kind)
            {
                case CommandKind.Flow: return Direction == other.Direction;
                case CommandKind.Bind:
                case CommandKind.Release: return Target == other.Target;
                case CommandKind.MoveTo: return Destination == other.Destination;
                default: return true;
            }
        }

        public override bool Equals(object obj) => obj is Command c && Equals(c);

        public override int GetHashCode()
        {
            switch (Kind)
            {
                case CommandKind.Flow: return ((int)Kind << 8) | (int)Direction;
                case CommandKind.Bind:
                case CommandKind.Release: return ((int)Kind << 8) | (int)Target;
                case CommandKind.MoveTo: return ((int)Kind << 8) | (int)Destination;
                default: return (int)Kind << 8;
            }
        }

        /// <summary>Canonical spoken form. Parsing this string returns an equal command.</summary>
        public override string ToString()
        {
            switch (Kind)
            {
                case CommandKind.Flow: return "flow " + Direction.ToString().ToLowerInvariant();
                case CommandKind.Bind: return "bind " + Target.ToString().ToLowerInvariant();
                case CommandKind.Release: return "release " + Target.ToString().ToLowerInvariant();
                case CommandKind.WakeBearer: return "wake bearer";
                case CommandKind.SleepBearer: return "sleep bearer";
                case CommandKind.RecallBell: return "recall bell";
                case CommandKind.StrikeBell: return "strike bell";
                case CommandKind.EchoBell: return "echo bell";
                case CommandKind.TakeMirror: return "take mirror";
                case CommandKind.InstallMirror: return "install mirror";
                case CommandKind.ReadInscription: return "read inscription";
                case CommandKind.SpeakName: return "he who returns the morning";
                case CommandKind.MoveTo: return "go to " + Destination.ToString().ToLowerInvariant();
                case CommandKind.CrossBridge: return "cross bridge";
                case CommandKind.LowerLadder: return "lower ladder";
                default: return Kind.ToString();
            }
        }
    }
}
