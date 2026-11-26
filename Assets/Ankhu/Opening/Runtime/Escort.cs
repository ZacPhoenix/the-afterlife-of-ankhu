using System.Collections.Generic;
using UnityEngine;

namespace Ankhu.Opening
{
    /// <summary>
    /// Anpu and the two attendants on the road. Path-following, not NavMesh (the project has no AI Navigation package).
    /// Leader (Anpu): walks the road ahead of Ankhu, eases to a stop near the exit edge of each fixed shot, looks back
    /// and waits until Ankhu is close, then eases off again. At the end of the road he stops at the gate and waits.
    /// Follower (attendant): trails the leader by a slot behind and to one side. It reads the leader's position from a
    /// short history buffer, so it starts and settles a beat after him. Its own walk phase, cadence and a slow drift
    /// keep it from stepping in time with him or the other attendant.
    /// </summary>
    public sealed class Escort : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private const float ClipSpeed = 1.35f;

        [SerializeField] private WalkPath path;
        [SerializeField] private WalkPlayer player;
        [SerializeField] private Animator animator;
        [SerializeField] private float startDelay;
        [SerializeField] private float phase;
        [SerializeField] private float cadence = 1f;
        [SerializeField] private float roadHalfWidth = 1.35f;

        // Leader
        [SerializeField] private bool leader;
        [SerializeField] private float[] pausePoints = new float[0];
        [SerializeField] private float cruise = 1.28f;
        [SerializeField] private float accel = 0.9f;
        [SerializeField] private float decel = 0.75f;
        [SerializeField] private float waitFor = 3f;
        [SerializeField] private float minPause = 1.2f;
        [SerializeField] private float walkLateral = -0.55f;
        [SerializeField] private float endS = -1f;
        [SerializeField] private float endLateral = -0.85f;

        // Follower
        [SerializeField] private Escort lead;
        [SerializeField] private float back = 1.2f;
        [SerializeField] private float side = 0.75f;
        [SerializeField] private float reactDelay = 0.3f;
        [SerializeField] private float endBack = 1.7f;
        [SerializeField] private float endSide = 1.25f;
        [SerializeField] private int seed;

        private float s, lat, speed, delay, pauseTimer, clock;
        private int nextPause;
        private bool paused;
        private FootFloorClamp head;
        private readonly Queue<(float t, float s, float lat, float v)> trail = new Queue<(float, float, float, float)>();
        private (float s, float lat, float v) delayed;

        public bool IsLeader => leader;
        public bool Paused => paused;
        /// <summary>Arc length along the road.</summary>
        public float S => s;
        public float Lateral => lat;
        public float Speed => speed;
        public Animator Animator => animator;

        public void Configure(WalkPath p, WalkPlayer pl, Animator a, float delaySeconds, float walkPhase, float cadenceScale)
        {
            path = p;
            player = pl;
            animator = a;
            startDelay = delaySeconds;
            phase = walkPhase;
            cadence = cadenceScale;
        }

        public void SetLeader(float[] pauses, float stopAt, float stopLateral, float lateral)
        {
            leader = true;
            pausePoints = pauses;
            endS = stopAt;
            endLateral = stopLateral;
            walkLateral = lateral;
        }

        public void SetFollower(Escort leaderEscort, float behind, float sideOffset, float react, float gateBack, float gateSide, int jitterSeed)
        {
            leader = false;
            lead = leaderEscort;
            back = behind;
            side = sideOffset;
            reactDelay = react;
            endBack = gateBack;
            endSide = gateSide;
            seed = jitterSeed;
        }

        private void Start()
        {
            s = path.Project(transform.position, out lat);
            delayed = (s, lat, 0f);
            delay = startDelay;
            if (animator != null)
            {
                head = animator.GetComponent<FootFloorClamp>();
                if (animator.runtimeAnimatorController != null)
                {
                    animator.Update(0f);
                    AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
                    animator.Play(st.fullPathHash, 0, phase);
                }
            }
        }

        private void Update()
        {
            if (path == null || player == null) return;
            clock += Time.deltaTime;
            float playerS = path.Project(player.transform.position, out _);
            if (leader) UpdateLeader(playerS);
            else UpdateFollower(playerS);

            Vector3 pos = path.OffsetPoint(s, lat);
            pos.y = transform.position.y;
            Vector3 moved = pos - transform.position;
            transform.position = pos;
            Face(moved);
            Animate();
        }

        private void UpdateLeader(float playerS)
        {
            float stopAt = endS > 0f ? endS : path.Length;
            float targetLat = walkLateral + 0.12f * Mathf.Sin(clock * 0.31f);
            while (nextPause < pausePoints.Length && pausePoints[nextPause] <= s - 0.05f && !paused) nextPause++;
            float goal = nextPause < pausePoints.Length ? Mathf.Min(pausePoints[nextPause], stopAt) : stopAt;
            bool atEnd = goal >= stopAt - 0.01f;
            if (atEnd) targetLat = Mathf.Lerp(targetLat, endLateral, Mathf.InverseLerp(stopAt - 5f, stopAt, s));

            float target;
            if (delay > 0f) { delay -= Time.deltaTime; target = 0f; }
            else if (paused)
            {
                target = 0f;
                pauseTimer += Time.deltaTime;
                // Wait until Ankhu is close, then walk on. The last stop is the gate: he stays there.
                if (!atEnd && pauseTimer >= minPause && goal - playerS <= waitFor)
                {
                    paused = false;
                    nextPause++;
                }
            }
            else
            {
                float remaining = goal - s;
                // Ease in: the speed that still lets him stop at the mark with a gentle deceleration.
                target = Mathf.Min(cruise, Mathf.Sqrt(Mathf.Max(0f, 2f * decel * remaining)));
                if (remaining < 0.04f && speed < 0.12f)
                {
                    paused = true;
                    pauseTimer = 0f;
                    target = 0f;
                }
            }
            float rate = target > speed ? accel : decel * 1.4f;
            speed = Mathf.MoveTowards(speed, target, rate * Time.deltaTime);
            s = Mathf.Min(s + speed * Time.deltaTime, Mathf.Max(goal, s));
            lat = Mathf.MoveTowards(lat, Mathf.Clamp(targetLat, -roadHalfWidth, roadHalfWidth), 0.35f * Time.deltaTime);
        }

        private void UpdateFollower(float playerS)
        {
            if (lead == null) return;
            trail.Enqueue((clock, lead.S, lead.Lateral, lead.Speed));
            while (trail.Count > 1 && trail.Peek().t < clock - reactDelay)
            {
                var e = trail.Dequeue();
                delayed = (e.s, e.lat, e.v);
            }
            float u = seed * 1.7f;
            float jitterBack = 0.18f * Mathf.Sin(clock * 0.41f + u) + 0.08f * Mathf.Sin(clock * 1.13f + u * 2f);
            float jitterSide = 0.10f * Mathf.Sin(clock * 0.27f + u * 3f);
            float gateT = Mathf.InverseLerp(path.Length - 7f, path.Length - 2f, lead.S);
            float wantBack = Mathf.Lerp(back, endBack, gateT) + jitterBack;
            float wantLat = Mathf.Lerp(delayed.lat + side + jitterSide, Mathf.Sign(side) * endSide, gateT);
            // Keep clear of Ankhu: if he walks up beside an attendant, it steps out of his line.
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude < 0.9f) wantLat += Mathf.Sign(side) * (0.9f - toPlayer.magnitude);
            wantLat = Mathf.Clamp(wantLat, -roadHalfWidth - 0.2f, roadHalfWidth + 0.2f);

            // Hold the slot behind where Anpu is now, but match the speed he had a reaction-time ago: the attendant
            // starts and settles a beat after him, then closes the small gap that leaves.
            float slot = lead.S - wantBack;
            float gap = slot - s;
            float target;
            if (delay > 0f) { delay -= Time.deltaTime; target = 0f; }
            else target = Mathf.Clamp(delayed.v + gap * 0.9f, 0f, 1.7f);
            if (delayed.v < 0.05f && gap < 0.35f) target = 0f;
            float rate = target > speed ? 1.0f : 1.3f;
            speed = Mathf.MoveTowards(speed, target, rate * Time.deltaTime);
            s = Mathf.Clamp(s + speed * Time.deltaTime, 0f, path.Length);
            lat = Mathf.MoveTowards(lat, wantLat, 0.45f * Time.deltaTime);
        }

        private void Face(Vector3 moved)
        {
            Vector3 face;
            float turnRate = 160f;
            float lookWeight = 0f;
            if (speed > 0.1f && moved.sqrMagnitude > 1e-7f) face = moved;
            else
            {
                Vector3 toPlayer = player.transform.position - transform.position;
                toPlayer.y = 0f;
                Vector3 ahead = path.TangentAt(s);
                if (leader && paused)
                {
                    // Shoulders turn part way back toward Ankhu, the head the rest (see FootFloorClamp's look).
                    face = Vector3.Slerp(ahead, toPlayer.normalized, 0.45f);
                    lookWeight = 1f;
                    turnRate = 90f;
                }
                else face = Vector3.Slerp(ahead, toPlayer.normalized, leader ? 0.6f : 0.2f);
            }
            face.y = 0f;
            if (face.sqrMagnitude > 1e-4f)
            {
                Quaternion want = Quaternion.LookRotation(face.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnRate * Time.deltaTime);
            }
            if (head != null) head.SetLook(player.transform.position + Vector3.up * 1.55f, lookWeight);
        }

        private void Animate()
        {
            if (animator == null) return;
            // Feet locked to the ground: the clip is authored for 1.35 m/s, so the playback rate follows ground speed.
            // The cadence factor makes each walker's stride rate slightly its own, so phases never lock together.
            bool moving = speed > 0.25f;
            animator.SetFloat(SpeedId, moving ? Mathf.Max(speed, 1.4f) : speed, 0.15f, Time.deltaTime);
            animator.speed = moving ? Mathf.Clamp(speed / ClipSpeed, 0.6f, 1.25f) * cadence : 1f;
            animator.SetBool(GroundedId, true);
        }
    }
}
