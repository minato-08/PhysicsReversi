namespace PhysicsReversi
{
    // A stone is confirmed once it has stayed recognized in one cell, and comes loose again
    // once it has stayed out of that cell. Another cell counts as out: the count starts over there.
    public sealed class StoneConfirmation
    {
        public bool Confirmed { get; private set; }
        // The cell being counted toward confirmation, or held once confirmed; -1 for none.
        public int Cell { get; private set; } = -1;
        // Unconfirmed: time recognized in Cell. Confirmed: time spent out of Cell.
        double seconds;
        public void Reset() { Confirmed = false; Cell = -1; seconds = 0; }
        // Committed at once, without the wait: the stone has just made a capture from this cell.
        public void ConfirmNow(int cell) { if (cell < 0) return; Confirmed = true; Cell = cell; seconds = 0; }
        // cell is where the stone is recognized now, or -1. A frozen stone (mid capture flip) keeps its state.
        public void Tick(int cell, bool frozen, double deltaSeconds, double confirmSeconds, double loosenSeconds)
        {
            if (frozen) return;
            if (Confirmed)
            {
                if (cell == Cell) { seconds = 0; return; }
                seconds += deltaSeconds;
                // A brief dropout changes nothing; only a lasting one knocks the stone loose.
                if (seconds >= loosenSeconds) { Confirmed = false; Cell = cell; seconds = 0; }
                return;
            }
            if (cell < 0 || cell != Cell) { Cell = cell; seconds = 0; return; }
            seconds += deltaSeconds;
            if (seconds >= confirmSeconds) { Confirmed = true; seconds = 0; }
        }
    }
}
