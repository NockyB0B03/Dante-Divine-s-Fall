using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — FireOrbPool.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Dedicated pool for Dante's FireOrb projectile.
/// Singleton — accessed via FireOrbPool.Instance from FireAbility.cs.
///
/// INSPECTOR SETUP:
///   prefab      → Assets/_Project/Prefabs/Projectiles/FireOrb.prefab
///   initialSize → 10 (Dante can only fire one at a time with cooldown — 10 is safe)
///
/// HIERARCHY PLACEMENT:
///   Place this component on a GameObject in the Level_Boss_Lucifer scene.
///   Suggested name: "Pool_FireOrbs"
/// </summary>
public class FireOrbPool : ProjectilePool
{
    public static FireOrbPool Instance { get; private set; }

    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        base.Awake();   // runs pre-warm
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}