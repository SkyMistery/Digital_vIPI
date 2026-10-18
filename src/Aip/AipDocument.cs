using System.Text.Json.Serialization;

namespace Aip;

/// <summary>
/// Quel che si è letto dall'AIP di uno Stato: il documento che passa dall'importer al converter, al Lab e
/// all'«AIP espanso» (carta F6 §3). Un documento è di <b>uno</b> Stato e, di solito, di un ciclo AIRAC.
/// <para>Si scrive e si rilegge con <see cref="AipJson"/>; la forma del file la dice
/// <see cref="SchemaVersion"/>.</para>
/// </summary>
public sealed record AipDocument
{
    /// <summary>La versione dello schema che questo codice scrive, e la più alta che sa leggere.</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>
    /// Lo Stato da cui viene, con le lettere di nazionalità dell'ICAO (Doc 7910) che aprono i suoi
    /// identificativi: <c>LI</c> l'Italia, <c>LF</c> la Francia, <c>ED</c> la Germania.
    /// </summary>
    public required string State { get; init; }

    /// <summary>Il ciclo AIRAC nella forma <c>AACC</c> (<c>2609</c>), se il documento è di un ciclo solo.</summary>
    public string? Airac { get; init; }

    /// <summary>La data di entrata in vigore del ciclo. Le pagine possono essere più vecchie: vedi <see cref="AipPage"/>.</summary>
    public DateOnly? Effective { get; init; }

    /// <summary>ENR 2 (spazi aerei ATS) ed ENR 5 (aree P/R/D, TSA/TRA, militari, pericoli), nell'ordine dell'AIP.</summary>
    public IReadOnlyList<Airspace> Airspaces { get; init; } = [];

    public bool Equals(AipDocument? other) =>
        other is not null
        && SchemaVersion == other.SchemaVersion
        && State == other.State
        && Airac == other.Airac
        && Effective == other.Effective
        && Airspaces.SequenceEqual(other.Airspaces);

    public override int GetHashCode() => HashCode.Combine(SchemaVersion, State, Airac, Effective, Airspaces.Count);
}

/// <summary>
/// Uno spazio aereo o un'area, come lo scrive l'AIP: chi è, quanto è alto, dove sta, e da che pagina viene.
/// </summary>
public sealed record Airspace
{
    /// <summary>La sezione dell'AIP, con la numerazione dell'Annesso 15: <c>ENR 2.1</c>, <c>ENR 5.1</c>.</summary>
    public required string Section { get; init; }

    public required AirspaceType Type { get; init; }

    /// <summary>
    /// L'identificativo come lo stampa l'AIP (<c>LI R107A</c>, <c>MILANO CTA</c>, <c>Zone '18'</c>), senza
    /// correggerlo: un identificativo fuori standard si vede, non si aggiusta in silenzio.
    /// </summary>
    public required string Designator { get; init; }

    /// <summary>Il nome, se l'AIP ne dà uno distinto dall'identificativo.</summary>
    public string? Name { get; init; }

    public required VerticalLimit Upper { get; init; }

    public required VerticalLimit Lower { get; init; }

    /// <summary>La classe dello spazio aereo (<c>A</c>…<c>G</c>), dove l'AIP la dice.</summary>
    public string? Class { get; init; }

    /// <summary>
    /// Il tipo di attività, come lo stampa l'AIP nelle aree P, R e D (<c>Military firing</c>, <c>Parachuting</c>…);
    /// vuoto dove l'AIP non lo dice.
    /// </summary>
    public string? Activity { get; init; }

    /// <summary>
    /// Le note dell'AIP sull'oggetto (orari, ente, condizioni), il testo com'è, a capo compresi (committente,
    /// 18 ottobre: anche CTR e aree P/R/D le hanno, e servono).
    /// </summary>
    public string? Remarks { get; init; }

    public required Geometry Geometry { get; init; }

    /// <summary>La pagina da cui viene, con la sua data AIRAC: ogni pagina dell'AIP ha la sua.</summary>
    public AipPage? Page { get; init; }

    /// <summary>
    /// La chiave di un'area dentro uno Stato: identificativo <b>più</b> nome. 🔴 L'identificativo da solo non
    /// basta: nel 2609 <c>LI TSA74</c> compare due volte, con due nomi (carta madre §5).
    /// </summary>
    [JsonIgnore]
    public string Key => Name is null ? Designator : $"{Designator} {Name}";
}

/// <summary>Il riferimento della pagina (<c>ENR 5.1.2-3</c>) e la data AIRAC stampata su di lei.</summary>
public sealed record AipPage(string Reference, DateOnly? Effective = null);

/// <summary>
/// Il tipo di uno spazio aereo, coi nomi dell'Annesso 11 e dell'Annesso 15. Quel che un importer non sa
/// mettere in uno di questi va in <see cref="Other"/>, e l'identificativo resta com'era.
/// </summary>
public enum AirspaceType
{
    Other,

    // ENR 2.1 — FIR, UIR, aree di controllo, TMA, CTR
    Fir,
    Uir,
    Cta,
    Uta,
    Tma,
    Ctr,

    // ENR 2.2 — gli altri spazi regolati
    Atz,
    Rmz,
    Tmz,

    // ENR 5.1 — aree vietate, regolamentate, pericolose; spazi riservati
    Prohibited,
    Restricted,
    Danger,
    Tsa,
    Tra,
    Cba,

    // ENR 5.2 — esercitazioni e addestramento militari
    Military,
}
