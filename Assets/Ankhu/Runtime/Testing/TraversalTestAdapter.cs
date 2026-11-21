using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ankhu.Integration;

namespace Ankhu.Testing
{
    /// <summary>
    /// Stand-in adapter for traversal testing. It contains no puzzle rules. Speech and
    /// interactions are refused with <see cref="CommandOutcome.NotConnected"/>. A designer sets
    /// every snapshot field by hand (including derived ones) through the public setters or the
    /// F1 debug panel. Undo, Reset, SaveState and LoadState work on snapshots this adapter owns.
    /// Engine free on purpose, like a real rules library.
    /// </summary>
    public sealed class TraversalTestAdapter : IRoomRulesAdapter
    {
        public const string SaveHeader = "ankhu.borrowed_dawn.traversal=1";
        public const int MaxUndoDepth = 256;

        private static readonly string[] Keys =
        {
            "water", "mirror", "elevator_bound", "shadow_bound", "bridge_bound", "bindings_used",
            "binding_capacity", "bearer_awake", "inscription_read", "bell_repaired", "bell_struck",
            "bell_echoing", "ladder_lowered", "gate_open", "elevator_high", "raft_high",
            "light_screen_raised", "shadow_present", "sun_reaches_gate"
        };

        private readonly List<RoomSnapshot> undoStack = new List<RoomSnapshot>();

        public RoomSnapshot Current { get; private set; }

        public event Action<RoomSnapshot, RoomSnapshot> StateChanged;

        public int UndoDepth => undoStack.Count;

        public TraversalTestAdapter()
        {
            Current = CreateInitialSnapshot();
        }

        /// <summary>Prototype start: water east, mirror on the raft, nothing bound, capacity 2, ladder stowed, elevator high.</summary>
        public static RoomSnapshot CreateInitialSnapshot()
        {
            return new RoomSnapshot(
                water: WaterSide.East,
                mirror: MirrorLocation.Raft,
                elevatorBound: false,
                shadowBound: false,
                bridgeBound: false,
                bindingsUsed: 0,
                bindingCapacity: 2,
                bearerAwake: false,
                inscriptionRead: false,
                bellRepaired: false,
                bellStruck: false,
                bellEchoing: false,
                ladderLowered: false,
                gateOpen: false,
                elevatorHigh: true,
                raftHigh: false,
                lightScreenRaised: false,
                shadowPresent: false,
                sunReachesGate: false);
        }

        // IRoomRulesAdapter

        public CommandResult Speak(string rawText, PlayerContext context)
        {
            return new CommandResult(CommandOutcome.NotConnected, "Rules library not connected. Heard: " + (rawText ?? string.Empty));
        }

        public CommandResult Interact(string interactionId, PlayerContext context)
        {
            return new CommandResult(CommandOutcome.NotConnected,
                "Rules library not connected. Requested: " + (interactionId ?? string.Empty) + " in zone " + (context.ZoneId ?? "none"));
        }

        public CommandResult Preview(string interactionId, PlayerContext context)
        {
            return new CommandResult(CommandOutcome.NotConnected, "Rules library not connected.");
        }

        public CommandResult Undo()
        {
            if (undoStack.Count == 0)
            {
                return new CommandResult(CommandOutcome.NoChange, "Nothing to undo.");
            }
            RoomSnapshot previous = Current;
            int last = undoStack.Count - 1;
            Current = undoStack[last];
            undoStack.RemoveAt(last);
            StateChanged?.Invoke(previous, Current);
            return new CommandResult(CommandOutcome.Success, "Restored the previous test state.");
        }

        public void Reset()
        {
            RoomSnapshot previous = Current;
            undoStack.Clear();
            Current = CreateInitialSnapshot();
            StateChanged?.Invoke(previous, Current);
        }

        public string Hint(PlayerContext context)
        {
            return "Traversal test mode has no hints. Press F1 to set the room state by hand.";
        }

        public string SaveState()
        {
            RoomSnapshot s = Current;
            string[] values =
            {
                s.Water.ToString(), s.Mirror.ToString(), B(s.ElevatorBound), B(s.ShadowBound), B(s.BridgeBound),
                s.BindingsUsed.ToString(CultureInfo.InvariantCulture), s.BindingCapacity.ToString(CultureInfo.InvariantCulture),
                B(s.BearerAwake), B(s.InscriptionRead), B(s.BellRepaired), B(s.BellStruck), B(s.BellEchoing),
                B(s.LadderLowered), B(s.GateOpen), B(s.ElevatorHigh), B(s.RaftHigh), B(s.LightScreenRaised),
                B(s.ShadowPresent), B(s.SunReachesGate)
            };
            StringBuilder sb = new StringBuilder();
            sb.Append(SaveHeader).Append('\n');
            for (int i = 0; i < Keys.Length; i++)
            {
                sb.Append(Keys[i]).Append('=').Append(values[i]).Append('\n');
            }
            return sb.ToString();
        }

        public bool LoadState(string payload)
        {
            if (!TryParse(payload, out RoomSnapshot loaded))
            {
                return false;
            }
            RoomSnapshot previous = Current;
            undoStack.Clear();
            Current = loaded;
            StateChanged?.Invoke(previous, Current);
            return true;
        }

        // Test setters. Each returns true and raises StateChanged when the value changed.

        public bool SetWater(WaterSide value) { Fields f = new Fields(Current); f.Water = value; return Commit(f); }
        public bool SetMirror(MirrorLocation value) { Fields f = new Fields(Current); f.Mirror = value; return Commit(f); }
        public bool SetElevatorBound(bool value) { Fields f = new Fields(Current); f.ElevatorBound = value; return Commit(f); }
        public bool SetShadowBound(bool value) { Fields f = new Fields(Current); f.ShadowBound = value; return Commit(f); }
        public bool SetBridgeBound(bool value) { Fields f = new Fields(Current); f.BridgeBound = value; return Commit(f); }
        public bool SetBindingsUsed(int value) { Fields f = new Fields(Current); f.BindingsUsed = value; return Commit(f); }
        public bool SetBindingCapacity(int value) { Fields f = new Fields(Current); f.BindingCapacity = value; return Commit(f); }
        public bool SetBearerAwake(bool value) { Fields f = new Fields(Current); f.BearerAwake = value; return Commit(f); }
        public bool SetInscriptionRead(bool value) { Fields f = new Fields(Current); f.InscriptionRead = value; return Commit(f); }
        public bool SetBellRepaired(bool value) { Fields f = new Fields(Current); f.BellRepaired = value; return Commit(f); }
        public bool SetBellStruck(bool value) { Fields f = new Fields(Current); f.BellStruck = value; return Commit(f); }
        public bool SetBellEchoing(bool value) { Fields f = new Fields(Current); f.BellEchoing = value; return Commit(f); }
        public bool SetLadderLowered(bool value) { Fields f = new Fields(Current); f.LadderLowered = value; return Commit(f); }
        public bool SetGateOpen(bool value) { Fields f = new Fields(Current); f.GateOpen = value; return Commit(f); }
        public bool SetElevatorHigh(bool value) { Fields f = new Fields(Current); f.ElevatorHigh = value; return Commit(f); }
        public bool SetRaftHigh(bool value) { Fields f = new Fields(Current); f.RaftHigh = value; return Commit(f); }
        public bool SetLightScreenRaised(bool value) { Fields f = new Fields(Current); f.LightScreenRaised = value; return Commit(f); }
        public bool SetShadowPresent(bool value) { Fields f = new Fields(Current); f.ShadowPresent = value; return Commit(f); }
        public bool SetSunReachesGate(bool value) { Fields f = new Fields(Current); f.SunReachesGate = value; return Commit(f); }

        /// <summary>Replaces the whole snapshot in one undoable step.</summary>
        public bool Replace(RoomSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            return Commit(new Fields(snapshot));
        }

        private bool Commit(Fields next)
        {
            Fields current = new Fields(Current);
            if (next.SameAs(current))
            {
                return false;
            }
            RoomSnapshot previous = Current;
            undoStack.Add(previous);
            if (undoStack.Count > MaxUndoDepth)
            {
                undoStack.RemoveAt(0);
            }
            Current = next.ToSnapshot();
            StateChanged?.Invoke(previous, Current);
            return true;
        }

        private static string B(bool value)
        {
            return value ? "true" : "false";
        }

        private static bool TryParse(string payload, out RoomSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrEmpty(payload))
            {
                return false;
            }
            string[] lines = payload.Split('\n');
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            bool headerSeen = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (!headerSeen)
                {
                    if (line != SaveHeader)
                    {
                        return false;
                    }
                    headerSeen = true;
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    return false;
                }
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (values.ContainsKey(key))
                {
                    return false;
                }
                values[key] = value;
            }
            if (!headerSeen)
            {
                return false;
            }
            for (int i = 0; i < Keys.Length; i++)
            {
                if (!values.ContainsKey(Keys[i]))
                {
                    return false;
                }
            }

            Fields f = default;
            if (!TryEnum(values["water"], out f.Water)) return false;
            if (!TryEnum(values["mirror"], out f.Mirror)) return false;
            if (!TryBool(values["elevator_bound"], out f.ElevatorBound)) return false;
            if (!TryBool(values["shadow_bound"], out f.ShadowBound)) return false;
            if (!TryBool(values["bridge_bound"], out f.BridgeBound)) return false;
            if (!int.TryParse(values["bindings_used"], NumberStyles.Integer, CultureInfo.InvariantCulture, out f.BindingsUsed)) return false;
            if (!int.TryParse(values["binding_capacity"], NumberStyles.Integer, CultureInfo.InvariantCulture, out f.BindingCapacity)) return false;
            if (!TryBool(values["bearer_awake"], out f.BearerAwake)) return false;
            if (!TryBool(values["inscription_read"], out f.InscriptionRead)) return false;
            if (!TryBool(values["bell_repaired"], out f.BellRepaired)) return false;
            if (!TryBool(values["bell_struck"], out f.BellStruck)) return false;
            if (!TryBool(values["bell_echoing"], out f.BellEchoing)) return false;
            if (!TryBool(values["ladder_lowered"], out f.LadderLowered)) return false;
            if (!TryBool(values["gate_open"], out f.GateOpen)) return false;
            if (!TryBool(values["elevator_high"], out f.ElevatorHigh)) return false;
            if (!TryBool(values["raft_high"], out f.RaftHigh)) return false;
            if (!TryBool(values["light_screen_raised"], out f.LightScreenRaised)) return false;
            if (!TryBool(values["shadow_present"], out f.ShadowPresent)) return false;
            if (!TryBool(values["sun_reaches_gate"], out f.SunReachesGate)) return false;
            snapshot = f.ToSnapshot();
            return true;
        }

        private static bool TryBool(string text, out bool value)
        {
            if (text == "true")
            {
                value = true;
                return true;
            }
            if (text == "false")
            {
                value = false;
                return true;
            }
            value = false;
            return false;
        }

        private static bool TryEnum<T>(string text, out T value) where T : struct, Enum
        {
            // Names only. Enum.TryParse alone would also accept numeric strings.
            if (Enum.TryParse(text, false, out value) && Enum.IsDefined(typeof(T), value) && text == value.ToString())
            {
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>Mutable copy of every snapshot field.</summary>
        private struct Fields
        {
            public WaterSide Water;
            public MirrorLocation Mirror;
            public bool ElevatorBound;
            public bool ShadowBound;
            public bool BridgeBound;
            public int BindingsUsed;
            public int BindingCapacity;
            public bool BearerAwake;
            public bool InscriptionRead;
            public bool BellRepaired;
            public bool BellStruck;
            public bool BellEchoing;
            public bool LadderLowered;
            public bool GateOpen;
            public bool ElevatorHigh;
            public bool RaftHigh;
            public bool LightScreenRaised;
            public bool ShadowPresent;
            public bool SunReachesGate;

            public Fields(RoomSnapshot s)
            {
                Water = s.Water;
                Mirror = s.Mirror;
                ElevatorBound = s.ElevatorBound;
                ShadowBound = s.ShadowBound;
                BridgeBound = s.BridgeBound;
                BindingsUsed = s.BindingsUsed;
                BindingCapacity = s.BindingCapacity;
                BearerAwake = s.BearerAwake;
                InscriptionRead = s.InscriptionRead;
                BellRepaired = s.BellRepaired;
                BellStruck = s.BellStruck;
                BellEchoing = s.BellEchoing;
                LadderLowered = s.LadderLowered;
                GateOpen = s.GateOpen;
                ElevatorHigh = s.ElevatorHigh;
                RaftHigh = s.RaftHigh;
                LightScreenRaised = s.LightScreenRaised;
                ShadowPresent = s.ShadowPresent;
                SunReachesGate = s.SunReachesGate;
            }

            public bool SameAs(Fields o)
            {
                return Water == o.Water && Mirror == o.Mirror && ElevatorBound == o.ElevatorBound &&
                       ShadowBound == o.ShadowBound && BridgeBound == o.BridgeBound &&
                       BindingsUsed == o.BindingsUsed && BindingCapacity == o.BindingCapacity &&
                       BearerAwake == o.BearerAwake && InscriptionRead == o.InscriptionRead &&
                       BellRepaired == o.BellRepaired && BellStruck == o.BellStruck && BellEchoing == o.BellEchoing &&
                       LadderLowered == o.LadderLowered && GateOpen == o.GateOpen && ElevatorHigh == o.ElevatorHigh &&
                       RaftHigh == o.RaftHigh && LightScreenRaised == o.LightScreenRaised &&
                       ShadowPresent == o.ShadowPresent && SunReachesGate == o.SunReachesGate;
            }

            public RoomSnapshot ToSnapshot()
            {
                return new RoomSnapshot(Water, Mirror, ElevatorBound, ShadowBound, BridgeBound, BindingsUsed,
                    BindingCapacity, BearerAwake, InscriptionRead, BellRepaired, BellStruck, BellEchoing,
                    LadderLowered, GateOpen, ElevatorHigh, RaftHigh, LightScreenRaised, ShadowPresent, SunReachesGate);
            }
        }
    }
}
