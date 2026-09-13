using UnityEngine;

namespace PhysicsReversi.Walk
{
    // Placed on each existing bowl collider in edit mode.
    public sealed class RecognitionCell : MonoBehaviour
    {
        [Range(0, 63)] public int cellIndex;
        public LineRenderer ring;
    }
}
