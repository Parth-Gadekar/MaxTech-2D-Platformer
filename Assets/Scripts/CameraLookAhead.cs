using Unity.Cinemachine;
using UnityEngine;

/// Camera look-ahead for the 2.5D side view.
/// Shifts the camera toward where the player is going, but never past a nearby wall,
/// so the camera can't end up inside a building while the player is wall climbing.
/// Put this on the Player Camera (the object with the CinemachineFollow component).
[RequireComponent(typeof(CinemachineFollow))]
public class CameraLookAhead : MonoBehaviour
{
    [Tooltip("The player's Rigidbody. Empty = found in the parent.")]
    [SerializeField] Rigidbody player;

    [Header("Look ahead")]
    [Tooltip("How far (in units) the camera moves toward the direction the player is going.")]
    [SerializeField] float lookAhead = 3f;
    [Tooltip("How fast the camera slides to its new offset (seconds). Higher = smoother.")]
    [SerializeField] float smoothTime = 0.35f;

    [Header("Direction changes")]
    [Tooltip("The player must move at least this fast sideways to count as moving.")]
    [SerializeField] float minSpeedToTurn = 0.5f;
    [Tooltip("How long the player must keep moving the other way before the camera flips side.")]
    [SerializeField] float turnDelay = 0.25f;

    [Header("Walls")]
    [Tooltip("Gap the camera keeps from a wall in front of the player.")]
    [SerializeField] float wallMargin = 0.75f;
    [SerializeField] LayerMask wallLayers = ~0;

    CinemachineFollow follow;
    float facing = 1f;
    float turnTimer;
    float currentX;
    float velocityX;

    void Awake()
    {
        follow = GetComponent<CinemachineFollow>();
        if (player == null) player = GetComponentInParent<Rigidbody>();
        currentX = follow.FollowOffset.x;
    }

    void LateUpdate()
    {
        if (player == null) return;

        UpdateFacing();

        // How far can the camera go to that side before it would reach a wall?
        float room = FreeSpace(facing);
        float targetX = facing * Mathf.Min(lookAhead, room);

        currentX = Mathf.SmoothDamp(currentX, targetX, ref velocityX, smoothTime);

        Vector3 offset = follow.FollowOffset;
        offset.x = currentX;
        follow.FollowOffset = offset;
    }

    // Flip the look direction only after the player keeps moving the other way for a moment,
    // so quick wall-jump bounces don't swing the camera back and forth.
    void UpdateFacing()
    {
        float vx = player.linearVelocity.x;
        bool movingOtherWay = Mathf.Abs(vx) > minSpeedToTurn && Mathf.Sign(vx) != facing;

        if (movingOtherWay)
        {
            turnTimer += Time.deltaTime;
            if (turnTimer >= turnDelay)
            {
                facing = -facing;
                turnTimer = 0f;
            }
        }
        else
        {
            turnTimer = 0f;
        }
    }

    // Checks at the player's height and at the camera's height, and returns the free distance.
    float FreeSpace(float direction)
    {
        float maxCheck = lookAhead + wallMargin;
        float free = maxCheck;
        Vector3 dir = Vector3.right * direction;

        Vector3 low = player.position;
        Vector3 high = player.position + Vector3.up * follow.FollowOffset.y;

        if (Physics.Raycast(low, dir, out RaycastHit hitLow, maxCheck, wallLayers, QueryTriggerInteraction.Ignore))
            free = Mathf.Min(free, hitLow.distance);
        if (Physics.Raycast(high, dir, out RaycastHit hitHigh, maxCheck, wallLayers, QueryTriggerInteraction.Ignore))
            free = Mathf.Min(free, hitHigh.distance);

        return Mathf.Max(0f, free - wallMargin);
    }
}
