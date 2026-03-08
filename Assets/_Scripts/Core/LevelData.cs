using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — LevelData.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// ScriptableObject that holds all designer-editable metadata for a single level.
/// Create one asset per level via:
///   Assets → Create → Dante / Level Data
/// Then drag each asset into LevelManager's levelDataList array in the Inspector,
/// ordered by Build Settings scene index (slot 0 = scene index 1 = Level 1, etc.)
///
/// INSPECTOR SETUP PER ASSET:
///   levelName        → "Selva Oscura"
///   levelSubtitle    → "Trova la porta dell'Inferno"
///   rulesText        → full rules paragraph shown on loading screen
///   backgroundSprite → optional artwork shown on loading screen canvas
///   musicClip        → optional — if AudioManager supports per-level BGM
/// </summary>

[CreateAssetMenu(menuName = "Dante/Level Data", fileName = "LevelData_Level_")]
public class LevelData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Short display name shown as heading on the loading screen.")]
    public string levelName = "Level Name";

    [Tooltip("One-line subtitle / flavour text shown below the title.")]
    public string levelSubtitle = "";

    [Header("Loading Screen Content")]
    [Tooltip("Full rules description displayed on the loading screen canvas.")]
    [TextArea(4, 10)]
    public string rulesText = "Describe the level rules here.";

    [Tooltip("Optional background image displayed on the loading screen. Leave null for solid colour fallback.")]
    public Sprite backgroundSprite;

    [Header("Audio")]
    [Tooltip("BGM clip to play for this level. Leave null to keep previous track.")]
    public AudioClip musicClip;
}