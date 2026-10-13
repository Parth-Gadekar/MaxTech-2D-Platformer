
using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

public class FeedbackAudio : MonoBehaviour
{
    [Serializable]
    public class Sfx
    {
        public AudioClip[] clips;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip("Random pitch change.")]
        [Range(0f, 0.5f)]
        public float pitchVariation = 0.05f;

        public bool IsEmpty => clips == null || clips.Length == 0;
    }

    [Header("Systems to listen to")]
    [SerializeField] BarrelSpin3D barrel;
    [SerializeField] BarrelLoadSequence loader;
    [SerializeField] HookFeedback hookFeedback;

    [Header("Output")]
    [SerializeField] AudioMixerGroup outputGroup;
    [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
    [SerializeField] int voices = 8;

    [Header("Roulette barrel")]
    [SerializeField] Sfx chamberTick;
    [SerializeField] float minTickInterval = 0.05f;
    [SerializeField] Sfx bulletLoaded;
    [SerializeField] Sfx spinStart;
    [SerializeField] Sfx barrelLanded;

    [Header("Roulette results")]
    [SerializeField] Sfx resultWin;
    [SerializeField] Sfx resultLose;


    [Header("Player Movement")]
    [SerializeField] Sfx jumpGrunt;
    [SerializeField] Sfx runOnGround;
    [SerializeField] Sfx runOnMetal;

    [Tooltip("Time between repeated running sounds.")]
    [SerializeField] float runSoundInterval = 0.4f;

    float nextRunSoundTime;


    [Header("Grappling hook")]
    [SerializeField] Sfx hookAttach;
    [SerializeField] Sfx hookMiss;
    [SerializeField] Sfx hookRelease;

    AudioSource[] sources;
    int nextSource;
    float lastTickTime = -10f;
    bool spinStartPlayed;

    UnityAction<Vector3> onHookAttached;
    UnityAction<Vector3> onHookMissed;
    UnityAction onHookReleased;

    void Awake()
    {
        if (barrel == null)
            barrel = FindAnyObjectByType<BarrelSpin3D>();

        if (loader == null)
            loader = FindAnyObjectByType<BarrelLoadSequence>();

        if (hookFeedback == null)
            hookFeedback = FindAnyObjectByType<HookFeedback>();

        sources = new AudioSource[Mathf.Max(1, voices)];

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.outputAudioMixerGroup = outputGroup;
            sources[i] = s;
        }

        onHookAttached = _ => Play(hookAttach);
        onHookMissed = _ => Play(hookMiss);
        onHookReleased = () => Play(hookRelease);
    }

    void OnEnable()
    {
        if (barrel != null)
        {
            barrel.OnChamberTick += HandleTick;
            barrel.OnChargeChanged += HandleCharge;
            barrel.OnResult += HandleBarrelResult;
        }

        if (loader != null)
            loader.OnBulletLoaded += HandleBulletLoaded;

        if (hookFeedback != null)
        {
            hookFeedback.onAttached.AddListener(onHookAttached);
            hookFeedback.onMissed.AddListener(onHookMissed);
            hookFeedback.onReleased.AddListener(onHookReleased);
        }
    }

    void OnDisable()
    {
        if (barrel != null)
        {
            barrel.OnChamberTick -= HandleTick;
            barrel.OnChargeChanged -= HandleCharge;
            barrel.OnResult -= HandleBarrelResult;
        }

        if (loader != null)
            loader.OnBulletLoaded -= HandleBulletLoaded;

        if (hookFeedback != null)
        {
            hookFeedback.onAttached.RemoveListener(onHookAttached);
            hookFeedback.onMissed.RemoveListener(onHookMissed);
            hookFeedback.onReleased.RemoveListener(onHookReleased);
        }
    }

    void HandleTick(int chamber)
    {
        if (Time.unscaledTime - lastTickTime < minTickInterval)
            return;

        lastTickTime = Time.unscaledTime;
        Play(chamberTick);
    }

    void HandleCharge(float charge01)
    {
        if (spinStartPlayed)
            return;

        spinStartPlayed = true;
        Play(spinStart);
    }

    void HandleBarrelResult(bool isLive, int chamber)
    {
        spinStartPlayed = false;
        Play(barrelLanded);
        Play(isLive ? resultWin : resultLose);
    }

    void HandleBulletLoaded(int chamber)
    {
        Play(bulletLoaded);
    }


    public void PlayJumpGrunt()
    {
        Play(jumpGrunt);
    }

    public void PlayRunOnGround()
    {
        if (Time.time < nextRunSoundTime) return;

        nextRunSoundTime = Time.time + runSoundInterval;
        Play(runOnGround);
    }

    public void PlayRunOnMetal()
    {
        if (Time.time < nextRunSoundTime) return;

        nextRunSoundTime = Time.time + runSoundInterval;
        Play(runOnMetal);
    }


    public void Play(Sfx sfx)
    {
        if (sfx == null || sfx.IsEmpty || sources == null)
            return;

        AudioClip clip =
            sfx.clips[UnityEngine.Random.Range(0, sfx.clips.Length)];

        if (clip == null)
            return;

        AudioSource source = NextFreeSource();

        source.clip = clip;
        source.volume = sfx.volume * masterVolume;
        source.pitch = 1f + UnityEngine.Random.Range(
            -sfx.pitchVariation,
            sfx.pitchVariation
        );

        source.Play();
    }

    AudioSource NextFreeSource()
    {
        for (int i = 0; i < sources.Length; i++)
        {
            int index = (nextSource + i) % sources.Length;

            if (!sources[index].isPlaying)
            {
                nextSource = (index + 1) % sources.Length;
                return sources[index];
            }
        }

        AudioSource stolen = sources[nextSource];
        nextSource = (nextSource + 1) % sources.Length;
        return stolen;
    }
}
