using System.Text.Json.Serialization;

namespace Aip;

/// <summary>
/// Il tipo di una radioassistenza, dal testo stampato (<c>VOR/DME</c>, <c>VORTAC</c>, <c>L</c>). Quel che un importer non
/// riconosce va in <see cref="Other"/>, e <see cref="Navaid.Type"/> resta com'era.
/// </summary>
public enum NavaidKind
{
    Other,
    Vor,
    Dvor,
    Dme,
    Tacan,
    VorDme,
    DvorDme,

    /// <summary>VOR terminale con DME (<c>TVOR/DME</c>).</summary>
    TvorDme,
    Vortac,
    Dvortac,
    Ndb,

    /// <summary>Radiofaro di avvicinamento (locator), che l'AIP italiano scrive <c>L</c>.</summary>
    Locator,
}

/// <summary>
/// Una radioassistenza di ENR 4.1 (carta F6, slice 3), come la stampa l'AIP: chi è, dove sta, su che frequenza, quanto
/// copre. Un impianto di più apparati (VOR e DME, VOR e TACAN) è <b>una</b> radioassistenza con un apparato per riga in
/// <see cref="Components"/>: ognuno ha la sua posizione e il suo orario (il DME di un VOR/DME sta di solito a un secondo
/// d'arco dal VOR).
/// </summary>
public sealed record Navaid
{
    /// <summary>La sezione dell'AIP: <c>ENR 4.1</c>.</summary>
    public required string Section { get; init; }

    /// <summary>
    /// Fuori dalla parte AD perché militare (in Italia ENR 4.1.2, «radioassistenze militari non contenute nella parte
    /// AD»): il militare che è nell'AIP lo portiamo noi (committente, 18 ottobre).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Military { get; init; }

    /// <summary>La sigla, com'è stampata: <c>ALG</c>. 🔴 Non è una chiave da sola: <c>PIS</c> è un DVOR/DME e un locator.</summary>
    public required string Designator { get; init; }

    /// <summary>Il nome della stazione: <c>ALGHERO</c>, <c>CATANIA/ Fontanarossa</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Il tipo com'è stampato: <c>VOR/DME</c>.</summary>
    public required string Type { get; init; }

    public NavaidKind Kind { get; init; }

    /// <summary>La declinazione del VOR com'è stampata, con l'anno: <c>1° E-2005.0</c>.</summary>
    public string? MagneticVariation { get; init; }

    /// <summary>La frequenza com'è stampata, unità compresa: <c>113.80 MHZ</c>, <c>390.00 KHZ</c>.</summary>
    public string? Frequency { get; init; }

    /// <summary>Il canale del DME o del TACAN: <c>85X</c>.</summary>
    public string? Channel { get; init; }

    /// <summary>La posizione dell'apparato principale (il primo della colonna delle coordinate).</summary>
    public required Position Position { get; init; }

    /// <summary>Gli apparati, nell'ordine stampato. Uno solo per un NDB o un TACAN.</summary>
    public required IReadOnlyList<NavaidComponent> Components { get; init; }

    /// <summary>L'elevazione dell'antenna del DME com'è stampata (<c>443 M AMSL</c>); vuota dove l'AIP dice NIL.</summary>
    public string? Elevation { get; init; }

    /// <summary>La copertura operativa nominale e le limitazioni, com'è stampata: un settore per riga.</summary>
    public string? Coverage { get; init; }

    /// <summary>L'interesse per il FRAIT, com'è stampato: <c>FRA(IA): LIEO</c>.</summary>
    public string? Fra { get; init; }

    /// <summary>Le note dell'AIP (manutenzione, limitazioni, identificazione): una nota per riga.</summary>
    public string? Remarks { get; init; }

    public AipPage? Page { get; init; }

    /// <summary>La chiave dentro uno Stato: sigla <b>più</b> tipo (<c>PIS VOR/DME</c> e <c>PIS L</c>).</summary>
    [JsonIgnore]
    public string Key => $"{Designator} {Type}";

    // ⚠️ Scritta a mano: l'uguaglianza dei record confronterebbe la lista degli apparati per riferimento.
    public bool Equals(Navaid? other) =>
        other is not null
        && Section == other.Section && Military == other.Military && Designator == other.Designator && Name == other.Name
        && Type == other.Type && Kind == other.Kind && MagneticVariation == other.MagneticVariation
        && Frequency == other.Frequency && Channel == other.Channel && Position == other.Position
        && Components.SequenceEqual(other.Components)
        && Elevation == other.Elevation && Coverage == other.Coverage && Fra == other.Fra && Remarks == other.Remarks
        && Page == other.Page;

    public override int GetHashCode() => HashCode.Combine(Section, Designator, Type);
}

/// <summary>
/// Un apparato di una radioassistenza: <paramref name="Equipment"/> com'è stampato (<c>VOR</c>, <c>DME</c>, <c>TACAN</c>;
/// vuoto quando la radioassistenza ne ha uno solo e l'AIP non lo nomina), la sua posizione e il suo orario (<c>H24</c>).
/// </summary>
public sealed record NavaidComponent(string? Equipment, Position Position, string? Hours = null);

/// <summary>
/// Un punto significativo di ENR 4.4 (designatori a cinque lettere): dove sta, e dove l'AIP dice che si usa — le rotte
/// ATS, le procedure, l'interesse per il FRAIT.
/// </summary>
public sealed record SignificantPoint
{
    /// <summary>La sezione dell'AIP: <c>ENR 4.4</c>.</summary>
    public required string Section { get; init; }

    /// <summary>Il designatore: <c>ABADI</c>.</summary>
    public required string Designator { get; init; }

    public required Position Position { get; init; }

    /// <summary>Le rotte ATS che lo usano, una per voce (<c>P92</c>); vuota dove l'AIP dice NIL.</summary>
    public IReadOnlyList<string> Routes { get; init; } = [];

    /// <summary>Le procedure che lo usano, com'è stampato: <c>BR star, BR sid</c>.</summary>
    public string? Procedures { get; init; }

    /// <summary>L'interesse per il FRAIT, com'è stampato: <c>FRA(X): FRA(D): LIMC, LIME</c>.</summary>
    public string? Fra { get; init; }

    public string? Remarks { get; init; }

    public AipPage? Page { get; init; }

    public bool Equals(SignificantPoint? other) =>
        other is not null
        && Section == other.Section && Designator == other.Designator && Position == other.Position
        && Routes.SequenceEqual(other.Routes) && Procedures == other.Procedures && Fra == other.Fra
        && Remarks == other.Remarks && Page == other.Page;

    public override int GetHashCode() => HashCode.Combine(Section, Designator);
}

/// <summary>
/// Una luce aeronautica al suolo in rotta (ENR 4.5): in Italia i fari marittimi che servono anche al volo. La posizione
/// non c'è sempre (l'AIP non la stampa per tutte).
/// </summary>
public sealed record GroundLight
{
    /// <summary>La sezione dell'AIP: <c>ENR 4.5</c>.</summary>
    public required string Section { get; init; }

    /// <summary>Il nome com'è stampato: <c>CAPO DELLE MELE</c>, <c>GIANNUTRI (Isola di)</c>.</summary>
    public required string Name { get; init; }

    public Position? Position { get; init; }

    /// <summary>Il tipo com'è stampato: <c>Marittimo / Maritime</c>.</summary>
    public string? Type { get; init; }

    /// <summary>Le caratteristiche e il codice, com'è stampato: <c>Gruppi di 3 lampi bianchi ogni 15''</c>.</summary>
    public string? Characteristics { get; init; }

    public string? Hours { get; init; }

    /// <summary>L'intensità in migliaia di candele, com'è stampata: <c>668,8</c>.</summary>
    public string? Intensity { get; init; }

    public string? Remarks { get; init; }

    public AipPage? Page { get; init; }
}
