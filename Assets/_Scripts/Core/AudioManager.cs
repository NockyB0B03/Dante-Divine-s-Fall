using System.Collections;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — AudioManager.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Singleton DontDestroyOnLoad.
/// Gestisce un singolo canale audio in loop (musica/ambience).
/// Metodi pubblici chiamabili da LevelManager, DeathManager, o qualsiasi script.
///
/// API:
///   PlayLooping(clip, fadeIn)  → avvia clip in loop con fade in opzionale
///   Stop(fadeOut)              → ferma la clip con fade out opzionale
///   SetVolume(float)           → cambia volume a runtime
///
/// SETUP: un GameObject "AudioManager" nella scena MainMenu con AudioManager.cs.
/// Per musica del menu: assegna clip + Play On Awake direttamente sull'AudioSource.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Settings")]
    [Range(0f, 1f)]
    public float volume = 0.8f;

    [Tooltip("Durata del fade in/out in secondi.")]
    public float fadeDuration = 1.5f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private AudioSource _source;
    private Coroutine _fadeRoutine;
    private AudioClip _currentClip;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _source = GetComponent<AudioSource>();
        _source.loop = true;
        _source.playOnAwake = false;
    }

    void Start()
    {
        // Musica menu — se Play On Awake è attivo in Inspector
        if (_source.clip != null && _source.playOnAwake)
        {
            _currentClip = _source.clip;
            _source.volume = 0f;
            _source.Play();
            StartFade(0f, volume, fadeDuration);
        }
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ─── API pubblica ─────────────────────────────────────────────────────────

    /// <summary>
    /// Avvia una clip in loop.
    /// Se è la stessa clip già in riproduzione non fa nulla.
    /// fadeIn: se true fa fade in, altrimenti parte al volume pieno.
    /// </summary>
    public void PlayLooping(AudioClip clip, bool fadeIn = true)
    {
        if (clip == null) { Stop(); return; }
        if (_currentClip == clip && _source.isPlaying) return;

        _currentClip = clip;
        _source.Stop();
        _source.clip = clip;
        _source.volume = fadeIn ? 0f : volume;
        _source.Play();

        if (fadeIn) StartFade(0f, volume, fadeDuration);
    }

    /// <summary>
    /// Ferma la clip corrente.
    /// fadeOut: se true fa fade out prima di fermarsi.
    /// </summary>
    public void Stop(bool fadeOut = false)
    {
        if (!_source.isPlaying) return;

        if (fadeOut)
            StartFade(volume, 0f, fadeDuration, stopAfter: true);
        else
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _source.Stop();
            _source.volume = 0f;
            _currentClip = null;
        }
    }

    /// <summary>Cambia il volume a runtime.</summary>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        _source.volume = volume;
    }

    // ─── Fade ─────────────────────────────────────────────────────────────────
    private void StartFade(float from, float to, float duration, bool stopAfter = false)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(from, to, duration, stopAfter));
    }

    private IEnumerator FadeRoutine(float from, float to, float duration, bool stopAfter)
    {
        float elapsed = 0f;
        _source.volume = from;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _source.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        _source.volume = to;

        if (stopAfter)
        {
            _source.Stop();
            _source.volume = 0f;
            _currentClip = null;
        }

        _fadeRoutine = null;
    }
}