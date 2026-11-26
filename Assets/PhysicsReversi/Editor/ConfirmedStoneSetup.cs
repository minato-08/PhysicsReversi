using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Trial (2026-11-27, SPEC.md 試遊で出た課題 2 and 3) in the toon scene: confirmed stones slide to
    // the middle of their cell, are all held equally firmly, and wear a thin ring on their caps.
    // The switches stay off in the scripts; this turns them on here. Safe to run again: the ring
    // materials are refreshed, the other tuned values kept.
    public static class ConfirmedStoneSetup
    {
        const string SceneName = "PhysicsReversiToon";
        const string Dir = "Assets/PhysicsReversi/WalkAssets/Toon/";

        [MenuItem("Physics Reversi/Walk/Try Confirmed Stone Snap and Ring (Trial)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != SceneName) { Debug.LogWarning("Open " + SceneName + " first."); return; }
            WalkBoardRecognition recognition = null;
            foreach (var root in scene.GetRootGameObjects()) if (recognition == null) recognition = root.GetComponentInChildren<WalkBoardRecognition>(true);
            if (recognition == null || recognition.stones == null || recognition.stones.Length == 0) { Debug.LogError("No board recognition with stones."); return; }

            // The ring on a black cap is milk, on a white cap the outline plum; both kept light.
            var black = Ring("ToonStoneBlack", Palette.StoneWhite, .6f);
            var white = Ring("ToonStoneWhite", Palette.Outline, .65f);
            if (black.loose == null || white.loose == null) { Debug.LogError("The toon stone materials are missing; run Apply Toon Look first."); return; }

            Undo.RecordObject(recognition, "Try confirmed stone snap");
            recognition.snapToCenter = true; recognition.uniformHold = true;
            var look = recognition.GetComponent<ConfirmedLook>();
            if (look == null) look = Undo.AddComponent<ConfirmedLook>(recognition.gameObject);
            Undo.RecordObject(look, "Try confirmed stone ring");
            look.recognition = recognition;
            look.loose = new[] { black.loose, white.loose };
            look.confirmed = new[] { black.ring, white.ring };

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Confirmed stones now slide to the middle, are held equally (Hold Multiplier " + recognition.holdMultiplier + "x), and wear a ring.");
        }

        static (Material loose, Material ring) Ring(string name, Color color, float strength)
        {
            var loose = AssetDatabase.LoadAssetAtPath<Material>(Dir + name + ".mat");
            if (loose == null) return (null, null);
            string path = Dir + name + " Confirmed.mat";
            var ring = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (ring == null) { ring = new Material(loose) { name = name + " Confirmed" }; AssetDatabase.CreateAsset(ring, path); }
            else ring.CopyPropertiesFromMaterial(loose);
            color.a = strength;
            ring.SetColor("_MarkColor", color);
            ring.SetFloat("_MarkRadius", .36f);
            ring.SetFloat("_MarkWidth", .05f);
            EditorUtility.SetDirty(ring);
            return (loose, ring);
        }
    }
}
