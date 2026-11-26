using System;
using System.Collections.Generic;
using Ankhu.Integration;
using UnityEngine;

namespace Ankhu.Core
{
    /// <summary>
    /// Scene-level owner of the rules adapter. Presentation components subscribe to
    /// <see cref="SnapshotChanged"/> and read <see cref="Snapshot"/>. They never hold puzzle logic.
    ///
    /// Adapter selection: call <see cref="Install"/> before Start (for example from a bootstrap
    /// component that wraps the rules library). If nothing is installed by the time Start runs,
    /// the host installs the <see cref="Ankhu.Testing.TraversalTestAdapter"/>, which accepts no
    /// spells and lets a designer toggle state by hand to test traversal.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class RoomRulesHost : MonoBehaviour
    {
        public static RoomRulesHost Instance { get; private set; }

        private readonly List<IWorldMover> movers = new List<IWorldMover>();
        private IRoomRulesAdapter adapter;

        private int holdDepth;
        private bool heldDirty;
        private RoomSnapshot heldPrevious;
        private RoomSnapshot presented;

        public IRoomRulesAdapter Adapter => adapter;

        /// <summary>
        /// The state the world is showing. While snapshots are held (<see cref="HoldSnapshots"/>) this is the state
        /// from before the hold, so HUD and views agree with what is on screen until the spell lands.
        /// </summary>
        public RoomSnapshot Snapshot => holdDepth > 0 && presented != null ? presented : adapter?.Current;

        /// <summary>True while a spell effect holds back world changes.</summary>
        public bool IsHoldingSnapshots => holdDepth > 0;

        /// <summary>(previous, current). Previous is null on the first broadcast after Install.</summary>
        public event Action<RoomSnapshot, RoomSnapshot> SnapshotChanged;

        /// <summary>True when no registered mover is mid-transition.</summary>
        public bool IsWorldSettled
        {
            get
            {
                if (holdDepth > 0)
                {
                    return false;
                }
                for (int i = 0; i < movers.Count; i++)
                {
                    if (movers[i].InTransit)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>Clears the static instance when entering play mode with domain reload disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Two RoomRulesHost components are active. Only one room can run at a time.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (adapter == null)
            {
                Install(new Ankhu.Testing.TraversalTestAdapter());
            }
        }

        private void OnDestroy()
        {
            if (adapter != null)
            {
                adapter.StateChanged -= HandleStateChanged;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Install(IRoomRulesAdapter newAdapter)
        {
            if (newAdapter == null)
            {
                throw new ArgumentNullException(nameof(newAdapter));
            }
            if (adapter != null)
            {
                adapter.StateChanged -= HandleStateChanged;
            }
            holdDepth = 0;
            heldDirty = false;
            heldPrevious = null;
            presented = null;
            adapter = newAdapter;
            adapter.StateChanged += HandleStateChanged;
            SnapshotChanged?.Invoke(null, adapter.Current);
        }

        public void RegisterMover(IWorldMover mover)
        {
            if (mover != null && !movers.Contains(mover))
            {
                movers.Add(mover);
            }
        }

        public void UnregisterMover(IWorldMover mover)
        {
            movers.Remove(mover);
        }

        /// <summary>
        /// Holds back <see cref="SnapshotChanged"/> so a spell can be seen to land before the world answers. Changes
        /// during the hold are merged into one broadcast (first previous, latest current) when the last hold is
        /// released. Holds nest. Every call must be matched by <see cref="ReleaseSnapshots"/>.
        /// </summary>
        public void HoldSnapshots()
        {
            if (holdDepth == 0)
            {
                presented = adapter?.Current;
                heldDirty = false;
                heldPrevious = null;
            }
            holdDepth++;
        }

        /// <summary>Releases one hold. The last release broadcasts any held change.</summary>
        public void ReleaseSnapshots()
        {
            if (holdDepth == 0)
            {
                return;
            }
            holdDepth--;
            if (holdDepth > 0)
            {
                return;
            }
            bool dirty = heldDirty;
            RoomSnapshot previous = heldPrevious;
            heldDirty = false;
            heldPrevious = null;
            presented = null;
            if (dirty && adapter != null)
            {
                SnapshotChanged?.Invoke(previous, adapter.Current);
            }
        }

        private void HandleStateChanged(RoomSnapshot previous, RoomSnapshot current)
        {
            if (holdDepth > 0)
            {
                if (!heldDirty)
                {
                    heldDirty = true;
                    heldPrevious = previous;
                }
                return;
            }
            SnapshotChanged?.Invoke(previous, current);
        }
    }

    /// <summary>Anything that animates between logical states (elevator, raft, bridge, Bearer).</summary>
    public interface IWorldMover
    {
        bool InTransit { get; }
    }
}
