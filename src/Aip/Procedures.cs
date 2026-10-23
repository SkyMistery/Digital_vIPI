using System.Text.Json.Serialization;

namespace Aip;

/// <summary>
/// Il genere di una procedura di AD 2: una partenza, un arrivo, una transizione verso la rotta, la salita iniziale, un
/// avvicinamento strumentale.
/// </summary>
public enum ProcedureKind
{
    Sid,
    Star,

    /// <summary>I segmenti di transizione verso la rotta (LIMC, LIME: «TRANSITION SEGMENTS TO ENROUTE»).</summary>
    Transition,

    /// <summary>La procedura di salita iniziale di una pista («INITIAL CLIMB PROCEDURE RWY 06»), prima delle SID.</summary>
    InitialClimb,

    /// <summary>
    /// Un avvicinamento strumentale del file 5 («ILS Z RWY24 – Instrument Approach procedure», passo 6 della carta «L'AIP
    /// intero»): una procedura per segmento stampato (<see cref="Procedure.Segment"/>).
    /// </summary>
    Approach,
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

    /// <summary>
    /// Un avvicinamento: il segmento com'è stampato sopra la sua tabella (<c>RNAV to ILS TRANSITION - APPROACH FROM BENTO</c>,
    /// <c>ILS to RNAV TRANSITION - MISSED APPROACH</c>); null per la tabella senza titolo sotto quello dell'avvicinamento
    /// (il finale e il mancato), «(2)», «(3)» per le seguenti.
    /// </summary>
    public string? Segment { get; init; }

    /// <summary>Un avvicinamento: la tabella FIX/POINT (IAF, IF, FAF, MAPt con la loro definizione e le coordinate).</summary>
    public IReadOnlyList<ApproachFix>? Fixes { get; init; }

    /// <summary>Un avvicinamento RNP: il blocco SBAS FAS DATA BLOCK, una riga per riga stampata (<c>Operation Type 0</c>).</summary>
    public IReadOnlyList<string>? DataBlock { get; init; }

    /// <summary>
    /// I tratti della tabella, nell'ordine. Per una convenzionale di sola prosa, quelli ricavati dalla prosa (carta F7,
    /// slice 7b: <see cref="LegsFromDescription"/>), o vuoto dove la prosa non si è capita tutta.
    /// </summary>
    public IReadOnlyList<ProcedureLeg> Legs { get; init; } = [];

    /// <summary>
    /// I tratti non sono stampati in una tabella: li ha ricavati il lettore dalla <see cref="Description"/> (slice 7b), coi
    /// punti senza nome calcolati dalle radioassistenze. Il testo resta, ed è lui che fa fede.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool LegsFromDescription { get; init; }

    /// <summary>La prosa prima della tabella (le convenzionali: «Proceed via SOSAK…»), com'è.</summary>
    public string? Description { get; init; }

    /// <summary>Le quote minime com'è stampate: «MCA/MCL: …», «MEA: …».</summary>
    public string? Minimums { get; init; }

    /// <summary>Quel che sta dopo la tabella: le note dei rimandi, la pendenza minima, gli avvisi, com'è.</summary>
    public string? Remarks { get; init; }

    /// <summary>
    /// Le pendenze minime di salita che valgono per la procedura (segnalazioni 2611, passo 5): dal suo testo, dai blocchi della
    /// pagina che la nominano («For the following SIDs: OSKOR 7W and UVNIB 1W – 365 ft/NM…»), dalla salita iniziale della sua
    /// pista. Null se l'AIP non ne dice.
    /// </summary>
    public IReadOnlyList<ClimbGradient>? ClimbGradients { get; init; }

    /// <summary>La pagina, con la <b>sua</b> data: nello stesso file le pagine hanno date diverse.</summary>
    public AipPage? Page { get; init; }

    /// <summary>La chiave dentro uno Stato: scalo, genere, designatore e piste (lo stesso designatore vale per più piste).</summary>
    [JsonIgnore]
    public string Key => $"{Aerodrome} {Kind} {Designator}{(Runways.Count == 0 ? "" : " RWY " + string.Join("/", Runways))}{(Segment is null ? "" : " " + Segment)}";

    // ⚠️ Scritta a mano per le liste (vedi Airspace): un campo nuovo va aggiunto anche qui.
    public bool Equals(Procedure? other) =>
        other is not null
        && Aerodrome == other.Aerodrome && Kind == other.Kind && Designator == other.Designator
        && Runways.SequenceEqual(other.Runways) && Section == other.Section && Legs.SequenceEqual(other.Legs)
        && LegsFromDescription == other.LegsFromDescription
        && Description == other.Description && Minimums == other.Minimums && Remarks == other.Remarks && Page == other.Page
        && Segment == other.Segment && (Fixes ?? []).SequenceEqual(other.Fixes ?? []) && (Fixes is null) == (other.Fixes is null)
        && (DataBlock ?? []).SequenceEqual(other.DataBlock ?? []) && (DataBlock is null) == (other.DataBlock is null)
        && (ClimbGradients ?? []).SequenceEqual(other.ClimbGradients ?? []) && (ClimbGradients is null) == (other.ClimbGradients is null);

    public override int GetHashCode() => HashCode.Combine(Aerodrome, Kind, Designator);
}

/// <summary>
/// Un tratto della tabella di una procedura, colonna per colonna com'è stampato. Il <i>path terminator</i> è quello
/// dell'ARINC 424 (<c>IF</c>, <c>TF</c>, <c>CF</c>, <c>CA</c>, <c>DF</c>, <c>RF</c>, <c>HM</c>).
/// </summary>
public sealed record ProcedureLeg
{
    /// <summary>Gli avvicinamenti (file 5): il numero d'ordine del tratto, com'è stampato: <c>010</c>.</summary>
    public string? Sequence { get; init; }

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

    /// <summary>Un tratto <c>RF</c>: il raggio dell'arco in NM (<c>r=2.1NM</c>). Un <c>AF</c> (l'arco DME della prosa): la distanza.</summary>
    public double? ArcRadiusNm { get; init; }

    /// <summary>
    /// Un arco della prosa col centro calcolato, che non è un punto con nome (segnalazioni 2611, passo 6: la virata che raccorda
    /// la rotta al localizzatore, «turn left to intercept and follow IFA LOC», tangente all'una e all'altro).
    /// </summary>
    public Position? ArcCenterPosition { get; init; }

    /// <summary>
    /// Un punto senza nome della prosa (slice 7b), com'è definito: <c>RDL 258/14 NM LMT</c>, <c>INT RDL 062 VOG/RDL 164 BRL</c>.
    /// Con <see cref="Position"/>, calcolata dalle radioassistenze; <see cref="Fix"/> è null.
    /// </summary>
    public string? Computed { get; init; }

    /// <summary>La posizione calcolata di un punto senza nome (<see cref="Computed"/>).</summary>
    public Position? Position { get; init; }

    /// <summary>Gli avvicinamenti: l'angolo di discesa e l'altezza sulla soglia (VPA/TCH), com'è stampato: <c>-3.33/15.6</c>.</summary>
    public string? VerticalPath { get; init; }

    /// <summary>
    /// Gli avvicinamenti di LIRV e LIPX: le colonne di riferimento — la radioassistenza raccomandata, radiale e distanza da
    /// lei, la distanza dalla soglia —, com'è stampate, separate da « | »: <c>VIT | 223/12.1 | 16.2</c>.
    /// </summary>
    public string? Reference { get; init; }
}

/// <summary>
/// Un punto della tabella FIX/POINT di un avvicinamento: com'è stampato a sinistra (il ruolo, <c>IAF</c>, <c>FAF</c>, e la
/// definizione, <c>INT LOC INPL/9.5NM NAP DME</c>), e la posizione dalle coordinate a destra.
/// </summary>
public sealed record ApproachFix(string Text, Position? Position);

/// <summary>Perché una pendenza minima è quella: gli ostacoli, l'ATC, o non lo dice.</summary>
public enum ClimbGradientReason
{
    Unspecified,
    Obstacles,
    Atc,
}

/// <summary>
/// Una pendenza minima di salita com'è stampata: in piedi per miglio e in per cento (una delle due può mancare), fino a che
/// quota («FL90», «6000 ft», «Transition Level»), il perché, e il testo da cui viene.
/// </summary>
public sealed record ClimbGradient(double? FeetPerNm, double? Percent, string? Until, ClimbGradientReason Reason, string Text);
