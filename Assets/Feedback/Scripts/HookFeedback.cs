using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Visual feedback for the grappling hook. Listens to events on GrapplingHook and
/// handles the rope (extend, wobble, settle, retract), impact burst, hit-stop and miss fizzle.
///
/// Setup: put this on the player, give it its OWN LineRenderer (not the one on GrapplingHook;
/// leave GrapplingHook's "Rope" field empty so it doesn't draw a second rope).
/// </summary>
public class HookFeedback : MonoBehaviour
{
    enum RopeState { Hidden, Extending, Attached, Retracting }

    [SerializeField] bool hideHookOwnRope = true;
    [Header("References")]
    [SerializeField] GrapplingHook hook;
    [Tooltip("A LineRenderer used only by this script.")]
    [SerializeField] LineRenderer rope;
    [Tooltip("Where the rope starts. Defaults to the hook's gunTip.")]
    [SerializeField] Transform ropeStart;

    [Header("Rope motion")]
    [SerializeField] int segments = 24;
    [SerializeField] float extendTime = 0.08f;
    [SerializeField] float retractTime = 0.12f;
    [SerializeField] float wobbleAmplitude = 0.35f;
    [SerializeField] float wobbleWaves = 12f;     // waves along the rope
    [SerializeField] float wobbleSpeed = 30f;
    [SerializeField] float settleTime = 0.35f;    // time for the wobble to die out

    [Header("Rope color")]
    [SerializeField] Color slackColor = Color.white;
    [SerializeField] Color tautColor = new Color(1f, 0.85f, 0.3f);

    [Header("Impact")]
    [SerializeField] ParticleSystem attachBurstPrefab;
    [SerializeField] ParticleSystem missFizzlePrefab;
    [SerializeField, Range(0f, 0.2f)] float hitStopSeconds = 0.05f;

    [Header("Extra hooks (sound, camera shake, etc.)")]
    public UnityEvent<Vector3> onAttached;
    public UnityEvent<Vector3> onMissed;
    public UnityEvent onReleased;

    RopeState state = RopeState.Hidden;
    float stateTime;
    Vector3 target;

    void Awake()
    {
        if (hook == null) hook = FindAnyObjectByType<GrapplingHook>();
        if (ropeStart == null && hook != null) ropeStart = hook.gunTip;

        rope.useWorldSpace = true;
        rope.positionCount = 0;
        rope.enabled = false;
    }

    void OnEnable()
    {
        if (hook == null) return;
        hook.OnHookAttached += HandleAttached;
        hook.OnHookMissed += HandleMissed;
        hook.OnHookReleased += HandleReleased;
    }

    void OnDisable()
    {
        if (hook == null) return;
        hook.OnHookAttached -= HandleAttached;
        hook.OnHookMissed -= HandleMissed;
        hook.OnHookReleased -= HandleReleased;
    }

    // ------------------------------------------------------------- events

    void HandleAttached(Vector3 point)
    {
        target = point;
        state = RopeState.Extending;
        stateTime = 0f;
        rope.positionCount = segments;
        rope.enabled = true;
        StartCoroutine(ImpactAfterExtend(point));
    }

    void HandleMissed(Vector3 point) => PlayMiss(point);

    void HandleReleased()
    {
        if (state == RopeState.Hidden) return;
        state = RopeState.Retracting;
        stateTime = 0f;
        onReleased?.Invoke();
    }

    /// <summary>Public so the roulette can also call this when the barrel result is a dud.</summary>
    public void PlayMiss(Vector3 point)
    {
        point.z = 0f;
        Spawn(missFizzlePrefab, point);
        onMissed?.Invoke(point);
    }

    // ----------------------------------------------------------- rope drawing

    void LateUpdate()
    {
        if (hideHookOwnRope && hook != null && hook.rope != null && hook.rope.enabled)
            hook.rope.enabled = false;
        if (state == RopeState.Hidden || ropeStart == null) return;

        stateTime += Time.deltaTime;

        Vector3 start = ropeStart.position;
        Vector3 tip = target;
        float amplitude = 0f;
        float taut = 0f;

        switch (state)
        {
            case RopeState.Extending:
                {
                    float t = Mathf.Clamp01(stateTime / extendTime);
                    tip = Vector3.Lerp(start, target, EaseOutCubic(t));
                    amplitude = wobbleAmplitude;
                    if (t >= 1f) { state = RopeState.Attached; stateTime = 0f; }
                    break;
                }
            case RopeState.Attached:
                {
                    float s = Mathf.Clamp01(stateTime / settleTime);
                    amplitude = wobbleAmplitude * (1f - s) * (1f - s);
                    taut = s;
                    break;
                }
            case RopeState.Retracting:
                {
                    float t = Mathf.Clamp01(stateTime / retractTime);
                    tip = Vector3.Lerp(target, start, t * t);
                    amplitude = wobbleAmplitude * 0.3f * (1f - t);
                    if (t >= 1f) { Hide(); return; }
                    break;
                }
        }

        DrawRope(start, tip, amplitude, taut);
    }

    void DrawRope(Vector3 start, Vector3 tip, float amplitude, float taut)
    {
        // The game is a side view, so "sideways" is perpendicular in the XY plane.
        Vector3 dir = tip - start;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f).normalized;

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            float envelope = Mathf.Sin(t * Mathf.PI); // 0 at both ends, 1 in the middle
            float wave = Mathf.Sin(t * wobbleWaves - Time.time * wobbleSpeed);
            Vector3 p = Vector3.Lerp(start, tip, t) + perp * (wave * amplitude * envelope);
            rope.SetPosition(i, p);
        }

        Color c = Color.Lerp(slackColor, tautColor, taut);
        rope.startColor = c;
        rope.endColor = c;
    }

    void Hide()
    {
        state = RopeState.Hidden;
        rope.positionCount = 0;
        rope.enabled = false;
    }

    // ---------------------------------------------------------------- impact

    IEnumerator ImpactAfterExtend(Vector3 point)
    {
        yield return new WaitForSeconds(extendTime);
        Spawn(attachBurstPrefab, point);
        onAttached?.Invoke(point);
        if (hitStopSeconds > 0f) yield return HitStop(hitStopSeconds);
    }

    IEnumerator HitStop(float seconds)
    {
        float previous = Time.timeScale;
        if (previous <= 0f) yield break;     // already paused, don't interfere

        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(seconds);

        // Only restore if nobody else changed it in the meantime.
        if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = previous;
    }

    void Spawn(ParticleSystem prefab, Vector3 position)
    {
        if (prefab == null) return;
        ParticleSystem ps = Instantiate(prefab, position, Quaternion.identity);
        ps.Play();
        Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax + 0.5f);
    }

    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
}