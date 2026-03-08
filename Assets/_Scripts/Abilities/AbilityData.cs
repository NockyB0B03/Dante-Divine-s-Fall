using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — AbilityData.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// ScriptableObject base + tutte le sottoclassi per le abilità di Dante.
/// Crea gli asset tramite:
///   Assets → Create → Dante/Abilities → [tipo]
///
/// FILE UNICO — contiene 4 classi:
///   AbilityData     → base condivisa (cooldown, damage, icona)
///   FireData        → usata da FireAbility.cs
///   DashData        → usata da DashAbility.cs
///   UltimateData    → usata da UltimateAbility.cs
///
/// PERCORSO CONSIGLIATO ASSET:
///   Assets/_Project/ScriptableObjects/Abilities/
///     SO_FireAbility.asset
///     SO_DashAbility.asset
///     SO_Ultimate.asset
/// </summary>

// ── Base ──────────────────────────────────────────────────────────────────────
public abstract class AbilityData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Nome leggibile dell'abilità — usato nella UI.")]
    public string abilityName = "Ability";

    [Tooltip("Icona mostrata nell'HUD per questa abilità.")]
    public Sprite icon;

    [Header("Timing")]
    [Tooltip("Secondi di cooldown dopo l'utilizzo.")]
    public float cooldown = 2f;

    [Header("Stats")]
    [Tooltip("Danno base applicato al bersaglio.")]
    public float damage = 10f;
}

// ── FireData ──────────────────────────────────────────────────────────────────
/// <summary>
/// Dati per FireAbility.cs — proiettile orb lanciato verso il crosshair.
/// Crea asset: Assets → Create → Dante/Abilities/Fire Data
/// </summary>
[CreateAssetMenu(menuName = "Dante/Abilities/Fire Data", fileName = "SO_FireAbility")]
public class FireData : AbilityData
{
    [Header("Projectile")]
    [Tooltip("Prefab del proiettile recuperato da FireOrbPool.")]
    public GameObject projectilePrefab;

    [Tooltip("Velocità iniziale del proiettile in unità/secondo.")]
    public float projectileSpeed = 18f;
}

// ── DashData ──────────────────────────────────────────────────────────────────
/// <summary>
/// Dati per DashAbility.cs — scatto con iframes.
/// Crea asset: Assets → Create → Dante/Abilities/Dash Data
/// </summary>
[CreateAssetMenu(menuName = "Dante/Abilities/Dash Data", fileName = "SO_DashAbility")]
public class DashData : AbilityData
{
    [Header("Dash")]
    [Tooltip("Distanza percorsa durante il dash in unità Unity.")]
    public float dashDistance = 6f;

    [Tooltip("Durata degli iframes (invincibilità) durante il dash in secondi.")]
    public float iFrameDuration = 0.3f;

    [Tooltip("Durata totale del dash in secondi — controlla anche la velocità effettiva.")]
    public float dashDuration = 0.2f;
}

// ── UltimateData ──────────────────────────────────────────────────────────────
/// <summary>
/// Dati per UltimateAbility.cs — LegioniCelesti.
/// Crea asset: Assets → Create → Dante/Abilities/Ultimate Data
/// </summary>
[CreateAssetMenu(menuName = "Dante/Abilities/Ultimate Data", fileName = "SO_Ultimate")]
public class UltimateData : AbilityData
{
    [Header("Legioni Celesti")]
    [Tooltip("Prefab della singola entità celeste lanciata verso il nemico.")]
    public GameObject legionePrefab;

    [Tooltip("Numero di entità spawnate per attivazione.")]
    public int spawnCount = 8;

    [Tooltip("Raggio dell'area attorno a Dante in cui spawnano le entità.")]
    public float spawnRadius = 5f;

    [Tooltip("Velocità di movimento delle entità verso il bersaglio.")]
    public float legionSpeed = 12f;

    [Tooltip("Durata in secondi prima che le entità si auto-distruggano se non colpiscono.")]
    public float legionLifetime = 6f;
}