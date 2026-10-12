
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSpawnManager : MonoBehaviour
{
    public static LevelSpawnManager Instance { get; private set; }

    private string targetSpawnPoint;
    private bool hasPendingSpawn;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadLevel(string sceneName, string spawnPointName)
    {
        targetSpawnPoint = spawnPointName;
        hasPendingSpawn = true;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        SceneManager.LoadScene(sceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!hasPendingSpawn)
            return;

        GameObject spawnPoint = GameObject.Find(targetSpawnPoint);
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (spawnPoint == null || player == null)
        {
            Debug.LogError(
                $"Could not find spawn point '{targetSpawnPoint}' or Player in {scene.name}."
            );
            return;
        }

        player.transform.position = spawnPoint.transform.position;

        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        hasPendingSpawn = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
