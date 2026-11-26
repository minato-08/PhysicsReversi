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

        // The animation values are shared by both figures and live in Player Tuning (2026-11-27).
        // These hidden copies are kept only so that Gather Player Tuning can move the scene's
        // values there; removed after that.
        [SerializeField, HideInInspector] float stepRate = 3.2f, bobHeight = .12f, waddle = 7, footStride = .28f, footLift = .14f;
        [SerializeField, HideInInspector] float breathRate = .6f, breathAmount = .025f;
        [SerializeField, HideInInspector] Vector3 holdReach = new Vector3(0, .35f, .45f);
        [SerializeField, HideInInspector] float liftDip = .14f, liftLean = 16, catchSquash = .2f, readyLean = 14, blend = 10;

        PlayerTuning T => player != null ? player.Tuning : PlayerTuning.Find();

        Vector3 bodyRest, headRest, leftFootRest, rightFootRest, leftHandRest, rightHandRest;
        Vector3 lastPosition;
        float phase, speedBlend, holdBlend, readyBlend, liftBlend, squash, squashSize = .12f;
        bool wasHolding;

        void Awake()
        {
            bodyRest = Rest(body); headRest = Rest(head);
            leftFootRest = Rest(leftFoot); rightFootRest = Rest(rightFoot);
            leftHandRest = Rest(leftHand); rightHandRest = Rest(rightHand);
        }
        void OnEnable() { lastPosition = transform.position; WalkPlayer.StoneCaught += OnCaught; }
        void OnDisable() => WalkPlayer.StoneCaught -= OnCaught;
        void OnCaught(WalkPlayer who, CarryStone stone) { if (who == player) { squash = 1; squashSize = T.catchSquash; } }

        static Vector3 Rest(Transform t) => t != null ? t.localPosition : Vector3.zero;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            Vector3 delta = transform.position - lastPosition; delta.y = 0;
            lastPosition = transform.position;
            float speed = player != null && T.moveSpeed > 0 ? Mathf.Clamp01(delta.magnitude / dt / T.moveSpeed) : 0;
            float k = 1 - Mathf.Exp(-T.blend * dt);
            speedBlend = Mathf.Lerp(speedBlend, speed, k);

            bool holding = player != null && player.HeldStone != null;
            bool ready = player != null && player.IsReady;
            holdBlend = Mathf.Lerp(holdBlend, holding ? 1 : 0, k);
            readyBlend = Mathf.Lerp(readyBlend, ready ? 1 : 0, k);
            bool lifting = player != null && player.Arriving;
            liftBlend = Mathf.Lerp(liftBlend, lifting ? 1 : 0, 1 - Mathf.Exp(-T.blend * 2.5f * dt));
            // A little squash when a stone is let go; a bigger one when it lands in the hand (OnCaught).
            if (!holding && wasHolding) { squash = 1; squashSize = .12f; }
            wasHolding = holding;
            squash = Mathf.MoveTowards(squash, 0, dt * 5);

            phase += dt * T.stepRate * Mathf.PI * Mathf.Max(speedBlend, .001f);
            float step = Mathf.Sin(phase);
            float bounce = Mathf.Abs(step);
            float breath = Mathf.Sin(Time.time * T.breathRate * Mathf.PI * 2) * T.breathAmount * (1 - speedBlend);

            if (player != null) player.HoldBob = bounce * T.bobHeight * speedBlend * holdBlend;
            if (body != null)
            {
                body.localPosition = bodyRest + Vector3.up * (bounce * T.bobHeight * speedBlend - T.liftDip * liftBlend);
                body.localRotation = Quaternion.Euler(T.readyLean * readyBlend + T.liftLean * liftBlend + 4 * speedBlend, 0, step * T.waddle * speedBlend);
                float pop = Mathf.Sin(squash * Mathf.PI) * squashSize;
                body.localScale = new Vector3(1 + pop - breath, 1 - pop + breath * 2, 1 + pop - breath);
            }
            if (head != null) head.localRotation = Quaternion.Euler(-T.readyLean * .5f * readyBlend, 0, -step * T.waddle * .5f * speedBlend);
            Foot(leftFoot, leftFootRest, step);
            Foot(rightFoot, rightFootRest, -step);
            Hand(leftHand, leftHandRest, -step);
            Hand(rightHand, rightHandRest, step);
        }

        void Foot(Transform foot, Vector3 rest, float swing)
        {
            if (foot == null) return;
            float lift = Mathf.Max(0, swing) * T.footLift * speedBlend;
            foot.localPosition = rest + new Vector3(0, lift, swing * T.footStride * speedBlend);
        }

        // Hands swing with the walk, and reach forward together toward a held stone.
        void Hand(Transform hand, Vector3 rest, float swing)
        {
            if (hand == null) return;
            Vector3 walk = new Vector3(0, 0, swing * T.footStride * .6f * speedBlend * (1 - holdBlend));
            hand.localPosition = rest + walk + T.holdReach * holdBlend;
        }
    }
}
