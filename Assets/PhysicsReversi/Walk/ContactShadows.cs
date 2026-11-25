using UnityEngine;

namespace PhysicsReversi.Walk
{
    // Draws a flat shadow on the table under every stone and every player figure (ART.md "接地影").
    // Both are too small near the table for the real shadow map: a stone's shadow reaches only
    // ~0.6 past its edge and the figures' feet cast none at all, both lost to the shadow bias
    // (measured in Play, 2026-11-25). Their own shadow casting is turned off in the toon scene,
    // so this is their only shadow. A stone's falls along the sun, so a lifted stone's shadow
    // slides away; a figure's sits right under its feet, to show it standing on the table.
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
        [Tooltip("Decor standing up from the table (the board frame). The part of a shadow over one is drawn again on its top, since the table-level shadow is buried inside it.")]
        public Bounds[] raised = new Bounds[0];
        [Header("Player figures")]
        [Tooltip("Half width and half depth of the shadow under a figure (world). Wider than the torso (0.68), or the body hides it from the camera.")]
        public Vector2 figureSize = new Vector2(1.1f, .95f);
        [Range(0, 1)] public float figureAmount = .9f;

        // Above the table top. At 0.015 the shadows vanished on the cream table (not on the board
        // cells, 0.01 higher) and showed from 0.03; the table is truly at -0.5, so the cause is in
        // the depth test and not yet found (2026-11-25). Shadows lie over the recognition marks.
        const float Lift = .04f;
        CarryStone[] stones;
        WalkPlayer[] figures;
        Transform[] shadows;   // stones first, then figures
        Renderer[] renderers;
        Transform[] tops;      // the same shadows again, on the top of the frame bar they overlap
        Renderer[] topRenderers;
        MaterialPropertyBlock block;
        Light sun;
        static readonly int AmountId = Shader.PropertyToID("_Amount");
        static readonly int ClipRectId = Shader.PropertyToID("_ClipRect");

        void Start()
        {
            stones = FindObjectsByType<CarryStone>(FindObjectsInactive.Include);
            figures = FindObjectsByType<WalkPlayer>();
            int count = stones.Length + figures.Length;
            shadows = new Transform[count]; renderers = new Renderer[count];
            tops = new Transform[count]; topRenderers = new Renderer[count];
            var mesh = Disc(32);
            for (int i = 0; i < count; i++)
            {
                string owner = i < stones.Length ? stones[i].name : figures[i - stones.Length].name;
                renderers[i] = Make("Shadow " + owner, mesh, out shadows[i]);
                topRenderers[i] = Make("Shadow on frame " + owner, mesh, out tops[i]);
                topRenderers[i].enabled = false;
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

        // One shadow on the table, plus a copy on the top of the frame bar it overlaps most, cut
        // to that bar so it never hangs in the air. Where the table-level one runs under the bar
        // the bar hides it, so together they read as one shadow draped over the frame.
        void Place(int i, Vector3 at, Quaternion turn, float across, float along, float shade)
        {
            var scale = new Vector3(across, 1, along);
            block.SetFloat(AmountId, shade);
            block.SetVector(ClipRectId, new Vector4(-1e5f, -1e5f, 1e5f, 1e5f));
            if (!renderers[i].enabled) renderers[i].enabled = true;
            shadows[i].SetPositionAndRotation(new Vector3(at.x, groundHeight + Lift, at.z), turn);
            shadows[i].localScale = scale;
            renderers[i].SetPropertyBlock(block);

            float reach = Mathf.Max(across, along), best = 0; int bar = -1;
            for (int b = 0; b < raised.Length; b++)
            {
                float w = Mathf.Min(at.x + reach, raised[b].max.x) - Mathf.Max(at.x - reach, raised[b].min.x);
                float d = Mathf.Min(at.z + reach, raised[b].max.z) - Mathf.Max(at.z - reach, raised[b].min.z);
                if (w > 0 && d > 0 && w * d > best) { best = w * d; bar = b; }
            }
            bool onFrame = bar >= 0;
            if (topRenderers[i].enabled != onFrame) topRenderers[i].enabled = onFrame;
            if (!onFrame) return;
            var top = raised[bar];
            tops[i].SetPositionAndRotation(new Vector3(at.x, top.max.y + Lift, at.z), turn);
            tops[i].localScale = scale;
            block.SetVector(ClipRectId, new Vector4(top.min.x, top.min.z, top.max.x, top.max.z));
            topRenderers[i].SetPropertyBlock(block);
        }

        void Hide(int i)
        {
            if (renderers[i].enabled) renderers[i].enabled = false;
            if (topRenderers[i].enabled) topRenderers[i].enabled = false;
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
            var mesh = new Mesh { name = "Contact shadow disc", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
