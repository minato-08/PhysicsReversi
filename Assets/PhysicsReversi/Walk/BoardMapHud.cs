using UnityEngine;
using UnityEngine.UI;

namespace PhysicsReversi.Walk
{
    // Top-down map of the recognized board plus the player's position and view direction.
    // Scene-authored UI; only colors, visibility and the marker change during play.
    public sealed class BoardMapHud : MonoBehaviour
    {
        public WalkBoardRecognition recognition;
        public Transform player;
        public Transform view;
        [Tooltip("One per cell, index = z * 8 + x. Shown only while that cell holds a recognized stone.")]
        public Image[] stones;
        public RectTransform marker;
        [Tooltip("Map pixels per world unit, matching the authored cell size.")]
        public float pixelsPerUnit = 4.5f;
        public Color black = new Color(.06f, .06f, .08f);
        public Color white = new Color(.93f, .91f, .82f);
        BoardRules.Snapshot displayed;

        void OnEnable() => displayed = null;
        void LateUpdate()
        {
            if (recognition == null || recognition.boardOrigin == null) return;
            if (stones != null && recognition.HasConfirmedSnapshot && displayed != recognition.Snapshot)
            {
                displayed = recognition.Snapshot;
                for (int i = 0; i < stones.Length && i < 64; i++)
                {
                    if (stones[i] == null) continue;
                    int owner = displayed.Owners[i];
                    stones[i].enabled = owner != 0;
                    if (owner != 0) stones[i].color = owner == 1 ? black : white;
                }
            }
            if (marker == null || player == null) return;
            Vector3 local = recognition.boardOrigin.InverseTransformPoint(player.position);
            marker.anchoredPosition = new Vector2(local.x, local.z) * pixelsPerUnit;
            if (view == null) return;
            Vector3 forward = recognition.boardOrigin.InverseTransformDirection(view.forward);
            marker.localEulerAngles = new Vector3(0, 0, -Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg);
        }
    }
}
