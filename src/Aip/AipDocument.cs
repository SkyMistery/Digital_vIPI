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
    /// <summary>
    /// La versione dello schema che questo codice scrive, e la più alta che sa leggere. La 2 (carta F6, slice 4a) ha la
    /// geometria <see cref="CorridorGeometry"/>; un file della 1 si legge com'è e, riscritto, resta 1.
    /// </summary>
    public const int CurrentSchemaVersion = 2;

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

    /// <summary>ENR 2.1.1: le linee che dividono le aree degli ACC/FIC, dalle note delle FIR. Null se il documento non ne ha.</summary>
    public IReadOnlyList<AtsUnitLine>? UnitLines { get; init; }

    /// <summary>ENR 2.1.1: le righe delle tabelle delle frequenze ACC, SCCAM e FIC. Null se il documento non ne ha (campo facoltativo).</summary>
    public IReadOnlyList<AtsUnitFrequencies>? UnitFrequencies { get; init; }

    /// <summary>ENR 3: le rotte ATS, nell'ordine in cui si sono lette. Null se il documento non ne ha (lo schema resta 1).</summary>
    public IReadOnlyList<Route>? Routes { get; init; }

    /// <summary>ENR 3.4.1: le attese in rotta. Null se il documento non ne ha.</summary>
    public IReadOnlyList<Holding>? Holdings { get; init; }

    /// <summary>
    /// ENR 3.4.2: l'elenco delle radioassistenze con le rotte e procedure associate, anche quelle senza attese. Null se il
    /// documento non ne ha (campo facoltativo).
    /// </summary>
    public IReadOnlyList<NavaidProcedures>? NavaidProcedures { get; init; }

    /// <summary>ENR 4.1: le radioassistenze in rotta. Null se il documento non ne ha (lo schema resta 1).</summary>
    public IReadOnlyList<Navaid>? Navaids { get; init; }

    /// <summary>ENR 4.4: i punti significativi. Null se il documento non ne ha.</summary>
    public IReadOnlyList<SignificantPoint>? Points { get; init; }

    /// <summary>ENR 4.5: le luci aeronautiche al suolo. Null se il documento non ne ha.</summary>
    public IReadOnlyList<GroundLight>? Lights { get; init; }

    /// <summary>AD 2: gli scali (slice 5). Null se il documento non ne ha (lo schema non cambia: un campo facoltativo).</summary>
    public IReadOnlyList<Aerodrome>? Aerodromes { get; init; }

    /// <summary>ENR 5.3: i pericoli puntuali (palloni sonda, laser, vulcani). Null se il documento non ne ha (campo facoltativo).</summary>
    public IReadOnlyList<Hazard>? Hazards { get; init; }

    /// <summary>AD 2: i settori delle altitudini minime di sorveglianza (ATC SMAC). Null se il documento non ne ha.</summary>
    public IReadOnlyList<MinimumAltitudeArea>? MinimumAltitudes { get; init; }

    /// <summary>AD 2: le procedure — SID, STAR, transizioni, salite iniziali (carta F7). Null se il documento non ne ha.</summary>
    public IReadOnlyList<Procedure>? Procedures { get; init; }

    public bool Equals(AipDocument? other) =>
        other is not null
        && SchemaVersion == other.SchemaVersion
        && State == other.State
        && Airac == other.Airac
        && Effective == other.Effective
        && Airspaces.SequenceEqual(other.Airspaces)
        && (UnitFrequencies ?? []).SequenceEqual(other.UnitFrequencies ?? [])
        && (UnitLines ?? []).SequenceEqual(other.UnitLines ?? [])
        && (Routes ?? []).SequenceEqual(other.Routes ?? [])
        && (Holdings ?? []).SequenceEqual(other.Holdings ?? [])
        && (NavaidProcedures ?? []).SequenceEqual(other.NavaidProcedures ?? [])
        && (Navaids ?? []).SequenceEqual(other.Navaids ?? [])
        && (Points ?? []).SequenceEqual(other.Points ?? [])
        && (Lights ?? []).SequenceEqual(other.Lights ?? [])
        && (Aerodromes ?? []).SequenceEqual(other.Aerodromes ?? [])
        && (Hazards ?? []).SequenceEqual(other.Hazards ?? [])
        && (MinimumAltitudes ?? []).SequenceEqual(other.MinimumAltitudes ?? [])
        && (Procedures ?? []).SequenceEqual(other.Procedures ?? []);

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

    /// <summary>
    /// Le fasce verticali, quando l'area ne ha più d'una con classi diverse (la UIR di Milano: da UNL a FL660 «G», da FL660
    /// a FL195 «C»). Vuoto per un'area di una fasca sola: allora valgono <see cref="Upper"/>, <see cref="Lower"/> e
    /// <see cref="Class"/>, che con più fasce sono il limite più alto, il più basso e niente. Null con una fascia sola.
    /// </summary>
    public IReadOnlyList<VerticalBand>? Bands { get; init; }

    /// <summary>L'ente che fornisce il servizio, o il nominativo e la lingua, come li stampa l'AIP (ENR 2).</summary>
    public string? Unit { get; init; }

    /// <summary>Gli orari di attività o di servizio, come li stampa l'AIP (RMZ, TMZ).</summary>
    public string? Hours { get; init; }

    /// <summary>L'altitudine di transizione, se l'AIP la dà con l'area (ATZ, CTR).</summary>
    public string? TransitionAltitude { get; init; }

    public required Geometry Geometry { get; init; }

    /// <summary>La pagina da cui viene, con la sua data AIRAC: ogni pagina dell'AIP ha la sua.</summary>
    public AipPage? Page { get; init; }

    /// <summary>
    /// La chiave di un'area dentro uno Stato: identificativo <b>più</b> nome. 🔴 L'identificativo da solo non
    /// basta: nel 2609 <c>LI TSA74</c> compare due volte, con due nomi (carta madre §5).
    /// </summary>
    [JsonIgnore]
    public string Key => Name is null ? Designator : $"{Designator} {Name}";

    // ⚠️ Scritta a mano per le fasce: l'uguaglianza dei record confronterebbe la lista per riferimento, e un'area riletta
    // dal JSON non sarebbe più uguale a sé stessa. Un campo nuovo va aggiunto anche qui (lo prova AipJsonTests).
    public bool Equals(Airspace? other) =>
        other is not null
        && Section == other.Section && Type == other.Type && Designator == other.Designator && Name == other.Name
        && Upper == other.Upper && Lower == other.Lower && Class == other.Class
        && Activity == other.Activity && Remarks == other.Remarks
        && (Bands ?? []).SequenceEqual(other.Bands ?? [])
        && Unit == other.Unit && Hours == other.Hours && TransitionAltitude == other.TransitionAltitude
        && Equals(Geometry, other.Geometry) && Page == other.Page;

    public override int GetHashCode() => HashCode.Combine(Section, Type, Designator, Name);
}

/// <summary>Una fascia verticale di un'area con più fasce: i suoi limiti e la sua classe.</summary>
public sealed record VerticalBand(VerticalLimit Upper, VerticalLimit Lower, string? Class);

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
    Npz,

    /// <summary>Spazio aereo a rotte libere (Free Route Airspace: in Italia il FRAIT).</summary>
    Fra,

    // ENR 5.1 — aree vietate, regolamentate, pericolose; spazi riservati
    Prohibited,
    Restricted,
    Danger,
    Tsa,
    Tra,
    Cba,

    // ENR 5.2 — esercitazioni e addestramento militari, zona di identificazione di difesa aerea
    Military,
    Adiz,
}
