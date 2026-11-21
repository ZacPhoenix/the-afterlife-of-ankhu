using System;
using System.Collections.Generic;

namespace Ankhu.Integration
{
    public enum CommandOutcome
    {
        /// <summary>The command was valid and the state changed atomically.</summary>
        Success,
        /// <summary>The command was understood and valid but changed nothing (for example "flow east" when water is already east).</summary>
        NoChange,
        /// <summary>No known verb or phrase.</summary>
        NotUnderstood,
        /// <summary>A known verb with an unknown or invalid target.</summary>
        InvalidTarget,
        /// <summary>A known verb with a missing target or argument.</summary>
        Incomplete,
        /// <summary>Understood, but a precondition failed. State is untouched.</summary>
        Rejected,
        /// <summary>No rules library is connected. Used by the traversal test adapter.</summary>
        NotConnected
    }

    public sealed class CommandResult
    {
        private static readonly IReadOnlyList<string> NoDiscoveries = Array.Empty<string>();

        public CommandOutcome Outcome { get; }
        public string Message { get; }
        /// <summary>Stable codex entry ids discovered by this command, in order.</summary>
        public IReadOnlyList<string> CodexDiscoveries { get; }

        public bool Accepted => Outcome == CommandOutcome.Success;

        public CommandResult(CommandOutcome outcome, string message, IReadOnlyList<string> codexDiscoveries = null)
        {
            Outcome = outcome;
            Message = message ?? string.Empty;
            CodexDiscoveries = codexDiscoveries ?? NoDiscoveries;
        }

        public override string ToString() => $"{Outcome}: {Message}";
    }
}
