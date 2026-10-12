
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrapplingHook : MonoBehaviour
{
    public event Action<Vector3> OnHookAttached;
    public event Action<Vector3> OnHookMissed;
    public event Action OnHookReleased;

    [Header("References")]
    public Camera playerCamera;
    public Transform gunTip;
    public LineRenderer rope;

    [Header("Roulette Charges")]
    [SerializeField] private RussianRouletteController rouletteController;

    [Header("Grapple Movement")]
    public float maxDistance = 40f;
    public float grappleSpeed = 18f;
    public float grappleAcceleration = 35f;
    public float stopDistance = 1.25f;
    public float approachStrength = 5f;

    private Rigidbody rb;
    private Vector3 grapplePoint;
    private bool isGrappling;

    public bool IsGrappling => isGrappling;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rouletteController == null)
            rouletteController =
                FindAnyObjectByType<RussianRouletteController>();

        if (rope != null)
        {
            rope.positionCount = 0;
            rope.enabled = false;
        }
    }

    private void Start()
    {
        if (rouletteController == null)
            Debug.LogError("GrapplingHook: RussianRouletteController not found!");

        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.rightButton.wasPressedThisFrame && !isGrappling)
            StartGrapple();

        if (Mouse.current.leftButton.wasPressedThisFrame && isGrappling)
            StopGrapple();

        DrawRope();
    }

    private void FixedUpdate()
    {
        if (!isGrappling || rb == null)
            return;

        Vector3 toTarget = grapplePoint - rb.position;
        toTarget.z = 0f;

        float distance = toTarget.magnitude;

        if (distance <= stopDistance)
        {
            StopGrapple();
            return;
        }

        Vector3 direction = toTarget.normalized;

        // Reduce the target speed as the player approaches the grapple point.
        float targetSpeed = Mathf.Min(
            grappleSpeed,
            distance * approachStrength
        );

        Vector3 desiredVelocity = direction * targetSpeed;

        // Accelerate smoothly instead of instantly replacing the Rigidbody velocity.
        Vector3 newVelocity = Vector3.MoveTowards(
            rb.linearVelocity,
            desiredVelocity,
            grappleAcceleration * Time.fixedDeltaTime
        );

        newVelocity.z = 0f;
        rb.linearVelocity = newVelocity;
    }

    private void StartGrapple()
    {
        if (rouletteController == null || playerCamera == null)
            return;

        if (rouletteController.GrapplingCharges <= 0)
        {
            Debug.Log("No grappling charges remaining!");
            return;
        }

        Vector3 mousePosition = Mouse.current.position.ReadValue();

        // Convert the mouse position to a point on the player's 2.5D plane.
        float cameraDepth = Mathf.Abs(
            playerCamera.transform.position.z - transform.position.z
        );

        mousePosition.z = cameraDepth;

        Vector3 mouseWorld =
            playerCamera.ScreenToWorldPoint(mousePosition);

        mouseWorld.z = transform.position.z;

        Vector3 direction = mouseWorld - transform.position;
        direction.z = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        if (!Physics.Raycast(
            transform.position,
            direction,
            out RaycastHit hit,
            maxDistance))
        {
            OnHookMissed?.Invoke(
                transform.position + direction * maxDistance
            );
            return;
        }

        if (hit.collider.CompareTag("NoGrapple"))
        {
            OnHookMissed?.Invoke(hit.point);
            return;
        }

        // Validate that the chosen target is not behind another collider.
        Vector3 toTarget = hit.point - transform.position;
        float targetDistance = toTarget.magnitude;

        if (targetDistance <= 0.01f)
            return;

        if (Physics.Raycast(
            transform.position,
            toTarget.normalized,
            out RaycastHit blocker,
            targetDistance - 0.02f))
        {
            if (blocker.collider != hit.collider)
            {
                OnHookMissed?.Invoke(blocker.point);
                return;
            }
        }

        // Consume a charge only after a valid target has been found.
        if (!rouletteController.TryConsumeGrapplingCharge())
            return;

        grapplePoint = hit.point;
        grapplePoint.z = transform.position.z;

        isGrappling = true;

        OnHookAttached?.Invoke(grapplePoint);

        if (rope != null)
        {
            rope.enabled = true;
            rope.positionCount = 2;
        }
    }

    private void StopGrapple()
    {
        if (!isGrappling)
            return;

        isGrappling = false;

        // Retain some momentum instead of stopping abruptly.
        if (rb != null)
            rb.linearVelocity *= 0.65f;

        if (rope != null)
        {
            rope.positionCount = 0;
            rope.enabled = false;
        }

        OnHookReleased?.Invoke();
    }

    private void DrawRope()
    {
        if (!isGrappling || rope == null || gunTip == null)
            return;

        rope.SetPosition(0, gunTip.position);
        rope.SetPosition(1, grapplePoint);
    }

    private void OnDisable()
    {
        if (isGrappling)
            StopGrapple();
    }
}
