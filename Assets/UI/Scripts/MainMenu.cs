using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MaxTech.UI
{
    /// Main menu: Start Game, Controls, Settings (later), Quit.
    public class MainMenu : MonoBehaviour
    {
        [Tooltip("Scene loaded by Start Game. Must be in Build Profiles > Scene List.")]
        [SerializeField] string firstLevelScene = "Level 1";

        [Header("Panels")]
        [SerializeField] GameObject mainButtons;
        [SerializeField] GameObject controlsPanel;

        [Header("Selection (keyboard / gamepad)")]
        [SerializeField] Selectable firstSelected;      // Start button
        [SerializeField] Selectable controlsBackButton; // Back button in the controls panel

        void Awake()
        {
            // Arriving from a paused game: make sure nothing stays frozen or muted.
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            ShowControls(false);
        }

        void Start() => Select(firstSelected);

        void Update()
        {
            if (controlsPanel == null || !controlsPanel.activeSelf) return;

            bool back =
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

            if (back) CloseControls();
        }

        public void StartGame()
        {
            if (!Application.CanStreamedLevelBeLoaded(firstLevelScene))
            {
                Debug.LogWarning($"MainMenu: scene '{firstLevelScene}' isn't in Build Profiles > Scene List.", this);
                return;
            }
            ScreenFader.FadeOutThen(() => SceneManager.LoadScene(firstLevelScene));
        }

        public void OpenControls()
        {
            ShowControls(true);
            Select(controlsBackButton);
        }

        public void CloseControls()
        {
            ShowControls(false);
            Select(firstSelected);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ShowControls(bool show)
        {
            if (controlsPanel != null) controlsPanel.SetActive(show);
            if (mainButtons != null) mainButtons.SetActive(!show);
        }

        static void Select(Selectable target)
        {
            if (target != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
    }
}
