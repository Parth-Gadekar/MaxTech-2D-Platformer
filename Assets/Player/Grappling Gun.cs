using UnityEngine;
using System.Collections.Generic;

public class GrapplingHook : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public Transform gunTip;
    public LineRenderer rope;
    public Rigidbody rb;

    [Header("Grapple Settings")]
    public float maxDistance = 50f;
    public float grappleSpeed = 35f;
    public float stopDistance = 1f;

    [Header("Movement Feel")]
    public float arrivalBoost = 5f;
    public float momentumRetention = 0.8f;

    [Header("Cooldown")]
    public float grappleCooldown = 0.3f;

    private Vector3 grapplePoint;
    private bool isGrappling;
    private bool canGrapple = true;

    private List<Vector3> grappleQueue = new List<Vector3>();
    private Vector3 currentTarget;
    private bool isChainGrappling;
    public GameObject markerPrefab;

    private void Start()
    {
        if (rope != null)
        {
            rope.positionCount = 0;
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && canGrapple)
        {
            StartGrapple();
        }

        if (isGrappling)
        {
            DrawRope();
        }

        if(Input.GetMouseButtonDown(0))
        {
            AddGrapplePoint();
        }
        if(Input.GetKeyDown(KeyCode.E))
        {
            StartChain();
        }
    }

    private void FixedUpdate()
    {
        if (!isGrappling)
            return;

        Vector3 direction =
            (grapplePoint - transform.position).normalized;

        rb.linearVelocity = direction * grappleSpeed;

        float distance =
            Vector3.Distance(transform.position, grapplePoint);

        if (distance <= stopDistance)
        {
            CompleteGrapple();
        }
        
    }

    void StartGrapple()
    {
        Vector3 mousePos = Input.mousePosition;

        float distanceFromCamera =
            Mathf.Abs(playerCamera.transform.position.z);

        mousePos.z = distanceFromCamera;

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

    void CompleteGrapple()
    {
        Vector3 launchDir =
            (grapplePoint - transform.position).normalized;

        rb.linearVelocity *= momentumRetention;

        rb.AddForce(
            launchDir * arrivalBoost,
            ForceMode.VelocityChange);

        StopGrapple();
    }

    void StopGrapple()
    {
        isGrappling = false;

        if (rope != null)
        {
            rope.positionCount = 0;
            rope.enabled = false;
        }

        StartCoroutine(GrappleCooldown());
    }

    System.Collections.IEnumerator GrappleCooldown()
    {
        canGrapple = false;

        yield return new WaitForSeconds(grappleCooldown);

        canGrapple = true;
    }

    void DrawRope()
    {
        if (rope == null)
            return;

        rope.SetPosition(0, gunTip.position);
        rope.SetPosition(1, grapplePoint);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxDistance);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(isGrappling)
        {
            StopGrapple();
        }
    }

    void AddGrapplePoint()
{
    RaycastHit hit;

    Vector3 mousePos = Input.mousePosition;

    mousePos.z =
        Mathf.Abs(playerCamera.transform.position.z);

    Vector3 mouseWorld =
        playerCamera.ScreenToWorldPoint(mousePos);

    mouseWorld.z = 0f;

    Vector3 direction =
        (mouseWorld - transform.position).normalized;

    if (Physics.Raycast(
        transform.position,
        direction,
        out hit,
        maxDistance))
    {
        if(hit.collider.CompareTag("NoGrapple"))
            return;

        Vector3 point = hit.point;

        point.z = 0f;

        grappleQueue.Add(point);

        Instantiate(
            markerPrefab,
            point,
            Quaternion.identity);
    }
}
void StartChain()
{
    if(grappleQueue.Count == 0)
        return;

    currentTarget = grappleQueue[0];

    isChainGrappling = true;
}
}