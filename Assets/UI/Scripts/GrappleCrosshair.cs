using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MaxTech.UI
{
    /// Grapple crosshair: follows the mouse and shows whether a right-click grapple would work.
    /// Uses the same checks as GrapplingHook (range, NoGrapple tag, blockers, charges); its code is not changed.
    /// Hides the system cursor while playing and shows it again in menus.
    public class GrappleCrosshair : MonoBehaviour
    {
        [Tooltip("The crosshair shape. All images inside it get the state color.")]
        [SerializeField] RectTransform reticle;
        [Tooltip("Optional small marker drawn where the hook would attach.")]
        [SerializeField] RectTransform targetMarker;

        [Header("Colors")]
        [SerializeField] Color readyColor = new(0.78f, 0.6f, 0.29f);           // brass C79A4A
        [SerializeField] Color blockedColor = new(0.43f, 0.38f, 0.33f, 0.8f);  // grey 6E6253
        [SerializeField] Color noChargeColor = new(0.82f, 0.23f, 0.16f);       // red D23A2A

        [Header("Feel")]
        [SerializeField] float noChargeScale = 0.8f;
        [SerializeField] float blendSpeed = 15f;

        GrapplingHook hook;
        Camera cam;
        Graphic[] reticleGraphics;
        Color currentColor;
        bool cursorHidden;

        void Start()
        {
            hook = FindAnyObjectByType<GrapplingHook>();
            if (hook == null || reticle == null)
            {
                SetVisible(false); // scene without a grapple (e.g. UI_Test): keep the normal cursor
                enabled = false;
                return;
            }

            cam = hook.playerCamera != null ? hook.playerCamera : Camera.main;
            reticleGraphics = reticle.GetComponentsInChildren<Graphic>(true);
            currentColor = readyColor;
        }

        void OnDisable() => ShowSystemCursor(true);

        void Update()
        {
            bool inMenu = PauseMenu.IsPaused || RouletteFX.IsRouletteOpen || SettingsMenu.IsOpen
                          || GameOverScreen.IsShown || LevelCompleteScreen.IsShown;
            bool playing = !inMenu && Mouse.current != null && cam != null && hook.isActiveAndEnabled;

            ShowSystemCursor(!playing);
            SetVisible(playing);
            if (!playing) return;

            Vector2 mouse = Mouse.current.position.ReadValue();
            reticle.position = mouse; // Screen Space Overlay: UI position = screen pixels

            bool hasCharges = HUDEvents.MaxCharges < 0 || HUDEvents.Charges > 0;
            bool canHit = TryGetTarget(mouse, out Vector3 point);

            Color target = !hasCharges ? noChargeColor : canHit ? readyColor : blockedColor;
            float k = 1f - Mathf.Exp(-blendSpeed * Time.unscaledDeltaTime);
            currentColor = Color.Lerp(currentColor, target, k);
            foreach (Graphic g in reticleGraphics) g.color = currentColor;

            float scale = hasCharges ? 1f : noChargeScale;
            reticle.localScale = Vector3.Lerp(reticle.localScale, Vector3.one * scale, k);

            if (targetMarker != null)
            {
                bool showMarker = hasCharges && canHit;
                if (targetMarker.gameObject.activeSelf != showMarker) targetMarker.gameObject.SetActive(showMarker);
                if (showMarker) targetMarker.position = cam.WorldToScreenPoint(point);
            }
        }

        // Same checks as GrapplingHook.StartGrapple, without firing anything.
        bool TryGetTarget(Vector2 mouse, out Vector3 point)
        {
            point = default;
            Transform player = hook.transform;

            float depth = Mathf.Abs(cam.transform.position.z - player.position.z);
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, depth));

            Vector3 direction = world - player.position;
            direction.z = 0f;
            if (direction.sqrMagnitude < 0.001f) return false;
            direction.Normalize();

            if (!Physics.Raycast(player.position, direction, out RaycastHit hit, hook.maxDistance)) return false;
            if (hit.collider.CompareTag("NoGrapple")) return false;

            Vector3 toTarget = hit.point - player.position;
            float distance = toTarget.magnitude;
            if (distance <= 0.01f) return false;

            if (Physics.Raycast(player.position, toTarget.normalized, out RaycastHit blocker, distance - 0.02f)
                && blocker.collider != hit.collider)
                return false;

            point = hit.point;
            return true;
        }

        void SetVisible(bool visible)
        {
            if (reticle != null && reticle.gameObject.activeSelf != visible) reticle.gameObject.SetActive(visible);
            if (!visible && targetMarker != null && targetMarker.gameObject.activeSelf) targetMarker.gameObject.SetActive(false);
        }

        void ShowSystemCursor(bool show)
        {
            if (cursorHidden == !show) return;
            cursorHidden = !show;
            Cursor.visible = show;
        }
    }
}
