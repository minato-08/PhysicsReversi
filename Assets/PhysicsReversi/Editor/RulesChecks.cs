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
            Check(StoneFaces.Owner(StoneFaces.UpSign(1)) == 1 && StoneFaces.Owner(StoneFaces.UpSign(2)) == 2,
                "a stone turned for its holder shows the holder's color"); checks++;
            b = new BoardRules.Snapshot();
            b.Ids[0] = 0; b.Owners[0] = 2;
            b.Ids[1] = 1; b.Owners[1] = 1;
            b.Ids[2] = 2; b.Owners[2] = 2;
            placements.Enqueue(0, 1); resolved = placements.Resolve(b);
            Check(resolved[0].Owner == 2 && resolved[0].CapturedIds.Count == 1,
                "dropped black reserve landing white captures as white"); checks++;
            var live = new RealtimeCaptures(); var free = new HashSet<int>();
            b = new BoardRules.Snapshot();
            b.Ids[0] = 0; b.Owners[0] = 1; b.Ids[1] = 1; b.Owners[1] = 2; b.Ids[2] = 2; b.Owners[2] = 1;
            var liveTargets = live.Scan(b, free, .05, .15);
            Check(liveTargets.Count == 1 && liveTargets[0] == 1, "live line captures without any placement event"); checks++;
            Check(live.Scan(b, free, .05, .15).Count == 0, "unchanged live line does not repeat"); checks++;
            b.Owners[0] = 2; b.Owners[1] = 1; b.Owners[2] = 2;
            Check(live.Scan(b, free, .05, .15).Count == 0, "same physical line reversing colors does not oscillate"); checks++;
            var empty = new BoardRules.Snapshot();
            live.Scan(empty, new HashSet<int> { 1 }, 2, .15);
            Check(live.Scan(b, free, .05, .15).Count == 0, "airborne capture participant preserves repeat protection"); checks++;
            live.Scan(empty, free, .05, .15);
            Check(live.Scan(b, free, .05, .15).Count == 0, "brief boundary dropout does not retrigger"); checks++;
            live.Scan(empty, free, .2, .15);
            Check(live.Scan(b, free, .05, .15).Count == 1, "broken then reformed line can capture again"); checks++;
            live.Clear();
            b.Ids[32] = 30; b.Owners[32] = 1; b.Ids[33] = 31; b.Owners[33] = 2; b.Ids[34] = 32; b.Owners[34] = 1;
            liveTargets = live.Scan(b, new HashSet<int> { 1 }, .05, .15);
            Check(liveTargets.Count == 1 && liveTargets[0] == 31, "flipping in one region does not block another"); checks++;
            live.Clear(); liveTargets = live.Scan(b, free, .05, .15);
            Check(liveTargets.Count == 2 && liveTargets[0] == 1 && liveTargets[1] == 31, "both colors capture from the same snapshot"); checks++;
            live.Clear(); b = new BoardRules.Snapshot();
            b.Ids[24] = 0; b.Owners[24] = 1; b.Ids[26] = 1; b.Owners[26] = 2; b.Ids[27] = 2; b.Owners[27] = 1;
            Check(live.Scan(b, free, .05, .15).Count == 0, "live capture does not bridge empty cells"); checks++;
            b.Ids[24] = -1; b.Owners[24] = 0; b.Ids[25] = 0; b.Owners[25] = 1;
            Check(live.Scan(b, free, .05, .15).Count == 1, "pushing existing endpoint into line triggers capture"); checks++;
            live.Clear();
            var sources = new Dictionary<int, MotionOrigin> { { 0, new MotionOrigin(true) } };
            // b is the prior test's [black at25, white at26, black at27].
            var changedByPlayer = RealtimeCaptures.PlayerChanges(empty, b, sources);
            var participants = new HashSet<int>();
            Check(live.Scan(b, free, .05, .15, changedByPlayer, participants).Count == 1,
                "player-caused formation still captures immediately"); checks++;
            Check(participants.Contains(0) && participants.Contains(1) && participants.Contains(2),
                "capture exposes all participants for action consumption"); checks++;
            live.Clear();
            Check(live.Scan(b, free, .05, .15, new HashSet<int>()).Count == 0,
                "passive or flip-created formation does not capture"); checks++;
            Check(RealtimeCaptures.PlayerChanges(b, b, sources).Count == 0,
                "stationary endpoint with stale player history cannot start a chain"); checks++;
            var flipMotion = new MotionOrigin(false);
            var collisionMotion = MotionOrigin.Newest(sources[0], flipMotion);
            Check(!collisionMotion.CanCapture, "flip collision replaces older player cause"); checks++;
            sources[0] = collisionMotion;
            Check(RealtimeCaptures.PlayerChanges(empty, b, sources).Count == 0,
                "movement caused by flip has no capture eligibility"); checks++;
            var renewed = new MotionOrigin(true); sources[0] = renewed;
            Check(MotionOrigin.Newest(collisionMotion, renewed).CanCapture,
                "fresh intervention after flip enables player-caused motion"); checks++;
            changedByPlayer = RealtimeCaptures.PlayerChanges(empty, b, sources);
            Check(live.Scan(b, free, .05, .15, changedByPlayer).Count == 1,
                "fresh intervention can capture a previously suppressed line"); checks++;
            var struckNeighbour = renewed;
            renewed.Consume();
            Check(!struckNeighbour.CanCapture, "capture consumes shared cause across collision chain"); checks++;
            // Confirmation: 1.5 seconds in one cell to confirm, .5 seconds out of it to come loose.
            // The first sample in a cell only starts the count.
            var settled = new StoneConfirmation();
            settled.Tick(27, false, 1, 1.5, .5); settled.Tick(27, false, 1, 1.5, .5);
            Check(!settled.Confirmed, "stone is not confirmed before the time is up"); checks++;
            settled.Tick(27, false, 1, 1.5, .5);
            Check(settled.Confirmed && settled.Cell == 27, "stone recognized in one cell long enough is confirmed"); checks++;
            settled.Tick(-1, false, .25, 1.5, .5); settled.Tick(27, false, .25, 1.5, .5); settled.Tick(-1, false, .25, 1.5, .5);
            Check(settled.Confirmed, "brief dropouts do not add up to loosen a confirmed stone"); checks++;
            settled.Tick(-1, false, .25, 1.5, .5);
            Check(!settled.Confirmed, "confirmed stone out of its cell long enough comes loose"); checks++;
            var pushed = new StoneConfirmation();
            for (int i = 0; i < 3; i++) pushed.Tick(27, false, 1, 1.5, .5);
            pushed.Tick(28, false, .25, 1.5, .5); pushed.Tick(28, false, .25, 1.5, .5);
            Check(!pushed.Confirmed && pushed.Cell == 28, "confirmed stone pushed into another cell comes loose"); checks++;
            pushed.Tick(28, false, 1, 1.5, .5);
            Check(!pushed.Confirmed, "pushed stone counts from the start in its new cell"); checks++;
            pushed.Tick(28, false, 1, 1.5, .5);
            Check(pushed.Confirmed && pushed.Cell == 28, "pushed stone is confirmed again in its new cell"); checks++;
            var wandering = new StoneConfirmation();
            wandering.Tick(27, false, 1, 1.5, .5); wandering.Tick(27, false, 1, 1.5, .5);
            wandering.Tick(28, false, 1, 1.5, .5); wandering.Tick(28, false, 1, 1.5, .5);
            Check(!wandering.Confirmed, "changing cell before confirmation restarts the count"); checks++;
            wandering.Tick(-1, false, .05, 1.5, .5); wandering.Tick(28, false, 1, 1.5, .5); wandering.Tick(28, false, 1, 1.5, .5);
            Check(!wandering.Confirmed, "losing recognition before confirmation restarts the count"); checks++;
            var flipped = new StoneConfirmation();
            for (int i = 0; i < 3; i++) flipped.Tick(27, false, 1, 1.5, .5);
            flipped.Tick(-1, true, 5, 1.5, .5);
            Check(flipped.Confirmed, "confirmed stone stays confirmed through a capture flip"); checks++;
            flipped.Reset();
            Check(!flipped.Confirmed && flipped.Cell == -1, "released or returned stone starts unconfirmed"); checks++;
            // A stone that makes a capture is committed on the spot, without the wait.
            var committed = new StoneConfirmation();
            committed.Tick(27, false, .05, 1.5, .5); committed.ConfirmNow(27);
            Check(committed.Confirmed && committed.Cell == 27, "a stone that captures is confirmed at once"); checks++;
            committed.Tick(27, false, 5, 1.5, .5); committed.Tick(-1, false, .25, 1.5, .5);
            Check(committed.Confirmed, "a stone confirmed by capturing rides out a brief dropout like any other"); checks++;
            committed.Tick(-1, false, .25, 1.5, .5);
            Check(!committed.Confirmed, "a stone confirmed by capturing still comes loose when knocked out of its cell"); checks++;
            live.Clear(); b = new BoardRules.Snapshot();
            b.Ids[0] = 10; b.Owners[0] = 1; b.Ids[1] = 11; b.Owners[1] = 2; b.Ids[2] = 12; b.Owners[2] = 1;
            var ends = new HashSet<int>();
            liveTargets = live.Scan(b, free, .05, .15, null, null, ends);
            Check(liveTargets.Count == 1 && liveTargets[0] == 11 && ends.Count == 2 && ends.Contains(10) && ends.Contains(12),
                "both ends of a capturing line are reported, and not the stone it flips"); checks++;
            ends.Clear();
            Check(live.Scan(b, free, .05, .15, null, null, ends).Count == 0 && ends.Count == 0,
                "a line that has already captured reports no ends again"); checks++;
            // One button while carrying: a tap of .2 seconds, then 1 second to charge from speed 5 to 14.
            Check(StoneThrow.Charge(.1, .2, 1) < 0, "a short press puts the stone down instead of throwing it"); checks++;
            Check(StoneThrow.Charge(.2, .2, 1) == 0 && StoneThrow.Speed(0, 5, 14) == 5, "a press just past a tap throws at the lowest speed"); checks++;
            Check(Math.Abs(StoneThrow.Charge(.7, .2, 1) - .5) < 1e-9 && Math.Abs(StoneThrow.Speed(.5, 5, 14) - 9.5) < 1e-9,
                "throw speed grows with the time held"); checks++;
            Check(StoneThrow.Charge(9, .2, 1) == 1 && StoneThrow.Speed(1, 5, 14) == 14, "a full charge does not grow further"); checks++;
            // Hold: full within .1 cells of the center of the stone's cell, none from .45 out. Cell 27 is centered on (-.5, -.5).
            Check(Math.Abs(StoneHold.CenterDistance(-.5, -.5, 27)) < 1e-9 && Math.Abs(StoneHold.CenterDistance(-.2, -.5, 27) - .3) < 1e-9,
                "distance is measured from the center of the stone's own cell"); checks++;
            Check(StoneHold.Strength(0, .1, .45) == 1 && StoneHold.Strength(.1, .1, .45) == 1, "a well-centered stone is held fully"); checks++;
            Check(StoneHold.Strength(.45, .1, .45) == 0 && StoneHold.Strength(.6, .1, .45) == 0, "a stone at the edge of its cell is not held"); checks++;
            double halfway = StoneHold.Strength(.275, .1, .45);
            Check(Math.Abs(halfway - .5) < 1e-9 && StoneHold.Strength(.2, .1, .45) > halfway && StoneHold.Strength(.35, .1, .45) < halfway,
                "hold falls off smoothly with distance from the center"); checks++;
            Check(StoneHold.MassFactor(0, 4) == 1 && StoneHold.MassFactor(1, 4) == 4 && StoneHold.MassFactor(.5, 4) == 2.5,
                "hold makes a stone heavier, up to the multiplier"); checks++;
            // Legal moves, from the opening of ordinary reversi: white on 27 and 36, black on 28 and 35.
            b = new BoardRules.Snapshot();
            b.Ids[27] = 27; b.Owners[27] = 2; b.Ids[28] = 28; b.Owners[28] = 1;
            b.Ids[35] = 35; b.Owners[35] = 1; b.Ids[36] = 36; b.Owners[36] = 2;
            var legalBlack = BoardRules.LegalMoves(b, 1); var legalWhite = BoardRules.LegalMoves(b, 2);
            Check(Array.FindAll(legalBlack, x => x).Length == 4 && legalBlack[19] && legalBlack[26] && legalBlack[37] && legalBlack[44],
                "black's legal moves are the cells from which it would capture"); checks++;
            Check(Array.FindAll(legalWhite, x => x).Length == 4 && legalWhite[20] && legalWhite[29] && legalWhite[34] && legalWhite[43],
                "white's legal moves are judged on the same board"); checks++;
            Check(!legalBlack[27] && !legalBlack[28] && !legalBlack[0], "an occupied cell, or one that captures nothing, is not a legal move"); checks++;
            Check(BoardRules.CapturesFrom(b, 26, 1).Count == 1 && BoardRules.CapturesFrom(b, 26, 1)[0] == 27 && BoardRules.Captures(b, 26, 1).Count == 0,
                "captures can be asked of a cell before any stone stands there"); checks++;
            // Captures by legal move. 'b' is the settled board; 'now' adds the loose stones lying on it.
            var legal = new LegalMoveCaptures(); var loose = new HashSet<int> { 50 };
            var now = new BoardRules.Snapshot();
            for (int i = 0; i < 64; i++) { now.Ids[i] = b.Ids[i]; now.Owners[i] = b.Owners[i]; }
            now.Ids[26] = 50; now.Owners[26] = 1;
            var moves = legal.Scan(now, b, loose, free);
            Check(moves.Count == 1 && moves[0].Id == 50 && moves[0].Cell == 26 && moves[0].CapturedIds.Count == 1 && moves[0].CapturedIds[0] == 27,
                "a loose stone arriving on a legal move captures"); checks++;
            Check(legal.Scan(now, b, loose, free).Count == 0, "a stone that stays where it was judged is not judged again"); checks++;
            legal.Clear();
            Check(legal.Scan(now, b, new HashSet<int>(), free).Count == 0, "a stone no player moved makes no move"); checks++;
            now.Ids[26] = -1; now.Owners[26] = 0; now.Ids[0] = 50; now.Owners[0] = 1;
            Check(legal.Scan(now, b, loose, free).Count == 0, "a stone on a cell that captures nothing makes no move"); checks++;
            now.Ids[0] = -1; now.Owners[0] = 0; now.Ids[26] = 50; now.Owners[26] = 1;
            Check(legal.Scan(now, b, loose, free).Count == 1, "the same stone moved onto a legal move captures"); checks++;
            // White on 0 and 2: black set down between them captures nothing, and is not captured.
            legal.Clear(); b = new BoardRules.Snapshot(); now = new BoardRules.Snapshot();
            b.Ids[0] = 0; b.Owners[0] = 2; b.Ids[2] = 2; b.Owners[2] = 2;
            now.Ids[0] = 0; now.Owners[0] = 2; now.Ids[2] = 2; now.Owners[2] = 2; now.Ids[1] = 50; now.Owners[1] = 1;
            Check(legal.Scan(now, b, loose, free).Count == 0, "a stone set down between two opposing stones is not captured"); checks++;
            // Black loose on 0, white settled on 1: black arriving on 2 has no settled stone to capture with.
            legal.Clear(); b = new BoardRules.Snapshot(); now = new BoardRules.Snapshot();
            b.Ids[1] = 1; b.Owners[1] = 2;
            now.Ids[0] = 51; now.Owners[0] = 1; now.Ids[1] = 1; now.Owners[1] = 2; now.Ids[2] = 50; now.Owners[2] = 1;
            Check(legal.Scan(now, b, new HashSet<int> { 50, 51 }, free).Count == 0, "a loose stone cannot be the far end of a capture"); checks++;
            // Black settled on 0, white settled on 1 and still turning over from a capture.
            legal.Clear(); b.Ids[0] = 0; b.Owners[0] = 1; now.Ids[0] = 0;
            Check(legal.Scan(now, b, loose, new HashSet<int> { 1 }).Count == 0, "a move through a stone still flipping waits"); checks++;
            Check(legal.Scan(now, b, loose, free).Count == 1, "and is judged once the flip is over"); checks++;
            return checks;
        }
    }
}
