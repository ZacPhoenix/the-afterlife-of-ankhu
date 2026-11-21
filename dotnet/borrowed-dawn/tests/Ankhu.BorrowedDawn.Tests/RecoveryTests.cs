using Ankhu.BorrowedDawn.Analysis;
using NUnit.Framework;
using static Ankhu.BorrowedDawn.Tests.TestSupport;

namespace Ankhu.BorrowedDawn.Tests
{
    [TestFixture]
    public class RecoveryTests
    {
        /// <summary>Canonical steps 1-10: mirror installed, elevator bound, Bearer asleep, one free binding.</summary>
        private static RoomState MirrorInstalled() => AfterCanonicalSteps(10);

        [Test]
        public void WakeBeforeBindingShadow_ThenSleep_ThenBind_Recovers()
        {
            RoomState s = MirrorInstalled();
            Assert.That(s.ShadowPresent, Is.True);
            Assert.That(s.IsBound(BindTarget.Shadow), Is.False);

            // Mistake: wake first. The shadow follows its owner.
            s = MustApply(s, "wake bearer");
            Assert.That(s.ShadowPresent, Is.False);
            CommandResult bind = RoomRules.Apply(s, Command.Bind(BindTarget.Shadow));
            Assert.That(bind.Outcome, Is.EqualTo(Outcome.NoShadowToBind));
            Assert.That(bind.State, Is.SameAs(s));

            // Sleep restores the shadow.
            s = MustApply(s, "sleep bearer");
            Assert.That(s.BearerAwake, Is.False);
            Assert.That(s.ShadowPresent, Is.True);

            // Bind, then wake: the bound shadow persists.
            s = MustApply(s, "bind shadow");
            s = MustApply(s, "wake bearer");
            Assert.That(s.BearerAwake, Is.True);
            Assert.That(s.ShadowPresent, Is.True);

            StateSpace space = StateSpace.Explore(s);
            Assert.That(space.GoalReachable, Is.True);
            Assert.That(space.ShortestSolution(), Is.Not.Null);
        }

        [Test]
        public void WakeBeforeBindingShadow_StateIsStillSolvable()
        {
            RoomState s = MustApply(MirrorInstalled(), "wake bearer");
            Assert.That(StateSpace.Explore(s).GoalReachable, Is.True);
        }

        [Test]
        public void WakeMessage_ReflectsWhetherShadowIsBound()
        {
            RoomState s = MirrorInstalled();
            Assert.That(RoomRules.Apply(s, Command.WakeBearer).Message, Does.Contain("follows"));
            RoomState bound = MustApply(s, Command.Bind(BindTarget.Shadow));
            Assert.That(RoomRules.Apply(bound, Command.WakeBearer).Message, Does.Contain("remains"));
        }

        [Test]
        public void ReleasingShadowWhileBearerAwake_RemovesShadow_AndSleepRestoresIt()
        {
            RoomState s = AfterCanonicalSteps(12); // shadow bound, Bearer awake
            Assert.That(s.ShadowPresent, Is.True);

            s = MustApply(s, "release shadow");
            Assert.That(s.IsBound(BindTarget.Shadow), Is.False);
            Assert.That(s.ShadowPresent, Is.False);

            s = MustApply(s, "sleep bearer");
            Assert.That(s.ShadowPresent, Is.True);
            Assert.That(StateSpace.Explore(s).GoalReachable, Is.True);
        }

        [Test]
        public void ReleasingShadowWhileBearerAsleep_KeepsShadow()
        {
            RoomState s = MustApply(MirrorInstalled(), "bind shadow");
            s = MustApply(s, "release shadow");
            Assert.That(s.ShadowPresent, Is.True);
        }

        [Test]
        public void ReleasingElevatorWithWaterWest_DropsElevatorWithAnkhuOnIt()
        {
            // flow west, board elevator, flow east: on the lift, high.
            RoomState s = AfterCanonicalSteps(3);
            s = MustApply(s, "bind elevator");
            s = MustApply(s, "flow west");
            Assert.That(s.Location, Is.EqualTo(Location.Lift));
            Assert.That(s.ElevatorHigh, Is.True, "binding holds it with water west");

            CommandResult r = RoomRules.Apply(s, Command.Release(BindTarget.Elevator));
            Assert.That(r.Outcome, Is.EqualTo(Outcome.Accepted));
            s = r.State;
            Assert.That(s.ElevatorHigh, Is.False);
            Assert.That(s.Location, Is.EqualTo(Location.Lift), "Ankhu rides the lift down");

            // At the bottom: the floor is reachable, the gallery is not.
            Assert.That(RoomRules.Apply(s, Command.MoveTo(Location.Gallery)).Outcome, Is.EqualTo(Outcome.NoLandingAtThisHeight));
            Assert.That(RoomRules.Apply(s, Command.MoveTo(Location.Floor)).Outcome, Is.EqualTo(Outcome.Accepted));
            Assert.That(StateSpace.Explore(s).GoalReachable, Is.True);
        }

        [Test]
        public void ReleasingElevatorWithWaterEast_KeepsElevatorHigh()
        {
            RoomState s = MustApply(AfterCanonicalSteps(3), "bind elevator");
            s = MustApply(s, "release elevator");
            Assert.That(s.ElevatorHigh, Is.True);
            Assert.That(s.Location, Is.EqualTo(Location.Lift));
        }

        [Test]
        public void ReleasingElevatorWithWaterWest_WhileOnGallery_BreaksWalkway()
        {
            RoomState s = AfterCanonicalSteps(8); // mirror taken, gallery, elevator bound, water west
            s = MustApply(s, "release elevator");
            Assert.That(s.ElevatorHigh, Is.False);
            Assert.That(s.UpperWalkwayComplete, Is.False);
            Assert.That(RoomRules.Apply(s, Command.MoveTo(Location.Landing)).Outcome, Is.EqualTo(Outcome.WalkwayIncomplete));
            Assert.That(StateSpace.Explore(s).GoalReachable, Is.True);
        }

        [Test]
        public void SleepAndWake_AreRejectedWhenAlreadyInState()
        {
            Assert.That(RoomRules.Apply(RoomState.Initial, Command.SleepBearer).Outcome, Is.EqualTo(Outcome.AlreadyInState));
            RoomState awake = MustApply(RoomState.Initial, Command.WakeBearer);
            Assert.That(RoomRules.Apply(awake, Command.WakeBearer).Outcome, Is.EqualTo(Outcome.AlreadyInState));
        }

        [Test]
        public void SecondStrike_IsRejected_PerDocumentedTightening()
        {
            RoomState s = AfterCanonicalSteps(21);
            Assert.That(s.BellStruck, Is.True);
            CommandResult r = RoomRules.Apply(s, Command.StrikeBell);
            Assert.That(r.Outcome, Is.EqualTo(Outcome.AlreadyInState));
            Assert.That(r.State, Is.SameAs(s));
        }
    }
}
