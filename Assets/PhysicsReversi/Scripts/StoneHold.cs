using System;

namespace PhysicsReversi
{
    // How firmly a confirmed stone sits, from how well it is centered in its cell.
    // Distances are in cells: 0 at the center of the cell, .5 at the middle of an edge.
    public static class StoneHold
    {
        public static double CenterDistance(double x, double z, int cell)
        {
            double dx = x - (cell % 8 - 3.5), dz = z - (cell / 8 - 3.5);
            return Math.Sqrt(dx * dx + dz * dz);
        }
        // 1 within fullDistance of the center, easing down to 0 at zeroDistance.
        public static double Strength(double distance, double fullDistance, double zeroDistance)
        {
            if (distance <= fullDistance) return 1;
            if (distance >= zeroDistance) return 0;
            double t = (distance - fullDistance) / (zeroDistance - fullDistance);
            return 1 - t * t * (3 - 2 * t);
        }
        // A fully held stone weighs 'multiplier' times as much; one that is not held, its own weight.
        public static double MassFactor(double strength, double multiplier) => 1 + (multiplier - 1) * strength;
    }
}
