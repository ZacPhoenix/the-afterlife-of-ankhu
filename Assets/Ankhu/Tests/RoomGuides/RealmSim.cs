using System;
using System.Collections.Generic;
using System.Linq;
using Ankhu.Opening;
using Ankhu.Realms.Core;

namespace Ankhu.Tests.RoomGuides
{
    /// <summary>
    /// The RealmSceneDirector's story logic without the engine: zones entered, spells judged by the real RealmRoom,
    /// deeds and E-uses, beats queued and run in order (Once, RequiresFlags, ForbidsFlags), flags, learned verbs,
    /// set states, codex writes, flashback cues, freeze and free, and the NextScene / EndSlice exits.
    /// Timing, camera, glyph flight and audio are left out. Mirrors RealmSceneDirector.cs line for line where it
    /// decides anything, so a fault found here is a fault in the scene script or the director's rules.
    /// </summary>
    public sealed class RealmSim
    {
        public SceneScript Script { get; }
        public RealmRoom Room { get; }
        public string Zone { get; private set; }
        public bool Frozen { get; private set; }
        public bool Leaving { get; private set; }
        public string ExitTo { get; private set; }
        public readonly HashSet<string> Fired = new HashSet<string>();
        /// <summary>Every beat that ran, Once or not, in order.</summary>
        public readonly List<string> Ran = new List<string>();
        public readonly HashSet<string> Flags = new HashSet<string>();
        public readonly HashSet<string> Films = new HashSet<string>();
        public readonly HashSet<string> Codex = new HashSet<string>();
        public readonly List<string> Log = new List<string>();
        /// <summary>The last mark a Move step sent Anpu to, or null.</summary>
        public string AnpuMark { get; private set; }
        private readonly HashSet<string> failuresSeen = new HashSet<string>();
        private readonly Queue<Beat> pending = new Queue<Beat>();
        private readonly HashSet<string> done;
        private bool solvedFired;

        /// <param name="progress">Wernes rooms already done, in the order they were done (the last is "back").</param>
        public RealmSim(SceneScript script, IEnumerable<string> progress, IEnumerable<string> codex)
        {
            Script = script ?? throw new ArgumentNullException(nameof(script));
            Room = new RealmRoom(script.Room);
            var order = (progress ?? Enumerable.Empty<string>()).ToList();
            if (script.SceneName == PlayOrder.TerraceScene) order.Clear();
            done = new HashSet<string>(order);
            foreach (string l in done.OrderBy(x => x)) Flags.Add("progress.done." + l);
            if (order.Count > 0) Flags.Add("progress.back." + order[order.Count - 1]);
            Flags.Add("progress.count." + done.Count);
            if (WernesProgress.Rooms.All(r => done.Contains(WernesProgress.LetterOf(r)))) Flags.Add("progress.all");
            foreach (string c in script.CodexOnEntry) Codex.Add(c);
            if (codex != null) foreach (string c in codex) Codex.Add(c);
            Fire(TriggerKind.Start, string.Empty);
            Drain();
        }

        /// <summary>The usable whose prompt shows now, as the director's Update picks it.</summary>
        public Usable UsableHere()
        {
            if (Frozen || Leaving) return null;
            return Script.Usables.FirstOrDefault(u => u.Zone == Zone && u.When.All(c => c.Holds(Room.State)));
        }

        public bool CanType => !Frozen && !Leaving;

        public void Enter(string zoneId)
        {
            if (zoneId == Zone) return;
            Zone = zoneId;
            Log.Add("enter " + zoneId);
            if (Zone != null && !Leaving) Fire(TriggerKind.EnterZone, Zone);
            Drain();
        }

        /// <summary>Mirrors OnSubmitted, including the deed and wait shortcut for rooms with a clock.</summary>
        public SpellResult Say(string text)
        {
            List<string> toks = RealmRoom.Tokenize(text);
            SpellResult r;
            if (Room.Def.Tick != null && toks.Count > 0 && (toks[0] == "wait" || toks[0] == "watch" || toks[0] == "rest" || Room.IsDeed(text)))
            {
                r = DoDirect(text);
            }
            else
            {
                r = Room.Speak(text, Zone);
                foreach (string ev in r.Events) Fire(TriggerKind.Event, ev);
                if (r.Accepted) CheckSolved();
                else
                {
                    if (failuresSeen.Add(r.Outcome.ToString())) Fire(TriggerKind.Failure, r.Outcome.ToString());
                    CheckSolved();
                }
            }
            Log.Add($"say \"{text}\" @{Zone} -> {r.Outcome}: {r.Message}");
            Drain();
            return r;
        }

        /// <summary>Press E: the deed if any, then the Use trigger, as Update does.</summary>
        public SpellResult Use(Usable u)
        {
            SpellResult r = null;
            if (!string.IsNullOrEmpty(u.Deed)) r = DoDirect(u.Deed);
            Fire(TriggerKind.Use, u.Id);
            Log.Add($"use {u.Id} @{Zone}" + (r != null ? $" deed -> {r.Outcome}: {r.Message}" : string.Empty));
            Drain();
            return r;
        }

        public SpellResult Wait()
        {
            SpellResult r = Room.Advance();
            CheckSolved();
            Drain();
            return r;
        }

        private SpellResult DoDirect(string text)
        {
            SpellResult r = Room.Speak(text, Zone);
            foreach (string ev in r.Events) Fire(TriggerKind.Event, ev);
            CheckSolved();
            return r;
        }

        private void CheckSolved()
        {
            if (Room.Solved && !solvedFired)
            {
                solvedFired = true;
                Fire(TriggerKind.Solved, string.Empty);
            }
        }

        /// <summary>Same gate as RealmSceneDirector.Fire.</summary>
        public void Fire(TriggerKind kind, string key)
        {
            foreach (Beat b in Script.Beats)
            {
                if (b.When != kind) continue;
                if (kind != TriggerKind.Start && kind != TriggerKind.Solved && b.Key != key) continue;
                if (b.Once && Fired.Contains(b.Id)) continue;
                if (b.RequiresFlags.Any(f => !Flags.Contains(f))) continue;
                if (b.ForbidsFlags.Any(f => Flags.Contains(f))) continue;
                if (b.Once) Fired.Add(b.Id);
                pending.Enqueue(b);
            }
        }

        private void Drain()
        {
            int guard = 0;
            while (pending.Count > 0 && guard++ < 500)
            {
                Beat b = pending.Dequeue();
                Ran.Add(b.Id);
                Log.Add("beat " + b.Id);
                foreach (Step s in b.Steps) Run(s);
            }
        }

        private void Run(Step s)
        {
            switch (s.Kind)
            {
                case StepKind.Cue:
                    if (s.Text.StartsWith("flashback:", StringComparison.Ordinal)) Films.Add(s.Text.Substring("flashback:".Length));
                    else if (s.Text.StartsWith("codex:", StringComparison.Ordinal))
                    {
                        string cid = s.Text.Substring("codex:".Length);
                        if (cid.StartsWith("rev.") || cid.StartsWith("ep.") || cid.StartsWith("q.") || cid == "hall") Codex.Add(cid);
                        else Write(cid);
                    }
                    break;
                case StepKind.Write: Write(s.Text); break;
                case StepKind.Control: Frozen = s.Value < 0.5f; break;
                case StepKind.Flag: Flags.Add(s.Text); break;
                case StepKind.Learn: Room.Learn(s.Text); Write("word." + s.Text); break;
                case StepKind.SetState: Room.Force(s.Text, s.Image); break;
                case StepKind.MoveActor: if (s.Text == "Anpu") AnpuMark = s.Image; break;
                case StepKind.NextScene:
                    if (Leaving) break;
                    Leaving = true;
                    string l = WernesProgress.LetterOf(Script.SceneName);
                    if (l != null) done.Add(l);
                    ExitTo = ExitOf(Script.SceneName, done);
                    Log.Add("exit -> " + ExitTo);
                    break;
                case StepKind.EndSlice:
                    if (Leaving) break;
                    Leaving = true;
                    ExitTo = "title";
                    Log.Add("exit -> title");
                    break;
            }
        }

        /// <summary>A codex write lands only when the catalog has the entry, as CodexStore.Write does.</summary>
        private void Write(string id)
        {
            if (CodexCatalog.Get(id) == null) { Log.Add("codex write dropped, no catalog entry: " + id); return; }
            Codex.Add(id);
        }

        /// <summary>PlayOrder.ExitOf, with Wernes progress given rather than read from PlayerPrefs, and the hub's rooms assumed built.</summary>
        public static string ExitOf(string scene, HashSet<string> doneLetters)
        {
            if (WernesProgress.IsRoom(scene)) return PlayOrder.HubScene;
            if (scene == PlayOrder.HubScene)
            {
                string room = WernesProgress.Rooms.FirstOrDefault(r => !doneLetters.Contains(WernesProgress.LetterOf(r)));
                return room ?? PlayOrder.FinaleScene;
            }
            int i = PlayOrder.IndexOf(scene);
            if (i < 0 || i + 1 >= PlayOrder.Chain.Length) return "title";
            string next = PlayOrder.Chain[i + 1];
            if (next == PlayOrder.BorrowedDawnSlot) return PlayOrder.BorrowedDawnPainted;
            return next;
        }
    }
}
