using System;
using UnityEngine;

namespace MaxTech.UI
{
    /// Link between gameplay and the HUD. Core System calls these; the HUD listens.
    ///   HUDEvents.SetLives(current, max);
    ///   HUDEvents.SetCharges(current, max);
    ///   HUDEvents.ReportRoulette(wasLive);
    public static class HUDEvents
    {
        public static event Action<int, int> LivesChanged;
        public static event Action<int, int> ChargesChanged;

        // Roulette (raised by RouletteMenu)
        public static event Action<bool> RouletteOpenChanged;    // true = opened (game frozen), false = closed
        public static event Action<int> RouletteSpinStarted;     // bullets loaded; Visual Feedback spins the model
        public static event Action<int, bool> RouletteResolved;  // bullets, hit; Core applies the rules

        // Last values sent, so UI that loads late still shows the right state.
        public static int Lives { get; private set; } = -1;
        public static int MaxLives { get; private set; } = -1;
        public static int Charges { get; private set; } = -1;
        public static int MaxCharges { get; private set; } = -1;

        // Gameplay scripts (movement, grapple) should ignore input while this is true.
        public static bool GameplayInputBlocked => PauseMenu.IsPaused || RouletteMenu.IsOpen;
    
        public static void SetLives(int current, int max)
        {
            MaxLives = Mathf.Max(0, max);
            Lives = Mathf.Clamp(current, 0, MaxLives);
            LivesChanged?.Invoke(Lives, MaxLives);
        }

        public static void SetCharges(int current, int max)
        {
            MaxCharges = Mathf.Max(0, max);
            Charges = Mathf.Clamp(current, 0, MaxCharges);
            ChargesChanged?.Invoke(Charges, MaxCharges);
        }

        public static void RaiseRouletteOpenChanged(bool open) => RouletteOpenChanged?.Invoke(open);
        public static void RaiseRouletteSpinStarted(int bullets) => RouletteSpinStarted?.Invoke(bullets);
        public static void RaiseRouletteResolved(int bullets, bool hit) => RouletteResolved?.Invoke(bullets, hit);


        // Clears old values between play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LivesChanged = null;
            ChargesChanged = null;
            RouletteOpenChanged = null;
            RouletteSpinStarted = null;
            RouletteResolved = null;
            Lives = MaxLives = Charges = MaxCharges = -1;
        }
    }
}
