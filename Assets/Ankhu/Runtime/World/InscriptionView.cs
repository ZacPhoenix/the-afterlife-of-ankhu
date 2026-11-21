using Ankhu.Integration;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>Inscription glyphs turn gold once <see cref="RoomSnapshot.InscriptionRead"/>.</summary>
    public sealed class InscriptionView : SnapshotListener
    {
        [SerializeField] private Renderer[] glyphs = new Renderer[0];

        public void Configure(Renderer[] glyphRenderers)
        {
            glyphs = glyphRenderers ?? new Renderer[0];
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            Color c = next.InscriptionRead ? GreyboxPalette.Gold : GreyboxPalette.Black;
            for (int i = 0; i < glyphs.Length; i++)
            {
                GreyboxRender.Tint(glyphs[i], c);
            }
        }
    }
}
