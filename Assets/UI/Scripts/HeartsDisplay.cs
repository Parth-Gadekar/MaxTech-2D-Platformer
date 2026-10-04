using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MaxTech.UI
{
    /// Row of hearts showing lives. Clones the first heart if max lives needs more.
    public class HeartsDisplay : MonoBehaviour
    {
        [Tooltip("Left to right. The first one is cloned if more are needed.")]
        [SerializeField] List<Image> hearts = new();
        [SerializeField] Color fullColor = new(0.70f, 0.12f, 0.10f);  // oxblood
        [SerializeField] Color emptyColor = new(0.18f, 0.16f, 0.14f); // dark iron

        [Header("Feedback")]
        [SerializeField] float punchScale = 1.4f;
        [SerializeField] float punchDuration = 0.3f;

        readonly List<float> punchTimers = new();
        int shownLives = -1;

        void OnEnable()
        {
            HUDEvents.LivesChanged += Show;
            if (HUDEvents.MaxLives >= 0) Show(HUDEvents.Lives, HUDEvents.MaxLives);
        }

        void OnDisable() => HUDEvents.LivesChanged -= Show;

        public void Show(int current, int max)
        {
            if (hearts.Count == 0) return;

            while (hearts.Count < max)
            {
                Image clone = Instantiate(hearts[0], hearts[0].transform.parent);
                clone.name = $"Heart_{hearts.Count}";
                hearts.Add(clone);
            }
            while (punchTimers.Count < hearts.Count) punchTimers.Add(0f);

            for (int i = 0; i < hearts.Count; i++)
            {
                bool full = i < current;
                hearts[i].gameObject.SetActive(i < max);
                hearts[i].color = full ? fullColor : emptyColor;

                // Punch hearts that just changed (not on the very first update).
                if (shownLives >= 0 && full != (i < shownLives)) punchTimers[i] = punchDuration;
            }
            shownLives = current;
        }

        void Update()
        {
            for (int i = 0; i < punchTimers.Count; i++)
            {
                if (punchTimers[i] <= 0f) continue;

                punchTimers[i] -= Time.unscaledDeltaTime;
                float t = 1f - Mathf.Clamp01(punchTimers[i] / punchDuration);
                hearts[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(punchScale, 1f, t * t * (3f - 2f * t));
            }
        }
    }
}
