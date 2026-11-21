using NUnit.Framework;
using static Ankhu.BorrowedDawn.Tests.TestSupport;

namespace Ankhu.BorrowedDawn.Tests
{
    [TestFixture]
    public class BindingTests
    {
        private static void AssertRejected(RoomState s, Command c, Outcome expected)
        {
            CommandResult r = RoomRules.Apply(s, c);
            Assert.That(r.Outcome, Is.EqualTo(expected), r.Message);
            Assert.That(r.State, Is.SameAs(s));
            AssertStatesIdentical(r.State, s);
        }

        // Landing with elevator high (water east), mirror installed with sleeping Bearer (shadow present).
        private static RoomState LandingWithShadowAvailable(BindingSet bindings) => Make(
            water: WaterSide.East, location: Location.Landing, bindings: bindings,
            mirror: MirrorPlace.Socket, awake: false, read: true);

        [Test]
        public void ThirdBind_IsRejectedWithCapacityFull()
        {
            RoomState s = LandingWithShadowAvailable(BindingSet.Elevator | BindingSet.Shadow);
            Assert.That(IsReachable(s), Is.True);
            Assert.That(s.HasFreeBinding, Is.False);
            AssertRejected(s, Command.Bind(BindTarget.Bridge), Outcome.BindingCapacityFull);
        }

        [TestCase(BindTarget.Elevator, BindTarget.Shadow, BindTarget.Bridge)]
        [TestCase(BindTarget.Elevator, BindTarget.Bridge, BindTarget.Shadow)]
        [TestCase(BindTarget.Shadow, BindTarget.Bridge, BindTarget.Elevator)]
        public void ThirdBind_EveryCombination_IsRejectedWithCapacityFull(BindTarget a, BindTarget b, BindTarget third)
        {
            RoomState s = LandingWithShadowAvailable(a.ToFlag() | b.ToFlag());
            AssertRejected(s, Command.Bind(third), Outcome.BindingCapacityFull);
        }

        [Test]
        public void BindingTwoTargets_ThroughPlay_FillsCapacity()
        {
            RoomState s = LandingWithShadowAvailable(BindingSet.None);
            s = MustApply(s, Command.Bind(BindTarget.Elevator));
            Assert.That(s.BindingCount, Is.EqualTo(1));
            s = MustApply(s, Command.Bind(BindTarget.Bridge));
            Assert.That(s.BindingCount, Is.EqualTo(2));
            AssertRejected(s, Command.Bind(BindTarget.Shadow), Outcome.BindingCapacityFull);
        }

        [TestCase(BindTarget.Elevator)]
        [TestCase(BindTarget.Shadow)]
        [TestCase(BindTarget.Bridge)]
        public void RebindingHeldTarget_IsBindingAlreadyHeld(BindTarget t)
        {
            RoomState s = LandingWithShadowAvailable(t.ToFlag());
            AssertRejected(s, Command.Bind(t), Outcome.BindingAlreadyHeld);
        }

        [Test]
        public void RebindingHeldTarget_WhenFull_IsAlreadyHeldNotCapacityFull()
        {
            RoomState s = LandingWithShadowAvailable(BindingSet.Elevator | BindingSet.Bridge);
            AssertRejected(s, Command.Bind(BindTarget.Bridge), Outcome.BindingAlreadyHeld);
        }

        [Test]
        public void Release_FreesASlot_ForAnotherBinding()
        {
            RoomState s = LandingWithShadowAvailable(BindingSet.Elevator | BindingSet.Shadow);
            s = MustApply(s, Command.Release(BindTarget.Elevator));
            Assert.That(s.Bindings, Is.EqualTo(BindingSet.Shadow));
            Assert.That(s.HasFreeBinding, Is.True);
            s = MustApply(s, Command.Bind(BindTarget.Bridge));
            Assert.That(s.Bindings, Is.EqualTo(BindingSet.Shadow | BindingSet.Bridge));
        }

        [TestCase(BindTarget.Elevator)]
        [TestCase(BindTarget.Shadow)]
        [TestCase(BindTarget.Bridge)]
        public void ReleaseUnbound_IsNotBound(BindTarget t)
        {
            AssertRejected(LandingWithShadowAvailable(BindingSet.None), Command.Release(t), Outcome.NotBound);
            AssertRejected(RoomState.Initial, Command.Release(t), Outcome.NotBound);
        }

        [Test]
        public void ReleaseBridge_AtGate_WouldStrand()
        {
            RoomState s = ReadyAtGate();
            AssertRejected(s, Command.Release(BindTarget.Bridge), Outcome.WouldStrandAtGate);
        }

        [Test]
        public void ReleaseOtherBinding_AtGate_IsAllowed()
        {
            RoomState s = ReadyAtGate();
            RoomState next = MustApply(s, Command.Release(BindTarget.Shadow));
            Assert.That(next.Bindings, Is.EqualTo(BindingSet.Bridge));
            Assert.That(next.Location, Is.EqualTo(Location.Gate));
        }

        [Test]
        public void ReleaseBridge_OnLanding_IsAllowed()
        {
            RoomState s = ReadyAtGate().WithLocation(Location.Landing);
            RoomState next = MustApply(s, Command.Release(BindTarget.Bridge));
            Assert.That(next.BridgeHeld, Is.False);
            AssertRejected(next, Command.CrossBridge, Outcome.BridgeSwinging);
            AssertRejected(next, Command.MoveTo(Location.Gate), Outcome.BridgeSwinging);
        }

        // ---------------------------------------------------------------- elevator precondition

        [TestCase(Location.Lift)]
        [TestCase(Location.Gallery)]
        [TestCase(Location.Landing)]
        public void BindElevator_HighAndUpper_IsAccepted(Location where)
        {
            RoomState s = Make(water: WaterSide.East, location: where);
            RoomState next = MustApply(s, Command.Bind(BindTarget.Elevator));
            Assert.That(next.IsBound(BindTarget.Elevator), Is.True);
        }

        [TestCase(Location.Floor)]
        [TestCase(Location.Gate)]
        public void BindElevator_HighButNotUpperLocation_IsNotReachable(Location where)
        {
            BindingSet b = where == Location.Gate ? BindingSet.Bridge : BindingSet.None;
            AssertRejected(Make(water: WaterSide.East, location: where, bindings: b),
                Command.Bind(BindTarget.Elevator), Outcome.ElevatorNotReachable);
        }

        [TestCase(Location.Lift)]
        [TestCase(Location.Gallery)]
        [TestCase(Location.Landing)]
        public void BindElevator_WhenLow_IsNotReachable(Location where)
        {
            AssertRejected(Make(water: WaterSide.West, location: where),
                Command.Bind(BindTarget.Elevator), Outcome.ElevatorNotReachable);
        }

        // ---------------------------------------------------------------- shadow precondition

        [Test]
        public void BindShadow_WithShadowPresent_IsAccepted_FromAnyLocation()
        {
            foreach (Location where in new[] { Location.Floor, Location.Lift, Location.Gallery, Location.Landing })
            {
                RoomState s = Make(location: where, mirror: MirrorPlace.Socket);
                Assert.That(s.ShadowPresent, Is.True);
                Assert.That(MustApply(s, Command.Bind(BindTarget.Shadow)).IsBound(BindTarget.Shadow), Is.True);
            }
        }

        [TestCase(MirrorPlace.Raft, false)]
        [TestCase(MirrorPlace.Carried, false)]
        [TestCase(MirrorPlace.Socket, true)]
        public void BindShadow_WithoutShadow_IsNoShadowToBind(MirrorPlace mirror, bool awake)
        {
            RoomState s = Make(location: Location.Floor, mirror: mirror, awake: awake);
            Assert.That(s.ShadowPresent, Is.False);
            AssertRejected(s, Command.Bind(BindTarget.Shadow), Outcome.NoShadowToBind);
        }

        // ---------------------------------------------------------------- bridge precondition

        [Test]
        public void BindBridge_OnLanding_IsAccepted()
        {
            RoomState next = MustApply(Make(location: Location.Landing), Command.Bind(BindTarget.Bridge));
            Assert.That(next.BridgeHeld, Is.True);
        }

        [TestCase(Location.Floor)]
        [TestCase(Location.Lift)]
        [TestCase(Location.Gallery)]
        public void BindBridge_AwayFromLanding_IsNotReachable(Location where)
        {
            AssertRejected(Make(location: where), Command.Bind(BindTarget.Bridge), Outcome.BridgeNotReachable);
        }

        [Test]
        public void ConstructorRejectsThreeBindings()
        {
            Assert.That(() => Make(bindings: BindingSet.Elevator | BindingSet.Shadow | BindingSet.Bridge),
                Throws.ArgumentException);
            Assert.That(() => Make(bindings: (BindingSet)8), Throws.ArgumentException);
        }

        [Test]
        public void NoReachableStateExceedsCapacity()
        {
            foreach (RoomState s in ReachableStates)
            {
                Assert.That(s.BindingCount, Is.LessThanOrEqualTo(RoomState.BindingCapacity));
            }
        }
    }
}
