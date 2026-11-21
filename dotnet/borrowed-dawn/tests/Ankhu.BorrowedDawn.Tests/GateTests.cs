using System.Collections.Generic;
using NUnit.Framework;

namespace Ankhu.BorrowedDawn.Tests
{
    [TestFixture]
    public class GateTests
    {
        public static IEnumerable<TestCaseData> OneMissingPrerequisite()
        {
            RoomState ready = TestSupport.ReadyAtGate();

            yield return new TestCaseData(ready.WithLocation(Location.Landing), Outcome.WrongLocation)
                .SetName("Gate_NotAtGate_OnLanding_WrongLocation");
            yield return new TestCaseData(ready.WithNameDiscovered(false), Outcome.NameUnknown)
                .SetName("Gate_NameNotDiscovered_NameUnknown");
            yield return new TestCaseData(ready.WithWater(WaterSide.West), Outcome.GateConditionsUnmet)
                .SetName("Gate_NoSun_WaterWest_ConditionsUnmet");
            yield return new TestCaseData(ready.WithBearerAwake(false), Outcome.GateConditionsUnmet)
                .SetName("Gate_NoSun_BearerAsleep_ConditionsUnmet");
            // Mirror still carried: no shadow can have been bound, so the second binding is the elevator.
            yield return new TestCaseData(
                    ready.WithMirror(MirrorPlace.Carried).WithBindings(BindingSet.Elevator | BindingSet.Bridge),
                    Outcome.GateConditionsUnmet)
                .SetName("Gate_NoSun_MirrorCarried_ConditionsUnmet");
            yield return new TestCaseData(
                    ready.WithMirror(MirrorPlace.Raft).WithBindings(BindingSet.Bridge),
                    Outcome.GateConditionsUnmet)
                .SetName("Gate_NoSun_MirrorOnRaft_ConditionsUnmet");
            yield return new TestCaseData(ready.WithBindings(BindingSet.Bridge), Outcome.GateConditionsUnmet)
                .SetName("Gate_NoShadow_AwakeBearerUnboundShadow_ConditionsUnmet");
            yield return new TestCaseData(
                    ready.WithBindings(BindingSet.Elevator | BindingSet.Bridge),
                    Outcome.GateConditionsUnmet)
                .SetName("Gate_NoShadow_ElevatorInsteadOfShadowBound_ConditionsUnmet");
            yield return new TestCaseData(ready.WithEchoActive(false), Outcome.GateConditionsUnmet)
                .SetName("Gate_NoEcho_StruckButNotEchoed_ConditionsUnmet");
            yield return new TestCaseData(ready.WithEchoActive(false).WithBellStruck(false), Outcome.GateConditionsUnmet)
                .SetName("Gate_NoEcho_RepairedButNeverStruck_ConditionsUnmet");
        }

        [Test]
        public void ReadyState_IsReachable_AndSpeakingTheNameWins()
        {
            RoomState ready = TestSupport.ReadyAtGate();
            Assert.That(TestSupport.IsReachable(ready), Is.True);
            Assert.That(ready.AllGateConditions, Is.True);
            CommandResult r = RoomRules.Apply(ready, Command.SpeakName);
            Assert.That(r.Outcome, Is.EqualTo(Outcome.Accepted));
            TestSupport.AssertStatesIdentical(r.State, ready.WithWon(true));
        }

        [TestCaseSource(nameof(OneMissingPrerequisite))]
        public void SpeakName_WithExactlyOnePrerequisiteMissing_IsRejectedAndStateUnchanged(RoomState s, Outcome expected)
        {
            Assert.That(TestSupport.IsReachable(s), Is.True, $"constructed state should be reachable in play: [{s}]");
            CommandResult r = RoomRules.Apply(s, Command.SpeakName);
            Assert.That(r.Outcome, Is.EqualTo(expected), r.Message);
            Assert.That(r.Accepted, Is.False);
            Assert.That(r.State, Is.SameAs(s));
            Assert.That(r.Previous, Is.SameAs(s));
            Assert.That(r.Command, Is.EqualTo(Command.SpeakName));
            Assert.That(r.State.Won, Is.False);
        }

        [Test]
        public void DerivedConditions_FailForTheDocumentedReasons()
        {
            RoomState ready = TestSupport.ReadyAtGate();
            Assert.Multiple(() =>
            {
                Assert.That(ready.WithWater(WaterSide.West).SunAtGate, Is.False);
                Assert.That(ready.WithWater(WaterSide.West).ShadowPresent, Is.True);
                Assert.That(ready.WithBearerAwake(false).SunAtGate, Is.False);
                Assert.That(ready.WithBearerAwake(false).ShadowPresent, Is.True);
                Assert.That(ready.WithMirror(MirrorPlace.Carried).SunAtGate, Is.False);
                Assert.That(ready.WithBindings(BindingSet.Bridge).ShadowPresent, Is.False);
                Assert.That(ready.WithBindings(BindingSet.Bridge).SunAtGate, Is.True);
                Assert.That(ready.WithEchoActive(false).AllGateConditions, Is.False);
                Assert.That(ready.WithNameDiscovered(false).AllGateConditions, Is.False);
                Assert.That(ready.WithLocation(Location.Landing).AllGateConditions, Is.True);
            });
        }

        [Test]
        public void SpeakName_LocationIsCheckedBeforeName()
        {
            RoomState s = TestSupport.ReadyAtGate().WithLocation(Location.Landing).WithNameDiscovered(false);
            Assert.That(RoomRules.Apply(s, Command.SpeakName).Outcome, Is.EqualTo(Outcome.WrongLocation));
        }

        [Test]
        public void SpeakName_NameIsCheckedBeforeConditions()
        {
            RoomState s = TestSupport.ReadyAtGate().WithNameDiscovered(false).WithEchoActive(false);
            Assert.That(RoomRules.Apply(s, Command.SpeakName).Outcome, Is.EqualTo(Outcome.NameUnknown));
        }

        [TestCase(Location.Floor)]
        [TestCase(Location.Lift)]
        [TestCase(Location.Gallery)]
        [TestCase(Location.Landing)]
        public void SpeakName_AwayFromGate_IsWrongLocation(Location where)
        {
            RoomState s = TestSupport.ReadyAtGate().WithLocation(where);
            CommandResult r = RoomRules.Apply(s, Command.SpeakName);
            Assert.That(r.Outcome, Is.EqualTo(Outcome.WrongLocation));
            Assert.That(r.State, Is.SameAs(s));
        }

        [Test]
        public void SpeakName_FromEveryReachableState_WinsExactlyWhenAtGateWithAllConditions()
        {
            foreach (RoomState s in TestSupport.ReachableStates)
            {
                if (s.Won) continue;
                bool shouldWin = s.Location == Location.Gate && s.AllGateConditions;
                CommandResult r = RoomRules.Apply(s, Command.SpeakName);
                Assert.That(r.Accepted, Is.EqualTo(shouldWin), $"[{s}] -> {r.Outcome}");
            }
        }
    }
}
