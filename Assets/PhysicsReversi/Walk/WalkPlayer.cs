using UnityEngine;

namespace PhysicsReversi.Walk
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class WalkPlayer : MonoBehaviour
    {
        public int playerId = 1;
        public Transform carryPoint;
        [Tooltip("The values both players share (speed, push, pick-up, sway). Empty: the scene's Player Tuning is found at start.")]
        public PlayerTuning tuning;
        public PlayerTuning Tuning { get { if (tuning == null) tuning = PlayerTuning.Find(); return tuning; } }
        // Before 2026-11-27 each player held its own copy of the tuning. Kept, hidden, only so that
        // Gather Player Tuning can move the scene's values into Player Tuning; removed after that.
        [SerializeField, HideInInspector] float moveSpeed = 6, turnSpeed = 540, pushStrength = 35;
        [SerializeField, HideInInspector] Vector2 liftSeconds = new Vector2(.16f, .26f);
        [SerializeField, HideInInspector] float liftArc = .8f, swayStiffness = 11, swayDamping = .35f, swayAmount = .85f, swayLimit = .4f, swayTilt = 30;
        [SerializeField, HideInInspector] Collider walkableArea;
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
        public bool Arriving => HeldStone != null && arriving;
        // Raised once the picked-up stone lands in the hand.
        public static event System.Action<WalkPlayer, CarryStone> StoneCaught;
        // Set each frame by the figure's animation: how far its hands bob with the walk, so a
        // held stone bobs with them instead of floating still over a bouncing body.
        public float HoldBob { get; set; }
        Vector3 liftFrom; float liftTime, liftDuration; // liftFrom is in world space, fixed at the grab
        // The held stone's lag behind the hand (a damped spring pushed by the hand's acceleration).
        Vector3 sway, swayVelocity, lastHand, lastHandVelocity; bool handTracked;
        // How far the stone may be from the hand before the holder is held back.
        const float HoldSlack = .4f;
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
            var t = Tuning;
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            Vector3 step = (movement * t.moveSpeed + Vector3.up * verticalSpeed) * Time.deltaTime;
            if (HeldStone != null && carryPoint != null && !arriving) step = StayWithStone(step);
            if (t.walkableArea != null) step = KeepOnArea(step, t.walkableArea);
            controller.Move(step);
            if (movement.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(movement), t.turnSpeed * Time.deltaTime);
            if (HeldStone != null && carryPoint != null) Sway();
        }
        // Players stay on the table (decided 2026-11-25): a player who walked off the edge fell
        // forever and could not play again. The step is trimmed at the edge, so walking along it slides.
        Vector3 KeepOnArea(Vector3 step, Collider walkable)
        {
            Bounds area = walkable.bounds; float margin = controller.radius;
            Vector3 here = transform.position, next = here + step;
            next.x = Mathf.Clamp(next.x, area.min.x + margin, area.max.x - margin);
            next.z = Mathf.Clamp(next.z, area.min.z + margin, area.max.z - margin);
            step.x = next.x - here.x; step.z = next.z - here.z;
            return step;
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
            var t = Tuning;
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
            float swing = t.turnSpeed * 2 * Time.fixedDeltaTime;
            bearing = Quaternion.AngleAxis(Mathf.Clamp(Vector3.SignedAngle(bearing, front, Vector3.up), -swing, swing), Vector3.up) * bearing;
            Vector3 hold = transform.position + bearing * reach + Vector3.up * height;
            Vector3 wanted;
            if (arriving)
            {
                // Just picked up: lifted off and swung into the hand on a short arc, slow to
                // start and fastest at the end, so it reads as something with weight hauled up
                // and caught, not drawn in like a magnet. It aims at the hand as it is now, so
                // it keeps up with a holder who turns meanwhile. It goes round the holder, not
                // through: bearing and distance from the holder are blended, not positions.
                liftTime += Time.fixedDeltaTime;
                float along = Mathf.Clamp01(liftTime / liftDuration), u = along * along;
                Vector3 from = liftFrom - transform.position;
                float fromHeight = from.y; from.y = 0;
                float fromReach = from.magnitude;
                Vector3 fromBearing = fromReach > .01f ? from / fromReach : front;
                Vector3 way = Quaternion.AngleAxis(Vector3.SignedAngle(fromBearing, front, Vector3.up) * u, Vector3.up) * fromBearing;
                Vector3 target = transform.position + way * Mathf.Lerp(fromReach, reach, u)
                    + Vector3.up * (Mathf.Lerp(fromHeight, height, u) + t.liftArc * Mathf.Sin(along * Mathf.PI));
                wanted = Vector3.ClampMagnitude((target - body.position) / Time.fixedDeltaTime, MaxHoldSpeed);
                if (along >= 1) { arriving = false; StoneCaught?.Invoke(this, HeldStone); }
            }
            // In the hand it is held firmly: where the hand goes, the stone goes at once, give
            // or take the sway.
            else wanted = Vector3.ClampMagnitude((hold + sway + Vector3.up * (lowered ? 0 : HoldBob) - body.position) / Time.fixedDeltaTime, MaxHoldSpeed);
            Vector3 error = hold - body.position;
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
            // The lag tips the stone toward it, like a plate carried in both hands.
            Vector3 lean = Vector3.Cross(Vector3.up, sway);
            Quaternion tilt = lean.sqrMagnitude > 1e-6f ? Quaternion.AngleAxis(t.swayTilt * sway.magnitude, lean.normalized) : Quaternion.identity;
            Quaternion turn = tilt * carryPoint.rotation * carryRotationOffset * Quaternion.Inverse(body.rotation);
            turn.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180) degrees -= 360;
            if (axis.sqrMagnitude > .001f && !float.IsNaN(axis.x)) body.angularVelocity = axis * Mathf.Clamp(degrees * Mathf.Deg2Rad * 5, -6, 6);
        }
        // The stone lags the hand when the hand speeds up, slows down or turns, and springs
        // back with a little overshoot. Settled out while lowered: there the stone is a tool for
        // shoving, and it must go where it is aimed.
        // Run per frame, where the holder moves: sampled in FixedUpdate the hand would move by
        // uneven steps and its acceleration would be noise.
        void Sway()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            Vector3 hand = carryPoint.position;
            bool lowered = IsReady && ReadySeconds >= readyDelay;
            Vector3 velocity = handTracked ? (hand - lastHand) / dt : Vector3.zero;
            Vector3 acceleration = handTracked ? (velocity - lastHandVelocity) / dt : Vector3.zero;
            lastHand = hand; lastHandVelocity = velocity; handTracked = true;
            acceleration.y = 0;
            if (arriving || lowered) acceleration = Vector3.zero;
            var t = Tuning;
            float k = t.swayStiffness * t.swayStiffness, c = 2 * t.swayDamping * t.swayStiffness;
            swayVelocity += (-k * sway - c * swayVelocity - acceleration * t.swayAmount) * dt;
            sway += swayVelocity * dt;
            if (lowered) sway = Vector3.MoveTowards(sway, Vector3.zero, 4 * dt);
            // At the limit only the outward motion stops, so it slides along the edge, not sticks.
            float outward = sway.magnitude;
            if (outward > t.swayLimit)
            {
                Vector3 dir = sway / outward;
                sway = dir * t.swayLimit;
                swayVelocity -= dir * Mathf.Max(0, Vector3.Dot(swayVelocity, dir));
            }
        }
        public void Attach(CarryStone stone, bool showOwnColor = false)
        {
            HeldStone = stone; readySince = -1; arriving = true;
            liftFrom = stone.Body.position; liftTime = 0;
            float distance = Vector3.ProjectOnPlane(liftFrom - carryPoint.position, Vector3.up).magnitude;
            liftDuration = Mathf.Lerp(Tuning.liftSeconds.x, Tuning.liftSeconds.y,Mathf.InverseLerp(0, 6, distance));
            sway = swayVelocity = Vector3.zero; handTracked = false;
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
        public void Detach() => Detach(movement * Tuning.moveSpeed * .35f);
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
            stone.Body.AddForceAtPosition(direction * Tuning.pushStrength * Time.deltaTime, hit.point, ForceMode.Impulse);
        }
        void OnDisable() { Detach(); movement = Vector3.zero; }
    }
}
