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

        public IRoomRulesAdapter Adapter => adapter;
        public RoomSnapshot Snapshot => adapter?.Current;

        /// <summary>(previous, current). Previous is null on the first broadcast after Install.</summary>
        public event Action<RoomSnapshot, RoomSnapshot> SnapshotChanged;

        /// <summary>True when no registered mover is mid-transition.</summary>
        public bool IsWorldSettled
        {
            get
            {
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

        private void HandleStateChanged(RoomSnapshot previous, RoomSnapshot current)
        {
            SnapshotChanged?.Invoke(previous, current);
        }
    }

    /// <summary>Anything that animates between logical states (elevator, raft, bridge, Bearer).</summary>
    public interface IWorldMover
    {
        bool InTransit { get; }
    }
}
