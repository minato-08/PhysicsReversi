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
        [Tooltip("Off: every recognized stone counts. On: only confirmed stones count.")]
        public bool countConfirmedOnly;
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
            var board = countConfirmedOnly ? recognition.Settled : recognition.Snapshot;
            if (recognition.HasSnapshot && displayed != board)
            {
                displayed = board;
                SetText(blackScore, displayed.Count(1).ToString());
                SetText(whiteScore, displayed.Count(2).ToString());
            }
            SetText(statusText, !recognition.HasSnapshot ? loadingLabel : liveLabel);
        }
        static void SetText(Text target, string value)
        {
            if (target != null && target.text != value) target.text = value;
        }
    }
}
