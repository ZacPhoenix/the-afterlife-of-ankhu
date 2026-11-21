using Ankhu.Core;
using Ankhu.Integration;
using Ankhu.Layout;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>
    /// Gallery hatch and its ladder, driven by <see cref="RoomSnapshot.LadderLowered"/>.
    /// Stowed: the hatch is solid floor, the ladder is drawn folded under the slab (a clue seen
    /// from the floor tier) and the climb volume is off.
    /// Lowered: the hatch becomes a one-way platform (PlatformEffector2D), the ladder unfolds
    /// down to the floor and the climb volume turns on once it has fully unfolded.
    /// The root sits at (LadderX, UpperY).
    /// </summary>
    public sealed class LadderHatch : SnapshotMover
    {
        [SerializeField] private BoxCollider2D hatchCollider;
        [SerializeField] private PlatformEffector2D effector;
        [SerializeField] private LadderVolume ladderVolume;
        [SerializeField] private Renderer hatchLid;
        [Tooltip("Scaled leaf object for the extended rails. Its top stays at the slab underside.")]
        [SerializeField] private Transform extendedLadder;
        [SerializeField] private GameObject foldedLadder;
        [SerializeField] private float slabUndersideY = BorrowedDawnLayout.UpperY - BorrowedDawnLayout.UpperSlabThickness;
        [SerializeField] private float floorY = BorrowedDawnLayout.FloorY;
        [SerializeField] private float travelSeconds = BorrowedDawnLayout.MoverTransitionSeconds;

        private readonly FloatTween tween = new FloatTween();
        private bool lowered;

        public override bool InTransit => tween.InTransit;
        public bool Lowered => lowered;

        public void Configure(BoxCollider2D hatch, PlatformEffector2D oneWay, LadderVolume volume, Renderer lid, Transform extended, GameObject folded)
        {
            hatchCollider = hatch;
            effector = oneWay;
            ladderVolume = volume;
            hatchLid = lid;
            extendedLadder = extended;
            foldedLadder = folded;
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            lowered = next.LadderLowered;
            SetHatchOneWay(lowered);
            GreyboxRender.Tint(hatchLid, lowered ? GreyboxPalette.Black : GreyboxPalette.Bronze);

            float target = lowered ? 1f : 0f;
            if (instant)
            {
                tween.Snap(target);
            }
            else
            {
                tween.SetTarget(target, travelSeconds);
            }
            PlaceLadder();
        }

        private void Update()
        {
            if (tween.Step(Time.deltaTime))
            {
                PlaceLadder();
            }
        }

        private void SetHatchOneWay(bool oneWay)
        {
            if (effector != null)
            {
                effector.useOneWay = true;
                effector.surfaceArc = 160f;
                effector.enabled = oneWay;
            }
            if (hatchCollider != null)
            {
                hatchCollider.usedByEffector = oneWay;
            }
        }

        private void PlaceLadder()
        {
            float amount = tween.Value;
            bool extended = amount > 0.001f;
            GreyboxRender.SetActive(foldedLadder, !extended);
            if (extendedLadder != null)
            {
                GreyboxRender.SetActive(extendedLadder.gameObject, extended);
                float fullLength = slabUndersideY - floorY;
                float length = Mathf.Max(0.01f, fullLength * amount);
                // Parent root is unscaled and sits at UpperY, so local offsets are world offsets.
                Vector3 local = extendedLadder.localPosition;
                local.y = (slabUndersideY - transform.position.y) - length * 0.5f;
                extendedLadder.localPosition = local;
                Vector3 s = extendedLadder.localScale;
                s.y = length;
                extendedLadder.localScale = s;
            }
            if (ladderVolume != null)
            {
                Collider2D climb = ladderVolume.GetComponent<Collider2D>();
                if (climb != null)
                {
                    climb.enabled = lowered && !tween.InTransit && amount >= 0.999f;
                }
            }
        }
    }
}
