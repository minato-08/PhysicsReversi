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
        public void ReadUpperFace(Vector3 boardUp, float tolerance)
        {
            if (status == StoneStatus.OnBoard) ownerId = StoneFaces.Owner(Vector3.Dot(transform.up, boardUp), tolerance);
        }
        public void Flip(Vector3 up, Vector3 axis, float height, float duration)
        {
            if (IsFlipping || Body == null || Body.isKinematic || status != StoneStatus.OnBoard) return;
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
        void Awake() => Body = GetComponent<Rigidbody>();
        void OnCollisionEnter(Collision collision) => TrackBoardContact(collision);
        void OnCollisionStay(Collision collision) => TrackBoardContact(collision);
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
        void OnDisable() { StopAllCoroutines(); IsFlipping = false; boardContacts.Clear(); }
        public bool TryClaim(WalkPlayer player)
        {
            if (status != StoneStatus.Reserve || Holder != null || reserveOwnerId != player.playerId) return false;
            Holder = player; status = StoneStatus.Held; Body.WakeUp(); return true;
        }
        public void Release()
        {
            Holder = null; status = StoneStatus.OnBoard;
        }
    }
}
