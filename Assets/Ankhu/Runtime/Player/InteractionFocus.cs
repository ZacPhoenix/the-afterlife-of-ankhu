using System.Collections.Generic;
using Ankhu.Core;
using Ankhu.Integration;
using Ankhu.Layout;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ankhu.Player
{
    /// <summary>
    /// Picks the nearest enabled <see cref="Interactable"/> whose trigger overlaps Ankhu and shows a
    /// prompt above his head. E sends the interaction id to the rules adapter, Q shows the inspect text.
    /// Both are idle while the speech bubble owns the keyboard.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionFocus : MonoBehaviour
    {
        public const string StillMovingMessage = "The chamber is still moving.";
        private const float PromptHeightAboveHead = 0.55f;

        [SerializeField] private PlayerZoneTracker zoneTracker;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private Text promptText;

        private readonly List<Collider2D> overlaps = new List<Collider2D>(16);
        private ContactFilter2D filter;
        private Rigidbody2D body;
        private Interactable focus;

        public Interactable Focus => focus;

        public void Configure(PlayerZoneTracker tracker, Camera cameraForPrompt, RectTransform canvas, Text prompt)
        {
            zoneTracker = tracker;
            worldCamera = cameraForPrompt;
            canvasRect = canvas;
            promptText = prompt;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            filter = new ContactFilter2D();
            filter.useTriggers = true;
            filter.SetLayerMask(Physics2D.AllLayers);
        }

        private void OnDisable()
        {
            focus = null;
            if (promptText != null)
            {
                promptText.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            focus = FindFocus();
            if (focus == null)
            {
                return;
            }
            if (GameplayKeys.Pressed(Key.E))
            {
                Use(focus);
            }
            else if (GameplayKeys.Pressed(Key.Q))
            {
                Inspect(focus);
            }
        }

        private void LateUpdate()
        {
            if (promptText == null)
            {
                return;
            }
            bool show = focus != null && !InputGate.TextEntryActive;
            Vector2 local = Vector2.zero;
            if (show)
            {
                Vector3 anchor = transform.position + new Vector3(0f, BorrowedDawnLayout.AnkhuHeight + PromptHeightAboveHead, 0f);
                show = AnkhuUi.WorldToCanvas(worldCamera, canvasRect, anchor, out local);
            }
            if (promptText.gameObject.activeSelf != show)
            {
                promptText.gameObject.SetActive(show);
            }
            if (!show)
            {
                return;
            }
            string prompt = BuildPrompt(focus);
            if (promptText.text != prompt)
            {
                promptText.text = prompt;
            }
            promptText.rectTransform.anchoredPosition = local;
        }

        public static string BuildPrompt(Interactable target)
        {
            string name = LowerFirst(string.IsNullOrEmpty(target.DisplayName) ? "it" : target.DisplayName);
            string inspect = "Q  Inspect " + name;
            if (!target.HasUse)
            {
                return inspect;
            }
            string use = string.IsNullOrEmpty(target.UsePrompt) ? "Use " + name : target.UsePrompt;
            return "E  " + use + "\n" + inspect;
        }

        private static string LowerFirst(string value)
        {
            if (string.IsNullOrEmpty(value) || !char.IsUpper(value[0]))
            {
                return value;
            }
            // Keep acronyms and all caps names as written.
            if (value.Length > 1 && char.IsUpper(value[1]))
            {
                return value;
            }
            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }

        private Interactable FindFocus()
        {
            Vector2 feet = body != null ? body.position : (Vector2)transform.position;
            var center = new Vector2(feet.x, feet.y + BorrowedDawnLayout.AnkhuHeight * 0.5f);
            var size = new Vector2(BorrowedDawnLayout.AnkhuWidth + 0.1f, BorrowedDawnLayout.AnkhuHeight);
            int count = Physics2D.OverlapBox(center, size, 0f, filter, overlaps);

            Interactable best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider2D c = overlaps[i];
                if (c == null || !c.isTrigger)
                {
                    continue;
                }
                Interactable candidate = c.GetComponent<Interactable>();
                if (candidate == null || !candidate.isActiveAndEnabled)
                {
                    continue;
                }
                float d = Vector2.Distance(center, c.bounds.center);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = candidate;
                }
            }
            return best;
        }

        private void Use(Interactable target)
        {
            if (!target.HasUse)
            {
                Inspect(target);
                return;
            }
            RoomRulesHost host = RoomRulesHost.Instance;
            if (host == null || host.Adapter == null)
            {
                FeedbackBus.Publish(FeedbackSource.System, CommandOutcome.NotConnected, "No rules are connected to this room.");
                return;
            }
            if (!host.IsWorldSettled)
            {
                FeedbackBus.Publish(FeedbackSource.System, CommandOutcome.Rejected, StillMovingMessage);
                return;
            }
            PlayerContext context = zoneTracker != null ? zoneTracker.Context : new PlayerContext(ZoneIds.Floor, false, false);
            CommandResult result = host.Adapter.Interact(target.InteractionId, context);
            if (result == null)
            {
                return;
            }
            FeedbackBus.Publish(FeedbackSource.Interaction, result.Outcome, result.Message);
        }

        private static void Inspect(Interactable target)
        {
            string text = string.IsNullOrEmpty(target.InspectText) ? "Nothing more can be read from it." : target.InspectText;
            FeedbackBus.Publish(FeedbackSource.Inspect, CommandOutcome.NoChange, text);
        }
    }
}
