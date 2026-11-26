using System.Collections;
using Ankhu.Opening;
using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// Carries the player along a path of marks when a rule moves a place-state ("ankhu": ground to ledge).
    /// Walking and the character controller are switched off while riding, so the path ignores colliders.
    /// The path runs marks[0] to marks[n-1] for the forward state and back again for the other.
    /// </summary>
    public sealed class RealmRide : MonoBehaviour
    {
        public RealmObjectView view;
        public Transform rider;
        public string forwardState = "ledge";
        public Vector3[] marks = new Vector3[0];
        public float speed = 2.2f;
        public float bob = 0.06f;

        private Coroutine run;

        private void OnEnable() { if (view != null) view.StateShown += OnShown; }
        private void OnDisable() { if (view != null) view.StateShown -= OnShown; }

        private void OnShown(string prev, string next)
        {
            if (string.IsNullOrEmpty(prev) || rider == null || marks.Length < 2) return;
            if (run != null) StopCoroutine(run);
            run = StartCoroutine(Ride(next == forwardState));
        }

        private IEnumerator Ride(bool forward)
        {
            var walk = rider.GetComponent<WalkPlayer>();
            var body = rider.GetComponent<CharacterController>();
            var anim = rider.GetComponentInChildren<Animator>();
            if (walk != null) walk.enabled = false;
            if (body != null) body.enabled = false;

            int n = marks.Length;
            Vector3 start = rider.position;
            Vector3 first = forward ? marks[0] : marks[n - 1];
            yield return Leg(start, first, anim);
            for (int i = 1; i < n; i++)
            {
                Vector3 a = forward ? marks[i - 1] : marks[n - i];
                Vector3 b = forward ? marks[i] : marks[n - 1 - i];
                yield return Leg(a, b, anim);
            }
            SetSpeed(anim, 0f);
            if (body != null) body.enabled = true;
            if (walk != null) walk.enabled = true;
            run = null;
        }

        private IEnumerator Leg(Vector3 a, Vector3 b, Animator anim)
        {
            float len = Vector3.Distance(a, b);
            if (len < 0.01f) yield break;
            Vector3 flat = b - a;
            flat.y = 0f;
            if (flat.sqrMagnitude > 1e-4f) rider.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            SetSpeed(anim, 1.2f);
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime * speed / len);
                rider.position = Vector3.Lerp(a, b, t) + Vector3.up * Mathf.Abs(Mathf.Sin(t * len * 3.1f)) * bob;
                yield return null;
            }
            rider.position = b;
        }

        private static void SetSpeed(Animator anim, float v)
        {
            if (anim == null) return;
            foreach (AnimatorControllerParameter p in anim.parameters)
                if (p.type == AnimatorControllerParameterType.Float && p.name == "Speed") { anim.SetFloat("Speed", v); return; }
        }
    }
}
