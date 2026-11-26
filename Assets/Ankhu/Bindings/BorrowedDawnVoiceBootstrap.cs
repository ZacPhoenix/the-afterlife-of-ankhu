using Ankhu.Voice;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ankhu.Bindings
{
    /// <summary>
    /// Gives a Borrowed Dawn scene Ankhu's spoken voice without touching the scene file: after a scene
    /// loads, if it has a <see cref="BorrowedDawnRulesInstaller"/> and no <see cref="SpellVoicePlayer"/>,
    /// one is added beside the installer with the library from Resources and the Borrowed Dawn resolver.
    /// A player placed in the scene by hand is kept and only given a resolver if it lacks one.
    /// </summary>
    public static class BorrowedDawnVoiceBootstrap
    {
        private static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            hooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (scene, mode) => Install();
            }
            Install();
        }

        public static SpellVoicePlayer Install()
        {
            var installer = Object.FindAnyObjectByType<BorrowedDawnRulesInstaller>();
            if (installer == null)
            {
                return null;
            }
            var player = Object.FindAnyObjectByType<SpellVoicePlayer>();
            if (player == null)
            {
                var library = Resources.Load<SpellVoiceLibrary>(SpellVoiceLibrary.ResourcePath);
                if (library == null)
                {
                    Debug.Log("Ankhu's spell voice is off: no SpellVoiceLibrary at Resources/" + SpellVoiceLibrary.ResourcePath +
                              ". Run Ankhu > Rebuild Spell Voice Library.");
                    return null;
                }
                player = installer.gameObject.AddComponent<SpellVoicePlayer>();
                player.Library = library;
            }
            if (player.Resolver == null)
            {
                player.Resolver = new BorrowedDawnSpellVoice();
            }
            return player;
        }
    }
}
