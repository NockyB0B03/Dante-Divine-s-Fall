using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — LuciferFireball.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Estende Projectile.cs per il fireball di Lucifero.
/// Differenze rispetto al FireOrb di Dante:
///   - Lanciato verso l'ULTIMA POSIZIONE NOTA del player al momento dello spawn
///     (non segue il player durante il volo — traiettoria parabolica fissa).
///   - Fa danno ad AREA al contatto con qualsiasi collider (terreno incluso).
///   - Usa LuciferFireballPool invece di FireOrbPool.
///   - LaunchToward() calcola la velocità iniziale autonomamente — LuciferBoss.cs
///     chiama solo LaunchToward(playerPosition) dopo Get() dal pool.
///
/// RIGIDBODY SETUP (sul prefab LuciferFireball):
///   Mass                → 1
///   Use Gravity         → ✓
///   Is Kinematic        → ✗
///   Collision Detection → Continuous
///   Constraints         → Freeze Rotation X Y Z
///
/// COLLIDER SETUP:
///   SphereCollider, isTrigger = ✓, radius = 0.4  (più grande del FireOrb)
///
/// INSPECTOR:
///   damage          → ereditato da Projectile — danno al player sul contatto
///   splashRadius    → raggio del danno ad area all'impatto (default 3)
///   projectileSpeed → velocità di lancio in unità/secondo (default 14)
///   arcBoost        → componente verticale aggiuntiva per la parabola (default 0.5)
///   playerLayer     → LayerMask del layer "Player" per il danno ad area
///   impactVFXPrefab → ereditato da Projectile
/// </summary>
public class LuciferFireball : Projectile
{
    [Header("Fireball Config")]
    [Tooltip("Raggio del danno ad area all'impatto.")]
    public float splashRadius = 3f;

    [Tooltip("Velocità iniziale del fireball in unità/secondo.")]
    public float projectileSpeed = 14f;

    [Tooltip("Spinta verticale aggiuntiva per creare la parabola.")]
    [Range(0f, 5f)]
    public float arcBoost = 0.5f;

    [Tooltip("LayerMask del Player — usata per il danno ad area all'impatto.")]
    public LayerMask playerLayer;

    // ─── API pubblica chiamata da LuciferBoss.cs ──────────────────────────────

    /// <summary>
    /// Calcola la velocità parabolica verso targetPosition e lancia il fireball.
    /// Chiamato da LuciferBoss.cs immediatamente dopo Get() dal pool.
    /// </summary>
    public void LaunchToward(Vector3 targetPosition)
    {
        Vector3 origin = transform.position;
        Vector3 toTarget = targetPosition - origin;
        Vector3 toTargetFlat = new Vector3(toTarget.x, 0f, toTarget.z);
        float horizontalDist = toTargetFlat.magnitude;

        // Direzione base verso il target
        Vector3 direction = toTarget.normalized;

        // Aggiunge componente verticale proporzionale alla distanza orizzontale
        direction.y += arcBoost * Mathf.Clamp01(horizontalDist / 20f);
        direction = direction.normalized;

        // Usa il Launch() di Projectile.cs che setta Rigidbody.velocity
        Launch(direction * projectileSpeed);
    }

    // ─── Override OnTriggerEnter per danno ad area ────────────────────────────

    /// <summary>
    /// Sovrascrive il comportamento base di Projectile.cs:
    /// invece di danno diretto, fa un OverlapSphere al contatto
    /// e applica TakeDamage a tutti i collider nel raggio splash
    /// che appartengono al layer Player.
    /// </summary>
    protected override void OnContactHit(Vector3 contactPoint)
    {
        // Danno ad area — colpisce il player se nel raggio splash
        Collider[] hits = Physics.OverlapSphere(contactPoint, splashRadius, playerLayer);
        foreach (var col in hits)
        {
            Health health = col.GetComponent<Health>();
            if (health == null)
                health = col.GetComponentInParent<Health>();
            health?.TakeDamage(damage);
        }

        // VFX impatto (ereditato da Projectile)
        SpawnImpactVFX(contactPoint);
    }

#if UNITY_EDITOR
    // Mostra il raggio splash nella scena
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, splashRadius);
    }
#endif
}