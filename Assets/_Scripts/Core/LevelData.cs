using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — LevelData.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// ScriptableObject con tutti i dati di un livello.
/// Crea un asset per ogni livello:
///   Assets → Create → Dante/Level Data
///
/// INSPECTOR:
///   canvasContent   → CanvasContentData SO con i testi della loading screen
///   loadingMusic    → clip audio in loop durante la loading screen
///   gameplayMusic   → clip audio in loop durante il gameplay
///   deathMusic      → clip audio in loop durante la schermata di morte
/// </summary>
[CreateAssetMenu(menuName = "Dante/Level Data", fileName = "LevelData_Level_")]
public class LevelData : ScriptableObject
{
    [Header("Canvas Content")]
    [Tooltip("ScriptableObject con i testi della loading screen.")]
    public CanvasContentData canvasContent;

    [Header("Audio")]
    [Tooltip("Clip in loop durante la loading screen.")]
    public AudioClip loadingMusic;

    [Tooltip("Clip in loop durante il gameplay — parte quando il player preme INVIO.")]
    public AudioClip gameplayMusic;

    [Tooltip("Clip in loop durante la schermata di morte.")]
    public AudioClip deathMusic;
}