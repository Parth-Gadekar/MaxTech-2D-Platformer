using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MaxTech.UI
{
    /// Level Complete overlay. The level-end trigger calls LevelCompleteScreen.TryShow(nextScene, spawnPoint);
    /// the game freezes and shows time, lives and charges. Continue loads the next level.
    /// Put this on an always-active object (the HUD root); the overlay itself starts hidden.
    public class LevelCompleteScreen : MonoBehaviour
    {
        public static bool IsShown { get; private set; }
        static LevelCompleteScreen instance;

        [SerializeField] CanvasGroup overlay;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text statsText;
        [Tooltip("Selected when the screen opens (Continue), so keyboard/gamepad navigation works.")]
        [SerializeField] Selectable firstSelected;
        [Tooltip("Scene loaded by the Main Menu button. Must be in Build Profiles > Scene List.")]
        [SerializeField] string mainMenuScene = "MainMenu";
        [SerializeField] float fadeTime = 0.5f;

        string nextScene;
        string nextSpawnPoint;

        /// Called by the level-end trigger. Returns false if this scene has no Level Complete screen,
        /// so the caller can load the next level directly instead.
        public static bool TryShow(string nextSceneName, string spawnPointName)
        {
            if (instance == null) return false;
            instance.Show(nextSceneName, spawnPointName);
            return true;
        }

        void Awake()
        {
            instance = this;
            IsShown = false;
            SetVisible(0f);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            if (IsShown) Time.timeScale = 1f; // never leave the next scene frozen
            IsShown = false;
        }

        public void Show(string nextSceneName, string spawnPointName)
        {
            if (IsShown || GameOverScreen.IsShown) return;
            IsShown = true;
            nextScene = nextSceneName;
            nextSpawnPoint = spawnPointName;
            StartCoroutine(ShowRoutine());
        }

        IEnumerator ShowRoutine()
        {
            LevelTimer timer = FindAnyObjectByType<LevelTimer>();
            if (timer != null) timer.Running = false;
            Time.timeScale = 0f;

            if (titleText != null) titleText.text = $"{SceneManager.GetActiveScene().name.ToUpper()} COMPLETE";
            if (statsText != null) statsText.text = BuildStats(timer);

            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                SetVisible(t / fadeTime);
                yield return null;
            }
            SetVisible(1f);

            if (firstSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
        }

        string BuildStats(LevelTimer timer)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (timer != null) parts.Add($"Time {Mathf.FloorToInt(timer.Seconds)} s");

            PlayerLives lives = FindAnyObjectByType<PlayerLives>();
            if (lives != null) parts.Add($"Lives {lives.Lives}/{lives.MaxLives}");

            if (HUDEvents.MaxCharges >= 0) parts.Add($"Charges {HUDEvents.Charges}/{HUDEvents.MaxCharges}");

            return string.Join("   ·   ", parts);
        }

        void SetVisible(float alpha)
        {
            if (overlay == null) return;
            overlay.alpha = alpha;
            overlay.blocksRaycasts = alpha > 0f;
            overlay.interactable = alpha >= 1f;
        }

        // ---------- Buttons ----------

        public void Continue()
        {
            if (string.IsNullOrEmpty(nextScene))
            {
                LoadMainMenu(); // last level: nothing comes next
                return;
            }

            ScreenFader.FadeOutThen(() =>
            {
                Time.timeScale = 1f;
                IsShown = false;

                if (LevelSpawnManager.Instance != null)
                    LevelSpawnManager.Instance.LoadLevel(nextScene, nextSpawnPoint);
                else
                    SceneManager.LoadScene(nextScene);
            });
        }

        public void Replay() => ScreenFader.FadeOutThen(() =>
        {
            Time.timeScale = 1f;
            IsShown = false;
            Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        });

        public void LoadMainMenu()
        {
            if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
            {
                Debug.LogWarning($"LevelCompleteScreen: scene '{mainMenuScene}' isn't in Build Profiles yet.", this);
                return;
            }
            ScreenFader.FadeOutThen(() =>
            {
                Time.timeScale = 1f;
                IsShown = false;
                SceneManager.LoadScene(mainMenuScene);
            });
        }

        // Right-click the component header in Play mode → "Test: Show Level Complete".
        [ContextMenu("Test: Show Level Complete")]
        void TestShow() => Show("Level 2", "Level2SpawnPoint");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsShown = false;
            instance = null;
        }
    }
}
