using UnityEngine;
using System.Collections.Generic;

namespace PhysicsReversi.Walk
{
    public sealed class WalkCaptureController : MonoBehaviour
    {
        public CarryAuthority authority;
        public WalkBoardRecognition recognition;
        [HideInInspector] public Material blackMaterial;
        [HideInInspector] public Material whiteMaterial;
        [Header("Physical half-turn")]
        [Range(.5f, 3)] public float flipDuration = 1.2f;
        [Range(1.5f, 4)] public float flipHeight = 1.8f;
        [Tooltip("Rotation axis relative to the board.")]
        public Vector3 captureAxis = Vector3.right;
        [Header("Latest placement (read during Play)")]
        [SerializeField] string lastResult = "Waiting for a placement";
        [SerializeField] int lastCaptureCount;
        public string LastResult => lastResult;
        public int LastCaptureCount => lastCaptureCount;
        readonly PlacementCaptures pending = new PlacementCaptures();

        void OnEnable()
        {
            if (authority != null) authority.StonePlaced += OnPlaced;
            if (recognition != null) recognition.SnapshotConfirmed += OnSnapshot;
        }
        void OnDisable()
        {
            if (authority != null) authority.StonePlaced -= OnPlaced;
            if (recognition != null) recognition.SnapshotConfirmed -= OnSnapshot;
            pending.Clear();
        }
        void Start()
        {
            if (authority == null || recognition == null)
            {
                Debug.LogError("Capture references missing. Run Add Capture Rules in edit mode.", this);
                enabled = false;
            }
        }
        void OnPlaced(CarryStone stone, int owner)
        {
            int id = System.Array.IndexOf(recognition.stones, stone);
            if (id < 0) { Debug.LogWarning("Stone is not registered with recognition. Refresh scene setup.", stone); return; }
            pending.Enqueue(id, owner);
            lastCaptureCount = 0; lastResult = "Waiting for placed stone to settle";
            recognition.RequestSnapshot();
        }
        void OnSnapshot(BoardRules.Snapshot snapshot)
        {
            var results = pending.Resolve(snapshot);
            if (results.Count == 0) return;
            // Ownership is never assigned here: only the settled face determines it.
            foreach (var result in results)
            {
                lastCaptureCount = result.CapturedIds.Count;
                lastResult = result.OriginCell < 0 ? "No capture: placed stone was not recognized"
                    : "Placed at " + result.OriginCell % 8 + "," + result.OriginCell / 8 + ": flipping " + lastCaptureCount;
                Debug.Log("Physics Reversi: " + lastResult, this);
            }
            var animated = new HashSet<int>();
            Vector3 up = recognition.boardOrigin.up;
            Vector3 axis = recognition.boardOrigin.TransformDirection(captureAxis.normalized);
            foreach (var result in results)
                foreach (int id in result.CapturedIds)
                {
                    // Shared capture targets receive a single half-turn.
                    if (!animated.Add(id)) continue;
                    var stone = recognition.stones[id];
                    if (stone == null || !stone.isActiveAndEnabled || stone.status != StoneStatus.OnBoard || stone.Body == null || stone.Body.isKinematic) continue;
                    Vector3 flipAxis = Vector3.ProjectOnPlane(axis, stone.transform.up).normalized;
                    if (flipAxis.sqrMagnitude < .01f) flipAxis = stone.transform.right;
                    stone.Flip(up, flipAxis, flipHeight, flipDuration);
                }
            // Landing triggers recognition only, never re-enqueues a capture origin.
            if (animated.Count > 0) recognition.RequestSnapshot();
        }
    }
}
