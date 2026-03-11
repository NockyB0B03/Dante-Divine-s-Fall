using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// DANTE: DIVINE'S FALL — PauseMenu.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Vive come figlio di Dante nel prefab — viene istanziato con lui.
/// Trova il PauseCanvas nella scena tramite tag "PauseCanvas" in Start().
///
/// SETUP IN OGNI SCENA:
///   1. Crea Canvas → rinominalo "PauseCanvas" → tag "PauseCanvas"
///   2. Aggiungi CanvasGroup al Canvas
///   3. Struttura figli:
///      PauseCanvas
///      └── Panel
///          ├── TitleText
///          ├── BtnRiprendi    → OnClick: PauseMenu.Resume()
///          ├── BtnRicomincia  → OnClick: PauseMenu.RestartLevel()
///          └── BtnMainMenu    → OnClick: PauseMenu.GoToMainMenu()
///   4. Disabilita PauseCanvas di default
///
/// PREFAB DANTE:
///   Dante
///   └── PauseMenu  ← questo script, nessun canvas figlio
///
/// INSPECTOR:
///   fadeDuration → durata fade (default 0.2)
///   btnRiprendi, btnRicomincia, btnMainMenu → bottoni del canvas
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Settings")]
    public float fadeDuration = 0.2f;

    [Header("Bottoni")]
    public UnityEngine.UI.Button btnRiprendi;
    public UnityEngine.UI.Button btnRicomincia;
    public UnityEngine.UI.Button btnMainMenu;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private PlayerInputActions _input;
    private CanvasGroup _canvasGroup;
    private bool _isPaused = false;
    private Coroutine _fadeRoutine;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _input = new PlayerInputActions();
    }

    void Start()
    {
        // Trova il PauseCanvas nella scena tramite tag
        GameObject canvasGO = GameObject.FindWithTag("PauseCanvas");
        if (canvasGO != null)
        {
            _canvasGroup = canvasGO.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                Debug.LogError("[PauseMenu] PauseCanvas non ha un CanvasGroup!");

            // Parte nascosto
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            canvasGO.SetActive(false);
        }
        else
        {
            Debug.LogError("[PauseMenu] Nessun GameObject con tag 'PauseCanvas' trovato in scena!");
        }

        // Trova i bottoni tramite nome se non assegnati in Inspector
        if (_canvasGroup != null)
        {
            Transform panel = _canvasGroup.transform.GetComponentInChildren<UnityEngine.UI.Button>()?.transform.parent;
            if (btnRiprendi == null) btnRiprendi = FindButton("BtnRiprendi");
            if (btnRicomincia == null) btnRicomincia = FindButton("BtnRicomincia");
            if (btnMainMenu == null) btnMainMenu = FindButton("BtnMainMenu");
        }

        btnRiprendi?.onClick.AddListener(Resume);
        btnRicomincia?.onClick.AddListener(RestartLevel);
        btnMainMenu?.onClick.AddListener(GoToMainMenu);
    }

    void OnEnable()
    {
        _input.Enable();
        _input.Player.Pause.performed += OnPauseInput;
    }

    void OnDisable()
    {
        _input.Player.Pause.performed -= OnPauseInput;
        _input.Disable();
    }

    // ─── Input ────────────────────────────────────────────────────────────────
    private void OnPauseInput(InputAction.CallbackContext ctx)
    {
        if (_isPaused) Resume();
        else Pause();
    }

    // ─── API pubblica ─────────────────────────────────────────────────────────
    public void Pause()
    {
        if (_isPaused) return;
        _isPaused = true;

        GameManager.Instance?.SetPaused(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeCanvas(true));
    }

    public void Resume()
    {
        if (!_isPaused) return;
        _isPaused = false;

        GameManager.Instance?.SetPaused(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeCanvas(false));
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        GameManager.Instance?.RestartCurrentLevel();
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameManager.Instance?.SetPaused(false);
        SceneManager.LoadScene(0);
    }

    // ─── Fade ─────────────────────────────────────────────────────────────────
    private IEnumerator FadeCanvas(bool fadeIn)
    {
        if (_canvasGroup == null) yield break;

        if (fadeIn)
        {
            _canvasGroup.gameObject.SetActive(true);
            _canvasGroup.alpha = 0f;
        }

        float startAlpha = fadeIn ? 0f : 1f;
        float targetAlpha = fadeIn ? 1f : 0f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;   // unscaled — funziona con timeScale=0
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;

        if (fadeIn)
        {
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }
        else
        {
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.gameObject.SetActive(false);
        }

        _fadeRoutine = null;
    }

    // ─── Utility ──────────────────────────────────────────────────────────────
    private UnityEngine.UI.Button FindButton(string buttonName)
    {
        GameObject go = GameObject.Find(buttonName);
        if (go == null)
        {
            Debug.LogWarning($"[PauseMenu] Bottone '{buttonName}' non trovato in scena. " +
                             "Assegnalo manualmente in Inspector.");
            return null;
        }
        return go.GetComponent<UnityEngine.UI.Button>();
    }
}