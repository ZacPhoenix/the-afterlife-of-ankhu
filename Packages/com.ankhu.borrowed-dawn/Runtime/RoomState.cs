using System;
using System.Text;

namespace Ankhu.BorrowedDawn
{
    /// <summary>
    /// Immutable logical state of the Borrowed Dawn room. Presentation reads this and the derived
    /// properties. Nothing here knows about animation, timing or Unity.
    /// </summary>
    public sealed class RoomState : IEquatable<RoomState>
    {
        /// <summary>Maximum number of simultaneous bindings.</summary>
        public const int BindingCapacity = 2;

        public WaterSide Water { get; }
        public Location Location { get; }
        public BindingSet Bindings { get; }
        public MirrorPlace Mirror { get; }
        public bool BearerAwake { get; }
        public bool NameDiscovered { get; }
        public bool BellRepaired { get; }
        public bool BellStruck { get; }
        public bool EchoActive { get; }
        public bool Won { get; }
        /// <summary>Gallery ladder hatch is down (Greybox profile only). Once down it stays down.</summary>
        public bool LadderLowered { get; }

        public RoomState(
            WaterSide water,
            Location location,
            BindingSet bindings,
            MirrorPlace mirror,
            bool bearerAwake,
            bool nameDiscovered,
            bool bellRepaired,
            bool bellStruck,
            bool echoActive,
            bool won,
            bool ladderLowered = false)
        {
            if (bindings.Count() > BindingCapacity)
            {
                throw new ArgumentException("Binding count exceeds capacity.", nameof(bindings));
            }
            if ((bindings & ~(BindingSet.Elevator | BindingSet.Shadow | BindingSet.Bridge)) != 0)
            {
                throw new ArgumentException("Unknown binding flag.", nameof(bindings));
            }
            Water = water;
            Location = location;
            Bindings = bindings;
            Mirror = mirror;
            BearerAwake = bearerAwake;
            NameDiscovered = nameDiscovered;
            BellRepaired = bellRepaired;
            BellStruck = bellStruck;
            EchoActive = echoActive;
            Won = won;
            LadderLowered = ladderLowered;
        }

        /// <summary>The prototype's starting state: water east, Ankhu on the floor, nothing bound.</summary>
        public static RoomState Initial { get; } = new RoomState(
            WaterSide.East, Location.Floor, BindingSet.None, MirrorPlace.Raft,
            bearerAwake: false, nameDiscovered: false, bellRepaired: false,
            bellStruck: false, echoActive: false, won: false);

        // ---------------------------------------------------------------- derived conditions

        public bool IsBound(BindTarget target) => Bindings.Has(target);

        public int BindingCount => Bindings.Count();

        public bool HasFreeBinding => BindingCount < BindingCapacity;

        /// <summary>The elevator is high when water is east or a binding holds it.</summary>
        public bool ElevatorHigh => Water == WaterSide.East || IsBound(BindTarget.Elevator);

        /// <summary>The raft (and its light screen) rises when water is west.</summary>
        public bool RaftRaised => Water == WaterSide.West;

        /// <summary>The light screen rides on the raft, so it is raised exactly when the raft is.</summary>
        public bool LightScreenRaised => RaftRaised;

        /// <summary>Light reaches the Bearer once the mirror sits in the socket.</summary>
        public bool NearBeamLit => Mirror == MirrorPlace.Socket;

        /// <summary>A shadow lies on the seal while the Bearer sleeps in the beam, or while the shadow is bound.</summary>
        public bool ShadowPresent => Mirror == MirrorPlace.Socket && (!BearerAwake || IsBound(BindTarget.Shadow));

        /// <summary>Sunlight reaches the gate when the mirror is installed, the Bearer has stepped aside and the raft screen is down.</summary>
        public bool SunAtGate => Mirror == MirrorPlace.Socket && BearerAwake && Water == WaterSide.East;

        /// <summary>The Bearer is out of the way but the raised raft screen blocks the far beam.</summary>
        public bool FarBeamBlocked => Mirror == MirrorPlace.Socket && BearerAwake && !SunAtGate;

        /// <summary>The upper walkway between gallery and landing is complete.</summary>
        public bool UpperWalkwayComplete => ElevatorHigh;

        /// <summary>The pendulum bridge is held horizontal.</summary>
        public bool BridgeHeld => IsBound(BindTarget.Bridge);

        /// <summary>All four gate conditions, independent of where Ankhu stands.</summary>
        public bool AllGateConditions => SunAtGate && ShadowPresent && EchoActive && NameDiscovered;

        // ---------------------------------------------------------------- copy helpers

        public RoomState WithWater(WaterSide v) => new RoomState(v, Location, Bindings, Mirror, BearerAwake, NameDiscovered, BellRepaired, BellStruck, EchoActive, Won, LadderLowered);
        public RoomState WithLocation(Location v) => new RoomState(Water, v, Bindings, Mirror, BearerAwake, NameDiscovered, BellRepaired, BellStruck, EchoActive, Won, LadderLowered);
        public RoomState WithBindings(BindingSet v) => new RoomState(Water, Location, v, Mirror, BearerAwake, NameDiscovered, BellRepaired, BellStruck, EchoActive, Won, LadderLowered);
        public RoomState WithMirror(MirrorPlace v) => new RoomState(Water, Location, Bindings, v, BearerAwake, NameDiscovered, BellRepaired, BellStruck, EchoActive, Won, LadderLowered);
        public RoomState WithBearerAwake(bool v) => new RoomState(Water, Location, Bindings, Mirror, v, NameDiscovered, BellRepaired, BellStruck, EchoActive, Won, LadderLowered);
        public RoomState WithNameDiscovered(bool v) => new RoomState(Water, Location, Bindings, Mirror, BearerAwake, v, BellRepaired, BellStruck, EchoActive, Won, LadderLowered);
        public RoomState WithBellRepaired(bool v) => new RoomState(Water, Location, Bindings, Mirror, BearerAwake, NameDiscovered, v, BellStruck, EchoActive, Won, LadderLowered);
        public RoomState WithBellStruck(bool v) => new RoomState(Water, Location, Bindings, Mirror, BearerAwake, NameDiscovered, BellRepaired, v, EchoActive, Won, LadderLowered);
        public RoomState WithEchoActive(bool v) => new RoomState(Water, Location, Bindings, Mirror, BearerAwake, NameDiscovered, BellRepaired, BellStruck, v, Won, LadderLowered);
        public RoomState WithLadderLowered(bool v) => new RoomState(Water, Location, Bindings, Mirror, BearerAwake, NameDiscovered, BellRepaired, BellStruck, EchoActive, Won, v);
        public RoomState WithWon(bool v) => new RoomState(Water, Location, Bindings, Mirror, BearerAwake, NameDiscovered, BellRepaired, BellStruck, EchoActive, v, LadderLowered);

        // ---------------------------------------------------------------- identity

        /// <summary>
        /// Packs the state into 16 bits. Unique per distinct state, so it doubles as a save key and a
        /// search key. Layout (LSB first): water 1, location 3, bindings 3, mirror 2, then seven flags (ladder last).
        /// </summary>
        public int Pack()
        {
            int v = (int)Water;
            v |= (int)Location << 1;
            v |= (int)Bindings << 4;
            v |= (int)Mirror << 7;
            v |= (BearerAwake ? 1 : 0) << 9;
            v |= (NameDiscovered ? 1 : 0) << 10;
            v |= (BellRepaired ? 1 : 0) << 11;
            v |= (BellStruck ? 1 : 0) << 12;
            v |= (EchoActive ? 1 : 0) << 13;
            v |= (Won ? 1 : 0) << 14;
            v |= (LadderLowered ? 1 : 0) << 15;
            return v;
        }

        /// <summary>Inverse of <see cref="Pack"/>. Throws on values that do not describe a legal state.</summary>
        public static RoomState Unpack(int v)
        {
            if (v < 0 || v >= 1 << 16)
            {
                throw new ArgumentOutOfRangeException(nameof(v));
            }
            int loc = (v >> 1) & 7;
            int mirror = (v >> 7) & 3;
            if (loc > (int)Location.Bridge || mirror > (int)MirrorPlace.Socket)
            {
                throw new ArgumentOutOfRangeException(nameof(v));
            }
            return new RoomState(
                (WaterSide)(v & 1),
                (Location)loc,
                (BindingSet)((v >> 4) & 7),
                (MirrorPlace)mirror,
                ((v >> 9) & 1) != 0,
                ((v >> 10) & 1) != 0,
                ((v >> 11) & 1) != 0,
                ((v >> 12) & 1) != 0,
                ((v >> 13) & 1) != 0,
                ((v >> 14) & 1) != 0,
                ((v >> 15) & 1) != 0);
        }

        public bool Equals(RoomState other) => other is object && Pack() == other.Pack();

        public override bool Equals(object obj) => obj is RoomState s && Equals(s);

        public override int GetHashCode() => Pack();

        public static bool operator ==(RoomState a, RoomState b) => a is null ? b is null : a.Equals(b);

        public static bool operator !=(RoomState a, RoomState b) => !(a == b);

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("water=").Append(Water)
              .Append(" loc=").Append(Location)
              .Append(" bind=").Append(Bindings)
              .Append(" mirror=").Append(Mirror)
              .Append(" awake=").Append(BearerAwake)
              .Append(" read=").Append(NameDiscovered)
              .Append(" bell=").Append(BellRepaired)
              .Append(" struck=").Append(BellStruck)
              .Append(" echo=").Append(EchoActive)
              .Append(" won=").Append(Won)
              .Append(" ladder=").Append(LadderLowered);
            return sb.ToString();
        }
    }
}
