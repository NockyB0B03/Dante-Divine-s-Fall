using UnityEngine;

// Base ScriptableObject — extend per ability
[CreateAssetMenu(menuName = "Abilities/AbilityData")]
public class AbilityData : ScriptableObject
{
    [Header("Identity")]
    public string abilityName;
    public Sprite icon;

    [Header("Timing")]
    public float cooldown = 2f;
    public float duration = 0f;   // 0 = instant

    [Header("Stats")]
    public float damage = 0f;
    public float speed = 0f;
}

// ── Dash ──────────────────────────────────────────
[CreateAssetMenu(menuName = "Abilities/DashData")]
public class DashData : AbilityData
{
    public float dashDistance = 6f;
    public float iFrameDuration = 0.3f;
}

// ── Fire ──────────────────────────────────────────
[CreateAssetMenu(menuName = "Abilities/FireData")]
public class FireData : AbilityData
{
    public GameObject projectilePrefab;
    public float launchAngle = 15f;      // degrees above horizon
    public float projectileSpeed = 18f;
}

// ── Ultimate ──────────────────────────────────────
[CreateAssetMenu(menuName = "Abilities/UltimateData")]
public class UltimateData : AbilityData
{
    public float aoeRadius = 8f;
    public float slowFactor = 0.3f;
    public float ultimateDuration = 5f;
}