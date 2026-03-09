using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — ArrowPool.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Pool dedicato per le frecce delle trappole.
/// NON estende ProjectilePool — le frecce non hanno Rigidbody e
/// usano Arrow.cs invece di Projectile.cs, quindi serve un pool dedicato.
///
/// INSPECTOR:
///   arrowPrefab → Assets/_Project/Prefabs/Projectiles/Arrow.prefab
///   initialSize → 20 (più trappole nella scena = aumenta questo valore)
///
/// GERARCHIA: posiziona su un GameObject "Pool_Arrows" nella scena Level 2.
/// Singleton per-scena — accessibile via ArrowPool.Instance.
/// </summary>
public class ArrowPool : MonoBehaviour
{
    public static ArrowPool Instance { get; private set; }

    [Header("Pool Config")]
    public GameObject arrowPrefab;
    public int initialSize = 20;

    private List<GameObject> _pool = new List<GameObject>();
    private Transform _container;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _container = new GameObject("ArrowPool_Container").transform;
        _container.SetParent(transform);

        for (int i = 0; i < initialSize; i++)
            _pool.Add(CreateInstance());
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── API pubblica ─────────────────────────────────────────────────────────

    /// <summary>
    /// Restituisce una freccia inattiva posizionata e orientata.
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

        // Pool esaurito — espandi
        Debug.LogWarning("[ArrowPool] Pool esaurito — espansione. Aumenta initialSize.");
        GameObject newObj = CreateInstance();
        _pool.Add(newObj);
        newObj.transform.SetPositionAndRotation(position, rotation);
        newObj.SetActive(true);
        return newObj;
    }

    /// <summary>
    /// Restituisce una freccia al pool disattivandola.
    /// </summary>
    public void Return(GameObject obj) => obj.SetActive(false);

    // ─── Privati ──────────────────────────────────────────────────────────────
    private GameObject CreateInstance()
    {
        if (arrowPrefab == null)
        {
            Debug.LogError("[ArrowPool] arrowPrefab non assegnato!");
            return null;
        }

        GameObject obj = Instantiate(arrowPrefab, _container);
        obj.SetActive(false);

        Arrow arrow = obj.GetComponent<Arrow>();
        if (arrow != null)
            arrow.OwnerPool = this;
        else
            Debug.LogWarning("[ArrowPool] Il prefab non ha il componente Arrow.cs!");

        return obj;
    }
}