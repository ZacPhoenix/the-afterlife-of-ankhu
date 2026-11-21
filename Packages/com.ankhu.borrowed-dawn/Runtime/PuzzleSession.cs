using System;
using System.Collections.Generic;

namespace Ankhu.BorrowedDawn
{
    /// <summary>Outcome of submitting typed text: the parse and, when parsing succeeded, the rule result.</summary>
    public sealed class SubmitResult
    {
        public ParseResult Parse { get; }
        /// <summary>Null when the text did not parse.</summary>
        public CommandResult Execution { get; }
        public bool Accepted => Execution != null && Execution.Accepted;
        public string Message => Execution != null ? Execution.Message : Parse.Feedback;

        internal SubmitResult(ParseResult parse, CommandResult execution)
        {
            Parse = parse;
            Execution = execution;
        }
    }

    /// <summary>
    /// Mutable wrapper around the pure rules: current state, undo history, reset, and a change event.
    /// One instance per room. Not thread safe. A Unity adapter owns one and subscribes to <see cref="StateChanged"/>.
    /// </summary>
    public sealed class PuzzleSession
    {
        /// <summary>Matches the prototype's history cap.</summary>
        public const int DefaultUndoCapacity = 100;

        private readonly LinkedList<RoomState> _undo = new LinkedList<RoomState>();
        private readonly int _undoCapacity;

        public RoomState State { get; private set; }

        /// <summary>Rule set used for every command in this session.</summary>
        public RuleProfile Profile { get; }

        /// <summary>Count of accepted actions since the last reset, minus undone actions.</summary>
        public int StepCount { get; private set; }

        public int UndoDepth => _undo.Count;

        public bool CanUndo => _undo.Count > 0;

        /// <summary>Raised after every state change (accepted command, undo, reset). Arguments: previous, current.</summary>
        public event Action<RoomState, RoomState> StateChanged;

        /// <summary>Raised for every command attempt, accepted or not, so presentation can give feedback.</summary>
        public event Action<CommandResult> CommandResolved;

        public PuzzleSession() : this(RoomState.Initial, DefaultUndoCapacity) { }

        public PuzzleSession(RuleProfile profile) : this(RoomState.Initial, DefaultUndoCapacity, profile) { }

        public PuzzleSession(RoomState start, int undoCapacity = DefaultUndoCapacity, RuleProfile profile = RuleProfile.Prototype)
        {
            Profile = profile;
            if (undoCapacity < 0) throw new ArgumentOutOfRangeException(nameof(undoCapacity));
            State = start ?? throw new ArgumentNullException(nameof(start));
            _undoCapacity = undoCapacity;
        }

        public CommandResult Execute(Command command)
        {
            RoomState before = State;
            CommandResult result = RoomRules.Apply(before, command, Profile);
            if (result.Accepted)
            {
                if (_undoCapacity > 0)
                {
                    _undo.AddLast(before);
                    if (_undo.Count > _undoCapacity) _undo.RemoveFirst();
                }
                State = result.State;
                StepCount++;
            }
            CommandResolved?.Invoke(result);
            if (result.Accepted)
            {
                StateChanged?.Invoke(before, State);
            }
            return result;
        }

        /// <summary>Parses then executes. Parse failures never touch state, history or step count.</summary>
        public SubmitResult Submit(string text)
        {
            ParseResult parse = SpellParser.Parse(text);
            if (!parse.Success)
            {
                return new SubmitResult(parse, null);
            }
            return new SubmitResult(parse, Execute(parse.Command));
        }

        /// <summary>Restores the state before the last accepted command. Returns false when there is nothing to undo.</summary>
        public bool Undo()
        {
            if (_undo.Count == 0) return false;
            RoomState before = State;
            State = _undo.Last.Value;
            _undo.RemoveLast();
            StepCount = Math.Max(0, StepCount - 1);
            StateChanged?.Invoke(before, State);
            return true;
        }

        /// <summary>Returns to the initial state and clears history. Always available, including after victory.</summary>
        public void Reset()
        {
            RoomState before = State;
            State = RoomState.Initial;
            _undo.Clear();
            StepCount = 0;
            StateChanged?.Invoke(before, State);
        }

        /// <summary>Restores a saved state (see <see cref="RoomState.Pack"/>). Clears undo history.</summary>
        public void Load(RoomState state)
        {
            RoomState before = State;
            State = state ?? throw new ArgumentNullException(nameof(state));
            _undo.Clear();
            StepCount = 0;
            StateChanged?.Invoke(before, State);
        }
    }
}
