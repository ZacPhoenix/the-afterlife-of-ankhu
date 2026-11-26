using UnityEngine;

namespace Ankhu.Opening
{
    /// <summary>
    /// Keeps a painted backdrop quad filling its camera's view at any screen aspect. The painting is made at
    /// 16:9 from this exact camera, so at 16:9 it lines up with the invisible ground. Wider screens scale it to
    /// cover (cropping top and bottom slightly), narrower screens crop the sides.
    /// </summary>
    [ExecuteAlways]
    public sealed class BackdropFit : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private float distance = 300f;
        [SerializeField] private float imageAspect = 16f / 9f;
        [Tooltip("The lens the painting was made with. The camera may zoom in from it, cropping the painting in place.")]
        [SerializeField] private float paintedFov;

        public void Configure(Camera c, float d, float aspect)
        {
            cam = c;
            distance = d;
            imageAspect = aspect;
            paintedFov = c != null ? c.fieldOfView : 0f;
            Fit();
        }

        private void LateUpdate() { Fit(); }

        private void Fit()
        {
            if (cam == null) return;
            float h = 2f * distance * Mathf.Tan((paintedFov > 0f ? paintedFov : cam.fieldOfView) * 0.5f * Mathf.Deg2Rad);
            float w = h * imageAspect;
            // Sized for the painted lens, so a zoomed camera crops into the painting and it stays on the 3D ground.
            float viewW = h * cam.aspect;
            if (viewW > w)
            {
                float k = viewW / w;
                w *= k;
                h *= k;
            }
            transform.localPosition = new Vector3(0f, 0f, distance);
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(w * 1.005f, h * 1.005f, 1f);
        }
    }
}
