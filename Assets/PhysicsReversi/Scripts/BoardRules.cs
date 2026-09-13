using System;
using System.Collections.Generic;

namespace PhysicsReversi
{
    // Pure rules: no Unity or collision callbacks. Coordinates range from -4 to +4.
    public static class BoardRules
    {
        public struct Point
        {
            public double X, Y;
            public Point(double x, double y) { X = x; Y = y; }
        }
        public sealed class Candidate
        {
            public int Id, Owner;
            public List<Point> Polygon;
            public int FirstCell = -1;
            public double Ratio;
        }
        public sealed class Snapshot
        {
            public readonly int[] Ids = new int[64];
            public readonly int[] Owners = new int[64];
            public Snapshot() { for (int i = 0; i < 64; i++) Ids[i] = -1; }
            public int Count(int owner) { int n = 0; foreach (int value in Owners) if (value == owner) n++; return n; }
        }
        public static List<Point> Hull(List<Point> input)
        {
            var sorted = new List<Point>(input);
            sorted.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            var h = new List<Point>();
            foreach (var p in sorted)
            {
                while (h.Count >= 2 && Cross(h[h.Count - 2], h[h.Count - 1], p) <= 0) h.RemoveAt(h.Count - 1);
                h.Add(p);
            }
            int lower = h.Count;
            for (int i = sorted.Count - 2; i >= 0; i--)
            {
                var p = sorted[i];
                while (h.Count > lower && Cross(h[h.Count - 2], h[h.Count - 1], p) <= 0) h.RemoveAt(h.Count - 1);
                h.Add(p);
            }
            if (h.Count > 1) h.RemoveAt(h.Count - 1);
            return h;
        }
        static double Cross(Point a, Point b, Point c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        public static double Area(List<Point> polygon)
        {
            double sum = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                sum += a.X * b.Y - b.X * a.Y;
            }
            return Math.Abs(sum) * .5;
        }
        static List<Point> Clip(List<Point> polygon, int axis, double boundary, bool greater)
        {
            var result = new List<Point>();
            if (polygon.Count == 0) return result;
            var previous = polygon[polygon.Count - 1];
            double pv = axis == 0 ? previous.X : previous.Y;
            bool pin = greater ? pv >= boundary : pv <= boundary;
            foreach (var current in polygon)
            {
                double cv = axis == 0 ? current.X : current.Y;
                bool cin = greater ? cv >= boundary : cv <= boundary;
                if (cin != pin)
                {
                    double t = (boundary - pv) / (cv - pv);
                    result.Add(new Point(previous.X + t * (current.X - previous.X), previous.Y + t * (current.Y - previous.Y)));
                }
                if (cin) result.Add(current);
                previous = current; pv = cv; pin = cin;
            }
            return result;
        }
        public static Snapshot Recognize(List<Candidate> candidates, double minimum, double tolerance)
        {
            var board = new Snapshot();
            var best = new double[64];
            foreach (var stone in candidates)
            {
                stone.FirstCell = -1; stone.Ratio = 0;
                double total = Area(stone.Polygon);
                if (total < 1e-9) continue;
                int cell = -1; double first = 0, second = 0;
                for (int z = 0; z < 8; z++) for (int x = 0; x < 8; x++)
                {
                    var p = Clip(stone.Polygon, 0, x - 4, true);
                    p = Clip(p, 0, x - 3, false);
                    p = Clip(p, 1, z - 4, true);
                    p = Clip(p, 1, z - 3, false);
                    double ratio = Area(p) / total;
                    if (ratio > first) { second = first; first = ratio; cell = z * 8 + x; }
                    else if (ratio > second) second = ratio;
                }
                stone.FirstCell = cell; stone.Ratio = first;
                if (cell < 0 || first < minimum || first - second < Math.Max(tolerance, 1e-9)) continue;
                if (board.Ids[cell] < 0 || first > best[cell] + 1e-9 ||
                    (Math.Abs(first - best[cell]) <= 1e-9 && stone.Id < board.Ids[cell]))
                { best[cell] = first; board.Ids[cell] = stone.Id; board.Owners[cell] = stone.Owner; }
            }
            return board;
        }
        public static List<int> Captures(Snapshot board, int originCell, int owner)
        {
            var result = new List<int>();
            if (originCell < 0 || originCell >= 64 || board.Owners[originCell] != owner) return result;
            for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                var line = new List<int>();
                int x = originCell % 8 + dx, z = originCell / 8 + dz;
                while (x >= 0 && x < 8 && z >= 0 && z < 8)
                {
                    int cell = z * 8 + x;
                    if (board.Owners[cell] == 0) break;
                    if (board.Owners[cell] == owner) { if (line.Count > 0) result.AddRange(line); break; }
                    line.Add(board.Ids[cell]); x += dx; z += dz;
                }
            }
            return result;
        }
    }
}
