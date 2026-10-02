using UnityEngine;
using UnityEngine.InputSystem;

namespace MaxTech.UI
{
    /// Fake values to test the HUD. 1/2 = lose/gain life, 3/4 = lose/gain charge.
    /// Remove once Core System calls HUDEvents.
    public class HUDDebugTester : MonoBehaviour
    {
        [SerializeField] int maxLives = 3;
        [SerializeField] int maxCharges = 6;
        [SerializeField] int startCharges = 3;

        int lives, charges;

        void Start()
        {
            lives = maxLives;
            charges = startCharges;
            Push();
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame) lives--;
            else if (kb.digit2Key.wasPressedThisFrame) lives++;
            else if (kb.digit3Key.wasPressedThisFrame) charges--;
            else if (kb.digit4Key.wasPressedThisFrame) charges++;
            else return;

            lives = Mathf.Clamp(lives, 0, maxLives);
            charges = Mathf.Clamp(charges, 0, maxCharges);
            Push();
        }

        void Push()
        {
            HUDEvents.SetLives(lives, maxLives);
            HUDEvents.SetCharges(charges, maxCharges);
        }
    }
}
