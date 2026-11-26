using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ankhu.Tests.RoomGuides
{
    /// <summary>One command line from a room guide's e2e block (docs/rooms/*.md).</summary>
    public sealed class GuideLine
    {
        public int LineNo;
        public string Raw = string.Empty;
        public string Cmd = string.Empty;
        /// <summary>Everything after the command, with any "=> expect | message" and [AC-n] tags removed.</summary>
        public string Arg = string.Empty;
        /// <summary>For say: the expected judgement or outcome after "=>".</summary>
        public string Expect = string.Empty;
        /// <summary>For say: the text after "|" that the reply must contain.</summary>
        public string Message = string.Empty;
        public string[] Tags = Array.Empty<string>();

        public string Where(GuideScript s) => $"{Path.GetFileName(s.File)}:{LineNo} [{s.Name}] {Raw.Trim()}";
    }

    /// <summary>One fenced e2e block: a scripted playthrough of one room, checked against its acceptance criteria.</summary>
    public sealed class GuideScript
    {
        public string File = string.Empty;
        public string Name = string.Empty;
        public string Scene = string.Empty;
        public string Engine = "realm";
        public List<string> Progress = new List<string>();
        public List<string> Codex = new List<string>();
        public List<GuideLine> Lines = new List<GuideLine>();

        public string Id => $"{Path.GetFileNameWithoutExtension(File)}/{Name}";
        public override string ToString() => Id;
    }

    /// <summary>Reads the e2e blocks out of the room guides. The grammar is in docs/rooms/GUIDE_FORMAT.md.</summary>
    public static class GuideParser
    {
        private static readonly Regex Fence = new Regex(@"^\s*```\s*e2e\s+(\S+)\s*$");
        private static readonly Regex Tag = new Regex(@"\[(AC-[0-9A-Za-z.\-]+)\]");

        /// <summary>D:\TheAfterlifeofAnkhu\docs\rooms, found from the Unity project folder.</summary>
        public static string GuideFolder()
        {
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            return Path.Combine(root, "docs", "rooms");
        }

        public static List<GuideScript> LoadAll(string folder = null)
        {
            folder = folder ?? GuideFolder();
            var all = new List<GuideScript>();
            if (!Directory.Exists(folder)) return all;
            foreach (string f in Directory.GetFiles(folder, "*.md").OrderBy(x => x, StringComparer.Ordinal))
                all.AddRange(ParseFile(f));
            return all;
        }

        public static List<GuideScript> ParseFile(string path)
        {
            var result = new List<GuideScript>();
            string[] lines = System.IO.File.ReadAllLines(path);
            GuideScript cur = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                if (cur == null)
                {
                    Match m = Fence.Match(raw);
                    if (m.Success) cur = new GuideScript { File = path, Name = m.Groups[1].Value };
                    continue;
                }
                if (raw.Trim().StartsWith("```", StringComparison.Ordinal))
                {
                    result.Add(cur);
                    cur = null;
                    continue;
                }
                GuideLine g = ParseLine(raw, i + 1);
                if (g == null) continue;
                switch (g.Cmd)
                {
                    case "scene": cur.Scene = g.Arg; break;
                    case "engine": cur.Engine = g.Arg; break;
                    case "progress": cur.Progress.AddRange(g.Arg.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToUpperInvariant())); break;
                    case "codex": cur.Codex.AddRange(g.Arg.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)); break;
                    default: cur.Lines.Add(g); break;
                }
            }
            return result;
        }

        public static GuideLine ParseLine(string raw, int lineNo)
        {
            string s = raw;
            int hash = IndexOfComment(s);
            if (hash >= 0) s = s.Substring(0, hash);
            var tags = Tag.Matches(s).Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            s = Tag.Replace(s, string.Empty).Trim();
            if (s.Length == 0) return null;
            var g = new GuideLine { LineNo = lineNo, Raw = raw, Tags = tags };
            int sp = s.IndexOf(' ');
            g.Cmd = (sp < 0 ? s : s.Substring(0, sp)).ToLowerInvariant();
            string rest = sp < 0 ? string.Empty : s.Substring(sp + 1).Trim();
            int arrow = rest.IndexOf("=>", StringComparison.Ordinal);
            if (arrow >= 0)
            {
                string tail = rest.Substring(arrow + 2).Trim();
                rest = rest.Substring(0, arrow).Trim();
                int bar = tail.IndexOf('|');
                if (bar >= 0)
                {
                    g.Message = tail.Substring(bar + 1).Trim().Trim('"');
                    tail = tail.Substring(0, bar).Trim();
                }
                g.Expect = tail;
            }
            g.Arg = rest.Trim().Trim('"');
            return g;
        }

        /// <summary>A "#" starts a comment, except inside double quotes.</summary>
        private static int IndexOfComment(string s)
        {
            bool q = false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '"') q = !q;
                else if (s[i] == '#' && !q) return i;
            }
            return -1;
        }

        /// <summary>"door=open" into ("door", "open").</summary>
        public static (string id, string value) KeyValue(string arg)
        {
            int eq = arg.IndexOf('=');
            if (eq < 0) return (arg.Trim(), string.Empty);
            return (arg.Substring(0, eq).Trim(), arg.Substring(eq + 1).Trim());
        }
    }
}
