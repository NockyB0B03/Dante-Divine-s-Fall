using System.Collections;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — VirgilioOrbit.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Virgilio è uno spiritello fluttuante che orbita le spalle di Dante.
/// NON è figlio di Dante in gerarchia — legge danteTransform ogni frame.
///
/// MOVIMENTO BASE:
///   Orbita semicircolare alle spalle di Dante su piano XZ, con bob sinusoidale
///   sull'asse Y. Un Lerp morbido introduce un ritardo poetico sui movimenti rapidi.
///
/// REAZIONE ALL'HEAL (eventi statici da HealAbility.cs):
///   OnHealReady → cambia colore verso readyColor + esegue il saltino (bounce).
///   OnHealUsed  → torna al colore base normalColor.
///   I due script non si referenziano mai direttamente.
///
/// INSPECTOR SETUP:
///   danteTransform   → trascina il GameObject di Dante
///   orbitRadius      → raggio dell'orbita (default 1.2)
///   orbitSpeed       → velocità angolare rad/s (default 1.8)
///   heightOffset     → altezza sopra l'origine di Dante (default 1.5)
///   bobAmplitude     → ampiezza del bob Y (default 0.15)
///   bobFrequency     → frequenza del bob Y (default 2.0)
///   followLerpSpeed  → velocità di inseguimento posizione (default 8)
///   normalColor      → colore di Virgilio a riposo
///   readyColor       → colore quando la cura è disponibile
///   bounceHeight     → altezza del saltino in unità (default 0.4)
///   bounceDuration   → durata del saltino in secondi (default 0.35)
///   rendererTarget   → il Renderer di Virgilio (per il cambio colore)
///
/// NOTA SUL COLORE:
///   Il cambio colore modifica MaterialPropertyBlock per non alterare il
///   materiale condiviso (asset-safe, no istanza runtime del materiale).
/// </summary>
public class VirgilioOrbit : MonoBehaviour
{
    // ─── Orbita ───────────────────────────────────────────────────────────────
    [Header("Orbit Target")]
    public Transform danteTransform;

    [Header("Orbit Parameters")]
    public float orbitRadius = 1.2f;
    public float orbitSpeed = 1.8f;
    public float heightOffset = 1.5f;
    public float bobAmplitude = 0.15f;
    public float bobFrequency = 2.0f;

    [Header("Smoothing")]
    public float followLerpSpeed = 8f;

    // ─── Reazione Heal ────────────────────────────────────────────────────────
    [Header("Heal Visual Reaction")]
    [Tooltip("Renderer di Virgilio — Mesh Renderer o Skinned Mesh Renderer.")]
    public Renderer rendererTarget;

    [Tooltip("Colore di Virgilio a riposo.")]
    public Color normalColor = new Color(0.7f, 0.85f, 1f);     // azzurro tenue

    [Tooltip("Colore di Virgilio quando la cura è disponibile.")]
    public Color readyColor = new Color(0.3f, 1f, 0.4f);      // verde brillante

    [Tooltip("Altezza del saltino in unità Unity.")]
    public float bounceHeight = 0.4f;

    [Tooltip("Durata totale del saltino (su + giù) in secondi.")]
    public float bounceDuration = 0.35f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private float _angle;
    private float _bobTime;

    // Offset Y aggiunto al di sopra del calcolo orbit — gestito dal bounce
    private float _bounceOffsetY = 0f;

    private MaterialPropertyBlock _propBlock;
    private static readonly int _colorID = Shader.PropertyToID("_Color");

    private bool _isReady = false;
    private Coroutine _bounceRoutine;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
        ApplyColor(normalColor);
    }

    void OnEnable()
    {
        HealAbility.OnHealReady += HandleHealReady;
        HealAbility.OnHealUsed += HandleHealUsed;
    }

    void OnDisable()
    {
        HealAbility.OnHealReady -= HandleHealReady;
        HealAbility.OnHealUsed -= HandleHealUsed;
    }

    void Update()
    {
        if (danteTransform == null) return;

        // ── Aggiorna angolo e bob ─────────────────────────────────────────
        _angle += orbitSpeed * Time.deltaTime;
        _bobTime += bobFrequency * Time.deltaTime;

        // ── Posizione target: semicerchio alle SPALLE di Dante ────────────
        // Le spalle = direzione opposta al forward di Dante → offset angolo di π
        float backAngle = _angle + Mathf.PI;

        float x = Mathf.Cos(backAngle) * orbitRadius;
        float z = Mathf.Sin(backAngle) * orbitRadius;
        float y = heightOffset
                  + Mathf.Sin(_bobTime) * bobAmplitude
                  + _bounceOffsetY;           // saltino addizionale

        Vector3 targetPos = danteTransform.position + new Vector3(x, y, z);

        // ── Smooth follow ─────────────────────────────────────────────────
        transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            followLerpSpeed * Time.deltaTime);

        // ── Guarda sempre verso Dante ─────────────────────────────────────
        transform.LookAt(danteTransform.position + Vector3.up * heightOffset);
    }

    // ─── Handler Eventi ───────────────────────────────────────────────────────

    /// <summary>
    /// Chiamato da HealAbility quando il cooldown è terminato.
    /// Cambia colore e fa il saltino.
    /// </summary>
    private void HandleHealReady()
    {
        _isReady = true;
        ApplyColor(readyColor);

        // Interrompe un eventuale bounce precedente ancora in corso
        if (_bounceRoutine != null)
            StopCoroutine(_bounceRoutine);

        _bounceRoutine = StartCoroutine(BounceRoutine());
    }

    /// <summary>
    /// Chiamato da HealAbility quando il player usa la cura.
    /// Torna al colore normale.
    /// </summary>
    private void HandleHealUsed()
    {
        _isReady = false;
        ApplyColor(normalColor);
    }

    // ─── Coroutine Saltino ────────────────────────────────────────────────────

    /// <summary>
    /// Anima _bounceOffsetY con una curva sinusoidale:
    /// sale rapidamente nella prima metà, scende nella seconda.
    /// Si somma al bob normale senza interferire con esso.
    /// </summary>
    private IEnumerator BounceRoutine()
    {
        float elapsed = 0f;

        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bounceDuration;

            // Sin(0→π) genera un arco pulito: parte da 0, picco a metà, torna a 0
            _bounceOffsetY = Mathf.Sin(t * Mathf.PI) * bounceHeight;

            yield return null;
        }

        _bounceOffsetY = 0f;
        _bounceRoutine = null;
    }

    // ─── Colore ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Applica il colore tramite MaterialPropertyBlock —
    /// non istanzia un nuovo materiale, sicuro per la memoria.
    /// </summary>
    private void ApplyColor(Color color)
    {
        if (rendererTarget == null) return;

        rendererTarget.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(_colorID, color);
        rendererTarget.SetPropertyBlock(_propBlock);
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("DEBUG — Simula Heal Ready")]
    private void Debug_HealReady() => HandleHealReady();

    [ContextMenu("DEBUG — Simula Heal Used")]
    private void Debug_HealUsed() => HandleHealUsed();

    void OnDrawGizmosSelected()
    {
        if (danteTransform == null) return;
        UnityEditor.Handles.color = new Color(0.3f, 1f, 0.4f, 0.2f);
        UnityEditor.Handles.DrawWireDisc(
            danteTransform.position + Vector3.up * heightOffset,
            Vector3.up,
            orbitRadius);
    }
#endif
}