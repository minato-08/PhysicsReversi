using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Gives the trial scene the toon look in ART.md: toon materials from the palette, a warm key
    // light, a gradient ambient, a plain sky colour and one global post-processing volume.
    // Safe to run again: it reuses the assets it made and re-applies the palette to them.
    public static class ToonLookSetup
    {
        const string SceneName = "PhysicsReversiToon";
        const string Folder = "Assets/PhysicsReversi/WalkAssets/Toon";
        public const string ShaderName = "PhysicsReversi/ToonLit";

        [MenuItem("Physics Reversi/Walk/Apply Toon Look (Trial)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != SceneName) { Debug.LogWarning("Open " + SceneName + " first."); return; }
            var shader = Shader.Find(ShaderName);
            if (shader == null) { Debug.LogError("Shader " + ShaderName + " is missing or does not compile."); return; }

            // Old material name -> toon material. Outline widths are in screen pixels; flat
            // surfaces get none, because a hull outline on a plane only draws its rim.
            var swap = new Dictionary<string, Material>
            {
                { "BlackStone", Toon(shader, "ToonStoneBlack", Palette.StoneBlack, 2f) },
                { "WhiteStone", Toon(shader, "ToonStoneWhite", Palette.StoneWhite, 2f) },
                { "BoardLight", Toon(shader, "ToonBoardLight", Palette.BoardLight, 0f) },
                { "BoardTeal", Toon(shader, "ToonBoardDark", Palette.BoardDark, 0f) },
                { "Ground", Toon(shader, "ToonTable", Palette.Table, 0f) },
                { "Player", Toon(shader, "ToonPlayerBlack", Palette.PlayerBlack, 2.5f) },
                { "Player2", Toon(shader, "ToonPlayerWhite", Palette.PlayerWhite, 2.5f) },
            };
            int swapped = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials; bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                        if (materials[i] != null && swap.TryGetValue(materials[i].name, out var toon)) { materials[i] = toon; changed = true; }
                    if (!changed) continue;
                    Undo.RecordObject(renderer, "Apply toon look");
                    renderer.sharedMaterials = materials; swapped++;
                    // Stones are prefab instances; the swap stays in this scene, the prefab is untouched.
                    if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }

            // A warm sun from the far side of the board, so shadows fall toward the camera and
            // peek out from under the thin stones (ART.md "光").
            foreach (var light in Object.FindObjectsByType<Light>())
            {
                if (light.type != LightType.Directional) continue;
                Undo.RecordObject(light, "Apply toon look"); Undo.RecordObject(light.transform, "Apply toon look");
                light.color = Palette.Sun; light.intensity = 1.2f; light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(40, 60, 0);
            }
            // Shadow sides take a cool sky above and a warm floor below instead of flat grey.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.SkyTop;
            RenderSettings.ambientEquatorColor = Palette.SkyHorizon;
            RenderSettings.ambientGroundColor = Palette.GroundAmbient;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            foreach (var camera in Object.FindObjectsByType<Camera>())
            {
                Undo.RecordObject(camera, "Apply toon look");
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Palette.SkyHorizon;
                var data = camera.GetUniversalAdditionalCameraData();
                Undo.RecordObject(data, "Apply toon look"); data.renderPostProcessing = true;
            }
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null)
            {
                // Tight enough for the 0.36-thick stones to keep a shadow; measured in Play on 2026-11-25.
                Undo.RecordObject(pipeline, "Apply toon look");
                pipeline.shadowDistance = 75; pipeline.shadowCascadeCount = 2;
                pipeline.shadowDepthBias = .2f; pipeline.shadowNormalBias = .2f;
                pipeline.msaaSampleCount = 4; // the hull outlines alias badly without it
                EditorUtility.SetDirty(pipeline);
            }

            AddVolume(scene);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Toon look applied: " + swapped + " renderers now use " + ShaderName + ".");
        }

        // Creates or refreshes one toon material under WalkAssets/Toon. Shared with the other toon menus.
        public static Material Toon(Shader shader, string name, Color color, float outline)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/PhysicsReversi/WalkAssets", "Toon");
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetColor("_ShadowTint", Palette.Shadow);
            material.SetColor("_OutlineColor", Palette.Outline);
            material.SetFloat("_OutlineWidth", outline);
            material.SetFloat("_ReceiveAmbient", .25f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Starting values from the craft floor's bright-toy row: neutral tonemapping keeps the flat
        // palette colours true, a little bloom for the accent, a light vignette to frame the board.
        static void AddVolume(UnityEngine.SceneManagement.Scene scene)
        {
            string path = Folder + "/ToonVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            var tone = Get<Tonemapping>(profile); tone.mode.Override(TonemappingMode.Neutral);
            var bloom = Get<Bloom>(profile); bloom.intensity.Override(.3f); bloom.threshold.Override(1.1f);
            var vignette = Get<Vignette>(profile); vignette.intensity.Override(.2f);
            var adjust = Get<ColorAdjustments>(profile); adjust.saturation.Override(10f); adjust.contrast.Override(5f);
            EditorUtility.SetDirty(profile);

            Volume volume = null;
            foreach (var root in scene.GetRootGameObjects()) if (volume == null) volume = root.GetComponentInChildren<Volume>(true);
            if (volume == null)
            {
                var obj = new GameObject("Toon Volume", typeof(Volume));
                Undo.RegisterCreatedObjectUndo(obj, "Apply toon look");
                volume = obj.GetComponent<Volume>();
            }
            Undo.RecordObject(volume, "Apply toon look");
            volume.isGlobal = true; volume.sharedProfile = profile;
        }

        static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                component.name = typeof(T).Name; component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            return component;
        }
    }
}
