using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    public static class WalkRecognitionSetup
    {
        [MenuItem("Physics Reversi/Walk/Add Recognition Rings")]
        public static void AddRings()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "PhysicsReversiWalk") { Debug.LogWarning("Open PhysicsReversiWalk first."); return; }
            Transform root = null;
            foreach (var item in scene.GetRootGameObjects()) if (item.name == "Walk Prototype") root = item.transform;
            if (root == null) { Debug.LogWarning("Place Editable Scene Parts first."); return; }
            Transform board = root.Find("Board - 8 x 8 recessed cells");
            Transform systems = root.Find("Rules and local input");
            if (board == null || systems == null) { Debug.LogError("Board or Rules and local input was renamed. Restore the group name before setup."); return; }
            var cellObjects = new Transform[64];
            for (int z = 0; z < 8; z++) for (int x = 0; x < 8; x++)
            {
                var cell = board.Find("Cell " + x + "," + z);
                if (cell == null || cell.GetComponent<Collider>() == null)
                { Debug.LogError("Missing cell or collider: " + x + "," + z + ". No scene changes made."); return; }
                cellObjects[z * 8 + x] = cell;
            }
            const string path = "Assets/PhysicsReversi/WalkAssets/RecognitionRing.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(.2f, .95f, .85f) };
                AssetDatabase.CreateAsset(material, path);
            }
            const string framePath = "Assets/PhysicsReversi/WalkAssets/RecognitionFrame.asset";
            var frame = AssetDatabase.LoadAssetAtPath<Mesh>(framePath);
            if (frame == null) { frame = new Mesh { name = "Recognition corners" }; AssetDatabase.CreateAsset(frame, framePath); }
            // Rebuilt in place on every run, so existing cells pick up a changed shape.
            BuildCorners(frame, 1.72f, .55f, .07f); EditorUtility.SetDirty(frame);
            // Quiet by design: pale and translucent, so it reads as a mark on the board rather than a UI element.
            material.color = new Color(.82f, .95f, .9f, .5f);
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_ZWrite", 0);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; EditorUtility.SetDirty(material);
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add editable recognition marks");
            var cells = new RecognitionCell[64];
            for (int i = 0; i < 64; i++)
            {
                var cell = cellObjects[i].GetComponent<RecognitionCell>();
                if (cell == null) cell = Undo.AddComponent<RecognitionCell>(cellObjects[i].gameObject);
                Undo.RecordObject(cell, "Wire cell"); cell.cellIndex = i; cells[i] = cell;
                // Earlier versions used a thin floating ring; replace it with the flat frame.
                var old = cell.transform.Find("Recognition Ring");
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                if (cell.marker != null) continue;
                var obj = new GameObject("Recognition Mark", typeof(MeshFilter), typeof(MeshRenderer)); Undo.RegisterCreatedObjectUndo(obj, "Create mark");
                obj.transform.SetParent(cell.transform, false); obj.transform.localPosition = new Vector3(0, .02f, 0);
                obj.GetComponent<MeshFilter>().sharedMesh = frame;
                var mark = obj.GetComponent<MeshRenderer>(); mark.sharedMaterial = material;
                mark.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mark.receiveShadows = false;
                mark.enabled = false; cell.marker = mark;
            }
            var recognition = systems.GetComponent<WalkBoardRecognition>();
            if (recognition == null) recognition = Undo.AddComponent<WalkBoardRecognition>(systems.gameObject);
            Undo.RecordObject(recognition, "Wire recognition"); recognition.boardOrigin = board; recognition.cells = cells;
            recognition.stones = root.GetComponentsInChildren<CarryStone>(true);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject = systems.gameObject;
            Debug.Log("Recognition marks saved. Press Play: initial four should light after settling. Move stones to check recognition; select a stone for details in Inspector.");
        }
        // Four L-shaped corner marks set in from the cell border, so neighbouring cells
        // never draw side-by-side lines. 'reach' is the outer half-size. Double-sided.
        static void BuildCorners(Mesh mesh, float reach, float arm, float width)
        {
            var vertices = new System.Collections.Generic.List<Vector3>(); var triangles = new System.Collections.Generic.List<int>();
            for (int corner = 0; corner < 4; corner++)
            {
                float x = corner == 0 || corner == 3 ? -1 : 1, z = corner < 2 ? -1 : 1;
                // The two arms meet without overlapping, which would show as a darker square when translucent.
                Quad(vertices, triangles, x * (reach - arm), x * reach, z * (reach - width), z * reach);
                Quad(vertices, triangles, x * (reach - width), x * reach, z * (reach - arm), z * (reach - width));
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
        static void Quad(System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<int> triangles, float x0, float x1, float z0, float z1)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(x0, 0, z0)); vertices.Add(new Vector3(x1, 0, z0));
            vertices.Add(new Vector3(x1, 0, z1)); vertices.Add(new Vector3(x0, 0, z1));
            int[] order = { 0, 1, 2, 0, 2, 3, 2, 1, 0, 3, 2, 0 };
            foreach (int index in order) triangles.Add(start + index);
        }
    }
}
