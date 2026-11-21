using UnityEditor;
using UnityEngine;

namespace Ankhu.EditorTools
{
    /// <summary>
    /// A script recompile in the middle of play mode reloads the C# domain. Plain C# objects such as
    /// the rules adapter do not survive it, so the room keeps running with no rules. This guard sets
    /// the editor preference "Script Changes While Playing" to "Recompile After Finished Playing" for
    /// this machine when it is still on the default "Recompile And Continue Playing".
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeRecompileGuard
    {
        private const string PrefKey = "ScriptCompilationDuringPlay";
        private const int RecompileAndContinue = 0;
        private const int RecompileAfterFinishedPlaying = 1;

        static PlayModeRecompileGuard()
        {
            if (EditorPrefs.GetInt(PrefKey, RecompileAndContinue) == RecompileAndContinue)
            {
                EditorPrefs.SetInt(PrefKey, RecompileAfterFinishedPlaying);
                Debug.Log("Ankhu: set Preferences > General > Script Changes While Playing to \"Recompile After Finished Playing\" so code changes never disconnect the rules mid-play.");
            }
        }
    }
}
