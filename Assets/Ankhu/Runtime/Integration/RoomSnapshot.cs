namespace Ankhu.Integration
{
    public enum WaterSide
    {
        East,
        West
    }

    public enum MirrorLocation
    {
        Raft,
        Carried,
        Socket
    }

    /// <summary>
    /// Immutable view of the Borrowed Dawn logical state, produced by the rules library.
    /// The greybox derives every visual and every traversal collider from this object and
    /// never computes puzzle conditions itself. Derived values (ElevatorHigh, SunReachesGate,
    /// and so on) are filled in by the rules side so there is exactly one implementation of
    /// each rule.
    /// </summary>
    public sealed class RoomSnapshot
    {
        // Primary state
        public WaterSide Water { get; }
        public MirrorLocation Mirror { get; }
        public bool ElevatorBound { get; }
        public bool ShadowBound { get; }
        public bool BridgeBound { get; }
        public int BindingsUsed { get; }
        public int BindingCapacity { get; }
        public bool BearerAwake { get; }
        public bool InscriptionRead { get; }
        public bool BellRepaired { get; }
        public bool BellStruck { get; }
        public bool BellEchoing { get; }
        public bool LadderLowered { get; }
        public bool GateOpen { get; }

        // Derived state, computed by the rules library
        public bool ElevatorHigh { get; }
        public bool RaftHigh { get; }
        public bool LightScreenRaised { get; }
        public bool ShadowPresent { get; }
        public bool SunReachesGate { get; }

        public RoomSnapshot(
            WaterSide water,
            MirrorLocation mirror,
            bool elevatorBound,
            bool shadowBound,
            bool bridgeBound,
            int bindingsUsed,
            int bindingCapacity,
            bool bearerAwake,
            bool inscriptionRead,
            bool bellRepaired,
            bool bellStruck,
            bool bellEchoing,
            bool ladderLowered,
            bool gateOpen,
            bool elevatorHigh,
            bool raftHigh,
            bool lightScreenRaised,
            bool shadowPresent,
            bool sunReachesGate)
        {
            Water = water;
            Mirror = mirror;
            ElevatorBound = elevatorBound;
            ShadowBound = shadowBound;
            BridgeBound = bridgeBound;
            BindingsUsed = bindingsUsed;
            BindingCapacity = bindingCapacity;
            BearerAwake = bearerAwake;
            InscriptionRead = inscriptionRead;
            BellRepaired = bellRepaired;
            BellStruck = bellStruck;
            BellEchoing = bellEchoing;
            LadderLowered = ladderLowered;
            GateOpen = gateOpen;
            ElevatorHigh = elevatorHigh;
            RaftHigh = raftHigh;
            LightScreenRaised = lightScreenRaised;
            ShadowPresent = shadowPresent;
            SunReachesGate = sunReachesGate;
        }

        public override string ToString()
        {
            return $"water={Water} mirror={Mirror} bind[e={ElevatorBound} s={ShadowBound} b={BridgeBound} {BindingsUsed}/{BindingCapacity}] " +
                   $"awake={BearerAwake} read={InscriptionRead} bell[r={BellRepaired} s={BellStruck} e={BellEchoing}] ladder={LadderLowered} gate={GateOpen} " +
                   $"derived[elevHigh={ElevatorHigh} raftHigh={RaftHigh} screen={LightScreenRaised} shadow={ShadowPresent} sun={SunReachesGate}]";
        }
    }
}
