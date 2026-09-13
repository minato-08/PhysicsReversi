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
        [Header("Prototype recognition thresholds")]
        [Range(.25f, 1)] public float minimumOverlap = .5f;
        [Range(.001f, .1f)] public float boundaryTolerance = .03f;
        public float stableSeconds = .5f;
        public float speedThreshold = .05f;
        public float spinThreshold = .15f;
        [Range(0, .5f)] public float edgeFaceTolerance = .1f;
        [Header("Latest confirmed snapshot (read during Play)")]
        [SerializeField] string recognitionState = "Waiting";
        [SerializeField] int blackRecognized;
        [SerializeField] int whiteRecognized;
        public string RecognitionState => recognitionState;
        public BoardRules.Snapshot Snapshot { get; private set; } = new BoardRules.Snapshot();
        public bool IsStable { get; private set; }
        public event System.Action<BoardRules.Snapshot> SnapshotConfirmed;
        public void RequestSnapshot()
        {
            dirty = true; quietTime = 0; IsStable = false;
            recognitionState = "Waiting for physics to settle";
            HideRings();
        }
        public void RefreshSnapshotOwnership()
        {
            for (int cell = 0; cell < 64; cell++)
            {
                int id = Snapshot.Ids[cell];
                if (id >= 0 && id < stones.Length && stones[id] != null)
                {
                    Snapshot.Owners[cell] = stones[id].ownerId;
                    owners[id] = stones[id].ownerId;
                }
            }
            blackRecognized = Snapshot.Count(1); whiteRecognized = Snapshot.Count(2);
        }
        float quietTime;
        bool dirty = true;
        Vector3[] positions;
        Quaternion[] rotations;
        StoneStatus[] statuses;
        int[] owners;
        bool[] contacts;
        bool[] active;
        MeshFilter[] filters;
        Vector3[][] meshVertices;

        void Start()
        {
            if (boardOrigin == null || cellWidth <= 0 || cells == null || cells.Length != 64 || stones == null)
            { Debug.LogError("Recognition references missing: run Add Recognition Rings in edit mode.", this); enabled = false; return; }
            int n = stones.Length;
            positions = new Vector3[n]; rotations = new Quaternion[n]; statuses = new StoneStatus[n];
            owners = new int[n]; contacts = new bool[n]; active = new bool[n]; filters = new MeshFilter[n]; meshVertices = new Vector3[n][];
            for (int i = 0; i < n; i++)
            {
                if (stones[i] == null) continue;
                filters[i] = stones[i].GetComponentInChildren<MeshFilter>();
                if (filters[i] != null && filters[i].sharedMesh != null) meshVertices[i] = filters[i].sharedMesh.vertices;
            }
            HideRings();
        }
        void FixedUpdate()
        {
            bool moving = false, changed = false;
            for (int i = 0; i < stones.Length; i++)
            {
                var s = stones[i]; if (s == null) continue;
                Vector3 localPosition = boardOrigin.InverseTransformPoint(s.transform.position) / cellWidth;
                bool nearBoard = Mathf.Abs(localPosition.x) < 5 && Mathf.Abs(localPosition.z) < 5 && localPosition.y > -2;
                bool eligible = s.isActiveAndEnabled && s.status == StoneStatus.OnBoard;
                if (s.IsFlipping) moving = true;
                if (s.status != statuses[i] || s.ownerId != owners[i] || s.TouchingBoard != contacts[i] || active[i] != s.isActiveAndEnabled)
                { changed = true; quietTime = 0; }
                if (eligible)
                {
                    if ((s.transform.position - positions[i]).sqrMagnitude > .000001f || Quaternion.Angle(s.transform.rotation, rotations[i]) > .02f) changed = true;
                    if (nearBoard && (s.Body.linearVelocity.sqrMagnitude > speedThreshold * speedThreshold ||
                        s.Body.angularVelocity.sqrMagnitude > spinThreshold * spinThreshold)) moving = true;
                }
                positions[i] = s.transform.position; rotations[i] = s.transform.rotation;
                statuses[i] = s.status; owners[i] = s.ownerId; contacts[i] = s.TouchingBoard;
                active[i] = s.isActiveAndEnabled;
            }
            if (changed) dirty = true;
            if (moving)
            {
                quietTime = 0; IsStable = false; dirty = true;
                recognitionState = "Moving: rings hidden, snapshot unchanged"; HideRings(); return;
            }
            quietTime += Time.fixedDeltaTime;
            if (!dirty) return;
            if (quietTime < stableSeconds) return;
            Recognize(); dirty = false; IsStable = true;
            recognitionState = "Stable: recognition confirmed";
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
                owners[i] = stone.ownerId;
                if (stone.ownerId == 0)
                { stone.SetRecognition("Unrecognized: standing on edge, neither face is up"); continue; }
                if (!stone.TouchingBoard)
                { stone.SetRecognition("Unrecognized: no direct board support (stacked / outside)"); continue; }
                if (meshVertices[i] == null)
                { stone.SetRecognition("Unrecognized: missing readable mesh"); continue; }
                var points = new List<BoardRules.Point>();
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
                    ? "Recognized: cell " + cell % 8 + "," + cell / 8 + " / overlap " + c.Ratio.ToString("P0")
                    : "Unrecognized: boundary / overlap / competition. Best cell " + c.FirstCell + " / " + c.Ratio.ToString("P0"));
            }
            foreach (var cell in cells)
                if (cell != null && cell.ring != null) cell.ring.enabled = Snapshot.Ids[cell.cellIndex] >= 0;
            blackRecognized = Snapshot.Count(1); whiteRecognized = Snapshot.Count(2);
        }
        void HideRings()
        {
            if (cells == null) return;
            foreach (var cell in cells) if (cell != null && cell.ring != null) cell.ring.enabled = false;
        }
        void OnDisable() => HideRings();
    }
}
