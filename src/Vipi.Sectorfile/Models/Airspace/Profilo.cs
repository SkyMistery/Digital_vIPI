using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Un'impostazione di un profilo <c>.cpr</c> (<c>PREFS\TWR.cpr</c>; lotto «Subito» slice 11d, «file per file» N1): una riga
/// <c>Chiave=Valore</c> nella sua sezione (<c>[PREFS]</c>, <c>[INSET1]</c>; vuota prima della prima). Il formato è quello
/// dei profili di Aurora, e 🔴 ogni impostazione sovrascrive quella dell'utente a ogni connessione alla posizione.
/// </summary>
public sealed class ImpostazioneDelProfilo
{
    /// <summary>La sezione, senza parentesi; vuota per le righe prima della prima (<c>PAR_VERTICAL_SCAN=30</c>).</summary>
    public string Sezione { get; set; } = string.Empty;

    /// <summary>La chiave, com'è scritta prima del primo <c>=</c> (<c>INS1PAR_Radial</c>).</summary>
    public string Chiave { get; set; } = string.Empty;

    /// <summary>Il valore, com'è scritto dopo il primo <c>=</c>.</summary>
    public string Valore { get; set; } = string.Empty;

    public SourceRef Source { get; set; } = null!;
}
