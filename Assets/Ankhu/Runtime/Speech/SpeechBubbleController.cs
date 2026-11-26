using System;
using System.Collections.Generic;
using Ankhu.Core;
using Ankhu.Integration;
using Ankhu.Layout;
using Ankhu.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Ankhu.Speech
{
    /// <summary>
    /// The speech bubble beside Ankhu's head. Enter opens it and takes the keyboard through
    /// <see cref="InputGate"/>. Typed characters arrive from Keyboard.onTextInput. Enter submits the
    /// utterance to the rules adapter, Escape cancels, Backspace deletes with key repeat, Up and Down
    /// browse the last ten utterances. The reply shows under the bubble for a few seconds.
    /// This component lives on the always active canvas root and shows or hides its child rects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpeechBubbleController : MonoBehaviour
    {
        public const float ReplySeconds = 3f;
        public const float HeadAnchorAbove = 0.3f;
        private const float BackspaceRepeatDelay = 0.42f;
        private const float BackspaceRepeatInterval = 0.045f;
        private const float CaretBlinkSeconds = 0.53f;
        private const float ScreenMargin = 16f;
        private const float PanelPadding = 18f;
        private const float MinPanelWidth = 96f;
        private const float ReplyWidth = 640f;
        private static readonly Vector2 HeadOffset = new Vector2(28f, 8f);
        private static readonly Color TypingColor = new Color(0.17f, 0.11f, 0.05f);
        private static readonly Color SpokenColor = new Color(0.17f, 0.11f, 0.05f, 0.6f);

        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform speaker;
        [SerializeField] private PlayerZoneTracker zoneTracker;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform bubbleRoot;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text inputText;
        [SerializeField] private Text replyText;

        private readonly SpeechBuffer buffer = new SpeechBuffer(SpeechBuffer.DefaultMaxLength);
        private readonly SpeechHistory history = new SpeechHistory(SpeechHistory.DefaultCapacity);
        private readonly List<KeyControl> heldAtOpen = new List<KeyControl>();

        private Keyboard subscribedKeyboard;
        private bool isOpen;
        private int openedFrame = -1;
        private float backspaceRepeatAt;
        private float caretTimer;
        private bool caretVisible = true;
        private float replyTimer;
        private string spokenText = string.Empty;
        private bool layoutDirty = true;

        private bool suppressed;

        public bool IsOpen => isOpen;

        /// <summary>The camera the bubble projects Ankhu's head through.</summary>
        public Camera WorldCamera => worldCamera;
        /// <summary>Ankhu's root transform (the bubble anchors at <see cref="BorrowedDawnLayout.AnkhuHeight"/> above it).</summary>
        public Transform Speaker => speaker;
        public PlayerZoneTracker ZoneTracker => zoneTracker;

        /// <summary>
        /// True while another front end owns typing (the painted bubble in Ankhu.Spellcast). Enter then does not open
        /// this flat bubble, but <see cref="SpeakExternal"/> still runs the same speak path, so voice, animation and
        /// history keep working through <see cref="Spoken"/>.
        /// </summary>
        public bool Suppressed
        {
            get => suppressed;
            set
            {
                suppressed = value;
                if (!value) return;
                if (isOpen) Close();
                replyTimer = 0f;
                layoutDirty = true;
            }
        }

        /// <summary>True while the bubble or its reply is on screen. The interaction prompt hides then so the two never overlap.</summary>
        public static bool AnyVisible { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyVisible = false;
        }
        public SpeechBuffer Buffer => buffer;
        public SpeechHistory History => history;

        /// <summary>Raised after an utterance was sent to the adapter, with the text and the result.</summary>
        public event Action<string, CommandResult> Spoken;

        public void Configure(Camera cameraForBubble, Transform speakerTransform, PlayerZoneTracker tracker,
            RectTransform canvas, RectTransform root, RectTransform panelRect, Text input, Text reply)
        {
            worldCamera = cameraForBubble;
            speaker = speakerTransform;
            zoneTracker = tracker;
            canvasRect = canvas;
            bubbleRoot = root;
            panel = panelRect;
            inputText = input;
            replyText = reply;
            if (bubbleRoot != null)
            {
                bubbleRoot.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (isOpen)
            {
                Close();
            }
            replyTimer = 0f;
            if (bubbleRoot != null)
            {
                bubbleRoot.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (isOpen)
            {
                UpdateOpen(keyboard);
            }
            else if (!suppressed && keyboard != null && InputGate.GameplayInputAllowed &&
                     (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                Open(keyboard);
            }

            if (replyTimer > 0f)
            {
                replyTimer -= Time.unscaledDeltaTime;
                if (replyTimer <= 0f)
                {
                    replyTimer = 0f;
                    layoutDirty = true;
                }
            }

            if (isOpen)
            {
                caretTimer += Time.unscaledDeltaTime;
                if (caretTimer >= CaretBlinkSeconds)
                {
                    caretTimer -= CaretBlinkSeconds;
                    caretVisible = !caretVisible;
                    layoutDirty = true;
                }
            }
        }

        private void LateUpdate()
        {
            if (bubbleRoot == null)
            {
                return;
            }
            bool panelVisible = isOpen || (replyTimer > 0f && spokenText.Length > 0);
            bool replyVisible = replyTimer > 0f;
            bool visible = panelVisible || replyVisible;
            AnyVisible = visible;
            if (bubbleRoot.gameObject.activeSelf != visible)
            {
                bubbleRoot.gameObject.SetActive(visible);
                layoutDirty = true;
            }
            if (!visible)
            {
                return;
            }
            if (layoutDirty)
            {
                RebuildLayout(panelVisible, replyVisible);
                layoutDirty = false;
            }
            Position();
        }

        // ---------------------------------------------------------------- open, type, close

        private void Open(Keyboard keyboard)
        {
            isOpen = true;
            openedFrame = Time.frameCount;
            buffer.Clear();
            history.ResetCursor();
            spokenText = string.Empty;
            replyTimer = 0f;
            caretVisible = true;
            caretTimer = 0f;
            layoutDirty = true;

            // Keys already held must not type themselves through OS key repeat.
            heldAtOpen.Clear();
            foreach (KeyControl key in keyboard.allKeys)
            {
                if (key != null && key.isPressed)
                {
                    heldAtOpen.Add(key);
                }
            }

            InputGate.BeginTextEntry();
            subscribedKeyboard = keyboard;
            subscribedKeyboard.onTextInput += HandleTextInput;
        }

        private void Close()
        {
            isOpen = false;
            heldAtOpen.Clear();
            if (subscribedKeyboard != null)
            {
                subscribedKeyboard.onTextInput -= HandleTextInput;
                subscribedKeyboard = null;
            }
            buffer.Clear();
            history.ResetCursor();
            layoutDirty = true;
            InputGate.EndTextEntry();
        }

        private void HandleTextInput(char c)
        {
            if (!isOpen || Time.frameCount == openedFrame)
            {
                return;
            }
            if (IsHeldSinceOpen(c))
            {
                return;
            }
            if (buffer.Append(c))
            {
                history.ResetCursor();
                ShowCaret();
            }
        }

        private bool IsHeldSinceOpen(char c)
        {
            for (int i = heldAtOpen.Count - 1; i >= 0; i--)
            {
                KeyControl key = heldAtOpen[i];
                if (!key.isPressed)
                {
                    heldAtOpen.RemoveAt(i);
                    continue;
                }
                string label = key.displayName;
                if (!string.IsNullOrEmpty(label) && label.Length == 1 && char.ToLowerInvariant(label[0]) == char.ToLowerInvariant(c))
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateOpen(Keyboard keyboard)
        {
            if (keyboard == null || keyboard != subscribedKeyboard)
            {
                Cancel();
                return;
            }
            if (Time.frameCount == openedFrame)
            {
                // The Enter that opened the bubble must not also submit.
                return;
            }
            for (int i = heldAtOpen.Count - 1; i >= 0; i--)
            {
                if (!heldAtOpen[i].isPressed)
                {
                    heldAtOpen.RemoveAt(i);
                }
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cancel();
                return;
            }
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                Submit();
                return;
            }

            float now = Time.unscaledTime;
            KeyControl backspace = keyboard.backspaceKey;
            if (backspace.wasPressedThisFrame)
            {
                if (buffer.Backspace())
                {
                    history.ResetCursor();
                }
                backspaceRepeatAt = now + BackspaceRepeatDelay;
                ShowCaret();
            }
            else if (backspace.isPressed && now >= backspaceRepeatAt)
            {
                buffer.Backspace();
                backspaceRepeatAt = now + BackspaceRepeatInterval;
                ShowCaret();
            }

            if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                string recalled = history.Previous(buffer.Text);
                if (recalled != null)
                {
                    buffer.SetText(recalled);
                    ShowCaret();
                }
            }
            else if (keyboard.downArrowKey.wasPressedThisFrame)
            {
                string recalled = history.Next();
                if (recalled != null)
                {
                    buffer.SetText(recalled);
                    ShowCaret();
                }
            }
        }

        private void ShowCaret()
        {
            caretVisible = true;
            caretTimer = 0f;
            layoutDirty = true;
        }

        /// <summary>Closes the bubble and discards the text.</summary>
        public void Cancel()
        {
            if (isOpen)
            {
                Close();
            }
        }

        private void Submit()
        {
            string utterance = buffer.Text.Trim();
            if (utterance.Length == 0)
            {
                Close();
                return;
            }

            RoomRulesHost host = RoomRulesHost.Instance;
            if (host == null || host.Adapter == null)
            {
                const string offline = "No rules are connected to this room.";
                FeedbackBus.Publish(FeedbackSource.System, CommandOutcome.NotConnected, offline);
                ShowReply(utterance, offline, CommandOutcome.NotConnected);
                Close();
                return;
            }

            if (!host.IsWorldSettled)
            {
                // Keep the bubble open with the text intact so the player can send it once things settle.
                FeedbackBus.Publish(FeedbackSource.System, CommandOutcome.Rejected, InteractionFocus.StillMovingMessage);
                spokenText = string.Empty;
                replyTimer = ReplySeconds;
                SetReply(InteractionFocus.StillMovingMessage, CommandOutcome.Rejected);
                layoutDirty = true;
                return;
            }

            CommandResult result = SpeakCore(utterance, true);
            ShowReply(utterance, result.Message, result.Outcome);
            Close();
            Spoken?.Invoke(utterance, result);
        }

        /// <summary>
        /// Speaks for another front end: pushes history, asks the rules, raises <see cref="Spoken"/> (voice and
        /// animation) and returns the result. Feedback is published only when <paramref name="publishFeedback"/>
        /// is true, so a front end that plays an effect first can publish when the effect lands. Returns null when no
        /// rules are connected. The caller checks <see cref="RoomRulesHost.IsWorldSettled"/> first.
        /// </summary>
        public CommandResult SpeakExternal(string utterance, bool publishFeedback)
        {
            utterance = (utterance ?? string.Empty).Trim();
            RoomRulesHost host = RoomRulesHost.Instance;
            if (utterance.Length == 0 || host == null || host.Adapter == null)
            {
                return null;
            }
            CommandResult result = SpeakCore(utterance, publishFeedback);
            Spoken?.Invoke(utterance, result);
            return result;
        }

        private CommandResult SpeakCore(string utterance, bool publishFeedback)
        {
            RoomRulesHost host = RoomRulesHost.Instance;
            history.Push(utterance);
            PlayerContext context = zoneTracker != null ? zoneTracker.Context : new PlayerContext(ZoneIds.Floor, false, false);
            CommandResult result;
            try
            {
                result = host.Adapter.Speak(utterance, context);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                result = new CommandResult(CommandOutcome.NotConnected, "The words falter. The rules could not answer.");
            }
            if (result == null)
            {
                result = new CommandResult(CommandOutcome.NotConnected, "The words fall into silence.");
            }
            if (publishFeedback)
            {
                FeedbackBus.Publish(FeedbackSource.Speech, result.Outcome, result.Message);
            }
            return result;
        }

        private void ShowReply(string utterance, string message, CommandOutcome outcome)
        {
            spokenText = utterance;
            replyTimer = ReplySeconds;
            SetReply(message, outcome);
            layoutDirty = true;
        }

        private void SetReply(string message, CommandOutcome outcome)
        {
            if (replyText == null)
            {
                return;
            }
            replyText.text = message ?? string.Empty;
            replyText.color = FeedbackPalette.For(outcome);
        }

        // ---------------------------------------------------------------- layout

        private void RebuildLayout(bool panelVisible, bool replyVisible)
        {
            if (panel != null)
            {
                if (panel.gameObject.activeSelf != panelVisible)
                {
                    panel.gameObject.SetActive(panelVisible);
                }
                if (panelVisible && inputText != null)
                {
                    string content = isOpen ? buffer.Text : spokenText;
                    inputText.text = isOpen && caretVisible ? content + "|" : content;
                    inputText.color = isOpen ? TypingColor : SpokenColor;
                    // Measure with the caret always present so blinking never changes the width.
                    float width = AnkhuUi.MeasureWidth(inputText, content + "|") + PanelPadding * 2f;
                    float maxWidth = canvasRect != null ? canvasRect.rect.width - ScreenMargin * 2f : 1800f;
                    panel.sizeDelta = new Vector2(Mathf.Clamp(width, MinPanelWidth, maxWidth), inputText.fontSize * 1.75f);
                }
            }
            if (replyText != null)
            {
                bool show = replyVisible && replyText.text.Length > 0;
                if (replyText.gameObject.activeSelf != show)
                {
                    replyText.gameObject.SetActive(show);
                }
                if (show)
                {
                    RectTransform r = replyText.rectTransform;
                    r.sizeDelta = new Vector2(ReplyWidth, r.sizeDelta.y);
                    r.sizeDelta = new Vector2(ReplyWidth, Mathf.Ceil(replyText.preferredHeight));
                }
            }
        }

        private void Position()
        {
            if (speaker == null || canvasRect == null)
            {
                return;
            }
            Vector3 head = speaker.position + new Vector3(0f, BorrowedDawnLayout.AnkhuHeight + HeadAnchorAbove, 0f);
            if (!AnkhuUi.WorldToCanvas(worldCamera, canvasRect, head, out Vector2 local))
            {
                return;
            }
            Vector2 target = local + HeadOffset;

            float panelWidth = panel != null && panel.gameObject.activeSelf ? panel.sizeDelta.x : 0f;
            float panelHeight = panel != null && panel.gameObject.activeSelf ? panel.sizeDelta.y : 0f;
            float replyHeight = 0f;
            float replyWidth = 0f;
            if (replyText != null && replyText.gameObject.activeSelf)
            {
                RectTransform r = replyText.rectTransform;
                replyHeight = r.sizeDelta.y - r.anchoredPosition.y;
                replyWidth = r.sizeDelta.x + r.anchoredPosition.x;
            }
            float width = Mathf.Max(panelWidth, replyWidth);

            Rect area = canvasRect.rect;
            float minX = area.xMin + ScreenMargin;
            float maxX = Mathf.Max(minX, area.xMax - ScreenMargin - width);
            float minY = area.yMin + ScreenMargin + replyHeight;
            float maxY = Mathf.Max(minY, area.yMax - ScreenMargin - panelHeight);
            target.x = Mathf.Clamp(target.x, minX, maxX);
            target.y = Mathf.Clamp(target.y, minY, maxY);
            bubbleRoot.anchoredPosition = target;
        }
    }
}
