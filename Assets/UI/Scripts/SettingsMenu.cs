using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MaxTech.UI
{
    /// Settings window: master / music / SFX volume and fullscreen.
    /// Values are saved (PlayerPrefs) and applied again in every scene that has this component.
    /// Opens on top of the current menu; Back or Esc closes it.
    /// Put this on an always-active object; the window itself starts hidden.
    public class SettingsMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        /// Frame the window closed on, so the same Esc press doesn't also toggle the pause menu.
        public static int LastClosedFrame { get; private set; } = -1;

        [SerializeField] GameObject window;

        [Header("Audio")]
        [Tooltip("Mixer with exposed parameters MasterVolume, MusicVolume and SFXVolume. " +
                 "Without a mixer, only Master works (it sets the overall volume).")]
        [SerializeField] AudioMixer mixer;
        [SerializeField] Slider masterSlider;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] TMP_Text masterValue;
        [SerializeField] TMP_Text musicValue;
        [SerializeField] TMP_Text sfxValue;

        [Header("Display")]
        [SerializeField] Toggle fullscreenToggle;

        [Header("Selection (keyboard / gamepad)")]
        [Tooltip("Selected when the window opens.")]
        [SerializeField] Selectable firstSelected;
        [Tooltip("Selected again after closing, e.g. the Settings button that opened it.")]
        [SerializeField] Selectable returnSelected;

        const string MasterKey = "settings.masterVolume";
        const string MusicKey = "settings.musicVolume";
        const string SfxKey = "settings.sfxVolume";

        void Awake()
        {
            if (masterSlider != null) masterSlider.onValueChanged.AddListener(v => SetVolume(MasterKey, "MasterVolume", v, masterValue));
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(v => SetVolume(MusicKey, "MusicVolume", v, musicValue));
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(v => SetVolume(SfxKey, "SFXVolume", v, sfxValue));
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(on => Screen.fullScreen = on);

            SetWindow(false);
        }

        // Applied in Start, not Awake: AudioMixer ignores SetFloat during Awake.
        void Start()
        {
            LoadSlider(masterSlider, MasterKey, "MasterVolume", masterValue);
            LoadSlider(musicSlider, MusicKey, "MusicVolume", musicValue);
            LoadSlider(sfxSlider, SfxKey, "SFXVolume", sfxValue);
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        }

        void OnDestroy()
        {
            if (IsOpen) IsOpen = false;
        }

        void Update()
        {
            if (!IsOpen) return;

            bool back =
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

            if (back) Close();
        }

        // ---------- Buttons ----------

        public void Open()
        {
            SetWindow(true);
            Select(firstSelected);
        }

        public void Close()
        {
            if (!IsOpen) return;
            SetWindow(false);
            LastClosedFrame = Time.frameCount;
            PlayerPrefs.Save();
            Select(returnSelected);
        }

        // ---------- Internals ----------

        void SetWindow(bool open)
        {
            IsOpen = open;
            if (window != null) window.SetActive(open);
        }

        void LoadSlider(Slider slider, string key, string parameter, TMP_Text label)
        {
            float value = PlayerPrefs.GetFloat(key, 0.8f);
            if (slider != null) slider.SetValueWithoutNotify(value);
            ApplyVolume(parameter, value);
            ShowPercent(label, value);
        }

        void SetVolume(string key, string parameter, float value, TMP_Text label)
        {
            PlayerPrefs.SetFloat(key, value);
            ApplyVolume(parameter, value);
            ShowPercent(label, value);
        }

        void ApplyVolume(string parameter, float value)
        {
            if (mixer != null)
            {
                // Sliders are 0..1; the mixer works in decibels (0 dB = full, -80 dB = silent).
                float db = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
                mixer.SetFloat(parameter, db);
            }
            else if (parameter == "MasterVolume")
            {
                AudioListener.volume = value;
            }
        }

        static void ShowPercent(TMP_Text label, float value)
        {
            if (label != null) label.text = $"{Mathf.RoundToInt(value * 100f)} %";
        }

        static void Select(Selectable target)
        {
            if (target != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsOpen = false;
            LastClosedFrame = -1;
        }
    }
}
