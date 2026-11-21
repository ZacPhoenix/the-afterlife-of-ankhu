using Ankhu.Layout;
using Ankhu.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ankhu.CameraRig
{
    /// <summary>
    /// Side-on camera rig. Follows the target's x with smoothing, keeps y at
    /// <see cref="BorrowedDawnLayout.CameraFixedY"/>, and clamps x so the view at the play plane
    /// (z = 0) never shows beyond RoomMinX..RoomMaxX.
    ///
    /// Orthographic mode uses <see cref="BorrowedDawnLayout.CameraOrthoSize"/>. Perspective mode uses a
    /// narrow field of view and places the camera so the vertical span at z = 0 equals
    /// 2 * CameraOrthoSize, so both modes frame the same play area. F2 toggles the mode at runtime.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        public enum Projection
        {
            Orthographic,
            Perspective
        }

        [SerializeField] private Transform target;
        [SerializeField] private Projection projection = Projection.Perspective;
        [SerializeField, Range(5f, 60f)] private float perspectiveFieldOfView = 20f;
        [SerializeField, Min(0f)] private float smoothTime = 0.2f;
        [Tooltip("Extra far clip distance beyond the play plane, for scenery behind it.")]
        [SerializeField, Min(1f)] private float depthBeyondPlane = 60f;

        [Header("Framing (defaults are the Borrowed Dawn room)")]
        [SerializeField, Min(0.5f)] private float orthoSize = BorrowedDawnLayout.CameraOrthoSize;
        [SerializeField] private float fixedY = BorrowedDawnLayout.CameraFixedY;
        [SerializeField] private float boundsMinX = BorrowedDawnLayout.RoomMinX;
        [SerializeField] private float boundsMaxX = BorrowedDawnLayout.RoomMaxX;

        private Camera cam;
        private float velocityX;

        public Transform Target => target;
        public Projection Mode => projection;
        public float PerspectiveFieldOfView => perspectiveFieldOfView;

        /// <summary>Distance from the camera to z = 0. Equal in both modes so depth content stays in view.</summary>
        public float PlaneDistance => DistanceForFieldOfView(orthoSize, perspectiveFieldOfView);

        /// <summary>Sets the framing for a scene other than Borrowed Dawn. Half height at z = 0, camera y, and x bounds.</summary>
        public void SetFraming(float halfHeight, float cameraY, float minX, float maxX)
        {
            orthoSize = Mathf.Max(0.5f, halfHeight);
            fixedY = cameraY;
            boundsMinX = minX;
            boundsMaxX = maxX;
        }

        public void Configure(Transform followTarget, Projection mode)
        {
            target = followTarget;
            projection = mode;
        }

        public void SetProjection(Projection mode)
        {
            projection = mode;
            ApplyProjection();
        }

        private Camera Cam
        {
            get
            {
                if (cam == null)
                {
                    cam = GetComponent<Camera>();
                }
                return cam;
            }
        }

        private void Start()
        {
            SnapToTarget();
        }

        private void Update()
        {
            if (GameplayKeys.Pressed(Key.F2))
            {
                SetProjection(projection == Projection.Perspective ? Projection.Orthographic : Projection.Perspective);
            }
        }

        private void LateUpdate()
        {
            ApplyProjection();
            if (target == null)
            {
                return;
            }
            float desired = ClampX(target.position.x);
            Vector3 p = transform.position;
            float x = smoothTime > 0f ? Mathf.SmoothDamp(p.x, desired, ref velocityX, smoothTime) : desired;
            transform.position = new Vector3(x, fixedY, -PlaneDistance);
        }

        /// <summary>Applies projection settings and jumps to the target with no smoothing. Works in edit mode.</summary>
        public void SnapToTarget()
        {
            ApplyProjection();
            float x = target != null ? ClampX(target.position.x) : ClampX(transform.position.x);
            velocityX = 0f;
            transform.position = new Vector3(x, fixedY, -PlaneDistance);
        }

        private void ApplyProjection()
        {
            Camera c = Cam;
            transform.rotation = Quaternion.identity;
            float distance = PlaneDistance;
            if (projection == Projection.Orthographic)
            {
                c.orthographic = true;
                c.orthographicSize = orthoSize;
            }
            else
            {
                c.orthographic = false;
                c.fieldOfView = perspectiveFieldOfView;
            }
            c.nearClipPlane = 0.3f;
            c.farClipPlane = Mathf.Max(c.farClipPlane, distance + depthBeyondPlane);
        }

        public float ClampX(float desiredX)
        {
            float halfWidth = orthoSize * Mathf.Max(0.01f, Cam.aspect);
            return ClampCameraX(desiredX, halfWidth, boundsMinX, boundsMaxX);
        }

        /// <summary>
        /// Camera distance from a plane at which a perspective camera with the given vertical field of
        /// view sees a vertical half span of <paramref name="halfHeight"/>.
        /// </summary>
        public static float DistanceForFieldOfView(float halfHeight, float verticalFovDegrees)
        {
            float halfAngle = Mathf.Clamp(verticalFovDegrees, 1f, 179f) * 0.5f * Mathf.Deg2Rad;
            return halfHeight / Mathf.Tan(halfAngle);
        }

        /// <summary>
        /// Clamps a camera x so [x - halfWidth, x + halfWidth] stays inside [minX, maxX]. When the
        /// room is narrower than the view the camera centres on the room.
        /// </summary>
        public static float ClampCameraX(float desiredX, float halfWidth, float minX, float maxX)
        {
            float low = minX + halfWidth;
            float high = maxX - halfWidth;
            if (low > high)
            {
                return (minX + maxX) * 0.5f;
            }
            return Mathf.Clamp(desiredX, low, high);
        }
    }
}
