using System;
using System.Collections.Generic;

namespace PhysicsReversi
{
    // Each physical line fires on formation, not on a player action or global settle.
    public sealed class RealtimeCaptures
    {
        sealed class Latch
        {
            public int[] Participants;
            public double AbsentTime;
        }
        readonly Dictionary<string, Latch> latches = new Dictionary<string, Latch>();
        static readonly int[,] Directions = { { 1, 0 }, { 0, 1 }, { 1, 1 }, { -1, 1 } };
        public static HashSet<int> PlayerChanges(BoardRules.Snapshot previous, BoardRules.Snapshot current,
            Dictionary<int, MotionOrigin> origins)
        {
            var changed = new HashSet<int>();
            for (int cell = 0; cell < 64; cell++)
            {
                int id = current.Ids[cell];
                if (id < 0 || !origins.TryGetValue(id, out var cause) || cause == null || !cause.CanCapture) continue;
                int before = Array.IndexOf(previous.Ids, id);
                if (before != cell || previous.Owners[before] != current.Owners[cell]) changed.Add(id);
            }
            return changed;
        }

        public List<int> Scan(BoardRules.Snapshot board, HashSet<int> unavailable, double deltaSeconds, double rearmSeconds,
            HashSet<int> playerChanged = null, HashSet<int> activatedParticipants = null, HashSet<int> capturingEnds = null)
        {
            var present = new HashSet<string>();
            var targets = new HashSet<int>();
            for (int start = 0; start < 64; start++)
            {
                int owner = board.Owners[start];
                if (owner == 0 || board.Ids[start] < 0) continue;
                for (int direction = 0; direction < 4; direction++)
                {
                    int dx = Directions[direction, 0], dz = Directions[direction, 1];
                    int x = start % 8 + dx, z = start / 8 + dz;
                    var middle = new List<int>();
                    while (x >= 0 && x < 8 && z >= 0 && z < 8)
                    {
                        int cell = z * 8 + x;
                        if (board.Owners[cell] == 0 || board.Ids[cell] < 0) break;
                        if (board.Owners[cell] == owner)
                        {
                            if (middle.Count > 0)
                            {
                                int a = board.Ids[start], b = board.Ids[cell];
                                var interior = new List<int>(middle); interior.Sort();
                                // Color and coordinates are excluded: moving the intact line or
                                // reversing its faces must not continually retrigger that same line.
                                string key = Math.Min(a, b) + ":" + Math.Max(a, b) + "/" + string.Join(",", interior);
                                present.Add(key);
                                if (latches.TryGetValue(key, out var existing)) existing.AbsentTime = 0;
                                else
                                {
                                    var participants = new List<int>(middle) { a, b };
                                    bool blocked = participants.Exists(id => unavailable.Contains(id));
                                    bool playerCaused = playerChanged == null || participants.Exists(id => playerChanged.Contains(id));
                                    if (!blocked && playerCaused)
                                    {
                                        latches[key] = new Latch { Participants = participants.ToArray() };
                                        foreach (int id in middle) targets.Add(id);
                                        if (activatedParticipants != null) foreach (int id in participants) activatedParticipants.Add(id);
                                        if (capturingEnds != null) { capturingEnds.Add(a); capturingEnds.Add(b); }
                                    }
                                }
                            }
                            break;
                        }
                        middle.Add(board.Ids[cell]); x += dx; z += dz;
                    }
                }
            }
            var remove = new List<string>();
            foreach (var pair in latches)
            {
                if (present.Contains(pair.Key)) continue;
                bool blocked = Array.Exists(pair.Value.Participants, id => unavailable.Contains(id));
                if (blocked) { pair.Value.AbsentTime = 0; continue; }
                pair.Value.AbsentTime += deltaSeconds;
                if (pair.Value.AbsentTime >= rearmSeconds) remove.Add(pair.Key);
            }
            foreach (string key in remove) latches.Remove(key);
            var result = new List<int>(targets); result.Sort(); return result;
        }
        public void Clear() => latches.Clear();
    }
}
