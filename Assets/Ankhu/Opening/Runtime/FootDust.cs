using UnityEngine;

namespace Ankhu.Opening
{
    /// <summary>
    /// A small puff of pale road dust where a foot lands, so the walkers have weight on the paving. Each character
    /// carries one and counts its own distance; all of them share one particle system in the scene.
    /// </summary>
    public sealed class FootDust : MonoBehaviour
    {
        [SerializeField] private ParticleSystem dust;
        [SerializeField] private Animator animator;
        [SerializeField] private float stride = 0.72f;
        [SerializeField] private Color tint = new Color(0.72f, 0.64f, 0.53f, 0.42f);

        private Vector3 last;
        private float travelled;
        private bool left;
        private Transform footL, footR;

        public void Configure(ParticleSystem system, Animator a, float strideLength)
        {
            dust = system;
            animator = a;
            stride = strideLength;
        }

        private void Start()
        {
            last = transform.position;
            if (animator != null && animator.isHuman)
            {
                footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                footR = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
        }

        private void LateUpdate()
        {
            Vector3 p = transform.position;
            Vector3 d = p - last;
            d.y = 0f;
            last = p;
            travelled += d.magnitude;
            if (travelled < stride || dust == null) return;
            travelled = 0f;
            left = !left;
            Transform foot = left ? footL : footR;
            Vector3 at = foot != null ? foot.position : p;
            at.y = p.y + 0.02f;
            var e = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            e.position = at;
            for (int i = 0; i < 7; i++)
            {
                Vector2 r = Random.insideUnitCircle * 0.5f;
                e.velocity = new Vector3(r.x, Random.Range(0.0f, 0.04f), r.y);
                e.startSize = Random.Range(0.22f, 0.4f);
                e.startLifetime = Random.Range(0.7f, 1.2f);
                e.startColor = tint;
                dust.Emit(e, 1);
            }
        }
    }
}
