using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ankhu.BorrowedDawn;

namespace DiffFuzz
{
    // ------------------------------------------------------------------ JSON rows written by js_reference.js
    public sealed class JsState
    {
        public string water { get; set; }
        public string loc { get; set; }
        public string[] bindings { get; set; }
        public string mirror { get; set; }
        public bool awake { get; set; }
        public bool read { get; set; }
        public bool bell { get; set; }
        public bool struck { get; set; }
        public bool echo { get; set; }
        public bool won { get; set; }
    }

    public sealed class JsAction
    {
        public string t { get; set; }
        public string text { get; set; }
        public string to { get; set; }
    }

    public sealed class JsRow
    {
        public JsState state { get; set; }
        public JsAction action { get; set; }
        public bool accepted { get; set; }
        public bool changed { get; set; }
        public JsState next { get; set; }
        public string msg { get; set; }
        public string interp { get; set; }
    }

    internal sealed class Tally
    {
        public readonly ConcurrentDictionary<string, long> Counts = new ConcurrentDictionary<string, long>();
        public readonly ConcurrentDictionary<string, ConcurrentQueue<string>> Examples = new ConcurrentDictionary<string, ConcurrentQueue<string>>();
        public readonly ConcurrentDictionary<string, string> ExampleKeys = new ConcurrentDictionary<string, string>();
        public int MaxExamples = 5;

        public void Add(string category, string example = null, string dedupKey = null)
        {
            long n = Counts.AddOrUpdate(category, 1, (_, v) => v + 1);
            if (example == null) return;
            var q = Examples.GetOrAdd(category, _ => new ConcurrentQueue<string>());
            if (dedupKey != null && !ExampleKeys.TryAdd(category + "\u0001" + dedupKey, "")) return;
            // Unexpected mismatches keep up to MaxExamples repros; expected/known categories keep one sample.
            int cap = category.StartsWith("UNEXPECTED") || category.StartsWith("ALIAS") || category.StartsWith("JS:") ? MaxExamples : 1;
            if (q.Count < cap) q.Enqueue(example);
        }
    }

    public static class Program
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions();

        // ------------------------------------------------------------------ JS <-> C# conversion
        public static RoomState ToRoom(JsState j)
        {
            WaterSide w = j.water switch { "east" => WaterSide.East, "west" => WaterSide.West, _ => throw new FormatException("water " + j.water) };
            Location l = ParseLoc(j.loc) ?? throw new FormatException("loc " + j.loc);
            BindingSet b = BindingSet.None;
            foreach (var t in j.bindings)
            {
                BindingSet f = t switch { "elevator" => BindingSet.Elevator, "shadow" => BindingSet.Shadow, "bridge" => BindingSet.Bridge, _ => throw new FormatException("binding " + t) };
                if ((b & f) != 0) throw new FormatException("duplicate binding " + t);
                b |= f;
            }
            MirrorPlace m = j.mirror switch { "raft" => MirrorPlace.Raft, "carried" => MirrorPlace.Carried, "socket" => MirrorPlace.Socket, _ => throw new FormatException("mirror " + j.mirror) };
            return new RoomState(w, l, b, m, j.awake, j.read, j.bell, j.struck, j.echo, j.won);
        }

        public static RoomState TryToRoom(JsState j, out string error)
        {
            try { error = null; return ToRoom(j); }
            catch (Exception e) when (e is FormatException || e is ArgumentException) { error = e.Message; return null; }
        }

        /// <summary>Converts both sides of a row; on failure records an UNEXPECTED category and returns false.</summary>
        private static bool Convert(JsRow row, Tally t, out RoomState before, out RoomState jsNext)
        {
            before = TryToRoom(row.state, out var e1);
            jsNext = TryToRoom(row.next, out var e2);
            if (before != null && jsNext != null) return true;
            t.Add("UNEXPECTED JS state not representable as RoomState", "    " + (e1 ?? e2) + ": " + JsonSerializer.Serialize(row) + "\n",
                JsonSerializer.Serialize(before == null ? row.state : row.next));
            return false;
        }

        public static Location? ParseLoc(string s) => s switch
        {
            "floor" => Location.Floor, "lift" => Location.Lift, "gallery" => Location.Gallery,
            "landing" => Location.Landing, "gate" => Location.Gate, _ => (Location?)null,
        };

        /// <summary>The "obvious" mapping from the prototype's typed strings to C# commands (no parser involved).</summary>
        public static readonly Dictionary<string, Command> DirectMap = new Dictionary<string, Command>
        {
            { "flow west", Command.Flow(WaterSide.West) }, { "flow east", Command.Flow(WaterSide.East) },
            { "bind elevator", Command.Bind(BindTarget.Elevator) }, { "bind shadow", Command.Bind(BindTarget.Shadow) }, { "bind bridge", Command.Bind(BindTarget.Bridge) },
            { "release elevator", Command.Release(BindTarget.Elevator) }, { "release shadow", Command.Release(BindTarget.Shadow) }, { "release bridge", Command.Release(BindTarget.Bridge) },
            { "wake bearer", Command.WakeBearer }, { "wake statue", Command.WakeBearer },
            { "sleep bearer", Command.SleepBearer }, { "sleep statue", Command.SleepBearer },
            { "recall bell", Command.RecallBell }, { "strike bell", Command.StrikeBell }, { "echo bell", Command.EchoBell },
            { "take mirror", Command.TakeMirror }, { "install mirror", Command.InstallMirror },
            { "read inscription", Command.ReadInscription }, { "read gallery", Command.ReadInscription },
            { "he who returns the morning", Command.SpeakName }, { "he who returns morning", Command.SpeakName },
        };

        private static readonly Dictionary<Location, Location[]> UiNeighbours = new Dictionary<Location, Location[]>
        {
            { Location.Floor, new[] { Location.Lift } },
            { Location.Lift, new[] { Location.Floor, Location.Gallery } },
            { Location.Gallery, new[] { Location.Floor, Location.Landing } },
            { Location.Landing, new[] { Location.Gallery, Location.Gate } },
            { Location.Gate, new[] { Location.Landing } },
        };

        private static string Lc(Location l) => l.ToString().ToLowerInvariant();

        /// <summary>Canonical interpretation label for the C# side, in the same vocabulary as jsInterpret().</summary>
        private static string CsInterp(ParseResult p)
        {
            if (!p.Success) return "PARSE_" + p.Status;
            var c = p.Command;
            if (c.Kind == CommandKind.MoveTo) return "go to " + Lc(c.Destination);
            return c.ToString();
        }

        private static string Show(RoomState r) => r.ToString();
        private static string Q(string s) => JsonSerializer.Serialize(s ?? "");

        /// <summary>Compares one JS outcome against one C# outcome. Returns null on exact match, else a category.</summary>
        private static string Classify(JsRow row, RoomState before, RoomState jsNext, Command cmd, CommandResult cs)
        {
            bool csAcc = cs != null && cs.Accepted;
            RoomState csNext = cs?.State ?? before;
            if (row.accepted == csAcc && jsNext == csNext) return null;
            if (cmd != null && cmd.Kind == CommandKind.StrikeBell && row.accepted && !row.changed && jsNext == before
                && cs != null && cs.Outcome == Outcome.AlreadyInState && cs.State == before)
                return "EXPECTED (a) strike bell when already struck: JS accepts no-op, C# AlreadyInState";
            if (row.accepted != csAcc) return "UNEXPECTED decision mismatch";
            return "UNEXPECTED resulting-state mismatch";
        }

        private static string Repro(string where, JsRow row, RoomState before, RoomState jsNext, string csDesc, CommandResult cs)
        {
            var sb = new StringBuilder();
            sb.Append("  [").Append(where).Append("]\n");
            sb.Append("    state : ").Append(JsonSerializer.Serialize(row.state)).Append("  (C# ").Append(Show(before)).Append(", Pack=").Append(before.Pack()).Append(")\n");
            sb.Append("    action: ").Append(row.action.t == "move" ? "JS move(" + Q(row.action.to) + ")" : "JS command(" + Q(row.action.text) + ")")
              .Append("  vs C# ").Append(csDesc).Append('\n');
            sb.Append("    JS    : accepted=").Append(row.accepted).Append(" next=").Append(Show(jsNext)).Append(" msg=").Append(Q(row.msg)).Append('\n');
            sb.Append("    C#    : ").Append(cs == null ? "not applied (parse failed)" : "accepted=" + cs.Accepted + " outcome=" + cs.Outcome + " next=" + Show(cs.State) + " msg=" + Q(cs.Message)).Append('\n');
            return sb.ToString();
        }

        private static IEnumerable<JsRow> ReadRows(string path) =>
            File.ReadLines(path).Where(l => l.Length > 0).Select(l => JsonSerializer.Deserialize<JsRow>(l, Json));

        public static int Main(string[] args)
        {
            string bfsPath = null;
            var fuzzPaths = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--bfs") bfsPath = args[++i];
                else if (args[i] == "--fuzz") fuzzPaths.Add(args[++i]);
                else { Console.Error.WriteLine("usage: DiffFuzz --bfs <bfs.jsonl> [--fuzz <fuzz.jsonl>]..."); return 2; }
            }
            if (bfsPath == null) { Console.Error.WriteLine("--bfs is required"); return 2; }

            Console.WriteLine($"DiffFuzz: {Environment.ProcessorCount} logical CPUs, Parallel.ForEach over rows");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int unexpected = 0;

            // ================================================================== Part 1: exhaustive BFS table
            var bfsRows = ReadRows(bfsPath).ToArray();
            // JS table keyed by (Pack, action) so alias divergences in Part 2 can be checked against the canonical spelling.
            var jsTable = new Dictionary<(int, string), JsRow>();
            var jsStates = new Dictionary<int, JsRow>();
            foreach (var r in bfsRows)
            {
                var rs = TryToRoom(r.state, out _);
                if (rs == null) continue;
                int k = rs.Pack();
                jsTable[(k, r.action.t == "move" ? "move:" + r.action.to : r.action.text)] = r;
                if (!jsStates.ContainsKey(k)) jsStates[k] = r;
            }

            var bfs = new Tally { MaxExamples = int.MaxValue };
            long comparisons = 0;
            Parallel.ForEach(bfsRows, row =>
            {
                if (!Convert(row, bfs, out var before, out var jsNext)) return;
                if (RoomState.Unpack(before.Pack()) != before) bfs.Add("UNEXPECTED Pack/Unpack round-trip failure", Show(before));
                if (row.action.t == "move")
                {
                    Location to = ParseLoc(row.action.to).Value;
                    var cmd = Command.MoveTo(to);
                    var cs = RoomRules.Apply(before, cmd);
                    Interlocked.Increment(ref comparisons);
                    string cat = Classify(row, before, jsNext, cmd, cs);
                    bfs.Add(cat ?? "match: neighbour move vs MoveTo", cat == null ? null : Repro("bfs move", row, before, jsNext, "Apply(MoveTo(" + to + "))", cs));
                    // CrossBridge is the C# counterpart of the UI's "Cross bridge"/"Return across bridge" buttons.
                    if ((before.Location == Location.Landing && to == Location.Gate) || (before.Location == Location.Gate && to == Location.Landing))
                    {
                        var cb = RoomRules.Apply(before, Command.CrossBridge);
                        Interlocked.Increment(ref comparisons);
                        string cat2 = Classify(row, before, jsNext, Command.CrossBridge, cb);
                        bfs.Add(cat2 ?? "match: neighbour move vs CrossBridge", cat2 == null ? null : Repro("bfs cross", row, before, jsNext, "Apply(CrossBridge)", cb));
                    }
                }
                else
                {
                    string text = row.action.text;
                    if (DirectMap.TryGetValue(text, out var cmd))
                    {
                        var cs = RoomRules.Apply(before, cmd);
                        Interlocked.Increment(ref comparisons);
                        string cat = Classify(row, before, jsNext, cmd, cs);
                        bfs.Add(cat ?? "match: typed command vs direct Command", cat == null ? null : Repro("bfs direct", row, before, jsNext, "Apply(" + cmd + ")", cs));
                    }
                    // Same text through the C# parser.
                    var p = SpellParser.Parse(text);
                    var csp = p.Success ? RoomRules.Apply(before, p.Command) : null;
                    Interlocked.Increment(ref comparisons);
                    string catp = Classify(row, before, jsNext, p.Command, csp);
                    if (catp == null) bfs.Add("match: typed command vs SpellParser+Apply");
                    else if (CsInterp(p) != row.interp && !catp.StartsWith("EXPECTED"))
                        bfs.Add("ALIAS JS:" + row.interp + " / C#:" + CsInterp(p), Repro("bfs parser", row, before, jsNext, "SpellParser(" + Q(text) + ") -> " + CsInterp(p), csp));
                    else bfs.Add(catp, catp.StartsWith("EXPECTED") ? null : Repro("bfs parser", row, before, jsNext, "SpellParser(" + Q(text) + ") -> " + CsInterp(p), csp));
                }
            });

            // C#-only checks per distinct JS state: MoveTo to non-neighbours and to self must be rejected; CrossBridge elsewhere rejected.
            var extra = new Tally();
            Parallel.ForEach(jsStates.Keys, k =>
            {
                var st = RoomState.Unpack(k);
                foreach (Location l in Enum.GetValues(typeof(Location)))
                {
                    if (UiNeighbours[st.Location].Contains(l)) continue;
                    var r = RoomRules.Apply(st, Command.MoveTo(l));
                    Interlocked.Increment(ref comparisons);
                    string kind = l == st.Location ? "self" : "non-neighbour";
                    if (r.Accepted || r.State != st)
                        extra.Add("UNEXPECTED C# accepted MoveTo " + kind, "    state " + Show(st) + " MoveTo(" + l + ") -> " + r.Outcome);
                    else extra.Add("EXPECTED (b) C# rejects MoveTo " + kind + " (outcome " + r.Outcome + ")");
                }
                if (st.Location != Location.Landing && st.Location != Location.Gate)
                {
                    var r = RoomRules.Apply(st, Command.CrossBridge);
                    Interlocked.Increment(ref comparisons);
                    if (r.Accepted) extra.Add("UNEXPECTED C# CrossBridge accepted away from landing/gate", "    " + Show(st));
                    else extra.Add("C#-only CrossBridge away from landing/gate rejected (no JS counterpart)");
                }
            });

            // Reachable-set cross-check: BFS in C# over RoomRules.AllCommands from Initial.
            var csReach = new HashSet<int> { RoomState.Initial.Pack() };
            var queue = new Queue<RoomState>(new[] { RoomState.Initial });
            while (queue.Count > 0)
            {
                var st = queue.Dequeue();
                foreach (var c in RoomRules.AllCommands)
                {
                    var r = RoomRules.Apply(st, c);
                    if (r.Accepted && csReach.Add(r.State.Pack())) queue.Enqueue(r.State);
                }
            }
            var jsReach = new HashSet<int>(jsStates.Keys);
            int onlyJs = jsReach.Count(x => !csReach.Contains(x)), onlyCs = csReach.Count(x => !jsReach.Contains(x));

            Console.WriteLine();
            Console.WriteLine("==================== PART 1: exhaustive BFS ====================");
            Console.WriteLine($"JS reachable states (BFS over typed commands + UI neighbour moves): {jsStates.Count}");
            Console.WriteLine($"C# reachable states (BFS over RoomRules.AllCommands):              {csReach.Count}  (only-JS {onlyJs}, only-C# {onlyCs})");
            Console.WriteLine($"JS (state, action) rows: {bfsRows.Length};  C# comparisons performed: {comparisons}");
            foreach (var t in new[] { bfs, extra })
                foreach (var kv in t.Counts.OrderBy(k => k.Key))
                    Console.WriteLine($"  {kv.Value,8}  {kv.Key}");
            if (onlyJs + onlyCs > 0)
            {
                Console.WriteLine("  reachable-set difference (first 20):");
                foreach (var x in jsReach.Where(x => !csReach.Contains(x)).Take(20)) Console.WriteLine("    only JS: " + Show(RoomState.Unpack(x)));
                foreach (var x in csReach.Where(x => !jsReach.Contains(x)).Take(20)) Console.WriteLine("    only C#: " + Show(RoomState.Unpack(x)));
            }
            foreach (var t in new[] { bfs, extra })
                foreach (var kv in t.Examples.OrderBy(k => k.Key))
                {
                    if (kv.Key.StartsWith("UNEXPECTED")) unexpected += (int)t.Counts[kv.Key];
                    Console.WriteLine($"--- {kv.Key} ({t.Counts[kv.Key]}):");
                    foreach (var e in kv.Value.Take(30)) Console.Write(e.EndsWith("\n") ? e : e + "\n");
                    if (kv.Value.Count > 30) Console.WriteLine($"  ... {kv.Value.Count - 30} more");
                }

            // ================================================================== Part 2: random-walk fuzz
            if (fuzzPaths.Count > 0)
            {
                var fz = new Tally { MaxExamples = 4 };
                var interpDiv = new Tally { MaxExamples = 4 };
                long total = 0;
                foreach (var path in fuzzPaths)
                {
                    Parallel.ForEach(ReadRows(path), row =>
                    {
                        Interlocked.Increment(ref total);
                        if (!Convert(row, fz, out var before, out var jsNext)) return;
                        if (row.action.t == "move")
                        {
                            var cmd = Command.MoveTo(ParseLoc(row.action.to).Value);
                            var cs = RoomRules.Apply(before, cmd);
                            string cat = Classify(row, before, jsNext, cmd, cs);
                            fz.Add(cat ?? "match: move", cat == null ? null : Repro("fuzz move", row, before, jsNext, "Apply(" + cmd + ")", cs));
                            return;
                        }
                        string text = row.action.text;
                        var p = SpellParser.Parse(text);
                        var csr = p.Success ? RoomRules.Apply(before, p.Command) : null;
                        string ci = CsInterp(p), ji = row.interp;
                        bool sameInterp = ci == ji
                            || (ji == "EMPTY" || ji == "UNKNOWN" || ji == "HELP" || ji == "BIND_INVALID" || ji == "RELEASE_INVALID") && !p.Success;
                        string catp = Classify(row, before, jsNext, p.Command, csr);
                        string exampleKey = text;
                        if (!sameInterp)
                            interpDiv.Add("JS:" + ji + " / C#:" + ci + (catp == null ? "  [same decision+state here]" : "  [DIFFERENT result]"),
                                "    " + Q(text), exampleKey);
                        if (catp == null) { fz.Add(sameInterp ? "match" : "match despite different interpretation"); return; }
                        if (catp.StartsWith("EXPECTED")) { fz.Add(catp); return; }
                        if (sameInterp) { fz.Add(catp + " (same interpretation)", Repro("fuzz", row, before, jsNext, "SpellParser(" + Q(text) + ") -> " + ci, csr), Show(before) + text); return; }
                        // Different interpretation: is C# consistent with what JS does for the canonical spelling C# chose?
                        string verdict;
                        if (!p.Success)
                            verdict = "ALIAS: JS accepts a form C# parser rejects";
                        else if (p.Command.Kind == CommandKind.MoveTo || p.Command.Kind == CommandKind.CrossBridge)
                            verdict = "ALIAS: C#-only movement verb (JS has no typed movement)";
                        else if (jsTable.TryGetValue((before.Pack(), p.Command.ToString()), out var canon))
                        {
                            var canonNext = TryToRoom(canon.next, out _);
                            var canonCat = canonNext == null ? "UNEXPECTED" : Classify(canon, before, canonNext, p.Command, csr);
                            verdict = canonCat == null || canonCat.StartsWith("EXPECTED")
                                ? "ALIAS: C# accepts a form JS does not understand (C# result == JS result for canonical \"" + p.Command + "\")"
                                : "UNEXPECTED alias whose C# result differs from JS canonical \"" + p.Command + "\"";
                        }
                        else verdict = "UNEXPECTED alias with no JS canonical row for this state";
                        fz.Add(verdict, verdict.StartsWith("UNEXPECTED") ? Repro("fuzz alias", row, before, jsNext, "SpellParser(" + Q(text) + ") -> " + ci, csr) : null);
                    });
                }

                Console.WriteLine();
                Console.WriteLine("==================== PART 2: random-walk fuzz ====================");
                Console.WriteLine($"actions compared: {total} (from {fuzzPaths.Count} shard file(s))");
                foreach (var kv in fz.Counts.OrderBy(k => k.Key)) Console.WriteLine($"  {kv.Value,8}  {kv.Key}");
                foreach (var kv in fz.Examples.OrderBy(k => k.Key))
                {
                    if (!kv.Key.StartsWith("UNEXPECTED")) continue;
                    unexpected += (int)fz.Counts[kv.Key];
                    Console.WriteLine($"--- {kv.Key} ({fz.Counts[kv.Key]}), up to 4 distinct examples:");
                    foreach (var e in kv.Value) Console.Write(e);
                }
                Console.WriteLine();
                Console.WriteLine("Interpretation divergences (JS normalization+dispatch vs C# SpellParser), by category, with up to 4 distinct inputs each:");
                foreach (var kv in interpDiv.Counts.OrderByDescending(k => k.Value))
                {
                    Console.WriteLine($"  {kv.Value,8}  {kv.Key}");
                    if (interpDiv.Examples.TryGetValue(kv.Key, out var ex)) foreach (var e in ex) Console.WriteLine("            " + e.Trim());
                }
            }

            Console.WriteLine();
            Console.WriteLine($"UNEXPECTED mismatches total: {unexpected}   (elapsed {sw.Elapsed.TotalSeconds:F1}s)");
            return unexpected == 0 ? 0 : 1;
        }
    }
}
