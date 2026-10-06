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
        [Header("Confirmed stones")]
        [Tooltip("A stone recognized in the same cell for this long becomes confirmed.")]
        [Min(0)] public float confirmSeconds = 1.5f;
        [Tooltip("Off: time on a cell confirms nothing. Only a capture confirms a stone (see Walk Capture Controller), and the stones that start on the board are confirmed from the first.")]
        public bool confirmByTime = true;
        [Tooltip("A confirmed stone out of its cell for this long comes loose again. Shorter dropouts are ignored.")]
        [Min(0)] public float loosenSeconds = .5f;
        [Tooltip("A confirmed stone at the center of its cell weighs this many times as much, and so is harder to shove. 1 turns the hold off.")]
        [Min(1)] public float holdMultiplier = 1;
        [Tooltip("Within this distance of the cell's center, in cells, a confirmed stone is held fully.")]
        [Range(0, .5f)] public float fullHoldDistance = .1f;
        [Tooltip("At this distance from the cell's center, in cells, a confirmed stone is not held at all. The middle of an edge is at .5.")]
        [Range(0, .75f)] public float zeroHoldDistance = .45f;
        [Tooltip("Color of the corner marks around a confirmed stone. Other recognized cells keep the mark material's own color.")]
        public Color confirmedMark = new Color(.82f, .95f, .9f, 1);
        [Header("Current board")]
        [SerializeField] string recognitionState = "Starting";
        [SerializeField] int blackRecognized;
        [SerializeField] int whiteRecognized;
        [SerializeField] int confirmedStones;
        public string RecognitionState => recognitionState;
        public BoardRules.Snapshot Snapshot { get; private set; } = new BoardRules.Snapshot();
        // The confirmed stones only, each in the cell it is confirmed in: the settled position.
        public BoardRules.Snapshot Settled { get; private set; } = new BoardRules.Snapshot();
        public bool HasSnapshot { get; private set; }
        public event System.Action<BoardRules.Snapshot> SnapshotUpdated;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock confirmedLook;
        float elapsed;
        MeshFilter[] filters;
        Vector3[][] meshVertices;
        // Stones the scene starts with on the board, until each has been confirmed where it lies.
        bool[] opening;

        void Start()
        {
            if (boardOrigin == null || cellWidth <= 0 || cells == null || cells.Length != 64 || stones == null)
            { Debug.LogError("Recognition references missing: run Add Recognition Rings in edit mode.", this); enabled = false; return; }
            filters = new MeshFilter[stones.Length]; meshVertices = new Vector3[stones.Length][];
            opening = new bool[stones.Length];
            for (int i = 0; i < stones.Length; i++)
            {
                if (stones[i] == null) continue;
                opening[i] = stones[i].status == StoneStatus.OnBoard;
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
            float interval = Mathf.Max(.02f, updateInterval);
            if (elapsed < interval) return;
            elapsed -= interval;
            Recognize();
            Confirm(interval);
            HasSnapshot = true;
            recognitionState = "Live";
            SnapshotUpdated?.Invoke(Snapshot);
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
        // Every stone is ticked, not only the recognized ones: time out of a cell counts too.
        void Confirm(float deltaSeconds)
        {
            confirmedStones = 0;
            var settled = new BoardRules.Snapshot();
            for (int i = 0; i < stones.Length; i++)
            {
                var stone = stones[i]; if (stone == null) continue;
                int cell = System.Array.IndexOf(Snapshot.Ids, i);
                // With no confirming by time, the opening stones would otherwise never be part of the position.
                if (opening[i] && !confirmByTime)
                {
                    if (stone.status != StoneStatus.OnBoard) opening[i] = false;
                    else if (cell >= 0) { stone.ConfirmAt(cell); opening[i] = false; }
                }
                stone.TickConfirmation(cell, deltaSeconds, confirmByTime ? confirmSeconds : float.PositiveInfinity, loosenSeconds);
                if (stone.Confirmed) confirmedStones++;
                // Two stones can share a cell for a moment, while the one knocked out of it has not come loose yet.
                int held = stone.ConfirmedCell;
                if (held >= 0 && (settled.Ids[held] < 0 || cell == held)) { settled.Ids[held] = i; settled.Owners[held] = stone.ownerId; }
                // The better a confirmed stone is centered in its cell, the more firmly it is held.
                float hold = 0;
                if (stone.Confirmed && holdMultiplier > 1)
                {
                    Vector3 local = boardOrigin.InverseTransformPoint(stone.transform.position) / cellWidth;
                    hold = (float)StoneHold.Strength(StoneHold.CenterDistance(local.x, local.z, stone.ConfirmedCell), fullHoldDistance, zeroHoldDistance);
                }
                stone.SetHold(hold, holdMultiplier);
            }
            Settled = settled;
            // The marks of a cell deepen once its stone is confirmed there.
            if (confirmedLook == null) confirmedLook = new MaterialPropertyBlock();
            confirmedLook.SetColor(BaseColor, confirmedMark);
            foreach (var cell in cells)
            {
                if (cell == null || cell.marker == null) continue;
                int id = Snapshot.Ids[cell.cellIndex];
                cell.marker.SetPropertyBlock(id >= 0 && stones[id].ConfirmedCell == cell.cellIndex ? confirmedLook : null);
            }
        }
        void OnDisable()
        {
            if (cells == null) return;
            foreach (var cell in cells) if (cell != null && cell.marker != null) cell.marker.enabled = false;
        }
    }
}
