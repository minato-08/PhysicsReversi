using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Turns on the legal-move trial in the flat scene: the rule switches, and the marks that
    // show where each color can capture and which stones are confirmed.
    public static class LegalMoveSetup
    {
        const string SceneName = "PhysicsReversiWalkFlat";
        const string MaterialPath = "Assets/PhysicsReversi/WalkAssets/BoardMark.mat";

        [MenuItem("Physics Reversi/Walk/Add Legal Move Rules")]
        public static void Add()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != SceneName) { Debug.LogWarning("Open " + SceneName + " first."); return; }
            Transform root = null;
            foreach (var item in scene.GetRootGameObjects()) if (item.name == "Walk Prototype") root = item.transform;
            var recognition = root != null ? root.GetComponentInChildren<WalkBoardRecognition>() : null;
            var capture = root != null ? root.GetComponentInChildren<WalkCaptureController>() : null;
            var input = root != null ? root.GetComponentInChildren<LocalWalkInput>() : null;
            if (recognition == null || capture == null || input == null || input.view == null)
            { Debug.LogError("Recognition, capture rules or local input is missing from the scene. No scene changes made."); return; }

            // One flat, unlit material for both kinds of mark; each mark sets its own color.
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };
                material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_ZWrite", 0);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add legal move rules");
            var holder = root.Find("Board Marks");
            if (holder == null)
            {
                var made = new GameObject("Board Marks"); Undo.RegisterCreatedObjectUndo(made, "Create board marks");
                made.transform.SetParent(root, false); holder = made.transform;
            }
            // Colors and sizes tuned by hand are kept; only the references are wired again.
            var legal = holder.GetComponent<LegalMoveMarks>();
            if (legal == null) legal = Undo.AddComponent<LegalMoveMarks>(holder.gameObject);
            Undo.RecordObject(legal, "Wire legal move marks");
            legal.recognition = recognition; legal.view = input.view; legal.material = material;
            var confirmed = holder.GetComponent<ConfirmedStoneMarks>();
            if (confirmed == null) confirmed = Undo.AddComponent<ConfirmedStoneMarks>(holder.gameObject);
            Undo.RecordObject(confirmed, "Wire confirmed stone marks");
            confirmed.recognition = recognition; confirmed.material = material;

            Undo.RecordObject(recognition, "Confirm by legal move only"); recognition.confirmByTime = false;
            Undo.RecordObject(capture, "Capture by legal move"); capture.captureByLegalMove = true;
            foreach (var item in scene.GetRootGameObjects())
                foreach (var score in item.GetComponentsInChildren<ScoreHud>(true))
                { Undo.RecordObject(score, "Count confirmed stones"); score.countConfirmedOnly = true; }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject = holder.gameObject;
            Debug.Log("Legal move rules are on in " + SceneName + ". To compare with the earlier rules, switch off Capture By Legal Move, switch on Confirm By Time and switch off Count Confirmed Only.");
        }
    }
}
