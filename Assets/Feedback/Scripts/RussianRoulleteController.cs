
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class RussianRouletteController : MonoBehaviour
{
    [Header("Revolver References")]
    [SerializeField] private BarrelSpin3D barrel;
    [SerializeField] private BarrelLoadSequence loader;

    [Header("UI")]
    [Tooltip("The GameObject containing BulletSelectUI. Its generated canvas is hidden separately.")]
    [SerializeField] private GameObject bulletSelectUIObject;

    [Header("Grappling Charges")]
    [SerializeField] private int startingCharges = 0;
    [SerializeField] private int maxCharges = 5;

    [Header("Live Round Penalty")]
    [Tooltip("Connect your player's health damage method here in the Inspector.")]
    [SerializeField] private UnityEvent onLiveRound;

    [Header("Timing")]
    [SerializeField] private float resultDelay = 0.5f;

    [SerializeField] private GameObject rouletteVisuals;

    private int grapplingCharges;

    private bool isOpen;
    private bool canSpin;
    private bool isCharging;
    private bool isResolving;
    private bool roundLoaded;

    private CanvasGroup selectionCanvasGroup;
    private Coroutine resultCoroutine;

    public int GrapplingCharges => grapplingCharges;
    public int MaxCharges => maxCharges;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        grapplingCharges = Mathf.Clamp(
            startingCharges, 0, maxCharges
        );

        if (barrel == null)
            barrel = FindAnyObjectByType<BarrelSpin3D>();

        if (loader == null)
            loader = FindAnyObjectByType<BarrelLoadSequence>();

        if (bulletSelectUIObject != null)
        {
            Transform canvasTransform =
                bulletSelectUIObject.transform.Find("BulletSelectCanvas");

            if (canvasTransform != null)
            {
                selectionCanvasGroup =
                    canvasTransform.GetComponent<CanvasGroup>();

                if (selectionCanvasGroup == null)
                {
                    selectionCanvasGroup =
                        canvasTransform.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }
    }

    private void OnEnable()
    {
        if (barrel != null)
            barrel.OnResult += HandleResult;

        if (loader != null)
            loader.OnLoadComplete += HandleLoadComplete;
    }

    private void SetRouletteVisible(bool visible)
    {
        if (rouletteVisuals != null)
            rouletteVisuals.SetActive(visible);
    }

    private void Start()
    {
        SetSelectionUIVisible(false);
        PublishCharges();
        SetRouletteVisible(false);

        if (barrel == null || loader == null)
        {
            Debug.LogError(
                "RussianRouletteController: Assign BarrelSpin3D " +
                "and BarrelLoadSequence in the Inspector."
            );
        }
    }

    private void OnDisable()
    {
        if (barrel != null)
            barrel.OnResult -= HandleResult;

        if (loader != null)
            loader.OnLoadComplete -= HandleLoadComplete;
    }

    private void Update()
    {
        if (Keyboard.current == null || barrel == null || loader == null)
            return;

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            ToggleRoulette();
        }

        if (!isOpen || isResolving || loader.IsLoading)
            return;

        // Hold Space to spin the cylinder.
        // Release Space to let the revolver choose a chamber.
        if (canSpin &&
            Keyboard.current.spaceKey.wasPressedThisFrame &&
            !barrel.IsBusy)
        {
            barrel.BeginCharge();
            isCharging = true;
        }

        if (isCharging &&
            Keyboard.current.spaceKey.wasReleasedThisFrame)
        {
            isCharging = false;
            canSpin = false;

            barrel.ReleaseCharge();
        }
    }

    private void ToggleRoulette()
    {
        if (isResolving || barrel == null || loader == null)
            return;

        // Do not close the interface during a load or spin.
        if (isOpen && (loader.IsLoading || barrel.IsBusy))
            return;

        isOpen = !isOpen;

        SetSelectionUIVisible(isOpen);
        SetRouletteVisible(isOpen);

        if (isOpen)
        {
            canSpin = roundLoaded && !loader.IsLoading;
            Debug.Log(
                "Roulette opened. Select bullets, press SPIN, " +
                "then hold and release Space to spin the cylinder."
            );
        }
        else
        {
            canSpin = false;
            isCharging = false;
        }
    }

    private void HandleLoadComplete()
    {
        if (!isOpen)
            return;

        roundLoaded = true;
        canSpin = true;

        Debug.Log(
            "Loading complete. Hold Space and release it to spin."
        );
    }

   
    private void HandleResult(bool isLive, int chamberIndex)
    {
        // Prevent the same spin result from being processed twice.
        if (isResolving)
            return;

        isResolving = true;
        canSpin = false;

        Debug.Log(
            $"Roulette result: chamber {chamberIndex}, " +
            (isLive ? "LIVE ROUND" : "BLANK")
        );

        resultCoroutine = StartCoroutine(
            ResolveResult(isLive, chamberIndex)
        );
    }

    private IEnumerator ResolveResult(bool isLive, int chamberIndex)
    {
        yield return new WaitForSeconds(resultDelay);

        if (isLive)
        {
            onLiveRound?.Invoke();
            Debug.Log("Live round! Health penalty triggered.");
        }
        else
        {
            AddGrapplingCharge();
        }

        isOpen = false;
        isCharging = false;
        canSpin = false;
        roundLoaded = false;

        SetSelectionUIVisible(false);

        isResolving = false;
        resultCoroutine = null;

        SetRouletteVisible(false);
    }



    public void AddGrapplingCharge()
    {
        int previousCharges = grapplingCharges;

        grapplingCharges = Mathf.Min(
            grapplingCharges + 1,
            maxCharges
        );

        PublishCharges();

        if (grapplingCharges > previousCharges)
        {
            Debug.Log(
                $"Blank chamber! Grappling charges: " +
                $"{grapplingCharges}/{maxCharges}"
            );
        }
        else
        {
            Debug.Log("Blank chamber, but grappling charges are already full.");
        }
    }

    // Call this from GrapplingHook before starting a grapple.
    // Returns false when the player has no charges.
    public bool TryConsumeGrapplingCharge()
    {
        if (grapplingCharges <= 0)
        {
            Debug.Log("No grappling charges remaining!");
            return false;
        }

        grapplingCharges--;
        PublishCharges();

        Debug.Log(
            $"Grappling charge used: {grapplingCharges}/{maxCharges}"
        );

        return true;
    }

    private void PublishCharges()
    {
        Debug.Log(
            $"Grappling charges: {grapplingCharges}/{maxCharges}"
        );
    }


    private void SetSelectionUIVisible(bool visible)
    {
        // BulletSelectUI creates its canvas during Awake, so find it
        // when visibility changes instead of relying on an early lookup.
        if (selectionCanvasGroup == null && bulletSelectUIObject != null)
        {
            Transform[] children =
                bulletSelectUIObject.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in children)
            {
                if (child.name == "BulletSelectCanvas")
                {
                    selectionCanvasGroup =
                        child.GetComponent<CanvasGroup>();

                    if (selectionCanvasGroup == null)
                    {
                        selectionCanvasGroup =
                            child.gameObject.AddComponent<CanvasGroup>();
                    }

                    break;
                }
            }
        }

        if (selectionCanvasGroup != null)
        {
            selectionCanvasGroup.alpha = visible ? 1f : 0f;
            selectionCanvasGroup.interactable = visible;
            selectionCanvasGroup.blocksRaycasts = visible;
        }
        else
        {
            Debug.LogError(
                "Cannot find BulletSelectCanvas. " +
                "Assign the GameObject containing BulletSelectUI " +
                "to Bullet Select UI Object."
            );
        }
    }
}
