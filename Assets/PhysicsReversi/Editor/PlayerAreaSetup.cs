using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Keeps both players on the table top (SPEC.md "場外のプレイヤー"): sets each WalkPlayer's
    // walkable area to the floor collider. Works in the flat scene and in the toon trial.
    public static class PlayerAreaSetup
    {
        [MenuItem("Physics Reversi/Walk/Keep Players On Table")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "PhysicsReversiWalkFlat" && scene.name != "PhysicsReversiToon") { Debug.LogWarning("Open PhysicsReversiWalkFlat or PhysicsReversiToon first."); return; }
            var top = GameObject.Find("Walkable surrounding floor");
            var area = top != null ? top.GetComponent<Collider>() : null;
            if (area == null) { Debug.LogError("The floor (Walkable surrounding floor) or its collider is missing."); return; }
            // Both players share the area through Player Tuning.
            var tuning = PlayerTuningSetup.Ensure(scene);
            Undo.RecordObject(tuning, "Keep players on table");
            tuning.walkableArea = area;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Both players now stay on " + top.name + ".");
        }
    }
}
