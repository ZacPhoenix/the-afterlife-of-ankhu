using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// The shabti's endless work: while this driver object is active, the worker walks between the channel and
    /// the basin, dips and pours. When the driver is deactivated the worker eases back to its rest pose.
    /// </summary>
    public sealed class WorkerLoop : UnityEngine.MonoBehaviour
    {
        public UnityEngine.Transform worker;
        public UnityEngine.Transform jars;
        public UnityEngine.Vector3 from;
        public UnityEngine.Vector3 to;
        public float period = 5f;
        private UnityEngine.Vector3 rest;
        private bool haveRest;
        private float t;

        private void OnEnable()
        {
            if (worker != null && !haveRest) { rest = worker.localPosition; haveRest = true; }
            t = 0f;
        }

        private void Update()
        {
            if (worker == null) return;
            t += UnityEngine.Time.deltaTime / period;
            float k = 0.5f - 0.5f * UnityEngine.Mathf.Cos(t * UnityEngine.Mathf.PI * 2f);
            worker.localPosition = UnityEngine.Vector3.Lerp(from, to, k);
            float dir = UnityEngine.Mathf.Sin(t * UnityEngine.Mathf.PI * 2f);
            worker.localRotation = UnityEngine.Quaternion.Euler(0f, dir >= 0f ? 90f : -90f, 0f);
            if (jars != null)
            {
                float tip = UnityEngine.Mathf.Clamp01((k - 0.9f) * 10f) * 35f;
                jars.localRotation = UnityEngine.Quaternion.Euler(0f, 0f, tip);
            }
        }

        private void OnDisable()
        {
            if (worker != null && haveRest)
            {
                worker.localPosition = rest;
                worker.localRotation = UnityEngine.Quaternion.identity;
            }
            if (jars != null) jars.localRotation = UnityEngine.Quaternion.identity;
        }
    }
}
