using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ankhu.Memories;
using Ankhu.Opening;
using Ankhu.Realms;
using Ankhu.Realms.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ankhu.Tests.RoomGuides
{
    /// <summary>
    /// Plays a realm e2e block in the real scene: the real RealmSceneDirector, SpellPresenter bubble, beats,
    /// codex roll, memory films and scene exit. Ankhu is placed inside each zone (the guide says where he walks),
    /// words are put in the bubble with InsertWord and submitted as Enter would, E-uses run the director's own
    /// deed and Use trigger, and presented codex moments are closed as soon as the pen lifts.
    /// </summary>
    public sealed class PlayRunner
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        public static float TimeScale = 3f;
        public static float IdleTimeout = 240f;

        private readonly GuideScript g;
        private readonly ScriptResult res;
        private RealmSceneDirector d;
        private readonly HashSet<string> films = new HashSet<string>();
        private readonly HashSet<string> ran = new HashSet<string>();
        // Held from the start so a room that has already faded to the next scene can still be asked what it did.
        private RealmRoom room;
        private HashSet<string> firedSet, flagSet;
        private string startScene;
        private Vector3 anpuStart;

        private readonly bool chained;

        /// <param name="chained">Full chain run: the previous room's exit has already loaded this scene, and the
        /// codex and Wernes progress carry over as a player's would.</param>
        public PlayRunner(GuideScript script, bool chained = false)
        {
            g = script;
            this.chained = chained;
            res = new ScriptResult { Script = g, Mode = "play" };
        }

        public ScriptResult Result => res;

        public IEnumerator Run()
        {
            if (g.Engine != "realm") { res.Skipped = true; res.Pass = true; res.Summary = "engine " + g.Engine; yield break; }
            if (!Application.CanStreamedLevelBeLoaded(g.Scene)) { res.Summary = g.Scene + " is not in Build Settings"; yield break; }

            if (chained && GuideScenes.Norm(SceneManager.GetActiveScene().name) != GuideScenes.Norm(g.Scene))
            {
                res.Summary = $"the chain arrived in {SceneManager.GetActiveScene().name}, not {g.Scene}";
                yield break;
            }
            // Wernes progress as the guide says, the last letter being the room just finished.
            if (chained) { }
            else if (g.Progress.Count == 0) { PlayerPrefs.DeleteKey(WernesProgress.PrefsKey); PlayerPrefs.DeleteKey(WernesProgress.LastKey); }
            else
            {
                PlayerPrefs.SetString(WernesProgress.PrefsKey, string.Join(",", g.Progress.Distinct().OrderBy(x => x)));
                PlayerPrefs.SetString(WernesProgress.LastKey, g.Progress.Last());
            }
            if (!chained) CodexStore.Reset();
            if (g.Codex.Count > 0) CodexStore.EnsureEntries(g.Codex.ToArray());
            PlayerPrefs.Save();
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();

            MemoryFlashback.Started += OnFilm;
            RealmSceneDirector.BeatStarted += OnBeat;
            float oldScale = Time.timeScale;
            Time.timeScale = TimeScale;
            try
            {
                if (!chained) yield return SceneManager.LoadSceneAsync(g.Scene, LoadSceneMode.Single);
                startScene = SceneManager.GetActiveScene().name;
                float guard = Time.realtimeSinceStartup + 20f;
                while ((d = RealmSceneDirector.Current) == null || d.Room == null)
                {
                    if (Time.realtimeSinceStartup > guard) { res.Summary = "no RealmSceneDirector came up in " + g.Scene; yield break; }
                    yield return null;
                }
                room = d.Room;
                firedSet = Field<HashSet<string>>("fired");
                flagSet = Field<HashSet<string>>("flags");
                yield return Idle();
                if (d.anpu != null) anpuStart = d.anpu.transform.position;

                foreach (GuideLine line in g.Lines)
                {
                    var lr = new LineResult { Line = line };
                    res.Lines.Add(lr);
                    yield return Step(line, lr);
                    if (!lr.Pass) break;
                }
                res.Pass = res.Lines.Count > 0 && res.Lines.All(l => l.Pass);
                res.Summary = res.Pass ? $"{res.Lines.Count} lines" : "stopped at line " + res.Lines.Last().Line.LineNo;
            }
            finally
            {
                MemoryFlashback.Started -= OnFilm;
                RealmSceneDirector.BeatStarted -= OnBeat;
                Time.timeScale = oldScale;
            }
        }

        private void OnFilm(string id) { films.Add(id); res.Trace.Add("film " + id); }
        private void OnBeat(string id) { ran.Add(id); res.Trace.Add("beat " + id); }

        private T Field<T>(string name)
        {
            FieldInfo f = typeof(RealmSceneDirector).GetField(name, F);
            return f == null ? default : (T)f.GetValue(d);
        }

        private SpellPresenter Presenter => Field<SpellPresenter>("presenter");
        private bool Running => Field<bool>("running");
        private int Pending => Field<Queue<Beat>>("pending")?.Count ?? 0;
        private bool Frozen => Field<bool>("frozen");
        private bool Leaving => Field<bool>("leaving");

        private bool Busy()
        {
            SpellPresenter p = Presenter;
            return Running || Pending > 0 || (p != null && p.Busy) || MemoryFlashback.IsPlaying || CodexBook.IsOpen || Riding();
        }

        private static readonly FieldInfo RideRun = typeof(RealmRide).GetField("run", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>A ride (beam, bridge, door) carries Ankhu along its marks in real time. Wait for it to set him down.</summary>
        private static bool Riding()
        {
            if (RideRun == null) return false;
            foreach (RealmRide r in UnityEngine.Object.FindObjectsByType<RealmRide>(FindObjectsSortMode.None))
                if (RideRun.GetValue(r) != null) return true;
            return false;
        }

        /// <summary>Waits for beats, films, the bubble and the roll to settle. Closes presented codex moments as a player would.</summary>
        private IEnumerator Idle()
        {
            float guard = Time.realtimeSinceStartup + IdleTimeout;
            int calm = 0;
            while (Time.realtimeSinceStartup < guard)
            {
                if (d == null || SceneManager.GetActiveScene().name != startScene) yield break;
                if (CodexBook.PagesVisible && !CodexBook.Scribing) CodexBook.Close();
                calm = Busy() ? 0 : calm + 1;
                if (calm > 10) yield break;
                yield return null;
            }
            res.Trace.Add($"idle timeout: running {Running} pending {Pending} bubble {(Presenter != null && Presenter.Busy)} film {MemoryFlashback.IsPlaying} codex {CodexBook.IsOpen}");
        }

        private IEnumerator Step(GuideLine line, LineResult lr)
        {
            bool gone = d == null || SceneManager.GetActiveScene().name != startScene;
            switch (line.Cmd)
            {
                case "at":
                {
                    if (gone) { lr.Detail = "the room has already left"; yield break; }
                    RealmZone z = d.zones.FirstOrDefault(x => x != null && x.zoneId == line.Arg);
                    if (z == null) { lr.Detail = "the scene has no zone " + line.Arg; yield break; }
                    var cc = d.player.GetComponent<CharacterController>();
                    int tries = 0;
                    foreach (Vector3 p in PointsIn(z))
                    {
                        if (++tries > 12) break;
                        if (cc != null) cc.enabled = false;
                        d.player.transform.position = p;
                        if (cc != null) cc.enabled = true;
                        for (int i = 0; i < 4; i++) yield return null;
                        if (d.Zone == line.Arg) break;
                    }
                    yield return Idle();
                    if (BdRunner.Shots) BdRunner.Shot(startScene + "_" + line.LineNo.ToString("000") + "_at");
                    lr.Detail = $"zone {d.Zone ?? "none"} after {tries} placement(s)";
                    lr.Pass = d.Zone == line.Arg;
                    yield break;
                }
                case "say":
                case "wait":
                {
                    string text = line.Cmd == "wait" ? "wait" : line.Arg;
                    if (gone) { lr.Detail = "the room has already left"; yield break; }
                    yield return Idle();
                    SpellPresenter p = Presenter;
                    for (int i = 0; i < 3; i++) yield return null;
                    if (p == null || !p.InputAllowed) { lr.Detail = $"the bubble will not open (frozen {Frozen}, leaving {Leaving}, busy {p?.Busy})"; yield break; }
                    SpellResult preview = room.Preview(text, d.Zone);
                    p.InsertWord(text);
                    yield return null;
                    if (!p.IsOpen) { lr.Detail = "InsertWord did not open the bubble"; yield break; }
                    if (BdRunner.Shots) { for (int i = 0; i < 20; i++) yield return null; BdRunner.Shot(startScene + "_" + line.LineNo.ToString("000") + "_typed"); }
                    typeof(SpellPresenter).GetMethod("Submit", F).Invoke(p, null);
                    yield return null;
                    yield return Idle();
                    lr.Detail = $"{preview.Outcome}: {preview.Message}";
                    lr.Pass = (string.IsNullOrEmpty(line.Expect) || string.Equals(preview.Outcome.ToString(), line.Expect, StringComparison.OrdinalIgnoreCase))
                              && (string.IsNullOrEmpty(line.Message) || (preview.Message ?? "").IndexOf(line.Message, StringComparison.OrdinalIgnoreCase) >= 0);
                    res.Trace.Add($"say \"{text}\" @{d.Zone} -> {lr.Detail}");
                    yield break;
                }
                case "use":
                {
                    if (gone) { lr.Detail = "the room has already left"; yield break; }
                    yield return Idle();
                    for (int i = 0; i < 3; i++) yield return null;
                    Usable u = Field<Usable>("usableHere");
                    if (u == null || u.Id != line.Arg) { lr.Detail = $"prompt here is {(u == null ? "none" : u.Id)} (zone {d.Zone}, frozen {Frozen})"; yield break; }
                    if (!string.IsNullOrEmpty(u.Deed)) typeof(RealmSceneDirector).GetMethod("DoDirect", F).Invoke(d, new object[] { u.Deed });
                    d.FireForCheck(TriggerKind.Use, u.Id);
                    yield return null;
                    yield return Idle();
                    if (BdRunner.Shots) BdRunner.Shot(startScene + "_" + line.LineNo.ToString("000") + "_use");
                    lr.Pass = true;
                    lr.Detail = "used";
                    yield break;
                }
                case "alive":
                {
                    if (gone) { lr.Detail = "the room has already left"; yield break; }
                    yield return Idle();
                    // With no command typed, the object's lit lights must still move (Zac 2026-11-26).
                    var lights = d.views.Where(v => v != null && v.objectId == line.Arg)
                        .SelectMany(v => v.GetComponentsInChildren<Light>(false)).Where(l => l.intensity > 0.01f).ToList();
                    if (lights.Count == 0) { lr.Detail = "no lit light under " + line.Arg; yield break; }
                    var first = lights.Select(l => l.intensity).ToList();
                    // 1.5 s of game time, not a frame count: batch mode runs far more frames per second, so a dim
                    // light barely moved in 40 frames. Pass on a change of more than 3 percent of its first intensity.
                    float until = Time.time + 1.5f;
                    float most = 0f;
                    while (Time.time < until)
                    {
                        yield return null;
                        for (int k = 0; k < lights.Count; k++) most = Mathf.Max(most, Mathf.Abs(lights[k].intensity - first[k]) / Mathf.Max(0.01f, first[k]));
                    }
                    lr.Detail = $"{lights.Count} lit light(s), largest change {most:P1} over 1.5 s with no command";
                    lr.Pass = most > 0.03f;
                    yield break;
                }
                case "anpu":
                {
                    if (gone) { lr.Detail = "the room has already left"; yield break; }
                    yield return Idle();
                    yield return Anpu(line.Arg, lr);
                    yield break;
                }
                case "exit":
                {
                    float guard = Time.realtimeSinceStartup + 60f;
                    while (SceneManager.GetActiveScene().name == startScene && Time.realtimeSinceStartup < guard)
                    {
                        if (CodexBook.PagesVisible && !CodexBook.Scribing) CodexBook.Close();
                        yield return null;
                    }
                    string now = SceneManager.GetActiveScene().name;
                    lr.Detail = "now in " + now;
                    string want = line.Arg == "title" ? OpeningSettings.TitleScene : line.Arg;
                    lr.Pass = now != startScene && GuideScenes.Norm(now) == GuideScenes.Norm(want);
                    yield break;
                }
            }

            if (!gone) yield return Idle();
            if (room == null) { lr.Detail = "no room"; yield break; }
            switch (line.Cmd)
            {
                case "expect":
                {
                    var (id, val) = GuideParser.KeyValue(line.Arg);
                    string now = room.Get(id);
                    lr.Detail = $"{id}={now}";
                    lr.Pass = now == val;
                    break;
                }
                case "beat":
                case "nobeat":
                {
                    bool did = ran.Contains(line.Arg) || (firedSet != null && firedSet.Contains(line.Arg));
                    lr.Detail = did ? "ran" : "not run. Ran: " + string.Join(", ", ran);
                    lr.Pass = line.Cmd == "beat" ? did : !did;
                    break;
                }
                case "flag": lr.Pass = flagSet != null && flagSet.Contains(line.Arg); break;
                case "noflag": lr.Pass = flagSet == null || !flagSet.Contains(line.Arg); break;
                case "verb": lr.Pass = room.Def.Verbs.Contains(line.Arg); break;
                case "film": lr.Detail = "films: " + string.Join(", ", films); lr.Pass = films.Contains(line.Arg); break;
                case "codexhas": lr.Pass = CodexStore.Has(line.Arg); break;
                case "solved": lr.Pass = room.Solved; break;
                case "unsolved": lr.Pass = !room.Solved; break;
                case "frozen": lr.Pass = !gone && Frozen; break;
                case "free": lr.Pass = !gone && !Frozen; lr.Detail = gone ? "left" : Frozen ? "frozen" : "free"; break;
                case "noexit": lr.Pass = !gone && !Leaving; break;
                default: lr.Detail = "unknown command " + line.Cmd; break;
            }
        }

        /// <summary>
        /// Where Anpu really is. waits: active, holding, and no more than 0.6 m from where he stood when the block
        /// began. absent: not in the room. follows: active and not holding. at mark: holding, within 0.7 m of the
        /// mark (he is given up to 20 s to walk there).
        /// </summary>
        private IEnumerator Anpu(string arg, LineResult lr)
        {
            string[] a = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            RealmFollower f = d.anpu;
            bool here = f != null && f.gameObject.activeInHierarchy;
            Vector3 pos = f != null ? f.transform.position : Vector3.zero;
            float moved = Flat(pos - anpuStart);
            lr.Detail = f == null ? "no Anpu in the scene" : $"active {here}, holding {f.Holding}, at {pos.x:0.00},{pos.z:0.00}, moved {moved:0.00} m";
            if (a.Length == 0) yield break;
            switch (a[0])
            {
                case "absent": lr.Pass = !here; break;
                case "follows": lr.Pass = here && !f.Holding; break;
                case "waits": lr.Pass = here && f.Holding && moved <= 0.6f; break;
                case "at":
                {
                    RealmSceneDirector.Mark m = a.Length > 1 ? d.marks.FirstOrDefault(x => x != null && x.name == a[1]) : null;
                    if (m == null || m.transform == null) { lr.Detail = "the scene has no mark " + (a.Length > 1 ? a[1] : "(none)"); yield break; }
                    float guard = Time.realtimeSinceStartup + 20f;
                    while (here && Flat(f.transform.position - m.transform.position) > 0.7f && Time.realtimeSinceStartup < guard) yield return null;
                    float off = here ? Flat(f.transform.position - m.transform.position) : -1f;
                    // Hold him there a moment longer: he must stay at the mark, not fall back in behind Ankhu.
                    for (int i = 0; i < 60 && here; i++) yield return null;
                    float after = here ? Flat(f.transform.position - m.transform.position) : -1f;
                    lr.Detail = $"active {here}, holding {f.Holding}, {off:0.00} m from {a[1]} on arrival, {after:0.00} m a second later";
                    lr.Pass = here && f.Holding && off >= 0f && off <= 0.7f && after <= 0.7f;
                    break;
                }
                default: lr.Detail = "unknown anpu check " + a[0]; break;
            }
        }

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        /// <summary>Points inside the zone where the director would pick this zone (highest priority wins), centre first.</summary>
        private IEnumerable<Vector3> PointsIn(RealmZone target)
        {
            Bounds b = target.WorldBounds;
            float y0 = d.player.transform.position.y;
            foreach (float y in new[] { Mathf.Clamp(y0, b.min.y - 0.5f, b.max.y - 0.1f), b.min.y + 0.05f, b.center.y })
            for (int r = 0; r <= 6; r++)
            {
                for (int i = 0; i <= r; i++)
                {
                    foreach (var (sx, sz) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
                    {
                        float fx = 0.5f + sx * 0.5f * i / 7f;
                        float fz = 0.5f + sz * 0.5f * (r - i) / 7f;
                        var p = new Vector3(Mathf.Lerp(b.min.x, b.max.x, fx), y, Mathf.Lerp(b.min.z, b.max.z, fz));
                        RealmZone best = null;
                        foreach (RealmZone z in d.zones)
                        {
                            if (z == null || string.IsNullOrEmpty(z.zoneId) || !z.Contains(p)) continue;
                            if (best == null || z.priority > best.priority) best = z;
                        }
                        if (best == target) yield return p;
                    }
                }
            }
        }
    }
}
