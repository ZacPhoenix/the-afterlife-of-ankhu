using Ankhu.Integration;
using Ankhu.Layout;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>
    /// One body of water drawn as a scaled translucent block. It is high when
    /// <see cref="RoomSnapshot.Water"/> equals <see cref="side"/> and low otherwise.
    /// The component scales its own transform, so it must sit on a leaf object with no children.
    /// </summary>
    public sealed class WaterColumnView : SnapshotMover
    {
        [SerializeField] private WaterSide side = WaterSide.East;
        [SerializeField] private float minX;
        [SerializeField] private float maxX = 1f;
        [SerializeField] private float bottomY = BorrowedDawnLayout.BasinBottomY;
        [SerializeField] private float lowTopY;
        [SerializeField] private float highTopY = 1f;
        [SerializeField] private float centreZ = GreyboxDepth.WalkCentreZ;
        [SerializeField] private float depth = GreyboxDepth.WalkDepth;
        [SerializeField] private float travelSeconds = BorrowedDawnLayout.MoverTransitionSeconds;

        private readonly FloatTween tween = new FloatTween();

        public override bool InTransit => tween.InTransit;
        public WaterSide Side => side;

        public void Configure(WaterSide waterSide, float xMin, float xMax, float yBottom, float yLowTop, float yHighTop, float zCentre, float zDepth)
        {
            side = waterSide;
            minX = xMin;
            maxX = xMax;
            bottomY = yBottom;
            lowTopY = yLowTop;
            highTopY = yHighTop;
            centreZ = zCentre;
            depth = zDepth;
            tween.Snap(0f);
            Place();
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            float target = next.Water == side ? 1f : 0f;
            if (instant)
            {
                tween.Snap(target);
                Place();
                return;
            }
            tween.SetTarget(target, travelSeconds);
        }

        private void Update()
        {
            if (tween.Step(Time.deltaTime))
            {
                Place();
            }
        }

        private void Place()
        {
            float top = Mathf.Lerp(lowTopY, highTopY, tween.Value);
            float height = Mathf.Max(0.01f, top - bottomY);
            // The parent is expected to be unscaled, so local scale equals world size.
            Transform t = transform;
            t.position = new Vector3((minX + maxX) * 0.5f, bottomY + height * 0.5f, centreZ);
            t.localScale = new Vector3(maxX - minX, height, depth);
        }
    }
}
