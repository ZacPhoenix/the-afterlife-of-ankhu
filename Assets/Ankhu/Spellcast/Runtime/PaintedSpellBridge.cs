using System;
using System.Collections.Generic;
using System.Reflection;
using Ankhu.Bindings;
using Ankhu.Core;
using Ankhu.Integration;
using Ankhu.Layout;
using Ankhu.Player;
using Ankhu.Realms;
using Ankhu.Realms.Core;
using Ankhu.Speech;
using Ankhu.Voice;
using UnityEngine;
using UnityEngine.SceneManagement;
using Rules = Ankhu.BorrowedDawn;
using CommandResult = Ankhu.Integration.CommandResult;

namespace Ankhu.Spellcast
{
    /// <summary>
    /// Gives the Borrowed Dawn style rooms (Borrowed Dawn and the teaching rooms, greybox or painted) the painted
    /// speech bubble and the glyph flight of the words of power.
    ///
    /// The flat <see cref="SpeechBubbleController"/> stays in charge of the speak path (history, rules, voice and
    /// animation through its Spoken event). This bridge only takes over the front end: Enter opens the painted
    /// <see cref="SpellPresenter"/> bubble, and on submit the bridge
    ///   1. holds the room's snapshot broadcasts (<see cref="RoomRulesHost.HoldSnapshots"/>),
    ///   2. speaks through <see cref="SpeechBubbleController.SpeakExternal"/>,
    ///   3. turns the typed words into their hieroglyphs (<see cref="Lexicon.TryGlossToken"/>) and finds the object
    ///      the command acts on (<see cref="SpellTargets"/>),
    ///   4. plays the outcome: the glyphs leave the bubble, fly to the object and strike it,
    ///   5. on the strike, releases the hold so the object changes, and publishes the rules' message.
    /// A refusal crumbles the words and shows the rules' reason in rubric red. Movement words just fade.
    ///
    /// Installed automatically in any scene with a SpeechBubbleController (see <see cref="SpellcastBootstrap"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaintedSpellBridge : MonoBehaviour
    {
        private SpeechBubbleController controller;
        private SpellPresenter presenter;
        private SpellVoicePlayer voice;
        private Delegate codexHandler;
        private EventInfo codexEvent;

        private bool holding;
        private float lastVoiceSeconds;
        private Rules.CommandResult lastRules;
        private Rules.CommandResult lastAction;

        public SpellPresenter Presenter => presenter;

        private void Start()
        {
            controller = GetComponent<SpeechBubbleController>();
            if (controller == null)
            {
                Destroy(this);
                return;
            }
            controller.Suppressed = true;

            // The presenter carries its own overlay canvas, so it stays at the scene root (a canvas nested in the
            // rig's canvas would lose its scaler).
            presenter = SpellPresenter.Create(CameraForBubble, controller.Speaker);
            SceneManager.MoveGameObjectToScene(presenter.gameObject, gameObject.scene);
            presenter.Bubble.HeadOffset = new Vector3(0f, BorrowedDawnLayout.AnkhuHeight + SpeechBubbleController.HeadAnchorAbove, 0f);
            presenter.Submitted += HandleSubmitted;

            voice = FindAnyObjectByType<SpellVoicePlayer>();
            if (voice != null) voice.SpeechStarted += HandleSpeechStarted;

            SubscribeCodex();
        }

        private void OnDestroy()
        {
            if (presenter != null) presenter.Submitted -= HandleSubmitted;
            if (voice != null) voice.SpeechStarted -= HandleSpeechStarted;
            UnsubscribeCodex();
            ReleaseHold();
            if (controller != null) controller.Suppressed = false;
            if (presenter != null) Destroy(presenter.gameObject);
        }

        private void Update()
        {
            if (presenter == null || controller == null) return;
            // The flat bubble's component switches off in cutscenes and menus. Follow it.
            presenter.InputAllowed = controller.isActiveAndEnabled;
            Coach();
        }

        /// <summary>
        /// Zac 2026-11-26: in the first teaching room he did not know what to do. There, the bubble shows the next
        /// words to speak in grey letters to type over (as at the First Gate), but only where speaking is the next
        /// step. When the next step is a walk (onto the lowered platform, off at the top) the bubble stays plain.
        /// </summary>
        private void Coach()
        {
            var adapter = RoomRulesHost.Instance != null ? RoomRulesHost.Instance.Adapter as BorrowedDawnRulesAdapter : null;
            if (adapter == null || adapter.Room.Id != CoachedRoom) return;
            Rules.RoomState state = adapter.Session.State;
            if (ReferenceEquals(state, coachState)) return;
            coachState = state;
            presenter.Coach = adapter.NextSpokenCommand();
        }

        private const string CoachedRoom = "teach-1-cistern";
        private Rules.RoomState coachState;

        private Camera CameraForBubble()
        {
            if (controller != null && controller.WorldCamera != null && controller.WorldCamera.isActiveAndEnabled) return controller.WorldCamera;
            return Camera.main;
        }

        // ------------------------------------------------------------------ speak

        private void HandleSubmitted(string text)
        {
            RoomRulesHost host = RoomRulesHost.Instance;
            if (host == null || host.Adapter == null)
            {
                Refuse("No rules are connected to this room.", CommandOutcome.NotConnected, FeedbackSource.System);
                return;
            }
            if (!host.IsWorldSettled)
            {
                Refuse(InteractionFocus.StillMovingMessage, CommandOutcome.Rejected, FeedbackSource.System);
                return;
            }

            var adapter = host.Adapter as BorrowedDawnRulesAdapter;
            lastRules = null;
            lastAction = null;
            lastVoiceSeconds = 0f;
            if (adapter != null) adapter.RulesResolved += CaptureRules;
            host.HoldSnapshots();
            holding = true;
            CommandResult result;
            try
            {
                result = controller.SpeakExternal(text, false);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                result = null;
            }
            finally
            {
                if (adapter != null) adapter.RulesResolved -= CaptureRules;
            }
            if (result == null)
            {
                ReleaseHold();
                Refuse("The words fall into silence.", CommandOutcome.NotConnected, FeedbackSource.System);
                return;
            }

            bool landed = result.Outcome == CommandOutcome.Success || result.Outcome == CommandOutcome.NoChange;
            Rules.Command command = lastAction?.Command ?? lastRules?.Command;
            bool movement = SpellTargets.IsMovement(command);

            var view = new SpellOutcomeView
            {
                Success = landed,
                Reply = landed ? string.Empty : result.Message,
                VoiceSeconds = lastVoiceSeconds,
            };
            if (landed && !movement)
            {
                view.Words = Gloss(text);
                view.Target = SpellTargets.For(command);
            }
            if (!landed)
            {
                // Nothing changes on a refusal, so the room need not wait for the sand.
                ReleaseHold();
            }
            CommandOutcome outcome = result.Outcome;
            string message = result.Message;
            view.OnArrive = () =>
            {
                ReleaseHold();
                FeedbackBus.Publish(FeedbackSource.Speech, outcome, message);
            };
            presenter.Resolve(view);
        }

        private void Refuse(string reason, CommandOutcome outcome, FeedbackSource source)
        {
            presenter.Resolve(new SpellOutcomeView
            {
                Success = false,
                Reply = reason,
                OnArrive = () => FeedbackBus.Publish(source, outcome, reason),
            });
        }

        private void CaptureRules(Rules.CommandResult r)
        {
            lastRules = r;
            if (r != null && !SpellTargets.IsMovement(r.Command)) lastAction = r;
        }

        private void HandleSpeechStarted(SpokenSpell spell, VoicePlan plan)
        {
            if (plan != null) lastVoiceSeconds = (float)plan.Duration;
        }

        private void ReleaseHold()
        {
            if (!holding) return;
            holding = false;
            RoomRulesHost host = RoomRulesHost.Instance;
            if (host != null) host.ReleaseSnapshots();
        }

        /// <summary>The typed words that have an Egyptian form, in order. Other words fade.</summary>
        public static List<SpokenWord> Gloss(string text)
        {
            var words = new List<SpokenWord>();
            if (string.IsNullOrEmpty(text)) return words;
            int i = 0;
            while (i < text.Length)
            {
                if (!IsWordChar(text[i])) { i++; continue; }
                int start = i;
                while (i < text.Length && IsWordChar(text[i])) i++;
                string token = text.Substring(start, i - start);
                if (Lexicon.TryGlossToken(token, out WordGloss g))
                {
                    words.Add(new SpokenWord(token, g.Transliteration, g.Signs, g.Cartouche));
                }
            }
            return words;
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '\'' || c == '-' || c == '’';

        // ------------------------------------------------------------------ codex roll

        // CodexBook.WordChosen is being added by the codex thread. Bind to it by reflection so this assembly compiles
        // with or without it, and picks it up as soon as it exists.
        private void SubscribeCodex()
        {
            codexEvent = typeof(CodexBook).GetEvent("WordChosen", BindingFlags.Public | BindingFlags.Static);
            if (codexEvent == null || codexEvent.EventHandlerType != typeof(Action<string>)) { codexEvent = null; return; }
            codexHandler = new Action<string>(InsertFromCodex);
            codexEvent.AddEventHandler(null, codexHandler);
        }

        private void UnsubscribeCodex()
        {
            if (codexEvent != null && codexHandler != null) codexEvent.RemoveEventHandler(null, codexHandler);
            codexEvent = null;
            codexHandler = null;
        }

        private void InsertFromCodex(string word)
        {
            if (presenter != null) presenter.InsertWord(word);
        }
    }

    /// <summary>
    /// Adds a <see cref="PaintedSpellBridge"/> next to every <see cref="SpeechBubbleController"/>, in every scene,
    /// including rigs built at runtime. Set <see cref="Enabled"/> false (before the scene loads) to keep the flat bubble.
    /// </summary>
    public sealed class SpellcastBootstrap : MonoBehaviour
    {
        public static bool Enabled = true;
        private const float ScanSeconds = 0.5f;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("Spellcast Bootstrap") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<SpellcastBootstrap>();
            Scan();
        }

        private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Scan();

        private void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + ScanSeconds;
            Scan();
        }

        private static void Scan()
        {
            if (!Enabled) return;
            SpeechBubbleController[] bubbles = FindObjectsByType<SpeechBubbleController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (SpeechBubbleController b in bubbles)
            {
                if (b != null && b.GetComponent<PaintedSpellBridge>() == null) b.gameObject.AddComponent<PaintedSpellBridge>();
            }
        }
    }
}
