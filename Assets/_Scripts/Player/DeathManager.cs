using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Cinemachine;

/// <summary>
/// DANTE: DIVINE'S FALL — DeathManager.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Gestisce la sequenza di morte per caduta libera:
///   1. Dante cade per fallDeathTime secondi
///   2. Camera si stacca dal FreeLook e segue Dante dall'alto guardando verso il basso
///   3. Fade nero
///   4. Canvas di morte con bottoni Riprendi / Main Menu
///
/// STRUTTURA PREFAB DANTE:
///   Dante (root)
///   ├── DeathManager        ← questo script
///   └── DeathCanvas         ← Canvas (Screen Space Overlay)
///       └── Panel
///           ├── TitleText   "SEI MORTO"
///           ├── BtnRiprendi
///           └── BtnMainMenu
///
/// INSPECTOR:
///   deathCanvas        → Canvas di morte figlio di Dante
///   btnRiprendi        → bottone Riprendi
///   btnMainMenu        → bottone Main Menu
///   freeLookCamera     → CM FreeLook1 nella scena (trovato automaticamente se null)
///   fallDeathTime      → secondi di caduta prima della sequenza (default 3)
///   cameraDetachSpeed  → velocità con cui la camera sale e guarda in basso (default 2)
///   fadeDuration       → durata fade nero (default 1)
/// </summary>
public class DeathManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject deathCanvas;
    public Button btnRiprendi;
    public Button btnMainMenu;

    [Header("Camera")]
    [Tooltip("CM FreeLook1 — trovato automaticamente se lasciato vuoto.")]
    public CinemachineFreeLook freeLookCamera;

    [Tooltip("Offset verticale della camera sopra Dante durante la sequenza cinematica.")]
    public float cameraHeightOffset = 3f;

    [Tooltip("Velocità con cui la camera segue Dante durante la caduta.")]
    public float cameraFollowSpeed = 2f;

    [Header("Timing")]
    [Tooltip("Secondi di caduta libera prima di triggerare la sequenza di morte.")]
    public float fallDeathTime = 3f;

    [Tooltip("Durata del fade nero in secondi.")]
    public float fadeDuration = 1f;

    [Header("Fade")]
    [Tooltip("CanvasGroup del pannello nero per il fade — cercato nel DeathCanvas se null.")]
    public CanvasGroup fadeCanvasGroup;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private PlayerController _playerController;
    private Health _health;
    private CanvasGroup _deathCanvasGroup;
    private Camera _mainCamera;

    private float _fallTimer = 0f;
    private bool _isFalling = false;
    private bool _sequenceStarted = false;
    private bool _isDead = false;

    // Posizione e rotazione della camera al momento del distacco
    private Vector3 _detachedCamPos;
    private Quaternion _detachedCamRot;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _playerController = GetComponentInParent<PlayerController>();
        _health = GetComponentInParent<Health>();
        _mainCamera = Camera.main;

        // Setup DeathCanvas
        if (deathCanvas != null)
        {
            _deathCanvasGroup = deathCanvas.GetComponent<CanvasGroup>();
            if (_deathCanvasGroup == null)
                _deathCanvasGroup = deathCanvas.AddComponent<CanvasGroup>();

            _deathCanvasGroup.alpha = 0f;
            _deathCanvasGroup.interactable = false;
            _deathCanvasGroup.blocksRaycasts = false;
            deathCanvas.SetActive(false);
        }

        // Setup FadePanel — cerca dentro DeathCanvas se non assegnato
        if (fadeCanvasGroup == null && deathCanvas != null)
        {
            Transform fadePanel = deathCanvas.transform.Find("FadePanel");
            if (fadePanel != null)
                fadeCanvasGroup = fadePanel.GetComponent<CanvasGroup>();
        }
    }

    void Start()
    {
        // Trova FreeLook automaticamente se non assegnato
        if (freeLookCamera == null)
            freeLookCamera = FindObjectOfType<CinemachineFreeLook>();

        btnRiprendi?.onClick.AddListener(RestartLevel);
        btnMainMenu?.onClick.AddListener(GoToMainMenu);
    }

    void OnDestroy()
    {
        btnRiprendi?.onClick.RemoveListener(RestartLevel);
        btnMainMenu?.onClick.RemoveListener(GoToMainMenu);
    }

    [Tooltip("Velocità verticale negativa minima per iniziare a contare la caduta. " +
             "Aumenta se il terreno irregolare causa falsi positivi (default -8).")]
    public float fallVelocityThreshold = -8f;

    // Terrain layer gestito da PlayerController.IsOverTerrain

    void Update()
    {
        if (_sequenceStarted || _isDead) return;
        if (_playerController == null) return;

        // Caduta libera reale: non a terra E velocità Y sotto la soglia
        bool falling = !_playerController.IsGrounded &&
                       _playerController.VerticalVelocity < fallVelocityThreshold;

        if (falling)
        {
            // Se sotto Dante c'è un Terrain — nessuna morte per caduta
            if (_playerController.IsOverTerrain) { _fallTimer = 0f; return; }
            _fallTimer += Time.deltaTime;
            if (_fallTimer >= fallDeathTime)
            {
                _sequenceStarted = true;
                StartCoroutine(DeathSequence());
            }
        }
        else
        {
            _fallTimer = 0f;
        }
    }

    // ─── Sequenza di morte ────────────────────────────────────────────────────
    private IEnumerator DeathSequence()
    {
        _isDead = true;

        // Blocca il movimento di Dante
        if (_playerController != null)
            _playerController.IsAbilityCasting = true;

        // Disabilita FreeLook — la Main Camera si muove manualmente
        if (freeLookCamera != null)
            freeLookCamera.gameObject.SetActive(false);

        // Posizione iniziale della camera — sopra Dante
        if (_mainCamera != null)
        {
            _detachedCamPos = _mainCamera.transform.position;
            _detachedCamRot = _mainCamera.transform.rotation;
        }

        // ── Step 1: Camera segue Dante dall'alto per fadeDuration secondi ────
        float elapsed = 0f;
        float cinematicDuration = fadeDuration * 0.8f;

        while (elapsed < cinematicDuration)
        {
            elapsed += Time.deltaTime;

            if (_mainCamera != null && _playerController != null)
            {
                // Posizione target — sopra Dante
                Vector3 targetPos = _playerController.transform.position +
                                    Vector3.up * cameraHeightOffset;

                // Rotazione target — guarda verso il basso su Dante
                Quaternion targetRot = Quaternion.LookRotation(
                    _playerController.transform.position - _mainCamera.transform.position);

                // Lerp fluido verso la posizione target
                _mainCamera.transform.position = Vector3.Lerp(
                    _mainCamera.transform.position, targetPos,
                    cameraFollowSpeed * Time.deltaTime);

                _mainCamera.transform.rotation = Quaternion.Slerp(
                    _mainCamera.transform.rotation, targetRot,
                    cameraFollowSpeed * Time.deltaTime);
            }

            yield return null;
        }

        // ── Step 2: Fade nero ─────────────────────────────────────────────────
        yield return StartCoroutine(FadeToBlack());

        // ── Step 3: Mostra canvas di morte ────────────────────────────────────
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;

        ShowDeathCanvas();
    }

    // ─── Fade ─────────────────────────────────────────────────────────────────
    private IEnumerator FadeToBlack()
    {
        if (fadeCanvasGroup == null) yield break;

        // Attiva il pannello nero
        if (fadeCanvasGroup.gameObject != deathCanvas)
            fadeCanvasGroup.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 1f;
    }

    // ─── Death Canvas ─────────────────────────────────────────────────────────
    private void ShowDeathCanvas()
    {
        if (deathCanvas == null) return;

        deathCanvas.SetActive(true);

        if (_deathCanvasGroup != null)
        {
            _deathCanvasGroup.alpha = 1f;
            _deathCanvasGroup.interactable = true;
            _deathCanvasGroup.blocksRaycasts = true;
        }

        // Avvia musica di morte
        LevelData data = LevelManager.Instance?.levelData;
        if (data?.deathMusic != null)
            AudioManager.Instance?.PlayLooping(data.deathMusic, fadeIn: true);
    }

    // ─── Bottoni ──────────────────────────────────────────────────────────────
    public void RestartLevel()
    {
        AudioManager.Instance?.Stop(fadeOut: false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        AudioManager.Instance?.Stop(fadeOut: false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(0);
    }
}