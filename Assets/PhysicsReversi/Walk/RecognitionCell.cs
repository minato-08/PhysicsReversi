using UnityEngine;

namespace PhysicsReversi.Walk
{
    // Placed on each existing bowl collider in edit mode.
    public sealed class RecognitionCell : MonoBehaviour
    {
        [Range(0, 63)] public int cellIndex;
        [Tooltip("Flat frame lying on the cell; shown while the cell holds a recognized stone.")]
        public Renderer marker;
    }
}
