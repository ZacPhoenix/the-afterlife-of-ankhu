using Ankhu.Integration;
using Ankhu.Layout;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>
    /// Pendulum bridge hinged at BridgePivot. Angle 0 hangs straight down and positive angles
    /// rotate counter-clockwise, so +90 points east along the walkway.
    /// Unbound (<see cref="RoomSnapshot.BridgeBound"/> false) it swings with no collider.
    /// Bound, it eases to +90 over MoverTransitionSeconds, then enables its walkable collider and
    /// the bridge zone. Releasing disables both at once and resumes the swing from the current
    /// angle without a jump.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PendulumBridge : SnapshotMover
    {
        public const float HorizontalAngle = 90f;

        [SerializeField] private float swingMinDeg = BorrowedDawnLayout.BridgeSwingMinDeg;
        [SerializeField] private float swingMaxDeg = BorrowedDawnLayout.BridgeSwingMaxDeg;
        [SerializeField] private float swingPeriod = BorrowedDawnLayout.BridgeSwingPeriod;
        [SerializeField] private float settleSeconds = BorrowedDawnLayout.MoverTransitionSeconds;
        [SerializeField] private Collider2D walkableCollider;
        [SerializeField] private GameObject bridgeZone;

        private bool bound;
        private bool settling;
        private float settleFrom;
        private float settleElapsed;
        private float swingClock;
        private float angle;

        /// <summary>True when bound and settled horizontal, with the walkable collider on.</summary>
        public bool Walkable { get; private set; }

        public float Angle => angle;

        public override bool InTransit => settling;

        public void Configure(Collider2D walkable, GameObject zone)
        {
            walkableCollider = walkable;
            bridgeZone = zone;
        }

        private void Awake()
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            SetWalkable(false);
            angle = SwingAngle(swingClock);
            ApplyRotation();
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            bool nowBound = next.BridgeBound;
            if (instant)
            {
                bound = nowBound;
                settling = false;
                if (bound)
                {
                    angle = HorizontalAngle;
                }
                else
                {
                    swingClock = 0f;
                    angle = SwingAngle(swingClock);
                }
                ApplyRotation();
                SetWalkable(bound);
                return;
            }
            if (nowBound == bound)
            {
                return;
            }
            bound = nowBound;
            if (bound)
            {
                settling = true;
                settleFrom = angle;
                settleElapsed = 0f;
            }
            else
            {
                settling = false;
                SetWalkable(false);
                swingClock = ClockForAngle(angle);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (!bound)
            {
                swingClock += dt;
                angle = SwingAngle(swingClock);
                ApplyRotation();
                return;
            }
            if (!settling)
            {
                return;
            }
            settleElapsed += dt;
            float t = settleSeconds > 0f ? settleElapsed / settleSeconds : 1f;
            if (t >= 1f)
            {
                settling = false;
                angle = HorizontalAngle;
                ApplyRotation();
                SetWalkable(true);
                return;
            }
            angle = Mathf.Lerp(settleFrom, HorizontalAngle, GreyboxRender.Ease(t));
            ApplyRotation();
        }

        private void ApplyRotation()
        {
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void SetWalkable(bool walkable)
        {
            Walkable = walkable;
            if (walkableCollider != null)
            {
                walkableCollider.enabled = walkable;
            }
            GreyboxRender.SetActive(bridgeZone, walkable);
        }

        private float Mid => (swingMinDeg + swingMaxDeg) * 0.5f;
        private float Amplitude => (swingMaxDeg - swingMinDeg) * 0.5f;
        private float Omega => swingPeriod > 0f ? 2f * Mathf.PI / swingPeriod : 0f;

        private float SwingAngle(float clock)
        {
            return Mid + Amplitude * Mathf.Sin(Omega * clock);
        }

        /// <summary>Swing clock that reproduces <paramref name="fromAngle"/> while heading back west.</summary>
        private float ClockForAngle(float fromAngle)
        {
            if (Omega <= 0f || Amplitude <= 0f)
            {
                return 0f;
            }
            float s = Mathf.Clamp((fromAngle - Mid) / Amplitude, -1f, 1f);
            return (Mathf.PI - Mathf.Asin(s)) / Omega;
        }
    }
}
