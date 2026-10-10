using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Plays the "loading the revolver" animation: the barrel steps one chamber at a time,
/// and every live chamber gets a bullet that drops in with a small overshoot.
/// Put this on the same object as BarrelSpin3D (the RevolverBarrel root).
/// </summary>
[RequireComponent(typeof(BarrelSpin3D))]
public class BarrelLoadSequence : MonoBehaviour
{
    [SerializeField] BarrelSpin3D barrel;

    [Header("Speed")]
    [Tooltip("1 = original speed. Higher = the whole loading animation plays faster.")]
    [SerializeField, Range(0.5f, 4f)] float speedMultiplier = 1.7f;

    [Header("Timing (seconds, before the speed multiplier)")]
    [SerializeField] float stepRotateTime = 0.15f;  // barrel turn between chambers
    [SerializeField] float dropTime = 0.18f;        // bullet dropping into the chamber
    [SerializeField] float pauseAfterDrop = 0.05f;

    [Header("Bullet drop look")]
    [Tooltip("How far above the chamber the bullet starts (local units, the barrel is about 0.04 wide).")]
    [SerializeField] float dropDistance = 0.02f;
    [SerializeField] float startScale = 1.15f;
    [Tooltip("Bigger = the bullet sinks further past its seat before settling.")]
    [SerializeField] float overshoot = 1.7f;

    [Header("Testing (press L to play the sequence)")]
    [SerializeField] bool enableTestInput = true;

    public event Action<int> OnBulletLoaded;   // chamber index, hook a click sound here
    public event Action OnLoadComplete;

    public bool IsLoading { get; private set; }

    Vector3[] restPositions;
    Vector3[] restScales;

    float Speed => Mathf.Max(0.1f, speedMultiplier);

    void Awake()
    {
        if (barrel == null) barrel = GetComponent<BarrelSpin3D>();

        // Remember where each bullet sits when it is fully loaded.
        restPositions = new Vector3[6];
        restScales = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            GameObject b = barrel.GetBullet(i);
            if (b == null) continue;
            restPositions[i] = b.transform.localPosition;
            restScales[i] = b.transform.localScale;
        }
    }

    void Update()
    {
        if (!enableTestInput) return;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.lKey.wasPressedThisFrame) PlayLoad();
#else
        if (Input.GetKeyDown(KeyCode.L)) PlayLoad();
#endif
    }

    /// <summary>Call this from other scripts to start the loading animation.</summary>
    public void PlayLoad()
    {
        if (IsLoading || barrel.IsBusy) return;
        StartCoroutine(LoadRoutine());
    }

    IEnumerator LoadRoutine()
    {
        IsLoading = true;

        // Pick which chambers are loaded this time, but keep every bullet hidden for now.
        barrel.LoadChambers(false);

        for (int i = 0; i < 6; i++)
        {
            yield return barrel.RotateToChamber(i, stepRotateTime / Speed);

            if (barrel.IsLive(i))
            {
                yield return DropBullet(i);
                OnBulletLoaded?.Invoke(i);
                if (pauseAfterDrop > 0f) yield return new WaitForSeconds(pauseAfterDrop / Speed);
            }
        }

        IsLoading = false;
        OnLoadComplete?.Invoke();
    }

    IEnumerator DropBullet(int i)
    {
        GameObject bullet = barrel.GetBullet(i);
        if (bullet == null) yield break;

        Transform t = bullet.transform;
        Vector3 axis = barrel.SpinAxis;                    // points out of the barrel's face
        Vector3 seated = restPositions[i];
        Vector3 from = seated + axis * dropDistance;
        Vector3 scaleSeated = restScales[i];
        Vector3 scaleFrom = scaleSeated * startScale;

        t.localPosition = from;
        t.localScale = scaleFrom;
        bullet.SetActive(true);

        float e = 0f;
        float duration = Mathf.Max(0.01f, dropTime / Speed);
        while (e < 1f)
        {
            e += Time.deltaTime / duration;
            float k = EaseOutBack(Mathf.Clamp01(e), overshoot);
            t.localPosition = Vector3.LerpUnclamped(from, seated, k);
            t.localScale = Vector3.LerpUnclamped(scaleFrom, scaleSeated, k);
            yield return null;
        }

        t.localPosition = seated;
        t.localScale = scaleSeated;
    }

    // Goes past 1 and comes back, which gives the "seating" overshoot.
    static float EaseOutBack(float x, float strength)
    {
        float c3 = strength + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + strength * p * p;
    }
}