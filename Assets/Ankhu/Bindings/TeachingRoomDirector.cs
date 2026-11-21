using System.Collections;
using System.Collections.Generic;
using Ankhu.BorrowedDawn;
using Ankhu.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Rules = Ankhu.BorrowedDawn;

namespace Ankhu.Bindings
{
    /// <summary>
    /// Runs one of Anpu's teaching rooms. Shows the room title and Anpu's intro lines as subtitles,
    /// shows a nudge the first time Ankhu hits each refusal the room has a nudge for, and when the
    /// room's goal holds, shows Anpu's closing line and loads the next scene in the sequence.
    /// Anpu's lines are provisional greybox dialogue from the rules library, not final script.
    /// F3 skips to the next room (development shortcut).
    /// </summary>
    [RequireComponent(typeof(BorrowedDawnRulesInstaller))]
    public sealed class TeachingRoomDirector : MonoBehaviour
    {
        public const float TitleSeconds = 3f;
        public const float SecondsPerIntroLine = 6f;
        public const float NudgeSeconds = 7f;
        public const float SolveSeconds = 5f;

        private static readonly Color PanelColor = new Color(0.05f, 0.04f, 0.03f, 0.78f);
        private static readonly Color TitleColor = new Color(1f, 0.82f, 0.36f);
        private static readonly Color SpeakerColor = new Color(0.78f, 0.66f, 1f);
        private static readonly Color LineColor = new Color(0.96f, 0.93f, 0.86f);

        /// <summary>
        /// Raised when Anpu starts a line: (dialogue key, text). Keys are
        /// dialogue.anpu.&lt;room id&gt;.intro.&lt;n&gt; (n from 1), dialogue.anpu.&lt;room id&gt;.nudge.&lt;Outcome&gt;
        /// and dialogue.anpu.&lt;room id&gt;.solve. Voice playback subscribes here.
        /// </summary>
        public static event System.Action<string, string> AnpuLineStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnpuLineStarted = null;
        }

        private BorrowedDawnRulesInstaller installer;
        private BorrowedDawnRulesAdapter adapter;
        private readonly HashSet<Outcome> nudged = new HashSet<Outcome>();
        private readonly Queue<KeyValuePair<string, string>> pending = new Queue<KeyValuePair<string, string>>();

        private GameObject panel;
        private Text speakerText;
        private Text lineText;
        private float lineHideAt;
        private bool solved;
        private bool loading;

        /// <summary>The subtitle on screen now, or empty. Read by tests.</summary>
        public string CurrentLine => panel != null && panel.activeSelf ? lineText.text : string.Empty;

        public bool Solved => solved;

        private void Start()
        {
            installer = GetComponent<BorrowedDawnRulesInstaller>();
            adapter = installer != null ? installer.Adapter : null;
            if (adapter == null)
            {
                Debug.LogError("TeachingRoomDirector found no rules adapter, so Anpu stays silent.", this);
                enabled = false;
                return;
            }

            BuildSubtitlePanel();
            adapter.RulesResolved += HandleRulesResolved;
            adapter.StateChanged += HandleStateChanged;

            RoomDefinition room = adapter.Room;
            Show(room.Title, string.Empty, TitleSeconds, TitleColor);
            for (int i = 0; i < room.AnpuIntro.Count; i++)
            {
                pending.Enqueue(new KeyValuePair<string, string>(Key(room, "intro." + (i + 1)), room.AnpuIntro[i]));
            }
        }

        private void OnDestroy()
        {
            if (adapter != null)
            {
                adapter.RulesResolved -= HandleRulesResolved;
                adapter.StateChanged -= HandleStateChanged;
            }
        }

        private void Update()
        {
            if (panel != null && panel.activeSelf && Time.unscaledTime >= lineHideAt)
            {
                panel.SetActive(false);
            }
            if ((panel == null || !panel.activeSelf) && pending.Count > 0)
            {
                KeyValuePair<string, string> next = pending.Dequeue();
                ShowAnpu(next.Key, next.Value, SecondsPerIntroLine);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame && !Ankhu.Core.InputGate.TextEntryActive)
            {
                LoadNext();
            }
        }

        private void HandleRulesResolved(Rules.CommandResult result)
        {
            if (solved || result.Accepted || nudged.Contains(result.Outcome))
            {
                return;
            }
            if (adapter.Room.AnpuNudges.TryGetValue(result.Outcome, out string nudge))
            {
                nudged.Add(result.Outcome);
                pending.Clear();
                ShowAnpu(Key(adapter.Room, "nudge." + result.Outcome), nudge, NudgeSeconds);
            }
        }

        private void HandleStateChanged(Ankhu.Integration.RoomSnapshot previous, Ankhu.Integration.RoomSnapshot current)
        {
            if (solved || !adapter.IsSolved)
            {
                return;
            }
            solved = true;
            pending.Clear();
            ShowAnpu(Key(adapter.Room, "solve"), adapter.Room.AnpuOnSolve, SolveSeconds + 1f);
            StartCoroutine(LoadNextAfter(SolveSeconds));
        }

        private IEnumerator LoadNextAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            LoadNext();
        }

        private void LoadNext()
        {
            if (loading)
            {
                return;
            }
            string next = BorrowedDawnRooms.NextSceneName(installer.RoomId);
            if (string.IsNullOrEmpty(next))
            {
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(next))
            {
                Debug.LogError("The next room, " + next + ", is not in Build Settings. Run Ankhu > Build Borrowed Dawn Greybox.", this);
                return;
            }
            loading = true;
            SceneManager.LoadScene(next, LoadSceneMode.Single);
        }

        public static string Key(RoomDefinition room, string part)
        {
            return "dialogue.anpu." + room.Id + "." + part;
        }

        private void ShowAnpu(string key, string line, float seconds)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }
            Show("Anpu", line, seconds, LineColor);
            AnpuLineStarted?.Invoke(key, line);
        }

        private void Show(string speaker, string line, float seconds, Color lineColor)
        {
            if (panel == null)
            {
                return;
            }
            bool titleOnly = string.IsNullOrEmpty(line);
            speakerText.text = titleOnly ? string.Empty : speaker;
            lineText.text = titleOnly ? speaker : line;
            lineText.color = titleOnly ? TitleColor : lineColor;
            lineText.fontSize = titleOnly ? 40 : 28;
            panel.SetActive(true);
            lineHideAt = Time.unscaledTime + seconds;
        }

        private void BuildSubtitlePanel()
        {
            var canvasGo = new GameObject("Anpu Subtitles", typeof(RectTransform));
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var canvasRect = (RectTransform)canvasGo.transform;

            // Top centre, below the status strip, clear of the feedback log and the speech bubble.
            Image backing = AnkhuUi.CreateImage("Subtitle Panel", canvasRect, PanelColor);
            AnkhuUi.Place(backing.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(1180f, 0f));
            var layout = backing.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 14, 16);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = backing.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            speakerText = AnkhuUi.CreateText("Speaker", backing.rectTransform, 22, TextAnchor.UpperCenter, SpeakerColor, true);
            lineText = AnkhuUi.CreateText("Line", backing.rectTransform, 28, TextAnchor.UpperCenter, LineColor, true);
            lineText.horizontalOverflow = HorizontalWrapMode.Wrap;
            lineText.lineSpacing = 1.08f;

            panel = backing.gameObject;
            panel.SetActive(false);
        }
    }
}
