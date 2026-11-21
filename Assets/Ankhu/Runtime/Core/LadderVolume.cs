using UnityEngine;

namespace Ankhu.Core
{
    /// <summary>
    /// Climbable span. The player controller enters climbing when it overlaps this trigger and
    /// presses up or down. While climbing it snaps to <see cref="ClimbX"/>, ignores gravity and
    /// ignores <see cref="TopPlatform"/> so it can pass through the hatch.
    /// The world disables this component's collider while the ladder is stowed.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class LadderVolume : MonoBehaviour
    {
        [SerializeField] private float bottomY;
        [SerializeField] private float topY = 5f;
        [Tooltip("One-way collider at the top of the ladder (the open hatch). Ignored while climbing.")]
        [SerializeField] private Collider2D topPlatform;

        public float ClimbX => transform.position.x;
        public float BottomY => bottomY;
        public float TopY => topY;
        public Collider2D TopPlatform => topPlatform;
        public bool Available => isActiveAndEnabled && GetComponent<BoxCollider2D>().enabled;

        public void Configure(float bottom, float top, Collider2D hatchPlatform)
        {
            bottomY = bottom;
            topY = top;
            topPlatform = hatchPlatform;
            GetComponent<BoxCollider2D>().isTrigger = true;
        }
    }
}
