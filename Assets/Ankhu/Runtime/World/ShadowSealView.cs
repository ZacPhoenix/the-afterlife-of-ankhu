using Ankhu.Integration;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>Shows the shadow figure on the seal while <see cref="RoomSnapshot.ShadowPresent"/>.</summary>
    public sealed class ShadowSealView : SnapshotListener
    {
        [SerializeField] private GameObject shadowFigure;

        public void Configure(GameObject figure)
        {
            shadowFigure = figure;
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            GreyboxRender.SetActive(shadowFigure, next.ShadowPresent);
        }
    }
}
