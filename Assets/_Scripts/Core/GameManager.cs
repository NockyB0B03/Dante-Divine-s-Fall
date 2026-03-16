using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// DANTE: DIVINE'S FALL — GameManager.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Singleton DontDestroyOnLoad.
/// Responsabilità: stato di gioco, timer, riferimento al player, combat unlock,
///                 gestione usi Ultimate.
///
/// NON gestisce: audio (AudioManager), transizioni (LevelManager),
///               morte (DeathManager), pausa (PauseMenu).
///
/// SCENE BUILD INDEX CONTRACT:
///   0 → MainMenu
///   1 → Level 1 (Selva Oscura)
///   2 → Level 2 (Piattaforme)
///   3 → Level 3 (Labirinto Diavoli)
///   4 → Level 4 (Sabbia / Cappe di Piombo)
///   5 → Level 5 (Boss — Lucifero)  ← COMBAT_UNLOCK_INDEX
///   6 → Level 6 (Fine / Le Stelle)
/// </summary>
public class GameManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ─── Costanti ─────────────────────────────────────────────────────────────
    private const int MAIN_MENU_INDEX = 0;
    private const int FIRST_LEVEL_INDEX = 1;
    private const int COMBAT_UNLOCK_INDEX = 5;
    private const int VICTORY_SCENE_INDEX = 6;
    private const int ULTIMATE_USES_PER_PHASE = 2;

    // ─── Stato ────────────────────────────────────────────────────────────────
    public enum GameState { MainMenu, Playing, Paused, GameOver, Victory }
    private GameState _currentState = GameState.MainMenu;
    public GameState CurrentState => _currentState;

    public static event Action<GameState> OnGameStateChanged;
    public static event Action OnCombatUnlocked;
    public static event Action<int> OnUltimateUsesChanged;
    public static event Action<float, float> OnTimerUpdated;

    // ─── Combat Gate ──────────────────────────────────────────────────────────
    public bool CombatUnlocked { get; private set; } = false;

    // ─── Ultimate ─────────────────────────────────────────────────────────────
    private int _ultimateUsesRemaining = ULTIMATE_USES_PER_PHASE;
    public int UltimateUsesRemaining => _ultimateUsesRemaining;

    // ─── Player Reference ─────────────────────────────────────────────────────
    public GameObject Player { get; private set; }
    public Health PlayerHealth { get; private set; }

    // ─── Timer ────────────────────────────────────────────────────────────────
    private float _totalPlayTime = 0f;
    private float _levelStartTime = 0f;
    public float TotalPlayTime => _totalPlayTime;
    public float CurrentLevelTime => _currentState == GameState.Playing
                                       ? Time.time - _levelStartTime : 0f;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (_currentState != GameState.Playing) return;
        _totalPlayTime += Time.deltaTime;
        OnTimerUpdated?.Invoke(CurrentLevelTime, _totalPlayTime);
    }

    // ─── Scene Load ───────────────────────────────────────────────────────────
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        int index = scene.buildIndex;

        if (index == MAIN_MENU_INDEX) { SetState(GameState.MainMenu); return; }
        if (index == VICTORY_SCENE_INDEX) { SetState(GameState.Victory); return; }

        // Scena di gioco
        Player = null;   // reset — PlayerSpawnPoint.Awake() chiamerà RegisterPlayer()
        _levelStartTime = Time.time;
        EvaluateCombatUnlock(index);
        SetState(GameState.Playing);
    }

    // ─── Stato ────────────────────────────────────────────────────────────────
    private void SetState(GameState newState)
    {
        if (_currentState == newState) return;
        _currentState = newState;
        OnGameStateChanged?.Invoke(_currentState);
    }

    /// <summary>
    /// Toggle pausa — chiamato da PauseMenu.
    /// GameManager gestisce solo lo stato; timeScale e canvas sono di PauseMenu.
    /// </summary>
    public void SetPaused(bool paused)
    {
        if (paused && _currentState == GameState.Playing) SetState(GameState.Paused);
        if (!paused && _currentState == GameState.Paused) SetState(GameState.Playing);
    }

    // ─── Player ───────────────────────────────────────────────────────────────
    /// <summary>Chiamato da PlayerController.Awake() ad ogni istanziazione.</summary>
    public void RegisterPlayer(GameObject playerObject)
    {
        Player = playerObject;
        PlayerHealth = playerObject.GetComponent<Health>();

        if (PlayerHealth == null)
            Debug.LogError("[GameManager] Player manca del componente Health!");
    }

    // ─── HP ───────────────────────────────────────────────────────────────────
    /// <summary>
    /// Ripristina gli HP di Dante — chiamato da LevelManager dopo il reload.
    /// </summary>
    public void RestorePlayerHP()
    {
        PlayerHealth?.Heal(float.MaxValue);
    }

    // ─── Combat Unlock ────────────────────────────────────────────────────────
    private void EvaluateCombatUnlock(int sceneIndex)
    {
        bool should = sceneIndex == COMBAT_UNLOCK_INDEX;

        if (should && !CombatUnlocked)
        {
            CombatUnlocked = true;
            OnCombatUnlocked?.Invoke();
            Debug.Log("[GameManager] Combat UNLOCKED.");
        }
        else if (!should)
        {
            CombatUnlocked = false;
        }
    }

    // ─── Ultimate ─────────────────────────────────────────────────────────────
    public void ResetUltimateUses()
    {
        _ultimateUsesRemaining = ULTIMATE_USES_PER_PHASE;
        OnUltimateUsesChanged?.Invoke(_ultimateUsesRemaining);
    }

    public bool TryConsumeUltimateUse()
    {
        if (!CombatUnlocked || _ultimateUsesRemaining <= 0) return false;
        _ultimateUsesRemaining--;
        OnUltimateUsesChanged?.Invoke(_ultimateUsesRemaining);
        return true;
    }

    // ─── Public API ───────────────────────────────────────────────────────────
    public void StartGame() { ResetAllState(); SceneManager.LoadScene(FIRST_LEVEL_INDEX); }
    public void ReturnToMainMenu() { ResetAllState(); Time.timeScale = 1f; SceneManager.LoadScene(MAIN_MENU_INDEX); }
    public void RestartCurrentLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ─── Reset ────────────────────────────────────────────────────────────────
    private void ResetAllState()
    {
        CombatUnlocked = false;
        _ultimateUsesRemaining = ULTIMATE_USES_PER_PHASE;
        _totalPlayTime = 0f;
        _levelStartTime = 0f;
        Player = null;
        PlayerHealth = null;
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("DEBUG — Unlock Combat")]
    private void Debug_UnlockCombat() { CombatUnlocked = true; OnCombatUnlocked?.Invoke(); }

    [ContextMenu("DEBUG — Simulate Player Death")]
    private void Debug_SimulateDeath() => SetState(GameState.GameOver);
#endif
}