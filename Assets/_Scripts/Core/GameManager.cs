using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// DANTE: DIVINE'S FALL — GameManager.cs
/// ─────────────────────────────────────
/// Persistent singleton (DontDestroyOnLoad).
/// Owns: GameState machine, combat unlock gate, LegioniCelesti usage tracking,
///       player HP reset between levels, time tracking (per-level + total),
///       and scene-change coordination with LevelManager.
///
/// SCENE BUILD INDEX CONTRACT (must match File → Build Settings order):
///   0  → MainMenu
///   1  → Level 1  (Selva Oscura)
///   2  → Level 2  (Piattaforme)
///   3  → Level 3  (Labirinto Diavoli)
///   4  → Level 4  (Sabbia / Cappe di Piombo)
///   5  → Level 5  (Boss — Lucifero)   ← COMBAT_UNLOCK_INDEX
///   6  → Level 6  (Fine / Le Stelle)
/// </summary>

public class GameManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ─── Constants ────────────────────────────────────────────────────────────
    private const int MAIN_MENU_INDEX = 0;
    private const int FIRST_LEVEL_INDEX = 1;
    private const int COMBAT_UNLOCK_INDEX = 5;   // Level 5 — boss arena
    private const int VICTORY_SCENE_INDEX = 6;
    private const int ULTIMATE_USES_PER_PHASE = 2;

    // ─── Game State ───────────────────────────────────────────────────────────
    public enum GameState { MainMenu, Playing, Paused, GameOver, Victory }

    private GameState _currentState = GameState.MainMenu;
    public GameState CurrentState => _currentState;

    // Events — other systems subscribe; GameManager never calls their methods directly
    public static event Action<GameState> OnGameStateChanged;
    public static event Action OnCombatUnlocked;
    public static event Action<int> OnUltimateUsesChanged;   // passes remaining uses
    public static event Action<float, float> OnTimerUpdated;      // (levelTime, totalTime)

    // ─── Combat Gate ──────────────────────────────────────────────────────────
    public bool CombatUnlocked { get; private set; } = false;

    // ─── LegioniCelesti (Ultimate) ────────────────────────────────────────────
    private int _ultimateUsesRemaining = ULTIMATE_USES_PER_PHASE;
    public int UltimateUsesRemaining => _ultimateUsesRemaining;

    // ─── Player Reference ─────────────────────────────────────────────────────
    /// <summary>
    /// Set by PlayerController.Awake(). All systems use this instead of FindObjectOfType.
    /// </summary>
    public GameObject Player { get; private set; }
    public Health PlayerHealth { get; private set; }

    // ─── Time Tracking ────────────────────────────────────────────────────────
    private float _totalPlayTime = 0f;    // accumulates across levels, resets at victory
    private float _levelStartTime = 0f;    // Time.time snapshot when level loaded
    public float TotalPlayTime => _totalPlayTime;
    public float CurrentLevelTime => _currentState == GameState.Playing
                                     ? Time.time - _levelStartTime
                                     : 0f;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        // Singleton enforcement
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        if (_currentState == GameState.Playing)
        {
            _totalPlayTime += Time.deltaTime;
            // Broadcast timer update every frame (HUD subscribes to update cooldown UI)
            OnTimerUpdated?.Invoke(CurrentLevelTime, _totalPlayTime);
        }
    }

    // ─── Scene Load Callback ──────────────────────────────────────────────────
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        int index = scene.buildIndex;

        // ── Main Menu
        if (index == MAIN_MENU_INDEX)
        {
            SetState(GameState.MainMenu);
            return;
        }

        // ── Victory scene
        if (index == VICTORY_SCENE_INDEX)
        {
            SetState(GameState.Victory);
            return;
        }

        // ── Any gameplay level
        RegisterPlayerInScene();
        RestorePlayerHP();
        StartLevelTimer();
        EvaluateCombatUnlock(index);
        SetState(GameState.Playing);
    }

    // ─── State Machine ────────────────────────────────────────────────────────
    private void SetState(GameState newState)
    {
        if (_currentState == newState) return;
        _currentState = newState;
        OnGameStateChanged?.Invoke(_currentState);
    }

    // ─── Player Registration ──────────────────────────────────────────────────
    /// <summary>
    /// Called by PlayerController.Awake() to register itself.
    /// </summary>
    public void RegisterPlayer(GameObject playerObject)
    {
        Player = playerObject;
        PlayerHealth = playerObject.GetComponent<Health>();

        if (PlayerHealth == null)
            Debug.LogError("[GameManager] Player is missing a Health component!");

        // Subscribe to player death
        PlayerHealth.OnDeath.AddListener(OnPlayerDeath);
    }

    private void RegisterPlayerInScene()
    {
        // Fallback if RegisterPlayer hasn't been called yet (race condition safety)
        if (Player == null)
        {
            var found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) RegisterPlayer(found);
            else Debug.LogWarning("[GameManager] No Player tagged object found in scene.");
        }
    }

    // ─── HP Management ────────────────────────────────────────────────────────
    private void RestorePlayerHP()
    {
        // HP is fully restored when a new level loads
        PlayerHealth?.Heal(float.MaxValue);    // Health.cs clamps to maxHealth internally
    }

    // ─── Timer ────────────────────────────────────────────────────────────────
    private void StartLevelTimer()
    {
        _levelStartTime = Time.time;
    }

    private float StopLevelTimer()
    {
        // Returns the elapsed level time at the moment of stopping
        return Time.time - _levelStartTime;
    }

    // ─── Combat Unlock Gate ───────────────────────────────────────────────────
    private void EvaluateCombatUnlock(int sceneIndex)
    {
        bool shouldBeUnlocked = (sceneIndex == COMBAT_UNLOCK_INDEX);

        if (shouldBeUnlocked && !CombatUnlocked)
        {
            CombatUnlocked = true;
            OnCombatUnlocked?.Invoke();
            Debug.Log("[GameManager] Combat abilities UNLOCKED (Boss Level).");
        }
        else if (!shouldBeUnlocked && CombatUnlocked)
        {
            // Safety: if somehow the player ends up outside Level 5 after it was unlocked,
            // lock again. In normal flow this shouldn't occur.
            CombatUnlocked = false;
        }
    }

    // ─── LegioniCelesti (Ultimate) ────────────────────────────────────────────
    /// <summary>
    /// Called by LuciferBoss.cs on each phase transition.
    /// Resets the Ultimate usage counter to 2.
    /// </summary>
    public void ResetUltimateUses()
    {
        _ultimateUsesRemaining = ULTIMATE_USES_PER_PHASE;
        OnUltimateUsesChanged?.Invoke(_ultimateUsesRemaining);
        Debug.Log("[GameManager] LegioniCelesti uses reset to " + ULTIMATE_USES_PER_PHASE);
    }

    /// <summary>
    /// Called by UltimateAbility.cs before executing.
    /// Returns false if no uses remain — ability should abort.
    /// </summary>
    public bool TryConsumeUltimateUse()
    {
        if (!CombatUnlocked)
        {
            Debug.LogWarning("[GameManager] Ultimate attempted outside boss level — blocked.");
            return false;
        }

        if (_ultimateUsesRemaining <= 0)
        {
            Debug.Log("[GameManager] No Ultimate uses remaining this phase.");
            return false;
        }

        _ultimateUsesRemaining--;
        OnUltimateUsesChanged?.Invoke(_ultimateUsesRemaining);
        return true;
    }

    // ─── Death Handling ───────────────────────────────────────────────────────
    private void OnPlayerDeath()
    {
        if (_currentState == GameState.GameOver) return;   // prevent double-trigger

        float levelTime = StopLevelTimer();
        // Subtract this level's time from total — player didn't complete it
        _totalPlayTime -= levelTime;
        _totalPlayTime = Mathf.Max(0f, _totalPlayTime);

        SetState(GameState.GameOver);
        // PauseMenu.cs / GameOverScreen.cs listens to OnGameStateChanged and
        // shows the Game Over canvas + "Restart Level" button.
        // Time.timeScale is NOT touched here — that's the UI's responsibility.
    }

    // ─── Public API — called by UI buttons ────────────────────────────────────

    /// <summary>
    /// "Restart Level" button on the Game Over screen.
    /// Reloads the current scene. HP is restored by OnSceneLoaded → RestorePlayerHP().
    /// </summary>
    public void RestartCurrentLevel()
    {
        CombatUnlocked = false;   // will re-evaluate in OnSceneLoaded
        _ultimateUsesRemaining = ULTIMATE_USES_PER_PHASE;
        Time.timeScale = 1f;      // safety — reset if GameOver froze time
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// "Play Again" button on the Victory screen (Level 6).
    /// Resets ALL persistent state and returns to Main Menu.
    /// </summary>
    public void RestartFromBeginning()
    {
        ResetAllState();
        SceneManager.LoadScene(MAIN_MENU_INDEX);
    }

    /// <summary>
    /// Called by the Main Menu "Play" button to start Level 1.
    /// </summary>
    public void StartGame()
    {
        ResetAllState();
        SceneManager.LoadScene(FIRST_LEVEL_INDEX);
    }

    /// <summary>
    /// Called by PauseMenu.cs when the player clicks "Main Menu" from pause.
    /// </summary>
    public void ReturnToMainMenu()
    {
        ResetAllState();
        Time.timeScale = 1f;
        SceneManager.LoadScene(MAIN_MENU_INDEX);
    }

    /// <summary>
    /// Pause state toggle — GameManager owns the STATE, PauseMenu.cs owns timeScale + canvas.
    /// </summary>
    public void SetPaused(bool paused)
    {
        if (paused && _currentState == GameState.Playing)
            SetState(GameState.Paused);
        else if (!paused && _currentState == GameState.Paused)
            SetState(GameState.Playing);
    }

    // ─── Internal Reset ───────────────────────────────────────────────────────
    private void ResetAllState()
    {
        CombatUnlocked = false;
        _ultimateUsesRemaining = ULTIMATE_USES_PER_PHASE;
        _totalPlayTime = 0f;
        _levelStartTime = 0f;
        Player = null;
        PlayerHealth = null;
    }

    // ─── Debug / Utility ─────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("DEBUG — Unlock Combat")]
    private void Debug_UnlockCombat()
    {
        CombatUnlocked = true;
        OnCombatUnlocked?.Invoke();
        Debug.Log("[GameManager] DEBUG: Combat force-unlocked.");
    }

    [ContextMenu("DEBUG — Simulate Player Death")]
    private void Debug_SimulateDeath() => OnPlayerDeath();

    [ContextMenu("DEBUG — Print Timers")]
    private void Debug_PrintTimers()
    {
        Debug.Log($"[GameManager] Level time: {CurrentLevelTime:F2}s | Total: {_totalPlayTime:F2}s");
    }
#endif
}