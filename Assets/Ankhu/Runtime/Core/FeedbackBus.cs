using System;
using Ankhu.Integration;

namespace Ankhu.Core
{
    public enum FeedbackSource
    {
        Speech,
        Interaction,
        Inspect,
        Hint,
        System
    }

    /// <summary>
    /// One-way channel from gameplay to on-screen text. The HUD log and the speech bubble's
    /// reply line subscribe. Publishing never touches puzzle state.
    /// </summary>
    public static class FeedbackBus
    {
        public static event Action<FeedbackSource, CommandOutcome, string> Published;

        public static void Publish(FeedbackSource source, CommandOutcome outcome, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }
            Published?.Invoke(source, outcome, message);
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Published = null;
        }
    }
}
