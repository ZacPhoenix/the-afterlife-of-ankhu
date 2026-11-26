using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Ankhu.Opening;
using Ankhu.Realms.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Zac 2026-11-24: proves the game is wired end to end in play order (Ankhu.Opening.PlayOrder).
    /// For every scene in the chain it checks the scene file exists and is enabled in Build Settings, opens it
    /// (never saving), finds the component that owns its exit, asks that exit where it goes and compares that with
    /// the next scene in the chain. It also checks every memory film, painted memory still and codex write the scene
    /// plays. Report: art_gen/realms/audit/play_order_check.md. The exit code is 1 when any link is broken.
    ///
    /// ApplyBuildOrderCli puts Build Settings in play order (title first, so a built player starts at the title)
    /// and keeps every other scene enabled after the chain.
    /// </summary>
    public static class PlayOrderCheck
    {
        private const string ReportPath = "art_gen/realms/audit/play_order_check.md";

        private sealed class Row
        {
            public int Step;
            public string Scene;
            public string Owner = "";
            public string Exit = "";
            public string Expected = "";
            public string Plays = "";
            public List<string> Problems = new List<string>();
        }

        [UnityEditor.MenuItem("Ankhu/Play Order/Check Play Order")]
        public static void CheckFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int bad = Run(out string report);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Play order", bad == 0 ? "Every link is wired." : bad + " problems. See " + ReportPath, "OK");
        }

        public static void CheckCli()
        {
            int bad = 0;
            try { bad = Run(out string report); Debug.Log(report); }
            catch (Exception e) { Debug.LogException(e); bad = 99; }
            EditorApplication.Exit(bad == 0 ? 0 : 1);
        }

        [UnityEditor.MenuItem("Ankhu/Play Order/Apply Build Settings Order")]
        public static void ApplyBuildOrder()
        {
            var current = EditorBuildSettings.scenes.ToList();
            var ordered = new List<EditorBuildSettingsScene>();
            // Zac 2026-11-25: a chain room with a painted twin on disk takes the twin's place (Teach1_Cistern_Painted and so on).
            var chainNames = PlayOrder.Chain.Select(c => c == PlayOrder.BorrowedDawnSlot ? PlayOrder.BorrowedDawnPainted
                : FindScenePath(c + PlayOrder.PaintedSuffix) != null ? c + PlayOrder.PaintedSuffix : c).ToList();
            foreach (string name in chainNames)
            {
                string path = FindScenePath(name);
                if (path == null)
                {
                    Debug.LogWarning("[PlayOrder] no scene file for " + name);
                    continue;
                }
                ordered.Add(new EditorBuildSettingsScene(path, true));
            }
            foreach (var s in current)
            {
                if (ordered.Any(o => o.path == s.path)) continue;
                ordered.Add(new EditorBuildSettingsScene(s.path, s.enabled));
            }
            EditorBuildSettings.scenes = ordered.ToArray();
            AssetDatabase.SaveAssets();
            var sb = new StringBuilder("[PlayOrder] Build Settings now:\n");
            for (int i = 0; i < ordered.Count; i++) sb.AppendLine(i + " " + (ordered[i].enabled ? "" : "(off) ") + ordered[i].path);
            Debug.Log(sb.ToString());
        }

        public static void ApplyBuildOrderCli()
        {
            int code = 0;
            try { ApplyBuildOrder(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        private static string FindScenePath(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Scene"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == name) return p;
            }
            return null;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = a.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        private static MonoBehaviour FindComponent(string typeName)
        {
            return UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(m => m != null && m.GetType().Name == typeName);
        }

        private static bool FilmExists(string id) =>
            File.Exists(Path.Combine(Application.streamingAssetsPath, "Memories", id + ".mp4"));

        public static int Run(out string report)
        {
            string[] chain = PlayOrder.Resolved();
            var rows = new List<Row>();
            var global = new List<string>();
            EditorBuildSettingsScene[] build = EditorBuildSettings.scenes;
            var enabled = build.Where(s => s.enabled).Select(s => s.path).ToList();

            if (enabled.Count == 0 || Path.GetFileNameWithoutExtension(enabled[0]) != OpeningSettings.TitleScene)
            {
                global.Add("Build index 0 is " + (enabled.Count > 0 ? Path.GetFileNameWithoutExtension(enabled[0]) : "nothing")
                           + ", not " + OpeningSettings.TitleScene + ", so a built player would not start at the title. Run Ankhu > Play Order > Apply Build Settings Order.");
            }
            if (PlayOrder.BorrowedDawnScene() != PlayOrder.BorrowedDawnPainted)
            {
                global.Add("BorrowedDawn_Painted is not in Build Settings, so the chain falls back to the greybox Borrowed Dawn.");
            }

            Type exitType = FindType("Ankhu.Bindings.BorrowedDawnExit");
            Type roomsType = FindType("Ankhu.Bindings.BorrowedDawnRooms");
            Type flickerType = FindType("Ankhu.Bindings.MemoryFlicker");

            WernesProgress.Simulate(new string[0]);
            try
            {
            for (int i = 0; i < chain.Length; i++)
            {
                var row = new Row { Step = i + 1, Scene = chain[i] };
                rows.Add(row);
                string expected = PlayOrder.ExpectedExit(chain[i]);
                if (expected == PlayOrder.BorrowedDawnSlot) expected = PlayOrder.BorrowedDawnScene();
                row.Expected = expected == OpeningSettings.TitleScene && i > 0 ? expected + " (end of built content)" : expected;

                string path = FindScenePath(chain[i]);
                if (path == null) { row.Problems.Add("scene file missing"); continue; }
                if (!enabled.Contains(path)) row.Problems.Add("not enabled in Build Settings");

                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                string exit = null;
                var plays = new List<string>();

                if (chain[i] == OpeningSettings.TitleScene)
                {
                    row.Owner = "TitleMenu";
                    if (FindComponent("TitleMenu") == null) row.Problems.Add("no TitleMenu");
                    exit = OpeningSettings.CinematicScene; // New game: TitleMenu.cs fades to the cinematic.
                }
                else if (chain[i] == OpeningSettings.CinematicScene)
                {
                    row.Owner = "OpeningCinematic";
                    if (FindComponent("OpeningCinematic") == null) row.Problems.Add("no OpeningCinematic");
                    exit = OpeningSettings.WalkScene;
                }
                else if (chain[i] == OpeningSettings.WalkScene)
                {
                    row.Owner = "WalkSequence";
                    if (FindComponent("WalkSequence") == null) row.Problems.Add("no WalkSequence");
                    exit = OpeningSettings.SceneAfterWalk();
                }
                else if (FindComponent("RealmSceneDirector") != null)
                {
                    row.Owner = "RealmSceneDirector";
                    exit = RealmSceneDirector.NextSceneName();
                    CheckScript(chain[i], row, plays);
                }
                else
                {
                    MonoBehaviour installer = FindComponent("BorrowedDawnRulesInstaller");
                    if (installer == null)
                    {
                        row.Problems.Add("no exit owner (no RealmSceneDirector or BorrowedDawnRulesInstaller)");
                    }
                    else
                    {
                        string roomId = installer.GetType().GetProperty("RoomId")?.GetValue(installer) as string;
                        bool teaching = roomsType != null && (bool)roomsType.GetMethod("IsTeachingRoom").Invoke(null, new object[] { roomId });
                        string film = flickerType?.GetMethod("FilmForRoom")?.Invoke(null, new object[] { roomId }) as string;
                        if (!string.IsNullOrEmpty(film))
                        {
                            plays.Add("film " + film);
                            if (!FilmExists(film)) row.Problems.Add("film missing: StreamingAssets/Memories/" + film + ".mp4");
                        }
                        if (teaching)
                        {
                            row.Owner = "TeachingRoomDirector (" + roomId + ")";
                            exit = roomsType.GetMethod("NextSceneName").Invoke(null, new object[] { roomId }) as string;
                            string bdName = roomsType.GetMethod("SceneNameFor").Invoke(null, new object[] { (string)roomsType.GetField("BorrowedDawnId").GetValue(null) }) as string;
                            if (exit == bdName) { exit = PlayOrder.BorrowedDawnScene(); plays.Add("Anpu farewell"); }
                            else exit = PlayOrder.PaintedOf(exit);
                        }
                        else
                        {
                            row.Owner = "BorrowedDawnExit (added at runtime)";
                            exit = (exitType?.GetProperty("NextScene")?.GetValue(null) ?? exitType?.GetField("NextScene")?.GetValue(null)) as string;
                            plays.Add("Anpu exit lines");
                        }
                    }
                }

                row.Exit = exit ?? "(none)";
                row.Plays = string.Join(", ", plays);
                if (exit != expected) row.Problems.Add("exit goes to " + row.Exit + ", play order says " + expected);
                if (exit != null && FindScenePath(exit) == null) row.Problems.Add("exit target scene file missing");
            }
            CheckHubRouting(rows, chain.Length);
            }
            finally { WernesProgress.Simulate(null); }

            int bad = global.Count + rows.Sum(r => r.Problems.Count);
            var sb = new StringBuilder();
            sb.AppendLine("# Play order check");
            sb.AppendLine();
            sb.AppendLine("Run " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ". " + (bad == 0 ? "PASS, every link is wired." : "FAIL, " + bad + " problems."));
            sb.AppendLine();
            foreach (string g in global) sb.AppendLine("- " + g);
            if (global.Count > 0) sb.AppendLine();
            sb.AppendLine("| # | Scene | Exit owner | Exit goes to | Play order expects | Plays inside | Status |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (Row r in rows)
            {
                sb.AppendLine("| " + r.Step + " | " + r.Scene + " | " + r.Owner + " | " + r.Exit + " | " + r.Expected + " | " + r.Plays + " | "
                              + (r.Problems.Count == 0 ? "wired" : "UNWIRED: " + string.Join("; ", r.Problems)) + " |");
            }
            report = sb.ToString();
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, report);
            return bad;
        }

        /// <summary>
        /// The hub is visited five times. With each room done in turn, its exit must lead to the next undone room,
        /// and on to the scene after the last Wernes room (Rosetau A) once all four are done. Uses simulated progress, never the player's PlayerPrefs.
        /// </summary>
        private static void CheckHubRouting(List<Row> rows, int step)
        {
            string hubPath = FindScenePath(PlayOrder.HubScene);
            if (hubPath == null) return; // the chain row already reports the missing scene
            EditorSceneManager.OpenScene(hubPath, OpenSceneMode.Single);
            string[] rooms = WernesProgress.Rooms;
            for (int done = 1; done <= rooms.Length; done++)
            {
                var letters = rooms.Take(done).Select(WernesProgress.LetterOf).ToArray();
                WernesProgress.Simulate(letters);
                string expected = done < rooms.Length ? rooms[done] : (PlayOrder.After(PlayOrder.LastWernesRoom) ?? OpeningSettings.TitleScene);
                string exit = RealmSceneDirector.NextSceneName();
                var row = new Row
                {
                    Step = ++step, Scene = PlayOrder.HubScene + " (return " + done + ", done " + string.Join("", letters) + ")",
                    Owner = "RealmSceneDirector", Exit = exit, Expected = expected,
                };
                if (exit != expected) row.Problems.Add("hub exit goes to " + exit + ", expected " + expected);
                rows.Add(row);
            }
        }

        private static void CheckScript(string scene, Row row, List<string> plays)
        {
            // A painted twin (Realm_01_Arit1_Painted) runs its greybox's script: the builder sets director.sceneName to it.
            SceneScript script = RealmContent.ByScene(scene)
                ?? (scene.EndsWith("_Painted") ? RealmContent.ByScene(scene.Substring(0, scene.Length - "_Painted".Length)) : null);
            if (script == null) { row.Problems.Add("no scene script"); return; }
            bool hasNext = false;
            foreach (Step s in script.Beats.SelectMany(b => b.Steps))
            {
                switch (s.Kind)
                {
                    case StepKind.NextScene: hasNext = true; break;
                    case StepKind.Cue when s.Text.StartsWith("flashback:"):
                        string id = s.Text.Substring("flashback:".Length);
                        plays.Add("film " + id);
                        if (!FilmExists(id)) row.Problems.Add("film missing: " + id);
                        break;
                    case StepKind.Memory:
                        if (!plays.Contains("still " + s.Image)) plays.Add("still " + s.Image);
                        if (Resources.Load<Texture2D>("RealmMemories/" + s.Image) == null) row.Problems.Add("memory still missing: RealmMemories/" + s.Image);
                        break;
                    case StepKind.Write:
                        if (!plays.Contains("codex " + s.Text)) plays.Add("codex " + s.Text);
                        if (CodexCatalog.Get(s.Text) == null) row.Problems.Add("codex entry missing: " + s.Text);
                        break;
                }
            }
            if (!hasNext) row.Problems.Add("script has no NextScene step, so the room never exits");
        }
    }
}
