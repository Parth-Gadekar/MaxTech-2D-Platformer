using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MaxTech.UI
{
    /// Esc / gamepad Start toggles the pause menu and freezes time.
    /// Gameplay scripts should ignore input while PauseMenu.IsPaused is true.
    public class PauseMenu : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }
        public static event Action<bool> PauseChanged;

        [SerializeField] GameObject pauseOverlay;
        [Tooltip("Selected when the menu opens, so keyboard/gamepad navigation works.")]
        [SerializeField] Selectable firstSelected;
        [Tooltip("Scene loaded by Main Menu. Must be in Build Profiles > Scene List.")]
        [SerializeField] string mainMenuScene = "MainMenu";

        void Awake()
        {
            EnsureEventSystem();
            SetPaused(false);
        }

        void OnDestroy()
        {
            if (IsPaused) SetPaused(false); // never leave the game frozen
        }

        void Update()
        {
            if (RouletteMenu.IsOpen || RouletteMenu.LastClosedFrame == Time.frameCount) return;
            bool togglePressed =
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

            if (togglePressed) SetPaused(!IsPaused);
        }

        public void Resume() => SetPaused(false);

        public void Restart()
        {
            SetPaused(false);
            Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // Also works for scenes that aren't in the build list yet (like UI_Test).
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }

        public void LoadMainMenu()
        {
            if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
            {
                Debug.LogWarning($"PauseMenu: scene '{mainMenuScene}' isn't in Build Profiles yet.", this);
                return;
            }
            SetPaused(false);
            SceneManager.LoadScene(mainMenuScene);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void SetPaused(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (pauseOverlay != null) pauseOverlay.SetActive(paused);

            if (paused && firstSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);

            PauseChanged?.Invoke(paused);
        }

        // Lets the HUD prefab work in scenes that have no EventSystem.
        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsPaused = false;
            PauseChanged = null;
        }
    }
}
