namespace Vipi.Ui.Components.App;

/// <summary>
/// Quale campo di una radioassistenza si sta scrivendo. ⚠️ Esiste perché la scrittura è <b>per campo</b> e
/// non per riga (carta vSOP militari §12b): salvando tutta la riga, chi cambia la frequenza e chi cambia le
/// coordinate si sovrascriverebbero a vicenda senza aver toccato la stessa cosa.
/// </summary>
public enum NavaidCampo
{
    /// <summary>Il tipo <b>mostrato</b> (VORTACAN su un VOR): non è mai della sorgente.</summary>
    Tipo,

    Frequenza,
    Canale,

    /// <summary>La coppia, scritta in sessagesimale. ⚠️ Una sola voce per le due metà: una latitudine senza
    /// la sua longitudine non è una posizione.</summary>
    Coordinate,
}
