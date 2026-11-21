using System;
using System.Collections;
using System.Collections.Generic;
using Ankhu.Core;
using Ankhu.Speech;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Ankhu.Realms
{
    /// <summary>
    /// The words of power. Enter opens a speech bubble above Ankhu's head, the player types, Enter submits
    /// (<see cref="Submitted"/>), the director judges the words and calls <see cref="Resolve"/>. On success
    /// each English word dissolves into gold motes and becomes its hieroglyphs, which then leave the bubble,
    /// fly through the world to the target and set it alight. On failure the words crumble to sand and a
    /// quiet reply is shown. Built entirely in code: <see cref="Create"/>.
    ///
    /// Guarantees: for every <see cref="Resolve"/>, <see cref="SpellOutcomeView.OnArrive"/> is called exactly
    /// once and <see cref="Busy"/> returns to false, even if the target is destroyed mid flight, the presenter
    /// is disabled, or an exception escapes a callback (a watchdog finishes any sequence that overruns).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpellPresenter : MonoBehaviour
    {
        // ------------------------------------------------------------------ timing (seconds, unscaled)

        private const float BackspaceRepeatDelay = 0.42f;
        private const float BackspaceRepeatInterval = 0.045f;
        private const float PendingTimeout = 4f;
        private const float LetterStagger = 0.045f;
        private const float SignStagger = 0.085f;
        private const float SignAppear = 0.5f;
        private const float HoldAssembled = 0.6f;
        private const float LaunchStagger = 0.075f;
        private const float SandStagger = 0.028f;
        private const float WatchdogSlack = 15f;

        // ------------------------------------------------------------------ static API

        /// <summary>True when the bubble opens for typing, false when it closes. Freeze movement while true.</summary>
        public static event Action<bool> OpenChanged;
        /// <summary>A sign leaves the bubble for the world: Gardiner code and world position. For sfx.</summary>
        public static event Action<string, Vector3> GlyphLaunched;
        /// <summary>A sign strikes its target: Gardiner code and world position. For sfx.</summary>
        public static event Action<string, Vector3> GlyphArrived;
        /// <summary>A printable character was typed into the bubble. For soft typing sfx.</summary>
        public static event Action<char> CharTyped;
        /// <summary>An outcome starts playing: true for success, false for failure. For sfx and music stings.</summary>
        public static event Action<bool> OutcomeStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OpenChanged = null;
            GlyphLaunched = null;
            GlyphArrived = null;
            CharTyped = null;
            OutcomeStarted = null;
        }

        /// <summary>Builds the presenter and its bubble canvas. <paramref name="activeCamera"/> is asked every frame (camera cuts).</summary>
        public static SpellPresenter Create(Func<Camera> activeCamera, Transform speakerHead)
        {
            var go = new GameObject("Spell Presenter");
            var p = go.AddComponent<SpellPresenter>();
            p.cameraSource = activeCamera;
            p.head = speakerHead;
            p.bubble = RealmSpeechBubble.Create(go.transform);
            p.bubble.CameraSource = activeCamera;
            p.bubble.Head = speakerHead;
            return p;
        }

        // ------------------------------------------------------------------ instance API

        /// <summary>Raw text when the player presses Enter on a non empty bubble.</summary>
        public event Action<string> Submitted;

        public bool IsOpen => isOpen;

        /// <summary>When false, Enter does not open the bubble (cutscenes, menus). Closing an open bubble is up to the caller via <see cref="Cancel"/>.</summary>
        public bool InputAllowed { get; set; } = true;

        /// <summary>True while an outcome animation plays.</summary>
        public bool Busy { get; private set; }

        /// <summary>The view, for tweaks such as <see cref="RealmSpeechBubble.HeadOffset"/>.</summary>
        public RealmSpeechBubble Bubble => bubble;

        /// <summary>The speaker head transform (glyphs launch from its depth).</summary>
        public Transform Head
        {
            get => head;
            set { head = value; if (bubble != null) bubble.Head = value; }
        }

        public SpeechHistory History => history;

        // ------------------------------------------------------------------ state

        private sealed class Flight
        {
            public string Code;
            public FxQuad Core;
            public FxQuad Glow;
            public Vector3 P0, P1, P2, P3;
            public Vector3 TargetOffset;
            public float Start, Duration;
            public float StartSize, EndSize;
            public float Roll, RollSpeed, Phase;
            public bool Dissipate;
            public bool Done;
            public float TrailCarry;
            public Vector3 Last;
        }

        private Func<Camera> cameraSource;
        private Transform head;
        private RealmSpeechBubble bubble;
        private readonly SpeechBuffer buffer = new SpeechBuffer(SpeechBuffer.DefaultMaxLength);
        private readonly SpeechHistory history = new SpeechHistory(SpeechHistory.DefaultCapacity);
        private readonly List<KeyControl> heldAtOpen = new List<KeyControl>();
        private readonly List<Flight> flights = new List<Flight>();
        private Keyboard subscribedKeyboard;
        private bool isOpen;
        private int openedFrame = -1;
        private float backspaceRepeatAt;

        private SpellOutcomeView current;
        private bool arriveCalled;
        private Coroutine sequence;
        private float deadline;
        private bool skipRequested;
        private ISpellTarget target;
        private Vector3 lastAnchor;

        private static float Now => Time.unscaledTime;

        private Camera ActiveCamera => FxQuad.ResolveCamera(cameraSource != null ? SafeCamera() : null);

        private Camera SafeCamera()
        {
            try { return cameraSource(); }
            catch (Exception e) { Debug.LogException(e, this); return null; }
        }

        // ------------------------------------------------------------------ lifecycle

        private void OnDisable()
        {
            if (isOpen) Close(false);
            if (Busy || current != null) EndSequence(true);
            FlushFlights();
            if (bubble != null) bubble.HideImmediate();
        }

        private void OnDestroy()
        {
            if (isOpen) Close(false);
            if (Busy || current != null) EndSequence(true);
            FlushFlights();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (isOpen)
            {
                UpdateOpen(keyboard);
            }
            else if (keyboard != null && InputAllowed && !Busy && InputGate.GameplayInputAllowed &&
                     (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                Open(keyboard);
            }
            else if (Busy && keyboard != null &&
                     (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                skipRequested = true;
            }

            if (!Busy && !isOpen && bubble != null && bubble.PendingSeconds > PendingTimeout)
            {
                // Submitted but never resolved: let the words fade away quietly.
                for (int i = 0; i < bubble.LetterCount; i++) bubble.ExitLetter(i, LetterExit.Fade, Now + i * 0.01f, Vector2.zero, Vector2.zero);
                bubble.Hide();
            }

            UpdateFlights();

            if (Busy && Now > deadline)
            {
                Debug.LogWarning("[Ankhu.Realms] Spell sequence overran, finishing it.", this);
                EndSequence(true);
            }
        }

        // ------------------------------------------------------------------ typing

        private void Open(Keyboard keyboard)
        {
            isOpen = true;
            openedFrame = Time.frameCount;
            buffer.Clear();
            history.ResetCursor();
            heldAtOpen.Clear();
            foreach (KeyControl key in keyboard.allKeys)
            {
                if (key != null && key.isPressed) heldAtOpen.Add(key);
            }
            InputGate.BeginTextEntry();
            subscribedKeyboard = keyboard;
            subscribedKeyboard.onTextInput += HandleTextInput;
            bubble.BeginTyping();
            RaiseOpenChanged(true);
        }

        private void Close(bool keepText)
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
            InputGate.EndTextEntry();
            if (bubble != null) bubble.EndTyping(keepText);
            RaiseOpenChanged(false);
        }

        /// <summary>Closes the bubble and discards the text. Does nothing when closed.</summary>
        public void Cancel()
        {
            if (isOpen) Close(false);
        }

        private void HandleTextInput(char c)
        {
            if (!isOpen || Time.frameCount == openedFrame) return;
            if (IsHeldSinceOpen(c)) return;
            if (buffer.Append(c))
            {
                history.ResetCursor();
                bubble.SetText(buffer.Text);
                try { CharTyped?.Invoke(c); } catch (Exception e) { Debug.LogException(e, this); }
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
                if (!string.IsNullOrEmpty(label) && label.Length == 1 && char.ToLowerInvariant(label[0]) == char.ToLowerInvariant(c)) return true;
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
            if (Time.frameCount == openedFrame) return; // the Enter that opened must not submit
            for (int i = heldAtOpen.Count - 1; i >= 0; i--)
            {
                if (!heldAtOpen[i].isPressed) heldAtOpen.RemoveAt(i);
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

            float now = Now;
            KeyControl backspace = keyboard.backspaceKey;
            bool changed = false;
            if (backspace.wasPressedThisFrame)
            {
                if (buffer.Backspace()) { history.ResetCursor(); changed = true; }
                backspaceRepeatAt = now + BackspaceRepeatDelay;
            }
            else if (backspace.isPressed && now >= backspaceRepeatAt)
            {
                changed |= buffer.Backspace();
                backspaceRepeatAt = now + BackspaceRepeatInterval;
            }
            if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                string recalled = history.Previous(buffer.Text);
                if (recalled != null) { buffer.SetText(recalled); changed = true; }
            }
            else if (keyboard.downArrowKey.wasPressedThisFrame)
            {
                string recalled = history.Next();
                if (recalled != null) { buffer.SetText(recalled); changed = true; }
            }
            if (changed) bubble.SetText(buffer.Text);
        }

        private void Submit()
        {
            string utterance = buffer.Text.Trim();
            if (utterance.Length == 0 || !buffer.CanSubmit)
            {
                Close(false);
                return;
            }
            history.Push(utterance);
            bubble.SetText(utterance, false);
            Close(true);
            try { Submitted?.Invoke(utterance); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        private void RaiseOpenChanged(bool open)
        {
            try { OpenChanged?.Invoke(open); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        // ------------------------------------------------------------------ resolve

        /// <summary>
        /// Plays the judged outcome of the last submission. Call right after <see cref="Submitted"/> (it may be
        /// called from inside the Submitted handler). If no words are on show (a scripted spell) the English of
        /// the outcome's words is shown first. A Resolve during another sequence finishes the previous one at once.
        /// </summary>
        public void Resolve(SpellOutcomeView outcome)
        {
            if (outcome == null)
            {
                Debug.LogWarning("[Ankhu.Realms] SpellPresenter.Resolve called with null.", this);
                return;
            }
            if (Busy || current != null) EndSequence(true);
            if (isOpen) Close(false);

            current = outcome;
            arriveCalled = false;
            skipRequested = false;
            Busy = true;
            target = outcome.Target;
            lastAnchor = SafeAnchor(target, HeadPosition());
            float voice = Mathf.Max(0f, outcome.VoiceSeconds);
            deadline = Now + voice + WatchdogSlack;

            if (!isActiveAndEnabled || bubble == null)
            {
                EndSequence(true);
                return;
            }
            if (bubble.LetterCount == 0 || !bubble.Visible || bubble.IsHiding)
            {
                bubble.ShowWords(JoinEnglish(outcome.Words));
            }
            bubble.BeginPerformance();
            try { OutcomeStarted?.Invoke(outcome.Success); } catch (Exception e) { Debug.LogException(e, this); }
            sequence = StartCoroutine(outcome.Success ? SuccessSequence(outcome, voice) : FailureSequence(outcome, voice));
        }

        private static string JoinEnglish(IReadOnlyList<SpokenWord> words)
        {
            if (words == null || words.Count == 0) return "...";
            var parts = new string[words.Count];
            for (int i = 0; i < words.Count; i++) parts[i] = words[i].English;
            return string.Join(" ", parts);
        }

        private IEnumerator Until(float t)
        {
            while (Now < t) yield return null;
        }

        // ------------------------------------------------------------------ success

        private IEnumerator SuccessSequence(SpellOutcomeView o, float voice)
        {
            float t0 = Now;
            IReadOnlyList<SpokenWord> words = o.Words ?? Array.Empty<SpokenWord>();
            List<Vector2Int> spans = bubble.MatchWords(words);
            bool anySigns = bubble.BuildGlyphRow(words);
            bubble.FitGlyphRow();

            // Words the spell did not use fade, the ones it did turn gold.
            var used = new bool[bubble.LetterCount];
            foreach (Vector2Int s in spans)
            {
                if (s.x < 0) continue;
                for (int i = s.x; i < s.y; i++) used[i] = true;
            }
            for (int i = 0; i < bubble.LetterCount; i++)
            {
                if (used[i]) bubble.TintLetter(i, MagicPalette.Gold, t0, 0.2f);
                else bubble.ExitLetter(i, LetterExit.Fade, t0 + 0.08f + i * 0.012f, Vector2.zero, Vector2.zero);
            }

            // Letter by letter the words dissolve into motes that assemble the signs.
            float wordStart = t0 + 0.24f;
            float lastAppear = t0 + 0.3f;
            for (int w = 0; w < words.Count; w++)
            {
                Vector2Int span = w < spans.Count ? spans[w] : new Vector2Int(-1, -1);
                RealmSpeechBubble.SignGroup g = w < bubble.Groups.Count ? bubble.Groups[w] : null;
                Vector2 groupCenter = bubble.GroupCenter(w);
                Vector2 spread = new Vector2(Mathf.Max(20f, bubble.GroupWidth(w) * 0.8f), RealmSpeechBubble.GlyphEm * 0.6f * bubble.GlyphScale);
                int n = span.x >= 0 ? span.y - span.x : 0;
                if (g == null || g.Signs.Count == 0)
                {
                    // No drawable sign for this word: its motes simply rise from where it stood.
                    groupCenter = bubble.LetterCenter(Mathf.Max(0, span.x), Mathf.Max(0, span.y)) + new Vector2(0f, 46f);
                    spread = new Vector2(60f, 20f);
                }
                for (int j = 0; j < n; j++)
                {
                    bubble.ExitLetter(span.x + j, LetterExit.Gold, wordStart + j * LetterStagger, groupCenter, spread);
                }
                float groupStart = wordStart + 0.2f + n * LetterStagger * 0.6f;
                if (g != null && g.Signs.Count > 0)
                {
                    bubble.ScheduleGroupFrame(g, groupStart - 0.06f);
                    for (int s = 0; s < g.Signs.Count; s++)
                    {
                        float at = groupStart + s * SignStagger;
                        bubble.ScheduleSign(g.Signs[s], at);
                        lastAppear = Mathf.Max(lastAppear, at);
                    }
                }
                wordStart += Mathf.Max(0.26f, n * LetterStagger * 0.75f);
            }
            float assembled = lastAppear + SignAppear * 0.9f;
            yield return Until(assembled);

            if (anySigns)
            {
                bubble.StartShimmer(Now);
                float holdUntil = Mathf.Max(Now + HoldAssembled, t0 + voice - 0.35f);
                while (Now < holdUntil) yield return null;
                bubble.StartAnticipation(Now);
                yield return Until(Now + RealmSpeechBubble.AnticipateSeconds);
            }
            else
            {
                yield return Until(Mathf.Max(Now + 0.3f, t0 + voice - 0.35f));
            }

            // Launch.
            Camera cam = ActiveCamera;
            bool haveTarget = TargetAlive(target);
            int signCount = bubble.Signs.Count;
            if (cam != null && (signCount > 0 || haveTarget))
            {
                if (signCount == 0)
                {
                    // No drawable sign: send a single comet of light so the target still answers.
                    Vector3 from = HeadPosition();
                    LaunchFlight(null, from, 0.35f, cam, 0, 1, !haveTarget);
                }
                for (int i = 0; i < signCount; i++)
                {
                    RealmSpeechBubble.Sign s = bubble.Signs[i];
                    Vector2 sp = bubble.SignScreenPoint(s);
                    float pixels = bubble.SignScreenHeight(s);
                    Vector3 world = ScreenToWorldAtHead(cam, sp, pixels, out float worldSize);
                    bubble.LaunchSign(s);
                    LaunchFlight(s.Code, world, worldSize, cam, i, signCount, !haveTarget);
                    if (i < signCount - 1) yield return Until(Now + LaunchStagger);
                }
            }
            yield return Until(Now + 0.22f);
            bubble.Hide();

            while (HasLiveFlights()) yield return null;

            if (TargetAlive(target))
            {
                try { target.Ignite(1f); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
            CallArrive();
            yield return Until(Mathf.Max(Now + 0.35f, t0 + voice));
            sequence = null;
            EndSequence(false);
        }

        // ------------------------------------------------------------------ failure

        private IEnumerator FailureSequence(SpellOutcomeView o, float voice)
        {
            float t0 = Now;
            int n = bubble.LetterCount;
            for (int i = 0; i < n; i++) bubble.TintLetter(i, MagicPalette.Sand, t0 + 0.04f, 0.35f);
            bubble.ShakeLetters(t0, 0.5f, 3.2f);
            float crumble = t0 + 0.46f;
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                if (bubble.IsSpace(i)) continue;
                bubble.ExitLetter(i, LetterExit.Sand, crumble + k * SandStagger, Vector2.zero, Vector2.zero);
                k++;
            }
            float crumbled = crumble + k * SandStagger + 0.25f;
            float replyAt = crumble + k * SandStagger * 0.5f;
            string reply = o.Reply ?? string.Empty;
            bubble.ShowReply(reply, replyAt);
            yield return Until(crumbled);
            bubble.CollapseMainRow();

            float readEnd = reply.Length > 0 ? replyAt + 1.5f + reply.Length * 0.045f : crumbled + 0.3f;
            float voiceEnd = t0 + voice;
            skipRequested = false;
            while (Now < Mathf.Max(readEnd, voiceEnd))
            {
                // After the voice has finished, Enter or Escape dismisses the reply early.
                if (skipRequested && Now >= voiceEnd && Now > replyAt + 0.4f) break;
                skipRequested = false;
                yield return null;
            }
            CallArrive();
            bubble.Hide();
            sequence = null;
            EndSequence(false);
        }

        // ------------------------------------------------------------------ flights

        private Vector3 HeadPosition()
        {
            if (head != null) return head.position + (bubble != null ? bubble.HeadOffset : Vector3.up * 0.3f);
            Camera cam = ActiveCamera;
            return cam != null ? cam.transform.position + cam.transform.forward * 5f : Vector3.zero;
        }

        private Vector3 ScreenToWorldAtHead(Camera cam, Vector2 screen, float pixelHeight, out float worldSize)
        {
            Vector3 h = HeadPosition();
            float depth = Vector3.Dot(h - cam.transform.position, cam.transform.forward);
            depth = Mathf.Max(depth, cam.nearClipPlane + 0.6f);
            float perPixel = cam.orthographic
                ? 2f * cam.orthographicSize / Mathf.Max(1, cam.pixelHeight)
                : 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);
            worldSize = Mathf.Max(0.05f, pixelHeight * perPixel);
            return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
        }

        private void LaunchFlight(string code, Vector3 start, float size, Camera cam, int index, int count, bool dissipate)
        {
            var f = new Flight
            {
                Code = code,
                P0 = start,
                Start = Now,
                StartSize = size,
                Dissipate = dissipate,
                Roll = UnityEngine.Random.Range(-8f, 8f),
                RollSpeed = UnityEngine.Random.Range(-14f, 14f),
                Phase = UnityEngine.Random.value * 6.28f,
                Last = start,
            };
            Vector3 right = cam.transform.right;
            Vector3 up = Vector3.up;
            if (dissipate)
            {
                float spreadIndex = count > 1 ? index - (count - 1) * 0.5f : 0f;
                f.P3 = start + up * UnityEngine.Random.Range(1.3f, 2.1f) + right * (spreadIndex * 0.42f + UnityEngine.Random.Range(-0.15f, 0.15f));
                f.P1 = start + up * 0.45f + right * spreadIndex * 0.08f;
                f.P2 = f.P3 - up * 0.35f;
                f.Duration = UnityEngine.Random.Range(1.5f, 1.8f);
                f.EndSize = size * 1.5f;
            }
            else
            {
                float spreadIndex = count > 1 ? index - (count - 1) * 0.5f : 0f;
                f.TargetOffset = right * spreadIndex * 0.05f;
                f.P3 = lastAnchor + f.TargetOffset;
                float dist = Vector3.Distance(start, f.P3);
                Vector3 toward = dist > 0.001f ? (f.P3 - start) / dist : Vector3.forward;
                float lift = (0.32f * dist + 0.45f) * UnityEngine.Random.Range(0.85f, 1.18f);
                f.P1 = start + up * lift + toward * 0.15f * dist + right * UnityEngine.Random.Range(-0.3f, 0.3f) * dist * 0.3f;
                f.P2 = f.P3 + up * (0.42f * dist + 0.3f) * UnityEngine.Random.Range(0.8f, 1.2f) + right * UnityEngine.Random.Range(-0.3f, 0.3f) * dist * 0.25f;
                f.Duration = Mathf.Clamp(0.7f + 0.22f * Mathf.Sqrt(dist), 0.8f, 1.7f) + UnityEngine.Random.Range(-0.06f, 0.08f);
                f.EndSize = Mathf.Max(0.12f, size * 0.7f);
            }
            Texture coreTex = code != null ? GlyphAtlas.GetTexture(code) : GlyphAtlas.Flare;
            Texture glowTex = code != null ? GlyphAtlas.GetGlowTexture(code) : GlyphAtlas.Mote;
            f.Glow = new FxQuad("Glyph Glow " + code, glowTex);
            f.Core = new FxQuad("Glyph " + code, coreTex);
            ApplyFlight(f, 0f, cam);
            flights.Add(f);
            MagicFx.SparkAt(start, Vector3.up * 0.4f, size * 0.9f, 0.3f, MagicPalette.WhiteHot);
            for (int i = 0; i < 5; i++)
            {
                MagicFx.Mote(start, UnityEngine.Random.onUnitSphere * 0.6f + Vector3.up * 0.4f, size * 0.12f, UnityEngine.Random.Range(0.4f, 0.8f), MagicPalette.Gold);
            }
            try { GlyphLaunched?.Invoke(code ?? string.Empty, start); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        private void UpdateFlights()
        {
            if (flights.Count == 0) return;
            Camera cam = ActiveCamera;
            if (TargetAlive(target)) lastAnchor = SafeAnchor(target, lastAnchor);
            float now = Now;
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                Flight f = flights[i];
                if (!f.Dissipate) f.P3 = lastAnchor + f.TargetOffset;
                float t = Ease.Progress(now, f.Start, f.Duration);
                if (!f.Core.Alive || !f.Glow.Alive)
                {
                    FinishFlight(f, false);
                    flights.RemoveAt(i);
                    continue;
                }
                ApplyFlight(f, t, cam);
                if (t >= 1f)
                {
                    FinishFlight(f, !f.Dissipate);
                    flights.RemoveAt(i);
                }
            }
        }

        private void ApplyFlight(Flight f, float t, Camera cam)
        {
            float e = f.Dissipate ? Ease.OutCubic(t) : Ease.Flight(t);
            Vector3 pos = Bezier(f.P0, f.P1, f.P2, f.P3, e);
            // Lift off: a little rise and swell in the first moments.
            float lift = Ease.Bell(Mathf.Clamp01(t / 0.25f)) * (1f - t);
            float size = Mathf.Lerp(f.StartSize, f.EndSize, Ease.InOutSine(t)) * (1f + 0.22f * Ease.Bell(t) + 0.1f * lift);
            float aspect = f.Code != null ? GlyphAtlas.Aspect(f.Code) : 1f;
            f.Core.Transform.position = pos;
            f.Glow.Transform.position = pos;
            f.Core.Transform.localScale = new Vector3(size * aspect, size, 1f);
            f.Glow.Transform.localScale = new Vector3(size * aspect * 1.08f, size * 1.08f, 1f);
            float roll = f.Roll + f.RollSpeed * t + Mathf.Sin(t * Mathf.PI * 1.3f + f.Phase) * 10f * (1f - t);
            f.Core.Billboard(cam, roll);
            f.Glow.Billboard(cam, roll);

            float alpha = Ease.OutCubic(t * 8f);
            if (f.Dissipate) alpha *= 1f - Ease.InQuad(Mathf.InverseLerp(0.45f, 1f, t));
            float flicker = 1f + 0.08f * Mathf.Sin(Now * 23f + f.Phase) + 0.05f * Mathf.Sin(Now * 37f + f.Phase * 2f);
            Color core = Color.Lerp(MagicPalette.WhiteHot, MagicPalette.GoldPale, Ease.OutCubic(t));
            f.Core.SetColor(core, (f.Code == null ? 3.2f : 2.1f + 0.8f * Ease.Bell(t)) * flicker, alpha);
            f.Glow.SetColor(MagicPalette.Gold, (2.6f + 1.2f * (1f - t)) * flicker, alpha * 0.95f);

            // Trailing motes: emitted by distance so speed changes do not thin the trail.
            float step = Mathf.Max(0.03f, f.StartSize * 0.16f);
            Vector3 delta = pos - f.Last;
            f.TrailCarry += delta.magnitude;
            int guard = 0;
            while (f.TrailCarry >= step && guard++ < 12)
            {
                f.TrailCarry -= step;
                float k = 1f - f.TrailCarry / Mathf.Max(delta.magnitude, 1e-4f);
                Vector3 p = Vector3.Lerp(f.Last, pos, Mathf.Clamp01(k)) + UnityEngine.Random.insideUnitSphere * size * 0.18f;
                Color c = Color.Lerp(MagicPalette.Gold, MagicPalette.GoldPale, UnityEngine.Random.value);
                c.a = alpha;
                MagicFx.Mote(p, -delta.normalized * 0.15f + UnityEngine.Random.insideUnitSphere * 0.12f, size * UnityEngine.Random.Range(0.07f, 0.14f), UnityEngine.Random.Range(0.45f, 0.95f), c);
                if (UnityEngine.Random.value < 0.12f) MagicFx.SparkAt(p, UnityEngine.Random.insideUnitSphere * 0.2f, size * 0.22f, 0.25f, MagicPalette.WhiteHot);
            }
            f.Last = pos;
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        private void FinishFlight(Flight f, bool burst)
        {
            if (f.Done) return;
            f.Done = true;
            Vector3 at = f.Core != null && f.Core.Alive ? f.Core.Transform.position : f.P3;
            f.Core?.Destroy();
            f.Glow?.Destroy();
            if (burst)
            {
                MagicFx.Burst(at, Mathf.Max(0.5f, f.EndSize * 2.2f));
                try { GlyphArrived?.Invoke(f.Code ?? string.Empty, at); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
            else if (f.Dissipate)
            {
                MagicFx.FlareAt(at, f.EndSize * 1.2f, 0.35f, MagicPalette.GoldPale);
            }
        }

        private bool HasLiveFlights() => flights.Count > 0;

        private void FlushFlights()
        {
            foreach (Flight f in flights)
            {
                f.Done = true;
                f.Core?.Destroy();
                f.Glow?.Destroy();
            }
            flights.Clear();
        }

        // ------------------------------------------------------------------ finishing

        private static bool TargetAlive(ISpellTarget t)
        {
            if (t == null) return false;
            if (t is UnityEngine.Object o) return o != null;
            return true;
        }

        private Vector3 SafeAnchor(ISpellTarget t, Vector3 fallback)
        {
            if (!TargetAlive(t)) return fallback;
            try { return t.GlyphAnchor; }
            catch (Exception e) { Debug.LogException(e, this); return fallback; }
        }

        private void CallArrive()
        {
            if (arriveCalled || current == null) return;
            arriveCalled = true;
            Action cb = current.OnArrive;
            try { cb?.Invoke(); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        /// <summary>Ends the current sequence. forced = cut short (watchdog, disable, new Resolve): flights become bursts now.</summary>
        private void EndSequence(bool forced)
        {
            if (sequence != null)
            {
                StopCoroutine(sequence);
                sequence = null;
            }
            if (forced)
            {
                foreach (Flight f in flights) FinishFlight(f, false);
                flights.Clear();
                if (current != null && current.Success && !arriveCalled && TargetAlive(target))
                {
                    try { target.Ignite(1f); }
                    catch (Exception e) { Debug.LogException(e, this); }
                }
                if (bubble != null) bubble.Hide();
            }
            CallArrive();
            current = null;
            target = null;
            Busy = false;
        }
    }
}
