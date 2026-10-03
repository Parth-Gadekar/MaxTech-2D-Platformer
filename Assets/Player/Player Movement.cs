using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float acceleration = 20f;
    public float deceleration = 30f;
    public float airControl = 0.25f;

    [Header("Jump")]
    public float jumpForce = 12f;
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

    [Header("Wall Slide")]
    public float wallSlideSpeed = -3f;

    private Rigidbody rb;

    private bool isGrounded;
    private bool wasGrounded;
    private bool isTouchingWall;

    private int jumpsRemaining;

    private float coyoteCounter;
    private float jumpBufferCounter;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        jumpsRemaining = maxJumps;
    }

    void Update()
    {
        CheckGround();
        CheckWall();
        HandleJumpInput();
    }

    void FixedUpdate()
    {
        Move();
        BetterJump();
    }

    void CheckGround()
    {
        isGrounded = Physics.Raycast(
            groundCheck.position,
            Vector3.down,
            groundCheckDistance
        );

        Debug.DrawRay(
            groundCheck.position,
            Vector3.down * groundCheckDistance,
            isGrounded ? Color.green : Color.red
        );

        if (isGrounded)
        {
            coyoteCounter = coyoteTime;

            if (!wasGrounded)
            {
                jumpsRemaining = maxJumps;
            }
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }

        wasGrounded = isGrounded;
    }

    void Move()
    {
        float x = Input.GetAxisRaw("Horizontal");

        float targetSpeed = x * moveSpeed;

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

        float speedDifference =
            targetSpeed - rb.linearVelocity.x;

        float movement =
            speedDifference * accelRate;

        rb.AddForce(
            movement * Vector3.right,
            ForceMode.Force
        );

        Vector3 vel = rb.linearVelocity;

        vel.x = Mathf.Clamp(
            vel.x,
            -moveSpeed,
            moveSpeed
        );

        rb.linearVelocity = vel;
    }

    void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        if (jumpBufferCounter > 0)
        {
            if (isGrounded || coyoteCounter > 0)
            {
                PerformJump();

                jumpsRemaining = maxJumps - 1;

                jumpBufferCounter = 0;
                coyoteCounter = 0;
            }
            else if (jumpsRemaining > 0)
            {
                PerformJump();

                jumpsRemaining--;

                jumpBufferCounter = 0;
            }
        }
    }

    void PerformJump()
    {
        float force = jumpForce;

        if (rb.linearVelocity.y < 0)
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
        if (Mathf.Abs(rb.linearVelocity.y) < apexThreshold)
        {
            rb.AddForce(
                Vector3.up *
                Physics.gravity.y *
                (apexGravityMultiplier - 1),
                ForceMode.Acceleration
            );
        }

        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up *
                                 Physics.gravity.y *
                                 (fallMultiplier - 1) *
                                 Time.fixedDeltaTime;
        }

        if (rb.linearVelocity.y > 0 &&
            !Input.GetKey(KeyCode.Space))
        {
            rb.linearVelocity += Vector3.up *
                                 Physics.gravity.y *
                                 (lowJumpMultiplier - 1) *
                                 Time.fixedDeltaTime;
        }

        if (rb.linearVelocity.y < maxFallSpeed)
        {
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                maxFallSpeed,
                0f
            );
        }
    }

    void CheckWall()
    {
        isTouchingWall =
            Physics.Raycast(
                transform.position,
                transform.right,
                0.3f
            )
            ||
            Physics.Raycast(
                transform.position,
                -transform.right,
                0.3f
            );

        if (
            isTouchingWall &&
            !isGrounded &&
            rb.linearVelocity.y < wallSlideSpeed
        )
        {
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                wallSlideSpeed,
                0f
            );
        }
    }
}