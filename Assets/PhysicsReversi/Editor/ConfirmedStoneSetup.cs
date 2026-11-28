using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Trial (2026-11-27, SPEC.md 試遊で出た課題 2 and 3) in the toon scene: confirmed stones slide to
    // the middle of their cell and are all held equally firmly; stones on the board that are not
    // confirmed yet wear a faint overlay on their caps, and confirmed ones look plain. Only stones put
    // down by hand and near the middle of their cell are confirmed. The switches
    // stay off in the scripts; this turns them on here. Safe to run again: the overlay materials
    // are refreshed to the values below, the other tuned values kept.
    public static class ConfirmedStoneSetup
    {
        const string SceneName = "PhysicsReversiToon";
        const string Dir = "Assets/PhysicsReversi/WalkAssets/Toon/";
        // 0 plain, 1 stripes, 2 dots; and blinks per second, 0 for steady.
        const int Pattern = 1;
        const float Blink = 0;
        // How far the stones' outline is drawn toward the camera (world units).
        const float OutlinePull = .18f;

        [MenuItem("Physics Reversi/Walk/Try Confirmed Stone Snap and Loose Look (Trial)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != SceneName) { Debug.LogWarning("Open " + SceneName + " first."); return; }
            WalkBoardRecognition recognition = null;
            foreach (var root in scene.GetRootGameObjects()) if (recognition == null) recognition = root.GetComponentInChildren<WalkBoardRecognition>(true);
            if (recognition == null || recognition.stones == null || recognition.stones.Length == 0) { Debug.LogError("No board recognition with stones."); return; }

            // Yellow, the colour that already means "grabbable" (the grab outline): a loose stone on
            // the board is exactly one that can be grabbed. Stripes on the caps and a yellow outline.
            // Strong on white, where pale yellow on milk hardly shows; on black a thin yellow turns muddy.
            var black = Overlay("ToonStoneBlack", Palette.Accent, .4f);
            var white = Overlay("ToonStoneWhite", Palette.Accent, .75f);
            if (black.plain == null || white.plain == null) { Debug.LogError("The toon stone materials are missing; run Apply Toon Look first."); return; }

            Undo.RecordObject(recognition, "Try confirmed stone snap");
            recognition.snapToCenter = true; recognition.uniformHold = true;
            // 2026-11-28: confirming is stricter. Only a stone near the middle, and only one put down or
            // thrown by hand, is confirmed.
            recognition.requireCentered = true; recognition.handPlacedOnly = true;
            var look = recognition.GetComponent<LooseStoneLook>();
            if (look == null) look = Undo.AddComponent<LooseStoneLook>(recognition.gameObject);
            Undo.RecordObject(look, "Try loose stone look");
            look.recognition = recognition;
            look.plain = new[] { black.plain, white.plain };
            look.loose = new[] { black.overlay, white.overlay };

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Confirmed stones now slide to the middle and are held equally (Hold Multiplier " + recognition.holdMultiplier + "x); loose stones wear an overlay.");
        }

        static (Material plain, Material overlay) Overlay(string name, Color color, float strength)
        {
            var plain = AssetDatabase.LoadAssetAtPath<Material>(Dir + name + ".mat");
            if (plain == null) return (null, null);
            // The stones' near rim keeps its outline (see _OutlineDepthBias in ToonLit.shader); for
            // confirmed and loose stones alike, so both read the same.
            plain.SetFloat("_OutlineDepthBias", OutlinePull); EditorUtility.SetDirty(plain);
            string path = Dir + name + " Loose.mat";
            var overlay = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (overlay == null) { overlay = new Material(plain) { name = name + " Loose" }; AssetDatabase.CreateAsset(overlay, path); }
            else overlay.CopyPropertiesFromMaterial(plain);
            color.a = strength;
            overlay.SetColor("_LooseColor", color);
            overlay.SetFloat("_LoosePattern", Pattern);
            overlay.SetFloat("_LooseScale", 7);
            overlay.SetFloat("_LoosePulse", Blink);
            overlay.SetColor("_OutlineColor", Palette.Accent);
            EditorUtility.SetDirty(overlay);
            return (plain, overlay);
        }
    }
}
