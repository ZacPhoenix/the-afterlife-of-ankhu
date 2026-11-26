using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ankhu.Opening;
using Ankhu.Realms.Core;
using Ankhu.Realms.Core.Content;
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
        /// <summary>The guided first spell (Zac 2026-11-26): the word the player is being walked through typing, or null.</summary>
        private string coachWord;
        private bool solvedFired;
        private bool leaving;
        private float lastSuccess;
        private AudioSource voice;
        /// <summary>Zac 2026-11-25: Anpu speaks through his own source with a gentle low-end roll-off (VoiceTone).</summary>
        private AudioSource anpuVoice;
        private AudioSource ankhuVoice;
        private AudioSource sfx;
        private AudioSource amb;
        private Usable usableHere;

        public RealmRoom Room => room;
        /// <summary>Raised with a beat's id as it starts to run (room guide E2E tests listen, nothing else does).</summary>
        public static event Action<string> BeatStarted;
        public string Zone => zone;
        public static RealmSceneDirector Current { get; private set; }

        private void Awake()
        {
            Current = this;
            voice = MakeSource();
            ankhuVoice = MakeSource();
            anpuVoice = AnpuSource(transform);
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
            CodexBook.WordChosen -= OnWordChosen;
            CodexFamiliar.HeldUntilGift = false;
            TalkMark.ClearAll();
        }

        // ------------------------------------------------------------------ the roll

        /// <summary>
        /// A word chosen on the roll drops into the speech bubble. SpellPresenter.InsertWord is called by name so
        /// this compiles whether or not the bubble's method has landed yet.
        /// </summary>
        private void OnWordChosen(string word)
        {
            if (presenter == null || string.IsNullOrEmpty(word)) return;
            presenter.SendMessage("InsertWord", word, SendMessageOptions.DontRequireReceiver);
        }

        /// <summary>Write an entry: keystones stop the scene and his hand writes them on the open roll, the rest toast.</summary>
        private IEnumerator WriteCodex(string id)
        {
            CodexEntry e = CodexCatalog.Get(id);
            if (e != null && !CodexStore.Has(id) && CodexRollCatalog.IsPresented(e))
            {
                while (presenter.Busy) yield return null;
                hud.HideLine();
                yield return CodexBook.Present(id);
                yield return new WaitForSeconds(0.25f);
                yield break;
            }
            CodexStore.Write(id);
            yield return new WaitForSeconds(0.35f);
        }

        /// <summary>Margin questions for this scene go on the roll once he has the roll.</summary>
        private void AskQuestions()
        {
            if (!CodexStore.Has("lore.codex")) return;
            foreach (CodexQuestion q in CodexRollCatalog.QuestionsFor(script.Title))
            {
                if (!CodexStore.Journal.WasAsked(q.Id)) CodexStore.Ask(q.Id);
            }
        }

        private float questionCheck;
        private bool wasBookOpen;

        /// <summary>A margin question is answered, in green, once the room shows what it asked.</summary>
        private void AnswerQuestions()
        {
            questionCheck -= Time.deltaTime;
            if (questionCheck > 0f) return;
            questionCheck = 0.5f;
            foreach (CodexQuestion q in CodexRollCatalog.QuestionsFor(script.Title))
            {
                if (!CodexStore.Journal.WasAsked(q.Id) || CodexStore.Journal.IsAnswered(q.Id)) continue;
                if (q.When.Length > 0 && q.When.All(c => c.Holds(room.State))) CodexStore.Answer(q.Id);
            }
        }

        /// <summary>A hint is shown and Anpu's note goes in the margin, so it can be read again.</summary>
        private void ShowHint(string text, float seconds)
        {
            hud.ShowHint(text, seconds);
            if (!string.IsNullOrEmpty(text) && CodexStore.Has("lore.codex")) CodexStore.Note(CodexHand.Anpu, script.Title, text);
        }

        private Transform AnpuTransform()
        {
            if (anpu != null) return anpu.transform;
            ActorRef a = actors.FirstOrDefault(x => x.name == "Anpu" && x.transform != null);
            return a != null ? a.transform : null;
        }

        private Animator AnpuAnimator()
        {
            if (anpu != null && anpu.animator != null) return anpu.animator;
            ActorRef a = actors.FirstOrDefault(x => x.name == "Anpu");
            if (a != null && a.animator != null) return a.animator;
            Transform t = AnpuTransform();
            return t != null ? t.GetComponentInChildren<Animator>() : null;
        }

        /// <summary>A child GameObject with one AudioSource and Anpu's VoiceTone, since a filter acts on the source beside it.</summary>
        public static AudioSource AnpuSource(Transform parent)
        {
            var go = new GameObject("AnpuVoice");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            VoiceTone.AddAnpu(go);
            return s;
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
            // Wernes progress: the terrace starts the realm fresh, and every scene sees what is done as
            // progress.* flags, so the Memory House hub can light its panels and pick Anpu's return lines.
            string activeName = SceneManager.GetActiveScene().name;
            if (activeName == PlayOrder.TerraceScene) WernesProgress.Reset();
            foreach (string f in WernesProgress.Flags()) flags.Add("progress." + f);
            hud = RealmHud.Create();
            CodexStore.EnsureEntries(script.CodexOnEntry);
            CodexBook.Ensure();
            presenter = SpellPresenter.Create(() => cameras != null ? cameras.ActiveCamera : Camera.main, playerHead != null ? playerHead : player.transform);
            presenter.Submitted += OnSubmitted;
            presenter.ResolveTarget = ResolveTypedTarget;
            presenter.LiveTargetChanged += OnLiveTarget;
            presenter.KnownWords = () => CodexStore.Journal.Entries(CodexKind.Word, CodexKind.Name)
                .Select(CodexRollSheets.SpeakableWord).Where(w => !string.IsNullOrEmpty(w));
            CodexBook.WordChosen += OnWordChosen;
            AskQuestions();

            foreach (RealmObjectView v in views)
            {
                string s = room.Get(v.objectId);
                if (s != null) v.SetState(s, true);
            }
            PlaceAnpu();
            LiveLights();
            UpdateHints();
            lastSuccess = Time.time;
            SceneFader.FadeIn(1.2f);
            // Zac 2026-11-26: the roll does not float beside him before Anpu has handed it over.
            if (!CodexStore.Has("lore.codex") && script.Beats.Any(b => b.When == TriggerKind.Start && b.Steps.Any(st => st.Kind == StepKind.Cue && st.Text == "anpu_offers_codex")))
                CodexFamiliar.HeldUntilGift = true;
            Fire(TriggerKind.Start, "");
        }

        /// <summary>
        /// Anpu where the scene says he is (Zac 2026-11-26): waiting where he said he would wait, or not in a room he
        /// never entered. His lines still play as a voice when he is absent.
        /// </summary>
        private void PlaceAnpu()
        {
            if (anpu == null) return;
            switch (script.Anpu)
            {
                case AnpuPresence.Absent:
                    anpu.gameObject.SetActive(false);
                    break;
                case AnpuPresence.Waits:
                    anpu.gameObject.SetActive(true);
                    anpu.Hold();
                    break;
            }
        }

        /// <summary>Flame and fire light breathes in real time, whatever state the puzzle holds (Zac 2026-11-26).</summary>
        private void LiveLights()
        {
            foreach (string id in script.LiveLights)
                foreach (RealmObjectView v in views)
                {
                    if (v == null || v.objectId != id) continue;
                    foreach (Light l in v.GetComponentsInChildren<Light>(true))
                        if (l.GetComponent<LightBreath>() == null && l.GetComponent<FlameFlicker>() == null) l.gameObject.AddComponent<LightBreath>();
                }
        }

        // ------------------------------------------------------------------ frame

        private void Update()
        {
            if (room == null) return;
            if (amb.clip != null) amb.volume = Mathf.MoveTowards(amb.volume, ambienceVolume * OpeningSettings.Music * (voice.isPlaying || ankhuVoice.isPlaying ? 0.6f : 1f), Time.deltaTime * 0.5f);

            bool bookOpen = CodexBook.IsOpen;
            // A hint already on screen moves into the roll's margin; clear it so it does not sit over the roll.
            if (bookOpen && !wasBookOpen) { hud.ShowHint(string.Empty, 0f); hud.HideHintScroll(); }
            wasBookOpen = bookOpen;
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
                usableHere = script.Usables.FirstOrDefault(u => u.Zone == zone && u.When.All(c => c.Holds(room.State)));
                // Zac 2026-11-25, prompts pick B: a glint when near, the keycap and verb only when at the thing.
                if (coachWord != null && presenter.InputAllowed) hud.ShowPrompt("ENTER", "Begin speaking", playerHead != null ? playerHead.position + Vector3.up * 0.5f : (Vector3?)null);
                else if (usableHere != null) hud.ShowPrompt(gp != null && kb == null ? "A" : "E", usableHere.Prompt, UsableMark(usableHere.Zone));
                else hud.HidePrompt();
                hud.ShowGlint(usableHere == null ? NearUsableMark() : null);

                bool use = (kb != null && kb.eKey.wasPressedThisFrame) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
                if (use && usableHere != null)
                {
                    if (!string.IsNullOrEmpty(usableHere.Deed)) DoDirect(usableHere.Deed);
                    Fire(TriggerKind.Use, usableHere.Id);
                }

                bool hint = (kb != null && kb.hKey.wasPressedThisFrame) || (gp != null && gp.buttonNorth.wasPressedThisFrame);
                if (hint && !room.Solved)
                {
                    // Zac 2026-11-25, hints pick B: H breaks the next seal of nudge, direction, answer.
                    string[] tiers = RealmHintSolver.Tiers(room);
                    hud.ShowHintTiers(tiers);
                    if (tiers.Length > 0 && CodexStore.Has("lore.codex")) CodexStore.Note(CodexHand.Anpu, script.Title, tiers[0]);
                }

                if (!room.Solved && !running)
                    foreach (Beat ib in script.Beats)
                        if (ib.When == TriggerKind.Idle && !fired.Contains(ib.Id) && Time.time - lastSuccess > ib.Value)
                        { fired.Add(ib.Id); pending.Enqueue(ib); StartCoroutine(RunBeats()); break; }

                if (!room.Solved && !running && Time.time - lastSuccess > 75f)
                {
                    lastSuccess = Time.time;
                    ShowHint(room.Hint(), 8f);
                }
            }
            else { hud.HidePrompt(); hud.ShowGlint(null); }
            hud.SetLegendVisible(!frozen && !typing && !bookOpen);
            hud.SetRoomState(room);
            AnswerQuestions();
        }

        private const float NearPad = 2.6f;

        private RealmZone ZoneById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (RealmZone z in zones) if (z != null && z.zoneId == id) return z;
            return null;
        }

        /// <summary>Where a usable's glint and prompt hang: the upper middle of its zone.</summary>
        private Vector3? UsableMark(string zoneId)
        {
            RealmZone z = ZoneById(zoneId);
            if (z == null) return null;
            Bounds b = z.WorldBounds;
            return b.center + Vector3.up * (b.extents.y * 0.35f);
        }

        /// <summary>The nearest usable whose zone Ankhu is close to but not inside, or null.</summary>
        private Vector3? NearUsableMark()
        {
            Vector3 p = player.transform.position;
            Vector3? best = null;
            float bestD = float.MaxValue;
            foreach (Usable u in script.Usables)
            {
                if (u.Zone == zone || !u.When.All(c => c.Holds(room.State))) continue;
                RealmZone z = ZoneById(u.Zone);
                if (z == null) continue;
                Bounds b = z.WorldBounds;
                b.Expand(NearPad * 2f);
                if (!b.Contains(p)) continue;
                float d = (z.WorldBounds.ClosestPoint(p) - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = UsableMark(u.Zone); }
            }
            return best;
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

        /// <summary>A deed or a wait: judged and applied at once, with no glyphs in the air.</summary>
        private void DoDirect(string text)
        {
            SpellResult r = room.Speak(text, zone);
            ApplyChanges(r);
            if (!string.IsNullOrEmpty(r.Message)) hud.ShowHint(r.Message, 5f);
            foreach (string ev in r.Events) Fire(TriggerKind.Event, ev);
            if (r.Accepted) lastSuccess = Time.time;
            CheckSolved();
        }

        private void ApplyChanges(SpellResult r)
        {
            foreach (var c in r.Changes)
            {
                RealmObjectView v = views.FirstOrDefault(x => x.objectId == c.id);
                if (v != null) v.SetState(c.to, false);
            }
            if (r.Changes.Count > 0) UpdateHints();
        }

        private void CheckSolved()
        {
            if (room.Solved && !solvedFired)
            {
                solvedFired = true;
                Fire(TriggerKind.Solved, "");
            }
        }

        private void OnSubmitted(string text)
        {
            List<string> toks = RealmRoom.Tokenize(text);
            if (room.Def.Tick != null && toks.Count > 0 && (toks[0] == "wait" || toks[0] == "watch" || toks[0] == "rest" || room.IsDeed(text)))
            {
                presenter.Resolve(new SpellOutcomeView { Success = true, Reply = string.Empty, Words = new List<SpokenWord>() });
                DoDirect(text);
                return;
            }
            SpellResult r = room.Speak(text, zone);
            if (coachWord != null && r.Accepted) { coachWord = null; presenter.Coach = null; }
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
                    ApplyChanges(r);
                    foreach (string ev in r.Events) Fire(TriggerKind.Event, ev);
                    string key = r.Outcome.ToString();
                    if (failuresSeen.Add(key)) Fire(TriggerKind.Failure, key);
                    CheckSolved();
                }
            };
            presenter.Resolve(outcome);
        }

        /// <summary>Zac 2026-11-25, spell input pick A: a typed phrase that names a room object (by alias or name).</summary>
        private bool ResolveTypedTarget(string phrase, out string id, out WordGloss gloss)
        {
            id = null;
            gloss = null;
            if (script == null || script.Room == null) return false;
            foreach (RoomObject o in script.Room.Objects)
            {
                bool hit = o.Name != null && string.Equals(o.Name, phrase, StringComparison.OrdinalIgnoreCase);
                if (!hit && o.Aliases != null) hit = Array.IndexOf(o.Aliases, phrase) >= 0;
                if (!hit && o.Refuses != null) hit = o.Refuses.ContainsKey(phrase);
                if (!hit) continue;
                id = o.Id;
                gloss = o.Gloss;
                return true;
            }
            return false;
        }

        private string focusedId;

        /// <summary>The object the typed words name glows while the player types, and stops when typing ends.</summary>
        private void OnLiveTarget(string id)
        {
            if (focusedId == id) return;
            SetFocus(focusedId, false);
            focusedId = id;
            SetFocus(focusedId, true);
        }

        private void SetFocus(string id, bool on)
        {
            if (string.IsNullOrEmpty(id)) return;
            RealmObjectView v = views.FirstOrDefault(x => x != null && x.objectId == id);
            GoldMagic g = v != null ? v.GetComponent<GoldMagic>() : null;
            if (g != null) g.SetFocus(on);
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

        /// <summary>Diagnostics only (FlashbackPlayerCheck): fire a trigger as if the room raised it.</summary>
        public void FireForCheck(TriggerKind kind, string key = "") => Fire(kind, key);

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
                try { BeatStarted?.Invoke(b.Id); } catch (Exception e) { Debug.LogException(e, this); }
                foreach (Step s in b.Steps)
                {
                    // Re-check gates for queued beats, since an earlier beat may have set or cleared a flag.
                    yield return RunStep(s);
                }
            }
            running = false;
        }

        /// <summary>
        /// Full-screen memory film from the flashback thread: Ankhu.Memories.MemoryFlashback.Play(id, onDone).
        /// Found by reflection so this assembly compiles before or without that one. Missing means a no-op.
        /// </summary>
        private IEnumerator PlayFlashback(string id)
        {
            Type t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Ankhu.Memories.MemoryFlashback", false)).FirstOrDefault(x => x != null);
            var play = t?.GetMethod("Play", new[] { typeof(string), typeof(Action) });
            if (play == null) { Debug.LogWarning("[Realms] MemoryFlashback not found, skipping " + id); yield break; }
            // Zac 2026-11-24, pick 7: the Codex paints the memory first, then the film grows out of the page.
            CodexMemoryPage page = CodexMemoryPage.Open(id);
            if (page != null)
            {
                hud.HideLine();
                yield return page.PaintIn();
                yield return page.GrowToFilm();
            }
            bool done = false;
            play.Invoke(null, new object[] { id, (Action)(() => done = true) });
            if (page != null) page.StartCoroutine(page.HandOff(id));
            float guard = Time.unscaledTime + 120f;
            while (!done && Time.unscaledTime < guard) yield return null;
        }

        private IEnumerator RunStep(Step s)
        {
            if (s.Kind == StepKind.Cue && s.Text.StartsWith("flashback:", StringComparison.Ordinal))
            {
                yield return PlayFlashback(s.Text.Substring("flashback:".Length));
                yield break;
            }
            if (s.Kind == StepKind.Cue && s.Text.StartsWith("codex:", StringComparison.Ordinal))
            {
                // Step.Cue("codex:rev.father") strikes and rewrites a line on screen, "codex:ep.hori" grows his name.
                string id = s.Text.Substring("codex:".Length);
                while (presenter.Busy) yield return null;
                hud.HideLine();
                if (id == "hall")
                {
                    // The ending: he reads his roll aloud before the Assessors, then the last line is written.
                    yield return CodexBook.ReadInHall(null);
                    int truths = CodexBook.HallResults.Count(l => l.RevisionId != null && l.ReadTrueLine);
                    flags.Add("hall.read." + CodexBook.HallResults.Count);
                    flags.Add("hall.true." + truths);
                    yield return CodexBook.WriteLastLine();
                }
                else if (id.StartsWith("rev.", StringComparison.Ordinal)) yield return CodexBook.PresentRevision(id);
                else if (id.StartsWith("ep.", StringComparison.Ordinal)) yield return CodexBook.PresentEpithet(id);
                else if (id.StartsWith("q.", StringComparison.Ordinal))
                {
                    // A question no room state can answer (q.fieldwaits.face after Memory A).
                    if (!CodexStore.Journal.WasAsked(id)) CodexStore.Ask(id);
                    CodexStore.Answer(id);
                }
                else yield return WriteCodex(id);
                yield break;
            }
            if (s.Kind == StepKind.Cue && s.Text == "anpu_offers_codex")
            {
                // The gift: the roll forms above Anpu's palm and floats into Ankhu's hands.
                RunCue(s.Text);
                hud.HideLine();
                yield return CodexGift.Play(AnpuTransform(), AnpuAnimator(), player != null ? player.transform : null, playerAnimator);
                CodexFamiliar.HeldUntilGift = false;
                yield break;
            }
            if (s.Kind == StepKind.Cue && s.Text.StartsWith(WernesScripts.TeachSpeakCue, StringComparison.Ordinal))
            {
                // Zac 2026-11-26: walk the first spell through. Enter is shown, then the name in grey letters to type over.
                coachWord = s.Text.Substring(WernesScripts.TeachSpeakCue.Length);
                presenter.Coach = coachWord;
                yield break;
            }
            switch (s.Kind)
            {
                case StepKind.Say:
                {
                    while (presenter.Busy) yield return null;
                    AudioClip clip = RealmVoice.Find(s.VoiceKey);
                    // Zac 2026-11-26: Ankhu turns to Anpu while they talk, and a figure that speaks shows sound rings.
                    Transform anpuT = AnpuTransform();
                    if (player != null && anpuT != null && anpuT.gameObject.activeInHierarchy && (s.Who == Speaker.Anpu || s.Who == Speaker.Ankhu))
                        player.FaceToward = anpuT.position;
                    bool figure = s.Who == Speaker.Announcer || s.Who == Speaker.Guardian || s.Who == Speaker.Keeper;
                    if (figure) TalkMark.Set(s.Who, true);
                    hud.ShowLine(s.Who, s.Text);
                    if (clip != null)
                    {
                        AudioSource src = s.Who == Speaker.Ankhu ? ankhuVoice : s.Who == Speaker.Anpu ? anpuVoice : voice;
                        src.clip = clip;
                        src.Play();
                        if (s.Who == Speaker.Ankhu && player != null) player.Speak();
                    }
                    yield return HoldOrSkip(RealmVoice.HoldSeconds(s.Text, clip));
                    if (figure) TalkMark.Set(s.Who, false);
                    hud.HideLine();
                    yield return new WaitForSeconds(0.18f);
                    break;
                }
                case StepKind.Write:
                    yield return WriteCodex(s.Text);
                    if (s.Text == "lore.codex") AskQuestions();
                    break;
                case StepKind.Memory:
                {
                    Texture tex = Resources.Load<Texture2D>("RealmMemories/" + s.Image);
                    hud.HideLine();
                    hud.ShowMemory(tex, s.Who, s.Text);
                    AudioClip clip = RealmVoice.Find(s.VoiceKey);
                    if (clip != null)
                    {
                        AudioSource src = s.Who == Speaker.Ankhu ? ankhuVoice : s.Who == Speaker.Anpu ? anpuVoice : voice;
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
                    if (!frozen && player != null) player.FaceToward = null;
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
                    WernesProgress.MarkDone(SceneManager.GetActiveScene().name);
                    SceneFader.FadeTo(NextSceneName(), 1.6f, 1.0f);
                    yield return new WaitForSeconds(5f);
                    break;
                case StepKind.Learn:
                    room.Learn(s.Text);
                    yield return WriteCodex("word." + s.Text);
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
            // Zac 2026-11-24: through the First Gate, Anpu takes Ankhu down into his teaching rooms first.
            if (SceneManager.GetActiveScene().name == OpeningSettings.FirstGateScene
                && Application.CanStreamedLevelBeLoaded(OpeningSettings.FirstTeachScene))
            {
                return PlayOrder.PaintedOf(OpeningSettings.FirstTeachScene);
            }
            // Zac 2026-11-24: scenes in the play-order chain follow it, so Build Settings order cannot reroute them.
            string active = SceneManager.GetActiveScene().name;
            if (PlayOrder.Contains(active)) return PlayOrder.ExitOf(active);
            int me = SceneManager.GetActiveScene().buildIndex;
            if (me >= 0 && me + 1 < SceneManager.sceneCountInBuildSettings)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(me + 1);
                string n = System.IO.Path.GetFileNameWithoutExtension(path);
                if (n.StartsWith("Realm_") || n.StartsWith("Wernes_")) return n;
            }
            return OpeningSettings.TitleScene;
        }
    }
}
