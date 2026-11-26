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
    // apart by mistake. Works in the flat scene and in the toon trial; safe to run again.
    public static class PlayerTuningSetup
    {
        const string ObjectName = "Player Tuning";

        [MenuItem("Physics Reversi/Walk/Gather Player Tuning")]
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
                              : "Player Tuning created from the players' own values; every player now uses it.");
        }

        // The scene's Player Tuning, with every player pointed at it. Made from the players' own
        // values if missing.
        public static PlayerTuning Ensure(Scene scene)
        {
            var tuning = Find(scene);
            if (tuning == null)
            {
                var holder = new GameObject(ObjectName);
                SceneManager.MoveGameObjectToScene(holder, scene);
                Undo.RegisterCreatedObjectUndo(holder, "Add Player Tuning");
                tuning = holder.AddComponent<PlayerTuning>();
                Gather(scene, tuning);
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

        // Every value of Player Tuning is copied from the field of the same name on the first
        // player (black) or the first figure. Where the second one differs, it is reported, since
        // only one value can be kept.
        static void Gather(Scene scene, PlayerTuning tuning)
        {
            var sources = new List<List<SerializedObject>>
            {
                Players(scene).ConvertAll(p => new SerializedObject(p)),
                All<ToyFigureMotion>(scene).ConvertAll(f => new SerializedObject(f)),
            };
            var target = new SerializedObject(tuning);
            var names = new List<string>();
            var property = target.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false)
                if (property.name != "m_Script") names.Add(property.name);
            var differences = new List<string>();
            int copied = 0;
            foreach (var name in names)
                foreach (var group in sources)
                {
                    if (group.Count == 0) continue;
                    var first = group[0].FindProperty(name);
                    if (first == null) continue;
                    target.CopyFromSerializedProperty(first); copied++;
                    for (int i = 1; i < group.Count; i++)
                    {
                        var other = group[i].FindProperty(name);
                        if (other != null && !SerializedProperty.DataEquals(first, other))
                            differences.Add(name + " (" + group[0].targetObject.name + " / " + group[i].targetObject.name + ")");
                    }
                    break;
                }
            target.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("Player Tuning: " + copied + " values taken from the first player and figure.");
            if (differences.Count > 0)
                Debug.LogWarning("These differed between the players; the first one's value was kept: " + string.Join(", ", differences));
        }
    }
}
