using UnityEngine;

namespace Ankhu.Opening
{
    /// <summary>
    /// Ankhu on foot for the fixed-camera walk. CharacterController movement relative to the active camera, with
    /// the camera basis latched across a cut: after a cut the old camera's basis is kept until the stick is
    /// released or turned more than 50 degrees, so holding forward keeps walking the same way through the cut.
    /// A solemn walk only (no run). Drives the Ankhu animator's Speed, plays footsteps by distance.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class WalkPlayer : MonoBehaviour
    {
        [SerializeField] private FixedCameraDirector director;
        [SerializeField] private Animator animator;
        [SerializeField] private float walkSpeed = 1.35f;
        [SerializeField] private float acceleration = 3.5f;
        [SerializeField] private float turnSpeed = 420f;
        [SerializeField] private float animatorSpeedScale = 1f;
        [SerializeField] private AudioClip[] footsteps = new AudioClip[0];
        [SerializeField] private float stride = 0.72f;
        [SerializeField] private Escort leader;
        [SerializeField] private WalkPath path;

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int SpeakId = Animator.StringToHash("Speak");

        private CharacterController body;
        private Vector3 velocity;
        private float fall;
        private Quaternion basis = Quaternion.identity;

        /// <summary>The camera basis the stick is read in right now (latched across cuts until the stick is released).</summary>
        public Quaternion InputBasis => basis;
        private bool latched;
        private Vector2 latchedInput;
        private float travelled;
        private AudioSource feet;
        private int stepIndex;

        public bool InputEnabled { get; set; } = true;

        /// <summary>While set and he is standing, he turns to face this point (Zac 2026-11-26: face Anpu when they talk).</summary>
        public Vector3? FaceToward { get; set; }
        public float CurrentSpeed => new Vector3(velocity.x, 0f, velocity.z).magnitude;
        public Vector3 Velocity => velocity;

        /// <summary>Anpu leads: Ankhu slows to a stop rather than walk past him along the road.</summary>
        public void SetLeader(Escort lead, WalkPath road)
        {
            leader = lead;
            path = road;
        }

        public void Configure(FixedCameraDirector d, Animator a, AudioClip[] steps)
        {
            director = d;
            animator = a;
            footsteps = steps;
        }

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            feet = gameObject.AddComponent<AudioSource>();
            feet.playOnAwake = false;
            feet.spatialBlend = 0f;
            feet.volume = 0.35f;
        }

        private void OnEnable()
        {
            if (director != null) director.Cut += OnCut;
        }

        private void OnDisable()
        {
            if (director != null) director.Cut -= OnCut;
        }

        private void Start()
        {
            if (director != null && director.ActiveCamera != null) basis = Flatten(director.ActiveCamera.transform.rotation);
        }

        private void OnCut(int from, int to)
        {
            Vector2 input = OpeningInput.Move();
            if (from >= 0 && input.sqrMagnitude > 0.04f)
            {
                latched = true;
                latchedInput = input;
            }
            else
            {
                basis = Flatten(director.ActiveCamera.transform.rotation);
                latched = false;
            }
        }

        private static Quaternion Flatten(Quaternion q)
        {
            Vector3 f = q * Vector3.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f.normalized, Vector3.up) : Quaternion.identity;
        }

        public void Speak()
        {
            if (animator != null) animator.SetTrigger(SpeakId);
        }

        private void Update()
        {
            Vector2 input = InputEnabled ? OpeningInput.Move() : Vector2.zero;
            if (latched)
            {
                bool released = input.sqrMagnitude < 0.04f;
                bool turned = Vector2.Angle(input, latchedInput) > 50f;
                if (released || turned)
                {
                    latched = false;
                    if (director != null && director.ActiveCamera != null) basis = Flatten(director.ActiveCamera.transform.rotation);
                }
            }
            else if (director != null && director.ActiveCamera != null && input.sqrMagnitude < 0.04f)
            {
                basis = Flatten(director.ActiveCamera.transform.rotation);
            }

            Vector3 wish = basis * new Vector3(input.x, 0f, input.y);
            float mag = Mathf.Clamp01(wish.magnitude);
            Vector3 target = mag > 0.05f ? wish.normalized * walkSpeed * Mathf.SmoothStep(0.25f, 1f, mag) : Vector3.zero;
            if (leader != null && path != null && target.sqrMagnitude > 1e-4f)
            {
                // Soft block: the part of his step that goes down the road fades out as he comes up behind Anpu.
                float myS = path.Project(transform.position, out _);
                Vector3 along = path.TangentAt(myS);
                along.y = 0f;
                along.Normalize();
                float fwd = Vector3.Dot(target, along);
                if (fwd > 0f)
                {
                    float room = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.9f, 3.0f, leader.S - myS));
                    target -= along * fwd * (1f - room);
                }
            }
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            flat = Vector3.MoveTowards(flat, target, acceleration * Time.deltaTime);

            if (body.isGrounded) fall = -1f;
            else fall -= 9.81f * Time.deltaTime;
            velocity = new Vector3(flat.x, fall, flat.z);
            body.Move(velocity * Time.deltaTime);

            if (flat.sqrMagnitude > 0.01f)
            {
                Quaternion face = Quaternion.LookRotation(flat.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnSpeed * Time.deltaTime);
            }
            else if (FaceToward.HasValue)
            {
                Vector3 to = FaceToward.Value - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.04f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), turnSpeed * 0.6f * Time.deltaTime);
            }

            float speed = flat.magnitude;
            if (animator != null)
            {
                // Half-blended idle and walk read as a hop while he sets off. Once moving, play the full walk and slow
                // the clip to the ground speed instead, the same foot lock the escorts use.
                bool moving = speed > 0.3f;
                animator.SetFloat(SpeedId, moving ? Mathf.Max(speed, walkSpeed) * animatorSpeedScale : speed * animatorSpeedScale, 0.12f, Time.deltaTime);
                animator.speed = moving ? Mathf.Clamp(speed / walkSpeed, 0.6f, 1.15f) : 1f;
                animator.SetBool(GroundedId, true);
            }

            lastSpeed = speed;
            if (FootBones()) return;
            // No humanoid feet to listen to: fall back to a step every stride of ground covered.
            travelled += speed * Time.deltaTime;
            if (travelled >= stride)
            {
                travelled = 0f;
                Step();
            }
        }

        // Zac 2026-11-26: the steps did not land with his feet. A step now sounds when a foot comes down: each foot's
        // height is tracked against its own lowest point, and the step plays as it settles back onto the ground.
        private Transform footL, footR;
        private bool feetLooked;
        private float lastSpeed;
        private readonly float[] footLow = { float.MaxValue, float.MaxValue };
        private readonly float[] footPrev = new float[2];
        private readonly bool[] footArmed = new bool[2];
        private const float FootLift = 0.05f, FootDown = 0.018f;

        private bool FootBones()
        {
            if (!feetLooked && animator != null && animator.isHuman)
            {
                feetLooked = true;
                footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                footR = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
            return footL != null && footR != null;
        }

        private void LateUpdate()
        {
            if (footL == null || footR == null) return;
            for (int i = 0; i < 2; i++)
            {
                Transform f = i == 0 ? footL : footR;
                float h = f.position.y - transform.position.y;
                // The low point creeps back up slowly so a slope or a new pose does not leave it stale.
                footLow[i] = Mathf.Min(footLow[i] + 0.02f * Time.deltaTime, h);
                if (h > footLow[i] + FootLift) footArmed[i] = true;
                bool falling = h < footPrev[i] - 0.0005f;
                if (footArmed[i] && h < footLow[i] + FootDown && !falling && lastSpeed > 0.3f)
                {
                    footArmed[i] = false;
                    Step();
                }
                if (lastSpeed <= 0.3f) footArmed[i] = false;
                footPrev[i] = h;
            }
        }

        private void Step()
        {
            if (footsteps == null || footsteps.Length == 0) return;
            stepIndex = (stepIndex + 1 + Random.Range(0, footsteps.Length - 1)) % footsteps.Length;
            feet.pitch = Random.Range(0.94f, 1.05f);
            feet.PlayOneShot(footsteps[stepIndex], Random.Range(0.7f, 1f));
        }
    }
}
