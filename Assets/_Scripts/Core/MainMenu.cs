using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// DANTE: DIVINE'S FALL — MainMenu.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Gestisce il Main Menu — un canvas con un solo bottone "Inizia".
/// Quando premuto: fade out → carica Level 1 (scene index 1).
///
/// SETUP IN SCENA (MainMenu, scene index 0):
///   1. Crea Canvas (Screen Space Overlay, Sort Order 0)
///   2. Aggiungi CanvasGroup al Canvas
///   3. Struttura:
///      MainMenuCanvas
///      ├── FadePanel     ← Image nera fullscreen, CanvasGroup separato per il fade
///      └── Panel
///          ├── TitleText ← "DANTE: DIVINE'S FALL"
///          └── BtnInizio ← bottone "Inizia"
///   4. Aggiungi MainMenu.cs su un GameObject vuoto "MainMenu"
///   5. Collega i campi in Inspector
///
/// INSPECTOR:
///   btnInizio        → bottone Inizia
///   fadeCanvasGroup  → CanvasGroup del FadePanel (Image nera fullscreen)
///   fadeDuration     → durata fade out in secondi (default 0.6)
///   targetSceneIndex → scene index del Level 1 (default 1)
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Bottone Inizia.")]
    public Button btnInizio;

    [Tooltip("CanvasGroup del pannello nero fullscreen — guidato dal fade.")]
    public CanvasGroup fadeCanvasGroup;

    [Header("Settings")]
    [Tooltip("Durata del fade out in secondi.")]
    public float fadeDuration = 0.6f;

    [Tooltip("Build index della scena del Level 1.")]
    public int targetSceneIndex = 1;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Start()
    {
        // Parte trasparente — fade in opzionale all'apertura del menu
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        // Mostra il cursore nel menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        btnInizio?.onClick.AddListener(OnInizioPressed);
    }

    void OnDestroy()
    {
        btnInizio?.onClick.RemoveListener(OnInizioPressed);
    }

    // ─── Bottone ──────────────────────────────────────────────────────────────
    private void OnInizioPressed()
    {
        // Disabilita il bottone per evitare doppio click
        if (btnInizio != null)
            btnInizio.interactable = false;

        StartCoroutine(FadeAndLoad());
    }

    // ─── Fade + Load ──────────────────────────────────────────────────────────
    private IEnumerator FadeAndLoad()
    {
        // Fade out — schermo diventa nero
        if (fadeCanvasGroup != null)
        {
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
        else
        {
            // Nessun fade configurato — piccola pausa per feedback visivo
            yield return new WaitForSeconds(0.1f);
        }

        // Carica Level 1
        SceneManager.LoadScene(targetSceneIndex);
    }
}