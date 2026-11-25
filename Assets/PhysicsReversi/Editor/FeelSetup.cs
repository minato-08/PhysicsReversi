using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Adds the feedback layer (StoneFeel) next to the CarryAuthority, with the material its square
    // particles use. Running it again keeps tuned values and only refills missing references.
    public static class FeelSetup
    {
        const string SceneName = "PhysicsReversiToon";
        const string MaterialPath = "Assets/PhysicsReversi/WalkAssets/Toon/FeelParticle.mat";

        [MenuItem("Physics Reversi/Walk/Add Stone Feel (Trial)")]
        public static void Add()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != SceneName) { Debug.LogWarning("Open " + SceneName + " first."); return; }
            CarryAuthority authority = null;
            foreach (var root in scene.GetRootGameObjects()) if (authority == null) authority = root.GetComponentInChildren<CarryAuthority>(true);
            if (authority == null) { Debug.LogError("No CarryAuthority in the scene."); return; }

            // Sprites/Default blends by vertex colour and alpha, and with no texture each particle is a square.
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(Shader.Find("Sprites/Default")) { name = "FeelParticle" }; AssetDatabase.CreateAsset(material, MaterialPath); }

            var feel = authority.GetComponent<StoneFeel>();
            if (feel == null) feel = Undo.AddComponent<StoneFeel>(authority.gameObject);
            Undo.RecordObject(feel, "Add stone feel");
            feel.authority = authority; feel.particleMaterial = material;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Stone feel added on " + authority.name + ".");
        }
    }
}
