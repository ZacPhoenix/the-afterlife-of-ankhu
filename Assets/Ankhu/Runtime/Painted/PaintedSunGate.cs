using UnityEngine;

namespace Ankhu.Painted
{
    /// <summary>
    /// Painted look for the sun gate. The rules narrow two greybox leaves toward the posts when the gate
    /// opens. Here the painted door leaves follow that motion instead: each slides outward behind the
    /// painted frame, which has a hole where the door is, and the painted dawn behind them shows through.
    /// Visual only: reads the greybox leaf width, never touches the gate's logic.
    /// </summary>
    public sealed class PaintedSunGate : MonoBehaviour
    {
        [SerializeField] private Transform westLeaf;
        [SerializeField] private Transform westDoor;
        [SerializeField] private Transform eastDoor;
        [SerializeField] private Renderer dawn;
        [SerializeField] private float closedLeafWidth = 1.2f;
        [SerializeField] private float openLeafWidth = 0.12f;
        [SerializeField] private float slide = 0.81f;
        [SerializeField] private Vector3 westClosed;
        [SerializeField] private Vector3 eastClosed;

        public void Configure(Transform greyLeaf, Transform doorW, Transform doorE, Renderer dawnQuad, float halfWidth, float slideMetres)
        {
            westLeaf = greyLeaf;
            westDoor = doorW;
            eastDoor = doorE;
            dawn = dawnQuad;
            closedLeafWidth = halfWidth;
            slide = slideMetres;
            westClosed = doorW != null ? doorW.position : Vector3.zero;
            eastClosed = doorE != null ? doorE.position : Vector3.zero;
        }

        private void LateUpdate()
        {
            if (westLeaf == null)
            {
                return;
            }
            float t = Mathf.InverseLerp(closedLeafWidth, openLeafWidth, westLeaf.localScale.x);
            // Ease so the leaves start heavy and settle into the frame.
            float e = t * t * (3f - 2f * t);
            if (westDoor != null) westDoor.position = westClosed + Vector3.left * (slide * e);
            if (eastDoor != null) eastDoor.position = eastClosed + Vector3.right * (slide * e);
            if (dawn != null) dawn.enabled = t > 0.001f;
        }
    }
}
