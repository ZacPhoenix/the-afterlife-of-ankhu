using UnityEngine;

namespace Ankhu.Opening
{
    /// <summary>
    /// Zac 2026-11-25: Anpu's voice was too bassy. A gentle, non-destructive low-end roll-off on the AudioSource
    /// on the same GameObject: a 12 dB per octave high-pass plus a low-shelf cut. The source takes stay untouched,
    /// and every value can be tweaked live in the inspector. Put one source per GameObject, because a filter
    /// component acts on the AudioSource beside it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoiceTone : MonoBehaviour
    {
        [Tooltip("High-pass corner in Hz (12 dB per octave, Butterworth).")]
        [Range(20f, 300f)] public float highPassHz = 90f;
        [Tooltip("Low-shelf corner in Hz.")]
        [Range(60f, 600f)] public float lowShelfHz = 200f;
        [Tooltip("Low-shelf gain in dB. Negative cuts the low end.")]
        [Range(-12f, 6f)] public float lowShelfDb = -3f;
        [Tooltip("Off passes the voice through untouched.")]
        public bool shaping = true;

        /// <summary>Anpu's settings, in one place so every scene that voices him sounds the same.</summary>
        public static VoiceTone AddAnpu(GameObject go)
        {
            VoiceTone t = go.GetComponent<VoiceTone>() ?? go.AddComponent<VoiceTone>();
            t.highPassHz = 90f;
            t.lowShelfHz = 200f;
            t.lowShelfDb = -3f;
            t.shaping = true;
            return t;
        }

        private struct Biquad
        {
            public float b0, b1, b2, a1, a2;
        }

        private const int MaxChannels = 8;
        private readonly float[] hx1 = new float[MaxChannels], hx2 = new float[MaxChannels], hy1 = new float[MaxChannels], hy2 = new float[MaxChannels];
        private readonly float[] sx1 = new float[MaxChannels], sx2 = new float[MaxChannels], sy1 = new float[MaxChannels], sy2 = new float[MaxChannels];
        private Biquad hp, ls;
        private float sampleRate = 48000f;
        private float builtHp = -1f, builtLsHz = -1f, builtLsDb = 99f;
        private volatile bool on = true;

        private void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            Rebuild();
        }

        private void OnValidate() { if (Application.isPlaying) Rebuild(); }

        private void Update()
        {
            on = shaping;
            if (highPassHz != builtHp || lowShelfHz != builtLsHz || lowShelfDb != builtLsDb) Rebuild();
        }

        // RBJ audio EQ cookbook coefficients.
        private void Rebuild()
        {
            float fs = Mathf.Max(8000f, sampleRate);
            float w = 2f * Mathf.PI * Mathf.Clamp(highPassHz, 10f, fs * 0.45f) / fs;
            float cw = Mathf.Cos(w), sw = Mathf.Sin(w);
            float alpha = sw / (2f * 0.7071f);
            float a0 = 1f + alpha;
            hp = new Biquad
            {
                b0 = (1f + cw) / 2f / a0, b1 = -(1f + cw) / a0, b2 = (1f + cw) / 2f / a0,
                a1 = -2f * cw / a0, a2 = (1f - alpha) / a0,
            };

            float A = Mathf.Pow(10f, lowShelfDb / 40f);
            w = 2f * Mathf.PI * Mathf.Clamp(lowShelfHz, 10f, fs * 0.45f) / fs;
            cw = Mathf.Cos(w); sw = Mathf.Sin(w);
            alpha = sw / 2f * Mathf.Sqrt(2f); // shelf slope S = 1
            float sq = 2f * Mathf.Sqrt(A) * alpha;
            a0 = (A + 1f) + (A - 1f) * cw + sq;
            ls = new Biquad
            {
                b0 = A * ((A + 1f) - (A - 1f) * cw + sq) / a0,
                b1 = 2f * A * ((A - 1f) - (A + 1f) * cw) / a0,
                b2 = A * ((A + 1f) - (A - 1f) * cw - sq) / a0,
                a1 = -2f * ((A - 1f) + (A + 1f) * cw) / a0,
                a2 = ((A + 1f) + (A - 1f) * cw - sq) / a0,
            };
            builtHp = highPassHz; builtLsHz = lowShelfHz; builtLsDb = lowShelfDb;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!on || channels > MaxChannels) return;
            Biquad h = hp, s = ls;
            for (int i = 0; i < data.Length; i += channels)
            {
                for (int c = 0; c < channels; c++)
                {
                    float x = data[i + c];
                    float y = h.b0 * x + h.b1 * hx1[c] + h.b2 * hx2[c] - h.a1 * hy1[c] - h.a2 * hy2[c];
                    hx2[c] = hx1[c]; hx1[c] = x; hy2[c] = hy1[c]; hy1[c] = y;
                    float z = s.b0 * y + s.b1 * sx1[c] + s.b2 * sx2[c] - s.a1 * sy1[c] - s.a2 * sy2[c];
                    sx2[c] = sx1[c]; sx1[c] = y; sy2[c] = sy1[c]; sy1[c] = z;
                    data[i + c] = z;
                }
            }
        }
    }
}
