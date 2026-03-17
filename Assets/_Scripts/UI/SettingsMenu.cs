using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// DANTE: DIVINE'S FALL — SettingsMenu.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Pannello Settings condiviso tra MainMenu e PauseMenu.
/// Appare come overlay sopra il menu corrente.
/// Salva le impostazioni con PlayerPrefs — persistono tra sessioni.
///
/// IMPOSTAZIONI:
///   mouseSensitivity → moltiplicatore velocità camera FreeLook (0.1 - 5)
///   audioVolume      → volume AudioManager (0 - 1)
///
/// COME FUNZIONA:
///   - SettingsMenu è un figlio del prefab Dante O un GameObject separato in scena
///   - MainMenu e PauseMenu chiamano SettingsMenu.Instance.Open() / Close()
///   - Le modifiche agli slider sono applicate in tempo reale
///   - Salvate automaticamente alla chiusura del pannello
///
/// PLAYERPREFS KEYS:
///   "MouseSensitivity" → float (default 1.0)
///   "AudioVolume"      → float (default 0.8)
///
/// SETUP NEL PREFAB DANTE:
///   Dante
///   └── SettingsCanvas       ← Canvas SO Overlay, Sort Order 60, disabilitato
///       └── SettingsPanel
///           ├── TitleText            "IMPOSTAZIONI"
///           ├── SensitivityLabel     "Sensibilità Mouse"
///           ├── SensitivitySlider    ← Slider (0.1 - 5)
///           ├── SensitivityValue     ← TMP_Text valore numerico
///           ├── VolumeLabel          "Volume Audio"
///           ├── VolumeSlider         ← Slider (0 - 1)
///           ├── VolumeValue          ← TMP_Text valore numerico
///           └── BtnChiudi            ← bottone Chiudi
///
/// INSPECTOR:
///   settingsCanvas       → GameObject canvas settings
///   sensitivitySlider    → Slider mouse sensitivity
///   sensitivityValueText → TMP_Text che mostra il valore numerico
///   volumeSlider         → Slider volume audio
///   volumeValueText      → TMP_Text che mostra il valore numerico
///   btnChiudi            → bottone Chiudi
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static SettingsMenu Instance { get; private set; }

    // Impostazioni gestite da GameManager — persistono tra le scene

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Canvas")]
    public GameObject settingsCanvas;

    [Header("Sensitivity")]
    public Slider sensitivitySlider;
    public TMP_Text sensitivityValueText;

    [Header("Volume")]
    public Slider volumeSlider;
    public TMP_Text volumeValueText;

    [Header("Bottone")]
    public Button btnChiudi;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private CanvasGroup _canvasGroup;
    private Cinemachine.CinemachineFreeLook _freeLook;



    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        // Singleton — uno solo per scena (figlio di Dante, istanziato da PlayerSpawnPoint)
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Setup canvas — parte disabilitato
        if (settingsCanvas != null)
        {
            _canvasGroup = settingsCanvas.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = settingsCanvas.AddComponent<CanvasGroup>();
            settingsCanvas.SetActive(false);
        }

        // Impostazioni caricate da GameManager
    }

    void Start()
    {
        // Trova FreeLook nella scena
        _freeLook = FindObjectOfType<Cinemachine.CinemachineFreeLook>();

        // Collega slider
        sensitivitySlider?.onValueChanged.AddListener(OnSensitivityChanged);
        volumeSlider?.onValueChanged.AddListener(OnVolumeChanged);
        btnChiudi?.onClick.AddListener(Close);

        // Inizializza slider con valori da GameManager
        float sens = GameManager.Instance?.MouseSensitivity ?? 0.3f;
        float volume = GameManager.Instance?.AudioVolume ?? 0.8f;

        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = 0f;
            sensitivitySlider.maxValue = 10f;   // 10 step discreti
            sensitivitySlider.wholeNumbers = true;  // scatti interi
            sensitivitySlider.value = Mathf.RoundToInt(sens * 10f);
        }

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = volume;
        }

        UpdateValueTexts();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        sensitivitySlider?.onValueChanged.RemoveListener(OnSensitivityChanged);
        volumeSlider?.onValueChanged.RemoveListener(OnVolumeChanged);
        btnChiudi?.onClick.RemoveListener(Close);
    }

    // ─── API pubblica ─────────────────────────────────────────────────────────

    /// <summary>Apre il pannello settings. Chiamato da MainMenu e PauseMenu.</summary>
    public void Open()
    {
        if (settingsCanvas == null) return;
        settingsCanvas.SetActive(true);

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }
    }

    /// <summary>Chiude il pannello e salva le impostazioni.</summary>
    public void Close()
    {
        // Salvataggio già avvenuto in tempo reale tramite GameManager.SaveSettings()
        if (settingsCanvas == null) return;
        settingsCanvas.SetActive(false);

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }

    // ─── Slider callbacks ─────────────────────────────────────────────────────
    private void OnSensitivityChanged(float value)
    {
        float normalised = value / 10f;   // converti 0-10 → 0-1
        float vol = GameManager.Instance?.AudioVolume ?? volumeSlider?.value ?? 0.8f;
        GameManager.Instance?.SaveSettings(normalised, vol);
        UpdateValueTexts();
    }

    private void OnVolumeChanged(float value)
    {
        float sens = GameManager.Instance?.MouseSensitivity ?? sensitivitySlider?.value ?? 0.7f;
        GameManager.Instance?.SaveSettings(sens, value);
        UpdateValueTexts();
    }

    // Applica impostazioni delegate a GameManager

    // ─── Testo valori ─────────────────────────────────────────────────────────
    private void UpdateValueTexts()
    {
        float sens = GameManager.Instance?.MouseSensitivity ?? sensitivitySlider?.value ?? 0.7f;
        float vol = GameManager.Instance?.AudioVolume ?? volumeSlider?.value ?? 0.8f;

        if (sensitivityValueText != null)
            sensitivityValueText.text = sens.ToString("F1");   // mostra 0.0-1.0

        if (volumeValueText != null)
            volumeValueText.text = Mathf.RoundToInt(vol * 100f) + "%";
    }

    // PlayerPrefs e salvataggio gestiti da GameManager.SaveSettings()
}