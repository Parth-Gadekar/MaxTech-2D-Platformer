using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MaxTech.UI
{
    /// Game Over overlay. Shown when PlayerLives runs out: freezes the game,
    /// shows the level and how long the player survived, then Retry or Main Menu.
    /// Put this on an always-active object (the HUD root); the overlay itself starts hidden.
    public class GameOverScreen : MonoBehaviour
    {
        public static bool IsShown { get; private set; }

        [SerializeField] CanvasGroup overlay;
        [SerializeField] TMP_Text statsText;
        [Tooltip("Selected when the screen opens, so keyboard/gamepad navigation works.")]
        [SerializeField] Selectable firstSelected;
        [Tooltip("Scene loaded by the Main Menu button. Must be in Build Profiles > Scene List.")]
        [SerializeField] string mainMenuScene = "MainMenu";

        [Header("Timing (real seconds)")]
        [Tooltip("Lets the last hit (e.g. the roulette BANG) play before the screen appears.")]
        [SerializeField] float showDelay = 0.8f;
        [SerializeField] float fadeTime = 0.5f;

        void Awake()
        {
            IsShown = false;
            SetVisible(0f);
        }

        void OnEnable() => PlayerLives.OutOfLives += Show;
        void OnDisable() => PlayerLives.OutOfLives -= Show;

        void OnDestroy()
        {
            if (IsShown) Time.timeScale = 1f; // never leave the next scene frozen
            IsShown = false;
        }

        public void Show()
        {
            if (IsShown) return;
            IsShown = true;
            StartCoroutine(ShowRoutine());
        }

        IEnumerator ShowRoutine()
        {
            yield return new WaitForSecondsRealtime(showDelay);

            Time.timeScale = 0f;
            if (statsText != null) statsText.text = BuildStats();

            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                SetVisible(t / fadeTime);
                yield return null;
            }
            SetVisible(1f);

            if (firstSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
        }

        string BuildStats()
        {
            string level = SceneManager.GetActiveScene().name;
            LevelTimer timer = FindAnyObjectByType<LevelTimer>();
            return timer != null
                ? $"{level}   ·   Survived {Mathf.FloorToInt(timer.Seconds)} s"
                : level;
        }

        void SetVisible(float alpha)
        {
            if (overlay == null) return;
            overlay.alpha = alpha;
            overlay.blocksRaycasts = alpha > 0f;
            overlay.interactable = alpha >= 1f;
        }

        // ---------- Buttons ----------

        public void Retry()
        {
            Time.timeScale = 1f;
            IsShown = false;
            Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // Also works for scenes that aren't in the build list (like UI_Test).
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
                Debug.LogWarning($"GameOverScreen: scene '{mainMenuScene}' isn't in Build Profiles yet.", this);
                return;
            }
            Time.timeScale = 1f;
            IsShown = false;
            SceneManager.LoadScene(mainMenuScene);
        }

        // Right-click the component header in Play mode → "Test: Show Game Over".
        [ContextMenu("Test: Show Game Over")]
        void TestShow() => Show();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => IsShown = false;
    }
}
