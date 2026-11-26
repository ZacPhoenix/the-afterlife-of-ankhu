using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// A living flame light: an uneven breath laid over whatever intensity the room's state gives the light, so a lamp
    /// or a channel of fire keeps moving while the puzzle waits for Ankhu (Zac 2026-11-26: the environment animates in
    /// real time, the puzzle only moves on commands). When a state change writes a new intensity, that becomes the
    /// base. A dark light (intensity 0) stays dark.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class LightBreath : MonoBehaviour
    {
        public float amount = 0.16f;
        public float speed = 3.1f;
        private Light lightRef;
        private float seed;
        private float baseIntensity;
        private float written = -1f;

        /// <summary>The intensity the room's state asks for, without the breath.</summary>
        public float BaseIntensity => baseIntensity;

        private void Awake()
        {
            lightRef = GetComponent<Light>();
            seed = Random.value * 100f;
            baseIntensity = lightRef != null ? lightRef.intensity : 0f;
        }

        private void LateUpdate()
        {
            if (lightRef == null) return;
            // Anyone else who wrote the light this frame (a state pose blending) sets the new base.
            if (!Mathf.Approximately(lightRef.intensity, written)) baseIntensity = lightRef.intensity;
            float t = Time.time * speed + seed;
            float n = Mathf.PerlinNoise(t, seed) * 0.7f + Mathf.PerlinNoise(t * 2.7f, seed + 5f) * 0.3f;
            written = baseIntensity * (1f - amount + n * amount * 2f);
            lightRef.intensity = written;
        }
    }
}
