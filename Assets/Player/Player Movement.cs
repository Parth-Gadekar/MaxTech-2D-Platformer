
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
    [Min(1)]
    public int maxJumps = 2;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckDistance = 0.15f;

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
    public float wallCheckDistance = 1.2f;

    [Header("Wall Slide")]
    public float wallSlideSpeed = -3f;

    [Header("Wall Jump")]
    public float wallJumpForceX = 8f;
    public float wallJumpForceY = 12f;
    public float wallJumpBuffer = 0.2f;
    public float wallJumpLockTime = 0.15f;

    [Header("Landing")]
    public float minFallSpeedForLock = 9f;   // slower landings don't lock at all
    public float landLockMin = 0.06f;        // seconds, gentlest locked landing
    public float landLockMax = 0.18f; 
    
    [Header("Aiming")]
    [Range(0f, 1f)] public float aimMoveMultiplier = 0f;   // 0 = stand still while aiming
    public bool IsAiming { get; set; }       // seconds, hardest landing

    private float landLockCounter;
    private float peakFallSpeed;

    [Header("Player Audio")]
    public AudioClip jumpGrunt;
    public AudioClip normalRunning;
    public AudioClip metalRunning;

    [Range(0.05f, 1f)]
    public float footstepInterval = 0.35f;

    private AudioSource jumpAudioSource;
    private AudioSource runningAudioSource;
    private float footstepTimer;
    private AudioClip currentRunningClip;

    public Rigidbody rb;
    private GrapplingHook grapplingHook;

    public event System.Action<bool> OnJump;   // true = airborne (double) jump
    public float MoveInput => moveInput;
    public bool isGrounded { get; private set; }

    private bool wasGrounded;
    private bool isWallLeft;
    private bool isWallRight;
    private bool isTouchingWall;
    private bool isWallSliding;
    private bool isWallJumping;

    // Normal jumps remaining. Wall jumps never change this value.
    private int jumpsRemaining;

    private float coyoteCounter;
    private float jumpBufferCounter;
    private float wallJumpCounter;
    private float wallJumpLockCounter;

    private float moveInput;
    private bool jumpPressed;
    private bool jumpHeld;

    public bool IsWallSliding => isWallSliding;
    public int WallSide => isWallLeft ? -1 : (isWallRight ? 1 : 0);   // -1 wall on left, +1 on right
    public event System.Action OnWallJump;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        grapplingHook = GetComponent<GrapplingHook>();

        maxJumps = Mathf.Max(1, maxJumps);
        jumpsRemaining = maxJumps;

        // Separate sources allow jumping and running sounds
        // to play independently.
        jumpAudioSource = gameObject.AddComponent<AudioSource>();
        jumpAudioSource.playOnAwake = false;
        jumpAudioSource.spatialBlend = 0f;

        runningAudioSource = gameObject.AddComponent<AudioSource>();
        runningAudioSource.playOnAwake = false;
        runningAudioSource.spatialBlend = 0f;
        runningAudioSource.loop = true;
        runningAudioSource.volume = 0.5f;
    }

    void Update()
    {
        ReadInput();

        // Let the grappling hook control the player while pulling.
        if (grapplingHook != null && grapplingHook.IsGrappling)
        {
            jumpPressed = false;
            jumpBufferCounter = 0f;
            return;
        }

        CheckGround();
        UpdateFootsteps();
        CheckWall();
        WallJump();
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
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics.Raycast(
            groundCheck.position,
            Vector3.down,
            groundCheckDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        Debug.DrawRay(
            groundCheck.position,
            Vector3.down * groundCheckDistance,
            isGrounded ? Color.green : Color.red
        );

        if (isGrounded)
        {
            coyoteCounter = coyoteTime;

            if (!wasGrounded && rb.linearVelocity.y <= 0.1f)
            {
                jumpsRemaining = maxJumps;
                isWallJumping = false;

                // Lock the player briefly after a real fall
                float fallSpeed = -peakFallSpeed;
                if (fallSpeed >= minFallSpeedForLock)
                {
                    float t = Mathf.InverseLerp(minFallSpeedForLock, Mathf.Abs(maxFallSpeed), fallSpeed);
                    landLockCounter = Mathf.Lerp(landLockMin, landLockMax, t);
                }
                peakFallSpeed = 0f;
            }
        }
        else
        {
            coyoteCounter = Mathf.Max(0f, coyoteCounter - Time.deltaTime);

            // Remember how fast we were falling
            peakFallSpeed = Mathf.Min(peakFallSpeed, rb.linearVelocity.y);
        }

        wasGrounded = isGrounded;
    }

    void Move()
    {
        // Preserve the initial push away from the wall.
        // Brief stop after a landing so the player doesn't glide through the Land animation
        if (landLockCounter > 0f)
        {
            landLockCounter -= Time.fixedDeltaTime;

            Vector3 v = rb.linearVelocity;
            v.x = 0f;
            v.z = 0f;
            rb.linearVelocity = v;
            return;
        }
        
        if (isWallJumping)
            return;

        float speedLimit = IsAiming ? moveSpeed * aimMoveMultiplier : moveSpeed;
        float targetSpeed = moveInput * speedLimit;
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

        // Preserve the 2.5D movement plane.
        velocity.z = 0f;

        rb.linearVelocity = velocity;
    }

    void HandleJumpInput()
    {
        if (jumpPressed)
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter = Mathf.Max(
                0f,
                jumpBufferCounter - Time.deltaTime
            );
        }

        if (jumpBufferCounter <= 0f)
            return;

        // First normal jump: ground jump or coyote-time jump.
        // This branch is available only before any normal jump
        // has been consumed during the current airtime.
        if (jumpsRemaining == maxJumps &&
            (isGrounded || coyoteCounter > 0f))
        {
            PerformJump();
            OnJump?.Invoke(false);

            jumpsRemaining = maxJumps - 1;
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            return;
        }

        // Additional normal jumps, such as the double jump.
        if (jumpsRemaining > 0)
        {
            PerformJump();
            OnJump?.Invoke(true); 

            jumpsRemaining--;
            jumpBufferCounter = 0f;
        }
    }

    void PerformJump()
    {
        landLockCounter = 0f;
        if (jumpGrunt != null && jumpAudioSource != null)
        {
            jumpAudioSource.PlayOneShot(jumpGrunt);
        }

        float force = jumpForce;

        if (rb.linearVelocity.y < 0f)
        {
            force -= rb.linearVelocity.y;
        }

        rb.AddForce(
            Vector3.up * force,
            ForceMode.Impulse
        );
    }

    void BetterJump()
    {
        Vector3 velocity = rb.linearVelocity;

        if (velocity.y > 0f &&
            Mathf.Abs(velocity.y) < apexThreshold)
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

        if (velocity.y > 0f && !jumpHeld)
        {
            velocity.y += Physics.gravity.y *
                          (lowJumpMultiplier - 1f) *
                          Time.fixedDeltaTime;
        }

        velocity.y = Mathf.Max(velocity.y, maxFallSpeed);
        velocity.z = 0f;

        rb.linearVelocity = velocity;
    }

    void CheckWall()
    {
        isWallRight = Physics.Raycast(
            transform.position,
            Vector3.right,
            wallCheckDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        isWallLeft = Physics.Raycast(
            transform.position,
            Vector3.left,
            wallCheckDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        bool pressingIntoWall =
            (isWallRight && moveInput > 0f) ||
            (isWallLeft && moveInput < 0f);

        isTouchingWall = isWallLeft || isWallRight;

        isWallSliding =
            isTouchingWall &&
            pressingIntoWall &&
            !isGrounded &&
            rb.linearVelocity.y < 0f;

        if (isWallSliding && !isWallJumping)
        {
            wallJumpCounter = wallJumpBuffer;

            Vector3 velocity = rb.linearVelocity;
            velocity.y = Mathf.Max(
                velocity.y,
                wallSlideSpeed
            );

            rb.linearVelocity = velocity;
        }
        else
        {
            wallJumpCounter = Mathf.Max(
                0f,
                wallJumpCounter - Time.deltaTime
            );
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

    void WallJump()
    {
        if (jumpPressed && wallJumpCounter > 0f)
        {
            isWallJumping = true;
            wallJumpLockCounter = wallJumpLockTime;

            float direction = isWallLeft ? 1f : -1f;

            Vector3 velocity = rb.linearVelocity;
            velocity.x = direction * wallJumpForceX;
            velocity.y = wallJumpForceY;
            velocity.z = 0f;

            rb.linearVelocity = velocity;
            OnWallJump?.Invoke();
            OnJump?.Invoke(false);

            if (jumpGrunt != null && jumpAudioSource != null)
            {
                jumpAudioSource.PlayOneShot(jumpGrunt);
            }

            wallJumpCounter = 0f;
            coyoteCounter = 0f;

            // Consume this press so it cannot also trigger
            // a normal jump in HandleJumpInput().
            jumpPressed = false;
            jumpBufferCounter = 0f;

            // IMPORTANT:
            // Do not reset or decrement jumpsRemaining here.
            // Wall jumps are independent of normal jumps.
        }

        if (isWallJumping)
        {
            wallJumpLockCounter -= Time.deltaTime;

            if (wallJumpLockCounter <= 0f)
            {
                wallJumpLockCounter = 0f;
                isWallJumping = false;
            }
        }
    }

    void UpdateFootsteps()
    {
        bool moving = Mathf.Abs(moveInput) > 0.1f;

        // Stop running sounds while airborne, stationary,
        // or being pulled by the grappling hook.
        if (!isGrounded ||
            !moving ||
            (grapplingHook != null && grapplingHook.IsGrappling))
        {
            if (runningAudioSource != null)
                runningAudioSource.Stop();

            currentRunningClip = null;
            footstepTimer = 0f;
            return;
        }

        AudioClip desiredClip = normalRunning;

        // Detect the surface immediately beneath the player.
        if (groundCheck != null &&
            Physics.Raycast(
                groundCheck.position,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance + 0.2f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            string materialName = "";

            if (hit.collider.sharedMaterial != null)
            {
                materialName =
                    hit.collider.sharedMaterial.name.ToLower();
            }

            // Use a physics material named something like Metal.
            if (materialName.Contains("metal"))
            {
                desiredClip = metalRunning;
            }
        }

        if (desiredClip == null)
        {
            runningAudioSource.Stop();
            currentRunningClip = null;
            return;
        }

        // Keep the existing looping running-audio behavior.
        if (currentRunningClip != desiredClip ||
            !runningAudioSource.isPlaying)
        {
            runningAudioSource.clip = desiredClip;
            runningAudioSource.loop = true;
            runningAudioSource.Play();
            currentRunningClip = desiredClip;
        }
    }
}
