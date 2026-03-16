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
/// Responsabilità: loading screen, transizioni fade, audio tramite AudioManager.
///
/// FLUSSO COMPLETO:
///   PortalController.LoadLevel(index)
///     → FadeOut
///     → Mostra LoadingCanvas + avvia loadingMusic
///     → FadeIn loading screen
///     → LoadSceneAsync (allowActivation = false)
///     → Barra progresso: riempie nei 4s minimi oppure segue async (il più lento)
///     → Mostra "Premi INVIO"
///     → Attende INVIO → Stop loadingMusic → avvia gameplayMusic
///     → FadeOut
///     → allowSceneActivation = true
///     → Nuova scena: LevelManager.Awake() → FadeIn
///
/// FLUSSO RELOAD (morte/pausa → ricarica):
///   SceneManager.LoadScene(current) — nessuna loading screen
///   Nuova scena: LevelManager.Awake() → FadeIn → avvia gameplayMusic
///
/// INSPECTOR:
///   levelData         → LevelData SO con audio e CanvasContentData
///   loadingCanvasPrefab → prefab del canvas loading screen
///   fadeDuration      → durata fade (default 0.5s)
///   minimumReadTime   → secondi minimi loading (default 4s)
/// </summary>
public class LevelManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static LevelManager Instance { get; private set; }

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Data")]
    [Tooltip("LevelData SO con audio e contenuto del canvas.")]
    public LevelData levelData;

    [Header("Canvas")]
    [Tooltip("Prefab del canvas loading screen — istanziato a runtime.")]
    public GameObject loadingCanvasPrefab;

    [Header("Timing")]
    public float fadeDuration = 0.5f;
    public float minimumReadTime = 4f;

    // ─── Riferimenti canvas (trovati sul prefab istanziato) ───────────────────
    private GameObject _loadingCanvasInstance;
    private CanvasGroup _loadingCanvasGroup;
    private CanvasGroup _fadeCanvasGroup;
    private TMP_Text _headerText;
    private TMP_Text _bodyText;
    private TMP_Text _premiInvioText;
    private Image _loadingBarFill;

    // ─── Stato ────────────────────────────────────────────────────────────────
    private bool _isTransitioning = false;

    // Flag: questa scena è stata caricata tramite portal (true)
    // o tramite reload diretto (false)?
    private static bool _isReload = false;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Istanzia il canvas loading e trovane i riferimenti
        if (loadingCanvasPrefab != null)
        {
            _loadingCanvasInstance = Instantiate(loadingCanvasPrefab);
            DontDestroyOnLoad(_loadingCanvasInstance);
            FindCanvasReferences();
            _loadingCanvasInstance.SetActive(false);
        }

        // Blocca input durante la loading screen
        GameManager.Instance?.SetPaused(true);

        // Avvia la loading screen all'avvio della scena
        StartCoroutine(SceneStartRoutine());
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_loadingCanvasInstance != null)
            Destroy(_loadingCanvasInstance);
    }

    // ─── Avvio scena ─────────────────────────────────────────────────────────
    private IEnumerator SceneStartRoutine()
    {
        // Mostra sempre la loading screen all'avvio della scena
        // Questo gestisce sia il primo avvio (MainMenu) che ogni livello
        yield return StartCoroutine(LoadingScreenRoutine());
    }

    // ─── Loading Screen all'avvio ────────────────────────────────────────────
    private IEnumerator LoadingScreenRoutine()
    {
        // Schermo parte nero (da FadeOut della scena precedente o dal primo avvio)
        if (_fadeCanvasGroup != null)
        {
            _fadeCanvasGroup.gameObject.SetActive(true);
            _fadeCanvasGroup.alpha = 1f;
            _fadeCanvasGroup.blocksRaycasts = true;
        }

        // Popola e mostra loading canvas
        PopulateCanvas(SceneManager.GetActiveScene().buildIndex);
        if (_loadingCanvasInstance != null)
            _loadingCanvasInstance.SetActive(true);

        // Avvia musica loading
        if (levelData?.loadingMusic != null)
            AudioManager.Instance?.PlayLooping(levelData.loadingMusic, fadeIn: false);

        // Fade in loading screen
        yield return StartCoroutine(FadeCanvas(0f, 1f));
        yield return StartCoroutine(Fade(1f, 0f));

        // Nascondi "Premi INVIO" finché non è pronto
        if (_premiInvioText != null) _premiInvioText.gameObject.SetActive(false);

        // Simula barra che si riempie nel minimumReadTime
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

        // Fade out loading screen
        yield return StartCoroutine(FadeCanvas(1f, 0f));
        if (_loadingCanvasInstance != null)
            _loadingCanvasInstance.SetActive(false);

        // Fade in scena
        yield return StartCoroutine(Fade(1f, 0f));

        // Avvia gameplay music
        if (levelData?.gameplayMusic != null)
            AudioManager.Instance?.PlayLooping(levelData.gameplayMusic, fadeIn: true);

        // Sblocca input
        GameManager.Instance?.SetPaused(false);
    }

    // ─── API pubblica — chiamata da PortalController ──────────────────────────
    public void LoadLevel(int targetSceneIndex)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;
        _isReload = false;
        StartCoroutine(TransitionRoutine(targetSceneIndex));
    }

    // ─── Transizione completa ─────────────────────────────────────────────────
    private IEnumerator TransitionRoutine(int targetIndex)
    {
        // Blocca input
        GameManager.Instance?.SetPaused(true);

        // Step 1 — Fade out
        yield return StartCoroutine(Fade(0f, 1f));

        // Step 2 — Popola e mostra loading canvas
        PopulateCanvas(targetIndex);
        if (_loadingCanvasInstance != null)
            _loadingCanvasInstance.SetActive(true);

        // Step 3 — Avvia musica loading
        if (levelData?.loadingMusic != null)
            AudioManager.Instance?.PlayLooping(levelData.loadingMusic, fadeIn: false);

        // Step 4 — Fade in loading screen
        yield return StartCoroutine(FadeCanvas(0f, 1f));

        // Nascondi "Premi INVIO" finché non è pronto
        if (_premiInvioText != null) _premiInvioText.gameObject.SetActive(false);

        // Step 5 — Carica scena in background
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetIndex);
        asyncLoad.allowSceneActivation = false;

        float elapsed = 0f;
        bool readyToActivate = false;

        // Step 6 — Attendi caricamento + tempo minimo
        while (!readyToActivate)
        {
            elapsed += Time.deltaTime;

            // Barra: il più lento tra progresso async e timer
            float asyncProgress = asyncLoad.progress / 0.9f;          // 0→1
            float timerProgress = elapsed / minimumReadTime;           // 0→1
            float barProgress = Mathf.Min(asyncProgress, timerProgress);

            if (_loadingBarFill != null)
                _loadingBarFill.fillAmount = Mathf.Clamp01(barProgress);

            if (asyncLoad.progress >= 0.9f && elapsed >= minimumReadTime)
                readyToActivate = true;

            yield return null;
        }

        // Barra al 100%
        if (_loadingBarFill != null) _loadingBarFill.fillAmount = 1f;

        // Step 7 — Mostra "Premi INVIO"
        if (_premiInvioText != null) _premiInvioText.gameObject.SetActive(true);

        // Step 8 — Attendi INVIO
        yield return StartCoroutine(WaitForEnter());

        // Step 9 — Stop loading music, avvia gameplay music
        AudioManager.Instance?.Stop(fadeOut: false);
        // La gameplay music parte in SceneStartRoutine() della nuova scena

        // Step 10 — Fade out loading screen
        yield return StartCoroutine(FadeCanvas(1f, 0f));
        if (_loadingCanvasInstance != null)
            _loadingCanvasInstance.SetActive(false);

        // Step 11 — Fade out schermo (nero prima di attivare scena)
        yield return StartCoroutine(Fade(0f, 1f));

        // Step 12 — Attiva scena
        GameManager.Instance?.SetPaused(false);
        asyncLoad.allowSceneActivation = true;
        // Il nuovo LevelManager farà FadeIn e avvierà la gameplayMusic
    }

    // ─── Popola canvas con CanvasContentData ──────────────────────────────────
    private void PopulateCanvas(int targetSceneIndex)
    {
        // Cerca il LevelData del livello di destinazione nel LevelManager
        // della scena corrente — usa il proprio levelData come fallback
        LevelData data = levelData;

        if (data?.canvasContent == null) return;

        CanvasContentData content = data.canvasContent;

        if (_headerText != null)
        {
            _headerText.text = $"<b><i>{content.header}</i></b>";
            _headerText.gameObject.SetActive(!string.IsNullOrEmpty(content.header));
        }

        if (_bodyText != null)
        {
            _bodyText.text = $"<i>{content.testo}</i>";
            _bodyText.gameObject.SetActive(!string.IsNullOrEmpty(content.testo));
        }

        if (_premiInvioText != null)
            _premiInvioText.text = content.premiInvio;
    }

    // ─── Trova riferimenti nel canvas istanziato ──────────────────────────────
    private void FindCanvasReferences()
    {
        if (_loadingCanvasInstance == null) return;

        // CanvasGroup sul root del canvas (per fade)
        _loadingCanvasGroup = _loadingCanvasInstance.GetComponent<CanvasGroup>();

        // FadePanel — figlio con nome "FadePanel"
        Transform fadePanel = _loadingCanvasInstance.transform.Find("FadePanel");
        if (fadePanel != null)
            _fadeCanvasGroup = fadePanel.GetComponent<CanvasGroup>();

        // Testi — cercati per nome
        _headerText = FindChild<TMP_Text>("HeaderText");
        _bodyText = FindChild<TMP_Text>("BodyText");
        _premiInvioText = FindChild<TMP_Text>("PremiInvioText");
        _loadingBarFill = FindChild<Image>("LoadingBarFill");

        if (_loadingBarFill == null)
            Debug.LogWarning("[LevelManager] LoadingBarFill non trovato nel canvas prefab.");
    }

    private T FindChild<T>(string childName) where T : Component
    {
        if (_loadingCanvasInstance == null) return null;
        Transform t = _loadingCanvasInstance.transform.Find(childName);
        if (t == null)
        {
            // Cerca in profondità
            foreach (T comp in _loadingCanvasInstance.GetComponentsInChildren<T>(true))
                if (comp.gameObject.name == childName) return comp;
            return null;
        }
        return t.GetComponent<T>();
    }

    // ─── Fade schermo (FadePanel) ─────────────────────────────────────────────
    private IEnumerator Fade(float from, float to)
    {
        if (_fadeCanvasGroup == null) yield break;

        _fadeCanvasGroup.gameObject.SetActive(true);
        _fadeCanvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        _fadeCanvasGroup.alpha = from;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            _fadeCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        _fadeCanvasGroup.alpha = to;
        if (to <= 0f) _fadeCanvasGroup.blocksRaycasts = false;
    }

    // ─── Fade canvas loading screen (CanvasGroup root) ────────────────────────
    private IEnumerator FadeCanvas(float from, float to)
    {
        if (_loadingCanvasGroup == null) yield break;

        float elapsed = 0f;
        _loadingCanvasGroup.alpha = from;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            _loadingCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        _loadingCanvasGroup.alpha = to;
    }

    // ─── Attendi INVIO ────────────────────────────────────────────────────────
    private IEnumerator WaitForEnter()
    {
        yield return null;   // flush frame

        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            Debug.LogWarning("[LevelManager] Nessuna tastiera rilevata.");
            yield break;
        }

        while (!keyboard.enterKey.wasPressedThisFrame &&
               !keyboard.numpadEnterKey.wasPressedThisFrame)
            yield return null;
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("DEBUG — Load Next Level")]
    private void Debug_LoadNext() =>
        LoadLevel(SceneManager.GetActiveScene().buildIndex + 1);
#endif
}