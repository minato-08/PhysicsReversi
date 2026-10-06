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
        [Header("Capture rule")]
        [Tooltip("Off: a line captures when it forms, whichever of its stones a player moved. On: as in ordinary reversi, a loose stone captures only as one end of a line, only against confirmed stones, and is confirmed by doing so. It is judged once, when it has kept its cell for the recognition's Confirm Seconds; 0 judges it as it arrives.")]
        public bool captureByLegalMove;
        [Header("Capturing stones")]
        [Tooltip("The stones at both ends of a line that captures are confirmed at once, so the stone that made the capture cannot be picked up and used again.")]
        public bool confirmCapturingStones = true;
        [Header("Live capture")]
        [SerializeField] string lastResult = "Watching for new lines";
        [SerializeField] int lastCaptureCount;
        public string LastResult => lastResult;
        public int LastCaptureCount => lastCaptureCount;
        readonly RealtimeCaptures detector = new RealtimeCaptures();
        readonly LegalMoveCaptures legalMoves = new LegalMoveCaptures();
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
            detector.Clear(); legalMoves.Clear(); unavailableUntil.Clear();
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
            if (captureByLegalMove) { previousSnapshot = snapshot; CaptureByLegalMove(snapshot, unavailable, delta); return; }
            var playerChanged = RealtimeCaptures.PlayerChanges(previousSnapshot, snapshot, origins);
            previousSnapshot = snapshot;
            var participants = new HashSet<int>();
            var capturingEnds = new HashSet<int>();
            var targets = detector.Scan(snapshot, unavailable, delta, lineBreakSeconds, playerChanged, participants, capturingEnds);
            if (targets.Count == 0) return;
            // Capturing commits the stones that did it. Left loose, the stone just placed could
            // be picked up again before it confirmed and used to capture line after line.
            if (confirmCapturingStones)
                foreach (int id in capturingEnds)
                    if (recognition.stones[id] != null) recognition.stones[id].ConfirmAt(System.Array.IndexOf(snapshot.Ids, id));
            // Consume only player actions that actually formed these lines. Shared causes
            // also stop a struck neighbour from firing a delayed secondary capture.
            foreach (int id in participants)
                if (playerChanged.Contains(id) && origins.TryGetValue(id, out var cause)) cause.Consume();
            Flip(targets);
            // No placement queue, no whole-board pause, no forced ownership change.
        }
        // A loose stone a player has moved is judged once it has stayed in a cell. On a legal move it captures and is confirmed there.
        void CaptureByLegalMove(BoardRules.Snapshot snapshot, HashSet<int> unavailable, float delta)
        {
            var loose = new HashSet<int>();
            for (int i = 0; i < recognition.stones.Length; i++)
            {
                var stone = recognition.stones[i];
                if (stone != null && stone.status == StoneStatus.OnBoard && !stone.Confirmed && !stone.IsFlipping &&
                    stone.Motion != null && stone.Motion.CanCapture) loose.Add(i);
            }
            var moves = legalMoves.Scan(snapshot, recognition.Settled, loose, unavailable, delta, recognition.confirmSeconds);
            if (moves.Count == 0) return;
            var targets = new HashSet<int>();
            foreach (var move in moves)
            {
                var stone = recognition.stones[move.Id];
                stone.ConfirmAt(move.Cell);
                // The action is spent, for this stone and for any it struck on the way.
                stone.Motion.Consume();
                foreach (int id in move.CapturedIds) targets.Add(id);
            }
            Flip(targets);
        }
        void Flip(IEnumerable<int> targets)
        {
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
        }
    }
}
