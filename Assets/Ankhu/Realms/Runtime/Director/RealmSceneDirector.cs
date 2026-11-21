using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ankhu.Opening;
using Ankhu.Realms.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ankhu.Realms
{
    /// <summary>
    /// Runs one realm scene from its <see cref="SceneScript"/>: zones and camera cuts, the rules room, the speech
    /// presenter (bubble, voice, glyph flight), object state visuals, beats (dialogue, codex writes, memories,
    /// titles, scene changes), the use prompt and hints. One per scene, wired by the realm scene builder.
    /// </summary>
    public sealed class RealmSceneDirector : MonoBehaviour
    {
        [Serializable]
        public sealed class ActorRef
        {
            public string name;
            public Transform transform;
            public Animator animator;
        }

        [Serializable]
        public sealed class Mark
        {
            public string name;
            public Transform transform;
        }

        [Serializable]
        public sealed class CueHook
        {
            public string cue;
            public GameObject[] activate = new GameObject[0];
            public GameObject[] deactivate = new GameObject[0];
            public Animator animator;
            public string trigger;
        }

        public string sceneName = "";
        public WalkPlayer player;
        public Animator playerAnimator;
        public Transform playerHead;
        public FixedCameraDirector cameras;
        public RealmFollower anpu;
        public List<RealmZone> zones = new List<RealmZone>();
        public List<RealmObjectView> views = new List<RealmObjectView>();
        public List<ActorRef> actors = new List<ActorRef>();
        public List<Mark> marks = new List<Mark>();
        public List<CueHook> cues = new List<CueHook>();
        public AudioClip ambience;
        public float ambienceVolume = 0.5f;
        public AudioClip spellCastSfx;
        public AudioClip spellLandSfx;
        public AudioClip spellFailSfx;

        private SceneScript script;
        private RealmRoom room;
        private RealmHud hud;
        private SpellPresenter presenter;
        private readonly Queue<Beat> pending = new Queue<Beat>();
        private readonly HashSet<string> fired = new HashSet<string>();
        private readonly HashSet<string> flags = new HashSet<string>();
        private readonly HashSet<string> failuresSeen = new HashSet<string>();
        private readonly HashSet<string> entered = new HashSet<string>();
        private string zone;
        private bool running;
        private bool frozen;
        private bool solvedFired;
        private bool leaving;
        private float lastSuccess;
        private AudioSource voice;
        private AudioSource ankhuVoice;
        private AudioSource sfx;
        private AudioSource amb;
        private Usable usableHere;

        public RealmRoom Room => room;
        public string Zone => zone;
        public static RealmSceneDirector Current { get; private set; }

        private void Awake()
        {
            Current = this;
            voice = MakeSource();
            ankhuVoice = MakeSource();
            sfx = MakeSource();
            amb = MakeSource();
            if (ambience != null)
            {
                amb.clip = ambience;
                amb.loop = true;
                amb.volume = 0f;
                amb.Play();
            }
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private AudioSource MakeSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        private void Start()
        {
            script = RealmContent.ByScene(string.IsNullOrEmpty(sceneName) ? SceneManager.GetActiveScene().name : sceneName);
            if (script == null)
            {
                Debug.LogError("[Realms] No scene script for " + sceneName, this);
                enabled = false;
                return;
            }
            room = new RealmRoom(script.Room);
            hud = RealmHud.Create();
            CodexStore.EnsureEntries(script.CodexOnEntry);
            CodexBook.Ensure();
            presenter = SpellPresenter.Create(() => cameras != null ? cameras.ActiveCamera : Camera.main, playerHead != null ? playerHead : player.transform);
            presenter.Submitted += OnSubmitted;

            foreach (RealmObjectView v in views)
            {
                string s = room.Get(v.objectId);
                if (s != null) v.SetState(s, true);
            }
            UpdateHints();
            lastSuccess = Time.time;
            SceneFader.FadeIn(1.2f);
            Fire(TriggerKind.Start, "");
        }

        // ------------------------------------------------------------------ frame

        private void Update()
        {
            if (room == null) return;
            if (amb.clip != null) amb.volume = Mathf.MoveTowards(amb.volume, ambienceVolume * OpeningSettings.Music * (voice.isPlaying || ankhuVoice.isPlaying ? 0.6f : 1f), Time.deltaTime * 0.5f);

            bool bookOpen = CodexBook.IsOpen;
            CodexBook.InputBlocked = presenter.IsOpen || presenter.Busy || frozen;
            bool canMove = !frozen && !presenter.IsOpen && !presenter.Busy && !bookOpen && !leaving;
            player.InputEnabled = canMove;
            presenter.InputAllowed = !frozen && !bookOpen && !presenter.Busy && !leaving && !hud.MemoryShowing;

            UpdateZone();

            Keyboard kb = Keyboard.current;
            Gamepad gp = Gamepad.current;
            bool typing = presenter.IsOpen;
            usableHere = null;
            if (!frozen && !typing && !bookOpen)
            {
                usableHere = script.Usables.FirstOrDefault(u => u.Zone == zone);
                if (usableHere != null) hud.ShowPrompt("E", usableHere.Prompt);
                else hud.HidePrompt();

                bool use = (kb != null && kb.eKey.wasPressedThisFrame) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
                if (use && usableHere != null) Fire(TriggerKind.Use, usableHere.Id);

                bool hint = (kb != null && kb.hKey.wasPressedThisFrame) || (gp != null && gp.buttonNorth.wasPressedThisFrame);
                if (hint && !room.Solved) hud.ShowHint(room.Hint(), 8f);

                if (!room.Solved && !running && Time.time - lastSuccess > 75f)
                {
                    lastSuccess = Time.time;
                    hud.ShowHint(room.Hint(), 8f);
                }
            }
            else hud.HidePrompt();
            hud.SetLegendVisible(!frozen && !typing && !bookOpen);
        }

        private void UpdateZone()
        {
            Vector3 p = player.transform.position;
            RealmZone best = null;
            RealmZone camZone = null;
            foreach (RealmZone z in zones)
            {
                if (z == null || !z.Contains(p)) continue;
                if (!string.IsNullOrEmpty(z.zoneId) && (best == null || z.priority > best.priority)) best = z;
                if (z.cameraIndex >= 0 && (camZone == null || z.priority > camZone.priority)) camZone = z;
            }
            if (camZone != null && cameras != null && cameras.Active != camZone.cameraIndex) cameras.CutTo(camZone.cameraIndex);
            string now = best != null ? best.zoneId : null;
            if (now != zone)
            {
                zone = now;
                if (zone != null)
                {
                    entered.Add(zone);
                    Fire(TriggerKind.EnterZone, zone);
                }
            }
        }

        // ------------------------------------------------------------------ speech

        private void OnSubmitted(string text)
        {
            SpellResult r = room.Speak(text, zone);
            var outcome = new SpellOutcomeView
            {
                Success = r.Accepted,
                Reply = r.Accepted ? string.Empty : r.Message,
                Words = r.Words.Select(w => new SpokenWord(w.English, w.Transliteration, w.Signs, w.Cartouche)).ToList(),
            };

            AudioClip clip = null;
            if (r.Accepted || r.Outcome == Judgement.Refused || r.Outcome == Judgement.NoChange || r.Outcome == Judgement.OutOfReach)
                clip = r.Verb == "name" && r.Words.Count > 0 ? RealmVoice.Find("name." + r.Words[0].Transliteration) : RealmVoice.FindSpell(r.Verb, r.TargetId);
            if (clip != null)
            {
                ankhuVoice.clip = clip;
                ankhuVoice.Play();
                outcome.VoiceSeconds = clip.length;
                if (player != null) player.Speak();
            }

            RealmObjectView targetView = views.FirstOrDefault(v => v.objectId == r.TargetId);
            if (r.Accepted && targetView != null) outcome.Target = targetView.GetComponent<GoldMagic>();

            if (r.Accepted && spellCastSfx != null) sfx.PlayOneShot(spellCastSfx, 0.8f);
            if (!r.Accepted && spellFailSfx != null) sfx.PlayOneShot(spellFailSfx, 0.6f);

            outcome.OnArrive = () =>
            {
                if (r.Accepted)
                {
                    if (spellLandSfx != null) sfx.PlayOneShot(spellLandSfx, 0.9f);
                    lastSuccess = Time.time;
                    foreach (var c in r.Changes)
                    {
                        RealmObjectView v = views.FirstOrDefault(x => x.objectId == c.id);
                        if (v != null) v.SetState(c.to, false);
                    }
                    UpdateHints();
                    if (!string.IsNullOrEmpty(r.Message)) hud.ShowHint(r.Message, 6f);
                    foreach (string ev in r.Events) Fire(TriggerKind.Event, ev);
                    if (room.Solved && !solvedFired)
                    {
                        solvedFired = true;
                        Fire(TriggerKind.Solved, "");
                    }
                }
                else
                {
                    string key = r.Outcome.ToString();
                    if (failuresSeen.Add(key)) Fire(TriggerKind.Failure, key);
                }
            };
            presenter.Resolve(outcome);
        }

        private void UpdateHints()
        {
            // Objects that a known word could change right now shimmer faintly.
            foreach (RealmObjectView v in views)
            {
                GoldMagic g = v.GetComponent<GoldMagic>();
                if (g == null) continue;
                bool live = script.Room.Rules.Any(x => x.Target == v.objectId && x.From.Contains(room.Get(v.objectId)) &&
                                                       x.Requires.All(c => c.Holds(room.State)));
                g.SetHint(live && !room.Solved);
            }
        }

        // ------------------------------------------------------------------ beats

        private void Fire(TriggerKind kind, string key)
        {
            foreach (Beat b in script.Beats)
            {
                if (b.When != kind) continue;
                if (kind != TriggerKind.Start && kind != TriggerKind.Solved && b.Key != key) continue;
                if (b.Once && fired.Contains(b.Id)) continue;
                if (b.RequiresFlags.Any(f => !flags.Contains(f))) continue;
                if (b.ForbidsFlags.Any(f => flags.Contains(f))) continue;
                if (b.Once) fired.Add(b.Id);
                pending.Enqueue(b);
            }
            if (!running && pending.Count > 0) StartCoroutine(RunBeats());
        }

        private IEnumerator RunBeats()
        {
            running = true;
            while (pending.Count > 0)
            {
                Beat b = pending.Dequeue();
                foreach (Step s in b.Steps)
                {
                    // Re-check gates for queued beats, since an earlier beat may have set or cleared a flag.
                    yield return RunStep(s);
                }
            }
            running = false;
        }

        private IEnumerator RunStep(Step s)
        {
            switch (s.Kind)
            {
                case StepKind.Say:
                {
                    while (presenter.Busy) yield return null;
                    AudioClip clip = RealmVoice.Find(s.VoiceKey);
                    hud.ShowLine(s.Who, s.Text);
                    if (clip != null)
                    {
                        AudioSource src = s.Who == Speaker.Ankhu ? ankhuVoice : voice;
                        src.clip = clip;
                        src.Play();
                        if (s.Who == Speaker.Ankhu && player != null) player.Speak();
                    }
                    yield return HoldOrSkip(RealmVoice.HoldSeconds(s.Text, clip));
                    hud.HideLine();
                    yield return new WaitForSeconds(0.18f);
                    break;
                }
                case StepKind.Write:
                    CodexStore.Write(s.Text);
                    yield return new WaitForSeconds(0.35f);
                    break;
                case StepKind.Memory:
                {
                    Texture tex = Resources.Load<Texture2D>("RealmMemories/" + s.Image);
                    hud.HideLine();
                    hud.ShowMemory(tex, s.Who, s.Text);
                    AudioClip clip = RealmVoice.Find(s.VoiceKey);
                    if (clip != null)
                    {
                        AudioSource src = s.Who == Speaker.Ankhu ? ankhuVoice : voice;
                        src.clip = clip;
                        src.Play();
                    }
                    yield return HoldOrSkip(RealmVoice.HoldSeconds(s.Text, clip) + 0.6f);
                    if (!NextIsMemory(s)) { hud.HideMemory(); yield return new WaitForSeconds(1.0f); }
                    break;
                }
                case StepKind.Wait:
                    yield return new WaitForSeconds(s.Value);
                    break;
                case StepKind.Title:
                    yield return hud.Title(s.Text, s.Value);
                    break;
                case StepKind.Control:
                    frozen = s.Value < 0.5f;
                    break;
                case StepKind.Flag:
                    flags.Add(s.Text);
                    break;
                case StepKind.Cue:
                    RunCue(s.Text);
                    break;
                case StepKind.MoveActor:
                {
                    Mark m = marks.FirstOrDefault(x => x.name == s.Image);
                    if (m != null && anpu != null && s.Text == "Anpu") anpu.GoTo(m.transform.position);
                    break;
                }
                case StepKind.NextScene:
                    leaving = true;
                    SceneFader.FadeTo(NextSceneName(), 1.6f, 1.0f);
                    yield return new WaitForSeconds(5f);
                    break;
                case StepKind.Learn:
                    room.Learn(s.Text);
                    CodexStore.Write("word." + s.Text);
                    yield return new WaitForSeconds(0.35f);
                    break;
                case StepKind.SetState:
                {
                    room.Force(s.Text, s.Image);
                    RealmObjectView v = views.FirstOrDefault(x => x.objectId == s.Text);
                    if (v != null) v.SetState(s.Image, false);
                    UpdateHints();
                    break;
                }
                case StepKind.Caption:
                    hud.HideLine();
                    hud.ShowHint(s.Text, RealmVoice.HoldSeconds(s.Text, null) + 0.5f);
                    yield return new WaitForSeconds(RealmVoice.HoldSeconds(s.Text, null));
                    break;
                case StepKind.EndSlice:
                    leaving = true;
                    SceneFader.FadeTo(OpeningSettings.TitleScene, 2.5f, 1.2f);
                    yield return new WaitForSeconds(6f);
                    break;
            }
        }

        private bool NextIsMemory(Step s)
        {
            foreach (Beat b in script.Beats)
            {
                int i = b.Steps.IndexOf(s);
                if (i >= 0) return i + 1 < b.Steps.Count && b.Steps[i + 1].Kind == StepKind.Memory;
            }
            return false;
        }

        /// <summary>Hold a line for its time, but Space, E or the gamepad A button moves on early (after a short guard).</summary>
        private IEnumerator HoldOrSkip(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                Keyboard kb = Keyboard.current;
                Gamepad gp = Gamepad.current;
                bool skip = t > 0.5f && !presenter.IsOpen && ((kb != null && (kb.spaceKey.wasPressedThisFrame)) || (gp != null && gp.buttonSouth.wasPressedThisFrame));
                if (skip) { voice.Stop(); ankhuVoice.Stop(); break; }
                yield return null;
            }
        }

        private void RunCue(string name)
        {
            foreach (CueHook h in cues.Where(c => c.cue == name))
            {
                foreach (GameObject g in h.activate) if (g != null) g.SetActive(true);
                foreach (GameObject g in h.deactivate) if (g != null) g.SetActive(false);
                if (h.animator != null && !string.IsNullOrEmpty(h.trigger)) h.animator.SetTrigger(h.trigger);
            }
        }

        /// <summary>The next enabled scene in Build Settings, or the title when this is the last.</summary>
        public static string NextSceneName()
        {
            int me = SceneManager.GetActiveScene().buildIndex;
            if (me >= 0 && me + 1 < SceneManager.sceneCountInBuildSettings)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(me + 1);
                string n = System.IO.Path.GetFileNameWithoutExtension(path);
                if (n.StartsWith("Realm_")) return n;
            }
            return OpeningSettings.TitleScene;
        }
    }
}
