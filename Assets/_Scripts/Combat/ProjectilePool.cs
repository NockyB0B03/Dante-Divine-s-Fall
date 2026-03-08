using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — ProjectilePool.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Base class for dedicated per-prefab object pools.
/// Subclass this to create FireOrbPool, LuciferFireballPool, etc.
/// Each pool is a singleton that lives in the boss level scene.
///
/// USAGE (from a subclass):
///   public class FireOrbPool : ProjectilePool { }
///
/// INSPECTOR SETUP (on the subclass component):
///   prefab        → drag the FireOrb prefab
///   initialSize   → how many instances to pre-warm at Awake (e.g. 10)
///   parentName    → name of the container GameObject (e.g. "Pool_FireOrbs")
///
/// HOW IT WORKS:
///   - Awake() pre-instantiates <initialSize> copies, deactivates them.
///   - Get()    finds the first inactive instance; if none, expands the pool by 1.
///   - Return() simply deactivates the instance — no Destroy() ever called.
///   - Projectile.cs calls Return() on itself when it hits something or expires.
/// </summary>
public abstract class ProjectilePool : MonoBehaviour
{
    [Header("Pool Config")]
    [Tooltip("The projectile prefab this pool manages.")]
    public GameObject prefab;

    [Tooltip("Number of instances pre-created at startup.")]
    public int initialSize = 10;

    [Tooltip("Name of the hierarchy container GameObject created at runtime.")]
    public string containerName = "ProjectilePool_Container";

    // Internal pool storage
    private List<GameObject> _pool = new List<GameObject>();
    private Transform _container;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    protected virtual void Awake()
    {
        if (prefab == null)
        {
            Debug.LogError($"[{GetType().Name}] Prefab is not assigned!");
            return;
        }

        // Create a container to keep the hierarchy clean
        _container = new GameObject(containerName).transform;
        _container.SetParent(transform);

        // Pre-warm the pool
        for (int i = 0; i < initialSize; i++)
            _pool.Add(CreateInstance());
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves an inactive instance from the pool.
    /// If all are active, expands the pool by one (logged as a warning).
    /// </summary>
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        foreach (var obj in _pool)
        {
            if (!obj.activeInHierarchy)
            {
                obj.transform.SetPositionAndRotation(position, rotation);
                obj.SetActive(true);
                return obj;
            }
        }

        // Pool exhausted — expand
        Debug.LogWarning($"[{GetType().Name}] Pool exhausted — expanding. Consider increasing initialSize.");
        GameObject newObj = CreateInstance();
        _pool.Add(newObj);
        newObj.transform.SetPositionAndRotation(position, rotation);
        newObj.SetActive(true);
        return newObj;
    }

    /// <summary>
    /// Returns an instance to the pool by deactivating it.
    /// Called by Projectile.cs — never call Destroy() on pooled objects.
    /// </summary>
    public void Return(GameObject obj)
    {
        obj.SetActive(false);
    }

    // ─── Private ──────────────────────────────────────────────────────────────
    private GameObject CreateInstance()
    {
        GameObject obj = Instantiate(prefab, _container);
        obj.SetActive(false);

        // Give the projectile a reference back to this pool
        Projectile proj = obj.GetComponent<Projectile>();
        if (proj != null)
            proj.OwnerPool = this;
        else
            Debug.LogWarning($"[{GetType().Name}] Prefab '{prefab.name}' has no Projectile component.");

        return obj;
    }
}