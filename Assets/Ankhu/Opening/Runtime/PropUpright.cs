using UnityEngine;

namespace Ankhu.Opening
{
    /// <summary>
    /// Keeps a hand-held staff upright after animation. Anpu's was-sceptre is a rigid mesh skinned to the Prop.R bone,
    /// so on borrowed locomotion clips the wrist swings it out like a pike. Each LateUpdate this turns the socket bone
    /// about its own pivot (the grip) so the staff's long axis points to world up, the way a god walks with a was-sceptre.
    /// </summary>
    public sealed class PropUpright : MonoBehaviour
    {
        [SerializeField] private string socketName = "Prop.R";
        [SerializeField] private string rendererName = "Prop_WasSceptre";
        [Tooltip("1 = exactly vertical. Slightly under 1 keeps a little life from the hand.")]
        [Range(0f, 1f)] [SerializeField] private float strength = 0.92f;

        [Tooltip("Staff long axis (foot to head) in the socket's space, measured in the editor from the baked mesh.")]
        [SerializeField] private Vector3 localAxis = Vector3.zero;

        [Tooltip("Staff foot in the socket's space, measured in the editor. Zero = do not plant.")]
        [SerializeField] private Vector3 localFoot = Vector3.zero;
        [Tooltip("The staff slides through the grip at most this far to put its foot on the ground.")]
        [SerializeField] private float maxSlide = 0.6f;

        private Transform socket;

        private void Awake() { Resolve(); }

        public void Resolve()
        {
            socket = null;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == socketName) { socket = t; break; }
            if (socket == null) socket = FindSocket();
            if (socket == null) { Debug.LogWarning("[PropUpright] " + name + ": no socket " + socketName); return; }
            if (localAxis.sqrMagnitude > 0.5f) return;
            localAxis = Vector3.up;
            foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name != rendererName || r.sharedMesh == null) continue;
                int i = System.Array.IndexOf(r.bones, socket);
                if (i < 0 || i >= r.sharedMesh.bindposes.Length) break;
                // The staff is modelled standing, so its long axis is mesh up. Carry that into the socket's own space.
                Vector3 e = r.sharedMesh.bounds.extents;
                Vector3 meshAxis = e.y >= e.x && e.y >= e.z ? Vector3.up : (e.x >= e.z ? Vector3.right : Vector3.forward);
                localAxis = r.sharedMesh.bindposes[i].MultiplyVector(meshAxis).normalized;
                return;
            }
        }

        /// <summary>
        /// The staff's pivot: the Prop.R bone on the old Mixamo rig, or on the AccuRig rig the Socket_Prop_WasSceptre
        /// transform under the right hand (pivot at the fist, the staff a plain MeshRenderer below it).
        /// </summary>
        public Transform FindSocket()
        {
            Transform fallback = null;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == socketName) return t;
                if (t.name == "Socket_" + rendererName) fallback = t;
            }
            return fallback;
        }

        public string SocketName => socketName;
        public string RendererName => rendererName;
        public void SetAxis(Vector3 axisInSocketSpace) { localAxis = axisInSocketSpace.normalized; }
        public void SetFoot(Vector3 footInSocketSpace) { localFoot = footInSocketSpace; }
        public Vector3 LocalAxis => localAxis;
        public Vector3 LocalFoot => localFoot;

        public void Apply()
        {
            if (socket == null) return;
            Vector3 now = socket.rotation * localAxis;
            Quaternion fix = Quaternion.FromToRotation(now, Vector3.up);
            socket.rotation = Quaternion.Slerp(Quaternion.identity, fix, strength) * socket.rotation;
            // Plant the forked foot on the road: slide the staff through the hand, as a walker does with a long staff.
            if (localFoot.sqrMagnitude < 1e-6f) return;
            float drop = socket.TransformPoint(localFoot).y - (transform.position.y + 0.01f);
            socket.position -= Vector3.up * Mathf.Clamp(drop, -maxSlide, maxSlide);
        }

        private void LateUpdate() { Apply(); }
    }
}
