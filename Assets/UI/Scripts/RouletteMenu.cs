using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace MaxTech.UI
{
    /// Russian roulette recharge. E opens it (game freezes) and shows the 3D revolver barrel.
    /// 1. Choose 1–5 bullets   2. LOAD: bullets drop into the barrel   3. Hold SPACE to spin, release to fire.
    /// The barrel decides the result (the chamber that stops at the top). Core applies it via HUDEvents.RouletteResolved.
    [RequireComponent(typeof(CanvasGroup))]
    public class RouletteMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        /// Frame the menu closed on, so the same Esc press doesn't also open the pause menu.
        public static int LastClosedFrame { get; private set; } = -1;

        enum State { Closed, Choosing, Loading, Ready, Spinning, Result }
        const int Chambers = 6;

        [Header("3D barrel")]
        [Tooltip("RouletteStage prefab (barrel + camera + light). Spawned far away from the level.")]
        [SerializeField] GameObject stagePrefab;
        [SerializeField] Vector3 stagePosition = new(0f, -1000f, 0f);

        [Header("Panel")]
        [Tooltip("Chamber_1 … Chamber_6, left to right.")]
        [SerializeField] Image[] chamberImages = new Image[Chambers];
        [SerializeField] TMP_Text bulletsText;
        [SerializeField] TMP_Text oddsText;
        [SerializeField] TMP_Text outcomeText;
        [SerializeField] TMP_Text promptText;
        [Tooltip("Text on the FIRE button. Shows LOAD, then SPIN.")]
        [SerializeField] TMP_Text fireLabel;

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
        [Tooltip("How long the FIRE button 'holds' the spin when clicked with the mouse.")]
        [SerializeField] float autoSpinChargeTime = 0.7f;
        [SerializeField] float resultHoldTime = 1f;
        [SerializeField] float fadeOutTime = 0.4f;

        CanvasGroup group;
        State state = State.Closed;
        int bullets;
        bool charging;

        GameObject stage;
        BarrelSpin3D barrel;
        BarrelLoadSequence loader;
        Camera stageCamera;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            SpawnStage();
            HideInstant();
        }

        void OnDisable()
        {
            if (IsOpen) Close(); // never leave the game frozen
        }

        void OnDestroy()
        {
            if (barrel != null) barrel.OnResult -= HandleResult;
            if (loader != null) loader.OnLoadComplete -= HandleLoadComplete;
            if (stage != null) Destroy(stage);
        }

        void SpawnStage()
        {
            if (stagePrefab == null)
            {
                Debug.LogWarning("RouletteMenu: Stage Prefab is not assigned.", this);
                return;
            }

            stage = Instantiate(stagePrefab, stagePosition, Quaternion.identity);
            barrel = stage.GetComponentInChildren<BarrelSpin3D>();
            loader = stage.GetComponentInChildren<BarrelLoadSequence>();
            stageCamera = stage.GetComponentInChildren<Camera>();

            if (stageCamera != null) stageCamera.enabled = false; // only render while the menu is open
            if (barrel != null) barrel.OnResult += HandleResult;
            if (loader != null) loader.OnLoadComplete += HandleLoadComplete;
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool interact = Pressed(kb?.eKey) || Pressed(pad?.buttonNorth);
            bool firePressed = Pressed(kb?.enterKey) || Pressed(kb?.spaceKey) || Pressed(pad?.buttonSouth);
            bool fireReleased = Released(kb?.enterKey) || Released(kb?.spaceKey) || Released(pad?.buttonSouth);

            switch (state)
            {
                case State.Closed:
                    if (interact && !PauseMenu.IsPaused) Open();
                    break;

                case State.Choosing:
                    if (interact || Pressed(kb?.escapeKey) || Pressed(pad?.buttonEast))
                    {
                        Cancel();
                        break;
                    }
                    if (Pressed(kb?.leftArrowKey) || Pressed(kb?.aKey) || Pressed(pad?.dpad.left)) SetBullets(bullets - 1);
                    if (Pressed(kb?.rightArrowKey) || Pressed(kb?.dKey) || Pressed(pad?.dpad.right)) SetBullets(bullets + 1);
                    if (kb != null)
                        for (int n = 1; n <= maxBullets; n++)
                            if (kb[Key.Digit1 + (n - 1)].wasPressedThisFrame) SetBullets(n);
                    if (firePressed) Fire();
                    break;

                case State.Ready:
                    if (firePressed) BeginSpin();
                    break;

                case State.Spinning:
                    if (charging && fireReleased) ReleaseSpin();
                    break;
            }
        }

        static bool Pressed(ButtonControl c) => c != null && c.wasPressedThisFrame;
        static bool Released(ButtonControl c) => c != null && c.wasReleasedThisFrame;

        // ---------- Called by keys and by the UI buttons ----------

        public void Open()
        {
            if (state != State.Closed) return;

            state = State.Choosing;
            IsOpen = true;
            Time.timeScale = 0f;
            if (stageCamera != null) stageCamera.enabled = true;

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

            // Live preview: show that many bullets in the 3D barrel.
            if (barrel != null && !barrel.IsBusy)
            {
                barrel.SetLiveChambers(bullets);
                barrel.LoadChambers(true);
            }
            Refresh();
        }

        /// FIRE button: LOAD while choosing, SPIN when loaded.
        public void Fire()
        {
            if (state == State.Choosing) StartLoad();
            else if (state == State.Ready) StartCoroutine(AutoSpin());
        }

        // ---------- Flow ----------

        void StartLoad()
        {
            if (barrel == null || loader == null) return;

            state = State.Loading;
            group.interactable = false;
            SetPrompt("Loading...");
            barrel.SetLiveChambers(bullets);
            loader.PlayLoad(); // ends with HandleLoadComplete
        }

        void HandleLoadComplete()
        {
            if (state != State.Loading) return;

            state = State.Ready;
            group.interactable = true; // FIRE button now means SPIN (chambers ignore clicks outside Choosing)
            if (fireLabel != null) fireLabel.text = "SPIN";
            SetPrompt("Hold SPACE to spin, release to fire");
        }

        void BeginSpin()
        {
            if (barrel == null || charging) return;

            state = State.Spinning;
            charging = true;
            group.interactable = false;
            SetPrompt("Release to fire!");
            barrel.BeginCharge();
            HUDEvents.RaiseRouletteSpinStarted(bullets);
        }

        void ReleaseSpin()
        {
            if (!charging) return;
            charging = false;
            SetPrompt("");
            barrel.ReleaseCharge(); // ends with HandleResult
        }

        IEnumerator AutoSpin()
        {
            BeginSpin();
            yield return new WaitForSecondsRealtime(autoSpinChargeTime);
            ReleaseSpin();
        }

        void HandleResult(bool isLive, int chamber)
        {
            if (state != State.Spinning) return;
            StartCoroutine(ResultSequence(isLive));
        }

        IEnumerator ResultSequence(bool hit)
        {
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
            charging = false;
            HideInstant();
            state = State.Closed;
            if (stageCamera != null) stageCamera.enabled = false;

            if (!IsOpen) return;
            IsOpen = false;
            LastClosedFrame = Time.frameCount;
            Time.timeScale = 1f;
            HUDEvents.RaiseRouletteOpenChanged(false);
        }

        // ---------- Display ----------

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

            if (fireLabel != null) fireLabel.text = "LOAD";
            SetPrompt("Choose your bullets");
        }

        static string ChargeWord(int n) => n == 1 ? "1 charge" : $"{n} charges";

        void SetPrompt(string text)
        {
            if (promptText != null) promptText.text = text;
        }

        void HideInstant()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            stamp.gameObject.SetActive(false);
            SetBlackout(0f);
            SetPrompt("");
        }

        void SetBlackout(float alpha)
        {
            Color c = blackout.color;
            c.a = alpha;
            blackout.color = c;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsOpen = false;
            LastClosedFrame = -1;
        }
    }
}
