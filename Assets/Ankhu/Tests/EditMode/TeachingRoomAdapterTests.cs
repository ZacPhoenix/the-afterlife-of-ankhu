using Ankhu.BorrowedDawn;
using Ankhu.Bindings;
using Ankhu.Integration;
using NUnit.Framework;
using CommandResult = Ankhu.Integration.CommandResult;

namespace Ankhu.Tests
{
    /// <summary>The teaching rooms through the greybox adapter: each lesson solves, untaught spells change nothing.</summary>
    public sealed class TeachingRoomAdapterTests
    {
        private static PlayerContext At(string zone) => new PlayerContext(zone, false, false);

        [Test]
        public void SequenceMapsEveryRoomToASceneInOrder()
        {
            Assert.AreEqual("Teach2_HeldStep", BorrowedDawnRooms.NextSceneName(TeachingRooms.Cistern.Id));
            Assert.AreEqual("Teach3_LingeringShadow", BorrowedDawnRooms.NextSceneName(TeachingRooms.HeldStep.Id));
            Assert.AreEqual("BorrowedDawn_Greybox", BorrowedDawnRooms.NextSceneName(TeachingRooms.LingeringShadow.Id));
            Assert.IsNull(BorrowedDawnRooms.NextSceneName(BorrowedDawnRooms.BorrowedDawnId));
            foreach (string id in BorrowedDawnRooms.SequenceIds)
            {
                Assert.IsNotNull(BorrowedDawnRooms.SceneNameFor(id), id);
                Assert.AreEqual(id, BorrowedDawnRooms.Resolve(id).Id);
            }
        }

        [Test]
        public void CisternSolvesByWalkingAndSpeakingFlow()
        {
            var adapter = new BorrowedDawnRulesAdapter(TeachingRooms.Cistern);
            Assert.AreEqual(CommandOutcome.Success, adapter.Speak("flow west", At(ZoneIds.Floor)).Outcome);
            Assert.IsNull(adapter.SyncLocation(ZoneIds.Lift));
            Assert.AreEqual(CommandOutcome.Success, adapter.Speak("flow east", At(ZoneIds.Lift)).Outcome);
            Assert.IsNull(adapter.SyncLocation(ZoneIds.Gallery));
            Assert.IsTrue(adapter.IsSolved);
        }

        [Test]
        public void UntaughtSpellIsRefusedAndRaisesNotYetTaught()
        {
            var adapter = new BorrowedDawnRulesAdapter(TeachingRooms.Cistern);
            Outcome seen = Outcome.Accepted;
            adapter.RulesResolved += r => seen = r.Outcome;
            RoomSnapshot before = adapter.Current;

            CommandResult result = adapter.Speak("bind elevator", At(ZoneIds.Floor));

            Assert.AreEqual(CommandOutcome.Rejected, result.Outcome);
            Assert.AreEqual(Outcome.NotYetTaught, seen);
            Assert.AreSame(before, adapter.Current);
        }

        [Test]
        public void PreviewRespectsTheRoomVocabulary()
        {
            var adapter = new BorrowedDawnRulesAdapter(TeachingRooms.Cistern);
            Assert.AreNotEqual(CommandOutcome.Success, adapter.Preview(InteractionIds.ReadInscription, At(ZoneIds.Floor)).Outcome);
        }

        [Test]
        public void HeldStepSolvesWithBindAndRelease()
        {
            var adapter = new BorrowedDawnRulesAdapter(TeachingRooms.HeldStep);
            Assert.AreEqual(ZoneIds.Lift, adapter.RulesZoneId);
            Assert.AreEqual(CommandOutcome.Success, adapter.Speak("bind elevator", At(ZoneIds.Lift)).Outcome);
            Assert.AreEqual(CommandOutcome.Success, adapter.Speak("flow west", At(ZoneIds.Lift)).Outcome);
            Assert.AreEqual(CommandOutcome.Success, adapter.Interact(InteractionIds.TakeMirror, At(ZoneIds.Lift)).Outcome);
            Assert.AreEqual(CommandOutcome.Success, adapter.Speak("flow east", At(ZoneIds.Lift)).Outcome);
            Assert.AreEqual(CommandOutcome.Success, adapter.Speak("release elevator", At(ZoneIds.Lift)).Outcome);
            Assert.IsNull(adapter.SyncLocation(ZoneIds.Gallery));
            Assert.IsTrue(adapter.IsSolved);
        }

        [Test]
        public void LingeringShadowStartsHoldingTwoThreadsAndSolves()
        {
            var adapter = new BorrowedDawnRulesAdapter(TeachingRooms.LingeringShadow);
            Assert.IsTrue(adapter.Current.ElevatorBound);
            Assert.IsTrue(adapter.Current.BridgeBound);
            Assert.AreEqual(CommandOutcome.Success, adapter.Speak("flow east", At(ZoneIds.Floor)).Outcome);
            Assert.AreEqual(CommandOutcome.Success, adapter.Speak("release elevator", At(ZoneIds.Floor)).Outcome);
            Assert.AreEqual(CommandOutcome.Success, adapter.Speak("bind shadow", At(ZoneIds.Floor)).Outcome);
            Assert.AreEqual(CommandOutcome.Success, adapter.Speak("wake bearer", At(ZoneIds.Floor)).Outcome);
            Assert.IsTrue(adapter.IsSolved);
        }
    }
}
