using UnityEngine;

public class CraneSpawner : MonoBehaviour
{
    public GameObject platformPrefab;

    public Transform spawnPoint;
    public Transform liftPoint;
    public Transform dropPoint;

    public float respawnDelay = 2f;

    private bool waitingToSpawn;

    void Start()
    {
        SpawnPlatform();
    }

    void SpawnPlatform()
    {
        GameObject platform =
            Instantiate(
                platformPrefab,
                spawnPoint.position,
                Quaternion.identity);

        CranePlatform cp =
            platform.GetComponent<CranePlatform>();

        cp.Initialize(
            liftPoint,
            dropPoint,
            this);
    }

    public void PlatformDestroyed()
    {
        if (waitingToSpawn)
            return;

        waitingToSpawn = true;

        Invoke(nameof(Respawn), respawnDelay);
    }

    void Respawn()
    {
        waitingToSpawn = false;
        SpawnPlatform();
    }
}