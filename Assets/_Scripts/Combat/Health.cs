using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// DANTE: DIVINE'S FALL — Health.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Componente universale per tutte le entità danneggiabili:
/// Dante, Diavoli, Lucifero.
///
/// INSPECTOR:
///   maxHealth        → HP massimi (default 100)
///   destroyOnDeath   → se true, il GameObject viene distrutto alla morte
///
/// EVENTS (wirable in Inspector):
///   OnHealthChanged(float currentHP) → HUDController.UpdateHP(), BossHealthBar
///   OnDeath()                        → GameManager.OnPlayerDeath(), animazione morte
///
/// IFRAMES:
///   IsInvincible = true  → TakeDamage() ignorato completamente.
///   Settato da DashAbility durante lo scatto.
/// </summary>
public class Health : MonoBehaviour
{
    [Header("Config")]
    public float maxHealth = 100f;
    public bool destroyOnDeath = false;

    [Header("Events")]
    public UnityEvent<float> OnHealthChanged;   // passa currentHP
    public UnityEvent OnDeath;

    private float _current;

    // ─── Proprietà ────────────────────────────────────────────────────────────
    public float Current => _current;
    public float Percent => _current / maxHealth;

    /// <summary>
    /// Quando true, TakeDamage() viene ignorato completamente.
    /// Settato da DashAbility durante gli iframes dello scatto.
    /// </summary>
    public bool IsInvincible { get; set; } = false;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake() => _current = maxHealth;

    // ─── API pubblica ─────────────────────────────────────────────────────────
    public void TakeDamage(float amount)
    {
        if (_current <= 0f) return;
        if (IsInvincible) return;   // iframes dash — ignora il danno

        _current = Mathf.Max(0f, _current - amount);
        Debug.Log($"[Health] {gameObject.name} HP: {_current}/{maxHealth} (-{amount})");
        OnHealthChanged?.Invoke(_current);

        if (_current <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (_current <= 0f) return;
        _current = Mathf.Min(maxHealth, _current + amount);
        OnHealthChanged?.Invoke(_current);
    }

    private void Die()
    {
        OnDeath?.Invoke();
        if (destroyOnDeath) Destroy(gameObject);
    }
}