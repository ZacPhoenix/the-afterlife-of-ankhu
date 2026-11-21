using Ankhu.Integration;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>
    /// Chain across one edge of the bridge gap. Solid unless the snapshot says the bridge is bound
    /// and the bridge has physically settled horizontal (<see cref="PendulumBridge.Walkable"/>).
    /// </summary>
    public sealed class ChainBarrier : SnapshotListener
    {
        [SerializeField] private PendulumBridge bridge;
        [SerializeField] private Collider2D blocker;
        [SerializeField] private GameObject raisedChain;
        [SerializeField] private GameObject droppedChain;

        private bool open;
        private bool initialised;

        public bool IsOpen => open;

        public void Configure(PendulumBridge watchedBridge)
        {
            bridge = watchedBridge;
        }

        public void ConfigureParts(Collider2D solidBlocker, GameObject raised, GameObject dropped)
        {
            blocker = solidBlocker;
            raisedChain = raised;
            droppedChain = dropped;
        }

        protected override void Start()
        {
            if (bridge == null)
            {
                bridge = Object.FindAnyObjectByType<PendulumBridge>();
                if (bridge == null)
                {
                    Debug.LogError("ChainBarrier has no PendulumBridge to watch. It stays closed.", this);
                }
            }
            base.Start();
            Evaluate();
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            Evaluate();
        }

        private void FixedUpdate()
        {
            Evaluate();
        }

        private void Evaluate()
        {
            bool shouldOpen = Current != null && Current.BridgeBound && bridge != null && bridge.Walkable;
            if (initialised && shouldOpen == open)
            {
                return;
            }
            initialised = true;
            open = shouldOpen;
            if (blocker != null)
            {
                blocker.enabled = !open;
            }
            GreyboxRender.SetActive(raisedChain, !open);
            GreyboxRender.SetActive(droppedChain, open);
        }
    }
}
