using System;
using System.Collections.Generic;
using System.Linq;

namespace Ankhu.BorrowedDawn.Analysis
{
    /// <summary>One accepted transition in the reachable graph.</summary>
    public readonly struct Edge
    {
        public Edge(int from, int to, Command command)
        {
            From = from;
            To = to;
            Command = command;
        }

        public int From { get; }
        public int To { get; }
        public Command Command { get; }
    }

    /// <summary>
    /// Exhaustive breadth-first exploration of every state reachable from a start state using
    /// <see cref="RoomRules.AllCommands"/>. Undo and reset are excluded, so "softlock" here means a state
    /// from which the gate can no longer be opened by play alone.
    /// </summary>
    public sealed class StateSpace
    {
        public RoomState Start { get; }
        public RuleProfile Profile => Room.Profile;
        public RoomDefinition Room { get; }
        /// <summary>Reachable states keyed by <see cref="RoomState.Pack"/>.</summary>
        public IReadOnlyDictionary<int, RoomState> States => _states;
        public IReadOnlyList<Edge> Edges => _edges;
        /// <summary>BFS depth from <see cref="Start"/> (minimum accepted commands).</summary>
        public IReadOnlyDictionary<int, int> DepthFromStart => _depth;
        /// <summary>Minimum commands from each state to any won state. Missing key means the goal is unreachable.</summary>
        public IReadOnlyDictionary<int, int> DistanceToGoal => _toGoal;

        private readonly Dictionary<int, RoomState> _states = new Dictionary<int, RoomState>();
        private readonly List<Edge> _edges = new List<Edge>();
        private readonly Dictionary<int, int> _depth = new Dictionary<int, int>();
        private readonly Dictionary<int, (int parent, Command cmd)> _parent = new Dictionary<int, (int, Command)>();
        private readonly Dictionary<int, int> _toGoal = new Dictionary<int, int>();
        private readonly Dictionary<int, List<int>> _reverse = new Dictionary<int, List<int>>();

        private StateSpace(RoomDefinition room, RoomState start)
        {
            Room = room;
            Start = start;
        }

        public static StateSpace Explore(RoomState start = null, RuleProfile profile = RuleProfile.Prototype) =>
            Explore(RoomDefinition.BorrowedDawn(profile), start);

        /// <summary>Explores a room under its own vocabulary and goal. Goal states are not expanded.</summary>
        public static StateSpace Explore(RoomDefinition room, RoomState start = null)
        {
            if (room is null) throw new ArgumentNullException(nameof(room));
            var space = new StateSpace(room, start ?? room.Start);
            space.Forward();
            space.Backward();
            return space;
        }

        private void Forward()
        {
            int s0 = Start.Pack();
            _states[s0] = Start;
            _depth[s0] = 0;
            var queue = new Queue<int>();
            queue.Enqueue(s0);
            while (queue.Count > 0)
            {
                int k = queue.Dequeue();
                RoomState s = _states[k];
                foreach (Command c in RoomRules.AllCommands)
                {
                    CommandResult r = Room.Apply(s, c);
                    if (!r.Accepted) continue;
                    int n = r.State.Pack();
                    _edges.Add(new Edge(k, n, c));
                    if (!_reverse.TryGetValue(n, out var preds)) _reverse[n] = preds = new List<int>();
                    preds.Add(k);
                    if (_states.ContainsKey(n)) continue;
                    _states[n] = r.State;
                    _depth[n] = _depth[k] + 1;
                    _parent[n] = (k, c);
                    queue.Enqueue(n);
                }
            }
        }

        private void Backward()
        {
            var queue = new Queue<int>();
            foreach (var kv in _states)
            {
                if (Room.IsGoal(kv.Value))
                {
                    _toGoal[kv.Key] = 0;
                    queue.Enqueue(kv.Key);
                }
            }
            while (queue.Count > 0)
            {
                int k = queue.Dequeue();
                if (!_reverse.TryGetValue(k, out var preds)) continue;
                foreach (int p in preds)
                {
                    if (_toGoal.ContainsKey(p)) continue;
                    _toGoal[p] = _toGoal[k] + 1;
                    queue.Enqueue(p);
                }
            }
        }

        public bool GoalReachable => _toGoal.ContainsKey(Start.Pack());

        /// <summary>Non-won reachable states from which no won state is reachable.</summary>
        public IEnumerable<RoomState> Softlocks => _states.Where(kv => !Room.IsGoal(kv.Value) && !_toGoal.ContainsKey(kv.Key)).Select(kv => kv.Value);

        /// <summary>Non-won states with no accepted outgoing command at all.</summary>
        public IEnumerable<RoomState> DeadEnds
        {
            get
            {
                var hasOut = new HashSet<int>(_edges.Select(e => e.From));
                return _states.Where(kv => !Room.IsGoal(kv.Value) && !hasOut.Contains(kv.Key)).Select(kv => kv.Value);
            }
        }

        /// <summary>One shortest command sequence from start to a won state, or null.</summary>
        public IReadOnlyList<Command> ShortestSolution()
        {
            int best = -1;
            foreach (var kv in _states)
            {
                if (Room.IsGoal(kv.Value) && (best < 0 || _depth[kv.Key] < _depth[best])) best = kv.Key;
            }
            if (best < 0) return null;
            return PathTo(best);
        }

        public IReadOnlyList<Command> PathTo(int packed)
        {
            var path = new List<Command>();
            int k = packed;
            while (_parent.TryGetValue(k, out var p))
            {
                path.Add(p.cmd);
                k = p.parent;
            }
            path.Reverse();
            return path;
        }

        /// <summary>Number of distinct shortest solutions (counted over the BFS layer DAG).</summary>
        public long CountShortestSolutions()
        {
            int startKey = Start.Pack();
            if (!_toGoal.TryGetValue(startKey, out int len)) return 0;
            var memo = new Dictionary<int, long>();
            long Count(int k)
            {
                if (Room.IsGoal(_states[k])) return 1;
                if (memo.TryGetValue(k, out long v)) return v;
                long total = 0;
                int d = _toGoal[k];
                foreach (var e in _edges)
                {
                    if (e.From == k && _toGoal.TryGetValue(e.To, out int dt) && dt == d - 1) total += Count(e.To);
                }
                memo[k] = total;
                return total;
            }
            return Count(startKey);
        }

        /// <summary>
        /// Shortest solution under an extra restriction on which commands may be used or which states may be
        /// visited. Returns null when the restriction makes the room unsolvable. Used to ask design questions
        /// such as "can the room be solved without ever binding the elevator".
        /// </summary>
        public static IReadOnlyList<Command> ShortestSolutionWhere(Func<RoomState, Command, bool> allowCommand, Func<RoomState, bool> allowState = null, RoomState start = null, RuleProfile profile = RuleProfile.Prototype) =>
            ShortestSolutionWhere(RoomDefinition.BorrowedDawn(profile), allowCommand, allowState, start);

        public static IReadOnlyList<Command> ShortestSolutionWhere(RoomDefinition room, Func<RoomState, Command, bool> allowCommand, Func<RoomState, bool> allowState = null, RoomState start = null)
        {
            start = start ?? room.Start;
            var parent = new Dictionary<int, (int, Command)>();
            var seen = new HashSet<int> { start.Pack() };
            var queue = new Queue<RoomState>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                RoomState s = queue.Dequeue();
                if (room.IsGoal(s))
                {
                    var path = new List<Command>();
                    int k = s.Pack();
                    while (parent.TryGetValue(k, out var p))
                    {
                        path.Add(p.Item2);
                        k = p.Item1;
                    }
                    path.Reverse();
                    return path;
                }
                foreach (Command c in RoomRules.AllCommands)
                {
                    if (!allowCommand(s, c)) continue;
                    CommandResult r = room.Apply(s, c);
                    if (!r.Accepted) continue;
                    if (allowState != null && !allowState(r.State)) continue;
                    int n = r.State.Pack();
                    if (!seen.Add(n)) continue;
                    parent[n] = (s.Pack(), c);
                    queue.Enqueue(r.State);
                }
            }
            return null;
        }
    }
}
