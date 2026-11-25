using UnityEngine;

namespace PhysicsReversi.Walk
{
    // Animates the primitive toy figure that stands in for a player. Visual only: it moves child
    // transforms under the figure root and never touches the controller, the stones or physics.
    public sealed class ToyFigureMotion : MonoBehaviour
    {
        public WalkPlayer player;
        [Tooltip("Squashed and stretched; parent of everything that bobs.")]
        public Transform body;
        public Transform head;
        public Transform leftFoot, rightFoot;
        public Transform leftHand, rightHand;

        [Header("Walk")]
        [Tooltip("Steps per second at full walking speed.")]
        public float stepRate = 3.2f;
        public float bobHeight = .12f;
        [Tooltip("Side-to-side roll of the body while walking, in degrees.")]
        public float waddle = 7;
        public float footStride = .28f;
        public float footLift = .14f;
        [Header("Idle")]
        public float breathRate = .6f;
        public float breathAmount = .025f;
        [Header("Hold")]
        [Tooltip("Hands move this far forward and up when a stone is held.")]
        public Vector3 holdReach = new Vector3(0, .35f, .45f);
        [Tooltip("Forward lean while readying a throw, in degrees.")]
        public float readyLean = 14;
        [Tooltip("How quickly poses blend, per second.")]
        public float blend = 10;

        Vector3 bodyRest, headRest, leftFootRest, rightFootRest, leftHandRest, rightHandRest;
        Vector3 lastPosition;
        float phase, speedBlend, holdBlend, readyBlend, squash;
        bool wasHolding;

        void Awake()
        {
            bodyRest = Rest(body); headRest = Rest(head);
            leftFootRest = Rest(leftFoot); rightFootRest = Rest(rightFoot);
            leftHandRest = Rest(leftHand); rightHandRest = Rest(rightHand);
        }
        void OnEnable() => lastPosition = transform.position;

        static Vector3 Rest(Transform t) => t != null ? t.localPosition : Vector3.zero;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            Vector3 delta = transform.position - lastPosition; delta.y = 0;
            lastPosition = transform.position;
            float speed = player != null && player.moveSpeed > 0 ? Mathf.Clamp01(delta.magnitude / dt / player.moveSpeed) : 0;
            float k = 1 - Mathf.Exp(-blend * dt);
            speedBlend = Mathf.Lerp(speedBlend, speed, k);

            bool holding = player != null && player.HeldStone != null;
            bool ready = player != null && player.IsReady;
            holdBlend = Mathf.Lerp(holdBlend, holding ? 1 : 0, k);
            readyBlend = Mathf.Lerp(readyBlend, ready ? 1 : 0, k);
            // A little squash whenever a stone is picked up or let go.
            if (holding != wasHolding) squash = 1;
            wasHolding = holding;
            squash = Mathf.MoveTowards(squash, 0, dt * 5);

            phase += dt * stepRate * Mathf.PI * Mathf.Max(speedBlend, .001f);
            float step = Mathf.Sin(phase);
            float bounce = Mathf.Abs(step);
            float breath = Mathf.Sin(Time.time * breathRate * Mathf.PI * 2) * breathAmount * (1 - speedBlend);

            if (body != null)
            {
                body.localPosition = bodyRest + Vector3.up * bounce * bobHeight * speedBlend;
                body.localRotation = Quaternion.Euler(readyLean * readyBlend + 4 * speedBlend, 0, step * waddle * speedBlend);
                float pop = Mathf.Sin(squash * Mathf.PI) * .12f;
                body.localScale = new Vector3(1 + pop - breath, 1 - pop + breath * 2, 1 + pop - breath);
            }
            if (head != null) head.localRotation = Quaternion.Euler(-readyLean * .5f * readyBlend, 0, -step * waddle * .5f * speedBlend);
            Foot(leftFoot, leftFootRest, step);
            Foot(rightFoot, rightFootRest, -step);
            Hand(leftHand, leftHandRest, -step);
            Hand(rightHand, rightHandRest, step);
        }

        void Foot(Transform foot, Vector3 rest, float swing)
        {
            if (foot == null) return;
            float lift = Mathf.Max(0, swing) * footLift * speedBlend;
            foot.localPosition = rest + new Vector3(0, lift, swing * footStride * speedBlend);
        }

        // Hands swing with the walk, and reach forward together toward a held stone.
        void Hand(Transform hand, Vector3 rest, float swing)
        {
            if (hand == null) return;
            Vector3 walk = new Vector3(0, 0, swing * footStride * .6f * speedBlend * (1 - holdBlend));
            hand.localPosition = rest + walk + holdReach * holdBlend;
        }
    }
}
