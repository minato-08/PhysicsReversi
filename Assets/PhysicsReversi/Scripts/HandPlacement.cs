namespace PhysicsReversi
{
    // Whether a stone on the board was put there by hand (put down or thrown), as opposed to being
    // pushed into its cell. Being knocked (pushed, struck, flipped, slid to the middle) keeps it,
    // as long as the stone stays in the cell it was in at the time; ending up in another cell loses
    // it until the stone is put down by hand again.
    public sealed class HandPlacement
    {
        const int NotKnocked = -2;
        public bool Placed { get; private set; }
        int knockedFrom = NotKnocked;
        public void Place() { Placed = true; knockedFrom = NotKnocked; }
        public void Clear() { Placed = false; knockedFrom = NotKnocked; }
        // Moved by something other than its own release. cell is where it was then, or -1 when it had
        // not been recognized yet (knocked just after being let go): then the first cell it is
        // recognized in afterwards counts as its own.
        public void Knock(int cell) { if (Placed && knockedFrom == NotKnocked) knockedFrom = cell; }
        // Called once per sample with where the stone is recognized now, or -1 (which changes nothing).
        public bool Allows(int cell)
        {
            if (Placed && knockedFrom == -1 && cell >= 0) knockedFrom = cell;
            if (Placed && knockedFrom != NotKnocked && cell >= 0 && cell != knockedFrom) Clear();
            return Placed;
        }
    }
}
