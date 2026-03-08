using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// DANTE: DIVINE'S FALL — LevelManager.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Per-scene singleton (NOT DontDestroyOnLoad).
/// A fresh instance lives in every gameplay scene as a prefab.
/// Owns: async scene loading, fade-to-black transition, loading screen display,
///       Enter-to-start gate (only after async load reaches 90%), and
///       LevelData ScriptableObject lookup.
///
/// ── SCENE BUILD INDEX CONTRACT (mirrors GameManager) ──────────────────────
///   0  → MainMenu
///   1  → Level 1  (Selva Oscura)
///   2  → Level 2  (Piattaforme)
///   3  → Level 3  (Labirinto Diavoli)
///   4  → Level 4  (Sabbia / Cappe di Piombo)
///   5  → Level 5  (Boss — Lucifero)
///   6  → Level 6  (Fine / Le Stelle)
///
/// ── INSPECTOR SETUP ───────────────────────────────────────────────────────
///   levelDataList   → drag LevelData SOs in order: slot 0 = Level 1 (index 1),
///                     slot 1 = Level 2 (index 2), … slot 5 = Level 6 (index 6).
///                     Slot count must equal total gameplay scenes (6).
///
///   fadeImage       → a full-screen black Image on a Screen Space — Overlay canvas
///                     with a Canvas Group (alpha driven by coroutine).
///   loadingCanvas   → the entire loading screen canvas (disabled by default).
///   loadingBG       → Image component for background sprite swap.
///   titleText       → TMP text for level name.
///   subtitleText    → TMP text for level subtitle.
///   rulesText       → TMP text for rules paragraph.
///   pressEnterLabel → TMP text "Premi INVIO per iniziare" (hidden until load ≥ 90%).
///   loadingBarFill  → optional Image (type: Filled) showing async progress 0→1.
///
/// ── FADE CANVAS SETUP IN EDITOR ───────────────────────────────────────────
///   1. Create a Canvas (Screen Space Overlay, Sort Order 99).
///   2. Add a full-screen black Image child → assign to fadeImage.
///   3. Add a CanvasGroup to that Image → FadeManager will drive its alpha.
///   4. Set Image alpha to 0 initially (CanvasGroup.alpha = 0).
///   The same canvas can parent both the fade Image and the loadingCanvas.
///
/// ── PORTAL WIRING ─────────────────────────────────────────────────────────
///   On each portal's PortalController component:
///     portalTarget = 2   (scene build index of destination)
///   PortalController.cs calls:
///     LevelManager.Instance.LoadLevel(portalTarget);
/// </summary>

public class LevelManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static LevelManager Instance { get; private set; }

    // ─── Level Data ───────────────────────────────────────────────────────────
    [Header("Level Data (slot 0 = scene index 1, slot 1 = scene index 2, …)")]
    public LevelData[] levelDataList;

    // ─── UI References ────────────────────────────────────────────────────────
    [Header("Fade")]
    [Tooltip("Full-screen black Image with a CanvasGroup component.")]
    public CanvasGroup fadeCanvasGroup;

    [Header("Loading Screen")]
    public GameObject loadingCanvas;
    public Image loadingBackground;
    public TMP_Text titleText;
    public TMP_Text subtitleText;
    public TMP_Text rulesBodyText;
    public TMP_Text pressEnterLabel;
    public Image loadingBarFill;      // optional — can be null

    [Header("Timing")]
    [Tooltip("Seconds for the fade-to-black animation.")]
    public float fadeDuration = 0.6f;

    [Tooltip("Seconds the loading screen stays visible before Enter is even checked " +
             "(gives player time to read even on fast machines).")]
    public float minimumReadTime = 1.5f;

    // ─── Private State ────────────────────────────────────────────────────────
    private bool _readyToActivate = false;   // true when async ≥ 90% AND minimumReadTime elapsed
    private bool _isTransitioning = false;   // guard against double-triggers
    private int _pendingSceneIndex = -1;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        // Per-scene singleton: destroy duplicates (e.g. if prefab appears twice)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Loading canvas starts hidden
        if (loadingCanvas != null)
            loadingCanvas.SetActive(false);

        // Fade canvas starts transparent
        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = 0f;

        // Fade IN when scene loads (screen was black from previous fade-out)
        StartCoroutine(FadeIn());
    }

    void OnDestroy()
    {
        // Clear singleton ref when this scene unloads
        if (Instance == this) Instance = null;
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Entry point for portals. Called by PortalController.cs.
    /// targetSceneIndex: the Build Settings index of the destination scene.
    /// </summary>
    public void LoadLevel(int targetSceneIndex)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;
        _pendingSceneIndex = targetSceneIndex;
        StartCoroutine(TransitionRoutine(targetSceneIndex));
    }

    // ─── Master Transition Coroutine ──────────────────────────────────────────

    /// <summary>
    /// Full sequence:
    ///   1. Fade to black
    ///   2. Show loading screen with LevelData content
    ///   3. Begin async load in background
    ///   4. Wait for async ≥ 90% AND minimumReadTime
    ///   5. Show "Premi INVIO" label
    ///   6. Wait for Enter key
    ///   7. Activate scene
    /// </summary>
    private IEnumerator TransitionRoutine(int targetIndex)
    {
        // ── Step 1: Disable player input during transition ─────────────────
        // PlayerController listens to GameManager state; setting Paused blocks movement.
        // We do NOT use Time.timeScale = 0 here — async loading needs real time.
        GameManager.Instance?.SetPaused(true);

        // ── Step 2: Fade to black ─────────────────────────────────────────
        yield return StartCoroutine(FadeOut());

        // ── Step 3: Populate and show loading screen ──────────────────────
        PopulateLoadingScreen(targetIndex);
        loadingCanvas.SetActive(true);

        // ── Step 4: Fade loading screen in ────────────────────────────────
        yield return StartCoroutine(FadeIn());

        // ── Step 5: Begin async scene load (NOT activated yet) ────────────
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetIndex);
        asyncLoad.allowSceneActivation = false;

        if (pressEnterLabel != null)
            pressEnterLabel.gameObject.SetActive(false);

        float elapsedReadTime = 0f;
        _readyToActivate = false;

        // ── Step 6: Wait for load ≥ 90% AND minimum read time ─────────────
        while (!_readyToActivate)
        {
            elapsedReadTime += Time.deltaTime;

            // Update optional loading bar
            if (loadingBarFill != null)
                loadingBarFill.fillAmount = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            bool loadReady = asyncLoad.progress >= 0.9f;
            bool readReady = elapsedReadTime >= minimumReadTime;

            if (loadReady && readReady)
                _readyToActivate = true;

            yield return null;
        }

        // ── Step 7: Show "Premi INVIO" label ──────────────────────────────
        if (pressEnterLabel != null)
            pressEnterLabel.gameObject.SetActive(true);

        // Fill bar to 100% visually
        if (loadingBarFill != null)
            loadingBarFill.fillAmount = 1f;

        // ── Step 8: Wait for Enter key ────────────────────────────────────
        yield return StartCoroutine(WaitForEnterKey());

        // ── Step 9: Fade to black before activation ────────────────────────
        yield return StartCoroutine(FadeOut());

        // ── Step 10: Re-enable normal game state, activate scene ──────────
        GameManager.Instance?.SetPaused(false);
        asyncLoad.allowSceneActivation = true;
        // OnSceneLoaded in GameManager fires next, which restores HP, timers, etc.
        // LevelManager in the NEW scene will FadeIn() from its own Awake().
    }

    // ─── Loading Screen Population ────────────────────────────────────────────
    private void PopulateLoadingScreen(int targetSceneIndex)
    {
        LevelData data = GetLevelData(targetSceneIndex);

        if (data == null)
        {
            Debug.LogWarning($"[LevelManager] No LevelData found for scene index {targetSceneIndex}.");

            if (titleText != null) titleText.text = $"Level {targetSceneIndex}";
            if (subtitleText != null) subtitleText.text = "";
            if (rulesBodyText != null) rulesBodyText.text = "";
            return;
        }

        if (titleText != null) titleText.text = data.levelName;
        if (subtitleText != null) subtitleText.text = data.levelSubtitle;
        if (rulesBodyText != null) rulesBodyText.text = data.rulesText;

        if (loadingBackground != null)
        {
            loadingBackground.sprite = data.backgroundSprite;
            loadingBackground.enabled = data.backgroundSprite != null;
        }

        // Notify AudioManager if a music clip is defined
        if (data.musicClip != null) { }
            // TODO: AudioManager.Instance?.PlayMusic(data.musicClip);
    }

    /// <summary>
    /// Converts a scene build index into the matching LevelData slot.
    /// Scene index 1 → slot 0, index 2 → slot 1, … index 6 → slot 5.
    /// Scene index 0 (Main Menu) returns null — no loading screen needed.
    /// </summary>
    private LevelData GetLevelData(int sceneIndex)
    {
        int slot = sceneIndex - 1;   // offset: scene 1 lives at slot 0
        if (levelDataList == null || slot < 0 || slot >= levelDataList.Length)
            return null;
        return levelDataList[slot];
    }

    // ─── Fade Coroutines ──────────────────────────────────────────────────────

    /// <summary>Fade screen to black (alpha 0 → 1).</summary>
    private IEnumerator FadeOut()
    {
        if (fadeCanvasGroup == null) yield break;

        float elapsed = 0f;
        fadeCanvasGroup.blocksRaycasts = true;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 1f;
    }

    /// <summary>Fade screen in from black (alpha 1 → 0).</summary>
    private IEnumerator FadeIn()
    {
        if (fadeCanvasGroup == null) yield break;

        float elapsed = 0f;
        fadeCanvasGroup.blocksRaycasts = true;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / fadeDuration));
            yield return null;
        }

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
    }

    // ─── Input Wait ───────────────────────────────────────────────────────────

    /// <summary>
    /// Waits until the player presses Enter / Return.
    /// Uses the legacy Input API intentionally — this fires even when
    /// the New Input System's Action Maps are disabled during pause state.
    /// </summary>
    private IEnumerator WaitForEnterKey()
    {
        // Flush any Enter press that may already be queued from this frame
        yield return null;

        while (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter))
            yield return null;
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("DEBUG — Load Next Level")]
    private void Debug_LoadNext()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        LoadLevel(next);
    }

    [ContextMenu("DEBUG — Reload Current Level")]
    private void Debug_ReloadCurrent()
    {
        LoadLevel(SceneManager.GetActiveScene().buildIndex);
    }
#endif
}