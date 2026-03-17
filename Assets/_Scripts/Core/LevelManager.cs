using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// DANTE: DIVINE'S FALL — LevelManager.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Per-scena singleton (NOT DontDestroyOnLoad).
///
/// RESPONSABILITÀ:
///   - All'avvio della scena: mostra loading screen con dati del levelData CORRENTE
///   - Alla transizione: carica la scena successiva, la nuova scena mostrerà
///     la propria loading screen nel proprio Awake()
///
/// FLUSSO AVVIO SCENA:
///   LevelManager.Awake()
///   → LoadingScreenRoutine (usa levelData di questa scena)
///     → loadingMusic → barra → INVIO
///     → stop loadingMusic → fade in scena → gameplayMusic
///     → LoadingScreenComplete = true
///
/// FLUSSO TRANSIZIONE (portal/bottone):
///   LoadLevel(targetIndex)
///   → FadeOut → LoadSceneAsync → allowActivation = true
///   → nuova scena si avvia → suo LevelManager.Awake() → sua loading screen
///
/// INSPECTOR:
///   levelData           → LevelData SO di questa scena
///   loadingCanvasPrefab → prefab canvas loading screen
///   fadeDuration        → durata fade (default 0.5)
///   minimumReadTime     → secondi minimi loading (default 4)
/// </summary>
public class LevelManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static LevelManager Instance { get; private set; }

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Data")]
    public LevelData levelData;

    [Header("Canvas")]
    public GameObject loadingCanvasPrefab;

    [Header("Timing")]
    public float fadeDuration = 0.5f;
    public float minimumReadTime = 4f;

    // ─── Proprietà pubblica ───────────────────────────────────────────────────
    /// <summary>True quando la loading screen è completata e il gameplay è attivo.</summary>
    public bool LoadingScreenComplete { get; private set; } = false;

    // ─── Riferimenti canvas ───────────────────────────────────────────────────
    private GameObject _canvasInstance;
    private CanvasGroup _canvasGroup;      // root canvas — per fade canvas
    private CanvasGroup _fadeGroup;        // FadePanel — per fade schermo
    private TMP_Text _headerText;
    private TMP_Text _bodyText;
    private TMP_Text _premiInvioText;
    private Image _loadingBarFill;

    // ─── Stato ────────────────────────────────────────────────────────────────
    private bool _isTransitioning = false;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Blocca input durante la loading screen
        GameManager.Instance?.SetPaused(true);

        // Istanzia canvas
        if (loadingCanvasPrefab != null)
        {
            _canvasInstance = Instantiate(loadingCanvasPrefab);
            DontDestroyOnLoad(_canvasInstance);
            FindCanvasReferences();
            _canvasInstance.SetActive(false);
        }

        // Avvia loading screen di questa scena
        StartCoroutine(LoadingScreenRoutine());
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_canvasInstance != null)
            Destroy(_canvasInstance);
    }

    // ─── Loading Screen (avvio scena) ─────────────────────────────────────────
    private IEnumerator LoadingScreenRoutine()
    {
        LoadingScreenComplete = false;

        // Pausa immediata — blocca input e camera
        GameManager.Instance?.SetPaused(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Canvas appare istantaneamente — sfondo nero copre il mondo
        PopulateCanvas();
        if (_canvasInstance != null)
        {
            _canvasInstance.SetActive(true);
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        }

        // FadePanel a 0 — non serve nascondere nulla, il canvas è già opaco
        SetFadeAlpha(0f);

        // Avvia loading music
        if (levelData?.loadingMusic != null)
            AudioManager.Instance?.PlayLooping(levelData.loadingMusic, fadeIn: false);

        // Nascondi "Premi INVIO" e azzera barra
        if (_premiInvioText != null) _premiInvioText.gameObject.SetActive(false);
        if (_loadingBarFill != null) _loadingBarFill.fillAmount = 0f;

        // Barra progresso — si riempie in minimumReadTime secondi
        float elapsed = 0f;
        while (elapsed < minimumReadTime)
        {
            elapsed += Time.deltaTime;
            if (_loadingBarFill != null)
                _loadingBarFill.fillAmount = Mathf.Clamp01(elapsed / minimumReadTime);
            yield return null;
        }

        if (_loadingBarFill != null) _loadingBarFill.fillAmount = 1f;

        // Mostra "Premi INVIO"
        if (_premiInvioText != null) _premiInvioText.gameObject.SetActive(true);

        // Attendi INVIO
        yield return StartCoroutine(WaitForEnter());

        // Stop loading music
        AudioManager.Instance?.Stop(fadeOut: false);

        // Canvas scompare istantaneamente
        if (_canvasInstance != null) _canvasInstance.SetActive(false);

        // Avvia gameplay music
        if (levelData?.gameplayMusic != null)
            AudioManager.Instance?.PlayLooping(levelData.gameplayMusic, fadeIn: true);

        // Sblocca input
        GameManager.Instance?.SetPaused(false);
        LoadingScreenComplete = true;

        // Cursore: libero nel MainMenu (index 0), bloccato nelle scene di gioco
        bool isMainMenu = UnityEngine.SceneManagement.SceneManager
                              .GetActiveScene().buildIndex == 0;
        Cursor.lockState = isMainMenu ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isMainMenu;
    }

    // ─── Transizione verso scena successiva ──────────────────────────────────
    /// <summary>
    /// Chiamato da PortalController o MainMenu.
    /// Fa solo FadeOut → carica scena → il nuovo LevelManager gestisce il resto.
    /// </summary>
    public void LoadLevel(int targetSceneIndex)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;
        StartCoroutine(TransitionRoutine(targetSceneIndex));
    }

    private IEnumerator TransitionRoutine(int targetIndex)
    {
        // Blocca input
        GameManager.Instance?.SetPaused(true);

        // Fade out scena corrente
        yield return StartCoroutine(FadeScreen(0f, 1f));

        // Carica scena — il nuovo LevelManager.Awake() gestirà la loading screen
        SceneManager.LoadScene(targetIndex);
    }

    // ─── Popola canvas ────────────────────────────────────────────────────────
    private void PopulateCanvas()
    {
        if (levelData?.canvasContent == null) return;

        CanvasContentData c = levelData.canvasContent;

        if (_headerText != null)
        {
            _headerText.text = $"<b><i>{c.header}</i></b>";
            _headerText.gameObject.SetActive(!string.IsNullOrEmpty(c.header));
        }
        if (_bodyText != null)
        {
            _bodyText.text = $"<i>{c.testo}</i>";
            _bodyText.gameObject.SetActive(!string.IsNullOrEmpty(c.testo));
        }
        if (_premiInvioText != null)
            _premiInvioText.text = c.premiInvio;
    }

    // ─── Trova riferimenti nel canvas ─────────────────────────────────────────
    private void FindCanvasReferences()
    {
        if (_canvasInstance == null) return;

        _canvasGroup = _canvasInstance.GetComponent<CanvasGroup>();

        Transform fadePanel = _canvasInstance.transform.Find("FadePanel");
        if (fadePanel != null) _fadeGroup = fadePanel.GetComponent<CanvasGroup>();

        _headerText = FindDeep<TMP_Text>("HeaderText");
        _bodyText = FindDeep<TMP_Text>("BodyText");
        _premiInvioText = FindDeep<TMP_Text>("PremiInvioText");
        _loadingBarFill = FindDeep<Image>("LoadingBarFill");
    }

    private T FindDeep<T>(string childName) where T : Component
    {
        if (_canvasInstance == null) return null;
        foreach (T c in _canvasInstance.GetComponentsInChildren<T>(true))
            if (c.gameObject.name == childName) return c;
        return null;
    }

    // ─── Fade schermo (FadePanel) ─────────────────────────────────────────────
    private void SetFadeAlpha(float alpha)
    {
        if (_fadeGroup != null)
        {
            _fadeGroup.gameObject.SetActive(true);
            _fadeGroup.alpha = alpha;
            _fadeGroup.blocksRaycasts = alpha > 0f;
        }
    }

    private IEnumerator FadeScreen(float from, float to)
    {
        if (_fadeGroup == null) yield break;

        _fadeGroup.gameObject.SetActive(true);
        _fadeGroup.blocksRaycasts = true;
        float elapsed = 0f;
        _fadeGroup.alpha = from;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            _fadeGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        _fadeGroup.alpha = to;
        if (to <= 0f) _fadeGroup.blocksRaycasts = false;
    }

    // ─── Fade canvas loading ──────────────────────────────────────────────────
    private IEnumerator FadeCanvas(float from, float to)
    {
        if (_canvasGroup == null) yield break;

        float elapsed = 0f;
        _canvasGroup.alpha = from;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        _canvasGroup.alpha = to;
    }

    // ─── Attendi INVIO ────────────────────────────────────────────────────────
    private IEnumerator WaitForEnter()
    {
        yield return null;
        var kb = Keyboard.current;
        if (kb == null) yield break;
        while (!kb.enterKey.wasPressedThisFrame && !kb.numpadEnterKey.wasPressedThisFrame)
            yield return null;
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("DEBUG — Load Next Level")]
    private void Debug_LoadNext() =>
        LoadLevel(SceneManager.GetActiveScene().buildIndex + 1);
#endif
}