using Ankhu.Integration;
using Ankhu.Layout;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>
    /// Floating raft in the west cistern. Deck height follows <see cref="RoomSnapshot.RaftHigh"/>.
    /// The mirror is drawn on the raft and its pickup trigger is active only while
    /// <see cref="RoomSnapshot.Mirror"/> is <see cref="MirrorLocation.Raft"/>.
    /// The transform position is the centre of the deck's top surface.
    /// </summary>
    public sealed class RaftView : SnapshotMover
    {
        [SerializeField] private float lowY = BorrowedDawnLayout.RaftDeckLowY;
        [SerializeField] private float highY = BorrowedDawnLayout.RaftDeckHighY;
        [SerializeField] private float travelSeconds = BorrowedDawnLayout.MoverTransitionSeconds;
        [SerializeField] private GameObject mirrorVisual;
        [Tooltip("Mirror pickup interactable. Lives outside the raft, at the docked-high platform's west end.")]
        [SerializeField] private GameObject mirrorPickup;

        private readonly FloatTween tween = new FloatTween();

        public override bool InTransit => tween.InTransit;

        public void Configure(GameObject mirror, GameObject pickup)
        {
            mirrorVisual = mirror;
            mirrorPickup = pickup;
        }

        public void SetPickup(GameObject pickup)
        {
            mirrorPickup = pickup;
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            float target = next.RaftHigh ? 1f : 0f;
            if (instant)
            {
                tween.Snap(target);
                Place();
            }
            else
            {
                tween.SetTarget(target, travelSeconds);
            }
            bool onRaft = next.Mirror == MirrorLocation.Raft;
            GreyboxRender.SetActive(mirrorVisual, onRaft);
            GreyboxRender.SetActive(mirrorPickup, onRaft);
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
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(lowY, highY, tween.Value);
            transform.position = p;
        }
    }
}
