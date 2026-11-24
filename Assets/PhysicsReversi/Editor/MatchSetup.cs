using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Adds the timed match (countdown, timer, result, restart) to the flat scene, and puts the
    // flat scene first in the build list so a build opens it and a restart can reload it.
    public static class MatchSetup
    {
        const string ScenePath = "Assets/Scenes/PhysicsReversiWalkFlat.unity";

        [MenuItem("Physics Reversi/Walk/Add Match Flow (Flat)")]
        public static void Add()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "PhysicsReversiWalkFlat") { Debug.LogWarning("Open PhysicsReversiWalkFlat first."); return; }
            PutSceneFirstInBuild();
            WalkBoardRecognition recognition = null; LocalWalkInput input = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var existing = root.GetComponentInChildren<WalkMatch>(true);
                if (existing != null) { Selection.activeGameObject = existing.gameObject; Debug.Log("Match flow already exists; its layout is preserved."); return; }
                if (recognition == null) recognition = root.GetComponentInChildren<WalkBoardRecognition>(true);
                if (input == null) input = root.GetComponentInChildren<LocalWalkInput>(true);
            }
            if (recognition == null || input == null) { Debug.LogError("Board recognition or local input is missing from the scene."); return; }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Match HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(WalkMatch));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Add match flow");
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var match = canvasObject.GetComponent<WalkMatch>();
            match.recognition = recognition; match.input = input;
            // The timer sits just under the stone counts at the top centre.
            match.timerText = AddText(canvasObject.transform, "Timer", "3:00", new Vector2(.5f, 1), new Vector2(0, -78), new Vector2(160, 30), 22, font, false);
            match.bannerText = AddText(canvasObject.transform, "Banner", "", new Vector2(.5f, .5f), new Vector2(0, 40), new Vector2(900, 200), 64, font, true);
            match.hintText = AddText(canvasObject.transform, "Restart Hint", "", new Vector2(.5f, .5f), new Vector2(0, -90), new Vector2(900, 40), 22, font, true);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = canvasObject;
            Debug.Log("Match flow saved. Match length and texts are on Match HUD / Walk Match.");
        }

        static void PutSceneFirstInBuild()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static Text AddText(Transform parent, string name, string value, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, Font font, bool outlined)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, anchor.y);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = obj.GetComponent<Text>(); text.text = value; text.font = font; text.fontSize = fontSize; text.fontStyle = FontStyle.Bold;
            text.color = new Color(.95f, .96f, .98f); text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            // Large text over the board needs an edge to stay readable on both stone colours.
            if (outlined) { var outline = obj.AddComponent<Outline>(); outline.effectColor = new Color(0, 0, 0, .85f); outline.effectDistance = new Vector2(2, -2); }
            return text;
        }
    }
}
