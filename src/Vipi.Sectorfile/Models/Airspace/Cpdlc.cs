using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Un messaggio CPDLC di <c>[CPDLC]</c> (<c>ita.cpdlc</c>; manuale IVAO «Aurora CPDLC Sectorfile», lotto «Subito» slice 11c):
/// <c>Comando;Risposta;Gruppo;ReplVal1-4;AtVal1-4;TpVal1-4;TotVal;</c>. Il comando ha al più 128 caratteri e i valori da
/// riempire scritti <c>[0]</c>…<c>[3]</c>; i tredici campi dei valori li prepara uLink e si tengono come sono.
/// </summary>
public sealed class MessaggioCpdlc
{
    /// <summary>Il testo del messaggio (<c>CLIMB TO [0]</c>).</summary>
    public string Comando { get; set; } = string.Empty;

    /// <summary>La risposta attesa: <c>WU</c> (WILCO/UNABLE), <c>AN</c> (AFFIRMATIVE/NEGATIVE), <c>R</c> (ROGER), <c>NE</c> (nessuna ora).</summary>
    public string Risposta { get; set; } = string.Empty;

    /// <summary>Il gruppo della finestra CPDLC di Aurora: da 0 a 18, 20 per il DCL. Com'è scritto.</summary>
    public string Gruppo { get; set; } = string.Empty;

    /// <summary>I tredici campi dei valori (ReplVal, AtVal, TpVal, TotVal), com'erano scritti: li prepara uLink.</summary>
    public IList<string> Valori { get; } = new List<string>();

    /// <summary>Quanti valori dichiara il messaggio (TotVal, l'ultimo dei campi dei valori); null se non è un numero.</summary>
    public int? TotaleDeiValori => Valori.Count == 13 && int.TryParse(Valori[12].Trim(), out int n) ? n : null;

    public SourceRef Source { get; set; } = null!;
}

/// <summary>
/// Il nome di un gruppo della finestra CPDLC (<c>[CPDLCNAMES]</c>, <c>ita.cpdlcnames</c>): <c>GROUP.15;TWR;</c>. Gruppi da 0
/// a 18, nome di al più 20 caratteri (manuale IVAO, slice 11c).
/// </summary>
public sealed class NomeDelGruppoCpdlc
{
    /// <summary>Il numero del gruppo, com'è scritto dopo <c>GROUP.</c>.</summary>
    public string Gruppo { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public SourceRef Source { get; set; } = null!;
}
