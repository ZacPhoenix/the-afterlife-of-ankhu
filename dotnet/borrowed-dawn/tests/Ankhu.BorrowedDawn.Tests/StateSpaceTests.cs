using System.Linq;
using Ankhu.BorrowedDawn.Analysis;
using NUnit.Framework;
using static Ankhu.BorrowedDawn.Tests.TestSupport;

namespace Ankhu.BorrowedDawn.Tests
{
    [TestFixture]
    public class StateSpaceTests
    {
        [Test]
        public void GoalIsReachableFromInitial()
        {
            Assert.That(Space.Start, Is.SameAs(RoomState.Initial));
            Assert.That(Space.GoalReachable, Is.True);
            Assert.That(Space.States.Values.Any(s => s.Won), Is.True);
        }

        [Test]
        public void ShortestSolution_ReplaysToWonThroughRules()
        {
            var path = Space.ShortestSolution();
            Assert.That(path, Is.Not.Null.And.Not.Empty);
            RoomState s = RoomState.Initial;
            for (int i = 0; i < path.Count; i++)
            {
                Assert.That(s.Won, Is.False, $"won before step {i}");
                s = MustApply(s, path[i]);
            }
            Assert.That(s.Won, Is.True);
            Assert.That(path.Count, Is.EqualTo(Space.DistanceToGoal[RoomState.Initial.Pack()]));
            Assert.That(path.Count, Is.LessThanOrEqualTo(CanonicalSolution.Length));
        }

        [Test]
        public void ShortestSolution_ReplaysThroughSessionAsText()
        {
            var session = new PuzzleSession();
            foreach (Command c in Space.ShortestSolution())
            {
                Assert.That(session.Submit(c.ToString()).Accepted, Is.True, c.ToString());
            }
            Assert.That(session.State.Won, Is.True);
        }

        [Test]
        public void NoSoftlocks()
        {
            var softlocks = Space.Softlocks.ToList();
            Assert.That(softlocks, Is.Empty, string.Join("\n", softlocks.Take(10)));
        }

        [Test]
        public void NoDeadEnds()
        {
            Assert.That(Space.DeadEnds, Is.Empty);
        }

        [Test]
        public void EveryNonWonStateHasDistanceToGoal()
        {
            foreach (var kv in Space.States)
            {
                Assert.That(Space.DistanceToGoal.ContainsKey(kv.Key), Is.True, kv.Value.ToString());
                Assert.That(Space.DistanceToGoal[kv.Key] == 0, Is.EqualTo(kv.Value.Won));
            }
        }

        [Test]
        public void PackUnpack_RoundTripsForAllReachableStates()
        {
            foreach (var kv in Space.States)
            {
                RoomState s = kv.Value;
                int packed = s.Pack();
                Assert.That(packed, Is.EqualTo(kv.Key));
                Assert.That(packed, Is.InRange(0, (1 << 15) - 1));
                RoomState u = RoomState.Unpack(packed);
                Assert.That(u, Is.EqualTo(s));
                Assert.That(u.GetHashCode(), Is.EqualTo(s.GetHashCode()));
                Assert.Multiple(() =>
                {
                    Assert.That(u.Water, Is.EqualTo(s.Water));
                    Assert.That(u.Location, Is.EqualTo(s.Location));
                    Assert.That(u.Bindings, Is.EqualTo(s.Bindings));
                    Assert.That(u.Mirror, Is.EqualTo(s.Mirror));
                    Assert.That(u.BearerAwake, Is.EqualTo(s.BearerAwake));
                    Assert.That(u.NameDiscovered, Is.EqualTo(s.NameDiscovered));
                    Assert.That(u.BellRepaired, Is.EqualTo(s.BellRepaired));
                    Assert.That(u.BellStruck, Is.EqualTo(s.BellStruck));
                    Assert.That(u.EchoActive, Is.EqualTo(s.EchoActive));
                    Assert.That(u.Won, Is.EqualTo(s.Won));
                });
            }
        }

        [Test]
        public void Pack_IsInjectiveAcrossReachableStates()
        {
            var strings = Space.States.Values.Select(s => s.ToString()).ToList();
            Assert.That(strings.Distinct().Count(), Is.EqualTo(Space.States.Count));
        }

        [TestCase(-1)]
        [TestCase(1 << 16)]
        public void Unpack_OutOfRange_Throws(int v)
        {
            Assert.That(() => RoomState.Unpack(v), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void Unpack_IllegalLocationOrMirror_Throws()
        {
            Assert.That(() => RoomState.Unpack(6 << 1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => RoomState.Unpack(3 << 7), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => RoomState.Unpack(7 << 4), Throws.ArgumentException);
        }

        [Test]
        public void Edges_AreConsistentWithRules()
        {
            foreach (Edge e in Space.Edges)
            {
                CommandResult r = RoomRules.Apply(Space.States[e.From], e.Command);
                Assert.That(r.Accepted, Is.True);
                Assert.That(r.State.Pack(), Is.EqualTo(e.To));
            }
        }

        [Test]
        public void PathTo_EveryState_ReplaysToThatStateAtBfsDepth()
        {
            foreach (var kv in Space.States)
            {
                var path = Space.PathTo(kv.Key);
                Assert.That(path.Count, Is.EqualTo(Space.DepthFromStart[kv.Key]));
                RoomState s = RoomState.Initial;
                foreach (Command c in path) s = MustApply(s, c);
                Assert.That(s, Is.EqualTo(kv.Value));
            }
        }

        [Test]
        public void CountShortestSolutions_IsPositive()
        {
            Assert.That(Space.CountShortestSolutions(), Is.GreaterThan(0));
        }

        [Test]
        public void ShortestSolutionWhere_Unrestricted_MatchesShortestLength()
        {
            var path = StateSpace.ShortestSolutionWhere((_, __) => true);
            Assert.That(path, Is.Not.Null);
            Assert.That(path.Count, Is.EqualTo(Space.ShortestSolution().Count));
        }

        [Test]
        public void ShortestSolutionWhere_WithoutBindingShadow_IsImpossible()
        {
            var path = StateSpace.ShortestSolutionWhere((_, c) => !c.Equals(Command.Bind(BindTarget.Shadow)));
            Assert.That(path, Is.Null);
        }

        [Test]
        public void Explore_FromWonState_HasOnlyThatState()
        {
            RoomState won = TestSupport.ReadyAtGate().WithWon(true);
            StateSpace space = StateSpace.Explore(won);
            Assert.That(space.States.Count, Is.EqualTo(1));
            Assert.That(space.GoalReachable, Is.True);
            Assert.That(space.ShortestSolution(), Is.Empty);
        }
    }
}
