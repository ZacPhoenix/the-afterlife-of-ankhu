using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// One spoken word after the rules have read it: the English the player typed, and the hieroglyphs it becomes.
    /// Signs are Gardiner codes (for example "E34", "N35", "O31"), resolved to sprites by <see cref="GlyphAtlas"/>.
    /// </summary>
    public readonly struct SpokenWord
    {
        public readonly string English;
        public readonly string Transliteration;
        public readonly IReadOnlyList<string> Signs;
        /// <summary>True when the signs should sit inside a cartouche (a name).</summary>
        public readonly bool Cartouche;

        public SpokenWord(string english, string transliteration, IReadOnlyList<string> signs, bool cartouche = false)
        {
            English = english ?? string.Empty;
            Transliteration = transliteration ?? string.Empty;
            Signs = signs ?? Array.Empty<string>();
            Cartouche = cartouche;
        }
    }

    /// <summary>
    /// Something in the world a spell can land on. Room objects carry a <see cref="GoldMagic"/> component that
    /// implements this. The glyphs fly to <see cref="GlyphAnchor"/> and call <see cref="Ignite"/> on arrival.
    /// </summary>
    public interface ISpellTarget
    {
        string TargetId { get; }
        Vector3 GlyphAnchor { get; }
        /// <summary>Golden light-up on arrival. strength 1 = a full success flare.</summary>
        void Ignite(float strength);
    }

    /// <summary>What the speech presenter shows once the rules have judged an utterance.</summary>
    public sealed class SpellOutcomeView
    {
        public bool Success;
        /// <summary>Short reply line shown under the bubble (failure reason, or empty on success).</summary>
        public string Reply = string.Empty;
        /// <summary>The words that became glyphs, in spoken order. Unknown words are left out.</summary>
        public IReadOnlyList<SpokenWord> Words = Array.Empty<SpokenWord>();
        /// <summary>Where the glyphs go. Null on failure, or for a spell with no target (the glyphs rise and fade).</summary>
        public ISpellTarget Target;
        /// <summary>Called once, when the glyphs reach the target (or after the failure animation). The director applies the state change visuals here.</summary>
        public Action OnArrive;
        /// <summary>Seconds of Ankhu's recorded voice for this utterance (0 if none). The presenter holds the bubble at least this long.</summary>
        public float VoiceSeconds;
    }
}
