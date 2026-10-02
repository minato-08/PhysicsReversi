using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicsReversi.Walk
{
    // Both players share one machine and one screen. Each has only a movement direction
    // and a single grab / release action; the character faces the way it walks.
    public sealed class LocalWalkInput : MonoBehaviour
    {
        [Tooltip("Index 0: first gamepad, or WASD + F. Index 1: second gamepad, or IJKL + H.")]
        public WalkPlayer[] players;
        [Tooltip("Movement is relative to this camera: up walks away from it.")]
        public Camera view;
        public CarryAuthority authority;
        [Tooltip("The grab takes the grabbable stone nearest to the point this far ahead of the character.")]
        [Range(0, 4)] public float grabFocus = 1.5f;
        [Range(0, .9f)] public float stickDeadZone = .25f;
        // Up, down, left, right, action.
        static readonly Key[][] Keys =
        {
            new[] { Key.W, Key.S, Key.A, Key.D, Key.F },
            new[] { Key.I, Key.K, Key.J, Key.L, Key.H },
        };
        WalkBoardRecognition board;
        CarryStone[] targets;
        // The stone the player's action would grab right now, or null; read by AimHud.
        public CarryStone Target(int index) => targets != null && index >= 0 && index < targets.Length ? targets[index] : null;

        void Update()
        {
            if (players == null || view == null || authority == null) return;
            if (targets == null || targets.Length != players.Length) targets = new CarryStone[players.Length];
            Quaternion heading = Quaternion.Euler(0, view.transform.eulerAngles.y, 0);
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null || !player.isActiveAndEnabled) { targets[i] = null; continue; }
                Read(i, out Vector2 move, out bool action);
                player.SetMovement(heading * new Vector3(move.x, 0, move.y));
                targets[i] = player.HeldStone == null ? NearestGrabbable(player) : null;
                if (!action) continue;
                if (player.HeldStone != null) authority.TryRelease(player);
                else if (targets[i] != null) authority.TryGrab(player, targets[i]);
            }
        }

        // Gamepad and keyboard are both live, so a pad can be picked up without any setting.
        void Read(int index, out Vector2 move, out bool action)
        {
            move = Vector2.zero; action = false;
            var keyboard = Keyboard.current;
            if (keyboard != null && index < Keys.Length)
            {
                var keys = Keys[index];
                move.x = (keyboard[keys[3]].isPressed ? 1 : 0) - (keyboard[keys[2]].isPressed ? 1 : 0);
                move.y = (keyboard[keys[0]].isPressed ? 1 : 0) - (keyboard[keys[1]].isPressed ? 1 : 0);
                action = keyboard[keys[4]].wasPressedThisFrame;
            }
            if (index >= Gamepad.all.Count) return;
            var pad = Gamepad.all[index];
            Vector2 stick = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
            if (stick.magnitude > stickDeadZone) move += stick;
            action |= pad.buttonSouth.wasPressedThisFrame;
        }

        CarryStone NearestGrabbable(WalkPlayer player)
        {
            if (board == null) board = authority.GetComponent<WalkBoardRecognition>();
            if (board == null || board.stones == null) return null;
            Vector3 focus = player.transform.position + player.transform.forward * grabFocus;
            CarryStone best = null; float bestDistance = float.PositiveInfinity;
            foreach (var stone in board.stones)
            {
                if (stone == null || !authority.CanGrab(player, stone)) continue;
                float distance = Vector3.ProjectOnPlane(stone.transform.position - focus, Vector3.up).sqrMagnitude;
                if (distance < bestDistance) { best = stone; bestDistance = distance; }
            }
            return best;
        }
        void OnDisable()
        {
            targets = null;
            if (players != null) foreach (var player in players) if (player != null) player.SetMovement(Vector3.zero);
        }
    }
}
