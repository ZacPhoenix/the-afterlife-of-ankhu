using System.Linq;
using Ankhu.BorrowedDawn;
using Ankhu.BorrowedDawn.Analysis;
using NUnit.Framework;

namespace Ankhu.BorrowedDawn.Tests
{
    [TestFixture]
    public class TeachingRoomTests
    {
        private static PuzzleSession Play(RoomDefinition room, params string[] steps)
        {
            var session = new PuzzleSession(room);
            foreach (string step in steps)
            {
                SubmitResult r = session.Submit(step);
                Assert.That(r.Accepted, Is.True, $"{room.Id}: \"{step}\" rejected: {r.Message}");
            }
            return session;
        }

        [Test]
        public void Cistern_SolvesInFourTypedSteps()
        {
            var s = Play(TeachingRooms.Cistern, "flow west", "board elevator", "flow east", "enter gallery");
            Assert.That(s.IsSolved, Is.True);
        }

        [Test]
        public void HeldStep_SolvesAndRequiresLettingGo()
        {
            var s = Play(TeachingRooms.HeldStep, "bind elevator", "flow west", "carry mirror", "flow east", "go to gallery");
            Assert.That(s.IsSolved, Is.False, "still bound, so not solved");
            Assert.That(s.Submit("release elevator").Accepted, Is.True);
            Assert.That(s.IsSolved, Is.True);
        }

        [Test]
        public void HeldStep_ReleaseUnderLoadIsRefused()
        {
            var s = Play(TeachingRooms.HeldStep, "bind elevator", "flow west");
            SubmitResult r = s.Submit("release elevator");
            Assert.That(r.Execution.Outcome, Is.EqualTo(Outcome.BindingBearsLoad));
            Assert.That(TeachingRooms.HeldStep.AnpuNudges.ContainsKey(Outcome.BindingBearsLoad), Is.True);
        }

        [Test]
        public void LingeringShadow_SolvesAfterFreeingAThread()
        {
            var s = new PuzzleSession(TeachingRooms.LingeringShadow);
            Assert.That(s.Submit("bind shadow").Execution.Outcome, Is.EqualTo(Outcome.BindingCapacityFull));
            foreach (string step in new[] { "release bridge", "bind shadow", "wake bearer", "flow east" })
                Assert.That(s.Submit(step).Accepted, Is.True, step);
            Assert.That(s.IsSolved, Is.True);
        }

        [Test]
        public void LingeringShadow_EarlyWakeRecoversWithSleep()
        {
            var s = Play(TeachingRooms.LingeringShadow, "release bridge", "wake bearer");
            Assert.That(s.State.ShadowPresent, Is.False);
            Assert.That(s.Submit("bind shadow").Execution.Outcome, Is.EqualTo(Outcome.NoShadowToBind));
            foreach (string step in new[] { "sleep bearer", "bind shadow", "wake bearer", "flow east" })
                Assert.That(s.Submit(step).Accepted, Is.True, step);
            Assert.That(s.IsSolved, Is.True);
        }

        [TestCase("teach-1-cistern", "bind elevator")]
        [TestCase("teach-1-cistern", "wake bearer")]
        [TestCase("teach-2-held-step", "bind shadow")]
        [TestCase("teach-3-lingering-shadow", "take mirror")]
        [TestCase("teach-3-lingering-shadow", "go to lift")]
        public void UntaughtSpells_AreRefusedWithoutChangingState(string roomId, string text)
        {
            RoomDefinition room = TeachingRooms.Sequence.First(r => r.Id == roomId);
            var s = new PuzzleSession(room);
            RoomState before = s.State;
            SubmitResult r = s.Submit(text);
            Assert.That(r.Execution.Outcome, Is.EqualTo(Outcome.NotYetTaught));
            Assert.That(s.State, Is.SameAs(before));
            Assert.That(s.UndoDepth, Is.EqualTo(0));
        }

        [Test]
        public void SolvedRoom_RefusesFurtherCommands_ResetReturnsToRoomStart()
        {
            var s = Play(TeachingRooms.Cistern, "flow west", "board elevator", "flow east", "enter gallery");
            Assert.That(s.Submit("flow west").Execution.Outcome, Is.EqualTo(Outcome.PuzzleComplete));
            s.Reset();
            Assert.That(s.State, Is.EqualTo(TeachingRooms.Cistern.Start));
            var shadow = new PuzzleSession(TeachingRooms.LingeringShadow);
            shadow.Submit("release bridge");
            shadow.Reset();
            Assert.That(shadow.State, Is.EqualTo(TeachingRooms.LingeringShadow.Start));
        }

        [TestCaseSource(typeof(TeachingRooms), nameof(TeachingRooms.Sequence))]
        public void EveryRoom_HasNoSoftlocksOrDeadEnds(RoomDefinition room)
        {
            StateSpace space = StateSpace.Explore(room);
            Assert.That(space.GoalReachable, Is.True);
            Assert.That(space.Softlocks, Is.Empty);
            Assert.That(space.DeadEnds, Is.Empty);
        }

        [Test]
        public void ShortestLengths_MatchDesign()
        {
            Assert.That(StateSpace.Explore(TeachingRooms.Cistern).ShortestSolution().Count, Is.EqualTo(4));
            Assert.That(StateSpace.Explore(TeachingRooms.HeldStep).ShortestSolution().Count, Is.EqualTo(6));
            Assert.That(StateSpace.Explore(TeachingRooms.LingeringShadow).ShortestSolution().Count, Is.EqualTo(4));
        }

        [Test]
        public void Lessons_CannotBeBypassed()
        {
            Assert.That(StateSpace.ShortestSolutionWhere(TeachingRooms.Cistern, (s, c) => c.Kind != CommandKind.Flow), Is.Null);
            Assert.That(StateSpace.ShortestSolutionWhere(TeachingRooms.HeldStep, (s, c) => c.Kind != CommandKind.Bind), Is.Null);
            Assert.That(StateSpace.ShortestSolutionWhere(TeachingRooms.LingeringShadow, (s, c) => !(c.Kind == CommandKind.Bind && c.Target == BindTarget.Shadow)), Is.Null);
            Assert.That(StateSpace.ShortestSolutionWhere(TeachingRooms.LingeringShadow, (s, c) => c.Kind != CommandKind.Release), Is.Null);
        }

        [Test]
        public void AnpuLines_AvoidDashesAndSemicolons()
        {
            foreach (RoomDefinition room in TeachingRooms.Sequence.Take(3))
            {
                var lines = room.AnpuIntro.Concat(room.AnpuNudges.Values).Append(room.AnpuOnSolve);
                foreach (string line in lines)
                {
                    Assert.That(line, Does.Not.Contain("—").And.Not.Contain(";"), line);
                }
            }
        }
    }
}
