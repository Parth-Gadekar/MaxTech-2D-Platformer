using UnityEngine;
using UnityEngine.SceneManagement;

namespace MaxTech.UI
{
    /// End of the game: when the player walks into this box in the last level,
    /// the Level Complete screen opens in "Game Complete" mode (no next level).
    /// Lives on the HUD prefab so the level scene doesn't need editing.
    /// Select this object in the Scene view to see the box (green).
    public class GameEndZone : MonoBehaviour
    {
        [Tooltip("The zone only works in this scene.")]
        [SerializeField] string onlyInScene = "Level 2";
        [Tooltip("World position of the centre of the zone. Player plays at Z = 0.")]
        [SerializeField] Vector3 center;
        [SerializeField] Vector3 size = new Vector3(3f, 6f, 6f);

        Transform player;
        bool triggered;

        void Start()
        {
            if (SceneManager.GetActiveScene().name != onlyInScene)
            {
                enabled = false;
                return;
            }

            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        void Update()
        {
            if (triggered || player == null) return;
            if (!new Bounds(center, size).Contains(player.position)) return;

            triggered = true;
            if (!LevelCompleteScreen.TryShow(null, null))
                Debug.LogWarning("GameEndZone: no Level Complete screen in this scene.", this);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color32(0x4C, 0xAF, 0x50, 0xFF); // green 4CAF50
            Gizmos.DrawWireCube(center, size);
        }
    }
}
