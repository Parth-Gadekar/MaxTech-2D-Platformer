using System;
using UnityEngine;

namespace MaxTech.UI
{
    /// Player lives (hearts on the HUD). Lives on the HUD prefab for now so every level has it;
    /// Core can move it onto the player later. Anything that hurts the player calls LoseLife().
    public class PlayerLives : MonoBehaviour
    {
        /// Fired once when lives reach 0 (for the Game Over screen).
        public static event Action OutOfLives;

        [SerializeField] int maxLives = 3;

        public int Lives { get; private set; }
        public int MaxLives => maxLives;

        void Start()
        {
            Lives = maxLives;
            Publish();
        }

        public void LoseLife(int amount = 1)
        {
            if (Lives <= 0) return;

            Lives = Mathf.Max(0, Lives - amount);
            Publish();

            if (Lives == 0)
            {
                Debug.Log("PlayerLives: out of lives.", this);
                OutOfLives?.Invoke();
            }
        }

        public void GainLife(int amount = 1)
        {
            Lives = Mathf.Min(maxLives, Lives + amount);
            Publish();
        }

        void Publish() => HUDEvents.SetLives(Lives, maxLives);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => OutOfLives = null;
    }
}
