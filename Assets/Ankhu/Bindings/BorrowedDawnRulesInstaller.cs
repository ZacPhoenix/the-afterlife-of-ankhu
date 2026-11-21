using Ankhu.Core;
using Ankhu.Integration;
using Ankhu.Player;
using UnityEngine;
using L = Ankhu.Layout.BorrowedDawnLayout;

namespace Ankhu.Bindings
{
    /// <summary>
    /// Installs the real rules library into the scene's <see cref="RoomRulesHost"/> and keeps the
    /// rules location in step with where Ankhu physically walks.
    ///
    /// Walking: each time the zone tracker reports a new zone, the move is offered to the rules. If the
    /// rules refuse it (the physical layout should make that impossible, so it signals a layout bug or
    /// a frame of overlap), Ankhu is returned to the last position the rules accepted and the reason is
    /// shown.
    ///
    /// Undo, reset and load can change the rules location without Ankhu walking. When that happens,
    /// Ankhu is placed at that location's anchor once the room has finished moving.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class BorrowedDawnRulesInstaller : MonoBehaviour
    {
        private const float AnchorLiftX = (L.ShaftMinX + L.ShaftMaxX) * 0.5f;
        private const float AnchorGalleryX = (L.InscriptionMinX + L.InscriptionMaxX) * 0.5f;
        private const float AnchorLandingX = (L.LandingMinX + L.LandingMaxX) * 0.5f;
        private const float AnchorBridgeX = (L.GapMinX + L.GapMaxX) * 0.5f;
        private const float AnchorGateX = L.GateLedgeMinX + 1f;

        [Tooltip("Rules room id. Empty or borrowed-dawn plays the full room. Teaching rooms use their TeachingRooms id.")]
        [SerializeField] private string roomId = BorrowedDawnRooms.BorrowedDawnId;

        private BorrowedDawnRulesAdapter adapter;
        private RoomRulesHost host;
        private PlayerZoneTracker tracker;
        private PlayerController2D controller;
        private Vector2 lastAcceptedFeet;
        private bool pendingPlacement;
        private bool reportedLostAdapter;

        public BorrowedDawnRulesAdapter Adapter => adapter;

        public string RoomId => roomId;

        private void Awake()
        {
            host = GetComponent<RoomRulesHost>();
            if (host == null)
            {
                host = Object.FindAnyObjectByType<RoomRulesHost>();
            }
            if (host == null)
            {
                Debug.LogError("BorrowedDawnRulesInstaller needs a RoomRulesHost in the scene.", this);
                enabled = false;
                return;
            }

            try
            {
                adapter = new BorrowedDawnRulesAdapter(BorrowedDawnRooms.Resolve(roomId));
            }
            catch (System.Exception e)
            {
                Debug.LogError("The Borrowed Dawn rules library failed to start. The room falls back to the traversal test adapter.", this);
                Debug.LogException(e, this);
                adapter = null;
                enabled = false;
                return;
            }
            adapter.StateChanged += HandleStateChanged;
            host.Install(adapter);
        }

        private void Start()
        {
            tracker = Object.FindAnyObjectByType<PlayerZoneTracker>();
            controller = Object.FindAnyObjectByType<PlayerController2D>();
            if (tracker == null || controller == null)
            {
                Debug.LogError("BorrowedDawnRulesInstaller could not find the player rig.", this);
                enabled = false;
                return;
            }
            tracker.ZoneChanged += HandleZoneChanged;
            lastAcceptedFeet = controller.FeetPosition;
        }

        private void OnDestroy()
        {
            if (adapter != null)
            {
                adapter.StateChanged -= HandleStateChanged;
            }
            if (tracker != null)
            {
                tracker.ZoneChanged -= HandleZoneChanged;
            }
        }

        private void FixedUpdate()
        {
            if (controller == null || tracker == null)
            {
                return;
            }
            if (adapter == null)
            {
                ReportLostAdapter();
                return;
            }

            if (pendingPlacement)
            {
                if (host.IsWorldSettled)
                {
                    pendingPlacement = false;
                    controller.Teleport(AnchorFor(adapter.RulesZoneId));
                    lastAcceptedFeet = controller.FeetPosition;
                }
                return;
            }

            if (tracker.ZoneId == adapter.RulesZoneId && controller.IsGrounded)
            {
                lastAcceptedFeet = controller.FeetPosition;
            }
        }

        /// <summary>
        /// The adapter is a plain C# object, so a script recompile during play mode drops it while the
        /// component survives. Say so once instead of failing every physics step.
        /// </summary>
        private void ReportLostAdapter()
        {
            if (reportedLostAdapter)
            {
                return;
            }
            reportedLostAdapter = true;
            enabled = false;
            Debug.LogError("The Borrowed Dawn rules were lost, almost always because scripts recompiled during play mode. Stop play mode and press Play again.", this);
            FeedbackBus.Publish(FeedbackSource.System, CommandOutcome.NotConnected, "Scripts changed while playing. Stop and press Play again.");
        }

        private void HandleZoneChanged(string previousZone, string nextZone)
        {
            if (pendingPlacement || adapter == null || adapter.IsSolved)
            {
                // A solved teaching room accepts no more moves. Let Ankhu walk while Anpu speaks.
                return;
            }

            CommandResult refusal = adapter.SyncLocation(nextZone);
            if (refusal == null)
            {
                return;
            }

            controller.Teleport(lastAcceptedFeet);
            FeedbackBus.Publish(FeedbackSource.System, refusal.Outcome, refusal.Message);
        }

        private void HandleStateChanged(RoomSnapshot previous, RoomSnapshot current)
        {
            if (tracker != null && tracker.ZoneId != adapter.RulesZoneId)
            {
                pendingPlacement = true;
            }
        }

        private Vector2 AnchorFor(string zoneId)
        {
            switch (zoneId)
            {
                case ZoneIds.Lift:
                    return new Vector2(AnchorLiftX, adapter.Current.ElevatorHigh ? L.PlatformHighY : L.PlatformLowY);
                case ZoneIds.Gallery:
                    return new Vector2(AnchorGalleryX, L.UpperY);
                case ZoneIds.Landing:
                    return new Vector2(AnchorLandingX, L.UpperY);
                case ZoneIds.Bridge:
                    return new Vector2(AnchorBridgeX, L.UpperY);
                case ZoneIds.Gate:
                    return new Vector2(AnchorGateX, L.UpperY);
                default:
                    return L.Spawn;
            }
        }
    }
}
