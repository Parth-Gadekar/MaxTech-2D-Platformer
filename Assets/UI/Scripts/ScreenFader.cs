using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MaxTech.UI
{
    /// Fades the screen to black before a scene loads and back in after it loads.
    /// Creates itself when the game starts and lives across scenes, so nothing needs to be set up
    /// in any scene. Use ScreenFader.FadeOutThen(() => SceneManager.LoadScene(...)) to load with a fade.
    public class ScreenFader : MonoBehaviour
    {
        const float FadeOutTime = 0.35f;
        const float FadeInTime = 0.5f;
        static readonly Color FadeColor = new Color32(0x0A, 0x08, 0x06, 0xFF); // warm black 0A0806

        static ScreenFader instance;

        CanvasGroup group;
        bool busy;

        public static bool IsFading => instance != null && instance.busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Create()
        {
            if (instance != null) return;

            var go = new GameObject("ScreenFader");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ScreenFader>();
            instance.Build();
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // above the HUD (10) and every menu

            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f; // the game starts black and fades in
            group.blocksRaycasts = true;
            group.interactable = false;

            var image = new GameObject("Black", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(transform, false);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image.GetComponent<Image>().color = FadeColor;

            SceneManager.sceneLoaded += OnSceneLoaded;

            // Fade in the first scene here too: with "Enter Play Mode Options" (no scene reload)
            // sceneLoaded doesn't fire when Play is pressed.
            StartCoroutine(Fade(1f, 0f, FadeInTime, null));
        }

        void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopAllCoroutines();
            StartCoroutine(Fade(1f, 0f, FadeInTime, null));
        }

        /// Fades to black, then runs the action (usually a scene load).
        /// Calls made while a fade-out is already running are ignored, so double clicks don't load twice.
        public static void FadeOutThen(Action action)
        {
            if (instance == null)
            {
                action?.Invoke();
                return;
            }
            if (instance.busy) return;

            instance.StopAllCoroutines();
            instance.StartCoroutine(instance.Fade(instance.group.alpha, 1f, FadeOutTime, action));
        }

        IEnumerator Fade(float from, float to, float time, Action after)
        {
            busy = after != null;
            group.blocksRaycasts = true; // no clicks while the screen is changing

            for (float t = 0f; t < time; t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f))
            {
                group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time));
                yield return null;
            }
            group.alpha = to;
            group.blocksRaycasts = to > 0f;

            busy = false;
            after?.Invoke();
        }
    }
}
