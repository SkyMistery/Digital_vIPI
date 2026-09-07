namespace Vipi.Ui.Components.App;

/// <summary>
/// Quale campo di una radioassistenza si sta scrivendo. ⚠️ Esiste perché la scrittura è <b>per campo</b> e
/// non per riga (carta vSOP militari §12b): salvando tutta la riga, chi cambia la frequenza e chi cambia le
/// coordinate si sovrascriverebbero a vicenda senza aver toccato la stessa cosa.
/// </summary>
// ⚠️ Pubblico per FORZA: compare in un `[Parameter]` di un componente Razor, e la classe che Razor
// genera è pubblica. Un tipo che sta nella firma di un componente è superficie del modulo quanto il
// componente stesso (ADR-0005 D6, revisione del 6 settembre 2026, R-009).
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
