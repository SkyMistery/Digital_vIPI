namespace Aip;

/// <summary>
/// Una frequenza di una riga di <see cref="AtsUnitFrequencies"/>: com'è stampata (<c>CH 118.805</c>: il «CH» è il canale a
/// 8,33 kHz), il valore in MHz, l'emissione (<c>A3E</c>) e i rimandi alle note (<c>(3)</c>).
/// </summary>
public sealed record AtsFrequency
{
    public required string Text { get; init; }

    public required double Megahertz { get; init; }

    public string? Emission { get; init; }

    public string? NoteRefs { get; init; }
}

/// <summary>
/// Una riga della tabella delle frequenze di una FIR (ENR 2.1.1.x, «Frequenze ACC/SCCAM» e FIC): la stazione, il servizio,
/// il nominativo e le frequenze (carta «L'AIP intero», passo 2b). ⚠️ Gli orari e le note sono quelli <b>della riga</b>, come
/// stampati e nell'ordine: dentro una riga l'AIP non ha bordi fra una frequenza e l'altra e le celle degli orari stanno a
/// cavallo (Roma FIC nel 2611: «2200-0600 (2100-0500)» a metà fra 128.800 e 133.250), quindi l'orario di una frequenza non
/// si legge — si legge quello della riga.
/// </summary>
public sealed record AtsUnitFrequencies
{
    public required string Section { get; init; }

    /// <summary>La stazione: <c>Milano ACC</c>, <c>Roma FIC</c>, <c>SCCAM</c>.</summary>
    public string? Station { get; init; }

    /// <summary>Il servizio com'è stampato, italiano e inglese: <c>Servizio di controllo di area/ Area control service</c>.</summary>
    public string? Service { get; init; }

    /// <summary>Il nominativo: <c>Milano Radar</c>, <c>Roma Informazioni/ Roma Information</c>.</summary>
    public string? CallSign { get; init; }

    public IReadOnlyList<AtsFrequency> Frequencies { get; init; } = [];

    /// <summary>Gli orari della riga, uno per riga di testo, nell'ordine stampato (<c>H24</c>, <c>0600-2200</c>, <c>(0500-2100)</c>).</summary>
    public string? Hours { get; init; }

    /// <summary>Le note scritte nella riga, com'è stampate (anche le aree per punti delle frequenze VFR).</summary>
    public string? Remarks { get; init; }

    public AipPage? Page { get; init; }

    public bool Equals(AtsUnitFrequencies? other) =>
        other is not null && Section == other.Section && Station == other.Station && Service == other.Service
        && CallSign == other.CallSign && Frequencies.SequenceEqual(other.Frequencies) && Hours == other.Hours
        && Remarks == other.Remarks && Page == other.Page;

    public override int GetHashCode() => HashCode.Combine(Section, Station, CallSign, Frequencies.Count);
}
