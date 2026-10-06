using UnityEngine;

namespace PhysicsReversi.Walk
{
    // Shows where each color would capture by placing a stone: a small square lying on the
    // cell, black's toward the left of the view and white's toward the right, so a cell
    // open to both shows both. The squares are made at start; the scene holds only this.
    public sealed class LegalMoveMarks : MonoBehaviour
    {
        public WalkBoardRecognition recognition;
        [Tooltip("Left and right are taken from this camera.")]
        public Camera view;
        public Material material;
        public Color blackMark = new Color(.02f, .025f, .03f, 1);
        public Color whiteMark = new Color(.92f, .9f, .8f, 1);
        [Tooltip("Side of a square, in world units.")]
        [Min(.05f)] public float size = .6f;
        [Tooltip("How far each color's square sits from the center of its cell, toward that color's side of the view.")]
        [Min(0)] public float offset = .45f;
        [Tooltip("Height above the cell, clear of the cell's own marks.")]
        [Min(0)] public float lift = .03f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        // [0] black's squares, [1] white's, by cell.
        MeshRenderer[][] marks;
        Mesh square;

        void OnEnable() { if (recognition != null) recognition.SnapshotUpdated += Show; }
        void OnDisable()
        {
            if (recognition != null) recognition.SnapshotUpdated -= Show;
            if (marks != null) foreach (var color in marks) foreach (var mark in color) if (mark != null) mark.enabled = false;
        }
        void Show(BoardRules.Snapshot snapshot)
        {
            if (marks == null) Build();
            for (int color = 0; color < 2; color++)
            {
                var legal = BoardRules.LegalMoves(recognition.Settled, color + 1);
                for (int cell = 0; cell < 64; cell++)
                    if (marks[color][cell] != null && marks[color][cell].enabled != legal[cell]) marks[color][cell].enabled = legal[cell];
            }
        }
        void Build()
        {
            square = new Mesh { name = "Legal move square", hideFlags = HideFlags.HideAndDontSave };
            square.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 0, -.5f) };
            square.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            square.RecalculateNormals(); square.RecalculateBounds();
            var board = recognition.boardOrigin;
            Vector3 right = Vector3.ProjectOnPlane(view != null ? view.transform.right : board.right, board.up).normalized;
            marks = new[] { new MeshRenderer[64], new MeshRenderer[64] };
            var block = new MaterialPropertyBlock();
            for (int color = 0; color < 2; color++)
            {
                block.SetColor(BaseColor, color == 0 ? blackMark : whiteMark);
                foreach (var cell in recognition.cells)
                {
                    if (cell == null) continue;
                    var made = new GameObject((color == 0 ? "Black" : "White") + " legal move " + cell.cellIndex, typeof(MeshFilter), typeof(MeshRenderer));
                    made.transform.SetParent(transform, false);
                    made.transform.SetPositionAndRotation(cell.transform.position + board.up * lift + right * (color == 0 ? -offset : offset), board.rotation);
                    made.transform.localScale = Vector3.one * size;
                    made.GetComponent<MeshFilter>().sharedMesh = square;
                    var mark = made.GetComponent<MeshRenderer>(); mark.sharedMaterial = material;
                    mark.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mark.receiveShadows = false;
                    mark.SetPropertyBlock(block); mark.enabled = false;
                    marks[color][cell.cellIndex] = mark;
                }
            }
        }
        void OnDestroy() { if (square != null) Destroy(square); }
    }
}
