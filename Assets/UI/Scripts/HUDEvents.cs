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
        public static event Action<bool> RouletteFired; // true = live round, false = blank

        // Last values sent, so UI that loads late still shows the right state.
        public static int Lives { get; private set; } = -1;
        public static int MaxLives { get; private set; } = -1;
        public static int Charges { get; private set; } = -1;
        public static int MaxCharges { get; private set; } = -1;

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

        public static void ReportRoulette(bool wasLive) => RouletteFired?.Invoke(wasLive);

        // Clears old values between play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LivesChanged = null;
            ChargesChanged = null;
            RouletteFired = null;
            Lives = MaxLives = Charges = MaxCharges = -1;
        }
    }
}
