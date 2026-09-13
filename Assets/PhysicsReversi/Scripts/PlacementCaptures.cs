using System;
using System.Collections.Generic;

namespace PhysicsReversi
{
    // Pending placements are consumed once, even when their stone fails recognition.
    // Every search in a batch reads the same pre-capture snapshot: no chain reaction.
    public sealed class PlacementCaptures
    {
        public sealed class Result
        {
            public int StoneId, Owner, OriginCell;
            public List<int> CapturedIds;
        }
        readonly List<Result> pending = new List<Result>();
        public void Enqueue(int stoneId, int owner)
        {
            if (stoneId < 0 || owner < 1 || owner > 2) return;
            if (pending.Exists(p => p.StoneId == stoneId)) return;
            pending.Add(new Result { StoneId = stoneId, Owner = owner });
        }
        public List<Result> Resolve(BoardRules.Snapshot snapshot)
        {
            var results = new List<Result>(pending); pending.Clear();
            foreach (var result in results)
            {
                result.OriginCell = Array.IndexOf(snapshot.Ids, result.StoneId);
                if (result.OriginCell >= 0) result.Owner = snapshot.Owners[result.OriginCell];
                result.CapturedIds = BoardRules.Captures(snapshot, result.OriginCell, result.Owner);
            }
            return results;
        }
        public void Clear() => pending.Clear();
    }
}
