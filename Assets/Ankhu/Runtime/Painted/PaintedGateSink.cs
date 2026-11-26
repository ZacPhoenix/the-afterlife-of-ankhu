using UnityEngine;

namespace Ankhu.Painted
{
    /// <summary>
    /// Painted look for a lift gate. The rules move the gate's bars up when it opens, but the master paints
    /// each gate as one bronze post standing on a stone slab, and a raised post floats over the wall reliefs.
    /// This sits on the post's paint quad (a child of the bars) and turns the lift into a sink of the same
    /// distance. The material clips everything below the slab, so the post slides down into the stone.
    /// Visual only: the gate's blocker and logic are untouched.
    /// </summary>
    public sealed class PaintedGateSink : MonoBehaviour
    {
        [SerializeField] private Transform bars;
        [SerializeField] private float barsClosedY;
        [SerializeField] private Vector3 closedPosition;

        public void Configure(Transform gateBars, Vector3 quadClosedPosition)
        {
            bars = gateBars;
            // The greybox builds the bars at local zero under the gate root when closed.
            barsClosedY = gateBars == null ? 0f : gateBars.parent != null ? gateBars.parent.position.y : gateBars.position.y;
            closedPosition = quadClosedPosition;
        }

        private void LateUpdate()
        {
            if (bars == null)
            {
                return;
            }
            float lift = bars.position.y - barsClosedY;
            transform.position = closedPosition + Vector3.down * lift;
        }
    }
}
