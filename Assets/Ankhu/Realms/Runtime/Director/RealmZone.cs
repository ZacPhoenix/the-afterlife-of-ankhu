using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// A named region of a realm scene, as an axis-aligned box in world space. Zones decide which words reach
    /// which objects, which beats fire on entry, and (with a camera index) which fixed camera is live.
    /// </summary>
    public sealed class RealmZone : MonoBehaviour
    {
        public string zoneId = "";
        public Vector3 size = new Vector3(4f, 4f, 4f);
        /// <summary>Fixed camera to cut to while Ankhu is inside, or -1 for none.</summary>
        public int cameraIndex = -1;
        /// <summary>Higher wins when zones overlap.</summary>
        public int priority;

        public Bounds WorldBounds => new Bounds(transform.position, size);

        public bool Contains(Vector3 p)
        {
            Bounds b = WorldBounds;
            return p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z && p.y >= b.min.y - 1f && p.y <= b.max.y;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = cameraIndex >= 0 ? new Color(0.3f, 0.6f, 1f, 0.25f) : new Color(1f, 0.8f, 0.3f, 0.25f);
            Gizmos.DrawCube(transform.position, size);
        }
    }
}
