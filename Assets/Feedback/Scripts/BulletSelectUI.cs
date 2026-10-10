using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Bullet picking screen: a tray of bullets in the bottom-left corner, click to select / deselect,
/// then press SPIN and the barrel loads exactly that many bullets.
/// The tray shows your real 3D bullet model (rendered into a small texture).
/// Builds its own UI at runtime. Put this on the RouletteBarrel object.
/// </summary>
public class BulletSelectUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] BarrelSpin3D barrel;
    [SerializeField] BarrelLoadSequence loader;

    [Header("Rules")]
    [SerializeField] int trayCount = 6;          // bullets shown in the tray
    [SerializeField] int maxSelectable = 5;      // how many the player may pick
    [Tooltip("Bring the used bullets back in the tray once the barrel is loaded.")]
    [SerializeField] bool refillAfterLoad = true;

    [Header("Bullet model for the tray")]
    [Tooltip("Optional. Drag your bullet prefab or a bullet object here. Empty = uses Bullet0 from the barrel.")]
    [SerializeField] GameObject bulletModelOverride;
    [Tooltip("Stand the bullet upright automatically (its longest side points up).")]
    [SerializeField] bool autoOrient = true;
    [Tooltip("Tick if the bullet tip points down instead of up.")]
    [SerializeField] bool flipUpsideDown = false;
    [Tooltip("Extra rotation if the bullet still looks wrong (try 90, 0, 0 or 0, 90, 0).")]
    [SerializeField] Vector3 extraRotation = Vector3.zero;
    [SerializeField] float previewLightIntensity = 3f;

    [Header("SPIN button look")]
    [Tooltip("Optional. Drag your bullet's gold material here. Empty = picked automatically from the bullet.")]
    [SerializeField] Material goldMaterial;

    [Header("Layout")]
    [SerializeField] Vector2 margin = new Vector2(40f, 40f);          // distance from the bottom-left corner
    [SerializeField] Vector2 cardSize = new Vector2(70f, 140f);
    [SerializeField] float cardSpacing = 14f;
    [SerializeField] float selectedLift = 22f;

    [Header("Colours")]
    [SerializeField] Color cardColor = new Color(0.08f, 0.08f, 0.09f, 0.85f);
    [SerializeField] Color cardSelectedColor = new Color(0.16f, 0.2f, 0.13f, 0.95f);
    [SerializeField] Color selectedOutline = new Color(0.45f, 1f, 0.5f);

    class Slot
    {
        public RectTransform card;
        public Image cardImage;
        public RawImage bullet;
        public Outline outline;
        public GameObject root;
        public bool selected;
    }

    readonly List<Slot> slots = new List<Slot>();
    Button spinButton;
    RectTransform spinRect;
    Text counterText;
    bool locked;
    bool previewOk;
    Font font;
    Sprite rounded;
    RenderTexture previewTexture;

    static readonly Vector2 SpinSize = new Vector2(260f, 90f);

    void Awake()
    {
        if (barrel == null) barrel = GetComponentInChildren<BarrelSpin3D>();
        if (loader == null) loader = GetComponentInChildren<BarrelLoadSequence>();
        if (barrel == null) barrel = FindAnyObjectByType<BarrelSpin3D>();
        if (loader == null) loader = FindAnyObjectByType<BarrelLoadSequence>();

        maxSelectable = Mathf.Clamp(maxSelectable, 1, 6);
        trayCount = Mathf.Max(trayCount, maxSelectable);

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rounded = MakeRoundedSprite(64, 20);
        EnsureEventSystem();
        BuildUI();
        Refresh();
    }

    void Start()
    {
        StartCoroutine(BuildPreview());
    }

    void OnEnable()
    {
        if (loader != null) loader.OnLoadComplete += HandleLoadComplete;
    }

    void OnDisable()
    {
        if (loader != null) loader.OnLoadComplete -= HandleLoadComplete;
    }

    void OnDestroy()
    {
        if (previewTexture != null) previewTexture.Release();
    }

    void Update()
    {
        // Gentle pulse on the SPIN button while it is ready.
        if (spinButton == null) return;
        float s = spinButton.interactable ? 1f + 0.03f * Mathf.Sin(Time.unscaledTime * 4f) : 1f;
        spinRect.localScale = new Vector3(s, s, 1f);
    }

    // ------------------------------------------------------------------ logic

    int SelectedCount
    {
        get { int n = 0; foreach (var s in slots) if (s.selected && s.root.activeSelf) n++; return n; }
    }

    void ToggleSlot(Slot s)
    {
        if (locked) return;
        if (!s.selected && SelectedCount >= maxSelectable) return;   // already picked the max
        s.selected = !s.selected;
        Refresh();
    }

    void OnSpinPressed()
    {
        if (locked) return;
        int count = SelectedCount;
        if (count == 0) return;
        if (loader.IsLoading || barrel.IsBusy) return;

        locked = true;

        // The chosen bullets leave the tray, they are about to go into the barrel.
        foreach (var s in slots)
            if (s.selected) s.root.SetActive(false);

        barrel.SetLiveChambers(count);
        loader.PlayLoad();
        Refresh();
    }

    void HandleLoadComplete()
    {
        locked = false;
        if (refillAfterLoad) Refill();
        Refresh();
    }

    /// <summary>Put all bullets back in the tray and clear the selection.</summary>
    public void Refill()
    {
        foreach (var s in slots)
        {
            s.selected = false;
            s.root.SetActive(true);
        }
        Refresh();
    }

    void Refresh()
    {
        foreach (var s in slots)
        {
            s.cardImage.color = s.selected ? cardSelectedColor : cardColor;
            s.outline.enabled = s.selected;
            s.card.anchoredPosition = new Vector2(0f, s.selected ? selectedLift : 0f);

            if (previewOk)
                s.bullet.color = s.selected ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f);
            else
                s.bullet.color = s.selected ? new Color(1f, 0.9f, 0.35f) : new Color(0.78f, 0.62f, 0.2f);
        }

        int n = SelectedCount;
        counterText.text = $"Click bullets to select   {n} / {maxSelectable}";
        spinButton.interactable = !locked && n > 0;
    }

    // ----------------------------------------------------- 3D bullet -> texture

    IEnumerator BuildPreview()
    {
        GameObject src = bulletModelOverride != null ? bulletModelOverride : (barrel != null ? barrel.GetBullet(0) : null);
        if (src == null)
        {
            Debug.LogWarning("BulletSelectUI: no bullet model found, showing plain boxes. Drag a bullet into 'Bullet Model Override'.");
            yield break;
        }

        // A tiny stage far away from the scene, so only the bullet is visible to this camera.
        var rig = new GameObject("BulletPreviewRig");
        rig.transform.position = new Vector3(0f, -5000f, 0f);

        GameObject clone = Instantiate(src, rig.transform);
        clone.SetActive(true);
        foreach (var c in clone.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>()) Destroy(mb);
        clone.transform.position = rig.transform.position;
        clone.transform.rotation = Quaternion.identity;
        clone.transform.localScale = src.transform.lossyScale;

        // Make sure every renderer inside the clone is on.
        var renderers = clone.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            r.gameObject.SetActive(true);
            r.enabled = true;
        }
        if (renderers.Length == 0)
        {
            Debug.LogWarning("BulletSelectUI: the bullet has no renderer, showing plain boxes.");
            Destroy(rig);
            yield break;
        }

        // Stand the bullet upright: longest side of the mesh points up.
        var mf = clone.GetComponentInChildren<MeshFilter>(true);
        if (autoOrient && mf != null && mf.sharedMesh != null)
        {
            Vector3 ext = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
            Vector3 axis = (ext.x >= ext.y && ext.x >= ext.z) ? mf.transform.right
                         : (ext.y >= ext.z) ? mf.transform.up
                         : mf.transform.forward;
            clone.transform.rotation = Quaternion.FromToRotation(axis, Vector3.up) * clone.transform.rotation;
        }
        if (flipUpsideDown) clone.transform.rotation = Quaternion.Euler(0f, 0f, 180f) * clone.transform.rotation;
        clone.transform.rotation = Quaternion.Euler(extraRotation) * clone.transform.rotation;

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        int w = 256, h = Mathf.RoundToInt(256f * cardSize.y / cardSize.x);
        var rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
        rt.Create();
        float aspect = (float)w / h;

        var camGO = new GameObject("BulletPreviewCam");
        camGO.transform.SetParent(rig.transform, true);
        float dist = b.size.magnitude + 2f;
        camGO.transform.position = b.center + new Vector3(0f, 0f, -dist);
        camGO.transform.rotation = Quaternion.identity;
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / aspect) * 1.12f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = ~0;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = dist + b.size.magnitude + 5f;
        cam.targetTexture = rt;

        // Small local lights (point lights never reach the real scene from this far away).
        AddPreviewLight(rig.transform, b.center + new Vector3(-0.5f, 0.6f, -dist * 0.6f), dist, previewLightIntensity);
        AddPreviewLight(rig.transform, b.center + new Vector3(0.6f, -0.3f, -dist * 0.5f), dist, previewLightIntensity * 0.5f);

        // Wait until the camera has really drawn something into the texture.
        bool ok = false;
        for (int i = 0; i < 12 && !ok; i++)
        {
            yield return new WaitForEndOfFrame();
            ok = HasContent(rt);
            if (!ok && i == 4)
            {
                try { cam.Render(); } catch { /* some pipelines don't allow it, the camera is still enabled */ }
            }
        }

        cam.enabled = false;
        Destroy(rig);

        if (ok)
        {
            previewTexture = rt;
            previewOk = true;
            foreach (var s in slots) s.bullet.texture = previewTexture;
        }
        else
        {
            Debug.LogWarning("BulletSelectUI: the bullet preview came out empty, showing plain gold boxes. Check the bullet has a visible mesh/material.");
            rt.Release();
        }
        Refresh();
    }

    static void AddPreviewLight(Transform parent, Vector3 pos, float dist, float intensity)
    {
        var go = new GameObject("BulletPreviewLight");
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = dist * 4f;
        l.intensity = intensity;
    }

    bool HasContent(RenderTexture rt)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        RenderTexture.active = prev;

        bool found = false;
        var px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i += 7)
        {
            if (px[i].a > 10) { found = true; break; }
        }
        Destroy(tex);
        return found;
    }

    // --------------------------------------------------------------------- UI

    void BuildUI()
    {
        var canvasGO = new GameObject("BulletSelectCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Tray, bottom-left corner
        var tray = NewRect("Tray", canvasGO.transform);
        tray.anchorMin = tray.anchorMax = new Vector2(0f, 0f);
        tray.pivot = new Vector2(0f, 0f);
        float totalW = trayCount * cardSize.x + (trayCount - 1) * cardSpacing;
        tray.sizeDelta = new Vector2(totalW, cardSize.y + selectedLift);
        tray.anchoredPosition = margin;

        for (int i = 0; i < trayCount; i++)
        {
            // Fixed cell, so the others don't shift when one is hidden.
            var cell = NewRect("Cell" + i, tray);
            cell.anchorMin = cell.anchorMax = new Vector2(0f, 0f);
            cell.pivot = new Vector2(0f, 0f);
            cell.sizeDelta = cardSize;
            cell.anchoredPosition = new Vector2(i * (cardSize.x + cardSpacing), 0f);

            var card = NewRect("Card" + i, cell);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0f);
            card.pivot = new Vector2(0.5f, 0f);
            card.sizeDelta = cardSize;
            card.anchoredPosition = Vector2.zero;

            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.sprite = rounded;
            cardImg.type = Image.Type.Sliced;
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = selectedOutline;
            outline.effectDistance = new Vector2(3f, -3f);

            // soft lighter backdrop so a dark bullet still reads against the card
            var back = NewRect("Backdrop", card);
            back.anchorMin = Vector2.zero;
            back.anchorMax = Vector2.one;
            back.offsetMin = new Vector2(5f, 5f);
            back.offsetMax = new Vector2(-5f, -5f);
            var backImg = back.gameObject.AddComponent<Image>();
            backImg.sprite = rounded;
            backImg.type = Image.Type.Sliced;
            backImg.color = new Color(0.45f, 0.45f, 0.5f, 0.35f);
            backImg.raycastTarget = false;

            var bulletRect = NewRect("BulletImage", card);
            bulletRect.anchorMin = Vector2.zero;
            bulletRect.anchorMax = Vector2.one;
            bulletRect.offsetMin = new Vector2(6f, 6f);
            bulletRect.offsetMax = new Vector2(-6f, -6f);
            var raw = bulletRect.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;

            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = cardImg;
            btn.transition = Selectable.Transition.None;

            var slot = new Slot { card = card, cardImage = cardImg, bullet = raw, outline = outline, root = cell.gameObject };
            btn.onClick.AddListener(() => ToggleSlot(slot));
            slots.Add(slot);
        }

        // Hint text above the tray
        var hint = NewRect("Hint", canvasGO.transform);
        hint.anchorMin = hint.anchorMax = new Vector2(0f, 0f);
        hint.pivot = new Vector2(0f, 0f);
        hint.sizeDelta = new Vector2(700f, 40f);
        hint.anchoredPosition = margin + new Vector2(2f, cardSize.y + selectedLift + 12f);
        counterText = hint.gameObject.AddComponent<Text>();
        counterText.font = font;
        counterText.fontSize = 26;
        counterText.alignment = TextAnchor.MiddleLeft;
        counterText.color = new Color(1f, 1f, 1f, 0.9f);
        counterText.raycastTarget = false;
        hint.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);

        BuildSpinButton(canvasGO.transform);
    }

    void BuildSpinButton(Transform canvasRoot)
    {
        Color gold = PickGoldColor();

        spinRect = NewRect("SpinButton", canvasRoot);
        spinRect.anchorMin = spinRect.anchorMax = new Vector2(1f, 0f);
        spinRect.pivot = new Vector2(0.5f, 0.5f);
        spinRect.sizeDelta = SpinSize;
        spinRect.anchoredPosition = new Vector2(-margin.x - SpinSize.x * 0.5f, margin.y + SpinSize.y * 0.5f);

        var img = spinRect.gameObject.AddComponent<Image>();
        img.sprite = MakeGoldSprite((int)SpinSize.x, (int)SpinSize.y, 26, gold);
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        var depth = spinRect.gameObject.AddComponent<Shadow>();   // raised look
        depth.effectColor = new Color(0.25f, 0.14f, 0.02f, 1f);
        depth.effectDistance = new Vector2(0f, -7f);
        var rim = spinRect.gameObject.AddComponent<Outline>();
        rim.effectColor = new Color(0.35f, 0.2f, 0.03f, 0.9f);
        rim.effectDistance = new Vector2(2f, 2f);

        spinButton = spinRect.gameObject.AddComponent<Button>();
        spinButton.targetGraphic = img;
        spinButton.transition = Selectable.Transition.ColorTint;
        var cb = spinButton.colors;
        cb.normalColor = new Color(0.94f, 0.94f, 0.94f, 1f);
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        cb.disabledColor = new Color(0.4f, 0.38f, 0.34f, 0.95f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        spinButton.colors = cb;
        spinButton.onClick.AddListener(OnSpinPressed);

        var label = NewRect("Label", spinRect);
        label.anchorMin = Vector2.zero;
        label.anchorMax = Vector2.one;
        label.offsetMin = label.offsetMax = Vector2.zero;
        var t = label.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = "SPIN";
        t.fontSize = 46;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(0.22f, 0.12f, 0.02f, 1f);
        t.raycastTarget = false;
        var ts = label.gameObject.AddComponent<Shadow>();   // engraved look
        ts.effectColor = new Color(1f, 0.92f, 0.6f, 0.7f);
        ts.effectDistance = new Vector2(0f, -2f);
    }

    // Reads the colour of the bullet's gold material so the button matches it.
    Color PickGoldColor()
    {
        Material m = goldMaterial;
        if (m == null)
        {
            GameObject src = bulletModelOverride != null ? bulletModelOverride : (barrel != null ? barrel.GetBullet(0) : null);
            if (src != null)
            {
                foreach (var r in src.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var mat in r.sharedMaterials)
                    {
                        if (mat == null) continue;
                        string n = mat.name.ToLowerInvariant();
                        if (n.Contains("gold") || n.Contains("brass")) { m = mat; break; }
                        if (m == null) m = mat;
                    }
                    if (m != null && (m.name.ToLowerInvariant().Contains("gold") || m.name.ToLowerInvariant().Contains("brass"))) break;
                }
            }
        }

        Color c = new Color(0.9f, 0.68f, 0.2f);   // default gold
        if (m != null)
        {
            Color read = c;
            if (m.HasProperty("_BaseColor")) read = m.GetColor("_BaseColor");
            else if (m.HasProperty("_Color")) read = m.GetColor("_Color");

            Color.RGBToHSV(read, out float hh, out float ss, out float vv);
            if (ss > 0.3f) c = Color.HSVToRGB(hh, Mathf.Clamp01(ss), Mathf.Clamp(vv, 0.75f, 1f));   // colour is really gold-ish, use it
        }
        return c;
    }

    // Metallic gold plate made in code: bright top edge, dark horizon line, warm reflection below, a diagonal streak.
    static Sprite MakeGoldSprite(int w, int h, int radius, Color baseCol)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[w * h];

        Color hi = Color.Lerp(baseCol, Color.white, 0.55f);
        Color lite = baseCol * 1.15f;
        Color mid = baseCol * 0.85f;
        Color dark = baseCol * 0.42f;
        Color refl = Color.Lerp(baseCol, new Color(1f, 0.85f, 0.5f), 0.25f);
        Color low = baseCol * 0.7f;

        for (int y = 0; y < h; y++)
        {
            float t = 1f - (y + 0.5f) / h;   // 0 = top, 1 = bottom
            Color row;
            if (t < 0.12f) row = Color.Lerp(hi, lite, t / 0.12f);
            else if (t < 0.45f) row = Color.Lerp(lite, mid, (t - 0.12f) / 0.33f);
            else if (t < 0.55f) row = Color.Lerp(mid, dark, (t - 0.45f) / 0.10f);
            else if (t < 0.80f) row = Color.Lerp(dark, refl, (t - 0.55f) / 0.25f);
            else row = Color.Lerp(refl, low, (t - 0.80f) / 0.20f);

            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float streak = Mathf.Exp(-Mathf.Pow((u + t * 0.25f - 0.38f) * 7f, 2f)) * 0.22f;   // soft shine
                Color c = row + new Color(streak, streak, streak * 0.8f, 0f);
                c.a = 1f;

                float cx = Mathf.Clamp(x + 0.5f, radius, w - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, h - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                c.a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * w + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    // Rounded-corner sprite made in code (9-sliced), so no image asset is needed.
    static Sprite MakeRoundedSprite(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }
}