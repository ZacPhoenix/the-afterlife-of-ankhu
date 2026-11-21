using Ankhu.Integration;
using Ankhu.Layout;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>
    /// Buoyant elevator platform. Height comes only from <see cref="RoomSnapshot.ElevatorHigh"/>.
    /// The transform position is the centre of the platform's top surface. It moves with
    /// <see cref="Rigidbody2D.MovePosition"/> in FixedUpdate so a player controller can ride it
    /// (read <see cref="StepDelta"/> or <see cref="Velocity"/>).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class ElevatorPlatform : SnapshotMover
    {
        [SerializeField] private float lowY = BorrowedDawnLayout.PlatformLowY;
        [SerializeField] private float highY = BorrowedDawnLayout.PlatformHighY;
        [SerializeField] private float travelSeconds = BorrowedDawnLayout.MoverTransitionSeconds;

        private readonly FloatTween tween = new FloatTween();
        private Rigidbody2D body;
        private bool initialised;

        public override bool InTransit => tween.InTransit;

        /// <summary>World y of the walkable top surface.</summary>
        public float TopY => Body.position.y;

        /// <summary>World-space movement applied in the most recent FixedUpdate.</summary>
        public Vector2 StepDelta { get; private set; }

        /// <summary>Velocity of the most recent FixedUpdate step, in metres per second.</summary>
        public Vector2 Velocity { get; private set; }

        public Rigidbody2D Body
        {
            get
            {
                if (body == null)
                {
                    body = GetComponent<Rigidbody2D>();
                }
                return body;
            }
        }

        /// <summary>True when the top surface is within DockTolerance of <paramref name="levelY"/>.</summary>
        public bool IsDockedAt(float levelY)
        {
            return Mathf.Abs(TopY - levelY) <= BorrowedDawnLayout.DockTolerance;
        }

        public void Configure(float low, float high, float seconds)
        {
            lowY = low;
            highY = high;
            travelSeconds = seconds;
        }

        private void Awake()
        {
            Rigidbody2D rb = Body;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            InitialiseFromTransform();
        }

        private void InitialiseFromTransform()
        {
            if (initialised)
            {
                return;
            }
            initialised = true;
            float range = highY - lowY;
            float normalised = Mathf.Approximately(range, 0f) ? 0f : Mathf.Clamp01((transform.position.y - lowY) / range);
            tween.Snap(normalised);
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            InitialiseFromTransform();
            float target = next.ElevatorHigh ? 1f : 0f;
            if (instant)
            {
                tween.Snap(target);
                Vector2 position = new Vector2(Body.position.x, HeightFor(target));
                Body.position = position;
                transform.position = new Vector3(position.x, position.y, transform.position.z);
                StepDelta = Vector2.zero;
                Velocity = Vector2.zero;
                return;
            }
            tween.SetTarget(target, travelSeconds);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (!tween.Step(dt))
            {
                StepDelta = Vector2.zero;
                Velocity = Vector2.zero;
                return;
            }
            Vector2 current = Body.position;
            Vector2 next = new Vector2(current.x, HeightFor(tween.Value));
            StepDelta = next - current;
            Velocity = dt > 0f ? StepDelta / dt : Vector2.zero;
            Body.MovePosition(next);
        }

        private float HeightFor(float normalised)
        {
            return Mathf.Lerp(lowY, highY, normalised);
        }
    }
}
