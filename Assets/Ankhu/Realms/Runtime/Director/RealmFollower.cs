using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// Anpu walking with Ankhu: keeps a set distance at his shoulder, never crowds him, turns to face him when
    /// both stand still, and can be sent to a named mark by the director. No NavMesh: realm rooms are open floors,
    /// and a CharacterController keeps him out of walls.
    /// </summary>
    public sealed class RealmFollower : MonoBehaviour
    {
        public Transform leader;
        public Animator animator;
        public float keepDistance = 1.8f;
        public float sideOffset = -1.1f;
        public float walkSpeed = 1.3f;

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private CharacterController body;
        private Vector3 velocity;
        private Vector3? mark;
        private float fall;

        public bool AtMark => mark == null;

        public void Configure(Transform lead, Animator anim) { leader = lead; animator = anim; }

        public void GoTo(Vector3 position) { mark = position; }
        public void Release() { mark = null; }

        private void Awake() { body = GetComponent<CharacterController>(); }

        private void Update()
        {
            if (leader == null) return;
            Vector3 target;
            float stopAt;
            if (mark.HasValue)
            {
                target = mark.Value;
                stopAt = 0.15f;
            }
            else
            {
                Vector3 back = -leader.forward;
                Vector3 side = leader.right * sideOffset;
                target = leader.position + back * keepDistance * 0.6f + side;
                stopAt = 0.6f;
            }
            Vector3 to = target - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 want = dist > stopAt ? to.normalized * walkSpeed * Mathf.Clamp01((dist - stopAt) * 1.5f + 0.35f) : Vector3.zero;
            if (mark.HasValue && dist <= stopAt) mark = null;
            Vector3 flat = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z), want, 3f * Time.deltaTime);
            if (body != null)
            {
                fall = body.isGrounded ? -1f : fall - 9.81f * Time.deltaTime;
                velocity = new Vector3(flat.x, fall, flat.z);
                body.Move(velocity * Time.deltaTime);
            }
            else
            {
                velocity = flat;
                transform.position += flat * Time.deltaTime;
            }
            Vector3 face = flat.sqrMagnitude > 0.02f ? flat : (leader.position - transform.position);
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face.normalized), 240f * Time.deltaTime);
            if (animator != null)
            {
                animator.SetFloat(SpeedId, flat.magnitude, 0.12f, Time.deltaTime);
                animator.SetBool(GroundedId, true);
            }
        }
    }
}
