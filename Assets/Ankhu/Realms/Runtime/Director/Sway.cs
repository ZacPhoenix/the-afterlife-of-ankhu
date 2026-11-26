using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>A gentle idle sway on top of whatever pose a part holds (reed heads, a hanging bucket, a held beam's hum).</summary>
    public sealed class Sway : MonoBehaviour
    {
        public Vector3 axis = Vector3.forward;
        public float degrees = 3f;
        public float speed = 1.3f;
        private float seed;
        private Quaternion last = Quaternion.identity;

        private void Awake() { seed = Random.value * 20f; }

        private void LateUpdate()
        {
            // Remove last frame's sway, then add this frame's, so posing by other scripts is preserved.
            transform.localRotation = transform.localRotation * Quaternion.Inverse(last);
            last = Quaternion.AngleAxis(Mathf.Sin(Time.time * speed + seed) * degrees, axis);
            transform.localRotation = transform.localRotation * last;
        }
    }
}
