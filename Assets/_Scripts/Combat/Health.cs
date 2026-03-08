using System;
using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [Header("Config")]
    public float maxHealth = 100f;
    public bool destroyOnDeath = false;

    [Header("Events")]
    public UnityEvent<float> OnHealthChanged;  // (currentHP)
    public UnityEvent OnDeath;

    private float _current;

    public float Current => _current;
    public float Percent => _current / maxHealth;

    void Awake() => _current = maxHealth;

    public void TakeDamage(float amount)
    {
        if (_current <= 0f) return;
        _current = Mathf.Max(0f, _current - amount);
        OnHealthChanged?.Invoke(_current);
        if (_current <= 0f) Die();
    }

    public void Heal(float amount)
    {
        _current = Mathf.Min(maxHealth, _current + amount);
        OnHealthChanged?.Invoke(_current);
    }

    void Die()
    {
        OnDeath?.Invoke();
        if (destroyOnDeath) Destroy(gameObject);
    }
}