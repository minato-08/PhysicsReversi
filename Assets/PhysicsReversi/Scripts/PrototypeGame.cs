using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicsReversi
{
    public sealed class PrototypeGame : MonoBehaviour
    {
        public TuningConfig settings;
        TuningConfig tune;
        readonly List<StoneComponent> stones = new List<StoneComponent>();
        readonly List<Material> materials = new List<Material>();
        readonly int[] reserves = new int[3];
        BoardRules.Snapshot board = new BoardRules.Snapshot();
        Material black, white, ringMaterial;
        PhysicsMaterial physicsMaterial;
        Mesh stoneMesh;
        Camera view;
        LineRenderer trajectory;
        Transform launcher;
        int player = 1, nextId, captureCount, turn;
        float railX, angle, power = .35f;
        bool aiming, dragging, debug, practice = true, finished;
        Vector2 dragStart;
        float dragAngle, dragPower;
        string phase = "Ready", lastResult = "Aim for the green target to practice a capture.";
        float previousFixedDelta;
        static readonly Color Cyan = new Color(.22f, .9f, .92f);
        float Side => player == 1 ? 1 : -1;
        Vector3 LaunchPosition => new Vector3(railX, tune.launchHeight, -4.65f * Side);
        Vector3 LaunchVelocity => new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0, Mathf.Cos(angle * Mathf.Deg2Rad) * Side)
            * Mathf.Lerp(tune.minSpeed, tune.maxSpeed, power) + Vector3.up * tune.launchLift;

        void Start()
        {
            tune = settings != null ? Instantiate(settings) : ScriptableObject.CreateInstance<TuningConfig>();
            previousFixedDelta = Time.fixedDeltaTime;
            Time.fixedDeltaTime = 1f / 120f;
            black = MakeMaterial(new Color(.075f, .09f, .13f));
            white = MakeMaterial(new Color(.95f, .9f, .77f));
            ringMaterial = MakeMaterial(Cyan, true);
            physicsMaterial = new PhysicsMaterial("Reversi contact") { frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Minimum };
            UpdatePhysicsMaterial();
            stoneMesh = BuildCylinder();
            BuildTable();
            ResetBoard(true);
        }
        Material MakeMaterial(Color color, bool unlit = false)
        {
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader); m.color = color;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .28f);
            materials.Add(m); return m;
        }
        void UpdatePhysicsMaterial()
        {
            physicsMaterial.staticFriction = tune.friction;
            physicsMaterial.dynamicFriction = tune.friction;
            physicsMaterial.bounciness = tune.bounce;
        }
        void Box(string label, Vector3 position, Vector3 scale, Material material, bool collider = true)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = label; obj.transform.SetParent(transform);
            obj.transform.position = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (collider) obj.GetComponent<Collider>().sharedMaterial = physicsMaterial;
            else { obj.GetComponent<Collider>().enabled = false; Destroy(obj.GetComponent<Collider>()); }
        }
        void BuildTable()
        {
            var dark = MakeMaterial(new Color(.06f, .14f, .17f));
            var light = MakeMaterial(new Color(.09f, .21f, .23f));
            var edge = MakeMaterial(new Color(.22f, .3f, .34f));
            // One continuous collision surface prevents seams from catching thin stones.
            Box("Board surface", new Vector3(0, -.13f, 0), new Vector3(8, .26f, 8), dark);
            for (int z = 0; z < 8; z++) for (int x = 0; x < 8; x++)
                Box("Cell " + x + "," + z, new Vector3(x - 3.5f, .002f, z - 3.5f), new Vector3(.97f, .002f, .97f), (x + z) % 2 == 0 ? dark : light, false);
            Box("West rim", new Vector3(-4.08f, tune.rimHeight * .5f, 0), new Vector3(.16f, tune.rimHeight, 8.3f), edge);
            Box("East rim", new Vector3(4.08f, tune.rimHeight * .5f, 0), new Vector3(.16f, tune.rimHeight, 8.3f), edge);
            Box("Near rim", new Vector3(0, tune.rimHeight * .5f, -4.08f), new Vector3(8, tune.rimHeight, .16f), edge);
            Box("Far rim", new Vector3(0, tune.rimHeight * .5f, 4.08f), new Vector3(8, tune.rimHeight, .16f), edge);
            Box("Near launch rail", new Vector3(0, -.12f, -4.65f), new Vector3(8, .15f, .25f), edge, false);
            Box("Far launch rail", new Vector3(0, -.12f, 4.65f), new Vector3(8, .15f, .25f), edge, false);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Launcher"; marker.transform.SetParent(transform); marker.transform.localScale = Vector3.one * .25f;
            marker.GetComponent<Collider>().enabled = false; Destroy(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial = ringMaterial; launcher = marker.transform;
            trajectory = MakeLine("Approximate free-flight path", .025f, false);
            var cameraObject = new GameObject("Prototype Camera"); cameraObject.transform.SetParent(transform);
            view = cameraObject.AddComponent<Camera>(); view.tag = "MainCamera";
            view.transform.position = new Vector3(0, 12.5f, -10.5f); view.transform.LookAt(Vector3.zero);
            view.orthographic = true; view.orthographicSize = 6.6f;
            view.rect = new Rect(.29f, 0, .71f, 1);
            view.backgroundColor = new Color(.025f, .04f, .06f); view.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            var lamp = new GameObject("Key light"); lamp.transform.SetParent(transform);
            var lightComponent = lamp.AddComponent<Light>(); lightComponent.type = LightType.Directional; lightComponent.intensity = 1.8f;
            lamp.transform.rotation = Quaternion.Euler(50, -35, 0);
            RenderSettings.ambientLight = new Color(.5f, .55f, .6f);
        }
        LineRenderer MakeLine(string label, float width, bool loop)
        {
            var obj = new GameObject(label); obj.transform.SetParent(transform);
            var line = obj.AddComponent<LineRenderer>(); line.sharedMaterial = ringMaterial;
            line.startWidth = line.endWidth = width; line.useWorldSpace = true; line.loop = loop;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }
        Mesh BuildCylinder()
        {
            const int sides = 24;
            var vertices = new Vector3[sides * 2 + 2];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                vertices[i] = new Vector3(Mathf.Cos(a) * tune.diameter * .5f, -tune.thickness * .5f, Mathf.Sin(a) * tune.diameter * .5f);
                vertices[i + sides] = vertices[i] + Vector3.up * tune.thickness;
            }
            vertices[sides * 2] = Vector3.down * tune.thickness * .5f;
            vertices[sides * 2 + 1] = Vector3.up * tune.thickness * .5f;
            var triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                triangles.AddRange(new[] { sides * 2, i, j, sides * 2 + 1, j + sides, i + sides,
                    i, i + sides, j + sides, i, j + sides, j });
            }
            var mesh = new Mesh { name = "24-sided physical stone", vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        StoneComponent AddStone(int owner, Vector3 position)
        {
            var obj = new GameObject("Stone " + nextId); obj.transform.SetParent(transform); obj.transform.position = position;
            obj.AddComponent<MeshFilter>().sharedMesh = stoneMesh;
            var s = obj.AddComponent<StoneComponent>(); s.Id = nextId++; s.Owner = owner;
            s.Surface = obj.AddComponent<MeshRenderer>(); s.Surface.sharedMaterial = owner == 1 ? black : white;
            s.Shape = obj.AddComponent<MeshCollider>(); s.Shape.sharedMesh = stoneMesh; s.Shape.convex = true; s.Shape.sharedMaterial = physicsMaterial;
            s.Body = obj.AddComponent<Rigidbody>(); s.Body.mass = 1; s.Body.linearDamping = tune.linearDamping; s.Body.angularDamping = tune.angularDamping;
            s.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            s.Body.interpolation = RigidbodyInterpolation.Interpolate; s.Body.solverIterations = 12; s.Body.solverVelocityIterations = 8;
            s.Body.maxAngularVelocity = 35;
            s.Ring = MakeLine("Recognition " + s.Id, .018f, true); s.Ring.enabled = false;
            stones.Add(s); return s;
        }
        void Place(int owner, int x, int z) => AddStone(owner, new Vector3(x - 3.5f, tune.thickness * .5f + .003f, z - 3.5f));
        void ResetBoard(bool usePractice)
        {
            StopAllCoroutines(); dragging = false;
            foreach (var s in stones) { s.gameObject.SetActive(false); Destroy(s.Ring.gameObject); Destroy(s.gameObject); }
            stones.Clear(); nextId = 0; turn = 0; player = 1; finished = false; practice = usePractice;
            reserves[1] = reserves[2] = tune.reservePerPlayer; railX = -.5f; angle = 0; power = .12f;
            if (practice) { Place(2, 3, 2); Place(1, 3, 3); }
            else { Place(2, 3, 3); Place(2, 4, 4); Place(1, 3, 4); Place(1, 4, 3); }
            Physics.SyncTransforms();
            foreach (var s in stones) s.Body.Sleep();
            board = Recognize(); aiming = true; phase = "Ready"; captureCount = 0;
            lastResult = practice ? "Practice: land at the green target in front of WHITE." : "Central four stones. BLACK starts.";
        }
        void Update()
        {
            if (tune == null) return;
            CollectOutside();
            if (aiming)
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    Vector2 p = mouse.position.ReadValue();
                    if (mouse.rightButton.wasPressedThisFrame) dragging = false;
                    if (mouse.leftButton.wasPressedThisFrame && p.x > Screen.width * .30f)
                    {
                        var plane = new Plane(Vector3.up, Vector3.zero);
                        Ray ray = view.ScreenPointToRay(p);
                        if (plane.Raycast(ray, out float distance)) railX = Mathf.Clamp(ray.GetPoint(distance).x, -3.5f, 3.5f);
                        dragging = true; dragStart = p; dragAngle = angle; dragPower = power;
                    }
                    if (dragging)
                    {
                        Vector2 delta = p - dragStart;
                        angle = Mathf.Clamp(dragAngle + delta.x * .18f, -tune.maxAngle, tune.maxAngle);
                        power = Mathf.Clamp01(dragPower - delta.y / 350f);
                        if (mouse.leftButton.wasReleasedThisFrame) { dragging = false; Fire(); }
                    }
                }
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) { dragging = false; Fire(); }
            }
            launcher.position = LaunchPosition; launcher.gameObject.SetActive(aiming);
            trajectory.enabled = aiming;
            if (aiming) DrawTrajectory();
        }
        void DrawTrajectory()
        {
            // A ballistic guide up to first surface impact, NOT a collision outcome prediction.
            var points = new List<Vector3>();
            for (int i = 0; i < 60; i++)
            {
                float t = i * .02f;
                var p = LaunchPosition + LaunchVelocity * t + .5f * Physics.gravity * t * t;
                points.Add(p);
                if (p.y <= tune.thickness * .5f) break;
            }
            trajectory.positionCount = points.Count; trajectory.SetPositions(points.ToArray());
        }
        void CollectOutside()
        {
            foreach (var s in stones)
            {
                if (s.Removed) continue;
                Bounds b = s.Shape.bounds;
                if (b.min.x > tune.collectionBoundary || b.max.x < -tune.collectionBoundary ||
                    b.min.z > tune.collectionBoundary || b.max.z < -tune.collectionBoundary || b.max.y < -.6f)
                { s.Removed = true; s.Ring.enabled = false; s.gameObject.SetActive(false); }
            }
        }
        void Fire()
        {
            if (!aiming || finished) return;
            aiming = false; reserves[player]--; turn++;
            foreach (var s in stones) s.Ring.enabled = false;
            var shot = AddStone(player, LaunchPosition); shot.Body.linearVelocity = LaunchVelocity;
            StartCoroutine(ResolveTurn(shot));
        }
        IEnumerator ResolveTurn(StoneComponent shot)
        {
            phase = "Moving - score is previous snapshot";
            yield return Settle();
            board = Recognize();
            int origin = System.Array.IndexOf(board.Ids, shot.Id);
            var captured = BoardRules.Captures(board, origin, player);
            captureCount = captured.Count;
            // Commit ALL ownership first; apply physical impulses in a separate pass.
            foreach (int id in captured) { var s = stones.Find(item => item.Id == id); s.Owner = player; s.Surface.sharedMaterial = player == 1 ? black : white; }
            if (captured.Count > 0)
            {
                phase = "Capture animation - score pending";
                foreach (var s in stones) s.Ring.enabled = false;
                foreach (int id in captured)
                {
                    var s = stones.Find(item => item.Id == id);
                    s.Body.AddForce(Vector3.up * tune.captureLift, ForceMode.VelocityChange);
                    s.Body.AddTorque(Vector3.right * tune.captureSpin, ForceMode.VelocityChange);
                }
                yield return Settle();
                board = Recognize(); // Deliberately no capture search after snapshot B.
            }
            lastResult = "Shot " + turn + ": captured " + captureCount + (origin < 0 ? " / shot did not score." : ".");
            finished = reserves[1] == 0 && reserves[2] == 0;
            if (finished)
            {
                int b = board.Count(1), w = board.Count(2);
                phase = b == w ? "DRAW" : b > w ? "BLACK WINS" : "WHITE WINS";
                aiming = false;
            }
            else { player = 3 - player; angle = 0; aiming = true; phase = "Ready"; }
        }
        IEnumerator Settle()
        {
            float elapsed = 0, stable = 0;
            // Wait at least one physics step before evaluating a freshly launched body.
            while (elapsed < tune.phaseTimeout)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime; CollectOutside();
                bool quiet = true;
                float ramp = Mathf.InverseLerp(tune.phaseTimeout - tune.dampingRampSeconds, tune.phaseTimeout, elapsed);
                foreach (var s in stones)
                {
                    if (s.Removed) continue;
                    s.Body.linearDamping = Mathf.Lerp(tune.linearDamping, 12f, ramp);
                    s.Body.angularDamping = Mathf.Lerp(tune.angularDamping, 16f, ramp);
                    if (s.Body.linearVelocity.magnitude > tune.speedThreshold || s.Body.angularVelocity.magnitude > tune.spinThreshold) quiet = false;
                }
                stable = quiet ? stable + Time.fixedDeltaTime : 0;
                if (stable >= tune.stableSeconds) break;
            }
            foreach (var s in stones)
            {
                if (s.Removed) continue;
                s.Body.linearVelocity = Vector3.zero; s.Body.angularVelocity = Vector3.zero;
                s.Body.Sleep(); // Still dynamic: the next collision can wake this body.
                s.Body.linearDamping = tune.linearDamping; s.Body.angularDamping = tune.angularDamping;
            }
        }
        BoardRules.Snapshot Recognize()
        {
            Physics.SyncTransforms();
            var candidates = new List<BoardRules.Candidate>();
            foreach (var s in stones)
            {
                s.FirstCell = s.RecognizedCell = -1; s.Ratio = 0; s.Ring.enabled = false;
                if (s.Removed) continue;
                // Flat prototype board: bottom proximity plus height excludes upper stacked stones.
                Bounds bounds = s.Shape.bounds;
                if (Mathf.Abs(bounds.min.y) > tune.contactTolerance || s.transform.position.y > tune.thickness * .5f + tune.centerHeightAllowance) continue;
                var points = new List<BoardRules.Point>();
                foreach (var vertex in stoneMesh.vertices)
                {
                    Vector3 p = s.transform.TransformPoint(vertex); points.Add(new BoardRules.Point(p.x, p.z));
                }
                candidates.Add(new BoardRules.Candidate { Id = s.Id, Owner = s.Owner, Polygon = BoardRules.Hull(points) });
            }
            var snapshot = BoardRules.Recognize(candidates, tune.recognitionMinimum, tune.tieTolerance);
            foreach (var candidate in candidates)
            {
                var s = stones.Find(item => item.Id == candidate.Id); s.FirstCell = candidate.FirstCell; s.Ratio = candidate.Ratio;
            }
            for (int cell = 0; cell < 64; cell++)
            {
                if (snapshot.Ids[cell] < 0) continue;
                var s = stones.Find(item => item.Id == snapshot.Ids[cell]); s.RecognizedCell = cell;
                s.Ring.enabled = true; s.Ring.positionCount = 40;
                for (int i = 0; i < 40; i++)
                {
                    float a = i * Mathf.PI * 2 / 40;
                    s.Ring.SetPosition(i, new Vector3(cell % 8 - 3.5f + Mathf.Cos(a) * .46f, .025f, cell / 8 - 3.5f + Mathf.Sin(a) * .46f));
                }
            }
            return snapshot;
        }
        void OnGUI()
        {
            if (tune == null) return;
            float uiScale = Mathf.Max(.65f, Screen.height / 820f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            float panelWidth = Screen.width * .285f / uiScale;
            GUILayout.BeginArea(new Rect(8, 8, panelWidth - 12, Screen.height / uiScale - 16), GUI.skin.box);
            GUILayout.Label("PHYSICS REVERSI", new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold });
            GUILayout.Label("First playable / " + (practice ? "Practice" : "Central four"));
            GUILayout.Space(12);
            GUILayout.Label("BLACK  " + board.Count(1) + "       WHITE  " + board.Count(2));
            GUILayout.Label("Reserve: " + reserves[1] + " / " + reserves[2]);
            GUILayout.Label("Turn: " + (player == 1 ? "BLACK (near side)" : "WHITE (far side)"));
            GUILayout.Label(phase, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Space(10);
            GUI.enabled = aiming;
            GUILayout.Label("Rail: " + railX.ToString("F2")); railX = GUILayout.HorizontalSlider(railX, -3.5f, 3.5f);
            GUILayout.Label("Angle: " + angle.ToString("F1") + " deg"); angle = GUILayout.HorizontalSlider(angle, -tune.maxAngle, tune.maxAngle);
            GUILayout.Label("Power: " + (power * 100).ToString("F0") + "%"); power = GUILayout.HorizontalSlider(power, 0, 1);
            if (GUILayout.Button("FIRE  [Space]", GUILayout.Height(35))) Fire();
            GUI.enabled = true;
            GUILayout.Label("Or drag on board: down = power, sideways = angle. Release = fire. Right click = cancel.", new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Label("Cyan rings = scored cells. Guide shows free flight only; collisions and rolling are not predicted.", new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Space(8);
            GUILayout.Label(lastResult, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Space(8);
            if (GUILayout.Button("Reset: capture practice")) ResetBoard(true);
            if (GUILayout.Button("Reset: central four")) ResetBoard(false);
            debug = GUILayout.Toggle(debug, "Tuning + recognition details");
            if (debug)
            {
                GUI.enabled = aiming || finished;
                GUILayout.Label("Friction: " + tune.friction.ToString("F2")); tune.friction = GUILayout.HorizontalSlider(tune.friction, .03f, .6f);
                GUILayout.Label("Bounce: " + tune.bounce.ToString("F2")); tune.bounce = GUILayout.HorizontalSlider(tune.bounce, 0, .6f);
                GUILayout.Label("Capture lift: " + tune.captureLift.ToString("F2")); tune.captureLift = GUILayout.HorizontalSlider(tune.captureLift, 0, 1.5f);
                GUILayout.Label("Max speed: " + tune.maxSpeed.ToString("F1")); tune.maxSpeed = GUILayout.HorizontalSlider(tune.maxSpeed, 5, 15);
                UpdatePhysicsMaterial(); GUI.enabled = true;
                GUILayout.Label("Live adjustments reset on exit.\nMore values: Tuning asset in Inspector.");
            }
            GUILayout.EndArea(); GUI.matrix = Matrix4x4.identity;
            if (practice && aiming && player == 1 && turn == 0)
            {
                Vector3 p = view.WorldToScreenPoint(new Vector3(-.5f, .03f, -2.5f));
                GUI.color = Color.green; GUI.Label(new Rect(p.x - 25, Screen.height - p.y - 12, 70, 25), "TARGET"); GUI.color = Color.white;
            }
            if (debug) foreach (var s in stones)
            {
                if (s.Removed) continue;
                var p = view.WorldToScreenPoint(s.transform.position);
                string label = "#" + s.Id + " " + (s.RecognizedCell >= 0 ? "cell " + s.RecognizedCell : "UNSCORED") + "\nfirst " + s.FirstCell + " / " + s.Ratio.ToString("P0");
                GUI.Label(new Rect(p.x + 8, Screen.height - p.y - 12, 145, 45), label);
            }
        }
        void OnDestroy()
        {
            if (previousFixedDelta > 0) Time.fixedDeltaTime = previousFixedDelta;
            foreach (var material in materials) if (material != null) Destroy(material);
            if (stoneMesh != null) Destroy(stoneMesh);
            if (physicsMaterial != null) Destroy(physicsMaterial);
            if (tune != null) Destroy(tune);
        }
    }
}
