using Ankhu.Core;
using Ankhu.Integration;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>
    /// Base class for presentation that follows the room state. It subscribes to
    /// <see cref="RoomRulesHost.SnapshotChanged"/>, applies the current snapshot immediately
    /// (instant, no easing) and then applies every later change with easing.
    /// Subclasses that need OnEnable, Start or OnDisable must override them and call base.
    /// </summary>
    public abstract class SnapshotListener : MonoBehaviour
    {
        private RoomRulesHost boundHost;

        /// <summary>The last snapshot this view applied, or null before the first one.</summary>
        protected RoomSnapshot Current { get; private set; }

        protected virtual void OnEnable()
        {
            TryBind();
        }

        protected virtual void Start()
        {
            TryBind();
        }

        protected virtual void OnDisable()
        {
            Unbind();
        }

        /// <summary>Called once per snapshot. <paramref name="previous"/> is null on the first apply.</summary>
        protected abstract void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant);

        protected virtual void OnBound(RoomRulesHost host)
        {
        }

        protected virtual void OnUnbound(RoomRulesHost host)
        {
        }

        private void TryBind()
        {
            if (boundHost != null)
            {
                return;
            }
            RoomRulesHost host = RoomRulesHost.Instance;
            if (host == null)
            {
                return;
            }
            boundHost = host;
            host.SnapshotChanged += HandleSnapshotChanged;
            OnBound(host);
            RoomSnapshot snapshot = host.Snapshot;
            if (snapshot != null)
            {
                Receive(null, snapshot, true);
            }
        }

        private void Unbind()
        {
            if (boundHost == null)
            {
                return;
            }
            boundHost.SnapshotChanged -= HandleSnapshotChanged;
            OnUnbound(boundHost);
            boundHost = null;
        }

        private void HandleSnapshotChanged(RoomSnapshot previous, RoomSnapshot next)
        {
            if (next == null)
            {
                return;
            }
            bool instant = previous == null || Current == null;
            Receive(previous, next, instant);
        }

        private void Receive(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            Current = next;
            Apply(previous, next, instant);
        }
    }

    /// <summary>A snapshot listener that animates and reports itself to the host as a world mover.</summary>
    public abstract class SnapshotMover : SnapshotListener, IWorldMover
    {
        public abstract bool InTransit { get; }

        protected override void OnBound(RoomRulesHost host)
        {
            host.RegisterMover(this);
        }

        protected override void OnUnbound(RoomRulesHost host)
        {
            host.UnregisterMover(this);
        }
    }

    /// <summary>
    /// Eases a normalised value (0 to 1) with smoothstep. A full 0 to 1 move takes
    /// <c>fullRangeSeconds</c>. A partial move takes the matching fraction, so reversing mid
    /// move stays smooth and never jumps.
    /// </summary>
    public sealed class FloatTween
    {
        private float from;
        private float to;
        private float elapsed;
        private float duration;

        public float Value { get; private set; }
        public float Target => to;
        public bool InTransit => duration > 0f;

        public void Snap(float value)
        {
            Value = value;
            from = value;
            to = value;
            elapsed = 0f;
            duration = 0f;
        }

        public void SetTarget(float target, float fullRangeSeconds)
        {
            if (Mathf.Approximately(target, to) && (InTransit || Mathf.Approximately(Value, target)))
            {
                return;
            }
            from = Value;
            to = target;
            elapsed = 0f;
            duration = Mathf.Max(0f, fullRangeSeconds) * Mathf.Abs(to - from);
            if (duration <= 0f)
            {
                Snap(target);
            }
        }

        /// <summary>Advances the tween. Returns true while it moved this step.</summary>
        public bool Step(float deltaTime)
        {
            if (!InTransit)
            {
                return false;
            }
            elapsed += deltaTime;
            if (elapsed >= duration)
            {
                Snap(to);
                return true;
            }
            Value = Mathf.Lerp(from, to, GreyboxRender.Ease(elapsed / duration));
            return true;
        }
    }
}
