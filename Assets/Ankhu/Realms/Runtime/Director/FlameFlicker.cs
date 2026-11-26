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
}
