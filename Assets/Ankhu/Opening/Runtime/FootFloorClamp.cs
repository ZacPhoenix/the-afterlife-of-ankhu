using UnityEngine;

namespace Ankhu.Opening
{
    /// <summary>
    /// The walk clip's baked foot targets sit a few centimetres under the floor (the left one more than the right).
    /// With Foot IK on, that plants the left sole into the ground. This lifts any foot target that is below the floor
    /// back onto it and leaves everything else as the clip has it. Needs the IK pass on the animator's base layer.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Animator))]
    public sealed class FootFloorClamp : MonoBehaviour
    {
        private Animator animator;
        private Vector3 lookAt;
        private float lookTarget, lookWeight;

        private void Awake() => animator = GetComponent<Animator>();

        /// <summary>Optional head turn (Anpu looking back at Ankhu). Weight eases toward the target.</summary>
        public void SetLook(Vector3 point, float weight)
        {
            lookAt = point;
            lookTarget = Mathf.Clamp01(weight);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (layerIndex != 0 || animator == null || !animator.isHuman) return;
            if (Application.isPlaying)
            {
                lookWeight = Mathf.MoveTowards(lookWeight, lookTarget, 1.2f * Time.deltaTime);
                if (lookWeight > 0.001f)
                {
                    animator.SetLookAtPosition(lookAt);
                    animator.SetLookAtWeight(lookWeight, 0.25f, 0.8f, 0f, 0.6f);
                }
            }
            float floorY = transform.position.y;
            Clamp(AvatarIKGoal.LeftFoot, floorY + animator.leftFeetBottomHeight);
            Clamp(AvatarIKGoal.RightFoot, floorY + animator.rightFeetBottomHeight);
        }

        /// <summary>
        /// Zac 2026-11-26: the knees popped, even standing still. The clamp used to switch IK on only on frames where a
        /// foot dipped under the floor, so an idle foot hovering at floor height flipped between the clip's knee and the
        /// IK knee from frame to frame. Now the IK goal is always on, at the clip's own foot lifted to the floor, so the
        /// leg is solved the same way every frame and only moves as far as the foot does. Legacy restores the old
        /// switch, for the KneeJitterDiag comparison.
        /// </summary>
        public static bool Legacy;

        private void Clamp(AvatarIKGoal goal, float minY)
        {
            Vector3 p = animator.GetIKPosition(goal);
            if (Legacy && p.y >= minY) return;
            if (p.y < minY) p.y = minY;
            animator.SetIKPosition(goal, p);
            animator.SetIKPositionWeight(goal, 1f);
            animator.SetIKRotation(goal, animator.GetIKRotation(goal));
            animator.SetIKRotationWeight(goal, 1f);
        }
    }
}
