using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Cinemachine;

/// <summary>
/// DANTE: DIVINE'S FALL — CutsceneManager.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Gestisce il finale del gioco (Level 6):
///   1. Dante spawna ma non può muoversi
///   2. FreeLook si disabilita — camera si muove manualmente
///   3. Camera parte da dietro Dante guardando in basso, si alza guardando avanti
///   4. Fade in canvas titoli di coda
///   5. TMP_Text scorre dal basso verso l'alto tramite anchoredPosition
///   6. Bottone "Torna al MainMenu" + timer di sicurezza
///
/// SETUP CANVAS (semplice, senza ScrollRect):
///   CreditsCanvas        ← Canvas SO Overlay, Sort Order 50, disabilitato
///   ├── Background       ← Image nera fullscreen
///   ├── FadePanel        ← Image nera fullscreen + CanvasGroup (per fade in)
///   ├── CreditsText      ← TMP_Text con tutto il testo, Overflow=Overflow
///   └── BtnMainMenu      ← Button, disabilitato di default
///
/// CreditsText RectTransform:
///   Anchor  → bottom-center (0.5, 0)
///   Pivot   → (0.5, 0)
///   Width   → larghezza canvas
///   Height  → abbastanza grande da contenere tutto il testo (es. 2000)
///
/// INSPECTOR:
///   cameraStartOffset → offset camera da Dante all'inizio (es. 0, 1.5, -3)
///   cameraEndOffset   → offset camera finale (es. 0, 8, -10)
///   cameraDuration    → durata movimento camera (default 4)
///   creditsCanvas     → GameObject canvas credits
///   fadeCanvasGroup   → CanvasGroup FadePanel
///   creditsTextRect   → RectTransform del TMP_Text
///   viewportHeight    → altezza dello schermo in pixel (es. 1080)
///   btnMainMenu       → bottone torna al menu
///   scrollDuration    → durata scroll (default 12)
///   autoReturnDelay   → secondi prima del ritorno automatico (default 5)
///   fadeDuration      → durata fade in canvas (default 1)
/// </summary>
public class CutsceneManager : MonoBehaviour
{
    [Header("Camera")]
    public Vector3 cameraStartOffset = new Vector3(0f, 1.5f, -3f);
    public Vector3 cameraEndOffset = new Vector3(0f, 8f, -10f);
    public float cameraDuration = 4f;
    public AnimationCurve cameraEaseOut = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Credits")]
    public GameObject creditsCanvas;
    public CanvasGroup fadeCanvasGroup;
    public RectTransform creditsTextRect;

    [Tooltip("Altezza viewport in pixel — uguale alla Reference Resolution Y del CanvasScaler.")]
    public float viewportHeight = 1080f;

    public Button btnMainMenu;

    [Header("Timing")]
    public float scrollDuration = 12f;
    public float autoReturnDelay = 5f;
    public float fadeDuration = 1f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private Camera _mainCamera;
    private CinemachineFreeLook _freeLook;
    private PlayerController _playerController;
    private bool _returned = false;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Start()
    {
        _mainCamera = Camera.main;
        _freeLook = FindObjectOfType<CinemachineFreeLook>();

        if (creditsCanvas != null) creditsCanvas.SetActive(false);
        if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 0f;
        if (btnMainMenu != null) btnMainMenu.gameObject.SetActive(false);

        btnMainMenu?.onClick.AddListener(ReturnToMainMenu);

        StartCoroutine(WaitForPlayerAndStart());
    }

    void OnDestroy()
    {
        btnMainMenu?.onClick.RemoveListener(ReturnToMainMenu);
    }

    // ─── Attendi Dante ────────────────────────────────────────────────────────
    private IEnumerator WaitForPlayerAndStart()
    {
        while (GameManager.Instance?.Player == null)
            yield return null;

        _playerController = GameManager.Instance.Player
                                       .GetComponent<PlayerController>();
        StartCoroutine(CutsceneRoutine());
    }

    // ─── Cutscene ─────────────────────────────────────────────────────────────
    private IEnumerator CutsceneRoutine()
    {
        // Step 1 — Blocca Dante e cursore
        if (_playerController != null)
            _playerController.IsAbilityCasting = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Step 2 — Disabilita FreeLook
        if (_freeLook != null) _freeLook.enabled = false;

        // Step 3 — Movimento camera
        Transform dante = GameManager.Instance.Player.transform;
        Vector3 startPos = dante.position + dante.TransformDirection(cameraStartOffset);
        Vector3 endPos = dante.position + dante.TransformDirection(cameraEndOffset);

        _mainCamera.transform.position = startPos;
        Quaternion startRot = Quaternion.LookRotation(dante.position - startPos);
        Quaternion endRot = Quaternion.LookRotation(dante.forward);
        _mainCamera.transform.rotation = startRot;

        float elapsed = 0f;
        while (elapsed < cameraDuration)
        {
            elapsed += Time.deltaTime;
            float t = cameraEaseOut.Evaluate(Mathf.Clamp01(elapsed / cameraDuration));
            _mainCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
            _mainCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        // Step 4 — Attiva canvas, poi posiziona e riabilita il testo
        if (creditsCanvas != null) creditsCanvas.SetActive(true);

        // Il testo parte disabilitato in Inspector — lo posizioniamo e poi lo abilitiamo
        if (creditsTextRect != null)
        {
            TMP_Text tmp = creditsTextRect.GetComponent<TMP_Text>();
            float textH = tmp != null ? tmp.preferredHeight : 2000f;
            creditsTextRect.anchoredPosition = new Vector2(
                creditsTextRect.anchoredPosition.x, -textH);
            creditsTextRect.gameObject.SetActive(true);
        }

        if (fadeCanvasGroup != null)
        {
            float fe = 0f;
            while (fe < fadeDuration)
            {
                fe += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Clamp01(fe / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        // Step 5 — Scroll testo
        if (creditsTextRect != null)
        {
            // Legge altezza preferita direttamente da TMP
            TMP_Text tmp = creditsTextRect.GetComponent<TMP_Text>();
            float textHeight = tmp != null ? tmp.preferredHeight : 2000f;

            Debug.Log($"[CutsceneManager] textHeight={textHeight} viewport={viewportHeight}");

            // Parte completamente sotto lo schermo (fuori dal viewport)
            // poi sale fino a quando l'ultimo rigo è uscito sopra
            float startY = -viewportHeight;   // il fondo del testo è sotto lo schermo
            float endY = textHeight;        // la cima del testo è uscita sopra

            creditsTextRect.anchoredPosition = new Vector2(
                creditsTextRect.anchoredPosition.x, startY);

            float se = 0f;
            while (se < scrollDuration)
            {
                se += Time.deltaTime;
                float posY = Mathf.Lerp(startY, endY, se / scrollDuration);
                creditsTextRect.anchoredPosition = new Vector2(
                    creditsTextRect.anchoredPosition.x, posY);
                yield return null;
            }
        }

        // Step 6 — Bottone + timer
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (btnMainMenu != null) btnMainMenu.gameObject.SetActive(true);

        yield return new WaitForSeconds(autoReturnDelay);

        ReturnToMainMenu();
    }

    // ─── Ritorno ──────────────────────────────────────────────────────────────
    public void ReturnToMainMenu()
    {
        if (_returned) return;
        _returned = true;

        AudioManager.Instance?.Stop(fadeOut: false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameManager.Instance?.ReturnToMainMenu();
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + cameraStartOffset, 0.3f);
        UnityEditor.Handles.Label(transform.position + cameraStartOffset + Vector3.up * 0.4f, "Cam Start");

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + cameraEndOffset, 0.3f);
        UnityEditor.Handles.Label(transform.position + cameraEndOffset + Vector3.up * 0.4f, "Cam End");

        Gizmos.DrawLine(transform.position + cameraStartOffset, transform.position + cameraEndOffset);
    }
#endif
}