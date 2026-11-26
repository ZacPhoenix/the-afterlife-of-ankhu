using System;
using UnityEngine;

namespace Ankhu.Opening
{
    /// <summary>
    /// The road as a polyline on the ground (XZ). Gives arc length, nearest-point projection and the point and
    /// tangent at an arc length. The escorts walk along it, the player is projected onto it, cameras cut by it.
    /// </summary>
    public sealed class WalkPath : MonoBehaviour
    {
        [SerializeField] private Vector3[] points = Array.Empty<Vector3>();
        private float[] cumulative;

        public float Length { get { Ensure(); return cumulative.Length > 0 ? cumulative[cumulative.Length - 1] : 0f; } }
        public Vector3[] Points => points;

        public void SetPoints(Vector3[] p)
        {
            points = p;
            cumulative = null;
        }

        private void Ensure()
        {
            if (cumulative != null && cumulative.Length == points.Length) return;
            cumulative = new float[points.Length];
            for (int i = 1; i < points.Length; i++)
            {
                cumulative[i] = cumulative[i - 1] + Flat(points[i] - points[i - 1]).magnitude;
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>Arc length of the closest point on the path to a world position.</summary>
        public float Project(Vector3 world, out float lateral)
        {
            Ensure();
            float best = float.MaxValue;
            float bestS = 0f;
            lateral = 0f;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                Vector3 a = Flat(points[i]);
                Vector3 b = Flat(points[i + 1]);
                Vector3 p = Flat(world);
                Vector3 ab = b - a;
                float len2 = Mathf.Max(1e-6f, ab.sqrMagnitude);
                float k = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
                Vector3 c = a + ab * k;
                float d = (p - c).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    bestS = cumulative[i] + Mathf.Sqrt(len2) * k;
                    Vector3 right = Vector3.Cross(Vector3.up, ab.normalized);
                    lateral = Vector3.Dot(p - c, right);
                }
            }
            return bestS;
        }

        public Vector3 PointAt(float s)
        {
            Ensure();
            if (points.Length == 0) return Vector3.zero;
            s = Mathf.Clamp(s, 0f, Length);
            for (int i = 0; i + 1 < points.Length; i++)
            {
                if (s <= cumulative[i + 1] || i + 2 == points.Length)
                {
                    float seg = Mathf.Max(1e-6f, cumulative[i + 1] - cumulative[i]);
                    return Vector3.Lerp(points[i], points[i + 1], (s - cumulative[i]) / seg);
                }
            }
            return points[points.Length - 1];
        }

        public Vector3 TangentAt(float s)
        {
            Vector3 a = PointAt(s - 0.6f);
            Vector3 b = PointAt(s + 0.6f);
            Vector3 t = Flat(b - a);
            return t.sqrMagnitude > 1e-6f ? t.normalized : Vector3.forward;
        }

        /// <summary>Point at arc length s, pushed sideways by lateral metres (positive is to the right of travel).</summary>
        public Vector3 OffsetPoint(float s, float lateral)
        {
            Vector3 t = TangentAt(s);
            return PointAt(s) + Vector3.Cross(Vector3.up, t) * lateral;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
            for (int i = 0; i + 1 < points.Length; i++) Gizmos.DrawLine(points[i], points[i + 1]);
        }
    }
}
