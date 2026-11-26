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
        private bool focusUseAvailable;

        public Interactable Focus => focus;

        /// <summary>Raised after Ankhu uses a target and the rules answered. Presentation only (animation).</summary>
        public event System.Action<Interactable, CommandResult> Used;

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
            if (slipGroup != null) slipGroup.alpha = 0f;
            if (glintGroup != null) glintGroup.alpha = 0f;
        }

        private void Update()
        {
            focus = FindFocus();
            focusUseAvailable = focus != null && IsUseAvailable(focus);
            if (focus == null)
            {
                return;
            }
            if (GameplayKeys.Pressed(Key.E))
            {
                if (focusUseAvailable)
                {
                    Use(focus);
                }
                else
                {
                    Inspect(focus);
                }
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
            bool show = focus != null && !InputGate.TextEntryActive && !Ankhu.Speech.SpeechBubbleController.AnyVisible;
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
            UpdatePaintedPrompt(show, local);
            if (!show)
            {
                return;
            }
            string prompt = BuildPrompt(focus, focusUseAvailable);
            if (promptText.text != prompt)
            {
                promptText.text = prompt;
            }
            promptText.rectTransform.anchoredPosition = local;
        }

        // ------------------------------------------------------------------ painted glint first
        // Zac 2026-11-25, prompts pick B: a soft gold glint marks an interactable when Ankhu is near it, and only
        // when he stands at it does a papyrus slip with the keycaps and verbs appear. The plain promptText stays
        // as the data source (BuildPrompt, tests) but is drawn transparent once the slip is built.
        private const float NearReach = 2.4f;
        private const float GlintSize = 48f;
        private RectTransform slip;
        private RectTransform slipRow;
        private CanvasGroup slipGroup;
        private string slipShown;
        private RectTransform glint;
        private CanvasGroup glintGroup;
        private Image glintStar;
        private readonly List<Collider2D> nearOverlaps = new List<Collider2D>(16);

        private void BuildPaintedPrompt()
        {
            if (slip != null || canvasRect == null || !PapyrusKit.Painted) return;
            glint = PapyrusKit.Rect("Interact Glint", canvasRect);
            glint.anchorMin = glint.anchorMax = glint.pivot = new Vector2(0.5f, 0.5f);
            glint.sizeDelta = new Vector2(GlintSize, GlintSize);
            glintGroup = glint.gameObject.AddComponent<CanvasGroup>();
            glintGroup.alpha = 0f;
            Image halo = PapyrusKit.Image("Halo", glint, PapyrusKit.Plain("fx_mote"), new Color(1f, 0.78f, 0.36f, 0.35f));
            PapyrusKit.Stretch(halo.rectTransform, -GlintSize * 0.25f);
            Sprite star = PapyrusKit.Plain("fx_flare");
            glintStar = PapyrusKit.Image("Star", glint, star != null ? star : PapyrusKit.Plain("fx_spark"), PapyrusKit.GoldBright);
            PapyrusKit.Stretch(glintStar.rectTransform);

            slip = PapyrusKit.Rect("Interact Slip", canvasRect);
            slip.anchorMin = slip.anchorMax = new Vector2(0.5f, 0.5f);
            slip.pivot = new Vector2(0.5f, 0f);
            slipGroup = slip.gameObject.AddComponent<CanvasGroup>();
            slipGroup.alpha = 0f;
            RectTransform sheet = PapyrusKit.Sheet("Slip", slip, 14f);
            PapyrusKit.Stretch(sheet);
            slipRow = PapyrusKit.Rect("Row", slip);
            slipRow.anchorMin = slipRow.anchorMax = slipRow.pivot = new Vector2(0.5f, 0.5f);
            slipRow.anchoredPosition = new Vector2(0f, 1f);
        }

        private void UpdatePaintedPrompt(bool show, Vector2 local)
        {
            BuildPaintedPrompt();
            if (slip == null) return;
            Color c = promptText.color;
            if (c.a > 0f) promptText.color = new Color(c.r, c.g, c.b, 0f);
            float dt = Time.unscaledDeltaTime;
            if (show)
            {
                var pairs = new List<string>(4);
                string name = LowerFirst(string.IsNullOrEmpty(focus.DisplayName) ? "it" : focus.DisplayName);
                if (focus.HasUse && focusUseAvailable)
                {
                    pairs.Add("E");
                    pairs.Add(string.IsNullOrEmpty(focus.UsePrompt) ? "Use " + name : focus.UsePrompt);
                }
                pairs.Add("Q");
                pairs.Add("Inspect " + name);
                string key = string.Join("|", pairs);
                if (key != slipShown)
                {
                    slipShown = key;
                    float w = PapyrusKit.KeyRow(slipRow, pairs, 30f, 22, 22f);
                    slip.sizeDelta = new Vector2(w + 40f, 54f);
                }
                slip.anchoredPosition = local;
            }
            slipGroup.alpha = Mathf.MoveTowards(slipGroup.alpha, show ? 1f : 0f, dt * 6f);
            float k = slipGroup.alpha;
            slip.localScale = new Vector3(1f, Mathf.Lerp(0.6f, 1f, 1f - (1f - k) * (1f - k)), 1f);

            // The glint: the nearest interactable within reach that Ankhu is not already standing at.
            bool glintOn = false;
            if (!show && !InputGate.TextEntryActive && !Ankhu.Speech.SpeechBubbleController.AnyVisible)
            {
                Vector2 feet = body != null ? body.position : (Vector2)transform.position;
                var center = new Vector2(feet.x, feet.y + BorrowedDawnLayout.AnkhuHeight * 0.5f);
                var size = new Vector2(BorrowedDawnLayout.AnkhuWidth + NearReach * 2f, BorrowedDawnLayout.AnkhuHeight + NearReach);
                int count = Physics2D.OverlapBox(center, size, 0f, filter, nearOverlaps);
                float best = float.PositiveInfinity;
                Vector3 mark = Vector3.zero;
                for (int i = 0; i < count; i++)
                {
                    Collider2D col = nearOverlaps[i];
                    if (col == null || !col.isTrigger) continue;
                    Interactable it = col.GetComponent<Interactable>();
                    if (it == null || !it.isActiveAndEnabled) continue;
                    float d = Vector2.Distance(center, col.bounds.center);
                    if (d < best)
                    {
                        best = d;
                        Bounds b = col.bounds;
                        mark = new Vector3(b.center.x, Mathf.Min(b.max.y, b.min.y + BorrowedDawnLayout.AnkhuHeight), transform.position.z);
                    }
                }
                if (best < float.PositiveInfinity && AnkhuUi.WorldToCanvas(worldCamera, canvasRect, mark, out Vector2 g))
                {
                    glint.anchoredPosition = g;
                    glintOn = true;
                }
            }
            glintGroup.alpha = Mathf.MoveTowards(glintGroup.alpha, glintOn ? 1f : 0f, dt * 3f);
            if (glintGroup.alpha > 0f)
            {
                float t = Time.unscaledTime;
                glintStar.rectTransform.localScale = Vector3.one * (0.82f + 0.18f * Mathf.Sin(t * 2.6f));
                glintStar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 18f);
            }
        }

        /// <summary>
        /// Asks the rules, without changing anything, whether using the target would succeed from
        /// here. Only a definite refusal hides the prompt. Adapters that cannot evaluate rules keep it.
        /// </summary>
        private bool IsUseAvailable(Interactable target)
        {
            if (!target.HasUse)
            {
                return false;
            }
            RoomRulesHost host = RoomRulesHost.Instance;
            if (host == null || host.Adapter == null)
            {
                return true;
            }
            PlayerContext context = zoneTracker != null ? zoneTracker.Context : new PlayerContext(ZoneIds.Floor, false, false);
            CommandResult preview = host.Adapter.Preview(target.InteractionId, context);
            return preview == null ||
                   (preview.Outcome != CommandOutcome.Rejected &&
                    preview.Outcome != CommandOutcome.InvalidTarget &&
                    preview.Outcome != CommandOutcome.NoChange);
        }

        public static string BuildPrompt(Interactable target, bool useAvailable)
        {
            string name = LowerFirst(string.IsNullOrEmpty(target.DisplayName) ? "it" : target.DisplayName);
            string inspect = "Q  Inspect " + name;
            if (!target.HasUse || !useAvailable)
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
            Used?.Invoke(target, result);
            FeedbackBus.Publish(FeedbackSource.Interaction, result.Outcome, result.Message);
        }

        private static void Inspect(Interactable target)
        {
            string text = string.IsNullOrEmpty(target.InspectText) ? "Nothing more can be read from it." : target.InspectText;
            FeedbackBus.Publish(FeedbackSource.Inspect, CommandOutcome.NoChange, text);
        }
    }
}
