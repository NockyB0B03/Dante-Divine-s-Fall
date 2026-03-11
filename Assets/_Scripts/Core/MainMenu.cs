using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DANTE: DIVINE'S FALL — MainMenu.cs
/// ─────────────────────────────────────
/// Gestisce il Main Menu: avvia il gioco tramite GameManager.StartGame()
/// sia alla pressione del tasto Enter/Submit sia al click del bottone Start sul Canvas.
///
/// Setup in scena:
///   1. Aggancia questo script a un GameObject vuoto (es. "MainMenuController").
///   2. Collega il bottone Start del Canvas all'evento onClick → MainMenu.OnStartButtonPressed().
/// </summary>
public class MainMenu : MonoBehaviour
{
    // ─── Input ────────────────────────────────────────────────────────────────
    private PlayerInputActions _input;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    private void Awake()
    {
        _input = new PlayerInputActions();
    }

    private void OnEnable()
    {
        _input.Enable();
        _input.UI.Submit.performed += OnSubmitPressed;   // Enter / South button
    }

    private void OnDisable()
    {
        _input.UI.Submit.performed -= OnSubmitPressed;
        _input.Disable();
    }

    // ─── Input callback ───────────────────────────────────────────────────────
    private void OnSubmitPressed(InputAction.CallbackContext ctx) => StartGame();

    // ─── Bottone Canvas ───────────────────────────────────────────────────────
    /// <summary>
    /// Collega questo metodo all'evento OnClick del bottone Start nel Canvas.
    /// </summary>
    public void OnStartButtonPressed() => StartGame();

    // ─── Core ─────────────────────────────────────────────────────────────────
    private void StartGame()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[MainMenu] GameManager non trovato in scena!");
            return;
        }

        GameManager.Instance.StartGame();
    }
}
