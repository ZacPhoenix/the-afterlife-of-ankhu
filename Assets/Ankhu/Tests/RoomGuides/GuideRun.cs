using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ankhu.Realms.Core;
using Ankhu.Realms.Core.Content;

namespace Ankhu.Tests.RoomGuides
{
    /// <summary>The outcome of one guide line.</summary>
    public sealed class LineResult
    {
        public GuideLine Line;
        public bool Pass;
        public string Detail = string.Empty;
    }

    /// <summary>The outcome of one e2e block.</summary>
    public sealed class ScriptResult
    {
        public GuideScript Script;
        public string Mode = "sim";
        public bool Pass;
        public bool Skipped;
        public string Summary = string.Empty;
        public readonly List<LineResult> Lines = new List<LineResult>();
        public readonly List<string> Trace = new List<string>();
        public HashSet<string> FailedTags => new HashSet<string>(Lines.Where(l => !l.Pass).SelectMany(l => l.Line.Tags));
        public HashSet<string> PassedTags => new HashSet<string>(Lines.Where(l => l.Pass).SelectMany(l => l.Line.Tags));

        public string Explain()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Script.Id} ({Mode}): {(Skipped ? "SKIPPED" : Pass ? "PASS" : "FAIL")} {Summary}");
            foreach (LineResult l in Lines)
                sb.AppendLine($"  {(l.Pass ? "ok  " : "FAIL")} {l.Line.Where(Script)}{(string.IsNullOrEmpty(l.Detail) ? "" : "  -> " + l.Detail)}");
            if (!Pass && Trace.Count > 0)
            {
                sb.AppendLine("  trace:");
                foreach (string t in Trace.Skip(Math.Max(0, Trace.Count - 40))) sb.AppendLine("    " + t);
            }
            return sb.ToString();
        }
    }

    /// <summary>Finds every realm scene script, the same way RealmContent.ByScene does (Core only, no engine).</summary>
    public static class GuideScenes
    {
        public static SceneScript ByScene(string scene) =>
            WernesPaintedScripts.ByScene(scene) ?? WernesScripts.ByScene(scene) ?? RosetauScripts.ByScene(scene)
            ?? (scene.EndsWith("_Painted") ? ByScene(scene.Substring(0, scene.Length - "_Painted".Length)) : null);

        public static string Norm(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return scene;
            if (scene == "title" || scene == Ankhu.Opening.OpeningSettings.TitleScene) return "title";
            return scene.EndsWith("_Painted") ? scene.Substring(0, scene.Length - "_Painted".Length) : scene;
        }
    }

    /// <summary>Plays a realm e2e block through <see cref="RealmSim"/>. Stops at the first failed line.</summary>
    public static class SimRunner
    {
        public static ScriptResult Run(GuideScript g)
        {
            var res = new ScriptResult { Script = g, Mode = "sim" };
            if (g.Engine != "realm")
            {
                res.Skipped = true;
                res.Pass = true;
                res.Summary = "engine " + g.Engine + " runs in BdRunner";
                return res;
            }
            SceneScript script = GuideScenes.ByScene(g.Scene);
            if (script == null)
            {
                res.Summary = "no scene script for " + g.Scene;
                return res;
            }
            RealmSim sim;
            try { sim = new RealmSim(script, g.Progress, g.Codex); }
            catch (Exception e) { res.Summary = "scene start threw " + e.Message; return res; }

            foreach (GuideLine line in g.Lines)
            {
                var lr = new LineResult { Line = line };
                try { lr.Pass = Step(sim, line, out lr.Detail); }
                catch (Exception e) { lr.Pass = false; lr.Detail = "threw " + e.GetType().Name + ": " + e.Message; }
                res.Lines.Add(lr);
                if (!lr.Pass) break;
            }
            res.Trace.AddRange(sim.Log);
            res.Pass = res.Lines.All(l => l.Pass);
            res.Summary = res.Pass ? $"{res.Lines.Count} lines" : "stopped at line " + res.Lines.Last().Line.LineNo;
            return res;
        }

        private static bool Step(RealmSim sim, GuideLine line, out string detail)
        {
            detail = string.Empty;
            switch (line.Cmd)
            {
                case "at":
                    sim.Enter(line.Arg);
                    return true;
                case "say":
                {
                    if (!sim.CanType) { detail = sim.Leaving ? "the room has already left" : "Ankhu is frozen, the spell bubble cannot open"; return false; }
                    SpellResult r = sim.Say(line.Arg);
                    detail = $"{r.Outcome}: {r.Message}";
                    if (!string.IsNullOrEmpty(line.Expect) && !string.Equals(r.Outcome.ToString(), line.Expect, StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrEmpty(line.Message) && (r.Message ?? string.Empty).IndexOf(line.Message, StringComparison.OrdinalIgnoreCase) < 0) return false;
                    return true;
                }
                case "use":
                {
                    Usable u = sim.UsableHere();
                    if (u == null || u.Id != line.Arg)
                    {
                        detail = $"prompt offered here is {(u == null ? "none" : u.Id)} (zone {sim.Zone ?? "none"}, frozen {sim.Frozen})";
                        return false;
                    }
                    SpellResult r = sim.Use(u);
                    detail = r == null ? "used" : $"deed {r.Outcome}: {r.Message}";
                    if (!string.IsNullOrEmpty(line.Expect) && r != null && !string.Equals(r.Outcome.ToString(), line.Expect, StringComparison.OrdinalIgnoreCase)) return false;
                    return true;
                }
                case "wait":
                    if (!sim.CanType) { detail = "frozen or leaving"; return false; }
                    sim.Say("wait");
                    return true;
                case "expect":
                {
                    var (id, val) = GuideParser.KeyValue(line.Arg);
                    string now = sim.Room.Get(id);
                    detail = $"{id}={now ?? "(no such object)"}";
                    return now == val;
                }
                case "beat":
                    detail = sim.Ran.Contains(line.Arg) ? "ran" : "not run. Ran: " + string.Join(", ", sim.Ran);
                    return sim.Ran.Contains(line.Arg);
                case "nobeat":
                    detail = sim.Ran.Contains(line.Arg) ? "ran" : "not run";
                    return !sim.Ran.Contains(line.Arg);
                case "flag":
                    detail = "flags: " + string.Join(", ", sim.Flags);
                    return sim.Flags.Contains(line.Arg);
                case "noflag":
                    return !sim.Flags.Contains(line.Arg);
                case "verb":
                    return sim.Room.Def.Verbs.Contains(line.Arg);
                case "film":
                    detail = "films: " + string.Join(", ", sim.Films);
                    return sim.Films.Contains(line.Arg);
                case "codexhas":
                    detail = "codex: " + string.Join(", ", sim.Codex.OrderBy(x => x));
                    return sim.Codex.Contains(line.Arg);
                case "solved":
                    detail = Snapshot(sim);
                    return sim.Room.Solved;
                case "unsolved":
                    detail = Snapshot(sim);
                    return !sim.Room.Solved;
                case "frozen":
                    return sim.Frozen;
                case "free":
                    detail = sim.Frozen ? "frozen" : "free";
                    return !sim.Frozen;
                case "exit":
                {
                    detail = "exit " + (sim.ExitTo ?? "(none, still in the room)");
                    if (sim.ExitTo == null) return false;
                    return string.Equals(GuideScenes.Norm(sim.ExitTo), GuideScenes.Norm(line.Arg), StringComparison.OrdinalIgnoreCase);
                }
                case "noexit":
                    detail = "exit " + (sim.ExitTo ?? "none");
                    return sim.ExitTo == null;
                default:
                    detail = "unknown command " + line.Cmd;
                    return false;
            }
        }

        private static string Snapshot(RealmSim sim) => string.Join(" ", sim.Room.State.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value));
    }

    /// <summary>Writes the per-room table both runners share.</summary>
    public static class GuideReport
    {
        public static string AuditFolder()
        {
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            return Path.Combine(root, "art_gen", "realms", "audit");
        }

        public static void Write(string fileName, string title, IEnumerable<ScriptResult> results)
        {
            var list = results.ToList();
            var sb = new StringBuilder();
            sb.AppendLine("# " + title);
            sb.AppendLine();
            sb.AppendLine($"Run {DateTime.Now:yyyy-MM-dd HH:mm}. {list.Count(r => r.Pass && !r.Skipped)} pass, {list.Count(r => !r.Pass)} fail, {list.Count(r => r.Skipped)} skipped.");
            sb.AppendLine();
            sb.AppendLine("| Guide | Block | Mode | Result | Failed AC | Note |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (ScriptResult r in list)
                sb.AppendLine($"| {Path.GetFileNameWithoutExtension(r.Script.File)} | {r.Script.Name} | {r.Mode} | {(r.Skipped ? "skip" : r.Pass ? "PASS" : "FAIL")} | {string.Join(" ", r.FailedTags)} | {r.Summary.Replace("|", "/")} |");
            sb.AppendLine();
            foreach (ScriptResult r in list.Where(x => !x.Pass))
            {
                sb.AppendLine("```");
                sb.Append(r.Explain());
                sb.AppendLine("```");
            }
            Directory.CreateDirectory(AuditFolder());
            File.WriteAllText(Path.Combine(AuditFolder(), fileName), sb.ToString());
        }
    }
}
