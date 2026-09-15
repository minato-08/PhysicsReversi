using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    public static class ScoreHudSetup
    {
        [MenuItem("Physics Reversi/Walk/Add Score HUD")]
        public static void AddHud()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "PhysicsReversiWalk") { Debug.LogWarning("Open PhysicsReversiWalk first."); return; }
            WalkBoardRecognition recognition = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var existing = root.GetComponentInChildren<ScoreHud>(true);
                if (existing != null) { Selection.activeGameObject = existing.gameObject; Debug.Log("Score HUD already exists; its layout is preserved."); return; }
                var found = root.GetComponentInChildren<WalkBoardRecognition>();
                if (found != null) recognition = found;
            }
            if (recognition == null) { Debug.LogWarning("Add Recognition Rings first."); return; }
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Score HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(ScoreHud));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Add editable score HUD");
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("Score Panel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(canvasObject.transform, false);
            var panelRect = panel.GetComponent<RectTransform>(); panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, 1);
            panelRect.pivot = new Vector2(.5f, 1); panelRect.anchoredPosition = new Vector2(0, -18); panelRect.sizeDelta = new Vector2(380, 108);
            var background = panel.GetComponent<Image>(); background.color = new Color(.035f, .045f, .065f, .9f); background.raycastTarget = false;
            var hud = canvasObject.GetComponent<ScoreHud>(); hud.recognition = recognition;
            AddText(panel.transform, "Black Label", "BLACK", new Vector2(-85, 32), new Vector2(150, 22), 16, font);
            AddText(panel.transform, "White Label", "WHITE", new Vector2(85, 32), new Vector2(150, 22), 16, font);
            hud.blackScore = AddText(panel.transform, "Black Score", "--", new Vector2(-85, 0), new Vector2(150, 44), 36, font);
            hud.whiteScore = AddText(panel.transform, "White Score", "--", new Vector2(85, 0), new Vector2(150, 44), 36, font);
            hud.statusText = AddText(panel.transform, "Score Status", "Live score", new Vector2(0, -36), new Vector2(360, 22), 14, font);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = panel;
            Debug.Log("Score HUD saved. Edit Score Panel position and child Text styling before Play. Scores update continuously.");
        }
        static Text AddText(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, Font font)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = obj.GetComponent<Text>(); text.text = value; text.font = font; text.fontSize = fontSize;
            text.color = new Color(.95f, .96f, .98f); text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            return text;
        }
    }
}
