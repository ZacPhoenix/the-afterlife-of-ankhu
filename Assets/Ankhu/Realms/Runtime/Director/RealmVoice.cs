using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// Recorded lines for the realms, loaded by key from Resources/RealmVoice/&lt;key&gt;. Dialogue keys look like
    /// dialogue.anpu.wernes.landing.3. Spoken spells look like spell.open.door, with a fallback to word.open.
    /// A missing clip is fine: the line still shows, held for its reading time.
    /// </summary>
    public static class RealmVoice
    {
        public static AudioClip Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return Resources.Load<AudioClip>("RealmVoice/" + key);
        }

        public static AudioClip FindSpell(string verb, string target)
        {
            AudioClip c = null;
            if (!string.IsNullOrEmpty(target)) c = Find("spell." + verb + "." + target);
            return c != null ? c : Find("word." + verb);
        }

        /// <summary>Seconds a line should stay up: the clip plus a breath, or a reading time for silent lines.</summary>
        public static float HoldSeconds(string text, AudioClip clip)
        {
            float read = 1.6f + (text ?? string.Empty).Length * 0.055f;
            return clip != null ? Mathf.Max(clip.length + 0.45f, 1.2f) : Mathf.Clamp(read, 2.2f, 9f);
        }
    }
}
