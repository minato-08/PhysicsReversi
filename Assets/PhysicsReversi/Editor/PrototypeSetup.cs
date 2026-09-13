using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PhysicsReversi.Editor
{
    public static class PrototypeSetup
    {
        [MenuItem("Physics Reversi/Run Rule Checks")]
        public static void RunRuleChecks() => Debug.Log("Physics Reversi: " + RulesChecks.Run() + " rule checks passed.");

        [MenuItem("Physics Reversi/Open Prototype")]
        public static void OpenPrototype()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string scenePath = "Assets/PhysicsReversi/Prototype.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            { EditorSceneManager.OpenScene(scenePath); return; }
            const string tuningPath = "Assets/PhysicsReversi/Tuning.asset";
            var config = AssetDatabase.LoadAssetAtPath<TuningConfig>(tuningPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<TuningConfig>();
                AssetDatabase.CreateAsset(config, tuningPath);
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Physics Reversi Prototype");
            root.AddComponent<PrototypeGame>().settings = config;
            EditorSceneManager.SaveScene(scene, scenePath);
            Selection.activeGameObject = root;
            Debug.Log("Physics Reversi: press Play. Geometry is generated at runtime. Start with the FIRE button or Space.");
        }
    }
}
