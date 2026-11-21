using Ankhu.Bindings;
using Ankhu.Integration;
using NUnit.Framework;

namespace Ankhu.Tests
{
    /// <summary>
    /// Plays the greybox room through the adapter with the zones Ankhu would physically walk,
    /// so the binding, the rules profile and the layout agree end to end.
    /// </summary>
    public sealed class BorrowedDawnAdapterTests
    {
        private BorrowedDawnRulesAdapter adapter;

        [SetUp]
        public void SetUp()
        {
            adapter = new BorrowedDawnRulesAdapter();
        }

        private static PlayerContext At(string zone) =>
            new PlayerContext(zone, zone == ZoneIds.Lift, zone == ZoneIds.Bridge);

        private void Walk(params string[] zones)
        {
            foreach (string zone in zones)
            {
                Assert.IsNull(adapter.SyncLocation(zone), "Walking into " + zone + " was refused");
            }
        }

        private void Say(string zone, string text)
        {
            CommandResult r = adapter.Speak(text, At(zone));
            Assert.AreEqual(CommandOutcome.Success, r.Outcome, "\"" + text + "\" at " + zone + ": " + r.Message);
        }

        private void Use(string zone, string interaction)
        {
            CommandResult r = adapter.Interact(interaction, At(zone));
            Assert.AreEqual(CommandOutcome.Success, r.Outcome, interaction + " at " + zone + ": " + r.Message);
        }

        [Test]
        public void InitialSnapshotMatchesThePrototypeStart()
        {
            RoomSnapshot s = adapter.Current;
            Assert.AreEqual(WaterSide.East, s.Water);
            Assert.AreEqual(MirrorLocation.Raft, s.Mirror);
            Assert.IsTrue(s.ElevatorHigh);
            Assert.IsFalse(s.RaftHigh);
            Assert.IsFalse(s.LadderLowered);
            Assert.AreEqual(0, s.BindingsUsed);
            Assert.AreEqual(2, s.BindingCapacity);
            Assert.AreEqual(ZoneIds.Floor, adapter.RulesZoneId);
        }

        [Test]
        public void IntendedGreyboxSolutionOpensTheGate()
        {
            Say(ZoneIds.Floor, "flow west");
            Walk(ZoneIds.Lift);
            Say(ZoneIds.Lift, "flow east");
            Walk(ZoneIds.Gallery);
            Use(ZoneIds.Gallery, InteractionIds.ReadInscription);
            Walk(ZoneIds.Lift);
            Say(ZoneIds.Lift, "bind elevator");
            Say(ZoneIds.Lift, "flow west");
            Use(ZoneIds.Lift, InteractionIds.TakeMirror);
            Walk(ZoneIds.Gallery);
            Use(ZoneIds.Gallery, InteractionIds.LowerLadder);
            Walk(ZoneIds.Floor);
            Use(ZoneIds.Floor, InteractionIds.InstallMirror);
            Say(ZoneIds.Floor, "bind shadow");
            Say(ZoneIds.Floor, "wake bearer");
            Say(ZoneIds.Floor, "flow east");
            Say(ZoneIds.Floor, "release elevator");
            Walk(ZoneIds.Gallery, ZoneIds.Landing);
            Say(ZoneIds.Landing, "recall bell");
            Use(ZoneIds.Landing, InteractionIds.StrikeBell);
            Say(ZoneIds.Landing, "echo bell");
            Say(ZoneIds.Landing, "bind bridge");
            Walk(ZoneIds.Bridge, ZoneIds.Gate);
            Say(ZoneIds.Gate, "He who returns the morning.");

            Assert.IsTrue(adapter.Current.GateOpen);
            Assert.IsTrue(adapter.Current.SunReachesGate);
            Assert.IsTrue(adapter.Current.ShadowPresent);
            Assert.IsTrue(adapter.Current.BellEchoing);
        }

        [Test]
        public void ClimbingToTheGalleryBeforeTheLadderIsLoweredIsRefused()
        {
            CommandResult r = adapter.SyncLocation(ZoneIds.Gallery);
            Assert.IsNotNull(r);
            Assert.AreEqual(CommandOutcome.Rejected, r.Outcome);
            Assert.AreEqual(ZoneIds.Floor, adapter.RulesZoneId);
        }

        [Test]
        public void ElevatorBindingBearsLoadWhileWaterIsWest()
        {
            Say(ZoneIds.Floor, "flow west");
            Walk(ZoneIds.Lift);
            Say(ZoneIds.Lift, "flow east");
            Say(ZoneIds.Lift, "bind elevator");
            Say(ZoneIds.Lift, "flow west");
            RoomSnapshot before = adapter.Current;

            CommandResult r = adapter.Speak("release elevator", At(ZoneIds.Lift));

            Assert.AreEqual(CommandOutcome.Rejected, r.Outcome);
            Assert.AreSame(before, adapter.Current, "A refused command must not replace the snapshot");
        }

        [Test]
        public void ParseFailuresMapToOutcomesAndNeverChangeState()
        {
            int changes = 0;
            adapter.StateChanged += (a, b) => changes++;

            Assert.AreEqual(CommandOutcome.NotUnderstood, adapter.Speak("dance wildly", At(ZoneIds.Floor)).Outcome);
            Assert.AreEqual(CommandOutcome.Incomplete, adapter.Speak("bind", At(ZoneIds.Floor)).Outcome);
            Assert.AreEqual(CommandOutcome.InvalidTarget, adapter.Speak("bind moon", At(ZoneIds.Floor)).Outcome);
            Assert.AreEqual(CommandOutcome.InvalidTarget, adapter.Interact("juggle", At(ZoneIds.Floor)).Outcome);
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void RepeatedFlowIsNoChange()
        {
            Assert.AreEqual(CommandOutcome.NoChange, adapter.Speak("flow east", At(ZoneIds.Floor)).Outcome);
        }

        [Test]
        public void StateChangedFiresOncePerAcceptedCommandWithPreviousAndCurrent()
        {
            RoomSnapshot seenPrevious = null;
            RoomSnapshot seenCurrent = null;
            int changes = 0;
            adapter.StateChanged += (a, b) => { changes++; seenPrevious = a; seenCurrent = b; };
            RoomSnapshot start = adapter.Current;

            Say(ZoneIds.Floor, "flow west");

            Assert.AreEqual(1, changes);
            Assert.AreSame(start, seenPrevious);
            Assert.AreSame(adapter.Current, seenCurrent);
            Assert.AreEqual(WaterSide.West, seenCurrent.Water);
        }

        [Test]
        public void UndoRestoresTheEarlierSnapshotAndLocation()
        {
            Say(ZoneIds.Floor, "flow west");
            Walk(ZoneIds.Lift);
            Assert.AreEqual(CommandOutcome.Success, adapter.Undo().Outcome);
            Assert.AreEqual(ZoneIds.Floor, adapter.RulesZoneId);
            Assert.AreEqual(CommandOutcome.Success, adapter.Undo().Outcome);
            Assert.AreEqual(WaterSide.East, adapter.Current.Water);
            Assert.AreEqual(CommandOutcome.NoChange, adapter.Undo().Outcome);
        }

        [Test]
        public void SaveAndLoadRoundTrip()
        {
            Say(ZoneIds.Floor, "flow west");
            Walk(ZoneIds.Lift);
            string payload = adapter.SaveState();

            var other = new BorrowedDawnRulesAdapter();
            Assert.IsTrue(other.LoadState(payload));
            Assert.AreEqual(adapter.Current.ToString(), other.Current.ToString());
            Assert.AreEqual(ZoneIds.Lift, other.RulesZoneId);
        }

        [Test]
        public void LoadRejectsGarbageAndKeepsState()
        {
            RoomSnapshot before = adapter.Current;
            Assert.IsFalse(adapter.LoadState("not a number"));
            Assert.IsFalse(adapter.LoadState("-5"));
            Assert.IsFalse(adapter.LoadState(""));
            Assert.AreSame(before, adapter.Current);
        }

        [Test]
        public void ResetReturnsToTheStart()
        {
            Say(ZoneIds.Floor, "flow west");
            adapter.Reset();
            Assert.AreEqual(WaterSide.East, adapter.Current.Water);
            Assert.AreEqual(ZoneIds.Floor, adapter.RulesZoneId);
        }

        [Test]
        public void HintNamesMissingConditionsWithoutSolutionSteps()
        {
            string hint = adapter.Hint(At(ZoneIds.Floor));
            StringAssert.Contains("sun", hint);
            StringAssert.Contains("a name", hint);
        }
    }
}
