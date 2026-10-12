using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Spins a 3D revolver barrel (viewed face-on) while charging, then slows down
/// and lands on a random chamber at the top. Put this on the Cylinder_Root object.
///
/// Chamber 0 is at the top, the rest follow clockwise (as seen by the player).
/// </summary>
public class BarrelSpin3D : MonoBehaviour
{
    [Header("Model")]
    [Tooltip("The object that rotates. Usually this same object (Cylinder_Root).")]
    [SerializeField] Transform barrel;
    
    [Header("Top Chamber Detection")]
    [SerializeField] private Transform viewCamera;
    
    [Tooltip("One bullet object per chamber, in order: top first, then clockwise.")]
    [SerializeField] GameObject[] bullets = new GameObject[6];

    [Header("Spin axis")]
    [Tooltip("Local axis that points out of the barrel's face toward the camera.")]
    [SerializeField] Vector3 spinAxis = Vector3.forward;
    [Tooltip("Tick this if the barrel spins the wrong way on screen.")]
    [SerializeField] bool flipDirection = false;

    [Header("Charge spin")]
    [SerializeField] float minSpinSpeed = 120f;    // degrees/sec at 0% charge
    [SerializeField] float maxSpinSpeed = 1080f;   // degrees/sec at 100% charge
    [SerializeField] float timeToFullCharge = 1.5f;

    [Header("Landing")]
    [SerializeField] float settleDuration = 1.2f;
    [SerializeField] int extraSpins = 2;
    [SerializeField, Range(0f, 15f)] float overshootDegrees = 6f;

    [Header("Odds")]
    [SerializeField, Range(0, 6)] int liveChambers = 3;

    [Header("Testing (Space = hold to charge, release to land)")]
    [SerializeField] bool enableTestInput = true;

    // ---- Events other scripts can subscribe to ----
    public event Action<float> OnChargeChanged;     // 0..1 while charging
    public event Action<int> OnChamberTick;         // chamber index passing the top
    public event Action<bool, int> OnResult;        // (isLive, chamberIndex)

    const int ChamberCount = 6;
    const float Step = 360f / ChamberCount;

    bool[] isLive = new bool[ChamberCount];
    bool charging;
    bool settling;
    float charge01;
    float angle;                  // degrees, counter-clockwise as seen by the player
    int lastTickIndex;
    Quaternion baseRotation;
    Coroutine settleRoutine;

    public bool IsBusy => charging || settling;

    // ---- Helpers used by BarrelLoadSequence ----
    public Vector3 SpinAxis => spinAxis;
    public bool IsLive(int chamber) => isLive[chamber];
    public GameObject GetBullet(int chamber) =>
        (chamber >= 0 && chamber < bullets.Length) ? bullets[chamber] : null;

    /// <summary>Turn the barrel (always the same direction) until this chamber is at the top.</summary>
    public IEnumerator RotateToChamber(int chamber, float duration)
    {
        settling = true;

        float start = angle;
        float delta = Mathf.Repeat(chamber * Step - Mathf.Repeat(start, 360f), 360f);
        if (delta > 359.99f) delta = 0f;
        float end = start + delta;

        if (delta > 0.01f)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, duration);
                SetAngle(Mathf.Lerp(start, end, EaseOutCubic(Mathf.Clamp01(t))));
                yield return null;
            }
        }

        SetAngle(end);
        settling = false;
    }

    void Awake()
    {
        if (barrel == null) barrel = transform;
        baseRotation = barrel.localRotation;
        spinAxis = spinAxis.normalized;
        LoadChambers(false);
        ApplyAngle(0f);
    }

    // ---------------------------------------------------------------- loading

    /// <summary>Randomly choose which chambers are live and update the bullet objects.</summary>
    public void LoadChambers(bool showBullets = true)
    {
        Array.Clear(isLive, 0, isLive.Length);

        int placed = 0;
        while (placed < liveChambers)
        {
            int i = UnityEngine.Random.Range(0, ChamberCount);
            if (isLive[i]) continue;
            isLive[i] = true;
            placed++;
        }

        for (int i = 0; i < bullets.Length && i < ChamberCount; i++)
        {
            if (bullets[i] != null) bullets[i].SetActive(showBullets && isLive[i]);
        }
    }
    /// <summary>Set how many chambers get a bullet on the next load.</summary>
    public void SetLiveChambers(int count)
    {
        liveChambers = Mathf.Clamp(count, 0, ChamberCount);
    }

    // ----------------------------------------------------------------- public

    public void BeginCharge()
    {
        if (settling) return;
        charging = true;
        charge01 = 0f;
    }

    public void ReleaseCharge()
    {
        if (!charging) return;
        charging = false;
        settleRoutine = StartCoroutine(Settle());
    }

    // ---------------------------------------------------------------- update

    void Update()
    {
        if (enableTestInput) HandleTestInput();

        if (!charging) return;

        charge01 = Mathf.Clamp01(charge01 + Time.deltaTime / timeToFullCharge);
        OnChargeChanged?.Invoke(charge01);

        float speed = Mathf.Lerp(minSpinSpeed, maxSpinSpeed, charge01);
        SetAngle(angle + speed * Time.deltaTime);
    }

    IEnumerator Settle()
    {
        settling = true;

        int target = UnityEngine.Random.Range(0, ChamberCount);

        // Chamber i sits i*60 degrees clockwise from the top, so rotating the
        // barrel counter-clockwise by i*60 brings it to the top.
        float start = angle;
        float landing = target * Step;
        float delta = Mathf.Repeat(landing - Mathf.Repeat(start, 360f), 360f) + extraSpins * 360f;
        float end = start + delta;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / settleDuration;
            SetAngle(Mathf.Lerp(start, end, EaseOutCubic(Mathf.Clamp01(t))));
            yield return null;
        }

        if (overshootDegrees > 0f)
        {
            yield return TweenAngle(end, end + overshootDegrees, 0.05f);
            yield return TweenAngle(end + overshootDegrees, end, 0.12f);
        }

        SetAngle(end);
        settling = false;

        // Determine the result from the chamber physically at the top.
        int topChamber = GetTopChamberIndex();

        OnResult?.Invoke(isLive[topChamber], topChamber);
    }

    IEnumerator TweenAngle(float from, float to, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            SetAngle(Mathf.Lerp(from, to, EaseOutCubic(Mathf.Clamp01(t))));
            yield return null;
        }
    }

    // --------------------------------------------------------------- helpers

    void SetAngle(float newAngle)
    {
        angle = newAngle;
        ApplyAngle(angle);

        // Fire a tick every time a chamber passes the top.
        int tickIndex = Mathf.FloorToInt((angle + Step * 0.5f) / Step);
        if (tickIndex != lastTickIndex)
        {
            lastTickIndex = tickIndex;
            int chamber = (int)Mathf.Repeat(tickIndex, ChamberCount);
            OnChamberTick?.Invoke(chamber);
        }
    }

    void ApplyAngle(float degrees)
    {
        float signed = flipDirection ? -degrees : degrees;
        barrel.localRotation = baseRotation * Quaternion.AngleAxis(signed, spinAxis);
    }

    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    // --------------------------------------------------------------- testing

    
    int GetTopChamberIndex()
    {
        // "Up" as the player sees it = up direction of the camera that renders the barrel
        Transform reference = viewCamera != null
            ? viewCamera
            : (Camera.main != null ? Camera.main.transform : null);

        if (reference == null)
        {
            Debug.LogError("Assign View Camera (the camera that renders the revolver).");
            return 0;
        }

        Vector3 up = reference.up;

        int topIndex = -1;
        float best = float.NegativeInfinity;
        float second = float.NegativeInfinity;

        for (int i = 0; i < ChamberCount; i++)
        {
            GameObject bullet = GetBullet(i);
            if (bullet == null)
            {
                Debug.LogError($"Bullet reference {i} is missing.");
                continue;
            }

            // Works even when the bullet object is inactive
            float height = Vector3.Dot(GetChamberPoint(bullet), up);

            if (height > best)
            {
                second = best;
                best = height;
                topIndex = i;
            }
            else if (height > second)
            {
                second = height;
            }
        }

        if (topIndex < 0)
        {
            Debug.LogError("Could not identify the top chamber.");
            return 0;
        }

        // If the winner barely beats the runner-up, the barrel stopped between two chambers
        if (best - second < 0.001f)
            Debug.LogWarning("Barrel is not aligned: two chambers are equally high.");

        Debug.Log($"Top chamber: {topIndex}");
        return topIndex;
    }

    // Where the chamber visually is: the bullet MESH center, not the object pivot.
    // Works even when the bullet object is inactive.
    Vector3 GetChamberPoint(GameObject bullet)
    {
        MeshFilter mf = bullet.GetComponentInChildren<MeshFilter>(true);
        if (mf != null && mf.sharedMesh != null)
            return mf.transform.TransformPoint(mf.sharedMesh.bounds.center);

        return bullet.transform.position;
    }
    
    
    void HandleTestInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.spaceKey.wasPressedThisFrame) BeginCharge();
        if (kb.spaceKey.wasReleasedThisFrame) ReleaseCharge();
        if (kb.rKey.wasPressedThisFrame && !IsBusy) LoadChambers();
#else
        if (Input.GetKeyDown(KeyCode.Space)) BeginCharge();
        if (Input.GetKeyUp(KeyCode.Space)) ReleaseCharge();
        if (Input.GetKeyDown(KeyCode.R) && !IsBusy) LoadChambers();
#endif
    }

    void OnEnable()
    {
        OnResult += LogResult;
    }

    void OnDisable()
    {
        OnResult -= LogResult;
    }

    void LogResult(bool live, int chamber)
    {
        Debug.Log(live ? $"HIT! chamber {chamber}" : $"Misfire... chamber {chamber}");
    }

    void OnDrawGizmos()
    {
        if (bullets == null || bullets.Length < 2 || bullets[0] == null || bullets[1] == null) return;

        float r = Vector3.Distance(GetChamberPoint(bullets[0]), GetChamberPoint(bullets[1])) * 0.15f;

        for (int i = 0; i < bullets.Length && i < ChamberCount; i++)
        {
            if (bullets[i] == null) continue;
            Gizmos.color = isLive[i] ? Color.green : Color.red;
            Gizmos.DrawSphere(GetChamberPoint(bullets[i]), r);
        }
    }
}