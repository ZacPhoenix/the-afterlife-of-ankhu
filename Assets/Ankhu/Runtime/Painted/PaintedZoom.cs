using UnityEngine;

namespace Ankhu.Painted
{
    /// <summary>
    /// A slow push in on a painted shot, done as a lens change rather than a dolly. BackdropFit sizes the painting for
    /// the lens it was painted with, so narrowing the field of view crops into the painting in place and every card and
    /// the cast stay on the painted floor at any depth. Starts when enabled, eased in and out, then holds.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PaintedZoom : MonoBehaviour
    {
        public float fromFov = 42f;
        public float toFov = 36f;
        public float seconds = 90f;
        public float delay = 2f;

        private Camera cam;
        private float started;

        private void OnEnable()
        {
            cam = GetComponent<Camera>();
            started = Time.time;
            cam.fieldOfView = fromFov;
        }

        private void LateUpdate()
        {
            float t = seconds <= 0f ? 1f : Mathf.Clamp01((Time.time - started - delay) / seconds);
            float k = t * t * (3f - 2f * t);
            cam.fieldOfView = Mathf.Lerp(fromFov, toFov, k);
        }
    }
}
