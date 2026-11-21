using System.Linq;
using NUnit.Framework;

namespace Ankhu.BorrowedDawn.Tests
{
    [TestFixture]
    public class SolutionTests
    {
        [Test]
        public void CanonicalSolution_HasTwentyFiveSteps()
        {
            Assert.That(TestSupport.CanonicalSolution.Length, Is.EqualTo(25));
        }

        [Test]
        public void CanonicalSolution_ThroughSubmit_EveryStepAcceptedAndWins()
        {
            var session = new PuzzleSession();
            for (int i = 0; i < TestSupport.CanonicalSolution.Length; i++)
            {
                string text = TestSupport.CanonicalSolution[i];
                Assert.That(session.State.Won, Is.False, $"won early before step {i + 1}");
                SubmitResult r = session.Submit(text);
                Assert.That(r.Parse.Success, Is.True, $"step {i + 1} '{text}' failed to parse: {r.Parse.Status}");
                Assert.That(r.Execution, Is.Not.Null);
                Assert.That(r.Execution.Outcome, Is.EqualTo(Outcome.Accepted), $"step {i + 1} '{text}' rejected: {r.Message}");
                Assert.That(r.Accepted, Is.True);
                Assert.That(session.StepCount, Is.EqualTo(i + 1));
            }
            Assert.That(session.State.Won, Is.True);
            Assert.That(session.StepCount, Is.EqualTo(25));
            Assert.That(session.UndoDepth, Is.EqualTo(25));
        }

        [Test]
        public void CanonicalSolution_DerivedConditionsAtCheckpoints()
        {
            RoomState s = RoomState.Initial;
            Assert.Multiple(() =>
            {
                Assert.That(s.ElevatorHigh, Is.True, "initial: water east holds the elevator high");
                Assert.That(s.ShadowPresent, Is.False);
                Assert.That(s.SunAtGate, Is.False);
                Assert.That(s.FarBeamBlocked, Is.False);
            });

            // After "flow west": elevator drops to the floor.
            s = TestSupport.AfterCanonicalSteps(1);
            Assert.That(s.ElevatorHigh, Is.False);
            Assert.That(s.RaftRaised, Is.True);

            // After "flow east" on the lift: elevator high again.
            s = TestSupport.AfterCanonicalSteps(3);
            Assert.That(s.Location, Is.EqualTo(Location.Lift));
            Assert.That(s.ElevatorHigh, Is.True);

            // After "bind elevator" then "flow west": binding keeps it high with water west.
            s = TestSupport.AfterCanonicalSteps(7);
            Assert.That(s.Water, Is.EqualTo(WaterSide.West));
            Assert.That(s.IsBound(BindTarget.Elevator), Is.True);
            Assert.That(s.ElevatorHigh, Is.True);
            Assert.That(s.NameDiscovered, Is.True);

            // After "install mirror": sleeping Bearer casts a shadow, no sun yet.
            s = TestSupport.AfterCanonicalSteps(10);
            Assert.Multiple(() =>
            {
                Assert.That(s.Mirror, Is.EqualTo(MirrorPlace.Socket));
                Assert.That(s.NearBeamLit, Is.True);
                Assert.That(s.ShadowPresent, Is.True);
                Assert.That(s.SunAtGate, Is.False);
                Assert.That(s.FarBeamBlocked, Is.False);
            });

            // After "wake bearer" with water west: shadow kept by binding, far beam blocked by raft screen.
            s = TestSupport.AfterCanonicalSteps(12);
            Assert.Multiple(() =>
            {
                Assert.That(s.BearerAwake, Is.True);
                Assert.That(s.IsBound(BindTarget.Shadow), Is.True);
                Assert.That(s.ShadowPresent, Is.True);
                Assert.That(s.SunAtGate, Is.False);
                Assert.That(s.FarBeamBlocked, Is.True);
                Assert.That(s.BindingCount, Is.EqualTo(2));
                Assert.That(s.HasFreeBinding, Is.False);
            });

            // After "flow east": sun reaches the gate.
            s = TestSupport.AfterCanonicalSteps(13);
            Assert.Multiple(() =>
            {
                Assert.That(s.SunAtGate, Is.True);
                Assert.That(s.FarBeamBlocked, Is.False);
                Assert.That(s.ShadowPresent, Is.True);
                Assert.That(s.ElevatorHigh, Is.True);
            });

            // After "release elevator" then "flow west": elevator drops, beam blocked again.
            s = TestSupport.AfterCanonicalSteps(15);
            Assert.Multiple(() =>
            {
                Assert.That(s.IsBound(BindTarget.Elevator), Is.False);
                Assert.That(s.ElevatorHigh, Is.False);
                Assert.That(s.FarBeamBlocked, Is.True);
                Assert.That(s.SunAtGate, Is.False);
            });

            // After "echo bell": sound active, all but location satisfied once the bridge is held.
            s = TestSupport.AfterCanonicalSteps(22);
            Assert.That(s.EchoActive, Is.True);
            Assert.That(s.Location, Is.EqualTo(Location.Landing));
            Assert.That(s.AllGateConditions, Is.True);

            // At the gate before speaking.
            s = TestSupport.AfterCanonicalSteps(24);
            Assert.Multiple(() =>
            {
                Assert.That(s.Location, Is.EqualTo(Location.Gate));
                Assert.That(s.BridgeHeld, Is.True);
                Assert.That(s.SunAtGate, Is.True);
                Assert.That(s.ShadowPresent, Is.True);
                Assert.That(s.EchoActive, Is.True);
                Assert.That(s.NameDiscovered, Is.True);
                Assert.That(s.Won, Is.False);
            });

            s = TestSupport.AfterCanonicalSteps(25);
            Assert.That(s.Won, Is.True);
        }

        [Test]
        public void CanonicalSolution_FinalStateMatchesExpected()
        {
            RoomState final = TestSupport.AfterCanonicalSteps(25);
            RoomState expected = TestSupport.ReadyAtGate().WithWon(true);
            TestSupport.AssertStatesIdentical(final, expected);
        }

        [Test]
        public void CanonicalSolution_EveryIntermediateStateIsReachableAndSolvable()
        {
            for (int i = 0; i <= 25; i++)
            {
                RoomState s = TestSupport.AfterCanonicalSteps(i);
                Assert.That(TestSupport.IsReachable(s), Is.True, $"after {i} steps");
                Assert.That(TestSupport.Space.DistanceToGoal.ContainsKey(s.Pack()), Is.True, $"after {i} steps");
                Assert.That(TestSupport.Space.DistanceToGoal[s.Pack()], Is.LessThanOrEqualTo(25 - i));
            }
        }

        [Test]
        public void CanonicalSolution_IsNoLongerThanNecessaryAccordingToSolver()
        {
            var shortest = TestSupport.Space.ShortestSolution();
            Assert.That(shortest, Is.Not.Null);
            Assert.That(shortest.Count, Is.LessThanOrEqualTo(25));
            Assert.That(TestSupport.CanonicalSolution.Select(t => SpellParser.Parse(t).Command).All(c => c != null), Is.True);
        }
    }
}
