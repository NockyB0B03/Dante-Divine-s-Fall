using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DANTE: DIVINE'S FALL — MainMenu.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Gestisce il Main Menu — un canvas con un solo bottone "Inizia".
/// La loading screen prima del MainMenu è gestita da LevelManager.Awake().
/// Quando si preme Inizia chiama LevelManager.LoadLevel(1) — tutto il flusso
/// loading screen + audio + fade è gestito da LevelManager.
///
/// SETUP SCENA MainMenu (index 0):
///   GameManager          ← DontDestroyOnLoad
///   AudioManager         ← DontDestroyOnLoad
///   LevelManager         ← levelData = LevelData_MainMenu
///                           loadingCanvasPrefab assegnato
///   EventSystem          ← Input System UI Input Module
///   MainMenuCanvas       ← Canvas SO Overlay — parte disabilitato
///   └── Panel
///       ├── TitleText
///       └── BtnInizio
///   MainMenu             ← questo script
///
/// LevelData_MainMenu deve avere:
///   loadingMusic  → musica durante la loading screen iniziale
///   gameplayMusic → musica del main menu
///   canvasContent → header/body/premiInvio per la loading screen iniziale
///
/// INSPECTOR:
///   btnInizio     → bottone Inizia
///   mainMenuCanvas → GameObject del canvas principale — abilitato dopo la loading screen
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("UI")]
    public Button btnInizio;

    [Tooltip("Canvas del main menu — disabilitato di default, abilitato dopo la loading screen.")]
    public GameObject mainMenuCanvas;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        // Canvas parte disabilitato — LevelManager lo abilita dopo la loading screen
        if (mainMenuCanvas != null)
            mainMenuCanvas.SetActive(false);
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        btnInizio?.onClick.AddListener(OnInizioPressed);

        // Ascolta la fine della loading screen per abilitare il canvas
        StartCoroutine(WaitForLoadingScreenEnd());
    }

    void OnDestroy()
    {
        btnInizio?.onClick.RemoveListener(OnInizioPressed);
    }

    // ─── Attendi fine loading screen ─────────────────────────────────────────
    /// <summary>
    /// LevelManager.SceneStartRoutine chiama SetPaused(false) alla fine.
    /// Aspettiamo che GameManager esca dallo stato Paused per abilitare il canvas.
    /// </summary>
    private System.Collections.IEnumerator WaitForLoadingScreenEnd()
    {
        // Attendi che GameManager esista
        while (GameManager.Instance == null)
            yield return null;

        // Attendi che la loading screen finisca (stato torna a Playing o MainMenu)
        while (GameManager.Instance.CurrentState == GameManager.GameState.Paused)
            yield return null;

        // Abilita il canvas del main menu
        if (mainMenuCanvas != null)
            mainMenuCanvas.SetActive(true);
    }

    // ─── Bottone ──────────────────────────────────────────────────────────────
    private void OnInizioPressed()
    {
        if (btnInizio != null)
            btnInizio.interactable = false;

        if (LevelManager.Instance != null)
            LevelManager.Instance.LoadLevel(1);
        else
            Debug.LogError("[MainMenu] LevelManager non trovato!");
    }
}