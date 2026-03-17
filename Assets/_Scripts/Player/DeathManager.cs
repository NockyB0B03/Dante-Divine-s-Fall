using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Cinemachine;

/// <summary>
/// DANTE: DIVINE'S FALL — DeathManager.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Gestisce due tipi di morte:
///
/// MORTE PER CADUTA:
///   Dante cade per fallDeathTime secondi con velocità < fallVelocityThreshold
///   → Camera si stacca dal FreeLook e segue Dante dall'alto
///   → Fade nero → DeathCanvas
///
/// MORTE PER HP (chiamata da Health.cs):
///   Health chiama OnHealthDeath() pubblico
///   → Camera si blocca + zoom out verso l'alto per deathAnimDuration secondi
///   → Fade nero → DeathCanvas
///
/// DEATH CANVAS:
///   - Gioco in pausa (Time.timeScale = 0)
///   - Cursore visibile
///   - deathMusic dal LevelData tramite AudioManager
///   - BtnRetry: riprova il livello (timer continua)
///   - BtnMainMenu: torna al MainMenu (timer azzerato)
///
/// SETUP NEL PREFAB DANTE:
///   Dante
///   ├── DeathManager.cs      ← su GameObject figlio "DeathManager"
///   └── DeathCanvas          ← Canvas SO Overlay, Sort Order 100, disabilitato
///       ├── FadePanel        ← Image nera fullscreen + CanvasGroup (alpha 0)
///       └── Panel
///           ├── TitleText    "SEI MORTO"
///           ├── BtnRetry
///           └── BtnMainMenu
///
/// COLLEGA HEALTH A DEATHMANAGER:
///   Su Health.cs → OnDeath UnityEvent → DeathManager.OnHealthDeath()
///
/// INSPECTOR:
///   deathCanvas          → Canvas di morte figlio di Dante
///   fadeCanvasGroup      → CanvasGroup di FadePanel
///   btnRetry             → bottone Riprova
///   btnMainMenu          → bottone Main Menu
///   fallDeathTime        → secondi caduta prima della morte (default 3)
///   fallVelocityThreshold→ velocità Y minima per contare caduta (default -8)
///   deathAnimDuration    → secondi di animazione morte da HP prima del fade (default 2)
///   cameraZoomOutSpeed   → velocità zoom out camera durante morte da HP (default 1.5)
///   cameraZoomOutHeight  → unità di zoom out verso l'alto (default 4)
///   cameraFollowSpeed    → velocità camera che segue Dante in caduta (default 2)
///   cameraHeightOffset   → offset verticale camera durante caduta (default 3)
///   fadeDuration         → durata fade nero (default 1)
/// </summary>
public class DeathManager : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("UI")]
    public GameObject deathCanvas;
    public CanvasGroup fadeCanvasGroup;
    public Button btnRetry;
    public Button btnMainMenu;

    [Header("Caduta")]
    [Tooltip("Secondi di caduta libera prima della morte.")]
    public float fallDeathTime = 3f;
    [Tooltip("Velocità Y negativa minima per contare come caduta.")]
    public float fallVelocityThreshold = -8f;
    [Tooltip("Velocità con cui la camera segue Dante durante la caduta.")]
    public float cameraFollowSpeed = 2f;
    [Tooltip("Offset verticale della camera sopra Dante durante la caduta.")]
    public float cameraHeightOffset = 3f;

    [Header("Morte da HP")]
    [Tooltip("Secondi di animazione morte prima del fade (tempo per animazione Death).")]
    public float deathAnimDuration = 2f;
    [Tooltip("Velocità zoom out della camera verso l'alto durante morte da HP.")]
    public float cameraZoomOutSpeed = 1.5f;
    [Tooltip("Unità di zoom out verso l'alto della camera durante morte da HP.")]
    public float cameraZoomOutHeight = 4f;

    [Header("Timing")]
    public float fadeDuration = 1f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private PlayerController _playerController;
    private CinemachineFreeLook _freeLook;
    private Camera _mainCamera;
    private CanvasGroup _deathCanvasGroup;

    private float _fallTimer = 0f;
    private bool _sequenceStarted = false;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _playerController = GetComponentInParent<PlayerController>();
        _mainCamera = Camera.main;

        // Setup DeathCanvas — parte disabilitato
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

        // FadePanel parte invisibile
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        _freeLook = FindObjectOfType<CinemachineFreeLook>();

        btnRetry?.onClick.AddListener(OnRetry);
        btnMainMenu?.onClick.AddListener(OnMainMenu);
    }

    void OnDestroy()
    {
        btnRetry?.onClick.RemoveListener(OnRetry);
        btnMainMenu?.onClick.RemoveListener(OnMainMenu);
    }

    // ─── Update — rilevamento caduta ──────────────────────────────────────────
    void Update()
    {
        if (_sequenceStarted) return;
        if (_playerController == null) return;

        bool falling = !_playerController.IsGrounded &&
                       _playerController.VerticalVelocity < fallVelocityThreshold &&
                       !_playerController.IsOverTerrain;

        if (falling)
        {
            _fallTimer += Time.deltaTime;
            if (_fallTimer >= fallDeathTime)
                TriggerFallDeath();
        }
        else
        {
            _fallTimer = 0f;
        }
    }

    // ─── API pubblica — chiamata da Health.OnDeath ────────────────────────────
    /// <summary>
    /// Chiamato da Health.cs tramite OnDeath UnityEvent quando gli HP arrivano a 0.
    /// Collega in Inspector: Health → OnDeath → DeathManager.OnHealthDeath
    /// </summary>
    public void OnHealthDeath()
    {
        if (_sequenceStarted) return;
        _sequenceStarted = true;
        StartCoroutine(HealthDeathSequence());
    }

    // ─── Trigger caduta ───────────────────────────────────────────────────────
    private void TriggerFallDeath()
    {
        if (_sequenceStarted) return;
        _sequenceStarted = true;
        StartCoroutine(FallDeathSequence());
    }

    // ─── Sequenza morte per caduta ────────────────────────────────────────────
    private IEnumerator FallDeathSequence()
    {
        // Blocca input Dante
        if (_playerController != null)
            _playerController.IsAbilityCasting = true;

        // Disabilita il componente FreeLook (non il GameObject) —
        // evita MissingReferenceException sui rig interni di Cinemachine
        if (_freeLook != null)
            _freeLook.enabled = false;

        // Camera segue Dante dall'alto per cinematicDuration secondi
        float elapsed = 0f;
        float cinematicDuration = fadeDuration * 0.8f;

        while (elapsed < cinematicDuration)
        {
            elapsed += Time.deltaTime;

            if (_mainCamera != null && _playerController != null)
            {
                Vector3 targetPos = _playerController.transform.position +
                                    Vector3.up * cameraHeightOffset;
                Quaternion targetRot = Quaternion.LookRotation(
                    _playerController.transform.position - _mainCamera.transform.position);

                _mainCamera.transform.position = Vector3.Lerp(
                    _mainCamera.transform.position, targetPos,
                    cameraFollowSpeed * Time.deltaTime);
                _mainCamera.transform.rotation = Quaternion.Slerp(
                    _mainCamera.transform.rotation, targetRot,
                    cameraFollowSpeed * Time.deltaTime);
            }
            yield return null;
        }

        yield return StartCoroutine(FadeAndShowCanvas());
    }

    // ─── Sequenza morte per HP ────────────────────────────────────────────────
    private IEnumerator HealthDeathSequence()
    {
        // Blocca input Dante
        if (_playerController != null)
            _playerController.IsAbilityCasting = true;

        // Disabilita il componente FreeLook (non il GameObject)
        if (_freeLook != null)
            _freeLook.enabled = false;

        // Zoom out verso l'alto della camera
        if (_mainCamera != null)
        {
            Vector3 startPos = _mainCamera.transform.position;
            Vector3 targetPos = startPos + Vector3.up * cameraZoomOutHeight;
            float elapsed = 0f;

            while (elapsed < deathAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / deathAnimDuration);
                _mainCamera.transform.position = Vector3.Lerp(
                    startPos, targetPos, t * cameraZoomOutSpeed * Time.deltaTime);
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(deathAnimDuration);
        }

        yield return StartCoroutine(FadeAndShowCanvas());
    }

    // ─── Fade + canvas ────────────────────────────────────────────────────────
    private IEnumerator FadeAndShowCanvas()
    {
        // Fade a nero
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.blocksRaycasts = true;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        // Pausa, cursore, canvas
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Avvia deathMusic da LevelData
        LevelData data = LevelManager.Instance?.levelData;
        if (data?.deathMusic != null)
            AudioManager.Instance?.PlayLooping(data.deathMusic, fadeIn: true);

        // Mostra DeathCanvas
        if (deathCanvas != null)
        {
            deathCanvas.SetActive(true);
            if (_deathCanvasGroup != null)
            {
                _deathCanvasGroup.alpha = 1f;
                _deathCanvasGroup.interactable = true;
                _deathCanvasGroup.blocksRaycasts = true;
            }
        }
    }

    // ─── Bottoni ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Riprova il livello — timer di gioco continua (non viene azzerato).
    /// </summary>
    public void OnRetry()
    {
        AudioManager.Instance?.Stop(fadeOut: false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        // Reload scena — GameManager.OnSceneLoaded mantiene il timer
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Torna al MainMenu — timer azzerato tramite GameManager.ReturnToMainMenu().
    /// </summary>
    public void OnMainMenu()
    {
        AudioManager.Instance?.Stop(fadeOut: false);
        Time.timeScale = 1f;
        GameManager.Instance?.ReturnToMainMenu();
    }
}