using System;
using System.Collections.Generic;
using System.Linq;

namespace Ankhu.Realms.Core
{
    /// <summary>
    /// Zac 2026-11-25, hint pick B: the three sealed hint steps for a realm room. The nudge is the room's
    /// authored <see cref="RealmRoom.Hint"/>. The direction names the thing that must change next and the answer
    /// is the next utterance, both from a breadth first search over the room's own rules from the current state.
    /// Reach is ignored (Ankhu can walk), story beats that force states are not modelled, so when no path is
    /// found only the nudge is returned.
    /// </summary>
    public static class RealmHintSolver
    {
        public const int MaxStates = 6000;

        public sealed class Step
        {
            public string Utterance;
            public string TargetId;
            public bool Deed;
            /// <summary>Where Ankhu stands to say it: the target's first zone, or null for anywhere.</summary>
            public string Zone;
        }

        /// <summary>Nudge, direction, answer, or just the nudge when the search finds no way.</summary>
        public static string[] Tiers(RealmRoom room)
        {
            if (room == null) return null;
            string nudge = room.Hint();
            if (room.Solved) return new[] { nudge };
            Step next = NextStep(room);
            if (next == null) return new[] { nudge };
            RoomObject o = string.IsNullOrEmpty(next.TargetId) ? null : room.Def.Find(next.TargetId);
            string name = o != null && !string.IsNullOrEmpty(o.Name) ? o.Name : null;
            string direction = next.Utterance == "wait"
                ? "Nothing needs a word yet. Let the room move."
                : name != null ? "Something must change the " + name.ToLowerInvariant() + "." : "The room is waiting for a name.";
            string answer = next.Deed
                ? "With your hands, at the " + (name ?? "thing").ToLowerInvariant() + ": press E to " + next.Utterance.Split(' ')[0] + "."
                : "Say: " + next.Utterance + ".";
            return new[] { nudge, direction, answer };
        }

        /// <summary>The first step of one shortest path to the room's goal, or null.</summary>
        public static Step NextStep(RealmRoom room)
        {
            RoomDef def = room.Def;
            if (def.Goal.Length == 0) return null;
            List<Step> moves = Candidates(def);
            if (moves.Count == 0) return null;

            var start = new Dictionary<string, string>(room.State.ToDictionary(k => k.Key, k => k.Value));
            var seen = new HashSet<string> { Key(start) };
            var queue = new Queue<(Dictionary<string, string> s, Step first)>();
            queue.Enqueue((start, null));
            var probe = new RealmRoom(def);
            while (queue.Count > 0 && seen.Count < MaxStates)
            {
                var (s, first) = queue.Dequeue();
                foreach (Step m in moves)
                {
                    Load(probe, s);
                    SpellResult r;
                    try { r = probe.Speak(m.Utterance, m.Zone); }
                    catch (Exception) { continue; }
                    if (!r.Accepted && r.Changes.Count == 0) continue;
                    var after = probe.State.ToDictionary(k => k.Key, k => k.Value);
                    if (!seen.Add(Key(after))) continue;
                    Step f = first ?? m;
                    if (probe.Solved) return f;
                    queue.Enqueue((after, f));
                }
            }
            return null;
        }

        private static List<Step> Candidates(RoomDef def)
        {
            var list = new List<Step>();
            var said = new HashSet<string>();
            foreach (SpellRule rule in def.Rules)
            {
                if (rule.Verb == "auto") continue;
                string u;
                if (rule.Verb == "name")
                {
                    if (string.IsNullOrEmpty(rule.NameValue)) continue;
                    u = rule.NameValue;
                }
                else if (rule.Target != null)
                {
                    RoomObject o = def.Find(rule.Target);
                    string alias = o != null && o.Aliases.Length > 0 ? o.Aliases[0] : o?.Name?.ToLowerInvariant();
                    if (string.IsNullOrEmpty(alias)) continue;
                    u = rule.Verb + " " + alias;
                }
                else
                {
                    u = rule.Verb;
                }
                // Room guides 2026-11-26: speak from where the target can hear, so aliases shared by several
                // objects ("shabti", "sluice") resolve by reach instead of coming back Ambiguous.
                RoomObject at = rule.Target != null ? def.Find(rule.Target) : null;
                string zone = at != null && at.Zones.Length > 0 ? at.Zones[0] : null;
                if (!said.Add(u + "@" + zone)) continue;
                list.Add(new Step { Utterance = u, TargetId = rule.Target, Deed = def.Deeds.Contains(rule.Verb), Zone = zone });
            }
            if (def.Tick != null && said.Add("wait")) list.Add(new Step { Utterance = "wait" });
            return list;
        }

        private static void Load(RealmRoom probe, Dictionary<string, string> s)
        {
            probe.Reset();
            foreach (var kv in s) probe.Force(kv.Key, kv.Value);
        }

        private static string Key(Dictionary<string, string> s)
        {
            return string.Join("|", s.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + "=" + k.Value));
        }
    }
}
