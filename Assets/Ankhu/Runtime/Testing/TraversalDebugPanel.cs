using System;
using Ankhu.Core;
using Ankhu.Integration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ankhu.Testing
{
    /// <summary>
    /// IMGUI state panel toggled with F1. With the <see cref="TraversalTestAdapter"/> installed,
    /// every snapshot field is editable. With any other adapter the snapshot is shown read-only.
    /// Undo and Reset go through the adapter interface in both cases.
    /// </summary>
    public sealed class TraversalDebugPanel : MonoBehaviour
    {
        private const int WindowId = 0x41_4E_4B;

        [SerializeField] private bool visible;
        [SerializeField] private Rect windowRect = new Rect(16f, 16f, 340f, 600f);

        private Vector2 scroll;

        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !InputGate.GameplayInputAllowed)
            {
                return;
            }
            if (keyboard.f1Key.wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }
            windowRect = GUILayout.Window(WindowId, windowRect, DrawWindow, "Borrowed Dawn state (F1)");
        }

        private void DrawWindow(int id)
        {
            RoomRulesHost host = RoomRulesHost.Instance;
            RoomSnapshot s = host != null ? host.Snapshot : null;
            if (host == null || s == null)
            {
                GUILayout.Label("No RoomRulesHost or no adapter installed yet.");
                GUI.DragWindow();
                return;
            }

            TraversalTestAdapter test = host.Adapter as TraversalTestAdapter;
            bool editable = test != null;
            GUILayout.Label(editable
                ? "Traversal test adapter. Every field is editable. No rules run."
                : "Rules adapter: " + host.Adapter.GetType().Name + ". Read-only view.");
            GUILayout.Label("World settled: " + (host.IsWorldSettled ? "yes" : "no"));

            scroll = GUILayout.BeginScrollView(scroll);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && editable;

            GUILayout.Label("Primary");
            Choice("Water", (int)s.Water, new[] { "East", "West" }, v => test.SetWater((WaterSide)v));
            Choice("Mirror", (int)s.Mirror, new[] { "Raft", "Carried", "Socket" }, v => test.SetMirror((MirrorLocation)v));
            Toggle("Elevator bound", s.ElevatorBound, v => test.SetElevatorBound(v));
            Toggle("Shadow bound", s.ShadowBound, v => test.SetShadowBound(v));
            Toggle("Bridge bound", s.BridgeBound, v => test.SetBridgeBound(v));
            Counter("Bindings used", s.BindingsUsed, v => test.SetBindingsUsed(v));
            Counter("Binding capacity", s.BindingCapacity, v => test.SetBindingCapacity(v));
            Toggle("Bearer awake", s.BearerAwake, v => test.SetBearerAwake(v));
            Toggle("Inscription read", s.InscriptionRead, v => test.SetInscriptionRead(v));
            Toggle("Bell repaired", s.BellRepaired, v => test.SetBellRepaired(v));
            Toggle("Bell struck", s.BellStruck, v => test.SetBellStruck(v));
            Toggle("Bell echoing", s.BellEchoing, v => test.SetBellEchoing(v));
            Toggle("Ladder lowered", s.LadderLowered, v => test.SetLadderLowered(v));
            Toggle("Gate open", s.GateOpen, v => test.SetGateOpen(v));

            GUILayout.Space(6f);
            GUILayout.Label("Derived (set by the rules side)");
            Toggle("Elevator high", s.ElevatorHigh, v => test.SetElevatorHigh(v));
            Toggle("Raft high", s.RaftHigh, v => test.SetRaftHigh(v));
            Toggle("Light screen raised", s.LightScreenRaised, v => test.SetLightScreenRaised(v));
            Toggle("Shadow present", s.ShadowPresent, v => test.SetShadowPresent(v));
            Toggle("Sun reaches gate", s.SunReachesGate, v => test.SetSunReachesGate(v));

            GUI.enabled = wasEnabled;
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Undo"))
            {
                CommandResult result = host.Adapter.Undo();
                FeedbackBus.Publish(FeedbackSource.System, result.Outcome, result.Message);
            }
            if (GUILayout.Button("Reset"))
            {
                host.Adapter.Reset();
                FeedbackBus.Publish(FeedbackSource.System, CommandOutcome.Success, "Room reset.");
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private static void Toggle(string label, bool value, Action<bool> set)
        {
            bool next = GUILayout.Toggle(value, label);
            if (next != value && GUI.enabled)
            {
                set(next);
            }
        }

        private static void Choice(string label, int value, string[] options, Action<int> set)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110f));
            int next = GUILayout.Toolbar(value, options);
            GUILayout.EndHorizontal();
            if (next != value && GUI.enabled)
            {
                set(next);
            }
        }

        private static void Counter(string label, int value, Action<int> set)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ": " + value, GUILayout.Width(170f));
            bool minus = GUILayout.Button("-", GUILayout.Width(28f));
            bool plus = GUILayout.Button("+", GUILayout.Width(28f));
            GUILayout.EndHorizontal();
            if (!GUI.enabled)
            {
                return;
            }
            if (minus && value > 0)
            {
                set(value - 1);
            }
            else if (plus)
            {
                set(value + 1);
            }
        }
    }
}
