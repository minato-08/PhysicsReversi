using UnityEngine;
using UnityEngine.UI;

namespace PhysicsReversi.Walk
{
    // Shows what a click would act on: a crosshair, plus an outline of the stone as seen
    // from the camera. The outline is drawn inside the stone's silhouette with a faint
    // tint over the rest, so the stone never looks larger or differently shaped.
    public sealed class AimHud : MonoBehaviour
    {
        public LocalWalkInput input;
        public Image crosshair;
        [Tooltip("Scene object that is moved onto the highlighted stone. Uses the SilhouetteHighlight materials.")]
        public MeshRenderer highlight;
        public Color idleColor = new Color(1, 1, 1, .55f);
        [Header("A click would grab this stone")]
        public Color grabEdge = new Color(1, .85f, .2f, 1);
        public Color grabFill = new Color(1, .85f, .2f, .04f);
        [Tooltip("Outline thickness in pixels on a 720-pixel-high screen.")]
        [Range(1, 12)] public float grabWidth = 4;
        [Header("The stone being carried (quieter)")]
        public Color heldEdge = new Color(1, 1, 1, .8f);
        public Color heldFill = new Color(1, 1, 1, .015f);
        [Range(1, 12)] public float heldWidth = 2.5f;
        const int Segments = 48;
        static readonly int EdgeColor = Shader.PropertyToID("_EdgeColor");
        static readonly int FillColor = Shader.PropertyToID("_FillColor");
        static readonly int Width = Shader.PropertyToID("_Width");
        MaterialPropertyBlock block;
        Mesh mesh;

        void LateUpdate()
        {
            var aimed = input != null ? input.AimedStone : null;
            var held = input != null && input.player != null ? input.player.HeldStone : null;
            if (crosshair != null) crosshair.color = aimed != null ? grabEdge : idleColor;
            if (highlight == null) return;
            var stone = held != null ? held : aimed;
            highlight.enabled = stone != null;
            if (stone == null) return;
            if (mesh == null) Build();
            highlight.transform.SetPositionAndRotation(stone.transform.position, stone.transform.rotation);
            highlight.transform.localScale = stone.transform.lossyScale;
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor(EdgeColor, held != null ? heldEdge : grabEdge);
            block.SetColor(FillColor, held != null ? heldFill : grabFill);
            block.SetFloat(Width, held != null ? heldWidth : grabWidth);
            highlight.SetPropertyBlock(block);
        }
        // The stone mesh is a unit cylinder: radius .5, faces at local y = +1 and -1.
        // Normals here are not for lighting: the shader reads them as "away from the stone"
        // (radial in xz, axial in y) to pull the mask inward on screen.
        void Build()
        {
            mesh = new Mesh { name = "Stone silhouette", hideFlags = HideFlags.HideAndDontSave };
            int top = Segments * 2, bottom = top + 1;
            var vertices = new Vector3[Segments * 2 + 2]; var normals = new Vector3[vertices.Length];
            var triangles = new int[Segments * 12];
            vertices[top] = Vector3.up; normals[top] = Vector3.up;
            vertices[bottom] = Vector3.down; normals[bottom] = Vector3.down;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2 / Segments, x = Mathf.Cos(a), z = Mathf.Sin(a);
                vertices[i] = new Vector3(x * .5f, 1, z * .5f); normals[i] = new Vector3(x, 1, z);
                vertices[Segments + i] = new Vector3(x * .5f, -1, z * .5f); normals[Segments + i] = new Vector3(x, -1, z);
                int next = (i + 1) % Segments, t = i * 12;
                triangles[t] = top; triangles[t + 1] = next; triangles[t + 2] = i;
                triangles[t + 3] = i; triangles[t + 4] = next; triangles[t + 5] = Segments + i;
                triangles[t + 6] = next; triangles[t + 7] = Segments + next; triangles[t + 8] = Segments + i;
                triangles[t + 9] = bottom; triangles[t + 10] = Segments + i; triangles[t + 11] = Segments + next;
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.triangles = triangles; mesh.RecalculateBounds();
            highlight.GetComponent<MeshFilter>().sharedMesh = mesh;
        }
        void OnDisable() { if (highlight != null) highlight.enabled = false; }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
