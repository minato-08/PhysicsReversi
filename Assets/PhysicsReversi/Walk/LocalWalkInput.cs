using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicsReversi.Walk
{
    public sealed class LocalWalkInput : MonoBehaviour
    {
        public WalkPlayer player;
        public OrbitCamera orbit;
        public Camera view;
        public CarryAuthority authority;
        [Tooltip("Aiming at the ground this close to a stone still selects that stone.")]
        [Range(0, 3)] public float aimAssistRadius = 1.5f;
        // The stone a click would grab right now, or null; read by AimHud.
        public CarryStone AimedStone { get; private set; }
        void Start() => SetCursorCaptured(true);

        void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
            if (player != null) player.SetMovement(Vector3.zero);
        }

        void Update()
        {
            if (player == null || orbit == null || view == null || authority == null) return;
            var keys = Keyboard.current; var mouse = Mouse.current;
            if (keys != null && keys.escapeKey.wasPressedThisFrame)
            {
                SetCursorCaptured(false);
                return;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                player.SetMovement(Vector3.zero);
                AimedStone = null;
                // This click only resumes control; it must not grab or drop a stone.
                if (Application.isFocused && mouse != null && mouse.leftButton.wasPressedThisFrame)
                    SetCursorCaptured(true);
                return;
            }
            Vector2 input = Vector2.zero;
            if (keys != null)
            {
                input.x = (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0);
                input.y = (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0);
            }
            if (mouse != null) { orbit.Rotate(mouse.delta.ReadValue()); orbit.Zoom(mouse.scroll.ReadValue().y); }
            player.SetCarryAim(Quaternion.Euler(orbit.pitch, orbit.yaw, 0));
            player.SetMovement(Quaternion.Euler(0, orbit.yaw, 0) * new Vector3(input.x, 0, input.y));
            Ray ray = view.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            RefreshAim(ray);
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (player.HeldStone != null) authority.TryRelease(player);
            else if (AimedStone != null) authority.TryGrab(player, AimedStone);
        }

        // One sorted cast serves both modes: empty-handed it picks a stone,
        // while carrying it finds the surface the held stone should hover above.
        void RefreshAim(Ray ray)
        {
            bool carrying = player.HeldStone != null;
            AimedStone = null;
            var hits = Physics.RaycastAll(ray, 200, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool found = false; RaycastHit surface = default;
            foreach (var hit in hits)
            {
                // The third-person character must not obstruct its own picking ray.
                if (hit.collider.transform.IsChildOf(player.transform)) continue;
                var stone = hit.collider.GetComponentInParent<CarryStone>();
                // Stones never block placement: aim through them at the board beneath.
                if (carrying && stone != null) continue;
                if (!carrying && stone != null) AimedStone = stone;
                surface = hit; found = true; break;
            }
            if (carrying)
            {
                if (found) player.SetCarryTarget(surface.point);
                else
                {
                    // Looking past the stage: fall back to the player's own ground plane.
                    var ground = new Plane(Vector3.up, player.transform.position);
                    player.SetCarryTarget(ground.Raycast(ray, out float enter) ? ray.GetPoint(enter)
                        : player.transform.position + Vector3.ProjectOnPlane(ray.direction, Vector3.up).normalized * player.maxHoldDistance);
                }
                return;
            }
            player.ClearCarryTarget();
            // A stone that cannot be grabbed is never offered; look for one beside it instead.
            if (AimedStone != null && !authority.CanGrab(player, AimedStone)) AimedStone = null;
            if (AimedStone == null && found && aimAssistRadius > 0) AimedStone = NearestGrabbable(surface.point);
        }

        CarryStone NearestGrabbable(Vector3 point)
        {
            CarryStone best = null; float bestDistance = float.PositiveInfinity;
            foreach (var near in Physics.OverlapSphere(point, aimAssistRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var stone = near.GetComponentInParent<CarryStone>();
                if (stone == null || !authority.CanGrab(player, stone)) continue;
                float distance = (stone.transform.position - point).sqrMagnitude;
                if (distance < bestDistance) { best = stone; bestDistance = distance; }
            }
            return best;
        }
        void OnApplicationFocus(bool focused) { if (!focused) SetCursorCaptured(false); }
        void OnDisable() { AimedStone = null; SetCursorCaptured(false); }
    }
}
