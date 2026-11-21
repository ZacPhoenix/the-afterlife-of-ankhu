using System.Linq;
using Ankhu.BorrowedDawn;
using Ankhu.BorrowedDawn.Analysis;
using NUnit.Framework;

namespace Ankhu.BorrowedDawn.Tests
{
    [TestFixture]
    public class GreyboxProfileTests
    {
        private const RuleProfile G = RuleProfile.Greybox;

        public static readonly string[] GreyboxSolution =
        {
            "flow west", "board elevator", "flow east", "bind elevator", "flow west",
            "take mirror", "go to gallery", "flow east", "release elevator", "read inscription",
            "lower ladder", "descend", "install mirror", "bind shadow", "wake bearer",
            "go to gallery", "go to landing", "bind bridge", "recall bell", "strike bell",
            "echo bell", "go to bridge", "go to gate", "he who returns the morning",
        };

        private static readonly StateSpace Space = StateSpace.Explore(profile: G);

        private static RoomState Run(params string[] steps)
        {
            var session = new PuzzleSession(G);
            foreach (string step in steps)
            {
                SubmitResult r = session.Submit(step);
                Assert.That(r.Accepted, Is.True, $"\"{step}\" rejected: {r.Message}");
            }
            return session.State;
        }

        private static void AssertRejected(RoomState s, Command c, Outcome expected)
        {
            CommandResult r = RoomRules.Apply(s, c, G);
            Assert.That(r.Outcome, Is.EqualTo(expected), r.Message);
            Assert.That(r.State, Is.SameAs(s));
        }

        [Test]
        public void AuthoredSolution_Wins()
        {
            var session = new PuzzleSession(G);
            foreach (string step in GreyboxSolution)
            {
                SubmitResult r = session.Submit(step);
                Assert.That(r.Accepted, Is.True, $"\"{step}\" rejected: {r.Message}");
            }
            Assert.That(session.State.Won, Is.True);
            Assert.That(session.StepCount, Is.EqualTo(GreyboxSolution.Length));
        }

        [Test]
        public void ElevatorRelease_RejectedWhileWaterWest_AcceptedWhileWaterEast()
        {
            RoomState held = Run("flow west", "board elevator", "flow east", "bind elevator", "flow west");
            AssertRejected(held, Command.Release(BindTarget.Elevator), Outcome.BindingBearsLoad);
            RoomState east = RoomRules.Apply(held, Command.Flow(WaterSide.East), G).State;
            CommandResult r = RoomRules.Apply(east, Command.Release(BindTarget.Elevator), G);
            Assert.That(r.Accepted, Is.True);
            Assert.That(r.State.ElevatorHigh, Is.True);
        }

        [Test]
        public void ElevatorRelease_NeverAcceptedWithWaterWest_AnywhereInSpace()
        {
            bool any = Space.Edges.Any(e => e.Command.Equals(Command.Release(BindTarget.Elevator)) && Space.States[e.From].Water == WaterSide.West);
            Assert.That(any, Is.False);
        }

        [Test]
        public void Mirror_OnlyFromRaisedLiftWithWaterWest()
        {
            RoomState onLift = Run("flow west", "board elevator", "flow east", "bind elevator", "flow west");
            Assert.That(RoomRules.Apply(onLift, Command.TakeMirror, G).Accepted, Is.True);
            RoomState inGallery = RoomRules.Apply(onLift, Command.MoveTo(Location.Gallery), G).State;
            AssertRejected(inGallery, Command.TakeMirror, Outcome.WrongLocation);
            RoomState liftEast = RoomRules.Apply(onLift, Command.Flow(WaterSide.East), G).State;
            AssertRejected(liftEast, Command.TakeMirror, Outcome.MirrorNotReachable);
        }

        [Test]
        public void Prototype_StillTakesMirrorFromGallery()
        {
            RoomState onLift = Run("flow west", "board elevator", "flow east", "bind elevator", "flow west");
            Assert.That(RoomRules.Apply(onLift, Command.TakeMirror, RuleProfile.Prototype).Outcome, Is.EqualTo(Outcome.WrongLocation));
        }

        [Test]
        public void Walkway_OpenRegardlessOfElevator()
        {
            RoomState s = new RoomState(WaterSide.West, Location.Gallery, BindingSet.None, MirrorPlace.Raft, false, false, false, false, false, false);
            Assert.That(s.ElevatorHigh, Is.False);
            Assert.That(RoomRules.Apply(s, Command.MoveTo(Location.Landing), G).Accepted, Is.True);
            Assert.That(RoomRules.Apply(s, Command.MoveTo(Location.Landing), RuleProfile.Prototype).Outcome, Is.EqualTo(Outcome.WalkwayIncomplete));
        }

        [Test]
        public void Ladder_GatesFloorGalleryBothWays()
        {
            var floor = new RoomState(WaterSide.East, Location.Floor, BindingSet.None, MirrorPlace.Raft, false, false, false, false, false, false);
            var gallery = floor.WithLocation(Location.Gallery);
            AssertRejected(floor, Command.MoveTo(Location.Gallery), Outcome.LadderRaised);
            AssertRejected(gallery, Command.MoveTo(Location.Floor), Outcome.LadderRaised);
            AssertRejected(floor, Command.LowerLadder, Outcome.WrongLocation);
            RoomState lowered = RoomRules.Apply(gallery, Command.LowerLadder, G).State;
            Assert.That(lowered.LadderLowered, Is.True);
            AssertRejected(lowered, Command.LowerLadder, Outcome.AlreadyInState);
            RoomState down = RoomRules.Apply(lowered, Command.MoveTo(Location.Floor), G).State;
            Assert.That(down.Location, Is.EqualTo(Location.Floor));
            Assert.That(RoomRules.Apply(down, Command.MoveTo(Location.Gallery), G).Accepted, Is.True);
        }

        [Test]
        public void LowerLadder_UnavailableInPrototype()
        {
            var gallery = new RoomState(WaterSide.East, Location.Gallery, BindingSet.None, MirrorPlace.Raft, false, false, false, false, false, false);
            Assert.That(RoomRules.Apply(gallery, Command.LowerLadder).Outcome, Is.EqualTo(Outcome.UnavailableInProfile));
        }

        [Test]
        public void Bridge_ZoneNeedsBindingAndBlocksRelease()
        {
            var landing = new RoomState(WaterSide.East, Location.Landing, BindingSet.None, MirrorPlace.Raft, false, false, false, false, false, false);
            AssertRejected(landing, Command.MoveTo(Location.Bridge), Outcome.BridgeSwinging);
            RoomState held = RoomRules.Apply(landing, Command.Bind(BindTarget.Bridge), G).State;
            RoomState onBridge = RoomRules.Apply(held, Command.MoveTo(Location.Bridge), G).State;
            Assert.That(onBridge.Location, Is.EqualTo(Location.Bridge));
            AssertRejected(onBridge, Command.Release(BindTarget.Bridge), Outcome.WouldStrandAtGate);
            RoomState atGate = RoomRules.Apply(onBridge, Command.CrossBridge, G).State;
            Assert.That(atGate.Location, Is.EqualTo(Location.Gate));
            AssertRejected(atGate, Command.Release(BindTarget.Bridge), Outcome.WouldStrandAtGate);
        }

        [Test]
        public void Bridge_LocationUnreachableInPrototype()
        {
            Assert.That(StateSpace.Explore().States.Values.Any(s => s.Location == Location.Bridge), Is.False);
        }

        [Test]
        public void UnreadName_FeedbackDoesNotConfirmGuess()
        {
            var gate = new RoomState(WaterSide.East, Location.Gate, BindingSet.Bridge, MirrorPlace.Raft, false, false, false, false, false, false);
            CommandResult r = RoomRules.Apply(gate, Command.SpeakName, G);
            Assert.That(r.Outcome, Is.EqualTo(Outcome.NameUnknown));
            Assert.That(r.Message, Does.Not.Contain("name").IgnoreCase);
            Assert.That(r.Message, Does.Not.Contain("gallery").IgnoreCase);
        }

        [Test]
        public void Space_NoSoftlocksOrDeadEnds()
        {
            Assert.That(Space.GoalReachable, Is.True);
            Assert.That(Space.Softlocks, Is.Empty);
            Assert.That(Space.DeadEnds, Is.Empty);
        }

        [Test]
        public void Space_ElevatorBindingRequired()
        {
            var noElevator = StateSpace.ShortestSolutionWhere(
                (s, c) => !(c.Kind == CommandKind.Bind && c.Target == BindTarget.Elevator), profile: G);
            Assert.That(noElevator, Is.Null);
        }

        [Test]
        public void Space_ShortestSolutionReplaysToWon()
        {
            var path = Space.ShortestSolution();
            Assert.That(path, Is.Not.Null);
            RoomState s = RoomState.Initial;
            foreach (Command c in path)
            {
                CommandResult r = RoomRules.Apply(s, c, G);
                Assert.That(r.Accepted, Is.True, c.ToString());
                s = r.State;
            }
            Assert.That(s.Won, Is.True);
        }

        [Test]
        public void Space_RejectionsNeverMutate()
        {
            foreach (RoomState s in Space.States.Values)
            {
                foreach (Command c in RoomRules.AllCommands)
                {
                    CommandResult r = RoomRules.Apply(s, c, G);
                    if (!r.Accepted) Assert.That(r.State, Is.SameAs(s));
                }
            }
        }

        [Test]
        public void Space_PackRoundTrips()
        {
            foreach (var kv in Space.States)
            {
                Assert.That(RoomState.Unpack(kv.Key), Is.EqualTo(kv.Value));
            }
        }

        [Test]
        public void Parser_NewVocabulary()
        {
            Assert.That(SpellParser.Parse("Lower the ladder").Command, Is.EqualTo(Command.LowerLadder));
            Assert.That(SpellParser.Parse("lower hatch").Command, Is.EqualTo(Command.LowerLadder));
            Assert.That(SpellParser.Parse("go to bridge").Command, Is.EqualTo(Command.MoveTo(Location.Bridge)));
        }
    }
}
