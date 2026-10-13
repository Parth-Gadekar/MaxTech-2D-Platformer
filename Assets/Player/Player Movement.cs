
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float acceleration = 20f;
    public float deceleration = 30f;
    public float airControl = 0.25f;

    [Header("Jump")]
    public float jumpForce = 12f;

    [Tooltip("Total normal jumps per airtime. 2 = ground jump + double jump.")]
    [Min(1)]
    public int maxJumps = 2;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckDistance = 0.15f;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundLayer = ~0;

    [Header("Jump Feel")]
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;
    public float fallMultiplier = 3f;
    public float lowJumpMultiplier = 2f;
    public float maxFallSpeed = -20f;

    [Header("Apex")]
    public float apexThreshold = 1f;
    public float apexGravityMultiplier = 0.5f;

    [Header("Wall Detection")]
    public float wallCheckDistance = 0.6f;
    public LayerMask wallLayer = ~0;

    [Header("Wall Slide")]
    public float wallSlideSpeed = -3f;

    [Header("Wall Jump")]
    public float wallJumpForceX = 8f;
    public float wallJumpForceY = 12f;
    public float wallJumpLockTime = 0.15f;

    private Rigidbody rb;
    private GrapplingHook grapplingHook;

    public bool isGrounded { get; private set; }

    private bool wasGrounded;
    private bool isWallLeft;
    private bool isWallRight;
    private bool isWallSliding;
    private bool isWallJumping;

    // Normal jumps used. Wall jumps never modify this counter.
    private int jumpsUsed;

    private float coyoteCounter;
    private float jumpBufferCounter;
    private float wallJumpLockCounter;

    private float moveInput;
    private bool jumpPressed;
    private bool jumpHeld;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grapplingHook = GetComponent<GrapplingHook>();

        jumpsUsed = 0;
    }

    void Update()
    {
        ReadInput();

        if (grapplingHook != null && grapplingHook.IsGrappling)
        {
            jumpBufferCounter = 0f;
            jumpPressed = false;
            return;
        }

        CheckGround();
        CheckWall();
        UpdateWallJumpLock();
        HandleJumpInput();
    }

    void FixedUpdate()
    {
        if (grapplingHook != null && grapplingHook.IsGrappling)
            return;

        Move();
        BetterJump();
    }

    void ReadInput()
    {
        moveInput = 0f;
        jumpPressed = false;
        jumpHeld = false;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.aKey.isPressed)
            moveInput = -1f;
        else if (Keyboard.current.dKey.isPressed)
            moveInput = 1f;

        jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
        jumpHeld = Keyboard.current.spaceKey.isPressed;
    }

    void CheckGround()
    {
        bool groundedNow = false;

        if (groundCheck != null)
        {
            groundedNow = Physics.SphereCast(
                groundCheck.position,
                groundCheckRadius,
                Vector3.down,
                out _,
                groundCheckDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );
        }

        isGrounded = groundedNow;

        Debug.DrawRay(
            groundCheck != null ? groundCheck.position : transform.position,
            Vector3.down * groundCheckDistance,
            isGrounded ? Color.green : Color.red
        );

        // Reset normal jumps only while actually grounded and descending
        // or standing. This avoids resetting them while rising past an edge.
        if (isGrounded && rb.linearVelocity.y <= 0.1f)
        {
            jumpsUsed = 0;
            coyoteCounter = coyoteTime;
        }
        else
        {
            coyoteCounter = Mathf.Max(
                0f,
                coyoteCounter - Time.deltaTime
            );
        }

        wasGrounded = isGrounded;
    }

    void Move()
    {
        // Preserve the initial push away from the wall.
        if (isWallJumping)
            return;

        float targetSpeed = moveInput * moveSpeed;

        float accelRate;

        if (isGrounded)
        {
            accelRate = Mathf.Abs(targetSpeed) > 0.01f
                ? acceleration
                : deceleration;
        }
        else
        {
            accelRate = Mathf.Abs(targetSpeed) > 0.01f
                ? acceleration * airControl
                : deceleration * airControl;
        }

        float speedDifference = targetSpeed - rb.linearVelocity.x;
        float movement = speedDifference * accelRate;

        rb.AddForce(
            Vector3.right * movement,
            ForceMode.Force
        );

        Vector3 velocity = rb.linearVelocity;

        velocity.x = Mathf.Clamp(
            velocity.x,
            -moveSpeed,
            moveSpeed
        );

        // Keep the game constrained to its existing 2.5D plane.
        velocity.z = 0f;

        rb.linearVelocity = velocity;
    }

    void HandleJumpInput()
    {
        if (jumpPressed)
            jumpBufferCounter = jumpBufferTime;
        else
            jumpBufferCounter = Mathf.Max(
                0f,
                jumpBufferCounter - Time.deltaTime
            );

        if (jumpBufferCounter <= 0f)
            return;

        // Wall jump gets priority and consumes this jump press.
        if (!isGrounded && (isWallLeft || isWallRight))
        {
            bool pressingIntoWall =
                (isWallRight && moveInput > 0f) ||
                (isWallLeft && moveInput < 0f);

            if (pressingIntoWall && TryWallJump())
                return;
        }

        // Ground jump or coyote jump.
        if (isGrounded || coyoteCounter > 0f)
        {
            PerformNormalJump();

            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            return;
        }

        // Additional normal jumps, independent of wall jumps.
        if (jumpsUsed < maxJumps)
        {
            PerformNormalJump();
            jumpBufferCounter = 0f;
        }
    }

    void PerformNormalJump()
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;

        rb.AddForce(
            Vector3.up * jumpForce,
            ForceMode.Impulse
        );

        jumpsUsed++;
    }

    void CheckWall()
    {
        isWallRight = Physics.Raycast(
            transform.position,
            Vector3.right,
            wallCheckDistance,
            wallLayer,
            QueryTriggerInteraction.Ignore
        );

        isWallLeft = Physics.Raycast(
            transform.position,
            Vector3.left,
            wallCheckDistance,
            wallLayer,
            QueryTriggerInteraction.Ignore
        );

        bool pressingIntoWall =
            (isWallRight && moveInput > 0f) ||
            (isWallLeft && moveInput < 0f);

        isWallSliding =
            !isGrounded &&
            pressingIntoWall &&
            rb.linearVelocity.y < 0f;

        if (isWallSliding && !isWallJumping)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.y = Mathf.Max(
                velocity.y,
                wallSlideSpeed
            );

            rb.linearVelocity = velocity;
        }

        Debug.DrawRay(
            transform.position,
            Vector3.right * wallCheckDistance,
            isWallRight ? Color.green : Color.red
        );

        Debug.DrawRay(
            transform.position,
            Vector3.left * wallCheckDistance,
            isWallLeft ? Color.green : Color.red
        );
    }

    bool TryWallJump()
    {
        float direction = isWallRight ? -1f : 1f;

        isWallJumping = true;
        wallJumpLockCounter = wallJumpLockTime;

        Vector3 velocity = rb.linearVelocity;

        // Apply a predictable launch away from the wall.
        velocity.x = direction * wallJumpForceX;
        velocity.y = wallJumpForceY;
        velocity.z = 0f;

        rb.linearVelocity = velocity;

        // IMPORTANT:
        // Do not change jumpsUsed here.
        // Wall jumps are unlimited and independent of double jump.

        jumpBufferCounter = 0f;
        coyoteCounter = 0f;

        return true;
    }

    void UpdateWallJumpLock()
    {
        if (!isWallJumping)
            return;

        wallJumpLockCounter -= Time.deltaTime;

        if (wallJumpLockCounter <= 0f)
        {
            wallJumpLockCounter = 0f;
            isWallJumping = false;
        }
    }

    void BetterJump()
    {
        Vector3 velocity = rb.linearVelocity;

        if (Mathf.Abs(velocity.y) < apexThreshold)
        {
            rb.AddForce(
                Vector3.up *
                Physics.gravity.y *
                (apexGravityMultiplier - 1f),
                ForceMode.Acceleration
            );
        }

        velocity = rb.linearVelocity;

        if (velocity.y < 0f)
        {
            velocity.y += Physics.gravity.y *
                          (fallMultiplier - 1f) *
                          Time.fixedDeltaTime;
        }
        else if (velocity.y > 0f && !jumpHeld)
        {
            velocity.y += Physics.gravity.y *
                          (lowJumpMultiplier - 1f) *
                          Time.fixedDeltaTime;
        }

        velocity.y = Mathf.Max(velocity.y, maxFallSpeed);
        velocity.z = 0f;

        rb.linearVelocity = velocity;
    }
}
