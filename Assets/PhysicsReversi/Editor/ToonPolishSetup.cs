using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Second pass on the toon trial (ART.md "立体感と空気感"): rounder stones with a glossy glint
    // and a flat shadow under each, a low frame that makes the board read as a toy board, and
    // dust drifting in the sunlight. Looks only: nothing here has a collider, and the stones keep
    // their cylinder collider. Running it again rebuilds what it made.
    public static class ToonPolishSetup
    {
        const string SceneName = "PhysicsReversiToon";
        const string Folder = "Assets/PhysicsReversi/WalkAssets/Toon";
        const string FrameName = "Board Frame";
        const string DustName = "Sunlit Dust";
        const string ShadowsName = "Stone Shadows";

        // World units. Must stay below half the stone's thickness (0.18).
        const float StoneBevel = .13f;
        const int StoneBevelSteps = 5;
        // The frame is low enough that a stone sliding off the board passes over it without looking stuck.
        const float FrameWidth = .9f, FrameHeight = .16f;

        [MenuItem("Physics Reversi/Walk/Polish Toon Look (Trial)")]
        public static void Polish()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != SceneName) { Debug.LogWarning("Open " + SceneName + " first."); return; }
            var shader = Shader.Find(ToonLookSetup.ShaderName);
            if (shader == null) { Debug.LogError("Shader " + ToonLookSetup.ShaderName + " is missing."); return; }
            Transform root = null;
            foreach (var obj in scene.GetRootGameObjects()) if (obj.name == "Walk Prototype") root = obj.transform;
            if (root == null) { Debug.LogError("No Walk Prototype in the scene."); return; }

            int stones = RoundStones(root);
            Glints();
            BuildFrame(scene, root, shader);
            BuildDust(scene);
            BuildShadows(scene);

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Toon polish applied: " + stones + " stones use the rounder mesh, board frame and dust rebuilt.");
        }

        // The toon scene gets its own copy of the two-sided mesh with a fat rounded edge, so the
        // flat scene keeps the shared one. The outline seen from above is the same circle.
        static int RoundStones(Transform root)
        {
            string path = Folder + "/ToonStone.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = "Toon stone (black top - white bottom)" }; AssetDatabase.CreateAsset(mesh, path); }
            FaceStoneSetup.Fill(mesh, StoneBevel, StoneBevelSteps);
            EditorUtility.SetDirty(mesh);

            int count = 0;
            foreach (var stone in root.GetComponentsInChildren<CarryStone>(true))
            {
                var filter = stone.GetComponent<MeshFilter>();
                if (filter == null) continue;
                var renderer = stone.GetComponent<MeshRenderer>();
                Undo.RecordObject(filter, "Round toon stones"); Undo.RecordObject(renderer, "Round toon stones");
                filter.sharedMesh = mesh; count++;
                // Too thin for the shadow map; StoneShadows draws their shadow instead.
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                if (PrefabUtility.IsPartOfPrefabInstance(filter))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
            }
            return count;
        }

        // Glossy plastic: figures and toys catch a small hard highlight on their curves. Stones
        // get none: at the game camera's distance their rim is a few pixels wide and the glint
        // broke into a dashed one-pixel line that made the edge look grainy (2026-11-25).
        static void Glints()
        {
            foreach (var name in new[] { "ToonStoneBlack", "ToonStoneWhite", "ToonPlayerBlack", "ToonPlayerWhite", "ToonAccent", "ToonBlockMint" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + name + ".mat");
                if (material == null) continue;
                bool stone = name.StartsWith("ToonStone");
                material.SetColor("_HighlightColor", new Color(1, 1, 1, stone ? 0 : .9f));
                material.SetFloat("_HighlightSize", .03f);
                EditorUtility.SetDirty(material);
            }
        }

        // Four low bars around the 8 x 8 cells, with an outline, so the board stops looking printed.
        static void BuildFrame(UnityEngine.SceneManagement.Scene scene, Transform root, Shader shader)
        {
            var board = root.GetComponentInChildren<WalkBoardRecognition>(true);
            Transform cells = null;
            foreach (Transform child in root) if (child.name.StartsWith("Board")) cells = child;
            if (board == null || cells == null) { Debug.LogWarning("Board not found; frame skipped."); return; }
            var bounds = new Bounds(cells.position, Vector3.zero);
            foreach (var renderer in cells.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);

            foreach (var obj in scene.GetRootGameObjects()) if (obj.name == FrameName) Undo.DestroyObjectImmediate(obj);
            var frame = new GameObject(FrameName);
            Undo.RegisterCreatedObjectUndo(frame, "Build board frame");
            var material = ToonLookSetup.Toon(shader, "ToonBoardFrame", Palette.BoardFrame, 2f);

            // Bars sit on the table top, which is just under the cells.
            float baseY = bounds.min.y - .01f, y = baseY + FrameHeight / 2;
            float hx = bounds.extents.x, hz = bounds.extents.z, w = FrameWidth;
            Bar(frame.transform, new Vector3(bounds.center.x - hx - w / 2, y, bounds.center.z), new Vector3(w, FrameHeight, hz * 2 + w * 2), material);
            Bar(frame.transform, new Vector3(bounds.center.x + hx + w / 2, y, bounds.center.z), new Vector3(w, FrameHeight, hz * 2 + w * 2), material);
            Bar(frame.transform, new Vector3(bounds.center.x, y, bounds.center.z - hz - w / 2), new Vector3(hx * 2, FrameHeight, w), material);
            Bar(frame.transform, new Vector3(bounds.center.x, y, bounds.center.z + hz + w / 2), new Vector3(hx * 2, FrameHeight, w), material);
        }

        static void Bar(Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(obj.GetComponent<Collider>()); // never touches stones or players
            obj.name = "Bar"; obj.isStatic = true;
            obj.transform.SetParent(parent, false); obj.transform.position = position; obj.transform.localScale = scale;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        static void BuildShadows(UnityEngine.SceneManagement.Scene scene)
        {
            var shader = Shader.Find("PhysicsReversi/GroundShadow");
            if (shader == null) { Debug.LogError("Shader PhysicsReversi/GroundShadow is missing."); return; }
            string path = Folder + "/GroundShadow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader) { name = "GroundShadow" }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.SetColor("_ShadowTint", Palette.Shadow);
            EditorUtility.SetDirty(material);

            foreach (var obj in scene.GetRootGameObjects()) if (obj.name == ShadowsName) Undo.DestroyObjectImmediate(obj);
            var group = new GameObject(ShadowsName, typeof(StoneShadows));
            Undo.RegisterCreatedObjectUndo(group, "Build stone shadows");
            var shadows = group.GetComponent<StoneShadows>();
            shadows.material = material;
            var top = GameObject.Find("Walkable surrounding floor");
            if (top != null)
            {
                var b = top.GetComponent<Renderer>().bounds;
                shadows.groundHeight = b.max.y;
                shadows.tableArea = new Rect(b.min.x, b.min.z, b.size.x, b.size.z);
            }
        }

        // A few specks floating over the table, drifting slowly and fading in and out, as if lit
        // by the window. Square like every other particle (ART.md), and kept faint.
        static void BuildDust(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var obj in scene.GetRootGameObjects()) if (obj.name == DustName) Undo.DestroyObjectImmediate(obj);
            var top = GameObject.Find("Walkable surrounding floor");
            var b = top != null ? top.GetComponent<Renderer>().bounds : new Bounds(Vector3.zero, new Vector3(44, 1, 56));

            var dust = new GameObject(DustName);
            Undo.RegisterCreatedObjectUndo(dust, "Build dust");
            dust.transform.position = new Vector3(b.center.x, b.max.y + 5, b.center.z);
            var system = dust.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true; main.prewarm = true; main.playOnAwake = true;
            main.duration = 10; main.maxParticles = 90;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9, 14);
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(.12f, .26f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = Palette.Sun;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = system.emission; emission.rateOverTime = 7;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(b.size.x, 8, b.size.z);

            var velocity = system.velocityOverLifetime;
            velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-.05f, .05f);
            velocity.y = new ParticleSystem.MinMaxCurve(.03f, .12f);
            velocity.z = new ParticleSystem.MinMaxCurve(-.05f, .05f);
            var noise = system.noise;
            noise.enabled = true; noise.strength = .35f; noise.frequency = .15f; noise.scrollSpeed = .1f;
            var rotation = system.rotationOverLifetime;
            rotation.enabled = true; rotation.z = new ParticleSystem.MinMaxCurve(-.6f, .6f);

            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.55f, .3f), new GradientAlphaKey(.55f, .7f), new GradientAlphaKey(0, 1) });
            var color = system.colorOverLifetime; color.enabled = true; color.color = fade;

            var renderer = dust.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/FeelParticle.mat");
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            system.Play();
        }
    }
}
