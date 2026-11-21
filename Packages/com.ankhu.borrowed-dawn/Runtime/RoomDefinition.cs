using System;
using System.Collections.Generic;

namespace Ankhu.BorrowedDawn
{
    /// <summary>
    /// A playable room built on the Borrowed Dawn rules: a start state, the spells Ankhu has been taught so far,
    /// and a goal. Teaching rooms restrict the vocabulary and change the goal. Borrowed Dawn itself allows every
    /// spell and ends when the gate opens.
    /// </summary>
    public sealed class RoomDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public RuleProfile Profile { get; }
        public RoomState Start { get; }

        /// <summary>One line on what the room teaches, for designers and the codex.</summary>
        public string Lesson { get; }

        /// <summary>Anpu's lines on entering, in order. Provisional dialogue, not final script.</summary>
        public IReadOnlyList<string> AnpuIntro { get; }

        /// <summary>Anpu's line when the room is solved.</summary>
        public string AnpuOnSolve { get; }

        /// <summary>
        /// Anpu's nudges keyed by rejection outcome. Presentation may show one instead of the rules message the
        /// first time the player hits that outcome. Never names a full solution.
        /// </summary>
        public IReadOnlyDictionary<Outcome, string> AnpuNudges { get; }

        private readonly Func<Command, bool> _allows;
        private readonly Func<RoomState, bool> _isGoal;

        public RoomDefinition(
            string id,
            string title,
            RuleProfile profile,
            RoomState start,
            Func<Command, bool> allows,
            Func<RoomState, bool> isGoal,
            string lesson = null,
            IReadOnlyList<string> anpuIntro = null,
            string anpuOnSolve = null,
            IReadOnlyDictionary<Outcome, string> anpuNudges = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Title = title ?? id;
            Profile = profile;
            Start = start ?? throw new ArgumentNullException(nameof(start));
            _allows = allows ?? (c => true);
            _isGoal = isGoal ?? (s => s.Won);
            Lesson = lesson ?? string.Empty;
            AnpuIntro = anpuIntro ?? Array.Empty<string>();
            AnpuOnSolve = anpuOnSolve ?? string.Empty;
            AnpuNudges = anpuNudges ?? new Dictionary<Outcome, string>();
        }

        /// <summary>True when Ankhu knows this spell or action in this room.</summary>
        public bool Allows(Command command) => _allows(command);

        /// <summary>True when the room's goal holds. A solved room accepts no further commands.</summary>
        public bool IsGoal(RoomState state) => _isGoal(state);

        /// <summary>Applies a command under this room's vocabulary, goal and rule profile.</summary>
        public CommandResult Apply(RoomState state, Command command)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (command is null) throw new ArgumentNullException(nameof(command));
            if (IsGoal(state) && !state.Won)
            {
                return new CommandResult(Outcome.PuzzleComplete, state, state, command, "This chamber is already open.");
            }
            if (!Allows(command))
            {
                return new CommandResult(Outcome.NotYetTaught, state, state, command, "Those words have no power here yet.");
            }
            return RoomRules.Apply(state, command, Profile);
        }

        /// <summary>The full Borrowed Dawn room under the given profile.</summary>
        public static RoomDefinition BorrowedDawn(RuleProfile profile) => new RoomDefinition(
            "borrowed-dawn",
            "The Borrowed Dawn",
            profile,
            RoomState.Initial,
            allows: null,
            isGoal: null,
            lesson: "Water, binding, the lingering shadow, the remembered bell and a discovered Name, all at once.");
    }
}
