using UnityEngine;

namespace PhysicsReversi.Walk
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class WalkPlayer : MonoBehaviour
    {
        public int playerId = 1;
        public Transform carryPoint;
        public bool carryFollowsCamera = true;
        [Min(0)] public float minimumCarryHeight = .35f;
        public float moveSpeed = 6;
        public float turnSpeed = 540;
        public float pushStrength = 35;
        public float carryFollowSpeed = 8;
        public float maxCarrySpeed = 12;
        public CarryStone HeldStone { get; private set; }
        public bool HasMovementInput => movement.sqrMagnitude > .001f;
        public bool HasManipulationInput => HasMovementInput || Time.time - lastAimInput < .12f;
        CharacterController controller;
        Vector3 movement;
        float verticalSpeed;
        float oldDamping, oldAngularDamping;
        Collider[] heldColliders;
        Quaternion carryRotationOffset;
        Quaternion carryAim = Quaternion.identity;
        bool hasCarryAim;
        float lastAimInput = float.NegativeInfinity;
        public void SetCarryAim(Quaternion rotation)
        {
            if (hasCarryAim && Quaternion.Angle(carryAim, rotation) > .05f) lastAimInput = Time.time;
            carryAim = rotation; hasCarryAim = true;
        }
        Vector3 CarryPosition()
        {
            if (!carryFollowsCamera || !hasCarryAim) return carryPoint.position;
            var offset = Vector3.Scale(transform.InverseTransformPoint(carryPoint.position), transform.lossyScale);
            var relative = Vector3.up * offset.y + carryAim * new Vector3(offset.x, 0, offset.z);
            relative.y = Mathf.Max(minimumCarryHeight, relative.y);
            return transform.position + relative;
        }
        Quaternion CarryRotation()
        {
            if (!carryFollowsCamera || !hasCarryAim) return carryPoint.rotation;
            // Follow camera heading while preserving the held face when looking down.
            return Quaternion.Euler(0, carryAim.eulerAngles.y, 0) * Quaternion.Inverse(transform.rotation) * carryPoint.rotation;
        }
        void Awake() => controller = GetComponent<CharacterController>();
        // Input adapter or future replicated commands can both feed this controller.
        public void SetMovement(Vector3 worldDirection) => movement = Vector3.ClampMagnitude(worldDirection, 1);
        void Update()
        {
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            controller.Move((movement * moveSpeed + Vector3.up * verticalSpeed) * Time.deltaTime);
            if (movement.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(movement), turnSpeed * Time.deltaTime);
        }
        void FixedUpdate()
        {
            if (HeldStone == null || carryPoint == null) return;
            // Dynamic body keeps collision response while carried; no parenting/teleport.
            var body = HeldStone.Body;
            body.linearVelocity = Vector3.ClampMagnitude((CarryPosition() - body.position) * carryFollowSpeed, maxCarrySpeed);
            Quaternion error = CarryRotation() * carryRotationOffset * Quaternion.Inverse(body.rotation);
            error.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180) degrees -= 360;
            if (axis.sqrMagnitude > .001f && !float.IsNaN(axis.x)) body.angularVelocity = axis * Mathf.Clamp(degrees * Mathf.Deg2Rad * 5, -6, 6);
        }
        public void Attach(CarryStone stone)
        {
            HeldStone = stone;
            carryRotationOffset = Quaternion.Inverse(CarryRotation()) * stone.Body.rotation;
            oldDamping = stone.Body.linearDamping; oldAngularDamping = stone.Body.angularDamping;
            stone.Body.useGravity = false; stone.Body.linearDamping = 0; stone.Body.angularDamping = 2;
            heldColliders = stone.GetComponentsInChildren<Collider>();
            foreach (var shape in heldColliders) Physics.IgnoreCollision(controller, shape, true);
        }
        public void Detach()
        {
            if (HeldStone == null) return;
            var stone = HeldStone;
            stone.Body.useGravity = true; stone.Body.linearDamping = oldDamping; stone.Body.angularDamping = oldAngularDamping;
            // Release with a modest amount of walking momentum rather than a launch impulse.
            stone.Body.linearVelocity = movement * moveSpeed * .35f;
            stone.Body.angularVelocity = Vector3.zero;
            foreach (var shape in heldColliders) if (shape != null) Physics.IgnoreCollision(controller, shape, false);
            stone.Release(); HeldStone = null; heldColliders = null;
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var stone = hit.collider.GetComponentInParent<CarryStone>();
            if (stone == null || stone == HeldStone || stone.status == StoneStatus.Removed || hit.moveDirection.y < -.3f) return;
            var direction = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
            if (movement.sqrMagnitude > .001f && direction.sqrMagnitude > .001f && !stone.IsFlipping)
                stone.RegisterPlayerMotion();
            stone.Body.AddForceAtPosition(direction * pushStrength * Time.deltaTime, hit.point, ForceMode.Impulse);
        }
        void OnDisable() { Detach(); movement = Vector3.zero; }
    }
}
