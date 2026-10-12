
using UnityEngine;

public class LevelTransition : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "Level 2";
    [SerializeField] private string spawnPointName = "Level2SpawnPoint";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        // Show the Level Complete screen first; its Continue button loads the next level.
        if (MaxTech.UI.LevelCompleteScreen.TryShow(nextSceneName, spawnPointName))
            return;

        if (LevelSpawnManager.Instance != null)
        {
            LevelSpawnManager.Instance.LoadLevel(
                nextSceneName,
                spawnPointName
            );
        }
        else
        {
            Debug.LogError("LevelSpawnManager instance not found!");
        }
    }
}