using UnityEngine;

namespace PhysicsReversi.Walk
{
    // Projects a flat shadow under every stone and every player figure (ART.md "接地影").
    // Both are too small near the table for the real shadow map: a stone's shadow reaches only
    // ~0.6 past its edge and the figures' feet cast none at all, both lost to the shadow bias
    // (measured in Play, 2026-11-25). Their own shadow casting is turned off in the toon scene,
    // so this is their only shadow. A stone's falls along the sun, so a lifted stone's shadow
    // slides away; a figure's sits right under its feet, to show it standing on the table.
    // Each shadow is a box drawn with GroundShadow.shader, which darkens the up-facing surfaces
    // inside it (a screen-space decal): the table, the top of the board frame, the cell marks.
    // Looks only: it reads transforms and never touches bodies, controllers or the rules.
    public sealed class ContactShadows : MonoBehaviour
    {
        public Material material;
        [Tooltip("Height of the table top the shadows lie on (world).")]
        public float groundHeight = -.5f;
        [Tooltip("Shadows are hidden past this area, so none hang in the air off the table.")]
        public Rect tableArea = new Rect(-22, -28, 44, 56);
        [Tooltip("Above this height (world) a lifted stone's shadow has shrunk and faded fully.")]
        public float fadeHeight = 5f;
        [Range(0, 1)] public float amount = .7f;
        [Tooltip("Decor standing up from the table (the board frame). The shadow boxes reach up to its top, and a stone resting on it measures its height from there.")]
        public Bounds[] raised = new Bounds[0];
        [Header("Player figures")]
        [Tooltip("Half width and half depth of the shadow under a figure (world). Wider than the torso (0.68), or the body hides it from the camera.")]
        public Vector2 figureSize = new Vector2(1.1f, .95f);
        [Range(0, 1)] public float figureAmount = .9f;

        // How far the boxes reach below the table and above the highest raised top. Shoes and
        // stone sides inside the box are not shaded (they do not face up), and stone tops (0.36
        // above the table) stay above it.
        const float Below = .1f, Above = .06f;
        CarryStone[] stones;
        WalkPlayer[] figures;
        Transform[] shadows;   // stones first, then figures
        Renderer[] renderers;
        MaterialPropertyBlock block;
        Light sun;
        float boxBottom, boxHeight;
        static readonly int AmountId = Shader.PropertyToID("_Amount");

        void Start()
        {
            stones = FindObjectsByType<CarryStone>(FindObjectsInactive.Include);
            figures = FindObjectsByType<WalkPlayer>();
            int count = stones.Length + figures.Length;
            shadows = new Transform[count]; renderers = new Renderer[count];
            var mesh = Box();
            for (int i = 0; i < count; i++)
            {
                string owner = i < stones.Length ? stones[i].name : figures[i - stones.Length].name;
                renderers[i] = Make("Shadow " + owner, mesh, out shadows[i]);
            }
            float top = groundHeight;
            foreach (var b in raised) top = Mathf.Max(top, b.max.y);
            boxBottom = groundHeight - Below; boxHeight = top + Above - boxBottom;
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
                float ground = GroundAt(p);
                float height = lowest - ground;
                bool visible = stone.gameObject.activeInHierarchy && stone.status != StoneStatus.Removed
                    && height > -.3f && height < fadeHeight && tableArea.Contains(new Vector2(p.x, p.z));
                if (!visible) { Hide(i); continue; }

                float lift = Mathf.Clamp01(Mathf.Max(0, height) / fadeHeight);
                // A tilted stone's footprint narrows along the direction it leans.
                Vector3 lean = new Vector3(up.x, 0, up.z);
                Quaternion turn = lean.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(lean.normalized, Vector3.up) : Quaternion.Euler(0, t.eulerAngles.y, 0);
                float across = radius * Mathf.Lerp(1.04f, .7f, lift);
                float along = Mathf.Max(across * Mathf.Abs(up.y), halfThickness * 1.6f);
                // The middle of the stone's height decides where the middle of its shadow falls.
                float middle = Mathf.Max(0, height) + halfThickness * Mathf.Abs(up.y);
                Place(i, new Vector3(p.x, 0, p.z) + slide * middle, turn, across, along, amount * (1 - lift));
            }

            for (int f = 0; f < figures.Length; f++)
            {
                int i = stones.Length + f;
                var t = figures[f].transform; Vector3 p = t.position;
                bool visible = figures[f].isActiveAndEnabled && tableArea.Contains(new Vector2(p.x, p.z));
                if (!visible) { Hide(i); continue; }
                Place(i, p, Quaternion.Euler(0, t.eulerAngles.y, 0), figureSize.x, figureSize.y, figureAmount);
            }
        }

        // The shadow's box: the disc's footprint across, from just under the table to just over the frame.
        void Place(int i, Vector3 at, Quaternion turn, float across, float along, float shade)
        {
            block.SetFloat(AmountId, shade);
            if (!renderers[i].enabled) renderers[i].enabled = true;
            shadows[i].SetPositionAndRotation(new Vector3(at.x, boxBottom + boxHeight * .5f, at.z), turn);
            shadows[i].localScale = new Vector3(across * 2, boxHeight, along * 2);
            renderers[i].SetPropertyBlock(block);
        }

        void Hide(int i)
        {
            if (renderers[i].enabled) renderers[i].enabled = false;
        }

        Renderer Make(string name, Mesh mesh, out Transform t)
        {
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(transform, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            t = obj.transform;
            return renderer;
        }

        // The height of what a stone at this spot rests on: the table, or the top of the frame.
        float GroundAt(Vector3 p)
        {
            float y = groundHeight;
            foreach (var b in raised)
                if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z) y = Mathf.Max(y, b.max.y);
            return y;
        }

        // A unit cube (-0.5..0.5). Only its back faces are drawn; the shader does the rest.
        static Mesh Box()
        {
            var vertices = new Vector3[8];
            for (int v = 0; v < 8; v++) vertices[v] = new Vector3((v & 1) - .5f, ((v >> 1) & 1) - .5f, ((v >> 2) & 1) - .5f);
            var triangles = new[]
            {
                0, 2, 3, 0, 3, 1,  4, 5, 7, 4, 7, 6,  0, 1, 5, 0, 5, 4,
                2, 6, 7, 2, 7, 3,  0, 4, 6, 0, 6, 2,  1, 3, 7, 1, 7, 5,
            };
            var mesh = new Mesh { name = "Contact shadow box", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
