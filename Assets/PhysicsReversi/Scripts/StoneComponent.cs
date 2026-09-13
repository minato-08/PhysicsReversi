using UnityEngine;

namespace PhysicsReversi
{
    public sealed class StoneComponent : MonoBehaviour
    {
        public int Id, Owner;
        public bool Removed;
        public Rigidbody Body;
        public MeshCollider Shape;
        public Renderer Surface;
        public LineRenderer Ring;
        public int FirstCell = -1, RecognizedCell = -1;
        public double Ratio;
    }
}
