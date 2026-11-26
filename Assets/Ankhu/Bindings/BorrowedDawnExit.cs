using System.Collections;
using Ankhu.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ankhu.Bindings
{
    /// <summary>
    /// Zac 2026-11-24: Ankhu walks Borrowed Dawn alone, and Anpu waits at its exit. When the gate opens,
    /// Anpu greets him and leads him on to where the rest of the story begins (Wernes, the first realm),
    /// with a slow fade. Added at runtime to any scene whose rules installer runs Borrowed Dawn, so the
    /// greybox and painted scenes stay untouched. Keys dialogue.anpu.borrowed-dawn.exit.&lt;n&gt;, no
    /// recordings yet. Memory 0 and the terrace reunion walk (WK5) are meant to sit between the gate and
    /// Wernes once they are built.
    /// </summary>
    public sealed class BorrowedDawnExit : MonoBehaviour
    {
        /// <summary>The scene the story continues in after Borrowed Dawn: the terrace reunion when it is built (PlayOrder).</summary>
        public static string NextScene => Ankhu.Opening.PlayOrder.ExitOf(Ankhu.Opening.PlayOrder.BorrowedDawnPainted);

        public const float DelayAfterGateSeconds = 2.5f;
        public const float SecondsPerLine = 6f;

        /// <summary>(speaker, line). Provisional script. Anpu's keys are dialogue.anpu.borrowed-dawn.exit.&lt;n&gt;.</summary>
        public static readonly string[,] ExitLines =
        {
            { "Anpu", "You went in alone, and you came out to me. I knew that you would." },
            { "Ankhu", "There was a dawn in there. I held it up with words that were not mine. And a child was crying somewhere." },
            { "Anpu", "Then you remember more than you did at the gate. The rest is not mine to give you. It is waiting where you left it." },
            { "Anpu", "Come. Below us lies Wernes, the field of the first hour. Something with your name on it has been working there a long time." },
        };

        private static readonly Color PanelColor = new Color(0.05f, 0.04f, 0.03f, 0.78f);
        private static readonly Color SpeakerColor = new Color(0.78f, 0.66f, 1f);
        private static readonly Color AnkhuColor = new Color(1f, 0.82f, 0.36f);
        private static readonly Color LineColor = new Color(0.96f, 0.93f, 0.86f);

        private static bool hooked;

        private BorrowedDawnRulesAdapter adapter;
        private bool leaving;
        private GameObject panel;
        private Text lineText;
        private Text speakerText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            hooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (scene, mode) => Install();
            }
            Install();
        }

        public static BorrowedDawnExit Install()
        {
            var installer = Object.FindAnyObjectByType<BorrowedDawnRulesInstaller>();
            if (installer == null || BorrowedDawnRooms.IsTeachingRoom(installer.RoomId))
            {
                return null;
            }
            var exit = installer.GetComponent<BorrowedDawnExit>();
            return exit != null ? exit : installer.gameObject.AddComponent<BorrowedDawnExit>();
        }

        private void Start()
        {
            var installer = GetComponent<BorrowedDawnRulesInstaller>();
            adapter = installer != null ? installer.Adapter : null;
            if (adapter == null)
            {
                enabled = false;
                return;
            }
            adapter.StateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            if (adapter != null)
            {
                adapter.StateChanged -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(Ankhu.Integration.RoomSnapshot previous, Ankhu.Integration.RoomSnapshot current)
        {
            if (leaving || !current.GateOpen)
            {
                return;
            }
            leaving = true;
            StartCoroutine(Leave());
        }

        private IEnumerator Leave()
        {
            yield return new WaitForSecondsRealtime(DelayAfterGateSeconds);
            // Memory 0: the dawn Hori was born, before Anpu greets him.
            yield return MemoryFlicker.PlayForRoom(BorrowedDawnRooms.BorrowedDawnId);
            BuildPanel();
            int anpuCount = 0;
            for (int i = 0; i < ExitLines.GetLength(0); i++)
            {
                string speaker = ExitLines[i, 0];
                string line = ExitLines[i, 1];
                bool anpu = speaker == "Anpu";
                float hold = SecondsPerLine;
                speakerText.text = speaker;
                speakerText.color = anpu ? SpeakerColor : AnkhuColor;
                lineText.text = line;
                panel.SetActive(true);
                if (anpu)
                {
                    string key = "dialogue.anpu." + BorrowedDawnRooms.BorrowedDawnId + ".exit." + (++anpuCount);
                    hold = Mathf.Max(SecondsPerLine, AnpuVoiceBootstrap.DurationOf(key) + TeachingRoomDirector.VoiceTailSeconds);
                    TeachingRoomDirector.RaiseAnpuLine(key, line);
                }
                yield return new WaitForSecondsRealtime(hold);
            }
            if (!Application.CanStreamedLevelBeLoaded(NextScene))
            {
                Debug.LogError("Borrowed Dawn is solved but " + NextScene + " is not in Build Settings, so Anpu has nowhere to lead.", this);
                yield break;
            }
            Ankhu.Opening.SceneFader.FadeTo(NextScene, 2.5f, 1.2f);
        }

        private void BuildPanel()
        {
            if (panel != null)
            {
                return;
            }
            var canvasGo = new GameObject("Anpu Exit Subtitles", typeof(RectTransform));
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
