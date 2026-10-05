using UnityEngine;
using System.Collections.Generic;

namespace PhysicsReversi.Walk
{
    public sealed class WalkCaptureController : MonoBehaviour
    {
        [HideInInspector] public CarryAuthority authority;
        public WalkBoardRecognition recognition;
        [HideInInspector] public Material blackMaterial;
        [HideInInspector] public Material whiteMaterial;
        [Header("Physical half-turn")]
        [Range(.5f, 3)] public float flipDuration = 1.2f;
        [Range(1.5f, 4)] public float flipHeight = 1.8f;
        public Vector3 captureAxis = Vector3.right;
        [Header("Local repeat prevention (never stops other parts of the board)")]
        [Range(.05f, .5f)] public float lineBreakSeconds = .15f;
        [Range(.05f, .5f)] public float flipReleaseGrace = .2f;
        [Header("Live capture")]
        [SerializeField] string lastResult = "Watching for new lines";
        [SerializeField] int lastCaptureCount;
        public string LastResult => lastResult;
        public int LastCaptureCount => lastCaptureCount;
        readonly RealtimeCaptures detector = new RealtimeCaptures();
        readonly Dictionary<int, float> unavailableUntil = new Dictionary<int, float>();
        float lastScanTime;
        BoardRules.Snapshot previousSnapshot = new BoardRules.Snapshot();

        void OnEnable()
        {
            lastScanTime = Time.time;
            previousSnapshot = new BoardRules.Snapshot();
            if (recognition != null) recognition.SnapshotUpdated += OnSnapshot;
        }
        void OnDisable()
        {
            if (recognition != null) recognition.SnapshotUpdated -= OnSnapshot;
            detector.Clear(); unavailableUntil.Clear();
        }
        void Start()
        {
            if (recognition == null)
            { Debug.LogError("Capture recognition is missing.", this); enabled = false; }
        }
        void OnSnapshot(BoardRules.Snapshot snapshot)
        {
            var unavailable = new HashSet<int>();
            var origins = new Dictionary<int, MotionOrigin>();
            for (int i = 0; i < recognition.stones.Length; i++)
            {
                var stone = recognition.stones[i];
                if (stone != null) origins[i] = stone.Motion;
                if (stone != null && stone.IsFlipping) unavailableUntil[i] = Time.time + flipReleaseGrace;
                if (unavailableUntil.TryGetValue(i, out float until) && Time.time < until) unavailable.Add(i);
            }
            float delta = Mathf.Max(0, Time.time - lastScanTime); lastScanTime = Time.time;
            var playerChanged = RealtimeCaptures.PlayerChanges(previousSnapshot, snapshot, origins);
            previousSnapshot = snapshot;
            var participants = new HashSet<int>();
            var targets = detector.Scan(snapshot, unavailable, delta, lineBreakSeconds, playerChanged, participants);
            if (targets.Count == 0) return;
            // Consume only player actions that actually formed these lines. Shared causes
            // also stop a struck neighbour from firing a delayed secondary capture.
            foreach (int id in participants)
                if (playerChanged.Contains(id) && origins.TryGetValue(id, out var cause)) cause.Consume();
            Vector3 up = recognition.boardOrigin.up;
            Vector3 axis = recognition.boardOrigin.TransformDirection(captureAxis.normalized);
            int flipped = 0;
            foreach (int id in targets)
            {
                var stone = recognition.stones[id];
                if (stone == null || !stone.isActiveAndEnabled || stone.IsFlipping ||
                    stone.status != StoneStatus.OnBoard || stone.Body == null || stone.Body.isKinematic) continue;
                Vector3 flipAxis = Vector3.ProjectOnPlane(axis, stone.transform.up).normalized;
                if (flipAxis.sqrMagnitude < .01f) flipAxis = stone.transform.right;
                stone.Flip(up, flipAxis, flipHeight, flipDuration);
                unavailableUntil[id] = Time.time + flipDuration + flipReleaseGrace;
                flipped++;
            }
            lastCaptureCount = flipped;
            lastResult = "New live line: flipping " + flipped;
            // No placement queue, no whole-board pause, no forced ownership change.
        }
    }
}
