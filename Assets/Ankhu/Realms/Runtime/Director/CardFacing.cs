using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// Keeps a painted sprite card square to the active camera, the way a painted figure sits on the canvas.
    /// The card still takes its roll from its parent, so a wheel's spin, a jar's tip or a body's lean reads on screen,
    /// but a yaw (a worker turning to walk) never turns it edge-on.
    /// </summary>
    public sealed class CardFacing : MonoBehaviour
    {
        private Quaternion rest = Quaternion.identity;
        private bool haveRest;
        private static Camera cached;
        private static int cachedFrame = -1;

        private void Awake()
        {
            rest = transform.localRotation;
            haveRest = true;
        }

        private static Camera Active()
        {
            if (cachedFrame == Time.frameCount && cached != null) return cached;
            cachedFrame = Time.frameCount;
            cached = Camera.main;
            if (cached == null || !cached.isActiveAndEnabled)
            {
                cached = null;
                float best = float.MinValue;
                foreach (Camera c in Camera.allCameras)
                    if (c.isActiveAndEnabled && c.targetTexture == null && c.depth > best) { best = c.depth; cached = c; }
            }
            return cached;
        }

        private void LateUpdate()
        {
            Camera cam = Active();
            if (cam == null) return;
            if (!haveRest) { rest = transform.localRotation; haveRest = true; }
            Quaternion parent = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            Vector3 fwd = cam.transform.forward;
            Vector3 up = Vector3.ProjectOnPlane(parent * rest * Vector3.up, fwd);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.up, fwd);
            transform.rotation = Quaternion.LookRotation(fwd, up);
        }
    }
}
