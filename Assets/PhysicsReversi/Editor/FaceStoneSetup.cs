using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    public static class FaceStoneSetup
    {
        [MenuItem("Physics Reversi/Walk/Use Two-Sided Stones")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "PhysicsReversiWalk") { Debug.LogWarning("Open PhysicsReversiWalk first."); return; }
            Transform root = null;
            foreach (var obj in scene.GetRootGameObjects()) if (obj.name == "Walk Prototype") root = obj.transform;
            if (root == null) return;
            var board = root.GetComponentInChildren<WalkBoardRecognition>();
            if (board == null || board.boardOrigin == null) { Debug.LogWarning("Set up recognition first."); return; }
            const string folder = "Assets/PhysicsReversi/WalkAssets/";
            var black = AssetDatabase.LoadAssetAtPath<Material>(folder + "BlackStone.mat");
            var white = AssetDatabase.LoadAssetAtPath<Material>(folder + "WhiteStone.mat");
            if (black == null || white == null) return;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "TwoSidedStone.asset");
            if (mesh == null) { mesh = BuildMesh(); AssetDatabase.CreateAsset(mesh, folder + "TwoSidedStone.asset"); }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            foreach (var stone in root.GetComponentsInChildren<CarryStone>(true))
            {
                Undo.RecordObjects(new Object[] { stone, stone.transform, stone.GetComponent<MeshFilter>(), stone.GetComponent<MeshRenderer>() }, "Use two-sided stone");
                Apply(stone, mesh, black, white, board.boardOrigin.up);
                PrefabUtility.RecordPrefabInstancePropertyModifications(stone);
                PrefabUtility.RecordPrefabInstancePropertyModifications(stone.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(stone.GetComponent<MeshFilter>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(stone.GetComponent<MeshRenderer>());
            }
            string prefabPath = folder + "Stone.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                try { Apply(prefab.GetComponent<CarryStone>(), mesh, black, white, Vector3.up); PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            Undo.CollapseUndoOperations(group); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Two-sided stones saved: local +Y is BLACK, -Y is WHITE. Settled upper face determines ownership. Capture physically turns stones; materials never swap.");
        }
        static void Apply(CarryStone stone, Mesh mesh, Material black, Material white, Vector3 up)
        {
            stone.GetComponent<MeshFilter>().sharedMesh = mesh;
            stone.GetComponent<MeshRenderer>().sharedMaterials = new[] { black, white };
            if (!stone.twoSided)
            {
                stone.reserveOwnerId = stone.ownerId == 2 ? 2 : 1;
                Vector3 target = stone.ownerId == 2 ? -up : up;
                stone.transform.rotation = Quaternion.FromToRotation(stone.transform.up, target) * stone.transform.rotation;
                stone.twoSided = true;
            }
        }
        // Rebuilds the shared stone mesh in place, so every stone that already uses it changes
        // without touching the scene. Only the look changes: the collider is a separate cylinder.
        [MenuItem("Physics Reversi/Walk/Bevel Stone Edges")]
        public static void Bevel()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            const string path = "Assets/PhysicsReversi/WalkAssets/TwoSidedStone.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { AssetDatabase.CreateAsset(BuildMesh(), path); }
            else { Fill(mesh); EditorUtility.SetDirty(mesh); }
            AssetDatabase.SaveAssets();
            Debug.Log("Stone mesh rebuilt with beveled edges (" + BevelRadius + " world units). Outline and collider are unchanged.");
        }
        // World-space size of the rounded edge. Must stay below half the stone's thickness.
        const float BevelRadius = .06f;
        const int BevelSteps = 3;
        const int Sides = 48;
        static Mesh BuildMesh()
        {
            var mesh = new Mesh { name = "Black top - white bottom" };
            Fill(mesh); return mesh;
        }
        // The mesh stays inside the unit cylinder (radius .5, faces at local y = +1 and -1) and
        // still reaches its full radius on the side, so the outline seen from above is the same.
        static void Fill(Mesh mesh)
        {
            // Stones are scaled unevenly (wide and thin), so the bevel is sized in world units
            // and converted to local ones here. Scene stones take their scale from this prefab.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PhysicsReversi/WalkAssets/Stone.prefab");
            Vector3 scale = prefab != null ? prefab.transform.localScale : new Vector3(2.6f, .18f, 2.6f);
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var black = new List<int>(); var white = new List<int>();
            Half(vertices, normals, black, 1, scale);
            Half(vertices, normals, white, -1, scale);
            mesh.Clear(); mesh.subMeshCount = 2;
            mesh.SetVertices(vertices); mesh.SetNormals(normals);
            mesh.SetTriangles(black, 0); mesh.SetTriangles(white, 1);
            mesh.RecalculateBounds();
        }
        // One face of the stone: flat cap, rounded edge, then the side down to the middle seam.
        static void Half(List<Vector3> vertices, List<Vector3> normals, List<int> indices, int sign, Vector3 scale)
        {
            float radial = BevelRadius / scale.x, axial = BevelRadius / scale.y;
            int center = vertices.Count, rings = BevelSteps + 2;
            vertices.Add(new Vector3(0, sign, 0)); normals.Add(new Vector3(0, sign, 0));
            for (int ring = 0; ring < rings; ring++)
            {
                // Rings 0..BevelSteps sweep the quarter circle of the bevel; the last one is the seam.
                bool seam = ring == rings - 1;
                float t = seam ? Mathf.PI / 2 : ring * Mathf.PI / 2 / BevelSteps;
                float r = .5f - radial + radial * Mathf.Sin(t), y = seam ? 0 : 1 - axial + axial * Mathf.Cos(t);
                for (int i = 0; i < Sides; i++)
                {
                    float a = i * Mathf.PI * 2 / Sides, x = Mathf.Cos(a), z = Mathf.Sin(a);
                    vertices.Add(new Vector3(x * r, y * sign, z * r));
                    // Multiplying by the scale cancels the uneven scale the renderer applies to normals.
                    normals.Add(new Vector3(x * Mathf.Sin(t) * scale.x, Mathf.Cos(t) * sign * scale.y, z * Mathf.Sin(t) * scale.z).normalized);
                }
            }
            for (int i = 0; i < Sides; i++)
            {
                int next = (i + 1) % Sides;
                Triangle(indices, sign, center, center + 1 + next, center + 1 + i);
                for (int ring = 0; ring < rings - 1; ring++)
                {
                    int upper = center + 1 + ring * Sides, lower = upper + Sides;
                    Triangle(indices, sign, lower + i, upper + i, upper + next);
                    Triangle(indices, sign, lower + i, upper + next, lower + next);
                }
            }
        }
        // The lower half is the upper half mirrored, which turns its triangles inside out.
        static void Triangle(List<int> indices, int sign, int a, int b, int c)
        {
            indices.Add(a); indices.Add(sign > 0 ? b : c); indices.Add(sign > 0 ? c : b);
        }
    }
}
