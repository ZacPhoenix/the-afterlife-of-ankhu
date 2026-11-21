using System.Text;
using Ankhu.Core;
using Ankhu.Integration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ankhu.Player
{
    /// <summary>
    /// Top-left status strip read from the current <see cref="RoomSnapshot"/>, a key legend, and the
    /// room hotkeys: U undo, H hint, hold R for one second to reset. Hotkeys only act while gameplay
    /// input is allowed. The HUD shows state and never decides puzzle conditions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomHud : MonoBehaviour
    {
        public const float ResetHoldSeconds = 1f;
        public const string Legend = "A/D walk    W/S climb    E use    Q inspect    Enter speak    U undo    H hint    hold R reset    F2 camera    F3 skip lesson";

        [SerializeField] private Text statusText;
        [SerializeField] private Text legendText;
        [SerializeField] private PlayerController2D player;
        [SerializeField] private PlayerZoneTracker zoneTracker;

        private readonly StringBuilder builder = new StringBuilder(160);
        private RoomSnapshot shownSnapshot;
        private string shownZone;
        private bool shownConnected;
        private bool statusInitialised;
        private bool resetArmed;
        private float resetHeld;
        private int shownResetPercent = -1;

        public float ResetProgress => resetArmed ? Mathf.Clamp01(resetHeld / ResetHoldSeconds) : 0f;

        public void Configure(Text status, Text legend, PlayerController2D controller, PlayerZoneTracker tracker)
        {
            statusText = status;
            legendText = legend;
            player = controller;
            zoneTracker = tracker;
            if (legendText != null)
            {
                legendText.text = Legend;
            }
        }

        private void OnEnable()
        {
            statusInitialised = false;
            shownResetPercent = -1;
        }

        private void Update()
        {
            UpdateHotkeys();
            RefreshStatus();
            RefreshLegend();
        }

        private void UpdateHotkeys()
        {
            if (GameplayKeys.Pressed(Key.U))
            {
                Undo();
            }
            if (GameplayKeys.Pressed(Key.H))
            {
                Hint();
            }

            if (GameplayKeys.Pressed(Key.R))
            {
                resetArmed = true;
                resetHeld = 0f;
            }
            if (resetArmed)
            {
                if (!GameplayKeys.Held(Key.R))
                {
                    resetArmed = false;
                    resetHeld = 0f;
                }
                else
                {
                    resetHeld += Time.unscaledDeltaTime;
                    if (resetHeld >= ResetHoldSeconds)
                    {
                        resetArmed = false;
                        resetHeld = 0f;
                        ResetRoom();
                    }
                }
            }
        }

        private PlayerContext CurrentContext()
        {
            return zoneTracker != null ? zoneTracker.Context : new PlayerContext(ZoneIds.Floor, false, false);
        }

        private static IRoomRulesAdapter AdapterOrReport()
        {
            RoomRulesHost host = RoomRulesHost.Instance;
            if (host == null || host.Adapter == null)
            {
                FeedbackBus.Publish(FeedbackSource.System, CommandOutcome.NotConnected, "No rules are connected to this room.");
                return null;
            }
            return host.Adapter;
        }

        private static void Undo()
        {
            IRoomRulesAdapter adapter = AdapterOrReport();
            if (adapter == null)
            {
                return;
            }
            CommandResult result = adapter.Undo();
            if (result == null)
            {
                return;
            }
            string message = result.Message;
            if (string.IsNullOrEmpty(message))
            {
                message = result.Accepted ? "The last word is unsaid." : "There is nothing to undo.";
            }
            FeedbackBus.Publish(FeedbackSource.System, result.Outcome, message);
        }

        private void Hint()
        {
            IRoomRulesAdapter adapter = AdapterOrReport();
            if (adapter == null)
            {
                return;
            }
            string hint = adapter.Hint(CurrentContext());
            if (string.IsNullOrEmpty(hint))
            {
                hint = "The room offers no hint just now.";
            }
            FeedbackBus.Publish(FeedbackSource.Hint, CommandOutcome.NoChange, hint);
        }

        private void ResetRoom()
        {
            IRoomRulesAdapter adapter = AdapterOrReport();
            if (adapter == null)
            {
                return;
            }
            adapter.Reset();
            if (player != null)
            {
                player.Teleport(player.SpawnPoint);
            }
            FeedbackBus.Publish(FeedbackSource.System, CommandOutcome.Success, "The room returns to its first dawn.");
        }

        private void RefreshStatus()
        {
            if (statusText == null)
            {
                return;
            }
            RoomRulesHost host = RoomRulesHost.Instance;
            bool connected = host != null && host.Adapter != null;
            RoomSnapshot snapshot = connected ? host.Snapshot : null;
            string zone = zoneTracker != null ? zoneTracker.ZoneId : ZoneIds.Floor;
            if (statusInitialised && connected == shownConnected && ReferenceEquals(snapshot, shownSnapshot) && zone == shownZone)
            {
                return;
            }
            statusInitialised = true;
            shownConnected = connected;
            shownSnapshot = snapshot;
            shownZone = zone;
            statusText.text = BuildStatus(snapshot, zone);
        }

        /// <summary>Formats the status strip. Every value is read from the snapshot as is.</summary>
        public static string BuildStatus(RoomSnapshot snapshot, string zone)
        {
            if (snapshot == null)
            {
                return "Rules not connected    Zone  " + zone;
            }
            string shadow = snapshot.ShadowBound ? "bound" : (snapshot.ShadowPresent ? "present" : "absent");
            var sb = new StringBuilder(160);
            sb.Append("Sun  ").Append(snapshot.SunReachesGate ? "reaches the gate" : "short of the gate");
            sb.Append("    Shadow  ").Append(shadow);
            sb.Append("    Sound  ").Append(snapshot.BellEchoing ? "echoing" : "silent");
            sb.Append("    Name  ").Append(snapshot.InscriptionRead ? "known" : "unknown");
            sb.Append("    Bindings ").Append(snapshot.BindingsUsed).Append('/').Append(snapshot.BindingCapacity);
            sb.Append("    Zone  ").Append(zone);
            return sb.ToString();
        }

        private void RefreshLegend()
        {
            if (legendText == null)
            {
                return;
            }
            int percent = resetArmed ? Mathf.FloorToInt(ResetProgress * 100f) : -1;
            if (percent == shownResetPercent && !string.IsNullOrEmpty(legendText.text))
            {
                return;
            }
            shownResetPercent = percent;
            if (percent < 0)
            {
                legendText.text = Legend;
                return;
            }
            builder.Length = 0;
            builder.Append("Keep holding R to reset the room  ");
            int filled = Mathf.Clamp(percent / 10, 0, 10);
            builder.Append('[').Append('#', filled).Append('.', 10 - filled).Append(']');
            legendText.text = builder.ToString();
        }
    }
}
