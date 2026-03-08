using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — LuciferFireballPool.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Pool dedicato per i fireball di Lucifero. Estende ProjectilePool.
/// Singleton per-scena — accessibile via LuciferFireballPool.Instance.
///
/// INSPECTOR:
///   prefab      → Assets/_Project/Prefabs/Projectiles/LuciferFireball.prefab
///   initialSize → 6 (Lucifero lancia max 1-2 fireball per volta)
///
/// GERARCHIA: metti questo componente su un GameObject "Pool_LuciferFireballs"
/// nella scena Level_Boss_Lucifer.
/// </summary>
public class LuciferFireballPool : ProjectilePool
{
    public static LuciferFireballPool Instance { get; private set; }

    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        base.Awake();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}