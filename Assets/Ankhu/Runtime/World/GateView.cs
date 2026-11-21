using Ankhu.Integration;
using Ankhu.Layout;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>
    /// The sun gate. Two leaves narrow toward the posts when <see cref="RoomSnapshot.GateOpen"/>,
    /// revealing the dawn panel behind them. Visual only.
    /// The root sits at (GateX, ledge top). Each leaf is a scaled leaf object.
    /// </summary>
    public sealed class GateView : SnapshotMover
    {
        [SerializeField] private Transform westLeaf;
        [SerializeField] private Transform eastLeaf;
        [SerializeField] private Renderer dawn;
        [SerializeField] private float width = BorrowedDawnLayout.GateWidth;
        [SerializeField] private float openLeafWidth = 0.12f;
        [SerializeField] private float travelSeconds = BorrowedDawnLayout.MoverTransitionSeconds;

        private readonly FloatTween tween = new FloatTween();

        public override bool InTransit => tween.InTransit;

        public void Configure(Transform west, Transform east, Renderer dawnPanel, float gateWidth)
        {
            westLeaf = west;
            eastLeaf = east;
            dawn = dawnPanel;
            width = gateWidth;
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            float target = next.GateOpen ? 1f : 0f;
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
            float half = width * 0.5f;
            float leafWidth = Mathf.Lerp(half, openLeafWidth, tween.Value);
            PlaceLeaf(westLeaf, -half + leafWidth * 0.5f, leafWidth);
            PlaceLeaf(eastLeaf, half - leafWidth * 0.5f, leafWidth);
            GreyboxRender.SetVisible(dawn, tween.Value > 0.01f);
            GreyboxRender.Tint(dawn, GreyboxPalette.WithAlpha(GreyboxPalette.DawnBeyond, tween.Value));
        }

        private static void PlaceLeaf(Transform leaf, float localX, float leafWidth)
        {
            if (leaf == null)
            {
                return;
            }
            Vector3 p = leaf.localPosition;
            p.x = localX;
            leaf.localPosition = p;
            Vector3 s = leaf.localScale;
            s.x = leafWidth;
            leaf.localScale = s;
        }
    }
}
