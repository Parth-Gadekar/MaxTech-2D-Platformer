using System;
using TMPro;
using UnityEngine;

namespace MaxTech.UI
{
    /// Small seconds timer. Counts up by default; tick Count Down for timed levels.
    /// Uses game time, so it stops automatically while paused.
    public class LevelTimer : MonoBehaviour
    {
        /// Fired once when a countdown reaches 0. Core can subscribe to fail the level.
        public static event Action TimeUp;

        [SerializeField] TMP_Text label;
        [SerializeField] bool countDown = false;
        [SerializeField] float startSeconds = 90f;
        [Tooltip("Countdown only: turns red and pulses at or below this many seconds.")]
        [SerializeField] float warningSeconds = 10f;
        [SerializeField] Color normalColor = new(0.91f, 0.86f, 0.75f);  // paper
        [SerializeField] Color warningColor = new(0.85f, 0.15f, 0.1f);  // red

        public float Seconds { get; private set; }
        public bool Running { get; set; } = true;

        int shownSeconds = -1;
        bool finished;

        void Start()
        {
            Seconds = countDown ? startSeconds : 0f;
            Refresh();
        }

        void Update()
        {
            if (Running && !finished)
            {
                Seconds += countDown ? -Time.deltaTime : Time.deltaTime;

                if (countDown && Seconds <= 0f)
                {
                    Seconds = 0f;
                    finished = true;
                    TimeUp?.Invoke();
                }
            }
            Refresh();
        }

        void Refresh()
        {
            // Countdown rounds up (shows 1 until it really hits 0); count-up rounds down.
            int whole = countDown ? Mathf.CeilToInt(Seconds) : Mathf.FloorToInt(Seconds);
            if (whole != shownSeconds)
            {
                shownSeconds = whole;
                label.text = $"{whole}<size=55%> s</size>";
            }

            bool warning = countDown && Seconds <= warningSeconds;
            label.color = warning ? warningColor : normalColor;

            float pulse = warning && !finished ? 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.time * 6f)) : 1f;
            label.rectTransform.localScale = Vector3.one * pulse;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => TimeUp = null;
    }
}
