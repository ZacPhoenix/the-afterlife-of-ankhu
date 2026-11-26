using UnityEngine;

namespace Ankhu.Painted
{
    /// <summary>
    /// Restarts a one-shot Ankhu/PaintedFlipbook every time its card is shown, so a pose that shows the card plays
    /// the painted transition from frame one and holds the last frame. Loop cards need no component.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class FlipbookPlay : MonoBehaviour
    {
        private static readonly int StartTimeId = Shader.PropertyToID("_StartTime");
        private MaterialPropertyBlock block;

        private void OnEnable()
        {
            var r = GetComponent<Renderer>();
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetFloat(StartTimeId, Time.timeSinceLevelLoad);
            r.SetPropertyBlock(block);
        }
    }
}
