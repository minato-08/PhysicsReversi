using UnityEngine;
using UnityEngine.UI;

namespace PhysicsReversi.Walk
{
    // Scene-authored UI; only existing Text values change during play.
    public sealed class ScoreHud : MonoBehaviour
    {
        public WalkBoardRecognition recognition;
        public Text blackScore;
        public Text whiteScore;
        public Text statusText;
        public string loadingLabel = "Reading board";
        public string liveLabel = "Live score";
        BoardRules.Snapshot displayed;

        void OnEnable()
        {
            displayed = null;
            SetText(blackScore, "--"); SetText(whiteScore, "--"); SetText(statusText, loadingLabel);
        }
        void LateUpdate()
        {
            if (recognition == null || !recognition.isActiveAndEnabled)
            { SetText(statusText, "Recognition unavailable"); return; }
            if (recognition.HasConfirmedSnapshot && displayed != recognition.Snapshot)
            {
                displayed = recognition.Snapshot;
                SetText(blackScore, displayed.Count(1).ToString());
                SetText(whiteScore, displayed.Count(2).ToString());
            }
            SetText(statusText, !recognition.HasConfirmedSnapshot ? loadingLabel : liveLabel);
        }
        static void SetText(Text target, string value)
        {
            if (target != null && target.text != value) target.text = value;
        }
    }
}
