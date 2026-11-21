using System;
using System.Collections.Generic;
using System.Linq;
using Ankhu.BorrowedDawn.Analysis;
using NUnit.Framework;

namespace Ankhu.BorrowedDawn.Tests
{
    /// <summary>Shared fixtures: the canonical solution, a cached state space and state builders.</summary>
    internal static class TestSupport
    {
        /// <summary>The 25-step solution from the prototype help text.</summary>
        public static readonly string[] CanonicalSolution =
        {
            "flow west",
            "board elevator",
            "flow east",
            "enter gallery",
            "read inscription",
            "bind elevator",
            "flow west",
            "take mirror",
            "descend to floor",
            "install mirror",
            "bind shadow",
            "wake bearer",
            "flow east",
            "release elevator",
            "flow west",
            "board elevator",
            "flow east",
            "enter gallery",
            "go to landing",
            "recall bell",
            "strike bell",
            "echo bell",
            "bind bridge",
            "cross bridge",
            "he who returns the morning",
        };

        private static readonly Lazy<StateSpace> LazySpace = new Lazy<StateSpace>(() => StateSpace.Explore());

        /// <summary>Full reachable graph from <see cref="RoomState.Initial"/>, computed once.</summary>
        public static StateSpace Space => LazySpace.Value;

        public static IEnumerable<RoomState> ReachableStates => Space.States.Values;

        /// <summary>Applies the first <paramref name="count"/> canonical steps through the rules, asserting each is accepted.</summary>
        public static RoomState AfterCanonicalSteps(int count)
        {
            RoomState s = RoomState.Initial;
            for (int i = 0; i < count; i++)
            {
                s = MustApply(s, CanonicalSolution[i]);
            }
            return s;
        }

        public static RoomState MustApply(RoomState s, string text)
        {
            ParseResult p = SpellParser.Parse(text);
            Assert.That(p.Success, Is.True, $"'{text}' should parse but got {p.Status}");
            return MustApply(s, p.Command);
        }

        public static RoomState MustApply(RoomState s, Command c)
        {
            CommandResult r = RoomRules.Apply(s, c);
            Assert.That(r.Outcome, Is.EqualTo(Outcome.Accepted), $"'{c}' should be accepted from [{s}] but got {r.Outcome}: {r.Message}");
            return r.State;
        }

        /// <summary>Builds a state with named arguments; defaults match the initial state.</summary>
        public static RoomState Make(
            WaterSide water = WaterSide.East,
            Location location = Location.Floor,
            BindingSet bindings = BindingSet.None,
            MirrorPlace mirror = MirrorPlace.Raft,
            bool awake = false,
            bool read = false,
            bool bell = false,
            bool struck = false,
            bool echo = false,
            bool won = false) =>
            new RoomState(water, location, bindings, mirror, awake, read, bell, struck, echo, won);

        /// <summary>Standing at the gate with every gate condition satisfied (shadow + bridge bound).</summary>
        public static RoomState ReadyAtGate() => Make(
            water: WaterSide.East,
            location: Location.Gate,
            bindings: BindingSet.Shadow | BindingSet.Bridge,
            mirror: MirrorPlace.Socket,
            awake: true,
            read: true,
            bell: true,
            struck: true,
            echo: true);

        public static void AssertStatesIdentical(RoomState actual, RoomState expected)
        {
            Assert.That(actual.Pack(), Is.EqualTo(expected.Pack()), $"expected [{expected}] but was [{actual}]");
            Assert.That(actual, Is.EqualTo(expected));
        }

        public static bool IsReachable(RoomState s) => Space.States.ContainsKey(s.Pack());
    }
}
