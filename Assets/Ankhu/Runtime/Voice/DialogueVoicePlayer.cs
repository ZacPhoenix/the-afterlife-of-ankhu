using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Ankhu.Voice
{
    /// <summary>
    /// Plays one character's pre-recorded dialogue by line key (for Anpu: dialogue.anpu.&lt;room&gt;.intro.1 and so on).
    /// A new line cuts off the one still sounding. Missing keys are logged once and stay silent, so subtitles
    /// keep working before a voice exists. Non-spatial and dry: room reverb belongs on the mixer group.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueVoicePlayer : MonoBehaviour
    {
        [SerializeField] private SpellVoiceLibrary library;
        [SerializeField] private AudioMixerGroup output;
        [Range(0f, 1f)] [SerializeField] private float volume = 1f;

        private AudioSource source;
        private readonly System.Collections.Generic.HashSet<string> reportedMissing = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

        public SpellVoiceLibrary Library
        {
            get => library;
            set => library = value;
        }

        public bool IsSpeaking => source != null && source.isPlaying;

        /// <summary>Raised when a line starts sounding: (key, clip length in seconds).</summary>
        public event Action<string, float> LineStarted;

        /// <summary>Length in seconds of the recording for a key, or 0 when there is none. For subtitle timing.</summary>
        public float DurationOf(string key)
        {
            if (library == null || !library.Contains(key))
            {
                return 0f;
            }
            foreach (SpellVoiceLibrary.Entry e in library.Entries)
            {
                if (e.key == key && e.variants != null && e.variants.Length > 0 && e.variants[0] != null)
                {
                    return e.variants[0].length;
                }
            }
            return 0f;
        }

        /// <summary>Plays the line for a key. Returns its length in seconds, or 0 when nothing played.</summary>
        public float Play(string key)
        {
            if (library == null || string.IsNullOrEmpty(key))
            {
                return 0f;
            }
            if (!library.TryPick(key, out VoiceClipChoice choice))
            {
                if (reportedMissing.Add(key))
                {
                    Debug.Log("No recording yet for dialogue line " + key + ".", this);
                }
                return 0f;
            }
            AudioClip clip = library.GetClip(choice);
            if (clip == null)
            {
                return 0f;
            }
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.priority = 16;
            }
            source.Stop();
            source.clip = clip;
            source.volume = volume;
            source.outputAudioMixerGroup = output;
            source.Play();
            LineStarted?.Invoke(key, clip.length);
            return clip.length;
        }

        public void Stop()
        {
            if (source != null)
            {
                source.Stop();
            }
        }
    }
}
