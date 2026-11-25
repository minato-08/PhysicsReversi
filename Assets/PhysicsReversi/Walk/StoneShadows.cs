using UnityEngine;

namespace PhysicsReversi.Walk
{
    // Draws a flat shadow on the table under every stone (ART.md "接地影"). The stones are too
    // thin for the real shadow map: at the sun's height their shadow reaches only ~0.6 past the
    // edge and is lost to the shadow bias (measured in Play, 2026-11-25). The stones' own shadow
    // casting is turned off in the toon scene, so this is their only shadow; it falls where the
    // sun would put it, like the figures' real shadows. Looks only: it reads the stones'
    // transforms and never touches their bodies or the rules.
    public sealed class StoneShadows : MonoBehaviour
    {
        public Material material;
        [Tooltip("Height of the table top the shadows lie on (world).")]
        public float groundHeight = -.5f;
        [Tooltip("Shadows are hidden past this area, so none hang in the air off the table.")]
        public Rect tableArea = new Rect(-22, -28, 44, 56);
        [Tooltip("Above this height (world) a lifted stone's shadow has shrunk and faded fully.")]
        public float fadeHeight = 5f;
        [Range(0, 1)] public float amount = .7f;

        const float Lift = .015f; // above the board cells, below the recognition marks
        CarryStone[] stones;
        Transform[] shadows;
        Renderer[] renderers;
        MaterialPropertyBlock block;
        Light sun;
        static readonly int AmountId = Shader.PropertyToID("_Amount");

        void Start()
        {
            stones = FindObjectsByType<CarryStone>(FindObjectsInactive.Include);
            shadows = new Transform[stones.Length]; renderers = new Renderer[stones.Length];
            var mesh = Disc(32);
            for (int i = 0; i < stones.Length; i++)
            {
                var obj = new GameObject("Shadow " + stones[i].name, typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(transform, false);
                obj.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                shadows[i] = obj.transform; renderers[i] = renderer;
            }
            block = new MaterialPropertyBlock();
            foreach (var light in FindObjectsByType<Light>()) if (light.type == LightType.Directional) sun = light;
        }

        void LateUpdate()
        {
            // How far a shadow slides across the table per unit of height: the sun's light
            // direction laid flat, divided by how steeply it falls.
            Vector3 slide = Vector3.zero;
            if (sun != null && sun.transform.forward.y < -.05f) slide = Vector3.ProjectOnPlane(sun.transform.forward, Vector3.up) / -sun.transform.forward.y;

            for (int i = 0; i < stones.Length; i++)
            {
                var stone = stones[i]; var t = stone.transform;
                Vector3 p = t.position;
                float radius = t.lossyScale.x * .5f, halfThickness = t.lossyScale.y;
                // Height of the stone's lowest point above the table; a stone that left the table hides its shadow.
                Vector3 up = t.up;
                float lowest = p.y - Mathf.Abs(up.y) * halfThickness - Mathf.Sqrt(Mathf.Max(0, 1 - up.y * up.y)) * radius;
                float height = lowest - groundHeight;
                bool visible = stone.gameObject.activeInHierarchy && stone.status != StoneStatus.Removed
                    && height > -.3f && height < fadeHeight && tableArea.Contains(new Vector2(p.x, p.z));
                if (renderers[i].enabled != visible) renderers[i].enabled = visible;
                if (!visible) continue;

                float lift = Mathf.Clamp01(Mathf.Max(0, height) / fadeHeight);
                // A tilted stone's footprint narrows along the direction it leans.
                Vector3 lean = new Vector3(up.x, 0, up.z);
                Quaternion turn = lean.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(lean.normalized, Vector3.up) : Quaternion.Euler(0, t.eulerAngles.y, 0);
                float across = radius * Mathf.Lerp(1.04f, .7f, lift);
                float along = Mathf.Max(across * Mathf.Abs(up.y), halfThickness * 1.6f);
                // The middle of the stone's height decides where the middle of its shadow falls.
                float middle = Mathf.Max(0, height) + halfThickness * Mathf.Abs(up.y);
                shadows[i].SetPositionAndRotation(new Vector3(p.x, groundHeight + Lift, p.z) + slide * middle, turn);
                shadows[i].localScale = new Vector3(across, 1, along);
                block.SetFloat(AmountId, amount * (1 - lift));
                renderers[i].SetPropertyBlock(block);
            }
        }

        // A unit disc (radius 1) in the XZ plane, facing up.
        static Mesh Disc(int sides)
        {
            var vertices = new Vector3[sides + 1]; var triangles = new int[sides * 3];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                vertices[i + 1] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                triangles[i * 3] = 0; triangles[i * 3 + 1] = (i + 1) % sides + 1; triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Stone shadow disc", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
