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
        static Mesh BuildMesh()
        {
            var vertices = new List<Vector3>(); var black = new List<int>(); var white = new List<int>();
            const int sides = 48;
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                var p = new Vector3(Mathf.Cos(a) * .5f, 0, Mathf.Sin(a) * .5f);
                var q = new Vector3(Mathf.Cos(b) * .5f, 0, Mathf.Sin(b) * .5f);
                Triangle(vertices, black, Vector3.up, q + Vector3.up, p + Vector3.up);
                Triangle(vertices, white, Vector3.down, p + Vector3.down, q + Vector3.down);
                Triangle(vertices, black, p, p + Vector3.up, q + Vector3.up);
                Triangle(vertices, black, p, q + Vector3.up, q);
                Triangle(vertices, white, p + Vector3.down, p, q);
                Triangle(vertices, white, p + Vector3.down, q, q + Vector3.down);
            }
            var mesh = new Mesh { name = "Black top - white bottom", subMeshCount = 2 };
            mesh.SetVertices(vertices); mesh.SetTriangles(black, 0); mesh.SetTriangles(white, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static void Triangle(List<Vector3> vertices, List<int> indices, Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
        }
    }
}
