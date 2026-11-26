using UnityEngine;

namespace PhysicsReversi.Walk
{
    // The tuning both players share, kept in one place (2026-11-27) so the two cannot drift apart
    // by mistake: how they walk and push, how a stone is picked up and swings in the hand, and how
    // the toy figures move. Players read it every frame, so a change made in Play shows at once
    // (and, like any scene value, is lost when Play stops). It lives in the scene, like the rest of
    // the tuning, so each scene keeps its own values. The meaning of every value is in TUNING.md.
    [DisallowMultipleComponent]
    public sealed class PlayerTuning : MonoBehaviour
    {
        [Header("Walk")]
        [Tooltip("Walking speed (units per second).")]
        [Min(0)] public float moveSpeed = 6;
        [Tooltip("How fast a player turns to face where they walk (degrees per second). A held stone swings round at twice this.")]
        [Min(0)] public float turnSpeed = 540;
        [Tooltip("How hard walking into a stone shoves it. Kept where an unheld stone moves and a confirmed one does not.")]
        [Min(0)] public float pushStrength = 35;
        [Tooltip("The players cannot walk past the edges of this collider's bounds (the table top). Stones can still fall off. Empty: no limit.")]
        public Collider walkableArea;

        [Header("Pick up (ART.md 手ざわり)")]
        [Tooltip("Seconds a picked-up stone takes to reach the hand: from right in front, and from the far end of the reach.")]
        public Vector2 liftSeconds = new Vector2(.16f, .26f);
        [Tooltip("How far above the straight line the stone rises on its way to the hand.")]
        public float liftArc = .8f;

        [Header("Hold sway")]
        [Tooltip("How fast the held stone swings back to the hand (angular frequency, rad/s). Lower is looser.")]
        public float swayStiffness = 11;
        [Tooltip("How quickly the swing dies out. 0 swings on, 1 settles without overshoot.")]
        [Range(0, 1)] public float swayDamping = .35f;
        [Tooltip("How strongly the hand's acceleration throws the stone off; 1 is a free pendulum.")]
        [Range(0, 1)] public float swayAmount = .85f;
        [Tooltip("Farthest the held stone lags behind the hand.")]
        public float swayLimit = .4f;
        [Tooltip("Degrees the stone tilts per unit it lags.")]
        public float swayTilt = 30;

        [Header("Toy figure: walk")]
        [Tooltip("Steps per second at full walking speed.")]
        public float stepRate = 3.2f;
        [Tooltip("How high the body bounces with each step.")]
        public float bobHeight = .12f;
        [Tooltip("Side-to-side roll of the body while walking, in degrees.")]
        public float waddle = 7;
        [Tooltip("How far each foot swings forward and back.")]
        public float footStride = .28f;
        [Tooltip("How high a foot lifts as it swings forward.")]
        public float footLift = .14f;

        [Header("Toy figure: idle")]
        [Tooltip("Breaths per second while standing still.")]
        public float breathRate = .6f;
        [Tooltip("How much the body swells with each breath.")]
        public float breathAmount = .025f;

        [Header("Toy figure: hold")]
        [Tooltip("Hands move this far forward and up when a stone is held.")]
        public Vector3 holdReach = new Vector3(0, .35f, .45f);
        [Tooltip("While a picked-up stone is on its way to the hand, the body dips this far: hauling it up.")]
        public float liftDip = .14f;
        [Tooltip("...and leans forward this many degrees.")]
        public float liftLean = 16;
        [Tooltip("Squash of the body when the stone lands in the hand.")]
        public float catchSquash = .2f;
        [Tooltip("Forward lean while readying a throw, in degrees.")]
        public float readyLean = 14;
        [Tooltip("How quickly poses blend, per second.")]
        public float blend = 10;

        static PlayerTuning found;

        // The scene's tuning. A scene without one (the bowl-cell scene, which is no longer kept up)
        // gets one with the defaults, so the players still work.
        public static PlayerTuning Find()
        {
            if (found != null) return found;
            found = FindAnyObjectByType<PlayerTuning>();
            if (found != null || !Application.isPlaying) return found;
            Debug.LogWarning("No Player Tuning in the scene; the players use the default values. Run Physics Reversi/Walk/Add Player Tuning.");
            found = new GameObject("Player Tuning (defaults)").AddComponent<PlayerTuning>();
            return found;
        }
    }
}
