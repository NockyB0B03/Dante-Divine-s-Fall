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

    // ─── PlayerPrefs Keys ─────────────────────────────────────────────────────
    private const string KEY_SENSITIVITY = "MouseSensitivity";
    private const string KEY_VOLUME      = "AudioVolume";

    // ─── Valori default ───────────────────────────────────────────────────────
    private const float DEFAULT_SENSITIVITY = 1f;
    private const float DEFAULT_VOLUME      = 0.8f;

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Canvas")]
    public GameObject settingsCanvas;

    [Header("Sensitivity")]
    public Slider    sensitivitySlider;
    public TMP_Text  sensitivityValueText;

    [Header("Volume")]
    public Slider    volumeSlider;
    public TMP_Text  volumeValueText;

    [Header("Bottone")]
    public Button btnChiudi;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private CanvasGroup _canvasGroup;
    private Cinemachine.CinemachineFreeLook _freeLook;

    // ─── Proprietà pubblica ───────────────────────────────────────────────────
    public float MouseSensitivity { get; private set; }
    public float AudioVolume      { get; private set; }

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

        // Carica impostazioni salvate
        LoadSettings();
    }

    void Start()
    {
        // Trova FreeLook nella scena
        _freeLook = FindObjectOfType<Cinemachine.CinemachineFreeLook>();

        // Applica impostazioni caricate
        ApplySettings();

        // Collega slider
        sensitivitySlider?.onValueChanged.AddListener(OnSensitivityChanged);
        volumeSlider?.onValueChanged.AddListener(OnVolumeChanged);
        btnChiudi?.onClick.AddListener(Close);

        // Inizializza valori slider
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = 0.1f;
            sensitivitySlider.maxValue = 5f;
            sensitivitySlider.value    = MouseSensitivity;
        }

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value    = AudioVolume;
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
            _canvasGroup.alpha          = 1f;
            _canvasGroup.interactable   = true;
            _canvasGroup.blocksRaycasts = true;
        }
    }

    /// <summary>Chiude il pannello e salva le impostazioni.</summary>
    public void Close()
    {
        SaveSettings();

        if (settingsCanvas == null) return;
        settingsCanvas.SetActive(false);

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha          = 0f;
            _canvasGroup.interactable   = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }

    // ─── Slider callbacks ─────────────────────────────────────────────────────
    private void OnSensitivityChanged(float value)
    {
        MouseSensitivity = value;
        ApplySensitivity();
        UpdateValueTexts();
    }

    private void OnVolumeChanged(float value)
    {
        AudioVolume = value;
        ApplyVolume();
        UpdateValueTexts();
    }

    // ─── Applica impostazioni ─────────────────────────────────────────────────
    private void ApplySettings()
    {
        ApplySensitivity();
        ApplyVolume();
    }

    private void ApplySensitivity()
    {
        if (_freeLook == null)
            _freeLook = FindObjectOfType<Cinemachine.CinemachineFreeLook>();

        if (_freeLook != null)
        {
            // Scala la velocità degli assi X e Y per la sensibilità
            _freeLook.m_XAxis.m_MaxSpeed = 300f * MouseSensitivity;
            _freeLook.m_YAxis.m_MaxSpeed = 2f   * MouseSensitivity;
        }
    }

    private void ApplyVolume()
    {
        AudioManager.Instance?.SetVolume(AudioVolume);
    }

    // ─── Testo valori ─────────────────────────────────────────────────────────
    private void UpdateValueTexts()
    {
        if (sensitivityValueText != null)
            sensitivityValueText.text = MouseSensitivity.ToString("F1");

        if (volumeValueText != null)
            volumeValueText.text = Mathf.RoundToInt(AudioVolume * 100f) + "%";
    }

    // ─── PlayerPrefs ──────────────────────────────────────────────────────────
    private void LoadSettings()
    {
        MouseSensitivity = PlayerPrefs.GetFloat(KEY_SENSITIVITY, DEFAULT_SENSITIVITY);
        AudioVolume      = PlayerPrefs.GetFloat(KEY_VOLUME,      DEFAULT_VOLUME);
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(KEY_SENSITIVITY, MouseSensitivity);
        PlayerPrefs.SetFloat(KEY_VOLUME,      AudioVolume);
        PlayerPrefs.Save();
    }
}
