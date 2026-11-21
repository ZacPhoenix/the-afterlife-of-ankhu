using Ankhu.Integration;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>The water selector dial. The pointer and lamps show which basin holds the water.</summary>
    public sealed class WaterSelectorView : SnapshotListener
    {
        [SerializeField] private Transform pointer;
        [SerializeField] private Renderer westLamp;
        [SerializeField] private Renderer eastLamp;
        [SerializeField] private float pointerAngle = 50f;

        public void Configure(Transform dialPointer, Renderer west, Renderer east)
        {
            pointer = dialPointer;
            westLamp = west;
            eastLamp = east;
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            bool west = next.Water == WaterSide.West;
            if (pointer != null)
            {
                pointer.localRotation = Quaternion.Euler(0f, 0f, west ? pointerAngle : -pointerAngle);
            }
            GreyboxRender.Tint(westLamp, west ? GreyboxPalette.Water : GreyboxPalette.GreyDark);
            GreyboxRender.Tint(eastLamp, west ? GreyboxPalette.GreyDark : GreyboxPalette.Water);
        }
    }
}
