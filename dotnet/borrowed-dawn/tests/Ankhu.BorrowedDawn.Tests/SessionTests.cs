using System;
using System.Collections.Generic;
using NUnit.Framework;
using static Ankhu.BorrowedDawn.Tests.TestSupport;

namespace Ankhu.BorrowedDawn.Tests
{
    [TestFixture]
    public class SessionTests
    {
        private static PuzzleSession Solved(out List<RoomState> trail)
        {
            var session = new PuzzleSession();
            trail = new List<RoomState> { session.State };
            foreach (string step in CanonicalSolution)
            {
                Assert.That(session.Submit(step).Accepted, Is.True, step);
                trail.Add(session.State);
            }
            return session;
        }

        [Test]
        public void NewSession_StartsAtInitial()
        {
            var session = new PuzzleSession();
            Assert.That(session.State, Is.SameAs(RoomState.Initial));
            Assert.That(session.StepCount, Is.EqualTo(0));
            Assert.That(session.UndoDepth, Is.EqualTo(0));
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void Undo_RestoresEveryPreviousStateBackToInitial()
        {
            PuzzleSession session = Solved(out var trail);
            Assert.That(session.State.Won, Is.True);
            for (int i = trail.Count - 2; i >= 0; i--)
            {
                Assert.That(session.Undo(), Is.True);
                Assert.That(session.State, Is.SameAs(trail[i]), $"undo to step {i}");
                Assert.That(session.StepCount, Is.EqualTo(i));
                Assert.That(session.UndoDepth, Is.EqualTo(i));
            }
            Assert.That(session.State, Is.SameAs(RoomState.Initial));
            Assert.That(session.CanUndo, Is.False);
            Assert.That(session.Undo(), Is.False);
            Assert.That(session.State, Is.SameAs(RoomState.Initial));
        }

        [Test]
        public void Undo_OnEmptyHistory_ReturnsFalseAndRaisesNothing()
        {
            var session = new PuzzleSession();
            int changes = 0;
            session.StateChanged += (_, __) => changes++;
            Assert.That(session.Undo(), Is.False);
            Assert.That(changes, Is.EqualTo(0));
            Assert.That(session.StepCount, Is.EqualTo(0));
        }

        [Test]
        public void Reset_AfterWin_ReturnsInitialWithEmptyHistory()
        {
            PuzzleSession session = Solved(out _);
            RoomState won = session.State;
            RoomState prev = null, curr = null;
            int changes = 0;
            session.StateChanged += (p, c) => { prev = p; curr = c; changes++; };

            session.Reset();

            Assert.That(session.State, Is.SameAs(RoomState.Initial));
            Assert.That(session.StepCount, Is.EqualTo(0));
            Assert.That(session.UndoDepth, Is.EqualTo(0));
            Assert.That(session.CanUndo, Is.False);
            Assert.That(session.Undo(), Is.False);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(prev, Is.SameAs(won));
            Assert.That(curr, Is.SameAs(RoomState.Initial));

            // Playable again.
            Assert.That(session.Submit("flow west").Accepted, Is.True);
        }

        [Test]
        public void UndoCapacity_IsCapped()
        {
            var session = new PuzzleSession(RoomState.Initial, undoCapacity: 3);
            var trail = new List<RoomState> { session.State };
            for (int i = 0; i < 6; i++)
            {
                Assert.That(session.Submit(CanonicalSolution[i]).Accepted, Is.True);
                trail.Add(session.State);
                Assert.That(session.UndoDepth, Is.EqualTo(Math.Min(i + 1, 3)));
            }
            Assert.That(session.StepCount, Is.EqualTo(6));

            Assert.That(session.Undo(), Is.True);
            Assert.That(session.State, Is.SameAs(trail[5]));
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.State, Is.SameAs(trail[4]));
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.State, Is.SameAs(trail[3]));
            Assert.That(session.Undo(), Is.False, "oldest entries were evicted");
            Assert.That(session.State, Is.SameAs(trail[3]));
            Assert.That(session.StepCount, Is.EqualTo(3));
        }

        [Test]
        public void UndoCapacity_Zero_DisablesUndo()
        {
            var session = new PuzzleSession(RoomState.Initial, undoCapacity: 0);
            Assert.That(session.Submit("flow west").Accepted, Is.True);
            Assert.That(session.UndoDepth, Is.EqualTo(0));
            Assert.That(session.Undo(), Is.False);
            Assert.That(session.StepCount, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_ValidatesArguments()
        {
            Assert.That(() => new PuzzleSession(null), Throws.ArgumentNullException);
            Assert.That(() => new PuzzleSession(RoomState.Initial, -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(PuzzleSession.DefaultUndoCapacity, Is.EqualTo(100));
        }

        [Test]
        public void DefaultCapacity_KeepsOneHundredEntries()
        {
            var session = new PuzzleSession();
            for (int i = 0; i < 120; i++)
            {
                Assert.That(session.Submit(i % 2 == 0 ? "flow west" : "flow east").Accepted, Is.True);
            }
            Assert.That(session.UndoDepth, Is.EqualTo(100));
            Assert.That(session.StepCount, Is.EqualTo(120));
        }

        [Test]
        public void Events_AcceptedCommand_RaisesResolvedThenChanged()
        {
            var session = new PuzzleSession();
            var log = new List<string>();
            CommandResult resolved = null;
            RoomState prev = null, curr = null;
            session.CommandResolved += r => { log.Add("resolved"); resolved = r; };
            session.StateChanged += (p, c) => { log.Add("changed"); prev = p; curr = c; Assert.That(session.State, Is.SameAs(c)); };

            SubmitResult sr = session.Submit("flow west");

            Assert.That(log, Is.EqualTo(new[] { "resolved", "changed" }));
            Assert.That(resolved, Is.SameAs(sr.Execution));
            Assert.That(resolved.Command, Is.EqualTo(Command.Flow(WaterSide.West)));
            Assert.That(prev, Is.SameAs(RoomState.Initial));
            Assert.That(curr, Is.SameAs(session.State));
            Assert.That(curr.Water, Is.EqualTo(WaterSide.West));
        }

        [Test]
        public void Events_RejectedCommand_RaisesResolvedOnly()
        {
            var session = new PuzzleSession();
            var log = new List<string>();
            CommandResult resolved = null;
            session.CommandResolved += r => { log.Add("resolved"); resolved = r; };
            session.StateChanged += (_, __) => log.Add("changed");

            SubmitResult sr = session.Submit("flow east");

            Assert.That(sr.Accepted, Is.False);
            Assert.That(log, Is.EqualTo(new[] { "resolved" }));
            Assert.That(resolved.Outcome, Is.EqualTo(Outcome.AlreadyInState));
            Assert.That(sr.Message, Is.EqualTo(resolved.Message));
        }

        [Test]
        public void Events_UnparsedText_RaisesNothing_AndMessageIsParserFeedback()
        {
            var session = new PuzzleSession();
            int events = 0;
            session.CommandResolved += _ => events++;
            session.StateChanged += (_, __) => events++;

            SubmitResult sr = session.Submit("fly west");

            Assert.That(events, Is.EqualTo(0));
            Assert.That(sr.Execution, Is.Null);
            Assert.That(sr.Accepted, Is.False);
            Assert.That(sr.Message, Is.EqualTo(sr.Parse.Feedback));
        }

        [Test]
        public void Events_UndoResetAndLoad_RaiseStateChangedWithCorrectArguments()
        {
            var session = new PuzzleSession();
            session.Submit("flow west");
            RoomState afterFlow = session.State;
            var calls = new List<(RoomState prev, RoomState curr)>();
            int resolved = 0;
            session.StateChanged += (p, c) => calls.Add((p, c));
            session.CommandResolved += _ => resolved++;

            session.Undo();
            RoomState loaded = ReadyAtGate();
            session.Load(loaded);
            session.Reset();

            Assert.That(resolved, Is.EqualTo(0));
            Assert.That(calls.Count, Is.EqualTo(3));
            Assert.That(calls[0].prev, Is.SameAs(afterFlow));
            Assert.That(calls[0].curr, Is.SameAs(RoomState.Initial));
            Assert.That(calls[1].prev, Is.SameAs(RoomState.Initial));
            Assert.That(calls[1].curr, Is.SameAs(loaded));
            Assert.That(calls[2].prev, Is.SameAs(loaded));
            Assert.That(calls[2].curr, Is.SameAs(RoomState.Initial));
        }

        [Test]
        public void Load_ClearsHistoryAndStepCount()
        {
            var session = new PuzzleSession();
            session.Submit("flow west");
            session.Submit("board elevator");
            session.Load(ReadyAtGate());
            Assert.That(session.StepCount, Is.EqualTo(0));
            Assert.That(session.UndoDepth, Is.EqualTo(0));
            Assert.That(() => session.Load(null), Throws.ArgumentNullException);
            Assert.That(session.Submit("he who returns the morning").Accepted, Is.True);
            Assert.That(session.State.Won, Is.True);
        }

        [Test]
        public void WonState_RejectsEveryCommandWithPuzzleComplete()
        {
            PuzzleSession session = Solved(out _);
            RoomState won = session.State;
            foreach (Command c in RoomRules.AllCommands)
            {
                CommandResult r = session.Execute(c);
                Assert.That(r.Outcome, Is.EqualTo(Outcome.PuzzleComplete), c.ToString());
                Assert.That(session.State, Is.SameAs(won));
                Assert.That(session.StepCount, Is.EqualTo(25));
                Assert.That(session.UndoDepth, Is.EqualTo(25));
            }
        }

        [Test]
        public void WonState_RejectsEveryCommandWithPuzzleComplete_InEveryReachableWonState()
        {
            foreach (RoomState s in ReachableStates)
            {
                if (!s.Won) continue;
                foreach (Command c in RoomRules.AllCommands)
                {
                    CommandResult r = RoomRules.Apply(s, c);
                    Assert.That(r.Outcome, Is.EqualTo(Outcome.PuzzleComplete));
                    Assert.That(r.State, Is.SameAs(s));
                }
            }
        }

        [Test]
        public void UndoAfterWin_LeavesWonState()
        {
            PuzzleSession session = Solved(out var trail);
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.State.Won, Is.False);
            Assert.That(session.State, Is.SameAs(trail[24]));
            Assert.That(session.Submit("speak he who returns the morning").Accepted, Is.True);
            Assert.That(session.State.Won, Is.True);
        }
    }
}
