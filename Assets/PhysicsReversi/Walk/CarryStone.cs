using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace PhysicsReversi.Walk
{
    public enum StoneStatus { Reserve, Held, OnBoard, Removed }

    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarryStone : MonoBehaviour
    {
        [Min(0)] public int ownerId = 1;
        [Tooltip("Supply ownership; independent of the visible face once placed.")]
        public int reserveOwnerId = 1;
        [HideInInspector] public bool twoSided;
        public StoneStatus status = StoneStatus.Reserve;
        public WalkPlayer Holder { get; private set; }
        public Rigidbody Body { get; private set; }
        public bool IsFlipping { get; private set; }
        public MotionOrigin Motion { get; private set; }
        public void RegisterPlayerMotion() => Motion = new MotionOrigin(true);
        readonly StoneConfirmation confirmation = new StoneConfirmation();
        // Recognized in one cell for long enough; see StoneConfirmation.
        public bool Confirmed => confirmation.Confirmed;
        // Called by the board recognition only, once per sample, after it has set the
        // recognition text. cell is -1 when the stone is not recognized.
        public void TickConfirmation(int cell, float deltaSeconds, float confirmSeconds, float loosenSeconds)
        {
            if (status != StoneStatus.OnBoard) confirmation.Reset();
            else confirmation.Tick(cell, IsFlipping, deltaSeconds, confirmSeconds, loosenSeconds);
            if (Confirmed) recognition += " / confirmed";
        }
        public void ReadUpperFace(Vector3 boardUp, float tolerance)
        {
            if (status == StoneStatus.OnBoard) ownerId = StoneFaces.Owner(Vector3.Dot(transform.up, boardUp), tolerance);
        }
        public void Flip(Vector3 up, Vector3 axis, float height, float duration)
        {
            if (IsFlipping || Body == null || Body.isKinematic || status != StoneStatus.OnBoard) return;
            Motion = new MotionOrigin(false);
            StartCoroutine(FlipBody(up, axis, height, duration));
        }
        IEnumerator FlipBody(Vector3 up, Vector3 axis, float height, float duration)
        {
            IsFlipping = true; Body.WakeUp();
            Quaternion start = Body.rotation;
            float startHeight = Vector3.Dot(Body.position, up);
            float elapsed = 0;
            duration = Mathf.Max(.5f, duration);
            // A bounded physical motor: the body stays dynamic and collides throughout.
            while (elapsed < duration)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float rotationProgress = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.15f, .8f, t));
                Quaternion target = Quaternion.AngleAxis(180 * rotationProgress, axis) * start;
                Quaternion error = target * Quaternion.Inverse(Body.rotation);
                error.ToAngleAxis(out float degrees, out Vector3 direction);
                if (degrees > 180) degrees -= 360;
                if (!float.IsNaN(direction.x)) Body.angularVelocity = direction * Mathf.Clamp(degrees * Mathf.Deg2Rad * 20, -12, 12);
                float targetHeight = startHeight + height * Mathf.Sin(Mathf.PI * t);
                float vertical = Mathf.Clamp((targetHeight - Vector3.Dot(Body.position, up)) * 18, -8, 8);
                Body.linearVelocity += up * (vertical - Vector3.Dot(Body.linearVelocity, up));
            }
            Body.angularVelocity = Vector3.zero;
            IsFlipping = false;
        }
        readonly HashSet<Collider> boardContacts = new HashSet<Collider>();
        [SerializeField] string recognition = "Not checked";
        public bool TouchingBoard => boardContacts.Count > 0;
        public void SetRecognition(string value) => recognition = value;
        // Where the scene author put this stone; reserve stones return to these slots.
        public Vector3 HomePosition { get; private set; }
        public Quaternion HomeRotation { get; private set; }
        public bool HasReserveSlot { get; private set; }
        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            HomePosition = transform.position; HomeRotation = transform.rotation;
            HasReserveSlot = status == StoneStatus.Reserve;
        }
        // Called by CarryAuthority only.
        public void ReturnToReserve(Vector3 position, Quaternion rotation)
        {
            StopAllCoroutines(); IsFlipping = false; Motion = null; boardContacts.Clear(); confirmation.Reset();
            Holder = null; status = StoneStatus.Reserve; ownerId = reserveOwnerId;
            transform.SetPositionAndRotation(position, rotation);
            Body.position = position; Body.rotation = rotation;
            Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero;
        }
        void OnCollisionEnter(Collision collision) { TrackBoardContact(collision); TrackMotion(collision); }
        void OnCollisionStay(Collision collision) { TrackBoardContact(collision); TrackMotion(collision); }
        void TrackMotion(Collision collision)
        {
            var other = collision.collider.GetComponentInParent<CarryStone>();
            if (other == null || other == this || collision.relativeVelocity.sqrMagnitude < .0064f) return;
            // A moving carried stone is also a deliberate tool for pushing another stone.
            if (Holder != null && Holder.HasManipulationInput && !IsFlipping) RegisterPlayerMotion();
            if (other.Holder != null && other.Holder.HasManipulationInput && !other.IsFlipping) other.RegisterPlayerMotion();
            var newest = MotionOrigin.Newest(Motion, other.Motion);
            if (newest == null) return;
            // The capture motor remains the cause of its own motion until it finishes.
            if (!IsFlipping) Motion = newest;
            if (!other.IsFlipping) other.Motion = newest;
        }
        void OnCollisionExit(Collision collision) => boardContacts.Remove(collision.collider);
        void TrackBoardContact(Collision collision)
        {
            if (collision.collider.GetComponent<RecognitionCell>() == null) return;
            bool supported = false;
            foreach (var contact in collision.contacts)
                if (Vector3.Dot(contact.normal, collision.collider.transform.up) > .2f) { supported = true; break; }
            if (supported) boardContacts.Add(collision.collider);
            else boardContacts.Remove(collision.collider);
        }
        void OnDisable() { StopAllCoroutines(); IsFlipping = false; Motion = null; boardContacts.Clear(); confirmation.Reset(); }
        // The pickup rule without side effects; the aim highlight asks the same question.
        public bool CanClaim(WalkPlayer player, bool allowPlaced = false, bool allowOpponent = false, bool lockConfirmed = false)
        {
            if (player == null || !isActiveAndEnabled || Body == null || Holder != null || IsFlipping) return false;
            if (status == StoneStatus.Reserve) return reserveOwnerId == player.playerId;
            // A confirmed stone has to be knocked out of its cell before anyone can pick it up.
            if (status == StoneStatus.OnBoard)
                return allowPlaced && (allowOpponent || ownerId == player.playerId) && !(lockConfirmed && Confirmed);
            return false;
        }
        public bool TryClaim(WalkPlayer player, bool allowPlaced = false, bool allowOpponent = false, bool lockConfirmed = false)
        {
            if (!CanClaim(player, allowPlaced, allowOpponent, lockConfirmed)) return false;
            Holder = player; status = StoneStatus.Held; Body.WakeUp(); return true;
        }
        public void Release()
        {
            Holder = null; status = StoneStatus.OnBoard;
            // A released stone always starts loose, even if grabbed and dropped between two samples.
            confirmation.Reset();
            RegisterPlayerMotion();
        }
    }
}
