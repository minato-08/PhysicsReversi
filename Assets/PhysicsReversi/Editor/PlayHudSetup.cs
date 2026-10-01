using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    public static class PlayHudSetup
    {
        const float PixelsPerUnit = 4.5f;
        [MenuItem("Physics Reversi/Walk/Add Aim and Board HUD")]
        public static void AddHud()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "PhysicsReversiWalk") { Debug.LogWarning("Open PhysicsReversiWalk first."); return; }
            Transform root = null;
            foreach (var item in scene.GetRootGameObjects())
            {
                if (item.name == "Walk Prototype") root = item.transform;
            }
            if (root == null) { Debug.LogWarning("Place Editable Scene Parts first."); return; }
            foreach (var item in scene.GetRootGameObjects())
            {
                var existing = item.GetComponentInChildren<AimHud>(true);
                if (existing == null) continue;
                // Earlier versions drew floating rings or painted bands; replace only that part.
                if (existing.highlight == null || existing.highlight.sharedMaterials.Length != 3)
                {
                    Undo.RecordObject(existing, "Upgrade aim highlight");
                    existing.highlight = Highlight(root);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
                    Debug.Log("Aim highlight upgraded to the silhouette outline. HUD layout and camera settings are preserved.");
                }
                else Debug.Log("Aim and Board HUD already exists; its layout and the camera settings are preserved.");
                return;
            }
            var recognition = root.GetComponentInChildren<WalkBoardRecognition>();
            var input = root.GetComponentInChildren<LocalWalkInput>();
            if (recognition == null || input == null || input.player == null || input.orbit == null)
            { Debug.LogWarning("Add Recognition Rings first."); return; }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add aim and board HUD");

            // Shoulder camera: applied once, together with the crosshair it is tuned for.
            Undo.RecordObject(input.orbit, "Shoulder camera");
            input.orbit.distance = 10; input.orbit.height = 2.6f; input.orbit.pitch = 38; input.orbit.shoulderOffset = 1.4f;

            var canvasObject = new GameObject("Play HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(AimHud), typeof(BoardMapHud));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create play HUD");
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;

            var aim = canvasObject.GetComponent<AimHud>(); aim.input = input;
            aim.highlight = Highlight(root);
            aim.crosshair = Box(canvasObject.transform, "Crosshair", Vector2.zero, new Vector2(6, 6), Color.white);

            // Panel covers the whole walkable floor, so the marker stays visible at the reserves.
            float cell = recognition.cellWidth * PixelsPerUnit;
            var panel = Box(canvasObject.transform, "Board Map", Vector2.zero, new Vector2(48 * PixelsPerUnit, 60 * PixelsPerUnit), new Color(.035f, .045f, .065f, .8f)).rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1); panel.anchoredPosition = new Vector2(-16, -16);
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var map = canvasObject.GetComponent<BoardMapHud>();
            map.recognition = recognition; map.player = input.player.transform; map.view = input.orbit.transform;
            map.pixelsPerUnit = PixelsPerUnit; map.stones = new Image[64];
            for (int z = 0; z < 8; z++) for (int x = 0; x < 8; x++)
            {
                var position = new Vector2((x - 3.5f) * cell, (z - 3.5f) * cell);
                var square = Box(panel, "Cell " + x + "," + z, position, new Vector2(cell - 1, cell - 1),
                    (x + z) % 2 == 0 ? new Color(.07f, .25f, .24f) : new Color(.11f, .32f, .29f));
                var stone = Box(square.transform, "Stone", Vector2.zero, new Vector2(cell - 5, cell - 5), Color.white);
                stone.sprite = knob; stone.enabled = false; map.stones[z * 8 + x] = stone;
            }
            var marker = Box(panel, "Player marker (points where the camera looks)", Vector2.zero, new Vector2(9, 9), new Color(1, .48f, .14f));
            Box(marker.transform, "Nose", new Vector2(0, 8), new Vector2(3, 8), new Color(1, .48f, .14f));
            map.marker = marker.rectTransform;

            // Stone counts reuse ScoreHud; skipped when a separate Score HUD is already in the scene.
            bool hasScore = false;
            foreach (var item in scene.GetRootGameObjects()) if (item != canvasObject && item.GetComponentInChildren<ScoreHud>(true) != null) hasScore = true;
            if (!hasScore)
            {
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var score = Undo.AddComponent<ScoreHud>(canvasObject); score.recognition = recognition;
                float top = 30 * PixelsPerUnit - 16;
                score.blackScore = Label(panel, "Black Score", "--", new Vector2(-50, top), 22, font);
                score.whiteScore = Label(panel, "White Score", "--", new Vector2(50, top), 22, font);
                Label(panel, "Black Label", "BLACK", new Vector2(-50, top - 20), 11, font);
                Label(panel, "White Label", "WHITE", new Vector2(50, top - 20), 11, font);
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject = canvasObject;
            Debug.Log("Aim and Board HUD saved. A stone's edge lights up only when a click would grab it. Mouse wheel zooms. Edit Play HUD layout before Play.");
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
        static MeshRenderer Highlight(Transform root)
        {
            var old = root.Find("Aim Highlight");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var obj = new GameObject("Aim Highlight", typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(root, false);
            Undo.RegisterCreatedObjectUndo(obj, "Create aim highlight");
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
