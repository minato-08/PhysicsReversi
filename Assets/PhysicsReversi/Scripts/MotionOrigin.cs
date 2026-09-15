namespace PhysicsReversi
{
    // Shared by stones struck by the same action. A capture consumes the entire action.
    public sealed class MotionOrigin
    {
        static long nextSequence;
        readonly long sequence = ++nextSequence;
        public readonly bool FromPlayer;
        public bool Consumed { get; private set; }
        public bool CanCapture => FromPlayer && !Consumed;
        public MotionOrigin(bool fromPlayer) => FromPlayer = fromPlayer;
        public void Consume() => Consumed = true;
        public static MotionOrigin Newest(MotionOrigin a, MotionOrigin b)
            => a == null ? b : b == null ? a : a.sequence >= b.sequence ? a : b;
    }
}
