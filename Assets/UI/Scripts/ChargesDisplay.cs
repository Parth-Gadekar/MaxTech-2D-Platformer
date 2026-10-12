using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MaxTech.UI
{
    /// Row of playing cards showing grapple charges. A card is "dealt" on gain and wobbles away on loss.
    public class ChargesDisplay : MonoBehaviour
    {
        [Tooltip("Left to right, one per charge slot.")]
        [SerializeField] List<Image> cards = new();
        [SerializeField] TMP_Text countText;
        [SerializeField] Color fullColor = new(0.91f, 0.86f, 0.75f);        // paper
        [SerializeField] Color emptyColor = new(0.18f, 0.16f, 0.14f, 0.6f); // faded slot

        [Header("Feedback")]
        [SerializeField] float gainDuration = 0.25f;
        [SerializeField] float lossDuration = 0.35f;
        [SerializeField] float lossWobbleDegrees = 18f;
        [SerializeField] Color lossFlashColor = new(0.85f, 0.15f, 0.1f);

        readonly List<float> gainTimers = new();
        readonly List<float> lossTimers = new();
        int shownCharges = -1;

        void OnEnable()
        {
            HUDEvents.ChargesChanged += Show;
            if (HUDEvents.MaxCharges >= 0) Show(HUDEvents.Charges, HUDEvents.MaxCharges);
        }

        void OnDisable() => HUDEvents.ChargesChanged -= Show;

        public void Show(int current, int max)
        {
            while (gainTimers.Count < cards.Count)
            {
                gainTimers.Add(0f);
                lossTimers.Add(0f);
            }

            for (int i = 0; i < cards.Count; i++)
            {
                bool full = i < current;
                cards[i].gameObject.SetActive(i < max);
                cards[i].color = full ? fullColor : emptyColor;
                SetSuitVisible(cards[i], full);

                if (shownCharges < 0) continue; // no animation on the very first update

                bool wasFull = i < shownCharges;
                if (full && !wasFull)
                {
                    gainTimers[i] = gainDuration;
                    lossTimers[i] = 0f;
                    cards[i].rectTransform.localEulerAngles = Vector3.zero;
                }
                else if (!full && wasFull)
                {
                    lossTimers[i] = lossDuration;
                    gainTimers[i] = 0f;
                    cards[i].rectTransform.localScale = Vector3.one;
                }
            }

            if (countText != null) countText.text = $"{current}/{max}";
            shownCharges = current;
        }

        static void SetSuitVisible(Image card, bool visible)
        {
            foreach (Transform child in card.transform) child.gameObject.SetActive(visible);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < gainTimers.Count; i++)
            {
                RectTransform rt = cards[i].rectTransform;

                if (gainTimers[i] > 0f)
                {
                    gainTimers[i] -= dt;
                    float t = 1f - Mathf.Clamp01(gainTimers[i] / gainDuration);
                    rt.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, 1f - (1f - t) * (1f - t));
                }

                if (lossTimers[i] > 0f)
                {
                    lossTimers[i] -= dt;
                    float k = Mathf.Clamp01(lossTimers[i] / lossDuration); // 1 → 0
                    rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(k * 40f) * lossWobbleDegrees * k);
                    cards[i].color = Color.Lerp(emptyColor, lossFlashColor, k);
                }
            }
        }
    }
}
