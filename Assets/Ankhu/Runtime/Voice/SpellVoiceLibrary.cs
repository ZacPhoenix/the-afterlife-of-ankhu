using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ankhu.Voice
{
    /// <summary>
    /// Ankhu's pre-recorded spell voice: every phrase and word take, keyed as in tools/voice/voice_lines.json.
    /// Built by the editor menu Ankhu &gt; Rebuild Spell Voice Library from Assets/Ankhu/Audio/Voice/voice_manifest.json.
    /// Takes of one key rotate so a repeated spell never plays the same recording twice in a row.
    /// </summary>
    [CreateAssetMenu(menuName = "Ankhu/Spell Voice Library", fileName = "AnkhuSpellVoice")]
    public sealed class SpellVoiceLibrary : ScriptableObject, IVoiceClipCatalog
    {
        /// <summary>Resources path the Borrowed Dawn bootstrap loads.</summary>
        public const string ResourcePath = "AnkhuSpellVoice";

        [Serializable]
        public sealed class Entry
        {
            public string key;
            public AudioClip[] variants = Array.Empty<AudioClip>();
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField] private SpellVoiceTiming timing = SpellVoiceTiming.Default;
        [Tooltip("Integrated loudness target the clips were normalised to (active speech level, dB). Informational.")]
        [SerializeField] private float targetLevelDb = -20f;

        private Dictionary<string, Entry> index;
        private readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>(StringComparer.Ordinal);

        public SpellVoiceTiming Timing => timing;
        public float TargetLevelDb => targetLevelDb;
        public int Count => entries.Count;
        public IReadOnlyList<Entry> Entries => entries;

        public void SetContents(List<Entry> newEntries, SpellVoiceTiming newTiming, float newTargetLevelDb)
        {
            entries = newEntries ?? new List<Entry>();
            timing = newTiming;
            targetLevelDb = newTargetLevelDb;
            index = null;
            lastVariant.Clear();
        }

        private void OnEnable()
        {
            index = null;
            lastVariant.Clear();
        }

        private Dictionary<string, Entry> Index
        {
            get
            {
                if (index == null)
                {
                    index = new Dictionary<string, Entry>(StringComparer.Ordinal);
                    foreach (Entry e in entries)
                    {
                        if (e != null && !string.IsNullOrEmpty(e.key) && HasClip(e))
                        {
                            index[e.key] = e;
                        }
                    }
                }
                return index;
            }
        }

        private static bool HasClip(Entry e)
        {
            if (e.variants == null) return false;
            foreach (AudioClip c in e.variants)
            {
                if (c != null) return true;
            }
            return false;
        }

        public bool Contains(string key) => key != null && Index.ContainsKey(key);

        public bool TryPick(string key, out VoiceClipChoice choice)
        {
            choice = default;
            if (key == null || !Index.TryGetValue(key, out Entry e))
            {
                return false;
            }
            int n = e.variants.Length;
            int start = lastVariant.TryGetValue(key, out int last) ? last + 1 : 0;
            for (int k = 0; k < n; k++)
            {
                int v = (start + k) % n;
                AudioClip clip = e.variants[v];
                if (clip != null)
                {
                    lastVariant[key] = v;
                    choice = new VoiceClipChoice(key, v, clip.length);
                    return true;
                }
            }
            return false;
        }

        public AudioClip GetClip(VoiceClipChoice choice)
        {
            return choice.Key != null && Index.TryGetValue(choice.Key, out Entry e) && choice.Variant >= 0 && choice.Variant < e.variants.Length
                ? e.variants[choice.Variant]
                : null;
        }
    }
}
