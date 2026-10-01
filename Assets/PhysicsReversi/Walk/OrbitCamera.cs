using UnityEngine;

namespace PhysicsReversi.Walk
{
    // Over-the-shoulder camera: the pivot sits beside and above the character so the
    // screen centre (the crosshair) is never covered by the character itself.
    public sealed class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 10;
        public float height = 2.6f;
        [Tooltip("Sideways offset of the pivot. Positive puts the character left of the crosshair.")]
        public float shoulderOffset = 1.4f;
        public float sensitivity = .18f;
        public float yaw;
        public float pitch = 38;
        public float minPitch = 5;
        public float maxPitch = 85;
        [Header("Zoom (mouse wheel)")]
        public float minDistance = 5;
        public float maxDistance = 32;
        [Range(1.05f, 1.5f)] public float zoomStep = 1.15f;
        public float zoomSmoothing = 10;
        float shownDistance;
        void OnEnable() => shownDistance = distance;
        public void Rotate(Vector2 delta)
        {
            yaw += delta.x * sensitivity; pitch = Mathf.Clamp(pitch - delta.y * sensitivity, minPitch, maxPitch);
        }
        // Wheel deltas differ wildly between browsers and the editor, so only the sign is used.
        public void Zoom(float scroll)
        {
            if (Mathf.Abs(scroll) < .01f) return;
            distance = Mathf.Clamp(distance * (scroll > 0 ? 1 / zoomStep : zoomStep), minDistance, maxDistance);
        }
        void LateUpdate()
        {
            if (target == null) return;
            shownDistance = Mathf.Lerp(shownDistance, distance, 1 - Mathf.Exp(-zoomSmoothing * Time.deltaTime));
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 focus = target.position + Vector3.up * height + Quaternion.Euler(0, yaw, 0) * Vector3.right * shoulderOffset;
            Vector3 desired = focus - rotation * Vector3.forward * shownDistance;
            // Terrain collision only: do not snap inward because of the carried stone.
            var hits = Physics.SphereCastAll(focus, .2f, (desired - focus).normalized, shownDistance, ~0, QueryTriggerInteraction.Ignore);
            float safeDistance = shownDistance;
            foreach (var hit in hits)
                if (!hit.collider.transform.IsChildOf(target) && hit.collider.GetComponentInParent<CarryStone>() == null)
                    safeDistance = Mathf.Min(safeDistance, Mathf.Max(.6f, hit.distance - .15f));
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * safeDistance, rotation);
        }
    }
}
