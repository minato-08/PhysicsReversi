using System.Collections.Generic;
using UnityEngine;

namespace PhysicsReversi.Walk
{
    // The result count (UI.md): once the match is over and counted, every counted stone flies from
    // its cell onto one of two stacks in the middle of the board, black on the left and white on the
    // right as the camera sees it, one at a time and taking turns, its own colour up. MatchHud puts
    // the running counts on the stacks. The count itself is already settled (WalkMatch counts on
    // entering Finished); this only shows it, so the recognition and the captures are switched off
    // first, or the stacks would be read as a board and could capture.
    public sealed class ResultStacker : MonoBehaviour
    {
        public WalkMatch match;
        public WalkBoardRecognition recognition;
        public WalkCaptureController captures;
        [Tooltip("Left and right are taken from this camera's view.")]
        public Camera view;

        [Header("Stacks")]
        [Tooltip("How far each stack stands from the middle of the board, sideways on screen (world units; a cell is 4).")]
        public float spacing = 4.5f;
        [Tooltip("The height one stone adds to a stack.")]
        public float stoneThickness = .36f;

        [Header("Timing")]
        [Tooltip("Wait after the result begins before the first stone flies.")]
        public float delay = .5f;
        [Tooltip("Time between one stone and the next; black and white take turns.")]
        public float interval = .07f;
        [Tooltip("How long one stone's flight takes.")]
        public float flight = .38f;
        [Tooltip("How high above the straight line a stone arcs on its way.")]
        public float arc = 3;
        [Tooltip("The squash a stone gives as it lands on its stack.")]
        public Vector3 landPop = new Vector3(1.12f, .6f, 1.12f);

        public bool Started { get; private set; }
        public bool Done { get; private set; }
        // Raised as each stone lands: its colour (1 black, 2 white) and that stack's new height in stones.
        public event System.Action<int, int> StoneStacked;

        sealed class Flight
        {
            public CarryStone stone; public int owner, slot; public float start;
            public Vector3 from, to; public Quaternion fromRotation, toRotation; public bool landed;
        }
        readonly List<Flight> flights = new List<Flight>();
        readonly int[] stacked = new int[3];
        readonly Vector3[] stackBase = new Vector3[3];
        Vector3 up = Vector3.up;

        public int Stacked(int owner) => owner == 1 || owner == 2 ? stacked[owner] : 0;
        // The top of a stack as it stands now, in the world.
        public Vector3 StackTop(int owner) => stackBase[owner] + up * (stoneThickness * (stacked[owner] - .5f));

        void Update()
        {
            if (match == null) return;
            if (!Started && match.Current == WalkMatch.Phase.Finished) Begin();
            if (Started && !Done) Fly();
        }

        void Begin()
        {
            Started = true;
            if (recognition == null || !recognition.HasSnapshot || recognition.stones == null) { Done = true; return; }
            var snapshot = recognition.Snapshot;
            // From here on the board is no longer read: nothing is captured, nothing changes owner.
            recognition.enabled = false;
            if (captures != null) captures.enabled = false;

            var board = recognition.boardOrigin;
            up = board != null ? board.up : Vector3.up;
            Vector3 centre = board != null ? board.position : Vector3.zero;
            Vector3 right = view != null ? Vector3.ProjectOnPlane(view.transform.right, up).normalized : Vector3.right;
            var lists = new[] { null, new List<CarryStone>(), new List<CarryStone>() };
            float rest = float.MaxValue;
            for (int cell = 0; cell < 64; cell++)
            {
                int id = snapshot.Ids[cell], owner = snapshot.Owners[cell];
                if (id < 0 || id >= recognition.stones.Length || (owner != 1 && owner != 2)) continue;
                var stone = recognition.stones[id]; if (stone == null) continue;
                lists[owner].Add(stone);
                rest = Mathf.Min(rest, Vector3.Dot(stone.transform.position - centre, up));
            }
            // The lowest stone lies on the board: the stacks start at its height.
            if (rest == float.MaxValue) rest = stoneThickness * .5f;
            stackBase[1] = centre - right * spacing + up * rest;
            stackBase[2] = centre + right * spacing + up * rest;

            for (int owner = 1; owner <= 2; owner++)
            {
                // Farthest from its stack first, so the stones near the middle are not in the way.
                var stack = stackBase[owner];
                lists[owner].Sort((a, b) => (b.transform.position - stack).sqrMagnitude.CompareTo((a.transform.position - stack).sqrMagnitude));
                for (int i = 0; i < lists[owner].Count; i++)
                {
                    var stone = lists[owner][i];
                    if (stone.Body != null) { stone.Body.linearVelocity = Vector3.zero; stone.Body.angularVelocity = Vector3.zero; stone.Body.isKinematic = true; }
                    // Lying flat with the stone's own colour up: black's face is its local +Y.
                    Quaternion level = Quaternion.FromToRotation(stone.transform.up, up * StoneFaces.UpSign(owner)) * stone.transform.rotation;
                    flights.Add(new Flight
                    {
                        stone = stone, owner = owner, slot = i,
                        start = delay + (2 * i + (owner - 1)) * interval,
                        from = stone.transform.position, fromRotation = stone.transform.rotation,
                        to = stack + up * (stoneThickness * i), toRotation = level,
                    });
                }
            }
            if (flights.Count == 0) Done = true;
        }

        void Fly()
        {
            float now = match.PhaseTime;
            bool flying = false;
            foreach (var f in flights)
            {
                if (f.landed) continue;
                flying = true;
                float u = Mathf.Clamp01((now - f.start) / flight);
                if (u <= 0) continue;
                // Out and over in a smooth glide, coming down onto the stack at the end.
                float glide = u * u * (3 - 2 * u);
                var t = f.stone.transform;
                t.SetPositionAndRotation(Vector3.Lerp(f.from, f.to, glide) + up * arc * Mathf.Sin(u * Mathf.PI),
                                         Quaternion.Slerp(f.fromRotation, f.toRotation, glide));
                if (u < 1) continue;
                f.landed = true;
                stacked[f.owner] = Mathf.Max(stacked[f.owner], f.slot + 1);
                ToonFx.Pop(f.stone.GetComponent<Renderer>(), landPop, .16f);
                StoneStacked?.Invoke(f.owner, stacked[f.owner]);
            }
            if (!flying) Done = true;
        }
    }
}
