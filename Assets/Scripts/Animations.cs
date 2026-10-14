using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterAnimationTest : MonoBehaviour
{
    [SerializeField] PlayerMovement movementScript;
    [SerializeField] RussianRouletteController roulette;
    [SerializeField] float turnSpeed = 14f;
    [SerializeField] float walkThreshold = 0.5f;
    [SerializeField] Camera mainCamera;
    [SerializeField] float aimDeadZonePixels = 30f;   // ignore the cursor when it's almost directly above/below the player
    [SerializeField] bool faceWallWhileSliding = true;   // untick if the slide looks backwards

    Animator animator;
    bool aim;
    float facingY = 270f;

    void Awake()
    {
        animator = GetComponent<Animator>();

        if (mainCamera == null) mainCamera = Camera.main;
        if (movementScript == null)
            movementScript = GetComponentInParent<PlayerMovement>();

        if (roulette == null)
            roulette = FindAnyObjectByType<RussianRouletteController>();
    }

    void OnEnable()  { 
        if (movementScript != null){ 
            movementScript.OnJump += HandleJump;
            movementScript.OnWallJump += HandleWallJump;
        }
    }
    void OnDisable() { 
        if (movementScript != null) {
            movementScript.OnJump -= HandleJump;
            movementScript.OnWallJump -= HandleWallJump;
        }
    }

    void HandleWallJump()
    {
        animator.ResetTrigger("Jump");
        animator.ResetTrigger("DoubleJump");
        animator.SetTrigger("WallJump");

        // The player is pushed away from the wall, so face that way
        facingY = movementScript.WallSide < 0 ? 270f : 90f;
    }

    void HandleJump(bool airborne)
    {
        animator.ResetTrigger("Jump");
        animator.ResetTrigger("DoubleJump");
        animator.SetTrigger(airborne ? "DoubleJump" : "Jump");
    }

    void Update()
    {
        Vector3 v = movementScript.rb.linearVelocity;
        bool grounded = movementScript.isGrounded;

        // Everything comes from the real physics state
        animator.SetBool("isWalking", grounded && Mathf.Abs(v.x) > walkThreshold);
        animator.SetBool("isGrounded", grounded);
        animator.SetFloat("VerticalVelocity", v.y);
        animator.SetBool("isRoulette", roulette != null && roulette.IsOpen);

        float input = movementScript.MoveInput;

        if (aim)
        {
            FaceCursor();
        }
        else if (Mathf.Abs(input) > 0.1f)
        {
            facingY = input > 0f ? 270f : 90f;
        }

        bool sliding = movementScript.IsWallSliding;
        animator.SetBool("isWallSliding", sliding);

        if (sliding)
        {
            if (aim) SetAim(false);   // wall slide replaces the aim pose

            int side = movementScript.WallSide;
            if (side != 0)
            {
                bool faceRight = faceWallWhileSliding ? side > 0 : side < 0;
                facingY = faceRight ? 270f : 90f;
            }
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.Euler(0f, facingY, 0f),
            turnSpeed * Time.deltaTime
        );

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            SetAim(!aim);

        // Pressing A or D cancels aiming
        if (aim && Keyboard.current != null &&
            (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame))
            SetAim(false);
    }
    void SetAim(bool value)
    {
        aim = value;
        animator.SetBool("Aim", aim);
        movementScript.IsAiming = aim;
    }
    void FaceCursor()
    {
        if (mainCamera == null || Mouse.current == null) return;

        float playerX = mainCamera.WorldToScreenPoint(movementScript.transform.position).x;
        float cursorX = Mouse.current.position.ReadValue().x;
        float diff = cursorX - playerX;

        // Cursor almost straight above or below: keep the current facing
        if (Mathf.Abs(diff) < aimDeadZonePixels) return;

        facingY = diff > 0f ? 270f : 90f;
    }
}