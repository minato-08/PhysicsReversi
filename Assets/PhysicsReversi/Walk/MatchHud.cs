using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PhysicsReversi.Walk
{
    // The match HUD (UI.md, chosen 2026-11-27 as proposal B): a tug-of-war bar of black and white
    // stones at the top with the timer hanging under its middle, the controls in the bottom corners,
    // and the countdown, time-up and result over the board. The parts are built by the setup menu
    // (Build Match HUD); this only moves them. It reads the match and the board, and never changes them.
    public sealed class MatchHud : MonoBehaviour
    {
        public WalkMatch match;
        public WalkBoardRecognition recognition;
        [Tooltip("The players, for the name pins over their heads during the countdown.")]
        public LocalWalkInput input;
        public Camera view;

        [Header("Score bar")]
        [Tooltip("The black part of the bar. Its right edge is set to black's share of the stones.")]
        public RectTransform barFill;
        public TMP_Text blackCount, whiteCount;
        [Tooltip("Copied for each \"+n\" that floats up on the outer side of a count when it grows.")]
        public TMP_Text deltaTemplate;
        [Tooltip("How quickly the bar slides to a new share (seconds to settle, roughly).")]
        public float barSmoothing = .12f;

        [Header("Timer")]
        public RectTransform timer;
        public Image timerBack;
        public TMP_Text timerText;
        [Tooltip("In the last this many seconds the timer turns yellow, grows and beats each second.")]
        public float lastSeconds = 10;
        public float lastScale = 1.2f;

        [Header("Countdown")]
        public GameObject countdown;
        public TMP_Text countdownNumber;
        [Tooltip("Shown only before GO: the name pins and the one-line rule.")]
        public GameObject countdownIntro;
        [Tooltip("One per player, in the order of Local Walk Input's players.")]
        public RectTransform[] pins;
        [Tooltip("The size of 3, 2, 1 against GO! (which is drawn at full size). Kept modest: GO! is the moment.")]
        public float countdownScale = .72f;
        [Tooltip("How much 3, 2, 1 swell as each appears.")]
        public float countdownPop = .12f;
        [Tooltip("How far above a player's feet its pin points (world units).")]
        public float pinHeight = 3.4f;
        [Header("GO!")]
        [Tooltip("GO! stays this long after the countdown, from its entrance to its exit.")]
        public float goSeconds = 1.1f;
        [Tooltip("The entrance: it swells from small to Go Size in this long.")]
        public float goIn = .16f;
        [Tooltip("The size it swells to, then holds while growing very slowly by Go Drift.")]
        public float goSize = 1.15f;
        public float goDrift = .07f;
        [Tooltip("The exit at the end: a little swell (the anticipation) by Go Swell, then a quick shrink to nothing, in this long all told.")]
        public float goOut = .24f;
        public float goSwell = .1f;

        [Header("Time up and result")]
        public GameObject timeUp;
        public GameObject result;
        public TMP_Text resultTitle, resultBlack, resultWhite;
        public GameObject restartHint;

        int shownBlack = -1, shownWhite = -1;
        float barShare = .5f, barVelocity;
        string lastCountdown;
        WalkMatch.Phase lastPhase = (WalkMatch.Phase)(-1);
        readonly Dictionary<RectTransform, (float start, float rest, float amount)> pops = new Dictionary<RectTransform, (float, float, float)>();
        readonly List<(TMP_Text text, float born, Vector2 from)> deltas = new List<(TMP_Text, float, Vector2)>();
        const float PopSeconds = .38f, DeltaSeconds = .9f;

        void Start()
        {
            if (deltaTemplate != null) deltaTemplate.gameObject.SetActive(false);
            if (view == null) view = Camera.main;
        }

        void LateUpdate()
        {
            if (match == null) return;
            var phase = match.Current;
            bool entered = phase != lastPhase; lastPhase = phase;

            UpdateCounts();
            UpdateTimer(phase);
            UpdateCountdown(phase);
            SetActive(timeUp, phase == WalkMatch.Phase.Settling);
            SetActive(result, phase == WalkMatch.Phase.Finished);
            if (phase == WalkMatch.Phase.Finished) UpdateResult(entered);
            Animate();
        }

        void UpdateCounts()
        {
            if (recognition == null || !recognition.HasSnapshot) return;
            int black = recognition.Snapshot.Count(1), white = recognition.Snapshot.Count(2);
            if (black != shownBlack) { Grew(blackCount, black - shownBlack, shownBlack >= 0, -1); shownBlack = black; SetText(blackCount, black.ToString()); }
            if (white != shownWhite) { Grew(whiteCount, white - shownWhite, shownWhite >= 0, 1); shownWhite = white; SetText(whiteCount, white.ToString()); }
            int total = black + white;
            float target = total > 0 ? (float)black / total : .5f;
            barShare = Mathf.SmoothDamp(barShare, target, ref barVelocity, barSmoothing);
            if (barFill != null) { var max = barFill.anchorMax; max.x = barShare; barFill.anchorMax = max; }
        }

        // A count that grows bumps, and the gain floats up on the outer side of it: left of black's
        // number, right of white's, so the two mirror each other about the middle of the screen.
        void Grew(TMP_Text count, int change, bool counted, int side)
        {
            if (!counted || change <= 0 || count == null) return;
            Pop(count.rectTransform);
            if (deltaTemplate == null) return;
            var delta = Instantiate(deltaTemplate, deltaTemplate.transform.parent);
            delta.gameObject.SetActive(true);
            delta.text = "+" + change;
            var rect = delta.rectTransform; var countRect = count.rectTransform;
            // The count is aligned to its inner edge, so its outer edge is its preferred width away.
            float outer = countRect.anchoredPosition.x + side * (count.preferredWidth + 6);
            rect.pivot = new Vector2(side < 0 ? 1 : 0, .5f);
            delta.alignment = side < 0 ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            var from = new Vector2(outer, countRect.anchoredPosition.y + 12);
            rect.anchoredPosition = from;
            deltas.Add((delta, Time.time, from));
        }

        void UpdateTimer(WalkMatch.Phase phase)
        {
            if (timerText == null) return;
            float remaining = match.Remaining;
            int seconds = Mathf.CeilToInt(remaining);
            SetText(timerText, seconds / 60 + ":" + (seconds % 60).ToString("00"));
            bool last = phase == WalkMatch.Phase.Playing && remaining <= lastSeconds;
            if (timerBack != null) timerBack.color = last ? Palette.Accent : Palette.StoneWhite;
            if (timer == null) return;
            float scale = 1;
            if (last)
            {
                // A beat just after each second ticks over: up to 14% bigger, back within a third of a second.
                float sinceTick = Mathf.Ceil(remaining) - remaining;
                scale = lastScale * Mathf.Lerp(1.14f, 1, Mathf.Clamp01((1 - sinceTick) / .35f));
                if (remaining <= 0) scale = lastScale;
            }
            timer.localScale = Vector3.one * scale;
        }

        void UpdateCountdown(WalkMatch.Phase phase)
        {
            bool counting = phase == WalkMatch.Phase.Countdown;
            bool go = phase == WalkMatch.Phase.Playing && match.PhaseTime < goSeconds;
            SetActive(countdown, counting || go);
            SetActive(countdownIntro, counting);
            if (!counting && !go) { lastCountdown = null; return; }
            string value = counting ? Mathf.CeilToInt(match.countdownSeconds - match.PhaseTime).ToString() : "GO!";
            if (value != lastCountdown && countdownNumber != null)
            {
                SetText(countdownNumber, value); lastCountdown = value;
                if (counting) Pop(countdownNumber.rectTransform, countdownScale, countdownPop); else pops.Remove(countdownNumber.rectTransform);
            }
            if (counting) { if (countdownNumber != null) countdownNumber.alpha = 1; PlacePins(); }
            else if (countdownNumber != null) Go(match.PhaseTime);
        }

        // GO! in four beats: it swells in, holds while growing very slightly, swells a little more
        // (the anticipation), then shrinks away fast and fades.
        void Go(float t)
        {
            float scale, alpha = 1;
            float outStart = goSeconds - goOut, swellEnd = outStart + goOut * .35f;
            if (t < goIn)
            {
                float u = t / goIn;
                scale = Mathf.Lerp(.3f, goSize, 1 - (1 - u) * (1 - u) * (1 - u));
            }
            else if (t < outStart) scale = goSize + goDrift * Mathf.InverseLerp(goIn, outStart, t);
            else if (t < swellEnd)
            {
                float u = Mathf.InverseLerp(outStart, swellEnd, t);
                scale = goSize + goDrift + goSwell * (1 - (1 - u) * (1 - u));
            }
            else
            {
                float u = Mathf.InverseLerp(swellEnd, goSeconds, t);
                scale = (goSize + goDrift + goSwell) * (1 - u * u);
                alpha = 1 - u;
            }
            countdownNumber.rectTransform.localScale = Vector3.one * Mathf.Max(0, scale);
            countdownNumber.alpha = alpha;
        }

        // Each pin hangs over its player's head, so a first-time player can see which figure is theirs.
        void PlacePins()
        {
            if (pins == null || input == null || input.players == null || view == null) return;
            var canvas = (RectTransform)transform;
            float bob = Mathf.Sin(Time.time * Mathf.PI / .6f) * 6;
            for (int i = 0; i < pins.Length && i < input.players.Length; i++)
            {
                if (pins[i] == null || input.players[i] == null) continue;
                Vector3 screen = view.WorldToScreenPoint(input.players[i].transform.position + Vector3.up * pinHeight);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var local))
                    pins[i].anchoredPosition = local + Vector2.up * (6 + bob);
            }
        }

        // For now the result floats over the board as text. The stones are to be stacked and counted
        // later (UI.md); the numbers will then move onto the stacks.
        void UpdateResult(bool entered)
        {
            int black = match.BlackCount, white = match.WhiteCount;
            if (entered)
            {
                SetText(resultTitle, black > white ? "BLACK WINS" : white > black ? "WHITE WINS" : "DRAW");
                SetText(resultBlack, black.ToString()); SetText(resultWhite, white.ToString());
                Fade(resultBlack, black < white ? .55f : 1); Fade(resultWhite, white < black ? .55f : 1);
                if (resultTitle != null) Pop(resultTitle.rectTransform);
            }
            SetActive(restartHint, match.PhaseTime >= match.restartLockSeconds);
        }

        // A bump from rest size: up by amount in the first third, a small dip under rest, then settled.
        void Pop(RectTransform rect, float rest = 1, float amount = .35f) { if (rect != null) pops[rect] = (Time.time, rest, amount); }

        void Animate()
        {
            List<RectTransform> done = null;
            foreach (var pair in pops)
            {
                if (pair.Key == null) { (done ??= new List<RectTransform>()).Add(pair.Key); continue; }
                var (start, rest, amount) = pair.Value;
                float t = (Time.time - start) / PopSeconds;
                if (t >= 1) { pair.Key.localScale = Vector3.one * rest; (done ??= new List<RectTransform>()).Add(pair.Key); continue; }
                float s = t < .3f ? Mathf.Sin(t / .3f * Mathf.PI * .5f) : Mathf.Cos((t - .3f) / .7f * Mathf.PI * 1.5f) * (1 - (t - .3f) / .7f);
                pair.Key.localScale = Vector3.one * rest * (1 + amount * s);
            }
            if (done != null) foreach (var rect in done) pops.Remove(rect);

            for (int i = deltas.Count - 1; i >= 0; i--)
            {
                var (text, born, from) = deltas[i];
                float t = (Time.time - born) / DeltaSeconds;
                if (text == null || t >= 1) { if (text != null) Destroy(text.gameObject); deltas.RemoveAt(i); continue; }
                text.rectTransform.anchoredPosition = from + Vector2.up * 30 * (1 - (1 - t) * (1 - t));
                text.alpha = 1 - t;
            }
        }

        static void Fade(TMP_Text text, float alpha) { if (text != null) text.alpha = alpha; }
        static void SetActive(GameObject target, bool on) { if (target != null && target.activeSelf != on) target.SetActive(on); }
        static void SetText(TMP_Text target, string value) { if (target != null && target.text != value) target.text = value; }
    }
}
