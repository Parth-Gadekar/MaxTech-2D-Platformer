using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class CharacterAnimationTest : MonoBehaviour
{
    private Animator animator;
    public PlayerMovement movementScript;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float aimThreshold = 0.5f;

    private bool jump;
    private bool land;
    private bool wasGrounded;
    private bool isRoulette;
    private bool Aim;

    void Start()
    {
        movementScript = GetComponentInParent<PlayerMovement>();
        wasGrounded = movementScript.isGrounded;
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
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

            // Jump animation
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                    animator.SetTrigger("Jump");
            }
        }

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            if(isRoulette)
            {
                CloseRoulette();
            }
            else
            {
                OpenRoulette();
            }
        }
        if (Keyboard.current != null &&
        Keyboard.current.spaceKey.wasPressedThisFrame &&
        isRoulette && isRoulette)
    {
        OnRouletteFinished();
    }


    if (movementScript.isGrounded && !wasGrounded)
    {
        animator.SetTrigger("Land");
    }

    wasGrounded = movementScript.isGrounded;

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

            if (Mouse.current != null &&
                Mouse.current.rightButton.wasPressedThisFrame)
            {
                Aim=!Aim;
                animator.SetBool("Aim",Aim);
            }
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
        animator.SetBool("isRoulette", false);
    }

   public void OnRouletteFinished()
    {
        CloseRoulette();
    }
}