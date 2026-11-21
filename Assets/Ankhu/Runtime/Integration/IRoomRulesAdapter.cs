using System;

namespace Ankhu.Integration
{
    /// <summary>
    /// The only surface the Unity greybox uses to talk to puzzle rules.
    ///
    /// Contract:
    /// 1. Every call is synchronous and deterministic. No call requires a network or an LLM.
    /// 2. A command either succeeds and replaces <see cref="Current"/> atomically, or fails and
    ///    leaves <see cref="Current"/> untouched. Partial effects are not allowed.
    /// 3. <see cref="StateChanged"/> fires exactly once per successful state replacement
    ///    (Speak, Interact, Undo, Reset), after <see cref="Current"/> already holds the new value.
    /// 4. Text normalization (case, whitespace, punctuation, articles, aliases) happens on the
    ///    rules side. The greybox passes the raw typed string.
    /// 5. The adapter never references UnityEngine, so the rules library can stay engine free.
    /// </summary>
    public interface IRoomRulesAdapter
    {
        RoomSnapshot Current { get; }

        /// <summary>Raised with (previous, current) after every accepted state replacement.</summary>
        event Action<RoomSnapshot, RoomSnapshot> StateChanged;

        /// <summary>Submit one typed utterance from the speech bubble.</summary>
        CommandResult Speak(string rawText, PlayerContext context);

        /// <summary>Request a physical interaction by id. See <see cref="InteractionIds"/>.</summary>
        CommandResult Interact(string interactionId, PlayerContext context);

        /// <summary>Restore the previous accepted state. Returns NoChange when there is nothing to undo.</summary>
        CommandResult Undo();

        /// <summary>Return to the initial state and clear undo history.</summary>
        void Reset();

        /// <summary>A short, non-spoiling hint for the current state and position.</summary>
        string Hint(PlayerContext context);

        /// <summary>Serialize the logical state for save files. Format is owned by the rules side.</summary>
        string SaveState();

        /// <summary>Restore from <see cref="SaveState"/> output. Returns false and changes nothing if the payload is invalid.</summary>
        bool LoadState(string payload);
    }
}
