
using UnityEngine;

public class CranePlatform : MonoBehaviour
{
    public float liftSpeed = 3f;
    public float moveSpeed = 4f;

    private Transform liftPoint;
    private Transform dropPoint;
    private CraneSpawner spawner;
    private Rigidbody rb;

    private enum State
    {
        Lift,
        Move,
        Fall
    }

    private State state;

    public void Initialize(
        Transform lift,
        Transform drop,
        CraneSpawner owner)
    {
        liftPoint = lift;
        dropPoint = drop;
        spawner = owner;
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;

        state = State.Lift;
    }

    private void FixedUpdate()
    {
        switch (state)
        {
            case State.Lift:
                MoveTowardsPoint(liftPoint, liftSpeed);
                break;

            case State.Move:
                MoveTowardsPoint(dropPoint, moveSpeed);
                break;
        }
    }

    private void MoveTowardsPoint(Transform target, float speed)
    {
        Vector3 nextPosition = Vector3.MoveTowards(
            rb.position,
            target.position,
            speed * Time.fixedDeltaTime
        );

        rb.MovePosition(nextPosition);

        if (Vector3.Distance(nextPosition, target.position) < 0.01f)
        {
            rb.position = target.position;

            if (state == State.Lift)
            {
                state = State.Move;
            }
            else if (state == State.Move)
            {
                Drop();
            }
        }
    }

    private void Drop()
    {
        state = State.Fall;

        rb.isKinematic = false;
        rb.useGravity = true;

        Invoke(nameof(DestroyPlatform), 4f);
    }

    private void DestroyPlatform()
    {
        if (spawner != null)
        {
            spawner.PlatformDestroyed();
        }

        Destroy(gameObject);
    }
}
