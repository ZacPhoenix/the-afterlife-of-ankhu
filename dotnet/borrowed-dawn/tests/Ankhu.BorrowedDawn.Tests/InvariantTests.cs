using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Ankhu.BorrowedDawn.Tests.TestSupport;

namespace Ankhu.BorrowedDawn.Tests
{
    /// <summary>Invalid commands never mutate anything, in the rules or in the session.</summary>
    [TestFixture]
    public class InvariantTests
    {
        private static readonly string[] Garbage =
        {
            "", "   ", "the", "fly west", "bind", "bind gate", "flow north", "flow west now",
            "he who returns the dawn", "speak", "xyzzy", "!!!", "go to moon",
        };

        [Test]
        public void Apply_RejectionReturnsInputStateObject_ForEveryReachableStateAndCommand()
        {
            int rejections = 0;
            foreach (RoomState s in ReachableStates)
            {
                int before = s.Pack();
                foreach (Command c in RoomRules.AllCommands)
                {
                    CommandResult r = RoomRules.Apply(s, c);
                    Assert.That(r.Previous, Is.SameAs(s));
                    Assert.That(r.Command, Is.SameAs(c));
                    Assert.That(r.Message, Is.Not.Null.And.Not.Empty);
                    if (!r.Accepted)
                    {
                        rejections++;
                        Assert.That(r.State, Is.SameAs(s), $"{c} from [{s}] -> {r.Outcome}");
                        Assert.That(r.Outcome, Is.Not.EqualTo(Outcome.Accepted));
                    }
                    else
                    {
                        Assert.That(r.State, Is.Not.EqualTo(s), $"accepted {c} from [{s}] should change state");
                    }
                    Assert.That(s.Pack(), Is.EqualTo(before), "input state must never mutate");
                }
            }
            Assert.That(rejections, Is.GreaterThan(0));
        }

        [Test]
        public void Apply_IsDeterministic()
        {
            foreach (RoomState s in ReachableStates.Take(200))
            {
                foreach (Command c in RoomRules.AllCommands)
                {
                    CommandResult a = RoomRules.Apply(s, c);
                    CommandResult b = RoomRules.Apply(s, c);
                    Assert.That(a.Outcome, Is.EqualTo(b.Outcome));
                    Assert.That(a.State, Is.EqualTo(b.State));
                    Assert.That(a.Message, Is.EqualTo(b.Message));
                }
            }
        }

        [Test]
        public void Apply_NullArguments_Throw()
        {
            Assert.That(() => RoomRules.Apply(null, Command.WakeBearer), Throws.ArgumentNullException);
            Assert.That(() => RoomRules.Apply(RoomState.Initial, null), Throws.ArgumentNullException);
        }

        private static IEnumerable<string> AllInputs() =>
            RoomRules.AllCommands.Select(c => c.ToString()).Concat(Garbage);

        [Test]
        public void Session_RejectedOrUnparsedSubmissions_LeaveSessionUntouched_FromEveryReachableState()
        {
            var session = new PuzzleSession();
            int stateChanges = 0;
            session.StateChanged += (_, __) => stateChanges++;

            // Seed some history so UndoDepth/StepCount are non-trivial.
            foreach (string step in CanonicalSolution.Take(3)) session.Submit(step);

            foreach (RoomState s in ReachableStates)
            {
                session.Load(s);
                session.Execute(Command.Flow(s.Water == WaterSide.East ? WaterSide.West : WaterSide.East)); // gives one undo entry when not won
                RoomState anchor = session.State;
                int steps = session.StepCount;
                int depth = session.UndoDepth;

                foreach (string text in AllInputs())
                {
                    int changesBefore = stateChanges;
                    SubmitResult r = session.Submit(text);
                    if (r.Accepted)
                    {
                        Assert.That(session.Undo(), Is.True);
                        TestSupport.AssertStatesIdentical(session.State, anchor);
                        Assert.That(session.StepCount, Is.EqualTo(steps));
                        Assert.That(session.UndoDepth, Is.EqualTo(depth));
                        continue;
                    }
                    Assert.That(session.State, Is.SameAs(anchor), $"'{text}' from [{anchor}]");
                    Assert.That(session.StepCount, Is.EqualTo(steps));
                    Assert.That(session.UndoDepth, Is.EqualTo(depth));
                    Assert.That(stateChanges, Is.EqualTo(changesBefore), "no StateChanged on rejection");
                    Assert.That(r.Message, Is.Not.Null);
                    if (!r.Parse.Success)
                    {
                        Assert.That(r.Execution, Is.Null);
                    }
                }
            }
        }

        [Test]
        public void Session_UnparsedSubmission_DoesNotRaiseCommandResolved()
        {
            var session = new PuzzleSession();
            int resolved = 0;
            session.CommandResolved += _ => resolved++;
            foreach (string g in Garbage) session.Submit(g);
            session.Submit(null);
            Assert.That(resolved, Is.EqualTo(0));
            Assert.That(session.State, Is.SameAs(RoomState.Initial));
            Assert.That(session.StepCount, Is.EqualTo(0));
            Assert.That(session.UndoDepth, Is.EqualTo(0));
        }
    }
}
