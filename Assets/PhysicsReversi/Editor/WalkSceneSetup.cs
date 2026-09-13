using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    public static class WalkSceneSetup
    {
        const string Folder = "Assets/PhysicsReversi/WalkAssets";
        [MenuItem("Physics Reversi/Walk/Place Editable Scene Parts")]
        public static void PlaceParts()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "PhysicsReversiWalk") { Debug.LogWarning("Open Assets/Scenes/PhysicsReversiWalk first."); return; }
            foreach (var existing in scene.GetRootGameObjects())
                if (existing.name == "Walk Prototype") { Debug.LogWarning("Parts already exist. Edit them in Hierarchy; no duplicates were added."); return; }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/PhysicsReversi", "WalkAssets");
            var teal = MaterialAsset("BoardTeal", new Color(.07f, .25f, .24f));
            var tealLight = MaterialAsset("BoardLight", new Color(.11f, .32f, .29f));
            var ground = MaterialAsset("Ground", new Color(.17f, .2f, .24f));
            var black = MaterialAsset("BlackStone", new Color(.055f, .06f, .075f));
            var white = MaterialAsset("WhiteStone", new Color(.92f, .9f, .8f));
            var orange = MaterialAsset("Player", new Color(1, .48f, .14f));
            var contact = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Folder + "/Contact.physicMaterial");
            if (contact == null)
            {
                contact = new PhysicsMaterial("Contact") { staticFriction = .35f, dynamicFriction = .25f, bounciness = .12f };
                AssetDatabase.CreateAsset(contact, Folder + "/Contact.physicMaterial");
            }
            var bowl = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Bowl.asset");
            if (bowl == null) { bowl = MakeBowl(); AssetDatabase.CreateAsset(bowl, Folder + "/Bowl.asset"); }
            var stonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Stone.prefab");
            if (stonePrefab == null)
            {
                var template = GameObject.CreatePrimitive(PrimitiveType.Cylinder); template.name = "Stone";
                Object.DestroyImmediate(template.GetComponent<Collider>());
                template.transform.localScale = new Vector3(2.6f, .18f, 2.6f);
                var shape = template.AddComponent<MeshCollider>(); shape.sharedMesh = template.GetComponent<MeshFilter>().sharedMesh;
                shape.convex = true; shape.sharedMaterial = contact;
                var body = template.AddComponent<Rigidbody>(); body.mass = 2; body.linearDamping = .2f; body.angularDamping = .8f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.interpolation = RigidbodyInterpolation.Interpolate;
                body.solverIterations = 12; body.solverVelocityIterations = 8;
                template.AddComponent<CarryStone>();
                stonePrefab = PrefabUtility.SaveAsPrefabAsset(template, Folder + "/Stone.prefab"); Object.DestroyImmediate(template);
            }
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Place walk prototype");
            var root = new GameObject("Walk Prototype"); Undo.RegisterCreatedObjectUndo(root, "Place walk prototype");
            var stage = Parent("Stage", root.transform);
            Cube("Walkable surrounding floor", stage, new Vector3(0, -.75f, 0), new Vector3(44, .5f, 56), ground, contact);
            var board = Parent("Board - 8 x 8 recessed cells", root.transform);
            for (int z = 0; z < 8; z++) for (int x = 0; x < 8; x++)
            {
                var cell = new GameObject("Cell " + x + "," + z); cell.transform.SetParent(board);
                cell.transform.localPosition = new Vector3((x - 3.5f) * 4, 0, (z - 3.5f) * 4);
                cell.AddComponent<MeshFilter>().sharedMesh = bowl;
                cell.AddComponent<MeshRenderer>().sharedMaterial = (x + z) % 2 == 0 ? teal : tealLight;
                var collider = cell.AddComponent<MeshCollider>(); collider.sharedMesh = bowl; collider.sharedMaterial = contact;
            }
            var stones = Parent("Stones - select to change ownership and status", root.transform);
            for (int owner = 1; owner <= 2; owner++)
            {
                var rack = Parent(owner == 1 ? "Black reserve" : "White reserve", stones);
                for (int i = 0; i < 30; i++)
                    PlaceStone(stonePrefab, rack, "Reserve " + (i + 1), owner, StoneStatus.Reserve,
                        new Vector3((i % 10 - 4.5f) * 2.9f, -.3f, (18.5f + i / 10 * 3) * (owner == 1 ? -1 : 1)), owner == 1 ? black : white);
            }
            var initial = Parent("Initial four", stones);
            PlaceStone(stonePrefab, initial, "White A", 2, StoneStatus.OnBoard, new Vector3(-2, .2f, -2), white);
            PlaceStone(stonePrefab, initial, "White B", 2, StoneStatus.OnBoard, new Vector3(2, .2f, 2), white);
            PlaceStone(stonePrefab, initial, "Black A", 1, StoneStatus.OnBoard, new Vector3(-2, .2f, 2), black);
            PlaceStone(stonePrefab, initial, "Black B", 1, StoneStatus.OnBoard, new Vector3(2, .2f, -2), black);
            var systems = Parent("Rules and local input", root.transform);
            var authority = systems.gameObject.AddComponent<CarryAuthority>();
            var actor = new GameObject("Player 1 - Black"); actor.transform.SetParent(root.transform); actor.transform.position = new Vector3(0, .1f, -17);
            var controller = actor.AddComponent<CharacterController>(); controller.height = 1.8f; controller.radius = .38f;
            controller.center = new Vector3(0, .9f, 0); controller.stepOffset = .6f; controller.slopeLimit = 60;
            var player = actor.AddComponent<WalkPlayer>(); player.playerId = 1;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule); visual.name = "Appearance"; visual.transform.SetParent(actor.transform, false);
            visual.transform.localPosition = Vector3.up * .9f; visual.transform.localScale = new Vector3(.75f, .9f, .75f);
            Object.DestroyImmediate(visual.GetComponent<Collider>()); visual.GetComponent<Renderer>().sharedMaterial = orange;
            player.carryPoint = Parent("Carry point - adjust hand position", actor.transform); player.carryPoint.localPosition = new Vector3(0, 1.6f, 2.1f);
            Camera camera = null;
            foreach (var item in scene.GetRootGameObjects()) { camera = item.GetComponentInChildren<Camera>(); if (camera != null) break; }
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera"); Undo.RegisterCreatedObjectUndo(cameraObject, "Create camera");
                camera = cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<AudioListener>(); camera.tag = "MainCamera";
            }
            Undo.RecordObject(camera.transform, "Position camera");
            var orbit = Undo.AddComponent<OrbitCamera>(camera.gameObject); orbit.target = actor.transform;
            camera.transform.SetPositionAndRotation(actor.transform.position + new Vector3(0, 5, -7), Quaternion.Euler(28, 0, 0));
            var input = systems.gameObject.AddComponent<LocalWalkInput>(); input.player = player; input.orbit = orbit; input.view = camera; input.authority = authority;
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject = actor;
            Debug.Log("Walk scene parts placed and saved. WASD: move. Mouse: camera. Aim at a nearby BLACK reserve with screen center and left click: carry; click again: release. Esc: free cursor. All parts are editable before Play.");
        }
        static Transform Parent(string name, Transform parent)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false); return obj.transform;
        }
        static void Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, PhysicsMaterial contact)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.transform.SetParent(parent);
            obj.transform.position = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material; obj.GetComponent<Collider>().sharedMaterial = contact;
        }
        static void PlaceStone(GameObject prefab, Transform parent, string name, int owner, StoneStatus status, Vector3 position, Material material)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); obj.name = name; obj.transform.position = position;
            var stone = obj.GetComponent<CarryStone>(); stone.ownerId = owner; stone.reserveOwnerId = owner; stone.status = status;
            if (!stone.twoSided) obj.GetComponent<Renderer>().sharedMaterial = material;
            else if (owner == 2) obj.transform.rotation *= Quaternion.Euler(180, 0, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(stone);
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj.GetComponent<Renderer>());
        }
        static Material MaterialAsset(string name, Color color)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static Mesh MakeBowl()
        {
            const int steps = 20;
            var vertices = new Vector3[(steps + 1) * (steps + 1)]; var triangles = new List<int>();
            for (int z = 0; z <= steps; z++) for (int x = 0; x <= steps; x++)
            {
                float px = x * 4f / steps - 2, pz = z * 4f / steps - 2;
                float radius = Mathf.Sqrt(px * px + pz * pz);
                float y = -.45f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.7f, 1.9f, radius)));
                vertices[z * (steps + 1) + x] = new Vector3(px, y, pz);
                if (x < steps && z < steps)
                {
                    int a = z * (steps + 1) + x, b = a + steps + 1;
                    triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            }
            var mesh = new Mesh { name = "Recessed cell 4m", vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
