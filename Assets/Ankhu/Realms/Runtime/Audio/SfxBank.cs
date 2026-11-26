using System.Collections.Generic;
using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// Authored sound effects made with earshot, loaded from Resources/Sfx/&lt;family&gt;/&lt;name&gt;/ (the clips live in
    /// Assets/Ankhu/Audio/Sfx/Resources). Every file in a folder is one take. <see cref="Play"/> picks a random take,
    /// never the same one twice in a row, with a little pitch drift so repeats don't sound copied.
    /// A missing folder warns once and stays silent.
    /// </summary>
    public sealed class SfxBank : MonoBehaviour
    {
        private static SfxBank instance;
        private readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        private readonly Dictionary<string, int> lastTake = new Dictionary<string, int>();
        private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        private AudioSource ui;
        private readonly List<AudioSource> world = new List<AudioSource>();
        private int nextWorld;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; }

        private static SfxBank Get()
        {
            if (instance != null) return instance;
            var go = new GameObject("SfxBank");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SfxBank>();
            return instance;
        }

        private void Awake()
        {
            ui = gameObject.AddComponent<AudioSource>();
            ui.playOnAwake = false;
            ui.spatialBlend = 0f;
            ui.ignoreListenerPause = true;
            for (int i = 0; i < 8; i++)
            {
                var go = new GameObject("src" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0.5f; // placed in the room, never lost off screen
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 3f;
                s.maxDistance = 40f;
                world.Add(s);
            }
        }

        /// <summary>True when Resources/Sfx/key holds at least one clip.</summary>
        public static bool Has(string key) => Get().Takes(key).Length > 0;

        /// <summary>Plays a take of Resources/Sfx/key. minGap drops calls closer together than that, in seconds.</summary>
        public static void Play(string key, float volume = 1f, Vector3? at = null, float minGap = 0.04f, float pitchDrift = 0.04f)
        {
            SfxBank b = Get();
            float now = Time.unscaledTime;
            if (b.lastPlayed.TryGetValue(key, out float t) && now - t < minGap) return;
            AudioClip c = b.Pick(key);
            if (c == null) return;
            b.lastPlayed[key] = now;
            float pitch = 1f + Random.Range(-pitchDrift, pitchDrift);
            if (at == null)
            {
                b.ui.pitch = pitch;
                b.ui.PlayOneShot(c, volume);
                return;
            }
            AudioSource s = b.world[b.nextWorld];
            b.nextWorld = (b.nextWorld + 1) % b.world.Count;
            s.Stop();
            s.transform.position = at.Value;
            s.pitch = pitch;
            s.clip = c;
            s.volume = volume;
            s.Play();
        }

        private AudioClip[] Takes(string key)
        {
            if (!clips.TryGetValue(key, out AudioClip[] set))
            {
                set = Resources.LoadAll<AudioClip>("Sfx/" + key);
                if (set.Length == 0) Debug.LogWarning("[SfxBank] no clips at Resources/Sfx/" + key);
                clips[key] = set;
            }
            return set;
        }

        private AudioClip Pick(string key)
        {
            AudioClip[] set = Takes(key);
            if (set.Length == 0) return null;
            int i = Random.Range(0, set.Length);
            if (set.Length > 1 && lastTake.TryGetValue(key, out int last) && i == last) i = (i + 1) % set.Length;
            lastTake[key] = i;
            return set[i];
        }
    }
}
