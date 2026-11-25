using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Dresses the space around the board as a desk in a child's room (ART.md "まわりの飾り"):
    // an apron and legs under the existing table top, a wooden floor and a rug below, and three
    // toys on the free corners of the top. Decor only: nothing here has a collider. Running it
    // again rebuilds the "Toy Room" group from scratch.
    public static class ToyRoomSetup
    {
        const string SceneName = "PhysicsReversiToon";
        const string GroupName = "Toy Room";

        [MenuItem("Physics Reversi/Walk/Build Toy Room (Trial)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != SceneName) { Debug.LogWarning("Open " + SceneName + " first."); return; }
            var shader = Shader.Find(ToonLookSetup.ShaderName);
            if (shader == null) { Debug.LogError("Shader " + ToonLookSetup.ShaderName + " is missing."); return; }
            var top = GameObject.Find("Walkable surrounding floor");
            if (top == null) { Debug.LogError("The table top (Walkable surrounding floor) was renamed."); return; }

            foreach (var root in scene.GetRootGameObjects()) if (root.name == GroupName) Undo.DestroyObjectImmediate(root);
            var group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "Build toy room");

            var edge = ToonLookSetup.Toon(shader, "ToonTableEdge", Palette.TableEdge, 0f);
            var floor = ToonLookSetup.Toon(shader, "ToonRoomFloor", Palette.RoomFloor, 0f);
            var rug = ToonLookSetup.Toon(shader, "ToonRug", Palette.Rug, 0f);
            var coral = ToonLookSetup.Toon(shader, "ToonPlayerBlack", Palette.PlayerBlack, 2.5f);
            var sky = ToonLookSetup.Toon(shader, "ToonPlayerWhite", Palette.PlayerWhite, 2.5f);
            var accent = ToonLookSetup.Toon(shader, "ToonAccent", Palette.Accent, 2f);
            var mint = ToonLookSetup.Toon(shader, "ToonBlockMint", Palette.BoardDark, 2f);
            var milk = ToonLookSetup.Toon(shader, "ToonStoneWhite", Palette.StoneWhite, 2f);
            var ink = ToonLookSetup.Toon(shader, "ToonInk", Palette.Outline, 0f);

            // Everything is placed against the table top's own bounds (44 x 56 when written).
            var b = top.GetComponent<Renderer>().bounds;
            float topY = b.max.y, underY = b.min.y;
            float hx = b.extents.x, hz = b.extents.z;
            const float roomDrop = 12;
            float roomY = topY - roomDrop;

            // Table: an apron just under the top, and four legs down to the room floor.
            Part("Apron", PrimitiveType.Cube, group.transform, new Vector3(b.center.x, underY - .6f, b.center.z), new Vector3(hx * 2 - 1, 1.2f, hz * 2 - 1), edge);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    float legTop = underY - 1.2f, height = legTop - roomY;
                    Part("Leg", PrimitiveType.Cylinder, group.transform, new Vector3(b.center.x + sx * (hx - 2.5f), roomY + height / 2, b.center.z + sz * (hz - 2.5f)), new Vector3(2.2f, height / 2, 2.2f), edge);
                }
            // Room: a wide wooden floor and a rug a little larger than the table.
            Part("Room Floor", PrimitiveType.Cube, group.transform, new Vector3(b.center.x - 10, roomY - .5f, b.center.z), new Vector3(170, 1, 190), floor, false);
            Part("Rug", PrimitiveType.Cube, group.transform, new Vector3(b.center.x, roomY + .03f, b.center.z), new Vector3(hx * 2 + 22, .06f, hz * 2 + 26), rug, false);

            // Toys on the free corners of the top, beyond the board and the reserves.
            float far = b.center.x - hx + 3.2f, near = b.center.x + hx - 3.2f;
            float left = b.center.z - hz + 3.2f, right = b.center.z + hz - 3.2f;
            var blocks = Empty("Blocks", group.transform, new Vector3(far, topY, left), 18);
            Part("Block", PrimitiveType.Cube, blocks, new Vector3(0, 1.1f, 0), Vector3.one * 2.2f, coral);
            Part("Block", PrimitiveType.Cube, blocks, new Vector3(.15f, 3.3f, -.1f), Vector3.one * 2.2f, accent, true, 14);
            Part("Block", PrimitiveType.Cube, blocks, new Vector3(-.1f, 5.5f, .1f), Vector3.one * 2.2f, mint, true, -9);
            var ball = Empty("Ball", group.transform, new Vector3(far + .5f, topY, right), 0);
            Part("Ball", PrimitiveType.Sphere, ball, new Vector3(0, 1.3f, 0), Vector3.one * 2.6f, sky);
            var die = Empty("Die", group.transform, new Vector3(near, topY, right), 27);
            Part("Cube", PrimitiveType.Cube, die, new Vector3(0, .9f, 0), Vector3.one * 1.8f, milk);
            // Five pips on the face that looks up.
            foreach (var p in new[] { new Vector2(0, 0), new Vector2(-.5f, -.5f), new Vector2(.5f, .5f), new Vector2(-.5f, .5f), new Vector2(.5f, -.5f) })
                Part("Pip", PrimitiveType.Sphere, die, new Vector3(p.x, 1.8f, p.y), new Vector3(.32f, .08f, .32f), ink, false);

            // A notebook and two crayons on the near left corner of the top.
            var notes = Empty("Notebook", group.transform, new Vector3(near, topY, left + .3f), -14);
            Part("Cover", PrimitiveType.Cube, notes, new Vector3(0, .15f, 0), new Vector3(3.4f, .3f, 4.4f), sky);
            Part("Pages", PrimitiveType.Cube, notes, new Vector3(.08f, .2f, .06f), new Vector3(3.2f, .26f, 4.3f), milk, false);
            Part("Crayon", PrimitiveType.Cylinder, group.transform, new Vector3(near - 2.6f, topY + .25f, left + 4.2f), new Vector3(.5f, 1.6f, .5f), coral, true, 0, new Vector3(0, 35, 90));
            Part("Crayon", PrimitiveType.Cylinder, group.transform, new Vector3(near - 1.2f, topY + .25f, left + 5.4f), new Vector3(.5f, 1.6f, .5f), mint, true, 0, new Vector3(0, 70, 90));

            // The far wall, with a window the sun comes through (the key light shines from this side).
            // The wall faces away from the sun; extra ambient keeps it a soft cream instead of a dark band.
            var wallMat = ToonLookSetup.Toon(shader, "ToonWall", Palette.Wall, 0f); wallMat.SetFloat("_ReceiveAmbient", .9f);
            var pane = ToonLookSetup.Toon(shader, "ToonWindow", Palette.SkyTop, 0f); pane.SetFloat("_ReceiveAmbient", 1f);
            float wallX = b.center.x - hx - 16;
            Part("Wall", PrimitiveType.Cube, group.transform, new Vector3(wallX, roomY + 20, b.center.z), new Vector3(1, 40, 220), wallMat, false);
            Part("Baseboard", PrimitiveType.Cube, group.transform, new Vector3(wallX + .7f, roomY + .9f, b.center.z), new Vector3(.6f, 1.8f, 220), edge, false);
            var window = Empty("Window", group.transform, new Vector3(wallX + .6f, roomY + 9, b.center.z), 0);
            Part("Pane", PrimitiveType.Cube, window, new Vector3(0, 9, 0), new Vector3(.2f, 18, 30), pane, false);
            Part("Sill", PrimitiveType.Cube, window, new Vector3(.6f, -.4f, 0), new Vector3(1.6f, .8f, 33), milk, false);
            Part("Frame", PrimitiveType.Cube, window, new Vector3(.2f, 9, 0), new Vector3(.5f, 18, .8f), milk, false);
            Part("Frame", PrimitiveType.Cube, window, new Vector3(.2f, 9, -15), new Vector3(.5f, 18, .8f), milk, false);
            Part("Frame", PrimitiveType.Cube, window, new Vector3(.2f, 9, 15), new Vector3(.5f, 18, .8f), milk, false);

            // Toys left on the floor around the table.
            var box = Empty("Toy Box", group.transform, new Vector3(b.center.x - hx - 7, roomY, b.center.z - hz - 12), 12);
            Part("Box", PrimitiveType.Cube, box, new Vector3(0, 3, 0), new Vector3(9, 6, 7), coral);
            Part("Trim", PrimitiveType.Cube, box, new Vector3(0, 6.1f, 0), new Vector3(9.4f, .5f, 7.4f), accent);
            var books = Empty("Books", group.transform, new Vector3(b.center.x - hx - 6, roomY, b.center.z + hz + 11), -20);
            Part("Book", PrimitiveType.Cube, books, new Vector3(0, .6f, 0), new Vector3(5, 1.2f, 7), mint);
            Part("Book", PrimitiveType.Cube, books, new Vector3(.2f, 1.8f, -.1f), new Vector3(4.6f, 1.2f, 6.4f), sky, true, 9);
            Part("Book", PrimitiveType.Cube, books, new Vector3(-.1f, 2.9f, .2f), new Vector3(4.2f, 1, 6), coral, true, -6);
            Part("Block", PrimitiveType.Cube, group.transform, new Vector3(b.center.x + 2, roomY + 1.1f, b.center.z - hz - 9), Vector3.one * 2.2f, accent, true, 33);
            Part("Block", PrimitiveType.Cube, group.transform, new Vector3(b.center.x + 8, roomY + 1.1f, b.center.z + hz + 8), Vector3.one * 2.2f, mint, true, -21);
            Part("Ball", PrimitiveType.Sphere, group.transform, new Vector3(b.center.x - 4, roomY + 1.5f, b.center.z + hz + 14), Vector3.one * 3, accent);

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Toy room built under " + GroupName + ".");
        }

        static Transform Empty(string name, Transform parent, Vector3 position, float yaw)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return obj.transform;
        }

        static Transform Part(string name, PrimitiveType shape, Transform parent, Vector3 position, Vector3 scale, Material material, bool castShadow = true, float yaw = 0, Vector3? euler = null)
        {
            var obj = GameObject.CreatePrimitive(shape);
            Object.DestroyImmediate(obj.GetComponent<Collider>()); // decor never touches the game
            obj.name = name; obj.isStatic = true;
            obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.transform.localRotation = euler.HasValue ? Quaternion.Euler(euler.Value) : Quaternion.Euler(0, yaw, 0);
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return obj.transform;
        }
    }
}
