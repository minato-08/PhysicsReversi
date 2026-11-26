using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Builds the match HUD chosen on 2026-11-27 (UI.md, proposal B) in the toon trial: the score bar
    // with the timer under it, the controls in the bottom corners, and the countdown, time-up and
    // result. Generates what it needs: rounded-box and circle sprites, and the Fredoka font asset
    // with plain and outlined materials. Running it again rebuilds the HUD from scratch, so place
    // things here rather than by hand. The old Score Panel and match texts are switched off.
    public static class MatchHudSetup
    {
        const string SceneName = "PhysicsReversiToon";
        const string UiDir = "Assets/PhysicsReversi/WalkAssets/UI/";
        const string FontDir = "Assets/PhysicsReversi/Fonts/";
        const string RootName = "Toon HUD";
        // The rounded-box sprite's corner radius in pixels; Image.pixelsPerUnitMultiplier scales it.
        const float SpriteCorner = 48;

        static Sprite roundBox, circle, triangle;
        static TMP_FontAsset font;
        static Material plain, fine, outlined, heavy;
        static Color Plum => Palette.Outline;
        static Color Milk => Palette.StoneWhite;
        static Color Ink => Palette.StoneBlack;

        [MenuItem("Physics Reversi/Walk/Build Match HUD (Trial)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != SceneName) { Debug.LogWarning("Open " + SceneName + " first."); return; }
            WalkMatch match = Find<WalkMatch>(scene); WalkBoardRecognition recognition = Find<WalkBoardRecognition>(scene);
            LocalWalkInput input = Find<LocalWalkInput>(scene);
            if (match == null || recognition == null || input == null) { Debug.LogError("The match, board recognition or local input is missing."); return; }

            MakeAssets();
            RetireOldHud(scene, match);
            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(MatchHud));
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Build match HUD");
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var hud = root.GetComponent<MatchHud>();
            hud.match = match; hud.recognition = recognition; hud.input = input; hud.view = input.view;
            hud.stacker = AddStacker(scene, match, recognition, input.view);

            BuildScoreBar(root.transform, hud);
            BuildControls(root.transform, true);
            BuildControls(root.transform, false);
            BuildCountdown(root.transform, hud);
            BuildTimeUp(root.transform, hud);
            BuildResult(root.transform, hud);

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("Match HUD built under " + RootName + ".");
        }

        // ---------- The parts ----------

        static void BuildScoreBar(Transform parent, MatchHud hud)
        {
            var group = Node(parent, "Score Bar", new Vector2(.5f, 1), new Vector2(0, -34), Vector2.zero);
            // The bar: a plum frame, the milk track inside it, and the ink fill clipped by the track.
            Box(group, "Bar", new Vector2(560, 30), Vector2.zero, Milk, 3.5f, 15, out var track);
            track.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var fill = Node(track, "Black Share", Vector2.zero, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(.5f, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = Ink;
            hud.barFill = fill;
            // The even split: a milk mark edged in plum, so it reads on either colour, standing out past the bar.
            Box(group, "Even Mark", new Vector2(8, 46), Vector2.zero, Milk, 2.5f, 3, out _);

            foreach (int side in new[] { -1, 1 })
            {
                bool black = side < 0; string who = black ? "Black" : "White";
                Chip(group, who + " Chip", new Vector2(side * 309, 0), 30, black ? Ink : Milk);
                var count = Text(group, who + " Count", "0", 38, Milk, outlined, black ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
                var rect = count.rectTransform; rect.pivot = new Vector2(black ? 1 : 0, .5f);
                rect.anchoredPosition = new Vector2(side * 332, 0); rect.sizeDelta = new Vector2(110, 48);
                if (black) hud.blackCount = count; else hud.whiteCount = count;
            }
            var delta = Text(group, "Gain (template)", "+1", 26, Palette.Accent, outlined, TextAlignmentOptions.Right);
            delta.rectTransform.sizeDelta = new Vector2(80, 34);
            hud.deltaTemplate = delta;

            // The timer hangs under the middle of the bar, so the bar itself is never covered.
            var timer = Node(group, "Timer", new Vector2(.5f, .5f), new Vector2(0, -23), new Vector2(84, 36));
            timer.pivot = new Vector2(.5f, 1); timer.anchoredPosition = new Vector2(0, -23);
            Box(timer, "Frame", new Vector2(84, 36), Vector2.zero, Milk, 3.5f, 10, out var back);
            var time = Text(back, "Time", "3:00", 26, Plum, plain, TextAlignmentOptions.Center);
            Stretch(time.rectTransform, 0, 1);
            hud.timer = timer; hud.timerBack = back.GetComponent<Image>(); hud.timerText = time;
        }

        // The controls stay on screen for the whole match (UI.md), mirrored about the middle of the
        // screen: keys on the outer edge, words inside. No panel, no player colour: white letters edged in plum.
        static void BuildControls(Transform parent, bool black)
        {
            var group = Node(parent, black ? "Controls Black" : "Controls White", new Vector2(black ? 0 : 1, 0), new Vector2(black ? 26 : -26, 16), Vector2.zero);
            group.pivot = new Vector2(black ? 0 : 1, 0);
            var column = group.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 5; column.childAlignment = black ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
            column.childControlWidth = column.childControlHeight = true; column.childForceExpandWidth = column.childForceExpandHeight = false;
            Fit(group.gameObject);

            string[] move = black ? new[] { "W", "A", "S", "D" } : new[] { "I", "J", "K", "L" };
            string grab = black ? "F" : "H";
            var first = Row(group, "Move", black);
            var second = Row(group, "Grab", black);
            if (black)
            {
                foreach (var k in move) Key(first, k);
                Word(first, "/"); Word(first, "Stick  Move");
                Key(second, grab); Word(second, "/"); Pad(second); Word(second, "Grab · Tap to place · Hold to throw");
            }
            else
            {
                Word(first, "Move  Stick"); Word(first, "/");
                foreach (var k in move) Key(first, k);
                Word(second, "Grab · Tap to place · Hold to throw"); Pad(second); Word(second, "/"); Key(second, grab);
            }
        }

        static void BuildCountdown(Transform parent, MatchHud hud)
        {
            var group = Node(parent, "Countdown", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            var number = Text(group, "Number", "3", 150, Milk, heavy, TextAlignmentOptions.Center);
            number.rectTransform.anchoredPosition = new Vector2(0, 29); number.rectTransform.sizeDelta = new Vector2(600, 200);
            var intro = Node(group, "Intro", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            Label(intro, "Rule", "Most stones of your color in 3 minutes wins", 22, Milk, Plum, new Vector2(0, -79), new Vector2(.5f, .5f));
            hud.pins = new RectTransform[2];
            for (int i = 0; i < 2; i++)
            {
                bool black = i == 0;
                var pin = Node(intro, black ? "Pin Black" : "Pin White", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
                pin.pivot = new Vector2(.5f, 0);
                var arrow = Node(pin, "Arrow", new Vector2(.5f, 0), Vector2.zero, new Vector2(20, 13));
                arrow.pivot = new Vector2(.5f, 0);
                var arrowImage = arrow.gameObject.AddComponent<Image>(); arrowImage.sprite = triangle; arrowImage.color = Plum; arrowImage.raycastTarget = false;
                Label(pin, "Name", black ? "BLACK" : "WHITE", 20, black ? Ink : Milk, black ? Milk : Plum, new Vector2(0, 12), new Vector2(.5f, 0));
                hud.pins[i] = pin;
            }
            hud.countdown = group.gameObject; hud.countdownNumber = number; hud.countdownIntro = intro.gameObject;
            group.gameObject.SetActive(false);
        }

        static void BuildTimeUp(Transform parent, MatchHud hud)
        {
            var group = Node(parent, "Time Up", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            var big = Text(group, "Title", "TIME UP!", 110, Palette.Accent, heavy, TextAlignmentOptions.Center);
            big.rectTransform.anchoredPosition = new Vector2(0, 29); big.rectTransform.sizeDelta = new Vector2(900, 160);
            var sub = Text(group, "Counting", "Counting stones…", 24, Milk, fine, TextAlignmentOptions.Center);
            sub.rectTransform.anchoredPosition = new Vector2(0, -79); sub.rectTransform.sizeDelta = new Vector2(600, 40);
            hud.timeUp = group.gameObject; group.gameObject.SetActive(false);
        }

        // For now: the result floats over the board, with no card and no dimming. The stones are to
        // be stacked and counted later (UI.md), and the numbers will then sit on the stacks.
        static void BuildResult(Transform parent, MatchHud hud)
        {
            var group = Node(parent, "Result", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            Stretch(group, 0, 0);
            // Used only when there is no stacker: the result as text floating over the board.
            var floating = Node(group, "Floating", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            var title = Text(floating, "Title", "BLACK WINS", 64, Milk, outlined, TextAlignmentOptions.Center);
            title.rectTransform.anchoredPosition = new Vector2(0, 70); title.rectTransform.sizeDelta = new Vector2(900, 100);
            var score = Node(floating, "Score", new Vector2(.5f, .5f), new Vector2(0, -20), Vector2.zero);
            var row = score.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 18; row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true; row.childForceExpandWidth = row.childForceExpandHeight = false;
            Fit(score.gameObject);
            Sized(Chip(score, "Black Chip", Vector2.zero, 40, Ink), 40, 40);
            var black = Text(score, "Black", "0", 72, Milk, heavy, TextAlignmentOptions.Center);
            Text(score, "Dash", "–", 44, Milk, outlined, TextAlignmentOptions.Center);
            var white = Text(score, "White", "0", 72, Milk, heavy, TextAlignmentOptions.Center);
            Sized(Chip(score, "White Chip", Vector2.zero, 40, Milk), 40, 40);

            var hint = Node(group, "Play Again", new Vector2(.5f, 0), Vector2.zero, Vector2.zero);
            hint.pivot = new Vector2(.5f, 0); hint.anchoredPosition = new Vector2(0, 24);
            var line = hint.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 6; line.childAlignment = TextAnchor.MiddleCenter;
            line.childControlWidth = line.childControlHeight = true; line.childForceExpandWidth = line.childForceExpandHeight = false;
            Fit(hint.gameObject);
            Word(hint, "Press"); Key(hint, "F"); Key(hint, "H"); Word(hint, "or"); Pad(hint); Word(hint, "to play again");

            // On the stacks: each count stands over its stack's top, with WIN! (or DRAW) above it.
            foreach (int owner in new[] { 1, 2 })
            {
                bool isBlack = owner == 1;
                var label = Node(group, isBlack ? "Stack Black" : "Stack White", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
                label.pivot = new Vector2(.5f, 0);
                var count = Text(label, "Count", "0", 56, Milk, heavy, TextAlignmentOptions.Bottom);
                count.rectTransform.anchorMin = count.rectTransform.anchorMax = new Vector2(.5f, 0); count.rectTransform.pivot = new Vector2(.5f, 0);
                count.rectTransform.anchoredPosition = Vector2.zero; count.rectTransform.sizeDelta = new Vector2(160, 64);
                var win = Text(label, "Win", "WIN!", 40, Palette.Accent, outlined, TextAlignmentOptions.Bottom);
                win.rectTransform.anchorMin = win.rectTransform.anchorMax = new Vector2(.5f, 0); win.rectTransform.pivot = new Vector2(.5f, 0);
                win.rectTransform.anchoredPosition = new Vector2(0, 62); win.rectTransform.sizeDelta = new Vector2(200, 48);
                win.gameObject.SetActive(false);
                if (isBlack) { hud.stackLabelBlack = label; hud.stackCountBlack = count; hud.winBlack = win; }
                else { hud.stackLabelWhite = label; hud.stackCountWhite = count; hud.winWhite = win; }
            }

            hud.floatingResult = floating.gameObject;
            hud.result = group.gameObject; hud.resultTitle = title; hud.resultBlack = black; hud.resultWhite = white; hud.restartHint = hint.gameObject;
            group.gameObject.SetActive(false);
        }

        // The result count that stacks the stones (ResultStacker) sits with the match. Its tuned values
        // are kept on a rerun; only the references are filled in.
        static ResultStacker AddStacker(Scene scene, WalkMatch match, WalkBoardRecognition recognition, Camera view)
        {
            var stacker = match.GetComponent<ResultStacker>();
            if (stacker == null) stacker = Undo.AddComponent<ResultStacker>(match.gameObject);
            Undo.RecordObject(stacker, "Stack the result"); Undo.RecordObject(match, "Stack the result");
            stacker.match = match; stacker.recognition = recognition; stacker.captures = Find<WalkCaptureController>(scene); stacker.view = view;
            match.stacker = stacker;
            return stacker;
        }

        // The old HUD stays in the scene, switched off: the Score Panel and the match's own texts.
        static void RetireOldHud(Scene scene, WalkMatch match)
        {
            Undo.RecordObject(match, "Retire old match texts");
            foreach (var text in new[] { match.timerText, match.bannerText, match.hintText })
                if (text != null) { Undo.RecordObject(text.gameObject, "Retire old match texts"); text.gameObject.SetActive(false); }
            match.timerText = match.bannerText = match.hintText = null;
            var score = Find<ScoreHud>(scene);
            if (score != null)
            {
                Undo.RecordObject(score, "Retire score panel"); score.enabled = false;
                var panel = score.transform.Find("Score Panel");
                if (panel != null) { Undo.RecordObject(panel.gameObject, "Retire score panel"); panel.gameObject.SetActive(false); }
            }
        }

        // ---------- Building blocks ----------

        static RectTransform Node(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        static void Stretch(RectTransform rect, float inset, float bottomExtra)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset + bottomExtra); rect.offsetMax = new Vector2(-inset, -inset);
        }

        static Image Rounded(RectTransform rect, Color color, float radius)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = roundBox; image.type = Image.Type.Sliced; image.color = color; image.raycastTarget = false;
            image.pixelsPerUnitMultiplier = SpriteCorner / Mathf.Max(.5f, radius);
            return image;
        }

        // The rounded back of a box that a layout sizes to its contents. It sits on a child kept out of
        // the layout: on the box itself, the sliced image would claim its corners' size (96) as the
        // box's preferred size and stretch it.
        static void Backdrop(RectTransform box, Color color, float radius)
        {
            var back = Node(box, "Back", Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(back, 0, 0);
            back.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Rounded(back, color, radius);
        }

        // A plum frame with a coloured inside; inner is the inside, to put things in.
        static RectTransform Box(Transform parent, string name, Vector2 size, Vector2 position, Color fill, float edge, float radius, out RectTransform inner)
        {
            var frame = Node(parent, name, new Vector2(.5f, .5f), position, size);
            Rounded(frame, Plum, radius);
            inner = Node(frame, "Inside", Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(inner, edge, 0);
            Rounded(inner, fill, Mathf.Max(.5f, radius - edge));
            return frame;
        }

        static RectTransform Chip(Transform parent, string name, Vector2 position, float size, Color color)
        {
            var frame = Node(parent, name, new Vector2(.5f, .5f), position, new Vector2(size, size));
            var ring = frame.gameObject.AddComponent<Image>(); ring.sprite = circle; ring.color = Plum; ring.raycastTarget = false;
            var face = Node(frame, "Face", Vector2.zero, Vector2.zero, Vector2.zero); Stretch(face, 3, 0);
            var image = face.gameObject.AddComponent<Image>(); image.sprite = circle; image.color = color; image.raycastTarget = false;
            return frame;
        }

        static void Sized(RectTransform rect, float width, float height)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>(); element.preferredWidth = width; element.preferredHeight = height;
        }

        // A box sized to its text: a plum frame around a coloured inside, both following the text.
        static RectTransform Label(Transform parent, string name, string value, int size, Color fill, Color ink, Vector2 position, Vector2 pivot)
        {
            var frame = Node(parent, name, new Vector2(.5f, pivot.y), position, Vector2.zero);
            frame.pivot = pivot;
            Backdrop(frame, Plum, 12);
            var outer = frame.gameObject.AddComponent<HorizontalLayoutGroup>();
            outer.padding = new RectOffset(3, 3, 3, 3); outer.childControlWidth = outer.childControlHeight = true; outer.childForceExpandWidth = outer.childForceExpandHeight = false;
            Fit(frame.gameObject);
            var inside = Node(frame, "Inside", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            Backdrop(inside, fill, 9);
            var inner = inside.gameObject.AddComponent<HorizontalLayoutGroup>();
            inner.padding = new RectOffset(16, 16, 3, 5); inner.childControlWidth = inner.childControlHeight = true; inner.childForceExpandWidth = inner.childForceExpandHeight = false;
            Text(inside, "Text", value, size, ink, plain, TextAlignmentOptions.Center);
            return frame;
        }

        static RectTransform Row(Transform parent, string name, bool black)
        {
            var row = Node(parent, name, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4; layout.childAlignment = black ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return row;
        }

        // A key cap: a plum frame, thicker at the bottom, around a milk face with the letter.
        static void Key(Transform parent, string letter)
        {
            var key = Node(parent, "Key " + letter, new Vector2(.5f, .5f), Vector2.zero, new Vector2(26, 28));
            Sized(key, 26, 28);
            Rounded(key, Plum, 6);
            var face = Node(key, "Face", Vector2.zero, Vector2.zero, Vector2.zero);
            face.anchorMin = Vector2.zero; face.anchorMax = Vector2.one; face.offsetMin = new Vector2(2.5f, 4.5f); face.offsetMax = new Vector2(-2.5f, -2.5f);
            Rounded(face, Milk, 4);
            var text = Text(face, "Letter", letter, 15, Plum, plain, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 0, 0);
        }

        // The gamepad's south button.
        static void Pad(Transform parent)
        {
            var pad = Chip(parent, "Pad A", Vector2.zero, 26, Milk);
            Sized(pad, 26, 26);
            var text = Text(pad, "Letter", "A", 14, Plum, plain, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 0, 0);
        }

        static void Word(Transform parent, string value)
        {
            var text = Text(parent, value, value, 15, Milk, fine, TextAlignmentOptions.Center);
            text.margin = new Vector4(3, 0, 3, 0);
        }

        static TMP_Text Text(Transform parent, string name, string value, float size, Color color, Material material, TextAlignmentOptions alignment)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.font = font; text.fontSharedMaterial = material; text.text = value; text.fontSize = size; text.color = color;
            text.alignment = alignment; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false;
            return text;
        }

        static void Fit(GameObject obj)
        {
            var fitter = obj.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects()) { var found = root.GetComponentInChildren<T>(true); if (found != null) return found; }
            return null;
        }

        // ---------- Assets ----------

        static void MakeAssets()
        {
            Directory.CreateDirectory(UiDir);
            roundBox = MakeSprite("RoundBox", 128, (x, y) => RoundBox(x - 64, y - 64, 64, SpriteCorner), SpriteCorner);
            circle = MakeSprite("Circle", 128, (x, y) => new Vector2(x - 64, y - 64).magnitude <= 63.5f, 0);
            // Pointing down: the tip at the bottom middle.
            triangle = MakeSprite("Triangle", 64, (x, y) => y >= 2 && Mathf.Abs(x - 32) <= (y - 2) * 30f / 60f, 0);
            MakeFont();
        }

        static bool RoundBox(float x, float y, float half, float radius)
        {
            float qx = Mathf.Abs(x) - (half - radius), qy = Mathf.Abs(y) - (half - radius);
            return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) <= radius;
        }

        // A white shape, antialiased by 4x4 supersampling, imported as a sprite (sliced when border > 0).
        static Sprite MakeSprite(string name, int size, Func<float, float, bool> inside, float border)
        {
            string path = UiDir + name + ".png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 4; sy++) for (int sx = 0; sx < 4; sx++) if (inside(x + (sx + .5f) / 4, y + (sy + .5f) / 4)) hits++;
                    texture.SetPixel(x, y, new Color(1, 1, 1, hits / 16f));
                }
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
                importer.spritePixelsPerUnit = 100; importer.spriteBorder = new Vector4(border, border, border, border);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // The Fredoka font asset with a wide distance field, so that outlines several pixels thick
        // stay clean at every size, and three materials: plain, outlined, and heavily outlined for big titles.
        static void MakeFont()
        {
            string path = FontDir + "Fredoka SDF.asset";
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "Fredoka-SemiBold.ttf");
                font = TMP_FontAsset.CreateFontAsset(source, 64, 16, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                font.name = "Fredoka SDF";
                AssetDatabase.CreateAsset(font, path);
                font.atlasTextures[0].name = "Fredoka SDF Atlas"; AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
                font.material.name = "Fredoka SDF Material"; AssetDatabase.AddObjectToAsset(font.material, font);
                // Every character the HUD writes, so nothing has to be drawn into the atlas while playing.
                string ascii = ""; for (char c = ' '; c <= '~'; c++) ascii += c;
                font.TryAddCharacters(ascii + "…–·", out string missing);
                if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("Fredoka lacks: " + missing);
                EditorUtility.SetDirty(font); AssetDatabase.SaveAssets();
            }
            // An outline comes out about 0.47 x width x font size pixels wide (measured 2026-11-27 at
            // 1280x720), so one width per band of sizes gives the mock-ups' strokes: about 2-3 px on
            // small words, 3 px on the counts, 6-7 px on the big titles.
            plain = font.material;
            fine = Preset("Fredoka Fine Outline", .28f);       // 15-24 pt: the controls, short notes
            outlined = Preset("Fredoka Outlined", .18f);       // 26-64 pt: the counts, gains, result title
            heavy = Preset("Fredoka Heavy Outline", .11f);     // 72-150 pt: the countdown, TIME UP, result numbers
        }

        static Material Preset(string name, float width)
        {
            string path = FontDir + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(font.material) { name = name }; AssetDatabase.CreateAsset(material, path); }
            // The font's own material uses the mobile shader, which draws no outline unless told to;
            // the full one always does.
            material.shader = Shader.Find("TextMeshPro/Distance Field");
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Plum);
            // The outline straddles the letter's edge; swelling the face by half of it keeps the letters as bold.
            material.SetFloat(ShaderUtilities.ID_FaceDilate, width * .5f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
