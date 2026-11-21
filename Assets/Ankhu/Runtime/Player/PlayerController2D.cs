using System.Collections.Generic;
using Ankhu.Core;
using Ankhu.Integration;
using Ankhu.Layout;
using UnityEngine;

namespace Ankhu.Player
{
    /// <summary>
    /// Kinematic side-on character controller for Ankhu. The transform origin is the feet.
    /// All collision is resolved with explicit box casts against non trigger colliders, then the
    /// result is applied with <see cref="Rigidbody2D.MovePosition"/> so interpolation stays smooth.
    ///
    /// Rules of motion: walk with acceleration, gravity, step up and down by at most
    /// <see cref="BorrowedDawnLayout.MaxStepHeight"/>, never walk off a taller ledge, ride moving
    /// bodies, stand on one-way platforms only from above, climb ladders. There is no jump.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class PlayerController2D : MonoBehaviour
    {
        private const float Skin = 0.02f;
        private const float ProbeHeight = 0.04f;
        private const float Gravity = 22f;
        private const float MaxFallSpeed = 14f;
        private const float Acceleration = 22f;
        private const float Deceleration = 32f;
        private const float GroundSnap = 0.08f;
        private const float OneWayTolerance = 0.05f;
        private const float ClimbEndTolerance = 0.01f;
        private const float LadderReachBelow = 0.6f;
        private const float MinGroundNormalY = 0.5f;

        [SerializeField] private Vector2 spawnPoint = BorrowedDawnLayout.Spawn;
        [SerializeField] private AnkhuVisual visual;

        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(16);
        private readonly List<Collider2D> overlaps = new List<Collider2D>(16);

        private Rigidbody2D body;
        private Collider2D ownCollider;
        private ContactFilter2D solidFilter;
        private ContactFilter2D triggerFilter;

        private Vector2 velocity;
        private bool grounded;
        private Collider2D groundCollider;
        private Rigidbody2D groundBody;
        private Vector2 groundBodySample;
        private bool hasGroundBodySample;

        private LadderVolume ladder;
        private bool climbing;
        private Collider2D ignoredTopPlatform;

        private Vector2 lastSafe;
        private bool hasLastSafe;
        private int facing = 1;
        private bool facingDirty = true;

        public bool IsGrounded => grounded;
        public bool IsClimbing => climbing;
        public Collider2D GroundCollider => groundCollider;
        public LadderVolume CurrentLadder => climbing ? ladder : null;
        public Vector2 Velocity => velocity;
        public int Facing => facing;
        public Vector2 SpawnPoint => spawnPoint;
        public AnkhuVisual Visual => visual;

        /// <summary>Feet position in world space.</summary>
        public Vector2 FeetPosition => body != null ? body.position : (Vector2)transform.position;

        /// <summary>Set by the rig factory. Safe to call in edit mode.</summary>
        public void Configure(Vector2 spawn, AnkhuVisual visualComponent)
        {
            spawnPoint = spawn;
            visual = visualComponent;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            ownCollider = GetComponent<Collider2D>();
            if (visual == null)
            {
                visual = GetComponentInChildren<AnkhuVisual>();
            }
            lastSafe = body.position;
            hasLastSafe = true;
        }

        private void OnEnable()
        {
            int mask = Physics2D.GetLayerCollisionMask(gameObject.layer);
            solidFilter = new ContactFilter2D();
            solidFilter.useTriggers = false;
            solidFilter.SetLayerMask(mask);
            triggerFilter = new ContactFilter2D();
            triggerFilter.useTriggers = true;
            triggerFilter.SetLayerMask(Physics2D.AllLayers);
        }

        private void OnDisable()
        {
            SetTopPlatformIgnored(null);
        }

        /// <summary>Moves the feet to a point instantly with no interpolation, clearing motion and climbing.</summary>
        public void Teleport(Vector2 feet)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }
            EndClimb();
            facingDirty = true;
            velocity = Vector2.zero;
            // Tentatively grounded so the next step snaps onto a floor within reach, or starts falling.
            grounded = true;
            groundCollider = null;
            groundBody = null;
            hasGroundBodySample = false;
            body.position = feet;
            transform.position = new Vector3(feet.x, feet.y, transform.position.z);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector2 pos = body.position;

            if (pos.y < BorrowedDawnLayout.KillPlaneY)
            {
                Teleport(hasLastSafe ? lastSafe : spawnPoint);
                FeedbackBus.Publish(FeedbackSource.System, CommandOutcome.NoChange, "Ankhu slips into the dark and wakes where he last stood firm.");
                return;
            }

            float h = GameplayKeys.Horizontal();
            float v = GameplayKeys.Vertical();

            if (!climbing && v != 0f)
            {
                TryBeginClimb(pos, v);
            }

            pos = climbing ? StepClimbing(pos, h, v, dt) : StepWalking(pos, h, dt);

            if (!climbing && h != 0f)
            {
                facing = h > 0f ? 1 : -1;
            }

            body.MovePosition(pos);
        }

        private void Update()
        {
            if (visual == null)
            {
                return;
            }
            visual.SetFacing(facing, climbing, facingDirty);
            facingDirty = false;
            float speed = climbing ? Mathf.Abs(velocity.y) : Mathf.Abs(velocity.x);
            visual.SetMotion(speed, climbing, grounded);
        }

        // ---------------------------------------------------------------- walking

        private Vector2 StepWalking(Vector2 pos, float h, float dt)
        {
            float target = grounded ? h * BorrowedDawnLayout.WalkSpeed : 0f;
            bool speedingUp = target != 0f && (velocity.x == 0f || Mathf.Sign(target) == Mathf.Sign(velocity.x)) && Mathf.Abs(target) > Mathf.Abs(velocity.x);
            velocity.x = Mathf.MoveTowards(velocity.x, target, (speedingUp ? Acceleration : Deceleration) * dt);

            float dx = velocity.x * dt;
            if (dx != 0f)
            {
                dx = CastHorizontal(pos, dx);
                if (grounded && dx != 0f && !HasSupport(new Vector2(pos.x + dx, pos.y)))
                {
                    dx = 0f;
                }
                if (dx == 0f)
                {
                    velocity.x = 0f;
                }
                pos.x += dx;
            }

            float reach;
            if (grounded)
            {
                velocity.y = 0f;
                reach = GroundSnap;
            }
            else
            {
                velocity.y = Mathf.Max(velocity.y - Gravity * dt, -MaxFallSpeed);
                reach = Mathf.Max(0f, -velocity.y * dt);
            }

            if (ProbeGround(pos, BorrowedDawnLayout.AnkhuWidth - 2f * Skin, reach, velocity.y, out float surfaceY, out Collider2D hitCollider))
            {
                pos.y = surfaceY;
                velocity.y = 0f;
                SetGround(hitCollider);
                pos += TakeGroundBodyDelta();
                if (groundBody == null && HasSupport(pos))
                {
                    lastSafe = pos;
                    hasLastSafe = true;
                }
            }
            else
            {
                if (!grounded)
                {
                    pos.y += velocity.y * dt;
                }
                ClearGround();
            }
            return pos;
        }

        private void SetGround(Collider2D collider)
        {
            grounded = true;
            groundCollider = collider;
            Rigidbody2D attached = collider != null ? collider.attachedRigidbody : null;
            if (attached != null && attached.bodyType == RigidbodyType2D.Static)
            {
                attached = null;
            }
            if (attached != groundBody)
            {
                groundBody = attached;
                hasGroundBodySample = false;
            }
        }

        private void ClearGround()
        {
            grounded = false;
            groundCollider = null;
            groundBody = null;
            hasGroundBodySample = false;
        }

        /// <summary>
        /// Position change of the body we stand on since the previous FixedUpdate. A body moved with
        /// MovePosition reports its new pose only after the physics step, so the feet were resolved
        /// against its last pose. Adding the last delta predicts its next pose and keeps the feet on
        /// the deck instead of one step behind it.
        /// </summary>
        private Vector2 TakeGroundBodyDelta()
        {
            if (groundBody == null)
            {
                return Vector2.zero;
            }
            Vector2 current = groundBody.position;
            Vector2 delta = hasGroundBodySample ? current - groundBodySample : Vector2.zero;
            groundBodySample = current;
            hasGroundBodySample = true;
            return delta;
        }

        /// <summary>Returns how far the body may move horizontally (signed). Walls block, low steps do not.</summary>
        private float CastHorizontal(Vector2 pos, float dx)
        {
            float dir = Mathf.Sign(dx);
            float distance = Mathf.Abs(dx);
            float step = BorrowedDawnLayout.MaxStepHeight;
            var size = new Vector2(BorrowedDawnLayout.AnkhuWidth - 2f * Skin, BorrowedDawnLayout.AnkhuHeight - step - 2f * Skin);
            var center = new Vector2(pos.x, pos.y + step + Skin + size.y * 0.5f);

            int count = Physics2D.BoxCast(center, size, 0f, new Vector2(dir, 0f), solidFilter, hits, distance + Skin);
            float allowed = distance;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hits[i];
                Collider2D c = hit.collider;
                if (Skip(c) || IsOneWay(c))
                {
                    continue;
                }
                if (hit.distance <= 0f)
                {
                    // Already overlapping, for example a gate closed on Ankhu. Allow moving away from it only.
                    float side = c.bounds.center.x - pos.x;
                    if (side * dir > 0f)
                    {
                        allowed = 0f;
                    }
                    continue;
                }
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - Skin));
            }
            return allowed * dir;
        }

        /// <summary>True when a narrow probe under the body centre finds ground within one step up or down.</summary>
        private bool HasSupport(Vector2 feet)
        {
            return ProbeGround(feet, BorrowedDawnLayout.AnkhuWidth * 0.5f, BorrowedDawnLayout.MaxStepHeight + Skin, 0f, out _, out _);
        }

        /// <summary>
        /// Casts a thin box down from one step above the feet. Finds the highest walkable surface no
        /// higher than feet + MaxStepHeight and no lower than feet - extraDown.
        /// </summary>
        private bool ProbeGround(Vector2 feet, float width, float extraDown, float verticalSpeed, out float surfaceY, out Collider2D surface)
        {
            float step = BorrowedDawnLayout.MaxStepHeight;
            var size = new Vector2(width, ProbeHeight);
            var origin = new Vector2(feet.x, feet.y + step + ProbeHeight * 0.5f);
            int count = Physics2D.BoxCast(origin, size, 0f, Vector2.down, solidFilter, hits, step + extraDown);

            float best = float.PositiveInfinity;
            surface = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hits[i];
                Collider2D c = hit.collider;
                if (Skip(c) || hit.distance <= 0f || hit.normal.y < MinGroundNormalY)
                {
                    continue;
                }
                if (IsOneWay(c))
                {
                    bool fromAbove = feet.y >= c.bounds.max.y - OneWayTolerance;
                    if (!fromAbove || verticalSpeed > 0.01f)
                    {
                        continue;
                    }
                }
                if (hit.distance < best)
                {
                    best = hit.distance;
                    surface = c;
                }
            }
            if (surface == null)
            {
                surfaceY = feet.y;
                return false;
            }
            surfaceY = feet.y + step - best;
            return true;
        }

        private bool Skip(Collider2D c)
        {
            if (c == null || c.isTrigger || !c.enabled)
            {
                return true;
            }
            if (c == ownCollider || (body != null && c.attachedRigidbody == body))
            {
                return true;
            }
            return ignoredTopPlatform != null && c == ignoredTopPlatform;
        }

        private static bool IsOneWay(Collider2D c)
        {
            if (!c.usedByEffector)
            {
                return false;
            }
            return c.TryGetComponent(out PlatformEffector2D effector) && effector.enabled && effector.useOneWay;
        }

        // ---------------------------------------------------------------- ladders

        private void TryBeginClimb(Vector2 pos, float v)
        {
            LadderVolume found = FindLadder(pos);
            if (found == null)
            {
                return;
            }
            if (v > 0f && (pos.y >= found.TopY - ClimbEndTolerance || pos.y < found.BottomY - BorrowedDawnLayout.MaxStepHeight))
            {
                return;
            }
            if (v < 0f && pos.y <= found.BottomY + ClimbEndTolerance)
            {
                return;
            }
            ladder = found;
            climbing = true;
            velocity = Vector2.zero;
            ClearGround();
            SetTopPlatformIgnored(found.TopPlatform);
        }

        private LadderVolume FindLadder(Vector2 feet)
        {
            // Reaches a little below the feet so standing on the hatch finds the ladder under it.
            float height = BorrowedDawnLayout.AnkhuHeight + LadderReachBelow;
            var center = new Vector2(feet.x, feet.y - LadderReachBelow + height * 0.5f);
            var size = new Vector2(BorrowedDawnLayout.AnkhuWidth, height);
            int count = Physics2D.OverlapBox(center, size, 0f, triggerFilter, overlaps);

            LadderVolume best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider2D c = overlaps[i];
                if (c == null || !c.isTrigger)
                {
                    continue;
                }
                LadderVolume candidate = c.GetComponent<LadderVolume>();
                if (candidate == null || !candidate.Available)
                {
                    continue;
                }
                float d = Mathf.Abs(candidate.ClimbX - feet.x);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = candidate;
                }
            }
            return best;
        }

        private Vector2 StepClimbing(Vector2 pos, float h, float v, float dt)
        {
            if (ladder == null || !ladder.Available)
            {
                // The ladder was stowed under Ankhu. Let go and fall to whatever is below.
                EndClimb();
                ClearGround();
                return pos;
            }

            pos.x = ladder.ClimbX;
            float bottom = ladder.BottomY;
            float top = ladder.TopY;
            float y = Mathf.Clamp(pos.y + v * BorrowedDawnLayout.ClimbSpeed * dt, bottom, top);
            velocity = new Vector2(0f, v * BorrowedDawnLayout.ClimbSpeed);

            bool atTop = y >= top - ClimbEndTolerance;
            bool atBottom = y <= bottom + ClimbEndTolerance;
            bool stepOff = v == 0f && h != 0f;

            if (atTop && (v > 0f || stepOff))
            {
                pos.y = top;
                EndClimb();
                grounded = true;
                groundCollider = null;
                velocity = Vector2.zero;
            }
            else if (atBottom && (v < 0f || stepOff))
            {
                pos.y = bottom;
                EndClimb();
                grounded = true;
                groundCollider = null;
                velocity = Vector2.zero;
            }
            else
            {
                pos.y = y;
            }
            return pos;
        }

        private void EndClimb()
        {
            climbing = false;
            ladder = null;
            SetTopPlatformIgnored(null);
        }

        private void SetTopPlatformIgnored(Collider2D platform)
        {
            if (ignoredTopPlatform == platform)
            {
                return;
            }
            if (ignoredTopPlatform != null && ownCollider != null)
            {
                Physics2D.IgnoreCollision(ownCollider, ignoredTopPlatform, false);
            }
            ignoredTopPlatform = platform;
            if (ignoredTopPlatform != null && ownCollider != null)
            {
                Physics2D.IgnoreCollision(ownCollider, ignoredTopPlatform, true);
            }
        }
    }
}
