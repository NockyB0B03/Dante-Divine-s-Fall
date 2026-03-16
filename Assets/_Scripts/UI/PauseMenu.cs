using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// DANTE: DIVINE'S FALL — PauseMenu.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Vive come figlio di Dante nel prefab insieme al PauseCanvas.
/// Non serve creare nulla in scena — tutto è dentro il prefab.
///
/// STRUTTURA PREFAB DANTE:
///   Dante (root)
///   ├── ...
///   ├── PauseMenu          ← questo script
///   └── PauseCanvas        ← Canvas (Screen Space Overlay)
///       └── Panel
///           ├── TitleText
///           ├── BtnRiprendi
///           ├── BtnRicomincia
///           └── BtnMainMenu
///
/// SETUP PAUSECANVAS:
///   Canvas → Render Mode: Screen Space - Overlay, Sort Order: 50
///   Aggiungi CanvasGroup al GameObject PauseCanvas
///   Disabilita PauseCanvas di default (spunta off)
///
/// INSPECTOR su PauseMenu:
///   pauseCanvas      → trascina PauseCanvas (figlio di Dante)
///   btnRiprendi      → trascina BtnRiprendi
///   btnRicomincia    → trascina BtnRicomincia
///   btnMainMenu      → trascina BtnMainMenu
///   fadeDuration     → durata fade (default 0.2)
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("References")]
    [Tooltip("GameObject PauseCanvas — figlio di Dante nel prefab.")]
    public GameObject pauseCanvas;

    [Tooltip("Bottone Riprendi.")]
    public Button btnRiprendi;

    [Tooltip("Bottone Ricomincia.")]
    public Button btnRicomincia;

    [Tooltip("Bottone Main Menu.")]
    public Button btnMainMenu;

    [Tooltip("Bottone Settings.")]
    public Button btnSettings;

    [Header("Settings")]
    public float fadeDuration = 0.2f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private PlayerInputActions _input;
    private CanvasGroup _canvasGroup;
    private bool _isPaused = false;
    private Coroutine _fadeRoutine;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _input = new PlayerInputActions();

        if (pauseCanvas != null)
        {
            _canvasGroup = pauseCanvas.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                Debug.LogError("[PauseMenu] PauseCanvas non ha un CanvasGroup!");

            // Parte nascosto
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
            pauseCanvas.SetActive(false);
        }
        else
        {
            Debug.LogError("[PauseMenu] pauseCanvas non assegnato in Inspector!");
        }
    }

    void OnEnable()
    {
        _input.Enable();
        _input.Player.Pause.performed += OnPauseInput;

        btnRiprendi?.onClick.AddListener(Resume);
        btnRicomincia?.onClick.AddListener(RestartLevel);
        btnMainMenu?.onClick.AddListener(GoToMainMenu);
        btnSettings?.onClick.AddListener(OnSettingsPressed);
    }

    void OnDisable()
    {
        _input.Player.Pause.performed -= OnPauseInput;
        _input.Disable();

        btnRiprendi?.onClick.RemoveListener(Resume);
        btnRicomincia?.onClick.RemoveListener(RestartLevel);
        btnMainMenu?.onClick.RemoveListener(GoToMainMenu);
        btnSettings?.onClick.RemoveListener(OnSettingsPressed);
    }

    // ─── Settings ────────────────────────────────────────────────────────────
    private void OnSettingsPressed()
    {
        SettingsMenu.Instance?.Open();
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
            pauseCanvas.SetActive(true);
            _canvasGroup.alpha = 0f;
        }

        float startAlpha = fadeIn ? 0f : 1f;
        float targetAlpha = fadeIn ? 1f : 0f;
        float elapsed = 0f;

        // unscaledDeltaTime — funziona con timeScale = 0
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha,
                                             elapsed / fadeDuration);
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
            pauseCanvas.SetActive(false);
        }

        _fadeRoutine = null;
    }
}