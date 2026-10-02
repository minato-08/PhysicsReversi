using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Adds the White player to a scene placed before two-player support.
    public static class SecondPlayerSetup
    {
        [MenuItem("Physics Reversi/Walk/Add Second Player")]
        public static void AddSecondPlayer()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "PhysicsReversiWalk") { Debug.LogWarning("Open Assets/Scenes/PhysicsReversiWalk first."); return; }
            GameObject root = null;
            foreach (var item in scene.GetRootGameObjects()) if (item.name == "Walk Prototype") root = item;
            if (root == null) { Debug.LogWarning("Run Place Editable Scene Parts first."); return; }
            var input = root.GetComponentInChildren<LocalWalkInput>(true);
            if (input == null) { Debug.LogWarning("LocalWalkInput is missing under Walk Prototype."); return; }
            WalkPlayer black = null, white = null;
            foreach (var candidate in root.GetComponentsInChildren<WalkPlayer>(true))
            {
                if (candidate.playerId == 1 && black == null) black = candidate;
                if (candidate.playerId == 2 && white == null) white = candidate;
            }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add second player");
            bool created = white == null;
            if (created)
            {
                white = WalkSceneSetup.CreatePlayer(root.transform, 2, WalkSceneSetup.SecondPlayerMaterial());
                Undo.RegisterCreatedObjectUndo(white.gameObject, "Add second player");
                // Tuning done on Player 1 in the Inspector carries over; identity and placement do not.
                if (black != null)
                {
                    int id = white.playerId; var carryPoint = white.carryPoint;
                    EditorUtility.CopySerialized(black, white);
                    white.playerId = id; white.carryPoint = carryPoint;
                    carryPoint.localPosition = black.carryPoint != null ? black.carryPoint.localPosition : carryPoint.localPosition;
                }
            }
            Undo.RecordObject(input, "Register players");
            input.players = black != null ? new[] { black, white } : new[] { white };
            EditorUtility.SetDirty(input);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject = white.gameObject;
            Debug.Log(created
                ? "Player 2 - White added and saved. White uses IJKL + H or the second gamepad. Run Add Play HUD to give it a grab highlight."
                : "Player 2 already exists; no duplicate was added. Both players are registered for input.");
        }
    }
}
