using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Puts the values both players share in one Player Tuning object (2026-11-27). Before, each
    // WalkPlayer and each ToyFigureMotion held its own copy, so the two players could drift
    // apart by mistake; the scenes' values were moved over then (see git history). Works in the
    // flat scene and in the toon trial; safe to run again.
    public static class PlayerTuningSetup
    {
        const string ObjectName = "Player Tuning";

        [MenuItem("Physics Reversi/Walk/Add Player Tuning")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "PhysicsReversiWalkFlat" && scene.name != "PhysicsReversiToon") { Debug.LogWarning("Open PhysicsReversiWalkFlat or PhysicsReversiToon first."); return; }
            bool existed = Find(scene) != null;
            var tuning = Ensure(scene);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Selection.activeObject = tuning;
            Debug.Log(existed ? "Player Tuning was already there; every player now uses it."
                              : "Player Tuning created with the default values; every player now uses it.");
        }

        // The scene's Player Tuning, created with the default values if missing, with every
        // player pointed at it.
        public static PlayerTuning Ensure(Scene scene)
        {
            var tuning = Find(scene);
            if (tuning == null)
            {
                var holder = new GameObject(ObjectName);
                SceneManager.MoveGameObjectToScene(holder, scene);
                Undo.RegisterCreatedObjectUndo(holder, "Add Player Tuning");
                tuning = holder.AddComponent<PlayerTuning>();
            }
            foreach (var player in Players(scene))
            {
                if (player.tuning == tuning) continue;
                Undo.RecordObject(player, "Use Player Tuning");
                player.tuning = tuning;
                PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            }
            return tuning;
        }

        static PlayerTuning Find(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<PlayerTuning>(true);
                if (found != null) return found;
            }
            return null;
        }

        static List<T> All<T>(Scene scene) where T : Component
        {
            var list = new List<T>();
            foreach (var root in scene.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
            return list;
        }

        static List<WalkPlayer> Players(Scene scene)
        {
            var players = All<WalkPlayer>(scene);
            players.Sort((a, b) => a.playerId.CompareTo(b.playerId));
            return players;
        }
    }
}
