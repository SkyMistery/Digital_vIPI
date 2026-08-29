namespace Vipi.Application.Airspace;

/// <summary>
/// La famiglia di uno spazio aereo: che <b>cosa</b> è, non di che classe. È il perno di tutto il catalogo —
/// decide che cosa si può agganciare a un settore e che cosa si può mostrare (<see cref="AirspaceFamilies"/>).
///
/// <para>⚠️ <b>In coda si aggiunge, in mezzo mai.</b> Come ogni enum di questa applicazione può finire in un
/// payload serializzato come <b>ordinale</b>: inserire un valore in mezzo rinumera tutti quelli dopo.</para>
/// </summary>
public enum AirspaceFamily
{
    /// <summary>Zona di controllo d'aeroporto (CTR).</summary>
    Ctr,

    /// <summary>Area di controllo (CTA), incluse le classi A/C/D che non sono né CTR né ATZ.</summary>
    Cta,

    /// <summary>Area terminale di manovra (TMA).</summary>
    Tma,

    /// <summary>Zona di traffico d'aeroporto, civile (ATZ) o militare (MATZ).</summary>
    Atz,

    /// <summary>Regione informazioni volo (FIR).</summary>
    Fir,

    /// <summary>Zona a transponder obbligatorio (TMZ/FMC).</summary>
    Tmz,

    /// <summary>Area regolamentata: <c>R</c>.</summary>
    Restricted,

    /// <summary>Area vietata: <c>P</c>.</summary>
    Prohibited,

    /// <summary>Area pericolosa: <c>D</c>.</summary>
    Danger,

    /// <summary>Area di volo a vela.</summary>
    Gliding,

    /// <summary>Tutto il resto: acrobazia, airwork, parchi e riserve, TRA, e ciò che non si riconosce.</summary>
    Other,
}

/// <summary>Rispetto a che cosa è misurata una quota.</summary>
public enum AirspaceDatum
{
    /// <summary>Il suolo (<c>GND</c>/<c>SFC</c>).</summary>
    Gnd,

    /// <summary>Piedi sul livello del mare (<c>AMSL</c>).</summary>
    Amsl,

    /// <summary>Piedi sul terreno (<c>AGL</c>).</summary>
    Agl,

    /// <summary>Livello di volo (<c>FL</c>).</summary>
    FlightLevel,

    /// <summary>Illimitato (<c>UNL</c>). Nel file dell'AIP si scrive <c>FL999</c>.</summary>
    Unlimited,
}

/// <summary>
/// Una quota del file: che cosa dice alla lettera (<paramref name="Raw"/>) e che cosa vale.
///
/// <para><paramref name="Feet"/> è la misura <b>confrontabile</b>, in piedi, e per un livello di volo è
/// FL×100. Serve a ordinare e a dare una banda al 3D; <c>null</c> solo per l'illimitato, che un numero non
/// ce l'ha. ⚠️ Confrontare piedi AMSL con piedi AGL resta un confronto fra cose diverse: il riferimento non
/// si perde per strada proprio perché serve a chi legge.</para>
/// </summary>
public sealed record AirspaceLevel(AirspaceDatum Datum, int? Feet, string Raw)
{
    /// <summary>Il suolo, quando il file non dice niente.</summary>
    public static AirspaceLevel Suolo { get; } = new(AirspaceDatum.Gnd, 0, "GND");
}

/// <summary>Che cosa il lettore non è riuscito a fare, o ha fatto con una scelta che va dichiarata.</summary>
/// <remarks>⚠️ È un <b>codice</b>, non un messaggio: il testo lo scrive la UI, nelle due lingue.</remarks>
public enum AirspaceIssueKind
{
    /// <summary>Il file non si è aperto (zip, XML, tetti).</summary>
    FileNonLetto,

    /// <summary>Il volume non ha un anello utilizzabile: nessun poligono a quota costante, o meno di tre punti.</summary>
    VolumeSenzaAnello,

    /// <summary>Il volume non ha un nome, e senza nome non ha nemmeno un'identità.</summary>
    VolumeSenzaNome,

    /// <summary>Due volumi con la stessa chiave naturale: il secondo prende un ordinale.</summary>
    ChiaveDuplicata,

    /// <summary>La quota non si è riconosciuta: si è tenuto il testo, non il numero.</summary>
    QuotaNonLetta,

    /// <summary>Il volume ha più di un anello distinto: si tengono tutti, e lo si dice.</summary>
    VolumeAPiuAnelli,
}

/// <summary>Una segnalazione del lettore, ancorata al nome del volume quando ce l'ha.</summary>
public sealed record AirspaceIssue(AirspaceIssueKind Kind, string Volume, string? Dettaglio = null);

/// <summary>
/// Un volume di spazio aereo letto dal file: che cos'è, fin dove arriva, e il suo contorno.
///
/// <para><paramref name="Rings"/> sono gli anelli <b>distinti</b> del volume, ognuno in ordine e <b>senza</b>
/// ripetere il vertice di chiusura. Nel file misurato il 29 agosto 2026 sono sempre esattamente uno su tutti e
/// 1 536 i volumi — il tetto e il pavimento sono lo stesso contorno a due quote — ma il tipo è una lista
/// perché un volume in più parti è una cosa che esiste, e scoprirlo tenendo solo il primo pezzo sarebbe un
/// confine sbagliato disegnato in silenzio.</para>
///
/// <para><paramref name="NaturalKey"/> è l'identità: <c>famiglia|nome|base|tetto</c>. ⚠️ Il nome da solo non
/// basta — nel file <c>GRAZZANISE CTR Z2</c> compare due volte con bande diverse. E nemmeno la chiave basta
/// sempre: <c>CTA ROMA Z9 GOLFO MANFREDONIA</c> è duplicato identico, e il secondo prende
/// <paramref name="Ordinal"/> = 1.</para>
/// </summary>
public sealed record AirspaceVolumeRead(
    AirspaceFamily Family,
    string Name,
    string Category,
    string? AirspaceClass,
    AirspaceLevel Base,
    AirspaceLevel Top,
    IReadOnlyList<IReadOnlyList<(double Lat, double Lon)>> Rings,
    string NaturalKey,
    int Ordinal = 0)
{
    /// <summary>Quanti vertici in tutto: quel che si dice a chi ha appena caricato il file.</summary>
    public int PointCount => Rings.Sum(r => r.Count);

    /// <summary>Si può agganciare a un settore e mostrare? Lo decide la famiglia.</summary>
    public bool IsUsable => AirspaceFamilies.IsUsable(Family);
}

/// <summary>
/// L'esito di una lettura: i volumi, che cosa non ha funzionato, e quanti Placemark si sono guardati.
/// <paramref name="PlacemarksRead"/> conta gli spazi aerei incontrati, non i punti d'appoggio (aeroporti,
/// VOR, NDB), che questo lettore ignora.
/// </summary>
public sealed record AirspaceReadResult(
    IReadOnlyList<AirspaceVolumeRead> Volumes,
    IReadOnlyList<AirspaceIssue> Issues,
    int PlacemarksRead)
{
    /// <summary>Niente da leggere.</summary>
    public static AirspaceReadResult Vuoto { get; } = new([], [], 0);

    /// <summary>I volumi che si possono agganciare e mostrare.</summary>
    public IReadOnlyList<AirspaceVolumeRead> Usable => Volumes.Where(v => v.IsUsable).ToList();
}
