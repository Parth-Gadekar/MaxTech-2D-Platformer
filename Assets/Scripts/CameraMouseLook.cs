
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMouseLook : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private Key toggleKey = Key.Q;
    [SerializeField] private bool startInGrappleMode = false;

    [Header("Mouse Look")]
    [SerializeField] private float lookUpDistance = 5f;
    [SerializeField] private float lookDownDistance = 3f;
    [SerializeField] private float smoothSpeed = 5f;

    [Range(0f, 0.4f)]
    [SerializeField] private float deadZone = 0.08f;

    public bool IsGrappleMode { get; private set; }

    private Vector3 baseLocalPosition;
    private float currentOffset;
    private float offsetVelocity;

    private void Start()
    {
        baseLocalPosition = transform.localPosition;
        IsGrappleMode = startInGrappleMode;
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            IsGrappleMode = !IsGrappleMode;
        }
    }

    private void LateUpdate()
    {
        float targetOffset = 0f;

        if (IsGrappleMode && Mouse.current != null &&
            Screen.height > 0)
        {
            float mouseY =
                Mouse.current.position.ReadValue().y / Screen.height;

            float centeredY = (mouseY - 0.5f) * 2f;

            if (Mathf.Abs(centeredY) < deadZone)
                centeredY = 0f;

            targetOffset = centeredY >= 0f
                ? centeredY * lookUpDistance
                : centeredY * lookDownDistance;
        }

        currentOffset = Mathf.SmoothDamp(
            currentOffset,
            targetOffset,
            ref offsetVelocity,
            1f / Mathf.Max(0.01f, smoothSpeed)
        );

        transform.localPosition =
            baseLocalPosition + Vector3.up * currentOffset;
    }
}
