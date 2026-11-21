using Ankhu.Integration;
using Ankhu.Layout;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>
    /// The light screen that rises through a floor slot and blocks the beam. Raised exactly when
    /// <see cref="RoomSnapshot.LightScreenRaised"/> is true. Visual only, it never blocks Ankhu.
    /// The root sits at the slot (x, floor y). The panel child is moved so that its top is
    /// flush with the floor when lowered (hidden inside the floor block) and
    /// LightScreenHeight above it when raised.
    /// </summary>
    public sealed class LightScreenView : SnapshotMover
    {
        [SerializeField] private Transform panel;
        [SerializeField] private float height = BorrowedDawnLayout.LightScreenHeight;
        [SerializeField] private float travelSeconds = BorrowedDawnLayout.MoverTransitionSeconds;

        private readonly FloatTween tween = new FloatTween();

        public override bool InTransit => tween.InTransit;

        public void Configure(Transform panelTransform, float panelHeight)
        {
            panel = panelTransform;
            height = panelHeight;
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            float target = next.LightScreenRaised ? 1f : 0f;
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
            if (panel == null)
            {
                return;
            }
            float top = Mathf.Lerp(0f, height, tween.Value);
            Vector3 local = panel.localPosition;
            local.y = top - height * 0.5f;
            panel.localPosition = local;
        }
    }
}
