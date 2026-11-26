using Ankhu.Integration;
using UnityEngine;

namespace Ankhu.World
{
    public enum BindingTarget
    {
        Elevator,
        Shadow,
        Bridge
    }

    /// <summary>Violet binding thread shown while the matching snapshot binding is set.</summary>
    public sealed class BindingIndicator : SnapshotListener
    {
        [SerializeField] private BindingTarget target = BindingTarget.Elevator;
        [SerializeField] private Renderer[] renderers = new Renderer[0];

        [Tooltip("Thread colour. The dressed room sets violet or cyan.")]
        [SerializeField] private Color threadColor = GreyboxPalette.Violet;
        private bool shown;

        public BindingTarget Target => target;

        public void Configure(BindingTarget bindingTarget, Renderer[] threadRenderers)
        {
            target = bindingTarget;
            renderers = threadRenderers ?? new Renderer[0];
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            switch (target)
            {
                case BindingTarget.Elevator:
                    shown = next.ElevatorBound;
                    break;
                case BindingTarget.Shadow:
                    shown = next.ShadowBound;
                    break;
                default:
                    shown = next.BridgeBound;
                    break;
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                GreyboxRender.SetVisible(renderers[i], shown);
            }
        }

        private void Update()
        {
            if (!shown)
            {
                return;
            }
            float alpha = 0.65f + 0.35f * Mathf.Sin(Time.time * 4f);
            Color c = GreyboxPalette.WithAlpha(threadColor, alpha);
            for (int i = 0; i < renderers.Length; i++)
            {
                GreyboxRender.Tint(renderers[i], c);
            }
        }
    }
}
