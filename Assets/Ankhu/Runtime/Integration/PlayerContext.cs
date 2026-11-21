namespace Ankhu.Integration
{
    /// <summary>
    /// Stable zone identifiers the greybox reports to the rules library. A zone is the
    /// trigger volume Ankhu currently stands in. These replace the prototype's abstract
    /// location buttons with physical places.
    /// </summary>
    public static class ZoneIds
    {
        /// <summary>Ground tier: sun socket, Bearer, shadow seal, ladder foot, water selector.</summary>
        public const string Floor = "floor";
        /// <summary>Standing on the elevator platform, at either height. The mirror is reached from here.</summary>
        public const string Lift = "lift";
        /// <summary>Upper tier west section: inscription wall and ladder hatch.</summary>
        public const string Gallery = "gallery";
        /// <summary>Upper tier east section: the bell.</summary>
        public const string Landing = "landing";
        /// <summary>On the pendulum bridge span. Only reachable while the bridge is bound.</summary>
        public const string Bridge = "bridge";
        /// <summary>The gate threshold.</summary>
        public const string Gate = "gate";
    }

    /// <summary>
    /// Physical, non-spell interactions the greybox can request. Speech goes through
    /// <see cref="IRoomRulesAdapter.Speak"/>. These go through <see cref="IRoomRulesAdapter.Interact"/>.
    /// </summary>
    public static class InteractionIds
    {
        public const string ReadInscription = "read_inscription";
        public const string TakeMirror = "take_mirror";
        public const string InstallMirror = "install_mirror";
        public const string StrikeBell = "strike_bell";
        public const string LowerLadder = "lower_ladder";
    }

    /// <summary>
    /// Where Ankhu is when a command is issued. The rules library uses this for reach and
    /// safety checks (for example refusing to release the bridge while standing on it).
    /// </summary>
    public readonly struct PlayerContext
    {
        public string ZoneId { get; }
        public bool OnElevator { get; }
        public bool OnBridge { get; }

        public PlayerContext(string zoneId, bool onElevator, bool onBridge)
        {
            ZoneId = zoneId;
            OnElevator = onElevator;
            OnBridge = onBridge;
        }

        public override string ToString() => $"zone={ZoneId} onElevator={OnElevator} onBridge={OnBridge}";
    }
}
