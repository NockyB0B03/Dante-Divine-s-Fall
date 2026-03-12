using System.Collections;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — AudioManager.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Singleton DontDestroyOnLoad — persiste tra tutte le scene.
/// Gestisce la musica di gioco:
///   - Un clip per livello, assegnato tramite LevelData SO
///   - Loop automatico — riparte immediatamente quando finisce
///   - Fade in all'inizio di ogni livello
///   - Cambio immediato tra livelli (nessun fade out)
///
/// SETUP IN SCENA:
///   Crea un GameObject vuoto "AudioManager" nella scena MainMenu (index 0).
///   Aggiungi AudioManager.cs — persiste automaticamente in tutte le scene.
///   NON mettere AudioManager nelle altre scene — il singleton lo impedisce.
///
/// COME ASSEGNARE LA MUSICA PER LIVELLO:
///   Ogni LevelData SO ha un campo musicClip — trascina l'mp3.
///   LevelManager chiama AudioManager.Instance.PlayMusic(clip) ad ogni caricamento.
///   (La chiamata è già scritta in LevelManager.cs — basta decommentarla.)
///
/// INSPECTOR:
///   volume       → volume della musica (default 0.8)
///   fadeInTime   → durata fade in in secondi (default 2)
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static AudioManager Instance { get; private set; }

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Settings")]
    [Range(0f, 1f)]
    [Tooltip("Volume della musica.")]
    public float volume = 0.8f;

    [Tooltip("Durata del fade in in secondi all'inizio di ogni livello.")]
    public float fadeInTime = 2f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private AudioSource _audioSource;
    private Coroutine _fadeRoutine;
    private AudioClip _currentClip;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        // Singleton DontDestroyOnLoad
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Setup AudioSource
        _audioSource = GetComponent<AudioSource>();
        _audioSource.loop = true;    // loop automatico

        // Opzione A — Play On Awake attivo in Inspector:
        // non azzeriamo il volume né forziamo playOnAwake=false.
        // Il clip e il volume sono già impostati in Inspector.
        // Opzione B — PlayMusic() gestisce tutto via codice.
        if (!_audioSource.playOnAwake)
        {
            _audioSource.volume = 0f;
            _audioSource.playOnAwake = false;
        }
        else
        {
            // Fade in sulla musica del menu
            _currentClip = _audioSource.clip;
        }
    }

    void Start()
    {
        // Se Play On Awake è attivo avvia il fade in sulla musica del menu
        if (_audioSource.playOnAwake && _audioSource.clip != null)
        {
            _audioSource.volume = 0f;
            if (!_audioSource.isPlaying) _audioSource.Play();
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeIn());
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── API pubblica ─────────────────────────────────────────────────────────

    /// <summary>
    /// Avvia la riproduzione di un clip con fade in.
    /// Se il clip è lo stesso già in riproduzione non fa nulla.
    /// Chiamato da LevelManager ad ogni caricamento scena.
    /// </summary>
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null)
        {
            StopMusic();
            return;
        }

        // Stesso clip già in riproduzione — non interrompere
        if (_currentClip == clip && _audioSource.isPlaying) return;

        _currentClip = clip;

        // Cambio immediato — stop e riparti con il nuovo clip
        _audioSource.Stop();
        _audioSource.clip = clip;
        _audioSource.volume = 0f;
        _audioSource.Play();

        // Fade in
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeIn());
    }

    /// <summary>Ferma la musica immediatamente.</summary>
    public void StopMusic()
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _audioSource.Stop();
        _audioSource.volume = 0f;
        _currentClip = null;
    }

    /// <summary>Modifica il volume a runtime — es. da un menu opzioni.</summary>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        _audioSource.volume = volume;
    }

    // ─── Fade In ──────────────────────────────────────────────────────────────
    private IEnumerator FadeIn()
    {
        float elapsed = 0f;

        while (elapsed < fadeInTime)
        {
            elapsed += Time.unscaledDeltaTime;   // funziona anche in pausa
            _audioSource.volume = Mathf.Lerp(0f, volume, elapsed / fadeInTime);
            yield return null;
        }

        _audioSource.volume = volume;
        _fadeRoutine = null;
    }
}