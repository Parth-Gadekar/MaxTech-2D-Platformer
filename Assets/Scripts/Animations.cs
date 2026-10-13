using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Reflection;

public class CharacterAnimationTest : MonoBehaviour
{
    private Animator animator;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float aimThreshold = 0.5f;

    private bool jump;
    private bool land;
    private bool isRoulette;
    private bool Aim;
    private bool isGrounded;
    private bool wasGrounded;
    private bool hasBeenAirborne;
    private float airborneTimer;
    private bool rouletteActionStarted;

    [SerializeField] private float minimumAirborneTime = 0.2f;

    private PlayerMovement playerMovement;
    private FieldInfo groundedField;

    void Start()
    {
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
        playerMovement = GetComponentInParent<PlayerMovement>();

        if (playerMovement == null)
        {
            Debug.LogError("PlayerMovement not found on parent objects!");
            return;
        }

        groundedField = typeof(PlayerMovement).GetField(
            "isGrounded",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        if (groundedField == null)
        {
            Debug.LogError("Could not find isGrounded!");
        }
    }

    void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed)
            {
                transform.rotation = Quaternion.Euler(0, 90, 0);
            }
            else if (Keyboard.current.dKey.isPressed)
            {
                transform.rotation = Quaternion.Euler(0, 270, 0);
            }
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            if (!isRoulette)
            {
                OpenRoulette();
                rouletteActionStarted = false;
            }
            else
            {
                CloseRoulette();
            }
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (isRoulette && !rouletteActionStarted)
            {
                rouletteActionStarted = true;
            }
            else if (isRoulette && rouletteActionStarted)
            {
                OnRouletteFinished();
            }
            else
            {
                animator.ResetTrigger("Jump");
                animator.SetTrigger("Jump");
            }
        }

        if (groundedField != null && playerMovement != null)
        {
            isGrounded = (bool)groundedField.GetValue(playerMovement);

            if (!isGrounded)
            {
                hasBeenAirborne = true;
                airborneTimer += Time.deltaTime;
            }
            else
            {
                if (hasBeenAirborne &&
                    airborneTimer >= minimumAirborneTime)
                {
                    animator.ResetTrigger("Land");
                    animator.SetTrigger("Land");

                    Debug.Log("LAND TRIGGER FIRED");
                }

                hasBeenAirborne = false;
                airborneTimer = 0f;
            }

            wasGrounded = isGrounded;
        }

        if (animator == null || Keyboard.current == null)
            return;

        bool isWalking =
            Keyboard.current.aKey.isPressed ||
            Keyboard.current.dKey.isPressed;

        animator.SetBool("isWalking", isWalking);

        if (Mouse.current != null &&
            Mouse.current.rightButton.wasPressedThisFrame)
        {
            Debug.Log("Reached mouse check");

            Aim = !Aim;
            animator.SetBool("Aim", Aim);
        }
    }

    public void OpenRoulette()
    {
        isRoulette = true;
        animator.SetBool("isRoulette", true);
    }

    public void CloseRoulette()
    {
        isRoulette = false;
        rouletteActionStarted = false;
        animator.SetBool("isRoulette", false);
    }

    public void OnRouletteFinished()
    {
        CloseRoulette();
    }
}