using UnityEngine;

namespace Ankhu.Painted
{
    /// <summary>
    /// Glides a painted card (or a camera) from one local position to another over a set time, eased in and out,
    /// starting when the component is enabled. Used by the Wernes finale for the barque's glide down the river,
    /// Apep's shadow passing under it and the slow camera push that makes the painted layers parallax.
    /// Optional fade: scales the card's _Tint alpha from fadeFrom to fadeTo over the same time.
    /// </summary>
    public sealed class PaintedDrift : MonoBehaviour
    {
        public Vector3 from;
        public Vector3 to;
        public float seconds = 20f;
        public float delay;
        [Tooltip("Ease in and out (smoothstep). Off is linear.")]
        public bool ease = true;
        public bool fade;
        public float fadeFrom = 0f;
        public float fadeTo = 1f;

        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private float started;
        private Renderer rend;
        private MaterialPropertyBlock block;
        private Color baseTint = Color.white;

        private void OnEnable()
        {
            started = Time.time;
            transform.localPosition = from;
            rend = GetComponent<Renderer>();
            if (fade && rend != null)
            {
                block ??= new MaterialPropertyBlock();
                if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(TintId)) baseTint = rend.sharedMaterial.GetColor(TintId);
            }
            Apply(0f);
        }

        private void Update()
        {
            float t = seconds <= 0f ? 1f : Mathf.Clamp01((Time.time - started - delay) / seconds);
            Apply(t);
        }

        private void Apply(float t)
        {
            float k = ease ? t * t * (3f - 2f * t) : t;
            transform.localPosition = Vector3.LerpUnclamped(from, to, k);
            if (fade && rend != null)
            {
                rend.GetPropertyBlock(block);
                Color c = baseTint;
                c.a = baseTint.a * Mathf.Lerp(fadeFrom, fadeTo, k);
                block.SetColor(TintId, c);
                rend.SetPropertyBlock(block);
            }
        }
    }
}
