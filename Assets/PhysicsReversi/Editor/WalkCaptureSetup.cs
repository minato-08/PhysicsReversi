using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    public static class WalkCaptureSetup
    {
        static Transform FindRoot()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return null; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "PhysicsReversiWalk") { Debug.LogWarning("Open PhysicsReversiWalk first."); return null; }
            foreach (var item in scene.GetRootGameObjects()) if (item.name == "Walk Prototype") return item.transform;
            Debug.LogWarning("Walk Prototype is missing."); return null;
        }
        [MenuItem("Physics Reversi/Walk/Add Capture Rules")]
        public static void AddRules()
        {
            var root = FindRoot(); if (root == null) return;
            var recognition = root.GetComponentInChildren<WalkBoardRecognition>();
            var authority = root.GetComponentInChildren<CarryAuthority>();
            if (recognition == null || authority == null) { Debug.LogWarning("Add Recognition Rings first."); return; }
            var black = AssetDatabase.LoadAssetAtPath<Material>("Assets/PhysicsReversi/WalkAssets/BlackStone.mat");
            var white = AssetDatabase.LoadAssetAtPath<Material>("Assets/PhysicsReversi/WalkAssets/WhiteStone.mat");
            if (black == null || white == null) { Debug.LogError("BlackStone / WhiteStone materials are missing."); return; }
            var capture = recognition.GetComponent<WalkCaptureController>();
            if (capture == null) capture = Undo.AddComponent<WalkCaptureController>(recognition.gameObject);
            Undo.RecordObject(capture, "Wire capture rules");
            capture.authority = authority; capture.recognition = recognition;
            if (capture.blackMaterial == null) capture.blackMaterial = black;
            if (capture.whiteMaterial == null) capture.whiteMaterial = white;
            Undo.RecordObject(recognition, "Refresh registered stones");
            recognition.stones = root.GetComponentsInChildren<CarryStone>(true);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene); EditorSceneManager.SaveScene(root.gameObject.scene);
            Selection.activeGameObject = capture.gameObject;
            Debug.Log("Live capture saved. Any newly formed sandwich can trigger a flip, including lines formed by pushing existing stones.");
        }
        [MenuItem("Physics Reversi/Walk/Place Capture Practice")]
        public static void PlacePractice()
        {
            var root = FindRoot(); if (root == null) return;
            var recognition = root.GetComponentInChildren<WalkBoardRecognition>();
            if (recognition == null || recognition.boardOrigin == null) { Debug.LogWarning("Add Recognition Rings first."); return; }
            if (root.Find("Capture Practice") != null) { Debug.Log("Capture Practice already exists; its placement is preserved."); return; }
            const string folder = "Assets/PhysicsReversi/WalkAssets/";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Stone.prefab");
            var black = AssetDatabase.LoadAssetAtPath<Material>(folder + "BlackStone.mat");
            var white = AssetDatabase.LoadAssetAtPath<Material>(folder + "WhiteStone.mat");
            if (prefab == null || black == null || white == null) { Debug.LogError("Practice stone assets are missing."); return; }
            var targetMaterial = AssetDatabase.LoadAssetAtPath<Material>(folder + "CaptureTarget.mat");
            if (targetMaterial == null)
            {
                targetMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1, .75f, .1f) };
                AssetDatabase.CreateAsset(targetMaterial, folder + "CaptureTarget.mat");
            }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            var practice = new GameObject("Capture Practice"); practice.transform.SetParent(root, false);
            Undo.RegisterCreatedObjectUndo(practice, "Place capture practice");
            Place(prefab, practice.transform, recognition, "White - capture this", 2, 4, 1, white);
            Place(prefab, practice.transform, recognition, "Black - far end", 1, 4, 2, black);
            var target = new GameObject("TARGET - place black stone here (4,0)"); target.transform.SetParent(practice.transform, false);
            target.transform.position = recognition.boardOrigin.TransformPoint(new Vector3(.5f, 0, -3.5f) * recognition.cellWidth);
            target.transform.rotation = recognition.boardOrigin.rotation;
            target.transform.localScale = recognition.boardOrigin.lossyScale;
            var line = target.AddComponent<LineRenderer>(); line.sharedMaterial = targetMaterial;
            line.useWorldSpace = false; line.loop = true; line.widthMultiplier = .06f; line.positionCount = 4;
            float r = recognition.cellWidth * .35f;
            line.SetPositions(new[] { new Vector3(-r, .06f, -r), new Vector3(-r, .06f, r), new Vector3(r, .06f, r), new Vector3(r, .06f, -r) });
            Undo.RecordObject(recognition, "Register practice stones"); recognition.stones = root.GetComponentsInChildren<CarryStone>(true);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene); EditorSceneManager.SaveScene(root.gameObject.scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject = practice;
            Debug.Log("Practice saved: place a BLACK reserve in the yellow square near the starting edge. The WHITE stone beyond it should become BLACK. Remove the Capture Practice group to return to normal initial stones.");
        }
        static void Place(GameObject prefab, Transform parent, WalkBoardRecognition board, string name, int owner, int x, int z, Material material)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); obj.name = name;
            obj.transform.position = board.boardOrigin.TransformPoint(new Vector3((x - 3.5f) * board.cellWidth, .3f, (z - 3.5f) * board.cellWidth));
            obj.transform.rotation = board.boardOrigin.rotation;
            var stone = obj.GetComponent<CarryStone>(); stone.ownerId = owner; stone.reserveOwnerId = owner; stone.status = StoneStatus.OnBoard;
            var renderer = obj.GetComponent<MeshRenderer>();
            if (!stone.twoSided) renderer.sharedMaterial = material;
            else if (owner == 2) obj.transform.rotation *= Quaternion.Euler(180, 0, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(stone);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }
}
