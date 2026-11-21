namespace Ankhu.Core
{
    /// <summary>
    /// Global arbiter of keyboard ownership. While text entry is active, gameplay input
    /// (movement, climbing, interaction, hotkeys) must read as idle. Every gameplay input
    /// reader checks <see cref="GameplayInputAllowed"/> before acting.
    /// </summary>
    public static class InputGate
    {
        public static bool TextEntryActive { get; private set; }

        /// <summary>Frame on which text entry last closed. Keys released on that frame are ignored by gameplay.</summary>
        public static int TextEntryClosedFrame { get; private set; } = -1;

        public static bool GameplayInputAllowed =>
            !TextEntryActive && UnityEngine.Time.frameCount != TextEntryClosedFrame;

        public static void BeginTextEntry()
        {
            TextEntryActive = true;
        }

        public static void EndTextEntry()
        {
            TextEntryActive = false;
            TextEntryClosedFrame = UnityEngine.Time.frameCount;
        }

        /// <summary>Clears static state when entering play mode with domain reload disabled.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            TextEntryActive = false;
            TextEntryClosedFrame = -1;
        }
    }
}
