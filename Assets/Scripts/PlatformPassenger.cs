
using UnityEngine;

public class PlatformPassenger : MonoBehaviour
{
    private Rigidbody playerRb;
    private Transform platformRoot;
    private Vector3 lastPlatformPosition;

    private void Awake()
    {
        platformRoot = transform.parent;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerRb = other.attachedRigidbody;

        if (playerRb == null)
            return;

        lastPlatformPosition = platformRoot.position;
    }

    private void FixedUpdate()
    {
        if (playerRb == null || platformRoot == null)
            return;

        Vector3 platformDelta =
            platformRoot.position - lastPlatformPosition;

        playerRb.MovePosition(
            playerRb.position + platformDelta
        );

        lastPlatformPosition = platformRoot.position;
    }

    private void OnTriggerExit(Collider other)
    {
        if (playerRb != null &&
            other.attachedRigidbody == playerRb)
        {
            playerRb = null;
        }
    }
}
