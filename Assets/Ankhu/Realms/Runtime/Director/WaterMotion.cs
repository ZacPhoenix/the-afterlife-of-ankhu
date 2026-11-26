using UnityEngine;

namespace Ankhu.Realms
{
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
}
