using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PhysicsReversi.Walk
{
    // One match: a countdown, a timed round, then the result. Players can move only while the
    // round runs. The winner is decided by the stones recognized on the board once it has settled.
    public sealed class WalkMatch : MonoBehaviour
    {
        public enum Phase { Countdown, Playing, Settling, Finished }

        public WalkBoardRecognition recognition;
        [Tooltip("Turned off outside the round, so nobody can move or grab.")]
        public LocalWalkInput input;
        [Min(1)] public float matchSeconds = 180;
        [Min(0)] public float countdownSeconds = 3;
        [Tooltip("After time is up, the count waits until no stone on the board moves, for at least this long.")]
        [Min(0)] public float settleSeconds = 1.5f;
        [Tooltip("A stone on the board slower than this (units per second) counts as settled.")]
        [Min(0)] public float settledSpeed = .05f;
        [Tooltip("The count is taken after this long even if a stone is still moving.")]
        [Min(0)] public float maxSettleSeconds = 4;
        [Tooltip("The result stays this long before a button can start the next match.")]
        [Min(0)] public float restartLockSeconds = 1;
        public Text timerText;
        public Text bannerText;
        public Text hintText;
        public string restartHint = "Press F / H / A to play again";

        public Phase Current { get; private set; }
        public float Remaining => Current == Phase.Countdown ? matchSeconds
            : Current == Phase.Playing ? Mathf.Max(0, matchSeconds - phaseTime) : 0;
        // Seconds since the current phase began; the HUD times its countdown and result from it.
        public float PhaseTime => phaseTime;
        public int BlackCount { get; private set; }
        public int WhiteCount { get; private set; }
        float phaseTime;

        void Start() => Enter(countdownSeconds > 0 ? Phase.Countdown : Phase.Playing);

        void Update()
        {
            phaseTime += Time.deltaTime;
            switch (Current)
            {
                case Phase.Countdown:
                    SetText(bannerText, Mathf.CeilToInt(countdownSeconds - phaseTime).ToString());
                    if (phaseTime >= countdownSeconds) Enter(Phase.Playing);
                    break;
                case Phase.Playing:
                    if (phaseTime < .8f) SetText(bannerText, "GO!");
                    else SetText(bannerText, "");
                    if (phaseTime >= matchSeconds) Enter(Phase.Settling);
                    break;
                case Phase.Settling:
                    if ((phaseTime >= settleSeconds && !AnyMoving()) || phaseTime >= maxSettleSeconds) Enter(Phase.Finished);
                    break;
                case Phase.Finished:
                    if (phaseTime >= restartLockSeconds)
                    {
                        SetText(hintText, restartHint);
                        // Reloading rebuilds the board, the reserves and both players as authored.
                        // The scene must be in the build list (the setup menu adds it).
                        if (RestartPressed()) Restart();
                    }
                    break;
            }
            ShowTimer();
        }

        void Enter(Phase next)
        {
            Current = next; phaseTime = 0;
            if (input != null) input.enabled = next == Phase.Playing;
            SetText(hintText, "");
            if (next == Phase.Settling) { SetText(bannerText, "TIME UP"); DropHeldStones(); }
            if (next == Phase.Finished) ShowResult();
        }

        void ShowResult()
        {
            var snapshot = recognition != null && recognition.HasSnapshot ? recognition.Snapshot : null;
            BlackCount = snapshot != null ? snapshot.Count(1) : 0;
            WhiteCount = snapshot != null ? snapshot.Count(2) : 0;
            string winner = BlackCount > WhiteCount ? "BLACK WINS" : WhiteCount > BlackCount ? "WHITE WINS" : "DRAW";
            SetText(bannerText, winner + "\n" + BlackCount + " - " + WhiteCount);
        }

        void ShowTimer()
        {
            int seconds = Mathf.CeilToInt(Remaining);
            SetText(timerText, (seconds / 60) + ":" + (seconds % 60).ToString("00"));
        }

        // A stone still sliding may yet stop in a line and capture, so the count waits for it.
        bool AnyMoving()
        {
            if (recognition == null || recognition.stones == null) return false;
            float limit = settledSpeed * settledSpeed;
            foreach (var stone in recognition.stones)
            {
                if (stone == null) continue;
                if (stone.IsFlipping) return true;
                var body = stone.Body;
                if (stone.status != StoneStatus.OnBoard || body == null || body.isKinematic || body.IsSleeping()) continue;
                if (body.linearVelocity.sqrMagnitude > limit || body.angularVelocity.sqrMagnitude > limit) return true;
            }
            return false;
        }

        // Stones in hand at time-up are put down in front of their holders (decided 2026-11-25),
        // then counted with the rest once the board settles. Input is already off, so they fall straight.
        void DropHeldStones()
        {
            if (input == null || input.authority == null || input.players == null) return;
            foreach (var player in input.players) input.authority.ForceDrop(player);
        }

        void Restart()
        {
            int index = SceneManager.GetActiveScene().buildIndex;
            if (index < 0) { Debug.LogError("Add this scene to the build list (Physics Reversi/Walk/Add Match Flow (Flat)) to restart."); return; }
            SceneManager.LoadScene(index);
        }

        // If the match is switched off mid-round, players get their controls back.
        void OnDisable()
        {
            if (input != null) input.enabled = true;
        }

        static bool RestartPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.fKey.wasPressedThisFrame || keyboard.hKey.wasPressedThisFrame
                || keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) return true;
            foreach (var pad in Gamepad.all)
                if (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame) return true;
            return false;
        }

        static void SetText(Text target, string value)
        {
            if (target != null && target.text != value) target.text = value;
        }
    }
}
