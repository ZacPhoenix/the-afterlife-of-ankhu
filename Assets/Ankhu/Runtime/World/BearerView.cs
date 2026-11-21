using Ankhu.Integration;
using Ankhu.Layout;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>
    /// The Bearer statue. Stands on his pedestal in the beam while asleep and walks west into
    /// the alcove when <see cref="RoomSnapshot.BearerAwake"/> is true.
    /// The root's position is the point between his feet. Children (body, inspect trigger)
    /// travel with him.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class BearerView : SnapshotMover
    {
        [SerializeField] private float pedestalX = BorrowedDawnLayout.BearerPedestalX;
        [SerializeField] private float alcoveX = BorrowedDawnLayout.BearerAlcoveX;
        [SerializeField] private float pedestalTopY = 0.3f;
        [SerializeField] private float alcoveFloorY = BorrowedDawnLayout.FloorY;
        [SerializeField] private float travelSeconds = BorrowedDawnLayout.MoverTransitionSeconds;
        [Tooltip("Visual child that turns to face the walking direction.")]
        [SerializeField] private Transform body;

        private readonly FloatTween tween = new FloatTween();
        private Rigidbody2D rb;

        public override bool InTransit => tween.InTransit;

        /// <summary>Current world x of the statue.</summary>
        public float CurrentX => transform.position.x;

        public void Configure(float pedestalSurfaceY, Transform bodyVisual)
        {
            pedestalTopY = pedestalSurfaceY;
            body = bodyVisual;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            float target = next.BearerAwake ? 1f : 0f;
            if (instant)
            {
                tween.Snap(target);
                Place();
                Face(target > 0.5f ? -1f : 1f);
                return;
            }
            if (!Mathf.Approximately(target, tween.Target))
            {
                // Walking to the alcove faces west, walking back faces east.
                Face(target > tween.Value ? -1f : 1f);
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
            float t = tween.Value;
            float x = Mathf.Lerp(pedestalX, alcoveX, t);
            // Step down off the pedestal over the first fifth of the walk.
            float y = Mathf.Lerp(pedestalTopY, alcoveFloorY, Mathf.Clamp01(t * 5f));
            Vector3 p = new Vector3(x, y, transform.position.z);
            transform.position = p;
            if (rb != null)
            {
                rb.position = new Vector2(x, y);
            }
        }

        private void Face(float direction)
        {
            if (body == null)
            {
                return;
            }
            Vector3 s = body.localScale;
            s.x = Mathf.Abs(s.x) * (direction < 0f ? -1f : 1f);
            body.localScale = s;
        }
    }
}
