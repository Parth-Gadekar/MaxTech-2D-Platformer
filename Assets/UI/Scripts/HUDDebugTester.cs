using UnityEngine;
using UnityEngine.InputSystem;

namespace MaxTech.UI
{
    /// Fake gameplay values to test the HUD. Remove once Core System calls HUDEvents.
    /// 1/2 = lose/gain life, 3/4 = lose/gain charge. Applies roulette results the way Core should.
    public class HUDDebugTester : MonoBehaviour
    {
        [SerializeField] int maxLives = 3;
        [SerializeField] int maxCharges = 6;
        [SerializeField] int startCharges = 3;

        int lives, charges;

        void OnEnable() => HUDEvents.RouletteResolved += ApplyRoulette;
        void OnDisable() => HUDEvents.RouletteResolved -= ApplyRoulette;

        void Start()
        {
            lives = maxLives;
            charges = startCharges;
            Push();
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || HUDEvents.GameplayInputBlocked) return;

            if (kb.digit1Key.wasPressedThisFrame) lives--;
            else if (kb.digit2Key.wasPressedThisFrame) lives++;
            else if (kb.digit3Key.wasPressedThisFrame) charges--;
            else if (kb.digit4Key.wasPressedThisFrame) charges++;
            else return;

            Push();
        }

        // Example for Core's PlayerStats.
        void ApplyRoulette(int bullets, bool hit)
        {
            if (hit)
            {
                lives--;
                charges--;
            }
            else
            {
                charges += bullets; // anything above max is lost for now (team hasn't decided)
            }
            Push();
        }

        void Push()
        {
            lives = Mathf.Clamp(lives, 0, maxLives);
            charges = Mathf.Clamp(charges, 0, maxCharges);
            HUDEvents.SetLives(lives, maxLives);
            HUDEvents.SetCharges(charges, maxCharges);
        }
    }
}
