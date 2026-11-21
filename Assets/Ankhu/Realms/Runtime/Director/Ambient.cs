using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>Firelight: a gentle, uneven flicker on a point light.</summary>
    public sealed class FlameFlicker : MonoBehaviour
    {
        public float baseIntensity = 2.4f;
        public float amount = 0.18f;
        private Light lightRef;
        private float seed;

        private void Awake()
        {
            lightRef = GetComponent<Light>();
            seed = Random.value * 100f;
        }

        private void Update()
        {
            if (lightRef == null) return;
            float t = Time.time * 3.1f + seed;
            float n = Mathf.PerlinNoise(t, seed) * 0.7f + Mathf.PerlinNoise(t * 2.7f, seed + 5f) * 0.3f;
            lightRef.intensity = baseIntensity * (1f - amount + n * amount * 2f);
        }
    }

    /// <summary>Restless water: the surface pieces heave and tilt. Amplitude 0 is a mirror.</summary>
    public sealed class WaterMotion : MonoBehaviour
    {
        public float amplitude = 0.12f;
        public float speed = 1.2f;
        private Vector3 basePos;
        private Quaternion baseRot;
        private float seed;

        private void Awake()
        {
            basePos = transform.localPosition;
            baseRot = transform.localRotation;
            seed = Random.value * 50f;
        }

        private void Update()
        {
            float t = Time.time * speed + seed;
            transform.localPosition = basePos + new Vector3(0f, Mathf.Sin(t) * amplitude * 0.5f, 0f);
            transform.localRotation = baseRot * Quaternion.Euler(Mathf.Sin(t * 0.8f) * amplitude * 18f, 0f, Mathf.Cos(t * 0.6f) * amplitude * 14f);
        }
    }

    /// <summary>A slow bob for things afloat (the ferry).</summary>
    public sealed class Bob : MonoBehaviour
    {
        public float amplitude = 0.04f;
        public float roll = 1.5f;
        private Vector3 basePos;
        private Quaternion baseRot;

        private void Awake()
        {
            basePos = transform.localPosition;
            baseRot = transform.localRotation;
        }

        private void Update()
        {
            float t = Time.time;
            transform.localPosition = basePos + new Vector3(0f, Mathf.Sin(t * 0.9f) * amplitude, 0f);
            transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.7f) * roll);
        }
    }
}

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
