using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — CanvasContentData.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// ScriptableObject con i testi mostrati nella loading screen.
/// Crea un asset per ogni livello:
///   Assets → Create → Dante/Canvas Content Data
/// </summary>
[CreateAssetMenu(menuName = "Dante/Canvas Content Data", fileName = "CanvasContentData_Level_")]
public class CanvasContentData : ScriptableObject
{
    [Header("Testi Loading Screen")]
    [Tooltip("Titolo del livello — mostrato in grassetto e italics.")]
    public string header = "TITOLO LIVELLO";

    [Tooltip("Descrizione o regole del livello — mostrato in italics.")]
    [TextArea(3, 8)]
    public string testo = "Descrizione del livello.";

    [Tooltip("Testo mostrato sopra la barra quando il caricamento è completo e si attende INVIO.")]
    public string premiInvio = "Premi INVIO per continuare";
}