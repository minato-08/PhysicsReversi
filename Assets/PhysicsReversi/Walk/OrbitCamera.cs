using UnityEngine;

namespace PhysicsReversi.Walk
{
    public sealed class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 7;
        public float height = 1.5f;
        public float sensitivity = .18f;
        public float yaw;
        public float pitch = 28;
        public void Rotate(Vector2 delta)
        {
            yaw += delta.x * sensitivity; pitch = Mathf.Clamp(pitch - delta.y * sensitivity, 10, 75);
        }
        void LateUpdate()
        {
            if (target == null) return;
            Vector3 focus = target.position + Vector3.up * height;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 desired = focus - rotation * Vector3.forward * distance;
            // Terrain collision only: do not snap inward because of the carried stone.
            var hits = Physics.SphereCastAll(focus, .2f, (desired - focus).normalized, distance, ~0, QueryTriggerInteraction.Ignore);
            float safeDistance = distance;
            foreach (var hit in hits)
                if (!hit.collider.transform.IsChildOf(target) && hit.collider.GetComponentInParent<CarryStone>() == null)
                    safeDistance = Mathf.Min(safeDistance, Mathf.Max(.6f, hit.distance - .15f));
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * safeDistance, rotation);
        }
    }
}
