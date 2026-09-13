using System;
namespace PhysicsReversi
{
    public static class StoneFaces
    {
        public static int Owner(double upDot, double edgeTolerance = .1)
            => Math.Abs(upDot) <= edgeTolerance ? 0 : upDot > 0 ? 1 : 2;
    }
}
