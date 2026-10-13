using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MaxTech.UI
{
    /// Red flash around the screen edges whenever the player loses a life.
    /// Creates itself when the game starts and listens to HUDEvents, so nothing needs to be set up.
    public class DamageFlash : MonoBehaviour
    {
        const float FlashAlpha = 0.85f;
        const float FlashTime = 0.6f;
        static readonly Color FlashColor = new Color32(0xD2, 0x3A, 0x2A, 0xFF); // blood red D23A2A

        static DamageFlash instance;

        Image image;
        int lastLives = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Create()
        {
            if (instance != null) return;

            var go = new GameObject("DamageFlash");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DamageFlash>();
            instance.Build();
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // above the HUD and menus, below the ScreenFader (1000)

            var go = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            image = go.GetComponent<Image>();
            image.sprite = MakeVignette(128);
            image.raycastTarget = false;
            image.color = Clear;

            HUDEvents.LivesChanged += OnLivesChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            HUDEvents.LivesChanged -= OnLivesChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // A new level starts with full lives; that's not damage.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            lastLives = -1;
            StopAllCoroutines();
            image.color = Clear;
        }

        void OnLivesChanged(int current, int max)
        {
            bool lost = lastLives >= 0 && current < lastLives;
            lastLives = current;
            if (!lost) return;

            StopAllCoroutines();
            StartCoroutine(Flash());
        }

        IEnumerator Flash()
        {
            for (float t = 0f; t < FlashTime; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / FlashTime;
                image.color = WithAlpha(FlashAlpha * k * k); // quick hit, slow fade
                yield return null;
            }
            image.color = Clear;
        }

        static Color Clear => WithAlpha(0f);
        static Color WithAlpha(float a) => new Color(FlashColor.r, FlashColor.g, FlashColor.b, a);

        // White texture: clear in the middle, solid at the edges. The Image color tints it red.
        static Sprite MakeVignette(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half, dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f; // 0 centre, 1 corner
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.95f, d));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
