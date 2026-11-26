using UnityEngine;

namespace Ankhu.Player
{
    /// <summary>
    /// Measured standing size of a rigged model, written by the character setup from the baked
    /// bind-pose mesh. <see cref="AnkhuVisual"/> scales and grounds with these numbers instead of
    /// renderer bounds, because skinned bounds live in the root bone's space and a tilted root bone
    /// makes them taller than the mesh.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnkhuModelMetrics : MonoBehaviour
    {
        [Tooltip("Height from sole to crown in the model's own units, at scale 1.")]
        [SerializeField, Min(0.01f)] private float standingHeight = 1.8f;
        [Tooltip("Local y of the soles at scale 1.")]
        [SerializeField] private float soleY;

        public float StandingHeight => standingHeight;
        public float SoleY => soleY;

        public void Set(float height, float sole)
        {
            standingHeight = Mathf.Max(0.01f, height);
            soleY = sole;
        }
    }
}
