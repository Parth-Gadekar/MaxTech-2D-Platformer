using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MaxTech.UI
{
    /// Polish layer for Parth's RussianRouletteController. It finds the roulette at runtime
    /// and does not change its code. While the roulette is open:
    /// - the player is held in place (the level timer keeps running: roulette costs time)
    /// - the background is blurred
    /// - an info panel shows the steps, hit chance, reward and penalty
    /// On the result: CLICK / BANG, blackout on a hit, and the hit penalty (-1 life, -1 charge).
    public class RouletteFX : MonoBehaviour
    {
        public static bool IsRouletteOpen { get; private set; }

        [Header("Info panel")]
        [SerializeField] CanvasGroup infoPanel;
        [SerializeField] TMP_Text stepsText;
        [SerializeField] TMP_Text oddsText;
        [SerializeField] TMP_Text outcomeText;
        [SerializeField] TMP_Text promptText;

        [Header("Background blur")]
        [Tooltip("Global Volume (Depth of Field etc). Its weight fades in while the roulette is open.")]
        [SerializeField] Volume blurVolume;
        [SerializeField] float fadeTime = 0.25f;

        [Header("Result")]
        [SerializeField] TMP_Text stamp;
        [SerializeField] Image blackout;
        [SerializeField] float stampTime = 1.2f;
        [SerializeField] float stampPunchTime = 0.15f;
        [SerializeField] float blackoutHold = 0.6f;
        [SerializeField] float blackoutFade = 0.6f;
        [SerializeField] Color missColor = new(0.78f, 0.6f, 0.29f); // brass
        [SerializeField] Color hitColor = new(0.8f, 0.12f, 0.1f);   // blood red

        [Header("Hit penalty")]
        [SerializeField] int hitLifeLoss = 1;
        [SerializeField] int hitChargeLoss = 1;

        [Header("Timer")]
        [Tooltip("Off = the level timer keeps running during the roulette (it costs time). On = the timer stops while it is open.")]
        [SerializeField] bool pauseTimerDuringRoulette = false;

        const int Chambers = 6;
        const string Brass = "#C79A4A", Dim = "#6E6253", Red = "#D23A2A";

        RussianRouletteController controller;
        BarrelSpin3D barrel;
        BarrelLoadSequence loader;
        BulletSelectUI tray;
        PlayerLives lives;

        LevelTimer timer;
        bool timerWasRunning;
        bool timerHeld;

        PlayerMovement movement;
        GrapplingHook hook;
        Rigidbody playerBody;
        Vector3 savedVelocity;
        bool holding;

        bool wasOpen;
        bool loaded;
        float fade;
        Coroutine resultRoutine;

        void Awake()
        {
            if (infoPanel != null)
            {
                infoPanel.alpha = 0f;
                infoPanel.blocksRaycasts = false; // information only, the tray handles clicks
                infoPanel.interactable = false;
            }
            if (blurVolume != null) blurVolume.weight = 0f;
            if (stamp != null) stamp.gameObject.SetActive(false);
            SetBlackout(0f);
        }

        void Start()
        {
            controller = FindAnyObjectByType<RussianRouletteController>();
            if (controller == null)
            {
                enabled = false; // scene without the roulette (e.g. UI_Test)
                return;
            }

            barrel = FindAnyObjectByType<BarrelSpin3D>();
            loader = FindAnyObjectByType<BarrelLoadSequence>();
            tray = FindAnyObjectByType<BulletSelectUI>();
            lives = FindAnyObjectByType<PlayerLives>();
            timer = FindAnyObjectByType<LevelTimer>();
            movement = FindAnyObjectByType<PlayerMovement>();
            hook = FindAnyObjectByType<GrapplingHook>();
            if (movement != null) playerBody = movement.GetComponent<Rigidbody>();

            if (lives == null) Debug.LogWarning("RouletteFX: no PlayerLives found, a hit won't cost a life.", this);
            if (barrel != null) barrel.OnResult += HandleResult;
            if (loader != null) loader.OnLoadComplete += HandleLoadComplete;
        }

        void OnDestroy()
        {
            if (barrel != null) barrel.OnResult -= HandleResult;
            if (loader != null) loader.OnLoadComplete -= HandleLoadComplete;
            HoldPlayer(false);
            HoldTimer(false);
            IsRouletteOpen = false;
        }

        void Update()
        {
            bool open = controller.IsOpen;
            if (open != wasOpen)
            {
                wasOpen = open;
                IsRouletteOpen = open;
                loaded = false;
                HoldPlayer(open);
                HoldTimer(open && pauseTimerDuringRoulette);
            }

            fade = Mathf.MoveTowards(fade, open ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));
            if (blurVolume != null) blurVolume.weight = fade;
            if (infoPanel != null) infoPanel.alpha = fade;

            if (open) RefreshInfo();
        }

        // ---------- Player hold ----------

        // Stops the player without touching the movement scripts: they are switched off and the body is frozen.
        void HoldPlayer(bool hold)
        {
            if (hold == holding) return;
            holding = hold;

            if (movement != null) movement.enabled = !hold;
            if (hook != null) hook.enabled = !hold;

            if (playerBody == null) return;
            if (hold)
            {
                savedVelocity = playerBody.linearVelocity;
                playerBody.isKinematic = true;
            }
            else
            {
                playerBody.isKinematic = false;
                playerBody.linearVelocity = savedVelocity;
            }
        }

        // Restores the timer's previous state, so a timer stopped by something else (e.g. level end) stays stopped.
        void HoldTimer(bool hold)
        {
            if (timer == null || hold == timerHeld) return;
            timerHeld = hold;

            if (hold)
            {
                timerWasRunning = timer.Running;
                timer.Running = false;
            }
            else
            {
                timer.Running = timerWasRunning;
            }
        }

        // ---------- Info panel ----------

        void HandleLoadComplete() => loaded = true;

        void RefreshInfo()
        {
            if (tray == null) return;

            bool loading = loader != null && loader.IsLoading;
            bool spinning = !loading && barrel != null && barrel.IsBusy;
            bool afterLoad = loaded || loading || spinning;

            int bullets = afterLoad ? tray.LastLoadedBulletCount : tray.SelectedBulletCount;
            int step = (loaded || spinning) ? 3 : (loading || bullets > 0) ? 2 : 1;

            if (stepsText != null)
                stepsText.text = $"{Step(1, "SELECT", step)}    {Step(2, "LOAD", step)}    {Step(3, "SPIN", step)}";

            if (oddsText != null)
                oddsText.text = bullets == 0
                    ? "Each bullet you load is one more charge... and one more chance to die."
                    : $"{(bullets == 1 ? "1 bullet" : $"{bullets} bullets")}   ·   hit chance {bullets}/{Chambers} ({Mathf.RoundToInt(bullets * 100f / Chambers)}%)";

            if (outcomeText != null)
            {
                int gain = Mathf.Clamp(controller.MaxCharges - controller.GrapplingCharges, 0, bullets);
                string cap = gain < bullets ? $" <size=85%><color={Dim}>(max {controller.MaxCharges})</color></size>" : "";
                string reward = bullets == 0 ? "+1 charge per bullet" : $"+{ChargeWord(gain)}{cap}";
                outcomeText.text =
                    $"<color={Brass}>Miss:</color> {reward}\n" +
                    $"<color={Red}>Hit:</color> -{hitLifeLoss} life, -{ChargeWord(hitChargeLoss)}";
            }

            if (promptText != null)
            {
                bool spaceHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
                promptText.text =
                    spinning ? (spaceHeld ? "Release SPACE to fire!" : "Spinning...") :
                    loaded ? "Hold SPACE to spin, release to fire" :
                    loading ? "Loading the cylinder..." :
                    bullets > 0 ? "Press SPIN to load the cylinder" :
                    "Click bullets in the tray (max 5)   ·   R to close";
            }
        }

        static string Step(int number, string label, int current)
        {
            if (number < current) return $"<color={Dim}><s>{number}. {label}</s></color>";
            if (number == current) return $"<color={Brass}><b>{number}. {label}</b></color>";
            return $"<color={Dim}>{number}. {label}</color>";
        }

        static string ChargeWord(int n) => n == 1 ? "1 charge" : $"{n} charges";

        // ---------- Result ----------

        void HandleResult(bool isLive, int chamber)
        {
            if (isLive)
            {
                if (lives != null) lives.LoseLife(hitLifeLoss);
                controller.AddGrapplingCharge(-hitChargeLoss); // clamps at 0
            }
            // A blank's reward (+bullets charges) is already given by the controller.

            if (resultRoutine != null) StopCoroutine(resultRoutine);
            resultRoutine = StartCoroutine(ShowResult(isLive));
        }

        IEnumerator ShowResult(bool hit)
        {
            if (stamp != null)
            {
                stamp.text = hit ? "BANG" : "CLICK";
                stamp.color = hit ? hitColor : missColor;
                stamp.gameObject.SetActive(true);
            }
            if (hit) SetBlackout(1f);

            // Stamp punch: big to normal.
            for (float t = 0f; t < stampPunchTime; t += Time.unscaledDeltaTime)
            {
                if (stamp != null) stamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, t / stampPunchTime);
                yield return null;
            }
            if (stamp != null) stamp.rectTransform.localScale = Vector3.one;

            if (hit)
            {
                yield return new WaitForSecondsRealtime(blackoutHold);
                for (float t = 0f; t < blackoutFade; t += Time.unscaledDeltaTime)
                {
                    SetBlackout(1f - t / blackoutFade);
                    yield return null;
                }
                SetBlackout(0f);
            }

            float remaining = stampTime - stampPunchTime - (hit ? blackoutHold + blackoutFade : 0f);
            if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);

            if (stamp != null) stamp.gameObject.SetActive(false);
            resultRoutine = null;
        }

        void SetBlackout(float alpha)
        {
            if (blackout == null) return;
            Color c = blackout.color;
            c.a = alpha;
            blackout.color = c;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => IsRouletteOpen = false;
    }
}
