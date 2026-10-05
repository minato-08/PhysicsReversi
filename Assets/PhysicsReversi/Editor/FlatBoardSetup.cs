using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Builds the flat-board trial as a copy of the walk scene, so the bowl-cell scene stays as it is.
    public static class FlatBoardSetup
    {
        const string Source = "Assets/Scenes/PhysicsReversiWalk.unity";
        const string Target = "Assets/Scenes/PhysicsReversiWalkFlat.unity";
        const string Folder = "Assets/PhysicsReversi/WalkAssets";
        // The cells are drawn this far above the floor they lie on, so the two do not flicker.
        const float DrawOffset = .01f;

        [MenuItem("Physics Reversi/Walk/Create Flat Trial Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Target) != null)
            { Debug.LogWarning("The flat trial scene already exists and may hold hand-tuned values. Delete " + Target + " to build it again."); return; }
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) { Debug.LogWarning("Save or discard the open scene first."); return; }
            if (!AssetDatabase.CopyAsset(Source, Target)) { Debug.LogError("Could not copy " + Source + "."); return; }
            var scene = EditorSceneManager.OpenScene(Target);
            Transform root = null;
            foreach (var item in scene.GetRootGameObjects()) if (item.name == "Walk Prototype") root = item.transform;
            Transform board = root != null ? root.Find("Board - 8 x 8 recessed cells") : null;
            Transform floor = root != null ? root.Find("Stage/Walkable surrounding floor") : null;
            if (board == null || floor == null || floor.GetComponent<Collider>() == null)
            { Debug.LogError("Board or floor was renamed in the walk scene. The copy at " + Target + " was left unchanged; delete it before trying again."); return; }

            // The floor already runs under the board. Lowering the board onto it leaves one
            // unbroken surface for stones to slide on, with no seams between cells and no step.
            float floorTop = floor.position.y + floor.lossyScale.y * .5f;
            board.position = new Vector3(board.position.x, floorTop + DrawOffset, board.position.z);
            var flat = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/FlatCell.asset");
            if (flat == null) { flat = MakeFlatCell(); AssetDatabase.CreateAsset(flat, Folder + "/FlatCell.asset"); }
            foreach (var cell in board.GetComponentsInChildren<RecognitionCell>(true))
            {
                cell.GetComponent<MeshFilter>().sharedMesh = flat;
                var bowl = cell.GetComponent<Collider>(); if (bowl != null) Object.DestroyImmediate(bowl);
            }
            if (floor.GetComponent<BoardSurface>() == null) floor.gameObject.AddComponent<BoardSurface>();

            // Friction of its own: nothing has to slide into a bowl any more, and tuning it
            // must not change the bowl-cell scene. An existing asset keeps its tuned values.
            var contact = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Folder + "/ContactFlat.physicMaterial");
            if (contact == null)
            {
                contact = new PhysicsMaterial("ContactFlat") { staticFriction = .25f, dynamicFriction = .2f, bounciness = .12f };
                AssetDatabase.CreateAsset(contact, Folder + "/ContactFlat.physicMaterial");
            }
            floor.GetComponent<Collider>().sharedMaterial = contact;
            foreach (var stone in root.GetComponentsInChildren<CarryStone>(true))
            {
                foreach (var shape in stone.GetComponentsInChildren<Collider>(true))
                { shape.sharedMaterial = contact; PrefabUtility.RecordPrefabInstancePropertyModifications(shape); }
                if (stone.status != StoneStatus.OnBoard) continue;
                // Stones that started in the bowls come down with the board.
                var position = stone.transform.position; position.y = floorTop + .2f; stone.transform.position = position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(stone.transform);
            }

            // A player this narrow, climbing steps this low, pushes a lying stone instead of
            // walking up onto it (measured on the flat floor; see README).
            foreach (var player in root.GetComponentsInChildren<WalkPlayer>(true))
            {
                var controller = player.GetComponent<CharacterController>();
                controller.radius = .25f; controller.stepOffset = .1f;
            }

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Flat trial scene created and opened: " + Target + ". The walk scene with bowl cells is unchanged.");
        }

        static Mesh MakeFlatCell()
        {
            var mesh = new Mesh { name = "Flat cell 4m" };
            mesh.vertices = new[] { new Vector3(-2, 0, -2), new Vector3(-2, 0, 2), new Vector3(2, 0, 2), new Vector3(2, 0, -2) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
