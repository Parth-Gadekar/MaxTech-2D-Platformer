using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace MaxTech.UI
{
    /// Russian roulette recharge. E opens it (game freezes), the player loads 1–5 bullets,
    /// FIRE spins the cylinder. Miss = +bullets charges, hit = -1 life and -1 charge.
    /// This script only shows the choice and rolls the result. Core applies it via HUDEvents.RouletteResolved.
    [RequireComponent(typeof(CanvasGroup))]
    public class RouletteMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        /// Frame the menu closed on, so the same Esc press doesn't also open the pause menu.
        public static int LastClosedFrame { get; private set; } = -1;

        enum State { Closed, Choosing, Spinning, Result }
        const int Chambers = 6;

        [Header("Panel")]
        [Tooltip("Chamber_1 … Chamber_6, left to right.")]
        [SerializeField] Image[] chamberImages = new Image[Chambers];
        [SerializeField] TMP_Text bulletsText;
        [SerializeField] TMP_Text oddsText;
        [SerializeField] TMP_Text outcomeText;
        [Tooltip("Placeholder spin. Visual Feedback spins the real model on RouletteSpinStarted.")]
        [SerializeField] RectTransform cylinder;

        [Header("Result")]
        [SerializeField] Image blackout;
        [SerializeField] TMP_Text stamp;

        [Header("Rules")]
        [Range(1, 5)] [SerializeField] int maxBullets = 5;
        [SerializeField] int defaultBullets = 1;

        [Header("Look")]
        [SerializeField] Color loadedColor = new(0.78f, 0.6f, 0.29f); // brass
        [SerializeField] Color emptyColor = new(0.08f, 0.07f, 0.06f); // dark hole
        [SerializeField] Color missColor = new(0.78f, 0.6f, 0.29f);
        [SerializeField] Color hitColor = new(0.8f, 0.12f, 0.1f);

        [Header("Timing (real seconds)")]
        [SerializeField] float spinTime = 1f;
        [SerializeField] float spinTurns = 3f;
        [SerializeField] float resultHoldTime = 1f;
        [SerializeField] float fadeOutTime = 0.4f;

        CanvasGroup group;
        State state = State.Closed;
        int bullets;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            HideInstant();
        }

        void OnDisable()
        {
            if (IsOpen) Close(); // never leave the game frozen
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool interact = Pressed(kb?.eKey) || Pressed(pad?.buttonNorth);

            if (state == State.Closed)
            {
                if (interact && !PauseMenu.IsPaused) Open();
                return;
            }
            if (state != State.Choosing) return; // ignore input while spinning / showing result

            if (interact || Pressed(kb?.escapeKey) || Pressed(pad?.buttonEast))
            {
                Cancel();
                return;
            }

            if (Pressed(kb?.leftArrowKey) || Pressed(kb?.aKey) || Pressed(pad?.dpad.left)) SetBullets(bullets - 1);
            if (Pressed(kb?.rightArrowKey) || Pressed(kb?.dKey) || Pressed(pad?.dpad.right)) SetBullets(bullets + 1);

            if (kb != null)
                for (int n = 1; n <= maxBullets; n++)
                    if (kb[Key.Digit1 + (n - 1)].wasPressedThisFrame) SetBullets(n);

            if (Pressed(kb?.enterKey) || Pressed(kb?.spaceKey) || Pressed(pad?.buttonSouth)) Fire();
        }

        static bool Pressed(ButtonControl control) => control != null && control.wasPressedThisFrame;

        // ---------- Called by keys and by the UI buttons ----------

        public void Open()
        {
            if (state != State.Closed) return;

            state = State.Choosing;
            IsOpen = true;
            Time.timeScale = 0f;

            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            SetBullets(defaultBullets);
            HUDEvents.RaiseRouletteOpenChanged(true);
        }

        public void Cancel()
        {
            if (state == State.Choosing) Close();
        }

        public void SetBullets(int count)
        {
            if (state != State.Choosing) return;
            bullets = Mathf.Clamp(count, 1, maxBullets);
            Refresh();
        }

        public void Fire()
        {
            if (state != State.Choosing) return;
            StartCoroutine(FireSequence());
        }

        // ---------- Internals ----------

        void Refresh()
        {
            for (int i = 0; i < chamberImages.Length; i++)
                chamberImages[i].color = i < bullets ? loadedColor : emptyColor;

            int charges = Mathf.Max(0, HUDEvents.Charges);
            int maxCharges = HUDEvents.MaxCharges > 0 ? HUDEvents.MaxCharges : 6;
            int gain = Mathf.Clamp(maxCharges - charges, 0, bullets); // display only; Core decides the real rule
            string capNote = gain < bullets ? $" <size=75%>(max {maxCharges})</size>" : "";

            bulletsText.text = bullets == 1 ? "1 bullet" : $"{bullets} bullets";
            oddsText.text = $"Hit chance  {bullets}/{Chambers}  ({Mathf.RoundToInt(bullets * 100f / Chambers)}%)";
            outcomeText.text =
                $"<color=#C79A4A>Miss:</color> +{ChargeWord(gain)}{capNote}\n" +
                "<color=#D23A2A>Hit:</color> -1 life, -1 charge";
        }

        static string ChargeWord(int n) => n == 1 ? "1 charge" : $"{n} charges";

        IEnumerator FireSequence()
        {
            state = State.Spinning;
            group.interactable = false;
            HUDEvents.RaiseRouletteSpinStarted(bullets);

            // Game is frozen (timeScale 0), so everything here uses unscaled / realtime.
            for (float t = 0f; t < spinTime; t += Time.unscaledDeltaTime)
            {
                if (cylinder != null)
                    cylinder.localEulerAngles = new Vector3(0f, 0f, -360f * spinTurns * EaseOut(t / spinTime));
                yield return null;
            }
            if (cylinder != null) cylinder.localEulerAngles = Vector3.zero;

            bool hit = Random.Range(0, Chambers) < bullets; // bullets in 6 chambers
            state = State.Result;
            HUDEvents.RaiseRouletteResolved(bullets, hit);

            stamp.text = hit ? "BANG" : "CLICK";
            stamp.color = hit ? hitColor : missColor;
            stamp.gameObject.SetActive(true);
            if (hit) SetBlackout(1f);

            yield return new WaitForSecondsRealtime(resultHoldTime);

            for (float t = 0f; t < fadeOutTime; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / fadeOutTime;
                yield return null;
            }
            Close();
        }

        void Close()
        {
            StopAllCoroutines();
            HideInstant();
            state = State.Closed;

            if (!IsOpen) return;
            IsOpen = false;
            LastClosedFrame = Time.frameCount;
            Time.timeScale = 1f;
            HUDEvents.RaiseRouletteOpenChanged(false);
        }

        void HideInstant()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            stamp.gameObject.SetActive(false);
            SetBlackout(0f);
            if (cylinder != null) cylinder.localEulerAngles = Vector3.zero;
        }

        void SetBlackout(float alpha)
        {
            Color c = blackout.color;
            c.a = alpha;
            blackout.color = c;
        }

        static float EaseOut(float x)
        {
            x = Mathf.Clamp01(x);
            return 1f - (1f - x) * (1f - x);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsOpen = false;
            LastClosedFrame = -1;
        }
    }
}
