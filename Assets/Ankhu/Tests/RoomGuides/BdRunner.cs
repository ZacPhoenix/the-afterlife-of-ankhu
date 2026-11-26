using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Ankhu.Bindings;
using Ankhu.BorrowedDawn;
using Ankhu.Core;
using Ankhu.Integration;
using Ankhu.Opening;
using CommandResult = Ankhu.Integration.CommandResult;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ankhu.Tests.RoomGuides
{
    /// <summary>
    /// Plays an "engine bd" block (Teach1 to Teach3, Borrowed Dawn) through BorrowedDawnRulesAdapter, the adapter the
    /// scenes install. Sim mode builds a fresh adapter for the room. Play mode loads the scene and speaks to the
    /// adapter the scene installed (RoomRulesHost), so the teaching director, the painted views and the exit react.
    /// Commands: zone, say, interact, expect Field=Value (any RoomState property, e.g. Water=West, BindingCount=1),
    /// bound Target, unbound Target, solved, unsolved, exit Scene.
    /// </summary>
    public static class BdRunner
    {
        public static string RoomIdFor(string scene)
        {
            string n = GuideScenes.Norm(scene);
            if (n == "BorrowedDawn" || n == "BorrowedDawn_Greybox") return BorrowedDawnRooms.BorrowedDawnId;
            foreach (string id in BorrowedDawnRooms.SequenceIds)
                if (BorrowedDawnRooms.SceneNameFor(id) == n) return id;
            return null;
        }

        /// <summary>Where the room leaves to, as TeachingRoomDirector and BorrowedDawnExit decide it.</summary>
        public static string ExitFor(string scene)
        {
            string id = RoomIdFor(scene);
            if (id == null) return null;
            if (BorrowedDawnRooms.IsTeachingRoom(id))
            {
                string next = BorrowedDawnRooms.NextSceneName(id);
                return next == BorrowedDawnRooms.SceneNameFor(BorrowedDawnRooms.BorrowedDawnId) ? PlayOrder.BorrowedDawnPainted : next;
            }
            return PlayOrder.After(PlayOrder.BorrowedDawnPainted) ?? PlayOrder.TerraceScene;
        }

        public static ScriptResult Run(GuideScript g)
        {
            var res = new ScriptResult { Script = g, Mode = "sim" };
            string id = RoomIdFor(g.Scene);
            if (id == null) { res.Summary = "no Borrowed Dawn room for scene " + g.Scene; return res; }
            var adapter = new BorrowedDawnRulesAdapter(BorrowedDawnRooms.Resolve(id));
            string zone = adapter.RulesZoneId;
            foreach (GuideLine line in g.Lines)
            {
                var lr = new LineResult { Line = line };
                res.Lines.Add(lr);
                if (line.Cmd == "exit")
                {
                    string to = ExitFor(g.Scene);
                    lr.Detail = adapter.IsSolved ? "exit " + to : "not solved, the room does not leave";
                    lr.Pass = adapter.IsSolved && GuideScenes.Norm(to) == GuideScenes.Norm(line.Arg);
                }
                else lr.Pass = Step(adapter, ref zone, line, out lr.Detail, res);
                if (!lr.Pass) break;
            }
            res.Pass = res.Lines.Count > 0 && res.Lines.All(l => l.Pass);
            res.Summary = res.Pass ? $"{res.Lines.Count} lines" : "stopped at line " + res.Lines.Last().Line.LineNo;
            return res;
        }

        public static bool Step(BorrowedDawnRulesAdapter a, ref string zone, GuideLine line, out string detail, ScriptResult res)
        {
            detail = string.Empty;
            RoomState s = a.Session.State;
            switch (line.Cmd)
            {
                case "zone":
                case "at":
                {
                    // A refused move leaves Ankhu where he was: he cannot physically stand on a lift that is not there.
                    CommandResult moved = a.SyncLocation(line.Arg);
                    if (moved == null) zone = line.Arg;
                    detail = moved == null ? "rules at " + a.RulesZoneId : $"{moved.Outcome}: {moved.Message}";
                    res.Trace.Add("zone " + zone + " -> " + detail);
                    if (!string.IsNullOrEmpty(line.Expect)) return moved != null && string.Equals(moved.Outcome.ToString(), line.Expect, StringComparison.OrdinalIgnoreCase);
                    return moved == null;
                }
                case "say":
                case "interact":
                case "use":
                {
                    var ctx = new PlayerContext(zone, zone == ZoneIds.Lift, zone == ZoneIds.Bridge);
                    CommandResult r = line.Cmd == "say" ? a.Speak(line.Arg, ctx) : a.Interact(line.Arg, ctx);
                    detail = $"{r.Outcome}: {r.Message}";
                    res.Trace.Add($"{line.Cmd} \"{line.Arg}\" @{zone} -> {detail}");
                    if (!string.IsNullOrEmpty(line.Expect) && !string.Equals(r.Outcome.ToString(), line.Expect, StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrEmpty(line.Message) && (r.Message ?? "").IndexOf(line.Message, StringComparison.OrdinalIgnoreCase) < 0) return false;
                    return true;
                }
                case "expect":
                {
                    var (field, want) = GuideParser.KeyValue(line.Arg);
                    PropertyInfo p = typeof(RoomState).GetProperty(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                    if (p == null) { detail = "RoomState has no " + field; return false; }
                    string now = Convert.ToString(p.GetValue(s));
                    detail = $"{field}={now}";
                    return string.Equals(now, want, StringComparison.OrdinalIgnoreCase);
                }
                case "bound":
                case "unbound":
                {
                    if (!Enum.TryParse(line.Arg, true, out BindTarget t)) { detail = "no BindTarget " + line.Arg; return false; }
                    bool b = s.IsBound(t);
                    detail = b ? "bound" : "not bound";
                    return line.Cmd == "bound" ? b : !b;
                }
                case "film":
                {
                    string f = MemoryFlicker.FilmForRoom(a.Room.Id);
                    detail = "room film " + (f ?? "none");
                    return f == line.Arg;
                }
                case "solved": detail = s.ToString(); return a.IsSolved;
                case "unsolved": detail = s.ToString(); return !a.IsSolved;
                default: detail = "unknown command " + line.Cmd; return false;
            }
        }

        /// <summary>Set ANKHU_E2E_SHOTS=1 to save the main camera after every say and zone line (art_gen/realms/captures/e2e).</summary>
        public static bool Shots => System.Environment.GetEnvironmentVariable("ANKHU_E2E_SHOTS") == "1";

        public static void Shot(string name)
        {
            Camera cam = Camera.main;
            if (cam == null)
                foreach (Camera c in Camera.allCameras)
                    if (c.isActiveAndEnabled && c.targetTexture == null && (cam == null || c.depth > cam.depth)) cam = c;
            if (cam == null) { Debug.LogWarning("[E2E shot] no camera for " + name); return; }
            const int W = 1600, H = 900;
            var rt = new RenderTexture(W, H, 24);
            RenderTexture prevT = cam.targetTexture, prevA = RenderTexture.active;
            float aspect = cam.aspect;
            cam.aspect = (float)W / H;
            // Batch play mode has a 4:3 game view, so the follow camera clamps for 4:3. Re-frame it at 16:9 as a player sees it.
            var follow = cam.GetComponent<Ankhu.CameraRig.CameraFollow2D>();
            if (follow != null) follow.SnapToTarget();
            // Overlay canvases (the speech bubble, prompts) are not drawn by cam.Render, so hang them on the camera for the shot.
            var moved = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.isActiveAndEnabled)
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = cam.nearClipPlane + 0.05f;
                    moved.Add(c);
                }
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            foreach (Canvas c in moved) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            cam.targetTexture = prevT;
            cam.aspect = aspect;
            RenderTexture.active = prevA;
            string dir = System.IO.Path.Combine("art_gen", "realms", "captures", "e2e");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
        }

        /// <summary>Play mode: load the scene, speak to the adapter it installed, wait for its own exit.</summary>
        public static IEnumerator Play(GuideScript g, ScriptResult res, bool chained = false)
        {
            res.Mode = "play";
            if (!Application.CanStreamedLevelBeLoaded(g.Scene)) { res.Summary = g.Scene + " is not in Build Settings"; yield break; }
            float oldScale = Time.timeScale;
            Time.timeScale = PlayRunner.TimeScale;
            try
            {
                if (!chained) yield return SceneManager.LoadSceneAsync(g.Scene, LoadSceneMode.Single);
                else if (GuideScenes.Norm(SceneManager.GetActiveScene().name) != GuideScenes.Norm(g.Scene))
                {
                    res.Summary = $"the chain arrived in {SceneManager.GetActiveScene().name}, not {g.Scene}";
                    yield break;
                }
                string start = SceneManager.GetActiveScene().name;
                for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
                RoomRulesHost host = RoomRulesHost.Instance;
                var a = host != null ? host.Adapter as BorrowedDawnRulesAdapter : null;
                if (a == null) { res.Summary = "the scene installed no BorrowedDawnRulesAdapter"; yield break; }
                if (a.Room.Id != RoomIdFor(g.Scene)) { res.Summary = $"the scene runs room {a.Room.Id}, not {RoomIdFor(g.Scene)}"; yield break; }
                string zone = a.RulesZoneId;
                foreach (GuideLine line in g.Lines)
                {
                    var lr = new LineResult { Line = line };
                    res.Lines.Add(lr);
                    if (line.Cmd == "exit")
                    {
                        float guard = Time.realtimeSinceStartup + 120f;
                        while (SceneManager.GetActiveScene().name == start && Time.realtimeSinceStartup < guard) yield return null;
                        string now = SceneManager.GetActiveScene().name;
                        lr.Detail = "now in " + now;
                        lr.Pass = now != start && GuideScenes.Norm(now) == GuideScenes.Norm(line.Arg);
                    }
                    else
                    {
                        float settle = Time.realtimeSinceStartup + 20f;
                        while (!host.IsWorldSettled && Time.realtimeSinceStartup < settle) yield return null;
                        lr.Pass = Step(a, ref zone, line, out lr.Detail, res);
                        yield return null;
                        if (Shots && (line.Cmd == "say" || line.Cmd == "zone"))
                        {
                            // Let the world finish moving (water, lift) before the picture.
                            float settle2 = Time.realtimeSinceStartup + 20f;
                            while (!host.IsWorldSettled && Time.realtimeSinceStartup < settle2) yield return null;
                            for (int w = 0; w < 30; w++) yield return null;
                            Shot(g.Scene + "_" + line.LineNo.ToString("000") + "_" + line.Cmd);
                        }
                    }
                    if (!lr.Pass) break;
                }
                res.Pass = res.Lines.Count > 0 && res.Lines.All(l => l.Pass);
                res.Summary = res.Pass ? $"{res.Lines.Count} lines" : "stopped at line " + res.Lines.Last().Line.LineNo;
            }
            finally { Time.timeScale = oldScale; }
        }
    }
}
