using System;
using System.Collections.Generic;
using Ankhu.Integration;
using Ankhu.Speech;
using UnityEngine;
using UnityEngine.Audio;

namespace Ankhu.Voice
{
    /// <summary>
    /// Speaks Ankhu's spell aloud when the speech bubble submits an utterance the rules accepted.
    /// Recordings are scheduled on the audio clock (AudioSource.PlayScheduled), so the gaps between
    /// phrases and words are sample accurate and do not depend on frame rate. A new spell cuts off the
    /// one still sounding. The voice is dry and non-spatial: room reverb belongs on the mixer group.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpellVoicePlayer : MonoBehaviour
    {
        [SerializeField] private SpellVoiceLibrary library;
        [Tooltip("Mixer group for Ankhu's voice (reverb send, ducking). Optional.")]
        [SerializeField] private AudioMixerGroup output;
        [Range(0f, 1f)] [SerializeField] private float volume = 1f;
        [Tooltip("Also voice typed deeds such as 'take the mirror'. Words of power and Names are always voiced.")]
        [SerializeField] private bool voiceDeeds;
        [Tooltip("Also voice spells the rules accepted but that changed nothing (for example 'flow east' when the water is already east).")]
        [SerializeField] private bool voiceNoChange;
        [Tooltip("Seconds between Enter and the first sound. Scheduling needs a little lead to be sample accurate.")]
        [SerializeField] private float scheduleLead = 0.05f;

        private readonly List<AudioSource> pool = new List<AudioSource>();
        private SpeechBubbleController bubble;
        private double speechEndDsp;

        /// <summary>Set by the room binding. Without one, the player stays silent.</summary>
        public ISpellVoiceResolver Resolver { get; set; }

        public SpellVoiceLibrary Library
        {
            get => library;
            set => library = value;
        }

        public bool VoiceDeeds
        {
            get => voiceDeeds;
            set => voiceDeeds = value;
        }

        public bool IsSpeaking => AudioSettings.dspTime < speechEndDsp;

        /// <summary>Raised when a spell starts sounding, with the spell and its length in seconds (for mouth animation or subtitles).</summary>
        public event Action<SpokenSpell, VoicePlan> SpeechStarted;

        private void Update()
        {
            if (bubble == null)
            {
                bubble = UnityEngine.Object.FindAnyObjectByType<SpeechBubbleController>();
                if (bubble != null)
                {
                    bubble.Spoken += HandleSpoken;
                }
            }
        }

        private void OnDisable()
        {
            if (bubble != null)
            {
                bubble.Spoken -= HandleSpoken;
                bubble = null;
            }
            Stop();
        }

        private void HandleSpoken(string utterance, CommandResult result)
        {
            if (result == null || Resolver == null)
            {
                return;
            }
            bool voiced = result.Outcome == CommandOutcome.Success || (voiceNoChange && result.Outcome == CommandOutcome.NoChange);
            if (!voiced)
            {
                return;
            }
            SpokenSpell spell = Resolver.Resolve(utterance, result);
            if (spell == null || (spell.Category == SpellVoiceCategory.Deed && !voiceDeeds))
            {
                return;
            }
            Speak(spell);
        }

        /// <summary>Speaks a spell now. Returns the plan, or null when nothing could be voiced.</summary>
        public VoicePlan Speak(SpokenSpell spell)
        {
            if (spell == null || library == null)
            {
                return null;
            }
            VoicePlan plan = SpellVoicePlanner.Plan(spell, library, library.Timing);
            if (plan.Missing.Count > 0)
            {
                Debug.LogWarning("Ankhu's voice has no recording for: " + string.Join(", ", plan.Missing) + " (spell: " + spell + ")", this);
            }
            if (plan.Cues.Count == 0)
            {
                return null;
            }

            Stop();
            EnsurePool(plan.Cues.Count);
            double t0 = AudioSettings.dspTime + Math.Max(0.0, scheduleLead);
            for (int i = 0; i < plan.Cues.Count; i++)
            {
                VoiceCue cue = plan.Cues[i];
                AudioSource source = pool[i];
                source.clip = library.GetClip(cue.Clip);
                source.volume = volume;
                source.outputAudioMixerGroup = output;
                source.PlayScheduled(t0 + cue.Start);
            }
            speechEndDsp = t0 + plan.Duration;
            SpeechStarted?.Invoke(spell, plan);
            return plan;
        }

        public void Stop()
        {
            foreach (AudioSource s in pool)
            {
                if (s != null)
                {
                    s.Stop();
                }
            }
            speechEndDsp = 0;
        }

        private void EnsurePool(int count)
        {
            while (pool.Count < count)
            {
                var go = new GameObject("Ankhu Voice " + pool.Count);
                go.transform.SetParent(transform, false);
                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.spatialBlend = 0f;
                s.priority = 32;
                s.dopplerLevel = 0f;
                pool.Add(s);
            }
        }
    }
}
