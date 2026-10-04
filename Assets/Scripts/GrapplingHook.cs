using UnityEngine;
using UnityEngine.InputSystem;

public class GrapplingHook : MonoBehaviour
{
    public Camera playerCamera;
    public Transform gunTip;
    public LineRenderer rope;

    public float maxDistance = 40f;
    public float grappleSpeed = 25f;
    public float stopDistance = 1f;

    private Rigidbody rb;

    private Vector3 grapplePoint;
    private bool isGrappling;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rope != null)
            rope.positionCount = 0;
    }

    void Update()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            StartGrapple();
        }
        if (Mouse.current.leftButton.wasPressedThisFrame && isGrappling)
        {
            StopGrapple();
        }

        DrawRope();
    }

    void FixedUpdate()
    {
        if (!isGrappling)
            return;

        Vector3 direction =
            (grapplePoint - transform.position).normalized;

        rb.linearVelocity = direction * grappleSpeed;

        if (Vector3.Distance(transform.position, grapplePoint)
            <= stopDistance)
        {
            StopGrapple();
        }
    }

    void StartGrapple()
    {
        Vector3 mousePos = Mouse.current.position.ReadValue();

        mousePos.z =
            Mathf.Abs(playerCamera.transform.position.z);

        Vector3 mouseWorld =
            playerCamera.ScreenToWorldPoint(mousePos);

        mouseWorld.z = 0f;

        Vector3 direction =
            (mouseWorld - transform.position).normalized;

        RaycastHit hit;

        if (Physics.Raycast(
            transform.position,
            direction,
            out hit,
            maxDistance))
        {
            if (hit.collider.CompareTag("NoGrapple"))
                return;

            Vector3 dirToTarget =
                (hit.point - transform.position).normalized;

            float distToTarget =
                Vector3.Distance(transform.position, hit.point);

            RaycastHit blocker;

            if (Physics.Raycast(
                transform.position,
                dirToTarget,
                out blocker,
                distToTarget))
            {
                if (blocker.collider != hit.collider)
                {
                    return;
                }
            }

            grapplePoint = hit.point;
            grapplePoint.z = 0f;

            isGrappling = true;

            if (rope != null)
            {
                rope.enabled = true;
                rope.positionCount = 2;
            }
        }
    }

    void StopGrapple()
    {
        isGrappling = false;

        rb.linearVelocity *= 0.5f;

        if (rope != null)
        {
            rope.positionCount = 0;
            rope.enabled = false;
        }
    }

    void DrawRope()
    {
        if (!isGrappling || rope == null)
            return;

        rope.SetPosition(0, gunTip.position);
        rope.SetPosition(1, grapplePoint);
    }

    private void OnCollisionEnter(Collision collision)
{
    if (isGrappling)
    {
        StopGrapple();
    }
}
}