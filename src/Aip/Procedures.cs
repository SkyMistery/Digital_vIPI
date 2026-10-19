using System.Text.Json.Serialization;

namespace Aip;

/// <summary>Il genere di una procedura di AD 2: una partenza, un arrivo, una transizione verso la rotta, la salita iniziale.</summary>
public enum ProcedureKind
{
    Sid,
    Star,

    /// <summary>I segmenti di transizione verso la rotta (LIMC, LIME: «TRANSITION SEGMENTS TO ENROUTE»).</summary>
    Transition,

    /// <summary>La procedura di salita iniziale di una pista («INITIAL CLIMB PROCEDURE RWY 06»), prima delle SID.</summary>
    InitialClimb,
}

/// <summary>
/// Una procedura di AD 2 (carta F7): SID, STAR, transizione o salita iniziale, come la stampa l'AIP — la tabella dei tratti
/// per le RNAV (e le convenzionali che ce l'hanno), la prosa per le convenzionali che hanno solo quella. Tutto il testo
/// <b>com'è</b>: le quote (<c>+FL110</c>, <c>FL180/190*</c>) e le velocità (<c>-210</c>, <c>AT 200</c>) si leggono dopo, chi le
/// usa.
/// </summary>
public sealed record Procedure
{
    /// <summary>Lo scalo: <c>LIRN</c>.</summary>
    public required string Aerodrome { get; init; }

    public required ProcedureKind Kind { get; init; }

    /// <summary>
    /// Il designatore com'è stampato: <c>AGNIS 7G</c>, <c>XIBRI 5A</c>. Per una salita iniziale, la condizione com'è
    /// stampata (<c>If cleared for ALAXI 7H, SIPRO 7H or BEDPI 7H</c>) o, se non c'è, <c>RWY 06</c>.
    /// </summary>
    public required string Designator { get; init; }

    /// <summary>Le piste a cui vale, dal titolo della sezione: <c>24</c>, <c>16L</c>, <c>16R</c>.</summary>
    public IReadOnlyList<string> Runways { get; init; } = [];

    /// <summary>Il titolo della sezione com'è stampato (<c>SID RNAV1 or RNP1 RWY24 - Description</c>): dice RNAV o convenzionale.</summary>
    public string? Section { get; init; }

    /// <summary>I tratti della tabella, nell'ordine. Vuoto per una convenzionale di sola prosa.</summary>
    public IReadOnlyList<ProcedureLeg> Legs { get; init; } = [];

    /// <summary>La prosa prima della tabella (le convenzionali: «Proceed via SOSAK…»), com'è.</summary>
    public string? Description { get; init; }

    /// <summary>Le quote minime com'è stampate: «MCA/MCL: …», «MEA: …».</summary>
    public string? Minimums { get; init; }

    /// <summary>Quel che sta dopo la tabella: le note dei rimandi, la pendenza minima, gli avvisi, com'è.</summary>
    public string? Remarks { get; init; }

    /// <summary>La pagina, con la <b>sua</b> data: nello stesso file le pagine hanno date diverse.</summary>
    public AipPage? Page { get; init; }

    /// <summary>La chiave dentro uno Stato: scalo, genere, designatore e piste (lo stesso designatore vale per più piste).</summary>
    [JsonIgnore]
    public string Key => $"{Aerodrome} {Kind} {Designator}{(Runways.Count == 0 ? "" : " RWY " + string.Join("/", Runways))}";

    // ⚠️ Scritta a mano per le liste (vedi Airspace): un campo nuovo va aggiunto anche qui.
    public bool Equals(Procedure? other) =>
        other is not null
        && Aerodrome == other.Aerodrome && Kind == other.Kind && Designator == other.Designator
        && Runways.SequenceEqual(other.Runways) && Section == other.Section && Legs.SequenceEqual(other.Legs)
        && Description == other.Description && Minimums == other.Minimums && Remarks == other.Remarks && Page == other.Page;

    public override int GetHashCode() => HashCode.Combine(Aerodrome, Kind, Designator);
}

/// <summary>
/// Un tratto della tabella di una procedura, colonna per colonna com'è stampato. Il <i>path terminator</i> è quello
/// dell'ARINC 424 (<c>IF</c>, <c>TF</c>, <c>CF</c>, <c>CA</c>, <c>DF</c>, <c>RF</c>, <c>HM</c>).
/// </summary>
public sealed record ProcedureLeg
{
    public required string PathTerminator { get; init; }

    /// <summary>Il punto (<c>GEMMA</c>, <c>RN601</c>); null per i tratti che finiscono a una quota (<c>CA</c>).</summary>
    public string? Fix { get; init; }

    /// <summary>Fly-over (<c>Y</c> nella tabella).</summary>
    public bool FlyOver { get; init; }

    /// <summary>La rotta magnetica com'è stampata: <c>160°</c>.</summary>
    public string? Course { get; init; }

    /// <summary>La rotta vera, tra parentesi sotto quella magnetica: <c>163.8</c>.</summary>
    public string? TrueCourse { get; init; }

    /// <summary>La declinazione: <c>3.6°E</c>.</summary>
    public string? MagneticVariation { get; init; }

    public double? DistanceNm { get; init; }

    /// <summary>Il verso della virata: <c>L</c>, <c>R</c>.</summary>
    public string? Turn { get; init; }

    /// <summary>La quota com'è stampata: <c>+6000</c>, <c>+FL110</c>, <c>-FL170</c>, <c>FL180/190*</c>.</summary>
    public string? Altitude { get; init; }

    /// <summary>La velocità com'è stampata: <c>-210</c>, <c>AT 200</c>.</summary>
    public string? Speed { get; init; }

    /// <summary>La specifica di navigazione com'è stampata: <c>RNAV1/RNP1 (1)</c>.</summary>
    public string? Navigation { get; init; }

    /// <summary>I rimandi accanto alle celle che non sono il loro valore (<c>(2)</c> sotto il fly-over), com'è.</summary>
    public string? Notes { get; init; }

    /// <summary>Un tratto <c>RF</c> (arco a raggio costante): il punto centro dell'arco (<c>RF centre: RF620</c>).</summary>
    public string? ArcCenter { get; init; }

    /// <summary>Un tratto <c>RF</c>: il raggio dell'arco in NM (<c>r=2.1NM</c>).</summary>
    public double? ArcRadiusNm { get; init; }
}
