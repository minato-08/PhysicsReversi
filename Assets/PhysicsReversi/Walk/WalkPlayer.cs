using UnityEngine;

namespace PhysicsReversi.Walk
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class WalkPlayer : MonoBehaviour
    {
        public int playerId = 1;
        public Transform carryPoint;
        public float moveSpeed = 6;
        public float turnSpeed = 540;
        public float pushStrength = 35;
        public float carryFollowSpeed = 8;
        public float maxCarrySpeed = 12;
        public CarryStone HeldStone { get; private set; }
        public bool HasMovementInput => movement.sqrMagnitude > .001f;
        public bool HasManipulationInput => HasMovementInput;
        // Ready stance: the action is being held while carrying. Past the tap time the stone is
        // lowered to just above the board in front, level with lying stones, and a throw charges.
        public bool IsReady => readySince >= 0;
        public float ReadySeconds => readySince < 0 ? 0 : Time.time - readySince;
        float readySince = -1, readyDelay, readyHeight, readyForce, readyWeight;
        // Called by CarryAuthority only.
        public void BeginReady(float delaySeconds, float height, float maxForce, float weight)
        {
            if (HeldStone == null || IsReady) return;
            readySince = Time.time; readyDelay = delaySeconds; readyHeight = height; readyForce = maxForce; readyWeight = weight;
        }
        // Just picked up, the stone is still on its way to the hand.
        bool arriving;
        // How far the stone may be from the hand before the holder is held back, and how
        // close a picked-up stone comes before it counts as in the hand.
        const float HoldSlack = .4f, ArriveDistance = .15f;
        // Faster than any swing of the arm, slow enough that a stone never jumps through things.
        const float MaxHoldSpeed = 40;
        CharacterController controller;
        Vector3 movement;
        float verticalSpeed;
        float oldDamping, oldAngularDamping;
        Collider[] heldColliders;
        Quaternion carryRotationOffset;
        void Awake() => controller = GetComponent<CharacterController>();
        // Input adapter or future replicated commands can both feed this controller.
        public void SetMovement(Vector3 worldDirection) => movement = Vector3.ClampMagnitude(worldDirection, 1);
        void Update()
        {
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            Vector3 step = (movement * moveSpeed + Vector3.up * verticalSpeed) * Time.deltaTime;
            if (HeldStone != null && carryPoint != null && !arriving) step = StayWithStone(step);
            controller.Move(step);
            if (movement.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(movement), turnSpeed * Time.deltaTime);
        }
        // The hold is rigid both ways: the holder cannot walk into a stone that will not give
        // way, nor away from one that cannot follow.
        Vector3 StayWithStone(Vector3 step)
        {
            Vector3 hand = carryPoint.position - transform.position; hand.y = 0;
            Vector3 toStone = HeldStone.Body.position - (transform.position + step); toStone.y = 0;
            float reach = hand.magnitude, distance = toStone.magnitude;
            if (distance < .001f) return step;
            Vector3 along = toStone / distance;
            float closing = Vector3.Dot(step, along);
            if (distance < reach - HoldSlack && closing > 0) step -= along * Mathf.Min(closing, reach - HoldSlack - distance);
            else if (distance > reach + HoldSlack && closing < 0) step -= along * Mathf.Max(closing, reach + HoldSlack - distance);
            return step;
        }
        void FixedUpdate()
        {
            if (HeldStone == null || carryPoint == null) return;
            // Dynamic body keeps collision response while carried; no parenting/teleport.
            var body = HeldStone.Body;
            bool lowered = IsReady && ReadySeconds >= readyDelay;
            HeldStone.SetCarriedWeight(lowered ? readyWeight : 1);
            // The stone is held at arm's length: where the carry point is, in front of the holder,
            // or lowered from there to just above the board. It is swung round the holder toward
            // that side, keeping its distance, so it never cuts across the holder's body to get
            // there. It swings faster than the holder turns, or it could never make up a lag.
            Vector3 hand = carryPoint.position - transform.position;
            float height = lowered ? readyHeight : hand.y; hand.y = 0;
            float reach = hand.magnitude;
            Vector3 front = reach > .001f ? hand / reach : transform.forward;
            Vector3 bearing = body.position - transform.position; bearing.y = 0;
            bearing = bearing.sqrMagnitude > .01f ? bearing.normalized : front;
            float swing = turnSpeed * 2 * Time.fixedDeltaTime;
            bearing = Quaternion.AngleAxis(Mathf.Clamp(Vector3.SignedAngle(bearing, front, Vector3.up), -swing, swing), Vector3.up) * bearing;
            Vector3 error = transform.position + bearing * reach + Vector3.up * height - body.position;
            Vector3 wanted;
            if (arriving)
            {
                // Just picked up: the stone comes to the hand at its own pace.
                wanted = Vector3.ClampMagnitude(error * carryFollowSpeed, maxCarrySpeed);
                arriving = error.magnitude > ArriveDistance;
            }
            // In the hand it is held firmly: where the hand goes, the stone goes at once.
            else wanted = Vector3.ClampMagnitude(error / Time.fixedDeltaTime, MaxHoldSpeed);
            if (lowered)
            {
                // Lowered, it is handled as a light thing drawn by a limited force. Being light it
                // starts and stops with its holder and only nudges what it bumps; the force is
                // then all there is to how hard it shoves. It never comes in faster than it can stop.
                float push = readyForce / body.mass;
                wanted = Vector3.ClampMagnitude(wanted, Mathf.Sqrt(2 * push * error.magnitude));
                body.linearVelocity += Vector3.ClampMagnitude(wanted - body.linearVelocity, push * Time.fixedDeltaTime);
            }
            else body.linearVelocity = wanted;
            Quaternion turn = carryPoint.rotation * carryRotationOffset * Quaternion.Inverse(body.rotation);
            turn.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180) degrees -= 360;
            if (axis.sqrMagnitude > .001f && !float.IsNaN(axis.x)) body.angularVelocity = axis * Mathf.Clamp(degrees * Mathf.Deg2Rad * 5, -6, 6);
        }
        public void Attach(CarryStone stone, bool showOwnColor = false)
        {
            HeldStone = stone; readySince = -1; arriving = true;
            // Carried level with the holder's own color up, or just as it was picked up.
            Quaternion carried = showOwnColor
                ? Quaternion.FromToRotation(stone.transform.up, Vector3.up * StoneFaces.UpSign(playerId)) * stone.Body.rotation
                : stone.Body.rotation;
            carryRotationOffset = Quaternion.Inverse(carryPoint.rotation) * carried;
            oldDamping = stone.Body.linearDamping; oldAngularDamping = stone.Body.angularDamping;
            stone.Body.useGravity = false; stone.Body.linearDamping = 0; stone.Body.angularDamping = 2;
            heldColliders = stone.GetComponentsInChildren<Collider>();
            foreach (var shape in heldColliders) Physics.IgnoreCollision(controller, shape, true);
        }
        // Put down with a modest amount of walking momentum rather than a launch impulse.
        public void Detach() => Detach(movement * moveSpeed * .35f);
        // A throw leaves with the velocity it is given.
        public void Detach(Vector3 velocity)
        {
            if (HeldStone == null) return;
            var stone = HeldStone;
            stone.Body.useGravity = true; stone.Body.linearDamping = oldDamping; stone.Body.angularDamping = oldAngularDamping;
            stone.SetCarriedWeight(1);
            stone.Body.linearVelocity = velocity;
            stone.Body.angularVelocity = Vector3.zero;
            foreach (var shape in heldColliders) if (shape != null) Physics.IgnoreCollision(controller, shape, false);
            stone.Release(); HeldStone = null; heldColliders = null; readySince = -1;
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
