namespace Aip;

/// <summary>
/// Uno scalo di AD 2 (carta F6, slice 5): i dati del suo testo (il primo PDF di AD 2 di ogni scalo) — dove sta, quanto è
/// alto, la declinazione, le piste con le soglie e le distanze dichiarate, le frequenze dei servizi. Lo spazio aereo dello
/// scalo (AD 2.17, ATZ e CTR) sta fra gli <see cref="AipDocument.Airspaces"/>, con la sezione <c>AD 2.17</c>.
/// </summary>
public sealed record Aerodrome
{
    /// <summary>L'indicatore di località: <c>LICA</c>.</summary>
    public required string Icao { get; init; }

    /// <summary>Il nome come lo scrive l'AIP: <c>LAMEZIA TERME</c>, <c>MILANO Malpensa</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Il punto di riferimento dello scalo (AD 2.2, 1).</summary>
    public Position? Arp { get; init; }

    /// <summary>L'elevazione com'è stampata: <c>46 FT</c>.</summary>
    public string? Elevation { get; init; }

    /// <summary>La temperatura di riferimento com'è stampata: <c>31.4° C</c>.</summary>
    public string? ReferenceTemperature { get; init; }

    /// <summary>La declinazione magnetica com'è stampata, con l'anno: <c>4° E (2020.0)</c>.</summary>
    public string? MagneticVariation { get; init; }

    /// <summary>La variazione annuale com'è stampata: <c>6'E</c>.</summary>
    public string? AnnualChange { get; init; }

    /// <summary>Il traffico consentito (AD 2.2, 7): <c>IFR/VFR</c>.</summary>
    public string? Traffic { get; init; }

    /// <summary>AD 2.12: le piste, una per designatore (una pista fisica sono due: <c>10</c> e <c>28</c>).</summary>
    public IReadOnlyList<Runway> Runways { get; init; } = [];

    /// <summary>AD 2.13: le distanze dichiarate, anche delle intersezioni.</summary>
    public IReadOnlyList<DeclaredDistance> DeclaredDistances { get; init; } = [];

    /// <summary>AD 2.18: i servizi di comunicazione, uno per frequenza.</summary>
    public IReadOnlyList<CommunicationFacility> Communications { get; init; } = [];

    /// <summary>
    /// AD 2.19: le radioassistenze dello scalo che non sono di un ILS (VOR, DVOR/DME, NDB, locator, un DME da solo), come
    /// quelle di ENR 4.1 ma con la sezione <c>AD 2.19</c>; il nome è la sigla (AD 2.19 non stampa il nome della stazione).
    /// Molte sono anche in ENR 4.1 (CDC, LMT a Lamezia).
    /// </summary>
    public IReadOnlyList<Navaid> NavigationAids { get; init; } = [];

    /// <summary>AD 2.19: gli ILS, uno per pista, con i loro apparati (LOC, GP, DME, radiomarker).</summary>
    public IReadOnlyList<Ils> Ils { get; init; } = [];

    /// <summary>La pagina del testo dello scalo da cui viene (AD 2.2), con la sua data.</summary>
    public AipPage? Page { get; init; }

    // ⚠️ Scritta a mano: l'uguaglianza dei record confronterebbe le liste per riferimento.
    public bool Equals(Aerodrome? other) =>
        other is not null
        && Icao == other.Icao && Name == other.Name && Arp == other.Arp && Elevation == other.Elevation
        && ReferenceTemperature == other.ReferenceTemperature && MagneticVariation == other.MagneticVariation
        && AnnualChange == other.AnnualChange && Traffic == other.Traffic
        && Runways.SequenceEqual(other.Runways) && DeclaredDistances.SequenceEqual(other.DeclaredDistances)
        && Communications.SequenceEqual(other.Communications) && NavigationAids.SequenceEqual(other.NavigationAids)
        && Ils.SequenceEqual(other.Ils) && Page == other.Page;

    public override int GetHashCode() => HashCode.Combine(Icao, Name);
}

/// <summary>
/// Una pista di AD 2.12, per designatore: la rotta vera, le dimensioni, resistenza e superficie, la soglia e la fine della
/// pista (le coordinate della colonna 5), l'elevazione della soglia, l'ondulazione del geoide alla soglia, come stampati.
/// </summary>
public sealed record Runway
{
    /// <summary>Il designatore: <c>10</c>, <c>35L</c>.</summary>
    public required string Designator { get; init; }

    /// <summary>La rotta vera com'è stampata: <c>098.49°</c>.</summary>
    public string? TrueBearing { get; init; }

    /// <summary>Le dimensioni in metri com'è stampato: <c>3015 x 45</c>.</summary>
    public string? Dimensions { get; init; }

    /// <summary>Resistenza e superficie: <c>PCN 58/F/B/W/T ASPH</c>.</summary>
    public string? Surface { get; init; }

    /// <summary>La soglia (la prima coordinata della colonna 5).</summary>
    public Position? Threshold { get; init; }

    /// <summary>La fine della pista (la seconda coordinata della colonna 5), dove l'AIP la dà.</summary>
    public Position? End { get; init; }

    /// <summary>L'elevazione della soglia e la massima della TDZ, com'è stampato: <c>19.3 FT / NIL</c>.</summary>
    public string? ThresholdElevation { get; init; }

    /// <summary>L'ondulazione del geoide alla soglia: <c>140.2 FT</c>.</summary>
    public string? GeoidUndulation { get; init; }

    /// <summary>Le note della pista (AD 2.12, colonna 13): <c>1) DTHR 366 m</c>.</summary>
    public string? Remarks { get; init; }
}

/// <summary>
/// Le distanze dichiarate di AD 2.13, in metri come stampate (<c>-</c> dove non c'è). <paramref name="Runway"/>: la pista;
/// <paramref name="Intersection"/>: per il decollo da un'intersezione, com'è stampato (<c>INT TAKE-OFF B</c>), o null.
/// </summary>
public sealed record DeclaredDistance(string Runway, string? Intersection, string? Tora, string? Toda, string? Asda, string? Lda);

/// <summary>
/// Un servizio di comunicazione di AD 2.18: il servizio (<c>TWR</c>, <c>APP</c>, <c>ATIS</c>), il nominativo
/// (<c>Lamezia TWR</c>), la frequenza com'è stampata (<c>125.900</c>), l'orario e le note.
/// </summary>
public sealed record CommunicationFacility(string Service, string? CallSign, string Frequency, string? Hours = null, string? Remarks = null);

/// <summary>
/// Un ILS di AD 2.19 (carta F6, slice 5d): la pista e la categoria, la sigla del localizzatore, la declinazione stampata, e
/// gli apparati nell'ordine stampato — il localizzatore (<c>LOC</c>) per primo, poi il sentiero di discesa (<c>GP</c>), il
/// DME (<c>DME</c> o <c>DME-P</c>), i radiomarker (<c>OM</c>, <c>MM</c>). Del GP anche la pendenza e l'altezza del
/// riferimento (RDH), dalle sue note.
/// </summary>
public sealed record Ils
{
    /// <summary>La pista: <c>28</c>, <c>35L</c>.</summary>
    public required string Runway { get; init; }

    /// <summary>La categoria com'è stampata: <c>CAT I</c>, <c>CAT IIIB</c>.</summary>
    public string? Category { get; init; }

    /// <summary>La sigla del localizzatore: <c>LAM</c>.</summary>
    public required string Designator { get; init; }

    /// <summary>La declinazione dell'ILS com'è stampata: <c>3° E-2010.0</c>.</summary>
    public string? MagneticVariation { get; init; }

    /// <summary>La pendenza del sentiero di discesa com'è stampata: <c>3°</c>, <c>3.2°</c>.</summary>
    public string? Slope { get; init; }

    /// <summary>L'altezza del riferimento sulla soglia com'è stampata: <c>17 M</c>.</summary>
    public string? ReferenceDatumHeight { get; init; }

    public required IReadOnlyList<IlsComponent> Components { get; init; }

    public AipPage? Page { get; init; }

    // ⚠️ Scritta a mano: l'uguaglianza dei record confronterebbe la lista degli apparati per riferimento.
    public bool Equals(Ils? other) =>
        other is not null
        && Runway == other.Runway && Category == other.Category && Designator == other.Designator
        && MagneticVariation == other.MagneticVariation && Slope == other.Slope && ReferenceDatumHeight == other.ReferenceDatumHeight
        && Components.SequenceEqual(other.Components) && Page == other.Page;

    public override int GetHashCode() => HashCode.Combine(Runway, Designator);
}

/// <summary>
/// Un apparato di un ILS, una riga di AD 2.19: il tipo com'è stampato (<c>LOC</c>, <c>GP</c>, <c>DME-P</c>, <c>OM</c>), la
/// frequenza (<c>110.30 MHZ</c>) o il canale (<c>40X</c>), dove sta, l'orario, l'elevazione dell'antenna del DME, la
/// copertura e le note com'è stampate.
/// </summary>
public sealed record IlsComponent(
    string Type,
    Position Position,
    string? Frequency = null,
    string? Channel = null,
    string? Hours = null,
    string? Elevation = null,
    string? Coverage = null,
    string? Remarks = null);
