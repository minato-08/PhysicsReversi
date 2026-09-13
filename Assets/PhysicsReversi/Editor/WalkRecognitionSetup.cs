using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    public static class WalkRecognitionSetup
    {
        [MenuItem("Physics Reversi/Walk/Add Recognition Rings")]
        public static void AddRings()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "PhysicsReversiWalk") { Debug.LogWarning("Open PhysicsReversiWalk first."); return; }
            Transform root = null;
            foreach (var item in scene.GetRootGameObjects()) if (item.name == "Walk Prototype") root = item.transform;
            if (root == null) { Debug.LogWarning("Place Editable Scene Parts first."); return; }
            Transform board = root.Find("Board - 8 x 8 recessed cells");
            Transform systems = root.Find("Rules and local input");
            if (board == null || systems == null) { Debug.LogError("Board or Rules and local input was renamed. Restore the group name before setup."); return; }
            var cellObjects = new Transform[64];
            for (int z = 0; z < 8; z++) for (int x = 0; x < 8; x++)
            {
                var cell = board.Find("Cell " + x + "," + z);
                if (cell == null || cell.GetComponent<Collider>() == null)
                { Debug.LogError("Missing cell or collider: " + x + "," + z + ". No scene changes made."); return; }
                cellObjects[z * 8 + x] = cell;
            }
            const string path = "Assets/PhysicsReversi/WalkAssets/RecognitionRing.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(.2f, .95f, .85f) };
                AssetDatabase.CreateAsset(material, path);
            }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add editable recognition rings");
            var cells = new RecognitionCell[64];
            for (int i = 0; i < 64; i++)
            {
                var cell = cellObjects[i].GetComponent<RecognitionCell>();
                if (cell == null) cell = Undo.AddComponent<RecognitionCell>(cellObjects[i].gameObject);
                Undo.RecordObject(cell, "Wire cell"); cell.cellIndex = i; cells[i] = cell;
                if (cell.ring != null) continue;
                var obj = new GameObject("Recognition Ring"); Undo.RegisterCreatedObjectUndo(obj, "Create ring");
                obj.transform.SetParent(cell.transform, false);
                var line = obj.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true;
                line.sharedMaterial = material; line.widthMultiplier = .045f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.positionCount = 64;
                for (int n = 0; n < 64; n++)
                {
                    float a = n * Mathf.PI * 2 / 64;
                    line.SetPosition(n, new Vector3(Mathf.Cos(a) * 1.7f, .035f, Mathf.Sin(a) * 1.7f));
                }
                line.enabled = false; cell.ring = line;
            }
            var recognition = systems.GetComponent<WalkBoardRecognition>();
            if (recognition == null) recognition = Undo.AddComponent<WalkBoardRecognition>(systems.gameObject);
            Undo.RecordObject(recognition, "Wire recognition"); recognition.boardOrigin = board; recognition.cells = cells;
            recognition.stones = root.GetComponentsInChildren<CarryStone>(true);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject = systems.gameObject;
            Debug.Log("Recognition rings saved. Press Play: initial four should light after settling. Move stones to check recognition; select a stone for details in Inspector.");
        }
    }
}
