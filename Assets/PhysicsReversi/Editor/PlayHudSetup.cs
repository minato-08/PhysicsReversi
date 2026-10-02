using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    public static class PlayHudSetup
    {
        [MenuItem("Physics Reversi/Walk/Add Play HUD")]
        public static void AddHud()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "PhysicsReversiWalk") { Debug.LogWarning("Open PhysicsReversiWalk first."); return; }
            Transform root = null; AimHud aim = null; bool separateScore = false;
            foreach (var item in scene.GetRootGameObjects())
            {
                if (item.name == "Walk Prototype") root = item.transform;
                if (aim == null) aim = item.GetComponentInChildren<AimHud>(true);
                var score = item.GetComponentInChildren<ScoreHud>(true);
                if (score != null && score.GetComponent<AimHud>() == null) separateScore = true;
            }
            if (root == null) { Debug.LogWarning("Place Editable Scene Parts first."); return; }
            var recognition = root.GetComponentInChildren<WalkBoardRecognition>();
            var input = root.GetComponentInChildren<LocalWalkInput>();
            if (recognition == null || input == null || input.view == null || input.players == null || input.players.Length == 0)
            { Debug.LogWarning("Add Recognition Rings first (and Add Second Player on a scene without players registered)."); return; }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add play HUD");

            // A scene built for the earlier over-the-shoulder view still carries its camera script,
            // crosshair and map. Replace those once; later runs keep whatever was tuned by hand.
            if (GameObjectUtility.RemoveMonoBehavioursWithMissingScript(input.view.gameObject) > 0) WalkSceneSetup.PlaceOverviewCamera(input.view);
            GameObject canvasObject;
            if (aim == null)
            {
                canvasObject = new GameObject("Play HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(AimHud));
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create play HUD");
                var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
                var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
                aim = canvasObject.GetComponent<AimHud>();
            }
            else
            {
                canvasObject = aim.gameObject;
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(canvasObject);
                var crosshair = canvasObject.transform.Find("Crosshair");
                if (crosshair != null) Undo.DestroyObjectImmediate(crosshair.gameObject);
            }
            Undo.RecordObject(aim, "Connect play HUD");
            aim.input = input;
            var single = root.Find("Aim Highlight");
            if (single != null) Undo.DestroyObjectImmediate(single.gameObject);
            var highlights = new MeshRenderer[input.players.Length];
            for (int i = 0; i < highlights.Length; i++)
            {
                var kept = aim.highlights != null && i < aim.highlights.Length ? aim.highlights[i] : null;
                highlights[i] = kept != null && kept.sharedMaterials.Length == 3 ? kept : Highlight(root, "Grab Highlight " + (i + 1));
            }
            aim.highlights = highlights;

            // Stone counts sit at the top centre; skipped when a separate Score HUD is already in the scene.
            var panel = canvasObject.transform.Find("Score Panel") as RectTransform;
            var map = canvasObject.transform.Find("Board Map") as RectTransform;
            if (panel == null && map != null)
            {
                for (int i = map.childCount - 1; i >= 0; i--)
                    if (map.GetChild(i).GetComponent<Text>() == null) Undo.DestroyObjectImmediate(map.GetChild(i).gameObject);
                Undo.RecordObject(map.gameObject, "Map to score panel"); map.name = "Score Panel"; panel = map;
                LayOutScore(panel, canvasObject, recognition);
            }
            else if (panel == null && !separateScore)
            {
                panel = Box(canvasObject.transform, "Score Panel", Vector2.zero, Vector2.zero, new Color(.035f, .045f, .065f, .8f)).rectTransform;
                LayOutScore(panel, canvasObject, recognition);
            }
            EditorUtility.SetDirty(aim);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject = canvasObject;
            Debug.Log("Play HUD saved: one grab highlight per player and the stone counts. Edit the Play HUD layout before Play.");
        }
        static void LayOutScore(RectTransform panel, GameObject canvasObject, WalkBoardRecognition recognition)
        {
            Undo.RecordObject(panel, "Lay out score");
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 1); panel.anchoredPosition = new Vector2(0, -12); panel.sizeDelta = new Vector2(210, 58);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var score = canvasObject.GetComponent<ScoreHud>();
            if (score == null) score = Undo.AddComponent<ScoreHud>(canvasObject);
            Undo.RecordObject(score, "Lay out score");
            score.recognition = recognition;
            score.blackScore = ScoreLabel(panel, "Black Score", "--", new Vector2(-50, 9), 22, font);
            score.whiteScore = ScoreLabel(panel, "White Score", "--", new Vector2(50, 9), 22, font);
            ScoreLabel(panel, "Black Label", "BLACK", new Vector2(-50, -13), 11, font);
            ScoreLabel(panel, "White Label", "WHITE", new Vector2(50, -13), 11, font);
        }
        static Text ScoreLabel(Transform panel, string name, string value, Vector2 position, int fontSize, Font font)
        {
            var existing = panel.Find(name);
            if (existing == null) return Label(panel, name, value, position, fontSize, font);
            Undo.RecordObject(existing, "Lay out score");
            ((RectTransform)existing).anchoredPosition = position;
            return existing.GetComponent<Text>();
        }
        // One shader, three materials, drawn in queue order: mask, edge, fill (see the shader header).
        static Material[] HighlightMaterials()
        {
            const string folder = "Assets/PhysicsReversi/WalkAssets/";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(folder + "SilhouetteHighlight.shader");
            if (shader == null) { Debug.LogError("SilhouetteHighlight.shader is missing."); return null; }
            // Superseded by the three materials below.
            if (AssetDatabase.LoadAssetAtPath<Material>(folder + "AimHighlight.mat") != null) AssetDatabase.DeleteAsset(folder + "AimHighlight.mat");
            var always = (float)UnityEngine.Rendering.CompareFunction.Always;
            return new[]
            {
                HighlightMaterial(folder + "AimMask.mat", shader, 0, 3000, always, (float)UnityEngine.Rendering.StencilOp.Replace, always, 0),
                HighlightMaterial(folder + "AimEdge.mat", shader, 1, 3001, (float)UnityEngine.Rendering.CompareFunction.NotEqual,
                    (float)UnityEngine.Rendering.StencilOp.Keep, (float)UnityEngine.Rendering.CompareFunction.LessEqual, 15),
                HighlightMaterial(folder + "AimFill.mat", shader, 2, 3002, always, (float)UnityEngine.Rendering.StencilOp.Zero,
                    (float)UnityEngine.Rendering.CompareFunction.LessEqual, 15),
            };
        }
        static Material HighlightMaterial(string path, Shader shader, float mode, int queue, float comp, float pass, float depth, float colorMask)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.renderQueue = queue;
            material.SetFloat("_Mode", mode); material.SetFloat("_StencilComp", comp); material.SetFloat("_StencilPass", pass);
            material.SetFloat("_ZTest", depth); material.SetFloat("_ColorMask", colorMask);
            EditorUtility.SetDirty(material); return material;
        }
        static MeshRenderer Highlight(Transform root, string name)
        {
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(root, false);
            Undo.RegisterCreatedObjectUndo(obj, "Create grab highlight");
            var renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterials = HighlightMaterials();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.enabled = false; return renderer;
        }
        static Image Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = obj.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        static Text Label(Transform parent, string name, string value, Vector2 position, int fontSize, Font font)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = new Vector2(90, fontSize + 6);
            var text = obj.GetComponent<Text>(); text.text = value; text.font = font; text.fontSize = fontSize;
            text.color = new Color(.95f, .96f, .98f); text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            return text;
        }
    }
}
