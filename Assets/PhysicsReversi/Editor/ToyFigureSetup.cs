using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Replaces each player's capsule with a toy figure built from primitives (ART.md): a big
    // cream head with dot eyes, a knit cap in the player's colour with a yellow pompom, a round
    // body, hands and feet. Only the look changes: the CharacterController stays as it is, and the
    // old capsule is switched off, not deleted. Running it again rebuilds the figures.
    public static class ToyFigureSetup
    {
        const string SceneName = "PhysicsReversiToon";
        // The figure is drawn larger than the controller so it reads from the game camera next to
        // 2.6-wide stones (compared 1.0, 1.5 and 1.8 in Play on 2026-11-25). Collisions are unchanged.
        const float FigureScale = 1.8f;

        [MenuItem("Physics Reversi/Walk/Build Toy Figures (Trial)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != SceneName) { Debug.LogWarning("Open " + SceneName + " first."); return; }
            var shader = Shader.Find(ToonLookSetup.ShaderName);
            if (shader == null) { Debug.LogError("Shader " + ToonLookSetup.ShaderName + " is missing."); return; }
            var face = ToonLookSetup.Toon(shader, "ToonFace", Palette.Face, 2.5f);
            // Faces turn toward the camera, away from the sun behind the board; more ambient keeps them cream, not grey.
            face.SetFloat("_ReceiveAmbient", .6f);
            var ink = ToonLookSetup.Toon(shader, "ToonInk", Palette.Outline, 0f);
            var accent = ToonLookSetup.Toon(shader, "ToonAccent", Palette.Accent, 2f);
            int built = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var player in root.GetComponentsInChildren<WalkPlayer>(true))
                {
                    var colour = ToonLookSetup.Toon(shader, player.playerId == 2 ? "ToonPlayerWhite" : "ToonPlayerBlack", Palette.Player(player.playerId), 2.5f);
                    var old = player.transform.Find("Figure");
                    if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                    var capsule = player.transform.Find("Appearance");
                    if (capsule != null) { Undo.RecordObject(capsule.gameObject, "Build toy figure"); capsule.gameObject.SetActive(false); }
                    BuildFigure(player, colour, face, ink, accent);
                    built++;
                }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Toy figures built for " + built + " players.");
        }

        // Sizes are in metres against the 1.8 m controller; the head may be wide because stones
        // are only 0.36 thick, but everything below the knees stays near the 0.25 collider radius.
        static void BuildFigure(WalkPlayer player, Material colour, Material face, Material ink, Material accent)
        {
            var figure = new GameObject("Figure");
            Undo.RegisterCreatedObjectUndo(figure, "Build toy figure");
            figure.transform.SetParent(player.transform, false);
            figure.transform.localScale = Vector3.one * FigureScale;

            var body = Empty("Body", figure.transform, new Vector3(0, .3f, 0));
            Part("Torso", PrimitiveType.Sphere, body, new Vector3(0, .45f, 0), new Vector3(.75f, .9f, .7f), colour);
            var head = Empty("Head", body, new Vector3(0, 1.25f, 0));
            Part("Face", PrimitiveType.Sphere, head, Vector3.zero, Vector3.one, face);
            Part("Cap", PrimitiveType.Sphere, head, new Vector3(0, .2f, -.02f), new Vector3(1.04f, .72f, 1.04f), colour);
            Part("Pompom", PrimitiveType.Sphere, head, new Vector3(0, .62f, -.04f), Vector3.one * .28f, accent);
            Part("Left Eye", PrimitiveType.Sphere, head, new Vector3(-.17f, -.02f, .46f), new Vector3(.1f, .15f, .06f), ink, false);
            Part("Right Eye", PrimitiveType.Sphere, head, new Vector3(.17f, -.02f, .46f), new Vector3(.1f, .15f, .06f), ink, false);
            var leftHand = Part("Left Hand", PrimitiveType.Sphere, body, new Vector3(-.46f, .38f, .05f), Vector3.one * .26f, face);
            var rightHand = Part("Right Hand", PrimitiveType.Sphere, body, new Vector3(.46f, .38f, .05f), Vector3.one * .26f, face);
            var leftFoot = Part("Left Foot", PrimitiveType.Sphere, figure.transform, new Vector3(-.16f, .09f, .03f), new Vector3(.26f, .18f, .38f), ink);
            var rightFoot = Part("Right Foot", PrimitiveType.Sphere, figure.transform, new Vector3(.16f, .09f, .03f), new Vector3(.26f, .18f, .38f), ink);

            var motion = Undo.AddComponent<ToyFigureMotion>(figure);
            motion.player = player; motion.body = body; motion.head = head;
            motion.leftFoot = leftFoot; motion.rightFoot = rightFoot;
            motion.leftHand = leftHand; motion.rightHand = rightHand;
        }

        static Transform Empty(string name, Transform parent, Vector3 position)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
            return obj.transform;
        }

        static Transform Part(string name, PrimitiveType shape, Transform parent, Vector3 position, Vector3 scale, Material material, bool castShadow = true)
        {
            var obj = GameObject.CreatePrimitive(shape);
            Object.DestroyImmediate(obj.GetComponent<Collider>()); // looks only; the controller does the colliding
            obj.name = name;
            obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = scale;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return obj.transform;
        }
    }
}
