using Vipi.Domain;

namespace Vipi.Application.Airspace;

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
/// <para><paramref name="GeneratedUtc"/> è quando AirspaceConverter ha prodotto il file, che lo scrive in
/// testa: risponde a «di quando è questo file» senza doverlo chiedere a chi lo carica. Null se il file non
/// lo dice.</para>
public sealed record AirspaceReadResult(
    IReadOnlyList<AirspaceVolumeRead> Volumes,
    IReadOnlyList<AirspaceIssue> Issues,
    int PlacemarksRead,
    DateTime? GeneratedUtc = null)
{
    /// <summary>Niente da leggere.</summary>
    public static AirspaceReadResult Vuoto { get; } = new([], [], 0);

    /// <summary>I volumi che si possono agganciare e mostrare.</summary>
    public IReadOnlyList<AirspaceVolumeRead> Usable => Volumes.Where(v => v.IsUsable).ToList();
}
