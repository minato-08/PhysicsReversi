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
            if (mouse != null) orbit.Rotate(mouse.delta.ReadValue());
            player.SetCarryAim(Quaternion.Euler(orbit.pitch, orbit.yaw, 0));
            player.SetMovement(Quaternion.Euler(0, orbit.yaw, 0) * new Vector3(input.x, 0, input.y));
            if (mouse == null) return;
            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (player.HeldStone != null) { authority.TryRelease(player); return; }
            Ray ray = view.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            // The third-person character must not obstruct its own picking ray.
            var hits = Physics.RaycastAll(ray, 100, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(player.transform)) continue;
                authority.TryGrab(player, hit.collider.GetComponentInParent<CarryStone>());
                break;
            }
        }
        void OnApplicationFocus(bool focused) { if (!focused) SetCursorCaptured(false); }
        void OnDisable() => SetCursorCaptured(false);
    }
}
