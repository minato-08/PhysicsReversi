using System;
namespace PhysicsReversi
{
    public static class StoneFaces
    {
        public static int Owner(double upDot, double edgeTolerance = .1)
            => Math.Abs(upDot) <= edgeTolerance ? 0 : upDot > 0 ? 1 : 2;
        // Which way the stone's local +Y has to point, along up, for this owner's color to show.
        public static int UpSign(int owner) => owner == 2 ? -1 : 1;
    }
}
