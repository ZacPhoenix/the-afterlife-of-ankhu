using Ankhu.Voice;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ankhu.Bindings
{
    /// <summary>
    /// Gives Anpu his voice in the teaching rooms without touching scene files. After a scene loads, if it has a
    /// <see cref="TeachingRoomDirector"/>, a <see cref="DialogueVoicePlayer"/> with the Anpu library from Resources
    /// is added beside it and plays each line when <see cref="TeachingRoomDirector.AnpuLineStarted"/> fires.
    /// Without the library (before Anpu's voice is recorded) nothing is added and the subtitles run as before.
    /// </summary>
    public static class AnpuVoiceBootstrap
    {
        public const string ResourcePath = "AnpuDialogueVoice";

        private static bool hooked;
        private static DialogueVoicePlayer player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            hooked = false;
            player = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (scene, mode) => Install();
                TeachingRoomDirector.AnpuLineStarted += HandleLine;
            }
            Install();
        }

        public static DialogueVoicePlayer Install()
        {
            var director = Object.FindAnyObjectByType<TeachingRoomDirector>();
            if (director == null)
            {
                return null;
            }
            player = director.GetComponentInChildren<DialogueVoicePlayer>();
            if (player == null)
            {
                var library = Resources.Load<SpellVoiceLibrary>(ResourcePath);
                if (library == null)
                {
                    return null;
                }
                // Zac 2026-11-25: on its own child so Anpu's VoiceTone (gentle low-end roll-off) filters only his source.
                var go = new GameObject("AnpuVoice");
                go.transform.SetParent(director.transform, false);
                Ankhu.Opening.VoiceTone.AddAnpu(go);
                player = go.AddComponent<DialogueVoicePlayer>();
                player.Library = library;
            }
            return player;
        }

        /// <summary>Length of Anpu's recording for a line key, or 0. Subtitle timing can hold a line at least this long.</summary>
        public static float DurationOf(string key) => player != null ? player.DurationOf(key) : 0f;

        private static void HandleLine(string key, string text)
        {
            if (player == null)
            {
                Install();
            }
            if (player != null)
            {
                player.Play(key);
            }
        }
    }
}
