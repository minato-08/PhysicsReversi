using System;

namespace PhysicsReversi
{
    // One button while carrying: a short press puts the stone down; a held one readies the
    // stone and charges a throw, faster the longer it is held.
    public static class StoneThrow
    {
        // 0 to 1 once the press has lasted past a tap, or -1 while it still counts as one.
        public static double Charge(double heldSeconds, double tapSeconds, double chargeSeconds)
            => heldSeconds < tapSeconds ? -1 : chargeSeconds <= 0 ? 1 : Math.Min(1, (heldSeconds - tapSeconds) / chargeSeconds);
        public static double Speed(double charge, double minSpeed, double maxSpeed) => minSpeed + (maxSpeed - minSpeed) * charge;
    }
}
