using System;
using System.Collections.Generic;

namespace PhysicsReversi
{
    public static class RulesChecks
    {
        static BoardRules.Candidate Stone(int id, int owner, double x, double z)
        {
            var points = new List<BoardRules.Point>();
            for (int i = 0; i < 24; i++)
            {
                double a = i * Math.PI * 2 / 24;
                points.Add(new BoardRules.Point(x + Math.Cos(a) * .39, z + Math.Sin(a) * .39));
            }
            return new BoardRules.Candidate { Id = id, Owner = owner, Polygon = points };
        }
        static BoardRules.Snapshot Recognize(params BoardRules.Candidate[] stones) => BoardRules.Recognize(new List<BoardRules.Candidate>(stones), .34, .02);
        static void Check(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); }
        public static int Run()
        {
            int checks = 0;
            var b = Recognize(Stone(7, 1, -.5, -.5));
            Check(b.Ids[27] == 7 && b.Count(1) == 1, "cell center / one stone one cell"); checks++;
            Check(Recognize(Stone(0, 1, 0, -.5)).Count(1) == 0, "two-cell boundary"); checks++;
            Check(Recognize(Stone(0, 1, 0, 0)).Count(1) == 0, "four-cell junction"); checks++;
            b = Recognize(Stone(9, 2, -.2, -.5), Stone(3, 1, -.5, -.5));
            Check(b.Ids[27] == 3 && b.Count(2) == 0 && b.Ids[28] == -1, "competition / no reassignment"); checks++;
            b = Recognize(Stone(9, 2, -.5, -.5), Stone(3, 1, -.5, -.5));
            Check(b.Ids[27] == 3, "equal overlap uses lower ID"); checks++;
            b = Recognize(Stone(3, 1, -.5, -.5), Stone(9, 2, -.5, -.5));
            Check(b.Ids[27] == 3, "equal overlap independent of enumeration order"); checks++;
            Check(Recognize(Stone(0, 1, 6, 6)).Count(1) == 0, "outside board"); checks++;
            var hull = BoardRules.Hull(new List<BoardRules.Point> { new BoardRules.Point(0, 0), new BoardRules.Point(1, 0), new BoardRules.Point(1, 1), new BoardRules.Point(0, 1), new BoardRules.Point(.5, .5), new BoardRules.Point(0, 0) });
            Check(Math.Abs(BoardRules.Area(hull) - 1) < 1e-8, "projected convex hull"); checks++;
            for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                b = new BoardRules.Snapshot();
                b.Ids[27] = 0; b.Owners[27] = 1;
                int mid = (3 + dz) * 8 + 3 + dx, end = (3 + dz * 2) * 8 + 3 + dx * 2;
                b.Ids[mid] = 1; b.Owners[mid] = 2; b.Ids[end] = 2; b.Owners[end] = 1;
                var found = BoardRules.Captures(b, 27, 1);
                Check(found.Count == 1 && found[0] == 1, "capture direction " + dx + "," + dz); checks++;
            }
            b = new BoardRules.Snapshot();
            b.Ids[24] = 0; b.Owners[24] = 1; b.Ids[26] = 1; b.Owners[26] = 2; b.Ids[27] = 2; b.Owners[27] = 1;
            Check(BoardRules.Captures(b, 24, 1).Count == 0, "empty cell stops capture"); checks++;
            Check(BoardRules.Captures(b, -1, 1).Count == 0, "unrecognized shot cannot capture"); checks++;
            b = new BoardRules.Snapshot();
            b.Ids[27] = 0; b.Owners[27] = 1;
            b.Ids[28] = 1; b.Owners[28] = 2; b.Ids[29] = 2; b.Owners[29] = 1;
            b.Ids[35] = 3; b.Owners[35] = 2; b.Ids[43] = 4; b.Owners[43] = 1;
            Check(BoardRules.Captures(b, 27, 1).Count == 2 && b.Owners[28] == 2, "multi-direction capture gathers without mutation"); checks++;
            b.Ids[0] = 5; b.Owners[0] = 1;
            Check(BoardRules.Captures(b, 0, 1).Count == 0, "does not capture from another friendly stone"); checks++;
            b = Recognize(Stone(0, 1, -.5, -.5), Stone(1, 2, .5, -.5), Stone(2, 1, 1.5, -.5), Stone(3, 2, 0, 0));
            Check(BoardRules.Captures(b, 27, 1).Count == 1, "unrecognized physical obstacle ignored"); checks++;
            b = BoardRules.Recognize(new List<BoardRules.Candidate> { Stone(1, 1, -.5, -.5) }, .5, .03);
            Check(b.Ids[27] == 1, "walk thresholds: centered stone"); checks++;
            b = BoardRules.Recognize(new List<BoardRules.Candidate> { Stone(1, 1, 0, -.5) }, .5, .03);
            Check(b.Count(1) == 0, "walk thresholds: equal boundary excluded"); checks++;
            b = BoardRules.Recognize(new List<BoardRules.Candidate> { Stone(1, 1, -.1, -.5) }, .5, .03);
            Check(b.Ids[27] == 1, "walk thresholds: majority overlap accepted"); checks++;
            b = BoardRules.Recognize(new List<BoardRules.Candidate> { Stone(1, 1, 0, 0) }, .5, .03);
            Check(b.Count(1) == 0, "walk thresholds: four-way overlap excluded"); checks++;
            var placements = new PlacementCaptures();
            b = new BoardRules.Snapshot();
            b.Ids[4] = 10; b.Owners[4] = 1;
            b.Ids[12] = 11; b.Owners[12] = 2;
            b.Ids[20] = 12; b.Owners[20] = 1;
            Check(placements.Resolve(b).Count == 0, "existing layout does not capture without placement"); checks++;
            placements.Enqueue(10, 1); placements.Enqueue(10, 1);
            var resolved = placements.Resolve(b);
            Check(resolved.Count == 1 && resolved[0].CapturedIds.Count == 1 && resolved[0].CapturedIds[0] == 11,
                "practice capture / duplicate placement ignored"); checks++;
            Check(b.Owners[12] == 2 && placements.Resolve(b).Count == 0,
                "capture consumed once / query does not change ownership"); checks++;
            placements.Enqueue(99, 1); resolved = placements.Resolve(b);
            Check(resolved.Count == 1 && resolved[0].OriginCell == -1 && resolved[0].CapturedIds.Count == 0,
                "unrecognized placement is consumed"); checks++;
            b.Ids[4] = 99;
            Check(placements.Resolve(b).Count == 0, "later movement cannot retry failed placement"); checks++;
            // A second origin may only capture if evaluated AFTER a first capture; batch must not chain.
            b = new BoardRules.Snapshot();
            b.Ids[0] = 0; b.Owners[0] = 1;
            b.Ids[1] = 1; b.Owners[1] = 2;
            b.Ids[2] = 2; b.Owners[2] = 1;
            b.Ids[9] = 9; b.Owners[9] = 2;
            b.Ids[17] = 17; b.Owners[17] = 1;
            placements.Enqueue(0, 1); placements.Enqueue(17, 1); resolved = placements.Resolve(b);
            Check(resolved[0].CapturedIds.Count == 1 && resolved[1].CapturedIds.Count == 0,
                "all pending placements use the same pre-capture snapshot"); checks++;
            placements.Enqueue(0, 1); placements.Clear();
            Check(placements.Resolve(b).Count == 0, "disable clears pending placements"); checks++;
            Check(StoneFaces.Owner(1) == 1 && StoneFaces.Owner(-1) == 2, "physical upper face determines owner"); checks++;
            Check(StoneFaces.Owner(0) == 0 && StoneFaces.Owner(.05) == 0, "edge-standing stone has no owner"); checks++;
            Check(StoneFaces.Owner(-.8) == 2 && StoneFaces.Owner(.8) == 1, "tilted faces retain readable ownership"); checks++;
            b = new BoardRules.Snapshot();
            b.Ids[0] = 0; b.Owners[0] = 2;
            b.Ids[1] = 1; b.Owners[1] = 1;
            b.Ids[2] = 2; b.Owners[2] = 2;
            placements.Enqueue(0, 1); resolved = placements.Resolve(b);
            Check(resolved[0].Owner == 2 && resolved[0].CapturedIds.Count == 1,
                "dropped black reserve landing white captures as white"); checks++;
            return checks;
        }
    }
}
