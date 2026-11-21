using Ankhu.Opening;
using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// The realm UI fonts, shipped in the project (Resources/RealmFonts) so every machine renders the design
    /// fonts: Cinzel for capitals and titles, Cormorant Garamond for body and dialogue. Falls back to UiKit's OS fonts.
    /// </summary>
    public static class RealmFonts
    {
        private static Font caps;
        private static Font body;

        public static Font Caps
        {
            get
            {
                if (caps == null) caps = Resources.Load<Font>("RealmFonts/Cinzel-Medium");
                return caps != null ? caps : UiKit.Caps;
            }
        }

        public static Font Body
        {
            get
            {
                if (body == null) body = Resources.Load<Font>("RealmFonts/CormorantGaramond-SemiBold");
                return body != null ? body : UiKit.Serif;
            }
        }
    }
}
