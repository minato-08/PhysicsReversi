using UnityEngine;

namespace PhysicsReversi.Walk
{
    // Marks a collider that stones rest on as the top of the board, for a flat board
    // whose cells have no colliders of their own. Which cell a stone is in still comes
    // from the recognition; this only says the stone is lying on the board's surface.
    public sealed class BoardSurface : MonoBehaviour { }
}
