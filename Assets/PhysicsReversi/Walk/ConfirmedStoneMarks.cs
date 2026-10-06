using UnityEngine;

namespace PhysicsReversi.Walk
{
    // Shows which stones are confirmed, on the stones themselves: a small square lying on the
    // upper face, squared up with the board whichever way the stone has turned. It lies
    // within the face, so the stone keeps its shape. The squares are made at start.
    public sealed class ConfirmedStoneMarks : MonoBehaviour
    {
        public WalkBoardRecognition recognition;
        public Material material;
        [Tooltip("On a stone showing black.")]
        public Color onBlack = new Color(.82f, .95f, .9f, 1);
        [Tooltip("On a stone showing white.")]
        public Color onWhite = new Color(.1f, .33f, .3f, 1);
        [Tooltip("Side of the square, as a fraction of the stone's width.")]
        [Range(.1f, .7f)] public float size = .3f;
        [Tooltip("Height above the stone's face.")]
        [Min(0)] public float lift = .01f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        MeshRenderer[] marks;
        // The face each square was last colored for: 1 black, 2 white, 0 not yet.
        int[] shown;
        MaterialPropertyBlock block;
        Mesh square;

        void LateUpdate()
        {
            if (recognition == null || recognition.stones == null || recognition.boardOrigin == null) return;
            if (marks == null) Build();
            var board = recognition.boardOrigin;
            for (int i = 0; i < marks.Length; i++)
            {
                var stone = recognition.stones[i]; var mark = marks[i];
                if (mark == null) continue;
                // A stone turning over in a capture stays confirmed, but has no upper face to mark until it lands.
                bool show = stone != null && stone.isActiveAndEnabled && stone.Confirmed && !stone.IsFlipping && stone.ownerId != 0;
                if (mark.enabled != show) mark.enabled = show;
                if (!show) continue;
                Vector3 face = stone.transform.up * StoneFaces.UpSign(stone.ownerId);
                Vector3 scale = stone.transform.lossyScale;
                Vector3 forward = Vector3.ProjectOnPlane(board.forward, face);
                if (forward.sqrMagnitude < .01f) forward = Vector3.ProjectOnPlane(board.right, face);
                // The stone fits a unit cylinder with its faces at local y = +1 and -1.
                mark.transform.SetPositionAndRotation(stone.transform.position + face * (scale.y + lift), Quaternion.LookRotation(forward, face));
                mark.transform.localScale = Vector3.one * scale.x * size;
                if (shown[i] == stone.ownerId) continue;
                shown[i] = stone.ownerId;
                block.SetColor(BaseColor, stone.ownerId == 1 ? onBlack : onWhite); mark.SetPropertyBlock(block);
            }
        }
        void Build()
        {
            square = new Mesh { name = "Confirmed stone square", hideFlags = HideFlags.HideAndDontSave };
            square.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 0, -.5f) };
            square.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            square.RecalculateNormals(); square.RecalculateBounds();
            block = new MaterialPropertyBlock();
            marks = new MeshRenderer[recognition.stones.Length]; shown = new int[marks.Length];
            for (int i = 0; i < marks.Length; i++)
            {
                if (recognition.stones[i] == null) continue;
                var made = new GameObject("Confirmed mark " + i, typeof(MeshFilter), typeof(MeshRenderer));
                made.transform.SetParent(transform, false);
                made.GetComponent<MeshFilter>().sharedMesh = square;
                var mark = made.GetComponent<MeshRenderer>(); mark.sharedMaterial = material;
                mark.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mark.receiveShadows = false;
                mark.enabled = false; marks[i] = mark;
            }
        }
        void OnDisable() { if (marks != null) foreach (var mark in marks) if (mark != null) mark.enabled = false; }
        void OnDestroy() { if (square != null) Destroy(square); }
    }
}
