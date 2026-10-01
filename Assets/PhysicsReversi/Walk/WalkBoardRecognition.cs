using System.Collections.Generic;
using UnityEngine;

namespace PhysicsReversi.Walk
{
    public sealed class WalkBoardRecognition : MonoBehaviour
    {
        public Transform boardOrigin;
        public float cellWidth = 4;
        public RecognitionCell[] cells;
        public CarryStone[] stones;
        [Header("Continuous recognition")]
        [Range(.02f, .2f)] public float updateInterval = .05f;
        [Range(.25f, 1)] public float minimumOverlap = .5f;
        [Range(.001f, .1f)] public float boundaryTolerance = .03f;
        [Range(0, .5f)] public float edgeFaceTolerance = .1f;
        [Header("Current board")]
        [SerializeField] string recognitionState = "Starting";
        [SerializeField] int blackRecognized;
        [SerializeField] int whiteRecognized;
        public string RecognitionState => recognitionState;
        public BoardRules.Snapshot Snapshot { get; private set; } = new BoardRules.Snapshot();
        public bool HasConfirmedSnapshot { get; private set; }
        public event System.Action<BoardRules.Snapshot> SnapshotConfirmed;
        float elapsed;
        MeshFilter[] filters;
        Vector3[][] meshVertices;

        void Start()
        {
            if (boardOrigin == null || cellWidth <= 0 || cells == null || cells.Length != 64 || stones == null)
            { Debug.LogError("Recognition references missing: run Add Recognition Rings in edit mode.", this); enabled = false; return; }
            filters = new MeshFilter[stones.Length]; meshVertices = new Vector3[stones.Length][];
            for (int i = 0; i < stones.Length; i++)
            {
                if (stones[i] == null) continue;
                filters[i] = stones[i].GetComponentInChildren<MeshFilter>();
                if (filters[i] != null && filters[i].sharedMesh != null)
                {
                    // The two-sided mesh repeats vertices for material seams and hard normals.
                    // Deduplicate once, rather than sorting all duplicates every live sample.
                    var unique = new HashSet<Vector3>(filters[i].sharedMesh.vertices);
                    meshVertices[i] = new Vector3[unique.Count]; unique.CopyTo(meshVertices[i]);
                }
            }
        }
        void FixedUpdate()
        {
            elapsed += Time.fixedDeltaTime;
            if (elapsed < Mathf.Max(.02f, updateInterval)) return;
            elapsed -= Mathf.Max(.02f, updateInterval);
            Recognize();
            HasConfirmedSnapshot = true;
            recognitionState = "Live";
            SnapshotConfirmed?.Invoke(Snapshot);
        }
        void Recognize()
        {
            var candidates = new List<BoardRules.Candidate>();
            for (int i = 0; i < stones.Length; i++)
            {
                var stone = stones[i]; if (stone == null) continue;
                if (!stone.isActiveAndEnabled || stone.status != StoneStatus.OnBoard)
                { stone.SetRecognition("Excluded: reserve, held or removed"); continue; }
                stone.ReadUpperFace(boardOrigin.up, edgeFaceTolerance);
                if (stone.ownerId == 0)
                { stone.SetRecognition("Unrecognized: standing on edge"); continue; }
                if (!stone.TouchingBoard)
                { stone.SetRecognition("Unrecognized: airborne, stacked or outside"); continue; }
                if (meshVertices[i] == null)
                { stone.SetRecognition("Unrecognized: missing readable mesh"); continue; }
                var points = new List<BoardRules.Point>(meshVertices[i].Length);
                foreach (var vertex in meshVertices[i])
                {
                    Vector3 local = boardOrigin.InverseTransformPoint(filters[i].transform.TransformPoint(vertex)) / cellWidth;
                    points.Add(new BoardRules.Point(local.x, local.z));
                }
                candidates.Add(new BoardRules.Candidate { Id = i, Owner = stone.ownerId, Polygon = BoardRules.Hull(points) });
            }
            Snapshot = BoardRules.Recognize(candidates, minimumOverlap, boundaryTolerance);
            foreach (var c in candidates)
            {
                int cell = System.Array.IndexOf(Snapshot.Ids, c.Id);
                stones[c.Id].SetRecognition(cell >= 0
                    ? "Live cell " + cell % 8 + "," + cell / 8 + " / " + c.Ratio.ToString("P0")
                    : "Unrecognized: boundary / overlap / competition");
            }
            foreach (var cell in cells)
                if (cell != null && cell.marker != null) cell.marker.enabled = Snapshot.Ids[cell.cellIndex] >= 0;
            blackRecognized = Snapshot.Count(1); whiteRecognized = Snapshot.Count(2);
        }
        void OnDisable()
        {
            if (cells == null) return;
            foreach (var cell in cells) if (cell != null && cell.marker != null) cell.marker.enabled = false;
        }
    }
}
