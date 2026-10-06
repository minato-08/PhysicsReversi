using System.Collections.Generic;

namespace PhysicsReversi
{
    // Captures as in ordinary reversi. A loose stone that has kept its cell for the wait captures
    // only as one end of a line, and only against the settled board: the confirmed stones. A
    // stone set down between two opposing stones captures nothing, and nothing captures it.
    public sealed class LegalMoveCaptures
    {
        public sealed class Move
        {
            public int Id, Cell, Owner;
            public List<int> CapturedIds;
        }
        sealed class Stay
        {
            // The cell and owner the stone is staying as.
            public int Key;
            public double Seconds;
            public bool Judged;
        }
        // By stone id. A stone is judged once per stay: when it has kept its cell and color for the wait.
        readonly Dictionary<int, Stay> stays = new Dictionary<int, Stay>();

        // recognized: every stone on the board now. settled: the confirmed stones only.
        // loose: unconfirmed stones a player has moved. unavailable: stones in a capture flip.
        // With no wait a stone is judged on the sample it arrives.
        public List<Move> Scan(BoardRules.Snapshot recognized, BoardRules.Snapshot settled, HashSet<int> loose, HashSet<int> unavailable,
            double deltaSeconds = 0, double waitSeconds = 0)
        {
            var moves = new List<Move>();
            var seen = new HashSet<int>();
            for (int cell = 0; cell < 64; cell++)
            {
                int id = recognized.Ids[cell], owner = recognized.Owners[cell];
                if (id < 0 || owner == 0 || !loose.Contains(id)) continue;
                seen.Add(id);
                int key = cell * 4 + owner;
                // The first sample in a cell only starts the count.
                if (!stays.TryGetValue(id, out var stay) || stay.Key != key) stays[id] = stay = new Stay { Key = key };
                else stay.Seconds += deltaSeconds;
                if (stay.Judged || stay.Seconds < waitSeconds) continue;
                // A flip in one of its lines leaves the outcome open: judge once the flip is over.
                if (Touches(settled, cell, owner, unavailable)) continue;
                stay.Judged = true;
                if (settled.Ids[cell] >= 0 && settled.Ids[cell] != id) continue;
                var captured = BoardRules.CapturesFrom(settled, cell, owner);
                if (captured.Count > 0) moves.Add(new Move { Id = id, Cell = cell, Owner = owner, CapturedIds = captured });
            }
            // A stone that left its cell, or is no longer loose, starts over when it comes back.
            var gone = new List<int>();
            foreach (int id in stays.Keys) if (!seen.Contains(id)) gone.Add(id);
            foreach (int id in gone) stays.Remove(id);
            return moves;
        }
        // Whether a line from 'cell' meets an unavailable stone before the line ends.
        static bool Touches(BoardRules.Snapshot settled, int cell, int owner, HashSet<int> unavailable)
        {
            if (unavailable.Count == 0) return false;
            for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int x = cell % 8 + dx, z = cell / 8 + dz;
                while (x >= 0 && x < 8 && z >= 0 && z < 8)
                {
                    int next = z * 8 + x;
                    if (settled.Ids[next] < 0) break;
                    if (unavailable.Contains(settled.Ids[next])) return true;
                    if (settled.Owners[next] == 0 || settled.Owners[next] == owner) break;
                    x += dx; z += dz;
                }
            }
            return false;
        }
        public void Clear() => stays.Clear();
    }
}
