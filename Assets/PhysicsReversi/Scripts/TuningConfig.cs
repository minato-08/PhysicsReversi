using UnityEngine;

namespace PhysicsReversi
{
    [CreateAssetMenu(menuName = "Physics Reversi/Tuning")]
    public sealed class TuningConfig : ScriptableObject
    {
        [Header("Stone and board (one cell = 1 unit)")]
        [Range(.4f, .95f)] public float diameter = .78f;
        [Range(.06f, .25f)] public float thickness = .12f;
        public float rimHeight = .15f;
        [Range(0, 1)] public float friction = .22f;
        [Range(0, .8f)] public float bounce = .18f;
        public float linearDamping = .15f;
        public float angularDamping = .8f;
        [Header("Launcher")]
        public float minSpeed = 2.5f;
        public float maxSpeed = 11f;
        public float launchHeight = .30f;
        public float launchLift = 1.4f;
        [Range(5, 80)] public float maxAngle = 65f;
        [Header("Recognition")]
        [Range(0, 1)] public float recognitionMinimum = .34f;
        [Range(0, .1f)] public float tieTolerance = .02f;
        public float contactTolerance = .025f;
        public float centerHeightAllowance = .065f;
        [Header("Settle: two phases, each bounded to 3 seconds")]
        public float stableSeconds = .75f;
        public float speedThreshold = .045f;
        public float spinThreshold = .15f;
        public float phaseTimeout = 3f;
        public float dampingRampSeconds = 1f;
        [Header("Capture")]
        [Range(0, 2)] public float captureLift = .5f;
        [Range(0, 8)] public float captureSpin = 1.8f;
        public float collectionBoundary = 4.8f;
        public int reservePerPlayer = 30;
    }
}
